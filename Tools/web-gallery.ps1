<#
.SYNOPSIS
    Takes the website's gallery (#922): one still per level, a moment of the game in motion from a stated camera, written to
    Web/src/img/shots/<file>-1600.jpg and -800.jpg.

.DESCRIPTION
    The gallery's pictures are not the plain view from behind the gun at score 0 (web-screenshots.ps1 takes that, for the page's
    big picture): each is a moment of the benchmark's action - a group falling, a shot in flight, a cut striking - from a camera
    framed for it, with no HUD, at the Ultra tier. What to take is in Web/src/_data/gallery.json, each entry's "shot":
    "from" is the game time of the frame, "campos", "camtarget" and "fov" the camera (world coordinates, the Testbed's spelling).

    Each picture is one run of Tools/web-capture.ps1 with -Seconds 0 (docs/website.md, "Filming the game"). The capture's clock
    is a fixed step a frame and its world repeats exactly, so the same entry gives the same picture on the same build; the window
    size and the tier change the picture's sharpness, not the world. A change to the game's physics, the demo's aim or a level
    moves what happens at a given second, so LOOK at every picture before committing it, as with any capture: a moment that
    showed a group falling may show an empty sky on a later build. To find a new moment, film the level's demo from the entry's
    camera (web-capture.ps1 -Demo -Fps 15 -From 8 -Seconds 16 -NoEncode -KeepFrames) and read the frame's time off the folder:
    frame n of a window from F is game time ceil(F * 15) / 15 + (n - 1) / 15.

    The rate is fixed at 15 frames a second because the demo's shots are checked once a frame: the same second on another rate
    can be another world.

.PARAMETER Levels
    The levels to take, by name; all of gallery.json's by default.

.PARAMETER NoBuild
    Use the Game already built in web-capture.ps1's folder.

.EXAMPLE
    .\Tools\web-gallery.ps1
    .\Tools\web-gallery.ps1 -Levels Reel,Vortex -NoBuild
#>
param(
    [string[]]$Levels,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$gallery = Get-Content (Join-Path $root 'Web\src\_data\gallery.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$target = Join-Path $root 'Web\src\img\shots'

# 2560x1440 when the working area holds it (the 1600 px picture is then scaled down from more than it shows), else what
# web-capture.ps1 picks. The window's aspect is 16:9 either way, so the framing is the same
Add-Type -AssemblyName System.Windows.Forms
$area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$size = @{}
if ($area.Width -ge 2560 -and $area.Height -ge 1440 + 40) { $size = @{ Width = 2560; Height = 1440 } }

$built = $NoBuild.IsPresent
foreach ($entry in $gallery) {
    if ($Levels -and $Levels -notcontains $entry.level) { continue }
    if (-not $entry.shot) { throw "gallery.json's '$($entry.file)' has no shot" }

    $capture = @{
        Name      = $entry.file
        Level     = $entry.level
        From      = [double]$entry.shot.from
        Seconds   = 0
        Fps       = 15
        Demo      = $true
        CamPos    = $entry.shot.campos
        CamTarget = $entry.shot.camtarget
        Fov       = [double]$entry.shot.fov
        GameArgs  = 'quality=ultra'
        OutDir    = $target
        NoBuild   = $built
    } + $size
    Write-Host "== $($entry.level) at $($entry.shot.from) s"
    & (Join-Path $PSScriptRoot 'web-capture.ps1') @capture
    $built = $true
}
Write-Host "Look at every picture in $target before committing it."
