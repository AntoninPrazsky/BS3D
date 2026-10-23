# The tropical beach

Part of [The scenes](../scenes.md).

## What it is

The fourteenth `SceneKind` (#244): the island in the middle of a tropical lagoon. White-gold sand with palms and mossy rocks slopes through a wiggling waterline into turquoise water; some 300 units out a green shore ridge closes the horizon, with one channel to the open sea.

- Code: `Tropical.fx`, `TropicalBackdrop`, `TropicalSceneConfig` (groups Terrain, Water, Palms, Rocks, Birds), `Palm.fx`, `PalmMesh`, `TerrainMirror.Tropical`. Grid `TROPICAL_GRID_N` 360 over `TROPICAL_EXTENT` 1000.
- Launch: `scene=tropical`. No shipped level names it.
- The first `IsSolidTerrainScene` with water (sand under the island; the lagoon starts past the beach, outside the hole). It **defaults to dome 1** on entry, overridable. The flock ([birds](birds.md)) circles the lagoon on its own `BirdsConfig`.

## Draw order and the lagoon

`DrawEnvironment`'s tropical case states its order: the land (opaque, depth-writing), then the lagoon depth-**READ** over the bed, then palms, rocks and flock.

- **The lagoon is `Sea.fx` unchanged**, with the tropical water values, and each water draw has **its own `Effect.Clone()`** (#580). ⚠ A NumPad2/V switch applies no config, so with one shared instance the scene switched to drew the other's water.
- **The visible waterline is the depth test's**: opaque sand rejects the water plane where the beach is still above it, so the line is where the profile crosses the water level. The clip radius only anchors the calm band.
- **`ShallowBias`** (a `Sea.fx` uniform; lagoon 0.78, open sea 0, where `lerp(x, 1, 0)` is bit-exact): `Sea.fx` gives its pale colour only to up-facing swell faces, so the lagoon drew grey-navy `WaterDeep`. The lagoon's body colours are also much brighter, since the Fresnel sky reflection dominates. To check the open sea is unchanged, compare **regional means** over water pixels, not a per-pixel diff (45 % of pixels differ between identical builds: swell and clouds run off the wall clock).
- ⚠ **The play camera cannot see the lagoon.** The island's cap stands 5 units over the beach and hides everything at sand level; moving `ShoreRadius` 100 → 66 changed nothing. Past the island only things that **stand up** show (palms, the far ridge, the sky), so the lagoon belongs to raised vantages (front-end orbit, drop cinematic, editor).

## The land

One radial profile keyed on the distance to the **waterline**: flat dry sand, a hermite down through the water level exactly at the coast, the lagoon bed, then the far shore's rise. The hermites meet at the coast, so waterline, wet band, fringe and clip share one figure. The waterline wobbles by sine octaves of **bearing**, the far shore by its own, and the ridge has one channel (a closed ring reads as a crater lake).

- ⚠ **The height field is sines and hermite ramps ONLY, no gradient noise**, because `TerrainMirror.Tropical` mirrors it to plant on. Noisy things (sand patches, canopy mottle, wind bands) live in the pixel shader.
- The near beach has a **wet band** keyed on height above water and a ripple that shades its troughs (`RIPPLE_SHADE`; domain scale 0.28, or it fades at a third of the distance); the far shore is jungle over a sand fringe.
- ⚠ **Vegetation is two questions** (#268): a short radial gate says which landmass, and the ridge's height above water says how green (`SHORE_SAND_FRINGE`, `JUNGLE_RISE`). Keyed on the ridge's own rise ramp, greenness grew outward and the near flank was sand.
- The haze is two stages, the arrangement the [outback](outback.md) had until #967 replaced its air, weaker: `HazeStrength` towards a sky-lit tint over `haze²`, then the dome's `HorizonColor`. ⚠ The last stage is `haze⁸`, not `haze⁴`: the ridge stands at 300–420 against a 480 haze distance, and at the fourth power its crest was already 48 % to the sky and the jungle came out beige.
- **The foam line is on the land**, as lace from one gradient-noise octave, centred a little *above* the water (on it, it reads as a ring round the island).

## Palms and rocks

- `PalmMesh` is two meshes over one instance: solids (trunk, boot, coconuts) and leaves (live fronds, dead skirt); never stretch an instance (the normals' inverse-transpose). `Palm.fx` is `Acacia.fx` plus a sway whose weight is baked into `TEXCOORD0.x`. **The wood is shut** (#974, `SeamAuditClosureTests`, `WindingCheck --openness`): the trunk runs on `FOOT_SINK` (1.2 trunk radii) under the ground and ends in a disc, so a dune that falls away under the low side of a leaning palm shows a skin and not the inside of a tube (the lip #754 describes), and the boot closes over its top; only the fronds are open, as sheets.
- ⚠ **`SwayStrength` and `PalmShading` are per-DRAW arguments** (`DrawPalmPart`). `TEXCOORD0` is a sway weight only because `PalmMesh` bakes one; `LatheMesh` (`RockMesh` and the moss cap) writes `s / segments` there, so rocks drawn at the palms' strength sheared open.
- **Planting**: dry sand only, clumped by `ScatterSpacing`, sunk a fraction into the sand. ⚠ **The crown, not the root, is tested against the menu orbit** (#555): the bow and lean carry a crown several units off its root, so a spot is a candidate only if the crown's position less `PalmMesh.FrondReach` stays outside `PalmConfig.OrbitClearance`.
- ⚠ Rocks and dressing draw from the same rng stream after the palms, so a change to the palm loop re-rolls the beach behind it.
- **Lean is seaward** (`0.08 + 0.55 · r₁ · r₂`, axis biased to the outward bearing). ⚠ `SceneRenderer.PalmWorld` must yaw **before** the lean: row-vector matrices apply left to right, and yaw after the lean threw the bias away.
- **Tall palms** (#555): `Height` 22, with trunk and crown raised much less (`TrunkRadius` 0.38; 0.46 was a colonnade) and a bole at the foot rather than a taper. The chapter tour's lagoon stand rides at 20° ([viewpoints](viewpoints.md)).
- **Real palms** (#557): `PalmMesh.AddFrond` builds a midrib and `LIVE_PAIRS` leaflet pairs, clamped so no tip passes the frond's end, which keeps `FrondReach` and every placement unchanged. ⚠ Leaves are single-sided with `CullNone` and `Palm.fx` flips the normal by `SV_IsFrontFace` (the old double-sided strips lit their underside as their top). ⚠ The trunk is one swept tube: separate segments left a slit at every joint. `TEXCOORD0.y` carries the part (`PalmMesh.Part`); take `fwidth` before the per-part branch.
- **Rocks**: a `RockMesh` plus a moss cap, planted in the band straddling the waterline.
- **Dressing** (#445): `TropicalDressingConfig` plants 250 bushes, 520 tufts and 26 driftwood pieces with the savanna's meshes, none swaying. ⚠ Big scrub (1.7) read as beanbags and a savanna-proportioned bush as a flat puddle (shipped: 1.05, height near its width). Not built: the reef (needs a bed texture the water shader samples), hut, outrigger.

## Costs to compare against

- Desktop (RX 6900 XT), Testbed `scene=tropical sceneseed=0 sky=1 nopost nocap`, 1600×900: play camera 2.97 ms at ssaa 2, low grove camera 2.68; the Low tier's program (`detail=0 ssaa=1`) 1.69, so there is no reduced path. The laptop is not measured; if it misses budget, make `LIVE_PAIRS` a `PalmMesh` parameter.
- The play camera pays for the beach's detail while seeing almost none of it (the coping hides the near sand; instances are submitted regardless).
