<#
.SYNOPSIS
  Regenerates app.ico, the icon used for the exe, the settings window and the tray.

.DESCRIPTION
  Every frame is drawn from the one layout below, so the 16 pixel icon in the notification area is
  the same picture as the 256 pixel one in Explorer.

  It did not used to be. The large frames were scaled down from icon-source.png, three landscape
  monitors with thin bezels, while the 16 pixel frame was a separate hand drawn glyph of three
  portrait monitors with heavy bezels. In the tray it read as a different app.

  Scaling the artwork down on its own does not work either. The three monitors sit side by side, so
  the art is a band three and a half times wider than it is tall, and at 16 pixels that leaves the
  monitors about four pixels high and unreadable. The layout here is therefore a little taller and
  chunkier than the artwork, and is drawn with integer rectangles at every size, so the small frames
  stay crisp instead of being interpolated into mush.

  icon-source.png stays in the repository as the reference the colours and proportions came from.

  Run it after changing anything here:
    pwsh src/BlackScreens/Assets/pack-icon.ps1
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$assets = $PSScriptRoot
$icoPath = Join-Path $assets 'app.ico'

# Sampled from icon-source.png so the redraw keeps the original palette.
$script:Bezel = [System.Drawing.Color]::FromArgb(255, 193, 194, 196)
$script:Stand = [System.Drawing.Color]::FromArgb(255, 152, 153, 155)
$script:Base  = [System.Drawing.Color]::FromArgb(255, 190, 190, 191)
$script:Dark  = [System.Drawing.Color]::FromArgb(255, 6, 6, 8)
$script:LitA  = [System.Drawing.Color]::FromArgb(255, 55, 216, 255)   # cyan, top left
$script:LitB  = [System.Drawing.Color]::FromArgb(255, 106, 108, 244)  # indigo, bottom right

# PowerShell rounds halves to even by default, which makes sizes like 40 fall a pixel short.
function Round-Away([double]$value) {
    return [int][Math]::Round($value, [MidpointRounding]::AwayFromZero)
}

function Draw-Monitor {
    param(
        [System.Drawing.Graphics]$G,
        [System.Drawing.Bitmap]$Bmp,
        [int]$X, [int]$Y, [int]$W, [int]$H,
        [int]$Bezel, [int]$StandH, [int]$BaseH,
        [switch]$Lit
    )

    $frame = [System.Drawing.SolidBrush]::new($script:Bezel)
    $G.FillRectangle($frame, $X, $Y, $W, $H)
    $frame.Dispose()

    $sx = $X + $Bezel
    $sy = $Y + $Bezel
    $sw = [Math]::Max(1, $W - (2 * $Bezel))
    $sh = [Math]::Max(1, $H - (2 * $Bezel))

    if ($Lit) {
        # Filled a pixel at a time rather than with a gradient brush, which dithers badly over an
        # area only a few pixels across.
        for ($py = $sy; $py -lt ($sy + $sh); $py++) {
            for ($px = $sx; $px -lt ($sx + $sw); $px++) {
                if ($sw -le 1) { $tx = 0.5 } else { $tx = ($px - $sx) / ($sw - 1.0) }
                if ($sh -le 1) { $ty = 0.5 } else { $ty = ($py - $sy) / ($sh - 1.0) }

                # Mostly left to right, the way the artwork runs, with a little vertical drift.
                $t = ($tx * 0.72) + ($ty * 0.28)
                $r = [int][Math]::Round($script:LitA.R + (($script:LitB.R - $script:LitA.R) * $t))
                $gc = [int][Math]::Round($script:LitA.G + (($script:LitB.G - $script:LitA.G) * $t))
                $b = [int][Math]::Round($script:LitA.B + (($script:LitB.B - $script:LitA.B) * $t))
                $Bmp.SetPixel($px, $py, [System.Drawing.Color]::FromArgb(255, $r, $gc, $b))
            }
        }
    }
    else {
        $off = [System.Drawing.SolidBrush]::new($script:Dark)
        $G.FillRectangle($off, $sx, $sy, $sw, $sh)
        $off.Dispose()
    }

    # Stand and base, both centred under the panel.
    $standW = [Math]::Max(1, (Round-Away ($W * 0.18)))
    $baseW = [Math]::Max(($standW + 1), (Round-Away ($W * 0.37)))

    $standBrush = [System.Drawing.SolidBrush]::new($script:Stand)
    $G.FillRectangle($standBrush, ($X + [int](($W - $standW) / 2)), ($Y + $H), $standW, $StandH)
    $standBrush.Dispose()

    $baseBrush = [System.Drawing.SolidBrush]::new($script:Base)
    $G.FillRectangle($baseBrush, ($X + [int](($W - $baseW) / 2)), ($Y + $H + $StandH), $baseW, $BaseH)
    $baseBrush.Dispose()
}

function New-Frame([int]$size) {
    # The layout is described at 64 pixels and scaled from there: two 16 wide side monitors, a 24
    # wide centre one, and a 4 wide gap either side of the centre. The panel heights give roughly
    # the same landscape proportions as the artwork, 1.14 and 1.33 against its 1.26 and 1.47, while
    # still rounding to something legible at 16 pixels.
    $scale = $size / 64.0

    $gap = [Math]::Max(1, (Round-Away (4 * $scale)))
    $sideW = [Math]::Max(3, (Round-Away (16 * $scale)))
    # Whatever is left, so the three monitors always fill the width exactly and stay symmetric.
    $centreW = $size - (2 * $sideW) - (2 * $gap)

    $centreH = [Math]::Max(4, (Round-Away (18 * $scale)))
    $sideH = [Math]::Max(3, (Round-Away (14 * $scale)))
    $standH = [Math]::Max(1, (Round-Away (4 * $scale)))
    $baseH = [Math]::Max(1, (Round-Away (3 * $scale)))

    # Panels sit on a common baseline, the way they do in the artwork, and the whole group is
    # centred in the square.
    $top = [int](($size - ($centreH + $standH + $baseH)) / 2)
    $baseline = $top + $centreH

    $bezel = [Math]::Max(1, (Round-Away ($sideW * 0.06)))

    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

    Draw-Monitor -G $g -Bmp $bmp -X 0 -Y ($baseline - $sideH) -W $sideW -H $sideH `
                 -Bezel $bezel -StandH $standH -BaseH $baseH
    Draw-Monitor -G $g -Bmp $bmp -X ($sideW + $gap) -Y $top -W $centreW -H $centreH `
                 -Bezel $bezel -StandH $standH -BaseH $baseH -Lit
    Draw-Monitor -G $g -Bmp $bmp -X ($size - $sideW) -Y ($baseline - $sideH) -W $sideW -H $sideH `
                 -Bezel $bezel -StandH $standH -BaseH $baseH

    $g.Dispose()
    return $bmp
}

function Get-IcoImageBytes([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width
    $h = $bmp.Height
    $xor = New-Object byte[] ($w * $h * 4)
    $andStride = [int][Math]::Floor(($w + 31) / 32) * 4
    $and = New-Object byte[] ($andStride * $h)
    $i = 0
    for ($y = $h - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $w; $x++) {
            $p = $bmp.GetPixel($x, $y)
            $xor[$i++] = $p.B
            $xor[$i++] = $p.G
            $xor[$i++] = $p.R
            $xor[$i++] = $p.A
        }
    }

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([int32]40)
    $bw.Write([int32]$w)
    $bw.Write([int32]($h * 2))
    $bw.Write([int16]1)
    $bw.Write([int16]32)
    $bw.Write([int32]0)
    $bw.Write([int32]($w * $h * 4))
    $bw.Write([int32]0)
    $bw.Write([int32]0)
    $bw.Write([int32]0)
    $bw.Write([int32]0)
    $bw.Flush()
    $ms.Write($xor, 0, $xor.Length)
    $ms.Write($and, 0, $and.Length)
    return $ms.ToArray()
}

function Write-Ico([string]$path, $images) {
    $sizes = @($images.Keys | Sort-Object { [int]$_ })
    $offset = 6 + (16 * $sizes.Count)
    $fs = [System.IO.File]::Create($path)
    try {
        $hdr = New-Object System.IO.MemoryStream
        $bw = New-Object System.IO.BinaryWriter $hdr
        $bw.Write([uint16]0)
        $bw.Write([uint16]1)
        $bw.Write([uint16]$sizes.Count)
        foreach ($size in $sizes) {
            $len = ([byte[]]$images[$size]).Length
            $stored = [int]$size
            if ($stored -ge 256) { $stored = 0 }
            $bw.Write([byte]$stored)
            $bw.Write([byte]$stored)
            $bw.Write([byte]0)
            $bw.Write([byte]0)
            $bw.Write([uint16]1)
            $bw.Write([uint16]32)
            $bw.Write([int32]$len)
            $bw.Write([int32]$offset)
            $offset += $len
        }
        $bw.Flush()
        $headerBytes = $hdr.ToArray()
        $fs.Write($headerBytes, 0, $headerBytes.Length)
        foreach ($size in $sizes) {
            $data = [byte[]]$images[$size]
            $fs.Write($data, 0, $data.Length)
        }
    }
    finally {
        $fs.Dispose()
    }
}

# 20, 24, 32 and 40 are the sizes the notification area asks for at 125, 150 and 200 per cent
# scaling. Without them Windows picks a neighbour and rescales it, which is part of why the tray
# icon looked soft.
$images = @{}
foreach ($size in 16, 20, 24, 32, 40, 48, 64, 128, 256) {
    $frame = New-Frame $size
    try { $images[$size] = Get-IcoImageBytes $frame } finally { $frame.Dispose() }
}

Write-Ico $icoPath $images
Write-Output "Wrote $icoPath ($((Get-Item $icoPath).Length) bytes, $($images.Count) sizes)"
