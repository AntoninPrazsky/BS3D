---
name: verify
description: How to build, launch and observe the BS3D Testbed game to verify changes at runtime.
---

# Verifying BS3D changes in the running game

## Build and launch

```powershell
dotnet build C:\Projects\Testbed.sln          # builds libs + Testbed + compiles MonoGame content (.mgcb)
```

The exe is `Testbed\bin\net10.0-windows\Testbed.exe`. CLI arguments (any order):

- a path to a map JSON — loaded right at startup
- `autoshoot` — shoots a random ball every second and logs one line per second to stdout:
  `[autoshoot] FPS: <n>, balls drawn: <after frustum culling>/<total>`

Launch headless-ish (it still opens a window, 1280x800) with stdout captured:

```powershell
Start-Process Testbed\bin\net10.0-windows\Testbed.exe -ArgumentList '"<map.json>"','autoshoot' -RedirectStandardOutput out.log -PassThru
```

Don't try SendKeys into the SDL window — it's unreliable. Drive tests via CLI args instead.

## Useful maps

- `Testbed\Maps\Full.json` — legacy format (no field dimensions), ~1000 balls
- `Testbed\Maps\Dense20x10x15.json` — full 20×10×15 grid, 3000 balls; the stress map for rendering/physics perf
- `Testbed\Maps\20x10x15.json` — sparse map with hanging chains (tests cluster release / ceiling disconnection)

FPS is vsync-capped at 60 (`PresentInterval.One`); baseline non-instanced rendering did ~30 FPS on the dense map.

## Screenshot of the game window

The game has no screenshot hotkey; capture the window from PowerShell with
`user32.dll GetWindowRect` + `System.Drawing.Graphics.CopyFromScreen` on the process's
`MainWindowHandle` (call `SetForegroundWindow` first; keep spaces around `-` in `$r.Right - $r.Left`
or PS 5.1 misparses). The FPS counter and ball/constraint counts render in the top-left overlay.
