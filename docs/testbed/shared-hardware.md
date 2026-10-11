# The hardware shared with the Game

Part of [The Testbed](../testbed.md).

The gun and everything both executables built around it are one copy each in the libraries (#76), so [The cannon](cannon.md) and [The game-mode camera and precise aim](camera-and-aim.md) describe the Game too.

## What each piece owns

- **`GameObjects.CannonRig`**: the barrel, carriage and glass meshes and their renderers, and every figure the tube is cut to. It derives `PivotToFrontBall` and `BarrelReach` from the queue and the mesh actually built, so the muzzle, the bore balls and the camera fit's gun box cannot disagree. It does **not** dispose the shared `InstancedModel.fx`.
- **`GameObjects.Magazine`**: the loaded queue, its glide and the bore placement (`BorePose`). A slot is one `MagazineSlot` that moves whole, and a next-colour policy answering `default(BallType)` **throws** rather than loading an invisible ball.
- **`GameCameraFit`**: camera stand-off, aim height, orbit radius and walk range, solved together.
- **`PreciseAim`**: the lean's dials, `Blend`, `ButtonHeld`, and the `AimPose` from `BlendedPose`.
- **`Cannon`'s "The barrel's pose" region** (`AimDirection`, `StandBearing`, `MuzzlePosition`, `BarrelWorld`, `CarriageWorld`), derived, never stored. The walk and the recoil are the gun's; callers only tick them (`KickRecoil`, in the Testbed in game mode only; `StepRecoil`). `AimDirection` and `MuzzlePosition` never see the recoil, so it is drawing-only.
- **`AimReachability`**: a pure function of map and orbit geometry, so the map editor can ask it on save.
- **`Prazsky.BS3D.Physics.PhysicsWorld`**: pools, `Simulation`, the `ContactEvents` stream and the build and teardown order.
  - ⚠ **The stepping policy stays with each caller, by measured decision.** `Step` takes exactly one step of the length given. The Testbed steps once per frame, `min(frameTime, 1/60)` (F9 slows it); the Game spends whole 1/120 s steps under a cap ("Physics in the game" in `docs/game-session.md`). No accumulating overload: it would fight the Testbed's stop-the-world keys.
  - It owns the order inside a step, `Timestep` → `ContactEvents.Flush` → the contact work passed in, and keeps the `ThreadDispatcher` private so no step skips the flush.
  - ⚠ `ContactEvents.Initialize` runs once, in its constructor; `Simulation.Create` already initialises the stream, and a second call silently doubles the freshness pass.
  - `RetireBall` unregisters **then** removes, because Bepu reuses a collidable reference for the next body.
- **`BallRenderSet`** with **`ClusterCollector`**: everything it takes to draw balls, and the walk that fills the buckets; all three executables draw through it.
  - ⚠ **Every ball is visited exactly once a frame**, because the occlusion ease, attach glide and ripple advance per visit. `BeginFrame` returns a `readonly ref struct BallDrawFrame` that cannot be stored, and opening a second frame before drawing throws.
  - `OcclusionTarget` is the only builder of the occlusion direction and divides by the occluder maximum. ⚠ Do not clamp the occluder count: `BallsMap.CountOccupiedNeighbors` cannot exceed the divisor, and a clamp invites someone to change the divisor.
- **`MouseAim`**: the cursor delta is divided by the frame time, cancelling the one `Cannon.Aim` multiplies in, so the aim is `0.001 × SENSITIVITY` rad/px at any frame rate. The viewport centre is read live and the first captured frame skipped (`Initialized`, `Invalidate`). The pad stick (`PadAimRate`, #819) is a rate with no dial (see `PAD_RATE`). Its steps are separate methods because the callers interleave work between them differently.
- **`LaunchSmears`** ([The launch smear](cannon.md#the-launch-smear)) and **`Crosshair`**, which takes an opacity (1 in free mode, `PreciseAim.Blend` in game mode) and a strain that turns it `Crosshair.WARNING` against the elevation clamp (#431; "The landing preview" in `docs/game-session.md`).

## What stays with each executable

Real differences, not unfinished tidying: the stepping policy and the cull; the ceiling's height (the level count in the Testbed, a descending plate in the Game); the field's vertical placement, `GameCameraFit.Solve`'s Y arguments (the Testbed's lattice frame is the world frame; the Game hangs each field above its death line, "Levels in the game" in `docs/game-session.md`); the camera type and its set order; the field of view, handed to the fit as rendered; the Game's transmute and next-colour policy; the `[camera]` and `[aimcheck]` lines.

## Rules the one copy rests on

- ⚠ **`GameCameraFit.Solve` never writes a `Cannon`**: the `OrbitRadius` setter moves `Position`, which the camera half of the solve reads, so the solve returns the radius for one assignment afterwards.
- ⚠ **The alternation needs all three `CONVERGENCE_ROUNDS`**: a sub-0.1 residual survives to the third round, so dropping rounds would move the fit on some fields.
- **`PreciseAim` returns a pose and touches no camera**, because the Testbed's `BasicCamera3D` needs the set order `FieldOfView → Position → Target` while the Game lerps its drop cinematic over the pose on a `RecoilCamera`.
- `CollectMagazineBalls` passes `BorePose.SlotWorld` rather than multiplying a translation per ball (`BestPractices.md` §6).
- Vector crossings use the named `ToXna()`/`ToNumerics()`, though MonoGame converts implicitly, so the boundary is visible (CLAUDE.md's convention).
