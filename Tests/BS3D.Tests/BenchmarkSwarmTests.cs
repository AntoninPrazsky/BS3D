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
    /// the count.
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
            Assert.True(swarm.Held < broke && swarm.Held >= held);
            Assert.Equal(21f, swarm.BrokenMedian, 3);
            Assert.Equal(5, swarm.Steps);

            //And nothing moves once it is over
            Assert.False(swarm.Tick(1f));
        }

        [Fact]
        public void AMachineThatCannotHoldTheFirstStepHeldNothing()
        {
            var swarm = new BenchmarkSwarm();
            RunStep(swarm, 40f);
            Assert.True(RunStep(swarm, 40f));
            Assert.True(swarm.Done);
            Assert.Equal(0, swarm.Held);
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
