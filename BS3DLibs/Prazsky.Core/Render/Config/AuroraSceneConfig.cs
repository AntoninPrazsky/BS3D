using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the aurora backdrop (#205): a night sky over forested land, a strong, clearly
    /// pulsing coloured aurora overhead with the stars still showing through and around it.
    /// <para>
    /// The second scene in both families at once, after the Moon (#125): solid terrain
    /// (<see cref="SceneRenderer.IsSolidTerrainScene"/> — real forested ground, its own planting of the
    /// shared <see cref="ForestScatterRenderer"/> wood, the island's footprint cut out, the dark pit shaft
    /// backing the drain) <b>and</b> sky-replacing (<see cref="SceneRenderer.ReplacesSky"/> — no dome, the
    /// shared star lattice, black clear, its own light rig). Every colour is <b>linear radiance</b>.
    /// </para>
    /// </summary>
    public sealed class AuroraSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Aurora;

        /// <summary>No weather: this scene draws no dome and no cloud deck, exactly as the Moon's does not — see that config's own class doc.</summary>
        public AuroraSceneConfig() => Weather = WeatherPreset.Clear;

        /// <summary>The empty sky between the stars, before the aurora (linear). Not exactly zero — a frame that goes to zero reads as a hole rather than as night.</summary>
        public Rgb VoidColor { get; set; } = new(0.0018f, 0.0022f, 0.0032f);

        /// <summary>
        /// The night sky's own light on the snow, apart from the aurora's (linear) — starlight and airglow,
        /// neutral and cool. Added to the ground's hemisphere term beside <see cref="SceneRenderer.AuroraGlowColor"/>
        /// (#462). The aurora's glow alone is almost pure green (its <see cref="AuroraSkyConfig.ColorLow"/> has
        /// next to no red), and a white floor lit by nothing else is a green-painted floor rather than snow
        /// under a green sky; this is what keeps the snow white with the aurora's cast on it.
        /// </summary>
        public Rgb GroundStarlight { get; set; } = new(0.07f, 0.08f, 0.105f);

        /// <summary>
        /// The forested ground: the same hills-and-clearing shape and the same scattered wood the daytime
        /// forest stands on (#75's <see cref="ForestScatterRenderer"/>), planted a second time here under
        /// this scene's own night lighting rather than shared with the daytime forest's planting — a config
        /// edit to one does not move the other's trees, and the two woods can drift apart in density without
        /// either caller's own lookup changing.
        /// <para>
        /// <b>Retuned after the first capture (owner's own words): "the aurora looks good, the forest not so
        /// much."</b> It is winter, so the wood is all conifer — needles keep their colour year-round where a
        /// broadleaf does not, and the one attempt at a bare broadleaf read as mushrooms (see
        /// <c>ConiferFraction</c> below) — and denser than the daytime clearing's.
        /// </para>
        /// <para>
        /// <b>Then rebuilt from generated references (#462): "the forest reads as primitive."</b> Snow on the
        /// floor under near-black spruces, the spruces boreal spires of many ragged whorls rather than garden
        /// cones, planted over the hills as well as the clearing, and dead wood among them — standing snags and
        /// fallen logs. Each change is commented where it is set below; <c>docs/scenes.md</c> ("A boreal
        /// night") carries the reasoning and the traps.
        /// </para>
        /// </summary>
        public ForestSceneConfig Terrain { get; set; } = new()
        {
            //⚠ SNOW, since #462 — and it is the biggest single change that pass made. The floor was a dark
            //winter moss (0.026, 0.042, 0.034) under dark trees, so the clearing read as one black mass the
            //trees barely stood out of; every reference rendered for the pass (img2img over this scene, #489)
            //put the wood on SNOW instead, and the value structure is the whole picture — a pale floor
            //catching the sky's light under near-black spruces is what reads as a boreal night at a glance.
            //The pigment is a cool white (snow's albedo is high, and at night the eye reads it bluish); the
            //hills keep TreelineColor's near-black canopy, so the snowy clearing is ringed by dark forest the
            //way the references are. The "dark" colour is shaded, wind-scoured snow in the hollows and on
            //the steep banks, not bare earth.
            ForestColor = new(0.58f, 0.64f, 0.76f),
            ForestColorDark = new(0.26f, 0.30f, 0.38f),
            TreelineColor = new(0.005f, 0.011f, 0.009f),
            //The hills' painted canopy lets some snow through: at the forest's full 0.95 the slopes behind
            //the clearing read as one black mass, where a snowy wood seen from afar is dark with pale between.
            TreelineStrength = 0.7f,
            //Snow lies smoother than a needle-strewn floor: the relief that reads as needles and moss there
            //reads as grit on snow.
            NeedleReliefStrength = 0.035f,
            //⚠ Pure conifer, not "mostly" (ConiferFraction 1, not 0.94). The first attempt at a bare winter
            //broadleaf kept the daytime trunk height and shrunk only CrownRadius/CrownHeight — reported back
            //as "little mushrooms with a brown leg and a green cap", and once said it cannot be unseen: a
            //round leaf-lobe crown does not stop reading as a round leaf-lobe crown by getting smaller, it
            //just becomes a small one on a stick. There is no bare-branch mesh this scene can reach for
            //(ForestScatterRenderer has none), so rather than hunt for a proportion that still reads as
            //foliage from some angle, the species is left out of this planting entirely.
            //Near-black needles (#462): against snow and a lit sky a spruce at night is a silhouette, and
            //the daytime pigment (0.028, 0.075, 0.026) under this scene's key light read as a lit green
            //Christmas tree on a black floor — the value structure the references all invert.
            //And boreal spires rather than garden spruces (#462): the daytime forest's conifer is a broad cone
            //of four to six whorls, which planted here read as a Christmas-tree lot. The references all stand
            //the wood as tall narrow spruces of many short whorls, crowded into a ragged wall — so the crown is
            //two thirds as wide and a third taller, carried on a thinner, shorter trunk, in nine to thirteen
            //tiers.
            Trees = new()
            {
                Count = 850,
                ConiferFraction = 1.0f,
                Clusters = 36,
                ClusterSpread = 40f,
                MaxRadius = 430f,
                ConiferColor = new(0.011f, 0.026f, 0.019f),
                TrunkColor = new(0.030f, 0.024f, 0.020f),
                TrunkBaseRadius = 0.30f,
                TrunkTopRadius = 0.18f,
                ConiferTrunkHeight = 1.1f,
                ConiferCrownRadius = 1.6f,
                ConiferCrownHeight = 13f,
                ConiferTiers = 9,
                ConiferTierSpread = 5,
                ConiferRaggedness = 2f,
            },
            //Dead wood (#462): every reference stood a few grey snags among the spruces, leaning, and a
            //trunk or two lying in the snow. Few on purpose — a snag reads as a snag because the wood round
            //it is alive; forty of them in eight hundred is a stand, four hundred would be a burn.
            Snags = new()
            {
                Count = 40, Length = 13f, Radius = 0.55f, Clusters = 14, MaxRadius = 380f,
                //Darker and cooler than the forest default's silver: under this scene's green key light and
                //the wood's own shift towards the aurora's hue, the default read as a pale olive shoot -
                //bamboo, not a dead spruce.
                Color = new(0.05f, 0.05f, 0.055f),
            },
            Logs = new() { Count = 18, Length = 8f, Radius = 0.38f, Clusters = 8, MaxRadius = 300f, Color = new(0.05f, 0.05f, 0.055f) },
        };

        /// <summary>
        /// A little falling snow — it is winter, and the owner asked for it after the first capture. Shares
        /// <c>Snow.fx</c> and its flake buffer with the mountain scene (<see cref="SceneRenderer.DrawSnow"/>
        /// now takes the config as an argument instead of reading the mountain's own, precisely so a second
        /// scene could ask for snow of its own look without a second buffer — and since #580 through its own
        /// clone of the effect, this look pushed into it once at load); the flake count is still the buffer's
        /// own capacity, sized by <see cref="MountainSceneConfig"/>'s copy.
        /// Slower and thinner than the mountain's own snow — a gentle winter hush over the wood, not a storm.
        /// </summary>
        public SnowConfig Snow { get; set; } = new()
        {
            BoxSize = new(70f, 55f, 70f),
            FallSpeed = 4.5f,
            Wind = new(1.2f, 0.4f),
            Sway = 1.4f,
            FlakeSize = 0.08f,
            Opacity = 0.45f,
            FlakeColor = new(0.7f, 0.75f, 0.82f),
        };

        /// <summary>The aurora itself.</summary>
        public AuroraSkyConfig Aurora { get; set; } = new();

        /// <summary>
        /// The starfield showing through and around the aurora — the same three-layer lattice the space and
        /// Moon skies draw (<c>Stars.fxh</c>, one copy for all three), tuned between the two: richer than the
        /// Moon's stark vacuum (there is air here to carry no scatter of its own, but a forest floor under a
        /// living sky should not read as bare as an airless one), thinner than space's long exposure.
        /// </summary>
        public SpaceStarsConfig Stars { get; set; } = new()
        {
            BrightChance = 0.26f,
            MediumChance = 0.28f,
            FaintChance = 0.32f,
        };

        /// <summary>What lights the island, the gun and the balls here, since there is no dome to derive it from.</summary>
        public AuroraLightingConfig Lighting { get; set; } = new();
    }

    /// <summary>
    /// The aurora ribbons overhead: soft vertical curtains folded out of domain-warped noise, banded from a
    /// low green to a high violet the way a real aurora's emission changes with altitude, additive over the
    /// stars so they never fully disappear behind it — the issue's own "partially visible through/alongside"
    /// (#205), and the same reasoning the storm's lightning channel is additive for: a glow adds light to
    /// what is behind it, it does not hide it.
    /// <para>
    /// Two clocks drive it, kept apart on purpose. <see cref="PulseSpeed"/> is the brightness breathing —
    /// fast enough that a player watching for a few seconds sees it move, the issue's own "clearly pulsing".
    /// <see cref="DriftHueSpeed"/> is the slow hue drift between the green- and violet-dominant ends of the
    /// band; it is the one <see cref="SceneRenderer.AuroraGlowColor"/> approximates on the CPU side to tint
    /// the forest canopy and the ground's own ambient wash — and since #462 the light rig on the island, the
    /// gun and the balls — because that value has to be one clock the whole scene shares rather than a second
    /// copy of the shader's own noise re-derived in C#. See that method's doc for why only the slow drift
    /// crosses over and the fast pulse and the curtain noise do not. <b>⚠ Until #462 the sky itself never
    /// drew this drift</b>: it was a CPU clock only, so the ground went violet under a sky that stayed green.
    /// The shader takes it now as a shift of its own colour ramp (<see cref="HueSwing"/>).
    /// </para>
    /// </summary>
    public sealed class AuroraSkyConfig
    {
        /// <summary>Low-altitude colour of the curtains (linear) — the oxygen green real aurorae are dominated by.</summary>
        public Rgb ColorLow { get; set; } = new(0.05f, 1.15f, 0.55f);

        /// <summary>High-altitude colour the curtains shade towards overhead (linear) — the rarer high-oxygen red and nitrogen-blue fringe that reads as violet against the green.</summary>
        public Rgb ColorHigh { get; set; } = new(0.65f, 0.12f, 1.05f);

        /// <summary>
        /// Peak linear radiance at a curtain's brightest fold. Deliberately allowed to cross the glare
        /// threshold — a broad, coherent band blooms the way the Milky Way and the space planet's lit limb
        /// do; it is only small, pointy things (a single star) that have to stay under it, because those are
        /// what the glare's sparse sampling catches inconsistently.
        /// </summary>
        public float Intensity { get; set; } = 1.35f;

        /// <summary>How much of the upper sky the band occupies, 0–1 of the hemisphere measured up from the horizon.</summary>
        public float BandHeight { get; set; } = 0.62f;

        /// <summary>How soft the band's own lower edge is against the plain night sky beneath it.</summary>
        public float BandSoftness { get; set; } = 0.28f;

        /// <summary>Frequency of the curtain folds across the sky.</summary>
        public float CurtainScale { get; set; } = 2.6f;

        /// <summary>
        /// How much the noise domain is compressed vertically before the curtains are drawn from it — low
        /// (floored at 0.15) reads as a flat mottle with little vertical structure, past about 1.5 the folds
        /// stretch into long streaks reading as ribbons. Fed as a division of the sample direction's own Y
        /// component (see <c>Aurora.fx</c>'s own header for why it is 3D noise on the direction and not 2D
        /// noise on an angle), so it is a genuine domain stretch now rather than the post-noise contrast
        /// multiplier an earlier build of this dial actually was.
        /// </summary>
        public float CurtainWarp { get; set; } = 1.7f;

        /// <summary>
        /// How fast the curtain field turns about the zenith, in radians per second.
        /// <para>
        /// <b>⚠ It was 0.15 and the owner's word on it was "the aurora moves too fast" (#462).</b> 0.15 rad/s
        /// is 8.6° a second — a full turn in 42 s, a fold across a 60° view in about seven — and it moves
        /// every fold at once, rigidly, so the whole sky read as a carousel. 0.02 is a turn in about five
        /// minutes and a fold across the view in under a minute: the sky still visibly travels over a level,
        /// never within a glance. What makes it read as ALIVE now is <see cref="MorphSpeed"/>, not this.
        /// </para>
        /// </summary>
        public float DriftSpeed { get; set; } = 0.02f;

        /// <summary>
        /// How fast the folds re-form in place (#462), in noise-domain units per second along the curtain's
        /// own long axis: a real curtain ripples and changes shape where it hangs rather than sliding across
        /// the sky, and a slide along the axis the streaks are stretched on changes each one slowly without
        /// sweeping it sideways. 0.06 renews the pattern over roughly a quarter of a minute.
        /// </summary>
        public float MorphSpeed { get; set; } = 0.06f;

        /// <summary>
        /// Frequency of the rays across the sky (#462) — the fine vertical striation inside a curtain that
        /// every photograph of an aurora shows and the folds alone did not: without it the band read as soft
        /// green smoke. In noise cells per unit of view direction, so 24 is a ray every two or three degrees.
        /// </summary>
        public float RayScale { get; set; } = 24f;

        /// <summary>How deeply the rays cut into the curtain's brightness, 0 (none) to 1 (dark gaps between them).</summary>
        public float RayStrength { get; set; } = 0.55f;

        /// <summary>
        /// The brightness envelope's speed in radians per second — the issue's own "clearly pulsing". Tuned
        /// against a real capture (#205): the first authored value, 0.35 (an 18 s cycle), measured two shots
        /// five seconds apart as visually indistinguishable — the phase had moved, but not by enough for a
        /// glance to catch, so it went to 0.9 (a 7 s cycle). <b>That was a test of whether a still shows a
        /// phase difference, not of how the sky reads in motion</b>, and in motion the owner found it too
        /// fast (#462): the whole sky swelling and fading every seven seconds is a strobe, where a real
        /// aurora's brightness surges over tens of seconds. 0.4 is a 16 s breath under the shader's slower
        /// second clock (0.31 of this frequency), so the envelope still never repeats on a plain period.
        /// </summary>
        public float PulseSpeed { get; set; } = 0.4f;

        /// <summary>How deep the brightness pulse cuts, 0–1 of the peak — 0 is a steady glow, 1 nearly extinguishes between beats.</summary>
        public float PulseDepth { get; set; } = 0.45f;

        /// <summary>
        /// The slow hue drift's speed between <see cref="ColorLow"/>-dominant and <see cref="ColorHigh"/>-
        /// dominant — the one clock <see cref="SceneRenderer.AuroraGlowColor"/> shares with the ground and
        /// the trees (see the class doc).
        /// </summary>
        public float DriftHueSpeed { get; set; } = 0.045f;

        /// <summary>
        /// How far the drift moves the sky's own green-to-violet ramp, end to end, in units of that ramp
        /// (#462): 0.35 swings the curtain up to 0.175 of the way either side — greener at one end of the
        /// cycle, with violet reaching further down the folds at the other, never a violet sky. The ground's
        /// light and the island's follow the same shift (<c>SceneRenderer.AuroraLightMix</c>).
        /// </summary>
        public float HueSwing { get; set; } = 0.35f;
    }

    /// <summary>
    /// The aurora's light rig — stated here rather than derived from a dome, for the space scene's reasons
    /// (<see cref="SpaceLightingConfig"/>): there is no dome, and the metallic drain beads live off the
    /// hemisphere ambient, which therefore must not go to zero. Deliberately restrained in <b>brightness</b>
    /// — unlike the ground shader and the sky itself, the balls, the island and the gun do not pulse with the
    /// aurora; the light show is the sky's job, and a gun strobing in time with it would read as a fault
    /// rather than as weather.
    /// <para>
    /// <b>But it takes the sky's COLOUR, since #462</b> — the owner: "the glow should reflect its light's
    /// colour onto the cannon and the island". Until then the rig was static, a fixed grey-green, so the
    /// island stood the same colour under a green sky and a violet one. <see cref="GlowTint"/> carries the
    /// key light and the sky ambient that far towards the aurora's current hue at their own brightness
    /// (<c>SceneRenderer.TowardsHue</c>), on the slow hue drift the ground and the wood already follow
    /// (<see cref="AuroraSkyConfig.DriftHueSpeed"/>) and never on the fast pulse — colour, not strobing.
    /// </para>
    /// <para>
    /// <b>⚠ The first authored figures kept every other sky-replacing scene's "~1 per channel" KeyTint/
    /// BackTint convention (the Moon, the dream, the cavern all do), and it was wrong here.</b> Those three
    /// keep a bright key on purpose — the Moon's is a raking sun, the dream and the cavern each state a
    /// coloured but undimmed one — and none of them lights a populated `ForestScatterRenderer`. Fed the same
    /// near-1 tint, this scene's wood read as vividly, almost daytime lit, which is what the owner's first
    /// capture caught ("the forest not so much"). Cut by roughly half again here, on top of the ground's own
    /// separate cut in <see cref="SceneRenderer.AuroraGlowColor"/> — the two are different lights (this rig
    /// lights the balls, the island and the gun; that method lights the ground mesh and, through
    /// <c>ApplySkyTint</c>, the trees) and both needed the same correction independently.
    /// </para>
    /// </summary>
    public sealed class AuroraLightingConfig
    {
        /// <summary>The hemisphere ambient from above (linear): starlight and the aurora's own glow, cool and faint, biased green the way the sky itself is.</summary>
        public Rgb SkyAmbient { get; set; } = new(0.016f, 0.028f, 0.026f);

        /// <summary>The bounce from below (linear): the forest floor under a night sky — darker than the sky above it and a shade greener than a neutral night, the aurora's own cast reflected up.</summary>
        public Rgb GroundAmbient { get; set; } = new(0.010f, 0.017f, 0.016f);

        /// <summary>The key light's tint (linear) — a cool green-cyan cast standing in for a sun this scene does not have, well under the ~1-per-channel every other sky-replacing rig uses (see the class doc's warning).</summary>
        public Rgb KeyTint { get; set; } = new(0.40f, 0.52f, 0.48f);

        /// <summary>The back/fill light's tint (linear) — cooler and bluer still, plain starlight rather than aurora, cut with the key. Deliberately NOT carried towards the aurora's hue with the key: a light from behind that stays starlight is what keeps a violet sky from turning every edge the same colour.</summary>
        public Rgb BackTint { get; set; } = new(0.26f, 0.36f, 0.50f);

        /// <summary>
        /// How far the key light and the sky ambient are carried towards the aurora's current hue (#462), 0–1,
        /// at their own brightness; the ground's bounce goes half as far (it is snow lit mostly by starlight).
        /// 0 is the static rig this scene had until then.
        /// </summary>
        public float GlowTint { get; set; } = 0.6f;
    }
}
