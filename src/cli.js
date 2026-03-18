'use strict';

const { Command } = require('commander');
const path = require('path');
const fs = require('fs');
const chalk = require('chalk');

const { downloadAll } = require('./downloader');
const ProgressManager = require('./progress');
const { getFilenameFromUrl, formatBytes } = require('./utils');

const program = new Command();

program
  .name('sekodl')
  .description('Terminal download manager – download files from the command line')
  .version('1.0.0')
  .argument('<urls...>', 'One or more URLs to download')
  .option('-o, --output <filename>', 'Output filename (single URL only)')
  .option(
    '-d, --dir <directory>',
    'Output directory (default: current directory)',
    '.',
  )
  .option(
    '-c, --concurrent <n>',
    'Maximum concurrent downloads',
    (v) => parseInt(v, 10),
    3,
  )
  .option(
    '-r, --retries <n>',
    'Retry attempts per URL on failure',
    (v) => parseInt(v, 10),
    3,
  )
  .option(
    '-t, --timeout <ms>',
    'Request timeout in milliseconds',
    (v) => parseInt(v, 10),
    30000,
  )
  .option('--no-progress', 'Disable progress bars')
  .option(
    '--header <header>',
    'Add a custom HTTP request header (format: "Name: Value"). Can be repeated.',
    collectHeaders,
    {},
  )
  .addHelpText(
    'after',
    `
Examples:
  sekodl https://example.com/file.zip
  sekodl https://example.com/a.zip https://example.com/b.zip -d ./downloads
  sekodl https://example.com/file.zip -o myfile.zip
  sekodl https://api.example.com/data --header "Authorization: Bearer token123"
`,
  )
  .action(async (urls, options) => {
    const outputDir = path.resolve(options.dir);
    const showProgress = options.progress !== false;

    // Ensure output directory exists
    try {
      fs.mkdirSync(outputDir, { recursive: true });
    } catch (err) {
      console.error(chalk.red(`Error: cannot create directory "${outputDir}": ${err.message}`));
      process.exit(1);
    }

    // --output is only meaningful for a single URL
    if (options.output && urls.length > 1) {
      console.error(
        chalk.yellow('Warning: --output is ignored when downloading multiple URLs'),
      );
      options.output = null;
    }

    // Build the download list
    const downloads = urls.map((url, index) => {
      let filename =
        options.output && index === 0
          ? options.output
          : getFilenameFromUrl(url);

      if (!filename || filename === '.') {
        filename = `download_${index + 1}`;
      }

      return {
        id: index,
        url,
        filename,
        outputPath: path.join(outputDir, filename),
      };
    });

    console.error(chalk.bold('\n  SekoDL – Terminal Download Manager'));
    console.error(
      chalk.gray(
        `  ${downloads.length} file(s) → ${outputDir}\n`,
      ),
    );

    const progressManager = showProgress ? new ProgressManager() : null;
    const results = { success: 0, failed: 0 };

    await downloadAll(downloads, {
      concurrency: options.concurrent,
      retries: options.retries,
      timeout: options.timeout,
      headers: options.header,

      onProgress(item, downloaded, total) {
        if (!progressManager) return;
        if (!progressManager._bars.has(item.id)) {
          progressManager.add(item.id, item.filename, total);
        }
        progressManager.update(item.id, downloaded, total);
      },

      onComplete(item, result) {
        if (progressManager) {
          progressManager.complete(item.id);
        }
        results.success++;
        if (!showProgress) {
          console.log(
            chalk.green(`  ✓  ${item.filename}  (${formatBytes(result.size)})`),
          );
        }
      },

      onError(item, err) {
        results.failed++;
        if (progressManager) {
          progressManager.complete(item.id);
        }
        process.stderr.write(
          chalk.red(`\n  ✗  ${item.filename}: ${err.message}\n`),
        );
      },
    });

    if (progressManager) {
      progressManager.stop();
    }

    console.error('');
    console.error(chalk.bold('  Summary:'));
    if (results.success > 0) {
      console.error(
        chalk.green(`  ✓  ${results.success} file(s) downloaded successfully`),
      );
    }
    if (results.failed > 0) {
      console.error(chalk.red(`  ✗  ${results.failed} file(s) failed`));
      process.exit(1);
    }
  });

/** Collect repeated --header flags into a plain object. */
function collectHeaders(value, previous) {
  const colonIdx = value.indexOf(':');
  if (colonIdx === -1) return previous;
  const name = value.slice(0, colonIdx).trim();
  const val = value.slice(colonIdx + 1).trim();
  return { ...previous, [name]: val };
}

module.exports = program;
