using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities.Memory;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Physics;
using Prazsky.BS3D.Scoring;
using Prazsky.Core;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;
using static Prazsky.BS3D.Physics.Simu;

//BepuUtilities is deliberately NOT imported: it carries its own Matrix and MathHelper, which would make every
//existing use of the XNA ones in this file ambiguous. The one type needed from it is qualified where it is
//declared. Bepu's own vectors are System.Numerics and are likewise spelled out at each crossing, the
//convention the Testbed uses (see CLAUDE.md, "Conventions").

namespace BS3D
{
    /// <summary>
    /// The game. Deliberately small: it owns the frame, the gun and the shot, and borrows everything else
    /// from the three libraries the Testbed and the map editor already share — the instanced ball shader,
    /// the procedural city, the sky dome, the linear-radiance render pipeline.
    /// <para>
    /// What it is <b>not</b> is a copy of <c>Testbed.cs</c>. There is no Bepu simulation here yet, no map
    /// files, no scene switching and no editor plumbing: a shot ball flies ballistically and snaps into the
    /// cluster's lattice where it touches. That is enough to carry the thing this build exists to establish — balls
    /// crossing a neon city fast enough that you cannot follow them, and a camera that is visibly hit
    /// every time the gun fires.
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

        //Narrow, like the Testbed's game camera and for the same reason: from down behind the gun the
        //barrel and the cluster are close together in the frame, so it can close in on the cluster.
        private static readonly float GAME_FOV = MathF.PI / 4.2f;

        //Where the lens stands relative to the gun: back from the field centre along the gun's own bearing,
        //and just below the trunnions, so the player looks up at the hanging cluster past the barrel.
        private const float CAMERA_HEIGHT = -1.5f;

        //How far back the camera stands and how high it aims. Both are SOLVED per level and per display
        //(FitGameCameraToMap) rather than tuned, because both of their inputs move underneath a fixed number:
        //the field is sized per level, and the frustum per display. These defaults only cover the frames
        //before the first level is installed.
        private float _gameCameraDistance = 34f;
        private float _gameCameraTargetY = 3.5f;

        //How much of the frustum the fit is allowed to fill. Under 1 so the field does not sit hard against
        //the frame's edges, which reads as cropped even when nothing is.
        private const float GAME_CAMERA_FIT_MARGIN = 0.92f;

        //Where the gun stands relative to the camera, and the two lower bounds that override it. The gun is
        //placed off the LENS and not off the field, because the magazine showing through its slot is really a
        //HUD element and has to keep its size on screen — anchoring it to the field lets a large level push
        //the camera back and shrink the queue with it. The bounds: it must clear the field's own footprint at
        //every orbit angle (closer, and it stands under the cluster it shoots at), and it must stay far
        //enough out that its RESTING aim is well inside Cannon's elevation clamp, with headroom to elevate
        //onto the high cells. Which bound binds changes with the level, so this can never be one number.
        private const float CANNON_CAMERA_STANDOFF = 15f;
        private const float CANNON_FIELD_CLEARANCE = 2f;
        private const float CANNON_MAX_REST_ELEVATION = 0.70f;  //radians, ~40°, against a clamp that reaches ~80°

        //Precise aim, held on the right mouse button (or the gamepad's left trigger): the lens leans in over
        //the barrel and looks straight down the bore, so the shot goes where a screen-centre crosshair points
        //and a cell on the far side of the cluster can actually be picked out. Releasing eases back.
        //
        //It is the deliberate inverse of the overview's rule that the view does not ride the aim, and that is
        //what makes it worth having: how much of an angle onto the map the overview can give is fixed by
        //where the camera stands (a camera behind the gun is always further out than the gun, so it always
        //sees the cluster flatter than the barrel does), while a lens looking *along* the aim sees whatever
        //the barrel points at, head-on.
        private static readonly float ADS_FOV = MathF.PI / 5f;  //36°: a 1.19× lean-in on GAME_FOV — a zoom, not a tunnel
        private const float ADS_BACK = 6f;                      //lens set-back from the muzzle ball, along -aim
        private const float ADS_RISE = 2f;                      //lens height over the bore: clears the tube, keeps it a low sliver
        private const float ADS_CONVERGE_MIN = 6f;              //nearest convergence depth (keeps the look-at point off the barrel)
        private const float ADS_CONVERGE_MAX = 90f;             //farthest, well inside the far plane
        private const float ADS_BLEND_TAU = 0.08f;              //ease time constant, seconds (~90 % in ~0.18 s) — the _magazineSlide idiom
        private const float ADS_TRIGGER_THRESHOLD = 0.5f;       //gamepad left-trigger pull that counts as held

        //Aiming steeply up, -aim points downwards and the set-back would drop the lens through the stone
        //island and show it from underneath. Floored a margin over the island's top instead: from there the
        //bottom of the frame still looks upwards, so the stone stays out of it.
        private const float ADS_MIN_Y = ISLAND_Y + 1f;

        private float _adsBlend;
        private bool _adsHeld;

        private readonly GraphicsDeviceManager _graphics;
        private readonly bool _uncappedFps;

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

        #endregion

        #region The overlay (display space, after the resolve)

        //FPS. A DrawableGameComponent, so the component list draws it in base.Draw — last of everything, in
        //display space, with its own SpriteBatch. F12 hides it, as it hides the Testbed's text overlay.
        private InfoRenderer _info;

        //The crosshair. No bitmap: four bars struck from a 1×1 white texture, which is one asset fewer to
        //keep in step with the Testbed's. It appears only as precise aim leans in, because only then does the
        //lens look along the shot — in the overview a screen-centre mark would point at nothing in particular.
        private SpriteBatch _spriteBatch;
        private Texture2D _pixel;

        //Written as a scale of white rather than as R,G,B,A: SpriteBatch's default AlphaBlend expects
        //*premultiplied* colour, and a plain (255,255,255,190) is not — it would put full white down and
        //only partly occlude what is behind it, which is a solid crosshair, not a translucent one. Color's
        //float multiply scales all four channels, so this stays premultiplied through the blend fade too.
        private static readonly Color CROSSHAIR_COLOR = Color.White * 0.75f;

        //Authored for a 2160p viewport and scaled down with it, exactly as InfoRenderer's text is, so the
        //crosshair keeps its size on the screen rather than in pixels
        private const float CROSSHAIR_SCALE_DIVISOR = 2160f;
        private const float CROSSHAIR_ARM = 48f;        //length of one bar
        private const float CROSSHAIR_GAP = 18f;        //clear space at the centre, so the mark never hides what it marks
        private const float CROSSHAIR_THICKNESS = 5f;

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
        private const byte SKY_DOME_COUNT = 18;
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
        private const int SCENE_COUNT = 7;

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

        private Effect _instancingEffect;

        private readonly CitySceneConfig _cityConfig = new();
        private City _city;
        private BoxMesh _unitBox;
        private InstancedModelRenderer _cityRenderer;

        //The glass drain funnel in the middle of the island, the Testbed's own: a truncated cone from a wide
        //rim flush with the stone down to a small hole, ringed with polished gold at both circles. Every
        //figure here is the Testbed's unchanged, because the island's top is at the Testbed's arena height —
        //the two drains are the same object. It is *visual only* in this build: there is no Bepu
        //simulation, so nothing rests on the stone and nothing runs down the cone yet. A missed shot still
        //flies straight through and ages out (#48 carries the physics half).
        //
        //These are const rather than static readonly because ISLAND_INNER_RADIUS is initialised from the
        //rim radius: a static field initialiser reads the fields declared above it, so a later reorder of a
        //static readonly would silently leave the island's bore at zero.
        private const float FUNNEL_TOP_RADIUS = 14f;      //the mouth; the island's bore is cut to exactly this
        private const float FUNNEL_HOLE_RADIUS = 1.8f;    //the hole at the bottom, comfortably wider than a ball
        private const float FUNNEL_BOTTOM_Y = -27.5f;     //~19 below the rim: a wall steep enough to run a ball down
        private const int FUNNEL_SEGMENTS = 64;
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
        //a hard vertical edge the city falls away past. No physics floor — nothing here falls. Const for the
        //same reason the funnel's figures are: the precise-aim floor (ADS_MIN_Y) is derived from it, and that
        //is declared further up the file.
        private const float ISLAND_Y = -8.5f;

        //A washer, not a disc: the bore is the drain's mouth and the funnel fills it exactly. Both circles are
        //drawn at FUNNEL_SEGMENTS facets, so stone and glass meet with no gap for the sky to show through.
        private static readonly float ISLAND_INNER_RADIUS = FUNNEL_TOP_RADIUS;
        private static readonly float ISLAND_RADIUS = 26f;
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
        //hole shows through the narrow aperture anyway. Visual only; the funnel mesh is still the only floor.
        private static readonly float TERRAIN_HOLE_RADIUS = ISLAND_RADIUS - 2f;  //tucked under the stone edge, no gap
        private static readonly float PIT_BOTTOM_Y = -46f;                       //below KILL_PLANE_Y, so balls vanish inside the pit
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

        #endregion

        #region Balls

        private static readonly int BALL_TYPE_COUNT = (int)BallType.Type8;

        private static readonly int[,] BALL_LOD_RESOLUTIONS = { { 32, 24 }, { 16, 12 }, { 10, 7 } };
        private static readonly float[] BALL_LOD_DISTANCES = { 15f, 30f };
        private static readonly int BALL_LOD_COUNT = 3;

        private static readonly int BALL_PATTERN_GORES = 5;
        private static readonly float BALL_ALBEDO = 0.5f;
        private static readonly float BALL_EMISSION = 0.5f;
        private static readonly float BALL_TRANSLUCENCY = 0.35f;
        private static readonly float BALL_PULSE_BEATS_PER_SECOND = 1.1f;
        private static readonly float BALL_PULSE_DEPTH = 0.55f;
        private static readonly float BALL_PULSE_WAVELENGTH = 14f;

        private static readonly float BALL_OCCLUSION_STRENGTH = 0.55f;
        private static readonly int MAX_BALL_OCCLUDERS = 12;

        //How long a ball takes to reach its new shading. A ball joins or leaves the lattice in one step, so its
        //occlusion target changes instantly while the ball has not moved — eased, that reads as the light
        //filling in; taken straight, every ball around a hole a matched group left pops brighter in one frame.
        private static readonly float BALL_OCCLUSION_EASE_SECONDS = 1f;

        //A landed ball is snapped to the nearest free cell rather than to where it hit, so the constraints drag
        //its body up to several diameters within a frame or two. Drawing it gliding in from where it actually
        //hit turns that click into a movement, and costs the simulation nothing.
        private static readonly float BALL_ATTACH_GLIDE_SECONDS = 0.08f;
        private static readonly float BALL_ATTACH_GLIDE_DONE_SQUARED = 0.025f * 0.025f;

        private const float BALL_RADIUS = Constants.HALF;

        private SphereMesh[] _ballMeshes;
        private InstancedModelRenderer[] _ballRenderers;

        private readonly ModelInstance[][] _ballInstances = new ModelInstance[BALL_TYPE_COUNT * BALL_LOD_COUNT][];
        private readonly int[] _ballInstanceCounts = new int[BALL_TYPE_COUNT * BALL_LOD_COUNT];

        #endregion

        #region Physics

        //BepuPhysics 2. The cluster is real: bodies held to each other and to the ceiling by BallSocket
        //constraints, a shot is a body thrown at it, and the island's drain is a collision mesh balls run
        //down. All of it comes from Prazsky.BS3D.Physics, which the Testbed uses unchanged.
        private BufferPool _bufferPool;
        private BepuUtilities.ThreadDispatcher _threadDispatcher;
        private Simulation _simulation;
        private ContactEvents _events;
        private BallContactEventHandler _eventHandler;

        /// <summary>
        /// The physics step, held <b>fixed</b> — and this is the one place the game deliberately does not do
        /// what the Testbed does. The Testbed takes one step of <c>min(frameTime, 1/60)</c> per rendered frame,
        /// which means the simulation runs in slow motion below 60 FPS (at 30 FPS everything moves at half
        /// speed) and with a dt that varies with the display and the load above it. Bepu's own guidance is to
        /// keep the timestep constant, and this project's rule is that nothing about gameplay may depend on the
        /// frame rate — the game runs with <c>IsFixedTimeStep = false</c> and offers <c>nocap</c>, so it is
        /// exactly the configuration that breaks under. The frame time is accumulated instead and spent in
        /// whole steps of this length.
        /// </summary>
        private const float PHYSICS_TIMESTEP = 1f / 120f;

        /// <summary>
        /// How many steps one frame may spend at most. Without a ceiling a frame that hitched — a shader
        /// compile, a window drag, a breakpoint — would try to catch up the whole gap at once, take even longer
        /// doing it and fall further behind: the simulation would spiral instead of recovering. Past this the
        /// remaining time is dropped on the floor and the world simply runs slow for that one frame, which is
        /// the right trade for a game.
        /// </summary>
        private const int PHYSICS_MAX_STEPS_PER_FRAME = 4;

        private float _physicsAccumulator;

        /// <summary>
        /// Below this a ball has left the game: it has run down the drain and out of the bottom of the funnel,
        /// or fallen off the island's edge into the city. Well under <see cref="FUNNEL_BOTTOM_Y"/>, so a ball
        /// that goes down the hole falls a visible distance before it is culled rather than winking out in the
        /// mouth of the drain.
        /// </summary>
        private static readonly float KILL_PLANE_Y = -42f;

        #endregion

        #region The cluster

        //The fallback level, used only when no level file can be read (see LoadLevelSet): a stepped square
        //pyramid hanging point-down. The top level is the full FALLBACK_X × FALLBACK_Z base and each level
        //under it is half a unit narrower on every side, so the side count steps 9, 8, 7 … 1 and the flanks
        //come out straight. Half a unit rather than a whole cell, because consecutive levels are offset by
        //+0.5 in X and Z: shrinking by a cell per level puts every second level half a unit off the axis and
        //the flank zig-zags. It is the shape the game shipped with before it read levels off disk, kept so a
        //missing or broken Levels directory still gives something to play rather than an empty field.
        private const byte FALLBACK_X = 9;
        private const byte FALLBACK_Z = 9;

        //One level per half-unit of the base's half-extent, plus the apex — exactly FALLBACK_X of them. The
        //base is square on purpose: a rectangular one would run out of width on its narrow axis first and
        //finish as a ridge rather than a point.
        private const byte FALLBACK_LEVELS = FALLBACK_X;

        //Empty field levels below the layout: the room the cluster grows into as shot balls attach under it,
        //which is how a map file's field is taller than the layout hanging at its top.
        private const byte FALLBACK_EXTRA_LEVELS = 7;
        private const byte FALLBACK_FIELD_LEVELS = FALLBACK_LEVELS + FALLBACK_EXTRA_LEVELS;

        //Fixed, so the fallback is the same pile every run
        private const int FALLBACK_SEED = 20260726;

        //What the magazine loads when a map carries no balls at all. A real level's colours are read off the
        //map itself — loading a colour that is nowhere in the cluster is a shot that cannot be spent, so the
        //queue can only be built from what is actually up there.
        private static readonly BallType[] DEFAULT_BALL_TYPES =
            { BallType.Type1, BallType.Type2, BallType.Type3, BallType.Type4 };

        /// <summary>
        /// How many balls of each type are still hanging, indexed by <c>(int)BallType - 1</c>. Recounted off
        /// the map every time the cluster changes, which is the only thing that can change it: the magazine
        /// may only ever load a colour whose count is above zero.
        /// <para>
        /// Recounted rather than maintained incrementally, deliberately. The walk is the field's cell count —
        /// 1500 for <c>One.json</c> — and it runs when a shot lands, not per frame; carrying a running total
        /// through the release path, the attach path and the second-ring fallback would be three places to get
        /// wrong in exchange for microseconds nobody can measure.
        /// </para>
        /// </summary>
        private readonly int[] _ballsOfType = new int[BALL_TYPE_COUNT];

        /// <summary>
        /// Where the lattice frame meets the world, and the <b>only</b> place it does on the drawing side.
        /// <para>
        /// Y puts the top of the field at a fixed world height whatever the field's depth: a cell's height is
        /// its level index over √2, so without this a map with more empty levels below its layout would hang
        /// that much higher than the camera frames.
        /// </para>
        /// <para>
        /// X and Z correct the residual half-unit <see cref="BallsMap.Center"/> can leave behind. It offsets
        /// by the top level's bounding-box <i>half-extent</i> less a ball radius, which lands on the origin
        /// only when that level is one of the shifted (odd-index) ones and its cells start at index 0 — an
        /// <b>odd</b> field tops out on an unshifted level, whose cells run 0…N-1 rather than 0.5…N-0.5, and
        /// the whole cluster then hangs half a unit off the axis the gun orbits and the camera looks down.
        /// That used to be a rule the hard-coded field had to satisfy by hand; a level file is authored
        /// elsewhere and cannot be held to it (One.json is fifteen levels deep), so the residual is measured
        /// off the centred top level and folded in here instead. Both halves ride the one vector the physics
        /// builder and the contact handler already take, so no new frame crossing is introduced.
        /// </para>
        /// </summary>
        private Vector3 _clusterWorldOffset;

        /// <summary>
        /// The height the field's topmost level hangs at, which is what everything else is measured from.
        /// It is where the previous hard-coded field put its top ((16-1)/√2 − 7/√2), kept exactly so the
        /// camera, the gun and the ceiling frame a loaded level the way they framed that one.
        /// </summary>
        private static readonly float FIELD_TOP_Y = (FALLBACK_FIELD_LEVELS - 1 - FALLBACK_EXTRA_LEVELS) / Constants.SQRT_TWO;

        //The middle of the field in world Y, which is what precise aim converges its crosshair on. The whole
        //field rather than the layout hanging at its top, because the cluster grows down into the empty
        //levels as balls attach, and the impact face sweeps that range over a game.
        private float _clusterCentreY;

        //The lattice is the truth about what is where and what is free for a shot to land in. The parallel
        //array of PhysicsBalls is that same structure as Bepu bodies — one per occupied cell, each held to its
        //neighbours and, on the top level, to the ceiling by BallSocket constraints. The bodies are what the
        //frame draws, so the player is looking at the simulation rather than at a copy of it: the cluster
        //sways, a shot shoves it, and a matched group falls.
        private BallsMap _map;
        private PhysicsBall[,,] _physicsBalls;

        //What the cluster hangs from: a translucent glass plate over the play field, the Testbed's own ceiling.
        //Without it the cluster hangs out of nothing, and a mass of balls suspended in mid-air over a city
        //reads as an object that has not been finished rather than as one that is held up. It is a real
        //kinematic body now, because that is what a BallSocket needs at the far end — Bepu constraints take
        //bodies, never statics.
        private static readonly Vector3 CEILING_GLASS_COLOR = new(0.55f, 0.75f, 0.85f);
        private static readonly float CEILING_GLASS_ALPHA = 0.4f;

        //Two above the centres of the top level's balls, the Testbed's own figure. The kinematic body and the
        //drawn glass box both sit here — the box is drawn straight from the body's pose (see KinematicBody),
        //so the collidable and the thing the player sees cannot drift apart.
        //
        //Note the cluster does not settle on the lattice: the ceiling BallSocket anchors a ball's top (local
        //+0.5) to the plate's bottom face (local -0.5), so the top level comes to rest one unit under the body
        //and the whole rigid structure with it. Half the clearance this constant looks like it buys is spent
        //that way, and the top balls end up close under the glass. It is the Testbed's behaviour, kept for
        //parity; the figure to change if the cluster should hang exactly on its lattice is this one.
        private const float CEILING_CLEARANCE = 2f;
        private float _ceilingY;

        private BoxMesh _ceilingMesh;
        private InstancedModelRenderer _ceilingRenderer;
        private KinematicBody _ceiling;

        //The descending ceiling — the second of the two pressures that can lose a level, made visible where the
        //shot budget is made numerical. Every ceilingStep shots the glass steps down by CEILING_DESCENT_PER_STEP,
        //and with it the cluster (the top level is held to the body by BallSocket constraints, so moving the body
        //drags the structure along — Bepu does the work). The level is lost the moment any ball crosses the death
        //line, which is above the gun and the drain so a cluster reaching into them reads as a loss before it
        //reads as a bug.
        //
        //The descent is animated at constant velocity per step rather than teleported: a hundred constrained
        //bodies jerked in one write can throw the solver, and a short slide lets the contact between a descending
        //cluster and anything below it resolve. The body is kinematic and this build's integrator does not move
        //kinematics from their velocity (PoseIntegratorCallbacks.IntegrateVelocityForKinematics is false), so the
        //slide is driven by writing the pose — in small steps, which is what makes it tolerable to the solver.
        private const float CEILING_DESCENT_PER_STEP = 0.6f;        //world units the glass drops each step
        private const float CEILING_DESCENT_SPEED = 1.5f;           //units/sec while a step is sliding in
        private const float CEILING_DEATH_Y = -5.5f;                //a ball below this has lost the level

        //Where the glass body sits now (_ceilingY) and where it is sliding to (_ceilingTargetY). Equal while at
        //rest; _ceilingTargetY is lowered by StartCeilingDescent and _ceilingY catches up in UpdateCeilingDescent.
        private float _ceilingTargetY;
        private bool _ceilingDescending;

        #endregion

        #region Levels

        /// <summary>
        /// The levels and the order they are played in, read from <c>Levels/Levels.json</c> beside the exe.
        /// Null when no set could be read at all, which is the one case the procedural fallback covers.
        /// </summary>
        private LevelSet _levelSet;

        /// <summary>Which entry of <see cref="_levelSet"/> the current session is playing.</summary>
        private int _levelIndex;

        private const string LEVELS_DIRECTORY = "Levels";

        /// <summary>
        /// Seconds left of the pause between the field emptying and the level actually ending, counted down
        /// only while it is above zero — so zero doubles as "this level is still being played".
        /// <para>
        /// The pause is the point. Balls leave the map at <b>release</b> time, while their bodies are still
        /// falling: ending the level the instant the last group is cut would take the collapse the player just
        /// earned off the screen before they saw it.
        /// </para>
        /// </summary>
        private float _clearedCountdown;

        /// <summary>
        /// Set the moment a level is lost, and only once — a descent and a spent budget can both reach their line
        /// on the same frame, and the loss must not fire twice. Cleared back to false by <see cref="BuildLevel"/>,
        /// which is the real reload that starts a level over.
        /// </summary>
        private bool _levelLost;

        /// <summary>How long that pause is — long enough for a big collapse to reach the drain and go down it.</summary>
        private const float LEVEL_CLEARED_BEAT = 2.5f;

        /// <summary>
        /// The level's score and ball budget. Built fresh for each level from that entry's rules, so it never
        /// carries anything across; it holds the rules themselves and this file only feeds it the three events
        /// a shot goes through.
        /// </summary>
        private ScoreKeeper _score = new();

        /// <summary>
        /// What ended the level, set when it ends and read by the result screen. <c>None</c> means the level is
        /// still being played; <see cref="FinishLevel"/> is what leaves <c>None</c>.
        /// </summary>
        private enum LevelOutcome { None, Cleared, Failed }

        /// <summary>
        /// Which of the two limits ended the level. An enum rather than a message carried through from where
        /// the loss was detected: the wording is a <b>display</b> concern and belongs on the screen that shows
        /// it, and a string built at the point of detection ends up carrying the numbers that were convenient
        /// there — which is how a player came to be shown "a ball at -5,58 &lt;= -5,50". Those figures are
        /// diagnostics; they belong in the log, and they still go there.
        /// </summary>
        private enum LevelFailure { None, OutOfBalls, ClusterReachedLine, ShortOfGate }

        private LevelFailure _pendingFailure;

        private LevelOutcome _pendingOutcome = LevelOutcome.None;

        /// <summary>
        /// When <see cref="_pendingOutcome"/> is <c>Failed</c>, said plainly: which limit ran out (or which gate
        /// was missed). A single number is what the result screen has to show a player who did not already know.
        /// </summary>

        #endregion

        #region The gun and the shot

        private Cannon _cannon;
        private CannonMesh _cannonMesh;
        private InstancedModelRenderer _cannonRenderer;

        private const float CANNON_BORE_RADIUS = 0.6f;
        private const float CANNON_WALL_THICKNESS = 0.14f;
        private const float CANNON_SLOT_HALF_ANGLE = 0.5f;
        private static readonly Vector3 CANNON_COLOR = new(0.42f, 0.44f, 0.48f);

        private const int MAGAZINE_SIZE = 5;
        private const float MAGAZINE_SPACING = 1.0f;
        private const float MAGAZINE_SLIDE_TAU = 0.07f;
        private const float CANNON_PIVOT_TO_FRONT_BALL = (MAGAZINE_SIZE - 1) * MAGAZINE_SPACING * Constants.HALF;

        private readonly BallType[] _magazine = new BallType[MAGAZINE_SIZE];
        private float _magazineSlide;

        /// <summary>
        /// The colour a loaded ball is dissolving <i>out of</i>, per slot, and how far through that it is
        /// (0 = settled, nothing to draw twice). A ball whose colour has just been eliminated from the cluster
        /// is re-coloured where it sits rather than left to be fired at nothing — see <see cref="Transmute"/>.
        /// </summary>
        private readonly BallType[] _magazineFrom = new BallType[MAGAZINE_SIZE];
        private readonly float[] _magazineTransmute = new float[MAGAZINE_SIZE];

        /// <summary>
        /// How long a loaded ball takes to change colour. Slow enough to be unmistakably seen — the whole
        /// point is that the player watches the game help them, and a snap would read as a bug — and short
        /// enough not to hold up a queue the player is aiming with.
        /// </summary>
        private const float TRANSMUTE_SECONDS = 0.75f;

        //Several ball diameters a frame: the shot is a streak, not something the eye can follow. That is
        //the intended feel, and it is why the launch smear below exists at all.
        private const float SHOOT_SPEED = 200f;

        //How hard a shot hits the camera. Still a full kick: it is the *ceiling* that was lowered
        //(CAMERA_SHAKE_SCALE), not the strength of one shot. Kicking at a fraction instead would let two
        //shots in quick succession accumulate straight back up to the response that was too strong.
        private const float RECOIL_KICK = 1f;

        //The camera's whole shake, scaled off CameraShake's defaults through this one dial. The gun now
        //throws itself back visibly when it fires, so the camera no longer has to carry the shot's force on
        //its own — and carrying all of it read as the lens being hit rather than as a gun going off.
        private const float CAMERA_SHAKE_SCALE = 0.45f;

        //The gun's own recoil: the barrel is thrown straight back along its bore and slides home again,
        //carrying the balls loaded in it. Drawing only — a shot leaves along the true aim on the frame it is
        //fired, before any of this, so nothing about where a ball goes depends on it.
        private const float CANNON_RECOIL_BACK = 1.15f;  //how far back at the peak, world units (a little over one ball diameter)
        private const float CANNON_RECOIL_DECAY = 4.2f;  //how fast it comes home, per second (1 ÷ this is the stroke: ~0.24 s)

        private float _cannonRecoil;

        private const float MOUSE_AIM_SENSITIVITY = 2.0f;
        private const float PAD_AIM_RATE = 1.0f;
        private const float CANNON_ORBIT_RATE = 1.0f;

        //Balls in flight, and balls that have been let go and are falling. Both are real Bepu bodies with no
        //constraints; the difference is only that a shot ball still listens for its contacts, because it can
        //still attach to the structure, while a released one is finished with and merely falls.
        //
        //Both list INSTANCES are shared with the contact handler, which mutates them — so they are cleared,
        //never reassigned.
        private readonly List<PhysicsBall> _shotBalls = new();
        private readonly List<PhysicsBall> _fallingBalls = new();

        //The template a shot is stamped from: the sphere, its inertia and its sleep threshold, built once.
        private BodyDescription _shotBall;

        /// <summary>The launch smear: anchored at the muzzle, living its own short life while it fades.</summary>
        private struct ShotTrail
        {
            public Vector3 Origin;
            public Vector3 Direction;
            public Vector3 Color;
            public float Age;
        }

        private readonly List<ShotTrail> _trails = new();

        private const float TRAIL_LIFETIME = 0.45f;
        private const float TRAIL_LENGTH = 7f;
        private const float TRAIL_LEAD_WIDTH = 0.72f;
        private const float TRAIL_MUZZLE_WIDTH = 0.42f;
        private const float TRAIL_BRIGHTNESS = 3.0f;
        private const float TRAIL_COLOR_FLOOR = 0.12f;

        private Effect _shotTrailEffect;
        private VertexBuffer _shotTrailVertexBuffer;
        private IndexBuffer _shotTrailIndexBuffer;

        //Cached in CreateShotTrailQuad; the fixed widths are set once there and never re-sent
        private EffectParameter _trailViewParam;
        private EffectParameter _trailProjectionParam;
        private EffectParameter _trailCameraPositionParam;
        private EffectParameter _trailHeadParam;
        private EffectParameter _trailTailParam;
        private EffectParameter _trailColorParam;
        private EffectParameter _trailAlphaParam;

        private static readonly Random RANDOM = new();

        private MouseState _previousMouse;
        private KeyboardState _previousKeyboard;
        private GamePadState _previousPad;
        private bool _mouseAimInitialized;

        #endregion

        #region The menu (Myra)

        //The game's three coarse states, gated at the top of Update and Draw. Menu is the launch state: a
        //camera orbiting one of the seven scenes with the front end over it, and no session at all. Playing
        //is the game loop. Paused freezes the session and puts the pause menu over the frame it stopped on.
        //The split is the seam the whole menu is built around — every gameplay path (input, physics,
        //shooting, the game camera) sits behind it, so the menu never has to fight the game for anything.
        private enum GameState { Menu, Playing, Paused }

        private enum MenuScreen { MainMenu, Settings, SceneSelect, About, Pause, Result }

        private GameState _state = GameState.Menu;
        private MenuScreen _menuScreen = MenuScreen.MainMenu;

        //True once the physics world, the ceiling body and the cluster have been built. They are deferred out
        //of LoadContent and built on the first "Play" — the simulation is the expensive part of starting up,
        //and there is no point paying for it before the player has chosen to play. It is also what tells the
        //main menu whether there is a session to go back to, and what keeps Draw off a cluster that is not
        //there: every gameplay object is drawn behind this flag.
        private bool _gameBuilt;

        //The Myra host. Rendered as the very last thing in Draw (after the tonemap resolve and base.Draw),
        //straight to the back buffer — the same place the map editor puts it. Render() also processes Myra's
        //own mouse/keyboard input, so it is only called in Menu/Paused, where the game's own input stands down.
        private Desktop _desktop;

        //The screens, built once at load and swapped into _desktop.Root as the player navigates. Building them
        //once rather than per frame keeps the menu out of the frame loop's allocation path.
        private Widget _mainMenuRoot, _settingsRoot, _sceneSelectRoot, _aboutRoot, _pauseRoot, _resultRoot;

        //Widgets the menu writes back into: the resume entry only exists while there is a session to resume,
        //the settings screen shows each value on its own button, and the scene list marks the one in use.
        private Button _resumeButton;
        private Label _playLabel;
        private Label _fullscreenValue, _ssaaValue, _exposureValue, _skyValue, _fpsValue;
        private readonly Label[] _sceneLabels = new Label[SCENE_COUNT];

        //The result screen's own widgets, written to in RefreshResultScreen from the score snapshot. The heading
        //states the outcome, the reason says which limit ran out (only on a fail), the breakdown rows account for
        //the score, and the two buttons are shown or held back depending on whether the level was passed and
        //whether a next entry exists. Like the resume entry, "held back" means absent, not greyed-out.
        private Label _resultHeading, _resultReason;
        private Label _resultMatchedDetail, _resultMatchedValue;
        private Label _resultOrphanedDetail, _resultOrphanedValue;
        private Label _resultStreakValue;
        private Label _resultUnusedDetail, _resultUnusedValue;
        private Label _resultTotalValue, _resultNeededValue;
        private Label _resultFailedScore;
        private Widget _resultBreakdown;
        private Button _retryButton, _nextLevelButton;

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

        //The menu camera orbits the scene's origin in XZ. The angle is advanced by elapsed seconds, never by
        //a frame count, so the turn takes the same time on any machine.
        private float _menuAngle;

        //The orbit sits well outside the island (radius 26) so the whole platform reads with the backdrop
        //around it, and close to level with its target so the frame is a look across the scene rather than
        //down onto it — which is what shows most of a city, a sea or a mountain range at once.
        private const float MENU_CAM_RADIUS = 44f;
        private const float MENU_CAM_HEIGHT = 3f;
        private const float MENU_TARGET_Y = 5f;

        //About a full turn every 90 s: slow enough to read as ambience rather than as a turntable.
        private const float MENU_ROTATION_SPEED = MathHelper.TwoPi / 90f;

        private static readonly float MENU_FOV = MathF.PI / 3f;  //60°: wide, to take in the scene behind the panel

        //The menu is deliberately GREYSCALE — no hue anywhere, and no coloured frames. It has to sit over
        //seven backdrops whose palettes are nothing alike (a neon city, an ochre desert, a blue sea, white
        //peaks, green meadow), and any accent colour that reads as the game's own over one of them fights
        //the next. Neutral black-to-white belongs over all of them equally: emphasis is carried by
        //brightness and by opacity, which is legible against any hue.
        //
        //Display-space sRGB throughout: Myra draws to the back buffer, after the frame's one and only exit
        //from linear light.
        private static readonly Color MENU_TEXT = new(244, 244, 244);        //the active thing on a screen
        private static readonly Color MENU_TEXT_BODY = new(208, 208, 208);   //prose, a shade under a heading
        private static readonly Color MENU_TEXT_DIM = new(146, 146, 146);    //asides, always on a dark plate

        //Buttons: a dark slab at rest that the pointer lifts a step up the grey ramp, so the highlight is
        //brightness and not hue. Each of these REPLACES the one before it rather than being laid over it
        //(Myra picks one brush per state), so they all have to be opaque enough to hide the scene behind
        //them — a translucent hover would show the backdrop through the entry the pointer is on, which is
        //exactly the one that has to read clearly. They stay dark, because the label on top of them is white.
        private static readonly Color MENU_BUTTON = new(11, 11, 11, 212);
        private static readonly Color MENU_BUTTON_OVER = new(72, 72, 72, 232);
        private static readonly Color MENU_BUTTON_PRESSED = new(120, 120, 120, 240);

        //A pause dims the whole frame, because what is behind it is a stopped game and the menu is the thing
        //to look at. The front end does NOT: there the rotating scene is the point of the screen, and a
        //full-screen wash over it throws away the one thing that screen exists to show. Its legibility comes
        //from the widgets instead — the entries are near-opaque slabs and the prose sits on a plate.
        private static readonly Color PAUSE_SCRIM = new(0, 0, 0, 176);

        //Behind prose, where a slab alone cannot hold a line of small text steady over a moving scene
        private static readonly Color MENU_PLATE = new(0, 0, 0, 190);

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
        private const string GAME_TITLE = "Bubble Shooter 3D";

        //Design units: pixels at 2160p, put through Scaled() wherever they reach a widget
        #region Adaptive quality

        /// <summary>
        /// True once the supersample factor is not to be touched again: the player named one on the command
        /// line, the player set one in Settings, the machine proved fast enough, or there is nothing left to
        /// lower. It is a one-way latch on purpose — a dial that keeps moving under the player is worse than
        /// one that is merely wrong once.
        /// </summary>
        private bool _qualitySettled;

        private float _qualityWarmupLeft = QUALITY_WARMUP_SECONDS;
        private float _qualityWindowSeconds;
        private int _qualityWindowFrames;

        /// <summary>Shown on the main menu once, and only if something was actually lowered.</summary>
        private Label _qualityNotice;

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
        private const int SETTING_VALUE_WIDTH = 560;
        private const int ABOUT_TEXT_WIDTH = 1860;
        private const int MENU_COLUMN_SPACING = 26;

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
        private static readonly string[] SCENE_NAMES =
            { "City", "Sea", "Savanna", "Desert", "Mountains", "Meadow", "Neon City" };

        #endregion

        /// <param name="supersampleFactor">
        /// <c>null</c> when the player did not say — which is what lets <see cref="TuneQualityToFrameRate"/>
        /// lower it on hardware that cannot afford the default. An explicit <c>ssaa=</c> is never overridden.
        /// </param>
        public BS3DGame(bool fullscreen = false, int? supersampleFactor = null, float exposure = DEFAULT_EXPOSURE, bool uncappedFps = false)
        {
            _fullscreen = fullscreen;
            _supersampleFactor = Math.Clamp(supersampleFactor ?? DEFAULT_SUPERSAMPLE_FACTOR, 1, 4);

            //An explicit factor is the player's decision and settles the question; an absent one leaves the
            //adaptive path free to measure this machine and lower it
            _qualitySettled = supersampleFactor.HasValue;
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

            //The cursor is captured for aiming only while a game is actually running; in the menu it is the
            //pointer the player clicks with. This runs from the constructor too, before the device exists,
            //where the field initialiser has already put the state at Menu.
            IsMouseVisible = _state != GameState.Playing;
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
        /// Re-derives the camera's aspect and, with it, the framing. The fit is checked on <b>both</b> frustum
        /// axes, and only the vertical one is aspect-independent — a narrow window or a tall one flips which
        /// binds — so a resize has to re-solve the stand-off and not just the projection.
        /// </summary>
        private void UpdateCameraAspect()
        {
            if (_camera == null) return;

            _camera.AspectRatio = GraphicsDevice.Viewport.AspectRatio;

            FitCannonAndGameCameraToLevel();
        }

        protected override void Initialize()
        {
            _camera = new RecoilCamera
            {
                AspectRatio = GraphicsDevice.Viewport.AspectRatio,
                FieldOfView = GAME_FOV
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

            //Orbit centre is the field the cluster hangs over; the trunnions sit an axle's height above the
            //island, and the gun stands well inside the island's rim.
            _cannon = new Cannon(new Vector3(0f, 5f, 0f), -6.4f, 20f);

            for (int i = 0; i < MAGAZINE_SIZE; i++) _magazine[i] = RandomBallType();

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
            CreateShotTrailQuad();

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
            //queue of loaded balls recedes from a muzzle lip CANNON_PIVOT_TO_FRONT_BALL ahead of it.
            float muzzleZ = -(CANNON_PIVOT_TO_FRONT_BALL + Constants.HALF);
            float breechZ = (MAGAZINE_SIZE - 1) * MAGAZINE_SPACING - CANNON_PIVOT_TO_FRONT_BALL + Constants.HALF;

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
            //part of starting up and belong to a play session, so StartGame builds them on the first "Play"
            //and rebuilds them for a new game. Everything above is the scene, which the menu also stands in.

            _skyEffect = Content.Load<Effect>("Shaders/Sky");
            _skyCameraPositionParam = _skyEffect.Parameters["CameraPosition"];
            _sky = new SkyDome(Content.Load<Model>("Skyes/SkyDome" + _skyDome), GraphicsDevice, linearVertexColors: true)
            {
                Effect = _skyEffect
            };

            SetCloudParameters();

            //A different one of the seven every launch, so the front end is not the same picture twice. It
            //also sets the dome and the city's lighting, and ends in ApplySkyLighting — which is why nothing
            //derives the light rig before this point.
            SetScene((SceneKind)RANDOM.Next(SCENE_COUNT));

            EnsureSceneTarget();

            //The order the levels are played in, read once here: a broken set is reported at startup rather
            //than at the moment the player presses Play, and the maps themselves are only parsed per level.
            LoadLevelSet();

            Console.WriteLine($"[game] {_city.Buildings.Length} buildings, scene {_scene}, dome {_skyDome}");

            BuildMenu();
        }

        #region The menu's screens

        /// <summary>
        /// Boots Myra and builds the five screens. Called once at the end of <see cref="LoadContent"/>: the
        /// menu is drawn over the live scene, so everything it stands on has to exist first. Each screen is
        /// built once here and swapped into <see cref="_desktop"/>.Root as the player navigates, which is what
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

            EnsureMenuLayout();
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

            _mainMenuRoot = BuildMainMenuScreen();
            _settingsRoot = BuildSettingsScreen();
            _sceneSelectRoot = BuildSceneSelectScreen();
            _aboutRoot = BuildAboutScreen();
            _pauseRoot = BuildPauseScreen();
            _resultRoot = BuildResultScreen();

            //Re-asserts the screen the player was on onto the freshly built widgets, and with it everything
            //ShowMenuScreen keeps in step — the resume entry, the setting values, the marked scene, the result
            ShowMenuScreen(_menuScreen);
        }

        /// <summary>A 2160p design figure at the viewport's actual size. Never below one pixel.</summary>
        private int Scaled(int designUnits) => Math.Max(1, (int)MathF.Round(designUnits * _menuScale));

        private Thickness ScaledThickness(int horizontal, int vertical) =>
            new(Scaled(horizontal), Scaled(vertical));

        private Thickness ScaledThickness(int left, int top, int right, int bottom) =>
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

        /// <summary>
        /// Puts a screen up. Everything that has to agree with the game's current state — whether there is a
        /// session to resume, what each setting reads, which scene is in use — is refreshed here rather than
        /// per frame, because a screen can only change while it is being shown.
        /// </summary>
        private void ShowMenuScreen(MenuScreen screen)
        {
            _menuScreen = screen;

            switch (screen)
            {
                case MenuScreen.MainMenu:
                    //Resuming is only offered when there is something to resume, and the play entry says
                    //plainly that pressing it again deals a new cluster rather than continuing this one
                    _resumeButton.Visible = _gameBuilt;
                    _playLabel.Text = _gameBuilt ? "New Game" : "Play";
                    _desktop.Root = _mainMenuRoot;
                    break;

                case MenuScreen.Settings:
                    RefreshSettingsLabels();

                    //The three shared screens are reached from the front end and from a pause alike, and the
                    //dimming behind them belongs to where they were opened from: light over a scene that is
                    //the point of the picture, heavy over a frozen game that is not.
                    _settingsRoot.Background = CurrentScrim();
                    _desktop.Root = _settingsRoot;
                    break;

                case MenuScreen.SceneSelect:
                    MarkSelectedScene();
                    _sceneSelectRoot.Background = CurrentScrim();
                    _desktop.Root = _sceneSelectRoot;
                    break;

                case MenuScreen.About:
                    _aboutRoot.Background = CurrentScrim();
                    _desktop.Root = _aboutRoot;
                    break;

                case MenuScreen.Pause:
                    _desktop.Root = _pauseRoot;
                    break;

                case MenuScreen.Result:
                    //The breakdown is a snapshot of the score keeper, written onto the screen's labels here
                    //rather than baked at build — the widget tree is built once and reused every time a level
                    //ends, and the numbers are different each one. Mirrors how RefreshSettingsLabels writes onto
                    //the settings buttons after their build.
                    RefreshResultScreen();
                    _desktop.Root = _resultRoot;
                    break;
            }
        }

        /// <summary>
        /// The dimming a shared screen gets: the pause scrim over a stopped game, and <b>none at all</b> over
        /// the front end's live scene — see the palette for why. A null background simply draws nothing.
        /// </summary>
        private IBrush CurrentScrim() => _state == GameState.Paused ? _pauseScrimBrush : null;

        /// <summary>Back out of a sub-screen to whichever menu opened it.</summary>
        private void BackFromSubScreen() =>
            ShowMenuScreen(_state == GameState.Paused ? MenuScreen.Pause : MenuScreen.MainMenu);

        private Widget BuildMainMenuScreen()
        {
            VerticalStackPanel column = MenuColumn();

            //The title carries no plate and no frame: at this size the letters are their own mass, and a
            //frame around them would be one more thing competing with whichever scene is turning behind it.
            //"BS3D" is the repository's and the assembly's shorthand; the game's name is spelled out.
            column.Widgets.Add(new Label
            {
                Text = GAME_TITLE,
                Font = _menuFontTitle,
                TextColor = MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 60),
            });

            _resumeButton = MenuButton("Continue", () => StartGame(newGame: false));
            column.Widgets.Add(_resumeButton);

            column.Widgets.Add(MenuButton("Play", () => StartGame(newGame: true), out _playLabel));
            column.Widgets.Add(MenuButton("Scene", () => ShowMenuScreen(MenuScreen.SceneSelect)));
            column.Widgets.Add(MenuButton("Settings", () => ShowMenuScreen(MenuScreen.Settings)));
            column.Widgets.Add(MenuButton("About", () => ShowMenuScreen(MenuScreen.About)));
            column.Widgets.Add(MenuButton("Quit", Exit));

            //Hidden unless the adaptive path actually lowered something (see TuneQualityToFrameRate). A player
            //whose machine copes never learns this exists, which is the point: it explains a change they did
            //not ask for, and is not itself a setting.
            _qualityNotice = new Label
            {
                Text = string.Empty,
                Font = _menuFontSmall,
                TextColor = MENU_TEXT_BODY,
                Wrap = true,
                Width = Scaled(ABOUT_TEXT_WIDTH),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 40, 0, 0),

                //Its own backing, because the front end deliberately has no scrim — the rotating scene is the
                //point of that screen — and a line of small text over open water or a lit skyline is exactly
                //what a plate exists for. Buttons carry their own; this is the only prose here that does not.
                Background = new SolidBrush(MENU_PLATE),
                Padding = ScaledThickness(34, 18),

                Visible = false,
            };
            column.Widgets.Add(_qualityNotice);

            return ScreenRoot(column, scrim: null);
        }

        private Widget BuildPauseScreen()
        {
            VerticalStackPanel column = MenuColumn();

            column.Widgets.Add(ScreenHeading("PAUSED"));
            column.Widgets.Add(MenuButton("Resume", ResumeGame));
            column.Widgets.Add(MenuButton("Settings", () => ShowMenuScreen(MenuScreen.Settings)));
            column.Widgets.Add(MenuButton("Scene", () => ShowMenuScreen(MenuScreen.SceneSelect)));
            column.Widgets.Add(MenuButton("Main Menu", ReturnToMainMenu));
            column.Widgets.Add(MenuButton("Quit", Exit));

            return ScreenRoot(column, _pauseScrimBrush);
        }

        /// <summary>
        /// The end-of-level screen: the one place both ways a level ends (cleared, #56; failed, #58) land, and
        /// the one place a player is told which happened and chooses what to do about it. It is a pause screen
        /// with different contents rather than a fourth <see cref="GameState"/> — the freeze, the scrim over a
        /// stopped frame and Myra owning the input are exactly what <see cref="GameState.Paused"/> already gives.
        /// </summary>
        /// <remarks>
        /// The widget tree is built once and reused for every level end; the numbers and the visible buttons
        /// change, and are written in <see cref="RefreshResultScreen"/> from the score snapshot, exactly as the
        /// settings values are written onto their buttons after their build.
        /// </remarks>
        private Widget BuildResultScreen()
        {
            VerticalStackPanel column = MenuColumn();

            //CLEARED / FAILED / CAMPAIGN COMPLETE — a title's size, like the main menu's name, because this is
            //the line the screen exists to state.
            _resultHeading = new Label
            {
                Text = string.Empty,
                Font = _menuFontTitle,
                TextColor = MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 30),
            };
            column.Widgets.Add(_resultHeading);

            //Which limit ran out, said plainly — only on a fail. Held back (Visible = false) on a cleared level.
            _resultReason = new Label
            {
                Text = string.Empty,
                Font = _menuFontBody,
                TextColor = MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 12),
            };
            column.Widgets.Add(_resultReason);

            //The score reached, on a fail. The breakdown below is rightly held back — a failed level is awarded
            //no completion bonus and its partial rows would explain a total nobody is being offered — but the
            //total itself still has to be said, or the player is told they lost and nothing about how they did.
            _resultFailedScore = new Label
            {
                Text = string.Empty,
                Font = _menuFontBody,
                TextColor = MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 30),
            };
            column.Widgets.Add(_resultFailedScore);

            //The breakdown: caption · detail · value, the same three-column shape the settings screen uses, so a
            //number lines up under the number above it and reads at a glance. A plate behind it, because small
            //text over a frozen scene needs the backing the scrim does not give — the same reason the settings
            //and about screens carry one. Held back on a fail: a single total teaches nothing, and the only
            //number a failed level's player cares about is the reason above.
            _resultBreakdown = Plate(BuildResultBreakdown());
            column.Widgets.Add(_resultBreakdown);

            column.Widgets.Add(_retryButton = MenuButton("Retry", RetryLevel));

            //Absent rather than disabled when there is no next level to go to or the score did not clear the gate
            //(see RefreshResultScreen). Retry stays: it is the one thing that always makes sense at a level's end.
            column.Widgets.Add(_nextLevelButton = MenuButton("Next Level", AdvanceLevel));

            column.Widgets.Add(MenuButton("Main Menu", () =>
            {
                //The session is torn down, not kept: a level that has ended is not one to "Continue" into, and
                //the main menu should offer "Play", not "Continue" into a level that is already finished.
                TearDownGame();
                ReturnToMainMenu();
            }));

            return ScreenRoot(column, _pauseScrimBrush);
        }

        /// <summary>
        /// The score breakdown grid: each row a caption, the detail that earned it, and the points it was worth.
        /// The labels are kept on fields so <see cref="RefreshResultScreen"/> can write the numbers onto them
        /// without rebuilding the grid — the tree is built once and reused for every level end.
        /// </summary>
        private Grid BuildResultBreakdown()
        {
            Grid grid = new()
            {
                ColumnSpacing = Scaled(48),
                RowSpacing = Scaled(12),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));   //caption
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));   //detail (count × worth)
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Part));   //value, right-aligned by the cell

            AddBreakdownRow(grid, 0, "matched", out _resultMatchedDetail, out _resultMatchedValue);
            AddBreakdownRow(grid, 1, "orphaned", out _resultOrphanedDetail, out _resultOrphanedValue);
            AddBreakdownRow(grid, 2, "streak bonus", out _, out _resultStreakValue);
            AddBreakdownRow(grid, 3, "shots unused", out _resultUnusedDetail, out _resultUnusedValue);

            //The total sits on its own line under the rows — in the value column, so it lines up under the row
            //totals — in the heading weight, so it reads as the answer rather than as another line of the sum.
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            _resultTotalValue = new Label
            {
                Text = string.Empty,
                Font = _menuFontHeading,
                TextColor = MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(_resultTotalValue, 2);
            Grid.SetRow(_resultTotalValue, 4);
            grid.Widgets.Add(_resultTotalValue);

            //The gate the level set, as an aside under the total — "needed 1500" — so a player can see at a
            //glance whether the score cleared it without having to remember the number. Empty when there is no
            //gate, which RefreshResultScreen sets to a blank rather than hiding the row.
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            _resultNeededValue = new Label
            {
                Text = string.Empty,
                Font = _menuFontSmall,
                TextColor = MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(_resultNeededValue, 2);
            Grid.SetRow(_resultNeededValue, 5);
            grid.Widgets.Add(_resultNeededValue);

            return grid;
        }

        private void AddBreakdownRow(Grid grid, int row, string caption, out Label detail, out Label value)
        {
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));

            Label captionLabel = new()
            {
                Text = caption,
                Font = _menuFontBody,
                TextColor = MENU_TEXT,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(captionLabel, 0);
            Grid.SetRow(captionLabel, row);
            grid.Widgets.Add(captionLabel);

            detail = new Label
            {
                Text = string.Empty,
                Font = _menuFontBody,
                TextColor = MENU_TEXT_DIM,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(detail, 1);
            Grid.SetRow(detail, row);
            grid.Widgets.Add(detail);

            value = new Label
            {
                Text = string.Empty,
                Font = _menuFontBody,
                TextColor = MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(value, 2);
            Grid.SetRow(value, row);
            grid.Widgets.Add(value);
        }

        /// <summary>
        /// Replays the current entry by tearing the session down and building the same level again, so the
        /// score, the multiplier, the budget and the cluster all start over. <see cref="BuildLevel"/> is the
        /// real reload the result screen offers as Retry — it is the same path a missed score gate used to take
        /// inline, now behind a button.
        /// </summary>
        private void RetryLevel()
        {
            BuildLevel(_levelIndex);
            EnterPlaying();
        }

        /// <summary>
        /// Builds the next entry of the set and drops straight into it. Only ever called from the "Next Level"
        /// button, which is itself only shown when a next entry exists — see <see cref="RefreshResultScreen"/>.
        /// </summary>
        private void AdvanceLevel()
        {
            BuildLevel(_levelIndex + 1);
            EnterPlaying();
        }

        /// <summary>
        /// Freezes the session and puts the result screen over the stopped frame, in the same state a pause
        /// uses: the sim stands still (the early return in <see cref="Update"/>), the heavy scrim dims what is
        /// behind it, and Myra owns the input. Called when a level ends — see <see cref="FinishLevel"/>.
        /// </summary>
        private void ShowResultScreen()
        {
            _state = GameState.Paused;

            //RefreshResultScreen runs from inside ShowMenuScreen, so the snapshot is taken the instant the screen
            //is shown and not a frame later; _score is frozen with the sim, so it does not move again
            ShowMenuScreen(MenuScreen.Result);

            Console.WriteLine($"[level] Result for '{LevelName(_levelIndex)}': {_pendingOutcome}" + (_pendingOutcome == LevelOutcome.Failed ? $" ({_pendingFailure})" : "")
                + $", score {_score.Score}");
        }

        /// <summary>
        /// Writes the outcome onto the result screen's widgets from the score snapshot — the heading, the reason
        /// (on a fail), the breakdown rows (on a clear), and the two buttons' visibility. Built once, the tree
        /// is reused for every level end, so everything the screen says about <i>this</i> end arrives here.
        /// </summary>
        private void RefreshResultScreen()
        {
            //The screen is built at load, before any level has been played; with nothing to say, say nothing
            //rather than read fields that are still at their defaults (see RefreshSettingsLabels for the same
            //guard against running before the first build)
            if (_resultHeading == null) return;

            bool lastEntry = _levelSet == null || _levelIndex + 1 >= _levelSet.Count;
            bool cleared = _pendingOutcome == LevelOutcome.Cleared;

            //"Campaign complete" only when there actually was a campaign — a set of more than one level cleared
            //to its end. A single-level set (or none at all) is just a level cleared, and calling it a campaign's
            //end overstates what happened and hides the Retry that is still the point of the screen.
            bool campaignComplete = cleared && lastEntry && _levelSet != null && _levelSet.Count > 1;

            //Brightness, not colour: "FAILED" is the same grey as "CLEARED", and the reason below is what tells
            //them apart — see the palette comment for why nothing here carries a hue.
            _resultHeading.Text = campaignComplete ? "CAMPAIGN COMPLETE" : (cleared ? "CLEARED" : "FAILED");

            //The reason and the score reached are only on a fail. Hidden rather than left blank on a clear, so
            //they take no space. The reason is worded here and not where the loss was detected: the wording is
            //a display concern, and a message built at the point of detection carries the figures that were
            //convenient there — which is how "a ball at -5,58 <= -5,50" once reached a player.
            bool failed = _pendingOutcome == LevelOutcome.Failed;

            _resultReason.Text = failed ? FailureText(_pendingFailure) : string.Empty;
            _resultReason.Visible = failed;

            //Only for a hard loss: a clear short of the gate shows its total in the breakdown below, and saying
            //it twice on one screen reads as two different numbers until you check that they agree.
            bool hardLoss = failed && _pendingFailure != LevelFailure.ShortOfGate;

            _resultFailedScore.Text = hardLoss ? $"Score {_score.Score:N0}" : string.Empty;
            _resultFailedScore.Visible = hardLoss;

            //The breakdown is shown whenever the FIELD was cleared, which includes a clear that fell short of
            //the gate — there the numbers are the most useful thing on the screen, because they are where the
            //score the player missed by came from. A hard loss shows none of it: no completion bonus was
            //awarded, and partial rows would explain a total nobody is being offered.
            bool fieldCleared = cleared || _pendingFailure == LevelFailure.ShortOfGate;

            _resultBreakdown.Visible = fieldCleared;
            if (fieldCleared)
            {
                _resultMatchedDetail.Text = $"{_score.MatchedBalls} × {ScoreKeeper.MatchedBallPoints}";
                _resultMatchedValue.Text = (_score.MatchedBalls * ScoreKeeper.MatchedBallPoints).ToString("N0", CultureInfo.InvariantCulture);
                _resultOrphanedDetail.Text = $"{_score.OrphanedBalls} × {ScoreKeeper.OrphanedBallPoints}";
                _resultOrphanedValue.Text = (_score.OrphanedBalls * ScoreKeeper.OrphanedBallPoints).ToString("N0", CultureInfo.InvariantCulture);
                _resultStreakValue.Text = _score.StreakBonus.ToString("N0", CultureInfo.InvariantCulture);
                //What was AWARDED, not what recomputing it now would give. The level is held on screen for a
                //beat after it is cleared, and a player firing into the empty field meanwhile moves the balls
                //remaining — so a recomputed row does not add up to the total beneath it.
                _resultUnusedDetail.Text = _score.ShotsRemaining.HasValue
                    ? $"{_score.UnusedShotsAwarded} × {ScoreKeeper.UnusedShotPoints}"
                    : "—";
                _resultUnusedValue.Text = _score.CompletionBonusAwarded.ToString("N0", CultureInfo.InvariantCulture);
                _resultTotalValue.Text = _score.Score.ToString("N0", CultureInfo.InvariantCulture);

                int needed = LevelMinScore(_levelIndex);
                _resultNeededValue.Text = needed > 0 ? $"needed {needed:N0}" : string.Empty;
            }

            //Next Level is shown only when the level was passed AND there is another entry to go to. Absent,
            //not disabled, when neither holds — a greyed-out button over a frozen frame is a thing the player
            //cannot do, which reads as the game being broken rather than as the level being the last.
            _nextLevelButton.Visible = cleared && !lastEntry;
        }

        /// <summary>
        /// The settings. Every value is a button that cycles it rather than a slider or a drop-down: one
        /// widget kind, one click, and nothing that can be left half-dragged — and each change takes effect
        /// where it is made, so what the scene behind the panel looks like <i>is</i> the preview.
        /// </summary>
        private Widget BuildSettingsScreen()
        {
            VerticalStackPanel column = MenuColumn();
            column.Widgets.Add(ScreenHeading("SETTINGS"));

            Grid grid = new()
            {
                ColumnSpacing = Scaled(58),
                RowSpacing = Scaled(24),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 43),
            };
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));

            AddSettingRow(grid, 0, "Fullscreen", ToggleFullscreen, out _fullscreenValue);
            AddSettingRow(grid, 1, "Antialiasing", CycleSupersampling, out _ssaaValue);
            AddSettingRow(grid, 2, "Exposure", CycleExposure, out _exposureValue);
            AddSettingRow(grid, 3, "Sky", CycleSkyDome, out _skyValue);
            AddSettingRow(grid, 4, "FPS counter", ToggleFpsOverlay, out _fpsValue);

            column.Widgets.Add(grid);
            column.Widgets.Add(MenuButton("Back", BackFromSubScreen));

            return ScreenRoot(Plate(column), CurrentScrim());
        }

        private void AddSettingRow(Grid grid, int row, string caption, Action onClick, out Label value)
        {
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));

            Label captionLabel = new()
            {
                Text = caption,
                Font = _menuFontBody,
                TextColor = MENU_TEXT,
                VerticalAlignment = VerticalAlignment.Center,
            };

            Grid.SetColumn(captionLabel, 0);
            Grid.SetRow(captionLabel, row);
            grid.Widgets.Add(captionLabel);

            Button button = MenuButton(string.Empty, onClick, out value);
            button.Width = Scaled(SETTING_VALUE_WIDTH);

            Grid.SetColumn(button, 1);
            Grid.SetRow(button, row);
            grid.Widgets.Add(button);
        }

        /// <summary>Writes the current value onto each setting's button. Cheap, and only run on a change.</summary>
        private void RefreshSettingsLabels()
        {
            //The display hotkeys work before the menu has been built (LoadContent runs after Initialize), and
            //there is nothing to write onto until it has been
            if (_fullscreenValue == null) return;

            _fullscreenValue.Text = _fullscreen ? "On" : "Off";
            _ssaaValue.Text = _supersampleFactor == 1 ? "Off" : _supersampleFactor + "×";
            _exposureValue.Text = _exposure.ToString("0.0", CultureInfo.InvariantCulture);
            _skyValue.Text = _skyDome.ToString(CultureInfo.InvariantCulture);
            _fpsValue.Text = _info.Visible ? "On" : "Off";
        }

        /// <summary>
        /// Supersampling, the dominant frame cost at a high resolution and the first dial a weak machine
        /// reaches for. Off means 8× MSAA instead (see <see cref="EnsureSceneTarget"/>) — multisampling
        /// antialiases geometry edges but not shading, so it only earns its memory with supersampling off.
        /// </summary>
        private void CycleSupersampling()
        {
            SetSupersampleFactor(_supersampleFactor switch { 1 => 2, 2 => 4, _ => 1 });

            //The player has now said what they want, so the adaptive path stops second-guessing them — and
            //the notice about what it did has been answered and goes away.
            _qualitySettled = true;

            if (_qualityNotice != null) _qualityNotice.Visible = false;
        }

        private void CycleExposure()
        {
            _exposure += EXPOSURE_STEP;

            //A command-line exposure can start anywhere, so this wraps on the ceiling rather than assuming
            //the value is already on the ladder
            if (_exposure > EXPOSURE_MAX + Constants.THOUSANDTH) _exposure = EXPOSURE_MIN;

            _tonemapEffect.Parameters["Exposure"].SetValue(_exposure);

            RefreshSettingsLabels();
        }

        private void CycleSkyDome()
        {
            SetSkyDome((byte)(_skyDome == SKY_DOME_COUNT ? 1 : _skyDome + 1));
            RefreshSettingsLabels();
        }

        private Widget BuildSceneSelectScreen()
        {
            VerticalStackPanel column = MenuColumn();
            column.Widgets.Add(ScreenHeading("SCENE"));

            for (int i = 0; i < SCENE_COUNT; i++)
            {
                //Captured per iteration, not off the loop variable's final value
                SceneKind scene = (SceneKind)i;
                column.Widgets.Add(MenuButton(SCENE_NAMES[i], () => ChooseScene(scene), out _sceneLabels[i]));
            }

            column.Widgets.Add(new Label
            {
                Text = "Applies at once — the menu and the game both play in it.",
                Font = _menuFontSmall,
                TextColor = MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 29, 0, 29),
            });
            column.Widgets.Add(MenuButton("Back", BackFromSubScreen));

            return ScreenRoot(Plate(column), CurrentScrim());
        }

        private void ChooseScene(SceneKind scene)
        {
            SetScene(scene);
            MarkSelectedScene();
        }

        /// <summary>
        /// Marks the scene in use, so the screen says where you are as well as where you can go. Brightness,
        /// not colour: the one in use is stated white and the rest step back to grey, which reads the same over
        /// a neon city and over a snowfield.
        /// </summary>
        private void MarkSelectedScene()
        {
            for (int i = 0; i < SCENE_COUNT; i++)
                _sceneLabels[i].TextColor = (SceneKind)i == _scene ? MENU_TEXT : MENU_TEXT_DIM;
        }

        private Widget BuildAboutScreen()
        {
            VerticalStackPanel column = MenuColumn();

            column.Widgets.Add(ScreenHeading("ABOUT"));
            column.Widgets.Add(AboutParagraph(
                GAME_TITLE + " is a 3D bubble shooter: shoot coloured balls at a cluster hanging from a glass ceiling. "
                + "Three or more of one colour let go and fall — and take with them everything they were the "
                + "last anchor for."));
            column.Widgets.Add(AboutParagraph(
                "Controls:  the mouse aims,  left button or space fires,  right button leans in along the "
                + "barrel,  A/D traverses the carriage,  Esc pauses,  F11 toggles fullscreen,  F12 hides the "
                + "FPS counter."));
            column.Widgets.Add(AboutParagraph(
                "Built on MonoGame (DirectX 11) and BepuPhysics 2. The scenes, the balls and the city are all "
                + "procedural — no models, only code. Typeface Inter (SIL OFL 1.1)."));
            column.Widgets.Add(AboutParagraph("github.com/AntoninPrazsky/BS3D"));

            column.Widgets.Add(MenuButton("Back", BackFromSubScreen));

            return ScreenRoot(Plate(column), CurrentScrim());
        }

        private Label AboutParagraph(string text) => new()
        {
            Text = text,
            Font = _menuFontSmall,
            TextColor = MENU_TEXT_BODY,
            Wrap = true,
            Width = Scaled(ABOUT_TEXT_WIDTH),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = ScaledThickness(0, 0, 0, 34),
        };

        /// <summary>
        /// A dark plate behind a column that carries actual prose. A scrim alone is not enough for small text
        /// over a moving scene — every line lands on a different background — and darkening the whole scrim
        /// would throw away the scene the screen is standing in. No frame: the edge of the plate is the tone
        /// step itself, which is all it takes, and a drawn border is one more shape to fight the backdrop.
        /// The button screens need no plate at all — a button carries its own background.
        /// </summary>
        private Panel Plate(Widget content)
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
        private static Panel ScreenRoot(Widget content, IBrush scrim)
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

        private VerticalStackPanel MenuColumn() => new()
        {
            Spacing = Scaled(MENU_COLUMN_SPACING),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        private Label ScreenHeading(string text) => new()
        {
            Text = text,
            Font = _menuFontHeading,
            TextColor = MENU_TEXT,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = ScaledThickness(0, 0, 0, 43),
        };

        private Button MenuButton(string text, Action onClick) => MenuButton(text, onClick, out _);

        /// <summary>
        /// One menu entry. Myra's default button style is a framed grey tool button, so every brush is stated
        /// here instead: dark glass at rest, and the pointer <b>lifts</b> it with a wash of white rather than
        /// tinting it — see the palette above for why nothing in this menu carries a hue. No border either:
        /// the tone step at the button's edge is enough to read it as a control, and a drawn frame over seven
        /// different backdrops is a shape competing with all of them.
        /// </summary>
        private Button MenuButton(string text, Action onClick, out Label label)
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
                Background = new SolidBrush(MENU_BUTTON),
                OverBackground = new SolidBrush(MENU_BUTTON_OVER),
                PressedBackground = new SolidBrush(MENU_BUTTON_PRESSED),

                //Myra's stylesheet gives a button a border of its own, so it has to be explicitly cleared
                Border = null,
                BorderThickness = new Thickness(0),
            };

            button.Click += (_, _) => onClick();

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
        /// merely orbiting it, which is why the ceiling's <i>mesh and renderer</i> are built here while its
        /// kinematic <i>body</i> belongs to the simulation and is built with the rest of the world. Building
        /// the renderer here is also what keeps <see cref="SkyLitRenderers"/> complete before the first game.
        /// </para>
        /// </summary>
        private void BuildScene()
        {
            _unitBox = new BoxMesh(GraphicsDevice, 1f, 1f, 1f);
            _city = new City(seed: 20260720, arenaHalfExtent: ISLAND_RADIUS, config: _cityConfig);

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
            //field, so FitCeilingToMap makes it (and remakes it on every level) — which is why
            //SkyLitRenderers tolerates a null ceiling renderer and ApplySkyLighting runs again after a load.
        }

        /// <summary>
        /// Stands up the simulation and everything in it that does not move: the kinematic glass ceiling the
        /// cluster hangs from, and the island's floor — which is the drain's own surface, that being the only
        /// thing here a ball can rest on or run down.
        /// </summary>
        private void BuildPhysicsWorld()
        {
            //One dispatcher and one pool for the whole process. ContactEvents sizes its per-worker queues from
            //the dispatcher's thread count, so the dispatcher must exist first and must outlive the simulation.
            _threadDispatcher = new BepuUtilities.ThreadDispatcher(Environment.ProcessorCount);
            _bufferPool = new BufferPool();
            _events = new ContactEvents(_threadDispatcher, _bufferPool);

            //Both callback types are structs, copied by value into the simulation; _events survives that
            //because it is a class reference held inside one of them. SolveDescription is
            //(velocityIterationCount, substepCount) in that order — eight iterations, one substep — and those
            //are tuned together with the contact material and the BallSocket spring, so they move together.
            _simulation = Simulation.Create(
                _bufferPool,
                new NarrowPhaseCallbacks(_events),
                new PoseIntegratorCallbacks(new System.Numerics.Vector3(0f, Constants.EARTH_GRAVITY, 0f)),
                new SolveDescription(8, 1));

            //No _events.Initialize(_simulation) here: Simulation.Create has already called
            //NarrowPhaseCallbacks.Initialize, which is what initialises it. Calling it again would hook its
            //BeforeCollisionDetection handler onto the timestepper a second time.

            BuildCeilingBody();
            BuildFunnelPhysics();

            //The template every shot is stamped from. The collidable comes from the bare shape index rather
            //than from a CollidableDescription with a speculative margin, and that is load-bearing: it is what
            //gives the shot continuous collision detection. At SHOOT_SPEED a ball crosses several diameters in
            //one step, and a discrete test would let it pass clean through the cluster.
            Sphere ballShape = new(BallsConstraintsBuilder.BALL_RADIUS);
            _shotBall = BodyDescription.CreateDynamic(
                new System.Numerics.Vector3(),
                ballShape.ComputeInertia(BallsConstraintsBuilder.BALL_MASS),
                BallsConstraintsBuilder.GetSphereShapeIndex(_simulation),
                Constants.HUNDREDTH); //sleep threshold, via the implicit conversion to BodyActivityDescription
        }

        /// <summary>
        /// The glass plate, as physics. Kinematic rather than static because a <c>BallSocket</c> needs a body at
        /// both ends — Bepu constraints do not take statics — and the whole cluster hangs from this one.
        /// </summary>
        private void BuildCeilingBody()
        {
            //Sized to the field with the same one-unit margin the drawn plate has: a field's worth of balls is
            //one unit wider than its cell count, since odd levels are shifted by half and a radius is another.
            //The same figures FitCeilingToMap gave the drawn box, so the glass and the collidable agree.
            Box box = new(_map.StageSizeX + 1f, 1f, _map.StageSizeZ + 1f);
            TypedIndex shape = _simulation.Shapes.Add(box);

            BodyHandle handle = _simulation.Bodies.Add(BodyDescription.CreateKinematic(
                new System.Numerics.Vector3(0f, _ceilingY, 0f),
                new CollidableDescription(shape, 0.1f),
                new BodyActivityDescription(Constants.HUNDREDTH)));

            _ceiling = new KinematicBody(new BodyReference(handle, _simulation.Bodies), handle);
        }

        /// <summary>
        /// Begins one step of the ceiling's descent: lowers the target by <see cref="CEILING_DESCENT_PER_STEP"/>,
        /// clamped at the death line so an overlong level cannot drive the glass through the gun. The body itself
        /// does not move here — <see cref="UpdateCeilingDescent"/> slides it to the target, which is what keeps a
        /// hundred constrained bodies from being jerked in a single write.
        /// </summary>
        private void StartCeilingDescent()
        {
            //No target to reach if the glass is already as low as it can go — further steps would be a no-op and
            //a needless log, and clamping here is what stops an inconsistent level (more steps than the geometry
            //allows) from scraping the body past the death line.
            if (_ceilingTargetY <= CEILING_DEATH_Y) return;

            _ceilingTargetY = MathF.Max(CEILING_DEATH_Y, _ceilingTargetY - CEILING_DESCENT_PER_STEP);
            _ceilingDescending = true;

            Console.WriteLine($"[ceiling] Step to {_ceilingTargetY:F2} (death line {CEILING_DEATH_Y:F2})"
                + $", shots fired {_score.ShotsFired}");
        }

        /// <summary>
        /// Slides the ceiling body toward <see cref="_ceilingTargetY"/> at <see cref="CEILING_DESCENT_SPEED"/>,
        /// one frame's worth at a time, and refreshes the drawn world matrix to match. Called before the physics
        /// step so the solver works against the moved body this frame, letting the contact between a descending
        /// cluster and anything below it resolve rather than interpenetrate.
        /// </summary>
        private void UpdateCeilingDescent(float elapsed)
        {
            if (!_ceilingDescending) return;

            //Equal within a hair means the slide is done — a frame that would otherwise move a thousandth of a
            //unit and never quite arrive. Snap, stop, and the matrix reflects the final pose exactly.
            if (MathF.Abs(_ceilingY - _ceilingTargetY) <= CEILING_DESCENT_SPEED * elapsed)
            {
                _ceilingY = _ceilingTargetY;
                _ceilingDescending = false;
            }
            else
            {
                _ceilingY -= CEILING_DESCENT_SPEED * elapsed;
            }

            _ceiling.BodyReference.Pose.Position = new System.Numerics.Vector3(0f, _ceilingY, 0f);
            _ceiling.RefreshWorld();
        }

        /// <summary>
        /// The island's whole floor, and it is the drain's own surface: the sloped cone plus the flat stone ring
        /// from its rim out to the island's edge, as one triangle mesh. Balls rest on the ring, run down the
        /// cone at its ~55° and drop through the hole; past the ring they fall off the island's edge into the
        /// city. Either way the kill plane takes them.
        /// <para>
        /// Every quad goes in with <b>both</b> windings — eight triangles a segment, not four. A Bepu mesh
        /// triangle only collides on its front face, and rather than depend on getting the winding right for a
        /// surface that is met from above, from inside the funnel and from underneath, it is made double-sided
        /// deliberately.
        /// </para>
        /// </summary>
        private void BuildFunnelPhysics()
        {
            const int segments = FUNNEL_SEGMENTS;
            float depth = ISLAND_Y - FUNNEL_BOTTOM_Y;

            //Take gives exactly the requested length, which is what the Mesh constructor is handed; TakeAtLeast
            //would round the count up and leave uninitialised triangles at the end of the buffer.
            _bufferPool.Take<Triangle>(segments * 8, out Buffer<Triangle> triangles);

            for (int s = 0; s < segments; s++)
            {
                float a0 = (float)(s / (double)segments * Math.PI * 2.0);
                float a1 = (float)((s + 1) / (double)segments * Math.PI * 2.0);

                //Local space: the rim at y = 0 and the hole at y = -depth, so the static's own pose is what
                //puts the rim flush with the island's stone top
                System.Numerics.Vector3 t0 = Ring(a0, FUNNEL_TOP_RADIUS, 0f);
                System.Numerics.Vector3 t1 = Ring(a1, FUNNEL_TOP_RADIUS, 0f);
                System.Numerics.Vector3 h0 = Ring(a0, FUNNEL_HOLE_RADIUS, -depth);
                System.Numerics.Vector3 h1 = Ring(a1, FUNNEL_HOLE_RADIUS, -depth);
                System.Numerics.Vector3 r0 = Ring(a0, ISLAND_RADIUS, 0f);
                System.Numerics.Vector3 r1 = Ring(a1, ISLAND_RADIUS, 0f);

                int b = s * 8;

                //The cone wall, both faces
                triangles[b] = new Triangle(t0, h0, t1);
                triangles[b + 1] = new Triangle(t1, h0, h1);
                triangles[b + 2] = new Triangle(t0, t1, h0);
                triangles[b + 3] = new Triangle(t1, h1, h0);

                //The flat stone ring from the rim out to the island's edge, both faces
                triangles[b + 4] = new Triangle(t0, t1, r1);
                triangles[b + 5] = new Triangle(t0, r1, r0);
                triangles[b + 6] = new Triangle(t0, r1, t1);
                triangles[b + 7] = new Triangle(t0, r0, r1);
            }

            static System.Numerics.Vector3 Ring(float angle, float radius, float y) =>
                new(radius * MathF.Cos(angle), y, radius * MathF.Sin(angle));

            Mesh mesh = new(triangles, System.Numerics.Vector3.One, _bufferPool);
            TypedIndex shape = _simulation.Shapes.Add(mesh);

            _simulation.Statics.Add(new StaticDescription(new System.Numerics.Vector3(0f, ISLAND_Y, 0f), shape));
        }

        /// <summary>
        /// The procedural pyramid the game shipped with, used only when no level file could be read: a
        /// <see cref="BallsMap"/> carved to a stepped square pyramid, apex down, hanging at the top of a
        /// taller field so the empty levels underneath are room for shot balls to attach into — the same
        /// arrangement a map file carries.
        /// </summary>
        private static BallsMap BuildFallbackMap()
        {
            BallsMap map = new(FALLBACK_X, FALLBACK_Z, FALLBACK_FIELD_LEVELS);

            //Its own generator off a fixed seed, so the pile is reproducible however many shots the
            //magazine's unseeded one has drawn by the time this runs
            Random layout = new(FALLBACK_SEED);

            //The pyramid is built about the centre of the field's topmost level, because that is the level
            //BallsMap.Center() puts on the origin. Odd levels are shifted by +0.5 in X and Z, so which
            //centre that is depends on its parity.
            float topShift = LevelShift(FALLBACK_FIELD_LEVELS - 1);
            float axisX = (FALLBACK_X - 1) * Constants.HALF + topShift;
            float axisZ = (FALLBACK_Z - 1) * Constants.HALF + topShift;

            for (byte level = 0; level < FALLBACK_LEVELS; level++)
            {
                byte fieldLevel = (byte)(level + FALLBACK_EXTRA_LEVELS);

                //Half the pyramid's width here: nothing but the apex cell at the bottom, growing half a unit
                //per level up to the full base at the top. Both this and the cell positions are whole
                //multiples of a half, hence exact in binary — which is why the test below needs no tolerance.
                float half = level * Constants.HALF;
                float shift = LevelShift(fieldLevel);

                for (byte x = 0; x < FALLBACK_X; x++)
                    for (byte z = 0; z < FALLBACK_Z; z++)
                    {
                        //Measured against where the cell actually sits, not its raw index, so a level's
                        //own half-unit offset cannot throw the flank out of line
                        if (MathF.Abs(x + shift - axisX) > half) continue;
                        if (MathF.Abs(z + shift - axisZ) > half) continue;

                        map.PutBallAt(x, z, fieldLevel, layout.Next(DEFAULT_BALL_TYPES.Length) switch
                        {
                            0 => DEFAULT_BALL_TYPES[0],
                            1 => DEFAULT_BALL_TYPES[1],
                            2 => DEFAULT_BALL_TYPES[2],
                            _ => DEFAULT_BALL_TYPES[3],
                        });
                    }
            }

            return map;
        }

        /// <summary>
        /// Mirrors the loaded lattice into Bepu bodies, which is what the frame actually draws, and wires up
        /// the contact handler that catches a shot landing on it.
        /// </summary>
        private void BuildCluster()
        {
            //The lattice mirrored into bodies, one per occupied cell, constrained to its neighbours and — on
            //the top level — to the ceiling. The offset is what creates them where the cluster is drawn: a
            //BallsMap reckons in its own grid frame and this game draws that frame lower (and, for an odd
            //field, half a cell across — see _clusterWorldOffset), so the empty levels below the layout do
            //not raise it. The bodies have to be in world coordinates because everything else the simulation
            //touches is — the floor, the ceiling, the muzzle a shot leaves from, the kill plane. It is
            //applied to the body positions and to nothing else: the constraint anchors are differences of two
            //grid positions, so the offset cancels out of them.
            _physicsBalls = BallsConstraintsBuilder.BuildBallsStructure(
                _map.GetStaticBallsArray(), ref _simulation, _ceiling.BodyReference,
                _clusterWorldOffset.ToNumerics());

            //What happens on a hit lives in the handler: the snap into the lattice, the constraints, and the
            //match rule. It gets the very list instances the frame draws from, and the same offset, so it can
            //take a world contact down into the grid frame to ask the map about it and bring the answer back up.
            _eventHandler = new BallContactEventHandler(_simulation, _events, _ceiling, _map, _physicsBalls,
                _shotBalls, _fallingBalls, _clusterWorldOffset);

            //The handler reports what a shot did; what it is worth is the scorer's business. Subscribed on the
            //handler the level just built, and the handler is rebuilt with it, so there is nothing to unhook.
            //Both go through a method that reads _score rather than binding the instance the field happens to
            //hold now: the scorer is replaced per level, and a handler holding a stale one would score into a
            //keeper nothing reads.
            _eventHandler.BallLanded += OnBallLanded;
            _eventHandler.ShotSpent += OnShotSpent;

            Console.WriteLine($"[game] {_map.GetBallsCount()} balls in the cluster, "
                + $"{_simulation.Solver.CountConstraints()} constraints");
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
                //set is then obvious in one place before a single level is played. Nothing reads these values
                //yet — the budget, the score gate and the descending ceiling are still to come — so what this
                //reports today is what the file says, not what the game does with it.
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

        /// <summary>
        /// Installs the map for one entry of the level set, and everything the field's size and depth decide:
        /// where the lattice meets the world, how high the glass hangs, how big it is, and which colours the
        /// magazine may load. Nothing here touches the simulation — it runs before
        /// <see cref="BuildPhysicsWorld"/>, which needs the ceiling's height and footprint to place its body.
        /// <para>
        /// The file is parsed <b>before</b> anything is installed, so a broken level leaves the previous state
        /// alone and simply falls back to the built-in one, exactly as the Testbed's loader does.
        /// </para>
        /// </summary>
        private void InstallLevel(int index)
        {
            BallsMap map = null;

            if (_levelSet != null && index >= 0 && index < _levelSet.Count)
            {
                string path = _levelSet.ResolvePath(index);

                try
                {
                    //Both a full level file (marked "bs3d-level", carrying a scene and a sky as well) and a
                    //plain map file are .json, so the loader probes exactly as the Testbed's does. A level's
                    //scene is honoured; the player's own pick from the menu stands when the level has none.
                    if (Level.IsLevelFile(path))
                    {
                        Level level = Level.Load(path);
                        map = new BallsMap(level.Map);

                        if (level.Scene != null) SetScene(level.Scene.Kind);
                        SetSkyDome(Math.Clamp(level.SkyDome, (byte)1, (byte)SKY_DOME_COUNT));
                    }
                    else map = new BallsMap(path);

                    Console.WriteLine($"[levels] Loaded {index + 1}/{_levelSet.Count} '{_levelSet.DisplayName(index)}' "
                        + $"({_levelSet.DescribeRules(index)}) from '{path}'");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[levels] Failed to load '{path}': {e.Message}");
                    map = null;
                }
            }

            _map = map ?? BuildFallbackMap();
            _map.Center();

            FitFieldToMap();
            FitCeilingToMap();

            //The gun and the lens both move with the field's size, and each is placed off the other
            FitCannonAndGameCameraToLevel();

            RecountBallTypes();

            for (int i = 0; i < MAGAZINE_SIZE; i++)
            {
                _magazine[i] = RandomBallType();

                //Nothing is mid-transmute in a queue that has just been dealt, and a level loaded over a
                //session that was would otherwise inherit its half-finished dissolves
                _magazineTransmute[i] = 0f;
                _magazineFrom[i] = _magazine[i];
            }

            //A fresh scorer per level, holding that entry's rules. Built even when the level fell back to the
            //built-in map, which then has no rules at all and so an unlimited budget and a still ceiling — the
            //same thing an entry that authors no "shots" or "ceilingStep" means.
            _score = new ScoreKeeper(LevelShotBudget(index), LevelCeilingStep(index));
        }

        /// <summary>
        /// A shot has landed in the lattice, having cut <paramref name="released"/> loose. Zero of both means
        /// it stuck without completing a group, which the scorer treats as a spent shot.
        /// </summary>
        private void OnBallLanded(BallsReleased released)
        {
            ScoreAward award = _score.Landed(released.Matched, released.Orphaned);

            //Temporary: until the HUD (#60) exists there is nowhere to show this, and a score that is counted
            //but never seen is indistinguishable from one that is not counted at all. A shot that dropped
            //nothing logs nothing — the reset still shows, as the next scoring shot coming back at x1.
            //Spelled out in ASCII rather than through ScoreAward.ToString(), whose "×" is a proper
            //multiplication sign: the console this lands in is a legacy code page and mangles anything outside
            //it. Logged here rather than at the end of the method so it reads before the level's own lines.
            if (award.Scored)
                Console.WriteLine($"[score] {released} -> +{award.Points} x{award.Multiplier}"
                    + $"  score {_score.Score} (base {_score.BaseScore} + streak {_score.StreakBonus})"
                    + $", next x{_score.Multiplier}"
                    + $", balls left {(_score.ShotsRemaining?.ToString() ?? "unlimited")}");

            //The cluster just changed — a ball joined it, and a group may have left. Recount before anything
            //asks what may be loaded, and re-colour whatever is already in the barrel and has just gone dead.
            RecountBallTypes();
            Transmute();

            CheckLevelCleared();
        }

        /// <summary>A shot is over without having landed. The streak breaks.</summary>
        private void OnShotSpent() => _score.Missed();

        /// <summary>
        /// Has the field just been emptied? That is the goal of a level: release every ball so it falls.
        /// <para>
        /// Tested only here, on a landing, which is the one thing that can empty the field — and testing it
        /// anywhere else would be worse than redundant. Polling it per frame would declare a level authored
        /// with no balls won before the player had fired a shot, and would keep declaring it.
        /// </para>
        /// <para>
        /// The completion bonus is awarded <b>now</b> rather than when the level actually ends, so that shots
        /// fired into the empty field during the pause below cannot eat the balls the player finished with.
        /// A shot still in flight at this moment is harmless: there is nothing left for it to hit, and the
        /// body goes with the simulation when the next level replaces it.
        /// </para>
        /// </summary>
        private void CheckLevelCleared()
        {
            if (_levelLost || _clearedCountdown > 0f || _map.GetBallsCount() > 0) return;

            int bonus = _score.AwardCompletionBonus();
            _clearedCountdown = LEVEL_CLEARED_BEAT;

            Console.WriteLine($"[level] Cleared '{LevelName(_levelIndex)}' with {_score.Score}"
                + $" (+{bonus} for {_score.ShotsRemaining?.ToString() ?? "unlimited"} unused)"
                + $", needed {LevelMinScore(_levelIndex)}");
        }

        /// <summary>
        /// Has the level been lost? The two pressures that lose it — a spent budget with the field uncleared, and
        /// the ceiling reaching the death line — are decided here, after the physics step, once every shot in
        /// flight has resolved. Either one alone loses; both are checked because either can be the one a last shot
        /// earned.
        /// </summary>
        /// <remarks>
        /// The spent budget is tested last — after a possible clear this same frame has run. The last ball of a
        /// budget may be the one that empties the field, and a loss called before <see cref="OnBallLanded"/> had
        /// its say would steal that win. So the budget only loses when nothing is in flight and the field is still
        /// standing. The ceiling, by contrast, is an immediate loss the moment a ball crosses the line — a descent
        /// can push one there between landings, so it cannot wait on the same event the budget does.
        /// </remarks>
        private void CheckLevelLost()
        {
            //Already ending — a cleared countdown or a loss in flight. Testing further would re-trigger a loss
            //on top of a clear or a teardown already underway.
            if (_clearedCountdown > 0f || _levelLost) return;

            //The ceiling reaching the death line. Live poses are in _physicsBalls (the lattice in _map holds
            //cells, not bodies); the loop mirrors DrawBallsInstanced, including the null check for cells a
            //release has emptied.
            XZLevel size = XZLevel.FromArray(_physicsBalls);

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        PhysicsBall ball = _physicsBalls[x, z, level];
                        if (ball == null) continue;

                        if (ball.BallReference.Pose.Position.Y <= CEILING_DEATH_Y)
                        {
                            LoseLevel(LevelFailure.ClusterReachedLine,
                                $"a ball at {ball.BallReference.Pose.Position.Y:F2} <= {CEILING_DEATH_Y:F2}");
                            return;
                        }
                    }

            //The budget spent with the field uncleared — but only once every shot has resolved, so the last ball
            //fired has had its chance to clear. A ball still in flight could be that chance, and a loss called
            //beneath it would steal the win.
            if (_score.OutOfShots && _shotBalls.Count == 0 && _map.GetBallsCount() > 0)
                LoseLevel(LevelFailure.OutOfBalls,
                    $"budget {LevelShotBudget(_levelIndex)?.ToString() ?? "unlimited"}, fired {_score.ShotsFired}");
        }

        /// <summary>
        /// Ends the level as a loss for the stated reason. It does <b>not</b> tear the session down here — a loss
        /// can be reached from the middle of <see cref="Update"/> (a shot that spends the budget, a frame that
        /// slides the ceiling past the line), and rebuilding mid-frame would leave the rest of the frame running
        /// against a simulation that no longer exists. Instead it sets the outcome and hands the player the result
        /// screen, whose Retry button does the real reload — the same screen a cleared level lands on.
        /// </summary>
        /// <param name="diagnostic">
        /// The figures behind the loss. <b>Logged and never shown</b>: what a player needs is which limit ran
        /// out, and a world-space Y against a death line tells them nothing they can act on.
        /// </param>
        private void LoseLevel(LevelFailure failure, string diagnostic)
        {
            //Once only: a descent and a budget can reach their lines on the same frame, and a loss in flight
            //must not stack a second screen onto the first.
            if (_levelLost) return;
            _levelLost = true;

            Console.WriteLine($"[level] Lost '{LevelName(_levelIndex)}': {failure} ({diagnostic}), score {_score.Score}");

            _pendingOutcome = LevelOutcome.Failed;
            _pendingFailure = failure;
            ShowResultScreen();
        }

        /// <summary>
        /// What the player is told about a loss. The two hard limits carry no figures at all — a world-space Y
        /// or a budget they already watched run down tells them nothing they can act on. The gate does carry
        /// one, because the number they missed by is the whole of what it is telling them.
        /// </summary>
        private string FailureText(LevelFailure failure) => failure switch
        {
            LevelFailure.OutOfBalls => "You ran out of balls.",
            LevelFailure.ClusterReachedLine => "The cluster reached the line.",
            LevelFailure.ShortOfGate => $"Cleared — but {LevelMinScore(_levelIndex):N0} was needed to unlock the next level.",
            _ => string.Empty,
        };

        /// <summary>
        /// The level is over and the collapse has played out. It does <b>not</b> act on the outcome — it decides
        /// which one it was and hands the player the result screen to choose what to do about it. The build,
        /// the teardown and the advance that used to happen here now live behind the result screen's buttons
        /// (<see cref="RetryLevel"/>, <see cref="AdvanceLevel"/>), which is what a player actually presses.
        /// <para>
        /// A cleared field is the only thing that reaches here today; a lost one reaches the same screen through
        /// <see cref="LoseLevel"/>. Both set the outcome and call <see cref="ShowResultScreen"/>, and the screen
        /// does the rest.
        /// </para>
        /// </summary>
        private void FinishLevel()
        {
            int required = LevelMinScore(_levelIndex);

            //Cleared but short of the gate is a fail the player chose — a sloppy clear — rather than a clear
            //the game then undoes. The level did not advance, and "Retry" is the way forward.
            if (_score.Score < required)
            {
                _pendingOutcome = LevelOutcome.Failed;
                _pendingFailure = LevelFailure.ShortOfGate;
            }
            else
            {
                _pendingOutcome = LevelOutcome.Cleared;
                _pendingFailure = LevelFailure.None;
            }

            ShowResultScreen();
        }

        /// <summary>What to call the level at <paramref name="index"/>, set or no set.</summary>
        private string LevelName(int index) =>
            _levelSet != null && index >= 0 && index < _levelSet.Count ? _levelSet.DisplayName(index) : "the built-in level";

        /// <summary>
        /// The score the entry at <paramref name="index"/> demands before the <b>next</b> level unlocks. Zero —
        /// what an absent rule, a missing set and an index outside it all mean — leaves clearing the field
        /// enough on its own, which is what every level is until one opts into the gate.
        /// </summary>
        private int LevelMinScore(int index) =>
            _levelSet != null && index >= 0 && index < _levelSet.Count
                ? _levelSet.Levels[index].MinScore.GetValueOrDefault()
                : 0;

        /// <summary>
        /// The ball budget the entry at <paramref name="index"/> grants, or null for unlimited — which is what
        /// an absent <c>shots</c> rule, an index outside the set and a missing set all mean. This is the read
        /// site the nullable rule is documented against: the set records only that a rule is absent, and this
        /// is where the game says what absent means.
        /// </summary>
        private int? LevelShotBudget(int index) =>
            _levelSet != null && index >= 0 && index < _levelSet.Count ? _levelSet.Levels[index].Shots : null;

        /// <summary>
        /// Shots between two descents of the glass ceiling, or null for a ceiling that holds still — which is what
        /// an absent <c>ceilingStep</c> rule, an index outside the set and a missing set all mean. Mirrors
        /// <see cref="LevelShotBudget"/>: the nullable rule is read at one site, and this is where absent is given
        /// its meaning.
        /// </summary>
        private int? LevelCeilingStep(int index) =>
            _levelSet != null && index >= 0 && index < _levelSet.Count ? _levelSet.Levels[index].CeilingStep : null;

        /// <summary>
        /// Derives everything the loaded field's size and depth decide. See <see cref="_clusterWorldOffset"/>
        /// for why the offset has an X and a Z as well as a Y.
        /// </summary>
        private void FitFieldToMap()
        {
            XZLevel size = _map.GetStaticBallsArraySize();
            byte topLevel = (byte)(size.Level - 1);

            //The residual the centring leaves: the midpoint of the top level's own cells, measured through
            //the map's public centred-position accessor rather than re-deriving its arithmetic here
            Vector3 nearCorner = _map.GetRealCenteredPosition(new XZLevel(0, 0, topLevel));
            Vector3 farCorner = _map.GetRealCenteredPosition(new XZLevel(size.X - 1, size.Z - 1, topLevel));

            _clusterWorldOffset = new Vector3(
                -(nearCorner.X + farCorner.X) * Constants.HALF,
                FIELD_TOP_Y - topLevel / Constants.SQRT_TWO,
                -(nearCorner.Z + farCorner.Z) * Constants.HALF);

            _ceilingY = FIELD_TOP_Y + CEILING_CLEARANCE;
            //At rest to start: target equals current, so nothing slides until a step is taken.
            _ceilingTargetY = _ceilingY;
            _ceilingDescending = false;
            _clusterCentreY = topLevel * Constants.HALF / Constants.SQRT_TWO + _clusterWorldOffset.Y;
        }

        /// <summary>
        /// Rebuilds the drawn glass plate at the loaded field's footprint. Its renderer is recreated here, so
        /// it starts without the sky palette — <see cref="ApplySkyLighting"/> has to run after this, exactly
        /// as it does after the Testbed's <c>FitCeilingToMap</c>.
        /// </summary>
        private void FitCeilingToMap()
        {
            _ceilingMesh?.Dispose();
            _ceilingRenderer?.Dispose();

            //Odd levels are shifted by +0.5 and a ball's radius is another 0.5, so a field's worth of balls is
            //one unit wider than its cell count; the plate covers it with that margin, as the Testbed's does.
            //It is drawn from the kinematic body's own pose, so the glass and the collidable cannot disagree.
            _ceilingMesh = new BoxMesh(GraphicsDevice, _map.StageSizeX + 1f, 1f, _map.StageSizeZ + 1f);
            _ceilingRenderer = new InstancedModelRenderer(GraphicsDevice, _ceilingMesh, CEILING_GLASS_COLOR, _instancingEffect, CEILING_GLASS_ALPHA);
        }

        /// <summary>
        /// Recounts how many balls of each colour are still hanging. The magazine may only load a colour whose
        /// count is above zero: a ball of a colour that exists nowhere in the cluster can never match anything,
        /// so it can only be parked somewhere — which grows the very cluster the player is shrinking, wastes a
        /// budgeted shot, and in the limit makes a level unwinnable.
        /// <para>
        /// A colour with fewer than <see cref="BallsConstraintsBuilder.MINIMUM_CLUSTER_SIZE"/> left is arguably
        /// already dead weight and could be dropped from the queue early. It is deliberately <b>not</b>: that
        /// changes which levels are solvable at all, which makes it a difficulty decision rather than this fix.
        /// </para>
        /// </summary>
        private void RecountBallTypes()
        {
            for (int i = 0; i < _ballsOfType.Length; i++) _ballsOfType[i] = 0;

            StaticBall[,,] balls = _map.GetStaticBallsArray();
            XZLevel size = XZLevel.FromArray(balls);

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        StaticBall ball = balls[x, z, level];
                        if (ball == null) continue;

                        int index = (int)ball.Type - 1;
                        if (index >= 0 && index < _ballsOfType.Length) _ballsOfType[index]++;
                    }
        }

        /// <summary>
        /// Re-colours every loaded ball whose colour has just been eliminated from the cluster, and starts the
        /// dissolve that shows it happening.
        /// <para>
        /// The alternative — letting a stale queue play out — costs the player up to <see cref="MAGAZINE_SIZE"/>
        /// shots on colours that cannot match anything, through no fault of their own. This is a game and not a
        /// simulation, so a ball that is already loaded may simply be re-coloured; the player will notice, and
        /// noticing is the point, because what they see is the game helping rather than the game cheating them.
        /// </para>
        /// <para>
        /// The colour changes <b>immediately</b> and the dissolve is cosmetic: firing mid-transition must give
        /// the new colour, never the dead one it is still fading out of. The replacement is drawn at random
        /// from what survives — picking whichever colour would help most would quietly make the game easier,
        /// and that is a difficulty decision, not a fix.
        /// </para>
        /// </summary>
        private void Transmute()
        {
            for (int slot = 0; slot < MAGAZINE_SIZE; slot++)
            {
                int index = (int)_magazine[slot] - 1;
                if (index >= 0 && index < _ballsOfType.Length && _ballsOfType[index] > 0) continue;

                BallType replacement = RandomBallType();
                if (replacement == _magazine[slot]) continue; //nothing survives to swap to; leave it alone

                //The ball it is fading OUT of is whatever is on screen now — which for a slot caught
                //mid-transmute is the colour it was already fading out of, not the one it never finished
                //becoming. Restarting from the visible colour is what keeps the animation continuous.
                if (_magazineTransmute[slot] <= 0f) _magazineFrom[slot] = _magazine[slot];

                Console.WriteLine($"[transmute] slot {slot}: {_magazineFrom[slot]} is gone from the cluster -> {replacement}");

                _magazine[slot] = replacement;
                _magazineTransmute[slot] = 1f;
            }
        }

        #endregion

        #region The session (menu ⇄ play)

        /// <summary>
        /// Starts playing. The world — the level's map, the simulation, the ceiling body, the drain's
        /// collision mesh and the cluster — is built here rather than in <see cref="LoadContent"/>, so the
        /// menu comes up without paying for it and a machine that never presses Play never pays at all.
        /// </summary>
        /// <param name="newGame">
        /// True to throw away a session in progress and deal the first level again; false to carry on with
        /// whatever is already standing (and to build it, if this is the first time).
        /// </param>
        private void StartGame(bool newGame)
        {
            if (newGame || !_gameBuilt) BuildLevel(0);

            EnterPlaying();
        }

        /// <summary>
        /// Tears down whatever session is standing and builds the level at <paramref name="index"/> in its
        /// place — the path a first "Play", a "New Game", a retry and an advance all take, which is the whole
        /// reason it is not inlined in <see cref="StartGame"/> any more.
        /// </summary>
        private void BuildLevel(int index)
        {
            if (_gameBuilt) TearDownGame();

            //The map first: the ceiling's height and footprint come off the field, and the ceiling body has
            //to exist before the cluster, whose top level is constrained to it.
            _levelIndex = index;
            InstallLevel(_levelIndex);

            BuildPhysicsWorld();
            BuildCluster();

            //FitCeilingToMap made a new renderer, which starts without the sky palette
            ApplySkyLighting();

            _gameBuilt = true;
            _clearedCountdown = 0f;
            _levelLost = false;
        }

        /// <summary>
        /// The one door into <see cref="GameState.Playing"/>, from a fresh start and from a resume alike. What
        /// it exists for is the input state: the cursor is captured and recentred every frame while playing
        /// and left alone in the menu, so the first frame back would otherwise read the distance from wherever
        /// the player clicked to the viewport centre as an aim delta and yank the barrel across the field —
        /// and the very click that pressed the button would arrive against a stale "released" and fire a shot
        /// nobody asked for. Clearing <see cref="_mouseAimInitialized"/> skips that first frame's aim <i>and</i>
        /// its shot test, since both live behind it.
        /// </summary>
        private void EnterPlaying()
        {
            _state = GameState.Playing;

            _mouseAimInitialized = false;
            _adsHeld = false;

            //A gamepad reports to an unfocused window and to a paused one; both triggers must be released
            //before they mean anything again. One poll on a state change, which is not a per-frame path.
            _padTriggerReleased = false;
            _previousPad = GamePad.GetState(PlayerIndex.One);

            IsMouseVisible = false;
        }

        /// <summary>Freezes the session and puts the pause menu over the frame the player was looking at.</summary>
        private void PauseGame()
        {
            _state = GameState.Paused;
            ShowMenuScreen(MenuScreen.Pause);
        }

        private void ResumeGame() => EnterPlaying();

        /// <summary>
        /// Back to the front end. The session is <b>kept</b>, not discarded — the main menu offers to resume it
        /// and to start a new game, which is the difference between a mis-click costing a click and costing a
        /// game. What it does drop is the camera: the menu's own orbit takes over from here.
        /// </summary>
        private void ReturnToMainMenu()
        {
            _state = GameState.Menu;
            ShowMenuScreen(MenuScreen.MainMenu);
        }

        /// <summary>
        /// Tears the session down so a new one can be built. The simulation is disposed outright rather than
        /// emptied ball by ball: the constraints, the bodies, the statics and the per-worker contact queues all
        /// go with it, and rebuilding is a few milliseconds. The order is <see cref="UnloadContent"/>'s and for
        /// the same reason — <see cref="ContactEvents"/> unhooks itself from the timestepper, so it has to go
        /// before the simulation it hooked into, and the pool both allocated from has to outlive the two.
        /// </summary>
        private void TearDownGame()
        {
            _events?.Dispose();
            _simulation?.Dispose();
            _threadDispatcher?.Dispose();
            _bufferPool?.Clear();

            _events = null;
            _simulation = null;
            _threadDispatcher = null;
            _bufferPool = null;
            _eventHandler = null;
            _ceiling = null;
            _map = null;
            _physicsBalls = null;

            //Cleared, never reassigned: the contact handler holds these very instances
            _shotBalls.Clear();
            _fallingBalls.Clear();
            _trails.Clear();

            _physicsAccumulator = 0f;
            _cannonRecoil = 0f;
            _magazineSlide = 0f;
            _adsBlend = 0f;

            //The magazine is not refilled here: its colours belong to a level, and InstallLevel loads the
            //next one's before the queue means anything again

            //The gun goes back to its resting orbit angle and aim, so a new game starts pointed where the
            //first one did rather than wherever the last shot left the barrel
            _cannon.Restart();

            _gameBuilt = false;
        }

        #endregion

        /// <summary>
        /// The half-unit offset a level's cells carry in X and Z: odd levels of the lattice are shifted, which
        /// is what nests each layer into the pockets of the one below. Mirrors
        /// <see cref="BallsMap.GetRealPosition"/>, which is where the shift actually happens.
        /// </summary>
        private static float LevelShift(byte level) => (level % 2) > 0 ? Constants.HALF : 0f;

        //There is no MapToWorld/WorldToMap pair here any more. The lattice-to-world offset used to be applied
        //wherever a grid position was drawn; now the bodies ARE the drawn positions, so the offset is applied
        //exactly twice — once to place them (BuildBallsStructure's worldOffset) and once inside
        //BallContactEventHandler, which takes a world contact down into the grid frame to ask the map about it.
        //Keeping a general-purpose converter around invited a third, uncounted crossing.

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
        /// anything until it is radiance.
        /// </summary>
        private void ApplySkyLighting()
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
        /// startup and the scene menu.
        /// </summary>
        private void SetScene(SceneKind scene)
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
        /// happens, shared by <see cref="SetScene"/> and the sky setting.
        /// </summary>
        private void SetSkyDome(byte number)
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
        /// the camera — goes in <see cref="Draw"/>, right before the dome does.
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
            bool edgeInputAllowed = IsActive && _wasActive;

            //The menu/pause split. Neither state runs any gameplay path — no aiming, no shooting, no physics,
            //no game camera — so the whole of the rest of this method is behind Playing, and Myra owns the
            //input instead (it reads it in Desktop.Render, at the end of Draw).
            if (_state != GameState.Playing)
            {
                UpdateMenu(gameTime, elapsed, edgeInputAllowed);
                return;
            }

            if (IsActive)
            {
                IsMouseVisible = false;

                //One XInput poll for the whole frame: UpdateInput and UpdateAim used to each poll the pad,
                //two OS queries of the same slot microseconds apart
                GamePadState pad = GamePad.GetState(PlayerIndex.One);

                UpdateInput(gameTime, edgeInputAllowed, pad);
                UpdateAim(gameTime, edgeInputAllowed, pad);
            }
            else
            {
                //The cursor belongs to the desktop again as soon as the window is not the one being played:
                //hidden over an unfocused window it simply disappears wherever the player moves it.
                IsMouseVisible = true;
                _mouseAimInitialized = false;

                //A trigger held while the window was away must be re-released before it fires
                _padTriggerReleased = false;

                //And a held precise-aim button must not keep an alt-tabbed window leaned in — the gamepad's
                //triggers report through XInput whether the window has focus or not. The blend below still
                //runs, so losing focus eases the lean out rather than dropping it.
                _adsHeld = false;
            }

            _wasActive = IsActive;

            _cannon.Update(gameTime);

            //The queue glides forward into the slot the fired ball left rather than snapping
            if (_magazineSlide > 0f) _magazineSlide *= MathF.Exp(-elapsed / MAGAZINE_SLIDE_TAU);
            if (_magazineSlide < 0.001f) _magazineSlide = 0f;

            //And a re-coloured ball dissolves out of its old colour. Linear, so it genuinely finishes rather
            //than leaving a slot for ever a few pixels short of its new colour.
            for (int i = 0; i < MAGAZINE_SIZE; i++)
                if (_magazineTransmute[i] > 0f)
                    _magazineTransmute[i] = MathF.Max(0f, _magazineTransmute[i] - elapsed / TRANSMUTE_SECONDS);

            //The barrel slides home. Linear in the stroke, so it genuinely ends rather than approaching zero
            //forever and leaving the gun permanently a hair out of place.
            if (_cannonRecoil > 0f) _cannonRecoil = MathF.Max(0f, _cannonRecoil - CANNON_RECOIL_DECAY * elapsed);

            //Slide the ceiling before the step, so the solver works against the moved body this frame and the
            //contact between a descending cluster and anything below it resolves rather than interpenetrates.
            UpdateCeilingDescent(elapsed);

            StepPhysics(elapsed);

            //After the step: poses have advanced, so a ball dragged down by the descent is at its new Y now, and a
            //shot that spent the budget has had its landing. The two losses are checked here rather than only on a
            //landing, because a descent can push a ball across the death line between landings, and a spent budget
            //loses only once nothing remains in flight.
            CheckLevelLost();

            UpdateTrails(elapsed);

            UpdateCamera(elapsed);

            //Last, because FinishLevel may tear the whole session down and build the next one in its place —
            //everything above it this frame would then be running against a simulation that no longer exists.
            //
            //A loss needs no entry here: LoseLevel shows the result screen straight away (the same one a clear
            //lands on), which sets _state = Paused, so the Playing branch — and this tail with it — does not run
            //again until the player picks Retry or leaves.
            if (_clearedCountdown > 0f)
            {
                _clearedCountdown -= elapsed;
                if (_clearedCountdown <= 0f) FinishLevel();
            }

            base.Update(gameTime);
        }

        /// <summary>
        /// The menu's and the pause screen's own frame. Myra consumes the mouse and the keyboard itself (in
        /// <c>Desktop.Render</c>, at the end of <see cref="Draw"/>), so all this owes it is the keys the menu
        /// does not carry as buttons — Escape, and the two display toggles that are useful from anywhere.
        /// <para>
        /// The keyboard snapshot is stored into the very same <see cref="_previousKeyboard"/> the play loop
        /// uses, which is what makes the two states hand over cleanly: the Escape that paused the game is
        /// still down on the first menu frame and is correctly not seen as a second press, and the Escape that
        /// resumed it is likewise not seen again by the play loop.
        /// </para>
        /// </summary>
        private void UpdateMenu(GameTime gameTime, float elapsed, bool edgeInputAllowed)
        {
            //The cursor is the pointer here, not the aim; nothing recentres it while the menu is up
            IsMouseVisible = true;

            if (IsActive)
            {
                KeyboardState keyboard = Keyboard.GetState();

                if (edgeInputAllowed)
                {
                    //Escape backs out one level: out of a sub-screen to the screen that opened it, out of the
                    //pause menu into the game. The main menu has no back — quitting is a menu item, and a front
                    //end that closes when a key is tapped is one that closes by accident.
                    //
                    //The result screen has no back either, and for the same reason: the level has already ended,
                    //so there is nothing to resume into, and "back one level" has no meaning. Retry, Next Level
                    //and Main Menu are the only ways off it — Escape does nothing, exactly as it does at the
                    //front end.
                    if (IsKeyEdge(keyboard, Keys.Escape)
                        && _menuScreen != MenuScreen.MainMenu
                        && _menuScreen != MenuScreen.Result)
                    {
                        if (_menuScreen != MenuScreen.Pause)
                            ShowMenuScreen(_state == GameState.Paused ? MenuScreen.Pause : MenuScreen.MainMenu);
                        else if (_state == GameState.Paused) ResumeGame();
                    }

                    //The same two display keys the game has, because a player who wants windowed mode or no
                    //FPS line wants them before pressing Play as much as after
                    if (IsKeyEdge(keyboard, Keys.F11)) ToggleFullscreen();
                    if (IsKeyEdge(keyboard, Keys.F12)) ToggleFpsOverlay();
                }

                _previousKeyboard = keyboard;
            }

            _wasActive = IsActive;

            //Only the front end's camera moves. A pause holds the exact frame the player left, camera shake
            //and all — the game is stopped, not merely un-simulated.
            if (_state == GameState.Menu)
            {
                UpdateMenuCamera(elapsed);
                TuneQualityToFrameRate(elapsed);
            }

            base.Update(gameTime);
        }

        /// <summary>
        /// The front end's slow orbit around the scene's origin. The angle is advanced by elapsed seconds, so
        /// the turn takes the same ninety seconds on any machine, and the camera is the game's own
        /// <see cref="RecoilCamera"/> with its shake at rest — nothing kicks it here.
        /// </summary>
        private void UpdateMenuCamera(float elapsed)
        {
            _menuAngle += MENU_ROTATION_SPEED * elapsed;
            if (_menuAngle >= MathHelper.TwoPi) _menuAngle -= MathHelper.TwoPi;

            _camera.BasePosition = new Vector3(
                MathF.Cos(_menuAngle) * MENU_CAM_RADIUS, MENU_CAM_HEIGHT, MathF.Sin(_menuAngle) * MENU_CAM_RADIUS);
            _camera.BaseTarget = new Vector3(0f, MENU_TARGET_Y, 0f);
            _camera.FieldOfView = MENU_FOV;

            _camera.Update(elapsed);
        }

        /// <summary>
        /// Lowers supersampling on a machine that visibly cannot afford it, measured rather than guessed.
        /// <para>
        /// The default of 2× costs four times the shaded pixels, and on a weak GPU that is the single biggest
        /// thing in the frame — measured on an integrated Vega 10, the <b>main menu</b> ran at 13 FPS at 2×
        /// and the one setting that would fix it was behind a menu the player had to sit through at 13 FPS to
        /// reach. Something has to notice for them.
        /// </para>
        /// <para>
        /// It notices by <b>timing the frames it is already drawing</b>, not by recognising the adapter. A
        /// name or vendor list is wrong on the first machine nobody tested — plenty of AMD parts are discrete,
        /// plenty of Intel ones now are too — and it cannot see the other reasons a frame is slow: a 4K
        /// display, a laptop on battery, something else eating the GPU. The front end is a fair probe on its
        /// own: it draws the same city, clouds, glare and tonemap the game does, at the same factor, and it is
        /// the fixed scene cost rather than the ball count that dominates on this class of hardware (#64).
        /// </para>
        /// <para>
        /// One step per verdict and never upwards. Raising it again on a machine that recovered would put the
        /// player back where they started, and a quality dial that oscillates is worse than one set too low.
        /// </para>
        /// </summary>
        private void TuneQualityToFrameRate(float elapsed)
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

            int lowered = _supersampleFactor switch { 4 => 2, 2 => 1, _ => 1 };

            Console.WriteLine($"[quality] {fps:F0} FPS in the menu at {_supersampleFactor}x supersampling"
                + $" — lowering to {lowered}x");

            SetSupersampleFactor(lowered);
            ShowQualityNotice(lowered);

            //Nothing left to give: 1x already falls back to MSAA, and there is no lower tier to step to.
            if (_supersampleFactor <= 1) _qualitySettled = true;
        }

        /// <summary>
        /// Tells the player what was changed and where to change it back. Once per run, on the main menu —
        /// which is where they are: the verdict lands about three seconds in.
        /// </summary>
        private void ShowQualityNotice(int factor)
        {
            if (_qualityNotice == null) return;

            _qualityNotice.Text = $"Antialiasing lowered to {factor}× for a smoother frame rate — change it in Settings.";
            _qualityNotice.Visible = true;
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

            RefreshSettingsLabels();
        }

        /// <summary>A key pressed this frame that was not pressed last frame.</summary>
        private bool IsKeyEdge(KeyboardState keyboard, Keys key) =>
            keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

        private void ToggleFullscreen()
        {
            _fullscreen = !_fullscreen;
            SetGraphics();
            RefreshSettingsLabels();
        }

        private void ToggleFpsOverlay()
        {
            _info.Visible = !_info.Visible;
            RefreshSettingsLabels();
        }

        private void UpdateInput(GameTime gameTime, bool edgeInputAllowed, GamePadState pad)
        {
            KeyboardState keyboard = Keyboard.GetState();

            if (edgeInputAllowed)
            {
                //Escape (or the gamepad's Back button) pauses rather than quitting outright: quitting is a
                //menu item now, and a game that closes the instant Escape is tapped is one that loses a game.
                if (IsKeyEdge(keyboard, Keys.Escape) || (pad.IsButtonDown(Buttons.Back) && !_previousPad.IsButtonDown(Buttons.Back)))
                {
                    _previousKeyboard = keyboard;
                    _previousPad = pad;
                    PauseGame();

                    //Nothing else this frame: the game is paused as of now, and firing or traversing on the
                    //way out would be an action the player asked of a game that has stopped
                    return;
                }

                if (IsKeyEdge(keyboard, Keys.F11)) ToggleFullscreen();

                //F12 hides the FPS overlay, the same key that hides the Testbed's text
                if (IsKeyEdge(keyboard, Keys.F12)) ToggleFpsOverlay();

                //Space fires; the gamepad fires off its right trigger, read with the aim (below)
                if (IsKeyEdge(keyboard, Keys.Space)) Shoot();
            }

            //The carriage traverses on A/D — it turns where it stands, it does not walk
            if (keyboard.IsKeyDown(Keys.A)) _cannon.Orbit(CANNON_ORBIT_RATE);
            else if (keyboard.IsKeyDown(Keys.D)) _cannon.Orbit(-CANNON_ORBIT_RATE);

            _previousKeyboard = keyboard;
        }

        /// <summary>
        /// Aiming is the mouse, all the time: the cursor is hidden and recentred every frame off the live
        /// viewport, and the pixel delta is divided by the frame time, which cancels exactly against the
        /// frame time <see cref="Cannon.Aim"/> multiplies back in — so the aim moves a fixed amount per
        /// pixel at any frame rate. Firing is read from the same state, so the click and the aim cannot
        /// disagree about the frame they happened in.
        /// </summary>
        private void UpdateAim(GameTime gameTime, bool edgeInputAllowed, GamePadState pad)
        {
            int centreX = GraphicsDevice.Viewport.Width / 2;
            int centreY = GraphicsDevice.Viewport.Height / 2;

            MouseState mouse = Mouse.GetState();

            if (_mouseAimInitialized)
            {
                float dtMillis = (float)gameTime.ElapsedGameTime.TotalMilliseconds;
                if (dtMillis > 0f)
                {
                    float invDt = 1f / dtMillis;
                    float pitch = -(mouse.Y - centreY) * MOUSE_AIM_SENSITIVITY * invDt; //mouse up -> aim up
                    float yaw = -(mouse.X - centreX) * MOUSE_AIM_SENSITIVITY * invDt;   //mouse left -> yaw left

                    if (pitch != 0f || yaw != 0f) _cannon.Aim(new Vector2(pitch, yaw), gameTime);
                }

                if (edgeInputAllowed && mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released)
                    Shoot();
            }

            //Precise aim is a hold, not an edge, so it is read straight off this frame's state — no
            //edge-input gate: leaning the camera in is not an action that can go off by accident.
            _adsHeld = mouse.RightButton == ButtonState.Pressed || pad.Triggers.Left > ADS_TRIGGER_THRESHOLD;

            Mouse.SetPosition(centreX, centreY);
            _mouseAimInitialized = true;

            //Only its LeftButton is ever read (the shot's edge test above); the aim delta is measured against
            //the viewport centre, never against this, so the state captured at the top of the method serves
            _previousMouse = mouse;

            if (pad.IsConnected)
            {
                if (pad.ThumbSticks.Right.LengthSquared() > 0f)
                    _cannon.Aim(new Vector2(pad.ThumbSticks.Right.Y, -pad.ThumbSticks.Right.X) * PAD_AIM_RATE, gameTime);

                //Gated like the keyboard and the mouse: XInput reports a held trigger whether the window has
                //focus or not, so without this the click that refocuses the game would arrive alongside a
                //trigger that was never released and fire a shot the player did not ask for
                if (edgeInputAllowed && pad.Triggers.Right > 0.5f && _padTriggerReleased) { Shoot(); _padTriggerReleased = false; }
                else if (pad.Triggers.Right <= 0.5f) _padTriggerReleased = true;
            }

            //Stored here rather than in UpdateInput, which runs first: the Back button's press edge is
            //measured against the previous *frame*, so the snapshot has to be the last thing the frame does
            //with the pad. Both methods are handed the one poll taken at the top of Update.
            _previousPad = pad;
        }

        private bool _padTriggerReleased = true;

        /// <summary>
        /// Fires the ball the player can see sitting at the muzzle: a real body thrown down the bore, a launch
        /// smear along the shot, the queue shifted forward — and the camera and the barrel both kicked, which is
        /// the whole feel of the thing. Where it goes from there is the simulation's business: it may hit the
        /// cluster and attach (see <see cref="BallContactEventHandler"/>), bounce off the glass, run down the
        /// drain, or fly off the island and be culled.
        /// </summary>
        private void Shoot()
        {
            //The budget is spent, so no more shots leave the barrel — but the level is not lost here. The last
            //ball fired is still in flight and may be the one that clears the field, and a loss called now would
            //steal that win. Whether the spent budget actually loses is decided once every shot has resolved,
            //in CheckLevelLost, against the state of the field then.
            if (_score.OutOfShots) return;

            Vector3 direction = CannonAimDirection();
            Vector3 muzzle = CannonMuzzlePosition();
            BallType type = _magazine[0];

            _shotBall.Pose.Position = new System.Numerics.Vector3(muzzle.X, muzzle.Y, muzzle.Z);
            _shotBall.Velocity.Linear = new System.Numerics.Vector3(direction.X, direction.Y, direction.Z) * SHOOT_SPEED;

            BodyHandle handle = _simulation.Bodies.Add(_shotBall);

            PhysicsBall ball = new()
            {
                BallReference = new BodyReference(handle, _simulation.Bodies),
                Type = type //the colour the player saw loaded at the muzzle, so aiming for it means something
            };

            _shotBalls.Add(ball);

            //The ball is spent the instant it leaves the barrel. What it *did* takes a physics step or more to
            //resolve, so the budget and the score are driven by different events on purpose — see ScoreKeeper.
            _score.Shot();

            //The same shot drives the ceiling's descent: every ceilingStep-th shot steps the glass down. Checked
            //after Shot() so ShotsFired includes the one just fired, and the scorer owns the cadence exactly as it
            //owns the budget — the two pressures are coupled by design, so they are read in one place.
            if (_score.StepCeilingThisShot()) StartCeilingDescent();

            //Registered after the body exists, since a listener is keyed on its collidable reference
            _events.Register(_simulation.Bodies[handle].CollidableReference, _eventHandler);

            _trails.Add(new ShotTrail { Origin = muzzle, Direction = direction, Color = TrailColorFor(type), Age = 0f });

            AdvanceMagazine();

            //Set, not accumulated: a barrel's recoil stroke restarts from the top with every round, it does
            //not stack up over a burst the way the camera's trauma does.
            _cannonRecoil = 1f;

            //Fired, therefore felt. Nothing else in the frame moves the camera, so every wobble the player
            //sees is unambiguously their own shot.
            _camera.Shake.Kick(RECOIL_KICK);
        }

        private void AdvanceMagazine()
        {
            //A ball's half-finished transmute rides forward with it: the queue is drawn from these three
            //arrays in step, so shifting only the colours would leave a slot dissolving out of a colour that
            //belongs to the ball behind it.
            for (int i = 0; i < MAGAZINE_SIZE - 1; i++)
            {
                _magazine[i] = _magazine[i + 1];
                _magazineFrom[i] = _magazineFrom[i + 1];
                _magazineTransmute[i] = _magazineTransmute[i + 1];
            }

            _magazine[MAGAZINE_SIZE - 1] = RandomBallType();
            _magazineTransmute[MAGAZINE_SIZE - 1] = 0f; //freshly drawn from what is alive; nothing to fade

            //Armed at one slot back, so the queue eases forward into the muzzle slot the shot just vacated
            _magazineSlide = 1f;
        }

        /// <summary>
        /// Advances the simulation, spending whatever the frame took out of an accumulator in whole steps of
        /// <see cref="PHYSICS_TIMESTEP"/>. Each step's contacts are flushed and handled before the next one is
        /// taken: a contact queued during a step describes a world the following step has already moved on from.
        /// </summary>
        private void StepPhysics(float elapsed)
        {
            _physicsAccumulator += elapsed;

            int steps = 0;

            while (_physicsAccumulator >= PHYSICS_TIMESTEP && steps < PHYSICS_MAX_STEPS_PER_FRAME)
            {
                _simulation.Timestep(PHYSICS_TIMESTEP, _threadDispatcher);

                //Flush first, then handle. Unregistering a listener is only safe once the per-worker adds
                //collected during the timestep have been applied, and the handler unregisters as it attaches.
                _events.Flush();
                _eventHandler.ProcessQueuedContacts();

                _physicsAccumulator -= PHYSICS_TIMESTEP;
                steps++;
            }

            //A frame that hitched must not try to catch the whole gap up — it would take even longer doing so
            //and fall further behind on the next frame, and the one after that. Drop what is left and let the
            //world run slow for that single frame instead.
            if (steps == PHYSICS_MAX_STEPS_PER_FRAME) _physicsAccumulator = 0f;

            RemoveFallenBalls(_shotBalls, unregisterListeners: true);
            RemoveFallenBalls(_fallingBalls, unregisterListeners: false);
        }

        /// <summary>
        /// Drops the balls that have left the game: those below <see cref="KILL_PLANE_Y"/>, having gone down
        /// the drain or over the island's edge.
        /// <para>
        /// <b>Falling below the map is the only thing that removes a ball</b> — deliberately, and this is where
        /// the game parts company with the Testbed. The Testbed also culls any ball whose body has gone to
        /// sleep, i.e. come to rest anywhere, which is cheap and keeps the scene tidy but means a ball that
        /// settles on the island's stone winks out in front of the player. A ball vanishing while it is plainly
        /// in shot reads as a bug, whatever it saves. So a ball that comes to rest on the stone ring stays
        /// there and stays visible; the cost is that such balls accumulate over a session. It is a small cost:
        /// they fall asleep, and a sleeping body leaves Bepu's active set, so it is drawn but barely simulated —
        /// and most of them never rest at all, since anything inside the funnel's rim runs down its ~55° wall
        /// and out through the hole.
        /// </para>
        /// <para>
        /// Only ever handed <see cref="_shotBalls"/> and <see cref="_fallingBalls"/>. Handing it the structure
        /// array would delete the cluster.
        /// </para>
        /// </summary>
        /// <param name="unregisterListeners">True for shot balls, which may still be listening for contacts.</param>
        private void RemoveFallenBalls(List<PhysicsBall> balls, bool unregisterListeners)
        {
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                BodyReference body = balls[i].BallReference;
                if (body.Pose.Position.Y >= KILL_PLANE_Y) continue;

                if (unregisterListeners && _events.IsListener(body.CollidableReference))
                {
                    //Still listening means the shot never resolved: it missed the island as well as the
                    //cluster and fell straight past everything into the city. The far rarer of the two misses
                    //— a shot that strikes the stone is spent the moment it does (see the handler) — but it is
                    //what makes "every shot resolves exactly once" true rather than nearly true.
                    _score.Missed();

                    _events.Unregister(body.CollidableReference);
                }

                _simulation.Bodies.Remove(body.Handle);
                balls.RemoveAt(i);
            }
        }

        private void UpdateTrails(float elapsed)
        {
            for (int i = _trails.Count - 1; i >= 0; i--)
            {
                ShotTrail trail = _trails[i];
                trail.Age += elapsed;

                if (trail.Age >= TRAIL_LIFETIME) _trails.RemoveAt(i);
                else _trails[i] = trail;
            }
        }

        /// <summary>
        /// The camera's base pose, rebuilt each frame from where the gun stands: back from the field centre
        /// along the gun's own bearing and below its trunnions, looking at the cluster. The bearing is
        /// flattened to the horizontal — taken straight from the gun's offset it tilts down by however far
        /// the gun stands below the cluster, which would eat the camera's height and put the lens on the
        /// barrel's own axis. The shake is added on top of this pose, never into it.
        /// <para>
        /// That overview is one end of a Lerp; the other is the precise-aim lean over the barrel, and
        /// <c>_adsBlend</c> is where between them the frame sits. Only the <b>base</b> pose is interpolated,
        /// so the two never fight: the kick is applied to whatever came out, by the camera itself.
        /// </para>
        /// </summary>
        private void UpdateCamera(float elapsed)
        {
            //The lean into precise aim, eased both ways off one reversible scalar. At 0 the Lerps below
            //return the overview pose bit for bit, so letting go re-asserts today's framing exactly; the ease
            //is continuous through an interrupted hold, so there is no state machine and nothing to snap.
            float adsTarget = _adsHeld ? 1f : 0f;
            _adsBlend = adsTarget + (_adsBlend - adsTarget) * MathF.Exp(-elapsed / ADS_BLEND_TAU);
            if (adsTarget == 0f && _adsBlend < 0.002f) _adsBlend = 0f;
            if (adsTarget == 1f && _adsBlend > 0.998f) _adsBlend = 1f;

            Vector3 overviewPosition = GameCameraPositionAt(_gameCameraDistance);
            Vector3 overviewTarget = new(_cannon.OrbitCenter.X, _gameCameraTargetY, _cannon.OrbitCenter.Z);

            _camera.BasePosition = Vector3.Lerp(overviewPosition, AdsCameraPosition(), _adsBlend);
            _camera.BaseTarget = Vector3.Lerp(overviewTarget, AdsCameraTarget(), _adsBlend);
            _camera.FieldOfView = MathHelper.Lerp(GAME_FOV, ADS_FOV, _adsBlend);

            _camera.Update(elapsed);
        }

        #region Fitting the camera and the gun to the level

        /// <summary>
        /// The horizontal direction from the field out towards the gun — the way the camera stands back.
        /// Deliberately <b>flattened to the horizontal</b>: taken straight from <c>Position - OrbitCenter</c>
        /// it tilts down by however far the gun stands below the cluster, which eats the camera's height and
        /// leaves the lens sitting on the barrel's own axis, seeing the gun end-on.
        /// </summary>
        private Vector3 GameCameraBearing()
        {
            Vector3 back = _cannon.Position - _cannon.OrbitCenter;
            back.Y = 0f;

            return back == Vector3.Zero ? Vector3.Backward : Vector3.Normalize(back);
        }

        /// <summary>The field's centre at ground level: what the camera stands off from and turns about.</summary>
        private Vector3 FieldCentreGround() => new(_cannon.OrbitCenter.X, 0f, _cannon.OrbitCenter.Z);

        /// <summary>Where the lens sits for a given stand-off — the pose the fit below searches over.</summary>
        private Vector3 GameCameraPositionAt(float distance) =>
            FieldCentreGround() + GameCameraBearing() * distance + Vector3.Up * (_cannon.Position.Y + CAMERA_HEIGHT);

        /// <summary>
        /// Solves the gun's orbit radius and the camera's stand-off together, since each depends on the other
        /// — the camera is placed to frame the field <i>and the gun</i>, and the gun is placed a fixed
        /// distance in front of the camera. Alternating converges at once in practice: at a fixed distance
        /// from the lens the gun's angular footprint is the same whatever the radius, so the camera's solve
        /// barely moves after the first round. Run on every level load and every resize.
        /// </summary>
        private void FitCannonAndGameCameraToLevel()
        {
            if (_map == null || _camera == null || _cannon == null) return;

            for (int round = 0; round < 3; round++)
            {
                FitCannonOrbitToLevel();
                FitGameCameraToLevel();
            }

            Console.WriteLine($"[camera] Field {_map.StageSizeX}x{_map.StageSizeZ}x{_map.Levels}, aspect {_camera.AspectRatio:F2}: "
                + $"camera {_gameCameraDistance:F1} out, aim Y {_gameCameraTargetY:F1}, "
                + $"gun orbit {_cannon.OrbitRadius:F1} ({_gameCameraDistance - _cannon.OrbitRadius:F1} in front of the lens)");
        }

        /// <summary>
        /// Puts the gun <see cref="CANNON_CAMERA_STANDOFF"/> in front of the camera, held off by the two lower
        /// bounds documented with that constant.
        /// </summary>
        private void FitCannonOrbitToLevel()
        {
            float halfX = (_map.StageSizeX + 1f) * Constants.HALF;
            float halfZ = (_map.StageSizeZ + 1f) * Constants.HALF;

            float clearFootprint = MathF.Sqrt(halfX * halfX + halfZ * halfZ) + CANNON_FIELD_CLEARANCE;
            float clearElevation = (_cannon.OrbitCenter.Y - _cannon.Position.Y) / MathF.Tan(CANNON_MAX_REST_ELEVATION);

            _cannon.OrbitRadius = MathF.Max(_gameCameraDistance - CANNON_CAMERA_STANDOFF,
                MathF.Max(clearFootprint, clearElevation));
        }

        /// <summary>
        /// Places the camera so the whole play field, the glass over it and the gun fit inside the frustum,
        /// and aims it so they sit centred in it.
        /// <para>
        /// This has to be <b>solved</b> rather than tuned, because both of its inputs move. The field is
        /// sized per level, so a stand-off that frames one crops another off the top of the screen — which is
        /// exactly what the fixed number it replaces did. And the frustum is sized per display:
        /// <c>CreatePerspectiveFieldOfView</c> takes the <b>vertical</b> FOV, so a wider screen only adds
        /// width. That is the behaviour wanted — the field keeps its size on an ultrawide and the extra width
        /// goes to scenery — but it also means the horizontal fit is generous at 21:9 and tightest on the
        /// narrowest display, so both axes are checked.
        /// </para>
        /// </summary>
        private void FitGameCameraToLevel()
        {
            float halfX = (_map.StageSizeX + 1f) * Constants.HALF;
            float halfZ = (_map.StageSizeZ + 1f) * Constants.HALF;

            //The field in WORLD Y, which is the one place this differs from the Testbed's own solver: there
            //the lattice frame IS the world frame, while here level 0 sits at the cluster offset rather than
            //at zero. A deep level's empty growth levels are inside this on purpose — the cluster grows down
            //into them, so they have to be in frame before the first ball ever lands there.
            float bottomY = _clusterWorldOffset.Y;
            float topY = _ceilingY + Constants.HALF;   //upper face of the ceiling slab

            float verticalHalf = GAME_FOV * Constants.HALF * GAME_CAMERA_FIT_MARGIN;
            float horizontalHalf = MathF.Atan(MathF.Tan(GAME_FOV * Constants.HALF) * _camera.AspectRatio) * GAME_CAMERA_FIT_MARGIN;

            //Everything fits from far enough away and nothing does from close in, so the smallest distance
            //that fits can be bisected for. The near bound is the lens right behind the gun.
            float near = CannonOrbitRadius() + 2f;
            float far = 400f;

            for (int i = 0; i < 32; i++)
            {
                float middle = (near + far) * Constants.HALF;
                if (GameCameraFitsAt(middle, halfX, halfZ, bottomY, topY, verticalHalf, horizontalHalf, out _)) far = middle;
                else near = middle;
            }

            GameCameraFitsAt(far, halfX, halfZ, bottomY, topY, verticalHalf, horizontalHalf, out float axisElevation);

            _gameCameraDistance = far;
            _gameCameraTargetY = _cannon.Position.Y + CAMERA_HEIGHT + far * MathF.Tan(axisElevation);
        }

        /// <summary>
        /// Whether the field, its ceiling and the gun all land inside the frustum with the lens that far out,
        /// and at what elevation the view axis has to sit for it. Elevations are measured off the horizontal
        /// and bisected, which is what centres the subject between the top and bottom edges.
        /// </summary>
        private bool GameCameraFitsAt(float distance, float halfX, float halfZ, float bottomY, float topY,
            float verticalHalf, float horizontalHalf, out float axisElevation)
        {
            Vector3 back = GameCameraBearing();
            Vector3 camera = GameCameraPositionAt(distance);
            Vector3 forward = -back;
            Vector3 right = Vector3.Cross(Vector3.Up, forward);

            float minElevation = float.MaxValue;
            float maxElevation = float.MinValue;
            float maxSide = 0f;
            bool ahead = true;

            void Consider(Vector3 point)
            {
                Vector3 offset = point - camera;
                float depth = Vector3.Dot(offset, forward);

                //On or behind the lens: there is no angle to measure, and the pose is rejected outright
                if (depth <= Constants.ONE) { ahead = false; return; }

                float elevation = MathF.Atan2(offset.Y, depth);
                minElevation = MathF.Min(minElevation, elevation);
                maxElevation = MathF.Max(maxElevation, elevation);
                maxSide = MathF.Max(maxSide, MathF.Atan2(MathF.Abs(Vector3.Dot(offset, right)), depth));
            }

            //The field's eight corners, from the floor of the play space to the top of the glass
            for (int cornerX = -1; cornerX <= 1; cornerX += 2)
                for (int cornerZ = -1; cornerZ <= 1; cornerZ += 2)
                {
                    Consider(new Vector3(cornerX * halfX, bottomY, cornerZ * halfZ));
                    Consider(new Vector3(cornerX * halfX, topY, cornerZ * halfZ));
                }

            //The gun, as a box around its trunnions large enough to hold the barrel at any aim, so the fit
            //does not change as the player elevates or traverses
            float reach = CANNON_PIVOT_TO_FRONT_BALL + Constants.HALF;
            Consider(_cannon.Position + Vector3.Up * reach);
            Consider(_cannon.Position - Vector3.Up * reach);
            Consider(_cannon.Position + back * reach);
            Consider(_cannon.Position - back * reach);
            Consider(_cannon.Position + right * reach);
            Consider(_cannon.Position - right * reach);

            axisElevation = (minElevation + maxElevation) * Constants.HALF;

            return ahead
                && (maxElevation - minElevation) * Constants.HALF <= verticalHalf
                && maxSide <= horizontalHalf;
        }

        /// <summary>The gun's horizontal distance from the centre it orbits — its orbit radius.</summary>
        private float CannonOrbitRadius()
        {
            Vector3 offset = _cannon.Position - _cannon.OrbitCenter;
            offset.Y = 0f;

            return offset.Length();
        }

        #endregion

        protected override void Draw(GameTime gameTime)
        {
            //What belongs to a session rather than to the setting: the gun, the cluster, the shots and the
            //glass the cluster hangs from. The main menu shows the scene on its own, with the camera orbiting
            //it; a pause shows the game exactly as the player left it. And before the first "Play" there is
            //no cluster at all, which is what the built flag guards against.
            bool drawGameplay = _gameBuilt && _state != GameState.Menu;

            //The occlusion ease and the attach glide are advanced on the draw clock, since both are purely
            //about what is on screen
            if (drawGameplay)
                CollectBallInstances((float)gameTime.ElapsedGameTime.TotalSeconds);

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

            if (drawGameplay)
            {
                _cannonRenderer.Draw(_camera, CannonWorld(), _sceneEffectParams);

                DrawBallsInstanced();

                //Over the opaque scene (which the depth buffer now holds, so the cluster and the gun occlude
                //them) and additive, so they glow through the glare
                DrawShotTrails();
            }

            //The drain's gold beads are opaque, so they belong to the opaque scene and go down before the
            //glass the funnel composites over them. A closed convex tube, so CullNone is safe (the nearest
            //face wins on depth and the winding is moot) and is one less thing to get wrong unseen.
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            _funnelRimsRenderer.Draw(_camera, _funnelWorld, _funnelRimEffectParams);

            //The glass funnel itself: one open, single-sided cone, so culling stays off to show both the
            //inside looking down into it and the outside looking up through the hole.
            _funnelRenderer.Draw(_camera, _funnelWorld, _sceneEffectParams);
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            //The glass the cluster hangs from, last of the session's objects: it is translucent, so everything
            //it should be seen through has to be in the depth buffer and the frame already.
            if (drawGameplay)
                _ceilingRenderer.Draw(_camera, _ceiling.World, _sceneEffectParams);

            //The scene's foreground weather — the mountain's snow, the sea's spray, the savanna's flame —
            //settles over everything, so it goes down in front of what it should hide. A no-op in the two
            //cities and the desert, which carry none.
            _sceneRenderer.DrawOverlays(_scene, sceneFrame);

            ResolveSceneTarget();

            //Display space from here down: the resolve is the frame's one and only exit from linear light,
            //and the crosshair, the FPS overlay (a component, so base.Draw puts it out) and the menu are sRGB.
            if (drawGameplay)
                DrawCrosshair();

            base.Draw(gameTime);

            //The Myra GUI renders last, on top of everything, straight to the back buffer (base.Draw and
            //ResolveSceneTarget leave it bound). Render also processes Myra's own mouse and keyboard input,
            //which is why it runs only where the game's own input has stood down — in Menu and Paused.
            //
            //While the window is not focused it is laid out and drawn but NOT fed input: Mouse.GetState
            //reports the button and a window-relative position whether the window has focus or not, so a
            //click meant for another application that happens to land over where a menu entry is would
            //otherwise press it — and "Quit" would close a game nobody was looking at.
            if (_state != GameState.Playing && _desktop != null)
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
        }

        /// <summary>
        /// The crosshair, shown only while precise aim is leaning in — that is the only pose whose lens looks
        /// along the shot, so it is the only one where a screen-centre mark means anything. Four bars around
        /// a clear centre, struck from a single white texel; multiplying the colour by the blend scales its
        /// alpha too, so it fades up with the lean instead of snapping on.
        /// </summary>
        private void DrawCrosshair()
        {
            if (_adsBlend <= 0.01f) return;

            Viewport viewport = GraphicsDevice.Viewport;

            float scale = viewport.Height / CROSSHAIR_SCALE_DIVISOR;
            float arm = CROSSHAIR_ARM * scale;
            float gap = CROSSHAIR_GAP * scale;

            //A bar authored five units thick is under a pixel on a small window, where rounding down would
            //leave nothing to draw at all
            int thickness = Math.Max(1, (int)(CROSSHAIR_THICKNESS * scale));
            int length = Math.Max(1, (int)arm);

            int centreX = viewport.Width / 2;
            int centreY = viewport.Height / 2;
            int inner = (int)gap;
            int half = thickness / 2;

            Color color = CROSSHAIR_COLOR * _adsBlend;

            _spriteBatch.Begin();
            _spriteBatch.Draw(_pixel, new Rectangle(centreX - inner - length, centreY - half, length, thickness), color);
            _spriteBatch.Draw(_pixel, new Rectangle(centreX + inner, centreY - half, length, thickness), color);
            _spriteBatch.Draw(_pixel, new Rectangle(centreX - half, centreY - inner - length, thickness, length), color);
            _spriteBatch.Draw(_pixel, new Rectangle(centreX - half, centreY + inner, thickness, length), color);
            _spriteBatch.End();
        }

        /// <summary>
        /// Gathers every ball in the frame — the structure, the shots in flight, the ones falling and the queue
        /// in the barrel — into one bucket per type and LOD, each of which becomes a single instanced draw call.
        /// The first three come straight off their bodies' poses, so what is drawn <i>is</i> the simulation.
        /// <para>
        /// Neighbour-based ambient occlusion is derived here too: a ball buried in the mass is darker than one
        /// on the outside, which is what makes the cluster read as one body rather than a heap of spheres. It is
        /// re-derived for every ball every frame rather than for a new arrival alone, because a ball that
        /// attaches also boxes in each neighbour it arrived next to. Each ball must be visited <b>exactly
        /// once</b> per frame — the ease and the glide below advance state on the ball itself.
        /// </para>
        /// </summary>
        private void CollectBallInstances(float elapsed)
        {
            for (int i = 0; i < _ballInstanceCounts.Length; i++) _ballInstanceCounts[i] = 0;

            //How far towards its target each ball's occlusion moves this frame, and how much of the attach
            //glide is left after it
            float ease = elapsed <= 0f ? 1f : MathF.Min(1f, elapsed / BALL_OCCLUSION_EASE_SECONDS);
            float glide = elapsed <= 0f ? 0f : MathF.Exp(-elapsed / BALL_ATTACH_GLIDE_SECONDS);

            //Hoisted: the array's dimensions do not change, and this is the innermost loop in the frame
            XZLevel size = XZLevel.FromArray(_physicsBalls);

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        PhysicsBall ball = _physicsBalls[x, z, level];
                        if (ball == null) continue;

                        int occluders = BallsConstraintsBuilder.CountOccupiedNeighbors(
                            _physicsBalls, new XZLevel(x, z, level), size, out System.Numerics.Vector3 direction);

                        //The direction is a sum of unit vectors, one per occupied neighbour, so it has to be
                        //divided by the most there can be before the shader reads it as a direction-and-weight.
                        //Handed over raw it is up to twelve times too long, the shader's dot against it
                        //saturates over most of the ball, and every surface ball wears a hard black crescent
                        //instead of the soft inward shading that makes the cluster read as one body.
                        System.Numerics.Vector4 target = new(
                            direction / MAX_BALL_OCCLUDERS,
                            1f - BALL_OCCLUSION_STRENGTH * Math.Min(occluders, MAX_BALL_OCCLUDERS) / MAX_BALL_OCCLUDERS);

                        CollectBallInstance(ball, EaseOcclusion(ball, target, ease), glide);
                    }

            //Indexed rather than foreach: these are List<T> on a per-frame path
            for (int i = 0; i < _shotBalls.Count; i++)
                CollectBallInstance(_shotBalls[i], EaseOcclusion(_shotBalls[i], PhysicsBall.UNOCCLUDED, ease), glide);

            for (int i = 0; i < _fallingBalls.Count; i++)
                CollectBallInstance(_fallingBalls[i], EaseOcclusion(_fallingBalls[i], PhysicsBall.UNOCCLUDED, ease), glide);

            CollectMagazineBalls();
        }

        /// <summary>
        /// Moves a ball's drawn occlusion towards what its surroundings now call for. A ball joins or leaves the
        /// lattice in a single step, while it has not moved at all, so taking the new value straight would pop
        /// its shading — most visibly when a matched group lets go and every ball around the hole brightens at
        /// once. The <i>first</i> frame a ball is drawn does take it straight, or a freshly built cluster would
        /// fade into its own shading instead of starting out correct.
        /// </summary>
        private static System.Numerics.Vector4 EaseOcclusion(PhysicsBall ball, System.Numerics.Vector4 target, float ease)
        {
            if (!ball.OcclusionInitialized)
            {
                ball.Occlusion = target;
                ball.OcclusionInitialized = true;
            }
            else ball.Occlusion += (target - ball.Occlusion) * ease;

            return ball.Occlusion;
        }

        /// <summary>
        /// One ball, drawn from its body: where the pose puts it, turned the way the pose turns it, plus
        /// whatever is left of its attach glide.
        /// </summary>
        private void CollectBallInstance(PhysicsBall ball, System.Numerics.Vector4 occlusion, float glide)
        {
            RigidPose pose = ball.BallReference.Pose;

            //The glide is an offset from the body that decays to nothing, not a smoothed position: the ball
            //still follows every bit of the structure's swaying meanwhile, so nothing is left over to jump when
            //it ends. Skipped for exactly one frame after it is armed, because the constraints that drag the
            //body into its cell have not run yet and offsetting it now would move it the wrong way.
            if (ball.RenderOffsetArmed) ball.RenderOffsetArmed = false;
            else if (ball.RenderOffset.LengthSquared() > BALL_ATTACH_GLIDE_DONE_SQUARED) ball.RenderOffset *= glide;
            else ball.RenderOffset = default;

            System.Numerics.Vector3 drawn = pose.Position + ball.RenderOffset;
            Vector3 position = new(drawn.X, drawn.Y, drawn.Z);

            //The balls turn now, which is what makes the beach-ball pattern readable — so the world matrix has
            //to carry the orientation. Built from the quaternion with the translation written into the fourth
            //row rather than multiplied in by a second 4×4.
            Matrix world = Matrix.CreateFromQuaternion(
                new Quaternion(pose.Orientation.X, pose.Orientation.Y, pose.Orientation.Z, pose.Orientation.W));

            world.M41 = position.X;
            world.M42 = position.Y;
            world.M43 = position.Z;

            CollectBallInstance(position, world, ball.Type, new Vector4(occlusion.X, occlusion.Y, occlusion.Z, occlusion.W));
        }

        /// <summary>
        /// The loaded queue, drawn as real balls inside the bore so they show through the barrel's slot —
        /// the player reads the next colour off them. They take the barrel's own basis: drawn unrotated they
        /// would hold a fixed world orientation while the barrel tilts around them, which reads as each
        /// ball skewing in its slot.
        /// </summary>
        private void CollectMagazineBalls()
        {
            Vector3 direction = CannonAimDirection();

            //The queue rides the barrel, recoil included: it sits in the bore, so it goes back with it
            Vector3 front = CannonMuzzlePosition() + CannonRecoilOffset();

            //The barrel's basis with the translation written straight in: CannonOrientation() carries zero
            //translation (Matrix.CreateWorld with Vector3.Zero), so orientation × translation is exactly the
            //orientation with its fourth row set — no per-ball matrix multiply needed
            Matrix world = CannonOrientation();

            for (int i = 0; i < MAGAZINE_SIZE; i++)
            {
                Vector3 position = front - direction * ((i + _magazineSlide) * MAGAZINE_SPACING);
                world.M41 = position.X;
                world.M42 = position.Y;
                world.M43 = position.Z;

                //A ball whose colour was eliminated from the cluster is re-coloured where it sits, and the two
                //colours cross-fade by dithering against each other: the new one arrives (negative) while the
                //old one goes (positive), and the two cuts are exact complements, so every pixel of the sphere
                //is written by exactly one of the two draws. Both stay in the opaque path — no sorting, no
                //muddy overlap. A settled ball is a single draw at zero, which clips nothing.
                //
                //_magazineTransmute counts DOWN from 1 (just swapped) to 0 (settled), so the dissolve's own
                //progress is its complement. Feeding the countdown straight in runs the effect backwards: the
                //new colour arrives complete on the frame of the swap and the old one is never seen at all.
                float remaining = _magazineTransmute[i];

                if (remaining > 0f)
                {
                    float progress = 1f - remaining;

                    CollectBallInstance(position, world, _magazine[i], new Vector4(0f, 0f, 0f, 1f), -progress);
                    CollectBallInstance(position, world, _magazineFrom[i], new Vector4(0f, 0f, 0f, 1f), progress);
                }
                else CollectBallInstance(position, world, _magazine[i], new Vector4(0f, 0f, 0f, 1f));
            }
        }

        /// <param name="dissolve">
        /// Zero for every ball but one caught mid-transmute — see <see cref="ModelInstance.Dissolve"/>.
        /// </param>
        private void CollectBallInstance(Vector3 position, Matrix world, BallType type, Vector4 occlusion, float dissolve = 0f)
        {
            int typeIndex = (int)type - 1;
            if (typeIndex < 0 || typeIndex >= BALL_TYPE_COUNT) return;

            float distance = Vector3.Distance(position, _camera.Position);
            int lod = 0;
            while (lod < BALL_LOD_DISTANCES.Length && distance > BALL_LOD_DISTANCES[lod]) lod++;

            int bucketIndex = typeIndex * BALL_LOD_COUNT + lod;
            ModelInstance[] bucket = _ballInstances[bucketIndex];
            int count = _ballInstanceCounts[bucketIndex];

            if (bucket == null)
            {
                bucket = new ModelInstance[256];
                _ballInstances[bucketIndex] = bucket;
            }
            else if (count == bucket.Length)
            {
                Array.Resize(ref bucket, bucket.Length * 2);
                _ballInstances[bucketIndex] = bucket;
            }

            bucket[count] = new ModelInstance(world, occlusion, dissolve);
            _ballInstanceCounts[bucketIndex] = count + 1;
        }

        private void DrawBallsInstanced()
        {
            for (int lod = 0; lod < BALL_LOD_COUNT; lod++) _ballRenderers[lod].PulseTime = _wallClock;

            for (int typeIndex = 0; typeIndex < BALL_TYPE_COUNT; typeIndex++)
                for (int lod = 0; lod < BALL_LOD_COUNT; lod++)
                {
                    int bucketIndex = typeIndex * BALL_LOD_COUNT + lod;
                    int count = _ballInstanceCounts[bucketIndex];
                    if (count == 0) continue;

                    BallType type = (BallType)(typeIndex + 1);

                    _ballRenderers[lod].Draw(_camera, _ballInstances[bucketIndex], count,
                        BasicEffectParamsProvider.GetEffectByType(type),
                        BasicEffectParamsProvider.GetDiffuseTintByType(type));
                }
        }

        /// <summary>
        /// The launch smears. A ball leaves at <see cref="SHOOT_SPEED"/> — several diameters a frame — so
        /// the shot itself is not something the eye can follow; the smear is what sells it. It is anchored
        /// at the muzzle and lives its own short life rather than following the ball, and its <b>bright,
        /// wide end is the leading one</b>: the muzzle end is hidden behind the barrel, so a muzzle-bright
        /// streak shows only its faint tapering tip and reads as a thin thread.
        /// </summary>
        private void DrawShotTrails()
        {
            if (_trails.Count == 0) return;

            _trailViewParam.SetValue(_camera.View);
            _trailProjectionParam.SetValue(_camera.Projection);
            _trailCameraPositionParam.SetValue(_camera.Position);

            GraphicsDevice.BlendState = BlendState.Additive;
            GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.SetVertexBuffer(_shotTrailVertexBuffer);
            GraphicsDevice.Indices = _shotTrailIndexBuffer;

            foreach (ShotTrail trail in _trails)
            {
                //Held near-full for most of the life and dropped away at the end (1 - t²), so the smear
                //does not dim the instant it appears and get missed
                float t = trail.Age / TRAIL_LIFETIME;

                _trailHeadParam.SetValue(trail.Origin + trail.Direction * TRAIL_LENGTH);
                _trailTailParam.SetValue(trail.Origin);
                _trailColorParam.SetValue(trail.Color);
                _trailAlphaParam.SetValue(1f - t * t);

                _shotTrailEffect.CurrentTechnique.Passes[0].Apply();
                GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// The smear's colour: the ball type's diffuse tint decoded to linear, its hue kept but its peak
        /// lifted to a floor so even the near-black ball leaves a faint grey smear, then boosted over 1 so
        /// the streak glows and blooms through the glare.
        /// </summary>
        private static Vector3 TrailColorFor(BallType type)
        {
            Vector3 linear = ColorSpace.SrgbToLinear(BasicEffectParamsProvider.GetDiffuseTintByType(type));

            float peak = MathF.Max(linear.X, MathF.Max(linear.Y, linear.Z));
            if (peak < TRAIL_COLOR_FLOOR) linear *= TRAIL_COLOR_FLOOR / MathF.Max(peak, 1e-4f);

            return linear * TRAIL_BRIGHTNESS;
        }

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

        /// <summary>
        /// The smear billboard: a unit quad whose texture channel carries (side in {-1,1}, along in {0 tail,
        /// 1 head}); the shader places it in world space from each trail's head and tail. The vertex
        /// positions are unused, so one shared quad serves every trail.
        /// </summary>
        private void CreateShotTrailQuad()
        {
            VertexPositionTexture[] corners =
            {
                new(Vector3.Zero, new Vector2(-1f, 0f)), //tail, left
                new(Vector3.Zero, new Vector2(1f, 0f)),  //tail, right
                new(Vector3.Zero, new Vector2(-1f, 1f)), //head, left
                new(Vector3.Zero, new Vector2(1f, 1f))   //head, right
            };

            _shotTrailVertexBuffer = new VertexBuffer(GraphicsDevice, VertexPositionTexture.VertexDeclaration, corners.Length, BufferUsage.WriteOnly);
            _shotTrailVertexBuffer.SetData(corners);

            short[] indices = { 0, 1, 2, 2, 1, 3 };
            _shotTrailIndexBuffer = new IndexBuffer(GraphicsDevice, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            _shotTrailIndexBuffer.SetData(indices);

            _shotTrailEffect = Content.Load<Effect>("Shaders/ShotTrail");

            _trailViewParam = _shotTrailEffect.Parameters["View"];
            _trailProjectionParam = _shotTrailEffect.Parameters["Projection"];
            _trailCameraPositionParam = _shotTrailEffect.Parameters["CameraPosition"];
            _trailHeadParam = _shotTrailEffect.Parameters["TrailHead"];
            _trailTailParam = _shotTrailEffect.Parameters["TrailTail"];
            _trailColorParam = _shotTrailEffect.Parameters["TrailColor"];
            _trailAlphaParam = _shotTrailEffect.Parameters["TrailAlpha"];

            //The widths never change; a parameter's value persists on the effect, so once is enough
            _shotTrailEffect.Parameters["TrailHeadWidth"].SetValue(TRAIL_LEAD_WIDTH);
            _shotTrailEffect.Parameters["TrailTailWidth"].SetValue(TRAIL_MUZZLE_WIDTH);
        }

        #endregion

        #region The gun's geometry

        /// <summary>The direction the gun fires: from the trunnions towards its aim target.</summary>
        private Vector3 CannonAimDirection() => Vector3.Normalize(_cannon.AimTarget - _cannon.Position);

        /// <summary>
        /// Where the ball at the head of the queue sits, and so where a shot leaves from: on the barrel
        /// axis, ahead of the trunnions the barrel turns about.
        /// </summary>
        private Vector3 CannonMuzzlePosition() => _cannon.Position + CannonAimDirection() * CANNON_PIVOT_TO_FRONT_BALL;

        /// <summary>
        /// The barrel's orientation: forward down the aim, with the magazine slot (the mesh's local +Y)
        /// pinned to <b>world</b> up, so the slit stays on the barrel's upper face and never rolls about
        /// the bore — the gun sits on a stand that only elevates and traverses.
        /// </summary>
        private Matrix CannonOrientation() => Matrix.CreateWorld(Vector3.Zero, CannonAimDirection(), Vector3.Up);

        /// <summary>
        /// The lens for precise aim: behind the muzzle along the bore and lifted over it, so the camera looks
        /// down the aim with the barrel a small sliver along the bottom of the frame — grounding, not
        /// obstruction. Its Y is floored at <see cref="ADS_MIN_Y"/>: aiming steeply up, <c>-aim</c> points
        /// down and the set-back would otherwise drop the lens through the island and show it from below.
        /// </summary>
        private Vector3 AdsCameraPosition()
        {
            Vector3 aim = CannonAimDirection();
            Vector3 lens = CannonMuzzlePosition() - aim * ADS_BACK + AdsCamUp() * ADS_RISE;

            lens.Y = MathF.Max(lens.Y, ADS_MIN_Y);

            return lens;
        }

        /// <summary>
        /// What that lens looks at: a point <b>on the shot ray</b> (muzzle + aim · d), so screen centre marks
        /// where the shot is directed — honest before gravity, since that is the direction the ball leaves
        /// on. The depth <c>d</c> is the cluster's, clamped: the crosshair is on the ray at any depth, so
        /// this only centres the small over-the-barrel parallax over the range the impact face sweeps.
        /// <para>
        /// The honest limit is that parallax. Because the lens sits <see cref="ADS_RISE"/> above the ray, the
        /// crosshair is pixel-exact only at <c>d</c> — a nearer impact reads slightly low. Fixing that would
        /// mean raycasting the cluster for the true first contact and setting <c>d</c> to the hit distance.
        /// </para>
        /// </summary>
        private Vector3 AdsCameraTarget()
        {
            Vector3 aim = CannonAimDirection();
            Vector3 muzzle = CannonMuzzlePosition();

            Vector3 clusterCentre = new(_cannon.OrbitCenter.X, _clusterCentreY, _cannon.OrbitCenter.Z);
            float d = MathHelper.Clamp(Vector3.Dot(clusterCentre - muzzle, aim), ADS_CONVERGE_MIN, ADS_CONVERGE_MAX);

            return muzzle + aim * d;
        }

        /// <summary>
        /// The up the lens is <b>lifted</b> along: world up made perpendicular to the bore, so the lift is
        /// straight over the barrel at every pitch and yaw and the tube stays a bottom-centre sliver. The
        /// <b>view</b> up is plain world up — <see cref="RecoilCamera"/> builds its basis from one — which is
        /// what keeps the horizon level. Well conditioned across <see cref="Cannon"/>'s elevation clamp: at
        /// its ~80° ceiling the bore is still ~10° off vertical, so |up|² stays around 0.03, far above the
        /// threshold below; the horizontal fallback only trips within ~0.6° of straight up.
        /// </summary>
        private Vector3 AdsCamUp()
        {
            Vector3 aim = CannonAimDirection();
            Vector3 up = Vector3.Up - aim * Vector3.Dot(Vector3.Up, aim);

            return up.LengthSquared() < 1e-4f ? Vector3.Normalize(new Vector3(aim.Z, 0f, -aim.X)) : Vector3.Normalize(up);
        }

        /// <summary>
        /// How far the barrel is displaced by its own recoil this instant — straight back along the bore, and
        /// exactly zero once the stroke is over. Squared rather than linear in the stroke, so the shot throws
        /// the gun back at once and the return eases off, which is the shape a recoiling barrel has (the same
        /// reasoning as <see cref="CameraShake"/>'s: a linear amplitude spends most of its life mid-stroke and
        /// reads as a wobble instead of a jolt). Applied where the gun is <b>drawn</b> and nowhere else.
        /// </summary>
        private Vector3 CannonRecoilOffset() =>
            _cannonRecoil <= 0f ? Vector3.Zero : CannonAimDirection() * (-CANNON_RECOIL_BACK * _cannonRecoil * _cannonRecoil);

        /// <summary>
        /// Where the barrel is drawn: its orientation with the recoiled pivot written straight into the
        /// translation row. <see cref="CannonOrientation"/> carries no translation of its own, so orientation
        /// × translation is exactly the orientation with that row set — no 4×4 multiply needed.
        /// </summary>
        private Matrix CannonWorld()
        {
            Matrix world = CannonOrientation();
            Vector3 pivot = _cannon.Position + CannonRecoilOffset();

            world.M41 = pivot.X;
            world.M42 = pivot.Y;
            world.M43 = pivot.Z;

            return world;
        }

        #endregion

        /// <summary>
        /// What the magazine loads next: one of the colours <b>still hanging</b> (see
        /// <see cref="RecountBallTypes"/>), drawn evenly among them off the unseeded run-to-run generator.
        /// Not an instance method by accident — the live set changes with every shot that lands, so it cannot
        /// be static the way it was when the cluster was a fixed pyramid.
        /// </summary>
        private BallType RandomBallType()
        {
            int live = 0;
            for (int i = 0; i < _ballsOfType.Length; i++) if (_ballsOfType[i] > 0) live++;

            //An empty cluster — a level authored with no balls, or one the player has just cleared. There is
            //nothing left to match, so what is loaded cannot matter; the default four keep the barrel full.
            if (live == 0) return DEFAULT_BALL_TYPES[RANDOM.Next(DEFAULT_BALL_TYPES.Length)];

            int pick = RANDOM.Next(live);

            for (int i = 0; i < _ballsOfType.Length; i++)
            {
                if (_ballsOfType[i] <= 0) continue;
                if (pick == 0) return (BallType)(i + 1);

                pick--;
            }

            return DEFAULT_BALL_TYPES[0]; //unreachable: pick < live, and live is the count of the loop's hits
        }

        protected override void UnloadContent()
        {
            _sceneTarget?.Dispose();
            _glareBright?.Dispose();
            _glareStreak?.Dispose();
            _fullScreenQuad?.Dispose();
            _shotTrailVertexBuffer?.Dispose();
            _shotTrailIndexBuffer?.Dispose();
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

            //Order matters: ContactEvents unhooks itself from the timestepper, so it has to go before the
            //simulation it hooked into, and the pool it allocated from has to outlive both.
            _events?.Dispose();
            _simulation?.Dispose();
            _threadDispatcher?.Dispose();
            _bufferPool?.Clear();

            base.UnloadContent();
        }
    }
}
