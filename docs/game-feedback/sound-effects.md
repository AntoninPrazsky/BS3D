# Sound effects

Part of [The game: feedback](../game-feedback.md).

`Game/Audio/ProceduralAudio.cs` synthesizes the effects from raw 16-bit PCM at startup and plays them as plain `SoundEffect`s: no asset file, no `.mgcb` entry, no pipeline step. Seven of them are also generated recordings since #482 (see "The recordings" below), and the bake stays behind each as its fallback. The buffers are the host's, built in `LoadContent` and disposed in `UnloadContent`: a level is torn down and rebuilt per play, the sounds outlive it. Where a sound is placed and how loud it is: [Sound: placement, voices and the mix](sound-placement-and-mix.md).

## Rules to keep

- **Warm, bass-heavy booms; no hiss.** The owner's ruling, reached through four refused bakes of the bomb's report: pleasantness and bass, not physical credibility ("it is about the bass, not squealing debris"). No `tanh` drive (it was heard as "digital"), no high debris, no hash noise. Loudness comes from `Compress` (a slow look-ahead gain) and `Limit` (a look-ahead peak limiter), never from bending the wave.
- **A sound's identity lives in 300 Hz-4 kHz.** The owner listens on his monitor's built-in speakers, so a bass-only sound is inaudible to him: bass is weight, not identity. Judge a sound by its energy in that band (`AimStopSynthTests` does) and by ear, not by file level and not by an energy split over the whole spectrum. The first bomb bake had 89.8 % of its energy under 60 Hz, a speaker played almost none of it, and it was heard as "a mouse dropping a crystal cup".
- **Sound cannot be judged from a screenshot.** What a spectrum can say is where the energy is and how the envelope falls; whether it is right is the owner's ear.
- **The effects stay MONO.** A placed sound takes a mono source (`Apply3D`), so a stereo bake would not widen it, it would take the placement away.
- ⚠ **`SoundEffect.Play`'s pitch is an offset in OCTAVES, -1...1, where 0 is the sound as baked**, not a multiplier. `NextPitch` once returned `1 + jitter` and every sound played an octave up (thin, toy-like) and no retuning of the synthesis could have fixed it. If a procedural sound is wrong in a way the maths does not explain, check this first.
- **Nothing plays twice the same**: every play takes a small random pitch nudge. A sound's nudge is bounded by what must stay readable (the landing's, below).
- **Reports are driven by loudness, tones are peak-normalised.** The loudest sample of a report is its crack, a few milliseconds tall, so peak normalisation scales the body down to a tick with a quiet thud behind it. `Loudness(signal, targetRms, ceiling)` drives the RMS and rounds the transient through a `tanh`; the burst, the popper and the shot take it, `Normalize` stays for tones. The bomb takes neither (see its section).
- **Two noise layers need two seeds.** `Noise(i, seed)` and `MakeNoiseArray(samples, seed)` are pure functions of the sample index, so two unseeded layers are one sequence filtered twice, correlated, and sum thin instead of sounding like two things.

## How a bake is built

Not a tone but a small chain: layers of additive harmonics, low-passed noise transients and a sub-bass weight mixed on a float buffer, run through a **Schroeder reverb** (four parallel combs into two series all-passes), normalised, then quantized. Baking once at load means a play costs a buffer play.

## The sounds

### The shot

Layered like a discharge, each part in its own band: a **boom** (a 150 to 60 Hz pitch drop that crashes in 30 ms and rings, plus a rumble low-passed at 190 Hz), a **zap** (additive sawtooth sweep), a **crackle** (noise band-passed to 1.5-5 kHz under a ~45 Hz tremolo) and a muzzle **thud**. The crackle is there because that band is where the ear is most sensitive: it keeps the shot present at the volume a game is actually played at.

### The landing: one note per colour, one voice per material

- **One note per ball type**: thirteen steps up a low register, adjacent colours exactly a whole tone apart (2^(1/6) per step, 150-600 Hz), fronted by a filtered contact click over a sub thump. The step is the design constant: another type extends the ladder rather than squeezing every step below what the ear can tell apart.
- **The random nudge is bounded under half a step** (+-0.06 octave against the 1/6-octave tone): at the old +-0.1 two neighbours' wobble ranges overlapped and a low green could land under a high red. Per-play variety must never swap two colours' notes.
- **One voice per material too (#314).** A `LandedMaterial` row per `BallStyle` moves everything but the colour's note and the arena's reverb: how long it rings (wool is dead, metal sings), where its two partials sit (harmonic 2 and 3 for stone and ceramic; **inharmonic 2.76 and 5.40 for metal**, a struck bar's own, which beat against the fundamental), how much sub it has (lava a lot, the bubble little: a film around nothing has no mass), and how bright and hard the click is. A material may transpose the whole ladder (`PitchScale`) but never its **step**: the thirteen must stay countable under every material. Each row is read off that style's own doc comment on `BallStyle`, so the sound cannot drift from the look. The default style's row (`BallStyle.Beach`) is the original constants unchanged.
- **Rows are baked when a level names its material** (`PrepareLanded`, called where `GameplayScreen` states the render style), not as the whole 10 x 13 cross product at startup (130 buffers, 390 voices of which a level sounds thirteen) and not on the first landing (that would spend the synthesis inside the one frame that must not stall). `PlayLanded` still bakes an unprepared row once rather than play silence.
- A landing is shorter and duller than the shot: a ball meeting its own kind should read as solid, not explosive.

### The release

The lattice's snap, then the freed balls popping away in a quick rising run: six **band-passed noise pops, not tones**, because the theme transposes itself per pass and a melodic run in any fixed key would land wrong against half of them. The run rises and accelerates slightly (release reads as reward). Scaled by the group's size and a shade deeper as it grows, capped at 15 balls. The landing tone stays blind to what came loose and a plain attach plays only the thunk.

### Ice breaking (#329)

The first sound a `BallKind` had of its own, because a thaw changes what one ball *is* three cells from where the eye was (the bomb has one too, below).

- `PlayIceBreak`: a crack and eight short ringing shards, always **with** the release, never alone.
- **It is separated from the release by register, not by level.** The release lives at 140 Hz-2.8 kHz; the crack sits near 5 kHz and the shards at 3.4-8.6 kHz, where nothing else is, so the ear takes them apart without either being loud. Making it louder would bury the release, which is the more important (what the player did is the group; the ice is what that opened).
- Tinkles, no low end (a few grams of ice under a bass thump reads as masonry); shards are near-tones, their pitches deliberately neither a chord nor a scale (anything harmonic reads as a bell).
- One voice for the whole break however many blocks, scaled by the count (full at four): nine cracks milliseconds apart are a noise burst the ear cannot count.

### The bomb's report (#389)

`PlayBlast` speaks one report per detonation from the bomb's own position; a chain is a run of them, staggered by `Blasts`. A landing that set bombs off plays the release for its **group** alone (`OnBallLanded`): folding the victims in as well put a release the size of half the cluster under the reports.

It is **bass and nothing else**, and it is the fifth bake: an undriven sine boom (130 to 44 Hz, a soft octave), a rumble (three cascaded poles at 200 Hz), a thump, and a **whump** cascaded under 650 Hz for the body a speaker with no low end still plays, plus a short fused echo and a modest reverb; the whole signal low-passed twice at **1.4 kHz** so nothing a speaker could squeal with survives, then made as loud as it will go without bending the wave (`Compress`, `Limit`, never driven). Measured (`RenderBlast`, rendered apart from its `SoundEffect` wrapper so a scratch tool can compile `ProceduralAudio.cs` and call it): RMS 0.282 (-11.0 dBFS), crest factor 3.37, 84.7 % of the energy under 60 Hz, nothing above 2 kHz, within 3 dB of its peak for about 0.9 s. It plays at full level, near-flat with distance, and every link of a chain past the first at `BLAST_CHAIN_LEVEL`, because five full-level reports inside a third of a second sum past full scale in the mix.

**The four refused bakes are the design; each failure is a trap.**

1. A deep thump (sub at 15.5 Hz): measured loud, heard as a crystal cup. The band a speaker plays held 9.7 % of the energy.
2. A noise roar with a `tanh` drive and a stepping tail: "horribly digital, not booming". Drive harmonics, single-pole hash noise that hisses, a stepping tail and an undamped comb reverb ringing up top are each a textbook source of "digital".
3. A warm boom and nothing else, low-passed at 2.8 kHz: pleasant, and nothing in it for the ear to catch.
4. The warm boom with nine shrapnel whizzes over it: refused, "take the squealing away completely and leave only maximum bass". The earlier verdict asking for a little shrapnel is superseded.
5. Two mixing traps from that round still stand: balancing two halves by *peak* left the whizzes at 0.4 % of the energy (the high half's peak was a crack's first few samples), and a compressor follows an averaged level, so noise spikes sail through it and set the final normalisation's peak until a look-ahead `Limit` caught them.

### The gun refusing a shot, and the aim meeting its stop

- **`PlayShotRefused` (#431)** answers a trigger pulled while the aim is pressed into the elevation clamp (`Cannon.ElevationRefusesShot`). It is the one sound in the game that **falls**: every rising gesture here is a reward. A soft "bwom-bwoww": two round notes falling a minor third (220 to 208 Hz, then 185 to 165 Hz), a fundamental with two soft harmonics and a sub octave, rolled off twice at 1.4 kHz, no noise, no drive. **Dry and unplaced** like the menu's clicks (it is the player's own hand being told no), billed to the effects row at `SHOT_REFUSED_VOLUME`.
- **`PlayAimStop` (#811)** plays once as `Cannon.ElevationStrain` goes full, on the very edge the pad's knock rides (`GameplayScreen.UpdateAim`, `_aimStopArmed`, re-armed only once the strain lets go), so ear and hand agree and a hand held against the stop gets one "uh-uh", not a loop. Dry and unplaced, `AIM_STOP_VOLUME`, with the refusal's own small pitch nudge.
  - It is `AimStopSynth`, pure arithmetic in a file of its own (as `DrumLayer` is) so the tests compile the lines the game plays: two beats gliding down, the second below the first (440 to 415 Hz, then 392 to 349 Hz), a square wave's odd harmonics softened and rounded by two low-passes at 2.8 kHz, no noise, 0.34 s.
  - The first design (a stick-slip creak ending in a puff of air) read as "a croak, or a grotesque burp". The owner asked for something more technical, closer to the refusal: "'you can't go here', 'e-e'".
  - **Checked by `AimStopSynthTests`** (99.9 % of the energy at 300-4000 Hz, the second beat lower than the first, two beats with silence between them read off the envelope and not off the synth's own constants).

### A streak breaking (#845)

`PlayStreakBreak(lost)` sounds with the badge breaking on the HUD ([the streak](hud.md#the-streak)), dry and unplaced like the aim stop: it answers the readout in the corner, not a place in the scene. It is `StreakBreakSynth`, pure arithmetic in a file of its own so the tests compile the lines the game plays.

- **A run of struck notes falling**, as many as the multiplier lost (one per unit of it, not per `MultiplierStep`), down an A minor arpeggio: A5 E5 C5 A4 E4. A lost ×5 plays all five, a lost ×3 the last three, so both end on the same E4: the run is the streak falling back to ×1, and ending on the fifth leaves it unresolved. Each note glides a further 3 % down as it rings.
- **A soft mallet's tone**: the fundamental, a quiet octave and the struck bar's inharmonic 2.76 (the glassy edge of something breaking; the landing's metal row uses the same ratio), rolled off twice at 3 kHz. No noise, no drive, peak-normalised.
- **It starts 30 ms in**, so the thud of the landing that broke the streak, or the moment of the miss, is heard first. It plays at `STREAK_BREAK_VOLUME` (0.4, under the aim stop), a lost ×3 at `STREAK_BREAK_LOW_LEVEL` (0.6) of a lost cap: the cap is louder as well as longer (0.73 s heard against 0.54 s).
- **Checked by `StreakBreakSynthTests`**: 99.8 % of the energy at 300-4000 Hz and none above 5 kHz for both; every note lower than the one before, read off the signal; the same run at a fifth of its pitch puts only 1.3 % in that band and fails the threshold the real one passes, which is the band check seen to refuse a bass-only sound.

### The blue ceiling step (#726)

The glass coming down to feed a tall level, and the Brake lifting it, have a sound of their own: `Sfx/ceiling-feed.ogg`, played by `PlayCeilingStep(feed: true)` at `CEILING_FEED_LEVEL`. Where it plays, how loud, why the Brake shares it and how a pour climbs: [The ceiling announces itself](ceiling-and-floor-alarms.md#the-ceiling-announces-itself).

- **The recording** is the Stable Audio render `feed3-f-25` ("a short, warm, rising two-note chime going up, like a soft mallet on a wooden marimba", 1.0 s): a C4 (263 Hz) held for a fifth of a second, then a struck C5 (524 Hz) that rings out by about 0.6 s. A sine an octave under the first note (132 Hz, 0.15 of the render's peak, decaying 5/s) was mixed under it before `MusicBake --sfx`: the warm body the owner asked to keep for the speakers that can play one, which the render did not have. Measured as the game decodes it: 76.7 % of the energy at 300 Hz-4 kHz, 3.4 % under 200 Hz, 0.02 % above 5 kHz.
- **How it was chosen.** Twenty-nine renders in three rounds, ranked by band share, hiss, raw clipping and distance from the red step; the sidecars and the prepared file (`726-feed-final`) are in `C:\Users\panrd\AI\sfx\out`. "Swelling", "rising hum" and "servo whir" prompts came back as 1.5 s drones with 23-45 % of their energy above 5 kHz and 1-16 % of their samples clipped; **a short chime "with a quick natural decay into silence", "recorded with plenty of headroom"** came back clean and tonal with nothing clipped. "Bass marimba with a warm low body" put 100 % under 200 Hz: add a body afterwards, do not prompt for one. The marimba prompt gave the same rising octave on all five seeds.
- **The bake** (`CeilingFeedSynth`, pure arithmetic in a file of its own as `AimStopSynth` is) says the same thing: two struck notes rising a fifth (E4, then B4 0.11 s later), each sliding 3 % up as it rings, an E3 body under the first, rolled off twice at 3 kHz; 91.2 % at 300 Hz-4 kHz, 8.5 % under 200 Hz.
- **Checked by `CeilingFeedSoundTests`**, on the shipped file decoded through `OggTrack` the way `TryLoadSfx` reads it, and on the bake: the band share (75 % for the recording, which keeps a body; 80 % for the bake), no hiss, a rise (the strongest partial later in the sound above 1.2 times the first), and under 200 Hz less than a quarter of the red step's share. The band check is seen to refuse the red step's own recording (3.6 % in band) and the bake at a fifth of its pitch (0.0 %), and it refused a first bake of this sound with a heavier body (59.6 %).
- **The pour is checked by `CeilingFeedPourTests`** against `CeilingDescent`'s own release of a feed's queued steps, at 15 to 240 frames a second: eleven steps climb 0 to 7 semitones and hold there; a red step between two blue ones leaves the climb alone; a gap past the window, or a new level, starts it at the bottom; and a pause longer than the window on the wall clock, with no frame advanced, does not.
- ⚠ **A whole sound's band shares are measured unwindowed** (Parseval over the whole signal, zero-padded), never through one Hann window over all of it: the window all but drops the sound's first and last tenths, which is where a body under a first note lives. The same file measured 90.9 % in band through the window and 59.6 % without it; the red step, 94.6 % under 200 Hz through it (the figure #500 recorded) and 86.7 % without.

### The menu's clicks (#46)

`MenuButton` wraps each entry's action, so a press sounds whether it arrived through Myra's `Click` (the mouse) or the `Tag` that `ActivateNavEntry` invokes (the pad and the keys), and an entry added later cannot forget its click. One 90 ms buffer serves all three UI sounds: the press plays it straight, the focus step plays it pitched up (`PlayUiTick`; faster is also shorter and far quieter, which a held direction walking the list needs), backing out plays it pitched down. A dry "thock" (a low-passed knock over a fast 900 to 420 Hz drop) with **no reverb and no pan**: the UI lives at the player's hand. A screen change restores the focus cursor by assignment, not through `StepNavFocus`, so arriving on a page does not tick.

### The star cue (#139)

The result screen's rating lands one star at a time, each playing `PlayStarEarned`: a small struck chime (a tubular bell's near-harmonic 1:2:3 with one inharmonic 4.2 on top) over a 2 ms noise tick. It is the one UI sound that is a **tone**: a repitched click would stutter, a noise gesture has no pitch to read, and the run must *rise* so a rating is heard being counted (`STAR_FINAL_LIFT` separates a four-star clear's fourth star from a three-star clear's third).

**A fixed key and pace (#613).** The fanfare has had its moment before the stars start, so the chime is not tuned to it: `ResultPage.CHIME_TRIAD` sounds over the buffer's own A5 (A5, C#6, E6, A6, the register the bell was voiced for and the brightest a four-step run reaches inside the +-12 semitones `SoundEffect.Play` can express) at `REVEAL_STEP_SECONDS` a star. A major is also the victory recording's key, so an overlap cannot clash. `Tools/MusicBake --sfx --music` still writes the key and tempo tags; nothing reads them.

### Fireworks

The celebration's sounds are the three baked here (the launch, the burst, a party popper) and the report's roll. The shot at the victory is [described with the fireworks](../game-feedback.md); this is the sound.

- **The burst is a boom, not a crackle.** A recording whose energy sits above 2 kHz stacks thirty-two voices into a hiss wall that buries the fanfare: the "rustling at the end of a level" the owner reported *was* the bursts. The shipped `firework-burst-c-12` is 96.5 % under 200 Hz (the bake's was 94 % under 150 Hz); the rejected `a-12` measured 0.3 % under 200 Hz and 54 % at 2-10 kHz. **Bass first; a crackle, if wanted, goes in under the boom as a second layer, never as the whole report.** `BURST_VOICES` is 32 (= `Fireworks.MAX_SHELLS`). Its level is `BURST_TARGET_RMS` 0.10 with a 300 Hz fourth-order low-pass and a short fade-in (#552), measured against a replica of the display's schedule: at the bake's 0.30 the stream ran 15 LU over the theme and clipped the output.
- **The launch is a sparkler, not a siren.** `LAUNCH_LEVEL` is 0.012, `LAUNCH_VOICES` 4 and the whistle's tone is off (`LAUNCH_WHISTLE` 0, one constant from coming back): fizz alone. The reason is the opening barrage: a shell every 70 ms and a 0.55 s whistle let ten sound at once, which at the old level summed to 0.8 of a burst in the 1-2.6 kHz band the ear hears best, and the owner heard the whoosh drowning the display (#498). A slow rising tone over a second is a siren by definition, so the pitch runs 900 to 2600 Hz over 0.55 s. The shells' visual rise is 0.62-0.95 s to match: a shell still climbing a second after its hiss has finished reads as two unrelated things.
- **A vibrato is a shimmer, not a wobble**: the whistle's spin is shallow and fast (0.6 % at 16 Hz); at 7 Hz and 2.8 % the ear tracks each swing as a note ("vu-vu-vu").
- **The roll is what puts a firework over a landscape**, and a reverb cannot do it: `RollingEcho` lays down slap-backs, the report arriving again off every building and hillside. Over about 40 ms apart the ear resolves each repeat as an event (four taps at 60-85 ms made "bum-bum-bum-bum", a corridor's flutter echo), so it is nine taps from 23 ms stretching by an irrational ratio (which stops the repeats reinforcing into a pitch). It must sit UNDER the direct report (`mix` 0.42): doubling it put a second wavefront 20-60 ms behind the crack and turned a hard "BUM" into a soft "whoomph".
- The popper is deliberately close and dry where the shells are big and wet, which is what makes the shells read as distant.
- A firework's sound is **placed** by the hardware; its distance term stays near-flat, because a firework *is* far away and rolling it off like a landing makes every burst a whisper.

### The weather's one-shots (#219, #223)

The storm's **thunder** and the volcano's **boom** are baked in `ProceduralAudio` and fired by `Game/Audio/SceneEventSounds.cs`, ticked from `BS3DGame.Update` right after the bed. They are the first sounds that answer nothing the player did; the design and figures are in "The storm" in `docs/scenes.md`.

- **They ride the AMBIENCE row, not the effects row** (`ProceduralAudio.WeatherGain`, written by `AudioDirector.ApplyVolumes`): a player who turned the atmosphere down has said what they think of weather, and thunder on the effects row would turn down the gun with it.
- **The scene schedules them, not the session.** `SceneRenderer.TryGetSceneEvent` hands over the event's *index* and the second its light began, the very hash the flash is drawn from, so the sound can never name a different strike from the one on screen, and a strike seen from the pause menu is heard from it. The delay is `distance / 343` with nothing authored. Never an edge detector on brightness: it would hear one strike as four.
- **The thunder is UNPLACED and the boom is placed**, the only sounds that split that way: a strike goes off inside a deck that surrounds the arena and has no direction left after crossing it, while a crater is somewhere and saying where is half of what a boom is for.
- **Authored quiet and driven, not peak-normalised**: thunder 4.60 s at 0.132 RMS, the eruption 5.00 s at 0.144, against a report's 0.30 of the time. Weather that competes with the gun is weather turned up too far, and normalising a roll to its loudest surge would leave the rest inaudible under one.

## The recordings (#482)

Seven effects are generated recordings: the shot, a landing, a release, the firework's burst, the ceiling's red step (#500) and its blue step (#726), and the line's cut (#434). They are Stable Audio 3 Small-SFX renders the owner chose by ear (the blue step by measurement, awaiting his ear), written to `Game/Sfx/<name>.ogg` by `Tools/MusicBake --sfx <wav> <name>` (mono, peak 0.9, the tracks' encoder, decoded straight back through `OggTrack` to prove the length) and loaded by `ProceduralAudio` at startup. The pipeline (`C:\Users\panrd\AI\sfx`, outside the repository; every render's prompt and seed in a sidecar) and its scripts are in `Tools/Generation` and the `local-ai` skill.

- **A file goes through its bake's own law and voices.** A report takes the same `Loudness` its bake uses (the shot at 0.27 RMS, the burst at `BURST_TARGET_RMS`), **not** the bomb's compressor: that had left the burst 12 dB under the bake it replaced (-22.9 dBFS against -10.5) and the owner heard the launch's whoosh over it (#498). The release is peak-normalised to 0.9 as its bake is. Every one is then placed by the same `VoiceRing`s, the same distance law and pitch jitter, so nothing in the mix moved but the timbre.
- **The landing keeps its colour ladder by resampling**: the one file is played 2^((type - 7)/6) times faster per colour times the material's `PitchScale`. What a file cannot carry is #314's per-material ring, partials and sub: the trade a recording makes.
- **A missing file leaves the bake standing**, silently (a load path logs nothing), so the folder can be emptied sound by sound.
- **The fanfares are the eighth and ninth.** `Sfx/victory-fanfare.ogg` keeps its stereo image (the owner ruled the recording is the bed); the render is `fanfare-m2-12s-12`, a modern hybrid (cinematic synth horns over electronic drums, a motif that builds three bars into a drop), twelve seconds, after four rounds of the owner's ear: a 4 s orchestral brass was "really very short", a 12 s brass "strange, a little funereal". It carries `ROOT` and `BPM` Vorbis comments the tool measures (a Krumhansl-profile match for the key, an autocorrelation of onset flux for the tempo; `--root`/`--bpm` override an estimate the ear disagrees with). The defeat (#446) is `Sfx/defeat-fanfare.ogg`, the render `defeat-a-6s-21` (a descending minor synth line over a soft kick and electric piano, six seconds). `GameMusic` hands each to `ProceduralMusic`, which plays it through the path the bake takes (a completed task, the same realisation, fades and ducking); the bake is the fallback when the file is missing. What a recording gives up is the bake's growth with the score. See [The procedural score](procedural-music.md#the-fanfares).
- **What the prompts taught:** "soft, gentle, clean" came back "dull, weak and boring"; "punchy, satisfying, arcade, big" is the register for everything but impacts; an impact has to be told its volume in words ("moderate volume", negative prompt "loud, aggressive") or the model saturates (8 of 9 attach renders clipped); balloons render harsh whatever the numbers say.
