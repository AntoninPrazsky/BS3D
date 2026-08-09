using BepuPhysics;
using BepuPhysics.Collidables;
using BS3D.Effects;
using BS3D.Physics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Physics;
using Prazsky.BS3D.Scoring;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Prazsky.Core.Screens;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

//BepuUtilities is deliberately NOT imported: it carries its own Matrix and MathHelper, which would make every
//existing use of the XNA ones in this file ambiguous. Nothing here needs a type out of it any more — the
//worker pool that was the one such type is PhysicsWorld's since #76. Bepu's own vectors are System.Numerics
//and every crossing is a named call — MonoGame's own ToNumerics outbound, Prazsky.Core.Tools' matching ToXna
//inbound (see CLAUDE.md, "Conventions", and that extension's remarks on why not the implicit conversion).

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
    /// <para>
    /// <b>Partial across nine files</b> since #72. This one holds the state and the two overrides that order
    /// it: every field, the constructor, <see cref="CoveredChanged"/>, <see cref="Update"/> and
    /// <see cref="Draw"/>. <b>No state moved</b> — the fields stay here on purpose, because which partial
    /// "owns" a field is a claim the code cannot enforce and several are read by four of them.
    /// <c>.Session.cs</c> is a session's lifetime, <c>.Physics.cs</c> the Bepu world and its fixed step,
    /// <c>.Ceiling.cs</c> the descending plate's state machine, <c>.Rules.cs</c> the level's rules and its end,
    /// <c>.Input.cs</c> the player's doing, <c>.Camera.cs</c> the lens and the gun's stance, <c>.Draw.cs</c>
    /// the balls in the frame, <c>.Ripple.cs</c> the wave a landing sends through the cluster.
    /// <see cref="Update"/> stays here for the same reason <c>BS3DGame.LoadContent</c> does: it is an ordered
    /// script whose order <i>is</i> the behaviour — the step, then the landings it produced, then the
    /// cinematic gate, then the clear, then the loss, then the tear-down — and this is the one place that can
    /// be read end to end. The extractions the split stages are named on the partials that hold them; the
    /// ripple is the nearest.
    /// </para>
    /// </summary>
    internal sealed partial class GameplayScreen : Screen
    {
        private readonly BS3DGame Game;

        //Forwarders for what the frame borrows from the host every few lines, so the session's own code reads
        //undisturbed: the one camera (the menus orbit it, this screen poses it), the wall clock everything
        //alive runs off, and the device.
        private RecoilCamera Camera => Game.Camera;
        private float WallClock => Game.WallClock;
        private GraphicsDevice GraphicsDevice => Game.GraphicsDevice;

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

        //How far back the camera stands and how high it aims. Both are SOLVED per level and per display
        //(FitCannonAndGameCameraToLevel, over GameCameraFit.Solve) rather than tuned, because both of their
        //inputs move underneath a fixed number: the field is sized per level, and the frustum per display.
        //These defaults only cover the frames before the first level is installed, and nothing seeds itself
        //off them — the solve seeds round one off where the gun already stands.
        //
        //Every dial of the fit is GameCameraFit's, shared with the Testbed since #76: the lens's drop below
        //the trunnions, the fraction of the frustum it may fill, the gun's stand-off in front of the lens and
        //the two lower bounds that override it (the field's footprint and the steepest resting aim). Their
        //whys are on those constants.
        private float _gameCameraDistance = 34f;
        private float _gameCameraTargetY = 3.5f;

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
        //
        //The lean itself is PreciseAim's, shared with the Testbed since #76: every dial (the set-back behind
        //the muzzle, the lift over the bore, the narrower lens, the island floor under it, the convergence
        //clamp and the ease), the one reversible blend and the held-button read. It hands this frame's pose
        //back as a VALUE and touches no camera, which is what lets the drop cinematic go on lerping over the
        //top of it — see UpdateCamera.
        //
        //What stays here is the held flag and its gates: the window's focus, a running cinematic and a screen
        //pushed over this one all clear it, and PreciseAim.Step is called on unheld frames too, which is how
        //the lean eases out rather than dropping.
        private readonly PreciseAim _preciseAim = new();
        private bool _adsHeld;

        #endregion

        #region The in-play HUD (display space, after the resolve)

        /// <summary>
        /// Score, streak, balls left and the awards flying into the corner. Its own class — see
        /// <see cref="PlayHud"/> for why, and for the whole of how it looks and moves; this screen only feeds it
        /// the events it animates and gives it the frame to draw into.
        /// </summary>
        private readonly PlayHud _hud;

        //The crosshair: four bars around a clear centre, struck from a white texel it makes itself. Its own
        //component since #76 — the bars, their size on the screen, the premultiplied white they are drawn in
        //and the skip below a hundredth of an opacity are all Crosshair's, and the Testbed draws the same one
        //now instead of loading a bitmap. Session-owned, like the laser net: it is made with the device up
        //and released with the session's own resources, and the host's white texel that existed only to feed
        //it went with the hoist.
        //
        //What stays here is when it is shown at all — only as precise aim leans in, because only then does
        //the lens look along the shot; in the overview a screen-centre mark would point at nothing in
        //particular — which is the blend handed to Draw.
        private readonly Crosshair _crosshair;

        #endregion

        #region Physics

        //BepuPhysics 2. The cluster is real: bodies held to each other and to the ceiling by BallSocket
        //constraints, a shot is a body thrown at it, and the island's drain is a collision mesh balls run
        //down. All of it comes from Prazsky.BS3D.Physics, which the Testbed uses unchanged — the hardware
        //itself (the worker pool, the buffer pool, the simulation, the contact stream, the order they are
        //built and torn down in and the mandatory order inside one step) is PhysicsWorld's since #76. What
        //stays here is the stepping POLICY below, which is deliberately not the Testbed's.
        private PhysicsWorld _world;
        private BallContactEventHandler _eventHandler;

        //Each step's contact work, handed to PhysicsWorld.Step as the work that belongs INSIDE the step. Built
        //once and held rather than written at the call site: a lambda allocates a fresh delegate every time it
        //is evaluated, and this one is evaluated up to PHYSICS_MAX_STEPS_PER_FRAME times a frame. It reads
        //_eventHandler out of the field rather than binding the instance the field happens to hold, so it
        //survives the handler being rebuilt per level. Assigned in the constructor because an instance field
        //initializer may not reference another instance member (CS0236).
        private readonly Action _processContacts;

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
        /// or fallen off the island's edge into the city. Well under <see cref="ArenaIsland.FUNNEL_BOTTOM_Y"/>, so
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
        private readonly int[] _ballsOfType = new int[BallRenderSet.TYPE_COUNT];

        /// <summary>
        /// The biggest single release of <b>this</b> level so far, which is the bar the drop cinematic has to
        /// clear (see <see cref="DropCinematic.MustBeatBestBy"/>). Per level and cleared by
        /// <see cref="BuildLevel"/>: carried across, a small level played after Crown would never show one,
        /// and "big" has to mean big <i>here</i> rather than big in the campaign.
        /// </summary>
        private int _biggestDrop;

        /// <summary>
        /// The lowest occupied level the tall level was authored with — the height its underside is <b>fed
        /// back down to</b> as the player clears it. See <see cref="FeedTallColumn"/>.
        /// </summary>
        private byte _feedFloorLevel;

        /// <summary>How many descents the feed has already asked for, so it never asks for the same one twice.</summary>
        private int _feedStepsQueued;

        /// <summary>
        /// How many of the steps still waiting in <c>_ceilingStepsPending</c> were asked for by the feed
        /// rather than by the shot count — which is what decides whether the glass flashes red or blue when
        /// one of them comes down. A count and not a flag: the two kinds can be queued together (a landing
        /// that both spends a shot and clears a band), and they come down one at a time.
        /// </summary>
        private int _ceilingFeedStepsQueued;

        /// <summary>
        /// Where the lattice frame meets the world, and the <b>only</b> place it does on the drawing side.
        /// <para>
        /// Y hangs the top of the field at <see cref="FIELD_TOP_Y"/> — or higher, when the field is deep
        /// enough that its bottom level would otherwise start past the death line (see
        /// <see cref="FIELD_FLOOR_MARGIN"/>): a cell's height is its level index over √2, so without an
        /// offset every map would hang at its own depth rather than in one frame.
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
        /// The height the field's topmost level hangs at — for every field shallow enough that hanging it
        /// here keeps its bottom level clear of the death line; a deeper one is raised past it (see
        /// <see cref="FIELD_FLOOR_MARGIN"/>). It is where the previous hard-coded field put its top
        /// ((16-1)/√2 − 7/√2 = 8/√2), kept exactly so the camera, the gun and the ceiling frame a loaded
        /// level the way they framed that one.
        /// <para>
        /// <b>Its own number, deliberately.</b> It used to be written as
        /// <c>(FALLBACK_FIELD_LEVELS - 1 - FALLBACK_EXTRA_LEVELS) / √2</c>, which arrives at the same 8 and made
        /// the height of <i>every</i> loaded level a function of the built-in fallback pyramid's size — widen
        /// that pyramid (its level count is <see cref="FALLBACK_X"/>) or change how many empty levels it carries
        /// and every real level's cluster would silently move, along with the camera, the gun's orbit and the
        /// ceiling fitted to it. The fallback is one map among many; this is the frame they are all hung in.
        /// </para>
        /// </summary>
        private const int FIELD_TOP_LEVELS = 8;

        /// <inheritdoc cref="FIELD_TOP_LEVELS"/>
        private static readonly float FIELD_TOP_Y = FIELD_TOP_LEVELS / Constants.SQRT_TWO;

        /// <summary>
        /// The tallest slice of field the camera will frame, in levels. A field this deep or shallower is
        /// framed <b>whole</b>, exactly as it always was; a deeper one is framed from its floor up to here
        /// and the rest of it — the cluster's upper reaches and the glass plate hanging over them — is simply
        /// above the frame.
        /// <para>
        /// It exists so a level can be <b>tall</b>. Without it <see cref="GameCameraFit"/> does the only
        /// honest thing with a taller field and stands further back to fit it, which shrinks the balls until
        /// a forty-level column is a smudge and the gun a speck: a level three times as deep is a level
        /// rendered a third the size, and the depth buys nothing. Capped, the depth buys exactly what it
        /// should — a column that reaches up out of shot and comes down as the glass descends, so the level's
        /// length is its height and not its footprint.
        /// </para>
        /// <para>
        /// <b>Sixteen because that is the deepest field that is framed whole today</b> (see
        /// <see cref="FIELD_FLOOR_MARGIN"/>: the two branches of the hang meet at a 16-level field). So this
        /// cap changes nothing about any level that exists — One, Two and the whole pattern pack solve
        /// bit-identically — and the window a tall level is played through is the very frame every other
        /// level is played in, rather than a second set of camera figures to tune.
        /// </para>
        /// </summary>
        private const int FRAMED_LEVELS = 16;

        /// <summary>
        /// How many levels above a tall column's <b>underside</b> the gun may be aimed — the working band,
        /// and the dial that decides whether a tall level is a climb or a formality.
        /// <para>
        /// It first reached the top of the framed window (sixteen levels) and that made the height
        /// decorative: a band cut high in the window orphans the whole visible column beneath it, so the
        /// tallest level in the game could be taken in two or three shots. Held to a band just above the
        /// underside, the column has to be eaten from the bottom and <see cref="FeedTallColumn"/> — which
        /// keeps that underside at the height the level hung it — is what hands over the next of it.
        /// </para>
        /// <para>
        /// <b>Five, tried at three.</b> Three was the first tightening and it was measured too tight in play:
        /// it holds the aim to very nearly the underside itself, which reads as the gun being unable to look
        /// up rather than as the column being tall. Two more levels give the shot somewhere to go without
        /// putting a whole band's worth of orphaning back in reach.
        /// </para>
        /// </summary>
        private const int TALL_AIM_HEADROOM_LEVELS = 5;

        /// <summary>
        /// A little over the steepest shot a tall level's working band actually needs, in radians (~3°), so
        /// the player is not fighting the clamp on the very cell the limit was solved for. Small on purpose:
        /// every degree of it is a degree further up the unseen column.
        /// </summary>
        private const float TALL_AIM_MARGIN = 0.05f;

        /// <summary>Whether this level is deeper than the camera frames — a tall, descending one.</summary>
        private bool FieldIsTallerThanFrame => _map != null && _map.Levels > FRAMED_LEVELS;

        /// <summary>
        /// The least the field's <b>bottom</b> level clears the death line by, and what raises a deep field
        /// past <see cref="FIELD_TOP_Y"/>: a ball's radius, so a ball hung in the field's lowest cell rests
        /// its surface exactly on the line — alive, one descent from loss. It makes the whole field playable
        /// by construction, however deep it is, and turns the empty levels an author leaves under a layout
        /// into the level's starting clearance instead of dead space past the line: pinning the top
        /// unconditionally meant any field 17 or more levels deep (its bottom more than ~15.8 level-steps
        /// below its top) <i>started</i> with cells below the line, and a 30-level map was lost on the frame
        /// it was built, before a shot was fired (a ball at −6.36 against the −5.5 line).
        /// <para>
        /// A ball's radius and not more, so that every field that fits under <see cref="FIELD_TOP_Y"/>
        /// hangs exactly where it always has: the two branches of the max meet at a top level of ~15.07,
        /// so a field up to 16 levels deep — the fallback's own depth — is pinned unchanged and a 17-level
        /// one is the first raised. Measured: the fallback and <c>One.json</c> (15 levels) place and solve
        /// bit-identically to before the rule; <c>Two.json</c>, whose 18 levels were already reaching 0.86
        /// past the line, hangs 1.36 higher — its ceiling pressure was nominal either way (~13 descents to
        /// lose before, ~15 after, against a budget that allows 11).
        /// </para>
        /// </summary>
        private const float FIELD_FLOOR_MARGIN = Constants.HALF;

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

        //Reusable backing array for the HUD's cluster profile: one entry per ball the frame could draw, filled
        //from the live poses in Draw and handed to the HUD as a span. Sized to the field's cell count and kept
        //across frames — the cluster profile is per-frame, but the array it fills is not. No per-frame allocation.
        private PlayHud.BallMarker[] _profileBalls;

        //The cluster profile's horizontal axis is the GAMEPLAY camera's right vector — the lens the player aims
        //with, not whatever a drop cinematic has swung the lens to. A cinematic blends the camera away from the
        //overview pose (UpdateCamera Lerps towards _cinematic.Position/Target), and the profile drawn from that
        //swung lens would turn the cluster's outline as the shot played out, which reads as the HUD shaking
        //rather than as the cluster turning. So this holds the right vector of the pose BEFORE the cinematic blend,
        //updated every frame, and the profile reads it instead of the live camera. See BuildClusterProfile.
        private Vector3 _gameplayCameraRight = Vector3.Right;

        //Where the glass hangs: CeilingPlate.CentreYAbove the field's top level — the plate's own clearance
        //(CeilingPlate.CLEARANCE, which carries the note about the cluster coming to rest a unit under the
        //plate rather than settling on its lattice) applied to the base this session picks. The kinematic body
        //and the drawn glass box both sit here, and the box is drawn straight from the body's pose (see
        //KinematicBody), so the collidable and the thing the player sees cannot drift apart.
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
        //with almost nothing in the other two channels: this is the game's one alarm COLOUR — the ceiling
        //flash and the floor net both take it (LaserGrid is handed this very constant), so the two read as
        //one warning at two heights — and it should not be mistakable for anything the scene does on its own.
        //Far over 1, and it has to be: the plate is 35 % opaque, so most of what is seen where the glass is is
        //the sky BEHIND it. At 1.5 the red merely tinted that blue-white and the plate came out pink. The
        //emissive is added on top of the composite, so it is the number that has to out-shout the sky.
        private static readonly Vector3 CEILING_FLASH_COLOR = new(6f, 0.15f, 0.1f);

        /// <summary>
        /// What the glass says instead when the descent is a <b>feed</b> — a tall level handing the player
        /// more of its column because they have just cleared a lot of it. Cold blue-white rather than red,
        /// and the reason is the whole point of separating them: nothing has gone wrong. The player has
        /// played well and the game is answering; a red flash there tells them off for it.
        /// <para>
        /// Balanced the same way <see cref="CEILING_FLASH_COLOR"/> is and for the same reason — the plate is
        /// 35 % opaque and the emissive is added over the sky behind it — so this is bright enough to read as
        /// the glass lighting up rather than as the sky changing.
        /// </para>
        /// </summary>
        /// <para>
        /// <b>Deep blue and not a bright one.</b> The first pairing carried enough green (2.2 against the
        /// blue's 6) to come out cyan-white over a lit sky, which reads as the glass being <i>blown out</i>
        /// rather than as it saying something — the same washing the red's own doc warns about from the other
        /// end. Nearly all the green is gone, and the blue keeps the level it needs to be seen through a
        /// 35 %-opaque plate.
        /// </para>
        private static readonly Vector3 CEILING_FEED_COLOR = new(0.04f, 0.5f, 5f);

        /// <summary>The flat colour the cluster's ripple carries, as opposed to the plate's own emissive.</summary>
        private static readonly Vector3 RIPPLE_ALARM_COLOR = new(1f, 0.07f, 0.05f);

        /// <inheritdoc cref="CEILING_FEED_COLOR"/>
        private static readonly Vector3 RIPPLE_FEED_COLOR = new(0.02f, 0.1f, 1f);

        /// <summary>
        /// Which of the two the glass and the wave are currently saying. Set the instant a descent is started
        /// and read while it is on screen, because the flash and the ripple outlive the call that began them.
        /// </summary>
        private Vector3 _ceilingFlashColor = CEILING_FLASH_COLOR;

        /// <summary>
        /// Whether the flash currently on screen is a feed rather than pressure. The colour above is what the
        /// 3D plate needs; this is what the <b>HUD</b> needs, which picks its own display-space colour rather
        /// than converting a linear radiance — see <c>PlayHud.PROFILE_ALARM</c> for why.
        /// </summary>
        private bool _ceilingFlashIsFeed;

        //Where the glass body sits now (_ceilingY) and where it is sliding to (_ceilingTargetY). Equal while at
        //rest; _ceilingTargetY is lowered by StartCeilingDescent and _ceilingY catches up in UpdateCeilingDescent.
        private float _ceilingTargetY;

        //Ceiling steps that have come due but are waiting for their moment — see ReleaseCeilingStep. A count,
        //because a ceilingStep of 1 steps on every shot and two of them inside the hold must not lose one.
        private int _ceilingStepsPending;
        private float _ceilingStepHold;
        private float _ceilingStepWaited;

        //How long a step waits after the shot that earned it. Long enough for that shot to have landed and a
        //drop cinematic to have engaged if it is going to — the shot leaves at SHOOT_SPEED and lands in about
        //a tenth of a second, so this is generous — and short enough that on an ordinary shot the descent
        //still reads as the answer to firing.
        private const float CEILING_STEP_HOLD = 0.45f;
        private bool _ceilingDescending;

        //The floor alarm — the descending ceiling's warning at the other end of the field. The net itself
        //(geometry, pulse, fades, the linger after the level ends) lives in LaserGrid; this screen owns the
        //trigger: within LASER_WARN_STEPS more descents of the death line, measured on the same live poses
        //the loss itself is decided on (see UpdateLaserWarning). The hysteresis keeps a swaying cluster
        //from flickering it at the threshold — a shot shoves the structure, and a lowest ball bobbing a few
        //tenths of a unit across the exact line would arm and stand down the net with every swing.
        private const float LASER_WARN_STEPS = 2f;
        private const float LASER_WARN_HYSTERESIS = 0.3f;

        private readonly LaserGrid _laserGrid;

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

        //How long the victory display goes on launching, and how long it waits before it starts.
        //
        //A full minute, because the display is meant to still be going while the player reads their score and
        //the released camera swings around the arena — nine seconds ran out while the result screen was still
        //being read, and a celebration that stops before its audience does reads as the game losing interest.
        //Fireworks eases its own rhythm off after the opening so a minute is not a minute of barrage.
        private const float CELEBRATION_SECONDS = 60f;

        //And it hangs back, so the fanfare gets its opening statement to itself: the level ends, brass
        //announces it, and only then does the sky start going off. Everything at once is nothing heard.
        private const float CELEBRATION_DELAY = 2.2f;

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

        //The queue of loaded colours, its post-shot glide and where each loaded ball sits in the bore are all
        //Magazine's, shared with the Testbed since #76 — as are the two figures the barrel was cut to
        //(Magazine.SIZE and Magazine.SPACING, which CannonRig derives the tube's length and
        //CannonRig.PivotToFrontBall from). What stays here is this screen's own two rules: which colours may be
        //loaded at all (RandomBallType, injected below), and the cross-fade a re-coloured ball dissolves
        //through. Built in the constructor rather than initialized here, because its hooks write the two
        //arrays below.
        private readonly Magazine _magazine;

        /// <summary>
        /// The colour a loaded ball is dissolving <i>out of</i>, per slot, and how far through that it is
        /// (0 = settled, nothing to draw twice). A ball whose colour has just been eliminated from the cluster
        /// is re-coloured where it sits rather than left to be fired at nothing — see <see cref="Transmute"/>.
        /// <para>
        /// Both are drawn in step with the queue, so both must shift with it — and
        /// <see cref="Magazine.Advance"/> owns that shift, reporting every <c>(destination, source)</c> pair to
        /// the hook wired at construction. Shifting only the colours would leave a slot dissolving out of the
        /// ball behind it.
        /// </para>
        /// </summary>
        private readonly BallType[] _magazineFrom = new BallType[Magazine.SIZE];
        private readonly float[] _magazineTransmute = new float[Magazine.SIZE];

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

        private const float CANNON_ORBIT_RATE = 1.0f;

        //The walk's held rate, the same ±1 protocol as the orbit's — the speed itself is the shared
        //Cannon.ADVANCE_SPEED, so the walk feels the same in the Testbed
        private const float CANNON_ADVANCE_RATE = 1.0f;

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

        //The template a shot is stamped from — the sphere, its inertia, its bare shape index and its sleep
        //threshold — is PhysicsWorld's, which stamps a fresh copy per shot rather than holding one and writing
        //this shot's pose and velocity over the last one's, as this and the Testbed both used to.

        //The launch smears: the streak a shot leaves at the muzzle, anchored there and living its own short
        //life while it fades. LaunchSmears' since #76 — the billboard, the six dials, the colour rule and the
        //additive depth-read draw stood here and in the Testbed, value for value. What stays here is when one
        //is added (Shoot), that they age every frame this screen updates, and where the draw sits in the
        //frame, which the call site in Draw states.
        private readonly LaunchSmears _smears;

        private static readonly Random RANDOM = new();

        private MouseState _previousMouse;
        private bool _padTriggerReleased = true;

        //Aiming the gun from the captured cursor and the pad's right stick, both dials and all the arithmetic
        //shared with the Testbed since #76. It holds the "a captured frame has been seen" flag that gates both
        //the aim delta and the shot edge.
        private readonly MouseAim _mouseAim = new();

        /// <summary>
        /// Where a shot fired right now would land, answered every frame by the very function that will place it
        /// (<see cref="ShotPlacement"/>) — see "The landing preview" in <c>docs/game-session.md</c>.
        /// <para>
        /// It exists because the field is a <b>box</b>, and a shot at a full pocket in its wall sticks nowhere: the
        /// ball bounces off, falls through the drain and costs the player a ball from the budget <i>and</i> their
        /// streak, for a refusal they had no way to see coming (#70). Growing the field instead was considered and
        /// rejected — there is always an edge somewhere, so the answer is to make the edge legible rather than to
        /// move it.
        /// </para>
        /// </summary>
        private XZLevel _previewCell;

        /// <summary>Whether <see cref="_previewCell"/> holds a cell — a shot fired now sticks, and there is a ghost to draw.</summary>
        private bool _previewHasCell;

        /// <summary>
        /// How far the cluster hangs off its lattice where <see cref="_previewCell"/> is — measured on the very ball
        /// the aim reaches, and handed back by the same call that answers the cell.
        /// <para>
        /// It is what makes the ghost obey the <b>ceiling</b>. A cell is an index into a lattice this session hung
        /// once (<see cref="_clusterWorldOffset"/>); the balls are wherever the descending glass has since dragged
        /// them, <see cref="CEILING_DESCENT_PER_STEP"/> per step, plus the stretch the structure hangs with and
        /// whatever the last shot set swaying. None of that is in the lattice, and a ghost placed from the lattice
        /// alone drifted out of the cluster as a level went on. It also covers the sway and the resting stretch,
        /// because all three are the same quantity — see <see cref="ShotPlacement.CellWorldPosition"/>.
        /// </para>
        /// </summary>
        private Vector3 _previewDrift;

        /// <summary>
        /// Whether the aim reaches the cluster at all. Only when it does <b>and</b> <see cref="_previewHasCell"/> is
        /// false is a refusal <i>certain</i>, which is what the reddened crosshair says.
        /// <para>
        /// <b>The ghost is the signal; the crosshair only confirms it.</b> The ghost is drawn exactly when a shot
        /// sticks, so its absence covers every way one does not — a full pocket, open sky, or the glass by a path
        /// this preview deliberately does not walk — with no case where it says the wrong thing. The crosshair is
        /// the explicit version and is only drawn while precise aim is leaning in, because that is the only mode
        /// where the screen's centre is where the gun points: in the overview the lens looks <i>at</i> the cluster
        /// rather than along the bore, so a mark in the middle of the screen there would name a spot the barrel is
        /// not aimed at. Hence the overview gets the ghost alone, which is in the right place by construction.
        /// </para>
        /// </summary>
        private bool _previewReachesCluster;

        /// <summary>
        /// The dashed line of light out of the muzzle, and the two ends it is drawn between. It exists for the
        /// <b>overview</b>, where there is no crosshair and the barrel's foreshortened angle was the only clue to
        /// where the gun pointed — and it is what carries the refusal to that mode, since a red beam ending on the
        /// ball it cannot stick to says what the reddened crosshair says in precise aim.
        /// </summary>
        private readonly AimBeam _aimBeam;

        /// <summary>Where the beam starts. Stored rather than recomputed in <c>Draw</c>, so the line, the ghost and the shot cannot disagree about where the bore is.</summary>
        private Vector3 _previewMuzzle;

        /// <summary>Where it ends: the point a shot would touch, or a reach out along the aim when it would touch nothing.</summary>
        private Vector3 _previewBeamEnd;

        /// <summary>Whether there is a beam at all — false only before a session is standing and while a cinematic has the gun.</summary>
        private bool _previewBeamVisible;

        #endregion

        #region Ball instances

        //The one walk that turns a simulated cluster into ball instances, shared with the Testbed since #76:
        //every hanging ball, every shot in flight and every released ball on its way down, each read off its
        //own body's pose, shaded by what is packed around it and offset by whatever is left of its arrival
        //glide. Both eases (the occlusion's one-second time constant, the glide's 0.08 s) and the neighbour
        //occlusion's own figures are its own, and so is the rule the whole thing exists for: every ball is
        //visited exactly once a frame, because all three of those pieces of state live on the ball itself.
        //
        //The ripple hook is what this game adds to it and the Testbed does not — handed over ONCE here rather
        //than per frame, since a method group written at a per-frame call site builds a fresh delegate every
        //time it is evaluated.
        private readonly ClusterCollector _clusterCollector = new(AdvanceRipple);

        #endregion

        public GameplayScreen(BS3DGame game)
        {
            Game = game;
            _hud = new PlayHud(game);

            //Orbit centre is the field the cluster hangs over. No trunnion height goes in: the gun stands on
            //the island's dished stone, so its height is the carriage's own figure of its radius
            //(CannonRig.TrunnionHeightAt) and the pose re-seats it on every move — the wheels stay on the
            //stone wherever the walk stands.
            _cannon = new Cannon(new Vector3(0f, 5f, 0f), 20f);

            //The queue's colours are the level's business (RandomBallType draws only among what is still
            //hanging), so what to load next is injected; the constructor deals a full queue with it, which is
            //what gives the player something to read from the first frame. The two hooks carry this screen's
            //transmute state through every shift, so the three arrays stay drawn in step — wired once, here,
            //since a delegate built per shot would allocate one per round fired.
            _magazine = new Magazine(RandomBallType,
                (destination, source) =>
                {
                    _magazineFrom[destination] = _magazineFrom[source];
                    _magazineTransmute[destination] = _magazineTransmute[source];
                },
                (slot, type) =>
                {
                    //A ball dealt from what is alive has nothing to fade out of. This is also what keeps a
                    //level loaded over a session that was mid-transmute from inheriting its half-finished
                    //dissolves: Magazine.Refill fires it for every slot.
                    _magazineTransmute[slot] = 0f;
                    _magazineFrom[slot] = type;
                });

            //The work each physics step carries inside it, wired once here for the reason the field states
            _processContacts = () => _eventHandler.ProcessQueuedContacts();

            //The smears' billboard quad and every parameter handle their draw needs, in one construction. The
            //effect is the content manager's and is never disposed there.
            _smears = new LaunchSmears(GraphicsDevice, Game.Content.Load<Effect>("Shaders/ShotTrail"));

            //The aim beam borrows the SAME effect instance — it is the same billboard between two world points,
            //and a short segment of it comes out as a dash for free. Sharing it is why both components now push
            //the trail's two widths per draw instead of once; see AimBeam's remarks.
            _aimBeam = new AimBeam(GraphicsDevice, Game.Content.Load<Effect>("Shaders/ShotTrail"));

            //And the crosshair's own white texel, which the host used to hold for it
            _crosshair = new Crosshair(GraphicsDevice);

            //The floor alarm's net, loaded like the smears: session content, made here with the device up.
            //It is handed the ceiling flash's own red — the two are one warning at two heights, and passing
            //the constant is what keeps them from drifting apart.
            _laserGrid = new LaserGrid(GraphicsDevice, Game.Content.Load<Effect>("Shaders/LaserGrid"), CEILING_FLASH_COLOR);
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
        /// "released" and fire a shot nobody asked for. <see cref="MouseAim.Invalidate"/> skips that
        /// first frame's aim <i>and</i> its shot test, since both live behind it.
        /// </summary>
        public override void CoveredChanged()
        {
            _mouseAim.Invalidate();
            _adsHeld = false;

            //A gamepad reports to an unfocused window and to a paused one; both triggers must be released
            //before they mean anything again. One poll on a state change, which is not a per-frame path.
            _padTriggerReleased = false;
            Game.PreviousPad = GamePad.GetState(PlayerIndex.One);

            Game.IsMouseVisible = false;
        }

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

                //A pause takes effect at the top of the NEXT frame, because that is where ScreenManager applies
                //stack changes — so without stopping here the rest of THIS frame would go on running against a
                //session the player has just stopped (#79): the aim would take its delta and recentre the
                //cursor, a left-button edge landing on the same frame would fire a shot, the ceiling would
                //slide, the simulation would step, and a loss detected here would stack a result screen on top
                //of the pause page. Everything below tolerates the one-frame stall by construction — it is the
                //same stall every covered frame already gives it.
                //
                //Only the pause path reports true. UpdateInput's other early exits must NOT stop the frame:
                //they write PreviousKeyboard alone and rely on UpdateAim to write PreviousPad, whereas the
                //pause path writes both itself. That is what keeps the Escape edge that un-pauses from being
                //read a second time against a stale snapshot.
                if (UpdateInput(gameTime, Game.EdgeInputAllowed, pad)) return;

                UpdateAim(gameTime, Game.EdgeInputAllowed, pad);
            }
            else
            {
                //The cursor belongs to the desktop again as soon as the window is not the one being played:
                //hidden over an unfocused window it simply disappears wherever the player moves it.
                Game.IsMouseVisible = true;
                _mouseAim.Invalidate();

                //A trigger held while the window was away must be re-released before it fires
                _padTriggerReleased = false;

                //And a held precise-aim button must not keep an alt-tabbed window leaned in — the gamepad's
                //triggers report through XInput whether the window has focus or not. The blend below still
                //runs, so losing focus eases the lean out rather than dropping it.
                _adsHeld = false;
            }

            _cannon.Update(gameTime);

            //The queue glides forward into the slot the fired ball left rather than snapping. Wall clock, not
            //the simulation's step: balls sliding down a tube is the gun answering the shot.
            _magazine.Step(elapsed);

            //And a re-coloured ball dissolves out of its old colour. Linear, so it genuinely finishes rather
            //than leaving a slot for ever a few pixels short of its new colour.
            for (int i = 0; i < Magazine.SIZE; i++)
                if (_magazineTransmute[i] > 0f)
                    _magazineTransmute[i] = MathF.Max(0f, _magazineTransmute[i] - elapsed / TRANSMUTE_SECONDS);

            //The barrel slides home. Linear in the stroke, so it genuinely ends rather than approaching zero
            //forever and leaving the gun permanently a hair out of place.
            if (_cannonRecoil > 0f) _cannonRecoil = MathF.Max(0f, _cannonRecoil - CANNON_RECOIL_DECAY * elapsed);

            //The cinematic reads the balls where the last step left them and answers with this frame's pose and
            //time scale, so the scale is applied to the very step its own framing was chosen against.
            _cinematic.Update(elapsed, TryGetDropCentre(out Vector3 dropCentre), dropCentre);

            if (!_cinematic.Engaged) _cinematicSubject.Clear();

            //A step that came due while the shot was in the air waits here for its moment — see
            //ReleaseCeilingStep. Before the descent update, so a released step slides on the same frame.
            ReleaseCeilingStep(elapsed);

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
            //Only the ENDINGS are held while a cinematic runs — both put the result screen over this one, and
            //a level that ends mid-collapse takes the collapse the player earned off the screen before they
            //have seen it, which is the same reason the cleared countdown below waits. The floor alarm inside
            //is NOT held: the release that engages a cinematic is exactly the one that rescues a low cluster,
            //and a warning frozen lit would blaze across the player's reward for the whole dive down the drain.
            CheckLevelLost(mayLose: !_cinematic.Engaged);

            //Where a shot fired now would land. After the step, so the ghost sits against the poses the player is
            //looking at rather than the ones from before this frame's physics.
            UpdateShotPreview();

            _smears.Update(elapsed);
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

            //This frame's ball collection, opened here and closed by the one Draw below. It is a ref struct and
            //lives as a LOCAL, deliberately: BeginFrame is the only thing that can open one, it empties the
            //buckets on the way and it throws if a second is opened before the first is drawn — which is what
            //makes "every ball is visited exactly once" structural rather than a rule to remember. The
            //occlusion ease, the attach glide and the ripple all advance state on the ball itself, so a second
            //visit would run all three at double speed while the buckets still looked perfectly correct.
            BallDrawFrame ballFrame = Game.Balls.BeginFrame(Camera);

            //The cluster, the shots in flight and the released balls falling, each off its own body's pose — so
            //what the player is looking at IS the simulation. On the DRAW clock, since the ease, the glide and
            //the ripple are all purely about what is on screen.
            _clusterCollector.Collect(ballFrame, (float)gameTime.ElapsedGameTime.TotalSeconds,
                _physicsBalls, _shotBalls, _fallingBalls);

            //And the loaded queue into the very same frame — this screen's own loop, because the barrel's bore
            //and the transmute cross-fade are its own business
            CollectMagazineBalls(ballFrame);

            //And the ghost of where the loaded ball would land, into the same frame — it is a ball in the cluster's
            //own frame, so it is bucketed and LOD-picked with the rest rather than drawn by itself
            CollectShotPreview(ballFrame);

            SceneFrame sceneFrame = Game.BeginSceneDraw();

            //The barrel, drawn with its recoil stroke: the pose is Cannon's and the hardware CannonRig's, so
            //the tube that was built and the bore a shot leaves from cannot disagree. The carriage under it
            //deliberately takes no recoil — the tube slides in the cradle, the carriage holds its ground —
            //and its wheels are spun by the advance walk's own covered distance.
            //Into a local because the window's glazing is drawn with the very same pose further down — it is set
            //into this tube, so the one pose serves both rather than being built a second time from a second
            //read of the stroke, which is one more thing that could ever come out differently.
            Matrix barrelWorld = _cannon.BarrelWorld(CannonRecoilBack());

            Game.CannonRig.Draw(Camera, barrelWorld, Game.SceneEffectParams);
            Game.CannonRig.DrawCarriage(Camera, _cannon.CarriageWorld(), _cannon.AdvanceTravel, Game.SceneEffectParams);

            //Everything collected above, as one instanced draw per ball type and LOD level — and the frame's
            //collection is closed by it. The heartbeat runs on the WALL clock: the balls go on breathing while
            //a pause has the session frozen, because it is what they are and not something they are doing.
            Game.Balls.Draw(WallClock);

            //Over the opaque scene (which the depth buffer now holds, so the cluster and the gun occlude
            //them) and additive, so they glow through the glare. It puts back exactly the states it found,
            //so the frame's translucent baseline still stands for the glass below.
            _smears.Draw(Camera);

            //The aim beam in the smears' own slot and states — additive, depth-read — so the cluster and the gun
            //occlude it and it blooms through the glare with them. After the smears rather than before for one
            //reason: a shot's flare should sit over the guide that aimed it, not under it.
            DrawShotPreviewBeam();

            Game.DrawSettingGlass();

            //The floor alarm, in the trails' states but AFTER the drain's glass and BEFORE the ceiling's,
            //because the net lies between the two in Y and neither translucent draw can depth-test against
            //it (the net writes no depth): the funnel is entirely below the net, so a cinematic looking
            //down the throat has the net in front of the glass — drawn earlier, the cone would wrongly dim
            //it — while the descending plate is above, so an overhead shot seeing the net through the glass
            //still gets the plate composited over it. Wall-clock driven, so its pulse and its linger-and-
            //fade keep running while a result screen covers this screen and only draws still arrive.
            _laserGrid.Draw(Camera, WallClock);

            //The glass the cluster hangs from, last of the session's objects: it is translucent, so everything
            //it should be seen through has to be in the depth buffer and the frame already.
            //Squared, so the glass is unmistakable on the frame it steps and has thinned well before the slide
            //ends — it marks the event rather than colouring the plate for the duration
            Game.CeilingRenderer.EmissiveTint = _ceilingFlashColor * (_ceilingFlash * _ceilingFlash);

            Game.CeilingRenderer.Draw(Camera, _ceiling.World, Game.SceneEffectParams);

            //And the gun's own glass after all of them, because it is far and away the nearest translucent
            //surface in the frame: the loaded queue it covers is in the depth buffer by now, and so are the
            //drain's cone and the descending plate the barrel is seen against — composited first, this pane
            //would let both of those bleed through it. Drawn with the barrel's own pose, recoil and all.
            Game.CannonRig.DrawGlass(Camera, barrelWorld, Game.SceneEffectParams);

            Game.FinishSceneDraw(sceneFrame);

            //Display space from here down: the resolve is the frame's one and only exit from linear light,
            //and the crosshair and the FPS overlay (a component, drawn in base.Draw after this) are sRGB.
            //Not once the level is over. The result screen states the score itself, so the in-play readout
            //behind it is the same figure said twice — and worse, the corners are the only thing on the frame
            //that would still be pinned to the screen while the camera is released and swings out around the
            //arena. An award caught mid-flight is the visible half of that: it ages on the play clock, which
            //has stopped, so it would hang frozen and be reprojected against a moving camera, sliding across
            //the frame on its way to a score nobody is playing for any more.
            if (_pendingOutcome == LevelOutcome.None)
            {
                PlayHud.ClusterProfile profile = BuildClusterProfile(out int ballCount);
                _hud.Draw(_score, Camera, in profile,
                    new ReadOnlySpan<PlayHud.BallMarker>(_profileBalls, 0, ballCount));
            }

            //The crosshair, into the host's overlay batch (the one the HUD above just used): shown only while
            //precise aim is leaning in, that being the only pose whose lens looks along the shot, and faded up
            //with the lean rather than snapped on. The gate below a hundredth is the component's own.
            //Reddened only when the refusal is CERTAIN — the aim reaches a ball and neither ring around it has a
            //free cell. Aiming at open sky leaves it neutral, because a miss the player can already see does not
            //need a warning, and a warning that cries wolf is one nobody reads. Note the opacity is the ADS blend,
            //so this mark exists only while the lens looks along the bore; the overview's signal is the ghost.
            _crosshair.Draw(Game.OverlayBatch, _preciseAim.Blend,
                _previewReachesCluster && !_previewHasCell ? PREVIEW_REFUSED : null);
        }

    }
}
