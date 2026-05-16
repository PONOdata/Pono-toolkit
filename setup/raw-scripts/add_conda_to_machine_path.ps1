# Add Miniforge3 paths to MACHINE (system) PATH so elevated installers
# that don't inherit user PATH can find conda.
# Requires admin (UAC prompt).

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Continue'
$transcript = 'C:\Brofalo\scripts\add_conda_to_machine_path.transcript.txt'
Start-Transcript -Path $transcript -Force | Out-Null

$miniforge = 'C:\Users\Jack\miniforge3'
$add = @(
  (Join-Path $miniforge 'condabin'),
  (Join-Path $miniforge 'Scripts'),
  (Join-Path $miniforge 'Library\bin'),
  $miniforge
)

foreach ($p in $add) {
  if (-not (Test-Path $p)) { Write-Host "  SKIP (missing): $p"; continue }
}

Write-Host "=== BEFORE ==="
$machine = [Environment]::GetEnvironmentVariable('PATH','Machine')
Write-Host $machine

Write-Host ""
Write-Host "=== APPENDING CONDA PATHS ==="
$new = $machine
foreach ($p in $add) {
  if ($new -notmatch [regex]::Escape($p)) {
    $new = "$p;$new"
    Write-Host "  added: $p"
  } else {
    Write-Host "  already present: $p"
  }
}

if ($new -ne $machine) {
  [Environment]::SetEnvironmentVariable('PATH', $new, 'Machine')
  Write-Host "  MACHINE PATH updated."
} else {
  Write-Host "  No change."
}

Write-Host ""
Write-Host "=== AFTER ==="
$after = [Environment]::GetEnvironmentVariable('PATH','Machine')
Write-Host $after

Write-Host ""
Write-Host "=== VERIFY with a fresh cmd.exe (inherits new Machine PATH) ==="
$out = & cmd.exe /c 'where conda' 2>&1
Write-Host "  where conda ->"
$out | ForEach-Object { Write-Host "    $_" }

Stop-Transcript | Out-Null
