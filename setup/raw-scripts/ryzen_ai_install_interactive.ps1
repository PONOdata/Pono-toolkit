# Re-launch the AMD Ryzen AI installer interactively, with conda on PATH,
# so the installer's conda-detection succeeds.

$ErrorActionPreference = 'Continue'

Write-Host "=== LOCATE MINIFORGE ==="
$miniforgeCandidates = @(
  "$env:USERPROFILE\miniforge3",
  'C:\ProgramData\miniforge3',
  'C:\Miniforge3'
)
$miniforge = $null
foreach ($c in $miniforgeCandidates) {
  if (Test-Path (Join-Path $c 'Scripts\conda.exe')) { $miniforge = $c; break }
  if (Test-Path (Join-Path $c 'condabin\conda.bat')) { $miniforge = $c; break }
}
if (-not $miniforge) {
  Write-Host "  miniforge not found in candidate paths - checking Uninstall registry..."
  $roots = @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall','HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall')
  $mf = Get-ChildItem $roots -ErrorAction SilentlyContinue |
    Get-ItemProperty | Where-Object { $_.DisplayName -match 'Miniforge' } | Select-Object -First 1
  if ($mf) {
    Write-Host "    Uninstall entry: $($mf.DisplayName) at $($mf.InstallLocation)"
    $miniforge = $mf.InstallLocation
  }
}
Write-Host "  miniforge path: $miniforge"

$condaExe = Join-Path $miniforge 'Scripts\conda.exe'
$condaBat = Join-Path $miniforge 'condabin\conda.bat'
Write-Host "  conda.exe: $condaExe (exists=$(Test-Path $condaExe))"
Write-Host "  conda.bat: $condaBat (exists=$(Test-Path $condaBat))"

Write-Host ""
Write-Host "=== PREPEND CONDA TO PATH FOR THIS PROCESS ==="
$env:PATH = (Join-Path $miniforge 'Scripts') + ';' + (Join-Path $miniforge 'condabin') + ';' + (Join-Path $miniforge 'Library\bin') + ';' + $miniforge + ';' + $env:PATH

Write-Host "  which conda: $((where.exe conda 2>$null) -join ', ')"

Write-Host ""
Write-Host "=== ALSO PERSIST CONDA PATH TO USER ENV (survives reboot) ==="
$userPath = [Environment]::GetEnvironmentVariable('PATH','User')
$condaScripts = Join-Path $miniforge 'Scripts'
$condaBin     = Join-Path $miniforge 'condabin'
if ($userPath -notmatch [regex]::Escape($condaScripts)) {
  $newPath = "$condaBin;$condaScripts;$userPath"
  [Environment]::SetEnvironmentVariable('PATH', $newPath, 'User')
  Write-Host "  Added conda to User PATH."
} else {
  Write-Host "  User PATH already has conda."
}

Write-Host ""
Write-Host "=== LAUNCH RYZEN AI INSTALLER (interactive, no silent flags) ==="
$exe = "$env:USERPROFILE\Downloads\ryzen-ai-lt-1.7.1.exe"
if (Test-Path $exe) {
  Write-Host "  launching: $exe"
  Start-Process $exe
  Write-Host "  Watch the installer window. Accept prompts. It should now find conda."
} else {
  Write-Host "  installer missing: $exe"
}
