using BS3D.Screens;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The benchmark's swarm (#988): the ramp grows the storm while a step's median frame holds 60 FPS and stops at a count
    /// that breaks it twice running, the step's first moments left out; the storm's places are the same for an index whatever
    /// the count. A machine that cannot hold the first count is followed down by halves and grows back up under the count
    /// that broke, so the Pi gets a figure.
    /// </summary>
    public class BenchmarkSwarmTests
    {
        //A step's worth of frames of one length, at that length's own rate
        private static bool RunStep(BenchmarkSwarm swarm, float milliseconds)
        {
            bool ended = false;
            float seconds = 0f;
            while (seconds < BenchmarkSwarm.STEP_SECONDS + 0.01f && !ended)
            {
                ended = swarm.Tick(milliseconds);
                seconds += milliseconds / 1000f;
            }

            return ended;
        }

        [Fact]
        public void AStepThatHoldsGrowsTheStorm()
        {
            var swarm = new BenchmarkSwarm();
            Assert.Equal(BenchmarkSwarm.START_BALLS, swarm.Balls);

            Assert.True(RunStep(swarm, 8f));
            Assert.False(swarm.Done);
            Assert.Equal(BenchmarkSwarm.START_BALLS, swarm.Held);
            Assert.Equal((int)MathF.Ceiling(BenchmarkSwarm.START_BALLS * BenchmarkSwarm.GROWTH), swarm.Balls);
        }

        [Fact]
        public void TheFirstStepPastTheBudgetEndsItOnTheLastThatHeld()
        {
            var swarm = new BenchmarkSwarm();
            RunStep(swarm, 8f);
            RunStep(swarm, 12f);
            int held = swarm.Balls;
            RunStep(swarm, 16f);
            int broke = swarm.Balls;

            //Once past it the count is measured again; twice past it, the run is over
            Assert.True(RunStep(swarm, 20f));
            Assert.False(swarm.Done);
            Assert.Equal(broke, swarm.Balls);
            Assert.True(RunStep(swarm, 21f));
            Assert.True(swarm.Done);
            Assert.False(swarm.Capped);
            Assert.Equal(broke, swarm.Balls);
            Assert.Equal(broke, swarm.BrokeAt);
            Assert.True(swarm.Held < broke && swarm.Held >= held);
            Assert.Equal(21f, swarm.BrokenMedian, 3);
            Assert.Equal(5, swarm.Steps);

            //And nothing moves once it is over
            Assert.False(swarm.Tick(1f));
        }

        //Steps until the run is over, each frame as long as the machine draws the storm's count in
        private static void RunToEnd(BenchmarkSwarm swarm, Func<int, float> frameMilliseconds)
        {
            for (int steps = 0; !swarm.Done && steps < 100; steps++) RunStep(swarm, frameMilliseconds(swarm.Balls));
            Assert.True(swarm.Done);
        }

        [Fact]
        public void AMachineThatCannotHoldTheFirstCountIsFollowedDownAndGrowsBackUnderTheCountThatBroke()
        {
            //The Pi's shape (#988): 1024 breaks, half of it holds, and the way back up stops at the first count that breaks
            var swarm = new BenchmarkSwarm();
            RunToEnd(swarm, balls => 10f + balls * 0.012f);

            int half = BenchmarkSwarm.START_BALLS / BenchmarkSwarm.DESCENT;
            int up = (int)MathF.Ceiling(half * BenchmarkSwarm.GROWTH);
            Assert.Equal(half, swarm.Held);
            Assert.Equal(up, swarm.BrokeAt);
            Assert.False(swarm.Capped);
            //1024 twice, its half once, the count over it twice
            Assert.Equal(5, swarm.Steps);
        }

        [Fact]
        public void TheWayBackUpNeverMeasuresACountThatAlreadyBroke()
        {
            var swarm = new BenchmarkSwarm();
            RunToEnd(swarm, balls => balls < BenchmarkSwarm.START_BALLS ? 10f : 20f);

            //Up by GROWTH from the half while under 1024, and over then without measuring 1024's next count again
            int held = BenchmarkSwarm.START_BALLS / BenchmarkSwarm.DESCENT;
            while ((int)MathF.Ceiling(held * BenchmarkSwarm.GROWTH) < BenchmarkSwarm.START_BALLS)
                held = (int)MathF.Ceiling(held * BenchmarkSwarm.GROWTH);
            Assert.Equal(held, swarm.Held);
            Assert.Equal(BenchmarkSwarm.START_BALLS, swarm.BrokeAt);
            Assert.True(swarm.Balls < BenchmarkSwarm.START_BALLS);
        }

        [Fact]
        public void AMachineThatCannotHoldTheFewestHeldNothing()
        {
            var swarm = new BenchmarkSwarm();
            RunToEnd(swarm, _ => 40f);

            Assert.Equal(0, swarm.Held);
            Assert.Equal(BenchmarkSwarm.MIN_BALLS, swarm.BrokeAt);
            //Every count from the first down to the fewest, each broken twice
            int counts = (int)MathF.Round(MathF.Log2((float)BenchmarkSwarm.START_BALLS / BenchmarkSwarm.MIN_BALLS)) + 1;
            Assert.Equal(2 * counts, swarm.Steps);
        }

        [Fact]
        public void OneHitchOfAStepDoesNotEndTheRun()
        {
            //A count broken once and held when measured again goes on growing
            var swarm = new BenchmarkSwarm();
            RunStep(swarm, 8f);
            int count = swarm.Balls;
            RunStep(swarm, 30f);
            Assert.False(swarm.Done);
            Assert.Equal(count, swarm.Balls);

            RunStep(swarm, 9f);
            Assert.False(swarm.Done);
            Assert.Equal(count, swarm.Held);
            Assert.True(swarm.Balls > count);

            //And a later count gets its own second chance
            RunStep(swarm, 30f);
            Assert.False(swarm.Done);
        }

        [Fact]
        public void TheBrokenMedianIsTheCountThatBrokeNotAHitchThatHeld()
        {
            //On the way back up a count breaks once and holds when measured again: the figure kept is still BrokeAt's
            var swarm = new BenchmarkSwarm();
            RunStep(swarm, 30f);
            RunStep(swarm, 30f);
            Assert.Equal(BenchmarkSwarm.START_BALLS, swarm.BrokeAt);
            Assert.Equal(30f, swarm.BrokenMedian, 3);

            RunStep(swarm, 10f);
            RunStep(swarm, 25f);
            RunStep(swarm, 10f);
            Assert.False(swarm.Done);
            Assert.Equal(30f, swarm.BrokenMedian, 3);
        }

        [Fact]
        public void TheStepsFirstMomentsAreLeftOut()
        {
            //A long hitch inside the settle (the buffers growing for the new count) does not break a step that holds after it
            var swarm = new BenchmarkSwarm();
            swarm.Tick(BenchmarkSwarm.STEP_SETTLE_SECONDS * 1000f * 0.9f);
            for (int i = 0; i < 200 && !swarm.Tick(5f); i++) { }

            Assert.False(swarm.Done);
            Assert.Equal(BenchmarkSwarm.START_BALLS, swarm.Held);
        }

        [Fact]
        public void AMachineThatHoldsEveryStepStopsAtTheMost()
        {
            var swarm = new BenchmarkSwarm();
            int steps = 0;
            while (!swarm.Done && steps++ < 100) RunStep(swarm, 1f);

            Assert.True(swarm.Capped);
            Assert.Equal(BenchmarkSwarm.MAX_BALLS, swarm.Held);
            Assert.Equal(0, swarm.BrokeAt);
            Assert.True(swarm.Steps < 30, $"{swarm.Steps} steps to the cap: the run would last {swarm.Steps * BenchmarkSwarm.STEP_SECONDS} s");
        }

        [Fact]
        public void TheStormStandsInItsColumnTheSameForEveryCount()
        {
            for (int i = 0; i < 5000; i++)
            {
                Vector3 p = BenchmarkSwarm.Place(i);
                float radius = MathF.Sqrt(p.X * p.X + p.Z * p.Z);
                Assert.InRange(radius, 12.99f, 27.01f);
                Assert.True(p.Y > ArenaIsland.TOP_Y, $"ball {i} at {p.Y} is in the island's stone");
                Assert.Equal(p, BenchmarkSwarm.Place(i));
            }
        }
    }
}
