# The menu camera

Part of [The game: shell](../game-shell.md).

`BackdropScreen`'s camera is a **flight** (#254), not a fixed orbit. It is the game's one `RecoilCamera` with the shake at rest, the one the gameplay screen poses while playing, so it must stop when a session is on the stack (`MenuPage.UpdatesUnderlying`). Everything advances by elapsed seconds.

## The legs

- **The wide leg is the establishing shot**: one turn per 90 s (`WIDE_ROTATION_SPEED`), roughly level with its target, a look *across* the scene. Its stand-off is **solved from the horizontal half-angle** (a disc of radius R fits a frustum of half-angle t past `R / sin(t)`; the island's R is 26), with a second bound against a map deeper or broader than any shipped.
- **Once a cycle it flies at the map**, each cycle rolling its legs (`WIDE_SECONDS`, `APPROACH_SECONDS`, `CLOSE_SECONDS`, `RETREAT_SECONDS`, each with a `…_JITTER_SECONDS`) and its clearance (`CLOSE_CLEARANCE` ± `CLOSE_CLEARANCE_JITTER`). The wide roll's floor is the quality probe's settle (below).
- The close stand-off rides a **clearance floor, not a fit**: 11.4 to 17.1 units across the shipped set (`Cannonball`, a pyramid in a 12-wide field, 11.4 to `Bolt`'s 34-level column 17.1; `[orbit]` lines, 2026-10-12). `CLOSE_MAP_SHARE` never binds; `CLOSE_CLEARANCE` of air off the cluster's bounding sphere decides.
- **The lens always cranes up and always looks up (#599)**, from `CRANE_LOW_PITCH` (the underside against the sky, the face that says the thing hangs) to `CRANE_HIGH_PITCH` (about the wide leg's own look up, so the retreat pulls straight back). The ends are **angles**, turned into heights against the pass's stand-off every frame; as heights they once started half the cycles over the top looking down.
- **The title steps aside without leaving**: the wordmark scales to `WORDMARK_ASIDE_SCALE` across the approach and back across the retreat (small, in its corner: the owner's ruling), so a full-size name does not cover the close pass.

## Framing is the map's

- `FrameOrbitFor` takes the aim height from the cluster's own middle and both stand-offs from its measured reach (the set runs from a four-level pancake to a twenty-four-level column; `FitClusterWorldOffset` hangs a deep field higher). A session frames too as it installs a level (`ResultPage` flies out onto this orbit). A new preview drifts onto its framing (`FRAMING_EASE_SECONDS`).
- ⚠ **The lens stays inside the scene's open ground (#1039).** The narrower the window, the further out the wide leg stands, because the island has to fit the horizontal half-angle: 47.1 units at 1920x1568 against 33.2 at 3840x1600. The cities' first towers stand nearer than that: **49.7 units in the City and 41.2 in the Neon City** (`City.OpenRadius`, the nearest box's footprint; the `[orbit]` line says it), so a half-screen window flew the wide leg through the Neon City's towers. `BS3DGame.OpenGroundRadius` gives the clearing (the Potato path's own city on that path, infinite in every other scene). `OpenGroundFieldOfView` holds the lens `OPEN_GROUND_MARGIN` (6 units) inside it and widens the field of view by the ratio of tangents instead: the Neon City's wide leg at 1920x1568 stands at 35.2 through 75.4° (60° elsewhere), up to `OPEN_GROUND_MAX_FOV` (100°). Past that, only for a window taller than wide, the lens goes on out. A tour from the menu starts on the lens's actual field of view.
- ⚠ **A map is never hung around the lens (#408).** A preview requested mid-flight is solved at once but **hangs only when the lens stands clear of its bounding sphere** by `HANG_CLEARANCE` (under the smallest clearance a cycle can roll, so the wait always ends), drifting at `CLEARING_EASE_SECONDS` meanwhile. Otherwise a thirty-level column hung around a lens skimming a sixteen-level pancake and the drift backed the camera out through the balls.

## The wide leg's length is the probe's

The adaptive-quality probe runs off this same `Update` and settles inside the first ten seconds, so it reaches its verdict on wide frames; a cycle that flew in sooner would let a screen full of cluster decide the tier the whole game runs at. Every route that reopens the probe (a fresh program, a rolled preview, a release from the result screen) restarts the flight on the wide leg. **The fullscreen toggle is the exception, left alone on measurement** (959 balls under the neon city held 330 to 420 FPS through a full cycle).
