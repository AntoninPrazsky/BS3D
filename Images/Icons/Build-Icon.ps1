<#
.SYNOPSIS
    Assembles the executables' multi-resolution Icon.ico out of the ico6-<N>.png ladder.

.DESCRIPTION
    Run it with no arguments to regenerate the game's icon after the artwork changes:

        .\Build-Icon.ps1

    The fourteen sizes are not arbitrary and not a Store artefact: {16, 20, 24, 30, 32, 36, 40,
    48, 60, 64, 72, 80, 96, 256} is exactly the union of the sizes the Windows 11 shell asks a
    Win32 executable for across its 100-400 % scale factors - 16-64 for the title bar, context
    menu and tray, 24-96 for the taskbar, search results and the all-apps list, and 32-256 for a
    Start pin. Windows matches a request exactly if it can and otherwise takes the next size
    ABOVE and scales it down, which is why the 256 entry matters even on a 100 % display: it is
    what guarantees the icon is never upscaled.

    Each PNG is used AS AUTHORED - nothing here resamples - so a size that ever does get drawn by
    hand for legibility keeps its own artwork rather than being overwritten by a downscale of the
    256. (Today they are all Inkscape exports of ico6.svg at scaled DPI, i.e. within a few codes
    of a plain downscale, but that is the artwork's business and not this script's.)

    PAYLOAD ENCODING. Sizes at or below -BmpMaxSize are stored as 32-bit BGRA DIBs, the classic
    ICO payload; the rest keep their PNG bytes verbatim. Both halves are deliberate:

      - PNG at every size would cut the file from 185 KB to 79 KB (measured with -BmpMaxSize 0:
        78 627 B against 184 590 B, i.e. 43 %) and the Windows 11 shell reads it perfectly -
        PrivateExtractIconsW and LoadImageW return all fourteen sizes pixel-exact from an
        all-PNG icon. But an old enough System.Drawing consumer - old installers, PowerShell 5.1
        scripts, shell extensions, Mono - cannot read a PNG sub-image at all, and it fails
        SILENTLY: it takes the entry for a DIB and decodes the compressed stream as raw pixels,
        so every size from 16 to 96 comes back as noise (the 32 frame measures 1024 distinct
        colours over 1024 pixels, none matching the source) with no exception and no blank frame
        anywhere. Two traps in testing that: it is gated on the CONSUMER'S OWN TARGET FRAMEWORK
        rather than on .NET Framework as such (Switch.System.Drawing.DontSupportPngFramesInIcons
        - measured, two byte-identical builds differing only in [TargetFramework] read the same
        all-PNG icon as noise at v4.5 and 100 % pixel-exact at v4.8, and a host declaring no
        target framework such as PowerShell 5.1 takes the broken path); and there is no throw and
        no blank frame to look for, so compare pixels against the source. The failure does track
        the payload encoding rather than the size: in a -BmpMaxSize 48 file the DIB range reads
        pixel-exact and the PNG range reads noise. Storing the small entries as DIB is what buys
        compatibility with every consumer that is not the shell - measured, the shipping split
        reads 100 % pixel-exact at every size even for a consumer targeting v4.5.
      - The 256 stays PNG because uncompressed it alone is 262 KB - 30 984 B against 270 376 B,
        the one place a tenth really is the ratio - and because Microsoft's own rule is that only
        the 256x256 image should be compressed.

    The default threshold therefore follows that rule literally (DIB through 96, PNG for the 256
    only) and has no failure mode below 256 at any size. It costs about 185 KB, which is nothing
    beside the content directory; passing -BmpMaxSize 48 brings it to 98 KB and is still correct
    for every size a legacy consumer realistically requests, at the price of PNG entries at 60-96.

    THE DIB QUIRK, both halves of which are mandatory: an icon DIB declares biHeight as TWICE the
    icon height, because it is the colour image stacked over a 1 bpp AND mask, and that mask has
    to actually be there (bottom-up, rows padded to 4 bytes, a set bit meaning transparent). It is
    derived from the alpha channel here rather than left blank, so the icon keeps a correct
    silhouette in the code paths that ignore alpha entirely. A PNG entry takes the opposite
    contract - 32 bpp ARGB, no BITMAPINFOHEADER, no mask, alpha IS the mask - and its directory
    entry writes the 256 dimension as 0.

    No ImageMagick, no Pillow, nothing to install: PowerShell and System.Drawing only. (Pillow's
    ICO writer resamples from one base image unless append_images= is used, and its BMP mode emits
    a DIB with the doubled biHeight but no AND mask at all, so neither route is usable here.)

.PARAMETER Source
    Directory holding the <Prefix><N>.png ladder. Defaults to the script's own directory.

.PARAMETER Output
    The .ico to write. Defaults to the game's Game\Icon.ico.

.PARAMETER Prefix
    Filename prefix before the pixel size.

.PARAMETER Sizes
    Pixel sizes to include, in the order they appear in the icon directory.

.PARAMETER BmpMaxSize
    Sizes at or below this are stored as DIB, larger ones as PNG. See the discussion above.
#>
[CmdletBinding()]
param(
    [string] $Source = $PSScriptRoot,
    [string] $Output = (Join-Path $PSScriptRoot '..\..\Game\Icon.ico'),
    [string] $Prefix = 'ico6-',
    [int[]] $Sizes = @(16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256),
    [int] $BmpMaxSize = 96
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Read-PngHeader([string] $path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ($bytes.Length -lt 33) { throw "$path is too short to be a PNG" }

    $signature = 137, 80, 78, 71, 13, 10, 26, 10
    for ($i = 0; $i -lt 8; $i++) {
        if ($bytes[$i] -ne $signature[$i]) { throw "$path is not a PNG" }
    }

    # IHDR: width at 16..19, height at 20..23, both big-endian. The [int] casts are load-bearing -
    # PowerShell's -shl keeps the left operand's type, so shifting a [byte] truncates the result
    # back into a byte and every dimension silently comes out 0.
    $w = (([int]$bytes[16]) -shl 24) -bor (([int]$bytes[17]) -shl 16) -bor (([int]$bytes[18]) -shl 8) -bor [int]$bytes[19]
    $h = (([int]$bytes[20]) -shl 24) -bor (([int]$bytes[21]) -shl 16) -bor (([int]$bytes[22]) -shl 8) -bor [int]$bytes[23]

    [pscustomobject]@{ Width = $w; Height = $h; Bytes = $bytes }
}

function New-IconDib([string] $path, [int] $size) {
    $bitmap = New-Object System.Drawing.Bitmap($path)
    try {
        $rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
        $data = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $stride = [Math]::Abs($data.Stride)
            $raw = New-Object byte[] ($stride * $size)
            [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $raw, 0, $raw.Length)
        }
        finally { $bitmap.UnlockBits($data) }

        $maskStride = [int][Math]::Floor(($size + 31) / 32) * 4
        $xorSize = $size * $size * 4
        $andSize = $maskStride * $size

        $stream = New-Object System.IO.MemoryStream
        $writer = New-Object System.IO.BinaryWriter($stream)
        try {
            $writer.Write([uint32] 40)                          # biSize
            $writer.Write([int32] $size)                        # biWidth
            $writer.Write([int32] ($size * 2))                  # biHeight - XOR image over AND mask
            $writer.Write([uint16] 1)                           # biPlanes
            $writer.Write([uint16] 32)                          # biBitCount
            $writer.Write([uint32] 0)                           # biCompression = BI_RGB
            $writer.Write([uint32] ($xorSize + $andSize))       # biSizeImage
            $writer.Write([int32] 0); $writer.Write([int32] 0)   # pixels per metre
            $writer.Write([uint32] 0); $writer.Write([uint32] 0) # palette

            # The XOR image, bottom-up. GDI+ hands back BGRA rows top-down at $stride.
            for ($y = $size - 1; $y -ge 0; $y--) {
                $writer.Write($raw, $y * $stride, $size * 4)
            }

            # The AND mask, bottom-up, a set bit meaning transparent. Taken from the alpha channel
            # so the silhouette survives wherever alpha is ignored.
            $maskRow = New-Object byte[] $maskStride
            for ($y = $size - 1; $y -ge 0; $y--) {
                [Array]::Clear($maskRow, 0, $maskRow.Length)
                for ($x = 0; $x -lt $size; $x++) {
                    if ($raw[$y * $stride + $x * 4 + 3] -eq 0) {
                        $byteIndex = [int][Math]::Floor($x / 8)
                        $maskRow[$byteIndex] = $maskRow[$byteIndex] -bor (0x80 -shr ($x % 8))
                    }
                }
                $writer.Write($maskRow, 0, $maskStride)
            }

            $writer.Flush()
            $stream.ToArray()
        }
        finally { $writer.Dispose(); $stream.Dispose() }
    }
    finally { $bitmap.Dispose() }
}

$entries = @()
foreach ($size in $Sizes) {
    $path = Join-Path $Source ("{0}{1}.png" -f $Prefix, $size)
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing source image: $path" }

    # The filename is not taken on trust: a ladder exported at the wrong DPI would otherwise be
    # assembled into a directory whose entries lie about their own payloads.
    $png = Read-PngHeader $path
    if ($png.Width -ne $size -or $png.Height -ne $size) {
        throw "$path is $($png.Width)x$($png.Height), expected $size x $size"
    }

    if ($size -le $BmpMaxSize) {
        $entries += [pscustomobject]@{ Size = $size; Payload = (New-IconDib $path $size); Kind = 'DIB' }
    }
    else {
        $entries += [pscustomobject]@{ Size = $size; Payload = $png.Bytes; Kind = 'PNG' }
    }
}

$offset = 6 + 16 * $entries.Count

$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($stream)
try {
    $writer.Write([uint16] 0)                  # idReserved
    $writer.Write([uint16] 1)                  # idType - 1 is an icon
    $writer.Write([uint16] $entries.Count)

    foreach ($e in $entries) {
        $dim = if ($e.Size -ge 256) { 0 } else { $e.Size }   # 0 means 256
        $writer.Write([byte] $dim)             # bWidth
        $writer.Write([byte] $dim)             # bHeight
        $writer.Write([byte] 0)                # bColorCount - 0 at 8 bpp and above
        $writer.Write([byte] 0)                # bReserved
        $writer.Write([uint16] 1)              # wPlanes
        $writer.Write([uint16] 32)             # wBitCount
        $writer.Write([uint32] $e.Payload.Length)
        $writer.Write([uint32] $offset)
        $offset += $e.Payload.Length
    }

    foreach ($e in $entries) { $writer.Write($e.Payload, 0, $e.Payload.Length) }

    $writer.Flush()
    # The default -Output is rooted but full of ..\ segments, and a relative one has to be taken
    # against the caller's directory rather than the script's; Join-Path on two rooted paths would
    # concatenate them into nonsense.
    $resolved = if ([System.IO.Path]::IsPathRooted($Output)) { [System.IO.Path]::GetFullPath($Output) }
    else { [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $Output)) }
    [System.IO.File]::WriteAllBytes($resolved, $stream.ToArray())
}
finally { $writer.Dispose(); $stream.Dispose() }

foreach ($e in $entries) { Write-Host ("{0,4} px  {1}  {2,8:N0} B" -f $e.Size, $e.Kind, $e.Payload.Length) }
Write-Host ""
Write-Host ("{0}: {1} entries, {2:N0} bytes" -f $resolved, $entries.Count, (Get-Item -LiteralPath $resolved).Length)
