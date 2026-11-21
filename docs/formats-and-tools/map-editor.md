# The map editor

Part of [Formats and tools](../formats-and-tools.md).

`MapEditor/` edits map and level JSON (drag-and-drop a file onto the window). It draws balls and sky as the game does, so a map looks as it will play: the same instanced procedural spheres, shader, sky-derived light and HDR pipeline, from the compiled shaders in `Prazsky.Shaders`. It has **no GUI of its own** since #522 and no ground, cannon, shadows or arena: the field is a translucent outline (`GUI/AABB.cs`, `WireBoxMesh`).

## Keys

**F2** (or a file dropped on the window) opens a map or level; **F4** saves a level (map + scene + sky + ball material + the loaded `music`, `author`, `crates`, `softness`, `weather`), **F1** the map alone, **F3** starts a new one. **B** cycles sky domes (the game's NumPad1; here NumPad1–9 pick ball colours and NumPad +/− cycle all thirteen). **V** cycles all twenty-one scenes, **L** the ball material, **K** the kind of the next placed ball; N fills, M clears, W/A/S/D/Q/E fly the camera, F8 saves a screenshot, **H** folds the key help.

**What the screen shows.** The top line is the shared `InfoRenderer`: the FPS and a status line naming what the last key did (the colour, kind, material, scene or level). Under it is the editor's own key help (`GUI/KeyHelpOverlay.cs`, #1209). It is grouped (FILE, then the H line, SELECTOR, BALLS, CAMERA, SCENE, WINDOW) and drawn on a dark see-through panel, with every description starting at one x. **The file keys come first and stay on screen when H folds the rest.** The axis cross (`GUI/AxisGizmo.cs`) stands in the bottom-right corner, clear of the panel. The help used to be one 39-line block in binding order, cut by the bottom edge of every window, so load and save, its last lines, were never seen.

- **It fits the window at any size** (`GUI/KeyHelpLayout.cs`). Text is 0.6 × the font at a 2160-line window, in proportion below that, and never smaller than an 18-pixel line while there is room. The groups flow into as many columns as that needs, a group never split between two; only when no number of columns fits does the text shrink. The window can be dragged to any size, so this is arithmetic, not a minimum size. `MapEditorKeyHelpTests` holds every line inside the window over a sweep of sizes from 100 × 60 to 3840 × 2160.
- **`GUI/EditorKeyHelp.cs` is the authority for the wording**, and each binding in `MapEditor.Initialize` names the line that documents it (`Bind(EditorKeyHelp.X, key, method)`), so no key can be bound without a line. A new line goes into `EditorKeyHelp.All`, which is what is drawn; a test fails on a line left out of it. The font has Latin-1 and Latin Extended-A only, so an en dash or an ellipsis there draws as `?` (a test checks).
- The help names the axis, never the members: "Next scene (named at the top)", not a list of scenes, so a new scene, material or kind cannot make it wrong (#320, #376).

- **V, L and K walk the enums** (`SceneRenderer.NextScene`, `BallStyles.Next`, `BallKinds.Next`), so a new scene, style or kind is reachable the moment it is declared. (A fixed prefix once left fifty shipped levels' scenes unreachable, with no way back to the level's own scene except reloading.)
- **V does not adopt the scene's default dome** (the Testbed does): here the dome is *data* that F4 writes, and overwriting it on a cycle would destroy the author's choice.
- **K is its own key** because kind and colour are orthogonal (a rock is not a fourteenth colour); they compose with nothing added. ⚠ The status line names each kind in a `switch` (`CycleBallKind`): a new kind needs its case.

## The command line

`MapEditor.exe [file] [width=W] [height=H] [shot=s1,s2,…]`. The first argument that is not an option is the map or level to open. `width=`/`height=` size the window's client area (1280 × 800 without them). `shot=` writes the back buffer to `Screenshots\mapeditor-<time>-<scene>.png` beside the exe at those seconds of the draw clock (the shared `ScreenshotWriter`, printing a `[shot] <path>` line). Use it to photograph the editor: its `Update` stops while the window is not active, but the draw and the shot carry on, so a scripted run needs no focus, and nothing in front of the window gets into the picture. An argument counts as an option only by its exact prefix, so a file path stays a file path. The open and save dialogs start in the working directory, so a scripted run gets a scratch one.

## The render pipeline

`MapEditor.Draw` binds the shared supersampled HDR target (`SUPERSAMPLE_FACTOR` fixed at 2, no `ssaa=`), clears it to the **current dome's horizon colour** (`_rig.HorizonLinear`; black for a sky-replacing scene: at a wide aspect the bottom corners look below the horizon past dome and scene, and a fixed blue showed through), draws sky, scene and balls, and `PostProcessPipeline.Resolve` runs `Glare.fx` and `Tonemap.fx` with the game's own look figures.

- **Balls** go through the shared `BallRenderSet` (#76): the game's albedo, emission, translucency and heartbeat, with no landing **ripple** (`ripples: false`).
- **The sky dome** goes through `BasicEffect`, not `Sky.fx`: `SkyDome` is built with `linearVertexColors: true`, `ApplySkyLighting` decodes the palette to linear and sets `LinearLightRig = true`, and the rig is the shared `SkyLightRig`. **No sun disc or clouds** by decision (#220): they live in `Sky.fx` and would drag the cloud evaluation in, while everything an author judges already follows the dome's sun ("The weather" in `docs/rendering.md`).
- **The AABB outline** is opaque and depth-tested with its flat colour linearised on the CPU (`ColorSpace.SrgbToLinear(COLOR)`, or it tonemaps too bright). **The selector** (additive, depth off) and the **axis gizmo** (`GUI/AxisGizmo.cs`) are drawn *after* the resolve, in display space, where `BasicEffect`'s gamma is right.

## The scene-config panel, removed in #522

From #45 to #522 a Myra `PropertyGrid` over the active `SceneConfig` (G toggled it) tuned a scene live. **It was removed because a scene's look is programmed, not configured** (the owner's ruling): a level names its scene, the parameters are fixed in code, and a per-run editor of shader and material parameters is not wanted. With it went Myra from the editor, `SceneRenderer.Apply(SceneConfig)` and both `Replant` methods; `Rgb`/`Vec2`/`Vec3` are `readonly struct`s again (they were classes only because the grid edited a boxed copy). **The configs' shape outlived it** (named nested groups such as `NebulaOne`/`Two`/`Three`, the storm's `Clouds`/`Surface`/`Flash`/`Air`): a named group reads as a config where an index does not, but no document should justify it by the grid's old limit.
