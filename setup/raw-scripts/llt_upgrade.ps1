# Upgrade from archived BartoszCichecki LLT 2.26.1 to active Team-fork v2.33.0.0 Stable.
# Source: https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit
# Settings at %LOCALAPPDATA%\LenovoLegionToolkit\ survive because the app name/path match.

$ErrorActionPreference = 'Continue'

Write-Host "=== KILL RUNNING LLT ==="
Get-Process -Name 'Lenovo Legion Toolkit','llt' -ErrorAction SilentlyContinue |
  ForEach-Object { Stop-Process -Id $_.Id -Force; Write-Host "  killed PID $($_.Id)" }
Start-Sleep -Seconds 2

Write-Host ""
Write-Host "=== UNINSTALL ARCHIVED LLT (user-scope, no admin needed) ==="
winget uninstall --id BartoszCichecki.LenovoLegionToolkit --silent --accept-source-agreements 2>&1 |
  ForEach-Object { Write-Host "  $_" }

Write-Host ""
Write-Host "=== DOWNLOAD v2.33.0.0 STABLE FROM TEAM FORK ==="
$url = 'https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit/releases/download/v2.33.0.0/LenovoLegionToolkitSetup-v2.33.0.0.exe'
$exe = "$env:TEMP\LLTSetup-v2.33.0.0.exe"
Write-Host "  -> $exe"
try {
  Invoke-WebRequest $url -OutFile $exe -UseBasicParsing
  $size = (Get-Item $exe).Length
  Write-Host "  downloaded $size bytes"
} catch {
  Write-Host "  DOWNLOAD FAILED: $_"
  exit 1
}

Write-Host ""
Write-Host "=== INSTALL (Inno Setup silent flags) ==="
$p = Start-Process $exe -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER' -Wait -PassThru
Write-Host "  installer exit code: $($p.ExitCode)"

Write-Host ""
Write-Host "=== VERIFY INSTALL ==="
$llt = "$env:LOCALAPPDATA\Programs\LenovoLegionToolkit\Lenovo Legion Toolkit.exe"
if (Test-Path $llt) {
  $v = (Get-Item $llt).VersionInfo
  Write-Host "  installed: $($v.FileVersion) ($($v.ProductVersion))"
} else {
  Write-Host "  NOT FOUND at $llt"
}

Write-Host ""
Write-Host "=== LAUNCH ==="
if (Test-Path $llt) {
  Start-Process $llt
  Write-Host "  launched."
}

Write-Host "=== DONE ==="
