# Generation tooling

The scripts that made the game's recordings (#443, #482, #486, #446). They run on the owner's desktop against models
kept **outside** the repository; nothing they produce is used until it has been chosen by ear and written into
`Game/Music` or `Game/Sfx` with `Tools/MusicBake`.

| File | What it does |
|---|---|
| `generate-music.ps1` | One ACE-Step 1.5 render through `acestep.cpp` (`ace-lm`/`ace-synth`, Vulkan). `-Loop` cuts a whole-bar loop with `loop_crossfade.py`. Set `BS3D_AI_ROOT` to the folder holding `ComfyUI\` (the engine and models) and `output\`. |
| `loop_crossfade.py` | Cuts a seamless whole-bar loop out of a render's body; the untouched render stays beside it as `<name>.raw.wav`. |
| `generate-sfx.py` | Stable Audio 3 sound-effect renders (CPU) from a prompt list; each `<name>-<seed>.wav` has a `.txt` sidecar with everything that made it. Run it from a Python venv that has `stable_audio_3`. |
| `measure-music.py` | Measures loops or candidate masters against a reference family (`--reference ember`) and bolds what falls outside its range: band shares, the tune proxies, tempo wander, clipping, pitch wobble, and in a second table the short gaps, how long the opening repeats and the held notes out of the sidecar's key. How a family's offenders are named (#841, #824, #706, #913). numpy; `.ogg` needs soundfile, `.wav` does not. |
| `drum-share.py` | How loud a recording's separated drums stem stands against its mix in 300 Hz-4 kHz (level, loud 250 ms, crest, share of windows within 6 dB): from a shipped layer in `Music/Drums`, or `--pairs` a candidate loop and the stem `C:/Users/panrd/AI/stems/separate_drums.py` made of it. The column that names "blows and pounding" (#905, #906). numpy and soundfile. |
| `vocal-share.py` | How loud a loop's separated VOICE stands against its mix in 300 Hz-4 kHz (level, loud 250 ms, share of windows within 10 dB), Demucs on the CPU. Fires on chopped wordless voices and not on instrumentals; blind to a vocoder (#916). Demucs venv. |
| `key-share.py` | The share of a loop's pitched energy (every note, 100 Hz-2.5 kHz, harmonic part) on the asked key's scale and on the best-fitting major scale; `--shift` varispeed-shifts a stretch to show the column fires. Where `measure-music.py` counts only the dominant pitch (#905). numpy, scipy and soundfile. |
| `sfx-body.py` | Mixes a warm sine body an octave under a render's first note, with short fades, before `MusicBake --sfx`: how `ceiling-feed.ogg` got the body its render lacked (#726). numpy and soundfile, which the `generate-sfx.py` venv has. |
| `sfx-prompts/` | The prompt lists of every batch so far — the record of what was asked for. |

Sound path: render, pick by ear, then `dotnet run --project Tools\MusicBake -c Release -- --sfx <wav> <name>` (add
`--music` to keep stereo). See "The sound" in `docs/game-feedback.md` and the `local-ai` skill.
