# Registers the Chrome native messaging host manifest for SekoDL.
# This script prefers the manifest inside the installed app folder.
# It also updates the host path inside the manifest before registration.

$InstalledManifestPath = Join-Path $PSScriptRoot "sekodl.nativehost.chrome.json"
$RepoManifestPath = "C:\Users\ahmedseko\Documents\GitHub\sdm\SekoDL\Install\sekodl.nativehost.chrome.json"
$ManifestPath = if (Test-Path $InstalledManifestPath) { $InstalledManifestPath } else { $RepoManifestPath }
$ExePath = Join-Path (Split-Path -Parent $PSScriptRoot) "SekoDL.exe"
$RegistryKey = "Registry::HKEY_CURRENT_USER\Software\Google\Chrome\NativeMessagingHosts\com.sekodl.bridge"

if (Test-Path $ManifestPath -and Test-Path $ExePath) {
    $json = Get-Content $ManifestPath -Raw | ConvertFrom-Json
    $json.path = $ExePath
    $json | ConvertTo-Json -Depth 5 | Set-Content $ManifestPath -Encoding UTF8
}

New-Item -Path $RegistryKey -Force | Out-Null
Set-ItemProperty -Path $RegistryKey -Name "(Default)" -Value $ManifestPath | Out-Null

Write-Host "Chrome native host registered: $ManifestPath"
