# Review before/after captures of BS3D: an exact block diff first, then the local vision model says in words what
# changed in every pair that differs - over the whole frame, and over a crop of each sharpest changed region.
#
#   .\review-captures.ps1 -Before C:\caps\before -After C:\caps\after       # pairs matched by file name
#   .\review-captures.ps1 -Before a.png -After b.png -Out report.md
#   .\review-captures.ps1 -Before C:\caps\before -After C:\caps\after -NoModel
#
# What its answers are worth, and when it pays to run it at all, is in SKILL.md.
# ASCII only: PowerShell 5.1 reads a script without a BOM as ANSI, and one em-dash breaks the parse.

param(
    [Parameter(Mandatory = $true)][string]$Before,
    [Parameter(Mandatory = $true)][string]$After,
    [string]$Filter = '*.png',

    # How far a 16x16 block's MEAN colour must move, 0-255 averaged over R, G and B, to count as changed. Block means
    # cancel film grain; 12 also drops the slow drift of the clouds between two captures a second apart (4-10 on the
    # Testbed's City sky), while a crosshair turning red and growing moved its blocks ~23 and a barrel shifting far more.
    [double]$BlockThreshold = 12,

    # How many of the sharpest changed regions get a cropped question of their own, and how small a region may be
    # (in blocks) and still get one
    [int]$Regions = 2,
    [int]$MinRegionBlocks = 2,

    # Pixel diff only: report which pairs differ and where, and ask no model
    [switch]$NoModel,

    # The vision model. Empty: a vision model already loaded (Gemma 4 preferred), otherwise Gemma 4, loaded.
    [string]$Model,

    [string]$Out,
    [string]$Endpoint = 'http://localhost:1234'
)

$ErrorActionPreference = 'Stop'
$vision = Join-Path $PSScriptRoot '..\local-ai\vision.ps1'
$block = 16

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class CaptureDiff
{
    // Returns: changed blocks, total blocks, width, height, region count, then per region, sharpest first:
    // blocks, minX, minY, maxX, maxY (pixels), peak block delta (rounded). changed = -1 if sizes differ.
    public static int[] Compare(string pathA, string pathB, double threshold, int block)
    {
        using (Bitmap a = new Bitmap(pathA))
        using (Bitmap b = new Bitmap(pathB))
        {
            if (a.Width != b.Width || a.Height != b.Height)
                return new int[] { -1, 0, a.Width, a.Height, 0 };

            int w = a.Width, h = a.Height, bw = w / block, bh = h / block;
            double[] delta = new double[bw * bh];
            byte[] pa = Bytes(a), pb = Bytes(b);
            int changed = 0;

            for (int by = 0; by < bh; by++)
            {
                for (int bx = 0; bx < bw; bx++)
                {
                    long ra = 0, ga = 0, ba = 0, rb = 0, gb = 0, bb = 0;
                    for (int y = by * block; y < (by + 1) * block; y++)
                    {
                        int row = y * w * 4;
                        for (int x = bx * block; x < (bx + 1) * block; x++)
                        {
                            int i = row + x * 4;
                            ba += pa[i]; ga += pa[i + 1]; ra += pa[i + 2];
                            bb += pb[i]; gb += pb[i + 1]; rb += pb[i + 2];
                        }
                    }
                    double d = (Math.Abs(ra - rb) + Math.Abs(ga - gb) + Math.Abs(ba - bb)) / (3.0 * block * block);
                    if (d > threshold) { delta[by * bw + bx] = d; changed++; }
                }
            }

            // Connected regions of changed blocks (8-neighbourhood), ranked by their sharpest block: a thin mark that
            // changed hard outranks a broad area that drifted a little
            bool[] seen = new bool[bw * bh];
            List<double[]> regions = new List<double[]>();
            Stack<int> stack = new Stack<int>();
            for (int s = 0; s < delta.Length; s++)
            {
                if (delta[s] <= 0 || seen[s]) continue;
                int count = 0; int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1; double peak = 0;
                stack.Push(s); seen[s] = true;
                while (stack.Count > 0)
                {
                    int c = stack.Pop(), cx = c % bw, cy = c / bw;
                    count++; peak = Math.Max(peak, delta[c]);
                    minX = Math.Min(minX, cx); minY = Math.Min(minY, cy); maxX = Math.Max(maxX, cx); maxY = Math.Max(maxY, cy);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = cx + dx, ny = cy + dy;
                            if (nx < 0 || ny < 0 || nx >= bw || ny >= bh) continue;
                            int n = ny * bw + nx;
                            if (delta[n] > 0 && !seen[n]) { seen[n] = true; stack.Push(n); }
                        }
                }
                regions.Add(new double[] { count, minX * block, minY * block, (maxX + 1) * block, (maxY + 1) * block, peak });
            }
            regions.Sort(delegate (double[] p, double[] q) { return q[5].CompareTo(p[5]); });

            List<int> result = new List<int> { changed, bw * bh, w, h, regions.Count };
            foreach (double[] r in regions)
                result.AddRange(new int[] { (int)r[0], (int)r[1], (int)r[2], (int)r[3], (int)r[4], (int)Math.Round(r[5]) });
            return result.ToArray();
        }
    }

    static byte[] Bytes(Bitmap bmp)
    {
        Rectangle r = new Rectangle(0, 0, bmp.Width, bmp.Height);
        BitmapData d = bmp.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte[] buf = new byte[bmp.Width * bmp.Height * 4];
            for (int y = 0; y < bmp.Height; y++)
                Marshal.Copy(IntPtr.Add(d.Scan0, y * d.Stride), buf, y * bmp.Width * 4, bmp.Width * 4);
            return buf;
        }
        finally { bmp.UnlockBits(d); }
    }
}
"@

# ---- the pairs
$pairs = @()
if ((Test-Path $Before -PathType Leaf) -and (Test-Path $After -PathType Leaf)) {
    $pairs += , @((Resolve-Path $Before).Path, (Resolve-Path $After).Path, (Split-Path $After -Leaf))
}
else {
    foreach ($f in Get-ChildItem -Path $After -Filter $Filter -File | Sort-Object Name) {
        $match = Join-Path $Before $f.Name
        if (Test-Path $match) { $pairs += , @($match, $f.FullName, $f.Name) }
        else { "== {0}: no file of that name in -Before, skipped" -f $f.Name }
    }
}
if ($pairs.Count -eq 0) { throw "No pairs: give two files, or two directories holding files of the same names." }

# ---- the model, chosen once
$useModel = -not $NoModel
if ($useModel) {
    $loaded = @()
    try { $loaded = @((Invoke-RestMethod "$Endpoint/api/v0/models" -TimeoutSec 5).data | Where-Object { $_.state -eq 'loaded' -and $_.type -eq 'vlm' }) }
    catch { "LM Studio is not answering at $Endpoint - reporting the pixel diff only."; $useModel = $false }

    if ($useModel -and -not $Model) {
        # Gemma 4 measured best on crops and pairs (#440). A Qwen3-VL that is already loaded is used as it is rather
        # than swapped: a swap costs a load, and another session may be sharing the card.
        if ($loaded | Where-Object { $_.id -eq 'google/gemma-4-12b' }) { $Model = 'google/gemma-4-12b' }
        elseif ($loaded.Count -gt 0) { $Model = $loaded[0].id }
        else { $Model = 'google/gemma-4-12b' }
    }
    if ($useModel) { "model: $Model" }
}

$QWhole = 'The two images are two frames from the same game, taken a moment apart. List the visible differences between them, most important first, as at most five short bullet points. If they look identical, say so.'
$QCrop = 'The two images are the same small region of two frames from the same game, taken a moment apart. List the visible differences between them, most important first, as at most five short bullet points. If they look identical, say so.'

function Ask-Model([string]$a, [string]$b, [string]$question, [int]$crop, [string]$rect) {
    $p = @{ Image = $a; Image2 = $b; Question = $question; Model = $Model; Endpoint = $Endpoint; Load = $true; Crop = $crop }
    if ($rect) { $p.Rect = $rect }
    try { return ((& $vision @p | Out-String).Trim() -replace '\s+', ' ') }
    catch { return 'MODEL ERROR: ' + $_.Exception.Message }
}

# ---- review
$report = New-Object System.Collections.ArrayList
$differing = 0
foreach ($pair in $pairs) {
    $a, $b, $name = $pair
    $r = [CaptureDiff]::Compare($a, $b, $BlockThreshold, $block)
    $width = $r[2]; $height = $r[3]; $regionCount = $r[4]
    $row = [ordered]@{ name = $name; changed = $r[0]; blocks = $r[1]; regions = ''; whole = ''; crops = '' }

    if ($r[0] -lt 0) {
        $row.regions = 'sizes differ'
        "== {0}: sizes differ, not compared" -f $name
        [void]$report.Add($row); $differing++
        continue
    }

    if ($r[0] -eq 0) {
        "== {0}: identical at block level - no model call" -f $name
        [void]$report.Add($row)
        continue
    }

    $differing++
    $list = @()
    for ($k = 0; $k -lt $regionCount; $k++) {
        $o = 5 + 6 * $k
        $list += , @($r[$o], $r[$o + 1], $r[$o + 2], $r[$o + 3], $r[$o + 4], $r[$o + 5])
    }
    $shown = ($list | Select-Object -First 4 | ForEach-Object { "x {0}-{1} y {2}-{3} ({4} blocks, peak {5})" -f $_[1], $_[3], $_[2], $_[4], $_[0], $_[5] }) -join '; '
    $row.regions = "{0} region(s): {1}{2}" -f $regionCount, $shown, $(if ($regionCount -gt 4) { '; ...' } else { '' })
    "== {0}: {1} of {2} blocks changed ({3:F1} %), {4}" -f $name, $r[0], $r[1], (100.0 * $r[0] / $r[1]), $row.regions

    if ($useModel) {
        $row.whole = Ask-Model $a $b $QWhole -1280 $null
        "   whole: $($row.whole)"

        # The crop is where the model was measured right (7 of 7 against 5 of 7 on a whole frame, #440): each of the
        # sharpest regions is asked about on its own - padded, at least 256 px square, enlarged when small - unless it
        # is a speck, or covers most of the frame, which the whole-frame answer already is.
        $crops = @()
        foreach ($g in ($list | Where-Object { $_[0] -ge $MinRegionBlocks } | Select-Object -First $Regions)) {
            $rw = $g[3] - $g[1]; $rh = $g[4] - $g[2]
            if ($rw * $rh -ge 0.5 * $width * $height) { continue }
            $side = [Math]::Min([Math]::Min($width, $height), [Math]::Max(256, [Math]::Max($rw, $rh) + 96))
            $x = [int][Math]::Max(0, [Math]::Min($width - $side, ($g[1] + $g[3]) / 2 - $side / 2))
            $y = [int][Math]::Max(0, [Math]::Min($height - $side, ($g[2] + $g[4]) / 2 - $side / 2))
            $scale = [int][Math]::Max(1, [Math]::Min(4, [Math]::Floor(640 / $side)))
            $answer = Ask-Model $a $b $QCrop 0 ("{0},{1},{2},{3},{4}" -f $x, $y, $side, $side, $scale)
            $crops += "[x $x-$($x + $side) y $y-$($y + $side)] $answer"
            "   crop x {0}-{1} y {2}-{3}: {4}" -f $x, ($x + $side), $y, ($y + $side), $answer
        }
        $row.crops = $crops -join ' '
    }
    [void]$report.Add($row)
}

"{0} pairs, {1} differ" -f $pairs.Count, $differing

if ($Out) {
    $md = New-Object System.Text.StringBuilder
    [void]$md.AppendLine('| Pair | Changed blocks | Regions | Whole frame | Changed regions |')
    [void]$md.AppendLine('|---|---|---|---|---|')
    foreach ($row in $report) {
        $changed = if ($row.changed -lt 0) { 'n/a' } else { '{0} / {1}' -f $row.changed, $row.blocks }
        [void]$md.AppendLine(('| {0} | {1} | {2} | {3} | {4} |' -f $row.name, $changed, $row.regions, ($row.whole -replace '\|', '/'), ($row.crops -replace '\|', '/')))
    }
    [IO.File]::WriteAllText($Out, $md.ToString(), (New-Object Text.UTF8Encoding $false))
    "report: $Out"
}
