# Launch Lenovo System Update (Tvsu.exe) elevated. LSU will detect 83Q7 and
# download/install any missing OEM drivers - specifically the Lenovo 4-zone
# RGB keyboard driver and related packages that LTSC 2024 omits but LLT needs
# in order to enumerate the RGB control surface.
#
# Why not pnputil with a direct .inf: Lenovo distributes the RGB driver inside
# a signed installer (.exe) that also registers a service, writes WMI classes,
# and prepopulates the ACPI RGB method table. Running LSU is the supported
# path; dropping the INF alone typically leaves the service uncreated.
#
# What this script does:
#   1. Self-elevate.
#   2. Launch Tvsu.exe. User clicks "Get new updates" and "Install selected".
#   3. Returns - reboot may be needed after the install.
#
# Reversibility: every driver LSU installs is visible in
# Settings > Apps > Installed apps and can be uninstalled individually.
# ---------------------------------------------------------------------------

$ErrorActionPreference = 'Continue'

# Self-elevate
$id = [Security.Principal.WindowsIdentity]::GetCurrent()
$p = [Security.Principal.WindowsPrincipal]::new($id)
if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Not elevated; relaunching as admin..." -ForegroundColor Yellow
    Start-Process -FilePath 'powershell.exe' `
        -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',$PSCommandPath) `
        -Verb RunAs
    exit 0
}

Write-Host "=== LAUNCHING LENOVO SYSTEM UPDATE (elevated) ===" -ForegroundColor Cyan
$tvsu = @(
    'C:\Program Files (x86)\Lenovo\System Update\Tvsu.exe',
    'C:\Program Files\Lenovo\System Update\Tvsu.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $tvsu) {
    Write-Host "Tvsu.exe not found. Install LSU from:" -ForegroundColor Red
    Write-Host "  https://pcsupport.lenovo.com/us/en/solutions/ht003029"
    Write-Host ""
    Write-Host "Press Enter..."
    [void](Read-Host)
    exit 2
}

Write-Host "  starting: $tvsu"
Start-Process -FilePath $tvsu
Write-Host ""
Write-Host "=== NEXT STEPS FOR JACK ===" -ForegroundColor Green
Write-Host "  1. Accept the License Agreement if prompted."
Write-Host "  2. Click 'Get new updates'."
Write-Host "  3. Select ALL Critical + Recommended updates."
Write-Host "  4. Click 'Next' then 'Install'."
Write-Host "  5. Let it finish (may take 5-15 min). Reboot if prompted."
Write-Host "  6. After reboot, run rgb_deep_diag.ps1 again to verify."
Write-Host ""
Write-Host "Press Enter to close this window..."
[void](Read-Host)
