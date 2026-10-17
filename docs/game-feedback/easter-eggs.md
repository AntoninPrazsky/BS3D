# The easter eggs

Part of [The game: feedback](../game-feedback.md).

Six easter eggs from #230: the sleeping gun, the secret shot code, the Konami code on the main menu, the game's birthday, the code word and the impossible shot. They are never explained anywhere in the game.

**Every one is cosmetic, by the issue's own rule.** An egg that touches the rules quietly invalidates every gate that measures them (ScoreSim, ClearProbe, the sag gate), and a bonus score would be refused by the online boards, which reject anything over the ceiling ScoreSim simulates. That rule overrode the issue's own wording for the wormhole (it asked for bonus points). Nothing in physics, scoring or level flow may read an egg's state.

## The sleeping gun

Leave a level untouched for `DozingGun.SLEEP_AFTER` (75 s) and the gun nods off: the barrel sags, rises and falls on a slow breath (`BREATH_PERIOD`), and at the top of every breath a quiet snore plays (`ProceduralAudio.PlaySnore`) and a letter Z rises over the trunnions (`PlayHud.DrawSnores`, `Z_LIFE`). The dials are `DozingGun`'s constants.

- **The sag is drawing only.** `Cannon.Droop` lowers the bore that `BarrelWorld`, `BarrelOrientation` and `DrawnMuzzlePosition` draw (the barrel, the rounds in it, the collar) and nothing that decides where a shot goes (`AimDirection`, `MuzzlePosition`). At 0, which is all the Testbed ever sets, the gun draws what it always drew.
- **The aim beam fades with it** (`DozingGun.Awake`): a line of light running out over a barrel pointing elsewhere would read as a fault, not a nap.
- **The snore is quiet on purpose**: it is a repeating layer, so it follows the rule new sustained sounds are written to (a fraction of the party popper's level).
- **The Zs rise from over the trunnions, not out of the muzzle**: from behind the gun a muzzle-born Z is a ~12 px speck among the cluster's lowest balls.
- **Any touch wakes it** (`WAKE`, 0.3 s): aim, walk, a shot, the lean, any key or mouse button, and on a pad any button, the D-pad, a trigger past XInput's own threshold (30 of 255) or a stick off rest (`GameplayScreen.PadTouched`, #818). It reads state the frame already has, no second poll.
- ⚠ **Triggers come raw on both platforms**, with the trigger button bits set for any reading on DesktopGL, so a worn trigger resting at a few counts would keep the gun awake for ever if it counted: hence the threshold.
- A pause does not count towards a nap (the session is not updated under it), nothing of it runs under the result page, and `BuildLevel` wakes the gun for the next level. The ceiling steps only for shots, so a level nobody touches is a level nobody loses.

## The secret shot code

Three shots that miss, two that land, three more that miss (`Prazsky.BS3D.Scoring.SecretShotCode`, fed from `OnShotSpent` and `OnBallLanded` after their `LevelOver` guards) answer with `SHOT_CODE_CONFETTI_SECONDS` of the campaign's confetti and the party popper's crack (`CelebrateShotCode`). No ball, no score. The six misses are the price of finding it. Pinned in `SleepAndSecretTests`.

## The Konami code on the main menu

Up, up, down, down, left, right, left, right, B, A: arrow keys and the B and A keys, or the pad's D-pad or left stick and its face buttons (`Prazsky.BS3D.Input.KonamiCode`, fed from `BS3DGame.FeedKonamiCode` off the same edges the menu navigation reads, **on the main menu only**). The game's 3D name answers it (`TitleWordmark.Celebrate`): each letter hops, turns once round and swells in reading order while the rainbow runs one extra turn, and a quick run up the star chime plays (the rating's triad with a note in front). A completed code prints `[konami] The code is complete at <t> s` to the run's log. Both codes are one mechanism, a sliding window over the last inputs (`SecretCode<T>`), each subclass naming its sequence.

- **The left stick spells it one tap a push** (`Prazsky.BS3D.Input.StickTap`, #818): one direction on the frame the stick leaves rest past `NAV_STICK_DEADZONE` (0.55), the axis pushed further on a diagonal, then nothing until it is back inside half of that, so a held stick is one tap and a thumb trembling on the edge is not two. It is disarmed until first seen at rest, and leaving the main menu disarms it with the code. `SleepAndSecretTests.TheStickTapsOnceAPush` pins it. The stick was ignored at first ("a code is spelled in taps") and the owner's pad attempt did nothing.
- ⚠ **The pad's completing A is swallowed.** Up-up-down-down leaves the focus cursor on the first entry (New Game / Continue, which starts a level), so the code is read *before* B and A are acted on, and the frame that completes it acts on nothing else. The keyboard's B and A mean nothing to the menu, so nothing to swallow there.
- **The title lifts in cap heights, not scaled by the line**: a badge letter scaled up with its line would jump into the line above, where the gap is `LINE_GAP` of the badge's height.
- **The code word is keyboard-only by nature** (a typed nickname; no on-screen keyboard for a pad). The other eggs ask nothing of the device: the shot codes read shots (a pad's stick left alone holds the pose as a still mouse does), the birthday reads the date.
- Scripted check: `padpress=<start>:<step>:<names>` (`ScriptedPlay`) writes presses into the frame's own pad state, the way `padrt=` writes the trigger, so everything after the XInput read sees a real press. Not seen on a real pad: what the XInput read delivers is the owner's hands to judge.

## The game's birthday

On **30 December** (the repository's first commit, 2019) the gun wears a party hat in every level (`Effects.PartyHat`), the campaign's confetti falls with the party popper's crack the first time the main menu comes up, and the title dances the Konami dance 1.5 s later and every 20 s (`BS3DGame.StepBirthday`, advanced with the celebrations, not in the menu's chrome, which runs only with focus). The About page's spectrum is the rainbow that day (`MusicVisualizer.VisualizerPalette.Spectrum`, #764).

- `BS3DGame.Birthday` is decided once at startup (a run that crosses midnight keeps its day), the hat is built only on the day, and nothing about play reads the flag. **`birthday` on the command line** makes any day the day and is the only way to photograph it.
- The hat sits on `CannonRig.BreechCrown`, the one place on the barrel's top that is neither glass nor dome, drawn with the barrel's own pose, so it recoils, walks, nods off with the gun and shrinks away as precise aim leans in. It casts no shadow and takes no motion blur.

## The code word

A nickname that is a secret word puts the same party hat on the gun, in every level, on any day. The words are `bepu`, `bepuphysics`, `monogame` and `prazsky`, compared case-blind with spaces, underscores and hyphens left out (`Nickname.IsSecretWord`): `Bepu Physics` and `mono_game` count; `bepus`, `mono`, `xbepu` and the accented `Pražský` do not (accents are letters). `NicknameSecretTests` pin it.

- A nickname is typed while the game runs, so the hat is built on the first frame it is wanted; the check is one string comparison per *change* of nickname, not per frame, and changing the name to anything else takes the hat off.
- ⚠ **The hat is handed the sky-light rig the moment it is built** (`BS3DGame.PartyHat`). Built on the first `DrawGun` that wants it, long after the scene's `ApplySkyLighting` ran, it would draw under the library's default rig, and the damage did not stop at the hat: the gun's steel and the island's ring came out white-silver in the frame with the fault (the mechanism is inferred: the hat's renderer shares the instancing effect and its defaults are the only difference). Building it in `LoadContent`, before the scene, is what had hidden the fault.
- Check it with a scratch profile whose `Online.json` names the player `Bepu` (`userdata=`, no server involved), against a control with no nickname.

## The impossible shot (the wormhole)

Three shots in a row fired from the same place along the same line, every one flying clean out past the island: a wormhole tears open in front of the last one, swallows the shots that missed and snaps shut with a pop.

- **The run** is `Prazsky.BS3D.Input.RepeatedMiss`. `Shoot` records each shot's muzzle and direction (`_firedPoses`, keyed by the body's handle); `WatchForEscapes`, once a frame after the step, reports a still-undecided shot that stands past `ESCAPE_RADIUS` (the island radius plus 3) and moves **outwards**. A shot that struck the stone or landed (`OnBallLanded`) breaks the run. "The same pose" is held against the **first** shot of the run within `ANGLE_TOLERANCE_DEGREES` and `MUZZLE_TOLERANCE`; pairwise comparison would let a barrel turned a degree a shot walk a run round the whole sky.
- **The hole** (`Effects.Wormhole`, `Wormhole.fx`) opens `AHEAD` in front of the third shot and swallows every shot out past the arena within `SWALLOW_REACH`, each spun down the throat, shrinking; then it stands, gulps and shuts with a white four-rayed pop. Two baked sounds: `PlayWormholeOpen` and `PlayWormholePop` (warm and round, by the owner's rule for this game's sounds).
- **A swallowed shot is the miss it already was**: retired the moment it is caught through the door the kill plane uses (`RetireBall`, then `OnShotSpent` if it was still undecided, never once the level is over). What spins into the hole is a drawing (type and kind handed to the frame's collection with a shrinking, tumbling matrix). It runs on the simulation's clock, so a drop cinematic slows it and a pause stops it.
- ⚠ **The hole is moved into the frame** (`KeepInView`). The play camera does not turn with the barrel, so a miss at the edge of the traverse leaves the frame long before it leaves the island and the first capture opened the hole as a sliver at the edge. It is kept at the same distance from the lens with its whole disc inside `VIEW_REACH_X` (0.72) of the half-width (inside the HUD's side columns) and 90 % of the half-height.
- ⚠ **The swallow must also require an outward-flying shot**: a gun standing past `ESCAPE_RADIUS` (a big field's orbit at a narrow window) fires shots that start out there flying in, and one of those would have been scored a miss before it could land.
- ⚠ **Sparks must not be hashed by their raw cell round the circle**: it jumps by `SPARK_SPOKES` at atan2's cut, so every spark the spin drew across it vanished or became another, leaving a seam from throat to rim.
- ⚠ **The colours are kept low.** Driven at 3-7 the swirl came out a pale pink-white smear (the tonemap's shoulder takes the hue off anything that bright) and its glare filled the throat with grey. The swirl peaks at about 2 and the pale edge about 1.3 over a disc that takes `SWIRL_DIM` (85 %) of what is behind it, so violet, magenta and cyan read even on the desert's noon sky; the sparks down the arms and the pop are driven far over the glare threshold.
- Scripted check: `aim=3:10:45 fire=5,5.7,6.4` (Pendant in the desert), reading the log's `[wormhole] opened at …`. `RepeatedMissTests` pin the run (three escapes complete it and it restarts, a break restarts it, the pose is held against the first shot).

## Hinted at from the campaign's finale

The campaign's last clear shows one message with one cryptic hint at an egg (["The campaign's finale"](milestone-endings.md#the-campaigns-finale-a-headline-and-one-message-880), #880). `CampaignFinaleCopy.Egg` is the list the hints are checked against and every egg on it is hinted at by at least one message; the eggs the hints may name are the ones described above plus the About page's player. Which hint points at which:

| Hint begins | Egg |
|---|---|
| "A detective’s note from the case file: the barrel that never tires…" | the sleeping gun |
| "Count your misses the way a detective counts alibis…" | the secret shot code |
| "The title above the menu has a ticklish memory…" and "The old front door is more forgiving…" | the Konami code on the main menu |
| "Between Christmas and the new year, one single day…" | the game's birthday |
| "Even a name can be a password…" | the code word |
| "Three identical shots into the same empty sky…" | the impossible shot (the wormhole) |
| "Turn every page of the credits, to the very last one…" | About's player of the original score |

A new egg gets a case in `CampaignFinaleCopy.Egg` and a hint; the About page's player counts as an egg here because the owner ruled it "a small easter egg, waiting at the end for whoever turns that far" (2026-10-12).
