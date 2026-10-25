# The sea

Part of [The scenes](../scenes.md).

The second setting: the stone island floats a little above open water. The part below the surface (the standing pool in the drain, the sinking ball, the murk, the fish) is in [The sea under the surface](sea-underwater.md).

## What it is made of

- `SceneKind.Sea`: `BS3DLibs/Prazsky.Shaders/Content/Shaders/Sea.fx`, drawn by `SeaBackdrop` (`Render/Scenes`). Reached with NumPad2 (the scene cycle) or `scene=sea`; a synthetic NumPad press never reaches the SDL window, so a script uses the argument.
- `Sea.fx` is **also the tropical lagoon's water**. Its `ShallowBias` uniform belongs to that scene: it lifts the body-colour mix so a basin shallow across its whole width takes the pale colour wherever it is seen. The open sea sets it to 0, and `lerp(x, 1, 0)` is bit-exactly `x`. So does `ShoreClarity` (the lagoon's shore, #968, [The tropical beach](tropical.md)): 0 on the open sea keeps the surface opaque bit for bit (`body * (1 - 0)` and an alpha of exactly 1), and its branches in both programs are never taken.
- Drawn opaque, first in the scene block where the city would be, `CullNone` (one open surface, read from above and through the crests), **depth-read, not depth-write** since #131 (the reason is in [The sea under the surface](sea-underwater.md)). The arena glass composites over it unchanged. Shader Model 5.0.
- The dials (the wave, chop, foam, subsurface and water-colour properties, the mean level `LevelY`, and the nested `SprayConfig`) sit on `SeaSceneConfig`, so the map editor draws the same water and spray. The editor keeps `TerrainHoleRadius` at 0 (no pool).
- **The sea mirrors the sky**, so its whole mood follows the dome: a bright dome gives a bright, breezy sea. It therefore enters under a darker dome, the violet/teal dusk that `SkyDome.SceneDefault(SceneKind.Sea)` returns (dome 13), on `scene=sea` or a NumPad2 switch into it, unless `sky=` overrode it. NumPad1 still cycles domes freely from there.
- Everything runs off the wall clock, so the sea keeps moving while the simulation is paused.
- **No shipped level is set on the sea.** In the Game it is reached only through the scene picker and the front end's random pick.
- ⚠ A per-pixel diff of two sea launches proves nothing: the swell and the cloud deck run off the wall clock, so two launches of the same build differ in about 45 % of their pixels. Compare regional means (62 400 water pixels moved by a third of a level between builds that should be identical).

## The geometry

The water is **real geometry**: a camera-centred grid (`AcquireGridMesh`, shared with the desert, mountains and meadow), snapped to a cell each frame so it does not swim, displaced in the vertex shader by a sum of **Gerstner (trochoidal) waves**: sharp crests, rounded troughs and the horizontal pinch that sells a wave over a plain height field. The horizontal **Jacobian fold** comes from the same evaluation.

- The wavelengths are **short relative to the scene** (a ball is about 1 unit), so several crests fall inside the visible water. A 100-unit swell shows only a corner of one wave at this scale, which is why the first flat-quad version read as a low-resolution mirror.
- The mean level (`SeaSceneConfig.LevelY`, -13) sits well below the island so the crests have headroom without poking up through the stone. The waves **fade to flat** between `WaveFadeStart` and `WaveFadeEnd` so the far sea settles into a hazed horizon, not a jagged fringe.

### The swell is twenty waves, and the normal is the pixel's (#674)

Six hand-picked waves read as a lattice from above: six plane waves are a quasi-crystal (the slope field correlated 0.971 with itself 68 units away), and the two shortest (8.5 and 5.5 units) were evaluated only at the vertices, `SEA_EXTENT / (SEA_GRID_N - 1)` = 4.2 units apart, which is under the grid's Nyquist limit, so they folded into a honeycomb on the grid's own pitch.

- **Twenty waves** (`WAVE_*` constants in `Sea.fx`, drawn once from numpy's `default_rng(1)`): the dominant swell is 52 units along (1, 0.35), so the foam streaks keep their fabric; nineteen more on a jittered geometric ladder from 46 down to 9.6 units, amplitude proportional to length^0.8, within 75 degrees of the wind, at random phases. The strongest self-likeness inside 250 units is 0.614.
- ⚠ **`GridCarries`: a wave the grid cannot sample must not move the vertices.** A wave's displacement fades out from four cells a wavelength to two. `TerrainPass` hands the shader the spacing as `GridCell` (again if a tier ever retakes the grid).
- **The normal**: the eight longest waves (6.5 cells or more) are accumulated at the vertices and interpolated; the twelve shorter are summed per pixel at the pixel's rest position (`SwellNormal`), each faded against the footprint.
- ⚠ A `[loop]` that stops at the first unresolvable wave cost **more** than the unrolled sum (+1.17 ms against +0.53): it stops the per-wave constants folding. All twenty per pixel looked the same and cost 0.23 ms more.
- `WAVE_STEEP_WEIGHT` holds the summed pinch where the old set had it. ⚠ **The crest is normalised by the height's spread** (`CREST_NORM` times the root of the summed squares), not by the sum of the amplitudes: with the sum, twenty waves parked the crest near 0.5 and the whitecaps all but went (13.1 % of the surface above a crest of 0.7 fell to 2.9 %).

## The shading

- **Wind chop**: four band-limited octaves of **gradient noise stretched three times along the crests**, scrolled downwind, tilting the normal through `PerturbNormalFromHeight`. ⚠ Not sines: crossing sines are a lattice of diamonds by definition (four of them repeated exactly every 50.6 units). `CHOP_NOISE_GAIN` gives the noise the sine's slope across the crests, and its fade against the pixel footprint reaches zero at half a noise cell (`Fbm2BandLimited`'s rule).
- **Whitecap foam.** Its *permission* is the crest gate (past `FoamCrestStart`); its *shape* is a streak field, band-limited fbm combed along the dominant swell's crest line (`Fbm2Combed`, the perpendicular of `WAVE_DIR[0]`), so foam reads as wind-torn lanes along the crests.
  - ⚠ **Never threshold the crest height for the shape** (#128): the sum was a localized round bump and a threshold on a bump is a disc drifting at phase speed. The gates set only the foam **density**.
  - ⚠ **The streak threshold window must stay above the field's mean at every density.** `streaks` is an fbm plus 0.5, so it sits near 0.5; a window sliding to (0.285, 0.49) at full density paints half the area at one value, which is the blob again at a larger size. The window slides from the top eighth of the field to its top quarter.
  - Where a lane fires it goes to full white (`saturate(density * 2.2)`). Scaling alpha with density spreads a little foam over a lot of water, which reads as sheen or an oil slick. A white cap gathers light from the whole hemisphere, hence `FOAM_AMBIENT_GAIN` (1.9) and `FoamColor`.
  - ⚠ **The Jacobian fold gate never fires** (nothing folds on either surface: the Jacobian stays at 0.59 or more on the sea and 0.79 on the lagoon, against thresholds of 0.5 and 0.55). The whitecaps are the crest gate's alone and `FoamStrength` does nothing at these configs.
- **Body colour is keyed on the crest**, not only on which way the surface faces: `lerp(0.12, 0.85, crest)`, whose mean is the old constant. Every reference has dark troughs and paler flanks.
- **Subsurface scattering** glows the thin water at the top of a crest when the sun is low behind it and the eye looks into the water. It is spread across the sunward quarter of the view (a third power on `dot(viewDir, -SunDirection)`; the fourth power put it only where the glitter path already owns the frame).
- A Fresnel **sky reflection** samples the dome's two-colour gradient in closed form (`SkyRadiance`'s trick), a sharp **sun glint** sparks where the reflected ray hits the sun, and the **cloud shadow** is `CloudSunlight` off `Clouds.fxh`, so the water darkens under the cloud overhead. A horizon haze fades the finite grid into the skyline.
- ⚠ **The haze must not make the far sea MORE saturated than the near sea**, which is backwards in every photograph. Water at the horizon is a mirror at grazing incidence and shows the whole sky including the cloud deck, but `HorizonColor` is the clear-sky band. The haze target is pulled towards its own luminance by `HAZE_DESATURATION` (0.55, a static constant, not a dial): brightness kept, saturation removed. It reaches the tropical lagoon on purpose, since both draw through `Sea.fx`. A control ruled out the dome: under the same dome the desert's horizon matches the sky's red channel exactly.

## The spray

Blown spray and spindrift: `Spray.fx`, drawn by `SceneRenderer.DrawOverlays` in the sea scene. A static buffer of billboards animated entirely in the vertex shader, in a thin slab that follows the camera in XZ and clings to the surface in Y (`SprayLevelY`, just above the mean), whipped downwind (`SprayWind`). One buffer carries fine droplets and larger, fainter mist wisps. It is depth-read, runs off the wall clock and caches its six uniform references at load (`SeaBackdrop`).

- Each billboard is **stretched along the wind as it projects onto the screen** and thinned across it, the area preserved, so the particles read as blown streaks. A round camera-facing billboard reads as a white snowball (#169). The stretch follows the wind's sine against the view, so a head-on wind foreshortens to a disc on its own.
- ⚠ **`GLARE_THRESHOLD` is 0.55, and at a grazing angle the view ray threads hundreds of particles that stack to full opacity**, so any spray colour above it blooms into a starfield however low the per-particle alpha. `SprayConfig.Color` stays just under it (luminance 0.534). A brighter spray would need a raised glare threshold or a post-resolve pass, which would lose the depth occlusion that keeps it behind the platform and cluster.
- ⚠ **The blend is premultiplied.** `BlendState.AlphaBlend` in MonoGame is `One, InverseSourceAlpha`, so `Spray.fx` returns `float4(SprayColor * alpha, alpha)` (#675). Returning the straight colour draws every droplet as a hard white lozenge, because the feather and the opacity never soften anything. Premultiplying alone took most of the spray away (a droplet's peak fell to about a quarter), so `Color`, `Opacity`, `DropletSize` and `ParticleCount` were retuned with it.
- ⚠ **The nearest particle is always the biggest on screen.** The slab is centred on the camera, so a particle can sit a world unit from the lens, where a 0.1-unit billboard covers forty pixels and draws its own outline; shrinking the size dial only shrinks every blob. `Spray.fx` fades a particle out over its last approach (nothing inside 5 units, full by 20).

## The references and what they decided (#503)

References were rendered locally (`design-references`, not in the repository): a dusk ocean, breaking crests, a low sun's glitter path, a backlit crest, wind-blown spray, the sea from height. They left the geometry alone and said **the water is darker than it was drawn and the white on it is whiter**. The decisions are in the shading list above (the crest-keyed body colour, the rarer and stronger foam, the third-power subsurface term, the desaturated haze) and the spray list (the lens fade).

## The oil platforms (#969)

The owner: objects found at a real sea, oil platforms first (lower priority, his words). `OilRigs` (drawn by `SeaBackdrop` after the water, opaque, writing depth) stands two far out, shaded by `Rig.fx`; references in `C:\Users\panrd\AI\sd\out\969-zimage` and `969-klein`.

- **Where**: beyond `WaveFadeEnd` (780), where the water is a flat plane at `LevelY`: the nearer `NEAR_DISTANCE` 820 out on `NEAR_BEARING`, behind the play view the Testbed opens on, a smaller one 1150 out on another bearing. Sizes at about two units a metre.
- **Built of boxes**: four thick yellow legs, bracing as tubes (a horizontal and an X of diagonals on every side, two storeys), a deck of two levels with white and orange modules, a red lattice derrick, a crane, a flare boom with its flame, a helideck and steady lamps (#401). ⚠ Bracing as lattice panels with the sky's colour between the members read as perforated sheets tinted by the sky: the derrick's gaps are darker steel.
- **The water** (`Rig.fx`): the surface writes no depth (#131), so the legs are `clip`ped at the waterline (the fault #761 fixed for the fireworks: anything under the water drawn after it shows through as through glass), with a band of foam where they meet it. Standing where the swell is flat makes the cut a straight line the water agrees with.
- **The air**: the sea's horizon colour at `HAZE` 1700, lighter than the water's 700, or a rig a kilometre off is gone.

## Costs a change must compare against

Desktop, 3840 x 1600, ssaa 2, play-height view, alternating pairs (#674):

- the sea: 6.55 ms before the twenty-wave spectrum and per-pixel normal, 7.50 after (+0.95; about 0.3 of it the swell's per-pixel normal, the rest the chop's noise);
- the tropical lagoon: 10.83 to 11.09 (+0.26);
- **the laptop is not measured**; an estimate by fill rate puts the sea's `Low` near +0.6 ms on a scene that sat at 14.2 against the 16.1 budget.

The shading retune (#503) cost about 0.3 % (6.74 / 6.75 against 6.76 / 6.77 ms, desktop, fixed camera, 3840 x 1600 at ssaa 2, `nopost nooverc`, `fpscap=400`). ⚠ At ssaa 1 both builds sat on exactly 2.50 ms, the cap, which measures nothing; raise the load before a pair means anything. The spray's blend fix (#675) cost one multiply (laptop, 22.36 against 22.24 ms).

`Tools/LatticeProbe` on three pinned captures (`sceneseed=3`, `nopost`) measures whether the field still reads as a repeating grid: the sea from above fell from 71 % to 32 % of tiles above its null, near from 18 % to 9 %, play height from 17 % to 12 %; the lagoon from above from 29 % to 7 % (what is left is single-orientation crests, which a wave train is allowed).
