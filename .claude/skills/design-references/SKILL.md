---
name: design-references
description: Draw reference images locally before designing how something in BS3D should look — a mesh's silhouette and ornament (the trophy cup), a family of props (rooftop dishes, masts, 5G panels), a scene's material treatment (the island per scene). Z-Image-Turbo through stable-diffusion.cpp on the desktop's RX 6900 XT, ~35 s an image. Load when starting a visual design issue or brainstorm, or when the owner asks what something could look like. References only — nothing generated goes into the game. Needs the desktop (C:\Users\panrd\AI\sd) and ~10.5 GB of the shared GPU.
---

# Design references

The game draws everything procedurally, and designing a mesh or a material in code is easier with something concrete to look at. This skill renders that something locally. **The owner's verdict on the first twenty (#441, 2026-09-16): *„ty obrázky jsou skvělé“*.**

```powershell
.\.claude\skills\design-references\render-references.ps1 -Name cup-gold -Width 832 -Height 1216 -Count 3 -Prompt "Studio product photograph of ..."
.\.claude\skills\design-references\render-references.ps1 -PromptFile C:\Users\panrd\AI\sd\prompts-441.json -Out C:\Users\panrd\AI\sd\out\441
```

The script starts `sd-server` if nothing listens on port 7860 (LM Studio holds 1234), renders every prompt `-Count` times with consecutive seeds, writes `<name>-<seed>.png` with a `.txt` beside it (prompt, size, seed, time), and stops the server again. Output goes under `C:\Users\panrd\AI\sd\out` unless `-Out` says otherwise. **A seed reproduces its image byte for byte**: the #441 cup came back identical through the script, so a reference can always be re-rendered from its `.txt`.

## What it is for, and what not

- **For:** a few concrete variants before a design is written in code. On #441 it drew five cups for #429 (tall, gems in raised settings, a lathe-readable front view), rooftops and a prop sheet for #436, and the island in six scenes' materials for #404.
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

- `bin\`: the Windows **Vulkan** build from [stable-diffusion.cpp releases](https://github.com/leejet/stable-diffusion.cpp/releases), `sd-master-<hash>-bin-win-vulkan-x64.zip`. Measured with `master-869-07a85c7`.
- `models\z_image_turbo-Q8_0.gguf` (6.6 GB): [leejet/Z-Image-Turbo-GGUF](https://huggingface.co/leejet/Z-Image-Turbo-GGUF). Apache 2.0.
- `models\Qwen3-4B-Instruct-2507-Q8_0.gguf` (4.3 GB), the text encoder: [unsloth/Qwen3-4B-Instruct-2507-GGUF](https://huggingface.co/unsloth/Qwen3-4B-Instruct-2507-GGUF). Apache 2.0.
- `models\ae.safetensors` (335 MB), the VAE: [Comfy-Org/z_image_turbo](https://huggingface.co/Comfy-Org/z_image_turbo/tree/main/split_files/vae).

**Why not ComfyUI or AMD Amuse**, which #441 started from: the desktop's ComfyUI venv has a CPU-only PyTorch, and ROCm on Windows does not support the RX 6000 series. stable-diffusion.cpp runs on ggml over Vulkan, the same route `acestep.cpp` already makes music on (#443). The laptop is unmeasured; the script stops with a pointer here if the files are missing.
