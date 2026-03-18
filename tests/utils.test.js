'use strict';

const {
  getFilenameFromUrl,
  getFilenameFromHeaders,
  formatBytes,
  formatSpeed,
  formatTime,
} = require('../src/utils');

describe('getFilenameFromUrl', () => {
  test('returns filename from simple URL', () => {
    expect(getFilenameFromUrl('https://example.com/files/photo.jpg')).toBe('photo.jpg');
  });

  test('returns filename with no path extension', () => {
    expect(getFilenameFromUrl('https://example.com/download')).toBe('download');
  });

  test('strips query-string artefacts', () => {
    expect(getFilenameFromUrl('https://example.com/file.zip?token=abc')).toBe('file.zip');
  });

  test('returns "download" for root path', () => {
    expect(getFilenameFromUrl('https://example.com/')).toBe('download');
  });

  test('returns "download" for invalid URL', () => {
    expect(getFilenameFromUrl('not-a-url')).toBe('download');
  });
});

describe('getFilenameFromHeaders', () => {
  test('returns null when header is absent', () => {
    expect(getFilenameFromHeaders({})).toBeNull();
  });

  test('extracts plain filename', () => {
    expect(
      getFilenameFromHeaders({ 'content-disposition': 'attachment; filename="report.pdf"' }),
    ).toBe('report.pdf');
  });

  test('extracts filename without quotes', () => {
    expect(
      getFilenameFromHeaders({ 'content-disposition': 'attachment; filename=archive.tar.gz' }),
    ).toBe('archive.tar.gz');
  });

  test('prefers filename* over filename', () => {
    expect(
      getFilenameFromHeaders({
        'content-disposition': "attachment; filename=\"old.txt\"; filename*=UTF-8''new%20file.txt",
      }),
    ).toBe('new file.txt');
  });
});

describe('formatBytes', () => {
  test('formats 0 bytes', () => {
    expect(formatBytes(0)).toBe('0 B');
  });

  test('formats bytes', () => {
    expect(formatBytes(512)).toBe('512 B');
  });

  test('formats kilobytes', () => {
    expect(formatBytes(1024)).toBe('1 KB');
  });

  test('formats megabytes', () => {
    expect(formatBytes(1024 * 1024)).toBe('1 MB');
  });

  test('formats gigabytes', () => {
    expect(formatBytes(1024 * 1024 * 1024)).toBe('1 GB');
  });

  test('handles negative / non-finite input gracefully', () => {
    expect(formatBytes(-1)).toBe('0 B');
    expect(formatBytes(NaN)).toBe('0 B');
    expect(formatBytes(Infinity)).toBe('0 B');
  });
});

describe('formatSpeed', () => {
  test('appends /s suffix', () => {
    expect(formatSpeed(1024)).toBe('1 KB/s');
  });
});

describe('formatTime', () => {
  test('formats seconds only', () => {
    expect(formatTime(45)).toBe('00:45');
  });

  test('formats minutes and seconds', () => {
    expect(formatTime(125)).toBe('02:05');
  });

  test('formats hours', () => {
    expect(formatTime(3665)).toBe('1:01:05');
  });

  test('returns --:-- for Infinity', () => {
    expect(formatTime(Infinity)).toBe('--:--');
  });

  test('returns --:-- for negative values', () => {
    expect(formatTime(-5)).toBe('--:--');
  });

  test('returns --:-- for NaN', () => {
    expect(formatTime(NaN)).toBe('--:--');
  });
});
