# The game: shell

Everything around play: the Myra front end and its pages, the screen stack that the pages and the play loop share, the settings, the adaptive quality probe and the quality tiers, the resolution, Send a Note, the command line and the run log.

**No tier lowers the pixels.** The frame renders at the display's native resolution and a tier cuts effects; only Potato may render below native. **The menu is greyscale by rule**, with a short list of deliberate exceptions ([The menu's look](game-shell/menu-look.md)). **Every scripted run passes `userdata=<dir>`**, or it writes the player's own `Settings.json` and `Progress.json`.

## Sub-documents

- [Adaptive quality: the probe](game-shell/adaptive-quality.md) — how the frame-rate probe decides to lower the tier (the refresh-derived floor, the ramp gate, one step per verdict), when it re-opens, why its verdict is never stored, and the Auto quality row
- [The quality tier](game-shell/quality-tier.md) — what Low, Medium, High and Ultra carry, the rule that no tier lowers the resolution, `ApplyQuality` and `ssaa=`/`quality=`, Potato (GamePi's lock, the `potato` argument, Potato chosen in Settings) and the Scenery row
- [What the frame costs: the quality tier's measurements](game-shell/quality-measurements.md) — how to measure without lying, what each lever costs on the reference APU and desktop, and the dated per-level table future tier changes compare against
- [The resolution](game-shell/resolution.md) — the player's Resolution row, how the 3D is drawn small and scaled up, `render=` and `renderHeight`, and the Raspberry Pi's one automatic step
- [The screen stack and the host's frame](game-shell/screen-stack.md) — `ScreenManager`'s rules (two flags, deferred and applied-time-decided operations), `FrameBlur`, why the frame is not "draw the screens back to front", and how the host and the session are split
- [Help, About, the splash and the build tag](game-shell/help-about-splash.md) — the six-subject Help page, the two-column About page and its visualizer, the corner version tag, and the intro splash (legs, skip, `nosplash`)
- [The level picker and the result page](game-shell/level-picker-and-result.md) — the chaptered tile picker (locks, preview, online boards, pager, camera lift), the sideways/row/scroller navigation rules it exposed, and the result page (camera release, snapshot, stars, breakdown)
- [Write to the Author (Send a Note)](game-shell/send-a-note.md) — the note page: entry, picture, context, typing, outbox and `note=`
- [The command line, the run log and the narrow window](game-shell/command-line-and-run-log.md) — `LaunchOptions` and `StartupScript`, `userdata=`, `Logs\last-run.log` and the crash report, and `MenuLayoutHeight`
- [The front end: pages and what they stand on](game-shell/front-end-pages.md) — the seam with the screen stack, the world built on the first Play, the preview map, a kept session's gun, the pages and the dim, the Quit confirm.
- [The menu's look](game-shell/menu-look.md) — the greyscale rule and its exceptions, premultiplied brushes, controls and plates and their tone order, flat fills, sizes authored for 2160p, the two typefaces, the pointer.
- [Entering and leaving play](game-shell/session-and-input.md) — New Game / Continue, returning to the menu, `CoveredChanged`, Escape and the pause frame, pausing on focus loss, `Desktop.Render()` and the input.
- [The Settings page: tabs, layout and the rows' rules](game-shell/settings.md) — the five tabs and what stands on each, how a tab is turned and why it is not in the pad's walk, the frame every page shares (two columns, measured height, the vertical budget a new row must keep), the sensitivity ladder and Aim sensitivity, the Tutorial and Intro logo rows
- [Online rows and the first-launch nickname plate](game-shell/online-and-nickname.md) — the ONLINE rows (Online scores, Nickname, Remove), the red "left to do" rule, the note's measured height and `SpaceWrap`, typing a nickname, and `NicknamePage` (the owner's consent ruling, the ways off the plate, when it asks, its traps and its `nickprompt` levers)
- [The window: size, borderless fullscreen, DPI](game-shell/window-and-dpi.md) — why the windowed size is recorded and restored (`_windowedSize`), why fullscreen is borderless, and per-monitor DPI awareness: its `[display]` log line, sizes in pixels rather than the scale, and what `WM_DPICHANGED` does to the window and its icon
- [Menu navigation: pad, keyboard, mouse and the prompts drawn for them](game-shell/menu-navigation.md) — who owns the highlight, how the entries are found, the focus cursor's rules and held-direction repeat, and "The keys are drawn in the UI (#799)": what is drawn, one hand at a time, `PromptGlyph`, glyph choices
- [The menu camera](game-shell/menu-camera.md) — the flight (wide leg, close pass, crane, the title stepping aside), the map's own framing and the rule that a map is never hung around the lens, and why the wide leg's length belongs to the quality probe
- [Launch arguments that open or drive the front end, and testing it](game-shell/front-end-launch-arguments.md) — `settings=`, `pick`, `pickfocus=`, `about`, `scenes`, `extras`, `jukebox`, `nickprompt`, `menucursor=`, `pad`, `preview=`, `balls=`, `colourcue=`, `tomenu=`, `detonate=`, `wind=`, `seed=`, and how to drive the menus by keys
- [The 3D title (TitleWordmark)](game-shell/title-wordmark.md) — how the game's name is built (swept tube letters), where it is drawn (last in the scene pass, over a cleared depth buffer, gated on the active page), how it is framed (camera projection at the sway's extreme, inward yaw), why the rainbow is admissible, the colour-space and beat rules, and the three-shell keyline/aura order and facet-count traps.

## Where the old sections went

Code comments cite sections of this document by title; this table resolves them. The full text before the diet (#839) is `git show f1183681:docs/game-shell.md`.

| Old section | Now in |
|---|---|
| Adaptive quality (Game) | [Adaptive quality: the probe](game-shell/adaptive-quality.md) |
| The quality tier, and what the frame actually costs (Game) | [The quality tier](game-shell/quality-tier.md), [What the frame costs](game-shell/quality-measurements.md) |
| The resolution (Game, #801) | [The resolution](game-shell/resolution.md) |
| The screen stack (Prazsky.Core + Game) | [The screen stack and the host's frame](game-shell/screen-stack.md), [Help, About, the splash and the build tag](game-shell/help-about-splash.md), [The level picker and the result page](game-shell/level-picker-and-result.md) |
| Send a Note (Game, #813) | [Write to the Author (Send a Note)](game-shell/send-a-note.md) |
| The command line (Game, #583) | [The command line, the run log and the narrow window](game-shell/command-line-and-run-log.md) |
| The run log and the crash report (Game, #570) | [The command line, the run log and the narrow window](game-shell/command-line-and-run-log.md) |
| The menu in a narrow window (#593) | [The command line, the run log and the narrow window](game-shell/command-line-and-run-log.md) |
| The front end (Game) | [The front end: pages](game-shell/front-end-pages.md), [the menu's look](game-shell/menu-look.md), [entering and leaving play](game-shell/session-and-input.md), [Help, About and the splash](game-shell/help-about-splash.md), [the level picker and the result page](game-shell/level-picker-and-result.md), [the 3D title](game-shell/title-wordmark.md), [settings](game-shell/settings.md), [online and nickname](game-shell/online-and-nickname.md), [window and DPI](game-shell/window-and-dpi.md), [menu navigation](game-shell/menu-navigation.md), [the menu camera](game-shell/menu-camera.md), [the front end's launch arguments](game-shell/front-end-launch-arguments.md) |
| The rows stand on pages, one category per tab, since #686 (and its sub-bullets: the rows on each page, the tab row, the chosen tab, "Every page stands in the same frame", "A tab turn is moving on", `settings=`) | [The Settings page](game-shell/settings.md) |
| How the page got here, kept for the reasons rather than the layout | [The Settings page](game-shell/settings.md) (as "The vertical budget is the trap") |
| The ONLINE rows (#548): "Online scores", "Nickname", "Remove scores", and a note under them (and #769, #687) | [Online rows and the nickname plate](game-shell/online-and-nickname.md) |
| The first-launch nickname question (#763) is `NicknamePage` | [Online rows and the nickname plate](game-shell/online-and-nickname.md) |
| The nickname is typed, not cycled, and it is still a button | [Online rows and the nickname plate](game-shell/online-and-nickname.md) |
| Two sensitivity rows since #497: the general one, and "Aim sensitivity" for the lean | [The Settings page](game-shell/settings.md) |
| The sensitivity row is a ladder of ratios, not of steps (#384) | [The Settings page](game-shell/settings.md) |
| The Tutorial row (#189) | [The Settings page](game-shell/settings.md) |
| The Intro logo row (#621) | [The Settings page](game-shell/settings.md) |
| The windowed size is the player's, and `SetGraphics` restores it rather than stating the default (#137) | [The window](game-shell/window-and-dpi.md) |
| Fullscreen is borderless since #157, not a hardware mode switch | [The window](game-shell/window-and-dpi.md) |
| The game is DPI-aware per monitor since #825 | [The window](game-shell/window-and-dpi.md) |
| The menus are drivable from a gamepad (#61), and that decides who owns the highlight | [Menu navigation](game-shell/menu-navigation.md) |
| The keys are drawn in the UI, beside what they do (#799) | [Menu navigation](game-shell/menu-navigation.md#the-keys-are-drawn-in-the-ui-799) |
| The menu camera (and its sub-bullets: the wide leg, once a cycle, the framing is the hanging map's, what the wide leg's length is really holding) | [The menu camera](game-shell/menu-camera.md) |
| `preview=<n\|name>`, `pick`, `pickfocus=<level>@<s>`, `about`, `scenes`, `settings`, `balls=<beach\|bubble>`, `detonate=<t1,t2,…>`, `tomenu=<s>`, `wind=<scale>`, `seed=<n>` | [Launch arguments](game-shell/front-end-launch-arguments.md) |
| Extras and the Jukebox (#704) (the pages) | [The front end: pages](game-shell/front-end-pages.md) (written by the first half); its arguments `extras`, `jukebox` in [Launch arguments](game-shell/front-end-launch-arguments.md) |
| The scene is a real setting, not a menu backdrop | [The front end: pages](game-shell/front-end-pages.md) (written by the first half) |
| The pointer is the game's own, generated at load, and it is the one piece of the chrome WINDOWS draws (#350) | [The menu's look](game-shell/menu-look.md) (written by the first half) |
| Testing it needs either clicks or the pad path, and the pad path is the one to reach for | [Launch arguments](game-shell/front-end-launch-arguments.md) (section "Testing the front end by keys") |
| The level picker fits the frame's aspect since #496 | [The front end: pages](game-shell/front-end-pages.md) (written by the first half) |
| The game's name is a 3D object standing in the scene, not a label over it (#248) | [The 3D title](game-shell/title-wordmark.md) |
| The letters are a hand-authored mono-line alphabet swept as tubes, not the real Anton outlines extruded | [The 3D title](game-shell/title-wordmark.md#the-letters) |
| S and 3 are constructed, not sketched, and a sweep written the wrong way round makes a spiral rather than a wrong-looking letter | [The 3D title](game-shell/title-wordmark.md#the-letters) |
| It is drawn in the HDR scene pass, LAST, in front of everything the scene drew (#600) | [The 3D title](game-shell/title-wordmark.md#where-and-when-it-is-drawn) |
| Nothing in the scene can cut into it (#600) | [The 3D title](game-shell/title-wordmark.md#where-and-when-it-is-drawn) |
| The gate is one test against the active page, in the screen that owns the front-end frame | [The 3D title](game-shell/title-wordmark.md#where-and-when-it-is-drawn) |
| A rainbow here is the SECOND deliberate exception to the greyscale rule, and it has to argue for itself | [The 3D title](game-shell/title-wordmark.md#a-rainbow-is-the-second-deliberate-exception) |
| The keyline is a second, fatter tube off the same skeleton, drawn with FRONT faces culled | [The 3D title](game-shell/title-wordmark.md#the-keyline) |
| It is thinner than it was and it plays the rainbow itself | [The 3D title](game-shell/title-wordmark.md#the-keyline) (as `OUTLINE_HUE_SHIFT` and the third leg of the rainbow's case) |
| The rim's colour arrives as EMISSIVE, not as diffuse, and that is the only way to get a stable one | [The 3D title](game-shell/title-wordmark.md#the-keyline) |
| It cost the batching, and that is the whole price | [The 3D title](game-shell/title-wordmark.md#the-keyline) (one line) |
| Two colour spaces, one object, and getting either backwards is silent | [The 3D title](game-shell/title-wordmark.md#colour-spaces-beat-and-dome-light) |
| The pulse's trough is a floor, and the figure was found over the Moon | [The 3D title](game-shell/title-wordmark.md#colour-spaces-beat-and-dome-light) |
| The coloured haloes are a shell of their own, not a brighter letter | [The 3D title](game-shell/title-wordmark.md#the-aura) |
| Its crest is bounded by what it does to the letters, not by itself | [The 3D title](game-shell/title-wordmark.md#the-aura) |
| Its WIDTH has a hard ceiling that is geometry, not taste | [The 3D title](game-shell/title-wordmark.md#the-aura) |
| The width and the two levels are tied by their PRODUCT | [The 3D title](game-shell/title-wordmark.md#the-aura) |
| THE ORDER OF THE THREE SHELLS IS LOAD-BEARING, AND IT IS: LETTER, GLOW, KEYLINE | [The 3D title](game-shell/title-wordmark.md#order-and-facets) |
| ALL THREE SHELLS SHARE ONE FACET COUNT, because their silhouettes have to NEST | [The 3D title](game-shell/title-wordmark.md#order-and-facets) |
| The ramp is whitened only as much as its dark side needs | (dropped: a tuning figure, held by the `WHITEN` constant in `TitleWordmark.cs`) |
| The letters take the dome's light like everything else in the frame | [The 3D title](game-shell/title-wordmark.md#colour-spaces-beat-and-dome-light) |
| There is ONE composition, and the title stands in it from the first frame it is drawn (#601) | [The 3D title](game-shell/title-wordmark.md#one-composition-and-its-framing) |
| The caller states where the flight is, never the size | [The 3D title](game-shell/title-wordmark.md#one-composition-and-its-framing) |
| Before any of it, "Loading" with one to three dots on black (#637) | [The 3D title](game-shell/title-wordmark.md#where-and-when-it-is-drawn) |
| The splash's tree is still empty, and what it draws is the 2D logo over black that lifts away | [The 3D title](game-shell/title-wordmark.md#where-and-when-it-is-drawn) and [The splash](game-shell/help-about-splash.md) |
| The size and the anchor are solved from the camera's own projection, at the sway's EXTREME rather than flat | [The 3D title](game-shell/title-wordmark.md#one-composition-and-its-framing) |
| The yaw is biased towards the frame's centre and never crosses back | [The 3D title](game-shell/title-wordmark.md#one-composition-and-its-framing) |
| Cost: 1.16 ms of a 24 ms frame, and only on the front end | [The 3D title](game-shell/title-wordmark.md#cost) |
| The 240-unit title font went with the label | [The menu's look](game-shell/menu-look.md) (`MENU_FONT_GAME_TITLE` is gone; already stated there) |
| A session kept for Continue leaves its gun standing (a paragraph cited by code) | [The front end: pages](game-shell/front-end-pages.md) |
| Both halves are partial across several files now (a paragraph cited by code) | [The screen stack](game-shell/screen-stack.md) |
| The build's name (a paragraph cited by code) | [Help, About, the splash and the build tag](game-shell/help-about-splash.md) |
