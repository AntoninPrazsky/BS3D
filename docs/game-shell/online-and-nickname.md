# Online rows and the first-launch nickname plate

Part of [The game: shell](../game-shell.md).

The ONLINE tab of [the Settings page](settings.md) holds three rows and a note; `NicknamePage` asks a player who never decided.

## The ONLINE rows (#548)

- **Online scores, Nickname, Remove scores**, and a note. Online scores is opt-in, off by default; turning it on the first time puts the page into typing a nickname, and an empty or refused one keeps it off. **Off keeps the identity**: on again later is the same player.
- **Remove** asks twice like the reset, then asks the **server** first (`DELETE /v1/players/{id}`). Only its yes (or its own 404; one without the service's JSON reason is no answer, #691) deletes `Online.json` and `Outbox.json` and turns the switch off. A removal made offline removes nothing, so the player still holds the token that proves the scores are theirs; with no server in reach at all, only the local copy goes.
## What is left to do is red

The menu's second hue, `BS3DGame.MENU_TEXT_ALERT` (#687). "Off", a nickname "Not set" and **"No server"** (on with nowhere to send: none in the settings, or one the client refused) are red; "No server" lives on the row alone, since the note is for deciding to opt in.

## The note

- **Fixed height**, so what it says can change without the page moving: normally `OnlineScores.PrivacySentence` (About shows it from the same call), and while typing, or after a removal or refused name, what the player needs instead. `SettingsPage.SizeOnlineNote` **measures** it from all four long notes as `Refresh` sets them (#769); counting `LineHeight`s cut the last clause.
- **Lines break at spaces only**: FontStashSharp's wrap takes a `.` as a break opportunity and split the server's address. `SpaceWrap` (`Game/Screens/SpaceWrap.cs`, `SpaceWrapTests`) breaks at spaces; About uses it too. ⚠ **Measure to `MenuPage.LaidOutWidth`, not the label's `Width`**: `Scaled` never returns less than 1, so `ScaledThickness(0, …)` gives each zero side a pixel, and a line measured at exactly the width lost its last word to a line of its own.

## Typing a nickname

- **Typed, not cycled, and still a button.** Activating it puts the page into typing (`MenuPage.CapturesKeyboard`): the host's menu frame reads no key of its own, so Space, arrows and letters type. Characters come from the window's `TextInput` (Enter and Escape arrive there too, so one press cannot act twice). The pad's A keeps and B drops.
- **Typing reads as typing (#687).** A blinking **underscore caret** follows the draft (a `|` read as a lowercase L) and the note leads with "Press Enter to keep the name, or Esc to cancel". **Moving on keeps a valid name** (another row, a tab turn, leaving the page) as Enter would; only Escape or the pad's B drops a good name, and a non-name draft is dropped quietly.
- **What a name may be** is `Online/Nickname.cs`: the service's rule (3 to 16 characters; letters, digits, single spaces, `_`, `-`), stricter in one respect: **only letters up to U+017F**, since Anton has no Cyrillic, Greek or CJK and FontStashSharp drops a missing glyph silently.

## The first-launch question (#763)

- **The owner's design: the plate says nothing about online, a server or a switch, and the name field alone is the consent.** No privacy sentence, no line on what is sent. This overrules #542's and #548's "one plain sentence" for this one entry point (`OnlineScores.PrivacySentence` stays on ONLINE and About). A confirmed name is `OnlineSession.AnswerNickname` (`SetNickname` + `SetOn(true)`, as the Online row); nothing is sent before it.
- **`GameSettings.Online` is a `bool?`**: absent is "never decided", the only thing asked about. `"online": false` reads as a decision (every file since #546 carries the key, so an untouched one cannot be told apart; not asking is the safe way to be wrong).
- **Ways off the plate.** Enter on a name, or the button while it reads **OK**, is on; **Skip** is an explicit off, never asked again. **The one button is OK or Skip by what the field holds** (the owner's ruling, after he typed a name and found Skip under it): OK for a name, Skip for empty or not a name (`NicknameEntry.IsKeepable`), re-labelled on every key; the pad's A does what it says. **Esc or the pad's B** leaves it undecided the first time (`nicknameDismissed` is 1 in `Settings.json`, the next launch asks again) and counts as Skip the second (`NicknamePrompt.Dismiss`).
- **When it asks.** `NicknamePrompt.Decide` runs the gates cheapest first, and the `[online]` line names the one that answered: the setting is undecided; the run is not scripted (`userdata=`, or any `StartupScript` lever armed, `StartupScript.Drives`); a server resolves (`TryResolveServer`); the machine is connected to the internet. Once a run (`BS3DGame.AskForNicknameOnce`), on the first frame the main menu is the player's page.
- **"Connected" is all "online" means here**: `InternetCheck` asks Windows' network list (`INetworkListManager`; fallback `NetworkInterface.GetIsNetworkAvailable()`) and sends nothing, so **the game makes no request, to the score server or anywhere, before the player has agreed.** Wrong either way is cheap (a false yes: the first clear waits in the outbox).

### Traps

- ⚠ **Nothing typed in its first `INPUT_GRACE_SECONDS` counts**: any key skips the title card, and a mashing player would hand the plate letters, an Enter that complains, or an Esc that spends one of two dismissals unseen.
- ⚠ **The first answer is the only one** (`_answered`). A pop only queues and the manager applies it next frame, so two answers between frames (Enter and a click, or auto-repeat under a slow frame) queued two pops and the second took the front end off with the dialog; two Esc were two dismissals. `nickprompt=type:Novak,enter,skip` reproduces it.

### Testing it

`nickprompt` asks in a scripted run too (replacing only the "no one is here" gate); `nickprompt=<steps>` runs `type:<text>`, `enter`, `esc`, `skip` through the handlers a key or the button runs; `nointernet` makes the connection check answer no. Re-check on a `userdata=<dir>` profile with `server` set to `http://localhost:5000` (nothing needs to listen).
