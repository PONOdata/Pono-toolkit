# Inventory the current Ryzen AI SDK / NPU driver state on Spambook-Max.
# Read-only. No admin needed.
$ErrorActionPreference = 'Continue'

Write-Host "=== NPU DEVICE (should be an AMD IPU / XDNA) ==="
Get-PnpDevice -ErrorAction SilentlyContinue |
  Where-Object { $_.FriendlyName -match 'IPU|XDNA|Neural|NPU|Compute' -or $_.HardwareID -match 'VEN_1022' -and $_.FriendlyName -match 'IPU' } |
  Select-Object Status, FriendlyName, InstanceId, Class |
  Format-Table -AutoSize -Wrap

Write-Host "=== AMD IPU / NPU DRIVER DETAIL ==="
$ipu = Get-PnpDevice -ErrorAction SilentlyContinue |
  Where-Object { $_.FriendlyName -match 'IPU|AI Engine|NPU' }
foreach ($d in $ipu) {
  Write-Host "  Device: $($d.FriendlyName)"
  $props = Get-PnpDeviceProperty -InstanceId $d.InstanceId -KeyName `
    'DEVPKEY_Device_DriverVersion','DEVPKEY_Device_DriverDate','DEVPKEY_Device_DriverProvider','DEVPKEY_Device_Service' `
    -ErrorAction SilentlyContinue
  foreach ($p in $props) { Write-Host "    $($p.KeyName) = $($p.Data)" }
}

Write-Host ""
Write-Host "=== RYZEN AI / VITIS AI INSTALLED PACKAGES ==="
$uninstallRoots = @(
  'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
  'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
)
Get-ChildItem $uninstallRoots -ErrorAction SilentlyContinue |
  Get-ItemProperty |
  Where-Object { $_.DisplayName -match 'Ryzen AI|Vitis|NPU|Xilinx|XLNX|ONNX' } |
  Select-Object DisplayName, DisplayVersion, Publisher, InstallDate |
  Format-Table -AutoSize -Wrap

Write-Host "=== RYZEN AI INSTALL DIRS (common paths) ==="
$paths = @(
  'C:\Program Files\RyzenAI',
  'C:\Program Files\AMD\RyzenAI',
  'C:\ProgramData\RyzenAI',
  "$env:LOCALAPPDATA\RyzenAI",
  "$env:USERPROFILE\ryzenai",
  "$env:USERPROFILE\.ryzenai"
)
foreach ($p in $paths) {
  if (Test-Path $p) {
    Write-Host "  FOUND: $p"
    Get-ChildItem $p -ErrorAction SilentlyContinue | Select-Object -First 10 Name, LastWriteTime | Format-Table
  }
}

Write-Host ""
Write-Host "=== PYTHON + ONNX / VITIS-AI-EP IN USER ENVS ==="
$pyCandidates = @(
  "$env:USERPROFILE\miniconda3\Scripts\conda.exe",
  "$env:USERPROFILE\Anaconda3\Scripts\conda.exe",
  'C:\ProgramData\miniconda3\Scripts\conda.exe',
  'C:\tools\miniconda3\Scripts\conda.exe'
)
foreach ($c in $pyCandidates) { if (Test-Path $c) { Write-Host "  conda: $c" } }

where.exe python 2>$null | ForEach-Object { Write-Host "  python: $_" }
where.exe pip 2>$null | ForEach-Object { Write-Host "  pip: $_" }

Write-Host ""
Write-Host "=== ENV VARS (XLNX_VART, XLNX_TARGET, RYZEN_AI_INSTALLATION_PATH) ==="
foreach ($v in 'XLNX_VART_FIRMWARE','XLNX_TARGET_NAME','RYZEN_AI_INSTALLATION_PATH','VAIP_CONFIG_HOME') {
  $val = [Environment]::GetEnvironmentVariable($v, 'Machine')
  if ($val) { Write-Host "  $v = $val" }
  $valu = [Environment]::GetEnvironmentVariable($v, 'User')
  if ($valu) { Write-Host "  $v (User) = $valu" }
}

Write-Host "=== DONE ==="
