using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The wildcard's shared cycle (#330), and since #821 the direction each crossing sweeps the marble's veins in: every
    /// other crossing runs back down, so the front starts where the last one stopped. Pinned here because the shader
    /// trusts it: a direction that followed the colour's index would repeat itself where the index wraps.
    /// </summary>
    public class WildcardCycleTests
    {
        private static WildcardCycle CycleOf(params BallType[] colours)
        {
            WildcardCycle cycle = new();
            cycle.SetColours(colours);
            return cycle;
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(5)]
        public void Each_crossing_runs_the_other_way_from_the_one_before_through_the_wrap(int colours)
        {
            BallType[] palette = new BallType[colours];
            for (int i = 0; i < colours; i++) palette[i] = (BallType)(i + 1);
            WildcardCycle cycle = CycleOf(palette);

            bool last = cycle.Returning;
            for (int crossing = 0; crossing < colours * 3; crossing++)
            {
                cycle.Step(WildcardCycle.CROSSING_SECONDS);
                Assert.NotEqual(last, cycle.Returning);
                last = cycle.Returning;
            }
        }

        [Fact]
        public void A_long_frame_that_spans_two_crossings_flips_twice()
        {
            WildcardCycle cycle = CycleOf(BallType.Type1, BallType.Type2, BallType.Type3);
            bool before = cycle.Returning;

            cycle.Step(WildcardCycle.CROSSING_SECONDS * 2.5f);

            Assert.Equal(before, cycle.Returning);
            Assert.Equal(0.5f, cycle.Progress, 3);
        }

        [Fact]
        public void The_progress_and_the_colour_shown_keep_their_meaning()
        {
            WildcardCycle cycle = CycleOf(BallType.Type1, BallType.Type2);
            cycle.Step(WildcardCycle.CROSSING_SECONDS * 1.25f);

            //A quarter into the second crossing, which runs back down: still a quarter, and still showing the colour
            //it is leaving
            Assert.True(cycle.Returning);
            Assert.Equal(0.25f, cycle.Progress, 3);
            Assert.Equal(cycle.From, cycle.Showing);
        }
    }
}
