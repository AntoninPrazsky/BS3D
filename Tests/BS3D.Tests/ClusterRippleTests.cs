using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The wave a landing sends through the cluster (#582 moved it out of the Game as <see cref="ClusterRipple"/>):
    /// it travels by <b>connectivity</b> — a ball's delay is its breadth-first distance over balls that touch,
    /// an empty cell passes nothing on, the reach is bounded by <see cref="ClusterRipple.MAX_HOPS"/> — and a
    /// ball at hop <i>h</i> starts to glow <i>h</i> × <see cref="ClusterRipple.HOP_SECONDS"/> after the start.
    /// No simulation is needed: the walk reads only which cells hold a <see cref="PhysicsBall"/>.
    /// </summary>
    public class ClusterRippleTests
    {
        private const float EPSILON = 1e-5f;

        private static PhysicsBall[,,] Fill(int sx, int sz, int levels)
        {
            PhysicsBall[,,] balls = new PhysicsBall[sx, sz, levels];

            for (int x = 0; x < sx; x++)
                for (int z = 0; z < sz; z++)
                    for (int level = 0; level < levels; level++)
                        balls[x, z, level] = new PhysicsBall();

            return balls;
        }

        //The hop a ball was armed at, read back off the countdown LightBall writes; -1 when the wave never
        //reached it
        private static int Hop(PhysicsBall ball)
        {
            if (ball.RippleAmplitude == 0f) return -1;

            float hops = -ball.RippleTime / ClusterRipple.HOP_SECONDS;
            int rounded = (int)MathF.Round(hops);
            Assert.True(MathF.Abs(hops - rounded) < 1e-3f, $"a countdown of {ball.RippleTime} s is not a whole hop");

            return rounded;
        }

        //An independent breadth-first walk over occupied cells, written without the class's own scratch, so
        //the test states the rule rather than repeating the code
        private static Dictionary<XZLevel, int> ReferenceHops(PhysicsBall[,,] balls, XZLevel origin)
        {
            XZLevel size = XZLevel.FromArray(balls);
            Dictionary<XZLevel, int> hops = new() { [origin] = 0 };
            Queue<XZLevel> queue = new();
            queue.Enqueue(origin);

            Span<XZLevel> buffer = stackalloc XZLevel[BallsMap.MAX_NEIGHBORS];

            while (queue.Count > 0)
            {
                XZLevel cell = queue.Dequeue();
                if (hops[cell] >= ClusterRipple.MAX_HOPS) continue;

                int count = BallsMap.FillNeighboringCells(cell, size, buffer);
                for (int i = 0; i < count; i++)
                {
                    XZLevel next = buffer[i];
                    if (hops.ContainsKey(next) || balls[next.X, next.Z, next.Level] == null) continue;

                    hops[next] = hops[cell] + 1;
                    queue.Enqueue(next);
                }
            }

            return hops;
        }

        /// <summary>
        /// Along a single row the hop is the distance, and the wave stops dead at
        /// <see cref="ClusterRipple.MAX_HOPS"/>: nothing further out is armed at all.
        /// </summary>
        [Fact]
        public void RowIsLitByDistanceAndStopsAtTheReach()
        {
            int length = ClusterRipple.MAX_HOPS + 6;
            PhysicsBall[,,] balls = Fill(length, 1, 1);

            new ClusterRipple().StartAt(balls, new XZLevel(0, 0, 0));

            //The ball AT the reach is armed with a zero amplitude — the squared falloff reaches nothing there —
            //so it is the last one the walk touches and the first one that does not glow
            for (int x = 0; x < length; x++)
                Assert.Equal(x < ClusterRipple.MAX_HOPS ? x : -1, Hop(balls[x, 0, 0]));

            Assert.Equal(-ClusterRipple.MAX_HOPS * ClusterRipple.HOP_SECONDS,
                balls[ClusterRipple.MAX_HOPS, 0, 0].RippleTime, EPSILON);
            Assert.Equal(0f, balls[ClusterRipple.MAX_HOPS + 1, 0, 0].RippleTime);

            //And the amplitude falls off across the reach — full at the origin, less at every hop after
            Assert.Equal(1f, balls[0, 0, 0].RippleAmplitude, EPSILON);
            for (int x = 1; x < ClusterRipple.MAX_HOPS; x++)
                Assert.True(balls[x, 0, 0].RippleAmplitude < balls[x - 1, 0, 0].RippleAmplitude);
        }

        /// <summary>
        /// A hole is gone <b>around</b>, not through: with the middle of a row empty, the ball across the gap
        /// is reached by the detour through the next row, two hops longer than the straight line.
        /// </summary>
        [Fact]
        public void HoleIsGoneAroundNotThrough()
        {
            PhysicsBall[,,] balls = Fill(5, 2, 1);
            balls[2, 0, 0] = null;

            new ClusterRipple().StartAt(balls, new XZLevel(0, 0, 0));

            Assert.Equal(1, Hop(balls[1, 0, 0]));
            Assert.Equal(5, Hop(balls[3, 0, 0]));     //(1,0) or (0,1), then (1,1) (2,1) (3,1) and down to (3,0)
            Assert.Equal(6, Hop(balls[4, 0, 0]));
        }

        /// <summary>
        /// Over a three-level field with holes, every ball carries exactly its breadth-first distance by the
        /// lattice's own neighbour rule (both parities of level), and nothing unreachable is lit.
        /// </summary>
        [Fact]
        public void EveryBallCarriesItsConnectedDistance()
        {
            Random random = new(582);
            PhysicsBall[,,] balls = Fill(7, 6, 5);

            for (int x = 0; x < 7; x++)
                for (int z = 0; z < 6; z++)
                    for (int level = 0; level < 5; level++)
                        if (random.NextDouble() < 0.3) balls[x, z, level] = null;

            XZLevel origin = new(3, 3, 2);
            balls[3, 3, 2] = new PhysicsBall();

            new ClusterRipple().StartAt(balls, origin);
            Dictionary<XZLevel, int> expected = ReferenceHops(balls, origin);

            int lit = 0;
            for (int x = 0; x < 7; x++)
                for (int z = 0; z < 6; z++)
                    for (int level = 0; level < 5; level++)
                    {
                        PhysicsBall ball = balls[x, z, level];
                        if (ball == null) continue;

                        //A ball exactly at the reach is armed dark (see the row test), so it reads as unlit
                        int want = expected.TryGetValue(new XZLevel(x, z, level), out int h)
                            && h < ClusterRipple.MAX_HOPS ? h : -1;
                        Assert.Equal(want, Hop(ball));
                        if (want >= 0) lit++;
                    }

            Assert.True(lit > 10, $"the random field left only {lit} balls connected to the origin");
        }

        /// <summary>The origin seeds the walk even when the landed ball has already been released from it.</summary>
        [Fact]
        public void EmptyOriginStillSeedsTheWave()
        {
            PhysicsBall[,,] balls = Fill(3, 1, 1);
            balls[1, 0, 0] = null;

            new ClusterRipple().StartAt(balls, new XZLevel(1, 0, 0));

            Assert.Equal(1, Hop(balls[0, 0, 0]));
            Assert.Equal(1, Hop(balls[2, 0, 0]));
        }

        /// <summary>
        /// The ceiling's wave starts from every ball on the topmost <b>occupied</b> level at once, carries the
        /// alarm's sign, and runs down from there.
        /// </summary>
        [Fact]
        public void CeilingWaveStartsFromTheTopmostOccupiedLevel()
        {
            PhysicsBall[,,] balls = Fill(3, 3, 4);
            for (int x = 0; x < 3; x++)
                for (int z = 0; z < 3; z++)
                    balls[x, z, 3] = null;      //the top level of the field is empty

            new ClusterRipple().StartFromTop(balls);

            for (int x = 0; x < 3; x++)
                for (int z = 0; z < 3; z++)
                {
                    Assert.Equal(0, Hop(balls[x, z, 2]));
                    Assert.True(balls[x, z, 2].RippleAmplitude < 0f, "the ceiling's wave is the alarm");
                    Assert.Equal(1, Hop(balls[x, z, 1]));
                }
        }

        /// <summary>
        /// Reach per second: a ball at hop <i>h</i> is dark until <i>h</i> × <see cref="ClusterRipple.HOP_SECONDS"/>,
        /// peaks <see cref="ClusterRipple.ATTACK_SECONDS"/> later, and is at rest again once the flare's length
        /// has passed.
        /// </summary>
        [Fact]
        public void BallLightsOnItsTurnAndGoesOut()
        {
            PhysicsBall[,,] balls = Fill(6, 1, 1);
            new ClusterRipple().StartAt(balls, new XZLevel(0, 0, 0));

            PhysicsBall ball = balls[4, 0, 0];
            const float STEP = 0.001f;
            float firstLit = -1f, peakAt = -1f, peak = 0f, outAt = -1f;

            for (int i = 1; i <= 2000 && outAt < 0f; i++)
            {
                float brightness = ClusterRipple.Advance(ball, STEP);
                float t = i * STEP;

                if (brightness > 0f && firstLit < 0f) firstLit = t;
                if (brightness > peak) { peak = brightness; peakAt = t; }
                if (firstLit >= 0f && ball.RippleAmplitude == 0f) outAt = t;
            }

            float turn = 4 * ClusterRipple.HOP_SECONDS;
            Assert.Equal(turn, firstLit, 0.0025f);
            Assert.Equal(turn + ClusterRipple.ATTACK_SECONDS, peakAt, 0.0025f);
            Assert.Equal(turn + ClusterRipple.ATTACK_SECONDS + ClusterRipple.DECAY_SECONDS, outAt, 0.0025f);
        }
    }
}
