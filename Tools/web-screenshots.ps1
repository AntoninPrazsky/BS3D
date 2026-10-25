<#
.SYNOPSIS
    Takes the website's gallery: one in-game picture per level, written to Web/src/img/shots/<level>-1600.jpg and -800.jpg.

.DESCRIPTION
    The page at bs3d.winphonew.eu shows the game as the newest release plays it (#815), so its pictures are retaken
    for every release, next to the release's own screenshot (Tools/release-screenshot.ps1): run this on the commit
    being tagged, look at the pictures, commit them, rebuild the site and publish it (docs/website.md).

    Builds the Game in Release into a folder of its own, so the everyday Game\bin is left alone, and runs it once per
    level on a scratch profile (userdata=) whose settings turn the tutorial cards, the intro logo and the FPS counter
    off and hold the High tier, so nothing but the game is in the picture and no run touches your save or posts to
    the online boards. The game runs in a 16:9 window, the largest of a few standard sizes the display's working area
    holds (2560x1440 on a 3840x1600 display), and each frame is its back buffer (shot=), written as a JPEG at two
    widths for the page's srcset. Not fullscreen cut to 16:9: the HUD is anchored to the window's edges, so on a wider
    display the crop cut the score, the balls left and the magazine away and halved the icons at its edge.

    The game's window opens for about fifteen seconds a level and takes the focus, so do not type while it runs.

.PARAMETER Levels
    The levels to photograph, by name; each brings its own scene. Meander is the page's big picture and the rest its
    gallery (Web/src/_data/gallery.json), so a change here is a change there.

.PARAMETER Seed
    The session's seed, which decides the magazine's deal (the queue in the corner is part of the picture).

.PARAMETER Seconds
    How long into the run the frame is taken; a level needs a few seconds for its cluster to settle.

.PARAMETER Quality
    The JPEGs' quality, 1-100.

.EXAMPLE
    .\Tools\web-screenshots.ps1
    .\Tools\web-screenshots.ps1 -Levels Reel,Vortex
#>
param(
    [string[]]$Levels = @('Meander', 'Cannonball', 'Elephant', 'Minaret', 'Helix', 'Umbrella', 'Vortex', 'Juggler', 'Reel'),
    [int]$Seed = 917045606,
    [double]$Seconds = 9,
    [ValidateRange(1, 100)][int]$Quality = 84
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $env:TEMP 'bs3d-web-shots'
$exe = Join-Path $build 'BS3D.exe'
$profileDir = Join-Path $build 'profile'
$target = Join-Path $root 'Web\src\img\shots'
$widths = @(1600, 800)

if (Get-Process -Name BS3D -ErrorAction SilentlyContinue) {
    throw 'A BS3D.exe is already running (another session, or you): close it first, two games at once contend for the GPU.'
}

Write-Host "Building the Game into $build ..."
dotnet build (Join-Path $root 'Game\Game.csproj') -c Release -o $build --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)" }

# A fresh profile every time: the settings below and nothing else, so a level's first-time cards never show
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

New-Item -ItemType Directory -Force $target | Out-Null
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

# The largest 16:9 window the working area holds with its title bar (the HUD is laid out to the window, see the help)
$area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$window = @(@(3840, 2160), @(2560, 1440), @(1920, 1080), @(1600, 900)) |
    Where-Object { $_[0] -le $area.Width -and $_[1] + 40 -le $area.Height } | Select-Object -First 1
if (-not $window) { throw "The display's working area ($($area.Width)x$($area.Height)) holds no 16:9 window of 1600x900 or more" }
Write-Host "Window: $($window[0])x$($window[1])"
$encoder = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
$parameters = New-Object System.Drawing.Imaging.EncoderParameters 1
$parameters.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), ([long]$Quality)

foreach ($level in $Levels) {
    $log = Join-Path $build "run-$level.log"
    # sweep with a 0 degree amplitude holds the barrel dead ahead (release-screenshot.ps1 says why)
    $hold = $Seconds + 20
    $arguments = "windowed width=$($window[0]) height=$($window[1]) nosplash mute nofps quality=high level=$level seed=$Seed sweep=0:${hold}:0 shot=$Seconds userdata=`"$profileDir`""
    Write-Host "Running: BS3D.exe $arguments"
    $process = Start-Process $exe -ArgumentList $arguments -WorkingDirectory $build -PassThru `
        -RedirectStandardOutput $log -RedirectStandardError "$log.err"

    try {
        $png = $null
        for ($waited = 0; $waited -lt 120 -and -not $png; $waited++) {
            Start-Sleep -Seconds 1
            if ($process.HasExited) { break }
            $line = Get-Content $log -ErrorAction SilentlyContinue | Select-String '^\[shot\] (.+\.png)\s*$' | Select-Object -First 1
            if ($line) { $png = $line.Matches[0].Groups[1].Value.Trim() }
        }
        Start-Sleep -Seconds 2
    }
    finally {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    }

    if (-not $png -or -not (Test-Path $png)) { throw "The run of $level never said [shot]; its log is $log" }

    $image = [System.Drawing.Image]::FromFile($png)
    try {
        # 16:9 round the centre: nothing to cut from the 16:9 window, unless Windows gave it another size
        Write-Host "  frame $($image.Width)x$($image.Height)"
        $cropWidth = [Math]::Min($image.Width, [int][Math]::Round($image.Height * 16 / 9))
        $cropHeight = [Math]::Min($image.Height, [int][Math]::Round($cropWidth * 9 / 16))
        $source = New-Object System.Drawing.Rectangle ([int](($image.Width - $cropWidth) / 2)), ([int](($image.Height - $cropHeight) / 2)), $cropWidth, $cropHeight

        foreach ($width in $widths) {
            $height = [int][Math]::Round($width * 9 / 16)
            $bitmap = New-Object System.Drawing.Bitmap $width, $height
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.DrawImage($image, (New-Object System.Drawing.Rectangle 0, 0, $width, $height), $source, [System.Drawing.GraphicsUnit]::Pixel)
                $file = Join-Path $target ("{0}-{1}.jpg" -f $level.ToLowerInvariant(), $width)
                $bitmap.Save($file, $encoder, $parameters)
                "{0}: {1:N0} KB" -f $file, ((Get-Item $file).Length / 1KB) | Write-Host
            }
            finally {
                $graphics.Dispose()
                $bitmap.Dispose()
            }
        }
    }
    finally {
        $image.Dispose()
    }
    Remove-Item $png
}

Write-Host "Look at them, commit them, then rebuild and publish the site (docs/website.md)."
