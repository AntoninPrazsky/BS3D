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

Coordinates are world units, `.` decimal, invariant culture. Useful reference points:

- The arena/drain is at the **origin**; the island top is `y = -8.5`, the funnel bottom `y = -27.5`.
- The ball cluster hangs **above**, roughly `y = 0..+30`.
- The sea surface is `y = -13`; **`campos=0,-18,24 camtarget=0,-8,0`** is a good under-the-sea vantage
  (shows the island from below and triggers the underwater murk + edge blur).
- **`campos=0,-1,24 camtarget=0,-9,0`** looks down into the drain (good for the funnel / the dark pit).

The synthetic-NumPad limitation from the `verify` skill still applies — scene and sky are best set on the
command line, not by pressing NumPad1/2:

- `scene=<city|sea|savanna|desert|mountain|meadow|neon|forest|space|dream|cavern|moon>` — starting environment. Everything past `neon` sits past the end of the NumPad2 cycle, so `scene=` is the only way to reach those in the Testbed.
- `sky=<1..18>` — starting sky dome. `ssaa=<n>`, `exposure=<f>`, `nocap`, a map path — as in `verify`.
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
- `-Keys` — key names pressed after launch, in order (see below).
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

## Judging lighting / a dome-dependent change

Launch once per dome (`sky=<n>`) rather than cycling in-window; each dome logs its zenith/horizon on load.
Water and the sea's underwater tint follow the dome, so compare the same shot under a few `sky=` values.
