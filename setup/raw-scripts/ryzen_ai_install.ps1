# Install Ryzen AI SDK 1.7.1 once the installer EXE has been downloaded.
# Watches Downloads for ryzen-ai-lt-1.7.1.exe, verifies it, runs silently
# with admin, then confirms install location. Requires admin (UAC prompt).
#
# Why no direct download: AMD gates ryzen-ai-*.exe behind an export-control
# click-through at account.amd.com/en/forms/downloads/xef.html. The form
# redirects to a signed URL after EULA accept, which we can't hit from code.

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Continue'
$transcript = 'C:\Brofalo\scripts\ryzen_ai_install.transcript.txt'
Start-Transcript -Path $transcript -Force | Out-Null

$downloads = "$env:USERPROFILE\Downloads"
$exeName   = 'ryzen-ai-lt-1.7.1.exe'
$exePath   = Join-Path $downloads $exeName

Write-Host "=== CHECK FOR INSTALLER IN $downloads ==="
if (-not (Test-Path $exePath)) {
  Write-Host "  NOT FOUND at $exePath"
  Write-Host "  Complete the AMD form and let the EXE finish downloading, then re-run this script."
  Stop-Transcript | Out-Null
  exit 2
}
$sz = (Get-Item $exePath).Length
Write-Host ("  found: {0} ({1:N0} bytes)" -f $exePath, $sz)
if ($sz -lt 50MB) {
  Write-Host "  WARNING: size is under 50MB - this may be a partial download or wrong file."
}

Write-Host ""
Write-Host "=== SIGNATURE CHECK ==="
$sig = Get-AuthenticodeSignature $exePath
Write-Host "  Status: $($sig.Status)"
Write-Host "  SignerCertificate: $($sig.SignerCertificate.Subject)"
if ($sig.Status -ne 'Valid') {
  Write-Host "  ABORT: signature not valid"
  Stop-Transcript | Out-Null
  exit 3
}

Write-Host ""
Write-Host "=== RUN INSTALLER (silent, if supported) ==="
# AMD's Ryzen AI installer is based on Inno Setup. /VERYSILENT /NORESTART /LOG works.
$installLog = "$env:TEMP\ryzen_ai_install.log"
$p = Start-Process $exePath -ArgumentList "/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART","/LOG=$installLog" -Wait -PassThru
Write-Host "  installer exit code: $($p.ExitCode)"

Write-Host ""
Write-Host "=== INSTALLER LOG TAIL ==="
if (Test-Path $installLog) {
  Get-Content $installLog -Tail 50
} else {
  Write-Host "  no installer log at $installLog"
}

Write-Host ""
Write-Host "=== POST-INSTALL VERIFY ==="
$roots = @(
  'C:\Program Files\RyzenAI\1.7.1',
  'C:\Program Files\RyzenAI',
  "$env:LOCALAPPDATA\RyzenAI",
  "$env:USERPROFILE\AppData\Local\RyzenAI"
)
foreach ($r in $roots) {
  if (Test-Path $r) {
    Write-Host "  FOUND: $r"
    Get-ChildItem $r -ErrorAction SilentlyContinue | Select-Object -First 15 Name, LastWriteTime | Format-Table
  }
}

Write-Host "=== ENV VARS AFTER INSTALL ==="
foreach ($v in 'RYZEN_AI_INSTALLATION_PATH','XLNX_VART_FIRMWARE','VAIP_CONFIG_HOME','PATH') {
  $val = [Environment]::GetEnvironmentVariable($v, 'Machine')
  if ($val) { Write-Host "  $v = $($val.Substring(0, [Math]::Min(300, $val.Length)))" }
}

Write-Host "=== DONE ==="
Stop-Transcript | Out-Null
