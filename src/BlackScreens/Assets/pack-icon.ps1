$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$assets = $PSScriptRoot
$srcPath = Join-Path $assets "icon-source.png"
$icoPath = Join-Path $assets "app.ico"

function Get-OpaqueBounds([System.Drawing.Bitmap]$bmp) {
    $minx = $bmp.Width; $miny = $bmp.Height; $maxx = -1; $maxy = -1
    for ($y = 0; $y -lt $bmp.Height; $y++) {
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            if ($bmp.GetPixel($x, $y).A -lt 16) { continue }
            if ($x -lt $minx) { $minx = $x }
            if ($y -lt $miny) { $miny = $y }
            if ($x -gt $maxx) { $maxx = $x }
            if ($y -gt $maxy) { $maxy = $y }
        }
    }
    if ($maxx -lt 0) {
        return [System.Drawing.Rectangle]::new(0, 0, $bmp.Width, $bmp.Height)
    }
    return [System.Drawing.Rectangle]::FromLTRB($minx, $miny, $maxx + 1, $maxy + 1)
}

function New-Cropped([System.Drawing.Bitmap]$source) {
    $bounds = Get-OpaqueBounds $source
    $pad = [Math]::Max(8, [int]([Math]::Max($bounds.Width, $bounds.Height) * 0.08))
    $x = [Math]::Max(0, $bounds.X - $pad)
    $y = [Math]::Max(0, $bounds.Y - $pad)
    $r = [Math]::Min($source.Width, $bounds.Right + $pad)
    $b = [Math]::Min($source.Height, $bounds.Bottom + $pad)
    $rect = [System.Drawing.Rectangle]::FromLTRB($x, $y, $r, $b)
    $cropped = New-Object System.Drawing.Bitmap $rect.Width, $rect.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($cropped)
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($source, (New-Object System.Drawing.Rectangle 0, 0, $rect.Width, $rect.Height), $rect, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    return $cropped
}

function New-Scaled([System.Drawing.Image]$source, [int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    $scale = [Math]::Min($size / [double]$source.Width, $size / [double]$source.Height)
    $w = [Math]::Max(1, [int]($source.Width * $scale))
    $h = [Math]::Max(1, [int]($source.Height * $scale))
    $x = [int](($size - $w) / 2)
    $y = [int](($size - $h) / 2)
    $g.DrawImage($source, $x, $y, $w, $h)
    $g.Dispose()
    return $bmp
}

function New-Tray16 {
    $bmp = New-Object System.Drawing.Bitmap 16, 16, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

    $frame = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 196, 202, 212))
    $off = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 32, 36, 44))
    $stand = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 160, 166, 176))

    function Draw-Monitor([int]$x, [int]$y, [int]$w, [int]$h, [System.Drawing.Brush]$screen) {
        $g.FillRectangle($frame, $x, $y, $w, $h)
        $g.FillRectangle($screen, $x + 1, $y + 1, $w - 2, $h - 2)
        $g.FillRectangle($stand, $x + [int]($w / 2) - 1, $y + $h, 2, 1)
    }

    Draw-Monitor 0 5 4 7 $off
    Draw-Monitor 12 5 4 7 $off

    $g.FillRectangle($frame, 5, 3, 6, 10)
    for ($row = 4; $row -le 11; $row++) {
        $t = ($row - 4) / 7.0
        $r = [int](50 + (150 * $t))
        $gc = [int](220 - (150 * $t))
        $b = [int](255 - (30 * $t))
        $brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, $r, $gc, $b))
        $g.FillRectangle($brush, 6, $row, 4, 1)
        $brush.Dispose()
    }
    $g.FillRectangle($stand, 7, 13, 2, 1)
    $bmp.SetPixel(8, 6, [System.Drawing.Color]::White)
    $bmp.SetPixel(7, 7, [System.Drawing.Color]::White)
    $bmp.SetPixel(8, 7, [System.Drawing.Color]::White)

    $frame.Dispose(); $off.Dispose(); $stand.Dispose(); $g.Dispose()
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
            $bw.Write([byte]($(if ($stored -ge 256) { 0 } else { $stored })))
            $bw.Write([byte]($(if ($stored -ge 256) { 0 } else { $stored })))
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

$source = [System.Drawing.Bitmap]::FromFile($srcPath)
$images = @{}
try {
    $cropped = New-Cropped $source
    try {
        $tray = New-Tray16
        try { $images[16] = Get-IcoImageBytes $tray } finally { $tray.Dispose() }

        foreach ($size in 24, 32, 48, 64, 128, 256) {
            $scaled = New-Scaled $cropped $size
            try { $images[$size] = Get-IcoImageBytes $scaled } finally { $scaled.Dispose() }
        }
    }
    finally {
        $cropped.Dispose()
    }

    Write-Ico $icoPath $images
    Write-Output "Wrote $icoPath ($((Get-Item $icoPath).Length) bytes)"
}
finally {
    $source.Dispose()
}
