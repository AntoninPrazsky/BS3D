using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the cavern backdrop: a bioluminescent crystal cave over an underground river — deep
    /// noise-carved rock veined with glowing minerals, god rays falling from unseen gaps, crystal clusters
    /// pulsing cyan and magenta and pooling their light on the rock, spores drifting up through the dark.
    /// The eleventh scene, and the third that replaces the SKY (see Space and the Dream): no dome, no
    /// weather, no horizon, its own light rig (<see cref="Lighting"/>).
    /// <para>
    /// Named nested objects, never arrays: the map editor's PropertyGrid is built <c>IgnoreCollections</c>,
    /// so a list would be invisible in the live scene-config editor.
    /// </para>
    /// </summary>
    public sealed class CavernSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Cavern;

        /// <summary>The cave shell — the rock, its veins and the abyssal fog.</summary>
        public CavernRockConfig Rock { get; set; } = new();

        /// <summary>The underground river under the island.</summary>
        public CavernWaterConfig Water { get; set; } = new();

        /// <summary>The god rays and the drifting spores — the air of the cave.</summary>
        public CavernAirConfig Air { get; set; } = new();

        /// <summary>The crystal clusters on the walls.</summary>
        public CavernCrystalConfig Crystals { get; set; } = new();

        /// <summary>The scene's own light rig — it draws no dome, so a dome-derived rig would be a lie.</summary>
        public CavernLightingConfig Lighting { get; set; } = new();
    }

    /// <summary>The cave shell: an analytic cylinder-and-ceiling of noise-shaded rock.</summary>
    public sealed class CavernRockConfig
    {
        /// <summary>The rock cylinder's radius around the world origin (world units). Far enough out that
        /// the walls read as a cavern, near enough that their texture still resolves.</summary>
        public float CaveRadius { get; set; } = 240f;

        /// <summary>The ceiling plane the god rays fall from.</summary>
        public float CeilingY { get; set; } = 95f;

        /// <summary>The walls' base colour (linear) — desaturated blue-grey, kept dark: a cave is dark, and
        /// the dark is what makes every glow in it read.</summary>
        public Rgb RockColor { get; set; } = new(0.030f, 0.036f, 0.052f);

        /// <summary>The glowing mineral veins threading the rock (linear, gently over the rock).</summary>
        public Rgb VeinColor { get; set; } = new(0.05f, 0.14f, 0.16f);

        /// <summary>The abyssal blue-purple the far cave sinks into (linear).</summary>
        public Rgb FogColor { get; set; } = new(0.010f, 0.008f, 0.026f);

        /// <summary>Exponential distance-fog density. The reciprocal is roughly the distance at which the
        /// rock has lost two thirds of itself to the abyss.</summary>
        public float FogDensity { get; set; } = 0.0045f;
    }

    /// <summary>The underground river: a wave-normal mirror of the cave, glowing along its crests.</summary>
    public sealed class CavernWaterConfig
    {
        /// <summary>The river's surface plane. Below the drain's kill plane, so a ball that falls through
        /// the funnel is culled before it would visibly reach water it can never splash.</summary>
        public float LevelY { get; set; } = -34f;

        /// <summary>What the depths transmit (linear, dark).</summary>
        public Rgb DeepColor { get; set; } = new(0.008f, 0.024f, 0.030f);

        /// <summary>The bioluminescent glow the crests carry (linear — allowed over the glare threshold:
        /// a crest is a smooth area wide enough to bloom steadily).</summary>
        public Rgb GlowColor { get; set; } = new(0.10f, 0.55f, 0.60f);

        /// <summary>Interference-wave frequency across the surface.</summary>
        public float WaveScale { get; set; } = 0.16f;

        /// <summary>How fast the waves travel.</summary>
        public float WaveSpeed { get; set; } = 0.8f;

        /// <summary>The caustic shimmer where the eye looks into the water rather than across it.</summary>
        public float CausticStrength { get; set; } = 0.8f;
    }

    /// <summary>The cave's air: the god rays and the spores.</summary>
    public sealed class CavernAirConfig
    {
        /// <summary>The shafts' light (linear) — cool, faintly green: day filtered through wet stone.</summary>
        public Rgb GodRayColor { get; set; } = new(0.10f, 0.13f, 0.12f);

        /// <summary>Overall strength of the shafts.</summary>
        public float GodRayStrength { get; set; } = 0.9f;

        /// <summary>The drifting motes' colour (linear, at the glare threshold's edge — they read through
        /// their slow motion, not through bloom).</summary>
        public Rgb SporeColor { get; set; } = new(0.30f, 0.42f, 0.20f);

        /// <summary>Peak brightness of a mote's core.</summary>
        public float SporeBrightness { get; set; } = 0.8f;
    }

    /// <summary>The crystal clusters: sharp octahedral growths pulsing on the walls.</summary>
    public sealed class CavernCrystalConfig
    {
        /// <summary>One end of the cluster palette — cyan (linear).</summary>
        public Rgb ColorA { get; set; } = new(0.10f, 0.85f, 1.00f);

        /// <summary>The other end — magenta (linear).</summary>
        public Rgb ColorB { get; set; } = new(0.95f, 0.15f, 0.90f);

        /// <summary>Peak emissive level of a pulsing cluster. Over the glare threshold deliberately — the
        /// clusters are the cavern's light sources, and their bloom is the point.</summary>
        public float Emission { get; set; } = 1.6f;

        /// <summary>How fast the clusters pulse (phase-spread per cluster, so the cave never beats in unison).</summary>
        public float PulseSpeed { get; set; } = 0.5f;

        /// <summary>How much of a cluster's light pools on the rock and the water around it — the single
        /// thing that makes the crystals belong to the cave instead of being stickers on it.</summary>
        public float WallLight { get; set; } = 0.55f;
    }

    /// <summary>
    /// The cavern's own light rig, for the reason Space states one: the scene draws no dome. Dim and cool —
    /// a cave lit by its own bioluminescence — with the ground bounce carrying the water's teal up onto the
    /// island's underside. The drain's gold beads live almost entirely off this ambient, which is why
    /// neither half is near zero.
    /// </summary>
    public sealed class CavernLightingConfig
    {
        /// <summary>The hemisphere ambient from above (linear) — cool rock-filtered light.</summary>
        public Rgb SkyAmbient { get; set; } = new(0.045f, 0.055f, 0.075f);

        /// <summary>The bounce from below (linear) — the river's teal glow.</summary>
        public Rgb GroundAmbient { get; set; } = new(0.020f, 0.055f, 0.060f);

        /// <summary>What the key light is tinted by (linear, ~1 per channel) — the god rays' cool cast.</summary>
        public Rgb KeyTint { get; set; } = new(0.88f, 0.98f, 1.06f);

        /// <summary>What the back/fill light is tinted by (linear, ~1 per channel) — faintly magenta, the
        /// crystals answering from the walls.</summary>
        public Rgb BackTint { get; set; } = new(0.95f, 0.72f, 1.00f);
    }
}
