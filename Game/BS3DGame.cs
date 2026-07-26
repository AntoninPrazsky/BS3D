using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities.Memory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using Prazsky.Core;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
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
        private const float CAMERA_DISTANCE = 34f;
        private const float CAMERA_HEIGHT = -1.5f;
        private const float CAMERA_TARGET_Y = 3.5f;

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
        private readonly int _supersampleFactor;
        private readonly float _exposure;
        private bool _fullscreen;

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

        //The first level: a stepped square pyramid hanging point-down. The top level is the full
        //CLUSTER_X × CLUSTER_Z base and each level under it is half a unit narrower on every side, so the
        //side count steps 9, 8, 7 … 1 and the flanks come out straight. Half a unit rather than a whole
        //cell, because consecutive levels are offset by +0.5 in X and Z: shrinking by a cell per level puts
        //every second level half a unit off the axis and the flank zig-zags. A map file will replace it.
        private const byte CLUSTER_X = 9;
        private const byte CLUSTER_Z = 9;

        //One level per half-unit of the base's half-extent, plus the apex — exactly CLUSTER_X of them. The
        //base is square on purpose: a rectangular one would run out of width on its narrow axis first and
        //finish as a ridge rather than a point.
        private const byte CLUSTER_LEVELS = CLUSTER_X;

        //Empty field levels below the layout: the room the cluster grows into as shot balls attach under it,
        //which is how a map file's field is taller than the layout hanging at its top.
        //
        //What actually constrains this number is that FIELD_LEVELS must come out EVEN. BallsMap.Center()
        //centres the map on its topmost level and offsets by that level's bounding-box half-extent less a
        //ball radius, which lands on the origin only when the top level is one of the shifted (odd-index)
        //ones and its occupied cells start at index 0. An ODD field tops out on an unshifted level, whose
        //cells run 0…N-1 rather than 0.5…N-0.5, and the same offset then hangs the whole pyramid half a unit
        //off the axis the gun orbits and the camera looks down. (Nothing enforces it, and CLUSTER_LEVELS
        //tracks CLUSTER_X, so re-shaping the level means re-checking this parity by hand.) The layout's own
        //shape is parity-proof either way: BuildCluster measures each level's width against the shifted
        //position of its cells rather than against the raw cell indices.
        private const byte CLUSTER_EXTRA_LEVELS = 7;
        private const byte FIELD_LEVELS = CLUSTER_LEVELS + CLUSTER_EXTRA_LEVELS;

        //Four colours, not the full eight: a first level is meant to be read at a glance, and half the set
        //makes a match something you see rather than something you hunt for. The magazine draws from this
        //same list — loading a colour that is nowhere in the cluster would be a shot that cannot be spent.
        private static readonly BallType[] LEVEL_BALL_TYPES =
            { BallType.Type1, BallType.Type2, BallType.Type3, BallType.Type4 };

        //Fixed, so the first level is the same one every run. It is a level, not a random pile — and until
        //map files arrive this seed is the whole of its authoring.
        private const int LEVEL_SEED = 20260726;

        //A cell's height is its level index over √2 and the layout now sits that many levels up the field, so
        //without this the whole cluster would hang CLUSTER_EXTRA_LEVELS higher than it was framed for. The
        //grid is the truth; this is only where it is drawn, so it is applied at the map/world boundary alone.
        private static readonly float CLUSTER_WORLD_Y = -CLUSTER_EXTRA_LEVELS / Constants.SQRT_TWO;

        //The middle of the field in world Y, which is what precise aim converges its crosshair on. The whole
        //field rather than the layout hanging at its top, because the cluster grows down into the empty
        //levels as balls attach, and the impact face sweeps that range over a game.
        private static readonly float CLUSTER_CENTRE_Y =
            (FIELD_LEVELS - 1) * Constants.HALF / Constants.SQRT_TWO + CLUSTER_WORLD_Y;

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
        private static readonly float CEILING_Y = (FIELD_LEVELS - 1) / Constants.SQRT_TWO + 2f + CLUSTER_WORLD_Y;

        private BoxMesh _ceilingMesh;
        private InstancedModelRenderer _ceilingRenderer;
        private KinematicBody _ceiling;

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

            //A fullscreen switch resizes the back buffer without necessarily going through the window's own
            //resize event, and the overlay's scale is derived from the viewport
            _info?.RecomputeScale();

            IsMouseVisible = false;
            IsFixedTimeStep = false;
        }

        private void OnClientSizeChanged()
        {
            if (_camera != null) _camera.AspectRatio = GraphicsDevice.Viewport.AspectRatio;

            //The overlay is authored for 2160p and scaled to the viewport, so a resize has to re-derive it
            _info?.RecomputeScale();

            EnsureSceneTarget();
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

            BuildScene();

            //The simulation, the ceiling body and the funnel floor first: the cluster's bodies are constrained
            //to the ceiling, so it has to exist before they are built.
            BuildPhysicsWorld();
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

            Console.WriteLine($"[game] {_city.Buildings.Length} buildings, {_map.GetBallsCount()} balls in the cluster, "
                + $"{_simulation.Solver.CountConstraints()} constraints, dome {SKY_DOME}");
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
            Box box = new(CLUSTER_X + 1f, 1f, CLUSTER_Z + 1f);
            TypedIndex shape = _simulation.Shapes.Add(box);

            BodyHandle handle = _simulation.Bodies.Add(BodyDescription.CreateKinematic(
                new System.Numerics.Vector3(0f, CEILING_Y, 0f),
                new CollidableDescription(shape, 0.1f),
                new BodyActivityDescription(Constants.HUNDREDTH)));

            _ceiling = new KinematicBody(new BodyReference(handle, _simulation.Bodies), handle);
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
        /// Fills the hanging cluster: a <see cref="BallsMap"/> carved to a stepped square pyramid, apex down,
        /// and centred on the origin. The layout hangs at the top of a taller field, so the empty levels
        /// underneath are room for shot balls to attach into — the same arrangement a map file carries, which
        /// is what a level will replace this with. The lattice is then mirrored into Bepu bodies, which is what
        /// the frame actually draws.
        /// </summary>
        private void BuildCluster()
        {
            _map = new BallsMap(CLUSTER_X, CLUSTER_Z, FIELD_LEVELS);

            //Its own generator off a fixed seed, so the level is reproducible however many shots the
            //magazine's unseeded one has drawn by the time this runs
            Random layout = new(LEVEL_SEED);

            //The pyramid is built about the centre of the field's topmost level, because that is the level
            //BallsMap.Center() puts on the origin. Odd levels are shifted by +0.5 in X and Z, so which
            //centre that is depends on its parity (FIELD_LEVELS is chosen to make it a shifted one).
            float topShift = LevelShift((byte)(FIELD_LEVELS - 1));
            float axisX = (CLUSTER_X - 1) * Constants.HALF + topShift;
            float axisZ = (CLUSTER_Z - 1) * Constants.HALF + topShift;

            for (byte level = 0; level < CLUSTER_LEVELS; level++)
            {
                byte fieldLevel = (byte)(level + CLUSTER_EXTRA_LEVELS);

                //Half the pyramid's width here: nothing but the apex cell at the bottom, growing half a unit
                //per level up to the full base at the top. Both this and the cell positions are whole
                //multiples of a half, hence exact in binary — which is why the test below needs no tolerance.
                float half = level * Constants.HALF;
                float shift = LevelShift(fieldLevel);

                for (byte x = 0; x < CLUSTER_X; x++)
                    for (byte z = 0; z < CLUSTER_Z; z++)
                    {
                        //Measured against where the cell actually sits, not its raw index, so a level's
                        //own half-unit offset cannot throw the flank out of line
                        if (MathF.Abs(x + shift - axisX) > half) continue;
                        if (MathF.Abs(z + shift - axisZ) > half) continue;

                        _map.PutBallAt(x, z, fieldLevel, RandomLevelBallType(layout));
                    }
            }

            _map.Center();

            //The lattice mirrored into bodies, one per occupied cell, constrained to its neighbours and — on
            //the top level — to the ceiling. The offset is what creates them where the cluster is drawn: a
            //BallsMap reckons in its own grid frame and this game draws that frame CLUSTER_WORLD_Y lower, so
            //the empty levels below the layout do not raise it. The bodies have to be in world coordinates
            //because everything else the simulation touches is — the floor, the ceiling, the muzzle a shot
            //leaves from, the kill plane. It is applied to the body positions and to nothing else: the
            //constraint anchors are differences of two grid positions, so the offset cancels out of them.
            _physicsBalls = BallsConstraintsBuilder.BuildBallsStructure(
                _map.GetStaticBallsArray(), ref _simulation, _ceiling.BodyReference,
                new System.Numerics.Vector3(0f, CLUSTER_WORLD_Y, 0f));

            //What happens on a hit lives in the handler: the snap into the lattice, the constraints, and the
            //match rule. It gets the very list instances the frame draws from, and the same offset, so it can
            //take a world contact down into the grid frame to ask the map about it and bring the answer back up.
            _eventHandler = new BallContactEventHandler(_simulation, _events, _ceiling, _map, _physicsBalls,
                _shotBalls, _fallingBalls, new Vector3(0f, CLUSTER_WORLD_Y, 0f));

            //Odd levels are shifted by +0.5 and a ball's radius is another 0.5, so a field's worth of balls is
            //one unit wider than its cell count; the plate covers it with that margin, as the Testbed's does.
            //It is drawn from the kinematic body's own pose, so the glass and the collidable cannot disagree.
            _ceilingMesh = new BoxMesh(GraphicsDevice, CLUSTER_X + 1f, 1f, CLUSTER_Z + 1f);
            _ceilingRenderer = new InstancedModelRenderer(GraphicsDevice, _ceilingMesh, CEILING_GLASS_COLOR, _instancingEffect, CEILING_GLASS_ALPHA);
        }

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
        /// Every renderer that takes its lighting from the sky dome.
        /// </summary>
        private IEnumerable<InstancedModelRenderer> SkyLitRenderers()
        {
            foreach (InstancedModelRenderer ballRenderer in _ballRenderers) yield return ballRenderer;

            yield return _cannonRenderer;
            yield return _cityRenderer;
            yield return _islandRenderer;
            yield return _funnelRenderer;
            yield return _funnelRimsRenderer;
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

            //The barrel slides home. Linear in the stroke, so it genuinely ends rather than approaching zero
            //forever and leaving the gun permanently a hair out of place.
            if (_cannonRecoil > 0f) _cannonRecoil = MathF.Max(0f, _cannonRecoil - CANNON_RECOIL_DECAY * elapsed);

            StepPhysics(elapsed);
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

                //F12 hides the FPS overlay, the same key that hides the Testbed's text
                if (keyboard.IsKeyDown(Keys.F12) && !_previousKeyboard.IsKeyDown(Keys.F12)) _info.Visible = !_info.Visible;

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
            for (int i = 0; i < MAGAZINE_SIZE - 1; i++) _magazine[i] = _magazine[i + 1];
            _magazine[MAGAZINE_SIZE - 1] = RandomBallType();

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

                if (unregisterListeners && _events.IsListener(body.CollidableReference)) _events.Unregister(body.CollidableReference);

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

            Vector3 back = _cannon.Position - _cannon.OrbitCenter;
            back.Y = 0f;
            Vector3 bearing = back == Vector3.Zero ? Vector3.Backward : Vector3.Normalize(back);

            Vector3 fieldCentre = new(_cannon.OrbitCenter.X, 0f, _cannon.OrbitCenter.Z);

            Vector3 overviewPosition = fieldCentre + bearing * CAMERA_DISTANCE + Vector3.Up * (_cannon.Position.Y + CAMERA_HEIGHT);
            Vector3 overviewTarget = new(_cannon.OrbitCenter.X, CAMERA_TARGET_Y, _cannon.OrbitCenter.Z);

            _camera.BasePosition = Vector3.Lerp(overviewPosition, AdsCameraPosition(), _adsBlend);
            _camera.BaseTarget = Vector3.Lerp(overviewTarget, AdsCameraTarget(), _adsBlend);
            _camera.FieldOfView = MathHelper.Lerp(GAME_FOV, ADS_FOV, _adsBlend);

            _camera.Update(elapsed);
        }

        protected override void Draw(GameTime gameTime)
        {
            //The occlusion ease and the attach glide are advanced on the draw clock, since both are purely
            //about what is on screen
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

            //The drain's gold beads are opaque, so they belong to the opaque scene and go down before the
            //glass the funnel composites over them. A closed convex tube, so CullNone is safe (the nearest
            //face wins on depth and the winding is moot) and is one less thing to get wrong unseen.
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            _funnelRimsRenderer.Draw(_camera, _funnelWorld, _funnelRimEffectParams);

            //The glass funnel itself: one open, single-sided cone, so culling stays off to show both the
            //inside looking down into it and the outside looking up through the hole.
            _funnelRenderer.Draw(_camera, _funnelWorld, _sceneEffectParams);
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            //The glass the cluster hangs from, last: it is translucent, so everything it should be seen
            //through has to be in the depth buffer and the frame already
            _ceilingRenderer.Draw(_camera, _ceiling.World, _sceneEffectParams);

            ResolveSceneTarget();

            //Display space from here down: the resolve is the frame's one and only exit from linear light,
            //and both the crosshair and the FPS overlay (a component, so base.Draw puts it out) are sRGB.
            DrawCrosshair();

            base.Draw(gameTime);
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

            Vector3 clusterCentre = new(_cannon.OrbitCenter.X, CLUSTER_CENTRE_Y, _cannon.OrbitCenter.Z);
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

        /// <summary>One of the level's colours. Only those: see <see cref="LEVEL_BALL_TYPES"/>.</summary>
        private static BallType RandomLevelBallType(Random random) => LEVEL_BALL_TYPES[random.Next(LEVEL_BALL_TYPES.Length)];

        /// <summary>What the magazine loads next — the level's colours, off the unseeded run-to-run generator.</summary>
        private static BallType RandomBallType() => RandomLevelBallType(RANDOM);

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
            _ceilingMesh?.Dispose();
            _ceilingRenderer?.Dispose();

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
