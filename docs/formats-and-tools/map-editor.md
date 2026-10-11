# The map editor

Part of [Formats and tools](../formats-and-tools.md).

`MapEditor/` edits map and level JSON (drag-and-drop a file onto the window). It draws balls and sky as the game does, so a map looks as it will play: the same instanced procedural spheres, shader, sky-derived light and HDR pipeline, from the compiled shaders in `Prazsky.Shaders`. It has **no GUI of its own** since #522 and no ground, cannon, shadows or arena: the field is a translucent outline (`GUI/AABB.cs`, `WireBoxMesh`).

## Keys

The key table in `MapEditor.cs` is the authority (its hint line names each). **F2**/drag-drop load a map or level; **F1** saves a map; **F4** a level (map + scene + sky + the loaded `music`, `author`, `crates`, `softness`, `weather`). **B** cycles sky domes (the game's NumPad1; here NumPad1–9 pick ball types and NumPad +/− cycle all thirteen). **V** cycles all twenty-one scenes, **L** the ball material, **K** the kind of the next placed ball; N fills, M clears, S/W/A/D/Q/E drive the camera.

- **V, L and K walk the enums** (`SceneRenderer.NextScene`, `BallStyles.Next`, `BallKinds.Next`), so a new scene, style or kind is reachable the moment it is declared. (A fixed prefix once left fifty shipped levels' scenes unreachable, with no way back to the level's own scene except reloading.)
- **V does not adopt the scene's default dome** (the Testbed does): here the dome is *data* that F4 writes, and overwriting it on a cycle would destroy the author's choice.
- **K is its own key** because kind and colour are orthogonal (a rock is not a fourteenth colour); they compose with nothing added. ⚠ The hint line names each kind in a `switch`: a new kind needs its case.

## The render pipeline

`MapEditor.Draw` binds the shared supersampled HDR target (`SUPERSAMPLE_FACTOR` fixed at 2, no `ssaa=`), clears it to the **current dome's horizon colour** (`_rig.HorizonLinear`; black for a sky-replacing scene: at a wide aspect the bottom corners look below the horizon past dome and scene, and a fixed blue showed through), draws sky, scene and balls, and `PostProcessPipeline.Resolve` runs `Glare.fx` and `Tonemap.fx` with the game's own look figures.

- **Balls** go through the shared `BallRenderSet` (#76): the game's albedo, emission, translucency and heartbeat, with no landing **ripple** (`ripples: false`).
- **The sky dome** goes through `BasicEffect`, not `Sky.fx`: `SkyDome` is built with `linearVertexColors: true`, `ApplySkyLighting` decodes the palette to linear and sets `LinearLightRig = true`, and the rig is the shared `SkyLightRig`. **No sun disc or clouds** by decision (#220): they live in `Sky.fx` and would drag the cloud evaluation in, while everything an author judges already follows the dome's sun ("The weather" in `docs/rendering.md`).
- **The AABB outline** is opaque and depth-tested with its flat colour linearised on the CPU (`ColorSpace.SrgbToLinear(COLOR)`, or it tonemaps too bright). **The selector** (additive, depth off) and the **axis gizmo** (`GUI/AxisGizmo.cs`) are drawn *after* the resolve, in display space, where `BasicEffect`'s gamma is right.

## The scene-config panel, removed in #522

From #45 to #522 a Myra `PropertyGrid` over the active `SceneConfig` (G toggled it) tuned a scene live. **It was removed because a scene's look is programmed, not configured** (the owner's ruling): a level names its scene, the parameters are fixed in code, and a per-run editor of shader and material parameters is not wanted. With it went Myra from the editor, `SceneRenderer.Apply(SceneConfig)` and both `Replant` methods; `Rgb`/`Vec2`/`Vec3` are `readonly struct`s again (they were classes only because the grid edited a boxed copy). **The configs' shape outlived it** (named nested groups such as `NebulaOne`/`Two`/`Three`, the storm's `Clouds`/`Surface`/`Flash`/`Air`): a named group reads as a config where an index does not, but no document should justify it by the grid's old limit.
