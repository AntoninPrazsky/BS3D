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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        /// <summary>The <c>shot=</c> schedule in seconds, or null for none (see <see cref="ScreenshotWriter"/>).</summary>
        public float[] ShotSeconds { get; set; }

        /// <summary><c>campos=</c> and <c>camtarget=</c>: where the camera starts and what it looks at, in the editor's grid
        /// frame, or null for the default. R returns to them, as it returns to the default.</summary>
        public Vector3? StartCameraPosition { get; set; }

        /// <inheritdoc cref="StartCameraPosition"/>
        public Vector3? StartCameraTarget { get; set; }

        /// <summary><c>notext</c>: the run starts with the text hidden, as F12 hides it, so a photograph shows the scene whole.</summary>
        public bool StartWithTextHidden { get; set; }

        //The editor's own frame written to a PNG (#1209): the shot= schedule, so its screen can be photographed at a
        //stated window size without focus, a screen grab or a window in front of it getting into the picture
        private ScreenshotWriter _shots;

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

        //The game ships SkyDome.Count sky domes and starts on the first one
        private static readonly int SKY_DOME_COUNT = SkyDome.Count;
        private int _skyDomeNumber = 1;

        #endregion Ball rendering

        private BallsMap _map;
        private Selector _selector;
        private AABB _aabb;
        private AxisGizmo _axisGizmo;

        private CameraInputHelper _cih;
        private ButtonAction[] _actions;

        //The grouped key help that fits the window (#1209), drawn over the scene beside the shared InfoRenderer
        private KeyHelpOverlay _keyHelp;

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

        //0.55 is the figure the game and the Testbed ship (it moved there with the bloom pyramid, #69); the
        //editor sat at the older 0.38 for a while — a silent drift this section's own comment forbids, found
        //and fixed while the pipeline was hoisted (#74)
        private static readonly float GLARE_THRESHOLD = 0.55f;

        //The pyramid accumulates on the way up (see the Testbed's figure and reasoning), so the intensity
        //sits far under the old streak star's — and matches the game's, so a map previews with its bloom.
        private static readonly float GLARE_INTENSITY = 0.1f;

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
        //ball types). The island, the plate and the game's sky stand in them too since #1210 (the region below), each
        //behind a switch; the AABB still marks the field.
        private SceneRenderer _sceneRenderer;
        private SceneKind _scene = SceneKind.City;

        //What a loaded level carried beyond the editor's own state, held so F4 writes it back — a round-trip
        //through the editor used to silently unpin a level's theme. Cleared when a fresh map or a plain map
        //file replaces the level.
        private string _levelMusic;
        private string _levelAuthor;

        //The crates a loaded level carried (#257), kept through a round-trip though the editor neither shows nor places them
        private CrateSpec[] _levelCrates;

        //And its spring (#690) and its sky (#221), on the same terms: the editor hangs nothing, and it draws the weather only
        //with the game's sky on (G, #1210), but a round-trip must not quietly stiffen a soft level or put it back under its
        //scene's usual sky, whatever the switch says
        private SoftnessSpec _levelSoftness;
        private string _levelWeather;

        //What the map's balls are made of (#258) — cycled by L, written into the level by F4 and read back off
        //one on load. Beach for a new map and for a plain map file, which carries no look at all.
        private BallStyle _ballStyle = BallStyle.Beach;

        private City _city;

        //The scene's procedural roll, rolled once per launch and added to every seeded arrangement (both
        //cities, their roofs, the wood, the savanna's planting, the palms). The editor takes no sceneseed=
        //to pin it with, and wants none: an author is looking at whether a MAP reads against a backdrop,
        //and a backdrop that is a different city each session is a better test of that than one that is not.
        private readonly int _sceneSeedOffset = Random.Shared.Next();

        //Which of the two cities _city holds (#471's follow-up), so V rebuilds only when it crosses between them.
        private bool _cityIsNeon;
        private BoxMesh _unitBox;
        private InstancedModelRenderer _cityRenderer;
        //The equipment on the city's roofs (#436), redressed whenever V rebuilds the city
        private CityRooftops _rooftops;
        //The street level the towers stand on (#399), rebuilt with the city
        private CityStreets _streets;
        //The city's fixed parameters — in code like every other scene's (SceneRenderer.GetSceneConfig), and
        //out here only because the city itself is: City and the instanced city technique live outside the
        //SceneRenderer. Nothing edits it at runtime any more (the live scene-config panel went in #522).
        private CitySceneConfig _cityConfig = new();

        //The forest's scattered trees, boulders and stumps, so a forest level previews with the wood standing on
        //it rather than as a bare clearing. It was the Game's alone until #75, which is why the editor drew the
        //glade and none of the trees on it even though the terrain under them was always the shared
        //SceneRenderer's. Every texture, mesh variant, renderer, matte material and encoded tint is the
        //component's; the draw's place in the frame and the scene gate on it are this file's.
        //
        //Built unconditionally, whether or not a forest level is ever loaded — fifteen meshes and twenty-five
        //instance buffers, the same deal the Game already takes. That is a better deal than it was: since #380
        //V reaches the forest, so a build nobody could arrive at without opening a forest level is now one press
        //away like any other scene.
        private ForestScatterRenderer _forestScatter;

        //A second, independent planting of the same shared wood for the aurora scene (#205) - its own
        //config (AuroraSceneConfig.Terrain), its own meshes and tints. See AuroraSceneConfig's class doc.
        private ForestScatterRenderer _auroraScatter;

        //The forest's handful of firefly-like blinking lights (#487), planted over the same floor.
        private ForestFireflies _forestFireflies;

        //The forest's old lamps on their poles round the island (#1166), off the backdrop's layout; their light falls on the
        //balls and the island through the editor's SceneLights (#1210), as in the game
        private ForestLamps _forestLamps;

        //A map or level dropped or opened is parsed off the render thread, and everything it changes is
        //applied on the main thread in Update — see ApplyPendingLoad. A level's scene/sky/city application
        //touches GPU resources (Content.Load, buffer rebuilds, a new City); a plain map used to be read
        //straight into the live _map on the worker (#573), and BallsMap.ApplyBallPositionTypes sets the field
        //size before it replaces the ball array, so a Draw landing between the two walked the new bounds over
        //the old array and threw on the render thread. So the worker only ever builds a NEW object and parks it
        //here; nothing the render thread reads is written off it. One slot: the last file dropped wins.
        private PendingLoad _pendingLoad;
        private readonly object _pendingLoadLock = new();

        //What a worker parsed: a level (map, scene, sky, style…) or a plain map file's layout alone.
        private sealed record PendingLoad(Level Level, BallsMap Map);

        //Wall-clock seconds the scene motion runs off (waves, wind, birds, snow), so the environment keeps
        //moving the way it does in the game instead of freezing
        private float _sceneSeconds;

        //The sky-derived light rig, shared with the game since #75 — the palette in linear radiance, the
        //hemisphere ambient, the tints and the sun the scenes are shaded by. It used to be a near-mirror of the
        //game's copy here, constants and all, which is exactly the drift this editor exists to not have.
        private SkyLightRig _rig;

        //Window brightness (day + neon) now lives in _cityConfig.WindowBrightness / _cityConfig.NeonLook.WindowBrightness.
        private const float CITY_SPECULAR_AMBIENT = 0.07f;
        private const float SCENE_AMBIENT_INTENSITY = 0.25f;

        private readonly BasicEffectParams _sceneEffectParams = new(Vector3.One * SCENE_AMBIENT_INTENSITY, Vector3.Zero, 0f, Vector3.Zero);

        #endregion Scenes

        #region The setting the game draws round the field (#1210)

        //The editor keeps the map in its raw grid frame (the selector, the outline, the six views, the flying camera); the
        //setting and the balls stand in the frame a session hangs the field in, drawn through this lens: the editor's own,
        //moved by the field's hang every frame (GameFrameCamera.Follow). So the island stands under the field, the plate over
        //it and the scene round it exactly where a level puts them.
        private GameFrameCamera _gameCamera;

        //The island, the glass drain and the pit (the shared ArenaIsland, as the Game and the Testbed draw it), the ceiling
        //glass the cluster hangs from with the cables and winches it hangs by, and the scene's point lights on all of it and
        //on the balls (the campfire, the neon, the forest's lamps)
        private ArenaIsland _island;
        private CeilingPlate _ceilingPlate;
        private CeilingRigging _ceilingRigging;
        private SceneLights _sceneLights;

        //The field the plate and the rigging were last fitted to: a refit is a new renderer, so only on a change of size
        private byte _plateStageX, _plateStageZ;

        //THE GAME'S SKY: Sky.fx over the dome (the sun's disc, the cloud deck under the level's weather), the heaps over it
        //where the Game draws them, and the deck's shadows on everything below. It reverses #220's "no disc and no clouds in
        //the editor" (the owner asked for the game's sky here); the dome is still the plain one with it off (G).
        private Effect _skyEffect;
        private EffectParameter _skyCameraPositionParam;
        private SkyPass _skyPass;
        private readonly CloudField _clouds = new();
        private CloudVolumes _cloudVolumes;

        //The three view switches (G, I, C): what is drawn, never what F4 writes. Each starts on, so the editor opens on
        //the game's view of the map, unless the command line said off.
        private bool _gameSkyShown = true;
        private bool _islandShown = true;
        private bool _ceilingGlassShown = true;

        //Built once each, so the per-frame hand-over allocates no delegate (BestPractices.md)
        private Action<Matrix> _drawShadowCasters;

        /// <summary><c>gamesky=off</c>: start under the plain dome rather than the game's sky (G switches it).</summary>
        public bool ShowGameSky { get => _gameSkyShown; set => _gameSkyShown = value; }

        /// <summary><c>island=off</c>: start with no island under the field (I switches it).</summary>
        public bool ShowIsland { get => _islandShown; set => _islandShown = value; }

        /// <summary><c>glass=off</c>: start with no ceiling glass over the field (C switches it).</summary>
        public bool ShowCeilingGlass { get => _ceilingGlassShown; set => _ceilingGlassShown = value; }

        /// <summary>
        /// <c>save=&lt;path&gt;</c>: once the file opened on the command line is applied, write it out as F4 writes a level, then
        /// carry on. With the view switches above it is how a run shows that a switch is a view and not data: the same level
        /// saved with them on and off is the same file.
        /// </summary>
        public string SaveLevelOnStart { get; set; }

        #endregion

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
            string filePath = e.Files[0];
            Task.Run(() => DeserializeMapFromJsonFile(filePath));
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
            Info = new InfoRenderer(this, "Content/Fonts/segoeui") { DrawOrder = int.MaxValue, Visible = !StartWithTextHidden };
            Components.Add(Info);

            //The command line's camera, before the helper below takes the pose R returns to
            if (StartCameraPosition is Vector3 position) Camera3D.Position = position;
            if (StartCameraTarget is Vector3 target) Camera3D.Target = target;
            Camera3D.Recalculate();

            _cih = new CameraInputHelper(Camera3D, this);

            #region Controls

            //Every binding names the line of the key help that documents it (#1209), so a key cannot be bound and left
            //off the screen. The help draws EditorKeyHelp.All, in groups with the file keys first, not this table in
            //its own order: that was one 39-line block whose last five lines, load and save among them, were below the
            //bottom edge of every window.
            var actions = new List<ButtonAction>
            {
                Bind(EditorKeyHelp.MoveSelector, mgKeys.Up, () => _selector.Move(Vector3.Forward), Buttons.DPadUp),
                Bind(EditorKeyHelp.MoveSelector, mgKeys.Down, () => _selector.Move(Vector3.Backward), Buttons.DPadDown),
                Bind(EditorKeyHelp.MoveSelector, mgKeys.Left, () => _selector.Move(Vector3.Left), Buttons.DPadLeft),
                Bind(EditorKeyHelp.MoveSelector, mgKeys.Right, () => _selector.Move(Vector3.Right), Buttons.DPadRight),
                Bind(EditorKeyHelp.MoveSelectorVertically, mgKeys.PageUp, () => _selector.Move(Vector3.Up), Buttons.RightShoulder),
                Bind(EditorKeyHelp.MoveSelectorVertically, mgKeys.PageDown, () => _selector.Move(Vector3.Down), Buttons.LeftShoulder),

                Bind(EditorKeyHelp.PutBall, mgKeys.Space, () => _selector.PutBall(), Buttons.A),
                Bind(EditorKeyHelp.RemoveBall, mgKeys.Delete, () => _selector.RemoveBall(), Buttons.B),
            };

            //One row per direct-key colour: NumPad1..NumPad9 and Type1..Type9 are both consecutive values,
            //so the nine bindings are one loop over the name table rather than nine rows whose three digits
            //(key, type, name index) would have to be kept agreeing by hand.
            for (int i = 0; i < EditorKeyHelp.DIRECT_COLOUR_KEYS; i++)
            {
                BallType type = (BallType)(i + 1);
                actions.Add(Bind(EditorKeyHelp.PickColour, (mgKeys)((int)mgKeys.NumPad1 + i), () => SetBallType(type)));
            }

            actions.AddRange(new ButtonAction[]
            {
                //Thirteen types outgrew the numpad's digits (#152): brown, silver, navy and olive have no
                //direct key and are reached by cycling. The wrap makes the two keys a complete picker on
                //their own; the digits above stay as shortcuts into the first nine.
                Bind(EditorKeyHelp.CycleColour, mgKeys.Add, () => CycleBallType(+1)),
                Bind(EditorKeyHelp.CycleColour, mgKeys.Subtract, () => CycleBallType(-1)),

                Bind(EditorKeyHelp.Quit, mgKeys.Escape, Exit, Buttons.Back),
                Bind(EditorKeyHelp.Fullscreen, mgKeys.F11, () => SetGraphics(_graphics.IsFullScreen)),
                //Both overlays: the FPS and status lines (the shared InfoRenderer) and the key help
                Bind(EditorKeyHelp.HideText, mgKeys.F12, () => _keyHelp.Visible = Info.Visible = !Info.Visible),
                //Not F12, the Testbed's and the Game's shot key: here F12 has always hidden the text
                Bind(EditorKeyHelp.Screenshot, mgKeys.F8, () => _shots.Request()),
                //H for help; nothing else here had it (S, W, A, D, Q and E drive the camera)
                Bind(EditorKeyHelp.ToggleHelp, mgKeys.H, () => _keyHelp.Expanded = !_keyHelp.Expanded),

                Bind(EditorKeyHelp.FillMap, mgKeys.N, FullMapTest, Buttons.X),
                Bind(EditorKeyHelp.ClearMap, mgKeys.M, () => _map.Clear()),

                Bind(EditorKeyHelp.Views, mgKeys.D1, () => CenterViewOn(Vector3.Forward)),
                Bind(EditorKeyHelp.Views, mgKeys.D2, () => CenterViewOn(Vector3.Backward)),
                Bind(EditorKeyHelp.Views, mgKeys.D3, () => CenterViewOn(Vector3.Left)),
                Bind(EditorKeyHelp.Views, mgKeys.D4, () => CenterViewOn(Vector3.Right)),
                Bind(EditorKeyHelp.Views, mgKeys.D5, () => CenterViewOn(Vector3.Up)),
                Bind(EditorKeyHelp.Views, mgKeys.D6, () => CenterViewOn(Vector3.Down)),

                Bind(EditorKeyHelp.ResetCamera, mgKeys.R, () => _cih.RestartCamera()),
                //Not S, W, A, D, Q or E: those move the camera (see CameraInputHelper)
                Bind(EditorKeyHelp.CycleSky, mgKeys.B, SwitchSkyDome),
                //The help names the axis, not the members (#320's rule, and #376's): it says the current one is named
                //at the top, where the status line says it, so a new scene, material or kind cannot make it wrong
                Bind(EditorKeyHelp.CycleScene, mgKeys.V, SwitchScene),
                Bind(EditorKeyHelp.CycleStyle, mgKeys.L, SwitchBallStyle),
                Bind(EditorKeyHelp.CycleKind, mgKeys.K, CycleBallKind),

                //The view switches (#1210). G and I for the game's sky and the island, C for the ceiling: none of them
                //was taken (G was the scene-config panel's until #522)
                Bind(EditorKeyHelp.ToggleSetting, mgKeys.G, ToggleGameSky),
                Bind(EditorKeyHelp.ToggleSetting, mgKeys.I, ToggleIsland),
                Bind(EditorKeyHelp.ToggleSetting, mgKeys.C, ToggleCeilingGlass),

                Bind(EditorKeyHelp.SaveMap, mgKeys.F1, SaveJson),
                Bind(EditorKeyHelp.Open, mgKeys.F2, LoadJson),
                Bind(EditorKeyHelp.NewMap, mgKeys.F3, NewMap),
                Bind(EditorKeyHelp.SaveLevel, mgKeys.F4, SaveLevel),
            });

            _actions = actions.ToArray();

            //The key help is the editor's own (#1209); the shared overlay keeps the FPS line and the status line and
            //is given no hint text
            _keyHelp = new KeyHelpOverlay(this, "Fonts/segoeui") { DrawOrder = int.MaxValue, Visible = Info.Visible };
            Components.Add(_keyHelp);

            #endregion

            base.Initialize();
        }

        //One copy of the colour names in the editor, EditorKeyHelp's: the key help's colour line and the cycle keys'
        //on-screen feedback both read it, so a colour added to BallType shows up here by adding one row.
        private static readonly string[] BALL_TYPE_NAMES = EditorKeyHelp.BallTypeNames;

        //A binding and its line of the key help: the help's text is the action's description too
        private static ButtonAction Bind(KeyHelpRow help, mgKeys key, Action method, Buttons button = Buttons.None) =>
            new(key, button, method, help.Text);

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

            //One line per kind rather than one line for "not normal": the hint is the only place the editor
            //says what a kind DOES, and two kinds sharing the rock's sentence is exactly the way the ball-style
            //hint fell behind its own enum (#320).
            Info.CustomText = kind switch
            {
                BallKind.Rock => "Ball kind: rock (matches with nothing, no colour removes it)",
                BallKind.Transparent => "Ball kind: transparent (colourless until a shot lands beside it, "
                    + "then that shot's colour)",
                BallKind.Bomb => "Ball kind: bomb (a shot landing beside it destroys everything within two "
                    + "units, itself included)",
                BallKind.Zap => "Ball kind: zap (a shot landing beside it takes every ordinary ball of THAT "
                    + "SHOT's colour off the whole field)",
                //The acid's line was missing until #329 — #328 added the kind and not the hint, which is the
                //very drift this switch's own comment above was written against. One line per kind, still.
                BallKind.Acid => "Ball kind: acid (a shot landing beside it eats DOWNWARD, drilling a shaft "
                    + "through the cluster beneath it until it reaches a gap)",
                BallKind.Frozen => "Ball kind: frozen (an ordinary ball of this colour sealed in ice: nothing "
                    + "matches it until the player clears a group NEXT TO it)",
                BallKind.Infectious => "Ball kind: infectious (an ordinary ball of this colour that is SICK: "
                    + "after every shot it infects a healthy neighbour and hardens into a rock)",
                BallKind.Gravity => "Ball kind: gravity (an ordinary ball of this colour that PULLS on shots "
                    + "passing near it, bending their flight; needs open space around it to bend anything)",
                BallKind.Heavy => "Ball kind: heavy (an ordinary ball of this colour with twelve times the "
                    + "mass: what hangs UNDER it hangs visibly lower, so it needs a branch to weigh down)",
                BallKind.Buckshot => "Ball kind: buckshot (a clump of pellets nothing matches: cut what holds it "
                    + "up and it pours out; the level is not cleared while any hangs)",
                _ => "Ball kind: normal",
            };
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
                Exposure = PostProcessPipeline.DEFAULT_EXPOSURE,
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

            //linearVertexColors: the dome is drawn into the linear HDR target, so its baked gradient has to be converted from
            //sRGB once at load or the tonemapper reads a gradient of display values as radiance. The game does the same; the
            //editor used to skip it, drawing in gamma.
            _sky = new SkyDome(GraphicsDevice, _skyDomeNumber, linearVertexColors: true);

            //THE GAME'S SKY (#1210), built as the Game builds it: Sky.fx on the dome, its static half pushed once, the pass
            //that shades it at display resolution and draws it behind the opaque scene, and the heaps where the device can
            //hold their noise. With the switch off the dome goes back to its own BasicEffect, the plain gradient (SetViewSwitches).
            _skyEffect = Content.Load<Effect>("Shaders/Sky");
            _skyCameraPositionParam = _skyEffect.Parameters["CameraPosition"];
            _clouds.ApplyStaticParameters(_skyEffect);
            _skyPass = new SkyPass(GraphicsDevice, _sky);
            if (GraphicsSupport.VolumeTextures(GraphicsDevice)) _cloudVolumes = new CloudVolumes(GraphicsDevice, Content);

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
            _sceneRenderer = new SceneRenderer(GraphicsDevice, Content, _sceneSeedOffset) { SupersampleFactor = SUPERSAMPLE_FACTOR };
            _unitBox = new BoxMesh(GraphicsDevice, 1f, 1f, 1f);
            _cityConfig.Seed += _sceneSeedOffset;
            _cityConfig.NeonLayout.Seed += _sceneSeedOffset;

            //The game's clearing, the island's radius, so the towers stand where they stand in play (#1210: it was 60 here,
            //the towers ~2.3x as far out, from before the island was drawn in the editor)
            _city = new City(_cityConfig, neon: _scene == SceneKind.NeonCity, ArenaIsland.RADIUS);
            _cityIsNeon = _scene == SceneKind.NeonCity;
            _cityRenderer = new InstancedModelRenderer(GraphicsDevice, _unitBox, Vector3.One, _instancingEffect)
            {
                CityWindowBrightness = _cityConfig.WindowBrightness,
                CityConfig = _cityConfig,
                SpecularAmbientStrength = CITY_SPECULAR_AMBIENT
            };
            _rooftops = new CityRooftops(GraphicsDevice, _instancingEffect, _city, _cityConfig, SCENE_AMBIENT_INTENSITY,
                CityRooftops.DEFAULT_SEED + _sceneSeedOffset);
            _streets = new CityStreets(GraphicsDevice, Content.Load<Effect>("Shaders/CityStreets"), _city);

            //After the scene renderer, because the rig consults it for the scenes that state their own lighting. The cloud
            //hook is captured once (a method group at a per-frame call site builds a delegate each time) and asks the G
            //switch itself, so the scenes' shaders take the deck exactly while the game's sky is up (ApplyCloudsToScene).
            _rig = new SkyLightRig(_sceneRenderer) { CloudHook = ApplyCloudsToScene };

            //The level's sky (the scene's own until a level names one), up at once rather than faded in as the Game fades a
            //level's: a preview shows the sky it previews
            ApplySceneWeather();

            //The setting a session hangs a field in (#1210): the island with its drain and pit, the ceiling glass and its
            //rigging, the scene's point lights, and the lens that draws all of it in the game's frame. The island's ambient
            //is the scene's, as the hosts hand it.
            _island = new ArenaIsland(GraphicsDevice, _instancingEffect, SCENE_AMBIENT_INTENSITY);
            _ceilingPlate = new CeilingPlate(GraphicsDevice, _instancingEffect);
            _ceilingRigging = new CeilingRigging(GraphicsDevice, _instancingEffect);
            _sceneLights = new SceneLights(_instancingEffect);
            _gameCamera = new GameFrameCamera(Camera3D);
            FitCeilingToMap();

            //The forest's wood, planted on the very terrain the SceneRenderer above draws. After it, because the
            //component is built from that renderer's own forest config, and before the sky lighting below, since
            //fresh renderers have never been told the dome's palette. No stone texture handed in: the editor has
            //none of its own, so the component builds one for the boulders.
            //The launch's roll goes in as the Game and the Testbed hand it (#400): the offset above reached the
            //cities and their roofs here but not the wood, so the editor planted the same forest and the same
            //aurora wood every session while its own comment on _sceneSeedOffset said the wood rolled too. The planting
            //is the backdrop's since #962 (planted on that roll), which dresses it on High and Ultra and draws the
            //broadleaves in leaf there.
            _forestScatter = new ForestScatterRenderer(GraphicsDevice, _instancingEffect,
                (ForestSceneConfig)_sceneRenderer.GetSceneConfig(SceneKind.Forest), SCENE_AMBIENT_INTENSITY,
                planting: _sceneRenderer.ForestPlanting, broadleavesInLeaf: true);

            //The aurora's own wood, a second planting from its own config - see AuroraSceneConfig's class doc. The planting
            //is the backdrop's (#963), which drifts the snow round it, so this draws the trees the drifts were made for.
            _auroraScatter = new ForestScatterRenderer(GraphicsDevice, _instancingEffect,
                ((AuroraSceneConfig)_sceneRenderer.GetSceneConfig(SceneKind.Aurora)).Terrain, SCENE_AMBIENT_INTENSITY,
                planting: _sceneRenderer.AuroraPlanting);

            _forestFireflies = new ForestFireflies(GraphicsDevice, _instancingEffect,
                (ForestSceneConfig)_sceneRenderer.GetSceneConfig(SceneKind.Forest), SCENE_AMBIENT_INTENSITY,
                seed: ForestScatterRenderer.DEFAULT_SEED + _sceneSeedOffset);

            _forestLamps = new ForestLamps(GraphicsDevice, _instancingEffect, _sceneRenderer.ForestLamps, SCENE_AMBIENT_INTENSITY);

            //The switches as the command line left them: the dome's effect and the hole the terrain cuts for the island
            SetViewSwitches();

            ApplySkyLighting();

            _pipeline.EnsureTarget();

            _shots = new ScreenshotWriter(GraphicsDevice, "mapeditor", ShotSeconds);

            //Load a map or level handed on the command line (it stashes to _pendingLoad and lands next Update)
            if (!string.IsNullOrEmpty(StartupFilePath) && File.Exists(StartupFilePath))
                DeserializeMapFromJsonFile(StartupFilePath);
        }

        /// <summary>
        /// Cycles the environment backdrop the map is previewed against (the Testbed's NumPad2), so a map can be
        /// checked against every scene it might play in — every one of them since #380, which is what the
        /// sentence above had been claiming for ten scenes it could not reach.
        /// </summary>
        private void SwitchScene()
        {
            //Every scene, off the enum itself (#380). It walked a seven-long prefix until then — "the scenes a
            //map is authored against" — which was true when it was written and had stopped being true without
            //anyone deciding it: fifty of the campaign's hundred and ten levels are authored in space, the
            //dream, the cavern, the Moon and the volcano, all of them past that prefix. The cost was not only
            //that ten backdrops could not be previewed. Loading such a level put _scene past the cycle's end,
            //and the first V then restarted at the city with NO WAY BACK to the level's own scene except
            //reloading the file — in the one program whose whole job is previewing a map against every backdrop
            //it might play in. (#73 fixed the arithmetic of that restart, in both this copy and the Testbed's;
            //what it could not fix was the prefix itself.)
            //
            //Nothing here counts the scenes, which is the point and is the rule L already follows for materials:
            //a new kind is reachable the moment it is declared. There is no backwards key — B cycles all
            //twenty-two sky domes on one key already, so a wrap as long as the scene list is this program's own idiom rather
            //than a new burden, and a modifier would want a keyboard state CameraInputHelper does not hand out.
            _scene = SceneRenderer.NextScene(_scene);

            //⚠ The day city and the neon city are DIFFERENT CITIES since #471's follow-up — a second seed and
            //a skyline of its own — so V crossing between them re-runs the generator. An author previewing a
            //map in the neon city has to be shown the neon city.
            EnsureCityLayout(_scene == SceneKind.NeonCity);

            Info.CustomText = $"Scene: {SceneRenderer.SceneName(_scene)}";

            //Re-derive the rig: a scene may state its own lighting instead of the dome's, and V is a scene
            //change like any other. It was missing here until the call was added against the day the cycle
            //widened — and #380 is that day, so what was latent is now load-bearing: the cycle reaches scenes
            //that state their own rig, and without this they would be lit by the dome's.
            //
            //What this deliberately does NOT do is adopt the scene's default sky dome the way the Testbed's
            //switch and the game's SetScene both do. The dome is DATA here: F4 writes _skyDomeNumber into the
            //level file, and in play the level's own sky is applied after the scene's default and wins
            //(GameplayScreen.Session's SetSkyDome call). Setting it on a scene cycle would overwrite the
            //author's choice for a preview, which is a lie in the one direction that also destroys their work.
            ApplySkyLighting();

            //The weather is not data that way: a level that names its sky keeps it, and one that does not stands under the
            //new scene's own, as it would play there (#1210)
            ApplySceneWeather();
        }

        /// <summary>
        /// Puts up the sky the level plays under (<see cref="WeatherLooks.ForLevel"/>, the Game's rule): the loaded level's
        /// word, else the scene's own. At once, with no fade. It reads <see cref="_levelWeather"/> and never writes it, so
        /// what F4 saves is what was loaded.
        /// </summary>
        private void ApplySceneWeather()
        {
            //The city's config is this file's: the two cities are drawn through the shared instanced technique, not by a
            //scene shader, so the scene renderer has none to give for them
            SceneConfig config = _scene is SceneKind.City or SceneKind.NeonCity
                ? _cityConfig
                : _sceneRenderer.GetSceneConfig(_scene);

            _clouds.SetWeatherImmediately(WeatherLooks.ForLevel(_levelWeather, config?.Weather));
        }

        /// <summary>
        /// G (#1210): the game's sky (<c>Sky.fx</c>: the sun's disc, the cloud deck under the level's weather, the heaps, the
        /// deck's shadows) or the plain dome. A view: the level's sky and weather stay what they were.
        /// </summary>
        private void ToggleGameSky()
        {
            _gameSkyShown = !_gameSkyShown;
            SetViewSwitches();
            Info.CustomText = _gameSkyShown
                ? "Sky: the game's (sun, clouds, the level's weather)"
                : "Sky: the plain dome (no sun, no clouds)";
        }

        /// <summary>I (#1210): the island under the field (its stone, the glass drain, the pit) and the hole the terrain
        /// cuts for it, or neither.</summary>
        private void ToggleIsland()
        {
            _islandShown = !_islandShown;
            SetViewSwitches();
            Info.CustomText = _islandShown ? "Island: on" : "Island: off";
        }

        /// <summary>C (#1210): the ceiling glass the cluster hangs from, with its cables and winches, or neither.</summary>
        private void ToggleCeilingGlass()
        {
            _ceilingGlassShown = !_ceilingGlassShown;
            Info.CustomText = _ceilingGlassShown ? "Ceiling glass: on" : "Ceiling glass: off";
        }

        /// <summary>
        /// What the switches change beyond what <see cref="Draw"/> reads each frame: the dome's effect (<c>Sky.fx</c>, or
        /// null for the dome's own plain gradient) and the hole the solid terrains cut for the island
        /// (<see cref="ArenaIsland.TERRAIN_HOLE_RADIUS"/>, or none: with no island the ground stays whole under the field).
        /// </summary>
        private void SetViewSwitches()
        {
            _sky.Effect = _gameSkyShown ? _skyEffect : null;
            _sceneRenderer.TerrainHoleRadius = _islandShown ? ArenaIsland.TERRAIN_HOLE_RADIUS : 0f;
        }

        /// <summary>
        /// The scenes' half of the cloud deck (<see cref="SkyLightRig.CloudHook"/>): the deck's parameters with the game's sky
        /// up, a coverage of nothing with it down. Suppressed rather than skipped, because an effect keeps its last values
        /// between frames and a deck left on it would go on shadowing the ground under the plain dome
        /// (<see cref="CloudField.SuppressOn"/>).
        /// </summary>
        private void ApplyCloudsToScene(Effect effect)
        {
            if (_gameSkyShown) _clouds.ApplyTo(effect);
            else _clouds.SuppressOn(effect);
        }

        /// <summary>
        /// Fits the ceiling glass and its rigging to the field's footprint, when the field's size has changed since the last
        /// fit (a new map, a loaded one). Called every frame: a fitted plate is a new renderer, which has never been told the
        /// sky, so it is re-lit here, and nothing else needs to remember to call it.
        /// </summary>
        private void FitCeilingToMap()
        {
            if (_map.StageSizeX == _plateStageX && _map.StageSizeZ == _plateStageZ) return;

            _plateStageX = _map.StageSizeX;
            _plateStageZ = _map.StageSizeZ;
            _ceilingPlate.Fit(_plateStageX, _plateStageZ);
            _ceilingRigging.Fit(_plateStageX, _plateStageZ);
            _rig.ApplyToGlass(_ceilingPlate.Renderer);
        }

        /// <summary>
        /// Cycles what the map's balls are made of (#258) and applies it to the render set at once, so the
        /// preview is the answer rather than a description of it. It is a property of the map, so <c>F4</c>
        /// writes it into the level file beside the scene and the dome, and loading one brings it back.
        /// <para>
        /// On <c>L</c> for "look", which is what was free: S, W, A, D, Q and E drive the camera, B is the sky
        /// dome, V the scene, N fills the map and M clears it. There is no count of styles
        /// here — the cycle is <see cref="BallStyles.Next"/>'s, off the enum itself, so a third material cannot
        /// be added and left unreachable in the one program that exists to choose between them.
        /// </para>
        /// <para>
        /// <b>Neither does the on-screen hint name any (#320.)</b> It read "switch ball look (beach vinyl /
        /// glass bubble)" — honest at #258, when those were the only two — and #272's eight more left the
        /// editor's own help naming two materials out of ten and calling a cycle a toggle. It says what the key
        /// <i>does</i> now and points at <see cref="Info"/>'s line for what the current one <i>is</i>, which is
        /// the form <c>K</c> already uses for the ball kind and is the same argument as the enum-driven cycle
        /// above: nothing here counts the materials, so an eleventh cannot make it wrong.
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

        /// <summary>Re-runs the city generator when V crosses between the day city and the neon one, and
        /// nothing otherwise. The roofs and the streets follow through their own <c>Rebuild</c>.</summary>
        private void EnsureCityLayout(bool neon)
        {
            if (_city == null || neon == _cityIsNeon) return;

            _cityIsNeon = neon;
            _city = new City(_cityConfig, neon, ArenaIsland.RADIUS);
            _rooftops.Rebuild(_city, _cityConfig);
            _streets.Rebuild(_city);
        }

        /// <summary>
        /// Moves on to the next sky dome, which changes the lighting of the whole scene with it.
        /// </summary>
        private void SwitchSkyDome() => SetSkyDome(_skyDomeNumber % SKY_DOME_COUNT + 1);

        /// <summary>Loads sky dome <paramref name="number"/> (1 to <see cref="SkyDome.Count"/>) and relights the scene from it.</summary>
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
        /// Only the enrolment is the editor's own: the balls, the city, the wood and the lamps, and since #1210 the island
        /// (never its pit shaft, which no dome may bleach), the rigging and the ceiling glass, lit whether or not their switch
        /// shows them, so a switch turned on shows them in the sky they stand under. The city takes part like every other
        /// instanced object — its facades are dark, but its specular ambient reads the sky.
        /// </para>
        /// </summary>
        private void ApplySkyLighting()
        {
            //A scene that states its own rig — space, the dream, the cavern — has to be honoured here too, or a
            //level of one would draw the right sky and light its balls by the wrong sun, which is the one thing
            //this editor exists to prevent. Those scenes are reachable both by V (every scene since #380) and by
            //loading a LEVEL, which sets _scene from the level's own scene kind. The rig reads the override
            //itself, which is why the scene goes in with the dome.
            _rig.SetSky(_sky, _scene);

            foreach (InstancedModelRenderer renderer in _balls.Renderers) _rig.ApplyTo(renderer);
            _rig.ApplyTo(_cityRenderer);
            foreach (InstancedModelRenderer renderer in _rooftops.Renderers) _rig.ApplyTo(renderer);

            //Every variant of every scattered kind, or a spruce of the variant this missed would stand under the
            //light rig of whatever dome was up when it was made. The array the component hands back, walked
            //directly, for the reason BallRenderSet.Renderers gives above.
            foreach (InstancedModelRenderer renderer in _forestScatter.Renderers) _rig.ApplyTo(renderer);
            foreach (InstancedModelRenderer renderer in _auroraScatter.Renderers) _rig.ApplyTo(renderer);
            foreach (InstancedModelRenderer renderer in _forestLamps.Renderers) _rig.ApplyTo(renderer);

            //The island's cap, drum, drain glass and gold bands (its list leaves the pit out), by index: the aurora's step
            //re-runs this while drawing, and a foreach over the interface would box an enumerator each time
            IReadOnlyList<InstancedModelRenderer> island = _island.SkyLitRenderers;
            for (int i = 0; i < island.Count; i++) _rig.ApplyTo(island[i]);
            foreach (InstancedModelRenderer renderer in _ceilingRigging.Renderers) _rig.ApplyTo(renderer);

            //The ceiling glass stands against the sky itself, so its lights follow the sky's brightness (SkyLightRig.ApplyToGlass)
            _rig.ApplyToGlass(_ceilingPlate.Renderer);

            //And the wood's own pigments, which the rig above cannot reach — see ForestScatterRenderer.
            //ShiftTowardsSky (#108). This is the executable the symptom is quickest to see in: B cycles the
            //domes over a parked forest.
            _forestScatter.ApplySkyTint(_rig.KeyTint);

            //The deck's colours follow the dome, its lit side handed the very radiance the rig gives the scene, and the sun's
            //direction rides along: the disc, the silver lining and the shadow the instanced shader throws (as the Game and the
            //Testbed push it). Pushed with the plain dome up as well, where the deck's coverage is zero and it shades nothing.
            _clouds.ApplyDome(_skyEffect, _instancingEffect, _rig.SunDirection,
                _rig.SunRadianceTinted, _rig.ZenithLinear, _rig.HorizonLinear);

            //The aurora's own wood takes the scene's glow colour instead (DrawScene), not the rig's key
            //tint pushed here: it is another scatter, and the tint is the scene's own.
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
            _levelCrates = null;
            _levelSoftness = null;
            _levelWeather = null;
            SetBallStyle(BallStyle.Beach);
            ApplySceneWeather();

            Info.CustomText = $"New map {dialog.StageSizeX} x {dialog.StageSizeZ} x {dialog.Levels}";
        }

        protected override void Update(GameTime gameTime)
        {
            //A map or level dropped/opened on a background thread is applied here, on the main thread, before
            //the focus guard so it lands even if the window briefly lost focus during the drop. The first one applied
            //is the command line's file, which save= writes back out (before the guard too: a scripted run has no focus).
            if (ApplyPendingLoad() && SaveLevelOnStart != null)
            {
                SaveLevelTo(SaveLevelOnStart);
                SaveLevelOnStart = null;
            }

            if (!IsActive) return;

            //Drives the scene motion (waves, wind, birds, snow) off the wall clock, like the game's pulse
            _sceneSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;

            _cih.RegisterCurrentInputState();

            //Nothing arbitrates the input any more: the editor had an in-engine GUI (the live scene-config
            //panel, #33/#45) whose focused widget or hovered panel used to stand the hotkeys and the camera
            //down, and #522 removed it — the editor's keys and camera are the only readers of the keyboard and
            //the mouse now, the WinForms dialogs being modal.
            foreach (var action in _actions) if (_cih.PressedOnce(action.Key, action.Button)) action.Method();

            //No circular camera movement: its NumPad7/9 orbit keys are ball types here (7 recoloured the
            //selector on every orbit press long before #152 put orange on 9). Mouse rotation and D1-D6 views
            //cover what the orbit did.
            _cih.CameraMovement(gameTime, allowCircularMovement: false);
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

            SaveLevelTo(filePath);
        }

        /// <summary>
        /// Writes the level to <paramref name="filePath"/>: F4's write, and <c>save=</c>'s. Only what a level IS goes in; the
        /// view switches (G, I, C) are not read here, so a level saved with any of them off is the file it is with them on.
        /// </summary>
        private void SaveLevelTo(string filePath)
        {
            Level level = new()
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                Author = _levelAuthor,
                SkyDome = (byte)_skyDomeNumber,
                Scene = _scene,
                Music = _levelMusic,
                Crates = _levelCrates,
                Softness = _levelSoftness,
                Weather = _levelWeather,
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
                Task.Run(() => DeserializeMapFromJsonFile(filePath));
            }
        }

        private void EnsureNotFullScreen()
        {
            if (!_graphics.IsFullScreen) return;

            SetGraphics(true);
        }

        /// <summary>
        /// Parses a map or level file into NEW objects and parks them in <see cref="_pendingLoad"/> for
        /// <see cref="ApplyPendingLoad"/> — it runs on a worker for a drop or the open dialog, so it must not
        /// write anything the render thread reads (#573). A file that will not parse leaves the slot alone.
        /// </summary>
        private void DeserializeMapFromJsonFile(string filePath)
        {
            try
            {
                //A level file (format marker "bs3d-level") carries a map plus the scene and sky that reproduce
                //its look; a plain map file carries just the layout. Both use .json, so the loader probes.
                if (Level.IsLevelFile(filePath))
                {
                    Level level = Level.Load(filePath);
                    lock (_pendingLoadLock) _pendingLoad = new PendingLoad(level, null);
                    return;
                }

                Stopwatch stopwatch = new();
                stopwatch.Start();
                BallsMap map = new(filePath);
                stopwatch.Stop();
                Console.WriteLine($"Deserialize JSON (ms): {stopwatch.ElapsedMilliseconds}");

                //BallsMap's file constructor returns an empty shell (no field, no array) for a file whose ball
                //data is null, where the in-place DeserializeJson this replaced left the current map as it was.
                //It is refused here so it cannot be swapped in and walked by Draw.
                if (map.Levels == 0) throw new InvalidDataException("the file carries no ball data");

                lock (_pendingLoadLock) _pendingLoad = new PendingLoad(null, map);
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
        /// Applies what <see cref="DeserializeMapFromJsonFile"/> parsed, on the main thread. A no-op when
        /// nothing is pending. A plain map swaps in its layout and resets what a map file does not carry (theme,
        /// author, ball style); a level also switches to the scene backdrop it names and sets the sky dome — so a
        /// level previews in the editor exactly the way it plays. Mirrors the Testbed's LoadLevel, minus the
        /// physics/cannon/camera the game derives. Whether a file was applied comes back, false for nothing pending and for
        /// a level that would not apply (so <c>save=</c> never writes the map that was up before it).
        /// </summary>
        private bool ApplyPendingLoad()
        {
            PendingLoad pending;
            lock (_pendingLoadLock) { pending = _pendingLoad; _pendingLoad = null; }
            if (pending == null) return false;

            if (pending.Level == null)
            {
                //The editor works in raw grid coordinates (the selector does), so the map is left uncentered.
                //The loaded map may have different play field dimensions, which the selector and outline follow.
                _map = pending.Map;
                _selector.UpdateBallsBap(_map);
                _aabb.FitToMap(_map);

                //A plain map carries no theme, author or ball style — F4 must not write the previous level's
                //onto it
                _levelMusic = null;
                _levelAuthor = null;
                _levelCrates = null;
                _levelSoftness = null;
                _levelWeather = null;
                SetBallStyle(BallStyle.Beach);
                ApplySceneWeather();
                return true;
            }

            Level level = pending.Level;
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
                _levelCrates = level.Crates;
                _levelSoftness = level.Softness;
                _levelWeather = level.Weather;
                SetBallStyle(level.Balls ?? BallStyle.Beach);

                //The level's dome wins over whatever is up (the sky key still cycles freely from here)
                SetSkyDome(Math.Clamp((int)level.SkyDome, 1, SKY_DOME_COUNT));

                //And its weather, or its scene's (#1210): the sky it plays under, for the game's sky to show
                ApplySceneWeather();

                string levelName = string.IsNullOrEmpty(level.Name) ? "Untitled" : level.Name;
                Info.CustomText = $"Level: {levelName}, scene {_scene}, sky {_skyDomeNumber}, "
                    + $"{BallStyles.ToName(_ballStyle)} balls";
                Console.WriteLine($"[level] Loaded '{levelName}': scene={_scene}, sky={_skyDomeNumber}, "
                    + $"style={BallStyles.ToName(_ballStyle)}, balls={_map.GetBallsCount()}");
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[level] Failed to apply level: {e.Message}");
                return false;
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            //THE GAME'S FRAME (#1210). The field's hang is taken once, here, for everything drawn through the game-frame lens
            //this frame: the sky, the scene, the island, the glass and the balls. The selector, the outline and the axis cross
            //stay on the editor's own camera, in the grid frame they work in; the two lenses differ by exactly the hang, so
            //their draws meet on the screen and in the depth buffer.
            _gameCamera.Follow(_map);
            FitCeilingToMap();
            Matrix plateWorld = Matrix.CreateTranslation(0f, CeilingPlate.CentreYAbove(_gameCamera.FieldTopY), 0f);

            //The map's balls, through the shared set, hung where a session hangs them, and collected BEFORE the shadow pass,
            //which casts them as the Game casts its cluster. BeginFrame hands back a ref struct, which is what keeps the
            //once-per-frame walk from being anything else: it cannot be stored, so it cannot outlive the frame it belongs
            //to; the _balls.Draw below closes it. A static map has nothing to ease towards, so AddMap simply reads the grid.
            BallDrawFrame ballFrame = _balls.BeginFrame(_gameCamera);
            ballFrame.AddMap(_map, _gameCamera.Shift);

            //The ceiling's glass shades what hangs under it (#553), stated for this frame's shadow pass while it is shown
            if (_ceilingGlassShown) _sceneRenderer.CastCeilingShadow(_ceilingPlate.Renderer, plateWorld.Translation);

            //The sun's shadow maps come first (#469): their own target, before the scene's target is bound (see
            //SceneRenderer.DrawShadowMaps on why). The effect is handed over so everything drawn through it receives (#470),
            //and the casters are the editor's own objects, as the Game casts its own (DrawShadowCasters).
            _sceneRenderer.DrawShadowMaps(_scene, _gameCamera, _rig.SunDirection, _instancingEffect, _drawShadowCasters ??= DrawShadowCasters);

            //THE SKY. A sky-replacing scene (space, the dream, the cavern, the Moon...) is its own sky and has no dome either way.
            //Under a dome, the game's sky (G) is the Game's: the deck on the wall clock the scene runs on, handed to the sky and
            //to the instanced effect from the one field, so the cloud seen and the shadow it throws are one cloud; then the
            //sky's own targets before the scene's is bound (the heaps marched, the dome shaded once a display pixel). With it
            //off, or no dome, the deck's coverage is zeroed on the instanced effect, so nothing is shadowed by a deck nobody sees.
            bool domed = !SceneRenderer.ReplacesSky(_scene);
            bool gameSky = domed && _gameSkyShown;
            bool skyBehind = gameSky && SkyPass.BehindScene(_pipeline.SceneTarget);

            if (gameSky)
            {
                _clouds.Time = _sceneSeconds;

                //The ground's light on the deck (the volcano's crater, #509), stated every frame so no scene inherits the last
                if (_sceneRenderer.TryGetGroundGlow(_scene, _sceneSeconds, out Vector3 glowAt, out Vector3 glowColor, out float glowRange))
                    _clouds.SetGroundGlow(glowAt, glowColor, glowRange);
                else _clouds.ClearGroundGlow();

                _clouds.ApplyTo(_skyEffect);
                _clouds.ApplyTo(_instancingEffect);
                _skyCameraPositionParam.SetValue(_gameCamera.Position);
                _cloudVolumes?.ApplyToSky(_skyEffect, _clouds, CloudVolumesShown);

                _skyPass.Prepare(_gameCamera, _clouds, _skyEffect, CloudVolumesShown ? _cloudVolumes : null, _pipeline.SceneTarget,
                    _pipeline.SupersampleFactor, SkyPass.DisplayResolutionPays(_scene, sceneryShown: true));
            }
            else
            {
                _clouds.SuppressOn(_instancingEffect);

                //No game sky this frame, so no target of the sky's own held for it
                _skyPass.Release();
            }

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
            GraphicsDevice.Clear(domed ? new Color(_rig.HorizonLinear) : Color.Black);

            //The plain dome over the clear, depth off, as the editor has always drawn it; the game's sky here only where the
            //target is multisampled (SkyPass.BehindScene), which the editor's supersampled one never is
            if (gameSky && !skyBehind) _skyPass.Draw(_gameCamera, behindScene: false);
            else if (domed && !gameSky) _sky.Draw(_gameCamera);

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

            SceneFrame sceneFrame = _rig.BuildSceneFrame(_gameCamera, _sceneSeconds);

            //The scene's own point lights (the campfire, the neon ring, the forest's lamps, space's planetshine) onto the
            //shared instanced effect, so the island and the balls take them as in the game, on the clock the scene runs on
            _sceneLights.Apply(_scene, _sceneRenderer, _cityConfig.NeonLook, _sceneSeconds);

            //The island's depth before the environment, as the Game lays it (#1098): every pixel of ground it stands in front
            //of is turned away unshaded, and the island itself is drawn after the environment as it always was. Its event
            //(the volcano's eruption in the basalt's cracks, #535) first, before its first draw of the frame.
            if (_islandShown)
            {
                _island.EventGlow = _scene == SceneKind.Volcano ? _sceneRenderer.VolcanoEruption(_sceneSeconds) : 0f;
                if (_sceneRenderer.EnvironmentFollowsIslandDepth(_scene, _pipeline.SceneTarget))
                    _island.DrawIslandDepth(_gameCamera, _sceneEffectParams, _scene);
            }

            //The environment's opaque half: the city or the backdrop's first half, and the forest's wood standing on it
            DrawSceneOpaque(sceneFrame);

            //THE GAME'S SKY, BEHIND THE OPAQUE SCENE (#1104), as the Game draws it: depth-read at the far plane, so every pixel
            //the ground, the wood, the city and the island's laid depth own is turned away before Sky.fx runs. It puts the
            //scene's states back on its way out. The city's street band at the far plane is painted back after it.
            if (skyBehind)
            {
                _skyPass.Draw(_gameCamera, behindScene: true);
                if (_scene == SceneKind.City || _scene == SceneKind.NeonCity) _streets.DrawAtFarPlane();
            }

            //The aurora's wood after the sky, where the Game draws it (it is its own sky there, so this is about the light
            //rig's step: see DrawAurora)
            if (_scene == SceneKind.Aurora) DrawAurora();

            //The backdrop's second half (#1098): what it blends over the sky and the ground without writing depth (the sea,
            //the storm, the fountains, the moons). Then the outback's heat shimmer at the skyline (#1135), before the island.
            _sceneRenderer.DrawEnvironmentBlended(_scene, sceneFrame);
            _sceneRenderer.DrawHorizonRefraction(_scene, sceneFrame, _pipeline);

            //The round island, opaque, over the depth it laid before the environment: its stone and drum, then the dark well
            //behind the glass drain in the solid-terrain scenes (I)
            if (_islandShown)
            {
                _island.DrawIsland(_gameCamera, _sceneEffectParams, _scene);
                _island.DrawPit(_gameCamera, _sceneEffectParams, _scene);
            }

            //The translucent baseline stated again: a backdrop or the island's slices may leave another behind, and the
            //balls, the outline and the glass below inherit it (the Game's #667 restatement)
            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            //The balls collected at the top of the frame, on the scene's wall clock
            _balls.Draw(_sceneSeconds);

            //What the plate hangs from (#896), opaque, with the scene: the Testbed's place for it, the editor's pane bending
            //nothing either (C)
            if (_ceilingGlassShown) _ceilingRigging.Draw(_gameCamera, plateWorld, _sceneEffectParams, _sceneRenderer, _scene);

            //The outline is translucent, so it is drawn over the opaque scene and does not write depth. In the grid frame,
            //through the editor's own camera.
            GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _aabb.Draw(Camera3D);
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            //The drain's gold bands and its glass (I), then the ceiling's pane (C), over everything already in the frame. The
            //editor's pane is the plain translucent one the Testbed draws: the Game alone bends the frame behind it (#541).
            if (_islandShown) _island.DrawGlass(_gameCamera, _sceneEffectParams, _scene);
            if (_ceilingGlassShown) _ceilingPlate.Renderer.Draw(_gameCamera, plateWorld, _sceneEffectParams);

            //The savanna's fires, the meadow's water and the Grid's walls of light, then falling snow (mountain), which
            //settles in front of everything; each a no-op for every other scene
            _sceneRenderer.DrawGrounded(_scene, sceneFrame);
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

            //Last, after the components have drawn the text, so a shot is the frame as presented. On the draw's own
            //clock: Update stands still while the window is not active, and a scheduled shot is taken without focus.
            _shots.Service((float)gameTime.TotalGameTime.TotalSeconds, _scene);
        }

        /// <summary>Whether the heaps stand over the deck this frame: where the Game draws them (<c>BS3DGame.CloudVolumesShown</c>),
        /// on the authored detail the editor draws (<see cref="SceneRenderer.FineMeshes"/>) under a dome.</summary>
        private bool CloudVolumesShown => _cloudVolumes != null && _sceneRenderer.FineMeshes && !SceneRenderer.ReplacesSky(_scene);

        /// <summary>
        /// The environment's opaque half, through the game-frame lens: the city's buildings (lit by the sky rig, neon-lit for
        /// the neon scene) or the backdrop's first half (<see cref="SceneRenderer.DrawEnvironmentOpaque"/>), and the forest's
        /// wood, fireflies and lamps standing on it. Everything here writes depth, which the game's sky behind the scene needs.
        /// </summary>
        private void DrawSceneOpaque(in SceneFrame frame)
        {
            if (_scene == SceneKind.City || _scene == SceneKind.NeonCity)
            {
                bool neon = _scene == SceneKind.NeonCity;
                _cityRenderer.CityNeon = neon ? 1f : 0f;
                _cityRenderer.CityWindowBrightness = neon ? _cityConfig.NeonLook.WindowBrightness : _cityConfig.WindowBrightness;
                //Frustum-culled and ordered near to far, as the game draws it — see City.PrepareVisible
                int visibleBuildings = _city.PrepareVisible(_gameCamera);
                _cityRenderer.Draw(_gameCamera, _city.Visible, visibleBuildings, _sceneEffectParams);
                _rooftops.Draw(_gameCamera, neon, frame.Time);
                _streets.Draw(frame, _cityConfig.Streets, neon, _cityConfig.NeonLook);
            }
            else
                //The target goes in so the dream, the cavern and the big top can be shaded at the back
                //buffer's size and scaled up (#155, #1096) — the editor draws the scenes exactly as the game does, this one included.
                _sceneRenderer.DrawEnvironmentOpaque(_scene, frame, _pipeline.SceneTarget);

            //The forest's scattered trees, boulders and stumps, after the terrain they stand on — with depth, or
            //they would draw through it. The state is the caller's, and it is already the game's: alpha blend,
            //depth test and write and counter-clockwise culling into the supersampled HDR target. The component
            //touches none of it, so the balls drawn after this are unaffected.
            if (_scene == SceneKind.Forest) _forestScatter?.Draw(_gameCamera, _sceneRenderer.FineMeshes);

            //The forest's firefly-like blinking lights (#487), on the same wall clock the roof beacon
            //flashes on
            if (_scene == SceneKind.Forest) _forestFireflies?.Draw(_gameCamera, frame.Time);

            //The old lamps on their poles round the island (#1166)
            if (_scene == SceneKind.Forest) _forestLamps?.Draw(_gameCamera, frame.Time);
        }

        /// <summary>The aurora's own wood, under the scene's own tint and the light rig's step, where the Game draws it: after the sky.</summary>
        private void DrawAurora()
        {
            //Re-tinted here rather than in ApplySkyLighting (see there), on the scene's own tint: constant since
            //#963, so ApplySkyTint's value guard encodes the wood once.
            _auroraScatter?.ApplySkyTint(_sceneRenderer.AuroraGlowColor(_sceneSeconds));

            //The balls take the curtains' light through the scene's own rig, its back light following the arcs;
            //the rig steps rather than slides, so this re-lights only on a step.
            if (_rig.StepSceneLight(_sceneSeconds)) ApplySkyLighting();

            _auroraScatter?.Draw(_gameCamera, _sceneRenderer.FineMeshes);
        }

        /// <summary>
        /// What the editor casts into the sun's shadow map: what it draws, on the conditions it draws it, as the Game's own
        /// casters do (<c>BS3DGame.DrawShadowCasters</c>): the island while it is shown (I), the balls, the forest's wood and
        /// lamps, the aurora's wood, and the cities' towers (all of them, not the frustum's: a tower off the side of the
        /// screen is the one whose shadow falls across the street in view). The backdrop's own planting (the savanna's
        /// scatter, the beach's palms) casts from inside the renderer. Handed to <see cref="SceneRenderer.DrawShadowMaps"/>
        /// with the map's target bound and its states set.
        /// </summary>
        private void DrawShadowCasters(Matrix shadowViewProjection)
        {
            if (_islandShown) _island.DrawShadow(shadowViewProjection);
            _balls.DrawShadow(shadowViewProjection);

            if (_scene == SceneKind.Forest) _forestScatter?.DrawShadow(shadowViewProjection, _sceneRenderer.FineMeshes, _gameCamera);
            if (_scene == SceneKind.Forest) _forestLamps?.DrawShadow(shadowViewProjection);
            if (_scene == SceneKind.Aurora) _auroraScatter?.DrawShadow(shadowViewProjection, _sceneRenderer.FineMeshes, _gameCamera);

            if (_scene == SceneKind.City || _scene == SceneKind.NeonCity)
                _cityRenderer.DrawDepth(shadowViewProjection, _city.Buildings, _city.Buildings.Length);
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

            //Borderless, not a DXGI mode switch - the Game's #157 fix, which never reached this copy (#400):
            //minimizing exclusive fullscreen tears down the swap chain's fullscreen state and the window never
            //came back. The back buffer is the display's size above either way, so the picture is the same.
            _graphics.HardwareModeSwitch = false;

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
            _rooftops?.Dispose();
            _streets?.Dispose();
            //Every mesh, renderer and procedural texture of the forest scatter, in one call — its stone texture
            //included, the editor having handed it none of its own
            _forestScatter?.Dispose();
            _auroraScatter?.Dispose();
            _forestFireflies?.Dispose();
            _forestLamps?.Dispose();
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
            //leaving the selector working on the orphaned old map). It runs on the main thread, from the key
            //handler: it used to be started as a Task and filled the live _map, the selector and the overlay
            //text while Draw read them (#573). The fill is one pass over the field and prints what it took below.
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
