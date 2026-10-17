# Controls, launch arguments and driving the Game

Part of [The game: a session](../game-session.md).

## Controls

- **Mouse aims; LMB / Space / pad right trigger fire; RMB / pad left trigger hold precise aim** ([Recoil, precise aim and the arena](recoil-and-presentation.md)). **A/D traverse** (the camera trails the turn), **W/S walk** towards the field and back ([The field, the camera fit and tall levels](field-and-camera.md)). **The pad's left stick does both** (#189) at a rate that follows its push (the push past MonoGame's own dead zone raised to `STICK_SPEED_EXPONENT` 1.5, #802), a full push being a key's full speed; a key wins on its axis. `PAD_WALK_DEADZONE` only decides which device the tutorial draws for.
- **E / pad X** swap, **Q / pad Y** brake, **R / pad right bumper** cutter ([Power-ups](powerups.md)).
- **F10** hides the FPS line, **F11** fullscreen, **F12** saves a PNG. **Escape pauses**; quitting is a menu item. In a drop cinematic the gun does not answer and LMB / Space / pad A skip it.
- Every edge-driven input (the pad trigger too: XInput reports it to an unfocused window) is skipped for one frame after focus returns, or the refocusing click fires a shot.

## Launch arguments

Full list: `Game/LaunchOptions.cs`.

- **Display:** `quality=<low|medium|high|ultra|potato>` (`potato` is the tier's preset row, not the Potato render path, which is the `potato` flag, #808), `ssaa=<n>`, `exposure=<f>`, `nocap`, `fpscap=`, `fullscreen`, `windowed` (a run's instruction, never written back, #455), `tenbit` / `tenbit=off` (the 10-bit back buffer forced on or off for the run whatever the panel reports) and `ramp` (a grey ramp over the lower third of the frame that shows the back buffer's depth to the eye) — both #859, "10-bit colour" in `docs/game-shell/window-and-dpi.md`. The first three seed Settings, which then owns them.
- **Scene:** `scene=<name>` (`SceneRenderer.TryParseScene`, one parse for all executables), `sky=<1..20>`, `sceneseed=`, `logfps`. Unpinned, the scene differs every launch.
- **Into a level:** `play` (the first level, through the pop-and-push a Play click takes: `[field]`/`[camera]` only print once a level is built, and building one needs a mouse on a Myra button), `level=<n or name>`, `levelfile=<path>` (a level outside the set; pins the run), `result` (a cleared level's result page, held back until the title card is gone; `lost`, `blockdone` and `stars=` stage its other endings, and `campaigndone` the campaign's, with [the finale](../game-feedback/milestone-endings.md#the-campaigns-finale-a-headline-and-one-message-880) in front of it, `finalemessage=<n>` pinning its message), `celebrate`, `lasers`, `tutorial` / `tutorial=demo`, `settings`, `board=<n>[:<page>]`, `highscores[=<chapter>[:all]]`.
- **Pinning play:** `seed=<n>`, `streak=` (until a shot breaks it, #845), `wildcard=<n>` (overrides every level's rule; the only test door to a wildcard), `powerups=swap:n,brake:n,cut:n`, `lineloss=`, `detonate=`, `balls=`, `wind=<scale>` (0 is still air), `logshots`, `logtilt` (the overview's tilt with the aim, a line a frame, #720).
- **Scripted input** (`ScriptedPlay`): `fire=`, `aim=`, `walk=`, `swap=`, `brake=`, `cut=`, `rmb=`, `shot=`, each `<t1,...>` wall-clock seconds, through the very call the key makes. **`cutshot=<s1,...>`** saves the frames at those seconds of the world's clock after a cutter strikes (#767): a strike cannot be framed on `shot=`'s clock (["Looking at it"](../game-feedback/cut-staging.md#looking-at-it)). **`swapshot=<s1,...>`** saves the frames at those seconds of a Swap's own picture (#871): the picture's age is held at each for a frame, so a swap's third of a second can be framed however long a capture takes (["Looking at it"](../game-feedback/swap-effect.md#looking-at-it)).
- **A level's buttons on the clock** (#709): `actions=<seconds>:<verb>,...`, each through the call its button makes and logged as `[actions] Testing: <verb> over <page>`: `retry` (the result page's Retry), `restart` (the pause page's Restart, made over the pause), `skip`, `next`, `menu` (the result page's Main Menu with one up, else the pause page's, which keeps the session) and `level<n>` (the picker's tile). A verb whose button is not up does nothing and says so. With `quitconfirm=<s>:quit` and `lineloss=` it walks every event the [play counts](play-statistics.md) count.
- **`userdata=<dir>`** keeps a run's save, settings, online identity and outbox out of the player's profile (`UserData.UseForTesting`). **Pass it to every scripted run**, or it writes the owner's real `Settings.json` and `Progress.json`. `Program` applies it before anything asks for the directory; it throws otherwise.

## Saving frames: `F12`, `shot=<t1,t2,...>`

`Prazsky.Core.Render.ScreenshotWriter` (shared; "Saving a frame" in `docs/testbed.md`) writes the presented frame to `Screenshots/` beside the exe and prints `[shot] <path>`. **The schedule is the whole point**: every external capture route can silently return the wrong picture (the lock screen, a covering window, a blank `PrintWindow`), and on a locked desktop no keystroke arrives. `F10` hides the overlay first. A capture is a ~0.1 s edge, never per-frame.

## A resize re-solves the framing and keeps the walk

`UpdateCameraAspect` re-runs `FitCannonAndGameCameraToLevel`.

- ⚠ **It must also drop the aim's baseline**: the aim is a delta against the viewport centre, a fullscreen switch moves the centre under a parked cursor, and the first frame slammed the barrel into its clamp. `OnViewportChanged` calls `MouseAim.Invalidate()` **before** re-solving, and `MouseAim.Initialized` gates the shot test.
- **The walk is kept (#560)**: `Cannon.Refit(rest, min, max)` keeps it as the same share of its half of the new range (a level load's `OrbitRadius` + `SetAdvanceRange` parks the gun at rest).

## The cursor

The capture is a **state the session enters**, never something a frame does: a frame that warps the pointer cannot be dragged, resized or left (#99, #154).

- **Arrival** (Play, a tile, Resume, Retry, Next Level) captures at once, but **only for a pointer already inside the picture** (the entry may have come from the pad with the mouse parked elsewhere, and warping in a pointer nobody offered is the hostage-taking #99 exists to prevent). Unfocused arrival stays free. The pressing click cannot fire a shot or yank the aim (`MouseAim.Invalidate()` drops the baseline).
- **After a focus loss** the cursor is the desktop's until a **left press edge inside the viewport** re-takes it: gated on `EdgeInputAllowed` (the click that brings the window forward must not also capture), bounds-tested explicitly (title-bar coordinates are reported), unable to fire.

## Traps when testing

- ⚠ **Any external `SetCursorPos` reads as a huge aim delta** once captured (recentred every frame): the `screenshot.ps1` title-bar click yanks the aim to the clamps and a shot flies 45 degrees wide. A test artefact: send focus clicks before the level is entered, or set the cursor a known offset from the centre (~0.002 rad per pixel).
- ⚠ **Input is polled once per frame**: hold a key ~40 ms and a mouse button ~400 ms, or a down+up pair is never seen.
- ⚠ **A harness that types into the game posts `WM_CHAR` through `PostMessageW`**, never the ANSI entry point: that one pushes the text through the code page and loses letters such as "ž" (the nickname plate and the note page read typed characters).
