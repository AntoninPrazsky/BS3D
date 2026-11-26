<#
.SYNOPSIS
    Collects what one program says through OutputDebugString while it runs, without a debugger: the D3D11 debug
    layer's messages, when the Game is started with d3ddebug (#1252).

.DESCRIPTION
    The D3D11 debug layer validates every call the program makes and reports what it finds through OutputDebugString,
    which goes nowhere unless something listens. This listens the way DebugView does, through the DBWIN shared
    buffer of the user's session, so it needs no administrator and no debugger. It must be started BEFORE the program,
    or the messages the device's creation prints are lost.

    It waits up to -WaitStart seconds for a process named -ProcessName to appear, keeps the lines that process writes
    and stops when the process ends, or after -Seconds. Every line is counted, and the report keeps each distinct line
    once (object addresses taken out of what makes it distinct), with how many times it came and when it first came,
    the D3D11 errors first, then the warnings, then the rest. A layer that complains about one draw complains every
    frame, so the raw stream is not kept: thousands of the same line would bury the one that matters. Its INFO lines,
    one for every object made or destroyed, are only counted.

    Only one listener at a time can read the buffer: close DebugView, and never run two of these at once. A program
    started under a debugger sends its output to the debugger, not here.

    The debug layer is the optional Windows feature "Graphics Tools". Without it the Game refuses d3ddebug and says so
    on a [gpu] line, since Direct3D cannot create a debug device at all. In an administrator PowerShell:
        Add-WindowsCapability -Online -Name Tools.Graphics.DirectX~~~~0.0.1.0

.PARAMETER OutFile
    The report, written when the listening ends.

.PARAMETER ProcessName
    The program's process name, without .exe. BS3D by default.

.PARAMETER Seconds
    The most seconds to listen once the process has appeared.

.PARAMETER WaitStart
    The most seconds to wait for the process to appear.

.EXAMPLE
    # Listen in the background, then start the run through quiet-capture.ps1
    Start-Process powershell -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File','Tools\d3d-debug-capture.ps1','-OutFile','C:\temp\city.txt'
    Tools\quiet-capture.ps1 -Exe ... -Arguments 'd3ddebug scene=City userdata=... shot=8' -Shots 1 -Timeout 60 ...
#>
param(
    [Parameter(Mandatory = $true)][string]$OutFile,
    [string]$ProcessName = 'BS3D',
    [int]$Seconds = 600,
    [int]$WaitStart = 60
)

$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

public static class DebugOutputListener
{
    private const int BUFFER_BYTES = 4096;

    private static readonly Regex Addresses = new Regex("0x[0-9A-Fa-f]{6,}", RegexOptions.Compiled);

    //"Create ID3D11Buffer", "Destroy ID3D11Texture2D": what an INFO line did to which kind of object
    private static readonly Regex InfoKind = new Regex("(Create|Destroy|Live) ID3D11\\w+", RegexOptions.Compiled);

    private sealed class Seen
    {
        public string Text;
        public int Count;
        public double FirstSecond;
    }

    public static string Listen(string processName, int seconds, int waitStart)
    {
        bool created;
        using (var bufferReady = new EventWaitHandle(false, EventResetMode.AutoReset, "DBWIN_BUFFER_READY", out created))
        using (var dataReady = new EventWaitHandle(false, EventResetMode.AutoReset, "DBWIN_DATA_READY", out created))
        using (var buffer = MemoryMappedFile.CreateOrOpen("DBWIN_BUFFER", BUFFER_BYTES))
        using (var view = buffer.CreateViewAccessor(0, BUFFER_BYTES))
        {
            var lines = new Dictionary<string, Seen>();
            var pids = new HashSet<int>();
            var bytes = new byte[BUFFER_BYTES - 4];
            Stopwatch clock = Stopwatch.StartNew();
            double appearedAt = -1;
            double lastLookup = -1;
            int total = 0;
            int infos = 0;
            var infoKinds = new SortedDictionary<string, int>(StringComparer.Ordinal);

            bufferReady.Set();

            while (true)
            {
                double now = clock.Elapsed.TotalSeconds;

                //Which processes are the program: looked up once a second, since a lookup is not free
                if (now - lastLookup >= 1.0)
                {
                    lastLookup = now;
                    Process[] running = Process.GetProcessesByName(processName);
                    foreach (Process p in running) pids.Add(p.Id);

                    if (appearedAt < 0 && running.Length > 0) appearedAt = now;
                    if (appearedAt >= 0 && running.Length == 0) break;
                    if (appearedAt < 0 && now > waitStart) return "The process " + processName + " never appeared in " + waitStart + " s.";
                    if (appearedAt >= 0 && now - appearedAt > seconds) break;
                }

                if (!dataReady.WaitOne(250)) continue;

                int pid = view.ReadInt32(0);
                view.ReadArray(4, bytes, 0, bytes.Length);
                bufferReady.Set();

                if (!pids.Contains(pid)) continue;

                int length = Array.IndexOf(bytes, (byte)0);
                if (length < 0) length = bytes.Length;
                string text = Encoding.Default.GetString(bytes, 0, length).TrimEnd('\r', '\n');
                if (text.Length == 0) continue;

                total++;

                //The layer's INFO lines name every object made and destroyed, each with its own address: counted by kind
                //and second, since objects made and destroyed every frame are a finding of their own
                if (text.Contains(" INFO: "))
                {
                    infos++;
                    Match made = InfoKind.Match(text);
                    string other = Addresses.Replace(text, "0x?");
                    string kind = made.Success ? made.Value : other.Substring(0, Math.Min(160, other.Length));
                    int bucket = (int)(appearedAt >= 0 ? now - appearedAt : 0);
                    string infoKey = bucket.ToString("000", CultureInfo.InvariantCulture) + " s  " + kind;
                    int count;
                    infoKinds.TryGetValue(infoKey, out count);
                    infoKinds[infoKey] = count + 1;
                    continue;
                }

                //One line about many objects is one finding: the addresses are taken out of what makes it distinct
                string key = Addresses.Replace(text, "0x?");
                Seen seen;
                if (!lines.TryGetValue(key, out seen))
                {
                    seen = new Seen { Text = text, FirstSecond = appearedAt >= 0 ? now - appearedAt : 0 };
                    lines.Add(key, seen);
                }
                seen.Count++;
            }

            var report = new StringBuilder();
            int errors = lines.Values.Where(IsError).Sum(s => s.Count);
            int warnings = lines.Values.Where(IsWarning).Sum(s => s.Count);
            report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0} lines from {1}; D3D11 errors {2}, warnings {3}, {4} other distinct; INFO {5}; listened {6:0} s",
                total, processName, errors, warnings, lines.Values.Count(s => !IsError(s) && !IsWarning(s)), infos,
                clock.Elapsed.TotalSeconds));

            foreach (Seen s in lines.Values.OrderBy(Rank).ThenBy(s => s.FirstSecond))
                report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,7}x  first at {1,6:0.0} s  {2}", s.Count, s.FirstSecond, s.Text));

            //The INFO lines by second and kind: a kind that goes on every second after the loading is made every frame
            if (infoKinds.Count > 0) report.AppendLine("INFO by second and kind:");
            foreach (KeyValuePair<string, int> kind in infoKinds)
                report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,7}x  {1}", kind.Value, kind.Key));

            return report.ToString();
        }
    }

    private static bool IsError(Seen s) { return s.Text.Contains("ERROR") || s.Text.Contains("CORRUPTION"); }

    private static bool IsWarning(Seen s) { return s.Text.Contains("WARNING"); }

    private static int Rank(Seen s) { return IsError(s) ? 0 : IsWarning(s) ? 1 : 2; }
}
'@

$report = [DebugOutputListener]::Listen($ProcessName, $Seconds, $WaitStart)
$directory = Split-Path -Parent $OutFile
if ($directory -and -not (Test-Path $directory)) { New-Item -ItemType Directory -Force $directory | Out-Null }
[IO.File]::WriteAllText($OutFile, $report, (New-Object Text.UTF8Encoding($false)))
Write-Output ($report -split "`n" | Select-Object -First 1)
