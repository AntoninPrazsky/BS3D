# The sea under the surface

Part of [The scenes](../scenes.md).

What happens where the sea meets something solid or the lens: a ball that falls into it, the drain's standing pool, the camera diving under. The water itself is in [The sea](sea.md). Everything here rests on one fact: **the open water writes no depth** (`DepthStencilState.DepthRead` in `SeaBackdrop.Draw`, restored to `Default` after it). The island is drawn after the sea, is opaque and owns its own depth.

## A missed ball sinks instead of being cut off (#131)

While the water wrote depth and drew opaque, a ball that missed the island was gone in the single frame it crossed the surface. Both halves of the fix are needed:

- the open water gives up its depth write, so the ball's pixels run below the waterline at all;
- `ApplySeaSubmerge` (`InstancedModel/Lighting.fxh`) lerps the shaded colour towards the deep-water tint and drops the alpha to nothing over `SEA_SUBMERGE_FADE` (3 world units) below `SeaLevelY`. The tint is `SeaSceneConfig.WaterDeep`, the colour the water shades with, so a ball converges on what is in front of it under any dome. The band is short because the mean level sits at -13 and the drain's `KILL_PLANE_Y` at -42: the ball is off screen from about -16.

`SceneRenderer.ApplySeaSubmerge` pushes the four uniforms (level, fade depth, tint and, since #159, `SeaLensSubmerged`) onto the shared instancing effect (the mirror of `CloudField.ApplyTo`), and **all three hosts, the map editor included**, call it once a frame in the scene block. Off the sea it pushes `SeaFadeDepth = 0` and the shader gates the whole block on that being positive: a uniform branch, one path per frame.

- ⚠ **The output is premultiplied.** Lowering the alpha while leaving the colour standing turns every faded pixel **additive**: the `End` release of a big map piles hundreds of half-sunk balls into the drain and their residues stack into a pale glowing mush. Both paths scale the faded colour to zero together with the alpha.
- **The fade is one function**, `ApplySeaSubmerge`, which `MainPS` and every ball technique call, so there is no second copy to keep identical; the release below is folded into `submerge` rather than applied after it, so the colour and the alpha always take the same figure.

## The fade is released as the lens goes under (#159)

The drop cinematic can film the sea from **below** (`OpenBelow`), swinging the eye well under the mean level to follow a released cluster down the drain. There the murk owns the frame and dissolving objects inside it as well would delete the cinematic's subject. So the fade is multiplied by `1 - SeaLensSubmerged`, and that uniform is **the same number the tonemap gets for its murk**: `SceneRenderer.LensSubmergedAmount` (0 above the water, 1 seven units under, `UNDERWATER_FADE_DEPTH`).

- **One number answers "is the lens in the water" for both effects.** Both hosts call it for the murk and pass it to `ApplySeaSubmerge`. The **map editor passes 0**, correct there because it has no murk to hand over to.
- ⚠ **The release must be precisely zero for any lens more than half a unit over the mean level.** A first attempt released the fade over a 1.5-unit band measured from `EyePosition`, which put the release above the surface: the fade was 93 % gone a tenth of a unit over the water, and the whole submerged drain shaft then drew straight through the standing pool (#132's composite). Every shipped camera is above the bound: the Game's play lens is pinned at Y = -7.4 (`GameCameraFit`'s `LENS_FLOOR_Y`), 5.6 units over the mean.
- Inside that last half unit the release follows the murk, on purpose: the crests reach about +-2.4, so a lens at the mean level is as likely to be in water as above it.
- Re-check on static geometry, no falling ball needed: the drain's glass cone and bottom gold band run through `MainPS`'s call of the fade. From `campos=0,-18,24 camtarget=0,-8,0`, five units under, the cone must be drawn, not cut dead at the waterline.
- Open: a ball watched from under water stays drawn to the kill plane at -42 and is culled in one frame (six other `OpenBelow` scenes show the same pop), and for the top quarter of the cinematic's elevation roll the eye is still above the water while the subject is metres under.

## The drain holds a standing pool of the same water (#132)

The funnel is a shape the camera sees *into*, so the open-ocean swell showed through its glass. The cut is an **annulus, not a disc**: `Sea.fx` keeps the open sea past `IslandHoleRadius` and the pool inside `FunnelPoolRadius` (one `clip` on the better of the two signed distances) and discards only the ring hidden inside the island's stone. Clipping the whole footprint left the drain bone dry, and the water must stand in it wherever the glass cone crosses the mean level.

- The pool radius is the cone's own at `SeaLevelY`, derived in `SeaBackdrop.Draw` from `ArenaIsland`'s figures (the same straight span `FunnelMesh` is built from, so water and glass cannot drift), buried `POOL_WALL_BIAS` (0.15) into the glass so the drawn cone's faceting (`FUNNEL_DRAWN_SEGMENTS`, 256; the 64 of `FUNNEL_SEGMENTS` is only the collision mesh) can never open a sliver of sky.
- **The swell dies before the glass, keyed on the rest position.** `CALM_BAND` (4, about one grid cell) under the island's rim takes the amplitude to zero, so the pool is exactly the flat rest grid at `SeaLevelY` and its clipped rim cannot breathe. Inside, `POOL_CHOP` (0.18) of the wind chop survives as capillary ripple; foam and the crest subsurface term are zeroed (the damped crest signal parks at 0.5, which a low `FoamCrestStart` would read as foam over still water); the body colour is biased to the deep tint (a still column in a drain reads dark).
- **The water meets the glass in a meniscus**: over the last `MENISCUS_BAND` (0.9) the normal curls up the wall (`MENISCUS_TILT`).
- **What continues below the pool sinks into it**: the cone and gold band draw after the water, so `MainPS` gives the plain-material path the balls' fade.
- The map editor keeps `TerrainHoleRadius` at 0: no cut, no pool, `FunnelPoolRadius` pushed but ignored.

## The underwater murk

The free camera can dive under the surface (in the Game, the drop cinematic's lens does), and only the sea gets an **underwater murk**. It is a post-resolve tint in `Tonemap.fx`, applied in **linear light before the ACES curve** so the submerged scene rolls through the same highlight response: the frame is absorbed towards blue-green (`UNDERWATER_ABSORB`, a multiplier under 1) and the water's in-scattered glow is added (`UNDERWATER_INSCATTER`), both scaled by `UnderwaterAmount`.

- `UnderwaterAmount` is set on the CPU **only in the sea scene**, ramping 0 to 1 over `UNDERWATER_FADE_DEPTH` below the mean surface (the ramp starts a touch above it so partial submersion tints), and is **0, a shader no-op, in every other scene**.
- The tint is uniform: no per-pixel depth in that pass, so no distance fog. The spray billboards double as suspended bubbles.
- A **peripheral blur** goes with it: an 8-tap golden-angle spiral disc whose radius and blend grow as `edge^2 * UnderwaterAmount`, 0 at the frame centre. It sits inside a branch on the `UnderwaterAmount` **uniform**, so there is no cost above water and no divergence. The overlay and crosshair draw after the resolve and stay sharp.

## The school of fish and the light shafts (#760)

An easter egg for a sea looked at from below, and what the Sea's chapter intro dives to see (#759, *the depths* in [Chapter intro: the other scenes' shots](../game-feedback/chapter-intro-scene-shots.md)). Both draw **only with the lens under every trough of the swell** (`SeaBackdrop.LensUnderEveryTrough`: deeper than 4.2 of the wave amplitude plus the chop under the mean level), so in play, where the lens never goes under, they cost nothing.

- ⚠ **Do not gate them on `LensSubmergedAmount > 0`.** That opens half a unit above the mean level, and the surface writes no depth, so a dive's first frames drew the school through the water as through glass.
- **`FishSchool` + `Fish.fx`**: `FISH_COUNT` (110) fish in one static buffer, one draw, no CPU per fish. One `FishMesh` (a six-sided torpedo deeper than wide, forked tail, dorsal fin, 75 triangles, flat normals, `CullNone` since the fins are single sheets) repeated, each vertex carrying its fish's index; the shader hashes each fish's radius (13-21 round the island's axis), height (a 6-unit band round the funnel's tip, `FUNNEL_BOTTOM_Y` + 2) and phase, and swims it round the ring at one nominal linear speed (`SWIM_SPEED` 2.0 a second since #903, about a body length a second as a schooling baitfish cruises; it was 3.2, and the owner found them too fast; the wobble and the breathing radius below swing a fish's own between about 0.4 and 1.6 times that on the outer ring, over periods of 9 and 20 seconds), so the ring turns as a body and the inner fish lap the outer.
  - ⚠ **The ring angle is built from the fish's BASE radius** (`Time * SwimSpeed / baseRadius`), the breathing radius moves the place only (#903). Built from the breathing radius, `Time / radius(Time)` has the rate S/r - Time*S*r'/r^2, whose second term grows with the session clock and ran the angle backwards for every fish whose radius was growing, so they swam tail first (the nose is the tangent of the growing angle): from about a minute into a session, roughly four in ten of the school by five. A fresh Testbed capture never shows it (its clock starts at 0): use `clock=600` (`docs/testbed/capture-and-measurement.md`). The wobble `0.08 sin(0.7 t)` can pull the rate back by at most 0.056 rad/s against S/21 = 0.152 at the outer edge, so the angle advances for any speed above 21 x 0.056 = 1.18 a second; `FishRingAngleTests` holds that for the constants the school states, so a slower school must stay above it.
  - ⚠ The body wave must **travel from head to tail** (phase `t + x`). `t - x` ran it up the body and the fish seemed to swim tail first. That is the other cause: with both in place every fish moves nose first at any clock (checked at 0, 600 and 3600 s, below).
  - **Checking it on frames** (#903): a throwaway edit of `Fish.fx` that paints a fish pure red when its place 0.2 s later lies behind its own nose (dot with its `forward`) and its head third magenta. The frame passes when no fish is red at `clock=0`, `600` and `3600` (`sceneseed=0`, the camera above, `nopost`); the old expression painted a good part of the school red from `clock=600` on. No probe is kept in the tree: recreate it from this description. `FishRingAngleTests` pins the shader's three lines (the base radius, the breathing radius and the angle) by their text, so a revert fails the suite without a frame.
  - Countershaded (dark blue back, silver belly), faded into `WaterShallow` over `FADE_DISTANCE` (60). ⚠ At 26 the fade swallowed the whole ring from 40 units out, because the underwater post is one tint for the whole frame with no depth.
- **`SeaLightShafts` + `LightShafts.fx`**: 40 additive quads hanging from the surface along the sun's direction with its slant halved (the water's refraction, rounded down so a low sun still reads as light coming down), each turned about its own axis to face the lens. They stand in an annulus from just outside the island's rim to 110, most near its inner edge (radius on the square of the hash). A sun under the horizon puts none down.
  - ⚠ **Drawn with the overlays** (`DrawOverlays`, after the island, gun and balls) and depth-read. Drawn with the school, before the opaque scene, the stone painted over every shaft between the lens and it.
  - The near fade is per pixel; per vertex it could not fire beside a shaft's middle (its corners are 42-70 apart).
- ⚠ **The water under the horizon is a flat pale plain** (the dome's lower half through the murk's single tint; there is no seabed and nothing gives depth to the view down), which is why the intro's shot looks up. Not fixed.
- Cost where they are drawn (Testbed, `campos=24,-36,6 camtarget=0,-20,0`, 1600 x 900, `fpscap=400`, two pairs): 2.80 and 2.80 ms with the school, 2.80 and 2.82 without.
