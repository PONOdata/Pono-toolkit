# Find out what ryzen-ai-lt-1.7.1.exe actually installed - it's NOT an Inno Setup installer
# (our /VERYSILENT /LOG flags may have been ignored). Scan likely locations, registry, and
# search for conda/python bundled by AMD.
$ErrorActionPreference = 'Continue'

Write-Host "=== RYZEN AI in uninstall registry ==="
$roots = @(
  'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
  'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall',
  'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'
)
Get-ChildItem $roots -ErrorAction SilentlyContinue |
  Get-ItemProperty |
  Where-Object { $_.DisplayName -match 'Ryzen|Vitis|Xilinx|XLNX|AMD' -and $_.DisplayName -notmatch 'Radeon|Chipset|PSP|PMF|Interface|GPIO|PPM|Crash|MicroPEP|Virtual|Installer' } |
  Select-Object DisplayName, DisplayVersion, Publisher, InstallLocation, UninstallString |
  Format-List

Write-Host "=== CONDA on disk ==="
Get-ChildItem 'C:\','C:\Users\Jack\AppData\Local\','C:\ProgramData\' -Filter 'conda.exe' -Recurse -ErrorAction SilentlyContinue -Depth 6 |
  Select-Object FullName, Length | Format-Table -AutoSize

Write-Host "=== MINIFORGE / MINICONDA on disk ==="
$candidates = @(
  'C:\ProgramData\miniforge3',
  'C:\ProgramData\miniconda3',
  "$env:USERPROFILE\miniforge3",
  "$env:USERPROFILE\miniconda3",
  'C:\Miniforge3',
  'C:\Miniconda3',
  'C:\Program Files\Miniforge3',
  'C:\Program Files\Miniconda3'
)
foreach ($c in $candidates) { if (Test-Path $c) { Write-Host "  FOUND: $c" } }

Write-Host "=== AMD / RYZEN AI directories ==="
$candidates = @(
  'C:\Program Files\RyzenAI',
  'C:\Program Files\AMD\RyzenAI',
  'C:\Program Files\AMD\Ryzen AI',
  'C:\Program Files (x86)\RyzenAI',
  'C:\RyzenAI',
  'C:\Xilinx',
  'C:\XLNX',
  "$env:LOCALAPPDATA\RyzenAI",
  "$env:ProgramData\RyzenAI"
)
foreach ($c in $candidates) { if (Test-Path $c) { Write-Host "  FOUND: $c"; Get-ChildItem $c -Depth 1 | Select-Object -First 15 FullName | ForEach-Object { Write-Host "    $_" } } }

Write-Host "=== INSTALLER-SPAWNED FILES IN TEMP (ryzen-ai-lt drops to Temp) ==="
Get-ChildItem "$env:TEMP" -Directory -ErrorAction SilentlyContinue |
  Where-Object { $_.LastWriteTime -gt (Get-Date).AddMinutes(-15) } |
  Select-Object Name, FullName, LastWriteTime | Format-Table -AutoSize -Wrap

Write-Host "=== ENV VARS (current process + Machine + User) ==="
foreach ($scope in 'Machine','User') {
  foreach ($v in 'RYZEN_AI_INSTALLATION_PATH','XLNX_VART_FIRMWARE','VAIP_CONFIG_HOME','CONDA_PREFIX') {
    $val = [Environment]::GetEnvironmentVariable($v, $scope)
    if ($val) { Write-Host "  [$scope] $v = $val" }
  }
}
