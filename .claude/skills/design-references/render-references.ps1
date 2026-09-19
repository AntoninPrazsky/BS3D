<#
.SYNOPSIS
Renders design reference images locally: Z-Image-Turbo through stable-diffusion.cpp's sd-server on Vulkan.

.DESCRIPTION
Starts sd-server when nothing listens on -Port (and stops it again when done, unless -KeepServer), renders every
prompt -Count times with consecutive seeds, and writes <name>-<seed>.png beside a <name>-<seed>.txt that carries
the prompt, size, seed and time. Everything goes under -Out, outside the repository by default.

The server flags are the ones measured on #441 (RX 6900 XT, 16 GB): the Q8 weights do not fit on the card,
--offload-to-cpu alone decodes on the CPU (63 s an image), --offload-to-cpu --vae-tiling renders in 33-37 s.

THIS RENDERER TAKES THE DESKTOP DOWN, AND NO CONFIGURATION OF IT HAS AVOIDED THAT. Seven of seven runs
between 2026-09-17 and 2026-09-18 ended in an instant hard reset (Kernel-Power 41, BugcheckCode 0, no WHEA,
no 4101) while ~100 Testbed/Game runs on the same days were clean, uncapped ones included.

The offload theory was wrong and is recorded here so it is not re-run: --offload-to-cpu streams weights over
PCIe every step, so the suspicion was those transients. The test that killed it was Q4_K + the Q8 encoder
with NO offload - auto-fit put all 7922 MB on the card ("VRAM 7921.64MB, RAM 0.00MB") - and the machine went
down within seconds of the first sampling step, having produced nothing, where an offloaded Q8 run had
managed four images. Quantization and offloading change nothing; what reproduces it is sd.cpp's Vulkan
COMPUTE load, which is a different power profile from the rasterizing this project's executables do.

So -DiffusionModel, -Encoder and -Offload exist to express a configuration, not to dodge the fault. Ask the
owner before starting this script at all.

.EXAMPLE
.\render-references.ps1 -Name cup-gold -Width 832 -Height 1216 -Count 3 -Prompt "Studio product photograph of ..."

.EXAMPLE
.\render-references.ps1 -PromptFile C:\Users\panrd\AI\sd\prompts-441.json -Out C:\Users\panrd\AI\sd\out\441
# the file is a JSON array of { "name": "...", "prompt": "...", "w": 1216, "h": 832, "seed": 1 } (w, h, seed optional)
#>
param(
    [string]$Prompt,
    [string]$Name = 'reference',
    [string]$PromptFile,
    [int]$Width = 1216,
    [int]$Height = 832,
    [int]$Seed = -1,
    [int]$Count = 1,
    [int]$Steps = 8,
    [string]$Out,
    [string]$Root = 'C:\Users\panrd\AI\sd',
    [int]$Port = 7860,
    [string]$DiffusionModel = 'z_image_turbo-Q8_0.gguf',
    [string]$Encoder = 'Qwen3-4B-Instruct-2507-Q8_0.gguf',
    [switch]$NoOffload,
    [string[]]$ExtraServerArgs,
    [switch]$KeepServer
)
$ErrorActionPreference = 'Stop'

if (-not $Prompt -and -not $PromptFile) { throw 'Pass -Prompt or -PromptFile.' }
if (-not $Out) { $Out = Join-Path $Root ('out\' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
New-Item -ItemType Directory -Force -Path $Out | Out-Null

$exe = Join-Path $Root 'bin\sd-server.exe'
$models = Join-Path $Root 'models'
# A bare file name is one of the models under $Root\models; an absolute path is taken as given.
function Resolve-Model([string]$p) { if ([IO.Path]::IsPathRooted($p)) { $p } else { Join-Path $models $p } }
$diffusion = Resolve-Model $DiffusionModel
$encoder = Resolve-Model $Encoder
$vae = Join-Path $models 'ae.safetensors'
foreach ($f in @($exe, $diffusion, $encoder, $vae)) {
    if (-not (Test-Path $f)) { throw "Missing $f - see 'Setting it up' in the design-references SKILL.md." }
}

# The work list: one entry per prompt, each rendered -Count times.
$items = @()
if ($PromptFile) {
    foreach ($it in (Get-Content -Raw -Encoding UTF8 $PromptFile | ConvertFrom-Json)) {
        $w = $Width; $h = $Height; $s = $Seed
        if ($it.w) { $w = [int]$it.w }
        if ($it.h) { $h = [int]$it.h }
        if ($null -ne $it.seed) { $s = [int]$it.seed }
        $items += [pscustomobject]@{ Name = $it.name; Prompt = $it.prompt; W = $w; H = $h; Seed = $s }
    }
} else {
    $items += [pscustomobject]@{ Name = $Name; Prompt = $Prompt; W = $Width; H = $Height; Seed = $Seed }
}

function Test-ServerPort([int]$p) {
    $c = New-Object Net.Sockets.TcpClient
    try { $c.Connect('127.0.0.1', $p); return $true } catch { return $false } finally { $c.Close() }
}

# The card is shared: say what else holds it before taking ~10.5 GB of it.
try {
    $loaded = @((Invoke-RestMethod -Uri 'http://localhost:1234/api/v0/models' -TimeoutSec 3).data |
        Where-Object { $_.state -eq 'loaded' -and $_.type -ne 'embeddings' })
    if ($loaded.Count) {
        Write-Warning ("LM Studio has loaded: " + (($loaded | ForEach-Object { $_.id }) -join ', ') +
            ". It and this renderer do not fit on the 16 GB card together - unload it or ask the session that loaded it.")
    }
} catch {}
try {
    $used = ((Get-Counter '\GPU Adapter Memory(*)\Dedicated Usage').CounterSamples |
        Measure-Object -Property CookedValue -Maximum).Maximum / 1GB
    Write-Host ("GPU memory in use before starting: {0:N1} GB" -f $used)
} catch {}

$proc = $null
$log = Join-Path $Out 'server.log'
try {
    #What the .txt records about the server. A reused one was started by someone else, so its options are not
    #ours to claim - say so rather than writing this run's intended flags over an image they did not shape.
    $serverNote = 'reused, options unknown'
    if (Test-ServerPort $Port) {
        Write-Host "Reusing the server already listening on port $Port."
    } else {
        $serverArgs = @('--listen-port', $Port, '--diffusion-model', "`"$diffusion`"", '--vae', "`"$vae`"",
            '--llm', "`"$encoder`"", '--vae-tiling')
        #Offloading stays the default because the Q8 weights do not fit otherwise; it is NOT what causes the
        #resets (see the .DESCRIPTION - a no-offload Q4_K run died sooner), so -NoOffload is not a safer mode.
        if (-not $NoOffload) { $serverArgs += '--offload-to-cpu' }
        #-ExtraServerArgs carries sd-server's default generation options, which is where the highres fix lives
        #(--hires --hires-scale --hires-upscaler --hires-denoising-strength). They belong on the server rather
        #than in the request because the prompt reaches the server as JSON and never goes near a command line:
        #a prompt with quotes in it would not survive the re-quoting, and a changed prompt is a changed image.
        if ($ExtraServerArgs) { $serverArgs += $ExtraServerArgs }
        #Skip --listen-port and its value; the rest is what shaped the image.
        $serverNote = ($serverArgs | Select-Object -Skip 2) -join ' '
        Write-Host ("Models: {0} + {1}{2}" -f [IO.Path]::GetFileName($diffusion),
            [IO.Path]::GetFileName($encoder), $(if ($NoOffload) { ', all on the card' } else { ', offloading to RAM' }))
        $proc = Start-Process -FilePath $exe -ArgumentList $serverArgs -RedirectStandardOutput $log `
            -RedirectStandardError "$log.err" -WindowStyle Hidden -PassThru
        $deadline = (Get-Date).AddSeconds(180)
        while (-not (Test-ServerPort $Port)) {
            if ($proc.HasExited) { throw "sd-server exited with code $($proc.ExitCode); see $log and $log.err" }
            if ((Get-Date) -gt $deadline) { throw "sd-server did not listen on port $Port within 180 s; see $log" }
            Start-Sleep -Milliseconds 500
        }
        Write-Host "sd-server started (pid $($proc.Id))."
    }

    foreach ($it in $items) {
        $base = $it.Seed
        if ($base -lt 0) { $base = Get-Random -Minimum 1 -Maximum 2000000000 }
        for ($i = 0; $i -lt $Count; $i++) {
            $s = $base + $i
            $body = @{ prompt = $it.Prompt; negative_prompt = ''; width = $it.W; height = $it.H; steps = $Steps;
                cfg_scale = 1.0; seed = $s; batch_size = 1 } | ConvertTo-Json -Compress
            $sw = [Diagnostics.Stopwatch]::StartNew()
            $res = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/sdapi/v1/txt2img" -Method Post `
                -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 1800
            $secs = $sw.Elapsed.TotalSeconds
            $file = Join-Path $Out ("{0}-{1}" -f $it.Name, $s)
            [IO.File]::WriteAllBytes("$file.png", [Convert]::FromBase64String($res.images[0]))
            #The .txt is what a reference is re-rendered from, so it carries everything that shaped the image:
            #the server options too, since a highres fix changes the output while the request stays the same.
            $meta = "name: $($it.Name)`r`nseed: $s`r`nsize: $($it.W)x$($it.H)`r`nsteps: $Steps`r`nmodel: " +
                [IO.Path]::GetFileName($diffusion) + " + " + [IO.Path]::GetFileName($encoder) +
                "`r`nserver: " + $serverNote + "`r`nseconds: " +
                $secs.ToString('F1', [Globalization.CultureInfo]::InvariantCulture) + "`r`n`r`n$($it.Prompt)`r`n"
            [IO.File]::WriteAllText("$file.txt", $meta, (New-Object Text.UTF8Encoding($false)))
            Write-Host ("{0}  {1}x{2}  seed {3}  {4:N1} s" -f "$file.png", $it.W, $it.H, $s, $secs)
        }
    }
} finally {
    if ($proc -and -not $KeepServer -and -not $proc.HasExited) {
        Stop-Process -Id $proc.Id -Force
        Write-Host 'sd-server stopped; the card is free.'
    }
}
