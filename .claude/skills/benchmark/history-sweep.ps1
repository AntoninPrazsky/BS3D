# WHEN DID A COST ARRIVE? Builds the Game at evenly spaced first-parent merges between two commits, each in a
# worktree of its own, then measures every build ROUND-ROBIN at one tier - a forward pass and a reversed one - so
# the machine's drift between runs (trap 14) lands on every build alike instead of reading as a step. Prints one
# row per build: each pass's median, the minimum of the two, and the step from the build before.
#
# Narrow a window by running it again with -From/-To at the two builds either side of the step and more -Points;
# three rounds of this took 217 merges to the one that cost the mountain a millisecond at Low (#551, 2026-09-29).
#
#   .\history-sweep.ps1 -From e097a475 -To HEAD -Points 7 -Levels 'Spyglass,Colossus' -Tier low
#
# Levels are ONE comma string, not an array: an array handed through `powershell -File` spills into the
# positional parameters (the trap the game-capture notes record).
#
# ⚠ An old build has no userdata= and reads the PLAYER'S OWN profile. Nothing here clears a level or changes a
# setting, but the save's two files are hashed before and after and a difference is reported loudly.
param(
    [Parameter(Mandatory)][string]$From,
    [string]$To = 'HEAD',
    [int]$Points = 7,
    [Parameter(Mandatory)][string]$Levels,
    [string]$Tier = 'low',
    [int]$Seconds = 40,
    [string]$OutDir = "$env:TEMP\bs3d-history-sweep",
    [string]$WorktreeRoot = 'C:\bs3d-sweep',
    [switch]$Keep
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..\..").Path
$tm = Join-Path $PSScriptRoot 'tier-matrix.ps1'
$levelList = $Levels.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }

# The builds: both ends, and Points merges evenly spaced between them
$merges = @(git -C $repo rev-list --first-parent --merges --reverse "$From..$To")
$picked = @()
for ($k = 1; $k -le $Points; $k++) {
    $i = [math]::Round($merges.Count * $k / ($Points + 1)) - 1
    if ($i -ge 0 -and $i -lt $merges.Count) { $picked += $merges[$i] }
}
$commits = @((git -C $repo rev-parse --short $From)) + @($picked | ForEach-Object { git -C $repo rev-parse --short $_ }) + @((git -C $repo rev-parse --short $To))
$commits = $commits | Select-Object -Unique

$save = Join-Path $env:LOCALAPPDATA 'BS3D'
function SaveHashes { Get-ChildItem $save -Filter *.json -ErrorAction SilentlyContinue | ForEach-Object { "$($_.Name)=$((Get-FileHash $_.FullName).Hash)" } }
$before = SaveHashes

New-Item -ItemType Directory -Force $OutDir, $WorktreeRoot | Out-Null
$builds = [ordered]@{}
$worktrees = @()
$n = 0
foreach ($c in $commits) {
    $w = Join-Path $WorktreeRoot "$n-$c"
    if (-not (Test-Path $w)) { git -C $repo worktree add -q --detach $w $c | Out-Null }
    $worktrees += $w
    Write-Output "building $n $c"
    & dotnet build (Join-Path $w 'Game\Game.csproj') -c Release -v q -nologo | Select-String ' error ' | Select-Object -First 3
    $exe = Join-Path $w 'Game\bin\Release\net10.0-windows\BS3D.exe'
    if (Test-Path $exe) { $builds["$n-$c"] = $exe } else { Write-Output "  $c did not build - left out" }
    $n++
}

$names = @($builds.Keys)
for ($pass = 1; $pass -le 2; $pass++) {
    $order = if ($pass -eq 1) { $names } else { $r = $names.Clone(); [array]::Reverse($r); $r }
    foreach ($b in $order) {
        & $tm -Targets ($levelList | ForEach-Object { "level:$_" }) -Tiers @($Tier) -Seconds $Seconds `
            -OutDir (Join-Path $OutDir "$b~$pass") -Exe $builds[$b] | Out-Null
    }
}

function MedianMs($log) {
    $ms = @(Get-Content $log | Where-Object { $_ -match '^\[fps\] [\d,.]+ \(([\d,.]+) ms\)' } |
        ForEach-Object { [double]($Matches[1] -replace ',', '.') } | Select-Object -Skip 6 | Sort-Object)
    if ($ms.Count -eq 0) { return [double]::NaN }
    if ($ms.Count % 2) { $ms[($ms.Count - 1) / 2] } else { ($ms[$ms.Count / 2 - 1] + $ms[$ms.Count / 2]) / 2 }
}

foreach ($lvl in $levelList) {
    Write-Output ''
    Write-Output "== $lvl, $Tier (ms: pass 1, pass 2, min, step)"
    $prev = $null
    foreach ($b in $names) {
        $c = $b.Split('-')[1]
        $m = 1..2 | ForEach-Object {
            $log = Get-ChildItem (Join-Path $OutDir "$b~$_") -Filter "L_${lvl}_$Tier-*.log" -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($log) { MedianMs $log.FullName } else { [double]::NaN }
        }
        $min = ($m | Measure-Object -Minimum).Minimum
        $step = if ($prev -ne $null) { '{0:+0.00;-0.00}' -f ($min - $prev) } else { '' }
        $subject = (git -C $repo log -1 --format='%ad %s' --date=format:'%m-%d %H:%M' $c)
        if ($subject.Length -gt 70) { $subject = $subject.Substring(0, 70) }
        Write-Output ('{0,-9} {1,7:0.00} {2,7:0.00} {3,7:0.00} {4,6}  {5}' -f $c, $m[0], $m[1], $min, $step, $subject)
        $prev = $min
    }
}

$after = SaveHashes
if ((Compare-Object @($before) @($after))) { Write-Output ''; Write-Output "!!! THE PLAYER'S SAVE CHANGED during the sweep - compare $save against a backup" }

if (-not $Keep) {
    foreach ($w in $worktrees) { git -C $repo worktree remove --force $w | Out-Null }
    git -C $repo worktree prune
}
