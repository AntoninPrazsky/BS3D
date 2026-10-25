using BS3D.Effects;
using Microsoft.Xna.Framework;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>The benchmark's action lens (#1018): where it stands round the gun, where it looks, and that it moves smoothly.</summary>
    public class BenchmarkDemoTests
    {
        private static readonly Vector3 Centre = new(0f, 6f, 0f), Gun = new(0f, -9f, 30f);
        private const float Distance = 34f, Fov = MathF.PI / 4.2f;

        private static BenchmarkDemo Begun()
        {
            var demo = new BenchmarkDemo();
            demo.Begin(Centre, Gun, Distance, Fov);
            return demo;
        }

        [Fact]
        public void TheBlendRisesFromThePlayPoseToTheLens()
        {
            BenchmarkDemo demo = Begun();
            Assert.Equal(0f, demo.Blend, 5);
            float last = 0f;
            for (int i = 0; i < 120; i++)
            {
                demo.Step(1f / 60f);
                Assert.True(demo.Blend >= last, "the blend never falls back");
                last = demo.Blend;
            }
            Assert.Equal(1f, demo.Blend, 5);
        }

        [Fact]
        public void TheLensCirclesTheGunCloseAndLowLookingAtTheCluster()
        {
            BenchmarkDemo demo = Begun();
            for (float age = 0f; age <= BenchmarkDemo.SECONDS; age += 0.5f)
            {
                demo.Pose(age, out Vector3 at, out Vector3 target, out float fov);
                float horizontal = new Vector2(at.X - Gun.X, at.Z - Gun.Z).Length();
                Assert.Equal(Distance * BenchmarkDemo.RADIUS_SHARE, horizontal, 3);
                Assert.InRange(at.Y - Gun.Y, BenchmarkDemo.HEIGHT - BenchmarkDemo.BOB - 1e-3f, BenchmarkDemo.HEIGHT + BenchmarkDemo.BOB + 1e-3f);
                Assert.Equal(Vector3.Lerp(Gun + Vector3.Up * BenchmarkDemo.LOOK_OVER_GUN, Centre, BenchmarkDemo.LOOK_SHARE), target);
                Assert.Equal(Fov * BenchmarkDemo.FOV_SCALE, fov, 5);
                //Never in front of the gun: the lens stays on its side of the gun, away from the cluster, or beside it
                Assert.True(Vector2.Dot(new Vector2(at.X - Gun.X, at.Z - Gun.Z), new Vector2(Centre.X - Gun.X, Centre.Z - Gun.Z)) <= 1e-3f,
                    $"the lens stands between the gun and the cluster at {age} s");
            }

            //It passes straight behind the gun on its way, looking along the shot
            Assert.True(BenchmarkDemo.START_ANGLE > 0f && BenchmarkDemo.START_ANGLE + BenchmarkDemo.SWEEP < 0f);
            Assert.Equal(BenchmarkDemo.START_ANGLE, demo.AngleAt(0f), 5);
            Assert.Equal(BenchmarkDemo.START_ANGLE + BenchmarkDemo.SWEEP, demo.AngleAt(BenchmarkDemo.SECONDS), 5);
        }

        /// <summary>No jump between frames anywhere in the run: a benchmark's camera that cut would read as a fault.</summary>
        [Fact]
        public void TheLensMovesSmoothly()
        {
            BenchmarkDemo demo = Begun();
            demo.Pose(0f, out Vector3 last, out _, out _);
            for (float age = 1f / 60f; age <= BenchmarkDemo.SECONDS; age += 1f / 60f)
            {
                demo.Pose(age, out Vector3 at, out _, out _);
                Assert.True(Vector3.Distance(at, last) < 0.05f, $"a step of {Vector3.Distance(at, last)} at {age:0.00} s");
                last = at;
            }
        }

        [Fact]
        public void ItIsDoneAfterItsTimeAndNotBefore()
        {
            BenchmarkDemo demo = Begun();
            demo.Step(BenchmarkDemo.SECONDS - 0.1f);
            Assert.False(demo.Done);
            demo.Step(0.2f);
            Assert.True(demo.Done);
            demo.Stop();
            Assert.False(demo.Running);
            Assert.Equal(0f, demo.Blend);
        }
    }
}
