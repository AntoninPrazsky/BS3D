# Sound: placement, voices and the mix

Part of [The game: feedback](../game-feedback.md).

## Placed by the hardware, attenuated by the game (#75)

**Every play takes a small random pitch, and every sound that has a place in the world is placed there by the hardware** through `AudioEmitter`/`AudioListener`/`Apply3D` (`Speak`). The shot leaves the muzzle, a landing sounds at the cell it stuck to, a release at the cell that broke, a shell whistles and bursts where it is, and the ice break, the blasts, the wormhole, the eruption, the Cut and Swap, the ceiling step (at the plate) and the line's cut and touch sound where they happen. The party popper, the thunder and the dry cues (the three UI sounds, the refused shot, the aim stop, the streak break, the star, the new best) have no emitting object and never reach an emitter.

- **The hardware gives direction and the game gives distance; that split is the whole design.** X3DAudio's own `1/d` curve would put a landing 28 units out 29 dB down, and XNA's `AudioEmitter` exposes no custom curve. So that curve is switched off: `SoundEffect.DistanceScale` is set once to 10 000, where the output matrix sums to 1 from 0 to 500 units in every direction, in stereo and 5.1 alike. Every distance law in the game (a landing attenuates to a floor, never to silence) stays the game's own. ⚠ Never vary `DistanceScale` per source: that puts an inverse-square curve back on top of the game's own. It is a plateau radius, and it does bite at 20 000 units.
- **On OpenAL (GamePi, #790) the placement comes after `Play`.** A DesktopGL `SoundEffectInstance` holds an OpenAL source only while it plays, so `Apply3D` before `Play` does nothing and the sound plays centred. XAudio2 needs the opposite order, because its placement writes the frequency ratio. `Speak` asks `Platform/AudioBackend.PlacesAfterPlay` (false on Windows, true in GamePi). A Pi with no sound device falls back to OpenAL Soft's null output and does not crash.

## Voices: `VoiceRing`

**`Apply3D` needs a handle and `SoundEffect.Play` is fire-and-forget, so each spatial buffer gets a `VoiceRing`**: a fixed ring of `SoundEffectInstance`s built once at load and reused (4 shot, 3 per ball type, 3 release, 4 launch, 32 burst).

- ⚠ **The framework will not bound it.** `MAX_PLAYING_INSTANCES` is `int.MaxValue` on WindowsDX, so a create-per-play design leaks a source voice per sound with no symptom.
- ⚠ **Every `Take()` stops the slot it hands back**, because `SoundEffectInstance.Play()` returns early on an instance that is already playing: a still-sounding slot would swallow the new sound.
- **The firework rings are sized by the opening barrage**, not the steady state: all 32 shells report inside one 2.6 s window. ⚠ The burst ring has no margin. A longer burst bake or a shorter shell life clips tails silently.

## The call order around `Apply3D`

Each way of getting it wrong is silent.

- `Apply3D` writes the voice's output matrix **and** its frequency ratio, so **the pitch is set after it**, or every random nudge is wiped. `Volume` is safe anywhere.
- **`Pan` writes the same matrix, so it is never touched on a placed voice.** The one deliberate use is the fallback before any listener exists.
- **`Apply3D` is re-applied on every take**: nothing resets a reused voice's matrix, so a slot replayed without it sounds at the previous event's position.
- ⚠ **A NaN reaching X3DAudio silences the whole audio graph, permanently, with no exception.** Both ends are gated on being finite: a bad camera pose leaves the last good listener standing, and a bad world point falls back to the listener's position. The listener's up vector is normalised, crossed from world up, because X3DAudio validates nothing: a wrong length skews the image, and a degenerate one collapses it to the centre.

## The image

- **The bearing is an angle**, so the sky needs no width constant. The near field keeps one exaggeration, `NEAR_WIDEN` (1.9): the truthful image of a landing is narrower than the mix has ever had, and 1 reads as the stereo collapsing.
- **Overhead is folded towards the middle**: the sideways leg is scaled by the cosine of the elevation. Stereo carries only a horizontal bearing, so without the fold a shell nearly above the lens cracks hard out of one speaker.
- **What positional audio does not buy**:
  - there is no height cue, on stereo or 5.1;
  - Doppler is refused (`DopplerScale` 0), because the ADS lean moves the lens at about 40 % of the speed of sound and would detune everything sounding;
  - `SoundEffect.SpeedOfSound` is dead code on WindowsDX;
  - surround placement is real but unverified on the owner's stereo machine.
- **The listener is the host's, posed once a frame from the one camera**, never the session's: a celebration plays over a frozen gameplay screen, and the clicks over the orbiting backdrop. Its up axis comes from world up, not from the camera's roll. Until the first pose it plays centred, and once it has been posed the latch is never cleared.

## The mix and the volume rows

- **The move to `Apply3D` cost 2.92 dB at the centre of the image**, because X3DAudio's matrix sums to 1 where the old pan law wrote both channels at full. `BASE_VOLUME` went to 1.0 to take back what can be taken back (`Volume` throws above 1). The shift depends on position, so the wide sounds gained relative to the centred ones.
- The sounds with no emitter (the popper, the thunder and the dry cues above) still use the centre-hot pan law, trimmed by `NON_SPATIAL_TRIM` (0.7).
- If the effects sit too low under the bed and the music, the fix is the other end (`AMBIENCE_VOLUME`, `MUSIC_VOLUME`, `MENU_VOLUME`, `FANFARE_VOLUME` × ~0.71). That is an ear's call and has not been made.
- **Volume is authored mix × player gain (#46).** The mix keeps its tuning in constants. The Volume, Effects, Music and Ambience rows scale it in quarters, with "Off" at zero. Each subsystem takes `master × its own row` through a `Gain` property written only by `AudioDirector.ApplyVolumes`.
  - The effects apply a change on the next play.
  - The music pushes it onto the instances already sounding.
  - The rows cycle downwards, because a click almost always means "quieter".
  - `mute` on the command line starts the master at zero, for harnesses.

## No audio device (#923)

- **`Audio/AudioDevice.Present` is asked once and is final for the run.** With every audio endpoint unplugged, disabled or asleep (a monitor's speakers over its display cable, gone with the screen) MonoGame's engine has no device, and the first streaming voice fails with a `NullReferenceException` out of `SharpDX.XAudio2.SourceVoice.CreateSourceVoice`: the engine's static device is null and nothing checks it. `Present` makes one throw-away `DynamicSoundEffectInstance` (the kind the theme needs) and, when that throws, prints the single line `[audio] no audio device: playing without sound until the game is restarted`. MonoGame builds its engine once, so a device that appears later is not picked up: a restart brings the sound back.
- **What skips from it:** `GameMusic` (set `_failed` at construction and decodes nothing; the family list is still built from the file names, the pages read it), `ProceduralMusic` (`GameMusic` calls its `Disable`) and `ProceduralAmbience` (`_failed` from the start), `RecordingPlayer.PlayAt` and `ProceduralJukebox.Start`. Each checks a bool, never a per-frame probe. The effects need no gate: `SoundEffect` instances are inert without a device (a scripted run with shots, the result page and the fireworks logged no exception). GamePi compiles the same file; OpenAL there has its own null-output fallback (above), and the probe only reports what it finds.
- A normal run does the same as before plus the probe, one voice made and disposed at start.
