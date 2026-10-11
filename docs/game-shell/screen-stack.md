# The screen stack and the host's frame

Part of [The game: shell](../game-shell.md).

`Screen` and `ScreenManager` (#26) live in **`Prazsky.Core/Screens/`** and know nothing about Myra, this game or drawing. What a page is and how it arrives: [The front end](front-end-pages.md).

## The library's rules

- **Two independent flags**, `DrawsUnderlying` and `UpdatesUnderlying`: a pause must *draw* over the game it froze while stopping it *updating*; a settings panel over it lets both draw and neither update. Update walks top-down until something blocks, then runs bottom-up within that slice; Draw paints back to front. `ResultPage` is the one page over a session answering **true** to `UpdatesUnderlying` (#241), so "paused" means a page that *stops* the gameplay screen is over it.
- **Mutations are deferred to the next frame**, applied at the **top** of `Update`, so a pushed screen gets `Enter` and its first `Update` before it is drawn. **Each operation is decided against the stack it is applied to, not the live one at the call** (#576): `PopTo` checks when applied and logs a `[screens]` line if the screen is absent (checking at the call skipped the `play` argument's pop); `Replace(old, next)` puts `next` at `old`'s index wherever it stands, dropped with a log line if `old` left. The `!Contains<SplashPage>()` gates in `BS3DGame.Update` stay: a page opened at boot would stand over the intro.

## What the stack bought

- `DimsFrame` is `Manager.Contains<PausePage>()`; **`GameState` is gone**: a level is being played when `GameplayScreen` is on the stack, paused when a page is over it. `_screens.Update`/`Draw` are the whole frame.
- **Pages are held for the life of the game** (`Contains<PausePage>()` means something) and each owns its tree (`Game/Screens/*Page.cs`: `BuildTree`, `Refresh`), built **lazily** against `MenuLayoutGeneration`. **`ShowPage` is the single arrival path**, raised by `CoveredChanged` on whichever page ends up on top: refresh, scrim from the stack, tree into the one desktop, nav entries re-collected. The session's door: [Entering and leaving play](session-and-input.md).
- **`FrameBlur`** (#178, #214) is how far the frame behind a screen goes out of focus ("The end of a level goes out of focus" in `docs/game-feedback.md`), asked of the **active** screen at resolve time through `IFrameBlurSource`, one answerer a frame. `PausePage` answers on the **wall clock**, because `MenuPage.UpdatesUnderlying` is false while a `GameplayScreen` is on the stack, so Settings over a pause stops the pause updating and a ramp in its `Update` would freeze half way.

## The frame is not "draw the screens back to front"

The pipeline binds an HDR target, draws the world and resolves once, and gameplay draws in slots *inside* that. The host owns the pieces and the bottom screen the sequence: `BeginSceneDraw` → the screen's 3D → `DrawSettingGlass` (the drain's beads, then its glass) → `FinishSceneDraw` (foreground weather, the resolve). `BackdropScreen.Draw` runs it with the gameplay slots empty, `GameplayScreen.Draw` with cluster, gun and trails.

- ⚠ **A frame the gameplay screen declines to draw has no target bound, no clear, no resolve**, so its un-built branch falls back to `DrawSetting()` rather than returning. That frame is real and lasts one: the result page's Main Menu tears the session down from `Update` while the pop is deferred. ⚠ **It opens an empty ball collection before `BeginSceneDraw` (#672)**, because `BallRenderSet.DrawShadow` throws on a frame nobody opened: leaving a result page by Main Menu with a keyboard or pad crashed on any tier with sun shadows.
- **The glass ceiling is split**: mesh and renderer are the host's, the kinematic body the session's. `RebuildCeilingRenderer` (a delegation to `CeilingPlate.Fit`) runs per level and **`ApplySkyLighting` must run after it** (a fresh renderer has no palette).

## Host and session

The **host** (`BS3DGame`) keeps the window, device, content, shared setting, pipeline, stack and desktop; the **session** (`GameplayScreen`) the simulation, cluster, gun, camera, HUD, scoring wiring and level flow.

- **Both are partial files, which is staging, not architecture** (#71, #72). **A partial hides coupling rather than removing it**. **The ordered scripts stay in the main files** because their order is the behaviour (`LoadContent`: scene, startup dome, tier, menu last; `Update`: step, landings, cinematic gate, clear, loss, tear-down).
- **Owned objects (#583)**: `LaunchOptions`, `StartupScript`, `Online/OnlineSession` (pages reach it as `Game.Online`), `Screens/SessionTestOptions`, `EffectiveSettings` and **`Audio/AudioDirector`**, which builds the audio objects and `GamepadRumble` in `LoadContent` and owns the policy (theme hand-over, the About player's yield, fireworks ducking, `ApplyVolumes`, the scene's bed). **The order within a frame is the host's**, so it is called three times: `UpdateListener` first, `Update`, then `UpdateRumble` after the stack.
- **The window's title names the level** (`ShowLevelInTitle`: `Bubble Shooter 3D — 3. Mosaic`): set by `EnterPlaying`, cleared by `ReturnToMainMenu`, which is **not** the session's lifetime (Main Menu keeps the session).
