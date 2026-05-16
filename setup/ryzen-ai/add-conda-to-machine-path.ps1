# Add Miniforge3 paths to MACHINE (system) PATH.
#
# Why this script exists as a standalone step:
#   AMD's Ryzen AI installer (ryzen-ai-lt-*.exe) self-elevates via UAC. The
#   elevated process reads the Machine PATH only — it does NOT inherit the
#   launching process's env modifications, and it does NOT see User PATH.
#   When Miniforge is installed user-scope (winget default), conda lives only
#   on User PATH, so the elevated installer hits "Conda not found" and aborts
#   despite conda being perfectly installed.
#
# Requires admin (self-elevates via UAC if not already).

$ErrorActionPreference = 'Continue'

$id = [Security.Principal.WindowsIdentity]::GetCurrent()
$p = [Security.Principal.WindowsPrincipal]::new($id)
if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  Write-Host "Not elevated; relaunching as admin..."
  Start-Process -FilePath 'powershell.exe' `
    -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',$PSCommandPath) `
    -Verb RunAs
  exit 0
}

# Locate Miniforge in the common install locations.
$candidates = @(
  "$env:USERPROFILE\miniforge3",
  'C:\ProgramData\miniforge3',
  'C:\Miniforge3',
  'C:\Program Files\Miniforge3'
)
$miniforge = $null
foreach ($c in $candidates) {
  if (Test-Path (Join-Path $c 'Scripts\conda.exe')) { $miniforge = $c; break }
}
if (-not $miniforge) {
  Write-Host "Miniforge3 not found. Install with: winget install --id CondaForge.Miniforge3 --silent --accept-source-agreements --accept-package-agreements"
  exit 1
}

Write-Host "Miniforge path: $miniforge"

$add = @(
  (Join-Path $miniforge 'condabin'),
  (Join-Path $miniforge 'Scripts'),
  (Join-Path $miniforge 'Library\bin'),
  $miniforge
)

Write-Host "=== BEFORE (Machine PATH) ==="
$machine = [Environment]::GetEnvironmentVariable('PATH','Machine')
Write-Host $machine

Write-Host ""
Write-Host "=== APPENDING CONDA PATHS ==="
$new = $machine
foreach ($p in $add) {
  if (-not (Test-Path $p)) { Write-Host "  skip (missing): $p"; continue }
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
  Write-Host "  No change needed."
}

Write-Host ""
Write-Host "=== VERIFY with a fresh cmd.exe (reads the new Machine PATH at startup) ==="
& cmd.exe /c 'where conda' 2>&1 | ForEach-Object { Write-Host "  where conda -> $_" }
