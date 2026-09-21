---
name: local-ai
description: Use the local AI models LM Studio serves on the owner's desktop — rank GitHub issues by meaning before filing one (Tools/SemanticSearch), and ask Gemma 4 what changed between two screenshots or about a cropped detail (vision.ps1). Load before filing an issue or before comparing a batch of captures by eye, and only when LM Studio answers on localhost:1234.
---

# Local AI on the desktop (LM Studio)

The owner's desktop runs **LM Studio** with an OpenAI-compatible server at `http://localhost:1234` (the RX 6900 XT has 16 GB). Other machines, the laptop included, probably do not — **check before relying on any of this**:

```powershell
& "$env:USERPROFILE\.lmstudio\bin\lms.exe" ps          # what is loaded, at what context, with what TTL
curl.exe -s http://localhost:1234/api/v0/models        # every model on disk, loaded or not
```

What follows is not a menu of plausible uses. **Every line was measured on 2026-09-16 against answers known in advance**, and half the obvious ideas failed. Use the model where it earned its place and nowhere else; `docs/agent-notes.md` (the entry "lokální AI: co funguje a co ne") has the runs.

| Task | Model | Verdict |
|---|---|---|
| Is there already an issue like this one? | `text-embedding-qwen3-embedding-0.6b` via `Tools/SemanticSearch` (the default since #439) | **Use it.** Seven probes with a known partner: 1st five times, 3rd twice. nomic on the same probes: 1st four times, then 2nd, 4th and 9th. |
| Find something in the Czech agent journal | same, `--journal` | **Use it, in Czech (#439).** Six questions with a known marker: the answer 2nd, 2nd, 8th, 8th, 17th and 49th of 707 chunks — in the top ten for 5 of 6, where nomic managed 3 of 6 (1st, 2nd, 10th, 83rd, 86th, 273rd). Read the top ten, not the top one. |
| Answer a question from the journal or the documents | `google/gemma-4-12b` via `Tools/SemanticSearch --ask` | **A lead, never a source (#494).** Ten notes a question, on the CPU 53–134 s an answer: journal questions right and citing the marker's entry 3 of 6, a sibling part of the right entry 2 of 6, and once — the answer outside the ten notes — a confident answer to a neighbouring question. Documents 4 of 4. Open the cited entry; if it reads off-topic, the ranking missed. |
| Which section of `docs/` answers this? | same, `--docs` | **Use it (#490).** Fifteen known-answer questions: the right section 1st twelve times, 2nd twice, once 20th (CLAUDE.md's "Project", a piece about six things at once — the top hit, `docs/testbed.md`'s opening, answered it too; nomic had it 115th). |
| What changed between two captures? | `google/gemma-4-12b` via `vision.ps1` (default); `qwen/qwen3-vl-8b` is close | **Use it as a first pass.** Gemma 3½ of 4 known pairs, Qwen3-VL-8B 3 of 4 (#440). Both said "identical" for one file sent twice. Gemma named the crosshair growing and turning red but called an upward tilt a zoom; Qwen got the tilt and spotted a ball that really had appeared, but missed the crosshair growing. Gemma 1.5–14 s a pair, Qwen 13–16 s. |
| Read a small detail (a colour, a mark) | Gemma 4; Qwen3-VL a step behind | **Only on a crop.** On a 256 px crop Gemma 7 of 7, Qwen 6 of 7 (in 0.3 s against Gemma's 1.3); on the whole frame both 5 of 7. For an exact value, measure pixels (`screenshot/palette.ps1`, a bar scan). |
| Does a level's shape read as its subject? | neither | **No.** Blind naming got 3–4 of 18 levels for both models, and most symmetric patterns came back as "butterfly" (Gemma) or "cannonball pattern" (Qwen). Neither model's 1–5 rating separates the levels playtesting said read from those it said did not; Qwen's does not separate them at all in 3D (3.8 against 3.7). |
| Draw a reference before designing something (a cup, roof props, a scene's island) | Z-Image-Turbo through stable-diffusion.cpp on Vulkan — **not LM Studio**, see the `design-references` skill | **Use it.** Twenty references for #429, #436 and #404 at 33–37 s each; the owner: *„ty obrázky jsou skvělé“*. Describe shapes rather than names, and expect placement and text to drift. |
| Edit code | `deepseek-coder-v2-lite-instruct` via Aider | **No.** Did half a two-part extraction and rewrote the line endings; reviewing it cost more than doing it. |

## Before filing an issue

```powershell
& "$env:USERPROFILE\.lmstudio\bin\lms.exe" load text-embedding-qwen3-embedding-0.6b --ttl 1800 -y   # 639 MB; --gpu off works too, embedding is cheap
dotnet run --project Tools\SemanticSearch -- --file draft.md     # or --issue 425, or "free text"
```

The issues come live through `gh` on every run, so an issue filed a minute ago is in the list. **Read the top five rather than trusting a score**: known duplicates and follow-ups scored 0.84–0.91, but an issue merely on the same subject reached 0.875, so there is no threshold that separates them. The whole tool is documented under "The semantic search" in `docs/formats-and-tools.md`.

## Before reading a whole document

```powershell
dotnet run --project Tools\SemanticSearch -- --docs "why are the shadow maps drawn before the scene target is bound"
```

It prints the sections most likely to hold the answer, with the first line of the piece under each label — a section here can run to thirty pieces, so open the file at that heading rather than from the top. The first run embeds the 1011 pieces once (27 s for nomic on the GPU, 112 s for Qwen3 on the CPU alone); after that the corpus costs nothing over an issues-only run. Measured on 2026-09-21 (#490, #439): the known section first for 12 of 15 questions and second for 2; the miss was a question about one paragraph of a section that is about six things. Add `--mark <text>` to read off the rank of the first result carrying a known marker — that is how every number in this table was measured. Add `--ask` to either command and Gemma 4 answers from those results, naming the notes it used (load it first: `lms load google/gemma-4-12b -c 16384 --ttl 1800 -y`; `--gpu off` works and is how it was measured). What it names is what to open; it does not say when the notes do not hold the answer.

## Generating a sound effect (#482)

Stable Audio 3 Small-SFX on the **CPU** (torch has no GPU on this card): `C:\Users\panrd\AI\sfx\venv` (uv, Python 3.13, torch 2.7.1 CPU, the `stable-audio-3` library), the model in the Hugging Face cache — it is gated, and the owner's login is on the desktop (`venv\Scripts\hf.exe auth whoami`).

```powershell
cd C:\Users\panrd\AI\sfx
venv\Scripts\python.exe generate-sfx.py prompts.json --model small-sfx --out out\<batch> --steps 8 --cfg 1 --count 3 --normalize -1
```

- **Measured 2026-09-21:** 9 s to load, **5.6–6.3 s a render** of 1.5–4 s at 44.1 kHz stereo, 8 steps at cfg 1 (the model's own defaults). `small-sfx-base`, the ungated pre-trained checkpoint, renders full-scale noise — do not use it. The base config points its text encoder at the gated repo; the script rewrites the conditioner to the model's own.
- **The prompts, learnt over three rounds with the owner's ear:** "soft, gentle, clean" came back *dull, weak and boring*; write "punchy, satisfying, arcade, big, juicy" for everything but impacts — and tell an impact its volume in words ("moderate volume", "gentle attack", negative "loud, distorted, clipping, aggressive") or the model saturates: 8 of 9 attach renders clipped until it was. Balloons render harsh whatever the clip count says. Warm and bass-heavy is still the taste; the game's taste rule stands.
- **The batch is listened to from `out\<batch>\index.html`** (players beside the files, the prompt under each, clipped rows flagged), one prompt family at a time, and a shortlist page is assembled from the picks. `--normalize -1` for listening; the game applies its own law at load.
- **Into the game:** `dotnet run --project Tools\MusicBake -c Release -- --sfx <render.wav> <shoot|landed|release|firework-burst|ceiling-step>` writes `Game/Sfx/<name>.ogg`; `git add` it at once. A file that is not there leaves the procedural bake standing. The victory fanfare goes in with `--sfx <render.wav> victory-fanfare --music`, which keeps the stereo image and measures the key and tempo the star chime tunes to (printed with the runners-up; `--root <midi>`/`--bpm <n>` override them).

## Comparing captures

```powershell
.\.claude\skills\local-ai\vision.ps1 -Load -Image before.png -Image2 after.png -Question "The two images are two frames from the same game, taken a moment apart. List the visible differences between them, most important first, as at most five short bullet points. If they look identical, say so."
```

- **The point is the sweep, not the single pair.** Looking at two captures yourself costs a few thousand tokens and is more reliable; asking the model costs a hundred tokens of answer. Over twenty scenes before and after a shader change, let it describe every pair and open only the ones whose description is unexpected. **For that, use the `capture-review` skill**, which runs an exact block diff first (identical pairs never reach the model) and asks about crops of the changed regions as well as the whole frame.
- **Crop to what the question is about** (`-Crop 256` round the centre, `-Rect "x,y,w,h,scale"` anywhere else): a crosshair over a busy cluster simply is not there for it in a scaled whole frame.
- **Its answer is a lead, never a measurement.** Gemma called a camera tilting up a zoom. Confirm anything that matters with the pixels or your own eyes before it goes into a document.
- Thinking is off unless `-Think` is passed — on these tasks it was ~20× slower and no better, and with thinking on a small `max_tokens` comes back as an empty answer.
- **Gemma 4 is the default; reach for `-Model qwen/qwen3-vl-8b` in two cases (#440).** When the card is shared — Qwen is 9.9 GB loaded against Gemma's 12.8, so a ~6 GB job such as the music generation fits beside it — and when a frame has to go in at full size: loaded at 16k, Qwen answered correctly on a 1600×900 frame (12 s) and on a pair of them (29 s), where Gemma at 16k fell over. Everywhere else it was a step behind: it said "none" for a white crosshair over the busy cluster that Gemma read, and it rates any level shape generously.

To photograph the Game for a question: `BS3D.exe play level=<Name> shot=14`, then stop the process once its `[shot]` line is logged (~20 s a level, past any chapter-intro tour). **The Game reads and writes the owner's real `%LOCALAPPDATA%\BS3D` save** — hash `Settings.json` and `Progress.json` before and after (they came back byte-identical across nineteen such launches). The Testbed takes `shot=` too and touches no save.

## Running LM Studio

- `lms load <key> -c <context> --ttl <seconds> -y`, `lms unload <key>`, `lms ps`. Give everything you load a **TTL** so the card is handed back when you are done.
- **After a reboot the server may not be on 1234.** `lms server status` says whether it runs; `lms server start` answering `listen EACCES … 1234` means Windows reserved the port (`netsh interface ipv4 show excludedportrange protocol=tcp`; on 2026-09-21 the range 1136–1235 covered it after one reboot and 1237–1336 covered 1240 after the next). Start it on a free port (`lms server start --port 8765`) and pass the tool `--endpoint http://localhost:8765/v1`; `vision.ps1` and the other scripts still expect 1234.
- **Gemma 4 at 8k context.** At 16k a full-size frame killed it: `terminated`, then `Model is unloaded`. Qwen3-VL-8B survived 16k with full frames.
- **One big model at a time.** Gemma is 12.8 GB loaded, Qwen3-VL-8B 9.9 GB and DeepSeek 15.6 GiB, on a 16 GB card.
- **The card is shared with other sessions** — music generation (ACE-Step, ~6 GB) and image generation (`design-references`, ~10.5 GB) run on it too. Check `lms ps` and ask a session that holds the GPU before loading anything big, and unload your model when a peer asks for a window; on 2026-09-16 two such handovers by message went cleanly.
- **If a request fails with `ErrorDeviceLost`**, LM Studio's Vulkan backend lost the device and reloads the model by itself — at its default 65k context. Unload and load it again at 8k. When it happened on 2026-09-16 the System log had no driver reset (4101) and no Kernel-Power event; if one ever shows up, tell the owner at once.

## Aider and coding models

Not for this repository's code (see the table). If a coding model is tried anyway: **never run Aider in a checkout of this repository** — on 2026-09-16 it auto-committed three commits onto an agent's feature branch, the first of which deleted 1727 lines of `PlayHud.cs`. Work in a copy outside the repo, with `--edit-format diff --no-auto-commits --no-git`, and set `PYTHONUTF8=1` or it crashes printing any character outside cp1250 (a `−` in a comment was enough).
