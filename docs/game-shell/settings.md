# The Settings page: tabs, layout and the rows' rules

Part of [The game: shell](../game-shell.md).

`SettingsPage` is reached from the front end and from a pause (pushed over whatever was up, so Back is a `Pop`). The ONLINE rows are in [Online rows and the nickname plate](online-and-nickname.md), the window in [The window](window-and-dpi.md).

## Pages by category (#686)

- Six tabs (`TAB_NAMES`): **ONLINE · SCREEN · PICTURE · AUDIO · CONTROLS · GAME**. The page **opens on ONLINE every time**, because the first online test could not find the rows among the display toggles.
- **SCREEN:** Fullscreen, Resolution (#801), Quality, Auto quality, Scenery (#808, beside Quality, which it turns Custom), FPS limit: how big and how fast the game draws. **PICTURE:** Brightness, Sky, 10-bit colour (#859), Aberration, Film grain, Motion blur: how the picture looks. They were one DISPLAY page of twelve rows until #869, the whole vertical budget (Back about 24 px clear at 1600×900, 15 at 1280×720); split along the seam the owner's UI review drew, six rows each. **Two tabs and not two columns**, because left and right already turn the tabs (`PageSideways`) and a grid would have needed them for its columns; the pad's walk stays one line down a page. `settings=tab:display` still puts up SCREEN for an older script. **AUDIO:** the four volumes. **CONTROLS:** Sensitivity, Aim sensitivity, Rumble. **GAME:** Tutorial, Intro logo, Drop camera, FPS counter (moved from the then DISPLAY by #859: a readout the game puts in front of the player, and DISPLAY's rows were its whole budget), then the CAMPAIGN pair under its own heading. The destructive rows are each last on their page.
- **Unlock all is a dev-build row (#349, #854).** It is drawn only when `BuildVersion.Release` is null, so a release (a tag build) ends the GAME page at Reset progress, and `ToggleUnlockAll` refuses there too, so nothing can open a shipped campaign through a row the player cannot see. The gate is the build's own word, not the Debug/Release configuration: the owner plays Release-configuration builds of main, and those keep the row. Where it is drawn it is off at every launch and never written (`GameSettings` excludes it).

## The tab row

- **It is not in the pad's walk** (five tabs ahead of the first row would cost five presses). The tabs are turned, wrapping, by left/right (arrows, D-pad, left stick: `PageSideways`), LB/RB (one press, one page), the mouse wheel over the row, and a click.
- A tab is a `MenuButton` whose `Tag` is cleared: `CollectNavEntries` walks only buttons with an `Action` on their `Tag`, so the mouse keeps hover, press and click sound and the pad never lands on one. The LB/RB prompts stand outside the tabs ([The keys are drawn in the UI](menu-navigation.md#the-keys-are-drawn-in-the-ui-799)).
- **The chosen tab is inverted** (`MenuPage.TabRow.SELECTED_BRUSH`, with `SELECTED_TEXT`), the opposite of a row's focus highlight. **ONLINE stays red from any other tab while the boards still want something done** (Off, or No server, #687). A tab turn is "moving on": a typed name is kept if it is one, and an armed "Sure?" stands down.

## Every page stands in the same frame

- The page area is a `Panel` holding all five grids, one shown, fixed at the tallest page's measured height and the tab row's width (#770), so a tab never moves Back.
- **Two columns of one width, the seam on the centre line (#833).** Captions end at the seam, right-aligned; values start from it, left-aligned; a group heading (CAMPAIGN) is centred over it.
- ⚠ Myra lays out only what is visible, so the height is **measured with every page in the tree at build**, and **value labels read "Off" until `Refresh` writes them**: a button round an empty label measures shorter than it stands.
- ⚠ **The page area carries no margin; the gap above Back is Back's own.** Myra counts a `Margin` *inside* an explicit `Height`, so a gap under the area cut the tallest page's last row (the then DISPLAY's).
- **The vertical budget is the trap.** Sizes are 2160p design figures scaled by the viewport's height, so a page that overruns overruns at every resolution and aspect, and rows that run off the bottom take Back, the only way out, with them (#138, #548). **A page past six rows means checking Back is whole at 1600×900 and 1280×720**: twelve on one page left it about 24 px and 15 px clear, and took the rows' spacing and padding down to 12 (`ROW_SPACING`, `ROW_PADDING_Y`, back at 20 and 18 since #869). The tallest page is ONLINE now, its three rows and the privacy list (#868).

## Two kinds of control (#816)

- **A checkbox for a row with two states that would read On and Off; a button that cycles its value for every other row.** Never a slider or a drop-down: one click, nothing left half-dragged, and each change takes effect where it is made, so the scene behind the panel is the preview. A row whose caption already says what is on reads faster as a tick than as the word "On".
- **The checkbox is a member of the menu's button family** (`MenuPage.CheckBox` round a `CheckGlyph`): the same slab and brushes, the pad's focus, the click sound and #800's haptics. The box and the tick are drawn as shapes (`CheckTick`, held to a brute-force cover by `CheckTickTests`), because Anton has no ☐ or ✓ and FontStashSharp drops a glyph a face lacks. On Settings the whole row toggles: a press on the caption is a press on the box.
- **The checkboxes:** Fullscreen, Auto quality and Scenery on SCREEN, 10-bit colour, Aberration, Film grain and Motion blur on PICTURE; Tutorial, Intro logo, Drop camera, FPS counter and Unlock all on GAME; and the note page's "Send the picture". A row the tier, the build or the panel holds is dimmed with its reason beside the box ("(tier)", "(locked)", "(Potato)", "(8-bit panel)", "(driver)"): unticked when held off, ticked when held on (Auto quality's "(resolution)" on the Pi, #832). Auto quality and Scenery take their state from the game's own words (`AdaptiveQualityLabel`, `SceneryLabel`), so each reason is written once.
- **The rows that keep their words:** Resolution, Quality, Brightness, Sky, the FPS limit (two states, but Monitor and Unlimited), the volumes, the sensitivities, Rumble, and Online scores, whose third state, "No server", a box cannot say.

## Sensitivity and aim sensitivity (#384, #497)

- `SENSITIVITY_LADDER` is 50/75/100/125/150/200/300 % of the shipped feel, 100 % the default; **ratios, not an even step** (about √2 apart). `MouseAim.SENSITIVITY` is an angle per *pixel*, so a 4K panel and a 1600×900 one differ 2.4× in sweep.
- **Read per frame** off `BS3DGame.MouseSensitivity`, not pushed into the session: the page opens from the pause, so the row can move under a running level. From disk a value is snapped **by ratio** (`NearestSensitivityRung`, boundary the geometric mean); anything unusable (zero, negative, NaN) becomes 100 %, since a mouse that cannot turn the gun looks like a broken game.
- **Aim sensitivity** is a second rung off the same ladder (`aimSensitivity`), multiplied in **as the lean blends in** (`Lerp(1, AimSensitivity, Blend)`) on top of the general rung and the FOV ratio.
- ⚠ **1.25 is the rung that was missing.** `PreciseAim.CursorRateScale` slows the leaned cursor by 0.828 (the ratio of the half-angle tangents, 42.86° overview against 36° leaned), compensated by 1.208. With rungs 1 → 1.5 a player answering "too slow" could only leave 17 % of the slowdown in or overshoot to 24 % *faster* than the overview; 0.828 × 1.25 = 1.035 is parity. `GameSettings.AimSensitivity` defaults to 1.5 since #644, a little faster than parity. **A new default does not touch a save that already has the key**, on purpose: a settings file is the player's.

## Tutorial and Intro logo rows

- **Tutorial (#189)** is the opt-out for the tutorial's cards (the first chapter's, and the score's two since #666), on by default. `GameSettings.Tutorial` is **read per frame** (`BS3DGame.IsTutorialEnabled`), so off takes a card down at once and back on resumes the lesson that was up. What was *taught* is `PlayerProgress.Lessons`, cleared by **Reset progress** ("The tutorial" in `docs/game-feedback.md`).
- **Intro logo (#621)** is whether the game opens on the splash: `GameSettings.IntroLogo` (`"introLogo"`), absent reads as on. ⚠ **Read once**, when `BuildMenu` builds the stack (`BS3DGame.ShowsSplash`), so a click takes effect at the next launch. `nosplash` skips the splash for one run.
