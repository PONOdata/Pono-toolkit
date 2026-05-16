# Verify Ryzen AI SDK install: install dir, conda env, ORT version, providers.
# Read-only. No admin.

$ErrorActionPreference = 'Continue'

Write-Host "=== RYZEN AI INSTALL DIR ==="
$candidates = @(
  'C:\Program Files\RyzenAI',
  "$env:LOCALAPPDATA\RyzenAI",
  "$env:ProgramData\RyzenAI"
)
$installDir = $null
foreach ($c in $candidates) {
  if (Test-Path $c) {
    Write-Host "  FOUND: $c"
    if (-not $installDir) {
      $v = Get-ChildItem $c -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
      if ($v) { $installDir = $v.FullName } else { $installDir = $c }
    }
    Get-ChildItem $c -ErrorAction SilentlyContinue | Select-Object Name, LastWriteTime | Format-Table -AutoSize
  }
}

Write-Host "=== MACHINE ENV VARS ==="
foreach ($v in 'RYZEN_AI_INSTALLATION_PATH','XLNX_VART_FIRMWARE','VAIP_CONFIG_HOME','XLNX_TARGET_NAME') {
  $val = [Environment]::GetEnvironmentVariable($v, 'Machine')
  if ($val) { Write-Host "  [Machine] $v = $val" }
}

Write-Host "=== CONDA ENVS ==="
$condaCandidates = @(
  "$env:USERPROFILE\miniforge3\condabin\conda.bat",
  'C:\ProgramData\miniforge3\condabin\conda.bat',
  'C:\Miniforge3\condabin\conda.bat'
)
$conda = $null
foreach ($c in $condaCandidates) { if (Test-Path $c) { $conda = $c; break } }
if (-not $conda) {
  Write-Host "  conda not found."
  exit 1
}
Write-Host "  conda: $conda"
& $conda env list 2>&1 | ForEach-Object { Write-Host "  $_" }

Write-Host ""
Write-Host "=== ONNX RUNTIME PROVIDERS IN SDK ENV ==="
$envName = (& $conda env list 2>$null | Select-String -Pattern 'ryzen' | ForEach-Object { ($_ -split '\s+')[0] } | Select-Object -First 1)
if ($envName) {
  Write-Host "  SDK env: $envName"
  & $conda run -n $envName python -c "import onnxruntime as ort; print('ORT version:', ort.__version__); print('Providers:', ort.get_available_providers())" 2>&1 |
    ForEach-Object { Write-Host "  $_" }
  Write-Host ""
  Write-Host "  Expect VitisAIExecutionProvider in the providers list — that's the NPU."
} else {
  Write-Host "  No 'ryzen*' conda env found. Install may have failed."
}
