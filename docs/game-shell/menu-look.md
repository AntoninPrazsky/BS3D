# The menu's look: greyscale, brushes, type and the pointer

Part of [The game: shell](../game-shell.md).

The menu sits over twenty-one backdrops whose palettes are nothing alike, and every rule below follows from that. Read this before changing a colour, a brush, a font size or a fill in `Game/Screens/`.

## The look is greyscale, and that is a constraint, not a taste

Any accent that reads as the game's own over one backdrop (neon city, ochre desert, blue sea, white peaks, green meadow) fights the next. Emphasis is carried by **brightness and opacity**:

- mid-grey slabs for entries (bar the front end's, below);
- a step up the grey ramp for hover;
- white for the active item, grey for the inactive;
- **no drawn frames**: a border is one more shape competing with every backdrop, and the tone step at a slab's edge already reads as a control. Myra's stylesheet supplies a framed grey tool button, so `Border` must be explicitly cleared.

Each state brush **replaces** the one before it; a state cannot be a wash laid over the previous one.

### The deliberate colours, and what admits one

A new accent has to argue for itself on the terms the existing ones did, or it gets waved through on their example. Each is a *readout* or the game's own name, never chrome:

- **The star rating (#139)**: the hue **is** the information. All four tiers have to read as "good", and a ramp that dims towards the best one says the opposite. It is read against a controlled grey (the result page softens the whole frame, #178; the picker's stars sit on a tile's slab over the page's plate). `BS3DGame.StarTierColor` is the one writer; `STAR_EMPTY` is the grey the unearned remainder recedes to.
- **The 3D wordmark's rainbow (#248)**: [The 3D title](title-wordmark.md#a-rainbow-is-the-second-deliberate-exception) argues it.
- **"This is you" on the online boards is magenta (#739)**, `BS3DGame.BoardYouColor`, the one writer: the player's row, the "You #n of N" line under each board, the High Scores page's own column, and the online signal once the service has their score. On a board's plate gold then means a cup and nothing else (a first-place row in the stars' gold read as a trophy's name). It is drawn only on the boards' own plates, and is the one hue the menu does not already mean something by: not a cup's metal, not the diamond tier's cyan, not the alert red. Violet read a step darker than the grey rows and blue sat next to the silver cup, so both were rejected on the real boards.
- **Alert red (#687)**, `BS3DGame.MENU_TEXT_ALERT`: what is left to do on the ONLINE page ([The ONLINE page and the nickname](online-and-nickname.md#what-is-left-to-do-is-red)).

## Brushes are premultiplied

Myra paints through `SpriteBatch`'s default `AlphaBlend`, so a brush composites as `rgb + dst * (1 - a/255)`: the RGB is the light the slab **adds** and the alpha is only how much of the scene it **blocks**. `MENU_BUTTON` `(55, 55, 55, 191)` is grey **73** at 75 %, not grey 55 at 75 %, and a lighter tone typed in unmultiplied comes out milky over a bright sky instead of lighter. Every figure in this document means the premultiplied reading.

## Controls, plates and the tone order

- **A control is a step, not a dial.** It has to hold near-white type over a *bright* sky (so it must block light), read as a surface over a *dark* one, **and** read as a control on a **plate**. A plate is itself a mid-dark panel, so a slab near its tone is invisible on it (tried at grey 51: the front end looked right and every settings value vanished into its card). A control is either well below `MENU_PLATE` or well above it, never modestly different. `MENU_BUTTON` is grey 73 at 75 %, hover grey 116 at 90 %, pressed grey 140 at 94 %, each about 20 of tone clear of the plate and of its neighbour.
- A quarter of the backdrop coming through is as far as the transparency can go: the label is near-white, and white type over a blown sky still measures about 4:1.
- **The plate is the recessed surface and the controls are raised on it** (#216; the order was the other way round under #179). "The card is darker than the things on it" is the reading every player already has. What the ramp must still guarantee is a clear step in *some* direction: the sign is free, the step is not. **`MENU_PLATE`'s tone is #179's and did not move**; its opacity is load-bearing, because the result screen carries no scrim and the small print on a plate is the one type here that is not near-white Anton, so at a lower alpha a quarter of a bright blurred sky came through the numbers.
- **The result screen's score panel is the one plate that stands beside buttons** (#179). Auto-sized around a few short numbers it came out narrower than every control beneath it and read as a fourth button that could not be pressed. It is pinned to `BS3DGame.MenuColumnWidth` (`Scaled(MENU_BUTTON_WIDTH)`, one copy of the figure) as a `MinWidth`, not a `Width`, so the unlock note under the total may be longer than the column. Myra applies both to the measured size with the padding already in, so at the minimum the plate's edges land on the buttons'. `MENU_BUTTON_WIDTH` must not quietly become "the front end's width".
- ⚠ **The level picker's tiles are the case to check when this ramp moves.** A tile is a readout as much as a control: `STAR_EMPTY` and the bronze tier are read against the tile's face, and both lose contrast as it lightens. At grey 73 the earned run and the hollow remainder still separate; a *hovered* tile (grey 116) loses it almost entirely, which is transient (the detail line restates the level). A lighter face would need the star greys re-derived with it.

## The front end rests its entries almost to nothing

The one place "mid-grey slabs for entries" does not hold. The front end has neither a plate nor small print: its entries are Anton at `MENU_FONT_FRONT_ENTRY`, which holds itself over any backdrop. So `MainMenuPage.EntryRestBrush` rests them at `MENU_FRONT_BUTTON` (black at alpha 18, about 7 %, pure transparency, so it only darkens and never milks a bright sky): a whisper kept so the hover has something to lift from.

- Hover and pressed stay the shared tones, so the pointer lifts the same slab every other page shows.
- It is asked of the **page** (`MenuPage.EntryRestBrush`), because `ApplyNavHighlight` repaints every entry each pass and would otherwise paint the shared grey back over it.
- ⚠ Issue #247's body cites grey 73 at 75 % and `Menu.cs:185`, which is `MENU_BUTTON`, the brush every *other* page uses. Do not follow it: editing that would change Settings, both pickers and the pause, which #216 rules out. The front end has its own rest brush.
- At 7 % the **edge** reads, not the tone: over the moon's regolith or the island's stone the rectangle is a faint step, over grass nearly gone. That is the ask ("a hint that a control is there"), and the place the next complaint would come from. Myra 1.6.3 has no gradient brush; a soft-edged slab would need a generated texture.

## Flat fills use `FlatBrush` (#712)

⚠ **Never Myra's `SolidBrush` or `RenderContext.FillRectangle` for a flat fill.** Both stretch `Stylesheet.Current.WhiteRegion`, one texel of Myra's default skin atlas whose right-hand neighbour is blue (27, 161, 226). The last columns of a stretched quad sample into it, so every plate, button and tag carried a dark teal stripe down its right edge (2 px on a plate, 1 px on a button): it is that blue times the slab's own tone, which is why it read as a different hue and clearing `Border` changed nothing.

`FlatBrush` draws from a 1×1 texture of its own and copies `SolidBrush.Draw`'s colour arithmetic, so the slab ends where its layout bounds do. **A new widget that fills a rectangle** uses `new FlatBrush(color)` as a `Background`, or `FlatBrush.Fill(context, rect, color)` inside an `InternalRender`.

## Sizes are authored for 2160p and resolved at build time

Myra measures in pixels, so without scaling the menu would be a postage stamp at 4K. Sizes are authored for `MENU_DESIGN_HEIGHT` and resolved by `Scaled()`, the way `InfoRenderer`'s overlay is; `Scaled` never returns less than 1.

- The tree is **rebuilt** at the new sizes, **not** scaled by `Desktop.Scale`: scaling the desktop also scales the full-screen scrim, which then stops covering the frame (tried; it left the scrim a rectangle in one corner).
- `EnsureMenuLayout` runs from `Draw`, not off the resize event, so the one place the layout can be stale is the frame about to draw it. The rebuild is quantized (`MENU_REBUILD_QUANTUM`) so a live window drag asks the font system for a couple of dozen sizes rather than hundreds: a size is its own glyph **atlas**, rasterized out of the display face on every rebuild.
- Every size on a page is a design figure scaled by the viewport's height, so a page that overruns the design height overruns it at *every* resolution and aspect. It is never a small-window bug, and rows that run off the bottom take Back, the page's only way out, with them.

## Typefaces

**Two faces split by role, not by weight: Anton for everything loud, Inter for the small print.** Anton sets the title, the menu entries, a result's headings and the whole in-play HUD; Inter sets `MENU_FONT_SMALL` (About's paragraphs, a level's rules on the picker, the adaptive-quality note), because a condensed display face closes up at reading size. Both are SIL OFL 1.1, each shipping with its licence, as `EmbeddedResource`s (no path to get wrong, nothing to install, nothing through the MGCB pipeline).

- **Anton is the shippable form of the Impact look, a licence decision before a taste one.** Impact is bundled with Windows and not redistributable, the rule that keeps Segoe UI (the `InfoRenderer` spritefont's source) out of this assembly; `Game.csproj` states it where the fonts are declared. Loading Impact off `C:\Windows\Fonts` would trade "nothing to install" for a system dependency and a fallback path.
- **Four `FontSystem`s, not four fonts in one.** FontStashSharp falls back through a system's fonts glyph by glyph, so a second face added beside the first is never reached. Picking a face means picking a system. The fourth is **PromptFont** (`_menuFontSystemPrompt`, SIL OFL 1.1): the tutorial's keycaps, mouse buttons and pad triggers and the menus' key prompts ([Menu navigation](menu-navigation.md#the-keys-are-drawn-in-the-ui-799)). It puts a keycap A on a codepoint of its own so that Anton's A is never what a prompt for the A key draws ("The tutorial" in `docs/game-feedback.md`). `_menuFontSystemBold` (Inter Bold) is still loaded for small print that needs a heavier cut.
- ⚠ **Anton has one weight**, so a "bold" slot resolves to the same glyphs. Emphasis has to come from size, colour or motion: the HUD's low-balls alarm is a size step (`HUD_LOW_EMPHASIS`).
- **Sizes:** `MENU_FONT_BODY` is every button in the game and **the Settings page bounds it** (thirteen rows once ran off the bottom of the window and took Back with them, #138). `MENU_FONT_TITLE` is shared with the result screen's headings. So the front end, which has no height budget, is loud by itself: `MENU_FONT_FRONT_ENTRY`, through `FrontEndEntry`, which is `MenuButton`'s own `MenuClickable` core in the bigger type with its label against the slab's left edge, so the shared brushes, the click sound and the pad's `Tag` cannot drift from every other page's entries. The slab keeps `MENU_BUTTON_WIDTH`.
- **Headings are `MENU_FONT_SECTION`** (the display face, #243): as the small face over larger rows, each heading was smaller than the rows it headed.
- **No 240-unit title font.** `MENU_FONT_GAME_TITLE` is gone with the Myra label the 3D wordmark replaced; `GAME_TITLE` is no longer set as a heading anywhere (it survives as About's prose and the window's title bar).

## The pointer is the game's own (#350)

`Game/Platform/PointerCursor.cs` builds a stylized arrow at load as a `Texture2D` from a signed distance field (outline, drop shadow and antialiasing all read off the one distance) and publishes it through `Mouse.SetCursor`. It is the one piece of chrome **Windows** draws.

- No bitmap asset, nothing for the content pipeline to carry into the release zip.
- **Its size comes from the display's own** (`HEIGHT_OF_DISPLAY` of the panel's height, clamped between `MIN_HEIGHT` and `MAX_HEIGHT`): a 32 px arrow authored for 1080p is a speck on 4K, this project's base. The height is in physical pixels (the game is DPI-aware, #825), so the Windows scale does not change it, as it does not change the menus: a 64×64 bitmap with its hot spot at (4, 4) at 3840×1600 at 100 % and at 125 %.
- It is greyscale for the chrome's reason, and because a grey bitmap is byte-identical if the RGBA-to-BGRA hand-off into GDI is ever the other way round; a coloured one would be the bug that only appears on somebody else's machine.
- ⚠ **`Mouse.SetCursor` is called exactly once, from `Initialize`.** It hands the window a GDI cursor handle, so a per-frame call builds and abandons one per frame, the known road to an intermittent MonoGame crash. What toggles per frame is `IsMouseVisible`, which only ever **hides**; hiding does not un-choose the cursor, so the play loop that hides it and draws the procedural `Crosshair` needs nothing here.
- **Neither capture route can photograph a cursor**: `shot=` saves the back buffer, and `screenshot.ps1`'s `CopyFromScreen` is a BitBlt, which does not draw the pointer. The check is `GetCursorInfo` → the live handle → `DrawIconEx` onto a plate of your own, which answers identity (ours, or `IDC_ARROW`), the hot spot and the bitmap at its real size in one call. Measured that way, the handle survives a window resize, the pointer leaving and returning, and a fullscreen toggle (the Game's only in-session `ApplyChanges`); in `GameplayScreen` it reports no cursor, and Escape brings the same handle back.
