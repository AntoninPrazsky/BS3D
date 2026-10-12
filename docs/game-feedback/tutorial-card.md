# The tutorial card: look, devices, detection and test levers

Part of [The game: feedback](../game-feedback.md). Which lessons exist, their order and the save: [the tutorial](tutorial.md).

## Where it stands, how it moves, how big

- **Top centre**, at the score's height (`HUD_MARGIN`): the strip between the FPS line and the score is empty in *both* camera poses. **No plate.**
- It arrives with a **bounce** (`HudEase.OutBack` from 0.6: the game offering something), **bobs** slowly while up (through the praise too), scales **about its own centre**.
- **The loudest standing type in the game (#461)** (an award's zoom is bigger, for its read alone): Anton at `HUD_FONT_TUTORIAL` (83 px at 900p, 178 at 3840×1600), detail `HUD_FONT_TUTORIAL_DETAIL`, glyph `HUD_FONT_PROMPT`. It outranks the score: sizing it as a readout was the error twice. It is the one text a new player *must* read, up for seconds, where the eye cannot leave the cluster. The keycap has its own size because PromptFont fills its em and Anton does not.
- **A strip, not an aspect** (`PlayHud.DrawTutorial`): bounded by the **score's** left edge (handed in) less `HUD_TUTORIAL_CLEARANCE`, scaled down if it would not fit. The clamp never bites at shipped sizes (the widest card, the pad's *Hold the left trigger to look down the barrel*, is 2552 design units against about 3020 at 16:9); it is for narrower windows and longer captions.

## The glyphs are a font: PromptFont

- v1.15, `Game/Content/Fonts/PromptFont.ttf`, SIL OFL 1.1. **Its own `FontSystem`** (`_menuFontSystemPrompt`): FontStashSharp falls back glyph by glyph, so inside Anton's system Anton's A would be drawn where a keycap A was asked for.
- Codepoints (`glyphs.json`): keycaps on the **fullwidth Latin letters** (U+FF37 = W), Space U+243A, mouse U+27FC, left/right buttons U+27F5/U+27F6, triggers U+2196/U+2197, sticks: right any way U+21BB, left sideways U+21C4, left up and down U+21C5.
- ⚠ **The repository ships no TTF** (only the FontForge `.sfd` and a compile script; the home moved to Codeberg): the built font is `promptfont.zip` on Codeberg's releases. Two keycaps side by side are two codepoints with **no space between**.

## The card draws for the hand in use; every pad prompt names a binding that exists

- The last device to produce input is latched (`Tutorial.NoteDevice`) and read **at draw time**, so picking up the pad swaps the prompt next frame. **A level's tutorial starts from the menus' hand** (#799, `BS3DGame.PromptsForPad`; "The keys are drawn in the UI" in `docs/game-shell.md`).
- **The pad's left stick traverses and walks the gun** (`GameplayScreen.UpdateInput`): a pad could aim, fire and lean but not turn or walk, and a card promising it would have lied. **At the speed it is pushed, from the first step** (#802): push to the power 1.5 is the share of full speed (MonoGame rescales the stick from 0 past its dead zone); `Cannon.Orbit`/`Advance` ramp to that.
- **Each stick by name:** aim the right stick (`↻`), walk-round the left sideways (`⇄`), step-in the left up and down (`⇅`), the combination LT with the left sideways.

## What the detection reads

- **Aim reads the pose** (`Cannon.Traverse`/`Elevation`, travel since the card came up), so mouse and stick are judged alike. The holds want `HOLD_SECONDS` **unbroken**.
- **Fire is the shot leaving** (`Shoot`), not landing; **match is a landing that scored**. The streak cue is the multiplier above ×1 *after* `Landed`. The glass lesson fires from `AnnounceCeilingStep` on a pressure step only; the line lesson from `UpdateLaserWarning` on the net's off-to-on edge only.
- **A lesson reported while its card is not the one up is ignored.** Nothing allocates (captions are literals).
- **The copy is held to the typesetting rules** ("Typesetting" in `docs/game-shell/menu-look.md`): `CopyRulesTests` runs `CopyRules.Fault` over every string `Tutorial.Copy` returns, both hands', so a card written with a straight apostrophe, a hyphen for a dash or three dots fails there.

## Takeovers and decided levels

- Under a drop cinematic, chapter intro or line loss the card blends out over `SUPPRESS_SECONDS` and a card arriving or up has its clocks **held** (a lesson shown to a player watching a collapse is shown to nobody); nothing shows until the intro lets go and `FIRST_CARD_DELAY` passes.
- ⚠ **A card on its way out (leaving, or praising towards it) runs to its end unseen** (#700): holding the leaving fade too made it fade back in with the hiding.
- **A decided level drops the card outright** (no queue, fade, praise or cue). Under a pause it is drawn into the overlay layer (#438), clocks stopped.

## Testing levers (both record nothing)

Cards are gated on the save and a real run would write the owner's (scripted runs pass `userdata=<dir>`).

- **`tutorial`**: every lesson with the **real** detection. **`tutorial=demo`**: a **reel**, every lesson eligible whatever the level, each card done after `DEMO_SECONDS`; for scripted Game runs, where no key can be pressed.
- **`settings`** opens the Settings page at boot. **`pad`** draws every prompt (cards, skip hint, chips) for a pad all run (`Tutorial.PinDevice`): an unplayed run reads as a mouse, so a device merely noted at start was gone by the first card.
- Pad half of the first chapter: `play level=1 tutorial=demo pad powerups=swap:1,brake:1,cut:1`. No script has run the real detection; play `level=One tutorial`.
