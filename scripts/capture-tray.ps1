<#
.SYNOPSIS
  Recaptures docs/assets/tray.png, the tray menu screenshot.

.DESCRIPTION
  Opens the tray menu with a real right click, then grabs the menu window itself with PrintWindow
  so the desktop behind it never appears in the image. Rounded corners are made transparent the
  same way as the settings window captures.

  BlackScreens has to be running. The mouse pointer moves during this, so leave it alone for a
  few seconds.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root 'docs\assets\tray.png'

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class Tray {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc f, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out int pid);
  [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public const uint RightDown = 0x0008, RightUp = 0x0010;
}
"@

[void][Tray]::SetProcessDpiAwarenessContext([IntPtr](-4))

$app = Get-Process BlackScreens -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $app) { throw 'BlackScreens is not running.' }
$appId = $app.Id

# A settings window on screen would add another candidate window later on.
$desktop = [System.Windows.Automation.AutomationElement]::RootElement
$settings = $desktop.FindFirst([System.Windows.Automation.TreeScope]::Children,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, 'BlackScreens')))
if ($settings) {
    $close = $settings.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty, 'Close')))
    if ($close) {
        $close.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 900
    }
}

function Find-TrayIcon {
    $desktop = [System.Windows.Automation.AutomationElement]::RootElement
    $buttons = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    foreach ($win in $desktop.FindAll([System.Windows.Automation.TreeScope]::Children,
                                      [System.Windows.Automation.Condition]::TrueCondition)) {
        foreach ($button in $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $buttons)) {
            # Only the notification area, never the taskbar button for the app's own window, which
            # is also a Button called BlackScreens and opens a jump list instead of the tray menu.
            if ($button.Current.Name -like '*BlackScreens*' -and
                $button.Current.ClassName -like 'SystemTray*') {
                return $button
            }
        }
    }
    return $null
}

$icon = Find-TrayIcon
if (-not $icon) {
    # The icon may be hidden in the overflow flyout.
    $desktop = [System.Windows.Automation.AutomationElement]::RootElement
    $shell = $desktop.FindFirst([System.Windows.Automation.TreeScope]::Children,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ClassNameProperty, 'Shell_TrayWnd')))
    $overflow = $shell.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty, 'Show Hidden Icons')))
    if ($overflow) {
        $overflow.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 1500
    }
    $icon = Find-TrayIcon
}
if (-not $icon) { throw 'Could not find the tray icon.' }

$bounds = $icon.Current.BoundingRectangle
Write-Host ("icon '{0}' at {1},{2} {3}x{4}" -f $icon.Current.Name, [int]$bounds.X, [int]$bounds.Y, [int]$bounds.Width, [int]$bounds.Height)
[void][Tray]::SetCursorPos([int]($bounds.X + $bounds.Width / 2), [int]($bounds.Y + $bounds.Height / 2))
Start-Sleep -Milliseconds 400
[Tray]::mouse_event([Tray]::RightDown, 0, 0, 0, [IntPtr]::Zero)
[Tray]::mouse_event([Tray]::RightUp, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 1500

# The menu is a top level window owned by the app, and the only narrow one. DWM frame bounds are
# not reliable for popups, so the plain window rect is used here.
$menu = [IntPtr]::Zero
$candidates = @()
$callback = [Tray+EnumProc] {
    param($handle, $lparam)
    $owner = 0
    [void][Tray]::GetWindowThreadProcessId($handle, [ref]$owner)
    if ($owner -eq $appId -and [Tray]::IsWindowVisible($handle)) {
        $name = New-Object System.Text.StringBuilder 256
        [void][Tray]::GetClassName($handle, $name, 256)
        if ($name.ToString() -like '*WindowsForms*') {
            $box = New-Object Tray+RECT
            [void][Tray]::GetWindowRect($handle, [ref]$box)
            $width = $box.R - $box.L
            $height = $box.B - $box.T
            $script:candidates += "{0} {1} {2}x{3}" -f $handle, $name.ToString().Substring(0, 22), $width, $height
            if ($width -gt 90 -and $width -lt 600 -and $height -gt 50 -and $height -lt 500) {
                $script:menu = $handle
            }
        }
    }
    return $true
}
[void][Tray]::EnumWindows($callback, [IntPtr]::Zero)
Write-Host ("app windows seen: {0}" -f $candidates.Count)
if ($menu -eq [IntPtr]::Zero) {
    $candidates | ForEach-Object { Write-Host "candidate: $_" }
    throw 'The tray menu did not open.'
}

$box = New-Object Tray+RECT
[void][Tray]::GetWindowRect($menu, [ref]$box)
$width = $box.R - $box.L
$height = $box.B - $box.T

$bitmap = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.Clear([System.Drawing.Color]::FromArgb(255, 255, 0, 255))
$dc = $graphics.GetHdc()
[void][Tray]::PrintWindow($menu, $dc, 2)
$graphics.ReleaseHdc($dc)
$graphics.Dispose()

$area = New-Object System.Drawing.Rectangle 0, 0, $width, $height
$data = $bitmap.LockBits($area, [System.Drawing.Imaging.ImageLockMode]::ReadWrite,
                         [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$pixels = New-Object byte[] ($data.Stride * $height)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
for ($i = 0; $i -lt $pixels.Length; $i += 4) {
    if ($pixels[$i] -eq 255 -and $pixels[$i + 1] -eq 0 -and $pixels[$i + 2] -eq 255) {
        $pixels[$i + 3] = 0
    } else {
        $pixels[$i + 3] = 255
    }
}
[System.Runtime.InteropServices.Marshal]::Copy($pixels, 0, $data.Scan0, $pixels.Length)
$bitmap.UnlockBits($data)

$bitmap.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()

[System.Windows.Forms.SendKeys]::SendWait('{ESC}')
'tray.png  {0} x {1}' -f $width, $height
