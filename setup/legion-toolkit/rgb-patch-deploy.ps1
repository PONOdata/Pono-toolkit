# Deploy a patched LenovoLegionToolkit.Lib.dll over an installed LLT to fix
# RGB detection on machine types the official build hasn't mapped yet.
#
# The patch: add your machine type to MachineTypeMap in
# LenovoLegionToolkit.Lib/Utils/Compatibility.cs so LLT classifies it correctly.
# See ../legion-toolkit/README.md for the full methodology and how to build the DLL.
#
# Why: LLT's RGB controllers (Spectrum per-key and 4-zone) bind by keyboard HID
# PID *prefix*, which varies by LegionSeries. If the series classification is
# 'Unknown' (because MachineTypeMap lacks your MTM), the HID hunt never matches
# and the RGB card never renders.
#
# Parameters:
#   -BuiltDll  path to your locally-built patched LenovoLegionToolkit.Lib.dll
#              (default: $PSScriptRoot\_build\LenovoLegionToolkit.Lib.dll)
#   -LLTExe    path to the installed LLT main exe
#              (default: $env:LOCALAPPDATA\Programs\LenovoLegionToolkit\Lenovo Legion Toolkit.exe)
#
# Backs up the original DLL to *.bak so rollback is a one-line copy.
# Requires admin.

#Requires -RunAsAdministrator

param(
  [string]$BuiltDll = (Join-Path $PSScriptRoot '_build\LenovoLegionToolkit.Lib.dll'),
  [string]$LLTExe   = "$env:LOCALAPPDATA\Programs\LenovoLegionToolkit\Lenovo Legion Toolkit.exe"
)

$ErrorActionPreference = 'Continue'

$instDir = Split-Path $LLTExe -Parent
$target  = Join-Path $instDir 'LenovoLegionToolkit.Lib.dll'
$backup  = "$target.bak"

if (-not (Test-Path $BuiltDll)) {
  Write-Host "ERROR: patched DLL not found at $BuiltDll"
  Write-Host "Build one first. See legion-toolkit/README.md for the build steps."
  exit 1
}
if (-not (Test-Path $target)) {
  Write-Host "ERROR: target DLL not found at $target"
  Write-Host "Is LLT installed? Run llt-upgrade-to-fork.ps1 first."
  exit 1
}

Write-Host "=== KILL LLT ==="
Get-Process -Name 'Lenovo Legion Toolkit','llt' -EA 0 | ForEach-Object {
  Stop-Process -Id $_.Id -Force -EA 0
  Write-Host "  killed PID $($_.Id)"
}
Start-Sleep -Seconds 2

Write-Host "=== BACKUP ORIGINAL ==="
if (-not (Test-Path $backup)) {
  Copy-Item $target $backup
  Write-Host "  backed up: $backup"
} else {
  Write-Host "  backup already exists (keeping the first one — don't clobber true original)"
}

Write-Host "=== DEPLOY PATCHED DLL ==="
Copy-Item $BuiltDll $target -Force
Write-Host "  installed: $((Get-Item $target).Length) bytes"

Write-Host "=== LAUNCH LLT ==="
Start-Process $LLTExe
Write-Host "  launched."

Start-Sleep -Seconds 12

Write-Host ""
Write-Host "=== POST-PATCH LOG EXCERPT ==="
$logDir = "$env:LOCALAPPDATA\LenovoLegionToolkit\log"
if (Test-Path $logDir) {
  $log = (Get-ChildItem $logDir | Sort-Object LastWriteTime -Desc | Select-Object -First 1).FullName
  Write-Host "  log: $log"
  Select-String -Path $log -Pattern 'LegionSeries|MachineType|RGB Keyboard|Spectrum|Found device|unsupported' |
    Select-Object -Last 40 |
    ForEach-Object { Write-Host "  $($_.Line)" }
} else {
  Write-Host "  no LLT log dir yet."
}

Write-Host ""
Write-Host "If LLT misbehaves, roll back with:"
Write-Host "  Copy-Item '$backup' '$target' -Force"
