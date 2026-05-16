# ============================================================================
#  WORK IN PROGRESS - DO NOT RUN YET.
#
#  Claude auto-install is on hold until the Pono Claude deployment is trained
#  and ready to ship to fleet nodes. Running this now installs a stock Claude
#  that has no Pono memory, no skills, no PAL wiring - defeats the point.
#
#  When training lands:
#    - Add the pre-baked memory/skills bundle to this repo under claude/bundle/
#    - Extend this script to pull the bundle into %USERPROFILE%\.claude\ after install
#    - Drop this banner
#
#  See claude/README.md for status.
# ============================================================================

Write-Host "Claude auto-install is WIP. See claude/README.md." -ForegroundColor Yellow
Write-Host "Aborting. No changes made."
exit 3

# ----------------------------------------------------------------------------
# Skeleton below. Unreachable until the banner is removed above.
# Order when re-enabled:
#   1. Node.js LTS (prereq for the Claude Code CLI)
#   2. Claude Code CLI (@anthropic-ai/claude-code, installed via npm)
#   3. Claude Desktop (Anthropic.Claude, via winget)
#   4. Pono memory/skills bundle restore (TBD - lives at claude/bundle/ when ready)
#   5. Authentication hint (we do not store credentials)
# ----------------------------------------------------------------------------

$ErrorActionPreference = 'Continue'

function Test-Exe {
  param([string]$name)
  $null -ne (Get-Command $name -ErrorAction SilentlyContinue)
}

function Invoke-Winget {
  param([string]$id, [string]$label = $id)
  Write-Host ""
  Write-Host "=== winget install $label ($id) ==="
  winget install --id $id --silent --accept-source-agreements --accept-package-agreements 2>&1 |
    ForEach-Object { Write-Host "  $_" }
}

# --- 1. Node.js LTS ---------------------------------------------------------

Write-Host "=== STEP 1: Node.js LTS ==="
if (Test-Exe 'node') {
  $v = & node --version 2>$null
  Write-Host "  node already present: $v"
} else {
  Invoke-Winget -id 'OpenJS.NodeJS.LTS' -label 'Node.js LTS'
  $machinePath = [Environment]::GetEnvironmentVariable('PATH','Machine')
  $userPath    = [Environment]::GetEnvironmentVariable('PATH','User')
  $env:PATH = "$machinePath;$userPath"
  if (Test-Exe 'node') {
    Write-Host "  node installed: $(& node --version)"
  } else {
    Write-Host "  node still not on PATH after install. Open a new terminal and re-run."
    exit 2
  }
}

# --- 2. Claude Code CLI -----------------------------------------------------

Write-Host ""
Write-Host "=== STEP 2: Claude Code CLI ==="
if (Test-Exe 'claude') {
  Write-Host "  claude already on PATH: $(& claude --version 2>$null)"
  Write-Host "  (to update: npm update -g @anthropic-ai/claude-code)"
} else {
  & npm install -g '@anthropic-ai/claude-code' 2>&1 | ForEach-Object { Write-Host "  $_" }
  $npmGlobal = "$env:APPDATA\npm"
  if ((Test-Path $npmGlobal) -and ($env:PATH -notmatch [regex]::Escape($npmGlobal))) {
    $env:PATH = "$npmGlobal;$env:PATH"
  }
  if (Test-Exe 'claude') {
    Write-Host "  claude installed: $(& claude --version 2>$null)"
  } else {
    Write-Host "  claude not on PATH. Check $npmGlobal."
  }
}

# --- 3. Claude Desktop ------------------------------------------------------

Write-Host ""
Write-Host "=== STEP 3: Claude Desktop (GUI) ==="
$desktopInstalled = Get-AppxPackage -Name '*Claude*' -ErrorAction SilentlyContinue
if ($desktopInstalled) {
  $desktopInstalled | Select-Object Name, Version | Format-Table -AutoSize
} else {
  Invoke-Winget -id 'Anthropic.Claude' -label 'Claude Desktop'
}

# --- 4. Pono bundle restore (TBD) -------------------------------------------

Write-Host ""
Write-Host "=== STEP 4: Pono memory + skills bundle ==="
$bundle = Join-Path $PSScriptRoot 'bundle'
if (Test-Path $bundle) {
  Write-Host "  bundle at: $bundle"
  Write-Host "  (restore logic TBD - waiting on training deliverable format)"
} else {
  Write-Host "  No bundle present at $bundle. Nothing to restore."
}

# --- 5. Auth hint -----------------------------------------------------------

Write-Host ""
Write-Host "=== STEP 5: Authenticate ==="
Write-Host "  CLI:     run 'claude' in a new terminal for OAuth, or set ANTHROPIC_API_KEY."
Write-Host "  Desktop: launch from Start menu, sign in."
Write-Host ""
Write-Host "=== DONE ==="
