# Rendering

How a frame is built: linear radiance in, one tonemap out. This area covers colour management and the post-process pipeline, the instanced effect and the ball shaders, the procedural city, scene lights, sun shadows, the weather, resolution and the Potato path. CLAUDE.md holds the project overview and conventions.

Three rules govern most of it. **The scene renders in linear radiance and is tonemapped exactly once** (`PostProcessPipeline`); a colour added without linearising it is the recurring bug. **Every shader compiles once, in `Prazsky.Shaders`**, and the instanced effect's include order is its constant-buffer layout. **A cost figure here was measured on a named machine and pin**: compare like with like (same camera, `alt=` pairs in one process), with `nopost` on (and `nooverc` for colour) when judging a shader.

## Sub-documents

- [The noise library and shared headers](rendering/noise-and-shared-headers.md): `Noise.fxh`, the guarded `PerturbNormalFromHeight`, the shared-header rules.
- [The city](rendering/city.md): why it is drawn near to far, and its windows at distance.
- [Scene point lights](rendering/scene-lights.md): shader slots, `SceneLights`, which scenes carry lights, a blast's flash.
- [Sun shadows](rendering/sun-shadows.md): the map, its fit, bias and cost, which scenes have it, the city, the ceiling glass's analytic shadow, photographing a shadow.
- [The weather](rendering/weather.md): the flat cloud deck, five skies, the sun disc and per-dome sun, the rejected vertical-extent deck.
- [The post-process pipeline](rendering/post-processing.md): resolve, aberration, grain, bloom, defocus, the foreground and overlay layers.
- [Glass](rendering/glass.md): the cup's refraction, the crystal and drain glass, the ceiling plate bending a frame copy.
- [Colour decoding and the lighting model](rendering/lighting-model.md): where colours decode, Blinn-Phong plus specular ambient, per-draw uniforms, polished metal.
- [The instanced effect's files](rendering/instanced-effect.md): `InstancedModel.fx`'s includes, the constant-buffer rule, shader warnings.
- [Ball rendering](rendering/ball-rendering.md): buckets, burial, emission, the LOD ladder, the display-pixel dissolve, relief, Testbed arguments.
- [The palette](rendering/ball-palette.md): the thirteen colours, measuring them, navy and olive, `nooverc`.
- [Ball shading styles](rendering/ball-styles.md): the style system, what every technique owes, the ten styles and their traps, and the #737 colour cue (a painted figure per colour, one of four figure sets per style, on by default).
- [Special ball kinds](rendering/ball-kinds.md): rock, clear glass, bomb, zap, acid, frozen, infectious, gravity, heavy, wildcard, and the crossings.
- [Motion blur, resolution and aspect](rendering/motion-blur-and-resolution.md): the velocity pass, the shutter, the distance trap, the shot stretch, 16:9 to 21:9, what supersampling costs.
- [The Potato path](rendering/potato-path.md): GamePi's forward path, its DirectX build, cost levers, drawn edges.

## Where the old sections went

Code comments and other documents cite sections of this document by title; this table resolves them. The last rows are paragraphs and bullets quoted by name.

| Old section | Now in |
|---|---|
| The noise library (Noise.fxh) | [Noise library](rendering/noise-and-shared-headers.md) |
| Drawing the city near to far (Prazsky.Core) | [The city](rendering/city.md) |
| The city's windows (Prazsky.Core) | [The city](rendering/city.md) |
| Scene point lights (Prazsky.Core) | [Scene point lights](rendering/scene-lights.md) |
| Sun shadows (Prazsky.Core, #469) | [Sun shadows](rendering/sun-shadows.md) |
| Every scene with a sun over it (#471) | [Sun shadows](rendering/sun-shadows.md) ("Which scenes, and the fit") |
| The city's shadows (#471, the host half) | [Sun shadows](rendering/sun-shadows.md) ("The city's shadows") |
| The ceiling's glass shadow (#553) | [Sun shadows](rendering/sun-shadows.md) |
| The map's size, and the tier it follows (#484) | [Sun shadows](rendering/sun-shadows.md) ("The map") |
| The weather (Testbed) | [The weather](rendering/weather.md) |
| Color management (Testbed) | [Post-process pipeline](rendering/post-processing.md); [Glass](rendering/glass.md); [Lighting model](rendering/lighting-model.md) |
| The instanced effect's files (InstancedModel.fx, #581) | [Instanced effect](rendering/instanced-effect.md) |
| Ball rendering (Testbed) | [Ball rendering](rendering/ball-rendering.md); [Palette](rendering/ball-palette.md); [Styles](rendering/ball-styles.md); [Kinds](rendering/ball-kinds.md) |
| Motion blur (Prazsky.Core + Game, #402) | [Motion blur and resolution](rendering/motion-blur-and-resolution.md) |
| Resolution and aspect (Testbed) | [Motion blur and resolution](rendering/motion-blur-and-resolution.md) |
| The Potato path (GamePi, #789) | [The Potato path](rendering/potato-path.md) |
| The ceiling's glass bends what is behind it (#541) | [Glass](rendering/glass.md) |
| And the glass in it bends what is behind it (#426), the cup | [Glass](rendering/glass.md) |
| One layer is exempt from it (#225); the OVERLAY layer (#438); the DEFOCUS (#178) | [Post-process pipeline](rendering/post-processing.md) |
| The ball dissolve dithers in display pixels; The LOD ladder | [Ball rendering](rendering/ball-rendering.md) |
| The ball shader | [Ball rendering](rendering/ball-rendering.md); per-kind looks in [Kinds](rendering/ball-kinds.md) |
| #395 is the third pass (lava colour) | [Styles](rendering/ball-styles.md) (Lava) |
| Judging colour through the Testbed carries weather the Game never has | [The palette](rendering/ball-palette.md) |
| The deck was given real vertical extent once | [The weather](rendering/weather.md) |
| (dropped: history) | the 18-sky sun survey, earlier values of aberration, glare and fade constants, each special kind's redraw history, per-style tuning journals, verification transcripts |
