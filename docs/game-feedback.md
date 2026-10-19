# The game: feedback

Everything the game says back to the player: the HUD, the tutorial, the sound, the music, the pad's rumble, the fireworks and the cup, the light that runs through the cluster, and the camera that follows a good shot down.

**What a player is shown, and when, is the part that decides whether a newcomer understands the game** ([The HUD](game-feedback/hud.md), [The tutorial](game-feedback/tutorial.md)): a mechanism ships with the cue that teaches it. **A sound is pleasant before it is credible, and its identity sits in 300 Hz–4 kHz**, because the owner listens on monitor speakers that drop the bass. **Generated recordings replace the procedural score**, which stays as the About page's player.

## Sub-documents

- [The ripple](game-feedback/ripple.md) — how a landing's light travels through the cluster, what reaches the GPU, and the channel the alarm shares.
- [Losing on the line](game-feedback/line-loss.md) — the line-loss cinematic: the camera, the net cutting, the sparks, staging a loss and its sound.
- [The result page looks up at the fireworks](game-feedback/result-page-glance.md) — the result page's camera glance at the sky.
- [Dead weight](game-feedback/dead-weight.md) — what qualifies as a released ball that never fell, and how it looks.
- [A bomb goes off](game-feedback/bomb-blast.md) — what makes the blast possible and the effect itself.
- [A cut is made](game-feedback/cut-staging.md) — the Cut's press, flight, severing and fall: what each beat draws, sounds and asks of the pad, the slash (the blasts' blade style), the hit-stop and the slow motion, the clocks, `cutshot=`.
- [The Swap passes the rounds through each other](game-feedback/swap-effect.md) — the two rounds sliding through each other along the bore: the picture of an exchange already made, the complementary dither at the crossing, the motion blur and the streak, the bore's limit on the arc, where it reaches and where the dither stays, `swapshot=`.
- [The ceiling and the floor alarms](game-feedback/ceiling-and-floor-alarms.md) — the ceiling announcing its step, and the laser grid on the floor.
- [Sound: placement, voices and the mix](game-feedback/sound-placement-and-mix.md) — `Apply3D` and the distance split, `VoiceRing`, the call order and the NaN trap, the stereo image, the listener, the volume rows.
- [The sound effects](game-feedback/sound-effects.md) — the rules a sound keeps, how a bake is built, the sounds and the generated recordings.
- [The easter eggs](game-feedback/easter-eggs.md) — the sleeping gun, the secret shot code, the Konami code, the birthday, the code word and the wormhole.
- [The victory fireworks](game-feedback/victory-fireworks.md) — where they live and when they run, the mechanism, the look and the sound level.
- [Milestones: a block and the campaign](game-feedback/milestone-endings.md) — finishing a block of levels and the campaign's confetti.
- [The cup the player just won](game-feedback/trophy-cup.md) — the trophy's placement and motion, its foreground layer, materials, ornament and geometry traps.
- [The in-play HUD](game-feedback/hud.md) — how the score, awards, balls-left and streak are drawn and move: structure, corners, `Pulse`, colour, legibility backing, when the HUD stops, harness traps
- [The magazine strip, the chips and the muzzle's marks](game-feedback/hud-magazine-and-muzzle.md) — how the player reads which round fires next and whether it is special: the strip, the Swap/Brake/Cut chips, the muzzle collar, the special-round rays, why the loaded balls do not breathe
- [The ceiling warning: the cluster profile](game-feedback/hud-cluster-profile.md) — the side-cut panel showing where the glass stands against the cluster and the death line
- [The tutorial: lessons, ladder and save](game-feedback/tutorial.md) — the thirteen ladder lessons with their levels and gates, one thing a step in plain imperatives (the combination's two steps), the send-off, the three lesson kinds and the praise rules, what the save remembers, the per-kind cards
- [The tutorial card: look, devices, detection and test levers](game-feedback/tutorial-card.md) — where and how big the card is, PromptFont, pad prompts, what each lesson's detection reads, camera takeovers, the `tutorial`, `tutorial=demo`, `settings` and `pad` arguments
- [The pad: rumble and haptics](game-feedback/pad-rumble.md) — how the pad answers a shot: the four-motor mixer, what triggers each pulse, why the trigger motors need Windows.Gaming.Input, what gates the output (active window, hand on the pad), and what the owner has and has not felt
- [The ambience](game-feedback/ambience.md) — the per-scene noise beds: filtered noise never a tone, sealed loops, baking a bed when its scene is first named, the crossfade, and why the stereo is decorrelation and not panning
- [The game's music: recordings, families and playback](game-feedback/music.md) — generated recordings replace the procedural score (#443): `GameMusic`, fades and arrivals, Ogg decoding traps, loudness and the mix constants, families and pinned recordings, the pause's own loop, the intensity drums layer, yielding to About
- [Making the recordings](game-feedback/music-generation.md) — the ACE-Step pipeline, the brief that worked, how a family's offenders are named and a candidate chosen, how a loop is cut and checked, and the exact `MusicBake` usage line and exit codes
- [The procedural score and the About page's player](game-feedback/procedural-music.md) — the five authored themes and the menu loop that now play on About, the streaming player, the rules of the scores with their traps (Nocturne's silent voices, Mural's rejection), the rotation, and the baked fanfares
- [The end of a level goes out of focus](game-feedback/level-end-defocus.md) — why the result page does not scrim the frame, the delayed defocus and its timing trap, the outlined text (`ShadowedLabel`), the pause's blur and the overlay layer that softens the HUD, precise aim's periphery blur
- [The drop cinematic](game-feedback/drop-cinematic.md) — when it fires (the record rule, the clearing shot, the Drop camera setting), the camera's pose/blend/focus rules, the clock (ordinary game time, no slow motion) and render interpolation, where the lens can go per scene kind, skip rules and the skip hint
- [The chapter intro](game-feedback/chapter-intro.md) — the trigger and once-per-run rule, the `[intro]` line, replaying a tour from the scene menu (`tour`), the four-key polar shot, handing control back (release tau, skip, HUD hidden over the tour)
- [Chapter intro prologues](game-feedback/chapter-intro-prologues.md) — the cut-together prologue mechanism, the cities' four shots, the volcano's and the aurora's, the smoothness fixes for terrain-hugging shots
- [Chapter intro: the other scenes' shots](game-feedback/chapter-intro-scene-shots.md) — how paths stay clear of ground and solids, and each remaining scene's shots with their traps

## Where the old sections went

Code comments cite sections of this document by title; this table resolves them. The full text before the diet (#839) is `git show f1183681:docs/game-feedback.md`.

| Old section | Now in |
|---|---|
| The ripple: the cluster answers a landing (Game) | [The ripple](game-feedback/ripple.md) |
| Losing on the line is a moment now (#434) | [Losing on the line](game-feedback/line-loss.md) |
| The result page looks up at the fireworks (#430) | [The result page looks up at the fireworks](game-feedback/result-page-glance.md) |
| Dead weight: a released ball that never fell (Game, #342) | [Dead weight](game-feedback/dead-weight.md) |
| A bomb goes off (Game, #389) | [A bomb goes off](game-feedback/bomb-blast.md) |
| The ceiling announces itself (Game) | [The ceiling and the floor alarms](game-feedback/ceiling-and-floor-alarms.md) |
| The floor alarm: the laser grid (Game) | [The ceiling and the floor alarms](game-feedback/ceiling-and-floor-alarms.md) |
| The sound (Game) | [The sound effects](game-feedback/sound-effects.md), [placement, voices and the mix](game-feedback/sound-placement-and-mix.md) |
| The easter eggs: the sleeping gun, the secret shot code, the Konami code, the birthday, the code word, the impossible shot (Game, #230) | [The easter eggs](game-feedback/easter-eggs.md) |
| The victory fireworks (Game) | [The victory fireworks](game-feedback/victory-fireworks.md) |
| The colour came back (#612) | [The victory fireworks](game-feedback/victory-fireworks.md) |
| The trails (#612's reference pass) | [The victory fireworks](game-feedback/victory-fireworks.md) |
| Finishing a block of levels is its own moment (Game, #184) | [Milestones](game-feedback/milestone-endings.md) |
| The campaign ends in confetti (Game, #215) | [Milestones](game-feedback/milestone-endings.md) |
| The cup the player just won (Game, #183) | [The cup the player just won](game-feedback/trophy-cup.md) |
| The in-play HUD (Game) | [The in-play HUD](game-feedback/hud.md); the strip, chips and muzzle bullets in [The magazine strip, the chips and the muzzle's marks](game-feedback/hud-magazine-and-muzzle.md); the ceiling-warning bullets in [The ceiling warning: the cluster profile](game-feedback/hud-cluster-profile.md) |
| The tutorial (Game, #189) | [The tutorial: lessons, ladder and save](game-feedback/tutorial.md) and [The tutorial card: look, devices, detection and test levers](game-feedback/tutorial-card.md) |
| The pad (Game, #378) | [The pad: rumble and haptics](game-feedback/pad-rumble.md) |
| The ambience (Game) | [The ambience](game-feedback/ambience.md) |
| The music (Game) | [The game's music: recordings, families and playback](game-feedback/music.md); [Making the recordings](game-feedback/music-generation.md); [The procedural score and the About page's player](game-feedback/procedural-music.md) |
| The About page's player (#443) | [procedural-music.md](game-feedback/procedural-music.md) |
| Pulse was reworked in #186; The second theme: Bohemia (#120); The third theme: Nocturne (#162); The fourth theme: Mural (#264, replacing #164's Dechovka; recomposed in #346); The fifth theme: Ember (#163) | [procedural-music.md](game-feedback/procedural-music.md) |
| The rotation | [procedural-music.md](game-feedback/procedural-music.md) (recordings: "Families rotate" in [music.md](game-feedback/music.md)) |
| The victory fanfare was tightened in #185; The defeat fanfare was shortened in #190; The fanfares | [procedural-music.md](game-feedback/procedural-music.md), "The fanfares" |
| The end of a level goes out of focus (Game, #178) | [The end of a level goes out of focus](game-feedback/level-end-defocus.md) |
| The drop cinematic (Game) | [The drop cinematic](game-feedback/drop-cinematic.md) |
| The chapter intro (Game, #267) | [The chapter intro](game-feedback/chapter-intro.md) |
