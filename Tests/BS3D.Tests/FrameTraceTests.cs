using BS3D.Screens;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>The benchmark's live graph (#988): what it keeps, in which order, and how tall a frame stands.</summary>
    public class FrameTraceTests
    {
        [Fact]
        public void KeepsFramesOldestFirst()
        {
            var trace = new FrameTrace();
            trace.Add(1f);
            trace.Add(2f, apart: true);
            trace.Add(3f);
            Assert.Equal(3, trace.Count);
            Assert.Equal(1f, trace[0]);
            Assert.Equal(2f, trace[1]);
            Assert.Equal(3f, trace[2]);
            Assert.False(trace.IsApart(0));
            Assert.True(trace.IsApart(1));
            Assert.False(trace.IsApart(2));
        }

        /// <summary>Past its capacity the oldest frames go, and the newest stays last, its mark with it.</summary>
        [Fact]
        public void AFullGraphGivesUpTheOldest()
        {
            var trace = new FrameTrace();
            int added = FrameTrace.CAPACITY + 37;
            for (int i = 0; i < added; i++) trace.Add(i, apart: i % 2 == 0);
            Assert.Equal(FrameTrace.CAPACITY, trace.Count);
            Assert.Equal(37f, trace[0]);
            Assert.Equal(added - 1f, trace[FrameTrace.CAPACITY - 1]);
            for (int i = 0; i < trace.Count; i++)
            {
                Assert.Equal(37f + i, trace[i]);
                Assert.Equal((37 + i) % 2 == 0, trace.IsApart(i));
            }
        }

        [Fact]
        public void ClearEmptiesIt()
        {
            var trace = new FrameTrace();
            for (int i = 0; i < 10; i++) trace.Add(5f);
            trace.Clear();
            Assert.Equal(0, trace.Count);
            trace.Add(7f);
            Assert.Equal(1, trace.Count);
            Assert.Equal(7f, trace[0]);
        }

        /// <summary>The budget's line stands at half the graph; twice the budget and more fills it; nothing is below empty.</summary>
        [Fact]
        public void HeightsAreShareOfTwoBudgets()
        {
            const float budget = 13.333f;
            Assert.Equal(1f / FrameTrace.HEADROOM, FrameTrace.Height(budget, budget), 4);
            Assert.Equal(1f, FrameTrace.Height(budget * FrameTrace.HEADROOM, budget), 4);
            Assert.Equal(1f, FrameTrace.Height(500f, budget));
            Assert.Equal(0f, FrameTrace.Height(-1f, budget));
            Assert.Equal(0f, FrameTrace.Height(10f, 0f));
        }

        /// <summary>A frame held to the refresh lands on the budget give or take the timer's jitter: not over, as the figures count it.</summary>
        [Fact]
        public void OverUsesTheFiguresSlack()
        {
            const float budget = 13.333f;
            Assert.False(FrameTrace.Over(budget, budget));
            Assert.False(FrameTrace.Over(budget * 1.01f, budget));
            Assert.True(FrameTrace.Over(budget * 1.05f, budget));
        }
    }
}
