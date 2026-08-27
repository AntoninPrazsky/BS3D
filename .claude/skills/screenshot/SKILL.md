---
name: screenshot
description: Frame the BS3D Testbed from any camera vantage and capture the window, driving the camera, scene, sky and balls from the command line and a few keypresses. Use to judge a rendering change visually.
---

# Screenshotting BS3D from a chosen vantage

Companion to the `verify` skill (build/launch/CLI args/perf logging). This one is about **framing a shot**:
putting the free camera exactly where you need it, picking the scene/sky, optionally clearing or draining the
balls, and capturing the window. It uses one reusable script, `screenshot.ps1` (next to this file).

## Build first

```powershell
dotnet build C:\GitHub\Testbed.sln   # or MapEditor.sln for the editor
```

The exe is `Testbed\bin\net10.0-windows\Testbed.exe`.

## The camera is the key trick: `campos` / `camtarget`

The free (fly) camera has no runtime "teleport", so to frame a reproducible shot the Testbed takes two
startup args (parsed in `Program.cs`, applied in `Initialize`):

- `campos=x,y,z` — place the free camera here.
- `camtarget=x,y,z` — aim it at this world point (defaults to the arena at the origin `0,0,0`).

**⚠ Do not aim straight up or straight down.** A target directly above or below the position is parallel to the
up vector, `Matrix.CreateLookAt` degenerates, and the frame comes back as **one flat colour with nothing drawn
in it** — not an error, not a black screen, just the clear colour, which is the sky rig's own horizon colour and
therefore reads as a plausible empty sky. #151 used `campos=0,0,0 camtarget=0,80,0` as its "sky only" control
and measured a frame that drew nothing at all. Tilt the target: `camtarget=0,80,20` from the same point draws
the dome and its clouds normally. The tell is that two *different scenes* through such a camera give
byte-identical frames — which is how it was finally caught, four months and one wrong figure later.

Coordinates are world units, `.` decimal, invariant culture. Useful reference points:

- The arena/drain is at the **origin**; the island top is `y = -8.5`, the funnel bottom `y = -27.5`.
- The ball cluster hangs **above**, roughly `y = 0..+30`.
- The sea surface is `y = -13`; **`campos=0,-18,24 camtarget=0,-8,0`** is a good under-the-sea vantage
  (shows the island from below and triggers the underwater murk + edge blur).
- **`campos=0,-1,24 camtarget=0,-9,0`** looks down into the drain (good for the funnel / the dark pit).

The synthetic-NumPad limitation from the `verify` skill still applies — scene and sky are best set on the
command line, not by pressing NumPad1/2:

- `scene=<city|sea|savanna|desert|mountain|meadow|neon|forest|space|dream|cavern|moon|outback|tropical|volcano|mars|storm>` — starting environment. Everything past `neon` sits past the end of the NumPad2 cycle, so `scene=` is the only way to reach those in the Testbed.
- `sky=<1..20>` — starting sky dome. `ssaa=<n>`, `exposure=<f>`, `nocap`, a map path — as in `verify`.
- `nopost` — zero the film grain and the chromatic aberration. **Pass it for any A/B of a shader change.**
  Both sit on top of every pixel after the tonemap, and the grain re-rolls per output pixel every frame, so
  two captures of an *unchanged* scene differ in over 90 % of their pixels — a diff without it says nothing
  at all. The aberration is the other half, and it once absorbed four straight attempts at a slab-joint
  artefact that was never in the shader being changed. Turn it back off before judging the *final* look:
  both are part of the authored image.
- `arena=<list>` — which members of the arena are drawn (`cap`, `drum`, `pit`, `rims`, `glass`, `all`,
  `none`; a leading `-` removes). For framing rather than measuring, `arena=none` is how to photograph a
  scene with nothing of the island in front of it.

Even with `nopost`, animated content still differs between two captures: the space scene's slow drift, the
cloud deck and the shadow it casts on everything, and the magazine's pulsing balls. Diff a region that holds
none of them, or choose a scene with no weather.

## Capturing at a higher resolution: `width=` / `height=`

A windowed screenshot is only ever as big as the window, so to judge detail the way it reads at the
desktop's play resolution, set the back buffer directly rather than relying on the 1600×900 default:

```powershell
.\screenshot.ps1 -Out kong.png -GameArgs @('scene=mountain','width=3840','height=2160','campos=0,-1,40','camtarget=0,-9,0')
```

- `width=<n>` / `height=<n>` — windowed back buffer size, overriding 1600×900. The capture is then that many
  pixels across. Pairs with `ssaa=<n>` (internal sharpness) but is separate from it: `ssaa` changes the
  supersampled render target that is box-filtered back down, `width`/`height` change what is presented and
  captured.
- **Caveat:** the capture still uses `GetWindowRect` + `CopyFromScreen`, so a window wider than the physical
  monitor is clipped to the monitor — this gets a 4K capture on a 4K (or larger) panel, but not on a sub-4K
  laptop display. A true decoupled off-screen capture is not yet wired up.

## What a capture can silently be instead of the game — all measured 2026-08-12

`CopyFromScreen` copies **a rectangle of the screen**, not a window. Everything below follows from that one
sentence, and each of these was a wasted round of shots before it was believed.

- **A LOCKED desktop gives you the lock screen.** It is genuinely what is on the desktop at that rectangle.
  Sometimes the call throws an invalid-handle `Win32Exception` instead (the secure desktop), so a batch can
  half-fail and half-succeed *with the wrong picture* — which is what it did. `Get-Process LogonUI` is the
  check for the state. The `-Keys` presses and the focus click do not reach the app while locked either, so a
  key-driven shot is worthless then even if the pixels look plausible.
- **An UNLOCKED desktop gives you whatever window is on top of that rectangle.** A shot came back with this
  agent's own terminal in it, at the game window's exact rect, because the title-bar focus click had landed on
  another window in front of it. The game does not have to be minimized for this — merely covered.
- **So look at every capture before drawing a conclusion from it**, and treat a batch where anything could
  have taken focus as suspect. A contaminated shot is not blank or obviously broken; it is a perfectly sharp
  picture of the wrong thing.

**`PrintWindow` with `PW_RENDERFULLCONTENT` is the fallback and it is UNRELIABLE rather than either working or
not.** Measured the same day on the same machine: against **`BS3D.exe` it returned the window frame with a
blank white client area** — eleven captures byte-identical, the flip-model D3D11 swap chain having no GDI
surface to print — and against **`Testbed.exe` it returned the real picture**, twice, while the desktop was
locked. Nothing in the two apps' device setup explains the difference, so the honest rule is: it may work,
and a `PrintWindow` capture has to be *looked at* before it is trusted. (An earlier version of this section
claimed it never works, on the `BS3D.exe` half of that evidence alone.)

The one route that has never lied: **have the app save its own frame** — the swap chain's own back buffer, so
no screen is involved and it is immune to the lock screen, to focus, to occlusion and to a window wider than
the panel.

**In the Game this is built in since #191** and is the way to photograph `BS3D.exe`:

```powershell
# Two shots of the result screen as its defocus ramps, no keys, no focus, works locked:
C:\GitHub\Game\bin\net10.0-windows\BS3D.exe result celebrate mute scene=meadow quality=medium shot=3.5,8
```

- **`shot=<t1,t2,…>`** — wall-clock seconds after start, one PNG each. This is the trigger that survives a
  locked desktop and the one that makes a shot repeatable.
- **`F12`** does the same by hand (`F10` is the FPS overlay now, `F11` still fullscreen). It cannot be scripted
  while the desktop is locked, which is the whole reason `shot=` exists.
- They land in `Screenshots\` beside the exe as `bs3d-<yyyyMMdd-HHmmss>-<scene>.png`, and each prints one
  `[shot] <path>` line to stdout — grep that rather than guessing the name.
- The shot is the frame **as presented**: FPS line, HUD, whatever menu page is up. `F10` first for a clean
  plate. The frame that takes a shot is long (~0.1 s at 1600×900), so never put this on a per-frame path.

**The Testbed has no such writer** — it has the camera arguments instead (`campos`/`camtarget`), which is why
it stays the framing rig and why an external capture is still the only way to see it. If a Testbed run has to
be seen through a locked desktop, `GetBackBufferData` + `SaveAsPng` as a temporary patch at the end of its
`Draw` is the same ten lines; keep it out of the commit.

## `screenshot.ps1`

```powershell
# Under the sea (murk + peripheral blur):
.\screenshot.ps1 -Out uw.png -GameArgs @('scene=sea','campos=0,-18,24','camtarget=0,-8,0')

# Drain a full map into the funnel and watch it go down the dark pit (End releases the cluster):
.\screenshot.ps1 -Out drain.png -Wait 6 -Settle 5 -Keys @('End') `
    -GameArgs @('C:\GitHub\Testbed\Maps\Full.json','scene=mountain','campos=0,-1,24','camtarget=0,-9,0')
```

- `-GameArgs` — arguments to `Testbed.exe` (a map path, `scene=`, `sky=`, `campos=`/`camtarget=`, `ssaa=`, ...).
- `-Keys` — key names TAPPED after launch, in order (see below).
- `-Hold` / `-HoldSeconds` — key names HELD DOWN as a set, and still down when the shot is taken. The only way to photograph anything the game drives off held input (the W/S advance and the A/D orbit) — see "Holding a key" below.
- `-Wait` — seconds after launch before acting (let the scene settle / the balls fall). Default 7.
- `-Settle` — seconds after the keys before the capture. Default 1.5.
- `-Out` — the PNG path.

The script focuses the window by clicking its title bar (background `SetForegroundWindow` alone often fails),
sends the keys **by scan code** (SDL reads the scan code, not the virtual key), waits, and captures with
`GetWindowRect` + `CopyFromScreen`. Extended keys (End) get the extended flag automatically.

### Keys it knows (`-Keys`)

| Name | Does | Why you'd want it |
|------|------|-------------------|
| `End` | Release the whole cluster | Clear the hanging balls out of the view, or watch them drain into the funnel / pit |
| `F10` | Toggle game mode | The low third-person gameplay camera (overrides `campos`; the fit camera takes over) |
| `F12` | Hide/show the text overlay | A clean shot with no HUD text over the left of the frame |
| `F5`  | Stop/start the simulation | Freeze motion for a still |
| `D1`..`D6` | View presets | Forward/Back/Left/Right/**Up (top-down)**/Down, aimed at the map centre (the cluster) |
| `Space` | Shoot a ball | — |

`End` is the one the task cares about: the cluster falls, so it stops blocking the view, and you can watch
the balls run down the drain (`Balls on scene:` in the overlay ticks down as the funnel culls them).


## Holding a key (`-Hold`): the only way to photograph the gun moving

`-Keys` **taps**: down, 80 ms, up. That is right for every switch in the table above, and useless for
anything the game drives off *held* input — the advance walk (W/S) and the orbit (A/D) move the carriage only
while the key is down, so a tap photographs a gun that has already stopped. Everything hanging off those two
is in the same position: the wheels' roll and their rollers' spin, the carriage's pose on the dish, and above
all the advance walk's **rubber ends** (`ADVANCE_EASE_ZONE`), whose whole point is how the motion *decelerates*.

```powershell
# Orbit left for two seconds and photograph the gun mid-walk:
.\screenshot.ps1 -Out walking.png -Keys @('F10','F12') -Settle 3 -Hold @('A') -HoldSeconds 2 `
    -GameArgs @('C:\GitHub\Testbed\Maps\Thirteen_Colors.json','scene=meadow')
```

- `-Hold` — key names held down **as a set**: every down goes out before any sleep, so `@('W','A')` really is
  simultaneous rather than staggered (which matters: a diagonal walk is what makes an omnidirectional wheel
  decompose its motion into roll and slide).
- `-HoldSeconds` — how long they stay down. Default 1.5.
- **The keys are still down when the shot is taken.** That is the point of the parameter; there is no
  after-release variant, because a released walk key leaves nothing to photograph.
- **`W`/`A`/`S`/`D` are in the key map** and are only ever useful here. Tapped, they move the gun by one
  frame's step, which is nothing.
- **Released in a `finally`**, before anything else can fail. A key left down because the script threw is a
  key left down for the whole desktop, and that is the one genuinely dangerous failure this script has. It
  does not survive a hard kill of the shell, so do not Ctrl-C a `-Hold` run if you can help it.

### What a held key cannot do, and how it lies

Everything in "What a capture can silently be instead of the game" applies **twice over** to a hold, because
its failure is silent in a new way: a key sent to a window that has lost focus, or to a **locked** desktop,
moves nothing at all — and the capture then shows a gun that simply did not walk, which reads as a *finding*
rather than as a failed run. `Get-Process LogonUI` before believing one. If a `-Hold` shot looks like the
feature is broken, prove the window had focus before you prove anything about the game.

**The walk ramps, so a short hold measures the ramp and not the walk.** Both strokes accelerate from a
standstill (`ACCELERATION_DELTA`), so under about a second the carriage has barely moved and two shots at
0.70 s and 0.85 s come back visibly identical — which is what happened the first time this was used, and it
looks exactly like the animation being broken. Hold past a second before comparing anything, and take the
pair from the *same* part of the ramp.

**The camera follows the gun**, so the orbit's own arc does not move the carriage across the frame; only the
background swings. That is what makes a two-shot comparison of the wheels work at all: hold for 2.00 s and
2.18 s and the wheel is in the same place with its rollers turned a little further, which is how #129's
roller spin was finally verified end to end.

## Inspecting fine detail (blur, relief, aliasing)

Zoom a region with nearest-neighbour so pixels stay crisp for judging sharpness:

```powershell
Add-Type -AssemblyName System.Drawing
$src = New-Object System.Drawing.Bitmap 'shot.png'
$crop = $src.Clone((New-Object System.Drawing.Rectangle 430,520,760,340), $src.PixelFormat)
$big = New-Object System.Drawing.Bitmap 1520,680
$g = [System.Drawing.Graphics]::FromImage($big); $g.InterpolationMode = 'NearestNeighbor'
$g.DrawImage($crop, 0,0,1520,680); $big.Save('shot_crop.png')
```

Note the HUD text and the crosshair draw **after** the tonemap resolve (display space), so they stay sharp and are
never affected by scene-space effects like the underwater blur — don't judge a blur by the overlay.

## Answering "this ball colour is too close to that one": `palette.ps1`

Recurring issue type here (#152, #246, #294; #285 and #286 are open as this is written), and it has a
measured answer rather than an opinion. `palette.ps1` reads a `Thirteen_Colors` capture, reduces every ball
to its own coloured gores and prints **CIEDE2000** for all 78 pairs, tightest first, plus every pair the
colour under discussion is in:

```powershell
.\screenshot.ps1 -Out palette.png -Keys @('F5','F12') -Wait 8 `
    -GameArgs @('C:\GitHub\Testbed\Maps\Thirteen_Colors.json','scene=meadow','sky=1','nopost','ssaa=2',
                'campos=0,4.5,11','camtarget=0,4.5,0')
.\palette.ps1 -Png palette.png
```

`F5` stops the simulation so the row hangs still, and the bottom row of that map is the enum's own order,
which is what the script's `-Xs` sample points index — move the camera and you must move `-Xs`/`-RowY` with
it. Three traps, each already paid for and repeated in the script's own header:

- **Measure under a bright dome AND a dark one.** A ball rides its dome's light by its own amount — olive
  read 59 luminance under dome 1 and 69 under dome 13 from the same tint — so the two domes disagree about
  which of its pairs is the tightest, and tuning against one alone walks the colour into the other's
  confusion (#294).
- **ΔE76 is the wrong instrument**: it scores a pure lightness gap like a hue gap, and once ranked
  black/navy *ninth* while three pairs nobody has ever complained about measured tighter (#246).
- **The balls pulse** (emissive heartbeat, phase differs per run): about ±0.4 dE of noise on a single
  capture, so shoot twice before believing a small delta.

And the finding that outlives any one issue: **moving one colour alone relocates the confusion rather than
ending it** — check the whole pair list after a change, not just the pair that was complained about.

## Judging lighting / a dome-dependent change

Launch once per dome (`sky=<n>`) rather than cycling in-window; each dome logs its zenith/horizon on load.
Water and the sea's underwater tint follow the dome, so compare the same shot under a few `sky=` values.
