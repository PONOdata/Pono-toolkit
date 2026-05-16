# Legion 5 15AHP11 (83Q7) RGB enablement: elevates LLT so it can talk to the EC
# for 4-zone RGB, and ensures the Lenovo hotkey service is running.
#
# Why it's needed:
#   - Fn+Space cycles RGB colors at the EC level (hardware RGB confirmed).
#   - LLT v2.33 running non-elevated enumerated the keyboard as
#     "WhiteKeyboardBacklight" because it couldn't probe the RGB EC registers.
#   - LenovoFnAndFunctionKeys service was Stopped + Disabled on this LTSC 2024
#     install, which is the bridge LLT uses to send EC commands.
#
# What this script does (all reversible):
#   1. Enable + start LenovoFnAndFunctionKeys service.
#   2. Kill any running LLT instance.
#   3. Relaunch LLT elevated.
#   4. Wait 12s, then pull the FRESH dashboard items line from the new log so
#      we can confirm whether RGB / Spectrum / FourZone card now appears.
#   5. Write a small .md report next to this script with the result.
#
# Requires admin. Self-elevates on first run.
# ---------------------------------------------------------------------------

$ErrorActionPreference = 'Continue'

# Self-elevate
$currentId = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($currentId)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Not elevated; relaunching as admin..." -ForegroundColor Yellow
    $argList = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$PSCommandPath)
    Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -Verb RunAs
    exit 0
}

Write-Host "=== RGB enablement (elevated) ===" -ForegroundColor Cyan
Write-Host ""

# 1. LenovoFnAndFunctionKeys service
Write-Host "[1/4] Enabling + starting LenovoFnAndFunctionKeys..." -ForegroundColor Cyan
$svc = Get-Service LenovoFnAndFunctionKeys -ErrorAction SilentlyContinue
if ($svc) {
    Set-Service -Name LenovoFnAndFunctionKeys -StartupType Automatic -ErrorAction SilentlyContinue
    if ($svc.Status -ne 'Running') {
        Start-Service -Name LenovoFnAndFunctionKeys -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 1
    Get-Service LenovoFnAndFunctionKeys | Select-Object Name, Status, StartType | Format-Table -AutoSize
} else {
    Write-Host "  service not found (driver package may be missing)."
}

# 2. Kill LLT
Write-Host "[2/4] Killing running LLT..." -ForegroundColor Cyan
Get-Process -Name 'Lenovo Legion Toolkit','llt' -ErrorAction SilentlyContinue | ForEach-Object {
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    Write-Host "  killed PID $($_.Id)"
}
Start-Sleep -Seconds 2

# 3. Relaunch LLT elevated
Write-Host "[3/4] Relaunching LLT elevated..." -ForegroundColor Cyan
$llt = "$env:LOCALAPPDATA\Programs\LenovoLegionToolkit\Lenovo Legion Toolkit.exe"
if (Test-Path $llt) {
    Start-Process -FilePath $llt
    Write-Host "  launched: $llt"
} else {
    Write-Host "  NOT FOUND at $llt" -ForegroundColor Red
    exit 2
}

# 4. Wait for new log + dump dashboard items
Write-Host "[4/4] Waiting 12s for LLT to enumerate hardware..." -ForegroundColor Cyan
Start-Sleep -Seconds 12

$logDir = "$env:LOCALAPPDATA\LenovoLegionToolkit\log"
$newest = Get-ChildItem $logDir -ErrorAction SilentlyContinue |
          Sort-Object LastWriteTime -Descending | Select-Object -First 1

$reportPath = Join-Path (Split-Path $PSCommandPath -Parent) 'rgb_enable_llt_admin.report.txt'
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("RGB Enablement Report - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$sb.AppendLine("Log: $($newest.FullName)")
[void]$sb.AppendLine("")

if ($newest) {
    Write-Host "  log: $($newest.FullName)"
    Write-Host ""
    Write-Host "=== NEW DASHBOARD ITEMS ===" -ForegroundColor Green
    $dashMatches = Select-String -Path $newest.FullName -Pattern 'DashboardPage.*Items:' |
                   Select-Object -Last 5
    foreach ($m in $dashMatches) {
        Write-Host $m.Line
        [void]$sb.AppendLine($m.Line)
    }
    [void]$sb.AppendLine("")
    Write-Host ""
    Write-Host "=== RGB / SPECTRUM / FOURZONE FEATURE PROBES ===" -ForegroundColor Green
    $rgbMatches = Select-String -Path $newest.FullName -Pattern 'Spectrum|RGBKeyboardBacklightFeature|FourZone|RGB Keyboard|LampArray' |
                  Select-Object -First 30
    foreach ($m in $rgbMatches) {
        Write-Host $m.Line
        [void]$sb.AppendLine($m.Line)
    }
} else {
    Write-Host "  no log found" -ForegroundColor Red
}

Set-Content -Path $reportPath -Value $sb.ToString() -Encoding UTF8
Write-Host ""
Write-Host "Report written to: $reportPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "=== DONE ===" -ForegroundColor Cyan
Write-Host "Now check LLT sidebar: 'RGB Keyboard' or 'Spectrum Keyboard' or '4-Zone RGB' should appear."
Write-Host "Press Enter to close..."
[void](Read-Host)
