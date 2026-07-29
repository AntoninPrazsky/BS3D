using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities.Memory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Physics;
using Prazsky.BS3D.Scoring;
using Prazsky.Core.Render;
using Prazsky.Core.Screens;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using static Prazsky.BS3D.Physics.Simu;

//BepuUtilities is deliberately NOT imported: it carries its own Matrix and MathHelper, which would make every
//existing use of the XNA ones in this file ambiguous. The one type needed from it is qualified where it is
//declared. Bepu's own vectors are System.Numerics and are likewise spelled out at each crossing, the
//convention the Testbed uses (see CLAUDE.md, "Conventions").

namespace BS3D.Screens
{
    /// <summary>
    /// A level being played — the <b>session</b>, on the same stack the menus live on (#65). Everything that
    /// exists only between "Play" and the level's end is here: the Bepu simulation and its contact handling,
    /// the cluster, the gun and its magazine, the shot and its trails, the game camera and precise aim, the
    /// HUD and the scoring wiring, and the level install/clear/lose logic.
    /// <para>
    /// <see cref="BS3DGame"/> is the host it stands in: the device, the content, the shared setting (sky,
    /// scene, city, island, drain) and the post-processing pipeline all outlive a session and stay there.
    /// <see cref="Draw"/> runs the frame's sequence itself, asking the host for the pieces — which is what
    /// lets a pause be a screen pushed <i>over</i> this one: the manager goes on drawing this screen
    /// underneath it (<see cref="Screen.DrawsUnderlying"/>) while no longer updating it, and the frozen frame
    /// the player pauses on is simply this screen, still drawn.
    /// </para>
    /// <para>
    /// One instance is held for the life of the game, like the menu pages; <see cref="BuildLevel"/> and
    /// <see cref="TearDown"/> are what a session's lifetime actually is. "Is there a game to continue" is
    /// <see cref="IsBuilt"/>, not whether this screen happens to be on the stack — the main menu keeps the
    /// session alive while this screen is off the stack entirely.
    /// </para>
    /// </summary>
    internal sealed class GameplayScreen : Screen
    {
        private readonly BS3DGame Game;

        //Forwarders for what the frame borrows from the host every few lines, so the session's own code reads
        //undisturbed: the one camera (the menus orbit it, this screen poses it), the wall clock everything
        //alive runs off, and the device.
        private RecoilCamera Camera => Game.Camera;
        private float WallClock => Game.WallClock;
        private GraphicsDevice GraphicsDevice => Game.GraphicsDevice;
        private int Scaled(int designUnits) => Game.Scaled(designUnits);

        /// <summary>
        /// True once the physics world, the ceiling body and the cluster have been built. They are built on
        /// the first "Play" rather than at load — the simulation is the expensive part of starting up, and
        /// there is no point paying for it before the player has chosen to play. It is also what tells the
        /// main menu whether there is a session to go back to.
        /// </summary>
        internal bool IsBuilt { get; private set; }

        /// <summary>Which entry of the level set the current session is playing.</summary>
        internal int LevelIndex => _levelIndex;

        #region The game camera

        //Narrow, like the Testbed's game camera and for the same reason: from down behind the gun the
        //barrel and the cluster are close together in the frame, so it can close in on the cluster.
        internal static readonly float GAME_FOV = MathF.PI / 4.2f;

        //Where the lens stands relative to the gun: back from the field centre along the gun's own bearing,
        //and just below the trunnions, so the player looks up at the hanging cluster past the barrel.
        private const float CAMERA_HEIGHT = -1.5f;

        //How far back the camera stands and how high it aims. Both are SOLVED per level and per display
        //(FitGameCameraToLevel) rather than tuned, because both of their inputs move underneath a fixed
        //number: the field is sized per level, and the frustum per display. These defaults only cover the
        //frames before the first level is installed.
        private float _gameCameraDistance = 34f;
        private float _gameCameraTargetY = 3.5f;

        //How much of the frustum the fit is allowed to fill. Under 1 so the field does not sit hard against
        //the frame's edges, which reads as cropped even when nothing is.
        private const float GAME_CAMERA_FIT_MARGIN = 0.92f;

        //Where the gun stands relative to the camera, and the two lower bounds that override it. The gun is
        //placed off the LENS and not off the field, because the magazine showing through its slot is really a
        //HUD element and has to keep its size on screen — anchoring it to the field lets a large level push
        //the camera back and shrink the queue with it. The bounds: it must clear the field's own footprint at
        //every orbit angle (closer, and it stands under the cluster it shoots at), and it must stay far
        //enough out that its RESTING aim is well inside Cannon's elevation clamp, with headroom to elevate
        //onto the high cells. Which bound binds changes with the level, so this can never be one number.
        private const float CANNON_CAMERA_STANDOFF = 15f;
        private const float CANNON_FIELD_CLEARANCE = 2f;
        private const float CANNON_MAX_REST_ELEVATION = 0.70f;  //radians, ~40°, against a clamp that reaches ~80°

        #endregion

        #region Precise aim (ADS)

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
        private const float ADS_MIN_Y = BS3DGame.ISLAND_Y + 1f;

        private float _adsBlend;
        private bool _adsHeld;

        #endregion

        #region The in-play HUD (display space, after the resolve)

        /// <summary>
        /// Score, streak, balls left and the awards flying into the corner. Its own class — see
        /// <see cref="PlayHud"/> for why, and for the whole of how it looks and moves; this screen only feeds it
        /// the events it animates and gives it the frame to draw into.
        /// </summary>
        private readonly PlayHud _hud;

        //The crosshair. No bitmap: four bars struck from the host's 1×1 white texture. It appears only as
        //precise aim leans in, because only then does the lens look along the shot — in the overview a
        //screen-centre mark would point at nothing in particular.
        //
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
        /// or fallen off the island's edge into the city. Well under <see cref="BS3DGame.FUNNEL_BOTTOM_Y"/>, so
        /// a ball that goes down the hole falls a visible distance before it is culled rather than winking out
        /// in the mouth of the drain.
        /// </summary>
        private static readonly float KILL_PLANE_Y = -42f;

        #endregion

        #region The cluster

        //The fallback level, used only when no level file can be read (see BS3DGame.LoadLevelSet): a stepped
        //square pyramid hanging point-down. The top level is the full FALLBACK_X × FALLBACK_Z base and each
        //level under it is half a unit narrower on every side, so the side count steps 9, 8, 7 … 1 and the
        //flanks come out straight. Half a unit rather than a whole cell, because consecutive levels are offset
        //by +0.5 in X and Z: shrinking by a cell per level puts every second level half a unit off the axis
        //and the flank zig-zags. It is the shape the game shipped with before it read levels off disk, kept so
        //a missing or broken Levels directory still gives something to play rather than an empty field.
        private const byte FALLBACK_X = 9;
        private const byte FALLBACK_Z = 9;

        //One level per half-unit of the base's half-extent, plus the apex — exactly FALLBACK_X of them. The
        //base is square on purpose: a rectangular one would run out of width on its narrow axis first and
        //finish as a ridge rather than a point.
        private const byte FALLBACK_LEVELS = FALLBACK_X;

        //Empty field levels below the layout: the room the cluster grows into as shot balls attach under it,
        //which is how a map file's field is taller than the layout hanging at its top.
        private const byte FALLBACK_EXTRA_LEVELS = 7;
        private const byte FALLBACK_FIELD_LEVELS = FALLBACK_LEVELS + FALLBACK_EXTRA_LEVELS;

        //Fixed, so the fallback is the same pile every run
        private const int FALLBACK_SEED = 20260726;

        //What the magazine loads when a map carries no balls at all. A real level's colours are read off the
        //map itself — loading a colour that is nowhere in the cluster is a shot that cannot be spent, so the
        //queue can only be built from what is actually up there.
        private static readonly BallType[] DEFAULT_BALL_TYPES =
            { BallType.Type1, BallType.Type2, BallType.Type3, BallType.Type4 };

        /// <summary>
        /// How many balls of each type are still hanging, indexed by <c>(int)BallType - 1</c>. Recounted off
        /// the map every time the cluster changes, which is the only thing that can change it: the magazine
        /// may only ever load a colour whose count is above zero.
        /// <para>
        /// Recounted rather than maintained incrementally, deliberately. The walk is the field's cell count —
        /// 1500 for <c>One.json</c> — and it runs when a shot lands, not per frame; carrying a running total
        /// through the release path, the attach path and the second-ring fallback would be three places to get
        /// wrong in exchange for microseconds nobody can measure.
        /// </para>
        /// </summary>
        private readonly int[] _ballsOfType = new int[BS3DGame.BALL_TYPE_COUNT];

        /// <summary>
        /// Where the lattice frame meets the world, and the <b>only</b> place it does on the drawing side.
        /// <para>
        /// Y puts the top of the field at a fixed world height whatever the field's depth: a cell's height is
        /// its level index over √2, so without this a map with more empty levels below its layout would hang
        /// that much higher than the camera frames.
        /// </para>
        /// <para>
        /// X and Z correct the residual half-unit <see cref="BallsMap.Center"/> can leave behind. It offsets
        /// by the top level's bounding-box <i>half-extent</i> less a ball radius, which lands on the origin
        /// only when that level is one of the shifted (odd-index) ones and its cells start at index 0 — an
        /// <b>odd</b> field tops out on an unshifted level, whose cells run 0…N-1 rather than 0.5…N-0.5, and
        /// the whole cluster then hangs half a unit off the axis the gun orbits and the camera looks down.
        /// That used to be a rule the hard-coded field had to satisfy by hand; a level file is authored
        /// elsewhere and cannot be held to it (One.json is fifteen levels deep), so the residual is measured
        /// off the centred top level and folded in here instead. Both halves ride the one vector the physics
        /// builder and the contact handler already take, so no new frame crossing is introduced.
        /// </summary>
        private Vector3 _clusterWorldOffset;

        /// <summary>
        /// The height the field's topmost level hangs at, which is what everything else is measured from.
        /// It is where the previous hard-coded field put its top ((16-1)/√2 − 7/√2), kept exactly so the
        /// camera, the gun and the ceiling frame a loaded level the way they framed that one.
        /// </summary>
        private static readonly float FIELD_TOP_Y = (FALLBACK_FIELD_LEVELS - 1 - FALLBACK_EXTRA_LEVELS) / Constants.SQRT_TWO;

        //The middle of the field in world Y, which is what precise aim converges its crosshair on. The whole
        //field rather than the layout hanging at its top, because the cluster grows down into the empty
        //levels as balls attach, and the impact face sweeps that range over a game.
        private float _clusterCentreY;

        //The lattice is the truth about what is where and what is free for a shot to land in. The parallel
        //array of PhysicsBalls is that same structure as Bepu bodies — one per occupied cell, each held to its
        //neighbours and, on the top level, to the ceiling by BallSocket constraints. The bodies are what the
        //frame draws, so the player is looking at the simulation rather than at a copy of it: the cluster
        //sways, a shot shoves it, and a matched group falls.
        private BallsMap _map;
        private PhysicsBall[,,] _physicsBalls;

        //Two above the centres of the top level's balls, the Testbed's own figure. The kinematic body and the
        //drawn glass box both sit here — the box is drawn straight from the body's pose (see KinematicBody),
        //so the collidable and the thing the player sees cannot drift apart.
        //
        //Note the cluster does not settle on the lattice: the ceiling BallSocket anchors a ball's top (local
        //+0.5) to the plate's bottom face (local -0.5), so the top level comes to rest one unit under the body
        //and the whole rigid structure with it. Half the clearance this constant looks like it buys is spent
        //that way, and the top balls end up close under the glass. It is the Testbed's behaviour, kept for
        //parity; the figure to change if the cluster should hang exactly on its lattice is this one.
        private const float CEILING_CLEARANCE = 2f;
        private float _ceilingY;

        private KinematicBody _ceiling;

        //The descending ceiling — the second of the two pressures that can lose a level, made visible where the
        //shot budget is made numerical. Every ceilingStep shots the glass steps down by CEILING_DESCENT_PER_STEP,
        //and with it the cluster (the top level is held to the body by BallSocket constraints, so moving the body
        //drags the structure along — Bepu does the work). The level is lost the moment any ball crosses the death
        //line, which is above the gun and the drain so a cluster reaching into them reads as a loss before it
        //reads as a bug.
        //
        //The descent is animated at constant velocity per step rather than teleported: a hundred constrained
        //bodies jerked in one write can throw the solver, and a short slide lets the contact between a descending
        //cluster and anything below it resolve. The body is kinematic and this build's integrator does not move
        //kinematics from their velocity (PoseIntegratorCallbacks.IntegrateVelocityForKinematics is false), so the
        //slide is driven by writing the pose — in small steps, which is what makes it tolerable to the solver.
        private const float CEILING_DESCENT_PER_STEP = 0.6f;        //world units the glass drops each step
        private const float CEILING_DESCENT_SPEED = 1.5f;           //units/sec while a step is sliding in
        private const float CEILING_DEATH_Y = -5.5f;                //a ball below this has lost the level

        /// <summary>
        /// How hard the glass is glowing right now, 1 at the moment of a step and decaying to nothing. It is
        /// the descent's announcement: a translucent plate sliding down against the sky is close to invisible
        /// while the player's eye is on the cluster, so the pressure the rule exists to apply was arriving
        /// without being noticed at all.
        /// </summary>
        private float _ceilingFlash;

        //Linear, so it genuinely ends — the CameraShake rule. Long enough to be seen even if the eye is on the
        //other half of the frame when it fires, short enough not to sit there as decoration.
        private const float CEILING_FLASH_SECONDS = 1.1f;

        //Linear radiance, well over GLARE_THRESHOLD so the plate blooms rather than merely turning pink. Red
        //with almost nothing in the other two channels: this is the one alarm in the game and it should not be
        //mistakable for anything the scene does on its own.
        //Far over 1, and it has to be: the plate is 35 % opaque, so most of what is seen where the glass is is
        //the sky BEHIND it. At 1.5 the red merely tinted that blue-white and the plate came out pink. The
        //emissive is added on top of the composite, so it is the number that has to out-shout the sky.
        private static readonly Vector3 CEILING_FLASH_COLOR = new(6f, 0.15f, 0.1f);

        //Where the glass body sits now (_ceilingY) and where it is sliding to (_ceilingTargetY). Equal while at
        //rest; _ceilingTargetY is lowered by StartCeilingDescent and _ceilingY catches up in UpdateCeilingDescent.
        private float _ceilingTargetY;
        private bool _ceilingDescending;

        #endregion

        #region Levels

        /// <summary>Which entry of the host's level set the current session is playing.</summary>
        private int _levelIndex;

        /// <summary>
        /// Seconds left of the pause between the field emptying and the level actually ending, counted down
        /// only while it is above zero — so zero doubles as "this level is still being played".
        /// <para>
        /// The pause is the point. Balls leave the map at <b>release</b> time, while their bodies are still
        /// falling: ending the level the instant the last group is cut would take the collapse the player just
        /// earned off the screen before they saw it.
        /// </para>
        /// </summary>
        private float _clearedCountdown;

        /// <summary>
        /// Set the moment a level is lost, and only once — a descent and a spent budget can both reach their line
        /// on the same frame, and the loss must not fire twice. Cleared back to false by <see cref="BuildLevel"/>,
        /// which is the real reload that starts a level over.
        /// </summary>
        private bool _levelLost;

        /// <summary>How long that pause is — long enough for a big collapse to reach the drain and go down it.</summary>
        private const float LEVEL_CLEARED_BEAT = 2.5f;

        //How long the victory display goes on launching. It deliberately outlasts LEVEL_CLEARED_BEAT by a long
        //way: the beat only holds the collapse on screen before the result page arrives, and the fireworks are
        //meant to still be going behind the score the player is reading.
        private const float CELEBRATION_SECONDS = 9f;

        /// <summary>
        /// The level's score and ball budget. Built fresh for each level from that entry's rules, so it never
        /// carries anything across; it holds the rules themselves and this class only feeds it the three events
        /// a shot goes through.
        /// </summary>
        private ScoreKeeper _score = new();

        /// <summary>
        /// What ended the level, set when it ends and read by the result screen. <c>None</c> means the level is
        /// still being played; <see cref="FinishLevel"/> is what leaves <c>None</c>.
        /// </summary>
        private enum LevelOutcome { None, Cleared, Failed }

        /// <summary>
        /// Which of the two limits ended the level. An enum rather than a message carried through from where
        /// the loss was detected: the wording is a <b>display</b> concern and belongs on the screen that shows
        /// it, and a string built at the point of detection ends up carrying the numbers that were convenient
        /// there — which is how a player came to be shown "a ball at -5,58 &lt;= -5,50". Those figures are
        /// diagnostics; they belong in the log, and they still go there.
        /// </summary>
        private enum LevelFailure { None, OutOfBalls, ClusterReachedLine, ShortOfGate }

        private LevelFailure _pendingFailure;

        private LevelOutcome _pendingOutcome = LevelOutcome.None;

        #endregion

        #region The gun and the shot

        private readonly Cannon _cannon;

        internal const int MAGAZINE_SIZE = 5;
        internal const float MAGAZINE_SPACING = 1.0f;
        private const float MAGAZINE_SLIDE_TAU = 0.07f;
        internal const float CANNON_PIVOT_TO_FRONT_BALL = (MAGAZINE_SIZE - 1) * MAGAZINE_SPACING * Constants.HALF;

        private readonly BallType[] _magazine = new BallType[MAGAZINE_SIZE];
        private float _magazineSlide;

        /// <summary>
        /// The colour a loaded ball is dissolving <i>out of</i>, per slot, and how far through that it is
        /// (0 = settled, nothing to draw twice). A ball whose colour has just been eliminated from the cluster
        /// is re-coloured where it sits rather than left to be fired at nothing — see <see cref="Transmute"/>.
        /// </summary>
        private readonly BallType[] _magazineFrom = new BallType[MAGAZINE_SIZE];
        private readonly float[] _magazineTransmute = new float[MAGAZINE_SIZE];

        /// <summary>
        /// How long a loaded ball takes to change colour. Slow enough to be unmistakably seen — the whole
        /// point is that the player watches the game help them, and a snap would read as a bug — and short
        /// enough not to hold up a queue the player is aiming with.
        /// </summary>
        private const float TRANSMUTE_SECONDS = 0.75f;

        //Several ball diameters a frame: the shot is a streak, not something the eye can follow. That is
        //the intended feel, and it is why the launch smear below exists at all.
        private const float SHOOT_SPEED = 200f;

        //How hard a shot hits the camera. Still a full kick: it is the *ceiling* that was lowered
        //(BS3DGame.CAMERA_SHAKE_SCALE), not the strength of one shot. Kicking at a fraction instead would let
        //two shots in quick succession accumulate straight back up to the response that was too strong.
        private const float RECOIL_KICK = 1f;

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

        //The reward shot for a big collapse: the camera lets go of the gun and follows the wreckage down the
        //drain. It owns the pose, the time scale and the blend; this screen owns the trigger, the subject and
        //the fact that the gun does not answer while it is engaged.
        private readonly DropCinematic _cinematic = new();

        //Which released balls this cinematic is following, by body handle — see TryBeginDropCinematic for why
        //handles and not list indices, and why recycling cannot bite here.
        private readonly HashSet<int> _cinematicSubject = new();

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
        private bool _mouseAimInitialized;
        private bool _padTriggerReleased = true;

        #endregion

        #region Ball instances

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

        private readonly ModelInstance[][] _ballInstances = new ModelInstance[BS3DGame.BALL_TYPE_COUNT * BS3DGame.BALL_LOD_COUNT][];
        private readonly int[] _ballInstanceCounts = new int[BS3DGame.BALL_TYPE_COUNT * BS3DGame.BALL_LOD_COUNT];

        #endregion

        public GameplayScreen(BS3DGame game)
        {
            Game = game;
            _hud = new PlayHud(game);

            //Orbit centre is the field the cluster hangs over; the trunnions sit an axle's height above the
            //island, and the gun stands well inside the island's rim.
            _cannon = new Cannon(new Vector3(0f, 5f, 0f), -6.4f, 20f);

            for (int i = 0; i < MAGAZINE_SIZE; i++) _magazine[i] = RandomBallType();

            CreateShotTrailQuad();
        }

        //A level is played with nothing above this screen; a pause and the result screen are pushed OVER it
        //and freeze it with their UpdatesUnderlying while the manager goes on drawing it underneath them.
        //Nothing draws or runs beneath this screen itself: the backdrop under it on the stack is dormant.

        /// <summary>
        /// The one door into play, from a fresh start and from a resume alike — the manager raises this on
        /// whichever screen ends up on top, so a push, a retry's re-push and the pop of a pause or result all
        /// arrive here. What it exists for is the input state: the cursor is captured and recentred every
        /// frame while playing and left alone in the menu, so the first frame back would otherwise read the
        /// distance from wherever the player clicked to the viewport centre as an aim delta and yank the
        /// barrel across the field — and the very click that pressed the button would arrive against a stale
        /// "released" and fire a shot nobody asked for. Clearing <see cref="_mouseAimInitialized"/> skips that
        /// first frame's aim <i>and</i> its shot test, since both live behind it.
        /// </summary>
        public override void CoveredChanged()
        {
            _mouseAimInitialized = false;
            _adsHeld = false;

            //A gamepad reports to an unfocused window and to a paused one; both triggers must be released
            //before they mean anything again. One poll on a state change, which is not a per-frame path.
            _padTriggerReleased = false;
            Game.PreviousPad = GamePad.GetState(PlayerIndex.One);

            Game.IsMouseVisible = false;
        }

        #region Building and tearing down a session

        /// <summary>
        /// Tears down whatever session is standing and builds the level at <paramref name="index"/> in its
        /// place — the path a first "Play", a "New Game", a retry and an advance all take.
        /// </summary>
        internal void BuildLevel(int index)
        {
            if (IsBuilt) TearDown();

            //Whatever the last level's victory left in the air goes with it. The display lives on the host and
            //would otherwise still be bursting over the opening seconds of the next level — which reads as the
            //game celebrating a level the player has not played yet.
            Game.Fireworks?.Stop();

            //The map first: the ceiling's height and footprint come off the field, and the ceiling body has
            //to exist before the cluster, whose top level is constrained to it.
            _levelIndex = index;
            InstallLevel(_levelIndex);

            BuildPhysicsWorld();
            BuildCluster();

            //FitCeilingToMap made a new renderer, which starts without the sky palette
            Game.ApplySkyLighting();

            IsBuilt = true;
            _clearedCountdown = 0f;

            //The HUD carries state of its own across nothing: a new level starts at zero without counting down
            //to it, and a popup from the level just finished must not fly into the score of the one just built.
            //Seeded from the fresh scorer, so a new budget is not read as a ball just spent.
            _hud.Reset(_score);
            _levelLost = false;

            //The outcome has to be cleared here now that it is read for something other than building the
            //result screen: it gates the HUD, so a level entered with the last one's Failed still standing
            //would play with no readout at all. It was harmless while ShowResultScreen was its only reader —
            //that is set and consumed on the same line — which is exactly how a field like this goes stale.
            _pendingOutcome = LevelOutcome.None;
            _pendingFailure = LevelFailure.None;

            //The glass is a fresh plate at the top of a fresh field, so nothing about the last level's last
            //descent should still be glowing on it
            _ceilingFlash = 0f;
        }

        /// <summary>
        /// Tears the session down so a new one can be built. The simulation is disposed outright rather than
        /// emptied ball by ball: the constraints, the bodies, the statics and the per-worker contact queues all
        /// go with it, and rebuilding is a few milliseconds. The order is <see cref="DisposeResources"/>'s and
        /// for the same reason — <see cref="ContactEvents"/> unhooks itself from the timestepper, so it has to
        /// go before the simulation it hooked into, and the pool both allocated from has to outlive the two.
        /// </summary>
        internal void TearDown()
        {
            _events?.Dispose();
            _simulation?.Dispose();
            _threadDispatcher?.Dispose();
            _bufferPool?.Clear();

            _events = null;
            _simulation = null;
            _threadDispatcher = null;
            _bufferPool = null;
            _eventHandler = null;
            _ceiling = null;
            _map = null;
            _physicsBalls = null;

            //Cleared, never reassigned: the contact handler holds these very instances
            _shotBalls.Clear();
            _fallingBalls.Clear();
            _trails.Clear();

            _physicsAccumulator = 0f;
            _cannonRecoil = 0f;
            _magazineSlide = 0f;
            _adsBlend = 0f;

            //A cinematic caught mid-shot by a level ending under it would otherwise hold the camera and the
            //controls into the next level, and its subject handles belong to a simulation that is now gone
            _cinematic.Reset();
            _cinematicSubject.Clear();

            //The magazine is not refilled here: its colours belong to a level, and InstallLevel loads the
            //next one's before the queue means anything again

            //The gun goes back to its resting orbit angle and aim, so a new game starts pointed where the
            //first one did rather than wherever the last shot left the barrel
            _cannon.Restart();

            IsBuilt = false;
        }

        /// <summary>The session's own graphics resources, on the way out of the program.</summary>
        internal void DisposeResources()
        {
            TearDown();

            _shotTrailVertexBuffer?.Dispose();
            _shotTrailIndexBuffer?.Dispose();
        }

        /// <summary>
        /// Installs the map for one entry of the level set, and everything the field's size and depth decide:
        /// where the lattice meets the world, how high the glass hangs, how big it is, and which colours the
        /// magazine may load. Nothing here touches the simulation — it runs before
        /// <see cref="BuildPhysicsWorld"/>, which needs the ceiling's height and footprint to place its body.
        /// <para>
        /// The file is parsed <b>before</b> anything is installed, so a broken level leaves the previous state
        /// alone and simply falls back to the built-in one, exactly as the Testbed's loader does.
        /// </para>
        /// </summary>
        private void InstallLevel(int index)
        {
            LevelSet levelSet = Game.LevelSet;
            BallsMap map = null;

            if (levelSet != null && index >= 0 && index < levelSet.Count)
            {
                string path = levelSet.ResolvePath(index);

                try
                {
                    //Both a full level file (marked "bs3d-level", carrying a scene and a sky as well) and a
                    //plain map file are .json, so the loader probes exactly as the Testbed's does. A level's
                    //scene is honoured; the player's own pick from the menu stands when the level has none.
                    if (Level.IsLevelFile(path))
                    {
                        Level level = Level.Load(path);
                        map = new BallsMap(level.Map);

                        if (level.Scene != null) Game.SetScene(level.Scene.Kind);
                        Game.SetSkyDome(Math.Clamp(level.SkyDome, (byte)1, BS3DGame.SKY_DOME_COUNT));
                    }
                    else map = new BallsMap(path);

                    Console.WriteLine($"[levels] Loaded {index + 1}/{levelSet.Count} '{levelSet.DisplayName(index)}' "
                        + $"({levelSet.DescribeRules(index)}) from '{path}'");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[levels] Failed to load '{path}': {e.Message}");
                    map = null;
                }
            }

            _map = map ?? BuildFallbackMap();
            _map.Center();

            FitFieldToMap();
            FitCeilingToMap();

            //The gun and the lens both move with the field's size, and each is placed off the other
            FitCannonAndGameCameraToLevel();

            RecountBallTypes();

            for (int i = 0; i < MAGAZINE_SIZE; i++)
            {
                _magazine[i] = RandomBallType();

                //Nothing is mid-transmute in a queue that has just been dealt, and a level loaded over a
                //session that was would otherwise inherit its half-finished dissolves
                _magazineTransmute[i] = 0f;
                _magazineFrom[i] = _magazine[i];
            }

            //A fresh scorer per level, holding that entry's rules. Built even when the level fell back to the
            //built-in map, which then has no rules at all and so an unlimited budget and a still ceiling — the
            //same thing an entry that authors no "shots" or "ceilingStep" means.
            _score = new ScoreKeeper(LevelShotBudget(index), LevelCeilingStep(index));
        }

        /// <summary>
        /// The procedural pyramid the game shipped with, used only when no level file could be read: a
        /// <see cref="BallsMap"/> carved to a stepped square pyramid, apex down, hanging at the top of a
        /// taller field so the empty levels underneath are room for shot balls to attach into — the same
        /// arrangement a map file carries.
        /// </summary>
        private static BallsMap BuildFallbackMap()
        {
            BallsMap map = new(FALLBACK_X, FALLBACK_Z, FALLBACK_FIELD_LEVELS);

            //Its own generator off a fixed seed, so the pile is reproducible however many shots the
            //magazine's unseeded one has drawn by the time this runs
            Random layout = new(FALLBACK_SEED);

            //The pyramid is built about the centre of the field's topmost level, because that is the level
            //BallsMap.Center() puts on the origin. Odd levels are shifted by +0.5 in X and Z, so which
            //centre that is depends on its parity.
            float topShift = LevelShift(FALLBACK_FIELD_LEVELS - 1);
            float axisX = (FALLBACK_X - 1) * Constants.HALF + topShift;
            float axisZ = (FALLBACK_Z - 1) * Constants.HALF + topShift;

            for (byte level = 0; level < FALLBACK_LEVELS; level++)
            {
                byte fieldLevel = (byte)(level + FALLBACK_EXTRA_LEVELS);

                //Half the pyramid's width here: nothing but the apex cell at the bottom, growing half a unit
                //per level up to the full base at the top. Both this and the cell positions are whole
                //multiples of a half, hence exact in binary — which is why the test below needs no tolerance.
                float half = level * Constants.HALF;
                float shift = LevelShift(fieldLevel);

                for (byte x = 0; x < FALLBACK_X; x++)
                    for (byte z = 0; z < FALLBACK_Z; z++)
                    {
                        //Measured against where the cell actually sits, not its raw index, so a level's
                        //own half-unit offset cannot throw the flank out of line
                        if (MathF.Abs(x + shift - axisX) > half) continue;
                        if (MathF.Abs(z + shift - axisZ) > half) continue;

                        map.PutBallAt(x, z, fieldLevel, layout.Next(DEFAULT_BALL_TYPES.Length) switch
                        {
                            0 => DEFAULT_BALL_TYPES[0],
                            1 => DEFAULT_BALL_TYPES[1],
                            2 => DEFAULT_BALL_TYPES[2],
                            _ => DEFAULT_BALL_TYPES[3],
                        });
                    }
            }

            return map;
        }

        /// <summary>
        /// The half-unit offset a level's cells carry in X and Z: odd levels of the lattice are shifted, which
        /// is what nests each layer into the pockets of the one below. Mirrors
        /// <see cref="BallsMap.GetRealPosition"/>, which is where the shift actually happens.
        /// </summary>
        private static float LevelShift(byte level) => (level % 2) > 0 ? Constants.HALF : 0f;

        //There is no MapToWorld/WorldToMap pair here. The lattice-to-world offset used to be applied wherever
        //a grid position was drawn; now the bodies ARE the drawn positions, so the offset is applied exactly
        //twice — once to place them (BuildBallsStructure's worldOffset) and once inside
        //BallContactEventHandler, which takes a world contact down into the grid frame to ask the map about it.
        //Keeping a general-purpose converter around invited a third, uncounted crossing.

        /// <summary>
        /// Derives everything the loaded field's size and depth decide. See <see cref="_clusterWorldOffset"/>
        /// for why the offset has an X and a Z as well as a Y.
        /// </summary>
        private void FitFieldToMap()
        {
            XZLevel size = _map.GetStaticBallsArraySize();
            byte topLevel = (byte)(size.Level - 1);

            //The residual the centring leaves: the midpoint of the top level's own cells, measured through
            //the map's public centred-position accessor rather than re-deriving its arithmetic here
            Vector3 nearCorner = _map.GetRealCenteredPosition(new XZLevel(0, 0, topLevel));
            Vector3 farCorner = _map.GetRealCenteredPosition(new XZLevel(size.X - 1, size.Z - 1, topLevel));

            _clusterWorldOffset = new Vector3(
                -(nearCorner.X + farCorner.X) * Constants.HALF,
                FIELD_TOP_Y - topLevel / Constants.SQRT_TWO,
                -(nearCorner.Z + farCorner.Z) * Constants.HALF);

            _ceilingY = FIELD_TOP_Y + CEILING_CLEARANCE;
            //At rest to start: target equals current, so nothing slides until a step is taken.
            _ceilingTargetY = _ceilingY;
            _ceilingDescending = false;
            _clusterCentreY = topLevel * Constants.HALF / Constants.SQRT_TWO + _clusterWorldOffset.Y;
        }

        /// <summary>
        /// Rebuilds the drawn glass plate at the loaded field's footprint. The mesh and renderer are the
        /// host's — the glass is lit with the rest of the scene — so the host recreates them, and
        /// <see cref="BS3DGame.ApplySkyLighting"/> has to run after this, exactly as it does after the
        /// Testbed's <c>FitCeilingToMap</c>.
        /// </summary>
        private void FitCeilingToMap() => Game.RebuildCeilingRenderer(_map.StageSizeX, _map.StageSizeZ);

        /// <summary>
        /// Stands up the simulation and everything in it that does not move: the kinematic glass ceiling the
        /// cluster hangs from, and the island's floor — which is the drain's own surface, that being the only
        /// thing here a ball can rest on or run down.
        /// </summary>
        private void BuildPhysicsWorld()
        {
            //One dispatcher and one pool per session. ContactEvents sizes its per-worker queues from the
            //dispatcher's thread count, so the dispatcher must exist first and must outlive the simulation.
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
            //The same figures FitCeilingToMap gave the drawn box, so the glass and the collidable agree.
            Box box = new(_map.StageSizeX + 1f, 1f, _map.StageSizeZ + 1f);
            TypedIndex shape = _simulation.Shapes.Add(box);

            BodyHandle handle = _simulation.Bodies.Add(BodyDescription.CreateKinematic(
                new System.Numerics.Vector3(0f, _ceilingY, 0f),
                new CollidableDescription(shape, 0.1f),
                new BodyActivityDescription(Constants.HUNDREDTH)));

            _ceiling = new KinematicBody(new BodyReference(handle, _simulation.Bodies), handle);
        }

        /// <summary>
        /// Begins one step of the ceiling's descent: lowers the target by <see cref="CEILING_DESCENT_PER_STEP"/>,
        /// clamped at the death line so an overlong level cannot drive the glass through the gun. The body itself
        /// does not move here — <see cref="UpdateCeilingDescent"/> slides it to the target, which is what keeps a
        /// hundred constrained bodies from being jerked in a single write.
        /// </summary>
        private void StartCeilingDescent()
        {
            //No target to reach if the glass is already as low as it can go — further steps would be a no-op and
            //a needless log, and clamping here is what stops an inconsistent level (more steps than the geometry
            //allows) from scraping the body past the death line.
            if (_ceilingTargetY <= CEILING_DEATH_Y) return;

            _ceilingTargetY = MathF.Max(CEILING_DEATH_Y, _ceilingTargetY - CEILING_DESCENT_PER_STEP);
            _ceilingDescending = true;

            //The descent itself is a slow slide of a translucent plate against a sky, which is very nearly
            //invisible while the player is watching the cluster — the pressure the whole rule exists to apply
            //was arriving unnoticed. So the glass says it: it lights up red, and drives a red wave down
            //through every ball hanging on it.
            _ceilingFlash = 1f;
            StartCeilingRipple();

            Console.WriteLine($"[ceiling] Step to {_ceilingTargetY:F2} (death line {CEILING_DEATH_Y:F2})"
                + $", shots fired {_score.ShotsFired}");
        }

        /// <summary>
        /// Slides the ceiling body toward <see cref="_ceilingTargetY"/> at <see cref="CEILING_DESCENT_SPEED"/>,
        /// one frame's worth at a time, and refreshes the drawn world matrix to match. Called before the physics
        /// step so the solver works against the moved body this frame, letting the contact between a descending
        /// cluster and anything below it resolve rather than interpenetrate.
        /// </summary>
        private void UpdateCeilingDescent(float elapsed)
        {
            //Ahead of the early return: the glow outlives the slide, and it has to keep fading once the plate
            //has arrived or the glass would stay red for the rest of the level
            if (_ceilingFlash > 0f) _ceilingFlash = MathF.Max(0f, _ceilingFlash - elapsed / CEILING_FLASH_SECONDS);

            if (!_ceilingDescending) return;

            //Equal within a hair means the slide is done — a frame that would otherwise move a thousandth of a
            //unit and never quite arrive. Snap, stop, and the matrix reflects the final pose exactly.
            if (MathF.Abs(_ceilingY - _ceilingTargetY) <= CEILING_DESCENT_SPEED * elapsed)
            {
                _ceilingY = _ceilingTargetY;
                _ceilingDescending = false;
            }
            else
            {
                _ceilingY -= CEILING_DESCENT_SPEED * elapsed;
            }

            _ceiling.BodyReference.Pose.Position = new System.Numerics.Vector3(0f, _ceilingY, 0f);
            _ceiling.RefreshWorld();
        }

        /// <summary>
        /// The island's whole floor, and it is the drain's own surface: the sloped cone plus the flat stone ring
        /// from its rim out to the edge of the platform's level top, as one triangle mesh. Balls rest on the
        /// ring, run down the cone at its ~55° and drop through the hole; past the ring they fall off the
        /// island's edge into the city. Either way the kill plane takes them.
        /// <para>
        /// The ring stops at <see cref="IslandMesh.FloorRadius"/> and not at the island's own radius: the
        /// coping falls away over the last stretch, so a floor carried out to the platform's widest point
        /// would hold a ball up on air over the wash.
        /// </para>
        /// <para>
        /// Every quad goes in with <b>both</b> windings — eight triangles a segment, not four. A Bepu mesh
        /// triangle only collides on its front face, and rather than depend on getting the winding right for a
        /// surface that is met from above, from inside the funnel and from underneath, it is made double-sided
        /// deliberately.
        /// </para>
        /// </summary>
        private void BuildFunnelPhysics()
        {
            const int segments = BS3DGame.FUNNEL_SEGMENTS;
            float depth = BS3DGame.ISLAND_Y - BS3DGame.FUNNEL_BOTTOM_Y;

            //Take gives exactly the requested length, which is what the Mesh constructor is handed; TakeAtLeast
            //would round the count up and leave uninitialised triangles at the end of the buffer.
            _bufferPool.Take<Triangle>(segments * 8, out Buffer<Triangle> triangles);

            for (int s = 0; s < segments; s++)
            {
                float a0 = (float)(s / (double)segments * Math.PI * 2.0);
                float a1 = (float)((s + 1) / (double)segments * Math.PI * 2.0);

                //Local space: the rim at y = 0 and the hole at y = -depth, so the static's own pose is what
                //puts the rim flush with the island's stone top
                System.Numerics.Vector3 t0 = Ring(a0, BS3DGame.FUNNEL_TOP_RADIUS, 0f);
                System.Numerics.Vector3 t1 = Ring(a1, BS3DGame.FUNNEL_TOP_RADIUS, 0f);
                System.Numerics.Vector3 h0 = Ring(a0, BS3DGame.FUNNEL_HOLE_RADIUS, -depth);
                System.Numerics.Vector3 h1 = Ring(a1, BS3DGame.FUNNEL_HOLE_RADIUS, -depth);
                System.Numerics.Vector3 r0 = Ring(a0, IslandMesh.FloorRadius(BS3DGame.ISLAND_RADIUS), 0f);
                System.Numerics.Vector3 r1 = Ring(a1, IslandMesh.FloorRadius(BS3DGame.ISLAND_RADIUS), 0f);

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

            _simulation.Statics.Add(new StaticDescription(new System.Numerics.Vector3(0f, BS3DGame.ISLAND_Y, 0f), shape));
        }

        /// <summary>
        /// Mirrors the loaded lattice into Bepu bodies, which is what the frame actually draws, and wires up
        /// the contact handler that catches a shot landing on it.
        /// </summary>
        private void BuildCluster()
        {
            //The lattice mirrored into bodies, one per occupied cell, constrained to its neighbours and — on
            //the top level — to the ceiling. The offset is what creates them where the cluster is drawn: a
            //BallsMap reckons in its own grid frame and this game draws that frame lower (and, for an odd
            //field, half a cell across — see _clusterWorldOffset), so the empty levels below the layout do
            //not raise it. The bodies have to be in world coordinates because everything else the simulation
            //touches is — the floor, the ceiling, the muzzle a shot leaves from, the kill plane. It is
            //applied to the body positions and to nothing else: the constraint anchors are differences of two
            //grid positions, so the offset cancels out of them.
            _physicsBalls = BallsConstraintsBuilder.BuildBallsStructure(
                _map.GetStaticBallsArray(), ref _simulation, _ceiling.BodyReference,
                _clusterWorldOffset.ToNumerics());

            //What happens on a hit lives in the handler: the snap into the lattice, the constraints, and the
            //match rule. It gets the very list instances the frame draws from, and the same offset, so it can
            //take a world contact down into the grid frame to ask the map about it and bring the answer back up.
            _eventHandler = new BallContactEventHandler(_simulation, _events, _ceiling, _map, _physicsBalls,
                _shotBalls, _fallingBalls, _clusterWorldOffset);

            //The handler reports what a shot did; what it is worth is the scorer's business. Subscribed on the
            //handler the level just built, and the handler is rebuilt with it, so there is nothing to unhook.
            //Both go through a method that reads _score rather than binding the instance the field happens to
            //hold now: the scorer is replaced per level, and a handler holding a stale one would score into a
            //keeper nothing reads.
            _eventHandler.BallLanded += OnBallLanded;
            _eventHandler.ShotSpent += OnShotSpent;

            Console.WriteLine($"[game] {_map.GetBallsCount()} balls in the cluster, "
                + $"{_simulation.Solver.CountConstraints()} constraints");
        }

        #endregion

        #region The level's rules and its end

        /// <summary>
        /// A shot has landed in the lattice, having cut <paramref name="released"/> loose. Zero of both means
        /// it stuck without completing a group, which the scorer treats as a spent shot.
        /// </summary>
        private void OnBallLanded(BallLanding landing)
        {
            //The landing's own sound, before anything is scored: it depends only on the colour that hit and
            //where, not on what came loose. Panned against the camera so a hit on the left is heard on the left.
            Game.Audio.PlayLanded(landing.Type, landing.World, Camera);

            ScoreAward award = _score.Landed(landing.Released.Matched, landing.Released.Orphaned);

            //What the shot was worth, born on the cell it landed in and flown into the corner from there. The
            //type is the colour of the group it completed — a match is by definition three of one colour
            //touching — and is what the number is tinted with on the way; see PlayHud.
            if (award.Scored) _hud.AddAward(landing.World, award, landing.Type);

            //The light runs out through the cluster from where the ball hit. Started AFTER the release above,
            //so the wave walks the cluster that is left rather than the one that was: it goes around the hole
            //a matched group has just left, which is most of what makes it read as travelling through the
            //balls rather than as a sphere expanding through space.
            StartRipple(landing.Cell);

            //The cluster just changed — a ball joined it, and a group may have left. Recount before anything
            //asks what may be loaded, and re-colour whatever is already in the barrel and has just gone dead.
            RecountBallTypes();
            Transmute();

            //Before the clear test, because a shot that empties the field is the one most worth watching and
            //CheckLevelCleared starts the countdown that ends the level
            TryBeginDropCinematic(landing.Released);

            CheckLevelCleared();
        }

        /// <summary>
        /// Hands the camera to <see cref="DropCinematic"/> if this shot cut enough loose to be worth
        /// watching. The subject is the balls that were just released: <c>ReleaseSameTypeCluster</c> appends
        /// them to <see cref="_fallingBalls"/>, so they are that list's last
        /// <c>Matched + Orphaned</c> entries at this instant and nothing else has run in between.
        /// <para>
        /// They are held by <b>body handle</b> rather than by index, because the kill plane removes them from
        /// the list one by one as they go. Bepu recycles a handle once its body is gone, which would let a
        /// later ball inherit a dead subject's identity — harmless here and only here, because nothing new is
        /// added to the simulation while a cinematic runs: the gun's controls are locked, so there is no shot
        /// to land and therefore no further release.
        /// </para>
        /// </summary>
        private void TryBeginDropCinematic(BallsReleased released)
        {
            int total = released.Matched + released.Orphaned;

            //Never over a cinematic already running, and never over the end of a level: the result screen is
            //about to cover this one, and a camera move under it is a move nobody sees.
            if (total < DropCinematic.MIN_BALLS || _cinematic.Engaged || _levelLost || _clearedCountdown > 0f) return;

            int first = _fallingBalls.Count - total;
            if (first < 0) return;

            _cinematicSubject.Clear();

            Vector3 centre = Vector3.Zero;

            for (int i = first; i < _fallingBalls.Count; i++)
            {
                BodyReference body = _fallingBalls[i].BallReference;

                _cinematicSubject.Add(body.Handle.Value);

                System.Numerics.Vector3 position = body.Pose.Position;
                centre += new Vector3(position.X, position.Y, position.Z);
            }

            centre /= total;

            _cinematic.Begin(Game.Scene, centre, Camera.Position, total, RANDOM);

            //One line per cinematic, in the manner of the [level] and [score] lines: it is a rare event, not a
            //per-frame one, and the shot is rolled — so when one frames badly this is the only record of what
            //it actually chose.
            Console.WriteLine($"[cinematic] {total} balls ({released.Matched} matched, {released.Orphaned} orphaned)"
                + $" from y={centre.Y:F1}, {_cinematic.Describe()}");
        }

        /// <summary>
        /// Where the released group is now, averaged over the ones the kill plane has not taken yet. False
        /// once the last of them is gone, which is what ends the cinematic.
        /// </summary>
        private bool TryGetDropCentre(out Vector3 centre)
        {
            centre = Vector3.Zero;

            if (_cinematicSubject.Count == 0) return false;

            int found = 0;

            for (int i = 0; i < _fallingBalls.Count; i++)
            {
                BodyReference body = _fallingBalls[i].BallReference;
                if (!_cinematicSubject.Contains(body.Handle.Value)) continue;

                System.Numerics.Vector3 position = body.Pose.Position;
                centre += new Vector3(position.X, position.Y, position.Z);
                found++;
            }

            if (found == 0) return false;

            centre /= found;
            return true;
        }

        /// <summary>A shot is over without having landed. The streak breaks.</summary>
        private void OnShotSpent() => _score.Missed();

        /// <summary>
        /// Has the field just been emptied? That is the goal of a level: release every ball so it falls.
        /// <para>
        /// Tested only here, on a landing, which is the one thing that can empty the field — and testing it
        /// anywhere else would be worse than redundant. Polling it per frame would declare a level authored
        /// with no balls won before the player had fired a shot, and would keep declaring it.
        /// </para>
        /// <para>
        /// The completion bonus is awarded <b>now</b> rather than when the level actually ends, so that shots
        /// fired into the empty field during the pause below cannot eat the balls the player finished with.
        /// A shot still in flight at this moment is harmless: there is nothing left for it to hit, and the
        /// body goes with the simulation when the next level replaces it.
        /// </para>
        /// </summary>
        private void CheckLevelCleared()
        {
            if (_levelLost || _clearedCountdown > 0f || _map.GetBallsCount() > 0) return;

            int bonus = _score.AwardCompletionBonus();
            _clearedCountdown = LEVEL_CLEARED_BEAT;

            //The party. Started here rather than when the result screen appears, so the first shells are
            //already climbing while the last of the cluster is still falling — the celebration overlaps the
            //moment it is celebrating instead of following it. It runs on the host, so it carries on over the
            //result screen and the released camera swings through it (see Fireworks).
            Game.Fireworks?.Celebrate(CELEBRATION_SECONDS);

            //The bonus has no popup to fly in and land on the readout, so it would otherwise be the one award
            //the score takes without being hit — it counts up out of nowhere while the collapse plays
            if (bonus > 0) _hud.FlashScore();

            Console.WriteLine($"[level] Cleared '{LevelName(_levelIndex)}' with {_score.Score}"
                + $" (+{bonus} for {_score.ShotsRemaining?.ToString() ?? "unlimited"} unused)"
                + $", needed {LevelMinScore(_levelIndex)}");
        }

        /// <summary>
        /// Has the level been lost? The two pressures that lose it — a spent budget with the field uncleared, and
        /// the ceiling reaching the death line — are decided here, after the physics step, once every shot in
        /// flight has resolved. Either one alone loses; both are checked because either can be the one a last shot
        /// earned.
        /// </summary>
        /// <remarks>
        /// The spent budget is tested last — after a possible clear this same frame has run. The last ball of a
        /// budget may be the one that empties the field, and a loss called before <see cref="OnBallLanded"/> had
        /// its say would steal that win. So the budget only loses when nothing is in flight and the field is still
        /// standing. The ceiling, by contrast, is an immediate loss the moment a ball crosses the line — a descent
        /// can push one there between landings, so it cannot wait on the same event the budget does.
        /// </remarks>
        private void CheckLevelLost()
        {
            //Already ending — a cleared countdown or a loss in flight. Testing further would re-trigger a loss
            //on top of a clear or a teardown already underway.
            if (_clearedCountdown > 0f || _levelLost) return;

            //The ceiling reaching the death line. Live poses are in _physicsBalls (the lattice in _map holds
            //cells, not bodies); the loop mirrors DrawBallsInstanced, including the null check for cells a
            //release has emptied.
            XZLevel size = XZLevel.FromArray(_physicsBalls);

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        PhysicsBall ball = _physicsBalls[x, z, level];
                        if (ball == null) continue;

                        if (ball.BallReference.Pose.Position.Y <= CEILING_DEATH_Y)
                        {
                            LoseLevel(LevelFailure.ClusterReachedLine,
                                $"a ball at {ball.BallReference.Pose.Position.Y:F2} <= {CEILING_DEATH_Y:F2}");
                            return;
                        }
                    }

            //The budget spent with the field uncleared — but only once every shot has RESOLVED, so the last ball
            //fired has had its chance to clear. A ball still in flight could be that chance, and a loss called
            //beneath it would steal the win. "Resolved" is the load-bearing word: see AnyShotUndecided.
            if (_score.OutOfShots && !AnyShotUndecided() && _map.GetBallsCount() > 0)
                LoseLevel(LevelFailure.OutOfBalls,
                    $"budget {LevelShotBudget(_levelIndex)?.ToString() ?? "unlimited"}, fired {_score.ShotsFired}"
                    + $", {_shotBalls.Count} spent ball(s) not yet culled");
        }

        /// <summary>
        /// Whether any shot is still <b>undecided</b> — in flight, and so still able to reach the cluster and
        /// clear the field.
        /// <para>
        /// Deliberately not <c>_shotBalls.Count == 0</c>, which was the bug behind #66. That list holds shot
        /// <i>bodies</i>, and a ball is taken out of it only when it attaches or is culled — so a ball that
        /// comes to rest on the island's stone ring stays in it for the rest of the session, since the game does
        /// not sleep-cull (see <see cref="RemoveFallenBalls"/> for why not). One such ball parked there made the
        /// out-of-balls loss unreachable for ever: the HUD read 0 balls left and no result screen ever came up.
        /// It bit on the authored 30-shot level and not on a 3-shot test set, because the smaller the budget the
        /// less chance there is of a shot having settled on the stone by the time it runs out.
        /// </para>
        /// <para>
        /// The quantity wanted is already maintained. A ball stops being a contact listener at exactly the
        /// moment its shot resolves — on attaching, on touching anything static or kinematic, or on being culled
        /// — and <see cref="Shoot"/> is the only place anything is ever registered, so every listener is a shot
        /// still in the air. A counter of its own would be a fourth place to keep in step with those three.
        /// </para>
        /// </summary>
        private bool AnyShotUndecided()
        {
            //An indexed walk over at most a handful of balls, like the handler's own FindShotBall, and IsListener
            //is a set lookup — this runs per frame, so neither LINQ nor an allocation belongs here.
            for (int i = 0; i < _shotBalls.Count; i++)
            {
                BodyReference body = _shotBalls[i].BallReference;

                //Awake as well as listening, which closes the one case the listener flag alone does not: a ball
                //that comes to rest supported by another LOOSE shot ball has touched nothing static or kinematic
                //and never attached, so nothing ever unregistered it (the handler returns without unregistering
                //when the other body is neither structure nor ceiling) — yet a sleeping ball is plainly not
                //going to reach the cluster. A ball in flight is always awake, so this can never hide a live shot.
                if (_events.IsListener(body.CollidableReference) && body.Awake) return true;
            }

            return false;
        }

        /// <summary>
        /// Ends the level as a loss for the stated reason. It does <b>not</b> tear the session down here — a loss
        /// can be reached from the middle of <see cref="Update"/> (a shot that spends the budget, a frame that
        /// slides the ceiling past the line), and rebuilding mid-frame would leave the rest of the frame running
        /// against a simulation that no longer exists. Instead it sets the outcome and hands the player the result
        /// screen, whose Retry button does the real reload — the same screen a cleared level lands on.
        /// </summary>
        /// <param name="diagnostic">
        /// The figures behind the loss. <b>Logged and never shown</b>: what a player needs is which limit ran
        /// out, and a world-space Y against a death line tells them nothing they can act on.
        /// </param>
        private void LoseLevel(LevelFailure failure, string diagnostic)
        {
            //Once only: a descent and a budget can reach their lines on the same frame, and a loss in flight
            //must not stack a second screen onto the first.
            if (_levelLost) return;
            _levelLost = true;

            Console.WriteLine($"[level] Lost '{LevelName(_levelIndex)}': {failure} ({diagnostic}), score {_score.Score}");

            _pendingOutcome = LevelOutcome.Failed;
            _pendingFailure = failure;
            ShowResultScreen();
        }

        /// <summary>
        /// What the player is told about a loss. The two hard limits carry no figures at all — a world-space Y
        /// or a budget they already watched run down tells them nothing they can act on. The gate does carry
        /// one, because the number they missed by is the whole of what it is telling them.
        /// </summary>
        private string FailureText(LevelFailure failure) => failure switch
        {
            LevelFailure.OutOfBalls => "You ran out of balls.",
            LevelFailure.ClusterReachedLine => "The cluster reached the line.",
            LevelFailure.ShortOfGate => $"Cleared — but {LevelMinScore(_levelIndex):N0} was needed to unlock the next level.",
            _ => string.Empty,
        };

        /// <summary>
        /// The level is over and the collapse has played out. It does <b>not</b> act on the outcome — it decides
        /// which one it was and hands the player the result screen to choose what to do about it. The build,
        /// the teardown and the advance live behind the result screen's buttons
        /// (<see cref="BS3DGame.RetryLevel"/>, <see cref="BS3DGame.AdvanceLevel"/>), which is what a player
        /// actually presses.
        /// <para>
        /// A cleared field is the only thing that reaches here today; a lost one reaches the same screen through
        /// <see cref="LoseLevel"/>. Both set the outcome and call <see cref="ShowResultScreen"/>, and the screen
        /// does the rest.
        /// </para>
        /// </summary>
        private void FinishLevel()
        {
            int required = LevelMinScore(_levelIndex);

            //Cleared but short of the gate is a fail the player chose — a sloppy clear — rather than a clear
            //the game then undoes. The level did not advance, and "Retry" is the way forward.
            if (_score.Score < required)
            {
                _pendingOutcome = LevelOutcome.Failed;
                _pendingFailure = LevelFailure.ShortOfGate;
            }
            else
            {
                _pendingOutcome = LevelOutcome.Cleared;
                _pendingFailure = LevelFailure.None;
            }

            ShowResultScreen();
        }

        /// <summary>
        /// Puts the result screen over the stopped frame. The screen is a push over this one, exactly as a
        /// pause is: this screen goes on being drawn underneath while its <c>UpdatesUnderlying</c> stops the
        /// simulation. Called when a level ends — see <see cref="FinishLevel"/> and <see cref="LoseLevel"/>.
        /// </summary>
        private void ShowResultScreen()
        {
            //The figures are handed over as a SNAPSHOT taken now, not read by the screen when it draws. The
            //level does not stop the instant it is cleared — the collapse is held for a beat and a player who
            //keeps firing moves the balls remaining — so a screen that re-read the keeper printed a row that
            //did not add up to the total above it. See LevelResult.
            bool cleared = _pendingOutcome == LevelOutcome.Cleared;
            bool lastEntry = Game.LevelSet == null || _levelIndex + 1 >= Game.LevelSet.Count;
            bool shortOfGate = _pendingOutcome == LevelOutcome.Failed && _pendingFailure == LevelFailure.ShortOfGate;

            Game.PresentResult(new LevelResult(
                cleared: cleared,
                failureText: cleared ? null : FailureText(_pendingFailure),
                shortOfGate: shortOfGate,
                hasNextLevel: !lastEntry,

                //"Campaign complete" only when there actually was a campaign — a set of more than one level
                //cleared to its end. A single-level set is just a level cleared, and calling it a campaign's
                //end overstates what happened and hides the Retry that is still the point of the screen.
                campaignComplete: cleared && lastEntry && Game.LevelSet != null && Game.LevelSet.Count > 1,

                score: _score.Score,
                matchedBalls: _score.MatchedBalls,
                orphanedBalls: _score.OrphanedBalls,
                streakBonus: _score.StreakBonus,
                hadBudget: _score.ShotsRemaining.HasValue,
                unusedShotsAwarded: _score.UnusedShotsAwarded,
                completionBonusAwarded: _score.CompletionBonusAwarded,
                neededScore: LevelMinScore(_levelIndex)));

            Console.WriteLine($"[level] Result for '{LevelName(_levelIndex)}': {_pendingOutcome}" + (_pendingOutcome == LevelOutcome.Failed ? $" ({_pendingFailure})" : "")
                + $", score {_score.Score}");
        }

        /// <summary>What to call the level at <paramref name="index"/>, set or no set.</summary>
        private string LevelName(int index) =>
            Game.LevelSet != null && index >= 0 && index < Game.LevelSet.Count ? Game.LevelSet.DisplayName(index) : "the built-in level";

        /// <summary>
        /// The score the entry at <paramref name="index"/> demands before the <b>next</b> level unlocks. Zero —
        /// what an absent rule, a missing set and an index outside it all mean — leaves clearing the field
        /// enough on its own, which is what every level is until one opts into the gate.
        /// </summary>
        private int LevelMinScore(int index) =>
            Game.LevelSet != null && index >= 0 && index < Game.LevelSet.Count
                ? Game.LevelSet.Levels[index].MinScore.GetValueOrDefault()
                : 0;

        /// <summary>
        /// The ball budget the entry at <paramref name="index"/> grants, or null for unlimited — which is what
        /// an absent <c>shots</c> rule, an index outside the set and a missing set all mean. This is the read
        /// site the nullable rule is documented against: the set records only that a rule is absent, and this
        /// is where the game says what absent means.
        /// </summary>
        private int? LevelShotBudget(int index) =>
            Game.LevelSet != null && index >= 0 && index < Game.LevelSet.Count ? Game.LevelSet.Levels[index].Shots : null;

        /// <summary>
        /// Shots between two descents of the glass ceiling, or null for a ceiling that holds still — which is what
        /// an absent <c>ceilingStep</c> rule, an index outside the set and a missing set all mean. Mirrors
        /// <see cref="LevelShotBudget"/>: the nullable rule is read at one site, and this is where absent is given
        /// its meaning.
        /// </summary>
        private int? LevelCeilingStep(int index) =>
            Game.LevelSet != null && index >= 0 && index < Game.LevelSet.Count ? Game.LevelSet.Levels[index].CeilingStep : null;

        /// <summary>
        /// Recounts how many balls of each colour are still hanging. The magazine may only load a colour whose
        /// count is above zero: a ball of a colour that exists nowhere in the cluster can never match anything,
        /// so it can only be parked somewhere — which grows the very cluster the player is shrinking, wastes a
        /// budgeted shot, and in the limit makes a level unwinnable.
        /// <para>
        /// A colour with fewer than <see cref="BallsConstraintsBuilder.MINIMUM_CLUSTER_SIZE"/> left is arguably
        /// already dead weight and could be dropped from the queue early. It is deliberately <b>not</b>: that
        /// changes which levels are solvable at all, which makes it a difficulty decision rather than this fix.
        /// </para>
        /// </summary>
        private void RecountBallTypes()
        {
            for (int i = 0; i < _ballsOfType.Length; i++) _ballsOfType[i] = 0;

            StaticBall[,,] balls = _map.GetStaticBallsArray();
            XZLevel size = XZLevel.FromArray(balls);

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        StaticBall ball = balls[x, z, level];
                        if (ball == null) continue;

                        int index = (int)ball.Type - 1;
                        if (index >= 0 && index < _ballsOfType.Length) _ballsOfType[index]++;
                    }
        }

        /// <summary>
        /// Re-colours every loaded ball whose colour has just been eliminated from the cluster, and starts the
        /// dissolve that shows it happening.
        /// <para>
        /// The alternative — letting a stale queue play out — costs the player up to <see cref="MAGAZINE_SIZE"/>
        /// shots on colours that cannot match anything, through no fault of their own. This is a game and not a
        /// simulation, so a ball that is already loaded may simply be re-coloured; the player will notice, and
        /// noticing is the point, because what they see is the game helping rather than the game cheating them.
        /// </para>
        /// <para>
        /// The colour changes <b>immediately</b> and the dissolve is cosmetic: firing mid-transition must give
        /// the new colour, never the dead one it is still fading out of. The replacement is drawn at random
        /// from what survives — picking whichever colour would help most would quietly make the game easier,
        /// and that is a difficulty decision, not a fix.
        /// </para>
        /// </summary>
        private void Transmute()
        {
            for (int slot = 0; slot < MAGAZINE_SIZE; slot++)
            {
                int index = (int)_magazine[slot] - 1;
                if (index >= 0 && index < _ballsOfType.Length && _ballsOfType[index] > 0) continue;

                BallType replacement = RandomBallType();
                if (replacement == _magazine[slot]) continue; //nothing survives to swap to; leave it alone

                //The ball it is fading OUT of is whatever is on screen now — which for a slot caught
                //mid-transmute is the colour it was already fading out of, not the one it never finished
                //becoming. Restarting from the visible colour is what keeps the animation continuous.
                if (_magazineTransmute[slot] <= 0f) _magazineFrom[slot] = _magazine[slot];

                Console.WriteLine($"[transmute] slot {slot}: {_magazineFrom[slot]} is gone from the cluster -> {replacement}");

                _magazine[slot] = replacement;
                _magazineTransmute[slot] = 1f;
            }
        }

        /// <summary>
        /// What the magazine loads next: one of the colours <b>still hanging</b> (see
        /// <see cref="RecountBallTypes"/>), drawn evenly among them off the unseeded run-to-run generator.
        /// Not a static method by accident — the live set changes with every shot that lands, so it cannot
        /// be static the way it was when the cluster was a fixed pyramid.
        /// </summary>
        private BallType RandomBallType()
        {
            int live = 0;
            for (int i = 0; i < _ballsOfType.Length; i++) if (_ballsOfType[i] > 0) live++;

            //An empty cluster — a level authored with no balls, or one the player has just cleared. There is
            //nothing left to match, so what is loaded cannot matter; the default four keep the barrel full.
            if (live == 0) return DEFAULT_BALL_TYPES[RANDOM.Next(DEFAULT_BALL_TYPES.Length)];

            int pick = RANDOM.Next(live);

            for (int i = 0; i < _ballsOfType.Length; i++)
            {
                if (_ballsOfType[i] <= 0) continue;
                if (pick == 0) return (BallType)(i + 1);

                pick--;
            }

            return DEFAULT_BALL_TYPES[0]; //unreachable: pick < live, and live is the count of the loop's hits
        }

        #endregion

        public override void Update(GameTime gameTime)
        {
            if (!IsBuilt) return;

            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (Game.IsActive)
            {
                Game.IsMouseVisible = false;

                //One XInput poll for the whole frame: UpdateInput and UpdateAim used to each poll the pad,
                //two OS queries of the same slot microseconds apart
                GamePadState pad = GamePad.GetState(PlayerIndex.One);

                UpdateInput(gameTime, Game.EdgeInputAllowed, pad);
                UpdateAim(gameTime, Game.EdgeInputAllowed, pad);
            }
            else
            {
                //The cursor belongs to the desktop again as soon as the window is not the one being played:
                //hidden over an unfocused window it simply disappears wherever the player moves it.
                Game.IsMouseVisible = true;
                _mouseAimInitialized = false;

                //A trigger held while the window was away must be re-released before it fires
                _padTriggerReleased = false;

                //And a held precise-aim button must not keep an alt-tabbed window leaned in — the gamepad's
                //triggers report through XInput whether the window has focus or not. The blend below still
                //runs, so losing focus eases the lean out rather than dropping it.
                _adsHeld = false;
            }

            _cannon.Update(gameTime);

            //The queue glides forward into the slot the fired ball left rather than snapping
            if (_magazineSlide > 0f) _magazineSlide *= MathF.Exp(-elapsed / MAGAZINE_SLIDE_TAU);
            if (_magazineSlide < 0.001f) _magazineSlide = 0f;

            //And a re-coloured ball dissolves out of its old colour. Linear, so it genuinely finishes rather
            //than leaving a slot for ever a few pixels short of its new colour.
            for (int i = 0; i < MAGAZINE_SIZE; i++)
                if (_magazineTransmute[i] > 0f)
                    _magazineTransmute[i] = MathF.Max(0f, _magazineTransmute[i] - elapsed / TRANSMUTE_SECONDS);

            //The barrel slides home. Linear in the stroke, so it genuinely ends rather than approaching zero
            //forever and leaving the gun permanently a hair out of place.
            if (_cannonRecoil > 0f) _cannonRecoil = MathF.Max(0f, _cannonRecoil - CANNON_RECOIL_DECAY * elapsed);

            //The cinematic reads the balls where the last step left them and answers with this frame's pose and
            //time scale, so the scale is applied to the very step its own framing was chosen against.
            _cinematic.Update(elapsed, TryGetDropCentre(out Vector3 dropCentre), dropCentre);

            if (!_cinematic.Engaged) _cinematicSubject.Clear();

            //Slide the ceiling before the step, so the solver works against the moved body this frame and the
            //contact between a descending cluster and anything below it resolves rather than interpenetrates.
            UpdateCeilingDescent(elapsed);

            //Slow motion is applied here and nowhere else: the fixed timestep is untouched and only the time
            //fed to the accumulator is scaled, so a slowed world is exactly as stable as a full-speed one. The
            //ceiling's descent above is deliberately NOT scaled — it is a rule of the level playing out, not
            //part of the spectacle, and it is not moving while the gun is locked anyway.
            StepPhysics(elapsed * _cinematic.TimeScale);

            //After the step: poses have advanced, so a ball dragged down by the descent is at its new Y now, and a
            //shot that spent the budget has had its landing. The two losses are checked here rather than only on a
            //landing, because a descent can push a ball across the death line between landings, and a spent budget
            //loses only once nothing remains in flight.
            //
            //Held while a cinematic runs. Both endings put the result screen over this one, and a level that
            //ends mid-collapse takes the collapse the player earned off the screen before they have seen it —
            //which is the same reason the cleared countdown below waits, and why LEVEL_CLEARED_BEAT exists.
            if (!_cinematic.Engaged) CheckLevelLost();

            UpdateTrails(elapsed);
            _hud.Update(elapsed, _score);

            UpdateCamera(elapsed);

            //Last, because FinishLevel hands the player the result screen — pushed over this one, so the rest
            //of this frame still ran against a consistent session and nothing above it has torn anything down.
            //
            //A loss needs no entry here: LoseLevel shows the result screen straight away (the same one a clear
            //lands on), which covers this screen and freezes it until the player picks Retry or leaves.
            if (_clearedCountdown > 0f && !_cinematic.Engaged)
            {
                _clearedCountdown -= elapsed;
                if (_clearedCountdown <= 0f) FinishLevel();
            }
        }

        #region Input

        private void UpdateInput(GameTime gameTime, bool edgeInputAllowed, GamePadState pad)
        {
            KeyboardState keyboard = Keyboard.GetState();

            if (edgeInputAllowed)
            {
                //Escape (or the gamepad's Back button) pauses rather than quitting outright: quitting is a
                //menu item now, and a game that closes the instant Escape is tapped is one that loses a game.
                if (Game.IsKeyEdge(keyboard, Keys.Escape) || (pad.IsButtonDown(Buttons.Back) && !Game.PreviousPad.IsButtonDown(Buttons.Back)))
                {
                    Game.PreviousKeyboard = keyboard;
                    Game.PreviousPad = pad;
                    Game.PauseGame();

                    //Nothing else this frame: the game is paused as of now, and firing or traversing on the
                    //way out would be an action the player asked of a game that has stopped
                    return;
                }

                if (Game.IsKeyEdge(keyboard, Keys.F11)) Game.ToggleFullscreen();

                //F12 hides the FPS overlay, the same key that hides the Testbed's text
                if (Game.IsKeyEdge(keyboard, Keys.F12)) Game.ToggleFpsOverlay();

                //While the drop cinematic has the camera the gun does not answer, and Space skips the shot
                //instead of firing. Escape is deliberately NOT the skip: it already means pause, and taking
                //it would both give one key two meanings and leave the player unable to pause during a
                //cinematic — the skip belongs with the buttons that mean "yes, go on" (Space, the left mouse
                //button and the pad's A), which are the ones the hand is already on.
                if (_cinematic.Engaged)
                {
                    if (Game.IsKeyEdge(keyboard, Keys.Space)
                        || (pad.IsButtonDown(Buttons.A) && !Game.PreviousPad.IsButtonDown(Buttons.A)))
                        _cinematic.TrySkip();

                    Game.PreviousKeyboard = keyboard;
                    return;
                }

                //Space fires; the gamepad fires off its right trigger, read with the aim (below)
                if (Game.IsKeyEdge(keyboard, Keys.Space)) Shoot();
            }
            else if (_cinematic.Engaged)
            {
                //Edge input is being held off for a frame after a refocus, but the gun still must not answer
                Game.PreviousKeyboard = keyboard;
                return;
            }

            //The carriage traverses on A/D — it turns where it stands, it does not walk
            if (keyboard.IsKeyDown(Keys.A)) _cannon.Orbit(CANNON_ORBIT_RATE);
            else if (keyboard.IsKeyDown(Keys.D)) _cannon.Orbit(-CANNON_ORBIT_RATE);

            Game.PreviousKeyboard = keyboard;
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

            //While the cinematic has the frame the barrel does not move and nothing fires — but the cursor is
            //still recentred below, so the aim is not handed back a delta measured from wherever the mouse
            //drifted to during the shot. The left button skips, matching Space and the pad's A.
            if (_cinematic.Engaged)
            {
                if (edgeInputAllowed && _mouseAimInitialized
                    && mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released)
                    _cinematic.TrySkip();

                //Forced rather than read: the lean is a hold, so a player still holding the right button when
                //the cinematic ends gets precise aim back, and one who let go during it does not
                _adsHeld = false;

                Mouse.SetPosition(centreX, centreY);
                _mouseAimInitialized = true;
                _previousMouse = mouse;

                Game.PreviousPad = pad;
                return;
            }

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

            //Stored here rather than in UpdateInput, which runs first: the Back button's press edge is
            //measured against the previous *frame*, so the snapshot has to be the last thing the frame does
            //with the pad. Both methods are handed the one poll taken at the top of Update.
            Game.PreviousPad = pad;
        }

        /// <summary>
        /// Fires the ball the player can see sitting at the muzzle: a real body thrown down the bore, a launch
        /// smear along the shot, the queue shifted forward — and the camera and the barrel both kicked, which is
        /// the whole feel of the thing. Where it goes from there is the simulation's business: it may hit the
        /// cluster and attach (see <see cref="BallContactEventHandler"/>), bounce off the glass, run down the
        /// drain, or fly off the island and be culled.
        /// </summary>
        private void Shoot()
        {
            //The budget is spent, so no more shots leave the barrel — but the level is not lost here. The last
            //ball fired is still in flight and may be the one that clears the field, and a loss called now would
            //steal that win. Whether the spent budget actually loses is decided once every shot has resolved,
            //in CheckLevelLost, against the state of the field then.
            if (_score.OutOfShots) return;

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

            //The ball is spent the instant it leaves the barrel. What it *did* takes a physics step or more to
            //resolve, so the budget and the score are driven by different events on purpose — see ScoreKeeper.
            _score.Shot();

            //The same shot drives the ceiling's descent: every ceilingStep-th shot steps the glass down. Checked
            //after Shot() so ShotsFired includes the one just fired, and the scorer owns the cadence exactly as it
            //owns the budget — the two pressures are coupled by design, so they are read in one place.
            if (_score.StepCeilingThisShot()) StartCeilingDescent();

            //Registered after the body exists, since a listener is keyed on its collidable reference
            _events.Register(_simulation.Bodies[handle].CollidableReference, _eventHandler);

            _trails.Add(new ShotTrail { Origin = muzzle, Direction = direction, Color = TrailColorFor(type), Age = 0f });

            AdvanceMagazine();

            //Set, not accumulated: a barrel's recoil stroke restarts from the top with every round, it does
            //not stack up over a burst the way the camera's trauma does.
            _cannonRecoil = 1f;

            //Fired, therefore felt. Nothing else in the frame moves the camera, so every wobble the player
            //sees is unambiguously their own shot.
            Camera.Shake.Kick(RECOIL_KICK);

            //Heard as well as felt: the shot's synthesized crack, centred (the muzzle is the lens's own work)
            //and nudged by a small random pitch so a burst never sounds flat.
            Game.Audio.PlayShoot();
        }

        private void AdvanceMagazine()
        {
            //A ball's half-finished transmute rides forward with it: the queue is drawn from these three
            //arrays in step, so shifting only the colours would leave a slot dissolving out of a colour that
            //belongs to the ball behind it.
            for (int i = 0; i < MAGAZINE_SIZE - 1; i++)
            {
                _magazine[i] = _magazine[i + 1];
                _magazineFrom[i] = _magazineFrom[i + 1];
                _magazineTransmute[i] = _magazineTransmute[i + 1];
            }

            _magazine[MAGAZINE_SIZE - 1] = RandomBallType();
            _magazineTransmute[MAGAZINE_SIZE - 1] = 0f; //freshly drawn from what is alive; nothing to fade

            //Armed at one slot back, so the queue eases forward into the muzzle slot the shot just vacated
            _magazineSlide = 1f;
        }

        #endregion

        #region The simulation's frame

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

                if (unregisterListeners && _events.IsListener(body.CollidableReference))
                {
                    //Still listening means the shot never resolved: it missed the island as well as the
                    //cluster and fell straight past everything into the city. The far rarer of the two misses
                    //— a shot that strikes the stone is spent the moment it does (see the handler) — but it is
                    //what makes "every shot resolves exactly once" true rather than nearly true.
                    _score.Missed();

                    _events.Unregister(body.CollidableReference);
                }

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

        #endregion

        #region The game camera and precise aim

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

            Vector3 overviewPosition = GameCameraPositionAt(_gameCameraDistance);
            Vector3 overviewTarget = new(_cannon.OrbitCenter.X, _gameCameraTargetY, _cannon.OrbitCenter.Z);

            Vector3 position = Vector3.Lerp(overviewPosition, AdsCameraPosition(), _adsBlend);
            Vector3 target = Vector3.Lerp(overviewTarget, AdsCameraTarget(), _adsBlend);
            float fov = MathHelper.Lerp(GAME_FOV, ADS_FOV, _adsBlend);

            //And the drop cinematic is a second Lerp over the top of that one, on its own reversible scalar
            //and for the same reason: at a blend of 0 these three lines return the pose above bit for bit, so
            //a cinematic that ends — or is skipped halfway — hands the player back exactly the frame the game
            //would have given them. The tilt rides along, and is the camera's only deliberate roll.
            float cinematic = _cinematic.Blend;

            if (cinematic > 0f)
            {
                position = Vector3.Lerp(position, _cinematic.Position, cinematic);
                target = Vector3.Lerp(target, _cinematic.Target, cinematic);
                fov = MathHelper.Lerp(fov, _cinematic.FieldOfView, cinematic);
            }

            Camera.BasePosition = position;
            Camera.BaseTarget = target;
            Camera.FieldOfView = fov;
            Camera.BaseRoll = _cinematic.Roll * cinematic;

            Camera.Update(elapsed);
        }

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

            Vector3 clusterCentre = new(_cannon.OrbitCenter.X, _clusterCentreY, _cannon.OrbitCenter.Z);
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

        #endregion

        #region Fitting the camera and the gun to the level

        /// <summary>
        /// The horizontal direction from the field out towards the gun — the way the camera stands back.
        /// Deliberately <b>flattened to the horizontal</b>: taken straight from <c>Position - OrbitCenter</c>
        /// it tilts down by however far the gun stands below the cluster, which eats the camera's height and
        /// leaves the lens sitting on the barrel's own axis, seeing the gun end-on.
        /// </summary>
        private Vector3 GameCameraBearing()
        {
            Vector3 back = _cannon.Position - _cannon.OrbitCenter;
            back.Y = 0f;

            return back == Vector3.Zero ? Vector3.Backward : Vector3.Normalize(back);
        }

        /// <summary>The field's centre at ground level: what the camera stands off from and turns about.</summary>
        private Vector3 FieldCentreGround() => new(_cannon.OrbitCenter.X, 0f, _cannon.OrbitCenter.Z);

        /// <summary>Where the lens sits for a given stand-off — the pose the fit below searches over.</summary>
        private Vector3 GameCameraPositionAt(float distance) =>
            FieldCentreGround() + GameCameraBearing() * distance + Vector3.Up * (_cannon.Position.Y + CAMERA_HEIGHT);

        /// <summary>
        /// The viewport has changed size or shape under the session: the aim's mouse baseline is stale (the
        /// delta is measured against the viewport centre, which just moved — left alone, the frame after an
        /// F11 reads a delta of half the screen and slams the barrel into its elevation clamp), and the
        /// camera's fit has to be re-solved, since which frustum axis binds flips with the aspect.
        /// </summary>
        internal void OnViewportChanged()
        {
            _mouseAimInitialized = false;

            FitCannonAndGameCameraToLevel();
        }

        /// <summary>
        /// Solves the gun's orbit radius and the camera's stand-off together, since each depends on the other
        /// — the camera is placed to frame the field <i>and the gun</i>, and the gun is placed a fixed
        /// distance in front of the camera. Alternating converges at once in practice: at a fixed distance
        /// from the lens the gun's angular footprint is the same whatever the radius, so the camera's solve
        /// barely moves after the first round. Run on every level load and every resize.
        /// </summary>
        private void FitCannonAndGameCameraToLevel()
        {
            if (_map == null) return;

            for (int round = 0; round < 3; round++)
            {
                FitCannonOrbitToLevel();
                FitGameCameraToLevel();
            }

            Console.WriteLine($"[camera] Field {_map.StageSizeX}x{_map.StageSizeZ}x{_map.Levels}, aspect {Camera.AspectRatio:F2}: "
                + $"camera {_gameCameraDistance:F1} out, aim Y {_gameCameraTargetY:F1}, "
                + $"gun orbit {_cannon.OrbitRadius:F1} ({_gameCameraDistance - _cannon.OrbitRadius:F1} in front of the lens)");
        }

        /// <summary>
        /// Puts the gun <see cref="CANNON_CAMERA_STANDOFF"/> in front of the camera, held off by the two lower
        /// bounds documented with that constant.
        /// </summary>
        private void FitCannonOrbitToLevel()
        {
            float halfX = (_map.StageSizeX + 1f) * Constants.HALF;
            float halfZ = (_map.StageSizeZ + 1f) * Constants.HALF;

            float clearFootprint = MathF.Sqrt(halfX * halfX + halfZ * halfZ) + CANNON_FIELD_CLEARANCE;
            float clearElevation = (_cannon.OrbitCenter.Y - _cannon.Position.Y) / MathF.Tan(CANNON_MAX_REST_ELEVATION);

            _cannon.OrbitRadius = MathF.Max(_gameCameraDistance - CANNON_CAMERA_STANDOFF,
                MathF.Max(clearFootprint, clearElevation));
        }

        /// <summary>
        /// Places the camera so the whole play field, the glass over it and the gun fit inside the frustum,
        /// and aims it so they sit centred in it.
        /// <para>
        /// This has to be <b>solved</b> rather than tuned, because both of its inputs move. The field is
        /// sized per level, so a stand-off that frames one crops another off the top of the screen — which is
        /// exactly what the fixed number it replaces did. And the frustum is sized per display:
        /// <c>CreatePerspectiveFieldOfView</c> takes the <b>vertical</b> FOV, so a wider screen only adds
        /// width. That is the behaviour wanted — the field keeps its size on an ultrawide and the extra width
        /// goes to scenery — but it also means the horizontal fit is generous at 21:9 and tightest on the
        /// narrowest display, so both axes are checked.
        /// </para>
        /// </summary>
        private void FitGameCameraToLevel()
        {
            float halfX = (_map.StageSizeX + 1f) * Constants.HALF;
            float halfZ = (_map.StageSizeZ + 1f) * Constants.HALF;

            //The field in WORLD Y, which is the one place this differs from the Testbed's own solver: there
            //the lattice frame IS the world frame, while here level 0 sits at the cluster offset rather than
            //at zero. A deep level's empty growth levels are inside this on purpose — the cluster grows down
            //into them, so they have to be in frame before the first ball ever lands there.
            float bottomY = _clusterWorldOffset.Y;
            float topY = _ceilingY + Constants.HALF;   //upper face of the ceiling slab

            float verticalHalf = GAME_FOV * Constants.HALF * GAME_CAMERA_FIT_MARGIN;
            float horizontalHalf = MathF.Atan(MathF.Tan(GAME_FOV * Constants.HALF) * Camera.AspectRatio) * GAME_CAMERA_FIT_MARGIN;

            //Everything fits from far enough away and nothing does from close in, so the smallest distance
            //that fits can be bisected for. The near bound is the lens right behind the gun.
            float near = CannonOrbitRadius() + 2f;
            float far = 400f;

            for (int i = 0; i < 32; i++)
            {
                float middle = (near + far) * Constants.HALF;
                if (GameCameraFitsAt(middle, halfX, halfZ, bottomY, topY, verticalHalf, horizontalHalf, out _)) far = middle;
                else near = middle;
            }

            GameCameraFitsAt(far, halfX, halfZ, bottomY, topY, verticalHalf, horizontalHalf, out float axisElevation);

            _gameCameraDistance = far;
            _gameCameraTargetY = _cannon.Position.Y + CAMERA_HEIGHT + far * MathF.Tan(axisElevation);
        }

        /// <summary>
        /// Whether the field, its ceiling and the gun all land inside the frustum with the lens that far out,
        /// and at what elevation the view axis has to sit for it. Elevations are measured off the horizontal
        /// and bisected, which is what centres the subject between the top and bottom edges.
        /// </summary>
        private bool GameCameraFitsAt(float distance, float halfX, float halfZ, float bottomY, float topY,
            float verticalHalf, float horizontalHalf, out float axisElevation)
        {
            Vector3 back = GameCameraBearing();
            Vector3 camera = GameCameraPositionAt(distance);
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

                //On or behind the lens: there is no angle to measure, and the pose is rejected outright
                if (depth <= Constants.ONE) { ahead = false; return; }

                float elevation = MathF.Atan2(offset.Y, depth);
                minElevation = MathF.Min(minElevation, elevation);
                maxElevation = MathF.Max(maxElevation, elevation);
                maxSide = MathF.Max(maxSide, MathF.Atan2(MathF.Abs(Vector3.Dot(offset, right)), depth));
            }

            //The field's eight corners, from the floor of the play space to the top of the glass
            for (int cornerX = -1; cornerX <= 1; cornerX += 2)
                for (int cornerZ = -1; cornerZ <= 1; cornerZ += 2)
                {
                    Consider(new Vector3(cornerX * halfX, bottomY, cornerZ * halfZ));
                    Consider(new Vector3(cornerX * halfX, topY, cornerZ * halfZ));
                }

            //The gun, as a box around its trunnions large enough to hold the barrel at any aim, so the fit
            //does not change as the player elevates or traverses
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

        /// <summary>The gun's horizontal distance from the centre it orbits — its orbit radius.</summary>
        private float CannonOrbitRadius()
        {
            Vector3 offset = _cannon.Position - _cannon.OrbitCenter;
            offset.Y = 0f;

            return offset.Length();
        }

        #endregion

        /// <summary>
        /// The session's frame, run by the screen itself (#65's second half): the host is asked for the
        /// setting's slices, and the gun, the cluster, the shots and the glass go into the gaps between them.
        /// The ordering is the pipeline's and is load-bearing throughout — the balls over the opaque scene,
        /// the trails additively over them, the drain's glass compositing over both, the ceiling last of the
        /// session's objects because it is translucent, and the HUD and crosshair in display space after the
        /// one exit from linear light.
        /// </summary>
        public override void Draw(GameTime gameTime)
        {
            //The setting alone, not nothing at all: this screen does not draw what is under it (it draws the
            //setting itself), so the backdrop below is never reached and a bare return would leave the frame
            //with no target bound, no clear and no resolve — the back buffer as the last Present left it, with
            //the FPS line and a menu page composited onto it. It happens for exactly one frame whenever the
            //session is torn down from inside Update, which the result screen's "Main Menu" does when it is
            //pressed with the keyboard or the pad: TearDown is immediate while the stack's pop is deferred to
            //the next frame.
            if (!IsBuilt)
            {
                Game.DrawSetting();
                return;
            }

            //The occlusion ease and the attach glide are advanced on the draw clock, since both are purely
            //about what is on screen
            CollectBallInstances((float)gameTime.ElapsedGameTime.TotalSeconds);

            SceneFrame sceneFrame = Game.BeginSceneDraw();

            Game.CannonRenderer.Draw(Camera, CannonWorld(), Game.SceneEffectParams);

            DrawBallsInstanced();

            //Over the opaque scene (which the depth buffer now holds, so the cluster and the gun occlude
            //them) and additive, so they glow through the glare
            DrawShotTrails();

            Game.DrawSettingGlass();

            //The glass the cluster hangs from, last of the session's objects: it is translucent, so everything
            //it should be seen through has to be in the depth buffer and the frame already.
            //Squared, so the glass is unmistakable on the frame it steps and has thinned well before the slide
            //ends — it marks the event rather than colouring the plate for the duration
            Game.CeilingRenderer.EmissiveTint = CEILING_FLASH_COLOR * (_ceilingFlash * _ceilingFlash);

            Game.CeilingRenderer.Draw(Camera, _ceiling.World, Game.SceneEffectParams);

            Game.FinishSceneDraw(sceneFrame);

            //Display space from here down: the resolve is the frame's one and only exit from linear light,
            //and the crosshair and the FPS overlay (a component, drawn in base.Draw after this) are sRGB.
            //Not once the level is over. The result screen states the score itself, so the in-play readout
            //behind it is the same figure said twice — and worse, the corners are the only thing on the frame
            //that would still be pinned to the screen while the camera is released and swings out around the
            //arena. An award caught mid-flight is the visible half of that: it ages on the play clock, which
            //has stopped, so it would hang frozen and be reprojected against a moving camera, sliding across
            //the frame on its way to a score nobody is playing for any more.
            if (_pendingOutcome == LevelOutcome.None) _hud.Draw(_score, Camera);

            DrawCrosshair();
        }

        #region The crosshair

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

            SpriteBatch batch = Game.OverlayBatch;
            Texture2D pixel = Game.WhitePixel;

            batch.Begin();
            batch.Draw(pixel, new Rectangle(centreX - inner - length, centreY - half, length, thickness), color);
            batch.Draw(pixel, new Rectangle(centreX + inner, centreY - half, length, thickness), color);
            batch.Draw(pixel, new Rectangle(centreX - half, centreY - inner - length, thickness, length), color);
            batch.Draw(pixel, new Rectangle(centreX - half, centreY + inner, thickness, length), color);
            batch.End();
        }

        #endregion

        #region The balls in the frame

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

                        CollectBallInstance(ball, EaseOcclusion(ball, target, ease), glide, elapsed);
                    }

            //Indexed rather than foreach: these are List<T> on a per-frame path
            for (int i = 0; i < _shotBalls.Count; i++)
                CollectBallInstance(_shotBalls[i], EaseOcclusion(_shotBalls[i], PhysicsBall.UNOCCLUDED, ease), glide, elapsed);

            //The falling balls advance their ripple too: a group cut loose while the wave was passing through
            //it keeps glowing on its way down rather than snapping dark the instant it stops being cluster
            for (int i = 0; i < _fallingBalls.Count; i++)
                CollectBallInstance(_fallingBalls[i], EaseOcclusion(_fallingBalls[i], PhysicsBall.UNOCCLUDED, ease), glide, elapsed);

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
        private void CollectBallInstance(PhysicsBall ball, System.Numerics.Vector4 occlusion, float glide, float elapsed)
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

            CollectBallInstance(position, world, ball.Type, new Vector4(occlusion.X, occlusion.Y, occlusion.Z, occlusion.W),
                ripple: AdvanceRipple(ball, elapsed));
        }

        #region The ripple

        //A ball landing sends a wave of light out through the cluster: the balls touching the impact flare
        //first, then the ones touching those, and so on, each fading as the next takes over. It is what makes
        //the cluster read as a connected, living body rather than as a heap of independent spheres — the shot
        //does not just stick, the thing it stuck to answers.
        //
        //It travels by CONNECTIVITY and not by distance, which is the whole of why it looks right: a wave
        //evaluated from a world-space radius would cross the holes a played cluster is full of as if they were
        //not there, while a walk over balls that actually touch goes AROUND them — including around the hole
        //the matched group has just left, which is the most satisfying thing it does.

        //These three decide whether it reads as a WAVE or as a flash, and the ratio between them is the whole
        //of it: the lit band is as many balls wide as the flare's length divided by the hop delay. The first
        //build had a 0.36 s flare stepping every 0.045 s — a band nine balls deep against a reach of twelve,
        //which is very nearly the whole cluster alight at once, and on screen it read as the shot flashing the
        //lot rather than as anything travelling. Keep the band around three or four balls.

        /// <summary>Seconds between one ball flaring and the ones touching it taking their turn.</summary>
        private const float RIPPLE_HOP_SECONDS = 0.09f;

        /// <summary>How long one ball's flare lasts: a fast rise and a soft fall, so the band reads as a wave
        /// front with a tail rather than as a row of balls switching on and off.</summary>
        private const float RIPPLE_ATTACK_SECONDS = 0.05f;
        private const float RIPPLE_DECAY_SECONDS = 0.22f;

        /// <summary>
        /// How far the wave carries. Bounds the walk, and the flare's amplitude falls off across it so the
        /// ripple dies away instead of stopping at a hard ring of lit balls. Fourteen hops at the delay above
        /// is a bit over a second to cross a big cluster — long enough to watch it go, short enough that the
        /// next shot is not still waiting for it.
        /// </summary>
        private const int RIPPLE_MAX_HOPS = 14;

        //Hop count + 1 per cell, 0 meaning "not reached by this walk" — so it doubles as the visited mark and
        //needs only a clear between ripples rather than a second array. Reused rather than allocated per
        //landing, and sized to the field the level actually loaded.
        private int[,,] _rippleHops;
        private readonly Queue<XZLevel> _rippleQueue = new();

        /// <summary>
        /// Sends the wave out from the cell a ball has just landed in. A breadth-first walk over the balls that
        /// touch, so a ball's hop count is how many balls the light has to pass through to reach it — which is
        /// exactly the delay before it lights.
        /// <para>
        /// The origin cell seeds the walk whether or not a ball is still standing in it: a shot that completed
        /// a group is released along with it, so by the time this runs the cell it landed in is often empty and
        /// the wave has to start from the balls around the gap.
        /// </para>
        /// </summary>
        private void StartRipple(XZLevel origin)
        {
            if (!BeginRippleWalk(out XZLevel size)) return;

            _rippleHops[origin.X, origin.Z, origin.Level] = 1;      //reached, at hop 0
            _rippleQueue.Enqueue(origin);

            //The ball that landed, if it is still there, flares first and on its own — it is hop 0
            LightBall(_physicsBalls[origin.X, origin.Z, origin.Level], 0, alarm: false);

            WalkRipple(size, alarm: false);
        }

        /// <summary>
        /// The other wave, and the other thing the cluster has to say: the glass has just stepped down. It is
        /// seeded from <b>every ball hanging on the top level at once</b> and runs downwards, so it reads as a
        /// shock delivered by the ceiling to the whole cluster rather than as something that happened at a
        /// point — which is exactly what a descent is.
        /// <para>
        /// Red, and the ball's own colour has no say in it: the point is that every ball in the wave says the
        /// same thing. See <see cref="LightBall"/> for how the two waves share one channel.
        /// </para>
        /// </summary>
        private void StartCeilingRipple()
        {
            if (!BeginRippleWalk(out XZLevel size)) return;

            //Downwards from the top: the topmost occupied level is where the cluster meets the glass, and the
            //walk only ever moves outwards from there, so the wave travels down the way the push does
            for (int level = size.Level - 1; level >= 0; level--)
            {
                bool any = false;

                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        PhysicsBall ball = _physicsBalls[x, z, level];
                        if (ball == null) continue;

                        _rippleHops[x, z, level] = 1;
                        _rippleQueue.Enqueue(new XZLevel(x, z, level));

                        LightBall(ball, 0, alarm: true);
                        any = true;
                    }

                if (any) break;     //the first level with anything on it is the one the glass is pressing
            }

            WalkRipple(size, alarm: true);
        }

        /// <summary>Clears the walk's scratch state and sizes it to the field. False when there is no cluster.</summary>
        private bool BeginRippleWalk(out XZLevel size)
        {
            size = default;
            if (_physicsBalls == null) return false;

            size = XZLevel.FromArray(_physicsBalls);

            if (_rippleHops == null || _rippleHops.GetLength(0) != size.X
                || _rippleHops.GetLength(1) != size.Z || _rippleHops.GetLength(2) != size.Level)
                _rippleHops = new int[size.X, size.Z, size.Level];
            else Array.Clear(_rippleHops);

            _rippleQueue.Clear();

            return true;
        }

        private void WalkRipple(XZLevel size, bool alarm)
        {
            while (_rippleQueue.Count > 0)
            {
                XZLevel cell = _rippleQueue.Dequeue();
                int hops = _rippleHops[cell.X, cell.Z, cell.Level] - 1;

                if (hops >= RIPPLE_MAX_HOPS) continue;

                //The allocating enumerator, deliberately: this runs once per landing, not once per ball per
                //frame, which is the case CountOccupiedNeighbors exists to keep clear of it
                foreach (XZLevel next in BallsMap.GetNeighboringCells(cell, size))
                {
                    if (_rippleHops[next.X, next.Z, next.Level] != 0) continue;

                    //An empty cell stops the wave rather than passing it on — that is what makes it travel
                    //through the balls. It is left unmarked, so it costs a re-test from each of its own
                    //neighbours and nothing else.
                    PhysicsBall ball = _physicsBalls[next.X, next.Z, next.Level];
                    if (ball == null) continue;

                    _rippleHops[next.X, next.Z, next.Level] = hops + 2;
                    _rippleQueue.Enqueue(next);

                    LightBall(ball, hops + 1, alarm);
                }
            }
        }

        /// <summary>
        /// Arms one ball's flare: a countdown to its turn, and how bright it will be when it comes. A ball the
        /// wave reaches again while it is still lit simply takes the newer wave — the nearest impact wins,
        /// which is what a burst of quick shots should look like.
        /// </summary>
        private static void LightBall(PhysicsBall ball, int hops, bool alarm)
        {
            if (ball == null) return;

            ball.RippleTime = -hops * RIPPLE_HOP_SECONDS;

            //Squared falloff over the walk's reach, not linear. The far balls still take part — a wave that
            //reached them at full strength and then stopped dead would put a bright ring around nothing — but
            //the COUNT of balls at a given hop grows as its square in a packed lattice, so a linear falloff
            //leaves hundreds of them near full brightness a few hops out, which is what flooded the glare.
            float reach = hops / (float)RIPPLE_MAX_HOPS;
            float amplitude = (1f - reach) * (1f - reach);

            //The SIGN carries which of the two waves this is — the landing's own light, or the ceiling's
            //alarm — so one per-instance float says both how bright and what colour, the way Dissolve encodes
            //its two directions in one. A ball can only be in one wave at a time, which it already could not
            //be: the newest to reach it takes it over.
            ball.RippleAmplitude = alarm ? -amplitude : amplitude;
        }

        /// <summary>
        /// Advances one ball's flare and returns how brightly it is burning this frame. Called from the one
        /// place that visits every ball exactly once a frame, like the occlusion ease and the attach glide, and
        /// for the same reason: it advances state on the ball itself.
        /// </summary>
        private static float AdvanceRipple(PhysicsBall ball, float elapsed)
        {
            //Zero is at rest; the sign is which wave this is, so it is the magnitude that says whether one is
            //running at all
            if (ball.RippleAmplitude == 0f) return 0f;

            ball.RippleTime += elapsed;

            //Still on its way here — the countdown has not run out
            if (ball.RippleTime < 0f) return 0f;

            if (ball.RippleTime >= RIPPLE_ATTACK_SECONDS + RIPPLE_DECAY_SECONDS)
            {
                //Done. Cleared rather than left to drift, so a resting ball costs one comparison a frame and
                //the float cannot accumulate over a long level.
                ball.RippleAmplitude = 0f;
                return 0f;
            }

            if (ball.RippleTime < RIPPLE_ATTACK_SECONDS)
                return ball.RippleAmplitude * (ball.RippleTime / RIPPLE_ATTACK_SECONDS);

            //Squared on the way down: the flare drops away quickly and then trails, which is what leaves a tail
            //behind the front instead of a hard band with an edge at each end
            float fade = 1f - (ball.RippleTime - RIPPLE_ATTACK_SECONDS) / RIPPLE_DECAY_SECONDS;

            return ball.RippleAmplitude * fade * fade;
        }

        #endregion

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

                //A ball whose colour was eliminated from the cluster is re-coloured where it sits, and the two
                //colours cross-fade by dithering against each other: the new one arrives (negative) while the
                //old one goes (positive), and the two cuts are exact complements, so every pixel of the sphere
                //is written by exactly one of the two draws. Both stay in the opaque path — no sorting, no
                //muddy overlap. A settled ball is a single draw at zero, which clips nothing.
                //
                //_magazineTransmute counts DOWN from 1 (just swapped) to 0 (settled), so the dissolve's own
                //progress is its complement. Feeding the countdown straight in runs the effect backwards: the
                //new colour arrives complete on the frame of the swap and the old one is never seen at all.
                float remaining = _magazineTransmute[i];

                if (remaining > 0f)
                {
                    float progress = 1f - remaining;

                    CollectBallInstance(position, world, _magazine[i], new Vector4(0f, 0f, 0f, 1f), -progress);
                    CollectBallInstance(position, world, _magazineFrom[i], new Vector4(0f, 0f, 0f, 1f), progress);
                }
                else CollectBallInstance(position, world, _magazine[i], new Vector4(0f, 0f, 0f, 1f));
            }
        }

        /// <param name="dissolve">
        /// Zero for every ball but one caught mid-transmute — see <see cref="ModelInstance.Dissolve"/>.
        /// </param>
        private void CollectBallInstance(Vector3 position, Matrix world, BallType type, Vector4 occlusion,
            float dissolve = 0f, float ripple = 0f)
        {
            int typeIndex = (int)type - 1;
            if (typeIndex < 0 || typeIndex >= BS3DGame.BALL_TYPE_COUNT) return;

            float distance = Vector3.Distance(position, Camera.Position);
            int lod = 0;
            while (lod < BS3DGame.BALL_LOD_DISTANCES.Length && distance > BS3DGame.BALL_LOD_DISTANCES[lod]) lod++;

            int bucketIndex = typeIndex * BS3DGame.BALL_LOD_COUNT + lod;
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

            bucket[count] = new ModelInstance(world, occlusion, dissolve, ripple);
            _ballInstanceCounts[bucketIndex] = count + 1;
        }

        private void DrawBallsInstanced()
        {
            for (int lod = 0; lod < BS3DGame.BALL_LOD_COUNT; lod++) Game.BallRenderers[lod].PulseTime = WallClock;

            for (int typeIndex = 0; typeIndex < BS3DGame.BALL_TYPE_COUNT; typeIndex++)
                for (int lod = 0; lod < BS3DGame.BALL_LOD_COUNT; lod++)
                {
                    int bucketIndex = typeIndex * BS3DGame.BALL_LOD_COUNT + lod;
                    int count = _ballInstanceCounts[bucketIndex];
                    if (count == 0) continue;

                    BallType type = (BallType)(typeIndex + 1);

                    Game.BallRenderers[lod].Draw(Camera, _ballInstances[bucketIndex], count,
                        BasicEffectParamsProvider.GetEffectByType(type),
                        BasicEffectParamsProvider.GetDiffuseTintByType(type));
                }
        }

        #endregion

        #region The launch smears

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

            _trailViewParam.SetValue(Camera.View);
            _trailProjectionParam.SetValue(Camera.Projection);
            _trailCameraPositionParam.SetValue(Camera.Position);

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

            _shotTrailEffect = Game.Content.Load<Effect>("Shaders/ShotTrail");

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
    }
}
