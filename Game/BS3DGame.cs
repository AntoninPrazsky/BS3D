using BS3D.Audio;
using BS3D.Effects;
using BS3D.Screens;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using Prazsky.BS3D;
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
using System.Runtime.InteropServices;
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

        //Procedurally synthesized SFX (shot, landing). Built once in LoadContent and shared by the gameplay
        //screen and, later, the menu — same pattern as the camera.
        private ProceduralAudio _audio;

        //The victory display. The frame's, not the session's: it has to go on running once the result screen
        //covers the gameplay screen, which is exactly when the player is watching it.
        private Fireworks _fireworks;

        //The level theme. On the host with the rest of the audio, because it outlives any one session and a
        //track that restarted from the top on every retry would be exhausting.
        private ProceduralMusic _music;

        //Whether the front end's music is on — the edge detector for the stack question in Update (#46).
        private bool _menuMusicOn;

        //The scenes' ambient beds (#46): one looping texture per backdrop, crossfaded by SetScene. On the
        //host with the rest of the audio — the scene is the host's, and its sound runs pause included.
        private ProceduralAmbience _ambience;

        //Testing only: the "celebrate" argument, fired once the display exists.
        private readonly bool _startupCelebrate;

        //Testing only: the "lasers" argument, read by the session's warning check every frame.
        private readonly bool _startupLasers;

        //Wall clock. Everything alive in the scene runs off it — the balls' heartbeat, the city's windows —
        //so none of it is tied to a simulation that may later be paused.
        private float _wallClock;

        private bool _wasActive = true;

        /// <summary>The one camera. The front end's backdrop orbits it; the gameplay screen poses it while playing.</summary>
        internal RecoilCamera Camera => _camera;

        /// <summary>Procedurally generated SFX, shared by the gameplay screen and the menu.</summary>
        internal ProceduralAudio Audio => _audio;

        /// <summary>
        /// The victory display. On the host rather than on the session because a cleared level puts the result
        /// screen over the session, and a covered screen stops being updated — see <see cref="Fireworks"/>.
        /// </summary>
        internal Fireworks Fireworks => _fireworks;

        /// <summary>The level theme, synthesized at load and looped while a level is being played.</summary>
        internal ProceduralMusic Music => _music;

        /// <summary>The wall clock everything alive runs off, paused or not.</summary>
        internal float WallClock => _wallClock;

        /// <summary>
        /// Testing only (the <c>lasers</c> argument): pins the floor alarm's laser net on. Reaching it
        /// honestly means playing a level to within two ceiling steps of losing it, which can no more be
        /// scripted than clearing one can — the <c>celebrate</c> reasoning, for the session-owned effect.
        /// </summary>
        internal bool ForceLaserWarning => _startupLasers;

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

        //One SpriteBatch for everything drawn over the resolve: the gameplay screen's HUD and its crosshair
        //both go into this one. The white texel that used to sit beside it went with the crosshair in #76 —
        //Prazsky.Core.Render.Crosshair makes its own, and it was the texel's only consumer, so the host no
        //longer holds a texture for a mark it does not draw.
        private SpriteBatch _spriteBatch;

        internal SpriteBatch OverlayBatch => _spriteBatch;

        #endregion

        #region Post-processing (linear radiance in, sRGB out — the shared pipeline, #74)

        //The HDR scene target, the bloom pyramid, the tonemap resolve and every cached parameter live in
        //Prazsky.Core.Render.PostProcessPipeline now — one copy for all three executables. What stays here
        //is what is this executable's to decide: the look figures below, the Settings toggles that write
        //them, and the underwater amount (scene knowledge the pipeline takes as a per-frame scalar).
        private PostProcessPipeline _pipeline;

        //How far under the mean surface the underwater tint takes to reach full
        private const float UNDERWATER_FADE_DEPTH = 7f;
        private EffectParameter _skyCameraPositionParam;

        private static readonly float GLARE_THRESHOLD = 0.55f;

        //Peak red/blue channel displacement at the frame CORNERS, as a fraction of the frame (the shader
        //grows it quadratically from zero at the centre, so the cluster and the gun stay registered and
        //only the periphery fringes). Visible at a glance BY DESIGN: it shipped at 0.0016 first - under
        //two corner pixels - and nobody ever noticed it, which defeats a taste effect with its own
        //Settings row. At 1600x900 this is now a ~5 px corner shift (the red-to-blue split is twice
        //that), a deliberate stylized lens; the centre stays clean either way. Off in Settings sets the
        //uniform to 0, which also skips the shader's whole branch.
        private static readonly float CHROMATIC_ABERRATION = 0.004f;

        //On by default; a taste toggle in Settings, like the FPS counter (nothing persists — see docs).
        private bool _aberration = true;

        //Peak film-grain modulation at 50% grey, as a fraction of the display value. The shader weights
        //the grain by 4*luma*(1-luma), so it peaks in the mid-tones — about ±12/255 at its strongest
        //here (raised from 0.05, which was invisible at arm's length) — and vanishes into both black
        //and white: texture on the print, never sensor noise. One monochrome grain per OUTPUT pixel,
        //re-rolled every frame, applied after the tonemap curve. Off in Settings sets the uniform to 0,
        //which skips the shader's branch.
        private static readonly float FILM_GRAIN = 0.10f;

        //On by default, the aberration's sibling taste toggle
        private bool _grain = true;

        //Far lower than the streak star's 0.9: the pyramid ACCUMULATES on the way up, so the head carries
        //its own halo plus every wider level's, and the same subjective glow needs a fraction of the gain.
        private static readonly float GLARE_INTENSITY = 0.5f;

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

        //Space deliberately forces NO dome, unlike those two. Its dome is neither drawn (Space.fx covers the
        //whole frame) nor read (SpaceLightingConfig states the light rig instead, for the reasons set out
        //there) — so it is completely inert in that scene, and changing the player's dome behind their back to
        //no visible effect would be a silent side effect rather than a setting. Whatever is up stays up.

        private byte _skyDome = DEFAULT_SKY_DOME;

        /// <summary>
        /// Which of the seven settings the frame stands in — the backdrop the menu's camera orbits and the
        /// one the game is then played in, since the player picks it from the menu and it stays picked. The
        /// city and the neon city are the procedural <see cref="City"/> under two lightings; the other five
        /// are the shared <see cref="SceneRenderer"/>'s self-lit backdrops, the same ones the Testbed and the
        /// map editor draw.
        /// </summary>
        private SceneKind _scene = SceneKind.NeonCity;

        //How many scenes there are, and what each is called, are SceneRenderer's since #75:
        //SceneRenderer.SceneCount and SceneRenderer.SceneName, both in the declared order of SceneKind, so the
        //scene page still indexes its labels by the enum's own value. The count stood here as a literal 11 and
        //the name list existed twice, here and in the Testbed.

        private SceneRenderer _sceneRenderer;

        private SkyDome _sky;
        private Effect _skyEffect;

        /// <summary>
        /// The whole scene's lighting derived from the dome it stands under — the palette decoded to linear
        /// radiance, the hemisphere ambient and the tinted three-light rig — shared with the Testbed and the
        /// map editor since #75, and with it the sun's direction and radiance and the sky tint strength that
        /// used to stand here. Built in <see cref="LoadContent"/>, after the scene renderer it consults for the
        /// scenes that state a rig of their own.
        /// <para>
        /// <b>The game deliberately never steps the overcast lerp</b> (<see cref="SkyLightRig.StepOvercast"/>),
        /// which the Testbed does: that overcast palette is authored for a daylight sky and is brighter than
        /// this dusk dome's own, so lerping towards it would <i>lighten</i> a night city as the weather
        /// thickened. The half that matters is the shader's, which takes the sun away per pixel where cloud
        /// covers it. So <see cref="SkyLightRig.Overcast"/> stays 0 here — and <c>Lerp(a, b, 0)</c> is
        /// bit-exactly <c>a</c>, so this gets the un-lerped ambient rather than an approximation of it.
        /// </para>
        /// </summary>
        private SkyLightRig _rig;

        //The weather. Clouds live on a flat plane at a finite altitude rather than as a texture on the dome,
        //which is what lets the same field be both the cloud you look at and the shadow it throws: the sky
        //shader crosses the plane with the view ray, the ball/city/island shader with the sun ray. One field,
        //handed to both shaders from here, so the two cannot be tuned apart by accident.
        private readonly CloudField _clouds = new();

        //The cloud look values that used to stand here — the shadowed colour, the detail strength, the opacity,
        //the horizon fade, the sun step, both absorptions, both silver-lining figures and the harder tint the
        //shadowed side takes — are CloudField's own since #75. They were identical to the Testbed's to the last
        //digit, which is the drift a shared field should never have been able to have. The weather's shape
        //(plane, scale, wind, coverage) was already the field's, and the one direction both shaders are
        //shadowed along is SkyLightRig.SUN_DIRECTION. The LIT side's radiance went to the rig rather than to
        //CloudField, because it is the sun's and not the cloud's: SkyLightRig.SUN_RADIANCE, handed over as
        //ApplyPalette's argument so that it is bit-for-bit the number SceneFrame carries.

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

        //How many of the city's buildings the last frame actually drew, for the logfps line. A frame's worth of
        //diagnostics, not state anything renders from.
        private int _cityVisible;
        private BoxMesh _unitBox;
        private InstancedModelRenderer _cityRenderer;

        //The whole arena the gun stands on: the round island's stone cap and concrete drum, the glass drain
        //bored through its middle, the two gold beads that ring the drain's circles, and the dark pit shaft
        //that backs the glass in the solid-terrain scenes. Every mesh, procedural texture, renderer, world
        //matrix and figure of it is ArenaIsland's since #75 — the Testbed had a copy of the lot, value for
        //value, under ARENA_*/FUNNEL_*/PIT_* names against these ISLAND_*/FUNNEL_*/PIT_* ones. The figures the
        //rest of this executable still reads are constants on that type: ArenaIsland.TOP_Y (the old ISLAND_Y,
        //which the session's precise-aim floor and the drop cinematic derive from in constant expressions),
        //RADIUS, EDGE_HEIGHT, TERRAIN_HOLE_RADIUS and the drain's four figures the session's collider wants.
        //
        //The frame sequence stays here: the three DrawIsland/DrawPit/DrawGlass slices are placed by hand
        //below, which is what lets the session put its gun, balls and shot trails between the pit and the glass.
        private ArenaIsland _island;

        //The forest scene's scattered trees, rocks and stumps: every procedural texture, mesh variant,
        //renderer, matte material and encoded tint of them is ForestScatterRenderer's since #75 — the piece
        //stood in this file alone, which is the whole reason the Testbed and the map editor drew the forest as a
        //bare clearing on the very same shared terrain. Built once (a fixed default forest, like the city is a
        //fixed default city), and it needs no stone texture handed to it: ArenaIsland's copy is that
        //component's private business and SurfaceTexture.Stone is deterministic, so the one the scatter builds
        //for its boulders is the very same 512² tile — one more copy in memory and not a pixel of difference.
        //
        //What stays this file's: where the draw sits in the frame, the if (_scene == SceneKind.Forest) gate on
        //it, and the sky-lit enrolment below.
        private ForestScatterRenderer _forestScatter;

        //The scene's own point lights (the neon city's ring of magenta and cyan around the island, the
        //savanna's campfire, space's planetshine) pushed onto the shared instanced effect each frame, so the
        //balls, the island, the gun and the city all take them on top of the sun and the dome. The slots, the
        //three arrays and the change gate are SceneLights' own since #75 — this and the Testbed held a copy
        //each. The neon ring's figures live in _cityConfig.NeonLook.
        private SceneLights _sceneLights;

        //What the cluster hangs from: a translucent glass plate over the play field, and CeilingPlate's since
        //#75 (it stood line for line here and in the Testbed). The MESH AND RENDERER are the host's — the glass
        //is lit with the rest of the scene, and SkyLitRenderers has to reach it — while the kinematic BODY it is
        //drawn from belongs to the session (GameplayScreen), which is the split the issue warned about. Fitted
        //per level at the field's footprint, so before the first level its Renderer is null and SkyLitRenderers
        //tolerates that.
        private CeilingPlate _ceilingPlate;

        /// <summary>
        /// The glass plate's renderer, or null before the first level is installed: the session reaches through
        /// it for <see cref="InstancedModelRenderer.EmissiveTint"/> — how the glass flashes on the frame the
        /// ceiling steps down — and draws it from its own kinematic body's pose.
        /// </summary>
        internal InstancedModelRenderer CeilingRenderer => _ceilingPlate.Renderer;

        #endregion

        #region Balls (the render set; the balls themselves are the session's)

        //Everything it takes to draw a ball is BallRenderSet's since #76: the three procedural sphere LODs and
        //the distances they are picked by, the three renderers built on them, every figure of the look (the
        //albedo, the five-gore beach pattern, the emission and translucency, the heartbeat's rate, depth,
        //direction and wavelength, the ripple's strength) and the (type × LOD) instance buckets each of which
        //becomes one instanced draw call. It stood here, in the Testbed's LoadContent and in the map editor,
        //with the bucket bookkeeping written a fourth time inside the session's own collect walk. Every one of
        //those figures kept its value and its reasoning; the pulse depth is now picked BY the ripple flag,
        //which is what keeps the breath from being left loud enough to drown the wave out.
        private BallRenderSet _balls;

        /// <summary>
        /// The ball meshes, renderers and instance buckets — content, like the barrel, so it is built once with
        /// the device up and outlives every session. The session opens one frame of collection on it per draw
        /// (<see cref="BallRenderSet.BeginFrame"/>) and hands it every ball there is.
        /// </summary>
        internal BallRenderSet Balls => _balls;

        #endregion

        #region The gun's hardware (the barrel; the gun's pose and magazine are the session's)

        //The procedural barrel and the renderer that draws it, with every figure the tube is cut to — all of it
        //CannonRig's since #76, shared with the Testbed. It outlives a session because it is content: the mesh
        //and the instance buffer are built once with the device up and disposed on the way out.
        private CannonRig _cannonRig;

        /// <summary>The barrel, for the session to draw with its own pose and to size the queue's place in the
        /// bore off (<see cref="CannonRig.PivotToFrontBall"/>).</summary>
        internal CannonRig CannonRig => _cannonRig;

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

        /// <summary>
        /// The bottom of the stack, exposed for its <b>orbit</b> alone: the result screen flies the camera out
        /// onto it when a level ends, so the two share one orbit and one angle rather than each keeping its
        /// own — see <see cref="BackdropScreen.AdvanceOrbit"/>.
        /// </summary>
        internal BackdropScreen Backdrop => _backdrop;

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

        //Anton and Inter (both SIL OFL), through FontStashSharp. Myra's embedded stylesheet carries a small
        //bitmap font that is fine for a tool panel and much too coarse for a game's title, so the menu brings
        //its own; they are embedded in the assembly, so there is no path to get wrong and nothing to install.
        //Each size is a separate rasterized atlas, so they are resolved once at load rather than per label.
        //
        //TWO TYPEFACES BY ROLE, not by weight. Anton is a display face — condensed, black, one weight — and it
        //is what everything meant to be loud is set in: the title, the menu entries and the HUD. It is the
        //shippable form of the Impact look (Impact itself is bundled with Windows and not redistributable,
        //which is the same rule that keeps Segoe UI out of this assembly). Inter stays for the small print —
        //About's paragraphs, a level's rules, the adaptive-quality note — because a display face is drawn for
        //headlines and turns to mush at body sizes, which is exactly where those live.
        //
        //Three systems, not three fonts in one: FontStashSharp falls back through a system's fonts glyph by
        //glyph, so a second face added beside the first would never be reached — the first has every glyph.
        //Picking a face means picking a system.
        private FontSystem _menuFontSystem, _menuFontSystemBold, _menuFontSystemDisplay;
        private SpriteFontBase _menuFontBody, _menuFontSmall, _menuFontHeading, _menuFontTitle;

        //The menu is deliberately GREYSCALE — no hue anywhere, and no coloured frames. It has to sit over
        //eleven backdrops whose palettes are nothing alike (a neon city, an ochre desert, a blue sea, white
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

        /// <summary>
        /// Whether the tier was fixed by the player (the command line or the Settings row) rather than reached by
        /// the probe. A player-fixed tier is the player's decision and is never re-measured; a probe-reached one
        /// is only this machine's answer for <i>this</i> back-buffer size, so a fullscreen switch — which moves
        /// the back buffer from 1600×900 to the display's native resolution and back, a fill-rate change of
        /// several times — re-opens it (see <see cref="ToggleFullscreen"/>).
        /// </summary>
        private bool _qualityPinnedByPlayer;

        private float _qualityWarmupLeft = QUALITY_WARMUP_SECONDS;
        private float _qualityWindowSeconds;
        private int _qualityWindowFrames;

        /// <summary>
        /// The frame rate below which the probe spends image quality. Derived from the display's refresh rather
        /// than fixed, so the target tracks the monitor the player is actually on: a 75 Hz panel wants ~75, a
        /// 60 Hz one ~60, a 144 Hz one ~144. The probe settles when the machine reaches <see cref="QUALITY_REFRESH_FRACTION"/>
        /// of it, not when it merely clears 45 — the old fixed floor was tuned to a 60 Hz laptop and left a fast
        /// card on a 75 Hz panel pinned to High at 37 FPS because 37 was never going to clear a verdict it was
        /// never measured against (a windowed run had settled the latch first).
        /// </summary>
        private float _qualityMinFps = DEFAULT_QUALITY_MIN_FPS;

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
        /// Below this, the frame rate is judged bad enough to be worth spending image quality on. Derived from
        /// the display's refresh at startup (see <see cref="SetQualityMinFpsFromRefresh"/>), so it tracks the
        /// monitor the player is actually on rather than a single fixed floor. Comfortably under that refresh,
        /// so a vsync-capped machine (the normal case) never trips it — 75 Hz reads as 75, not as "only just
        /// enough".
        /// </summary>
        private const float DEFAULT_QUALITY_MIN_FPS = 45f;

        /// <summary>
        /// The probe asks for this fraction of the display's refresh. The floor is the refresh <i>minus</i> this
        /// margin: 75 Hz → 67.5, 60 Hz → 54, 144 Hz → 129.6. The margin keeps a vsync-capped machine from
        /// tripping the probe on the rounding of its own cap.
        /// </summary>
        private const float QUALITY_REFRESH_MARGIN = 0.1f;

        /// <summary>
        /// A sanity floor on the refresh-derived target, for an adapter that reports nothing sensible (headless,
        /// a remote session). The number is the old fixed floor — below any common refresh — so behaviour there
        /// is unchanged.
        /// </summary>
        private const float QUALITY_MIN_FPS_FLOOR = 45f;

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

        //The ladder the three volume rows walk: quarters from the authored mix down to silence, then back to
        //full. 100 % is the mix as tuned (the BASE/MUSIC/FANFARE constants in ProceduralAudio and
        //ProceduralMusic), and the player's gains only ever scale it — so retuning the mix never invalidates
        //what a setting means.
        private const float VOLUME_STEP = 0.25f;

        //1 is the authored mix; the "mute" argument starts the master at 0 (see the constructor).
        private float _masterVolume = 1f;
        private float _sfxVolume = 1f;
        private float _musicVolume = 1f;
        private float _ambienceVolume = 1f;

        #endregion

        #region The in-play HUD's fonts

        //The HUD is the GameplayScreen's, but its type is the frame's — the same display face the menu's own
        //loud type is set in, resolved per viewport height exactly as the menu's sizes are. Separate from EnsureMenuLayout,
        //which only runs while a menu is up, and quantized the same way so a window being dragged does not
        //ask the font system for a new atlas every frame.
        //Authored large on purpose. A HUD in a game is not a data readout — the score and the ball count are
        //two of the three things the player is actually tracking, and they have to land at a glance while the
        //eye is in the middle of the frame on the cluster; the corners are empty in both poses, so size costs
        //nothing here. HUD_FONT_SCORE leaves headroom for the pulse: the score is pivoted on its vertical
        //centre at HUD_MARGIN + height/2, so at PlayHud's clamped pulse ceiling it must still clear the frame.
        private const int HUD_FONT_SCORE = 140;
        private const int HUD_FONT_LABEL = 76;
        //The awards are read at a glance out on the cluster and then shrink as they fly to the corner, so they
        //are authored larger still than the score they land on
        private const int HUD_FONT_POPUP = 112;

        //The balls-left alarm's first step, which used to be a heavier weight and cannot be: the display face
        //has ONE weight, so a bold slot would resolve to the very same glyphs and the step would vanish in
        //silence — a documented escalation quietly reduced from three steps to two. It is a size step instead.
        //Kept modest: it has to read as the number leaning in, not as a second number, and the count is
        //pivoted on its bottom-left corner (see PlayHud.DrawBallsLeft), so growing it cannot push it off frame
        //the way the centre-pivoted score would be.
        private const float HUD_LOW_EMPHASIS = 1.14f;

        private SpriteFontBase _hudFontScore, _hudFontLabel, _hudFontPopup;
        private SpriteFontBase _hudFontScoreLoud, _hudFontLabelLoud;
        private int _hudFontsForHeight = -1;

        internal SpriteFontBase HudFontScore => _hudFontScore;
        internal SpriteFontBase HudFontLabel => _hudFontLabel;
        internal SpriteFontBase HudFontPopup => _hudFontPopup;

        /// <summary>The balls-left readout once the budget is low — the same face a step larger. See <see cref="HUD_LOW_EMPHASIS"/>.</summary>
        internal SpriteFontBase HudFontScoreLoud => _hudFontScoreLoud;

        /// <summary>The caption under a low balls-left readout, grown to match <see cref="HudFontScoreLoud"/>.</summary>
        internal SpriteFontBase HudFontLabelLoud => _hudFontLabelLoud;

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

            _hudFontScore = _menuFontSystemDisplay.GetFont(Scaled(HUD_FONT_SCORE));
            _hudFontLabel = _menuFontSystemDisplay.GetFont(Scaled(HUD_FONT_LABEL));
            _hudFontPopup = _menuFontSystemDisplay.GetFont(Scaled(HUD_FONT_POPUP));
            _hudFontScoreLoud = _menuFontSystemDisplay.GetFont(Scaled((int)(HUD_FONT_SCORE * HUD_LOW_EMPHASIS)));
            _hudFontLabelLoud = _menuFontSystemDisplay.GetFont(Scaled((int)(HUD_FONT_LABEL * HUD_LOW_EMPHASIS)));
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
        /// <param name="celebrate">
        /// Testing only (the <c>celebrate</c> argument): fire the victory display on the front end. Clearing a
        /// level is the only thing that normally starts it and clearing one cannot be scripted, so this is how
        /// the fireworks get screenshotted and measured at all.
        /// </param>
        /// <param name="lasers">
        /// Testing only (the <c>lasers</c> argument): pin the floor alarm's laser net on while a level is
        /// being played — see <see cref="ForceLaserWarning"/>.
        /// </param>
        /// <param name="mute">
        /// Testing only (the <c>mute</c> argument): start with the master volume at zero — a scripted
        /// screenshot or benchmark run has no business making noise. The settings rows can still raise it.
        /// </param>
        public BS3DGame(bool fullscreen = false, int? supersampleFactor = null, float exposure = DEFAULT_EXPOSURE,
            bool uncappedFps = false, SceneKind? scene = null, byte? skyDome = null, bool logFrameRate = false,
            QualityLevel? quality = null, bool celebrate = false, bool lasers = false, bool mute = false)
        {
            _fullscreen = fullscreen;
            _startupCelebrate = celebrate;
            _startupLasers = lasers;
            if (mute) _masterVolume = 0f;

            //The tier owns supersampling, so the tier's factor is taken first and an explicit ssaa= then
            //overrides that one entry of it — the expert override the benchmark and the screenshot harness use.
            //The rest of the tier is applied in LoadContent, once the city it also sizes exists.
            if (quality.HasValue) _quality = quality.Value;
            _supersampleFactor = QualityPreset.Presets[(int)_quality].SupersampleFactor;

            if (supersampleFactor.HasValue) _supersampleFactor = Math.Clamp(supersampleFactor.Value, 1, 4);

            _startupScene = scene;
            _startupSkyDome = skyDome;
            _logFrameRate = logFrameRate;

            //Either one is the player's decision and settles the question for good; with neither, the adaptive
            //path is free to measure this machine and step the tier down. The distinction matters on a fullscreen
            //switch: a player-pinned tier stays put, a probe-reached one is only this machine's answer for this
            //back-buffer size and gets re-measured (see ToggleFullscreen).
            _qualityPinnedByPlayer = supersampleFactor.HasValue || quality.HasValue;
            _qualitySettled = _qualityPinnedByPlayer;
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

            //The probe's frame-rate floor follows the refresh of the monitor the window is on, so a 75 Hz panel
            //asks for ~75 and a 60 Hz one ~60 rather than the same fixed floor for both. Re-derived on every
            //fullscreen switch, and the constructor call resolves properly too — MonoGame has built the window
            //by the time this runs, measured (see TryGetWindowDisplayRefresh).
            SetQualityMinFpsFromRefresh();

            _graphics.PreferredBackBufferWidth = _fullscreen ? display.Width : WINDOW_WIDTH;
            _graphics.PreferredBackBufferHeight = _fullscreen ? display.Height : WINDOW_HEIGHT;
            _graphics.IsFullScreen = _fullscreen;
            _graphics.SynchronizeWithVerticalRetrace = !_uncappedFps;

            _graphics.ApplyChanges();

            //Null-conditional for the constructor's call, which runs before LoadContent has built the
            //pipeline (the old in-class EnsureSceneTarget guarded on GraphicsDevice == null the same way)
            _pipeline?.EnsureTarget();

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

        /// <summary>
        /// Sets the probe's frame-rate floor from the display's refresh, less <see cref="QUALITY_REFRESH_MARGIN"/>
        /// so a vsync-capped machine does not trip it on the rounding of its own cap. The floor is clamped to
        /// <see cref="QUALITY_MIN_FPS_FLOOR"/> so a headless or remote adapter that reports no refresh keeps the
        /// old fixed number rather than settling at zero (which would make every run "fast enough" instantly).
        /// </summary>
        private void SetQualityMinFpsFromRefresh()
        {
            //MonoGame's DisplayMode carries no refresh rate (XNA dropped it, and the DesktopGL/WindowsDX adapter
            //never re-added one), so the floor would otherwise be a single fixed number for a 60 Hz laptop and a
            //75 Hz panel alike. user32's EnumDisplaySettings is the same call Win32_VideoController answers to.
            //It has to be asked about a NAMED display device: passing null asks for the primary one, which is
            //not the same question on any machine with two monitors (#81). The struct is laid out by explicit
            //offset rather than marshalled field-by-field: only dmSize (set so the call accepts the buffer) and
            //dmDisplayFrequency are read, which keeps it to two pinned, stable Win2000-onwards offsets.
            float refresh = 0f;
            if (TryGetWindowDisplayRefresh(out int hz)) refresh = hz;
            _qualityMinFps = Math.Max(refresh * (1f - QUALITY_REFRESH_MARGIN), QUALITY_MIN_FPS_FLOOR);
        }

        //The two DEVMODEW fields this reads. The full struct is ~220 bytes and differs across Windows versions
        //only in the trailing private/registry fields, so a buffer sized off the current value of dmSize is
        //always enough; the offsets below are fixed by the public part of the struct and have not moved.
        private const int ENUM_CURRENT_SETTINGS = unchecked((int)0xFFFFFFFF);

        [StructLayout(LayoutKind.Explicit, Size = 220)]
        private struct DEVMODE
        {
            [FieldOffset(68)] public ushort dmSize;
            [FieldOffset(184)] public uint dmDisplayFrequency;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        //Which monitor the window is actually on. MONITOR_DEFAULTTONEAREST rather than the NULL variants because
        //a window is always somewhere: dragged half off the desktop, or onto a monitor that has just been
        //unplugged, "nearest" is the honest answer and never fails.
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        //szDevice is the whole point of the EX variant — the \\.\DISPLAYn name EnumDisplaySettings wants. The
        //rects and flags are read by nobody here; they are declared because the struct is passed by value and
        //cbSize has to match what GetMonitorInfo expects.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MONITORINFOEX
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX monitorInfo);

        /// <summary>
        /// Reads the current refresh rate of the monitor <b>this window is on</b>, in Hz. False on any failure.
        /// <para>
        /// It used to pass <c>null</c> to <c>EnumDisplaySettings</c>, which asks for the <b>primary</b> display
        /// device and not the window's — and the comments claimed otherwise, which is how it survived (#81). On a
        /// mixed multi-monitor desktop that read the wrong panel in whichever direction the pair happened to be
        /// arranged: a window on a 144 Hz secondary beside a 60 Hz primary took a floor from 60, and so tolerated
        /// a frame rate its own monitor shows as visible jank — the exact failure the refresh-derived floor was
        /// introduced to stop. Reversed, a 60 Hz window was held to a 144 Hz floor and stepped quality down for
        /// no gain the player could see.
        /// </para>
        /// <para>
        /// Falling back to the primary display when the window cannot be resolved is the old behaviour kept as a
        /// guard, not a path known to be taken: a floor derived from the wrong panel still beats none, since a
        /// <c>refresh</c> of 0 clamps to <see cref="QUALITY_MIN_FPS_FLOOR"/> and throws the panel's own rate away
        /// entirely. It is deliberately not leaning on MonoGame's construction order — though as it happens the
        /// window <i>is</i> already built when <see cref="SetGraphics"/> runs from the constructor, which was
        /// measured rather than assumed, so in practice even that first call reads the right monitor.
        /// </para>
        /// </summary>
        private bool TryGetWindowDisplayRefresh(out int refreshHz)
        {
            refreshHz = 0;

            //Null means "the primary display" to EnumDisplaySettings, which is the fallback described above
            string device = null;
            IntPtr windowHandle = Window?.Handle ?? IntPtr.Zero;

            if (windowHandle != IntPtr.Zero)
            {
                IntPtr monitor = MonitorFromWindow(windowHandle, MONITOR_DEFAULTTONEAREST);

                if (monitor != IntPtr.Zero)
                {
                    MONITORINFOEX info = default;
                    info.cbSize = (uint)Marshal.SizeOf<MONITORINFOEX>();

                    if (GetMonitorInfo(monitor, ref info)) device = info.szDevice;
                }
            }

            DEVMODE dm = default;
            dm.dmSize = (ushort)Marshal.SizeOf<DEVMODE>();
            if (!EnumDisplaySettings(device, ENUM_CURRENT_SETTINGS, ref dm)) return false;
            //0 or 1 are what Windows reports for a projector/TV that did not declare a refresh, and 5 is a
            //placeholder for "default" — none is a real panel rate, so treat them as "no answer".
            if (dm.dmDisplayFrequency < 10) return false;
            refreshHz = (int)dm.dmDisplayFrequency;
            return true;
        }

        private void OnClientSizeChanged()
        {
            UpdateCameraAspect();

            //A window that changed size may also have changed monitor, and the probe's floor is derived from the
            //refresh of the one it is on (#81). This is the cheap hook rather than the complete one: a window
            //DRAGGED between two monitors of the same size raises no resize, so its floor stays on the old
            //panel's rate until the next resize or fullscreen switch. Tracking the move itself would want a
            //WM_DISPLAYCHANGE/position hook into the WinForms host, which is a lot of surface for a case that
            //corrects itself the moment anything else about the window changes.
            SetQualityMinFpsFromRefresh();

            //The overlay is authored for 2160p and scaled to the viewport, so a resize has to re-derive it.
            //The menu is authored the same way, and refits itself in Draw (EnsureMenuLayout).
            _info?.RecomputeScale();

            _pipeline?.EnsureTarget();
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

            //After base.Initialize, so the window's handle exists and MonoGame's own icon assignment has
            //already been published — this has to win that race, not lose it.
            ApplyWindowIcon();
        }

        //The two icon handles the window is publishing, extracted once from the executable's own icon group.
        //They are deliberately never destroyed: Windows draws from them for as long as the window lives, so
        //they are freed when the process is, and re-extracting them per resize would be work for nothing.
        private IntPtr _windowIconBig;
        private IntPtr _windowIconSmall;

        /// <summary>
        /// Publishes the executable's own icon on the window at the two sizes Windows actually draws — the
        /// large one for ALT+TAB and the taskbar, the small one for the title bar — each taken from the
        /// authored frame of that size rather than resampled from another.
        /// </summary>
        /// <remarks>
        /// MonoGame does set an icon itself, and it is not enough. <c>WinFormsGameWindow.SetIcon</c> calls
        /// shell32's <c>ExtractIcon</c>, which hands back a <b>single</b> handle at the system large-icon size
        /// (measured: 32×32 at 96 DPI), and assigns it through <c>Icon.FromHandle</c> — which keeps no icon-file
        /// bytes. So WinForms' small-icon path has no frames to choose from and can only copy that one image:
        /// measured on the live game, the window published ICON_SMALL as a <b>32×32</b>, i.e. the title bar was
        /// squeezing the 32 px artwork into 16 px while the authored <c>ico6-16.png</c> sat unused in the exe.
        /// <para>
        /// <c>PrivateExtractIcons</c> is the API that picks a named size out of a group, so the frames come
        /// straight out of the running executable's resources — no second copy of the icon embedded as content,
        /// nothing to keep in step. It reads <see cref="Environment.ProcessPath"/> rather than
        /// <c>Assembly.Location</c>, which is what MonoGame uses and what comes back <i>empty</i> under a
        /// single-file publish (where MonoGame's own SetIcon silently does nothing and this becomes the only
        /// thing setting the window icon at all).
        /// </para>
        /// <para>
        /// The order below is mandatory: assigning <c>Form.Icon</c> makes WinForms send <i>both</i> messages,
        /// deriving the small one by stretching the large, so the authored small frame has to be sent after it.
        /// </para>
        /// </remarks>
        private void ApplyWindowIcon()
        {
            //Nothing here is worth a crash on a machine whose shell answers differently — a stock icon is a
            //cosmetic loss, and every step below can fail independently.
            try
            {
                if (System.Windows.Forms.Control.FromHandle(Window.Handle) is not System.Windows.Forms.Form form) return;

                if (_windowIconBig == IntPtr.Zero && _windowIconSmall == IntPtr.Zero)
                {
                    string module = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(module)) return;

                    _windowIconBig = ExtractIconFrame(module, GetSystemMetrics(SM_CXICON), GetSystemMetrics(SM_CYICON));
                    _windowIconSmall = ExtractIconFrame(module, GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON));
                }

                if (_windowIconBig != IntPtr.Zero) form.Icon = System.Drawing.Icon.FromHandle(_windowIconBig);
                if (_windowIconSmall != IntPtr.Zero) SendMessage(form.Handle, WM_SETICON, ICON_SMALL, _windowIconSmall);
            }
            catch (Exception)
            {
                //Leaves whatever MonoGame published standing.
            }
        }

        private const int SM_CXICON = 11;
        private const int SM_CYICON = 12;
        private const int SM_CXSMICON = 49;
        private const int SM_CYSMICON = 50;

        private const uint WM_SETICON = 0x0080;
        private static readonly IntPtr ICON_SMALL = IntPtr.Zero;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int PrivateExtractIcons(string fileName, int iconIndex, int cx, int cy,
            IntPtr[] icons, int[] iconIds, int iconCount, int flags);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// The one frame of <paramref name="module"/>'s first icon group closest to the requested size, or
        /// <see cref="IntPtr.Zero"/>. Zero rather than a throw for a missing group: an executable built without
        /// an <c>ApplicationIcon</c> has none, and that is not an error worth a stack trace.
        /// </summary>
        private static IntPtr ExtractIconFrame(string module, int width, int height)
        {
            IntPtr[] icons = new IntPtr[1];
            int[] iconIds = new int[1];

            //Returns the count extracted, or -1 on failure — so anything but exactly one is "no icon".
            return PrivateExtractIcons(module, 0, width, height, icons, iconIds, 1, 0) == 1 ? icons[0] : IntPtr.Zero;
        }

        protected override void LoadContent()
        {
            _instancingEffect = Content.Load<Effect>("Shaders/InstancedModel");

            //The pipeline caches its parameters and sets each look value exactly once through the required
            //initializer — the aberration/grain toggles and the exposure ladder write the same properties
            //later, and everything else about the resolve lives in the class (see #74)
            _pipeline = new PostProcessPipeline(GraphicsDevice,
                Content.Load<Effect>("Shaders/Tonemap"), Content.Load<Effect>("Shaders/Glare"))
            {
                GlareThreshold = GLARE_THRESHOLD,
                GlareIntensity = GLARE_INTENSITY,
                Exposure = _exposure,
                ChromaticAberration = _aberration ? CHROMATIC_ABERRATION : 0f,
                FilmGrain = _grain ? FILM_GRAIN : 0f,
                SupersampleFactor = _supersampleFactor,
            };

            //Off the shared instanced effect, because one push of the scene's lights has to reach the balls, the
            //island, the gun and the city alike. It caches its four parameter references here, for the same
            //per-frame reason it always did: the campfire flickers, so this is not a set-once.
            _sceneLights = new SceneLights(_instancingEffect);

            //Nothing is built here — the plate's footprint is the loaded field's, so the first Fit is a level's
            _ceilingPlate = new CeilingPlate(GraphicsDevice, _instancingEffect);

            //The overlay's own batch, shared by the HUD and the crosshair the session draws into it
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            #region Balls

            //Off the same shared instanced effect as everything else in the scene, which is why it is handed in
            //rather than loaded: the effect stays the content manager's. ripples: true is this executable alone
            //— the Testbed and the map editor run no wave through their cluster — and it both switches the
            //shader's ripple term on and picks the shallower resting breath that keeps the wave visible over
            //it. The ground the balls' bellies darken against is the island's own top, which is the set's
            //default and the one thing every ball in this game hangs over.
            _balls = new BallRenderSet(GraphicsDevice, _instancingEffect, ripples: true);

            #endregion

            #region The gun

            //The bore is cut to the loaded queue: the rig derives the tube's length, its two lips and the
            //pivot-to-front-ball distance from the queue's size and spacing, which are Magazine's own figures,
            //so the barrel that is built and the muzzle a shot leaves from cannot disagree. The instancing
            //effect is handed in and stays the content manager's — the rig disposes its mesh and renderer only.
            _cannonRig = new CannonRig(GraphicsDevice, _instancingEffect, Magazine.SIZE, Magazine.SPACING);

            #endregion

            //The five self-lit backdrops, shared with the Testbed and the map editor — one copy of every
            //scene shader, built out of the Testbed's content directory. The hole radius is fixed (the island
            //never moves or resizes here), so it is set once rather than per frame.
            _sceneRenderer = new SceneRenderer(GraphicsDevice, Content)
            {
                TerrainHoleRadius = ArenaIsland.TERRAIN_HOLE_RADIUS,
                SupersampleFactor = _supersampleFactor
            };

            //After the scene renderer, which the rig consults for the scenes that state their own lighting. The
            //cloud hook is captured ONCE here rather than per frame: a method group written at the call site
            //builds a fresh delegate every time it is evaluated, and this one used to be evaluated in Draw.
            _rig = new SkyLightRig(_sceneRenderer) { CloudHook = _clouds.ApplyTo };

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

            //Everything about the clouds that does not change frame to frame, pushed once. The per-frame half —
            //the clock and the camera — goes out in BeginSceneDraw, right before the dome; the two dome-derived
            //colours follow the dome and are ApplySkyLighting's business.
            _clouds.ApplyStaticParameters(_skyEffect, _instancingEffect, SkyLightRig.SUN_DIRECTION);

            //A different one of the eleven every launch, so the front end is not the same picture twice — unless
            //the command line pinned one. It also sets the dome and the city's lighting, and ends in
            //ApplySkyLighting, which is why nothing derives the light rig before this point.
            SetScene(_startupScene ?? (SceneKind)RANDOM.Next(SceneRenderer.SceneCount));

            //After SetScene and never before: the sea and the savanna each replace the dome with one of their
            //own, so an explicit sky= would be silently overridden the other way round. The Testbed's rule.
            if (_startupSkyDome.HasValue) SetSkyDome(_startupSkyDome.Value);

            //The rest of the tier, now that the city it also sizes exists. The constructor could only take the
            //supersample factor, since the target and the city are both built here. At the default High this
            //writes the config's own defaults back over themselves and rebuilds nothing.
            ApplyQuality(_quality);

            _pipeline.EnsureTarget();

            //The order the levels are played in, read once here: a broken set is reported at startup rather
            //than at the moment the player presses Play, and the maps themselves are only parsed per level.
            LoadLevelSet();

            Console.WriteLine($"[game] {_city.Buildings.Length} buildings, scene {_scene}, dome {_skyDome}");

            //The two non-page screens. The gameplay screen loads its own content (the shot-trail effect), so
            //it is made here with the device up; the backdrop is the scene-only frame the menus stand over.
            _backdrop = new BackdropScreen(this);
            _gameplayScreen = new GameplayScreen(this);

            //The SFX are synthesized from raw PCM here, once, so the per-event paths only ever play a buffer —
            //no asset files, no pipeline step.
            _audio = new ProceduralAudio();

            //The victory display. Its one static buffer is built here too, so a cleared level costs nothing
            //but a handful of uniforms.
            _fireworks = new Fireworks(GraphicsDevice, Content.Load<Effect>("Shaders/Fireworks"), _audio);

            //Testing only, and deliberately long: it has to outlast a scripted screenshot burst.
            if (_startupCelebrate) _fireworks.Celebrate(90f);

            //The level theme. The constructor only starts the synthesis — two minutes of PCM is a couple of
            //seconds of arithmetic, and it runs on a background thread while the player is still looking at
            //the splash and the menu (see ProceduralMusic).
            _music = new ProceduralMusic();

            //The scene beds. The scene was picked before the audio existed (SetScene runs early in
            //LoadContent, and its _ambience hook is null-conditional for exactly that), so the pick is
            //handed over here; every later change reaches it through SetScene like everything else scenic.
            _ambience = new ProceduralAmbience();
            _ambience.SetScene(_scene);

            //A muted start (the mute argument) has to reach the freshly made subsystems; every later change
            //comes through the settings rows.
            ApplyVolumes();

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

            //Anton and Inter (both SIL OFL 1.1), embedded in the assembly so there is no path to get wrong and
            //nothing to install. Myra's own stylesheet carries a small bitmap font, which is fine for a tool
            //panel and far too coarse for a title. Each size is rasterized into its own atlas by GetFont, so
            //they are resolved once here rather than per label.
            _menuFontSystemDisplay = LoadEmbeddedFont("BS3D.Content.Fonts.Anton-Regular.ttf");
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
            //
            //Split by role, not by weight: the title, the entries a player picks from and a result's headings
            //are the loud type and are set in the display face; SMALL stays in Inter, because it is the size
            //About's paragraphs and a level's rules are read at and a condensed display face closes up there.
            _menuFontSmall = _menuFontSystem.GetFont(Scaled(MENU_FONT_SMALL));
            _menuFontBody = _menuFontSystemDisplay.GetFont(Scaled(MENU_FONT_BODY));
            _menuFontHeading = _menuFontSystemDisplay.GetFont(Scaled(MENU_FONT_HEADING));
            _menuFontTitle = _menuFontSystemDisplay.GetFont(Scaled(MENU_FONT_TITLE));

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
        internal float MasterVolume => _masterVolume;
        internal float SfxVolume => _sfxVolume;
        internal float MusicVolume => _musicVolume;
        internal float AmbienceVolume => _ambienceVolume;
        internal bool IsAberrationEnabled => _aberration;
        internal bool IsGrainEnabled => _grain;
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

            //After the guard, so a screen with no back stays silent as well as still.
            _audio.PlayUiBack();

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

            //Only user input reaches here — a screen change restores the cursor in CollectNavEntries by
            //assignment, deliberately, so arriving on a page does not tick.
            _audio.PlayUiTick();

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

            _pipeline.Exposure = _exposure;

            _settingsPage.Refresh();
        }

        internal void CycleSkyDome()
        {
            SetSkyDome((byte)(_skyDome == SKY_DOME_COUNT ? 1 : _skyDome + 1));
            _settingsPage.Refresh();
        }

        //The three volume rows (#46). Each steps its own gain and takes effect where it is made — the music
        //keeps playing under the settings page, so what the row does is heard as it is clicked.
        internal void CycleMasterVolume()
        {
            _masterVolume = NextVolume(_masterVolume);
            ApplyVolumes();
            _settingsPage.Refresh();
        }

        internal void CycleSfxVolume()
        {
            _sfxVolume = NextVolume(_sfxVolume);
            ApplyVolumes();
            _settingsPage.Refresh();
        }

        internal void CycleMusicVolume()
        {
            _musicVolume = NextVolume(_musicVolume);
            ApplyVolumes();
            _settingsPage.Refresh();
        }

        internal void CycleAmbienceVolume()
        {
            _ambienceVolume = NextVolume(_ambienceVolume);
            ApplyVolumes();
            _settingsPage.Refresh();
        }

        /// <summary>
        /// Steps a volume down a quarter and wraps back to full under zero. Downwards, unlike the exposure
        /// ladder, because the reason to click a volume row at all is almost always "quieter" — upwards, the
        /// first click would be a jump to silence. The epsilon is the exposure wrap's: a mute's 0 sits on the
        /// ladder already, so only a step below it wraps.
        /// </summary>
        private static float NextVolume(float current)
        {
            float next = current - VOLUME_STEP;
            return next < -Constants.THOUSANDTH ? 1f : Math.Max(next, 0f);
        }

        /// <summary>
        /// The one place the player's gains reach the audio: effects and music each take master times their
        /// own row, so the two subsystems cannot disagree about what the master row means.
        /// </summary>
        private void ApplyVolumes()
        {
            _audio.Gain = _masterVolume * _sfxVolume;
            _music.Gain = _masterVolume * _musicVolume;

            //The beds have a row of their own: how much atmosphere sits under the music is a taste, and
            //chaining it to the effects would turn the shot down with it.
            _ambience.Gain = _masterVolume * _ambienceVolume;
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

            //One wrapper for every input device (#46): the mouse reaches it through Myra's Click, the pad and
            //the arrow keys through the Tag that ActivateNavEntry invokes — so the press sounds once wherever
            //it came from, and an entry added later cannot forget its click.
            Action pressed = () =>
            {
                _audio.PlayUiClick();
                onClick();
            };

            button.Click += (_, _) => pressed();

            //The pad and the arrow keys find their entries by walking the widget tree, which has no way back
            //to this delegate — so it rides on the widget itself. See ActivateNavEntry.
            button.Tag = pressed;

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
            _city = new City(seed: CITY_SEED, arenaHalfExtent: ArenaIsland.RADIUS, config: _cityConfig);

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

            //The arena the gun stands on, all of it: the island's stone cap and concrete drum, the glass drain
            //bored through the middle, its two gold beads and the dark pit shaft that backs the glass where the
            //terrain has the island's footprint cut out of it. Meshes, procedural textures, renderers and the
            //one world matrix are the component's; the ambient is the scene's, so it is handed in.
            _island = new ArenaIsland(GraphicsDevice, _instancingEffect, SCENE_AMBIENT_INTENSITY);

            //The forest's scattered trees, rocks and stumps, all of it: both procedural textures, the fifteen
            //mesh variants, the twenty-five renderers with their bark, foliage, stone and sawn-wood dressing,
            //the matte materials and the tints encoded once from the config. The config is the SceneRenderer's
            //own instance and is read at build time only, which is all this executable ever needs — nothing
            //here edits a scene config at runtime, so there is no Replant call in the Game (the map editor's
            //live grid is the one caller that has to make it). No stone texture handed in: the component builds
            //its own, since ArenaIsland's is that component's private business. The ambient is the scene's, so
            //it is handed over as it is to the island.
            _forestScatter = new ForestScatterRenderer(GraphicsDevice, _instancingEffect,
                (ForestSceneConfig)_sceneRenderer.GetSceneConfig(SceneKind.Forest), SCENE_AMBIENT_INTENSITY);

            //Note the glass the cluster hangs from is NOT built here: its footprint is the loaded level's
            //field, so RebuildCeilingRenderer fits it (and refits it on every level) — which is why
            //SkyLitRenderers tolerates a null ceiling renderer and ApplySkyLighting runs again after a load.
        }

        /// <summary>
        /// Refits the drawn glass plate to the loaded field's footprint, called by the session as it installs a
        /// level. The renderer is recreated, so it starts without the sky palette —
        /// <see cref="ApplySkyLighting"/> has to run after this, exactly as it does after the Testbed's
        /// <c>FitCeilingToMap</c>. The margin, the thickness and the glass itself are
        /// <see cref="CeilingPlate"/>'s; the plate is drawn from the session's kinematic body's own pose, so
        /// the glass and the collidable cannot disagree.
        /// </summary>
        internal void RebuildCeilingRenderer(float stageSizeX, float stageSizeZ) =>
            _ceilingPlate.Fit(stageSizeX, stageSizeZ);

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
        /// missing: it is refitted at each level's footprint, so before the first level is installed — and for
        /// the moment inside a load when the old one has gone — there is none.
        /// </summary>
        private IEnumerable<InstancedModelRenderer> SkyLitRenderers()
        {
            //Sky-lit enrolment is the one thing BallRenderSet exposes its renderers for, and deliberately so:
            //which renderers take part is each executable's own list with its own reasons, which is why this
            //walk is here and not in the component.
            foreach (InstancedModelRenderer ballRenderer in _balls.Renderers) yield return ballRenderer;

            yield return _cannonRig.Renderer;
            yield return _cityRenderer;

            //The island's stone cap and concrete drum, the drain's glass and its two gold beads — but
            //deliberately not its pit shaft, which is a hole in the ground no dome may bleach. Dereferenced
            //unconditionally, unlike the ceiling below: BuildScene makes the island and runs in LoadContent
            //BEFORE the startup SetScene, which is what first calls ApplySkyLighting, and every other way here
            //(the scene page, the sky setting, a level's own dome, the session after a refit) is later still.
            //A guard would be reassurance about something that cannot happen.
            foreach (InstancedModelRenderer renderer in _island.SkyLitRenderers) yield return renderer;

            //The forest scatter, present only in the forest scene but always built. Every variant of every kind
            //takes part — the one flat array the component exposes for exactly this — or a spruce of the variant
            //this missed would stand under the light rig of whatever dome was up when it was made. Dereferenced
            //unconditionally like the island's, and for the same reason: BuildScene makes it well before the
            //startup SetScene, which is what first calls ApplySkyLighting.
            foreach (InstancedModelRenderer renderer in _forestScatter.Renderers) yield return renderer;

            if (_ceilingPlate.Renderer != null) yield return _ceilingPlate.Renderer;
        }

        /// <summary>
        /// Derives the whole scene's lighting from the dome: hemisphere ambient plus a tinted key light, in
        /// linear radiance. The derivation itself is <see cref="SkyLightRig"/>'s since #75 — palette decode,
        /// scale factors, tints and the sky-replacing scenes' own rig alike — and it stood here, in the Testbed
        /// and in the map editor. Which renderers take part stays this file's business, which is why the walk
        /// is here. Internal because the session re-runs it after rebuilding the ceiling's renderer, which
        /// starts without the palette.
        /// </summary>
        internal void ApplySkyLighting()
        {
            _rig.SetSky(_sky, _scene);

            foreach (InstancedModelRenderer renderer in SkyLitRenderers()) _rig.ApplyTo(renderer);

            //The clouds' own two colours follow the dome as well, and the lit side is handed the very radiance
            //the rig gives the scene — one sun, one number (see SkyLightRig.SunRadianceTinted)
            _clouds.ApplyPalette(_skyEffect, _rig.SunRadianceTinted, _rig.ZenithLinear);
        }

        /// <summary>
        /// Puts the scene into the frame: the backdrop's own lighting defaults, the dome that suits it and
        /// the city's day-or-neon switch. The one place a scene change happens, shared by the random pick at
        /// startup, the scene menu and a loaded level that carries a scene of its own.
        /// </summary>
        internal void SetScene(SceneKind scene)
        {
            _scene = scene;

            //The scene's own sound follows the scene, on the one writer's rule. Null-conditional because the
            //startup pick runs before LoadContent has built the audio; that first pick is handed over there.
            _ambience?.SetScene(scene);

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
        /// The per-frame inputs the shared <see cref="SceneRenderer"/> needs. The rig holds five of the six and
        /// its cloud hook was captured once at load, so this allocates nothing — it used to build a fresh
        /// delegate and re-derive the sun's tint on every frame of the draw path.
        /// </summary>
        private SceneFrame BuildSceneFrame() => _rig.BuildSceneFrame(_camera, _wallClock);

        protected override void Update(GameTime gameTime)
        {
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _wallClock += elapsed;

            //Advanced here, with the wall clock and above the stack, because the celebration outlives the
            //screen that started it: clearing a level pushes the result page over the gameplay screen, and a
            //covered screen is not updated at all. Driven off the frame's own elapsed time rather than a play
            //clock, so it does not stop when the game does.
            _fireworks?.Update(elapsed, _camera);

            //The music's handover: a pass is played once rather than looped, and this is what puts the next
            //freshly synthesized variation on when the current one ends (see ProceduralMusic.Update). Up here
            //with the fireworks and for the same reason — it has to keep running whatever is on the stack.
            _music?.Update();

            //The scene's bed and its crossfade, on the wall clock's frame like the clouds: the scene is on
            //screen whether or not a session stands, so its sound is too, pause included.
            _ambience?.Update(elapsed);

            //Which music the moment wants is the stack question (#46): the front end's loop plays exactly
            //while no session screen is on it. The theme's own lifecycle stays the session's — BuildLevel
            //starts it, TearDown and the level's endings stop it — this only closes the one gap that had no
            //owner: leaving to the main menu keeps the session but must not keep its music ("it plays while a
            //level is being played", docs/game-feedback.md), and Continue re-wants the theme because it comes
            //back WITHOUT a BuildLevel. A fresh build's own Play a moment later is the "already sounding"
            //no-op, so the two writers cannot fight.
            if (_music != null)
            {
                bool onFrontEnd = !_screens.Contains<GameplayScreen>();

                if (onFrontEnd != _menuMusicOn)
                {
                    _menuMusicOn = onFrontEnd;

                    if (onFrontEnd)
                    {
                        _music.Stop();
                        _music.PlayMenu();
                    }
                    else
                    {
                        _music.StopMenu();
                        if (_gameplayScreen != null && _gameplayScreen.IsBuilt) _music.Play();
                    }
                }
            }

            //The fireworks give way to the fanfare. Both arrive on the frame a level ends, and a report is
            //broadband and loud enough to bury a tune under it — the bang is an event, the fanfare is the
            //point. Read per frame rather than latched, so the ducking lifts by itself when the piece ends.
            if (_audio != null && _music != null)
                _audio.FireworkDuck = _music.IsFanfarePlaying ? ProceduralAudio.FIREWORK_DUCKED : 1f;

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

            if (fps >= _qualityMinFps)
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

            Console.WriteLine($"[quality] {fps:F0} FPS in the menu at {_quality} (floor {_qualityMinFps:F0}) — lowering to {lowered}");

            ApplyQuality(lowered);
            ShowQualityNotice(lowered);

            //A tier step resizes the scene target, which hitches a frame or two (it would also rebuild the
            //city if the tiers still differed in radius — since the sort none does, see QualityLevel). Left
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
            //notice about what it did has been answered and goes away. Marked as pinned, so a later fullscreen
            //switch does not re-open the probe and walk the tier back off their choice.
            _qualityPinnedByPlayer = true;
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
                _city = new City(seed: CITY_SEED, arenaHalfExtent: ArenaIsland.RADIUS, config: _cityConfig);
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

            //The pipeline's setter writes the tonemap uniform and recreates the scene target in one move —
            //the factor is the target's size, so changing it is exactly what makes the recreate happen
            _pipeline.SupersampleFactor = _supersampleFactor;

            //The space scene sizes its stars in OUTPUT pixels rather than in texels, so it has to be told the
            //factor too — sized in texels a star would come out four times dimmer on High than on Medium.
            //Guarded only as insurance against a future caller: every path that reaches here today
            //(LoadContent's ApplyQuality, the settings row, the adaptive step) runs after the renderer is
            //constructed. Unlike the settings page below, this is not a live case.
            if (_sceneRenderer != null) _sceneRenderer.SupersampleFactor = _supersampleFactor;

            //Null-conditional because the tier is applied during LoadContent, before the menu pages exist — a
            //command-line quality= reaches here well before there is a settings row to write the value onto.
            _settingsPage?.Refresh();
        }

        internal void ToggleFullscreen()
        {
            _fullscreen = !_fullscreen;
            SetGraphics();

            //A fullscreen switch moves the back buffer between 1600×900 and the display's native resolution — a
            //fill-rate change of several times — so a tier the probe reached for the old size can be wrong for
            //the new one (the neon city goes from ~110 to ~37 FPS on a 3840×1600 panel, the same machine). The
            //probe is re-opened, but only when the tier was the probe's verdict rather than the player's: a tier
            //the player set in Settings or on the command line is their decision and is never overridden. The
            //warmup lets the new target's first frames settle before the window counts them.
            if (!_qualityPinnedByPlayer && _qualitySettled && _quality != QualityLevel.High)
            {
                _qualitySettled = false;
                _qualityWarmupLeft = QUALITY_WARMUP_SECONDS;
                _qualityWindowSeconds = 0f;
                _qualityWindowFrames = 0;
            }

            _settingsPage.Refresh();
        }

        internal void ToggleFpsOverlay()
        {
            _info.Visible = !_info.Visible;
            _settingsPage.Refresh();
        }

        /// <summary>
        /// Toggles the lens's chromatic aberration. A taste setting: zero disables the shader's whole
        /// branch, so Off costs literally nothing. Not a per-frame path — the uniform persists on the
        /// effect, so it is written only here and at load.
        /// </summary>
        internal void ToggleAberration()
        {
            _aberration = !_aberration;
            _pipeline.ChromaticAberration = _aberration ? CHROMATIC_ABERRATION : 0f;
            _settingsPage.Refresh();
        }

        /// <summary>
        /// Toggles the film grain, the aberration's sibling: zero disables the shader's whole branch,
        /// so Off costs literally nothing. Only the strength is written here — the seed and the pixel
        /// grid go out per frame in ResolveSceneTarget regardless, and are no-ops while disabled.
        /// </summary>
        internal void ToggleGrain()
        {
            _grain = !_grain;
            _pipeline.FilmGrain = _grain ? FILM_GRAIN : 0f;
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
            //The city's drawn/total is on the line for the same reason everything else here is: it changes what
            //the number means, and it is the one figure that says whether the frustum cull is doing anything
            //from where the camera happens to be standing (see City.PrepareVisible).
            string city = (_scene == SceneKind.City || _scene == SceneKind.NeonCity) && _city != null
                ? $", city {_cityVisible}/{_city.Buildings.Length}"
                : string.Empty;

            Console.WriteLine($"[fps] {_fpsFrames / _fpsWindow:F1} — {_scene}, dome {_skyDome}, ssaa {_supersampleFactor}x"
                + $", {GraphicsDevice.PresentationParameters.BackBufferWidth}x{GraphicsDevice.PresentationParameters.BackBufferHeight}"
                + $", vsync {(_uncappedFps ? "off" : "on")}{city}");

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
            GraphicsDevice.SetRenderTarget(_pipeline.SceneTarget);

            //Cleared to the dome's horizon colour rather than a fixed one: at a wide aspect the bottom
            //corners can look below the horizon past both the dome and the island, and there any other
            //colour shows up as a band instead of blending into the hazed skyline.
            //The sky-replacing scenes (space, the dream) have no dome and no horizon, so they clear to black
            //instead: their pass covers every pixel of the frame, and black is what would show if it ever did not.
            GraphicsDevice.Clear(SceneRenderer.ReplacesSky(_scene) ? Color.Black : new Color(_rig.HorizonLinear));

            //The weather runs off the same wall clock the balls pulse to, so it keeps drifting whatever the
            //game does. Handed to both shaders from the one field, which is what keeps the cloud the player
            //looks at and the shadow it throws across the cluster the same cloud.
            //
            //Space is the one scene with no weather at all: its dome is not drawn (Space.fx covers the frame),
            //and the cloud coverage is zeroed on the instanced effect so the cluster, island and gun are not
            //crossed by the shadows of a deck nobody can see — InstancedModel.fx calls CloudSunlight
            //unconditionally, and a gain left standing from the scene before would go on shadowing this one.
            _clouds.Time = _wallClock;

            if (SceneRenderer.ReplacesSky(_scene)) _clouds.SuppressOn(_instancingEffect);
            else
            {
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
            }

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            //Stated rather than inherited, for the same reason: the scene's cull mode must not depend on
            //what the previous frame's last pass happened to leave behind.
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            //The setting: the backdrop, then the island standing in it. The two cities are the procedural
            //skyline under the shared shader's city technique; the other five are the SceneRenderer's
            //self-lit terrain and water, which need this frame's camera, sun and sky handed over.
            SceneFrame sceneFrame = BuildSceneFrame();

            //The scene's own point lights (the neon ring, the savanna's campfire, space's planetshine) onto the
            //shared instanced effect, so the island, the gun and the balls take them as well as the towers. The
            //clock is the balls' own, so the campfire's light and its flame billboard cannot drift.
            _sceneLights.Apply(_scene, _sceneRenderer, _cityConfig.NeonLook, _wallClock);

            if (_scene == SceneKind.City || _scene == SceneKind.NeonCity)
            {
                //The city's windows keep their own rhythm off the wall clock — a city's lamps do not stop
                //because the game is paused
                _cityRenderer.CityWindowTime = _wallClock;

                //Culled to the frustum and ordered near to far first. The ordering is what pays: the city's
                //pixel shader is the most expensive in the frame, and in generator order most of what it
                //shades is a facade another tower is standing in front of. See City.PrepareVisible.
                _cityVisible = _city.PrepareVisible(_camera);
                _cityRenderer.Draw(_camera, _city.Visible, _cityVisible, _sceneEffectParams);
            }
            else _sceneRenderer.DrawEnvironment(_scene, sceneFrame);

            //The forest's scattered trees, rocks and stumps, drawn after the forest terrain they stand on and
            //before the island: one draw per kind per mesh variant, and per material within a tree. The scene
            //gate stays here because the component draws the wood whenever it is called — where it sits in the
            //frame and whether this frame wants it at all are this file's business, not its.
            if (_scene == SceneKind.Forest) _forestScatter.Draw(_camera);

            //The round island, opaque: its stone cap and concrete drum. Then the dark well behind the glass
            //drain, which is drawn in the solid-terrain scenes only — it fills the hole those shaders cut in
            //the ground, so the drain reads as a deep shaft rather than as a glass ring over bright sky haze —
            //and which brings the culling its open cone needs with it. Each slice owns the states its own
            //geometry wants; where they sit in the frame is this file's decision, which is the whole reason
            //ArenaIsland hands them over separately.
            _island.DrawIsland(_camera, _sceneEffectParams);
            _island.DrawPit(_camera, _sceneEffectParams, _scene);

            return sceneFrame;
        }

        /// <summary>
        /// The drain's gold beads and its glass, after the frame's opaque work: the beads are opaque but the
        /// funnel composites over everything drawn so far — including the gameplay screen's balls, which is
        /// why this is a separate slice the session calls after its own 3D.
        /// </summary>
        internal void DrawSettingGlass() => _island.DrawGlass(_camera, _sceneEffectParams);

        /// <summary>
        /// The frame's close: the scene's foreground weather — the mountain's snow, the sea's spray, the
        /// savanna's flame — settles over everything, and the resolve takes the HDR target to the back
        /// buffer. Display space from here on.
        /// </summary>
        internal void FinishSceneDraw(SceneFrame sceneFrame)
        {
            //A no-op in the two cities and the desert, which carry no overlay weather
            _sceneRenderer.DrawOverlays(_scene, sceneFrame);

            //The victory display goes last of the scene's own draws and before the resolve, so it is inside the
            //HDR pass and blooms through the glare like everything else that emits — which is the entire point
            //of a firework. Drawn from here rather than from a screen so it keeps running once the result page
            //covers the session (see Fireworks).
            _fireworks?.Draw(_camera);

            //How far under the sea the lens is. Only the sea has water to get under, and only the drop
            //cinematic ever takes the camera down there — the play camera stands on the island. Measured a
            //touch above the mean surface so partial submersion already begins to tint, full by
            //UNDERWATER_FADE_DEPTH; zero everywhere else, which is a no-op in the shader.
            float underwater = _scene == SceneKind.Sea
                ? MathHelper.Clamp((_sceneRenderer.SeaLevelY + 0.5f - _camera.Position.Y) / UNDERWATER_FADE_DEPTH, 0f, 1f)
                : 0f;

            _pipeline.Resolve(_wallClock, underwater);
        }

        #endregion

        protected override void UnloadContent()
        {
            _pipeline?.Dispose();
            _spriteBatch?.Dispose();

            //The three sphere meshes and the three renderers' instance buffers, in one call — but not the
            //shared instancing effect they draw through, which the content manager owns
            _balls?.Dispose();

            //The barrel's mesh and its instance buffer, in one call — but not the shared instancing effect,
            //which the content manager owns and the balls, the city, the island and the ceiling all use
            _cannonRig?.Dispose();
            _unitBox?.Dispose();
            _cityRenderer?.Dispose();

            //The island's three meshes, both of its procedural textures and all five of its renderers, in one
            //call — everything the component made
            _island?.Dispose();

            //Every mesh, renderer and procedural texture of the forest scatter, likewise in one call
            _forestScatter?.Dispose();
            _ceilingPlate?.Dispose();

            //The five self-lit backdrops own their own meshes, particle buffers and effects
            _sceneRenderer?.Dispose();

            //The menu: the Desktop holds the widget tree, and each FontSystem holds the glyph atlases it
            //rasterized for the sizes that were asked of it
            _desktop?.Dispose();
            _menuFontSystem?.Dispose();
            _menuFontSystemBold?.Dispose();
            _menuFontSystemDisplay?.Dispose();

            //The session: the simulation, the contact events, the dispatcher, the pool and the shot-trail
            //buffers all live on the gameplay screen now, which disposes them in the order they need
            _gameplayScreen?.DisposeResources();

            //The synthesized SFX buffers were built in LoadContent and outlive every session.
            _audio?.Dispose();
            _ambience?.Dispose();
            _fireworks?.Dispose();
            _music?.Dispose();

            base.UnloadContent();
        }
    }
}
