# The Testbed

The Testbed is the project's development instrument (#100): the rig every colour, cost and shading judgement is framed through, and where the shot, the gun and its cameras are built and tuned. **It must never become a second game** (no menu, no HUD, no level flow), because every step towards the game takes away what it is for: a camera that holds still and reads back (`campos`/`camtarget`/`fov`, `C`), passes that can be isolated (`arena=`, `capprobe=`), a CPU mirror checked against its shader (`mirrorcheck`), variants swept in one process (`alt=`), a self-driving input timeline (`at=`, `hold=`, `aim=`, `rmb=`) and its own frame writer (`shot=`, `shotframe=`). The Game's camera orbits and its levels override the scene; the MapEditor has no simulation or gun.

**Its argument spellings and log lines are a contract.** `.claude/skills/verify`, `screenshot` (with `screenshot.ps1`) and `benchmark` drive it by those exact strings, so `Testbed/Diagnostics/TestOptions.cs` may gain switches but never rename one, and a log line changes only together with every script that reads it. Each switch's doc comment in `TestOptions.cs` is its reference.

The gun, the magazine, the camera fit, precise aim, the physics session and the ball draw are one copy each in the libraries (#76), so much of this is also what the Game does.

## Sub-documents

- [The executable](testbed/executable.md) — which file a change belongs in, the harness drivers, the overlay and its key help, and how a shot joins the cluster.
- [Capturing and measuring](testbed/capture-and-measurement.md) — `shot=`/`shotframe=`/F8, `clock=` (the scene's clock started late), reading the camera back with `C`, sweeping variants with `alt=` and its dials, `mirrorcheck`, `softness=`.
- [Driving a run without a keyboard](testbed/scripted-input.md) — the input timeline `at=`, `hold=`, `aim=`, `rmb=`: syntax, what it refuses, why it works minimised; the Game's smaller timeline.
- [The hardware shared with the Game](testbed/shared-hardware.md) — what `CannonRig`, `Magazine`, `GameCameraFit`, `PreciseAim`, `Cannon`, `PhysicsWorld`, `BallRenderSet`, `MouseAim`, `LaunchSmears` and `Crosshair` own, what stays with each executable, and the rules that keep one copy honest.
- [The cannon](testbed/cannon.md) — the barrel and its slot, the magazine in the bore, the glass over the queue, the carriage and omni wheels, the trunnions and the recoil, the launch smear.
- [The game-mode camera and precise aim](testbed/camera-and-aim.md) — the solved overview camera, the controls and the walk, the elevation clamp, `aimcheck`/`aimshoot`, precise aim (ADS), its cursor and its parallax.

## Where the old sections went

Code comments cite sections of this document by title; this table resolves them. The last two rows are bullets other documents quote by name.

| Old section | Now in |
|---|---|
| How the executable is laid out (#73) | [The executable](testbed/executable.md) |
| Saving a frame (#371) | [Capturing and measuring](testbed/capture-and-measurement.md) |
| The overlay's key help, which cannot age (#376) | [The executable](testbed/executable.md) |
| Reading the camera back: `C` (#375) | [Capturing and measuring](testbed/capture-and-measurement.md) |
| Checking the terrain mirrors against the GPU: `mirrorcheck` (#590) | [Capturing and measuring](testbed/capture-and-measurement.md) |
| Hanging a map as rope or cloth: `softness=` (#690, a prototype) | [Capturing and measuring](testbed/capture-and-measurement.md) |
| Sweeping variants inside one process: `alt=` (#151, generalized in #374) | [Capturing and measuring](testbed/capture-and-measurement.md) |
| Driving a run without a keyboard (#373) | [Driving a run without a keyboard](testbed/scripted-input.md) |
| The mouse: a stated aim and a held lean (#379) | [Driving a run without a keyboard](testbed/scripted-input.md) |
| The gun's hardware, in one copy | [The hardware shared with the Game](testbed/shared-hardware.md); the barrel, carriage and glass in [The cannon](testbed/cannon.md) |
| The rest of the hardware, in one copy | [The hardware shared with the Game](testbed/shared-hardware.md) |
| Shooting flow (Testbed) | [The executable](testbed/executable.md) |
| The launch smear | [The cannon](testbed/cannon.md) |
| The cannon (Testbed) | [The cannon](testbed/cannon.md) |
| The game-mode camera (Testbed) | [The game-mode camera and precise aim](testbed/camera-and-aim.md) |
| Precise aim (ADS) (Testbed) | [The game-mode camera and precise aim](testbed/camera-and-aim.md) |
| Cannon controls (a bullet) | [The game-mode camera and precise aim](testbed/camera-and-aim.md) |
| The honest limit is parallax (a bullet) | [The game-mode camera and precise aim](testbed/camera-and-aim.md) |
