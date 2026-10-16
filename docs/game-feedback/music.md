# The game's music: recordings, families and playback

Part of [The game: feedback](../game-feedback.md).

**Owner ruling (#443): generated music replaces the procedural score.** A level and the front end play generated recordings; the procedural compositions stay as the About page's player and the fanfares' fallback ([The procedural score](procedural-music.md)). How the recordings are made: [Making the recordings](music-generation.md).

## Who plays what

- `Game/Audio/GameMusic.cs` plays the loops in `Music/*.ogg` beside the executable. `ProceduralMusic.cs` keeps the scores and fanfares (owned by `GameMusic`, which forwards to it); `ProceduralJukebox.cs` renders a composition for About.
- **Families** are grouped by the name before the first dash (`ember.ogg` and `ember-punk-03.ogg` are `ember`'s); the front end's `menu` is its own.
- **The machinery (#211, #212):** the chain queues the same buffer again before the sounding pass ends, so the join costs nothing; a switch retires the chain into a fading slot (`_retiring`); the lobby loop is a framework-looped instance. Tracks are **48 kHz** stereo; fanfares and procedural pieces stay 44.1 kHz. A loop has no prelude: a level opens on it at its top.
- **The stack question:** the host plays the lobby loop exactly while no `GameplayScreen` is on the stack. Leaving to the main menu keeps the session but must not keep its music, so the host fades the theme out and re-wants it on Continue (a fresh build's `Play` is the "already sounding" no-op, so the writers cannot fight).

## Fades: a switch fades, only an ending stops dead

- Every replacement fades (theme 0.9 s, loop 0.5 s, fanfare 0.4 s, **squared**, as a multiplier beside `Gain` so a fade and a settings change cannot fight over one `Volume`). `FadeOut` is every switch's call, `Stop` the endings'. **A level's ending stops dead**, taking any retiring chain with it: the silence is the message, the fireworks land in it, and a dance track over a loss is the wrong feeling.
- **The arriving side fades too (#456).** `MusicFade.Arrive(seconds)` starts an instance silent and walks it to full (`_themeFade` beside `_retiringFade`), called where each side actually *starts sounding* (`Advance()`'s fresh chain; the `Play()` in `PlayMenu()` or `Update()`'s load completion), so a slow load cannot finish the ramp unheard. `THEME_ARRIVAL_SECONDS` and `MENU_ARRIVAL_SECONDS` are starting points; the ear decides. The feed's resubmit path never touches `_themeFade`, so a loop's wrap stays seamless.

## Ogg files (#444)

- `Game/Audio/OggTrack.cs` decodes through NVorbis 0.10.5 at load into the 16-bit PCM the chain submits; `Game.csproj` copies `Music\*.ogg`. **Not MGCB content:** the chain needs the samples back, which a pipeline `SoundEffect` does not give, and `Song`/`MediaPlayer` cannot be fed buffers. Anything but 48 kHz stereo is refused with a log line; a slot with no readable file plays silence.
- **The length is the loop, and it came out wrong twice** (caught by `MusicBake --tracks`, which decodes each encode through `OggTrack` and compares both ends to the master): OggVorbisEncoder 1.2.2 overwrote each track's first 1024 frames (the tool now writes half a block first), and NVorbis returned 80 frames too many on Ember (`OggTrack` stops at the stream's own count).
- **`OGG_QUALITY` (0.59) is where the encoder stops being right**: from 0.6 OggVorbisEncoder 1.2.2 uses a coupled high residue with a copy of the low table (upstream PR #24), costing 23 dB of SNR under 2 kHz at 0.8. The tool refuses 0.6 and above.

## Loudness and the mix

- MusicBake brings each track to `THEME_RMS_DB` (menu `MENU_RMS_DB`, pause `PAUSE_RMS_DB`) and bends peaks along a **memoryless** tanh knee from -1 dBFS, so a loop's seam stays seamless.
- **The music sits under the effects because of the mix constants, not the bake.** The owner heard it quieter at all-100 % rows (the rows only attenuate), and `MUSIC_VOLUME`/`MENU_VOLUME` were raised 3.4 dB (#467). K-weighted loudness (ITU-R BS.1770-4) showed generated and procedural themes are one loudness at one RMS (procedural -12.8 to -13.6 LUFS, generated median -13.7; 2026-09-23).
- **What it did find is a 3.8 LU spread within the recordings**, so a chapter's rotation can step 2.5 LU between levels. A LUFS-target re-bake would close it (masters live on the desktop); not done. `MusicBake --shipped` measures every shipped track through the game's decoder.

## Families rotate; a level can pin a recording (#486)

- A family's recordings are its bare file, then `name-*.ogg` in name order. A family with more than one moves to the next **every time a chain head is built** (a level opening, a retry, a switch of piece, a Continue). The counter is the session's, so a fresh launch starts at the bare file.
- A level's `music` string names a family (`ember`) or pins one recording by stem (`ember-punk-03`); a pinned name not on disk falls back to the family, logged. Adding a recording is dropping a file; a **family** is files under a new name plus naming it in `Tools/LevelGen`. `GameMusic.Families` lists what is on disk; `MusicTheme` is About's catalogue only. Each head logs `[music] ember: ember-punk-02.ogg` beside `[levels] Loaded`.
- **A recording is decoded when its family is first asked for, not at the splash** (a hundred loops would be a gigabyte of PCM). A family holds the recording playing and the next, and lets go when another family takes over. The first level opens its theme a fraction late, under the chapter intro; a track still decoding leaves the chain unbuilt and retried every frame.

## The pause has its own music (#668)

`GameMusic.Pausing` plays `Music/pause.ogg` at `PAUSE_VOLUME` (= `MENU_VOLUME`). The theme is **faded (0.5 s), never stopped** and returns exactly where it stands. It is asked every frame from the stack, like `Yielding`, so every way out of the pause reaches it. **The settings page opened from the pause lets the theme back** (its Music row must be heard against it). `pause-vibes-12` awaits the owner's ear; a missing file leaves the theme playing.

## The music tightens as the cluster nears the line (#495)

- A recording with a **drums layer** (`Music/Drums/<recording>.ogg`, a folder the families never count) plays as `full - (1 - g) * drums` (`DrumLayer.Mix`): at `g = 1` the recording as it was, below it its drums turned down to `DrumLayer.CALM_GAIN` (not silent: a groove with no drums at all is a different piece). `g` follows `GameMusic.Intensity`, set every frame from the cluster's lowest ball (`MusicIntensity`: 0 at `MUSIC_CALM_STEPS` ceiling steps or more above the line, rising evenly to 1 on it), at 0.5 of full a second up and 0.2 down.
- **The layer is separated, not generated**: two ACE-Step renders can never be cross-faded (other tempos; unequal lengths). Demucs (htdemucs, CPU: `C:/Users/panrd/AI/stems/separate_drums.py`, `batch_drums.py`) takes the drums out of the *shipped* loop, three copies end to end with the middle kept so the seam is separated with both sides heard. `preview_layer.py` renders calm, full and swept for listening.
- **Played in chunks** (#212): `LAYER_CHUNK_FRAMES` mixed at a time into one scratch buffer, gain ramped across each chunk, `LAYER_CHUNKS_AHEAD` queued (two seconds a stall cannot starve), so a change is heard up to two seconds late. The layer is stored at half level (`DrumLayer.STORED_SCALE`; a stem can peak over full scale where its mix does not).
- **`MusicBake --drums <wav> <recording>` refuses any layer whose calm mix would reach full scale** (turned down, that recording would clip); a layer is kept only where the drums stand within 18 dB of the recording. 123 of the 138 recordings carry one (the owner's OK came on the meadow, 2026-10-02); the rest, whose drums stand further down or would clip when calm, play as before. The layers add ~160 MB, inside the 1 GB budget. Checks: `[music] bloom: bloom.ogg (drums layered)`; `DrumLayerTests`.

## Stepping aside for About

`GameMusic.Yielding` (0.7 s each way): everything `GameMusic` plays fades out while the About player holds a piece and returns where it was. The fanfare is left alone.
