# Scan Windows shell icon overlay handlers and Google-related installs.
# The green checks on files come from overlay handlers that Google Drive for
# desktop registers. Windows has a 15-slot limit on overlays, and Google
# sometimes registers 4-8 of them. Uninstalling Drive doesn't always clean
# these up.
# Read-only.

$ErrorActionPreference = 'Continue'

Write-Host "=== GOOGLE / DRIVE PACKAGES INSTALLED ==="
$roots = @(
  'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
  'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall',
  'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'
)
Get-ChildItem $roots -ErrorAction SilentlyContinue |
  Get-ItemProperty |
  Where-Object { $_.DisplayName -match 'Google|Drive|Backup and Sync' -and $_.DisplayName -notmatch 'Chrome' } |
  Select-Object DisplayName, DisplayVersion, Publisher, InstallLocation, UninstallString |
  Format-List

Write-Host "=== GOOGLE DRIVE PROCESSES ==="
Get-Process | Where-Object { $_.ProcessName -match 'GoogleDrive|DriveFS|Backup' } |
  Select-Object ProcessName, Id, Path | Format-Table -AutoSize -Wrap

Write-Host "=== SHELL ICON OVERLAY IDENTIFIERS (all) ==="
$ovPaths = @(
  'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ShellIconOverlayIdentifiers',
  'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\ShellIconOverlayIdentifiers'
)
foreach ($p in $ovPaths) {
  if (Test-Path $p) {
    Write-Host ""
    Write-Host "  $p"
    Get-ChildItem $p -ErrorAction SilentlyContinue | ForEach-Object {
      $clsid = (Get-ItemProperty $_.PsPath -Name '(default)' -ErrorAction SilentlyContinue).'(default)'
      Write-Host ("    {0,-40}  CLSID: {1}" -f $_.PSChildName, $clsid)
    }
  }
}

Write-Host ""
Write-Host "=== GOOGLE-SPECIFIC OVERLAY ENTRIES (fix targets) ==="
foreach ($p in $ovPaths) {
  if (Test-Path $p) {
    Get-ChildItem $p -ErrorAction SilentlyContinue |
      Where-Object { $_.PSChildName -match 'Google|Drive|GDFS|DriveFS' } |
      ForEach-Object { Write-Host "  $($_.PsPath)" }
  }
}

Write-Host ""
Write-Host "=== SCHEDULED TASKS (Drive often keeps one around) ==="
Get-ScheduledTask -ErrorAction SilentlyContinue |
  Where-Object { $_.TaskName -match 'Google|Drive' } |
  Select-Object TaskName, State, TaskPath | Format-Table -AutoSize -Wrap

Write-Host "=== DONE ==="
Write-Host ""
Write-Host "If entries show up under GOOGLE-SPECIFIC OVERLAY ENTRIES, run"
Write-Host "  drivers/fix-google-overlays.ps1  (elevated)"
Write-Host "to remove them and restart Explorer."
