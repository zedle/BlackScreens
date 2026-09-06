<#
.SYNOPSIS
  Signs a local build with a self signed certificate.

.DESCRIPTION
  For testing the signing path only. A self signed certificate is NOT trusted on anyone else's
  machine and does not remove the SmartScreen prompt. Real releases are signed by SignPath.
  For a real signature, the release workflow signs through SignPath.

.EXAMPLE
  pwsh scripts\sign-dev.ps1 -Path artifacts\BlackScreens-1.0.0-win-x64.exe
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [string]$Subject = 'CN=BlackScreens Development'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Path)) { throw "No such file: $Path" }

$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $Subject -and $_.NotAfter -gt (Get-Date) } |
    Select-Object -First 1

if (-not $certificate) {
    Write-Host "Creating a development certificate: $Subject"
    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Subject `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyUsage DigitalSignature `
        -NotAfter (Get-Date).AddYears(2)
}

Set-AuthenticodeSignature `
    -FilePath $Path `
    -Certificate $certificate `
    -HashAlgorithm SHA256 `
    -TimestampServer 'http://timestamp.digicert.com' | Format-List Status, StatusMessage, Path

Write-Host ''
Write-Host 'Get-AuthenticodeSignature will report UnknownError for this file: the certificate is not'
Write-Host 'in any trusted root store. That is expected. The point is to exercise the signing path.'
Write-Host 'Do not ship a build signed this way.'
