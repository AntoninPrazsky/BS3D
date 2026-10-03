# The game's logo

Three files, each made from the one before it. They are kept together so the logo can be remade or
re-cut on any machine rather than only on the one that first drew it.

| File | What it is |
|---|---|
| `bs3d-logo-2048.png` | **The logo.** 2048x1267, RGBA straight alpha, cropped to the drawing. This is the one the game uses (#454). |
| `logo-9110-x4.png` | The upscaled master, 4864x3328, still on its plum background. Re-cut from this, not from the 2048. |
| `logo-tube-9110.png` | The original render, 1216x832. |

## How they were made

All three steps are the `design-references` skill's, and its `SKILL.md` carries the measured notes
behind each one — including why the background is cut by connectivity rather than by colour, and why
the logo is upscaled instead of re-rendered larger.

**1. The render** — Z-Image-Turbo through `stable-diffusion.cpp`, `z_image_turbo-Q8_0.gguf` with the
`Qwen3-4B-Instruct-2507-Q8_0.gguf` text encoder, `--offload-to-cpu --vae-tiling`, 1216x832, 8 steps,
cfg 1.0, euler, **seed 9110**. Chosen by the owner out of sixteen consecutive seeds on this prompt:

> Video game logo on a very dark violet background. The words "BUBBLE SHOOTER" set on two stacked
> lines in thick glossy inflated tube lettering, like bent balloon rods with round ends, and a big
> round badge below them reading "3D". Every letter is a different colour of a rainbow that flows
> across the word, magenta into orange into yellow into green into cyan, each letter ringed by a thin
> dark outline. Shiny plastic surfaces with a bright specular streak down each tube, a soft glow
> behind the lettering, centred, sharp, clean rendering, no other objects.

A seed reproduces its image byte for byte, which was checked here: a second run of this prompt and
seed came back with the same SHA-256.

**2. The upscale** — `sd-cli -M upscale` with `RealESRGAN_x4plus_anime_6B`, 1216x832 to 4864x3328 in
about eight seconds. The anime variant is trained for illustration, which is what this is; the general
model invents photographic texture.

**3. The cut** — `cutout-alpha.py <in> <out> 2048 45 95 counters 0.65` (the design-references skill's).
The thresholds come from this image's own histogram, where the distance from the background is cleanly
bimodal: frame noise at ~1, the outer glow spanning 8 to 45, and drawn artwork above ~145. The last two
arguments are #731's: the counters pass, and the share of the height below which it leaves the "3D" badge's
own dark shadows alone. Without them the same command gives the logo as it was until 2026-10-03, byte for
byte (checked: re-cutting the master reproduced the shipped file exactly before the pass was added).

## What was decided and must not be quietly undone

- **The outer glow is gone on purpose.** It was drawn as light over dark plum, so over a bright
  background it composites as a purple smudge rather than as light.
- **The purple keyline around the letters is kept on purpose.** A matting network (BiRefNet) removes it
  and makes the letter counters transparent; the owner compared both and chose the version that keeps
  the keyline, *"protoze zachovava fialovy obrys textu (alespon naznaky)"*. Those alternatives are
  alternatives, not improvements.
- **The nine counters are transparent, and the keyline is still kept (#731).** The counters of the B, B,
  B, O, O and R were an opaque dark violet - the cut keeps every enclosed region - so over the live scene the
  picture read as a sticker, and the 3D wordmark that grows in as it leaves has open counters. The owner's
  choice above was about the keyline; the counters were never part of it. The `counters` pass clears them
  and leaves the thin dark rim round each as the counter's own keyline; nothing outside those nine regions
  changed by one value (checked against the previous file). **The "3D" badge's shadows stay opaque** on
  purpose: they are shadows on a solid disc, not holes. The D's counter shows the disc's own purple.
- **Straight alpha, not premultiplied.** Every content project passes `PremultiplyAlpha=True`, so the
  pipeline premultiplies on build and `BlendState.AlphaBlend` is then correct. Loading this with
  `Texture2D.FromStream` instead skips that and the edges fringe.
