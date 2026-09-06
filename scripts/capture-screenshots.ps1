<#
.SYNOPSIS
  Recaptures the settings window screenshots used on the website.

.DESCRIPTION
  Grabs the window itself with PrintWindow rather than copying a rectangle off the screen, so
  nothing behind the window ends up in the image. The bitmap is pre-filled with a colour the UI
  never uses, and anything still that colour afterwards, which is the area outside the rounded
  corners, is turned transparent.

  Run it with BlackScreens not already running. It starts the app, walks the settings pages, and
  writes docs/assets/*.png. Follow it with redact-about.ps1, which blurs the user name out of the
  paths shown on the About page.
#>
[CmdletBinding()]
param(
    [string]$Exe
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'docs\assets'

if (-not $Exe) {
    $Exe = Join-Path $root 'src\BlackScreens\bin\Debug\net10.0-windows\BlackScreens.exe'
}
if (-not (Test-Path $Exe)) { throw "No build at $Exe. Run dotnet build first." }

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Cap {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public const int ExtendedFrameBounds = 9;
  public const uint RenderFullContent = 2;
}
"@

[void][Cap]::SetProcessDpiAwarenessContext([IntPtr](-4))

function Get-WindowImage {
    param([IntPtr]$Handle)

    # PrintWindow draws from the window origin, which sits outside the visible frame because of the
    # invisible resize border. Capture the whole window, then trim to what is actually on screen,
    # otherwise the content is pushed right and a dark band appears down the left.
    $full = New-Object Cap+RECT
    if (-not [Cap]::GetWindowRect($Handle, [ref]$full)) { throw 'Could not measure the window.' }

    $visible = New-Object Cap+RECT
    if ([Cap]::DwmGetWindowAttribute($Handle, [Cap]::ExtendedFrameBounds, [ref]$visible, 16) -ne 0) {
        $visible = $full
    }

    $fullWidth = $full.R - $full.L
    $fullHeight = $full.B - $full.T
    $offsetX = $visible.L - $full.L
    $offsetY = $visible.T - $full.T
    $width = $visible.R - $visible.L
    $height = $visible.B - $visible.T

    $bitmap = New-Object System.Drawing.Bitmap $fullWidth, $fullHeight, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    # A colour the interface never uses, so leftovers mark the pixels the window did not paint.
    $key = [System.Drawing.Color]::FromArgb(255, 255, 0, 255)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear($key)
    $dc = $graphics.GetHdc()
    $ok = [Cap]::PrintWindow($Handle, $dc, [Cap]::RenderFullContent)
    $graphics.ReleaseHdc($dc)
    $graphics.Dispose()
    if (-not $ok) { throw 'PrintWindow failed.' }

    # Trim the invisible border away.
    $trimmed = $bitmap.Clone(
        (New-Object System.Drawing.Rectangle $offsetX, $offsetY, $width, $height),
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bitmap.Dispose()
    $bitmap = $trimmed

    # Rounded corners leave the fill colour behind. Make those pixels transparent.
    $data = $bitmap.LockBits(
        (New-Object System.Drawing.Rectangle 0, 0, $width, $height),
        [System.Drawing.Imaging.ImageLockMode]::ReadWrite,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bytes = New-Object byte[] ($data.Stride * $height)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    for ($i = 0; $i -lt $bytes.Length; $i += 4) {
        # BGRA order
        if ($bytes[$i] -eq 255 -and $bytes[$i + 1] -eq 0 -and $bytes[$i + 2] -eq 255) {
            $bytes[$i + 3] = 0
        } else {
            $bytes[$i + 3] = 255
        }
    }
    [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
    $bitmap.UnlockBits($data)

    return $bitmap
}

function Find-Window {
    param([string]$Name = 'BlackScreens')
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
}

Get-Process BlackScreens -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

Start-Process $Exe
Start-Sleep -Seconds 4
Start-Process $Exe      # a second launch asks the running copy to open settings
Start-Sleep -Seconds 5

$window = Find-Window
if (-not $window) { throw 'The settings window did not open.' }
$handle = [IntPtr]$window.Current.NativeWindowHandle

foreach ($page in @('General', 'Detection', 'Blackout', 'Monitors', 'Denylist', 'About')) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $page)
    $item = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 900

    $image = Get-WindowImage -Handle $handle
    $path = Join-Path $out ($page.ToLower() + '.png')
    $image.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    '{0,-12} {1} x {2}' -f $page, $image.Width, $image.Height
    $image.Dispose()
}
