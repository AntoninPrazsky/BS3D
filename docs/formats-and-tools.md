# Formats and tools

What is written to disk and what edits, checks and generates it: the map, level and level-set JSON, the player's save files, the map editor, the level generator and its probes, the other command-line tools, the `[build]` lines and the application icon. **File formats and tool command lines are contracts** (the online score service, the skills, CI and scripts read them): their field names, argument spellings and exit codes change only with every reader.

## Sub-documents

- [Map, level and level-set formats](formats-and-tools/level-formats.md): JSON fields, versions, leniencies, set entries, blocks, `LevelIdentity`.
- [Player files](formats-and-tools/player-files.md): `Progress.json`, `Settings.json`, `Online.json`, `Outbox.json`, `Stats.json`, `Notes/`; `AtomicFile`.
- [The level generator](formats-and-tools/levelgen.md): LevelGen's command line, layout, the fourteen blocks, the unlock ramp.
- [What LevelGen refuses](formats-and-tools/levelgen-gates.md): every static gate with its reason; kinding.
- [LevelGen's probes](formats-and-tools/levelgen-probes.md): sag, shortest clear, arrival, cuts, drop cinematic.
- [Designing levels](formats-and-tools/levelgen-design-lessons.md): the traps each block taught.
- [The score simulator](formats-and-tools/score-sim.md): ScoreSim's refusals and the ceilings table.
- [The logic tests](formats-and-tools/logic-tests.md): what each family of `Tests/BS3D.Tests` guards.
- [The music bakery](formats-and-tools/music-bake.md): MusicBake's modes, checks, exit codes.
- [The semantic search](formats-and-tools/semantic-search.md): usage (run it before filing an issue) and how far to trust it.
- [Checking tools](formats-and-tools/checking-tools.md): DocDrift, WindingCheck, VectorBoundary, doc-comment warnings.
- [The lattice probe](formats-and-tools/lattice-probe.md): finding a repeating grid in a screenshot.
- [The map editor](formats-and-tools/map-editor.md): keys, render pipeline, the removed scene-config panel.
- [The `[build]` lines](formats-and-tools/build-lines.md): what a run says it is; `width=`/`height=` in the Game.
- [The application icon](formats-and-tools/application-icon.md): the ladder, DIB/PNG encoding, the window icon.

## Where the old sections went

Code comments cite sections of this document by title; this table resolves them. The last rows are bold paragraphs that comments quote by name.

| Old section | Now in |
|---|---|
| Levels and serialization | [level-formats](formats-and-tools/level-formats.md); the save files in [player-files](formats-and-tools/player-files.md) |
| The music bakery (Tools/MusicBake) | [music-bake](formats-and-tools/music-bake.md) |
| The semantic search (Tools/SemanticSearch) | [semantic-search](formats-and-tools/semantic-search.md) |
| The scoring simulator (Tools/ScoreSim) | [score-sim](formats-and-tools/score-sim.md) |
| The logic tests (Tests/BS3D.Tests) | [logic-tests](formats-and-tools/logic-tests.md) |
| The number-drift sweep (Tools/DocDrift) | [checking-tools](formats-and-tools/checking-tools.md) |
| The lattice probe (Tools/LatticeProbe, #674) | [lattice-probe](formats-and-tools/lattice-probe.md) |
| What the first full sweep said (2026-09-29, laptop, 1600 × 900, `sceneseed=3`; 80 captures, 66 tiles each) | [lattice-probe](formats-and-tools/lattice-probe.md) |
| The winding check (Tools/WindingCheck) | [checking-tools](formats-and-tools/checking-tools.md) |
| The vector boundary scan (Tools/VectorBoundary) | [checking-tools](formats-and-tools/checking-tools.md) |
| The doc comments are compiler-checked (Directory.Build.props, #594) | [checking-tools](formats-and-tools/checking-tools.md) |
| The level generator (Tools/LevelGen) | [levelgen](formats-and-tools/levelgen.md) |
| The campaign is fourteen blocks (#194, #207, #182, the Arcade, #253, #255, #295, #325, #420, #491, #690) | [levelgen](formats-and-tools/levelgen.md); each block's style in [levelgen-design-lessons](formats-and-tools/levelgen-design-lessons.md) |
| What a design is, and what the tool refuses | [levelgen-gates](formats-and-tools/levelgen-gates.md) |
| The Quarry thinned, and what a level's LENGTH actually is (#398) | [levelgen-design-lessons](formats-and-tools/levelgen-design-lessons.md) |
| Play order inside a block (#413) | [levelgen-design-lessons](formats-and-tools/levelgen-design-lessons.md) |
| The sag gate: hanging a level instead of reading it (#301, #302) | [levelgen-probes](formats-and-tools/levelgen-probes.md) |
| The anchor load: what the glass actually carries (#301, #302) | [levelgen-probes](formats-and-tools/levelgen-probes.md) |
| The shortest clear: how few shots empty the field (#458) | [levelgen-probes](formats-and-tools/levelgen-probes.md) |
| Shot arrival: which landings can be got to, and from how far round (#514) | [levelgen-probes](formats-and-tools/levelgen-probes.md) |
| What one cut lets go (#692) | [levelgen-probes](formats-and-tools/levelgen-probes.md) |
| How often the drop cinematic fires (#719) | [levelgen-probes](formats-and-tools/levelgen-probes.md) |
| The reveal: the pattern the later chapters are designed against (#161, #255, #743) | gate in [levelgen-gates](formats-and-tools/levelgen-gates.md); pattern in [levelgen-design-lessons](formats-and-tools/levelgen-design-lessons.md) |
| The coiled levels (#207) | [levelgen-design-lessons](formats-and-tools/levelgen-design-lessons.md) |
| The arcade levels (the eighth block written; thirteenth in play since 2026-10-05) | [levelgen-design-lessons](formats-and-tools/levelgen-design-lessons.md) |
| The spectrum levels (the ninth block written, #253; twelfth in play since 2026-10-05) | [levelgen-design-lessons](formats-and-tools/levelgen-design-lessons.md) |
| The map editor at a glance | [map-editor](formats-and-tools/map-editor.md) |
| The map editor's render pipeline | [map-editor](formats-and-tools/map-editor.md) |
| The scene-config panel (MapEditor) — removed in #522 | [map-editor](formats-and-tools/map-editor.md) |
| What a run says it is: the `[build]` lines (#372) | [build-lines](formats-and-tools/build-lines.md) |
| The application icon (Game) | [application-icon](formats-and-tools/application-icon.md) |
| `width=` / `height=` reach the Game too | [build-lines](formats-and-tools/build-lines.md) |
| The flat opening (#603); "The campaign is nine blocks of five" (old title) | [levelgen-design-lessons](formats-and-tools/levelgen-design-lessons.md); [levelgen](formats-and-tools/levelgen.md) |
