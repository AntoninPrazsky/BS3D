using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the Australian outback backdrop (#112): weathered red-orange monoliths standing on a
    /// wide, near-flat spinifex plain, the island in a clearing among them.
    /// <para>
    /// It is the arid sibling of <see cref="DesertSceneConfig"/> and deliberately shares none of its look:
    /// the Sahara is golden dunes whose only silhouette is their own crests, this is red ground under rock.
    /// Every colour is <b>linear radiance</b>, like every other scene config's.
    /// </para>
    /// <para>
    /// Grouped rather than flat because the dials fall into families a designer tunes one at a time — and as
    /// named nested objects rather than a collection, since the map editor's <c>PropertyGrid</c> is built
    /// <c>IgnoreCollections = true</c> and a list would simply be invisible in the live scene-config editor.
    /// </para>
    /// </summary>
    public sealed class OutbackSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Outback;

        /// <summary>The plain and the monoliths standing on it.</summary>
        public OutbackTerrainConfig Terrain { get; set; } = new();

        /// <summary>What the rock and the ground are made of.</summary>
        public OutbackSurfaceConfig Surface { get; set; } = new();

        /// <summary>The air between: red dust, distance haze and the heat coming off the ground.</summary>
        public OutbackAirConfig Air { get; set; } = new();

        /// <summary>
        /// The shared flock of birds circling overhead — the same buffer the savanna and the desert draw from,
        /// so this count only ever raises the size the three of them share.
        /// </summary>
        public BirdsConfig Birds { get; set; } = new();
    }

    /// <summary>
    /// The land: a near-flat plain with a broad swell in it, and two lattices of rock standing on it — the
    /// monoliths that make the skyline and the small outcrops that give the plain its scale.
    /// <para>
    /// The clearing gates the <b>rock</b> only, and per formation rather than per pixel (see
    /// <c>RockLayer</c> in <c>Outback.fx</c>): a ramp applied per pixel slices a monolith with a radial
    /// gradient and draws it half sunk, and a ramp over the plain's own swell would make its mean a bowl with
    /// the island at the bottom — the trap the desert's dune sum carries a trailing constant to avoid.
    /// </para>
    /// </summary>
    public sealed class OutbackTerrainConfig
    {
        /// <summary>Mean ground level in the clearing (world Y) — the island's foot, the same level the
        /// savanna's grass, the desert's sand and the Moon's regolith sit at.</summary>
        public float LevelY { get; set; } = -13.5f;

        /// <summary>
        /// Peak height of the plain's own broad swell. Deliberately small: a gibber plain <i>is</i> flat, and
        /// everything dramatic in this scene is what stands on it. At 0 the ground is a plane and reads as one.
        /// </summary>
        public float PlainRelief { get; set; } = 3.2f;

        /// <summary>Radius of the clearing the island stands in; no monolith's centre falls inside it.</summary>
        public float ClearingRadius { get; set; } = 95f;

        /// <summary>Transition band over which formations are allowed to stand at their full height. Short: the
        /// band is not a look, it is only what keeps a formation that lands right on the clearing's edge from
        /// popping to full height across one cell of the lattice.</summary>
        public float ClearingTransition { get; set; } = 55f;

        /// <summary>
        /// How far apart the monoliths' lattice cells sit. Large on purpose — the outback's monoliths stand
        /// alone with a plain between them, and a cell this size is also what buys the jitter its room: the
        /// margin a formation is held clear of its cell edge by is charged as a <i>fraction</i> of the cell,
        /// so the bigger the cell the more world units of wander the same fraction is worth.
        /// </summary>
        public float RockSpacing { get; set; } = 370f;

        /// <summary>Fraction of lattice cells that carry a monolith. The empty ones are what break the grid.</summary>
        public float RockChance { get; set; } = 0.62f;

        /// <summary>Height of the tallest monolith above the plain. The play camera's lens grazes the island's
        /// own deck, so this is also what decides whether the scene has a skyline at all.</summary>
        public float RockHeight { get; set; } = 44f;

        /// <summary>How far apart the small outcrops' cells sit — a lattice rotated against the monoliths'.</summary>
        public float OutcropSpacing { get; set; } = 110f;

        /// <summary>Fraction of outcrop cells that carry one.</summary>
        public float OutcropChance { get; set; } = 0.5f;

        /// <summary>Height of the tallest outcrop — boulder scale, the cue that says how big the monoliths are.
        /// Kept near the outcrop's own width in <c>Outback.fx</c>: a rock much wider than it is tall is a disc,
        /// and a gullied disc is a starfish.</summary>
        public float OutcropHeight { get; set; } = 7f;
    }

    /// <summary>
    /// The rock's and the ground's materials. Reflectances are <b>linear</b>: the rock runs between an
    /// oxidised iron red in the shade and a bright orange where the sun rakes it, the ground between deep red
    /// sand and the pale dust that gathers on it.
    /// </summary>
    public sealed class OutbackSurfaceConfig
    {
        /// <summary>Rock in shadow — the oxidised iron red of a south face.</summary>
        public Rgb RockColorDeep { get; set; } = new(0.130f, 0.030f, 0.011f);

        /// <summary>Rock the sun rakes — the orange that makes this scene recognisable at a glance.</summary>
        public Rgb RockColorBright { get; set; } = new(0.40f, 0.115f, 0.035f);

        /// <summary>
        /// Desert varnish: the near-black mineral skin left where water has run repeatedly down one line. It is
        /// what turns a red dome into a weathered one, and the streaks are the strongest single cue that this
        /// rock has been standing a long time.
        /// </summary>
        public Rgb VarnishColor { get; set; } = new(0.030f, 0.021f, 0.019f);

        /// <summary>How much of the flank the varnish claims. 0 leaves clean unstreaked rock.</summary>
        public float VarnishStrength { get; set; } = 0.55f;

        /// <summary>How hard the varnish glints. The one glossy thing in the scene — the ground never takes it.</summary>
        public float VarnishGloss { get; set; } = 0.5f;

        /// <summary>Roughly how many gullies run round a formation, over 2·pi. They are cut into the geometry, so
        /// they reach the silhouette — and so they must stay several grid cells wide, which is what caps this.</summary>
        public float RibCount { get; set; } = 3.2f;

        /// <summary>How deeply a gully cuts into the formation's radius, as a fraction of it.</summary>
        public float RibDepth { get; set; } = 0.18f;

        /// <summary>
        /// How rough the rock's own face is (world units of relief) — the scale below the gullies. It is what a
        /// near monolith is read from: without it the closest formation, which from the play camera fills a
        /// third of the frame, is one airbrushed orange dome however well the gullies shape its silhouette.
        /// </summary>
        public float RockRelief { get; set; } = 0.55f;

        /// <summary>Red sand.</summary>
        public Rgb SoilColor { get; set; } = new(0.34f, 0.095f, 0.032f);

        /// <summary>The paler dust that gathers on it in patches — sand is never one colour.</summary>
        public Rgb SoilColorPale { get; set; } = new(0.50f, 0.215f, 0.088f);

        /// <summary>Dry spinifex: the olive-straw of a tussock that has not seen rain this year.</summary>
        public Rgb SpinifexColor { get; set; } = new(0.155f, 0.148f, 0.055f);

        /// <summary>World units between hummocks. Plants competing for the same water space themselves out,
        /// which is why the field that draws them is cellular rather than a noise mottle.</summary>
        public float SpinifexSpacing { get; set; } = 2.6f;

        /// <summary>How much of the ground the hummocks claim where they grow. 0 is bare gibber plain.</summary>
        public float SpinifexCover { get; set; } = 0.7f;

        /// <summary>How high a tussock stands (world units of relief), so it catches the sun on its own dome.</summary>
        public float SpinifexRelief { get; set; } = 0.09f;

        /// <summary>How much of the sky's hemisphere light fills the flats.</summary>
        public float AmbientStrength { get; set; } = 0.62f;
    }

    /// <summary>The air: the red dust the distance haze is tinted by, and the heat coming off the ground.</summary>
    public sealed class OutbackAirConfig
    {
        /// <summary>The dust's own colour (linear), lit by the sky and the sun where it is applied.</summary>
        public Rgb HazeTint { get; set; } = new(0.45f, 0.26f, 0.155f);

        /// <summary>
        /// How far the distance haze is carried from the dome's horizon colour towards the dust. High, and
        /// deliberately: at 0.45 a dome with a teal horizon (13, the benchmark one) painted the far plain and
        /// the shadowed flank of every monolith green, which is aerial perspective doing exactly what it
        /// should and still the wrong picture. Red dust in the air is what the outback's distance is made of,
        /// so the scene keeps its own colour under any of the eighteen domes instead of borrowing one.
        /// </summary>
        public float DustStrength { get; set; } = 0.62f;

        /// <summary>World distance over which the plain melts into the skyline. Must stay inside the terrain
        /// grid's half-extent (500), or the mesh's own edge shows against the dome.</summary>
        public float HorizonHazeDistance { get; set; } = 480f;

        /// <summary>
        /// Heat shimmer over the middle distance — the one thing here that moves besides the cloud shadows and
        /// the birds. It rides <c>haze · (1 − haze)</c>, so it dies at both ends: the near ground stays steady
        /// and the skyline stays clean.
        /// </summary>
        public float HeatShimmer { get; set; } = 0.16f;

        /// <summary>The wind, a direction in the XZ plane. It only leans the shimmer — nothing else here blows.</summary>
        public Vec2 Wind { get; set; } = new(0.91f, 0.41f);
    }
}
