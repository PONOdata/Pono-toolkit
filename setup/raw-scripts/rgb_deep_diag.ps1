# Deep RGB diagnostic for LLT v2.33.0.0 on Legion 5 15AHP11.
$ErrorActionPreference = 'Continue'

Write-Host "=== LLT VERSION ==="
$llt = "$env:LOCALAPPDATA\Programs\LenovoLegionToolkit\Lenovo Legion Toolkit.exe"
if (Test-Path $llt) { (Get-Item $llt).VersionInfo | Select-Object FileVersion, ProductVersion | Format-List }

Write-Host "=== LLT PROCESS RUNNING? ==="
Get-Process -Name 'Lenovo Legion Toolkit','llt' -ErrorAction SilentlyContinue |
  Select-Object ProcessName, Id, StartTime | Format-Table

Write-Host "=== NEW LLT LOG FILES ==="
$logDir = "$env:LOCALAPPDATA\LenovoLegionToolkit\log"
if (Test-Path $logDir) {
  Get-ChildItem $logDir | Sort-Object LastWriteTime -Descending |
    Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
}

Write-Host "=== LATEST LLT LOG (last 200 lines) ==="
if (Test-Path $logDir) {
  $latest = Get-ChildItem $logDir | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if ($latest) {
    Write-Host "FILE: $($latest.FullName)"
    Get-Content $latest.FullName -Tail 200
  }
}

Write-Host ""
Write-Host "=== LLT LOG FILTERED (RGB/SPECTRUM/KEYBOARD/LAMP/CAPABILITY) ==="
if (Test-Path $logDir) {
  $latest = Get-ChildItem $logDir | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if ($latest) {
    Select-String -Path $latest.FullName -Pattern 'Spectrum|RGB|Keyboard|Backlight|Lighting|LampArray|Capability|Feature|Detect' |
      Select-Object LineNumber, Line | Format-List
  }
}

Write-Host ""
Write-Host "=== LEGION ZONE / LEGION SPACE / HOTKEYS STILL INSTALLED? ==="
$uninstallRoots = @(
  'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
  'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
)
Get-ChildItem $uninstallRoots -ErrorAction SilentlyContinue |
  Get-ItemProperty |
  Where-Object { $_.DisplayName -match 'Legion Zone|Legion Space|Lenovo Hotkey|Hotkeys|Legion Arena|LegionAI|Legion Edge|SpectrumDriver|Spectrum2' } |
  Select-Object DisplayName, DisplayVersion, Publisher | Format-Table -AutoSize

Write-Host "=== APPX (Store apps) LENOVO ==="
Get-AppxPackage -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -match 'Lenovo|Legion' } |
  Select-Object Name, Version, PackageFullName | Format-Table -AutoSize -Wrap

Write-Host "=== SERVICES: SPECTRUM / LEGION ==="
Get-Service | Where-Object { $_.Name -match 'Spectrum|Legion|ImController|Hotkey' } |
  Select-Object Name, DisplayName, Status, StartType | Format-Table -AutoSize -Wrap

Write-Host "=== LLT INNO UNINSTALL REGISTRY ==="
Get-ChildItem $uninstallRoots -ErrorAction SilentlyContinue |
  Get-ItemProperty |
  Where-Object { $_.DisplayName -match 'Legion Toolkit' } |
  Select-Object DisplayName, DisplayVersion, Publisher, InstallLocation | Format-Table -AutoSize

Write-Host "=== LAMPARRAY MSIX REGISTERED? ==="
Get-AppxPackage -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -match 'LampArray|LegionToolkit' } |
  Select-Object Name, Version, InstallLocation, Status | Format-List

Write-Host "=== DONE ==="
