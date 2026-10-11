# The executable

Part of [The Testbed](../testbed.md).

## Which file a change belongs in

`Testbed` is one partial class: the core file is the state, each partial a phase of the frame.

| File | Holds |
|---|---|
| `Testbed.cs` | Every field, the lifecycle, the key table, scene and sky (`SwitchScene`, `SetSkyDome`, `ApplySkyLighting`, `UpdateOvercast`), the setting (`BuildCity`, `BuildCeiling`), map and level loading (`DeserializeMapFromFile`, `LoadLevel`, `InstallMap`, `FitCeilingToMap`), the camera fit, the overlay text, `ApplyVariant` |
| `Testbed.Update.cs` | One simulation step, the wall-clock ageing outside that gate, the input poll, the game-mode transition, the harness ticks |
| `Testbed.Draw.cs` | Scene, resolve, overlay, `CollectMagazineBalls`, the back buffer (`SetGraphics`), the `[fps]` line, the frame cap and, last, the capture |
| `Testbed.Input.cs` | `SwitchGameMode`, `UpdateCannon`, `UpdateMouseAim` |
| `Testbed.Shooting.cs` | `InitializeShooting`, `ShootBall`, the cull (`RemoveFallenBalls`, `KILL_PLANE_Y`), teardown (`RemoveCurrentBallsStructure`, `RemoveAllConstraints`, `ReleaseAllBalls`) |
| `Program.cs` | The `[build]` lines, then `new Testbed(TestOptions.Parse(args))` |
| `Diagnostics/TestOptions.cs` | Every switch, parsed once |
| `Diagnostics/InputScript.cs`, `TerrainMirrorCheck.cs` | The input timeline; `mirrorcheck` |
| `Diagnostics/AutoShootDriver.cs`, `AimShootDriver.cs`, `SwitchMapDriver.cs` | The harnesses, each **null unless its switch was given** and ticked from one line |

- **Fields stay in `Testbed.cs`**, not in the partial that uses them, because several are initialised from each other and a split between them starts a fight over initialisation order.
- **`[autoshoot]`'s line is written in `Testbed.cs`** (`AutoShootOnce`), since every figure on it belongs to this executable's renderers; the driver owns only the cadence and target box.

## The overlay and its key help

F12 toggles the overlay.

- **A key hint names the axis, never its members**, because an enumerating hint ages silently when the enum grows (the scene hint once named seven of seventeen). The dome hint is built from `SKY_DOME_COUNT`. The live values are stated once, on the overlay's own line (`Scene: Space   Dome: 1   Balls: beach`).
- **The overlay text is a dirty flag** (`InvalidateOverlay`), spent by `RefreshOverlayText` once a frame and only while visible, because `Solver.CountConstraints()` walks every constraint batch and a cascade changes the population several times a frame. The visibility test precedes clearing the flag, so the overlay is true the frame F12 brings it back.
- **NumPad2 walks every scene** off the enum (`SceneRenderer.NextScene`), so a new scene needs nothing here. A scene's default dome is `SkyDome.SceneDefault`, the one table both executables' `SetScene` ask. ⚠ Local copies of either (a seven-scene cycle, a two-row dome table) went silently wrong as scenes were added.

## How a shot joins the cluster

`Prazsky.BS3D.Physics.BallContactEventHandler`, one copy shared with the Game (#68); read its class doc before touching it.

- Bepu calls back on worker threads, so contacts are only recorded there and processed on the main thread in the order `PhysicsWorld.Step` owns: `Timestep` → `ContactEvents.Flush` → the step's contact work.
- It listens on `OnTouching`, not `OnContactAdded`, so it never attaches off a speculative contact, and `ShotPlacement` decides the cell, so the Game's aim preview asks the same question.
- The Testbed builds a fresh handler per map load (`InstallMap`) with a zero lattice-to-world offset (its lattice frame is the world frame) and no score events, because `FitCeilingToMap` replaces the ceiling's `KinematicBody` wrapper on every load and a long-lived handler would hold a stale one.
- `TryFindStructureBall` walks the whole structure array when a shot meets a loose ball (8 000 cells on a 20×20×20 field). Unoptimised, not a divergence.
