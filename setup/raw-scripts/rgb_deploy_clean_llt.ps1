# Swap in the v2.33.9.0-tagged + patched Lib.dll (clean ABI match) over the
# master-built patched DLL currently deployed. Keeps the original .bak untouched.
#
# Why: the first deploy used master-branch source which had drifted slightly
# from the installed 2.33.9.0 Dev WPF exe. Worked, but "janky." This deploy
# uses Lib.dll built from the exact v2.33.9.0 source tag + the 83Q7 patch.

$ErrorActionPreference = 'Continue'

$id = [Security.Principal.WindowsIdentity]::GetCurrent()
$p = [Security.Principal.WindowsPrincipal]::new($id)
if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process -FilePath 'powershell.exe' `
        -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',$PSCommandPath) `
        -Verb RunAs
    exit 0
}

$source = 'C:\Brofalo\_build\llt-tag\BuildLLT\bin\Release\net9.0-windows10.0.26100.0\win-x64\LenovoLegionToolkit.Lib.dll'
$instDir = "$env:LOCALAPPDATA\Programs\LenovoLegionToolkit"
$target  = Join-Path $instDir 'LenovoLegionToolkit.Lib.dll'
$masterBackup = Join-Path $instDir 'LenovoLegionToolkit.Lib.dll.master-build.bak'

Write-Host "=== KILLING LLT ===" -ForegroundColor Cyan
Get-Process -Name 'Lenovo Legion Toolkit','llt' -EA 0 | ForEach-Object {
    Stop-Process -Id $_.Id -Force -EA 0
}
Start-Sleep -Seconds 2

Write-Host "=== BACKING UP CURRENT (master-build) DLL ===" -ForegroundColor Cyan
Copy-Item $target $masterBackup -Force
Write-Host "  -> $masterBackup"

Write-Host "=== DEPLOYING TAG-BUILT DLL ===" -ForegroundColor Cyan
Copy-Item $source $target -Force
$sz = (Get-Item $target).Length
Write-Host ("  installed ({0} bytes, v2.33.9.0-tag + 83Q7 patch)" -f $sz)

Write-Host "=== LAUNCHING LLT ===" -ForegroundColor Cyan
Start-Process (Join-Path $instDir 'Lenovo Legion Toolkit.exe')

Start-Sleep -Seconds 12

$log = (Get-ChildItem "$env:LOCALAPPDATA\LenovoLegionToolkit\log" |
        Sort-Object LastWriteTime -Desc | Select-Object -First 1).FullName

$reportPath = Join-Path (Split-Path $PSCommandPath -Parent) 'rgb_deploy_clean_llt.report.txt'
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("Clean (tag-matched) Deploy Report - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$sb.AppendLine("Log: $log")
[void]$sb.AppendLine("")

$hits = Select-String -Path $log -Pattern 'LegionSeries|Found device|RGB Keyboard initialized|Spectrum Keyboard initialized|Layout is|Keyboard profile is|DashboardPage.*Items:' |
        Select-Object -Last 30
foreach ($m in $hits) {
    Write-Host $m.Line
    [void]$sb.AppendLine($m.Line)
}
Set-Content -Path $reportPath -Value $sb.ToString() -Encoding UTF8
Write-Host ""
Write-Host "Report: $reportPath" -ForegroundColor Cyan
Write-Host "Press Enter..."
[void](Read-Host)
