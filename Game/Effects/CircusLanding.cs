using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// <b>The gate home</b> (#1118): the last shot of the big top's chapter intro, in through the performers' entrance low
    /// as the act comes in, out of the gap into the ring, round the island inside or outside the king poles and onto the
    /// gameplay pose itself, so the player is carried from the tent into the game without a cut — as the cities' swing home
    /// (<see cref="CitySwing"/>, #972) and the meadow's brook home (<see cref="MeadowLanding"/>, #1107) are. The owner: "as
    /// the last shot of the intro animation, where the camera goes through the entrance beside the audience, I want this
    /// shot to end only in the game position behind the cannon".
    /// <para>
    /// <b>The route.</b> Straight in along the entrance's bearing from just inside the wall, at the old gate shot's height
    /// over the floor (<see cref="CircusIntroShots.GATE_FROM"/>, <see cref="CircusIntroShots.GATE_HEIGHT"/>), past the front
    /// row; a curve off that line onto a circle round the arena (the shared release, <see cref="IntroLanding.Release"/>),
    /// climbing as it leaves the gap; round that circle the shorter way to the pose; and the shared turn in onto the pose
    /// (<see cref="IntroLanding.TurnIn"/>). The circle stands INSIDE the king poles' ring, between them and the pose, wherever
    /// the pose leaves room for it (the level's own lens stands 30 out and the poles 46), and OUTSIDE it, over the front rows,
    /// for a pose too far out; either way a pole stands no nearer it than <see cref="POLE_AIR"/>. From the entrance's bearing
    /// (200) the shorter way to the gun's (90) passes the pole at 135, which the inner circle goes by on its inside. A pose
    /// fewer than <see cref="DIRECT_TURN"/> degrees round from the entrance is reached straight off the run in.
    /// </para>
    /// <para>
    /// <b>The pace.</b> The run in at a walk's pace, picking up as it leaves the gap to a cruise round the island, then the
    /// shared deceleration to rest on the pose; the look down the gap at the island, turning in towards the arena as the lens
    /// comes off the entrance's line (<see cref="IntroLanding.Style.LookInward"/>), so the island, the gun and the cluster
    /// stay in the frame while the lens goes round them; a light bank into the turn.
    /// </para>
    /// <para>
    /// <b>Found, not assumed.</b> The tent has no CPU geometry but its figures (<see cref="CircusSceneConfig"/>, which
    /// <c>Circus.fx</c> traces), so a candidate is held to those (<see cref="Tent"/>): inside the side wall and under the
    /// canvas, over the floor, over the seats' rake outside the entrance's gap and well inside the gap within it, off the
    /// king poles, over the ring's curb and its footlights, never nearer the axis than the pose
    /// (<see cref="IntroLanding.KeepOutFor"/>). Rolled candidates (how far in the run goes, the circle's radius and height,
    /// how far round the curves onto and off it sweep, the climb, the cruise, which way the lens arrives) are each laid in
    /// full and refused if they turn tighter than the speed allows or any point is not clear; the best of the rest flies.
    /// Null when none is clear, or for a pose inside the island's own radius (the menu's crane, which no flight from the
    /// entrance reaches without crossing the arena), and the big top then plays the reel it had before: the gate, and the
    /// tour's arrival leg.
    /// </para>
    /// </summary>
    internal static class CircusLanding
    {
        /// <summary>The shot's name, in the intro's log line.</summary>
        internal const string NAME = "the gate home";

        //The run in: down the entrance's gap from the old gate shot's start to this far out, past the front row (the seats'
        //InnerRadius, 51), at a walk's pace. The release curves off the line from there, inside the gap.
        private const float RUN_TO_MIN = 52f, RUN_TO_MAX = 60f;
        private const float GATE_SPEED = 13f;

        //The climb: from this far short of the run's end, over this long a run, to the circle's height over the floor: low
        //down the gap, the way the act comes in, and over the curb and its footlights by the time it crosses them
        private const float CLIMB_LEAD_MIN = 0f, CLIMB_LEAD_MAX = 8f;
        private const float CLIMB_MIN = 14f, CLIMB_MAX = 24f;
        private const float CIRCLE_ABOVE_MIN = 6f, CIRCLE_ABOVE_MAX = 9f;

        //The pace picks up from this far short of the run's end, and reaches the cruise's on the circle. A circle long
        //enough to take more than CIRCLE_SECONDS at the rolled cruise is flown faster, up to CRUISE_FASTEST: the far side of
        //the ring, 160 degrees round from the entrance, ran to 16.7 seconds.
        private const float ACCELERATE_BEFORE = 6f;
        private const float CRUISE_MIN = 18f, CRUISE_MAX = 23f, CRUISE_FASTEST = 27f;
        private const float CIRCLE_SECONDS = 3f;

        //The circle round the arena: at least CIRCLE_OVER_POSE outside the pose (the turn in only ever closes on the axis,
        //IntroLanding.TurnIn), inside the king poles by their thickness and POLE_AIR where that leaves room, and otherwise
        //outside them by as much, at most OUTER_WIDTH further out, over the front rows
        private const float CIRCLE_OVER_POSE = 4f;
        private const float OUTER_WIDTH = 5f;

        //⚠ A circle outside the poles stands over the front rows, and the gap opens only at the first of them: a release off
        //the run 52-60 out onto it turned at radii of 2 to 8. So the run goes on to OUTER_RUN_PAST further out than the
        //circle and the curve onto it sweeps at least OUTER_SWEEP_IN (13 to 19 then), and since the curve leaves the gap
        //over the rake there, the lens has climbed to the circle's height by the run's end, SEAT_AIR and OUTER_RISE_AIR over
        //the rake where the run ends: the far poses' entrance rises up the gap rather than running in low.
        private const float OUTER_RUN_PAST_MIN = 10f, OUTER_RUN_PAST_MAX = 14f;
        private const float OUTER_SWEEP_IN = 45f;
        private const float OUTER_RISE_AIR = 0.5f;

        //How far round the arena the curve onto the circle sweeps, and how far round before the pose the turn in leaves it,
        //degrees (both shrunk in proportion where the way round is shorter than the two); and how much of the arrival runs
        //along the turn rather than straight down the pose's facing, off the arrival's roll. ⚠ More of them along the turn
        //than the meadow's: its circle stands 3 to 31 outside the pose, this one 4 to 13 (the poles stand 46 out), and a
        //turn in straight down the facing over so short a drop is a hairpin (radii of 2 to 6, where SLANT_MORE's are 7 to 25)
        private const float SWEEP_IN_MIN = 30f, SWEEP_IN_MAX = 60f;
        private const float SWEEP_OUT_MIN = 30f, SWEEP_OUT_MAX = 50f;
        private const float SLANT_SOME = 0.3f, SLANT_MORE = 0.55f, SLANT_MOST = 0.8f;

        /// <summary>A pose fewer than this many degrees round from the entrance is reached straight off the run in, the
        /// deceleration over at least the last <see cref="SETTLE_LENGTH"/>.</summary>
        internal const float DIRECT_TURN = 40f;
        private const float SETTLE_LENGTH = 24f;
        private const float SETTLE_SECONDS = 2.6f;

        //No tighter than this off the run in, and over the decelerating turn in: at a cruise of 21 u/s a 10-unit radius is
        //120 degrees a second of heading, which the look's averaging and its lean take well under the 170 the ride is held to
        private const float MIN_TURN_RADIUS = 10f;
        private const float RELEASE_TURN_RADIUS = 7f;

        //How long the flight should be: among clear candidates the one nearest it is preferred. The gate and the tour's leg
        //it replaces ran 3.2 and 4.5 s.
        private const float TARGET_SECONDS = 8f;

        //The plan's spacing, and the candidates rolled
        private const float STEP = 0.5f;
        private const int CANDIDATES = 48;

        //Clearances (see Tent). Every point keeps WALL_AIR inside the side wall and under the canvas, LOWEST over the floor;
        //SEAT_AIR over the seats' rake, or GAP_AIR inside the entrance's own walls where it is within the gap; POLE_AIR off a
        //king pole's surface; CURB_AIR over the curb; LAMP_AIR off a footlight. The pose's own distance less ARRIVAL_SLACK is
        //the nearest the axis the lens comes.
        private const float WALL_AIR = 1.5f;
        private const float LOWEST = 1f;
        private const float SEAT_AIR = 1.5f;
        private const float GAP_AIR = 1.5f;
        internal const float POLE_AIR = 3f;
        private const float CURB_AIR = 1f;
        private const float LAMP_AIR = 2.5f;
        private const float ARRIVAL_SLACK = 0.5f;

        //Where the footlights stand: CircusBackdrop's FOOTLIGHT_COUNT lamps, FOOTLIGHT_INSET inside the curb and
        //FOOTLIGHT_HEIGHT over the floor, which the Game cannot see (it is the library's own); CircusLandingTests holds them
        //equal
        internal const int FOOTLIGHT_COUNT = 4;
        internal const float FOOTLIGHT_INSET = 1.2f, FOOTLIGHT_HEIGHT = 2.2f;

        //How far outside the island's radius the pose must stand for a landing at all: the game's lens stands 28 to 45 out
        private const float ISLAND_CLEAR = 2f;

        /// <summary>How the gate home is timed, pointed and banked once laid (<see cref="IntroLanding.Fly"/>): the old gate
        /// shot's lens at first, easing to the game's; the meadow's settle; a light bank. The look, level down the gap as the
        /// old gate's was, turns in towards the arena and lifts towards the pose's target as the lens climbs out of it
        /// (<see cref="IntroLanding.Route.Lean"/>), further in than the meadow's (0.8 of the way, the axis 18 degrees off it
        /// on the circle) and up to the target's height (<see cref="IntroLanding.Style.LookRise"/>): the circle runs 4 to 13
        /// outside the pose and 7 to 16 from the island's edge, close under the cluster, and looked along it level with the
        /// meadow's 0.7, the cluster stood in the frame's top corner, the glass cut by its edge, and the island's cap filled
        /// the lower half.</summary>
        internal static readonly IntroLanding.Style Landing = new(WideLens: CircusIntroShots.GATE_LENS, ReleaseTimeScale: 2f,
            LookPitchFollow: 0.3f, LookDistance: 30f, LookSmoothSeconds: 0.3f, LookLandSeconds: 2.2f, LookInward: 0.8f,
            RollPerTurn: 0.1f, MaxRollDegrees: 8f, RollSmoothSeconds: 0.25f, Points: 240, LookRise: 1f);

        /// <summary>
        /// The gate home in <paramref name="circus"/>, landing on <paramref name="endPosition"/> looking at
        /// <paramref name="endTarget"/> with the game's <paramref name="fieldOfView"/>; null when no rolled route is clear.
        /// </summary>
        public static IntroShot Build(CircusSceneConfig circus, Vector3 endPosition, Vector3 endTarget, float fieldOfView, Random random)
        {
            if (circus == null) return null;

            Vector2 end = new(endPosition.X, endPosition.Z);
            if (end.Length() < ArenaIsland.RADIUS + ISLAND_CLEAR) return null;

            Vector2 facing = new(endTarget.X - endPosition.X, endTarget.Z - endPosition.Z);
            if (facing.LengthSquared() < 1e-6f) return null;
            facing.Normalize();

            var tent = new Tent(circus, IntroLanding.KeepOutFor(endPosition, float.MaxValue, ARRIVAL_SLACK));
            IntroLanding.Route best = IntroLanding.Best(CANDIDATES, () => Lay(circus, endPosition, facing, random), tent.Clear);
            return best == null ? null : IntroLanding.Fly(NAME, best, endPosition, endTarget, fieldOfView, Landing);
        }

        /// <summary>The radii of the circle round the arena a landing on a pose <paramref name="poseRadius"/> out may run on:
        /// inside the king poles where the pose leaves room, outside them otherwise (see the class's note).</summary>
        internal static (float From, float To) CircleRange(CircusSceneConfig circus, float poseRadius)
        {
            CircusTentConfig tent = circus.Tent;
            float inside = tent.KingPoleRadius - tent.KingPoleThickness - POLE_AIR - STEP;
            float from = poseRadius + CIRCLE_OVER_POSE;
            if (from <= inside) return (from, inside);

            float outside = MathF.Max(tent.KingPoleRadius + tent.KingPoleThickness + POLE_AIR + STEP, from);
            return (outside, outside + OUTER_WIDTH);
        }

        private static IntroLanding.Route Lay(CircusSceneConfig circus, Vector3 endPosition, Vector2 facing, Random random)
        {
            float runTo = Roll(random, RUN_TO_MIN, RUN_TO_MAX);
            float circleRoll = (float)random.NextDouble();
            float sweepIn = MathHelper.ToRadians(Roll(random, SWEEP_IN_MIN, SWEEP_IN_MAX));
            float sweepOut = MathHelper.ToRadians(Roll(random, SWEEP_OUT_MIN, SWEEP_OUT_MAX));
            float above = Roll(random, CIRCLE_ABOVE_MIN, CIRCLE_ABOVE_MAX);
            float climbLead = Roll(random, CLIMB_LEAD_MIN, CLIMB_LEAD_MAX);
            float climb = Roll(random, CLIMB_MIN, CLIMB_MAX);
            float cruise = Roll(random, CRUISE_MIN, CRUISE_MAX);
            float outerRunPast = Roll(random, OUTER_RUN_PAST_MIN, OUTER_RUN_PAST_MAX);
            double arrival = random.NextDouble();
            float slant = arrival < 0.35 ? 0f : arrival < 0.6 ? SLANT_SOME : arrival < 0.85 ? SLANT_MORE : SLANT_MOST;

            Vector2 end = new(endPosition.X, endPosition.Z);
            (float circleFromRadius, float circleToRadius) = CircleRange(circus, end.Length());
            float radius = MathHelper.Lerp(circleFromRadius, circleToRadius, circleRoll);
            float floor = circus.Ring.FloorY;

            //Outside the poles (see OUTER_RUN_PAST): a longer run, a wider sweep onto the circle and the climb begun at the
            //gate and done by the run's end, over the rake where it ends
            if (radius > circus.Tent.KingPoleRadius)
            {
                runTo = radius + outerRunPast;
                sweepIn = MathF.Max(sweepIn, MathHelper.ToRadians(OUTER_SWEEP_IN));
                above = MathF.Max(above, CircusIntroShots.SeatY(circus, runTo) + SEAT_AIR + OUTER_RISE_AIR - floor);
                climbLead = CircusIntroShots.GATE_FROM - runTo;
                climb = MathF.Min(climb, climbLead);
            }

            //Which way round: the shorter turn from the entrance's bearing to the pose's
            float gate = MathHelper.ToRadians(circus.Seating.EntranceBearingDegrees), poseBearing = IntroGround.Bearing(end);
            float anticlockwise = (poseBearing - gate) % MathHelper.TwoPi;
            if (anticlockwise <= 0f) anticlockwise += MathHelper.TwoPi;
            float clockwise = MathHelper.TwoPi - anticlockwise;
            int sign = anticlockwise <= clockwise ? 1 : -1;
            float turn = sign > 0 ? anticlockwise : clockwise;

            //The run in, down the gap's middle
            Vector2 outward = IntroGround.Heading(gate), inward = -outward;
            var plan = new List<Vector2>();
            IntroLanding.Straight(plan, outward * CircusIntroShots.GATE_FROM, outward * runTo, STEP);
            int runEnd = plan.Count - 1;

            //Off the entrance's line: onto the circle and round it, then the turn in onto the pose; or, for a pose near the
            //entrance's own bearing, the release straight off the run, arriving along the pose's facing or halfway to the run's
            int circleFrom, releaseStart;
            float circle = 0f;
            Vector2 arrive;
            bool direct = MathHelper.ToDegrees(turn) < DIRECT_TURN;
            if (direct)
            {
                circleFrom = releaseStart = runEnd;
                arrive = IntroLanding.Arrival(MathF.Min((float)arrival, 0.79f), facing, inward);
                IntroLanding.Release(plan, plan[^1], inward, end, arrive, STEP);
            }
            else
            {
                if (sweepIn + sweepOut > turn)
                {
                    float share = turn / (sweepIn + sweepOut);
                    sweepIn *= share;
                    sweepOut *= share;
                }

                float entryBearing = gate + sign * sweepIn;
                IntroLanding.Release(plan, plan[^1], inward, IntroGround.Heading(entryBearing) * radius, Tangent(entryBearing, sign), STEP);
                circleFrom = plan.Count - 1;

                float arc = turn - sweepIn - sweepOut;
                int arcSteps = (int)MathF.Ceiling(arc * radius / STEP);
                for (int i = 1; i <= arcSteps; i++)
                    plan.Add(IntroGround.Heading(entryBearing + sign * arc * i / arcSteps) * radius);
                circle = arc * radius;

                releaseStart = plan.Count - 1;
                IntroLanding.TurnIn(plan, entryBearing + sign * arc, radius, sign, end, slant, STEP);
                arrive = Vector2.Normalize(plan[^1] - plan[^2]);
            }

            if (!IntroLanding.NoTighterThan(plan, runEnd, MIN_TURN_RADIUS, releaseStart + 3)
                || !IntroLanding.NoTighterThan(plan, releaseStart, RELEASE_TURN_RADIUS)) return null;

            //The lengths; where the climb starts (short of the run's end), the pace picks up, it reaches the cruise's and the
            //deceleration begins (the turn in off the circle, or the last stretch of a release straight off the run)
            var length = new float[plan.Count];
            for (int i = 1; i < plan.Count; i++) length[i] = length[i - 1] + Vector2.Distance(plan[i - 1], plan[i]);
            float total = length[^1];

            cruise = MathF.Min(MathF.Max(cruise, circle / CIRCLE_SECONDS), CRUISE_FASTEST);

            //The deceleration takes at least SETTLE_SECONDS (the shared landing's lasts ReleaseTimeScale times its length over
            //the speed it enters with), so the last half second is slow whatever the cruise
            float settle = MathF.Max(cruise * SETTLE_SECONDS / Landing.ReleaseTimeScale,
                direct ? SETTLE_LENGTH : total - length[releaseStart]);
            int settleFrom = plan.Count - 2;
            while (settleFrom > runEnd && total - length[settleFrom] < settle) settleFrom--;

            float sRise = length[runEnd] - climbLead;
            float sAccelerate = length[runEnd] - ACCELERATE_BEFORE;
            float sCruise = MathF.Min(direct ? length[settleFrom] : length[circleFrom], length[settleFrom]);

            //The heights and the speeds: the gate's height over the floor easing up to the circle's over the climb, and the
            //run's pace up to the cruise's; over the release, the height eased from where it was onto the pose's while the
            //shared landing decelerates
            float low = floor + CircusIntroShots.GATE_HEIGHT, circleY = floor + above;
            var path = new List<Vector3>(plan.Count);
            var speed = new float[plan.Count];
            var lean = new float[plan.Count];
            float releaseY = 0f, sRelease = length[releaseStart];
            for (int i = 0; i < plan.Count; i++)
            {
                float s = length[i], y;
                float climbed = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((s - sRise) / climb, 0f, 1f));
                if (i <= releaseStart)
                {
                    y = MathHelper.Lerp(low, circleY, climbed);
                    releaseY = y;
                }
                else y = MathHelper.Lerp(releaseY, endPosition.Y, MathHelper.SmoothStep(0f, 1f, (s - sRelease) / MathF.Max(total - sRelease, 1e-3f)));

                if (i <= settleFrom)
                    speed[i] = MathHelper.Lerp(GATE_SPEED, cruise, MathHelper.SmoothStep(0f, 1f,
                        MathHelper.Clamp((s - sAccelerate) / MathF.Max(sCruise - sAccelerate, 1f), 0f, 1f)));

                //The look down the gap level, as the old gate's was, turning in towards the arena and lifting towards the
                //cluster as the lens climbs out of the gap (Landing's LookInward and LookRise)
                lean[i] = climbed;

                path.Add(new Vector3(plan[i].X, y, plan[i].Y));
            }
            path[^1] = endPosition;

            //How long it will take (the shared landing's own timing), so the one nearest TARGET_SECONDS is preferred
            float estimate = 0f;
            for (int i = 1; i <= settleFrom; i++) estimate += (length[i] - length[i - 1]) / speed[i];
            estimate += Landing.ReleaseTimeScale * (total - length[settleFrom]) / speed[settleFrom];

            //The best is the length asked for, arriving along the pose's facing
            float swivel = MathF.Acos(MathHelper.Clamp(Vector2.Dot(arrive, facing), -1f, 1f));

            return new IntroLanding.Route
            {
                Path = path, Speed = speed, DecelerateFrom = settleFrom, Lean = lean,
                Score = -MathF.Abs(estimate - TARGET_SECONDS) - 2f * swivel + (float)random.NextDouble() * 0.5f,
            };
        }

        /// <summary>The circle's direction of travel at <paramref name="bearing"/>, going round the way <paramref name="sign"/> says
        /// (positive is the bearing growing).</summary>
        private static Vector2 Tangent(float bearing, int sign) => new(-sign * MathF.Sin(bearing), sign * MathF.Cos(bearing));

        private static float Roll(Random random, float min, float max) => min + (max - min) * (float)random.NextDouble();

        /// <summary>
        /// What a point of the flight may not be near, off the tent's own figures (see the class's note): it has no CPU
        /// geometry, and these are the quadrics <c>Circus.fx</c> traces and <c>CircusBackdrop</c> writes into the depth.
        /// </summary>
        internal sealed class Tent
        {
            private readonly CircusSceneConfig _circus;
            private readonly float _keepOut;
            private readonly float _entrance;
            private readonly float _roofA, _roofB;
            private readonly Vector2[] _poles;
            private readonly Vector3[] _lamps;

            /// <param name="keepOut">The nearest the axis a point may stand (<see cref="IntroLanding.KeepOutFor"/>).</param>
            public Tent(CircusSceneConfig circus, float keepOut)
            {
                _circus = circus;
                _keepOut = keepOut;
                _entrance = MathHelper.ToRadians(circus.Seating.EntranceBearingDegrees);

                CircusTentConfig tent = circus.Tent;
                _roofB = (tent.CrownRadius - tent.WallRadius) / (tent.CrownY - tent.WallTopY);
                _roofA = tent.WallRadius - _roofB * tent.WallTopY;

                _poles = new Vector2[Math.Clamp(tent.KingPoleCount, 0, 6)];
                for (int p = 0; p < _poles.Length; p++)
                    _poles[p] = IntroGround.Heading(CircusIntroShots.PoleBearing(circus, p)) * tent.KingPoleRadius;

                _lamps = new Vector3[FOOTLIGHT_COUNT];
                for (int k = 0; k < FOOTLIGHT_COUNT; k++)
                {
                    Vector2 at = IntroGround.Heading((k + 0.5f) * MathHelper.TwoPi / FOOTLIGHT_COUNT) * (circus.Ring.CurbRadius - FOOTLIGHT_INSET);
                    _lamps[k] = new Vector3(at.X, circus.Ring.FloorY + FOOTLIGHT_HEIGHT, at.Y);
                }
            }

            /// <summary>Whether a lens at <paramref name="p"/> is clear of everything the flight keeps off.</summary>
            public bool Clear(Vector3 p)
            {
                CircusTentConfig tent = _circus.Tent;
                CircusRingConfig ring = _circus.Ring;
                CircusSeatingConfig seats = _circus.Seating;
                float r = MathF.Sqrt(p.X * p.X + p.Z * p.Z);

                if (r < _keepOut || r > tent.WallRadius - WALL_AIR) return false;
                if (p.Y < ring.FloorY + LOWEST || _roofA + _roofB * p.Y < r + WALL_AIR) return false;

                //The seats: over the rake (the front riser's top in front of the first row), unless well inside the gap
                if (r > seats.InnerRadius - SEAT_AIR && !InGap(p, r)
                    && p.Y < CircusIntroShots.SeatY(_circus, MathF.Max(r, seats.InnerRadius)) + SEAT_AIR) return false;

                foreach (Vector2 pole in _poles)
                    if (Vector2.Distance(new Vector2(p.X, p.Z), pole) < tent.KingPoleThickness + POLE_AIR) return false;

                if (r > ring.CurbRadius - CURB_AIR && r < ring.CurbRadius + ring.CurbThickness + CURB_AIR
                    && p.Y < ring.FloorY + ring.CurbHeight + CURB_AIR) return false;

                foreach (Vector3 lamp in _lamps)
                    if (Vector3.Distance(p, lamp) < LAMP_AIR) return false;

                return true;
            }

            /// <summary>Whether the point stands within the entrance's gap, <see cref="GAP_AIR"/> inside the walls the seats
            /// are cut along (a strip as wide as the gap at the front row, so narrower than the gap everywhere behind it).</summary>
            public bool InGap(Vector3 p, float r) =>
                MathF.Abs(MathHelper.WrapAngle(MathF.Atan2(p.Z, p.X) - _entrance)) * r < _circus.Seating.EntranceWidth * 0.5f - GAP_AIR;
        }
    }
}
