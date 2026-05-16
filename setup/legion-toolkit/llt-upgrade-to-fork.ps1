# Migrate Lenovo Legion Toolkit from the archived original
# (BartoszCichecki/LenovoLegionToolkit — archived 2025-07-24) to the active
# community fork (LenovoLegionToolkit-Team/LenovoLegionToolkit).
#
# Why: winget's LLT manifest still points at the archived author's 2.26.1 as
# "latest" because winget pulls from GitHub release tags and the archived repo
# has no newer tags. The active fork ships bugfixes and new-hardware support
# but has no winget presence, so you have to pull EXE direct from its releases.
#
# Requires admin. Runs silently once prereqs are met.

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Continue'

# Override with -ReleaseTag if you want a specific version.
param(
  [string]$ReleaseTag = 'v2.33.0.0'
)

Write-Host "=== KILL RUNNING LLT ==="
taskkill /F /IM "Lenovo Legion Toolkit.exe" 2>&1 | Out-Null
taskkill /F /IM "llt.exe" 2>&1 | Out-Null
Start-Sleep -Seconds 2

Write-Host "=== WIPE STALE USER-SCOPE INSTALL DIR ==="
$userDir = "$env:LOCALAPPDATA\Programs\LenovoLegionToolkit"
if (Test-Path $userDir) {
  Remove-Item $userDir -Recurse -Force -ErrorAction SilentlyContinue
  Write-Host "  removed $userDir"
}

Write-Host "=== UNINSTALL ARCHIVED LLT IF PRESENT ==="
winget uninstall --id BartoszCichecki.LenovoLegionToolkit --silent --accept-source-agreements 2>&1 | ForEach-Object { Write-Host "  $_" }

Write-Host ""
Write-Host "=== DOWNLOAD $ReleaseTag FROM TEAM FORK ==="
$url = "https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit/releases/download/$ReleaseTag/LenovoLegionToolkitSetup-$ReleaseTag.exe"
$exe = "$env:TEMP\LenovoLegionToolkitSetup-$ReleaseTag.exe"
Write-Host "  url: $url"
Write-Host "  dst: $exe"
try {
  Invoke-WebRequest $url -OutFile $exe -UseBasicParsing
  $size = (Get-Item $exe).Length
  Write-Host "  downloaded: $size bytes"
} catch {
  Write-Host "  DOWNLOAD FAILED: $_"
  exit 1
}

Write-Host ""
Write-Host "=== INSTALL (Inno Setup silent, current user) ==="
$log = "$env:TEMP\llt_install_$ReleaseTag.log"
$p = Start-Process $exe -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/LOG=$log" -Wait -PassThru
Write-Host "  exit code: $($p.ExitCode)"

if (Test-Path $log) {
  Write-Host ""
  Write-Host "=== INSTALL LOG TAIL ==="
  Get-Content $log -Tail 25
}

Write-Host ""
Write-Host "=== VERIFY INSTALL ==="
$llt = "$env:LOCALAPPDATA\Programs\LenovoLegionToolkit\Lenovo Legion Toolkit.exe"
if (Test-Path $llt) {
  $v = (Get-Item $llt).VersionInfo
  Write-Host "  installed: $($v.FileVersion)"
  Write-Host ""
  Write-Host "  Launching LLT..."
  Start-Process $llt
} else {
  Write-Host "  NOT FOUND at $llt — install failed."
  exit 2
}
