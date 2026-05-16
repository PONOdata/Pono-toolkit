# Full hardware + driver inventory. Enumerates every PnP device Windows sees,
# flags the ones with problems (missing/failed drivers, resource conflicts,
# disabled), groups by device class, and lists notable vendor driver versions.
#
# Safe, read-only. Most useful after a fresh Windows install (LTSC especially,
# since it strips some OEM drivers) to see exactly what needs attention.
#
# Common problem codes worth watching for:
#   CM_PROB_FAILED_START   = 10  (driver present but fails to start)
#   CM_PROB_DISABLED       = 22  (user-disabled)
#   CM_PROB_NEED_RESTART   = 14
#   CM_PROB_FAILED_INSTALL = 28  (driver not installed - most common on fresh LTSC)
#   CM_PROB_NO_DRIVER      = 18
#
# Exit codes:
#   0 - all devices OK
#   1 - at least one device has a non-OK status
#   2 - script error

$ErrorActionPreference = 'Continue'

function Format-ProblemCode {
  param([int]$code)
  switch ($code) {
    0  { 'OK' }
    10 { 'Device failed to start (driver present but broken)' }
    14 { 'Need restart to take effect' }
    18 { 'Reinstall the driver' }
    22 { 'Disabled by user' }
    24 { 'Device not present / failed / other issue' }
    28 { 'Drivers not installed' }
    31 { 'Windows cannot load the drivers required' }
    43 { 'Reported a problem (typically USB/hardware fault)' }
    45 { 'Device not currently connected' }
    default { "Code $code" }
  }
}

Write-Host "=== HARDWARE + DRIVER SCAN ==="
$all = Get-PnpDevice -ErrorAction SilentlyContinue

if (-not $all) {
  Write-Host "  Get-PnpDevice returned nothing. Check PS version or module state."
  exit 2
}

$total    = $all.Count
$okCount  = ($all | Where-Object { $_.Status -eq 'OK' }).Count
$errors   = $all | Where-Object { $_.Status -ne 'OK' -and $_.Status -ne 'Unknown' }
$missing  = $all | Where-Object { $_.Problem -eq 28 -or $_.Problem -eq 18 }

Write-Host ""
Write-Host "Total devices enumerated: $total"
Write-Host "  OK:                     $okCount"
Write-Host "  With error status:      $($errors.Count)"
Write-Host "  Missing drivers (18/28):$($missing.Count)"

Write-Host ""
Write-Host "=== PROBLEM DEVICES (priority: fix these first) ==="
if ($errors) {
  $errors |
    Select-Object Status, Class, FriendlyName, @{n='Problem';e={Format-ProblemCode $_.Problem}}, InstanceId |
    Sort-Object Status, Class |
    Format-Table -AutoSize -Wrap
} else {
  Write-Host "  None. All devices report OK."
}

Write-Host ""
Write-Host "=== MISSING-DRIVER DEVICES - full HardwareID (for vendor lookup) ==="
if ($missing) {
  foreach ($d in $missing) {
    $hwIds = (Get-PnpDeviceProperty -InstanceId $d.InstanceId -KeyName 'DEVPKEY_Device_HardwareIds' -EA 0).Data
    Write-Host "  Device: $($d.FriendlyName)"
    Write-Host "    Class:   $($d.Class)"
    Write-Host "    Problem: $(Format-ProblemCode $d.Problem)"
    if ($hwIds) { foreach ($id in $hwIds) { Write-Host "    HWID:    $id" } }
    Write-Host ""
  }
} else {
  Write-Host "  None."
}

Write-Host ""
Write-Host "=== DEVICE COUNT BY CLASS ==="
$all | Group-Object Class | Sort-Object Count -Descending |
  Select-Object Count, @{n='Class';e={$_.Name}}, @{n='OK';e={($_.Group | Where-Object { $_.Status -eq 'OK' }).Count}} |
  Format-Table -AutoSize

Write-Host "=== NOTABLE VENDOR DRIVERS (versions) ==="
$notable = @(
  'AMD Ryzen',
  'AMD Radeon',
  'AMD Chipset',
  'AMD PMF',
  'AMD PSP',
  'NVIDIA GeForce',
  'NVIDIA Graphics',
  'Intel',
  'NPU Compute',
  'IPU',
  'Realtek',
  'MediaTek',
  'Qualcomm',
  'Lenovo',
  'Razer'
)
foreach ($pattern in $notable) {
  $hits = $all | Where-Object { $_.FriendlyName -match $pattern } | Select-Object -First 3
  foreach ($d in $hits) {
    $ver = (Get-PnpDeviceProperty -InstanceId $d.InstanceId -KeyName 'DEVPKEY_Device_DriverVersion' -EA 0).Data
    $dt  = (Get-PnpDeviceProperty -InstanceId $d.InstanceId -KeyName 'DEVPKEY_Device_DriverDate'    -EA 0).Data
    $prov= (Get-PnpDeviceProperty -InstanceId $d.InstanceId -KeyName 'DEVPKEY_Device_DriverProvider' -EA 0).Data
    if ($ver) {
      Write-Host ("  [{0,-12}] {1,-45} v{2,-18} {3}" -f $prov, $d.FriendlyName, $ver, $dt)
    }
  }
}

Write-Host ""
Write-Host "=== WINDOWS UPDATE DRIVER STATE ==="
Write-Host "  Run 'Get-WindowsUpdate -Category Drivers' with PSWindowsUpdate for live scan."
Write-Host "  Or: Settings -> Windows Update -> Advanced options -> Optional updates -> Driver updates"

Write-Host ""
Write-Host "=== OEM INSTALLED INF FILES (pnputil, first 30 lines) ==="
pnputil /enum-drivers 2>&1 |
  Select-String -Pattern 'Published Name|Original Name|Provider Name|Class Name|Driver Version' |
  Select-Object -First 30 | ForEach-Object { Write-Host "  $_" }
Write-Host "  ... truncated. Run 'pnputil /enum-drivers' for the full list."

Write-Host ""
Write-Host "=== DONE ==="
if ($errors -or $missing) { exit 1 } else { exit 0 }
