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
        /// The forested ground: the same hills-and-clearing shape and the same scattered wood the daytime
        /// forest stands on (#75's <see cref="ForestScatterRenderer"/>), planted a second time here under
        /// this scene's own night lighting rather than shared with the daytime forest's planting — a config
        /// edit to one does not move the other's trees, and the two woods can drift apart in density without
        /// either caller's own lookup changing.
        /// <para>
        /// <b>Retuned after the first capture (owner's own words): "the aurora looks good, the forest not so
        /// much."</b> Three changes, all in <see cref="ForestTreeConfig"/> and all independent of the daytime
        /// forest's own defaults. It is winter, so the wood is mostly conifer — needles keep their colour
        /// year-round where a broadleaf does not — and there are more trees than the daytime clearing plants,
        /// a denser stand reading as a real winter wood rather than a scattered few. The broadleaf minority
        /// that remains is shrunk to a near-bare stub of a crown rather than a full leafy dome: this scene has
        /// no bare-branch mesh to reach for, so a small enough crown is the honest approximation available —
        /// a broadleaf that has dropped its leaves for the winter, standing thin beside the spruces.
        /// </para>
        /// </summary>
        public ForestSceneConfig Terrain { get; set; } = new()
        {
            //Darker and cooler than the daytime forest's own mossy green — a winter floor under starlight
            //and the aurora's own glow, not a summer clearing under a dome. The lighting rig is the bigger
            //lever (AuroraLightingConfig), but a pigment this saturated stayed bright even under a dim
            //light, so the base colour comes down too.
            ForestColor = new(0.026f, 0.042f, 0.034f),
            ForestColorDark = new(0.008f, 0.018f, 0.014f),
            TreelineColor = new(0.005f, 0.011f, 0.009f),
            Trees = new()
            {
                Count = 380,
                ConiferFraction = 0.94f,

                //The broadleaf crown, shrunk from the daytime forest's full leafy dome (radius 3.1, height
                //5.6) to a bare-branch stub — see the class doc above.
                CrownRadius = 0.55f,
                CrownHeight = 0.9f,
            },
        };

        /// <summary>
        /// A little falling snow — it is winter, and the owner asked for it after the first capture. Shares
        /// <c>Snow.fx</c> and its flake buffer with the mountain scene (<see cref="SceneRenderer.DrawSnow"/>
        /// now takes the config as an argument instead of reading the mountain's own, precisely so a second
        /// scene could ask for snow of its own look without a second buffer or a second effect); the flake
        /// count is still the buffer's own capacity, sized by <see cref="MountainSceneConfig"/>'s copy.
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
    /// the forest canopy and the ground's own ambient wash, because that value has to be one clock the whole
    /// scene shares rather than a second copy of the shader's own noise re-derived in C#. See that method's
    /// doc for why only the slow drift crosses over and the fast pulse and the curtain noise do not.
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
        /// How hard the curtain lines are domain-warped before they are drawn — 0 is a flat glow with no
        /// structure, past about 1.5 they read as ribbons. The same dial <c>Dream.fx</c>'s <c>SwirlWarp</c>
        /// is, on a different palette and a different mood.
        /// </summary>
        public float CurtainWarp { get; set; } = 1.7f;

        /// <summary>How fast the curtains drift sideways across the sky.</summary>
        public float DriftSpeed { get; set; } = 0.15f;

        /// <summary>
        /// The brightness envelope's speed in radians per second — the issue's own "clearly pulsing". Tuned
        /// against a real capture (#205): the first authored value, 0.35 (an 18 s cycle), measured two shots
        /// five seconds apart as visually indistinguishable — the phase had moved, but not by enough for a
        /// glance to catch. At 0.9 (a 7 s cycle) the same five-second gap is unmistakable. The shader sums
        /// this frequency with a third of it, so the breathing is not a plain metronome.
        /// </summary>
        public float PulseSpeed { get; set; } = 0.9f;

        /// <summary>How deep the brightness pulse cuts, 0–1 of the peak — 0 is a steady glow, 1 nearly extinguishes between beats.</summary>
        public float PulseDepth { get; set; } = 0.45f;

        /// <summary>
        /// The slow hue drift's speed between <see cref="ColorLow"/>-dominant and <see cref="ColorHigh"/>-
        /// dominant — the one clock <see cref="SceneRenderer.AuroraGlowColor"/> shares with the ground and
        /// the trees (see the class doc).
        /// </summary>
        public float DriftHueSpeed { get; set; } = 0.045f;
    }

    /// <summary>
    /// The aurora's light rig — stated here rather than derived from a dome, for the space scene's reasons
    /// (<see cref="SpaceLightingConfig"/>): there is no dome, and the metallic drain beads live off the
    /// hemisphere ambient, which therefore must not go to zero. Kept <b>static</b> and deliberately
    /// restrained — unlike the ground shader and the sky itself, the balls, the island and the gun do not
    /// visibly pulse with the aurora; the light show is the sky's job, and a gun strobing in time with it
    /// would read as a fault rather than as weather.
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

        /// <summary>The back/fill light's tint (linear) — cooler and bluer still, plain starlight rather than aurora, cut with the key.</summary>
        public Rgb BackTint { get; set; } = new(0.26f, 0.36f, 0.50f);
    }
}
