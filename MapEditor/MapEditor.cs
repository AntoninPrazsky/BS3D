using MapEditor.GUI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D;
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
using System.Collections.Generic;
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
        //generated sphere LODs picked by camera distance, instanced through the game's own shader. Every figure
        //of that, the LOD ladder and the instance buckets are BallRenderSet's since #76 — this file held a third
        //copy of them, and the occlusion divisor a copy is exactly the thing that goes wrong quietly.
        //See the "Ball rendering" section in CLAUDE.md.
        private Effect _instancingEffect;
        private BallRenderSet _balls;

        private SkyDome _sky;

        //The game ships eighteen sky domes and starts on the first one
        private static readonly int SKY_DOME_COUNT = SkyDome.Count;
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

        //One either/or with supersampling, decided the same way the shared pipeline decides it for the
        //scene target (PostProcessPipeline.MSAA_SAMPLES)
        private static readonly int MSAA_SAMPLES = PostProcessPipeline.MSAA_SAMPLES;

        #endregion Graphics

        #region Post-processing

        //The scene is drawn in linear radiance into a supersampled HDR target and resolved to the back buffer
        //in one pass — box filter, glare, exposure, the ACES curve, then the sRGB encode — exactly as the game
        //does it, so a map looks here the way it will play. The exposure, clear color and every glare figure
        //are the game's own, so the two agree. See the "Color management" and "Ball rendering" sections in
        //CLAUDE.md, and the matching code in Testbed.cs.
        private const int SUPERSAMPLE_FACTOR = 2;
        private const float DEFAULT_EXPOSURE = 1.1f;

        //0.55 is the figure the game and the Testbed ship (it moved there with the bloom pyramid, #69); the
        //editor sat at the older 0.38 for a while — a silent drift this section's own comment forbids, found
        //and fixed while the pipeline was hoisted (#74)
        private static readonly float GLARE_THRESHOLD = 0.55f;

        //The pyramid accumulates on the way up (see the Testbed's figure and reasoning), so the intensity
        //sits far under the old streak star's — and matches the game's, so a map previews with its bloom.
        private static readonly float GLARE_INTENSITY = 0.5f;

        //The game's default lens fringing, so a map previews with it too (the toggle is the game's alone).
        private static readonly float CHROMATIC_ABERRATION = 0.0015f;

        //The game's default film grain, for the same reason (see FILM_GRAIN in the game)
        private static readonly float FILM_GRAIN = 0.10f;

        //The HDR scene target, the bloom pyramid (#69), the tonemap resolve and every cached parameter —
        //one shared copy for all three executables (Prazsky.Core.Render.PostProcessPipeline, #74). What
        //stays here is the editor's own look figures above, passed in once at load.
        private PostProcessPipeline _pipeline;

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

        //What a loaded level carried beyond the editor's own state, held so F4 writes it back — a round-trip
        //through the editor used to silently unpin a level's theme. Cleared when a fresh map or a plain map
        //file replaces the level.
        private string _levelMusic;
        private string _levelAuthor;

        //What the map's balls are made of (#258) — cycled by L, written into the level by F4 and read back off
        //one on load. Beach for a new map and for a plain map file, which carries no look at all.
        private BallStyle _ballStyle = BallStyle.Beach;

        private City _city;
        private BoxMesh _unitBox;
        private InstancedModelRenderer _cityRenderer;
        //The city's fixed parameters; the G panel edits them live for tuning, nothing persists them
        private CitySceneConfig _cityConfig = new();

        //The forest's scattered trees, boulders and stumps, so a forest level previews with the wood standing on
        //it rather than as a bare clearing. It was the Game's alone until #75, which is why the editor drew the
        //glade and none of the trees on it even though the terrain under them was always the shared
        //SceneRenderer's. Every texture, mesh variant, renderer, matte material and encoded tint is the
        //component's; the draw's place in the frame and the scene gate on it are this file's.
        //
        //Built unconditionally, whether or not a forest level is ever loaded — fifteen meshes and twenty-five
        //instance buffers, the same deal the Game already takes. The forest is reachable here ONLY by loading a
        //level whose scene is forest: V stops at SceneRenderer.CycleLength, exactly as it does for space, the
        //dream and the cavern.
        private ForestScatterRenderer _forestScatter;

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

        //The sky-derived light rig, shared with the game since #75 — the palette in linear radiance, the
        //hemisphere ambient, the tints and the sun the scenes are shaded by. It used to be a near-mirror of the
        //game's copy here, constants and all, which is exactly the drift this editor exists to not have.
        private SkyLightRig _rig;

        //The clearing radius no longer mirrors anything: the game's arena became the round stone island
        //(ArenaIsland.RADIUS = 26), so this 60 keeps the editor's towers ~2.3× as far from the field as they
        //stand in play. Whether to close that to 26 is a look decision, not a sync.
        private const float ARENA_HALF_EXTENT = 60f;
        //Window brightness (day + neon) now lives in _cityConfig.WindowBrightness / _cityConfig.NeonLook.WindowBrightness.
        private const float CITY_SPECULAR_AMBIENT = 0.07f;
        private const float SCENE_AMBIENT_INTENSITY = 0.25f;

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

            _pipeline?.EnsureTarget(); //The back buffer just changed size, so the scene target has to follow

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

            var actions = new List<ButtonAction>
            {
                new(mgKeys.Up, Buttons.DPadUp, () => _selector.Move(Vector3.Forward), "Move selector forward"),
                new(mgKeys.Down, Buttons.DPadDown,() => _selector.Move(Vector3.Backward), "Move selector backward"),
                new(mgKeys.Left, Buttons.DPadLeft,() => _selector.Move(Vector3.Left), "Move selector left"),
                new(mgKeys.Right, Buttons.DPadRight,() => _selector.Move(Vector3.Right), "Move selector right"),
                new(mgKeys.PageUp, Buttons.RightShoulder,() => _selector.Move(Vector3.Up), "Move selector up"),
                new(mgKeys.PageDown, Buttons.LeftShoulder,() => _selector.Move(Vector3.Down), "Move selector down"),

                new(mgKeys.Space, Buttons.A,() => _selector.PutBall(), "Put ball"),
                new(mgKeys.Delete, Buttons.B,() => _selector.RemoveBall(), "Remove ball"),

            };

            //One row per direct-key colour: NumPad1..NumPad9 and Type1..Type9 are both consecutive values,
            //so the nine bindings are one loop over the name table rather than nine rows whose three digits
            //(key, type, name index) would have to be kept agreeing by hand.
            for (int i = 0; i < 9; i++)
            {
                BallType type = (BallType)(i + 1);
                actions.Add(new((mgKeys)((int)mgKeys.NumPad1 + i), () => SetBallType(type),
                    $"Change ball type to {(int)type} ({BALL_TYPE_NAMES[i]})"));
            }

            actions.AddRange(new ButtonAction[]
            {
                //Thirteen types outgrew the numpad's digits (#152): brown, silver, navy and olive have no
                //direct key and are reached by cycling. The wrap makes the two keys a complete picker on
                //their own; the digits above stay as shortcuts into the first nine.
                new(mgKeys.Add,() => CycleBallType(+1), "Next ball type (10-13 have no direct key)"),
                new(mgKeys.Subtract,() => CycleBallType(-1), "Previous ball type"),

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
                new(mgKeys.L, SwitchBallStyle, "Switch ball look (beach vinyl / glass bubble)"),
                new(mgKeys.K, CycleBallKind, "Switch ball kind (normal / rock)"),
                new(mgKeys.G, ToggleSceneConfigPanel, "Show/hide scene-config editor"),

                new(mgKeys.F1, SaveJson, "Save map to file (JSON)"),
                new(mgKeys.F2, LoadJson, "Load map or level from file (JSON)"),
                new(mgKeys.F3, NewMap, "New map (choose play field size)"),
                new(mgKeys.F4, SaveLevel, "Save level: map + current scene + sky (JSON)"),
            });

            _actions = actions.ToArray();

            StringBuilder builder = new();
            foreach (var act in _actions) builder.Append(string.Format("{0,-9} {1}\n", act.Key.ToString(), act.Description));

            Info.HintText = builder.ToString();

            #endregion

            base.Initialize();
        }

        //One copy of the colour names in the editor: the direct keys' hint descriptions and the cycle keys'
        //on-screen feedback both read it, so a colour added to BallType shows up here by adding one row.
        //Ordered by BallType value, index = value - 1.
        private static readonly string[] BALL_TYPE_NAMES =
        {
            "red", "green", "blue", "white", "cyan", "magenta", "yellow", "black",
            "orange", "brown", "silver", "navy blue", "olive green",
        };

        private void SetBallType(BallType type)
        {
            _selector.ChangeBallType(type);

            //The fallback keeps a keypress from crashing the editor when BallType has grown a member whose
            //name row is not here yet — the cycle reaches every enum value the moment the enum has it
            int index = (int)type - 1;
            string name = index < BALL_TYPE_NAMES.Length ? BALL_TYPE_NAMES[index] : type.ToString();
            Info.CustomText = $"Ball type {(int)type} ({name})";
        }

        private void CycleBallType(int step)
        {
            //The +Count keeps the modulo positive when stepping back off type 1
            int index = ((int)_selector.ActiveBallType - 1 + step + BallTypes.Count) % BallTypes.Count;
            SetBallType((BallType)(index + 1));
        }

        /// <summary>
        /// Cycles what the next placed ball IS, beside its colour (#323) — normal, or a rock (#324).
        /// <para>
        /// A key of its own rather than more entries on the colour cycle, because the two are orthogonal: a
        /// rock is not a fourteenth colour, and a picker that mixed them would say it was. The colour keys go
        /// on working while a rock is armed — a rock carries a type like every other cell, nothing reads it,
        /// and the moment a second kind exists that <i>does</i> (a frozen ball is a coloured ball in ice) the
        /// two pickers already compose.
        /// </para>
        /// </summary>
        private void CycleBallKind()
        {
            BallKind kind = BallKinds.Next(_selector.ActiveBallKind);
            _selector.ChangeBallKind(kind);

            Info.CustomText = kind == BallKind.Normal
                ? "Ball kind: normal"
                : $"Ball kind: {BallKinds.ToName(kind)} (matches with nothing, no colour removes it)";
        }

        protected override void LoadContent()
        {
            _instancingEffect = Content.Load<Effect>("Shaders/InstancedModel");

            //The pipeline caches its parameters and sets each look value exactly once through the required
            //initializer — fixed for the whole run here (the game alone has the Settings toggles). See #74.
            _pipeline = new PostProcessPipeline(GraphicsDevice,
                Content.Load<Effect>("Shaders/Tonemap"), Content.Load<Effect>("Shaders/Glare"))
            {
                GlareThreshold = GLARE_THRESHOLD,
                GlareIntensity = GLARE_INTENSITY,
                Exposure = DEFAULT_EXPOSURE,
                ChromaticAberration = CHROMATIC_ABERRATION,
                FilmGrain = FILM_GRAIN,
                SupersampleFactor = SUPERSAMPLE_FACTOR,
            };

            //No ripples here: the landing wave is the game's, and passing false switches the shader's whole
            //ripple term off on a branch over the uniform, so the editor pays nothing for not having one.
            //SupersampleFactor, for the same reason the scene renderer is told it below: the dissolve's dither cell
            //is authored in output pixels, so unscaled it would be averaged away by the tonemap's box filter. The
            //editor draws no dissolving ball today (no magazine, no landing preview), so this is here to keep the
            //three callers saying the same thing rather than to fix anything visible here.
            //Style included: a level opened from the command line is applied before this runs, so the set has
            //to be built with whatever the editor already holds rather than with the default.
            _balls = new BallRenderSet(GraphicsDevice, _instancingEffect, ripples: false)
            {
                SupersampleFactor = SUPERSAMPLE_FACTOR,
                Style = _ballStyle
            };

            //linearVertexColors: the dome is drawn through BasicEffect into the linear HDR target, so its
            //baked gradient has to be converted from sRGB once at load or the tonemapper reads a gradient of
            //display values as radiance. The game does the same; the editor used to skip it, drawing in gamma.
            _sky = new SkyDome(GraphicsDevice, _skyDomeNumber, linearVertexColors: true);

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

            //After the scene renderer, because the rig consults it for the scenes that state their own lighting
            _rig = new SkyLightRig(_sceneRenderer);

            //The forest's wood, planted on the very terrain the SceneRenderer above draws. After it, because the
            //component is built from that renderer's own forest config, and before the sky lighting below, since
            //fresh renderers have never been told the dome's palette. No stone texture handed in: the editor has
            //none of its own, so the component builds one for the boulders.
            _forestScatter = new ForestScatterRenderer(GraphicsDevice, _instancingEffect,
                (ForestSceneConfig)_sceneRenderer.GetSceneConfig(SceneKind.Forest), SCENE_AMBIENT_INTENSITY);

            ApplySkyLighting();

            _pipeline.EnsureTarget();

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
            //The cycle is deliberately only SceneRenderer.CycleLength long — the seven scenes a map is authored
            //against — but it has to be enterable from OUTSIDE it, and (index + 1) % 7 was not: a level whose
            //config names the forest, space, the dream, the cavern, the Moon or the outback puts _scene past the
            //cycle's end, and the modulo then landed wherever the arithmetic fell rather than at a boundary. From
            //a forest level (index 7) the first V came out at the SEA, three scenes deep, so the four entries
            //before it could not be reached without four more presses — in the one program whose whole job is
            //previewing a map against every backdrop it might play in. Anything off the end restarts the cycle at
            //the city now. The same one-liner in the Testbed was fixed with #73; this is its other copy.
            int next = (int)_scene + 1;
            _scene = (SceneKind)(next < SceneRenderer.CycleLength ? next : 0);

            Info.CustomText = $"Scene: {SceneRenderer.SceneName(_scene)}";

            //Re-derive the rig: a scene may state its own lighting instead of the dome's, and V is a scene
            //change like any other. It was missing here — latent rather than visible, since the cycle stays
            //inside the seven scenes that all take the dome's, but it became a real bug the moment the cycle
            //widened or one of those seven stated a rig, and neither is a change anyone would think to check.
            ApplySkyLighting();

            RebindSceneConfigGrid();
        }

        /// <summary>
        /// Cycles what the map's balls are made of (#258) — the vinyl beach ball, or the glass bubble — and
        /// applies it to the render set at once, so the preview is the answer rather than a description of it.
        /// It is a property of the map, so <c>F4</c> writes it into the level file beside the scene and the
        /// dome, and loading one brings it back.
        /// <para>
        /// On <c>L</c> for "look", which is what was free: S, W, A, D, Q and E drive the camera, B is the sky
        /// dome, V the scene, G the tuning panel, N fills the map and M clears it. There is no count of styles
        /// here — the cycle is <see cref="BallStyles.Next"/>'s, off the enum itself, so a third material cannot
        /// be added and left unreachable in the one program that exists to choose between them.
        /// </para>
        /// </summary>
        private void SwitchBallStyle()
        {
            SetBallStyle(BallStyles.Next(_ballStyle));

            Info.CustomText = $"Balls: {BallStyles.ToName(_ballStyle)}";
        }

        //The one place the editor's own answer and the render set's are set together, so a load, a new map and
        //the N cycle cannot leave the two saying different things — which would mean F4 writing a style the
        //preview never showed.
        private void SetBallStyle(BallStyle style)
        {
            _ballStyle = style;

            //Null until LoadContent has run, and a level can be handed in before then (a file argument, a drop
            //that lands during startup): the field is the truth either way, and the render set is built with it.
            if (_balls != null) _balls.Style = style;
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
            _sceneConfigHeader.Text = $"{_scene}  —  edit to preview live; not saved  (G: hide)";
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

                    //A forest edit reaches into the meshes as well as the planting — the tree, boulder and stump
                    //proportions are baked into them and the three colours into the cached tints — so the wood is
                    //rebuilt whole rather than merely re-planted, and then re-lit, its renderers all being new.
                    //This is the one thing the game never needs: nothing there edits a scene config at runtime,
                    //which is exactly why the component reads the config at build time only.
                    if (sceneConfig is ForestSceneConfig forest)
                    {
                        _forestScatter.Replant(forest);
                        ApplySkyLighting();
                    }
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
            _sky.DomeNumber = number;

            ApplySkyLighting();

            Info.CustomText = $"Sky dome {_skyDomeNumber}";
        }

        /// <summary>
        /// Re-derives the lighting from the sky dome and pushes it onto everything the editor draws. The whole
        /// derivation is the game's, literally — <see cref="SkyLightRig"/> is the one copy since #75, so a
        /// palette, a tint or a scene's own rig cannot mean one thing here and another in play.
        /// <para>
        /// Only the enrolment is the editor's own, and it is short: the balls, the city and the forest's scatter.
        /// The city takes part like every other instanced object — its facades are dark, but its specular ambient
        /// reads the sky. There is no island, no drain and no ceiling here to light.
        /// </para>
        /// <para>
        /// Run again after every <see cref="ForestScatterRenderer.Replant"/>: a re-planted wood is twenty-five
        /// brand-new renderers, none of which has been told the dome's palette, exactly as a refitted
        /// <c>CeilingPlate</c> is in the game.
        /// </para>
        /// </summary>
        private void ApplySkyLighting()
        {
            //A scene that states its own rig — space, the dream, the cavern — has to be honoured here too, or a
            //level of one would draw the right sky and light its balls by the wrong sun, which is the one thing
            //this editor exists to prevent. Those scenes are reachable despite V cycling only the first seven:
            //loading a LEVEL sets _scene from the level's own scene kind. The rig reads the override itself,
            //which is why the scene goes in with the dome.
            _rig.SetSky(_sky, _scene);

            foreach (InstancedModelRenderer renderer in _balls.Renderers) _rig.ApplyTo(renderer);
            _rig.ApplyTo(_cityRenderer);

            //Every variant of every scattered kind, or a spruce of the variant this missed would stand under the
            //light rig of whatever dome was up when it was made. The array the component hands back, walked
            //directly, for the reason BallRenderSet.Renderers gives above.
            foreach (InstancedModelRenderer renderer in _forestScatter.Renderers) _rig.ApplyTo(renderer);

            //And the wood's own pigments, which the rig above cannot reach — see ForestScatterRenderer.
            //ShiftTowardsSky (#108). This is the executable the symptom is quickest to see in: B cycles the
            //domes over a parked forest.
            _forestScatter.ApplySkyTint(_rig.KeyTint);
        }

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

            //A fresh map is not the loaded level any more — F4 must not write a stale theme onto it
            _levelMusic = null;
            _levelAuthor = null;
            SetBallStyle(BallStyle.Beach);

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

            //No circular camera movement: its NumPad7/9 orbit keys are ball types here (7 recoloured the
            //selector on every orbit press long before #152 put orange on 9). Mouse rotation and D1-D6 views
            //cover what the orbit did.
            if (!guiHasKeyboard && !guiHasMouse) _cih.CameraMovement(gameTime, allowCircularMovement: false);
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
        /// Saves the current state as a level file (issue #32): the map, the NAME of the active scene and the
        /// sky dome — a level names its scene and the scene's parameters are fixed in code (format 2), so
        /// there is no config to write. What a loaded level carried beyond the editor's own state — its
        /// theme, its author — is written back, so a round-trip through the editor no longer silently unpins
        /// a level's music (the trap `docs/formats-and-tools.md` used to record).
        /// </summary>
        private void SaveLevel()
        {
            EnsureNotFullScreen();

            string filePath = GetFilePathByDialog(true);
            if (string.IsNullOrEmpty(filePath)) return;

            Level level = new()
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                Author = _levelAuthor,
                SkyDome = (byte)_skyDomeNumber,
                Scene = _scene,
                Music = _levelMusic,
                //Written only when it is not the default (#258), so a level of ordinary vinyl balls stays
                //byte-for-byte the file it was: the field is absent from every level authored before the style
                //existed, and a round-trip through the editor must not start adding it to all of them.
                Balls = _ballStyle == BallStyle.Beach ? null : _ballStyle,
                Map = _map.ToBallPositionTypes(),
            };

            try
            {
                level.Save(filePath);
                Info.CustomText = $"Saved level to {Path.GetFileName(filePath)}";
                Console.WriteLine($"[level] Saved '{level.Name}': scene={_scene}, sky={_skyDomeNumber}, "
                    + $"style={BallStyles.ToName(_ballStyle)}, balls={_map.GetBallsCount()} -> {filePath}");
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

                //A plain map carries no theme, author or ball style — F4 must not write the previous level's
                //onto it
                _levelMusic = null;
                _levelAuthor = null;
                SetBallStyle(BallStyle.Beach);
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
        /// and field outline follow the new field), switches to the scene backdrop it names and sets
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

                //A level names its scene and nothing more (format 2): the scene's parameters are fixed in
                //code and already live in the renderer's defaults, so switching to it is what the V cycle
                //does — set the kind. The default city and forest stand from startup.
                if (level.Scene is SceneKind sceneKind) _scene = sceneKind;

                //Capture what the file carried that the editor does not otherwise know, so F4 writes it back
                //and a round-trip through the editor no longer silently unpins a level's theme
                _levelMusic = level.Music;
                _levelAuthor = level.Author;
                SetBallStyle(level.Balls ?? BallStyle.Beach);

                //The level's dome wins over whatever is up (the sky key still cycles freely from here)
                SetSkyDome(Math.Clamp((int)level.SkyDome, 1, SKY_DOME_COUNT));

                //Point the live tuning panel at the named scene's config (and sync the city's Neon flag)
                RebindSceneConfigGrid();

                string levelName = string.IsNullOrEmpty(level.Name) ? "Untitled" : level.Name;
                Info.CustomText = $"Level: {levelName}, scene {_scene}, sky {_skyDomeNumber}, "
                    + $"{BallStyles.ToName(_ballStyle)} balls";
                Console.WriteLine($"[level] Loaded '{levelName}': scene={_scene}, sky={_skyDomeNumber}, "
                    + $"style={BallStyles.ToName(_ballStyle)}, balls={_map.GetBallsCount()}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[level] Failed to apply level: {e.Message}");
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            //The scene is drawn in linear radiance into the HDR target; the pipeline's Resolve box-filters,
            //glares, tonemaps and sRGB-encodes it onto the back buffer. The selector gizmo and the text
            //overlay are drawn after that, in display space, so they stay exactly as authored — the same
            //split the game makes for its aimer and overlay.
            GraphicsDevice.SetRenderTarget(_pipeline.SceneTarget);
            //Clear to the dome's horizon colour (linear), not a fixed blue, so any pixel the hemisphere dome
            //and the finite scene do not cover - the bottom corners at a wide aspect - blends with the hazed
            //skyline instead of showing through as a blue band (same fix as the game).
            //Space has no dome and no horizon, so it clears to black instead: Space.fx covers every pixel of
            //the frame, and black is what would show if it ever did not.
            GraphicsDevice.Clear(SceneRenderer.ReplacesSky(_scene) ? Color.Black : new Color(_rig.HorizonLinear));

            //Space draws no dome either - the full-screen sky pass would only overdraw it. (The editor sets no
            //cloud uniforms at all, so unlike the game and the Testbed it has no cloud shadow to suppress:
            //CloudCoverageGain sits at 0 and CloudSunlight already returns a flat 1.)
            if (!SceneRenderer.ReplacesSky(_scene)) _sky.Draw(Camera3D);

            //Stated after the sky (which sets its own depth state): the backdrop and the balls both want
            //alpha blending, depth on and back-face culling. Drawing the selector also leaves additive
            //blending behind, which would wash the sky dome out on the next frame without this.
            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            //The sea's submerge fade for missed balls — a no-op off the sea scene (see SceneRenderer.ApplySeaSubmerge).
            //The editor draws the balls exactly as the game does, and since #131 the sea is depth-read, so without
            //this the editor inherits the depth-read half and none of the fade half (#143).
            //The lens is reported as never submerged, and that is correct here rather than a shortcut: the fade
            //is released by what the tonemap's murk takes over (#159), and the editor pins that murk to zero
            //(see the Resolve call) — so a released fade there would be a release with nothing behind it. The
            //editor's flying camera therefore keeps the fade it has always had, whatever depth it swims to.
            _sceneRenderer.ApplySeaSubmerge(_instancingEffect, _scene, 0f);

            //The chosen environment stands in for what the game draws where the city would be. It is drawn
            //before the balls so the cluster reads in front of it; the snow (mountain) comes after, in front.
            SceneFrame sceneFrame = BuildSceneFrame();
            DrawScene(sceneFrame);

            //The map's balls, through the shared set. BeginFrame hands back a ref struct, which is what keeps
            //the once-per-frame walk from being anything else: it cannot be stored, so it cannot outlive the
            //frame it belongs to. A static map has nothing to ease towards, so AddMap simply reads the grid.
            BallDrawFrame ballFrame = _balls.BeginFrame(Camera3D);
            ballFrame.AddMap(_map);
            _balls.Draw(_sceneSeconds);

            //The outline is translucent, so it is drawn over the finished scene and does not write depth
            GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _aabb.Draw(Camera3D);

            //Falling snow (mountain) settles in front of everything; a no-op for every other scene
            _sceneRenderer.DrawOverlays(_scene, sceneFrame);

            //No water in the editor, so the underwater amount is pinned at zero (a no-op in the shader) — and
            //no level to end, so the defocus is pinned at zero with it and its targets are never built
            _pipeline.Resolve(_sceneSeconds, 0f, 0f);

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
            //the pipeline's Resolve leave it bound). Render also processes Myra's own mouse/keyboard input.
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
                //Frustum-culled and ordered near to far, as the game draws it — see City.PrepareVisible
                int visibleBuildings = _city.PrepareVisible(Camera3D);
                _cityRenderer.Draw(Camera3D, _city.Visible, visibleBuildings, _sceneEffectParams);
            }
            else
                //The target goes in so the cavern and the dream can be shaded at the back buffer's size and
                //scaled up (#155) — the editor draws the scenes exactly as the game does, this one included.
                _sceneRenderer.DrawEnvironment(_scene, frame, _pipeline.SceneTarget);

            //The forest's scattered trees, boulders and stumps, after the terrain they stand on — with depth, or
            //they would draw through it. The state is the caller's, and it is already the game's: alpha blend,
            //depth test and write and counter-clockwise culling into the supersampled HDR target. The component
            //touches none of it, so the balls drawn after this are unaffected.
            if (_scene == SceneKind.Forest) _forestScatter?.Draw(Camera3D);
        }

        /// <summary>
        /// The per-frame inputs the shared <see cref="SceneRenderer"/> needs, built by the rig that already
        /// holds five of the six. No clouds here (the editor draws none): the rig's cloud hook was never set, so
        /// it stays null, the cloud uniforms stay at zero and the scenes get full sun with no shadow.
        /// </summary>
        private SceneFrame BuildSceneFrame() => _rig.BuildSceneFrame(Camera3D, _sceneSeconds);

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

            //The back buffer just changed size, so the scene target has to follow. Null-conditional for the
            //constructor's call, which runs before LoadContent has built the pipeline.
            _pipeline?.EnsureTarget();

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
            _pipeline?.Dispose();
            _balls?.Dispose();
            _sceneRenderer?.Dispose();
            _cityRenderer?.Dispose();
            //Every mesh, renderer and procedural texture of the forest scatter, in one call — its stone texture
            //included, the editor having handed it none of its own
            _forestScatter?.Dispose();
            //The dome's two buffers and its owned BasicEffect (the editor's only dome draw path)
            _sky?.Dispose();
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
