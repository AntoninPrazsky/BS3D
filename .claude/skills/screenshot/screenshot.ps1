# Launch the BS3D Testbed from a chosen vantage, optionally press a few keys, and screenshot the window.
#
#   .\screenshot.ps1 -Out shot.png -GameArgs @('scene=mountain','campos=0,-18,24','camtarget=0,-8,0')
#   .\screenshot.ps1 -Out drained.png -GameArgs @('scene=mountain','Maps\Full.json') -Keys @('End') -Wait 10
#
# -GameArgs : arguments passed to Testbed.exe (a map path, scene=, sky=, campos=/camtarget=, ssaa=, exposure=, nocap...)
# -Keys     : key names sent to the window after launch, in order (see $KEYS below): view presets D1..D6, F5 (stop
#             sim), F10 (game mode), F12 (hide overlay), End (release all balls), Space (shoot). Sent via scan code
#             because SDL reads the scan code, not the virtual key.
# -Wait     : seconds to wait after launch before acting (let the scene settle / balls fall). Default 7.
# -Settle   : seconds to wait after the key presses before the capture. Default 1.5.
param(
    [Parameter(Mandatory=$true)][string]$Out,
    [string[]]$GameArgs = @(),
    [string[]]$Keys = @(),
    [int]$Wait = 7,
    [double]$Settle = 1.5,
    [string]$Exe = "C:\Users\panrd\source\repos\BS3D\Testbed\bin\net10.0-windows\Testbed.exe"
)

# key name -> (vk, scan, extended). SDL reads the scan code. Extended keys (End, arrows) need the extended flag.
# (Named $KeyMap, not $Keys: PowerShell variable names are case-insensitive, so $Keys would collide with the param.)
$KeyMap = @{
    'D1' = @(0x31,0x02,$false); 'D2' = @(0x32,0x03,$false); 'D3' = @(0x33,0x04,$false)
    'D4' = @(0x34,0x05,$false); 'D5' = @(0x35,0x06,$false); 'D6' = @(0x36,0x07,$false)  # D5 = up (top-down) view, D6 = down view
    'F5' = @(0x74,0x3F,$false)  # stop/start simulation
    'F10'= @(0x79,0x44,$false)  # switch game mode (low third-person camera)
    'F12'= @(0x7B,0x58,$false)  # hide/show the text overlay
    'End'= @(0x23,0x4F,$true)   # release all balls (extended key)
    'Space'=@(0x20,0x39,$false) # shoot a ball
}

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public class Shot {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$p = Start-Process $Exe -ArgumentList $GameArgs -PassThru
Start-Sleep -Seconds $Wait
$hwnd = $p.MainWindowHandle

# Focus: SetForegroundWindow alone often fails from a background process, so click the title bar (left side,
# clear of the map editor's right-docked panel), then confirm the window is really foreground.
$r = New-Object Shot+RECT
[Shot]::GetWindowRect($hwnd, [ref]$r) | Out-Null
[Shot]::SetCursorPos($r.Left + 60, $r.Top + 12); Start-Sleep -Milliseconds 200
[Shot]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero); [Shot]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 400
if ([Shot]::GetForegroundWindow() -ne $hwnd) { [Shot]::SetForegroundWindow($hwnd) | Out-Null; Start-Sleep -Milliseconds 300 }

foreach ($k in $Keys) {
    if (-not $KeyMap.ContainsKey($k)) { Write-Warning "unknown key '$k'"; continue }
    $vk, $scan, $ext = $KeyMap[$k]
    $down = 0x08; $up = 0x0A                          # KEYEVENTF_SCANCODE (+ KEYUP)
    if ($ext) { $down = $down -bor 0x01; $up = $up -bor 0x01 }  # + KEYEVENTF_EXTENDEDKEY
    [Shot]::keybd_event([byte]$vk, [byte]$scan, [uint32]$down, [IntPtr]::Zero); Start-Sleep -Milliseconds 80
    [Shot]::keybd_event([byte]$vk, [byte]$scan, [uint32]$up, [IntPtr]::Zero); Start-Sleep -Milliseconds 250
}

Start-Sleep -Seconds $Settle
[Shot]::GetWindowRect($hwnd, [ref]$r) | Out-Null
$w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
$bmp.Save($Out)
$g.Dispose(); $bmp.Dispose()
Write-Output "saved $Out ($w x $h)"
Stop-Process -Id $p.Id -Force
