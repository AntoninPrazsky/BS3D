using BS3D.Screens;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using Prazsky.Core;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Prazsky.Core.Screens;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D
{
    /// <summary>
    /// The <b>host</b> — what outlives a session (#65). It owns the window and the device, the content, the
    /// shared setting the menus and the game both stand in (the sky, the scene backdrops, the city, the
    /// island and its drain), the linear-radiance render pipeline, the screen stack and the Myra desktop the
    /// menu pages share. The game itself — the simulation, the cluster, the gun, the shot, the HUD — is
    /// <see cref="GameplayScreen"/>, a screen on that same stack; a pause is a page pushed over it.
    /// <para>
    /// The frame is not "draw the screens back to front" naively: the pipeline binds an HDR target, draws
    /// the world into it and resolves once, and gameplay draws in slots <i>inside</i> that sequence. So the
    /// screens own the frame and the host owns the pieces — <see cref="BeginSceneDraw"/>,
    /// <see cref="DrawSettingGlass"/> and <see cref="FinishSceneDraw"/> are the setting's slices, and each
    /// bottom screen (the backdrop, or the gameplay screen) runs the sequence with its own work in the gaps.
    /// </para>
    /// </summary>
    public class BS3DGame : Microsoft.Xna.Framework.Game
    {
        #region The frame

        //The windowed default is 16:9, the narrowest aspect the game targets, so what is framed in a window
        //is the tightest case and a wider display only adds width (CreatePerspectiveFieldOfView takes the
        //vertical FOV, so this is Hor+).
        private const int WINDOW_WIDTH = 1600;
        private const int WINDOW_HEIGHT = 900;

        private const float DEFAULT_EXPOSURE = 1.1f;

        //The camera's whole shake, scaled off CameraShake's defaults through this one dial. The gun throws
        //itself back visibly when it fires, so the camera does not have to carry the shot's force on its
        //own — and carrying all of it read as the lens being hit rather than as a gun going off.
        private const float CAMERA_SHAKE_SCALE = 0.45f;

        private readonly GraphicsDeviceManager _graphics;
        private readonly bool _uncappedFps;

        //Pinned from the command line for a reproducible measurement. The scene is otherwise a different one of
        //the seven every launch, which makes any A/B of the frame's cost meaningless — the seven are nothing
        //like each other in what they cost — and two of them bring a dome of their own, so the dome has to be
        //pinnable too. Null in both means "as the game normally does it".
        private readonly SceneKind? _startupScene;
        private readonly byte? _startupSkyDome;

        //Seeded from the command line and then owned by the settings screen: supersampling resizes the scene
        //target (EnsureSceneTarget compares dimensions, so changing the factor is what recreates it) and the
        //exposure is one uniform on the tonemap. Both are the dials a weak machine and a bright monitor reach
        //for first, which is exactly why they are in the menu rather than only in argv.
        private int _supersampleFactor;
        private float _exposure;
        private bool _fullscreen;

        /// <summary>
        /// What supersampling starts at when nobody says otherwise. Two is the look this game is authored for
        /// — the balls' procedural relief is *shading*, which MSAA does not touch, so the extra samples are
        /// what keep the fine octaves alive. It is also, on a weak GPU, by far the most expensive thing in the
        /// frame: measured on an integrated Vega 10, dropping it to 1 nearly tripled the frame rate, while
        /// cutting the ball count threefold barely moved it. Hence <see cref="TuneQualityToFrameRate"/>.
        /// </summary>
        private const int DEFAULT_SUPERSAMPLE_FACTOR = 2;

        private RecoilCamera _camera;

        //Wall clock. Everything alive in the scene runs off it — the balls' heartbeat, the city's windows —
        //so none of it is tied to a simulation that may later be paused.
        private float _wallClock;

        private bool _wasActive = true;

        /// <summary>The one camera. The front end's backdrop orbits it; the gameplay screen poses it while playing.</summary>
        internal RecoilCamera Camera => _camera;

        /// <summary>The wall clock everything alive runs off, paused or not.</summary>
        internal float WallClock => _wallClock;

        /// <summary>
        /// Whether edge-driven input (presses, clicks) may act this frame. False for one frame after focus
        /// returns: the very click that refocuses a windowed game would otherwise read as a fresh press
        /// against a stale "released" state and fire an unintended shot, since input is not sampled while
        /// inactive. Computed once per <see cref="Update"/> and read by whichever screen is running.
        /// </summary>
        internal bool EdgeInputAllowed { get; private set; }

        #endregion

        #region The overlay (display space, after the resolve)

        //FPS. A DrawableGameComponent, so the component list draws it in base.Draw — last of everything, in
        //display space, with its own SpriteBatch. F12 hides it, as it hides the Testbed's text overlay.
        private InfoRenderer _info;

        //One SpriteBatch and one white texel for everything drawn over the resolve — the gameplay screen's
        //HUD and crosshair strike their bars and shadows from these. No bitmap: one asset fewer to keep in
        //step with the Testbed's.
        private SpriteBatch _spriteBatch;
        private Texture2D _pixel;

        internal SpriteBatch OverlayBatch => _spriteBatch;
        internal Texture2D WhitePixel => _pixel;

        #endregion

        #region Post-processing (linear radiance in, sRGB out — exactly the Testbed's pipeline)

        private RenderTarget2D _sceneTarget;
        private RenderTarget2D _glareBright;
        private RenderTarget2D _glareStreak;

        private Effect _tonemapEffect;
        private Effect _glareEffect;
        private VertexBuffer _fullScreenQuad;

        //Cached in LoadContent: the resolve runs every frame and the by-name indexer is a linear scan over
        //the effect's parameter list. Values that never change after startup (the thresholds, the exposure,
        //the trail widths) are set once there and never touched again; the textures and texel sizes still go
        //out per frame through these references, because the render targets are recreated on every resize.
        private EffectTechnique _glareBrightPassTechnique;
        private EffectTechnique _glareStreakTechnique;
        private EffectParameter _glareSourceTextureParam;
        private EffectParameter _glareSourceTexelSizeParam;
        private EffectParameter _tonemapGlareTextureParam;
        private EffectParameter _tonemapSceneTextureParam;
        private EffectParameter _tonemapSourceTexelSizeParam;
        private EffectParameter _skyCameraPositionParam;

        private static readonly int GLARE_DOWNSAMPLE = 4;
        private static readonly float GLARE_THRESHOLD = 0.55f;
        private static readonly float GLARE_STREAK_LENGTH = 34f;
        private static readonly float GLARE_STREAK_FALLOFF = 3.2f;
        private static readonly float GLARE_INTENSITY = 0.9f;

        //Only used when supersampling is off: multisampling antialiases geometry edges but not shading, and
        //the balls' procedural relief is shading.
        private static readonly int MSAA_SAMPLES = 8;

        #endregion

        #region Scene

        //Dome 13 is the violet/teal dusk. The neon city reads best under a dark sky — the facades stay dark
        //under any dome, so a bright one only fights the neon it is meant to set off. It is the default the
        //game starts on; the sky setting cycles the whole set, and two scenes bring a dome of their own.
        internal const byte SKY_DOME_COUNT = 18;
        private const byte DEFAULT_SKY_DOME = 13;

        //The sea mirrors the sky, so its whole mood follows the dome and a bright one gives a breezy sea
        //rather than a moody one; the savanna wants the set's warmest gold horizon. The Testbed's own figures.
        private const byte SEA_SKY_DOME = 13;
        private const byte SAVANNA_SKY_DOME = 14;

        private byte _skyDome = DEFAULT_SKY_DOME;

        /// <summary>
        /// Which of the seven settings the frame stands in — the backdrop the menu's camera orbits and the
        /// one the game is then played in, since the player picks it from the menu and it stays picked. The
        /// city and the neon city are the procedural <see cref="City"/> under two lightings; the other five
        /// are the shared <see cref="SceneRenderer"/>'s self-lit backdrops, the same ones the Testbed and the
        /// map editor draw.
        /// </summary>
        private SceneKind _scene = SceneKind.NeonCity;

        //Every value of the enum, in its declared order (City, Sea, Savanna, Desert, Mountain, Meadow,
        //NeonCity). Written out rather than counted with Enum.GetValues so nothing walks reflection at load,
        //and so the scene menu's labels below can be indexed by the same number.
        internal const int SCENE_COUNT = 7;

        private SceneRenderer _sceneRenderer;

        private SkyDome _sky;
        private Effect _skyEffect;
        private Vector3 _zenithLinear = Vector3.One;
        private Vector3 _horizonLinear = Vector3.One;

        //The weather. Clouds live on a flat plane at a finite altitude rather than as a texture on the dome,
        //which is what lets the same field be both the cloud you look at and the shadow it throws: the sky
        //shader crosses the plane with the view ray, the ball/city/island shader with the sun ray. One field,
        //handed to both shaders from here, so the two cannot be tuned apart by accident.
        private readonly CloudField _clouds = new();

        private static readonly Vector3 CLOUD_SUN_COLOR = new(1.7f, 1.66f, 1.55f);
        private static readonly Vector3 CLOUD_SHADOW_COLOR = new(0.18f, 0.21f, 0.28f);
        private static readonly float CLOUD_DETAIL_STRENGTH = 2.5f;
        private static readonly float CLOUD_OPACITY = 2.4f;
        private static readonly float CLOUD_HORIZON_FADE = 0.16f;
        private static readonly float CLOUD_SUN_STEP = 90f;
        private static readonly float CLOUD_SELF_ABSORPTION = 2.5f;
        private static readonly float CLOUD_SUN_ABSORPTION = 1f;
        private static readonly float CLOUD_SILVER_STRENGTH = 1.2f;
        private static readonly float CLOUD_SILVER_POWER = 12f;

        /// <summary>
        /// The shadowed side of a cloud sees no sun at all — only sky — so it takes the zenith colour far more
        /// completely than any surface the rig lights from two sides.
        /// </summary>
        private static readonly float CLOUD_SHADOW_TINT_STRENGTH = 0.8f;

        /// <summary>
        /// Towards the sun. A direction rather than <c>KeyLightPosition</c>, which is a point forty units off
        /// the island: near enough that its direction fans right across the scene, while a cloud shadow has to
        /// arrive in parallel bands over a city hundreds of units wide.
        /// </summary>
        private static readonly Vector3 SUN_DIRECTION = -DefaultLighting.Light0Direction;

        private static readonly float SKY_TINT_STRENGTH = 0.5f;
        private static readonly float SCENE_AMBIENT_INTENSITY = 0.25f;

        //Zero specular here keeps each mesh's own material specular; the ambient is the scene's flat fill.
        private readonly BasicEffectParams _sceneEffectParams =
            new(Vector3.One * SCENE_AMBIENT_INTENSITY, Vector3.Zero, 0f, Vector3.Zero);

        internal BasicEffectParams SceneEffectParams => _sceneEffectParams;

        private Effect _instancingEffect;

        private readonly CitySceneConfig _cityConfig = new();

        //Fixed, so the skyline is the same city every launch — and so a quality tier that rebuilds it at a
        //smaller radius produces the same towers, minus the outer rings, rather than a different city
        private const int CITY_SEED = 20260720;

        private City _city;
        private BoxMesh _unitBox;
        private InstancedModelRenderer _cityRenderer;

        //The glass drain funnel in the middle of the island, the Testbed's own: a truncated cone from a wide
        //rim flush with the stone down to a small hole, ringed with polished gold at both circles. Every
        //figure here is the Testbed's unchanged, because the island's top is at the Testbed's arena height —
        //the two drains are the same object. The GameplayScreen's session builds a Bepu collision mesh from
        //the same figures (BuildFunnelPhysics), which is why they are internal.
        //
        //These are const rather than static readonly because ISLAND_INNER_RADIUS is initialised from the
        //rim radius: a static field initialiser reads the fields declared above it, so a later reorder of a
        //static readonly would silently leave the island's bore at zero.
        internal const float FUNNEL_TOP_RADIUS = 14f;      //the mouth; the island's bore is cut to exactly this
        internal const float FUNNEL_HOLE_RADIUS = 1.8f;    //the hole at the bottom, comfortably wider than a ball
        internal const float FUNNEL_BOTTOM_Y = -27.5f;     //~19 below the rim: a wall steep enough to run a ball down
        internal const int FUNNEL_SEGMENTS = 64;
        private static readonly Vector3 FUNNEL_GLASS_COLOR = new(0.42f, 0.62f, 0.72f);

        //More opaque than plain glass, so the drain reads as a frosted-glass funnel rather than as a hole
        private const float FUNNEL_GLASS_ALPHA = 0.55f;

        //The gold bead around both circles — what makes the drain read at a glance, since it rings exactly
        //the junction where the stone meets the glass. Drawn metallic (Metalness 1), so the sky it reflects
        //comes back gold instead of the 4 % dielectric white every other surface reflects it with.
        private static readonly Vector3 FUNNEL_RIM_COLOR = new(0.62f, 0.44f, 0.13f);      //warm gold diffuse (sRGB)
        private static readonly Vector3 FUNNEL_RIM_SPECULAR = new(1f, 0.83f, 0.48f);      //gold reflectance (sRGB)
        private const float FUNNEL_RIM_SPECULAR_POWER = 80f;                              //polished: a tight highlight
        private const float FUNNEL_RIM_TOP_TUBE = 0.5f;                                   //bead radius at the mouth
        private const float FUNNEL_RIM_HOLE_TUBE = 0.3f;                                  //bead radius at the hole
        private const int FUNNEL_RIM_TUBE_SEGMENTS = 16;

        private FunnelMesh _funnelMesh;
        private InstancedModelRenderer _funnelRenderer;
        private FunnelRimsMesh _funnelRimsMesh;
        private InstancedModelRenderer _funnelRimsRenderer;
        private BasicEffectParams _funnelRimEffectParams;
        private Matrix _funnelWorld;

        //The round stone island the gun stands on: a ring of stone from the drain's rim out to its own, then
        //a hard vertical edge the city falls away past. No physics floor here — the session's drain mesh is
        //the floor. Const because the session's precise-aim floor (GameplayScreen.ADS_MIN_Y) is derived from
        //it in a constant expression.
        internal const float ISLAND_Y = -8.5f;

        //A washer, not a disc: the bore is the drain's mouth and the funnel fills it exactly. Both circles are
        //drawn at FUNNEL_SEGMENTS facets, so stone and glass meet with no gap for the sky to show through.
        private static readonly float ISLAND_INNER_RADIUS = FUNNEL_TOP_RADIUS;
        internal static readonly float ISLAND_RADIUS = 26f;
        private static readonly float ISLAND_EDGE_HEIGHT = 5f;
        private const int ISLAND_SEGMENTS = FUNNEL_SEGMENTS;
        private static readonly Vector3 ISLAND_COLOR = new(0.58f, 0.56f, 0.54f);

        //How many world units one tile of the marble spans. The Testbed derives the same figure from the size
        //of the ground block the texture was modelled on; here it is what it is — the grain of the stone.
        private static readonly float ISLAND_DETAIL_SPAN = 30f;

        private DiscMesh _islandMesh;
        private InstancedModelRenderer _islandRenderer;
        private Matrix _islandWorld;

        //The dark pit shaft behind the glass drain, in the four solid-terrain scenes only (the Testbed's own,
        //figures included). Those scenes are a flat clearing at the island's foot, so their ground plane would
        //slice straight across the funnel just under its rim; the terrain shaders cut the island's footprint
        //out of it (TerrainHoleRadius), and this near-black cone then backs the ~55 %-opaque glass so the
        //drain reads as a deep well rather than as a glass ring over bright sky haze. It must HUG the funnel —
        //it shares the mouth and descends just outside it — or it hides behind the stone ring and the bright
        //hole shows through the narrow aperture anyway. Visual only; the session's funnel mesh is the floor.
        private static readonly float TERRAIN_HOLE_RADIUS = ISLAND_RADIUS - 2f;  //tucked under the stone edge, no gap
        private static readonly float PIT_BOTTOM_Y = -46f;                       //below the session's kill plane, so balls vanish inside the pit
        private static readonly float PIT_HOLE_RADIUS = 1.2f;                    //nearly closed: a dark receding throat
        private static readonly Vector3 PIT_COLOR = new(0.03f, 0.03f, 0.035f);   //near-black, a touch cool

        private FunnelMesh _pitMesh;
        private InstancedModelRenderer _pitRenderer;
        private Matrix _pitWorld;

        //Lights a scene carries of its own, on top of the sun and the dome-derived ambient — real lights that
        //illuminate, not emissive surfaces that only glow. Two scenes have them: the neon city's ring of
        //magenta and cyan around the island, and the savanna's campfire. Rebuilt per frame (the campfire
        //flickers), so the parameter references are cached rather than looked up by name each time.
        private const int MAX_SCENE_LIGHTS = 8;
        private readonly Vector3[] _sceneLightPos = new Vector3[MAX_SCENE_LIGHTS];
        private readonly Vector3[] _sceneLightColor = new Vector3[MAX_SCENE_LIGHTS];
        private readonly float[] _sceneLightRange = new float[MAX_SCENE_LIGHTS];

        private EffectParameter _sceneLightPositionParam, _sceneLightColorParam, _sceneLightRangeParam, _sceneLightCountParam;

        //What was last pushed. A scene with no lights only has to send the zero once — the arrays are not
        //touched while the count is zero, so re-sending them every frame writes four parameters that cannot
        //have changed. Starts at -1, so the first frame always pushes, whatever the scene turns out to be.
        private int _lastSceneLightCount = -1;

        //What the cluster hangs from: a translucent glass plate over the play field, the Testbed's own ceiling.
        //The MESH AND RENDERER are the host's — the glass is lit with the rest of the scene, and
        //SkyLitRenderers has to reach it — while the kinematic BODY it is drawn from belongs to the session
        //(GameplayScreen), which is the split the issue warned about. Rebuilt per level at the field's
        //footprint (RebuildCeilingRenderer), so before the first level there is none and SkyLitRenderers
        //tolerates the null.
        private static readonly Vector3 CEILING_GLASS_COLOR = new(0.55f, 0.75f, 0.85f);
        private static readonly float CEILING_GLASS_ALPHA = 0.4f;

        private BoxMesh _ceilingMesh;
        private InstancedModelRenderer _ceilingRenderer;

        internal InstancedModelRenderer CeilingRenderer => _ceilingRenderer;

        #endregion

        #region Balls (the renderers; the balls themselves are the session's)

        internal static readonly int BALL_TYPE_COUNT = (int)BallType.Type8;

        private static readonly int[,] BALL_LOD_RESOLUTIONS = { { 32, 24 }, { 16, 12 }, { 10, 7 } };
        internal static readonly float[] BALL_LOD_DISTANCES = { 15f, 30f };
        internal static readonly int BALL_LOD_COUNT = 3;

        private static readonly int BALL_PATTERN_GORES = 5;
        private static readonly float BALL_ALBEDO = 0.5f;
        private static readonly float BALL_EMISSION = 0.5f;
        private static readonly float BALL_TRANSLUCENCY = 0.35f;
        private static readonly float BALL_PULSE_BEATS_PER_SECOND = 1.1f;
        private static readonly float BALL_PULSE_DEPTH = 0.55f;
        private static readonly float BALL_PULSE_WAVELENGTH = 14f;

        private const float BALL_RADIUS = Constants.HALF;

        private SphereMesh[] _ballMeshes;
        private InstancedModelRenderer[] _ballRenderers;

        /// <summary>One renderer per LOD; the session buckets its instances against these.</summary>
        internal InstancedModelRenderer[] BallRenderers => _ballRenderers;

        #endregion

        #region The gun's hardware (the mesh; the gun's pose and magazine are the session's)

        private CannonMesh _cannonMesh;
        private InstancedModelRenderer _cannonRenderer;

        private const float CANNON_BORE_RADIUS = 0.6f;
        private const float CANNON_WALL_THICKNESS = 0.14f;
        private const float CANNON_SLOT_HALF_ANGLE = 0.5f;
        private static readonly Vector3 CANNON_COLOR = new(0.42f, 0.44f, 0.48f);

        internal InstancedModelRenderer CannonRenderer => _cannonRenderer;

        #endregion

        #region Levels

        /// <summary>
        /// The levels and the order they are played in, read from <c>Levels/Levels.json</c> beside the exe.
        /// Null when no set could be read at all, which is the one case the procedural fallback covers.
        /// </summary>
        private LevelSet _levelSet;

        private const string LEVELS_DIRECTORY = "Levels";

        /// <summary>The set the session installs its levels from. Null when none could be read.</summary>
        internal LevelSet LevelSet => _levelSet;

        #endregion

        #region The menu (Myra)

        //The screens, on a stack (Prazsky.Core.Screens). The BackdropScreen sits at the very bottom for the
        //life of the program and draws the setting; the menu pages sit over it (DrawsUnderlying) and the
        //GameplayScreen goes over it while a level is played. What the stack replaced was a GameState enum
        //and a switch that had to work out where the player had come from: "Settings backs out to the pause
        //it was opened from" and "it dims the frame because that pause is a stopped game" were both asked as
        //_state == Paused, from two places. On a stack the pause is simply UNDERNEATH settings, so backing
        //out is a pop and the dimming is a question about the stack — and "is the game paused" is whether the
        //gameplay screen is covered, asked by nobody because the manager's traversal simply stops updating it.
        //
        //The pages are held rather than made per navigation, so Contains<PausePage>() means something and
        //nothing is allocated on a button press.
        private readonly ScreenManager _screens = new();
        private BackdropScreen _backdrop;
        private GameplayScreen _gameplayScreen;
        private SplashPage _splashPage;
        private MainMenuPage _mainMenuPage;
        private PausePage _pausePage;
        private SettingsPage _settingsPage;
        private LevelSelectPage _levelSelectPage;
        private ScenePage _scenePage;
        private AboutPage _aboutPage;
        private ResultPage _resultPage;

        //The Myra host. Rendered as the very last thing in Draw (after the tonemap resolve and base.Draw),
        //straight to the back buffer — the same place the map editor puts it. Render() also processes Myra's
        //own mouse/keyboard input, so it is only called while a menu page is the active screen, where the
        //game's own input stands down.
        private Desktop _desktop;

        //Each page owns its own widgets. What is here is the host's share of the menu: the desktop the top
        //page's tree is put into, the fonts, the layout scale and the palette — all of which belong to the
        //frame rather than to any one page.

        //The current screen's entries in the order they are drawn, and which one a pad or the arrow keys have
        //landed on. Collected by walking the shown tree rather than registered per button: a list that has to
        //be kept in step by hand is one an added entry silently falls out of, and the walk is run only on a
        //screen change. -1 means the focus cursor is not up at all — the pointer is driving, and two entries
        //lit at once (one hovered, one focused) reads as a bug.
        private readonly List<Button> _navEntries = new();
        private int _navIndex = -1;
        private float _navRepeatDelay;
        private int _navDirection;
        private Point _navMouseAt;

        //Inter (SIL OFL), through FontStashSharp. Myra's embedded stylesheet carries a small bitmap font that
        //is fine for a tool panel and much too coarse for a game's title, so the menu brings its own; it is
        //embedded in the assembly, so there is no path to get wrong and nothing to install. Each size is a
        //separate rasterized atlas, so they are resolved once at load rather than per label.
        //
        //Two systems, not two fonts in one: FontStashSharp falls back through a system's fonts glyph by
        //glyph, so a bold added beside the regular would never be reached — the regular has every glyph.
        //Picking a weight means picking a system.
        private FontSystem _menuFontSystem, _menuFontSystemBold;
        private SpriteFontBase _menuFontBody, _menuFontSmall, _menuFontHeading, _menuFontTitle;

        //The menu is deliberately GREYSCALE — no hue anywhere, and no coloured frames. It has to sit over
        //seven backdrops whose palettes are nothing alike (a neon city, an ochre desert, a blue sea, white
        //peaks, green meadow), and any accent colour that reads as the game's own over one of them fights
        //the next. Neutral black-to-white belongs over all of them equally: emphasis is carried by
        //brightness and by opacity, which is legible against any hue.
        //
        //Display-space sRGB throughout: Myra draws to the back buffer, after the frame's one and only exit
        //from linear light.
        internal static readonly Color MENU_TEXT = new(244, 244, 244);        //the active thing on a screen
        internal static readonly Color MENU_TEXT_BODY = new(208, 208, 208);   //prose, a shade under a heading
        internal static readonly Color MENU_TEXT_DIM = new(146, 146, 146);    //asides, always on a dark plate

        //Buttons: a dark slab at rest that the pointer lifts a step up the grey ramp, so the highlight is
        //brightness and not hue. Each of these REPLACES the one before it rather than being laid over it
        //(Myra picks one brush per state), so they all have to be opaque enough to hide the scene behind
        //them — a translucent hover would show the backdrop through the entry the pointer is on, which is
        //exactly the one that has to read clearly. They stay dark, because the label on top of them is white.
        private static readonly Color MENU_BUTTON = new(11, 11, 11, 212);
        private static readonly Color MENU_BUTTON_OVER = new(72, 72, 72, 232);
        private static readonly Color MENU_BUTTON_PRESSED = new(120, 120, 120, 240);

        //Built once and shared by every entry. A brush holds no per-widget state, and the focus highlight
        //swaps an entry between the first two of these rather than minting a brush per frame.
        private static readonly IBrush MENU_BUTTON_BRUSH = new SolidBrush(MENU_BUTTON);
        private static readonly IBrush MENU_BUTTON_OVER_BRUSH = new SolidBrush(MENU_BUTTON_OVER);
        private static readonly IBrush MENU_BUTTON_PRESSED_BRUSH = new SolidBrush(MENU_BUTTON_PRESSED);

        //A pause dims the whole frame, because what is behind it is a stopped game and the menu is the thing
        //to look at. The front end does NOT: there the rotating scene is the point of the screen, and a
        //full-screen wash over it throws away the one thing that screen exists to show. Its legibility comes
        //from the widgets instead — the entries are near-opaque slabs and the prose sits on a plate.
        private static readonly Color PAUSE_SCRIM = new(0, 0, 0, 176);

        //Behind prose, where a slab alone cannot hold a line of small text steady over a moving scene
        internal static readonly Color MENU_PLATE = new(0, 0, 0, 190);

        //Held rather than made per navigation: the shared screens swap between this and no scrim at all,
        //depending on whether they were opened from the front end or from a pause.
        private readonly SolidBrush _pauseScrimBrush = new(PAUSE_SCRIM);

        /// <summary>
        /// The height the menu is laid out for, in the same spirit as <see cref="InfoRenderer"/>'s overlay:
        /// every size in the menu is a 2160p figure put through <see cref="Scaled"/> at build time. Myra
        /// measures in pixels, so without this the menu would keep its pixel size and shrink to a postage
        /// stamp on a 4K screen — against the game's own resolution policy, and against the FPS line beside
        /// it, which is authored exactly this way.
        /// <para>
        /// It is done by re-resolving the sizes rather than by <c>Desktop.Scale</c>: scaling the desktop also
        /// scales the full-screen scrim, which then stops covering the frame.
        /// </para>
        /// </summary>
        private const int MENU_DESIGN_HEIGHT = 2160;

        /// <summary>
        /// How much the viewport has to change before the widget tree is rebuilt at the new size, in pixels
        /// of height. A live window drag reports a new size every frame, and each rebuild asks the font
        /// system for glyphs at another size; quantizing bounds a drag across the whole screen to a couple of
        /// dozen rebuilds instead of hundreds, and the sizes are never more than this far out meanwhile.
        /// </summary>
        private const int MENU_REBUILD_QUANTUM = 32;

        private float _menuScale = 1f;
        private int _menuBuiltForHeight = -1;

        /// <summary>
        /// The game's name, as the player sees it — on the menu and in the window's title bar. <c>BS3D</c>
        /// stays the shorthand the repository, the assembly and this file's namespace are named for; it is
        /// not what the game is called.
        /// </summary>
        internal const string GAME_TITLE = "Bubble Shooter 3D";

        #region Adaptive quality

        /// <summary>
        /// Which bundle of detail the frame is being drawn at, and the setting the player sees. It starts at
        /// <see cref="QualityLevel.High"/> — the look the game is authored at — and only ever comes down, either
        /// because the player asked or because <see cref="TuneQualityToFrameRate"/> measured this machine.
        /// </summary>
        private QualityLevel _quality = QualityLevel.High;

        internal QualityLevel Quality => _quality;

        /// <summary>
        /// True once the quality tier is not to be touched again: the player named one on the command line, the
        /// player set one in Settings, the machine proved fast enough, or there is nothing left to lower. It is a
        /// one-way latch on purpose — a dial that keeps moving under the player is worse than one that is merely
        /// wrong once.
        /// </summary>
        private bool _qualitySettled;

        private float _qualityWarmupLeft = QUALITY_WARMUP_SECONDS;
        private float _qualityWindowSeconds;
        private int _qualityWindowFrames;

        /// <summary>
        /// Ignored before this much of the run has passed. The opening frames are shader compiles, the first
        /// touch of every render target and the window settling, and none of them are what this machine costs
        /// to draw. Counted in <b>seconds</b> rather than frames deliberately: a fixed frame count is itself a
        /// function of the frame rate, so on the slow hardware this exists for it would wait the longest.
        /// </summary>
        private const float QUALITY_WARMUP_SECONDS = 1.5f;

        /// <summary>How long a verdict is averaged over. Long enough that a single hitched frame cannot cause one.</summary>
        private const float QUALITY_WINDOW_SECONDS = 1.5f;

        /// <summary>
        /// Below this, the frame rate is judged bad enough to be worth spending image quality on. Comfortably
        /// under any display's refresh, so a vsync-capped machine (the normal case) never trips it — 60 Hz
        /// reads as 60, not as "only just enough".
        /// </summary>
        private const float QUALITY_MIN_FPS = 45f;

        #endregion

        private const int MENU_BUTTON_WIDTH = 1000;
        private const int MENU_COLUMN_SPACING = 26;

        //Held direction repeats: one step, a pause long enough that a deliberate single press stays single,
        //then a steady walk. Both are wall-clock seconds and not frame counts, or the list would run faster on
        //a faster machine — the same rule the rattle's phase and the menu's orbit follow.
        private const float NAV_REPEAT_DELAY = 0.42f;
        private const float NAV_REPEAT_INTERVAL = 0.11f;

        //Well past a resting stick's drift, and far enough in that a diagonal push does not step the list
        private const float NAV_STICK_DEADZONE = 0.55f;

        //A pointer has to move by more than its own jitter before it takes the focus back off the pad
        private const int NAV_MOUSE_WAKE_PIXELS = 6;

        //Entries are read at a glance and from across a room, so the type is set large. The title is sized to
        //the whole of GAME_TITLE on one line at the narrowest targeted aspect (16:9, i.e. 3840 design units
        //wide) with room to spare — a short acronym would take far more, but the name is what is set.
        private const int MENU_FONT_SMALL = 58;
        private const int MENU_FONT_BODY = 80;
        private const int MENU_FONT_HEADING = 124;
        private const int MENU_FONT_TITLE = 170;

        //The exposure ladder the settings button walks. Centred on DEFAULT_EXPOSURE, wide enough either way
        //to matter on a dim laptop panel and on a bright monitor without ever crushing or blowing the frame.
        private const float EXPOSURE_MIN = 0.7f;
        private const float EXPOSURE_MAX = 1.5f;
        private const float EXPOSURE_STEP = 0.2f;

        //In the declared order of SceneKind (City, Sea, Savanna, Desert, Mountain, Meadow, NeonCity), so the
        //scene list can be indexed by the enum's own value
        internal static readonly string[] SCENE_NAMES =
            { "City", "Sea", "Savanna", "Desert", "Mountains", "Meadow", "Neon City" };

        #endregion

        #region The in-play HUD's fonts

        //The HUD is the GameplayScreen's, but its type is the frame's — the same Inter the menu is set in,
        //resolved per viewport height exactly as the menu's own sizes are. Separate from EnsureMenuLayout,
        //which only runs while a menu is up, and quantized the same way so a window being dragged does not
        //ask the font system for a new atlas every frame.
        private const int HUD_FONT_SCORE = 110;
        private const int HUD_FONT_LABEL = 62;
        private const int HUD_FONT_POPUP = 64;

        private SpriteFontBase _hudFontScore, _hudFontLabel, _hudFontPopup;
        private SpriteFontBase _hudFontScoreBold, _hudFontLabelBold;
        private int _hudFontsForHeight = -1;

        internal SpriteFontBase HudFontScore => _hudFontScore;
        internal SpriteFontBase HudFontLabel => _hudFontLabel;
        internal SpriteFontBase HudFontPopup => _hudFontPopup;
        internal SpriteFontBase HudFontScoreBold => _hudFontScoreBold;

        /// <summary>
        /// Resolves the HUD's fonts for the viewport they are about to be drawn into. Called by the gameplay
        /// screen at the top of its HUD pass.
        /// </summary>
        internal void EnsureHudFonts()
        {
            int quantized = GraphicsDevice.Viewport.Height / MENU_REBUILD_QUANTUM;
            if (quantized == _hudFontsForHeight) return;

            _hudFontsForHeight = quantized;
            _menuScale = GraphicsDevice.Viewport.Height / (float)MENU_DESIGN_HEIGHT;

            _hudFontScore = _menuFontSystem.GetFont(Scaled(HUD_FONT_SCORE));
            _hudFontLabel = _menuFontSystem.GetFont(Scaled(HUD_FONT_LABEL));
            _hudFontPopup = _menuFontSystem.GetFont(Scaled(HUD_FONT_POPUP));
            _hudFontScoreBold = _menuFontSystemBold.GetFont(Scaled(HUD_FONT_SCORE));
            _hudFontLabelBold = _menuFontSystemBold.GetFont(Scaled(HUD_FONT_LABEL));
        }

        #endregion

        #region Shared input state (menu ⇄ play handover)

        //One set of previous-frame snapshots for BOTH the menu chrome and the gameplay screen, and that is
        //load-bearing: the Escape that paused the game is still down on the first menu frame and must not be
        //seen as a second press by the menu, and the Escape that resumed it must likewise not be seen again
        //by the play loop. Two separate snapshots would each see the other's press as fresh — pause and
        //instant resume, forever.
        private KeyboardState _previousKeyboard;
        private GamePadState _previousPad;

        internal KeyboardState PreviousKeyboard
        {
            get => _previousKeyboard;
            set => _previousKeyboard = value;
        }

        internal GamePadState PreviousPad
        {
            get => _previousPad;
            set => _previousPad = value;
        }

        /// <summary>A key pressed this frame that was not pressed last frame.</summary>
        internal bool IsKeyEdge(KeyboardState keyboard, Keys key) =>
            keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

        #endregion

        /// <param name="supersampleFactor">
        /// <c>null</c> when the player did not say — which is what lets <see cref="TuneQualityToFrameRate"/>
        /// lower it on hardware that cannot afford the default. An explicit <c>ssaa=</c> is never overridden.
        /// </param>
        /// <param name="scene">
        /// The backdrop to start in, or <c>null</c> for the usual random one of the seven. Pinning it is what
        /// makes a frame-cost measurement repeatable — see <see cref="LogFrameRate"/>.
        /// </param>
        /// <param name="skyDome">The dome to start under, or <c>null</c> to let the scene choose as it normally does.</param>
        /// <param name="logFrameRate">Write one frame-rate line a second to stdout (the <c>logfps</c> argument).</param>
        /// <param name="quality">
        /// The tier to start at, or <c>null</c> to start at <see cref="QualityLevel.High"/> — the look the game is
        /// authored at — and let <see cref="TuneQualityToFrameRate"/> measure this machine.
        /// </param>
        public BS3DGame(bool fullscreen = false, int? supersampleFactor = null, float exposure = DEFAULT_EXPOSURE,
            bool uncappedFps = false, SceneKind? scene = null, byte? skyDome = null, bool logFrameRate = false,
            QualityLevel? quality = null)
        {
            _fullscreen = fullscreen;

            //The tier owns supersampling, so the tier's factor is taken first and an explicit ssaa= then
            //overrides that one entry of it — the expert override the benchmark and the screenshot harness use.
            //The rest of the tier is applied in LoadContent, once the city it also sizes exists.
            if (quality.HasValue) _quality = quality.Value;
            _supersampleFactor = QualityPreset.Presets[(int)_quality].SupersampleFactor;

            if (supersampleFactor.HasValue) _supersampleFactor = Math.Clamp(supersampleFactor.Value, 1, 4);

            _startupScene = scene;
            _startupSkyDome = skyDome;
            _logFrameRate = logFrameRate;

            //Either one is the player's decision and settles the question; with neither, the adaptive path is
            //free to measure this machine and step the tier down
            _qualitySettled = supersampleFactor.HasValue || quality.HasValue;
            _exposure = exposure > 0f ? exposure : DEFAULT_EXPOSURE;
            _uncappedFps = uncappedFps;

            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreparingDeviceSettings += Graphics_PreparingDeviceSettings;

            Content.RootDirectory = "Content";

            Window.AllowUserResizing = true;
            Window.Title = GAME_TITLE;
            Window.ClientSizeChanged += (_, _) => OnClientSizeChanged();

            SetGraphics();
        }

        private void Graphics_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            e.GraphicsDeviceInformation.PresentationParameters.PresentationInterval = _uncappedFps ? PresentInterval.Immediate : PresentInterval.One;
            e.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.HiDef;

            //Nothing but one already-resolved full-screen quad ever reaches the back buffer, so multisampling
            //it would cost memory and antialias nothing.
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 0;
        }

        private void SetGraphics()
        {
            //The adapter's mode, not the device's: this is called from the constructor as well, before the
            //GraphicsDeviceManager has made a device at all — so reading GraphicsDevice there fell through to
            //the windowed default and `BS3D.exe fullscreen` mode-switched the display down to 1600×900
            //instead of filling it. GraphicsAdapter is valid with no device.
            DisplayMode display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;

            _graphics.PreferredBackBufferWidth = _fullscreen ? display.Width : WINDOW_WIDTH;
            _graphics.PreferredBackBufferHeight = _fullscreen ? display.Height : WINDOW_HEIGHT;
            _graphics.IsFullScreen = _fullscreen;
            _graphics.SynchronizeWithVerticalRetrace = !_uncappedFps;

            _graphics.ApplyChanges();

            EnsureSceneTarget();

            //A fullscreen switch resizes the back buffer without necessarily going through the window's own
            //resize event, and both the overlay's scale and the camera's framing are derived from the viewport
            _info?.RecomputeScale();
            UpdateCameraAspect();

            //The cursor is captured for aiming only while a game is actually being played; everywhere else it
            //is the pointer the player clicks with. This runs from the constructor too, before any screen
            //exists, where an empty stack correctly reads as "not playing".
            IsMouseVisible = _screens.Active is not GameplayScreen;
            IsFixedTimeStep = false;
        }

        private void OnClientSizeChanged()
        {
            UpdateCameraAspect();

            //The overlay is authored for 2160p and scaled to the viewport, so a resize has to re-derive it.
            //The menu is authored the same way, and refits itself in Draw (EnsureMenuLayout).
            _info?.RecomputeScale();

            EnsureSceneTarget();
        }

        /// <summary>
        /// Re-derives the camera's aspect and hands the change to the session, which owns the framing: the
        /// fit is checked on <b>both</b> frustum axes, and only the vertical one is aspect-independent — a
        /// narrow window or a tall one flips which binds — so a resize has to re-solve the stand-off and not
        /// just the projection. The session also drops its mouse-aim baseline there, since the viewport's
        /// centre just moved (see <see cref="GameplayScreen.OnViewportChanged"/>).
        /// </summary>
        private void UpdateCameraAspect()
        {
            if (_camera == null) return;

            _camera.AspectRatio = GraphicsDevice.Viewport.AspectRatio;

            _gameplayScreen?.OnViewportChanged();
        }

        protected override void Initialize()
        {
            _camera = new RecoilCamera
            {
                AspectRatio = GraphicsDevice.Viewport.AspectRatio,
                FieldOfView = GameplayScreen.GAME_FOV
            };

            //Every dial of the kick scaled by the one factor: the shape of the response is kept — the
            //directional throw, the rattle, the roll that is what reads as "the camera was hit" — and only
            //its ceiling comes down, now that the gun itself takes most of the shot's force.
            CameraShake shake = _camera.Shake;
            shake.MaxPitch *= CAMERA_SHAKE_SCALE;
            shake.MaxYaw *= CAMERA_SHAKE_SCALE;
            shake.MaxRoll *= CAMERA_SHAKE_SCALE;
            shake.MaxOffset *= CAMERA_SHAKE_SCALE;
            shake.MaxRecoilBack *= CAMERA_SHAKE_SCALE;
            shake.MaxRecoilPitch *= CAMERA_SHAKE_SCALE;
            shake.MaxFovPunch *= CAMERA_SHAKE_SCALE;

            //Added before base.Initialize, which is what initializes the component and loads its font
            _info = new InfoRenderer(this, "Content/Fonts/segoeui") { DrawOrder = int.MaxValue };
            Components.Add(_info);

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _instancingEffect = Content.Load<Effect>("Shaders/InstancedModel");
            _tonemapEffect = Content.Load<Effect>("Shaders/Tonemap");
            _glareEffect = Content.Load<Effect>("Shaders/Glare");

            _glareBrightPassTechnique = _glareEffect.Techniques["BrightPass"];
            _glareStreakTechnique = _glareEffect.Techniques["Streak"];
            _glareSourceTextureParam = _glareEffect.Parameters["SourceTexture"];
            _glareSourceTexelSizeParam = _glareEffect.Parameters["SourceTexelSize"];
            _tonemapGlareTextureParam = _tonemapEffect.Parameters["GlareTexture"];
            _tonemapSceneTextureParam = _tonemapEffect.Parameters["SceneTexture"];
            _tonemapSourceTexelSizeParam = _tonemapEffect.Parameters["SourceTexelSize"];

            //Re-sent every frame (the savanna's campfire flickers), so the by-name scans are done once here
            _sceneLightPositionParam = _instancingEffect.Parameters["SceneLightPosition"];
            _sceneLightColorParam = _instancingEffect.Parameters["SceneLightColor"];
            _sceneLightRangeParam = _instancingEffect.Parameters["SceneLightRange"];
            _sceneLightCountParam = _instancingEffect.Parameters["SceneLightCount"];

            //Fixed for the whole run, so they are set exactly once: a parameter's value persists on the
            //effect, and re-sending a constant every frame bought nothing
            _glareEffect.Parameters["GlareThreshold"].SetValue(GLARE_THRESHOLD);
            _glareEffect.Parameters["StreakLength"].SetValue(GLARE_STREAK_LENGTH);
            _glareEffect.Parameters["StreakFalloff"].SetValue(GLARE_STREAK_FALLOFF);
            _tonemapEffect.Parameters["GlareIntensity"].SetValue(GLARE_INTENSITY);
            _tonemapEffect.Parameters["SupersampleFactor"].SetValue(_supersampleFactor);
            _tonemapEffect.Parameters["Exposure"].SetValue(_exposure);

            //There is no water in this scene to get under, so the underwater murk is a no-op
            _tonemapEffect.Parameters["UnderwaterAmount"].SetValue(0f);

            CreateFullScreenQuad();

            //The overlay's own batch and its one white texel, stretched into each bar of the crosshair
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            #region Balls

            _ballMeshes = new SphereMesh[BALL_LOD_COUNT];
            _ballRenderers = new InstancedModelRenderer[BALL_LOD_COUNT];

            for (int lod = 0; lod < BALL_LOD_COUNT; lod++)
            {
                _ballMeshes[lod] = new SphereMesh(GraphicsDevice, BALL_RADIUS, BALL_LOD_RESOLUTIONS[lod, 0], BALL_LOD_RESOLUTIONS[lod, 1]);
                _ballRenderers[lod] = new InstancedModelRenderer(GraphicsDevice, _ballMeshes[lod], BALL_ALBEDO * Vector3.One, _instancingEffect)
                {
                    PatternGoreCount = BALL_PATTERN_GORES,
                    EmissiveStrength = BALL_EMISSION,
                    TranslucencyStrength = BALL_TRANSLUCENCY,
                    PulseSpeed = BALL_PULSE_BEATS_PER_SECOND,
                    PulseDepth = BALL_PULSE_DEPTH,
                    PulseDirection = Vector3.Up,
                    PulseWavelength = BALL_PULSE_WAVELENGTH,
                    GroundHeight = ISLAND_Y
                };
            }

            #endregion

            #region The gun

            //The barrel is modelled about its midpoint, so the world matrix's translation is the pivot: the
            //queue of loaded balls recedes from a muzzle lip CANNON_PIVOT_TO_FRONT_BALL ahead of it. The
            //magazine the bore is sized to is the session's (GameplayScreen), which is why its figures live
            //there.
            float muzzleZ = -(GameplayScreen.CANNON_PIVOT_TO_FRONT_BALL + Constants.HALF);
            float breechZ = (GameplayScreen.MAGAZINE_SIZE - 1) * GameplayScreen.MAGAZINE_SPACING - GameplayScreen.CANNON_PIVOT_TO_FRONT_BALL + Constants.HALF;

            _cannonMesh = new CannonMesh(GraphicsDevice, CANNON_BORE_RADIUS, CANNON_WALL_THICKNESS, muzzleZ, breechZ, CANNON_SLOT_HALF_ANGLE, 24);
            _cannonRenderer = new InstancedModelRenderer(GraphicsDevice, _cannonMesh, CANNON_COLOR, _instancingEffect)
            {
                SpecularAmbientStrength = 0.5f,
                GroundHeight = ISLAND_Y
            };

            #endregion

            //The five self-lit backdrops, shared with the Testbed and the map editor — one copy of every
            //scene shader, built out of the Testbed's content directory. The hole radius is fixed (the island
            //never moves or resizes here), so it is set once rather than per frame.
            _sceneRenderer = new SceneRenderer(GraphicsDevice, Content) { TerrainHoleRadius = TERRAIN_HOLE_RADIUS };

            BuildScene();

            //Note the simulation, the ceiling body and the cluster are NOT built here: they are the expensive
            //part of starting up and belong to a play session, so the GameplayScreen builds them on the first
            //"Play" and rebuilds them per level. Everything above is the scene, which the menu also stands in.

            _skyEffect = Content.Load<Effect>("Shaders/Sky");
            _skyCameraPositionParam = _skyEffect.Parameters["CameraPosition"];
            _sky = new SkyDome(Content.Load<Model>("Skyes/SkyDome" + _skyDome), GraphicsDevice, linearVertexColors: true)
            {
                Effect = _skyEffect
            };

            SetCloudParameters();

            //A different one of the seven every launch, so the front end is not the same picture twice — unless
            //the command line pinned one. It also sets the dome and the city's lighting, and ends in
            //ApplySkyLighting, which is why nothing derives the light rig before this point.
            SetScene(_startupScene ?? (SceneKind)RANDOM.Next(SCENE_COUNT));

            //After SetScene and never before: the sea and the savanna each replace the dome with one of their
            //own, so an explicit sky= would be silently overridden the other way round. The Testbed's rule.
            if (_startupSkyDome.HasValue) SetSkyDome(_startupSkyDome.Value);

            //The rest of the tier, now that the city it also sizes exists. The constructor could only take the
            //supersample factor, since the target and the city are both built here. At the default High this
            //writes the config's own defaults back over themselves and rebuilds nothing.
            ApplyQuality(_quality);

            EnsureSceneTarget();

            //The order the levels are played in, read once here: a broken set is reported at startup rather
            //than at the moment the player presses Play, and the maps themselves are only parsed per level.
            LoadLevelSet();

            Console.WriteLine($"[game] {_city.Buildings.Length} buildings, scene {_scene}, dome {_skyDome}");

            //The two non-page screens. The gameplay screen loads its own content (the shot-trail effect), so
            //it is made here with the device up; the backdrop is the scene-only frame the menus stand over.
            _backdrop = new BackdropScreen(this);
            _gameplayScreen = new GameplayScreen(this);

            BuildMenu();
        }

        private static readonly Random RANDOM = new();

        #region The menu's screens

        /// <summary>
        /// Boots Myra and builds the pages. Called once at the end of <see cref="LoadContent"/>: the menu is
        /// drawn over the live scene, so everything it stands on has to exist first. Each page is built once
        /// here; its tree is swapped into <see cref="_desktop"/>.Root as the player navigates, which is what
        /// keeps the menu out of the frame loop's allocation path.
        /// </summary>
        private void BuildMenu()
        {
            MyraEnvironment.Game = this;

            //Inter (SIL OFL 1.1), embedded in the assembly so there is no path to get wrong and nothing to
            //install. Myra's own stylesheet carries a small bitmap font, which is fine for a tool panel and
            //far too coarse for a title. Each size is rasterized into its own atlas by GetFont, so they are
            //resolved once here rather than per label.
            _menuFontSystem = LoadEmbeddedFont("BS3D.Content.Fonts.Inter-Regular.ttf");
            _menuFontSystemBold = LoadEmbeddedFont("BS3D.Content.Fonts.Inter-Bold.ttf");

            _desktop = new Desktop();

            //Held for the life of the game rather than made per navigation: nothing is allocated on a button
            //press, and Contains<PausePage>() — which is how a shared page knows it is over a stopped game —
            //means something only if there is one pause page rather than a new one each time.
            _splashPage = new SplashPage(this);
            _mainMenuPage = new MainMenuPage(this);
            _pausePage = new PausePage(this);
            _settingsPage = new SettingsPage(this);
            _levelSelectPage = new LevelSelectPage(this);
            _scenePage = new ScenePage(this);
            _aboutPage = new AboutPage(this);
            _resultPage = new ResultPage(this);

            EnsureMenuLayout();

            //The backdrop is the bottom of the stack for the life of the program — the world every menu page
            //shows behind itself — and the game opens on the title card over it, which replaces itself with
            //the front end once its beat is up. Pushed after the trees exist, since a page puts its own tree
            //into the desktop the moment the stack makes it active.
            _screens.Push(_backdrop);
            _screens.Push(_splashPage);
        }

        /// <summary>
        /// Builds the widget tree at the viewport's current size, and rebuilds it when that size has moved
        /// far enough to matter. Called from <see cref="Draw"/> right before the menu is rendered rather than
        /// hooked onto the resize event, so there is one place it can be out of date and it is the frame that
        /// is about to draw it — a fullscreen switch, a window drag and the first frame all come through here.
        /// </summary>
        private void EnsureMenuLayout()
        {
            int height = GraphicsDevice.Viewport.Height;
            int quantized = height / MENU_REBUILD_QUANTUM;

            if (quantized == _menuBuiltForHeight) return;

            _menuBuiltForHeight = quantized;
            _menuScale = height / (float)MENU_DESIGN_HEIGHT;

            //Each size is its own rasterized atlas, so they are asked for once per rebuild and not per label.
            //Quantizing the rebuild is what keeps a drag from asking for a hundred slightly different sizes.
            _menuFontSmall = _menuFontSystem.GetFont(Scaled(MENU_FONT_SMALL));
            _menuFontBody = _menuFontSystem.GetFont(Scaled(MENU_FONT_BODY));
            _menuFontHeading = _menuFontSystemBold.GetFont(Scaled(MENU_FONT_HEADING));
            _menuFontTitle = _menuFontSystemBold.GetFont(Scaled(MENU_FONT_TITLE));

            //The trees themselves are NOT rebuilt here: each page rebuilds its own the next time it is asked
            //for one, against this generation (MenuLayoutGeneration). A page the player never opens never
            //builds a tree at all, where this used to rebuild all eight on every step of a window drag.
            //
            //Re-asserts the page the player was on, which is what pulls its tree through at the new size, and
            //with it everything ShowPage keeps in step. Not a menu page while a level is being played, when
            //this is not called at all.
            if (_screens.Active is MenuPage page) ShowPage(page);
        }

        /// <summary>
        /// Bumped whenever the menu is laid out at a new size. A page holds the generation its tree was built
        /// for and rebuilds when they differ — which is how a rebuild reaches a page that is not on screen at
        /// the moment the window is resized.
        /// </summary>
        internal int MenuLayoutGeneration => _menuBuiltForHeight;

        /// <summary>A 2160p design figure at the viewport's actual size. Never below one pixel.</summary>
        internal int Scaled(int designUnits) => Math.Max(1, (int)MathF.Round(designUnits * _menuScale));

        internal Thickness ScaledThickness(int horizontal, int vertical) =>
            new(Scaled(horizontal), Scaled(vertical));

        internal Thickness ScaledThickness(int left, int top, int right, int bottom) =>
            new(Scaled(left), Scaled(top), Scaled(right), Scaled(bottom));

        /// <summary>Reads one TTF out of the assembly's own resources into a <see cref="FontSystem"/>.</summary>
        private static FontSystem LoadEmbeddedFont(string resourceName)
        {
            FontSystem system = new();

            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            using MemoryStream buffer = new();

            stream.CopyTo(buffer);
            system.AddFont(buffer.ToArray());

            return system;
        }

        /// <summary>The page the splash hands over to when its beat is up.</summary>
        internal MainMenuPage MainMenuPage => _mainMenuPage;

        //The fonts and the palette are the frame's, not any one page's — every page is set in the same type
        internal SpriteFontBase MenuFontBody => _menuFontBody;
        internal SpriteFontBase MenuFontSmall => _menuFontSmall;
        internal SpriteFontBase MenuFontHeading => _menuFontHeading;
        internal SpriteFontBase MenuFontTitle => _menuFontTitle;

        //What the pages ask the game about itself. Read-only: a page shows state and asks for an action, and
        //nothing here lets it write one directly.
        internal bool HasSession => _gameplayScreen != null && _gameplayScreen.IsBuilt;
        internal bool IsFullscreen => _fullscreen;
        internal int SupersampleFactor => _supersampleFactor;
        internal float Exposure => _exposure;
        internal byte SkyDomeNumber => _skyDome;
        internal bool IsFpsOverlayVisible => _info.Visible;
        internal SceneKind Scene => _scene;

        internal int LevelCount => _levelSet?.Count ?? 0;
        internal string LevelDisplayName(int index) => _levelSet.DisplayName(index);
        internal string LevelRulesText(int index) => _levelSet.DescribeRules(index);

        //The actions a page's entries invoke. Named for what the player asked for rather than for how it is
        //done, so a page reads as a list of choices.
        internal void ContinueGame() => StartGame(newGame: false);
        internal void OpenLevelSelect() => OpenPage(_levelSelectPage);
        internal void OpenSceneSelect() => OpenPage(_scenePage);
        internal void OpenSettings() => OpenPage(_settingsPage);
        internal void OpenAbout() => OpenPage(_aboutPage);

        /// <summary>
        /// What the result screen's "Main Menu" does. The session is torn down, not kept: a level that has
        /// ended is not one to "Continue" into, and the front end should offer "Play" rather than "Continue"
        /// into a level that is already finished.
        /// </summary>
        internal void EndSessionAndReturnToMainMenu()
        {
            _gameplayScreen.TearDown();
            ReturnToMainMenu();
        }

        /// <summary>
        /// Puts a page's tree into the shared Myra desktop and brings it up to date. Called by the page itself
        /// when the stack makes it the active one — which covers a push, a pop, a replace and a reset alike,
        /// so there is one path here rather than a branch per way of arriving.
        /// </summary>
        internal void ShowPage(MenuPage page)
        {
            //Everything that has to agree with the game's state — the resume entry, the setting values, the
            //marked scene, the score just earned — is re-read here rather than per frame, because a page can
            //only change while nobody is looking at it.
            page.Refresh();

            //Whether the frame behind is dimmed belongs to where the page was opened from, and the stack is
            //what knows: light over a scene that is the point of the picture, heavy over a frozen game that
            //is not. A null background simply draws nothing.
            page.Root.Background = page.DimsFrame ? _pauseScrimBrush : null;

            _desktop.Root = page.Root;

            //Last: Refresh above has finished deciding which entries this page actually shows — the resume
            //entry, Next Level — and the walk reads what is up rather than what was built.
            CollectNavEntries();
        }

        /// <summary>Opens a page over whatever is up. Backing out of it is a pop; nothing has to remember where it came from.</summary>
        private void OpenPage(MenuPage page) => _screens.Push(page);

        /// <summary>
        /// One level back, wherever Escape or the pad's B is pressed: out of a sub-screen to the screen that
        /// opened it, out of the pause menu into the game.
        /// <para>
        /// The main menu has no back — quitting is a menu item, and a front end that closes when a key is
        /// tapped is one that closes by accident. The result screen has none either, and for a different
        /// reason: the level has already ended, so there is nothing to resume into and "back one level" has no
        /// meaning; Retry, Next Level and Main Menu are the only ways off it.
        /// </para>
        /// </summary>
        private void MenuBack()
        {
            if (_screens.Active is not MenuPage page || !page.CanGoBack) return;

            //The pause's own back is not a plain pop but a resume: the game underneath has to start running
            //again, and ResumeGame is the one door back into it. Everything else is one level off the stack,
            //whatever opened it.
            if (page is PausePage) ResumeGame();
            else _screens.Pop();
        }

        #region Menu navigation (pad and arrow keys)

        /// <summary>
        /// Re-reads the entries of the screen that is up, in the order they are drawn. Walking the shown tree
        /// is deliberate: a list registered per button is one that an entry added later silently falls out of,
        /// and a screen whose entries come and go — the resume entry, Next Level — would need the registration
        /// repeating anyway. It runs on a screen change and on a layout rebuild, never per frame.
        /// </summary>
        private void CollectNavEntries()
        {
            //The INDEX does not survive a screen change — entry 3 of the settings screen is not entry 3 of the
            //one it returns to — but whether the cursor was up does. A pad player who opens Settings and backs
            //out again would otherwise land on a screen with no cursor and have to press a direction to get one
            //back, every time; and putting it up unconditionally would light an entry for a player who is
            //using the pointer and never asked for one.
            bool cursorWasUp = _navIndex >= 0;

            _navEntries.Clear();
            _navIndex = -1;
            _navDirection = 0;

            CollectNavEntries(_desktop.Root);

            if (cursorWasUp && _navEntries.Count > 0) _navIndex = 0;

            //Re-baseline the pointer, or the very next frame reads a move the PLAYER did not make and takes
            //the cursor straight back down: the game recentres the mouse every frame while it is being played,
            //so a pause always arrives with the pointer somewhere other than where the menu last saw it. One
            //poll on a screen change, which is not a per-frame path.
            MouseState mouse = Mouse.GetState();
            _navMouseAt = new Point(mouse.X, mouse.Y);

            ApplyNavHighlight();
        }

        private void CollectNavEntries(Widget widget)
        {
            //An entry that is not shown is not an entry — this is what keeps the focus off the resume entry
            //before there is a session, and off Next Level when the level was not passed
            if (widget == null || !widget.Visible) return;

            if (widget is Button button)
            {
                if (button.Enabled) _navEntries.Add(button);

                //A button's content is its label, never another entry
                return;
            }

            //Myra keeps a container's real child list internal, so the walk goes through the public
            //multiple-items interface — which is every container this menu is built from (Panel,
            //VerticalStackPanel, Grid). A Label or an Image simply has no children and ends the branch.
            if (widget is IContainer container)
                foreach (Widget child in container.Widgets) CollectNavEntries(child);
        }

        /// <summary>
        /// Lights the focused entry with the pointer's own hover brush, so a pad-driven menu reads exactly as a
        /// moused one rather than growing a second visual language. Myra will not do this on its own — its
        /// <c>OverBackground</c> follows the pointer and nothing else — so the focused entry's resting
        /// background is swapped instead, which the hover brush then still overrides for the pointer.
        /// </summary>
        private void ApplyNavHighlight()
        {
            //Exactly one device drives at a time. While the cursor is up the pointer's own hover is switched
            //OFF, because the pointer does not have to move to sit over an entry — a pause recentres it, and
            //it lands wherever the column happens to be, lighting a second entry beside the focused one. That
            //reads as a bug, not as two input devices. Moving the pointer puts the cursor down again (see
            //UpdateMenuNavigation), which restores the hover in the same pass.
            bool cursorUp = _navIndex >= 0;

            for (int i = 0; i < _navEntries.Count; i++)
            {
                IBrush rest = i == _navIndex ? MENU_BUTTON_OVER_BRUSH : MENU_BUTTON_BRUSH;

                _navEntries[i].Background = rest;
                _navEntries[i].OverBackground = cursorUp ? rest : MENU_BUTTON_OVER_BRUSH;
            }
        }

        /// <summary>
        /// The menu's directional input: the D-pad, the left stick and the arrow keys move the focus, A or
        /// Enter presses the focused entry, B backs out exactly as Escape does.
        /// <para>
        /// This is what makes the game shippable on a pad at all — it is otherwise fully playable on one (aim
        /// on the right stick, fire on the right trigger, precise aim on the left), and <c>Buttons.Back</c>
        /// already opens the pause menu, so without this the pad could open a menu it could not then use.
        /// </para>
        /// <para>
        /// Called only while the window is focused, since a pad reports through XInput whether it is or not —
        /// the same gate the precise-aim trigger has.
        /// </para>
        /// </summary>
        private void UpdateMenuNavigation(float elapsed, KeyboardState keyboard, GamePadState pad, bool edgeInputAllowed)
        {
            if (_navEntries.Count == 0) return;

            //Moving the pointer puts the focus cursor away again: the hover and the cursor use the same
            //highlight, and two entries lit at once reads as a bug rather than as two input devices
            MouseState mouse = Mouse.GetState();

            if (Math.Abs(mouse.X - _navMouseAt.X) > NAV_MOUSE_WAKE_PIXELS
                || Math.Abs(mouse.Y - _navMouseAt.Y) > NAV_MOUSE_WAKE_PIXELS)
            {
                _navMouseAt = new Point(mouse.X, mouse.Y);

                if (_navIndex >= 0)
                {
                    _navIndex = -1;
                    ApplyNavHighlight();
                }
            }

            int direction = 0;

            if (keyboard.IsKeyDown(Keys.Down) || pad.IsButtonDown(Buttons.DPadDown)
                || pad.ThumbSticks.Left.Y < -NAV_STICK_DEADZONE) direction = 1;
            else if (keyboard.IsKeyDown(Keys.Up) || pad.IsButtonDown(Buttons.DPadUp)
                || pad.ThumbSticks.Left.Y > NAV_STICK_DEADZONE) direction = -1;

            //A held direction steps once, waits, then walks — a stick read per frame would cross the whole
            //list before the player let go
            if (direction == 0)
            {
                _navDirection = 0;
            }
            else if (direction != _navDirection)
            {
                _navDirection = direction;
                _navRepeatDelay = NAV_REPEAT_DELAY;
                StepNavFocus(direction);
            }
            else
            {
                _navRepeatDelay -= elapsed;

                if (_navRepeatDelay <= 0f)
                {
                    _navRepeatDelay = NAV_REPEAT_INTERVAL;
                    StepNavFocus(direction);
                }
            }

            if (!edgeInputAllowed) return;

            //B is Escape. Read before A, since backing out changes the screen the accept below would act on.
            if (pad.IsButtonDown(Buttons.B) && !_previousPad.IsButtonDown(Buttons.B))
            {
                MenuBack();
                return;
            }

            if ((pad.IsButtonDown(Buttons.A) && !_previousPad.IsButtonDown(Buttons.A)) || IsKeyEdge(keyboard, Keys.Enter))
            {
                //Pressing accept with the cursor down only raises it. Firing the top entry instead would make
                //the pad's first press mean whatever happened to be first — "New Game" over a session in
                //progress, on the very screen that exists to offer Continue.
                if (_navIndex < 0) StepNavFocus(1);
                else ActivateNavEntry();
            }
        }

        private void StepNavFocus(int direction)
        {
            //The first press only shows the cursor, at the top of the list, rather than stepping off nothing
            _navIndex = _navIndex < 0
                ? (direction > 0 ? 0 : _navEntries.Count - 1)
                : (_navIndex + direction + _navEntries.Count) % _navEntries.Count;

            ApplyNavHighlight();
        }

        /// <summary>
        /// Presses the focused entry. The action is carried on the button's own <c>Tag</c> because the entries
        /// are found by walking the widget tree, which has no way back to the delegate handed to
        /// <see cref="MenuButton"/> — and reaching into Myra's own click plumbing would tie this to a version
        /// of it. Returns immediately after: the action may swap the screen or tear the session down, and
        /// <see cref="_navEntries"/> is not the same list afterwards.
        /// </summary>
        private void ActivateNavEntry()
        {
            if (_navIndex < 0 || _navIndex >= _navEntries.Count) return;

            (_navEntries[_navIndex].Tag as Action)?.Invoke();
        }

        #endregion

        /// <summary>
        /// Replays the current entry by tearing the session down and building the same level again, so the
        /// score, the multiplier, the budget and the cluster all start over. <see cref="GameplayScreen.BuildLevel"/>
        /// is the real reload the result screen offers as Retry.
        /// </summary>
        internal void RetryLevel()
        {
            _gameplayScreen.BuildLevel(_gameplayScreen.LevelIndex);
            EnterPlaying();
        }

        /// <summary>
        /// Builds the next entry of the set and drops straight into it. Only ever called from the "Next Level"
        /// button, which is itself only shown when a next entry exists — see <see cref="ResultPage"/>.
        /// </summary>
        internal void AdvanceLevel()
        {
            _gameplayScreen.BuildLevel(_gameplayScreen.LevelIndex + 1);
            EnterPlaying();
        }

        /// <summary>
        /// Puts the result screen over the stopped frame, in the same state a pause uses: the session goes on
        /// being drawn underneath (the page draws what is under it) while no longer updating, the heavy scrim
        /// dims it, and Myra owns the input. Called by the session when a level ends.
        /// </summary>
        internal void PresentResult(LevelResult result)
        {
            //The figures arrive as a SNAPSHOT taken at the level's end, not read by the screen when it draws —
            //see LevelResult for why that arithmetic has to be frozen.
            _resultPage.Take(result);

            _screens.Push(_resultPage);
        }

        internal void CycleExposure()
        {
            _exposure += EXPOSURE_STEP;

            //A command-line exposure can start anywhere, so this wraps on the ceiling rather than assuming
            //the value is already on the ladder
            if (_exposure > EXPOSURE_MAX + Constants.THOUSANDTH) _exposure = EXPOSURE_MIN;

            _tonemapEffect.Parameters["Exposure"].SetValue(_exposure);

            _settingsPage.Refresh();
        }

        internal void CycleSkyDome()
        {
            SetSkyDome((byte)(_skyDome == SKY_DOME_COUNT ? 1 : _skyDome + 1));
            _settingsPage.Refresh();
        }

        /// <summary>
        /// A dark plate behind a column that carries actual prose. A scrim alone is not enough for small text
        /// over a moving scene — every line lands on a different background — and darkening the whole scrim
        /// would throw away the scene the screen is standing in. No frame: the edge of the plate is the tone
        /// step itself, which is all it takes, and a drawn border is one more shape to fight the backdrop.
        /// The button screens need no plate at all — a button carries its own background.
        /// </summary>
        internal Panel Plate(Widget content)
        {
            Panel plate = new()
            {
                Background = new SolidBrush(MENU_PLATE),
                Padding = ScaledThickness(106, 67),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            plate.Widgets.Add(content);

            return plate;
        }

        /// <summary>
        /// A screen: the column of widgets centred over the whole frame, optionally on a scrim that dims the
        /// scene behind it. <paramref name="scrim"/> is null for every front-end screen — only a pause dims.
        /// The panel itself still stretches, because it is what centres the column and what Myra hit-tests.
        /// </summary>
        internal static Panel ScreenRoot(Widget content, IBrush scrim)
        {
            Panel panel = new()
            {
                Background = scrim,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            panel.Widgets.Add(content);

            return panel;
        }

        internal VerticalStackPanel MenuColumn() => new()
        {
            Spacing = Scaled(MENU_COLUMN_SPACING),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        internal Label ScreenHeading(string text) => new()
        {
            Text = text,
            Font = _menuFontHeading,
            TextColor = MENU_TEXT,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = ScaledThickness(0, 0, 0, 43),
        };

        internal Button MenuButton(string text, Action onClick) => MenuButton(text, onClick, out _);

        /// <summary>
        /// One menu entry. Myra's default button style is a framed grey tool button, so every brush is stated
        /// here instead: dark glass at rest, and the pointer <b>lifts</b> it with a wash of white rather than
        /// tinting it — see the palette above for why nothing in this menu carries a hue. No border either:
        /// the tone step at the button's edge is enough to read it as a control, and a drawn frame over seven
        /// different backdrops is a shape competing with all of them.
        /// </summary>
        internal Button MenuButton(string text, Action onClick, out Label label)
        {
            label = new Label
            {
                Text = text,
                Font = _menuFontBody,
                TextColor = MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            Button button = new()
            {
                Content = label,
                Width = Scaled(MENU_BUTTON_WIDTH),
                Padding = ScaledThickness(43, 18),
                HorizontalAlignment = HorizontalAlignment.Center,
                //Shared rather than one brush per button: a brush is a paint recipe with no state of its own,
                //and the focus highlight swaps between these two by identity (see ApplyNavHighlight)
                Background = MENU_BUTTON_BRUSH,
                OverBackground = MENU_BUTTON_OVER_BRUSH,
                PressedBackground = MENU_BUTTON_PRESSED_BRUSH,

                //Myra's stylesheet gives a button a border of its own, so it has to be explicitly cleared
                Border = null,
                BorderThickness = new Thickness(0),
            };

            button.Click += (_, _) => onClick();

            //The pad and the arrow keys find their entries by walking the widget tree, which has no way back
            //to this delegate — so it rides on the widget itself. See ActivateNavEntry.
            button.Tag = onClick;

            return button;
        }

        #endregion

        /// <summary>
        /// The city and the island it leaves a clearing for, plus everything the island carries: the glass
        /// drain, its gold beads, the dark pit shaft behind it and the glass plate the cluster will hang
        /// from. One unit box under a different instance matrix per building, so the whole skyline is a
        /// single instanced draw call.
        /// <para>
        /// All of it is scene, not session: it stands whether a game is being played or the menu's camera is
        /// merely orbiting it, which is why the ceiling's <i>mesh and renderer</i> live on the host while its
        /// kinematic <i>body</i> belongs to the session and is built with the rest of the world.
        /// </para>
        /// </summary>
        private void BuildScene()
        {
            _unitBox = new BoxMesh(GraphicsDevice, 1f, 1f, 1f);
            _city = new City(seed: CITY_SEED, arenaHalfExtent: ISLAND_RADIUS, config: _cityConfig);

            //The neon flags are what SetScene switches between the two city lightings; these are only the
            //values they start at, and are overwritten before the first frame is drawn.
            _cityRenderer = new InstancedModelRenderer(GraphicsDevice, _unitBox, Vector3.One, _instancingEffect)
            {
                CityConfig = _cityConfig,
                CityNeon = 1f,
                CityWindowBrightness = _cityConfig.NeonLook.WindowBrightness,

                //The specular ambient is not multiplied by albedo, and almost every facade of a city seen
                //from inside it is at a grazing angle where Fresnel is near 1 — left alone it bleaches the
                //whole skyline into a white cliff with the windows lost in it.
                SpecularAmbientStrength = 0.07f
            };

            _islandMesh = new DiscMesh(GraphicsDevice, ISLAND_INNER_RADIUS, ISLAND_RADIUS, ISLAND_EDGE_HEIGHT, ISLAND_SEGMENTS);
            _islandRenderer = new InstancedModelRenderer(GraphicsDevice, _islandMesh, ISLAND_COLOR, _instancingEffect)
            {
                //The stone surface, the Testbed's own: coursed slab relief over the marble texture, projected
                //triplanar because the disc carries no UVs worth the name. The detail texture is what selects
                //the technique that reads any of this — without one the renderer falls through to the plain
                //one and every setting here is silently dead, which is exactly what left the island a flat
                //grey band under the neon.
                DetailTexture = Content.Load<Texture2D>("GameObjects/Ground_8"),
                DetailTextureMapping = DetailMapping.Triplanar,
                DetailScale = 1f / ISLAND_DETAIL_SPAN,

                SurfaceReliefFrequency = 9f,
                SurfaceReliefStrength = 0.008f,
                SlabSize = 2f,
                SlabJointWidth = 0.025f,
                SlabJointDepth = 0.04f,
                CavityStrength = 0.7f,
                ReliefShadowStrength = 0.85f,
                ParallaxScale = 1f,

                //A floor is seen at a grazing angle everywhere except right under your feet, which is
                //exactly where Fresnel puts the sky reflection at full strength.
                SpecularAmbientStrength = 0.4f
            };
            _islandWorld = Matrix.CreateTranslation(0f, ISLAND_Y, 0f);

            //The drain. Its rim is flush with the stone top and meets the disc's bore directly, so it needs no
            //collar (the 0 argument — the machinery that filled the corners of a square pit is unused here).
            //Drawn translucent and with culling off, so the one-sided cone reads both looking down into it and
            //up through the hole; it joins SkyLitRenderers, so its sheen takes the dome like everything else.
            float funnelHeight = ISLAND_Y - FUNNEL_BOTTOM_Y;

            _funnelMesh = new FunnelMesh(GraphicsDevice, FUNNEL_TOP_RADIUS, FUNNEL_HOLE_RADIUS, funnelHeight, FUNNEL_SEGMENTS, 0f);
            _funnelRenderer = new InstancedModelRenderer(GraphicsDevice, _funnelMesh, FUNNEL_GLASS_COLOR, _instancingEffect, FUNNEL_GLASS_ALPHA);

            //Both gold beads in one mesh (built in the funnel's own local space), so one renderer draws them
            //and they share the funnel's world matrix. Opaque, so they go down with the opaque scene before
            //the glass; the gold specular rides in as a per-draw override rather than the scene's white.
            _funnelRimsMesh = new FunnelRimsMesh(GraphicsDevice, FUNNEL_TOP_RADIUS, FUNNEL_HOLE_RADIUS, funnelHeight,
                FUNNEL_RIM_TOP_TUBE, FUNNEL_RIM_HOLE_TUBE, FUNNEL_SEGMENTS, FUNNEL_RIM_TUBE_SEGMENTS);
            _funnelRimsRenderer = new InstancedModelRenderer(GraphicsDevice, _funnelRimsMesh, FUNNEL_RIM_COLOR, _instancingEffect)
            {
                Metalness = 1f,
                SpecularAmbientStrength = 1f
            };
            _funnelRimEffectParams = new BasicEffectParams(Vector3.One * SCENE_AMBIENT_INTENSITY, FUNNEL_RIM_SPECULAR, FUNNEL_RIM_SPECULAR_POWER, Vector3.Zero);

            _funnelWorld = Matrix.CreateTranslation(0f, ISLAND_Y, 0f);

            //The dark well behind the glass, drawn in the solid-terrain scenes only. It reuses FunnelMesh (a
            //wall facing inward and up, so it reads looking down into it), shares the funnel's mouth and
            //descends just outside it. Deliberately NOT in SkyLitRenderers and near-matte, so no dome
            //bleaches the inside of a hole in the ground.
            _pitMesh = new FunnelMesh(GraphicsDevice, FUNNEL_TOP_RADIUS, PIT_HOLE_RADIUS, ISLAND_Y - PIT_BOTTOM_Y, FUNNEL_SEGMENTS, 0f);
            _pitRenderer = new InstancedModelRenderer(GraphicsDevice, _pitMesh, PIT_COLOR, _instancingEffect)
            {
                SpecularAmbientStrength = 0.03f
            };
            _pitWorld = Matrix.CreateTranslation(0f, ISLAND_Y, 0f);

            //Note the glass the cluster hangs from is NOT built here: its footprint is the loaded level's
            //field, so RebuildCeilingRenderer makes it (and remakes it on every level) — which is why
            //SkyLitRenderers tolerates a null ceiling renderer and ApplySkyLighting runs again after a load.
        }

        /// <summary>
        /// Rebuilds the drawn glass plate at the loaded field's footprint, called by the session as it
        /// installs a level. The renderer is recreated, so it starts without the sky palette —
        /// <see cref="ApplySkyLighting"/> has to run after this, exactly as it does after the Testbed's
        /// <c>FitCeilingToMap</c>.
        /// </summary>
        internal void RebuildCeilingRenderer(float stageSizeX, float stageSizeZ)
        {
            _ceilingMesh?.Dispose();
            _ceilingRenderer?.Dispose();

            //Odd levels are shifted by +0.5 and a ball's radius is another 0.5, so a field's worth of balls is
            //one unit wider than its cell count; the plate covers it with that margin, as the Testbed's does.
            //It is drawn from the kinematic body's own pose, so the glass and the collidable cannot disagree.
            _ceilingMesh = new BoxMesh(GraphicsDevice, stageSizeX + 1f, 1f, stageSizeZ + 1f);
            _ceilingRenderer = new InstancedModelRenderer(GraphicsDevice, _ceilingMesh, CEILING_GLASS_COLOR, _instancingEffect, CEILING_GLASS_ALPHA);
        }

        #region Levels

        /// <summary>
        /// Reads the level set — the file that says which level is first and which is second — from the
        /// <c>Levels</c> directory beside the executable. Run once at load, so a broken set is reported before
        /// the player ever presses Play rather than at the moment they do.
        /// <para>
        /// A missing or broken set is <b>not</b> fatal: <see cref="_levelSet"/> stays null and the session
        /// falls back to the procedural pyramid the game shipped with, so the thing still plays. That is
        /// deliberate — the levels are loose data files beside the binary precisely so they can be edited, and
        /// a typo in one of them should cost a level, not the game.
        /// </para>
        /// </summary>
        private void LoadLevelSet()
        {
            //AppContext.BaseDirectory, not the working directory: the game is launched from a shortcut, from
            //a shell somewhere else and from the debugger, and only the first of those has them agree
            string path = Path.Combine(AppContext.BaseDirectory, LEVELS_DIRECTORY, LevelSet.DefaultFileName);

            try
            {
                _levelSet = LevelSet.Load(path);

                Console.WriteLine($"[levels] '{_levelSet.Name ?? LevelSet.DefaultFileName}': {_levelSet.Count} level(s)");

                //Every entry's rules, at load rather than as each level comes up: an inconsistently authored
                //set is then obvious in one place before a single level is played.
                for (int i = 0; i < _levelSet.Count; i++)
                    Console.WriteLine($"[levels]   {i + 1}. '{_levelSet.DisplayName(i)}' — {_levelSet.DescribeRules(i)}");
            }
            catch (Exception e)
            {
                //Anything at all: no file, no directory, malformed JSON, a set that lists nothing. The game
                //has a level of its own to fall back on, so this is a log line and not a crash.
                Console.WriteLine($"[levels] No level set at '{path}' ({e.Message}); using the built-in level");
                _levelSet = null;
            }
        }

        #endregion

        #region The session on the stack (menu ⇄ play)

        /// <summary>
        /// Starts playing. The world — the level's map, the simulation, the ceiling body, the drain's
        /// collision mesh and the cluster — is built by the session screen rather than at load, so the menu
        /// comes up without paying for it and a machine that never presses Play never pays at all.
        /// </summary>
        /// <param name="newGame">
        /// True to throw away a session in progress and deal the first level again; false to carry on with
        /// whatever is already standing (and to build it, if this is the first time).
        /// </param>
        private void StartGame(bool newGame)
        {
            if (newGame || !_gameplayScreen.IsBuilt) _gameplayScreen.BuildLevel(0);

            EnterPlaying();
        }

        /// <summary>
        /// Starts the level the player picked. Always a fresh build, even if that entry is the one already
        /// standing: choosing a level off the picker means starting it, not resuming a half-played attempt at
        /// it — resuming is what Continue is for.
        /// </summary>
        internal void StartGameAt(int index)
        {
            _gameplayScreen.BuildLevel(index);
            EnterPlaying();
        }

        /// <summary>
        /// Puts the session on the stack, over the standing backdrop and with nothing above it — whatever the
        /// player navigated through to get here is popped on the way. The input hygiene a (re)entry needs —
        /// the cleared mouse-aim baseline, the re-polled pad — lives on the screen itself
        /// (<see cref="GameplayScreen.CoveredChanged"/>), which the manager raises on whichever screen ends up
        /// on top, so a fresh push and the pop of a pause arrive through the one path.
        /// </summary>
        private void EnterPlaying()
        {
            _screens.PopTo<BackdropScreen>();
            _screens.Push(_gameplayScreen);
        }

        /// <summary>Puts the pause menu over the frame the player was looking at; the stack freezes the game under it.</summary>
        internal void PauseGame() => _screens.Push(_pausePage);

        /// <summary>
        /// Back into the game: the pause pops off and the gameplay screen is the top again — its
        /// <see cref="GameplayScreen.CoveredChanged"/> re-captures the cursor and re-baselines the input.
        /// </summary>
        internal void ResumeGame() => _screens.Pop();

        /// <summary>
        /// Back to the front end. The session is <b>kept</b>, not discarded — the gameplay screen leaves the
        /// stack but stands, so the main menu offers to resume it and to start a new game, which is the
        /// difference between a mis-click costing a click and costing a game. The camera is the backdrop's
        /// again from here: its orbit takes over the moment it updates.
        /// </summary>
        internal void ReturnToMainMenu()
        {
            //Down to the backdrop rather than a pop: the player may be anywhere — the pause, a settings panel
            //over it, the result screen — and the front end sits directly over the backdrop, not on top of
            //wherever they wandered.
            _screens.PopTo<BackdropScreen>();
            _screens.Push(_mainMenuPage);
        }

        #endregion

        /// <summary>
        /// Every renderer that takes its lighting from the sky dome. The ceiling's is the one that can be
        /// missing: it is rebuilt at each level's footprint, so before the first level is installed — and for
        /// the moment inside a load when the old one has gone — there is none.
        /// </summary>
        private IEnumerable<InstancedModelRenderer> SkyLitRenderers()
        {
            foreach (InstancedModelRenderer ballRenderer in _ballRenderers) yield return ballRenderer;

            yield return _cannonRenderer;
            yield return _cityRenderer;
            yield return _islandRenderer;
            yield return _funnelRenderer;
            yield return _funnelRimsRenderer;

            if (_ceilingRenderer != null) yield return _ceilingRenderer;
        }

        /// <summary>
        /// Derives the whole scene's lighting from the dome: hemisphere ambient plus a tinted key light, in
        /// linear radiance. The palette comes off the dome's vertex colours, so it arrives sRGB-encoded and
        /// is decoded here — everything below the decode scales and lerps it, and none of that means
        /// anything until it is radiance. Internal because the session re-runs it after rebuilding the
        /// ceiling's renderer, which starts without the palette.
        /// </summary>
        internal void ApplySkyLighting()
        {
            _zenithLinear = ColorSpace.SrgbToLinear(_sky.ZenithColor);
            _horizonLinear = ColorSpace.SrgbToLinear(_sky.HorizonColor);

            Vector3 keyTint = Vector3.Lerp(Vector3.One, _horizonLinear, SKY_TINT_STRENGTH);
            Vector3 backTint = Vector3.Lerp(Vector3.One, _zenithLinear, SKY_TINT_STRENGTH);

            foreach (InstancedModelRenderer renderer in SkyLitRenderers())
            {
                renderer.LinearLightRig = true;
                renderer.SkyColor = _zenithLinear * 1.3f;
                renderer.GroundColor = _horizonLinear * 0.75f;   //bounce from below is dimmer than the sky
                renderer.KeyLightPosition = -DefaultLighting.Light0Direction * 40f;
                renderer.SetLightTint(keyTint, backTint);
            }

            ApplyCloudPalette();
        }

        /// <summary>
        /// Puts the scene into the frame: the backdrop's own lighting defaults, the dome that suits it and
        /// the city's day-or-neon switch. The one place a scene change happens, shared by the random pick at
        /// startup, the scene menu and a loaded level that carries a scene of its own.
        /// </summary>
        internal void SetScene(SceneKind scene)
        {
            _scene = scene;

            //Neither city is drawn by the SceneRenderer — the city is one instanced box mesh under the shared
            //shader's city technique, and its two lightings are a flag and a brightness on the renderer.
            bool neon = scene == SceneKind.NeonCity;
            _cityRenderer.CityNeon = neon ? 1f : 0f;
            _cityRenderer.CityWindowBrightness = neon ? _cityConfig.NeonLook.WindowBrightness : _cityConfig.WindowBrightness;

            //The sea mirrors the sky, so a bright dome would give it a breezy mood rather than the moody one
            //it is built for, and the savanna wants the warmest gold horizon of the set. Every other scene
            //keeps whatever dome is up — including the neon city, whose default IS the dusk. The Testbed's
            //rule, so a scene looks the same in both.
            if (scene == SceneKind.Sea) _skyDome = SEA_SKY_DOME;
            else if (scene == SceneKind.Savanna) _skyDome = SAVANNA_SKY_DOME;

            //Re-derives the whole light rig from the dome, which every scene needs whether its dome changed
            //or not: the renderers were told nothing until now. Content.Load caches, so re-loading the dome
            //that is already up costs a dictionary hit and a re-read of its palette.
            SetSkyDome(_skyDome);
        }

        /// <summary>
        /// Loads a sky dome and re-derives the whole scene's lighting from it — the one place a dome change
        /// happens, shared by <see cref="SetScene"/>, the sky setting and a loaded level's own dome.
        /// </summary>
        internal void SetSkyDome(byte number)
        {
            _skyDome = number;
            _sky.SkyDomeModel = Content.Load<Model>("Skyes/SkyDome" + number);

            ApplySkyLighting();
        }

        /// <summary>
        /// Whether the scene is one of the solid-ground backdrops (mountains, meadow, savanna, desert), whose
        /// terrain has the island's footprint cut out of it and therefore needs the dark pit shaft drawn
        /// behind the glass funnel. The sea fills the drain with water and the two cities have their own
        /// canyon falling away below the island, so neither needs it.
        /// </summary>
        private static bool IsSolidTerrainScene(SceneKind scene) =>
            scene == SceneKind.Mountain || scene == SceneKind.Meadow ||
            scene == SceneKind.Savanna || scene == SceneKind.Desert;

        /// <summary>
        /// The per-frame inputs the shared <see cref="SceneRenderer"/> needs, taken from this frame's camera,
        /// sun and sky. The sun colour is the sun's own radiance tinted by the dome, and the clouds are handed
        /// over from the one shared field — which is what keeps the cloud the player looks at and the shadow
        /// it throws over the terrain the same cloud. A readonly struct, so this allocates nothing.
        /// </summary>
        private SceneFrame BuildSceneFrame() => new(
            _camera,
            SUN_DIRECTION,
            _zenithLinear,
            _horizonLinear,
            CLOUD_SUN_COLOR * Vector3.Lerp(Vector3.One, _horizonLinear, SKY_TINT_STRENGTH),
            _wallClock,
            _clouds.ApplyTo);

        /// <summary>
        /// Colours the clouds with the dome they hang in. A cloud has no colour of its own: its lit side is
        /// the colour of the sun and its underside the colour of the sky, so the lit side takes the very tint
        /// the key light takes — the clouds are lit by literally the same light as everything under them —
        /// and the underside takes the zenith, harder, since it sees no sun at all.
        /// <para>
        /// The dome never changes here, but this is not therefore optional: the two colours have no defaults,
        /// and left unset the whole deck comes out black.
        /// </para>
        /// </summary>
        private void ApplyCloudPalette()
        {
            Vector3 sunTint = Vector3.Lerp(Vector3.One, _horizonLinear, SKY_TINT_STRENGTH);
            Vector3 skyTint = Vector3.Lerp(Vector3.One, _zenithLinear, CLOUD_SHADOW_TINT_STRENGTH);

            _skyEffect.Parameters["CloudSunColor"].SetValue(CLOUD_SUN_COLOR * sunTint);
            _skyEffect.Parameters["CloudShadowColor"].SetValue(CLOUD_SHADOW_COLOR * skyTint);
        }

        /// <summary>
        /// Everything about the clouds that does not change frame to frame. The per-frame half — the clock and
        /// the camera — goes in <see cref="BeginSceneDraw"/>, right before the dome does.
        /// <para>
        /// The Testbed also flattens its <b>ambient</b> as cloud closes over the arena. That is not copied
        /// here: its overcast palette is authored for a daylight sky and is brighter than this dusk dome's
        /// own, so lerping towards it would <i>lighten</i> a night city as the weather thickened. The half
        /// that matters is the shader's, which takes the sun away per pixel where cloud covers it.
        /// </para>
        /// </summary>
        private void SetCloudParameters()
        {
            _skyEffect.Parameters["CloudDetailStrength"].SetValue(CLOUD_DETAIL_STRENGTH);
            _skyEffect.Parameters["CloudOpacity"].SetValue(CLOUD_OPACITY);
            _skyEffect.Parameters["CloudHorizonFade"].SetValue(CLOUD_HORIZON_FADE);
            _skyEffect.Parameters["CloudSunStep"].SetValue(CLOUD_SUN_STEP);
            _skyEffect.Parameters["CloudSelfAbsorption"].SetValue(CLOUD_SELF_ABSORPTION);
            _skyEffect.Parameters["CloudSunAbsorption"].SetValue(CLOUD_SUN_ABSORPTION);
            _skyEffect.Parameters["CloudSilverStrength"].SetValue(CLOUD_SILVER_STRENGTH);
            _skyEffect.Parameters["CloudSilverPower"].SetValue(CLOUD_SILVER_POWER);

            //The clouds are lit by whatever the rig's key light is, and the scene is shadowed along the very
            //same direction — so both shaders are told about the one sun
            _skyEffect.Parameters["SunDirection"].SetValue(SUN_DIRECTION);
            _instancingEffect.Parameters["SunDirection"].SetValue(SUN_DIRECTION);
        }

        /// <summary>
        /// This frame's scene point lights, on the shared instanced effect — so the balls, the island, the gun
        /// and the city are lit by the neon city's neon or the savanna's campfire on top of the sun and the
        /// dome, under whatever sky is up. Two scenes carry lights; the rest push a count of zero once and
        /// then cost nothing.
        /// </summary>
        private void ApplySceneLights()
        {
            int count = 0;

            if (_scene == SceneKind.NeonCity)
            {
                //A ring of alternating magenta and cyan around the island, so the near towers and the balls
                //actually take the neon's colour rather than the windows merely glowing at them
                NeonConfig neon = _cityConfig.NeonLook;
                count = Math.Min(neon.LightCount, MAX_SCENE_LIGHTS);

                for (int i = 0; i < count; i++)
                {
                    float angle = i / (float)count * MathHelper.TwoPi;
                    _sceneLightPos[i] = new Vector3(MathF.Cos(angle) * neon.LightRadius, neon.LightHeight, MathF.Sin(angle) * neon.LightRadius);
                    _sceneLightColor[i] = (i % 2 == 0) ? neon.Magenta.ToVector3() : neon.Cyan.ToVector3();
                    _sceneLightRange[i] = neon.LightRange;
                }
            }
            else if (_scene == SceneKind.Savanna)
            {
                //The campfire on the grass just off the island, flickering off the same wall clock its flame
                //billboard does — one clock, so the light and the fire cannot fall out of step
                _sceneLightPos[0] = _sceneRenderer.SavannaCampfirePosition;
                _sceneLightColor[0] = _sceneRenderer.CampfireColor(_wallClock);
                _sceneLightRange[0] = _sceneRenderer.SavannaCampfireRange;
                count = 1;
            }

            //Nothing above touches the arrays while the count is zero, so once the zero has gone out there is
            //nothing left that could have changed
            if (count == 0 && _lastSceneLightCount == 0) return;

            _sceneLightPositionParam.SetValue(_sceneLightPos);
            _sceneLightColorParam.SetValue(_sceneLightColor);
            _sceneLightRangeParam.SetValue(_sceneLightRange);
            _sceneLightCountParam.SetValue(count);

            _lastSceneLightCount = count;
        }

        protected override void Update(GameTime gameTime)
        {
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _wallClock += elapsed;

            //The very click that refocuses a windowed game would otherwise read as a fresh press against a
            //stale "released" state and fire an unintended shot, since input is not sampled while inactive.
            EdgeInputAllowed = IsActive && _wasActive;

            //The whole frame is the stack's now: pending pushes and pops are applied, then the update walks
            //top-down until a screen freezes what is under it — which is how a pause stops the game and how
            //the front end keeps the backdrop turning under itself. Myra runs its click handlers in Draw, so
            //a page opened by a click lands here on the following frame, which is the deferred design.
            _screens.Update(gameTime);

            _wasActive = IsActive;

            base.Update(gameTime);
        }

        /// <summary>
        /// The menu's shared frame, run by the <b>active</b> page from its <see cref="MenuPage.Update"/>: the
        /// pointer, the keys the menu does not carry as buttons — Escape, and the two display toggles that are
        /// useful from anywhere — and the pad navigation. Myra consumes the mouse and the keyboard itself (in
        /// <c>Desktop.Render</c>, at the end of <see cref="Draw"/>), so this owes it nothing else.
        /// <para>
        /// The keyboard snapshot is stored into the very same <see cref="_previousKeyboard"/> the play loop
        /// uses, which is what makes the two hand over cleanly: the Escape that paused the game is still down
        /// on the first menu frame and is correctly not seen as a second press, and the Escape that resumed it
        /// is likewise not seen again by the play loop.
        /// </para>
        /// </summary>
        internal void UpdateMenuChrome(GameTime gameTime)
        {
            //The cursor is the pointer here, not the aim; nothing recentres it while a menu is up
            IsMouseVisible = true;

            if (!IsActive) return;

            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            KeyboardState keyboard = Keyboard.GetState();
            GamePadState pad = GamePad.GetState(PlayerIndex.One);

            if (EdgeInputAllowed)
            {
                //Escape backs out one level; MenuBack owns which screens have a back at all
                if (IsKeyEdge(keyboard, Keys.Escape)) MenuBack();

                //The same two display keys the game has, because a player who wants windowed mode or no
                //FPS line wants them before pressing Play as much as after
                if (IsKeyEdge(keyboard, Keys.F11)) ToggleFullscreen();
                if (IsKeyEdge(keyboard, Keys.F12)) ToggleFpsOverlay();
            }

            //After the keys above, so an Escape and a B press in the same frame cannot both act, and
            //before the snapshots below, which are what its own edge tests are read against
            UpdateMenuNavigation(elapsed, keyboard, pad, EdgeInputAllowed);

            _previousKeyboard = keyboard;
            _previousPad = pad;
        }

        /// <summary>
        /// Lowers supersampling on a machine that visibly cannot afford it, measured rather than guessed.
        /// Driven by the <see cref="BackdropScreen"/>, which updates exactly while the front end is what is
        /// being drawn — and the front end is a fair probe on its own: it draws the same city, clouds, glare
        /// and tonemap the game does, at the same factor, and it is the fixed scene cost rather than the ball
        /// count that dominates on the hardware this exists for (#64).
        /// <para>
        /// It notices by <b>timing the frames it is already drawing</b>, not by recognising the adapter. A
        /// name or vendor list is wrong on the first machine nobody tested — plenty of AMD parts are discrete,
        /// plenty of Intel ones now are too — and it cannot see the other reasons a frame is slow: a 4K
        /// display, a laptop on battery, something else eating the GPU.
        /// </para>
        /// <para>
        /// One step per verdict and never upwards. Raising it again on a machine that recovered would put the
        /// player back where they started, and a quality dial that oscillates is worse than one set too low.
        /// </para>
        /// </summary>
        internal void TuneQualityToFrameRate(float elapsed)
        {
            if (_qualitySettled) return;

            if (_qualityWarmupLeft > 0f)
            {
                _qualityWarmupLeft -= elapsed;
                return;
            }

            _qualityWindowSeconds += elapsed;
            _qualityWindowFrames++;

            if (_qualityWindowSeconds < QUALITY_WINDOW_SECONDS) return;

            float fps = _qualityWindowFrames / _qualityWindowSeconds;

            _qualityWindowSeconds = 0f;
            _qualityWindowFrames = 0;

            if (fps >= QUALITY_MIN_FPS)
            {
                //Fast enough. Stop measuring rather than keep watching: from here the only thing that could
                //trip it is the player alt-tabbing away, and lowering quality for that would be absurd.
                _qualitySettled = true;
                return;
            }

            //Steps the TIER, not supersampling alone, which is the whole point of #63: on the two city scenes
            //supersampling is only the first of three measured levers, and stepping it alone left the neon city
            //at 30 FPS with 40% still on the table.
            QualityLevel lowered = _quality == QualityLevel.High ? QualityLevel.Medium : QualityLevel.Low;

            Console.WriteLine($"[quality] {fps:F0} FPS in the menu at {_quality} — lowering to {lowered}");

            ApplyQuality(lowered);
            ShowQualityNotice(lowered);

            //A tier step rebuilds the city and resizes the scene target, which hitches a frame or two. Left
            //unarmed, the very next window would measure that hitch and step again on the strength of it.
            _qualityWarmupLeft = QUALITY_WARMUP_SECONDS;

            //Nothing left to give: Low is the bottom of the ladder.
            if (_quality == QualityLevel.Low) _qualitySettled = true;
        }

        /// <summary>
        /// Tells the player what was changed and where to change it back. Once per run, on the main menu —
        /// which is where they are: the verdict lands about three seconds in.
        /// </summary>
        private void ShowQualityNotice(QualityLevel quality) => _mainMenuPage.ShowQualityNotice(quality);

        /// <summary>
        /// Steps the quality tier, which is the setting the player sees. Wraps Low → Medium → High → Low.
        /// </summary>
        internal void CycleQuality()
        {
            ApplyQuality(_quality switch { QualityLevel.Low => QualityLevel.Medium, QualityLevel.Medium => QualityLevel.High, _ => QualityLevel.Low });

            //The player has now said what they want, so the adaptive path stops second-guessing them — and the
            //notice about what it did has been answered and goes away.
            _qualitySettled = true;

            _mainMenuPage.ClearQualityNotice();
        }

        /// <summary>
        /// The one place the tier changes — <see cref="SetScene"/>'s rule applied to quality: everything a tier
        /// touches is written here and nowhere else, so the adaptive probe, the settings row and the command line
        /// cannot disagree about what a tier means.
        /// <para>
        /// The city's dials are pushed to the shader on every city draw (the renderer holds the config by
        /// reference), so writing them is enough; only the block radius is baked into the generated buildings and
        /// needs the city rebuilt. The renderer itself is <b>not</b> recreated, so its sky palette survives and no
        /// <see cref="ApplySkyLighting"/> is owed here.
        /// </para>
        /// </summary>
        internal void ApplyQuality(QualityLevel quality)
        {
            _quality = quality;

            QualityPreset preset = QualityPreset.Presets[(int)quality];

            _cityConfig.FacadeGrainStrength = preset.FacadeGrainStrength;
            _cityConfig.WindowFrameWidth = preset.WindowFrameWidth;

            //Rebuilt only when the count actually changes: the generator walks a block grid and the instance
            //array is re-uploaded, which is a frame's hitch and not something a tier step should pay for twice.
            //Null until BuildScene has run, which is the case when the command line pins a tier at startup.
            if (_city != null && _cityConfig.RadiusBlocks != preset.CityRadiusBlocks)
            {
                _cityConfig.RadiusBlocks = preset.CityRadiusBlocks;
                _city = new City(seed: CITY_SEED, arenaHalfExtent: ISLAND_RADIUS, config: _cityConfig);
            }
            else _cityConfig.RadiusBlocks = preset.CityRadiusBlocks;

            SetSupersampleFactor(preset.SupersampleFactor);
        }

        /// <summary>
        /// The one place the factor changes: the scene target's size is derived from it, and the tonemap has to
        /// be told how many samples its box filter is averaging.
        /// </summary>
        private void SetSupersampleFactor(int factor)
        {
            _supersampleFactor = Math.Clamp(factor, 1, 4);

            _tonemapEffect.Parameters["SupersampleFactor"].SetValue(_supersampleFactor);

            //The factor is the scene target's size, so changing it is exactly what makes EnsureSceneTarget
            //recreate the target rather than recognize it as the one already there
            EnsureSceneTarget();

            //Null-conditional because the tier is applied during LoadContent, before the menu pages exist — a
            //command-line quality= reaches here well before there is a settings row to write the value onto.
            _settingsPage?.Refresh();
        }

        internal void ToggleFullscreen()
        {
            _fullscreen = !_fullscreen;
            SetGraphics();
            _settingsPage.Refresh();
        }

        internal void ToggleFpsOverlay()
        {
            _info.Visible = !_info.Visible;
            _settingsPage.Refresh();
        }

        protected override void Draw(GameTime gameTime)
        {
            //Bottom-up down the stack to the lowest uncovered screen: the backdrop (or the gameplay screen)
            //runs the whole pipeline — the HDR target, the setting, its own 3D, the resolve — and the pages
            //above it draw nothing themselves, because their picture is the Myra desktop below.
            _screens.Draw(gameTime);

            //The FPS overlay: a component, in display space, after the resolve
            base.Draw(gameTime);

            //The Myra GUI renders last, on top of everything, straight to the back buffer (base.Draw and the
            //resolve leave it bound) — and only while a menu page is the active screen: Render also processes
            //Myra's own mouse and keyboard input, which may only happen where the game's own input has stood
            //down. There is one desktop and it holds the top page's tree, so nothing stale can be drawn.
            //
            //While the window is not focused it is laid out and drawn but NOT fed input: Mouse.GetState
            //reports the button and a window-relative position whether the window has focus or not, so a
            //click meant for another application that happens to land over where a menu entry is would
            //otherwise press it — and "Quit" would close a game nobody was looking at.
            if (_desktop != null && _screens.Active is MenuPage)
            {
                //Refitted here rather than off the resize event, so the one place it can be out of date is
                //the frame that is about to draw it — a fullscreen switch and a window drag both land here
                EnsureMenuLayout();

                if (IsActive) _desktop.Render();
                else
                {
                    _desktop.UpdateLayout();
                    _desktop.RenderVisual();
                }
            }

            //Last, so it counts a frame that has actually been drawn end to end
            if (_logFrameRate) LogFrameRate((float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        #region The frame-rate log (benchmarking)

        //Deliberately NOT TuneQualityToFrameRate's window, which is a latching probe: it measures 1.5 s in the
        //menu and then stops watching on purpose (#62), which is exactly what a benchmark must not do. This
        //counts presented frames for as long as the process lives, in the menu and in a level alike.
        //
        //It exists because nothing could measure this game from a script at all: the frame rate the player sees
        //is drawn and never logged, and InfoRenderer freezes its counter while the overlay is hidden. #64 asks
        //which part of the fixed per-frame cost is the expensive one, and that question cannot be answered
        //without a number a script can read.
        private readonly bool _logFrameRate;
        private float _fpsWindow;
        private int _fpsFrames;

        /// <summary>
        /// Writes one line a second: the frame rate and every setting that changes what it means, so two runs —
        /// or two machines — can be compared without having to remember what each was launched with.
        /// </summary>
        private void LogFrameRate(float elapsed)
        {
            _fpsWindow += elapsed;
            _fpsFrames++;

            if (_fpsWindow < 1f) return;

            //Divided by the window actually measured rather than assumed to be a second. At the frame rates this
            //exists to measure a single frame overshoots by more than a tenth of it, and calling that "frames
            //this second" would be wrong by the same tenth.
            Console.WriteLine($"[fps] {_fpsFrames / _fpsWindow:F1} — {_scene}, dome {_skyDome}, ssaa {_supersampleFactor}x"
                + $", {GraphicsDevice.PresentationParameters.BackBufferWidth}x{GraphicsDevice.PresentationParameters.BackBufferHeight}"
                + $", vsync {(_uncappedFps ? "off" : "on")}");

            _fpsWindow = 0f;
            _fpsFrames = 0;
        }

        #endregion

        #region The setting's slices (the pipeline the bottom screens run)

        /// <summary>
        /// The setting on its own — the whole pipeline with both gameplay slots empty. It is what the front
        /// end looks at, and it is also what the <see cref="GameplayScreen"/> falls back to on the one frame
        /// it is still on the stack with no session left to draw.
        /// </summary>
        internal void DrawSetting()
        {
            SceneFrame sceneFrame = BeginSceneDraw();

            DrawSettingGlass();
            FinishSceneDraw(sceneFrame);
        }

        /// <summary>
        /// Everything up to the frame's first gameplay slot: binds the HDR scene target, clears it to the
        /// dome's horizon, hands the clouds and the camera to the shaders, draws the sky, the backdrop and
        /// the island with its pit, and returns the <see cref="SceneFrame"/> the closing slices need. The
        /// gameplay screen draws its gun, cluster and trails after this; the backdrop goes straight on to
        /// <see cref="DrawSettingGlass"/>.
        /// </summary>
        internal SceneFrame BeginSceneDraw()
        {
            GraphicsDevice.SetRenderTarget(_sceneTarget);

            //Cleared to the dome's horizon colour rather than a fixed one: at a wide aspect the bottom
            //corners can look below the horizon past both the dome and the island, and there any other
            //colour shows up as a band instead of blending into the hazed skyline.
            GraphicsDevice.Clear(new Color(_horizonLinear));

            //The weather runs off the same wall clock the balls pulse to, so it keeps drifting whatever the
            //game does. Handed to both shaders from the one field, which is what keeps the cloud the player
            //looks at and the shadow it throws across the cluster the same cloud.
            _clouds.Time = _wallClock;
            _clouds.ApplyTo(_skyEffect);
            _clouds.ApplyTo(_instancingEffect);

            _skyCameraPositionParam.SetValue(_camera.Position);

            //Stated before the frame's first draw rather than only after it. SkyDome.Draw sets the sampler
            //and the depth state it needs but neither the blend nor the cull mode, and what ran last is the
            //overlay's SpriteBatch (AlphaBlend, CullCounterClockwise) over the tonemap's full-screen quad
            //(Opaque, CullNone) — so the dome would be drawn under whichever of them finished the frame.
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;

            _sky.Draw(_camera);

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            //Stated rather than inherited, for the same reason: the scene's cull mode must not depend on
            //what the previous frame's last pass happened to leave behind.
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            //The setting: the backdrop, then the island standing in it. The two cities are the procedural
            //skyline under the shared shader's city technique; the other five are the SceneRenderer's
            //self-lit terrain and water, which need this frame's camera, sun and sky handed over.
            SceneFrame sceneFrame = BuildSceneFrame();

            //The scene's own point lights (the neon ring, the savanna's campfire) onto the shared instanced
            //effect, so the island, the gun and the balls take them as well as the towers
            ApplySceneLights();

            if (_scene == SceneKind.City || _scene == SceneKind.NeonCity)
            {
                //The city's windows keep their own rhythm off the wall clock — a city's lamps do not stop
                //because the game is paused
                _cityRenderer.CityWindowTime = _wallClock;
                _cityRenderer.Draw(_camera, _city.Buildings, _city.Buildings.Length, _sceneEffectParams);
            }
            else _sceneRenderer.DrawEnvironment(_scene, sceneFrame);

            //The island is a solid ring, so the nearest face wins on depth and the winding is moot
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            _islandRenderer.Draw(_camera, _islandWorld, _sceneEffectParams);

            //The dark well behind the glass drain, in the solid-terrain scenes only: it fills the hole those
            //shaders cut in the ground, so the drain reads as a deep shaft rather than as a glass ring over
            //bright sky haze. Opaque and CullNone like the island, and before the glass, which composites
            //over it. The two cities and the sea have their own canyon or water down there instead.
            if (IsSolidTerrainScene(_scene)) _pitRenderer.Draw(_camera, _pitWorld, _sceneEffectParams);

            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            return sceneFrame;
        }

        /// <summary>
        /// The drain's gold beads and its glass, after the frame's opaque work: the beads are opaque but the
        /// funnel composites over everything drawn so far — including the gameplay screen's balls, which is
        /// why this is a separate slice the session calls after its own 3D.
        /// </summary>
        internal void DrawSettingGlass()
        {
            //The drain's gold beads are opaque, so they belong to the opaque scene and go down before the
            //glass the funnel composites over them. A closed convex tube, so CullNone is safe (the nearest
            //face wins on depth and the winding is moot) and is one less thing to get wrong unseen.
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            _funnelRimsRenderer.Draw(_camera, _funnelWorld, _funnelRimEffectParams);

            //The glass funnel itself: one open, single-sided cone, so culling stays off to show both the
            //inside looking down into it and the outside looking up through the hole.
            _funnelRenderer.Draw(_camera, _funnelWorld, _sceneEffectParams);
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// The frame's close: the scene's foreground weather — the mountain's snow, the sea's spray, the
        /// savanna's flame — settles over everything, and the resolve takes the HDR target to the back
        /// buffer. Display space from here on.
        /// </summary>
        internal void FinishSceneDraw(SceneFrame sceneFrame)
        {
            //A no-op in the two cities and the desert, which carry no overlay weather
            _sceneRenderer.DrawOverlays(_scene, sceneFrame);

            ResolveSceneTarget();
        }

        #endregion

        #region Render targets and the resolve

        private void EnsureSceneTarget()
        {
            if (GraphicsDevice == null) return;

            int width = GraphicsDevice.PresentationParameters.BackBufferWidth * _supersampleFactor;
            int height = GraphicsDevice.PresentationParameters.BackBufferHeight * _supersampleFactor;

            if (_sceneTarget != null && _sceneTarget.Width == width && _sceneTarget.Height == height) return;

            _sceneTarget?.Dispose();

            //Supersampling already averages its samples per output pixel, geometry edges included, so MSAA
            //only earns its memory with supersampling off — which is exactly the ssaa=1 path this used to
            //leave with no antialiasing of any kind, on the setting a weak machine reaches for first. It goes
            //on the scene target, never the back buffer: nothing but one resolved quad ever reaches that.
            _sceneTarget = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.HdrBlendable,
                DepthFormat.Depth24Stencil8, _supersampleFactor > 1 ? 0 : MSAA_SAMPLES, RenderTargetUsage.DiscardContents);

            //Sized off the back buffer, not the supersampled target: the glare is blurred anyway, so the
            //extra samples buy nothing and would only cost fill rate to produce
            int glareWidth = Math.Max(GraphicsDevice.PresentationParameters.BackBufferWidth / GLARE_DOWNSAMPLE, 1);
            int glareHeight = Math.Max(GraphicsDevice.PresentationParameters.BackBufferHeight / GLARE_DOWNSAMPLE, 1);

            _glareBright?.Dispose();
            _glareStreak?.Dispose();

            _glareBright = new RenderTarget2D(GraphicsDevice, glareWidth, glareHeight, false, SurfaceFormat.HdrBlendable, DepthFormat.None);
            _glareStreak = new RenderTarget2D(GraphicsDevice, glareWidth, glareHeight, false, SurfaceFormat.HdrBlendable, DepthFormat.None);
        }

        private void DrawGlare()
        {
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.SetVertexBuffer(_fullScreenQuad);

            GraphicsDevice.SetRenderTarget(_glareBright);
            _glareEffect.CurrentTechnique = _glareBrightPassTechnique;
            _glareSourceTextureParam.SetValue(_sceneTarget);
            DrawFullScreenQuad(_glareEffect);

            GraphicsDevice.SetRenderTarget(_glareStreak);
            _glareEffect.CurrentTechnique = _glareStreakTechnique;
            _glareSourceTextureParam.SetValue(_glareBright);
            _glareSourceTexelSizeParam.SetValue(new Vector2(1f / _glareBright.Width, 1f / _glareBright.Height));
            DrawFullScreenQuad(_glareEffect);
        }

        /// <summary>
        /// Box-filters the supersampled HDR scene onto the back buffer, tonemaps it from linear radiance
        /// into display range and encodes it to sRGB. The frame's one and only exit from linear light.
        /// </summary>
        private void ResolveSceneTarget()
        {
            DrawGlare(); //Reads the scene target, so it has to happen before the back buffer is bound

            GraphicsDevice.SetRenderTarget(null);

            //The constants (exposure, glare intensity, supersample factor, the underwater no-op) were set
            //once in LoadContent and persist on the effect; only what can change goes out per frame
            _tonemapGlareTextureParam.SetValue(_glareStreak);
            _tonemapSceneTextureParam.SetValue(_sceneTarget);
            _tonemapSourceTexelSizeParam.SetValue(new Vector2(1f / _sceneTarget.Width, 1f / _sceneTarget.Height));

            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.SetVertexBuffer(_fullScreenQuad);

            DrawFullScreenQuad(_tonemapEffect);
        }

        private void DrawFullScreenQuad(Effect effect)
        {
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
            }
        }

        /// <summary>
        /// The quad the post-processing passes draw. Its corners are already in normalized device
        /// coordinates, so no pass needs a transform of any kind.
        /// </summary>
        private void CreateFullScreenQuad()
        {
            VertexPositionTexture[] corners =
            {
                new(new Vector3(-1f, 1f, 0f), new Vector2(0f, 0f)),
                new(new Vector3(1f, 1f, 0f), new Vector2(1f, 0f)),
                new(new Vector3(-1f, -1f, 0f), new Vector2(0f, 1f)),
                new(new Vector3(1f, -1f, 0f), new Vector2(1f, 1f))
            };

            _fullScreenQuad = new VertexBuffer(GraphicsDevice, VertexPositionTexture.VertexDeclaration, corners.Length, BufferUsage.WriteOnly);
            _fullScreenQuad.SetData(corners);
        }

        #endregion

        protected override void UnloadContent()
        {
            _sceneTarget?.Dispose();
            _glareBright?.Dispose();
            _glareStreak?.Dispose();
            _fullScreenQuad?.Dispose();
            _spriteBatch?.Dispose();
            _pixel?.Dispose();

            if (_ballMeshes != null) foreach (SphereMesh mesh in _ballMeshes) mesh?.Dispose();
            if (_ballRenderers != null) foreach (InstancedModelRenderer renderer in _ballRenderers) renderer?.Dispose();

            _cannonMesh?.Dispose();
            _cannonRenderer?.Dispose();
            _unitBox?.Dispose();
            _cityRenderer?.Dispose();
            _islandMesh?.Dispose();
            _islandRenderer?.Dispose();
            _funnelMesh?.Dispose();
            _funnelRenderer?.Dispose();
            _funnelRimsMesh?.Dispose();
            _funnelRimsRenderer?.Dispose();
            _pitMesh?.Dispose();
            _pitRenderer?.Dispose();
            _ceilingMesh?.Dispose();
            _ceilingRenderer?.Dispose();

            //The five self-lit backdrops own their own meshes, particle buffers and effects
            _sceneRenderer?.Dispose();

            //The menu: the Desktop holds the widget tree, and each FontSystem holds the glyph atlases it
            //rasterized for the sizes that were asked of it
            _desktop?.Dispose();
            _menuFontSystem?.Dispose();
            _menuFontSystemBold?.Dispose();

            //The session: the simulation, the contact events, the dispatcher, the pool and the shot-trail
            //buffers all live on the gameplay screen now, which disposes them in the order they need
            _gameplayScreen?.DisposeResources();

            base.UnloadContent();
        }
    }
}
