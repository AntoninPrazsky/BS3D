# Chapter intro: the other scenes' shots

Part of [The game: feedback](../game-feedback.md). Mechanism and shared rules: [Chapter intro prologues](chapter-intro-prologues.md). Each list is `IntroShot[]` from `Game/Effects/<Scene>IntroShots.cs` (#559), the establishing view first, then the tour's last leg.

## How a path is kept safe

- **Terrain scenes read the land off `TerrainMirror`** (`Prazsky.Core`: CPU mirrors of `Desert.fx`, `Mountain.fx`, `Outback.fx` with `RockLayer`'s monoliths, plus meadow, forest, polar) and lift the lens over a **dilated ground** (`AridIntroPaths.Hug`: the highest ground a few points either side, off the line on both hands, plus clearance), so it rises before a crest and never cuts a dune's knife edge. None of the four arid terrains is seeded by `sceneseed=`.
- **`Game/Effects/IntroGround.cs`** (savanna, forest, tropical, meadow) holds the height mirror and every solid as a capsule (each plant its `PlantFigure`); a path is taken only when all 96 points keep a margin off all of them. ⚠ The savanna's spacing footprints are not the plants (a bush's crown overhangs). ⚠ In the forest a one-unit margin passed under a broadleaf's crown; it is two.
- **`IntroGround.SightClear`** (#610) marches the sight line to the subject, so the baobab walk sees its baobab (not asked of the acacia's track, where neighbours' crowns block).
- **The Moon and Mars have no CPU mirror**: `OffworldGround` gives a **ceiling** and each path one constant lift of 2.5-3 over it.
- **The strange scenes** have no mesh, so `SceneRenderer` carries host copies of the shaders' placement ("Where the strange scenes' things stand"), and `IntroShot` has a **look-at path** for a moving subject.

## Scene by scene

- **Sea**: *the island* (arc round its flank at the waterline), *the depths* (#759, under the island up through the fish and light shafts, the cluster seen through the funnel's glass; the only shot under water), *the climb to the sun* (#652, ending on the glitter path leading to it). *The swell* was replaced because its cut into the climb read as a glitch (same heading at the lens floor over featureless swell). The flank and climb stay over `CREST_REACH` (4.2) above `LevelY`.
- **Desert**: *the erg*, *the crest*, *the flock*. **Outback**: *the plain*, *the approach*, *the crown*, round the tallest monolith. ⚠ The approach comes in 50-110 degrees off the arena line (it once started beside the island, under its rim). **Mountains**: *the range*, *the pass*, *the summit* (turned round **28 under it**, so it stands over the lens; from ten over, a row of crests could not be told apart).
- **Meadow**: *the valley*, *the brook*, *the path* (each ends on a subject). **Savanna**: *the plain*, *the baobab*, *the acacia*. **Forest**: *the forest*, *the grove*, *the tree*; ⚠ a run *through* the wood (the aurora's shot) does not carry here (240 trees; the few clear runs had five trees within twelve units). **Tropical**: *the lagoon*, *the beach*, *the palm*.
- **Dream**: *the marbled sky*, *the glass*, *the underside* (#655, replacing a dolly into one soft orb). ⚠ **The dream writes no depth**, so the island covers any solid in front of it: shots refuse a lens whose line to the subject or funnel a solid's bound touches. The solids orbit on the wall clock, so shots are laid out for the seconds they play in (`IntroContext.WallClock`).
- **Cavern**: *the cavern*, *the crystals*, *the underside*, *the river* (#656); *the glowworms* (the vault) is gone (#528).
- **Storm**: *the deck*, *the tops*, *the strike*, **caught, not hoped for**: when is a pure function of the wall clock (`TryGetSceneEvent`), where is fixed the first time it is asked at or after the onset (#750), so the builder stretches the first two shots (2.6-4.4 s) to open the third about 1.2 s before it, and tests every lens point against every cell (inside a puff the frame is milk).
- **Polar**: *the icesheet*, *the crevasses*, *the pressure ridge*.
- **Moon, Mars, space and the Grid use ABSOLUTE lenses** (each frames one object); other prologues widen the lens they are handed, so a tour replayed from the menu shows what play will.
  - **Moon**: *the plain*, *the crater* (⚠ a 2.3 to 1.5 radii rise smeared under motion blur, so it travels half that), *the Earth*. **Mars**: *the plain*, *the mesa* (only across open plain), *Phobos* (⚠ over bare plain it was a tan sky with a dot, so a mesa stands on it).
  - **Space**: *the station*, *the drain*, *the nebula* (⚠ looking out from outside the rim it had no scale). **Grid**: *the grid*, *the cube*, *the ring*, every path tested against every solid as its box (`SceneRenderer.GridSolids`, `TryGetGridRing`) and against sight lines (⚠ a slide ten units clear of a tower still ended on its dark side).
  - ⚠ The Moon's Earth shot showed diagonal sky streaks: the motion blur's background reprojected at the cluster's depth (see the #559 note under "Motion blur" in `docs/rendering.md`); the tour has no motion blur.
