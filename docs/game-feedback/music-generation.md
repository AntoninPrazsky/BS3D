# Making the recordings

Part of [The game: feedback](../game-feedback.md).

What a future music round needs: where the recordings come from, how a loop is cut, the checks, and the exact `MusicBake` commands. Playback is [The game's music](music.md).

## Where they come from

- **ACE-Step 1.5 through acestep.cpp v0.0.5** on the desktop's RX 6900 XT via Vulkan, each prompted from the description of the composition in its slot (the `MusicTheme` docs). Scripts in `Tools/Generation` (README there): `generate-music.ps1` makes one render (`-Loop` cuts a whole-bar loop with `loop_crossfade.py`, which keeps the untouched render as `<name>.raw.wav`; `-NoCotCaption` stops the language model rewriting the brief; `BS3D_AI_ROOT` points at the engine and models).
- The float masters of the first eleven are in `Research/AI-Music/`, each with a `.json` sidecar holding **the exact prompt, what the language model rewrote it into, the loop cut and its check**: that is where a brief that worked is kept, and the next family's brief starts from the nearest one. Later families' masters (#486) live **outside the repository** (`MusicBake --tracks --masters <dir>`); the `.ogg` is the artefact.
- **A brief that worked, for the pause loop (#668):** nine renders over three briefs (bossa nova, vibraphone and air, chillhop; `generate-music.ps1 -Loop -NoCotCaption`, 120 s, three seeds each), picked by seam: context match 0.949, crossfade rhythm r = 0.99, spectrum cosine 1.00 (`pause-vibes-12`, still awaiting the owner's ear).

## Naming a family's offenders

**Which recordings of a family offend is decided by measurement against the desert's `ember` family** (the owner, 2026-10-11): tunelessness, the low-mid share, energy above 6 kHz. `Tools/Generation/measure-music.py <files> --reference ember` measures loops or candidate masters and bolds every figure outside ember's range (band shares, the tonal share and flatness of 300 Hz-2 kHz, held-note changes, tempo wander, clipped runs, pitch wobble and detune). Measure ember in the same run: the tune columns are only comparable within one. The tables it gave are on #841 (magma), #824 (pulse) and #706 (mural).

- ⚠ **"Drunk" was not in the pitch or the timing**: no mural recording wobbles or drifts more than an ember one, while a synthetic ±25-cent wow moves wobble and detune three to seven times. What the owner hears there is timbre or playing, which only the ear names; a note sent from the pause names the recording (#824).

## Cutting a loop

- **Cut a loop out of the render's body, never from its own ends.** A generated piece *ends* (a fade or a final hit into silence), so folding the tail onto the head was sample-seamless and audibly an ending followed by a beginning: the level fell from 1.0 to 0.04 of the median across one wrap.
- Loops are **whole bars**, aligned to the millisecond on the transient envelope and crossfaded over about two beats. Every render came back within 1 % of the asked tempo but one (asked 180, rendered ~96 felt at ~192, cut on the half-time grid), so measure the tempo.
- **The check** is the rhythm correlation of the two streams the crossfade mixes against the track's own passages a bar apart. Every loop keeps time by it except **Bohemia** (0.43 against 0.53), whose render changes rhythm near 76 s and so yields only 42 s. The `menu` loop predates the method and dips to a tenth of its median at the wrap; the owner heard it and kept it.

## `MusicBake`

The usage line (`Tools/MusicBake/Program.cs`; every argument is read before anything runs):

```
MusicBake [--out <dir>] [--theme <name>] [--no-write]
        | --tracks [--masters <dir>] [--only <prefix>] [--quality <-0.1..0.59>] [--no-write]
        | --shipped [--only <prefix>]
        | --sfx <wav> <name> [--music [--root <midi>] [--bpm <n>]] [--no-write]
        | --drums <wav> <recording> [--no-write]
```

- No mode renders the procedural compositions to `.wav`. `--tracks` encodes masters to `Game/Music` Ogg and checks each through `OggTrack`; `--shipped` measures shipped tracks through the game's decoder; `--sfx` writes a sound effect ([The sound](sound-effects.md)); `--drums` writes a drums layer.
- Exit codes: 2 usage; 3 a stored `LIMITER_DRIVE` is stale; 4 a streamed render differs from the one-shot by more than a step; 5 the loudness meter is off its standard. See "The music bakery" in `docs/formats-and-tools.md`.
