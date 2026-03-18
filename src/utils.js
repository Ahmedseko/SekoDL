'use strict';

const path = require('path');
const { URL } = require('url');

/**
 * Extract a filename from a URL pathname.
 * @param {string} urlString
 * @returns {string}
 */
function getFilenameFromUrl(urlString) {
  try {
    const parsed = new URL(urlString);
    const basename = path.basename(parsed.pathname);
    // Remove query-string artifacts that may have bled into the basename
    return basename.split('?')[0] || 'download';
  } catch {
    return 'download';
  }
}

/**
 * Extract a filename from a Content-Disposition response header.
 * @param {object} headers  Node http.IncomingMessage headers
 * @returns {string|null}
 */
function getFilenameFromHeaders(headers) {
  const disposition = headers['content-disposition'];
  if (!disposition) return null;
  // RFC 6266 – prefer filename* (encoded), fall back to filename
  const encodedMatch = disposition.match(/filename\*\s*=\s*(?:[^']*'')?([^;]+)/i);
  if (encodedMatch) {
    try {
      return decodeURIComponent(encodedMatch[1].trim().replace(/['"]/g, ''));
    } catch {
      // fall through
    }
  }
  const plainMatch = disposition.match(/filename\s*=\s*(?:"([^"]+)"|([^;]+))/i);
  if (plainMatch) {
    return (plainMatch[1] || plainMatch[2]).trim();
  }
  return null;
}

/**
 * Format a byte count into a human-readable string.
 * @param {number} bytes
 * @param {number} [decimals=2]
 * @returns {string}
 */
function formatBytes(bytes, decimals = 2) {
  if (!Number.isFinite(bytes) || bytes < 0) return '0 B';
  if (bytes === 0) return '0 B';
  const k = 1024;
  const dm = Math.max(0, decimals);
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.min(Math.floor(Math.log(bytes) / Math.log(k)), sizes.length - 1);
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(dm))} ${sizes[i]}`;
}

/**
 * Format a transfer speed in bytes/s.
 * @param {number} bytesPerSecond
 * @returns {string}
 */
function formatSpeed(bytesPerSecond) {
  return `${formatBytes(bytesPerSecond)}/s`;
}

/**
 * Format seconds into mm:ss or hh:mm:ss.
 * @param {number} seconds
 * @returns {string}
 */
function formatTime(seconds) {
  if (!Number.isFinite(seconds) || seconds < 0) return '--:--';
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = Math.floor(seconds % 60);
  if (h > 0) {
    return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
  }
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

module.exports = {
  getFilenameFromUrl,
  getFilenameFromHeaders,
  formatBytes,
  formatSpeed,
  formatTime,
};
