<#
.SYNOPSIS
    Builds the latest main of BS3D and starts the Game, without touching the folder you work in.

.DESCRIPTION
    Fetches origin, puts a second checkout of the repository (a git worktree named BS3D-play, beside this one) on the
    newest commit of main, builds the Game there in Release - the configuration players get - and starts it. Your own
    working folder keeps its branch, its changes and its build: nothing here switches a branch, resets or cleans.

    The first run copies the repository (a few hundred megabytes, mostly the music) and builds everything, a few
    minutes; the runs after it check out the new commit and build only what changed. The build says which commit it is
    in the main menu's and About's corner, as dev-<sha>.

    The Game runs on YOUR save and settings (%LOCALAPPDATA%\BS3D), because the point is to play it. For a scripted run
    that must not touch them, pass userdata=<folder> to the exe yourself.

    BS3D-play is a folder for this script alone. Do not edit it: a run refuses a folder with changes in it, and
    deleting it is harmless (git worktree prune, then the next run makes it again).

.PARAMETER Ref
    What to build: a branch, a tag or a commit. Default origin/main. A branch of the other machine is origin/<name>.

.PARAMETER NoFetch
    Do not ask GitHub first; build what origin/main was the last time anything fetched.

.PARAMETER NoRun
    Build and stop, without starting the Game.

.NOTES
    A stock Windows refuses to load any .ps1 ("running scripts is disabled on this system"). Run it through
    play-latest.cmd beside it, which lifts the policy for that one run and changes nothing on the machine, or pass
    -ExecutionPolicy Bypass to powershell yourself.

.EXAMPLE
    Tools\play-latest.cmd

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File Tools\play-latest.ps1 -Ref origin/<branch> -NoRun
#>
param(
    [string]$Ref = 'origin/main',
    [switch]$NoFetch,
    [switch]$NoRun
)

$ErrorActionPreference = 'Stop'

function Fail([string]$message) {
    Write-Host ''
    Write-Host ('play-latest: ' + $message) -ForegroundColor Red
    exit 1
}

# A native command's exit code is the only thing PowerShell 5.1 reports for it, so every call is followed by this
function Check([string]$what) {
    if ($LASTEXITCODE -ne 0) { Fail ($what + ' failed (exit code ' + $LASTEXITCODE + ').') }
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) { Fail 'git is not on the PATH.' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Fail 'dotnet is not on the PATH (the .NET SDK is needed to build).' }

# This script's own checkout, whichever folder it is run from; the play folder is its sibling
$repo = (git -C $PSScriptRoot rev-parse --show-toplevel) -replace '/', '\'
Check 'git rev-parse'
$play = Join-Path (Split-Path -Parent $repo) 'BS3D-play'

if (-not $NoFetch) {
    Write-Host 'Fetching origin ...'
    git -C $repo fetch --prune origin
    Check 'git fetch'
}

$sha = git -C $repo rev-parse --verify ($Ref + '^{commit}')
Check ("resolving '" + $Ref + "'")
$short = git -C $repo rev-parse --short $sha

# The Game must not be running from the folder it is about to be rebuilt in: the exe would be locked
$running = Get-Process -Name BS3D -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($play, [System.StringComparison]::OrdinalIgnoreCase) }
if ($running) { Fail 'The Game from BS3D-play is still running. Close it and run this again.' }

$registered = @(git -C $repo worktree list --porcelain | Where-Object { $_ -like 'worktree *' } | ForEach-Object { $_.Substring(9) -replace '/', '\' })

if ($registered -contains $play -and -not (Test-Path $play)) {
    git -C $repo worktree prune
    Check 'git worktree prune'
    $registered = @($registered | Where-Object { $_ -ne $play })
}

if ($registered -contains $play) {
    $changes = git -C $play status --porcelain
    Check 'git status in BS3D-play'
    if ($changes) { Fail ('BS3D-play has changes in it (' + $play + '). It is only for this script: put them somewhere else, or delete the folder and run this again.') }

    $now = git -C $play rev-parse HEAD
    if ($now -ne $sha) {
        Write-Host ('Moving BS3D-play to ' + $short + ' ...')
        git -C $play checkout --detach --quiet $sha
        Check 'git checkout in BS3D-play'
    }
}
elseif (Test-Path $play) {
    Fail ($play + ' exists but is not a worktree of this repository. Rename or delete it and run this again.')
}
else {
    Write-Host ('Making the second checkout in ' + $play + ' (the first time only) ...')
    git -C $repo worktree add --detach $play $sha
    Check 'git worktree add'
}

# The content build shells out to `dotnet mgcb`, which a project finds through its own tool manifest
foreach ($project in @('Game', 'BS3DLibs\Prazsky.Shaders')) {
    Push-Location (Join-Path $play $project)
    try {
        dotnet tool restore | Out-Null
        Check 'dotnet tool restore'
    }
    finally { Pop-Location }
}

Write-Host ('Building ' + $short + ' in Release ...')
$started = Get-Date
dotnet build (Join-Path $play 'Game\Game.csproj') -c Release -v q -nologo
Check 'The build'
Write-Host ('Built in ' + [int]((Get-Date) - $started).TotalSeconds + ' s.')

$exe = Join-Path $play 'Game\bin\Release\net10.0-windows\BS3D.exe'
if (-not (Test-Path $exe)) { Fail ('The build said it succeeded, and ' + $exe + ' is not there.') }

Write-Host ''
Write-Host ('BS3D ' + $short + ': ' + $exe) -ForegroundColor Green

if ($NoRun) { exit 0 }

Write-Host 'Starting the Game.'
Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe)
