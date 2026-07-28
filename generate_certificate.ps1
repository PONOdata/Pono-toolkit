# Lenovo Legion Toolkit - Certificate Generation Script
# This script generates a self-signed certificate and prepares the manifest for Sparse Packaging.
# Run this script once to set up your environment or when you need to regenerate certificates.
#
# The password is a SecureString. Interactively:
#   .\generate_certificate.ps1 -Password (Read-Host "PFX password" -AsSecureString)
# In CI, leave -Password off and set LLT_CERT_PASSWORD instead.

param(
    [Parameter()]
    [System.Security.SecureString]$Password
)

$Publisher = "CN=LenovoLegionToolkit"
$CertPath = "LenovoLegionToolkit.pfx"
$PublicPath = "LenovoLegionToolkit.cer"

# LLT_CERT_PASSWORD is a plain environment variable, so it has to be converted
# once, here. NetworkCredential does that without ConvertTo-SecureString
# -AsPlainText, which PSScriptAnalyzer rejects as an error.
if (($null -eq $Password -or $Password.Length -eq 0) -and
    -not [string]::IsNullOrWhiteSpace($env:LLT_CERT_PASSWORD)) {
    $Password = [System.Net.NetworkCredential]::new('', $env:LLT_CERT_PASSWORD).SecurePassword
}

Write-Host "--- Lenovo Legion Toolkit Packaging Prep ---" -ForegroundColor Cyan

if ((-not (Test-Path $CertPath)) -and (-not (Test-Path $PublicPath))) {
    if ($null -eq $Password -or $Password.Length -eq 0) {
        throw "A password is required to protect the PFX. Pass -Password or set the LLT_CERT_PASSWORD environment variable."
    }

    try {
        Write-Host "Generating self-signed certificate for $Publisher..." -ForegroundColor Green
        
        $cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher `
            -KeyUsage DigitalSignature -FriendlyName "LLT Packaging Certificate" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -NotAfter (Get-Date).AddYears(10) `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
        
        Export-PfxCertificate -Cert $cert -FilePath $CertPath -Password $Password
        Export-Certificate -Cert $cert -FilePath $PublicPath
        
        Write-Host "Created $CertPath and $PublicPath" -ForegroundColor Green
    } finally {
        if ($null -ne $cert) {
            Get-Item $cert.PSPath -ErrorAction SilentlyContinue | Remove-Item -ErrorAction SilentlyContinue
            Write-Host "Removed temporary certificate from your Personal Store (tidy up)." -ForegroundColor Gray
        }
    }
} else {
    Write-Host "Certificate files already exist. Skipping generation." -ForegroundColor Yellow
}

Write-Host "Packaging prep complete." -ForegroundColor Cyan
