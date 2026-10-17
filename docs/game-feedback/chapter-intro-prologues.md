# Chapter intro prologues

Part of [The game: feedback](../game-feedback.md). The flight itself is in [The chapter intro](chapter-intro.md); the remaining scenes' shots are in [the other scenes' shots](chapter-intro-scene-shots.md).

A scene that opens on a **prologue** plays cut-together `IntroShot`s (a path and a look) before the tour's last leg. `Effects/IntroPrologues.For(SceneKind, IntroContext)` picks the builder (`IntroContext`: the renderer, the city, the two plantings, the dome's sun, the wall clock); `IntroPaths` holds the shared geometry. A declarative shot list was rejected: builders *search* their scene with reject-and-retry loops, which is logic, not data.

## What every prologue shares

- **A prologue is taken with a cut and skipped with one.** The blend cannot make a cut (it would glide the lens through the towers), so `Begin` sets it to 1 with a prologue and `TrySkip` drops it to 0.
- **After a prologue the tour flies only its last leg** (`ARRIVAL_SECONDS`, 4.5 s): the tour's scene stands lie on a circle round the arena that in a city runs through the towers.
- **One concrete object per shot, an establishing view first.** `[intro]` and `[tour]` name the shots, e.g. `[intro] block 'The Eruption' (9/13), cut 'the flank' 3.2s | 'the crater' 5.4s | cut -> map 43 out at 46deg -> game pose, 4.5s; 13.1s in all`. Totals run 11-16 s, the cities 22 (22.5 when the roof skim stands in for the rooftop), the meadow 22.9 (#893, its two orbits).
- **`IntroShot` moves without corners (#645)**: `Pose` extrapolates the look-ahead along the last leg (swapping in a direction swung the view in one frame); `At` is a Catmull-Rom (linear interpolation changed velocity at every point).

## The cities (#488, #780)

`Game/Effects/CityIntroShots.cs` builds four shots off the city being drawn (the streets lie ninety units under the island, out of any spline's reach). **Every path runs down a street's centre line**, which keeps it out of the towers.

- **The street**: 26 units up and 36 degrees down (the cars' and crossings' paint is flat and reads only from above), in the deepest of twelve rolled canyons.
- **The plaza**: an 80-degree orbit rising 32 to 54 over an open block. ⚠ **From 16 units up the trees read as holes** (discs painted on the paving); from 30 up they are a canopy.
- **The swing**: round a tower corner into the cross street.
- **The rooftop** (#780): an 85-degree arc round one dressed roof (`CityRooftops.Showcases.Best`), which must stand over every roof within 26 units (else the lens looks down a canyon at walls). The old roof skim is the fallback.
  - ⚠ **The whole mast stays in frame (#840).** Looking at the mast's lower third from a fixed height, a tall mast's beacon ran off the top edge for most of the arc. `FrameMast` bounds the look by the beacon's top (its radius included), held 6 % of the frame's height under the top edge, and the mast's foot, held 15 % over the bottom one, from every point of the arc, and keeps the lower-third look wherever it fits; where no look fits, the lens climbs in half-unit steps until one does (a higher lens sees the mast foreshortened). The foot's margin is the wider because the skip hint stands centred on the bottom edge for the whole intro, up to 12.2 % of the frame's height, and the mast is centred across: at 6 % the foot stood behind the hint. The projection is Hor+, so the window's shape does not change this.
  - The look stays on the mast's own axis (`RoofShowcase.MastFoot`): a mast stands on the roof's middle **cell**, which on a 2×2 roof is off the roof's middle. A roof carries one mast at most, because the neon extras fill only the cells the day's dressing left (#850): where the day's put none, `Best(neon: true)` reports the extras' mast.
  - The beacon is not phased to the shot: the city's beacons flash in eight clusters on the wall clock, each with its own period (`BeaconPeriod` times 0.75 to 1.02, so at most 1.63 s), and a 5-second shot holds at least three flashes of the mast's own cluster wherever the clock stands (`CityRooftopsTests` counts them; a period over 5/3 s would break it).
- ⚠ **The order alternates what the lens sees** (street, plaza, swing, rooftop; 4.5, 4.0, 4.0, 5.0 s, or the roof skim's 5.5 s where no roof qualifies): two canyon shots in a row from slightly different places read as a glitch. `CityIntroShotsTests` builds the prologue over the real dressed roofs (`CityRooftops.ArrangeShowcases`, no device) and holds a minimum shot length, no equal consecutive names, the swing never next to the street, a minimum turn or travel at every cut, clearance from the real towers' boxes, and the rooftop's whole mast inside the frame, its foot over the skip hint, at 3840×1600, 1920×1080 and 1600×900.

## The volcano (#530) and the aurora (#531)

- **Volcano** (`VolcanoIntroShots`, through `SceneRenderer.VolcanoGroundHeight`, a mirror of `Volcano.fx`): *the flank* (3.2 s, an arc at 0.6 of the cone's radius, lens on the vent; ⚠ at 20 up it was only orange bands) and *the crater* (5.4 s), a run **beside the axis, never over it** (straight above a fixed look-at there is no horizontal forward to build an up vector from), lifted over the rim so the bowl opens late. The weather fades in from the menu's over `WEATHER_FADE_SECONDS` (8 s, deliberate), so the flank plays under the previous scene's clouds.
- **Aurora** (`AuroraIntroShots`, off `ForestScatterRenderer.Scatter`'s instance arrays): *the wood* (3.4 s) is *found*, the best of 120 rolled runs measured against every conifer and snag (crown plus 3.8), trunks on both hands weighted (⚠ a bare count chose a grove's edge); *the snow* (3.4 s) drifts 1.7 over the snow, clear of trunks by seven (⚠ four put a crown on the lens). Heights come from `TerrainMirror.Forest`.

## Smoothness of the terrain-hugging shots (#645)

The mountains' pass, the desert's crest and the volcano's crater were "like a roller coaster". `Tests/BS3D.Tests/IntroShotSmoothnessTests.cs` holds the fixes (seen firing on each):

- **Speed**: 30-59 u/s down to about 19-25 (pass 85 units in 4.2 s, crest 72 in 4.0, crater 5.4 s).
- **Height**: `AridIntroPaths.Hug` averaged with one box, whose corners are accelerations. `passes` runs several narrower boxes (three make a piecewise-quadratic blur), half-widths summing to no more than the window, so the lens still never dips under a crest.
- **Pitch**: a lens looking along its travel tips into every slip face; `IntroShot`'s `pitchDamping` (0-1, default 0) levels the look (0.6 crest, 0.4 pass); the crater is pinned on the vent.

Not measured: the outback's approach and the `IntroGround` shots (`passes` and `pitchDamping` are the tools).
