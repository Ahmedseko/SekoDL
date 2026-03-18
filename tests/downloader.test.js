'use strict';

const fs = require('fs');
const path = require('path');
const os = require('os');
const http = require('http');
const { downloadFile, downloadWithRetry, downloadAll } = require('../src/downloader');

// ─── helpers ────────────────────────────────────────────────────────────────

function createTempDir() {
  return fs.mkdtempSync(path.join(os.tmpdir(), 'sekodl-test-'));
}

function startServer(handler) {
  return new Promise((resolve) => {
    const server = http.createServer(handler);
    server.listen(0, '127.0.0.1', () => {
      resolve(server);
    });
  });
}

function serverUrl(server, pathname = '/') {
  const { port } = server.address();
  return `http://127.0.0.1:${port}${pathname}`;
}

// ─── downloadFile ───────────────────────────────────────────────────────────

describe('downloadFile', () => {
  let server;
  let tmpDir;

  beforeEach(async () => {
    tmpDir = createTempDir();
  });

  afterEach(async () => {
    if (server) {
      await new Promise((r) => server.close(r));
      server = null;
    }
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  test('downloads a simple file', async () => {
    const content = 'Hello, SekoDL!';
    server = await startServer((_req, res) => {
      res.writeHead(200, {
        'Content-Type': 'text/plain',
        'Content-Length': Buffer.byteLength(content),
      });
      res.end(content);
    });

    const dest = path.join(tmpDir, 'hello.txt');
    const result = await downloadFile(serverUrl(server), dest);

    expect(result.size).toBe(Buffer.byteLength(content));
    expect(fs.readFileSync(dest, 'utf8')).toBe(content);
  });

  test('reports progress via onProgress callback', async () => {
    const content = Buffer.alloc(1024, 'x');
    server = await startServer((_req, res) => {
      res.writeHead(200, {
        'Content-Length': content.length,
      });
      res.end(content);
    });

    const dest = path.join(tmpDir, 'progress.bin');
    const progressCalls = [];
    await downloadFile(serverUrl(server), dest, {
      onProgress: (dl, total) => progressCalls.push({ dl, total }),
    });

    expect(progressCalls.length).toBeGreaterThan(0);
    const last = progressCalls[progressCalls.length - 1];
    expect(last.dl).toBe(content.length);
    expect(last.total).toBe(content.length);
  });

  test('follows HTTP redirects', async () => {
    const content = 'redirected!';
    // We need a single server that handles both /redirect and /target
    server = await startServer((req, res) => {
      if (req.url === '/redirect') {
        const target = serverUrl(server, '/target');
        res.writeHead(302, { Location: target });
        res.end();
      } else {
        res.writeHead(200, { 'Content-Type': 'text/plain' });
        res.end(content);
      }
    });

    const dest = path.join(tmpDir, 'redirected.txt');
    const result = await downloadFile(serverUrl(server, '/redirect'), dest);
    expect(fs.readFileSync(dest, 'utf8')).toBe(content);
    expect(result.size).toBe(Buffer.byteLength(content));
  });

  test('rejects on non-200 status', async () => {
    server = await startServer((_req, res) => {
      res.writeHead(404, 'Not Found');
      res.end();
    });

    const dest = path.join(tmpDir, 'notfound.txt');
    await expect(downloadFile(serverUrl(server), dest)).rejects.toThrow('HTTP 404');
  });

  test('respects Content-Disposition filename', async () => {
    const content = 'data';
    server = await startServer((_req, res) => {
      res.writeHead(200, {
        'Content-Disposition': 'attachment; filename="server_name.txt"',
      });
      res.end(content);
    });

    const dest = path.join(tmpDir, 'original.txt');
    const result = await downloadFile(serverUrl(server), dest);
    expect(result.filename).toBe('server_name.txt');
  });
});

// ─── downloadWithRetry ───────────────────────────────────────────────────────

describe('downloadWithRetry', () => {
  let server;
  let tmpDir;

  beforeEach(() => {
    tmpDir = createTempDir();
  });

  afterEach(async () => {
    if (server) {
      await new Promise((r) => server.close(r));
      server = null;
    }
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  test('succeeds on first attempt', async () => {
    server = await startServer((_req, res) => {
      res.writeHead(200);
      res.end('ok');
    });

    const dest = path.join(tmpDir, 'ok.txt');
    const result = await downloadWithRetry(serverUrl(server), dest, { retries: 3 });
    expect(result.size).toBe(2);
  });

  test('retries and eventually succeeds', async () => {
    let attempts = 0;
    server = await startServer((_req, res) => {
      attempts++;
      if (attempts < 3) {
        res.destroy(); // simulate network error
      } else {
        res.writeHead(200);
        res.end('finally');
      }
    });

    const dest = path.join(tmpDir, 'retry.txt');
    const result = await downloadWithRetry(serverUrl(server), dest, { retries: 5 });
    expect(attempts).toBe(3);
    expect(fs.readFileSync(dest, 'utf8')).toBe('finally');
    expect(result.size).toBeGreaterThan(0);
  });

  test('throws after exhausting retries', async () => {
    server = await startServer((_req, res) => {
      res.destroy();
    });

    const dest = path.join(tmpDir, 'fail.txt');
    await expect(
      downloadWithRetry(serverUrl(server), dest, { retries: 2 }),
    ).rejects.toThrow();
  });
});

// ─── downloadAll ────────────────────────────────────────────────────────────

describe('downloadAll', () => {
  let server;
  let tmpDir;

  beforeEach(() => {
    tmpDir = createTempDir();
  });

  afterEach(async () => {
    if (server) {
      await new Promise((r) => server.close(r));
      server = null;
    }
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  test('downloads multiple files concurrently', async () => {
    server = await startServer((req, res) => {
      const body = `content-of${req.url}`;
      res.writeHead(200, { 'Content-Length': Buffer.byteLength(body) });
      res.end(body);
    });

    const downloads = ['/a', '/b', '/c'].map((p, i) => ({
      id: i,
      url: serverUrl(server, p),
      filename: `file${i}.txt`,
      outputPath: path.join(tmpDir, `file${i}.txt`),
    }));

    const { successes, failures } = await downloadAll(downloads, { concurrency: 3, retries: 1 });

    expect(failures).toHaveLength(0);
    expect(successes).toHaveLength(3);
    downloads.forEach((d) => {
      expect(fs.existsSync(d.outputPath)).toBe(true);
    });
  });

  test('calls onComplete for each success', async () => {
    server = await startServer((_req, res) => {
      res.writeHead(200);
      res.end('data');
    });

    const completed = [];
    const downloads = [0, 1].map((i) => ({
      id: i,
      url: serverUrl(server),
      filename: `f${i}.txt`,
      outputPath: path.join(tmpDir, `f${i}.txt`),
    }));

    await downloadAll(downloads, {
      concurrency: 2,
      retries: 1,
      onComplete: (item) => completed.push(item.id),
    });

    expect(completed.sort()).toEqual([0, 1]);
  });

  test('calls onError for failed downloads', async () => {
    server = await startServer((_req, res) => {
      res.writeHead(500, 'Internal Server Error');
      res.end();
    });

    const errors = [];
    const downloads = [
      {
        id: 0,
        url: serverUrl(server),
        filename: 'bad.txt',
        outputPath: path.join(tmpDir, 'bad.txt'),
      },
    ];

    const { failures } = await downloadAll(downloads, {
      retries: 1,
      onError: (item, err) => errors.push({ item, err }),
    });

    expect(failures).toHaveLength(1);
    expect(errors).toHaveLength(1);
    expect(errors[0].err.message).toMatch('HTTP 500');
  });
});
