---
name: design-references
description: Draw reference images locally before designing how something in BS3D should look — a mesh's silhouette and ornament (the trophy cup), a family of props (rooftop dishes, masts, 5G panels), a scene's material treatment (the island per scene). Z-Image-Turbo through stable-diffusion.cpp on the desktop's RX 6900 XT, ~35 s an image. Load when starting a visual design issue or brainstorm, or when the owner asks what something could look like. References only — nothing generated goes into the game. Needs the desktop (C:\Users\panrd\AI\sd) and ~10.5 GB of the shared GPU.
---

# Design references

The game draws everything procedurally, and designing a mesh or a material in code is easier with something concrete to look at. This skill renders that something locally. **The owner's verdict on the first twenty (#441, 2026-09-16): *„ty obrázky jsou skvělé”*.**

> **The resets are over (2026-09-21, the owner's ruling).** This renderer hard-reset the desktop ten times in fourteen runs between 2026-09-17 and 2026-09-21 — instant power loss (`Kernel-Power 41`, BugcheckCode 0, no WHEA, no 4101), model and configuration notwithstanding: Z-Image Q8 with offload, Q4 all on the card, FLUX.2 klein 4B on the card, while a hundred Testbed and Game runs across the same days were clean, and every flag was ruled out in turn (a replaced cable, the power limit up and down, offload on and off). **The cause was the card's own boost clock and voltage**: the owner capped the core at 2100 MHz and 1080 mV with the power limit at −5 %, stress-tested it with LLMs at length and reports it *„100% stabilní … nikdy to nespadne“*. A rasterizer never pushed the card into its boost under dense compute; a diffusion sampler did every time. So **render without asking**; keep the output outside the repository and finished work pushed as ordinary hygiene; and if a `Kernel-Power 41` ever recurs (he may raise the clock later), say so at once and name the clock first.

```powershell
.\.claude\skills\design-references\render-references.ps1 -Name cup-gold -Width 832 -Height 1216 -Count 3 -Prompt "Studio product photograph of ..."
.\.claude\skills\design-references\render-references.ps1 -PromptFile C:\Users\panrd\AI\sd\prompts-441.json -Out C:\Users\panrd\AI\sd\out\441
```

The script starts `sd-server` if nothing listens on port 7860 (LM Studio holds 1234), renders every prompt `-Count` times with consecutive seeds, writes `<name>-<seed>.png` with a `.txt` beside it (prompt, size, seed, time), and stops the server again. Output goes under `C:\Users\panrd\AI\sd\out` unless `-Out` says otherwise. **A seed reproduces its image byte for byte**: the #441 cup came back identical through the script, so a reference can always be re-rendered from its `.txt`.

## What it is for, and what not

- **For:** a few concrete variants before a design is written in code. On #441 it drew five cups for #429 (tall, gems in raised settings, a lathe-readable front view), rooftops and a prop sheet for #436, and the island in six scenes' materials for #404.
- **A chosen reference can go on to become a mesh reference** (#492): TripoSR on the CPU turns one object on a plain background into an OBJ in about 45 s, silhouette and proportions from every side — see "A mesh from a picture" in the `local-ai` skill.
- **Not for anything shipped.** No textures, sprites or meshes come out of it. The game stays procedural, and the repository is public, so **keep generated images out of it** unless the owner asks.
- **Not for exact detail.** Placement in a prompt is a suggestion: sapphires asked "around the base of the bowl" went into the plinth. Text appears when told not to ("5G" printed on a panel and an air conditioner). Counts and proportions drift. Take the idea, not the measurements.

## Writing the prompt — what was measured

- **Describe the thing by its shape, not its name.** "A wide glass funnel drain ringed with a polished gold band" put a martini glass standing on the platform in **6 of 6** islands. "A round drain hole in the middle of the floor… lined with glass, sunk down into the platform like a sink drain, its rim flush with the floor… Nothing stands on the platform" was right in **6 of 6**, with the same seeds.
- **Start from the issue's own words**, then name the kind of picture:
  - an object: *"Studio product photograph of …, dark neutral gradient background, centred, three-quarter view, sharp focus"*;
  - a setting: *"Game environment concept art: …, seen from an elevated three-quarter view"*;
  - a family of props: *"Game asset concept sheet of …, each object shown separately in a neat row on a plain light grey background"*. That was the most direct basis for procedural meshes.
- *"Front elevation, orthographic, perfectly symmetrical"* came back as a flat illustration rather than a photograph. It still gave a clean silhouette that reads as a lathe profile, which is what a revolved mesh like `TrophyMesh` needs.
- **Size:** 832×1216 for a tall object, 1216×832 for a scene. 8 steps at cfg 1 are the Turbo model's settings, so leave them.
- **More seeds beat more rewording** when the prompt already says the right thing: `-Count 3` is three variants for ~105 s.

## Drawing over the game's own frame (#489)

`-Init <capture.png> -Strength <s>` renders **over a Testbed capture** through sd-server's img2img instead of from noise, so the reference keeps the game's composition — the island, the drain, the gun, the cluster and the horizon stay where `campos`/`camtarget` put them — and only what the prompt describes is redrawn. The capture is fitted to the render size (scaled to cover it, centre-cropped, never stretched) and written once beside the first seed as `<name>-<seed>-init.png`, so what went in is on record; the sidecar carries `init:` and `strength:`. A prompt-file entry may name its own `init` and `strength`. `-DryRun` builds every request and writes the fitted image without starting a server.

**Capture the frame with the post effects off and at the render size.** `nopost` zeroes the film grain and the chromatic aberration, which would otherwise go into the model as texture; `width=1216 height=832` makes the back buffer the render size so nothing is resampled; `F12` hides the overlay and `shot=` saves the frame from inside the program:

```
Testbed.exe Maps\Full.json scene=aurora campos=0,-4,30 camtarget=0,-8,0 width=1216 height=832 nopost fpscap=75 at=6:F12 shot=8 at=11:Escape
```

**Measured on the aurora (2026-09-21, the wood #462 complains about), one prompt from that issue's brief, seeds 11–13** — the images are in `C:\Users\panrd\AI\sd\out\489`:

| Strength | Seconds an image | What came back |
|---|---|---|
| 0.35 | 18.4 | The game's frame with a light retouch: the stamped spruces are still stamps, the balls, the gun and the tiles are the Testbed's. Nothing to design from. |
| 0.5 | 25.4 | **The reference the issue asked for.** Same island, drain, gun and cluster in the same places; the wood redrawn as layered silhouettes in depth — tall thin spruces, dead spars, leaning trunks, a darker treeline behind, snow patches on the platform — with rays in the curtains. The cannon comes back as a period gun and the balls as a mixed cluster: the price of the redraw, and irrelevant to a scene reference. |
| 0.65 | 28–37 | Still the game's layout, and the redraw starts eating the island's own features: the glass drain came back as a flat glass disc under the gun, the cluster shrank, the wood gained layers and snow. Usable for the backdrop, not for anything on the platform. |
| 0.8 | 137–140 | **The composition is gone**: the island replaced by a small railed pit in a snowy clearing, ground fog, lit trunks, the cluster a handful of balls. A beautiful concept of the scene and no longer a reference *over the game's frame* — that is text-to-image with a colour hint. |

The first image of a run costs about 8 s more than the rest (the init image's VAE encode and the graph build). So for a scene rework **start at 0.5**; go lower only to keep one specific object as it is, and expect 0.35 to hand the game back, 0.65 to start replacing what stands on the island and 0.8 to replace the composition. **Measured in two runs**: 0.35 and 0.5 before the reset of 09:22, 0.65 and 0.8 at 10:30–10:50 with the owner's go after he had raised the GPU power limit — that run went through clean, but its step time rose from ~3.5 s to ~18.7 s from the third image on (28–37 s an image became 123–140 s) while a 3.3 GB download ran beside it and stayed slow after; the cause is not isolated, so the two timing columns are not comparable and the 0.8 figure is not the model's cost.

## A second model: FLUX.2 klein 4B (#493, measured; the owner picks)

`-DiffusionModel flux-2-klein-4b-Q8_0.gguf -Encoder Qwen3-4B-Q8_0.gguf -Vae full_encoder_small_decoder.safetensors -NoOffload -Steps 4` — the files are in `models` (from `leejet/FLUX.2-klein-4B-GGUF`, `unsloth/Qwen3-4B-GGUF` and `black-forest-labs/FLUX.2-small-decoder`, all Apache 2.0 and none gated; the FLUX.2-dev VAE is gated, the small decoder is sd.cpp's own listed alternative). The encoder is the plain Qwen3-4B, not Z-Image's Instruct-2507 — the one klein was trained against.

**The full sweep, 2026-09-25** (after the owner's GPU cap): `prompts-493.json` — the five #429 cups, the three #436 rooftop prompts, the six #404 islands in the name wording and again in the shape wording — two seeds each, both models, **80 of 80 images, no reset, no failed request**. klein ran with everything on the card for all 40, the 1216×832 landscapes included; the one earlier failure on the card (a landscape VAE decode at 14.65 GB) was a card another process already held, not klein. Images in `C:\Users\panrd\AI\sd\out\493-klein-2` and `493-zimage-2`; the pairs, per-image times and notes are on the owner's page https://claude.ai/artifact/WrEoKtFx3ytTvVSR9psvF9.

| | Z-Image-Turbo Q8, offload, 8 steps | FLUX.2 klein 4B Q8, on the card, 4 steps |
|---|---|---|
| Seconds an image | median 40 (832×1216) and 37 (1216×832), range 36–55 | **median 13 either way**, range 13–20 (the first image of a server ~19) |
| All 40 | 26.4 min | **9.3 min** |
| Memory | sd-server peaked at **10.5 GB** on the card even offloaded (card 13.3 of 16), weights streamed over PCIe every step | sd-server peaked at **11.3 GB** (card 13.6), nothing streamed |
| Applied ornament (the gold cup) | **richer**: gems in raised settings on rim, bowl and plinth | a cleaner product shot, gems on the base only |
| Following the wording | the bronze grew the handles the prompt forbade (seed 1); no laurel band on the silver | **closer**: no handles on the bronze, a laurel band on both silvers, gold mounts on the crystal |
| Prop sheet (#436) | **all six props**, the three-panel 5G pole | fewer props, 5G panels drawn like solar panels, a stray satellite |
| Drain test, shape wording | 12 of 12 a flush drain, nothing standing | 12 of 12, and the only true inward-sloping dishes |
| Drain test, name wording | 12 of 12 a martini glass | 12 of 12 a martini glass — the prompt rule holds for both models |
| Marks and text | **none in the corners** | **a fake watermark/logo in the bottom corners of 9 of the 12 seed-2 islands** (none on seed 1, the cups or the rooftops); pseudo-text on neon signs |
| Materials | painterly concept art | more photographic: refraction and caustics in crystal, driftwood, glowing cracks |

**Reading it:** klein is three times faster in the same memory and follows wording at least as well, so it is the natural pick for scenes, settings and anything iterated over many seeds; Z-Image still draws the richer applied ornament and the more useful prop sheet, and never stamps a corner mark. A klein corner stamp is a crop, not a lost reference — but check the corners before handing one on. Which model is the default is **the owner's verdict on #493**, and until he gives it the script's default stays Z-Image.

`-SkipExisting` makes a long sweep resumable: rerun it with the same arguments and it renders only the `<name>-<seed>.png` that are missing. The #493 sweep ran detached (`Start-Process powershell -File … -RedirectStandardOutput`) in chunks of five prompts, one server start each, with a sampler logging the card's total and sd-server's own dedicated memory (`\GPU Process Memory(pid_<id>*)\Dedicated Usage`) — the card total alone cannot tell this renderer from another session's game.

## A silhouette becomes a picture level (#491)

The three shape levels nobody complains about — Heart, Smiley, Star — are the ones drawn as **pictures**, and their bitmaps were drawn by hand, which is why there are three. `silhouette-to-picture.py` lets the renderer draw the silhouette and turns the picture into the bitmap `Picture()` in `Tools/LevelGen/Designs/Block02_Gallery.cs` takes:

```powershell
& C:\Users\panrd\AI\ComfyUI\venv\Scripts\python.exe .\.claude\skills\design-references\silhouette-to-picture.py `
    C:\Users\panrd\AI\sd\out\491\491-anchor-1.png --name Anchor --out-dir C:\Users\panrd\AI\sd\out\491-bitmaps
```

- **The prompt is the skill's own rule turned on the renderer**: *"A solid black silhouette of a ship's anchor, centred on a plain white background, flat vector icon, no shading, no outline, no text, no shadow."* — `prompts-491-silhouettes.json` holds ten of them (anchor, key, teapot, cat, guitar, umbrella, rocket, fish, crown, bell), at 832×832.
- **Ink is whatever is not the background.** A cut-out with an alpha channel says so directly; a flat picture is split by Otsu's threshold and the border — the background by construction of the prompt — says which side is ink, so black-on-white and white-on-black both work. (A midpoint between the border and the darkest pixel did not: the #441 cup's gold body fell on the background side and 7 cells came out of it.)
- **The wall's rows sit 1/√2 apart against a column pitch of 1**, so the shape gets √2 more rows than a square grid would give it or it hangs squashed — the Heart is 14 rows for 13 columns. Resampled by area average, thresholded at `--fill` (0.5), optionally closed 3×3 (`--close`) to bridge a key's teeth; never more than `--max-rows` 18 (`GameplayScreen.FRAMED_LEVELS` — 20 would silently make a tall level), always an even count (an odd one moves the drawing a level), `--top` empty rows over the shape and `--side` beside it because the background is what the wall hangs by, and never wider than the Gallery's 15.
- **Three files a shape**: the C# `string[]` literal, a preview of the wall as it will look (one disc a cell, rows at their real pitch, odd rows shifted half a cell) and a **map the Testbed opens directly** (`Testbed.exe <name>-map.json`) — the wall in a 15×15×18 field, ink as type 1 over the Heart's 4/7 check — so a bitmap can be photographed in place without touching the pack. It prints the ink count and how many 4-connected pieces the ink fell into: a key whose teeth broke into islands reads as noise, and the count says so before anyone plays it.
- **Checked by a round trip**: the Heart bitmap drawn as a black-on-white picture at the wall's own proportions came back **identical, all 14 rows** (`--width 13 --top 2 --side 2`), which is the arithmetic of the stretch and the threshold proven on the one shape whose answer is known.
- The pack is not changed by the script; whether generated pictures are a level source worth having is the issue's verdict, from the ten bitmaps and the owner's eye (#440 measured that neither local vision model can judge whether a shape reads).

## Making a chosen one bigger

**Upscale it; do not re-render it larger.** A seed does not survive a change of size — the latent noise is a different shape, so the same prompt and seed at another resolution gives a different picture, not a bigger one. And the highres fix is not available on this machine:

- **`--hires` at scale 2 measured 680 and 889 seconds per step** (against 3.45 s/step at 1216×832), because the second pass at 2432×1664 cannot get a pinned buffer — `ggml_vulkan: Failed to allocate pinned memory (Requested buffer size exceeds device buffer size limit)` — and falls back to unpinned transfers. `sd-server` took **77.5 GB of commit**, the machine reached **92.2 GB of its 92.4 GB commit limit**, and the run died on the script's 30-minute request timeout with nothing written. Eight steps would have been about two hours of thrashing. Anything that *samples* at that size hits the same wall, img2img included.
- **What works is `sd-cli -M upscale`, in about ten seconds:**
  ```powershell
  & C:\Users\panrd\AI\sd\bin\sd-cli.exe -M upscale -i <in.png> `
      --upscale-model C:\Users\panrd\AI\sd\models\RealESRGAN_x4plus_anime_6B.pth -o <out.png>
  ```
  1216×832 → 4864×3328, 8.2 s of upscaling, tiled at 128 px so the memory cost is nothing. The **anime_6B** variant is the right one for this project's references — it is trained for illustration, which is what a flat glossy logo or a concept sheet is; the general `x4plus` model invents photographic texture. Checked at 1:1 against a bicubic resample of the same image to the same size: the keyline is a clean edge instead of a soft ramp and the speculars keep a defined border. Nothing is re-sampled, so the composition is exactly the one that was chosen.
- The upscaler is not part of the original setup; fetch it once from [Real-ESRGAN v0.2.2.4](https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.2.4/RealESRGAN_x4plus_anime_6B.pth) (18 MB) into `models`.

**Watch the variable name if you extend the script.** PowerShell identifiers are case-insensitive, so a parameter `$ServerArgs` *is* the local `$serverArgs` the script builds its command line in: the local assignment silently ate the parameter, the array was appended to itself, and the run looked normal while rendering at the old size with none of the flags. The parameter is `-ExtraServerArgs` for that reason. A rendered image proves nothing about which flags were used — read them back out of `server.log`.

## Cutting the background out

`cutout-alpha.py` turns a chosen reference into a straight-alpha RGBA PNG, cropped to what is drawn and scaled to a target width. It needs numpy, Pillow and scipy, which the **system Python does not have** — run it with the ComfyUI virtualenv's interpreter:

```powershell
& C:\Users\panrd\AI\ComfyUI\venv\Scripts\python.exe .\.claude\skills\design-references\cutout-alpha.py `
    <in.png> <out.png> 2048 [t_lo] [t_hi]
```

- **A colour key does not work on this art and the reason generalises.** Between each letter and its outline runs a dark groove only ~16 units from the background colour, so any threshold that keeps the groove also keeps half the background. The script identifies background by **connectivity** instead: a near-background pixel is background only if it can be reached from the frame edge without crossing artwork, so grooves, letter counters and any enclosed dark stay opaque whatever their colour. Single grains of film noise clear the threshold too, and one at the frame edge stretches the crop box over the whole picture — regions under 256 px are dropped before the box is measured.
- **Choose the cut from the image's own histogram.** On the logo the distance-from-background was cleanly bimodal: frame noise at ~1, the outer glow spanning 8–45, artwork above ~145 (the 70th and 75th percentiles of the frame were 43.7 and 145.2). The default 45/95 lands in that gap.
- **Drop the glow unless the logo will sit on the dark background it was drawn against.** A glow drawn as light over dark plum is *darker* than a pale background, so over the game's sky it composites as a purple smudge rather than as light. Cutting at 25/80 to keep a little of it is worse than either extreme: un-compositing at low alpha drives the colour towards white and the word gets a pale sticker fringe.
- The resize happens in **premultiplied** space and is un-premultiplied afterwards, or transparent pixels' colour bleeds into the edge. The PNG is straight alpha, which is what `Content.mgcb` wants — every content project here already passes `/processorParam:PremultiplyAlpha=True`, so the pipeline premultiplies on build and `BlendState.AlphaBlend` is correct. A texture loaded at runtime with `Texture2D.FromStream` instead is **not** premultiplied and will fringe.

### Letting a network decide instead — measured, and it is not a clear win

`matte-birefnet.py` runs **BiRefNet** (MIT, ONNX Runtime, **CPU only — it cannot cost a GPU reset**) and `combine-matte.py` gates the connectivity cut with its mask. Models live in `C:\Users\panrd\AI\matting\models`, in their own venv (`C:\Users\panrd\AI\matting\venv`, onnxruntime + numpy + Pillow) so ComfyUI's environment is untouched; `combine-matte.py` needs scipy and runs under ComfyUI's interpreter.

```powershell
& C:\Users\panrd\AI\matting\venv\Scripts\python.exe .\.claude\skills\design-references\matte-birefnet.py `
    C:\Users\panrd\AI\matting\models\birefnet-lite.onnx <in.png> <out.png> 2048
```

Measured on the logo (4864×3328 source, output 2048 wide):

| | inference | fully opaque | soft edge |
|---|---|---|---|
| connectivity cut | — | 40.6 % | 7.5 % |
| BiRefNet lite (224 MB) | 6.2 s | 12.6 % | 35.4 % |
| BiRefNet full (973 MB) | 11.2 s | 26.0 % | 19.2 % |
| hybrid (cut gated by lite) | +1 s | 38.6 % | 6.1 % |

- **The big model was worse than the small one.** BiRefNet full smeared the right-hand letters and dirtied the bottom edges; lite did not. Four times the weights bought nothing here, so try lite first and only reach for full if lite fails.
- **A matting net is soft by construction.** BiRefNet takes a fixed 1024×1024 input, so on a 4864-wide source the matte is upscaled 4.75× before it meets the picture. It cannot resolve a keyline a few pixels wide, which is why its soft fraction is three to five times the cut's.
- **It made two calls the cut cannot** — it dropped the thin purple keyline looping each letter, and it made the letter counters transparent — and **the owner rejected both**. Shown all four versions over sky and over white he picked the plain cut: *„Nejlepší je pořád bs3d-logo-2048.png, protože zachovává fialový obrys textu (alespoň náznaky)."* My own note here said the keyline "reads as a sticker outline" anywhere but the plum it was drawn on; that was a guess, and it was wrong. The keyline is part of the mark to him even in traces, so **the hybrid and the net-only versions are alternatives, not improvements, and the plain cut is what the logo is.**
- **The finding that survives is about where each method is strong, not about which output to ship.** The net answers *what the subject is* and cannot resolve a thin edge; the cut answers *where the edge is* and has no opinion about what belongs. `combine-matte.py` exists to compose the two when a reference genuinely needs the net's judgement — and this logo shows that whether it does is an aesthetic call, so **put the versions in front of the owner rather than choosing for him**.
- **Generating alpha directly is not available here.** Z-Image-Turbo decodes RGB through its VAE; there is no alpha anywhere in the path, and any transparency-capable model is a *different* model, so it would produce a different picture rather than this one with an alpha channel. LayerDiffuse (SDXL/SD1.5) is the mainstream option and sd.cpp does not support it; ComfyUI here is CPU-only torch with an empty model tree. This sd.cpp build *does* carry `--qwen-image-layers` for Qwen-Image-Layered, but Qwen-Image is a 20B model — around 12 GB of weights even at Q4, on a machine where the 7 GB Z-Image already wanted 77 GB of commit for one 2× pass.

## Showing them

The owner judges whether a reference helps, so **publish a page**: each image with its prompt and one line on what it got right and what deviates from the prompt. #441 did it that way and the owner answered from the page.

## The card

| Server flags | Result on the RX 6900 XT (832×1216, 8 steps) |
|---|---|
| everything on the card | out of memory: weights 10.5 GB plus 4.3 GB for the diffusion compute |
| `--offload-to-cpu` | 62.8 s — the VAE decode spilled onto the CPU (25 s) |
| `--offload-to-cpu --vae-tiling` (what the script runs) | **33–37 s**, decode 3.8 s, no visible tile seams |

- **Memory:** the server peaked at **10.5 GB**, and the whole card at 12.8 GB. So it **does not fit beside the Game or beside Gemma 4 in LM Studio**. The script warns when LM Studio has a model loaded.
- **Sharing:** tell peer sessions before rendering (`ListAgents`, `SendMessage`) and wait for a free window. On 2026-09-16 the handovers with the music session went cleanly.
- **Stability:** 22 images once the server fit, and no failure. The System log had no 4101, 41 or 6008 during them. If one ever appears, stop and tell the owner at once.

## Setting it up (the desktop already has it)

Everything lives in `C:\Users\panrd\AI\sd`, outside the repository. Nothing is installed; it is one unpacked zip and three model files.

- `bin`: the Windows **Vulkan** build from [stable-diffusion.cpp releases](https://github.com/leejet/stable-diffusion.cpp/releases), `sd-master-<hash>-bin-win-vulkan-x64.zip`. Measured with `master-869-07a85c7`.
- `models\z_image_turbo-Q8_0.gguf` (6.6 GB): [leejet/Z-Image-Turbo-GGUF](https://huggingface.co/leejet/Z-Image-Turbo-GGUF). Apache 2.0.
- `models\Qwen3-4B-Instruct-2507-Q8_0.gguf` (4.3 GB), the text encoder: [unsloth/Qwen3-4B-Instruct-2507-GGUF](https://huggingface.co/unsloth/Qwen3-4B-Instruct-2507-GGUF). Apache 2.0.
- `models\ae.safetensors` (335 MB), the VAE: [Comfy-Org/z_image_turbo](https://huggingface.co/Comfy-Org/z_image_turbo/tree/main/split_files/vae).

**Why not ComfyUI or AMD Amuse**, which #441 started from: the desktop's ComfyUI venv has a CPU-only PyTorch, and ROCm on Windows does not support the RX 6000 series. stable-diffusion.cpp runs on ggml over Vulkan, the same route `acestep.cpp` already makes music on (#443). The laptop is unmeasured; the script stops with a pointer here if the files are missing.
