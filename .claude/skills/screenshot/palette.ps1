# Measure the thirteen ball colours out of a Thirteen_Colors capture and print CIEDE2000 for every pair.
#
# "This colour is too close to that one" is a recurring issue here (#152, #246, #294, and #285/#286 are open
# as this is written), and it is answerable with numbers rather than opinions. The capture the defaults below
# expect, from this folder:
#
#   .\screenshot.ps1 -Out palette.png -Keys @('F5','F12') -Wait 8 `
#       -GameArgs @('C:\GitHub\Testbed\Maps\Thirteen_Colors.json','scene=meadow','sky=1','nopost','ssaa=2',
#                   'campos=0,4.5,11','camtarget=0,4.5,0')
#   .\palette.ps1 -Png palette.png
#
# F5 stops the simulation so the row hangs still; the bottom row is the enum's own order, which is what -Xs
# indexes. Change the camera and the sample points move: -Xs and -RowY are there for that.
#
# Three things this has already been wrong about, all paid for:
#
#   - MEASURE UNDER A BRIGHT DOME AND A DARK ONE. A ball rides its dome's light by its own amount: olive
#     came out 59 luminance under dome 1 and 69 under dome 13 from the same tint, and the two domes disagree
#     about which of its pairs is the tightest. Tuning against one alone walks the colour into the other's
#     confusion (#294).
#   - dE76 IS THE WRONG INSTRUMENT and will mislead you: it scores a pure lightness gap the same as a hue
#     gap, and ranked black/navy ninth while three pairs nobody has ever complained about measured tighter
#     (#246). Everything here is CIEDE2000.
#   - THE BALLS PULSE. They are emissive on a heartbeat and the phase differs between runs, which is about
#     +-0.4 dE of noise on any single capture. Shoot twice before believing a small delta.
#
# The balls are beach-ball patterned: coloured gores alternating with white ones, plus a specular highlight.
# Sampling the whole disc would measure the pattern rather than the colour, so each ball is reduced to the
# MEDIAN of its own coloured gores - pixels inside the disc with the brightest third dropped - which is the
# tint as the eye reads it at a glance.
param(
    [Parameter(Mandatory=$true)][string]$Png,
    [int]$RowY = 450,
    [int]$Radius = 20,
    [int[]]$Xs = @(443,500,558,615,672,728,785,840,895,950,1005,1057,1110),
    [string[]]$Focus = @()
)

Add-Type -AssemblyName System.Drawing
$names = @('red','green','blue','white','cyan','magenta','yellow','black','orange','brown','silver','navy','olive')
$bmp = [System.Drawing.Bitmap]::FromFile((Resolve-Path $Png))

function Get-BallColor([System.Drawing.Bitmap]$b, [int]$cx, [int]$cy, [int]$r) {
    $pix = New-Object System.Collections.ArrayList
    for ($dy = -$r; $dy -le $r; $dy++) {
        for ($dx = -$r; $dx -le $r; $dx++) {
            if ($dx*$dx + $dy*$dy -gt $r*$r) { continue }
            $x = $cx + $dx; $y = $cy + $dy
            if ($x -lt 0 -or $y -lt 0 -or $x -ge $b.Width -or $y -ge $b.Height) { continue }
            $c = $b.GetPixel($x, $y)
            $lum = 0.2126*$c.R + 0.7152*$c.G + 0.0722*$c.B
            [void]$pix.Add(@($lum, $c.R, $c.G, $c.B))
        }
    }
    $sorted = $pix | Sort-Object { $_[0] }
    $keep = [int]($sorted.Count * 2 / 3)          # drop the brightest third: white gores + highlight
    $sel = $sorted[0..($keep-1)]
    $mid = [int]($sel.Count / 2)
    $r0 = ($sel | ForEach-Object { $_[1] } | Sort-Object)[$mid]
    $g0 = ($sel | ForEach-Object { $_[2] } | Sort-Object)[$mid]
    $b0 = ($sel | ForEach-Object { $_[3] } | Sort-Object)[$mid]
    $out = @([double]$r0, [double]$g0, [double]$b0); return ,$out
}

function To-Lab([double[]]$rgb) {
    $lin = @(0.0,0.0,0.0)
    for ($i = 0; $i -lt 3; $i++) {
        $v = $rgb[$i] / 255.0
        if ($v -le 0.04045) { $lin[$i] = $v / 12.92 } else { $lin[$i] = [Math]::Pow(($v + 0.055) / 1.055, 2.4) }
    }
    $X = 0.4124*$lin[0] + 0.3576*$lin[1] + 0.1805*$lin[2]
    $Y = 0.2126*$lin[0] + 0.7152*$lin[1] + 0.0722*$lin[2]
    $Z = 0.0193*$lin[0] + 0.1192*$lin[1] + 0.9505*$lin[2]
    $ref = @(0.95047, 1.0, 1.08883)
    $f = @(0.0,0.0,0.0); $xyz = @($X,$Y,$Z)
    for ($i = 0; $i -lt 3; $i++) {
        $t = $xyz[$i] / $ref[$i]
        if ($t -gt 0.008856) { $f[$i] = [Math]::Pow($t, 1.0/3.0) } else { $f[$i] = (7.787 * $t) + (16.0/116.0) }
    }
    $L = 116.0*$f[1] - 16.0; $A = 500.0*($f[0]-$f[1]); $B = 200.0*($f[1]-$f[2]); $out = @($L,$A,$B); return ,$out
}

function DE2000([double[]]$lab1, [double[]]$lab2) {
    $kL = 1.0; $kC = 1.0; $kH = 1.0
    $L1 = $lab1[0]; $a1 = $lab1[1]; $b1 = $lab1[2]; $L2 = $lab2[0]; $a2 = $lab2[1]; $b2 = $lab2[2]
    $C1 = [Math]::Sqrt($a1*$a1 + $b1*$b1); $C2 = [Math]::Sqrt($a2*$a2 + $b2*$b2)
    $Cb = ($C1 + $C2) / 2.0
    $G = 0.5 * (1 - [Math]::Sqrt([Math]::Pow($Cb,7) / ([Math]::Pow($Cb,7) + [Math]::Pow(25.0,7))))
    $a1p = (1 + $G) * $a1; $a2p = (1 + $G) * $a2
    $C1p = [Math]::Sqrt($a1p*$a1p + $b1*$b1); $C2p = [Math]::Sqrt($a2p*$a2p + $b2*$b2)
    $h1p = 0.0; if (-not ($b1 -eq 0 -and $a1p -eq 0)) { $h1p = ((([Math]::Atan2($b1,$a1p) * 180 / [Math]::PI) + 360) % 360) }
    $h2p = 0.0; if (-not ($b2 -eq 0 -and $a2p -eq 0)) { $h2p = ((([Math]::Atan2($b2,$a2p) * 180 / [Math]::PI) + 360) % 360) }
    $dLp = $L2 - $L1; $dCp = $C2p - $C1p
    $dhp = 0.0
    if ($C1p * $C2p -ne 0) {
        $diff = $h2p - $h1p
        if ([Math]::Abs($diff) -le 180) { $dhp = $diff }
        elseif ($diff -gt 180) { $dhp = $diff - 360 }
        else { $dhp = $diff + 360 }
    }
    $dHp = 2 * [Math]::Sqrt($C1p * $C2p) * [Math]::Sin(($dhp / 2) * [Math]::PI / 180)
    $Lbp = ($L1 + $L2) / 2.0; $Cbp = ($C1p + $C2p) / 2.0
    $hbp = 0.0
    if ($C1p * $C2p -ne 0) {
        $sum = $h1p + $h2p; $absdiff = [Math]::Abs($h1p - $h2p)
        if ($absdiff -le 180) { $hbp = $sum / 2 }
        elseif ($sum -lt 360) { $hbp = ($sum + 360) / 2 }
        else { $hbp = ($sum - 360) / 2 }
    } else { $hbp = $h1p + $h2p }
    $T = 1 - 0.17*[Math]::Cos(($hbp-30)*[Math]::PI/180) + 0.24*[Math]::Cos((2*$hbp)*[Math]::PI/180) `
           + 0.32*[Math]::Cos((3*$hbp+6)*[Math]::PI/180) - 0.20*[Math]::Cos((4*$hbp-63)*[Math]::PI/180)
    $dTheta = 30 * [Math]::Exp(-[Math]::Pow(($hbp-275)/25.0, 2))
    $Rc = 2 * [Math]::Sqrt([Math]::Pow($Cbp,7) / ([Math]::Pow($Cbp,7) + [Math]::Pow(25.0,7)))
    $Sl = 1 + (0.015 * [Math]::Pow($Lbp-50,2)) / [Math]::Sqrt(20 + [Math]::Pow($Lbp-50,2))
    $Sc = 1 + 0.045 * $Cbp
    $Sh = 1 + 0.015 * $Cbp * $T
    $Rt = -[Math]::Sin((2*$dTheta)*[Math]::PI/180) * $Rc
    return [Math]::Sqrt([Math]::Pow($dLp/($kL*$Sl),2) + [Math]::Pow($dCp/($kC*$Sc),2) + [Math]::Pow($dHp/($kH*$Sh),2) `
        + $Rt * ($dCp/($kC*$Sc)) * ($dHp/($kH*$Sh)))
}

$rgbs = @(); $labs = @()
for ($i = 0; $i -lt 13; $i++) {
    $rgb = Get-BallColor $bmp $Xs[$i] $RowY $Radius
    $rgbs += ,$rgb
    $labs += ,(To-Lab $rgb)
    $lum = 0.2126*$rgb[0] + 0.7152*$rgb[1] + 0.0722*$rgb[2]
    "{0,-8} rgb {1,3} {2,3} {3,3}   lum {4,5:F1}" -f $names[$i], [int]$rgb[0], [int]$rgb[1], [int]$rgb[2], $lum
}
$bmp.Dispose()

"`n--- CIEDE2000, tightest pairs first ---"
$pairs = @()
for ($i = 0; $i -lt 13; $i++) {
    for ($j = $i+1; $j -lt 13; $j++) {
        $pairs += [pscustomobject]@{ pair = "$($names[$i])/$($names[$j])"; dE = [Math]::Round((DE2000 $labs[$i] $labs[$j]), 1) }
    }
}
$pairs | Sort-Object dE | Select-Object -First 14 | Format-Table -AutoSize

# Every pair the colours under discussion are in. -Focus takes their names (the list above), so this answers
# "is THIS palette confusable?" rather than only the one colour a past issue happened to be about: it was
# hardcoded to olive, left behind by #294. Naming several prints one table per colour, in the order given.
#
# The finding that outlives any one issue: MOVING ONE COLOUR ALONE RELOCATES THE CONFUSION RATHER THAN ENDING
# IT, so read the whole list after a change and not just the pair that was complained about.
foreach ($f in $Focus) {
    "`n--- every pair $f is in ---"
    $pairs | Where-Object { $_.pair -like "*$f*" } | Sort-Object dE | Format-Table -AutoSize
}
