# The front end: pages and what they stand on

Part of [The game: shell](../game-shell.md).

The Game's menus are built on **Myra**, the project's one GUI library (the map editor's Myra panel went in #522, so the Game is the only executable on it). Each page is its own class under `Game/Screens/`; `BS3DGame` keeps only the frame's share of the menu: the one `Desktop`, the fonts, the layout scale and the palette. The look is in [The menu's look](menu-look.md), the keys and the pad in [Menu navigation](menu-navigation.md), and entering and leaving play in [Entering and leaving play](session-and-input.md).

## The seam: the screen stack

The menu is built on the screen stack (#65), not on a `GameState` enum. A menu page is *over* the gameplay screen, so no gameplay path runs (no aiming, shooting, physics or game camera): the manager stops updating what is covered, while the frozen game keeps being *drawn* underneath because the page draws what is under it. A pause therefore shows the game exactly as the player left it, camera shake and all. The setting (sky, backdrop, island, drain) is on screen with or without a session, because whichever screen is at the bottom draws it, and that screen hangs a random level's map over the island too (below). The split and the frame's sequence around the one HDR resolve: "The screen stack" in `docs/game-shell.md`.

## The world is built on the first Play

`GameplayScreen.BuildLevel` and `TearDown` build and free the simulation, the ceiling body, the drain's collision mesh and the cluster, which are the expensive part of starting up. The menu comes up without them. Two consequences:

- The ceiling's **mesh and renderer** are the host's, not the cluster's (its kinematic *body* is still the simulation's), because `SkyLitRenderers` must be complete before the first `ApplySkyLighting`.
- A rebuild disposes the whole `Simulation`/`ContactEvents`/`ThreadDispatcher`/`BufferPool` set rather than emptying the cluster ball by ball, in the same order as `UnloadContent`.

## The preview map over the island (#249)

The front end hangs a random level's map over the island: no cannon, no physics.

- **Which map.** `BackdropScreen.RollPreviewMap` draws any **unlocked** entry of the level set, played or not (a menu that hangs a twenty-level cluster over a player who has cleared one spoils the campaign's shape, #266). `Game.PinnedPreviewLevel` (`preview=`) is exempt. An unreadable entry skips to the next; an unreadable set falls back to the bare setting. A `[menu] preview map …` line names what was rolled. It is rolled again on every return to the front end (`ReturnToMainMenu`).
- **Where it hangs.** Through the offset a session derives for the same map (`GameplayScreen.FitClusterWorldOffset`), so the preview sits where the map will hang when played, **raised by one unit** (#254): a played cluster settles a whole diameter under the plate (`BallsConstraintsBuilder.CeilingRestY`, `CeilingPlate.CLEARANCE`) and a preview has no bodies, so it would hang a unit short of the glass. The camera is framed off the raised figure.
- **What it costs.** Parsed data and one instanced draw (`BallDrawFrame.AddMap`'s offset form, the map editor's physics-free path), breathing on the wall clock. For the adaptive-quality probe this means the front-end verdict shades balls ("Adaptive quality" in `docs/game-shell.md`).
- **The menu's own glass.** A second `CeilingPlate` on the host, not a refit of the session's: a session kept for Continue never reinstalls its level and would draw glass cut to another field's footprint. It rests above the preview field's top level from a plain translation (nothing steps it down) and is more opaque than the played one (`MENU_CEILING_ALPHA` against `CeilingPlate.GLASS_ALPHA`), because the menu camera looks almost level from outside the island, where the played whisper vanishes against a bright sky.
- **The scene is not the map's** for the random roll, so the Scene page keeps its meaning. ⚠ The level picker's focus is the exception: it turns the backdrop to the level looked at, scene and all (#405).

## A kept session's gun stands in the menu (#650)

The pause's Main Menu keeps the session (#607). Its gun must be drawn in the front end too, or the island wears the shadow of a gun that is not there: the sun's shadow pass asked the session for its gun, but `BackdropScreen.Draw` drew none.

- **One registration.** `BS3DGame.StandingGun` (`IStandingGun`, `Game/StandingGun.cs`) is set by `GameplayScreen.BuildLevel` and cleared by `TearDown`, and the host asks **it** for everything: `CastShadow` in the shadow pass; `CollectBalls` (the rounds in the bore, before `BeginSceneDraw`, whose shadow pass reads the buckets); `Draw` after the ceiling's background grab; `DrawGlass` after the ceiling's glass. `Draw` is `GameplayScreen.DrawGun`, **one method for play and for the menu**, so the menu's gun is the play gun.
- **It stands where it was left**: the pose is read afresh from the kept session's `Cannon`, frozen because the session is off the stack. No session (a fresh launch, or the result page's Main Menu, which tears it down) means no registration: no gun, no shadow.
- The gun is often backlit in the menu (dark silhouette with a rim on the muzzle); on the meadow, with the sun behind the lens, it is lit like the play frame.
- ⚠ **The original bug reproduces only on `quality=high`.** The low tier carries no sun shadow (`QualityLevel.ShadowMapCap`), so a capture there proves nothing. `tomenu=<s>` makes the state scriptable ([launch arguments](front-end-launch-arguments.md)); `level=2 tomenu=20 shot=28,32,36` at high shows the shadow beside the drain.

## The pages and the dim

Each page owns its tree, swapped into the one `_desktop.Root`. Everything that must agree with the game's state (the resume entry, each setting's value, the marked scene, the result screen's figures) is refreshed in `ShowPage`, because a page can only change while nobody is looking at it.

- **Front end:** Splash, Main menu (New Game/Continue, Select Level, High Scores, Extras, Settings, Help, About, Quit), Level select, High Scores (#685, "High Scores" in `docs/game-session.md`), Extras, Scene, Jukebox.
- **Pause:** Resume, Settings, Scene, Write to the Author, Restart, Main Menu, Quit. Restart is two entries clear of Resume (#383).
- **Others:** Quit confirm (below), Settings, About, Result.
- Settings, Scene and the Quit confirm are reached from both the front end and a pause; About only from the front end. They are pushed over whatever was up, so "Back" is a `Pop`.
- **Only a pause dims the frame.** The front end draws no scrim: the rotating scene is the whole point of that screen. A pause dims hard, because what is behind it is a stopped game. Legibility on the front end comes from the widgets: entries carry their own slab and text-carrying screens sit on a dark plate. `DimsFrame` is `Manager.Contains<PausePage>()`.
- ⚠ **The scrim is drawn by the host's own batch** right before the desktop renders, not set as the Myra root's background (#114): Myra's background paint stops short of the viewport's bottom edge (row 897 against 899 at 1600×900), leaving a strip of undimmed arena across every dimmed page. The root panel still stretches, because it places the page's children and Myra hit-tests it.

## The front end is a composition, the other pages a stack (#217)

A centred column is right for a panel that has the frame's attention and wrong for the one screen whose subject is *behind* it. So the game's name is pinned **top-right** and the entries **bottom-left**, off one shared inset (`MainMenuPage.FRONT_INSET`), leaving the middle to the island and the turning scene. The pause and both pickers stay centred: behind a pause is a stopped game, and a picker is a grid that wants the whole frame.

- It is one Myra `Panel` with two children (`ScreenRoot` takes `params`): a `Panel` arranges every child against the full client rectangle by that child's own alignment.
- ⚠ **Children are added in the order the pad should step through them**, because `CollectNavEntries` follows the order widgets were added, not where they landed.
- The adaptive-quality notice is at the **top** of the entry column: a bottom-anchored stack pushes everything above the point where height is added, and as the last child it shifted all the entries the moment the probe spoke.
- The entries are `FrontEndEntry`, in the front end's own type size ([The menu's look](menu-look.md#typefaces)).

## The Quit confirm (#826)

The game's one "are you sure" page, and it reverses #383's "no confirm dialog" for Quit alone (a player's note asked that the game always ask). Both Quit entries push `QuitConfirmPage` ("QUIT BS3D?", Cancel, Quit) over the page that opened them. Its Quit is the one `Game.Exit` call in the game.

- **Cancel is first and is the page's `NavArrival`**, so the key or pad button that opened the page, pressed again without reading, cancels. Escape and the pad's B are the stack's own back.
- "This run will not be kept." stands under the heading only when `HasSession` (a level being played, or one kept for Continue): a session lives only in memory, and a warning shown when nothing is lost is one nobody reads.
- It sits on a plate, because over the front end nothing dims the scene behind it. The plate does not reach where the pause's Quit stood (at 1600×900 it ends at row 599 and that Quit spans rows 646–695), so a double click on the pause's Quit lands on nothing.
- Left alone on purpose: Restart (two entries clear of Resume already), and the window's close button and Alt+F4 (the operating system's gestures).
- `quitconfirm=` drives it: "The command line" in `docs/game-shell.md`.

## The scene is a real setting

`SetScene` is the one place a scene changes, shared by the random pick at startup and the Scene page, and what the player picks is what they then play in. It switches the city's day/neon lighting (a flag and a brightness on the renderer: neither city goes through `SceneRenderer`), applies the sea's and savanna's default domes as the Testbed does, and ends in `ApplySkyLighting`, so nothing may derive the light rig before it. Scene lights are per-frame (the savanna's campfire flickers) through `SceneLights.Apply`, one copy with the Testbed (#75). The dark pit shaft behind the drain and `TerrainHoleRadius` serve the solid-terrain scenes, classified by `SceneRenderer.IsSolidTerrainScene`.

## Scene, Extras and the Jukebox

- **The Scene page lists scenes in the campaign's order, each named with its chapter** ("Meadow (The Meadow)"), read off the level set (each chapter's first level's scene, `ScenePage.OrderByChapters`); a scene two chapters share names both. Then come the scenes no chapter plays, unlabelled, in `SceneKind` order, which says what is free for a new chapter.
- **Extras (#704)** gave the Scene entry's slot on the main menu its name: the owner wanted no ninth entry. `ExtrasPage` holds the Scene picker and the **Jukebox**, in the front end's own composition (entries off the bottom-left inset, no plate, the scene turning behind them), so it reads as the menu going one level deeper. The pause keeps its own Scene entry.
- **`JukeboxPage`** plays every recording in `Game/Music`, whatever the player has unlocked, grouped by the chapter that plays it (each chapter's first level's `music` word; the family is the name before the first dash), in campaign order: the family's bare file first as "Main theme", its variants after (title-cased from the file name), the front end's and pause's loops last as "Menus". After a recording comes the next one, across chapters and round to the first.
- **While it is open the scene's own sound is silent**: `AudioDirector` fades the bed out over `BED_QUIET_SECONDS` through `ProceduralAmbience.Duck` and back on close, and drops the weather's one-shots (a thunder already on its way included) rather than letting it roll in late.
- Layout: the chapters on the left (a scroller), the chosen chapter's recordings on the right over the visualizer and the transport (`MediaGlyph`).
- The player is `Audio/RecordingPlayer`, on the About player's pattern: its own `DynamicSoundEffectInstance` fed half-second chunks, one recording decoded by `OggTrack.Decode` on the thread pool and the next decoded ahead, `GameMusic.Yielding` asked every frame by the audio director, the Music row's gain. The visualizer reads it through `IBandSource` (the analysis lives in `SpectrumAnalyser`, shared with About). Leaving the page stops it.
- ⚠ With no audio device XAudio2 cannot make the voice: the page logs `[jukebox] … could not be played` and stays usable.

## The level picker fits the frame's aspect (#496)

The menu scales by height (`MENU_DESIGN_HEIGHT`), so the picker kept the same share of the frame's height at every aspect and read as a tall narrow block on a 2.4:1 monitor. `LevelSelectPage.Fit` shrinks the page's geometry (tile widths, pips, arrows, spacing, the header's width) by 16:9 over the frame's aspect, clamped to 0.7–1: exactly 1 at 16:9 and 16:10.

**The rule: what is font-bound keeps the font's size, and `Fit` touches nothing that wraps a glyph.**

- A tile's height is not fitted (its number over its name is font-bound; fitting it clipped every name at 2.4:1).
- The chapter arrows and pips are **drawn shapes** (#847): a triangle (`MediaGlyph.Symbol.Back`/`Play`) and a disc or ring (`PipGlyph`), each centred on its plate to the pixel. As font glyphs (Inter's ◀ and ○ in `FontStars`) their ink was placed by the font's metrics and overflowed a fitted plate at 2.4:1 (#539, then #847). The arrow keeps the font-bound rule: its triangle is `ARROW_OF_CAP` of the heading's capitals (`MenuPage.CapHeightOf`, 53 px at 3840×1600) on an unfitted `ARROW_SIZE` plate. A pip has no font left in it, so its plate (`PIP_SIZE`) and its mark (`PIP_DOT`) are fitted together.
- The detail line under the grid takes the grid's width (`GridWidth`, the arithmetic `BuildGrid` lays the tiles out with) and cuts the rest with an ellipsis, so a long sentence cannot widen the plate as the pointer moves. Its height is not fitted either (`FontSmall` is not).

⚠ **A tile's type must fit its box, at any resolution.** The number is `FontBody` (the display face) and the name and star row `FontTile` (`MENU_FONT_TILE`, Inter): their sum plus the two gaps must fit inside `MenuTile`'s padding, and the sum is written beside `BuildTile` so the next change to either figure redoes it. Myra **clips** a stack that overflows its box: for a while the last line, the star row, was not drawn on any tile at any resolution, and no capture noticed a line *missing* rather than empty (#472, found in #496). The tile size is also set by width: the longest shipped name (*Phyllotaxis*) must fit the tile at `Fit`'s floor. A lock's line carries the price alone (`236 ★`, `#38 first`) and the detail line under the grid says the sentence.
