---
name: verify
description: How to build, launch and observe the BS3D Testbed game to verify changes at runtime.
---

# Verifying BS3D changes in the running game

## Build and launch

```powershell
dotnet build C:\Projects\Testbed.sln          # builds libs + Testbed + compiles MonoGame content (.mgcb)
```

The exe is `Testbed\bin\net10.0-windows\Testbed.exe`.

**Every run opens with two `[build]` lines saying what it is (#372)** — the managed assembly's write time and
hash, then `shaders <n> set <hash>, newest <name> <time>, oldest <name> <time>`. Check them before believing
any capture or measurement: `set` is a hash over every compiled shader's name and bytes (the authority on
content), and after a shader rebuild that landed, `newest` is the file you just edited. See "Prove the exe is
running the change" in `.claude/skills/screenshot` for the trap they exist to catch.

CLI arguments (any order):

- a path to a map JSON — loaded right at startup
- `autoshoot` — shoots a random ball every second and logs one line per second to stdout:
  `[autoshoot] FPS: <n>, balls drawn: <after frustum culling>/<total>`
- `nocap` — disables vsync (PresentInterval.Immediate) for performance measurements. Reference numbers on this
  machine, dense map, default view: ~300 FPS with physics running (the CPU simulation of 3000 bodies is the
  bottleneck), ~800 FPS with simulation stopped (F5, send via keybd_event scan 0x3F). The autoshoot log line
  includes `LOD: a/b/c` — per-level ball counts of the procedural sphere LOD (thresholds 15/30 world units).
- `fpscap=<n>` — `nocap`'s presentation with a ceiling of n frames a second, so a scene cheaper than the cap
  never runs the card flat out while anything dearer than it still reads its true cost. Prefer it to bare
  `nocap` on the desktop, and read `.claude/skills/benchmark`'s own `fpscap` section first: that machine has
  been hard-resetting under load (#250), capped runs included, so no cap is a guarantee.
- `ssaa=<n>` — supersampling factor, 1–4, default 2. The scene renders into an n× target and is box-filtered
  onto the back buffer, which is what keeps the balls' procedural relief sharp; `ssaa=1` turns it off (and
  hands antialiasing back to 8x MSAA) for a before/after or when a machine is fill-rate bound. On the dense
  map it costs nothing measurable — CPU physics is the bottleneck long before the fill rate is.
- `sky=<n>` — starts with sky dome n (1–18) instead of 1; each dome logs `[sky] Dome n: zenith …, horizon …`
  on load. Use separate launches per dome for lighting comparisons — synthetic NumPad1 presses don't register
  (numpad VKs need NumLock; only extended keys like End/F10 work via keybd_event).
- `balls=<name>` — what the balls are made of for the run: `beach` (the vinyl, and the default for a plain
  map), `bubble`, `marble`, `wool`, `metal`, `ice`, `gem`, `plasma`, `lava`, `porcelain` (#318). It outranks a
  loaded level's own material; without it, a level comes up in whatever it names. `L` cycles at runtime and
  logs `[balls] <name>`, and unlike the numpad keys it is an ordinary key, so a synthetic press does reach it.
- `shot=<t1,t2,…>` / `shotframe=<n1,n2,…>` — the Testbed saves its own frame at those wall-clock seconds, or
  at those frame indices (#371). See "Screenshot of the game window" below; prefer these to any capture that
  goes through the screen.
- `switchmap=<path>` — loads a second map on top of the running one after 10 s (logs `[switchmap] Loading …`);
  exercises the map re-loading path used by F2 and drag-and-drop. Note `Dense20x10x15.json` is completely
  full — nothing can attach to it, so to verify attachment after a switch, switch **to** a map with free cells
  (e.g. `Full.json`) and grep stdout for `Ball placed at` after the `[switchmap]` line.

Launch headless-ish (it still opens a window, 1280x800) with stdout captured:

```powershell
Start-Process Testbed\bin\net10.0-windows\Testbed.exe -ArgumentList '"<map.json>"','autoshoot' -RedirectStandardOutput out.log -PassThru
```

**Drive the keys from the command line, not from the desktop (#373).** `at=<t>:<key>` presses one of the
Testbed's own actions at a wall-clock second and `hold=<key>:<from>:<to>` holds `W`/`A`/`S`/`D` down across an
interval; both accumulate, both take comma-separated lists, and the key names are the ones in the overlay's
help. `at=<t>:Escape` ends the run, so a scripted run need not be killed from outside:

```powershell
Testbed.exe Maps\Full.json scene=meadow at=8:F10 at=9:F12 hold=A:10:14 shot=11,13 at=16:Escape
```

The script announces its plan on a `[script]` line and logs every tap and hold edge, so a typo shows up as
"dropped, no such action" instead of a press nobody can see missing. **Verified with the window minimized** —
no focus, no scan codes, no unlocked desktop. Only `W`/`A`/`S`/`D` can be held (they are the only held input
this program reads, and game mode only), and `at=…:F2` is refused and named, because its modal load dialog is
a Win32 window nothing in the timeline could dismiss.

**The mouse is on the timeline too (#379).** `aim=<t>:<elevation>:<traverse>` puts the barrel at a stated pose
in degrees (elevation above horizontal, traverse off the heading to the field's centre) and
`rmb=<from>:<to>` holds **precise aim** across an interval — the mode this repo's own rule says aim-adjacent
visuals must be verified in, with the button actually held. `C` prints the live aim back in the same spelling
(`[aimpin] … -> aim=<t>:30.0:20.0`), so a framing found by hand can be pinned for the next run. Both work on a
minimised window; both need game mode (`at=<t>:F10`), and a lean asked for outside it says so.

```powershell
Testbed.exe Maps\Full.json scene=meadow at=2:F10 at=2.5:F12 aim=6:30:20 rmb=8:14 shot=7,10,16 at=18:Escape
```

⚠ **The Game has no timeline** — `play`, `level=`, `result`, `shot=` and nothing else — so verifying an
aim-adjacent visual *there* with RMB held still needs the external route below.

Don't try SendKeys into the SDL window — it's unreliable. For that external route,
`user32.dll keybd_event` (virtual key + scan code + extended flag)
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

**The Testbed saves its own frames since #371 — use that, not the screen.** `shot=<t1,t2,…>` writes a PNG at
those wall-clock seconds, `shotframe=<n1,n2,…>` at those frame indices (counted from 1, for anything that
moves), `F8` by hand. They land in `Screenshots\` beside the exe as `testbed-<yyyyMMdd-HHmmss>-<scene>.png`
and each prints `[shot] <path>` — grep that rather than guessing the name. The mechanism is
`Prazsky.Core.Render.ScreenshotWriter`, shared with the Game's `shot=`/`F12` (#191).

It reads the back buffer, so it is immune to everything the external route is not: a **locked** desktop, a
window covering the game, a lost focus click, a window wider than the panel. The old route —
`user32.dll GetWindowRect` + `System.Drawing.Graphics.CopyFromScreen` on the process's `MainWindowHandle`
(`SetForegroundWindow` first; keep spaces around `-` in `$r.Right - $r.Left` or PS 5.1 misparses) — copies a
rectangle of the **screen** and can hand back a sharp picture of the wrong thing; `.claude/skills/screenshot`
records all three ways it does that. It is still what `screenshot.ps1` uses, because a key-driven or held-key
shot needs focus anyway.

The FPS counter, the ball/constraint counts and the key help render in the top-left overlay; `F12` hides it
for a clean plate.
