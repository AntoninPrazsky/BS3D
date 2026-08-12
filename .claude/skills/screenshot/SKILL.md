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

## A LOCKED desktop cannot be screenshotted at all — measured

`screenshot.ps1` falls back to `PrintWindow` with `PW_RENDERFULLCONTENT` when `CopyFromScreen` throws, and the
note there claimed that works while the desktop is locked. **It does not, measured 2026-08-12** (Windows 11,
WindowsDX/DX11): `PrintWindow` returned the window's *frame* — title bar, borders — with a **blank white client
area**, eleven captures in a row byte-identical, because a flip-model D3D11 swap chain has no GDI surface to
print. And `CopyFromScreen` grabs the **lock screen**, since that is genuinely what is on the desktop; it is
also the reason it sometimes throws an invalid-handle `Win32Exception` and sometimes quietly succeeds with the
wrong picture. `Get-Process LogonUI` is the check for the state.

Nor do the `-Keys` presses or the focus click reach the app. So while the session is locked there is **no
scripted way to see this game's picture**. Two ways out, in order of cost:

- Unlock the desktop and screenshot normally. Everything above works.
- Have the app save its own frame: `GraphicsDevice.GetBackBufferData<Color>(pixels)` at the end of `Draw` into
  a `Texture2D` and `SaveAsPng`. Verified working while locked (it is the swap chain's own back buffer, so no
  screen is involved) — that is how the result screen's defocus was judged. It is not wired into the game, so
  it means a temporary patch; keep it out of the commit.

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
