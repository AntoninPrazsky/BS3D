<#
.SYNOPSIS
Exercises Tools/PathGuard.ps1 in both directions: `powershell -File Tools\PathGuard.test.ps1` (#657).

Exits 1 when any case answers the wrong way. Both halves matter, for the reason BestPractices.md section 10
gives: a guard is evidence only once its refusing branch has been seen to fire on the real input, and a guard
that refuses a documented use (`-Out shot.png`) is one somebody switches off. The real input is the string a
shell actually hands over, which is `C:UserspanrdAIsdout622` - the first case below is that, verbatim.
#>
. "$PSScriptRoot\PathGuard.ps1"

$fail = 0

function Expect([string]$want, [string]$label, [string]$value, [switch]$full) {
    $got = 'allow'
    try {
        if ($full) { Assert-SanePath -Name 'Out' -Value $value -Full } else { Assert-SanePath -Name 'Out' -Value $value }
    } catch { $got = 'REFUSE' }

    if ($got -eq $want) { '  ok    {0,-7} {1}' -f $got, $label }
    else { '  WRONG {0,-7} {1} (wanted {2})' -f $got, $label, $want; $script:fail = 1 }
}

'must REFUSE:'
Expect REFUSE 'the incident, verbatim'        'C:UserspanrdAIsdout622'
Expect REFUSE 'drive-relative with a folder'  'D:Users\x\out'
Expect REFUSE 'drive-relative, lower case'    'c:out'
Expect REFUSE 'the incident, -Full'           'C:UserspanrdAIsdout640-klein' -full
Expect REFUSE 'bare name, -Full'              'UserspanrdAIsdout622' -full
Expect REFUSE 'relative folder, -Full'        'out\622' -full
Expect REFUSE 'root of the current drive'     '\out\622' -full
Expect REFUSE 'forward-slash root'            '/out/622' -full

'must ALLOW:'
Expect allow  'backslash path'                'C:\Users\panrd\AI\sd\out\622'
Expect allow  'forward-slash path'            'C:/Users/panrd/AI/sd/out/622'
Expect allow  'UNC path'                      '\\server\share\out'
Expect allow  'backslash path, -Full'         'C:\Users\panrd\AI\sd\out\622' -full
Expect allow  'forward-slash path, -Full'     'C:/Users/panrd/AI/sd/out/622' -full
Expect allow  'UNC path, -Full'               '\\server\share\out' -full
Expect allow  'lower-case drive, -Full'       'c:\out' -full
Expect allow  'bare file name (documented)'   'shot.png'
Expect allow  'relative path (documented)'    'out\shot.png'
Expect allow  'dot-relative'                  '.\shot.png'
Expect allow  'empty'                         ''
Expect allow  'empty, -Full'                  '' -full

if ($fail) { 'FAILED'; exit 1 }
'PathGuard: all cases answer as they should'
exit 0
