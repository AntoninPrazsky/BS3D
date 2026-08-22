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

        //THE FLY-IN'S JOB IS THE MAP, so this is what the whole leg is for: the map fills this much of the
        //frame's half-angle on whichever axis binds. It is short of 1 because the ball cluster is not the
        //whole subject — the glass over it is drawn to the FIELD's footprint, which is wider than the balls,
        //and a share that filled the frame would push the ceiling the map is supposed to be hanging from off
        //the top of it.
        private const float CLOSE_MAP_SHARE = 0.8f;

        //And what the fly-in may not do, whatever the arithmetic above asks for: come nearer the cluster than
        //this. The 3D wordmark hangs 7 units in front of the lens with depth writes on (TitleWordmark.DISTANCE)
        //and its far corners reach about 8.5 out from the lens, so a ball nearer than that is a ball drawn
        //THROUGH the game's own name. Measured off the cluster's bounding SPHERE, which is a floor on the
        //distance to any ball at any bearing and any point of the crane — the true nearest ball is further,
        //and on most of a pass much further, so the margin this leaves is a floor and not an estimate.
        //
        //It is what actually binds on nearly every shipped level: the map fit above asks for 17-21 units and
        //this holds the pass at 21-24. That is the trade the wordmark is worth, and the figure to revisit
        //first if the fly-in should ever come closer.
        private const float CLOSE_CLEARANCE = 10f;

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
            if (map == null)
            {
                SetFraming(OrbitFraming.Bare);
                return;
            }

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

            SetFraming(new OrbitFraming(
                MathF.Sqrt(halfX * halfX + halfZ * halfZ),
                (topY - bottomY) * Constants.HALF,
                centreY,
                MathF.Max(bottomY - CRANE_UNDER_CLUSTER, LENS_FLOOR_Y),
                topY + CRANE_OVER_CLUSTER));

            //At the window's CURRENT shape, which is what makes this a reading of the fit rather than a
            //restatement of it: the two stand-offs are solved per frame and move with a resize.
            Console.WriteLine($"[orbit] framed for {map.GetBallsCount()} balls:"
                + $" reach {_framingTarget.SpanXZ:F1} x {_framingTarget.HalfHeight:F1}"
                + $", aim y {_framingTarget.CentreY:F1}"
                + $", wide {WideRadius(_framingTarget):F1}"
                + $", close {CloseRadius(_framingTarget):F1}"
                + $", crane {_framingTarget.UnderY:F1} to {_framingTarget.OverY:F1}");
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
        /// <see cref="CLOSE_CLEARANCE"/> — which is what actually decides it on nearly every shipped level.
        /// </summary>
        private float CloseRadius(in OrbitFraming framing)
        {
            FrameHalfAngles(out float vertical, out float horizontal);

            return MathF.Max(
                framing.Span + CLOSE_CLEARANCE,
                MathF.Max(
                    StandOffFor(framing.HalfHeight, vertical, CLOSE_MAP_SHARE),
                    StandOffFor(framing.SpanXZ, horizontal, CLOSE_MAP_SHARE)));
        }

        #endregion

        #region The flight

        //The menu camera orbits the scene's origin in XZ. The angle is advanced by elapsed seconds, never by
        //a frame count, so the turn takes the same time on any machine.
        private float _angle;

        //And how far into the current fly-in cycle it is, on that same clock and for that same reason.
        private float _flightClock;

        //=== THE CYCLE ===
        //
        //The front end's camera is not a carousel: it holds the wide establishing turn for a long stretch, then
        //leaves it, comes in over the island, cranes up the hanging map while it circles, and backs out again
        //(#254 — "the camera just flies around the scene and looks at it"). Then it does it again, from
        //wherever the bearing has reached by then, so no two passes are the same view.
        //
        //THE WIDE LEG IS THE LONG ONE, and it has a second job that fixes its lower bound: BS3DGame
        //.TuneQualityToFrameRate is driven off this screen's Update, and it reaches a verdict from a 1.5 s
        //warm-up and 1.5 s windows — so the whole probe settles inside the first ten seconds of a front end,
        //well inside this leg. A cycle that flew in sooner would be handing the probe a frame with the cluster
        //filling it and letting THAT decide the tier the whole game runs at. Every route that reopens the
        //probe from the front end puts the flight back here with it: a fresh program, a rolled preview
        //(returning from a level, which is what ReopenQualityProbe answers) and a release from the result
        //screen all start the clock at zero. The one that does not is the fullscreen toggle, which reopens the
        //probe wherever the flight has got to — measured, the pass is not the expensive half anyway (see the
        //frame-rate figures under "The menu camera" in docs/game-shell.md), and the probe only ever steps down.
        private const float WIDE_SECONDS = 30f;
        private const float APPROACH_SECONDS = 8f;
        private const float CLOSE_SECONDS = 20f;
        private const float RETREAT_SECONDS = 8f;

        private const float EXCURSION_SECONDS = APPROACH_SECONDS + CLOSE_SECONDS + RETREAT_SECONDS;
        private const float CYCLE_SECONDS = WIDE_SECONDS + EXCURSION_SECONDS;

        //About a full turn every 90 s out wide: slow enough to read as ambience rather than as a turntable.
        //The fly-in turns half again as fast — but at little more than half the radius, so it crosses the
        //frame SLOWER than the wide leg does while still visibly moving: a camera that has come in to look at
        //something and slowed down to do it. Holding the wide leg's angular rate through the pass instead
        //would have read as the flight stalling the moment it arrived.
        private const float WIDE_ROTATION_SPEED = MathHelper.TwoPi / 90f;
        private const float CLOSE_ROTATION_SPEED = MathHelper.TwoPi / 60f;

        private static readonly float FOV = MathF.PI / 3f;  //60°: wide, to take in the scene behind the panel

        /// <summary>
        /// Where in the excursion the camera is, as one reversible scalar: 0 out on the wide turn, 1 in on the
        /// map. Smoothstepped at both ends, so the flight leaves the wide leg at rest and arrives at rest — the
        /// same shape precise aim, the drop cinematic and the result screen's own release are built on.
        /// </summary>
        private static float Closeness(float clock)
        {
            float since = clock - WIDE_SECONDS;

            if (since <= 0f) return 0f;
            if (since < APPROACH_SECONDS) return MathHelper.SmoothStep(0f, 1f, since / APPROACH_SECONDS);

            since -= APPROACH_SECONDS;
            if (since < CLOSE_SECONDS) return 1f;

            return MathHelper.SmoothStep(1f, 0f, (since - CLOSE_SECONDS) / RETREAT_SECONDS);
        }

        /// <summary>
        /// How far up the crane is, 0 under the cluster and 1 over it, run across the <b>whole</b> excursion
        /// rather than only its close leg. Over the whole of it because the rise is what stops the pass reading
        /// as a second orbit: the camera is climbing the map for the entire time it is anywhere near it, and it
        /// is moving fastest in the middle of the close leg, where the smoothstep is steepest.
        /// <para>
        /// It jumps back to 0 at the end of the cycle, and that is not a discontinuity anyone can see:
        /// <see cref="Closeness"/> is exactly 0 there, so the height it feeds is not being mixed in at all.
        /// </para>
        /// </summary>
        private static float Rise(float clock) => MathHelper.SmoothStep(0f, 1f,
            MathHelper.Clamp((clock - WIDE_SECONDS) / EXCURSION_SECONDS, 0f, 1f));

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
            //and has no arrival to overshoot. It is a no-op on every frame the map has not changed.
            _framing = OrbitFraming.Lerp(_framing, _framingTarget,
                1f - MathF.Exp(-elapsed / FRAMING_EASE_SECONDS));

            _flightClock += elapsed;
            if (_flightClock >= CYCLE_SECONDS) _flightClock -= CYCLE_SECONDS;

            float closeness = Closeness(_flightClock);

            //Both stand-offs solved here, from the map's measurements and the window's own shape, rather than
            //stored with the framing — see OrbitFraming for why they cannot be settled at load.
            float radius = MathHelper.Lerp(WideRadius(_framing), CloseRadius(_framing), closeness);

            float height = MathF.Max(LENS_FLOOR_Y, MathHelper.Lerp(
                _framing.CentreY - WIDE_LENS_DROP,
                MathHelper.Lerp(_framing.UnderY, _framing.OverY, Rise(_flightClock)),
                closeness));

            _angle += MathHelper.Lerp(WIDE_ROTATION_SPEED, CLOSE_ROTATION_SPEED, closeness) * elapsed;
            if (_angle >= MathHelper.TwoPi) _angle -= MathHelper.TwoPi;

            position = new Vector3(MathF.Cos(_angle) * radius, height, MathF.Sin(_angle) * radius);

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
        }

        /// <summary>
        /// Draws a level at random and hangs it over the island: any entry of the set, played or not — the
        /// front end's promise is <i>what awaits</i>, not what is next, which is why progress has no say
        /// here. The scene and its dome are not the map's: the backdrop keeps whatever sky it stands under,
        /// the note the feature answers being explicit that they do not matter (#249).
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
            _previewMap = null;
            _previewOffset = Vector3.Zero;
            _menuCeilingWorld = Matrix.Identity;

            //A fresh map gets the establishing shot before anything flies at it — and this is also what keeps
            //the adaptive probe on wide frames after a level, since building one reopens it (#121) and a
            //return to the front end is where it is measured again.
            _flightClock = 0f;

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
            for (int tried = 0; tried < set.Count; tried++)
            {
                int index = (start + tried) % set.Count;

                string path = set.ResolvePath(index);
                BallsMap map = null;
                string name = null;
                try
                {
                    //The same tolerance InstallLevel reads a level with: a level file carries its map
                    //inside, anything else is tried as a plain map outright.
                    if (Level.IsLevelFile(path))
                    {
                        map = new BallsMap(Level.Load(path).Map);
                        name = set.DisplayName(index);
                    }
                    else
                    {
                        map = new BallsMap(path);
                        name = Path.GetFileNameWithoutExtension(path);
                    }
                }
                catch (Exception)
                {
                    //Unreadable or unparseable — the next entry is the whole recovery.
                }

                if (map == null) continue;

                map.Center();
                _previewMap = map;
                _previewOffset = GameplayScreen.FitClusterWorldOffset(map, out float fieldTopY);

                float ceilingCentreY = CeilingPlate.CentreYAbove(fieldTopY);

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
                _previewOffset.Y += topLevelY - fieldTopY;

                //The menu's glass over what was just hung, and the sky palette the fresh renderer starts
                //without — the same re-run the session makes after its own refit.
                Game.RebuildMenuCeilingRenderer(map.StageSizeX, map.StageSizeZ);
                Game.ApplySkyLighting();
                _menuCeilingWorld = Matrix.CreateTranslation(0f, ceilingCentreY, 0f);

                //And the camera framed for it, off where the balls have just been hung rather than off the
                //lattice they would have hung on.
                FrameOrbitFor(map, topLevelY);

                Console.WriteLine($"[menu] preview map {name} — {map.GetBallsCount()} balls");
                return;
            }

            SetFraming(OrbitFraming.Bare);
            Console.WriteLine("[menu] preview map: no readable level in the set");
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
            //the title card wants the whole name on one line in the middle of the frame, the menu wants it in
            //the corner. The move between them belongs to the wordmark, so no page can leave it stranded half
            //way across the frame, and the splash's replacement by the menu is what starts it (#248).
            //
            //After the balls and BEFORE the drain's glass, so the frame's stated order holds — every opaque
            //thing, then everything translucent. It states its own three states and puts them back.
            Screen active = Manager?.Active;
            if (active is MainMenuPage || active is SplashPage)
                Game.TitleWordmark?.Draw(Game.Camera, Game.WallClock, settled: active is MainMenuPage);

            Game.DrawSettingGlass();

            //Over the glass drain, as in play — and hung from the pose RollPreviewMap wrote, at rest above
            //the preview field's top level. Null only through the no-readable-level fallback above.
            Game.MenuCeilingRenderer?.Draw(Game.Camera, _menuCeilingWorld, Game.SceneEffectParams);

            Game.FinishSceneDraw(sceneFrame);
        }
    }
}
