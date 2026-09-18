using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Prazsky.Core.Screens;
using Prazsky.Core.Tools;
using System;
using System.IO;

namespace BS3D.Screens
{
    /// <summary>
    /// The setting the front end stands in, at the <b>bottom of the stack</b> for the whole life of the
    /// program. Every menu page sits over it with <see cref="Screen.DrawsUnderlying"/>, so "a menu with no
    /// session still shows the world" is the stack working rather than a special case in the host's
    /// <c>Draw</c>; while a level is being played the <see cref="GameplayScreen"/> above it draws the
    /// setting itself (its gameplay is <i>interleaved</i> with it), and this screen lies dormant underneath.
    /// <para>
    /// Its update is the front end's motion: the camera's flight around the scene, and the adaptive quality
    /// probe that watches the frame rate while the menu is what is being drawn. Both stop the moment a session
    /// is on the stack, because the pages above only let this screen update while no game stands over it.
    /// </para>
    /// <para>
    /// Since #249 the setting it draws is not empty of the game: a random level's map hangs over the island
    /// — no cannon, no physics, just the cluster and the glass over it — so the player sees at launch what a
    /// juicy map awaits them to shoot apart. The map is drawn again at random on every return to the front
    /// end (<see cref="BS3DGame.ReturnToMainMenu"/>).
    /// </para>
    /// <para>
    /// And since #254 the camera does more than turn: it is <b>framed for the map that is actually hanging</b>
    /// (see <see cref="FrameOrbitFor"/>) and it <b>flies in to look at it</b> once a cycle (see
    /// <see cref="AdvanceOrbit"/>). Both halves answer the same complaint — a preview hung under its glass and
    /// then watched from a fixed 44 units away through a 60 degree lens reads as a speck floating short of the
    /// ceiling, whatever size the map is.
    /// </para>
    /// <para>
    /// And since #408 the camera is <b>never inside the map</b>: the pass keeps a floor of air off the
    /// cluster's bounding sphere, and a map the level picker asks to hang while the lens is in among the
    /// balls of the one before it waits until the lens has backed out of where it would stand — see
    /// <see cref="StepPreviewRequest"/>.
    /// </para>
    /// </summary>
    internal sealed class BackdropScreen : Screen
    {
        private readonly BS3DGame Game;

        //Its own generator, in the manner of the host's own private RANDOM: the menu's scene pick and its
        //map pick have no reason to share a stream.
        private static readonly Random RANDOM = new();

        //The preview the front end hangs: pure data, rolled at random out of the level set. The offset is
        //the very one a session would derive for the same map, so the cluster sits where it will sit when
        //played — and the glass is a pose rather than a body, because nothing ever steps this ceiling down.
        private BallsMap _previewMap;

        //What the rolled map's balls are made of (#258). The front end and a session share one render set, so
        //this is what this screen states before it draws rather than what it hopes is still standing there.
        private BallStyle _previewStyle = BallStyle.Beach;

        private Vector3 _previewOffset = Vector3.Zero;
        private Matrix _menuCeilingWorld = Matrix.Identity;

        public BackdropScreen(BS3DGame game)
        {
            Game = game;

            //Rolled here rather than on first draw: the level set is loaded and the sky is up by the time the
            //host builds this screen, so the very first frame the front end ever shows already carries the
            //promise.
            RollPreviewMap();
        }

        #region How the hanging map is framed

        /// <summary>
        /// The measurements of the map hanging over the island — how wide it reaches about the orbit's axis,
        /// how tall it is, where its middle is, and the two heights the fly-in cranes between.
        /// <para>
        /// <b>Measurements and not stand-offs</b>, deliberately: which frustum axis a map is framed by flips
        /// with the shape of the window (the vertical FOV is what <c>CreatePerspectiveFieldOfView</c> takes, so
        /// a wider display only adds width), and a radius solved at load would be framing the shape of a window
        /// that a fullscreen switch has since changed. The stand-offs come off these every frame instead — see
        /// <see cref="WideRadius"/> and <see cref="CloseRadius"/>, which is also where the arithmetic that
        /// turns an extent into a distance is written once.
        /// </para>
        /// <para>
        /// <b>Solved per map rather than stated</b> (#254), because the shipped set runs from a four-level
        /// pancake to a twenty-four-level column, and a deep field is hung higher than a shallow one on top of
        /// that (<see cref="GameplayScreen.FitClusterWorldOffset"/> raises a field whose floor would otherwise
        /// start past the death line, which puts <c>Helix</c>'s top level eleven units above <c>Nine</c>'s).
        /// One fixed radius and one fixed aim height cannot hold both ends of that: the figures they replaced
        /// framed neither, and looked most wrong on the smallest maps, which is where the complaint came from.
        /// </para>
        /// </summary>
        private readonly struct OrbitFraming
        {
            /// <summary>
            /// How far the field's footprint reaches from the orbit's own axis — its circumradius, which is
            /// exactly the widest it ever presents as the camera goes round it (an axis-aligned box of half
            /// extents a and b shows <c>a·|sin θ| + b·|cos θ|</c>, largest at the diagonal).
            /// </summary>
            public readonly float SpanXZ;

            /// <summary>Half the hanging cluster's own height, ball surface to ball surface.</summary>
            public readonly float HalfHeight;

            /// <summary>The middle of the hanging cluster in world Y: what both legs aim at.</summary>
            public readonly float CentreY;

            /// <summary>Where the fly-in starts its crane, under the cluster looking up at its underside.</summary>
            public readonly float UnderY;

            /// <summary>Where it ends it, just over the top of the cluster with the glass in frame.</summary>
            public readonly float OverY;

            /// <summary>
            /// The bounding sphere about <see cref="CentreY"/>: what a clearance has to be measured off, since
            /// it is the one figure that bounds the cluster from every bearing and every height at once.
            /// </summary>
            public float Span => MathF.Sqrt(SpanXZ * SpanXZ + HalfHeight * HalfHeight);

            public OrbitFraming(float spanXZ, float halfHeight, float centreY, float underY, float overY)
            {
                SpanXZ = spanXZ;
                HalfHeight = halfHeight;
                CentreY = centreY;
                UnderY = underY;
                OverY = overY;
            }

            /// <summary>
            /// What the front end frames with no map hanging at all — the no-readable-level fallback. The
            /// drain's mouth stands in for the cluster, because with nothing hanging over the island the hole
            /// in the middle of it is what the flight is going round; the heights are the ones the fixed orbit
            /// used before any of this was solved.
            /// </summary>
            public static readonly OrbitFraming Bare =
                new(ArenaIsland.FUNNEL_TOP_RADIUS, 0f, 5f, -1f, 9f);

            public static OrbitFraming Lerp(in OrbitFraming from, in OrbitFraming to, float amount) => new(
                MathHelper.Lerp(from.SpanXZ, to.SpanXZ, amount),
                MathHelper.Lerp(from.HalfHeight, to.HalfHeight, amount),
                MathHelper.Lerp(from.CentreY, to.CentreY, amount),
                MathHelper.Lerp(from.UnderY, to.UnderY, amount),
                MathHelper.Lerp(from.OverY, to.OverY, amount));
        }

        //What the camera is flying to today, and what it is drifting towards. Two of them rather than one
        //because the map under the camera CHANGES while the camera is up: every return to the front end rolls
        //a new preview (BS3DGame.ReturnToMainMenu), and a solved framing applied on the frame it lands would
        //be a cut in radius and aim height on a camera that is otherwise never cut. It drifts instead.
        private OrbitFraming _framing = OrbitFraming.Bare;
        private OrbitFraming _framingTarget = OrbitFraming.Bare;
        private bool _framingSet;

        //How quickly it drifts, as the time constant of an exponential approach — about 95 % of the way there
        //in three times this. Off elapsed seconds and not a frame count, like everything else here.
        private const float FRAMING_EASE_SECONDS = 1.2f;

        //And how quickly it drifts while a map is WAITING to hang because the lens stands where its balls
        //would be (#408, see StepPreviewRequest): brisk, so the player who stopped on a tile sees its level
        //within a second rather than watching the camera amble out of the way. Still an ease and not a cut.
        private const float CLEARING_EASE_SECONDS = 0.3f;

        //What the front end actually has hanging, as a framing — where the drift goes back to if a map that
        //was waiting to hang is dropped before it did (the picker's focus moved on, or a session took over).
        private OrbitFraming _hungFraming = OrbitFraming.Bare;

        //=== HOW MUCH OF THE FRAME EACH LEG FILLS ===

        //THE WIDE LEG'S JOB IS THE WHOLE SCENE, and the island is what measures it: a disc of radius R sits
        //wholly inside a frustum of half-angle t at any distance past R / sin(t), so this is that distance
        //with the share held off the frame's own edge. The HORIZONTAL half-angle is the one that binds on any
        //window wider than it is tall, which is why the aspect is read every frame rather than baked in — the
        //44 this replaces was one number for every window shape, and it left about 15 % of a widescreen
        //frame's width on the table. It comes out near 38 units at 16:9, 40 at 16:10, 45 at 4:3, 34 at 21:9.
        private const float ISLAND_FRAME_SHARE = 0.95f;

        //THE WIDE LEG'S OTHER JOB IS NOT TO CROP THE MAP, which is a guarantee rather than a framing rule: no
        //shipped level reaches it — the tallest column and the widest footprint both solve to about 23 units
        //against the 38 the island asks for — and it exists for the hand-built map deeper or broader than
        //anything in the set. Deliberately generous, because a wide leg pulled out to frame a monster map
        //tightly would be showing less of the scene it is there to establish.
        private const float WIDE_MAP_SHARE = 0.72f;

        //THE FLY-IN'S JOB IS THE MAP AT BALL-FILLING DETAIL, and the share states how much of the frame's
        //half-angle the map is asked to overfill — 1.6 of it, enough that the fit below is never what holds
        //a pass back on any plausible map and the clearance floor is the whole answer. The old 0.8 framed
        //the map as an object with its glass in shot; the owner's ruling on that pass was that the balls
        //never came into detailed view, and detail is what an over-full frame is.
        private const float CLOSE_MAP_SHARE = 1.6f;

        //And what the fly-in may not do, whatever the arithmetic above asks for: come nearer the cluster than
        //this. A unit and a half of air off the cluster's bounding SPHERE — a floor on the distance to any
        //ball at any bearing and any point of the crane, and margin for the near plane with it. The sphere
        //bounds the FIELD's footprint too, so the same floor keeps the lens outside the glass plate by as
        //much again at the top of the crane.
        //
        //This used to be 10, and for a reason that is gone. The 3D wordmark hung 7 units in front of the lens
        //with depth writes on (TitleWordmark.DISTANCE) and its far corners reached about 8.5 out, so a ball
        //nearer than that was a ball drawn THROUGH the game's own name — and the clearance, not the map fit,
        //was what held nearly every shipped level's pass at 21-24 units (#254). The title has stepped aside
        //for the pass ever since — shrunk to a small corner mark — and the floor fell to what only the lens
        //needs: the owner's ruling on the first version of that (#261) was that the title should not vanish
        //but stay small, and a ball passing IN FRONT of a small corner mark is honest parallax rather than
        //the broken-looking name a full-size one showed.
        private const float CLOSE_CLEARANCE = 1.5f;

        //How far either way a cycle rolls its clearance (#261's "one pass skims closer among the balls than
        //the last", #408's home for it): a fraction of CLOSE_CLEARANCE, so the air runs 0.9 to 2.1 units.
        //It was a scale of 0.95–1.05 on the whole stand-off until #408, and that ate the floor rather than
        //jittering around it — at 0.95 of (span + 1.5) a wide map kept a third of a unit — which was also
        //what made the deferred hang below unreachable on such a map: it waits for half the floor.
        private const float CLOSE_CLEARANCE_JITTER = 0.4f;

        //How much air a map has to have off the lens before it is HUNG (#408) — half the pass's floor, and
        //the two are different questions. The floor is where the pass settles; this is only whether a ball
        //of the incoming map could stand at the lens on the frame it appears, while the drift is still
        //carrying the lens out to the full figure. Below the smallest clearance a cycle can roll, so a
        //waiting map always gets hung.
        private const float HANG_CLEARANCE = CLOSE_CLEARANCE * 0.5f;

        //=== THE HEIGHTS ===

        //How far under its target the lens sits on the wide leg. Small, so the frame stays a look ACROSS the
        //scene rather than down onto it — which is what shows most of a city, a sea or a mountain range at
        //once — and the same 2 units the fixed pose had, now measured off the map's own middle instead of off
        //a constant that only suited a map of one depth.
        private const float WIDE_LENS_DROP = 2f;

        //Where the fly-in's crane starts and ends, measured off the cluster's own bottom and top ball. Under
        //it first, because the underside is the face that says the thing hangs — the sky is behind it and the
        //island below the lens — and over it last, where the top balls and the glass they hang from are in the
        //same frame and the gap between them is finally readable at all.
        private const float CRANE_UNDER_CLUSTER = 2f;
        private const float CRANE_OVER_CLUSTER = 1.5f;

        //The floor under all of it: the deepest fields hang their bottom level a hand's breadth over the death
        //line, so a crane that started CRANE_UNDER_CLUSTER below THAT would put the lens through the island's
        //stone. Held this far over the arena's top face instead, which is a low, near-grazing look up at the
        //cluster rather than a shot from inside the rock.
        private static readonly float LENS_FLOOR_Y = ArenaIsland.TOP_Y + 4f;

        /// <summary>
        /// Solves how the camera frames one hanging map, and hands the result to the flight as the framing to
        /// drift towards. Run once per map — as the front end rolls a preview, and as a session installs a
        /// level, since the fly-out at the end of a level (<see cref="ResultPage"/>) flies onto this same orbit
        /// and would otherwise be framing whatever map the MENU last rolled rather than the one the player has
        /// just finished.
        /// <para>
        /// <b>The cluster's own extent, not the field's.</b> The empty levels at the bottom of a field are
        /// growth room for shot balls and are what <see cref="GameCameraFit"/> must frame — a ball is going to
        /// land there. Nothing lands in a preview, so framing them would be framing several units of nothing
        /// and pushing the map that is actually there up into the top of the shot. The footprint IS the
        /// field's, off <see cref="CeilingPlate.FootprintFor"/> like every other reader of it: that is what
        /// the glass covers, and the glass is part of what a fly-in is looking at.
        /// </para>
        /// </summary>
        /// <param name="map">The map hanging over the island, centred.</param>
        /// <param name="topLevelY">
        /// World Y the field's topmost level of balls actually <b>hangs</b> at — which is
        /// <see cref="BallsConstraintsBuilder.CeilingRestY"/> of the plate's centre and not the lattice height
        /// <see cref="GameplayScreen.FitClusterWorldOffset"/> hands back, since a cluster held by the ceiling
        /// socket settles a diameter under that plate. Both callers pass the settled figure; passing the
        /// lattice one would frame the map a unit below where it is drawn.
        /// </param>
        internal void FrameOrbitFor(BallsMap map, float topLevelY)
        {
            //A session installing its level supersedes whatever the picker was still waiting to hang (#408):
            //the front end is about to go dormant, and the map it comes back to is rolled afresh anyway
            DropPending();

            if (map == null)
            {
                _hungFraming = OrbitFraming.Bare;
                SetFraming(OrbitFraming.Bare);
                return;
            }

            ApplyFraming(SolveFraming(map, topLevelY), map);
        }

        /// <summary>
        /// Takes a solved framing as the one the front end has hanging and the one the flight drifts towards,
        /// and writes the one <c>[orbit]</c> line — at the window's CURRENT shape, which is what makes it a
        /// reading of the fit rather than a restatement of it: the two stand-offs are solved per frame and
        /// move with a resize.
        /// </summary>
        private void ApplyFraming(in OrbitFraming framing, BallsMap map)
        {
            _hungFraming = framing;
            SetFraming(framing);

            Console.WriteLine($"[orbit] framed for {map.GetBallsCount()} balls:"
                + $" reach {framing.SpanXZ:F1} x {framing.HalfHeight:F1}"
                + $", aim y {framing.CentreY:F1}"
                + $", wide {WideRadius(framing):F1}"
                + $", close {CloseRadius(framing, CLOSE_CLEARANCE):F1}"
                + $", crane {framing.UnderY:F1} to {framing.OverY:F1}");
        }

        /// <summary>
        /// The measurements of one hanging map (see <see cref="OrbitFraming"/>), solved and not yet applied —
        /// so a map that has to wait before it hangs (#408) can be measured for the wait.
        /// </summary>
        private static OrbitFraming SolveFraming(BallsMap map, float topLevelY)
        {
            XZLevel size = map.GetStaticBallsArraySize();
            byte topLevel = (byte)(size.Level - 1);

            //Ball surfaces rather than ball centres at both ends: the fit is a bounding volume, and half a
            //unit is a tenth of the smallest cluster's whole height.
            float topY = topLevelY + BallRenderSet.BALL_RADIUS;
            float bottomY = topLevelY
                - (topLevel - map.GetLowestOccupiedLevel()) / Constants.SQRT_TWO
                - BallRenderSet.BALL_RADIUS;

            float centreY = (topY + bottomY) * Constants.HALF;

            //The bounding SPHERE of what hangs, about the point both legs aim at. A sphere because the camera
            //orbits: any box would present a different width at every bearing, and a fit that breathed once a
            //turn is the one thing worse than a fit that is too far out.
            float halfX = CeilingPlate.FootprintFor(map.StageSizeX) * Constants.HALF;
            float halfZ = CeilingPlate.FootprintFor(map.StageSizeZ) * Constants.HALF;

            return new OrbitFraming(
                MathF.Sqrt(halfX * halfX + halfZ * halfZ),
                (topY - bottomY) * Constants.HALF,
                centreY,
                MathF.Max(bottomY - CRANE_UNDER_CLUSTER, LENS_FLOOR_Y),
                topY + CRANE_OVER_CLUSTER);
        }

        /// <summary>
        /// Takes a solved framing as the one to drift towards — and as the one the camera is already at, if
        /// this is the first map to hang here at all. There is nothing to ease from on the frame the program
        /// starts on, and easing anyway would open the front end on a pose the arithmetic never asked for.
        /// </summary>
        private void SetFraming(OrbitFraming framing)
        {
            _framingTarget = framing;

            if (!_framingSet)
            {
                _framing = framing;
                _framingSet = true;
            }
        }

        /// <summary>
        /// The frame's two half-angles at the window's current shape. The vertical one is the lens's own —
        /// <c>CreatePerspectiveFieldOfView</c> takes the vertical FOV, so a wider display adds width and
        /// nothing else — and the horizontal one follows from it and the aspect. Which of the two binds a fit
        /// flips with the window, which is why every fit here asks for both.
        /// </summary>
        private void FrameHalfAngles(out float vertical, out float horizontal)
        {
            vertical = FOV * Constants.HALF;
            horizontal = MathF.Atan(MathF.Tan(vertical) * Game.Camera.AspectRatio);
        }

        /// <summary>
        /// How far out an object of half-extent <paramref name="reach"/> has to be watched from to fill
        /// <paramref name="share"/> of a frame half-angle: the tangent-line condition, written once because
        /// four fits here want it.
        /// </summary>
        private static float StandOffFor(float reach, float halfAngle, float share) =>
            reach / MathF.Sin(halfAngle * share);

        /// <summary>
        /// The wide leg's stand-off: the furthest of the three things it has to hold at once — the island
        /// whole across the frame, and the hanging map uncropped on either frustum axis.
        /// </summary>
        private float WideRadius(in OrbitFraming framing)
        {
            FrameHalfAngles(out float vertical, out float horizontal);

            return MathF.Max(
                StandOffFor(ArenaIsland.RADIUS, horizontal, ISLAND_FRAME_SHARE),
                MathF.Max(
                    StandOffFor(framing.HalfHeight, vertical, WIDE_MAP_SHARE),
                    StandOffFor(framing.SpanXZ, horizontal, WIDE_MAP_SHARE)));
        }

        /// <summary>
        /// The fly-in's stand-off: as near as the map can be framed on the axis that binds, held off by
        /// <paramref name="clearance"/> — the cycle's roll about <see cref="CLOSE_CLEARANCE"/>, which is what
        /// actually decides it on nearly every shipped level. The clearance is the parameter and not the
        /// figure, so that a cycle's roll can only ever move the pass INSIDE the floor's own range (#408).
        /// </summary>
        private float CloseRadius(in OrbitFraming framing, float clearance)
        {
            FrameHalfAngles(out float vertical, out float horizontal);

            return MathF.Max(
                framing.Span + clearance,
                MathF.Max(
                    StandOffFor(framing.HalfHeight, vertical, CLOSE_MAP_SHARE),
                    StandOffFor(framing.SpanXZ, horizontal, CLOSE_MAP_SHARE)));
        }

        /// <summary>
        /// Whether the lens, where the flight last put it, stands clear of a map framed as
        /// <paramref name="framing"/> by <see cref="HANG_CLEARANCE"/> — outside its bounding sphere by that
        /// much, which is the one test that holds from every bearing and every height of the crane at once.
        /// The pose is last frame's, since the request is stepped before the flight; a frame's movement is
        /// nothing against the margin.
        /// </summary>
        private bool LensClearOf(in OrbitFraming framing) =>
            Vector3.Distance(_lens, new Vector3(0f, framing.CentreY, 0f)) >= framing.Span + HANG_CLEARANCE;

        #endregion

        #region The flight

        //The menu camera orbits the scene's origin in XZ. The angle is advanced by elapsed seconds, never by
        //a frame count, so the turn takes the same time on any machine.
        private float _angle;

        //And how far into the current fly-in cycle it is, on that same clock and for that same reason.
        private float _flightClock;

        //Where the flight last put the lens — what a map waiting to hang is measured against (#408). The
        //orbit's own pose and not the camera's: under the result page the camera is a blend of this and the
        //gun's, and the question is where the FLIGHT stands, since that is what the hang would land under.
        private Vector3 _lens = new(0f, 0f, 1e4f);

        //=== THE CYCLE, ROLLED FRESH AT THE TOP OF EVERY ONE ===
        //
        //The front end's camera is not a carousel: it holds the wide establishing turn for a stretch, then
        //leaves it, comes in over the island, cranes along the hanging map while it circles, and backs out
        //again (#254 — "the camera just flies around the scene and looks at it"). Then it does it again,
        //from wherever the bearing has reached by then — and since the owner's follow-up ruling on #261 no
        //two arrivals are shaped alike either: each leg's length is rolled within the range its constant
        //below names (the first figure is the floor, the second the jitter above it), the crane sets off
        //over the top as often as from under the bottom, and this pass's stand-off jitters a few per cent
        //either way, so the same map is never visited the same way twice.
        //
        //THE WIDE LEG'S ROLL HAS A FLOOR, and the reason is not variety: BS3DGame.TuneQualityToFrameRate is
        //driven off this screen's Update, and it reaches a verdict from a 1.5 s warm-up and 1.5 s windows —
        //so the whole probe settles inside the first ten seconds of a front end, and the wide roll STARTS at
        //twelve so that every verdict is a wide one (the jitter only ever lengthens the hold; #261 cut the
        //hold itself from 30, which stood three times longer than the ask it was serving). A cycle that flew
        //in sooner would be handing the probe a frame with the cluster filling it and letting THAT decide
        //the tier the whole game runs at. Every route that reopens the probe from the front end puts the
        //flight back at the top of the wide leg with it: a fresh program, a rolled preview (returning from a
        //level, which is what ReopenQualityProbe answers) and a release from the result screen all start the
        //clock at zero. The one that does not is the fullscreen toggle, which reopens the probe wherever the
        //flight has got to — measured, the pass is not the expensive half anyway (see the frame-rate figures
        //under "The menu camera" in docs/game-shell.md), and the probe only ever steps down.
        private const float WIDE_SECONDS = 12f, WIDE_JITTER_SECONDS = 8f;
        private const float APPROACH_SECONDS = 3.5f, APPROACH_JITTER_SECONDS = 1.5f;
        private const float CLOSE_SECONDS = 16f, CLOSE_JITTER_SECONDS = 8f;
        private const float RETREAT_SECONDS = 7f, RETREAT_JITTER_SECONDS = 3f;

        //This cycle's rolled legs and its two rolls of the dice. The initial values are the floors, so the
        //very first frames of a program fly a sane cycle even before RollCycle has been near them — though
        //the constructor's RollPreviewMap resets the clock and rolls one anyway.
        private float _wideSeconds = WIDE_SECONDS;
        private float _approachSeconds = APPROACH_SECONDS;
        private float _closeSeconds = CLOSE_SECONDS;
        private float _retreatSeconds = RETREAT_SECONDS;

        //Whether this pass's crane starts over the top of the map (and comes down it) or from under the
        //bottom (and climbs), and how much air this pass leaves off the balls — CLOSE_CLEARANCE rolled
        //within CLOSE_CLEARANCE_JITTER either way, so one pass skims closer among the balls than the last
        //one did, and none of them closer than the floor allows.
        private bool _craneFromOver;
        private float _closeClearance = CLOSE_CLEARANCE;

        private float ExcursionSeconds => _approachSeconds + _closeSeconds + _retreatSeconds;
        private float CycleSeconds => _wideSeconds + ExcursionSeconds;

        //How small the 3D title gets while the flight is in among the balls (its `presence` floor, #261):
        //not away to nothing — the owner's ruling on the first version was that it should stay, small, in
        //its corner — but to a third of its size, where it reads as a modest corner mark instead of the
        //frame-dominating name. At this scale a ball nearer the lens than the block's 7-unit hang does pass
        //in front of it, which is honest parallax; the full-size name is what looked broken (#254).
        private const float WORDMARK_ASIDE_SCALE = 0.35f;

        //About a full turn every 90 s out wide: slow enough to read as ambience rather than as a turntable.
        //The fly-in turns half again as fast — but at little more than half the radius, so it crosses the
        //frame SLOWER than the wide leg does while still visibly moving: a camera that has come in to look at
        //something and slowed down to do it. Holding the wide leg's angular rate through the pass instead
        //would have read as the flight stalling the moment it arrived.
        private const float WIDE_ROTATION_SPEED = MathHelper.TwoPi / 90f;
        private const float CLOSE_ROTATION_SPEED = MathHelper.TwoPi / 60f;

        private static readonly float FOV = MathF.PI / 3f;  //60°: wide, to take in the scene behind the panel

        /// <summary>
        /// Rolls the next cycle's shape: the four leg lengths within their ranges, whether the crane sets off
        /// over the top of the map or from under it, and this pass's stand-off within a few per cent. Called
        /// at the top of every cycle, and wherever the flight is put back onto a wide leg with the clock at
        /// zero — a fresh program, a rolled preview, a release off the result screen.
        /// </summary>
        private void RollCycle()
        {
            _wideSeconds = WIDE_SECONDS + (float)RANDOM.NextDouble() * WIDE_JITTER_SECONDS;
            _approachSeconds = APPROACH_SECONDS + (float)RANDOM.NextDouble() * APPROACH_JITTER_SECONDS;
            _closeSeconds = CLOSE_SECONDS + (float)RANDOM.NextDouble() * CLOSE_JITTER_SECONDS;
            _retreatSeconds = RETREAT_SECONDS + (float)RANDOM.NextDouble() * RETREAT_JITTER_SECONDS;
            _craneFromOver = RANDOM.NextDouble() < 0.5;
            _closeClearance = CLOSE_CLEARANCE * (1f + (2f * (float)RANDOM.NextDouble() - 1f) * CLOSE_CLEARANCE_JITTER);
        }

        /// <summary>
        /// Where in the excursion the camera is, as one reversible scalar: 0 out on the wide turn, 1 in on
        /// the map. Smoothstepped at both ends, so the flight leaves the wide leg at rest and arrives at rest — the
        /// same shape precise aim, the drop cinematic and the result screen's own release are built on.
        /// </summary>
        private float Closeness(float clock)
        {
            float since = clock - _wideSeconds;

            if (since <= 0f) return 0f;
            if (since < _approachSeconds) return MathHelper.SmoothStep(0f, 1f, since / _approachSeconds);

            since -= _approachSeconds;
            if (since < _closeSeconds) return 1f;

            return MathHelper.SmoothStep(1f, 0f, (since - _closeSeconds) / _retreatSeconds);
        }

        /// <summary>
        /// How far up the crane is, 0 under the cluster and 1 over it, run across the <b>whole</b> excursion
        /// rather than only its close leg. Over the whole of it because the rise is what stops the pass reading
        /// as a second orbit: the camera is climbing (or descending — half the rolls start it over the top,
        /// #261) the map for the entire time it is anywhere near it, and it is moving fastest in the middle of
        /// the close leg, where the smoothstep is steepest.
        /// <para>
        /// It jumps back to its starting end at the end of the cycle, whichever end that is, and that is not
        /// a discontinuity anyone can see: <see cref="Closeness"/> is exactly 0 there, so the height it
        /// feeds is not being mixed in at all.
        /// </para>
        /// </summary>
        private float Rise(float clock)
        {
            float rise = MathHelper.SmoothStep(0f, 1f,
                MathHelper.Clamp((clock - _wideSeconds) / ExcursionSeconds, 0f, 1f));

            return _craneFromOver ? 1f - rise : rise;
        }

        #endregion

        /// <summary>
        /// The front end's flight around the scene, on the game's own <see cref="RecoilCamera"/> with its shake
        /// at rest — nothing kicks it here. Runs whenever no <see cref="GameplayScreen"/> is on the stack (the
        /// menu pages pass updates down exactly then), which is also when the adaptive quality probe is a fair
        /// measurement: the menu draws the same city, clouds, glare and tonemap the game does — and, since
        /// #249, shades a real cluster, so the verdict the probe reaches already includes the balls.
        /// </summary>
        public override void Update(GameTime gameTime)
        {
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            //The level picker's request, once its focus has rested (#405) — before the flight, so a map hung this
            //frame is the one the framing starts drifting towards on the same frame.
            StepPreviewRequest(elapsed);

            AdvanceOrbit(elapsed, out Vector3 position, out Vector3 target, out float fieldOfView);

            RecoilCamera camera = Game.Camera;

            camera.BasePosition = position;
            camera.BaseTarget = target;
            camera.FieldOfView = fieldOfView;

            camera.Update(elapsed);

            Game.TuneQualityToFrameRate(elapsed, "menu");
        }

        /// <summary>
        /// Advances the flight and hands back the pose it has reached, without touching the camera.
        /// <para>
        /// Shared with <see cref="ResultPage"/>, which flies the camera out onto this very flight when a level
        /// ends. <b>One flight and one angle</b>, deliberately: a second orbit of its own would leave the front
        /// end at some unrelated bearing, so pressing "Main Menu" off the result screen would cut to a
        /// different view of the same arena. Sharing it makes that a continuation — the angle the result
        /// screen leaves is the angle this screen picks up, and the pose at the end of its ease is the pose
        /// this screen would have set on its own next frame.
        /// </para>
        /// <para>
        /// That contract survived the fly-in (#254) because the fly-in lives in <i>here</i>, with the orbit,
        /// rather than beside it: whichever screen is asking, one clock advances and one pose comes back. What
        /// it needed on top was <see cref="AlignOrbitTo"/> putting a release at the start of the wide leg, so
        /// what a result screen eases onto is the establishing turn and never the middle of a pass at the map.
        /// </para>
        /// </summary>
        internal void AdvanceOrbit(float elapsed, out Vector3 position, out Vector3 target, out float fieldOfView)
        {
            //Exponential approach, so the drift onto a newly rolled map's framing is frame-rate independent
            //and has no arrival to overshoot. It is a no-op on every frame the map has not changed. Brisker
            //while a map is waiting on the lens to get out of its way (#408), because that wait is the one
            //the player is watching.
            float ease = _pendingIndex >= 0 ? CLEARING_EASE_SECONDS : FRAMING_EASE_SECONDS;
            _framing = OrbitFraming.Lerp(_framing, _framingTarget, 1f - MathF.Exp(-elapsed / ease));

            _flightClock += elapsed;
            if (_flightClock >= CycleSeconds)
            {
                _flightClock -= CycleSeconds;
                RollCycle();
            }

            float closeness = Closeness(_flightClock);

            //Both stand-offs solved here, from the map's measurements and the window's own shape, rather than
            //stored with the framing — see OrbitFraming for why they cannot be settled at load. The close one
            //carries this cycle's rolled clearance, so one pass skims nearer the balls than the last.
            float radius = MathHelper.Lerp(WideRadius(_framing),
                CloseRadius(_framing, _closeClearance), closeness);

            float height = MathF.Max(LENS_FLOOR_Y, MathHelper.Lerp(
                _framing.CentreY - WIDE_LENS_DROP,
                MathHelper.Lerp(_framing.UnderY, _framing.OverY, Rise(_flightClock)),
                closeness));

            _angle += MathHelper.Lerp(WIDE_ROTATION_SPEED, CLOSE_ROTATION_SPEED, closeness) * elapsed;
            if (_angle >= MathHelper.TwoPi) _angle -= MathHelper.TwoPi;

            position = new Vector3(MathF.Cos(_angle) * radius, height, MathF.Sin(_angle) * radius);
            _lens = position;

            //Both legs aim at the middle of what hangs, and the camera's own height is what changes around it.
            //Aiming the crane anywhere else would swing the map across the frame as the lens climbed.
            target = new Vector3(0f, _framing.CentreY, 0f);
            fieldOfView = FOV;
        }

        /// <summary>
        /// Releases the flight where <paramref name="lens"/> already stands: the bearing is put on the one it
        /// is at, so a camera flown onto the orbit moves straight <i>out</i> from the arena rather than
        /// swinging around it. Without this the ease would be a chord to wherever the front end was last left
        /// — which crosses the island on the way and reads as the camera being yanked sideways rather than
        /// being released.
        /// <para>
        /// It puts the two other things a release has to agree with in the same place, for the same reason.
        /// The <b>flight</b> goes back to the start of its wide leg, so what the result screen eases onto is
        /// the establishing turn — a page of numbers over a camera that dived at the arena a second after
        /// arriving is not the moment being asked for, and the pass comes round on its own if the player sits
        /// there. And the <b>framing</b> snaps to the level's rather than drifting onto it: the camera is
        /// about to lerp in from behind the gun anyway, so there is nothing to be gained by easing the figures
        /// it is lerping towards, and a whole ease to be wasted aiming at the menu's last preview.
        /// </para>
        /// </summary>
        internal void AlignOrbitTo(Vector3 lens)
        {
            _angle = MathF.Atan2(lens.Z, lens.X);
            _flightClock = 0f;
            _framing = _framingTarget;

            //A release lands on the top of a wide leg, so the next cycle is a fresh roll — the pass that
            //eventually follows is not the one the menu was about to make when the level began
            RollCycle();
        }

        /// <summary>
        /// Draws a level at random and hangs it over the island: any <b>unlocked</b> entry of the set, played
        /// or not (#266) — the front end may promise what awaits within reach, but not the shape of a part of
        /// the campaign the player has not gotten to yet. That was the rule until #266: "any entry, played or
        /// not" was the deliberate original design, and the owner's ruling reverses it rather than fixing a
        /// bug in it — a menu that can hang a twenty-level cluster over a player who has cleared only the
        /// first is spoiling the campaign's own shape, not promising it. <see cref="BS3DGame.PinnedPreviewLevel"/>
        /// is exempt: a scripted `preview=` is asking for that entry specifically, not rolling one, so it is
        /// answered whatever its unlock state — the same distinction <see cref="LevelSelectPage"/> draws
        /// between what a player may open and what a test may ask to look at. The scene and its dome are not
        /// the map's: the backdrop keeps whatever sky it stands under, the note the feature answers being
        /// explicit that they do not matter (#249). ⚠ That holds for this random roll only — a tile the level
        /// picker's focus rests on is hung in its own scene, sky, weather and material (#405, see
        /// <see cref="RequestPreview"/>).
        /// <para>
        /// The map is data and nothing more — no physics is built for it, no cannon stands under it. It
        /// hangs by the very offset a session derives for the same map
        /// (<see cref="GameplayScreen.FitClusterWorldOffset"/>), under the menu's own glass
        /// (<see cref="BS3DGame.MenuCeilingRenderer"/>) fitted to its footprint, and the camera is framed for
        /// it (<see cref="FrameOrbitFor"/>).
        /// </para>
        /// </summary>
        internal void RollPreviewMap()
        {
            DropPending();

            _previewMap = null;
            _previewIndex = -1;
            _requestedPreview = -1;
            _previewStyle = BallStyle.Beach;
            _previewOffset = Vector3.Zero;
            _menuCeilingWorld = Matrix.Identity;

            //A fresh map gets the establishing shot before anything flies at it — and this is also what keeps
            //the adaptive probe on wide frames after a level, since building one reopens it (#121) and a
            //return to the front end is where it is measured again. A fresh cycle with it.
            _flightClock = 0f;
            RollCycle();

            LevelSet set = Game.LevelSet;
            if (set == null || set.Count == 0)
            {
                SetFraming(OrbitFraming.Bare);
                return;
            }

            //Every entry tried at most once, from a random start — or from the pinned one, which is the only
            //way a scripted run can photograph the same preview twice (see BS3DGame.PinnedPreviewLevel). One
            //unreadable file skips that map, not the feature — and if the whole set is unreadable, the front
            //end falls back to the bare setting it always was.
            int start = Game.PinnedPreviewLevel ?? RANDOM.Next(set.Count);

            //A pin is a request for THAT entry, unlock state and all — see the class doc — so only the random
            //roll is filtered.
            bool pinned = Game.PinnedPreviewLevel.HasValue;

            for (int tried = 0; tried < set.Count; tried++)
            {
                int index = (start + tried) % set.Count;

                //Locked entries are skipped before the file is even opened (#266): the menu may promise what
                //the player can already reach, not the shape of a part of the campaign still ahead of them.
                //The same test LevelSelectPage locks its own tiles with, so the two screens can never disagree
                //about what "unlocked" means.
                if (!pinned && !Game.IsLevelUnlocked(index)) continue;

                //Unreadable or unparseable — the next entry is the whole recovery.
                if (!TryLoadPreview(set, index, out BallsMap map, out string name, out BallStyle style, out _))
                    continue;

                HangPreview(map, name, style, index);
                return;
            }

            SetFraming(OrbitFraming.Bare);
            Console.WriteLine("[menu] preview map: no readable level in the set");
        }

        /// <summary>
        /// How long the level picker's focus has to rest on a tile before the backdrop turns to that level
        /// (#405). A player sweeping the pointer across a row passes over four tiles in a fraction of a second,
        /// and each one would otherwise be a scene change, a new sky and a new cluster — a strobe of the whole
        /// campaign behind the page. Short enough that a tile the player stops on answers at once.
        /// </summary>
        private const float PREVIEW_SETTLE_SECONDS = 0.35f;

        //The entry hanging now, -1 for none or for one this screen rolled before #405 kept count; and the one
        //the level picker last asked for, with how long its focus still has to rest before it is hung.
        private int _previewIndex = -1;
        private int _requestedPreview = -1;
        private float _requestSettle;

        //A settled request that is read, centred, solved and NOT yet hung, because the lens stood where its
        //balls would be (#408): everything the hang needs, held until the flight has carried the lens clear.
        //The framing target already points at it — that drift is what clears the lens.
        private int _pendingIndex = -1;
        private BallsMap _pendingMap;
        private string _pendingName;
        private BallStyle _pendingStyle;
        private Level _pendingLevel;
        private Vector3 _pendingOffset;
        private float _pendingCeilingCentreY, _pendingTopLevelY;
        private OrbitFraming _pendingFraming;

        /// <summary>
        /// Asks the backdrop to hang entry <paramref name="index"/> of the set <b>in its own scene, sky, weather
        /// and material</b> — what the level picker does with the tile its pointer or cursor stands on (#405).
        /// <para>
        /// <b>This is the one place the front end's scene follows a map</b>, and it reverses #249's rule for this
        /// page and this page only. #249 kept the scene the player's while the menu rolled a map at random,
        /// because a random map is decoration and the scene page is the player's own choice. A tile the player
        /// is looking at is neither: it is a question about that level, and the owner's report was that the
        /// answer came back in the wrong place and the wrong material. The random roll on a return to the front
        /// end still leaves the scene alone.
        /// </para>
        /// <para>
        /// A locked entry is not previewed — the picker only asks for unlocked ones, on #266's ruling that the
        /// menu must not show the shape of a level still ahead of the player. The request is <b>settled</b>
        /// (<see cref="PREVIEW_SETTLE_SECONDS"/>) and the flight is <b>not</b> restarted: the camera carries on
        /// round the island and drifts onto the new map's framing, so looking along a row reads as the arena
        /// changing under a camera that is still, not as a cut per tile.
        /// </para>
        /// </summary>
        internal void RequestPreview(int index)
        {
            if (index == _previewIndex)
            {
                //Back on the tile that hangs: nothing left to wait for, settling or pending
                _requestedPreview = -1;
                DropPending();
                return;
            }

            //Already read and waiting on the lens: a settling request for another tile is withdrawn, or it
            //would replace the very map the focus has come back to
            if (index == _pendingIndex)
            {
                _requestedPreview = -1;
                return;
            }

            if (index == _requestedPreview) return;

            _requestedPreview = index;
            _requestSettle = PREVIEW_SETTLE_SECONDS;
        }

        /// <summary>
        /// The settled half of <see cref="RequestPreview"/>, stepped from <see cref="Update"/> — in two steps
        /// since #408. A request whose focus has rested is <b>read and solved</b> (<see cref="TakePending"/>),
        /// which also points the flight's framing at it; it is <b>hung</b> only once the lens stands clear of
        /// where its balls would be (<see cref="LensClearOf"/>), which on the wide leg is at once and on the
        /// close pass is after the drift has backed the lens out — under a second, at
        /// <see cref="CLEARING_EASE_SECONDS"/>.
        /// <para>
        /// ⚠ <b>NEVER HANG A MAP AROUND THE LENS.</b> The pass keeps a floor of air off the bounding sphere of
        /// the map that hangs, but a map hung while the camera is in among the balls of a smaller one appears
        /// AROUND the camera — a twenty-level column materializing about a lens that was skimming a pancake —
        /// and the drift then backs the lens out through the balls it is now inside of. That was the owner's
        /// "the camera abruptly flies through the map" (#408): a cut in what hangs, under a camera placed for
        /// what hung before. The flight is still not restarted, which #405 asked for: the camera carries on
        /// round the island and drifts out, and the map arrives the moment there is room for it.
        /// </para>
        /// </summary>
        private void StepPreviewRequest(float elapsed)
        {
            if (_requestedPreview >= 0)
            {
                _requestSettle -= elapsed;

                if (_requestSettle <= 0f)
                {
                    int index = _requestedPreview;
                    _requestedPreview = -1;
                    TakePending(index);
                }
            }

            if (_pendingIndex < 0 || !LensClearOf(in _pendingFraming)) return;

            HangPending();
        }

        /// <summary>
        /// Reads entry <paramref name="index"/> and solves everything its hang needs — the offset, the glass,
        /// the framing — without hanging it, and turns the flight towards it so the lens starts making room.
        /// </summary>
        private void TakePending(int index)
        {
            LevelSet set = Game.LevelSet;
            if (set == null || index < 0 || index >= set.Count) return;

            //A file that will not read leaves whatever is hanging: the tile still describes and still starts the
            //level, and a preview is not worth an error the player has to see.
            if (!TryLoadPreview(set, index, out BallsMap map, out string name, out BallStyle style, out Level level))
                return;

            map.Center();

            _pendingIndex = index;
            _pendingMap = map;
            _pendingName = name;
            _pendingStyle = style;
            _pendingLevel = level;
            _pendingTopLevelY = SolveHang(map, out _pendingOffset, out _pendingCeilingCentreY);
            _pendingFraming = SolveFraming(map, _pendingTopLevelY);

            //The flight starts drifting onto the new map's framing NOW, whether or not it can hang yet: on the
            //wide leg that is the same drift #405 had, and on the close pass it is what carries the lens out
            SetFraming(_pendingFraming);

            if (!LensClearOf(in _pendingFraming))
                Console.WriteLine($"[menu] preview map {name} waits for the lens to clear the cluster");
        }

        /// <summary>Hangs the waiting map, its place first — the order a session builds a level in.</summary>
        private void HangPending()
        {
            //The place before the map, in the order a session builds a level (GameplayScreen.BuildLevel): the
            //scene states its own dome and weather, the level then says which dome and what it is like today, and
            //the map is hung last so the menu's glass takes the light rig those just derived.
            if (_pendingLevel != null)
            {
                if (_pendingLevel.Scene is SceneKind scene) Game.SetScene(scene);
                Game.SetSkyDome(Math.Clamp(_pendingLevel.SkyDome, (byte)1, BS3DGame.SKY_DOME_COUNT));
                Game.ApplySceneWeather(_pendingLevel.Weather);
            }

            HangPreview(_pendingMap, _pendingName, _pendingStyle, _pendingIndex,
                _pendingOffset, _pendingCeilingCentreY, _pendingTopLevelY);

            ClearPending();
        }

        /// <summary>
        /// Forgets a map waiting to hang, and turns the flight back to the map that does hang — the focus moved
        /// on, or a session took over; either way what the lens was making room for is not coming.
        /// </summary>
        private void DropPending()
        {
            if (_pendingIndex < 0) return;

            ClearPending();
            SetFraming(_hungFraming);
        }

        private void ClearPending()
        {
            _pendingIndex = -1;
            _pendingMap = null;
            _pendingName = null;
            _pendingLevel = null;
        }

        /// <summary>
        /// Reads entry <paramref name="index"/> of the set with the tolerance a session reads a level with: a
        /// level file carries its map and its look inside, anything else is tried as a plain map outright and
        /// stays vinyl. False on a file that will not read.
        /// </summary>
        private static bool TryLoadPreview(LevelSet set, int index, out BallsMap map, out string name,
            out BallStyle style, out Level level)
        {
            map = null;
            name = null;
            style = BallStyle.Beach;
            level = null;

            string path = set.ResolvePath(index);

            try
            {
                if (Level.IsLevelFile(path))
                {
                    level = Level.Load(path);

                    map = new BallsMap(level.Map);
                    name = set.DisplayName(index);

                    //Hung in whatever the level is authored in, so the front end promises the material as
                    //well as the shape (#258). A plain map file carries no look at all and stays vinyl.
                    style = level.Balls ?? BallStyle.Beach;
                }
                else
                {
                    map = new BallsMap(path);
                    name = Path.GetFileNameWithoutExtension(path);
                }
            }
            catch (Exception)
            {
                map = null;
                level = null;
            }

            return map != null;
        }

        /// <summary>
        /// Where a read map hangs: at the offset a session would hang it at, raised to where the ceiling socket
        /// settles it, with its glass over it. Solved apart from the hang itself since #408, so a map that has
        /// to wait for the lens can be measured for the wait. The map must be centred first.
        /// </summary>
        /// <returns>World Y the top level of balls hangs at — the figure the framing is solved off.</returns>
        private static float SolveHang(BallsMap map, out Vector3 offset, out float ceilingCentreY)
        {
            offset = GameplayScreen.FitClusterWorldOffset(map, out float fieldTopY);
            ceilingCentreY = CeilingPlate.CentreYAbove(fieldTopY);

            //AND THEN RAISED TO WHERE THE PHYSICS WOULD HAVE PUT IT, which is the other half of #254 and
            //the half that is not about the camera at all. A played cluster does not rest on its lattice:
            //the ceiling BallSocket ties a top ball's crown to a point one radius under the plate's CENTRE,
            //so the top level settles a whole diameter under it and its surface meets the plate's underside
            //exactly (BallsConstraintsBuilder.CeilingRestY, and CeilingPlate.CLEARANCE says the same thing
            //from the other end — "half the clearance this constant looks like it buys is spent that way").
            //A preview has no bodies and no solver, so it hung at the lattice height instead, one whole unit
            //of daylight short of the glass — a gap no level ever shows in play, on the one screen whose
            //job is to promise what play looks like.
            float topLevelY = BallsConstraintsBuilder.CeilingRestY(ceilingCentreY);
            offset.Y += topLevelY - fieldTopY;

            return topLevelY;
        }

        /// <summary>
        /// Hangs a read map over the island as the front end's preview: centred, at the offset a session would
        /// hang it at, raised to where the ceiling socket settles it, under the menu's own glass, with the
        /// camera's framing aimed at it. The random roll's form; the level picker's request (#405) hangs
        /// through the overload below with the figures it solved while it waited (#408).
        /// </summary>
        private void HangPreview(BallsMap map, string name, BallStyle style, int index)
        {
            map.Center();
            float topLevelY = SolveHang(map, out Vector3 offset, out float ceilingCentreY);
            HangPreview(map, name, style, index, offset, ceilingCentreY, topLevelY);
        }

        /// <summary>The hang itself, off figures <see cref="SolveHang"/> already solved for a centred map.</summary>
        private void HangPreview(BallsMap map, string name, BallStyle style, int index,
            Vector3 offset, float ceilingCentreY, float topLevelY)
        {
            _previewMap = map;
            _previewIndex = index;
            _previewStyle = Game.BallStyleOverride ?? style;
            _previewOffset = offset;

            //The menu's glass over what was just hung, and the sky palette the fresh renderer starts
            //without — the same re-run the session makes after its own refit.
            Game.RebuildMenuCeilingRenderer(map.StageSizeX, map.StageSizeZ);
            Game.ApplySkyLighting();
            _menuCeilingWorld = Matrix.CreateTranslation(0f, ceilingCentreY, 0f);

            //And the camera framed for it, off where the balls have just been hung rather than off the
            //lattice they would have hung on. Applied rather than solved again: a map that waited was
            //already measured, and what hangs and what the flight frames must be one reading.
            ApplyFraming(SolveFraming(map, topLevelY), map);

            Console.WriteLine($"[menu] preview map {name} — {map.GetBallsCount()} balls, {BallStyles.ToName(_previewStyle)}"
                + $", {Game.Scene}");
        }

        /// <summary>
        /// The setting with the preview in its gameplay slot: the host's pipeline sliced open, the map's
        /// cluster and the menu's glass drawn where the session draws its own. The collection happens
        /// <b>before</b> <see cref="BS3DGame.BeginSceneDraw"/> because the LOD ladder solves against the back
        /// buffer's height — the same slot <see cref="GameplayScreen.Draw"/> collects in — and the balls
        /// draw after it, in the states it binds. No gun, no trails, no warning grid: only the map hangs
        /// here, breathing on the wall clock.
        /// </summary>
        public override void Draw(GameTime gameTime)
        {
            //Stated, not inherited: the set is the whole program's and a session hangs its own level through it
            //in whatever that level is made of (#258).
            Game.Balls.Style = _previewStyle;

            BallDrawFrame ballFrame = Game.Balls.BeginFrame(Game.Camera);
            ballFrame.AddMap(_previewMap, _previewOffset);

            SceneFrame sceneFrame = Game.BeginSceneDraw();

            Game.Balls.Draw(Game.WallClock);

            //The game's name in 3D (#248), on the title card and on the main menu and nowhere else.
            //
            //THE GATE IS ONE TEST HERE RATHER THAN A CALL FROM THE PAGE, and that is a correctness point, not
            //a preference: Screen.Enter and Screen.Leave are raised on a PUSH and a POP only, and every other
            //front-end page (Settings, Scene, About, the level picker) is pushed OVER the main menu without
            //popping it — so a Present/Hide pair in MainMenuPage would leave the title standing behind all
            //four of them. Covering is signalled by CoveredChanged, which this screen never sees. One test
            //against the active page needs no page to opt in and cannot be forgotten by a page added later.
            //
            //And it is HERE, in the front end's own screen, rather than in the host's BeginSceneDraw where the
            //fireworks, the confetti and the cup are drawn: nothing the host draws is front-end-only (which is
            //why `celebrate` and `confetti` work as front-end test levers at all), while this screen is not
            //reached at all once a session is on the stack.
            //
            //IT IS THE SAME OBJECT ON BOTH PAGES, and the page only says WHICH COMPOSITION it is heading for:
            //under the splash it stands in the 2D logo's own layout in the middle of the frame, where the
            //picture is cross-fading into it (#454); the menu wants it in the corner. The move between them
            //belongs to the wordmark, so no page can leave it stranded half way across the frame, and the
            //splash's replacement by the menu is what starts it (#248).
            //
            //UNDER THE SPLASH IT IS DRAWN ONLY ONCE THE HAND-OVER HAS BEGUN (SplashPage.WordmarkShown). The
            //page opens black with the picture over it and the scene arrives behind the picture first; letters
            //standing in the scene from frame one would show round the picture's edges as the black went, and
            //the title would be seen arriving twice. From the frame the picture starts to thin, the letters
            //are under it in its own place, and what the picture leaves behind is them.
            //
            //After the balls and BEFORE the drain's glass, so the frame's stated order holds — every opaque
            //thing, then everything translucent. It states its own three states and puts them back.
            //And it STEPS ASIDE for the fly-in (#261): the pass comes in far nearer than the title hangs
            //(see CLOSE_CLEARANCE), so across the approach the block shrinks to WORDMARK_ASIDE_SCALE of its
            //size and back up across the retreat, on the flight's own curve — a small corner mark through
            //the close pass rather than the full-size name the balls would draw through. What changes is the
            //block's SIZE, the reveal's own idiom, because the letters are opaque geometry and have no alpha
            //to fade.
            Screen active = Manager?.Active;
            if (active is MainMenuPage || (active is SplashPage splash && splash.WordmarkShown))
                Game.TitleWordmark?.Draw(Game.Camera, Game.WallClock, settled: active is MainMenuPage,
                    presence: MathHelper.Lerp(1f, WORDMARK_ASIDE_SCALE, Closeness(_flightClock)));

            Game.DrawSettingGlass();

            //Over the glass drain, as in play — and hung from the pose RollPreviewMap wrote, at rest above
            //the preview field's top level. Null only through the no-readable-level fallback above.
            //Through the host, so this plate and the session's are one decision about how glass draws (#299).
            if (Game.MenuCeilingRenderer != null)
                Game.DrawCeilingGlass(Game.MenuCeilingRenderer, _menuCeilingWorld);

            Game.FinishSceneDraw(sceneFrame);
        }
    }
}
