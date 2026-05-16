# Second-pass diagnostic: service state, running processes, latest LLT version
$ErrorActionPreference = 'Continue'

Write-Host "=== LENOVO SERVICES ==="
Get-Service | Where-Object { $_.Name -match 'Lenovo|Vantage|ImController|SystemInterface' } |
  Select-Object Name, DisplayName, Status, StartType |
  Format-Table -AutoSize -Wrap

Write-Host "=== LENOVO PROCESSES ==="
Get-Process | Where-Object { $_.ProcessName -match 'Lenovo|Vantage|LegionToolkit|ImController' } |
  Select-Object ProcessName, Id, StartTime |
  Format-Table -AutoSize

Write-Host "=== WINGET LATEST LLT ==="
winget show --id BartoszCichecki.LenovoLegionToolkit --exact 2>&1 |
  Select-String -Pattern '^(Version|Publisher|Release Notes|Homepage|Found)' |
  ForEach-Object { $_.ToString() }

Write-Host "=== WINGET LIST LLT ==="
winget list --id BartoszCichecki.LenovoLegionToolkit --exact 2>&1 |
  ForEach-Object { $_.ToString() }

Write-Host "=== DONE ==="
