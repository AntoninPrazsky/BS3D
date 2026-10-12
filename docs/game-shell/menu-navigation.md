# Menu navigation: pad, keyboard, mouse and the prompts drawn for them

Part of [The game: shell](../game-shell.md).

The menus are fully drivable from a gamepad (#61): Xbox is a target, and `Buttons.Back` already opens the pause menu. The pad also feels what it does here ("The pad" in `docs/game-feedback.md`, #800).

## Who drives

- The D-pad, left stick and arrow keys move a focus cursor; **A** or Enter presses the focused entry; **B** is Escape, both through the one `MenuBack` that owns which screens have a back. The A that completes the Konami code on the main menu (#230) presses nothing.
- **A grid page is walked by the pad in the direction pressed** (`MenuPage.PadWalksGrid`, the level picker, #848): the D-pad and the left stick move the cursor to the entry in line ahead (`NavWalk.Toward`: in line first, then the nearest; out of line only within 45° edge to edge, so the grid's ends stop), and the shoulders turn the page. The arrow keys there keep the list's order on up and down and turn the page on left and right, so a keyboard player still reaches every entry. The owner, pad in hand, expected down to light the level under the lit one and found the D-pad turning chapters the prompts gave to LB and RB.
- **Entries are found by walking the shown widget tree** (`CollectNavEntries`: on a screen change, a layout rebuild and a level-picker chapter turn, never per frame), not from a registered list that an entry added later silently falls out of. `Visible = false` keeps the cursor off Continue and Next Level for free.
- ⚠ **Add children in the order the pad should step through them**: the walk follows insertion order.
- ⚠ **Exactly one device drives at a time.** The focus cursor reuses the hover brush, but the pointer need not *move* to sit over an entry (a pause recentres it), which lit a second entry beside the focused one. So while the cursor is up the hover is **off** (`ApplyNavHighlight`); moving the pointer puts the cursor down and restores it.
- **Which entry is focused does not survive a screen change; whether the cursor is up does** (a pad player backing out of a sub-screen would otherwise press a direction every time). `CollectNavEntries` re-baselines the pointer, or the frame after a pause reads its recentring as the mouse moving. **A page turn is the one re-read where the index must survive**: `RefreshNavEntries` names the entry to keep.
- **Accept with the cursor down only *raises* it**: firing the top entry would make the first press mean "New Game" over a session in progress, on the screen that exists to offer Continue.
- **A held direction** steps once, waits `NAV_REPEAT_DELAY`, then walks at `NAV_REPEAT_INTERVAL`, in wall-clock seconds, not frames. One `HeldDirectionFires` serves **two axes** (up/down walk the entries, left/right go to the active page, `MenuPage.PageSideways`: picker, Help, Settings, High Scores), each with its own held-direction fields so a diagonal push holds both.

## The keys are drawn in the UI (#799)

The binding is part of the UI, always visible beside what it does, never a legend elsewhere.

- **What is drawn.** B or Escape on every Back and on the entries that do what B does (the pause's Resume, the Quit confirm's Cancel); LB/RB or ←/→ at the ends of the Settings and Help tab rows (`MenuPage.TabRow`, #857); **the pad's LB/RB under the picker's and High Scores' chapter arrows and About's page arrows (#807), and nothing for the keyboard there** (`MenuPage.ArrowWithShoulder`, #848: an arrow key beside an arrow only repeated it, the owner's call); LB/RB or ←/→ under the level boards' Previous/Next (`LevelBoardPage.PageSideways`); A or Enter on the focused entry; the right stick or wheel beside "Scroll" on a long Help page.
- **One hand at a time** (`BS3DGame.PromptsForPad`): the last device to give a menu any input decides (`NotePromptHand`). The pad means a button, or a stick or trigger past `NAV_STICK_DEADZONE`; keyboard and mouse mean a key held, a button, or the pointer past `NAV_MOUSE_WAKE_PIXELS` (reset on a screen change, so a pause's recentring does not count). A pad beside a keyboard player changes nothing until touched (#817). A pause starts from the session's hand (`Rumble.HandOnPad`). **`pad`** pins the menus as it pins the session.
- **`PromptGlyph` draws whichever glyph is current as it renders**, as wide as the wider of the two, so changing hands changes no layout. Inside a button (`MenuButton`'s prompted form) the word stays centred and the glyph stands at the slab's inner edge. Too-narrow entries get their prompt outside, dimmed with the entry (`StandsFor`): under the level boards' Previous/Next (`MenuPage.PageTurnUnder`, #848; Help's walking buttons had theirs beside them until the tabs of #857).
- **A or Enter is the host's** (`DrawFocusPrompt`, after the desktop), so a walk moves one glyph and relays no page. Drawn only while the cursor is up, never on an entry with its own prompt, and only on one at least `FOCUS_PROMPT_MIN_SHARE` of a column wide.
- **Glyphs.** The shoulders are PromptFont's full-height pills U+2B98/U+2B99 (the HUD's Cut chip's U+2199 is half height and read as small print). PromptFont has no single-arrow keycap, so ← and → are its bold arrows.
- **The pad's Menu button pauses a level and resumes it** (before #799 only View did). Re-check with `pad` and `menucursor=` ([Launch arguments](front-end-launch-arguments.md)).
