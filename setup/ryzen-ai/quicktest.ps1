# Run AMD's bundled quicktest.py through the SDK conda env on the NPU.
#
# Expected output on a working install:
#   Session successfully initialized.
#   Setting environment for PHX/HPT      (on Hawk Point / XDNA 1)
#    -- or --
#   Setting environment for STX/KRK      (on Strix Point / Krackan Point / XDNA 2)
#   Test Finished
#
# Read-only in terms of filesystem state; does create a session using the NPU.

$ErrorActionPreference = 'Continue'

# Auto-detect install path.
$base = 'C:\Program Files\RyzenAI'
$latest = Get-ChildItem $base -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
if (-not $latest) {
  Write-Host "ERROR: Ryzen AI not installed at $base"
  exit 1
}
$rai = $latest.FullName
$quicktest = Join-Path $rai 'quicktest'
if (-not (Test-Path (Join-Path $quicktest 'quicktest.py'))) {
  Write-Host "ERROR: quicktest.py missing at $quicktest"
  exit 1
}

# Locate conda.
$condaCandidates = @(
  "$env:USERPROFILE\miniforge3\condabin\conda.bat",
  'C:\ProgramData\miniforge3\condabin\conda.bat',
  'C:\Miniforge3\condabin\conda.bat'
)
$conda = $null
foreach ($c in $condaCandidates) { if (Test-Path $c) { $conda = $c; break } }
if (-not $conda) {
  Write-Host "ERROR: conda not found."
  exit 1
}

# Pick the SDK's conda env by name prefix.
$envName = (& $conda env list 2>$null | Select-String -Pattern 'ryzen' | ForEach-Object { ($_ -split '\s+')[0] } | Select-Object -First 1)
if (-not $envName) {
  Write-Host "ERROR: no 'ryzen*' conda env. Install the SDK first."
  exit 1
}

Write-Host "Install:  $rai"
Write-Host "Env:      $envName"
Write-Host "Running:  $quicktest\quicktest.py"
Write-Host ""

Push-Location $quicktest
try {
  $env:RYZEN_AI_INSTALLATION_PATH = $rai
  & $conda run -n $envName --no-capture-output python quicktest.py 2>&1
} finally {
  Pop-Location
}
