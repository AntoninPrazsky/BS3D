# The ambience

Part of [The game: feedback](../game-feedback.md).

Every backdrop sounds like itself (#46): one looping bed per scene, synthesized from raw PCM in `Game/Audio/ProceduralAmbience.cs` and crossfaded when the scene changes. The host owns it beside the music and `SetScene` (the one scene writer) retargets it. It runs off the frame: the scene is on screen whether or not a session stands, so its sound is too, **pause included**.

## Design rules

- **Filtered noise, never a tone.** A bed sits under the music for the whole run; anything with a pitch would fight the theme or be noticed, and a bed that is noticed is too loud. Even the neon city's hum is a band of noise around 100-150 Hz.
- **The loops are sealed by construction.** Every envelope runs a whole number of cycles per 16 s loop, and each bed renders a second past the loop point, folded onto the head under an equal-power ramp. Measured at the wrap: the step across the seam is 0.1-1.5x the bed's mean step, in **both** ears (folding one channel only opens a seam in one ear, which is harder to notice).
- **Levels are RMS targets per scene**, set at bake. The first build sat *over* the theme, so every target was cut to a quarter by ear: an ambience picked out over the music is too loud by definition. **The beds have a volume row of their own** (`master x ambience`); chaining it to the effects row would turn the shot down with it.
- **Crackles are point events** (the savanna's campfire, the cavern's drips): each snap rolls its own side and keeps it for its tail. The fire stays **narrow**, the drips take the **widest** spread (hollow noise becomes a room with a roof).

## Baking and the crossfade

- **A bed is baked when its scene is first named, not at startup (#592).** Baking all at launch cost a 148-153 ms main-thread hitch and ~56 MB held for a session that hears one or two (desktop; the laptop is unmeasured). `SetScene` starts a bake of that scene alone on the thread pool, and only the `SoundEffect` is made on the main thread, the frame the bytes land (0.5-0.65 ms).
- One bake at a time (a scene page clicked through bakes the scene it ends on). Beds sit in a cache of `CACHED_BEDS` (sounding, fading out, just left), evicting the least recently used that is not sounding. **Until the new bed lands the old one plays on.** Each bake logs `[audio] ambience bed <Scene> baked ...`.
- **The crossfade is 1.5 s, equal-power, with only two instances ever live**, one made per arrival and disposed once faded. Volumes are written only on frames the fade or settings moved them.

## Stereo: decorrelation, not panning (#146)

- **The trap: panning noise does not widen it, it moves the point it comes from**, because two channels carrying one sequence at two levels are still one source. Each layer draws a **shared** sequence and one **per side**, mixed constant-power (`left = shared*cos(theta) + uniqueL*sin(theta)`, `theta = width*pi/2`). Correlation is exactly `cos^2(theta)` and each channel's power stays 1 at every width, so **width is a dial on the image alone, not the level**. The per-side seeds are offset **+5101/+9173, not +1**, or a layer's side would be drawn from the next layer's own sequence.
- **Width is per layer and per scene.** Low bands stay nearer the middle (little direction in them, and the most cost on a mono downmix). Across beds it is a judgement about the place: mountains and forest canopy fully decorrelated, the sea keeping a middle for the swell, the cities holding the rumble near the centre. **Space and the Moon stay nearly a point on purpose** (an enveloping void is a contradiction), and so does the `default` arm, meant to be conspicuously featureless.
