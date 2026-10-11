# The ceiling warning: the cluster profile

Part of [The game: feedback](../game-feedback.md). Sibling of [the in-play HUD](hud.md); the 3D side is [the ceiling and floor alarms](ceiling-and-floor-alarms.md).

## Why a profile, not a counter

A counter says *when* the glass steps, which the player can see; not **where it stands** against the cluster and the death line. `DrawClusterProfile` (`PlayHud`) draws a **side cut** down the left edge: the glass as a bar at its true height, the balls as markers in their type colours, the death line at the bottom, so a descent reads as the bar closing on the balls.

## Geometry

- **A true isotropic section, over the scene, no plate.** Vertical is world Y, horizontal is each ball's projection onto the camera's right axis, both on ONE scale `pixelsPerUnit = min(panelHeight/span, width/(2·halfDepth))` (`FieldHalfDiagonal`). Markers are antialiased `DrawDisc`s at the ball's true diameter in `TypeColor`'s palette.
- **FIXED size, framing the whole field** (glass's rest position to the death line): a panel that resized as balls were matched read as a HUD fault.
- **`PROFILE_WIDTH` is the only lever on size**: the width term (half-diagonal 9.2 to 12.7 units) is far below the height term (13.16 span), so raising `PROFILE_HEIGHT` does nothing.
- **It reads the GAMEPLAY camera** (`_gameplayCameraRight`, before the drop cinematic's blend), or it would look like the HUD shaking.
- The balls are the live bodies this frame (`BuildClusterProfile` walks `_physicsBalls`) into one reused array (`_profileBalls`): no per-frame allocation.

## Balls in the air and below the line

- **A ball in the air is a RING** (#89; `DrawDisc` with an inner radius), from `_shotBalls` and `_fallingBalls`. **A climbing shot under the death line stays culled**, or every shot would read as *lost* for half its climb.
- **A ball crossing the death line dissolves rather than pops** (#134): whole for `PROFILE_SINK_HOLD` of a fall measured in **world units**, then dissolving. The fall runs to just above the balls-left readout (`BallsLeftTop`), never shorter than `PROFILE_SINK_FADE`, never longer than the fall to `KILL_PLANE_Y` (the kill plane would take a ball mid-fade).
- **"Falling" is where the ball came from, not its velocity** (#822): `BallMarker.FromAbove`, decided once per ball (a released ball always came down; a shot has once above the death line, `PhysicsBall.RoseAboveDeathLine`). Velocity flickers in the funnel, and the list cannot decide (a missed shot is still in `_shotBalls`).
- **A CLUSTER ball under the line is drawn whole, ringed in `PROFILE_ALARM` red** (#643): the panel stays up through a loss so the player reads what beat them.

## The bars

- **The glass bar is at the plate's OWN thickness** (`CeilingPlate.THICKNESS × pixelsPerUnit`, floored at 3 px). At rest the cluster hangs flush. ⚠ **Sample after the cluster has settled**: the constraints pull it 0.272 up from `CLEARANCE`'s grid position, so a first-frame probe sees a gap no player does.
- The death line keeps a design-unit thickness (a threshold has no world thickness). **The glass bar is NEUTRAL at rest and red only on the flash**, squared like `CeilingDescent.Flash²` on the 3D `EmissiveTint`; **the death line is ALWAYS red**.
