# Write to the Author (Send a Note)

Part of [The game: shell](../game-shell.md).

**A player can tell the author what they just saw, from the pause menu, About or Extras; the note goes with the game's context and, unless the player unticks it, a picture of that moment** (#813). The owner's answers (2026-10-06): anyone may send one, online scores on or off (pressing Send is the consent for that note), and the picture is the player's to leave out. Files: `Game/Screens/NotePage.cs`, `Game/BS3DGame.Note.cs`, `Game/Online/OnlineNotes.cs`.

## The entry

"Write to the Author" in sea teal (`BS3DGame.NoteEntry`, `NOTE_ENTRY`; `MENU_TEXT_NOTE` is one more deliberate exception to the greyscale rule: an invitation, not a control). (It read "Send a Note" until 2026-10-07: that did not say to whom.) **Where**: the pause page after Settings and Scene, above the three that end the run (#383); About under the repository link; **Extras first**, above Top Players (the owner, 2026-10-29). **Not the result page** (no room).

## The picture

The frame the player was looking at, not the menu over it: the page's first frame draws the game sharp with no scrim or widgets, the host reads it back at the end of `Draw`, scales it down by a whole factor to at most `NOTE_SHOT_WIDTH` and encodes a JPEG, retaking it smaller while it exceeds `NOTE_SHOT_MAX_BYTES` (the service refuses over 400 KB). Only then do the blur and widgets appear. A failed read-back sends the note without a picture and says so.

## What it carries

`NoteContext`: build, OS, GPU, back buffer size, fullscreen, tier, scene; with a session, the level's file, name, number, chapter and hash, the phase and the shots, score and seconds so far; while a level's theme is up, the recording it is playing (`music`, the stem a level can pin) and whether its drums layer is mixed in (`musicDrums`), since a family rotates through its recordings and a note about the music names one only by this (#824). **Nothing names the player or the machine**: no paths, user name or address. A player with an online identity **signs** it (id, name, bearer token, read when sent, #572); "Remove scores" makes the service forget their notes too.

## Typing

The nickname plate's way (#763): the characters Windows sends, keyboard captured from the first frame, only what both menu faces draw. **Enter sends**; Shift+Enter or Ctrl+Enter starts a line; at most `OnlineNotes.MaxTextLength` characters. A key within `INPUT_GRACE_SECONDS` of the picture is ignored, so an Enter held from opening the page cannot send a restored draft unseen. Esc goes back and keeps the draft. A pad cannot type: A sends, B goes back. A phone types through its own dialog, which opens with the page and again on a finger on the field (`PhoneTyping`, stepped in `PhoneTypingStepTests`): its OK only puts the text in the field, and the page's Send sends it; **its Cancel leaves the page and the text as they were** (#1182, `NotePage.TypingDialogEscapes`), and the page's Back or the system's back gesture leaves it with the draft kept (the host hands a typing page the gesture as B). The nickname plate and the Settings row keep a cancel as their Esc. On the phone layout the field is as tall as the page has room for (`NotePage.FieldLines`, at most `FIELD_LINES_PHONE_MAX` lines), measured when the tree is built and fixed with it, so Send does not move while a note is typed.

**The line under the field speaks to the hand in use** (`NoteHint`, `NoteHintTests`, #1149): on a phone, whatever is held, "Tap the field to type, then tap Send.", and the same on a monitor under the `touch` argument, which lays the phone's hand out there. Elsewhere a pad the menus last saw gets "Enter sends. Shift+Enter starts a new line." and "A pad cannot type.", and any other hand that first line alone, a finger on a Windows touch screen included: a tap on the field opens a dialog only on the phone, so off it a keyboard is what types. Each carries the length against `OnlineNotes.MaxTextLength`, the two tied. It is rewritten when the hand changes, and its height is measured for the tallest it can say on this platform (`NoteHint.HandsFor`: the phone's line, or the keyboard's and the pad's, at the full count, and the "Write something first." complaint), so a hand picked up never moves Send and a phone keeps no row for the pad's longer line.

**The field shows its last lines as laid out** (six, more on a phone) (`FieldTail`, `FieldTailTests`): each line measured against the field's width, an over-wide word broken between characters, an ellipsis opening the first line.

## The outbox

Its own outbox and worker, on the score client's rules ("Online scores" in `docs/game-session.md`).

- **Every note is written to `Notes/` in the user data before a byte goes anywhere** (format: `docs/formats-and-tools.md`) and leaves only when the service has answered for it; at most `OutboxCapacity` are kept.
- **The server is the one `OnlineScores.TryResolveServer` resolves**: a local build reaches only a server `Settings.json` names, so a developer's runs never post to the owner's notes. One `[note]` line at start says where notes go.
- **A 2xx counts only when it names the note's id**. A 408, 429, 5xx, a 4xx without the service's reason, or no answer within `RequestTimeout` keeps the note; **a 4xx with a reason drops it and logs why**, except a refused picture (`bad-picture`, `picture-too-large`), when the words go again without it.
- **No retry timer**: the outbox drains at start and after each new note, oldest first, stopping at the first without an answer.

## Testing it

**`note=<seconds>:<steps>`**: at that wall-clock second, with a level being played, the game pauses and opens the page as the button does; past its picture it runs `type:<text>`, `newline`, `untick`, `send` and `back` (to the pause) through the handlers the keys and buttons run, as `nickprompt=` does (a synthetic keystroke lands in whatever window has focus). **No test note goes to the live service without the owner's leave.**
