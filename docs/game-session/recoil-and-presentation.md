# Recoil, precise aim and the arena as the Game draws it

Part of [The game: a session](../game-session.md).

## Recoil and camera shake

- **`Prazsky.Core.Camera.CameraShake`** produces **offsets only** (rotation, lens displacement, set-back, FOV punch), so a kick never accumulates into the pose and at rest every output is exactly zero. A **directional recoil** (thrown back, muzzle rising: what separates recoil from a wobble) plus a **random rattle**; both decay **linearly in time** so the shake truly ends, amplitude is trauma squared, and the rattle's phase runs off accumulated seconds, never a frame counter (a per-frame random shakes faster on a faster machine).
- **A blast in the scene is a RUMBLE, not a kick** (#389): `CameraShake.Rumble` is a third channel (slower and lower than the rattle) because a bomb heaves the whole world and through `Kick` read as the gun firing twice. Its dials are **not** scaled by `CAMERA_SHAKE_SCALE`. Fed from `Blasts.Update` (`BLAST_RUMBLE` x size) on the world's clock, not under the result page.
- **`RecoilCamera` is its own `ICamera` because of roll**: `BasicCamera3D` rebuilds with a fixed world up; `RecoilCamera` keeps the base pose separate and rolls the up vector about the view axis, which is what reads as "the camera was hit". `BaseRoll` is the drop cinematic's tilt, added to the shake's.
- **`CAMERA_SHAKE_SCALE` scales the ceiling and `RECOIL_KICK` stays 1**: `Kick` saturates at 1, so a fractional kick would let two quick shots climb back to a too-strong response. The shared `Cannon`'s own stroke (`RECOIL_BACK`, `RECOIL_DECAY`, `CARRIAGE_RECOIL_BACK`) carries the difference; the executables keep the ticks (`KickRecoil`, `StepRecoil`). **All of it is drawing only**: a shot leaves along the true aim before the barrel moves.
- **The camera pose** is `GameCameraFit.CameraPosition`, back along `Cannon.StandBearing` **flattened to the horizontal** (taken straight it tilts down by however far the gun stands below the cluster and eats the camera's height), `CAMERA_HEIGHT` below the trunnions.

## Precise aim (ADS) in the Game

The shared `PreciseAim` (lean, parallax: "Precise aim (ADS) (Testbed)" in `docs/testbed.md`). `PreciseAim.Blend` eases 0 to 1 and `BlendedPose` lerps overview and leaned pose, so at 0 the overview returns bit for bit.

- **The left trigger is analog (#520)**: `LeanAmount` maps the pull over `TRIGGER_REST`..`TRIGGER_FULL`. `TRIGGER_REST` sits a hair above the trigger's zero (MonoGame applies no dead zone, and a worn trigger must not hold the blend, and the defocus riding it, above zero).
- **It returns a pose as a value** because `RecoilCamera` takes a base pose, not `BasicCamera3D`'s ordered setters; the drop cinematic lerps over it and the shake goes on top. **Focus**: `UpdateAim` does not run while inactive (`_adsHeld` is cleared there) but `Step` still runs, so the lean eases out and an unfocused pad trigger cannot hold it. `PreciseAim.FLOOR_CLEARANCE` floors the lens above the stone under it (`FloorHeightAt`). The lean also defocuses the periphery (#214; "The end of a level goes out of focus" in `docs/game-feedback.md`).
- **`Crosshair`** is drawn **only while ADS leans in** (the only pose whose lens looks along the shot) and not once the level is over, because the lean freezes under the result page (#259). That is the caller's decision: it takes an opacity. The FPS line (`InfoRenderer`) is scaled by `Viewport.Height / 2160`, so `RecomputeScale` must re-run on a resize **and** a fullscreen switch.

## Clouds, occlusion, the island

- **Clouds are the Testbed's weather minus its ambient half**: `_clouds.ApplyTo` on **both** the sky and the instanced effect each frame off the wall clock, so the cloud seen and the shadow it throws are one cloud. `CloudField.ApplyDome` is **not** optional: the two cloud colours have no defaults (unset is a black deck) and it tells both shaders where the sun stands (#220). `SkyLightRig.StepOvercast` is **deliberately left out**: its daylight palette would lighten a night city as the weather thickened.
- **The occlusion direction must be divided by `BallRenderSet.MAX_OCCLUDERS`** (raw it is up to 12x too long and every surface ball wears a hard black crescent). `BallRenderSet.OcclusionTarget` is the only builder and divides inside; the Game once skipped it.
- **The stone ring is a dish** (`ArenaIsland.DISH_DEPTH`, one constant for `IslandMesh` and `FunnelPhysics.Build`): on a flat ring released balls outside the mouth's radius parked, and Bepu spheres have no rolling resistance so a shallow grade drains them. The gun stands on the dish (`CannonRig.TrunnionHeightAt`; no collider: a drawing fact); the overview lens floor (`LENS_FLOOR_Y`), the ADS floor and the gun's ground-darkening anchor all follow it.
- **The island is a washer, the drain fills it** (#48): `ArenaIsland` builds the whole assembly for both executables and `FunnelPhysics.Build` is its collision floor ("The setting, in one copy" in `docs/scenes.md`). ⚠ **Without a `DetailTexture` the renderer falls through to the plain technique and every `SlabSize`/`CavityStrength`/relief setting is silently dead.**
