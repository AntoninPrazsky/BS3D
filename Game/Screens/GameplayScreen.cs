using BepuPhysics;
using BepuPhysics.Collidables;
using BS3D.Effects;
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
    /// the player pauses on is simply this screen, still drawn. The result page is the one exception and it
    /// is a deliberate one — it lets this screen go on updating so the arena stays alive behind the numbers
    /// (#241); see <see cref="UpdateUnderResult"/> for the half of the frame that runs there.
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
    /// the balls in the frame. (<c>.Ripple.cs</c>, the wave a landing sends through the cluster, has left as a
    /// real object: it is <see cref="ClusterRipple"/> in <c>Prazsky.BS3D.Physics</c> since #582.)
    /// <see cref="Update"/> stays here for the same reason <c>BS3DGame.LoadContent</c> does: it is an ordered
    /// script whose order <i>is</i> the behaviour — the step, then the landings it produced, then the
    /// cinematic gate, then the clear, then the loss, then the tear-down — and this is the one place that can
    /// be read end to end. The extractions the split stages are named on the partials that hold them.
    /// </para>
    /// </summary>
    internal sealed partial class GameplayScreen : Screen, IFrameBlurSource
    {
        private readonly BS3DGame Game;

        /// <summary>
        /// The command line's testing levers for play (#582) — forced streak, wildcard cadence, power-ups, the
        /// laser net, a staged line loss, scheduled detonations, the tutorial mode, a ball-style override, a
        /// pinned level file and the session seed — handed over once, at construction, rather than read off the
        /// host one property at a time. Every member is at its "nobody said" value on a player's launch.
        /// </summary>
        private readonly SessionTestOptions _test;

        //How far through _test.DetonateSeconds the run has got. The screen outlives every level, as the host's
        //cursor did before #582, so a schedule runs once per launch and not once per level.
        private int _detonateNext;

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
        internal int LevelIndex => _run.Index;

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

        //Whether the carriage was being driven this frame (A/D or W/S), kept for the length of one update so
        //the combination lesson can read it together with _adsHeld (#460). The two halves are read in
        //different parts of the update, and the gesture being taught is holding them AT ONCE.
        private bool _carriageMoving;

        //Peak defocus amount at a full lean (#214) — what the periphery reaches while the frame's centre is
        //held in focus by the shape (PostProcessPipeline.Resolve's defocusFocus; the falloff itself lives in
        //Tonemap.fx). Well under the result page's 1: at 1 the edges are a field of colour and glow, which is
        //a page over a finished level, not a lens being aimed — ADS is a lean-in, not a scope, and its blur
        //has to match that restraint. The blur's radius scales with this too, not only its mix.
        private const float ADS_DEFOCUS = 0.5f;

        //The session's IFrameBlurSource answer — the same question MenuPage.FrameBlur answers for a page,
        //asked by BS3DGame.FinishSceneDraw while this screen is the active one. The amount rides the ADS
        //blend directly: the lean and the focus are one gesture, they land and release together, and the
        //gates that clear the lean while this screen stays active (a cinematic, a lost window) take the
        //blur out with it on the blend's own ease. A screen pushed OVER this one is the exception — a
        //covered session stops updating, so its answer simply stops being asked and the incoming page's
        //ramp starts from sharp (see "The end of a level goes out of focus" in docs/game-feedback.md).
        //The shape is 1: precise aim's periphery-only lens, the aimed centre held in focus.
        float IFrameBlurSource.FrameBlur => _preciseAim.Blend * ADS_DEFOCUS;
        float IFrameBlurSource.FrameBlurFocus => 1f;

        /// <summary>
        /// How far the frame's <b>overlay</b> — the HUD and the crosshair, drawn in display space after the
        /// resolve — goes out of focus this frame (#438): the blur of the page standing over this screen, and
        /// only a page's. The session's own answer above is a lens leaning in, and the readouts are not seen
        /// through that lens: they are on the glass, and they stay sharp while the periphery softens behind
        /// them. A page's blur is the whole frame going soft because the menu is now the subject, and a HUD
        /// left pixel-sharp inside it read as a fault — the owner's report was the score's count-up hanging
        /// crisp over a paused, blurred arena. In practice this is the pause's ramp and the pages that inherit
        /// it (Settings and Scene over a pause, <c>MenuPage.FrameBlur</c>'s default): the result page blurs
        /// too, but the overlay is gone by then (see <see cref="Draw"/>). Zero while this screen is active,
        /// which is every frame of play, so the direct draw costs what it always did.
        /// </summary>
        private float OverlayBlur =>
            Manager?.Active is IFrameBlurSource page && !ReferenceEquals(page, this) ? page.FrameBlur : 0f;

        #endregion

        #region The in-play HUD (display space, after the resolve)

        /// <summary>
        /// Score, streak, balls left and the awards flying into the corner. Its own class — see
        /// <see cref="PlayHud"/> for why, and for the whole of how it looks and moves; this screen only feeds it
        /// the events it animates and gives it the frame to draw into.
        /// </summary>
        private readonly PlayHud _hud;

        /// <summary>
        /// The tutorial's lessons and their card (#189): what the first chapter teaches and how far it has got.
        /// Its own class for the HUD's reason — it is the feel of being taught, and the part most likely to be
        /// retuned — and it draws nothing: the HUD draws what it reads there. This screen feeds it the events
        /// (a shot, a landing, a hold, the glass stepping, the net lighting) and the frame.
        /// </summary>
        private readonly Tutorial _tutorial;

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
        /// This frame's gravity wells (#332) — refreshed once in <c>StepPhysics</c>, read by the steps that
        /// follow it and by the aim preview drawn after them. One snapshot for both, which is the whole
        /// contract; see <see cref="GravityWells"/>.
        /// </summary>
        private readonly GravityWells _gravityWells = new();

        /// <summary>
        /// The knots of this frame's previewed flight (#332) — the muzzle, the segments the solver walked, and
        /// the touch. One or two entries on every level without wells, which is the beam it always drew;
        /// several when a well bends the shot, and then the beam is drawn as a chain along them.
        /// <para>
        /// Held as a field and refilled rather than allocated per frame: this is the aim path, which runs every
        /// frame the gun is pointed, and <c>BestPractices.md</c> §3 is about exactly that.
        /// </para>
        /// </summary>
        private readonly List<Vector3> _previewPath = new(16);

        /// <summary>
        /// The physics step, held <b>fixed</b> — and this is the one place the game deliberately does not do
        /// what the Testbed does. The Testbed takes one step of <c>min(frameTime, 1/60)</c> per rendered frame,
        /// which means the simulation runs in slow motion below 60 FPS (at 30 FPS everything moves at half
        /// speed) and with a dt that varies with the display and the load above it. Bepu's own guidance is to
        /// keep the timestep constant, and this project's rule is that nothing about gameplay may depend on the
        /// frame rate — the game runs with <c>IsFixedTimeStep = false</c> and offers <c>nocap</c>, so it is
        /// exactly the configuration that breaks under. The frame time is accumulated instead and spent in
        /// whole steps of this length — <see cref="PhysicsWorld.FIXED_TIMESTEP"/>, the one copy the aim
        /// preview's curved solver integrates at too (#585).
        /// </summary>
        private const float PHYSICS_TIMESTEP = PhysicsWorld.FIXED_TIMESTEP;

        /// <summary>
        /// How many steps one frame may spend at most. Without a ceiling a frame that hitched — a shader
        /// compile, a window drag, a breakpoint — would try to catch up the whole gap at once, take even longer
        /// doing it and fall further behind: the simulation would spiral instead of recovering. Past this the
        /// remaining time is dropped on the floor and the world simply runs slow for that one frame, which is
        /// the right trade for a game.
        /// </summary>
        private const int PHYSICS_MAX_STEPS_PER_FRAME = 4;

        private float _physicsAccumulator;

        //Simulated seconds the last StepPhysics actually ran: whole steps, at most PHYSICS_MAX_STEPS_PER_FRAME of
        //them. Below 30 FPS that is less than the frame's elapsed, and the rules that judge the simulation read
        //this rather than the wall clock (#577).
        private float _simulatedThisFrame;

        /// <summary>
        /// Where the drawn frame sits between the last physics step and the next one due — the accumulator as
        /// a fraction of <see cref="PHYSICS_TIMESTEP"/>, written by <c>StepPhysics</c> and read by everything
        /// that draws a ball (#293). 1 draws live poses; see <c>PhysicsBall.InterpolatedPose</c>.
        /// </summary>
        private float _renderAlpha = 1f;

        /// <summary>
        /// Below this a ball has left the game: it has run down the drain and out of the bottom of the funnel,
        /// or fallen off the island's edge into the city. Well under <see cref="ArenaIsland.FUNNEL_BOTTOM_Y"/>, so
        /// a ball that goes down the hole falls a visible distance before it is culled rather than winking out
        /// in the mouth of the drain.
        /// </summary>
        /// <remarks>
        /// Internal rather than private since #192: the plane is this screen's own physics rule, but the fade
        /// that softens the cull is pushed once a frame by <see cref="BS3DGame.BeginSceneDraw"/> — which runs
        /// for the front end too, so it needs the value even on frames this screen never draws — rather than
        /// duplicated as a second literal there.
        /// </remarks>
        internal static readonly float KILL_PLANE_Y = -42f;

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
        /// Where the lattice frame meets the world, and the <b>only</b> place it does on the drawing side.
        /// <para>
        /// Y hangs the top of the field at <see cref="FIELD_TOP_Y"/> — or higher, when the field is deep
        /// enough that its bottom level would otherwise start past the death line (see
        /// <see cref="FIELD_FLOOR_MARGIN"/>): a cell's height is its level index over √2, so without an
        /// offset every map would hang at its own depth rather than in one frame.
        /// </para>
        /// <para>
        /// X and Z put the middle of the field's top level on the axis the gun orbits and the camera looks
        /// down, correcting the residual <see cref="BallsMap.Center"/> leaves behind (it centres on the top
        /// level's <i>balls</i>, not on the field). The residual is measured off the centred top level rather
        /// than assumed, because a level file is authored elsewhere and cannot be held to a rule about where
        /// its layout sits. <see cref="Prazsky.BS3D.Levels.ClusterHang.FitWorldOffset"/> computes both halves
        /// and carries the full account, including how large the residual was before #561. Both halves ride
        /// the one vector the physics builder and the contact handler already take, so no new frame crossing
        /// is introduced.
        /// </para>
        /// </summary>
        private Vector3 _clusterWorldOffset;

        /// <summary>
        /// The height the field's topmost level hangs at — <see cref="ClusterHang.FIELD_TOP_Y"/>, aliased here
        /// because this screen reads it in several places and because the reason it is <b>its own number</b>
        /// is a fact about this file: it used to be written as
        /// <c>(FALLBACK_FIELD_LEVELS - 1 - FALLBACK_EXTRA_LEVELS) / √2</c>, which arrives at the same 8 and made
        /// the height of <i>every</i> loaded level a function of the built-in fallback pyramid's size — widen
        /// that pyramid (its level count is <see cref="FALLBACK_X"/>) or change how many empty levels it carries
        /// and every real level's cluster would silently move, along with the camera, the gun's orbit and the
        /// ceiling fitted to it. The fallback is one map among many; this is the frame they are all hung in.
        /// </summary>
        private static readonly float FIELD_TOP_Y = ClusterHang.FIELD_TOP_Y;

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
        /// <b>It was sixteen — the deepest field framed whole</b> (see <see cref="FIELD_FLOOR_MARGIN"/>: the two
        /// branches of the hang meet at a 16-level field), chosen so the cap changed nothing about any level
        /// that existed. <b>Eighteen since #135</b>, and the two extra levels are not a retune but the spending
        /// of frame that was being wasted: <see cref="GameCameraFit"/> used to reserve a whole barrel's length
        /// <i>below</i> the gun's trunnions, a pose the tube cannot strike, and reclaiming it freed 1.14 world
        /// units — 1.6 levels — at the bottom of the frame. Rounded to two, which is what makes Two (18 levels)
        /// framed whole rather than clipped by a hair.
        /// </para>
        /// <para>
        /// Levels no deeper than this are untouched by both changes <b>by construction</b>: <c>FramedTopY</c>
        /// is a <c>min</c> against the glass, so on a field the glass already sits under the window it is the
        /// glass that binds, and the window moving up cannot be seen. What the pair buys is levels 17 and 18 of
        /// every tall field — measured against a cluster reaching 11 levels above the old window on Onion, so
        /// it is a dent in that and not a cure; standing back to frame such a column whole is the thing this
        /// cap exists to refuse.
        /// </para>
        /// </summary>
        private const int FRAMED_LEVELS = 18;

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
        /// past <see cref="FIELD_TOP_Y"/> — <see cref="ClusterHang.FIELD_FLOOR_MARGIN"/>, aliased here because
        /// the incident that bought it is a fact about this screen: pinning the top unconditionally meant any
        /// field deep enough for its bottom to reach past the line <i>started</i> with cells below it, and a
        /// 30-level map was lost on the frame it was built, before a shot was fired (a ball at −6.36 against
        /// the −5.5 the line then stood at).
        /// <para>
        /// Measured across the move that lowered the line onto the island: <c>One.json</c> and every other
        /// 16-level field did not move at all, an 18-level one stopped being raised (top 7.02 → 5.66, floor
        /// −5.00 → −6.36, so it hangs 1.36 lower and starts 2 further above the line), and a raised field's
        /// clearance is unchanged by construction — its floor is pinned a radius over the line wherever the
        /// line is, so Comet still starts 7.57 above it and only its world Y moved (top 17.92 → 14.92).
        /// </para>
        /// </summary>
        private const float FIELD_FLOOR_MARGIN = ClusterHang.FIELD_FLOOR_MARGIN;

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

        //The same idiom for the magazine strip's colours (#236): fixed at Magazine.SIZE, refilled from the live
        //queue in Draw and handed over as a span. The HUD is given the COLOURS rather than the Magazine itself
        //deliberately — the strip is a readout of what is loaded, and a HUD that could reach the magazine could
        //also step it.
        private readonly BallType[] _magazineQueue = new BallType[Magazine.SIZE];

        //The cluster profile's horizontal axis is the GAMEPLAY camera's right vector — the lens the player aims
        //with, not whatever a drop cinematic has swung the lens to. A cinematic blends the camera away from the
        //overview pose (UpdateCamera Lerps towards _cinematic.Position/Target), and the profile drawn from that
        //swung lens would turn the cluster's outline as the shot played out, which reads as the HUD shaking
        //rather than as the cluster turning. So this holds the right vector of the pose BEFORE the cinematic blend,
        //updated every frame, and the profile reads it instead of the live camera. See BuildClusterProfile.
        private Vector3 _gameplayCameraRight = Vector3.Right;

        private KinematicBody _ceiling;

        /// <summary>
        /// The ceiling's descent — where the glass hangs and where it is sliding to, the steps queued and held,
        /// the tall level's feed and the flash (#582). The body above is moved to its <see cref="CeilingDescent.Y"/>
        /// by <see cref="SlideCeiling"/>; see <c>GameplayScreen.Ceiling.cs</c> for what a step does to the world.
        /// </summary>
        private readonly CeilingDescent _ceilingDescent = new();

        //The death line and the pair that keeps a mere SWING from crossing it are ClusterHang's since
        //#301/#302 - the level generator hangs a level in a real simulation to find out whether the remainder
        //sags after a group is shot off, and a gate that hangs a cluster somewhere else or forgives a dip
        //differently answers a question about a different game. The reasons behind all three figures are
        //written there; only the ONE below is this file's, because it is about what the game DRAWS: the laser
        //net hovers half a unit lower than the line (see LaserGrid.Fit, which puts it where a lost ball
        //SURFACE would be), so at anything under a unit of clearance the net is drawn inside the island cap it
        //is meant to hover over.
        private const float CEILING_DEATH_Y = ClusterHang.DEATH_Y;   //a ball below this has lost the level
        private const float CLUSTER_SWING_ALLOWANCE = ClusterHang.SWING_ALLOWANCE;
        private const float CLUSTER_BELOW_LINE_GRACE = ClusterHang.BELOW_LINE_GRACE;

        //The pad's answer to a step (#378) — heavier and longer than a shot or a landing, the way the plate's
        //own slide is a slower event than either. Left-heavy for the shove; halved on a feed step
        //(CEILING_RUMBLE_FEED_SCALE) for the same reason PlayCeilingStep and the flash colour go soft on one:
        //a feed is the game rewarding the player, not warning them.
        private const float CEILING_RUMBLE_LEFT = 0.6f;
        private const float CEILING_RUMBLE_RIGHT = 0.25f;
        private const float CEILING_RUMBLE_SECONDS = 0.5f;
        private const float CEILING_RUMBLE_FEED_SCALE = 0.5f;

        /// <summary>The flat colour the cluster's ripple carries, as opposed to the plate's own emissive.</summary>
        private static readonly Vector3 RIPPLE_ALARM_COLOR = new(1f, 0.07f, 0.05f);

        /// <inheritdoc cref="CeilingDescent.CEILING_FEED_COLOR"/>
        private static readonly Vector3 RIPPLE_FEED_COLOR = new(0.02f, 0.1f, 1f);

        //The floor alarm — the descending ceiling's warning at the other end of the field. The net itself
        //(geometry, pulse, fades, the linger after the level ends) lives in LaserGrid; this screen owns the
        //trigger: within LASER_WARN_STEPS more descents of the death line, measured on the same live poses
        //the loss itself is decided on (see UpdateLaserWarning). The hysteresis keeps a swaying cluster
        //from flickering it at the threshold — a shot shoves the structure, and a lowest ball bobbing a few
        //tenths of a unit across the exact line would arm and stand down the net with every swing.
        //Three descents, up from two (#110): playtesting said the net came on too close to the loss to be
        //tracked before it mattered — at the set's cadences a descent is 4–9 shots, so the extra step buys
        //roughly that many shots of watching it approach.
        private const float LASER_WARN_STEPS = 3f;
        private const float LASER_WARN_HYSTERESIS = 0.3f;

        private readonly LaserGrid _laserGrid;

        /// <summary>
        /// Every bomb going off (#389) — its flash, its report and the light it throws. Session content like the
        /// net above, but stepped on the world's clock rather than stamped on the wall's: see <see cref="Blasts"/>.
        /// </summary>
        private readonly Blasts _blasts;

        /// <summary>
        /// How hard a full-size blast heaves the camera, as a <c>CameraShake.Rumble</c> strength. Under 1 so that a
        /// chain still reads as bigger than one bomb before the rumble saturates — each link adds its own as it
        /// goes off.
        /// </summary>
        private const float BLAST_RUMBLE = 0.55f;

        #endregion

        #region Levels

        /// <summary>
        /// The attempt at the level being played (#582): its entry and board, its scorer, its clocks and counters,
        /// its wildcard cadence, its power-up charges, its balls' material and the line's grace — everything that
        /// belongs to one attempt and to nothing longer. <see cref="BuildLevel"/> replaces it whole, so nothing
        /// per-level is reset by hand any more; see <see cref="LevelRun"/> for what is in it and what was left
        /// here, and why. Before the first level it is a blank run, the values these fields used to start at.
        /// </summary>
        private LevelRun _run = new();

        /// <summary>
        /// Where this level stands in its flow (#582) — <see cref="LevelPhase"/> for the phases and the one rule
        /// over which may follow which. Written <b>only</b> by <see cref="EnterPhase"/>, which also does each
        /// phase's entry work; everything that used to infer the phase from a countdown, two flags and an outcome
        /// field reads it through <see cref="LevelDecided"/> and <see cref="LevelOver"/>.
        /// </summary>
        private LevelPhase _phase = LevelPhase.Playing;

        /// <summary>
        /// Seconds left of <see cref="LevelPhase.ClearedBeat"/>, the pause between the field emptying and the
        /// level actually ending. Data of that phase and nothing else: armed on entering it, counted down while it
        /// lasts and not while a camera takeover holds it, and read by nobody as a flag — the phase says whether
        /// the level is still being played.
        /// <para>
        /// The pause is the point. Balls leave the map at <b>release</b> time, while their bodies are still
        /// falling: ending the level the instant the last group is cut would take the collapse the player just
        /// earned off the screen before they saw it.
        /// </para>
        /// </summary>
        private float _clearedCountdown;

        /// <summary>
        /// Whether the clear that just happened is the one that finishes its <b>block</b> (#184) — decided in
        /// <see cref="CheckLevelCleared"/>, where the celebration starts, and read again by
        /// <see cref="ShowResultScreen"/> a beat later so the page, the fireworks and the fanfare cannot
        /// disagree about what the player just did. Never set on a loss, and cleared by <see cref="BuildLevel"/>
        /// with the rest of a level's state.
        /// </summary>
        private bool _blockCompleted;

        /// <summary>
        /// Whether the clear that just happened is the one that finishes the whole <b>campaign</b> (#215) —
        /// decided in <see cref="CheckLevelCleared"/> beside <see cref="_blockCompleted"/> and for the identical
        /// reason: the confetti starts there, so a decision taken on the result page would reach it a beat too
        /// late. <see cref="ShowResultScreen"/> then reads this rather than asking again, which is what stops
        /// the page and the celebration answering two subtly different questions.
        /// </summary>
        private bool _campaignCompleted;

        /// <summary>How long that pause is — long enough for a big collapse to reach the drain and go down it.</summary>
        private const float LEVEL_CLEARED_BEAT = 2.5f;

        /// <summary>
        /// The level's outcome is settled: anything past <see cref="LevelPhase.Playing"/> — the field has emptied
        /// and its beat is running, the line loss is holding the camera, or the page is up. Nothing belonging to
        /// <i>playing</i> the level may happen past this — no shot leaves the barrel, no preview promises one,
        /// nothing in the magazine changes colour (#176, #177), no ceiling step comes down (#563).
        /// <para>
        /// It has to be asked at the input and at the landing themselves rather than left to the stack: a
        /// clear pushes no screen at all for <see cref="LEVEL_CLEARED_BEAT"/> seconds — longer while a drop
        /// cinematic holds the countdown — and this screen goes on updating normally for the whole of that
        /// beat, and under the page afterwards (#241).
        /// </para>
        /// </summary>
        private bool LevelDecided => LevelPhases.IsDecided(_phase);

        /// <summary>
        /// The level's arithmetic is <b>read-only</b>: the result page is up (its figures snapshotted into
        /// <see cref="LevelResult"/>, its record written) — <b>or</b> the line loss is holding it back
        /// (<see cref="LevelPhase.LossHold"/>), the 1.7 s or so between the crossing and the page, in which the
        /// level is already lost and the page does not exist yet. Not true during the cleared beat, which
        /// <see cref="LevelDecided"/> covers: there a shot still in the air may land and score, because the page
        /// has not taken the figures yet.
        /// <para>
        /// It matters at all because the simulation goes on running under the page (#241), so a shot still in
        /// the air can land, stick and fall past the kill plane with the level already over and reported.
        /// </para>
        /// </summary>
        private bool LevelOver => LevelPhases.IsFinal(_phase);

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

        //How long the fireworks' fastest phase runs when a whole BLOCK of levels has just been finished
        //(#184), against the 2.2 s an ordinary clear takes. Nearly three times the barrage, and it is the
        //opening rather than the total for the reason Fireworks.Celebrate states: the opening is the part the
        //player is looking at. Eight seconds is about a hundred and fifteen shells at INTERVAL_OPENING, which
        //is more than the 32 concurrent ones the shader carries - so the sky stays saturated for the whole of
        //it rather than filling and emptying.
        private const float BLOCK_CELEBRATION_OPENING = 8f;

        //How long the campaign's closing confetti keeps falling (#215). LONGER than the fireworks' minute, and
        //that is the point rather than an accident: the display eases its density off after sixteen seconds
        //because a minute of barrage is exhausting, so by the time the player is reading the last score of the
        //campaign the sky has gone quiet — and the ending that deserves the most is the one that was thinning
        //out. Paper is the opposite kind of subject: it costs nothing to keep falling, it never shouts, and a
        //player who sits on the final screen should still be in it. Confetti fades itself out at the end.
        private const float CONFETTI_SECONDS = 105f;

        /// <summary>
        /// Which of the two limits ended the level. An enum rather than a message carried through from where
        /// the loss was detected: the wording is a <b>display</b> concern and belongs on the screen that shows
        /// it, and a string built at the point of detection ends up carrying the numbers that were convenient
        /// there — which is how a player came to be shown "a ball at -5,58 &lt;= -5,50". Those figures are
        /// diagnostics; they belong in the log, and they still go there.
        /// </summary>
        private enum LevelFailure { None, OutOfBalls, ClusterReachedLine }

        /// <summary>
        /// Which limit lost the level — data of a loss, set by <see cref="LoseLevel"/> as it leaves
        /// <see cref="LevelPhase.Playing"/> and <c>None</c> otherwise, so a level that reaches
        /// <see cref="LevelPhase.Over"/> with it still <c>None</c> is a cleared one. Reset by entering
        /// <see cref="LevelPhase.Playing"/>.
        /// </summary>
        private LevelFailure _pendingFailure;

        #endregion

        #region The gun and the shot

        private readonly Cannon _cannon;

        //What the gun hands the host's sun shadow pass — built once in the constructor, installed per level
        private readonly System.Action<Matrix> _gunShadowCaster;

        //The queue of loaded colours, its post-shot glide and where each loaded ball sits in the bore are all
        //Magazine's, shared with the Testbed since #76 — as are the two figures the barrel was cut to
        //(Magazine.SIZE and Magazine.SPACING, which CannonRig derives the tube's length and
        //CannonRig.PivotToFrontBall from). What stays here is this screen's own two rules: which colours may be
        //loaded at all (RandomBallType, injected below), how often one is a wildcard (NextLoadedKind, injected
        //beside it, #330), and which loaded colours have died (Transmute, which calls Magazine.Recolour). What
        //each slot carries — colour, kind, and the cross-fade a re-coloured ball dissolves through — is one
        //MagazineSlot the magazine moves whole since #582, where it used to be three parallel arrays here kept in
        //step by three hooks. Built in the constructor because its policies are instance methods.
        private readonly Magazine _magazine;

        /// <summary>
        /// What every wildcard on screen is showing this instant (#330). One per session and not one per ball —
        /// see <see cref="WildcardCycle"/> for why that is the whole point of it.
        /// </summary>
        private readonly WildcardCycle _wildcard = new();

        //Several ball diameters a frame: the shot is a streak, not something the eye can follow. That is
        //the intended feel, and it is why the launch smear below exists at all.
        private const float SHOOT_SPEED = 200f;

        //How hard a shot hits the camera. Still a full kick: it is the *ceiling* that was lowered
        //(BS3DGame.CAMERA_SHAKE_SCALE), not the strength of one shot. Kicking at a fraction instead would let
        //two shots in quick succession accumulate straight back up to the response that was too strong.
        private const float RECOIL_KICK = 1f;

        //How hard a shot answers in the player's hands (#378) — the recoil's own two channels: a heavier
        //thump on the left motor than the buzz on the right, same as the physical difference the two motors
        //are built on, decaying quickly so a burst of shots reads as a burst and not a held buzz.
        private const float SHOT_RUMBLE_LEFT = 0.55f;
        private const float SHOT_RUMBLE_RIGHT = 0.25f;
        private const float SHOT_RUMBLE_SECONDS = 0.12f;

        //The gun's own recoil — the tube thrown back along its bore, and since #115 the undercarriage's
        //smaller, later shove under it — is the shared Cannon's now (Cannon.RECOIL_BACK/RECOIL_DECAY/
        //CARRIAGE_RECOIL_BACK): two responses off one clock only stay one clock if the gun owns it. This
        //executable keeps the clock's ticks — KickRecoil in Shoot, StepRecoil in Update — and the whole of
        //it stays drawing only: a shot leaves along the true aim on the frame it is fired, before any of it.

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

        //The loss-side counterpart (#434): the camera flown at the point the cluster crossed the line, and
        //the beat the ending is held back for while it happens.
        //Whether its ending has gone up yet is the phase's question (LevelPhase.LossHold), not a flag of its own.
        private readonly LineLossCinematic _lineLoss = new();

        //Which released balls this cinematic is following, by body handle — see TryBeginDropCinematic for why
        //handles and not list indices, and why recycling cannot bite here.
        private readonly HashSet<int> _cinematicSubject = new();

        //The new chapter's own establishing shot (#267): a block's first level tours the arena before handing
        //the gun over. Owns the pose and the blend exactly as the drop cinematic does; this screen owns the
        //trigger and the fact that the gun does not answer while it plays — see TryBeginChapterIntro and
        //CameraTakeoverEngaged.
        private readonly ChapterIntro _chapterIntro = new();

        //Which blocks have already shown their intro this run of the program, keyed by the block's FIRST
        //level index rather than its name — a name is authored prose and not guaranteed unique across blocks,
        //where the first index always is. Deliberately not PlayerProgress and never cleared: a fresh launch
        //sees every chapter's opening again, which is the point (see TryBeginChapterIntro), and a retry of the
        //block's own opening level must not replay it a second time in the same sitting.
        private readonly HashSet<int> _chapterIntroShown = new();

        /// <summary>
        /// True while either camera takeover holds the frame — the drop cinematic or the chapter's
        /// establishing shot — which is everywhere the gun must not answer and a skip button means "let go of
        /// it". At most one is ever engaged at once: the intro blocks the very shot the drop cinematic needs
        /// to begin, and <see cref="BuildLevel"/> resets the drop cinematic before a new level's own intro
        /// could ever start. Asking both costs nothing and needs no third flag to say which one is live.
        /// </summary>
        private bool CameraTakeoverEngaged => _cinematic.Engaged || _chapterIntro.Engaged || _lineLoss.Engaged;

        /// <summary>Skips whichever takeover is currently running — see <see cref="CameraTakeoverEngaged"/>
        /// for why asking both is safe.</summary>
        private void SkipCameraTakeover()
        {
            //A skip that took is the player saying they know: the HUD's hint retires for the session (#499)
            if (_cinematic.TrySkip()) _hud.NoteCinematicSkipped();
            _chapterIntro.TrySkip();
        }

        //The template a shot is stamped from — the sphere, its inertia, its bare shape index and its sleep
        //threshold — is PhysicsWorld's, which stamps a fresh copy per shot rather than holding one and writing
        //this shot's pose and velocity over the last one's, as this and the Testbed both used to.

        //The launch smears: the streak a shot leaves at the muzzle, anchored there and living its own short
        //life while it fades. LaunchSmears' since #76 — the billboard, the six dials, the colour rule and the
        //additive depth-read draw stood here and in the Testbed, value for value. What stays here is when one
        //is added (Shoot), that they age every frame this screen updates, and where the draw sits in the
        //frame, which the call site in Draw states.
        private readonly LaunchSmears _smears;

        //The line loss's spark shower (#434), which shares the smears' effect instance and their draw slot
        private readonly LineSparks _lineSparks;

        /// <summary>
        /// The session's one generator: the magazine's deal (<see cref="RandomBallType"/>), the transmute's
        /// replacements, and the drop cinematic's and the chapter intro's rolls. Rebuilt for every level from
        /// <see cref="_seed"/> (<see cref="SeedSession"/>), so a level's deal is a function of its seed and the
        /// shots played. It was an unseeded static until #582, which is why no playtest report could be replayed
        /// and why a one-off like the seventeen glass bounces on #582 could not be told from noise.
        /// </summary>
        private Random _random = new();

        //The seed _random was last built from, printed as "[session] seed N"
        private int _seed;

        private MouseState _previousMouse;
        private bool _padTriggerReleased = true;

        //Whether the cursor is the aim's or the desktop's (#99, #154). Taken on arrival at play when the
        //pointer is already in the picture — pressing the menu entry that put this screen on top is the
        //opt-in — and given back on every focus loss, after which it is only re-taken by a click inside the
        //game's own picture: until then the window can be dragged by its title bar, resized, or left alone
        //for another application.
        private bool _cursorCaptured;

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
        /// them, <see cref="CeilingDescent.CEILING_DESCENT_PER_STEP"/> per step, plus the stretch the structure hangs with and
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

        //The muzzle round's coloured mark — what says "this one, right now" since #236, in place of the white
        //ripple #175 used to breathe on the ball itself — was a halo round the round until #425 and is the
        //muzzle collar now, which keeps no state here: see MuzzleGlowStrength and DrawMuzzleCollar.

        /// <summary>Where the beam starts. Stored rather than recomputed in <c>Draw</c>, so the line, the ghost and the shot cannot disagree about where the bore is.</summary>
        private Vector3 _previewMuzzle;

        /// <summary>
        /// Slot 0's world position as the magazine was collected this frame — the centre the halo was drawn
        /// concentric with (#236). Stored for the same reason <see cref="_previewMuzzle"/> is: the ring and the
        /// ball it rang must not be able to disagree, and they would if each asked <c>Magazine.Pose</c> itself.
        /// <b>Written every frame and read by nothing since #425</b>, which replaced the halo with the muzzle
        /// collar drawn in the barrel's own pose.
        /// </summary>
        private Vector3 _muzzleBallPosition;

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
        private readonly ClusterCollector _clusterCollector = new(ClusterRipple.Advance);

        //The wave a landing (and a ceiling step, and an infection's spread) sends through the cluster — its
        //walk and its figures are ClusterRipple's since #582; the screen only says where it starts and, for
        //the ceiling's wave, what colour the renderer flares it in (Ceiling.cs). Held for the life of the
        //screen because its scratch grid is reused from landing to landing; the cluster is handed in per call,
        //since BuildCluster replaces _physicsBalls wholesale every level.
        private readonly ClusterRipple _ripple = new();

        #endregion

        /// <summary>The play session's screen, built once for the life of the program and reused by every level.</summary>
        /// <param name="game">The host: the camera, the scene, the audio and the level set.</param>
        /// <param name="test">The testing levers for play, from the command line; see <see cref="_test"/>.</param>
        public GameplayScreen(BS3DGame game, SessionTestOptions test)
        {
            Game = game;
            _test = test ?? throw new ArgumentNullException(nameof(test));
            _drawMotionVelocity = DrawMotionVelocity;
            _hud = new PlayHud(game)
            {
                //Testing only (the "streak=" argument): pins what the multiplier readout SHOWS, so the capped
                //state added in #180 can be looked at. It cannot be reached by a script — it takes five
                //consecutive scoring shots — and it changes no scoring, only the display. See PlayHud.
                ForcedMultiplier = test.ForcedStreak
            };

            //Taught once, ever, and the save remembers (PlayerProgress.Lessons) — through the host's own two
            //verbs, so the write goes the one way every save write goes. The testing argument offers every card
            //and writes nothing.
            _tutorial = new Tutorial(game.WasLessonTaught, game.RecordLessonTaught, test.TutorialMode);

            //Orbit centre is the field the cluster hangs over. No trunnion height goes in: the gun stands on
            //the island's dished stone, so its height is the carriage's own figure of its radius
            //(CannonRig.TrunnionHeightAt) and the pose re-seats it on every move — the wheels stay on the
            //stone wherever the walk stands.
            _cannon = new Cannon(new Vector3(0f, 5f, 0f), 20f);

            //What this session adds to the sun's shadow map (#470): its gun. The host owns the rig and draws
            //the island; where the gun STANDS is this screen's, and the shadow pass runs before any screen
            //has drawn, so the session hands the host a closure rather than the host reaching in. Built once
            //here, so no level allocates it; installed by BuildLevel and cleared by TearDown, because this
            //screen outlives every session — installed here instead, the gun cast a shadow over the front end
            //before the first Play, and none at all after the first Retry, Next Level or Main Menu.
            _gunShadowCaster = vp => Game.CannonRig.DrawShadow(vp, _cannon.BarrelWorld(),
                _cannon.CarriageWorld(), _cannon.WheelTravel, _cannon.SlideTravel);

            //The queue's colours are the level's business (RandomBallType draws only among what is still
            //hanging), so what to load next is injected; the constructor deals a full queue with it, which is
            //what gives the player something to read from the first frame. What kind each dealt ball is, is this
            //screen's rule too — the deal is the one moment it is decided, and the count behind it is the
            //level's (#330). Both handed over once, here, since a delegate built per shot would allocate one per
            //round fired; the kind and the transmute state ride inside the magazine's own slots (#582).
            _magazine = new Magazine(RandomBallType, NextLoadedKind);

            //The work each physics step carries inside it, wired once here for the reason the field states
            _processContacts = () => _eventHandler.ProcessQueuedContacts();

            //The smears' billboard quad and every parameter handle their draw needs, in one construction. The
            //effect is the content manager's and is never disposed there.
            _smears = new LaunchSmears(GraphicsDevice, Game.Content.Load<Effect>("Shaders/ShotTrail"));

            //And the line loss's sparks borrow that same effect instance a third time (#434), which is why
            //all three push the trail widths per draw rather than once here - see LineSparks' own class doc.
            _lineSparks = new LineSparks(GraphicsDevice, Game.Content.Load<Effect>("Shaders/ShotTrail"));

            //The aim beam borrows the SAME effect instance — it is the same billboard between two world points,
            //and a short segment of it comes out as a dash for free. Sharing it is why both components now push
            //the trail's two widths per draw instead of once; see AimBeam's remarks.
            _aimBeam = new AimBeam(GraphicsDevice, Game.Content.Load<Effect>("Shaders/ShotTrail"));

            //Its OWN effect and not the trail's, unlike the two above: this billboard is placed from a centre
            //and the view basis rather than from two world points, so there is no quad to share and no widths
            //to fight over — which is the mess sharing ShotTrail cost those two.

            //And the crosshair's own white texel, which the host used to hold for it
            _crosshair = new Crosshair(GraphicsDevice);

            //The floor alarm's net, loaded like the smears: session content, made here with the device up.
            //It is handed the ceiling flash's own red — the two are one warning at two heights, and passing
            //the constant is what keeps them from drifting apart.
            _laserGrid = new LaserGrid(GraphicsDevice, Game.Content.Load<Effect>("Shaders/LaserGrid"), CeilingDescent.CEILING_FLASH_COLOR);

            //And the blasts (#389), loaded the same way. NOT handed the host's audio, unlike Fireworks: this
            //screen is built before the host has synthesized its sounds (BS3DGame.LoadContent), so the audio is
            //passed to each Update instead of being captured here as a null.
            _blasts = new Blasts(GraphicsDevice, Game.Content.Load<Effect>("Shaders/Blast"));
        }

        //A level is played with nothing above this screen. A pause is pushed OVER it and freezes it with its
        //UpdatesUnderlying while the manager goes on drawing it underneath; the result screen is pushed the
        //same way but leaves that flag true, so the world under it keeps running (#241). Nothing draws or
        //runs beneath this screen itself: the backdrop under it on the stack is dormant either way.

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

            //Arriving here IS the player's opt-in (#154): every way onto the top of the stack — Play, a level
            //tile, Resume, Retry, Next Level — is a menu entry the player just pressed, so the aim takes the
            //cursor at once instead of demanding one more click in the picture, which #99's arrive-free rule
            //cost every level start. Only a pointer already IN the picture, though: the entry may just as
            //well have been pressed by the pad or the arrow keys with the mouse parked on another monitor,
            //and warping in a pointer nobody offered is exactly the hostage-taking #99 exists to prevent —
            //the pad neither needs nor notices the capture, and a free pointer is re-taken by a click in the
            //picture as ever. The bounds test is the capture click's own, for its reason: a MouseState reads
            //coordinates over the title bar or another window quite happily. #99's two hazards are both still
            //answered: the pressing click cannot fire a shot, because the shot edge sits behind Invalidate's
            //dropped baseline and is measured against the mouse snapshot below, which already knows the
            //button is down (same hazard, and the same fix, as the menu's own _menuClickArmed); and the aim
            //cannot yank, because ApplyCursor applies nothing until the first Recentre has re-based the
            //delta. A mid-play focus loss is not this path — Update's inactive branch frees the cursor, and
            //THAT recovery still takes the click. An arrival with the window unfocused stays free too: there
            //is no press to read intent from.
            MouseState mouse = Mouse.GetState();

            _cursorCaptured = Game.IsActive
                && mouse.X >= 0 && mouse.X < GraphicsDevice.Viewport.Width
                && mouse.Y >= 0 && mouse.Y < GraphicsDevice.Viewport.Height;
            _previousMouse = mouse;

            Game.IsMouseVisible = !_cursorCaptured;
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsBuilt) return;

            //Covered, but by the one page that lets this screen keep running (#241) — a pause stops it dead
            //and does not reach Update at all. What goes on there is the WORLD and not the GAME; see
            //UpdateUnderResult for which is which.
            if (!IsActive)
            {
                UpdateUnderResult(gameTime);
                return;
            }

            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (Game.IsActive)
            {
                //Hidden only once the aim actually holds the cursor (#99). While it does not, this is the
                //pointer the player manages the window with, and it has to be visible to be usable.
                Game.IsMouseVisible = !_cursorCaptured;

                //One XInput poll for the whole frame: UpdateInput and UpdateAim used to each poll the pad,
                //two OS queries of the same slot microseconds apart. Circular rather than the default
                //IndependentAxes (#352, #515): the default clamps each stick axis on its own, which is a
                //SQUARE dead zone - a small diagonal push moves the stick while the same push along a single
                //axis does not, and near the edge the reported direction pulls towards the axes. Both sticks
                //read from this one state - the right (aim, MouseAim.Update) and the left (advance,
                //GameplayScreen.Input.cs) - and a circular zone is the more correct shape for either.
                GamePadState pad = GamePad.GetState(PlayerIndex.One, GamePadDeadZone.Circular);

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
                //hidden over an unfocused window it simply disappears wherever the player moves it. Since #99
                //the capture is dropped with it, so coming back does not re-take the pointer on its own — the
                //click that brings the window forward is the one gesture the old behaviour ate.
                Game.IsMouseVisible = true;
                _cursorCaptured = false;
                _mouseAim.Invalidate();

                //A trigger held while the window was away must be re-released before it fires
                _padTriggerReleased = false;

                //And a held precise-aim button must not keep an alt-tabbed window leaned in — the gamepad's
                //triggers report through XInput whether the window has focus or not.
                _adsHeld = false;

                //AND THE LEVEL STOPS (#355). Everything above this branch is about the CURSOR, which is what
                //losing focus was written as; it is a problem of TIME. Below this branch the world goes on
                //being played by nobody, and a player who alt-tabs to answer a message can come back to a
                //level they lost with no shot fired.
                //
                //⚠ WHAT ACTUALLY RUNS IS NOT WHAT #355 SAYS RUNS, and the difference is worth having written
                //down. Its report reads "the ceiling keeps sliding down, on wall clock" — it does not.
                //SlideCeiling only ANIMATES a step that a shot has already earned
                //(CeilingDescent's queued steps and its Update), so a field nobody is shooting at gets no new
                //steps. Three things do run, and together they are enough:
                //
                //  - StepPhysics. The cluster is a hanging lattice that never stops settling and swaying, and
                //    what the loss is read off is the LIVE lowest pose.
                //  - The last shot's OWED step. It waits CeilingDescent.CEILING_STEP_HOLD and then slides, so the descent the
                //    player fired for arrives while they are away and lands on nobody watching.
                //  - ClusterLineWatch's below-line grace, which counts in `elapsed` — wall clock. A cluster
                //    already under the line therefore loses on time alone.
                //
                //⚠ IT RETURNS, and that is the same requirement #79 met one layer in: the manager applies a
                //push at the top of the NEXT frame, so pausing without stopping here would let this frame run
                //the descent, the step and the loss check against a session that is already over. Once the
                //page is up this screen is covered and Update is not reached again, so nothing stacks a second
                //one.
                //
                //The pause page rather than a second frozen state, because it is honestly where the player is
                //and it reuses the whole existing stop rather than inventing a state to reason about; coming
                //back therefore takes a deliberate keypress, which is right, since the aim demands a click to
                //re-capture (#154) either way. The lean-out blend below does not run, so the frozen frame keeps
                //whatever lean it had — the ease-out happens on the way back instead, _adsHeld being false.
                if (Game.PauseOnFocusLoss)
                {
                    Game.PauseGame();
                    return;
                }
            }

            //Testing only: walk= and turn= hold W/S and A/D for a run nobody is sitting at (see ScriptedPlay), at the
            //rates the keys use. Outside the focus gate above on purpose — such a run is launched without focus — and
            //held off by a camera takeover exactly as the keys are.
            if (ScriptedPlay.Current is ScriptedPlay script && !CameraTakeoverEngaged)
            {
                float walk = script.Walk(WallClock), turn = script.Turn(WallClock);
                if (walk != 0f) _cannon.Advance(walk * CANNON_ADVANCE_RATE);
                if (turn != 0f) _cannon.Orbit(turn * CANNON_ORBIT_RATE);
            }

            _cannon.Update(gameTime);

            //The queue glides forward into the slot the fired ball left rather than snapping. Wall clock, not
            //the simulation's step: balls sliding down a tube is the gun answering the shot. The same call runs
            //a re-coloured ball's dissolve out of its old colour (#582 moved that countdown into the magazine).
            _magazine.Step(elapsed);

            //And every wildcard on screen turns over together (#330), on the same wall clock and for the same
            //reason as the two above: a wildcard cycling in the bore is the gun saying what is loaded.
            StepWildcards(elapsed);

            //The gun slides home — the tube in its cradle and the carriage under it, both off the one stroke
            //the shared Cannon owns since #115. Wall clock, like the magazine's glide above and for the same
            //reason: the recoil is the gun answering the shot, not the simulation.
            _cannon.StepRecoil(elapsed);

            //The cinematic reads the balls where the last step left them and answers with this frame's pose and
            //time scale, so the scale is applied to the very step its own framing was chosen against.
            _cinematic.Update(elapsed, TryGetDropCentre(out Vector3 dropCentre), dropCentre);

            //And the line's own (#434), which also decides when the ending it is holding back goes up.
            StepLineLoss(elapsed);

            if (!_cinematic.Engaged) _cinematicSubject.Clear();

            //The chapter intro has no subject to read and no time scale to feed the step below — see the
            //class remarks on why. Just its own pose and blend, exactly like the line above.
            _chapterIntro.Update(elapsed);

            //A step that came due while the shot was in the air waits here for its moment — see
            //CeilingDescent.Update. Before the physics step, so a released step slides on the same frame. The
            //glass's glow fades on the wall clock in the same call; the glass itself slides inside StepPhysics,
            //one physics step at a time (#577), so the solver always works against the moved body.
            if (_ceilingDescent.Update(elapsed, LevelDecided, CameraTakeoverEngaged, out bool ceilingFeeding,
                    out float ceilingWaited))
                AnnounceCeilingStep(ceilingFeeding, ceilingWaited);

            //Slow motion is applied here and nowhere else: the fixed timestep is untouched and only the time
            //fed to the accumulator is scaled, so a slowed world is exactly as stable as a full-speed one. The
            //ceiling's descent rides the steps since #577, so a slowed world slows it too — which no player sees,
            //because it is not moving while the gun is locked, and which keeps plate and cluster in step if it is.
            //Testing only (detonate=, #389): a bomb set off on the wall clock's schedule. Before the step, so its
            //debris moves on this frame the way a real landing's does.
            if (TryTakeForcedDetonation()) DetonateForTesting();

            //The level's own play clock (#546), on the frame's real seconds: here, beside the step, is exactly
            //the time the level is being played
            _run.Seconds += elapsed;

            StepPhysics(elapsed * _cinematic.TimeScale);
            NoteSimulated(_cinematic.TimeScale);

            //The blasts a landing inside that step set off (#389), on the step's own scaled time: the debris they
            //threw is moving at that speed, and a flash running at full speed over a slow-motion collapse is over
            //before its fragments have left the hole. The rumble goes to the camera as each blast goes off.
            float blastJolt = _blasts.Update(elapsed * _cinematic.TimeScale, Game.Audio);
            if (blastJolt > 0f) Camera.Shake.Rumble(BLAST_RUMBLE * blastJolt);

            //And the frame this level actually costs, judged where it is paid. The probe used to run under the
            //front end alone, so the tier was settled against a scene with no cluster in it and kept for one
            //with up to 959 balls — Onion cleared the menu at High and then played at exactly half refresh
            //(#121). The level's own build re-opened the latch; this is what closes it again, on evidence.
            //
            //REAL elapsed, not the scaled step above: a slowed world costs what it costs to draw. And not while
            //a camera takeover runs — the drop cinematic is the heaviest and least typical moment of a level,
            //and the chapter intro is judged on a frame with no cluster shot yet either; a verdict taken on
            //either would spend image quality for the rest of the session on a few seconds that are not play.
            if (Game.IsActive && !CameraTakeoverEngaged) Game.TuneQualityToFrameRate(elapsed, "level");

            //After the step: poses have advanced, so a ball dragged down by the descent is at its new Y now, and a
            //shot that spent the budget has had its landing. The two losses are checked here rather than only on a
            //landing, because a descent can push a ball across the death line between landings, and a spent budget
            //loses only once nothing remains in flight.
            //
            //Only the ENDINGS are held while a camera takeover runs — both put the result screen over this
            //one, and a level that ends mid-collapse (or before its own intro has even finished) takes the
            //moment the player earned off the screen before they have seen it, which is the same reason the
            //cleared countdown below waits. The floor alarm inside is NOT held: the release that engages a
            //drop cinematic is exactly the one that rescues a low cluster, and a warning frozen lit would
            //blaze across the player's reward for the whole dive down the drain.
            //On the SIMULATED seconds (#577): the line's grace judges how long the cluster stayed under it, and
            //below 30 FPS the world lives through less than the frame's wall time. Fed the wall clock, a swing
            //the world spent 0.8 s under took 1.2 s at 20 FPS and lost a level a fast machine survives - and the
            //level generator's sag gate, which runs this very rule, already counted simulated seconds.
            CheckLevelLost(_simulatedThisFrame, mayLose: !CameraTakeoverEngaged);

            //Where a shot fired now would land. After the step, so the ghost sits against the poses the player is
            //looking at rather than the ones from before this frame's physics.
            UpdateShotPreview();

            _smears.Update(elapsed);

            //The frame's own elapsed, UNSCALED by the cinematic's time scale, exactly as the smears above
            //take it: the shower answers a moment rather than running inside the simulation, and the line
            //loss's own cinematic does not slow time the way the drop's does anyway.
            _lineSparks.Update(elapsed);

            _hud.Update(elapsed, _run.Score);
            _hud.UpdateSkipHint(elapsed, _cinematic.Running, _cinematic.Elapsed);

            //The tutorial's card (#189): the settings row read here every frame, a camera takeover hiding it, and
            //a decided level ending it. A lesson just done is answered the way a point scored is — the HUD's own
            //kick, and the rating's chime at its root — because the card is a small dare and doing it wins it.
            _tutorial.Update(elapsed, Game.IsTutorialEnabled, CameraTakeoverEngaged, LevelDecided);

            if (_tutorial.TakePraiseCue())
            {
                _hud.KickTutorial();
                Game.Audio.PlayStarEarned(0, 1, 0f);
            }

            UpdateCamera(elapsed);

            //Last, because FinishLevel hands the player the result screen — pushed over this one, so the rest
            //of this frame still ran against a consistent session and nothing above it has torn anything down.
            //
            //A loss needs no entry here: LoseLevel shows the result screen straight away (the same one a clear
            //lands on), or StepLineLoss does when the line's hold ends.
            if (_phase == LevelPhase.ClearedBeat && !CameraTakeoverEngaged)
            {
                _clearedCountdown -= elapsed;
                if (_clearedCountdown <= 0f) FinishLevel();
            }
        }

        /// <summary>
        /// The frame this screen gets while the result page stands over it (#241). It used to get none: the
        /// page froze the session the way a pause does, and the arena the player had just won stopped dead
        /// behind the numbers — the cluster hanging perfectly still, the last of a collapse halted half way
        /// down the drain. A pause is a game put down mid-move and is right to freeze; an ending is the arena
        /// carrying on without a player.
        /// <para>
        /// <b>So what runs here is the world, not the game.</b> The simulation, and the gun's own answer to
        /// the last shot. Nothing belonging to <i>playing</i> does: no input or aim, no landing preview, no
        /// ceiling descent, no ending, no quality verdict — and above all no camera, because the page is
        /// itself easing the lens off the gun and out onto the front end's orbit, and two writers of one pose
        /// is one of them losing. The HUD is not stepped either, for the plainest of reasons: it is not drawn
        /// once the level is over (see <see cref="Draw"/>).
        /// </para>
        /// <para>
        /// <b>Leaving the rules out of this method is not what holds them.</b> Contacts are processed from
        /// <i>inside</i> the step, so a shot still in the air lands straight back into
        /// <see cref="OnBallLanded"/> whatever this method chose to call — and on a cleared field that would
        /// have re-fired the whole celebration while the countdown stood in for the phase (it had already run
        /// itself down to zero). <see cref="LevelOver"/> is what holds them, at each of those doors.
        /// </para>
        /// </summary>
        private void UpdateUnderResult(GameTime gameTime)
        {
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            //The gun settling. A loss lands on whatever frame the descent reached the line, so the tube can
            //still be mid-stroke and the queue mid-glide when the page arrives — and a gun frozen in front of
            //a cluster that is plainly still swinging is this very issue, one object further out. Wall clock,
            //as in the frame above: the recoil and the glide are the hardware answering the shot.
            _cannon.Update(gameTime);
            _cannon.StepRecoil(elapsed);
            _magazine.Step(elapsed);

            StepWildcards(elapsed);

            //Unscaled, unlike the frame above: a drop cinematic is the only thing that scales the step, and
            //neither ending is declared while one is engaged — the countdown freezes for it and the loss waits
            //on mayLose — so the scale is back at 1 before this page can exist.
            StepPhysics(elapsed);
            NoteSimulated(1f);

            //And a blast still going off when the page arrived (#389) carries on with the world it belongs to —
            //a flash frozen half-bright behind the numbers is this very issue, one effect further out. Its jolt is
            //dropped: the page is easing the lens out onto the front end's orbit, and the camera is not ours.
            _blasts.Update(elapsed, Game.Audio);
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

            //THE OVERLAY — the HUD and the crosshair — is display space, drawn after the resolve at the foot of
            //this method. Under a page's blur it is drawn into a LAYER instead (#438), so it softens with the
            //arena rather than staying pixel-sharp over a frame that has gone out of focus; and the layer is
            //the FIRST thing the frame draws, before the scene target is ever bound, for the rule the sharp
            //foreground layer learned the expensive way: a target is bound once a frame and the back buffer
            //last of all, because binding a discarded target clears it (see BS3DGame.BeginSceneDraw). The
            //blurred layer is composited back over the resolved frame exactly where the direct draw would
            //have gone, so nothing above or below it in the frame moves. Free on every unblurred frame, which
            //is all of play: the direct path is what it always was.
            bool overlayUp = !LevelOver;
            float overlayBlur = overlayUp ? OverlayBlur : 0f;
            bool overlayLayered = overlayBlur > 0f && Game.BeginOverlayLayer();

            if (overlayLayered)
            {
                DrawOverlay();
                Game.EndOverlayLayer(overlayBlur);
            }

            //This frame's ball collection, opened here and closed by the one Draw below. It is a ref struct and
            //lives as a LOCAL, deliberately: BeginFrame is the only thing that can open one, it empties the
            //buckets on the way and it throws if a second is opened before the first is drawn — which is what
            //makes "every ball is visited exactly once" structural rather than a rule to remember. The
            //occlusion ease, the attach glide and the ripple all advance state on the ball itself, so a second
            //visit would run all three at double speed while the buckets still looked perfectly correct.
            //Stated, not inherited: the set is shared with the front end's preview, which hangs whatever map it
            //rolled in whatever that map is made of (#258).
            Game.Balls.Style = _run.BallStyle;

            //And what a wildcard is crossing between this frame, stated on the same terms and for the same
            //reason (#330): the set is the whole program's, the crossing belongs to this session, and every
            //wildcard the frame draws — in the bore, at the muzzle, in the air — reads it from here, so no two
            //of them can show different colours at one instant.
            Game.Balls.SetWildcardCrossing(_wildcard.From, _wildcard.To, _wildcard.Progress);

            //THE MOTION BLUR (#402): this frame's gun poses, taken once here — the rounds in the bore about to be
            //collected are carried back by the barrel's motion, and the gun's own draw below uses the same two
            //matrices, so the tube and what it holds cannot disagree about where they were. Recorded only on a frame
            //that is blurred: the rest of the time nothing here runs and the collection is what it always was.
            Matrix barrelWorld = _cannon.BarrelWorld();
            Matrix carriageWorld = _cannon.CarriageWorld();

            _motionThisFrame = false;
            bool motion = Game.MotionBlurActive;
            Game.Balls.RecordMotion = motion;
            if (motion) PrepareMotion(barrelWorld, carriageWorld);

            BallDrawFrame ballFrame = Game.Balls.BeginFrame(Camera);

            //The cluster, the shots in flight and the released balls falling, each off its own body's pose — so
            //what the player is looking at IS the simulation. On the DRAW clock, since the ease, the glide and
            //the ripple are all purely about what is on screen.
            //
            //The last argument is the height above which a released ball that has STOPPED counts as dead
            //weight (#342): the field's own floor, so a group stuck up among the balls is marked and one
            //lying on the island's stone on its way into the drain is not.
            _clusterCollector.Collect(ballFrame, (float)gameTime.ElapsedGameTime.TotalSeconds,
                _physicsBalls, _shotBalls, _fallingBalls, _renderAlpha, _clusterWorldOffset.Y,
                motion ? BallShutterSeconds : 0f);

            //And the loaded queue into the very same frame — this screen's own loop, because the barrel's bore
            //and the transmute cross-fade are its own business
            CollectMagazineBalls(ballFrame);

            //And the ghost of where the loaded ball would land, into the same frame — it is a ball in the cluster's
            //own frame, so it is bucketed and LOD-picked with the rest rather than drawn by itself
            CollectShotPreview(ballFrame);

            //A blast's light on everything the shared instanced effect draws — the balls, the island, the gun —
            //stated for this frame only and before the scene lights are applied, which BeginSceneDraw does (#389).
            if (_blasts.TryGetLight(out Vector3 flashAt, out Vector3 flashColor, out float flashRange))
                Game.SetSceneFlash(flashAt, flashColor, flashRange);

            //The glass's own shadow on the island and the cluster under it (#553), consumed by the shadow pass
            Game.CastCeilingShadow(Game.CeilingRenderer, _ceiling.World);

            SceneFrame sceneFrame = Game.BeginSceneDraw();

            //Everything so far is the setting, which from under the plate is all that stands behind the ceiling's
            //glass: the copy it bends is taken here, before the gun and the cluster hang in front of it (#541).
            Game.GrabCeilingBackground(Game.CeilingRenderer, _ceiling.World);

            //The barrel, drawn with its recoil stroke: the pose is Cannon's and the hardware CannonRig's, so
            //the tube that was built and the bore a shot leaves from cannot disagree. The carriage under it
            //takes the stroke's own smaller, later share since #115 — the tube slides in the cradle and the
            //undercarriage lurches a beat behind it — and its wheels roll with everything that moves them,
            //the advance walk and that shove both (Cannon.WheelTravel).
            //Into a local (taken above, with the carriage's, for the motion blur) because the window's glazing is
            //drawn with the very same pose further down — it is set into this tube, so the one pose serves both
            //rather than being built a second time from a second read of the stroke, which is one more thing that
            //could ever come out differently.
            Game.CannonRig.Draw(Camera, barrelWorld, Game.SceneEffectParams);

            //The next round's colour, stated by the collar around the muzzle (#425). Opaque geometry drawn with
            //the barrel it rings, not with the additive marks below: it is part of the gun, so the cluster and
            //the carriage occlude it exactly as they occlude the tube, and nothing about what the player sees
            //depends on which way the gun happens to be turned.
            DrawMuzzleCollar(barrelWorld);
            Game.CannonRig.DrawCarriage(Camera, carriageWorld, _cannon.WheelTravel, _cannon.SlideTravel, Game.SceneEffectParams);

            //Everything collected above, as one instanced draw per ball type and LOD level — and the frame's
            //collection is closed by it. The heartbeat runs on the WALL clock: the balls go on breathing while
            //a pause has the session frozen, because it is what they are and not something they are doing.
            Game.Balls.Draw(WallClock);

            //Over the opaque scene (which the depth buffer now holds, so the cluster and the gun occlude
            //them) and additive, so they glow through the glare. It puts back exactly the states it found,
            //so the frame's translucent baseline still stands for the glass below.
            _smears.Draw(Camera);

            //In the smears' own slot and states, for the same reasons: additive over the opaque scene, so the
            //cluster and the gun occlude the shower and it blooms through the glare with them (#434).
            _lineSparks.Draw(Camera);

            //The aim beam in the smears' own slot and states — additive, depth-read — so the cluster and the gun
            //occlude it and it blooms through the glare with them. After the smears rather than before for one
            //reason: a shot's flare should sit over the guide that aimed it, not under it.
            DrawShotPreviewBeam();

            //The blasts (#389), in the same slot and the same states: additive and depth-read, so what stands
            //nearer the lens than a blast hides it, and before the drain's glass so the funnel composites over a
            //blast seen down its throat exactly as it does over a smear.
            _blasts.Draw(Camera);

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
            //it should be seen through has to be in the depth buffer and the frame already — and, since #299,
            //so it must not WRITE depth itself, or the victory fireworks burst behind an invisible wall. That
            //is why this goes through the host rather than touching CeilingRenderer directly; the state and the
            //reasoning live in BS3DGame.DrawCeilingGlass, in one copy with the front end's preview plate.
            //Squared, so the glass is unmistakable on the frame it steps and has thinned well before the slide
            //ends — it marks the event rather than colouring the plate for the duration
            Game.CeilingRenderer.EmissiveTint = _ceilingDescent.FlashColor * (_ceilingDescent.Flash * _ceilingDescent.Flash);

            Game.DrawCeilingGlass(Game.CeilingRenderer, _ceiling.World);

            //And the gun's own glass after all of them, because it is far and away the nearest translucent
            //surface in the frame: the loaded queue it covers is in the depth buffer by now, and so are the
            //drain's cone and the descending plate the barrel is seen against — composited first, this pane
            //would let both of those bleed through it. Drawn with the barrel's own pose, recoil and all.
            Game.CannonRig.DrawGlass(Camera, barrelWorld, Game.SceneEffectParams);

            //The velocity pass rides the frame's close: after the last of the scene and before the resolve, which is
            //the one moment it can bind a target of its own without costing the scene target a second resolve
            Game.FinishSceneDraw(sceneFrame, _drawMotionVelocity);

            //Display space from here down: the resolve is the frame's one and only exit from linear light,
            //and the overlay and the FPS overlay (a component, drawn in base.Draw after this) are sRGB.
            //Not once the level is over. The result screen states the score itself, so the in-play readout
            //behind it is the same figure said twice — and worse, the corners are the only thing on the frame
            //that would still be pinned to the screen while the camera is released and swings out around the
            //arena. An award caught mid-flight is the visible half of that: it ages on the play clock, which
            //is not stepped under the page (#241 runs the world there, not the readouts), so it would hang
            //frozen and be reprojected against a moving camera, sliding across the frame on its way to a
            //score nobody is playing for any more.
            //
            //Blurred under a page, the overlay was drawn into its layer at the top of this method, and what
            //goes here is the layer softened (#438); sharp, it is drawn straight onto the frame as ever.
            if (overlayLayered) Game.CompositeOverlayLayer(overlayBlur);
            else if (overlayUp) DrawOverlay();
        }

        /// <summary>
        /// The session's display-space overlay: the HUD and the crosshair, into the host's batch — onto the
        /// back buffer after the resolve on a sharp frame, into the pipeline's overlay layer under a page's
        /// blur (#438). One method for both, so the two cannot drift apart in what they draw; where it lands
        /// is the caller's business, and what is bound when it is called.
        /// </summary>
        private void DrawOverlay()
        {
            PlayHud.ClusterProfile profile = BuildClusterProfile(out int ballCount);

            //Through LoadedColour, so a wildcard's disc cycles with the ball it stands for (#330). The strip
            //is the only place the queue can be read while the eye is on the cluster, so it is exactly where
            //the two must not disagree — a disc showing the dealt colour under a wildcard would be a wrong
            //answer in the one readout built to be trusted at a glance (#236).
            for (int i = 0; i < _magazineQueue.Length; i++) _magazineQueue[i] = LoadedColour(i);

            _hud.Draw(_run.Score, Camera, in profile,
                new ReadOnlySpan<PlayHud.BallMarker>(_profileBalls, 0, ballCount),
                _magazineQueue, _tutorial);

            //The crosshair, into the host's overlay batch (the one the HUD above just used): shown only while
            //precise aim is leaning in, that being the only pose whose lens looks along the shot, and faded up
            //with the lean rather than snapped on. The gate below a hundredth is the component's own.
            //Reddened only when the refusal is CERTAIN — the aim reaches a ball and neither ring around it has a
            //free cell. Aiming at open sky leaves it neutral, because a miss the player can already see does not
            //need a warning, and a warning that cries wolf is one nobody reads. Note the opacity is the ADS blend,
            //so this mark exists only while the lens looks along the bore; the overview's signal is the ghost.
            //
            //And not once the level is over — the same gate the HUD above is behind (the caller's overlayUp), and
            //for a reason of its own besides: the blend this fades on is frozen under the result page, because
            //UpdateUnderResult runs the world and not the game (#241) and nothing there steps the lean out, so a
            //loss taken mid-hold left the reticle parked at full opacity over the numbers for as long as the page
            //stood (#259). A cut rather than a fade, like the aim blur's at the same moment — the page owns the
            //frame from there on, and a lens nobody is aiming is not a thing to keep. A PAUSE taken mid-hold
            //freezes the blend the same way, and there the reticle stays — softened with the rest of the overlay
            //under the page's blur (#438), which is what a lens the player let go of should look like.
            //
            //And it blinks red while the player pushes the aim into the elevation clamp (#431), which the beam
            //says in the overview the same way — see AimStrain.
            _crosshair.Draw(Game.OverlayBatch, _preciseAim.Blend,
                _previewReachesCluster && !_previewHasCell ? PREVIEW_REFUSED : null,
                AimStrain, WallClock);
        }

    }
}
