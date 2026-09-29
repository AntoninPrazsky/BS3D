# Generation tooling

The scripts that made the game's recordings (#443, #482, #486, #446). They run on the owner's desktop against models
kept **outside** the repository; nothing they produce is used until it has been chosen by ear and written into
`Game/Music` or `Game/Sfx` with `Tools/MusicBake`.

| File | What it does |
|---|---|
| `generate-music.ps1` | One ACE-Step 1.5 render through `acestep.cpp` (`ace-lm`/`ace-synth`, Vulkan). `-Loop` cuts a whole-bar loop with `loop_crossfade.py`. Set `BS3D_AI_ROOT` to the folder holding `ComfyUI\` (the engine and models) and `output\`. |
| `loop_crossfade.py` | Cuts a seamless whole-bar loop out of a render's body; the untouched render stays beside it as `<name>.raw.wav`. |
| `generate-sfx.py` | Stable Audio 3 sound-effect renders (CPU) from a prompt list; each `<name>-<seed>.wav` has a `.txt` sidecar with everything that made it. Run it from a Python venv that has `stable_audio_3`. |
| `sfx-prompts/` | The prompt lists of every batch so far — the record of what was asked for. |

Sound path: render, pick by ear, then `dotnet run --project Tools\MusicBake -c Release -- --sfx <wav> <name>` (add
`--music` to keep stereo). See "The sound" in `docs/game-feedback.md` and the `local-ai` skill.
