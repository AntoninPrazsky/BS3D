# Scene point lights

Part of [Rendering](../rendering.md).

A scene can carry **real point lights under every sky dome** (not emissive surfaces: the city windows and neon glow but light nothing).

## Mechanism

- `InstancedModel.fx` (`AddSceneLights`, in `ShadePixel`) and `Savanna.fx` loop over `SceneLight{Position,Color,Range}[MAX_SCENE_LIGHTS]` and `SceneLightCount`: Blinn-Phong with a quadratic falloff over sun and sky; colours are linear radiance over 1.
- `Prazsky.Core.Render.SceneLights` is the C# half for every executable; it pushes onto the **shared** instanced effect, so one push reaches balls, island, drain, gun and city. ⚠ **`SceneLights.MaxLights` (8) must equal `MAX_SCENE_LIGHTS` in both shaders**: raising it only here writes past the shader loop.
- The savanna grass is separate: `SavannaBackdrop.DrawSavanna` writes the same uniform names on its own effect, one slot per fire (`SavannaCampfireCount`), sharing the campfire itself so grass and balls stay in step.

## The lights

- **Volcano** (`SceneRenderer.VolcanoLightCount`/`VolcanoLightPosition`/`VolcanoLightColor`): the only scene whose ground is the light and whose lamps **move**: slot 0 in the crater (eruption envelope), the rest flow fronts travelling downhill. Its bearing mirrors `Volcano.fx`'s own wander term (change one, change the other). Scaled by `VolcanoSceneConfig.LightStrength`, a **readability** dial: red and orange balls hang over lava and the cluster's tops must keep the dome's light so the red underside reads as under-light. "The volcano" in `docs/scenes.md`.
- **Savanna campfire** (`SavannaCampfireLightPosition`/`CampfireColor`): a flickering warm light plus an **additive** billboard flame (`Flame.fx`) off the same clock, blue kept low so it reads as fire. ⚠ **The instanced effect's light stands at the flame's body, not at the fire's foot** (#885): the fires burn on the ground below the island's top plate, and a light below an up-facing surface lights nothing on it. The grass's own loop keeps `SavannaCampfirePosition`, on the ground. "The light on the island's plate" in [The savanna's campfires](../scenes/savanna-fires.md).
- **Neon city** (`NeonConfig` in `Prazsky.Core/Render/Config/CitySceneConfig.cs`): a ring of alternating magenta and cyan lights round the island.
- **Space's planetshine** (`TryGetSpacePlanetshine`, `PlanetshineDistance` out along the planet's direction, range 3x): a **real light, not more ambient** (ambient is directionless and flattens the scene; only a real light puts a highlight into the gold drain beads). **The Moon's earthshine** (`TryGetMoonEarthshine`) is the same at the Earth's colour.
- **No scene-light loop** in the sea, desert, mountains, meadow, forest and volcano terrain shaders (the volcano's lava is emissive and its lamps exist for everything that is *not* the ground).
- Adding a light is a slot in `SceneLights.Apply` plus a loop in any shader that should catch it. The `Apply` guards are mutually exclusive by construction, so their order is an order, not a precedence: **a further branch must not rely on being tested last**.

## A blast's flash (#389)

`SceneLights.SetFlash` adds a light to the *next* `Apply` only, in the first free slot; `Apply` consumes it either way, so a host that never asks lights none. ⚠ **It is dropped rather than evicting a lamp** when all slots are taken (the savanna's ring can fill eight; the volcano, where the bombs are, uses six): a flash that put a campfire out for a frame is a flicker. The session states it every frame a blast burns (`Blasts.TryGetLight`) before `BeginSceneDraw`. "A bomb goes off" in `docs/game-feedback.md`.
