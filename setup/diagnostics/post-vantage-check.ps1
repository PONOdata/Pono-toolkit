# Post-Vantage-uninstall verification. Lenovo Vantage leaves services, processes,
# registry entries, and folders behind on incomplete uninstalls. This scans for
# all of them. Run AFTER uninstalling Vantage via Settings → Apps.
#
# Why Vantage needs to go on a Legion running Legion Toolkit:
#   Vantage and LLT both claim exclusive ownership of the EC thermal/RGB interface.
#   With Vantage running, LLT's RGB card disappears even when the MachineTypeMap
#   patch is in place, and thermal mode is left in an undefined state between the two.
# Read-only.

$ErrorActionPreference = 'Continue'

Write-Host "=== LENOVO SERVICES (expect only Fn+FunctionKeys, maybe ImController, nothing Vantage) ==="
Get-Service | Where-Object { $_.Name -match 'Lenovo|Vantage|ImController|SystemInterface' } |
  Select-Object Name, DisplayName, Status, StartType |
  Format-Table -AutoSize -Wrap

Write-Host ""
Write-Host "=== VANTAGE PROCESSES (expect empty) ==="
Get-Process | Where-Object { $_.ProcessName -match 'Lenovo|Vantage|LegionToolkit' } |
  Select-Object ProcessName, Id, StartTime |
  Format-Table -AutoSize

Write-Host ""
Write-Host "=== VANTAGE IN UNINSTALL REGISTRY (expect clean) ==="
$uninstallRoots = @(
  'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
  'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
)
$vantage = Get-ChildItem $uninstallRoots -ErrorAction SilentlyContinue |
  Get-ItemProperty |
  Where-Object { $_.DisplayName -match 'Vantage' } |
  Select-Object DisplayName, DisplayVersion, Publisher
if ($vantage) { $vantage | Format-Table -AutoSize } else { Write-Host "  clean." }

Write-Host ""
Write-Host "=== VANTAGE FOLDERS (expect all 'gone') ==="
$paths = @(
  'C:\Program Files (x86)\Lenovo\VantageService',
  'C:\ProgramData\Lenovo\Vantage',
  "$env:LOCALAPPDATA\Packages\E046963F.LenovoCompanion_k1h2ywk1493x8",
  "$env:LOCALAPPDATA\Packages\E0469640.LenovoUtility_k1h2ywk1493x8"
)
foreach ($p in $paths) {
  if (Test-Path $p) { Write-Host "  STILL PRESENT: $p" } else { Write-Host "  gone: $p" }
}

Write-Host ""
Write-Host "=== APPX PACKAGES (Store-delivered Vantage) ==="
Get-AppxPackage -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -match 'Lenovo' } |
  Select-Object Name, PackageFullName |
  Format-Table -AutoSize

Write-Host ""
Write-Host "=== ACTIVE POWER PLAN (informational — Vantage drops Lenovo plans, without it you'll see only Balanced) ==="
powercfg /getactivescheme

Write-Host ""
Write-Host "=== LLT RUNNING? (informational) ==="
Get-Process -Name 'Lenovo Legion Toolkit','llt' -ErrorAction SilentlyContinue |
  Select-Object ProcessName, Id, StartTime | Format-Table -AutoSize
