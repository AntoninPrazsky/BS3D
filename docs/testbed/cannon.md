# The cannon

Part of [The Testbed](../testbed.md).

Built and drawn by `CannonRig`, posed by `Cannon` ([The hardware shared with the Game](shared-hardware.md)); both executables draw the same gun.

## The barrel and the magazine

`CannonMesh` is a profiled, thick-walled steel tube, open at the muzzle, closed at the breech by a dome with a cascabel, with a **slot** along the top (`SLOT_HALF_ANGLE`) whose cut edges have rim faces.

- **It must stay a tube with a slit**, or it reads as a trough. The balls sit wholly inside the bore (radius 0.5 in `BORE_RADIUS` 0.6), so the slot's width decides how much of the queue shows.
- The magazine is `Magazine.SIZE` real balls added to the shared buckets by `CollectMagazineBalls` at `BorePose.SlotPosition`, `Magazine.SPACING` apart, slot 0 at the muzzle (the spawn point), in the barrel's basis. The Testbed deals `RandomBallType` over all thirteen colours.
- **The freshest round parks in the breech**: the slot stops `CHAMBER_DEPTH` short of the breech face, the dome hides a cavity of that depth, and `BorePose.SlotPosition` clamps the last slot's glide to it. Hood, cavity and park depth are **one constant**, which keeps the handoff seamless.

## The glass over the queue

A thin pane (`CannonGlassMesh`; `GLASS_COLOR`, `GLASS_ALPHA`, `GLASS_SKY_REFLECTION`) fills the slot, with the front of the queue notched out so the round about to fire is never seen through glass.

- **The notch is half an ellipse reaching `GLASS_NOTCH_REACH` (3 ball radii)**, bounded on both sides from the precise-aim lens (the constant's comment has the geometry): shorter hides the front round from that lens, longer uncovers nothing more. It uncovers the second round too.
- ⚠ **So the Game's mark on the next round must not be removed as redundant.** The barrel hides its own muzzle end from the play camera, leaving only the front ball's cap in open air. The Game marks that round with the muzzle collar (`CannonRig.DrawMuzzleCollar`): "The muzzle round's mark: a collar on the gun" in [The magazine strip, the chips and the muzzle's marks](../game-feedback/hud-magazine-and-muzzle.md).
- **The pane is recessed into the steel** (`GLASS_EMBED`, `UNDERSIDE_LIFT`, #708), so nothing is coplanar. ⚠ A pane held off the steel left slits showing the rounds at full colour along the cheeks. To inspect: `campos=` on a circle round the window (centre about (0, -5.6, 20.1) with the gun at rest), `fov=32`, `nopost`.
- **`DrawGlass` is not part of `Draw`**: it is drawn **last of the translucent surfaces**, with the exact matrix the barrel was drawn with, recoil included.
- **Tuning**: only the alpha dims the queue (transmission `1 − alpha`); zeroing the colour and the reflection moved the covered band by half a level of 255, so darken with the alpha and leave those two. Screen brightness goes roughly as `transmittance^0.44` under ACES. The colour arrives premultiplied by the alpha, so lower every channel and keep the saturation in their ratio. The sky reflection is multiplied by the alpha and held far under other surfaces', because at the game camera's grazing angle Fresnel turns it into an opaque pale sheet.
- ⚠ **A pane opaque on only some scenes is a blend-state leak** (#667): `PlantPass.Draw` once left `BlendState.Opaque` set on the meadow, mountains and forest. `PlantPass` now restores its states and `BS3DGame.BeginSceneDraw` restates the translucent baseline. Check the blend state at `DrawGlass` before retuning.

## The carriage

Cheek plates holding the trunnion pins, two axle stubs, a split trail and two omnidirectional wheels (`GunCarriageMesh`, `OmniWheelMesh`/`OmniRollerMesh`).

- ⚠ **Nothing of the frame stands where the tube can reach.** The carriage yaws with the aim, so the tube only elevates and recoils along its axis. The plates hug it (`CHEEK_INNER_X`) only inside the hub (`CannonMesh.NearestSteelWiderThan` less `CHEEK_HUB_CLEARANCE`) and stand `CHEEK_RELIEF_CLEARANCE` past the widest steel elsewhere; the trail sits outboard of that plane; the axle is two stubs because the breech sweeps between the plates. The trail is split because the breech dips between its legs. The pins are the carriage's, not the tube's, because pins riding a recoiling tube would tear along the plates. Check a change by sweeping every elevation and recoil stroke against every part.
- **The gun stands on the stone under it**: `CannonRig.TrunnionHeightAt(radius)` = `ArenaIsland.FloorHeightAt(radius) + AXLE_DROP + WHEEL_RADIUS` on every move, because the island's top is a dish. `CannonRig.StanceGradeAt` pitches the carriage with the ground (`Cannon.CarriageWorld`'s up is the ground's normal, never the aim's), and `GroundHeight` is re-anchored per frame, or the ground darkening saturates under a sunken gun.
- The wheel bodies roll by `Cannon.WheelTravel` (`RollTravel` plus the recoil's shove) and not during an orbit, which drags a carriage rather than rolling it; the rollers spin by `SlideTravel`. ⚠ **Neither walk owns an axis**: the carriage yaws and the walk does not, so `Cannon.AccumulateWalk` resolves each step vectorially into the carriage's drawn axes.
- The roller profile `ρ(t) = R − √(a² + t²)` keeps the wheel round; `OmniWheelMesh.RollerSeats` throws if rollers collide. Rollers are their own instanced draw and fluted **helically**, because a smooth or straight-fluted roller does not show which way it turns.

## The pose: trunnions, world up and the recoil

- **`Cannon.Position` is the trunnions**, the point the barrel turns about. The mesh is laid out about `CannonRig.PivotToFrontBall`, and `Cannon.MuzzlePosition` derives the muzzle the magazine and `ShootBall` hang off.
- ⚠ **The trunnions sit `TRUNNION_SETBACK` behind the tube's midpoint** (#287), like a howitzer's, because elevation otherwise drove the cascabel into the stone. The setback lives only in `PivotToFrontBall`, so everything derived from it moves together.
- ⚠ **Not fixed: the recoil stroke.** At the elevation limit `RECOIL_BACK` drives the cascabel about 0.84 under the stone for ~0.24 s. Clearing it needs a taller carriage (another gun) or variable recoil, which would cost the shot's feel at the elevations most played; that is the owner's call.
- ⚠ **The slot's up is world up, never the camera** (`Cannon.BarrelOrientation`): rolling it towards the camera looked wrong on a stand that only elevates and traverses. The low camera does not always see the queue, and that is accepted.

## The launch smear

`Prazsky.Core.Render.LaunchSmears` (`Shaders/ShotTrail.fx`) sells a launch at `SHOOT_MULTIPLIER` (200 u/s) in both executables.

- **It is anchored at the muzzle**, because the ball attaches in ~0.075 s. It lives `LIFETIME`, fading on `1 − t²`.
- ⚠ **The bright end is the leading (far) end**, because the muzzle end hides behind the barrel. `Add` derives both ends itself, so no call site can invert them.
- Additive, depth-read, boosted over 1 (`BRIGHTNESS`), colour floored at `COLOR_FLOOR` so the near-black ball still leaves a smear.
- ⚠ **Each caller decides when it ages** (the Testbed only while simulating). A screen that stops updating but still draws freezes the smears, so the Game clears them for its result screen.
- **A streak (`AddStreak`, #871)** is the same billboard for one frame: a comet's tail behind something moving now (the Game's Swap, [the Swap](../game-feedback/swap-effect.md)), added from the draw and gone once drawn (`Update` drops any a skipped draw left), at the alpha and half-width it was given and `STREAK_BRIGHTNESS`; the two widths are per smear for it.
- It restores exactly the GPU states it changes, so the translucent baseline the glass after it needs is stated before the scene, never left to the smear.
