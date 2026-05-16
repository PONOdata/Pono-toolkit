# Elevated retry: force-kill LLT, run installer with logging, verify version.
# Requires admin (UAC prompt).

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Continue'
$transcript = 'C:\Brofalo\scripts\llt_upgrade_admin.transcript.txt'
Start-Transcript -Path $transcript -Force | Out-Null

Write-Host "=== ELEVATED KILL LLT ==="
taskkill /F /IM "Lenovo Legion Toolkit.exe" 2>&1
taskkill /F /IM "llt.exe" 2>&1
Start-Sleep -Seconds 2

# Double check nothing's running
$running = Get-Process -Name 'Lenovo Legion Toolkit','llt' -ErrorAction SilentlyContinue
if ($running) {
  Write-Host "  STILL RUNNING:"
  $running | Select-Object ProcessName, Id | Format-Table
} else {
  Write-Host "  all LLT procs dead."
}

Write-Host ""
Write-Host "=== WIPE STALE LLT INSTALL DIR ==="
$dir = "$env:LOCALAPPDATA\Programs\LenovoLegionToolkit"
if (Test-Path $dir) {
  try {
    Remove-Item $dir -Recurse -Force -ErrorAction Stop
    Write-Host "  removed $dir"
  } catch {
    Write-Host "  could not remove $dir : $_"
  }
} else {
  Write-Host "  already clean."
}

Write-Host ""
Write-Host "=== RUN INSTALLER WITH LOG ==="
$exe = "$env:TEMP\LLTSetup-v2.33.0.0.exe"
$log = "$env:TEMP\LLT_install.log"
if (-not (Test-Path $exe)) {
  Write-Host "  installer missing at $exe - downloading again."
  Invoke-WebRequest 'https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit/releases/download/v2.33.0.0/LenovoLegionToolkitSetup-v2.33.0.0.exe' -OutFile $exe -UseBasicParsing
}
$p = Start-Process $exe -ArgumentList "/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART","/LOG=$log" -Wait -PassThru
Write-Host "  exit code: $($p.ExitCode)"

Write-Host ""
Write-Host "=== TAIL INSTALLER LOG ==="
if (Test-Path $log) {
  Get-Content $log -Tail 40
} else {
  Write-Host "  no installer log at $log"
}

Write-Host ""
Write-Host "=== VERIFY VERSION ==="
$candidates = @(
  "$env:LOCALAPPDATA\Programs\LenovoLegionToolkit\Lenovo Legion Toolkit.exe",
  "C:\Program Files\LenovoLegionToolkit\Lenovo Legion Toolkit.exe",
  "C:\Program Files (x86)\LenovoLegionToolkit\Lenovo Legion Toolkit.exe"
)
foreach ($c in $candidates) {
  if (Test-Path $c) {
    $v = (Get-Item $c).VersionInfo
    Write-Host "  FOUND: $c  version=$($v.FileVersion)"
  }
}

Write-Host ""
Write-Host "=== LAUNCH ==="
foreach ($c in $candidates) {
  if (Test-Path $c) {
    Start-Process $c
    Write-Host "  launched $c"
    break
  }
}

Write-Host "=== DONE ==="
Stop-Transcript | Out-Null
