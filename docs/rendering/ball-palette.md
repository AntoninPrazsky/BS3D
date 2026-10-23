# The palette: thirteen colours that must stay apart

Part of [Rendering](../rendering.md).

There are thirteen `BallType`s (`Type1..Type13`; tints and ambient in `BasicEffectParamsProvider`): red/green/blue/white, cyan/magenta/yellow/black, orange/brown/silver/navy/olive. **The constraint the game cannot trade away is thirteen types apart at a glance under every dome.** Deliberate quirks: white is drawn beige, yellow gold and silver slate (so the pattern reads against the white gores); black is near-black with near-zero ambient and radiates nothing.

## Measuring

- **Use CIEDE2000, not dE76** (it scored a pure lightness gap like a hue gap and ranked black/navy ninth). **The heartbeat is a lub-dub, two narrow peaks and a long rest, and a shot can land on a peak**: shots at rest agree to about 0.4 dE, but one on the beat reads every ball far brighter and moves a pair by up to 10 dE (#940: the wool's black/brown on the savanna's dome 14 read 9.6 at rest and 17.3 on the beat; one shot in six, 0.5 s apart, landed there, in the savanna and in cloudless space alike). Take several shots, drop the bright one, and compare like with like. **Measure under a bright dome and a dark one** (olive rides the dome's light harder than its neighbours). Tool: `.claude/skills/screenshot/palette.ps1` on a `Thirteen_Colors` capture (`-Whole` disc average; `-Cores` the brightest quarter/half, right for emissive styles).
- **No style may hold two of the thirteen closer than the vinyl does** (#315): its tightest pair is **7.9 (black/brown)**. Fixes live in `InstancedModel.fx` beside the styles: `TintEmission` scales an emissive style's brightness by the tint's own luminance (square-root compressed, floored at `TintEmissionFloor`), because normalising the tint to its peak (plasma, lava) discards value and black and silver arrive at one near-white; the gem's `SaturateTint` lifts chroma at constant luminance (identity on neutrals).

## Decisions already measured

- **Navy (#246)** differed from black only in blue (0.072 of luminance). ⚠ **Lifting navy alone fails**: it walks into Type3 (black/navy 25.3 to 30.8 but navy/blue 24.4 down to 16.7). Type3 was lifted with it ((0.35,0.45,1.0) to (0.45,0.60,1.0)) and the blue family re-spaced black, navy, blue (30.8 and 33.4). Navy stays blue (Globe's ocean uses it).
- **Olive (#294)** sat on black/brown/silver (24.8-26.0). Fix: lift plus **chroma recovery**, (0.42, 0.52, 0.02) (0.08 of blue was desaturating a colour whose identity is having none): 29.6 / 32.6 / 27.6 under dome 1. ⚠ (0.44, 0.56, 0.025) drops green/olive to 22.5: the stopping point was measured.
- **Lava is inked by hue family, not tint** (#395): [Ball shading styles](ball-styles.md).
- The palette's genuinely tightest pair, never filed: red/orange at 4.0 dE on the gem under dome 13.

## Judging colour through the Testbed carries weather the Game never has

The Testbed steps `SkyLightRig.StepOvercast` each frame; the **Game never does** (the palette is authored for daylight and would lighten a dusk city) and the editor has no deck. **`nooverc` pins it** (#334): the ambient is the dome's own, bit-exactly what the Game draws, and the deck still drifts and dims the sun; every run prints one `[overcast]` line saying which regime. **Pass `nooverc` for any colour A/B the way `nopost` is passed for a shader A/B.** The Testbed steps it by default (decided 2026-09-04).

- **Worth** (`Thirteen_Colors`, meadow, dome 1, `weather=overcast`, stepped vs pinned): black/silver 22.8 vs 29.1, red/orange 13.9 vs 17.6, white/yellow 15.0 vs 15.9. **It changes which pair is tightest** (stepped red/orange 13.9; pinned white/yellow 15.9). Under the default scattered sky two runs agree within 1/255.
- Open (#367): a green ball reads 63/113/44 in the Testbed against 31/190/26 in the editor through "comparable" framing; the overcast term does not explain it.
