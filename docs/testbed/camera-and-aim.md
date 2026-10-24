# The game-mode camera and precise aim

Part of [The Testbed](../testbed.md).

F10 enters game mode: a camera behind the gun, the mouse on the barrel, precise aim on the right button. The fit and the lean are the shared `GameCameraFit` and `PreciseAim`, so the Game plays the same way.

## The overview camera

Behind the gun and **below** its trunnions, to show the whole map from underneath.

- **`Cannon.StandBearing` is flattened**, so `GameCameraFit.CAMERA_HEIGHT` alone sets the camera's height; unflattened, it tilts down ~30° and backs the camera onto the barrel's axis.
- **The angle on the map is small** (~25° from the floor): a camera behind the gun sees the cluster flatter than the barrel does. That is why `GAME_FOV` is narrow and why precise aim exists.
- **The view does not ride the aim**: it looks at the field centre, because a view along the barrel swings the board off screen.
- **Stand-off and aim height are solved, not tuned** (`GameCameraFit.Solve`, on load and resize): the smallest distance that fits the field's corners, the ceiling's top and the gun, which counts as a `CannonRig.BarrelReach` box up and sideways but only down to its wheels.
- **The gun is placed relative to the camera** (`CANNON_CAMERA_STANDOFF`), because the queue in its slot works as HUD and must keep its size on screen; `CANNON_FIELD_CLEARANCE` and `CANNON_MAX_REST_ELEVATION` are lower bounds. The two solves alternate ([why three rounds](shared-hardware.md#rules-the-one-copy-rests-on)).
- The lens height is floored at `LENS_FLOOR_Y`, or it sinks under the island's coping behind a sunken gun.
- ⚠ **A cannon nearer the field does not close the empty ground below the map**: it moves away from the camera and down the frame. The gap is the vertical drop from map to gun.

The Game's per-level figures: "The camera and the gun are solved to the field" in `docs/game-session.md`.

## Controls

- **Cannon controls (game mode)**: the **mouse** aims; **LMB or Space** fires; **A/D** orbit; **W/S** walk towards the field and back. In free mode they fly the camera.
- **The walk** (`Cannon.Advance`) spans `CANNON_ADVANCE_STROKE`, its near end clear of the footprint and the drain (`CANNON_DRAIN_CLEARANCE`); the rest radius sits `CANNON_ADVANCE_FORWARD` out from it (#364), or W does nothing. **The ends are rubber** (`ADVANCE_EASE_ZONE`).
- **The lens follows the half of the walk that backs away and none that closes in** (`CANNON_ADVANCE_FOLLOW`, #322), because the solved stand-off is the tightest fit and following inwards would crop the field.
- **The aim persists through a game-mode session**, through releasing precise aim and orbiting, because a zoom-out or a reposition must not discard the player's aim; leaving game mode eases it back to rest (`Cannon.ResetAim()`).
- **The cursor is re-centred on the live viewport centre**, not `CameraInputHelper`'s cached one, which goes stale on resize. The Testbed has no sensitivity dial (#384), so its runs stay comparable.

## The elevation clamp

`Cannon.MinElevation`/`MaxElevation` (about −5.7° to +80°) and `MaxTraverse` (±45°); a level may lower `ElevationLimit`, which every clamp uses.

- **Up-biased**, because the clean shot at a cell comes from the orbit angle facing it, ~50–65° on big maps.
- **Pushing into the upper clamp is signalled** (#431): the barrel stretches up to `ELEVATION_OVERSHOOT` and springs back and `ElevationStrain` throbs the crosshair; a shot fired then leaves at the clamp (`ShootBall` fires along `Cannon.FireDirection` from `FireMuzzle`, #1008). The lower end (`MinElevation`) is a soft stop with no signal, its rubber easing back without a rebound, and a shot there leaves at the clamp too (#872).

## Testing the aim: `aimcheck` and `aimshoot`

- `aimcheck` logs, per map, the steepest facing elevation any cell needs against the clamp, and PASS/FAIL (`AimReachability.Check`).
- `aimshoot` (implies `aimcheck`) fires up the field's centre column and at its four top corners (`OrbitToFace`, `AimAt`) and logs wanted against given elevation; a short shot reads `CLAMPED SHORT`.
- Both run headless; `.claude/skills/verify` documents their lines.

## Precise aim (ADS)

Holding the **right mouse button** or the pad's **left trigger** leans the camera over the barrel to look down the aim, so the map reads head-on and the shot goes where the centre crosshair points.

- **The pose**: the lens is `BACK` behind the muzzle and `RISE` above the bore, looking at a point **on the shot ray**. It is floored `FLOOR_CLEARANCE` over the stone under it, because a steep aim drops it below the dished island.
- **Two ups**: the offset uses `PreciseAim.LensUp`, so the lift stays over the barrel; the view gets world up by setting `Target` **last**.
- **One reversible blend**: `BlendedPose` lerps FOV, position and target by `Blend`, returning the overview pose bit for bit at 0, and `Step` runs on unheld frames too, so the lean eases out. Apply the pose **FOV → Position → Target**.
- **The lean slows the cursor** (#384) by `PreciseAim.CursorRateScale`. ⚠ It is a ratio of **half-angle tangents**, not of FOVs, because the projection maps tangents to the screen. The Game reads last frame's blend for it; moving `PreciseAim.Step` earlier to remove that lag would move where the camera reads the lean.
- **In the Game the lean also smooths the cursor** (#644, `PRECISE_AIM_SMOOTHING_SECONDS`), because pointer ballistics deliver whole pixels that stutter under magnification.

### Wiring that must stay

- `CameraInputHelper.CameraMovement`'s `allowMouseLook` is **false** in game mode, or the right button also drives the fly camera.
- Leaving game mode from ADS eases from the close-up (`_freeExitFromAds`) instead of teleporting.
- ⚠ The held flag is gated on `IsActive`, because XInput reports a held trigger to an unfocused window; edge input is skipped for one frame after focus returns (`_wasActive`), or the refocusing click fires.

### Parallax

- **The honest limit is parallax**: the lens sits `RISE` above the ray, so the crosshair is exact only at the convergence depth.
- **The Game converges on the real impact** (#382): `PreciseAim.ConvergeDepth` takes the landing preview's first-hit distance.
- ⚠ **The ease is the risk**: a first-hit distance jumps at every silhouette, so it eases at `CONVERGE_TAU`, slower than `BLEND_TAU` because it follows an event rather than a button the player pressed; it is tracked exactly while the lens is out (so a hold opens converged); and it falls back to the projected centre when the sweep finds nothing, never to `CONVERGE_MAX`, which would snap the look-at out over open sky.
- **The Testbed keeps the projected centre** (`PreciseAim.DepthToClusterCentre`), because it has no shot preview.
