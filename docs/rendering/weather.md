# The weather: clouds, sun and sky

Part of [Rendering](../rendering.md).

Procedural clouds drift, shade the sky, throw shadows and flatten the light when they cover the sun. They live on **a flat plane at a finite altitude**, not as a dome texture (which sits at infinity and casts no derivable shadow, so what you see and what shadows would be two drifting fields). One field, three ray-plane intersections: the sky shader with the view ray, the scene shader with the sun ray, the CPU with the sun ray from the arena.

## The field and who owns it

- Defined **once** in `BS3DLibs/Prazsky.Shaders/Content/Shaders/Clouds.fxh`. The **weather** layer is coarse and mirrored in `Prazsky.Core.Render.CloudField` (the only part that exists twice; its hash uses `frac`/`dot`/multiply and **no `sin`**, because a sine hash is where C# and HLSL part company); the **detail** layer is fine, sky-shader only, and nothing the shadow or light rig reads touches it.
- `CloudField.ApplyTo(effect)` hands the shared parameters to both shaders. A **weather** (shape plus character: `DetailStrength`, `Opacity`, two billow figures, `CharacterStrength`, underside) is pushed **per frame**; `ApplyStaticParameters` pushes once what no sky disagrees about (absorptions, silver lining, horizon fade, sun disc size); `ApplyDome` (each dome/scene switch) pushes dome-dependent colours and remembers the dome's tint for the underside.
- **Five authored skies** (`clear`, `scattered`, `broken`, `overcast`, `storm`; `WeatherLooks`, #221): a word, not eleven floats. **`Scattered` is the original weather to the last digit.** `SceneConfig.Weather` (base class) is the place's usual sky; `Level.Weather` overrides; an unrecognised word falls back to the scene's own. Defaults: desert `clear`, volcano `storm`, city `overcast`, sea/mountains/forest `broken`, meadow/savanna/outback/beach `scattered`, sky-replacing scenes `clear`. Testbed `weather=<name>` compares them.
- **A change of sky fades over 8 s** (`CloudField.WEATHER_FADE_SECONDS`; 2.5 s read as a dissolve). `SkyLightRig.StepOvercast` follows the deck's cover with its own 2.5 s lag (`OVERCAST_RESPONSE_SECONDS`) so the light arrives a beat behind the cloud; ⚠ that constant is not the fade's dial and the Game never steps it ("The clouds are the Testbed's weather, minus its ambient half" in `docs/game-session.md`).
- **Winds are slow** (#809): preset `Wind` halved, Storm 0.4, the second octave 1.25x the wind in `Clouds.fxh` and its mirror `CloudField.Weather` alike (volcano, `campos=0,-4,30 camtarget=0,90,160 fov=60 sceneseed=5`: 107 to 24.7 px/s).
- **A light on the ground can light the deck** (#509): `SceneRenderer.TryGetGroundGlow` (the volcano's crater) goes to `CloudField.SetGroundGlow`/`ClearGroundGlow` **every frame** so no scene inherits the last. **Black is an exact no-op, not a branch** (a uniform branch in the hottest pass keeps its registers either way).

## The sun

- **The disc** is a dot product and `smoothstep` in `Sky.fx`, added *before* the deck composites, so the same field occludes it. Radius `SunDiscAngularRadius` **1.6 degrees** (the true sun's quarter degree flickers in the glare pass's sparse grid). Colour: the rig's `sunRadiance` without the horizon lerp the clouds' lit side gets.
- **Each sky states where its sun stands** (`SkyDome.SUNS`, elevation and azimuth per dome, read by `SkyLightRig.SetSky`). Elevation carries the mood, read off the palette (dome 1 at 48 degrees, 11 at 55, 16 and 7 at 4). ⚠ **The azimuth covers the whole circle, not the half behind the opening view**: the gun's orbit is free 360 degrees, so every dome is seen front-, side- and back-lit anyway; fencing threw away the variety. An azimuth decides the world direction of shadows and glints and the opening frame (orbit starts at `+Z`).
- **Elevation 35 / azimuth 40 means "unchanged"** (`-DefaultLighting.Light0Direction`), kept by the sky-replacing scenes (`SkyLightRig.DOMELESS_SUN_DIRECTION`) **except the Moon and the Big Top**, which state their own sun (`SceneRenderer.TryGetSunDirection`: the Moon's is 16 degrees, #508; the Big Top's is its spot rig's key): a hidden sky byte must not swing the Moon's Earth phases or the space planet's terminator.
- **The map editor has no disc and no weather** (building `Sky.fx` there costs 6.4 ms of 24.9); `SkyDome` takes an optional `Effect`, null draws the plain gradient, and its cloud uniforms stay zero so `CloudSunlight` returns 1.

## The dome's gradient

The captured palette banded (sixteen rings, ~ten colours in pairs: a Mach band across every sky). `SkyDome` collapses same-colour rings, interpolates a 256-entry table **boxed twice** and draws a generated **64 x 48** mesh; `ZenithColor`/`HorizonColor` still average the capture's 92 entries, so the light rig is unchanged. ⚠ **The capture's vertex order is the welding order, not top to bottom**: read as a ladder it paints the sky its own ground colour.

## Shading the deck

- **Shading needs unclamped thickness, opacity the clamped** (`CloudThickness` runs past 1). **ACES eats cloud contrast**: the underside must be well below the lit top (0.18 vs 1.7). Detail strength is read against `CoverageGain`.
- **Cloud shadows** plug into `ShadePixel`'s `keyShadow`, coarse layer only. ⚠ **The key is dimmed per pixel by the shader, the ambient on the CPU, never both** (it counts the cloud twice). **Ambient reads an area** (`CoverAround`, five taps): one tap lurches at every cloud edge.
- **Cloud colour follows the dome**: the lit side starts at `SkyLightRig.SunRadianceTinted` and goes *further* towards the horizon colour (`CloudField.SunTintStrength`) than the scene's key (a daylight deck over a dusk dome read as pasted on, #50); the underside takes the zenith, harder. Test per dome with a launch per `sky=<n>`.
- **Shaded off its own slope** (`CloudNoiseD`, `CloudThicknessD`, `CloudFormStrength`, `CloudShapeStrength`). ⚠ **The derivative stops at the fourth detail octave while the value runs to seven** (a gradient scales with frequency: summed to the end the finest grain owns the slope and the form fizzes). ~1.8x the field's ALU (the one A/B ran on a degraded session: re-measure before quoting).

## ⚠ Real vertical extent for the deck was built and rejected

The flat deck has no silhouette with height and streaks towards the horizon from underneath. **The flat deck wins on looks, not only cost.**
- *Displacing the underside*: fixed-point iteration diverges towards the horizon (marbled); offset limiting is nearly invisible; a linear search smears (the displacement needed exceeds the field's features). *Lowering the deck / stronger billow shading*: a wash / speckle.
- *A marched slab* (four slices; was `QualityPreset.CloudSlices`, removed) delivered layered undersides and was **judged worse by eye** (blurry, hallucinated). Its traps: the body cannot run negative over clear sky, so ungated detail paints cloud on every clear pixel; the path lean `1/climb` unbounded is a navy wall and `min` creases (use a softened reciprocal); sampling where the ray first meets cloud quantises onto the slices (use a density-weighted centre of mass); a belly ramp narrower than the slice spacing gives coverage staircases, and slices `depth / climb` apart at the skyline give a moire only fading depth with the climb removes.
- **Cost to weigh any future attempt against** (Game front end, reference APU, meadow, ssaa 2): the flat deck's cloud pass is **6.4 ms of a 24.9 ms frame** (40.1 vs 54.0 FPS with `SkyPS` returning the bare gradient; 7.3 ms on the sea); marched **40.1 to 26.3 FPS**, six slices no cheaper than four (a fixed penalty, probably register pressure). A tier could only switch it off.

## Cheaper sky: measured, not worth it

- Testbed, sea, dome 13, `nocap`, fixed camera: the deck's shading costs 1.56 to 0.75 ms; approximating the shadow's second `CloudCover` **and** cutting `CloudDetailD` from seven octaves to five together still measure **1.56 ms**: the cost is the field evaluation itself (as for the forest floor and the dream, `docs/scenes.md`). ⚠ **A fixed camera is not optional** (an orbiting-camera sweep once showed two reductions together worse than either alone).
- **A lower-resolution deck target** is worth at most **~2.9-3.0 ms** (camera straight up, weakest machine, dome 13), only with the sky filling the frame and only at `High`: **not taken**.
