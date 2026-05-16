# Remove Google Drive's shell icon overlay handlers WITHOUT uninstalling Drive.
# Sync keeps working. The green/grey/blue status checkmarks on every file go away.
#
# What we're doing:
#   Under HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\
#   ShellIconOverlayIdentifiers there are entries like:
#     GDFS-Synced          (the green check)
#     GDFS-Syncing
#     GDFS-Error
#     GDFS-UpToDate
#     GDContentsColumnHandler
#     DriveFS-*
#   Each one is a COM handler Explorer queries for every icon paint. Deleting
#   the keys under ShellIconOverlayIdentifiers removes the overlay but leaves
#   the underlying COM object + Drive itself intact.
#
# Reversible: we export every key we're about to delete to a .reg file first.
# Re-import the .reg file to restore the overlays.
#
# Requires admin. Restarts explorer.exe at the end so the change takes effect.

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Continue'
$transcript = 'C:\Brofalo\scripts\fix_google_overlays.transcript.txt'
$backupDir  = 'C:\Brofalo\scripts\overlay_backup'
New-Item -ItemType Directory -Force -Path (Split-Path $transcript) | Out-Null
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
Start-Transcript -Path $transcript -Force | Out-Null

$ovPaths = @(
  'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ShellIconOverlayIdentifiers',
  'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\ShellIconOverlayIdentifiers'
)

Write-Host "=== SCANNING FOR GOOGLE OVERLAY HANDLERS ==="
$hits = @()
foreach ($p in $ovPaths) {
  if (-not (Test-Path $p)) { continue }
  Get-ChildItem $p -ErrorAction SilentlyContinue |
    Where-Object { $_.PSChildName -match 'Google|Drive|GDFS|GDContents|DriveFS' } |
    ForEach-Object {
      $hits += [pscustomobject]@{
        Root = $p
        Name = $_.PSChildName
        FullPath = $_.PsPath
        RegPath  = $_.Name
      }
    }
}

if (-not $hits) {
  Write-Host "  No Google Drive overlay entries found. Nothing to do."
  Stop-Transcript | Out-Null
  exit 0
}

Write-Host ("  Found {0} entries:" -f $hits.Count)
$hits | Format-Table Root, Name -AutoSize

# Backup every hit to a single .reg file so we can restore.
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$regBackup = Join-Path $backupDir "google_overlays_$stamp.reg"
Write-Host ""
Write-Host "=== BACKING UP TO $regBackup ==="
# reg export wants a registry-style path (no 'HKLM:' prefix), so convert.
foreach ($h in $hits) {
  $regStylePath = $h.RegPath   # reg.exe expects HKEY_LOCAL_MACHINE\... form already
  $tmp = [System.IO.Path]::GetTempFileName() + '.reg'
  $null = & reg.exe export $regStylePath $tmp /y 2>&1
  if (Test-Path $tmp) {
    Get-Content $tmp | Add-Content $regBackup
    Remove-Item $tmp -Force
    Write-Host "  backed up: $($h.RegPath)"
  } else {
    Write-Host "  WARNING: failed to export $($h.RegPath)"
  }
}

Write-Host ""
Write-Host "=== DELETING OVERLAY HANDLER REGISTRATIONS ==="
foreach ($h in $hits) {
  try {
    Remove-Item -Path $h.FullPath -Recurse -Force -ErrorAction Stop
    Write-Host "  deleted: $($h.Name)  under $($h.Root)"
  } catch {
    Write-Host "  FAILED to delete $($h.Name): $_"
  }
}

Write-Host ""
Write-Host "=== RESTARTING EXPLORER.EXE (overlays reload on restart) ==="
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 800
Start-Process explorer.exe
Write-Host "  explorer restarted."

Write-Host ""
Write-Host "=== DONE ==="
Write-Host "To restore the overlays later, re-import:"
Write-Host "  reg.exe import `"$regBackup`""
Write-Host "Google Drive sync keeps working regardless; only the icon overlays are affected."
Stop-Transcript | Out-Null
