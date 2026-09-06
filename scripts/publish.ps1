<#
.SYNOPSIS
  Builds the BlackScreens release artifacts.

.DESCRIPTION
  Produces these under artifacts\, for whichever runtime is asked for:
    BlackScreens-<version>-win-x64.exe          self contained single file, no runtime install needed
    BlackScreens-<version>-win-x64-runtime.zip  small download, needs the .NET Desktop Runtime
    BlackScreens-<version>-setup.exe            NSIS installer, only when makensis is on the machine

  Run it once per architecture to build a whole release. The Arm64 installer is named
  "-setup-arm64.exe" rather than "-arm64-setup.exe" so that 1.0.0 and 1.0.1, which match the
  installer by its ending and take the first asset that fits, cannot offer it to an x64 machine.

  The release pipeline skips the installer here and builds it after the app exe has been signed, so
  the installer ships a signed payload.

.EXAMPLE
  pwsh scripts\publish.ps1
  pwsh scripts\publish.ps1 -Runtime win-arm64
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',
    [string]$Version,
    [switch]$NoInstaller
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\BlackScreens\BlackScreens.csproj'
$artifacts = Join-Path $root 'artifacts'
$work = Join-Path $artifacts 'work'

# The release workflow passes the version from the git tag, so a release is just "push a tag".
$version = $Version
if (-not $version) {
    $version = ([xml](Get-Content $project)).Project.PropertyGroup.Version |
        Where-Object { $_ } | Select-Object -First 1
}
if (-not $version) { throw 'Could not determine the version.' }
if ($version -notmatch '^\d+\.\d+\.\d+') { throw "Version '$version' is not major.minor.patch." }
# Four part file version, because Windows resources want major.minor.patch.build.
$fileVersion = "$(($version -split '-')[0]).0"
Write-Host "BlackScreens $version ($Configuration, $Runtime)"

if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

Write-Host 'Running tests...'
dotnet test (Join-Path $root 'BlackScreens.slnx') -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

$selfContained = Join-Path $work 'self-contained'
Write-Host 'Publishing self contained single file...'
dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:Version=$version `
    -p:FileVersion=$fileVersion `
    -p:AssemblyVersion=$fileVersion `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishReadyToRun=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $selfContained `
    --nologo
if ($LASTEXITCODE -ne 0) { throw 'Self contained publish failed.' }

$frameworkDependent = Join-Path $work 'framework-dependent'
Write-Host 'Publishing framework dependent...'
dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -p:Version=$version `
    -p:FileVersion=$fileVersion `
    -p:AssemblyVersion=$fileVersion `
    -p:PublishSingleFile=true `
    -o $frameworkDependent `
    --nologo
if ($LASTEXITCODE -ne 0) { throw 'Framework dependent publish failed.' }

$exeOut = Join-Path $artifacts "BlackScreens-$version-$Runtime.exe"
Copy-Item (Join-Path $selfContained 'BlackScreens.exe') $exeOut -Force

$zipOut = Join-Path $artifacts "BlackScreens-$version-$Runtime-runtime.zip"
if (Test-Path $zipOut) { Remove-Item $zipOut -Force }
Compress-Archive -Path (Join-Path $frameworkDependent '*') -DestinationPath $zipOut

if (-not $NoInstaller) {
    try {
        & (Join-Path $PSScriptRoot 'build-installer.ps1') -SourceExe $exeOut -Version $version -Runtime $Runtime
    }
    catch {
        Write-Warning "Installer skipped: $($_.Exception.Message)"
    }
}

Write-Host ''
Write-Host 'Artifacts:'
Get-ChildItem $artifacts -File | ForEach-Object {
    '{0,-52} {1,8:N1} MB' -f $_.Name, ($_.Length / 1MB)
}
Write-Host ''
Write-Host 'Unsigned. The release workflow is what signs a build for distribution.'
