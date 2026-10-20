using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the aurora backdrop (#205, rebuilt in #963): a moonlit snowfield in a boreal clearing under a
    /// great folded aurora. Every colour is <b>linear radiance</b>.
    /// <para>
    /// In both scene families at once, after the Moon: solid terrain (<see cref="SceneRenderer.IsSolidTerrainScene"/>
    /// — real snow, its own planting of the shared <see cref="ForestScatterRenderer"/> wood, the island's footprint cut
    /// out, the dark pit shaft backing the drain) <b>and</b> sky-replacing (<see cref="SceneRenderer.ReplacesSky"/> —
    /// no dome, its own night sky, Moon and light rig). See docs/scenes/aurora.md.
    /// </para>
    /// <para>
    /// The value structure is the references' (#963, both models): the moonlit snow is the brightest surface in the
    /// picture and its shade is blue, the spruces are dark and heavy with snow, the sky is a deep blue with a paler
    /// glow along the horizon, and the aurora is a few distinct curtains with dark starry sky between them.
    /// </para>
    /// </summary>
    public sealed class AuroraSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Aurora;

        /// <summary>
        /// No weather (no dome, no cloud deck), and the Moon's cast shadows: the spruces' long shadows across the
        /// snow are the one thing every reference of the scene draws that a lit floor cannot fake.
        /// </summary>
        public AuroraSceneConfig()
        {
            Weather = WeatherPreset.Clear;
            Shadows = new ShadowConfig(strength: 0.88f, extent: 300f);
        }

        /// <summary>
        /// The clearing's hills and the planted wood: the forest's own field (<c>ForestGround.fxh</c>) and the shared
        /// <see cref="ForestScatterRenderer"/>, planted a second time here under the night's light so a change to one
        /// wood does not move the other's trees. The snow's own relief rides on top of this field
        /// (<see cref="Snowfield"/>), and the wood is planted on the two together (<c>TerrainMirror.AuroraBase</c>).
        /// <para>
        /// <b>Pure conifer</b> (<c>ConiferFraction</c> 1): a shrunken broadleaf read as "little mushrooms with a brown
        /// leg and a green cap" (#462). <b>Boreal spires</b> of many ragged whorls (the count of whorls says "boreal" far
        /// more than proportions; a regular stack reads as a pagoda), standing two to four times the height of the gap
        /// between them, the stand on long bare stems (#647). Snow lies on the whorls (<c>CrownSnow</c>), and since
        /// #963 on the dead wood and the boulders too (<c>TopSnow</c>). The floor colours here are read only by the
        /// Potato path's stand-in (<c>PotatoBackdrop.Aurora</c>); the desktop's snow is <see cref="Snowfield"/>'s.
        /// </para>
        /// </summary>
        public ForestSceneConfig Terrain { get; set; } = new()
        {
            ForestColor = new(0.58f, 0.64f, 0.76f),
            ForestColorDark = new(0.26f, 0.30f, 0.38f),
            TreelineColor = new(0.005f, 0.011f, 0.009f),
            TreelineStrength = 0.7f,
            NeedleReliefStrength = 0.035f,
            Trees = new()
            {
                Count = 850,
                ConiferFraction = 1.0f,
                Clusters = 36,
                ClusterSpread = 40f,
                MaxRadius = 430f,
                ConiferColor = new(0.011f, 0.026f, 0.019f),
                TrunkColor = new(0.030f, 0.024f, 0.020f),
                TrunkBaseRadius = 0.40f,
                TrunkTopRadius = 0.24f,
                ConiferTrunkHeight = 1.5f,
                ConiferCrownRadius = 2.2f,
                ConiferCrownHeight = 21f,
                ConiferStandTrunk = 6f,
                CrownSnow = 0.85f,
                //A heavier coat on High and Ultra's boughs than the daytime forest's frost: every #963 reference drew the
                //wood under the Moon white along its boughs
                BoughFrost = 0.7f,
                CrownSnowColor = new(0.58f, 0.64f, 0.76f),
                ConiferTiers = 9,
                ConiferTierSpread = 5,
                ConiferRaggedness = 2f,
            },
            //A snag reads as a snag because the wood round it is alive: forty in eight hundred is a stand, four hundred
            //would be a burn. Thick and dark cool grey: thin pale ones read as bamboo (#462).
            Snags = new()
            {
                Count = 40, Length = 13f, Radius = 0.55f, Clusters = 14, MaxRadius = 380f,
                Color = new(0.05f, 0.05f, 0.055f), TopSnow = 0.6f,
            },
            //The logs lie in drifts with a cap of snow along their tops, as every reference of a log in snow draws them
            Logs = new() { Count = 18, Length = 8f, Radius = 0.38f, Clusters = 8, MaxRadius = 300f, Color = new(0.05f, 0.05f, 0.055f), TopSnow = 0.95f },
            Rocks = new() { TopSnow = 0.95f },
            Stumps = new() { TopSnow = 0.95f },
        };

        /// <summary>The snow on the ground: its drifts, the bank against the island, the drifts round the planting, and how it takes the light.</summary>
        public AuroraSnowfieldConfig Snowfield { get; set; } = new();

        /// <summary>The night sky behind the curtains: its gradient, the glow along the horizon, and the Moon.</summary>
        public AuroraNightSkyConfig Sky { get; set; } = new();

        /// <summary>The aurora itself.</summary>
        public AuroraCurtainsConfig Curtains { get; set; } = new();

        /// <summary>The ground mist pooled in the clearing among the trunks, and the haze the hills recede into.</summary>
        public AuroraMistConfig Mist { get; set; } = new();

        /// <summary>The cheap spruces on the hills past the planted wood, so every ridge's skyline is tree tips.</summary>
        public AuroraFarStandConfig FarStand { get; set; } = new();

        /// <summary>
        /// A little falling snow, through <c>Snow.fx</c>'s shared flake buffer and the aurora's own clone of the effect;
        /// the buffer is sized by <see cref="MountainSceneConfig"/>'s copy and this draws the first 3000 of it. Gentler than
        /// the mountain's. ⚠ Near the lens a defocused flake against the night is a big pale disc (the intro's shots read
        /// as dust on the lens, #963): the flakes fade out from ten units and the lens's blur is small.
        /// </summary>
        public SnowConfig Snow { get; set; } = new()
        {
            FlakeCount = 3000,
            FarLayerScale = 1f,
            NearFade = 10f,
            Aperture = 0.05f,
            BoxSize = new(70f, 55f, 70f),
            FallSpeed = 4.5f,
            Wind = new(1.2f, 0.4f),
            Sway = 1.4f,
            FlakeSize = 0.07f,
            Opacity = 0.45f,
            FlakeColor = new(0.5f, 0.54f, 0.6f),
        };

        /// <summary>
        /// The stars between the curtains: the shared lattice (<c>Stars.fxh</c>), richer than the Moon's airless black
        /// and thinner than space's long exposure. Thinned towards the horizon and round the Moon in the shader.
        /// </summary>
        public SpaceStarsConfig Stars { get; set; } = new()
        {
            BrightChance = 0.26f,
            MediumChance = 0.28f,
            FaintChance = 0.32f,
        };

        /// <summary>What lights the island, the gun, the balls and the wood: the Moon as the key, the snow's bounce, the curtains' green from behind.</summary>
        public AuroraLightingConfig Lighting { get; set; } = new();

        /// <summary>
        /// The Potato path's stand-in (#829) keeps the look it shipped with: the desktop's rebuild (#963) does not reach
        /// the Raspberry Pi's renderer, whose sky and rig are read from here and from nowhere else.
        /// </summary>
        public AuroraPotatoConfig Potato { get; set; } = new();
    }

    /// <summary>
    /// The snowfield (#963): the forest's field (<see cref="AuroraSceneConfig.Terrain"/>) under the snow's own relief.
    /// <para>
    /// Three terms of geometry, all of them in the drawn ground and in the CPU mirror the wood is planted by:
    /// the wind's <b>drifts</b>, the <b>bank</b> piled against the island's drum, and the <b>mounds</b> drifted round
    /// every trunk, snag, log, boulder and stump (baked once from the planting into a map the vertex shader reads).
    /// Below a cell of the grid, the <b>sastrugi</b> (the wind-cut ridges) and the crust's grain are per pixel.
    /// </para>
    /// </summary>
    public sealed class AuroraSnowfieldConfig
    {
        /// <summary>The way the wind blows, in XZ (normalised where it is used): the drifts' crests and the sastrugi lie across it, and the lee drifts trail along it.</summary>
        public Vec2 Wind { get; set; } = new(0.82f, 0.57f);

        /// <summary>How high the wind's dunes stand, peak to mean.</summary>
        public float DriftHeight { get; set; } = 0.45f;

        /// <summary>The dunes' spacing along the wind.</summary>
        public float DriftAlong { get; set; } = 16f;

        /// <summary>How far a dune's crest runs across the wind.</summary>
        public float DriftAcross { get; set; } = 45f;

        /// <summary>Where the bank against the island starts: under the drum's face.</summary>
        public float BankRadius { get; set; } = ArenaIsland.TERRAIN_HOLE_RADIUS;

        /// <summary>How high the snow is piled against the drum, on average round it.</summary>
        public float BankHeight { get; set; } = 1.4f;

        /// <summary>How far out the bank falls away by e.</summary>
        public float BankReach { get; set; } = 7f;

        /// <summary>How much deeper it lies on the lee side than on average, 0 to 1.</summary>
        public float BankLee { get; set; } = 0.35f;

        /// <summary>How high the snow stands against a trunk of scale 1, before the lee tail.</summary>
        public float TrunkDrift { get; set; } = 0.55f;

        /// <summary>How far out from the bark the drift round a trunk falls away by e, at scale 1.</summary>
        public float TrunkDriftReach { get; set; } = 0.9f;

        /// <summary>How long the lee tail behind an obstacle runs, at scale 1, and how high it is against the drift at its foot.</summary>
        public float LeeTail { get; set; } = 5f;

        /// <summary>The lee tail's height as a share of the drift at the foot.</summary>
        public float LeeTailHeight { get; set; } = 0.45f;

        /// <summary>How deep the snow lies against a log, as a share of its radius: half buried, a ramp up each side.</summary>
        public float LogDrift { get; set; } = 0.65f;

        /// <summary>How much less sky the snow sees right under a crown (0 none, 1 none at all): the dark round a tree's foot that seats it.</summary>
        public float CanopyShade { get; set; } = 0.5f;

        /// <summary>The snow's albedo.</summary>
        public Rgb Albedo { get; set; } = new(0.80f, 0.84f, 0.90f);

        /// <summary>Light that went into the lit pack and came back out in the shade: the blue of shadowed snow, added where the Moon does not reach.</summary>
        public Rgb DeepTint { get; set; } = new(0.004f, 0.011f, 0.032f);

        /// <summary>How tall the wind-cut ridges stand, per pixel.</summary>
        public float SastrugiStrength { get; set; } = 0.06f;

        /// <summary>How many ridges a unit (across the ridges, along the wind).</summary>
        public float SastrugiScale { get; set; } = 0.9f;

        /// <summary>The share of the snow's grains that can glint.</summary>
        public float SparkleDensity { get; set; } = 0.02f;

        /// <summary>How bright a glint is against the moonlight.</summary>
        public float SparkleStrength { get; set; } = 7f;
    }

    /// <summary>The night sky behind the curtains (#963).</summary>
    public sealed class AuroraNightSkyConfig
    {
        /// <summary>The sky overhead: a deep blue, not black - the references' night is blue, and a black one reads as a hole.</summary>
        public Rgb Zenith { get; set; } = new(0.0035f, 0.0062f, 0.016f);

        /// <summary>The sky at the horizon, before its glow.</summary>
        public Rgb Horizon { get; set; } = new(0.016f, 0.026f, 0.040f);

        /// <summary>How fast the one turns into the other going up (a power on the elevation's sine).</summary>
        public float GradientPower { get; set; } = 0.45f;

        /// <summary>The glow along the horizon: airglow, and the curtains' light scattered low - every reference has one, and it is what the treeline and the hills stand out against.</summary>
        public Rgb HorizonGlow { get; set; } = new(0.050f, 0.074f, 0.082f);

        /// <summary>The glow's height, as a share of the hemisphere.</summary>
        public float HorizonGlowWidth { get; set; } = 0.2f;

        /// <summary>How high the Moon stands, in degrees: low, so the wood's shadows run long across the snow.</summary>
        public float MoonElevationDegrees { get; set; } = 17f;

        /// <summary>Its bearing (0 is +Z, 90 is +X): to the left of the play camera's view and a little behind it, so the shadows cross the clearing in front of the gun.</summary>
        public float MoonAzimuthDegrees { get; set; } = 290f;

        /// <summary>The disc's angular radius, in degrees: larger than the real Moon's, which at a game's field of view is a dot.</summary>
        public float MoonRadiusDegrees { get; set; } = 1.1f;

        /// <summary>The disc's radiance: over the glare threshold, so it blooms.</summary>
        public Rgb MoonDisc { get; set; } = new(2.4f, 2.35f, 2.2f);

        /// <summary>The air lit round it.</summary>
        public Rgb MoonHalo { get; set; } = new(0.035f, 0.042f, 0.055f);

        /// <summary>The halo's falloff, in radians.</summary>
        public float MoonHaloWidth { get; set; } = 0.22f;

        /// <summary>The moonlight on a face square to the Moon (linear): what lights the snow, and the rig's key.</summary>
        public Rgb MoonLight { get; set; } = new(0.30f, 0.36f, 0.50f);

        /// <summary>The night sky's own light on the snow from overhead (starlight, airglow, the deep blue).</summary>
        public Rgb SkyLightZenith { get; set; } = new(0.030f, 0.048f, 0.10f);

        /// <summary>And from the horizon: its glow and the snow's own bounce.</summary>
        public Rgb SkyLightHorizon { get; set; } = new(0.040f, 0.052f, 0.070f);
    }

    /// <summary>
    /// The aurora (#963): a few arcs, each a curtain hanging along a folded line across the sky. Distances are in the
    /// sky's own units, the main curtain's lower edge standing one up.
    /// <para>
    /// <b>⚠ The look the owner rejected</b> (#963, "it looks like a bug"): a band over two thirds of the sky filled with
    /// soft noise ramping green to violet, so everything above forty degrees was a magenta mottle. Every reference
    /// draws instead a few distinct curtains with a sharp, bright green lower edge, rays straight up them, red or
    /// magenta tops, and dark sky between them.
    /// </para>
    /// </summary>
    public sealed class AuroraCurtainsConfig
    {
        /// <summary>The bearing of the side of the sky the curtains hang over (0 is +Z): ahead of the play camera.</summary>
        public float BearingDegrees { get; set; } = 180f;

        /// <summary>The arcs, nearest the bearing's horizon first.</summary>
        public AuroraArcConfig[] Arcs { get; set; } =
        {
            //The great curtain over the treeline in the play camera's view: its lower edge some ten to eighteen degrees up,
            //one long S across the view
            new() { Offset = 4.4f, AngleDegrees = 0f, BendDepth = 1.6f, BendFrequency = 0.22f, FoldDepth = 0.25f, FoldFrequency = 1.0f,
                LitFrequency = 0.35f, LowerEdge = 1.0f, Height = 1.5f, Brightness = 1.0f, Seed = 1.3f, PulsePhase = 0f },
            //A fainter one closer overhead, for a lens that looks up
            new() { Offset = 1.6f, AngleDegrees = 14f, BendDepth = 0.5f, BendFrequency = 0.35f, FoldDepth = 0.15f, FoldFrequency = 1.4f,
                LitFrequency = 0.5f, LowerEdge = 1.3f, Height = 1.2f, Brightness = 0.4f, Seed = 7.9f, PulsePhase = 2.1f },
            //And one over the other half of the sky, for the views that turn round
            new() { Offset = -3.6f, AngleDegrees = -9f, BendDepth = 1.4f, BendFrequency = 0.25f, FoldDepth = 0.25f, FoldFrequency = 1.1f,
                LitFrequency = 0.4f, LowerEdge = 1.0f, Height = 1.4f, Brightness = 0.7f, Seed = 4.4f, PulsePhase = 4.0f },
        };

        /// <summary>The curtains' body: oxygen's green, at the lower altitudes (linear).</summary>
        public Rgb Green { get; set; } = new(0.10f, 1.0f, 0.36f);

        /// <summary>Their tops: oxygen's red, high up.</summary>
        public Rgb Red { get; set; } = new(0.45f, 0.04f, 0.16f);

        /// <summary>The thin magenta of the lowest edge in a bright display.</summary>
        public Rgb Fringe { get; set; } = new(0.45f, 0.05f, 0.40f);

        /// <summary>The peak brightness of the brightest fold, before the slant; over the glare threshold, so a bright fold blooms.</summary>
        public float Intensity { get; set; } = 0.75f;

        /// <summary>How soft the lower edge is, as a share of a curtain's height.</summary>
        public float EdgeSoftness { get; set; } = 0.03f;

        /// <summary>How far along its axis an arc reaches before it has faded, in sky units.</summary>
        public float Length { get; set; } = 14f;

        /// <summary>Rays a unit of an arc's length.</summary>
        public float RayScale { get; set; } = 16f;

        /// <summary>How deep the rays cut the curtain, 0 to 1.</summary>
        public float RayStrength { get; set; } = 0.55f;

        /// <summary>How fast the rays run along a curtain, in its units a second: the shimmer.</summary>
        public float RaySpeed { get; set; } = 0.35f;

        /// <summary>How fast the folds re-form, in fold-noise units a second: slow, so a curtain changes shape where it hangs rather than sliding (#462's lesson).</summary>
        public float FoldSpeed { get; set; } = 0.035f;

        /// <summary>How slowly the arcs breathe, in radians a second; each has its own phase, so the brightness wanders from curtain to curtain.</summary>
        public float PulseSpeed { get; set; } = 0.11f;

        /// <summary>How deep a breath goes, 0 to 1 of the brightness.</summary>
        public float PulseDepth { get; set; } = 0.4f;

        /// <summary>How far the curtains' green light reaches the ground on a face square to them, against their brightness on the sky.</summary>
        public float GroundLight { get; set; } = 0.035f;
    }

    /// <summary>One arc of the aurora (#963): a curtain hanging along a folded line across the sky.</summary>
    public sealed class AuroraArcConfig
    {
        /// <summary>How far ahead of the camera the line runs, towards the curtains' bearing (negative: behind), in sky units.</summary>
        public float Offset { get; set; }

        /// <summary>How far the line is turned from square to the bearing, in degrees.</summary>
        public float AngleDegrees { get; set; }

        /// <summary>How deep its long bend is, in sky units: the line the curtain hangs from snakes this far towards and away from the eye.</summary>
        public float BendDepth { get; set; }

        /// <summary>Bends a unit of its length: low, so a curtain makes one or two long S-curves across a view.</summary>
        public float BendFrequency { get; set; }

        /// <summary>How deep its folds are, in sky units: the smaller pleats along the bend.</summary>
        public float FoldDepth { get; set; }

        /// <summary>Folds a unit of its length.</summary>
        public float FoldFrequency { get; set; }

        /// <summary>How often along its length the curtain is lit and dark, a unit: it shines in stretches, not as one even fence.</summary>
        public float LitFrequency { get; set; }

        /// <summary>The altitude of its lower edge.</summary>
        public float LowerEdge { get; set; }

        /// <summary>How tall the curtain hangs above its lower edge.</summary>
        public float Height { get; set; }

        /// <summary>Its brightness against <see cref="AuroraCurtainsConfig.Intensity"/>.</summary>
        public float Brightness { get; set; }

        /// <summary>Its own roll of the folds and the rays.</summary>
        public float Seed { get; set; }

        /// <summary>Where in its breath it starts.</summary>
        public float PulsePhase { get; set; }
    }

    /// <summary>
    /// The mist (#963): height fog pooled in the clearing, dense at the snow and thinning upwards, in banks; it veils the
    /// snow, the wood and the far stand alike (<c>SceneFog.fxh</c>, the shared instanced effect included), and the
    /// horizon fades into it. A start distance keeps it off the island, the gun and the cluster.
    /// </summary>
    public sealed class AuroraMistConfig
    {
        /// <summary>The mist's own radiance (linear): moonlit, a little of the curtains' green in it.</summary>
        public Rgb Color { get; set; } = new(0.105f, 0.135f, 0.165f);

        /// <summary>Optical depth a unit of path at <see cref="BaseY"/>.</summary>
        public float Density { get; set; } = 0.05f;

        /// <summary>Where the density is stated: at the snow.</summary>
        public float BaseY { get; set; } = -15f;

        /// <summary>How far up it thins by e.</summary>
        public float ScaleHeight { get; set; } = 2.5f;

        /// <summary>How far from the lens nothing is veiled.</summary>
        public float Start { get; set; } = 20f;

        /// <summary>A thin haze at every height on top, so the far hills recede into the sky.</summary>
        public float Haze { get; set; } = 0.0012f;

        /// <summary>How deeply the banks break the layer, 0 to 1.</summary>
        public float Patchiness { get; set; } = 0.55f;

        /// <summary>How fast the banks drift with the wind, units a second.</summary>
        public float DriftSpeed { get; set; } = 0.6f;
    }

    /// <summary>
    /// The far stand (#963): the hills' forest past the planted wood, a few thousand spruces of one cheap spire mesh,
    /// instanced, so every ridge's skyline is tree tips. It stands from where the planted wood thins out to well into
    /// the haze, and casts no shadow (the map does not reach it).
    /// </summary>
    public sealed class AuroraFarStandConfig
    {
        /// <summary>How many spruces.</summary>
        public int Count { get; set; } = 10000;

        /// <summary>Where it starts, from the arena's axis.</summary>
        public float InnerRadius { get; set; } = 260f;

        /// <summary>Where it ends.</summary>
        public float OuterRadius { get; set; } = 600f;

        /// <summary>A spruce's height at scale 1.</summary>
        public float Height { get; set; } = 21f;

        /// <summary>Its skirt's radius at scale 1.</summary>
        public float Radius { get; set; } = 2.6f;

        /// <summary>The smallest and largest scale.</summary>
        public float MinScale { get; set; } = 0.75f;

        /// <summary>The largest scale.</summary>
        public float MaxScale { get; set; } = 1.45f;

        /// <summary>The needles (linear), the wood's own.</summary>
        public Rgb Needles { get; set; } = new(0.011f, 0.026f, 0.019f);

        /// <summary>The snow on the tiers.</summary>
        public Rgb Snow { get; set; } = new(0.58f, 0.64f, 0.76f);

        /// <summary>How much of a ridge's ground is left open (glades, scree), 0 to 1.</summary>
        public float Glades { get; set; } = 0.3f;

        /// <summary>The planting's seed.</summary>
        public int Seed { get; set; } = 96301;
    }

    /// <summary>
    /// The aurora's light rig (#963), stated rather than derived from a dome: the <b>Moon</b> is the key (its direction is
    /// <see cref="AuroraNightSkyConfig.MoonElevationDegrees"/>' and casts the shadow map), the snow's bounce lights the
    /// undersides, and the curtains' green comes from behind and above as the back light. The balls, the island and the
    /// gun take the curtains' colour (<see cref="GlowTint"/>), stepped as the arcs breathe, and do not pulse in
    /// brightness: a gun strobing with the sky reads as a fault.
    /// </summary>
    public sealed class AuroraLightingConfig
    {
        /// <summary>The hemisphere from above (linear): the night's blue and the curtains' glow.</summary>
        public Rgb SkyAmbient { get; set; } = new(0.030f, 0.045f, 0.080f);

        /// <summary>The bounce from below: moonlit snow, which is bright.</summary>
        public Rgb GroundAmbient { get; set; } = new(0.060f, 0.068f, 0.085f);

        /// <summary>The key's tint (the Moon): cool. The rig's key colour is this over the default key's warm white, so the Moon lands at <see cref="AuroraNightSkyConfig.MoonLight"/>.</summary>
        public Rgb KeyTint { get; set; } = new(0.30f, 0.39f, 0.80f);

        /// <summary>The back light's tint: the curtains' green from behind and above, over the default back light's dim grey.</summary>
        public Rgb BackTint { get; set; } = new(0.25f, 0.6f, 0.4f);

        /// <summary>How far the sky ambient is carried towards the curtains' current colour, 0 to 1.</summary>
        public float GlowTint { get; set; } = 0.2f;
    }

    /// <summary>
    /// The Potato path's aurora (#829): the sky, the rig and the void it shipped with, frozen here when the desktop scene
    /// was rebuilt (#963). <c>PotatoSceneRig</c> reads them and nothing else does.
    /// </summary>
    public sealed class AuroraPotatoConfig
    {
        /// <summary>The empty sky between the stars (linear).</summary>
        public Rgb VoidColor { get; set; } = new(0.0018f, 0.0022f, 0.0032f);

        /// <summary>The ribbons' look.</summary>
        public AuroraSkyConfig Sky { get; set; } = new();

        /// <summary>The rig.</summary>
        public AuroraLightingConfig Lighting { get; set; } = new()
        {
            SkyAmbient = new(0.016f, 0.028f, 0.026f),
            GroundAmbient = new(0.010f, 0.017f, 0.016f),
            KeyTint = new(0.40f, 0.52f, 0.48f),
            BackTint = new(0.26f, 0.36f, 0.50f),
            GlowTint = 0.6f,
        };
    }

    /// <summary>The Potato path's aurora ribbons (#829's <c>PotatoSkyAurora</c>): a banded green-to-violet glow.</summary>
    public sealed class AuroraSkyConfig
    {
        /// <summary>Low-altitude colour (linear).</summary>
        public Rgb ColorLow { get; set; } = new(0.05f, 1.15f, 0.55f);

        /// <summary>High-altitude colour (linear).</summary>
        public Rgb ColorHigh { get; set; } = new(0.65f, 0.12f, 1.05f);

        /// <summary>Peak linear radiance at the brightest fold.</summary>
        public float Intensity { get; set; } = 1.35f;

        /// <summary>How much of the upper sky the band occupies, 0 to 1 of the hemisphere up from the horizon.</summary>
        public float BandHeight { get; set; } = 0.62f;

        /// <summary>How fast the field turns about the zenith, radians a second.</summary>
        public float DriftSpeed { get; set; } = 0.02f;

        /// <summary>How fast the folds re-form in place.</summary>
        public float MorphSpeed { get; set; } = 0.06f;

        /// <summary>Frequency of the folds across the sky.</summary>
        public float CurtainScale { get; set; } = 2.6f;

        /// <summary>Frequency of the rays across the sky.</summary>
        public float RayScale { get; set; } = 24f;

        /// <summary>How deeply the rays cut the band.</summary>
        public float RayStrength { get; set; } = 0.55f;

        /// <summary>How deep the brightness pulse cuts.</summary>
        public float PulseDepth { get; set; } = 0.45f;
    }
}
