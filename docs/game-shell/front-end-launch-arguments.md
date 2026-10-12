# Launch arguments that open or drive the front end, and testing it

Part of [The game: shell](../game-shell.md).

A locked desktop takes no keystrokes, a synthetic one lands in whichever window has the focus, and no script moves the Game's pointer, hence these. The full list is "The command line" in `docs/game-shell.md`.

## Pages at boot

- `settings` / `settings=<row,...>` / `settings=tab:<name>`: the Settings page (#189). Rows (`online`, `nickname`, `remove`, `intro`, `brightness`, `resolution`, `quality`, `auto`, `scenery`) are activated through the page's own handlers, each putting up the page that holds it first; `tab:<name>` puts up a page by its tab (`settings=tab:audio`). ⚠ `remove` is refused outside `userdata=`: it would take the player's scores off the server.
- `pick` / `pick=<chapter>`: the level picker, on that chapter (#273).
- `pickfocus=<level>@<s>`: rests the picker's pointer on that level's tile at that wall-clock second (#684), so the detail line, preview and boards plate follow.
- `about` / `about=play` / `about=<n>`: the About page, `play` also starting the original-score player on page one (#443; pair with `shot=` past the piece's render time), a number opening it on that page of four (#807); `about=1:play` takes both.
- `scenes`: the Scene page (#773). `extras`, `jukebox`, `jukebox=<chapter>`, `jukebox=<chapter>:play`: Extras, or the Jukebox over it (#704).
- `nickprompt`, `nickprompt=<steps>`, `nointernet`: the nickname plate ([Online rows](online-and-nickname.md#testing-it)).
- `menucursor=<seconds>[:<steps>[:<presses>]]` (#799): at that wall-clock second the pad's cursor comes up on the page that is up and walks that many entries down (from the top of the walk, not from the page's arrival entry). `<presses>` (#807) are sideways presses made after it, 0.3 s apart, through the menu's own calls: `left`/`right` as the arrow keys, `lb`/`rb` as the shoulders, each logged as `[cursor] … turned the page`, `walked the row` or `nothing`, then where the cursor ended: `about=1 menucursor=6:1:left,right,right,rb` walks from Play: nothing to the left, Next, past the row's end to page two, and on RB to page three. `pad` pins the menus' prompts and the session's to the pad ([Menu navigation](menu-navigation.md)).
- `nosplash`: skips the intro logo for one run.

## What the front end hangs and plays

- `preview=<n|name>` pins which map the front end hangs, as `level=` pins the played one; the camera is framed per map, so two shots differ unless it is passed.
- `balls=<beach|bubble>` draws every ball in one style (#258): a testing lever, **not a setting**, since the style belongs to the map's author.
- `colourcue=<none|motif|motif:N>` switches on the colour cue (#737): every colour carries a painted figure from set N (1 to 4: `figures`, `emblems`, `lines`, `spots`, which also work as spellings; `motif` and `a` are set 1) on the vinyl, the wool and the marble ("The colour cue" in `docs/rendering/ball-styles.md`). A testing lever, off by default, until the owner picks a set.
- `tomenu=<s>` leaves the level for the main menu at that wall-clock second with the session **kept**, as the pause's Main Menu does; on `shot=`'s clock: `level=32 tomenu=20 shot=16,26,32`.
- `detonate=<t1,t2,…>` sets off the lowest reachable bomb at those seconds (#389), answered as a real blast; it changes play, like `wildcard=`.
- `wind=<scale>` multiplies the air's push on the hanging cluster (#95): 0 is still, 1 the table in `Game/SceneWind.cs`; none below the High tier (`QualityPreset.ClusterWind`).
- `seed=<n>` pins the session's generator (#582: the magazine's deal, the transmute, the drop cinematic's and chapter intro's rolls). Each level prints `[session] seed N`, so `level=<the level> seed=<N>` replays a report's queue.

## Testing the front end by keys

- **Down/Enter is deterministic where a click coordinate is a guess.** The first Down only *raises* the cursor, and each later screen arrives with the cursor already on entry 0, so Down steps *off* it.
- ⚠ **A key must be held across a frame.** At the default 2× supersampling the front end runs about 10 FPS until the adaptive path lowers it, so a 120 ms press lands between polls and is dropped. Hold about 400 ms and pass `ssaa=1`.
- ⚠ **Do not drive the Fullscreen row with Enter to get out of fullscreen**: going in works, coming out did not (twice; probably the mode change moves the pointer, which puts the cursor down so Enter presses nothing). **F11** is the same `ToggleFullscreen` with no menu focus in the way, and the round trip is clean on it.
