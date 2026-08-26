using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the tropical island beach (#244): a ring of white-gold sand around the arena palm
    /// trees stand on, mossy rocks strung along the waterline, and beyond it a calm turquoise lagoon that
    /// the green, palm-lined shore of the island itself closes — the arena stands in the middle of a
    /// tropical island's lagoon rather than on an open sea.
    /// <para>
    /// Every colour is <b>linear radiance</b>, like every other scene config's, and the dials are grouped
    /// as named nested objects because the map editor's <c>PropertyGrid</c> is built
    /// <c>IgnoreCollections = true</c> — see <see cref="OutbackSceneConfig"/> for the reasoning in full.
    /// </para>
    /// </summary>
    public sealed class TropicalSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Tropical;

        /// <summary>
        /// The beach is a postcard by design (#244 chose its dome for the brightest blue in the set), and a postcard's sky is a few cumulus over turquoise water - closing it over would take away the thing the scene was built for.
        /// </summary>
        public TropicalSceneConfig() => Weather = WeatherPreset.Scattered;

        /// <summary>The land: the beach's profile from the island's foot, down through the waterline, out
        /// to the lagoon bed and up into the green shore ridge that closes the horizon.</summary>
        public TropicalTerrainConfig Terrain { get; set; } = new();

        /// <summary>
        /// The lagoon. Drawn by the very machinery the open sea is (<c>Sea.fx</c> unchanged) but pushed
        /// these values instead of the sea's — calmer swell, turquoise water — so the two scenes cannot
        /// drift apart in how water is drawn, only in what it is.
        /// </summary>
        public TropicalWaterConfig Water { get; set; } = new();

        /// <summary>The palms scattered over the dry sand, swaying on the wind.</summary>
        public PalmConfig Palms { get; set; } = new();

        /// <summary>The rocks strung along the waterline, green-capped with moss.</summary>
        public TropicalRockConfig Rocks { get; set; } = new();

        /// <summary>
        /// The shared flock of birds circling over the lagoon — the same buffer the savanna, the desert
        /// and the outback draw from, so this count only ever raises the size the four of them share.
        /// </summary>
        public BirdsConfig Birds { get; set; } = new();
    }

    /// <summary>
    /// The land, as one radial profile keyed on the distance to the waterline rather than the distance to
    /// the island: flat dry sand at the island's foot, a slope down through the waterline (which is where
    /// the profile crosses <see cref="TropicalWaterConfig.LevelY"/> by construction), the lagoon bed
    /// hidden under opaque water, and the far shore ridge rising out of the water all around — with one
    /// channel through it where the open sea reaches the horizon, so the lagoon is an island's lagoon
    /// and not a crater lake's.
    /// <para>
    /// The waterline itself is a mean radius wobbled by a few sine octaves of <i>bearing</i>, so the
    /// coast breaks into bays and headlands instead of reading as a circle. The whole height field is
    /// built from sines and hermite ramps only (no gradient noise), because
    /// <c>SceneRenderer.TropicalTerrainHeight</c> mirrors it on the CPU to plant the palms and the rocks
    /// on the ground the shader draws — the same contract <c>SavannaTerrainHeight</c> holds.
    /// </para>
    /// </summary>
    public sealed class TropicalTerrainConfig
    {
        /// <summary>Mean level of the flat dry sand at the island's foot — the same level the savanna's
        /// grass, the desert's sand and the outback's plain sit at.</summary>
        public float LevelY { get; set; } = -13.5f;

        /// <summary>A soft undulation of the dry sand, so the beach is not a snooker table. Kept small
        /// enough that no dip reaches the water level inland — a dry hollow below the waterline with no
        /// water standing in it reads as a mistake the moment the eye finds it.</summary>
        public float ClearingRelief { get; set; } = 0.9f;

        /// <summary>
        /// Mean radius of the waterline around the origin. The sand is flat and dry inside roughly this
        /// radius less <see cref="BeachRise"/>, and slopes under the water at it.
        /// <para>
        /// <b>Moving this is NOT how the lagoon gets into the play frame, and #268 established that the
        /// expensive way — by trying it.</b> The island's stone cap stands 5 units over the beach, and the
        /// play camera looks out across that cap: its far rim cuts the sight line, and everything at the
        /// sand's own level behind it is hidden out to a very large radius. Proved rather than modelled —
        /// the identical play-camera frame drawn with <c>arena=none</c> shows the lagoon plainly, and with
        /// the island drawn shows none of it. Then tested: brought in to 66 (with the palm ring following
        /// it), the water was <i>still</i> not in frame, because a nearer waterline is a nearer thing to
        /// hide. Tested again on a small map, in case the stand-off was the variable: also no water.
        /// </para>
        /// <para>
        /// What follows from that is worth more than the number: <b>from the play camera the only things
        /// visible past the island are things that STAND UP</b> — the palms, the far ridge, the sky. A flat
        /// beach and a flat lagoon are equally invisible whatever their radii, so the lagoon is a feature of
        /// the raised vantages (the front end's orbit, the drop cinematic, the editor) and the far ridge is
        /// what the play camera actually gets. That is where #268's effort went instead: making the ridge
        /// read as a green jungle shore rather than a beige dune line.
        /// </para>
        /// </summary>
        public float ShoreRadius { get; set; } = 100f;

        /// <summary>How far the waterline wiggles around its mean, as sine octaves of bearing. What
        /// turns the circular cut into bays and headlands; 0 is a perfectly circular beach.</summary>
        public float CoastNoise { get; set; } = 9f;

        /// <summary>How far inland of the waterline the sand is still flat before it begins to slope —
        /// the top of the beach. The visible dry band is this wide plus the wiggle.</summary>
        public float BeachRise { get; set; } = 14f;

        /// <summary>How far past the waterline the sand keeps sloping down to the lagoon bed. Hidden
        /// under the opaque water past the first unit or two; what it really sets is how gently the
        /// waves' foam can lap onto the shore.</summary>
        public float BeachRun { get; set; } = 22f;

        /// <summary>The lagoon bed's level, well under the water and invisible — the water is drawn
        /// opaque. Only its rise into the far shore ridge is ever seen.</summary>
        public float SeabedY { get; set; } = -19.5f;

        /// <summary>Mean radius at which the far shore begins to rise out of the water all around.</summary>
        public float RingRadius { get; set; } = 300f;

        /// <summary>How far the far shore's own coastline wiggles around its mean.</summary>
        public float RingNoise { get; set; } = 36f;

        /// <summary>Over how far the far shore rises from the bed to its crest. Wide, deliberately: a
        /// tropical island's skyline is a jungle ridge, rounded and vegetated, not a crater rim.</summary>
        public float RingWidth { get; set; } = 95f;

        /// <summary>How high the far ridge crests above the bed. What decides whether the scene has a
        /// skyline at all from the play camera's low lens — the Moon's highland-belt lesson.</summary>
        public float HillHeight { get; set; } = 28f;

        /// <summary>The bearing (radians) of the one channel cut through the far ridge, where the open
        /// sea reaches the horizon. Without it the ring closes and the lagoon reads as a crater lake.</summary>
        public float ChannelBearing { get; set; } = 2.4f;

        /// <summary>How wide the channel is, as the cosine exponent that carves it. Higher is narrower.</summary>
        public float ChannelSharpness { get; set; } = 10f;

        /// <summary>Dry sun-bleached sand (linear), the beach's dominant colour.</summary>
        public Rgb SandColor { get; set; } = new(0.42f, 0.37f, 0.29f);

        /// <summary>Paler sand in patches — shell and coral debris the surf leaves in bands.</summary>
        public Rgb SandColorPale { get; set; } = new(0.52f, 0.48f, 0.40f);

        /// <summary>The far ridge's jungle green (linear).</summary>
        public Rgb VegetationColor { get; set; } = new(0.055f, 0.13f, 0.035f);

        /// <summary>A drier, yellower green the ridge's canopy mottles towards.</summary>
        public Rgb VegetationDry { get; set; } = new(0.14f, 0.15f, 0.045f);

        /// <summary>How much the wind combs the ridge's canopy in travelling bands — the savanna's grass
        /// trick, at a distance only the far shore can be seen from.</summary>
        public float CanopyWindStrength { get; set; } = 0.10f;

        /// <summary>How strongly the ridge's canopy relief tilts the normal (world units of height).</summary>
        public float CanopyRelief { get; set; } = 0.35f;

        /// <summary>A fine ripple relief on the near sand (world units) — what the surf leaves behind.</summary>
        public float SandRelief { get; set; } = 0.045f;

        /// <summary>How much of the sky's hemisphere light fills the flats.</summary>
        public float AmbientStrength { get; set; } = 0.68f;

        /// <summary>The wind, a direction in the XZ plane. It combs the canopy and rides the waves; the
        /// palms' sway is aligned with it in <see cref="PalmConfig"/>.</summary>
        public Vec2 Wind { get; set; } = new(0.9f, 0.44f);

        /// <summary>World distance over which the land melts into the skyline. Must stay inside the
        /// terrain grid's half-extent (500), or the mesh's own edge shows against the dome.</summary>
        public float HorizonHazeDistance { get; set; } = 480f;

        /// <summary>The warm marine haze's own colour (linear), lit by the sky where it is applied.
        /// Kept paler and weaker than the outback's red dust: clear tropical air, not dust.</summary>
        public Rgb HazeTint { get; set; } = new(0.50f, 0.52f, 0.50f);

        /// <summary>How far the distance haze is carried from the dome's horizon colour towards
        /// <see cref="HazeTint"/>. Low for the same reason: the scene keeps its own colours.</summary>
        public float HazeStrength { get; set; } = 0.38f;
    }

    /// <summary>
    /// The lagoon's water. These mirror <see cref="SeaSceneConfig"/>'s water dials one for one (minus
    /// the spray, which a calm lagoon does not carry) because <c>DrawTropicalWater</c> pushes them into
    /// the very same <c>Sea.fx</c> the open sea draws with — the whole set every frame, so switching
    /// between the two scenes on a keypress (which applies no config) can never leave one scene drawing
    /// the other's water.
    /// </summary>
    public sealed class TropicalWaterConfig
    {
        /// <summary>Mean water level (world Y). Sits under the flat sand's level and over the bed's, so
        /// the beach profile crosses it on the slope by construction.</summary>
        public float LevelY { get; set; } = -15.5f;

        /// <summary>Deep-lagoon water colour (linear reflectance, multiplied by skylight in the shader).</summary>
        public Rgb WaterDeep { get; set; } = new(0.030f, 0.140f, 0.160f);

        /// <summary>The turquoise the lagoon takes — its signature colour.</summary>
        public Rgb WaterShallow { get; set; } = new(0.160f, 0.520f, 0.500f);

        /// <summary>
        /// How much of the body is <see cref="WaterShallow"/> regardless of which way the surface faces.
        /// <para>
        /// <b>This is why the lagoon was grey (#268), and the number above was never the fault.</b>
        /// <c>Sea.fx</c>'s own rule gives the pale colour only to the up-facing faces of the swell and caps
        /// it at half, then biases a calm patch darker still — which is right for water with nothing under
        /// it and wrong for a lagoon, so the basin was drawn mostly in <see cref="WaterDeep"/>, a dark navy.
        /// Measured across the water band: (140, 146, 133), blue below red, against a turquoise that was
        /// sitting in the config the whole time. A lagoon's bed is a few units down across the whole basin,
        /// so it is the shallow colour wherever you look at it. The open sea keeps this at 0 and its water
        /// is bit-for-bit unchanged.
        /// </para>
        /// </summary>
        public float ShallowBias { get; set; } = 0.78f;

        /// <summary>Dominant swell height. Well under the sea's: a lagoon inside a reef is sheltered
        /// water, and the waves also fade to flat approaching the shore (Sea.fx's calm band).</summary>
        public float WaveAmplitude { get; set; } = 0.35f;

        /// <summary>Crest sharpness (0..1 Gerstner steepness).</summary>
        public float WaveSteepness { get; set; } = 0.85f;

        /// <summary>Multiplier on the dispersion-derived wave speed.</summary>
        public float WaveSpeed { get; set; } = 1.0f;

        /// <summary>Camera distance at which the waves begin fading towards flat.</summary>
        public float WaveFadeStart { get; set; } = 340f;

        /// <summary>Camera distance at which the waves are fully flat (clean hazed horizon).</summary>
        public float WaveFadeEnd { get; set; } = 780f;

        /// <summary>Fine per-pixel wind-chop peak height in world units.</summary>
        public float ChopAmplitude { get; set; } = 0.12f;

        /// <summary>Wind-chop ripples per world unit.</summary>
        public float ChopFrequency { get; set; } = 1.1f;

        /// <summary>Wind-chop scroll speed.</summary>
        public float ChopSpeed { get; set; } = 1.4f;

        /// <summary>Wind direction the swell and chop travel along (roughly a unit vector in XZ).</summary>
        public Vec2 Wind { get; set; } = new(0.9f, 0.44f);

        /// <summary>Strength of the sharp sun glint on the water.</summary>
        public float SunGlintStrength { get; set; } = 9f;

        /// <summary>Sun-glint specular power (higher = tighter spark).</summary>
        public float SunGlintPower { get; set; } = 500f;

        /// <summary>How far the wave Jacobian must fold before foam shows (nearer 1 = more foam).</summary>
        public float FoamJacobianThreshold { get; set; } = 0.55f;

        /// <summary>Strength of the fold-driven foam. Lower than the sea's — a sheltered lagoon whitecaps
        /// rarely; what foam it has rides the shore where the swell dies.</summary>
        public float FoamStrength { get; set; } = 0.6f;

        /// <summary>Where on a crest the height-driven foam begins (0..1).</summary>
        public float FoamCrestStart { get; set; } = 0.65f;

        /// <summary>Strength of the crest-height foam.</summary>
        public float FoamCrestStrength { get; set; } = 0.35f;

        /// <summary>Foam colour (linear).</summary>
        public Rgb FoamColor { get; set; } = new(0.85f, 0.92f, 0.95f);

        /// <summary>Strength of the subsurface glow when the sun is behind a wave. High for turquoise
        /// water — light through a thin crest is the most tropical thing this scene can do.</summary>
        public float SssStrength { get; set; } = 0.9f;

        /// <summary>The colour light takes coming through the water (linear).</summary>
        public Rgb SssColor { get; set; } = new(0.10f, 0.42f, 0.38f);

        /// <summary>Distance over which the finite water fades into the skyline. Only the channel and
        /// the lagoon's middle distance are ever seen at anything like this range.</summary>
        public float HorizonHazeDistance { get; set; } = 680f;
    }

    /// <summary>Scattered coconut palms over the dry sand, swaying on the wind (#244).</summary>
    public sealed class PalmConfig
    {
        /// <summary>Number of palms. The scatter is instanced and clumped into groves, so this is a look
        /// decision rather than a budget one — the savanna's 120 acacias make the same point.</summary>
        public int Count { get; set; } = 110;

        /// <summary>Base height of a palm to the crown (the trunk's own curve and the instance scale
        /// vary around it).</summary>
        public float Height { get; set; } = 12f;

        /// <summary>Base trunk radius at the crown's end (the root flare is a multiple of it).</summary>
        public float TrunkRadius { get; set; } = 0.34f;

        /// <summary>Base frond length — how wide the crown reads.</summary>
        public float FrondLength { get; set; } = 5.2f;

        /// <summary>Inner radius of the scatter ring (clear of the island's coping).</summary>
        public float MinRadius { get; set; } = 36f;

        /// <summary>Outer bound of the scatter. Palms are additionally planted only on dry sand (a height
        /// test against the water level, which follows the wiggling waterline), so a few of the outermost
        /// candidates land in the sea and are re-rolled rather than planted in the surf.</summary>
        public float MaxRadius { get; set; } = 88f;

        /// <summary>Number of cluster centres the palms gather around, so the beach reads as groves
        /// rather than an evenly spaced plantation.</summary>
        public int Clusters { get; set; } = 22;

        /// <summary>Spread of palms around each cluster centre.</summary>
        public float ClusterSpread { get; set; } = 22f;

        /// <summary>The crown's green (linear).</summary>
        public Rgb FrondColor { get; set; } = new(0.075f, 0.155f, 0.045f);

        /// <summary>A drier, sun-bleached green a variant's crown takes in part.</summary>
        public Rgb FrondDry { get; set; } = new(0.17f, 0.175f, 0.06f);

        /// <summary>The trunk and the dead frond skirt's warm grey-brown (linear).</summary>
        public Rgb TrunkColor { get; set; } = new(0.10f, 0.078f, 0.052f);

        /// <summary>How far the crown's fronds sway on the wind, at the frond tips. Keyed up from zero
        /// along each frond, so the trunk stands still and the crown moves — a palm whose whole body
        /// waves reads as a kelp.</summary>
        public float SwayStrength { get; set; } = 0.45f;

        /// <summary>How fast the sway oscillates.</summary>
        public float SwaySpeed { get; set; } = 1.3f;
    }

    /// <summary>
    /// The waterline's rocks: boulders strung where the surf reaches, each capped with a green moss
    /// crown — the issue's own words, "rocks with green tops", and the cap is a second mesh over the
    /// stone rather than a shader trick, so the moss can carry its own colour and its own ragged edge.
    /// </summary>
    public sealed class TropicalRockConfig
    {
        /// <summary>Number of rocks.</summary>
        public int Count { get; set; } = 30;

        /// <summary>Base half-width of a boulder.</summary>
        public float Radius { get; set; } = 1.7f;

        /// <summary>Base height of a boulder above the ground it sits on.</summary>
        public float Height { get; set; } = 1.4f;

        /// <summary>Inner bound of the scatter (clear of the island).</summary>
        public float MinRadius { get; set; } = 32f;

        /// <summary>Outer bound of the scatter. Rocks are planted only in the band around the waterline
        /// (a height test straddling the water level), which follows the wiggling coast — this only keeps
        /// the very rare re-roll from landing on the far shore.</summary>
        public float MaxRadius { get; set; } = 118f;

        /// <summary>The stone's weathered grey-brown (linear).</summary>
        public Rgb StoneColor { get; set; } = new(0.115f, 0.10f, 0.085f);

        /// <summary>The moss cap's green (linear).</summary>
        public Rgb MossColor { get; set; } = new(0.045f, 0.10f, 0.03f);
    }
}
