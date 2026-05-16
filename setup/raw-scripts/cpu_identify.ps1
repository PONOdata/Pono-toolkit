# Definitive CPU/NPU identification. Ignores marketing names, uses CPUID.
# Hawk Point (Phoenix 2 refresh, XDNA 1): Family 25 (0x19) Model 117 (0x75)
# Strix Point (XDNA 2):                   Family 26 (0x1A) Model  36 (0x24)
# Krackan Point (XDNA 2):                 Family 26 (0x1A) Model  32 (0x20)
# Strix Halo (XDNA 2):                    Family 26 (0x1A) Model  96 (0x60)

$ErrorActionPreference = 'Continue'

Write-Host "=== CPU (Win32_Processor) ==="
$cpus = Get-CimInstance Win32_Processor
foreach ($c in $cpus) {
  Write-Host ("  Name:           {0}" -f $c.Name)
  Write-Host ("  Manufacturer:   {0}" -f $c.Manufacturer)
  Write-Host ("  Description:    {0}" -f $c.Description)
  Write-Host ("  Family (int):   {0}" -f $c.Family)
  Write-Host ("  NumberOfCores:  {0}" -f $c.NumberOfCores)
  Write-Host ("  LogicalProc:    {0}" -f $c.NumberOfLogicalProcessors)
  Write-Host ("  MaxClockSpeed:  {0} MHz" -f $c.MaxClockSpeed)
  Write-Host ("  ProcessorId:    {0}" -f $c.ProcessorId)
}

Write-Host ""
Write-Host "=== CPUID DECODE (from Registry, what Windows sees at boot) ==="
$cpu0 = Get-ItemProperty 'HKLM:\HARDWARE\DESCRIPTION\System\CentralProcessor\0' -ErrorAction SilentlyContinue
if ($cpu0) {
  Write-Host "  ProcessorNameString: $($cpu0.'ProcessorNameString')"
  Write-Host "  VendorIdentifier:    $($cpu0.VendorIdentifier)"
  Write-Host "  Identifier:          $($cpu0.Identifier)"
  if ($cpu0.Identifier -match 'Family (\d+) Model (\d+) Stepping (\d+)') {
    $family   = [int]$matches[1]
    $model    = [int]$matches[2]
    $stepping = [int]$matches[3]
    Write-Host ("  Decoded: Family={0} (0x{0:X2}) Model={1} (0x{1:X2}) Stepping={2}" -f $family, $model, $stepping)

    $verdict = switch ($true) {
      ($family -eq 25 -and $model -eq 117) { 'HAWK POINT (Phoenix 2 refresh) — XDNA 1, ~16 TOPS' ; break }
      ($family -eq 25 -and $model -eq 116) { 'PHOENIX (original) — XDNA 1, ~10 TOPS' ; break }
      ($family -eq 26 -and $model -eq 36)  { 'STRIX POINT — XDNA 2, ~50 TOPS' ; break }
      ($family -eq 26 -and $model -eq 32)  { 'KRACKAN POINT — XDNA 2, ~50 TOPS' ; break }
      ($family -eq 26 -and $model -eq 96)  { 'STRIX HALO — XDNA 2, ~50 TOPS' ; break }
      default                              { "UNKNOWN: Family=$family Model=$model - look up manually" }
    }
    Write-Host ""
    Write-Host "  ==> VERDICT: $verdict" -ForegroundColor Green
  }
}

Write-Host ""
Write-Host "=== iGPU (Strix Point ships 880M/890M; Hawk Point ships 780M) ==="
Get-CimInstance Win32_VideoController |
  Where-Object { $_.Name -match 'Radeon|AMD' } |
  Select-Object Name, DriverVersion |
  Format-Table -AutoSize

Write-Host "=== NPU FRIENDLY NAME ==="
Get-PnpDevice -ErrorAction SilentlyContinue |
  Where-Object { $_.FriendlyName -match 'NPU|IPU|Neural' } |
  Select-Object FriendlyName, @{n='DriverVersion';e={(Get-PnpDeviceProperty -InstanceId $_.InstanceId -KeyName 'DEVPKEY_Device_DriverVersion' -EA 0).Data}} |
  Format-Table -AutoSize
