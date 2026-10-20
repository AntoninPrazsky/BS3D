using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the cavern backdrop (#958): an irregular limestone hall over a still black lake, its vault
    /// pocketed and hung with stalactite curtains, flowstone columns standing in the water, clusters of translucent
    /// crystals glowing cyan and magenta on rock islands and along the shore, a constellation of glowworms overhead
    /// and shafts of pale light falling through cracks in the vault. Every surface is real geometry built on the
    /// CPU (<see cref="CavernChamber"/>); the lake mirrors all of it. The eleventh scene, and one that replaces the
    /// SKY: no dome, no weather, no horizon, its own light rig (<see cref="Lighting"/>).
    /// <para>
    /// Named nested objects rather than arrays — the shape the map editor's PropertyGrid once required (it
    /// was built <c>IgnoreCollections</c>; the panel went in #522), kept because a named group reads as a
    /// config and an index does not.
    /// </para>
    /// </summary>
    public sealed class CavernSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Cavern;

        /// <summary>
        /// Underground: there is no sky at all and the deck is suppressed. Stated for the reason space's is.
        /// </summary>
        public CavernSceneConfig() => Weather = WeatherPreset.Clear;

        /// <summary>The hall: its size, its rock and the dark it sinks into.</summary>
        public CavernRockConfig Rock { get; set; } = new();

        /// <summary>The lake the hall stands in.</summary>
        public CavernWaterConfig Water { get; set; } = new();

        /// <summary>The glowworms and the light shafts: the air of the cave.</summary>
        public CavernAirConfig Air { get; set; } = new();

        /// <summary>The crystal clusters.</summary>
        public CavernCrystalConfig Crystals { get; set; } = new();

        /// <summary>The scene's own light rig — it draws no dome, so a dome-derived rig would be a lie.</summary>
        public CavernLightingConfig Lighting { get; set; } = new();
    }

    /// <summary>The hall: an irregular chamber built once on the CPU (<see cref="CavernChamber"/>).</summary>
    public sealed class CavernRockConfig
    {
        /// <summary>The hall's mean horizontal radius at the waterline (world units). Its plan is stretched by
        /// <see cref="Elongation"/>, its walls bulge and recede by a fifth of this and more, and passages run off it.</summary>
        public float HallRadius { get; set; } = 180f;

        /// <summary>How much longer the hall is along its long axis than across it.</summary>
        public float Elongation { get; set; } = 1.3f;

        /// <summary>The vault's height over the lake at its crown, before its pockets and shelves (world units).</summary>
        public float VaultHeight { get; set; } = 104f;

        /// <summary>The seed of the hall's shape and of everything placed in it; the renderer's seed offset is added,
        /// so a launch can roll another hall (<c>sceneseed=0</c> pins the shipped one).</summary>
        public int Seed { get; set; } = 958;

        /// <summary>The rock's albedo (linear): a dark warm grey-brown — a cave is dark, and the dark is what makes
        /// every light in it read.</summary>
        public Rgb RockColor { get; set; } = new(0.060f, 0.050f, 0.042f);

        /// <summary>Flowstone and calcite (linear): the paler, cream-ochre stone the formations are built of, and the
        /// flowstone sheets on the walls. Pale stone is what shows the crystals' colour.</summary>
        public Rgb CalciteColor { get; set; } = new(0.36f, 0.30f, 0.22f);

        /// <summary>The blue-violet the far hall sinks into (linear).</summary>
        public Rgb FogColor { get; set; } = new(0.003f, 0.004f, 0.010f);

        /// <summary>Exponential distance-fog density: the reciprocal is roughly where the rock has lost two thirds of itself.</summary>
        public float FogDensity { get; set; } = 0.0024f;
    }

    /// <summary>The lake: a still black mirror over a rocky bed.</summary>
    public sealed class CavernWaterConfig
    {
        /// <summary>The lake's surface. Below the drain's kill plane, so a ball that falls through the funnel is culled
        /// before it would visibly reach water it can never splash.</summary>
        public float LevelY { get; set; } = -34f;

        /// <summary>What the water scatters back out of its depths (linear, very dark teal).</summary>
        public Rgb DeepColor { get; set; } = new(0.002f, 0.007f, 0.009f);

        /// <summary>How quickly the water swallows what lies under it, per world unit of path.</summary>
        public float Absorption { get; set; } = 0.22f;

        /// <summary>The faint teal light the lake gives the rock low over it (linear): the crystals' and the shafts'
        /// light coming back off the water.</summary>
        public Rgb GlowColor { get; set; } = new(0.010f, 0.040f, 0.045f);

        /// <summary>How hard the surface stirs (the slope of its ripples): still enough to mirror, not glass.</summary>
        public float RippleStrength { get; set; } = 0.045f;

        /// <summary>The ripples' size in world units.</summary>
        public float RippleScale { get; set; } = 7f;

        /// <summary>How fast the ripples drift.</summary>
        public float RippleSpeed { get; set; } = 0.35f;

        /// <summary>The steam standing over the lake (linear, well under the glare threshold).</summary>
        public Rgb MistColor { get; set; } = new(0.012f, 0.020f, 0.026f);

        /// <summary>Optical density of the steam at the surface, per world unit.</summary>
        public float MistDensity { get; set; } = 0.012f;

        /// <summary>The e-folding height over which the steam thins (world units).</summary>
        public float MistHeight { get; set; } = 9f;
    }

    /// <summary>The cave's air: the glowworms and the light shafts.</summary>
    public sealed class CavernAirConfig
    {
        /// <summary>The glowworms' colour (linear radiance): a constellation of points on the vault, dense in places
        /// and bare in others. Over 1 so the pixel or two each one covers survives the tonemap. Black turns them off.</summary>
        public Rgb GlowwormColor { get; set; } = new(0.30f, 1.20f, 1.05f);

        /// <summary>How many glowworms hang on the vault.</summary>
        public int GlowwormCount { get; set; } = 9000;

        /// <summary>The shafts' light (linear): pale, faintly green daylight filtered through wet stone.</summary>
        public Rgb ShaftColor { get; set; } = new(0.13f, 0.16f, 0.15f);

        /// <summary>Overall strength of the shafts and of the pools they light.</summary>
        public float ShaftStrength { get; set; } = 1.8f;
    }

    /// <summary>The crystal clusters: translucent six-sided prisms with pyramid tips, glowing from within.</summary>
    public sealed class CavernCrystalConfig
    {
        /// <summary>One end of the cluster palette — cyan (linear).</summary>
        public Rgb ColorA { get; set; } = new(0.10f, 0.80f, 1.00f);

        /// <summary>The other end — magenta (linear).</summary>
        public Rgb ColorB { get; set; } = new(0.92f, 0.18f, 0.95f);

        /// <summary>Peak emissive level of a cluster's core. Over the glare threshold deliberately — the clusters are the
        /// cavern's lamps, and their bloom is the point.</summary>
        public float Emission { get; set; } = 2.6f;

        /// <summary>How fast the clusters breathe (phase-spread per cluster, so the cave never beats in unison).</summary>
        public float PulseSpeed { get; set; } = 0.5f;

        /// <summary>How much light a cluster throws on the rock and the water round it.</summary>
        public float WallLight { get; set; } = 1f;

        /// <summary>How much of the nearest clusters' light reaches the island, the gun and the balls (through the scene
        /// lights); 0 keeps the arena on the rig alone.</summary>
        public float ArenaLight { get; set; } = 1f;
    }

    /// <summary>
    /// The cavern's own light rig, for the reason Space states one: the scene draws no dome. Dim and cool —
    /// a cave lit by its own bioluminescence — with the ground bounce carrying the lake's teal up onto the
    /// island's underside. The drain's gold beads live almost entirely off this ambient, which is why
    /// neither half is near zero.
    /// </summary>
    public sealed class CavernLightingConfig
    {
        /// <summary>The hemisphere ambient from above (linear) — cool rock-filtered light.</summary>
        public Rgb SkyAmbient { get; set; } = new(0.045f, 0.055f, 0.075f);

        /// <summary>The bounce from below (linear) — the lake's teal.</summary>
        public Rgb GroundAmbient { get; set; } = new(0.020f, 0.050f, 0.058f);

        /// <summary>What the key light is tinted by (linear, ~1 per channel) — the shafts' pale cast.</summary>
        public Rgb KeyTint { get; set; } = new(0.88f, 0.98f, 1.06f);

        /// <summary>What the back/fill light is tinted by (linear, ~1 per channel) — faintly magenta, the
        /// crystals answering from the shore.</summary>
        public Rgb BackTint { get; set; } = new(0.95f, 0.72f, 1.00f);
    }
}
