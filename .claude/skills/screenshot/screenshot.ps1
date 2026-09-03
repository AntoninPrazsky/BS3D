# Launch the BS3D Testbed from a chosen vantage, optionally press or HOLD a few keys, and screenshot the window.
#
#   .\screenshot.ps1 -Out shot.png -GameArgs @('scene=mountain','campos=0,-18,24','camtarget=0,-8,0')
#   .\screenshot.ps1 -Out drained.png -GameArgs @('scene=mountain','Maps\Full.json') -Keys @('End') -Wait 10
#   .\screenshot.ps1 -Out walking.png -Keys @('F10','F12') -Hold @('A') -HoldSeconds 2
#
# -GameArgs : arguments passed to Testbed.exe (a map path, scene=, sky=, campos=/camtarget=, ssaa=, exposure=, nocap, width=/height=...)
# -Keys     : key names TAPPED after launch, in order (see $KeyMap below): view presets D1..D6, F5 (stop
#             sim), F10 (game mode), F12 (hide overlay), End (release all balls), Space (shoot). Sent via scan code
#             because SDL reads the scan code, not the virtual key.
# -Hold     : key names HELD DOWN as a set - all the downs first, so W and A together really are together -
#             for -HoldSeconds, and STILL DOWN when the shot is taken. That is the point: the walk keys
#             (W/S advance, A/D orbit) only move the gun while they are held, so a tap photographs a gun
#             that has already stopped. Released in a finally, so a failed capture cannot leave a key down
#             for the whole desktop. See "Holding a key" in SKILL.md for what it cannot do.
# -HoldSeconds : how long -Hold keys stay down before the capture. Default 1.5.
# -Wait     : seconds to wait after launch before acting (let the scene settle / balls fall). Default 7.
# -Settle   : seconds to wait after the tapped keys, before the hold begins. Default 1.5.
param(
    [Parameter(Mandatory=$true)][string]$Out,
    [string[]]$GameArgs = @(),
    [string[]]$Keys = @(),
    [string[]]$Hold = @(),
    [double]$HoldSeconds = 1.5,
    [int]$Wait = 7,
    [double]$Settle = 1.5,
    [string]$Exe = "C:\GitHub\Testbed\bin\net10.0-windows\Testbed.exe"
)

# key name -> (vk, scan, extended). SDL reads the scan code. Extended keys (End, arrows) need the extended flag.
# (Named $KeyMap, not $Keys: PowerShell variable names are case-insensitive, so $Keys would collide with the param.)
$KeyMap = @{
    'D1' = @(0x31,0x02,$false); 'D2' = @(0x32,0x03,$false); 'D3' = @(0x33,0x04,$false)
    'D4' = @(0x34,0x05,$false); 'D5' = @(0x35,0x06,$false); 'D6' = @(0x36,0x07,$false)  # D5 = up (top-down) view, D6 = down view
    'F5' = @(0x74,0x3F,$false)  # stop/start simulation
    'F10'= @(0x79,0x44,$false)  # switch game mode (low third-person camera)
    'F11'= @(0x7A,0x57,$false)  # fullscreen, in all three executables
    'F12'= @(0x7B,0x58,$false)  # hide/show the text overlay
    'End'= @(0x23,0x4F,$true)   # release all balls (extended key)
    'L'  = @(0x4C,0x26,$false)  # cycle the ball material (#318); "balls=<name>" on the command line pins one
    'Space'=@(0x20,0x39,$false) # shoot a ball
    # The walk. These are only ever useful with -Hold: tapped, they move the gun by one frame's step, which
    # is nothing. W/S walk the carriage towards the field and back, A/D orbit it around the field.
    'W' = @(0x57,0x11,$false); 'S' = @(0x53,0x1F,$false)
    'A' = @(0x41,0x1E,$false); 'D' = @(0x44,0x20,$false)
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
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

# One key event. KEYEVENTF_SCANCODE is 0x08, KEYUP adds 0x02, EXTENDEDKEY adds 0x01.
function Send-Key([string]$name, [bool]$release) {
    $vk, $scan, $ext = $KeyMap[$name]
    $flags = 0x08
    if ($release) { $flags = $flags -bor 0x02 }
    if ($ext) { $flags = $flags -bor 0x01 }
    [Shot]::keybd_event([byte]$vk, [byte]$scan, [uint32]$flags, [IntPtr]::Zero)
}

$p = Start-Process $Exe -ArgumentList $GameArgs -PassThru
Start-Sleep -Seconds $Wait
$hwnd = $p.MainWindowHandle

# Focus: SetForegroundWindow alone often fails from a background process, so click the title bar (left side,
# clear of the map editor's right-docked panel), then confirm the window is really foreground.
$r = New-Object Shot+RECT
[Shot]::GetWindowRect($hwnd, [ref]$r) | Out-Null
[Shot]::SetCursorPos($r.Left + 60, $r.Top + 12) | Out-Null; Start-Sleep -Milliseconds 200
[Shot]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero); [Shot]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 400
if ([Shot]::GetForegroundWindow() -ne $hwnd) { [Shot]::SetForegroundWindow($hwnd) | Out-Null; Start-Sleep -Milliseconds 300 }

# Park the cursor off the title bar. A second click there is what MAXIMIZES a window, and the benchmark
# skill's own notes record runs that came back at the panel's size rather than the one asked for.
[Shot]::SetCursorPos($r.Left + 60, $r.Top + 200) | Out-Null

foreach ($k in $Keys) {
    if (-not $KeyMap.ContainsKey($k)) { Write-Warning "unknown key '$k'"; continue }
    Send-Key $k $false; Start-Sleep -Milliseconds 80
    Send-Key $k $true;  Start-Sleep -Milliseconds 250
}

Start-Sleep -Seconds $Settle

# The hold. Every down goes out before any sleep, so a set held together (W and A, which is what makes an
# omnidirectional wheel decompose its motion) really is simultaneous rather than staggered. The capture
# happens with the keys STILL DOWN.
$held = @()
$bmp = $null

try {
    foreach ($k in $Hold) {
        if (-not $KeyMap.ContainsKey($k)) { Write-Warning "unknown key '$k'"; continue }
        Send-Key $k $false
        $held += $k
    }

    if ($held.Count -gt 0) { Start-Sleep -Seconds $HoldSeconds }

    [Shot]::GetWindowRect($hwnd, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
    }
    catch {
        # CopyFromScreen throws an invalid-handle Win32Exception when the desktop is LOCKED (secure desktop).
        # PrintWindow with PW_RENDERFULLCONTENT (2) grabs the DWM-composed surface instead, D3D content
        # included, and works while locked - the game keeps rendering, it just is not on screen. Note the
        # -Keys presses, the -Hold downs and the title-bar focus click above never reach the app while
        # locked, so only command-line-driven shots are trustworthy then - and a HELD key that never
        # arrived photographs a gun that simply did not move, which reads as a finding rather than as a
        # failed run. Check the desktop is unlocked before believing a -Hold shot (Get-Process LogonUI).
        Write-Warning "CopyFromScreen failed (desktop locked?) - falling back to PrintWindow"
        $hdc = $g.GetHdc()
        [Shot]::PrintWindow($hwnd, $hdc, 2) | Out-Null
        $g.ReleaseHdc($hdc)
    }
    $g.Dispose()
}
finally {
    # Unconditionally, and before anything else can fail: a key left down because this script threw is a key
    # left down for the whole desktop, which is the one genuinely dangerous failure here.
    foreach ($k in $held) { Send-Key $k $true }
}

$bmp.Save($Out)
$bmp.Dispose()
Write-Output "saved $Out ($w x $h)"
Stop-Process -Id $p.Id -Force
