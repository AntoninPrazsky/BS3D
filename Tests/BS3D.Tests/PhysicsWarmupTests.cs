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
    }
}
