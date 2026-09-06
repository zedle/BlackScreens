<#
.SYNOPSIS
  Builds the BlackScreens NSIS installer from an already published exe.

.DESCRIPTION
  Run scripts\publish.ps1 first. In the release pipeline this runs after the app exe has been signed,
  so the installer ships a signed payload and then gets signed itself.

.EXAMPLE
  pwsh scripts\build-installer.ps1
  pwsh scripts\build-installer.ps1 -SourceExe artifacts-signed\BlackScreens-1.0.0-win-x64.exe
#>
[CmdletBinding()]
param(
    [string]$SourceExe,
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$script = Join-Path $root 'installer\BlackScreens.nsi'

if (-not $Version) {
    $project = Join-Path $root 'src\BlackScreens\BlackScreens.csproj'
    $Version = ([xml](Get-Content $project)).Project.PropertyGroup.Version |
        Where-Object { $_ } | Select-Object -First 1
}
if (-not $Version) { throw 'Could not determine the version.' }

if (-not $SourceExe) {
    $SourceExe = Join-Path $artifacts "BlackScreens-$Version-win-x64.exe"
}
if (-not (Test-Path $SourceExe)) {
    throw "Published exe not found: $SourceExe. Run scripts\publish.ps1 first."
}

$makensis = (Get-Command makensis -ErrorAction SilentlyContinue).Source
if (-not $makensis) {
    foreach ($candidate in @(
        "${env:ProgramFiles(x86)}\NSIS\makensis.exe",
        "$env:ProgramFiles\NSIS\makensis.exe")) {
        if ($candidate -and (Test-Path $candidate)) { $makensis = $candidate; break }
    }
}
if (-not $makensis) {
    throw 'makensis was not found. Install NSIS from https://nsis.sourceforge.io or "winget install NSIS.NSIS".'
}

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$outFile = Join-Path $artifacts "BlackScreens-$Version-setup.exe"

Write-Host "Building installer $outFile"
& $makensis `
    "/DVERSION=$Version" `
    "/DSOURCE_EXE=$((Resolve-Path $SourceExe).Path)" `
    "/DOUTFILE=$outFile" `
    $script
if ($LASTEXITCODE -ne 0) { throw "makensis failed with exit code $LASTEXITCODE." }

'{0,-52} {1,8:N1} MB' -f (Split-Path $outFile -Leaf), ((Get-Item $outFile).Length / 1MB)
