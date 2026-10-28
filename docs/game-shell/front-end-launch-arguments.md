# Launch arguments that open or drive the front end, and testing it

Part of [The game: shell](../game-shell.md).

A locked desktop takes no keystrokes, a synthetic one lands in whichever window has the focus, and no script moves the Game's pointer, hence these. The full list is "The command line" in `docs/game-shell.md`.

## Pages at boot

- `settings` / `settings=<row,...>` / `settings=tab:<name>`: the Settings page (#189). Rows (`online`, `nickname`, `remove`, `intro`, `brightness`, `resolution`, `quality`, `auto`, `scenery`) are activated through the page's own handlers, each putting up the page that holds it first; `tab:<name>` puts up a page by its tab (`settings=tab:audio`). ⚠ `remove` is refused outside `userdata=`: it would take the player's scores off the server.
- `pick` / `pick=<chapter>`: the level picker, on that chapter (#273).
- `pickfocus=<level>@<s>`: rests the picker's pointer on that level's tile at that wall-clock second (#684), so the detail line, preview and boards plate follow.
- `about` / `about=play` / `about=<n>`: the About page, `play` also starting the original-score player and opening the player's page, the last (#443; pair with `shot=` past the piece's render time), a number opening it on that page of five (#807, #1034); `about=2:play` takes both.
- `scenes`: the Scene page (#773). `extras`, `jukebox`, `jukebox=<chapter>`, `jukebox=<chapter>:play`: Extras, or the Jukebox over it (#704). `benchmark`: Extras > Benchmark, started at once ([The benchmark](benchmark.md), #988); `benchmarkplay` starts it at its play, without the tours (#1018, testing only: shown, never saved or sent). `continue`: the main menu's first entry pressed past the title card, New Game or the saved campaign continued with its scene's intro (#870).
- `nickprompt`, `nickprompt=<steps>`, `nointernet`: the nickname plate ([Online rows](online-and-nickname.md#testing-it)).
- `menucursor=<seconds>[:<steps>[:<presses>]]` (#799): at that wall-clock second the pad's cursor comes up on the page that is up and walks that many entries down (from the top of the walk, not from the page's arrival entry). `<presses>` (#807) are sideways presses made after it, 0.3 s apart, through the menu's own calls: `left`/`right` as the arrow keys, `lb`/`rb` as the shoulders, each logged as `[cursor] … turned the page`, `walked the row` or `nothing`, then where the cursor ended: `about=5 menucursor=6:1:right,right,left,left,lb` walks from Play on the player's page, the last: Next, nothing past the last page, back to Play, past the row's start to page four, and on LB to page three. `pad` pins the menus' prompts and the session's to the pad ([Menu navigation](menu-navigation.md)).
- `nosplash`: skips the intro logo for one run.

## What the front end hangs and plays

- `preview=<n|name>` pins which map the front end hangs, as `level=` pins the played one; the camera is framed per map, so two shots differ unless it is passed.
- `menugun=<off|pass>` (#1040): `off` is the front end without its own gun and without the camera's pass at it (the menu as it was, for a before/after pair); `pass` makes every cycle's excursion the pass at the gun, the only way to photograph one on a schedule (the wide leg is still first, so with `shot=` at 3 s intervals from 10 s a pass is on screen from about 20 s). A `[orbit] gun pass:` line says when one begins.
- `balls=<beach|bubble>` draws every ball in one style (#258): a testing lever, **not a setting**, since the style belongs to the map's author.
- `colourcue=<none|motif|motif:N>` sets the colour cue (#737), the figure every colour carries painted beside its hue. Without the argument every style but the lava draws its own figure set (the lava draws none) (`motif`, also `a`, `on` or `style`); `none` draws no figure, the look the balls had before; `motif:N` (1 to 4, or `figures`, `emblems`, `lines`, `spots`) puts set N on every style for a comparison ("The colour cue" in `docs/rendering/ball-styles.md`).
- `tomenu=<s>` leaves the level for the main menu at that wall-clock second with the session **kept**, as the pause's Main Menu does; on `shot=`'s clock: `level=32 tomenu=20 shot=16,26,32`.
- `detonate=<t1,t2,…>` sets off the lowest reachable bomb at those seconds (#389), answered as a real blast; it changes play, like `wildcard=`.
- `wind=<scale>` multiplies the air's push on the hanging cluster (#95): 0 is still, 1 the table in `Game/SceneWind.cs`; none below the High tier (`QualityPreset.ClusterWind`).
- `seed=<n>` pins the session's generator (#582: the magazine's deal, the transmute, the drop cinematic's and chapter intro's rolls). Each level prints `[session] seed N`, so `level=<the level> seed=<N>` replays a report's queue.

## Testing the front end by keys

- **Down/Enter is deterministic where a click coordinate is a guess.** The first Down only *raises* the cursor, and each later screen arrives with the cursor already on entry 0, so Down steps *off* it.
- ⚠ **A key must be held across a frame.** At the default 2× supersampling the front end runs about 10 FPS until the adaptive path lowers it, so a 120 ms press lands between polls and is dropped. Hold about 400 ms and pass `ssaa=1`.
- ⚠ **Do not drive the Fullscreen row with Enter to get out of fullscreen**: going in works, coming out did not (twice; probably the mode change moves the pointer, which puts the cursor down so Enter presses nothing). **F11** is the same `ToggleFullscreen` with no menu focus in the way, and the round trip is clean on it.
