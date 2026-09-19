using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the savanna backdrop: open golden grassland rolling into low rises, combed by
    /// wind, dotted with acacia trees, ringed by campfires around the island, under the shared flock of birds.
    /// </summary>
    public sealed class SavannaSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Savanna;

        /// <summary>
        /// The gold-horizon dome the block chose wants sky behind it: scattered cloud keeps the warm horizon visible where a closed deck would grey it out.
        /// </summary>
        public SavannaSceneConfig()
        {
            Weather = WeatherPreset.Scattered;

            //The first scene to cast a shadow (#469) and the numbers the whole feature was tuned against:
            //0.9 leaves a shadow dark without reading as a hole, 260 units square round the camera puts the
            //edge outside every camera that plays here, and 2048 over that is 0.13 units a texel.
            Shadows = new ShadowConfig(strength: 0.9f, extent: 260f, mapSize: 2048);
        }

        /// <summary>Mean grass level, sitting at the island's foot (world origin clearing).</summary>
        public float LevelY { get; set; } = -13.5f;

        /// <summary>Height of the low rises the grassland rolls into with distance (flatter than the meadow).</summary>
        public float HillHeight { get; set; } = 34f;

        /// <summary>Radius of the flat clearing the island stands in around the world origin.</summary>
        public float ClearingRadius { get; set; } = 90f;

        /// <summary>Distance over which the terrain rises from the flat clearing into low rises.</summary>
        public float ClearingTransition { get; set; } = 130f;

        /// <summary>A soft undulation present even inside the clearing.</summary>
        public float ClearingRelief { get; set; } = 1.6f;

        /// <summary>Lush green grass (linear, dominant).</summary>
        public Rgb GrassSavanna { get; set; } = new(0.13f, 0.33f, 0.06f);

        /// <summary>Dry golden grass (linear, patches).</summary>
        public Rgb GrassDry { get; set; } = new(0.40f, 0.31f, 0.10f);

        /// <summary>Bare reddish earth (linear), in patches.</summary>
        public Rgb GrassBare { get; set; } = new(0.26f, 0.15f, 0.08f);

        /// <summary>The pale straw of the dry blade tips (linear, #281).</summary>
        public Rgb GrassTipColor { get; set; } = new(0.50f, 0.40f, 0.17f);

        /// <summary>How strongly the tips, the hollows and the blade strokes show (0 = the field is flat colour).</summary>
        public float GrassTipStrength { get; set; } = 0.8f;

        /// <summary>How big a bunch of grass is, in world units.</summary>
        public float TuftSize { get; set; } = 1.1f;

        /// <summary>How strongly the bunches show, each its own shade of dry gold or green (0 = none).</summary>
        public float TuftStrength { get; set; } = 0.6f;

        /// <summary>The sheen of a field seen edge-on (0 = matte).</summary>
        public float GrassSheenStrength { get; set; } = 0.3f;

        /// <summary>How brightly the grass glows looking towards the sun (0 = none).</summary>
        public float GrassTranslucency { get; set; } = 1.0f;

        /// <summary>How much sky fills the flats (ambient hemisphere strength).</summary>
        public float AmbientStrength { get; set; } = 0.7f;

        /// <summary>Wind direction combing the grass.</summary>
        public Vec2 Wind { get; set; } = new(0.86f, 0.51f);

        /// <summary>Distance over which the grass melts into the skyline.</summary>
        public float HorizonHazeDistance { get; set; } = 520f;

        /// <summary>Wind combing the grass: travelling band speed.</summary>
        public float WindRippleSpeed { get; set; } = 1.2f;

        /// <summary>Wind combing the grass: band frequency.</summary>
        public float WindRippleFrequency { get; set; } = 0.14f;

        /// <summary>Wind combing the grass: band strength.</summary>
        public float WindRippleStrength { get; set; } = 0.1f;

        /// <summary>Fine grass texture (normal-tilting height field) strength.</summary>
        public float GrassReliefStrength { get; set; } = 0.05f;

        /// <summary>Fine grass texture frequency.</summary>
        public float GrassReliefFrequency { get; set; } = 2f;

        /// <summary>
        /// Game trails (#451): bare red earth worn through the grass in wandering lines, the contour lines of
        /// one low-frequency noise — a network of paths that meander and fork the way tracks trodden across
        /// a plain do, with no plane wave in them (a sine would lay a trail in a row of identical bends).
        /// How bare the trail is, 0 = none (and the term is skipped in the shader).
        /// </summary>
        public float TrailStrength { get; set; } = 0.8f;

        /// <summary>How wide a trail is, as the width of the noise's zero band (in noise units, not world
        /// units — in world units it is about this over <see cref="TrailFrequency"/>, four to five units at
        /// the defaults, a track and not a road; 0.035 read as a road).</summary>
        public float TrailWidth { get; set; } = 0.02f;

        /// <summary>How closely the trails wander, in cycles per world unit — the lower, the longer their bends.</summary>
        public float TrailFrequency { get; set; } = 0.0045f;

        /// <summary>
        /// How far a path steps aside to go round what is standing on the plain (#476), in world units. 0
        /// leaves the trails running wherever the noise puts them, which is what they did until the owner saw
        /// one crossing a tree.
        /// </summary>
        public float TrailAvoidOffset { get; set; } = 16f;

        /// <summary>
        /// How far out from a plant's own edge the path starts bending, in world units. ⚠ <b>It is deliberately
        /// several times a trunk's width</b>: the owner's note is that people see a tree coming and are already
        /// going round it from a distance, so a repulsion that began at the bark would read as a kink at the
        /// last moment rather than as a detour.
        /// </summary>
        public float TrailAvoidReach { get; set; } = 34f;

        /// <summary>
        /// How big a thing has to be before a path goes round it rather than through it. Grass is walked
        /// through; a tree, a mound, a kopje and a fallen trunk are not. It is also what keeps the field cheap
        /// to build — the ground cover is most of what is planted.
        /// </summary>
        public float TrailAvoidMinRadius { get; set; } = 2f;

        /// <summary>Scattered acacia trees and low bushes.</summary>
        public AcaciaConfig Acacia { get; set; } = new();

        /// <summary>Everything else standing on the plain (#451): grass tufts, scrub, termite mounds, kopjes,
        /// fallen trees and the dark treeline at the horizon.</summary>
        public SavannaDressingConfig Dressing { get; set; } = new();

        /// <summary>The ring of campfires: their point lights and their visible flame billboards.</summary>
        public CampfireConfig Campfire { get; set; } = new();

        /// <summary>The shared flock of birds circling overhead.</summary>
        public BirdsConfig Birds { get; set; } = new();
    }

    /// <summary>Scattered acacia trees and low bushes over the savanna (real instanced geometry planted on the ground, #202).</summary>
    public sealed class AcaciaConfig
    {
        /// <summary>
        /// Number of scattered acacia trees and low bushes. 8 until #168, then 64; raised to 120 with #202,
        /// when the trees became real 3D geometry and a denser savanna was asked for — still dotted and open
        /// rather than a forest, but the plain no longer reads as empty. The scatter is instanced, so the
        /// count is a look decision rather than a budget one (the forest's 240 make the same point). 140
        /// since #451, with the bush share lowered: the scrub is a planting of its own now
        /// (<see cref="SavannaDressingConfig.ScrubCount"/>), so the bushes here are the few big ones.
        /// </summary>
        public int Count { get; set; } = 140;

        /// <summary>Fraction of the scatter that are low bushes rather than trees.</summary>
        public float BushFraction { get; set; } = 0.3f;

        /// <summary>Of the trees, the share planted young (<see cref="AcaciaKind.Young"/>) — slender, one small crown.</summary>
        public float YoungFraction { get; set; } = 0.2f;

        /// <summary>Of the trees, the share standing dead (<see cref="AcaciaKind.Dead"/>) — bare twigs, bleached.</summary>
        public float DeadFraction { get; set; } = 0.12f;

        /// <summary>Of the trees, the share storm-broken (<see cref="AcaciaKind.Broken"/>) — a crown to one side and a bare spar.</summary>
        public float BrokenFraction { get; set; } = 0.1f;

        /// <summary>Bleached wood (linear), pale and grey the way the references' dead trees are: a dead
        /// tree's whole silhouette and the fallen logs; a broken tree's spar is still bark.</summary>
        public Rgb DeadwoodColor { get; set; } = new(0.34f, 0.31f, 0.26f);

        /// <summary>Base half-width of a tree crown.</summary>
        public float Width { get; set; } = 6f;

        /// <summary>Base height of a tree billboard.</summary>
        public float Height { get; set; } = 9f;

        /// <summary>
        /// Inner radius of the scatter ring (clear of the island). 42 until #451; the umbrella tiers are wider
        /// and flatter than the old crowns, and a two-tier tree at 42 hung its lower plate over the play
        /// camera's shoulder as a featureless green lid across a corner of every frame. At 52 the nearest
        /// crown is a tree with boughs under it again. The tufts keep their own, nearer ring.
        /// </summary>
        public float MinRadius { get; set; } = 52f;

        /// <summary>Outer radius of the scatter ring.</summary>
        public float MaxRadius { get; set; } = 340f;

        /// <summary>
        /// Number of cluster centres plants gather around, so the scatter reads as groves rather than an even
        /// field. Raised 12 → 20 with the count (#202), keeping each grove its ~5–6 trees rather than packing
        /// the higher count into the same twelve clumps.
        /// </summary>
        public int Clusters { get; set; } = 20;

        /// <summary>Spread of plants around each cluster centre.</summary>
        public float ClusterSpread { get; set; } = 30f;

        /// <summary>Acacia canopy green (linear).</summary>
        public Rgb CanopyColor { get; set; } = new(0.09f, 0.17f, 0.05f);

        /// <summary>Drier yellow-green canopy (linear).</summary>
        public Rgb CanopyDry { get; set; } = new(0.22f, 0.22f, 0.07f);

        /// <summary>Dark brown trunk (linear).</summary>
        public Rgb TrunkColor { get; set; } = new(0.09f, 0.06f, 0.035f);
    }

    /// <summary>
    /// What stands on the savanna besides the acacias (#451): the plain's own furniture, planted by
    /// <see cref="SavannaScatter"/> on the same terrain and drawn on the same instanced path, sharing one
    /// occupancy list with the trees so nothing lands inside anything else. Every kind has a count and a
    /// colour and little more — where each stands is the scatter's own rule, the same as the trees'.
    /// <para>
    /// The counts were chosen by looking at the front end's orbit and the play camera, against the
    /// references rendered for #451: a savanna's character is in what is scattered across it, and a plain
    /// with only one kind of thing on it reads as a pattern however many of that thing there are.
    /// </para>
    /// </summary>
    public sealed class SavannaDressingConfig
    {
        /// <summary>Bunches of tall dry grass, the most numerous thing on the plain and the nearest to the
        /// island. Dropped at the Game's Low tier (they are the cheapest to lose and the most draws).</summary>
        public int TuftCount { get; set; } = 220;

        /// <summary>The tufts' ring: from just outside the fires to where they stop reading as more than dots.</summary>
        public float TuftMinRadius { get; set; } = 40f;
        public float TuftMaxRadius { get; set; } = 200f;

        /// <summary>The tufts' size, in world units (half-width; a tuft stands about as tall as it is wide).</summary>
        public float TuftSize { get; set; } = 1.5f;

        /// <summary>Dry straw (linear) — the tufts' colour, a shade paler than the ground's own tip colour.</summary>
        public Rgb TuftColor { get; set; } = new(0.42f, 0.33f, 0.13f);

        /// <summary>Low thorny scrub, in thickets around the groves and the odd one alone.</summary>
        public int ScrubCount { get; set; } = 70;

        /// <summary>The scrub's half-width in world units.</summary>
        public float ScrubSize { get; set; } = 2.2f;

        /// <summary>Grey-green (linear) — thorn scrub is duller than the acacias' foliage.</summary>
        public Rgb ScrubColor { get; set; } = new(0.085f, 0.125f, 0.055f);

        /// <summary>Termite mounds: tall spires of red earth standing alone in the grass.</summary>
        public int MoundCount { get; set; } = 14;

        /// <summary>A mound's height in world units (about half an acacia's); the foot is a third of it wide.</summary>
        public float MoundHeight { get; set; } = 4.5f;

        /// <summary>Red earth (linear), the same family as the ground's own bare patches but redder.</summary>
        public Rgb MoundColor { get; set; } = new(0.27f, 0.12f, 0.05f);

        /// <summary>Kopjes: piles of big rounded granite boulders, a few to the whole plain.</summary>
        public int KopjeCount { get; set; } = 5;

        /// <summary>Boulders per kopje, rolled between these.</summary>
        public int KopjeRocksMin { get; set; } = 3;
        public int KopjeRocksMax { get; set; } = 6;

        /// <summary>A kopje boulder's radius in world units (the largest; the rest are rolled down from it).
        /// A kopje is a landmark: the reference outcrops stand two or three acacias tall.</summary>
        public float KopjeRockSize { get; set; } = 8f;

        /// <summary>Single boulders lying alone in the grass, half-buried — the references have as many of
        /// these as they have piles.</summary>
        public int BoulderCount { get; set; } = 16;

        /// <summary>A lone boulder's radius in world units (the largest; the rest are rolled down from it).</summary>
        public float BoulderSize { get; set; } = 3f;

        /// <summary>Warm grey granite (linear).</summary>
        public Rgb RockColor { get; set; } = new(0.22f, 0.20f, 0.175f);

        /// <summary>Baobabs: a few, alone, each a landmark like a kopje (the owner's ask off the #451 plants sheet).</summary>
        public int BaobabCount { get; set; } = 3;

        /// <summary>A baobab's height in world units — taller than an acacia, and most of it trunk.</summary>
        public float BaobabHeight { get; set; } = 14f;

        /// <summary>Smooth grey-brown bark (linear), paler and greyer than the acacias' trunks.</summary>
        public Rgb BaobabColor { get; set; } = new(0.185f, 0.165f, 0.15f);

        /// <summary>Doum palms: the forking fan palm, planted in small clumps (the owner's ask off the #451 plants sheet).</summary>
        public int DoumPalmCount { get; set; } = 7;

        /// <summary>A doum palm's height in world units, to the top of its heads.</summary>
        public float DoumPalmHeight { get; set; } = 11f;

        /// <summary>Fallen trees lying in the grass.</summary>
        public int LogCount { get; set; } = 12;

        /// <summary>A log's length in world units; its thickness is a tenth of it.</summary>
        public float LogLength { get; set; } = 9f;

        /// <summary>
        /// The treeline: a band of dark low scrub masses and a few far acacias out beyond the plain, in the
        /// horizon haze, so the horizon is a wooded edge rather than a bare line — the reference savannas all
        /// close on one, and it is the cheapest thing that gives the scatter depth. Drawn opaque like the rest.
        /// </summary>
        public int TreelineCount { get; set; } = 150;

        /// <summary>How many far acacias stand in the treeline band (the mature variants, planted big).</summary>
        public int TreelineTreeCount { get; set; } = 28;

        /// <summary>
        /// The treeline's band: the outer part of the plain's own ring and a little beyond it. It stood at
        /// 380-520 first, past the acacias, and the haze took it — Acacia.fx fades a plant to the horizon
        /// colour by the ground's own rule, and at 450 out that is three-quarters of the way to sky, so the
        /// masses came out as pale lumps on a pale hill. At 300-400 they keep a third to a half of their
        /// dark, which is what a wooded edge in the haze looks like in the references.
        /// </summary>
        public float TreelineMinRadius { get; set; } = 300f;
        public float TreelineMaxRadius { get; set; } = 400f;

        /// <summary>A treeline mass's half-width in world units.</summary>
        public float TreelineSize { get; set; } = 16f;

        /// <summary>Near-black green (linear): distant scrub in its own shadow; the haze then lifts it towards the sky.</summary>
        public Rgb TreelineColor { get; set; } = new(0.04f, 0.065f, 0.03f);
    }

    /// <summary>
    /// The savanna's campfires: a ring of them around the island, each a real point light warming the grass
    /// and the stone, its own visible additive flame billboard, and since #282 the hearth it burns in - a ring
    /// of stones and the ground scorched under it. Positions are XZ; every Y is derived live
    /// — SavannaTerrainHeight(x, z) + <see cref="HeightAboveTerrain"/> on every read of
    /// SavannaCampfirePosition — so a GroundXZ or terrain edit in the editor moves the fires without a
    /// re-apply.
    /// <para>
    /// Everything but the position is shared by the whole ring: they are the same kind of fire, and a range
    /// or a colour per fire would be a config nobody could tune. What is <b>not</b> shared is the phase — see
    /// <c>SceneRenderer.CampfireColor</c> and <c>Flame.fx</c>'s <c>FlameSeed</c>, which give each fire its own
    /// clock and its own gait so the ring does not beat in unison.
    /// </para>
    /// </summary>
    public sealed class CampfireConfig
    {
        /// <summary>
        /// Ground position (XZ) of the <b>first</b> fire, just off the island. The rest of the ring is derived
        /// from it — see <see cref="Count"/> — so this one value still says everything it used to: how far out
        /// the fires stand, and which way the ring is turned. A config saved when there was only ever one fire
        /// therefore still places that fire exactly where it stood.
        /// </summary>
        public Vec2 GroundXZ { get; set; } = new(28f, -18f);

        /// <summary>
        /// How many fires ring the island, evenly spaced on the circle <see cref="GroundXZ"/> sits on and
        /// starting at it. Each is a real point light as well as a flame, so this is <b>capped at
        /// <c>SceneLights.MaxLights</c></b> (8, the shader's own array size) — eight fires spend the whole
        /// scene-light budget, which the savanna can afford because it is the only thing in it that lights.
        /// <para>
        /// One is the old single campfire and still valid. The ring exists because a lone fire lit one flank
        /// of the island and left the rest of the walk the gun makes in flat dome light.
        /// </para>
        /// </summary>
        public int Count { get; set; } = 8;

        /// <summary>Height above the terrain at that spot (the light/flame sits just above the ground).</summary>
        public float HeightAboveTerrain { get; set; } = 0.2f;

        /// <summary>Point-light range (quadratic distance falloff).</summary>
        public float Range { get; set; } = 32f;

        /// <summary>Width of the visible additive flame billboard (its half-width in world units).</summary>
        public float FlameSize { get; set; } = 2.3f;

        /// <summary>
        /// The billboard's height as a multiple of <see cref="FlameSize"/>. <b>Separate from the width on
        /// purpose</b>, and the reason is the game's own camera: it sits low, about level with the island's
        /// stone, while the fires stand on grass roughly five units below it. At the shader's old fixed 2.4
        /// the flames were 5.5 units tall and cleared the stone by barely half a unit — from the play camera
        /// all that showed of a fire was the tip of one. Growing <see cref="FlameSize"/> instead would have
        /// raised the tips by widening the fires into bonfires.
        /// <para>
        /// Six was chosen from the play camera against 4.5 and 8: at 4.5 a flame is present but slight, and at
        /// 8 it stops reading as a fire and becomes a beam standing in the sky. It does make a flame ~14 units
        /// tall — taller than the acacias, which are 9 — and that is accepted rather than overlooked: these
        /// fires are 33 units out and the eye judges them against the island beside them, not against a tree
        /// at the horizon.
        /// </para>
        /// </summary>
        public float FlameHeightScale { get; set; } = 6.0f;

        /// <summary>
        /// Sparks rising out of each fire (#468), from a shared buffer of 32 at most: each a small bright
        /// billboard on its own looping life off the wall clock, born in the base, cooling from yellow-white
        /// to red as it climbs, gone by the top of the flame. 0 draws none.
        /// </summary>
        public int SparkCount { get; set; } = 20;


        /// <summary>
        /// The scorched ground under a fire, as a multiple of <see cref="FlameSize"/> — so a bigger fire
        /// burns a bigger patch rather than standing in a hearth somebody sized once by hand (#282). At the
        /// default 2.0 against a 2.3 flame that is a ring 4.6 units across: char and ash inside, the grass
        /// coming back over the outer third of it.
        /// </summary>
        public float HearthRadiusScale { get; set; } = 2.0f;

        /// <summary>
        /// The ring of stones' radius, again a multiple of <see cref="FlameSize"/>. Just outside the flame's
        /// own base at the default 1.25 — stones inside it would be drawn through by the additive billboard
        /// and read as embers rather than as the hearth's kerb.
        /// </summary>
        public float StoneRingScale { get; set; } = 1.25f;

        /// <summary>Each stone's base radius, a multiple of <see cref="FlameSize"/>.</summary>
        public float StoneSizeScale { get; set; } = 0.36f;

        /// <summary>
        /// How many stones ring one fire. Seven rather than a round number so the ring has no axis of
        /// symmetry a camera can catch it on.
        /// </summary>
        public int StoneCount { get; set; } = 7;


        /// <summary>Cold ash the burnt ring is left in (linear), a shade paler and warmer than dead grass.</summary>
        public Rgb HearthAsh { get; set; } = new(0.165f, 0.152f, 0.140f);

        /// <summary>Char at the fire's own foot (linear) — nearly black, and warm rather than neutral.</summary>
        public Rgb HearthChar { get; set; } = new(0.036f, 0.029f, 0.024f);

        /// <summary>Dry basalt (linear), the same family as the island's own stone.</summary>
        public Rgb StoneColor { get; set; } = new(0.075f, 0.068f, 0.062f);

        /// <summary>
        /// How much of its own fire's light a hearth stone carries, as a fraction of the light the fire casts
        /// at the stone ring's distance. It is an ADDITIVE per-draw term rather than a ninth point light: the
        /// whole ring sits at one distance from one fire, so the arithmetic a point light would do per pixel
        /// is a constant per draw here, and it rides the fire's own flicker so a ring breathes with the fire
        /// it belongs to. What it therefore does not do is favour the inward-facing side of a stone over the
        /// outward one, which at this size (under a unit across, against a flame six times its height) is not
        /// what tells the eye it is a fire pit.
        /// </summary>
        public float StoneFirelight { get; set; } = 0.25f;

        /// <summary>Warm fire colour in LINEAR radiance, kept bright (over 1) so it casts real warm light, not a tint.</summary>
        public Rgb BaseColor { get; set; } = new(2.4f, 1.0f, 0.32f);
    }
}
