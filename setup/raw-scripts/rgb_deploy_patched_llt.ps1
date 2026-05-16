# Deploy the patched LLT build over the installed 2.33.9.0 Dev installation.
# Only LenovoLegionToolkit.Lib.dll changed (Compatibility.cs is in that project).
# We back up the original first so rollback is a single copy.
#
# Why: LLT master's build contains our patch that maps machine type 83Q7 to
# LegionSeries.LOQ so Jack's Strix Point keyboard (VID_048D PID_C615) is found
# by SpectrumDeviceFactory using PID prefix 0xC600.

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

$builtDir = 'C:\Brofalo\_build\llt\BuildLLT\bin\Release\net9.0-windows10.0.26100.0\win-x64'
$instDir  = "$env:LOCALAPPDATA\Programs\LenovoLegionToolkit"
$target   = Join-Path $instDir 'LenovoLegionToolkit.Lib.dll'
$source   = Join-Path $builtDir 'LenovoLegionToolkit.Lib.dll'
$backup   = Join-Path $instDir 'LenovoLegionToolkit.Lib.dll.bak'

if (-not (Test-Path $source)) { Write-Host "Source DLL not found: $source" -ForegroundColor Red; pause; exit 1 }
if (-not (Test-Path $target)) { Write-Host "Target DLL not found: $target" -ForegroundColor Red; pause; exit 1 }

Write-Host "=== KILLING LLT ===" -ForegroundColor Cyan
Get-Process -Name 'Lenovo Legion Toolkit','llt' -EA 0 | ForEach-Object {
    Stop-Process -Id $_.Id -Force -EA 0
    Write-Host "  killed PID $($_.Id)"
}
Start-Sleep -Seconds 2

Write-Host ""
Write-Host "=== BACKUP ORIGINAL ===" -ForegroundColor Cyan
if (-not (Test-Path $backup)) {
    Copy-Item $target $backup
    Write-Host "  backed up to $backup"
} else {
    Write-Host "  backup already exists (keeping first one)"
}

Write-Host ""
Write-Host "=== DEPLOY PATCHED DLL ===" -ForegroundColor Cyan
Copy-Item $source $target -Force
$sz = (Get-Item $target).Length
Write-Host ("  installed ({0} bytes)" -f $sz)

Write-Host ""
Write-Host "=== LAUNCH LLT ===" -ForegroundColor Cyan
$llt = Join-Path $instDir 'Lenovo Legion Toolkit.exe'
Start-Process $llt
Write-Host "  launched: $llt"

Write-Host ""
Write-Host "=== WAIT 12s FOR ENUMERATION ===" -ForegroundColor Cyan
Start-Sleep -Seconds 12

$logDir = "$env:LOCALAPPDATA\LenovoLegionToolkit\log"
$log = (Get-ChildItem $logDir | Sort-Object LastWriteTime -Desc | Select-Object -First 1).FullName

$reportPath = Join-Path (Split-Path $PSCommandPath -Parent) 'rgb_deploy_patched_llt.report.txt'
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("Patched LLT Deploy Report - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$sb.AppendLine("Log: $log")
[void]$sb.AppendLine("")

Write-Host ""
Write-Host "=== POST-PATCH LOG EXCERPT ===" -ForegroundColor Green
$hits = Select-String -Path $log -Pattern 'LegionSeries|MachineType|DashboardPage.*Items:|RGB Keyboard|Spectrum|Found device|keyboard unsupported|Unsupported.*Keyboard' |
        Select-Object -Last 50
foreach ($m in $hits) {
    Write-Host $m.Line
    [void]$sb.AppendLine($m.Line)
}

Set-Content -Path $reportPath -Value $sb.ToString() -Encoding UTF8
Write-Host ""
Write-Host "Report: $reportPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "If LLT crashed: restore backup with:"
Write-Host "  Copy-Item '$backup' '$target' -Force"
Write-Host ""
Write-Host "Closing in 3s."
Start-Sleep -Seconds 3
