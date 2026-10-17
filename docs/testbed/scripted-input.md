# Driving a run without a keyboard

Part of [The Testbed](../testbed.md).

The Testbed drives its own input from the command line (`Diagnostics/InputScript.cs`), on the wall clock `shot=` counts, so a whole session runs unattended, minimised and without focus:

```powershell
Testbed.exe Maps\Full.json scene=meadow at=8:F10 at=9:F12 hold=A:10:14 shot=11,13 at=16:Escape
Testbed.exe Maps\Full.json scene=meadow at=2:F10 at=2.5:F12 aim=6:30:20 at=6.5:C rmb=8:14 shot=7,10,16 at=18:Escape
```

Use it instead of sending input from another process, which fails silently: wrong scan codes, a refused `SetForegroundWindow`, numpad keys without NumLock, a locked desktop, and a focus click after which the game takes the cursor and the frame comes back in another pose.

## Keys: `at=` and `hold=`

- **`at=<t>:<key>`** runs one of the key table's actions (`ButtonAction[]`) at a wall-clock second. The key is a case-insensitive `Keys` name (`F10`, `End`, `Space`, `D1`, `NumPad2`, `C`, `Escape`), the spelling of the overlay help and `screenshot.ps1`'s `-Keys`, so a key added to the table is scriptable the day it exists. `Escape` ends the run.
- **`hold=<key>:<from>:<to>`** holds `W`, `A`, `S` or `D` (`InputScript.HoldableKeys`), the only input read as held (the orbit and the walk, game mode only). Any other key, or an interval ending before it starts, is refused at the parse.
- Both accumulate and take comma-separated lists. Holds over one interval are exactly simultaneous, and are ORed with the real keyboard.
- One `[script]` line states the whole plan first, then every tap and hold edge is logged; an unknown key prints `[script] dropped, no such action: <keys>`.
- ⚠ **`at=<t>:F2` is refused**: `[script] dropped, opens a modal dialog a script cannot dismiss: F2`, because the Win32 load dialog would stall the run. `InputScript.ModalKeys` is the list.
- A tap calls the action's delegate directly, bypassing the one-frame skip after focus returns that exists for a human's click.

## The mouse: `aim=` and `rmb=`

- **`aim=<t>:<elevation>:<traverse>`** sets the barrel's pose in degrees: elevation above horizontal, traverse off the heading to the field's centre (0 points at the cluster). It **sets** the pose (`Cannon.AimTo`) because the mouse aim integrates deltas against a re-centred cursor and cannot repeat. It is `campos=`'s counterpart for the gun.
- **`rmb=<from>:<to>`** holds precise aim, through the same `PreciseAim.ButtonHeld` decision the mouse and pad feed.
- `C` in game mode prints `[aimpin] elevation 30.0 deg, traverse 20.0 deg  ->  aim=<t>:30.0:20.0`; the time stays `<t>` because an aim pin is an event.
- The gun's clamps (`Cannon.MinElevation`/`ElevationLimit`/`MaxTraverse`) apply and are **reported**: `aim=9:95:70` prints `[script] aim clamped to 80.2/45.0 deg`, because a clipped pose will not reproduce the framing it was copied from.
- A lean outside game mode prints `[script] precise aim asked for outside game mode; it needs F10 (at=<t>:F10)`.
- Angles are degrees everywhere a reader sees them and become radians only in the delegate handed to `InputScript`.
- Fire with `at=<t>:Space`; a shot pressed into the elevation clamp is refused with a `[shot] refused:` line ([The elevation clamp](camera-and-aim.md#the-elevation-clamp)).

## Why it works minimised

- **The timeline ticks outside both gates**: the simulation gate (so it runs against a world its own F5 paused) and `IsActive` (a run nobody sits at is the whole case).
- ⚠ **The scripted lean is ORed in outside the `IsActive` gate, which stays on the devices**, because XInput reports a held trigger to an unfocused window. Inside the gate it compiles and does nothing on exactly the run it exists for.
- **An unattended run** (`at=`, `hold=`, `aim=`, `rmb=`, `shot=`, `shotframe=`, `mirrorcheck`) sets `InactiveSleepTime` to zero, because MonoGame idles an unfocused window at ~50 FPS and `shotframe=` indices and `hold=` ramps come out of the frame rate.

## The Game's timeline

`Game/ScriptedPlay.cs` (#402) takes `sweep=` (the traverse on a sine), `aim=`, `rmb=`, `fire=`, `swap=`, `brake=` and `cut=` on its `shot=` clock, so leaned-in looks such as the motion blur ("Motion blur" in `docs/rendering.md`) can be judged in the Game with the button held, plus `padrt=` and `padpress=` (written into the frame's pad state), `mbflip=` (the blur off and on) and `walk=<from>:<to>[:in|out]` and `turn=<from>:<to>[:left|right]` (#560), which drive `Cannon.Advance`/`Orbit` and, unlike the rest, run outside the focus gate (the ScriptedPlay doc comment lists every argument).
