# Entering and leaving play: the session, the cursor, focus

Part of [The game: shell](../game-shell.md).

The menu and the play loop share one window, one mouse and one keyboard, and every rule here exists because a hand-over between them once did something nobody asked for: a shot fired by the click that pressed a button, a pause that went on stepping physics, a menu entry pressed by a click meant for another application. The pages themselves are in [The front end: pages](front-end-pages.md).

## New Game / Continue (#607)

The main menu's first entry is one slot whose label changes (`MainMenuPage.Refresh`, `BS3DGame.NewGameOrContinue`); the level picker is the second entry, "Select Level".

- **New Game** on a save with no progress: straight into level 1 and its tutorial.
- **Continue** once a session stands (it resumes it) or anything is cleared or skipped in the save (`HasProgress`): it starts `ContinueLevel`, the first entry neither cleared nor skipped that is open, which by the sequence rule is the frontier when its star gate is met. When nothing unfinished is open (the frontier is star-locked, or the campaign is finished) it opens the picker, where a lock says what it wants.
- **A player with progress has no one-click restart**, on purpose: Select Level → level 1 replays it without erasing anything, and *Reset progress* in Settings is the friction-guarded way to start over.
- The pad's first press only raises the cursor ([Menu navigation](menu-navigation.md)). With the top entry now starting a level, that rule matters more.

## Returning to the main menu keeps the session

The gameplay screen leaves the stack but *stands*, so the first entry reads **Continue** and resumes it: a mis-click costs a click, not a game. "Is there a game to go back to" is `IsBuilt` (`BS3DGame.HasSession`), **not** whether that screen is on the stack: only the first is the player's question.

## `GameplayScreen.CoveredChanged` is the one door into play

It exists for the input state. The cursor is captured and recentred every frame while playing and left alone in the menu. Without care, the first frame back reads the distance from where the player clicked to the viewport centre as an aim delta and yanks the barrel across the field, and the very click that pressed the button arrives against a stale "released" and fires a shot nobody asked for.

- `MouseAim.Invalidate()` skips the first frame's aim, and `MouseAim.Initialized` gates its shot test (explicitly since #76; before, it was gated by nesting). The gamepad is re-polled there, so a held trigger or Back button has to be released first.
- An arrival **captures the cursor at once when the pointer is already in the picture** (#154); a pad or keyboard arrival with the mouse parked elsewhere stays free ("The cursor" in `docs/game-session.md`).
- The manager raises it on whichever screen ends up on top, so every way of arriving (a first push, a retry, the pop of a pause or a result screen) comes through the one path instead of each remembering.

## Escape and the pause frame (#79)

`UpdateMenuChrome` stores its keyboard snapshot into `_previousKeyboard`, the very field the play loop uses (it stayed on the host so the two sides share one). That is what makes the hand-over clean: the Escape that paused the game is still down on the first menu frame and is not seen as a second press, and the Escape that resumed it is not seen again by the play loop.

⚠ **`UpdateInput` must report whether it paused, and `Update` must stop on it.** `ScreenManager` applies stack mutations at the top of the *following* frame, so merely returning out of `UpdateInput` left the rest of `Update` running on a game that was not paused yet: `UpdateAim`, the ceiling slide, `StepPhysics` and `CheckLevelLost`. `UpdateInput` returns a `private bool`; only the pause path returns `true`, the other early exits return `false` because they write `PreviousKeyboard` alone and rely on `UpdateAim` to write `PreviousPad`, while the pause path writes both itself.

Do not check this by A/B ball counts: the input automation drives real `mouse_event`/`keybd_event` against a 60 Hz poll, and three runs of one unchanged build left 25, 28 and 26 balls.

## Losing the window's focus pauses a level (#355)

The `Game.IsActive` split in `GameplayScreen.Update` hands the pointer back, drops the capture, invalidates the aim and re-arms the pad trigger. Its inactive branch also pushes the pause page and **returns**, for the Escape path's reason (the push is applied next frame, and this frame would otherwise still step the world). The game is left paused when focus returns, which is right: the aim demands a click to re-capture anyway.

⚠ **What runs while the window is away is not what #355 first assumed.** The ceiling does not keep sliding on the wall clock: `SlideCeiling` (since #577, once per physics step) animates only a step a *shot* has earned. Three things do run, and together they are enough to hurt:

- the simulation keeps settling and swaying the hanging lattice;
- the step the **last shot already earned** finishes its hold and slides down unwatched;
- `ClusterLineWatch`'s below-line grace counts time (the world's simulated seconds since #577), so a cluster already under the line loses on time alone.

The front end does **not** pause: the orbiting camera, the music and the preview cluster are the game idling, and there is nothing to lose.

### `nofocuspause`, and `shot=` implies it

`nofocuspause` (`BS3DGame.PauseOnFocusLoss`) opts out, for any run nobody is sitting at (a benchmark measuring seventy seconds of frames, a capture rig on a locked desktop). **`shot=` implies it**: a capture schedule exists to photograph a particular second, and a run that quietly put the pause page up would hand back a picture of *that*, looking exactly like a finding. F11 does not trip the pause, so a fullscreen toggle keeps its activation.

**The unfocused window keeps its full frame rate**: `InactiveSleepTime` is zero for everyone (the owner's ruling, #518). A 100 ms sleep per tick was measured at 1600×900 High on the desktop to take the paused level from 37.5 FPS and 42 % of the GPU's engine time to 10 FPS and 11 %; if a laptop ever needs it, it is one line in the constructor.

## Menu clicks and `Desktop.Render()`

`Desktop.Render()` also reads the input, so it runs fed with input only where the game's own input has stood down, **only while the window is focused**, and **only once the left button has been seen released since the top screen last changed**.

- **Focus gate.** `Mouse.GetState()` reports the button and a window-relative position whether the window has focus or not, so a click meant for another application that landed over a menu entry would press it ("Quit" would close a game nobody was looking at).
- **Released-button gate.** Myra keeps its own previous-mouse-state and is only fed input while a menu is on top. On the first frame a page is drawn it compares a still-held button with whatever state it saw last, frames or minutes earlier, and reads a press that never happened: pausing with the fire button held opened whichever pause entry the cursor sat over (Settings, in both runs that found it).
- `_menuClickArmed` clears whenever `_screens.Active` changes and re-arms on a released button. It is polled **only while a `MenuPage` is on top**, which is when nothing else polls the mouse: the play loop takes its own snapshot in `UpdateAim`, and two reads of one device in a frame is what `BestPractices.md` #5 forbids.
- Keyboard and pad navigation are **not** gated: a page under a held button is still operable, just not clickable.
- Both gates share one path: the desktop is laid out and drawn (`UpdateLayout` + `RenderVisual`) but not fed input, so a just-arrived page and an unfocused window look the same (visible, not yet clickable). It is the rule `MouseAim.Invalidate()` applies on the way *into* play and `_padTriggerReleased` applies to a held trigger.
