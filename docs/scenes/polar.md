# The polar icesheet

Part of [The scenes](../scenes.md).

The eighteenth `SceneKind` (#222): a flat white expanse carved into sastrugi by the wind, with a crevassed pressure front at distance, under a low sun. Made of `PolarBackdrop`, `Polar.fx` and `PolarSceneConfig`. Launch: `scene=polar` (alias `ice`). A solid-terrain scene that keeps its dome, built on the desert's machinery a third time: a camera-centred `AcquireGridMesh` grid snapped to its cell (`POLAR_GRID_N` over `POLAR_EXTENT`, a cell of about 2.86 units), displaced in the vertex shader, the normal taken **per pixel** from the height gradient. It has a `TerrainMirror`: run `mirrorcheck` after touching its height.

**White in reflection, cyan in transmission: that opposition is the whole look.** The sky reflects off the glaze as white; light that goes into the ice comes back cyan, deeper the further it travelled. A crevasse is a slot that **glows**.

## ⚠ Rules and traps

- **The shadows carry the picture.** An all-white field flattens into one tone under ACES. So the snow's albedo is not 1 (0.72/0.76/0.82, cool), the shadow is the **sky's** colour (blue), and the sastrugi carry their own trough shading independent of the sun: under a high sun a flat field's `ndotl` is nearly constant, and a trough term of +/-0.12 photographed as white paper (it is +/-0.42). High `DriftAmplitude` and low `AmbientStrength` make carved snow, not a printed pattern.
- **The sastrugi are a SURFACE, not terrain.** At their real scale one is about one grid cell, so the mesh cannot hold it. Geometry carries the broad swells and the front; sastrugi are a normal perturbation sampled in **wind space** and stretched (`DriftStretch`), because an isotropic field reads as gravel.
- **The pressure front is tilted plates in a world-space lattice** (`SlabSize`, `SlabTilt`, lean hashed per plate, about 11 units tall). Smoothed it reads as breaking waves; quantised in **polar** coordinates the cells are wedges meeting along radial seams and read as unnamed columns ("a graphics glitch"). Real rubble ice is plates wider than tall.
- **A front, not a ring**: closed all round it reads as an arena wall. It covers a sector (`RidgeBearingDegrees`, `RidgeSpanDegrees`) and fades over its edges.
- **The cracked ice must be where the level is played.** Gating crevasses on the front's strain put them 300 units out. So crevasses open in **fields across the plain** (`CrevasseField`), and **blue ice** (`BlueIce`, `BlueIceThreshold`) marks scoured glacier, its edge a narrow **rim** (wider photographs as a halo), the sastrugi flattened on it. The island's clearing stays whole (`ClearingRadius`): a crack under the arena would be a hole the player cannot fall into.
- **⚠ Size a crevasse to what the grid holds.** A slot narrower than several cells falls between vertices: `CrevasseSharpness` 8 (a 3.3-unit slot) with `CrevasseDepth` 8 smeared into a V with near-vertical flanks that drew as ice **brighter than the snow**. It is 4 (a 6.7-unit slot) and 3.
- **It reads as a crack** only when narrow, with walls the sun can shade, a dark throat (`CrevasseDarkening`), transmission peaking **on the walls** and the walls' Fresnel held back (a steep face at a grazing angle mirrors the horizon). Wide and shallow it is a welt.
- **Judge it in `BS3D.exe`**, not the Testbed's F10 camera, which stands low enough behind the gun that the island hides the plain.
- **The sparkle** (one hash per fine cell) is faded out well before cells reach pixel size, or it crawls (the desert's grain rule).

## The dome is the biggest dial

Dome **13** (`POLAR_DOME`): a 13 degree sun over a teal horizon into indigo rakes the sastrugi, makes the front glow cyan and the crevasses slots of light. The rule is **a low sun and a cold horizon**. Dome 11 (a high sun) flattens the field, 16 goes golden-brown, and 17 is white on white: **transmission needs the sun behind the ice**, and a high sun is never behind anything.

## Open, the owner's call

- **Blue-ice brightness.** A scoured patch photographs as a pale cyan lagoon brighter than the snow; references show it slightly darker. Holding back its reflection (as a crevasse wall's is) was tried and made it **brighter** (a lower Fresnel hands back `snow * 0.55 + transmission`). The transmission is deliberate (it answered "the glowing cracked ice is nowhere"), so the lever is `TransmissionStrength` and `BlueIce`'s weight in `thickness`. The measurement is in `Polar.fx`.
- **Cast.** The lavender cast is the dome's (references are gold-and-blue).

## Costs

- **Against the desert** (Testbed, laptop, ssaa 2, 1600x900, `fpscap=400`): **+16 %** (32.6 against 28.2 ms); crevasse fields and blue ice add about six gradient-noise evaluations a pixel.
- `CrevasseField` once sampled the pressure ridge itself; passing it down bought 1.2 ms. The next lever: the crevasse mask is far lower in frequency than the normal taps, so all three could share one sample.
- Desktop, play pose, 3840x1600 ssaa 2, `nopost nooverc`, `fpscap=400`: about 12.3 ms.

The ambience is a long flat wind with spindrift, not the mountain's gusts. Airborne diamond dust is not built.
