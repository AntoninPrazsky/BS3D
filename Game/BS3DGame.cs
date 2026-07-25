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
    /// files, no scene switching and no editor plumbing: a shot ball flies ballistically and sticks where
    /// it touches the cluster. That is enough to carry the thing this build exists to establish — balls
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

        private static readonly int GLARE_DOWNSAMPLE = 4;
        private static readonly float GLARE_THRESHOLD = 0.55f;
        private static readonly float GLARE_STREAK_LENGTH = 34f;
        private static readonly float GLARE_STREAK_FALLOFF = 3.2f;
        private static readonly float GLARE_INTENSITY = 0.9f;

        #endregion

        #region Scene

        //Dome 13 is the violet/teal dusk. The neon city reads best under a dark sky — the facades stay dark
        //under any dome, so a bright one only fights the neon it is meant to set off.
        private const int SKY_DOME = 13;

        private SkyDome _sky;
        private Vector3 _zenithLinear = Vector3.One;
        private Vector3 _horizonLinear = Vector3.One;

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

        //The round stone island the gun stands on: the funnel's rim radius out to the disc's, then a hard
        //vertical edge the city falls away past. No drain and no physics floor here yet — nothing falls.
        private static readonly float ISLAND_Y = -8.5f;
        private static readonly float ISLAND_INNER_RADIUS = 14f;
        private static readonly float ISLAND_RADIUS = 26f;
        private static readonly float ISLAND_EDGE_HEIGHT = 5f;
        private static readonly int ISLAND_SEGMENTS = 64;
        private static readonly Vector3 ISLAND_COLOR = new(0.58f, 0.56f, 0.54f);

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

        /// <summary>A ball that is part of the hanging structure: where it is, what colour, how boxed in.</summary>
        private struct ClusterBall
        {
            public Vector3 Position;
            public BallType Type;
            public Vector4 Occlusion;
        }

        private readonly List<ClusterBall> _clusterBalls = new();

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
            _graphics.PreferredBackBufferWidth = _fullscreen ? GraphicsDevice?.DisplayMode.Width ?? WINDOW_WIDTH : WINDOW_WIDTH;
            _graphics.PreferredBackBufferHeight = _fullscreen ? GraphicsDevice?.DisplayMode.Height ?? WINDOW_HEIGHT : WINDOW_HEIGHT;
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

            _sky = new SkyDome(Content.Load<Model>("Skyes/SkyDome" + SKY_DOME), GraphicsDevice, linearVertexColors: true);

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

            //No detail texture: without one the renderer falls through to the plain technique, which is
            //what is wanted here (plain stone, its sheen the sky reflection). Adding relief settings
            //without a texture would be silently dead code.
            _islandMesh = new DiscMesh(GraphicsDevice, ISLAND_INNER_RADIUS, ISLAND_RADIUS, ISLAND_EDGE_HEIGHT, ISLAND_SEGMENTS);
            _islandRenderer = new InstancedModelRenderer(GraphicsDevice, _islandMesh, ISLAND_COLOR, _instancingEffect)
            {
                //A floor is seen at a grazing angle everywhere except right under your feet, which is
                //exactly where Fresnel puts the sky reflection at full strength.
                SpecularAmbientStrength = 0.4f
            };
            _islandWorld = Matrix.CreateTranslation(0f, ISLAND_Y, 0f);
        }

        /// <summary>
        /// Fills the hanging cluster: a <see cref="BallsMap"/> carved to a taper, centred on the origin, then
        /// flattened into a list of world positions. The map is what a level will hand over later; the list is
        /// what the frame actually draws and what a shot is tested against, so the two are built together here.
        /// </summary>
        private void BuildCluster()
        {
            BallsMap map = new(CLUSTER_X, CLUSTER_Z, CLUSTER_LEVELS);

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

                        map.PutBallAt(x, z, level, RandomBallType());
                    }
            }

            map.Center();

            //Neighbour-based ambient occlusion, computed once: a ball buried in the mass is darker than one
            //on the outside, which is what makes the cluster read as one body instead of a heap of spheres.
            StaticBall[,,] balls = map.GetStaticBallsArray();
            XZLevel size = map.GetStaticBallsArraySize();

            for (int level = 0; level < CLUSTER_LEVELS; level++)
                for (int x = 0; x < CLUSTER_X; x++)
                    for (int z = 0; z < CLUSTER_Z; z++)
                    {
                        StaticBall ball = balls[x, z, level];
                        if (ball == null) continue;

                        int occluders = BallsMap.CountOccupiedNeighbors(balls, new XZLevel(x, z, level), size, out Vector3 direction);
                        float open = 1f - BALL_OCCLUSION_STRENGTH * Math.Min(occluders, MAX_BALL_OCCLUDERS) / MAX_BALL_OCCLUDERS;

                        _clusterBalls.Add(new ClusterBall
                        {
                            Position = ball.Position,
                            Type = ball.Type,
                            Occlusion = new Vector4(direction.X, direction.Y, direction.Z, open)
                        });
                    }
        }

        /// <summary>
        /// Every renderer that takes its lighting from the sky dome.
        /// </summary>
        private IEnumerable<InstancedModelRenderer> SkyLitRenderers()
        {
            foreach (InstancedModelRenderer ballRenderer in _ballRenderers) yield return ballRenderer;

            yield return _cannonRenderer;
            yield return _cityRenderer;
            yield return _islandRenderer;
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
                UpdateInput(gameTime, edgeInputAllowed);
                UpdateAim(gameTime);
            }
            else _mouseAimInitialized = false;

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

        private void UpdateInput(GameTime gameTime, bool edgeInputAllowed)
        {
            KeyboardState keyboard = Keyboard.GetState();
            GamePadState pad = GamePad.GetState(PlayerIndex.One);

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
        private void UpdateAim(GameTime gameTime)
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

                if (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released && _wasActive)
                    Shoot();
            }

            Mouse.SetPosition(centreX, centreY);
            _mouseAimInitialized = true;

            //Read back the recentred position, or the next frame's delta would be measured against the old one
            _previousMouse = Mouse.GetState();

            GamePadState pad = GamePad.GetState(PlayerIndex.One);
            if (pad.IsConnected)
            {
                if (pad.ThumbSticks.Right.LengthSquared() > 0f)
                    _cannon.Aim(new Vector2(pad.ThumbSticks.Right.Y, -pad.ThumbSticks.Right.X) * PAD_AIM_RATE, gameTime);

                if (pad.Triggers.Right > 0.5f && _padTriggerReleased) { Shoot(); _padTriggerReleased = false; }
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
        /// A ball that touches sticks where it touched; the rest fly on until they age out or fall away.
        /// </summary>
        private void UpdateFlyingBalls(float elapsed)
        {
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                FlyingBall ball = _flying[i];

                Vector3 from = ball.Position;
                Vector3 to = from + ball.Velocity * elapsed;

                if (TryHitCluster(from, to, out Vector3 contact))
                {
                    StickToCluster(contact, ball.Type);
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
        /// Where along <c>from → to</c> the moving ball first touches a ball of the cluster, if it does.
        /// Solved rather than sampled: the closest approach of the segment to each cluster centre, backed
        /// off along the segment to the point where the two spheres are exactly touching.
        /// </summary>
        private bool TryHitCluster(Vector3 from, Vector3 to, out Vector3 contact)
        {
            contact = to;

            Vector3 segment = to - from;
            float segmentLengthSquared = segment.LengthSquared();
            if (segmentLengthSquared <= 1e-8f) return false;

            const float touchDistance = 2f * BALL_RADIUS;
            float touchSquared = touchDistance * touchDistance;

            float bestT = float.MaxValue;
            bool found = false;

            foreach (ClusterBall target in _clusterBalls)
            {
                Vector3 toCentre = target.Position - from;
                float t = MathHelper.Clamp(Vector3.Dot(toCentre, segment) / segmentLengthSquared, 0f, 1f);
                Vector3 closest = from + segment * t;

                float gapSquared = (target.Position - closest).LengthSquared();
                if (gapSquared > touchSquared) continue;

                if (t >= bestT) continue;

                bestT = t;
                found = true;

                //Step back from the closest approach to the moment of contact along the segment
                float backOff = MathF.Sqrt(MathF.Max(0f, touchSquared - gapSquared));
                float segmentLength = MathF.Sqrt(segmentLengthSquared);
                contact = closest - segment / segmentLength * MathF.Min(backOff, segmentLength * t);
            }

            return found;
        }

        /// <summary>
        /// Adds a landed ball to the structure. Unoccluded — it is on the outside of the mass by
        /// definition, and its neighbours' occlusion is left alone: snapping the new ball into a grid cell
        /// and re-deriving the neighbourhood is the game's rules, which this build does not have yet.
        /// </summary>
        private void StickToCluster(Vector3 position, BallType type) =>
            _clusterBalls.Add(new ClusterBall { Position = position, Type = type, Occlusion = new Vector4(0f, 0f, 0f, 1f) });

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

            foreach (ClusterBall ball in _clusterBalls)
                CollectBallInstance(ball.Position, Matrix.Identity, ball.Type, ball.Occlusion);

            foreach (FlyingBall ball in _flying)
                CollectBallInstance(ball.Position, Matrix.Identity, ball.Type, new Vector4(0f, 0f, 0f, 1f));

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
            Matrix orientation = CannonOrientation();

            for (int i = 0; i < MAGAZINE_SIZE; i++)
            {
                Vector3 position = front - direction * ((i + _magazineSlide) * MAGAZINE_SPACING);
                CollectBallInstance(position, orientation, _magazine[i], new Vector4(0f, 0f, 0f, 1f));
            }
        }

        private void CollectBallInstance(Vector3 position, Matrix orientation, BallType type, Vector4 occlusion)
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

            bucket[count] = new ModelInstance(orientation * Matrix.CreateTranslation(position), occlusion);
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

            _shotTrailEffect.Parameters["View"].SetValue(_camera.View);
            _shotTrailEffect.Parameters["Projection"].SetValue(_camera.Projection);
            _shotTrailEffect.Parameters["CameraPosition"].SetValue(_camera.Position);
            _shotTrailEffect.Parameters["TrailHeadWidth"].SetValue(TRAIL_LEAD_WIDTH);
            _shotTrailEffect.Parameters["TrailTailWidth"].SetValue(TRAIL_MUZZLE_WIDTH);

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

                _shotTrailEffect.Parameters["TrailHead"].SetValue(trail.Origin + trail.Direction * TRAIL_LENGTH);
                _shotTrailEffect.Parameters["TrailTail"].SetValue(trail.Origin);
                _shotTrailEffect.Parameters["TrailColor"].SetValue(trail.Color);
                _shotTrailEffect.Parameters["TrailAlpha"].SetValue(1f - t * t);

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
            //would only earn its memory with supersampling off — and it antialiases no shading either way.
            _sceneTarget = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.HdrBlendable,
                DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.DiscardContents);

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

        /// <summary>
        /// Box-filters the supersampled HDR scene onto the back buffer, tonemaps it from linear radiance
        /// into display range and encodes it to sRGB. The frame's one and only exit from linear light.
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

            //There is no water in this scene to get under, so the underwater murk is a no-op
            _tonemapEffect.Parameters["UnderwaterAmount"].SetValue(0f);

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

            base.UnloadContent();
        }
    }
}
