# Measure BS3D's frame rate under pinned, repeatable conditions and print a comparable table.
#
#   .\benchmark.ps1                                     # the 7-scene sweep at ssaa 1 and 2
#   .\benchmark.ps1 -Scenes neon -Ssaa 2 -Extra @('clouds=0')
#   .\benchmark.ps1 -Seconds 20                         # longer window for a noisy machine
#
# Reads the game's own `[fps]` lines (the `logfps` argument), so it measures what the game measures rather
# than screenshotting a counter. Every run pins the scene and the dome, because the game otherwise picks one
# of the seven scenes at random per launch and two of them bring a dome of their own.
param(
    [string[]]$Scenes  = @('city', 'sea', 'savanna', 'desert', 'mountain', 'meadow', 'neon'),
    [int[]]$Ssaa       = @(1, 2),
    [string[]]$Extra   = @(),      # further game arguments applied to every run (e.g. 'clouds=0')
    [int]$Sky          = 13,
    [int]$Seconds      = 14,       # wall time per run; the first WarmupLines readings are discarded
    [int]$WarmupLines  = 4,
    [string]$Exe       = "C:\GitHub\Game\bin\net10.0-windows\BS3D.exe",
    [string]$OutDir    = "$env:TEMP\bs3d-benchmark",
    [switch]$NoFocus               # skip the focus click (only valid if the frame rate stays well under 50)
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Add-Type @"
using System; using System.Runtime.InteropServices;
public class Bench {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

function Get-Hardware {
    $cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1).Name.Trim()
    $gpu = (Get-CimInstance Win32_VideoController | Select-Object -First 1).Name.Trim()
    $ram = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB)
    "$cpu | $gpu | ${ram} GB"
}

# One run: launch pinned, focus, let it settle, read the game's own FPS lines back.
function Measure-Run([string]$scene, [int]$ssaa, [string[]]$extra) {
    $tag = ($scene + '_ssaa' + $ssaa + ($extra -join '_')) -replace '[^\w]', ''
    $log = Join-Path $OutDir "$tag.log"
    # Ssaa 0 means "do not pass ssaa= at all", which is how a quality= tier is measured with the factor it
    # actually chose rather than one forced over the top of it.
    $args = @('logfps', 'nocap', "scene=$scene", "sky=$Sky") + $extra
    if ($ssaa -gt 0) { $args += "ssaa=$ssaa" }

    $p = Start-Process $Exe -ArgumentList $args -PassThru -RedirectStandardOutput $log -RedirectStandardError "$log.err"

    # Focus matters: MonoGame's InactiveSleepTime caps an unfocused window near 50 FPS, which silently becomes
    # the measurement instead of the frame cost. SetForegroundWindow from a background process usually fails,
    # so click the title bar as the other harnesses in this repo do.
    Start-Sleep -Seconds 4
    if (-not $NoFocus) {
        $p.Refresh()
        $h = $p.MainWindowHandle
        if ($h -ne [IntPtr]::Zero) {
            $r = New-Object Bench+RECT
            [Bench]::GetWindowRect($h, [ref]$r) | Out-Null
            [Bench]::SetCursorPos($r.Left + 60, $r.Top + 12) | Out-Null; Start-Sleep -Milliseconds 200
            [Bench]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero); [Bench]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero)
            Start-Sleep -Milliseconds 400
            if ([Bench]::GetForegroundWindow() -ne $h) { [Bench]::SetForegroundWindow($h) | Out-Null }
        }
    }

    Start-Sleep -Seconds $Seconds
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 400

    $text = Get-Content $log -Raw -ErrorAction SilentlyContinue
    if (-not $text) { Write-Warning "no output from $tag - see $log.err"; return $null }
    # The game writes the decimal separator in the machine's locale, so accept a comma as well as a dot.
    $all = ([regex]'\[fps\] ([\d.,]+)').Matches($text) | ForEach-Object {
        [double]($_.Groups[1].Value -replace ',', '.')
    }

    # The opening seconds are shader compiles and the first touch of each render target - the same reason the
    # game's own adaptive probe waits before it believes a number.
    $kept = @($all | Select-Object -Skip $WarmupLines)
    if ($kept.Count -eq 0) { return $null }

    $mean = [math]::Round(($kept | Measure-Object -Average).Average, 1)
    [pscustomobject]@{
        Scene = $scene; Ssaa = $ssaa; Extra = ($extra -join ' ')
        FPS = $mean
        MsPerFrame = [math]::Round(1000.0 / $mean, 1)
        Samples = $kept.Count
        Min = [math]::Round(($kept | Measure-Object -Minimum).Minimum, 1)
        Max = [math]::Round(($kept | Measure-Object -Maximum).Maximum, 1)
    }
}

Write-Output "BS3D benchmark - $(Get-Hardware)"
Write-Output "windowed 1600x900, vsync off, dome $Sky, front end (no level loaded), ${Seconds}s per run, first $WarmupLines readings discarded"
if ($Extra.Count -gt 0) { Write-Output "extra arguments: $($Extra -join ' ')" }
Write-Output ""

$results = foreach ($s in $Scenes) { foreach ($n in $Ssaa) { Measure-Run $s $n $Extra } }
$results | Where-Object { $_ } | Format-Table Scene, Ssaa, FPS, MsPerFrame, Min, Max, Samples -AutoSize
