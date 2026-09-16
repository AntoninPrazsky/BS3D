---
name: capture-review
description: Compare before/after screenshots of a BS3D change — an exact block diff says which pairs changed and where (identical pairs never reach a model), then the local vision model (Gemma 4 in LM Studio) describes each change over the whole frame and over crops of the sharpest changed regions, so you open only the pairs whose description is unexpected. Load whenever a rendering, shader, HUD, camera or scene change is verified with captures, especially more than a couple of pairs or a sweep across scenes, and to confirm a refactor left the picture unchanged. The descriptions need LM Studio on the owner's desktop (localhost:1234); the pixel diff runs anywhere.
---

# Reviewing captures

Companion to `screenshot` and `verify` (which make the captures) and `local-ai` (which runs the model). This one is about **reading a set of before/after captures cheaply and without missing what changed**.

```powershell
.\.claude\skills\capture-review\review-captures.ps1 -Before C:\caps\before -After C:\caps\after -Out report.md
.\.claude\skills\capture-review\review-captures.ps1 -Before a.png -After b.png
.\.claude\skills\capture-review\review-captures.ps1 -Before C:\caps\before -After C:\caps\after -NoModel   # diff only
```

Pairs are matched by **file name** across the two directories.

## When it pays, and when it does not

- **Pays:** three or more pairs; a sweep of scenes or domes before and after a shader change; a refactor that must not change the picture (the diff alone decides that, exactly, with no model); a HUD or effect change you want described in words across many captures.
- **Does not pay:** a single pair you are going to open anyway — your own look is more reliable than the model's; **exact values** (a colour, a size, a delta E) — measure pixels (`screenshot/palette.ps1`, a bar scan); **judgements of taste or readability** — on #440 neither local model could tell whether a level's shape reads.

## What it does

1. **Block diff, exact and fast** (0.6 s for four 1600×900 pairs). Each 16×16 block's mean colour is compared; a block counts as changed when its mean moves more than `-BlockThreshold` (12 of 255). Means rather than per-pixel differences, because film grain is zero-mean and cancels inside a block.
2. **Identical at block level → done**, no model call.
3. Changed blocks are joined into **regions**, ranked by their **sharpest** block, so a thin mark that changed hard (a crosshair turning red) outranks a broad area that drifted a little (clouds).
4. For each changed pair the model describes the **whole frame** (scaled to 1280 px) and then each of the `-Regions` sharpest regions (default 2) as a **crop** — padded, at least 256 px, enlarged when small — because the crop is where it was measured right: 7 of 7 against 5 of 7 on whole frames (#440). Specks under `-MinRegionBlocks` and regions covering most of the frame get no crop.

## Reading the report

For every pair: changed blocks, the regions (pixel bounds, size, peak), the whole-frame answer and the crop answers. **Compare each description with what the change was meant to do, and open only the pairs where something in it is unexpected.** Treat every answer as a lead: on #440 the same model called an upward camera tilt a zoom. Confirm with pixels or your own eyes before anything goes into a document.

## Captures that compare well

- **Same vantage, same names:** pin the camera (`campos=`/`camtarget=`, `scene=`, `sky=`) and capture with the Testbed's `shot=`/`shotframe=` — see `screenshot` and `verify`.
- **Anything that moves between two frames is a real difference and will be reported:** the drifting clouds, the balls' emissive breath, the gun's spring, and the **FPS counter** (the model named "75 to 3" on a pair taken a second apart). Hide the overlay (`F12` in the Testbed), stop the simulation (`F5`), take both captures at the same frame index (`shotframe=`), and pass `nopost` when shading is what is being judged — grain and chromatic aberration otherwise disguise it.

## Measured, 2026-09-16

On four pairs built from the #431 Testbed captures (1600×900, City):

| Pair | Changed blocks at 8 / 12 / 16 / 24 | Model (Gemma 4) |
|---|---|---|
| the same file twice | 0 | not called |
| two captures a second apart, only clouds and the gun moving | 133 / 71 / 27 / 5 | whole: FPS counter changed; crops: one "identical", one "the sphere moved slightly down" (the diff had that region changed too, peak 29) |
| crosshair white and small → red and twice the size, barrel shifted | 1141 / 567 / 388 / 173 | whole and crop both: crosshair turned red and grew, the barrel and cheeks shifted; a second crop over the sky: clouds and grain |
| camera tilted from the cluster up to the sky | 3811 / 3456 / 3261 / 3087 | whole: cluster gone, red crosshair appeared, but "the cannon is closer" |

The clouds' drift sits at a block delta of about 4–10, which is what put the default at 12, and the barrel's region peaked at 159. **A thin HUD mark sits close to that threshold**: the crosshair's own region peaked at only 16 and ranked fourth, and it was described only because the first crop, around the barrel, was wide enough to take it in. When the change under review is a fine mark, pass a lower `-BlockThreshold` (8) and more `-Regions`. The run took **70 s**, 12 s of it loading the model.

## The model and the card

- **Gemma 4 by default** (best on crops and pairs, #440). A Qwen3-VL that is already loaded is used as it is rather than swapped; `-Model` forces one. The mechanics — contexts, thinking off, what to do on `ErrorDeviceLost` — are in `local-ai`.
- **The desktop's GPU is shared** with other sessions (music generation runs on it). Check `lms ps` and `ListAgents`, tell a peer before loading Gemma (12.8 GB), and unload it when you are done (`lms unload google/gemma-4-12b`) rather than leaving it to its 30-minute TTL.
- **No LM Studio** (another machine): the script says so and reports the diff alone, which still answers "did anything change, and where".
