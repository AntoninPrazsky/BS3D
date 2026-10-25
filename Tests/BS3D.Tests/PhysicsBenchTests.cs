using BS3D.Screens;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Physics;
using System.Globalization;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The benchmark's physics segment (#988): it hangs the level it names, takes exactly the steps it says in slices of the
    /// time it is given, and measures every one; the figures are printed for the record.
    /// </summary>
    public class PhysicsBenchTests
    {
        private readonly ITestOutputHelper _output;

        public PhysicsBenchTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void HangsTheLargestClusterAndMeasuresEveryStep()
        {
            Level level = Level.Load(Shipped.Level(PhysicsBench.LEVEL + ".json"));
            int expected = 0;
            foreach (var ball in new BallsMap(level.Map).GetStaticBallsArray())
                if (ball != null) expected++;

            using PhysicsBench bench = new(new BallsMap(level.Map), new WindField(new System.Numerics.Vector2(0.87f, 0.5f), 0.05f));
            Assert.Equal(expected, bench.Balls);

            //No time, no step
            bench.Run(0.0);
            Assert.Equal(0f, bench.Progress);

            int slices = 0;
            while (!bench.Done)
            {
                bench.Run(5.0);
                slices++;
                Assert.True(slices < 100000, "the bench never finished");
            }

            FrameStats.Summary s = bench.Summarize(), first = bench.SummarizeWarmup();
            Assert.Equal(PhysicsBench.STEPS, s.Frames);
            Assert.Equal(PhysicsBench.WARMUP_STEPS, first.Frames);
            Assert.True(s.Median > 0f && s.Median <= s.P99 && s.P99 <= s.Worst);
            _output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{PhysicsBench.LEVEL}: {bench.Balls} balls, {slices} slices, median {s.Median:0.000} ms, p99 {s.P99:0.000}, worst {s.Worst:0.00}, {PhysicsBench.TimeShare(s.Median) * 100f:0.0} % of every frame; warm-up worst {first.Worst:0.0} ms"));
        }

        [Fact]
        public void TheShareIsAStepOverTheTimeItSimulates()
        {
            Assert.Equal(1000f / 120f, PhysicsBench.STEP_BUDGET_MILLISECONDS, 3);
            Assert.Equal(0.5f, PhysicsBench.TimeShare(PhysicsBench.STEP_BUDGET_MILLISECONDS / 2f), 4);
        }
    }
}
