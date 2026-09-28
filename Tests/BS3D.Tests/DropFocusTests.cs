using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// Where the drop cinematic looks (#616). The reported shot, headless: part of a released group in the drain,
    /// the rest gone over the island's rim and falling past its side to the kill plane, culled one by one. The
    /// focus has to stay on the drain and never step; the plain mean it replaced is run through the same frames
    /// and has to fail both — the check's failing branch, seen to fire on the case it exists for
    /// (<c>BestPractices.md</c> §10).
    /// </summary>
    public class DropFocusTests
    {
        //GameplayScreen.KILL_PLANE_Y — the Game's, which the tests cannot reference
        private const float KILL_PLANE_Y = -42f;

        //How far off the drain's axis the funnel balls are ever placed, and so how far the focus may be
        private const float FUNNEL_SCATTER = 3f;
        private const float MAX_OFF_AXIS = FUNNEL_SCATTER;

        //What one frame may move the focus: the funnel balls themselves fall 0.05 a frame, so anything past this
        //is the population changing under the point rather than the subject moving
        private const float MAX_STEP = 0.2f;

        private sealed class Ball
        {
            public int Id;
            public Vector3 Position;
            public float Speed;
        }

        /// <summary>
        /// Twenty balls in the throat, eight falling past the rim on one side, the eight reaching the kill plane
        /// at staggered moments. Returns every frame's (focus, plain mean) while any ball is alive.
        /// </summary>
        private static List<(Vector3 Focus, bool Resolved, Vector3 Mean)> RunSplit()
        {
            var random = new Random(616);
            var balls = new List<Ball>();

            for (int i = 0; i < 20; i++)
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2.0);
                float reach = (float)random.NextDouble() * FUNNEL_SCATTER;
                balls.Add(new Ball
                {
                    Id = i,
                    Position = new Vector3(MathF.Cos(angle) * reach, -16f - (float)random.NextDouble() * 6f, MathF.Sin(angle) * reach),
                    Speed = 0.05f,
                });
            }

            //All on the +X side, as a group that spilled over one stretch of the rim does
            for (int i = 0; i < 8; i++)
            {
                float angle = -0.4f + 0.1f * i;
                float reach = ArenaIsland.RADIUS + 0.6f + 0.2f * i;
                balls.Add(new Ball
                {
                    Id = 100 + i,
                    Position = new Vector3(MathF.Cos(angle) * reach, -11f - 1.5f * i, MathF.Sin(angle) * reach),
                    Speed = 0.35f + 0.03f * i,
                });
            }

            var focus = new DropFocus(KILL_PLANE_Y);
            focus.Reset();

            var frames = new List<(Vector3, bool, Vector3)>();

            //The strays are all culled well inside this; the funnel balls are still in the throat at its end
            for (int frame = 0; frame < 200 && balls.Count > 0; frame++)
            {
                focus.BeginFrame();
                Vector3 sum = Vector3.Zero;

                foreach (Ball ball in balls)
                {
                    focus.Add(ball.Id, ball.Position);
                    sum += ball.Position;
                }

                bool resolved = focus.TryResolve(out Vector3 centre);
                frames.Add((centre, resolved, sum / balls.Count));

                foreach (Ball ball in balls) ball.Position.Y -= ball.Speed;
                balls.RemoveAll(b => b.Position.Y < KILL_PLANE_Y);
            }

            return frames;
        }

        private static float OffAxis(Vector3 p) => new Vector2(p.X, p.Z).Length();

        [Fact]
        public void A_split_group_is_framed_on_the_drain_and_never_steps()
        {
            var frames = RunSplit();

            for (int i = 0; i < frames.Count; i++)
            {
                Assert.True(frames[i].Resolved, $"frame {i}: the drain still holds twenty balls");
                Assert.True(OffAxis(frames[i].Focus) <= MAX_OFF_AXIS,
                    $"frame {i}: focus {OffAxis(frames[i].Focus):F2} off the drain's axis");

                if (i > 0)
                {
                    float step = Vector3.Distance(frames[i].Focus, frames[i - 1].Focus);
                    Assert.True(step <= MAX_STEP, $"frame {i}: focus moved {step:F2}");
                }
            }
        }

        [Fact]
        public void The_plain_mean_fails_the_same_split()
        {
            var frames = RunSplit();

            float worstOffAxis = 0f, worstStep = 0f;

            for (int i = 0; i < frames.Count; i++)
            {
                worstOffAxis = MathF.Max(worstOffAxis, OffAxis(frames[i].Mean));
                if (i > 0) worstStep = MathF.Max(worstStep, Vector3.Distance(frames[i].Mean, frames[i - 1].Mean));
            }

            Assert.True(worstOffAxis > MAX_OFF_AXIS, $"the mean stayed {worstOffAxis:F2} off the axis");
            Assert.True(worstStep > MAX_STEP, $"the mean never stepped more than {worstStep:F2}");
        }

        [Fact]
        public void A_group_wholly_over_the_edge_is_still_filmed()
        {
            var focus = new DropFocus(KILL_PLANE_Y);
            focus.Reset();
            focus.BeginFrame();

            for (int i = 0; i < 5; i++) focus.Add(i, new Vector3(ArenaIsland.RADIUS + 1f, -15f - i, 0f));

            Assert.True(focus.TryResolve(out Vector3 centre));
            Assert.Equal(0f, focus.FunnelShare);
            Assert.InRange(centre.X, ArenaIsland.RADIUS + 0.5f, ArenaIsland.RADIUS + 1.5f);
        }

        [Fact]
        public void The_drain_emptying_ends_the_shot_though_strays_still_fall()
        {
            var focus = new DropFocus(KILL_PLANE_Y);
            focus.Reset();

            focus.BeginFrame();
            focus.Add(1, new Vector3(0.5f, -20f, 0f));
            focus.Add(2, new Vector3(ArenaIsland.RADIUS + 1f, -15f, 0f));
            Assert.True(focus.TryResolve(out _));

            //The drain ball culled, the stray still falling
            focus.BeginFrame();
            focus.Add(2, new Vector3(ArenaIsland.RADIUS + 1f, -20f, 0f));
            Assert.False(focus.TryResolve(out _));
        }

        [Fact]
        public void A_stray_never_comes_back()
        {
            var focus = new DropFocus(KILL_PLANE_Y);
            focus.Reset();

            focus.BeginFrame();
            focus.Add(1, new Vector3(0f, -5f, 0f));
            focus.Add(2, new Vector3(ArenaIsland.RADIUS + 2f, -12f, 0f));
            focus.TryResolve(out _);

            //Bounced back in over the stone: still not the subject
            focus.BeginFrame();
            focus.Add(1, new Vector3(0f, -5f, 0f));
            focus.Add(2, new Vector3(10f, -5f, 0f));
            focus.TryResolve(out Vector3 centre);

            Assert.Equal(0f, centre.X, 3);
        }
    }
}
