using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the procedural city backdrop: the block grid, tower heights, and the window/facade
    /// look. The same city serves two <see cref="SceneKind"/>s — the ordinary warm/cool daytime city and
    /// the <see cref="SceneKind.NeonCity"/> night relight — selected by <see cref="Neon"/>; the neon-only
    /// look lives in <see cref="NeonLook"/>.
    ///
    /// The config is wired to all three of its sites: <see cref="City"/>'s constructor reads the layout,
    /// <see cref="InstancedModelRenderer.CityConfig"/> pushes the window look to the shader on every city
    /// draw, and the caller reads <see cref="WindowBrightness"/> / <see cref="NeonLook"/> for the day/neon
    /// relight — which is what lets the map editor's PropertyGrid edit the city live.
    /// </summary>
    public sealed class CitySceneConfig : SceneConfig
    {
        /// <summary>City or NeonCity, chosen by <see cref="Neon"/>.</summary>
        [JsonIgnore]
        public override SceneKind Kind => Neon ? SceneKind.NeonCity : SceneKind.City;

        /// <summary>
        /// The campaign's last chapter plays here at morning with the neon off, and an overcast sheet is what makes that morning read as a morning rather than as a noon. It also flattens the ground light, which is what a chapter about telling neighbouring hues apart wants behind it.
        /// </summary>
        public CitySceneConfig() => Weather = WeatherPreset.Overcast;

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

        /// <summary>
        /// Where every building starts: the street level, which <see cref="CityStreets"/> draws (#399). It was
        /// -420 until then, so the streets read as canyons falling into darkness, and they did not: 400 units under
        /// the island no camera ever reached, and the boxes ended over the open sky dome, which showed as bright
        /// slits between the towers from any view that looked down. At -100 the generator's bounds put a tower between
        /// 83 and 156 units tall at 14 blocks (38 to 71 floors), and the island's top stands 91.5 over the square.
        /// </summary>
        public float BaseY { get; set; } = -100f;

        /// <summary>Towers taper down away from the centre; how much shorter per block outward.</summary>
        public float TaperPerBlock { get; set; } = 1.8f;

        /// <summary>
        /// Which city the generator makes. It was a constant in each of the three executables until #471's
        /// follow-up, which is how the day city and the neon city came to be the <b>same city</b>.
        /// </summary>
        public int Seed { get; set; } = 20260720;

        /// <summary>
        /// <b>And the neon city is a different city, not the same one relit.</b> The owner's report: the two
        /// scenes had buildings in the same places at the same sizes, which is what happens when one seed and
        /// one set of layout dials serve both. These are the neon city's own — a second seed, so no block
        /// carries the same tower, and a skyline of its own: a downtown at night reads as taller, tighter and
        /// more uneven than a daylit business district, so the blocks are closer together, the roofline higher
        /// and its spread wider.
        /// <para>
        /// Only the LAYOUT differs. The facades, the windows and the neon are the day city's own dials and
        /// <see cref="NeonLook"/>'s — the complaint was about where the buildings stand and how big they are,
        /// and making the two cities differ in material as well would be a second change wearing this one's
        /// clothes.
        /// </para>
        /// </summary>
        public NeonCityLayoutConfig NeonLayout { get; set; } = new();

        /// <summary>
        /// The seven figures <see cref="City"/> lays a grid out from, for whichever of the two cities is being
        /// built. <see cref="BaseY"/> is not among them: both cities stand on the one street level, because the
        /// island hangs at a fixed height over it and the drain would otherwise reach a different floor in each.
        /// </summary>
        public CityLayout LayoutFor(bool neon) => neon
            ? new CityLayout(NeonLayout.Seed, NeonLayout.BlockPitch, NeonLayout.StreetWidth,
                NeonLayout.RadiusBlocks, BaseY, NeonLayout.RooflineY, NeonLayout.RooflineSpread,
                NeonLayout.TaperPerBlock)
            : new CityLayout(Seed, BlockPitch, StreetWidth, RadiusBlocks, BaseY, RooflineY, RooflineSpread,
                TaperPerBlock);

        /// <summary>
        /// Albedo of the plaster between the windows by day, in <b>linear</b> radiance. A real albedo, not a
        /// dark one: neither specular term is multiplied by albedo, so with a near-black facade the whole
        /// brightness of a tower was its white highlight and its grazing sky reflection — a dark surface under
        /// a bright mirror, which is exactly how glass reads. The shine belongs to the windows
        /// (<see cref="WindowReflectionBoost"/>), and what lights the wall instead is diffuse light.
        /// </summary>
        public Rgb FacadeColor { get; set; } = new(0.17f, 0.17f, 0.168f);

        /// <summary>
        /// Albedo of the same plaster under the neon relight — dark, so the neon carries the scene whatever the
        /// dome, but not black: a matte wall with nothing to reflect would otherwise be a silhouette.
        /// </summary>
        public Rgb FacadeNeonColor { get; set; } = new(0.045f, 0.044f, 0.052f);

        /// <summary>
        /// How far a building's plaster tone wanders from <see cref="FacadeColor"/>, as a fraction either way
        /// (0 = every tower the same shade). Real renders are mixed and painted and weathered per building —
        /// and once the wall is matte its tone is the only variety it has, the tonal spread across the skyline
        /// having come from the mirror that is now gone.
        /// </summary>
        public float FacadeColorVariation { get; set; } = 0.25f;

        /// <summary>
        /// Peak height of the plaster's grain in world units (0 = a mathematically flat wall). This, not the
        /// reflectance, is what makes a facade read as rendered plaster: a flat box face takes the light as a
        /// flat box face however its specular is tuned. Only tilts the normal, so silhouettes stay clean, and
        /// every octave band-limits itself against the pixel footprint — full on the towers around the arena,
        /// silently gone on the skyline behind them.
        /// </summary>
        public float FacadeGrainStrength { get; set; } = 0.018f;

        /// <summary>
        /// Cells of the coarsest noise octave per world unit: larger is a finer grain. Three finer octaves ride
        /// on top, each fading out on its own once a screen pixel grows past half its cell.
        /// </summary>
        public float FacadeGrainFrequency { get; set; } = 2.2f;

        /// <summary>
        /// How much the grain also <i>shades</i> — the ambient it keeps out of its own hollows, and the mottling
        /// of the plaster's tone (0 = the normal is tilted and nothing else). It is what makes the grain visible
        /// at all at a tower's distance, where a few degrees of tilt move a matte surface by a percent: relief
        /// by normal alone has its bumps lit and its hollows just as bright, which is why it reads as a painted
        /// texture rather than as a surface. The mottling is the stronger of the two cues at this range.
        /// </summary>
        public float FacadeGrainShading { get; set; } = 0.35f;

        /// <summary>
        /// How polished the plaster is: 0 = fully rough (the sky reaches it as its average, and the Fresnel
        /// grazing mirror is gone), 1 = a polished slab, which is what the whole facade used to be. This is the
        /// dial that decides whether the city is built of plaster or of glass.
        /// </summary>
        public float FacadeSmoothness { get; set; } = 0.05f;

        /// <summary>How much of the direct lights' highlight the plaster shows (1 = as much as any other surface).</summary>
        public float FacadeHighlight { get; set; } = 0.1f;

        /// <summary>Direct highlight of the window glass, against the plaster's <see cref="FacadeHighlight"/>.</summary>
        public float WindowHighlightBoost { get; set; } = 1.6f;

        /// <summary>
        /// Albedo of the glass, in <b>linear</b> radiance — dark, because what is behind a pane is a dim room
        /// and not a rendered wall. This is the other half of why the windows read as glass while the plaster
        /// does not: a dark surface under a bright mirror is what glass looks like, which is the same
        /// combination that was wrong on the wall. It is also what gives a facade its variation back, a pane
        /// being dark where it faces nothing and bright where it catches the sky.
        /// </summary>
        public Rgb WindowGlassColor { get; set; } = new(0.02f, 0.025f, 0.032f);

        /// <summary>
        /// How much more of the sky a window mirrors than the scene's own
        /// <see cref="InstancedModelRenderer.SpecularAmbientStrength"/> asks for — a multiple, because the
        /// panes are the one part of a facade that really is a mirror, and that dial is set low for the wall.
        /// </summary>
        public float WindowReflectionBoost { get; set; } = 5f;

        /// <summary>Horizontal spacing of a window column, in world units (target pitch).</summary>
        public float WindowPitchX { get; set; } = 1.7f;

        /// <summary>Vertical spacing of a window row, in world units (target pitch).</summary>
        public float WindowPitchY { get; set; } = 2.2f;

        /// <summary>How much of each cell is glass horizontally (rest is wall).</summary>
        public float WindowFillX { get; set; } = 0.46f;

        /// <summary>How much of each cell is glass vertically (rest is wall).</summary>
        public float WindowFillY { get; set; } = 0.52f;

        /// <summary>
        /// Half-width of the plaster frame moulding around each pane, as a fraction of the cell beyond
        /// <see cref="WindowFillX"/>/<see cref="WindowFillY"/> (0 = no frame, just a flat pane). A pane cut
        /// straight into a flat wall reads as a hole in it; a real window is set into a frame, and the frame is
        /// the same render carried proud of the wall. The moulding lives in the ring between the glass edge and
        /// this width past it, so it is plaster -- it catches light and casts its own shadow -- not a second
        /// surface pasted on. ~0.1 is a clear middle weight that reads as a frame from across the arena.
        /// </summary>
        public float WindowFrameWidth { get; set; } = 0.1f;

        /// <summary>
        /// How high the moulding stands proud of the surrounding wall, in world units (0 = a flat pane with no
        /// frame). This is the bead's own relief, not its reflectance: what makes a frame read as standing off
        /// the wall is that its top catches light, its flanks lean the normal, AND it throws a cast shadow on
        /// the wall in the sun's lee. The shadow length scales with this height, so a taller trim reads as more
        /// strongly raised -- a fraction of a unit is enough to cast a visible shadow at the city's scale.
        /// </summary>
        public float WindowFrameHeight { get; set; } = 0.06f;

        /// <summary>
        /// How much the moulding's own relief also <i>shades</i> — the lit flat top lightened and the wall in
        /// its cast shadow darkened (0 = the normal is tilted and nothing else). The same lesson
        /// <see cref="FacadeGrainShading"/> already taught: a tilted normal alone is invisible at a tower's
        /// distance, so the fillet's top must also lighten and its shadow darken for the trim to read as a
        /// raised body rather than a painted stripe.
        /// </summary>
        public float WindowFrameShading { get; set; } = 0.55f;

        /// <summary>
        /// The window surround's albedo, the glazing bars' and the sill's, as a multiple of the wall's own (#435): a
        /// lighter stone surround on a plaster wall, and on the neon city's dark walls a frame only a little less
        /// dark. With the sill and the bars it is what a window reads by at a tower's distance, where the
        /// moulding's relief is under a pixel.
        /// </summary>
        public float WindowFrameTone { get; set; } = 1.6f;

        /// <summary>How tall the sill under each window is, in world units.</summary>
        public float WindowSillHeight { get; set; } = 0.14f;

        /// <summary>How far the sill runs past the surround at each side, in world units.</summary>
        public float WindowSillOverhang { get; set; } = 0.1f;

        /// <summary>How far down the wall the sill's shadow reaches, in world units.</summary>
        public float WindowSillShadow { get; set; } = 0.22f;

        /// <summary>How dark the sill's shadow is (0 = none).</summary>
        public float WindowSillShading { get; set; } = 0.5f;

        /// <summary>How deep the reveal's shadow reaches down the recessed glass from its head, in world units.</summary>
        public float WindowRevealDepth { get; set; } = 0.16f;

        /// <summary>The width of the glazing bars across each pane (one upright, one rail), in world units (0 = none).</summary>
        public float WindowBarWidth { get; set; } = 0.07f;

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

        /// <summary>The equipment on the roofs — masts, dishes, 5G poles, air conditioning (#436).</summary>
        public RooftopConfig Rooftops { get; set; } = new();

        /// <summary>The street level the towers stand on — asphalt, markings, sidewalks, plazas, traffic (#399).</summary>
        public CityStreetsConfig Streets { get; set; } = new();
    }

    /// <summary>
    /// How the street level under the towers looks (#399), read on every draw so the map editor's panel edits it
    /// live. Where the streets run is not here: that is the city's own grid (<see cref="CitySceneConfig.BlockPitch"/>,
    /// <see cref="CitySceneConfig.StreetWidth"/>), so the two cannot disagree. Colours are linear albedo unless
    /// named a radiance. The treatment was read off references rendered locally for #399: a canyon seen from
    /// above by day, one intersection from straight above, a paved square, and the same canyons at night.
    /// </summary>
    public sealed class CityStreetsConfig
    {
        /// <summary>How far each sidewalk reaches out from the block into the street; the asphalt is what is left.</summary>
        public float SidewalkWidth { get; set; } = 1.2f;

        /// <summary>Radius of the curb where two sidewalks meet at a crossing.</summary>
        public float CornerRadius { get; set; } = 2.2f;

        /// <summary>The road surface.</summary>
        public Rgb AsphaltColor { get; set; } = new(0.052f, 0.053f, 0.057f);

        /// <summary>The concrete slabs of a sidewalk around a built block.</summary>
        public Rgb SidewalkColor { get; set; } = new(0.25f, 0.24f, 0.225f);

        /// <summary>The stone paving of a block with no tower on it — the generator's plazas and the square under the island.</summary>
        public Rgb PlazaColor { get; set; } = new(0.30f, 0.28f, 0.25f);

        /// <summary>The curb stones along every sidewalk's edge.</summary>
        public Rgb CurbColor { get; set; } = new(0.36f, 0.35f, 0.33f);

        /// <summary>Lane lines, stop lines and crosswalk stripes — worn paint, not new.</summary>
        public Rgb MarkingColor { get; set; } = new(0.52f, 0.52f, 0.49f);

        /// <summary>How much open sky lights the ground, before the canyon takes its share.</summary>
        public float AmbientStrength { get; set; } = 0.65f;

        /// <summary>
        /// The fraction of the sky still seen from the floor of a street with towers on both sides. A canyon of
        /// this depth sees a few percent of it geometrically; the rest of what lights a real street is light the
        /// facades throw down (<see cref="FacadeBounce"/>), so this is a look, not a view factor.
        /// </summary>
        public float CanyonSkyView { get; set; } = 0.22f;

        /// <summary>The fraction of direct sun that reaches the floor of such a street.</summary>
        public float CanyonSunView { get; set; } = 0.08f;

        /// <summary>Light thrown down by sunlit facades onto a street between towers, as a fraction of the sun.</summary>
        public float FacadeBounce { get; set; } = 0.12f;

        /// <summary>
        /// The distance over which the ground past the city's last block fades into the horizon's colour. Nothing
        /// inside the city fades: the towers take no haze, and a street under one they are not in reads as a veil.
        /// </summary>
        public float HazeDistance { get; set; } = 900f;

        /// <summary>Chance that a parking slot in a lane holds a car.</summary>
        public float CarChance { get; set; } = 0.45f;

        /// <summary>The crowns of the trees in the squares (linear albedo).</summary>
        public Rgb TreeColor { get; set; } = new(0.045f, 0.085f, 0.03f);

        /// <summary>Chance that a place in a square's grid of trees holds one.</summary>
        public float TreeChance { get; set; } = 0.7f;

        /// <summary>Ambient irradiance on the ground in the neon city (linear), in place of the dome's: the neon carries the night whatever the dome.</summary>
        public Rgb NeonAmbient { get; set; } = new(0.010f, 0.010f, 0.016f);

        /// <summary>The street lamps' light in the neon city (linear irradiance at a pool's centre).</summary>
        public Rgb LampColor { get; set; } = new(1.3f, 0.62f, 0.2f);

        /// <summary>How far apart the lamps stand along a curb.</summary>
        public float LampSpacing { get; set; } = 10f;

        /// <summary>How brightly the shops' neon spills onto the sidewalk at a tower's foot, as a multiple of the neon lights' colours.</summary>
        public float NeonSpill { get; set; } = 0.22f;

        /// <summary>The colour the neon city's ground fades into with distance (linear radiance).</summary>
        public Rgb NeonHazeColor { get; set; } = new(0.012f, 0.010f, 0.022f);
    }

    /// <summary>
    /// How the roofs are dressed (#436). The chances are per building and read when the layout is built, so an
    /// edit here re-dresses the city (<see cref="CityRooftops.Rebuild"/>); the distances and the beacon are read
    /// every frame.
    /// </summary>
    public sealed class RooftopConfig
    {
        /// <summary>
        /// Chance that a tower standing above the roofline around it carries a lattice mast. The tall towers are
        /// the skyline, and a mast on a low roof in a canyon is a mast nobody sees.
        /// </summary>
        public float TallMastChance { get; set; } = 0.17f;

        /// <summary>The same for a tower below the roofline around it.</summary>
        public float MastChance { get; set; } = 0.025f;

        /// <summary>Chance of one satellite dish on a roof; a second one follows at half this.</summary>
        public float DishChance { get; set; } = 0.5f;

        /// <summary>Chance of a 5G pole on a roof corner; a second one follows at half this.</summary>
        public float SectorPoleChance { get; set; } = 0.38f;

        /// <summary>Chance of air-conditioning units on a roof (one to three of them).</summary>
        public float HvacChance { get; set; } = 0.62f;

        /// <summary>
        /// How much more equipment the neon city carries, as a multiplier on every chance above: the extra
        /// pieces are built with the layout and drawn only in that scene.
        /// </summary>
        public float NeonExtra { get; set; } = 1.45f;

        /// <summary>Beyond this distance the air-conditioning units are not drawn — under a pixel or two from there.</summary>
        public float ClutterDistance { get; set; } = 170f;

        /// <summary>Beyond this distance the dishes and the 5G poles are not drawn. Masts and their beacons always are.</summary>
        public float EquipmentDistance { get; set; } = 320f;

        /// <summary>Seconds between two flashes of the masts' aircraft-warning beacons.</summary>
        public float BeaconPeriod { get; set; } = 1.6f;

        /// <summary>Peak radiance of a beacon's flash, linear; over the glare threshold, so each one blooms.</summary>
        public float BeaconBrightness { get; set; } = 3.2f;

        /// <summary>How brightly a dish's neon ring burns in the neon city, as a multiple of the neon lights' colours.</summary>
        public float NeonRingBrightness { get; set; } = 0.55f;
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

    /// <summary>
    /// The neon city's own layout (#471 follow-up): what makes it a different city from the day one rather
    /// than the same city under different lights. Every figure here has a daylight counterpart on
    /// <see cref="CitySceneConfig"/>; see <see cref="CitySceneConfig.NeonLayout"/> for why only layout differs.
    /// </summary>
    public sealed class NeonCityLayoutConfig
    {
        /// <summary>A second seed, so no block carries the same tower as the day city's.</summary>
        public int Seed { get; set; } = 19940312;

        /// <summary>Closer together than the day city's 30: a night downtown is a tighter grid.</summary>
        public float BlockPitch { get; set; } = 25f;

        /// <summary>And narrower streets with it, so the canyons stay canyons at the smaller pitch.</summary>
        public float StreetWidth { get; set; } = 7.5f;

        /// <summary>One block further out than the day city's 14, which keeps the skyline's silhouette
        /// reaching as far at the smaller pitch.</summary>
        public int RadiusBlocks { get; set; } = 16;

        /// <summary>Taller than the day city's 34.</summary>
        public float RooflineY { get; set; } = 46f;

        /// <summary>And far more uneven than its 26 — the ragged skyline is most of what says "a different
        /// city" from the game camera, which sees rooflines and facades and almost none of the street.</summary>
        public float RooflineSpread { get; set; } = 36f;

        /// <summary>Falls away faster than the day city's 1.8, so the centre reads as a core.</summary>
        public float TaperPerBlock { get; set; } = 2.6f;
    }

    /// <summary>
    /// One city's grid, as <see cref="City"/> reads it — the day city's figures or the neon city's, chosen by
    /// <see cref="CitySceneConfig.LayoutFor"/>. A readonly struct carrying seven numbers: it exists so the
    /// generator takes ONE argument that says which city it is building, rather than seven that a caller could
    /// mix between the two.
    /// </summary>
    public readonly record struct CityLayout(int Seed, float BlockPitch, float StreetWidth, int RadiusBlocks,
        float BaseY, float RooflineY, float RooflineSpread, float TaperPerBlock);
}
