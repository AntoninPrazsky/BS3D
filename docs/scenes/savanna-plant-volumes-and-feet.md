# The savanna's plant volumes, grounding and tree feet

Part of [The scenes](../scenes.md). Continues [The savanna's trees and plain](savanna-trees.md).

## Plants keep apart by volume (#653)

`ScatterSpacing`'s footprint is one radius at 0.72 of the sum, so crowns interlace by design and it knows nothing of a thin trunk or of height: a bush under a high crown was held apart as if it met it, while two crowns that did meet could interpenetrate.

- **Every thing on the plain states a few slabs** (`PlantVolume.cs`: `Slab`, `PlantVolumes.Clearance`): vertical cylinders with a bottom, a top and a radius at scale 1 from the pivot (a trunk to its fork, one per crown tier, a foliage blob, a mound, a boulder, a kopje, a baobab's foot, taper and crown, a doum clump). Two things clip when slabs **meet in height** and their axes are closer than the radii sum less `TOLERANCE` (15 % of the smaller radius).
- **A site is the best on the worse of the two answers** (footprint, volume): the groves stay and the path-first ranking (#476) is untouched. The meshes state their own slabs (`AcaciaMesh.Volume`, `BaobabMesh.Volume`, `DoumPalmMesh.Volume`), so shape and clearance cannot part; the dressing's are in `SavannaScatter`.
- **The plain's retry budget is `SAVANNA_TRIES` (32), not `ScatterSpacing.TRIES` (8)**: at eight, a plant in a dense grove often settled for the roomiest site, still an overlap. The planting never drops a plant, and **re-rolls at an unchanged seed** when this rule changes.
- Over `sceneseed` 0 to 4, overlapping pairs fell from 7-19 a seed to **0**; `PlantVolumeTests` checks the arithmetic.

## Trees stand on the ground (#658)

The levitation was the **pivot's height, not the ground mirror** (`mirrorcheck` passes). Trees were planted at the ground under the trunk's *centre*, but a trunk stands on a disc (`BaseRadius`) and on a slope the downhill half hung in the air (up to 1.1 units). `SavannaScatter.LowestUnder` plants each acacia, baobab and doum palm at the lowest of eight points round its ring and the centre: downhill touching, uphill buried. The forest's and meadow's scatters plant by the centre rule, unchecked.

## The trees' feet (#670)

A trunk cut flat at the pivot met the plain as a clean circle, a pole on a lawn. Each foot is rolled off its own dice, so no site moved:

- **The wood goes on into the ground**: the acacia's four to six surface roots (`ROOT_REACH`; the tip dives `ROOT_DIVE` of its reach **in proportion**, ⚠ because a fixed depth ended above the plain on a slope's downhill side), the baobab's bottle flaring into lobes (`BOTTOM_RING`, flutes deepened by `FOOT_FOLD`; tubes stuck on read as boards), the doum's cone of exposed roots (`SKIRT_*`).
- **Grass grows up round the trunk**: tufts planted after the plain is final on their own stream (`FOOT_STREAM`), refused in a path's worn middle (`TRAIL_REFUSE`), `DetailOnly`.
- **The lower bark is the soil's colour**: `Acacia.fx` carries the plain's bare earth (`GrassColorBare`, per draw `FootEarth`) up about 0.9 units, on **standing trunks only** (`ScatterBucket.Earthed`), ⚠ because a fallen log lies inside the band and went red. The scatter hands the shader the ground's height above the pivot at the trunk (`Custom.z`): on a slope the pivot lies up to a unit under the plain and the earth was drawn underground.
- **A heap of earth round every foot** (`SoilHeapMesh`, `HEAP_REACH`, `HEAP_HEIGHT`; a flatter apron for a baobab), its rim under the plain; built at unit radius and scaled uniformly so normals stay true.
- ⚠ **A baobab's leaves overflowed 16-bit indices**: a crown of 26 or more leafy twig ends passes 65,536 vertices and the cards past it wrapped onto the first ones, a sixth of the crown never drawn. Sprays use `int` indices, uploaded 32-bit when needed; the 16-bit `Upload` throws past 65,536.

