# The savanna's trees and plain

Part of [The scenes](../scenes.md).

What is planted on the savanna ground ([The savanna](savanna.md)): the acacias, the baobabs and doum palms, and the dressing between them. A savanna's character is in what is scattered across it, so these are real 3D geometry on the shared instancing path, not billboards. The ring of fires and their hearth stones are in [The savanna's campfires](savanna-fires.md).

## How the plain is built

- **`SavannaScatter`** owns where and what (the forest's split; the effect and the frame's light stay the renderer's). It builds everything **once**, off one seed and one occupancy list, into `ScatterBucket`s: one instanced draw each, with **the instances uploaded once at build** rather than re-uploaded on every draw. The whole plain is some thirty draws. The hearth stones keep the dynamic path (they are drawn per fire with that fire's light).
- Plants stand in a ring clear of the island (`AcaciaConfig.MinRadius`/`MaxRadius`; the inner edge is 60 so wider crowns do not hang over the play camera's shoulder), **82 % around cluster centres** (`Clusters`; a centre no plant reaches is not a grove), on the terrain (`TerrainMirror.Savanna`), kept apart by `ScatterSpacing`'s rule (one copy with the forest's). Drawn opaque and depth-writing after the terrain, before the birds.
- **The plain is re-rolled on every `sceneseed`**, as every change to what is planted is. The volume rule (#653) holds: counted over `sceneseed` 0 to 4, 0 overlapping pairs on every seed.
- Everything on the plain casts and receives sun shadows (see [Shadows](savanna.md#shadows)).

## The acacia

`Acacia.fx`, `AcaciaMesh`; shaded from the scene's own sun and dome (`dot(N, SunDirection)` plus the zenith-to-horizon ambient off the normal), so a tree sits in the savanna's light. They were camera-facing billboards with a `clip()`ed silhouette until #202, which read as a paper cutout: one flat shape however the camera moved. **Do not go back to billboards.**

- **Four kinds** (`AcaciaKind`): mature (about half carry a second, smaller tier offset to one side, with sky between the layers), broken (one off-centre tier and a bare splintered spar), young (slender, forking high under one small crown) and dead (no foliage, boughs branching on into twigs, bleached `DeadwoodColor`). Shares are `AcaciaConfig.YoungFraction`, `DeadFraction`, `BrokenFraction`. Nine tree meshes, each at rolled proportions and its own structural seed.
- **The umbrella is a `FoliageStyle.Tier`**: a low dome over a nearly flat underside (`TopFlatten`, `BottomFlatten`; the tuck alone cannot give a floor, it narrows the lower half to a cone), thin at the rim and thickest in the middle. The old crown, a dome flattened a little on top, is a bush on a stick. Several tiers go into **one mesh** (`FoliageMesh.Generate` appends to a caller's lists), so a two-tier tree is one canopy draw. The style also serves the bushes (`Crown`), scrub and treeline (`Scrub`).
- **The boughs fork twice**: from each bough's middle two thinner limbs splay to the rim, the spokes an umbrella shows under its crown. One bough per spoke reads as a stick under a lid.
- **The canopy's leaf mottle is `Fbm3` of world position**, so it has no seam and does not swim as the canopy turns with its tree; the trunk takes none.

### Leaf sprays (#610) and the wood they grow from (#782)

- `AcaciaMesh.Leaves` lays each foliage tier as `LeafSprays`: flat cards in three layers (`LAYERS`: dense on top, thinner under, a ragged fringe beneath that droops at the rim), about 750 a tier. `Acacia.fx` cuts eleven pairs of leaflets and the rachis out of each card (`LeafMask`) and turns them into the spray's plain outline once a pixel is wider than half a leaflet, so far crowns do not sparkle. It lights both sides, wraps the sun a little, darkens the lower layers' ambient (`LAYER_AMBIENT_FALL`) and lets the sun through when the lens looks towards it (`TRANSMISSION_*`), only where the shadow map says the sun reaches. The cards cast with their leaflets cut out, so shade is dappled.
- **Buckets**: the plate is `LowOnly`, the sprays `DetailOnly`; the Game's Low tier keeps the plate and the mottle, Medium and up draw the sprays. Cost: the play camera 2.61 to 2.79 ms (desktop, Testbed, `sceneseed=3`, `nopost`, 1600 x 900 ssaa 2, `fpscap=400`). The laptop is not measured.
- ⚠ **Every spray grows from wood.** Uniformly laid sprays measured **58 to 90 % of cards clear of any wood, the worst 3 to 8 card lengths off**: leaves in the air. `LeafSprays.HangOnTwigs`: twigs grow from the wood under the disc (an acacia tier's supports are the ends of the boughs and limbs that carry it, recorded by `BuildWood`; a baobab tuft's is its twig end) to points spread over the three layers, and the sprays hang along each twig's outer 70 % with every stem (the card's `u` = 0 edge, where the mask starts) on it. The layers keep their spray counts; an acacia twig carries 8 sprays, a baobab twiglet 24. The twigs go into the wood mesh, whose 16-bit indices they stay well inside (the test guards that too, #670's trap).
- Both trees are built by a static `Build` that returns the lists and a **skeleton** (every tube as the segment it was swept along) with no device, so `LeafAttachmentTests` holds every card's stem within its own length of the wood. The meadow's crowns are built this way (#697); the forest's and the aurora's conifers and the tropical palms are **still to be held to the same test**. Frame cost 3.29 to 3.34 ms (Testbed, fixed camera at 40,8,30, 1600 x 900 x 2).

### Bark, colour and haze

- **Per-instance colour in the shader**: `Acacia.fx` reads the instance's `Custom` vector (TEXCOORD5): `x` leans the draw's `DiffuseColor` towards its `DiffuseDry` (a per-plant dryness), `y` lifts or lowers brightness by up to 12 %, so one variant is several shades.
- **`BarkStrength`** ridges the wood with a two-octave `Fbm3` stretched up the trunk (zero on foliage and stone; the termite mounds take it for their flutes). The bark weathers: lichen in patches (`LICHEN_*`, scaled by `BarkStrength`; ⚠ a grey-green only a little paler than the bark, since at 0.36 it photographed as snow on the trunks) and the wood darker over the last `FOOT_HEIGHT` units above the root (`FOOT_DARKEN`).
- ⚠ **A plant hazes.** `HorizonHazeDistance` is handed over so a far tree fades towards the skyline as the ground under it does; without it the far scatter stood in full colour on a hazed hillside and read as pasted on. It is stretched by `PLANT_HAZE_REACH` (1.5): at the ground's own distance the treeline came back as pale lumps on a pale hill, and the references keep a far wooded edge darker than the field.

## The size of the trees (#663)

`AcaciaConfig.TreeSize` (1.7) scales every kind of acacia tree, not the bushes; `BaobabHeight` is 24 and `DoumPalmHeight` 18. From the play camera (`level=Zebra`) the umbrellas on the skyline were smaller than the campfires' flames in front of them. References put a mature acacia at about a third of a forty-metre platform's width: about 15 units tall and 20 across against the island's 52, its flat crown much wider than tall, the baobab near twice its height, the doums over it.

- **The plain stays as open as it was**: the trees went from 98 to 55 (`AcaciaConfig.Count` 97 with `BushFraction` 0.433, so the same 42 bushes), about 208 plants on a seed where there were 251.
- The prologue's shots aim at the plants' own figures and needed no change. Cheaper for the fewer trees: Game, `level=Zebra`, 1920 x 1080, `quality=high`, `nocap`, 201.9 / 202.5 to 205.7 / 205.7 FPS.

## The dressing (`SavannaDressingConfig`)

Each a planting with a count, a size and a colour (every count is a look decision):

- **Grass tufts** (`GrassTuftMesh`: a dozen tapering blades fanning up and out from one root, each ribbon emitted twice so it is two-sided under the ordinary cull). ⚠ A spiked `FoliageMesh` was tried first; at sixteen slices the spikes rounded into a yellow potato from any distance. Tufts are `DetailOnly`: the Low tier skips them, the most draws for the least silhouette.
- **Scrub** bushes in thickets round the groves.
- **Termite mounds** (`TermiteMoundMesh`, a lathe spire three to four times as tall as its column is wide, with an apron at the foot: the reference mounds' proportion).
- **Kopjes** (piles of three to six `RockMesh` boulders rolled rounder than the forest's, the biggest set into the ground, one or two on its shoulder, the rest half-buried round the foot, at `KopjeRockSize` 8: a landmark two or three acacias tall in the references) and **lone boulders** of the same meshes. Lichen is the foliage mottle at 0.35 on the stone.
- **Fallen trees** (`DeadwoodMesh`, a bent tapered log with capped ends and broken stubs).
- **The treeline** (`TreelineCount` dark `Scrub`-style masses and `TreelineTreeCount` far mature acacias in the band `TreelineMinRadius` to `TreelineMaxRadius`): the horizon closes on a wooded edge rather than a bare line, the cheapest thing that gives the scatter depth.
- **Baobabs** (`BaobabMesh`: a smooth bottle of a trunk widest at the foot, built by `TubeGeometry.AddRevolved` into the same lists as its limbs so the wood is one draw; five to seven thick limbs leaning well out, each forking into twigs and twiglets; ⚠ the first cut had short limbs and big tufts and read as balloons on a bottle) and **doum palms** (`DoumPalmMesh`, the one palm whose trunk forks, once and often again, each fork ending in a head of stiff fan leaves, `AddRibbon` blades spread 65 degrees in a rolled plane). Neither is the tropical beach's `PalmMesh`: a doum's silhouette is the fork, a coconut palm's the bow.
  - The baobab trunk is folded into six to eight flutes (`AddRevolved`'s `flutes`/`fluteDepth`, `TRUNK_FLUTE_DEPTH`) and its cap is leaf sprays on 70 % of the twig ends (`BaobabMesh.Leaves`, detail tiers; round tufts stay for Low).
