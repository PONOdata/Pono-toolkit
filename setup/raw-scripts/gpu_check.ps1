# GPU inventory: list all video controllers, NVIDIA driver state, dGPU power state.
$ErrorActionPreference = 'Continue'

Write-Host "=== VIDEO CONTROLLERS ==="
Get-CimInstance Win32_VideoController |
  Select-Object Name, AdapterRAM, DriverVersion, VideoProcessor, Status, CurrentHorizontalResolution, CurrentVerticalResolution |
  Format-List

Write-Host "=== DISPLAY ADAPTERS (PnP, all states) ==="
Get-PnpDevice -Class 'Display' |
  Select-Object Status, FriendlyName, InstanceId |
  Format-Table -AutoSize -Wrap

Write-Host "=== NVIDIA PACKAGES ==="
Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall','HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall' -ErrorAction SilentlyContinue |
  Get-ItemProperty |
  Where-Object { $_.DisplayName -match 'NVIDIA|GeForce|Radeon|Adrenalin' } |
  Select-Object DisplayName, DisplayVersion | Format-Table -AutoSize

Write-Host "=== NVIDIA PROCESSES / SERVICES ==="
Get-Service | Where-Object { $_.Name -match 'NVIDIA|nv' -and $_.Name -notmatch 'nvagent|nvhost' } |
  Select-Object Name, Status, StartType | Format-Table -AutoSize
Get-Process | Where-Object { $_.ProcessName -match 'nvidia|nvcontainer|nvdisplay' } |
  Select-Object ProcessName, Id | Format-Table -AutoSize

Write-Host "=== LAST DISPLAY-RELATED EVENTS (last 24h) ==="
Get-WinEvent -FilterHashtable @{ LogName='System'; StartTime=(Get-Date).AddHours(-24) } -ErrorAction SilentlyContinue |
  Where-Object { $_.ProviderName -match 'Display|Video|nvlddmkm|amdkmdag|TDR' -or $_.Message -match 'nvidia|gpu|display driver' } |
  Select-Object -First 10 TimeCreated, ProviderName, Id, @{n='M';e={$_.Message.Substring(0,[Math]::Min(150,$_.Message.Length))}} |
  Format-Table -AutoSize -Wrap

Write-Host "=== LLT SETTINGS (GPU mode fields) ==="
$s = "$env:LOCALAPPDATA\LenovoLegionToolkit\settings.json"
if (Test-Path $s) { Get-Content $s | Select-String -Pattern 'GPU|Hybrid|Discrete|PowerMode' }

Write-Host "=== DONE ==="
