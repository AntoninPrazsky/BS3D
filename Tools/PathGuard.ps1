<#
.SYNOPSIS
Refuses a path argument that a shell has mangled, so the script fails loudly instead of creating a folder
(#657). Dot-source it: . "$PSScriptRoot\..\..\..\Tools\PathGuard.ps1"

.DESCRIPTION
An unquoted Windows path typed into bash loses its backslashes: `-Out C:\Users\panrd\AI\sd\out\622` reaches
the script as `C:UserspanrdAIsdout622`. That is a *drive-relative* path - the drive C, then a name relative to
the current directory on it - and every cmdlet accepts it. `New-Item` made a folder called
`UserspanrdAIsdout622` in whatever directory the agent's shell stood in, which was the repository's root, and
the script then failed a step later because the render went nowhere. Three empty folders (`...622`,
`...640-klein`, `...640-zimage`) were left in the checkout.

Two shapes are refused, both by the exact mistake rather than by guessing what the user meant:

  * a drive letter and a colon that is NOT followed by a separator (`C:foo`, `C:Users\x`). No script here
    means it - a drive-relative path is only ever what a mangled one looks like - so every path argument
    gets this check. `[IO.Path]::IsPathRooted('C:foo')` is True, which is why "is it rooted?" alone would not
    have caught the incident.
  * with -Full, anything that is not fully qualified: not `C:\` or `C:/`, and not a UNC `\\server\share`.
    For arguments that a script creates or writes under and whose documented use is a full path (an output
    folder, a worktree root). Arguments whose documented use includes a relative name (`-Out shot.png`,
    `-Image before.png`) are checked without it.

From bash, quote a Windows path ('C:\Users\...') or write it with forward slashes (C:/Users/...).
#>

function Assert-SanePath {
    [CmdletBinding()]
    param(
        # The parameter's own name, for the message
        [Parameter(Mandatory)][string]$Name,

        # Its value. Null or empty passes: whether an argument is required is the script's own business.
        [AllowNull()][AllowEmptyString()][string]$Value,

        # Also require a fully qualified path - see the description
        [switch]$Full
    )

    if ([string]::IsNullOrEmpty($Value)) { return }

    $hint = "From bash, quote a Windows path ('C:\...') or use forward slashes (C:/...): an unquoted one loses its backslashes."

    if ($Value -match '^[A-Za-z]:(?![\\/])') {
        throw "-$Name '$Value' is a drive-relative path (a drive letter with no separator after it) - a Windows path whose backslashes a shell ate. $hint"
    }

    if ($Full -and $Value -notmatch '^([A-Za-z]:[\\/]|\\\\|//)') {
        throw "-$Name '$Value' must be a full path (C:\... or C:/... or \\server\share). $hint"
    }
}
