# RGB / Legion Toolkit diagnostic
# Collects: model/SKU, installed Lenovo/Razer/Corsair products, LLT install path,
# PnP keyboard/RGB devices, LLT log tail, LLT settings.json.
# Read-only. Safe to run any time.

$ErrorActionPreference = 'Continue'

Write-Host "=== MODEL ==="
Get-CimInstance Win32_ComputerSystem | Select-Object Manufacturer, Model, SystemSKUNumber | Format-List
Get-CimInstance Win32_BIOS          | Select-Object SMBIOSBIOSVersion, Manufacturer | Format-List
Get-CimInstance Win32_BaseBoard     | Select-Object Product, Manufacturer, Version | Format-List

Write-Host "=== INSTALLED (Lenovo/Razer/Corsair/etc) ==="
$uninstallRoots = @(
  'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
  'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
)
Get-ChildItem $uninstallRoots -ErrorAction SilentlyContinue |
  Get-ItemProperty |
  Where-Object { $_.DisplayName -match 'Legion|Lenovo|Spectrum|Vantage|iCUE|Synapse|Razer|Corsair|RGB' } |
  Select-Object DisplayName, DisplayVersion, Publisher |
  Format-Table -AutoSize

Write-Host "=== LLT FOLDERS ==="
$lltPaths = @(
  'C:\Program Files\LenovoLegionToolkit',
  'C:\Program Files (x86)\LenovoLegionToolkit',
  "$env:LOCALAPPDATA\Programs\LenovoLegionToolkit"
)
foreach ($p in $lltPaths) {
  if (Test-Path $p) {
    Write-Host "FOUND: $p"
    Get-ChildItem $p -Filter *.exe -ErrorAction SilentlyContinue |
      Select-Object Name, Length, LastWriteTime |
      Format-Table -AutoSize
  }
}

Write-Host "=== KEYBOARD / RGB PnP DEVICES ==="
Get-PnpDevice |
  Where-Object { $_.FriendlyName -match 'keyboard|backlight|RGB|Spectrum|Lenovo' -and $_.Class -ne 'Net' } |
  Select-Object Status, FriendlyName, Class, InstanceId |
  Format-Table -AutoSize -Wrap

Write-Host "=== HID DEVICES (often where RGB controllers hide) ==="
Get-PnpDevice -Class HIDClass -ErrorAction SilentlyContinue |
  Select-Object Status, FriendlyName, InstanceId |
  Format-Table -AutoSize -Wrap

Write-Host "=== LLT LOG TAIL (most recent file) ==="
$logDirs = @(
  "$env:LOCALAPPDATA\LenovoLegionToolkit\log",
  "$env:APPDATA\LenovoLegionToolkit\log",
  "$env:PROGRAMDATA\LenovoLegionToolkit\log"
)
$foundLog = $false
foreach ($d in $logDirs) {
  if (Test-Path $d) {
    $foundLog = $true
    Write-Host "Logs at: $d"
    $latest = Get-ChildItem $d -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latest) {
      Write-Host "  -> $($latest.FullName)"
      Get-Content $latest.FullName -Tail 50
    }
  }
}
if (-not $foundLog) { Write-Host "No LLT log directory found." }

Write-Host "=== LLT SETTINGS ==="
$settings = @(
  "$env:LOCALAPPDATA\LenovoLegionToolkit\settings.json",
  "$env:APPDATA\LenovoLegionToolkit\settings.json"
)
foreach ($s in $settings) {
  if (Test-Path $s) {
    Write-Host "FOUND: $s"
    Get-Content $s
  }
}

Write-Host "=== DONE ==="
