<#
.SYNOPSIS
  Blurs the Windows user name out of docs/assets/about.png.

.DESCRIPTION
  The About page prints the real settings and error log paths, which include the account name.
  This pixelates that part of both lines. The rectangle was measured from the About layout at the
  captured window size, so re-measure it if that page changes.

  Run it after scripts\capture-screenshots.ps1.
#>
[CmdletBinding()]
param(
    [int]$X = 418,
    [int]$Y = 582,
    [int]$Width = 86,
    [int]$Height = 40,
    [int]$Block = 7
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$path = Join-Path $root 'docs\assets\about.png'
if (-not (Test-Path $path)) { throw "No screenshot at $path." }

# Loaded through a memory stream so the file is not left locked, and without any redraw, because
# Graphics.DrawImage rescales by the image's DPI metadata and would resize the screenshot.
$bytes = [System.IO.File]::ReadAllBytes($path)
$stream = New-Object System.IO.MemoryStream (, $bytes)
$image = New-Object System.Drawing.Bitmap $stream

if (($X + $Width) -gt $image.Width -or ($Y + $Height) -gt $image.Height) {
    throw "The redaction box falls outside the $($image.Width)x$($image.Height) screenshot."
}

# Average each small block, which reads as a deliberate redaction and leaves nothing to sharpen.
for ($blockY = $Y; $blockY -lt ($Y + $Height); $blockY += $Block) {
    for ($blockX = $X; $blockX -lt ($X + $Width); $blockX += $Block) {
        $lastX = [Math]::Min($blockX + $Block, $X + $Width) - 1
        $lastY = [Math]::Min($blockY + $Block, $Y + $Height) - 1

        # Note: PowerShell variables are case insensitive, so these counters must not be called
        # $x and $y, which would overwrite the $X and $Y parameters mid loop.
        $red = 0; $green = 0; $blue = 0; $count = 0
        for ($py = $blockY; $py -le $lastY; $py++) {
            for ($px = $blockX; $px -le $lastX; $px++) {
                $pixel = $image.GetPixel($px, $py)
                $red += $pixel.R; $green += $pixel.G; $blue += $pixel.B; $count++
            }
        }
        if ($count -eq 0) { continue }

        $flat = [System.Drawing.Color]::FromArgb(255,
            [int]($red / $count), [int]($green / $count), [int]($blue / $count))
        for ($py = $blockY; $py -le $lastY; $py++) {
            for ($px = $blockX; $px -le $lastX; $px++) {
                $image.SetPixel($px, $py, $flat)
            }
        }
    }
}

$image.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$image.Dispose()
$stream.Dispose()

"redacted {0},{1} {2}x{3} in about.png" -f $X, $Y, $Width, $Height
