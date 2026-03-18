# SekoDL

**Terminal Download Manager** – download files from the command line without a GUI.

## Features

- Download one or multiple files simultaneously from the command line
- Live progress bars with percentage, download speed, and ETA
- Configurable concurrency (parallel downloads)
- Automatic retry with exponential back-off on failure
- Follows HTTP redirects
- Respects `Content-Disposition` headers for automatic filename detection
- Custom HTTP request headers (e.g. `Authorization`)
- Works anywhere Node.js runs – no GUI required

## Installation

```bash
npm install
npm link       # makes `sekodl` available globally (optional)
```

## Usage

```
sekodl [options] <url> [url2 url3 ...]
```

### Options

| Flag | Description | Default |
|------|-------------|---------|
| `-o, --output <filename>` | Output filename (single URL only) | derived from URL |
| `-d, --dir <directory>` | Output directory | current directory |
| `-c, --concurrent <n>` | Maximum parallel downloads | `3` |
| `-r, --retries <n>` | Retry attempts per URL on failure | `3` |
| `-t, --timeout <ms>` | Request timeout in milliseconds | `30000` |
| `--no-progress` | Disable progress bars | — |
| `--header <Name: Value>` | Add a custom HTTP header (repeatable) | — |
| `-V, --version` | Show version | — |
| `-h, --help` | Show help | — |

### Examples

```bash
# Download a single file
sekodl https://example.com/file.zip

# Download to a specific directory
sekodl https://example.com/file.zip -d ./downloads

# Save with a custom filename
sekodl https://example.com/file.zip -o myarchive.zip

# Download several files in parallel (up to 5 at a time)
sekodl https://example.com/a.zip https://example.com/b.zip -c 5

# Pass a custom header (e.g. authentication)
sekodl https://api.example.com/export --header "Authorization: Bearer mytoken"

# Disable progress bars (useful in scripts / CI)
sekodl https://example.com/file.zip --no-progress
```

## Running without installing

```bash
node bin/sekodl.js <url> [options]
```

## Development

```bash
# Install dependencies
npm install

# Run tests
npm test
```
