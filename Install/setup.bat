@echo off
setlocal

set "ROOT=%~dp0.."
set "EXE=%ROOT%\bin\Release\net8.0-windows\win-x64\publish\SekoDL.exe"

echo ==========================================
echo SekoDL Setup
echo ==========================================
echo.

if not exist "%EXE%" (
    echo Published EXE not found:
    echo %EXE%
    echo.
    echo Run this first:
    echo dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
    exit /b 1
)

echo 1. Register sekodl:// protocol
powershell -ExecutionPolicy Bypass -File "%~dp0register-sekodl-protocol.ps1"
if errorlevel 1 exit /b 1

echo.
echo 2. Register Chrome native host
powershell -ExecutionPolicy Bypass -File "%~dp0register-native-host-chrome.ps1"
if errorlevel 1 exit /b 1

echo.
echo 3. Register Edge native host
powershell -ExecutionPolicy Bypass -File "%~dp0register-native-host-edge.ps1"
if errorlevel 1 exit /b 1

echo.
echo 4. Register Firefox native host
powershell -ExecutionPolicy Bypass -File "%~dp0register-native-host-firefox.ps1"
if errorlevel 1 exit /b 1

echo.
echo Setup finished.
echo.
echo Next steps:
echo - Load the browser extension from BrowserExtension\chrome-edge or BrowserExtension\firefox
echo - For Chrome/Edge, make sure allowed_origins uses your real extension ID
echo - Launch SekoDL.exe from the publish folder
echo.
pause
