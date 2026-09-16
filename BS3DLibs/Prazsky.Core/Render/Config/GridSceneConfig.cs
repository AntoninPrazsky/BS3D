using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the Grid backdrop (#393): an early-1980s computer-graphics vocabulary worn as a
    /// style rather than as a limitation — a flat, glowing circuit-board floor with a Hilbert curve traced along
    /// its lines, and distant solids with glowing seams whose windows run Conway's Game of Life, under a
    /// near-black, starless void. Named mathematics rather than noise: nothing here is a summed sine field,
    /// because the whole point is a look a computer had <b>before</b> it could afford to fake one.
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

        /// <summary>The distant solids standing on the floor: glowing seams, and windows each running a Game of Life of their own.</summary>
        public GridTowerConfig Towers { get; set; } = new();

        /// <summary>What lights the island, the gun and the balls here, since there is no dome to derive it from.</summary>
        public GridLightingConfig Lighting { get; set; } = new();
    }

    /// <summary>
    /// The floor: genuinely flat (a constant world Y, not a height field), lit only by its own two kinds of
    /// glowing line — an ordinary grid, and a Hilbert curve traced along it, wider and brighter, that reads as a
    /// circuit rather than as a random subset of the grid. Being flat is the honest source of most of this
    /// scene's cheapness: no octave sum, no per-pixel gradient normal (the normal is always straight up), and
    /// the camera-centred mesh itself can be coarse, because nothing is displaced at vertex resolution — see
    /// <c>Grid.fx</c>'s own header.
    /// </summary>
    public sealed class GridTerrainConfig
    {
        /// <summary>The floor's world Y — the same plane every other terrain scene's clearing sits at.</summary>
        public float LevelY { get; set; } = -13.5f;

        /// <summary>The size of one grid cell, in world units — with <see cref="TraceStride"/>, the scale of the Hilbert curve as well as of the plain grid (see <c>Grid.fx</c>'s own header for why the curve's tile order is a shader constant rather than a tunable dial).</summary>
        public float CellSize { get; set; } = 8f;

        /// <summary>
        /// How many grid lines apart the Hilbert curve's lattice is: 1 traces it along every line, 2 along every
        /// other line with a plain one between. A curve through every node lights half of all the lattice's
        /// segments, which from a grazing camera is most of the floor; at 2 the dim lines between keep the curve
        /// reading as a trace laid over a board.
        /// </summary>
        public int TraceStride { get; set; } = 2;

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
    /// The distant solids (issue #393's own "any new hard geometry follows MAGI's combinatorial-solid
    /// discipline" — plain closed rectangular prisms, the project's own <c>BoxMesh</c> vocabulary, nothing
    /// sculpted). Placed once, deterministically (<see cref="Seed"/>), scattered on a ring around the arena far
    /// enough out that they read as background silhouettes rather than as play-field obstacles.
    /// <para>
    /// Two shapes, on purpose rather than one random footprint range. A <b>tower</b> (tall and narrow —
    /// <see cref="TowerHeightMin"/>/<see cref="TowerHeightMax"/> high, <see cref="TowerFootprintMin"/>/<see cref="TowerFootprintMax"/>
    /// across) reads as architecture, which is the one thing the owner's review said these should not look like
    /// — so a <see cref="CubeFraction"/> of the count are instead <b>cubes</b>, sized
    /// (<see cref="CubeSizeMin"/>/<see cref="CubeSizeMax"/>) so a face at the shipped <see cref="WindowCellSize"/>
    /// shows most or all of a 32×32 Life board at once: a big, roughly equilateral block showing its own
    /// generation whole is what makes it an abstract digital object rather than a building with lit windows.
    /// </para>
    /// <para>
    /// Every edge of every solid glows (<see cref="EdgeWidth"/>, <see cref="EdgeColor"/>) — the backlit-costume
    /// rule the floor's lines follow — and its faces are a bank of windows lit by <b>its own</b> Game of Life
    /// (<see cref="GridLife"/>): a board stamped with one of a deck of named patterns, wrapped round the solid's
    /// sides like a label and centred on the side facing the arena, stepped every <see cref="LifeStepInterval"/>
    /// and moved on to its next pattern when it goes stale or has run <see cref="LifePatternGenerations"/>.
    /// </para>
    /// </summary>
    public sealed class GridTowerConfig
    {
        /// <summary>How many solids stand on the floor.</summary>
        public int Count { get; set; } = 18;

        /// <summary>What fraction of <see cref="Count"/> are cubes rather than towers, 0–1.</summary>
        public float CubeFraction { get; set; } = 0.4f;

        /// <summary>Nearest a solid is placed to the arena, in world units — far enough out that none of them is ever mistaken for part of the play field.</summary>
        public float RadiusMin { get; set; } = 140f;

        /// <summary>Farthest a solid is placed, in world units.</summary>
        public float RadiusMax { get; set; } = 380f;

        /// <summary>Shortest a tower stands, in world units.</summary>
        public float TowerHeightMin { get; set; } = 40f;

        /// <summary>Tallest a tower stands, in world units.</summary>
        public float TowerHeightMax { get; set; } = 85f;

        /// <summary>Narrowest a tower's footprint is, per side, in world units.</summary>
        public float TowerFootprintMin { get; set; } = 12f;

        /// <summary>Widest a tower's footprint is, per side, in world units.</summary>
        public float TowerFootprintMax { get; set; } = 22f;

        /// <summary>Smallest a cube's side is, in world units — see the class doc for why this runs far larger than a tower's footprint: the point is a face big enough to show a Life board whole.</summary>
        public float CubeSizeMin { get; set; } = 45f;

        /// <summary>Largest a cube's side is, in world units.</summary>
        public float CubeSizeMax { get; set; } = 75f;

        /// <summary>The seed placement and every solid's Life board are drawn from — fixed rather than time-based, so the Game, the Testbed and the map editor all stand the same solids in the same places showing the same patterns, and two Testbed captures of this scene stay comparable.</summary>
        public int Seed { get; set; } = 393;

        /// <summary>The size of one window pane, in world units, on a solid's face — independent of the floor's own <see cref="GridTerrainConfig.CellSize"/>, since a facade's windows and a circuit board's cells answer different questions.</summary>
        public float WindowCellSize { get; set; } = 2.4f;

        /// <summary>How much of each window cell is the dark mullion between panes, 0–0.5 of the cell. Not zero — a window with no border reads as a single unbroken glowing wall rather than as a bank of separate panes.</summary>
        public float WindowMargin { get; set; } = 0.16f;

        /// <summary>The solid's own dark body (linear) — not exactly <see cref="GridTerrainConfig.BodyColor"/>: a vertical face and a horizontal floor read the void's own ambient differently, the same reason the light rig's own sky and ground ambients differ.</summary>
        public Rgb BodyColor { get; set; } = new(0.0025f, 0.0075f, 0.0110f);

        /// <summary>A lit window (linear) — past the glare threshold like the floor's own Hilbert trace, so a lit face reads as a field of small bright panes rather than a grey chequerboard.</summary>
        public Rgb WindowColor { get; set; } = new(0.60f, 1.85f, 2.05f);

        /// <summary>How wide a solid's glowing seam is, in world units — the full width of the line at a corner, where two faces each draw half of it.</summary>
        public float EdgeWidth { get; set; } = 0.9f;

        /// <summary>A solid's glowing seams (linear) — past the glare threshold, so the silhouette blooms against the void the way the floor's trace does; without them the body is within a few codes of the void and only the lit windows show.</summary>
        public Rgb EdgeColor { get; set; } = new(0.70f, 2.20f, 2.60f);

        /// <summary>How often each board advances a generation, in seconds. The issue's own "a few generations a second, not per-frame — needs to read as a deliberate clock, not a flicker".</summary>
        public float LifeStepInterval { get; set; } = 0.5f;

        /// <summary>How long a cell that has just died keeps glowing, in seconds — the time constant of a phosphor's decay, so a glider leaves a short trail and a pattern being replaced goes out rather than off. Well under <see cref="LifeStepInterval"/>, so the trail is one generation long.</summary>
        public float PhosphorDecay { get; set; } = 0.12f;

        /// <summary>How many generations one pattern runs before its board moves on to the next, even if it has not gone stale — at the shipped <see cref="LifeStepInterval"/> three minutes, so a long level shows more than one.</summary>
        public int LifePatternGenerations { get; set; } = 360;
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
