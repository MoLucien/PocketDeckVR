param(
  [Parameter(Mandatory = $true)][ValidateSet('shot', 'click', 'focus')][string]$Mode,
  [string]$Out,
  [int]$X = 0,
  [int]$Y = 0
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not ('Win32Ui' -as [type])) {
  Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32Ui {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
}
"@
}

function Get-AppWindow {
  $p = Get-Process PocketDeck -ErrorAction SilentlyContinue |
       Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } | Select-Object -First 1
  if (-not $p) { throw 'app window not found' }
  return $p.MainWindowHandle
}

# 把应用窗口提到前台。Windows 会阻止后台进程抢前台，所以先用 ALT 键事件解除限制，
# 再 SetForegroundWindow；最后断言前台窗口确实是它，否则一律中止（绝不盲点）。
function Focus-App([IntPtr]$h) {
  [void][Win32Ui]::ShowWindow($h, 9)   # SW_RESTORE
  [Win32Ui]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
  [Win32Ui]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
  [void][Win32Ui]::BringWindowToTop($h)
  [void][Win32Ui]::SetForegroundWindow($h)
  [void][Win32Ui]::SetWindowPos($h, [IntPtr]::new(-1), 0, 0, 0, 0, 0x0003)  # HWND_TOPMOST, NOSIZE|NOMOVE
  [void][Win32Ui]::SetWindowPos($h, [IntPtr]::new(-2), 0, 0, 0, 0, 0x0003)  # HWND_NOTOPMOST
  Start-Sleep -Milliseconds 350
  $fg = [Win32Ui]::GetForegroundWindow()
  if ($fg -ne $h) { throw ("cannot bring app to foreground (fg=0x{0:X} app=0x{1:X}); aborting to avoid a blind click" -f [int64]$fg, [int64]$h) }
}

$h = Get-AppWindow
Focus-App $h

$cr = New-Object Win32Ui+RECT
[void][Win32Ui]::GetClientRect($h, [ref]$cr)
$clientW = $cr.Right - $cr.Left
$clientH = $cr.Bottom - $cr.Top
$scale = $clientW / 920.0

switch ($Mode) {
  'focus' { "focused: 0x$('{0:X}' -f [int64]$h) client=${clientW}x${clientH} scale=$([math]::Round($scale,3))" }
  'shot' {
    $r = New-Object Win32Ui+RECT
    [void][Win32Ui]::GetWindowRect($h, [ref]$r)
    $w = $r.Right - $r.Left; $hh = $r.Bottom - $r.Top
    $bmp = New-Object System.Drawing.Bitmap($w, $hh)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $hh)))
    $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    "shot: $Out window=${w}x${hh} scale=$([math]::Round($scale,3))"
  }
  'click' {
    $pt = New-Object Win32Ui+POINT
    $pt.X = [int]($X * $scale); $pt.Y = [int]($Y * $scale)
    [void][Win32Ui]::ClientToScreen($h, [ref]$pt)
    [void][Win32Ui]::SetCursorPos($pt.X, $pt.Y)
    Start-Sleep -Milliseconds 200
    $fg = [Win32Ui]::GetForegroundWindow()
    if ($fg -ne $h) { throw 'app lost foreground before click; aborted' }
    [Win32Ui]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 90
    [Win32Ui]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    "click: logical=($X,$Y) screen=($($pt.X),$($pt.Y)) scale=$([math]::Round($scale,3))"
  }
}
