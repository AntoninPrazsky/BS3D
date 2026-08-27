using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the Mars backdrop (#277): rust/ochre cratered ground under a dusty, horizon-bright
    /// daytime sky, with two small moons — Phobos and Deimos — standing in for the Moon scene's Earth.
    /// <para>
    /// Deliberately <b>not</b> a red-palette copy of <see cref="MoonSceneConfig"/>: the real Moon has no
    /// atmosphere at all, which is the only reason that scene replaces the sky and closes its horizon with
    /// curvature. Mars keeps a (thin) atmosphere, so it is an ordinary <see cref="SceneRenderer.IsSolidTerrainScene"/>
    /// backdrop — ground drawn through the same terrain machinery every atmospheric sibling uses, under an
    /// ordinary dome — and only its crater field (<c>Mars.fx</c>'s <c>CraterLayer</c>/<c>CraterField</c>) is
    /// ported from the Moon, because that part of it is generic height-field math with nothing Moon-specific
    /// in it. The sixteenth scene.
    /// </para>
    /// <para>
    /// The sky above it is a nineteenth <see cref="SkyDome"/> palette rather than a new code path: none of
    /// the eighteen existing ones is right for a dust-scattering Martian daytime sky, which gets
    /// <b>brighter</b> near the horizon and dimmer toward the zenith — the opposite of Earth's blue-zenith
    /// Rayleigh falloff — but nothing in <see cref="SkyDome"/> assumes which end is the bright one, so a
    /// nineteenth palette (and its own sun direction, #220) is all it takes.
    /// </para>
    /// <para>
    /// The plain carries its own scattered stone field — the outback's <c>RockLayer</c> lattice ported
    /// verbatim, retuned from a skyline of monoliths to the litter of boulders and pebbles real rover
    /// photographs show, dark volcanic basalt against the rust rather than more of the ground's own colour
    /// (<see cref="MarsSurfaceConfig.BoulderColorDeep"/>). One real gap remains, and it does not block a
    /// shippable scene: a foreground dust-haze/dust-devil overlay (<c>Spray.fx</c>'s or the volcano's ash's
    /// machinery) — left out for now, the issue's own fallback.
    /// </para>
    /// </summary>
    public sealed class MarsSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Mars;

        /// <summary>
        /// Mars has no cloud deck worth drawing — what weather it has is dust, not water vapour, and this
        /// scene's dust is the ground haze in <see cref="MarsAirConfig"/> rather than a sky feature — so
        /// the shared cloud deck stays off rather than defaulting on and drawing Earth cumulus over a
        /// Martian sky.
        /// </summary>
        public MarsSceneConfig() => Weather = WeatherPreset.Clear;

        /// <summary>The cratered plain.</summary>
        public MarsTerrainConfig Terrain { get; set; } = new();

        /// <summary>What the ground is made of.</summary>
        public MarsSurfaceConfig Surface { get; set; } = new();

        /// <summary>The thin, dusty air between.</summary>
        public MarsAirConfig Air { get; set; } = new();

        /// <summary>Phobos and Deimos, standing in the sky.</summary>
        public MarsMoonsConfig Moons { get; set; } = new();
    }

    /// <summary>
    /// The cratered plain: flat in a clearing at the island's foot, rising into layered craters with
    /// distance — the Moon's own field (<c>CraterLayer</c>/<c>CraterField</c>/<c>MareBase</c>, ported
    /// verbatim into <c>Mars.fx</c>), retextured and re-tuned rather than reshaped. Unlike the Moon there
    /// is no highland belt and no planetary curvature: Mars keeps its air, so <c>Mars.fx</c> closes the
    /// horizon the ordinary way, with the haze in <see cref="MarsAirConfig"/>, rather than with geometry.
    /// </summary>
    public sealed class MarsTerrainConfig
    {
        /// <summary>Mean ground level in the clearing (world Y) — the island's foot, the same level every
        /// other terrain scene's ground sits at.</summary>
        public float LevelY { get; set; } = -13.5f;

        /// <summary>Radius of the flat clearing the island stands in; no crater's rim reaches inside it.</summary>
        public float ClearingRadius { get; set; } = 90f;

        /// <summary>Transition band over which the flat clearing rises into cratered ground.</summary>
        public float ClearingTransition { get; set; } = 60f;

        /// <summary>
        /// Peak height of the crater field (world units over the whole three-octave sum). Well under the
        /// Moon's 12: that figure was chosen so the largest craters still show a lit far wall from a lens
        /// grazing the island's own deck plane over a domeless black sky, a constraint this scene does not
        /// have — an ordinary dome and haze close the horizon here, so the craters can stay the modest,
        /// walkable relief a thin-aired rocky plain actually has.
        /// </summary>
        public float CraterAmplitude { get; set; } = 4.5f;

        /// <summary>
        /// How far apart the boulders' lattice cells sit, and how many of them carry one — the outback's
        /// own single-cell-lattice trick (<c>RockLayer</c>, ported verbatim into <c>Mars.fx</c>), retuned
        /// for a scattered stone field rather than a skyline of monoliths: rover photographs of the real
        /// surface are not a few tall formations but ground littered with rock at every scale, so this tier
        /// is denser and far shorter than the outback's own.
        /// </summary>
        public float RockSpacing { get; set; } = 30f;

        public float RockChance { get; set; } = 0.55f;

        /// <summary>Height of the tallest boulder above the plain (world units) — a stone a player could
        /// trip over, not a formation.</summary>
        public float RockHeight { get; set; } = 2.2f;

        /// <summary>A second, finer lattice — the litter of smaller stones between the boulders, rotated
        /// against <see cref="RockSpacing"/>'s grid so the two never line up.</summary>
        public float PebbleSpacing { get; set; } = 9f;

        public float PebbleChance { get; set; } = 0.5f;

        public float PebbleHeight { get; set; } = 0.5f;
    }

    /// <summary>
    /// The ground's material: oxidised iron in the shade, the pale dust that settles into every crater rim
    /// and hollow. Reflectances are <b>linear</b>, like every other scene config's.
    /// </summary>
    public sealed class MarsSurfaceConfig
    {
        /// <summary>The rust in shadow — deep oxidised iron.</summary>
        public Rgb RustColor { get; set; } = new(0.190f, 0.078f, 0.042f);

        /// <summary>The paler, dustier rust that gathers in patches and on fresh crater rims.</summary>
        public Rgb RustColorPale { get; set; } = new(0.440f, 0.255f, 0.148f);

        /// <summary>How strongly a crater's raised rim brightens towards <see cref="RustColorPale"/> —
        /// fresh material thrown up by the impact, not yet weathered back to the plain's own tone.</summary>
        public float EjectaBrightness { get; set; } = 0.55f;

        /// <summary>Peak height of the pixel-scale surface relief (world units).</summary>
        public float MicroReliefStrength { get; set; } = 0.06f;

        /// <summary>Strength of the near-camera grain — the Moon's cascaded three-octave field, carried
        /// over unchanged (only the colour it modulates is Mars's own).</summary>
        public float GrainStrength { get; set; } = 0.16f;

        /// <summary>How much of the sky's hemisphere light fills the plain.</summary>
        public float AmbientStrength { get; set; } = 0.60f;

        /// <summary>
        /// The boulders in shadow — dark volcanic basalt rather than a darker version of the ground's own
        /// rust. Real Mars rock is the one thing on the plain that is <b>not</b> red: rover photographs
        /// show near-black stones dusted with the same rust everything else wears, which is exactly what
        /// makes a stone field read as rock rather than as lumps of the ground itself.
        /// </summary>
        public Rgb BoulderColorDeep { get; set; } = new(0.048f, 0.043f, 0.040f);

        /// <summary>The sunlit facets and the rust dust settled on them.</summary>
        public Rgb BoulderColorBright { get; set; } = new(0.155f, 0.108f, 0.086f);

        /// <summary>
        /// How rough a boulder's own face is (world units of relief) — the scale below its silhouette. A
        /// smooth bump reads as a mound of the dust it stands in; this is what says "rock" up close.
        /// </summary>
        public float RockRelief { get; set; } = 0.35f;
    }

    /// <summary>The air: thin, but not nothing — the rust dust that tints Mars's own aerial perspective and
    /// the distance it is carried over before the ordinary dome haze takes over at the horizon.</summary>
    public sealed class MarsAirConfig
    {
        /// <summary>The dust's own colour (linear), lit by the sky and the sun where it is applied.</summary>
        public Rgb HazeTint { get; set; } = new(0.400f, 0.220f, 0.125f);

        /// <summary>
        /// How far the distance haze is carried from the dome's own horizon colour towards the dust — the
        /// outback's own argument: a dome's horizon colour alone would paint the far plain whatever hue
        /// that dome happens to be, and Mars's distance is made of its own rust dust under any of the
        /// nineteen skies, not borrowed from one.
        /// </summary>
        public float DustStrength { get; set; } = 0.55f;

        /// <summary>World distance over which the plain melts into the skyline. Must stay inside the
        /// terrain grid's half-extent, or the mesh's own edge shows against the dome.</summary>
        public float HorizonHazeDistance { get; set; } = 460f;
    }

    /// <summary>
    /// Phobos and Deimos, two small analytic discs drawn over the dome in <c>Mars.fx</c>'s own
    /// <c>MarsMoons</c> pass — the Moon scene's Earth, minus the continents, the weather and the
    /// atmosphere rim neither moon has one of.
    /// <para>
    /// <b>Not drawn at their true angular size, and that is a deliberate departure from the Moon Earth's
    /// own rule.</b> The real Phobos subtends at most about 0.2° and Deimos under 0.06° — roughly a
    /// twentieth and a hundredth of a pixel-forgiving disc at the Earth's own carefully-kept 30-pixel
    /// scale, i.e. sub-pixel points that would simply never resolve. A game whose whole aesthetic is a
    /// pleasant, readable postcard of each place gains nothing from two moons nobody can see; these are
    /// sized instead for the same legibility the Earth was sized <i>down</i> to, just from the other
    /// direction.
    /// </para>
    /// </summary>
    public sealed class MarsMoonsConfig
    {
        /// <summary>Where Phobos hangs: elevation above the horizon and azimuth from +Z towards +X, in
        /// degrees — <see cref="SkyDome"/>'s own <c>SUNS</c> convention, so a direction is guaranteed
        /// normalized and never the zero vector a raw <see cref="Vec3"/> could roll.</summary>
        public float PhobosElevation { get; set; } = 38f;

        public float PhobosAzimuth { get; set; } = 205f;

        /// <summary>Angular radius in degrees (see the class doc for why this is not the real ~0.1°).</summary>
        public float PhobosAngularRadiusDegrees { get; set; } = 1.05f;

        /// <summary>Phobos's own reflectance (linear) — a very dark, near-neutral carbonaceous grey.</summary>
        public Rgb PhobosColor { get; set; } = new(0.095f, 0.088f, 0.080f);

        /// <summary>Where Deimos hangs (degrees, the same convention as <see cref="PhobosElevation"/>).</summary>
        public float DeimosElevation { get; set; } = 58f;

        public float DeimosAzimuth { get; set; } = 268f;

        /// <summary>Angular radius in degrees — smaller and further than Phobos, as the real moon is.</summary>
        public float DeimosAngularRadiusDegrees { get; set; } = 0.45f;

        /// <summary>Deimos's own reflectance (linear) — a shade paler than Phobos.</summary>
        public Rgb DeimosColor { get; set; } = new(0.115f, 0.108f, 0.100f);
    }
}
