# Open the Lenovo driver download page keyed to this exact machine's MTM + serial.
# More useful than the generic model page because it filters to drivers Lenovo
# publishes for YOUR exact SKU. Lenovo's page is JS-rendered so we can't easily
# scrape; opening it in the browser is the practical path.
#
# Derives the URL from Win32_BIOS (SerialNumber) and SystemSKUNumber. Falls back
# to the generic product page if we can't extract the specific CTO identifier.

$ErrorActionPreference = 'Continue'

Write-Host "=== MACHINE IDENTIFIERS ==="
$cs  = Get-CimInstance Win32_ComputerSystem
$bios = Get-CimInstance Win32_BIOS
$serial = $bios.SerialNumber
$sku    = $cs.SystemSKUNumber
$mt     = $cs.Model

Write-Host "  Manufacturer: $($cs.Manufacturer)"
Write-Host "  Model:        $mt"
Write-Host "  SystemSKU:    $sku"
Write-Host "  Serial:       $serial"

if ($cs.Manufacturer -notmatch 'LENOVO') {
  Write-Host ""
  Write-Host "  This script targets Lenovo machines. Your manufacturer is '$($cs.Manufacturer)'."
  Write-Host "  Opening the generic Windows Update driver settings instead."
  Start-Process 'ms-settings:windowsupdate-optionalupdates'
  exit 0
}

# The LLT warranty URL format is the closest we have to a direct "this exact
# machine" link. We've seen:
#   https://pcsupport.lenovo.com/us/en/products/LAPTOPS-AND-NETBOOKS/LEGION-SERIES/<MODEL>/<MT>/<MTCTO>/<SERIAL>/downloads/driver-list
# but the series slug (LEGION-SERIES) varies by line. Easier: Lenovo's landing
# page for a serial deep-links to drivers.

$candidates = @()
if ($serial) {
  $candidates += "https://pcsupport.lenovo.com/us/en/products/laptops-and-netbooks/legion-series/driver-list?machineTypeCode=$($mt)&serialNumber=$serial"
  $candidates += "https://pcsupport.lenovo.com/products/$serial/downloads/driver-list"
}
if ($mt) {
  $candidates += "https://pcsupport.lenovo.com/products/laptops-and-netbooks/legion-series/$mt/downloads/driver-list"
}

Write-Host ""
Write-Host "=== OPENING DRIVER PAGES ==="
foreach ($u in $candidates) {
  Write-Host "  $u"
  Start-Process $u
  Start-Sleep -Milliseconds 400
}

Write-Host ""
Write-Host "Hints once the page loads:"
Write-Host "  - 'Auto Update' card downloads Lenovo System Update (LSU), which does a per-machine driver pull."
Write-Host "  - 'Manual Update' lets you pick a driver category (Chipset, BIOS, Display, NIC, etc.)."
Write-Host "  - If 'NPU / AI Engine' is listed, that's the one that matters for Ryzen AI on LTSC."
