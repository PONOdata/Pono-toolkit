# Persist RYZEN_AI_INSTALLATION_PATH to MACHINE scope so any new shell,
# service, or UAC-elevated process picks it up on start.
#
# The AMD installer does not set this itself on Windows LTSC — so quicktest.py
# and anything that reads this var will fail until you run this.
#
# Requires admin (self-elevates).

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

# Auto-detect install path. Ryzen AI installs per-version under C:\Program Files\RyzenAI\<version>.
$base = 'C:\Program Files\RyzenAI'
if (-not (Test-Path $base)) {
  Write-Host "ERROR: $base not found. Install the SDK first (ryzen-ai/install.ps1)."
  exit 1
}
$latest = Get-ChildItem $base -Directory | Sort-Object Name -Descending | Select-Object -First 1
if (-not $latest) {
  Write-Host "ERROR: no version directory under $base"
  exit 1
}
$rai = $latest.FullName
Write-Host "Detected install: $rai"

Write-Host "=== BEFORE ==="
Write-Host "  [Machine] RYZEN_AI_INSTALLATION_PATH = $([Environment]::GetEnvironmentVariable('RYZEN_AI_INSTALLATION_PATH', 'Machine'))"

Write-Host ""
Write-Host "=== SETTING Machine RYZEN_AI_INSTALLATION_PATH = $rai ==="
[Environment]::SetEnvironmentVariable('RYZEN_AI_INSTALLATION_PATH', $rai, 'Machine')

Write-Host ""
Write-Host "=== AFTER (registry verification) ==="
$after = [Environment]::GetEnvironmentVariable('RYZEN_AI_INSTALLATION_PATH', 'Machine')
Write-Host "  [Machine] RYZEN_AI_INSTALLATION_PATH = $after"
if ($after -ne $rai) { Write-Host "  WARNING: registry value does not match; check permissions."; exit 2 }
Write-Host "  OK — persisted. New shells will inherit this value."
