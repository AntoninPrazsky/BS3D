using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// The Grid's ride (#1116, the owner: "one shot could have the camera follow a motorcycle, moving at a similar speed to it,
    /// as if the camera rode beside the motorcycle"): a lens beside one of the light cycles, at its speed, for one shot of the
    /// chapter intro (<see cref="GridIntroShots"/>).
    /// <para>
    /// <b>Posed by the wall clock, every frame.</b> A cycle is a pure function of the wall clock (<see cref="LightCycleRuns"/>,
    /// riders made by the drawn cycles' own code on their own seeds), and a shot's own clock is not: it is eased, held to a
    /// frame's worth through a hitch and stopped by a pause, and on a path laid out in advance the eased clock alone drifts 1.8
    /// units off a cycle over the shot's 2.6 seconds; a level's first frames put the shot on screen nearly a second late, 23
    /// units of the cycle's run. So the lens is put where the cycle is at the second the frame is drawn at
    /// (<see cref="IntroShot.OnTheWallClock"/>), and the cycle filmed is the cycle on screen.
    /// </para>
    /// <para>
    /// <b>Turned with the cycle, not snapped.</b> The lens stands at a fixed place in a frame turned to the cycle's heading,
    /// averaged over <see cref="REACH_SECONDS"/> of its run either side (<see cref="Heading"/>): the cycle snaps a right angle at
    /// a node and the lens swings round it smoothly, starting before the node and finishing after it, so the cycle shows its own
    /// turn in the frame, its nose towards the lens or away, before the lens comes round beside it again. Its distance from the
    /// cycle never changes, so it is never nearer the rider and never in its path, and the rider holds its place in the frame.
    /// </para>
    /// <para>
    /// <b>Low, over the walls.</b> The lens rides <see cref="HEIGHT"/> over the floor, over every wall of light and every cycle,
    /// so it is inside none of them whatever the clock. Which cycle, and which side of it, is chosen (<see cref="Choose"/>) for
    /// the seconds the shot will play in: the lens keeps off every wall seen from above (its own rider's too, which a turn
    /// towards the lens brings round beside it), off every other cycle, off every solid and the ring, with nothing standing
    /// between it and the rider. Those seconds are the wall clock's, and the shot holds for its own: a pause stops the intro and
    /// not the wall clock, and under 20 fps every frame is held to a twentieth of a second of the shot. So a choice says how far
    /// it reaches (<see cref="Until"/>), and the shot chooses again past it (<see cref="GridIntroShots"/>).
    /// </para>
    /// </summary>
    internal sealed class GridRide
    {
        /// <summary>What a choice of cycle and side keeps the ride off, the strictest first.</summary>
        internal enum Keep
        {
            /// <summary>Everything the class doc names, the frame never swinging faster than <see cref="MAX_SWING_DEGREES"/>.</summary>
            Calm,

            /// <summary>Everything the class doc names, however fast the frame swings.</summary>
            Clear,

            /// <summary>The solids, the ring and the island alone: the lens rides over every wall and cycle whatever the clock,
            /// so this is a ride with nothing inside the lens, for a shot already on screen when nothing stricter is clear.</summary>
            Safe,
        }

        //Where the lens stands in the frame turned to the cycle: this far across from the cycle's middle, this far ahead of it
        //and this high over the floor (over the walls' 2.6 and the cycle's 2.5); and what it looks at, this far behind the
        //middle and this high, so the whole cycle stands near the middle of the frame, at any aspect from 4:3, with its wall
        //running back from it. ⚠ 1.5 behind put the front wheel at 0.94 of a 4:3 frame's half-width
        internal const float SIDE = 9f;
        internal const float AHEAD = 1.5f;
        internal const float HEIGHT = 3.4f;
        internal const float LOOK_BEHIND = 0.5f;
        internal const float LOOK_HEIGHT = 1.2f;

        //How many seconds of the cycle's run either side the heading the lens turns to is averaged over: the swing round one node
        //takes twice this and turns at most 90 / REACH_SECONDS degrees a second (37.5) at its middle; a U-turn, two right angles
        //the same way a node apart, at up to 71. ⚠ Both sides together, with the longest stretch a choice is made over, stay
        //inside the run a rider keeps (LightCycleRuns.KEPT_SECONDS): past it, the first frame posed after a choice would make the
        //cycle's run again from its start
        internal const float REACH_SECONDS = 2.4f;

        //What the lens keeps off, through the seconds the shot plays in: any wall seen from above; any other cycle's middle; any
        //solid or the ring (the cycles ride inside the solids' ring, but a cube near its inner edge reaches in among them, and
        //the lens rides 9 aside of its cycle); and the cycle it films keeps its own body off any solid. Every run keeps further
        //off the solids it was made among (LightCycles.SOLID_CLEARANCE, #1136), so that last binds only on a solid handed to
        //the ride and not to its runs
        internal const float WALL_CLEARANCE = 3f;
        internal const float CYCLE_CLEARANCE = LightCycleRuns.BODY_REACH + 2f;
        internal const float SOLID_CLEARANCE = 6f;
        internal const float RIDER_SOLID_CLEARANCE = LightCycleRuns.BODY_REACH + 1f;

        //The line from the lens to the rider: kept this far off any other cycle's wall and middle, and off any solid
        private const float SIGHT_WALL_CLEARANCE = 1f;
        private const float SIGHT_SOLID_CLEARANCE = 0.5f;
        private static readonly float[] SIGHT_POINTS = { 0.2f, 0.4f, 0.6f, 0.8f };

        //Nearer the axis than this the lens would be over the island; the cycles keep 56 off it, so it never binds
        private const float ISLAND_CLEARANCE = ArenaIsland.RADIUS + 6f;

        //The fastest a calm ride's frame swings round its cycle, degrees a second (Keep.Calm): a cycle that turns three right
        //angles within a few nodes (circling at the edge of its ring) swings it faster than a U-turn's 71, at up to about 100.
        //⚠ Averaged over 1.2 s either side, one right angle swung at 75 a second and a U-turn at 123 (150 averaged as vectors,
        //see Heading), and refusing every U-turn left two reels in five without a ride
        internal const float MAX_SWING_DEGREES = 80f;

        //How finely the seconds are tested
        private const float STEP = 1f / 30f;

        //How much a turn more than the first costs a candidate, against how squarely it looks in towards the arena (-1 to 1,
        //averaged over the shot): one turn shows the snap and the corner of the wall, more are busy
        private const float EXTRA_TURN_COST = 0.25f;

        private const int SIDES = 2;

        private readonly LightCycleRuns _runs;
        private readonly float _floorY;
        private readonly IReadOnlyList<GridSolid> _solids;
        private readonly GridRing? _ring;

        //The candidates' state while they are tested, made once, so a choice made while the shot is on screen allocates nothing.
        //A choice reads its stretch and REACH_SECONDS either side, and the frames posed after it, or another choice at its first
        //second, read back across all of it: inside LightCycleRuns.KEPT_SECONDS, so no rider makes its run again from its start
        //(GridRideTests holds the intro's longest stretch to it)
        private readonly bool[] _clear = new bool[LightCycleRuns.COUNT * SIDES];
        private readonly float[] _inward = new float[LightCycleRuns.COUNT * SIDES];
        private readonly int[] _turns = new int[LightCycleRuns.COUNT];
        private readonly Vector2[] _positions = new Vector2[LightCycleRuns.COUNT];
        private readonly Vector2[] _headings = new Vector2[LightCycleRuns.COUNT];

        /// <summary>The cycle the lens rides beside.</summary>
        public int Cycle { get; private set; }

        /// <summary>Which side of it the lens rides: 1 where its heading turned a quarter turn from +X towards +Z points, -1 the
        /// other.</summary>
        public float Side { get; private set; }

        /// <summary>The last second on the wall clock the cycle and side were chosen for (<see cref="Choose"/>), or held to
        /// (<see cref="Hold"/>): posed later than that, nothing has checked what the lens keeps off.</summary>
        public float Until { get; private set; }

        private GridRide(LightCycleRuns runs, float floorY, IReadOnlyList<GridSolid> solids, GridRing? ring)
        {
            _runs = runs;
            _floorY = floorY;
            _solids = solids ?? Array.Empty<GridSolid>();
            _ring = ring;
        }

        /// <summary>
        /// A ride beside whichever cycle, on whichever side, keeps off what <paramref name="keep"/> names from
        /// <paramref name="from"/> to <paramref name="until"/> on the wall clock, the shot playing its first
        /// <paramref name="seconds"/>; null when none does.
        /// </summary>
        internal static GridRide Plan(LightCycleRuns runs, float floorY, IReadOnlyList<GridSolid> solids, GridRing? ring,
            float from, float until, float seconds, Keep keep = Keep.Calm)
        {
            var ride = new GridRide(runs, floorY, solids, ring);
            return ride.Choose(from, until, seconds, keep) ? ride : null;
        }

        /// <summary>
        /// Chooses the cycle and the side again for <paramref name="from"/> to <paramref name="until"/>, the shot playing its first
        /// <paramref name="seconds"/>: of the candidates that keep off what <paramref name="keep"/> names through all of it, the
        /// one that looks most squarely in towards the arena, with a turn more than the first costing
        /// <see cref="EXTRA_TURN_COST"/>; with <paramref name="stay"/>, the cycle and side already chosen or none, for a shot on
        /// screen, where another is a cut. False, and the choice left as it was, when none does.
        /// </summary>
        internal bool Choose(float from, float until, float seconds, Keep keep = Keep.Calm, bool stay = false)
        {
            bool strict = keep != Keep.Safe;
            Array.Fill(_clear, !stay);
            if (stay) _clear[Cycle * SIDES + (Side > 0f ? 0 : 1)] = true;
            Array.Clear(_inward);
            int samples = 0;
            float maxSwing = MathHelper.ToRadians(MAX_SWING_DEGREES);

            //How often each cycle turns while the shot plays: once a cycle, for both its sides
            for (int cycle = 0; cycle < LightCycleRuns.COUNT; cycle++) _turns[cycle] = Turns(cycle, from, from + seconds);

            //Evenly, at no more than STEP apart, so every step's swing is measured over the same time
            int steps = Math.Max((int)MathF.Ceiling((until - from) / STEP), 1);
            float step = (until - from) / steps;
            for (int k = 0; k <= steps; k++)
            {
                float t = from + k * step;
                for (int i = 0; i < LightCycleRuns.COUNT; i++) _runs.At(i, t, out _positions[i], out _);

                bool counted = t <= from + seconds;
                if (counted) samples++;

                for (int cycle = 0; cycle < LightCycleRuns.COUNT; cycle++)
                {
                    if (!_clear[cycle * SIDES] && !_clear[cycle * SIDES + 1]) continue;

                    Vector2 heading = Heading(cycle, t);
                    float swing = k > 0 ? MathF.Acos(MathHelper.Clamp(Vector2.Dot(heading, _headings[cycle]), -1f, 1f)) : 0f;
                    if (keep == Keep.Calm && swing > maxSwing * step)
                    {
                        _clear[cycle * SIDES] = _clear[cycle * SIDES + 1] = false;
                        continue;
                    }

                    _headings[cycle] = heading;
                    for (int s = 0; s < SIDES; s++)
                    {
                        int candidate = cycle * SIDES + s;
                        if (!_clear[candidate]) continue;

                        Frame(_positions[cycle], heading, s == 0 ? 1f : -1f, out Vector2 lens, out Vector2 look);
                        if (!Clear(cycle, t, lens, strict))
                        {
                            _clear[candidate] = false;
                            continue;
                        }

                        if (counted)
                        {
                            Vector2 view = look - lens;
                            if (view.LengthSquared() > 1e-6f && lens.LengthSquared() > 1e-6f)
                                _inward[candidate] += Vector2.Dot(Vector2.Normalize(view), -Vector2.Normalize(lens));
                        }
                    }
                }
            }

            int best = -1;
            float bestScore = float.NegativeInfinity;
            for (int candidate = 0; candidate < _clear.Length; candidate++)
            {
                if (!_clear[candidate]) continue;

                float score = _inward[candidate] / Math.Max(samples, 1) - EXTRA_TURN_COST * Math.Max(_turns[candidate / SIDES] - 1, 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (best < 0) return false;

            Cycle = best / SIDES;
            Side = best % SIDES == 0 ? 1f : -1f;
            Until = until;
            return true;
        }

        /// <summary>Keeps the cycle and the side as they are through <paramref name="until"/> on the wall clock, unchecked: for a
        /// shot that found nothing clear to choose (<see cref="Until"/>).</summary>
        internal void Hold(float until) => Until = until;

        /// <summary>Where the lens stands and what it looks at, at <paramref name="time"/> on the wall clock: the ride's
        /// <see cref="IntroShot.WallClockPose"/>.</summary>
        public void Pose(float time, out Vector3 position, out Vector3 target)
        {
            _runs.At(Cycle, time, out Vector2 middle, out _);
            Frame(middle, Heading(Cycle, time), Side, out Vector2 lens, out Vector2 look);
            position = new Vector3(lens.X, _floorY + HEIGHT, lens.Y);
            target = new Vector3(look.X, _floorY + LOOK_HEIGHT, look.Y);
        }

        /// <summary>
        /// The way the lens's frame faces at <paramref name="time"/>: cycle <paramref name="cycle"/>'s heading ANGLE averaged over
        /// <see cref="REACH_SECONDS"/> of its run either side, weighted by a raised cosine. The heading is constant along each
        /// lattice segment and turns a right angle or not at all at a node, so the average is exact: each segment's angle, unwound
        /// from the first in the window, times the kernel's weight over the part of the segment inside it. A right angle is
        /// swung over twice the reach, at most 90 / <see cref="REACH_SECONDS"/> degrees a second.
        /// <para>
        /// ⚠ The angle, not the heading vector: averaged as vectors, the two headings either side of a U-turn (two right angles
        /// the same way a node apart) cancel, the average shrinks to the short leg between them and swings round it at up to 150
        /// degrees a second, where the angle sweeps its 180 at the rate of its two turns' weights.
        /// </para>
        /// </summary>
        internal Vector2 Heading(int cycle, float time)
        {
            const float LATTICE = LightCycleRuns.LATTICE;
            float distance = MathF.Max(time, 0f) * LightCycleRuns.SPEED;
            float reach = REACH_SECONDS * LightCycleRuns.SPEED;

            float start = 0f, turned = 0f, average = 0f;
            Vector2 was = Vector2.Zero;
            long first = (long)MathF.Floor((distance - reach) / LATTICE), last = (long)MathF.Floor((distance + reach) / LATTICE);
            for (long j = first; j <= last; j++)
            {
                float a = MathF.Max(j * LATTICE - distance, -reach), b = MathF.Min((j + 1) * LATTICE - distance, reach);
                if (b <= a) continue;

                _runs.At(cycle, (j + 0.5f) * LATTICE / LightCycleRuns.SPEED, out _, out Vector2 heading);
                if (was == Vector2.Zero) start = MathF.Atan2(heading.Y, heading.X);
                else turned += MathF.Atan2(was.X * heading.Y - was.Y * heading.X, Vector2.Dot(was, heading));

                was = heading;
                average += turned * (Weight(b, reach) - Weight(a, reach));
            }

            float angle = start + average;
            return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }

        //How much of a raised-cosine window of half-width `reach` lies before `u` (0 at -reach, 1 at reach)
        private static float Weight(float u, float reach) =>
            (u + reach) / (2f * reach) + MathF.Sin(MathF.PI * u / reach) / MathHelper.TwoPi;

        //The lens and its look on the floor (world X, Z), in the frame turned to `heading`, `side` to the cycle's left or right
        private static void Frame(Vector2 middle, Vector2 heading, float side, out Vector2 lens, out Vector2 look)
        {
            var across = new Vector2(-heading.Y, heading.X) * side;
            lens = middle + across * SIDE + heading * AHEAD;
            look = middle - heading * LOOK_BEHIND;
        }

        //Whether a lens at `lens` (on the floor's plane) beside `cycle` at `time` keeps off everything the class doc names; not
        //strict, off the island, the ring and the solids alone
        private bool Clear(int cycle, float time, Vector2 lens, bool strict)
        {
            if (lens.Length() < ISLAND_CLEARANCE) return false;

            Vector2 rider = _positions[cycle];
            var lens3 = new Vector3(lens.X, _floorY + HEIGHT, lens.Y);
            var rider3 = new Vector3(rider.X, _floorY + LightCycleRuns.BODY_HEIGHT * 0.5f, rider.Y);

            if (_ring is GridRing ring && ring.Distance(lens3) < SOLID_CLEARANCE) return false;
            for (int i = 0; i < _solids.Count; i++)
            {
                GridSolid solid = _solids[i];
                if (solid.Distance(lens3) < SOLID_CLEARANCE || solid.Distance(rider3) < RIDER_SOLID_CLEARANCE) return false;
            }

            if (!strict) return true;

            for (int i = 0; i < LightCycleRuns.COUNT; i++)
            {
                if (_runs.WallDistance(i, time, lens) < WALL_CLEARANCE) return false;
                if (i != cycle && Vector2.Distance(lens, _positions[i]) < CYCLE_CLEARANCE) return false;
            }

            //Nothing between the lens and the rider: no other wall or cycle across the line, no solid
            foreach (float f in SIGHT_POINTS)
            {
                Vector2 point = Vector2.Lerp(lens, rider, f);
                Vector3 point3 = Vector3.Lerp(lens3, rider3, f);

                for (int i = 0; i < LightCycleRuns.COUNT; i++)
                {
                    if (i == cycle) continue;
                    if (_runs.WallDistance(i, time, point) < SIGHT_WALL_CLEARANCE) return false;
                    if (Vector2.Distance(point, _positions[i]) < LightCycleRuns.BODY_REACH) return false;
                }

                for (int i = 0; i < _solids.Count; i++)
                    if (_solids[i].Distance(point3) < SIGHT_SOLID_CLEARANCE) return false;
            }

            return true;
        }

        //How many times `cycle` snaps a right angle from `from` to `until` on the wall clock: once at every node it turns at
        private int Turns(int cycle, float from, float until)
        {
            const float HALF = LightCycleRuns.LATTICE * 0.5f / LightCycleRuns.SPEED;
            int turns = 0;
            _runs.At(cycle, from, out _, out Vector2 previous);
            int steps = (int)MathF.Ceiling((until - from) / HALF);
            for (int k = 1; k <= steps; k++)
            {
                _runs.At(cycle, MathF.Min(from + k * HALF, until), out _, out Vector2 heading);
                if (Vector2.Dot(previous, heading) < 0.5f) turns++;
                previous = heading;
            }

            return turns;
        }
    }
}
