# Checking tools: DocDrift, WindingCheck, VectorBoundary, doc-comment warnings

Part of [Formats and tools](../formats-and-tools.md).

Sweeps for conventions no test covers. DocDrift and WindingCheck exit 0 on findings (sweeps, not gates); the [lattice probe](lattice-probe.md) has its own page.

## Tools/DocDrift: a figure beside a name the code defines differently (#525)

`dotnet run --project Tools\DocDrift [-- --all] [-- --root <repository>]`. **Always exits 0**: run by hand before a release or in a pass like #400, never a gate.

- **Reads** every named numeric defined **exactly once** (a C# `const`, a config property with an initialiser, a `static readonly` scalar, a shader `static const`; plain literals; `.cs`/`.fx`/`.fxh`), then `docs/*.md` and `CLAUDE.md` for a name in backticks with a figure **within 50 characters after it**, in the same sentence. The agent journal is left out.
- **Forgives** rounding, `~`/`about`/`≈` within 10 %, a range containing the value, `0.22 → 0.12` (the arrow's end is current), written units, `±600` for 1200, multipliers and name lists paired in order.
- **History is filtered per sentence, never per line** (a paragraph is one line of thousands of characters): a disagreement in a sentence with a word such as *was, until, before, no longer, used to, formerly, originally* (the list is in `Program.cs`) is set aside, and `--all` prints those too. ⚠ A bare "read as" and "rather than" are deliberately **not** on the list (often present tense: with them a real drift, the storm's `PuffOpacity`, was filtered away), **and neither is an issue number**: a document states what is true now, so "(#461)" says where a sentence came from, and treating it as history hid the tutorial card's 178 px against the 148 the code gives.
- **`--citations` is the tool's other sweep** (#400, `Citations.cs`): every section title cited beside a document's path in code or docs — the form `"Sun shadows" in `docs/rendering.md``, straight or typographic quotes, the path with or without backticks — must name a heading of that document, a heading of one of its sub-documents (`docs/<area>/*.md` under an index, #839) or a row of the index's "Where the old sections went" table; case, a trailing issue number, backticks and emphasis are ignored, and a heading that begins with or contains the cited title counts, since citations shorten titles on purpose. The journal is left out. Passes 8 and 9 each found such citations with a scratchpad script; now `dotnet run --project Tools\DocDrift -- --citations --selftest` — the self-test plants a citation of no section and sees it come back unresolved before the real list is read (188 citations, 0 unresolved on 2026-10-13). A citation without quotes is not seen.

## Tools/WindingCheck: CLAUDE.md's winding rule, mesh by mesh (#400)

`dotnet run --project Tools\WindingCheck [-- --verbose] [-- --detail <text>] [-- --selftest]`. The one tool that is `net10.0-windows` with a real MonoGame backend: it runs the meshes' **own constructors** on **WARP** (`GraphicsAdapter.UseDriverType = FastSoftware`) on a form never shown (no GPU, no focus) and reads every buffer back. It loads `InstancedModel` from the compiled shaders beside itself (**exit 2** when missing: build it). It builds meshes through their owners (`CannonRig`, `ArenaIsland`, `CeilingPlate`, `BallRenderSet`, the scatters, every sky dome) or directly where an owner needs a whole scene, walking their fields by reflection for `IProceduralMesh`.

- **Two judgements per part.** *Normals*: clockwise from outside means `(b − a) × (c − a)` points inward, so its cosine against the outward normals must be negative; a triangle past `AGREEMENT_COS` (+0.25) is reported (`--detail Wood` shows the baobab flare's soft-shading triangles). *Signed volume* per welded closed piece must be negative; it does not trust the normals, so it catches a backwards piece whose normals were flipped to match.
- **Named exceptions, printed**: the sky dome (captured order, seen from inside), the editor's `WireBoxMesh` (inward on purpose) and the drain's `FunnelRimsMesh`, **wound wholly backwards** and harmless only because drawn `CullNone`. ⚠ Enrol the rims in `TwoSidedNormals`, or any culled pass, and they shade from the wrong side: rewind first.
- ⚠ Every mesh is uploaded `WriteOnly` and MonoGame refuses `GetData` on it (`NotSupportedException`) though the DirectX read works; the tool flips the flag by reflection (nothing in the game does).

## Tools/VectorBoundary: a Roslyn analyzer (#400)

Reports **VEC001** at every crossing between `System.Numerics` and `Microsoft.Xna.Framework` through a conversion *operator* instead of the named `ToXna()`/`ToNumerics()` (CLAUDE.md's convention). MonoGame 3.8.5 declares the inbound implicit operator, so such a crossing has nothing on the line to find; only the compiler's bound tree can. It is wired into no project; point a build at it:

```powershell
dotnet build Tools\VectorBoundary -c Release
dotnet build Game.sln -t:Rebuild "-p:CustomAfterMicrosoftCommonTargets=<repo>\Tools\VectorBoundary\VectorBoundary.targets"
```

`-t:Rebuild` because an up-to-date project is not recompiled and so not analysed. `IMPLICIT` marks a conversion the source never spelled, `CAST` one written as a cast. A `netstandard2.0` project in `Game.sln` that excludes itself; all four solutions scan clean.

## Doc comments are compiler-checked (`Directory.Build.props`, #594)

`GenerateDocumentationFile` is on for every project but VectorBoundary, so these are ordinary warnings in every build and in `build.yml`: **CS1574** (a `cref` that does not resolve), **CS0419** (ambiguous `cref`: name the overload), **CS1570** (malformed XML), **CS1572/CS1571/CS1734** (`<param>`/`<paramref>` not matching the signature), **CS1587** (a doc comment on nothing). **CS1591** and **CS1573** are suppressed on purpose.

- **A project's own `<NoWarn>` must be `$(NoWarn);…`**: a bare value replaces the inherited one and every missing doc comment returns.
- **A file compiled into a tool as well** (`ProceduralMusic.cs`, `OggTrack.cs`, `MusicLevels.cs`, `MusicFade.cs`, `Spectrum.cs` into `MusicBake`) may not `cref` a Game-only type (`GameMusic`, `ProceduralJukebox`): write `<c>GameMusic</c>`.
- **The compiler does not see** a second `<summary>` stacked on a member or a doc block separated from its member by `//` lines; both attach silently. Move a doc with its member.
