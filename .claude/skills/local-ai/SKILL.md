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
| Is there already an issue like this one? | `text-embedding-nomic-embed-text-v1.5` via `Tools/SemanticSearch` | **Use it.** Known partners ranked 1st–2nd for 4 of 7 probes; the other three sat under issues on the same subject. |
| Find something in the Czech agent journal | same | **Don't trust it.** Czech questions put the answer 9th–201st of 532. Ask in English (ranked 2nd), or try a multilingual model. |
| What changed between two captures? | `google/gemma-4-12b` via `vision.ps1` | **Use it as a first pass.** Named the crosshair growing and turning red; said "identical" for one file sent twice; mis-stated which way a camera moved. 20–80 s a pair. |
| Read a small detail (a colour, a mark) | Gemma 4 | **Only on a crop.** 7 of 7 on a 256 px crop, 5 of 7 on the whole frame. For an exact value, measure pixels (`screenshot/palette.ps1`, a bar scan). |
| Does a level's shape read as its subject? | Gemma 4 | **No.** Blind naming got Heart, Star and Smiley and called most other symmetric patterns "butterfly"; ratings did not separate the levels playtesting said read from those it said did not. |
| Edit code | `deepseek-coder-v2-lite-instruct` via Aider | **No.** Did half a two-part extraction and rewrote the line endings; reviewing it cost more than doing it. |

## Before filing an issue

```powershell
& "$env:USERPROFILE\.lmstudio\bin\lms.exe" load text-embedding-nomic-embed-text-v1.5 --ttl 1800 -y   # 80 MB
dotnet run --project Tools\SemanticSearch -- --file draft.md     # or --issue 425, or "free text"
```

The issues come live through `gh` on every run, so an issue filed a minute ago is in the list. **Read the top five rather than trusting a score**: known duplicates and follow-ups scored 0.84–0.91, but an issue merely on the same subject reached 0.875, so there is no threshold that separates them. The whole tool is documented under "The semantic search" in `docs/formats-and-tools.md`.

## Comparing captures

```powershell
.\.claude\skills\local-ai\vision.ps1 -Load -Image before.png -Image2 after.png -Question "The two images are two frames from the same game, taken a moment apart. List the visible differences between them, most important first, as at most five short bullet points. If they look identical, say so."
```

- **The point is the sweep, not the single pair.** Looking at two captures yourself costs a few thousand tokens and is more reliable; asking the model costs a hundred tokens of answer. Over twenty scenes before and after a shader change, let it describe every pair and open only the ones whose description is unexpected.
- **Crop to what the question is about** (`-Crop 256` round the centre, `-Rect "x,y,w,h,scale"` anywhere else): a crosshair over a busy cluster simply is not there for it in a scaled whole frame.
- **Its answer is a lead, never a measurement.** It said a camera moved "forward and downward" when it had tilted up. Confirm anything that matters with the pixels or your own eyes before it goes into a document.
- Thinking is off unless `-Think` is passed — on these tasks it was ~20× slower and no better, and with thinking on a small `max_tokens` comes back as an empty answer.

To photograph the Game for a question: `BS3D.exe play level=<Name> shot=14`, then stop the process once its `[shot]` line is logged (~20 s a level, past any chapter-intro tour). **The Game reads and writes the owner's real `%LOCALAPPDATA%\BS3D` save** — hash `Settings.json` and `Progress.json` before and after (they came back byte-identical across nineteen such launches). The Testbed takes `shot=` too and touches no save.

## Running LM Studio

- `lms load <key> -c <context> --ttl <seconds> -y`, `lms unload <key>`, `lms ps`. Give everything you load a **TTL** so the card is handed back when you are done.
- **Gemma 4 at 8k context.** At 16k a full-size frame killed it: `terminated`, then `Model is unloaded`.
- **One big model at a time.** Gemma is 12.8 GB loaded and DeepSeek 15.6 GiB, on a 16 GB card.
- **If a request fails with `ErrorDeviceLost`**, LM Studio's Vulkan backend lost the device and reloads the model by itself — at its default 65k context. Unload and load it again at 8k. When it happened on 2026-09-16 the System log had no driver reset (4101) and no Kernel-Power event; if one ever shows up, tell the owner at once.

## Aider and coding models

Not for this repository's code (see the table). If a coding model is tried anyway: **never run Aider in a checkout of this repository** — on 2026-09-16 it auto-committed three commits onto an agent's feature branch, the first of which deleted 1727 lines of `PlayHud.cs`. Work in a copy outside the repo, with `--edit-format diff --no-auto-commits --no-git`, and set `PYTHONUTF8=1` or it crashes printing any character outside cp1250 (a `−` in a comment was enough).
