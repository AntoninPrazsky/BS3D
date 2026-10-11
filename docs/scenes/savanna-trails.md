# A path goes round a tree: savanna trails and planting

Part of [The scenes](../scenes.md).

The savanna's trails are painted by `Savanna.fx` as the contour lines of one low-frequency noise, while the plants are placed on the CPU by `SavannaScatter`. Neither knew about the other, so worn paths crossed trunks (#476). Two halves fix it, and the planting half depends on the first.

## The bend: `TrailWarpField`

A small CPU-built field of which way to step aside, handed to the shader as an RG texture, domain-warps the trail's noise sample: a point near a plant samples as if it stood further out, so the contour that would cross the trunk arrives as a **bend**. (Cutting the trail where a tree stands was rejected: a path that stops and starts is a different wrong picture.)

- ⚠ **The bend is wide and early**, because people see ahead: `TrailAvoidReach` extends past the plant's own edge by many trunk widths and `TrailAvoidOffset` steps the path far aside. A repulsion that began at the bark would read as a kink at the last moment.
- **Summed, not nearest-only**: a nearest-only field would send the path into the gap between two close trunks.
- **Built from what is worth going round** (`TrailAvoidMinRadius`): trees, mounds, kopjes, fallen trunks and the hearths; grass is walked through. That also keeps the build cheap.
- ⚠ **Push the uniforms where the field is built**, not with the rest of the savanna's dials: `ApplySavannaParameters` runs before the planting, so a texture pushed there is always null (it looked exactly like the feature not working).

## The planting refuses trodden ground

A bend has a limit, so a site is refused if the ground under it is trodden. `SavannaTrails` is the CPU mirror of the term `Savanna.fx` paints (same noise, zero band and warp) without `TrailStrength` and the band-limiting fade. `ShaderMath` holds the one C# copy of `Clouds.fxh`'s `CloudNoise` that this mirror and `CloudField`'s weather both read.

- **The test is the stem, not the crown**: a path under a canopy is what a path does.
- **What a path bends round and what may stand on it are different questions.** The warp uses what is big enough to walk round; the refusal applies to **everything planted, grass tufts included**, since a tuft in the bare middle of a path is the artefact.
- ⚠ **The plain is planted three times (`SavannaScatter.PLANTING_SWEEPS`).** Whether a site is on a path cannot be answered until the plain is planted, and the answer moves the plants. One sweep fixed ground cover (planted last) and barely the trees (planted first, nothing to bend round yet; the report is about trees). The first sweep finds where tracks will run, later ones plant clear of them; only the last sweep's instances are kept. It does not converge to nothing and is not meant to (a few stems at a track's fringe remain; 4 and 5 sweeps straddle 3).
- ⚠ **Each site rolls from its own stream of dice**, or a site needing a second proposal shifts the dice for everything after it and every sweep is a fresh savanna tested against a plain that no longer exists. It also makes before/after captures readable: only plants that moved differ.
- ⚠ **The path is the first ranking key, not a penalty added to clearance.** As a priced term it lost to elbow room every time (16-19 plants a seed were put back on a path). Ranking it first costs only the gap between the roomiest proposal and the roomiest off-path one, and about one proposal in ten is on a path.
- **Result** over six scene seeds: trees and the like left on a path fell from 12-19 to 0-5 each, ground cover from 9-17 to 0-1. Pairs standing inside each other (#108's fault) did not return.
- **Cost:** 10 / 18 / 24 ms for one / two / three sweeps on the slow machine (Vega 10 APU), at scene load and in the editor's re-plant. Nothing per frame.
