# Fire AMD's bundled quicktest through the SDK conda env.
# Sets RYZEN_AI_INSTALLATION_PATH for the process so vaip_config.json
# and device overlay resolve correctly.
$ErrorActionPreference = 'Continue'

$rai = 'C:\Program Files\RyzenAI\1.7.1'
$quicktest = Join-Path $rai 'quicktest'
$conda = 'C:\Users\Jack\miniforge3\condabin\conda.bat'
$envName = 'ryzen-ai-1.7.1'

Write-Host "=== QUICKTEST DIR CONTENTS ==="
Get-ChildItem $quicktest -ErrorAction SilentlyContinue | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize

Write-Host "=== README EXCERPT (if exists) ==="
$readme = Join-Path $quicktest 'README.md'
if (Test-Path $readme) { Get-Content $readme -TotalCount 60 }

Write-Host ""
Write-Host "=== FIRING QUICKTEST (with RYZEN_AI_INSTALLATION_PATH set) ==="
Push-Location $quicktest
try {
  $env:RYZEN_AI_INSTALLATION_PATH = $rai
  & $conda run -n $envName --no-capture-output python quicktest.py 2>&1 | ForEach-Object { Write-Host $_ }
} finally {
  Pop-Location
}

Write-Host ""
Write-Host "=== DONE ==="
