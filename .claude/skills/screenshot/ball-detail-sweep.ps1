# Photograph every ball style at the size of the play view and at the size of the menu's close pass, from one place,
# for ball-detail.py (#1216): does a ball keep the same surface detail at every size the game draws it at?
#
#   .\ball-detail-sweep.ps1 -OutDir C:\scratch\detail                    # ten styles, the pair, the cue off
#   .\ball-detail-sweep.ps1 -OutDir C:\scratch\detail-cue -Cue motif     # the cue on, as a player sees it
#   .\ball-detail-sweep.ps1 -OutDir C:\scratch\detail-ssaa1 -Ssaa 1      # Medium and Low (and the phone)
#   .\ball-detail-sweep.ps1 -OutDir C:\scratch\ladder -NoPair -Distances 10,14,20,28,40,56,80
#   python ball-detail.py C:\scratch\detail --check 0.9,1.1
#
# One Testbed run a capture: Testbed\Maps\Thirteen_Colors.json, the meadow under dome 1, clear weather, nopost (no
# grain, no aberration), nooverc, clockstill, 1920x1080, the camera at (0, 4.5, z) looking along -Z at the map's upper
# row, F5 on the first frame so every run hangs the balls in the same pose. A ball is half a unit in radius.
#
# THE PAIR (unless -NoPair), both from z 28, so every ball is seen from exactly the same place and only its size on
# the screen differs: <style>-<cue>-z028 at fov 42.86, R 25 display pixels (the play view on the desktop's
# 3840x1600, R 17-31), and <style>-<cue>-close at fov 15.94, R 69 (the menu's close pass, R 60-90). The band limits
# read the ball's size in pixels, not the distance, so a 1080-row frame at those sizes is the same test. A close-up
# taken by moving the camera in instead sees every ball from another angle (the outer ones 31 degrees off the axis at
# z 10, 12 at z 28), and its glints and reflections move with it.
#
# THE LADDER (-Distances): <style>-<cue>-zNNN at fov 42.86 from each z, for contact sheets (z 10 is R 69, z 80 R 9).
#
# ball-detail.py knows these cameras; change one here and the centres it predicts are wrong. Testbed only: it keeps no
# player profile, and this refuses any other exe. A capture already in -OutDir is not taken again, so an interrupted
# sweep resumes. Each run takes 13 to 20 s on the desktop, and its window opens without the focus
# (Tools\quiet-capture.ps1).
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\..\..\Testbed\bin\net10.0-windows\Testbed.exe'),
    [Parameter(Mandatory)][string]$OutDir,
    [string[]]$Styles = @('beach', 'bubble', 'marble', 'wool', 'metal', 'ice', 'gem', 'plasma', 'lava', 'porcelain'),
    [double[]]$Distances = @(),
    [switch]$NoPair,
    [string]$Cue = 'none',
    [ValidateSet(1, 2)][int]$Ssaa = 2,
    [int]$Timeout = 60
)

$ErrorActionPreference = 'Stop'
$Exe = [IO.Path]::GetFullPath($Exe)
if ([IO.Path]::GetFileName($Exe) -ne 'Testbed.exe') { throw "ball-detail-sweep.ps1 drives Testbed.exe only, not '$Exe'." }
if (-not (Test-Path $Exe)) { throw "No Testbed at '$Exe': build Testbed.sln first, or pass -Exe." }
if (-not [IO.Path]::IsPathRooted($OutDir)) { throw "-OutDir must be an absolute path." }

$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$quiet = Join-Path $repo 'Tools\quiet-capture.ps1'
$map = Join-Path $repo 'Testbed\Maps\Thirteen_Colors.json'
New-Item -ItemType Directory -Force $OutDir | Out-Null
$cueTag = $Cue -replace ':', ''

# Each view: its name in the file, the camera's z and the lens
$views = @()
if (-not $NoPair) {
    $views += @{ name = 'z028'; z = 28; fov = '42.86' }
    $views += @{ name = 'close'; z = 28; fov = '15.94' }
}
foreach ($z in $Distances) {
    $name = 'z{0:000}' -f $z
    if (-not ($views | Where-Object { $_.name -eq $name })) { $views += @{ name = $name; z = $z; fov = '42.86' } }
}

foreach ($style in $Styles) {
    foreach ($v in $views) {
        $tag = '{0}-{1}-{2}' -f $style, $cueTag, $v.name
        if (Test-Path (Join-Path $OutDir "$tag-1.png")) { continue }
        $zs = ([double]$v.z).ToString([Globalization.CultureInfo]::InvariantCulture)
        $arguments = "`"$map`" scene=meadow sky=1 campos=0,4.5,$zs camtarget=0,4.5,0 fov=$($v.fov) balls=$style " +
            "colourcue=$Cue nopost nooverc weather=clear ssaa=$Ssaa clockstill width=1920 height=1080 sceneseed=0 " +
            "at=0:F5 at=0:F12 shot=3.5 at=5:Escape"
        & $quiet -Exe $Exe -Arguments $arguments -Shots 1 -Timeout $Timeout -OutDir $OutDir -Tag $tag `
            -StdOut (Join-Path $OutDir "$tag.log")
    }
}
