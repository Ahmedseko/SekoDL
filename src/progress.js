'use strict';

const cliProgress = require('cli-progress');
const chalk = require('chalk');
const { formatBytes, formatSpeed, formatTime } = require('./utils');

/**
 * Manages one progress bar per active download.
 * Falls back gracefully when the terminal is not a TTY (e.g. piped output).
 */
class ProgressManager {
  constructor() {
    this._bars = new Map();
    this._startTimes = new Map();
    this._lastBytes = new Map();
    this._lastUpdate = new Map();

    this._multi = new cliProgress.MultiBar(
      {
        clearOnComplete: false,
        hideCursor: true,
        forceRedraw: false,
        stream: process.stderr,
        format: this._formatBar.bind(this),
      },
      cliProgress.Presets.shades_grey,
    );
  }

  _formatBar(_options, params, payload) {
    const BAR_LEN = 25;
    const progress = Math.min(1, params.progress || 0);
    const filled = Math.round(progress * BAR_LEN);
    const empty = BAR_LEN - filled;
    const bar =
      chalk.green('\u2588'.repeat(filled)) + chalk.gray('\u2591'.repeat(empty));

    const name = chalk.cyan(
      (payload.filename || 'download').substring(0, 28).padEnd(28),
    );
    const pct = chalk.yellow(`${Math.floor(progress * 100)}%`.padStart(4));

    if (!params.total || params.total <= 0) {
      // Unknown total – show spinner-style bar
      return `${name} ${bar} ${pct} ${formatBytes(params.value || 0).padStart(10)} ${chalk.blue(formatSpeed(payload.speed || 0).padStart(12))}`;
    }

    const sizeStr = `${formatBytes(params.value || 0)}/${formatBytes(params.total)}`;
    const speedStr = chalk.blue(formatSpeed(payload.speed || 0).padStart(12));
    const etaStr = chalk.magenta(formatTime(payload.eta).padStart(8));
    return `${name} ${bar} ${pct} ${sizeStr.padStart(22)} ${speedStr} ETA:${etaStr}`;
  }

  /**
   * Register a new download and create its progress bar.
   * @param {string|number} id       Unique identifier for this download
   * @param {string}        filename Display name
   * @param {number}        total    Expected size in bytes (0 if unknown)
   */
  add(id, filename, total = 0) {
    const bar = this._multi.create(total || 0, 0, {
      filename,
      speed: 0,
      eta: Infinity,
    });
    this._bars.set(id, bar);
    this._startTimes.set(id, Date.now());
    this._lastBytes.set(id, 0);
    this._lastUpdate.set(id, Date.now());
    return bar;
  }

  /**
   * Update a bar's progress.
   * @param {string|number} id
   * @param {number}        downloaded  Bytes received so far
   * @param {number}        total       Total bytes (may be 0 if unknown)
   */
  update(id, downloaded, total) {
    const bar = this._bars.get(id);
    if (!bar) return;

    const now = Date.now();
    const elapsed = (now - (this._lastUpdate.get(id) || now)) / 1000;

    // Throttle updates to ~10 fps
    if (elapsed < 0.1) return;

    const lastBytes = this._lastBytes.get(id) || 0;
    const speed = elapsed > 0 ? (downloaded - lastBytes) / elapsed : 0;
    const remaining = total > 0 && speed > 0 ? (total - downloaded) / speed : Infinity;

    if (total > 0) bar.setTotal(total);
    bar.update(downloaded, { speed, eta: remaining });

    this._lastBytes.set(id, downloaded);
    this._lastUpdate.set(id, now);
  }

  /**
   * Mark a download as complete (or failed).
   * @param {string|number} id
   */
  complete(id) {
    const bar = this._bars.get(id);
    if (!bar) return;
    const total = bar.getTotal();
    const value = total > 0 ? total : bar.getValue();
    bar.update(value, { speed: 0, eta: 0 });
    bar.stop();
    this._bars.delete(id);
  }

  /** Stop all bars and clean up. */
  stop() {
    this._multi.stop();
  }
}

module.exports = ProgressManager;
