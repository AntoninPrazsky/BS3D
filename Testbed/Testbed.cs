using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuUtilities;
using BepuUtilities.Memory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Input;
using Prazsky.BS3D.Physics;
using Prazsky.Core;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Testbed.Backdrops;
using static Testbed.Simu;
using mgKeys = Microsoft.Xna.Framework.Input.Keys;

namespace Testbed
{
    public class Testbed : Game
    {
        private BasicCamera3D _camera;
        private Model _hrSphere;

        #region Instanced ball rendering (issues #19 and #28)

        private static readonly int BALL_TYPE_COUNT = (int)BallType.Type4;

        //Procedurally generated sphere LODs, finest first: {slices, stacks} and the camera distance
        //up to which each level is used (the last level covers everything beyond the last distance).
        //Per-pixel lighting shades even the coarse levels smoothly; only the silhouette reveals polygons.
        private static readonly int[,] BALL_LOD_RESOLUTIONS = { { 32, 24 }, { 16, 12 }, { 10, 7 } };
        private static readonly float[] BALL_LOD_DISTANCES = { 15f, 30f };
        private static readonly int BALL_LOD_COUNT = 3;

        //Beach-ball pattern (concept art in issue #43): five gores in the type color, each narrower than
        //the three the ball started out with, separated by narrower white ones, plus a white polar disc
        private static readonly int BALL_PATTERN_GORES = 5;

        /// <summary>
        /// Diffuse reflectance of the ball material, multiplying both pattern colors. It used to be a
        /// flat 1 — a surface that reflects every photon that reaches it, which nothing real does; white
        /// vinyl manages about this. In gamma space dropping it muted the colors, which is why it was
        /// raised in the first place, but that was the wrong composition talking: in linear light this
        /// scales radiance evenly and takes the glare off without touching the hue or the saturation.
        /// The one number to nudge if the balls want to be darker or lighter still.
        /// </summary>
        private static readonly float BALL_ALBEDO = 0.5f;

        /// <summary>
        /// How much of its own color a ball radiates. Kept well under 1: the ball should read as lit from
        /// within, not as a lamp — past about a third it stops looking like a glowing object and starts
        /// looking like an unlit one with the brightness turned up, because the shading that gives it its
        /// shape gets drowned out.
        /// </summary>
        private static readonly float BALL_EMISSION = 0.5f;

        /// <summary>How much light passes through the shell from a source behind the ball.</summary>
        private static readonly float BALL_TRANSLUCENCY = 0.35f;

        /// <summary>
        /// A resting human heart, near enough. Slow on purpose — a fast pulse reads as an alarm rather
        /// than as something alive and calm.
        /// </summary>
        private static readonly float BALL_PULSE_BEATS_PER_SECOND = 1.1f;

        private static readonly float BALL_PULSE_DEPTH = 0.55f;

        /// <summary>
        /// World units one beat spans as it travels up the cluster. Comparable to the height of a full
        /// map, so a beat is visibly a wave crossing the structure rather than a uniform flash.
        /// </summary>
        private static readonly float BALL_PULSE_WAVELENGTH = 14f;

        private float _pulseSeconds;

        private SphereMesh[] _ballMeshes;
        private InstancedModelRenderer[] _ballRenderers;

        #region Shadow mapping (issue #41)

        private static readonly int SHADOW_MAP_SIZE = 2048;
        private static readonly float SHADOW_STRENGTH = 0.4f;
        private static readonly float SHADOW_AREA = 90f; //World units covered by the light's ortho projection and by the ground overlay quad

        /// <summary>
        /// Y of the quad receiving the shadows: just above the ground top (the ground box top sits at -9.5).
        /// </summary>
        private static readonly float SHADOW_OVERLAY_Y = -9.49f;

        private RenderTarget2D _shadowMap;
        private Microsoft.Xna.Framework.Matrix _lightViewProjection;
        private Effect _shadowOverlayEffect;
        private readonly VertexPosition[] _shadowOverlayVertices = new VertexPosition[4];
        private static readonly short[] SHADOW_OVERLAY_INDICES = { 0, 1, 2, 2, 1, 3 };

        #endregion

        #region Contact AO blobs (the dark contact pool on the ground around resting balls)

        private static readonly float BLOB_RADIUS = 1.1f;
        private static readonly float BLOB_FADE_HEIGHT = 2.5f; //Measured from the ground plane; a resting ball center sits 0.5 above it
        private static readonly float BLOB_STRENGTH = 0.4f;

        private ModelInstance[] _blobInstances = new ModelInstance[256];
        private int _blobInstanceCount;
        private DynamicVertexBuffer _blobInstanceBuffer;
        private VertexBuffer _blobQuadVertexBuffer;
        private IndexBuffer _blobQuadIndexBuffer;

        #endregion

        //One instance bucket per ball type and LOD level; each bucket becomes a single instanced draw call
        private readonly ModelInstance[][] _ballInstances = new ModelInstance[BALL_TYPE_COUNT * BALL_LOD_COUNT][];
        private readonly int[] _ballInstanceCounts = new int[BALL_TYPE_COUNT * BALL_LOD_COUNT];
        private readonly int[] _ballLodTotals = new int[BALL_LOD_COUNT];

        /// <summary>
        /// How dark a fully surrounded ball gets: its lighting is scaled down by up to this fraction
        /// (neighbor-based ambient occlusion, issue #40).
        /// </summary>
        private static readonly float BALL_OCCLUSION_STRENGTH = 0.55f;

        /// <summary>
        /// The most occluders a ball can have: 12 touching neighbor cells.
        /// </summary>
        private static readonly int MAX_BALL_OCCLUDERS = 12;

        /// <summary>
        /// Time constant of the occlusion easing (roughly three times this to arrive). The occlusion is computed
        /// from the grid, which changes in a single step - a ball is released, another one attaches - while the
        /// balls involved have not moved yet, so without the easing they would pop brighter (a released ball and
        /// the neighbors it leaves behind) or darker (an attached ball and the neighbors it joins) a whole
        /// frame before anything visibly happened.
        /// </summary>
        private static readonly float BALL_OCCLUSION_EASE_SECONDS = 1f;

        /// <summary>
        /// Time constant of the glide a freshly attached ball is drawn with, and the offset below which the
        /// glide is dropped (a twentieth of a ball radius is under a pixel at any distance it is drawn from).
        /// </summary>
        private static readonly float BALL_ATTACH_GLIDE_SECONDS = 0.08f;

        private static readonly float BALL_ATTACH_GLIDE_DONE_SQUARED = 0.025f * 0.025f;

        //Last frame's statistics (how many balls passed frustum culling out of how many exist)
        private int _drawnBalls;
        private int _collectedBalls;

        #endregion

        #region Scene object rendering (same lit shader as the balls, drawn one instance at a time)

        private Effect _instancingEffect;
        private InstancedModelRenderer _groundRenderer;
        private InstancedModelRenderer _ceilingRenderer;
        private InstancedModelRenderer _cannonRenderer;

        /// <summary>
        /// The ceiling plate is a procedurally generated translucent glass box, rebuilt at the exact
        /// field size of the loaded map (no model asset, no non-uniform scaling of a fixed mesh).
        /// </summary>
        private BoxMesh _ceilingMesh;

        private static readonly Vector3 CEILING_GLASS_COLOR = new(0.55f, 0.75f, 0.85f);
        private static readonly float CEILING_GLASS_ALPHA = 0.4f;

        /// <summary>
        /// Lighting parameters shared by all scene objects; the ambient color is set by
        /// <see cref="ApplySkyLighting"/>, zero specular keeps each mesh part's own material specular.
        /// </summary>
        private readonly BasicEffectParams _sceneEffectParams = new(Vector3.One * SCENE_AMBIENT_INTENSITY, Vector3.Zero, 0f, Vector3.Zero);

        #endregion

        #region Ground

        /// <summary>
        /// Size of one ground block. The GroundMarble model is modeled at this size and its texture is
        /// mapped for it, so the field is tiled from copies rather than stretched from a single slab.
        /// </summary>
        private static readonly float GROUND_BLOCK_SIZE = 30f;

        /// <summary>
        /// How many blocks the ground reaches from the center in each direction. It has to extend past
        /// the castle backdrop (whose base reaches Z -95) and far enough beyond that its edge falls close
        /// to the horizon. All the blocks are one instanced draw call, so the extent is nearly free.
        /// </summary>
        private static readonly int GROUND_BLOCK_RADIUS = 4;

        /// <summary>Y of the recessed center block and of the plateau around it.</summary>
        private static readonly float GROUND_PIT_Y = -10f;

        private static readonly float GROUND_PLATEAU_Y = -9f;

        /// <summary>The ground has no neighboring-cell occlusion; the shader still expects the vector.</summary>
        private static readonly Vector4 GROUND_NO_OCCLUSION = new(0f, 0f, 0f, 1f);

        private ModelInstance[] _groundInstances;

        #endregion

        private Model _groundModel;
        private KinematicBody _ceiling;
        private TypedIndex _ceilingShapeIndex;

        /// <summary>
        /// Size of the ceiling before a map is loaded.
        /// </summary>
        private static readonly float DEFAULT_CEILING_SIZE = 10f;

        private Simulation _simulation;
        private ThreadDispatcher _threadDispatcher;
        private BufferPool _bufferPool;

        private List<StaticBody> _staticBodies;
        private PhysicsBall[,,] _physicsBalls;
        private BallsMap _map;

        private CameraInputHelper _cih;

        private SkyDome _sky;
        private Model _skyModel;
        private byte _skyModelNumber = 1;

        private ButtonAction[] _actions;

        private bool _simulate = true;
        private bool _draw = true;
        private bool _gameMode = false;
        private bool _slowSimulation = false;

        private Vector3 _gameCameraOffset = Vector3.Up * 6.5f;

        #region Game mode transition animation

        private bool _gameModeAnimStarted = false;
        private bool _freeModeAnimStarted = false;
        private float _gameModeAnimStep = 0f;

        private static readonly float ANIMATION_SPEED = Constants.THOUSANDTH;

        private Vector3 _beforeAnimationPosition = Vector3.Zero;
        private Vector3 _beforeAnimationTarget = Vector3.Zero;

        #endregion

        #region Graphics

        private int _windowWidth;
        private int _windowHeight;

        private GraphicsDeviceManager _graphics;
        private bool _windowed;

        private InfoRenderer _info;

        //Only used when supersampling is off: multisampling antialiases geometry edges but not shading,
        //and the balls' procedural relief is shading
        private static readonly int MSAA_SAMPLES = 8;

        /// <summary>
        /// Chosen so the daylight domes land at roughly the brightness the gamma-space renderer used to
        /// show. It is a starting point for a rig that was lit by eye in the wrong space, not a
        /// photometric value — the whole lighting rig wants re-balancing now that it composes correctly.
        /// </summary>
        private const float DEFAULT_EXPOSURE = 1.1f;

        /// <summary>LightSlateGray, the old clear color, decoded into the linear space the target holds.</summary>
        private static readonly Color CLEAR_COLOR_LINEAR = new(new Vector3(0.185f, 0.246f, 0.319f));

        /// <summary>
        /// The scene renders into a target this many times larger per axis and is box-filtered down on
        /// the way to the back buffer. The balls' relief is the reason: it is a high-frequency signal
        /// evaluated per pixel, so raising the sampling rate is the only thing that keeps its fine
        /// octaves — which band-limit themselves against the pixel footprint — alive and sharp instead
        /// of quietly fading out. 1 disables it and hands the antialiasing back to MSAA.
        /// </summary>
        private readonly int _supersampleFactor;

        /// <summary>
        /// The scene renders into this instead of the back buffer, and always does: it is where linear
        /// radiance lives. A half-float format because linear light is open-ended — a lit highlight is
        /// genuinely several times brighter than white, and an 8-bit target would clip it flat before
        /// the tonemap curve ever got a chance to roll it off.
        /// </summary>
        private RenderTarget2D _sceneTarget;

        private Effect _tonemapEffect;
        private VertexBuffer _fullScreenQuad;

        #region Clouds

        /// <summary>
        /// Draws the sky dome with a procedural cloud layer over its baked gradient. The same field is
        /// read back by the scene shader for cloud shadows and by the CPU for the light rig, so all three
        /// agree by construction — see <c>Shaders/Clouds.fxh</c>, which is the only place it is defined.
        /// </summary>
        private Effect _skyEffect;

        /// <summary>
        /// The one cloud field, shared by the sky shader, the scene shader's shadows and the light rig.
        /// Its defaults are the tuned ones; only the clock moves at runtime.
        /// </summary>
        private readonly CloudField _clouds = new();

        /// <summary>
        /// How much cloud is over the arena, smoothed: 0 clear, 1 solid. Read straight off the field it
        /// would jump about as the sample crossed an edge, and the ambient of a whole scene snapping is
        /// far more noticeable than the cloud that caused it.
        /// </summary>
        private float _overcast;

        /// <summary>Seconds the overcast reading takes to catch up — about how long a sky takes to close over.</summary>
        private static readonly float OVERCAST_RESPONSE_SECONDS = 2.5f;

        /// <summary>How wide a patch of sky the ambient reads, roughly one cloud across.</summary>
        private static readonly float OVERCAST_SAMPLE_RADIUS = 320f;

        /// <summary>The dome's palette, decoded once per dome and re-applied whenever the weather moves.</summary>
        private Vector3 _zenithLinear = Vector3.One;

        private Vector3 _horizonLinear = Vector3.One;

        /// <summary>
        /// How hard the fine octaves chew at the shape the weather layer drew. Has to be read against
        /// <see cref="CLOUD_COVERAGE_GAIN"/>, which is what the weather is multiplied by: at 0.55 against a
        /// gain of 2.8 the detail was modulating the thickness by about six percent and the clouds came
        /// out airbrushed. It wants to be a decent fraction of the weather's own amplitude.
        /// </summary>
        private static readonly float CLOUD_DETAIL_STRENGTH = 2.5f;

        /// <summary>
        /// Lit tops and shadowed undersides, in **linear radiance** — these are quantities of light, not
        /// sRGB paint colors, so nothing decodes them. The sunlit color runs above 1 deliberately: a cloud
        /// edge with the sun behind it really is brighter than white paper, and saying so is what sends it
        /// through the same glare and the same highlight roll-off as the balls.
        /// </summary>
        private static readonly Vector3 CLOUD_SUN_COLOR = new(1.7f, 1.66f, 1.55f);

        //Well below the sunlit color rather than a shade under it. The frame goes through an ACES curve
        //that compresses the highlights hard, so two linear values close together up there come out of the
        //tonemapper as the same white - at 0.45 the undersides were indistinguishable from the tops and
        //the whole layer read as flat paper.
        private static readonly Vector3 CLOUD_SHADOW_COLOR = new(0.18f, 0.21f, 0.28f);

        /// <summary>
        /// Opacity of the densest cloud, and the elevation over which cloud fades into haze. Well over 1,
        /// so a cloud reaches solid at about half density and only its edges stay translucent — at 1.15 the
        /// whole layer was semi-transparent everywhere and read as haze rather than as weather.
        /// </summary>
        private static readonly float CLOUD_OPACITY = 2.4f;

        private static readonly float CLOUD_HORIZON_FADE = 0.16f;

        /// <summary>How far along the sun the shading looks to decide whether a piece of cloud is backlit.</summary>
        private static readonly float CLOUD_SUN_STEP = 90f;

        /// <summary>
        /// How much light a piece of cloud swallows on the way through its own body, and how much the
        /// cloud between it and the sun swallows first. The body term is the one that matters: it is what
        /// turns a flat white field into undersides with dark cores and edges the light comes through.
        /// </summary>
        private static readonly float CLOUD_SELF_ABSORPTION = 2.5f;

        private static readonly float CLOUD_SUN_ABSORPTION = 1f;

        /// <summary>The silver lining: forward scattering towards the sun, and how tightly it hugs it.</summary>
        private static readonly float CLOUD_SILVER_STRENGTH = 1.2f;

        private static readonly float CLOUD_SILVER_POWER = 12f;

        #endregion

        #region City prototype ("city" on the command line)

        private City _city;
        private BoxMesh _unitBox;
        private InstancedModelRenderer _cityRenderer;
        private InstancedModelRenderer _arenaGlassRenderer;
        private InstancedModelRenderer _arenaFrameRenderer;
        private ModelInstance[] _arenaGlassInstances;
        private ModelInstance[] _arenaFrameInstances;

        /// <summary>
        /// Half-width of the play surface, and the width of the marble band around it. Chosen as a whole
        /// number of panels that also divides the recess evenly (see <see cref="ARENA_PIT_HALF_EXTENT"/>).
        /// </summary>
        private static readonly float ARENA_HALF_EXTENT = 60f;

        private static readonly float ARENA_FRAME_WIDTH = 9f;

        /// <summary>Edge length of one panel, and the width of the marble mullion between panels.</summary>
        private static readonly float ARENA_PANEL_SIZE = 15f;

        private static readonly float ARENA_MULLION_WIDTH = 1.1f;

        /// <summary>
        /// Top of the floor, and top of the recess in the middle of it. Both are read off the physics
        /// ground blocks rather than guessed: the balls rest on those blocks, and a floor drawn anywhere
        /// else leaves them hanging over it or sunk into it. The recess is the bath the cluster settles
        /// into, and it was lost when the drawn ground became a single flat sheet.
        /// </summary>
        private static readonly float ARENA_Y = GROUND_PLATEAU_Y + Constants.HALF;

        private static readonly float ARENA_PIT_Y = GROUND_PIT_Y + Constants.HALF;

        /// <summary>Half-width of the recess: exactly the one recessed ground block, which is 30 across.</summary>
        private static readonly float ARENA_PIT_HALF_EXTENT = GROUND_BLOCK_SIZE * Constants.HALF;

        /// <summary>
        /// Thickness of every floor panel, mullion and band. It has to exceed the depth of the recess,
        /// since the sides of the panels around the recess are what draw its wall.
        /// </summary>
        private static readonly float ARENA_FLOOR_THICKNESS = 1.2f;

        private static readonly Vector3 ARENA_GLASS_COLOR = new(0.42f, 0.62f, 0.72f);
        private static readonly float ARENA_GLASS_ALPHA = 0.24f;

        private static readonly Vector3 ARENA_MARBLE_COLOR = new(0.58f, 0.56f, 0.54f);

        /// <summary>
        /// How brightly a lit window burns. Deliberately kept under <see cref="GLARE_THRESHOLD"/>: a
        /// window that glares is a window that veils the tower behind it, and a thousand of them turn the
        /// skyline into one white haze. The glare belongs to the balls — the windows only have to be
        /// bright enough that the unlit one beside them reads as dark.
        /// </summary>
        private static readonly float CITY_WINDOW_BRIGHTNESS = 0.35f;

        #endregion

        #region Glare

        /// <summary>
        /// Quarter-resolution ping-pong targets for the glare: the bright pass writes the first, the
        /// streak pass reads it and writes the second, and the tonemap adds that back. Quarter resolution
        /// throughout — glare is the one thing in the frame that is meant to be blurry, and running the
        /// streak star at full resolution would cost 193 taps a pixel for no visible gain.
        /// </summary>
        private RenderTarget2D _glareBright;
        private RenderTarget2D _glareStreak;
        private Effect _glareEffect;

        private static readonly int GLARE_DOWNSAMPLE = 4;

        /// <summary>
        /// Radiance a pixel has to exceed before it starts to glare. Above the lit scene but below a
        /// pulsing ball, so the glare belongs to the balls rather than to every bright surface.
        /// </summary>
        private static readonly float GLARE_THRESHOLD = 0.38f;

        /// <summary>Length of one streak arm in quarter-resolution texels, and its exponential falloff.</summary>
        private static readonly float GLARE_STREAK_LENGTH = 34f;

        private static readonly float GLARE_STREAK_FALLOFF = 3.2f;

        /// <summary>
        /// How much of the glare is added back. Still exaggerated — the point is to make it unmistakable
        /// that the balls emit light, not to model a lens — but no longer at 2.6, which was tuned while
        /// the spheres were being drawn inside out. Correcting their winding turned the far hemisphere
        /// the balls had been showing into the near one, and everything above the threshold got brighter
        /// at once; the same number afterwards bleached a hole in the skyline behind the cluster.
        /// </summary>
        private static readonly float GLARE_INTENSITY = 1.3f;

        #endregion

        /// <summary>
        /// Linear scale applied to the scene before the tonemap curve — the renderer's shutter speed.
        /// Overridable with "exposure=&lt;f&gt;" on the command line, which is how a sky dome that is much
        /// brighter or darker than the rest gets checked without a rebuild.
        /// </summary>
        private readonly float _exposure;

        private readonly bool _uncappedFps;
        private static float GAME_FOV = (float)Math.PI / 3.1f;
        private static float FREE_FOV = (float)Math.PI / 2.5f;
        private static Vector3 DEFAULT_CAMERA_POS = new (0f, -3f, 30f);

        #endregion Graphics

        #region Shooting

        private BodyDescription _shotBall;
        private List<PhysicsBall> _shotBalls;

        //Balls released from the structure (matched clusters and balls that lost their connection to the ceiling).
        //They are no longer part of the map, but their bodies keep falling in the simulation, so they still have to be drawn.
        //RemoveFallenBalls cleans them up once they fall out of the world or come to rest.
        private List<PhysicsBall> _fallingBalls;

        private static readonly float SHOOT_MULTIPLIER = 200f;
        private static readonly Random RANDOM = new();

        private Model _cilinderModel;
        private Cannon _cannon;

        private SpriteBatch _spriteBatch;
        private Texture2D _aimer;
        private Vector2 _aimerPos;
        private Color _aimerColor = new((byte)255, (byte)255, (byte)255, (byte)64);

        #endregion

        #region Backdrops


        #endregion

        #region Contacts

        private ContactEvents _events;
        private EventHandler _eventHandler;

        #endregion

        //Map file to load right after startup (e.g. passed on the command line); mainly for testing
        private readonly string _startupMapPath;

        //Testing mode: shoots a ball at a random spot of the structure every second ("autoshoot" on the command line)
        private readonly bool _autoShoot;
        private float _autoShootElapsed;

        //Testing mode: loads this map on top of the running one after a delay ("switchmap=<path>" on the command line);
        //exercises the map re-loading path that the F2 dialog and file drag-and-drop use
        private readonly string _switchMapPath;
        private float _switchMapElapsed;
        private bool _switchMapDone;
        private static readonly float SWITCH_MAP_DELAY_SECONDS = 10f;

        public Testbed(bool windowed = true, int windowWidth = 1280, int windowHeight = 800, string startupMapPath = null, bool autoShoot = false, string switchMapPath = null, byte skyNumber = 0, bool uncappedFps = false, int supersampleFactor = 2, float exposure = DEFAULT_EXPOSURE)
        {
            _exposure = exposure > 0f ? exposure : DEFAULT_EXPOSURE;
            _windowed = windowed;
            _startupMapPath = startupMapPath;
            _autoShoot = autoShoot;
            _switchMapPath = switchMapPath;
            _uncappedFps = uncappedFps; //Testing: "nocap" on the command line disables vsync so real rendering headroom can be measured
            _supersampleFactor = Math.Clamp(supersampleFactor, 1, 4); //Testing: "ssaa=<n>" on the command line trades sharpness against fill rate
            if (skyNumber >= 1 && skyNumber <= 18) _skyModelNumber = skyNumber; //Testing: "sky=<n>" on the command line picks the starting sky dome

            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreparingDeviceSettings += Graphics_PreparingDeviceSettings;
        
            Content.RootDirectory = "Content";

            _windowWidth = windowWidth;
            _windowHeight = windowHeight;

            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += Window_ClientSizeChanged;
            Window.FileDrop += Window_FileDrop;

            SetGraphics(_windowed);
        }

        private void Window_FileDrop(object sender, FileDropEventArgs e)
        {
            if (e.Files == null || e.Files.Length <= 0 || string.IsNullOrEmpty(e.Files[0])) return;
            DeserializeMapFromFile(e.Files[0]);
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            _camera.AspectRatio = GraphicsDevice.Viewport.AspectRatio;
            _info.RecomputeScale();
            ComputeAimerPosition();
            EnsureSceneTarget();
        }

        protected override void Initialize()
        {
            IsMouseVisible = true;

            _camera = new BasicCamera3D(DEFAULT_CAMERA_POS, GraphicsDevice.Viewport.AspectRatio, FREE_FOV);
            _info = new InfoRenderer(this, "Content/Fonts/cascadia", "Content/Bitmaps/Controller") { DrawOrder = int.MaxValue };
            Components.Add(_info);

            _cih = new CameraInputHelper(_camera, this);

            _staticBodies = new List<StaticBody>();

            _threadDispatcher = new ThreadDispatcher(Environment.ProcessorCount);
            _bufferPool = new BufferPool();
            _events = new ContactEvents(_threadDispatcher, _bufferPool);

            _simulation = Simulation.Create(
                _bufferPool,
                new NarrowPhaseCallbacks(_events),
                new PoseIntegratorCallbacks(new System.Numerics.Vector3(0, Constants.EARTH_GRAVITY, 0)),
                new SolveDescription(8, 1));

            #region Controls

            _actions = new ButtonAction[]
            {
                new(mgKeys.Escape, Buttons.Back, Exit, "Exit"),
                new(mgKeys.F2, Buttons.DPadLeft, LoadBallsMap, "Load map"),
                new(mgKeys.F5, Buttons.B, () => _simulate = !_simulate, "Stop/start simulation"),
                new(mgKeys.F6, Buttons.X, () => _draw = !_draw, "Hide/show 3D rendering"),
                new(mgKeys.F9, () => _slowSimulation = !_slowSimulation, "Switch simulation speed"),
                new(mgKeys.F10, () => SwitchGameMode(!_gameMode), "Switch game mode"),
                new(mgKeys.F11, () => SetGraphics(_graphics.IsFullScreen), "Fullscreen/windowed"),
                new(mgKeys.F12, () => _info.Visible = !_info.Visible, "Hide/show text overlay"),
                new(mgKeys.End, Buttons.Start, ReleaseAllBalls, "Release all balls"),
                new(mgKeys.NumPad1, SwitchSkyDome, "Switch sky dome"),
                new(mgKeys.D1, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Forward, true), "Forward view"),
                new(mgKeys.D2, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Backward, true), "Backward view"),
                new(mgKeys.D3, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Left, true), "Left view"),
                new(mgKeys.D4, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Right, true), "Right view"),
                new(mgKeys.D5, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Up, true), "Up view"),
                new(mgKeys.D6, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Down, true), "Down view"),
                new(mgKeys.R, () => { _cih.RestartCamera(); _cannon.Restart(); }, "Restart camera"),
                new(mgKeys.Space, () => ShootBall(), "Shoot ball")
            };

            string format = "{0,-9} {1}\n";

            StringBuilder builder = new();
            foreach (var act in _actions) builder.Append(string.Format(format, act.Key.ToString(), act.Description));
            builder.Append(string.Format(format, "Arrows", "Cannon aiming"));
            builder.Append(string.Format(format, mgKeys.NumPad4.ToString(), "Move cannon left"));
            builder.Append(string.Format(format, mgKeys.NumPad6.ToString(), "Move cannon right"));
            builder.Append(string.Format(format, mgKeys.NumPad7.ToString(), "Camera orbit left"));
            builder.Append(string.Format(format, mgKeys.NumPad9.ToString(), "Camera orbit right"));

            _info.HintText = builder.ToString();

            #endregion

            InitializeShooting();

            base.Initialize();
        }

        private void SwitchGameMode(bool gameMode)
        {
            if (_gameMode == gameMode) return;
            if (_gameModeAnimStarted || _freeModeAnimStarted) return;

            _gameMode = gameMode;
            _info.ShowIcon = gameMode;

            if (_gameMode)
            {
                _gameModeAnimStarted = true;
                _beforeAnimationPosition = _camera.Position;
                _beforeAnimationTarget = _camera.Target;
            }
            else
            {
                _freeModeAnimStarted = true;
            }
        }

        protected override void LoadContent()
        {
            _hrSphere = Content.Load<Model>("Balls/DebugSphere"); //Still used by BallsMap; balls themselves are drawn as generated spheres

            _instancingEffect = Content.Load<Effect>("Shaders/InstancedModel");
            _ballMeshes = new SphereMesh[BALL_LOD_COUNT];
            _ballRenderers = new InstancedModelRenderer[BALL_LOD_COUNT];

            for (int lod = 0; lod < BALL_LOD_COUNT; lod++)
            {
                _ballMeshes[lod] = new SphereMesh(GraphicsDevice, BallsConstraintsBuilder.BALL_RADIUS, BALL_LOD_RESOLUTIONS[lod, 0], BALL_LOD_RESOLUTIONS[lod, 1]);
                _ballRenderers[lod] = new InstancedModelRenderer(GraphicsDevice, _ballMeshes[lod], BALL_ALBEDO * Vector3.One, _instancingEffect);
                _ballRenderers[lod].PatternGoreCount = BALL_PATTERN_GORES;

                //The balls are alive: they radiate their own color on a heartbeat and pass light through
                //their shell. The beat runs up the cluster rather than firing everywhere at once.
                _ballRenderers[lod].EmissiveStrength = BALL_EMISSION;
                _ballRenderers[lod].TranslucencyStrength = BALL_TRANSLUCENCY;
                _ballRenderers[lod].PulseSpeed = BALL_PULSE_BEATS_PER_SECOND;
                _ballRenderers[lod].PulseDepth = BALL_PULSE_DEPTH;
                _ballRenderers[lod].PulseDirection = Vector3.Up;
                _ballRenderers[lod].PulseWavelength = BALL_PULSE_WAVELENGTH;
            }

            #region Shadow mapping

            _shadowMap = new RenderTarget2D(GraphicsDevice, SHADOW_MAP_SIZE, SHADOW_MAP_SIZE, false, SurfaceFormat.Single, DepthFormat.Depth24);
            _shadowOverlayEffect = Content.Load<Effect>("Shaders/ShadowOverlay");

            _tonemapEffect = Content.Load<Effect>("Shaders/Tonemap");
            _glareEffect = Content.Load<Effect>("Shaders/Glare");
            CreateFullScreenQuad();

            //The key light is directional for shadow purposes; a fixed ortho box around the play field covers
            //everything that can meaningfully cast or receive (the sun position used for shading lies on this axis)
            _lightViewProjection =
                Microsoft.Xna.Framework.Matrix.CreateLookAt(-DefaultLighting.Light0Direction * 60f, Vector3.Zero, Vector3.Up)
                * Microsoft.Xna.Framework.Matrix.CreateOrthographic(SHADOW_AREA, SHADOW_AREA, 1f, 150f);

            float half = SHADOW_AREA * Constants.HALF;
            _shadowOverlayVertices[0] = new(new Vector3(-half, SHADOW_OVERLAY_Y, -half));
            _shadowOverlayVertices[1] = new(new Vector3(half, SHADOW_OVERLAY_Y, -half));
            _shadowOverlayVertices[2] = new(new Vector3(-half, SHADOW_OVERLAY_Y, half));
            _shadowOverlayVertices[3] = new(new Vector3(half, SHADOW_OVERLAY_Y, half));

            _shadowOverlayEffect.Parameters["World"].SetValue(Microsoft.Xna.Framework.Matrix.Identity);
            _shadowOverlayEffect.Parameters["LightViewProjection"].SetValue(_lightViewProjection);
            _shadowOverlayEffect.Parameters["ShadowStrength"].SetValue(SHADOW_STRENGTH);
            _shadowOverlayEffect.Parameters["ShadowMapTexelSize"].SetValue(1f / SHADOW_MAP_SIZE);

            #endregion

            #region Contact AO blobs

            _shadowOverlayEffect.Parameters["GroundY"].SetValue(SHADOW_OVERLAY_Y);
            _shadowOverlayEffect.Parameters["BlobRadius"].SetValue(BLOB_RADIUS);
            _shadowOverlayEffect.Parameters["BlobFadeHeight"].SetValue(BLOB_FADE_HEIGHT);
            _shadowOverlayEffect.Parameters["BlobStrength"].SetValue(BLOB_STRENGTH);

            //Unit quad in the XZ plane; the blob vertex shader scales and places it under each ball
            _blobQuadVertexBuffer = new VertexBuffer(GraphicsDevice, VertexPosition.VertexDeclaration, 4, BufferUsage.WriteOnly);
            _blobQuadVertexBuffer.SetData(new VertexPosition[]
            {
                new(new Vector3(-1f, 0f, -1f)),
                new(new Vector3(1f, 0f, -1f)),
                new(new Vector3(-1f, 0f, 1f)),
                new(new Vector3(1f, 0f, 1f))
            });
            _blobQuadIndexBuffer = new IndexBuffer(GraphicsDevice, IndexElementSize.SixteenBits, SHADOW_OVERLAY_INDICES.Length, BufferUsage.WriteOnly);
            _blobQuadIndexBuffer.SetData(SHADOW_OVERLAY_INDICES);

            //The ground counts into the balls' own ambient occlusion too (dark bellies near the ground)
            foreach (InstancedModelRenderer ballRenderer in _ballRenderers) ballRenderer.GroundHeight = SHADOW_OVERLAY_Y;

            #endregion

            #region Ground and ceiling

            _groundModel = Content.Load<Model>("GameObjects/GroundMarble");

            _groundRenderer = new InstancedModelRenderer(GraphicsDevice, _groundModel, _instancingEffect);

            //The marble texture drew the veining but left the slab geometrically perfect, so it lit like
            //polished glass. A ball-sized grain gives the floor something for the low sun to rake across.
            _groundRenderer.SurfaceReliefFrequency = 9f;
            _groundRenderer.SurfaceReliefStrength = 0.008f;

            //The floor is laid in slabs two balls across, and the joints between them are the only thing
            //on it at a scale the eye reads as structure: at 8 mm the micro-relief is far too shallow for
            //parallax or self-shadowing to show, while a 4 cm joint casts a real shadow and hides its own
            //far wall as the camera moves. Everything below is measured against that joint.
            _groundRenderer.SlabSize = 2f;
            _groundRenderer.SlabJointWidth = 0.025f;
            _groundRenderer.SlabJointDepth = 0.04f;

            _groundRenderer.CavityStrength = 0.7f;
            _groundRenderer.ReliefShadowStrength = 0.85f;
            _groundRenderer.ParallaxScale = 1f;

            RecreateCeilingRenderer(DEFAULT_CEILING_SIZE, DEFAULT_CEILING_SIZE);

            BuildGroundAndCeiling();

            BuildCity();

            #endregion Ground and ceiling

            #region Contact events

            _eventHandler = new EventHandler(_simulation, _bufferPool, _events, _ceiling, _physicsBalls, _shotBalls, _fallingBalls);
            _events.Initialize(_simulation);

            #endregion

            _skyModel = Content.Load<Model>("Skyes/SkyDome" + _skyModelNumber);
            //The dome's vertex colors are sRGB; the target it is drawn into is linear
            _sky = new SkyDome(_skyModel, GraphicsDevice, linearVertexColors: true);

            _skyEffect = Content.Load<Effect>("Shaders/Sky");
            _sky.Effect = _skyEffect;
            SetCloudParameters();

            _cilinderModel = Content.Load<Model>("GameObjects/Cilinder");
            _cannon = new Cannon(_cilinderModel, new Vector3(0f, 5f, 0f), -6.4f, 20f);
            _cannonRenderer = new InstancedModelRenderer(GraphicsDevice, _cilinderModel, _instancingEffect);

            //The cannon aims and orbits, so its metal must be mapped through the model's own UVs
            //(a world-space projection would swim across the barrel as it moves)
            _cannonRenderer.DetailTexture = Content.Load<Texture2D>("GameObjects/CannonMetal");
            _cannonRenderer.DetailTextureMapping = DetailMapping.ModelUVs;
            _cannonRenderer.DetailScale = 3f; //Tiles the cast mottling a few times along the barrel
            _cannonRenderer.DetailStrength = 0.75f;
            _cannonRenderer.DetailBoost = 1.75f;
            _cannonRenderer.DetailNormalMap = Content.Load<Texture2D>("GameObjects/CannonMetalNormal");
            _cannonRenderer.DetailNormalStrength = 0.55f; //Enough relief to catch the light without reading as corrosion

            //Unevenness of the casting, broad enough that it does not compete with the fine grain the
            //normal map already carries — pushed finer than this it reads as a woven mesh over the barrel
            _cannonRenderer.SurfaceReliefFrequency = 10f;
            _cannonRenderer.SurfaceReliefStrength = 0.0037f;

            //Cast metal: shallow pitting, so only the cavity term has anything to work with
            _cannonRenderer.CavityStrength = 0.45f;


            //The ground darkens the downward-facing parts of the scene objects too, like the ball bellies
            _cannonRenderer.GroundHeight = SHADOW_OVERLAY_Y;

            _aimer = Content.Load<Texture2D>("Bitmaps/Aimer");
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            ComputeAimerPosition();
            EnsureSceneTarget();

            if (!string.IsNullOrEmpty(_startupMapPath) && File.Exists(_startupMapPath)) DeserializeMapFromFile(_startupMapPath);

            ApplySkyLighting();
        }

        /// <summary>
        /// Ambient intensity of the scene objects (ground, ceiling, cannon, castle). The sky tint itself
        /// comes from the hemisphere colors in the shader, so this stays a neutral gray.
        /// </summary>
        private static readonly float SCENE_AMBIENT_INTENSITY = 0.25f;

        /// <summary>
        /// Every renderer that takes part in the sky-derived lighting: the ball LODs plus the scene objects.
        /// </summary>
        private IEnumerable<InstancedModelRenderer> SkyLitRenderers()
        {
            foreach (InstancedModelRenderer ballRenderer in _ballRenderers) yield return ballRenderer;

            yield return _groundRenderer;
            yield return _ceilingRenderer;
            yield return _cannonRenderer;
            if (_cityRenderer != null) yield return _cityRenderer;
            if (_arenaGlassRenderer != null) yield return _arenaGlassRenderer;
            if (_arenaFrameRenderer != null) yield return _arenaFrameRenderer;
        }

        /// <summary>
        /// Derives the scene lighting from the current sky dome (issue #39): every object receives hemisphere
        /// ambient (zenith color from above, horizon color from below) and the tinted three-light rig,
        /// so every sky dome gives the whole scene its own mood.
        /// </summary>
        private void ApplySkyLighting()
        {
            Vector3 zenith = _sky.ZenithColor;
            Vector3 horizon = _sky.HorizonColor;

#if DEBUG
            Console.WriteLine($"[sky] Dome {_skyModelNumber}: zenith {zenith}, horizon {horizon}");
#endif

            //The palette is read off the dome's vertex colors, so it arrives sRGB-encoded. Everything below
            //this line scales, tints and lerps it, and none of that means anything until it is radiance:
            //scaling an sRGB value by 1.3 does not make 1.3 times the light. Doing it in display space is
            //what had the ambient running some 38% brighter than the rig asked for.
            _zenithLinear = ColorSpace.SrgbToLinear(zenith);
            _horizonLinear = ColorSpace.SrgbToLinear(horizon);

            ApplyCloudPalette();
            ApplyLightRig();
        }

        /// <summary>
        /// How far the key/fill lights are carried towards the horizon color, and the back light towards
        /// the zenith. The clouds borrow the same figure, being lit by the same rig.
        /// </summary>
        private static readonly float SKY_TINT_STRENGTH = 0.5f;

        /// <summary>
        /// The shadowed side of a cloud sees no sun at all — only sky — so it takes the zenith color far
        /// more completely than any surface the rig lights from two sides.
        /// </summary>
        private static readonly float CLOUD_SHADOW_TINT_STRENGTH = 0.8f;

        /// <summary>
        /// Colors the clouds with the dome they hang in.
        /// <para>
        /// Every dome was getting the same cold white cloud, and over eighteen skies running from turquoise
        /// day to blood-red dusk to near-black night it read as a grey smear pasted over the sky rather
        /// than as weather in it. A cloud has no color of its own: its lit side is the color of the sun and
        /// its underside is the color of the sky. So the lit side takes the same tint the scene's key light
        /// takes, which means the clouds are lit by literally the same light as everything under them, and
        /// the underside takes the zenith.
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
        /// Fully overcast ambient, in linear radiance: grey, and no dimmer than the clear sky it replaces.
        /// A cloud deck is a big lit diffuse source, so losing the sun does not darken what arrives from
        /// above so much as spread it out and take the colour out of it.
        /// </summary>
        private static readonly Vector3 OVERCAST_SKY = new(0.62f, 0.64f, 0.68f);

        private static readonly Vector3 OVERCAST_GROUND = new(0.34f, 0.35f, 0.37f);

        /// <summary>
        /// Hands the cached sky palette to every renderer, shaded by how much cloud is overhead.
        /// <para>
        /// Only the **ambient** is touched here. The key light is already dimmed per pixel by the cloud
        /// shadow in the shader, and dimming it again on this side would count the same cloud twice — and
        /// worse, would darken the scene uniformly, which is precisely the look this is meant to avoid.
        /// The two halves together are what makes overcast read as *flat*: the sun goes, the sky stays.
        /// </para>
        /// </summary>
        private void ApplyLightRig()
        {
            //The key/fill lights (the "sun" side) take on the horizon color, the back light the zenith color,
            //so the whole light rig follows the mood of the sky instead of just the ambient term
            Vector3 keyTint = Vector3.Lerp(Vector3.One, _horizonLinear, SKY_TINT_STRENGTH);
            Vector3 backTint = Vector3.Lerp(Vector3.One, _zenithLinear, SKY_TINT_STRENGTH);

            Vector3 skyColor = Vector3.Lerp(_zenithLinear * 1.3f, OVERCAST_SKY, _overcast);

            //Bounce light from below is dimmer than the sky above
            Vector3 groundColor = Vector3.Lerp(_horizonLinear * 0.75f, OVERCAST_GROUND, _overcast);

            foreach (InstancedModelRenderer renderer in SkyLitRenderers())
            {
                renderer.LinearLightRig = true;
                renderer.SkyColor = skyColor;
                renderer.GroundColor = groundColor;

                //The "sun": close enough for its direction to visibly differ from object to object
                renderer.KeyLightPosition = -DefaultLighting.Light0Direction * 40f;

                renderer.SetLightTint(keyTint, backTint);
            }
        }

        /// <summary>
        /// Follows the cloud straight over the arena and hands it to the light rig.
        /// <para>
        /// Overhead, not along the sun ray, and the difference matters: the sun ray is what decides whether
        /// you are standing in a shadow, which the shader already answers per pixel, while what is over
        /// your head is what decides how much of the sky is still blue air. Sun behind a cloud with the
        /// rest of the sky open should take the shadows away and leave the ambient where it was, and
        /// reading both off one number would not let it.
        /// </para>
        /// </summary>
        private void UpdateOvercast(float elapsedSeconds)
        {
            _clouds.Time = _pulseSeconds;

            //Straight up from the middle of the arena, so the sample is simply where the arena stands —
            //averaged over a patch about as wide as a cloud, since what lights the scene from above is how
            //much of the sky is covered, not what happens to sit over one point of it
            float overhead = _clouds.CoverAround(Vector2.Zero, OVERCAST_SAMPLE_RADIUS);

            //Exponential, framed in seconds rather than as a per-frame constant, so the response does not
            //change with the frame rate
            _overcast = Microsoft.Xna.Framework.MathHelper.Lerp(_overcast, overhead, 1f - MathF.Exp(-elapsedSeconds / OVERCAST_RESPONSE_SECONDS));

            ApplyLightRig();
        }

        private void ComputeAimerPosition()
        {
            _aimerPos = new Vector2(GraphicsDevice.Viewport.Width / 2f - _aimer.Width / 2f, GraphicsDevice.Viewport.Height / 2f - _aimer.Height / 2f);
        }

        /// <summary>
        /// Everything about the clouds that does not change frame to frame. The per-frame half — the
        /// clock and the camera — is set in <see cref="Draw"/>, right before the dome goes out.
        /// </summary>
        private void SetCloudParameters()
        {
            _skyEffect.Parameters["CloudDetailStrength"].SetValue(CLOUD_DETAIL_STRENGTH);

            //The two cloud colors are not set here: they follow the dome, and ApplySkyLighting sets them
            //every time it changes
            _skyEffect.Parameters["CloudOpacity"].SetValue(CLOUD_OPACITY);
            _skyEffect.Parameters["CloudHorizonFade"].SetValue(CLOUD_HORIZON_FADE);
            _skyEffect.Parameters["CloudSunStep"].SetValue(CLOUD_SUN_STEP);
            _skyEffect.Parameters["CloudSelfAbsorption"].SetValue(CLOUD_SELF_ABSORPTION);
            _skyEffect.Parameters["CloudSunAbsorption"].SetValue(CLOUD_SUN_ABSORPTION);
            _skyEffect.Parameters["CloudSilverStrength"].SetValue(CLOUD_SILVER_STRENGTH);
            _skyEffect.Parameters["CloudSilverPower"].SetValue(CLOUD_SILVER_POWER);

            //The rig's key light stands in for the sun, so the clouds are lit by whatever it is lit by,
            //and the scene is shadowed along the very same direction
            _skyEffect.Parameters["SunDirection"].SetValue(SUN_DIRECTION);
            _instancingEffect.Parameters["SunDirection"].SetValue(SUN_DIRECTION);
        }

        /// <summary>
        /// Towards the sun. Taken as a direction rather than from <c>KeyLightPosition</c>, which is a point
        /// forty units off the middle of the arena: near enough that its direction fans right across the
        /// scene, and cloud shadows have to arrive in parallel bands over a city hundreds of units wide.
        /// </summary>
        private static readonly Vector3 SUN_DIRECTION = -DefaultLighting.Light0Direction;

        private void SwitchSkyDome()
        {
            if (_skyModelNumber == 18) _skyModelNumber = default;

            _skyModelNumber++;
            _skyModel = Content.Load<Model>("Skyes/SkyDome" + _skyModelNumber);
            _sky.SkyDomeModel = _skyModel;

            ApplySkyLighting();
        }

        /// <summary>
        /// Builds the intended setting: a glass play surface framed in marble, standing among the tops of
        /// a procedural city. One unit box mesh serves all three — the buildings, the four marble bands
        /// and the glass panel are the same cube under different instance matrices, which is what keeps
        /// a whole city to a single draw call.
        /// </summary>
        private void BuildCity()
        {
            _unitBox = new BoxMesh(GraphicsDevice, 1f, 1f, 1f);

            _city = new City(seed: 20260720, arenaHalfExtent: ARENA_HALF_EXTENT);

            Console.WriteLine($"[city] {_city.Buildings.Length} buildings, arena half extent {ARENA_HALF_EXTENT}, floor at {ARENA_Y}, recess at {ARENA_PIT_Y}");

            //The floor is the same marble the ground blocks were modeled with; the model carries the
            //texture, so it is taken off the model rather than added to the content project again
            Texture2D marble = (_groundModel.Meshes[0].Effects[0] as BasicEffect)?.Texture;

            _cityRenderer = new InstancedModelRenderer(GraphicsDevice, _unitBox, Vector3.One, _instancingEffect)
            {
                CityWindowBrightness = CITY_WINDOW_BRIGHTNESS,
                //The specular ambient is not multiplied by albedo - a reflection does not care how dark
                //the surface under it is - so on a city's worth of facades seen at grazing angles it
                //washes the whole skyline in sky color. Concrete is rough and barely reflective; this is
                //the one place the physically-right term needs turning down to look right.
                //
                //Turned down again once the city rose above the floor: from up there almost every facade
                //is seen at a grazing angle, where Fresnel is near 1, and a quarter of the sky over a
                //thousand towers bleached the skyline into a white cliff with the windows lost in it.
                SpecularAmbientStrength = 0.07f
            };

            //The glass panel, sitting just under the play surface
            _arenaGlassRenderer = new InstancedModelRenderer(GraphicsDevice, _unitBox, ARENA_GLASS_COLOR, _instancingEffect, ARENA_GLASS_ALPHA);

            //The floor is a mosaic, not a sheet: some panels are solid marble, some are glass. That
            //contrast is the whole point of standing here — the stone says the floor holds, the glass
            //opens onto the drop, and the eye keeps being handed one and then the other. A single glass
            //sheet gives depth with nothing to measure it against, and a single stone one gives no depth.
            List<ModelInstance> glassPanels = new();
            List<ModelInstance> stonePanels = new();

            int panelsPerSide = (int)MathF.Round(ARENA_HALF_EXTENT * 2f / ARENA_PANEL_SIZE);
            Random panelRandom = new(7311);

            for (int px = 0; px < panelsPerSide; px++)
                for (int pz = 0; pz < panelsPerSide; pz++)
                {
                    float x = PanelCenter(px);
                    float z = PanelCenter(pz);

                    //Glass towards the middle, stone towards the rim: the drop opens under the play area,
                    //where the balls are, and the edge you stand on stays solid. Squared, so the stone is
                    //confined to the outermost ring or two and the openings in the middle run together
                    //into one sheet of glass instead of a checkerboard.
                    float distance = MathF.Max(MathF.Abs(x), MathF.Abs(z)) / ARENA_HALF_EXTENT;
                    bool glass = panelRandom.NextDouble() > distance * distance * 0.9;

                    (glass ? glassPanels : stonePanels).Add(
                        Slab(ARENA_PANEL_SIZE - ARENA_MULLION_WIDTH, ARENA_PANEL_SIZE - ARENA_MULLION_WIDTH, x, z, TopAt(x, z)));
                }

            _arenaGlassInstances = glassPanels.ToArray();

            //Four marble bands framing the glass. The glass is what the city shows through; the marble is
            //what it is set into, and it is also the only thing here with a surface worth the relief work.
            _arenaFrameRenderer = new InstancedModelRenderer(GraphicsDevice, _unitBox, ARENA_MARBLE_COLOR, _instancingEffect)
            {
                SurfaceReliefFrequency = 9f,
                SurfaceReliefStrength = 0.008f,
                SlabSize = 2f,
                SlabJointWidth = 0.025f,
                SlabJointDepth = 0.04f,
                CavityStrength = 0.7f,
                ReliefShadowStrength = 0.85f,
                ParallaxScale = 1f,

                //All of the above was dead: with no detail texture the renderer falls through to the plain
                //technique, which has no slab joints and no relief at all. The marble the ground blocks are
                //modeled with, projected triplanar so it tiles across every band and panel alike — these
                //are boxes with no UVs worth the name, and the floor is one continuous surface anyway.
                DetailTexture = marble,
                DetailTextureMapping = DetailMapping.Triplanar,
                DetailScale = 1f / GROUND_BLOCK_SIZE,

                //A floor is seen at a grazing angle almost everywhere except right under your feet, which
                //is exactly where Fresnel puts the sky reflection at full strength. Left at 1 the marble
                //mirrors the sky into a white sheet from the middle distance out, and the mosaic it is
                //there to carry disappears into it.
                SpecularAmbientStrength = 0.4f
            };

            float outer = ARENA_HALF_EXTENT + ARENA_FRAME_WIDTH;
            float bandCenter = ARENA_HALF_EXTENT + ARENA_FRAME_WIDTH * 0.5f;

            List<ModelInstance> frame = new()
            {
                Slab(outer * 2f, ARENA_FRAME_WIDTH, 0f, bandCenter, ARENA_Y),
                Slab(outer * 2f, ARENA_FRAME_WIDTH, 0f, -bandCenter, ARENA_Y),
                Slab(ARENA_FRAME_WIDTH, ARENA_HALF_EXTENT * 2f, bandCenter, 0f, ARENA_Y),
                Slab(ARENA_FRAME_WIDTH, ARENA_HALF_EXTENT * 2f, -bandCenter, 0f, ARENA_Y)
            };

            //Mullions dividing the glass into panels. Without them a translucent sheet over a lit city
            //reads as no floor at all - you see the city and nothing else, and the eye has no reason to
            //believe anything is between. A grid of things the glass is set into is what says "glass".
            //
            //Laid one panel at a time rather than as four long bands, because the floor is not flat: where
            //a mullion runs along the lip of the recess it belongs to the rim above, not to the floor
            //below, and a single band spanning the whole width would cut through the air over the bath.
            for (int line = 1; line < panelsPerSide; line++)
            {
                float offset = -ARENA_HALF_EXTENT + line * ARENA_PANEL_SIZE;

                for (int cell = 0; cell < panelsPerSide; cell++)
                {
                    float along = PanelCenter(cell);
                    float step = ARENA_PANEL_SIZE * Constants.HALF;

                    //A lip sits level with the higher of the two floors it separates
                    frame.Add(Slab(ARENA_MULLION_WIDTH, ARENA_PANEL_SIZE, offset, along,
                        MathF.Max(TopAt(offset - step, along), TopAt(offset + step, along))));

                    //Shortened by one mullion width so the two directions abut at the crossings instead of
                    //overlapping there: two coplanar tops in the same place is a z-fighting square
                    frame.Add(Slab(ARENA_PANEL_SIZE - ARENA_MULLION_WIDTH, ARENA_MULLION_WIDTH, along, offset,
                        MathF.Max(TopAt(along, offset - step), TopAt(along, offset + step))));
                }
            }

            //The solid panels join the frame: same marble, same slab relief, one draw call for all of it
            frame.AddRange(stonePanels);

            _arenaFrameInstances = frame.ToArray();

            static float PanelCenter(int index) => -ARENA_HALF_EXTENT + (index + 0.5f) * ARENA_PANEL_SIZE;

            //Inside the recessed block the floor drops by exactly what the physics block drops by
            static float TopAt(float x, float z) =>
                MathF.Abs(x) < ARENA_PIT_HALF_EXTENT && MathF.Abs(z) < ARENA_PIT_HALF_EXTENT ? ARENA_PIT_Y : ARENA_Y;

            static ModelInstance Slab(float sizeX, float sizeZ, float x, float z, float top) => new(
                Microsoft.Xna.Framework.Matrix.CreateScale(sizeX, ARENA_FLOOR_THICKNESS, sizeZ)
                * Microsoft.Xna.Framework.Matrix.CreateTranslation(x, top - ARENA_FLOOR_THICKNESS * Constants.HALF, z),
                GROUND_NO_OCCLUSION);
        }

        private void BuildGroundAndCeiling()
        {
            Box groundBox = new(GROUND_BLOCK_SIZE, 1f, GROUND_BLOCK_SIZE);

            for (int x = -GROUND_BLOCK_RADIUS; x <= GROUND_BLOCK_RADIUS; x++)
                for (int z = -GROUND_BLOCK_RADIUS; z <= GROUND_BLOCK_RADIUS; z++)
                {
                    //The center block is recessed by one unit, forming the arena the balls drop into
                    float y = x == 0 && z == 0 ? GROUND_PIT_Y : GROUND_PLATEAU_Y;

                    _staticBodies.Add(new(_groundModel, CreateStatic(new(x * GROUND_BLOCK_SIZE, y, z * GROUND_BLOCK_SIZE), groundBox)));
                }

            //All the blocks share one model, so they go out as a single instanced draw call
            _groundInstances = new ModelInstance[_staticBodies.Count];
            for (int i = 0; i < _staticBodies.Count; i++) _groundInstances[i] = new ModelInstance(_staticBodies[i].World, GROUND_NO_OCCLUSION);

            Box box = new(DEFAULT_CEILING_SIZE, 1f, DEFAULT_CEILING_SIZE);
            TypedIndex boxShapeIndex = _simulation.Shapes.Add(box);
            _ceilingShapeIndex = boxShapeIndex;
            CollidableDescription collidableDescription = new(boxShapeIndex, 0.1f);
            BodyDescription bodyDescription = BodyDescription.CreateKinematic(new System.Numerics.Vector3(0f, GetCeilingY(10), 0f), collidableDescription, new BodyActivityDescription(Constants.HUNDREDTH));

            BodyHandle topBodyHandle = _simulation.Bodies.Add(in bodyDescription);
            BodyReference topBodyReference = new(topBodyHandle, _simulation.Bodies);

            _ceiling = new KinematicBody(null, topBodyReference, topBodyHandle);
        }

        /// <summary>
        /// (Re)builds the procedural glass box of the ceiling at the given size. Called at startup
        /// and whenever a loaded map resizes the ceiling; the caller must re-apply the sky lighting.
        /// </summary>
        private void RecreateCeilingRenderer(float sizeX, float sizeZ)
        {
            _ceilingMesh?.Dispose();
            _ceilingRenderer?.Dispose();

            _ceilingMesh = new BoxMesh(GraphicsDevice, sizeX, 1f, sizeZ);
            _ceilingRenderer = new InstancedModelRenderer(GraphicsDevice, _ceilingMesh, CEILING_GLASS_COLOR, _instancingEffect, CEILING_GLASS_ALPHA);
        }

        /// <summary>
        /// The ceiling hovers this far above the center of the top-level balls (their Y is (levels - 1)/√2).
        /// </summary>
        private static float GetCeilingY(byte levels) => (levels - 1) / Constants.SQRT_TWO + 2f;

        /// <summary>
        /// Moves and resizes the ceiling so it covers the play field of the given map and sits just above its top level.
        /// The kinematic body is kept (constraints of a previously loaded structure may still reference it);
        /// only its pose, collision shape and drawn model scale change.
        /// </summary>
        private void FitCeilingToMap(BallsMap map)
        {
            float sizeX = map.StageSizeX + 1f; //Odd levels are shifted by +0.5 and balls have radius 0.5, so add a margin
            float sizeZ = map.StageSizeZ + 1f;

            BodyReference ceilingReference = _ceiling.BodyReference;
            ceilingReference.Pose.Position = new System.Numerics.Vector3(0f, GetCeilingY(map.Levels), 0f);

            TypedIndex newShapeIndex = _simulation.Shapes.Add(new Box(sizeX, 1f, sizeZ));
            ceilingReference.SetShape(newShapeIndex);
            _simulation.Shapes.Remove(_ceilingShapeIndex);
            _ceilingShapeIndex = newShapeIndex;

            //Recreate the wrapper so its world matrix matches the new pose (the body and handle stay the same);
            //the drawn glass box is regenerated at the exact new size instead of scaling a fixed mesh
            _ceiling = new KinematicBody(null, ceilingReference, _ceiling.BodyHandle);
            RecreateCeilingRenderer(sizeX, sizeZ);
        }

        private void LoadBallsMap()
        {
            var filePath = string.Empty;

            using (OpenFileDialog openFileDialog = new())
            {
                openFileDialog.InitialDirectory = Directory.GetCurrentDirectory();
                openFileDialog.Filter = Constants.MAPS_FILE_FILTER;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = openFileDialog.FileName;
                }
            }

            if (string.IsNullOrEmpty(filePath)) return;

            DeserializeMapFromFile(filePath);
        }

        private void DeserializeMapFromFile(string filePath)
        {
            RemoveCurrentBallsStructure();

            _map = new(filePath, _hrSphere);
            _map.Center();
            _eventHandler.Map = _map;

            FitCeilingToMap(_map);

            _physicsBalls = BallsConstraintsBuilder.BuildBallsStructure(_map.GetStaticBallsArray(), ref _simulation, _ceiling.BodyReference);
            _eventHandler.PhysicsBalls = _physicsBalls;

            RecountBallsAndConstraints();

            ApplySkyLighting(); //FitCeilingToMap recreated the ceiling renderer, which starts without the sky palette
        }

        /// <summary>
        /// Removes the previously loaded ball structure and all shot/falling balls from the simulation,
        /// so a newly loaded map starts with a clean slate. Without this the old bodies would keep hanging
        /// in the simulation invisibly (only <see cref="_physicsBalls"/> is drawn), blocking shots from
        /// attaching and catching balls released by the End action in mid-air.
        /// </summary>
        private void RemoveCurrentBallsStructure()
        {
            if (_physicsBalls != null)
            {
                //First pass: remove all constraints. A constraint whose owning ball had no free handle slot is tracked
                //only by the other ball of the pair, so bodies can only be removed once no constraints are left at all.
                RemoveAllConstraints();

                XZLevel size = XZLevel.FromArray(_physicsBalls);

                for (byte level = 0; level < size.Level; level++)
                    for (byte x = 0; x < size.X; x++)
                        for (byte z = 0; z < size.Z; z++)
                        {
                            PhysicsBall ball = _physicsBalls[x, z, level];
                            if (ball == null) continue;

                            _simulation.Bodies.Remove(ball.BallReference.Handle);
                            _physicsBalls[x, z, level] = null;
                        }
            }

            RemoveDynamicBalls(_shotBalls, unregisterListeners: true);
            RemoveDynamicBalls(_fallingBalls, unregisterListeners: false);
        }

        /// <summary>
        /// Removes the bodies of the given balls from the simulation and clears the list
        /// (the list instance is shared with <see cref="EventHandler"/>, so it must be cleared, not replaced).
        /// </summary>
        private void RemoveDynamicBalls(List<PhysicsBall> balls, bool unregisterListeners)
        {
            for (int i = 0; i < balls.Count; i++)
            {
                BodyReference body = balls[i].BallReference;

                if (unregisterListeners && _events.IsListener(body.CollidableReference)) _events.Unregister(body.CollidableReference);

                _simulation.Bodies.Remove(body.Handle);
            }

            balls.Clear();
        }

        private void RecountBallsAndConstraints()
        {
            _info.CustomText = "Balls on scene: " + (_simulation.Bodies.ActiveSet.Count) + "\nConstraints count: " + _simulation.Solver.CountConstraints();
        }

        /// <summary>
        /// Y below which a ball is considered fallen out of the world (the ground sits around Y = -10).
        /// </summary>
        private static readonly float KILL_PLANE_Y = -30f;

        /// <summary>
        /// Removes balls that can no longer affect gameplay from the simulation and from the given list:
        /// balls that fell below <see cref="KILL_PLANE_Y"/> and balls that came to rest on the ground
        /// (their body fell asleep - flying or rolling bodies never sleep).
        /// </summary>
        /// <param name="unregisterListeners">Pass true for shot balls, which may still be registered as contact listeners.</param>
        /// <returns>Number of removed balls.</returns>
        private int RemoveFallenBalls(List<PhysicsBall> balls, bool unregisterListeners)
        {
            int removed = 0;

            for (int i = balls.Count - 1; i >= 0; i--)
            {
                BodyReference body = balls[i].BallReference;

                if (body.Pose.Position.Y >= KILL_PLANE_Y && body.Awake) continue;

                if (unregisterListeners && _events.IsListener(body.CollidableReference)) _events.Unregister(body.CollidableReference);

                _simulation.Bodies.Remove(body.Handle);
                balls.RemoveAt(i);
                removed++;

#if DEBUG
                Console.WriteLine("Removed a fallen ball from the simulation");
#endif
            }

            return removed;
        }

        protected override void Update(GameTime gameTime)
        {
            float timeStep = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1 / 60f);
            if (timeStep == 0) timeStep = 1 / 60f;

            //Wall-clock time, not simulation time: the balls keep their pulse when the simulation is
            //paused or slowed (F5, F9), because it is what they are, not something they are doing
            _pulseSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;
            foreach (InstancedModelRenderer ballRenderer in _ballRenderers) ballRenderer.PulseTime = _pulseSeconds;

            //The city runs off the same wall clock, and for the same reason: its windows are lit by people
            //who do not care whether the simulation is running
            _cityRenderer.CityWindowTime = _pulseSeconds;

            UpdateOvercast((float)gameTime.ElapsedGameTime.TotalSeconds);

            if (_simulate)
            {
                if (_slowSimulation) _simulation.Timestep(timeStep * Constants.HUNDREDTH, _threadDispatcher);
                else _simulation.Timestep(timeStep, _threadDispatcher);

                #region Contact events

                //Flush must run right after the timestep and before ProcessQueuedContacts:
                //unregistering a listener is only safe once the pending worker adds collected during the timestep have been applied.
                _events.Flush();
                if (_eventHandler.ProcessQueuedContacts() > 0) RecountBallsAndConstraints();

                #endregion

                #region Fallen balls cleanup

                int removedBalls = RemoveFallenBalls(_shotBalls, unregisterListeners: true) + RemoveFallenBalls(_fallingBalls, unregisterListeners: false);
                if (removedBalls > 0) RecountBallsAndConstraints();

                #endregion

                #region Auto shooting (testing)

                if (_autoShoot && _map != null)
                {
                    _autoShootElapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;

                    if (_autoShootElapsed >= 1f)
                    {
                        _autoShootElapsed = 0f;
                        ShootBall(new Vector3(RANDOM.Next(-4, 5), RANDOM.Next(4, 11), RANDOM.Next(-4, 5)));
                        Console.WriteLine($"[autoshoot] FPS: {_info.CurrentFPS}, balls drawn: {_drawnBalls}/{_collectedBalls}, LOD: {string.Join("/", _ballLodTotals)}");
                    }
                }

                if (_switchMapPath != null && !_switchMapDone)
                {
                    _switchMapElapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;

                    if (_switchMapElapsed >= SWITCH_MAP_DELAY_SECONDS)
                    {
                        _switchMapDone = true;
                        Console.WriteLine($"[switchmap] Loading {_switchMapPath}");
                        DeserializeMapFromFile(_switchMapPath);
                    }
                }

                #endregion
            }

            if (IsActive)
            {
                _cih.RegisterCurrentInputState();

                foreach (var action in _actions) if (_cih.PressedOnce(action.Key, action.Button)) action.Method();

                _cih.Update(gameTime);
                _cih.CameraMovement(gameTime, !_gameMode);
                _cih.RegisterPreviousInputState();
            }
            else IsMouseVisible = true;

            _cih.MouseMovementDenominator = timeStep / Constants.THOUSANDTH;

            UpdateCannon(gameTime);

            #region Game mode animation

            if (_gameModeAnimStarted && _gameMode)
            {
                _camera.Position = Vector3.SmoothStep(_beforeAnimationPosition, GetCanonOffsettedPos(), _gameModeAnimStep);
                _camera.Target = Vector3.SmoothStep(_beforeAnimationTarget, GetCannonOffsettedTarget(), _gameModeAnimStep * 2f);
                _camera.FieldOfView = Microsoft.Xna.Framework.MathHelper.SmoothStep(FREE_FOV, GAME_FOV, _gameModeAnimStep);

                _gameModeAnimStep += ANIMATION_SPEED * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

                if (_gameModeAnimStep > Constants.ONE)
                {
                    _gameModeAnimStep = 0;
                    _gameModeAnimStarted = false;
                }
            }

            if (_freeModeAnimStarted && !_gameMode)
            {
                _camera.FieldOfView = Microsoft.Xna.Framework.MathHelper.SmoothStep(GAME_FOV, FREE_FOV, _gameModeAnimStep);

                _gameModeAnimStep += ANIMATION_SPEED * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

                if (_gameModeAnimStep > 1f)
                {
                    _gameModeAnimStep = 0;
                    _freeModeAnimStarted = false;
                }
            }

            #endregion

            base.Update(gameTime);
        }

        private void RemoveAllStatics()
        {
            foreach (var staticBody in _staticBodies) _simulation.Statics.Remove(staticBody.StaticReference.Handle);
            _staticBodies.Clear();
        }

        /// <summary>
        /// Debug action (End): releases the whole hanging structure at once. The balls move into
        /// <see cref="_fallingBalls"/>, so <see cref="RemoveFallenBalls"/> culls them once they come
        /// to rest — leaving them in <see cref="_physicsBalls"/> kept the pile on the ground alive
        /// (and generating contact constraints) forever.
        /// </summary>
        private void ReleaseAllBalls()
        {
            if (_physicsBalls == null || _map == null) return;

            if (BallsConstraintsBuilder.ReleaseAllBalls(_physicsBalls, _map, _simulation, _fallingBalls) > 0)
                RecountBallsAndConstraints();
        }

        private void RemoveAllConstraints()
        {
            if (_physicsBalls == null || _physicsBalls.Rank != 3) return;

            XZLevel size = XZLevel.FromArray(_physicsBalls);

            for (byte level = 0; level < size.Level; level++)
                for (byte x = 0; x < size.X; x++)
                    for (byte z = 0; z < size.Z; z++)
                        _physicsBalls[x, z, level]?.RemoveAllConstraints(_simulation);
        }

        protected override void Draw(GameTime gameTime)
        {
            if (_draw)
            {
                CollectBallInstances((float)gameTime.ElapsedGameTime.TotalSeconds);
                DrawBallsShadowMap(); //Must run before anything touches the backbuffer
            }

            //The scene goes through the HDR target; the aimer and the text overlay are drawn after the
            //resolve, at native resolution and in display space, so they stay exactly as authored instead
            //of being softened by the downsample and bent by the tonemap curve
            GraphicsDevice.SetRenderTarget(_sceneTarget);

            //The old clear color in linear light. Nothing should ever see it — the sky dome covers the
            //whole frame — but a clear color that is silently a different brightness than it reads is a
            //trap worth not leaving behind.
            GraphicsDevice.Clear(CLEAR_COLOR_LINEAR);

            //The clouds run off the same wall clock the balls pulse to, so the weather keeps moving while
            //the simulation is paused or slowed. Handed to both shaders from the one field, which is what
            //keeps the cloud you look at and the shadow it throws the same cloud.
            _clouds.Time = _pulseSeconds;
            _clouds.ApplyTo(_skyEffect);
            _clouds.ApplyTo(_instancingEffect);

            _skyEffect.Parameters["CameraPosition"].SetValue(_camera.Position);

            _sky.Draw(_camera);

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            //Stated rather than inherited. The last thing to touch the rasterizer in a frame is the
            //SpriteBatch drawing the overlay, which leaves its own state behind, and the tonemap pass
            //before it leaves CullNone - so what the scene culled depended on which of them ran last.
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            if (_draw)
            {
                //The city is the ground and the backdrop both. The old marble blocks still exist as
                //physics bodies under the arena, but nothing draws them any more.
                _cityRenderer.Draw(_camera, _city.Buildings, _city.Buildings.Length, _sceneEffectParams);
                _arenaFrameRenderer.Draw(_camera, _arenaFrameInstances, _arenaFrameInstances.Length, _sceneEffectParams);

                _cannonRenderer.Draw(_camera, _cannon.World, _sceneEffectParams);

                DrawBallsInstanced();

                //Translucent glass: drawn after the opaque scene so the city below shows through it
                _arenaGlassRenderer.Draw(_camera, _arenaGlassInstances, _arenaGlassInstances.Length, _sceneEffectParams);

                _ceilingRenderer.Draw(_camera, _ceiling.World, _sceneEffectParams);
            }

            ResolveSceneTarget();

            if (!_gameMode)
            {
                _spriteBatch.Begin();
                _spriteBatch.Draw(_aimer, _aimerPos, _aimerColor);
                _spriteBatch.End();
            }

            base.Draw(gameTime);
        }

        /// <summary>
        /// Gathers the instance data of all balls (map structure, shot and falling ones) into the
        /// per-type-and-LOD buckets used by both the shadow map pass and the visible pass.
        /// No camera frustum culling: balls outside the view must still cast shadows into it
        /// (and measurements showed the culling saved nothing on this scene anyway).
        /// </summary>
        private void CollectBallInstances(float elapsedSeconds)
        {
            for (int i = 0; i < _ballInstanceCounts.Length; i++) _ballInstanceCounts[i] = 0;
            _blobInstanceCount = 0;
            for (int i = 0; i < BALL_LOD_COUNT; i++) _ballLodTotals[i] = 0;
            _collectedBalls = 0;

            float ease = 1f - MathF.Exp(-elapsedSeconds / BALL_OCCLUSION_EASE_SECONDS);
            float glide = 1f - MathF.Exp(-elapsedSeconds / BALL_ATTACH_GLIDE_SECONDS);

            if (_physicsBalls != null)
            {
                XZLevel size = XZLevel.FromArray(_physicsBalls);

                for (byte level = 0; level < size.Level; level++)
                    for (byte x = 0; x < size.X; x++)
                        for (byte z = 0; z < size.Z; z++)
                        {
                            PhysicsBall ball = _physicsBalls[x, z, level];
                            if (ball == null) continue;

                            //The ceiling plate deliberately does NOT occlude: it is translucent glass, so it
                            //lets the light through (what keeps a released ball from flashing brighter is the
                            //occlusion easing below, not this)
                            int occluders = BallsConstraintsBuilder.CountOccupiedNeighbors(_physicsBalls, ball.ArrayPosition, size, out System.Numerics.Vector3 occlusionSum);

                            System.Numerics.Vector4 occlusionTarget = new(
                                occlusionSum.X / MAX_BALL_OCCLUDERS,
                                occlusionSum.Y / MAX_BALL_OCCLUDERS,
                                occlusionSum.Z / MAX_BALL_OCCLUDERS,
                                1f - BALL_OCCLUSION_STRENGTH * occluders / MAX_BALL_OCCLUDERS);

                            CollectBallInstance(ball, EaseOcclusion(ball, occlusionTarget, ease), glide);
                        }
            }

            //A free-flying ball has nothing packed around it (a shot one never had, so the easing keeps it there)
            for (int i = 0; i < _shotBalls.Count; i++) CollectBallInstance(_shotBalls[i], EaseOcclusion(_shotBalls[i], PhysicsBall.UNOCCLUDED, ease), glide);
            for (int i = 0; i < _fallingBalls.Count; i++) CollectBallInstance(_fallingBalls[i], EaseOcclusion(_fallingBalls[i], PhysicsBall.UNOCCLUDED, ease), glide);
        }

        /// <summary>
        /// Eases a ball's occlusion towards <paramref name="target"/> and returns it in render form.
        /// A ball rendered for the first time takes the target as it is - only changes happening in front
        /// of the player are worth easing, a newly built structure has to be shaded right away.
        /// </summary>
        private static Vector4 EaseOcclusion(PhysicsBall ball, System.Numerics.Vector4 target, float ease)
        {
            ball.Occlusion = ball.OcclusionInitialized ? System.Numerics.Vector4.Lerp(ball.Occlusion, target, ease) : target;
            ball.OcclusionInitialized = true;

            return ToXna(ball.Occlusion);
        }

        private static Vector4 ToXna(System.Numerics.Vector4 vector) => new(vector.X, vector.Y, vector.Z, vector.W);

        /// <summary>
        /// Renders all collected ball instances into the shadow map from the key light's point of view.
        /// </summary>
        private void DrawBallsShadowMap()
        {
            GraphicsDevice.SetRenderTarget(_shadowMap);
            GraphicsDevice.Clear(Color.White); //1 in the red channel = the far plane

            for (int lod = 0; lod < BALL_LOD_COUNT; lod++)
                for (int typeIndex = 0; typeIndex < BALL_TYPE_COUNT; typeIndex++)
                {
                    int bucketIndex = typeIndex * BALL_LOD_COUNT + lod;
                    _ballRenderers[lod].DrawDepth(_lightViewProjection, _ballInstances[bucketIndex], _ballInstanceCounts[bucketIndex]);
                }

            GraphicsDevice.SetRenderTarget(null);
        }

        /// <summary>
        /// Draws all collected ball instances with GPU instancing: one draw call per ball type and LOD level.
        /// </summary>
        private void DrawBallsInstanced()
        {
            _drawnBalls = 0;
            for (int typeIndex = 0; typeIndex < BALL_TYPE_COUNT; typeIndex++)
                for (int lod = 0; lod < BALL_LOD_COUNT; lod++)
                {
                    int bucketIndex = typeIndex * BALL_LOD_COUNT + lod;
                    int count = _ballInstanceCounts[bucketIndex];

                    _drawnBalls += count;
                    _ballLodTotals[lod] += count;

                    _ballRenderers[lod].Draw(_camera, _ballInstances[bucketIndex], count,
                        BasicEffectParamsProvider.GetEffectByType((BallType)(typeIndex + 1)),
                        BasicEffectParamsProvider.GetDiffuseTintByType((BallType)(typeIndex + 1)));
                }
        }

        /// <summary>
        /// Darkens the ground where the balls block the key light: a translucent quad on the ground plane
        /// sampling the shadow map, so the ground keeps its own material and lighting.
        /// </summary>
        private void DrawShadowOverlay()
        {
            _shadowOverlayEffect.Parameters["ViewProjection"].SetValue(_camera.View * _camera.Projection);
            _shadowOverlayEffect.Parameters["ShadowMap"].SetValue(_shadowMap);

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead; //Test against the scene, but do not write
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;

            _shadowOverlayEffect.CurrentTechnique = _shadowOverlayEffect.Techniques["ShadowOverlay"];
            _shadowOverlayEffect.CurrentTechnique.Passes[0].Apply();
            GraphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _shadowOverlayVertices, 0, 4, SHADOW_OVERLAY_INDICES, 0, 2);

            DrawContactBlobs();

            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Instanced radial darkening of the ground under every ball close to it (contact ambient occlusion).
        /// Overlapping blobs accumulate, so groups of resting balls pool into one dark contact area.
        /// Expects the blend, depth and rasterizer states set by <see cref="DrawShadowOverlay"/>.
        /// </summary>
        private void DrawContactBlobs()
        {
            if (_blobInstanceCount == 0) return;

            if (_blobInstanceBuffer == null || _blobInstanceBuffer.VertexCount < _blobInstances.Length)
            {
                _blobInstanceBuffer?.Dispose();
                _blobInstanceBuffer = new DynamicVertexBuffer(GraphicsDevice, ModelInstance.VertexDeclaration, _blobInstances.Length, BufferUsage.WriteOnly);
            }

            _blobInstanceBuffer.SetData(_blobInstances, 0, _blobInstanceCount, SetDataOptions.Discard);

            _shadowOverlayEffect.CurrentTechnique = _shadowOverlayEffect.Techniques["ContactBlobs"];

            GraphicsDevice.SetVertexBuffers(
                new VertexBufferBinding(_blobQuadVertexBuffer, 0, 0),
                new VertexBufferBinding(_blobInstanceBuffer, 0, 1));
            GraphicsDevice.Indices = _blobQuadIndexBuffer;

            _shadowOverlayEffect.CurrentTechnique.Passes[0].Apply();
            GraphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, 2, _blobInstanceCount);
        }

        /// <summary>
        /// Position to draw a freshly attached ball at: it eases towards the position of its body, which the
        /// constraints have already dragged into the cell. Every other ball is drawn where its body is.
        /// </summary>
        private static System.Numerics.Vector3 GlideRenderPosition(PhysicsBall ball, System.Numerics.Vector3 bodyPosition, float glide)
        {
            //The constraints that drag the body into its cell are only solved by the next timestep, so on this
            //one frame the body is still where the ball hit and applying the offset would move it the wrong way
            if (ball.RenderOffsetArmed)
            {
                ball.RenderOffsetArmed = false;
                return bodyPosition;
            }

            if (ball.RenderOffset == System.Numerics.Vector3.Zero) return bodyPosition;

            ball.RenderOffset *= 1f - glide;

            if (ball.RenderOffset.LengthSquared() < BALL_ATTACH_GLIDE_DONE_SQUARED)
            {
                ball.RenderOffset = System.Numerics.Vector3.Zero;
                return bodyPosition;
            }

            return bodyPosition + ball.RenderOffset;
        }

        private void CollectBallInstance(PhysicsBall ball, Vector4 occlusionData, float glide)
        {
            _collectedBalls++;

            RigidPose pose = ball.BallReference.Pose;
            System.Numerics.Vector3 renderPosition = GlideRenderPosition(ball, pose.Position, glide);

            Vector3 position = new(renderPosition.X, renderPosition.Y, renderPosition.Z);

            int typeIndex = (int)ball.Type - 1;
            if (typeIndex < 0 || typeIndex >= BALL_TYPE_COUNT) return;

            //Mesh resolution by distance from the camera
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

            Microsoft.Xna.Framework.Matrix world = Microsoft.Xna.Framework.Matrix.CreateFromQuaternion(
                    new Quaternion(pose.Orientation.X, pose.Orientation.Y, pose.Orientation.Z, pose.Orientation.W))
                * Microsoft.Xna.Framework.Matrix.CreateTranslation(renderPosition.X, renderPosition.Y, renderPosition.Z);

            bucket[count] = new ModelInstance(world, occlusionData);
            _ballInstanceCounts[bucketIndex] = count + 1;

            //Balls close to the ground also darken it with a contact AO blob
            if (position.Y - SHADOW_OVERLAY_Y < BLOB_FADE_HEIGHT)
            {
                if (_blobInstanceCount == _blobInstances.Length) Array.Resize(ref _blobInstances, _blobInstances.Length * 2);
                _blobInstances[_blobInstanceCount++] = bucket[count];
            }
        }

        private void SetGraphics(bool windowed = false)
        {
            _graphics.PreferredBackBufferWidth = windowed ? _windowWidth : GraphicsDevice.DisplayMode.Width;
            _graphics.PreferredBackBufferHeight = windowed ? _windowHeight : GraphicsDevice.DisplayMode.Height;
            _graphics.IsFullScreen = !windowed;

            _graphics.SynchronizeWithVerticalRetrace = !_uncappedFps;

            _graphics.ApplyChanges();

            EnsureSceneTarget(); //The back buffer just changed size, so the scene target has to follow

            IsMouseVisible = false;
            IsFixedTimeStep = false;
        }

        private void Graphics_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            e.GraphicsDeviceInformation.PresentationParameters.PresentationInterval = _uncappedFps ? PresentInterval.Immediate : PresentInterval.One;
            e.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.HiDef;

            //The 3D scene never reaches the back buffer any more — it goes through the HDR target and
            //arrives as one already-resolved full-screen quad — so multisampling the back buffer would
            //cost memory and antialias nothing. Any MSAA now belongs on the scene target itself.
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 0;
        }

        /// <summary>
        /// Creates the HDR scene target, or resizes it after a window resize or a fullscreen switch.
        /// Unlike the old supersample-only target this one always exists: the scene is rendered in linear
        /// radiance now, and there is nowhere in an 8-bit sRGB back buffer to put that.
        /// </summary>
        private void EnsureSceneTarget()
        {
            if (GraphicsDevice == null) return;

            int width = GraphicsDevice.PresentationParameters.BackBufferWidth * _supersampleFactor;
            int height = GraphicsDevice.PresentationParameters.BackBufferHeight * _supersampleFactor;

            if (_sceneTarget != null && _sceneTarget.Width == width && _sceneTarget.Height == height) return;

            _sceneTarget?.Dispose();

            //Supersampling already averages _supersampleFactor^2 samples per output pixel, geometry edges
            //included, so MSAA only earns its memory when supersampling is off.
            _sceneTarget = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.HdrBlendable,
                DepthFormat.Depth24Stencil8, _supersampleFactor > 1 ? 0 : MSAA_SAMPLES, RenderTargetUsage.DiscardContents);

            //Sized off the back buffer, not off the supersampled target: the glare is blurred anyway, so
            //it gains nothing from the extra samples and would only cost fill rate to produce them
            int glareWidth = Math.Max(GraphicsDevice.PresentationParameters.BackBufferWidth / GLARE_DOWNSAMPLE, 1);
            int glareHeight = Math.Max(GraphicsDevice.PresentationParameters.BackBufferHeight / GLARE_DOWNSAMPLE, 1);

            _glareBright?.Dispose();
            _glareStreak?.Dispose();

            _glareBright = new RenderTarget2D(GraphicsDevice, glareWidth, glareHeight, false, SurfaceFormat.HdrBlendable, DepthFormat.None);
            _glareStreak = new RenderTarget2D(GraphicsDevice, glareWidth, glareHeight, false, SurfaceFormat.HdrBlendable, DepthFormat.None);
        }

        /// <summary>
        /// Extracts what in the scene is bright enough to glare and smears it into a star of streaks.
        /// Runs between the scene and the tonemap, both passes at quarter resolution.
        /// </summary>
        private void DrawGlare()
        {
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.SetVertexBuffer(_fullScreenQuad);

            GraphicsDevice.SetRenderTarget(_glareBright);
            _glareEffect.CurrentTechnique = _glareEffect.Techniques["BrightPass"];
            _glareEffect.Parameters["SourceTexture"].SetValue(_sceneTarget);
            _glareEffect.Parameters["GlareThreshold"].SetValue(GLARE_THRESHOLD);
            DrawFullScreenQuad(_glareEffect);

            GraphicsDevice.SetRenderTarget(_glareStreak);
            _glareEffect.CurrentTechnique = _glareEffect.Techniques["Streak"];
            _glareEffect.Parameters["SourceTexture"].SetValue(_glareBright);
            _glareEffect.Parameters["SourceTexelSize"].SetValue(new Vector2(1f / _glareBright.Width, 1f / _glareBright.Height));
            _glareEffect.Parameters["StreakLength"].SetValue(GLARE_STREAK_LENGTH);
            _glareEffect.Parameters["StreakFalloff"].SetValue(GLARE_STREAK_FALLOFF);
            DrawFullScreenQuad(_glareEffect);
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
        /// Builds the clip-space quad the tonemap pass draws. Its corners are already in normalized device
        /// coordinates, so the pass needs no transform of any kind.
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
        /// Box-filters the supersampled HDR scene onto the back buffer, tonemaps it from linear radiance
        /// into display range and encodes it to sRGB. This is the frame's one and only exit from linear
        /// light; everything drawn after it (the overlay, the aimer) is already in display space.
        /// </summary>
        private void ResolveSceneTarget()
        {
            DrawGlare(); //Reads the scene target, so it has to happen before the back buffer is bound

            GraphicsDevice.SetRenderTarget(null);

            _tonemapEffect.Parameters["GlareTexture"].SetValue(_glareStreak);
            _tonemapEffect.Parameters["GlareIntensity"].SetValue(GLARE_INTENSITY);
            _tonemapEffect.Parameters["SceneTexture"].SetValue(_sceneTarget);
            _tonemapEffect.Parameters["SourceTexelSize"].SetValue(new Vector2(1f / _sceneTarget.Width, 1f / _sceneTarget.Height));
            _tonemapEffect.Parameters["SupersampleFactor"].SetValue(_supersampleFactor);
            _tonemapEffect.Parameters["Exposure"].SetValue(_exposure);

            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.SetVertexBuffer(_fullScreenQuad);

            foreach (EffectPass pass in _tonemapEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
            }
        }

        private StaticReference CreateStatic(System.Numerics.Vector3 position, Box boundingBox)
        {
            var shape = new CollidableDescription(_simulation.Shapes.Add(boundingBox), 0.1f).Shape;
            return new StaticReference(_simulation.Statics.Add(new StaticDescription(position, shape)), _simulation.Statics);
        }

        protected override void UnloadContent()
        {
            _unitBox?.Dispose();
            _sceneTarget?.Dispose();
            _glareBright?.Dispose();
            _glareStreak?.Dispose();
            _fullScreenQuad?.Dispose();
            _simulation.Dispose();
            _threadDispatcher.Dispose();
            _bufferPool.Clear();
        }

        private void InitializeShooting()
        {
            var ballShape = new Sphere(BallsConstraintsBuilder.BALL_RADIUS);
            _shotBall = BodyDescription.CreateDynamic(new System.Numerics.Vector3(), ballShape.ComputeInertia(BallsConstraintsBuilder.BALL_MASS), BallsConstraintsBuilder.GetSphereShapeIndex(_simulation), Constants.HUNDREDTH);
            _shotBalls = new List<PhysicsBall>();
            _fallingBalls = new List<PhysicsBall>();
        }

        private void ShootBall(Vector3? targetOverride = null)
        {
            var sourcePosition = _gameMode ? _cannon.Position : _camera.Position;
            var shootTarget = targetOverride ?? (_gameMode ? _cannon.AimTarget : _camera.Target);

            _shotBall.Pose.Position = new System.Numerics.Vector3(sourcePosition.X, sourcePosition.Y, sourcePosition.Z);

            var direction = shootTarget - sourcePosition;
            direction.Normalize();
            direction *= SHOOT_MULTIPLIER;

            _shotBall.Velocity.Linear = new System.Numerics.Vector3(direction.X, direction.Y, direction.Z);

            BodyHandle bodyHandle = _simulation.Bodies.Add(_shotBall);

            PhysicsBall ball = new()
            {
                BallReference = new(bodyHandle, _simulation.Bodies),
                Type = (BallType)RANDOM.Next((int)BallType.Type1, (int)BallType.Type4 + 1) //Random color so same-type clusters can form
            };

            _shotBalls.Add(ball);
            RecountBallsAndConstraints();

            #region Contact event registration

            //TODO: Unregister when removed from world
            _events.Register(_simulation.Bodies[bodyHandle].CollidableReference, _eventHandler);

            #endregion
        }

        private void UpdateCannon(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(mgKeys.NumPad4)) _cannon.Orbit(1f);
            if (Keyboard.GetState().IsKeyDown(mgKeys.NumPad6)) _cannon.Orbit(-1f);
            if (Keyboard.GetState().IsKeyDown(mgKeys.Up)) _cannon.Aim(new Vector2(1f, 0f), gameTime);
            if (Keyboard.GetState().IsKeyDown(mgKeys.Down)) _cannon.Aim(new Vector2(-1f, 0f), gameTime);
            if (Keyboard.GetState().IsKeyDown(mgKeys.Left)) _cannon.Aim(new Vector2(0f, 1f), gameTime);
            if (Keyboard.GetState().IsKeyDown(mgKeys.Right)) _cannon.Aim(new Vector2(0f, -1f), gameTime);

            _cannon.Update(gameTime);

            //The camera must follow the cannon's pose from THIS frame (after Update above has moved it).
            //Reading the pose before the move made the camera lag one frame behind, so any frame-time
            //fluctuation (shooting, contact processing) showed up as the cannon jittering on screen (#29).
            if (_gameMode && !_gameModeAnimStarted)
            {
                _camera.Position = GetCanonOffsettedPos();
                _camera.Target = GetCannonOffsettedTarget();
            }
        }

        private Vector3 GetCannonDirection() => Vector3.Normalize(_cannon.Position - _cannon.OrbitCenter) * 10f;
        private Vector3 GetCanonOffsettedPos() => _cannon.Position + GetCannonDirection() + _gameCameraOffset;
        private Vector3 GetCannonOffsettedTarget() => _cannon.OrbitCenter - _gameCameraOffset;
    }

    #region Contact event

    //WIP
    public class EventHandler : IContactEventHandler
    {
        public Simulation Simulation;
        public BufferPool Pool;
        private ContactEvents _contactEvents;
        private KinematicBody _ceiling;
        public BallsMap Map;
        public PhysicsBall[,,] PhysicsBalls;
        public List<PhysicsBall> ShotBalls;
        public List<PhysicsBall> FallingBalls;

        public EventHandler(Simulation simulation, BufferPool pool, ContactEvents contactEvents, KinematicBody ceiling, PhysicsBall[,,] physicsBalls, List<PhysicsBall> shotBalls, List<PhysicsBall> fallingBalls)
        {
            Simulation = simulation;
            Pool = pool;
            _contactEvents = contactEvents;
            _ceiling = ceiling;
            PhysicsBalls = physicsBalls;
            ShotBalls = shotBalls;
            FallingBalls = fallingBalls;
        }

        //Contact callbacks run inside Simulation.Timestep, potentially from multiple worker threads at once.
        //Mutating the simulation (constraints, velocities) or the ContactEvents listener set from there corrupts state
        //the solver and the event system are using (this used to cause occasional NullReferenceExceptions).
        //Contacts are therefore only recorded here and processed on the main thread by ProcessQueuedContacts after the timestep.
        private readonly ConcurrentQueue<QueuedContact> _queuedContacts = new();

        private readonly struct QueuedContact
        {
            public readonly CollidableReference EventSource;
            public readonly CollidablePair Pair;
            public readonly Vector3 ContactOffset;
            public readonly Vector3 ContactNormal;
            public readonly float Depth;
            public readonly int FeatureId;
            public readonly int ContactIndex;
            public readonly int WorkerIndex;

            public QueuedContact(CollidableReference eventSource, CollidablePair pair, Vector3 contactOffset, Vector3 contactNormal,
                float depth, int featureId, int contactIndex, int workerIndex)
            {
                EventSource = eventSource;
                Pair = pair;
                ContactOffset = contactOffset;
                ContactNormal = contactNormal;
                Depth = depth;
                FeatureId = featureId;
                ContactIndex = contactIndex;
                WorkerIndex = workerIndex;
            }
        }

        public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
            Vector3 contactOffset, Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold>
        {
            _queuedContacts.Enqueue(new QueuedContact(eventSource, pair, contactOffset, contactNormal, depth, featureId, contactIndex, workerIndex));
        }

        /// <summary>
        /// Processes contacts recorded during the last timestep. Must be called from the main thread while the simulation is not stepping,
        /// after <see cref="ContactEvents.Flush"/>.
        /// </summary>
        /// <returns>Number of balls attached to the ceiling.</returns>
        public int ProcessQueuedContacts()
        {
            int attachedBalls = 0;
            while (_queuedContacts.TryDequeue(out QueuedContact contact))
                if (ProcessContact(contact)) attachedBalls++;
            return attachedBalls;
        }

        private bool ProcessContact(in QueuedContact contact)
        {
            CollidablePair pair = contact.Pair;

#if DEBUG
            Console.WriteLine(" → Ball collided!");
            Console.WriteLine(nameof(contact.EventSource) + " : " + contact.EventSource.ToString());
            Console.WriteLine(nameof(pair.A) + " : " + pair.A.ToString());
            Console.WriteLine(nameof(pair.B) + " : " + pair.B.ToString());
            Console.WriteLine(nameof(contact.ContactOffset) + " : " + contact.ContactOffset.ToString());
            Console.WriteLine(nameof(contact.ContactNormal) + " : " + contact.ContactNormal.ToString());
            Console.WriteLine(nameof(contact.Depth) + " : " + contact.Depth.ToString());
            Console.WriteLine(nameof(contact.FeatureId) + " : " + contact.FeatureId.ToString());
            Console.WriteLine(nameof(contact.ContactIndex) + " : " + contact.ContactIndex.ToString());
            Console.WriteLine(nameof(contact.WorkerIndex) + " : " + contact.WorkerIndex.ToString());
            Console.WriteLine();
#endif

            //Once ball touches the ground or ceiling, unregister collision event
            //TODO: This might be possible to do by checking if the Static/Kinematic body is specific object (ground block, ceiling block by BodyReference)
            if (pair.A.Mobility == CollidableMobility.Static || pair.B.Mobility == CollidableMobility.Static ||
                pair.A.Mobility == CollidableMobility.Kinematic || pair.B.Mobility == CollidableMobility.Kinematic)
            {
                //A single timestep can queue several contacts for the same ball, so the listener may have been unregistered by a previous one
                if (pair.A.Mobility == CollidableMobility.Dynamic && _contactEvents.IsListener(pair.A)) _contactEvents.Unregister(pair.A);
                if (pair.B.Mobility == CollidableMobility.Dynamic && _contactEvents.IsListener(pair.B)) _contactEvents.Unregister(pair.B);
            }

            if (Map == null)
            {
                Console.WriteLine("Map is null\n");
                return false;
            }

            //The event source is the registered listener, i.e. the shot ball
            BodyHandle shotBallHandle = contact.EventSource.BodyHandle;
            var physicsBall = ShotBalls.Where(x => x.BallReference.Handle == shotBallHandle).FirstOrDefault(); //Linq is ok since this list should be short
            if (physicsBall == null)
            {
#if DEBUG
                Console.WriteLine("Ball already attached or no longer tracked as shot, skipping");
#endif
                return false;
            }

            CollidableReference other = pair.A.Packed == contact.EventSource.Packed ? pair.B : pair.A;

            #region Find a free cell for the ball

            Vector3 allowedPosition;
            XZLevel arrayPosition;

            if (other.Mobility == CollidableMobility.Kinematic && other.BodyHandle == _ceiling.BodyHandle)
            {
#if DEBUG
                Console.WriteLine(" → CEILING HIT");
#endif
                allowedPosition = Map.PutBallAtClosestEmptyCeilingPosition(contact.ContactOffset, out arrayPosition, physicsBall.Type);
            }
            else if (other.Mobility == CollidableMobility.Dynamic && TryFindMapBall(other.BodyHandle, out PhysicsBall hitBall))
            {
#if DEBUG
                Console.WriteLine(" → STRUCTURE BALL HIT");
#endif
                //Manifold offsets are relative to the position of the pair's first collidable
                var worldContact = Simulation.Bodies[pair.A.BodyHandle].Pose.Position + contact.ContactOffset.ToNumerics();
                allowedPosition = Map.PutBallAtClosestEmptyPositionNextTo(worldContact, hitBall.ArrayPosition, out arrayPosition, physicsBall.Type);
            }
            else return false; //Ground, a loose shot ball, …

            if (allowedPosition.X == float.MinValue)
            {
#if DEBUG
                Console.WriteLine("Outside of the map or every neighboring cell already occupied by another ball");
#endif
                return false;
            }

#if DEBUG
            Console.WriteLine("Ball placed at: " + allowedPosition);
#endif

            #endregion

            #region Attach the ball to the structure

            physicsBall.ArrayPosition = arrayPosition;

            ShotBalls.Remove(physicsBall); //Not shot anymore

            PhysicsBalls[arrayPosition.X, arrayPosition.Z, arrayPosition.Level] = physicsBall; //Part of the map now

            physicsBall.BallReference.Velocity.Linear = default; //Removing velocity from the shot
            physicsBall.BallReference.Velocity.Angular = default; //Also stop spinning, so the freshly created constraint anchors are not dragged around by residual rotation

            //The ball is snapped to the nearest free cell rather than to where it hit, so the constraints created
            //below drag it across up to several ball diameters within a frame or two. Drawing it gliding in from
            //where it actually hit hides that click without touching the simulation.
            physicsBall.StartRenderGlide(allowedPosition.ToNumerics());

            //Constraint anchors are computed from the static map grid (ideal positions) and rotated into each body's current local frame,
            //so they are correct even after the simulation has been running
            BallsConstraintsBuilder.AttachBallToStructure(physicsBall, PhysicsBalls, Map, Simulation, _ceiling.BodyReference);

            //Attached to the structure – no need to listen for its contacts anymore
            if (_contactEvents.IsListener(contact.EventSource)) _contactEvents.Unregister(contact.EventSource);

            #region Same-type cluster removal

            int releasedBalls = BallsConstraintsBuilder.ReleaseSameTypeCluster(physicsBall, PhysicsBalls, Map, Simulation, FallingBalls);

#if DEBUG
            if (releasedBalls > 0) Console.WriteLine($"Released a cluster of {releasedBalls} balls of type {physicsBall.Type}");
#endif

            #endregion

            #endregion

            return true;
        }

        private bool TryFindMapBall(BodyHandle handle, out PhysicsBall ball)
        {
            ball = null;
            if (PhysicsBalls == null) return false;

            XZLevel size = XZLevel.FromArray(PhysicsBalls);

            for (byte level = 0; level < size.Level; level++)
                for (byte x = 0; x < size.X; x++)
                    for (byte z = 0; z < size.Z; z++)
                    {
                        PhysicsBall candidate = PhysicsBalls[x, z, level];
                        if (candidate != null && candidate.BallReference.Handle == handle)
                        {
                            ball = candidate;
                            return true;
                        }
                    }

            return false;
        }
    }

    #endregion

    #region IContactEventHandler

    public interface IContactEventHandler
    {
        public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, Vector3 contactOffset, Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        void OnContactRemoved<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int removedFeatureId, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        void OnStartedTouching<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        void OnTouching<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        void OnStoppedTouching<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        void OnPairCreated<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        //void OnPairUpdated<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        void OnPairEnded(CollidableReference eventSource, CollidablePair pair) { }

        #endregion
    }
}

/*

Ball's contact points position from its origin (0,0,0):
        [ X,   Y,   Z ]
TOP   : [ 0,   0,   0.5]
DOWN  : [ 0,   0,  -0.5]
LEFT  : [ 0,  -0.5, 0]
RIGT  : [ 0,   0.5, 0]
FRONT : [ 0.5, 0,   0]
BACK  : [-0.5, 0,   0]

TOP-LEFT-BACK   : [-0.25, -0.25,  0.353553]
TOP-RIGHT-BACK  : [-0.25,  0.25,  0.353553]
TOP-LEFT-FRONT  : [0.25,  -0.25,  0.353553]
TOP-RIGHT-FRONT : [0.25,   0.25,  0.353553]

BOTTOM-LEFT-BACK   : [-0.25, -0.25, -0.353553]
BOTTOM-RIGHT-BACK  : [-0.25,  0.25, -0.353553]
BOTTOM-LEFT-FRONT  : [ 0.25, -0.25, -0.353553]
BOTTOM-RIGHT-FRONT : [ 0.25,  0.25, -0.353553]

*/

