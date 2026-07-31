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
using static Prazsky.BS3D.Physics.Simu;
using mgKeys = Microsoft.Xna.Framework.Input.Keys;

namespace Testbed
{
    public class Testbed : Game
    {
        private BasicCamera3D _camera;

        #region Instanced ball rendering (issues #19 and #28)

        private static readonly int BALL_TYPE_COUNT = (int)BallType.Type8;   //red/green/blue/white + cyan/magenta/yellow/black

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

        //Last frame's statistics: instances drawn (bucket contents, incl. the magazine preview) vs bodies
        //collected — there is no frustum culling (measured as saving nothing on this scene), so the two
        //differ by the magazine balls, not by any cull
        private int _drawnBalls;
        private int _collectedBalls;

        #endregion

        #region Scene object rendering (same lit shader as the balls, drawn one instance at a time)

        private Effect _instancingEffect;
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

        private static readonly float GROUND_PLATEAU_Y = -9f;

        /// <summary>
        /// Reference height below the island top that the ground-proximity occlusion darkens against: the
        /// balls' bellies and the downward-facing parts of the scene objects darken as they approach it
        /// (handed to the shader as GroundHeight). Kept from the old ground-block layout, whose recessed
        /// centre block's top sat at -9.5.
        /// </summary>
        private static readonly float GROUND_TOP_Y = -9.49f;

        #endregion

        private KinematicBody _ceiling;
        private TypedIndex _ceilingShapeIndex;

        /// <summary>
        /// Size of the ceiling before a map is loaded.
        /// </summary>
        private static readonly float DEFAULT_CEILING_SIZE = 10f;

        private Simulation _simulation;
        private ThreadDispatcher _threadDispatcher;
        private BufferPool _bufferPool;

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

        //Where the game camera stands relative to the cannon's trunnions: back along GetCannonDirection (the
        //horizontal away from the field) and up - here *below* them, near the arena floor, because the player
        //is aiming at the underside of a cluster hanging overhead and needs to see that face, not the top of
        //the gun. Note how little of the angle on the cluster is actually the camera's to give: it is
        //atan((clusterY - cameraY) / horizontal distance), and a camera behind the gun is always further out
        //horizontally than the gun itself, so it sees the cluster *flatter* than the 30 degrees the barrel
        //has. On the floor it is about 25; the 40-odd of a real from-below view would need it at y = -20.
        //What the drop actually buys is the range from 5 degrees (edge-on, the cluster a strip at the top of
        //the frame) to 25, and that is the whole difference between reading the layout and not.
        private static readonly float GAME_CAMERA_HEIGHT = -1.5f;

        //Fraction of the frustum the fit is allowed to use, so nothing sits exactly on the frame edge.
        private static readonly float GAME_CAMERA_FIT_MARGIN = 0.92f;

        //How far back the game camera stands and how high it aims - both solved per map and per display by
        //FitGameCameraToMap rather than tuned, because both of its inputs move underneath a fixed number.
        //The defaults only cover the frames before the first map is loaded.
        private float _gameCameraDistance = 37f;
        private float _gameCameraTargetY = 3f;

        //Precise-aim (ADS) sub-mode of game mode: hold the right mouse button (or the gamepad left trigger) to
        //lean in over the barrel and look straight down the aim, so the map reads clearly and the shot goes where
        //the crosshair is. _adsBlend eases 0->1 while held (0 == the exact game-mode overview pose);
        //_adsMouseInitialized skips the first captured frame so acquiring the cursor never yanks the aim.
        private float _adsBlend = 0f;
        private bool _adsMouseInitialized = false;

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

        #region City, island and funnel (the city is the default of the seven scenes)

        private City _city;
        private BoxMesh _unitBox;
        private InstancedModelRenderer _cityRenderer;
        private CitySceneConfig _cityConfig = new();
        private IslandMesh _arenaIslandMesh;
        private SurfaceTexture _stoneTexture, _concreteTexture;
        private InstancedModelRenderer _arenaCapRenderer, _arenaBodyRenderer;
        private Microsoft.Xna.Framework.Matrix _arenaDiscWorld;
        private FunnelMesh _funnelMesh;
        private InstancedModelRenderer _funnelRenderer;
        private FunnelRimsMesh _funnelRimsMesh;
        private InstancedModelRenderer _funnelRimsRenderer;
        private BasicEffectParams _funnelRimEffectParams;
        private Microsoft.Xna.Framework.Matrix _funnelWorld;

        //A dark pit shaft behind the glass funnel, drawn in the solid-terrain scenes only (mountain, meadow,
        //savanna, desert, forest). The terrain there has its footprint cut out (see TerrainHoleRadius); without a dark
        //backing the transparent funnel would just show the bright sky-haze behind the hole and read as a glass
        //ring on the ground, not a drain. This wide dark cone nests the funnel and descends past the kill plane,
        //so the drain reads as a deep, dark well going down into the earth. Not drawn in the sea (water fills it)
        //or the city (the city's own canyon falls away below the arena).
        private FunnelMesh _pitMesh;
        private InstancedModelRenderer _pitRenderer;
        private Microsoft.Xna.Framework.Matrix _pitWorld;

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

        //Scene point lights (the savanna's campfire, the neon city's neon) applied to the shared instanced
        //effect each frame, so the balls, island, cannon and city are lit by them on top of the sun and the
        //dome - present under every dome. MAX matches MAX_SCENE_LIGHTS in the shaders.
        private const int MAX_SCENE_LIGHTS = 8;
        private readonly Microsoft.Xna.Framework.Vector3[] _sceneLightPos = new Microsoft.Xna.Framework.Vector3[MAX_SCENE_LIGHTS];
        private readonly Microsoft.Xna.Framework.Vector3[] _sceneLightColor = new Microsoft.Xna.Framework.Vector3[MAX_SCENE_LIGHTS];
        private readonly float[] _sceneLightRange = new float[MAX_SCENE_LIGHTS];

        //Cached in LoadContent: ApplySceneLights runs every frame, and the by-name indexer is a linear scan
        //over the instanced effect's ~70 parameters. Starts at -1 so the first frame always pushes a count.
        private EffectParameter _sceneLightPositionParam;
        private EffectParameter _sceneLightColorParam;
        private EffectParameter _sceneLightRangeParam;
        private EffectParameter _sceneLightCountParam;
        private int _lastSceneLightCount = -1;

        //The neon city's ring of magenta/cyan point lights (count, range, radius, height, colours) now lives in
        //_cityConfig.NeonLook; the neon branch of ApplySceneLights reads it. Colours are bright linear radiance.

        /// <summary>
        /// Top of the stone island's surface, read off the physics plateau (<see cref="GROUND_PLATEAU_Y"/>) so
        /// the drawn floor sits exactly where the balls rest rather than being guessed. The funnel rim and the
        /// disc's top are flush with it.
        /// </summary>
        private static readonly float ARENA_Y = GROUND_PLATEAU_Y + Constants.HALF;

        //When the free camera dips below the sea surface the whole frame is pulled into a blue-green murk so
        //it reads as being underwater (see Tonemap.fx's underwater block). The murk's two colours live on
        //PostProcessPipeline; what stays scene knowledge here is the ramp — full effect this many world
        //units below the mean surface. Sea scene only.
        private const float UNDERWATER_FADE_DEPTH = 7f;

        //The drain funnel is glass; the platform around it is a cast-concrete drum with a dressed stone top
        private static readonly Vector3 ARENA_GLASS_COLOR = new(0.42f, 0.62f, 0.72f);

        //Under the figure the old flat disc carried, and the reason is worth keeping: the photograph it used
        //to project was black over half its canvas, so it was silently acting as an exposure control. Same
        //vantage, same nominal albedo, only the texture swapped: the top face measured 151 grey with the
        //broken photograph and 181 with a texture whose mean is 1 by construction - half a stop brighter for
        //a number nobody changed. Any albedo carried over from the old surface arrives overexposed.
        private static readonly Vector3 ARENA_STONE_COLOR = new(0.52f, 0.51f, 0.49f);

        //Concrete: a plain cool concrete grey, within a hair of the cannon's own steel (CANNON_COLOR) - and
        //deliberately NOT tuned bluer than that. A vertical face in this world comes back distinctly warm,
        //because the key light is carried halfway to the horizon colour (SKY_TINT_STRENGTH) and half of the
        //hemisphere ambient it can see is the ground bounce, which is that horizon again. That is the rig
        //doing what it exists to do: measured in the same frame, the cannon's steel reads 89,57,35 and this
        //wall 82,51,31, and the city's facades are the same family. An albedo pushed blue to cancel it would
        //make the platform the one object in the scene that does not take the light everything else does.
        //Not darker than the stone it carries, either - a wall that starts dark as well ends up a black band
        //with no material in it. (See ARENA_STONE_COLOR for why both albedos read lower than they used to.)
        private static readonly Vector3 ARENA_CONCRETE_COLOR = new(0.45f, 0.47f, 0.50f);

        //The arena is a small round island now, not a big square plaza: the drain funnel in the centre, a
        //platform around it out to this radius, then its edge and the scene beyond. Kept just big enough to
        //stand the funnel in - the whole point is that the scene (sea, dunes, city...) fills the frame rather
        //than a plaza. The stone runs from the funnel's rim (FUNNEL_TOP_RADIUS) out to here.
        private static readonly float ARENA_DISC_RADIUS = 26f;
        private static readonly float ARENA_EDGE_HEIGHT = 5f;

        //Outer edge of the flat, level part of the top: the coping falls away past it, so this and not
        //ARENA_DISC_RADIUS is where the physics floor ends - otherwise a ball rests on air over the wash.
        private static readonly float ARENA_FLOOR_RADIUS = IslandMesh.FloorRadius(ARENA_DISC_RADIUS);

        //Twice the drain's facet count. The mouldings are small enough that a coarse ring would show its
        //corners along the bright chamfer lines, which is exactly where the eye is drawn.
        private const int ARENA_SEGMENTS = FUNNEL_SEGMENTS * 2;

        //World units one tile of each procedural texture spans, far finer than the 30 the marble photograph
        //was mapped at - where one tile covered more than half the platform and no grain read at all.
        private static readonly float ARENA_STONE_SPAN = 4f;
        private static readonly float ARENA_CONCRETE_SPAN = 3.5f;

        //The drain funnel that replaces the recessed centre: a glass cone the shot balls fall into and roll
        //down, dropping through the hole at the bottom - below the map, where the kill plane removes them. The
        //top rim is flush with the platform and sized to the old bath, so it catches the balls that used to
        //pile there; the walls are steep (~55 degrees) so the balls run down to the hole rather than resting.
        private static readonly float FUNNEL_TOP_RADIUS = 14f;   //meets the stone disc's inner hole exactly; the gold rim bead rings the junction
        private static readonly float FUNNEL_HOLE_RADIUS = 1.8f;
        private static readonly float FUNNEL_TOP_Y = ARENA_Y;    //rim flush with the platform top
        private static readonly float FUNNEL_BOTTOM_Y = -27.5f;  //hole ~19 below the rim
        private const int FUNNEL_SEGMENTS = 64;

        //The funnel is glass, but more opaque than the glass ceiling so it reads clearly as a solid
        //frosted-glass drain rather than an almost-invisible sheet.
        private static readonly float FUNNEL_GLASS_ALPHA = 0.55f;

        //The dark pit shaft the funnel drains into in the solid-terrain scenes (see the _pitMesh field). The
        //terrain's own footprint is cut out (TERRAIN_HOLE_RADIUS, kept just inside the disc's rim so the terrain
        //tucks under the stone edge with no gap) so nothing solid shows through the drain; this dark cone then
        //backs the glass. It must HUG the glass funnel - a wide cone would hide behind the disc's stone ring and
        //leave the bright hole showing through the narrow (radius 14) aperture - so it shares the funnel's mouth
        //and descends just outside it, a near-black twin dropping well past the kill plane. A ball runs down the
        //glass into darkness and is culled inside the pit, not against the bright sky. Near-matte (its specular
        //ambient turned right down) so no dome bleaches the well; the disc's gold rim bead hides the shared mouth.
        private static readonly float TERRAIN_HOLE_RADIUS = ARENA_DISC_RADIUS - 2f;   //24: the terrain footprint that is cut out
        private static readonly float PIT_TOP_Y = FUNNEL_TOP_Y;                        //shares the funnel's mouth, right behind the glass
        private static readonly float PIT_BOTTOM_Y = -46f;                            //below KILL_PLANE_Y, so balls vanish inside the pit
        private static readonly float PIT_HOLE_RADIUS = 1.2f;                          //nearly closed at the bottom - a dark receding throat
        private static readonly Vector3 PIT_COLOR = new(0.03f, 0.03f, 0.035f);         //near-black, a touch cool

        //A polished-gold metal bead runs around both circles of the funnel (the wide top rim and the small
        //bottom hole), which is what makes the glass drain read at a glance. Drawn as two tori with the
        //metal path (Metalness = 1): the gold diffuse keeps it visible in any scene, the gold specular is
        //its reflectance so it mirrors the sky in gold, and a tight specular power keeps the highlight sharp.
        private static readonly Vector3 FUNNEL_RIM_COLOR = new(0.62f, 0.44f, 0.13f);        //warm gold diffuse (sRGB)
        private static readonly Vector3 FUNNEL_RIM_SPECULAR = new(1f, 0.83f, 0.48f);        //gold reflectance (sRGB)
        private const float FUNNEL_RIM_SPECULAR_POWER = 80f;                                //polished: a tight highlight
        private static readonly float FUNNEL_RIM_TOP_TUBE = 0.5f;                           //bead radius at the top rim
        private static readonly float FUNNEL_RIM_HOLE_TUBE = 0.3f;                          //bead radius at the hole
        private const int FUNNEL_RIM_TUBE_SEGMENTS = 16;                                    //facets around each bead

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

        private BodyDescription _shotBall;
        private List<PhysicsBall> _shotBalls;

        //Balls released from the structure (matched clusters and balls that lost their connection to the ceiling).
        //They are no longer part of the map, but their bodies keep falling in the simulation, so they still have to be drawn.
        //RemoveFallenBalls cleans them up once they fall out of the world or come to rest.
        private List<PhysicsBall> _fallingBalls;

        private static readonly float SHOOT_MULTIPLIER = 200f;
        private static readonly Random RANDOM = new();

        //Launch smear: a colour streak left at the muzzle when a ball fires, stretched in the flight direction
        //and fading over a fraction of a second, to sell how hard the ball leaves the cannon (Shaders/ShotTrail.fx).
        //Anchored at the muzzle (not following the ball, which attaches in ~0.075s - far too brief to read); one
        //additive billboard per live smear. Brightest and widest at the leading (far) end, clear of the barrel,
        //tapering back to the muzzle end, which is mostly hidden behind it - a muzzle-bright streak shows only
        //its faint tapering tip and reads as a thin thread (the mistake this replaced).
        private Effect _shotTrailEffect;
        private VertexBuffer _shotTrailVertexBuffer;
        private IndexBuffer _shotTrailIndexBuffer;
        private sealed class ShotTrail { public Vector3 Origin; public Vector3 Direction; public float Age; public Vector3 Color; }
        private readonly List<ShotTrail> _shotTrails = new();
        private const float TRAIL_LIFETIME = 0.45f;     //seconds the launch smear lasts - long enough not to be missed
        private const float TRAIL_LENGTH = 7f;          //world length of the streak, from the muzzle along the shot
        private const float TRAIL_LEAD_WIDTH = 0.72f;   //half-width at the leading (far) end - bright and clear of the barrel
        private const float TRAIL_MUZZLE_WIDTH = 0.42f; //half-width at the muzzle end (mostly hidden behind the barrel)
        private const float TRAIL_BRIGHTNESS = 3.0f;    //radiance boost so the streak glows and blooms through the glare
        private const float TRAIL_COLOR_FLOOR = 0.12f;  //min peak channel, so even the near-black ball leaves a faint smear

        private Cannon _cannon;
        private CannonMesh _cannonMesh;

        //The cannon's magazine: the next ball to fire (index 0) and the ones queued behind it, shown loaded
        //in the barrel so the player can see what is coming and aim for it. The queue never empties - firing
        //shifts it forward and a fresh random type drops in at the back.
        private const int MAGAZINE_SIZE = 5;
        private readonly BallType[] _magazine = new BallType[MAGAZINE_SIZE];

        //How far the queue is still displaced backwards from its resting slots while it slides forward after a
        //shot: 1 the instant a ball fires (each ball drawn one slot back, so the muzzle slot is empty), easing
        //to 0 as the balls glide into place. Keeps the advance from snapping. Wall-clock eased in Update.
        private float _magazineSlide;
        private const float MAGAZINE_SLIDE_TAU = 0.07f; //seconds; ease-out time constant for the glide (~0.2s to settle)

        //Cannon geometry. It stays a tube - a barrel - with a slot along the top: the balls nest inside the
        //bore (a little over the ball radius) and only a strip of each shows through the slot, enough to read
        //its colour. They sit one diameter apart along the aim axis, the front one at the muzzle.
        private const float CANNON_BORE_RADIUS = 0.6f;
        private const float CANNON_WALL_THICKNESS = 0.14f;
        //Radians from straight up. The balls sit entirely inside the bore (centres on the axis, radius 0.5
        //against a bore of 0.6), so the slot is the only thing that shows them and its width is how much of
        //each ball reads - and it has to keep reading from a camera that is not straight above it. At 0.36
        //(a 41-degree slot) anything off the slot's own axis was occluded by the near rim.
        private const float CANNON_SLOT_HALF_ANGLE = 0.5f;
        private const float MAGAZINE_SPACING = 1.0f;        //ball diameter

        //Where the barrel pivots. A real gun hangs from its trunnions - stub pins at the barrel's point of
        //balance, which the carriage holds - and elevates about them, so the muzzle rises as the breech drops
        //and the gun itself stays put. Cannon.Position is that pivot and the muzzle is derived from it;
        //pivoting about the muzzle instead swings the whole barrel and its loaded queue from the tip.
        private const float CANNON_PIVOT_TO_FRONT_BALL = (MAGAZINE_SIZE - 1) * MAGAZINE_SPACING * Constants.HALF;
        private static readonly Vector3 CANNON_COLOR = new(0.42f, 0.44f, 0.48f); //Steel grey (sRGB; the shader linearizes)

        //How far in front of the game camera the cannon stands. This, and not its distance from the field,
        //is what the orbit radius is solved for: the queue in the barrel is really a HUD element, and it has
        //to stay the same size on screen whatever map is loaded, which a fixed distance from the lens gives
        //and a fixed distance from the field does not (the camera itself moves per map).
        private static readonly float CANNON_CAMERA_STANDOFF = 15f;

        //Clearance the cannon keeps outside the field's corner, and the steepest resting aim it is placed for -
        //kept well under Cannon.MaxElevation so the rest pose sits mid-range, with room to elevate onto the high
        //cells (the clamp reaches ~80°; the top corners of a large map need ~50-65° facing them - see aimcheck)
        private static readonly float CANNON_FIELD_CLEARANCE = 2f;
        private static readonly float CANNON_MAX_REST_ELEVATION = 0.70f; //radians, about 40 degrees

        //Precise-aim (ADS) camera. Held with RMB / the gamepad left trigger, it leans the game camera in behind
        //the barrel and looks down the aim, so the whole map reads and the crosshair marks the shot. The lens sits
        //ADS_BACK behind the muzzle along -aim and ADS_RISE above the bore (perpendicular to it), so the barrel
        //stays a small sliver along the bottom; the look target is put ON the shot ray so screen centre is the
        //impact point (see AdsCameraTarget). The dials interact - retuning RISE/BACK/FOV must re-check both that
        //the barrel's top rim clears the centre crosshair and that the over-the-barrel parallax stays small.
        private const float ADS_BACK = 6.0f;              //lens set-back from the muzzle ball along -aim (== pivot - aim*4)
        private const float ADS_RISE = 2.0f;              //lens height above the bore axis; clears the tube (r=0.74) and keeps the sliver low
        //Floor for the ADS lens: aiming steeply up, "behind the muzzle along -aim" is *below* it, so the lens would
        //otherwise drop through the stone island (top at ARENA_Y) and show it from underneath. Held a margin above
        //the island surface - the lens looks up at the cluster from there, so the island stays below the frame.
        private static readonly float ADS_MIN_Y = ARENA_Y + 1.0f;
        private static readonly float ADS_FOV = (float)Math.PI / 5f; //36 degrees, a modest 1.19x zoom over GAME_FOV
        private const float ADS_CONVERGE_MIN = 6f;        //nearest convergence depth (keeps the look target clear of the barrel front)
        private const float ADS_CONVERGE_MAX = 90f;       //farthest (covers the biggest map's cluster, well under FarPlane)
        private const float ADS_BLEND_TAU = 0.08f;        //ease time constant, seconds (~90% in ~0.18s); same idiom as _magazineSlide
        private const float MOUSE_AIM_SENSITIVITY = 2.0f; //aim per pixel after the dt cancellation is 0.001 * this radians (game mode, mouse)
        private const float ADS_TRIGGER_THRESHOLD = 0.5f; //gamepad left-trigger pull that counts as precise aim "held"
        private const float PAD_AIM_RATE = 1.0f;          //right-stick aim rate (the stick is already a rate, so no 1/dt)

        private SpriteBatch _spriteBatch;
        private Texture2D _aimer;
        private Vector2 _aimerPos;
        private Color _aimerColor = new((byte)255, (byte)255, (byte)255, (byte)64);

        #endregion

        #region Contacts

        private ContactEvents _events;
        private BallContactEventHandler _eventHandler;

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

            //Testing: "scene=sea/savanna/desert/mountain/meadow/neon/forest/space" picks the starting
            //environment. Forest and Space are past the end of the NumPad2 cycle (which still runs % 7, so it
            //stays on the seven scenes a map is authored against), so this is the only way to reach them here.
            if (string.Equals(scene, "sea", StringComparison.OrdinalIgnoreCase)) _scene = SceneKind.Sea;
            else if (string.Equals(scene, "savanna", StringComparison.OrdinalIgnoreCase)) _scene = SceneKind.Savanna;
            else if (string.Equals(scene, "desert", StringComparison.OrdinalIgnoreCase)) _scene = SceneKind.Desert;
            else if (string.Equals(scene, "mountain", StringComparison.OrdinalIgnoreCase)) _scene = SceneKind.Mountain;
            else if (string.Equals(scene, "meadow", StringComparison.OrdinalIgnoreCase)) _scene = SceneKind.Meadow;
            else if (string.Equals(scene, "neon", StringComparison.OrdinalIgnoreCase)) _scene = SceneKind.NeonCity;
            else if (string.Equals(scene, "forest", StringComparison.OrdinalIgnoreCase)) _scene = SceneKind.Forest;
            else if (string.Equals(scene, "space", StringComparison.OrdinalIgnoreCase)) _scene = SceneKind.Space;
            else if (string.Equals(scene, "dream", StringComparison.OrdinalIgnoreCase)) _scene = SceneKind.Dream;
            else if (string.Equals(scene, "cavern", StringComparison.OrdinalIgnoreCase)) _scene = SceneKind.Cavern;
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
            ComputeAimerPosition();
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
                _adsMouseInitialized = false; //the first captured frame skips its delta, so grabbing the cursor never jumps the aim
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
                _freeExitFromAds = _adsBlend > 0f;
                if (_freeExitFromAds)
                {
                    _beforeAnimationPosition = _camera.Position;
                    _beforeAnimationTarget = _camera.Target;
                    _beforeAnimationFov = _camera.FieldOfView;
                }
                _adsBlend = 0f;
                _adsMouseInitialized = false;
                IsMouseVisible = true;
                _freeModeAnimStarted = true;
            }
        }

        protected override void LoadContent()
        {
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

            _sceneLightPositionParam = _instancingEffect.Parameters["SceneLightPosition"];
            _sceneLightColorParam = _instancingEffect.Parameters["SceneLightColor"];
            _sceneLightRangeParam = _instancingEffect.Parameters["SceneLightRange"];
            _sceneLightCountParam = _instancingEffect.Parameters["SceneLightCount"];

            CreateShotTrailQuad();

            //The ground counts into the balls' own ambient occlusion too (dark bellies near the ground)
            foreach (InstancedModelRenderer ballRenderer in _ballRenderers) ballRenderer.GroundHeight = GROUND_TOP_Y;

            #region Ceiling and scenery

            RecreateCeilingRenderer(DEFAULT_CEILING_SIZE, DEFAULT_CEILING_SIZE);

            BuildCeiling();

            BuildCity();

            #endregion Ceiling and scenery

            #region Contact events

            _eventHandler = new BallContactEventHandler(_simulation, _events, _ceiling, _physicsBalls, _shotBalls, _fallingBalls);
            _events.Initialize(_simulation);

            #endregion

            _skyModel = Content.Load<Model>("Skyes/SkyDome" + _skyModelNumber);
            //The dome's vertex colors are sRGB; the target it is drawn into is linear
            _sky = new SkyDome(_skyModel, GraphicsDevice, linearVertexColors: true);

            _skyEffect = Content.Load<Effect>("Shaders/Sky");
            _sky.Effect = _skyEffect;
            _skyCameraPositionParam = _skyEffect.Parameters["CameraPosition"];
            SetCloudParameters();

            //The self-lit outdoor backdrops (sea/savanna/desert/mountain/meadow, with the savanna's acacias, the
            //savanna's and desert's birds and the mountain's snow) all live here now, shared with the map editor
            //so a scene looks the same in both
            //SupersampleFactor: the space scene sizes its stars in OUTPUT pixels rather than in texels, so it
            //has to be told what ssaa= settled on — sized in texels a star would be four times dimmer at 2x
            _sceneRenderer = new SceneRenderer(GraphicsDevice, Content) { SupersampleFactor = _supersampleFactor };

            //Cut the island's footprint out of the solid terrain scenes so nothing solid shows through the drain
            //and the funnel below the island reads as a drain into a pit, not a bowl in flat ground (the dark pit
            //cone that backs the glass then fills that hole - see TERRAIN_HOLE_RADIUS / _pitMesh). The map editor
            //leaves this 0 - it draws no island, so nothing is cut and the terrain stays whole under the field's AABB.
            _sceneRenderer.TerrainHoleRadius = TERRAIN_HOLE_RADIUS;

            _cannon = new Cannon(new Vector3(0f, 5f, 0f), -6.4f, 20f);

            //Procedural barrel (the last modeled asset made procedural): a tube with a window cut in the top,
            //running from a muzzle lip just ahead of the front ball to a breech just behind the last one, so
            //all MAGAZINE_SIZE loaded balls sit inside it and show through the window. No UVs, so it uses no
            //detail texture — plain steel whose sheen comes from the specular ambient reflecting the sky.
            //Modelled about the trunnions rather than the muzzle: the front ball sits CANNON_PIVOT_TO_FRONT_BALL
            //ahead of the local origin and the queue recedes behind it, which puts the tube's midpoint at the
            //origin - so the world matrix's translation is the pivot, and aiming turns the barrel about it.
            float muzzleZ = -(CANNON_PIVOT_TO_FRONT_BALL + Constants.HALF);
            float breechZ = (MAGAZINE_SIZE - 1) * MAGAZINE_SPACING - CANNON_PIVOT_TO_FRONT_BALL + Constants.HALF;
            _cannonMesh = new CannonMesh(GraphicsDevice, CANNON_BORE_RADIUS, CANNON_WALL_THICKNESS, muzzleZ, breechZ, CANNON_SLOT_HALF_ANGLE, 24);
            _cannonRenderer = new InstancedModelRenderer(GraphicsDevice, _cannonMesh, CANNON_COLOR, _instancingEffect)
            {
                SpecularAmbientStrength = 0.5f //Metal takes a good deal of the sky as reflection
            };

            //The ground darkens the downward-facing parts of the scene objects too, like the ball bellies
            _cannonRenderer.GroundHeight = GROUND_TOP_Y;

            _aimer = Content.Load<Texture2D>("Bitmaps/Aimer");
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            ComputeAimerPosition();
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

            foreach (InstancedModelRenderer ballRenderer in _ballRenderers) _skyLitRenderers.Add(ballRenderer);

            _skyLitRenderers.Add(_ceilingRenderer);
            _skyLitRenderers.Add(_cannonRenderer);
            if (_cityRenderer != null) _skyLitRenderers.Add(_cityRenderer);
            if (_arenaCapRenderer != null) _skyLitRenderers.Add(_arenaCapRenderer);
            if (_arenaBodyRenderer != null) _skyLitRenderers.Add(_arenaBodyRenderer);
            if (_funnelRenderer != null) _skyLitRenderers.Add(_funnelRenderer);
            if (_funnelRimsRenderer != null) _skyLitRenderers.Add(_funnelRimsRenderer);

            return _skyLitRenderers;
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

            //Space states its own rig, because it draws no dome and a dome-derived one would be a lie — and
            //the particular lie is expensive: reaching for the darkest dome to get a dark sky halves the sun
            //through the key tint above and takes the metallic funnel beads with it. The overcast lerp is
            //bypassed with it, which is right: there is no weather out there to go overcast.
            if (_sceneRenderer != null && _sceneRenderer.TryGetLightRig(_scene, out SceneLightRig rig))
            {
                skyColor = rig.SkyAmbient;
                groundColor = rig.GroundAmbient;
                keyTint = rig.KeyTint;
                backTint = rig.BackTint;
            }

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
            _scene = (SceneKind)(((int)_scene + 1) % 7);
            Console.WriteLine($"[scene] {_scene}");

            //The sea reads best under a moody sky and the savanna under a warm golden one, so entering either
            //defaults its dome (NumPad1 still cycles freely from there). The other scenes keep whatever dome is up.
            if (_scene == SceneKind.Sea) SetSkyDome(SEA_DEFAULT_SKY_DOME);
            else if (_scene == SceneKind.Savanna) SetSkyDome(SAVANNA_DEFAULT_SKY_DOME);
        }

        /// <summary>
        /// Whether the scene is one of the solid-ground backdrops (mountains, meadow, savanna, desert, forest)
        /// whose terrain has the island's footprint cut out and needs the dark pit shaft drawn behind the glass
        /// funnel. The sea (water fills the funnel), the city/neon (their own canyon falls away below) and space
        /// (no ground and a black frame behind the drain) are excluded.
        /// </summary>
        private static bool IsSolidTerrainScene(SceneKind scene) =>
            scene == SceneKind.Mountain || scene == SceneKind.Meadow ||
            scene == SceneKind.Savanna || scene == SceneKind.Desert ||
            scene == SceneKind.Forest;

        /// <summary>
        /// The per-frame inputs the shared <see cref="SceneRenderer"/> needs, taken from this frame's camera,
        /// sun and sky. The sun color is the sun's own radiance tinted by the dome, exactly as the scene
        /// shaders used to read it inline; the clouds are handed over from the one shared field.
        /// </summary>
        private SceneFrame BuildSceneFrame() => new(
            _camera,
            SUN_DIRECTION,
            _zenithLinear,
            _horizonLinear,
            CLOUD_SUN_COLOR * Vector3.Lerp(Vector3.One, _horizonLinear, SKY_TINT_STRENGTH),
            _pulseSeconds,
            _clouds.ApplyTo);

        /// <summary>
        /// Builds this frame's scene point lights for the current scene and sets them on the shared instanced
        /// effect, so the balls, island, cannon and city are lit by the savanna's campfire or the neon city's
        /// neon on top of the sun and the dome — present under every sky dome. The savanna grass shader sets
        /// its own copy (in <see cref="SceneRenderer"/>); other scenes have no extra lights (count 0).
        /// </summary>
        private void ApplySceneLights()
        {
            int count = 0;

            if (_scene == SceneKind.Savanna)
            {
                //The campfire, flickering off the same wall clock as its flame
                _sceneLightPos[0] = _sceneRenderer.SavannaCampfirePosition;
                _sceneLightColor[0] = _sceneRenderer.CampfireColor(_pulseSeconds);
                _sceneLightRange[0] = _sceneRenderer.SavannaCampfireRange;
                count = 1;
            }
            else if (_sceneRenderer.TryGetSpacePlanetshine(_scene, out Vector3 shinePosition, out Vector3 shineColor, out float shineRange))
            {
                //Planetshine: the light the planet throws back on the island's flank. A real light rather than
                //more ambient, so it is directional and so the metallic funnel beads — which have almost
                //nothing but reflections to show — get a highlight back out of it.
                _sceneLightPos[0] = shinePosition;
                _sceneLightColor[0] = shineColor;
                _sceneLightRange[0] = shineRange;
                count = 1;
            }
            else if (_scene == SceneKind.NeonCity)
            {
                //A ring of alternating magenta/cyan lights around the island, colouring the near towers and balls
                var neonLook = _cityConfig.NeonLook;
                int neonCount = Math.Min(neonLook.LightCount, MAX_SCENE_LIGHTS);
                for (int i = 0; i < neonCount; i++)
                {
                    float a = i / (float)neonCount * Microsoft.Xna.Framework.MathHelper.TwoPi;
                    _sceneLightPos[i] = new Vector3(MathF.Cos(a) * neonLook.LightRadius, neonLook.LightHeight, MathF.Sin(a) * neonLook.LightRadius);
                    _sceneLightColor[i] = (i % 2 == 0) ? neonLook.Magenta.ToVector3() : neonLook.Cyan.ToVector3();
                    _sceneLightRange[i] = neonLook.LightRange;
                }
                count = neonCount;
            }

            //A scene with no lights only needs the count pushed the first time it goes to zero — the arrays
            //are untouched above when count is 0, so re-sending them every frame was four parameter writes
            //(and four linear name scans before the references were cached) for values that cannot change
            if (count == 0 && _lastSceneLightCount == 0) return;

            _sceneLightPositionParam.SetValue(_sceneLightPos);
            _sceneLightColorParam.SetValue(_sceneLightColor);
            _sceneLightRangeParam.SetValue(_sceneLightRange);
            _sceneLightCountParam.SetValue(count);

            _lastSceneLightCount = count;
        }

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
            _city = new City(seed: 20260720, arenaHalfExtent: ARENA_DISC_RADIUS, config: _cityConfig);

            Console.WriteLine($"[city] {_city.Buildings.Length} buildings, island radius {ARENA_DISC_RADIUS}, floor at {ARENA_Y}");

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

            //The arena is a small round island: a cast-concrete drum with a dressed stone top and a moulded
            //coping around its rim, the drain funnel bored through the middle (IslandMesh owns the whole
            //cross-section). It replaces the big square marble/glass plaza, whose panels ate the whole lower
            //frame and hid the scene the arena stands in - and, since the plaza, the plain extruded washer
            //that read as a cylinder with a hole in it because every edge of it was a raw 90-degree cut.
            //
            //Both textures are generated rather than loaded, and that is a fix rather than a flourish: the
            //marble photograph this used to project covers only the left half of its canvas and the rest is
            //black, so the triplanar projection multiplied roughly half of the platform by zero at any
            //detail scale - which is what left the wall reading as a dark band. These tile exactly.
            _stoneTexture = SurfaceTexture.Stone(GraphicsDevice);
            _concreteTexture = SurfaceTexture.Concrete(GraphicsDevice);

            _arenaIslandMesh = new IslandMesh(GraphicsDevice, FUNNEL_TOP_RADIUS, ARENA_DISC_RADIUS, ARENA_EDGE_HEIGHT, ARENA_SEGMENTS);

            //The dressed stone: the flat top and the coping that finishes it, coursed into slabs. The detail
            //texture is what selects the technique that reads any of this - without one the renderer falls
            //through to the plain one and every relief setting here is silently dead. DetailBoost normalises
            //the texture to a mean of 1, so it varies the albedo without dimming it and ARENA_STONE_COLOR
            //stays the honest colour of the stone.
            _arenaCapRenderer = new InstancedModelRenderer(GraphicsDevice, _arenaIslandMesh.Cap, ARENA_STONE_COLOR, _instancingEffect)
            {
                SurfaceReliefFrequency = 9f,
                SurfaceReliefStrength = 0.008f,

                //The joint grid is laid out in world X and Z whatever the face, so it also breaks the
                //coping's own ring into blocks - which is how a coping is actually laid.
                SlabSize = 2f,
                SlabJointWidth = 0.025f,

                //Shallower than the old flat disc's joints. The grid is cut on every face, so it also runs
                //down the coping's own ring - which is right, a coping is laid in blocks - but a groove on a
                //near-vertical face turns its walls towards the sky, and at the old depth every joint round
                //the rim came back as a bright wire rather than as a seam.
                SlabJointDepth = 0.025f,
                CavityStrength = 0.7f,

                //(No ReliefShadowStrength or ParallaxScale: the triplanar path builds its own height field
                //and never runs the self-shadow or parallax marches, so both were dead where they used to
                //be set here.)

                DetailTexture = _stoneTexture.Texture,
                DetailTextureMapping = DetailMapping.Triplanar,
                DetailScale = 1f / ARENA_STONE_SPAN,
                DetailBoost = 1f / _stoneTexture.LinearMean,
                DetailStrength = 0.5f,

                //A floor is seen at a grazing angle almost everywhere except right under your feet, which is
                //exactly where Fresnel puts the sky reflection at full strength. Left at 1 the stone mirrors
                //the sky into a white sheet from the middle distance out.
                //
                //This is also the dial that decides how POLISHED the top reads, and not the albedo: the
                //specular ambient is not multiplied by albedo (a reflection does not care how dark the
                //surface under it is), so halving the stone's colour barely dimmed a top face that was
                //washing out - most of its brightness was this term. Dressed stone is matte.
                SpecularAmbientStrength = 0.14f
            };

            //The concrete drum. No slab joints - it is cast, not laid - and a coarser, deeper relief than the
            //dressed stone above it, which is most of what makes the two read as different materials at a
            //distance where neither texture resolves. Barely reflective, because concrete is not.
            _arenaBodyRenderer = new InstancedModelRenderer(GraphicsDevice, _arenaIslandMesh.Body, ARENA_CONCRETE_COLOR, _instancingEffect)
            {
                //The relief is a sum of sines, and past a certain amplitude the sum stops reading as a rough
                //surface and starts reading as the waves it is made of - a regular diagonal weave across the
                //whole drum, which is what a first pass at 0.045 gave. The texture carries the roughness; the
                //relief only has to break the light over it.
                SurfaceReliefFrequency = 4.5f,
                SurfaceReliefStrength = 0.012f,
                SlabSize = 0f,
                CavityStrength = 0.85f,

                DetailTexture = _concreteTexture.Texture,
                DetailTextureMapping = DetailMapping.Triplanar,
                DetailScale = 1f / ARENA_CONCRETE_SPAN,
                DetailBoost = 1f / _concreteTexture.LinearMean,
                DetailStrength = 0.62f,

                //Lower than the stone's, because concrete is rougher and barely reflective. What a vertical
                //face reflects is the horizon rather than the zenith - the brightest, warmest part of the
                //dome, and unmultiplied by albedo - so this term is also what would wash the drum out into
                //one flat sheen and take its material with it.
                SpecularAmbientStrength = 0.08f
            };

            _arenaDiscWorld = Microsoft.Xna.Framework.Matrix.CreateTranslation(0f, ARENA_Y, 0f);

            //The drain funnel in the centre, glass. Its top rim (FUNNEL_TOP_RADIUS) is flush with the stone
            //disc and meets the disc's inner hole directly, so it needs no collar (pass 0); it descends to the
            //hole the balls fall through. Drawn CullNone (see Draw) so it reads both down into it and up through.
            _funnelMesh = new FunnelMesh(GraphicsDevice, FUNNEL_TOP_RADIUS, FUNNEL_HOLE_RADIUS, FUNNEL_TOP_Y - FUNNEL_BOTTOM_Y, FUNNEL_SEGMENTS, 0f);
            _funnelRenderer = new InstancedModelRenderer(GraphicsDevice, _funnelMesh, ARENA_GLASS_COLOR, _instancingEffect, FUNNEL_GLASS_ALPHA);
            _funnelWorld = Microsoft.Xna.Framework.Matrix.CreateTranslation(0f, FUNNEL_TOP_Y, 0f);

            //The gold metal beads around both circles of the funnel, in one mesh (top rim + hole) so one
            //renderer draws them. Opaque, and metallic (see FUNNEL_RIM_* / Metalness), with the gold specular
            //passed as an effect-params override so it reflects the sky in gold rather than the default white.
            _funnelRimsMesh = new FunnelRimsMesh(GraphicsDevice, FUNNEL_TOP_RADIUS, FUNNEL_HOLE_RADIUS,
                FUNNEL_TOP_Y - FUNNEL_BOTTOM_Y, FUNNEL_RIM_TOP_TUBE, FUNNEL_RIM_HOLE_TUBE, FUNNEL_SEGMENTS, FUNNEL_RIM_TUBE_SEGMENTS);
            _funnelRimsRenderer = new InstancedModelRenderer(GraphicsDevice, _funnelRimsMesh, FUNNEL_RIM_COLOR, _instancingEffect)
            {
                Metalness = 1f,
                SpecularAmbientStrength = 1f
            };
            _funnelRimEffectParams = new BasicEffectParams(Vector3.One * SCENE_AMBIENT_INTENSITY, FUNNEL_RIM_SPECULAR, FUNNEL_RIM_SPECULAR_POWER, Vector3.Zero);

            //The dark pit shaft (terrain scenes only) - a near-black cone sharing the glass funnel's mouth and
            //descending just outside it, past the kill plane, so the drain reads as a deep dark well rather than a
            //glass ring over the bright hole cut in the terrain. Reuses FunnelMesh (its wall faces inward-and-up,
            //so it reads looking down into it); drawn opaque and CullNone before the glass, which composites over
            //it. Near-matte so no dome bleaches it; the gold rim bead hides the mouth it shares with the funnel.
            _pitMesh = new FunnelMesh(GraphicsDevice, FUNNEL_TOP_RADIUS, PIT_HOLE_RADIUS, PIT_TOP_Y - PIT_BOTTOM_Y, FUNNEL_SEGMENTS, 0f);
            _pitRenderer = new InstancedModelRenderer(GraphicsDevice, _pitMesh, PIT_COLOR, _instancingEffect)
            {
                SpecularAmbientStrength = 0.03f
            };
            _pitWorld = Microsoft.Xna.Framework.Matrix.CreateTranslation(0f, PIT_TOP_Y, 0f);
        }

        private void BuildCeiling()
        {
            //The wide grid of ground blocks is gone with the big plaza: the round island's only physics floor
            //is the funnel's own mesh - the drain cone plus the flat stone ring around it (BuildFunnelPhysics),
            //out to ARENA_DISC_RADIUS. A ball that falls past the ring drops off the island's edge into the
            //scene and is culled by the kill plane, exactly as one that runs down the funnel is.
            BuildFunnelPhysics();

            Box box = new(DEFAULT_CEILING_SIZE, 1f, DEFAULT_CEILING_SIZE);
            TypedIndex boxShapeIndex = _simulation.Shapes.Add(box);
            _ceilingShapeIndex = boxShapeIndex;
            CollidableDescription collidableDescription = new(boxShapeIndex, 0.1f);
            BodyDescription bodyDescription = BodyDescription.CreateKinematic(new System.Numerics.Vector3(0f, GetCeilingY(10), 0f), collidableDescription, new BodyActivityDescription(Constants.HUNDREDTH));

            BodyHandle topBodyHandle = _simulation.Bodies.Add(in bodyDescription);
            BodyReference topBodyReference = new(topBodyHandle, _simulation.Bodies);

            _ceiling = new KinematicBody(topBodyReference, topBodyHandle);
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
            _ceiling = new KinematicBody(ceilingReference, _ceiling.BodyHandle);
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
                    _city = new City(seed: 20260720, arenaHalfExtent: ARENA_DISC_RADIUS, config: _cityConfig);
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

            _physicsBalls = BallsConstraintsBuilder.BuildBallsStructure(_map.GetStaticBallsArray(), ref _simulation, _ceiling.BodyReference);
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

                            _simulation.Bodies.Remove(ball.BallReference.Handle);
                            _physicsBalls[x, z, level] = null;
                        }
            }

            RemoveDynamicBalls(_shotBalls, unregisterListeners: true);
            RemoveDynamicBalls(_fallingBalls, unregisterListeners: false);
        }

        /// <summary>
        /// Removes the bodies of the given balls from the simulation and clears the list
        /// (the list instance is shared with <see cref="BallContactEventHandler"/>, so it must be cleared, not replaced).
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
        /// Y below which a ball is considered fallen out of the world. Set below the funnel's hole
        /// (<see cref="FUNNEL_BOTTOM_Y"/>) so a ball that drops through it falls a visible distance into the
        /// drop below the platform before it is removed.
        /// </summary>
        private static readonly float KILL_PLANE_Y = -42f;

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

            //Ease the magazine's post-shot slide towards its resting slots (wall-clock too, so it glides even
            //while the simulation is paused). Ease-out: quick off the mark, settling gently into place.
            if (_magazineSlide > 0f)
            {
                _magazineSlide *= MathF.Exp(-(float)gameTime.ElapsedGameTime.TotalSeconds / MAGAZINE_SLIDE_TAU);
                if (_magazineSlide < 0.01f) _magazineSlide = 0f;
            }

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

                #region Shot-trail launch smear

                //Age each muzzle smear and drop it once the launch burst has faded
                for (int i = _shotTrails.Count - 1; i >= 0; i--)
                {
                    _shotTrails[i].Age += (float)gameTime.ElapsedGameTime.TotalSeconds;
                    if (_shotTrails[i].Age >= TRAIL_LIFETIME) _shotTrails.RemoveAt(i);
                }

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
                                Console.WriteLine($"[aimshoot] centre-column sweep complete; holding straight-up, ADS lens Y={AdsCameraPosition().Y:F1} (island top {ARENA_Y:F1})");
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
            }

            //The scene goes through the HDR target; the aimer and the text overlay are drawn after the
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
            GraphicsDevice.Clear(SceneRenderer.ReplacesSky(_scene) ? Color.Black : new Color(_horizonLinear));

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
                //edge both. Either way the only physics floor is the funnel mesh (BuildFunnelPhysics); the
                //round stone island is the platform, and stays in all seven scenes.
                SceneFrame sceneFrame = BuildSceneFrame();

                //Scene point lights (campfire / neon) onto the shared instanced effect, so the balls, island,
                //cannon and city are lit by them under every dome, on top of the sun and sky
                ApplySceneLights();

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

                //The round island, opaque. A closed solid wound clockwise from outside, so it takes the scene's
                //ordinary back-face culling - and drawing it that way is what would show a winding mistake
                //rather than hiding one. Two draws because it is two materials: stone cap, concrete drum.
                _arenaCapRenderer.Draw(_camera, _arenaDiscWorld, _sceneEffectParams);
                _arenaBodyRenderer.Draw(_camera, _arenaDiscWorld, _sceneEffectParams);

                //The dark pit shaft behind the glass funnel, in the solid-terrain scenes only: it fills the hole
                //cut in the terrain so the drain reads as a deep well rather than a glass ring over bright haze.
                //An open cone rather than a closed solid, so it needs the culling off; drawn before the glass
                //funnel, which composites over it.
                GraphicsDevice.RasterizerState = RasterizerState.CullNone;

                if (IsSolidTerrainScene(_scene))
                    _pitRenderer.Draw(_camera, _pitWorld, _sceneEffectParams);

                GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

                _cannonRenderer.Draw(_camera, CannonWorld(), _sceneEffectParams);

                DrawBallsInstanced();

                //The launch smears trailing the shots, over the opaque scene (which the depth buffer now holds,
                //so the cluster/cannon/platform occlude them) and additive, so they glow through the glare
                DrawShotTrails();

                //The funnel's gold metal rims: opaque, so drawn with the opaque scene (before the glass, which
                //then composites over them). A closed convex-tube torus, drawn CullNone (both windings) rather
                //than relying on the winding - the nearest face wins on depth, so the result matches backface
                //culling. Restored to the scene's cull mode after.
                GraphicsDevice.RasterizerState = RasterizerState.CullNone;
                _funnelRimsRenderer.Draw(_camera, _funnelWorld, _funnelRimEffectParams);
                GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

                //The glass drain funnel. Its wall is a single-sided cone, so it is drawn with culling off to
                //show both the inside (looking down into it) and the outside (up through the hole); the state
                //is put back to the scene's cull mode afterwards.
                GraphicsDevice.RasterizerState = RasterizerState.CullNone;
                _funnelRenderer.Draw(_camera, _funnelWorld, _sceneEffectParams);
                GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

                _ceilingRenderer.Draw(_camera, _ceiling.World, _sceneEffectParams);

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

            //The crosshair: in free mode it marks where a shot from the camera goes; in game mode it appears only
            //as precise aim engages, fading in with the blend, and marks the impact point the camera converges on.
            //_aimerPos is kept centred on resize by ComputeAimerPosition. Color * float scales alpha too, so the
            //ADS crosshair grows in with _adsBlend.
            if (!_gameMode)
            {
                _spriteBatch.Begin();
                _spriteBatch.Draw(_aimer, _aimerPos, _aimerColor);
                _spriteBatch.End();
            }
            else if (_adsBlend > 0.01f)
            {
                _spriteBatch.Begin();
                _spriteBatch.Draw(_aimer, _aimerPos, _aimerColor * _adsBlend);
                _spriteBatch.End();
            }

            base.Draw(gameTime);
        }

        /// <summary>
        /// Gathers the instance data of all balls (map structure, shot and falling ones) into the
        /// per-type-and-LOD buckets drawn by the instanced pass. No camera frustum culling:
        /// measurements showed it saved nothing on this scene.
        /// </summary>
        private void CollectBallInstances(float elapsedSeconds)
        {
            for (int i = 0; i < _ballInstanceCounts.Length; i++) _ballInstanceCounts[i] = 0;
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

            //The loaded queue: drawn as real balls (same shader, pattern and emission) in a line along the
            //barrel, the next one at the muzzle. They go through the instanced path like every other ball.
            CollectMagazineBalls();
        }

        /// <summary>
        /// Adds the magazine's queued balls as instances along the cannon axis: index 0 at the muzzle (the
        /// spawn point), the rest receding back towards the breech, so the player sees the colour that will
        /// fire and the ones behind it.
        /// </summary>
        private void CollectMagazineBalls()
        {
            Vector3 direction = CannonAimDirection();
            Vector3 front = CannonMuzzlePosition();

            //The queued balls lie in the bore, so they are carried by the barrel: they take its orientation and
            //turn with it as it elevates and traverses. Drawn unrotated they would keep a fixed world orientation
            //while the barrel tilts around them, and the eye reads that mismatch as each ball skewing in its slot.
            //The very same basis the barrel is drawn with, so the two cannot drift apart.
            Microsoft.Xna.Framework.Matrix orientation = CannonOrientation();

            for (int i = 0; i < MAGAZINE_SIZE; i++)
            {
                //During the slide each ball is drawn (i + slide) slots back, so it eases forward by one slot
                //into the place the fired ball vacated
                Vector3 position = front - direction * ((i + _magazineSlide) * MAGAZINE_SPACING);
                CollectBallInstanceAt(position, orientation, _magazine[i]);
            }
        }

        /// <summary>
        /// Adds a single ball instance at a world position with a given orientation and type, unoccluded.
        /// Used for the magazine balls, which are previews rather than bodies, so they have no pose to read.
        /// </summary>
        private void CollectBallInstanceAt(Vector3 position, Microsoft.Xna.Framework.Matrix orientation, BallType type)
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

            bucket[count] = new ModelInstance(orientation * Microsoft.Xna.Framework.Matrix.CreateTranslation(position), new Vector4(0f, 0f, 0f, 1f));
            _ballInstanceCounts[bucketIndex] = count + 1;
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
        /// Draws each live launch smear: one camera-facing billboard from the muzzle (tail — faint, mostly hidden
        /// behind the barrel) stretched <see cref="TRAIL_LENGTH"/> along the shot to a bright leading end (head)
        /// out in the open, coloured by the ball type and fading over <see cref="TRAIL_LIFETIME"/> (~0.45 s). Additive
        /// and depth-read, like the campfire flame, so it glows and blooms through the glare while the opaque
        /// scene in front still hides it.
        /// </summary>
        private void DrawShotTrails()
        {
            if (_shotTrails.Count == 0) return;

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

            foreach (ShotTrail trail in _shotTrails)
            {
                Vector3 head = trail.Origin + trail.Direction * TRAIL_LENGTH;  //leading end, clear of the barrel: bright
                Vector3 tail = trail.Origin;                                   //muzzle end, mostly hidden by the barrel

                //Hold near-full for most of the life, then drop away at the end (1 - t^2), so the smear stays
                //clearly visible rather than dimming the instant it appears - the point is that it not be missed
                float t = trail.Age / TRAIL_LIFETIME;
                float fade = 1f - t * t;

                _shotTrailEffect.Parameters["TrailHead"].SetValue(head);
                _shotTrailEffect.Parameters["TrailTail"].SetValue(tail);
                _shotTrailEffect.Parameters["TrailColor"].SetValue(trail.Color);
                _shotTrailEffect.Parameters["TrailAlpha"].SetValue(fade);

                _shotTrailEffect.CurrentTechnique.Passes[0].Apply();
                GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
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

            //The translation is written into the rotation matrix rather than multiplied in: R × T is exactly
            //R with its fourth row set to the translation (R's fourth row is (0,0,0,1)), and this runs once
            //per ball per frame — a full 4×4 multiply here was the hottest needless work in the file
            Microsoft.Xna.Framework.Matrix world = Microsoft.Xna.Framework.Matrix.CreateFromQuaternion(
                    new Quaternion(pose.Orientation.X, pose.Orientation.Y, pose.Orientation.Z, pose.Orientation.W));
            world.M41 = renderPosition.X;
            world.M42 = renderPosition.Y;
            world.M43 = renderPosition.Z;

            bucket[count] = new ModelInstance(world, occlusionData);
            _ballInstanceCounts[bucketIndex] = count + 1;
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

        /// <summary>
        /// The shot-trail billboard: a unit quad whose texture channel carries (side in {-1,1}, along in
        /// {0 tail, 1 head}); the shader places it in world space from each trail's head/tail (ShotTrail.fx).
        /// The vertex positions are unused, so one shared quad serves every trail.
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

        /// <summary>
        /// The drain funnel's collision: a triangle-mesh cone (wide rim down to a small hole) placed where the
        /// centre ground block was. Each triangle is wound so its normal faces inward and up - the concave side
        /// the balls rest on - so a ball dropped in rolls down the wall to the hole and falls through (the hole
        /// is wider than a ball, so nothing collides there). Balls that drop through fall past the kill plane
        /// and are removed. Matches the drawn <see cref="FunnelMesh"/>: top rim at FUNNEL_TOP_Y, hole below it.
        /// </summary>
        private void BuildFunnelPhysics()
        {
            int segments = FUNNEL_SEGMENTS;
            float depth = FUNNEL_TOP_Y - FUNNEL_BOTTOM_Y;

            //Two triangles per quad, each added in both windings so the surface blocks a ball from either side:
            //Bepu meshes only collide with a triangle's front (normal) face, and rather than depend on getting
            //the winding right the funnel is made double-sided - a ball can never slip through it. Eight per
            //segment: the sloped cone wall (4) plus the flat stone ring around it (4), from the rim out to
            //ARENA_FLOOR_RADIUS - the round island's floor, that balls rest on until they fall off the edge.
            //That is the edge of the platform's LEVEL top, not of the platform: the coping falls away over
            //the last stretch out to ARENA_DISC_RADIUS, and a floor carried out to there would hold a ball
            //up on air over the wash.
            _bufferPool.Take<Triangle>(segments * 8, out var triangles);

            for (int s = 0; s < segments; s++)
            {
                float a0 = s / (float)segments * MathF.PI * 2f;
                float a1 = (s + 1) / (float)segments * MathF.PI * 2f;

                //Local space: top rim at y = 0 (radius FUNNEL_TOP_RADIUS), hole at y = -depth (radius FUNNEL_HOLE_RADIUS)
                var t0 = Ring(a0, FUNNEL_TOP_RADIUS, 0f);
                var t1 = Ring(a1, FUNNEL_TOP_RADIUS, 0f);
                var h0 = Ring(a0, FUNNEL_HOLE_RADIUS, -depth);
                var h1 = Ring(a1, FUNNEL_HOLE_RADIUS, -depth);
                var q0 = Ring(a0, ARENA_FLOOR_RADIUS, 0f);   //flat stone ring's outer edge, where the coping begins
                var q1 = Ring(a1, ARENA_FLOOR_RADIUS, 0f);

                int b = s * 8;
                //Cone wall (both faces)
                triangles[b] = new Triangle(t0, h0, t1);
                triangles[b + 1] = new Triangle(t1, h0, h1);
                triangles[b + 2] = new Triangle(t0, t1, h0);
                triangles[b + 3] = new Triangle(t1, h1, h0);
                //Flat stone ring, funnel rim -> disc edge (both faces)
                triangles[b + 4] = new Triangle(t0, t1, q1);
                triangles[b + 5] = new Triangle(t0, q1, q0);
                triangles[b + 6] = new Triangle(t0, q1, t1);
                triangles[b + 7] = new Triangle(t0, q0, q1);
            }

            static System.Numerics.Vector3 Ring(float angle, float radius, float y) =>
                new(radius * MathF.Cos(angle), y, radius * MathF.Sin(angle));

            var mesh = new Mesh(triangles, System.Numerics.Vector3.One, _bufferPool);
            var shapeIndex = _simulation.Shapes.Add(mesh);
            _simulation.Statics.Add(new StaticDescription(new System.Numerics.Vector3(0f, FUNNEL_TOP_Y, 0f), shapeIndex));
        }

        protected override void UnloadContent()
        {
            _unitBox?.Dispose();
            _cannonMesh?.Dispose();
            _arenaIslandMesh?.Dispose();
            _stoneTexture?.Dispose();
            _concreteTexture?.Dispose();
            _funnelMesh?.Dispose();
            _funnelRimsMesh?.Dispose();
            _pitMesh?.Dispose();
            _sceneRenderer?.Dispose();
            _pipeline?.Dispose();
            _shotTrailVertexBuffer?.Dispose();
            _shotTrailIndexBuffer?.Dispose();
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

            //Load the magazine up front so the player has a full queue to read from the first frame
            for (int i = 0; i < MAGAZINE_SIZE; i++) _magazine[i] = RandomBallType();
        }

        private static BallType RandomBallType() =>
            (BallType)RANDOM.Next((int)BallType.Type1, (int)BallType.Type8 + 1);

        /// <summary>
        /// Shifts the queue forward and drops a fresh random type in at the back, so it never empties. The
        /// slide is armed at 1 so the balls are drawn one slot back and glide forward into the freed muzzle
        /// slot rather than snapping.
        /// </summary>
        private void AdvanceMagazine()
        {
            for (int i = 0; i < MAGAZINE_SIZE - 1; i++) _magazine[i] = _magazine[i + 1];
            _magazine[MAGAZINE_SIZE - 1] = RandomBallType();

            _magazineSlide = 1f;
        }

        private void ShootBall(Vector3? targetOverride = null)
        {
            //In game mode the shot leaves from the ball the player watched sitting at the head of the queue,
            //not from the pivot in the middle of the barrel, so the drawn ball and the physics one that
            //replaces it are at the same place and the shot reads as that ball leaving the bore
            var sourcePosition = _gameMode ? CannonMuzzlePosition() : _camera.Position;
            var shootTarget = targetOverride ?? (_gameMode ? _cannon.AimTarget : _camera.Target);

            _shotBall.Pose.Position = new System.Numerics.Vector3(sourcePosition.X, sourcePosition.Y, sourcePosition.Z);

            var direction = shootTarget - sourcePosition;
            direction.Normalize();
            Vector3 launchDirection = direction; //unit, before it is scaled to a velocity below
            direction *= SHOOT_MULTIPLIER;

            _shotBall.Velocity.Linear = new System.Numerics.Vector3(direction.X, direction.Y, direction.Z);

            BodyHandle bodyHandle = _simulation.Bodies.Add(_shotBall);

            PhysicsBall ball = new()
            {
                BallReference = new(bodyHandle, _simulation.Bodies),
                Type = _magazine[0] //The colour the player saw loaded at the muzzle - so aiming for it means something
            };

            //Advance the magazine: the fired ball's slot empties, the queue shifts up and a new one loads
            AdvanceMagazine();

            _shotBalls.Add(ball);
            RecountBallsAndConstraints();

            //Give the shot its launch smear: a colour streak at the muzzle, along the shot, fading over
            //TRAIL_LIFETIME (drawn in DrawShotTrails, aged out in Update)
            _shotTrails.Add(new ShotTrail { Origin = sourcePosition, Direction = launchDirection, Age = 0f, Color = TrailColorFor(ball.Type) });

            #region Contact event registration

            //TODO: Unregister when removed from world
            _events.Register(_simulation.Bodies[bodyHandle].CollidableReference, _eventHandler);

            #endregion
        }

        /// <summary>
        /// The launch-smear colour for a ball type: its diffuse tint decoded to linear, its hue kept but lifted
        /// to a floor so even the near-black ball leaves a faint smear, then boosted to a glowing radiance so the
        /// streak reads as energy and blooms through the glare.
        /// </summary>
        private static Vector3 TrailColorFor(BallType type)
        {
            Vector3 linear = ColorSpace.SrgbToLinear(BasicEffectParamsProvider.GetDiffuseTintByType(type));

            float peak = MathF.Max(linear.X, MathF.Max(linear.Y, linear.Z));
            if (peak < TRAIL_COLOR_FLOOR) linear *= TRAIL_COLOR_FLOOR / MathF.Max(peak, 1e-4f);

            return linear * TRAIL_BRIGHTNESS;
        }

        private void UpdateCannon(GameTime gameTime)
        {
            //One snapshot of each input device for the whole game-mode frame: every extra GetState call
            //re-queries the OS (a real XInput poll for the pad), and two reads in one frame can even
            //disagree about a key pressed between them
            KeyboardState keyboard = Keyboard.GetState();
            MouseState mouse = Mouse.GetState();
            GamePadState pad = GamePad.GetState(PlayerIndex.One);

            //Orbiting the cannon around the field is on A/D, and only in game mode - in the free fly camera A/D
            //stay its strafe. W/S are left unused: the gun turns on a carriage, it does not rise or fall. Orbiting
            //does not touch the aim: the mouse owns it (below) and holds it wherever the player leaves it.
            if (_gameMode)
            {
                if (keyboard.IsKeyDown(mgKeys.A)) _cannon.Orbit(1f);
                if (keyboard.IsKeyDown(mgKeys.D)) _cannon.Orbit(-1f);
            }

            _cannon.Update(gameTime);

            //The camera must follow the cannon's pose from THIS frame (after Update above has moved it).
            //Reading the pose before the move made the camera lag one frame behind, so any frame-time
            //fluctuation (shooting, contact processing) showed up as the cannon jittering on screen (#29).
            if (_gameMode && !_gameModeAnimStarted)
            {
                //The mouse aims the cannon throughout game mode - in the overview as well as in precise aim (the
                //arrow keys are retired). The cursor is captured (hidden and re-centred) the whole time we are
                //actively playing, and the mouse delta drives Cannon.Aim before the pose is read so the camera does
                //not lag it (#29). Precise aim (RMB / left trigger) changes nothing about the aiming - it only leans
                //the camera in over the barrel and down the aim (adsHeld drives _adsBlend and the Lerped pose). The
                //order FOV -> Position -> Target is required: the Target setter rebuilds the view last, with world up.
                //IsActive gates the capture: the gamepad trigger reads globally through XInput, and losing focus must
                //free the cursor rather than keep grabbing it (the else branch).
                if (IsActive && _map != null && !_aimShoot) UpdateMouseAim(gameTime, mouse, pad);
                else { _adsMouseInitialized = false; IsMouseVisible = true; }

                bool adsHeld = IsActive && !_freeModeAnimStarted && _map != null && AdsButtonHeld(mouse, pad);
                float adsTarget = adsHeld ? 1f : 0f;
                float adsDt = (float)gameTime.ElapsedGameTime.TotalSeconds;
                _adsBlend = adsTarget + (_adsBlend - adsTarget) * MathF.Exp(-adsDt / ADS_BLEND_TAU);
                if (adsTarget == 0f && _adsBlend < 0.002f) _adsBlend = 0f;
                if (adsTarget == 1f && _adsBlend > 0.998f) _adsBlend = 1f;

                float s = _adsBlend;
                _camera.FieldOfView = Microsoft.Xna.Framework.MathHelper.Lerp(GAME_FOV, ADS_FOV, s);
                _camera.Position = Vector3.Lerp(GetCanonOffsettedPos(), AdsCameraPosition(), s);
                _camera.Target = Vector3.Lerp(GetCannonOffsettedTarget(), AdsCameraTarget(), s);
            }
        }

        /// <summary>
        /// The horizontal direction from the field out towards the cannon - the way the game camera stands
        /// back. Deliberately flattened to the horizontal: taken straight from <c>Position - OrbitCenter</c>
        /// it tilts down by however far the gun stands below the cluster (about 30 degrees here), which ate
        /// most of the camera's height and left it sitting on the barrel's own axis, seeing the gun end-on
        /// with the magazine slot along its top edge-on. Flat, the height below is the only thing that sets
        /// how far the view looks down onto the barrel.
        /// </summary>
        private Vector3 GetCannonDirection()
        {
            Vector3 back = _cannon.Position - _cannon.OrbitCenter;
            back.Y = 0f;

            return back == Vector3.Zero ? Vector3.Backward : Vector3.Normalize(back);
        }

        private Vector3 GetCanonOffsettedPos() =>
            FieldCentreGround() + GetCannonDirection() * _gameCameraDistance
            + Vector3.Up * (_cannon.Position.Y + GAME_CAMERA_HEIGHT);

        /// <summary>The field's centre at ground level: what the game camera stands off from and turns about.</summary>
        private Vector3 FieldCentreGround() => new(_cannon.OrbitCenter.X, 0f, _cannon.OrbitCenter.Z);

        /// <summary>
        /// Places the game camera so the whole play field, the glass ceiling over it and the cannon fit
        /// inside the frustum, and aims it so they sit centred in it. Run on every map load and every resize.
        /// <para>
        /// This has to be **solved** rather than tuned, because both of its inputs move. The field is sized
        /// per map — <c>20x20x20</c> stands 14 units tall against <c>Full.json</c>'s 10 — so a stand-off that
        /// frames one crops the other off the top of the screen, which is exactly what a hand-tuned number
        /// did. And the frustum is sized per display: <c>CreatePerspectiveFieldOfView</c> takes the
        /// **vertical** FOV, so a wider screen only adds width. That is the behaviour wanted — the map keeps
        /// its size on an ultrawide and the extra width goes to scenery — but it also means the horizontal
        /// fit is generous at 21:9 and tightest on the narrowest display, so both axes have to be checked.
        /// </para>
        /// </summary>
        private void FitGameCameraToMap()
        {
            if (_map == null || _camera == null || _cannon == null) return;

            float halfX = (_map.StageSizeX + 1f) * Constants.HALF;
            float halfZ = (_map.StageSizeZ + 1f) * Constants.HALF;
            float topY = GetCeilingY(_map.Levels) + Constants.HALF; //Upper face of the ceiling slab

            float verticalHalf = GAME_FOV * Constants.HALF * GAME_CAMERA_FIT_MARGIN;
            float horizontalHalf = MathF.Atan(MathF.Tan(GAME_FOV * Constants.HALF) * _camera.AspectRatio) * GAME_CAMERA_FIT_MARGIN;

            //Everything fits from far enough away and nothing does from close in, so the smallest distance
            //that fits can be bisected for. The near bound is the camera right behind the cannon.
            float near = CannonOrbitRadius() + 2f;
            float far = 400f;

            for (int i = 0; i < 32; i++)
            {
                float middle = (near + far) * Constants.HALF;
                if (GameCameraFitsAt(middle, halfX, halfZ, topY, verticalHalf, horizontalHalf, out _)) far = middle;
                else near = middle;
            }

            GameCameraFitsAt(far, halfX, halfZ, topY, verticalHalf, horizontalHalf, out float axisElevation);

            _gameCameraDistance = far;
            _gameCameraTargetY = _cannon.Position.Y + GAME_CAMERA_HEIGHT + far * MathF.Tan(axisElevation);
        }

        /// <summary>
        /// Whether the field, its ceiling and the cannon all land inside the frustum with the camera that far
        /// out, and at what elevation the view axis has to sit for it. Elevations are measured off the
        /// horizontal and bisected, which is what centres the subject between the top and bottom edges.
        /// </summary>
        private bool GameCameraFitsAt(float distance, float halfX, float halfZ, float topY,
            float verticalHalf, float horizontalHalf, out float axisElevation)
        {
            Vector3 back = GetCannonDirection();
            Vector3 camera = FieldCentreGround() + back * distance + Vector3.Up * (_cannon.Position.Y + GAME_CAMERA_HEIGHT);
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

                if (depth <= Constants.ONE) { ahead = false; return; }

                float elevation = MathF.Atan2(offset.Y, depth);
                minElevation = MathF.Min(minElevation, elevation);
                maxElevation = MathF.Max(maxElevation, elevation);
                maxSide = MathF.Max(maxSide, MathF.Atan2(MathF.Abs(Vector3.Dot(offset, right)), depth));
            }

            //The field's eight corners, from the floor of the play space to the top of the glass ceiling
            for (int cornerX = -1; cornerX <= 1; cornerX += 2)
                for (int cornerZ = -1; cornerZ <= 1; cornerZ += 2)
                {
                    Consider(new Vector3(cornerX * halfX, 0f, cornerZ * halfZ));
                    Consider(new Vector3(cornerX * halfX, topY, cornerZ * halfZ));
                }

            //The cannon, as a box around its trunnions large enough to hold the barrel at any aim, so the
            //fit does not change as the player elevates or traverses
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

        /// <summary>The cannon's horizontal distance from the centre it orbits — its orbit radius.</summary>
        private float CannonOrbitRadius()
        {
            Vector3 offset = _cannon.Position - _cannon.OrbitCenter;
            offset.Y = 0f;

            return offset.Length();
        }

        /// <summary>
        /// Solves the cannon's orbit radius and the camera's stand-off together, since each depends on the
        /// other — the camera is placed to frame the map <i>and the gun</i>, and the gun is placed a fixed
        /// distance in front of the camera. Alternating converges at once in practice: at a fixed distance
        /// from the lens the gun's angular footprint is the same whatever the radius, so the camera's solve
        /// barely moves after the first round.
        /// </summary>
        private void FitCannonAndGameCameraToMap()
        {
            if (_map == null || _camera == null || _cannon == null) return;

            for (int round = 0; round < 3; round++)
            {
                FitCannonOrbitToMap();
                FitGameCameraToMap();
            }

            Console.WriteLine($"[camera] Field {_map.StageSizeX}x{_map.StageSizeZ}x{_map.Levels}, aspect {_camera.AspectRatio:F2}: " +
                $"camera {_gameCameraDistance:F1} out, aim Y {_gameCameraTargetY:F1}, " +
                $"cannon orbit {_cannon.OrbitRadius:F1} ({_gameCameraDistance - _cannon.OrbitRadius:F1} in front of the camera)");
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

            int total = 0, unreachable = 0;
            float worstElevation = float.MinValue;
            XZLevel worstCell = default;
            float worstY = 0f;

            for (byte level = 0; level < _map.Levels; level++)
                for (byte x = 0; x < _map.StageSizeX; x++)
                    for (byte z = 0; z < _map.StageSizeZ; z++)
                    {
                        Vector3 world = _map.GetRealCenteredPosition(new XZLevel(x, z, level));
                        float cellHorizontal = MathF.Sqrt(world.X * world.X + world.Z * world.Z);

                        //Facing shot: cannon on the same side as the cell, so the horizontal separation is the
                        //smallest (orbitRadius - cellHorizontal) and the elevation the steepest
                        float facingSeparation = MathF.Max(orbitRadius - cellHorizontal, 1f);
                        float facingElevation = MathF.Atan2(world.Y - trunnionsY, facingSeparation);

                        total++;
                        if (facingElevation > Cannon.MaxElevation) unreachable++;
                        if (facingElevation > worstElevation) { worstElevation = facingElevation; worstCell = new XZLevel(x, z, level); worstY = world.Y; }
                    }

            string verdict = unreachable == 0
                ? "PASS - every cell can be shot while facing it"
                : $"FAIL - {unreachable}/{total} cells need more up-elevation than the clamp allows (unfinishable)";

            Console.WriteLine($"[aimcheck] Field {_map.StageSizeX}x{_map.StageSizeZ}x{_map.Levels}: cannon orbit R={orbitRadius:F1}, trunnions Y={trunnionsY:F1}");
            Console.WriteLine($"[aimcheck]   elevation clamp [{Cannon.MinElevation * RAD_TO_DEG:F1}, {Cannon.MaxElevation * RAD_TO_DEG:F1}] deg, traverse +/-{Cannon.MaxTraverse * RAD_TO_DEG:F0} deg");
            Console.WriteLine($"[aimcheck]   steepest cell ({worstCell.X},{worstCell.Z},{worstCell.Level}) at Y={worstY:F1} needs {worstElevation * RAD_TO_DEG:F1} deg facing elevation  ->  {verdict}");
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

            //ADS lens Y against the island top (ARENA_Y): confirms the precise-aim camera stays above the floor
            //even at the steep corner shots, where it used to sink through the stone disc.
            float adsLensY = AdsCameraPosition().Y;

            Console.WriteLine($"[aimshoot] {label} ({cell.X},{cell.Z},{cell.Level}) Y={target.Y:F1}: " +
                $"want {wantedElevation * RAD_TO_DEG:F1} deg, got {gotElevation * RAD_TO_DEG:F1} deg  ->  {(reachable ? "reachable" : "CLAMPED SHORT")}" +
                $"; ADS lens Y={adsLensY:F1} (island top {ARENA_Y:F1})");

            ShootBall();
        }

        /// <summary>
        /// Puts the cannon <see cref="CANNON_CAMERA_STANDOFF"/> in front of the game camera, so it keeps the
        /// same size on screen from map to map — the magazine showing through its slot is what the player
        /// reads the next colour off, and a gun that shrinks because a larger map pushed the camera back
        /// takes the queue with it.
        /// <para>
        /// Two lower bounds override that, and which one binds changes with the map, so this can never be a
        /// single number. The gun must clear the field's <b>footprint</b> at every orbit angle — closer than
        /// the field's corner and it would stand under the cluster it is shooting at. And the closer it
        /// stands the more steeply it looks up, so it stays far enough out that the <b>resting aim</b> is
        /// well within <c>Cannon</c>'s elevation clamp (<see cref="CANNON_MAX_REST_ELEVATION"/>, ~40°, kept
        /// under the ~80° ceiling so there is headroom to elevate onto the high cells); ignore this one and
        /// the gun is silently held below the angle it is trying to sit at.
        /// </para>
        /// </summary>
        private void FitCannonOrbitToMap()
        {
            if (_map == null || _cannon == null) return;

            float halfX = (_map.StageSizeX + 1f) * Constants.HALF;
            float halfZ = (_map.StageSizeZ + 1f) * Constants.HALF;

            float clearFootprint = MathF.Sqrt(halfX * halfX + halfZ * halfZ) + CANNON_FIELD_CLEARANCE;
            float clearElevation = (_cannon.OrbitCenter.Y - _cannon.Position.Y) / MathF.Tan(CANNON_MAX_REST_ELEVATION);

            _cannon.OrbitRadius = MathF.Max(_gameCameraDistance - CANNON_CAMERA_STANDOFF,
                MathF.Max(clearFootprint, clearElevation));
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

        /// <summary>The direction the cannon fires: from the trunnions (its Position) towards its aim target.</summary>
        private Vector3 CannonAimDirection() => Vector3.Normalize(_cannon.AimTarget - _cannon.Position);

        /// <summary>
        /// Where the ball at the head of the magazine sits, and so where a shot spawns: on the barrel axis,
        /// <see cref="CANNON_PIVOT_TO_FRONT_BALL"/> ahead of the trunnions. Unlike the pivot it swings with
        /// the aim - it is the muzzle end of a barrel that turns about its middle.
        /// </summary>
        private Vector3 CannonMuzzlePosition() =>
            _cannon.Position + CannonAimDirection() * CANNON_PIVOT_TO_FRONT_BALL;

        /// <summary>
        /// The lens position for precise aim (ADS): behind the muzzle along the bore and lifted above it, so the
        /// camera looks down the aim with the barrel a small sliver along the bottom of the frame (ADS_BACK/ADS_RISE).
        /// Aiming steeply up the set-back drops the lens below the stone island; its Y is held at <see cref="ADS_MIN_Y"/>
        /// so it never sinks through the floor (the lens looks up from there, so the island stays out of frame).
        /// </summary>
        private Vector3 AdsCameraPosition()
        {
            Vector3 aim = CannonAimDirection();
            Vector3 lens = CannonMuzzlePosition() - aim * ADS_BACK + AdsCamUp() * ADS_RISE;
            lens.Y = MathF.Max(lens.Y, ADS_MIN_Y);
            return lens;
        }

        /// <summary>
        /// Where the ADS camera looks: a point ON the shot ray (muzzle + aim * d), so screen centre marks where the
        /// shot is directed. d is the depth of the field's vertical midpoint projected onto the aim, clamped, which
        /// centres the small over-the-barrel parallax over the region the impact face sweeps during a game.
        /// </summary>
        private Vector3 AdsCameraTarget()
        {
            Vector3 aim = CannonAimDirection();
            Vector3 muzzle = CannonMuzzlePosition();

            float clusterY = _map != null
                ? GetCeilingY(_map.Levels) - (_map.Levels - 1) / Constants.SQRT_TWO * Constants.HALF
                : _cannon.OrbitCenter.Y;
            Vector3 clusterCentre = new(_cannon.OrbitCenter.X, clusterY, _cannon.OrbitCenter.Z);

            float d = Microsoft.Xna.Framework.MathHelper.Clamp(Vector3.Dot(clusterCentre - muzzle, aim), ADS_CONVERGE_MIN, ADS_CONVERGE_MAX);
            return muzzle + aim * d;
        }

        /// <summary>
        /// The "up" for the ADS lens offset: world up made perpendicular to the bore, so the lift is always straight
        /// over the barrel whatever the aim. Well-conditioned across Cannon's elevation clamp: at its ~80° ceiling
        /// |up|² is still ~0.03, far above the fallback threshold, which only trips within ~0.6° of vertical - so
        /// the horizontal-perpendicular fallback stays dead code unless the clamp is pushed almost to straight up.
        /// </summary>
        private Vector3 AdsCamUp()
        {
            Vector3 aim = CannonAimDirection();
            Vector3 up = Vector3.Up - aim * Vector3.Dot(Vector3.Up, aim);
            return up.LengthSquared() < 1e-4f ? Vector3.Normalize(new Vector3(aim.Z, 0f, -aim.X)) : Vector3.Normalize(up);
        }

        /// <summary>Whether precise aim is being held — the right mouse button or the gamepad's left trigger.
        /// Reads the frame's input snapshots from <see cref="UpdateCannon"/> rather than polling again.</summary>
        private static bool AdsButtonHeld(MouseState mouse, GamePadState pad) =>
            mouse.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed
            || pad.Triggers.Left > ADS_TRIGGER_THRESHOLD;

        /// <summary>
        /// Drives the cannon's aim from the mouse throughout game mode (the overview as well as precise aim; the arrow
        /// keys are retired). The cursor is hidden and re-centred every frame; the delta is read against the live
        /// viewport centre (robust to a resize / fullscreen switch) and divided by the frame time, which cancels
        /// exactly against the frame time <see cref="Cannon.Aim"/> multiplies back in, so the aim moves a fixed amount
        /// per pixel at any frame rate. The first captured frame is skipped so acquiring the cursor never jumps the
        /// aim. The gamepad's right stick aims too, fed straight in as a rate.
        /// </summary>
        private void UpdateMouseAim(GameTime gameTime, MouseState mouse, GamePadState pad)
        {
            int cx = GraphicsDevice.Viewport.Width / 2;
            int cy = GraphicsDevice.Viewport.Height / 2;

            IsMouseVisible = false;

            if (_adsMouseInitialized)
            {
                float dtMillis = (float)gameTime.ElapsedGameTime.TotalMilliseconds;
                if (dtMillis > 0f)
                {
                    float invDt = 1f / dtMillis;
                    float pitch = -(mouse.Y - cy) * MOUSE_AIM_SENSITIVITY * invDt; //mouse up -> aim up
                    float yaw = -(mouse.X - cx) * MOUSE_AIM_SENSITIVITY * invDt;    //mouse left -> yaw left
                    if (pitch != 0f || yaw != 0f) _cannon.Aim(new Vector2(pitch, yaw), gameTime);
                }
            }

            Mouse.SetPosition(cx, cy);
            _adsMouseInitialized = true;

            if (pad.IsConnected && pad.ThumbSticks.Right.LengthSquared() > 0f)
                _cannon.Aim(new Vector2(pad.ThumbSticks.Right.Y, -pad.ThumbSticks.Right.X) * PAD_AIM_RATE, gameTime);
        }

        /// <summary>
        /// The barrel's orientation: forward down the aim, with the magazine slot (the mesh's local +Y) pinned
        /// to world up - it stays on top of the barrel and never rolls about the bore. An earlier version rolled
        /// the slot to face the camera so the loaded queue was always readable, but the roll looked wrong in
        /// motion: the gun is to sit on a stand that only elevates and traverses, and a barrel that spins about
        /// its own axis to track the eye reads as unreal. The cost is accepted - from the low game camera the
        /// player sees the barrel's underside and not always the queue (precise aim, whose camera rides over the
        /// barrel, still looks into the slot). CreateWorld orthogonalises world up against the aim, keeping the
        /// slot on the barrel's upper face as it elevates; the bore is clamped well off vertical
        /// (<see cref="Cannon"/>'s elevation clamp) so world up and the aim are never parallel and this never
        /// degenerates. Note it no longer depends on the camera.
        /// </summary>
        private Microsoft.Xna.Framework.Matrix CannonOrientation() =>
            Microsoft.Xna.Framework.Matrix.CreateWorld(Vector3.Zero, CannonAimDirection(), Vector3.Up);

        /// <summary>
        /// The cannon's draw matrix, built from its pose rather than the (now unused) Object3D.World: the
        /// trunnions at Position and the barrel oriented by <see cref="CannonOrientation"/>. The barrel mesh
        /// is modelled about its midpoint with the muzzle towards local -Z, which is where Matrix.CreateWorld
        /// maps the forward direction, and its slot towards local +Y, which maps to the up vector.
        /// </summary>
        private Microsoft.Xna.Framework.Matrix CannonWorld() =>
            CannonOrientation() * Microsoft.Xna.Framework.Matrix.CreateTranslation(_cannon.Position);
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

