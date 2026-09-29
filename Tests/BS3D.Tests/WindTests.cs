using Prazsky.BS3D.Physics;
using System;
using System.Numerics;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The air over a scene (#95): <see cref="WindField"/>'s arithmetic, and that <see cref="PhysicsWorld.Wind"/> really
    /// reaches the bodies through the integrator. The gate that says a scene's wind leaves the sag verdicts standing is
    /// <c>Tools/LevelGen</c>'s <c>--wind=</c>, whose figures are in <c>docs/game-session.md</c>; what is checked here is
    /// what cannot be seen from a level: that a gust is bounded and smooth, that the field is deterministic (a retry
    /// meets the same air), and that the push arrives.
    /// </summary>
    public class WindTests
    {
        private static readonly Vector2 HEADING = new(0.87f, 0.5f);

        [Fact]
        public void NoWindIsZeroAtEveryTime()
        {
            for (float t = 0f; t < 60f; t += 0.37f) Assert.Equal(Vector3.Zero, WindField.None.Acceleration(t));

            //A zero heading is no wind whatever the strength says, and so is a zero strength
            Assert.Equal(Vector3.Zero, new WindField(Vector2.Zero, 5f).Acceleration(3f));
            Assert.Equal(Vector3.Zero, new WindField(HEADING, 0f).Acceleration(3f));
            Assert.Equal(Vector3.Zero, new WindField(HEADING, -2f).Acceleration(3f));
        }

        [Fact]
        public void AGustNeverExceedsTheStrengthAndNeverPushesBackwards()
        {
            WindField wind = new(HEADING, 1.4f);

            for (float t = 0f; t < 300f; t += 0.05f)
            {
                Vector3 a = wind.Acceleration(t);

                Assert.True(a.Length() <= 1.4f + 1e-4f, $"{a.Length()} at {t}");
                Assert.Equal(0f, a.Y);

                //Along the heading and never against it
                Assert.True(Vector2.Dot(new Vector2(a.X, a.Z), wind.Heading) >= -1e-5f);
            }
        }

        [Fact]
        public void AGustVariesFromNearlyStillToNearlyFull()
        {
            float low = float.MaxValue, high = float.MinValue;

            for (float t = 0f; t < 600f; t += 0.02f)
            {
                float gust = WindField.Gust(t);
                low = MathF.Min(low, gust);
                high = MathF.Max(high, gust);
            }

            //A wind that stayed near one value would only lean the cluster, which is what it exists not to do
            Assert.True(high - low > 0.85f, $"gust spans only {low}..{high}");
        }

        [Fact]
        public void AGustHasNoStepInIt()
        {
            //A discontinuity would ring the sockets; the largest change between 10 ms samples of a smooth
            //two-sine gust is a small fraction of its range
            float previous = WindField.Gust(0f);
            float worst = 0f;

            for (float t = 0.01f; t < 120f; t += 0.01f)
            {
                float gust = WindField.Gust(t);
                worst = MathF.Max(worst, MathF.Abs(gust - previous));
                previous = gust;
            }

            Assert.True(worst < 0.05f, $"largest 10 ms change {worst}");
        }

        [Fact]
        public void TheFieldIsDeterministic()
        {
            //A retry meets the same gusts in the same order, because the clock starts at zero with the level
            WindField a = new(HEADING, 1f), b = new(HEADING, 1f);

            for (float t = 0f; t < 30f; t += 0.5f) Assert.Equal(a.Acceleration(t), b.Acceleration(t));
        }

        [Fact]
        public void AClusterKeptAwakeStaysAwakeWhereAnOrdinaryOneFallsAsleep()
        {
            using HungLevel ordinary = HungLevel.FromLevelFile(Shipped.Level("Pennant.json"));
            using HungLevel kept = HungLevel.FromLevelFile(Shipped.Level("Pennant.json"));

            kept.World.KeepClusterAwake(kept.Balls);

            ordinary.Run(40f);
            kept.Run(40f);

            static PhysicsBall AnyBall(HungLevel hung)
            {
                foreach (PhysicsBall ball in hung.Balls) if (ball != null) return ball;
                throw new InvalidOperationException("an empty level");
            }

            //A cluster in still air is designed to fall asleep between shots; one that is kept awake is stepped for
            //as long as the level lasts, which is what a wind needs (a sleeping island is not integrated at all)
            Assert.False(AnyBall(ordinary).BallReference.Awake, "an ordinary cluster should have settled to sleep");
            Assert.True(AnyBall(kept).BallReference.Awake, "a kept-awake cluster must not sleep");

            //And a ball of every kind of island: a cluster shot in two has two islands, and every ball is kept awake
            foreach (PhysicsBall ball in kept.Balls)
                if (ball != null) Assert.True(ball.BallReference.Awake);
        }

        [Fact]
        public void AWorldPushesABodyAlongTheWindAndDefaultsToStillAir()
        {
            using HungLevel still = HungLevel.FromLevelFile(Shipped.Level("Helix.json"));
            using HungLevel windy = HungLevel.FromLevelFile(Shipped.Level("Helix.json"));

            Assert.Equal(Vector3.Zero, still.World.Wind);

            windy.World.Wind = new Vector3(3f, 0f, 0f);
            Assert.Equal(new Vector3(3f, 0f, 0f), windy.World.Wind);

            //Neither may fall asleep under the test's feet: a sleeping island is not integrated, wind or no wind
            still.World.KeepClusterAwake(still.Balls);
            windy.World.KeepClusterAwake(windy.Balls);

            //The lowest ball hangs furthest from the sockets' anchor and so leans furthest
            float LowestBallX(HungLevel hung)
            {
                float lowestY = float.MaxValue, x = 0f;

                foreach (PhysicsBall ball in hung.Balls)
                {
                    if (ball == null || ball.BallReference.Pose.Position.Y >= lowestY) continue;
                    lowestY = ball.BallReference.Pose.Position.Y;
                    x = ball.BallReference.Pose.Position.X;
                }

                return x;
            }

            still.Run(5f);
            windy.Run(5f);

            //Both clusters settle from the way they were hung, so what is compared is the two against each other: the
            //pushed one has leaned along +X further than the one in still air
            Assert.True(LowestBallX(windy) - LowestBallX(still) > 0.2f,
                $"a 3 u/s2 wind moved the lowest ball only {LowestBallX(windy) - LowestBallX(still)} beyond still air");
        }
    }
}
