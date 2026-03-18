# SekoDL

SekoDL is a colorful terminal-based download manager for Windows built with .NET 8 and `Spectre.Console`. It stays console-only while providing a modern dashboard, live progress, queue handling, pause/resume support when the server supports HTTP Range requests, and JSON-based history/settings persistence.

## Features

- Modern terminal UI with panels, tables, status badges, colors, and animated spinners
- One active download at a time with queue support
- Streaming downloads with `HttpClient` and `ResponseHeadersRead`
- Pause, resume, and cancel support
- `.part` files for incomplete downloads
- Resume detection using HTTP Range / `206 Partial Content`
- Graceful handling of bad URLs, timeouts, access issues, and unsupported resume
- JSON persistence for settings and download history
- Default download folder points to the current Windows user's `Downloads` folder

## Project Structure

```text
SekoDL/
  Program.cs
  SekoDL.csproj
  README.md
  Models/
    DownloadTaskInfo.cs
    DownloadHistoryItem.cs
    AppSettings.cs
  Services/
    DownloadService.cs
    QueueService.cs
    HistoryService.cs
    SettingsService.cs
  UI/
    TerminalUi.cs
    Theme.cs
    DownloadDashboard.cs
  Utils/
    FileNameHelper.cs
    SizeFormatter.cs
    TimeFormatter.cs
  Data/
  Downloads/
```

## Commands

Create the project:

```powershell
mkdir SekoDL
cd SekoDL
dotnet new console --framework net8.0
```

Install the required package:

```powershell
dotnet add package Spectre.Console
```

Build:

```powershell
dotnet build
```

Run:

```powershell
dotnet run
```

Publish a Windows single-file EXE:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

The published executable will be under:

```text
bin/Release/net8.0/win-x64/publish/
```

## Major Files

- `Program.cs`: app startup, dependency wiring, folder creation, main menu loop
- `Services/DownloadService.cs`: metadata detection, streamed download logic, pause/resume behavior, `.part` handling
- `Services/QueueService.cs`: single-active-task queue processor and task control methods
- `Services/HistoryService.cs`: JSON history load/save/update logic
- `Services/SettingsService.cs`: JSON settings load/save with defaults
- `UI/TerminalUi.cs`: menu system, prompts, tables, status messages
- `UI/DownloadDashboard.cs`: live active-download dashboard with animated refresh
- `UI/Theme.cs`: centralized status colors and styles
- `Utils/FileNameHelper.cs`: filename extraction, sanitization, duplicate handling
- `Utils/SizeFormatter.cs`: human-readable byte formatting
- `Utils/TimeFormatter.cs`: ETA and duration formatting

## Website Integration

SekoDL can safely accept direct HTTP/HTTPS links from external sources without changing the downloader engine. All external requests are validated first and then imported into the normal queue flow.

Protocol link example:

```text
sekodl://download?url=https%3A%2F%2Fexample.com%2Ffile.zip
```

Example HTML button:

```html
<a href="sekodl://download?url=https%3A%2F%2Fexample.com%2Ffile.zip">Download with SekoDL</a>
```

## Custom Protocol Setup

Publish the app first, then edit the EXE path inside:

```text
Install/register-sekodl-protocol.ps1
```

Run the script in PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install\register-sekodl-protocol.ps1
```

For a quick all-in-one setup after publish:

```powershell
.\Install\setup.bat
```

## Browser Extension Setup

The unpacked extension lives in:

```text
BrowserExtension/chrome-edge/
BrowserExtension/firefox/
```

To load it in Chrome or Edge:

1. Open the browser extensions page
2. Enable Developer mode
3. Choose Load unpacked
4. Select `BrowserExtension/chrome-edge/`

To load it in Firefox for temporary testing:

1. Open `about:debugging#/runtime/this-firefox`
2. Click `Load Temporary Add-on`
3. Select `BrowserExtension/firefox/manifest.json`

The extension only sends the exact link you clicked in the browser context menu:

```text
Download link with SekoDL
```

## Native Messaging Host Setup

The same `SekoDL.exe` binary runs as the native host:

```powershell
SekoDL.exe --native-host
```

Edit the path and extension ID placeholders inside:

```text
Install/sekodl.nativehost.chrome.json
Install/sekodl.nativehost.edge.json
Install/sekodl.nativehost.firefox.json
```

Then register the native host manifests:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install\register-native-host-chrome.ps1
powershell -ExecutionPolicy Bypass -File .\Install\register-native-host-edge.ps1
powershell -ExecutionPolicy Bypass -File .\Install\register-native-host-firefox.ps1
```

## Clipboard Monitor

SekoDL can watch the Windows clipboard for copied direct HTTP/HTTPS links. Enable or disable it from the terminal menu:

```text
Toggle clipboard monitor
```

When a valid new direct link is detected, SekoDL asks for confirmation before adding it to the queue.

## Security Scope

- Only direct `http://` and `https://` links are supported
- Unsupported schemes such as `file:`, `javascript:`, `data:`, `ftp:`, `blob:`, `chrome:`, and `edge:` are rejected
- Protected or restricted media extraction is not supported
- No site scraping, media sniffing, DRM bypass, or hidden link discovery is performed

## Windows Installer

A ready-to-edit Inno Setup script is included:

```text
Install/SekoDL.iss
```

How to build:

1. Publish SekoDL first
2. Install Inno Setup on Windows
3. Open `Install/SekoDL.iss`
4. Build the installer from Inno Setup Compiler

The installer copies:
- the published SekoDL executable
- browser extension folders
- native host manifests
- registration scripts
