using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the Moon backdrop (#125): the island standing on a cratered grey regolith plain
    /// under a black, starlit sky with the Earth hanging in it at its <b>real</b> angular size — the
    /// Apollo-photo look. Stark raking sun, hard shadows, no atmosphere, and nothing moving anywhere: the
    /// stillest scene in the game, deliberately.
    /// <para>
    /// The twelfth scene, and the first that belongs to <b>both</b> scene families at once: solid terrain
    /// (<see cref="SceneRenderer.IsSolidTerrainScene"/> — the island's footprint is cut out of the ground
    /// and the dark pit shaft backs the drain) <b>and</b> sky-replacing
    /// (<see cref="SceneRenderer.ReplacesSky"/> — no dome, no clouds, black clear, its own light rig).
    /// Every colour is <b>linear radiance</b>; angles are in <b>degrees</b> and converted where they are
    /// pushed at the shader, like the space scene's.
    /// </para>
    /// </summary>
    public sealed class MoonSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Moon;

        /// <summary>
        /// The empty sky between the stars (linear). Even darker than space's void: the lunar sky has no
        /// airglow or zodiacal floor to speak of. Not exactly zero — a frame that goes to zero reads as a
        /// hole rather than as darkness.
        /// </summary>
        public Rgb VoidColor { get; set; } = new(0.0012f, 0.0013f, 0.0018f);

        /// <summary>The cratered ground.</summary>
        public MoonTerrainConfig Terrain { get; set; } = new();

        /// <summary>The Earth in the sky.</summary>
        public MoonEarthConfig Earth { get; set; } = new();

        /// <summary>
        /// The starfield — the same three-layer lattice the space scene draws (<c>Stars.fxh</c>, one copy
        /// for both skies), tuned sparser here: an Apollo sky is stars on black, not a long exposure, and
        /// the sparseness is part of the stillness.
        /// </summary>
        public SpaceStarsConfig Stars { get; set; } = new()
        {
            BrightChance = 0.22f,
            MediumChance = 0.24f,
            FaintChance = 0.28f,
        };

        /// <summary>What lights the island, the gun and the balls here, since there is no dome to derive it from.</summary>
        public MoonLightingConfig Lighting { get; set; } = new();
    }

    /// <summary>
    /// The cratered regolith plain: flat in a clearing at the island's foot, rising into layered craters
    /// with distance, climbing again into the highland belt that rings the mare, and falling away past it
    /// with the square of the distance so the horizon closes the way a small world's horizon does. There is
    /// no haze dial and no wind — there is no air.
    /// <para>
    /// The belt is the part of it the player ever sees, and <see cref="HighlandHeight"/> says why: the play
    /// camera's lens grazes the island's own deck, so only relief standing <b>above</b> the deck plane
    /// reaches the frame at all.
    /// </para>
    /// </summary>
    public sealed class MoonTerrainConfig
    {
        /// <summary>Mean regolith level in the clearing (world Y) — the island's foot, the same level the
        /// savanna's grass and the desert's sand sit at.</summary>
        public float LevelY { get; set; } = -13.5f;

        /// <summary>Radius of the flat clearing the island stands in.</summary>
        public float ClearingRadius { get; set; } = 70f;

        /// <summary>Transition band over which the flat clearing rises into cratered ground.</summary>
        public float ClearingTransition { get; set; } = 110f;

        /// <summary>
        /// Peak height of the crater field out in the far field (world units over the whole three-octave
        /// sum). At 12 the largest craters (~55 units across) run about five units rim-to-floor — a
        /// depth-to-diameter ratio well under the real ~0.2, kept shallow so a crater the play camera looks
        /// across still shows its far wall lit rather than a black pit.
        /// </summary>
        public float CraterAmplitude { get; set; } = 12f;

        /// <summary>
        /// The highland belt's full rise over the plain at its crest (world units) — the massifs that ring the
        /// mare the island stands on, and <b>the only part of this scene the play camera can see</b>. Zero
        /// flattens the belt away and takes the whole landscape out of the game frame with it, which is not a
        /// hypothetical: the scene shipped without one and played as a black sky over bare stone.
        /// <para>
        /// The reason is geometric. The gameplay lens sits at <c>GameCameraFit.LENS_FLOOR_Y</c> = −7.9, a bare
        /// 0.6 units over the island's own deck plane (<c>ArenaIsland.TOP_Y</c> = −8.5), so the deck occludes
        /// everything below about half a degree of depression. A curved plain's skyline sits
        /// <c>2·√(eyeHeight · Curvature)</c> <b>below</b> the eye — 2.4° at the shipped figures — far under
        /// that line. Neither obvious dial reaches it: raising <see cref="LevelY"/> leaves the skyline below
        /// the deck even with the plain flush against it (0.8° at zero eye height), and slackening
        /// <see cref="Curvature"/> hits the 500-unit far plane long before the skyline clears. What clears the
        /// deck is relief standing <b>above</b> the lens — which is exactly why the atmospheric siblings' ground
        /// is visible at all: the desert's 14-unit dunes crest eight units over the same lens.
        /// </para>
        /// <para>
        /// At 40 the crest stands about 4.5° over the lens — a band of sunlit massifs half again as deep as the
        /// desert's dune skyline. Past the crest the curvature takes back over (quadratic growth against a
        /// saturated rise), so the elevation falls monotonically outward and the far-plane cut stays hidden
        /// behind the crest.
        /// </para>
        /// </summary>
        public float HighlandHeight { get; set; } = 40f;

        /// <summary>Where the ground starts to climb into the belt — well past the crater plain's clearing
        /// ramp (<see cref="ClearingRadius"/> + <see cref="ClearingTransition"/>), so the mare reads as a
        /// floor before it reads as a basin.</summary>
        public float HighlandInnerRadius { get; set; } = 190f;

        /// <summary>
        /// Where the belt reaches <see cref="HighlandHeight"/> — the distance the skyline stands at. Sized
        /// against the <b>500-unit far plane</b> like <see cref="Curvature"/> is: the crest is the horizon
        /// here, so it must fall comfortably inside the frustum with the ground it hides behind it.
        /// </summary>
        public float HighlandCrestRadius { get; set; } = 310f;

        /// <summary>
        /// The fraction of <see cref="HighlandHeight"/> the <b>lowest saddle</b> of the belt keeps, 0–1. Not
        /// zero: a saddle that drops to the plain is a notch the eye looks straight through, onto ground the
        /// crest exists to occlude — and at a shallow enough angle, that is the far plane's cut. At 0.5 the
        /// lowest saddle still stands over a degree above the lens. At 1 the belt is a lathe-turned bowl rim.
        /// </summary>
        public float HighlandSaddleFloor { get; set; } = 0.5f;

        /// <summary>
        /// The world's curvature, 1/(2R): the terrain drops <c>Curvature · distance²</c>, which is what
        /// closes the horizon <b>past the highland belt</b>. 8e-5 is a 6.25 km "moon". This replaces the
        /// horizon haze every atmospheric sibling fades out with: there is no air to scatter, so distance
        /// here is occlusion, not fog.
        /// <para>
        /// The skyline itself is <see cref="HighlandCrestRadius"/>, not this bulge. On its own the bulge put
        /// the horizon 360–450 units out and 2.4° <b>below</b> the play camera's eye, which is under the
        /// island deck's own occluding line — see <see cref="HighlandHeight"/> for the geometry and for why
        /// slackening this dial cannot answer it. What the curvature still does is everything past the crest:
        /// a quadratic drop against the belt's saturated rise, so elevation falls monotonically outward from
        /// the crest and each ridge is hidden by the one before it.
        /// </para>
        /// <para>
        /// It is also sized against the <b>Game camera's 500-unit far plane</b>: the ground must close by
        /// occlusion before the far plane can cut it, or the cut shows through the belt's saddles — dead
        /// level and camera-locked. Halve it and it does.
        /// </para>
        /// </summary>
        public float Curvature { get; set; } = 8e-5f;

        /// <summary>
        /// The regolith's reflectance (linear). The real thing is astonishingly dark — albedo about 0.12 —
        /// but stands under a sun with no air in the way; authored a touch over the physical value so the
        /// scene keys to the game's exposure.
        /// </summary>
        public Rgb RegolithColor { get; set; } = new(0.155f, 0.152f, 0.148f);

        /// <summary>The paler grey of fresh ejecta and the bright patches the plains are mottled with (linear).</summary>
        public Rgb RegolithColorPale { get; set; } = new(0.30f, 0.295f, 0.285f);

        /// <summary>How strongly a crater's raised rim brightens towards the pale grey — fresh excavated
        /// material, the cheapest ejecta there is. Zero turns the rims plain.</summary>
        public float EjectaBrightness { get; set; } = 0.7f;

        /// <summary>Peak height of the pixel-scale surface relief (world units).</summary>
        public float MicroReliefStrength { get; set; } = 0.05f;

        /// <summary>Strength of the near-camera regolith grain — glass beads and crushed rock at arm's
        /// length, the only cue at that distance that the surface is granular.</summary>
        public float GrainStrength { get; set; } = 0.14f;

        /// <summary>
        /// The sun's radiance on the terrain (linear, over 1 — it is a sun). The config's own value rather
        /// than the frame's dome-derived <c>SunColor</c>, because this scene draws no dome and a
        /// dome-derived sun would be a lie — the same argument the light rig makes. Near-white, a shade
        /// cool: there is no atmosphere to redden it.
        /// </summary>
        public Rgb SunColor { get; set; } = new(1.70f, 1.68f, 1.62f);

        /// <summary>
        /// The sky's fill on the ground (linear, tiny): starlight, and nothing else. Deliberately not zero —
        /// at zero the shadowed wall of every crater reads as a hole cut in the ground, the space scene's
        /// lesson about the island restated at terrain scale.
        /// </summary>
        public Rgb AmbientColor { get; set; } = new(0.015f, 0.017f, 0.024f);
    }

    /// <summary>
    /// The Earth, solved analytically in the sky pass the way the space scene's gas giant is — a unit
    /// sphere at the distance that gives the configured angular radius, lit by the very sun the island
    /// takes — but with continents, ice and swirled weather instead of cloud bands.
    /// <para>
    /// <b>The size is the real one, and that is an expectation-setting decision rather than a bug.</b>
    /// Earth seen from the Moon subtends about 1.9° — 3.7× the Moon seen from Earth, yet far smaller than
    /// most people's mental image from cropped Apollo photographs. About 30 pixels across at the default
    /// window: a small, detailed blue-and-white marble in a black sky, which is exactly the point. Anyone
    /// tempted to "fix" it should read issue #125 first; the dial is here if a level genuinely wants a
    /// poster Earth.
    /// </para>
    /// <para>
    /// Because the disc is small, its lit body — clouds included — stays <b>under</b> the glare threshold
    /// (0.55 on luminance), unlike the space planet's limb: a ~30-pixel disc is no long coherent arc, and a
    /// small bright thing the glare's sparse grid samples stochastically flickers. Only the thin atmosphere
    /// rim carries a touch of bloom, through a saturated blue whose luminance stays modest.
    /// </para>
    /// </summary>
    public sealed class MoonEarthConfig
    {
        /// <summary>
        /// Where it hangs (normalized when applied). Up and off to one side of where the play camera rests
        /// looking down −Z, so it frames the hanging cluster rather than hiding behind it — the space
        /// planet's placement logic at a fraction of its size. Moving it also moves the <b>phase</b>: the
        /// lit fraction is <c>(1 − dot(Direction, SunDirection)) / 2</c>, and a fat gibbous (dot around
        /// −0.5) is what shows both the terminator and the marble.
        /// </summary>
        public Vec3 Direction { get; set; } = new(-0.33f, 0.40f, -0.86f);

        /// <summary>
        /// Angular <b>radius</b> in degrees. 0.95° is the true value (Earth's mean angular diameter from
        /// the Moon is ~1.9°). Zero removes the Earth altogether.
        /// </summary>
        public float AngularRadiusDegrees { get; set; } = 0.95f;

        /// <summary>Its pole. Tilted, as the real one is against the lunar sky.</summary>
        public Vec3 Axis { get; set; } = new(0.20f, 0.95f, 0.24f);

        /// <summary>The oceans (linear) — most of the marble.</summary>
        public Rgb OceanColor { get; set; } = new(0.055f, 0.15f, 0.40f);

        /// <summary>Vegetated land (linear).</summary>
        public Rgb LandColor { get; set; } = new(0.11f, 0.20f, 0.07f);

        /// <summary>Arid land (linear) — the desert belts.</summary>
        public Rgb LandColorArid { get; set; } = new(0.40f, 0.30f, 0.14f);

        /// <summary>
        /// The weather (linear). Its luminance sits just <b>under</b> the glare threshold on purpose — the
        /// clouds are the brightest thing on the disc, and the disc must not bloom (see the class doc).
        /// </summary>
        public Rgb CloudColor { get; set; } = new(0.52f, 0.54f, 0.58f);

        /// <summary>How much of the disc the weather covers, 0–1.</summary>
        public float CloudAmount { get; set; } = 0.55f;

        /// <summary>The atmosphere, on the limb and standing just off it (linear). Saturated blue: its
        /// brightness can run past 1 in the blue channel while the luminance stays modest.</summary>
        public Rgb RimColor { get; set; } = new(0.35f, 0.55f, 1.00f);

        /// <summary>How strongly the limb and its halo glow.</summary>
        public float RimStrength { get; set; } = 0.5f;

        /// <summary>What the night side keeps. Not zero: a night side at pure black is a hole punched in
        /// the starfield rather than the dark half of a sphere.</summary>
        public float NightAmbient { get; set; } = 0.03f;
    }

    /// <summary>
    /// The Moon's light rig — stated here rather than derived from a dome, for the space scene's reasons
    /// (<see cref="SpaceLightingConfig"/>): there is no dome, the darkest dome would halve the sun through
    /// the key tint, and the metallic drain beads live off the hemisphere ambient, which therefore must not
    /// go to zero.
    /// <para>
    /// One thing is deliberately upside-down against every other rig: the ground bounce is <b>brighter</b>
    /// than the sky ambient. Everywhere else the sky is the bright half of the hemisphere; here the sky is
    /// black and the sunlit regolith below is the only diffuse source there is, which is exactly how things
    /// stand on the real thing — Apollo photographs fill their shadows from the ground, not the sky.
    /// </para>
    /// </summary>
    public sealed class MoonLightingConfig
    {
        /// <summary>The hemisphere ambient from above (linear): starlight and the Earth, cold, faint and blue.</summary>
        public Rgb SkyAmbient { get; set; } = new(0.030f, 0.034f, 0.052f);

        /// <summary>The bounce from below (linear): sunlit regolith — neutral grey, and deliberately
        /// <b>brighter</b> than the sky above it (see the class doc).</summary>
        public Rgb GroundAmbient { get; set; } = new(0.055f, 0.053f, 0.050f);

        /// <summary>The key light's tint (linear, ~1 per channel). Near-white and a hair blue: no
        /// atmosphere reddens the sun out here.</summary>
        public Rgb KeyTint { get; set; } = new(1.00f, 1.00f, 1.05f);

        /// <summary>The back/fill light's tint (linear, ~1 per channel). Cool — the fill out here is
        /// earthshine and starlight.</summary>
        public Rgb BackTint { get; set; } = new(0.72f, 0.78f, 1.00f);

        /// <summary>
        /// Earthshine: the light the Earth throws back onto the island, as a real scene point light so the
        /// metallic drain beads get a highlight out of it — the space planetshine's argument, at the
        /// Earth's colour. Zero switches it off.
        /// </summary>
        public float EarthshineStrength { get; set; } = 0.12f;

        /// <summary>How far out the earthshine light stands, along the Earth's own direction. Its range is
        /// three times this, so the fill stays even across the island.</summary>
        public float EarthshineDistance { get; set; } = 420f;
    }
}
