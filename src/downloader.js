'use strict';

const fs = require('fs');
const http = require('http');
const https = require('https');
const { getFilenameFromHeaders } = require('./utils');

const MAX_REDIRECTS = 10;

/**
 * Download a single URL to a file path.
 *
 * @param {string}   url         Target URL
 * @param {string}   outputPath  Destination file path
 * @param {object}   [opts]
 * @param {object}   [opts.headers]                  Extra request headers
 * @param {number}   [opts.timeout=30000]             Socket timeout (ms)
 * @param {function} [opts.onProgress]               Called with (downloaded, total|0)
 * @param {number}   [opts.maxRedirects=10]
 * @returns {Promise<{url, outputPath, size, filename}>}
 */
function downloadFile(url, outputPath, opts = {}) {
  const {
    headers = {},
    timeout = 30000,
    onProgress = null,
    maxRedirects = MAX_REDIRECTS,
  } = opts;

  return new Promise((resolve, reject) => {
    let redirectsLeft = maxRedirects;

    function doRequest(currentUrl, currentOutputPath) {
      const protocol = currentUrl.startsWith('https') ? https : http;
      const reqOptions = { headers };

      const req = protocol.get(currentUrl, reqOptions, (res) => {
        // Follow redirects
        if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
          if (redirectsLeft-- <= 0) {
            return reject(new Error('Too many redirects'));
          }
          // Consume the redirect body to free the socket
          res.resume();
          let nextUrl = res.headers.location;
          // Resolve relative redirects
          if (!nextUrl.startsWith('http')) {
            const base = new URL(currentUrl);
            nextUrl = new URL(nextUrl, base).href;
          }
          return doRequest(nextUrl, currentOutputPath);
        }

        if (res.statusCode !== 200) {
          res.resume();
          return reject(new Error(`HTTP ${res.statusCode}: ${res.statusMessage}`));
        }

        // Try to get a better filename from Content-Disposition
        const dispositionFilename = getFilenameFromHeaders(res.headers);
        const resolvedOutputPath = dispositionFilename
          ? currentOutputPath.replace(/[^/\\]+$/, dispositionFilename)
          : currentOutputPath;

        const total = parseInt(res.headers['content-length'] || '0', 10);
        let downloaded = 0;

        const fileStream = fs.createWriteStream(resolvedOutputPath);

        res.on('data', (chunk) => {
          downloaded += chunk.length;
          if (onProgress) {
            onProgress(downloaded, total);
          }
        });

        res.pipe(fileStream);

        fileStream.on('finish', () => {
          fileStream.close();
          resolve({
            url: currentUrl,
            outputPath: resolvedOutputPath,
            size: downloaded,
            filename: require('path').basename(resolvedOutputPath),
          });
        });

        fileStream.on('error', (err) => {
          fs.unlink(resolvedOutputPath, () => {});
          reject(err);
        });

        res.on('error', (err) => {
          fs.unlink(resolvedOutputPath, () => {});
          reject(err);
        });
      });

      req.on('error', reject);

      req.setTimeout(timeout, () => {
        req.destroy(new Error('Request timed out'));
      });
    }

    doRequest(url, outputPath);
  });
}

/**
 * Download a file with automatic retry on failure.
 *
 * @param {string}   url
 * @param {string}   outputPath
 * @param {object}   [opts]
 * @param {number}   [opts.retries=3]  Maximum number of attempts (1 = no retry)
 * @returns {Promise<object>}
 */
async function downloadWithRetry(url, outputPath, opts = {}) {
  const { retries = 3, ...rest } = opts;
  let lastError;
  for (let attempt = 1; attempt <= retries; attempt++) {
    try {
      return await downloadFile(url, outputPath, rest);
    } catch (err) {
      lastError = err;
      if (attempt < retries) {
        // Exponential back-off: 1 s, 2 s, 4 s …
        await sleep(1000 * Math.pow(2, attempt - 1));
      }
    }
  }
  throw lastError;
}

/**
 * Download a batch of items with bounded concurrency.
 *
 * Each item in `downloads` must have: { id, url, outputPath, filename }
 *
 * @param {object[]} downloads
 * @param {object}   [opts]
 * @param {number}   [opts.concurrency=3]
 * @param {number}   [opts.retries=3]
 * @param {number}   [opts.timeout=30000]
 * @param {object}   [opts.headers={}]
 * @param {function} [opts.onProgress]   (item, downloaded, total) => void
 * @param {function} [opts.onComplete]   (item, result) => void
 * @param {function} [opts.onError]      (item, error) => void
 * @returns {Promise<{successes: object[], failures: object[]}>}
 */
async function downloadAll(downloads, opts = {}) {
  const {
    concurrency = 3,
    retries = 3,
    timeout = 30000,
    headers = {},
    onProgress = null,
    onComplete = null,
    onError = null,
  } = opts;

  const queue = [...downloads];
  const successes = [];
  const failures = [];

  async function worker() {
    while (queue.length > 0) {
      const item = queue.shift();
      if (!item) break;
      try {
        const result = await downloadWithRetry(item.url, item.outputPath, {
          retries,
          timeout,
          headers,
          onProgress: onProgress ? (dl, total) => onProgress(item, dl, total) : null,
        });
        successes.push({ item, result });
        if (onComplete) onComplete(item, result);
      } catch (err) {
        failures.push({ item, error: err });
        if (onError) onError(item, err);
      }
    }
  }

  const workerCount = Math.min(concurrency, downloads.length);
  await Promise.all(Array.from({ length: workerCount }, () => worker()));

  return { successes, failures };
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

module.exports = { downloadFile, downloadWithRetry, downloadAll };
