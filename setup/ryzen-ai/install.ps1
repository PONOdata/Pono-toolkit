# Launch AMD's Ryzen AI installer (ryzen-ai-lt-*.exe) with conda visible on PATH
# so its prerequisite check passes. Runs interactively — no silent flags.
#
# Before running: download the installer from
#   https://account.amd.com/en/forms/downloads/xef.html?filename=ryzen-ai-lt-1.7.1.exe
# (requires AMD account + EULA click; can't curl past). Installer lands in ~/Downloads.
#
# The installer itself will UAC-elevate. It relies on Machine PATH having conda —
# run `add-conda-to-machine-path.ps1` first if you installed Miniforge user-scope.

$ErrorActionPreference = 'Continue'

# Resolve Miniforge (for prepending to current process PATH too, belt-and-suspenders).
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
  Write-Host "ERROR: Miniforge3 not found. Install it first:"
  Write-Host "  winget install --id CondaForge.Miniforge3 --silent --accept-source-agreements --accept-package-agreements"
  exit 1
}
Write-Host "Miniforge: $miniforge"

# Prepend conda paths to this process's PATH (the elevated installer uses Machine PATH,
# not this; this is only for the current shell).
$env:PATH = (Join-Path $miniforge 'condabin') + ';' + (Join-Path $miniforge 'Scripts') + ';' + (Join-Path $miniforge 'Library\bin') + ';' + $miniforge + ';' + $env:PATH
Write-Host "where conda (this process): $((where.exe conda 2>$null) -join ', ')"

# Find the installer EXE. Accepts any ryzen-ai-lt-*.exe version.
$dl = "$env:USERPROFILE\Downloads"
$exe = Get-ChildItem $dl -Filter 'ryzen-ai-lt-*.exe' -ErrorAction SilentlyContinue |
  Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $exe) {
  Write-Host "ERROR: no ryzen-ai-lt-*.exe in $dl"
  Write-Host "Download from: https://account.amd.com/en/forms/downloads/xef.html?filename=ryzen-ai-lt-1.7.1.exe"
  exit 1
}
Write-Host "Installer: $($exe.FullName)  ($([math]::Round($exe.Length / 1GB, 2)) GB)"

# Verify Authenticode signature before running anything AMD hands us.
$sig = Get-AuthenticodeSignature $exe.FullName
Write-Host "Signature: $($sig.Status) - $($sig.SignerCertificate.Subject)"
if ($sig.Status -ne 'Valid') {
  Write-Host "ABORT: installer signature not valid."
  exit 2
}

Write-Host ""
Write-Host "Launching installer. Click through prompts (EULA, install dir, conda env name)."
Write-Host "Install takes several minutes once conda env creation starts."
Start-Process $exe.FullName
