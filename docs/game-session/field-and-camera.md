# The field, the camera fit and tall levels

Part of [The game: a session](../game-session.md).

## The field is fitted to the level

`FitFieldToMap` derives size and depth per level. The field's top hangs at `max(FIELD_TOP_Y, CEILING_DEATH_Y + FIELD_FLOOR_MARGIN + topLevel/sqrt2)`: a deep field is **raised** so a ball in its lowest cell rests on the death line, one descent from loss (pinning the top let a 30-level map lose on the frame it was built). The empty levels an author leaves under a layout are starting clearance, not dead space. The `[field]` line logs top, floor and lowest-ball clearance. The glass's rest height (`CeilingDescent.Reset`), `_clusterCentreY` and the plate's footprint follow; `FitCeilingToMap` rebuilds the plate's mesh per level, so `BuildLevel` re-runs `ApplySkyLighting`.

- **`_clusterWorldOffset` has X and Z.** `BallsMap.Center` lands on the origin only when the top level is a shifted (odd-index) one starting at index 0; the residual is measured through `GetRealCenteredPosition` and folded into the offset the builder and the contact handler take. ⚠ `Center` takes the top level's **midpoint** (#561), so the offset is only the layout's inset; `AttachBallToStructure` must take the offset too, or a top-level landing's ceiling socket anchors at the raw cell (a median 5.04 units off; `SagProbe` lands through the same method).

## The camera and the gun are solved to the field

`FitCannonAndGameCameraToLevel` runs on every level load and aspect change over the shared `GameCameraFit.Solve`. The stand-off is **bisected** for the smallest distance at which the field's eight corners, the top of the glass and a box around the gun fit **both** frustum half-angles; the aim height from bisecting the elevation range. The gun stands `GameCameraFit.CANNON_CAMERA_STANDOFF` in front of the **lens** (the magazine in its slot is a HUD element and keeps its size), held off by two lower bounds (the field's footprint; the resting aim inside the clamp). The solves alternate three rounds (a sub-0.1 residual survives to the third), the gun is moved **once** after, and the field's vertical extent enters `Solve` as two arguments (here level 0 sits at `_clusterWorldOffset.Y`; the Testbed's lattice frame is the world frame).

- **The forward walk** (#364): the drain used to put the walk's near end on the rest stance, so W did nothing. `CANNON_ADVANCE_FORWARD` floors the rest radius: the gun rests 2 units out, walking 2 in and 4 out (the owner's ruling: each unit of forward room stands the whole campaign further out and flattens the resting shot; two are worth it, four are not).
- **The lens follows a retreat and not an advance** (`CANNON_ADVANCE_FOLLOW`, #322): the gun backs towards the lens, so otherwise the walk's far end left it nearer than the 11 units the stroke cap was sized for (`CANNON_ADVANCE_STROKE`).
- **The lens trails the A/D turn** (`BEARING_TRAIL_SECONDS`, #366): a first-order chase of the bearing **angle** through `WrapAngle` (a vector lerp cuts the chord and goes the long way round across pi). ADS gets none (`PreciseAim.Blend` interpolates it out); the Game's alone.
- ⚠ **Re-measure fit figures after any change to the fit** (a table went seven units stale once): read them off `play level=<name>` and the `[camera]` line.

## Tall levels: a column framed from the bottom

`FRAMED_LEVELS` is the tallest slice the fit frames: a field that deep or shallower is framed whole; a deeper one is framed from its floor to `FramedTopY()`, the rest above the frame. `Ten.json` (Column) is the first. Uncapped, the fit stands back and a level three times as deep renders a third the size; capped, depth becomes the level's *height* and so its length. `FramedTopY` measures from the floor, so a resize frames the same window although the glass has descended.

- **The descent also answers the state.** One ball into a band cuts fifty loose, so a shot-count cadence is the wrong delivery. `FeedTallColumn` runs on every landing and queues as many descents as the underside has climbed since authoring (`CeilingDescent.Feed`): it only **adds** descents, stops at the opening clearance, and skips an **empty** map (`GetLowestOccupiedLevel` of a cleared field is the top level and queued 20 descents).
- **The aim is limited to what is framed** (`Cannon.ElevationLimit`, `SolveElevationLimit`): otherwise the player shoots blind into the part that has not arrived. It is `AimReachability`'s geometry run backwards: the column's underside plus `TALL_AIM_HEADROOM_LEVELS` plus `TALL_AIM_MARGIN`, **capped at the framed top** (`TallAimBandTop`). `ResolveTallAimLimit` re-solves it on **every ceiling step** and resize (at `AimWorldOffset`); each prints `[aimlimit] <before> -> <after> deg (...)`. Solved only at load it stayed clamped above the band as the glass descended.
- **The Game runs `AimReachability` itself** at every load and resize, against `ElevationLimit` and over the framed window only; the Testbed's `aimcheck` does not answer for these. ⚠ `AimReachability.Check` takes the world offset (zero in the Testbed): in the lattice frame it read cells up to five units too high and called Column unfinishable.
