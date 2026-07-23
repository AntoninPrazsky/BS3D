using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the procedural city backdrop: the block grid, tower heights, and the window/facade
    /// look. The same city serves two <see cref="SceneKind"/>s — the ordinary warm/cool daytime city and
    /// the <see cref="SceneKind.NeonCity"/> night relight — selected by <see cref="Neon"/>; the neon-only
    /// look lives in <see cref="NeonLook"/>.
    ///
    /// Note: the city's parameters currently live across three places (City.cs generator, the city technique
    /// in InstancedModel.fx, and neon/brightness constants in Testbed.cs). This config gathers them as the
    /// intended single source; wiring each back to its site is a larger follow-up than the terrain scenes.
    /// </summary>
    public sealed class CitySceneConfig : SceneConfig
    {
        /// <summary>City or NeonCity, chosen by <see cref="Neon"/>.</summary>
        [JsonIgnore]
        public override SceneKind Kind => Neon ? SceneKind.NeonCity : SceneKind.City;

        /// <summary>Neon night relight instead of the ordinary warm/cool daytime city (drives <see cref="Kind"/>).</summary>
        public bool Neon { get; set; } = false;

        /// <summary>Centre-to-centre spacing of the street grid, in world units.</summary>
        public float BlockPitch { get; set; } = 30f;

        /// <summary>Width of the streets between blocks; the rest of the pitch is buildable.</summary>
        public float StreetWidth { get; set; } = 9f;

        /// <summary>How far the city reaches from the arena, in blocks (drives the building count).</summary>
        public int RadiusBlocks { get; set; } = 14;

        /// <summary>Roofline height; sits well above the play surface so towers stand against the sky.</summary>
        public float RooflineY { get; set; } = 34f;

        /// <summary>How far above and below the roofline the tops are allowed to wander.</summary>
        public float RooflineSpread { get; set; } = 26f;

        /// <summary>Where every building starts; far down so streets read as canyons falling into darkness.</summary>
        public float BaseY { get; set; } = -420f;

        /// <summary>Towers taper down away from the centre; how much shorter per block outward.</summary>
        public float TaperPerBlock { get; set; } = 1.8f;

        /// <summary>Highest a building directly under the arena is allowed to reach.</summary>
        public float UnderArenaTopY { get; set; } = -78f;

        /// <summary>How much further down the under-arena towers scatter.</summary>
        public float UnderArenaSpread { get; set; } = 90f;

        /// <summary>Horizontal spacing of a window column, in world units (target pitch).</summary>
        public float WindowPitchX { get; set; } = 1.7f;

        /// <summary>Vertical spacing of a window row, in world units (target pitch).</summary>
        public float WindowPitchY { get; set; } = 2.2f;

        /// <summary>How much of each cell is glass horizontally (rest is wall).</summary>
        public float WindowFillX { get; set; } = 0.46f;

        /// <summary>How much of each cell is glass vertically (rest is wall).</summary>
        public float WindowFillY { get; set; } = 0.52f;

        /// <summary>Wall border kept clear of glass at every building edge, in world units.</summary>
        public float WindowMargin { get; set; } = 0.9f;

        /// <summary>Fraction of the windows that are lit.</summary>
        public float WindowLitFraction { get; set; } = 0.42f;

        /// <summary>Warm lamp colour a lit window can take.</summary>
        public Rgb WindowWarm { get; set; } = new(1.0f, 0.78f, 0.44f);

        /// <summary>Cool lamp colour a lit window can take.</summary>
        public Rgb WindowCool { get; set; } = new(0.52f, 0.82f, 1.0f);

        /// <summary>How long a window holds one state before deciding again.</summary>
        public float WindowHoldSeconds { get; set; } = 7.0f;

        /// <summary>How much the hold interval varies from window to window.</summary>
        public float WindowHoldVariation { get; set; } = 24.0f;

        /// <summary>How much of an interval the on/off switch itself takes.</summary>
        public float WindowSwitchFade { get; set; } = 0.06f;

        /// <summary>How brightly a lit window burns; kept under the glare threshold so it does not veil its tower.</summary>
        public float WindowBrightness { get; set; } = 0.35f;

        /// <summary>The neon-night look, used when <see cref="Neon"/> is true.</summary>
        public NeonConfig NeonLook { get; set; } = new();
    }

    /// <summary>The neon night relight of the city: bloom-bright windows plus a ring of magenta/cyan point lights.</summary>
    public sealed class NeonConfig
    {
        /// <summary>Neon window brightness, well over the glare threshold so each lit sign blooms.</summary>
        public float WindowBrightness { get; set; } = 0.9f;

        /// <summary>Number of alternating magenta/cyan point lights ringing the island.</summary>
        public int LightCount { get; set; } = 6;

        /// <summary>Falloff range of each neon point light, in world units.</summary>
        public float LightRange { get; set; } = 58f;

        /// <summary>Radius of the ring of neon point lights around the island.</summary>
        public float LightRadius { get; set; } = 46f;

        /// <summary>Height (Y) of the neon point-light ring.</summary>
        public float LightHeight { get; set; } = -6f;

        /// <summary>Magenta neon point-light colour (linear radiance, over 1).</summary>
        public Rgb Magenta { get; set; } = new(2.6f, 0.25f, 2.2f);

        /// <summary>Cyan neon point-light colour (linear radiance, over 1).</summary>
        public Rgb Cyan { get; set; } = new(0.25f, 2.2f, 2.8f);
    }
}
