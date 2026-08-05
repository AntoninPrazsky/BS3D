using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using mgKeys = Microsoft.Xna.Framework.Input.Keys;

namespace Testbed
{
    public class Testbed : Game
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
        private Model _skyModel;
        private byte _skyModelNumber = 1;

        //An explicit "sky=<n>" on the command line pins the starting dome over a startup level's own dome (the
        //dome-testing workflow in CLAUDE.md relies on it). Consumed once the startup map/level has loaded, so a
        //level opened at runtime (F2/drag-drop) still takes its own dome.
        private bool _skyFromCommandLine;

        /// <summary>Number of <c>Skyes/SkyDome*.dae</c> assets the game cycles through.</summary>
        private const byte SKY_DOME_COUNT = 18;

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

        private int _windowWidth;
        private int _windowHeight;

        private GraphicsDeviceManager _graphics;
        private bool _windowed;

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
        //and reaches the deck as ApplyPalette's argument — the same number SceneFrame carries, which is the whole
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

        //When the free camera dips below the sea surface the whole frame is pulled into a blue-green murk so
        //it reads as being underwater (see Tonemap.fx's underwater block). The murk's two colours live on
        //PostProcessPipeline; what stays scene knowledge here is the ramp — full effect this many world
        //units below the mean surface. Sea scene only.
        private const float UNDERWATER_FADE_DEPTH = 7f;

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
        private static readonly float CHROMATIC_ABERRATION = 0.004f;

        //The film grain's mid-tone peak, likewise the game's default figure (see FILM_GRAIN there)
        private static readonly float FILM_GRAIN = 0.10f;

        #endregion

        /// <summary>
        /// Linear scale applied to the scene before the tonemap curve — the renderer's shutter speed.
        /// Overridable with "exposure=&lt;f&gt;" on the command line, which is how a sky dome that is much
        /// brighter or darker than the rest gets checked without a rebuild.
        /// </summary>
        private readonly float _exposure;

        private readonly bool _uncappedFps;
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
        //is built with: RandomBallType, uniform over all eight, because the Testbed's cluster is whatever map was
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

        //Map file to load right after startup (e.g. passed on the command line); mainly for testing
        private readonly string _startupMapPath;

        //Testing: an initial free-camera pose ("campos="/"camtarget=" on the command line), applied once in
        //Initialize so a screenshot can be framed from any vantage (e.g. under the sea, or in on the drain)
        private readonly Vector3? _startupCamPos;
        private readonly Vector3? _startupCamTarget;

        //Testing mode: shoots a ball at a random spot of the structure every second ("autoshoot" on the command line)
        private readonly bool _autoShoot;
        private float _autoShootElapsed;

        //Testing (see Program.cs): "aimcheck" logs, per loaded map, whether the cannon's elevation/traverse clamps
        //let it aim at every cell; "aimshoot" then auto-enters game mode and fires up the field's centre column so
        //the whole aim -> clamp -> shoot path is exercised, logging the elevation each shot wanted versus what it got.
        private readonly bool _aimCheck;
        private readonly bool _aimShoot;
        private float _aimShootElapsed;
        private int _aimShootIndex = -1;                       //-1 until the game-mode entry animation finishes; then 0..AIM_SHOOT_STEPS
        private const int AIM_SHOOT_COLUMN_STEPS = 6;          //samples up the centre column, bottom to top
        private const int AIM_SHOOT_STEPS = AIM_SHOOT_COLUMN_STEPS + 4; //then the four top corners (the steepest facing shots)
        private const float AIM_SHOOT_INTERVAL = 0.6f;         //seconds between shots, so they do not pile up mid-flight

        //Testing mode: loads this map on top of the running one after a delay ("switchmap=<path>" on the command line);
        //exercises the map re-loading path that the F2 dialog and file drag-and-drop use
        private readonly string _switchMapPath;
        private float _switchMapElapsed;
        private bool _switchMapDone;
        private static readonly float SWITCH_MAP_DELAY_SECONDS = 10f;

        //The windowed default is 16:9, the narrowest aspect the game targets (desktop and Xbox, 3840x2160 to
        //3840x1600), so what is framed in a window is the tightest case and a wider display only adds width
        public Testbed(bool windowed = true, int windowWidth = 1600, int windowHeight = 900, string startupMapPath = null, bool autoShoot = false, bool aimCheck = false, bool aimShoot = false, string switchMapPath = null, byte skyNumber = 0, bool uncappedFps = false, int supersampleFactor = 2, float exposure = DEFAULT_EXPOSURE, string scene = null, Vector3? camPos = null, Vector3? camTarget = null)
        {
            //Testing: "campos=x,y,z"/"camtarget=x,y,z" place and aim the free camera at startup (applied in
            //Initialize once the camera exists), so a screenshot can be framed from any vantage.
            _startupCamPos = camPos;
            _startupCamTarget = camTarget;

            //One delegate for the whole run (see the field): it is what PhysicsWorld.Step runs after it has
            //flushed the step's contacts, and an attach changes the body and constraint counts the overlay shows.
            _processContacts = () => { if (_eventHandler.ProcessQueuedContacts() > 0) RecountBallsAndConstraints(); };

            //Testing: "scene=<name>" picks the starting environment, through the one parser every executable
            //now shares (#75 — this was an if/else chain here and a switch in the game, kept in step by hand,
            //which is exactly what a script driving both cannot afford). An unrecognised name leaves the
            //default city standing. The four scenes past the end of the NumPad2 cycle — which still walks only
            //SceneRenderer.CycleLength, the seven a map is authored against — are reachable only this way here.
            if (SceneRenderer.TryParseScene(scene, out SceneKind startupScene)) _scene = startupScene;
            _exposure = exposure > 0f ? exposure : DEFAULT_EXPOSURE;
            _windowed = windowed;
            _startupMapPath = startupMapPath;
            _autoShoot = autoShoot;
            _aimCheck = aimCheck;
            _aimShoot = aimShoot;
            _switchMapPath = switchMapPath;
            _uncappedFps = uncappedFps; //Testing: "nocap" on the command line disables vsync so real rendering headroom can be measured
            _supersampleFactor = Math.Clamp(supersampleFactor, 1, 4); //Testing: "ssaa=<n>" on the command line trades sharpness against fill rate
            _skyFromCommandLine = skyNumber >= 1 && skyNumber <= SKY_DOME_COUNT;
            if (_skyFromCommandLine) _skyModelNumber = skyNumber; //Testing: "sky=<n>" on the command line picks the starting sky dome
            else if (_scene == SceneKind.Sea) _skyModelNumber = SEA_DEFAULT_SKY_DOME; //The sea scene defaults to a darker dome (unless sky= overrode it above)
            else if (_scene == SceneKind.Savanna) _skyModelNumber = SAVANNA_DEFAULT_SKY_DOME; //The savanna defaults to a warm golden dome

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
            _pipeline?.EnsureTarget();
            FitCannonAndGameCameraToMap(); //The frustum's width just changed, and the fit is checked on both axes
        }

        protected override void Initialize()
        {
            IsMouseVisible = true;

            _camera = new BasicCamera3D(DEFAULT_CAMERA_POS, GraphicsDevice.Viewport.AspectRatio, FREE_FOV);

            //Testing camera pose from the command line: position, then aim (the Target setter rebuilds the view
            //and the look angles, so mouse-look carries on from there). Target defaults to the arena at the origin.
            if (_startupCamPos.HasValue)
            {
                _camera.Position = _startupCamPos.Value;
                _camera.Target = _startupCamTarget ?? Vector3.Zero;
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

        private void SwitchGameMode(bool gameMode)
        {
            if (_gameMode == gameMode) return;
            if (_gameModeAnimStarted || _freeModeAnimStarted) return;

            _gameMode = gameMode;
            _info.ShowIcon = gameMode;

            if (_gameMode)
            {
                _cih.ResetMouseModes();       //drop any free-look pan/rotate toggle so it does not resume in game mode
                _mouseAim.Invalidate();       //the first captured frame skips its delta, so grabbing the cursor never jumps the aim
                _gameModeAnimStarted = true;
                _beforeAnimationPosition = _camera.Position;
                _beforeAnimationTarget = _camera.Target;
            }
            else
            {
                //Ease the aim back to its rest direction (~1s SmoothStep in Cannon.Update, not a snap) so the
                //gun is not left cocked at the last mouse aim - the aim persists within game mode, but a fresh
                //session starts neutral. Leaves the orbit position alone.
                _cannon.ResetAim();

                //Leaving game mode while precise aim is engaged: capture the leaned pose so the free-mode exit eases
                //it out to the overview pose (position, target and FOV), instead of snapping ~30 units in one frame.
                _freeExitFromAds = _preciseAim.Blend > 0f;
                if (_freeExitFromAds)
                {
                    _beforeAnimationPosition = _camera.Position;
                    _beforeAnimationTarget = _camera.Target;
                    _beforeAnimationFov = _camera.FieldOfView;
                }
                _preciseAim.Reset(); //the lean is dropped with no ease; the exit animation above carries the pose out
                _mouseAim.Invalidate();
                IsMouseVisible = true;
                _freeModeAnimStarted = true;
            }
        }

        protected override void LoadContent()
        {
            _instancingEffect = Content.Load<Effect>("Shaders/InstancedModel");

            //The meshes, the renderers, the look and the buckets in one construction. No ripple here — that wave
            //is the Game's, and asking for it would also turn the resting heartbeat down to leave room for it
            //(see BallRenderSet.PULSE_DEPTH_RIPPLING), so the Testbed keeps the deeper breath it always drew. The
            //shared instancing effect is handed in and never disposed there, the city, the island, the barrel and
            //the ceiling all drawing through the same copy.
            _balls = new BallRenderSet(GraphicsDevice, _instancingEffect);

            //The pipeline caches its parameters and sets each look value exactly once through the required
            //initializer — fixed for the whole run here (the game alone has the Settings toggles). See #74.
            _pipeline = new PostProcessPipeline(GraphicsDevice,
                Content.Load<Effect>("Shaders/Tonemap"), Content.Load<Effect>("Shaders/Glare"))
            {
                GlareThreshold = GLARE_THRESHOLD,
                GlareIntensity = GLARE_INTENSITY,
                Exposure = _exposure,
                ChromaticAberration = CHROMATIC_ABERRATION,
                FilmGrain = FILM_GRAIN,
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

            #region Contact events

            //No Initialize call on the stream: Simulation.Create has already run NarrowPhaseCallbacks.Initialize,
            //which is what initialises it, and this file used to call it a second time (issue #73) — hooking the
            //stream's BeforeCollisionDetection handler onto the timestepper twice, so the freshness pass walked
            //every listener's previous collisions twice on every step for nothing. PhysicsWorld's constructor
            //owns the one Initialize the program performs.
            _eventHandler = new BallContactEventHandler(_world.Simulation, _world.Events, _ceiling, _physicsBalls, _shotBalls, _fallingBalls);

            #endregion

            _skyModel = Content.Load<Model>("Skyes/SkyDome" + _skyModelNumber);
            //The dome's vertex colors are sRGB; the target it is drawn into is linear
            _sky = new SkyDome(_skyModel, GraphicsDevice, linearVertexColors: true);

            _skyEffect = Content.Load<Effect>("Shaders/Sky");
            _sky.Effect = _skyEffect;
            _skyCameraPositionParam = _skyEffect.Parameters["CameraPosition"];

            //Everything about the clouds that does not change frame to frame, pushed once. The per-frame half —
            //the clock and the camera — goes out in Draw, right before the dome; the two dome-derived colours
            //follow the dome and are ApplySkyLighting's business.
            _clouds.ApplyStaticParameters(_skyEffect, _instancingEffect, SkyLightRig.SUN_DIRECTION);

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

            if (!string.IsNullOrEmpty(_startupMapPath) && File.Exists(_startupMapPath)) DeserializeMapFromFile(_startupMapPath);

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
            if (_aimShoot) SwitchGameMode(true);
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
        /// Every renderer that takes part in the sky-derived lighting: the ball LODs plus the scene objects.
        /// Refilled on every call (into one reused list, so per-frame callers allocate nothing), which keeps
        /// it correct across renderer recreation — the ceiling's, for one, is rebuilt on every map load.
        /// </summary>
        private List<InstancedModelRenderer> SkyLitRenderers()
        {
            _skyLitRenderers.Clear();

            //BallRenderSet.Renderers hands back its own array rather than a read-only view, exactly so this can
            //be walked every frame without boxing an enumerator (its doc names this caller)
            foreach (InstancedModelRenderer ballRenderer in _balls.Renderers) _skyLitRenderers.Add(ballRenderer);

            _skyLitRenderers.Add(_ceilingPlate.Renderer);
            _skyLitRenderers.Add(_cannonRig.Renderer);
            _skyLitRenderers.Add(_cannonRig.GlassRenderer);
            _skyLitRenderers.Add(_cannonRig.CarriageRenderer);
            _skyLitRenderers.Add(_cannonRig.WheelRenderer);
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
        private void ApplySkyLighting()
        {
#if DEBUG
            Console.WriteLine($"[sky] Dome {_skyModelNumber}: zenith {_sky.ZenithColor}, horizon {_sky.HorizonColor}");
#endif

            _rig.SetSky(_sky, _scene);
            _rig.ApplyTo(SkyLitRenderers());

            //The clouds' own two colours follow the dome as well, and the lit side is handed the very radiance
            //the rig gives the scene — one sun, one number (see SkyLightRig.SunRadianceTinted)
            _clouds.ApplyPalette(_skyEffect, _rig.SunRadianceTinted, _rig.ZenithLinear, _rig.HorizonLinear);
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

            //Straight up from the middle of the arena, so the sample is simply where the arena stands —
            //averaged over a patch about as wide as a cloud, since what lights the scene from above is how
            //much of the sky is covered, not what happens to sit over one point of it
            _rig.StepOvercast(_clouds.CoverAround(Vector2.Zero, OVERCAST_SAMPLE_RADIUS), elapsedSeconds);

            //Refilled every frame into one reused list, and pushed by index, so the per-frame path allocates
            //nothing — this is the caller BestPractices.md §3 records the iterator incident for
            _rig.ApplyTo(SkyLitRenderers());
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
            _skyModel = Content.Load<Model>("Skyes/SkyDome" + _skyModelNumber);
            _sky.SkyDomeModel = _skyModel;

            ApplySkyLighting();
        }

        private void SwitchScene()
        {
            _scene = (SceneKind)(((int)_scene + 1) % SceneRenderer.CycleLength);
            Console.WriteLine($"[scene] {_scene}");

            //The sea reads best under a moody sky and the savanna under a warm golden one, so entering either
            //defaults its dome — and SetSkyDome re-derives the rig on its way, so that arm needs nothing more
            //(NumPad1 still cycles freely from there). Every other scene keeps whatever dome is up but must
            //still re-derive: a scene may state its own lighting instead of the dome's, and the rig has to be
            //told which scene it is standing in. Latent rather than visible while the cycle stays inside the
            //seven that all take the dome's — which is exactly why it would have gone unnoticed.
            if (_scene == SceneKind.Sea) SetSkyDome(SEA_DEFAULT_SKY_DOME);
            else if (_scene == SceneKind.Savanna) SetSkyDome(SAVANNA_DEFAULT_SKY_DOME);
            else ApplySkyLighting();
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
                //A broken or unreadable file must not kill the game: a hand-edited level with a typo'd
                //discriminator or sky, a non-rectangular ball array, a dropped folder. Both paths parse fully
                //before the current structure is torn down (LoadLevel / the BallsMap ctor before InstallMap),
                //so logging and returning leaves the running game exactly as it was.
                Console.WriteLine($"[load] Failed to load '{filePath}': {e.Message}");
            }
        }

        /// <summary>
        /// Loads a level: its map through the same path a plain map file takes, then the scene backdrop with
        /// its full config and the sky dome. The file is parsed completely before the current structure is
        /// torn down, so a broken file leaves the running game untouched.
        /// </summary>
        private void LoadLevel(string filePath)
        {
            Level level = Level.Load(filePath);
            BallsMap map = new(level.Map);

            InstallMap(map);

            if (level.Scene != null)
            {
                _scene = level.Scene.Kind;

                if (level.Scene is CitySceneConfig cityConfig)
                {
                    //The city lives outside the SceneRenderer: regenerate the buildings from the config's
                    //layout and hand the window/neon look to the city renderer
                    _cityConfig = cityConfig;
                    _city = new City(seed: 20260720, arenaHalfExtent: ArenaIsland.RADIUS, config: _cityConfig);
                    _cityRenderer.CityConfig = _cityConfig;
                }
                else
                {
                    _sceneRenderer.Apply(level.Scene);
                }
            }

            //The level's dome wins over any scene-entry default (NumPad1 still cycles freely from here), except
            //an explicit command-line sky= pins the startup dome — see _skyFromCommandLine
            if (!_skyFromCommandLine) SetSkyDome(Math.Clamp(level.SkyDome, (byte)1, (byte)18));

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
            _eventHandler.Map = _map;

            FitCeilingToMap(_map);
            FitCannonAndGameCameraToMap();

            _physicsBalls = BallsConstraintsBuilder.BuildBallsStructure(_map.GetStaticBallsArray(), _world.Simulation, _ceiling.BodyReference);
            _eventHandler.PhysicsBalls = _physicsBalls;

            RecountBallsAndConstraints();

            ApplySkyLighting(); //FitCeilingToMap recreated the ceiling renderer, which starts without the sky palette

            if (_aimCheck) LogAimReachability();
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

        private void RecountBallsAndConstraints()
        {
            _info.CustomText = "Balls on scene: " + (_world.Simulation.Bodies.ActiveSet.Count) + "\nConstraints count: " + _world.Simulation.Solver.CountConstraints();
        }

        /// <summary>
        /// Y below which a ball is considered fallen out of the world. Set below the funnel's hole
        /// (<see cref="ArenaIsland.FUNNEL_BOTTOM_Y"/>) so a ball that drops through it falls a visible distance into the
        /// drop below the platform before it is removed.
        /// </summary>
        private static readonly float KILL_PLANE_Y = -42f;

        /// <summary>
        /// Removes balls that can no longer affect gameplay from the simulation and from the given list:
        /// balls that fell below <see cref="KILL_PLANE_Y"/> and balls that came to rest on the ground
        /// (their body fell asleep - flying or rolling bodies never sleep).
        /// </summary>
        /// <returns>Number of removed balls.</returns>
        private int RemoveFallenBalls(List<PhysicsBall> balls)
        {
            int removed = 0;

            for (int i = balls.Count - 1; i >= 0; i--)
            {
                BodyReference body = balls[i].BallReference;

                //The sleep cull is deliberately the Testbed's alone: the Game culls on the kill plane only,
                //because a ball that settles on the island's stone winking out in front of the player reads as a
                //bug whatever it saves (docs/game-session.md). What the two share to the line is the retire below.
                if (body.Pose.Position.Y >= KILL_PLANE_Y && body.Awake) continue;

                //Unregisters the ball's listener if it still has one, then removes the body — that order being
                //PhysicsWorld.RetireBall's whole point, a listener being keyed on a collidable reference Bepu is
                //free to hand to the next body added. Its answer (whether the shot was still unresolved) is what
                //the Game scores a miss on; nothing here keeps score.
                _world.RetireBall(body);
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
            //paused or slowed (F5, F9), because it is what they are, not something they are doing. It reaches
            //the three ball renderers as BallRenderSet.Draw's argument, which is why it is not pushed here.
            _pulseSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;

            //Ease the magazine's post-shot slide towards its resting slots. Wall-clock too, so it glides even while
            //the simulation is paused (F5, F9): the balls sliding down a tube is the gun answering the shot, not
            //something the physics is doing.
            _magazine.Step((float)gameTime.ElapsedGameTime.TotalSeconds);

            //The city runs off the same wall clock, and for the same reason: its windows are lit by people
            //who do not care whether the simulation is running
            _cityRenderer.CityWindowTime = _pulseSeconds;

            UpdateOvercast((float)gameTime.ElapsedGameTime.TotalSeconds);

            if (_simulate)
            {
                //ONE step per rendered frame, of whatever the frame took. That is this executable's own stepping
                //policy and deliberately not the Game's — the Game accumulates the frame time and spends it in
                //whole fixed steps of 1/120 s, because a step that varies with the display runs the simulation in
                //slow motion below 60 FPS and Bepu's guidance is to keep it constant ("Physics in the game" in
                //docs/game-session.md). PhysicsWorld.Step takes one step of exactly the length it is handed and
                //nothing else, so the divergence stays visible in each caller's own loop; F9's slow motion scales
                //the dt right here, where the policy lives. What the component owns is the order INSIDE a step —
                //Timestep, then flush, then the contact work — which is mandatory and per step, not per frame: a
                //handler may only record what the worker threads saw, the flush is what applies those per-worker
                //adds, and a contact queued during a step describes a world the next step has already left behind.
                _world.Step(_slowSimulation ? timeStep * Constants.HUNDREDTH : timeStep, _processContacts);

                #region Fallen balls cleanup

                int removedBalls = RemoveFallenBalls(_shotBalls) + RemoveFallenBalls(_fallingBalls);
                if (removedBalls > 0) RecountBallsAndConstraints();

                #endregion

                #region Shot-trail launch smear

                //Age each muzzle smear and drop it once the launch burst has faded. Inside the simulation
                //gate on purpose: a paused Testbed (P) holds the smears where they are, along with the shot
                //that left them - the Game, whose smears age every frame it updates, does it differently.
                _smears.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

                #endregion

                #region Auto shooting (testing)

                if (_autoShoot && _map != null)
                {
                    _autoShootElapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;

                    if (_autoShootElapsed >= 1f)
                    {
                        _autoShootElapsed = 0f;
                        ShootBall(new Vector3(RANDOM.Next(-4, 5), RANDOM.Next(4, 11), RANDOM.Next(-4, 5)));
                        Console.WriteLine($"[autoshoot] FPS: {_info.CurrentFPS}, balls drawn: {_balls.DrawnCount}/{_collectedBalls}, LOD: {string.Join("/", _balls.LodTotals)}");
                    }
                }

                //Automated aim-and-shoot scan up the field's centre column (testing). Waits for the game-mode
                //entry animation to finish, then fires one shot per interval, aiming the cannon at each height
                //via Cannon.AimAt and logging the elevation wanted vs the elevation the clamp allowed.
                if (_aimShoot && _map != null && _gameMode && !_gameModeAnimStarted && !_freeModeAnimStarted)
                {
                    if (_aimShootIndex == -1) { _aimShootIndex = 0; _aimShootElapsed = AIM_SHOOT_INTERVAL; }

                    if (_aimShootIndex < AIM_SHOOT_STEPS)
                    {
                        _aimShootElapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;

                        if (_aimShootElapsed >= AIM_SHOOT_INTERVAL)
                        {
                            _aimShootElapsed = 0f;
                            AimShootStep(_aimShootIndex);
                            _aimShootIndex++;
                            if (_aimShootIndex >= AIM_SHOOT_STEPS)
                            {
                                //Sweep done: hold the barrel aimed straight up (clamped to MaxElevation, ~80°) and
                                //leave ADS engaged, so the steepest precise-aim view - the one that used to sink the
                                //lens under the island - sits as a stable frame to inspect or screenshot.
                                _cannon.AimAt(new Vector3(_cannon.OrbitCenter.X, 100f, _cannon.OrbitCenter.Z));
                                Console.WriteLine($"[aimshoot] centre-column sweep complete; holding straight-up, ADS lens Y={PreciseAimLens().Y:F1} (island top {ArenaIsland.TOP_Y:F1})");
                            }
                        }
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

            //Before CameraMovement below, which is what reads it: assigned after, the fly camera turned with
            //the PREVIOUS frame's denominator — one frame stale after every frame-time change, and one whole
            //frame wrong after a resize (#80).
            _cih.MouseMovementDenominator = timeStep / Constants.THOUSANDTH;

            if (IsActive)
            {
                _cih.RegisterCurrentInputState();

                //Skip edge-driven input the frame focus returns: while the window was inactive the input state was
                //not registered, so the click (or key) that refocuses would otherwise read as a fresh press against
                //a stale "released". RegisterPreviousInputState below re-syncs it, so edges resume next frame.
                if (_wasActive)
                {
                    foreach (var action in _actions) if (_cih.PressedOnce(action.Key, action.Button)) action.Method();

                    //In game mode the left mouse button fires, completing the shooter idiom (hold RMB to aim, click
                    //to shoot); Space still fires too. The free-cam mouse look is gated off in game mode (last
                    //argument), so the right button means "precise aim" there instead of toggling rotate/pan.
                    if (_gameMode && _cih.PressedOnceMouse(leftButton: true, middleButton: false, rightButton: false)) ShootBall();
                }

                _cih.Update(gameTime);
                _cih.CameraMovement(gameTime, !_gameMode, !_gameMode);
                _cih.RegisterPreviousInputState();
                _wasActive = true;
            }
            else { IsMouseVisible = true; _wasActive = false; }

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
                if (_freeExitFromAds)
                {
                    //Leaving straight from precise aim: ease the whole leaned pose out to the overview pose over the
                    //same animation that widens the FOV, so the camera does not teleport the ~30 units between them.
                    _camera.FieldOfView = Microsoft.Xna.Framework.MathHelper.SmoothStep(_beforeAnimationFov, FREE_FOV, _gameModeAnimStep);
                    _camera.Position = Vector3.SmoothStep(_beforeAnimationPosition, GetCanonOffsettedPos(), _gameModeAnimStep);
                    _camera.Target = Vector3.SmoothStep(_beforeAnimationTarget, GetCannonOffsettedTarget(), _gameModeAnimStep);
                }
                else
                {
                    //Plain overview -> free exit: the camera is already at the overview pose, so only the FOV widens.
                    _camera.FieldOfView = Microsoft.Xna.Framework.MathHelper.SmoothStep(GAME_FOV, FREE_FOV, _gameModeAnimStep);
                }

                _gameModeAnimStep += ANIMATION_SPEED * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

                if (_gameModeAnimStep > 1f)
                {
                    _gameModeAnimStep = 0;
                    _freeModeAnimStarted = false;
                    _freeExitFromAds = false;
                }
            }

            #endregion

            base.Update(gameTime);
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

            if (BallsConstraintsBuilder.ReleaseAllBalls(_physicsBalls, _map, _world.Simulation, _fallingBalls) > 0)
                RecountBallsAndConstraints();
        }

        private void RemoveAllConstraints()
        {
            if (_physicsBalls == null || _physicsBalls.Rank != 3) return;

            XZLevel size = XZLevel.FromArray(_physicsBalls);

            for (byte level = 0; level < size.Level; level++)
                for (byte x = 0; x < size.X; x++)
                    for (byte z = 0; z < size.Z; z++)
                        _physicsBalls[x, z, level]?.RemoveAllConstraints(_world.Simulation);
        }

        protected override void Draw(GameTime gameTime)
        {
            //The scene goes through the HDR target; the crosshair and the text overlay are drawn after the
            //resolve, at native resolution and in display space, so they stay exactly as authored instead
            //of being softened by the downsample and bent by the tonemap curve
            GraphicsDevice.SetRenderTarget(_pipeline.SceneTarget);

            //Clear to the current dome's HORIZON colour (linear), not a fixed blue. The dome is a hemisphere
            //model translated to the camera and drawn without depth, so it covers everything above the
            //horizon; below it the terrain covers what it reaches. But at a wide aspect (21:9) the bottom
            //corners look below the horizon past the terrain's finite edge, and there a fixed clear colour
            //showed through as a blue band. Clearing to the horizon colour makes any such gap blend seamlessly
            //with the hazed skyline the terrain and dome both fade to there, so it is never seen as a seam.
            //The sky-replacing scenes (space, the dream) have no dome and no horizon, so they clear to black
            //instead: their pass covers every pixel of the frame, and black is what would show if it ever did not.
            GraphicsDevice.Clear(SceneRenderer.ReplacesSky(_scene) ? Color.Black : new Color(_rig.HorizonLinear));

            //The clouds run off the same wall clock the balls pulse to, so the weather keeps moving while
            //the simulation is paused or slowed. Handed to both shaders from the one field, which is what
            //keeps the cloud you look at and the shadow it throws the same cloud.
            //
            //Space is the one scene with no weather at all: the dome is not drawn (Space.fx covers the frame),
            //and the cloud coverage is zeroed on the instanced effect so the balls, island and cannon are not
            //crossed by the shadows of a deck nobody can see - InstancedModel.fx calls CloudSunlight
            //unconditionally, and a gain left standing from the scene before would go on shadowing this one.
            _clouds.Time = _pulseSeconds;

            if (SceneRenderer.ReplacesSky(_scene)) _clouds.SuppressOn(_instancingEffect);
            else
            {
                _clouds.ApplyTo(_skyEffect);
                _clouds.ApplyTo(_instancingEffect);

                _skyCameraPositionParam.SetValue(_camera.Position);

                _sky.Draw(_camera);
            }

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            //Stated rather than inherited. The last thing to touch the rasterizer in a frame is the
            //SpriteBatch drawing the overlay, which leaves its own state behind, and the tonemap pass
            //before it leaves CullNone - so what the scene culled depended on which of them ran last.
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            if (_draw)
            {
                //The environment — city, sea or terrain — is the backdrop and the thing seen past the island's
                //edge both. Either way the only physics floor is the drain's own mesh (FunnelPhysics.Build);
                //the round stone island is the platform, and stays in every scene.
                SceneFrame sceneFrame = BuildSceneFrame();

                //Scene point lights (campfire / neon / planetshine) onto the shared instanced effect, so the
                //balls, island, cannon and city are lit by them under every dome, on top of the sun and sky.
                //The clock is the balls' own, so the campfire's light and its flame billboard cannot drift.
                _sceneLights.Apply(_scene, _sceneRenderer, _cityConfig.NeonLook, _pulseSeconds);

                if (_scene == SceneKind.City || _scene == SceneKind.NeonCity)
                {
                    bool neon = _scene == SceneKind.NeonCity;
                    _cityRenderer.CityNeon = neon ? 1f : 0f;
                    _cityRenderer.CityWindowBrightness = neon ? _cityConfig.NeonLook.WindowBrightness : _cityConfig.WindowBrightness;
                    //Frustum-culled and ordered near to far, as the game draws it — see City.PrepareVisible
                    int visibleBuildings = _city.PrepareVisible(_camera);
                    _cityRenderer.Draw(_camera, _city.Visible, visibleBuildings, _sceneEffectParams);
                }
                else
                    _sceneRenderer.DrawEnvironment(_scene, sceneFrame);

                //The forest's scattered trees, boulders and stumps: after the terrain they stand on (with depth,
                //or they would draw through it) and before the island. The state they need is the opaque scene
                //state stated above — alpha blend, depth test and write, cull counter-clockwise — plus this
                //frame's point lights, already on the shared effect; the component touches none of it, so the
                //island's slices below are unaffected.
                if (_scene == SceneKind.Forest) _forestScatter?.Draw(_camera);

                //The round island, opaque: its stone cap and concrete drum. Then the dark pit shaft behind the
                //drain, which is drawn in the solid-terrain scenes only and brings its own culling with it.
                //Each slice owns the states its own geometry needs; where they sit in the frame is this file's
                //decision, which is the whole reason the component hands them over separately.
                _island.DrawIsland(_camera, _sceneEffectParams);
                _island.DrawPit(_camera, _sceneEffectParams, _scene);

                //Into a local because the glazing further down is drawn with the very same pose — it is set into
                //this tube, so the one matrix serves both rather than being built twice a frame
                Matrix barrelWorld = _cannon.BarrelWorld();

                _cannonRig.Draw(_camera, barrelWorld, _sceneEffectParams);
                _cannonRig.DrawCarriage(_camera, _cannon.CarriageWorld(), _cannon.AdvanceTravel, _sceneEffectParams);

                //Every ball on the scene, collected and then put out: one instanced draw call per type and LOD
                //level. BeginFrame empties the buckets and is the only way to fill them, which is what makes the
                //once-per-frame visit structural rather than a rule to remember — the walk below advances each
                //ball's occlusion ease and its arrival glide, so a second collection in one frame would run both
                //at double speed while the drawn frame still looked perfectly correct (see BallRenderSet's
                //remarks; it throws rather than allow it). A ref struct local by design: it allocates nothing and
                //cannot be stashed in a field to bucket the next frame's balls against this frame's camera.
                //
                //Where this sits in the frame is still this file's: over the opaque scene, so the cluster and the
                //gun are in the depth buffer, and before the shots' additive smears and the drain's glass.
                BallDrawFrame frame = _balls.BeginFrame(_camera);

                _collectedBalls = _collector.Collect(frame, (float)gameTime.ElapsedGameTime.TotalSeconds,
                    _physicsBalls, _shotBalls, _fallingBalls);

                //The loaded queue goes into the same open frame, being balls like any other
                CollectMagazineBalls(frame);

                //Wall clock, not the simulation's step: the balls keep breathing while it is paused or slowed
                _balls.Draw(_pulseSeconds);

                //The launch smears trailing the shots, over the opaque scene (which the depth buffer now holds,
                //so the cluster/cannon/platform occlude them) and additive, so they glow through the glare.
                //It states the three states it needs and puts back exactly what it found, so the frame's
                //translucent baseline - which the two glass draws below depend on - is still standing here.
                _smears.Draw(_camera);

                //The drain's gold beads and then its glass, after the shots' smears: the beads are opaque and
                //belong with the opaque scene, and the glass composites over everything already in the frame.
                _island.DrawGlass(_camera, _sceneEffectParams);

                _ceilingPlate.Renderer.Draw(_camera, _ceiling.World, _sceneEffectParams);

                //The gun's own glass last of the three, because it is far and away the nearest: the loaded queue
                //is behind it and in the depth buffer by now, and so are the drain's cone and the ceiling's plate
                //the barrel is seen against. Composited first it would let both of those bleed through it.
                _cannonRig.DrawGlass(_camera, barrelWorld, _sceneEffectParams);

                //Falling snow settles over everything, so it is drawn last, in front of what it should hide
                _sceneRenderer.DrawOverlays(_scene, sceneFrame);
            }

            //Underwater murk: only the sea has water the camera can get under. Ramp it in by how far the lens
            //is below the mean surface (a touch above it, so partial submersion already begins to tint), full
            //by UNDERWATER_FADE_DEPTH down. Zero (a no-op in the shader) in every other scene.
            float underwater = _scene == SceneKind.Sea
                ? Math.Clamp((_sceneRenderer.SeaLevelY + 0.5f - _camera.Position.Y) / UNDERWATER_FADE_DEPTH, 0f, 1f)
                : 0f;

            _pipeline.Resolve(_pulseSeconds, underwater);

            //The crosshair, in display space after the resolve: in free mode it marks where a shot from the camera
            //goes, so it is simply there (opacity 1); in game mode it appears only as precise aim engages, fading
            //in with PreciseAim.Blend, and marks the impact point the camera converges on - the overview's screen
            //centre points at nothing in particular. Everything else about it, the below-0.01 skip included, is
            //Crosshair's.
            _crosshair.Draw(_spriteBatch, _gameMode ? _preciseAim.Blend : 1f);

            base.Draw(gameTime);
        }

        /// <summary>
        /// Adds the magazine's queued balls to this frame's collection along the cannon axis: index 0 at the
        /// muzzle (the spawn point), the rest receding back towards the breech, so the player sees the colour
        /// that will fire and the ones behind it. Drawn as real balls — the same shader, pattern and emission as
        /// every other ball, through the same buckets — and unoccluded, a ball in the bore having nothing packed
        /// around it.
        /// <para>
        /// The magazine deliberately stayed with the callers when the rest of the ball drawing was hoisted:
        /// which colours are loaded, where the bore puts them and (in the Game) the transmute cross-fade are
        /// three different questions, and none of them is <see cref="BallRenderSet"/>'s.
        /// </para>
        /// </summary>
        /// <param name="frame">The collection <see cref="Draw"/> opened, passed along as <c>in</c> rather than
        /// reopened — a second <see cref="BallRenderSet.BeginFrame"/> in one frame is exactly the double-advance
        /// this type refuses.</param>
        private void CollectMagazineBalls(in BallDrawFrame frame)
        {
            //One read of the barrel's pose for the whole queue rather than one per ball, and taken AFTER the gun
            //has been updated this frame - a pose read before the barrel moves makes the queue lag a frame behind
            //the tube it is supposed to be inside, which reads as jitter. The balls take the barrel's own basis,
            //which is what stops them skewing in their slots, and the slide is already applied per slot. The
            //Testbed animates no recoil, so it passes none.
            BorePose pose = _magazine.Pose(_cannon, _cannonRig.PivotToFrontBall);

            //BallDrawFrame.Add rather than AddOriented: BorePose.SlotWorld has already built the matrix, writing
            //the slot's translation into the barrel's own basis rather than multiplying one in, and it hands the
            //position back because the LOD is picked by distance and reading a translation out of a matrix to
            //measure one is what goes wrong the day something is scaled.
            for (int slot = 0; slot < Magazine.SIZE; slot++)
            {
                Matrix world = pose.SlotWorld(slot, out Vector3 position);

                frame.Add(_magazine.Peek(slot), position, world, BallRenderSet.UNOCCLUDED);
            }
        }

        private void SetGraphics(bool windowed = false)
        {
            _graphics.PreferredBackBufferWidth = windowed ? _windowWidth : GraphicsDevice.DisplayMode.Width;
            _graphics.PreferredBackBufferHeight = windowed ? _windowHeight : GraphicsDevice.DisplayMode.Height;
            _graphics.IsFullScreen = !windowed;

            _graphics.SynchronizeWithVerticalRetrace = !_uncappedFps;

            _graphics.ApplyChanges();

            //Null-conditional for the constructor's call, which runs before LoadContent has built the
            //pipeline (the old in-class EnsureSceneTarget guarded on GraphicsDevice == null the same way)
            _pipeline?.EnsureTarget();

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
            _spriteBatch?.Dispose();
            //Contact stream, simulation, dispatcher, pool — in that order, which is the reverse of the order they
            //were built in and PhysicsWorld.Dispose's to get right. It includes the ContactEvents this used to
            //leak: the stream unhooks itself from the timestepper, so it has to go while the simulation is still
            //there, and the pool both allocated from has to outlive the two.
            _world?.Dispose();
        }

        private void InitializeShooting()
        {
            //No shot-ball template here: the body description every shot is stamped from is PhysicsWorld's, built
            //once with the simulation and copied per shot rather than held as a field and written over. Its bare
            //shape index (rather than a CollidableDescription with a speculative margin) is what gives the shot
            //continuous collision detection, which at SHOOT_MULTIPLIER it cannot do without.
            _shotBalls = new List<PhysicsBall>();
            _fallingBalls = new List<PhysicsBall>();

            //The constructor deals a full queue, so the player has something to read from the first frame. The
            //next-colour policy is handed in and no hooks are: the Testbed has no per-slot state to carry through
            //an advance (the colour transmutation is the Game's).
            _magazine = new Magazine(RandomBallType);
        }

        private static BallType RandomBallType() =>
            (BallType)RANDOM.Next((int)BallType.Type1, (int)BallType.Type8 + 1);

        private void ShootBall(Vector3? targetOverride = null)
        {
            //In game mode the shot leaves from the ball the player watched sitting at the head of the queue,
            //not from the pivot in the middle of the barrel, so the drawn ball and the physics one that
            //replaces it are at the same place and the shot reads as that ball leaving the bore
            var sourcePosition = _gameMode ? _cannon.MuzzlePosition(_cannonRig.PivotToFrontBall) : _camera.Position;
            var shootTarget = targetOverride ?? (_gameMode ? _cannon.AimTarget : _camera.Target);

            var direction = shootTarget - sourcePosition;
            direction.Normalize();
            Vector3 launchDirection = direction; //unit, before it is scaled to a velocity below
            direction *= SHOOT_MULTIPLIER;

            PhysicsBall ball = new()
            {
                //Added to the simulation and registered as a contact listener in one call, in that order — a
                //listener is keyed on a collidable reference, so the body has to exist first. This is the only
                //place anything is registered, which is what makes "every listener is a shot in the air" true;
                //RetireBall is the unregister the TODO that stood here asked for.
                //ToNumerics is the framework's own crossing into Bepu's vector type, which this file used to
                //write out by hand here and call by name two hundred lines below
                BallReference = _world.AddShotBall(sourcePosition.ToNumerics(), direction.ToNumerics(), _eventHandler),
                Type = _magazine.Peek() //The colour the player saw loaded at the muzzle - so aiming for it means something
            };

            //Advance the magazine: the fired ball's slot empties, the queue shifts up and a new one loads
            _magazine.Advance();

            _shotBalls.Add(ball);
            RecountBallsAndConstraints();

            //Give the shot its launch smear: a colour streak at the muzzle, along the shot, fading over its own
            //short life (aged in Update, drawn in Draw). Only the ball's authored tint is handed over - decoding
            //it to linear, lifting its peak off the floor and boosting it to a glowing radiance is the smear's
            //own rule, and it was written out here and in the Game identically until #76.
            _smears.Add(sourcePosition, launchDirection, BasicEffectParamsProvider.GetDiffuseTintByType(ball.Type));
        }

        private void UpdateCannon(GameTime gameTime)
        {
            //Free mode drives no cannon input — A/D belong to the fly camera's strafe and the aim stays parked —
            //so only the pose easing runs, and a barrel caught mid-traverse settles instead of freezing.
            //Returning BEFORE the snapshot below is the point: those three GetState calls are real OS queries
            //(an XInput poll for the pad), _cih already took this frame's set, and free mode — where the Testbed
            //spends most of its life — was paying both for nothing (#80).
            if (!_gameMode)
            {
                _cannon.Update(gameTime);
                return;
            }

            //One snapshot of each input device for the whole game-mode frame: every extra GetState call
            //re-queries the OS (a real XInput poll for the pad), and two reads in one frame can even
            //disagree about a key pressed between them. In game mode this is still a SECOND set after _cih's —
            //sharing that one means threading CameraInputHelper's snapshot out through a library API, which #80
            //records as declined: the Testbed is not the product, and the cost is one extra poll per device.
            KeyboardState keyboard = Keyboard.GetState();
            MouseState mouse = Mouse.GetState();
            GamePadState pad = GamePad.GetState(PlayerIndex.One);

            //Orbiting the cannon around the field is on A/D and walking it towards the field and back on W/S —
            //in the free fly camera all four stay the camera's own, which is why the free-mode early-out above
            //exists. Walking closes on the cluster (a steeper shot up into its underside) or backs off for a
            //flatter one; the ends of the walk are rubber (Cannon.ADVANCE_EASE_ZONE), not stops. Neither
            //movement touches the aim: the mouse owns it (below) and holds it wherever the player leaves it.
            if (keyboard.IsKeyDown(mgKeys.A)) _cannon.Orbit(1f);
            if (keyboard.IsKeyDown(mgKeys.D)) _cannon.Orbit(-1f);

            if (keyboard.IsKeyDown(mgKeys.W)) _cannon.Advance(1f);
            if (keyboard.IsKeyDown(mgKeys.S)) _cannon.Advance(-1f);

            _cannon.Update(gameTime);

            //The camera must follow the cannon's pose from THIS frame (after Update above has moved it).
            //Reading the pose before the move made the camera lag one frame behind, so any frame-time
            //fluctuation (shooting, contact processing) showed up as the cannon jittering on screen (#29).
            if (!_gameModeAnimStarted)
            {
                //The mouse aims the cannon throughout game mode - in the overview as well as in precise aim (the
                //arrow keys are retired). The cursor is captured (hidden and re-centred) the whole time we are
                //actively playing, and the mouse delta drives Cannon.Aim before the pose is read so the camera does
                //not lag it (#29). Precise aim (RMB / left trigger) changes nothing about the aiming - it only leans
                //the camera in over the barrel and down the aim.
                //IsActive gates the capture: the gamepad trigger reads globally through XInput, and losing focus must
                //free the cursor rather than keep grabbing it (the else branch).
                if (IsActive && _map != null && !_aimShoot) UpdateMouseAim(gameTime, mouse, pad);
                else { _mouseAim.Invalidate(); IsMouseVisible = true; }

                //Stepped every frame, held or not: an unheld frame is how the lean eases back out, which is what
                //makes losing focus a fade rather than a drop. Every gate on the held flag is this file's - IsActive
                //(the gamepad trigger reads globally through XInput, and an alt-tabbed window must not stay leaned
                //in), the free-mode exit animation, and a loaded field.
                bool adsHeld = IsActive && !_freeModeAnimStarted && _map != null && PreciseAim.ButtonHeld(mouse, pad);
                _preciseAim.Step(adsHeld, (float)gameTime.ElapsedGameTime.TotalSeconds);

                //The muzzle is read after _cannon.Update above, for the same reason the camera pose is (#29). The
                //cluster centre is this file's own derivation off the loaded map - PreciseAim deliberately does not
                //learn what a map is.
                AimPose aim = _preciseAim.BlendedPose(GetCanonOffsettedPos(), GetCannonOffsettedTarget(), GAME_FOV,
                    _cannon.MuzzlePosition(_cannonRig.PivotToFrontBall), _cannon.AimDirection, ClusterCentre());

                //The order FOV -> Position -> Target is required: the Target setter rebuilds the view last, with
                //world up (which is also where the ADS lens's view up comes from for free).
                _camera.FieldOfView = aim.FieldOfView;
                _camera.Position = aim.Position;
                _camera.Target = aim.Target;
            }
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
        /// Where the precise-aim lens sits this instant, for the two "aimshoot" diagnostics that check it stays
        /// above the stone island at the steep corner shots, where it used to sink through the disc. The floor
        /// that holds it there is <see cref="PreciseAim.FLOOR_CLEARANCE"/> over the local stone
        /// (<see cref="ArenaIsland.FloorHeightAt"/>).
        /// </summary>
        private Vector3 PreciseAimLens() =>
            PreciseAim.LensPosition(_cannon.MuzzlePosition(_cannonRig.PivotToFrontBall), _cannon.AimDirection);

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

        private const float RAD_TO_DEG = 180f / MathF.PI;

        /// <summary>
        /// Diagnostic ("aimcheck"): reports whether the cannon can be aimed at every cell of the loaded map, which
        /// is what makes a level finishable. The clean shot at a cell is from the orbit angle that <i>faces</i> it
        /// (the cell on the near side): the ball rises from the gun straight to it over open ground. The opposite
        /// angle is geometrically shallower but fires across the whole hanging cluster, so it is obstructed for
        /// anything high — this facing angle is the one that actually has to fit the elevation clamp. It steepens
        /// with height and with distance out from the field's axis, so the top corners of a large map bind. Logs
        /// the steepest required facing elevation against the clamp and a PASS/FAIL.
        /// </summary>
        private void LogAimReachability()
        {
            if (_map == null || _cannon == null) return;

            float orbitRadius = _cannon.OrbitRadius;
            float trunnionsY = _cannon.Position.Y;

            //The test itself is AimReachability's since #76 — a pure function of the map and the gun's orbit, so
            //the map editor can ask it before saving a level rather than a script having to read a console line.
            //The three lines below stay here on purpose: they are a CLI surface .claude/skills/verify documents,
            //so their exact wording is a contract and belongs with the executable that publishes it.
            AimReachabilityResult reach = AimReachability.Check(_map, orbitRadius, trunnionsY, Cannon.MaxElevation);

            string verdict = reach.Pass
                ? "PASS - every cell can be shot while facing it"
                : $"FAIL - {reach.UnreachableCells}/{reach.TotalCells} cells need more up-elevation than the clamp allows (unfinishable)";

            Console.WriteLine($"[aimcheck] Field {_map.StageSizeX}x{_map.StageSizeZ}x{_map.Levels}: cannon orbit R={orbitRadius:F1}, trunnions Y={trunnionsY:F1}");
            Console.WriteLine($"[aimcheck]   elevation clamp [{Cannon.MinElevation * RAD_TO_DEG:F1}, {Cannon.MaxElevation * RAD_TO_DEG:F1}] deg, traverse +/-{Cannon.MaxTraverse * RAD_TO_DEG:F0} deg");
            Console.WriteLine($"[aimcheck]   steepest cell ({reach.WorstCell.X},{reach.WorstCell.Z},{reach.WorstCell.Level}) at Y={reach.WorstCellY:F1} needs {reach.WorstElevation * RAD_TO_DEG:F1} deg facing elevation  ->  {verdict}");
        }

        /// <summary>
        /// One step of the "aimshoot" scan. Steps 0..N-1 walk up the field's centre column; the last four are its
        /// top corners, the steepest facing shots. For each the carriage is orbited to face the cell and the cannon
        /// aimed at it, then a shot is fired through the normal game-mode path (so any attach is reported by the
        /// usual contact logging). Logs the elevation the aim asked for against what the clamp allowed, so a shot
        /// held short by the clamp is obvious.
        /// </summary>
        private void AimShootStep(int step)
        {
            byte topLevel = (byte)(_map.Levels - 1);
            XZLevel cell;
            string label;

            if (step < AIM_SHOOT_COLUMN_STEPS)
            {
                byte level = (byte)(topLevel * step / (AIM_SHOOT_COLUMN_STEPS - 1));
                cell = new XZLevel(_map.StageSizeX / 2, _map.StageSizeZ / 2, level);
                label = "centre";
            }
            else
            {
                byte lastX = (byte)(_map.StageSizeX - 1), lastZ = (byte)(_map.StageSizeZ - 1);
                cell = (step - AIM_SHOOT_COLUMN_STEPS) switch
                {
                    0 => new XZLevel(0, 0, topLevel),
                    1 => new XZLevel(lastX, 0, topLevel),
                    2 => new XZLevel(0, lastZ, topLevel),
                    _ => new XZLevel(lastX, lastZ, topLevel),
                };
                label = "top corner";
            }

            Vector3 target = _map.GetRealCenteredPosition(cell);

            _cannon.OrbitToFace(target); //stand facing the cell so the shot is the clean, steep facing one
            bool reachable = _cannon.CanAimAt(target, out float wantedElevation, out _);
            _cannon.AimAt(target);

            Vector3 dir = _cannon.AimTarget - _cannon.Position;
            float gotElevation = MathF.Atan2(dir.Y, MathF.Sqrt(dir.X * dir.X + dir.Z * dir.Z));

            //ADS lens Y against the island top (ArenaIsland.TOP_Y): confirms the precise-aim camera stays above the floor
            //even at the steep corner shots, where it used to sink through the stone disc.
            float adsLensY = PreciseAimLens().Y;

            Console.WriteLine($"[aimshoot] {label} ({cell.X},{cell.Z},{cell.Level}) Y={target.Y:F1}: " +
                $"want {wantedElevation * RAD_TO_DEG:F1} deg, got {gotElevation * RAD_TO_DEG:F1} deg  ->  {(reachable ? "reachable" : "CLAMPED SHORT")}" +
                $"; ADS lens Y={adsLensY:F1} (island top {ArenaIsland.TOP_Y:F1})");

            ShootBall();
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

        /// <summary>
        /// Drives the cannon's aim from the mouse throughout game mode (the overview as well as precise aim; the
        /// arrow keys are retired), and from the pad's right stick. The arithmetic and both dials are
        /// <see cref="MouseAim"/>'s since #76 — including why the delta is taken against the <b>live</b> viewport
        /// centre and divided by the frame time. What stays here is the order: the cursor is hidden, the delta
        /// applied, the cursor re-centred, and only then the pad added.
        /// </summary>
        private void UpdateMouseAim(GameTime gameTime, MouseState mouse, GamePadState pad)
        {
            int cx = GraphicsDevice.Viewport.Width / 2;
            int cy = GraphicsDevice.Viewport.Height / 2;

            IsMouseVisible = false;

            _mouseAim.ApplyCursor(_cannon, mouse, cx, cy, gameTime);
            _mouseAim.Recentre(cx, cy);

            MouseAim.ApplyPad(_cannon, pad, gameTime);
        }
    }

    #region Contact event

    //WIP
    public class BallContactEventHandler : IContactEventHandler
    {
        public Simulation Simulation;
        private ContactEvents _contactEvents;
        private KinematicBody _ceiling;
        public BallsMap Map;
        public PhysicsBall[,,] PhysicsBalls;
        public List<PhysicsBall> ShotBalls;
        public List<PhysicsBall> FallingBalls;

        public BallContactEventHandler(Simulation simulation, ContactEvents contactEvents, KinematicBody ceiling, PhysicsBall[,,] physicsBalls, List<PhysicsBall> shotBalls, List<PhysicsBall> fallingBalls)
        {
            Simulation = simulation;
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

            //An indexed walk rather than LINQ, the Game's FindShotBall reasoning: this runs per queued contact
            //on the shot path, and Where().FirstOrDefault() allocated a closure and two iterators per call for
            //a list that rarely holds more than a ball or two (#80)
            PhysicsBall physicsBall = null;
            for (int i = 0; i < ShotBalls.Count; i++)
                if (ShotBalls[i].BallReference.Handle == shotBallHandle) { physicsBall = ShotBalls[i]; break; }

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

            BallsReleased releasedBalls = BallsConstraintsBuilder.ReleaseSameTypeCluster(physicsBall, PhysicsBalls, Map, Simulation, FallingBalls);

#if DEBUG
            if (releasedBalls.Any) Console.WriteLine($"Released a cluster of type {physicsBall.Type}: {releasedBalls}");
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

