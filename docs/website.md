# The website

**The game's landing page is a static site in `Web/`, built with Eleventy, for `https://bs3d.winphonew.eu/` on the owner's Webglobe hosting** (#815). It is an advertisement, not documentation: what the game is, pictures of it, and how to download and run it. Beside it stands **the blog** (#928, `/blog/`), a few long posts about what was solved while making the game, in the first person plural, with an Atom feed. The page says what the **newest release** does and links to it, so it is published with a release, after the release exists.

## Building and looking at it

```powershell
cd Web
npm ci                      # once: Eleventy, pinned in package.json / package-lock.json
npx eleventy --serve        # http://localhost:8080, rebuilt on every save
npx eleventy                # the plain files in Web\_site, which is all the hosting serves
```

`_site/` and `node_modules/` are not committed. Eleventy only overwrites `_site`; `publish.ps1` deletes it before building, so a removed page does not linger in an upload.

`package.json` **overrides `argparse` to 2.x for the whole tree.** Eleventy's `gray-matter` still pins js-yaml 3, whose own `argparse` 1.x brought in `sprintf-js`, which carries an advisory with no fixed version (GHSA-hp3w-g68c-fv3c) and which nothing the build runs ever calls: js-yaml 3 needs `argparse` only for its command-line script, and the override builds the same `_site` byte for byte. Drop the override once gray-matter moves to js-yaml 4. `npm audit` still names `braces` (through chokidar, every version to date), which has no fixed version either and is a build-time watcher, not anything the site serves.

## What is where

| File | Holds |
|---|---|
| `src/index.njk` | The page: the logo and pitch, the big picture, how it plays, the numbers, the live cannon, the gallery, what else is in it, the download steps for Windows and the Raspberry Pi 5 |
| `src/_data/site.json` | The links (the newest release, the repository), the description the search engines and link previews show, the counts the page prints (levels, chapters, scenes) and the video slot |
| `src/_data/gallery.json` | The gallery's pictures in order, each with its level, chapter, caption and the moment and camera `Tools/web-gallery.ps1` takes it with |
| `src/_data/built.js` | The day of the build (Prague time), which the footer prints as "Last updated" |
| `src/_includes/base.njk` | The HTML shell, the link-preview tags, the feed's autodiscovery link and the footer |
| `src/_includes/bar.njk`, `src/_includes/post.njk` | The slim header (logo, Blog, Download) and the layout of a post: date and topics, the title, the text, the newer and older post, the way back |
| `src/blog.njk`, `src/feed.njk` | The blog's list page (`/blog/`, newest first, each post with its thumbnail, date, topics and summary) and the Atom feed (`/feed.xml`, with each post's whole text) |
| `src/blog/*.md` | The posts, and `blog.11tydata.json`, which gives every file there the layout and the `posts` tag |
| `src/img/blog/` | The posts' own pictures, and `thumbs/` with one small picture for each post (below) |
| `src/video/blog/` | The posts' looping clips, in a folder for each post: each clip as VP9 WebM and as H.264 MP4, with its poster (below) |
| `eleventy.config.js` | The passthrough copies, the date filters, `absoluteLinks` (the feed's links and pictures get the site's address) and **no Liquid pass over Markdown** |
| `src/css/site.css` | The look: dark like the game's menus, headings in Anton (the game's own display face, served from `src/fonts`) |
| `src/js/motion.js` | The page in motion (below): the sections' pieces rising into view and the counts counting up; the hero's entrance is `site.css`'s alone |
| `src/js/compare.js` | The before/after figure of a post (below); `post.njk` loads it only on a page that has a `figure.compare` |
| `src/js/clips.js` | A post's looping clips (below): each plays while it is in view; `post.njk` loads it only on a page that has a `video.clip` |
| `src/js/sound.js` | The sound toggle (below); the music and the shot it plays are the game's own `Game/Music/menu.ogg` and `Game/Sfx/shoot.ogg`, copied by `eleventy.config.js` at the build |
| `src/js/cannon.js`, `src/cannon/` | The live cannon (below): the WebGL2 script, and the game's meshes and figures it draws (`cannon.bin`, `cannon.json`), written by `Tools/WindingCheck --export-cannon` |
| `publish.ps1` | Builds and uploads (below) |

- **The counts and the feature list are typed, not read from the game.** At a release, check them against what ships: `site.json`'s `levels`, `chapters` and `scenes`, and the claims in "What else is in it" and in the download steps (the latter are the release notes' own, in `.github/workflows/release.yml`).
- **The Pi's picture is one sentence with one source** (#947): `site.json`'s `piPicture` says what the Raspberry Pi build lacks (shadows, clouds, post effects) and keeps (each chapter's scenery behind the island, simplified). `index.njk` prints it (`{{ site.piPicture }}`) and `release.yml` reads the same key into the notes' Pi section (the `@@PI_PICTURE@@` placeholder), so the page and every release page say the same words. **At a release, check it against `docs/rendering/potato-path.md`** and edit it in `site.json` only; `PiPictureCopyTests` (run by CI) refuses a hand-typed copy, a lost placeholder, the phrase "no scenery" anywhere in `Web/src`, and a chapter set in a scene `PotatoBackdrop.For` draws no backdrop for. A page already published is corrected with `gh release edit <tag> --notes-file` (see "What the release notes carry" in `docs/build-and-release.md`), the site by a hand publish.
- **The video slot:** `site.json`'s `video` is null and the page says a video is on its way. Set it to the video's address and the page shows a button to it instead. It is a link, not an embed, so the page still makes no request to anyone else.
- **Nothing on the page talks to a third party**: no analytics, no cookies, no web fonts from elsewhere, so it needs no consent banner. Keep it that way; the footer says so.

## The live cannon (#949)

The owner: "a rotating cannon right in the browser, using graphics acceleration ... the detail of the turning wheels of a cannon that drives round in a circle; it could even fire a ball." The section "The cannon, live" draws the game's own cannon in a WebGL2 canvas, from the game's own meshes.
- **The meshes and figures are exported, never copied**: `dotnet run --project Tools\WindingCheck -- --export-cannon Web\src\cannon` builds a `CannonRig` on the tool's WARP device and writes the barrel, the glass, the muzzle collar, the carriage, a wheel and a roller (`cannon.bin`, about 300 KB: 16-bit positions, byte normals, 16-bit indices), and `cannon.json` with every figure that joins them, read off the game's constants: the wheel's radius, track and drop under the trunnions, the rollers' seats on a wheel, the recoil's stroke and decay, the magazine's spacing and front seat, each part's colour, sky reflection and alpha, and the thirteen ball colours. **Run it again whenever the cannon's meshes or figures change**, and commit both files.
- **The script is raw WebGL2, no library** (`src/js/cannon.js`), so the page still makes no third-party request. Matrices are the game's (row vectors, row order, uploaded as they are), so the parts compose exactly as `CannonRig` and `Cannon` compose them: the carriage yawed to its heading with its recoil shove, the wheels at their place on the axle rolled by the ground covered, the rollers on their seats spun by the ground crossed sideways, the barrel about the trunnions with its stroke, the collar in the next round's colour, the queue of five on the bore's axis behind the glass, a shot every 3.4 s.
- **It drives round a circle, easing between rolling on its wheels and crabbing across on its rollers** (what omni wheels are for), the barrel breathing up and down. The light is the page's own violet (a warm key, a cool fill, a hemisphere), tonemapped with ACES.
- **It has no box: the gun stands in the page itself.** The canvas is transparent (`alpha: true`, premultiplied, cleared to 0) and `.cannon-canvas` has no background and no radius. The ground is only a pool of light, 9.5 units in radius, that dissolves (alpha squared, from a quarter of its radius out) long before the canvas edge and is darker under the gun, and an elliptical `mask-image` (opaque inside 80 % of the way out, clear at the edges' middles and in the corners) feathers anything else, a shot in flight included, so nothing ends at a straight line and the page's violet shows round the gun. A look at the four edges of the canvas in a capture, against the page's `#0d0118`, finds no difference.
- **The metal parts are polished metal, not lit plastic** (`FINISH` in `cannon.js`: barrel, carriage, wheels, rollers and collar, each a metalness, a roughness and a reflectance that is the game's colour for the part times a factor). A metal has almost no diffuse; it shows `studio()`, a small procedural room evaluated from the reflected view vector (the page's violet dome with a bright band along its horizon, a dark floor, a big warm soft panel where the key is, a tall cool strip and a small one overhead, all blurred by the roughness), weighted by a Schlick Fresnel that climbs to a mirror at the grazing edges (less so the rougher), plus the key's own glint; the inside of the bore reflects almost nothing. The result is dark with crisp bright streaks, which is what keeps it from reading bright and matte. The balls, the glass and the ground keep the game's plain gloss.
- ⚠ **The game winds its meshes clockwise seen from outside**, a back face to WebGL's default: `gl.frontFace(gl.CW)`, so `gl_FrontFacing` is true outside and the normal is turned only for an inner face seen through (the bore).
- ⚠ **The glass is drawn at 0.45 of the game's alpha**: the game's glass is lit by reflections this program does not draw, and at 0.84 the pane hid the queue the game shows through it.
- **It runs only while seen**: paused off screen (`IntersectionObserver`) and in a hidden tab, at most two device pixels a CSS pixel; under `prefers-reduced-motion` it draws one frame and stops; without WebGL2, or if the meshes cannot be fetched, the whole section is hidden (a transparent canvas has no poster to fall back on).
- **A visible Pause** (#948) in the canvas's corner holds the gun where it is (`button.cannon-pause`, `aria-pressed`; "Play" goes on): anything that moves on its own carries a way to stop it. It is shown only when the gun moves at all, so not under reduced motion nor with `?cannon-t=`.
- **Looking at it without a display**: `?cannon-t=<seconds>` draws the pose at that moment and stops, since headless Edge's virtual time does not advance animation frames. Serve `_site` (`python -m http.server` in it) and photograph it: `msedge --headless=new --use-angle=swiftshader --enable-unsafe-swiftshader --window-size=1400,2900 --virtual-time-budget=3000 --screenshot=<png> "http://127.0.0.1:<port>/?cannon-t=2.6"` (2.6 s: driving, the first ball in the air; 7: crabbing; 9.3: just fired).

## The sound (#950)

The owner: "the website could also have the game's sounds and music - then there needs to be an always-visible mute button at the bottom of the site."
- **A toggle fixed to the bottom corner, off until pressed** (`button.sound-toggle`, `aria-pressed`, a 44 px target; shown only once the script runs, so a page without JavaScript has no dead button). **Nothing is fetched before the first press**: the game's menu loop and its shot, about 1.2 MB, which the button's title says.
- **The files are the game's**: `eleventy.config.js` copies `Game/Music/menu.ogg` and `Game/Sfx/shoot.ogg` into `audio/` at the build, so the site never keeps a copy that could fall behind them.
- **The loop plays through Web Audio** (`AudioBufferSourceNode.loop`, which wraps sample-exactly where `<audio loop>` often gaps), at the game's level for the menu (`MusicLevels.MENU_LEVEL` times `DECODE_HEADROOM`: the recordings overshoot full scale once decoded), faded in and out over the front end's `MENU_FADE_SECONDS` (0.5 s). Suspended in a hidden tab. Nothing is remembered between visits: no cookie, no storage, so the footer's "sets no cookies" holds.
- **The live cannon's shots are heard** (#949): `cannon.js` sends `bs3d:shot` with where the gun is across the canvas, and the toggle's script plays the game's shot, panned there and nudged in pitch.
- ⚠ **Decoded through an `OfflineAudioContext`**, which needs no output device: a playing `AudioContext` that could not reach one never finished decoding in headless Edge, and the button stayed on "Loading…". An `AudioBuffer` belongs to no context, so the playing one takes it as it is. A browser that cannot decode Ogg Vorbis (Safari's support is unchecked) gets "No sound in this browser" and a disabled button; so does a load that takes over 20 s.
- **Checked without a display** through the DevTools protocol (headless Edge with `--remote-debugging-port`, a Node script that opens the page, presses the button and reads the label): "Sound on" 0.3 s after the press, "Sound off" after the second; both files decode (the loop 59.2 s, 48 kHz stereo, peak 0.65; the shot 2.0 s). Nothing was heard.

## The motion (#948)

The owner: "it is a game, the site must be fun and attention-grabbing even for someone who cannot read English." Step 1 is the page moving by itself; the looping clips of play (steps 2-3) are filmed with the capture tool (#946, "Filming the game" below) and no page uses one yet.
- **The hero enters**: the logo pops in (scaled from 0.82 with a slight overshoot), the pitch, the buttons, the note and the big picture rise after it, 0.25-0.55 s apart. CSS only (`@keyframes logo-in`, `rise-in`).
- **The sections' pieces rise into view**: every section's heading and lead, the cards, the gallery's pictures, the feature list, the platforms, the posts, the cannon's stage and the three counts fade in from 28 px below as they are scrolled to (`IntersectionObserver`, the bottom 8 % of the window excluded), up to six siblings 90 ms apart. What is on screen when the script runs is shown at once, with no frame hidden. **The counts count up** from 0 to their figure in 0.9 s, eased out: only the bold of `.numbers-row p`, since the count keeps the digits alone (counting every bold in a revealed piece turned the feature list's "A Raspberry Pi 5 version" into "5").
- ⚠ **Nothing is hidden unless motion is welcome and the script has run.** An inline script in the head sets `html.motion` before the first paint, and never under `prefers-reduced-motion`; every hidden state is written under `.motion`, and `js/motion.js` itself marks the pieces it hides. It sets `window.bs3dMotion` once it has finished, and the head takes `motion` off again 3 s after the start if it has not, so a script that fails to load, or throws halfway, leaves the whole page visible. Without JavaScript the page is the static one.
- **Checked without a display** through the DevTools protocol (headless Edge with SwiftShader, a Node script, a 1280x900 window): all 36 pieces reveal on a continuous scroll and none is left below 0.5 opacity; the counts read 57 / 6 / 9 a moment into the count and 140 / 14 / 21 after; the cannon runs at about 28 animation frames a second there, 0 while paused, 28 again after "Play"; under emulated reduced motion no class, no hidden piece, no count and no Pause; with `motion.js` blocked the class is gone by 3.6 s and nothing was hidden. A piece jumped past (an anchor link) stays hidden until it is scrolled back to, and rises then.

## Filming the game (#946)

The owner's ruling (2026-10-25): the pictures and the looping clips are made **by the Game**, which has a capture mode (`capture=<folder>`: no HUD, a stated camera, a fixed step of game time a frame, frames written off the render thread; the arguments, the clock and what repeats are in [Capture: filming the game](game-session/controls-and-testing.md#capture-filming-the-game-946)). **`Tools/web-capture.ps1` is the route**: it builds the Game in Release into `%TEMP%\bs3d-web-capture`, runs one capture on a scratch profile in a 16:9 window (the largest of 1920x1080, 1600x900 and 1280x720 the display holds, started without taking the keyboard), and encodes the frames.

```powershell
# a clip of the benchmark's action: the gun fires by itself, groups fall, two cuts (8 s at 60 a second, the video scaled to 1280 wide)
.\Tools\web-capture.ps1 -Name meander-demo -Level Meander -Demo -From 6 -Seconds 8 -VideoWidth 1280
# a scripted clip from a stated camera: the barrel posed, shots at 3.5 and 4.5 s of game time
.\Tools\web-capture.ps1 -Name reel-fall -Level Reel -From 3 -Seconds 4 -Fps 30 -CamPos 24,-3,15 -CamTarget 0,-2,5 -Fov 45 -GameArgs 'aim=0:34:0 fire=3.5,4.5'
# a flown camera
.\Tools\web-capture.ps1 -Name orbit -Level Vortex -From 3 -Seconds 3 -CamPath .\orbit.json
# a still (-Seconds 0): the gallery's pair, <Name>-1600.jpg and <Name>-800.jpg
.\Tools\web-capture.ps1 -Name island-still -Level Meander -From 9 -Seconds 0 -CamPos 30,-6,40 -CamTarget 0,-5,0 -Fov 45
```

- **Output** goes to `Web\src\video` (`-OutDir`): `<Name>.webm` (VP9), `<Name>.mp4` (H.264) and `<Name>-poster.jpg` (frame `-PosterFrame`, the window's size). **No page uses them yet**: `eleventy.config.js` copies only `src/video/blog` (the posts' clips, below), so a page that embeds a capture also adds a passthrough for it, and `site.json`'s `video` is a link, not an embed. In a page: `<video autoplay muted loop playsinline poster="…"><source src="x.webm" type="video/webm"><source src="x.mp4" type="video/mp4"></video>`.
- **The encoder is `Tools/web-capture-encode.py`**, run with a Python that has PyAV (its bundled FFmpeg) and Pillow: no ffmpeg is installed or needed, and `-Python` names the interpreter (the default is the ComfyUI venv on the desktop; its build carries `libx264` and `libvpx-vp9`). The frames are decoded once and fed to both encoders: **yuv420p, converted and tagged as BT.709** (the default conversion is BT.601, which shifts every colour; decoded back, a clip's mean colours were within 1.3 of 255 of its frames'), no audio, a keyframe every two seconds, VP9 at `crf 31` (`deadline good`, `cpu-used 2`, row-mt) and x264 at `crf 21` (`preset slow`, `high`, `+faststart`).
- **Measured**: the Meander demo above, 480 frames of 1920x1080, scaled to 1280x720 at 60 a second, 8 s: **WebM 3.9 MB, MP4 3.4 MB, poster 368 KB**, and the script's whole run 69 s (the game 16 s, the encode the rest). **The picture's film grain and fine detail are what the bits go to**: the same engine at the native 1080p, a calmer 2 s, came to 4.3 MB WebM and 1.9 MB MP4, and scaled to 1280 wide 512 KB and 463 KB; scale a clip for a page (`-VideoWidth`) before judging its weight. The frames are 4.5 MB each, so 480 of them are 2 GB in `%TEMP%\bs3d-web-capture-frames\<Name>` (removed after the encode unless `-KeepFrames`).
- **A loop does not jump at its seam only if the content is chosen so**: the file loops as it is, so pick a window where the lens and the cluster end where they began (a closed camera path, a swing and its return), or let the seam be a cut. A fall has no such window: it ends with the cluster changed, so its seam is a cut.
- **Look at every clip before using it**, as with the gallery: a level's first-of-chapter tour, a result page that comes up in the window and the cavern's few differing pixels are in the picture or the numbers, not in the arguments.

### The camera path file

`-CamPath` (`campath=` for the Game) is JSON; comments and trailing commas are allowed. `t` is seconds into the clip (0 is its first frame), strictly rising; `position` and `target` are world coordinates, which the run prints for the play camera (`[capture] play lens: campos=… camtarget=…; gun at …; cluster centre …`) to start from; `fov` is the vertical field in degrees, on every key or on none (then the play lens's); before the first key and after the last the lens holds that key.

```json
{
  "format": "bs3d-camera-path",
  "interpolation": "spline",
  "keys": [
    { "t": 0, "position": [0, -3, 30],  "target": [0, -1, 0], "fov": 42 },
    { "t": 1, "position": [14, -2, 26], "target": [0, -1, 0], "fov": 40 },
    { "t": 2, "position": [24, -1, 15], "target": [0, -1, 0], "fov": 38 },
    { "t": 3, "position": [29, 0, 2],   "target": [0, -1, 0], "fov": 38 }
  ]
}
```

- **`"interpolation": "eased"`** (the default) eases each segment by its **first** key's `"ease"` (`smooth`, the default, `linear`, `in`, `out`), so the lens comes to rest at every key; **`"spline"`** is one cubic Hermite curve through the keys, with each key's rate taken from its neighbours (one-sided at the ends), so the lens never stops at one, and `ease` is ignored. A file that cannot be flown (no `target`, a `t` that does not rise, `fov` on some keys only, a lens on its own target) is refused before a window opens, with the reason in the log, exit code 2.
- The lens is held level and still: the gun's kick does not shake it (`-Shake`), and the roll of a cinematic is not applied. The curve's own shape is held by `CaptureCameraTests`.

## The blog (#928)

**Adding a post is one Markdown file in `src/blog/`.** Its front matter:

```yaml
---
title: The ball that holds up thirty-eight others
date: 2026-10-19T12:00:00Z       # shown as the day; set it to the day it is published. The list and the feed run newest first
topics: [Physics, Data structures]   # the small labels beside the date
summary: One or two sentences: the list, the link preview, the search result and the feed all show it.
thumb: /img/blog/thumbs/the-ball-that-holds-up-thirty-eight.jpg   # the small picture of the list and the landing page (below)
---
```

The file name is the address (`/blog/<file name>/`) and the first heading of the body is a `##`, because the title is the page's `<h1>`. Quote a `summary` that contains a colon. Markdown here is plain Markdown with raw HTML allowed (there is **no Liquid pass over it**, so a brace in a code sample stays a brace). A picture goes in `src/img/blog/` (or reuses `src/img/shots/`) as a `<figure>` with `width`, `height`, `loading="lazy"` and an `alt` (a screenshot with the `srcset` and `sizes` of the posts that already have one); a table scrolls inside itself on a phone and a code block scrolls inside its own box, so neither widens the page. The landing page lists the newest three posts under "From the workshop" and links `/blog/`; the header strip, the footer and the line under the gallery link it too, and all of that is absent while there are no posts.

- **The voice is "we", first person plural, and every figure is one the repository records** (the journal, the docs, the issue it came from). A post says what was measured and how, and says what is not known. Antonín is called Antonín, never "the owner". Check each figure against its source before it goes in; a post is not the place to round a number.
- **The feed carries each post's whole text** (`feed.njk` runs the rendered HTML through `absoluteLinks`, which gives root-relative `href`, `src` and `srcset` the site's address), so a reader of the feed never needs the site. A feed is a file like any other: it is uploaded by `publish.ps1` and Cloudflare caches it.
- **The posts' pictures** are the game as it is played, taken from the repository's own captures (`Research/pi-port/`, a verdict's before/after frames), cropped and scaled to 800 or 1600 pixels; the grey ramp in "Ten bits" is drawn by a script and says so. Retake nothing for a post. A post about the score server's front panel ("Seventeen greys", #1042) takes its pictures and clips from [BS3D-LCD](https://github.com/AntoninPrazsky/BS3D-LCD)'s own `docs/photos` and `docs/screenshots`, in the same way. A post may also show a **reference** a local image model drew for a scene ("Five scenes that started as AI pictures", #942): one 960-pixel JPEG with no `srcset`, so it is shown at its own size and stays smaller than the game's captures beside it, and its caption names the model, quotes the words it was given (the `.txt` the image tool writes beside every picture) and says it is not in the game.
- **A before/after figure** (#942) switches between two pictures in the same place. The markup is a `<figure class="compare">` holding a `.compare-frame` with two `.compare-pane`, each an `<img>` (a screenshot's `srcset` and `sizes`, the same size for both) and a `<span class="compare-tag">Before</span>` (`data-label` on the pane names the button; "After" for the second), then a `<figcaption>`; the first pane is the before. **Without JavaScript both pictures show, the before one above the after one, each with its label in the corner** (a feed reader gets the same). `js/compare.js` marks the figure `.is-live`, lays the panes on one another in one grid cell, adds a **Before / After pair of buttons** (`aria-pressed`, 44 px tall, the focus ring of the other controls) and makes a click on the picture switch to the other. One pane is hidden by `visibility` and `opacity`, never `display`, so a lazy picture still loads, and the figure never changes height; the cross-fade is dropped under `prefers-reduced-motion`. The figure is as wide as any picture (`--figure`) with its caption in the text's column, and its corners are `--radius`. `post.njk` adds the script only when the post's HTML holds `<figure class="compare"`, so a post with none loads nothing new. No blank line inside the figure: a blank line ends a Markdown HTML block. Checked in headless Edge through the DevTools protocol at 1920, 3840 (a native window, not a metrics override: the override does not trigger the 2200 and 3000 px root sizes) and 400 px: real mouse clicks on a button and on the picture, Tab then Space on the buttons, no script error, nothing scrolling sideways, and with scripts disabled both pictures visible in order with no dead buttons.
- **A grid figure** (#1042) puts two or three pictures or clips side by side. The markup:
  - a `<figure class="grid grid-2">` (or `grid-3`);
  - inside it, a `<div class="cell">` for each picture or clip, with a `<span class="cell-label">` under it;
  - then the `<figcaption>`, which spans the row.

  Each picture fills its cell, and below 640 px the grid is one column. Give a row's pictures one size, or their labels stand at different heights; "Seventeen greys" pads the panel's frames to the size of the simulation beside them. A picture with a fine pattern can beat with its pixel grid when the browser shrinks it four times, to a third of the figure. Such a picture gets a filtered 710 px copy and a `srcset`, with `sizes="(max-width: 640px) calc(100vw - 32px), (max-width: 1088px) calc((100vw - 56px) / 3), 344px"`.
- **A looping clip** (#1042) lives in the post's own folder under `src/video/blog/`. Its markup is a `<video class="clip">` with:
  - `width`, `height`, `muted`, `loop`, `playsinline`, `controls`, `preload="none"` and a `poster`;
  - a WebM `<source>` before an MP4 one;
  - no `autoplay`.

  `js/clips.js` plays a clip while half of it is on the screen in a visible tab, and pauses it when it leaves. A clip the reader paused stays paused until they press Play. Without the script, and under `prefers-reduced-motion`, nothing moves and only the posters load. The controls are the way to stop it that anything moving on the site must have (#948). `post.njk` loads the script only when the post holds `<video class="clip"`.

  A clip is shown at its own size, like a picture drawn for the post, centred and never enlarged. The feed gets the `<video>` with the site's address on its sources and on its poster, because `absoluteLinks` rewrites `poster` too.

  "Seventeen greys" made its four clips from BS3D-LCD's GIFs with the system's ffmpeg:
  - a constant frame rate that the GIF's delays divide (25 or 20 a second), because the GIFs' frames last 40 to 680 ms;
  - BT.709, yuv420p, x264 at `crf 21`, `preset slow` with `+faststart`, and VP9 at `crf 31`, with a keyframe every two seconds;
  - the GIF's own 710 px, because doubling it with nearest neighbour first looked no different and cost twice the bytes.

  The four GIFs' 3.6 MB became 0.82 MB of WebM.
- **A chart drawn for a post** (#1025) is a PNG a script draws in the site's colours, and its caption says so ("Two and a half hours of play" draws its two from a copy of the score service's database). It has a wide layout at 800 and 1600 pixels with the `srcset` and `sizes` of a screenshot, so it fills the figure, **and a phone layout**: the same chart drawn 400 pixels wide (and 800 for two device pixels) with the same type size, its legend stacked and its end labels folded into the legend. Scaled to a phone's width, the wide chart's 15 px labels shrink to about 7 px. The markup is a `<picture>` around the wide `<img>`, with a `<source media="(max-width: 600px)">` that carries the phone layout's `srcset`, `sizes="calc(100vw - 32px)"`, `width` and `height`. The feed, and anything that ignores `<picture>`, gets the wide one. Checked in headless Chromium: the phone layout at 320 and 400 px, the wide one at 700 and 1920 px, and nothing scrolling sideways at any of them. "Zero balls at sixty frames a second" (#1043) draws its chart as an SVG in Lato and photographs it in headless Chromium at 1x and 2x, since the Pi has no plotting library.
- **Every post has a thumbnail**, so the list invites a click: the `thumb` field is a root path, `/img/blog/thumbs/<the post's file name>.jpg`, to a **480x270 JPEG at quality 82 (4 to 31 KB), cut to 16:9 from a picture already in the post** (its screenshot, one frame of a before/after sheet, a 313-pixel piece of the ramp strip). Make a new one with Pillow: `Image.open(src).crop(box).resize((480, 270), Image.LANCZOS).save(out, "JPEG", quality=82, optimize=True, progressive=True)`. `blog.njk` shows it on the card, beside the text from 640 px wide and above it below that; `index.njk` shows it beside each of the three posts under "From the workshop" from 560 px and above them below that. It is decorative (`alt=""`, `width`/`height`, `loading="lazy"`) inside a second link to the post with `tabindex="-1"` and `aria-hidden="true"`, so the title stays the one link a keyboard or a screen reader meets; it zooms 4 % on hover, not under reduced motion. A post with no `thumb` is listed without a picture and the card falls back to one column. The post's own page, the feed and the link preview do not use it.
- **The columns** are three tokens at the top of `site.css`, in rem so they grow with the root size, which steps up at 2200 and 3000 px (a 3840 px screen reads at 24 px to the rem). A post's text keeps to `--measure` (48rem: 768 px at 1920, 1152 px at 3840, about 72 to 85 characters a line) and a picture breaks out to `--figure` (66rem: 1056 px at 1920, 1584 px at 3840) with its caption left in the text's column; the post's type grows with the viewport (`clamp(1.0625rem, 1rem + 0.2vw, 1.2rem)`: 19.2 px at 1920, 28.8 px at 3840). The list page's column is `--list` (56rem). The container is the wide one and every direct child of `.post` that is not a `<figure>` is held to the measure and centred, so a table or a code block can never widen the page. A screenshot (it has a `srcset`) fills the figure, so its `sizes` says `(max-width: 1088px) calc(100vw - 32px), 1056px`; a picture drawn for the post (no `srcset`) is shown at its own size, centred and never enlarged. The 16 px side gutter stays at any width, and nothing scrolls sideways at 400 or 320 px.
- **To look at it**: `npx eleventy` and serve `_site` with any static server (`python -m http.server --directory _site`): the pages use absolute paths, so opening the files directly shows no styles. Phone width is a rule of the site: no horizontal scroll at 400 pixels wide.
- The journal (`docs/agent-notes.md`) is raw material for posts, never published as it stands.

## The pictures

**Every picture is the game itself, and they are retaken for every release**, next to the release's own screenshot (`Tools/release-screenshot.ps1`, `Images/releases/`). There are two kinds, from two scripts:

```powershell
.\Tools\web-screenshots.ps1          # the big picture: Meander as it is played, HUD and all
.\Tools\web-gallery.ps1              # the gallery's eight: a moment in motion from a stated camera, no HUD (#922)
.\Tools\web-gallery.ps1 -Levels Reel,Vortex -NoBuild
```

**The big picture** (`web-screenshots.ps1`) builds the Game in Release into a folder of its own and plays the level on a scratch profile whose settings turn the tutorial cards, the intro logo and the FPS counter off and hold High (so no first-time card covers the picture, nothing touches your save and nothing reaches the online boards). The game runs in a **16:9 window** (the largest of 3840x2160, 2560x1440, 1920x1080 and 1600x900 the display's working area holds) and the frame is written to `Web/src/img/shots/<level>-1600.jpg` and `-800.jpg`. ⚠ Not fullscreen cut to 16:9: the HUD is laid out to the window's edges, so on the 3840x1600 desktop the crop lost the score, the balls left and the magazine and halved the power-up icons. A level brings its own scene; the magazine's deal is pinned by `-Seed`. Look at every picture before committing it: a level that opens on a camera cinematic photographs the cinematic (Trefoil does).

**The gallery** (`web-gallery.ps1`) is the owner's ask of #922: not eight pictures from behind the gun at score 0, but the game moving, from a camera set for the picture. Each is one still of the capture mode (`web-capture.ps1 -Seconds 0`, "Filming the game" above) during the benchmark's action (`-Demo`): a group falling, a shot in flight, a cut striking, at the **Ultra** tier in a 2560x1440 window where the display holds one. What to take is `gallery.json`'s `shot` for each entry: `from`, the game time of the frame, and `campos`, `camtarget`, `fov`, the camera. The capture's world repeats exactly, so the same entry on the same build gives the same file byte for byte (measured: all sixteen JPEGs of two runs identical); the window size and the tier change the picture's sharpness, not the world. **A change to the physics, the demo's aim or a level moves what happens at a given second**, so look at every picture: a moment that showed a group falling may show an empty sky on the next build. To find a new moment, film the level's demo from the entry's camera (`web-capture.ps1 -Demo -Fps 15 -From 8 -Seconds 16 -NoEncode -KeepFrames`) and take the frame's time: frame *n* of a window from *F* is game time ⌈15 *F*⌉ / 15 + (*n* − 1) / 15. The rate stays 15: the demo's shots are checked once a frame, so the same second at another rate can be another world. Vortex's demo drops nothing worth a picture, so its still is the scene, the planet and the nebula over the cluster, with no moment in it.

The logo is `Images/logo/bs3d-logo-2048.png` scaled to 1000 and 600 px as WebP; the link-preview picture (`img/og-meander.jpg`, 1200×630) is Meander's frame cut to that shape.

**Weight** (measured on the first build): 28 files, 3.7 MB. The first screen on a desktop is about 600 KB (the page, its stylesheet, the Anton font at 171 KB, the logo at 182 KB and the big picture at 226 KB); the gallery's eight 800 px pictures, 690 KB together, load as they scroll into view (since #922's retake, 0.52 MB). With the first five posts a build is 39 files and 4.1 MB: a post's page is about 8 KB, the feed about 30 KB, the posts' own pictures (`img/blog`) 0.4 MB and their five thumbnails (`img/blog/thumbs`) 0.1 MB, all loaded lazily, and nothing on the landing page's first screen changed. The sixth post's pictures (`img/blog/five-scenes-that-started-as-ai-pictures`, 33 files, 3.8 MB: seven before/after pairs at 800 and 1600 px and five references) load about 3.0 MB on a desktop and 1.3 MB on a phone, lazily, and its page is 24 KB. The seventh post's eight charts (`img/blog/two-and-a-half-hours-of-play`, 0.53 MB) load 0.22 MB on a desktop and 0.16 MB on a phone, lazily. The eighth post's three captures (`img/blog/light-cycles-from-a-hash-and-a-strip`, 6 files, 0.91 MB at 800 and 1600 px, taken from the Testbed with `scene=grid sceneseed=0` at `shot=10` and the text overlay hidden by `at=1:F12`) are 0.69 MB at the 1600 px size a desktop picks and 0.23 MB at the 800 px size a phone picks (the files' sizes, not a browser's measurement), and its page is 14 KB. The ninth post's nine pictures (`img/blog/logos-of-studios-that-never-existed`, 0.74 MB: 960-pixel JPEGs a model drew, two frames of the game they were painted over, a collage of six corners and one strip, none with a `srcset`) all load lazily, and its page is 18 KB. The tenth post's pictures (`img/blog/seventeen-greys-on-a-screen-that-has-two`, 19 files, 0.85 MB: five phone photos at 1000 px, 0.60 MB, and BS3D-LCD's simulated screenshots as PNG, 0.26 MB) all load lazily. Its four clips (`video/blog/seventeen-greys-on-a-screen-that-has-two`) are 0.82 MB as WebM and 1.13 MB as MP4, and until a clip plays only their posters load (79 KB together); under reduced motion no clip loads at all. Its page is 24 KB. The eleventh post's one chart (`img/blog/zero-balls-at-sixty-frames-a-second`, four PNGs, 0.28 MB) loads 0.12 MB on a desktop (the 1600 px picture fills the 1056 px figure) and 0.03 to 0.07 MB on a phone, lazily.

## Publishing a change

```powershell
$env:BS3D_WEB_HOST = '<the server, by the name its FTP certificate covers>'
$env:BS3D_WEB_USER = '<the web account>'
$env:BS3D_WEB_ROOT = '/'
$env:BS3D_WEB_PASSWORD = [Net.NetworkCredential]::new('', (Read-Host 'Password' -AsSecureString)).Password
.\Web\publish.ps1 -Protocol ftps          # -DryRun builds and lists the files
Remove-Item Env:BS3D_WEB_PASSWORD
```

- **The credentials live in the environment, never in a file**: the repository is public. A GitHub Actions workflow that publishes on a release would keep them in repository secrets.
- **The web account is an FTP sub-account rooted at the subdomain's own folder** (Webglobe's Subdomény point `bs3d` at `_sub/bs3dweb`), so `BS3D_WEB_ROOT` is `/` and the account can reach nothing else. Webglobe's SFTP (port 222) serves only the main account, which reaches the whole hosting, the backups included, and `sftp -b` never asks for a password anyway: FTPS is the route.
- ⚠ **FTPS goes to the server's own name, not `ftp.<domain>`**: the domain's records are at Cloudflare, which proxies that name, and port 21 never answers through it. Webglobe's "Základní nastavení" gives the server's IP; by the IP alone the certificate check fails, and the certificate the server presents (`openssl s_client -starttls ftp -connect <ip>:21`) names the host to use. A login refused (`530`) is a wrong account or password; a new account can take a few minutes to work.
- **One curl run carries every file** over one connection and one login, retries a dropped transfer three times, goes on past one that still fails and names every failure at the end. ⚠ The server dropped the connection on one particular picture every time (`clouds-broken-before-800.jpg` as first written, 65,447 bytes; nothing in it looks like a script and Webglobe's malware scanner lists nothing), by a login per file and in one run alike, while its siblings went up; it was re-saved with its own quantization tables (no visible difference). A file that keeps failing alone: re-save it the same way.
- **⚠ Upload with the web account, never the off-site backup's.** The Pi's database backup (BS3D-API #6) goes to its own SFTP account on the same hosting, chrooted outside the web root, so nothing a deploy writes can reach the backups and nothing in them is served. The account names stay out of this public repository.
- The script only uploads: a file the site no longer has stays on the server until it is deleted by hand.
- **The footer's "Last updated" is the day of the build, not of the last commit.** `publish.ps1` builds a clean `_site` and uploads it at once, so the date is the last publish's; a build that is not uploaded (`npx eleventy`, `-DryRun`) stamps its own day only into the files it writes locally.
- **Cloudflare caches the page.** After a publish, look with a hard reload; purge the cache in the Cloudflare dashboard if the old files persist.

## The one-time setup (the owner's accounts)

- **Webglobe:** the subdomain `bs3d` with its own document root, and HTTPS at the origin (Webglobe's certificate for the subdomain, which Cloudflare's *Full (strict)* mode needs).
- **Cloudflare** (the domain's nameservers since 2026-10-01): a record for `bs3d` pointing at the Webglobe server, and a redirect rule sending `winphonew.eu` and `www.winphonew.eu` to `https://bs3d.winphonew.eu/`. The record for `scores` must keep pointing at the tunnel to the Pi: check `https://scores.winphonew.eu/` still answers after any change.
- **Check from outside, on more than one resolver**: one ISP's resolver followed the domain's old nameservers for a while after the move (#691).
