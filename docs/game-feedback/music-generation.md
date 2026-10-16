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

## Choosing a candidate

- Render **three candidates of a brief** and measure them in the same `measure-music.py` run as ember. The band shares do not choose by themselves. Two faults they miss:
  - **the step across the wrap**: `magma-doom` c3 sat inside ember's range on every band and jumped +15.4 dB at its seam (p99 of its own steps);
  - **a hole**: a long stretch 10 dB or more under the loop's loud level. `magma-electro` c4 spent 24 s of its 43 s at −9 to −16 dB, the fault the old `doom` was replaced for.

  The loop cut's own check must also say "in time". Two more faults ruled candidates out in #732:
  - **a dropout**: two seconds more than 20 dB under the loop's loud level (its p95 1 s RMS). No shipped `ember`, `magma` or first-generation `puppet` loop dips more than 15.8 dB; `puppet-waltz` c6 dipped 45 dB with only 12 % of its loop under −10 dB, so the hole share alone passed it;
  - **a seam whose two sides disagree**: the cut's spectrum cosine under 0.3 (`puppet-fantasy` c1 printed −0.03). The cut's log prints negative r and cosine values, so a parser must keep the sign.

  A large wrap step over 250 ms can be one drum hit, so measure it over 1 s and 2 s too: `mural-desertblues` ships with −9.0 dB (p94) over 250 ms, +0.9 dB over 1 s and −1.0 dB over 2 s (#706).
- **Ask the groove to play throughout.** "A hushed breakdown", "a secretive opening" and "a slow build from a hushed spell" put 19–38 % of the loop under −10 dB in 13 of 30 first-round renders of a mysterious brief (#732). "Playing throughout", "the full ensemble from the first bar" and "quiet breakdown, near silence, long soft passages, fade in" in the negative left aurora, theatre, oud and trip-hop at least two loops of three without a hole in the next round.
- Brief the drums as **"kick, snare and toms with a dark ride cymbal"**, with bright crash cymbals, open hi-hats, tambourine and shakers in the negative: it brought two of three drum and bass renders to 2.0 % above 6 kHz or under, where the first three sat at 2.5–3.5 % (#841).
- ⚠ **Some styles keep their treble whatever the brief says**: breakbeat rock stayed at 2.1–5.1 % above 6 kHz over nine renders and electro at 2.5–4.6 %, the synth lead made "warm" instead of "bright" included. Township jive stayed at 2.3–4.5 % in seven of eight renders (#706), a mellow round guitar tone, soft highs, low hand drums and "jangly guitar, crisp hi-hats, bells" in the negative included. When three rounds do not clear ember's line, ship the best and name the figure still outside it in the sidecar's `chosen.why`, as `theme-magma-breakrock.json` and `theme-magma-electro.json` do.
- ⚠ **And some keep their dynamics or their tune figure** (#732): an orchestral waltz left a hole or a dropout in six renders of six and a cinematic orchestra in seven of nine, "playing throughout" and a pop beat under it included; the one orchestra that held its level kept loose time (tempo wander 2.66 %, ember at most 0.29 %) and ships with that named in `theme-puppet-fantasy.json`. A waltz carried by a gypsy-jazz rhythm guitar and walking bass held its level. Trip-hop stayed under ember's 2.74 held-note changes a second in all nine renders (0.9–2.73), a long flowing melody asked included.
- The sidecar of a regenerated recording keeps the old one under `replaces` (its brief, its loop cut and why it went) and the losing candidates' seeds under `alternatives`, so a listener who prefers another candidate can name it and it can be cut from its master again.

## Cutting a loop

- **Cut a loop out of the render's body, never from its own ends.** A generated piece *ends* (a fade or a final hit into silence), so folding the tail onto the head was sample-seamless and audibly an ending followed by a beginning: the level fell from 1.0 to 0.04 of the median across one wrap.
- Loops are **whole bars**, aligned to the millisecond on the transient envelope and crossfaded over about two beats. Every render came back within 1 % of the asked tempo but one (asked 180, rendered ~96 felt at ~192, cut on the half-time grid), so measure the tempo.
- **A band that stops dead mid-render leaves a dropout in a long cut.** Cut again from the part of the render before the stop: run `loop_crossfade.py` on that stretch of the `.raw.wav`. `mural-jive` c4 stopped for two seconds 74 s in (−20 and −28 dB), so its 108 s cut failed the dropout check. Recut from its first 73.5 s, it gave a 40 s loop with context match 0.93 and shipped (#706; the sidecar's `recut` says how).
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
