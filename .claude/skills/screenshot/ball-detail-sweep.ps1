# Photograph every ball style at the menu's close pass and at the play view, for ball-detail.py (#1216): does a ball
# keep the same surface detail at every size the game draws it at?
#
#   .\ball-detail-sweep.ps1 -OutDir C:\scratch\detail                    # ten styles, R 69 and R 25, the cue off
#   .\ball-detail-sweep.ps1 -OutDir C:\scratch\detail-cue -Cue motif     # the cue on, as a player sees it
#   .\ball-detail-sweep.ps1 -OutDir C:\scratch\detail-ssaa1 -Ssaa 1      # Medium and Low (and the phone)
#   .\ball-detail-sweep.ps1 -OutDir C:\scratch\ladder -Distances 10,14,20,28,40,56,80
#   python ball-detail.py C:\scratch\detail --check 0.8,1.1
#
# One Testbed run a capture: Testbed\Maps\Thirteen_Colors.json, the meadow under dome 1, clear weather, nopost (no
# grain, no aberration), nooverc, clockstill, 1920x1080, fov 42.86, the camera at (0, 4.5, z) looking along -Z at the
# map's upper row, F5 on the first frame so every run hangs the balls in the same pose. A ball is half a unit in
# radius, so z 10 draws it at R 69 display pixels and z 28 at R 25: the sizes of the menu's close pass (R 60-90) and
# of the play view (R 17-31) on the desktop's 3840x1600. The band limits read the ball's size in pixels, not the
# distance, so a 1080-row capture at those sizes is the same test. ball-detail.py knows this camera; change it here
# and the centres it predicts are wrong.
#
# Testbed only: it keeps no player profile, and this refuses any other exe. A capture already in -OutDir is not taken
# again, so an interrupted sweep resumes. Each run takes 13 to 20 s on the desktop. The window opens without the
# focus (Tools\quiet-capture.ps1).
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\..\..\Testbed\bin\net10.0-windows\Testbed.exe'),
    [Parameter(Mandatory)][string]$OutDir,
    [string[]]$Styles = @('beach', 'bubble', 'marble', 'wool', 'metal', 'ice', 'gem', 'plasma', 'lava', 'porcelain'),
    [double[]]$Distances = @(10, 28),
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

foreach ($style in $Styles) {
    foreach ($z in $Distances) {
        $tag = '{0}-{1}-z{2:000}' -f $style, ($Cue -replace ':', ''), $z
        if (Test-Path (Join-Path $OutDir "$tag-1.png")) { continue }
        $zs = $z.ToString([Globalization.CultureInfo]::InvariantCulture)
        $arguments = "`"$map`" scene=meadow sky=1 campos=0,4.5,$zs camtarget=0,4.5,0 fov=42.86 balls=$style " +
            "colourcue=$Cue nopost nooverc weather=clear ssaa=$Ssaa clockstill width=1920 height=1080 sceneseed=0 " +
            "at=0:F5 at=0:F12 shot=3.5 at=5:Escape"
        & $quiet -Exe $Exe -Arguments $arguments -Shots 1 -Timeout $Timeout -OutDir $OutDir -Tag $tag `
            -StdOut (Join-Path $OutDir "$tag.log")
    }
}
