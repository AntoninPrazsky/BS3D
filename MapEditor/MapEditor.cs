using MapEditor.GUI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Input;
using Prazsky.BS3D.Levels;
using Prazsky.Core;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
//PropertyGrid, Label and HorizontalAlignment also exist in System.Windows.Forms (used here for the file
//dialogs), so alias Myra's (VerticalAlignment has no WinForms twin, so it needs none)
using MyraPropertyGrid = Myra.Graphics2D.UI.Properties.PropertyGrid;
using MyraLabel = Myra.Graphics2D.UI.Label;
using MyraHAlign = Myra.Graphics2D.UI.HorizontalAlignment;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using mgKeys = Microsoft.Xna.Framework.Input.Keys;

namespace MapEditor
{
    public class MapEditor : Game
    {
        private BasicCamera3D Camera3D;

        /// <summary>Optional map or level file loaded once at startup (first command-line argument).</summary>
        public string StartupFilePath { get; set; }

        //A little air around the play field, so that it does not touch the edges of the screen
        private const float VIEW_MARGIN = 1.1f;

        #region Ball rendering

        //The balls are drawn exactly as the game draws them, so that a map looks here the way it will play:
        //generated sphere LODs picked by camera distance, instanced through the game's own shader.
        //See the "Ball rendering" section in CLAUDE.md
        private static readonly int[,] BALL_LOD_RESOLUTIONS = { { 32, 24 }, { 16, 12 }, { 10, 7 } };
        private static readonly float[] BALL_LOD_DISTANCES = { 15f, 30f };
        private static readonly int BALL_LOD_COUNT = 3;
        private static readonly int BALL_TYPE_COUNT = (int)BallType.Type8;   //red/green/blue/white + cyan/magenta/yellow/black
        private static readonly int BALL_PATTERN_GORES = 5;

        private static readonly float BALL_OCCLUSION_STRENGTH = 0.55f;
        private static readonly int MAX_BALL_OCCLUDERS = 12;

        private Effect _instancingEffect;
        private SphereMesh[] _ballMeshes;
        private InstancedModelRenderer[] _ballRenderers;

        //One instance bucket per ball type and LOD level; each bucket becomes a single instanced draw call
        private readonly ModelInstance[][] _ballInstances = new ModelInstance[BALL_TYPE_COUNT * BALL_LOD_COUNT][];
        private readonly int[] _ballInstanceCounts = new int[BALL_TYPE_COUNT * BALL_LOD_COUNT];

        private SkyDome _sky;

        //The game ships eighteen sky domes and starts on the first one
        private static readonly int SKY_DOME_COUNT = 18;
        private int _skyDomeNumber = 1;

        #endregion Ball rendering

        private BallsMap _map;
        private Selector _selector;
        private AABB _aabb;
        private AxisGizmo _axisGizmo;

        private CameraInputHelper _cih;
        private ButtonAction[] _actions;

        #region Graphics

        private int _windowWidth;
        private int _windowHeight;

        private GraphicsDeviceManager _graphics;
        private bool _windowed;

        public InfoRenderer Info { private set; get; }

        private static readonly int MSAA_SAMPLES = 8;

        #endregion Graphics

        #region Post-processing

        //The scene is drawn in linear radiance into a supersampled HDR target and resolved to the back buffer
        //in one pass — box filter, glare, exposure, the ACES curve, then the sRGB encode — exactly as the game
        //does it, so a map looks here the way it will play. The exposure, clear color and every glare figure
        //are the game's own, so the two agree. See the "Color management" and "Ball rendering" sections in
        //CLAUDE.md, and the matching code in Testbed.cs.
        private const int SUPERSAMPLE_FACTOR = 2;
        private const float DEFAULT_EXPOSURE = 1.1f;

        private static readonly int GLARE_DOWNSAMPLE = 4;
        private static readonly float GLARE_THRESHOLD = 0.38f;
        private static readonly float GLARE_STREAK_LENGTH = 34f;
        private static readonly float GLARE_STREAK_FALLOFF = 3.2f;
        private static readonly float GLARE_INTENSITY = 1.3f;

        private RenderTarget2D _sceneTarget;
        private RenderTarget2D _glareBright;
        private RenderTarget2D _glareStreak;
        private Effect _tonemapEffect;
        private Effect _glareEffect;
        private VertexBuffer _fullScreenQuad;

        //Cached in LoadContent: the resolve runs every frame, and the by-name indexer is a linear scan
        private EffectTechnique _glareBrightPassTechnique;
        private EffectTechnique _glareStreakTechnique;
        private EffectParameter _glareSourceTextureParam;
        private EffectParameter _glareSourceTexelSizeParam;
        private EffectParameter _tonemapGlareTextureParam;
        private EffectParameter _tonemapSceneTextureParam;
        private EffectParameter _tonemapSourceTexelSizeParam;

        #endregion Post-processing

        #region Scenes

        //The switchable environment backdrops, shared with the game so a scene looks here the way it will
        //play. The five self-lit backdrops (sea/savanna/desert/mountain/meadow, with the savanna's acacias,
        //the shared savanna/desert birds, the mountain's snow and the sea's spray) live in the shared
        //SceneRenderer; the city is its buildings drawn through the shared InstancedModel city technique, lit
        //by the sky rig like the balls. V cycles the scenes (the game uses NumPad2, which the editor spends on
        //ball types). There is no arena platform here — the AABB marks the field instead — and no clouds, so
        //the scenes get full sun with no cloud shadow.
        private SceneRenderer _sceneRenderer;
        private SceneKind _scene = SceneKind.City;

        private City _city;
        private BoxMesh _unitBox;
        private InstancedModelRenderer _cityRenderer;
        //Not readonly: a loaded level (bs3d-level file) replaces it with the level's city config
        private CitySceneConfig _cityConfig = new();

        //A level dropped or opened is parsed off the render thread (like a map file), but its scene/sky/city
        //application touches GPU resources (Content.Load, buffer rebuilds, a new City), so the parsed level is
        //stashed here and applied on the main thread in Update. See ApplyPendingLevel.
        private Level _pendingLevel;
        private readonly object _pendingLevelLock = new();

        //Myra in-engine GUI (issue #45): a live scene-config editor (issue #33). The PropertyGrid reflects over
        //the active scene's SceneConfig POCO (issue #44); editing a value re-applies it, so the backdrop updates
        //in place. G toggles the panel. Myra's default stylesheet is embedded, so no content pipeline is needed.
        private Desktop _desktop;
        private MyraPropertyGrid _sceneConfigGrid;
        private MyraLabel _sceneConfigHeader;
        private Widget _sceneConfigPanel;
        private bool _sceneConfigPanelVisible = true;

        //Wall-clock seconds the scene motion runs off (waves, wind, birds, snow), so the environment keeps
        //moving the way it does in the game instead of freezing
        private float _sceneSeconds;

        //Cached sky palette in linear radiance, handed to the scenes each frame (set in ApplySkyLighting)
        private Vector3 _zenithLinear = Vector3.One;
        private Vector3 _horizonLinear = Vector3.One;

        //Near-mirrors of the game's scene-lighting constants: the sun radiance and tint give the scenes their
        //warm sun, and the ambient is the city's. The shaders — the actual look of every scene — are the
        //shared source. The clearing radius no longer mirrors anything: the game's arena became the round
        //stone island (ARENA_DISC_RADIUS/ISLAND_RADIUS = 26), so this 60 keeps the editor's towers ~2.3× as
        //far from the field as they stand in play. Whether to close that to 26 is a look decision, not a sync.
        private const float ARENA_HALF_EXTENT = 60f;
        //Window brightness (day + neon) now lives in _cityConfig.WindowBrightness / _cityConfig.NeonLook.WindowBrightness.
        private const float CITY_SPECULAR_AMBIENT = 0.07f;
        private const float SCENE_SKY_TINT = 0.5f;
        private const float SCENE_AMBIENT_INTENSITY = 0.25f;
        private static readonly Vector3 SCENE_SUN_RADIANCE = new(1.7f, 1.66f, 1.55f);
        private static readonly Vector3 SUN_DIRECTION = -DefaultLighting.Light0Direction;

        private readonly BasicEffectParams _sceneEffectParams = new(Vector3.One * SCENE_AMBIENT_INTENSITY, Vector3.Zero, 0f, Vector3.Zero);

        #endregion Scenes

        public MapEditor(bool windowed = true, int windowWidth = 1280, int windowHeight = 800)
        {
            _windowed = windowed;

            _graphics = new GraphicsDeviceManager(this);

            _graphics.PreparingDeviceSettings += _graphics_PreparingDeviceSettings;
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
			new Task(() => { DeserializeMapFromJsonFile(e.Files[0]); }).Start();
			
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            Camera3D.AspectRatio = GraphicsDevice.Viewport.AspectRatio;

            EnsureSceneTarget(); //The back buffer just changed size, so the scene target has to follow

            Info.RecomputeScale();
        }

        protected override void Initialize()
        {
            IsMouseVisible = true;

            Camera3D = new BasicCamera3D(new Vector3(5f, 3.2f, 20f), GraphicsDevice.Viewport.AspectRatio);
            Camera3D.SetCircularMovementProperties(15f);
            Info = new InfoRenderer(this, "Content/Fonts/segoeui") { DrawOrder = int.MaxValue };
            Components.Add(Info);

            _cih = new CameraInputHelper(Camera3D, this);

            #region Controls

            _actions = new ButtonAction[]
            {
                new(mgKeys.Up, Buttons.DPadUp, () => _selector.Move(Vector3.Forward), "Move selector forward"),
                new(mgKeys.Down, Buttons.DPadDown,() => _selector.Move(Vector3.Backward), "Move selector backward"),
                new(mgKeys.Left, Buttons.DPadLeft,() => _selector.Move(Vector3.Left), "Move selector left"),
                new(mgKeys.Right, Buttons.DPadRight,() => _selector.Move(Vector3.Right), "Move selector right"),
                new(mgKeys.PageUp, Buttons.RightShoulder,() => _selector.Move(Vector3.Up), "Move selector up"),
                new(mgKeys.PageDown, Buttons.LeftShoulder,() => _selector.Move(Vector3.Down), "Move selector down"),

                new(mgKeys.Space, Buttons.A,() => _selector.PutBall(), "Put ball"),
                new(mgKeys.Delete, Buttons.B,() => _selector.RemoveBall(), "Remove ball"),

                new(mgKeys.NumPad1,() => _selector.ChangeBallType(BallType.Type1), "Change ball type to 1 (red)"),
                new(mgKeys.NumPad2,() => _selector.ChangeBallType(BallType.Type2), "Change ball type to 2 (green)"),
                new(mgKeys.NumPad3,() => _selector.ChangeBallType(BallType.Type3), "Change ball type to 3 (blue)"),
				new(mgKeys.NumPad4,() => _selector.ChangeBallType(BallType.Type4), "Change ball type to 4 (white)"),
                new(mgKeys.NumPad5,() => _selector.ChangeBallType(BallType.Type5), "Change ball type to 5 (cyan)"),
                new(mgKeys.NumPad6,() => _selector.ChangeBallType(BallType.Type6), "Change ball type to 6 (magenta)"),
                new(mgKeys.NumPad7,() => _selector.ChangeBallType(BallType.Type7), "Change ball type to 7 (yellow)"),
                new(mgKeys.NumPad8,() => _selector.ChangeBallType(BallType.Type8), "Change ball type to 8 (black)"),

				new(mgKeys.Escape, Buttons.Back, Exit, "Exit"),
				new(mgKeys.F11, () => SetGraphics(_graphics.IsFullScreen), "Fullscreen/windowed"),
				new(mgKeys.F12,() => Info.Visible = ! Info.Visible, "Hide/show text overlay"),

                new(mgKeys.N, Buttons.X, () => new Task(FullMapTest).Start(), "Fill entire map with balls"),
                new(mgKeys.M, () => _map.Clear(), "Clear entire map"),

                new(mgKeys.D1,() => CenterViewOn(Vector3.Forward), "Forward view"),
                new(mgKeys.D2, () => CenterViewOn(Vector3.Backward), "Backward view"),
                new(mgKeys.D3, () => CenterViewOn(Vector3.Left), "Left view"),
                new(mgKeys.D4, () => CenterViewOn(Vector3.Right), "Right view"),
                new(mgKeys.D5, () => CenterViewOn(Vector3.Up), "Up view"),
                new(mgKeys.D6, () => CenterViewOn(Vector3.Down), "Down view"),

                new(mgKeys.R, () => _cih.RestartCamera(), "Restart camera"),
                //Not S, W, A, D, Q or E: those move the camera (see CameraInputHelper)
                new(mgKeys.B, SwitchSkyDome, "Switch sky dome (backdrop)"),
                new(mgKeys.V, SwitchScene, "Switch scene (city/sea/savanna/desert/mountain/meadow/neon)"),
                new(mgKeys.G, ToggleSceneConfigPanel, "Show/hide scene-config editor"),

                new(mgKeys.F1, SaveJson, "Save map to file (JSON)"),
                new(mgKeys.F2, LoadJson, "Load map or level from file (JSON)"),
                new(mgKeys.F3, NewMap, "New map (choose play field size)"),
                new(mgKeys.F4, SaveLevel, "Save level: map + current scene + sky (JSON)"),
            };

            StringBuilder builder = new();
            foreach (var act in _actions) builder.Append(string.Format("{0,-9} {1}\n", act.Key.ToString(), act.Description));

            Info.HintText = builder.ToString();

            #endregion

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

            //Fixed for the whole run, so they are set exactly once (a parameter's value persists on the effect)
            _glareEffect.Parameters["GlareThreshold"].SetValue(GLARE_THRESHOLD);
            _glareEffect.Parameters["StreakLength"].SetValue(GLARE_STREAK_LENGTH);
            _glareEffect.Parameters["StreakFalloff"].SetValue(GLARE_STREAK_FALLOFF);
            _tonemapEffect.Parameters["GlareIntensity"].SetValue(GLARE_INTENSITY);
            _tonemapEffect.Parameters["SupersampleFactor"].SetValue(SUPERSAMPLE_FACTOR);
            _tonemapEffect.Parameters["Exposure"].SetValue(DEFAULT_EXPOSURE);

            CreateFullScreenQuad();

            _ballMeshes = new SphereMesh[BALL_LOD_COUNT];
            _ballRenderers = new InstancedModelRenderer[BALL_LOD_COUNT];

            for (int lod = 0; lod < BALL_LOD_COUNT; lod++)
            {
                //Constants.HALF is the ball radius the physics builder uses in the game; the editor has no physics to ask
                _ballMeshes[lod] = new SphereMesh(GraphicsDevice, Constants.HALF, BALL_LOD_RESOLUTIONS[lod, 0], BALL_LOD_RESOLUTIONS[lod, 1]);
                _ballRenderers[lod] = new InstancedModelRenderer(GraphicsDevice, _ballMeshes[lod], Vector3.One, _instancingEffect)
                {
                    PatternGoreCount = BALL_PATTERN_GORES
                };
            }

            //linearVertexColors: the dome is drawn through BasicEffect into the linear HDR target, so its
            //baked gradient has to be converted from sRGB once at load or the tonemapper reads a gradient of
            //display values as radiance. The game does the same; the editor used to skip it, drawing in gamma.
            _sky = new SkyDome(Content.Load<Model>("Skyes/SkyDome" + _skyDomeNumber), GraphicsDevice, linearVertexColors: true);

			//10 levels for the initial ball layout plus empty levels at the bottom for the structure to grow into
			_map = new BallsMap(10, 10, 15);
            _selector = new Selector(Content, _map, Camera3D);
            _aabb = new AABB(GraphicsDevice);
            _aabb.FitToMap(_map);
            _axisGizmo = new AxisGizmo(GraphicsDevice, Content);

            //The switchable backdrops, drawn exactly as the game draws them. The self-lit ones live in the
            //shared SceneRenderer; the city is one instanced box mesh under the shared city technique, and it
            //takes part in the sky lighting below like the balls do.
            //SupersampleFactor: the space scene sizes its stars in OUTPUT pixels rather than in texels, so it
            //has to be told the factor — sized in texels a star would come out four times dimmer at 2x
            _sceneRenderer = new SceneRenderer(GraphicsDevice, Content) { SupersampleFactor = SUPERSAMPLE_FACTOR };
            _unitBox = new BoxMesh(GraphicsDevice, 1f, 1f, 1f);
            _city = new City(seed: 20260720, arenaHalfExtent: ARENA_HALF_EXTENT, config: _cityConfig);
            _cityRenderer = new InstancedModelRenderer(GraphicsDevice, _unitBox, Vector3.One, _instancingEffect)
            {
                CityWindowBrightness = _cityConfig.WindowBrightness,
                CityConfig = _cityConfig,
                SpecularAmbientStrength = CITY_SPECULAR_AMBIENT
            };

            ApplySkyLighting();

            EnsureSceneTarget();

            BuildSceneConfigPanel();

            //Load a map or level handed on the command line (a level stashes to _pendingLevel and lands next Update)
            if (!string.IsNullOrEmpty(StartupFilePath) && File.Exists(StartupFilePath))
                DeserializeMapFromJsonFile(StartupFilePath);
        }

        /// <summary>
        /// Cycles the environment backdrop the map is previewed against (the game's NumPad2), so a map can be
        /// checked against every scene it might play in.
        /// </summary>
        private void SwitchScene()
        {
            _scene = (SceneKind)(((int)_scene + 1) % 7);
            Info.CustomText = $"Scene: {_scene}";
            RebindSceneConfigGrid();
        }

        /// <summary>
        /// Builds the Myra scene-config editor: a right-docked panel with a header and a scrollable PropertyGrid
        /// that reflects over the active scene's config. Editing a value re-applies the config live.
        /// </summary>
        private void BuildSceneConfigPanel()
        {
            MyraEnvironment.Game = this;
            _desktop = new Desktop();

            _sceneConfigGrid = new MyraPropertyGrid { IgnoreCollections = true };
            _sceneConfigGrid.PropertyChanged += (s, e) => OnSceneConfigEdited();

            var scroll = new ScrollViewer
            {
                Content = _sceneConfigGrid,
                HorizontalAlignment = MyraHAlign.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            _sceneConfigHeader = new MyraLabel { Text = "Scene", Wrap = true, Padding = new Thickness(6, 6, 6, 8) };

            var layout = new Grid { Padding = new Thickness(4), Background = new SolidBrush(new Color(12, 12, 18, 214)) };
            layout.RowsProportions.Add(new Proportion(ProportionType.Auto));
            layout.RowsProportions.Add(new Proportion(ProportionType.Fill));
            Grid.SetRow(_sceneConfigHeader, 0);
            Grid.SetRow(scroll, 1);
            layout.Widgets.Add(_sceneConfigHeader);
            layout.Widgets.Add(scroll);

            layout.HorizontalAlignment = MyraHAlign.Right;
            layout.VerticalAlignment = VerticalAlignment.Stretch;
            layout.Width = 360;

            _sceneConfigPanel = layout;
            _desktop.Root = _sceneConfigPanel;

            RebindSceneConfigGrid();
        }

        /// <summary>Points the PropertyGrid at the current scene's config (the editor's CitySceneConfig for the city).</summary>
        private void RebindSceneConfigGrid()
        {
            if (_sceneConfigGrid == null) return;

            bool isCity = _scene == SceneKind.City || _scene == SceneKind.NeonCity;
            if (isCity) _cityConfig.Neon = _scene == SceneKind.NeonCity; //show the config's Neon matching the current view

            _sceneConfigGrid.Object = isCity ? _cityConfig : _sceneRenderer.GetSceneConfig(_scene);
            _sceneConfigHeader.Text = $"{_scene}  —  edit to preview live  (G: hide)";
        }

        /// <summary>Re-applies the edited scene config so the backdrop updates in place.</summary>
        private void OnSceneConfigEdited()
        {
            switch (_sceneConfigGrid.Object)
            {
                case CitySceneConfig city:
                    _city = new City(seed: 20260720, arenaHalfExtent: ARENA_HALF_EXTENT, config: city);
                    _cityRenderer.CityConfig = city;
                    break;
                case SceneConfig sceneConfig:
                    _sceneRenderer.Apply(sceneConfig);
                    break;
            }
        }

        private void ToggleSceneConfigPanel()
        {
            _sceneConfigPanelVisible = !_sceneConfigPanelVisible;
            _sceneConfigPanel.Visible = _sceneConfigPanelVisible;
        }

        /// <summary>
        /// Moves on to the next sky dome, which changes the lighting of the whole scene with it.
        /// </summary>
        private void SwitchSkyDome() => SetSkyDome(_skyDomeNumber % SKY_DOME_COUNT + 1);

        /// <summary>Loads sky dome <paramref name="number"/> (1–18) and relights the scene from it.</summary>
        private void SetSkyDome(int number)
        {
            _skyDomeNumber = number;
            _sky.SkyDomeModel = Content.Load<Model>("Skyes/SkyDome" + _skyDomeNumber);

            ApplySkyLighting();

            Info.CustomText = $"Sky dome {_skyDomeNumber}";
        }

        /// <summary>
        /// Derives the ball lighting from the sky dome exactly as the game does: hemisphere ambient (zenith
        /// color from above, horizon color from below) plus a light rig tinted by the same palette.
        /// </summary>
        private void ApplySkyLighting()
        {
            //The palette is read off the dome's vertex colors, so it arrives sRGB-encoded. Everything below
            //scales, tints and lerps it, and none of that means anything until it is radiance — scaling an
            //sRGB value by 1.3 does not make 1.3 times the light — so it is decoded to linear first, exactly
            //as the game's ApplySkyLighting does. The renderer works in linear now (LinearLightRig true), and
            //SkyColor/GroundColor hold linear values.
            _zenithLinear = ColorSpace.SrgbToLinear(_sky.ZenithColor);
            _horizonLinear = ColorSpace.SrgbToLinear(_sky.HorizonColor);

            //Key/fill lights take on the horizon color, the back light the zenith, so the whole rig follows
            //the mood of the sky (the game calls this figure SKY_TINT_STRENGTH; it is the same 0.5)
            Vector3 keyTint = Vector3.Lerp(Vector3.One, _horizonLinear, SCENE_SKY_TINT);
            Vector3 backTint = Vector3.Lerp(Vector3.One, _zenithLinear, SCENE_SKY_TINT);
            Vector3 skyAmbient = _zenithLinear * 1.3f;
            Vector3 groundAmbient = _horizonLinear * 0.75f; //Bounce light from below is dimmer than the sky above

            //The space scene states its own rig instead of deriving one from a dome it never draws, so the
            //editor has to honour it too — otherwise a space level would draw the right sky and light its balls
            //by the wrong sun, which is the one thing this editor exists to prevent. It is reachable here
            //despite V cycling only the first seven scenes: loading a LEVEL sets _scene from the level's own
            //scene kind, so a space level opened with F1 or dropped on the window lands in it.
            if (_sceneRenderer != null && _sceneRenderer.TryGetLightRig(_scene, out SceneLightRig rig))
            {
                skyAmbient = rig.SkyAmbient;
                groundAmbient = rig.GroundAmbient;
                keyTint = rig.KeyTint;
                backTint = rig.BackTint;
            }

            //The balls and the city are lit the same way — the city takes part in the sky rig like every
            //other instanced object (its facades are dark, but its specular ambient reads the sky)
            foreach (InstancedModelRenderer renderer in _ballRenderers) ApplySkyLightingTo(renderer, skyAmbient, groundAmbient, keyTint, backTint);
            if (_cityRenderer != null) ApplySkyLightingTo(_cityRenderer, skyAmbient, groundAmbient, keyTint, backTint);
        }

        private void ApplySkyLightingTo(InstancedModelRenderer renderer, Vector3 skyAmbient, Vector3 groundAmbient, Vector3 keyTint, Vector3 backTint)
        {
            renderer.LinearLightRig = true;
            renderer.SkyColor = skyAmbient;
            renderer.GroundColor = groundAmbient;
            renderer.KeyLightPosition = -DefaultLighting.Light0Direction * 40f;
            renderer.SetLightTint(keyTint, backTint);
        }

        /// <summary>
        /// Gathers every ball of the map into the per-type-and-LOD buckets, each of which becomes one instanced
        /// draw call. The editor map is static, so unlike the game there is nothing to ease: the occlusion is
        /// simply what the grid says it is.
        /// </summary>
        private void CollectBallInstances()
        {
            for (int i = 0; i < _ballInstanceCounts.Length; i++) _ballInstanceCounts[i] = 0;

            if (_map == null) return;

            StaticBall[,,] balls = _map.GetStaticBallsArray();
            XZLevel size = _map.GetStaticBallsArraySize();

            for (byte level = 0; level < size.Level; level++)
                for (byte x = 0; x < size.X; x++)
                    for (byte z = 0; z < size.Z; z++)
                    {
                        StaticBall ball = balls[x, z, level];
                        if (ball == null) continue;

                        int occluders = BallsMap.CountOccupiedNeighbors(balls, new XZLevel(x, z, level), size, out Vector3 occlusionSum);

                        CollectBallInstance(ball, new Vector4(
                            occlusionSum.X / MAX_BALL_OCCLUDERS,
                            occlusionSum.Y / MAX_BALL_OCCLUDERS,
                            occlusionSum.Z / MAX_BALL_OCCLUDERS,
                            1f - BALL_OCCLUSION_STRENGTH * occluders / MAX_BALL_OCCLUDERS));
                    }
        }

        private void CollectBallInstance(StaticBall ball, Vector4 occlusionData)
        {
            int typeIndex = (int)ball.Type - 1;
            if (typeIndex < 0 || typeIndex >= BALL_TYPE_COUNT) return;

            //Mesh resolution by distance from the camera
            float distance = Vector3.Distance(ball.Position, Camera3D.Position);
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

            //Editor balls never rotate, so the position is the whole transformation
            bucket[count] = new ModelInstance(Matrix.CreateTranslation(ball.Position), occlusionData);
            _ballInstanceCounts[bucketIndex] = count + 1;
        }

        private void DrawBallsInstanced()
        {
            for (int typeIndex = 0; typeIndex < BALL_TYPE_COUNT; typeIndex++)
                for (int lod = 0; lod < BALL_LOD_COUNT; lod++)
                {
                    int bucketIndex = typeIndex * BALL_LOD_COUNT + lod;

                    _ballRenderers[lod].Draw(Camera3D, _ballInstances[bucketIndex], _ballInstanceCounts[bucketIndex],
                        BasicEffectParamsProvider.GetEffectByType((BallType)(typeIndex + 1)),
                        BasicEffectParamsProvider.GetDiffuseTintByType((BallType)(typeIndex + 1)));
                }
        }

        /// <summary>
        /// Looks at the center of the play field from the given direction, from far enough away for the whole
        /// field to fit on the screen. The direction is expected to be axis aligned, as all six preset views are.
        /// </summary>
        private void CenterViewOn(Vector3 lookDirection)
        {
            Vector3 half = _aabb.Size * Constants.HALF;

            //Which of the field dimensions ends up across the screen, up the screen and pointing at the camera.
            //Looking from above or below puts Z up the screen, because that is where the up vector of the camera
            //tips over to once it is pointing straight down or up
            float halfWidth, halfHeight, halfDepth;
            if (lookDirection.Y != 0f) (halfWidth, halfHeight, halfDepth) = (half.X, half.Z, half.Y);
            else if (lookDirection.X != 0f) (halfWidth, halfHeight, halfDepth) = (half.Z, half.Y, half.X);
            else (halfWidth, halfHeight, halfDepth) = (half.X, half.Y, half.Z);

            //The window can be resized to any shape, so neither field of view can be assumed to be the narrower one
            float horizontalFieldOfView = 2f * MathF.Atan(MathF.Tan(Camera3D.FieldOfView * Constants.HALF) * Camera3D.AspectRatio);

            float distanceToFace = MathF.Max(
                VIEW_MARGIN * halfWidth / MathF.Tan(horizontalFieldOfView * Constants.HALF),
                VIEW_MARGIN * halfHeight / MathF.Tan(Camera3D.FieldOfView * Constants.HALF));

            //The camera is placed relative to the center of the field, so it has to clear the near half of it as well
            _cih.CameraOffset = halfDepth + distanceToFace;
            _cih.CenterCameraToMapCenter(_aabb.Center, lookDirection, true);
        }

        private void NewMap()
        {
            EnsureNotFullScreen();

            using NewMapDialog dialog = new(_map.StageSizeX, _map.StageSizeZ, _map.Levels);
            if (dialog.ShowDialog() != DialogResult.OK) return;

            _map = new BallsMap(dialog.StageSizeX, dialog.StageSizeZ, dialog.Levels);
            _selector.UpdateBallsBap(_map);
            _aabb.FitToMap(_map);

            Info.CustomText = $"New map {dialog.StageSizeX} x {dialog.StageSizeZ} x {dialog.Levels}";
        }

        protected override void Update(GameTime gameTime)
        {
            //A level dropped/opened on a background thread is applied here, on the main thread, before the
            //focus guard so it lands even if the window briefly lost focus during the drop
            ApplyPendingLevel();

            if (!IsActive) return;

            //Drives the scene motion (waves, wind, birds, snow) off the wall clock, like the game's pulse
            _sceneSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;

            _cih.RegisterCurrentInputState();

            //When a Myra widget owns the keyboard (a value is being typed) or the mouse is over the panel, the
            //editor's own keys and camera must stand down, or typing "42" would fire hotkeys and dragging a
            //slider would spin the camera. Myra processes its input in Draw (_desktop.Render), so these flags
            //reflect the previous frame — a negligible lag.
            bool guiHasKeyboard = _desktop?.FocusedKeyboardWidget != null;
            bool guiHasMouse = _sceneConfigPanelVisible && (_desktop?.IsMouseOverGUI ?? false);

            if (!guiHasKeyboard)
                foreach (var action in _actions) if (_cih.PressedOnce(action.Key, action.Button)) action.Method();

            if (!guiHasKeyboard && !guiHasMouse) _cih.CameraMovement(gameTime);
            _cih.RegisterPreviousInputState();
            
            _cih.Update(gameTime);

            base.Update(gameTime);
        }

        private void SaveJson()
        {
            EnsureNotFullScreen();

            string filePath = GetFilePathByDialog(true);
            if (!string.IsNullOrEmpty(filePath))
            {
                Stopwatch stopwatch = new();
                stopwatch.Start();
                _map.SerializeAsJson(filePath);
                stopwatch.Stop();
                Console.WriteLine($"Serialize JSON (ms): {stopwatch.ElapsedMilliseconds}");
            }
        }

        /// <summary>
        /// Saves the current state as a level file (issue #32): the map plus the active scene backdrop with its
        /// full config and the sky dome, so it reloads looking exactly as it does now — in the game as well as
        /// here. The scene config comes from the shared <see cref="SceneRenderer"/> for the self-lit scenes and
        /// from the editor's own <see cref="CitySceneConfig"/> for the city (its Neon flag set from the view).
        /// </summary>
        private void SaveLevel()
        {
            EnsureNotFullScreen();

            string filePath = GetFilePathByDialog(true);
            if (string.IsNullOrEmpty(filePath)) return;

            SceneConfig sceneConfig;
            if (_scene == SceneKind.City || _scene == SceneKind.NeonCity)
            {
                _cityConfig.Neon = _scene == SceneKind.NeonCity; //the saved config's Kind must match the current view
                sceneConfig = _cityConfig;
            }
            else
            {
                sceneConfig = _sceneRenderer.GetSceneConfig(_scene);
            }

            Level level = new()
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                SkyDome = (byte)_skyDomeNumber,
                Scene = sceneConfig,
                Map = _map.ToBallPositionTypes(),
            };

            try
            {
                level.Save(filePath);
                Info.CustomText = $"Saved level to {Path.GetFileName(filePath)}";
                Console.WriteLine($"[level] Saved '{level.Name}': scene={_scene}, sky={_skyDomeNumber}, balls={_map.GetBallsCount()} -> {filePath}");
            }
            catch (Exception e)
            {
                Info.CustomText = "Save failed";
                Console.WriteLine($"[level] Save failed: {e.Message}");
            }
        }

        private void LoadJson()
        {
            EnsureNotFullScreen();

            string filePath = GetFilePathByDialog(false);
            if (!string.IsNullOrEmpty(filePath))
            {
                new Task(() => { DeserializeMapFromJsonFile(filePath); }).Start();                
            }
        }

        private void EnsureNotFullScreen()
        {
            if (!_graphics.IsFullScreen) return;

            SetGraphics(true);
        }

        private void DeserializeMapFromJsonFile(string filePath)
        {
            try
            {
                //A level file (format marker "bs3d-level") carries a map plus the scene and sky that reproduce
                //its look; a plain map file carries just the layout. Both use .json, so the loader probes. The
                //level is parsed here (may run off the render thread) but applied on the main thread — see
                //ApplyPendingLevel.
                if (Level.IsLevelFile(filePath))
                {
                    Level level = Level.Load(filePath);
                    lock (_pendingLevelLock) _pendingLevel = level;
                    return;
                }

                Stopwatch stopwatch = new();
                stopwatch.Start();
                _map.DeserializeJson(filePath);
                stopwatch.Stop();
                Console.WriteLine($"Deserialize JSON (ms): {stopwatch.ElapsedMilliseconds}");

                //The loaded map may have different play field dimensions
                _selector.UpdateBallsBap(_map);
                _aabb.FitToMap(_map);
            }
            catch (Exception e)
            {
                //A broken or unreadable file (bad JSON, a hand-edit typo in a level, a dropped folder) must not
                //kill the editor. On the drop/dialog path this runs on a background task where an escaping
                //exception would be swallowed silently; on the startup path it runs on the main thread and would
                //crash — so both are caught and logged, leaving the current map/scene untouched.
                Console.WriteLine($"[load] Failed to load '{filePath}': {e.Message}");
            }
        }

        /// <summary>
        /// Applies a level parsed off the render thread, on the main thread: swaps in its map (so the selector
        /// and field outline follow the new field), applies the scene backdrop with its full config and sets
        /// the sky dome — so a level previews in the editor exactly the way it plays. A no-op when nothing is
        /// pending. Mirrors the Testbed's LoadLevel, minus the physics/cannon/camera the game derives.
        /// </summary>
        private void ApplyPendingLevel()
        {
            Level level;
            lock (_pendingLevelLock) { level = _pendingLevel; _pendingLevel = null; }
            if (level == null) return;

            try
            {
                //The editor works in raw grid coordinates (the selector does), so the map is left uncentered,
                //exactly as a loaded map file or a new map is. The ctor validates and throws before the
                //assignment, so a bad map leaves the current one in place.
                _map = new BallsMap(level.Map);
                _selector.UpdateBallsBap(_map);
                _aabb.FitToMap(_map);

                if (level.Scene != null)
                {
                    _scene = level.Scene.Kind;

                    if (level.Scene is CitySceneConfig cityConfig)
                    {
                        //The city lives outside the SceneRenderer: regenerate the buildings from the config's
                        //layout and hand the config to the city renderer (which reads the brightness per frame)
                        _cityConfig = cityConfig;
                        _city = new City(seed: 20260720, arenaHalfExtent: ARENA_HALF_EXTENT, config: _cityConfig);
                        _cityRenderer.CityConfig = _cityConfig;
                    }
                    else
                    {
                        _sceneRenderer.Apply(level.Scene);
                    }
                }

                //The level's dome wins over whatever is up (the sky key still cycles freely from here)
                SetSkyDome(Math.Clamp((int)level.SkyDome, 1, SKY_DOME_COUNT));

                //Point the live editor at the loaded level's scene config
                RebindSceneConfigGrid();

                string levelName = string.IsNullOrEmpty(level.Name) ? "Untitled" : level.Name;
                Info.CustomText = $"Level: {levelName}, scene {_scene}, sky {_skyDomeNumber}";
                Console.WriteLine($"[level] Loaded '{levelName}': scene={_scene}, sky={_skyDomeNumber}, balls={_map.GetBallsCount()}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[level] Failed to apply level: {e.Message}");
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            CollectBallInstances();

            //The scene is drawn in linear radiance into the HDR target; ResolveSceneTarget box-filters,
            //glares, tonemaps and sRGB-encodes it onto the back buffer. The selector gizmo and the text
            //overlay are drawn after that, in display space, so they stay exactly as authored — the same
            //split the game makes for its aimer and overlay.
            GraphicsDevice.SetRenderTarget(_sceneTarget);
            //Clear to the dome's horizon colour (linear), not a fixed blue, so any pixel the hemisphere dome
            //and the finite scene do not cover - the bottom corners at a wide aspect - blends with the hazed
            //skyline instead of showing through as a blue band (same fix as the game).
            //Space has no dome and no horizon, so it clears to black instead: Space.fx covers every pixel of
            //the frame, and black is what would show if it ever did not.
            GraphicsDevice.Clear(_scene == SceneKind.Space ? Color.Black : new Color(_horizonLinear));

            //Space draws no dome either - the full-screen sky pass would only overdraw it. (The editor sets no
            //cloud uniforms at all, so unlike the game and the Testbed it has no cloud shadow to suppress:
            //CloudCoverageGain sits at 0 and CloudSunlight already returns a flat 1.)
            if (_scene != SceneKind.Space) _sky.Draw(Camera3D);

            //Stated after the sky (which sets its own depth state): the backdrop and the balls both want
            //alpha blending, depth on and back-face culling. Drawing the selector also leaves additive
            //blending behind, which would wash the sky dome out on the next frame without this.
            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            //The chosen environment stands in for what the game draws where the city would be. It is drawn
            //before the balls so the cluster reads in front of it; the snow (mountain) comes after, in front.
            SceneFrame sceneFrame = BuildSceneFrame();
            DrawScene(sceneFrame);

            DrawBallsInstanced();

            //The outline is translucent, so it is drawn over the finished scene and does not write depth
            GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _aabb.Draw(Camera3D);

            //Falling snow (mountain) settles in front of everything; a no-op for every other scene
            _sceneRenderer.DrawOverlays(_scene, sceneFrame);

            ResolveSceneTarget();

            //The selector is additive and always on top (depth off), so it belongs after the resolve, in
            //display space, where its BasicEffect colors read the way they were authored
            if (_selector != null)
            {
                GraphicsDevice.BlendState = BlendState.Additive;
                GraphicsDevice.DepthStencilState = DepthStencilState.None;

                //ResolveSceneTarget leaves CullNone behind; the gizmo is back-face culled like the scene, or
                //an additive draw of both faces doubles its brightness where they overlap
                GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

                _selector.Draw(Camera3D);
            }

            //The orientation cross, a corner overlay in display space like the selector and the text
            _axisGizmo.Draw(Camera3D);

            base.Draw(gameTime);

            //The Myra GUI renders last, on top of everything, straight to the back buffer (base.Draw and
            //ResolveSceneTarget leave it bound). Render also processes Myra's own mouse/keyboard input.
            _desktop.Render();
        }

        /// <summary>
        /// Draws the chosen environment backdrop: the city's buildings (lit by the sky rig, neon-lit for the
        /// neon scene) or one of the self-lit shared backdrops. No arena platform — the AABB marks the field.
        /// </summary>
        private void DrawScene(in SceneFrame frame)
        {
            if (_scene == SceneKind.City || _scene == SceneKind.NeonCity)
            {
                bool neon = _scene == SceneKind.NeonCity;
                _cityRenderer.CityNeon = neon ? 1f : 0f;
                _cityRenderer.CityWindowBrightness = neon ? _cityConfig.NeonLook.WindowBrightness : _cityConfig.WindowBrightness;
                _cityRenderer.Draw(Camera3D, _city.Buildings, _city.Buildings.Length, _sceneEffectParams);
            }
            else
                _sceneRenderer.DrawEnvironment(_scene, frame);
        }

        /// <summary>
        /// The per-frame inputs the shared <see cref="SceneRenderer"/> needs. No clouds here (the editor draws
        /// none), so the cloud hook is null and the scenes get full sun; the sun color is its radiance tinted
        /// by the dome, exactly as the game computes it.
        /// </summary>
        private SceneFrame BuildSceneFrame() => new(
            Camera3D,
            SUN_DIRECTION,
            _zenithLinear,
            _horizonLinear,
            SCENE_SUN_RADIANCE * Vector3.Lerp(Vector3.One, _horizonLinear, SCENE_SKY_TINT),
            _sceneSeconds,
            null);

        /// <summary>
        /// Creates the HDR scene target and the quarter-resolution glare targets, or resizes them after a
        /// window resize or a fullscreen switch. Ported from the Testbed — see its <c>EnsureSceneTarget</c>.
        /// </summary>
        private void EnsureSceneTarget()
        {
            if (GraphicsDevice == null) return;

            int width = GraphicsDevice.PresentationParameters.BackBufferWidth * SUPERSAMPLE_FACTOR;
            int height = GraphicsDevice.PresentationParameters.BackBufferHeight * SUPERSAMPLE_FACTOR;
            if (width <= 0 || height <= 0) return;

            if (_sceneTarget != null && _sceneTarget.Width == width && _sceneTarget.Height == height) return;

            _sceneTarget?.Dispose();

            //Supersampling already averages SUPERSAMPLE_FACTOR^2 samples per output pixel, geometry edges
            //included, so MSAA on the scene target only earns its memory when supersampling is off
            _sceneTarget = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.HdrBlendable,
                DepthFormat.Depth24Stencil8, SUPERSAMPLE_FACTOR > 1 ? 0 : MSAA_SAMPLES, RenderTargetUsage.DiscardContents);

            //Sized off the back buffer, not the supersampled target: the glare is blurred anyway
            int glareWidth = Math.Max(GraphicsDevice.PresentationParameters.BackBufferWidth / GLARE_DOWNSAMPLE, 1);
            int glareHeight = Math.Max(GraphicsDevice.PresentationParameters.BackBufferHeight / GLARE_DOWNSAMPLE, 1);

            _glareBright?.Dispose();
            _glareStreak?.Dispose();

            _glareBright = new RenderTarget2D(GraphicsDevice, glareWidth, glareHeight, false, SurfaceFormat.HdrBlendable, DepthFormat.None);
            _glareStreak = new RenderTarget2D(GraphicsDevice, glareWidth, glareHeight, false, SurfaceFormat.HdrBlendable, DepthFormat.None);
        }

        /// <summary>
        /// Extracts what is bright enough to glare and smears it into a star of streaks, both passes at
        /// quarter resolution. Runs between the scene and the tonemap.
        /// </summary>
        private void DrawGlare()
        {
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.SetVertexBuffer(_fullScreenQuad);

            //Techniques and parameters through the references cached in LoadContent (the by-name indexer is
            //a linear scan, and this runs every frame); the constants went out once there
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

        private void DrawFullScreenQuad(Effect effect)
        {
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
            }
        }

        /// <summary>
        /// Builds the clip-space quad the glare and tonemap passes draw. Its corners are already in
        /// normalized device coordinates, so the passes need no transform.
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
        /// Box-filters the supersampled HDR scene onto the back buffer, adds the glare, tonemaps from linear
        /// radiance and encodes to sRGB. The frame's one and only exit from linear light.
        /// </summary>
        private void ResolveSceneTarget()
        {
            DrawGlare(); //Reads the scene target, so it has to happen before the back buffer is bound

            GraphicsDevice.SetRenderTarget(null);

            //The constants (exposure, glare intensity, supersample factor) were set once in LoadContent and
            //persist on the effect; only the targets and their texel size can change (a resize recreates them)
            _tonemapGlareTextureParam.SetValue(_glareStreak);
            _tonemapSceneTextureParam.SetValue(_sceneTarget);
            _tonemapSourceTexelSizeParam.SetValue(new Vector2(1f / _sceneTarget.Width, 1f / _sceneTarget.Height));

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

        private string GetFilePathByDialog(bool save)
        {
            string result = string.Empty;

            if (save)
            {
                using (SaveFileDialog saveFileDialog = new())
                {
                    saveFileDialog.InitialDirectory = Directory.GetCurrentDirectory();
                    saveFileDialog.Filter = Constants.MAPS_FILE_FILTER;
                    saveFileDialog.RestoreDirectory = true;

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        result = saveFileDialog.FileName;
                    }
                }

                return result;
            }

            using (OpenFileDialog openFileDialog = new())
            {
                openFileDialog.InitialDirectory = Directory.GetCurrentDirectory();
                openFileDialog.Filter = Constants.MAPS_FILE_FILTER;
				openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    result = openFileDialog.FileName;
                }
            }

            return result;
        }

        private void SetGraphics(bool windowed = false)
        {
            _graphics.PreferredBackBufferWidth = windowed ? _windowWidth : GraphicsDevice.DisplayMode.Width;
            _graphics.PreferredBackBufferHeight = windowed ? _windowHeight : GraphicsDevice.DisplayMode.Height;
            _graphics.IsFullScreen = !windowed;

            _graphics.SynchronizeWithVerticalRetrace = true;

            _graphics.ApplyChanges();

            EnsureSceneTarget(); //The back buffer just changed size, so the scene target has to follow

            IsMouseVisible = false;
            IsFixedTimeStep = false;
        }

        private void _graphics_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            e.GraphicsDeviceInformation.PresentationParameters.PresentationInterval = PresentInterval.Default;
            e.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.HiDef;

            //The scene renders into the supersampled HDR target and only the resolved full-screen quad ever
            //touches the back buffer, so back-buffer MSAA would antialias nothing. Any MSAA now belongs on
            //the scene target instead, and only when supersampling is off.
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = SUPERSAMPLE_FACTOR > 1 ? 0 : MSAA_SAMPLES;
        }

        protected override void UnloadContent()
        {
            _sceneTarget?.Dispose();
            _glareBright?.Dispose();
            _glareStreak?.Dispose();
            _fullScreenQuad?.Dispose();
            _sceneRenderer?.Dispose();
            _cityRenderer?.Dispose();
            _unitBox?.Dispose();
            _aabb?.Dispose();
            _axisGizmo?.Dispose();
        }

        #region Map Tests

        private void FullMapTest()
        {
            //Fill the current map's whole play field (previously this replaced _map with a new 10×10×10 instance,
            //leaving the selector working on the orphaned old map)
            byte sizeX = _map.StageSizeX;
            byte sizeZ = _map.StageSizeZ;
            byte levels = _map.Levels;

            Stopwatch stopwatch = new();
            stopwatch.Start();

            _map.Clear();

            Array ballTypes = Enum.GetValues(typeof(BallType));
            Random random = new();
            BallType lastBallType = BallType.Type1;

            for (byte x = 0; x < sizeX; x++)
                for (byte z = 0; z < sizeZ; z++)
                    for (byte l = 0; l < levels; l++)
                    {
                        BallType currentBallType;
                        do
                        {
                            currentBallType = (BallType)ballTypes.GetValue(random.Next(ballTypes.Length));
                        } while (currentBallType == lastBallType);

                        _map.PutBallAt(x, z, l, currentBallType);

                        lastBallType = currentBallType;
                    }

            Info.CustomText = "Balls in map: " + _map.GetBallsCount();

            stopwatch.Stop();
            Console.WriteLine($"Single thread build took: {stopwatch.ElapsedMilliseconds} ms");

            _selector.UpdateBallsBap(_map);
        }

        #endregion Map Tests
    }
}