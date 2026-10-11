# The city: draw order and windows

Part of [Rendering](../rendering.md).

## Drawing the city near to far

The city is **one instanced draw**, but of `City.PrepareVisible(camera)`'s `Visible` scratch array: the buildings in the frustum, **ordered near to far**.

- **The frame was pixel-bound.** At 3840x1600 the city cost 13.9 ms at `ssaa 2` and 4.4 at `ssaa 1`: ~12.7 ms was pixel work. Culling is the cheap half (off-screen buildings cost no pixels); what costs is a building **on screen and behind another**, whose pixels run the full city shader and are overwritten. **Ordering is what pays**: 13.88 to 7.66 ms mean and the worst second from 32.3 to 101.5 FPS (level, firing, 3840x1600, `quality=high`).
- **Reference machine** (Ryzen 7 5700U iGPU, windowed 1600x900, dome 13, #67): `High` neon 103 to 27.5 ms, city 76 to 23.0; `Medium` 10.5 / 9.3; `Low` 9.6 / 8.4. The block-radius sweep is flat now (14/12/10/8 blocks: 27.5-28.6 ms), so `Low` keeps the authored skyline (`Game/QualityLevel.cs`).
- **`CityPS` must neither `discard` nor write depth**, or the hardware shades before the depth test and the ordering buys nothing. The depth prepass (`DrawDepth` / `InstancedDepth`) stays unused: no hidden pixels are left to reclaim.
- **The sort is a counting sort and approximate** (`DEPTH_BUCKETS` 256; a bucket out of place costs overdraw, never correctness), keyed on the distance to the building's **near side** (centre less radius), since the near surface does the occluding.
- **No allocation:** bounds taken once, buckets and `Visible` allocated with the city, and the **`BoundingFrustum` is held and re-pointed** (it is a class; `new` per frame allocates). Bounds come free from the world matrix (`M41..M43` centre, half the `M11/M22/M33` diagonal a conservative radius).
- The `logfps` line carries `city N/M` (in-frustum of total, ~1090 of 1788 from the play camera). **Check a culling change by image invariance**: a fixed camera (`campos`/`camtarget`) with and without it must differ only in clouds, lit windows and the cluster; every tower silhouette by exactly zero.

## The city's windows

`CityPS` (`InstancedModel.fx`) lays windows out per building and decides per window whether it is lit; the look is `CitySceneConfig`'s `Window*` properties.

- **The raised moulding is invisible at tower distance** (#435): `WindowFrameWidth` is ~0.085 world units, one pixel at 60 units at 1600x900, and `resolvable` switches it off past that. What a window reads by there is four **flat** cues: the **surround** in its own tone (`WindowFrameTone`, a multiple of the wall albedo so neon-city dark walls keep dark frames), the **sill** and its shadow, the **reveal's shadow** along the head (darkens the pane, halves the lamp behind it), and **glazing bars**.
- ⚠ **Box-filter each piece (`WindowSpan`), do not smoothstep it**: a smoothstep across a feature narrower than a pixel still peaks at a half, so a sub-pixel bar draws as a shimmering half-bright line; the box filter gives it its true fraction and sub-pixel pieces blend into the facade's average.
- The Game's tier still sets `WindowFrameWidth` (0 at `Low`, which removes moulding and surround); the sill, reveal and bars draw on every tier. Cost +0.05 ms city, +0.06 neon (1600x900 ssaa 2, `nocap`, against `main`).
