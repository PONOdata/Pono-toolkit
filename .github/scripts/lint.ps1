#!/usr/bin/env pwsh
# PSScriptAnalyzer over the packaging, signing, and setup scripts.
#
# Run locally exactly as CI does:
#   pwsh -NoProfile -File .github/scripts/lint.ps1
#
# The exclusion list is the same one pono-os and windows-pono run. Keeping it
# identical is deliberate: the command a developer runs by hand and the command
# the gate runs must report the same thing, or the gate teaches people to
# distrust it. What each exclusion actually suppresses in THIS repo:
#
#   PSAvoidUsingWriteHost          775 hits. These scripts are console
#                                  diagnostics and build steps; Write-Host is
#                                  their output, not a lapse.
#   PSUseBOMForUnicodeEncodedFile  10 hits, in setup scripts that carry a few
#                                  non-ASCII bytes each. A BOM buys nothing
#                                  here and breaks some parsers.
#   PSUseSingularNouns             2 hits. Write-IdentityImages writes a SET of
#                                  images; the singular would misdescribe it.
#   PSUseShouldProcessForStateChangingFunctions
#                                  3 hits, all private helpers inside scripts
#                                  rather than exported cmdlets. Adding the
#                                  attribute without wiring real ShouldProcess
#                                  calls would satisfy the rule and change
#                                  nothing, which is worse than not claiming
#                                  -WhatIf support at all.
#   PSAvoidUsingEmptyCatchBlock    0 hits today, carried for fleet parity.
#   PSReviewUnusedParameter        0 hits today, carried for fleet parity.
#
# Everything else stays on. This names style rules with reasons, it does not
# blanket-pass. The rules this repo was actually failing before the SecureString
# pass are all live and will fail the gate again if they come back:
# PSAvoidUsingConvertToSecureStringWithPlainText, PSAvoidUsingPlainTextForPassword,
# PSAvoidAssignmentToAutomaticVariable, PSAvoidUsingWMICmdlet, PSUseApprovedVerbs.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Module -ListAvailable -Name PSScriptAnalyzer)) {
    Set-PSRepository -Name PSGallery -InstallationPolicy Trusted
    Install-Module -Name PSScriptAnalyzer -Force -Scope CurrentUser -SkipPublisherCheck
}
Import-Module PSScriptAnalyzer

# Split-Path rather than Join-Path '..' '..': the three-argument Join-Path needs
# PowerShell 6+, and a '..\..' literal would not resolve on the Linux runner.
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

# @() matters: with exactly one finding, $issues is a scalar DiagnosticRecord and
# Set-StrictMode -Version Latest makes $issues.Count a "property cannot be found"
# error, so the gate would fail with a PowerShell error instead of naming the
# finding. Forcing an array keeps the report readable for the single-finding case,
# which is the common one.
$issues = @(Invoke-ScriptAnalyzer -Path $repoRoot -Recurse `
    -Severity Warning, Error `
    -ExcludeRule PSAvoidUsingWriteHost, PSUseBOMForUnicodeEncodedFile,
                 PSAvoidUsingEmptyCatchBlock, PSReviewUnusedParameter,
                 PSUseSingularNouns, PSUseShouldProcessForStateChangingFunctions)

if ($issues.Count -gt 0) {
    $issues | Format-Table -AutoSize RuleName, Severity, ScriptName, Line, Message
    throw "PSScriptAnalyzer found $($issues.Count) issue(s)"
}
Write-Host 'PSScriptAnalyzer: clean'
