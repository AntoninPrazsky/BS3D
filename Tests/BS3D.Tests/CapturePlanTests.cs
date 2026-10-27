using BS3D.Capture;
using Prazsky.BS3D.Physics;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>The capture run's clock (#946): a fixed step a frame that the session's physics spends whole, and the window of frames written.</summary>
    public class CapturePlanTests
    {
        //GameplayScreen.PHYSICS_TIMESTEP and CAPTURE_MAX_STEPS_PER_FRAME / PHYSICS_MAX_STEPS_PER_FRAME, restated: they are private there
        private const float PhysicsStep = PhysicsWorld.FIXED_TIMESTEP;
        private const int CaptureCap = 16, PlayCap = 4;

        [Theory]
        [InlineData(15)]
        [InlineData(24)]
        [InlineData(30)]
        [InlineData(50)]
        [InlineData(60)]
        [InlineData(120)]
        [InlineData(144)]
        [InlineData(240)]
        public void TheStepIsRoundedUpNeverDownToTheClocksResolution(int fps)
        {
            CapturePlan plan = new(fps, 0f, 1f);

            //Never under the exact step: a deficit would be carried and paid out as a short frame and a long one
            Assert.True(plan.StepTicks * fps >= TimeSpan.TicksPerSecond);

            //And by less than a tick a frame (100 ns)
            Assert.True(plan.StepTicks * fps - TimeSpan.TicksPerSecond < fps);
        }

        [Theory]
        [InlineData(15)]
        [InlineData(24)]
        [InlineData(25)]
        [InlineData(30)]
        [InlineData(40)]
        [InlineData(50)]
        [InlineData(60)]
        [InlineData(90)]
        [InlineData(120)]
        [InlineData(144)]
        [InlineData(240)]
        public void TheSessionsPhysicsSpendsEveryFramesWholeTimeUnderTheCapturesCap(int fps)
        {
            CapturePlan plan = new(fps, 0f, 1f);
            float accumulator = 0f;
            int total = 0, frames = fps * 60;

            for (int frame = 0; frame < frames; frame++)
            {
                //GameplayScreen.StepPhysics, as it stands: the elapsed is the float read off the GameTime
                accumulator += plan.StepSeconds;
                int steps = 0;
                while (accumulator >= PhysicsStep && steps < CaptureCap)
                {
                    accumulator -= PhysicsStep;
                    steps++;
                }

                Assert.True(steps < CaptureCap, "no frame reaches the cap, which would drop its remainder");
                total += steps;
            }

            //A minute of game time is 7200 steps of 1/120 s, to within the one the accumulator still holds
            Assert.InRange(total, 7200 - 1, 7200 + 1);
        }

        [Fact]
        public void TheSessionsOwnCapWouldHaveRunASlowCaptureRateSlow()
        {
            //Why a capture raises it: at 24 frames a second a frame is five steps and the play cap is four, so the world ran at four fifths
            CapturePlan plan = new(24, 0f, 1f);
            float accumulator = 0f;
            int total = 0;

            for (int frame = 0; frame < 24 * 60; frame++)
            {
                accumulator += plan.StepSeconds;
                int steps = 0;
                while (accumulator >= PhysicsStep && steps < PlayCap)
                {
                    accumulator -= PhysicsStep;
                    steps++;
                }

                //What StepPhysics does on a frame that spent its cap
                if (steps == PlayCap) accumulator = 0f;
                total += steps;
            }

            Assert.InRange(total, 5700, 5800);
        }

        [Theory]
        [InlineData(0f, 60, 1)]
        [InlineData(1f, 60, 60)]
        [InlineData(10f, 60, 600)]
        [InlineData(0.5f, 30, 15)]
        [InlineData(12.34f, 30, 371)]
        public void TheWindowStartsAtTheFirstFrameWhoseGameTimeHasReachedTheStart(float from, int fps, int firstUpdate)
        {
            CapturePlan plan = new(fps, from, 1f);

            Assert.Equal(firstUpdate, plan.FirstUpdate);
            Assert.True(plan.GameSeconds(plan.FirstUpdate) >= from - 1e-3f);
            Assert.True(plan.FirstUpdate == 1 || plan.GameSeconds(plan.FirstUpdate - 1) < from);
        }

        [Fact]
        public void TheWindowHoldsTheSecondsAskedForAndZeroIsOneFrame()
        {
            Assert.Equal(300, new CapturePlan(60, 0f, 5f).FrameCount);
            Assert.Equal(75, new CapturePlan(30, 3f, 2.5f).FrameCount);
            Assert.Equal(1, new CapturePlan(60, 4f, 0f).FrameCount);

            CapturePlan plan = new(60, 2f, 1f);
            Assert.Equal(120, plan.FirstUpdate);
            Assert.Equal(60, plan.FrameCount);
        }

        [Fact]
        public void TimesAreFramesOverTheNominalRate()
        {
            CapturePlan plan = new(60, 2f, 1f);

            Assert.Equal(0f, plan.WindowSeconds(plan.FirstUpdate));
            Assert.Equal(0.5f, plan.WindowSeconds(plan.FirstUpdate + 30), 6);
            Assert.Equal(2f, plan.GameSeconds(120), 6);
            Assert.Equal(60f, new CapturePlan(30, 0f, 1f).GameSeconds(1800), 5);
        }

        [Fact]
        public void FilesAreNumberedFromOneInSixDigits()
        {
            Assert.Equal("frame-000001.png", CapturePlan.FrameFileName(1));
            Assert.Equal("frame-000300.png", CapturePlan.FrameFileName(300));
            Assert.Equal("frame-123456.png", CapturePlan.FrameFileName(123456));
        }

        [Theory]
        [InlineData(14, 0f, 1f)]
        [InlineData(241, 0f, 1f)]
        [InlineData(60, -1f, 1f)]
        [InlineData(60, 0f, -1f)]
        [InlineData(60, float.NaN, 1f)]
        public void ArgumentsThatCannotBeFilmedAreRefused(int fps, float from, float seconds) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new CapturePlan(fps, from, seconds));
    }
}
