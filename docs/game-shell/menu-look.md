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
- **NEW BEST's pastel rainbow (#926)**: a *celebration*, the rarest line the game says (a clear that beat a best, once a page), meant to be looked at and not used; it moves and is only ever the one word. Pastel by construction (`NewBestCelebration.LIFT`: no channel under 0.52, the lightness constant as the hue goes round) so it reads as a prize and not as a status, it is not a hue the menu means anything by, and it carries a dark outline at its own size so a pale fill is never alone on a bright sky.
- **"This is you" on the online boards is magenta (#739)**, `BS3DGame.BoardYouColor`, the one writer: the player's row, the "You #n of N" line under each board, the High Scores page's own column, and the online signal once the service has their score. On a board's plate gold then means a cup and nothing else (a first-place row in the stars' gold read as a trophy's name). It is drawn only on the boards' own plates, and is the one hue the menu does not already mean something by: not a cup's metal, not the diamond tier's cyan, not the alert red. Violet read a step darker than the grey rows and blue sat next to the silver cup, so both were rejected on the real boards.
- **Alert red (#687)**, `BS3DGame.MENU_TEXT_ALERT`: what is left to do on the ONLINE page ([The ONLINE page and the nickname](online-and-nickname.md#what-is-left-to-do-is-red)).

## Brushes are premultiplied

Myra paints through `SpriteBatch`'s default `AlphaBlend`, so a brush composites as `rgb + dst * (1 - a/255)`: the RGB is the light the slab **adds** and the alpha is only how much of the scene it **blocks**. `MENU_BUTTON` `(55, 55, 55, 191)` is grey **73** at 75 %, not grey 55 at 75 %, and a lighter tone typed in unmultiplied comes out milky over a bright sky instead of lighter. Every figure in this document means the premultiplied reading.

## Controls, plates and the tone order

- **A control is a step, not a dial.** It has to hold near-white type over a *bright* sky (so it must block light), read as a surface over a *dark* one, **and** read as a control on a **plate**. A plate is itself a mid-dark panel, so a slab near its tone is invisible on it (tried at grey 51: the front end looked right and every settings value vanished into its card). A control is either well below `MENU_PLATE` or well above it, never modestly different. `MENU_BUTTON` is grey 73 at 75 %, hover grey 116 at 90 %, pressed grey 140 at 94 %, each about 20 of tone clear of the plate and of its neighbour.
- A quarter of the backdrop coming through is as far as the transparency can go: the label is near-white, and white type over a blown sky still measures about 4:1.
- **The plate is the recessed surface and the controls are raised on it** (#216; the order was the other way round under #179). "The card is darker than the things on it" is the reading every player already has. What the ramp must still guarantee is a clear step in *some* direction: the sign is free, the step is not. **`MENU_PLATE`'s tone is #179's and did not move**; its opacity is load-bearing, because the result screen carries no scrim and the small print on a plate is the one type here that is not near-white Anton, so at a lower alpha a quarter of a bright blurred sky came through the numbers.
- **The result screen's score panel is the one plate that stands beside buttons** (#179). Auto-sized around a few short numbers it came out narrower than every control beneath it and read as a fourth button that could not be pressed. It is pinned to `BS3DGame.MenuColumnWidth` (`Scaled(MENU_BUTTON_WIDTH)`, one copy of the figure) as a `MinWidth`, not a `Width`, so the unlock note under the total may be longer than the column. Myra applies both to the measured size with the padding already in, so at the minimum the plate's edges land on the buttons'. `MENU_BUTTON_WIDTH` must not quietly become "the front end's width".
- **A tint that must carry on every backdrop is a recess, not a lighter slab** (High Scores' rows of levels not cleared, #898, and the online boards' rows of attempts that did not finish, #977, which borrow `MENU_ROW_UNFINISHED_BRUSH`). `MENU_BUTTON`'s step over the plate shrinks as the backdrop brightens (measured beside the plate: about +22 over the moon and space, +11 over the sea and mountain, +3 over the desert, polar and storm), so a tone that is only a step *above* the plate, or a half-step between plate and slab, is lost over a bright scene: the first High Scores tint, (34, 34, 34, 128), measured +5 over the plate and 13 under a cleared row on the meadow, and by the model (which the new captures agree with) falls to the plate's own tone and to within 2 of a cleared row behind a bright scene. Pure transparency (`MENU_ROW_UNFINISHED`, black at alpha 100) takes a fraction of the plate away, so its step does not shrink: 18 to 40 under the plate and 28 to 42 under `MENU_BUTTON`'s slab over those eight scenes. Judge a new tint over the desert, polar and storm, never over the meadow alone.
- ⚠ **The level picker's tiles are the case to check when this ramp moves.** A tile is a readout as much as a control: `STAR_EMPTY` and the bronze tier are read against the tile's face, and both lose contrast as it lightens. At grey 73 the earned run and the hollow remainder still separate; a *hovered* tile (grey 116) loses it almost entirely, which is transient (the detail line restates the level). A lighter face would need the star greys re-derived with it.

## The front end rests its entries almost to nothing

The one place "mid-grey slabs for entries" does not hold. The front end has neither a plate nor small print: its entries are Anton at `MENU_FONT_FRONT_ENTRY`, which holds itself over any backdrop. So `MainMenuPage.EntryRestBrush` rests them at `MENU_FRONT_BUTTON` (black at alpha 18, about 7 %, pure transparency, so it only darkens and never milks a bright sky): a whisper kept so the hover has something to lift from.

- Hover and pressed stay the shared tones, so the pointer lifts the same slab every other page shows.
- It is asked of the **page** (`MenuPage.EntryRestBrush`), because `ApplyNavHighlight` repaints every entry each pass and would otherwise paint the shared grey back over it. **A page that rests its entries in different tones answers per entry** (`MenuPage.EntryRestBrushFor(Button)`, defaulting to `EntryRestBrush`; High Scores' rows of levels not cleared, #898): a tint set on one widget is gone with the first move of the cursor. The brush must be a built-once one (`BS3DGame.MENU_ROW_UNFINISHED_BRUSH` is a static): the pass runs on each change of focus and of entries, and an allocation there is one per entry per press.
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

## Typesetting (#806)

The rules every page's text is set by. A rule the text engine cannot draw is said with it.

### What the engine can do

FontStashSharp 1.5.6 under Myra 1.6.3, read off the assemblies and the faces' own tables:

- **Kerning: yes**, from the faces' GPOS pair tables (`FontSystem.UseKernings`, on by default), each pair truncated to whole pixels at its size.
- **Leading: `Label.VerticalSpacing` only**, pixels added between lines. A face's `LineHeight` is its ascent-to-descent at the size asked for, because FontStashSharp sizes a face by that and not by its em: Inter's line is 1.21 em, Anton's 1.51.
- **No tracking on a label.** `characterSpacing` exists on `DrawString`, not on a Myra `Label`, so an all-caps heading cannot be letter-spaced.
- **No OpenType features.** They need text shaping (HarfBuzz), which the game does not ship: no `tnum`, small caps or ligatures. Inter's figures are proportional (its 1 is 833 units, its 0 1292); Anton's are tabular but for the 1 (677 against 1012). Tabular figures are set by hand ("Numbers" below).
- **Rich-text commands a label lays out**: `/s[n]` is a chunk of n pixels of nothing. The layout measures each chunk of text between commands on its own, so kerning acts only inside a chunk; a glyph's advance is a whole pixel.
- **Auto-ellipsis is three full stops** unless `AutoEllipsisString` says otherwise.
- **No hyphenation and no justification**, so prose is set ragged right.
- ⚠ **A label parses commands** (`SupportsCommands`): `/n` in copy is a line break and `/c[...]` a colour. A slash before one of `c e f i s t v n` is written `//`.
- **`/f[name]` switches the face inside one label** (#996), through `RichTextDefaults.FontResolver`, which Myra leaves unset (a `/f` then throws in the layout) and `BS3DGame.ResolveProseFont` answers: `lead` is Anton and `strong` Inter Bold, both at `MENU_FONT_SMALL`, so a line keeps its height whichever it carries. `/fd` goes back to the label's own face, `/cd` to its own colour. The resolver throws on any other name: a typo in copy drawn in the plain face would never be seen.

### Measure

**45 to 75 characters a line; prose is set at most `MENU_PROSE_MEASURE` wide**, Help's excepted, 1500 units, 67 characters of Inter at `MENU_FONT_SMALL` (22.3 units a character over the game's own copy). Every size scales with the height, so a column holds the same number of characters at 1600×900 and at 3840×1600.

- **Help is the exception: its text is set across `TEXT_WIDTH`, 1860 units, 83 characters** — the controls tab's lines and, by the owner's ruling (#865, 2026-10-15), every paragraph from BASICS on: the whole width for less scrolling, against a longer line than the measure. An odd ball's entry takes that less its square and the gap. The frame is the tab row's width where that is wider, which at every size it is (2072 units, #857); the text stands against its left edge, so Back stands under the text's first letter.
- The Settings note is the page's width, about 1590 units, up to the measure. About's pages are set at the measure (#807), and so is its widest thing, the player's spectrum; a credit list's two columns share it, so a role that would break the line is shortened rather than wrapped.

### Leading and paragraphs

- **Prose has a line pitch of 1.46 em**: Inter's own 1.21 plus `MENU_PROSE_LEADING` (12 at size 58, a pitch of 70). That is the open end of the usual 1.3 to 1.5, because it is light type on a dark plate. Anton keeps its own 1.51 em (the words on Help's controls page).
- **One gap between paragraphs, `MENU_PARAGRAPH_GAP`**: one em of the small face (48), two thirds of a line. It is tied to the face, not a per-page figure. A block carries the gap under itself and a stack of prose adds none (Help's body has `Spacing` 0).

### Hierarchy and the scale

- **Hierarchy is size, face and colour together**, not a gap alone: Anton for headings, entries and values, Inter for reading; `MENU_TEXT` for what is loud, `MENU_TEXT_BODY` for prose, `MENU_TEXT_DIM` for an aside (a caption, a note beside the rows, what a field wants).
- **The sizes are steps of one scale of about 1.3**: 46, 58, 80, 96, 124 and 170 lie within 6 % of 45, 58, 75, 98, 127 and 166, a 1.3 ratio from the small face. They are kept rather than snapped to it, because each was measured against a budget (the Settings rows, #138; the picker's tile, #496) and a few per cent is under what reads as a different step. **New text takes a role** (small, body, section, heading, title), never a new figure.
- **A lead-in opens a paragraph in the paragraph's own line** (#996, `MenuPage.LeadIn`): the display face's capitals at the small face's size, in `MENU_TEXT`, a third of an em more than a space after it, its words tied. It is how a block of prose says what it is about without a heading's line of its own, so a page set with leads is no taller than without them. **A strong word** (`MenuPage.Strong`) is Inter Bold in `MENU_TEXT` inside a sentence, for the thing on the screen the sentence tells the player to look at; each word is marked on its own, so a line may break between them.
- **A label over a block is the section role**: capitals in Anton at `MENU_FONT_SECTION`, such as the Settings groups' headings (CAMPAIGN) and the boards' chapter name. A small label is not used: set in the small face over rows in the display face, the Settings headings read as too small (#243, "Typefaces" above).

### Alignment and the rag

- **Prose is left-aligned and ragged right**; only a line that stands alone (a heading, a button's word, a value) is centred.
- **`SpaceWrap` chooses the breaks** (`Game/Screens/SpaceWrap.cs`, `SpaceWrapTests`), in order of weight: the fewest lines (what a greedy fill takes, so no page's height grows); no last line of one word where two fit; the evenest rag (the squared slack of every line but the last, plus a penalty on a last line under 30 % of the measure and a price on a line that ends on an operator, so a short sum goes over whole). It breaks at spaces only: FontStashSharp's own wrap breaks after a `.` and split the score server's address (#769).
- **A page asks for prose, it does not set it**: `MenuPage.Paragraph` and `Aside` (the small face, the colour, the measure the page passes, the leading, the gap), `Prose` for a page that places it otherwise, `SetProse` for a label whose text changes, `ProseHeight` for a label whose height must hold every text it can show.
- **Every label that wraps is written through `SetProse`, never through `Text` (#855).** FontStashSharp's own wrap takes a full stop as a break opportunity, and when the glyph that overflows the line is the space after a sentence, the last opportunity before it is the stop: the line breaks there and the next one starts with that space. Help's copy laid out raw, as it was before #806, printed " The aim preview shows the bend." in a 1600×900 window; through `SpaceWrap` the same copy gave no such line at any of eight heights, because it breaks at spaces only and no line it makes starts or ends with a space whatever the copy does (a tie at a word's edge is dropped like a space). `Wrap` stays on as the backstop for a line the breaker left wider than the label. **A line that carries a command is measured by FontStashSharp's `RichTextLayout`**, the class the label lays its text out with (`MenuPage.LineWidth`), because the font's own `MeasureString` counts the command's letters as glyphs and knows nothing of a lead-in's other face; plain copy keeps `MeasureString`. `ProseLabelsTests` reads `Game/Screens` and holds every `Wrap = true` initializer to it; the one exception is the note page's typing field, whose lines `FieldTail` cuts to the width. A status line that holds its height with a single space keeps that space.

### Characters

- **Apostrophes and quotes are ’ ‘ “ ”**, never `'` or `"`.
- **The dash is a spaced em dash, " — "**, the form most of the copy was written in. A range of numbers takes an en dash, "3–16"; in prose a range reads "3 to 16". A hyphen belongs inside a word.
- **An ellipsis is …**, one character, and a label that cuts its line sets `AutoEllipsisString = "…"` (the picker's detail line, High Scores' names). **One space** after a sentence, as between words.
- **The parts of one line of readout are parted by a spaced middle dot, " · "**, one space each side ("Chapter 2 of 14 · 3 of 10 cleared · ★ 4 of 40", "Level 13 · Star"). Runs of spaces are not a column: numbers that must line up stand in cells of their own ("Numbers" below).
- **Ties.** A no-break space (U+00A0, written in C# as its `\u` escape so it can be seen) holds a name to its version ("MonoGame 3.8.5", "SIL OFL 1.1", "DirectX 11"), a number to what it counts ("385 balls", "10 points"), a range ("3 to 16") and a label to its number ("Shot 1"). `SpaceWrap` adds two of its own: a lone dash or operator (— – + = × ÷ −) never starts a line, and a one-letter word (a, A, I) never ends one. A tie is drawn as a plain space.
- **A key name needs no tie of its own**: the copy names a key in one word (Enter, Esc, Space, F12), and a pad's A is held by the one-letter rule. A key named in two words would take one.
- ⚠ **A tie holds only in prose set through `SpaceWrap`.** FontStashSharp's own wrap counts U+00A0 as white space, so in a label that wraps by itself it is a break like any other.
- **`CopyRules.Fault`** names the first of these a string breaks; `CopyRulesTests` holds every string a tutorial card draws to it, `AboutCopyTests` every string About draws of its own (`AboutCopy`, #807), `PickerCopyTests` the level picker's readout, tile, detail and boards lines (`PickerCopy`) and `ResultCopyTests` the result page's words that change with the ending (`LevelResult`). The rest of those pages' fixed words (a button's name, a caption, the online status lines) and the other Myra pages' copy are held to it by review, since a page's strings are built inside a tree no test constructs; a page whose copy is moved into a plain class it draws from can be held by a test.

### Numbers

**A column of numbers that is read down is set flush right in tabular figures**, so the units line up and every digit stands under the digit above it. The faces cannot do it (above), so `TabularFigures` does it by hand: every digit gets a cell as wide as the face's widest digit and is centred in it by two `/s[n]` commands, one each side, written even where a share is 0, so no two figures share a chunk and no kerning pair between them can move one. `FontFigures` hands it a real font's ten advances (kept per font) and is what a page calls: `Tabular` for a label's text, `Width` for how wide that lays out.

- **Where**: the boards' ranks and scores (`BoardView`, so the result page's plate, the picker's plate and `LevelBoardPage`), High Scores' level numbers and both score columns, and the result breakdown's details, values and total. A number that stands alone keeps the face's own figures: a tile's best score, "Score 4 820", the HUD.
- ⚠ **Size a column by `FontFigures.Width`, never by `MeasureString` of the set text**, which counts the commands as glyphs. The breakdown pins its columns that way (#665).
- ⚠ **A cell is a width in one font size's pixels**, so a number is set again for the font of every tree it is drawn in: the result page sets its figures in `Refresh`, which a rebuild at another size runs. Set when a number changes, never per frame (the breakdown's reveal writes strings set once).
- The set text is command text: a `/` in it is doubled, and the label must keep `SupportsCommands` (Myra's default).

## The pointer is the game's own (#350)

`Game/Platform/PointerCursor.cs` builds a stylized arrow at load as a `Texture2D` from a signed distance field (outline, drop shadow and antialiasing all read off the one distance) and publishes it through `Mouse.SetCursor`. It is the one piece of chrome **Windows** draws.

- No bitmap asset, nothing for the content pipeline to carry into the release zip.
- **Its size comes from the display's own** (`HEIGHT_OF_DISPLAY` of the panel's height, clamped between `MIN_HEIGHT` and `MAX_HEIGHT`): a 32 px arrow authored for 1080p is a speck on 4K, this project's base. The height is in physical pixels (the game is DPI-aware, #825), so the Windows scale does not change it, as it does not change the menus: a 64×64 bitmap with its hot spot at (4, 4) at 3840×1600 at 100 % and at 125 %.
- It is greyscale for the chrome's reason, and because a grey bitmap is byte-identical if the RGBA-to-BGRA hand-off into GDI is ever the other way round; a coloured one would be the bug that only appears on somebody else's machine.
- ⚠ **`Mouse.SetCursor` is called exactly once, from `Initialize`.** It hands the window a GDI cursor handle, so a per-frame call builds and abandons one per frame, the known road to an intermittent MonoGame crash. What toggles per frame is `IsMouseVisible`, which only ever **hides**; hiding does not un-choose the cursor, so the play loop that hides it and draws the procedural `Crosshair` needs nothing here.
- **Neither capture route can photograph a cursor**: `shot=` saves the back buffer, and `screenshot.ps1`'s `CopyFromScreen` is a BitBlt, which does not draw the pointer. The check is `GetCursorInfo` → the live handle → `DrawIconEx` onto a plate of your own, which answers identity (ours, or `IDC_ARROW`), the hot spot and the bitmap at its real size in one call. Measured that way, the handle survives a window resize, the pointer leaving and returning, and a fullscreen toggle (the Game's only in-session `ApplyChanges`); in `GameplayScreen` it reports no cursor, and Escape brings the same handle back.
