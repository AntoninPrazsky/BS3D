# The music bakery (Tools/MusicBake)

Part of [Formats and tools](../formats-and-tools.md).

`Tools/MusicBake` renders the six procedural pieces to .wav and measures them, and builds the shipped music (`Game/Music/*.ogg`), sound effects (`Game/Sfx/*.ogg`) and drum layers (`Game/Music/Drums/*.ogg`) from generated masters (#443). Music cannot be judged from a screenshot: the numbers catch only faults that are not about taste.

```
MusicBake [--out <dir>] [--theme <name>] [--no-write]
        | --tracks [--masters <dir>] [--only <prefix>] [--quality <q>] [--no-write]
        | --shipped [--only <prefix>]
        | --sfx <wav> <name> [--music [--root <midi>] [--bpm <n>]] [--no-write]
        | --drums <wav> <recording> [--no-write]
```

Run: `dotnet run --project Tools\MusicBake -c Release -- <args>`.

## The modes

- **Default** writes the six procedural pieces (`pulse`, `bohemia`, `nocturne`, `mural`, `ember`, `menu`) as .wav to `--out` and prints the table (`--no-write`: table only; `--theme`: one piece). They are the About page's pieces ("The music (Game)" in `docs/game-feedback.md`).
- **`--tracks`** reads masters in `Research/AI-Music/` (plus `--masters <dir>`, where generated families' masters live outside the repository) that are tracks **by name**: `theme-<track>.wav` becomes `Game/Music/<track>.ogg`, `menu-loop-v2.wav` `menu.ogg`, `pause-loop.wav` `pause.ogg`; anything else is a reference. **Adding a track is a master named `theme-<track>.wav`**: the game groups `Game/Music/*.ogg` into families by the name before the first dash, with no list.
  - Loudness `THEME_RMS_DB` (−15.0 dBFS RMS; menu and pause `MENU_RMS_DB`, −19.5); samples past −1 dBFS pass a **memoryless** tanh knee (so a sample-exact loop stays seamless); Ogg Vorbis at the master's rate. `--quality` overrides `OGG_QUALITY` (0.59) and **refuses 0.6 and up** (the encoder's coupled high residue copies the low one: median SNR 9 dB against libvorbis's 32 at 0.8). The Ogg serial derives from the track's name (an unchanged rebake is byte-identical).
  - **A recording's committed sidecar may name a trim**, `"bake": { "trim_db": -1.5, "why": "…" }` in `Research/AI-Music/theme-<track>.json`: a cut of at most 6 dB under the loudness above (anything else is reported and ignored). It is for a master so peaky that the themes' loudness lifts its peaks over full scale, where the knee can only press them into runs at the top of its curve and the decode overshoots further. Tell that apart from a render that clipped by itself: the master's peak has no flat top. `mural-mbira` (crest factor 16.2 dB) decoded to 14 runs of 3+ samples at 0.98 of full scale. Trimmed 1.0 dB it decoded to 5, trimmed 1.5 dB to none, at −15.1 LUFS. The regenerated `mural-jive` (crest factor 16.8 dB) went from 40 to 1 with the same 1.5 dB, at −15.6 LUFS (#706). The trim lives in the sidecar, so a regenerated recording starts without it.
- **`--shipped`** prints the table for the shipped tracks decoded through `OggTrack` (needs no masters).
- **`--sfx <wav> <name>`** (#482) writes `Game/Sfx/<name>.ogg` from a 32-bit float or 16-bit stereo 44.1 kHz render (else refused): mixed to mono (`Apply3D` takes mono), peak-normalised to 0.9, encoded as identical stereo channels (the encoder and `OggTrack.Decode` are stereo-only) and decoded back to prove the frame count. `--music` (the fanfare) keeps stereo and writes estimated `ROOT`/`BPM` Vorbis comments, which nothing reads since #613. "The sound" in `docs/game-feedback.md`.
- **`--drums <wav> <recording>`** (#495) writes `Game/Music/Drums/<recording>.ogg` from the stem `C:/Users/panrd/AI/stems/separate_drums.py` separated out of the *shipped* recording, which must be **exactly its length at 48 kHz** or is refused. Not normalised (its level is its place in the mix), stored at `DrumLayer.STORED_SCALE`. **A layer whose calm mix (`DrumLayer.CALM_GAIN`) reaches full scale is not written and the tool exits 1** (judged in memory, so a failed rebake never overwrites a good layer).

## Checks and exit codes

- **Every encoded track is decoded again through the game's `OggTrack`**; a second line gives frame count against the master, head and tail offset (a second of each within ±2048 frames) and coding noise at the loop ends. **Exit 1 if a length or either end is out** (the loop would jump or gap at every repeat). It has caught OggVorbisEncoder 1.2.2 dropping each track's first 1024 frames (`EncodeOgg` writes half a block first) and NVorbis 0.10.5 decoding Ember 80 frames long (`OggTrack` stops at the stream's count).
- **A recording may not fall silent longer than `QUIET_LIMIT_SECONDS` (6 s)** (#772): `LongestQuiet` walks quarter-second windows of the mono fold **twice round the loop** under `QUIET_DBFS` (−35). Over it `--tracks` does not write the master and `--shipped` fails it, `REFUSED`, exit 1.
- **The loudness meter is held to its standard before every table (#467).** The `lufs` column is ITU-R BS.1770-4 integrated loudness, beside RMS because the two disagree on bass-heavy material. **`CheckMeter`** tests the 48 kHz coefficients against the standard and five EBU Tech 3341 signals at both rates within 0.1 LU; a meter off it exits 5 **without printing a row**.
- **Exit codes**: 0 whatever the music measures (not a gate on the music); 1 a length or end out, a refused silence or drums layer; 2 usage; 3 a stale limiter drive (measured against `LIMITER_DRIVE`, #464); 4 a streamed render off the one-shot one by more than a step (#464); 5 the meter.

## The table, and how it is built

One row per piece: length, render time, **`entry`** (seconds where a level came in; `-` for the menu loop and generated tracks), peak and RMS dBFS, `lufs`, channel balance, the mono fold's cost, energy in six bands (under 100 Hz, 100–200, 200–500, 500–2k, 2k–6k, over 6k), RMS of the first and last half-second. **The envelope under a row** is the RMS of every two seconds in dB under the loudest two: a section 20 dB down is a hole.

It **compiles `ProceduralMusic.cs`, `MusicFade.cs`, `Spectrum.cs` and `OggTrack.cs` from `Game/Audio` into itself** and calls `ProceduralMusic.Render`, the About page's own door (a `ProjectReference` would drag WinForms, MGCB and a Windows TFM behind an audio tool). It references NVorbis (the version `Game.csproj` pins) and OggVorbisEncoder (MIT) and takes MonoGame compile-time only. A file compiled into both projects may not `cref` a Game-only type ([Checking tools](checking-tools.md)).
