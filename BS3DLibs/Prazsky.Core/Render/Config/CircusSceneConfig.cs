using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the big top (#690), the twenty-first scene: the inside of a circus tent between shows — red and
    /// cream canvas sweeping up to a crown ring on four king poles, tiers of red seats round a sawdust ring with a red and
    /// gold curb, strings of warm bulbs sagging from the crown to the wall, and coloured spotlights cutting through dusty
    /// air onto the island in the middle of the ring. Drawn from the references rendered for #690 (both models, the
    /// owner's 2026-09-26 ruling). Like the cavern it replaces the SKY: the canvas is the sky, there is no weather and no
    /// horizon, and the light rig is its own (<see cref="Lighting"/>). Unlike the cavern it also stands on ground — the
    /// sawdust is a floor the island is set into — which is why it is a solid-terrain scene too, the Moon's and the
    /// Grid's double membership.
    /// <para>
    /// Every figure is in world units around the world origin, with the floor at <see cref="CircusRingConfig.FloorY"/>
    /// (the island's foot, where every solid scene puts its ground). The scale is the balls': a ball is about half a
    /// metre, so the seats, the curb and the bulbs are drawn at a person's size against them and not at the island's.
    /// </para>
    /// </summary>
    public sealed class CircusSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Circus;

        /// <summary>Indoors: there is no sky to have weather in, and the deck is suppressed. Space's reason.</summary>
        public CircusSceneConfig() => Weather = WeatherPreset.Clear;

        /// <summary>The canvas, the side wall and the king poles.</summary>
        public CircusTentConfig Tent { get; set; } = new();

        /// <summary>The sawdust ring, its curb and the floor round it.</summary>
        public CircusRingConfig Ring { get; set; } = new();

        /// <summary>The tiers of seats round the ring.</summary>
        public CircusSeatingConfig Seating { get; set; } = new();

        /// <summary>The spotlights, the bulb strings and the dusty air they light.</summary>
        public CircusLightsConfig Lights { get; set; } = new();

        /// <summary>The scene's own light rig — it draws no dome, so a dome-derived rig would be a lie.</summary>
        public CircusLightingConfig Lighting { get; set; } = new();
    }

    /// <summary>The tent: a striped canvas cone from the side wall's top up to the crown ring, on four king poles.</summary>
    public sealed class CircusTentConfig
    {
        /// <summary>The side wall's radius. Past the seats' back row, and far enough out that the far wall stands behind
        /// the whole bowl of seats as it does in every reference.</summary>
        public float WallRadius { get; set; } = 96f;

        /// <summary>Where the side wall meets the canvas roof.</summary>
        public float WallTopY { get; set; } = 12f;

        /// <summary>The crown ring at the top of the roof: its radius and its height. The canvas rises from the wall to it.</summary>
        public float CrownRadius { get; set; } = 9f;

        /// <summary>See <see cref="CrownRadius"/>.</summary>
        public float CrownY { get; set; } = 70f;

        /// <summary>How many canvas panels go round the roof, alternating <see cref="Red"/> and <see cref="Cream"/>.
        /// Even, or the last panel meets the first in its own colour.</summary>
        public int PanelCount { get; set; } = 44;

        /// <summary>How far each panel bellies down between its two seams (world units at the wall, less towards the
        /// crown): canvas hung from seams is never a flat cone, and the belly is what makes it read as cloth.</summary>
        public float PanelSag { get; set; } = 1.6f;

        /// <summary>The canvas's two colours (linear albedo): circus red and an aged cream.</summary>
        public Rgb Red { get; set; } = new(0.50f, 0.045f, 0.035f);

        /// <summary>See <see cref="Red"/>.</summary>
        public Rgb Cream { get; set; } = new(0.78f, 0.66f, 0.47f);

        /// <summary>The side wall's curtain (linear albedo) — a deep red velvet hung in folds.</summary>
        public Rgb Curtain { get; set; } = new(0.20f, 0.018f, 0.020f);

        /// <summary>How many king poles hold the roof, and the radius they stand on. Outside the camera's whole orbit
        /// (the backdrop is drawn behind everything, so a pole the lens could stand behind would be drawn behind the
        /// island it ought to hide) and inside the first row of seats.</summary>
        public int KingPoleCount { get; set; } = 4;

        /// <summary>See <see cref="KingPoleCount"/>.</summary>
        public float KingPoleRadius { get; set; } = 46f;

        /// <summary>A king pole's own radius — a mast, not a post.</summary>
        public float KingPoleThickness { get; set; } = 0.9f;

        /// <summary>The poles' painted wood (linear albedo).</summary>
        public Rgb PoleColor { get; set; } = new(0.30f, 0.12f, 0.06f);
    }

    /// <summary>The ring: a disc of raked sawdust inside a low red and gold curb, and the trodden floor round it.</summary>
    public sealed class CircusRingConfig
    {
        /// <summary>The floor's height — the island's foot, where every solid scene puts its ground.</summary>
        public float FloorY { get; set; } = -13.5f;

        /// <summary>The curb's inner radius, its height and its thickness. Just past the camera's widest stand-off, so the
        /// ring the island stands in is the ring the lens looks across.</summary>
        public float CurbRadius { get; set; } = 41f;

        /// <summary>See <see cref="CurbRadius"/>.</summary>
        public float CurbHeight { get; set; } = 1.5f;

        /// <summary>See <see cref="CurbRadius"/>.</summary>
        public float CurbThickness { get; set; } = 1.2f;

        /// <summary>The curb's red (linear albedo) and the gold of its trim.</summary>
        public Rgb CurbRed { get; set; } = new(0.42f, 0.035f, 0.030f);

        /// <summary>See <see cref="CurbRed"/>.</summary>
        public Rgb CurbGold { get; set; } = new(0.80f, 0.52f, 0.16f);

        /// <summary>The sawdust inside the ring (linear albedo) — pale, warm, raked in circles.</summary>
        public Rgb Sawdust { get; set; } = new(0.60f, 0.43f, 0.25f);

        /// <summary>The floor outside the ring (linear albedo) — dark trodden earth and boards.</summary>
        public Rgb Floor { get; set; } = new(0.10f, 0.075f, 0.055f);
    }

    /// <summary>The seats: a bowl of tiers rising from just past the king poles to the side wall.</summary>
    public sealed class CircusSeatingConfig
    {
        /// <summary>Where the first row stands and where the last one ends.</summary>
        public float InnerRadius { get; set; } = 51f;

        /// <summary>See <see cref="InnerRadius"/>.</summary>
        public float OuterRadius { get; set; } = 92f;

        /// <summary>How deep one row is, and how much higher each row sits than the one in front of it. A person's
        /// tread and rise at the balls' scale.</summary>
        public float RowDepth { get; set; } = 2.2f;

        /// <summary>See <see cref="RowDepth"/>.</summary>
        public float RowRise { get; set; } = 1.05f;

        /// <summary>The seats' red (linear albedo) and the tiers' wood.</summary>
        public Rgb SeatRed { get; set; } = new(0.40f, 0.040f, 0.035f);

        /// <summary>See <see cref="SeatRed"/>.</summary>
        public Rgb Wood { get; set; } = new(0.16f, 0.10f, 0.06f);

        /// <summary>How many stair aisles cut the bowl into blocks, and how wide each is (world units).</summary>
        public int AisleCount { get; set; } = 10;

        /// <summary>See <see cref="AisleCount"/>.</summary>
        public float AisleWidth { get; set; } = 3.2f;

        /// <summary>The performers' entrance through the seats, as a bearing in degrees and an opening width (world
        /// units at the front row).</summary>
        public float EntranceBearingDegrees { get; set; } = 200f;

        /// <summary>See <see cref="EntranceBearingDegrees"/>.</summary>
        public float EntranceWidth { get; set; } = 14f;
    }

    /// <summary>The spotlights, the bulb strings and the haze.</summary>
    public sealed class CircusLightsConfig
    {
        /// <summary>The ring the spotlights hang from (a truss under the crown), and how high it is.</summary>
        public float SpotRigRadius { get; set; } = 30f;

        /// <summary>See <see cref="SpotRigRadius"/>.</summary>
        public float SpotRigY { get; set; } = 56f;

        /// <summary>A spot's half-angle in degrees: narrow enough that each beam reads as a shaft of its own. At eleven the
        /// three that hold on the island overlapped into one veil over the whole middle of the frame.</summary>
        public float SpotHalfAngleDegrees { get; set; } = 7f;

        /// <summary>How bright a pool on the floor is, and how much of it the dusty air scatters back as a visible beam.</summary>
        public float SpotIntensity { get; set; } = 1.7f;

        /// <summary>See <see cref="SpotIntensity"/>.</summary>
        public float BeamStrength { get; set; } = 0.00045f;

        /// <summary>How fast the two roving spots sweep (radians a second). Slow: a show waiting to start, not a disco.</summary>
        public float SweepSpeed { get; set; } = 0.11f;

        /// <summary>How many strings of bulbs run from the crown down to the wall, how far apart the bulbs are along one
        /// (world units), and how far a string sags.</summary>
        public int BulbStrings { get; set; } = 16;

        /// <summary>See <see cref="BulbStrings"/>.</summary>
        public float BulbSpacing { get; set; } = 3.4f;

        /// <summary>See <see cref="BulbStrings"/>.</summary>
        public float BulbSag { get; set; } = 7f;

        /// <summary>The bulbs' warm colour (linear radiance at the core; over the glare threshold, so they bloom).</summary>
        public Rgb BulbColor { get; set; } = new(4.2f, 2.5f, 1.0f);

        /// <summary>The dusty air: its colour (linear) and how fast it thickens with distance.</summary>
        public Rgb HazeColor { get; set; } = new(0.034f, 0.022f, 0.014f);

        /// <summary>See <see cref="HazeColor"/>.</summary>
        public float HazeDensity { get; set; } = 0.0040f;

        /// <summary>The house lights between shows (linear): the warm general light that keeps the seats and the canvas
        /// readable outside the spots' pools.</summary>
        public Rgb HouseLight { get; set; } = new(0.21f, 0.14f, 0.088f);
    }

    /// <summary>The big top's own light rig: warm house light, sawdust bounce, and the spots' white key.</summary>
    public sealed class CircusLightingConfig
    {
        /// <summary>The hemisphere ambient from above (linear) — the canvas lit by the house lights.</summary>
        public Rgb SkyAmbient { get; set; } = new(0.20f, 0.13f, 0.090f);

        /// <summary>The bounce from below (linear) — the sawdust's warm tan.</summary>
        public Rgb GroundAmbient { get; set; } = new(0.17f, 0.11f, 0.060f);

        /// <summary>What the key light is tinted by (linear, ~1 per channel) — the spotlights' warm white.</summary>
        public Rgb KeyTint { get; set; } = new(1.05f, 0.95f, 0.80f);

        /// <summary>What the back/fill light is tinted by (linear, ~1 per channel) — the canvas's red glow.</summary>
        public Rgb BackTint { get; set; } = new(1.00f, 0.62f, 0.52f);

        /// <summary>The key's elevation and bearing (degrees): high, from the spot rig overhead.</summary>
        public float KeyElevationDegrees { get; set; } = 64f;

        /// <summary>See <see cref="KeyElevationDegrees"/>.</summary>
        public float KeyAzimuthDegrees { get; set; } = 35f;
    }
}
