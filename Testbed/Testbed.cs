using BepuPhysics;
using BepuPhysics.Collidables;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Input;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Physics;
using Prazsky.Core;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Testbed.Diagnostics;
using mgKeys = Microsoft.Xna.Framework.Input.Keys;

namespace Testbed
{
    public partial class Testbed : Game
    {
        private BasicCamera3D _camera;

        #region Instanced ball rendering (issues #19 and #28)

        //Everything it takes to draw balls is BallRenderSet's since #76: the three procedural sphere LODs and
        //the distances they are picked by, the three renderers carrying the albedo, the beach-ball gores, the
        //emission, the translucency and the heartbeat, the (type × LOD) instance buckets and the one walk that
        //issues them. It stood here, in the Game and in the map editor — with the bucket bookkeeping written
        //out a fourth time inside this file alone. The neighbour-based ambient occlusion (issue #40) went with
        //it: its strength, the twelve-occluder maximum and the one function that DIVIDES the direction sum by
        //that maximum, which is what makes handing the shader a raw sum impossible. GroundHeight is the
        //component's own default — ArenaIsland.TOP_Y, the island every ball here hangs over — so the balls'
        //bellies still darken over the stone with nothing said about it here.
        private BallRenderSet _balls;

        //The one walk over the simulated population — the hanging cluster, the shots in flight and the released
        //balls on their way down — read off their bodies' poses, shaded by what the grid says is packed around
        //them, and offset by whatever is left of an arrival glide. ClusterCollector's since #76, along with both
        //time constants; it stood here as CollectBallInstances and in the Game. It advances state that lives on
        //the ball itself, which is why the walk is not this file's to write any more: see its remarks and
        //BallRenderSet.BeginFrame on why every ball must be visited exactly once a frame. No ripple hook — the
        //landing ripple is the Game's, and a null one costs one predicted branch per ball.
        private readonly ClusterCollector _collector = new();

        private float _pulseSeconds;

        //Bodies visited by the last collection, held because the autoshoot line logs it from Update while the
        //walk runs in Draw. The instances actually put out and their split by LOD level are not counted here at
        //all — BallRenderSet.DrawnCount and .LodTotals are those. There is no frustum culling (measured as
        //saving nothing on this scene), so the drawn figure differs from this one by the magazine preview and
        //not by any cull.
        private int _collectedBalls;

        #endregion

        #region Scene object rendering (same lit shader as the balls, drawn one instance at a time)

        private Effect _instancingEffect;

        /// <summary>
        /// The drawn glass ceiling the cluster hangs from: a procedural translucent box rebuilt at the exact
        /// field size of the loaded map (no model asset, no non-uniform scaling of a fixed mesh). The plate and
        /// its figures are <see cref="CeilingPlate"/>'s since #75, the game having held the same ones; the
        /// kinematic body it agrees with is still <see cref="_ceiling"/> below, and still this file's.
        /// </summary>
        private CeilingPlate _ceilingPlate;

        /// <summary>
        /// Lighting parameters shared by all scene objects; the ambient color is set by
        /// <see cref="ApplySkyLighting"/>, zero specular keeps each mesh part's own material specular.
        /// </summary>
        private readonly BasicEffectParams _sceneEffectParams = new(Vector3.One * SCENE_AMBIENT_INTENSITY, Vector3.Zero, 0f, Vector3.Zero);

        #endregion

        private KinematicBody _ceiling;
        private TypedIndex _ceilingShapeIndex;

        /// <summary>
        /// Field size the ceiling is fitted to before a map is loaded. A <b>stage</b> size, like the one a map
        /// reports: the plate's own footprint is this plus <see cref="CeilingPlate.FOOTPRINT_MARGIN"/>, and both
        /// the drawn plate and the collidable box take it from <see cref="CeilingPlate.FootprintFor"/>, so the
        /// two cannot be given different numbers.
        /// </summary>
        private static readonly float DEFAULT_CEILING_STAGE_SIZE = 9f;

        //The physics session's own hardware in one object: the worker pool the solver runs on, the buffer pool
        //everything in the simulation is allocated from, the simulation itself and the contact stream wired into
        //its narrow phase — four objects with two build orders and one teardown order between them, all of them
        //PhysicsWorld's since #76 (it stood here and in the Game, value for value). One world for the whole run:
        //this executable swaps maps inside a live simulation (RemoveCurrentBallsStructure) rather than building
        //one per level as the Game does, and neither lifetime is written into the component.
        private PhysicsWorld _world;

        private PhysicsBall[,,] _physicsBalls;
        private BallsMap _map;

        //Field size of the empty map installed at startup when no map is given on the command line, so the game
        //is playable straight away — mouse aim and precise aim are gated on a map existing (see UpdateCannon).
        //It starts empty: shooting up attaches balls to the ceiling and builds a cluster from nothing, shooting
        //down drains through the funnel. 10×10×16 matches Full.json, the size the game camera fit is tuned against.
        private const byte DEFAULT_EMPTY_MAP_X = 10;
        private const byte DEFAULT_EMPTY_MAP_Z = 10;
        private const byte DEFAULT_EMPTY_MAP_LEVELS = 16;

        private CameraInputHelper _cih;

        private SkyDome _sky;
        private byte _skyModelNumber = 1;

        //An explicit "sky=<n>" on the command line pins the starting dome over a startup level's own dome (the
        //dome-testing workflow in CLAUDE.md relies on it). Consumed once the startup map/level has loaded, so a
        //level opened at runtime (F2/drag-drop) still takes its own dome.
        private bool _skyFromCommandLine;

        //Testing: "weather=<name>" pins the sky over every scene, so five skies can be judged over one
        //backdrop. Null when the switch is absent, which is every run but a comparison.
        private string _weatherFromCommandLine;

        /// <summary>Number of sky palettes the game cycles through — <see cref="SkyDome.Count"/>'s.</summary>
        private const byte SKY_DOME_COUNT = SkyDome.Count;

        private ButtonAction[] _actions;

        private bool _simulate = true;
        private bool _draw = true;
        private bool _gameMode = false;
        private bool _slowSimulation = false;

        //How far back the game camera stands and how high it aims - both solved per map and per display by
        //GameCameraFit.Solve rather than tuned, because both of its inputs move underneath a fixed number. Every
        //dial of that solve is GameCameraFit's own since #76 - the camera's height below the trunnions (and the
        //measured argument for why so little of the angle onto the cluster is the camera's to give), the frustum
        //fraction the fit may fill, the gun's stand-off in front of the lens and the two lower bounds that
        //override it - having stood here and in the Game with every figure identical. The defaults below only
        //cover the frames before the first map is loaded.
        private float _gameCameraDistance = 37f;
        private float _gameCameraTargetY = 3f;

        //Precise-aim (ADS) sub-mode of game mode: hold the right mouse button (or the gamepad left trigger) to
        //lean in over the barrel and look straight down the aim, so the map reads clearly and the shot goes where
        //the crosshair is. The lens, its dials and the reversible blend that eases 0->1 while held (0 == the exact
        //game-mode overview pose, bit for bit, so an interrupted hold never snaps) are PreciseAim's since #76 - it
        //stood here and in the Game, every figure identical. What stays here is where the pose is applied and
        //what it is gated on.
        private readonly PreciseAim _preciseAim = new();

        //Aiming the gun from the captured cursor and the pad's right stick, both dials and all the arithmetic
        //shared with the Game since #76. It holds the "a captured frame has been seen" flag that skips the first
        //delta, so acquiring the cursor never yanks the aim.
        private readonly MouseAim _mouseAim = new();

        //Whether the window was active on the previous frame. Edge-driven input (the button actions and the
        //game-mode LMB fire) is skipped the frame focus returns, so the click that refocuses a windowed game is
        //not read as a fresh press against the input state frozen while the window was inactive (an unintended shot).
        private bool _wasActive = true;

        #region Game mode transition animation

        private bool _gameModeAnimStarted = false;
        private bool _freeModeAnimStarted = false;
        private float _gameModeAnimStep = 0f;

        private static readonly float ANIMATION_SPEED = Constants.THOUSANDTH;

        private Vector3 _beforeAnimationPosition = Vector3.Zero;
        private Vector3 _beforeAnimationTarget = Vector3.Zero;

        //Leaving game mode straight from precise aim needs the pose eased out, not snapped: _freeExitFromAds says
        //the free-mode exit should Lerp position/target/FOV from the leaned ADS pose (captured here) to the overview
        //pose, rather than only widening the FOV. A plain overview->free exit leaves both false and is unchanged.
        private float _beforeAnimationFov = 0f;
        private bool _freeExitFromAds = false;

        #endregion

        #region Graphics

        private GraphicsDeviceManager _graphics;

        private InfoRenderer _info;

        /// <summary>
        /// Chosen so the daylight domes land at roughly the brightness the gamma-space renderer used to
        /// show. It is a starting point for a rig that was lit by eye in the wrong space, not a
        /// photometric value — the whole lighting rig wants re-balancing now that it composes correctly.
        /// </summary>
        private const float DEFAULT_EXPOSURE = 1.1f;

        /// <summary>
        /// The scene renders into a target this many times larger per axis and is box-filtered down on
        /// the way to the back buffer. The balls' relief is the reason: it is a high-frequency signal
        /// evaluated per pixel, so raising the sampling rate is the only thing that keeps its fine
        /// octaves — which band-limit themselves against the pixel footprint — alive and sharp instead
        /// of quietly fading out. 1 disables it and hands the antialiasing back to MSAA.
        /// </summary>
        private readonly int _supersampleFactor;

        //The HDR scene target, the bloom pyramid, the tonemap resolve and every cached parameter — one
        //shared copy for all three executables (Prazsky.Core.Render.PostProcessPipeline, #74). What stays
        //here is the Testbed's own look figures, passed in once at load.
        private PostProcessPipeline _pipeline;

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

        /// <summary>How wide a patch of sky the ambient reads, roughly one cloud across. A property of the
        /// cloud field's own scale, which is why it stayed here when the overcast palette and its response
        /// moved onto <see cref="SkyLightRig"/>.</summary>
        private static readonly float OVERCAST_SAMPLE_RADIUS = 320f;

        //The cloud LOOK values that used to stand here are CloudField's own since #75 — they were identical to
        //the game's to the last digit, which is the drift a shared field should never have been able to have.
        //The weather's shape (plane, scale, wind, coverage) was already the field's. The one figure that went
        //elsewhere is the lit side's radiance: it is the sun's, not the cloud's, so it is SkyLightRig.SUN_RADIANCE
        //and reaches the deck as ApplyDome's argument — the same number SceneFrame carries, which is the whole
        //point (see SkyLightRig.SunRadianceTinted).

        #endregion

        #region City, island and funnel (the city is the default of the seven scenes)

        private City _city;
        private BoxMesh _unitBox;
        private InstancedModelRenderer _cityRenderer;
        private CitySceneConfig _cityConfig = new();
        //The whole arena the gun stands on: the stone cap and concrete drum of the round island, the glass drain
        //bored through its middle, the two gold beads that ring the drain's circles, and the dark pit shaft that
        //backs the glass in the solid-terrain scenes. Every mesh, texture, renderer and figure of it is
        //ArenaIsland's since #75 — the game had a copy of the lot, value for value. The frame sequence is still
        //this file's: the three DrawIsland/DrawPit/DrawGlass slices are placed by hand below.
        private ArenaIsland _island;

        //The forest's scattered trees, boulders and stumps — the wood this executable never drew. The terrain
        //under it was always the shared SceneRenderer's, so the glade was here from the day the scene was built
        //and only the Game had the trees standing in it; ForestScatterRenderer is that piece hoisted (#75) and
        //this is the call site it was hoisted for. Every texture, mesh variant, renderer, matte material and
        //encoded tint is the component's; where the draw sits in the frame and the scene gate on it are this
        //file's, as the island's slices are.
        private ForestScatterRenderer _forestScatter;

        //Which environment the arena stands in. City is the default; Sea, Savanna, Desert, Mountain, Meadow and
        //NeonCity swap the city (and only the city) for open water, a savanna, a Sahara of dunes, a snowy range,
        //a flowering meadow, or the same city lit up in neon — the round island stays in all seven.
        //NumPad2 cycles them.
        //The five self-lit backdrops (sea/savanna/desert/mountain/meadow), plus the circling birds and falling
        //snow, live in the shared SceneRenderer so the map editor draws them exactly as the game does. The city
        //below stays here: its buildings go through the shared InstancedModel city technique and are lit by
        //this scene's rig like every other instanced object, and its arena floor is tied to the ground blocks.
        private SceneKind _scene = SceneKind.City;
        private SceneRenderer _sceneRenderer;

        //The sea mirrors the sky, so it reads best under a moody dome rather than the bright default: a sunny
        //sky gives a bright, breezy sea, not a stormy one. Entering the sea scene (at startup or via NumPad2)
        //therefore defaults the dome to this darker one; NumPad1 still cycles freely from there, and an
        //explicit sky= on the command line overrides the startup default. Dome 13 is a violet/teal dusk.
        private const byte SEA_DEFAULT_SKY_DOME = 13;

        //The savanna does the same for the opposite mood: it reads best under a warm golden-hour sky, so
        //entering it defaults the dome to a warm one (dome 14 has the warmest gold horizon of the set).
        private const byte SAVANNA_DEFAULT_SKY_DOME = 14;

        //The tropical beach is the postcard and knows it: white sand and turquoise water read best under
        //the brightest blue in the set, so entering it defaults the dome to dome 1 — a clear sunny sky
        //over a warm horizon. Same rules as the other two: NumPad1 cycles freely from there and an
        //explicit sky= overrides the startup default.
        private const byte TROPICAL_DEFAULT_SKY_DOME = 1;

        //The volcano wants the opposite of the beach, and for a reason that is the scene's whole idea: its
        //ground is the light, so the sky's job is to stay out of the way. Dome 9 is a dim mauve-and-slate
        //dusk with no bright band anywhere in it — chosen BY LOOKING, against 16 and 13 (#207's lesson).
        //16 is darker at the zenith but its horizon is a bright cream and it puts its sun disc up beside the
        //cone, which competes with the crater for the eye; 9 has neither, so the lava is unarguably the
        //brightest thing in the frame and the cone keeps a clean silhouette.
        private const byte VOLCANO_DEFAULT_SKY_DOME = 9;

        //Mars gets a dome built for it rather than picked from the eighteen general-purpose ones (#277):
        //dome 19 IS the Martian sky, so it is the default the same way a scene with no bespoke dome takes
        //whatever NumPad1 last left it on. NumPad1 still cycles freely from there.
        private const byte MARS_DEFAULT_SKY_DOME = 19;

        //The storm wants the one thing its deck cannot supply: CLEAN HIGH AIR above it, so it brought its
        //own dome (#219). Dome 20 is a deep blue zenith over a PALE BLUE-WHITE horizon — the one thing none
        //of the other nineteen has, every Earth-surface dome in the table warming towards its horizon
        //because that is what haze at sea level does, where altitude goes paler and bluer instead.
        //
        //⚠ It is not a matter of taste, and dome 11 (the set's brightest daylight, and the first pick here)
        //is the proof: a terrain scene's distance fade must ARRIVE at the dome's exact HorizonColor to hide
        //the finite grid's own edge, so whatever the dome's horizon is, is also what every far surface
        //becomes — and under 11's sandy horizon the storm's cloud deck photographed as beige desert dunes.
        private const byte STORM_DEFAULT_SKY_DOME = 20;

        //Space deliberately forces NO dome, unlike those two. Its dome is neither drawn (Space.fx covers the
        //whole frame) nor read (SpaceLightingConfig states the light rig instead, for the reasons set out
        //there) - so it is completely inert in that scene, and NumPad1 cycling domes in it changes nothing.

        //The scene's own point lights (the savanna's campfire, the neon city's ring, space's planetshine),
        //pushed onto the shared instanced effect each frame so the balls, island, cannon and city all take them
        //on top of the sun and the dome. The slots, the arrays and the change gate are SceneLights' own since
        //#75 — this and the game held a copy each. The neon ring's figures live in _cityConfig.NeonLook.
        private SceneLights _sceneLights;

        //The sky-derived light rig, also shared with the game and the map editor since #75. Built in
        //LoadContent, after the scene renderer it consults for the scenes that state their own lighting.
        private SkyLightRig _rig;

        //When the free camera dips below the sea surface the whole frame is pulled into a blue-green murk so it
        //reads as being underwater (see Tonemap.fx's underwater block). The murk's two colours live on
        //PostProcessPipeline and its ramp — this file's own constant until #159 — is
        //SceneRenderer.LensSubmergedAmount's, along with the Game's identical copy: the ball shader's submerge
        //fade is released by exactly that figure now, and two effects that hand over must read one expression.

        //Every figure of the arena — the island's radius, edge and segment count, its two albedos and
        //texture spans, the drain's radii and depth, the gold beads, the dark pit shaft and the terrain
        //footprint it is cut into — is ArenaIsland's since #75. It stood here as ARENA_*/FUNNEL_*/PIT_* and in
        //the game as ISLAND_*/FUNNEL_*/PIT_*, the same numbers under two sets of names.

        //Window brightness now lives in the city config: _cityConfig.WindowBrightness for the day city (kept
        //under GLARE_THRESHOLD, or a window that glares veils its tower) and _cityConfig.NeonLook.WindowBrightness
        //for neon (well over the threshold, so each lit sign blooms).

        #endregion

        #region Glare

        //The pyramid itself (targets, passes, cached parameters) is the shared pipeline's; these are the
        //Testbed's own figures for it, passed in once at load.
        private EffectParameter _skyCameraPositionParam;

        /// <summary>
        /// Radiance a pixel has to exceed before it starts to glare. Set high enough that a ball only glares
        /// near the peak of its heartbeat (emission ~0.5 on top of the lit colour), not while merely lit, so
        /// the stars follow the pulse wave through the cluster rather than sitting on every bright surface at
        /// once — which read as too many, too prominent. Above the lit scene, the city windows and the spray.
        /// </summary>
        private static readonly float GLARE_THRESHOLD = 0.55f;


        /// <summary>
        /// How much of the bloom is added back. The pyramid ACCUMULATES on the way up — the half-resolution
        /// head ends carrying its own halo plus every wider level's — so the same subjective glow sits at a
        /// far lower intensity than the single-pass streak star needed (that one shipped at 0.9). The point
        /// is unchanged: hint that the balls, the neon and the crystals emit light, without the glow owning
        /// the frame.
        /// </summary>
        private static readonly float GLARE_INTENSITY = 0.5f;

        //The lens's colour fringing at the frame edges — the game's default figure, so the Testbed shows
        //what ships (the game alone carries the Settings toggle).
        private static readonly float CHROMATIC_ABERRATION = 0.0015f;

        //The film grain's mid-tone peak, likewise the game's default figure (see FILM_GRAIN there)
        private static readonly float FILM_GRAIN = 0.10f;

        #endregion

        /// <summary>
        /// Linear scale applied to the scene before the tonemap curve — the renderer's shutter speed.
        /// Overridable with "exposure=&lt;f&gt;" on the command line, which is how a sky dome that is much
        /// brighter or darker than the rest gets checked without a rebuild.
        /// </summary>
        private readonly float _exposure;

        //Narrow, because dropping the camera to the floor collapsed the angle it has to span: from up behind
        //the gun the barrel and the cluster were 57 degrees apart and the FOV had to be wide enough to hold
        //both, which left the map itself a small patch in a very wide frame. From down here they are some 14
        //apart, so the frame can close in on the map - 40 degrees vertical puts the largest map (20x20x20
        //cells, about 20x20x14 units) at roughly two thirds of the frame height and 60% of its width, which
        //is what "seeing the whole map" has to mean if the player is to pick a cell out of it by colour. The
        //margin over that is for the maps the frame is not tuned against - a taller one reaches higher, and
        //its ceiling with it - and for the length of barrel that hangs below the frame's centre.
        private static readonly float GAME_FOV = (float)Math.PI / 4.2f;
        private static readonly float FREE_FOV = (float)Math.PI / 2.5f;
        private static readonly Vector3 DEFAULT_CAMERA_POS = new (0f, -3f, 30f);

        #endregion Graphics

        #region Shooting

        private List<PhysicsBall> _shotBalls;

        //Balls released from the structure (matched clusters and balls that lost their connection to the ceiling).
        //They are no longer part of the map, but their bodies keep falling in the simulation, so they still have to be drawn.
        //RemoveFallenBalls cleans them up once they fall out of the world or come to rest.
        private List<PhysicsBall> _fallingBalls;

        private static readonly float SHOOT_MULTIPLIER = 200f;
        private static readonly Random RANDOM = new();

        //Launch smears: a colour streak left at the muzzle when a ball fires, stretched in the flight direction
        //and fading over a fraction of a second, to sell how hard the ball leaves the cannon. The streak, its
        //billboard quad, the six dials it is cut to, the colour rule and the additive depth-read draw are all
        //LaunchSmears' since #76 - it stood here and in the Game, value for value, and this copy re-sent the two
        //widths and looked five parameters up by name every frame on top. What stays here is when a smear is
        //added (ShootBall), when they age (only while the simulation runs - a paused Testbed holds its smears)
        //and where the draw sits in the frame, which is stated at the call site in Draw.
        private LaunchSmears _smears;

        private Cannon _cannon;

        //The gun's hardware: the procedural barrel and the instanced renderer that draws it. It stays a tube -
        //a barrel - with a slot along the top, because the balls nest inside the bore and only a strip of each
        //shows through the slot, which is enough to read its colour, and you cannot aim a shot whose colour you
        //cannot see. Every figure the tube is cut to (the bore, the wall, the slot's width, the segment count,
        //the steel's colour and its sheen) is CannonRig's since #76 - it stood here and in the Game, value for
        //value - and the pivot-to-front-ball distance is derived there from the magazine the bore is sized to,
        //so the tube that was built and the muzzle that is fired from cannot disagree. Where the barrel pivots
        //is Cannon's own doc: a real gun hangs from its trunnions, at the point of balance, so Cannon.Position
        //is that pivot and Cannon.BarrelWorld's translation row IS it.
        private CannonRig _cannonRig;

        //The cannon's magazine: the next ball to fire and the ones queued behind it, shown loaded in the barrel
        //so the player can see what is coming and aim for it. The queue, the glide that carries it forward after
        //a shot and the invariant that it never empties are Magazine's since #76 - it stood here and in the Game
        //with the size, the spacing and the ease constant identical. What stays here is the next-colour policy it
        //is built with: RandomBallType, uniform over all thirteen, because the Testbed's cluster is whatever map was
        //dropped on it (the Game draws only among the colours still hanging, which is why the policy is injected).
        private Magazine _magazine;


        private SpriteBatch _spriteBatch;

        //The crosshair: four procedural bars around a clear centre, Crosshair's since #76. It replaces the
        //Bitmaps/Aimer.png this file used to load, stretch and re-centre on every resize - the component makes
        //its own white texel and reads the viewport per frame, so a resize is nothing it has to be told about.
        //Free mode passes a plain 1 (the shot leaves the lens, so screen centre IS the shot) and game mode the
        //precise-aim blend, which is the whole of the difference between the two modes' crosshairs.
        private Crosshair _crosshair;

        #endregion

        #region Contacts

        private BallContactEventHandler _eventHandler;

        //The per-step contact work PhysicsWorld.Step runs inside each step, built once and held. NOT written at
        //the call site: a lambda allocates a fresh delegate every time it is evaluated, and this one is evaluated
        //once per frame. It reads _eventHandler out of the field rather than binding the instance, so it survives
        //the handler being rebuilt on every map load. Assigned in the constructor rather than here, an instance
        //field initialiser not being allowed to reference another instance member (CS0236).
        private readonly Action _processContacts;

        #endregion

        /// <summary>
        /// Everything the command line said, parsed once by <see cref="TestOptions.Parse"/> and held whole. It
        /// was fourteen constructor parameters copied into fourteen fields until #73, i.e. the same list written
        /// out three times over two files; the two figures that are <b>normalised</b> rather than merely stored
        /// (<see cref="_supersampleFactor"/>'s clamp and <see cref="_exposure"/>'s default) keep fields of their
        /// own, and everything else is read from here at the one place that wants it.
        /// </summary>
        private readonly TestOptions _options;

        //The three test harnesses (#73), each null unless its switch was given, each ticked from one line of
        //Update. They used to be nine fields and four inline blocks in Update — a cadence, an index and an
        //"already done" flag apiece, interleaved with the frame's real work. What stays this file's is the
        //CLI lines' wording, which .claude/skills/verify greps: see AutoShootOnce below and the drivers' own
        //remarks. Built in LoadContent, the aim sweep needing a gun that does not exist before it.
        private AutoShootDriver _autoShootDriver;
        private AimShootDriver _aimShootDriver;
        private SwitchMapDriver _switchMapDriver;

        //The windowed default is 16:9, the narrowest aspect the game targets (desktop and Xbox, 3840x2160 to
        //3840x1600), so what is framed in a window is the tightest case and a wider display only adds width
        public Testbed(TestOptions options)
        {
            _options = options;

            //One delegate for the whole run (see the field): it is what PhysicsWorld.Step runs after it has
            //flushed the step's contacts, and an attach changes the body and constraint counts the overlay shows.
            _processContacts = () => { if (_eventHandler.ProcessQueuedContacts() > 0) InvalidateBallCounts(); };

            //Testing: "scene=<name>" picks the starting environment, through the one parser every executable
            //now shares (#75 — this was an if/else chain here and a switch in the game, kept in step by hand,
            //which is exactly what a script driving both cannot afford). An unrecognised name leaves the
            //default city standing. The six scenes past the end of the NumPad2 cycle — which still walks only
            //SceneRenderer.CycleLength, the seven a map is authored against — are reachable only this way here.
            if (SceneRenderer.TryParseScene(options.Scene, out SceneKind startupScene)) _scene = startupScene;
            _exposure = options.Exposure > 0f ? options.Exposure : DEFAULT_EXPOSURE;
            _supersampleFactor = Math.Clamp(options.SupersampleFactor, 1, 4); //"ssaa=<n>" trades sharpness against fill rate
            _weatherFromCommandLine = options.Weather;  //Testing: "weather=<name>" pins the sky (#221)
            _skyFromCommandLine = options.SkyNumber >= 1 && options.SkyNumber <= SKY_DOME_COUNT;
            if (_skyFromCommandLine) _skyModelNumber = options.SkyNumber; //Testing: "sky=<n>" on the command line picks the starting sky dome
            else if (_scene == SceneKind.Sea) _skyModelNumber = SEA_DEFAULT_SKY_DOME; //The sea scene defaults to a darker dome (unless sky= overrode it above)
            else if (_scene == SceneKind.Savanna) _skyModelNumber = SAVANNA_DEFAULT_SKY_DOME; //The savanna defaults to a warm golden dome
            else if (_scene == SceneKind.Tropical) _skyModelNumber = TROPICAL_DEFAULT_SKY_DOME; //The beach defaults to the brightest blue
            else if (_scene == SceneKind.Volcano) _skyModelNumber = VOLCANO_DEFAULT_SKY_DOME;   //The volcano defaults to the darkest dome
            else if (_scene == SceneKind.Mars) _skyModelNumber = MARS_DEFAULT_SKY_DOME;         //Mars defaults to its own dome
            else if (_scene == SceneKind.Storm) _skyModelNumber = STORM_DEFAULT_SKY_DOME;       //The storm defaults to the brightest daylight

            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreparingDeviceSettings += Graphics_PreparingDeviceSettings;

            Content.RootDirectory = "Content";

            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += Window_ClientSizeChanged;
            Window.FileDrop += Window_FileDrop;

            SetGraphics(options.Windowed);
        }

        private void Window_FileDrop(object sender, FileDropEventArgs e)
        {
            if (e.Files == null || e.Files.Length <= 0 || string.IsNullOrEmpty(e.Files[0])) return;
            DeserializeMapFromFile(e.Files[0]);
        }

        protected override void Initialize()
        {
            IsMouseVisible = true;

            _camera = new BasicCamera3D(DEFAULT_CAMERA_POS, GraphicsDevice.Viewport.AspectRatio, FREE_FOV);

            //Testing camera pose from the command line: position, then aim (the Target setter rebuilds the view
            //and the look angles, so mouse-look carries on from there). Target defaults to the arena at the origin.
            if (_options.CamPos.HasValue)
            {
                _camera.Position = _options.CamPos.Value;
                _camera.Target = _options.CamTarget ?? Vector3.Zero;
            }

            //"\uE7FC" is the "Game" glyph (gamepad) in the Segoe MDL2 Assets icon font
            _info = new InfoRenderer(this, "Content/Fonts/segoeui", "Content/Fonts/icons") { DrawOrder = int.MaxValue, IconGlyph = "\uE7FC" };
            Components.Add(_info);

            _cih = new CameraInputHelper(_camera, this);

            //The dispatcher the contact stream sizes its queues off, that stream, the simulation whose creation is
            //what initialises it (there is one ContactEvents.Initialize in the program now — issue #73), the
            //gravity and the solver description tuned together with the contact material and the BallSocket
            //spring: every one of them is PhysicsWorld's. Built here and kept for the whole run.
            _world = new PhysicsWorld();

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
                new(mgKeys.NumPad2, SwitchScene, "Switch scene (city/sea/savanna/desert/mountain/meadow/neon)"),
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
            builder.Append(string.Format(format, "Mouse", "Cannon aiming (game mode)"));
            builder.Append(string.Format(format, "RMB/LT", "Precise aim (hold)"));
            builder.Append(string.Format(format, "LMB", "Shoot (game mode)"));
            builder.Append(string.Format(format, "A / D", "Move cannon left / right (game mode)"));
            builder.Append(string.Format(format, mgKeys.NumPad7.ToString(), "Camera orbit left"));
            builder.Append(string.Format(format, mgKeys.NumPad9.ToString(), "Camera orbit right"));

            _info.HintText = builder.ToString();

            #endregion

            InitializeShooting();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _instancingEffect = Content.Load<Effect>("Shaders/InstancedModel");

            //The meshes, the renderers, the look and the buckets in one construction. No ripple here — that wave
            //is the Game's, and asking for it would also turn the resting heartbeat down to leave room for it
            //(see BallRenderSet.PULSE_DEPTH_RIPPLING), so the Testbed keeps the deeper breath it always drew. The
            //shared instancing effect is handed in and never disposed there, the city, the island, the barrel and
            //the ceiling all drawing through the same copy.
            //SupersampleFactor, for the same reason the scene renderer is told it below: the dissolve's dither
            //cell is authored in output pixels, so unscaled it would be averaged away by the tonemap's box filter.
            //Set at construction rather than on a hook, the factor here being fixed for the run by the command line.
            _balls = new BallRenderSet(GraphicsDevice, _instancingEffect) { SupersampleFactor = _supersampleFactor };

            //The pipeline caches its parameters and sets each look value exactly once through the required
            //initializer — fixed for the whole run here (the game alone has the Settings toggles). See #74.
            _pipeline = new PostProcessPipeline(GraphicsDevice,
                Content.Load<Effect>("Shaders/Tonemap"), Content.Load<Effect>("Shaders/Glare"))
            {
                GlareThreshold = GLARE_THRESHOLD,
                GlareIntensity = GLARE_INTENSITY,
                Exposure = _exposure,
                //Both zeroed by "nopost", which is what makes an A/B of a shader change readable at all: the
                //grain re-rolls a modulation on every output pixel every frame, so two captures of an
                //unchanged scene differ in over 90 % of their pixels and a diff says nothing. The aberration
                //is the other half — it splits every high-contrast edge towards the frame's periphery, and it
                //once absorbed four straight attempts at a slab-joint artefact that was never in the shader
                //under investigation. Each zero skips its branch in Tonemap.fx outright.
                ChromaticAberration = _options.NoPostEffects ? 0f : CHROMATIC_ABERRATION,
                FilmGrain = _options.NoPostEffects ? 0f : FILM_GRAIN,
                SupersampleFactor = _supersampleFactor,
            };

            _sceneLights = new SceneLights(_instancingEffect);

            //The launch smears' billboard quad and every parameter handle the draw needs, in one construction.
            //The effect is handed in and never disposed there, its lifetime being the content manager's.
            _smears = new LaunchSmears(GraphicsDevice, Content.Load<Effect>("Shaders/ShotTrail"));

            #region Ceiling and scenery

            _ceilingPlate = new CeilingPlate(GraphicsDevice, _instancingEffect);
            _ceilingPlate.Fit(DEFAULT_CEILING_STAGE_SIZE, DEFAULT_CEILING_STAGE_SIZE);

            BuildCeiling();

            BuildCity();

            #endregion Ceiling and scenery

            //No Initialize call on the contact stream anywhere in this file: Simulation.Create has already run
            //NarrowPhaseCallbacks.Initialize, which is what initialises it, and this used to call it a second
            //time (issue #73) — hooking the stream's BeforeCollisionDetection handler onto the timestepper twice,
            //so the freshness pass walked every listener's previous collisions twice on every step for nothing.
            //PhysicsWorld's constructor owns the one Initialize the program performs. The handler itself is built
            //per map load rather than here (InstallMap, #68): it holds the field it resolves contacts against.

            //The dome's palette is sRGB; the target it is drawn into is linear
            _sky = new SkyDome(GraphicsDevice, _skyModelNumber, linearVertexColors: true);

            _skyEffect = Content.Load<Effect>("Shaders/Sky");
            _sky.Effect = _skyEffect;
            _skyCameraPositionParam = _skyEffect.Parameters["CameraPosition"];

            //Everything about the clouds that does not change frame to frame, pushed once. The per-frame half —
            //the clock and the camera — goes out in Draw, right before the dome; the colours and the sun's own
            //direction follow the dome and are ApplySkyLighting's business.
            _clouds.ApplyStaticParameters(_skyEffect);

            //The self-lit outdoor backdrops (sea/savanna/desert/mountain/meadow, with the savanna's acacias, the
            //savanna's and desert's birds and the mountain's snow) all live here now, shared with the map editor
            //so a scene looks the same in both
            //SupersampleFactor: the space scene sizes its stars in OUTPUT pixels rather than in texels, so it
            //has to be told what ssaa= settled on — sized in texels a star would be four times dimmer at 2x
            _sceneRenderer = new SceneRenderer(GraphicsDevice, Content) { SupersampleFactor = _supersampleFactor };

            //After the scene renderer, which the rig consults for the scenes that state their own lighting. The
            //cloud hook is captured ONCE here rather than per frame: a method group written at the call site
            //builds a fresh delegate every time it is evaluated, and this one used to be evaluated in Draw.
            _rig = new SkyLightRig(_sceneRenderer) { CloudHook = _clouds.ApplyTo };

            //Cut the island's footprint out of the solid terrain scenes so nothing solid shows through the drain
            //and the funnel below the island reads as a drain into a pit, not a bowl in flat ground (the dark pit
            //cone that backs the glass then fills that hole - see ArenaIsland.TERRAIN_HOLE_RADIUS). The map editor
            //leaves this 0 - it draws no island, so nothing is cut and the terrain stays whole under the field's AABB.
            _sceneRenderer.TerrainHoleRadius = ArenaIsland.TERRAIN_HOLE_RADIUS;

            //The sky the starting scene stands under (#221), and IMMEDIATELY rather than faded: a fade here
            //would be the first two seconds of every launch spent arriving at the weather the scene already
            //asked for. It needs the scene renderer above, whose configs hold the answer.
            ApplySceneWeather(immediately: true);

            //The wood the forest scene stands in: both procedural textures, the fifteen mesh variants, the
            //twenty-five renderers and the tints, planted on the very terrain SceneRenderer draws. Here rather
            //than in BuildCity beside the island, which is where the rest of the setting is made, because it
            //needs the scene renderer's own forest config and that does not exist until the line above — and it
            //must be built before the first ApplySkyLighting below, or the whole wood would draw a frame under a
            //white sky. No stone texture handed in: the component builds one, ArenaIsland's being its private
            //business. The ambient is the scene's, exactly as the island is given it.
            _forestScatter = new ForestScatterRenderer(GraphicsDevice, _instancingEffect,
                (ForestSceneConfig)_sceneRenderer.GetSceneConfig(SceneKind.Forest), SCENE_AMBIENT_INTENSITY);

            //No trunnion height goes in: the gun stands on the island's dished stone, so its height is the
            //carriage's own figure of its radius (CannonRig.TrunnionHeightAt) and the pose re-seats it on
            //every move — walking in carries the wheels down the dish, walking out back up
            _cannon = new Cannon(new Vector3(0f, 5f, 0f), 20f);

            //The procedural barrel (the last modeled asset made procedural), cut to hold exactly the loaded queue:
            //a muzzle lip just ahead of the front ball, the domed breech closing just behind the last one. The rig is told only how
            //many slots there are and how far apart, because that is all the tube's length is; the shared
            //InstancedModel effect is handed in and never disposed there, being the balls', the city's, the
            //island's and the ceiling's too.
            _cannonRig = new CannonRig(GraphicsDevice, _instancingEffect, Magazine.SIZE, Magazine.SPACING);

            //The overlay's own batch, and the crosshair's one white texel stretched into each of its four bars
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _crosshair = new Crosshair(GraphicsDevice);

            _pipeline.EnsureTarget();

            BuildTestHarnesses();

            if (!string.IsNullOrEmpty(_options.StartupMapPath) && File.Exists(_options.StartupMapPath)) DeserializeMapFromFile(_options.StartupMapPath);

            //Start playable even with nothing on the command line (and as a fallback if a startup map failed to
            //load): an empty field the player can aim and shoot into right away. Without a map _map stays null,
            //which gates off the mouse aim and precise aim (UpdateCannon), so game mode could not aim or shoot.
            //A map loaded or dropped later replaces this one.
            if (_map == null) InstallMap(new BallsMap(DEFAULT_EMPTY_MAP_X, DEFAULT_EMPTY_MAP_Z, DEFAULT_EMPTY_MAP_LEVELS));

            //The command-line sky= only pins the startup dome; a level opened at runtime takes its own dome
            _skyFromCommandLine = false;

            ApplySkyLighting();

            //Testing: the aim-and-shoot scan needs to be in game mode (a shot then leaves the cannon along its aim
            //rather than the free camera). Kick off the entry animation now; the sweep waits for it in Update.
            if (_aimShootDriver != null) SwitchGameMode(true);
        }

        /// <summary>
        /// Builds whichever of the three test harnesses the command line asked for (#73). Here rather than in
        /// <see cref="Initialize"/> because the aim sweep needs the gun: it reads
        /// <see cref="CannonRig.PivotToFrontBall"/>, and neither the <see cref="Cannon"/> nor its rig exists
        /// until a few lines above. Before the startup map is installed, though — <see cref="InstallMap"/>
        /// pushes the field onto the sweep and runs the <c>aimcheck</c> report.
        /// <para>
        /// Each is <b>null unless its switch was given</b>, which is what the one-line ticks in
        /// <see cref="Update"/> test against; there is no "enabled" flag anywhere, so a harness that exists is a
        /// harness that runs. The delegates go in here, once — written at the tick instead they would allocate a
        /// fresh delegate on every frame of the run (BestPractices.md §3).
        /// </para>
        /// </summary>
        private void BuildTestHarnesses()
        {
            if (_options.AutoShoot) _autoShootDriver = new AutoShootDriver(AutoShootOnce);

            if (_options.AimShoot) _aimShootDriver = new AimShootDriver(_cannon, _cannonRig.PivotToFrontBall, () => ShootBall());

            if (!string.IsNullOrEmpty(_options.SwitchMapPath))
                _switchMapDriver = new SwitchMapDriver(_options.SwitchMapPath, DeserializeMapFromFile);
        }

        /// <summary>
        /// One <c>autoshoot</c> shot and the line that reports it. The <b>cadence and the target box are
        /// <see cref="AutoShootDriver"/>'s</b> and this is the half that stays here, because every figure on the
        /// line belongs to this executable's own renderers — and because
        /// <c>[autoshoot] FPS: …, balls drawn: …</c> is a CLI surface <c>.claude/skills/verify</c> greps, so its
        /// wording is a contract that belongs with the program that publishes it.
        /// </summary>
        private void AutoShootOnce(Vector3 target)
        {
            ShootBall(target);

            Console.WriteLine($"[autoshoot] FPS: {_info.CurrentFPS}, balls drawn: {_balls.DrawnCount}/{_collectedBalls}, LOD: {string.Join("/", _balls.LodTotals)}");
        }

        /// <summary>
        /// Ambient intensity of the scene objects (ceiling, cannon, city, arena floor). The sky tint itself
        /// comes from the hemisphere colors in the shader, so this stays a neutral gray.
        /// </summary>
        private static readonly float SCENE_AMBIENT_INTENSITY = 0.25f;

        //Reused by SkyLitRenderers: the overcast lerp re-applies the light rig every frame, and an iterator
        //method allocates its enumerator on every call — a steady per-frame allocation for a fixed list
        private readonly List<InstancedModelRenderer> _skyLitRenderers = new();

        /// <summary>
        /// Every renderer that takes part in the sky-derived lighting <b>at full strength</b> — the ceiling
        /// glass is deliberately not here: it stands against the sky itself and takes the rig through
        /// <see cref="SkyLightRig.ApplyToGlass"/> wherever this list is pushed. Refilled on every call (into
        /// one reused list, so per-frame callers allocate nothing), which keeps it correct across any
        /// renderer recreation.
        /// </summary>
        private List<InstancedModelRenderer> SkyLitRenderers()
        {
            _skyLitRenderers.Clear();

            //BallRenderSet.Renderers hands back its own array rather than a read-only view, exactly so this can
            //be walked every frame without boxing an enumerator (its doc names this caller)
            foreach (InstancedModelRenderer ballRenderer in _balls.Renderers) _skyLitRenderers.Add(ballRenderer);

            _skyLitRenderers.Add(_cannonRig.Renderer);
            _skyLitRenderers.Add(_cannonRig.GlassRenderer);
            _skyLitRenderers.Add(_cannonRig.CarriageRenderer);
            _skyLitRenderers.Add(_cannonRig.WheelRenderer);
            _skyLitRenderers.Add(_cannonRig.RollerRenderer);
            if (_cityRenderer != null) _skyLitRenderers.Add(_cityRenderer);
            //The island's cap and drum, the drain's glass and its gold beads — but deliberately not its pit
            //shaft, which is a hole in the ground no dome may bleach. Appended rather than enumerated: this
            //list is refilled every frame by the overcast lerp, and an iterator would allocate per call.
            _island?.AppendSkyLitTo(_skyLitRenderers);

            //Every variant of every scattered kind, or a spruce of the variant this missed would stand under the
            //light rig of whatever dome was up when it was made. Walked as the array ForestScatterRenderer hands
            //back, for the same reason BallRenderSet.Renderers does above: this list is refilled every frame, and
            //a foreach over an interface-typed collection would box an enumerator per call.
            if (_forestScatter != null)
                foreach (InstancedModelRenderer renderer in _forestScatter.Renderers) _skyLitRenderers.Add(renderer);

            return _skyLitRenderers;
        }

        /// <summary>
        /// Derives the scene lighting from the current sky dome (issue #39): every object receives hemisphere
        /// ambient (zenith colour from above, horizon colour from below) and the tinted three-light rig, so
        /// every sky dome gives the whole scene its own mood. The derivation itself is
        /// <see cref="SkyLightRig"/>'s since #75 — it stood here and in the game and in the map editor, palette
        /// decode, scale factors, tints and scene override alike.
        /// </summary>
        /// <summary>
        /// Puts the sky the current scene asks for over it (#221), unless a loaded level overrides it. The
        /// scene's answer is read off the shared <see cref="SceneConfig"/> — the same object the game reads
        /// and the map editor edits live — so a scene's weather cannot be one thing in one executable and
        /// another somewhere else, which is the loop #44 closed for scene looks and this closes for skies.
        /// </summary>
        /// <param name="levelWeather">The level file's word, or null. An unrecognised one is null too, so a
        /// typo leaves the scene's own weather standing rather than throwing.</param>
        private void ApplySceneWeather(string levelWeather = null, bool immediately = false)
        {
            //"weather=<name>" outranks both, which is what makes it useful: the point of it is to see one
            //backdrop under five skies, and a scene default that could override it would defeat that.
            //The city's own config is this file's and not the scene renderer's — the two cities are drawn
            //through the shared instanced technique rather than by a scene shader, which is why
            //GetSceneConfig answers null for both. Reading it from the right place is what stops the city
            //being the one scene with no authored sky.
            SceneConfig config = _scene is SceneKind.City or SceneKind.NeonCity
                ? _cityConfig
                : _sceneRenderer.GetSceneConfig(_scene);

            WeatherPreset preset = WeatherLooks.TryParse(_weatherFromCommandLine)
                ?? WeatherLooks.TryParse(levelWeather)
                ?? config?.Weather
                ?? WeatherPreset.Scattered;

            if (immediately) _clouds.SetWeatherImmediately(preset); else _clouds.SetWeather(preset);
        }

        private void ApplySkyLighting()
        {
#if DEBUG
            Console.WriteLine($"[sky] Dome {_skyModelNumber}: zenith {_sky.ZenithColor}, horizon {_sky.HorizonColor}");
#endif

            _rig.SetSky(_sky, _scene);
            _rig.ApplyTo(SkyLitRenderers());

            //The glass push (SkyLightRig.ApplyToGlass holds the why — the plate stands against the sky, #156)
            _rig.ApplyToGlass(_ceilingPlate.Renderer);

            //And the wood's own pigments, which the rig above cannot reach: a saturated green has almost no
            //red or blue for a coloured light to multiply, so the crowns stayed the same green under every
            //dome while the ground turned (#108). Guarded inside on the tint, so this is free on the frames
            //the dome has not moved — which is all of them but the switch.
            _forestScatter?.ApplySkyTint(_rig.KeyTint);

            //The clouds' own colours follow the dome as well, and the lit side is handed the very radiance the
            //rig gives the scene — one sun, one number (see SkyLightRig.SunRadianceTinted). Since #220 the
            //sun's DIRECTION rides along, the dome having its own: the drawn disc, the deck's silver lining
            //and the shadow the instanced shader throws are all re-aimed in this one call.
            _clouds.ApplyDome(_skyEffect, _instancingEffect, _rig.SunDirection,
                _rig.SunRadianceTinted, _rig.ZenithLinear, _rig.HorizonLinear);
        }

        /// <summary>
        /// Follows the cloud straight over the arena and flattens the ambient by it — the Testbed's own half of
        /// the weather, which the game deliberately leaves out (see <see cref="SkyLightRig.StepOvercast"/>,
        /// which holds the reasoning for both the mechanism and the omission).
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

            //A change of sky is a sky CHANGING rather than one frame's cut (#221). Walked here because this
            //is where the deck's own clock is set and where the rig's overcast lerp is stepped — the two
            //have to arrive together, and the deck is what leads.
            _clouds.Step(elapsedSeconds);

            //Straight up from the middle of the arena, so the sample is simply where the arena stands —
            //averaged over a patch about as wide as a cloud, since what lights the scene from above is how
            //much of the sky is covered, not what happens to sit over one point of it
            _rig.StepOvercast(_clouds.CoverAround(Vector2.Zero, OVERCAST_SAMPLE_RADIUS), elapsedSeconds);

            //Refilled every frame into one reused list, and pushed by index, so the per-frame path allocates
            //nothing — this is the caller BestPractices.md §3 records the iterator incident for
            _rig.ApplyTo(SkyLitRenderers());

            //Re-pushed here because the lerp above moved the very ambient the glass push scales by (see
            //SkyLightRig.ApplyToGlass; an overcast sky is bright, so the plate keeps its full rig)
            _rig.ApplyToGlass(_ceilingPlate.Renderer);
        }

        private void SwitchSkyDome()
        {
            SetSkyDome((byte)(_skyModelNumber == SKY_DOME_COUNT ? 1 : _skyModelNumber + 1));
        }

        /// <summary>
        /// Loads the given sky dome and re-derives the whole scene's lighting from it. The one place a dome
        /// change happens at runtime, shared by <see cref="SwitchSkyDome"/> and the sea scene's default dome.
        /// </summary>
        private void SetSkyDome(byte number)
        {
            _skyModelNumber = number;
            _sky.DomeNumber = number;

            ApplySkyLighting();
        }

        private void SwitchScene()
        {
            //The cycle is deliberately only SceneRenderer.CycleLength long — the seven scenes a map is authored
            //against — but it has to be entered from OUTSIDE it too, and (index + 1) % 7 could not do that (#73):
            //started from a scene reached with scene=, the modulo landed wherever the arithmetic fell rather than
            //at a cycle boundary, so NumPad2 from the forest (index 7) came out at the SEA, three scenes deep,
            //and the four cycle entries before it were unreachable without pressing it four more times. Anything
            //off the end restarts the cycle at the city instead.
            int next = (int)_scene + 1;
            _scene = (SceneKind)(next < SceneRenderer.CycleLength ? next : 0);

            Console.WriteLine($"[scene] {_scene}");

            //The sea reads best under a moody sky, the savanna under a warm golden one and the tropical
            //beach under the brightest blue, so entering one of them defaults its dome — and SetSkyDome
            //re-derives the rig on its way, so that arm needs nothing more (NumPad1 still cycles freely
            //from there). Every other scene keeps whatever dome is up but must
            //still re-derive: a scene may state its own lighting instead of the dome's, and the rig has to be
            //told which scene it is standing in. Latent rather than visible while the cycle stays inside the
            //seven that all take the dome's — which is exactly why it would have gone unnoticed.
            if (_scene == SceneKind.Sea) SetSkyDome(SEA_DEFAULT_SKY_DOME);
            else if (_scene == SceneKind.Savanna) SetSkyDome(SAVANNA_DEFAULT_SKY_DOME);
            else ApplySkyLighting();

            //And the sky that scene stands under (#221), which is the scene config's own — the same answer
            //the game reads, from the same place, so a scene's weather cannot be one thing here and another
            //there. SetWeather fades, so cycling with NumPad2 leaves one sky closing over into the next.
            ApplySceneWeather();
            //The tropical beach and the volcano sit past the cycle's end (CycleLength 7) and are never
            //reached by this switch — their default domes are applied at startup only, where the scene= arm
            //above finds them.
        }

        /// <summary>
        /// The per-frame inputs the shared <see cref="SceneRenderer"/> needs. The rig holds five of the six and
        /// the cloud hook was captured once at load, so this allocates nothing — it used to build a fresh
        /// delegate and re-derive the sun's tint on every frame of the draw path.
        /// </summary>
        private SceneFrame BuildSceneFrame() => _rig.BuildSceneFrame(_camera, _pulseSeconds);

        /// <summary>
        /// Builds the setting: the procedural city (one unit box mesh under a different instance matrix per
        /// building, which is what keeps the whole skyline to a single draw call) and the round island the
        /// play field stands over — the platform's stone cap and concrete drum, the glass drain funnel, its
        /// gold rims and the dark pit shaft, each its own procedural mesh and renderer.
        /// </summary>
        private void BuildCity()
        {
            _unitBox = new BoxMesh(GraphicsDevice, 1f, 1f, 1f);

            //The clearing the city keeps clear of towers is now the round island's radius, so the towers
            //frame the small island closely instead of a big plaza
            _city = new City(seed: 20260720, arenaHalfExtent: ArenaIsland.RADIUS, config: _cityConfig);

            Console.WriteLine($"[city] {_city.Buildings.Length} buildings, island radius {ArenaIsland.RADIUS}, floor at {ArenaIsland.TOP_Y}");

            _cityRenderer = new InstancedModelRenderer(GraphicsDevice, _unitBox, Vector3.One, _instancingEffect)
            {
                CityWindowBrightness = _cityConfig.WindowBrightness,
                CityConfig = _cityConfig,
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

            //The arena the gun stands on, all of it: the island's stone cap and concrete drum, the glass
            //drain bored through the middle, its two gold beads and the dark pit shaft that backs the glass
            //where the terrain has the island's footprint cut out of it. Meshes, procedural textures,
            //renderers and world matrices are the component's; the ambient is the scene's, so it is handed in.
            _island = new ArenaIsland(GraphicsDevice, _instancingEffect, SCENE_AMBIENT_INTENSITY);

            //All of it unless "arena=" says otherwise, which only a measurement run does — #151 needs each
            //member taken out of the frame in turn, and there was no way to do that from outside the class
            _island.Members = _options.Arena;

            //#151 PROBE - TEMPORARY: which of the cut-down copies of the cap's pixel shader is drawn, 0
            //being the shipped one. The members sweep can only take the whole cap out; this splits it up.
            _island.CapTriplanarProbe = _options.CapProbe;

            //#151 PROBE - TEMPORARY: "alt=" cycles the two above on every [fps] window, so a sweep's variants
            //share one process and one clock. Seed the first entry here rather than waiting for the first
            //switch, or the opening window would be measured as whatever "arena="/"capprobe=" left standing
            //and would then be labelled as the variant that follows it.
            if (_options.Alternation.Count > 0) ApplyArenaVariant(0);
        }

        private void BuildCeiling()
        {
            //The wide grid of ground blocks is gone with the big plaza: the round island's only physics floor
            //is the drain's own mesh - the cone plus the dished stone ring around it, out to the island's
            //walkable edge; the dish rolls a landed ball into the glass instead of parking it. A ball that
            //falls past the ring drops off the island's edge into the scene and is culled by the kill plane,
            //exactly as one that runs down the funnel is. The collider is FunnelPhysics' since #75 and is
            //handed the very figures ArenaIsland draws from - the dish's depth included - so the two cannot
            //drift apart.
            //The pool and the simulation come off the world: FunnelPhysics takes its triangles from the pool and
            //hands them to a mesh the pool's own teardown releases, which is why the pool is exposed at all.
            FunnelPhysics.Build(_world.Simulation, _world.BufferPool, ArenaIsland.TOP_Y, ArenaIsland.FUNNEL_BOTTOM_Y,
                ArenaIsland.FUNNEL_TOP_RADIUS, ArenaIsland.FUNNEL_HOLE_RADIUS, ArenaIsland.FLOOR_RADIUS,
                ArenaIsland.DISH_DEPTH, ArenaIsland.FUNNEL_SEGMENTS);

            //The same footprint and thickness the drawn plate was just fitted to, off the one helper
            Box box = new(CeilingPlate.FootprintFor(DEFAULT_CEILING_STAGE_SIZE), CeilingPlate.THICKNESS,
                CeilingPlate.FootprintFor(DEFAULT_CEILING_STAGE_SIZE));
            TypedIndex boxShapeIndex = _world.Simulation.Shapes.Add(box);
            _ceilingShapeIndex = boxShapeIndex;
            CollidableDescription collidableDescription = new(boxShapeIndex, 0.1f);
            BodyDescription bodyDescription = BodyDescription.CreateKinematic(new System.Numerics.Vector3(0f, GetCeilingY(10), 0f), collidableDescription, new BodyActivityDescription(Constants.HUNDREDTH));

            BodyHandle topBodyHandle = _world.Simulation.Bodies.Add(in bodyDescription);
            BodyReference topBodyReference = new(topBodyHandle, _world.Simulation.Bodies);

            _ceiling = new KinematicBody(topBodyReference, topBodyHandle);
        }

        /// <summary>
        /// The ceiling's centre Y: it hovers <see cref="CeilingPlate.CLEARANCE"/> above the centre of the
        /// top-level balls, whose own Y is (levels - 1)/√2 — a level's height being its index over √2.
        /// </summary>
        private static float GetCeilingY(byte levels) => CeilingPlate.CentreYAbove((levels - 1) / Constants.SQRT_TWO);

        /// <summary>
        /// Moves and resizes the ceiling so it covers the play field of the given map and sits just above its top level.
        /// The kinematic body is kept (constraints of a previously loaded structure may still reference it);
        /// only its pose, collision shape and drawn plate change.
        /// </summary>
        private void FitCeilingToMap(BallsMap map)
        {
            //Odd levels are shifted by +0.5 and balls have radius 0.5, so the plate oversails the field by a
            //margin — one figure, taken from the plate so the collidable and the drawn box cannot disagree
            float sizeX = CeilingPlate.FootprintFor(map.StageSizeX);
            float sizeZ = CeilingPlate.FootprintFor(map.StageSizeZ);

            BodyReference ceilingReference = _ceiling.BodyReference;
            ceilingReference.Pose.Position = new System.Numerics.Vector3(0f, GetCeilingY(map.Levels), 0f);

            TypedIndex newShapeIndex = _world.Simulation.Shapes.Add(new Box(sizeX, CeilingPlate.THICKNESS, sizeZ));
            ceilingReference.SetShape(newShapeIndex);
            _world.Simulation.Shapes.Remove(_ceilingShapeIndex);
            _ceilingShapeIndex = newShapeIndex;

            //Recreate the wrapper so its world matrix matches the new pose (the body and handle stay the same);
            //the drawn glass box is regenerated at the exact new size instead of scaling a fixed mesh. The
            //caller re-runs ApplySkyLighting afterwards — the new renderer has never been told the dome.
            _ceiling = new KinematicBody(ceilingReference, _ceiling.BodyHandle);
            _ceilingPlate.Fit(map.StageSizeX, map.StageSizeZ);
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
            try
            {
                //A level file (format marker "bs3d-level") carries a map plus the scene/sky that reproduce its
                //look; a plain map file carries just the layout. Both use .json, so the loader probes.
                if (Level.IsLevelFile(filePath))
                {
                    LoadLevel(filePath);
                    return;
                }

                InstallMap(new BallsMap(filePath));
            }
            catch (Exception e)
            {
                //A broken or unreadable file must not kill the game: a hand-edited level with a typo'd sky or
                //a version this build does not read, a non-rectangular ball array, a dropped folder. (A
                //typo'd SCENE name no longer reaches here — the converter takes it as "no scene" and the
                //backdrop simply stays, the music field's leniency.) Both paths parse fully
                //before the current structure is torn down (LoadLevel / the BallsMap ctor before InstallMap),
                //so logging and returning leaves the running game exactly as it was.
                Console.WriteLine($"[load] Failed to load '{filePath}': {e.Message}");
            }
        }

        /// <summary>
        /// Loads a level: its map through the same path a plain map file takes, then the scene backdrop it
        /// names and the sky dome. The file is parsed completely before the current structure is
        /// torn down, so a broken file leaves the running game untouched.
        /// </summary>
        private void LoadLevel(string filePath)
        {
            Level level = Level.Load(filePath);
            BallsMap map = new(level.Map);

            InstallMap(map);

            //A level names its scene and nothing more (format 2): the scene's parameters are fixed in code,
            //so switching to it is exactly what the NumPad2 cycle does — set the kind and draw. The city
            //needs nothing either: the default city stands from startup, and the draw derives day/neon from
            //_scene per frame.
            if (level.Scene is SceneKind sceneKind) _scene = sceneKind;

            //The level's own sky, or its scene's where it names none (#221) — the game's own order, so a
            //level looks the same in both.
            ApplySceneWeather(level.Weather);

            //The level's dome wins over any scene-entry default (NumPad1 still cycles freely from here), except
            //an explicit command-line sky= pins the startup dome — see _skyFromCommandLine. The rig re-derives
            //either way (SetSkyDome does it on its way): the scene just changed, and a scene may state its own
            //lighting instead of the dome's — SwitchScene's rule, held here too.
            if (!_skyFromCommandLine) SetSkyDome(Math.Clamp(level.SkyDome, (byte)1, SKY_DOME_COUNT));
            else ApplySkyLighting();

            Console.WriteLine($"[level] Loaded '{level.Name ?? Path.GetFileName(filePath)}': scene={_scene}, sky={_skyModelNumber}, balls={_map.GetBallsCount()}");
        }

        /// <summary>
        /// Installs an already-built map: tears down the current ball structure, fits the ceiling, cannon
        /// and game camera to the new field and builds the constrained physics structure.
        /// </summary>
        private void InstallMap(BallsMap map)
        {
            RemoveCurrentBallsStructure();

            _map = map;
            _map.Center();

            FitCeilingToMap(_map);
            FitCannonAndGameCameraToMap();

            _physicsBalls = BallsConstraintsBuilder.BuildBallsStructure(_map.GetStaticBallsArray(), _world.Simulation, _ceiling.BodyReference);

            //Built fresh for the field it resolves contacts against, the way the Game builds one per level (#68).
            //It used to be made once in LoadContent with the map, the structure array and the ceiling pushed onto
            //it afterwards — three mutable fields, of which the ceiling was quietly wrong: FitCeilingToMap above
            //replaces the whole KinematicBody wrapper, and #73 had to add a setter to keep the handler in step.
            //Rebuilding removes the whole class of staleness instead of pushing each field as it changes, and it
            //is safe precisely because RemoveCurrentBallsStructure above retired every ball in flight, so no
            //listener registered against the previous handler outlives it.
            //Vector3.Zero is the lattice-to-world offset: here the lattice frame IS the world frame (the field's
            //floor sits at y = 0 and there is no death line to hang it off), where the Game hangs each field
            //where its own fit decided. Nothing is subscribed to the handler's two events — what a landing is
            //worth is a rule, and this executable keeps no score.
            _eventHandler = new BallContactEventHandler(_world.Simulation, _world.Events, _ceiling, _map,
                _physicsBalls, _shotBalls, _fallingBalls, Vector3.Zero);

            InvalidateBallCounts();

            ApplySkyLighting(); //FitCeilingToMap recreated the ceiling renderer, which starts without the sky palette

            //The harness follows the field for the same reason the handler does: this executable swaps maps
            //inside a live session, so a sweep that captured one at construction would walk a field that is no
            //longer loaded. aimcheck runs without aimshoot, hence the static report and the separate gate.
            if (_aimShootDriver != null) _aimShootDriver.Map = _map;

            if (_options.AimCheck) AimShootDriver.LogReachability(_map, _cannon.OrbitRadius, _cannon.Position.Y);
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

                            //A structure ball never listened for contacts (it was unregistered the moment it
                            //attached), so this is a plain body removal rather than PhysicsWorld.RetireBall
                            _world.Simulation.Bodies.Remove(ball.BallReference.Handle);
                            _physicsBalls[x, z, level] = null;
                        }
            }

            RemoveDynamicBalls(_shotBalls);
            RemoveDynamicBalls(_fallingBalls);
        }

        /// <summary>
        /// Removes the bodies of the given balls from the simulation and clears the list
        /// (the list instance is shared with <see cref="BallContactEventHandler"/>, so it must be cleared, not replaced).
        /// </summary>
        private void RemoveDynamicBalls(List<PhysicsBall> balls)
        {
            //Unregister-then-remove, in that order, is PhysicsWorld.RetireBall's — and it probes for the listener
            //unconditionally, where this used to be told whether to bother. A released ball is not a listener (it
            //was unregistered when it attached), so the probe answers false for it anyway, and the flag could only
            //ever hide a ball that genuinely still was one — leaving a dangling listener behind a removed body.
            for (int i = 0; i < balls.Count; i++) _world.RetireBall(balls[i].BallReference);

            balls.Clear();
        }

        //Whether the overlay's body/constraint line is out of date. Starts true so the first visible frame states
        //the counts rather than an empty line.
        private bool _ballCountsDirty = true;

        /// <summary>
        /// Notes that the simulation's population has changed. <b>Deferred rather than counted on the spot
        /// (#73)</b>: this used to be the count itself, and each of the five things that can move the population
        /// — an attach, a cull, a shot, a map load, a release — walked <see cref="Solver.CountConstraints"/> over
        /// every constraint batch in the solver and composed two strings, whether or not anyone could see the
        /// result. A cascade that releases a cluster does all five in one frame. Now the frame that draws pays
        /// once, and only if the overlay is up.
        /// </summary>
        private void InvalidateBallCounts() => _ballCountsDirty = true;

        /// <summary>
        /// Rebuilds the overlay's count line if it is stale, called once per frame from <see cref="Update"/>.
        /// <para>
        /// The <see cref="InfoRenderer.Visible"/> test comes <b>before</b> the flag is cleared on purpose: with
        /// the overlay hidden (F12) the counts are simply not computed, and the dirt is left standing so the
        /// first frame after F12 brings it back states the truth instead of whatever the line last said.
        /// </para>
        /// </summary>
        private void RefreshBallCounts()
        {
            if (!_ballCountsDirty || !_info.Visible) return;

            _ballCountsDirty = false;

            _info.CustomText = "Balls on scene: " + (_world.Simulation.Bodies.ActiveSet.Count) + "\nConstraints count: " + _world.Simulation.Solver.CountConstraints();
        }

        protected override void UnloadContent()
        {
            _unitBox?.Dispose();
            //The rig releases the barrel's buffers AND the renderer's instance buffer - the mesh-only disposal
            //that stood here leaked the latter. Not the shared effect, which the content manager owns.
            _cannonRig?.Dispose();
            //The three sphere meshes and the three renderers' instance buffers, which nothing here used to
            //release at all — the set owns them now, so it owns letting them go
            _balls?.Dispose();
            _island?.Dispose();
            //Every mesh, renderer and procedural texture of the forest scatter, in one call. It owns its own
            //stone texture here (none was handed in), so nothing outside it is waiting on this.
            _forestScatter?.Dispose();
            _ceilingPlate?.Dispose();
            _sceneRenderer?.Dispose();
            _pipeline?.Dispose();
            //The smears' shared billboard quad (both buffers) and the crosshair's white texel - not the trail
            //effect, which the content manager owns
            _smears?.Dispose();
            _crosshair?.Dispose();
            //The dome's two buffers and its owned BasicEffect — not the sky effect, which the content
            //manager owns
            _sky?.Dispose();
            _spriteBatch?.Dispose();
            //Contact stream, simulation, dispatcher, pool — in that order, which is the reverse of the order they
            //were built in and PhysicsWorld.Dispose's to get right. It includes the ContactEvents this used to
            //leak: the stream unhooks itself from the timestepper, so it has to go while the simulation is still
            //there, and the pool both allocated from has to outlive the two.
            _world?.Dispose();
        }

        /// <summary>
        /// The middle of the hanging cluster: the depth <see cref="PreciseAim.LensTarget"/> converges its look-at
        /// point at, so the screen-centre crosshair marks where the shot is actually directed. Derived here from
        /// the loaded map — the component takes it as an argument on purpose, the Game solving it once per level
        /// instead — and falling back to the orbit centre before a map exists.
        /// </summary>
        private Vector3 ClusterCentre()
        {
            float clusterY = _map != null
                ? GetCeilingY(_map.Levels) - (_map.Levels - 1) / Constants.SQRT_TWO * Constants.HALF
                : _cannon.OrbitCenter.Y;

            return new Vector3(_cannon.OrbitCenter.X, clusterY, _cannon.OrbitCenter.Z);
        }

        /// <summary>
        /// Where the game camera stands: back from the field's centre along the horizontal bearing out to the gun,
        /// and below the gun's trunnions. The whole expression is <see cref="GameCameraFit"/>'s since #76 — the
        /// very one the fit searched over, so the pose the camera takes and the pose that was solved for cannot
        /// become two expressions.
        /// </summary>
        private Vector3 GetCanonOffsettedPos() => GameCameraFit.CameraPosition(_cannon, _gameCameraDistance);

        /// <summary>
        /// Places the game camera so the whole play field, the glass ceiling over it and the cannon fit inside
        /// the frustum, and puts the gun a fixed distance in front of the lens. Run on every map load and every
        /// resize, never per frame. Why it has to be solved rather than tuned, and how the two solves alternate,
        /// is <see cref="GameCameraFit"/>'s class doc.
        /// </summary>
        private void FitCannonAndGameCameraToMap()
        {
            if (_map == null || _camera == null || _cannon == null) return;

            //The solver is GameCameraFit's since #76 — it stood here and in the Game line for line. The field's
            //vertical extent is the one thing that genuinely differs and so goes in as arguments: here the
            //lattice frame IS the world frame, so the field's floor is y = 0. The half-extents come off the one
            //CeilingPlate helper because those corners ARE the plate's — a margin written out here again would
            //silently stop agreeing with the drawn plate and the collidable the moment it is retuned.
            CameraFit fit = GameCameraFit.Solve(_cannon,
                _cannonRig.BarrelReach,
                CeilingPlate.FootprintFor(_map.StageSizeX) * Constants.HALF,
                CeilingPlate.FootprintFor(_map.StageSizeZ) * Constants.HALF,
                0f,
                CeilingPlate.TopFaceY(GetCeilingY(_map.Levels)), //Upper face of the ceiling slab
                GAME_FOV,
                _camera.AspectRatio);

            _gameCameraDistance = fit.CameraDistance;
            _gameCameraTargetY = fit.CameraTargetY;

            //The one write the solve implies, made once and after it: the fit itself never touches the gun, so it
            //no longer parks it at every intermediate guess of the alternation
            _cannon.OrbitRadius = fit.CannonOrbitRadius;

            //And the walk the player gets around that rest (W/S in game mode), after OrbitRadius on purpose:
            //assigning the radius parks the gun at rest, and the range clamps against wherever it stands
            _cannon.SetAdvanceRange(fit.CannonMinRadius, fit.CannonMaxRadius);

            Console.WriteLine($"[camera] Field {_map.StageSizeX}x{_map.StageSizeZ}x{_map.Levels}, aspect {_camera.AspectRatio:F2}: " +
                $"camera {_gameCameraDistance:F1} out, aim Y {_gameCameraTargetY:F1}, " +
                $"cannon orbit {_cannon.OrbitRadius:F1} ({_gameCameraDistance - _cannon.OrbitRadius:F1} in front of the camera" +
                $", walk {fit.CannonMinRadius:F1}..{fit.CannonMaxRadius:F1})");
        }

        /// <summary>
        /// What the game camera looks at: the centre of the field, so the map is the thing framed and it
        /// holds still. The view deliberately does **not** ride the aim. The two controls split cleanly -
        /// orbiting the cannon carries the camera around the field with it, aiming turns the barrel inside a
        /// frame that stays put - which is what the game is: pick a cell out of a fixed board and put a ball
        /// of that colour into it. A view that panned with every aim would swing the board the player is
        /// reading, and at full traverse or depression would swing it off the screen entirely.
        /// </summary>
        private Vector3 GetCannonOffsettedTarget() =>
            new(_cannon.OrbitCenter.X, _gameCameraTargetY, _cannon.OrbitCenter.Z);
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

