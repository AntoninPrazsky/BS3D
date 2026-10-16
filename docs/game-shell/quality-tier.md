# The quality tier

Part of [The game: shell](../game-shell.md).

One bundled **`Quality: Low / Medium / High / Ultra`** setting (#63) replaced the lone Antialiasing row. `QualityLevel`/`QualityPreset` (`Game/QualityLevel.cs`) hold the tiers and **`ApplyQuality` is the one writer**, so the probe ([Adaptive quality](adaptive-quality.md)), the settings row and the command line cannot disagree. Why each entry sits where it does: [What the frame costs](quality-measurements.md).

**The rule: a tier lowers what is drawn, never the pixels.** The owner ruled (2026-08-28, #298) that no tier lowers the resolution: a magnified frame looks ugly and no frame rate buys that back. `PostProcessPipeline.RenderScale` (the Testbed's `rscale=`) is a measuring instrument only; supersampling *above* native is fine. The player's Resolution row is the one exception ([The resolution](resolution.md)).

## What each tier carries

| | supersampling | MSAA | scene detail, stone cap's fine relief | sun shadow map | ceiling refraction | motion blur | cluster wind | finer meshes |
|---|---|---|---|---|---|---|---|---|
| `Low` | 1× | 2 | given up | capped 2048 | no | no | no | no |
| `Medium` | 1× | 4 | full | capped 2048 | yes | yes | no | no |
| `High` | 2× | none | full | authored | yes | yes | yes | yes |
| `Ultra` | as `High` | as `High` | full | authored × 2 (8192) | yes | yes | yes | yes |

- **The table is not the whole tier**: `ApplyQuality` also owns `SceneRenderer.SceneDetail` (reduced scene programs) and `ArenaIsland.SurfaceDetail`, spent at `Low` alone through `QualityLevels.DropsSceneDetail`.
- **The shadow map is a cap, not a size** (`ShadowMapCap`, `ShadowMapScale`, #484): a tier only takes away, so a scene authored small never grows ("Sun shadows" in `docs/rendering.md`). **Reduced programs** (forest, dream, mountain, cavern, Mars, meadow, savanna, volcano) each give up a *pair*: these passes are occupancy-bound and a lone cut buys nothing. The ceiling's refraction (#541; `Medium` keeps it by the owner's word) is paid only while the plate is in frame (`BS3DGame.CeilingInView`).
- **Finer meshes are `High` and `Ultra`'s** (`QualityPreset.FineMeshes` into `SceneRenderer.FineMeshes`, #758): today the savanna acacias' grown fork, where `Low` and `Medium` keep the tube wood. The owner asked for the detail on those two rungs, and `Medium` is the weak machine's. It is read per draw, so a tier change rebuilds nothing. Its cost is in [What the frame costs](quality-measurements.md).
- **`Ultra` is the player's alone** (#484): the probe never reaches it, and turning Auto quality on over it applies `High` at once. An out-of-range stored value (a bare `7`) reads as none chosen, behind an `Enum.IsDefined` guard `quality=` shares.
- **`ssaa=` overrides the tier's factor** and, like `quality=`, latches `_qualitySettled`. It is held as `_supersampleOverride` because `ApplyQuality` re-runs in `LoadContent` and on each step and each wrote the tier's factor back over it until #122. **The `[fps]` line** prints the factor in force, the MSAA count the target was *actually built with* and whether the scene extras are authored: read it before believing an A/B.
- ⚠ `ApplyQuality` runs from `LoadContent`, before the menu pages exist, so `SetSupersampleFactor`'s `_settingsPage.Refresh()` is null-conditional.

## Potato, the fifth tier (#788)

- **The enum's order is not the ladder's**: Potato is appended after `Ultra` yet draws *less* than `Low`. Never compare tiers by number or step past `Low` by arithmetic: `DropsSceneDetail` is the one copy of "gives up scene detail" (`Low`, `Potato`) and `QualityLevels.NextOnRow` walks Low → Medium → High → Ultra → Potato → Low by name. Its preset row is `Low` without multisampling; it draws the **Potato path** (#789, "The Potato path" in `docs/rendering.md`), at native like every tier.
- **GamePi holds it, unchangeable**: `Platform/QualityLock` is a seam (`null` in `Game/Platform`, `Potato` in `GamePi/Platform`); startup takes it over `quality=` and the file, ignores `ssaa=`, pins the probe. The Quality row reads `Potato (locked)`; Auto quality's box is dimmed, **ticked beside `(resolution)` while the Resolution row is at Auto** (the probe runs, the resolution being what it may lower, #832) and unticked beside `(locked)` once a resolution is pinned. Log `[quality] Potato: this build's only tier, locked`.
- **A Windows launch with `potato` is a Potato run** (#808): the renderer is chosen at process start, so the argument sets the Windows `QualityLock` before the game object exists (`Program.Main`), holding the run as GamePi is. Log `[quality] Potato: held for this run by the potato argument, locked`, plus a fourth `[build]` line. It stores nothing.
- **Potato on the Quality row** (Windows, 2026-10-07): picking it stores it, the row reading `Potato (restart)` until the next start, when `Program.Main` reads the file (`GameSettings.ChoosesPotato`, after `userdata=`) and holds the run via `QualityLock.ChooseAtPotato` unless the line says `potato` or `quality=` (the line outranks the file). Log `[quality] Potato: chosen in Settings, so this run draws through the Raspberry Pi's renderer`. Turning Auto quality on over a pending Potato hands the tier back at `High`, as over `Ultra`. That run is **not locked** (`QualityLock.ChosenInSettings`): the row keeps cycling (`Low (restart)`), Auto quality is dimmed beside `(Potato)`, and it draws at the display's size.
- **The lens looks are absent there (#796)**: Aberration and Film grain show an unticked, dimmed box beside `(tier)` and their toggles return before touching anything (`BS3DGame.HasLensLooks`), so stored preferences survive.

## The Scenery row (#808)

`GameSettings.Scenery` (`"scenery"`, stored only as `false`). Off, the frame keeps sky, island, drain, cluster and gun and drops the setting's own world and its shadows (`SceneryShown`); sky-replacing scenes stand under the dome. **The scene's light and events stay**, with the sound that answers them (lamps, storm flashes, the eruption's surge, thunder, rumble): the light is seen, so the sound is not one nobody can place. The Quality row reads `Custom` while off; picking a tier turns scenery back on; on the Potato path the row reads `On (Potato)`, ticked and held: that path draws its own low-poly scenery (`PotatoBackdrop`, `docs/rendering/potato-path.md`) and the row cannot turn it off.
