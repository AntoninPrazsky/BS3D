# Ask the local vision model in LM Studio (Gemma 4) about one image, or about what changed between two.
#
#   .\vision.ps1 -Image before.png -Image2 after.png -Question "The two images are two frames from the same game, taken a moment apart. List the visible differences between them, most important first, as at most five short bullet points. If they look identical, say so."
#   .\vision.ps1 -Image frame.png -Crop 256 -Question "What colour is the crosshair in the centre? One word."
#   .\vision.ps1 -Image frame.png -Rect "30,360,220,170,4" -Question "..."
#
# What it has been measured good and bad at is in SKILL.md, and it matters more than the mechanics. Short form:
# it describes what changed between two frames and did not invent a difference between identical ones; it loses
# small detail in a whole frame, so crop to the part in question; it cannot judge whether a level's shape reads;
# and exact values (a colour, a size) are pixel measurements, not questions for a model.
#
# ASCII only: PowerShell 5.1 reads a script without a BOM as ANSI, and one em-dash breaks the parse.

param(
    [Parameter(Mandatory = $true)][string]$Image,
    [string]$Image2,
    [Parameter(Mandatory = $true)][string]$Question,

    # >0: a square of that many pixels round the centre; <0: the whole image scaled so its longer side is -Crop;
    # 0: as it is. The default keeps a 1600x900 or 4K capture well inside what the model takes.
    [int]$Crop = -1280,

    # "x,y,w,h,scale": a rectangle of the frame enlarged by scale, overriding -Crop
    [string]$Rect,

    # Let the model reason before it answers. Measured ~20x slower (1.5 to 5 minutes an image) and no better on
    # these tasks, with a max_tokens it can use up entirely and then answer nothing.
    [switch]$Think,

    # Load the model at the context it works at if it is not loaded already
    [switch]$Load,

    [string]$Model = 'google/gemma-4-12b',
    [string]$Endpoint = 'http://localhost:1234'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Test-Loaded {
    try { $m = Invoke-RestMethod -Uri "$Endpoint/api/v0/models" -TimeoutSec 10 }
    catch { throw "LM Studio is not answering at $Endpoint. This skill applies only on the desktop that runs LM Studio; start it there." }
    return @($m.data | Where-Object { $_.id -eq $Model -and $_.state -eq 'loaded' }).Count -gt 0
}

$lms = Join-Path $env:USERPROFILE '.lmstudio\bin\lms.exe'

if (-not (Test-Loaded)) {
    if (-not $Load) { throw "$Model is not loaded. Rerun with -Load, or: & '$lms' load $Model -c 8192 --ttl 1800 -y" }

    # 8k and not more: loaded at 16k, a full-size frame took the model down ("terminated", then "Model is unloaded")
    & $lms load $Model -c 8192 --ttl 1800 -y | Out-Null
    if (-not (Test-Loaded)) { throw "$Model did not load." }
}

function Get-ImagePart([string]$path) {
    $bmp = New-Object System.Drawing.Bitmap (Resolve-Path $path).Path
    try {
        $use = $bmp
        if ($Rect) {
            $v = @($Rect.Split(',') | ForEach-Object { [int]$_ })
            $part = $bmp.Clone((New-Object System.Drawing.Rectangle $v[0], $v[1], $v[2], $v[3]), $bmp.PixelFormat)
            $use = New-Object System.Drawing.Bitmap $part, (New-Object System.Drawing.Size ($v[2] * $v[4]), ($v[3] * $v[4]))
            $part.Dispose()
        }
        elseif ($Crop -gt 0) {
            $x = [int]($bmp.Width / 2 - $Crop / 2)
            $y = [int]($bmp.Height / 2 - $Crop / 2)
            $use = $bmp.Clone((New-Object System.Drawing.Rectangle $x, $y, $Crop, $Crop), $bmp.PixelFormat)
        }
        elseif ($Crop -lt 0) {
            $s = (-$Crop) / [double][Math]::Max($bmp.Width, $bmp.Height)
            $use = New-Object System.Drawing.Bitmap $bmp, (New-Object System.Drawing.Size ([int]($bmp.Width * $s)), ([int]($bmp.Height * $s)))
        }

        $ms = New-Object System.IO.MemoryStream
        $use.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        if (-not [object]::ReferenceEquals($use, $bmp)) { $use.Dispose() }
        return @{ type = 'image_url'; image_url = @{ url = 'data:image/png;base64,' + [Convert]::ToBase64String($ms.ToArray()) } }
    }
    finally { $bmp.Dispose() }
}

$content = @(@{ type = 'text'; text = $Question }, (Get-ImagePart $Image))
if ($Image2) { $content += Get-ImagePart $Image2 }

$body = @{ model = $Model; temperature = 0; max_tokens = 2000; messages = @(@{ role = 'user'; content = $content }) }

# "none" is what turns Gemma's thinking off in LM Studio; chat_template_kwargs and reasoning.effort did nothing
if (-not $Think) { $body.reasoning_effort = 'none' }

$json = $body | ConvertTo-Json -Depth 10 -Compress
$sw = [Diagnostics.Stopwatch]::StartNew()

try {
    $r = Invoke-WebRequest -Uri "$Endpoint/v1/chat/completions" -Method Post -UseBasicParsing -TimeoutSec 900 `
        -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($json))
}
catch {
    $detail = $_.ErrorDetails.Message
    if (-not $detail) { $detail = $_.Exception.Message }
    throw "LM Studio refused the request: $detail`nIf the model fell over (terminated / unloaded / ErrorDeviceLost), LM Studio may reload it at 65k context on its own. Unload it and rerun with -Load: & '$lms' unload $Model"
}

# Decoded by hand: PowerShell 5.1 reads a response without a charset as Latin-1
$answer = ([Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray()) | ConvertFrom-Json).choices[0].message.content
"{0}   [{1} ms]" -f $answer.Trim(), $sw.ElapsedMilliseconds
