# First-run setup for pono-imessage Windows server.
# Creates a conda env, installs deps, generates a shared secret, ACL-hardens
# the secret file, persists the secret as a User-scope env var.
#
# Requires Miniforge3 already on Machine PATH (see windows-pono repo for that).

$ErrorActionPreference = 'Stop'

$windowsDir = $PSScriptRoot

# Find conda.
$condaCandidates = @(
  "$env:USERPROFILE\miniforge3\condabin\conda.bat",
  'C:\ProgramData\miniforge3\condabin\conda.bat'
)
$conda = $null
foreach ($c in $condaCandidates) { if (Test-Path $c) { $conda = $c; break } }
if (-not $conda) {
  Write-Host "Miniforge not found. Install it:"
  Write-Host "  winget install --id CondaForge.Miniforge3 --silent --accept-source-agreements --accept-package-agreements"
  exit 1
}

# Create env (Python 3.11).
Write-Host "=== Creating conda env 'pono-imessage' (Python 3.11) ==="
$envs = & $conda env list 2>$null
if ($envs -notmatch '^pono-imessage\s') {
  & $conda create -n pono-imessage -y python=3.11 | Out-Host
}

Write-Host ""
Write-Host "=== Installing dependencies ==="
& $conda run -n pono-imessage pip install -r (Join-Path $windowsDir 'requirements.txt') | Out-Host

# Shared secret.
$secretDir = Join-Path $env:LOCALAPPDATA 'pono-imessage'
$keyFile   = Join-Path $secretDir 'shared_secret.txt'
New-Item -ItemType Directory -Force -Path $secretDir | Out-Null

if (-not (Test-Path $keyFile)) {
  Write-Host ""
  Write-Host "=== Generating shared secret ==="
  $secret = & $conda run -n pono-imessage python -c "import secrets; print(secrets.token_urlsafe(32))"
  $secret = $secret.Trim()
  Set-Content -Path $keyFile -Value $secret -NoNewline -Encoding ASCII
  Write-Host "  wrote: $keyFile"
} else {
  $secret = (Get-Content $keyFile -Raw).Trim()
  Write-Host "  reusing existing shared secret at $keyFile"
}

# ACL-harden the secret file: only the current user gets any access.
# /inheritance:r removes inherited permissions so we start clean.
# /grant:r <user>:F = grant full control to the user.
Write-Host ""
Write-Host "=== ACL-hardening $keyFile ==="
try {
  $userPrincipal = "$env:USERDOMAIN\$env:USERNAME"
  icacls $keyFile /inheritance:r 2>&1 | Out-Host
  icacls $keyFile /grant:r "${userPrincipal}:F" 2>&1 | Out-Host
  Write-Host "  ACLs set: owner-only read/write."
} catch {
  Write-Host "  WARNING: failed to harden ACLs: $_"
  Write-Host "  Run manually: icacls `"$keyFile`" /inheritance:r /grant:r `"${userPrincipal}:F`""
}

# Persist env var (User scope).
[Environment]::SetEnvironmentVariable('PONO_IMESSAGE_KEY', $secret, 'User')
Write-Host "  set PONO_IMESSAGE_KEY (User scope env var)"

Write-Host ""
Write-Host "=== DONE ==="
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Expose the listener to the internet. In a separate terminal:"
Write-Host "       cloudflared tunnel --url http://127.0.0.1:8765"
Write-Host "     Copy the https://<random>.trycloudflare.com URL it prints."
Write-Host ""
Write-Host "  2. Build the iOS Shortcut per ios/shortcut-receive.md."
Write-Host "     POST target: your trycloudflare URL + '/inbound'"
Write-Host "     Headers:     X-Pono-Key: <your secret from $keyFile>"
Write-Host ""
Write-Host "  3. Launch the server:"
Write-Host "       .\\run.ps1"
Write-Host ""
Write-Host "  4. Open http://127.0.0.1:8765/ in your browser. Paste the key ONCE per tab."
Write-Host "     Do not pass ?key= in the URL - the server rejects it (prevents secret-in-logs)."
