using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the mountain backdrop: a snow basin the arena sits in, ringed by ridged
    /// snow-capped peaks that rise with distance and fade into an alpine haze, with falling snow.
    /// </summary>
    public sealed class MountainSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Mountain;

        /// <summary>Basin floor level Y (the basin stays below the island top,
        /// <see cref="ArenaIsland.TOP_Y"/> = -8.5; the old -10.7 referent was the square plaza's recessed glass
        /// bath, removed with the panel arena).</summary>
        public float LevelY { get; set; } = -14f;

        /// <summary>Peak height far out in the field.</summary>
        public float Height { get; set; } = 82f;

        /// <summary>Flat clearing radius the arena sits in.</summary>
        public float ClearingRadius { get; set; } = 95f;

        /// <summary>Distance over which the peaks rise beyond the clearing radius.</summary>
        public float ClearingTransition { get; set; } = 110f;

        /// <summary>Gentle basin relief amplitude around the arena.</summary>
        public float ClearingRelief { get; set; } = 1.5f;

        /// <summary>Snow-cap colour (linear, near white).</summary>
        public Rgb SnowColor { get; set; } = new(0.90f, 0.93f, 0.99f);

        /// <summary>Dark bare rock (linear).</summary>
        public Rgb RockColor { get; set; } = new(0.08f, 0.07f, 0.065f);

        /// <summary>Lighter grey-brown rock (linear).</summary>
        public Rgb RockColorLight { get; set; } = new(0.20f, 0.17f, 0.14f);

        /// <summary>Lower normal.y of the snow-slope band; below this the face sheds snow to bare rock.</summary>
        public float RockSlope { get; set; } = 0.30f;

        /// <summary>Upper normal.y of the snow-slope band; flat/gentle faces above this keep snow.</summary>
        public float SnowSlope { get; set; } = 0.95f;

        /// <summary>Altitude below which the snowline is bare rock.</summary>
        public float SnowlineLow { get; set; } = -15f;

        /// <summary>Altitude above which the snowline is snow.</summary>
        public float SnowlineHigh { get; set; } = 50f;

        /// <summary>Fine rock relief: peak height of the normal-tilting height field on the rock faces.</summary>
        public float RockReliefStrength { get; set; } = 0.5f;

        /// <summary>Fine rock relief: features per world unit.</summary>
        public float RockReliefFrequency { get; set; } = 0.6f;

        /// <summary>Sky-hemisphere ambient strength.</summary>
        public float AmbientStrength { get; set; } = 0.6f;

        /// <summary>Distance over which the distant range fades into the alpine haze.</summary>
        public float HorizonHazeDistance { get; set; } = 500f;

        /// <summary>The falling snow.</summary>
        public SnowConfig Snow { get; set; } = new();
    }

    /// <summary>The mountain's falling snow: a static buffer of billboard flakes animated in the vertex shader.</summary>
    public sealed class SnowConfig
    {
        /// <summary>Number of falling snow flakes in the buffer. Was 1400, at nearly twice the flake size and
        /// half again the opacity — a flake now covers well under half the screen it used to and shows through
        /// far more, so it takes twice as many of them to leave a veil rather than a sprinkle.</summary>
        public int FlakeCount { get; set; } = 2800;

        /// <summary>The volume the flakes fill around the camera.</summary>
        public Vec3 BoxSize { get; set; } = new(70f, 55f, 70f);

        /// <summary>How fast the flakes fall.</summary>
        public float FallSpeed { get; set; } = 9f;

        /// <summary>The wind that drifts the flakes sideways.</summary>
        public Vec2 Wind { get; set; } = new(4f, 1.5f);

        /// <summary>How far a flake sways as it falls.</summary>
        public float Sway { get; set; } = 1.2f;

        /// <summary>The flake size in world units. Was 0.13, which put a flake 23 pixels across at eighteen
        /// units out — a ball is one unit wide, and that is the size a flake was competing with.</summary>
        public float FlakeSize { get; set; } = 0.085f;

        /// <summary>How fast a flake tumbles, in radians per second before its own ±40% variation.</summary>
        public float Spin { get; set; } = 1.1f;

        /// <summary>How deep the six arms of a flake's silhouette are cut, at the spikiest. A flake draws a
        /// hexagonal crystal rather than a disc because a feathered circle is what a lit sphere looks like,
        /// which is how the snow came to read as falling balls (#85).</summary>
        public float Lobing { get; set; } = 0.30f;

        /// <summary>Distance from the lens a flake reaches full strength at; nearer than a quarter of it, it is
        /// invisible. Snow this close to a camera is out of focus, and a crisp near flake reads as a ball.</summary>
        public float NearFade { get; set; } = 7f;

        /// <summary>How much a flake glints as it turns broadside to the light, twice per tumble.</summary>
        public float Twinkle { get; set; } = 0.25f;

        /// <summary>Bright cool white. Its Rec. 709 luminance (~0.76) sits over GLARE_THRESHOLD (0.55 in the
        /// game), so a large near flake can bloom slightly; in practice a flake's few pixels are diluted by
        /// the quarter-resolution glare downsample. Dim the colour, not the opacity, if flakes ever read as
        /// glowing orbs.</summary>
        public Rgb FlakeColor { get; set; } = new(0.72f, 0.76f, 0.82f);

        /// <summary>Snow flake opacity. Was 0.9 — near enough opaque that a flake was a solid white coin, and
        /// the feathered rim that was meant to soften it gets crushed back to white by the tonemap wherever
        /// the flake crosses something dark. Snow reads as snow at well under half that.</summary>
        public float Opacity { get; set; } = 0.6f;
    }
}
