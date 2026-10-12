using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The pad's answer to a landing and the group it released (#810, the owner with the pad in hand: "it shakes my
    /// joints"): one pulse that grows with the count along a logarithm, a light tap for three to five balls, still growing
    /// visibly past twenty, longer rather than harder, led by the light motor, and never above its ceiling — neither on its
    /// own nor stacked on a release still ringing. <see cref="LandingRumble"/> is the curve; <see cref="PadChannel"/> is
    /// the mixer's own accumulation and decay, driven here as <c>GamepadRumble</c> drives it, at several frame rates.
    /// </summary>
    public class LandingRumbleTests
    {
        //Far past anything a level can release (the biggest shipped cluster is about 1200 balls)
        private const int MANY = 5000;

        private const float EPSILON = 1e-6f;

        [Fact]
        public void APlainLandingIsTheTap()
        {
            foreach (int none in new[] { 0, -1, int.MinValue })
            {
                (float left, float right, float seconds) = LandingRumble.Pulse(none);
                Assert.Equal(LandingRumble.TAP_LEFT, left);
                Assert.Equal(LandingRumble.TAP_RIGHT, right);
                Assert.Equal(LandingRumble.TAP_SECONDS, seconds);
            }
        }

        [Fact]
        public void EveryBallMoreIsNeverLess()
        {
            (float Left, float Right, float Seconds) previous = LandingRumble.Pulse(0);

            for (int count = 1; count <= MANY; count++)
            {
                var pulse = LandingRumble.Pulse(count);
                Assert.True(pulse.Left >= previous.Left, $"{count} balls: the heavy motor fell from {previous.Left} to {pulse.Left}");
                Assert.True(pulse.Right >= previous.Right, $"{count} balls: the light motor fell from {previous.Right} to {pulse.Right}");
                Assert.True(pulse.Seconds >= previous.Seconds, $"{count} balls: the pulse got shorter, {previous.Seconds} to {pulse.Seconds} s");
                previous = pulse;
            }

            //And it really grows with every ball up to the full count, not in steps
            for (int count = 0; count < LandingRumble.FULL_COUNT; count++)
                Assert.True(LandingRumble.Size(count + 1) > LandingRumble.Size(count), $"no growth from {count} to {count + 1} balls");
        }

        [Fact]
        public void TheGrowthSlows()
        {
            for (int count = 1; count < LandingRumble.FULL_COUNT; count++)
            {
                float before = LandingRumble.Size(count) - LandingRumble.Size(count - 1);
                float after = LandingRumble.Size(count + 1) - LandingRumble.Size(count);
                Assert.True(after <= before + EPSILON, $"at {count} balls the curve speeds up: +{before} then +{after}");
            }
        }

        [Fact]
        public void NeverAboveTheCeilingAndTheCeilingWellBelowFull()
        {
            //"Even a big one must not be too strong": two thirds of the motor's full at the very most
            Assert.True(LandingRumble.CEILING_LEFT <= 2f / 3f, $"the heavy motor's ceiling {LandingRumble.CEILING_LEFT} is not well below full");
            Assert.True(LandingRumble.CEILING_RIGHT <= 2f / 3f, $"the light motor's ceiling {LandingRumble.CEILING_RIGHT} is not well below full");

            for (int count = 0; count <= MANY; count++) AssertUnderTheCeiling(count);
            AssertUnderTheCeiling(int.MaxValue);

            //And the very largest releases do reach it
            var full = LandingRumble.Pulse(LandingRumble.FULL_COUNT);
            Assert.Equal(LandingRumble.CEILING_LEFT, full.Left, 5);
            Assert.Equal(LandingRumble.CEILING_RIGHT, full.Right, 5);
        }

        private static void AssertUnderTheCeiling(int count)
        {
            var pulse = LandingRumble.Pulse(count);
            Assert.True(pulse.Left <= LandingRumble.CEILING_LEFT, $"{count} balls: the heavy motor at {pulse.Left}");
            Assert.True(pulse.Right <= LandingRumble.CEILING_RIGHT, $"{count} balls: the light motor at {pulse.Right}");
        }

        [Fact]
        public void ThreeToFiveBallsAreALightTap()
        {
            for (int count = 3; count <= 5; count++)
            {
                var pulse = LandingRumble.Pulse(count);

                //More than a ball sticking, so a release is told from a plain landing...
                Assert.True(pulse.Left > LandingRumble.TAP_LEFT && pulse.Right > LandingRumble.TAP_RIGHT,
                    $"{count} balls feel no more than a plain landing");

                //...but light: the heavy motor under half its ceiling, and short
                Assert.True(pulse.Left <= LandingRumble.CEILING_LEFT / 2f, $"{count} balls: the heavy motor at {pulse.Left} is no tap");
                Assert.True(pulse.Seconds <= LandingRumble.RELEASE_SECONDS_SMALL + 0.1f, $"{count} balls ring for {pulse.Seconds} s");
            }
        }

        [Fact]
        public void ItStillGrowsVisiblyPastTwenty()
        {
            var twenty = LandingRumble.Pulse(20);
            var full = LandingRumble.Pulse(LandingRumble.FULL_COUNT);

            //Between twenty balls (about the median release) and the full count each motor gains at least a sixth of its
            //full, and the pulse gets longer by more than a quarter of a second
            Assert.True(full.Left - twenty.Left >= 1f / 6f, $"the heavy motor gains only {full.Left - twenty.Left} past twenty");
            Assert.True(full.Right - twenty.Right >= 1f / 6f, $"the light motor gains only {full.Right - twenty.Right} past twenty");
            Assert.True(full.Seconds - twenty.Seconds >= 0.25f, $"the pulse grows only {full.Seconds - twenty.Seconds} s longer past twenty");
        }

        [Fact]
        public void ABigReleaseGrowsLongerAndTheLightMotorLeads()
        {
            var small = LandingRumble.Pulse(3);
            var full = LandingRumble.Pulse(LandingRumble.FULL_COUNT);

            //Longer rather than harder: the biggest release rings at least three times as long as the smallest, while its
            //heavy motor is asked for less than three times as much
            Assert.True(full.Seconds >= 3f * small.Seconds, $"{small.Seconds} s to {full.Seconds} s");
            Assert.True(full.Left / small.Left < full.Seconds / small.Seconds,
                $"the heavy motor grows {full.Left / small.Left}x, the length only {full.Seconds / small.Seconds}x");

            //The buzz carries the top, not the thump
            Assert.True(LandingRumble.CEILING_RIGHT > LandingRumble.CEILING_LEFT);
            for (int count = 1; count <= MANY; count++)
            {
                var pulse = LandingRumble.Pulse(count);
                Assert.True(pulse.Right > pulse.Left, $"{count} balls: heavy {pulse.Left} over light {pulse.Right}");
            }
        }

        [Fact]
        public void TheSameCountGivesTheSameAnswer()
        {
            for (int count = 0; count <= 1000; count++)
                Assert.Equal(LandingRumble.Pulse(count), LandingRumble.Pulse(count));
        }

        /// <summary>
        /// The ceiling holds for the sum (#810, "count the landing tap and the release together under one cap"): a burst of
        /// landings, big ones on the tails of big ones, through the mixer's own channels kicked under the ceiling as
        /// <c>GamepadRumble.KickUnder</c> kicks them and decayed a frame at a time, at four frame rates and four cadences.
        /// </summary>
        [Fact]
        public void ABurstOfReleasesNeverStacksPastTheCeiling()
        {
            int[] counts = { LandingRumble.FULL_COUNT, 3, 120, LandingRumble.FULL_COUNT, 0, 40, MANY, 20 };

            foreach (float fps in new[] { 30f, 60f, 144f, 240f })
                foreach (float cadence in new[] { 0.05f, 0.1f, 0.25f, 0.5f })
                {
                    PadChannel left = default, right = default;
                    float dt = 1f / fps, untilNext = 0f;
                    int next = 0;

                    for (float clock = 0f; clock < 4f; clock += dt)
                    {
                        untilNext -= dt;
                        if (untilNext <= 0f)
                        {
                            var pulse = LandingRumble.Pulse(counts[next++ % counts.Length]);
                            left.Kick(pulse.Left, pulse.Seconds, LandingRumble.CEILING_LEFT);
                            right.Kick(pulse.Right, pulse.Seconds, LandingRumble.CEILING_RIGHT);
                            untilNext += cadence;
                        }

                        Assert.True(left.Level <= LandingRumble.CEILING_LEFT + EPSILON,
                            $"{fps} fps, a landing every {cadence} s: the heavy motor at {left.Level} at {clock:F2} s");
                        Assert.True(right.Level <= LandingRumble.CEILING_RIGHT + EPSILON,
                            $"{fps} fps, a landing every {cadence} s: the light motor at {right.Level} at {clock:F2} s");

                        left.Decay(dt);
                        right.Decay(dt);
                    }
                }
        }

        [Fact]
        public void AnOrdinaryKickStillSaturatesAtFull()
        {
            //Every kick that names no ceiling — the shot, the line loss, the menus — adds and saturates at 1, as before #810
            for (int r = 0; r <= 20; r++)
                for (int a = 1; a <= 20; a++)
                {
                    float ringing = r / 20f, amount = a / 20f;
                    Assert.Equal(Math.Clamp(ringing + amount, 0f, 1f), PadChannel.Stack(ringing, amount, 1f));
                }
        }

        [Fact]
        public void AChannelAboveTheCeilingIsNeitherRaisedNorLowered()
        {
            //The line loss's heavy tail ringing when a landing comes in under a lower ceiling
            Assert.Equal(0.9f, PadChannel.Stack(0.9f, 0.3f, LandingRumble.CEILING_LEFT));

            //Under it, the sum up to it
            Assert.Equal(LandingRumble.CEILING_LEFT, PadChannel.Stack(0.4f, 0.3f, LandingRumble.CEILING_LEFT));
            Assert.Equal(0.3f, PadChannel.Stack(0.1f, 0.2f, LandingRumble.CEILING_LEFT), 6);
        }

        [Fact]
        public void AKickFallsToSilenceOverItsOwnLength()
        {
            PadChannel channel = default;
            channel.Kick(0.5f, 0.5f);
            channel.Decay(0.25f);
            Assert.Equal(0.25f, channel.Level, 5);
            channel.Decay(0.25f);
            Assert.Equal(0f, channel.Level, 5);

            //A kick of no length still falls, fast, rather than dividing by zero
            channel.Kick(1f, 0f);
            channel.Decay(0.05f);
            Assert.Equal(0f, channel.Level);
        }
    }
}
