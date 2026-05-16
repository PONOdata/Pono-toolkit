# Thermal + crash diagnostic for Legion 5 15AHP11 (Ryzen AI 7 250, LTSC 2024).
# Pulls: last unexpected shutdown events, thermal events, current CPU package temp,
# AMD chipset driver presence, active power plan, and LLT power-mode state.
# Read-only.

$ErrorActionPreference = 'Continue'

Write-Host "=== UNEXPECTED SHUTDOWN EVENTS (Kernel-Power 41, last 24h) ==="
Get-WinEvent -FilterHashtable @{
  LogName='System'; ProviderName='Microsoft-Windows-Kernel-Power'; Id=41;
  StartTime=(Get-Date).AddHours(-24)
} -ErrorAction SilentlyContinue |
  Select-Object TimeCreated, Id, LevelDisplayName, @{n='Message';e={$_.Message.Substring(0,[Math]::Min(200,$_.Message.Length))}} |
  Format-Table -AutoSize -Wrap

Write-Host ""
Write-Host "=== THERMAL / WHEA EVENTS (last 24h) ==="
Get-WinEvent -FilterHashtable @{
  LogName='System'; StartTime=(Get-Date).AddHours(-24)
} -ErrorAction SilentlyContinue |
  Where-Object { $_.ProviderName -match 'WHEA|Thermal' -or $_.Message -match 'thermal|overheat|throttle' } |
  Select-Object -First 20 TimeCreated, ProviderName, Id, @{n='Message';e={$_.Message.Substring(0,[Math]::Min(200,$_.Message.Length))}} |
  Format-Table -AutoSize -Wrap

Write-Host ""
Write-Host "=== BUGCHECK (BSOD) EVENTS (last 24h) ==="
Get-WinEvent -FilterHashtable @{
  LogName='System'; Id=1001; StartTime=(Get-Date).AddHours(-24)
} -ErrorAction SilentlyContinue |
  Select-Object TimeCreated, @{n='Message';e={$_.Message.Substring(0,[Math]::Min(400,$_.Message.Length))}} |
  Format-Table -AutoSize -Wrap

Write-Host ""
Write-Host "=== LAST BOOT TIME ==="
(Get-CimInstance Win32_OperatingSystem).LastBootUpTime

Write-Host ""
Write-Host "=== CURRENT CPU TEMP (WMI thermal zones, deci-Kelvin) ==="
try {
  $zones = Get-WmiObject -Namespace 'root/wmi' -Class MSAcpi_ThermalZoneTemperature -ErrorAction Stop
  foreach ($z in $zones) {
    $c = [math]::Round(($z.CurrentTemperature / 10) - 273.15, 1)
    Write-Host ("  {0}: {1} C (raw {2})" -f $z.InstanceName, $c, $z.CurrentTemperature)
  }
} catch {
  Write-Host "  MSAcpi_ThermalZoneTemperature unavailable: $_"
}

Write-Host ""
Write-Host "=== ACTIVE POWER PLAN ==="
powercfg /getactivescheme
Write-Host ""
Write-Host "=== ALL POWER PLANS ==="
powercfg /list

Write-Host ""
Write-Host "=== AMD CHIPSET / PROCESSOR DRIVERS ==="
Get-PnpDevice -Class 'Processor','System' -ErrorAction SilentlyContinue |
  Where-Object { $_.FriendlyName -match 'AMD|Ryzen|Chipset|Processor' } |
  Select-Object Status, FriendlyName, @{n='Driver';e={(Get-PnpDeviceProperty -InstanceId $_.InstanceId -KeyName 'DEVPKEY_Device_DriverVersion' -ErrorAction SilentlyContinue).Data}} |
  Format-Table -AutoSize -Wrap

Write-Host ""
Write-Host "=== AMD PACKAGES INSTALLED ==="
$uninstallRoots = @(
  'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
  'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
)
Get-ChildItem $uninstallRoots -ErrorAction SilentlyContinue |
  Get-ItemProperty |
  Where-Object { $_.DisplayName -match 'AMD|Ryzen|Chipset|Radeon|Adrenalin' } |
  Select-Object DisplayName, DisplayVersion, Publisher |
  Format-Table -AutoSize

Write-Host ""
Write-Host "=== FAN / EMBEDDED CONTROLLER (if exposed) ==="
Get-PnpDevice | Where-Object { $_.FriendlyName -match 'Fan|Embedded' } |
  Select-Object Status, FriendlyName, InstanceId | Format-Table -AutoSize -Wrap

Write-Host ""
Write-Host "=== RECENT MINIDUMPS ==="
$dumpDir = 'C:\Windows\Minidump'
if (Test-Path $dumpDir) {
  Get-ChildItem $dumpDir -Filter *.dmp -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 5 |
    Format-Table Name, Length, LastWriteTime -AutoSize
} else {
  Write-Host "  No minidump dir."
}

Write-Host ""
Write-Host "=== DONE ==="
