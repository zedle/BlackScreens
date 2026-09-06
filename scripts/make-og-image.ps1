<#
.SYNOPSIS
  Renders docs/assets/og.png, the link preview card, from scripts/og-image.html.

.DESCRIPTION
  Uses headless Chrome so the card is built from the same CSS and palette as the site. Run this
  after changing og-image.html, then commit the PNG.
#>
[CmdletBinding()]
param(
    [int]$Width = 1200,
    [int]$Height = 630,
    [int]$Scale = 2
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'scripts\og-image.html'
$output = Join-Path $root 'docs\assets\og.png'

$chrome = (Get-Command chrome -ErrorAction SilentlyContinue).Source
if (-not $chrome) {
    foreach ($candidate in @(
        "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
        "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
        "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe",
        "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe")) {
        if (Test-Path $candidate) { $chrome = $candidate; break }
    }
}
if (-not $chrome) { throw 'Chrome or Edge is needed to render the card.' }

$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("og-" + [guid]::NewGuid().ToString('N') + '.png')

# Chrome reports progress on stderr, which PowerShell would otherwise turn into a terminating error.
$previous = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
& $chrome `
    --headless `
    --disable-gpu `
    --hide-scrollbars `
    --default-background-color=00000000 `
    --force-device-scale-factor=$Scale `
    "--window-size=$Width,$Height" `
    "--screenshot=$temp" `
    ("file:///" + $source.Replace('\', '/')) 2>$null | Out-Null

if (-not (Test-Path $temp)) { throw 'Chrome did not produce a screenshot.' }

# Chrome renders at the device scale factor, so bring it back to the exact card size.
Add-Type -AssemblyName System.Drawing
$full = [System.Drawing.Image]::FromFile($temp)
$card = New-Object System.Drawing.Bitmap $Width, $Height
$graphics = [System.Drawing.Graphics]::FromImage($card)
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.DrawImage($full, 0, 0, $Width, $Height)
$graphics.Dispose()
$full.Dispose()

New-Item -ItemType Directory -Force -Path (Split-Path $output) | Out-Null
$card.Save($output, [System.Drawing.Imaging.ImageFormat]::Png)
$card.Dispose()
Remove-Item $temp -Force

'{0}  {1} x {2}  {3:N0} KB' -f (Split-Path $output -Leaf), $Width, $Height, ((Get-Item $output).Length / 1KB)
