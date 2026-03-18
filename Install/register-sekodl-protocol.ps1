# Registers the custom URI scheme:
#   sekodl://download?url=https%3A%2F%2Fexample.com%2Ffile.zip
#
# This script looks for SekoDL.exe next to the Install folder first.
# If it is not found there, it falls back to the published EXE in the repo.

$InstalledExePath = Join-Path (Split-Path -Parent $PSScriptRoot) "SekoDL.exe"
$RepoExePath = "C:\Users\ahmedseko\Documents\GitHub\sdm\SekoDL\bin\Release\net8.0-windows\win-x64\publish\SekoDL.exe"
$ExePath = if (Test-Path $InstalledExePath) { $InstalledExePath } else { $RepoExePath }
$ProtocolKey = "Registry::HKEY_CURRENT_USER\Software\Classes\sekodl"
$CommandKey = Join-Path $ProtocolKey "shell\open\command"

New-Item -Path $ProtocolKey -Force | Out-Null
Set-ItemProperty -Path $ProtocolKey -Name "(Default)" -Value "URL:SekoDL Protocol" | Out-Null
Set-ItemProperty -Path $ProtocolKey -Name "URL Protocol" -Value "" | Out-Null

New-Item -Path $CommandKey -Force | Out-Null
Set-ItemProperty -Path $CommandKey -Name "(Default)" -Value "`"$ExePath`" `"%1`"" | Out-Null

Write-Host "Registered sekodl:// protocol for $ExePath"
