# Kill LLT, dump its most recent log, then relaunch as admin so it gets
# the hardware handles it may need for RGB controller detection.
# Requires UAC prompt.

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Continue'

Write-Host "=== KILLING LLT ==="
Get-Process -Name 'Lenovo Legion Toolkit','llt' -ErrorAction SilentlyContinue |
  ForEach-Object { Stop-Process -Id $_.Id -Force; Write-Host "  killed PID $($_.Id)" }

Start-Sleep -Seconds 2

Write-Host ""
Write-Host "=== LLT LOG TAIL (last 80 lines of most recent) ==="
$logDir = "$env:LOCALAPPDATA\LenovoLegionToolkit\log"
if (Test-Path $logDir) {
  $latest = Get-ChildItem $logDir -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if ($latest) {
    Write-Host "FILE: $($latest.FullName)"
    Get-Content $latest.FullName -Tail 80
  } else {
    Write-Host "  no log files in $logDir"
  }
} else {
  Write-Host "  no log dir at $logDir"
}

Write-Host ""
Write-Host "=== GREP LOG FOR RGB / SPECTRUM / KEYBOARD DETECTION ==="
if (Test-Path $logDir) {
  $latest = Get-ChildItem $logDir -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if ($latest) {
    Select-String -Path $latest.FullName -Pattern 'Spectrum|RGB|Keyboard|Backlight|Lighting' -SimpleMatch |
      Select-Object -First 40 LineNumber, Line | Format-List
  }
}

Write-Host ""
Write-Host "=== RELAUNCHING LLT AS ADMIN ==="
$llt = "$env:LOCALAPPDATA\Programs\LenovoLegionToolkit\Lenovo Legion Toolkit.exe"
if (Test-Path $llt) {
  Start-Process $llt
  Write-Host "  LLT launched elevated."
} else {
  Write-Host "  LLT exe not found at $llt"
}

Write-Host ""
Write-Host "=== DONE ==="
