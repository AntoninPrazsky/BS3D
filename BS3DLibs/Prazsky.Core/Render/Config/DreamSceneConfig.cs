using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the dream backdrop: a hallucinatory skyscape — slow marbled colour across the whole
    /// sky, hard morphing solids melting into one another, soft luminous orbs and fast sparks. The tenth
    /// scene, and like Space it replaces the SKY rather than the ground: no dome, no weather, no horizon,
    /// and its own light rig (<see cref="Lighting"/>). Deliberately a scene of contrasts — sharp against
    /// blurred, fast against slow — which is what the groups below dial.
    /// <para>
    /// Named nested objects, never arrays: the map editor's PropertyGrid is built <c>IgnoreCollections</c>,
    /// so a list would be invisible in the live scene-config editor.
    /// </para>
    /// </summary>
    public sealed class DreamSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Dream;

        /// <summary>
        /// The floor colour of the sky between everything else (linear). Not black — a dream has no void,
        /// only deeper colour; a violet-blue that keeps the darkest frame reading as saturated night.
        /// </summary>
        public Rgb DeepColor { get; set; } = new(0.012f, 0.006f, 0.028f);

        /// <summary>The cosine palette every element indexes — the scene's whole colour identity.</summary>
        public DreamPaletteConfig Palette { get; set; } = new();

        /// <summary>The background marbling: the slow broad flow and the fast sharp ribbons through it.</summary>
        public DreamBackgroundConfig Background { get; set; } = new();

        /// <summary>The floating solids: raymarched, tumbling, melting between forms.</summary>
        public DreamShapesConfig Shapes { get; set; } = new();

        /// <summary>The soft orbs and the fast sparks — the blurred and the quick halves of the contrast.</summary>
        public DreamGlowsConfig Glows { get; set; } = new();

        /// <summary>The scene's own light rig — it draws no dome, so a dome-derived rig would be a lie.</summary>
        public DreamLightingConfig Lighting { get; set; } = new();
    }

    /// <summary>
    /// A cosine palette: <c>colour(t) = A + B·cos(2π(C·t + D))</c>. Every element of the dream indexes this
    /// one ramp at its own phase, which is what keeps a frame full of saturated colour reading as one
    /// hallucination rather than as a box of crayons. The defaults run magenta–cyan–amber–violet.
    /// </summary>
    public sealed class DreamPaletteConfig
    {
        /// <summary>The ramp's centre (linear).</summary>
        public Rgb A { get; set; } = new(0.42f, 0.36f, 0.44f);

        /// <summary>The ramp's swing (linear).</summary>
        public Rgb B { get; set; } = new(0.38f, 0.34f, 0.40f);

        /// <summary>Cycles per unit of t, per channel — unequal values are what make the hues rotate.</summary>
        public Rgb C { get; set; } = new(1.0f, 1.0f, 1.0f);

        /// <summary>Phase offset per channel.</summary>
        public Rgb D { get; set; } = new(0.00f, 0.33f, 0.67f);
    }

    /// <summary>The marbled background: sine fields on the view direction under a domain warp.</summary>
    public sealed class DreamBackgroundConfig
    {
        /// <summary>Bands per unit of direction — how fine the marbling is.</summary>
        public float SwirlScale { get; set; } = 2.6f;

        /// <summary>How far the field bends its own sampling direction. 0 is straight bands; the warp is
        /// what turns bands into marbling.</summary>
        public float SwirlWarp { get; set; } = 0.55f;

        /// <summary>The broad marbling's drift rate. Slow — it should read over minutes.</summary>
        public float SpeedSlow { get; set; } = 0.05f;

        /// <summary>The sharp ribbons' travel rate. Fast — they cross in seconds.</summary>
        public float SpeedFast { get; set; } = 0.6f;

        /// <summary>Exponent on the ribbon layer: higher is thinner and sharper.</summary>
        public float RibbonSharpness { get; set; } = 7f;

        /// <summary>Overall level of the marbling (linear). Kept well under the glare threshold — the
        /// background is the canvas, and at 0.32 it washed out the orbs, sparks and solids hung on it; the
        /// darker canvas is what lets the glows read as glows.</summary>
        public float Brightness { get; set; } = 0.24f;
    }

    /// <summary>
    /// The floating solids: six raymarched forms that tumble on their own slow orbits, each melting between
    /// a sphere, a rounded box and a torus.
    /// </summary>
    public sealed class DreamShapesConfig
    {
        /// <summary>
        /// How far out the solids roam (world units). They write no depth — the whole sky is drawn behind
        /// the scene — so they must stay well outside anything the camera can stand near (the island is 26,
        /// the menu orbit 44): a solid that drifted between the lens and the island would draw BEHIND it.
        /// </summary>
        public float OrbitRadius { get; set; } = 130f;

        /// <summary>Base half-size of a solid (world units).</summary>
        public float Size { get; set; } = 16f;

        /// <summary>How fast a solid melts between its forms.</summary>
        public float MorphSpeed { get; set; } = 0.22f;

        /// <summary>How much of the palette glows from inside a solid (linear).</summary>
        public float Emission { get; set; } = 0.85f;

        /// <summary>How much of the marbled sky a solid mirrors — the glassy half of its shading.</summary>
        public float Reflection { get; set; } = 0.9f;
    }

    /// <summary>The soft orbs and the fast sparks.</summary>
    public sealed class DreamGlowsConfig
    {
        /// <summary>Gaussian sigma of a soft orb (world units) — the blurred pole of the sharp/soft contrast.</summary>
        public float OrbRadius { get; set; } = 26f;

        /// <summary>
        /// Peak linear radiance of an orb's core. Allowed over the glare threshold deliberately: an orb is
        /// a smooth area hundreds of pixels wide, so it blooms steadily — the planet's lit-limb reasoning.
        /// </summary>
        public float OrbBrightness { get; set; } = 0.85f;

        /// <summary>
        /// Peak linear radiance of a spark's head. Kept AT the glare threshold rather than over it: a spark
        /// is small, and a small point over the threshold is sampled stochastically by the glare's sparse
        /// grid and flickers. Sparks read fast through their trails instead.
        /// </summary>
        public float SparkBrightness { get; set; } = 0.65f;

        /// <summary>How fast the sparks cross the sky.</summary>
        public float SparkSpeed { get; set; } = 0.55f;
    }

    /// <summary>
    /// The dream's own light rig, for the reason Space states one: the scene draws no dome, so a
    /// dome-derived rig would be a lie — and this one is deliberately coloured, so the island, the gun and
    /// the balls sit IN the hallucination instead of standing greyly in front of it. The drain's gold beads
    /// are metallic and live almost entirely off this ambient, which is why neither half is near zero.
    /// </summary>
    public sealed class DreamLightingConfig
    {
        /// <summary>The hemisphere ambient from above (linear) — a warm violet, the marbling's own cast.</summary>
        public Rgb SkyAmbient { get; set; } = new(0.11f, 0.06f, 0.14f);

        /// <summary>The bounce from below (linear) — teal, the opposing side of the palette.</summary>
        public Rgb GroundAmbient { get; set; } = new(0.03f, 0.07f, 0.075f);

        /// <summary>What the key light is tinted by (linear, ~1 per channel) — slightly rose.</summary>
        public Rgb KeyTint { get; set; } = new(1.06f, 0.92f, 1.08f);

        /// <summary>What the back/fill light is tinted by (linear, ~1 per channel) — cyan, so the fill
        /// argues with the key the way everything in this scene argues.</summary>
        public Rgb BackTint { get; set; } = new(0.62f, 0.95f, 1.05f);
    }
}
