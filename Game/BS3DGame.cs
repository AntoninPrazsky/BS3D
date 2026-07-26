using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.Core;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

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
        private const float CAMERA_DISTANCE = 34f;
        private const float CAMERA_HEIGHT = -1.5f;
        private const float CAMERA_TARGET_Y = 3.5f;

        private readonly GraphicsDeviceManager _graphics;
        private readonly bool _uncappedFps;
        private readonly int _supersampleFactor;
        private readonly float _exposure;
        private bool _fullscreen;

        private RecoilCamera _camera;

        //Wall clock. Everything alive in the scene runs off it — the balls' heartbeat, the city's windows —
        //so none of it is tied to a simulation that may later be paused.
        private float _wallClock;

        private bool _wasActive = true;

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
        //under any dome, so a bright one only fights the neon it is meant to set off.
        private const int SKY_DOME = 13;

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

        //The round stone island the gun stands on: a solid disc out to its rim, then a hard vertical edge the
        //city falls away past. No drain and no physics floor here yet — nothing falls.
        private static readonly float ISLAND_Y = -8.5f;

        //Solid, because there is nothing to leave a hole for. The Testbed's disc is a washer with a 14-unit
        //bore, and the glass drain funnel, its gold rims and the dark pit shaft are what fill it; this build
        //has none of them, so the bore was simply a 28-unit hole in the floor under the hanging cluster. It
        //goes back to 14 the day the funnel arrives, and not before.
        private static readonly float ISLAND_INNER_RADIUS = 0f;
        private static readonly float ISLAND_RADIUS = 26f;
        private static readonly float ISLAND_EDGE_HEIGHT = 5f;
        private static readonly int ISLAND_SEGMENTS = 64;
        private static readonly Vector3 ISLAND_COLOR = new(0.58f, 0.56f, 0.54f);

        //How many world units one tile of the marble spans. The Testbed derives the same figure from the size
        //of the ground block the texture was modelled on; here it is what it is — the grain of the stone.
        private static readonly float ISLAND_DETAIL_SPAN = 30f;

        private DiscMesh _islandMesh;
        private InstancedModelRenderer _islandRenderer;
        private Matrix _islandWorld;

        private const int MAX_SCENE_LIGHTS = 8;
        private readonly Vector3[] _sceneLightPos = new Vector3[MAX_SCENE_LIGHTS];
        private readonly Vector3[] _sceneLightColor = new Vector3[MAX_SCENE_LIGHTS];
        private readonly float[] _sceneLightRange = new float[MAX_SCENE_LIGHTS];

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

        private const float BALL_RADIUS = Constants.HALF;

        private SphereMesh[] _ballMeshes;
        private InstancedModelRenderer[] _ballRenderers;

        private readonly ModelInstance[][] _ballInstances = new ModelInstance[BALL_TYPE_COUNT * BALL_LOD_COUNT][];
        private readonly int[] _ballInstanceCounts = new int[BALL_TYPE_COUNT * BALL_LOD_COUNT];

        #endregion

        #region The cluster

        //One hanging cluster, carved to a stalactite so it reads as something suspended rather than a cube:
        //widest at the top level, tapering down. A map file will replace this.
        private const byte CLUSTER_X = 9;
        private const byte CLUSTER_Z = 9;
        private const byte CLUSTER_LEVELS = 12;
        private const float CLUSTER_TOP_RADIUS = 4.6f;

        //Empty field levels below the layout: the room the cluster grows into as shot balls attach under it,
        //which is how a map file's field is taller than the layout hanging at its top. Must be even — odd
        //levels are shifted by +0.5 in X and Z, so an odd offset would flip the parity of every layer of the
        //layout and change how the balls nest into each other.
        private const byte CLUSTER_EXTRA_LEVELS = 6;
        private const byte FIELD_LEVELS = CLUSTER_LEVELS + CLUSTER_EXTRA_LEVELS;

        //A cell's height is its level index over √2 and the layout now sits that many levels up the field, so
        //without this the whole cluster would hang CLUSTER_EXTRA_LEVELS higher than it was framed for. The
        //grid is the truth; this is only where it is drawn, so it is applied at the map/world boundary alone.
        private static readonly float CLUSTER_WORLD_Y = -CLUSTER_EXTRA_LEVELS / Constants.SQRT_TWO;

        /// <summary>A ball that is part of the hanging structure: where it is, what colour, how boxed in.</summary>
        private struct ClusterBall
        {
            public Vector3 Position;
            public XZLevel Cell;
            public BallType Type;
            public Vector4 Occlusion;
        }

        //The lattice is the truth about the cluster — what is where, and what is free for a shot to land in.
        //The list is the flattened frame-facing copy of it: what is drawn, and what a shot is tested against.
        private BallsMap _map;
        private readonly List<ClusterBall> _clusterBalls = new();

        //What the cluster hangs from: a translucent glass plate over the play field, the Testbed's own ceiling.
        //Without it the cluster hangs out of nothing, and a mass of balls suspended in mid-air over a city
        //reads as an object that has not been finished rather than as one that is held up.
        private static readonly Vector3 CEILING_GLASS_COLOR = new(0.55f, 0.75f, 0.85f);
        private static readonly float CEILING_GLASS_ALPHA = 0.4f;

        private BoxMesh _ceilingMesh;
        private InstancedModelRenderer _ceilingRenderer;
        private Matrix _ceilingWorld;

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

        //Several ball diameters a frame: the shot is a streak, not something the eye can follow. That is
        //the intended feel, and it is why the launch smear below exists at all.
        private const float SHOOT_SPEED = 200f;

        //How hard a shot hits the camera. Full strength — the whole point of this build is that firing is
        //felt, so it is tuned at the kick's own ceiling rather than at a polite fraction of it.
        private const float RECOIL_KICK = 1f;

        private const float MOUSE_AIM_SENSITIVITY = 2.0f;
        private const float PAD_AIM_RATE = 1.0f;
        private const float CANNON_ORBIT_RATE = 1.0f;

        /// <summary>A ball in flight. No rigid body: a position, a velocity and gravity.</summary>
        private struct FlyingBall
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public BallType Type;
            public float Age;
        }

        private readonly List<FlyingBall> _flying = new();

        private const float FLYING_LIFETIME = 4f;
        private const float FLYING_KILL_Y = -60f;

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
        private bool _mouseAimInitialized;

        #endregion

        public BS3DGame(bool fullscreen = false, int supersampleFactor = 2, float exposure = DEFAULT_EXPOSURE, bool uncappedFps = false)
        {
            _fullscreen = fullscreen;
            _supersampleFactor = Math.Clamp(supersampleFactor, 1, 4);
            _exposure = exposure > 0f ? exposure : DEFAULT_EXPOSURE;
            _uncappedFps = uncappedFps;

            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreparingDeviceSettings += Graphics_PreparingDeviceSettings;

            Content.RootDirectory = "Content";

            Window.AllowUserResizing = true;
            Window.Title = "BS3D";
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

            IsMouseVisible = false;
            IsFixedTimeStep = false;
        }

        private void OnClientSizeChanged()
        {
            if (_camera != null) _camera.AspectRatio = GraphicsDevice.Viewport.AspectRatio;
            EnsureSceneTarget();
        }

        protected override void Initialize()
        {
            _camera = new RecoilCamera
            {
                AspectRatio = GraphicsDevice.Viewport.AspectRatio,
                FieldOfView = GAME_FOV
            };

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

            BuildScene();
            BuildCluster();

            _skyEffect = Content.Load<Effect>("Shaders/Sky");
            _skyCameraPositionParam = _skyEffect.Parameters["CameraPosition"];
            _sky = new SkyDome(Content.Load<Model>("Skyes/SkyDome" + SKY_DOME), GraphicsDevice, linearVertexColors: true)
            {
                Effect = _skyEffect
            };

            SetCloudParameters();
            ApplySkyLighting();
            ApplyNeonLights();

            EnsureSceneTarget();

            Console.WriteLine($"[game] {_city.Buildings.Length} buildings, {_clusterBalls.Count} balls in the cluster, dome {SKY_DOME}");
        }

        /// <summary>
        /// The neon city and the island it leaves a clearing for. One unit box under a different instance
        /// matrix per building, so the whole skyline is a single instanced draw call.
        /// </summary>
        private void BuildScene()
        {
            _unitBox = new BoxMesh(GraphicsDevice, 1f, 1f, 1f);
            _city = new City(seed: 20260720, arenaHalfExtent: ISLAND_RADIUS, config: _cityConfig);

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
        }

        /// <summary>
        /// Fills the hanging cluster: a <see cref="BallsMap"/> carved to a taper and centred on the origin.
        /// The layout hangs at the top of a taller field, so the empty levels underneath are room for shot
        /// balls to attach into — the same arrangement a map file carries, which is what a level will replace
        /// this with. <see cref="RebuildClusterBalls"/> then derives the drawn copy from it.
        /// </summary>
        private void BuildCluster()
        {
            _map = new BallsMap(CLUSTER_X, CLUSTER_Z, FIELD_LEVELS);

            float centreX = (CLUSTER_X - 1) * Constants.HALF;
            float centreZ = (CLUSTER_Z - 1) * Constants.HALF;

            for (byte level = 0; level < CLUSTER_LEVELS; level++)
            {
                //Widest at the top level and tapering down, so the cluster hangs rather than sits
                float radius = CLUSTER_TOP_RADIUS * (level + 1) / CLUSTER_LEVELS;

                for (byte x = 0; x < CLUSTER_X; x++)
                    for (byte z = 0; z < CLUSTER_Z; z++)
                    {
                        float dx = x - centreX;
                        float dz = z - centreZ;
                        if (dx * dx + dz * dz > radius * radius) continue;

                        _map.PutBallAt(x, z, (byte)(level + CLUSTER_EXTRA_LEVELS), RandomBallType());
                    }
            }

            _map.Center();

            RebuildClusterBalls();

            //Odd levels are shifted by +0.5 and a ball's radius is another 0.5, so a field's worth of balls is
            //one unit wider than its cell count; the plate covers it with that margin, as the Testbed's does.
            _ceilingMesh = new BoxMesh(GraphicsDevice, CLUSTER_X + 1f, 1f, CLUSTER_Z + 1f);
            _ceilingRenderer = new InstancedModelRenderer(GraphicsDevice, _ceilingMesh, CEILING_GLASS_COLOR, _instancingEffect, CEILING_GLASS_ALPHA);

            //Two above the centres of the top level's balls, so the plate clears them and the cluster reads as
            //hanging just under it rather than embedded in it
            _ceilingWorld = Matrix.CreateTranslation(MapToWorld(new Vector3(0f, (FIELD_LEVELS - 1) / Constants.SQRT_TWO + 2f, 0f)));
        }

        /// <summary>
        /// Flattens the lattice into the list the frame draws and a shot is tested against, each ball carrying
        /// the cell it came from so a hit knows where in the lattice it landed.
        /// <para>
        /// Neighbour-based ambient occlusion is derived here too — a ball buried in the mass is darker than
        /// one on the outside, which is what makes the cluster read as one body rather than a heap of
        /// spheres. It is re-derived for the whole cluster rather than for the new ball alone, because a
        /// ball that attaches also boxes in every neighbour it just arrived next to.
        /// </para>
        /// </summary>
        private void RebuildClusterBalls()
        {
            _clusterBalls.Clear();

            StaticBall[,,] balls = _map.GetStaticBallsArray();
            XZLevel size = _map.GetStaticBallsArraySize();

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        StaticBall ball = balls[x, z, level];
                        if (ball == null) continue;

                        XZLevel cell = new(x, z, level);

                        int occluders = BallsMap.CountOccupiedNeighbors(balls, cell, size, out Vector3 direction);
                        float open = 1f - BALL_OCCLUSION_STRENGTH * Math.Min(occluders, MAX_BALL_OCCLUDERS) / MAX_BALL_OCCLUDERS;

                        //The direction is a sum of unit vectors, one per occupied neighbour, so it has to be
                        //divided by the most there can be before the shader reads it as a direction-and-weight.
                        //Handed over raw it is up to twelve times too long, and the shader's dot against it
                        //saturates over most of the ball — which paints a hard black crescent on every surface
                        //ball instead of the soft inward shading that makes the cluster read as one body.
                        _clusterBalls.Add(new ClusterBall
                        {
                            Position = MapToWorld(ball.Position),
                            Cell = cell,
                            Type = ball.Type,
                            Occlusion = new Vector4(direction / MAX_BALL_OCCLUDERS, open)
                        });
                    }
        }

        /// <summary>Where a lattice position is drawn.</summary>
        private static Vector3 MapToWorld(Vector3 mapPosition) => new(mapPosition.X, mapPosition.Y + CLUSTER_WORLD_Y, mapPosition.Z);

        /// <summary>Which lattice position a point in the world is at — the inverse of <see cref="MapToWorld"/>.</summary>
        private static Vector3 WorldToMap(Vector3 worldPosition) => new(worldPosition.X, worldPosition.Y - CLUSTER_WORLD_Y, worldPosition.Z);

        /// <summary>
        /// Every renderer that takes its lighting from the sky dome.
        /// </summary>
        private IEnumerable<InstancedModelRenderer> SkyLitRenderers()
        {
            foreach (InstancedModelRenderer ballRenderer in _ballRenderers) yield return ballRenderer;

            yield return _cannonRenderer;
            yield return _cityRenderer;
            yield return _islandRenderer;
            yield return _ceilingRenderer;
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
        /// The neon's point lights: a ring of alternating magenta and cyan around the island, so the near
        /// towers, the island, the gun and the balls actually take the neon's colour rather than the windows
        /// merely glowing. Set once — they do not move — on the shared effect every lit surface draws with.
        /// </summary>
        private void ApplyNeonLights()
        {
            NeonConfig neon = _cityConfig.NeonLook;
            int count = Math.Min(neon.LightCount, MAX_SCENE_LIGHTS);

            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * MathHelper.TwoPi;
                _sceneLightPos[i] = new Vector3(MathF.Cos(angle) * neon.LightRadius, neon.LightHeight, MathF.Sin(angle) * neon.LightRadius);
                _sceneLightColor[i] = (i % 2 == 0) ? neon.Magenta.ToVector3() : neon.Cyan.ToVector3();
                _sceneLightRange[i] = neon.LightRange;
            }

            _instancingEffect.Parameters["SceneLightPosition"].SetValue(_sceneLightPos);
            _instancingEffect.Parameters["SceneLightColor"].SetValue(_sceneLightColor);
            _instancingEffect.Parameters["SceneLightRange"].SetValue(_sceneLightRange);
            _instancingEffect.Parameters["SceneLightCount"].SetValue(count);
        }

        protected override void Update(GameTime gameTime)
        {
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _wallClock += elapsed;

            //The very click that refocuses a windowed game would otherwise read as a fresh press against a
            //stale "released" state and fire an unintended shot, since input is not sampled while inactive.
            bool edgeInputAllowed = IsActive && _wasActive;

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
            }

            _wasActive = IsActive;

            _cannon.Update(gameTime);

            //The queue glides forward into the slot the fired ball left rather than snapping
            if (_magazineSlide > 0f) _magazineSlide *= MathF.Exp(-elapsed / MAGAZINE_SLIDE_TAU);
            if (_magazineSlide < 0.001f) _magazineSlide = 0f;

            UpdateFlyingBalls(elapsed);
            UpdateTrails(elapsed);

            UpdateCamera(elapsed);

            base.Update(gameTime);
        }

        private void UpdateInput(GameTime gameTime, bool edgeInputAllowed, GamePadState pad)
        {
            KeyboardState keyboard = Keyboard.GetState();

            if (keyboard.IsKeyDown(Keys.Escape) || pad.IsButtonDown(Buttons.Back)) Exit();

            if (edgeInputAllowed)
            {
                if (keyboard.IsKeyDown(Keys.F11) && !_previousKeyboard.IsKeyDown(Keys.F11))
                {
                    _fullscreen = !_fullscreen;
                    SetGraphics();
                }

                //Space fires; the gamepad fires off its right trigger, read with the aim (below)
                if (keyboard.IsKeyDown(Keys.Space) && !_previousKeyboard.IsKeyDown(Keys.Space)) Shoot();
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
        }

        private bool _padTriggerReleased = true;

        /// <summary>
        /// Fires the ball the player can see sitting at the muzzle: a flying ball, a launch smear along the
        /// shot, the queue shifted forward — and the camera kicked, which is the whole feel of the thing.
        /// </summary>
        private void Shoot()
        {
            Vector3 direction = CannonAimDirection();
            Vector3 muzzle = CannonMuzzlePosition();
            BallType type = _magazine[0];

            _flying.Add(new FlyingBall { Position = muzzle, Velocity = direction * SHOOT_SPEED, Type = type, Age = 0f });
            _trails.Add(new ShotTrail { Origin = muzzle, Direction = direction, Color = TrailColorFor(type), Age = 0f });

            AdvanceMagazine();

            //Fired, therefore felt. Nothing else in the frame moves the camera, so every wobble the player
            //sees is unambiguously their own shot.
            _camera.Shake.Kick(RECOIL_KICK);
        }

        private void AdvanceMagazine()
        {
            for (int i = 0; i < MAGAZINE_SIZE - 1; i++) _magazine[i] = _magazine[i + 1];
            _magazine[MAGAZINE_SIZE - 1] = RandomBallType();

            //Armed at one slot back, so the queue eases forward into the muzzle slot the shot just vacated
            _magazineSlide = 1f;
        }

        /// <summary>
        /// Advances the shots. A ball crosses several diameters a frame, so the step is tested as a swept
        /// segment against the cluster — a point test at the end of the frame would tunnel clean through it.
        /// A ball that touches is snapped into the lattice; the rest fly on until they age out or fall away.
        /// </summary>
        private void UpdateFlyingBalls(float elapsed)
        {
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                FlyingBall ball = _flying[i];

                Vector3 from = ball.Position;
                Vector3 to = from + ball.Velocity * elapsed;

                if (TryHitCluster(from, to, out Vector3 contact, out XZLevel hitCell))
                {
                    AttachBall(contact, hitCell, ball.Type);
                    _flying.RemoveAt(i);
                    continue;
                }

                ball.Position = to;
                ball.Velocity += new Vector3(0f, Constants.EARTH_GRAVITY * elapsed, 0f);
                ball.Age += elapsed;

                if (ball.Age > FLYING_LIFETIME || ball.Position.Y < FLYING_KILL_Y) _flying.RemoveAt(i);
                else _flying[i] = ball;
            }
        }

        /// <summary>
        /// Where along <c>from → to</c> the moving ball first touches a ball of the cluster, if it does, and
        /// which cell of the lattice that ball occupies. Solved rather than sampled — a ball crosses several
        /// diameters a frame, so a point test at the end of the step tunnels clean through the cluster.
        /// <para>
        /// It is the moment of contact that is solved for, not the closest approach: the quadratic in the step
        /// parameter whose smaller root is where the two spheres first touch. Ranking candidates by their
        /// <i>clamped</i> closest approach instead — which is what this did — is wrong in exactly the case that
        /// matters. On the frame a ball first comes within reach it is still approaching, so every candidate's
        /// closest approach lies past the end of the step and clamps to 1: the whole field ties, and the "first
        /// touched" ball falls out of the array order rather than the geometry. The shot then reports the same
        /// buried cell every time however the cluster grows around it, and once that one cell's neighbourhood
        /// is full every further shot is refused.
        /// </para>
        /// </summary>
        private bool TryHitCluster(Vector3 from, Vector3 to, out Vector3 contact, out XZLevel hitCell)
        {
            contact = to;
            hitCell = new XZLevel(-1, -1, -1);

            Vector3 segment = to - from;
            float a = segment.LengthSquared();
            if (a <= 1e-8f) return false;

            const float touchDistance = 2f * BALL_RADIUS;
            float touchSquared = touchDistance * touchDistance;

            float bestS = float.MaxValue;

            foreach (ClusterBall target in _clusterBalls)
            {
                Vector3 fromCentre = from - target.Position;

                float b = Vector3.Dot(segment, fromCentre);
                float c = fromCentre.LengthSquared() - touchSquared;

                //Already touching at the start of the step (the previous frame left them overlapping): the
                //contact is here and now, and no root of the quadratic would say so
                if (c <= 0f)
                {
                    if (0f >= bestS) continue;

                    bestS = 0f;
                    contact = from;
                    hitCell = target.Cell;
                    continue;
                }

                float discriminant = b * b - a * c;
                if (discriminant < 0f) continue; //passes by without ever touching

                float s = (-b - MathF.Sqrt(discriminant)) / a;
                if (s < 0f || s > 1f || s >= bestS) continue; //touches outside this step, or later than one already found

                bestS = s;
                contact = from + segment * s;
                hitCell = target.Cell;
            }

            return bestS <= 1f;
        }

        /// <summary>
        /// Snaps a landed ball into the lattice: the free cell touching the ball it hit that lies closest to
        /// the contact point, which is what makes it land flush in the packing instead of wherever it happened
        /// to make contact. Sticking a ball at its contact point leaves it standing off the cluster's surface,
        /// and since each new ball is then a target for the next one they chain into an icicle pointing back
        /// down the line of fire — the one thing that made this read as broken rather than merely unfinished.
        /// </summary>
        private void AttachBall(Vector3 contact, XZLevel hitCell, BallType type)
        {
            Vector3 mapContact = WorldToMap(contact);

            _map.PutBallAtClosestEmptyPositionNextTo(mapContact, hitCell, out XZLevel cell, type);

            //Nothing free touching the ball it hit. That is not an exotic case: the ball a shot reaches first
            //is on the cluster's outer face, and where that face is the field's own wall there is no cell
            //beyond it to fall into — so the pocket around an edge ball fills after a handful of shots and
            //every ball after that would be silently eaten. Widen the search by one ring: the free cells
            //touching the balls that touch the one it hit, nearest the contact point first. Local by
            //construction, so the ball never lands somewhere it could not have rolled to.
            if (cell.X < 0 && TryFindCellInSecondRing(mapContact, hitCell, out XZLevel ringCell))
                _map.PutBallAt((byte)ringCell.X, (byte)ringCell.Z, (byte)ringCell.Level, type);
            else if (cell.X < 0)
            {
#if DEBUG
                Console.WriteLine($"[shot] nothing free within two rings of {hitCell.X},{hitCell.Z},{hitCell.Level} — ball discarded");
#endif
                return;
            }

            RebuildClusterBalls();
        }

        /// <summary>
        /// The free cell nearest <paramref name="mapContact"/> among those touching a ball that itself touches
        /// <paramref name="hitCell"/> — one ring further out than
        /// <see cref="BallsMap.PutBallAtClosestEmptyPositionNextTo"/> looks.
        /// </summary>
        private bool TryFindCellInSecondRing(Vector3 mapContact, XZLevel hitCell, out XZLevel best)
        {
            best = new XZLevel(-1, -1, -1);

            StaticBall[,,] balls = _map.GetStaticBallsArray();
            XZLevel size = _map.GetStaticBallsArraySize();

            float closest = float.MaxValue;

            foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(hitCell, size))
            {
                if (balls[neighbour.X, neighbour.Z, neighbour.Level] == null) continue; //free cells were the first ring's business

                foreach (XZLevel candidate in BallsMap.GetNeighboringCells(neighbour, size))
                {
                    if (balls[candidate.X, candidate.Z, candidate.Level] != null) continue;

                    float distance = Vector3.DistanceSquared(_map.GetRealCenteredPosition(candidate), mapContact);
                    if (distance >= closest) continue;

                    closest = distance;
                    best = candidate;
                }
            }

            return best.X >= 0;
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
        /// </summary>
        private void UpdateCamera(float elapsed)
        {
            Vector3 back = _cannon.Position - _cannon.OrbitCenter;
            back.Y = 0f;
            Vector3 bearing = back == Vector3.Zero ? Vector3.Backward : Vector3.Normalize(back);

            Vector3 fieldCentre = new(_cannon.OrbitCenter.X, 0f, _cannon.OrbitCenter.Z);

            _camera.BasePosition = fieldCentre + bearing * CAMERA_DISTANCE + Vector3.Up * (_cannon.Position.Y + CAMERA_HEIGHT);
            _camera.BaseTarget = new Vector3(_cannon.OrbitCenter.X, CAMERA_TARGET_Y, _cannon.OrbitCenter.Z);
            _camera.FieldOfView = GAME_FOV;

            _camera.Update(elapsed);
        }

        protected override void Draw(GameTime gameTime)
        {
            CollectBallInstances();

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

            _sky.Draw(_camera);

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            //Stated rather than inherited: the tonemap pass leaves CullNone behind, so what the scene culls
            //would otherwise depend on what ran last in the previous frame.
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            //The city's windows keep their own rhythm off the wall clock — a city's lamps do not stop
            //because the game is paused
            _cityRenderer.CityWindowTime = _wallClock;
            _cityRenderer.Draw(_camera, _city.Buildings, _city.Buildings.Length, _sceneEffectParams);

            //The island is a solid ring, so the nearest face wins on depth and the winding is moot
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            _islandRenderer.Draw(_camera, _islandWorld, _sceneEffectParams);
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            _cannonRenderer.Draw(_camera, CannonWorld(), _sceneEffectParams);

            DrawBallsInstanced();

            //Over the opaque scene (which the depth buffer now holds, so the cluster and the gun occlude
            //them) and additive, so they glow through the glare
            DrawShotTrails();

            //The glass the cluster hangs from, last: it is translucent, so everything it should be seen
            //through has to be in the depth buffer and the frame already
            _ceilingRenderer.Draw(_camera, _ceilingWorld, _sceneEffectParams);

            ResolveSceneTarget();

            base.Draw(gameTime);
        }

        /// <summary>
        /// Gathers every ball in the frame — the cluster, the queue in the barrel and the shots in flight —
        /// into one bucket per type and LOD, each of which becomes a single instanced draw call.
        /// </summary>
        private void CollectBallInstances()
        {
            for (int i = 0; i < _ballInstanceCounts.Length; i++) _ballInstanceCounts[i] = 0;

            //Unrotated balls take a plain translation for their world matrix — composing Identity with a
            //translation was a full 4×4 multiply per ball per frame for a result equal to the translation
            foreach (ClusterBall ball in _clusterBalls)
                CollectBallInstance(ball.Position, Matrix.CreateTranslation(ball.Position), ball.Type, ball.Occlusion);

            foreach (FlyingBall ball in _flying)
                CollectBallInstance(ball.Position, Matrix.CreateTranslation(ball.Position), ball.Type, new Vector4(0f, 0f, 0f, 1f));

            CollectMagazineBalls();
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
            Vector3 front = CannonMuzzlePosition();

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
                CollectBallInstance(position, world, _magazine[i], new Vector4(0f, 0f, 0f, 1f));
            }
        }

        private void CollectBallInstance(Vector3 position, Matrix world, BallType type, Vector4 occlusion)
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

            bucket[count] = new ModelInstance(world, occlusion);
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

        private Matrix CannonWorld() => CannonOrientation() * Matrix.CreateTranslation(_cannon.Position);

        #endregion

        private static BallType RandomBallType() => (BallType)RANDOM.Next((int)BallType.Type1, (int)BallType.Type8 + 1);

        protected override void UnloadContent()
        {
            _sceneTarget?.Dispose();
            _glareBright?.Dispose();
            _glareStreak?.Dispose();
            _fullScreenQuad?.Dispose();
            _shotTrailVertexBuffer?.Dispose();
            _shotTrailIndexBuffer?.Dispose();

            if (_ballMeshes != null) foreach (SphereMesh mesh in _ballMeshes) mesh?.Dispose();
            if (_ballRenderers != null) foreach (InstancedModelRenderer renderer in _ballRenderers) renderer?.Dispose();

            _cannonMesh?.Dispose();
            _cannonRenderer?.Dispose();
            _unitBox?.Dispose();
            _cityRenderer?.Dispose();
            _islandMesh?.Dispose();
            _islandRenderer?.Dispose();
            _ceilingMesh?.Dispose();
            _ceilingRenderer?.Dispose();

            base.UnloadContent();
        }
    }
}
