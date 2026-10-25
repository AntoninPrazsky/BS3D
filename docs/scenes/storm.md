# The storm

Part of [The scenes](../scenes.md).

The seventeenth `SceneKind` (#219): the arena hangs in open air among broken cumulus, with lightning breaking through the space between the cells. Made of `StormBackdrop`, `StormClouds.fx` and `StormSceneConfig` (groups Clouds, Surface, Flash, Air). Launch: `scene=storm`; `tour scene=storm` flies the chapter intro.

## What it is

- **Not a terrain scene, not a sky replacer.** It is `OpenBelow`: the drain looks out onto sky and cloud and the drop cinematic dives to about y = -66 (safe: `InnerRadius` keeps every cell at least 105 units out). It still draws the dome. `Weather` is `Clear` (`StormSceneConfig`'s constructor): the default `Scattered` would draw Earth cumulus above the arena too.
- **Dome 20** (`STORM_DOME`): a pale blue-white horizon, since at altitude it goes paler where every other dome warms.

### ⚠ Clouds are volume, never a height field

The first build was a displaced grid with turrets and was rejected on sight ("strange rocky land"). Three rounds of fixing its shape made a better landscape, never a cloud. A height field is a surface (one height per XZ): it can be lumpy but never **broken**, and its silhouette is always hard where a cloud's dissolves. Trap: `1 - |n|` is **ridged** noise (zero contours are lines) where `|n|` peaks at points (rounded blobs): one character decides rock against cloud.

So the clouds are soft-edged **billboard puffs clustered into cumulus cells**, alpha-blended (`BuildStormCloudBuffers`, capped by `STORM_MAX_QUADS`).

## The cloud field

- **A cell is built, not noised**: puffs on a profile widest through the middle and biased low, each radius shrinking as it climbs. Sizes are a squared roll over a wide band: ⚠ **balls of one size tile**, and a tiling reads as bubble wrap. Puffs are pulled in by their own radius (none detached on the rim); keep the random draws in order, or every cell moves and strikes land elsewhere.
- **⚠ Every puff carries the middle of its cell** (`MassNormalMix`): lit from its own disc a puff is a glossy ball, so the shader blends the reconstructed sphere normal towards the direction out of the cell's middle. The silver lining is gated on it, so only the cell's silhouette takes it.
- **`EdgeSoftness`: low is soft, high is hard.** Outline noise rides the quad's corner, not world space, or it swims.
- **The rim is scalloped** (#953; the owner: "the clouds still have visible circles"): `BILLOW_LOBES_MIN` to three more round lobes a puff on its own seed push the edge out at each lobe and pull it in at each crease, the creases in shade (`BILLOW_CREASE`), so a cell reads as the cauliflower of the references (`953-zimage`, `953-klein`) and not a heap of cotton balls. Analytic (one `atan2` and a cosine): ⚠ a 3D noise on each puff's reconstructed sphere did the same and cost the frame 3.5 ms, the puffs' overdraw being what it is; the scallops cost about 0.3 (Testbed 3840x1600, ssaa 2, `campos=0,-2,40 camtarget=-60,8,-160 fov=70`: 8.13 against 7.82).
- **Not yet answered** (#953's other three questions, against the references, which draw a storm dark, green-grey, with rain curtains under the cells): dome 20 is a bright high-air sky and no existing dome reads as a storm (swept: 3, 6, 8, 12, 15, 17, 18), and nothing rains.
- **⚠ The blend is premultiplied**: `float4(color * alpha, alpha)` under `BlendState.AlphaBlend`. Before #675 the bare colour was returned, every puff added its whole colour, cells summed to clipped white and every lobe wore an outline; the look first approved was that fault. A darker storm is `UnderShade`/`AmbientStrength`. Judge from a **far vantage** (`campos=0,-2,45 camtarget=0,8,-100`) as well as the play pose, which hides separate-ball cells.
- **The gaps are the subject** (cell count against `OuterRadius`, `PuffOpacity`); filling the volume evenly makes a lid. **Unsorted, depth-read, never depth-write** (a depth-writing billboard punches its quad out of what is behind it).
- **The field moves in the vertex shader on a wind-aligned band** (#532): cells over a band `OuterRadius` long and `BandHalfWidth` across, carried downwind at `DriftSpeed`, **wrapped** and **steered round the arena**, all as one offset per cell off its own middle (`StormCellOffset`). ⚠ Wrap per *cell* (per puff it tears a cell in half), in the far field that `STORM_FAR_THIN_START`/`STORM_FAR_THIN_END` thin out. The steer clears `InnerRadius` plus the largest cell's radius (`StormCellClearance`): the *puffs* must clear the arena. ⚠ A non-wrapping annulus drifted out of the sky (**time, not cell count**). `StormBackdrop.StormCellPosition` is the host copy of the offset, kept in step by hand.
- **⚠ The shared cloud field cannot serve a deck below the arena**: `Sky.fx`'s ray-plane crossing is not sign-safe from above, its horizon gate zeroes every ray at or below the horizon, and `CloudSunlight()` would hold the arena at `ShadowFloor` of sun. The `Cloud*` uniforms belong to the sky's weather, so the storm has its own and `StormBackdrop.Draw` does **not** invoke the cloud hook.

## Lightning

- **A strike is a pure function of the wall clock**: `SceneRenderer.StormFlash(time)` puts one in each period, hashing start and size off the period index (`StormStrikeSchedule`, shared with the thunder), with a flicker for return strokes. No state, so all three executables agree; `public` for the sound.
- **It lights the arena as a `SceneLights` point light far below** (`TryGetStormFlash`, like `TryGetMoonEarthshine`; self-gating in time). ⚠ **The range must comfortably exceed the distance** (3x recipe): attenuation is `saturate(1 - dist/range)^2`, exactly 0 beyond it. `GlowReach` is its own dial.
- **⚠ A strike goes off inside a cell, chosen with the camera in mind (#750).** A strike takes a built cell middle (`_stormStrikeCells`) by period index, asks `StormCellPosition` where it stands now, then walks on to the first within 420 units, the first the last-drawn camera sees off its centre line (`STRIKE_VIEW_MIN_DEGREES`, `STRIKE_VIEW_EDGE_DEGREES`) and the first tall enough for its channel to leave it above the arena (`BOLT_ORIGIN_MIN_Y`). The pick is **fixed the first time anyone asks at or after the onset** (`StrikeCell`), held in a memory of the **last eight**: the frame, the thunder and the chapter intro's look-ahead (#559) all ask, and with one slot each look-ahead threw out the strike running now. *When* is a pure function of the clock; *where* depends on the camera.
- **⚠ Three things hid the channel**: the glow lit its own cell to clipped white (additive over white adds nothing); the island hides everything below its own level from a lens just above the deck; about half the strikes were out of frame.
- **The channel is generated in the vertex shader** (path hashed off the period index): a camera-facing ribbon with its width in **screen space** (`BoltHalo`, `BoltCore`; under a pixel it crawls) in a violet-blue halo (`BoltHaloColor`); channel 0 is the main one and the other `BoltCount - 1` are **branches off its joints**; `BoltFlash` is its own envelope. **Additive** (alpha over cloud reads as a painted stripe), drawn only while an envelope runs.
- **⚠ It must leave its own cell, not run through it** (inside it is drowned by its glow and hidden by the island): from `BOLT_ORIGIN_SHARE` of the way up, sideways and square to the line of sight, diving to about 25 units under the cell's base.
- **⚠ The cloud lights `GLOW_LAG_SECONDS` after the channel** (nothing additive shows on white cloud); the arena's lamp (`TryGetFlash`) lags with it, the thunder does not.
- **Testing a short event:** nothing in `TestOptions` pins the wall clock (no `time=`) and a 0.45 s strike is shorter than the capture harness's slop: temporarily collapse the period.

## Thunder

A one-shot baked in `Game/Audio/ProceduralAudio.cs`, scheduled by `SceneEventSounds.cs`. **Not in the ambience bed**: a sealed 16 s loop would repeat it at a fixed interval.

- **It rides the light's schedule, never its brightness**: `SceneRenderer.TryGetSceneEvent` hands over the event's index and start second (one hash with the envelope); a brightness edge detector would hear one strike as four.
- **The delay is real**: distance over `SPEED_OF_SOUND` (343 units/s), 0.31 to 2.3 s, which is how a player tells near from far. **Unplaced, no crack ever** (strikes start past the clearing, `StormFlashCenter`); the roll is a slow irregular **gate** over a mid band, without which it is wind.

## Costs

- **About a millisecond over a terrain build** (reference APU, `campos=0,-4,30 camtarget=0,-8,0`, dome 20, 1600x900 ssaa 2: 26.56 against 27.55 ms). ⚠ Two sessions' absolutes differ by 2.5 ms on the iGPU: compare inside one.
- **⚠ A short window cannot measure this scene** (the field drifts): five 16 s pairs spread 1.83 ms around a true 0.22. Use 70 s pairs, dropping the first six readings. One desktop window ran 10.03 to 15.85 ms as cloud crossed the lens, which matters to the adaptive probe.
