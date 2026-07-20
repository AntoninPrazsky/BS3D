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
- `nocap` — disables vsync (PresentInterval.Immediate) for performance measurements. Reference numbers on this
  machine, dense map, default view: ~300 FPS with physics running (the CPU simulation of 3000 bodies is the
  bottleneck), ~800 FPS with simulation stopped (F5, send via keybd_event scan 0x3F). The autoshoot log line
  includes `LOD: a/b/c` — per-level ball counts of the procedural sphere LOD (thresholds 15/30 world units).
- `ssaa=<n>` — supersampling factor, 1–4, default 2. The scene renders into an n× target and is box-filtered
  onto the back buffer, which is what keeps the balls' procedural relief sharp; `ssaa=1` turns it off (and
  hands antialiasing back to 8x MSAA) for a before/after or when a machine is fill-rate bound. On the dense
  map it costs nothing measurable — CPU physics is the bottleneck long before the fill rate is.
- `sky=<n>` — starts with sky dome n (1–18) instead of 1; each dome logs `[sky] Dome n: zenith …, horizon …`
  on load. Use separate launches per dome for lighting comparisons — synthetic NumPad1 presses don't register
  (numpad VKs need NumLock; only extended keys like End/F10 work via keybd_event).
- `switchmap=<path>` — loads a second map on top of the running one after 10 s (logs `[switchmap] Loading …`);
  exercises the map re-loading path used by F2 and drag-and-drop. Note `Dense20x10x15.json` is completely
  full — nothing can attach to it, so to verify attachment after a switch, switch **to** a map with free cells
  (e.g. `Full.json`) and grep stdout for `Ball placed at` after the `[switchmap]` line.

Launch headless-ish (it still opens a window, 1280x800) with stdout captured:

```powershell
Start-Process Testbed\bin\net10.0-windows\Testbed.exe -ArgumentList '"<map.json>"','autoshoot' -RedirectStandardOutput out.log -PassThru
```

Don't try SendKeys into the SDL window — it's unreliable. Drive tests via CLI args instead.
When a real keypress is unavoidable, `user32.dll keybd_event` (virtual key + scan code + extended flag)
after `SetForegroundWindow` does reach the SDL window — e.g. End = `keybd_event(0x23, 0x4F, 1, 0)` then flags `3` for key-up.
`SetForegroundWindow` alone often silently fails when called from a background process, and both games skip
their whole `Update` while `!IsActive`, so the keys are dropped without a trace. Click the title bar first —
`SetCursorPos` to `Left + 60, Top + 12` then `mouse_event` down (`2`) and up (`4`) — and check
`GetForegroundWindow()` against `MainWindowHandle` before sending anything.
SDL reads the **scan code**, not the virtual key, so a wrong scan code silently presses a different key
(F1–F3 are `0x3B`–`0x3D`, F12 is `0x58`, D1–D6 are `0x02`–`0x07`, letters follow the keyboard rows: N is `0x31`).
WinForms dialogs the game opens (F1/F2 file dialogs, F3 new map) are ordinary Win32 windows — those do take
`WScript.Shell.SendKeys`, so a map can be loaded by pressing F2 and sending the path followed by `{ENTER}`.

## Useful maps

- `Testbed\Maps\Full.json` — legacy format (no field dimensions), ~1000 balls
- `Testbed\Maps\Dense20x10x15.json` — full 20×10×15 grid, 3000 balls; the stress map for rendering/physics perf
- `Testbed\Maps\20x20x20.json` — sparse map with hanging chains (tests cluster release / ceiling disconnection)

FPS is vsync-capped at 60 (`PresentInterval.One`); baseline non-instanced rendering did ~30 FPS on the dense map.

## Screenshot of the game window

The game has no screenshot hotkey; capture the window from PowerShell with
`user32.dll GetWindowRect` + `System.Drawing.Graphics.CopyFromScreen` on the process's
`MainWindowHandle` (call `SetForegroundWindow` first; keep spaces around `-` in `$r.Right - $r.Left`
or PS 5.1 misparses). The FPS counter and ball/constraint counts render in the top-left overlay.
