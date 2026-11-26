# The ambience

Part of [The game: feedback](../game-feedback.md).

Every backdrop sounds like itself (#46): one looping bed per scene, synthesized from raw PCM in `Game/Audio/ProceduralAmbience.cs` and crossfaded when the scene changes. The host owns it beside the music and `SetScene` (the one scene writer) retargets it. It runs off the frame: the scene is on screen whether or not a session stands, so its sound is too, on the front end as in play.

**Not under the pause over a level (#1243), nor under the Jukebox (#704).** Only one music is meant to be heard there and nobody is playing: `AudioDirector` fades the bed out over `BED_QUIET_SECONDS` through `ProceduralAmbience.Duck` and drops the scene's one-shots (a thunder already on its way included), and both come back on Resume. The settings page over the pause is the exception: the theme comes back there so its rows can be set against it, and the bed with it. The owner heard the desert's wind under the pause's loop, which sits about 10 dB under a theme.

## Design rules

- **Filtered noise, never a tone.** A bed sits under the music for the whole run; anything with a pitch would fight the theme or be noticed, and a bed that is noticed is too loud. Even the neon city's hum is a band of noise around 100-150 Hz.
- **The loops are sealed by construction.** Every envelope runs a whole number of cycles per 16 s loop, and each bed renders a second past the loop point, folded onto the head under an equal-power ramp. Measured at the wrap: the step across the seam is 0.1-1.5x the bed's mean step, in **both** ears (folding one channel only opens a seam in one ear, which is harder to notice).
- **Levels are RMS targets per scene**, set at bake. The first build sat *over* the theme, so every target was cut to a quarter by ear: an ambience picked out over the music is too loud by definition. **The beds have a volume row of their own** (`master x ambience`); chaining it to the effects row would turn the shot down with it.
- **A chapter's bed sits at least `UNDER_MUSIC_DB` (18 dB) under its music in the 300 Hz-4 kHz band** (#1243), the band the owner's monitor speakers play: the bed's band level at `AMBIENCE_VOLUME` against the band median, in 400 ms windows, of the quietest track of the chapter's family at the theme level. Full-band RMS does not say it: the desert's wind has nearly all its energy in that band and the mountain's gusts and the savanna's wind do not. The desert sat 13.6 dB under `ember-stoner` and the owner heard it over the music; the nine beds nobody had picked out kept 18.3 dB or more. Measured after the cut (dB under the quietest track):

  | savanna | desert | mountain | city | volcano | circus | neon | cavern | meadow | space | aurora | dream | grid | moon |
  |---|---|---|---|---|---|---|---|---|---|---|---|---|---|
  | 18.0 | 18.1 | 18.0 | 18.0 | 18.0 | 18.3 | 19.0 | 19.6 | 19.6 | 21.9 | 23.0 | 24.6 | 28.6 | 32.5 |

  A new track that is quieter than its family's, or a new bed, is measured against it. The scenes no chapter uses (sea, forest, outback, tropical, Mars, storm, polar ice) were not cut.
- **Crackles are point events** (the savanna's campfire, the cavern's drips): each snap rolls its own side and keeps it for its tail. The fire stays **narrow**, the drips take the **widest** spread (hollow noise becomes a room with a roof).

## Baking and the crossfade

- **A bed is baked when its scene is first named, not at startup (#592).** Baking all at launch cost a 148-153 ms main-thread hitch and ~56 MB held for a session that hears one or two (desktop; the laptop is unmeasured). `SetScene` starts a bake of that scene alone on the thread pool, and only the `SoundEffect` is made on the main thread, the frame the bytes land (0.5-0.65 ms).
- One bake at a time (a scene page clicked through bakes the scene it ends on). Beds sit in a cache of `CACHED_BEDS` (sounding, fading out, just left), evicting the least recently used that is not sounding. **Until the new bed lands the old one plays on.** Each bake logs `[audio] ambience bed <Scene> baked ...`.
- **The crossfade is 1.5 s, equal-power, with only two instances ever live**, one made per arrival and disposed once faded. Volumes are written only on frames the fade or settings moved them.

## Stereo: decorrelation, not panning (#146)

- **The trap: panning noise does not widen it, it moves the point it comes from**, because two channels carrying one sequence at two levels are still one source. Each layer draws a **shared** sequence and one **per side**, mixed constant-power (`left = shared*cos(theta) + uniqueL*sin(theta)`, `theta = width*pi/2`). Correlation is exactly `cos^2(theta)` and each channel's power stays 1 at every width, so **width is a dial on the image alone, not the level**. The per-side seeds are offset **+5101/+9173, not +1**, or a layer's side would be drawn from the next layer's own sequence.
- **Width is per layer and per scene.** Low bands stay nearer the middle (little direction in them, and the most cost on a mono downmix). Across beds it is a judgement about the place: mountains and forest canopy fully decorrelated, the sea keeping a middle for the swell, the cities holding the rumble near the centre. **Space and the Moon stay nearly a point on purpose** (an enveloping void is a contradiction), and so does the `default` arm, meant to be conspicuously featureless.
