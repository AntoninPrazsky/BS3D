# Special ball kinds

Part of [Rendering](../rendering.md).

Each `BallKind` that draws differently has its own `BallShading` and a **bucket region** in `BallRenderSet` (what they do: "Special ball kinds" in `docs/game-session.md`).

- **Why a region, not an instance flag:** technique, emission and pulse depth are per-*renderer* uniforms (#252). Renderers are put back with `ApplyStyle`, not by restoring fields.
- **Colourless regions** (`LodCount` buckets, never loaded in the cannon): rock (`DrawRocks`), clear glass (`DrawHollow`), bomb (`DrawBombs`), zap (`DrawZaps`), acid (`DrawAcids`): kinds whose colour nothing may read. **Coloured planes** (`TYPE_COUNT x LodCount`): frozen, infectious, gravity, heavy: colours the player plans around. The wildcard and the Cutter (a power-up round, not a special ball) have live/still regions.
- **Order:** opaque kinds first (rocks before the style's passes); clear glass **last** of the balls, repeating the bubble's two-pass states (an opaque level has no `DrawShell` to set them). `DrawHollow` leaves `DrawnCount` alone.
- **Each kind must be told apart from the nearest existing look** ("collision to solve"). Rock and clear glass are not in `BallStyle`: "that one is different" must survive all ten materials.

## Rock (`BallShading.Stone`, #324, #340)

No type colour: granite (warm grey `StoneBody`, a lot of the read is value), gradient-noise flecks and lumps, matte.

- **The silhouette is not a circle**: `InstancedModelStone` has its own vertex shader, as only the frozen ball's and the bomb's do too (a gem is *cut*, a rock *broken*). It **carves inward only** (a bigger ball would push into its neighbour's cell), its **normal is analytic** (the sphere's would light a carved ball like a round one), its field is coarser than the surface lumps; five fracture planes and a crack (#623).
- ⚠ **Every rock carries the same carving**, and physics never turns a hanging ball (0.05 degrees mean over 60 s on Cairn), so `RockTurns` folds a fixed per-cell turn into the world matrix (#356); it cannot reach the simulation.
- ⚠ **It needs emission** (`STONE_EMISSION` 0.45, no pulse: the one ball that does not breathe, which names it across a field). With none it came out black next to the 8-ball: the player looks *up* at the unlit side, which every other ball lights by emission plus `TranslucencyStrength`. `StoneEnvironment` 0.55 (0.20 read near-black in the editor; the blurred reflection is most of a rock's outdoor light). Give the draw real effect params (`null` fell back on `DefaultLighting`'s dim blue).
- Flecks and lumps are `GradientNoise3` (a product of sines is a lattice of dimples however warped; rectified octaves are rows of ridges). Body 0.54: it cannot just be darkened (0.42 read as the 8-ball).

## Clear glass (`Hollow`, #325, #624)

The ball with no colour *yet*. A **cavity**: `HollowPS` casts the eye ray (`EyeRayVS` in `BallCommon.fxh`) at a hollow of `HollowCavityRadius` 0.62 and draws only the grazing wall line and a faint dimming. **The collision is `Bubble`**: this says **emptiness** (`HOLLOW_BODY_OPACITY` 0.06; no dye, gores or film). ⚠ **Its rotation cue is designed**: an undyed shell is a function of eye and sky and draws a spinning ball as a still one, so it carries a **cast seam** (`HollowSeamFrequency`, two, object space) in the *normal*.

## Bomb (`Bomb`, #326, naval mine #625, #341)

Matte warm near-black casing (`BombCasing`: a neutral near-black is Type8), two glowing red seams at a slant (`MineSeamNormalA/B`: square to the axes a face-on mine is a "+", a crosshair), a red eye (`MineEye`, never band-limited), eight studs as geometry (`BombVS`). Emission is multiplied by the **seam mask**, not the ball (a whole ball flashing would strobe).

- **The casing is the full ball and the studs stand past it** (`MineStudRise` 0.13), the one ball figure that leaves its cell, on the owner's ruling (#860). ⚠ Cut back to 0.87 so the studs could stop at 1 (the rock's and the frozen ball's inward-only rule), the bomb read as a **smaller ball** with a gap round it: face-on, its silhouette's area-equivalent radius was 0.87 of an ordinary ball's beside it, now 1.00. A level-placed mine's studs point within 10 degrees of its eight cross-level neighbours, so a stud sinks into an occupied one by about its rise, inside the crevice.

- **It reads as armed by its beat**, the opposite end of the rock's channel (`BOMB_PULSE_DEPTH` 1.0, `BOMB_EMISSION` 1.25 vs the cluster's 0.55). ⚠ Unreadable at play distance at first: grooves too narrow; **the band limit fading six times too early** (Nyquist for a band spanning `pi / BombBandCount` is 0.5); a beat too fast, because **`Heartbeat`'s lit window is a fixed fraction of its cycle, so faster blinks *shorter*** (`BOMB_PULSE_SPEED` 0.5). It must blink **red**: green made it amber (`BombCharge` green 0.15).
- ⚠ **`DrawBombs` restores the pulse SPEED by hand**: `ApplyStyle` cannot (set once at build; `DrawPlane` returns early when nothing is loaded), so a bomb left in it puts the cluster on its heartbeat.
- ⚠ **Never phase-sweep a pulsing ball with `F5`** (it pins the pulse clock: 20 frozen captures spanned 109-115 codes, 12 unfrozen 96-142).
- ⚠ **Confirm the exe is running the shader on disk**: three rounds of captures came from a Debug Testbed while building `-c Release`, and MGCB skips an `.fx` whose `.xnb` is newer. `bin\<tfm>\Content\Shaders\InstancedModel.xnb` must be newer than the `.fx`; tinting a constant green proves it.

## Zap (`Zap`, #327, #626)

One discharge branching from a hot node (`ZapRootAxis`): six bolts with forks, band-limited on their width, converging to `ZapFarGlow`; two trees (node and antipode) so every side shows one. Collisions are structural: **plasma** glows all over in its own colour with drifting filaments; the **bomb** is warm latitude *bands*, slow and deep; this is cold oblique great circles, fast and shallow. **Fast is right here though wrong for the bomb** (the read is the arcs kept lit by `ZapRestingGlow`). Arcs combine with `max` (a sum doubles light at crossings), `ZapArcWidth` 0.115 (wider merged into a cow pattern).

## Acid (`Acid`, #328, #627)

A dark dry shell whose lower half is full of glowing acid: the only special with an **axis**, via the **world** normal (`-n.y`). ⚠ **A fill, not stripes** (nine lanes were a watermelon), and **a fill does not band-limit** (a half-disc is a half-disc at any size). `AcidFillLevel` 0.5, slow (`ACID_PULSE_SPEED` 0.8). Collision: the leafy green `Type2`. ⚠ `line` is an HLSL keyword. ⚠ The foam rolls on the integer `HashCell` (#674): `Hash21(cell + step x 7)` repeated every 100 cells and froze after ~50 minutes on the wall clock.

## Frozen (`Frozen`, #329, #628)

A **shape**: `FrozenVS` scales the sphere to a superellipsoid (`FrozenCubePower` 4.5), **inward only** (corners touch the 0.5 sphere, faces at 0.74; physics stays a sphere); the normal is exact (`g = |d|^(p-1) sign(d)`). Clear ice with the ball **ray-cast inside** (`EyeInObjectSpace`, `FrozenCoreRadius`, `FrozenIceAbsorb`), frost at edges and face patches, one crack. It takes the type colour (a plane). **The collision is `BallStyle.Ice`, the worst**: every ball there is already frosted with its colour inside, so only **shape** (the only flat faces) separates them. ⚠ Frost frequency is on a **unit direction**: 26 is four cycles across the ball and read as diagonal corduroy; use ~41 (cf. `IceFrostFrequency`). ⚠ The edge band is on `top` (1 at a face centre, ~0.71 mid-edge): `FrozenFrostEdgeTop` 0.72. Quietest uniforms (`FROZEN_EMISSION` 0.42, `FROZEN_PULSE_DEPTH` 0.22): "not yet".

## Infectious (`Infectious`, #331, #629)

The ball's own colour with one **crust** eating one side (olive lumpy cap round an object-space axis, wet edge, spore heads, a small world-space bead). **The colour is the counterplay.** Collision: the acid (both green and wet): acid is a dark shell running downward, this a film creeping in object space. ⚠ **The crust faces down and forward** (a looking-up player never sees a hanging ball's top). ⚠ Three bugs each cost the counterplay: the threshold assumed a field mean of 0.5, but a rectified-sine sum's mean is `2/pi` = **0.64**; a soft edge (`InfectEdge` 0.12) smeared the film everywhere (binary 0.05); the heartbeat **substituted** the slime colour for the ball's (all thirteen came out dark green): it is `lerp(primary, slime, film)`. The loudest uniforms (slime says hurry).

## Gravity (`Gravity`, #332, #630)

The ball's colour **swirled**: a logarithmic spiral into a dark eye (`GravityArms` 5, `GravityWind` 4), the direction being the message. ⚠ **Concentric rings read as a bullseye ("aim here")**. Light bands lift to a paler hue, not white (`GravityStreakPale`); the eye (`GravityEyeRadius`) is never band-limited. ⚠ **Space bands in the disc's own radius** (`sqrt(1 - facing^2)`), not the grazing term (which crowds them into the limb). The lens is a fake bending the normal the environment reflects about (stays opaque). Collisions: plasma and gem.

## Heavy (`Heavy`, #333, #631)

⚠ **The look must not draw the mass**: the branch hanging lower does; the look says *which* ball is heavy on a still frame. Sand-cast iron with the colour in soft patches (`HeavyStainCover` 0.78), a mould flash (`BandCoverage`, so it converges where `SeamLine` fades), a boss on the **-axis** underside, `GradientNoise3` grain. A casting, not a polish, with **its own albedo-tinted reflection** (`HeavyMetalSheen`; a dielectric reflects ~4 % face-on). ⚠ Iron at 0.235 photographed black: `HeavyIronColor` is a mid grey and the matte finish makes it read dark.

## Wildcard (`InstancedModelWildcard`, #632)

A glossy marble of the two colours `WildcardCycle` crosses between: the arriving colour (`PatternSecondaryColor`) spreads along swirled veins as `WildcardProgress` runs 0 to 1 (a dither would say "not there"). **Every crossing sweeps back** (#821): `WildcardCycle.Returning` flips per crossing (not on the wrapping colour index), `WildcardReturning` turns the sweep. One instance per wildcard in `WILDCARD_REGION_START`, drawn by `DrawWildcards`, which restores `StillEmission` and `PatternSecondaryColor`.

## The Cutter (`Cutter`, #766)

The Cut power-up's round, so loaded and in flight only. A dark polished steel ball with a **circular saw through it**, from the design references (both models drew a steel ball sliced by a saw whose teeth glow white-hot). Two techniques on one frame: `InstancedModelCutter` on the sphere, then `InstancedModelCutterBlade` on `SawBladeMesh` (a disc of 24 hooked teeth, tips at `TIP_RADIUS` 0.585 under the bore's 0.6, two chipped, every tip hashed, #640), one draw for every blade. Its light is `BasicEffectParamsProvider.CutterAccent`, the draw's tint, whitened towards the hottest parts.

- **The frame is built on the CPU each draw** (`BallRenderSet.CutterFrame`): the instance's place and scale, its Y axis the blade's normal **facing the eye, tipped back towards level** (`CUTTER_BLADE_LEVEL_BIAS`), turned about it on the wall clock (`CUTTER_SPIN_LOADED` 1.2 rad/s in the bore, `CUTTER_SPIN_FLYING` 18 in flight). The round's own turn is dropped: a loaded round carries the barrel's pitch, a fired one the simulation's, and a blade following either would snap at the shot. Written over the bucket's matrices (the rims' walk, the shadow and the velocity pass draw a sphere, which the turn leaves where it was).
- ⚠ **In the bore only the ball's cap shows**: the barrel hides its equator, so a level blade (tried first) was invisible there. So the **ball's face carries the saw's face**, a ring of 16 glowing hooked teeth round a hub (a washer, three bolts, the arbor's hole) at the pole the eye looks at, and the blade's own teeth stand round the outline where nothing covers them (flight, a close view). Under a pixel the teeth settle into a ring.
- **It holds still** (#252): no heartbeat (`PulseDepth` 0) and nothing reads `PulseTime`; what moves is the whole round turning as one saw. A dark ball with a bright dot in its middle read as an eye (photographed): the arbor's hole is dark.
- **The Potato path**: `PotatoKindLookFor` makes the ball steel with a trace of the accent, its face the gravity well's figure in the accent (a ring round the middle of the face, `SetPotatoKindFigure`), and the blade brighter steel glowing in the accent through `PotatoBall`. No GL shader changed: the ring has no teeth there, the blade has.
- Cost: nothing measurable. A cutter loaded on Teapot, `High`, 3840x1581 windowed, uncapped, desktop (RDT-PC): 9.24-9.29 ms with the zap's look, 9.12-9.17 ms with its own (a third run 9.77). The blade is 864 triangles for at most two rounds; the techniques declare no uniform, so nothing after `BallCutter.fxh` moved in the constant buffer.

## Crossings between looks

- **Clear to colour (#325):** the ball is its new colour at once; the *drawing* crosses over `ClusterCollector.COLOUR_FADE_SECONDS` (0.35): `BallDrawFrame.Route` puts it out **twice**, new colour at `-d` and hollow at `+d`, and the two signs partition its pixels exactly: no blend, sort, program or channel.
- **Thaw** is the same crossing but needs **its own timer on `PhysicsBall`** (a duration cannot name the second bucket). **Infection and hardening** share one timer, told apart by the *new kind*.
- **Wildcard lock-in (#437)** is the only crossing with both ends in ordinary colours, so it carries `PhysicsBall.LockFromType` over `ClusterCollector.LOCK_FADE_SECONDS` (0.5, the least contrasty). It runs only when the colour changed; the outgoing colour is the ball's `Type` before the resolve overwrites it.
