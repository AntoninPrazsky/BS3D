# Boards and High Scores

Part of [The game: a session](../game-session.md).

What the player sees of [the online scores](online-scores.md).

## The result page's plate

- **The plate is this ending's or none (#707, #716)**: `LevelResult.Submitted` says whether this ending went at all, and its visibility is written against the widget's own `Visible` every frame, not toggled against a flag kept beside it (a loss page once stood under a stale "Sending your score…"). On a loss it says "attempt", and **a zero-star row says "not finished"** wherever a board is drawn.
- **It stands from the page's first frame; only its rows wait for the reveal (#734)**: an answer before the stars land says "Your clear was sent." A player with the switch ON and **no score server** (a hand-made build; only a release names one) is told "Online scores are on, but this build has no score server to send to."
- Once the service accepts a clear the game fetches the level's two boards (`GET /v1/boards/{file}`: top five plus the player's row) through the same worker; `OnlineSession.ResultMonthBoard`/`ResultAllTimeBoard` hold them and `ResultGeneration` moves when anything shown changes. THIS MONTH and ALL TIME, **"You #N of M"** in `BS3DGame.BoardYouColor`. **The first three ranks wear a cup** (#725; PromptFont U+1F3C6, `BS3DGame.PlaceColor`, in a column reserved on every row). The plate is **centred between the column and the screen's right edge** (#701; `BOARD_COLUMN_GAP`, `BOARD_MIN_EDGE_MARGIN`, `BOARD_MIN_WIDTH`) with its width **computed** and content held to it. No answer reads "The score server did not answer. This clear is saved and goes out with your next one." A player who has not opted in gets **one** hint line, once a session (`TakeHint`).

## The picker's board and `LevelBoardPage`

The picker's footer has **"Board: <level>"** beside Back, opening **`LevelBoardPage`** (both boards side by side, ten rows a page, cached a minute); `board=<n>[:<page>]` opens it at boot.

## High Scores (#685)

The main menu's entry after Select Level opens `HighScoresPage`, a chapter a page, one row a level; `highscores[=<chapter>[:all]]` opens it at boot.

- **It stops at the ends (#727)**: no wrap (the picker's pips say where the player is; this page has none, and a press must not jump from the last chapter to the first), the off arrow dims its glyph and keeps its slab, `PageSideways` answers false. It opens on the chapter the player has reached.
- **A row** shows the board's #1 and the player's place and opens that level's boards (`OpenLevelBoard`); a locked level shows its name and "Locked" only (a top score is a level's shape in numbers, #266). A row that cannot be opened keeps the colours the writing pass gives it (`DisabledBackground`, `DisabledTextColor`; Myra's disabled look made the opted-out page unreadable). **It ranks levels** (ranking players needs a rule weighing levels with different ceilings).
- **A row's numbers stand in columns (#806)**: the level's number flush right in a column as wide as the set's longest number, then the name; "#1 Kuba" with the leader's score flush right at the end of its block; "You: 7th of 17" with the player's score flush right at the end of theirs. All in tabular figures ("Numbers" in `docs/game-shell/menu-look.md`). A name or place that would run into its score ends in "…". An unfinished attempt's mark follows the name ("#1 Rudi, not finished"), not the score.
- **One request per period, not per level**: `GET /v1/boards?period=&player=` (BS3D-API#7) returns every board's #1 and the asker's place (`OnlineScores.RequestSummary`, cached a minute, `SanitizeSummary`; boards match levels by `LevelIdentity`); per-board GETs would be ~130 an opening against Cloudflare's 50 in 10 s. A **404 or 405 is a service older than #7**: the page says it does not offer the overview yet.
- **Opted out, the page still opens**, says what turning online scores on gives and offers a Settings button.
