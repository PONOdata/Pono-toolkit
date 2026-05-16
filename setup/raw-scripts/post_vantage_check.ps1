# Post-uninstall verification: confirm no Lenovo Vantage leftovers, check LLT is still
# healthy, and read current power plan state. Read-only.

$ErrorActionPreference = 'Continue'

Write-Host "=== LENOVO SERVICES (should be just Fn+FunctionKeys + maybe ImController) ==="
Get-Service | Where-Object { $_.Name -match 'Lenovo|Vantage|ImController|SystemInterface' } |
  Select-Object Name, DisplayName, Status, StartType |
  Format-Table -AutoSize -Wrap

Write-Host ""
Write-Host "=== LENOVO / VANTAGE PROCESSES (should be empty or LLT-only) ==="
Get-Process | Where-Object { $_.ProcessName -match 'Lenovo|Vantage|LegionToolkit' } |
  Select-Object ProcessName, Id, StartTime |
  Format-Table -AutoSize

Write-Host ""
Write-Host "=== VANTAGE IN UNINSTALL REGISTRY (should be none) ==="
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
Write-Host "=== VANTAGE FOLDERS (should not exist) ==="
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
Get-AppxPackage -AllUsers -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -match 'Lenovo' } |
  Select-Object Name, PackageFullName |
  Format-Table -AutoSize

Write-Host ""
Write-Host "=== ACTIVE POWER PLAN ==="
powercfg /getactivescheme
Write-Host ""
Write-Host "=== ALL POWER PLANS ==="
powercfg /list

Write-Host ""
Write-Host "=== LLT INSTALLED AND VERSION ==="
Get-ChildItem $uninstallRoots -ErrorAction SilentlyContinue |
  Get-ItemProperty |
  Where-Object { $_.DisplayName -match 'Legion Toolkit' } |
  Select-Object DisplayName, DisplayVersion, InstallLocation |
  Format-Table -AutoSize

Write-Host ""
Write-Host "=== LLT RUNNING? ==="
Get-Process -Name 'Lenovo Legion Toolkit','llt' -ErrorAction SilentlyContinue |
  Select-Object ProcessName, Id, StartTime | Format-Table -AutoSize

Write-Host ""
Write-Host "=== DONE ==="
