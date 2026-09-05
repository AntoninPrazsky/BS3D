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

### ⚠ Prove the exe is running the change before you believe a single pixel

**A shader edit can leave the Testbed running the PREVIOUS shader, silently, and the capture then looks like
a finding.** It cost three rounds of tuning and a wrong conclusion written into five files (#326), and there
are two independent causes:

- **`-c Release` builds somewhere else.** The path above — and this script's `-Exe` default — is the
  **Debug** output. `dotnet build ... -c Release` writes `bin\Release\net10.0-windows\`, so every "rebuild"
  can succeed while the exe being launched is hours old. Build without `-c`, or point `-Exe` at the
  configuration you actually built.
- **MGCB skips an `.fx` whose `.xnb` is already newer — and then copies nothing.** The content task only
  copies what it built in that invocation, so an intermediate that is up to date leaves the output directory
  untouched. `dotnet build` prints `Skipping …\InstancedModel.fx` and reports success. Deleting
  `Testbed\bin` alone does **not** fix it; deleting `Testbed\Content\bin` (MGCB's own intermediate) forces
  the rebuild and the copy.

**The check is one line, and it is worth running before every shader capture:**

```powershell
# the .xnb must be NEWER than the .fx
Get-Item Testbed\Content\Shaders\InstancedModel.fx, Testbed\bin\net10.0-windows\Content\Shaders\InstancedModel.xnb |
    Select-Object LastWriteTime, FullName
```

**And when a result is surprising, prove it with a colour.** Tint a constant in the technique to pure green,
rebuild, capture: if the pixels are not green, the exe is not running your file. That is what finally caught
this one, after the measurements had "shown" three different things.

## The camera is the key trick: `campos` / `camtarget`

The free (fly) camera has no runtime "teleport", so to frame a reproducible shot the Testbed takes two
startup args (parsed in `Program.cs`, applied in `Initialize`):

- `campos=x,y,z` — place the free camera here.
- `camtarget=x,y,z` — aim it at this world point (defaults to the arena at the origin `0,0,0`).
- `fov=<degrees>` — the free camera's **vertical field of view**, default 72 (#367). A pose is only half of a
  framing: the same `campos`/`camtarget` at a different FOV puts a ball at a different on-screen size, and a
  ball's own shader band-limits its pattern by screen footprint, so its sampled colour follows that size.
  This exists because the map editor's camera is **45°** and matching it was otherwise impossible — see
  "Comparing the Testbed against the map editor" below. Free camera only; game mode has its own FOV and a run
  that presses F10 comes back to this one.

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
- `balls=<beach|bubble|marble|wool|metal|ice|gem|plasma|lava|porcelain>` — what the balls are made of (#318),
  pinned over a loaded level's own for the run. Without it a plain map draws the vinyl and a level draws what
  it names. **This is the only way to photograph a material on a chosen cluster under a chosen dome**, and
  `Testbed\Maps\Thirteen_Colors.json` is the map to do it on: two rows of thirteen, the bottom one in type
  order 1..13, so every colour of a style is in one frame.
- `nopost` — zero the film grain and the chromatic aberration. **Pass it for any A/B of a shader change.**
  Both sit on top of every pixel after the tonemap, and the grain re-rolls per output pixel every frame, so
  two captures of an *unchanged* scene differ in over 90 % of their pixels — a diff without it says nothing
  at all. The aberration is the other half, and it once absorbed four straight attempts at a slab-joint
  artefact that was never in the shader being changed. Turn it back off before judging the *final* look:
  both are part of the authored image.
- `arena=<list>` — which members of the arena are drawn (`cap`, `drum`, `pit`, `rims`, `glass`, `all`,
  `none`; a leading `-` removes). For framing rather than measuring, `arena=none` is how to photograph a
  scene with nothing of the island in front of it.

- `nooverc` — pin the **overcast lerp** to 0. **Pass it for any A/B of a COLOUR**, and pass it with `nopost`,
  which is the same idea one layer further in. The Testbed is the only one of the three executables that
  steps `SkyLightRig.StepOvercast`: the Game deliberately never does (that palette would *lighten* a dusk
  city as the weather thickened) and the map editor has no deck to step it with — so the program every
  colour judgement in this project is framed in is the only one applying the term. **Re-measured 2026-09-04,
  it is worth less than the first note claimed**, and the corrected figure is the useful one: at its maximum
  (`weather=overcast`, cover 1.000, the lerp settled at 0.982, hemisphere ambient carried from
  `0.093/0.315/0.638` to `0.609/0.633/0.679`) every ball moves **1–2 codes**, **no CIEDE2000 pair moves more
  than 1.4 dE**, and the tightest pair keeps its identity; under a scene's own weather it is under 0.5 dE,
  inside the pulse noise. Pass it regardless — it is the difference between a figure that is the *game's* and
  one that is this program's, and paired captures under it agree within ±0.3 dE — but **do not reach for it
  to explain a large delta**: at two codes on a ball it cannot be one. It takes away that term and nothing
  else: the deck still drifts and still occludes the sun per pixel, because the Game has both. #334.
- **Every run prints one `[overcast]` line** saying which regime it was in, in Release as well as Debug.
  Grep it out of a capture's log before believing a colour delta.

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
| `F5`  | Stop/start the simulation | Freeze motion for a still — **but see the warning below before using it to sample an animation** |
| `L` | Cycle the ball material | Step through the ten `BallStyle`s in enum order without relaunching; `balls=` is the way to *start* on one |
| `D1`..`D6` | View presets | Forward/Back/Left/Right/**Up (top-down)**/Down, aimed at the map centre (the cluster) |
| `Space` | Shoot a ball | — |

### ⚠ `F5` freezes the pulse clock too, so it destroys any sweep of an animation's phase

The standing way to sample a ball's heartbeat — the bomb's charge, the cluster's own throb — is a series of
runs at stepped `-Settle` values, each catching a different phase. **`F5` in that series makes every capture
the same phase**, because it stops the clock the pulse runs on and not merely the bodies. It is not obvious
from the pictures: the captures differ slightly (the beat freezes wherever the keypress landed, which varies
with startup jitter), so the sweep looks like it worked and reports an animation that has collapsed.

Measured on one build of the bomb during #341, same camera, same map: **twenty captures across a cycle with
`F5` spanned 109 to 115 codes of red; twelve captures without it spanned 96 to 142.** The first set was read
as a lost beat and nearly bought a wrong constant.

So: `F5` for a still, never for a phase sweep. The reason it is reached for — the cluster swaying between
captures — is not a real problem for a hanging map by the time `-Wait 9` has passed, and a disc sampled on
one ball tolerates a pixel of sway anyway.

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

## Comparing the Testbed against the map editor (#367)

The editor is the only cross-check the Testbed has, so "do the two agree about a ball's colour" is worth being
able to ask — and #334 asked it by pressing **D1 in both**, which reads as a 77-code gap in the green channel
and is an artefact of the framing. **The two `D1` presets have nothing in common but their name**, and there
are four differences, every one of which moves a sampled colour:

| | Testbed `D1` | map editor `D1` |
|---|---|---|
| distance | a flat **15** units (`CameraInputHelper.CameraOffset`'s default, which the Testbed never sets) | **fitted to the map's AABB** with a 1.1 margin — 11.95 on `Thirteen_Colors` |
| aim point | the world **origin** | the **map's own AABB centre** |
| field of view | **72°** (`FREE_FOV`) | **45°** |
| the map's place in the world | **centred** (`BallsMap.Center`) | the **grid frame**, so the same map hangs at a different X/Z |
| window | 1600×900 | 1280×800 — and the aspect feeds the editor's own fit |

Together those put the balls at roughly a **third** the on-screen size in the Testbed, through a different
depth of the meadow's haze. So the repro is to pin *both* cameras, not to press a key in both:

```powershell
# the editor's D1 for this map, printed by a temporary probe in CenterViewOn:
#   pos (6.25, 2.475, -11.703)  target (6.25, 2.475, 0.25)  fov 0.7854  aspect 1.6
# the same pose in the Testbed, whose copy of the map is CENTRED, so X/Z come back to zero:
Testbed.exe parity.json fov=45 width=1280 height=800 `
    campos=0,2.4748728,-11.9535 camtarget=0,2.4748728,0 nopost nooverc weather=clear
MapEditor.exe parity.json     # then D1, F12 (text), G (the config panel)
```

Hand **one level file** to both rather than a map plus flags: a level carries the scene, the dome and the ball
style, so neither program is left on its own defaults (the editor's are City and dome 1, and it has no
command line at all beyond the file path).

**Measured that way, 2026-09-05, the two programs agree.** Four captures each at stepped settles, means
compared: **green 2.46 dE, red 1.57, yellow 2.27, magenta 2.02** — every one of them *three to five times
smaller* than the gap each program shows against **itself** between two moments (5.8–11.1 dE, the heartbeat;
see the palette section below). Phase for phase they agree to 1–2 codes: the green ball reads
164.4 / 186.2 / 195.9 / 164.5 in the editor and 203.7 / 184.9 / 193.4 / 164.0 in the Testbed over the four,
the same oscillation with one phase landing differently. So **CLAUDE.md's claim that the editor draws the
balls the way the game does is safe**, and #334's 77-code gap was never a renderer difference.

## Answering "this ball colour is too close to that one": `palette.ps1`

Recurring issue type here (#152, #246, #294; #285 and #286 are open as this is written), and it has a
measured answer rather than an opinion. `palette.ps1` reads a `Thirteen_Colors` capture, reduces every ball
to its own coloured gores and prints **CIEDE2000** for all 78 pairs, tightest first, plus — for each colour
named in `-Focus` — every pair that colour is in:

```powershell
.\screenshot.ps1 -Out palette.png -Keys @('F5','F12') -Wait 8 `
    -GameArgs @('C:\GitHub\Testbed\Maps\Thirteen_Colors.json','scene=meadow','sky=1','nopost','nooverc','ssaa=2',
                'campos=0,4.5,11','camtarget=0,4.5,0')
.\palette.ps1 -Png palette.png -Focus @('orange','yellow','brown')
```

**`nooverc` belongs in that line** (#334): without it the palette is read under an ambient the game never
draws. The figures in #246, #294 and #315 were all taken before the flag existed — and **re-read under it,
2026-09-04, none of those three decisions moves**: black/navy 10.2 dE at its tightest over six styles and two
domes, olive's nearest neighbour 10.1, black/brown 6.1–10.2. The term is worth ≤1.4 dE here (see the `nooverc`
bullet above), so it was never what those decisions turned on.

**What the same sweep did turn up is a pair nobody has filed: red/orange, 4.0 dE on the gem under dome 13**
(5.3 marble/dome 13, 6.3 gem/dome 1, 7.2 vinyl/dome 1 — all `-Whole`), tighter than black/brown anywhere.
Whether it reads as confusable in play is a question for the eye, not for this script.

`F5` stops the simulation so the row hangs still, and the bottom row of that map is the enum's own order,
which is what the script's `-Xs` sample points index — move the camera and you must move `-Xs`/`-RowY` with
it. `-Focus` was hardcoded to olive until #286 (left behind by #294); name whatever colours the palette
under discussion is made of, and read every table, because **moving one colour alone relocates the confusion
rather than ending it**. Three traps, each already paid for and repeated in the script's own header:

- **Measure under a bright dome AND a dark one.** A ball rides its dome's light by its own amount — olive
  read 59 luminance under dome 1 and 69 under dome 13 from the same tint — so the two domes disagree about
  which of its pairs is the tightest, and tuning against one alone walks the colour into the other's
  confusion (#294).
- **ΔE76 is the wrong instrument**: it scores a pure lightness gap like a hue gap, and once ranked
  black/navy *ninth* while three pairs nobody has ever complained about measured tighter (#246).
- **⚠ The balls pulse, and a single capture is worth far more noise than this section said until #367.** The
  figure here was "about ±0.4 dE … so shoot twice"; **measured 2026-09-05 it is up to 11 dE**. Same level,
  same camera, same dome, `nopost nooverc weather=clear`, four captures of one green ball at four moments:
  the widest CIEDE2000 gap between two of them was **8.97 dE in the map editor and 11.08 dE in the Testbed**,
  and the raw green channel walked 164 → 186 → 196 → 164. Red, yellow and magenta span 5.8–10.4 dE the same
  way. The editor runs no simulation and draws no clouds, so in *that* program the heartbeat is the only
  thing that can be moving, which is what identifies it.
  **So a single capture cannot settle a delta under about 10 dE.** Shoot a series at stepped `-Settle`
  values and compare the *means*, or compare two programs at a matched phase — where they agree to 1–2
  codes. Every palette figure taken from one capture carries this, tightest-pair rankings included.

And the finding that outlives any one issue: **moving one colour alone relocates the confusion rather than
ending it** — check the whole pair list after a change, not just the pair that was complained about.

## Judging lighting / a dome-dependent change

Launch once per dome (`sky=<n>`) rather than cycling in-window; each dome logs its zenith/horizon on load.
Water and the sea's underwater tint follow the dome, so compare the same shot under a few `sky=` values.
