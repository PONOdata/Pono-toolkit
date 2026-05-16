# Launch the pono-imessage Windows server in the foreground.
# Reads PONO_IMESSAGE_KEY from env; aborts if not set (server refuses to start without it).

$ErrorActionPreference = 'Stop'

$condaCandidates = @(
  "$env:USERPROFILE\miniforge3\condabin\conda.bat",
  'C:\ProgramData\miniforge3\condabin\conda.bat'
)
$conda = $null
foreach ($c in $condaCandidates) { if (Test-Path $c) { $conda = $c; break } }
if (-not $conda) { Write-Host "Miniforge not found. Run setup.ps1 first."; exit 1 }

if (-not $env:PONO_IMESSAGE_KEY) {
  # Pull from User scope in case this shell was spawned before setup.ps1 ran.
  $env:PONO_IMESSAGE_KEY = [Environment]::GetEnvironmentVariable('PONO_IMESSAGE_KEY', 'User')
}
if (-not $env:PONO_IMESSAGE_KEY) {
  Write-Host "PONO_IMESSAGE_KEY is not set. Run setup.ps1 first to generate one."
  exit 1
}

$server = Join-Path $PSScriptRoot 'server.py'
Write-Host "Starting pono-imessage server. UI: http://127.0.0.1:8765/?key=<YOUR_KEY>"
& $conda run -n pono-imessage --no-capture-output python $server
