using BS3D.Screens;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The launch's physics warm-up (#1086): its throwaway world must keep the solver working for as long as it is stepped,
    /// or it compiles the first steps' code and nothing the level's release runs after them.
    /// </summary>
    public class PhysicsWarmupTests
    {
        [Fact]
        public void ItsClusterIsStillAwakeAfterTheStartSwing()
        {
            using PhysicsBench bench = PhysicsWarmup.Build();
            Assert.Equal(196, bench.Balls);

            //Three seconds of steps: the start swing eases out over the first two
            while (bench.Taken < 360) bench.Run(50.0);

            Assert.True(bench.AwakeBodies >= bench.Balls, $"{bench.AwakeBodies} bodies awake of {bench.Balls} balls");
        }

        /// <summary>
        /// The sleep and the wake it runs between slices (#1241) hand the cluster back awake and stepping, so the solver it
        /// is there to compile still runs after them.
        /// </summary>
        [Fact]
        public void ItsClusterIsAwakeAfterEverySleepAndWake()
        {
            using PhysicsBench bench = PhysicsWarmup.Build();
            while (bench.Taken < 60) bench.Run(50.0);

            for (int cycle = 0; cycle < 3; cycle++)
            {
                bench.SleepAndWake();
                Assert.True(bench.AwakeBodies >= bench.Balls, $"after cycle {cycle}: {bench.AwakeBodies} bodies awake of {bench.Balls} balls");

                int taken = bench.Taken;
                while (bench.Taken < taken + 120) bench.Run(50.0);
                Assert.True(bench.AwakeBodies >= bench.Balls, $"a second after cycle {cycle}: {bench.AwakeBodies} bodies awake of {bench.Balls} balls");
            }
            Assert.Equal(3, bench.SleepCycles);
        }
    }
}
