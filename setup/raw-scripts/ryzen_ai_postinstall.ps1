# Post-install verify + locate AMD's bundled quicktest.
$ErrorActionPreference = 'Continue'

Write-Host "=== RYZEN AI INSTALL DIR ==="
$candidates = @(
  'C:\Program Files\RyzenAI\1.7.1',
  'C:\Program Files\RyzenAI',
  'C:\Program Files\AMD\RyzenAI',
  'C:\RyzenAI',
  "$env:LOCALAPPDATA\RyzenAI",
  "$env:ProgramData\RyzenAI"
)
$installDir = $null
foreach ($c in $candidates) {
  if (Test-Path $c) {
    Write-Host "  FOUND: $c"
    if (-not $installDir) { $installDir = $c }
    Get-ChildItem $c -ErrorAction SilentlyContinue | Select-Object Name, LastWriteTime | Format-Table -AutoSize
  }
}

Write-Host "=== ENV VARS ==="
foreach ($scope in 'Machine','User') {
  foreach ($v in 'RYZEN_AI_INSTALLATION_PATH','XLNX_VART_FIRMWARE','VAIP_CONFIG_HOME','XLNX_TARGET_NAME') {
    $val = [Environment]::GetEnvironmentVariable($v, $scope)
    if ($val) { Write-Host "  [$scope] $v = $val" }
  }
}

Write-Host "=== CONDA ENVS ==="
$conda = 'C:\Users\Jack\miniforge3\condabin\conda.bat'
if (Test-Path $conda) {
  & $conda env list 2>&1 | ForEach-Object { Write-Host "  $_" }
}

Write-Host "=== LOOK FOR QUICKTEST / EXAMPLES / VAI-EP FILES ==="
if ($installDir) {
  Get-ChildItem $installDir -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match 'quicktest|smoke|example|onnxruntime-vitisai|vaip_config' } |
    Select-Object FullName | Format-Table -AutoSize -Wrap
}

Write-Host "=== ONNXRUNTIME-VITISAI IN CONDA ENV ==="
# Find the SDK's conda env
$envName = (& $conda env list 2>$null | Select-String -Pattern 'ryzen' | ForEach-Object { ($_ -split '\s+')[0] } | Select-Object -First 1)
if ($envName) {
  Write-Host "  SDK env: $envName"
  & $conda run -n $envName python -c "import onnxruntime as ort; print('ORT version:', ort.__version__); print('Providers:', ort.get_available_providers())" 2>&1 |
    ForEach-Object { Write-Host "  $_" }
}

Write-Host "=== DONE ==="
