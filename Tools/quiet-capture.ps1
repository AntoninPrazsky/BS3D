<#
.SYNOPSIS
    Runs BS3D.exe or Testbed.exe without taking the keyboard, waits for the frames its shot= asked for, ends it and
    copies the frames out.

.DESCRIPTION
    A capture an agent takes while the owner works at the desktop. A normal launch, or PowerShell's -WindowStyle
    Minimized, activates the new window, and whatever the owner types goes into the game. A minimized window wrote
    0-byte PNGs. So the program is started through CreateProcess with SW_SHOWNOACTIVATE: it is visible and draws, but
    it does not take the focus. Its console is not shown, and its output goes to its run log, not here.

    The game's shot= saves frames into Screenshots\ beside the exe. This waits until -Shots new non-empty PNGs are
    there, or -Timeout seconds pass, then ends the process. A shot lands 12 to 16 seconds into a Game run on the
    desktop (the scene's load varies), so a fixed sleep either wastes time or misses the frame. The frames are copied
    to -OutDir as <Tag>-1.png, <Tag>-2.png and so on, in the order they were written.

    The Game's shot= implies nofocuspause, so an unfocused run draws as a focused one does. A scripted Testbed run (any
    at=) does the same, and measures identically without the focus.

    What the arguments need:
      * userdata=<dir> on every Game run, or the run writes the player's own save and settings. A Settings.json there
        needs "format": "bs3d-settings", "version": 1 beside its keys, or the game rejects it.
      * levelfile= and userdata= paths with forward slashes when this is called from bash. A backslash before a
        bash variable ("...\\$name") stays literal.
      * a window that pops up under the owner's click can take a stray click: re-take an odd capture once before
        suspecting the code.

.PARAMETER Exe
    The program, a full path: Game\bin\net10.0-windows\BS3D.exe or Testbed\bin\net10.0-windows\Testbed.exe.

.PARAMETER Arguments
    Its command line, shot= included.

.PARAMETER Shots
    How many frames to wait for.

.PARAMETER Timeout
    The most seconds to wait before ending the program anyway.

.PARAMETER OutDir
    Where the frames are copied. Created if missing.

.PARAMETER Tag
    The copies' name before the number.

.PARAMETER StdOut
    A file the program's standard output and error go to, for a run whose lines are the point: a Testbed or Game
    `logfps` run, where every [fps] line is a reading. Without it the output is dropped. With -Shots 1, a shot=
    late in the run and an at=...:Escape after it (the Testbed), the run measures, photographs and ends itself.

.EXAMPLE
    .\Tools\quiet-capture.ps1 -Exe C:/src/BS3D/Game/bin/net10.0-windows/BS3D.exe -OutDir C:/tmp/caps -Tag help1 `
        -Arguments 'nosplash windowed width=1600 height=900 preview=1 help=1 userdata=C:/tmp/ud shot=16'
#>
param(
    [Parameter(Mandatory)][string]$Exe,
    [Parameter(Mandatory)][string]$Arguments,
    [ValidateRange(1, 64)][int]$Shots = 1,
    [ValidateRange(5, 900)][int]$Timeout = 90,
    [Parameter(Mandatory)][string]$OutDir,
    [string]$Tag = 'shot',
    [string]$StdOut = ''
)

. "$PSScriptRoot\PathGuard.ps1"
Assert-SanePath -Name Exe -Value $Exe -Full
Assert-SanePath -Name OutDir -Value $OutDir -Full
if ($StdOut) { Assert-SanePath -Name StdOut -Value $StdOut -Full }

if (-not (Test-Path $Exe)) { throw "-Exe '$Exe' does not exist; build it first." }
New-Item -ItemType Directory -Force $OutDir | Out-Null

if (-not ('QuietCapture.Launcher2' -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.IO;
using System.Runtime.InteropServices;
namespace QuietCapture
{
    public static class Launcher2
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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetHandleInformation(IntPtr handle, int mask, int flags);

        private const int HANDLE_FLAG_INHERIT = 1;
        private static FileStream _output;

        private const int STARTF_USESHOWWINDOW = 0x1, STARTF_USESTDHANDLES = 0x100;
        private const short SW_SHOWNOACTIVATE = 4;
        private const int CREATE_NO_WINDOW = 0x08000000;

        //The process id, or minus the Win32 error. The application name is passed explicitly: without it
        //CreateProcess searches for the program and fails with error 3 on a path it would find this way
        //With stdOut, the file is the program's output and error, its handle made inheritable and handed over in the
        //startup info; kept open until CloseOutput, after the program has ended
        public static int Start(string exe, string arguments, string directory, string stdOut)
        {
            StartupInfo info = new StartupInfo();
            info.cb = Marshal.SizeOf(typeof(StartupInfo));
            info.flags = STARTF_USESHOWWINDOW | STARTF_USESTDHANDLES;
            info.showWindow = SW_SHOWNOACTIVATE;

            bool inherit = !string.IsNullOrEmpty(stdOut);
            if (inherit)
            {
                _output = new FileStream(stdOut, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                IntPtr handle = _output.SafeFileHandle.DangerousGetHandle();
                SetHandleInformation(handle, HANDLE_FLAG_INHERIT, HANDLE_FLAG_INHERIT);
                info.stdOutput = handle;
                info.stdError = handle;
            }

            ProcessInfo process;
            if (!CreateProcess(exe, "\"" + exe + "\" " + arguments, IntPtr.Zero, IntPtr.Zero, inherit, CREATE_NO_WINDOW,
                IntPtr.Zero, directory, ref info, out process))
                return -Marshal.GetLastWin32Error();

            return process.processId;
        }

        public static void CloseOutput()
        {
            if (_output != null) _output.Dispose();
            _output = null;
        }
    }
}
"@
}

$directory = Split-Path $Exe
$screenshots = Join-Path $directory 'Screenshots'
$started = Get-Date
$id = [QuietCapture.Launcher2]::Start($Exe, $Arguments, $directory, $StdOut)
if ($id -le 0) { throw "CreateProcess failed with Win32 error $(-$id)." }

$frames = @()
while (((Get-Date) - $started).TotalSeconds -lt $Timeout) {
    Start-Sleep -Milliseconds 500
    if (Test-Path $screenshots) {
        $frames = @(Get-ChildItem $screenshots -Filter *.png |
            Where-Object { $_.LastWriteTime -gt $started -and $_.Length -gt 0 } | Sort-Object LastWriteTime)
    }
    if ($frames.Count -ge $Shots) { Start-Sleep -Milliseconds 800; break }
    if (-not (Get-Process -Id $id -ErrorAction SilentlyContinue)) { break }
}

#Waited for, so a capture taken right after this one does not start beside a dying window
try { Stop-Process -Id $id -Force -ErrorAction Stop; Wait-Process -Id $id -Timeout 15 -ErrorAction Stop } catch { }
[QuietCapture.Launcher2]::CloseOutput()

$n = 0
foreach ($frame in $frames) {
    $n++
    Copy-Item $frame.FullName (Join-Path $OutDir ('{0}-{1}.png' -f $Tag, $n))
}

$seconds = ((Get-Date) - $started).TotalSeconds
Write-Output ('{0}: {1} of {2} frame(s) in {3:N0} s' -f $Tag, $n, $Shots, $seconds)
if ($n -lt $Shots) { exit 1 }
