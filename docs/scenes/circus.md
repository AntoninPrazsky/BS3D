# The big top

Part of [The scenes](../scenes.md).

The twenty-first `SceneKind` (#690): **the inside of a circus tent between shows**: canvas sweeping up from a curtained wall to a gilded crown on four king poles, red seats round a sawdust ring, strings of warm bulbs, coloured spotlights in dusty air, and the island as a performer's tub (the `Plinth` shape). Made of `CircusBackdrop`, `Circus.fx` and `CircusSceneConfig` (groups Tent, Ring, Seating, Lights, Lighting). Launch: `scene=circus` (or `bigtop`). It is the soft-lattice chapter's stage.

**In both families, the Moon's and the Grid's way**: the canvas *is* the sky (no dome, cloud deck or weather, its own light rig), and the sawdust is a floor at `FloorY`, so the drain's glass is backed by the pit shaft.

## What it is made of

- **Everything is analytic, nothing is marched**: quadrics hit in closed form, nearest wins (cones for the canvas and the seats' rake, cylinders for wall, curb and king poles, the floor, two radial planes for the performers' entrance). Shading on the hit makes cloth, velvet, seats and sawdust. **A seat is never geometry**: a row is a few pixels tall and a normal carries its profile.
- **⚠ The floor and the king poles write depth and nothing else.** The pass draws with depth off, so alone the sawdust is a picture a falling ball falls through. After the quad, an annulus from `TerrainHoleRadius` outward is drawn with colour writes off (`ColorWriteChannels.None`) into the depth buffer, and so are the four poles, as cylinders a shade thinner than drawn: the front end's wide orbit (`BackdropScreen.WideRadius`, about 46.7) passes the poles' radius (46), and a pole the lens stood outside of was drawn **behind** the island. This is why the scene is not `DrawsAtDisplayResolution` (that target has no depth).
- **⚠ Every pattern round the tent is a whole number of periods**, because the bearing wraps from pi to -pi behind the island and a pattern not periodic over the full turn becomes a seam. The curtain's folds and the seats (2*pi*r / 1.15 is not whole) are rounded to a whole count, the creases sampled on a circle in noise space, the poles' grain read off the normal.
- **Panel colours blend across each boundary** over the pixel's width (`StripeMix`), or the roof shows a staircase; the roof stops at the crown (stripes converging on an apex are a moire).

## Lights

- **The house light** (warm, overhead) keeps canvas and seats readable. **The spots** (`SPOT_COUNT` five: three hold on the island, two rove the ring) are cones with an inverse-square pool about `SPOT_REACH` and a **beam integrated along the view ray in closed form**. ⚠ **Keep the beams narrow**: at 11 degrees half-angle the three island spots overlapped into one wash over every view (found with `detail=0`); they are 7.
- **The bulbs** are emissive points drawn as stars: per string, the bulbs about where the ray crosses its plane, each a closest-approach core widened to the pixel with its energy kept (a distant bulb is a steady point, not a shimmer). ⚠ **No halo**: only three bulbs are visited, so a halo lost pieces wherever the nearest bulb changed and stood on the canvas as blocks. The cores are past the glare threshold and bloom gives the glow.
- **The beams come out of lamps, and the lamps turn.** Each spot is a dark can with a lens (`HitFixture`) on a rod from a square truss ring (`HitTruss`), turning about the can's middle (`PIVOT_TO_APEX`, matched by `FIXTURE_BACK * 0.5` in the shader) with the ring placed by the pivots, so the rod meets it at every angle. The lens is `LENS_GAIN` over the lamp's colour, past the glare threshold, with a halo (`LampGlows`) weighted by how squarely it faces the camera. `UpdateSpots` turns all five on the wall clock, slowly; the island's three wander over the cap, where a pool can be seen, not the funnel's glass.
- **The footlights are real lights**: the drum is a vertical wall the overhead key grazes and stands **black** without them. `FOOTLIGHT_COUNT` lamps on the curb go through `SceneLights`, and the pass draws their lamps and pools from the same positions (`FootlightPosition`). **The island spots' pools** likewise: each of the three is a point light `POOL_LIFT` over its aim point (`SpotAim`, the answer `UpdateSpots` turns the lamp by). Four footlights and three pools (`POOL_COUNT`) make seven of `SceneLights`' eight slots; **the eighth stays free for the blast flash**.

## Costs

- At 2560x1440 it matches the other full-screen scenes (7.00 ms against the cavern's 6.88; `Sag_Chain.json`, `fpscap=600`, desktop, 2026-10-02): something common to them bounds the frame. The reduced program (`detail` at or under 0.5) drops the beams, the one part integrating five cones a pixel.
- **At 3840x1600 the lamps cost 5.4 ms**, the size that matters (the desktop panel, at the 75 Hz edge): 19.74 against 14.33 ms before them (`Full.json`, ssaa 2, `fpscap=600`, `campos=0,4,42 camtarget=0,-8,0`): dust noise 2.9, lamp intersections 2.95, glows 0.55. **Nearly additive, so this pass is work-bound, not occupancy-bound**: a cut pays on its own.
- **So gate the work**: dust behind `outer > 0` (the ray is in that beam), the rig behind `NearRig` (the ray's span in the rig's slab and distance from its axis), each lamp behind its own closest approach, glows behind `NearRig` widened by `GLOW_MARGIN`. After: about 0.5 ms over the pre-lamp frame.

The ambience is the canvas taking the wind outside, and the hall's quiet air.
