# Reversible fix test: stop Lenovo Vantage service + kill Vantage processes
# so Lenovo Legion Toolkit (LLT) can claim the hardware RGB interface.
#
# Why: LLT documentation says "LLT will disable RGB controls when Vantage is running
# to avoid conflicts." Machine has LenovoVantageService Running + 5 Vantage processes.
#
# Reversible: service start type left at Automatic - reboot restores state.
# Persist later via LLT Settings > "Disable Lenovo Vantage" toggle or full uninstall.
#
# Requires admin (UAC). Right-click this file -> Run with PowerShell (as admin).

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Continue'

Write-Host "=== BEFORE ==="
Get-Service LenovoVantageService, LenovoFnAndFunctionKeys -ErrorAction SilentlyContinue |
  Select-Object Name, Status, StartType | Format-Table -AutoSize

Write-Host ""
Write-Host "=== STOPPING VANTAGE SERVICE ==="
try {
  Stop-Service -Name LenovoVantageService -Force -ErrorAction Stop
  Write-Host "  LenovoVantageService stopped."
} catch {
  Write-Host "  ERROR stopping LenovoVantageService: $_"
}

Write-Host ""
Write-Host "=== KILLING VANTAGE PROCESSES ==="
$names = @('LenovoVantage-(GenericMessagingAddin)',
           'LenovoVantage-(LenovoServiceBridgeAddin)',
           'LenovoVantage-(SmartDisplayAddin)',
           'LenovoVantage-(VantageCoreAddin)',
           'LenovoVantageService',
           'LenovoUtilityService')
foreach ($n in $names) {
  Get-Process -Name $n -ErrorAction SilentlyContinue | ForEach-Object {
    try {
      Stop-Process -Id $_.Id -Force -ErrorAction Stop
      Write-Host "  killed: $($_.ProcessName) (PID $($_.Id))"
    } catch {
      Write-Host "  failed to kill $($_.ProcessName) (PID $($_.Id)): $_"
    }
  }
}

Write-Host ""
Write-Host "=== KILLING LLT SO IT RESTARTS CLEAN ==="
Get-Process -Name 'Lenovo Legion Toolkit','llt' -ErrorAction SilentlyContinue |
  ForEach-Object {
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    Write-Host "  killed LLT PID $($_.Id)"
  }

Start-Sleep -Seconds 2

Write-Host ""
Write-Host "=== AFTER ==="
Get-Service LenovoVantageService, LenovoFnAndFunctionKeys -ErrorAction SilentlyContinue |
  Select-Object Name, Status, StartType | Format-Table -AutoSize
Get-Process | Where-Object { $_.ProcessName -match 'Lenovo|Vantage' } |
  Select-Object ProcessName, Id | Format-Table -AutoSize

Write-Host ""
Write-Host "=== RELAUNCHING LLT ==="
$llt = "$env:LOCALAPPDATA\Programs\LenovoLegionToolkit\Lenovo Legion Toolkit.exe"
if (Test-Path $llt) {
  Start-Process $llt
  Write-Host "  LLT launched. Check for RGB Keyboard section in the sidebar."
} else {
  Write-Host "  LLT not found at $llt"
}

Write-Host ""
Write-Host "=== DONE ==="
