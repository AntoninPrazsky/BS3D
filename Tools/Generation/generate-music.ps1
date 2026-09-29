<#
.SYNOPSIS
  Generate a music/audio clip locally with ACE-Step (acestep.cpp), direct binary
  calls on the AMD RX 6900 XT via Vulkan -- no ComfyUI involved.

.NOTES
  The model's VAE decodes at a fixed 48000 Hz -- that is architectural, not a
  setting. "wav32" gives the highest sample PRECISION (32-bit float) this engine
  can produce, not a higher sample RATE; there is no 192kHz mode here.

.EXAMPLE
  .\generate-music.ps1 -Caption "Energetic arcade puzzle game music, synth and marimba" -Duration 15 -Out C:\Temp\track.mp3

.EXAMPLE
  .\generate-music.ps1 -Caption "Warm lounge pads, light bass" -NegativePrompt "heavy bass, booming sub-bass" `
    -Instrumental -Duration 120 -Bpm 100 -OutputFormat wav32 -Loop -Out C:\Temp\menu-loop.wav

.NOTES
  -Loop cuts a whole-bar loop out of the render's body (loop_crossfade.py) and keeps the untouched render
  beside it as <name>.raw.wav, so the loop can be recut later. Durations past ~150s crash ace-synth on a
  16 GB card: the tiled VAE decode needs a sixth tile at 199s.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Caption,

    [string]$Lyrics = "",
    [switch]$Instrumental,
    [string]$NegativePrompt = "",
    # Feed the caption to the DiT as written, instead of the LM's rewritten ("enriched") version.
    [switch]$NoCotCaption,
    [double]$Duration = 15.0,
    [int]$Steps = 8,
    [string]$VocalLanguage = "",
    [int]$Bpm = 0,
    [string]$KeyScale = "",
    [int]$Seed = -1,

    [ValidateSet("mp3", "wav16", "wav24", "wav32")]
    [string]$OutputFormat = "mp3",

    [switch]$Loop,

    [string]$Out = "$AiRoot\output\track.mp3"
)

$ErrorActionPreference = "Stop"

# The engine, its models and the outputs live outside the repository: BS3D_AI_ROOT names the folder holding
# ComfyUI\ and output\ (the scripts themselves, this one and loop_crossfade.py, are found beside it in the repo).
$AiRoot    = if ($env:BS3D_AI_ROOT) { $env:BS3D_AI_ROOT } else { $PSScriptRoot }
$ModelsDir = "$AiRoot\ComfyUI\models\text_encoders"
$BuildDir  = "$AiRoot\ComfyUI\custom_nodes\acestep-cpp-comfyui\acestep.cpp\build"
$AceLm     = "$BuildDir\ace-lm.exe"
$AceSynth  = "$BuildDir\ace-synth.exe"
$Python    = "$AiRoot\ComfyUI\venv\Scripts\python.exe"
$LoopScript = "$PSScriptRoot\loop_crossfade.py"

if (-not (Test-Path $AceLm))    { throw "ace-lm.exe not found at $AceLm" }
if (-not (Test-Path $AceSynth)) { throw "ace-synth.exe not found at $AceSynth" }
if ($Loop -and $OutputFormat -notin @("wav16", "wav32")) { throw "-Loop needs -OutputFormat wav16 or wav32 (it cuts raw PCM)" }

if ($Instrumental -and -not $Lyrics) { $Lyrics = "[Instrumental]" }

$work = Join-Path $env:TEMP ("acestep-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $work -Force | Out-Null

$request = [ordered]@{
    caption     = $Caption
    lyrics      = $Lyrics
    duration    = $Duration
    inference_steps = $Steps
    lm_model    = "acestep-5Hz-lm-4B-Q8_0.gguf"
    seed        = $Seed
    output_format = $OutputFormat
}
if ($NegativePrompt) { $request["lm_negative_prompt"] = $NegativePrompt }
if ($NoCotCaption)   { $request["use_cot_caption"] = $false }
if ($VocalLanguage)  { $request["vocal_language"] = $VocalLanguage }
if ($Bpm -gt 0)       { $request["bpm"] = $Bpm }
if ($KeyScale)        { $request["keyscale"] = $KeyScale }

$requestPath = Join-Path $work "request.json"
$requestJson = $request | ConvertTo-Json
# Set-Content -Encoding utf8 writes a BOM in Windows PowerShell 5.1, which
# breaks acestep.cpp's JSON parser -- write UTF-8 without BOM directly.
[System.IO.File]::WriteAllText($requestPath, $requestJson, (New-Object System.Text.UTF8Encoding $false))

Write-Host "[1/2] ace-lm: caption enrichment + planning ..." -ForegroundColor Cyan
$lmStart = Get-Date
& $AceLm --models $ModelsDir --request $requestPath
if ($LASTEXITCODE -ne 0) { throw "ace-lm failed (exit $LASTEXITCODE)" }
$lmElapsed = (Get-Date) - $lmStart

$request0 = Join-Path $work "request0.json"
if (-not (Test-Path $request0)) { throw "ace-lm did not produce request0.json" }

Write-Host "[2/2] ace-synth: DiT + VAE synthesis (Vulkan) ..." -ForegroundColor Cyan
$synthStart = Get-Date
& $AceSynth --models $ModelsDir --request $request0
if ($LASTEXITCODE -ne 0) { throw "ace-synth failed (exit $LASTEXITCODE)" }
$synthElapsed = (Get-Date) - $synthStart

$ext = if ($OutputFormat -eq "mp3") { ".mp3" } else { ".wav" }
$produced = Join-Path $work "request00$ext"
if (-not (Test-Path $produced)) { throw "ace-synth did not produce request00$ext" }

New-Item -ItemType Directory -Path (Split-Path $Out) -Force -ErrorAction SilentlyContinue | Out-Null

if ($Loop) {
    Write-Host "[loop] Cutting a whole-bar loop out of the render's body ..." -ForegroundColor Cyan
    $raw = [System.IO.Path]::ChangeExtension($Out, ".raw.wav")
    Copy-Item $produced $raw -Force
    & $Python $LoopScript $raw $Out $Bpm
    if ($LASTEXITCODE -ne 0) { throw "loop_crossfade.py failed (exit $LASTEXITCODE)" }
} else {
    Copy-Item $produced $Out -Force
}
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Done: $Out" -ForegroundColor Green
Write-Host ("  ace-lm:    {0:N1}s" -f $lmElapsed.TotalSeconds)
Write-Host ("  ace-synth: {0:N1}s" -f $synthElapsed.TotalSeconds)
