<#
.SYNOPSIS
    Films the Game for the website (#946): a looping clip (WebM and MP4) and its poster, or a still, out of the Game's capture mode.

.DESCRIPTION
    The page at bs3d.winphonew.eu shows the game as the newest release plays it, and a clip says what a still cannot: a storey
    of balls falling, the gun firing, a Cut striking, a scene's detail in motion. This runs the Game once in capture mode
    (docs/game-session/controls-and-testing.md, "Capture"), then encodes the numbered frames it wrote.

    Capture mode steps a FIXED amount of game time a rendered frame (-Fps, 60 by default), so frame n is the world at n/Fps
    seconds however long a frame took to render and write, and the same arguments make the same clip. It draws no HUD and has no
    FPS line, takes no input, and films a stated camera (-CamPos, -CamTarget, -Fov, or a keyframed -CamPath) if there is one.
    The window is 16:9 and not fullscreen (the HUD is laid out to the window's edges; docs/website.md says what a cut-down
    fullscreen cost the gallery), and the run uses a scratch profile (userdata=) of its own, so it never touches your save and
    never reaches the online boards. It is started without taking the keyboard focus: type as you like while it runs.

    Builds the Game in Release into a folder of its own, as web-screenshots.ps1 does, so the everyday Game\bin is left alone.
    The encoder is Tools\web-capture-encode.py, run with a Python that has PyAV (its bundled FFmpeg): no ffmpeg is installed or
    needed. The clip is H.264 (MP4, +faststart) and VP9 (WebM) in yuv420p, BT.709, with no audio - the page's own sound is its
    own - and the poster is a JPEG of one frame. Put the clip in a page as
    <video autoplay muted loop playsinline poster="..."><source src="x.webm" type="video/webm"><source src="x.mp4" type="video/mp4"></video>.
    Nothing loops by itself in the file: for a clip that does not jump at the seam, choose a window where the lens and the cluster
    end where they began (a closed path, a swing and its return).

    With -Seconds 0 it takes ONE frame and writes it as <Name>-1600.jpg and <Name>-800.jpg, the gallery's pair (web-screenshots.ps1),
    for a still that needs a stated camera or a moment the gallery's plain run cannot frame.

.PARAMETER Name
    What the files are called: <Name>.webm, <Name>.mp4, <Name>-poster.jpg in -OutDir.

.PARAMETER Level
    The level to film, by name or its 1-based place in the set; it brings its own scene. A chapter's first level flies its opening
    tour first (the HUD is off, and a stated camera replaces it, but it takes game time): start -From after it, or film another level.

.PARAMETER From
    Seconds of game time to start at. The clock starts when the game does; a level is built on the first frame, and its opening
    (and any tour) takes a few seconds of it. Frames before -From are drawn and not written, so the world is at its place.

.PARAMETER Seconds
    How much game time to write. 0 is a single frame, a still.

.PARAMETER Fps
    Frames of game time a second, 15 to 240. 60 is smooth; 30 halves the frames and the size. The video is encoded at this rate.

.PARAMETER Width
.PARAMETER Height
    The window's size, which is the frame's. By default the largest of 1920x1080, 1600x900 and 1280x720 the display's working area holds.

.PARAMETER VideoWidth
    Scale the clip down to this width when it is encoded (1280 for a page's hero), keeping the aspect. The frames, and the poster, stay the
    window's size. Film grain and fine detail are what a video spends its bits on: a 1080p60 clip is several megabits a second.

.PARAMETER CamPos
.PARAMETER CamTarget
.PARAMETER Fov
    A held camera, in world coordinates (the Testbed's spelling: -CamPos 0,-4,30 -CamTarget 0,-8,0 -Fov 40, the field of view in degrees).
    A capture run prints the play camera's own pose, the gun's and the cluster's centre on its [capture] lines to start from.

.PARAMETER CamPath
    A keyframed camera, a JSON file (docs/website.md, "Filming the game"): times in seconds into the clip, positions, targets, a field
    of view and an ease, eased between keys or one smooth spline through them.

.PARAMETER Demo
    The benchmark's action: the gun fires by itself, picking balls whose group will fall, with two cuts, and its lens circles the gun
    (unless a camera is stated). Takes about 24 seconds of game time after the level's opening.

.PARAMETER Shake
    Keep the gun's kick of the camera under a stated camera, which is otherwise held perfectly still.

.PARAMETER Aim
    Show the aim beam and the ghost ball, which a capture hides (nobody holds the aim of a clip).

.PARAMETER GameArgs
    More arguments for the Game, as one string: the scripted ones (fire=, aim=, sweep=, cut=, swap=, brake=, rmb=, wind=, powerups=, seed=, sceneseed=...)
    on the capture's clock, e.g. 'seed=7 aim=3:35:0 fire=4,5.2 cut=8 fire=9'.

.PARAMETER OutDir
    Where the clip goes. Default Web\src\video under the repository, which nothing publishes until a page uses it.

.PARAMETER NoBuild
    Use the Game a previous run built in %TEMP%\bs3d-web-capture.

.PARAMETER NoEncode
    Stop after the frames: leave them in %TEMP%\bs3d-web-capture-frames\<Name> and write nothing else.

.PARAMETER KeepFrames
    Keep the frames after encoding (they are 1-4 MB each at 1080p).

.PARAMETER PosterFrame
    The 1-based frame the poster is made of.

.PARAMETER Python
    A Python with PyAV and Pillow. Default the one in the ComfyUI venv on the desktop.

.EXAMPLE
    .\Tools\web-capture.ps1 -Name reel-fall -Level Reel -From 6 -Seconds 4 -GameArgs 'aim=3:32:0 fire=4.5'
    .\Tools\web-capture.ps1 -Name demo -Level Vortex -Demo -From 10 -Seconds 20 -Fps 30
    .\Tools\web-capture.ps1 -Name island-still -Level Meander -From 9 -Seconds 0 -CamPos 30,-6,40 -CamTarget 0,-5,0 -Fov 45
#>
param(
    [Parameter(Mandatory)][string]$Name,
    [string]$Level = 'Meander',
    [double]$From = 8,
    [double]$Seconds = 5,
    [ValidateRange(15, 240)][int]$Fps = 60,
    [int]$Width = 0,
    [int]$Height = 0,
    [int]$VideoWidth = 0,
    [object]$CamPos = $null,
    [object]$CamTarget = $null,
    [double]$Fov = 0,
    [string]$CamPath = '',
    [switch]$Demo,
    [switch]$Shake,
    [switch]$Aim,
    [string]$GameArgs = '',
    [string]$OutDir = '',
    [switch]$NoBuild,
    [switch]$NoEncode,
    [switch]$KeepFrames,
    [int]$PosterFrame = 1,
    [int]$Timeout = 1800,
    [string]$Python = 'C:\Users\panrd\AI\ComfyUI\venv\Scripts\python.exe'
)

$ErrorActionPreference = 'Stop'

. "$PSScriptRoot\PathGuard.ps1"
Assert-SanePath -Name OutDir -Value $OutDir
Assert-SanePath -Name CamPath -Value $CamPath
Assert-SanePath -Name Python -Value $Python

if ($Name -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw "-Name '$Name' is a file name: letters, digits, dots, dashes and underscores" }

# -CamPos 30,-6,40 reaches here as an array and -CamPos '30,-6,40' as a string: both are "x,y,z" for the Game
if ($null -ne $CamPos) { $CamPos = (@($CamPos) -join ',') } else { $CamPos = '' }
if ($null -ne $CamTarget) { $CamTarget = (@($CamTarget) -join ',') } else { $CamTarget = '' }

$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $env:TEMP 'bs3d-web-capture'
$exe = Join-Path $build 'BS3D.exe'
$profileDir = Join-Path $build 'profile'
$frames = Join-Path (Join-Path $env:TEMP 'bs3d-web-capture-frames') $Name
if (-not $OutDir) { $OutDir = Join-Path $root 'Web\src\video' }
$still = $Seconds -le 0

# Only a game of THIS script's build folder is in the way (it would lock the exe the build replaces). Another game running elsewhere
# only shares the GPU, which costs the capture time and nothing else: its clock is not the wall clock's
if (Get-Process -Name BS3D -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }) {
    throw "A BS3D.exe of $build is still running (an earlier capture?): end it first."
}
if (-not $NoEncode -and -not (Test-Path $Python)) { throw "-Python '$Python' does not exist: it needs PyAV (the encoder) and Pillow" }
if ($CamPath) {
    $CamPath = (Resolve-Path $CamPath).Path
}

if (-not $NoBuild) {
    Write-Host "Building the Game into $build ..."
    dotnet build (Join-Path $root 'Game\Game.csproj') -c Release -o $build --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)" }
}
elseif (-not (Test-Path $exe)) { throw "-NoBuild, but $exe does not exist: run once without it" }

# A fresh profile every time: the settings below and nothing else (see web-screenshots.ps1). The capture would use a scratch
# profile of its own without this, but a stated one keeps a level's first-time cards and the online boards out of it too
if (Test-Path $profileDir) { Remove-Item -Recurse -Force $profileDir }
New-Item -ItemType Directory -Force $profileDir | Out-Null
$settings = @'
{
  "format": "bs3d-settings",
  "version": 1,
  "fullscreen": false,
  "fpsOverlay": false,
  "tutorial": false,
  "introLogo": false,
  "quality": "High",
  "adaptiveQuality": false,
  "online": false
}
'@
[System.IO.File]::WriteAllText((Join-Path $profileDir 'Settings.json'), $settings, (New-Object System.Text.UTF8Encoding $false))

# The 16:9 window: the largest standard size the working area holds, not above 1920x1080 unless asked for
if ($Width -le 0 -or $Height -le 0) {
    Add-Type -AssemblyName System.Windows.Forms
    $area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    $window = @(@(1920, 1080), @(1600, 900), @(1280, 720)) |
        Where-Object { $_[0] -le $area.Width -and $_[1] + 40 -le $area.Height } | Select-Object -First 1
    if (-not $window) { throw "The display's working area ($($area.Width)x$($area.Height)) holds no 16:9 window of 1280x720 or more" }
    $Width = $window[0]
    $Height = $window[1]
}
if ($Width % 2 -ne 0 -or $Height % 2 -ne 0) { throw "The window's size ($Width x $Height) must be even: a video's pixels are in pairs" }
Write-Host "Window: ${Width}x${Height}"

# The frames' folder is this script's own: emptied here, because the Game refuses a folder that holds frames
if (Test-Path $frames) { Remove-Item -Recurse -Force $frames }
New-Item -ItemType Directory -Force $frames | Out-Null

$arguments = @(
    "capture=`"$frames`"", "capturefps=$Fps", "capturefrom=$From", "captureseconds=$Seconds",
    "windowed", "width=$Width", "height=$Height", "level=$Level", "userdata=`"$profileDir`""
)
if ($CamPos -or $CamTarget) {
    if (-not ($CamPos -and $CamTarget)) { throw '-CamPos and -CamTarget go together' }
    $arguments += "campos=$CamPos", "camtarget=$CamTarget"
    if ($Fov -gt 0) { $arguments += "fov=$Fov" }
}
elseif ($Fov -gt 0) { throw '-Fov goes with -CamPos and -CamTarget' }
if ($CamPath) {
    if ($CamPos -or $CamTarget) { throw 'Give -CamPos and -CamTarget or -CamPath, not both' }
    $arguments += "campath=`"$CamPath`""
}
if ($Demo) { $arguments += 'capturedemo' }
if ($Shake) { $arguments += 'captureshake' }
if ($Aim) { $arguments += 'captureaim' }
if ($GameArgs) { $arguments += $GameArgs }
$argumentLine = $arguments -join ' '

# CreateProcess with SW_SHOWNOACTIVATE, as quiet-capture.ps1 starts a capture without taking the keyboard: a normal launch, or
# -WindowStyle Minimized, activates the window and whatever is typed meanwhile goes into the game. A minimized window has no back
# buffer to read. The type's name differs from quiet-capture's so both can be loaded in one session.
if (-not ('WebCapture.Launcher' -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
namespace WebCapture
{
    public static class Launcher
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            public int cb; public string reserved, desktop, title;
            public int x, y, xSize, ySize, xChars, yChars, fillAttribute, flags;
            public short showWindow, reserved2;
            public IntPtr reserved3, stdInput, stdOutput, stdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInfo { public IntPtr process, thread; public int processId, threadId; }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateProcess(string application, string commandLine, IntPtr processAttributes,
            IntPtr threadAttributes, bool inheritHandles, int creationFlags, IntPtr environment, string directory,
            ref StartupInfo startupInfo, out ProcessInfo processInfo);

        [DllImport("kernel32.dll")]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll")]
        private static extern bool GetExitCodeProcess(IntPtr handle, out uint exitCode);

        private const int STARTF_USESHOWWINDOW = 0x1;
        private const short SW_SHOWNOACTIVATE = 4;
        private const int CREATE_NO_WINDOW = 0x08000000;
        private static IntPtr _process;

        // Waits for the started process to end: false if it did not within the time, else its exit code
        public static bool Wait(int milliseconds, out int exitCode)
        {
            exitCode = -1;
            if (WaitForSingleObject(_process, (uint)milliseconds) != 0) return false;

            uint code;
            GetExitCodeProcess(_process, out code);
            exitCode = (int)code;
            return true;
        }

        // The process id, or minus the Win32 error
        public static int Start(string exe, string arguments, string directory)
        {
            StartupInfo info = new StartupInfo();
            info.cb = Marshal.SizeOf(typeof(StartupInfo));
            info.flags = STARTF_USESHOWWINDOW;
            info.showWindow = SW_SHOWNOACTIVATE;

            ProcessInfo process;
            if (!CreateProcess(exe, "\"" + exe + "\" " + arguments, IntPtr.Zero, IntPtr.Zero, false, CREATE_NO_WINDOW,
                IntPtr.Zero, directory, ref info, out process))
                return -Marshal.GetLastWin32Error();

            _process = process.process;
            return process.processId;
        }
    }
}
"@
}

Write-Host "Running: BS3D.exe $argumentLine"
$started = Get-Date
$id = [WebCapture.Launcher]::Start($exe, $argumentLine, $build)
if ($id -le 0) { throw "CreateProcess failed with Win32 error $(-$id)." }

$exitCode = 0
if (-not [WebCapture.Launcher]::Wait($Timeout * 1000, [ref]$exitCode)) {
    try { Stop-Process -Id $id -Force } catch { }
    throw "The capture did not finish in $Timeout s and was ended; its log is in $profileDir\Logs\last-run.log"
}
$log = Join-Path $profileDir 'Logs\last-run.log'
Write-Host ("Game ended with exit code {0} after {1:N0} s" -f $exitCode, ((Get-Date) - $started).TotalSeconds)
if (Test-Path $log) { Get-Content $log | Select-String '^\[capture\]|^\[args\] Ignored' | ForEach-Object { Write-Host $_.Line } }
if ($exitCode -ne 0) { throw "The capture failed (exit code $exitCode); its log is $log" }

$manifestPath = Join-Path $frames 'capture.json'
if (-not (Test-Path $manifestPath)) { throw "The Game wrote no capture.json in $frames; its log is $log" }
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$written = @(Get-ChildItem $frames -Filter 'frame-*.png').Count
if ($written -ne $manifest.frames) { throw "The folder holds $written frames, the manifest says $($manifest.frames)" }
Write-Host "Frames: $written of $($manifest.width)x$($manifest.height) in $frames"

if ($NoEncode) { return }

New-Item -ItemType Directory -Force $OutDir | Out-Null
$encoderArguments = @('-I', (Join-Path $PSScriptRoot 'web-capture-encode.py'), '--frames', $frames, '--fps', $manifest.fps, '--out', (Join-Path $OutDir $Name))
if ($still) { $encoderArguments += '--still' } else { $encoderArguments += @('--poster-frame', $PosterFrame, '--video-width', $VideoWidth) }
& $Python @encoderArguments
if ($LASTEXITCODE -ne 0) { throw "The encoder failed (exit $LASTEXITCODE)" }

if (-not $KeepFrames) { Remove-Item -Recurse -Force $frames }
Write-Host 'Look at it, then use it in a page (docs/website.md).'
