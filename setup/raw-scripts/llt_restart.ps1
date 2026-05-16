# Non-elevated LLT restart + log pull. User-scoped LLT install doesn't need admin
# for RGB detection - only BIOS-level controls need elevation (prompted on-demand).

$ErrorActionPreference = 'Continue'

Write-Host "=== KILLING LLT ==="
Get-Process -Name 'Lenovo Legion Toolkit','llt' -ErrorAction SilentlyContinue |
  ForEach-Object { Stop-Process -Id $_.Id -Force; Write-Host "  killed PID $($_.Id)" }

Start-Sleep -Seconds 2

Write-Host ""
Write-Host "=== LLT LOG DIR CONTENTS ==="
$logDir = "$env:LOCALAPPDATA\LenovoLegionToolkit\log"
if (Test-Path $logDir) {
  Get-ChildItem $logDir | Sort-Object LastWriteTime -Descending |
    Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
}

Write-Host ""
Write-Host "=== LATEST LLT LOG (last 120 lines) ==="
if (Test-Path $logDir) {
  $latest = Get-ChildItem $logDir -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if ($latest) {
    Write-Host "FILE: $($latest.FullName)"
    Get-Content $latest.FullName -Tail 120
  }
}

Write-Host ""
Write-Host "=== GREP LOG FOR RGB/SPECTRUM/KEYBOARD/LIGHTING ==="
if (Test-Path $logDir) {
  $latest = Get-ChildItem $logDir | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if ($latest) {
    Select-String -Path $latest.FullName -Pattern 'Spectrum|RGB|Keyboard|Backlight|Lighting|Feature' |
      Select-Object -First 60 LineNumber, Line | Format-List
  }
}

Write-Host ""
Write-Host "=== RELAUNCHING LLT ==="
$llt = "$env:LOCALAPPDATA\Programs\LenovoLegionToolkit\Lenovo Legion Toolkit.exe"
Start-Process $llt
Write-Host "  launched."

Write-Host "=== DONE ==="
