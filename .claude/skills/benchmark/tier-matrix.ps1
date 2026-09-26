# #540: the quality ladder on the APU. One run per (target, tier); every [fps] and [build] line kept, then
#   python tier-matrix.py <OutDir>     # medians, and a flag on any run whose [fps] line is not what was asked
# A target is either "level:<name>" or "front:<scene>:<preview>", e.g.
#   .\tier-matrix.ps1 -Targets level:Caldera,front:sea:One -Seconds 45 -OutDir C:\Temp\matrix
param(
    [string[]]$Targets,
    [string[]]$Tiers = @('high', 'medium', 'low'),
    [int]$Seconds = 70,
    [string]$OutDir = "$env:TEMP\bs3d-tier-matrix",
    [string[]]$Extra = @(),
    [string]$Exe = "$PSScriptRoot\..\..\..\Game\bin\net10.0-windows\BS3D.exe"
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Add-Type @"
using System; using System.Runtime.InteropServices;
public class Bench540 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

foreach ($t in $Targets) {
    foreach ($tier in $Tiers) {
        Get-Process BS3D -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Sleep -Milliseconds 500
        $parts = $t.Split(':')
        $a = @('logfps', 'nocap', 'windowed', 'width=1600', 'height=900', 'mute', 'nofocuspause', 'nofps', 'sceneseed=0', "quality=$tier")
        if ($parts[0] -eq 'level') { $a += "level=$($parts[1])"; $tag = "L_$($parts[1])_$tier" }
        else { $a += "scene=$($parts[1])"; $a += "preview=$($parts[2])"; $tag = "F_$($parts[1])_$tier" }
        $a += $Extra
        $stamp = Get-Date -Format 'HHmmss'
        $log = Join-Path $OutDir "$tag-$stamp.log"
        $p = Start-Process $Exe -ArgumentList $a -PassThru -RedirectStandardOutput $log -RedirectStandardError "$log.err" -WorkingDirectory (Split-Path $Exe)
        # A level run has its window at ~5 s on the APU; the FRONT END does not (the first front-end run of
        # #540 had no window at 5 s and a null handle killed the whole sweep), so wait for it, up to 30 s.
        $waited = 0
        do { Start-Sleep -Seconds 1; $waited++; $p.Refresh(); $h = $p.MainWindowHandle }
        while (($h -eq $null -or $h -eq [IntPtr]::Zero) -and $waited -lt 30 -and -not $p.HasExited)
        if ($h -ne $null -and $h -ne [IntPtr]::Zero) {
            $r = New-Object Bench540+RECT
            [Bench540]::GetWindowRect($h, [ref]$r) | Out-Null
            [Bench540]::SetCursorPos($r.Left + 60, $r.Top + 12) | Out-Null; Start-Sleep -Milliseconds 200
            [Bench540]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero); [Bench540]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero)
            Start-Sleep -Milliseconds 300
            # Park the cursor off the title bar (trap 9: a second click there maximizes).
            [Bench540]::SetCursorPos($r.Left + 800, $r.Bottom + 40) | Out-Null
        }
        Start-Sleep -Seconds ([math]::Max(5, $Seconds - $waited))
        Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
        Write-Output "$(Get-Date -Format 'HH:mm:ss') done $tag -> $log"
    }
}
