# Help, About, the splash and the build tag

Part of [The game: shell](../game-shell.md).

`Game/Screens/` holds a `MenuPage` base, one class per page (`SplashPage`, `MainMenuPage`, `LevelSelectPage`, `PausePage`, `QuitConfirmPage`, `SettingsPage`, `ScenePage`, `ExtrasPage`, `JukeboxPage`, `HelpPage`, `AboutPage`, `ResultPage`, the boards' and the nickname's), the two screens that are not pages (`BackdropScreen`, `GameplayScreen`) and `PlayHud` ("The in-play HUD" in `docs/game-feedback.md`). Picker and result: [Level picker and result page](level-picker-and-result.md).

## Help: six subjects, walked (#427)

How it is played, scoring, the odd balls, the campaign, the glass and the line, the controls: **walked with Previous and Next, not scrolled** (a Myra scroll pane cannot be driven by the pad). `help` / `help=<n>` open it at boot. The odd balls come from a table keyed by `BallKind` (`HelpPage.KIND_ENTRIES`).

- ⚠ **The scoring page's worked example is computed from `ScoreKeeper`'s constants**, never typed: a hand-written one is wrong the first time the scoring is tuned and nothing says so.
- ⚠ **The controls page draws keycaps through `BS3DGame.MenuFontPrompt`, not `HudFontPrompt`**: the HUD's fonts are built by `EnsureHudFonts`, which only the gameplay screen calls, so on a menu page it is null, and **a Myra label with a null font draws nothing, silently**. Two columns (#728): keycaps (as wide as the widest pair, `MenuFontPrompt.MeasureString`), the pad's buttons (#799) and wrapping words; both hands shown.
- **Every page stands in the same frame (#728, #729)**: `HelpPage.BiggestPage` builds every page into a scroller, measures it (`Widget.Measure`) and gives the shown scroller the biggest width and height as `MinWidth`/`MinHeight` (minimums, so `MenuScroll`'s `MaxHeight` still scrolls a short window).
- **Back, Previous, Next are one row under the text** (#738); the pad arrives on Next (`HelpPage.NavArrival`), so a player who opened Help is not put on the way out.

## About: two columns (#463)

On `SettingsPage`'s pattern, not a scroller (a pad can focus but not scroll into view). Left: what the game is built on, the repository link (a real `MenuButton`, so the pad reaches it) and Write to the Author ([Send a note](send-a-note.md)). Right: what it was made with and the **original score's player** ("The About page's player" in `docs/game-feedback.md`). **`Leave`, raised on the pop, stops the piece**, which brings the game's music back (`GameMusic.Yielding`).

**`MusicVisualizer`** is the first widget that draws itself (Myra's `InternalRender`, `FlatBrush.Fill`, #712). **Its colour (#730) is the one deliberate exception to the menu's "brightness, never hue"**: an instrument, not chrome. 18 LED segments lit by height (`VisualizerPalette.Led` or `Spectrum`); **peak caps** (`Prazsky.Core.Tools.PeakHold`) fall under gravity integrated exactly in seconds (identical at 60 and 240 Hz), stepped from the wall clock inside its draw (the widget has no `Update`).

## The build's name tag

`BS3DGame.VersionTag`/`MenuPage.VersionTag`, bottom right of the front end and About: `v0.2.1` out of a release, `dev-<short sha>` otherwise, from `Game/BuildVersion.cs` (`Release` is the tag `release.yml` stamped; `Name` is the score submission's `gameVersion`). It shows `DisplayName`, `Name` plus ` ARM64` in an ARM64 process (GamePi; display only). It is not in the pad's walk. ⚠ **A dev build's tag changes every commit**, so a pixel diff of front-end captures from two commits always differs in that corner; a `-p:BS3DReleaseVersion=` build (`Tools/release-screenshot.ps1`) shows a fixed name.

## The splash (#454)

`SplashPage` is the intro and the first page that **owns a piece of time**: pushed at boot, it **replaces** itself with the front end (a pop would leave the scene without a menu). Five legs on the page's own clock (frame-rate independent): black; the 2D logo fades up and holds (`FADE_UP_SECONDS`, `HOLD_SECONDS`); the black fades over the orbiting scene (`BLACK_OUT_SECONDS`); a beat (`BEAT_SECONDS`); the picture fades out as the 3D wordmark grows in (`LOGO_OUT_SECONDS`, #621). **No fly or morph into the wordmark** (ruled out by the owner).

- **The titles cross over, they do not overlap**: `SplashPage.WordmarkPresence` takes the wordmark 0 → 1 over the logo's fade-out, into `TitleWordmark.Draw`'s `presence`, which scales the block's **size** about its corner (the letters are opaque; the scene menu's fly-in, #261, does the same and the two multiply).
- **The page stays on top through all five legs**, so Myra and the menu's keys see no input until it replaces itself, and `IsSplashUp` stays true: every startup-argument page waits on it.
- **The picture is drawn in pixels** (two sprites in the host's overlay batch, not a Myra widget: the black must cover the *frame*, and Myra's paint stops short of the bottom rows, #114): scale `min(width / 3840, height / 1600)`, 1:1 from 3840×1600 to 3840×2160, whole pixels.
- **The skip**: input is ignored for `SKIP_AFTER` (else the click that focused the window skips it). The first press finishes the fade up and runs the rest at `SKIP_RATE`; a second cuts to the menu. `play` skips the splash.
- **Off**: the Settings row Intro logo and **`nosplash`** make `BuildMenu` push the main menu where the splash would stand; `nosplash` is applied, never written back.
- **Testing**: the harness's title-bar click dismisses the intro, so use `shot=`; shots under a second apart trip the quality probe (photograph and time in separate runs).
