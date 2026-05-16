# Upgrade LLT 2.33.0.0 Stable (build 2026-03-14) -> 2.33.9.0 Dev (2026-03-23).
# The dev build includes commit 92432d4b "Fix Legion_Legacy" which activates the
# model-name keyword fallback for machines not in MachineTypeMap (like 83Q7).
# With that fix, 'Legion 5 15AHP11' model -> Legion_Legacy series, which lets
# the 4-zone RGB controller enumerate the VID_048D PID_C615 HID device and
# drive the keyboard.
#
# Source: https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit/releases/tag/v2.33.9.0
# Settings at %LOCALAPPDATA%\LenovoLegionToolkit\ are preserved across reinstall.

$ErrorActionPreference = 'Continue'

$id = [Security.Principal.WindowsIdentity]::GetCurrent()
$p = [Security.Principal.WindowsPrincipal]::new($id)
if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Not elevated; relaunching as admin..." -ForegroundColor Yellow
    Start-Process -FilePath 'powershell.exe' `
        -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',$PSCommandPath) `
        -Verb RunAs
    exit 0
}

Write-Host "=== KILLING LLT ===" -ForegroundColor Cyan
Get-Process -Name 'Lenovo Legion Toolkit','llt' -EA 0 | ForEach-Object {
    Stop-Process -Id $_.Id -Force -EA 0
    Write-Host "  killed PID $($_.Id)"
}
Start-Sleep -Seconds 2

Write-Host ""
Write-Host "=== DOWNLOADING 2.33.9.0 DEV ===" -ForegroundColor Cyan
$url = 'https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit/releases/download/v2.33.9.0/LenovoLegionToolkitSetup-v2.33.9.0.exe'
$exe = "$env:TEMP\LLTSetup-v2.33.9.0.exe"
Write-Host "  -> $exe"
try {
    Invoke-WebRequest $url -OutFile $exe -UseBasicParsing
    $size = (Get-Item $exe).Length
    Write-Host "  downloaded $size bytes"
} catch {
    Write-Host "  DOWNLOAD FAILED: $_" -ForegroundColor Red
    Write-Host "Press Enter..."
    [void](Read-Host)
    exit 1
}

Write-Host ""
Write-Host "=== INSTALLING (Inno Setup /VERYSILENT) ===" -ForegroundColor Cyan
$proc = Start-Process $exe -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER' -Wait -PassThru
Write-Host "  installer exit code: $($proc.ExitCode)"

Write-Host ""
Write-Host "=== VERIFY ===" -ForegroundColor Cyan
$llt = "$env:LOCALAPPDATA\Programs\LenovoLegionToolkit\Lenovo Legion Toolkit.exe"
if (Test-Path $llt) {
    $v = (Get-Item $llt).VersionInfo
    Write-Host ("  installed: {0} ({1})" -f $v.FileVersion, $v.ProductVersion)
} else {
    Write-Host "  NOT FOUND at $llt" -ForegroundColor Red
    Write-Host "Press Enter..."
    [void](Read-Host)
    exit 2
}

Write-Host ""
Write-Host "=== LAUNCHING LLT (elevated) ===" -ForegroundColor Cyan
Start-Process $llt
Write-Host "  launched."

Write-Host ""
Write-Host "=== WAITING 12s FOR ENUMERATION ===" -ForegroundColor Cyan
Start-Sleep -Seconds 12

$logDir = "$env:LOCALAPPDATA\LenovoLegionToolkit\log"
$log = (Get-ChildItem $logDir | Sort-Object LastWriteTime -Desc | Select-Object -First 1).FullName

$reportPath = Join-Path (Split-Path $PSCommandPath -Parent) 'rgb_upgrade_llt_dev.report.txt'
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("LLT Upgrade + RGB Report - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$sb.AppendLine("Log: $log")
[void]$sb.AppendLine("")

Write-Host ""
Write-Host "=== NEW LegionSeries + Dashboard Items ===" -ForegroundColor Green
$hits = Select-String -Path $log -Pattern 'LegionSeries|DashboardPage.*Items:|RGB Keyboard|Spectrum|LampArray|Unsupported.*KeyboardBacklight' |
        Select-Object -Last 40
foreach ($m in $hits) {
    Write-Host $m.Line
    [void]$sb.AppendLine($m.Line)
}

Set-Content -Path $reportPath -Value $sb.ToString() -Encoding UTF8
Write-Host ""
Write-Host "Report: $reportPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press Enter to close..."
[void](Read-Host)
