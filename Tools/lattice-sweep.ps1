<#
.SYNOPSIS
    Photographs the twenty-one scenes at pinned vantages and runs the lattice probe over the pictures (#674).

.DESCRIPTION
    For every scene and every vantage it launches the Testbed once with `nopost` (the film grain would hide exactly what
    the probe looks for), the overlay off (F12), a camera held still (`campos`/`camtarget`) and the game's own frame saved
    by `shotframe=` — the swap chain's back buffer, so nothing on the desktop can get into it. Then it runs
    `Tools/LatticeProbe` over the folder and prints its table.

    The vantages are chosen to look AWAY from the island and the cluster, whose own regularity is by design and would
    be scored as if it were the scene's:
        out      level, out over the scene's ground or water          campos 0,-4,30   -> 0,-6,200
        sky      up at the sky and whatever stands in it              campos 0,-4,30   -> 0,40,200
        graze    a low, grazing look at the horizon                   campos 0,-9,30   -> 0,-8.8,200
        down     down at the ground beside the arena                  campos 0,10,60   -> 0,-12,58
                 (two units off the vertical: a look straight down is a degenerate look-at against an up of +Y, and
                 the Testbed renders it as one flat field - every scene's "down" scored 0 % on a picture of nothing)
        play     the game's own pose, island, gun and cluster in view  campos 0,-4,30   -> 0,-8,0
                 (NOT in the default set: the cluster's regularity is by design and its tiles will flag)

    A scene reads as "flagged" when a large share of its scored tiles stand over the noise null; see the probe's own
    header for what that does and does not mean, and read the picture before believing the number: the table finds
    candidates (a horizon that got past the edge rule, a designed lattice like the Grid or the neon windows) and does
    not clear anything.

.PARAMETER Out
    Where the pictures and the table go. Full path; created if missing.

.PARAMETER Scenes
    Which scenes, by their command-line spelling. Default: all twenty-one.

.PARAMETER Vantages
    Which of out, sky, graze, down, play. Default: the first four.

.PARAMETER Frame
    The frame (counted from 1) each picture is taken at. Default 240 (about four seconds at the cap): long enough for
    the scene's own build to have settled.

.PARAMETER Seed
    The scene's random roll (`sceneseed=`), pinned so two runs photograph the same world. Default 3.

.PARAMETER Exe
    The Testbed. Default: the Release build next to the repository's `Testbed` project.

.EXAMPLE
    .\Tools\lattice-sweep.ps1 -Out C:\Temp\lattice
    .\Tools\lattice-sweep.ps1 -Out C:\Temp\lattice -Scenes meadow,space -Vantages out,sky
#>
param(
    [Parameter(Mandatory)][string]$Out,
    [string[]]$Scenes = @('city', 'sea', 'savanna', 'desert', 'mountain', 'meadow', 'neon', 'forest', 'space', 'dream',
        'cavern', 'moon', 'outback', 'tropical', 'volcano', 'mars', 'storm', 'polar', 'aurora', 'grid', 'circus'),
    [string[]]$Vantages = @('out', 'sky', 'graze', 'down'),
    [int]$Frame = 240,
    [int]$Seed = 3,
    [string]$Exe = ''
)

# `powershell -File a.ps1 -Scenes x,y` hands the list over as ONE string; a call from inside PowerShell hands over an array
$Scenes = @($Scenes | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$Vantages = @($Vantages | ForEach-Object { $_ -split ',' } | Where-Object { $_ })

. "$PSScriptRoot\PathGuard.ps1"
Assert-SanePath -Name Out -Value $Out -Full
Assert-SanePath -Name Exe -Value $Exe

$repo = Split-Path $PSScriptRoot -Parent
if ($Exe -eq '') { $Exe = Join-Path $repo 'Testbed\bin\Release\net10.0-windows\Testbed.exe' }
if (-not (Test-Path $Exe)) { throw "The Testbed is not built at '$Exe' - run: dotnet build Testbed\Testbed.csproj -c Release" }

$camera = @{
    out   = @('campos=0,-4,30', 'camtarget=0,-6,200')
    sky   = @('campos=0,-4,30', 'camtarget=0,40,200')
    graze = @('campos=0,-9,30', 'camtarget=0,-8.8,200')
    down  = @('campos=0,10,60', 'camtarget=0,-12,58')
    play  = @('campos=0,-4,30', 'camtarget=0,-8,0')
}

New-Item -ItemType Directory -Force -Path $Out | Out-Null
$map = Join-Path $repo 'Testbed\Maps\Full.json'
$shotDir = Join-Path (Split-Path $Exe) 'Screenshots'

foreach ($scene in $Scenes) {
    foreach ($vantage in $Vantages) {
        if (-not $camera.ContainsKey($vantage)) { throw "Unknown vantage '$vantage' (out, sky, graze, down, play)" }

        $stamp = Get-Date
        $arguments = @($map, "scene=$scene", "sceneseed=$Seed") + $camera[$vantage] +
            @('width=1600', 'height=900', 'nopost', 'nooverc', 'fpscap=60', 'at=1:F12', "shotframe=$Frame", 'at=12:Escape')

        $process = Start-Process -FilePath $Exe -ArgumentList $arguments -WorkingDirectory (Split-Path $Exe) -PassThru -WindowStyle Minimized
        if (-not $process.WaitForExit(90000)) { Stop-Process -Id $process.Id -Force }

        $shot = Get-ChildItem $shotDir -Filter '*.png' -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTime -gt $stamp } | Sort-Object LastWriteTime | Select-Object -Last 1

        if ($null -eq $shot) { Write-Warning "No picture from $scene/$vantage"; continue }

        Copy-Item $shot.FullName (Join-Path $Out "$scene-$vantage.png") -Force
        Write-Host "  $scene/$vantage"
    }
}

Write-Host ''
# Out-String then Set-Content: Tee-Object writes UTF-16 in Windows PowerShell 5.1, which nothing else here reads
$table = dotnet run --project (Join-Path $repo 'Tools\LatticeProbe\LatticeProbe.csproj') -c Release -- $Out | Out-String
Write-Host $table
Set-Content -Path (Join-Path $Out 'lattice.txt') -Value $table -Encoding utf8
