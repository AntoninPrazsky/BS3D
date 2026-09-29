<#
.SYNOPSIS
    Takes the screenshot a release's notes open with: Images/releases/<Version>.jpg.

.DESCRIPTION
    Builds the Game in Release stamped as the release (-p:BS3DReleaseVersion, exactly what release.yml passes on a
    tag, so the build says it is <Version> - its main menu and About page carry the name in the corner), into a
    folder of its own so the everyday Game\bin is left alone. Runs it once, scripted, saves the frame at the
    native resolution of this machine's display (a shot= capture is the back buffer, so the picture is what a
    player sees) and writes it as a JPEG.

    Run it on the commit you are about to tag, look at the picture, then commit the file BEFORE pushing the tag:
    release.yml links Images/releases/<tag>.jpg into the notes at the tag's own ref, and a tag without one still
    publishes (without a picture).

    The game's window opens for about fifteen seconds and takes the focus, so do not type while it runs. It runs
    on a scratch profile (userdata=), never on your save or your settings.

.PARAMETER Version
    The tag, "v0.2.1". Names the file and stamps the build.

.PARAMETER Level
    The level to photograph, by name. A level brings its own scene, so this picks the picture: Reel is the neon
    city, Vortex space, Minaret the desert, Meander the volcano.

.PARAMETER Menu
    Photograph the main menu instead of a level - the 3D title over the scene, and the version in the corner.

.PARAMETER Scene
    The menu's scene (only with -Menu; a level's scene is the level's own).

.PARAMETER Seed
    The session's seed, which decides the magazine's deal. Pinned because the queue in the corner is part of the
    picture and an unpinned deal is a lottery: one of the first four runs came out with five balls of the same grey. This one
    deals a queue of four different colours on Reel; another level or a new look wants another look at the deal
    (the run prints "[session] seed N" and any N replays with -Seed N).

.PARAMETER Seconds
    How long into the run the frame is taken (the barrel is held dead ahead until well after it). A level needs a few seconds for its cluster to settle.

.PARAMETER Quality
    The JPEG's quality, 1-100.

.EXAMPLE
    .\Tools\release-screenshot.ps1 -Version v0.2.1
    .\Tools\release-screenshot.ps1 -Version v0.2.1 -Level Vortex
    .\Tools\release-screenshot.ps1 -Version v0.2.1 -Menu -Scene sea
#>
param(
    [Parameter(Mandatory)][ValidatePattern('^v\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$')][string]$Version,
    [string]$Level = 'Reel',
    [switch]$Menu,
    [string]$Scene = 'desert',
    [int]$Seed = 917045606,
    [double]$Seconds = 9,
    [ValidateRange(1, 100)][int]$Quality = 92
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $env:TEMP "bs3d-release-shot-$Version"
$exe = Join-Path $build 'BS3D.exe'
$profileDir = Join-Path $build 'profile'
$log = Join-Path $build 'run.log'
$target = Join-Path $root "Images\releases\$Version.jpg"

if (Get-Process -Name BS3D -ErrorAction SilentlyContinue) {
    throw 'A BS3D.exe is already running (another session, or you): close it first, two games at once contend for the GPU.'
}

Write-Host "Building the Game as $Version into $build ..."
dotnet build (Join-Path $root 'Game\Game.csproj') -c Release "-p:BS3DReleaseVersion=$Version" -o $build --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)" }

New-Item -ItemType Directory -Force $profileDir | Out-Null

# nosplash: straight to the front end or the level. mute and nofps: nothing over the picture but the game.
# sweep with a 0 degree amplitude holds the barrel's traverse dead ahead for the whole run: the gun aims off the
# mouse otherwise, and a run started with the cursor somewhere else came out with the barrel turned 70 degrees.
$what = if ($Menu) { "scene=$Scene" } else { "level=$Level" }
$hold = $Seconds + 20
$arguments = "nosplash mute nofps $what seed=$Seed sweep=0:${hold}:0 shot=$Seconds userdata=`"$profileDir`""

Write-Host "Running: BS3D.exe $arguments"
$process = Start-Process $exe -ArgumentList $arguments -WorkingDirectory $build -PassThru `
    -RedirectStandardOutput $log -RedirectStandardError "$log.err"

try {
    $png = $null
    for ($waited = 0; $waited -lt 120 -and -not $png; $waited++) {
        Start-Sleep -Seconds 1
        if ($process.HasExited) { break }
        if (Test-Path $log) {
            $line = Get-Content $log -ErrorAction SilentlyContinue | Select-String '^\[shot\] (.+\.png)\s*$' | Select-Object -First 1
            if ($line) { $png = $line.Matches[0].Groups[1].Value.Trim() }
        }
    }

    # The line is printed as the file is written: give the write a moment before the game is closed on it
    Start-Sleep -Seconds 2
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
}

if (-not $png) { throw "The run never said [shot]; its log is $log" }
if (-not (Test-Path $png) -or (Get-Item $png).Length -eq 0) { throw "The capture is missing or empty: $png" }

Add-Type -AssemblyName System.Drawing
$image = [System.Drawing.Image]::FromFile($png)
try {
    $encoder = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
    $parameters = New-Object System.Drawing.Imaging.EncoderParameters 1
    $parameters.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), ([long]$Quality)

    New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
    $image.Save($target, $encoder, $parameters)
    "{0}x{1}" -f $image.Width, $image.Height | Write-Host
}
finally {
    $image.Dispose()
}

"{0}: {1:N2} MB" -f $target, ((Get-Item $target).Length / 1MB) | Write-Host
Write-Host "Look at it, commit it, then tag."
