using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the Grid backdrop (#393): an early-1980s computer-graphics vocabulary worn as a
    /// style rather than as a limitation — a flat, glowing circuit-board floor traced by a Hilbert curve
    /// under a near-black, starless void. Named mathematics rather than noise: nothing here is a summed
    /// sine field, because the whole point is a look a computer had <b>before</b> it could afford to fake
    /// one.
    /// <para>
    /// The twentieth scene, and the third that belongs to <b>both</b> scene families at once, after the
    /// Moon (#125) and the aurora (#205): solid terrain (<see cref="SceneRenderer.IsSolidTerrainScene"/> —
    /// the island's footprint is cut out of the ground and the dark pit shaft backs the drain) <b>and</b>
    /// sky-replacing (<see cref="SceneRenderer.ReplacesSky"/> — no dome, no clouds, black clear, its own
    /// light rig). Every colour is <b>linear radiance</b>.
    /// </para>
    /// <para>
    /// <b>Deliberately backdrop-only</b> — the issue's own recommended default. The balls, the island and
    /// the gun stay on the ordinary lit <c>InstancedModel.fx</c> path, tinted by <see cref="Lighting"/>
    /// exactly as every other scene's rig tints them; nothing here gives them a second, black-body/emissive
    /// shading mode. A full black-body frame across the shared instanced path is named in the issue as a
    /// much bigger, separate ask, and is not what this pass claims.
    /// </para>
    /// </summary>
    public sealed class GridSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Grid;

        /// <summary>No weather: this scene draws no dome and no cloud deck, exactly as the Moon's and the aurora's do not.</summary>
        public GridSceneConfig() => Weather = WeatherPreset.Clear;

        /// <summary>
        /// The empty sky, before the dither (linear). Not exactly zero — a frame that goes to zero reads as
        /// a hole rather than as a void — but darker than every other sky-replacing scene's: this one is
        /// meant to read as "nothing drawn" rather than as another night sky, which is also why it carries
        /// no starfield (see <see cref="SceneRenderer.ReplacesSky"/>'s doc and "The Grid" in
        /// <c>docs/scenes.md</c>).
        /// </summary>
        public Rgb VoidColor { get; set; } = new(0.0006f, 0.0026f, 0.0040f);

        /// <summary>The glowing floor.</summary>
        public GridTerrainConfig Terrain { get; set; } = new();

        /// <summary>What lights the island, the gun and the balls here, since there is no dome to derive it from.</summary>
        public GridLightingConfig Lighting { get; set; } = new();
    }

    /// <summary>
    /// The floor: genuinely flat (a constant world Y, not a height field), lit only by its own two kinds of
    /// glowing line — an ordinary grid, and a Hilbert-curve trace threading through it that reads as a
    /// circuit-board pattern rather than a random subset of the grid. Being flat is the honest source of
    /// most of this scene's cheapness: no octave sum, no per-pixel gradient normal (the normal is always
    /// straight up), and the camera-centred mesh itself can be coarse, because nothing is displaced at
    /// vertex resolution — see <c>Grid.fx</c>'s own header.
    /// </summary>
    public sealed class GridTerrainConfig
    {
        /// <summary>The floor's world Y — the same plane every other terrain scene's clearing sits at.</summary>
        public float LevelY { get; set; } = -13.5f;

        /// <summary>The size of one grid cell, in world units — the one "zoom" dial on the Hilbert tile as well as the plain grid (see <c>Grid.fx</c>'s own header for why the tile order itself is a shader constant rather than a second tunable dial).</summary>
        public float CellSize { get; set; } = 8f;

        /// <summary>How thick an ordinary glowing grid line is, in world units.</summary>
        public float LineWidth { get; set; } = 0.12f;

        /// <summary>How much wider the Hilbert-curve trace draws than an ordinary grid line — bolder as well as brighter, the same way a real circuit board's traces read as traces rather than as a differently-coloured part of the background lattice.</summary>
        public float AccentWidthScale { get; set; } = 2.4f;

        /// <summary>The dark body between the lines (linear). Not exactly the void colour and not exactly zero — a floor that matched the sky exactly would not read as a floor at grazing angles, and a floor at zero reads as a hole.</summary>
        public Rgb BodyColor { get; set; } = new(0.0015f, 0.0055f, 0.0085f);

        /// <summary>The ordinary grid lines (linear) — dim on purpose, kept under the glare threshold so the plain lattice never blooms and only the Hilbert trace does.</summary>
        public Rgb LineColor { get; set; } = new(0.05f, 0.34f, 0.52f);

        /// <summary>
        /// The Hilbert-curve trace (linear) — deliberately past the glare threshold, so the one deliberately
        /// drawn shape on this floor is the one thing on it that blooms, the same way the aurora's ribbons
        /// and a neon window are allowed to where a star is not.
        /// </summary>
        public Rgb AccentColor { get; set; } = new(0.55f, 2.00f, 2.35f);

        /// <summary>How far out the floor fades to the void colour. A flat floor with a hard edge at the far plane reads as a wall, not a vanishing point; this is what lets the grid recede into the sky instead.</summary>
        public float HorizonHazeDistance { get; set; } = 420f;
    }

    /// <summary>
    /// The Grid's light rig — stated here rather than derived from a dome, for the space scene's reasons
    /// (<see cref="SpaceLightingConfig"/>): there is no dome, and the metallic drain beads live off the
    /// hemisphere ambient, which therefore must not go to zero. A cool cyan-blue cast, restrained rather
    /// than saturated: this rig is what stands in for the whole scene's light on the balls, the island and
    /// the gun (see <see cref="GridSceneConfig"/>'s own class doc on why nothing here goes further than a
    /// tint), so it has to read as light rather than as a colour filter.
    /// </summary>
    public sealed class GridLightingConfig
    {
        /// <summary>The hemisphere ambient from above (linear): the glow of the grid itself, cold and faint.</summary>
        public Rgb SkyAmbient { get; set; } = new(0.022f, 0.052f, 0.062f);

        /// <summary>The bounce from below (linear): the floor's own glow reflected up, a shade dimmer than the sky above it.</summary>
        public Rgb GroundAmbient { get; set; } = new(0.016f, 0.040f, 0.050f);

        /// <summary>The key light's tint (linear, ~1 per channel) — cyan-blue, the grid's own colour standing in for a sun this scene does not have.</summary>
        public Rgb KeyTint { get; set; } = new(0.55f, 0.86f, 1.00f);

        /// <summary>The back/fill light's tint (linear, ~1 per channel) — cooler and dimmer still.</summary>
        public Rgb BackTint { get; set; } = new(0.32f, 0.60f, 0.92f);
    }
}
