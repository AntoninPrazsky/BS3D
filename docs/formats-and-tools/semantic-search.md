# The semantic search (Tools/SemanticSearch)

Part of [Formats and tools](../formats-and-tools.md).

`Tools/SemanticSearch` finds the GitHub issues (and optionally journal entries and document sections) that **mean** the same as a text, through a local embedding model in LM Studio. In `Game.sln`, plain `net10.0`, **not a gate**. A grep finds only the words you thought of (#319 was filed an hour after #316 and #317 described the same levels in other words).

```
dotnet run --project Tools\SemanticSearch -- "text"           # rank every issue, open and closed
dotnet run --project Tools\SemanticSearch -- --issue 425       # the duplicate check
dotnet run --project Tools\SemanticSearch -- --file draft.md   # a draft, before it is filed
  --open  --journal  --docs  --top N (8)  --mark TEXT  --ask  --answer-model KEY  --model KEY  --endpoint URL
```

Exactly one of a text, `--issue` or `--file`. `--journal` adds `docs/agent-notes.md` and its archive; `--docs` adds `docs/*.md`, `CLAUDE.md` and `BestPractices.md` section by section (#490); `--mark TEXT` reports the rank of the first result containing it. **Exit 2**: malformed command line, no repository or a failed `gh`; **exit 3**: LM Studio does not answer.

## Setup and behaviour

- LM Studio must be running with **`text-embedding-qwen3-embedding-0.6b`** (`DEFAULT_MODEL`; `lms load text-embedding-qwen3-embedding-0.6b`). It beat the English-centric nomic-embed-text on issues, the Czech journal and documents (#439). ⚠ If `lms server start` answers `EACCES` on port 1234 after a reboot, Windows has reserved the port (`netsh interface ipv4 show excludedportrange protocol=tcp`): start the server elsewhere and pass `--endpoint http://localhost:<port>/v1`.
- Texts carry the model family's prefixes (`search_document:`/`search_query:` nomic, `passage:`/`query:` e5, an `Instruct: <task>\nQuery: <text>` instruction on the *query only* for keys containing `qwen3-embedding`) and are clipped to 2500 characters.
- **Issues are fetched live through `gh` on every run and never cached** (a duplicate check that cannot see an issue filed a minute ago is the #319 failure again). **Vectors are cached** under `%LOCALAPPDATA%\BS3D-Tools\SemanticSearch`, one file per model and corpus, keyed by a hash of the exact text.
- The journal is cut at `## ` headings then paragraph breaks; documents at `## ` and `### `, labelled `section › subsection`; a paragraph longer than a piece is cut at sentence ends, not clipped (#490).

## What to trust

A question about what a section is *about* finds it (11 of 15 document questions first, 3 second); a question about one paragraph of a section about six things may not. **`--ask`** (#494) hands exactly the results shown to a local chat model (`google/gemma-4-12b`, thinking off, temperature 0) and prints a short answer with its sources. ⚠ Once, with the answer outside the shown notes, it answered a neighbouring question confidently instead of saying the notes did not hold it: **it is a lead into the entry it cites and nothing more.**
