# Persist RYZEN_AI_INSTALLATION_PATH to Machine scope so any shell/process
# (including elevated) sees it on start. Requires admin.

#Requires -RunAsAdministrator
$ErrorActionPreference = 'Continue'
$transcript = 'C:\Brofalo\scripts\persist_ryzen_ai_env.transcript.txt'
Start-Transcript -Path $transcript -Force | Out-Null

$rai = 'C:\Program Files\RyzenAI\1.7.1'
if (-not (Test-Path $rai)) {
  Write-Host "  NOT FOUND: $rai  -- aborting."
  Stop-Transcript | Out-Null
  exit 1
}

Write-Host "=== BEFORE ==="
foreach ($v in 'RYZEN_AI_INSTALLATION_PATH') {
  $m = [Environment]::GetEnvironmentVariable($v, 'Machine')
  Write-Host "  [Machine] $v = $m"
}

Write-Host ""
Write-Host "=== SETTING Machine RYZEN_AI_INSTALLATION_PATH = $rai ==="
[Environment]::SetEnvironmentVariable('RYZEN_AI_INSTALLATION_PATH', $rai, 'Machine')

Write-Host ""
Write-Host "=== AFTER (fresh cmd should see it) ==="
$out = & cmd.exe /c 'echo %RYZEN_AI_INSTALLATION_PATH%' 2>&1
Write-Host "  cmd echo -> $out"

Stop-Transcript | Out-Null
