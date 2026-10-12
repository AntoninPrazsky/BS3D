using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The pad's answer to a landing and the group it released (#810, the owner with the pad in hand: "it shakes my
    /// joints"): a release that grows with the count along a logarithm, a light tap for three to five balls, still growing
    /// visibly past twenty, longer rather than harder, led by the light motor, and never above its ceiling — neither on its
    /// own, nor stacked on a release still ringing, nor under the next shot. <see cref="LandingRumble"/> is the curve and
    /// <see cref="LandingRumble.Apply"/> the very sum <c>GamepadRumble.Landing</c> sends; <see cref="PadChannel"/> is the
    /// mixer's own accumulation and decay, driven here as <c>GamepadRumble</c> drives it, at several frame rates.
    /// </summary>
    public class LandingRumbleTests
    {
        //Far past anything a level can release (the biggest shipped cluster is about 1200 balls)
        private const int MANY = 5000;

        private const float EPSILON = 1e-5f;

        //An ordinary kick heavier than the landing's ceiling and short, the shape of the shot's thump
        private const float HEAVY_KICK = 0.55f, HEAVY_KICK_SECONDS = 0.12f;

        //The gun's rattle as it rolls: a light-motor bump, a few hundredths, at most ten a second
        private const float RATTLE = 0.07f, RATTLE_SECONDS = 0.035f, RATTLE_EVERY = 0.1f;

        private static readonly float[] FrameRates = { 30f, 60f, 144f, 240f };

        [Fact]
        public void APlainLandingIsTheTap()
        {
            foreach (int none in new[] { 0, -1, int.MinValue })
            {
                (float left, float right, float seconds) = LandingRumble.Pulse(none);
                Assert.Equal(LandingRumble.TAP_LEFT, left);
                Assert.Equal(LandingRumble.TAP_RIGHT, right);
                Assert.Equal(LandingRumble.TAP_SECONDS, seconds);
                Assert.Equal((0f, 0f, 0f), LandingRumble.Release(none));
            }
        }

        /// <summary>
        /// The tap the owner felt and kept (#378) is an ordinary kick, as it was before #810: whatever is still ringing
        /// under it — the shot's thump on a short flight — it adds to it and saturates at the motor's full, the ceiling
        /// playing no part in it.
        /// </summary>
        [Fact]
        public void APlainLandingIsWhatItWasBeforeTheCeiling()
        {
            for (int r = 0; r <= 20; r++)
            {
                float ringing = r / 20f;
                PadChannel left = default, right = default;
                left.Kick(ringing, 1f);
                right.Kick(ringing, 1f);

                LandingRumble.Apply(ref left, ref right, 0);

                Assert.Equal(MathF.Min(1f, ringing + LandingRumble.TAP_LEFT), left.Level, 5);
                Assert.Equal(MathF.Min(1f, ringing + LandingRumble.TAP_RIGHT), right.Level, 5);
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
                Assert.True(after <= before + 1e-6f, $"at {count} balls the curve speeds up: +{before} then +{after}");
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
            Assert.True(pulse.Left <= LandingRumble.CEILING_LEFT + EPSILON, $"{count} balls: the heavy motor at {pulse.Left}");
            Assert.True(pulse.Right <= LandingRumble.CEILING_RIGHT + EPSILON, $"{count} balls: the light motor at {pulse.Right}");
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

            //The buzz carries the top, not the thump: at the landing's instant and over the release's own tail
            Assert.True(LandingRumble.CEILING_RIGHT > LandingRumble.CEILING_LEFT);
            for (int count = 1; count <= MANY; count++)
            {
                var pulse = LandingRumble.Pulse(count);
                Assert.True(pulse.Right > pulse.Left, $"{count} balls: heavy {pulse.Left} over light {pulse.Right}");

                var release = LandingRumble.Release(count);
                Assert.True(release.Right > release.Left, $"{count} balls: the tail's heavy {release.Left} over light {release.Right}");
            }
        }

        [Fact]
        public void TheSameCountGivesTheSameAnswer()
        {
            for (int count = 0; count <= 1000; count++)
            {
                Assert.Equal(LandingRumble.Pulse(count), LandingRumble.Pulse(count));
                Assert.Equal(LandingRumble.Release(count), LandingRumble.Release(count));
            }
        }

        /// <summary>
        /// The ceiling is the largest releases' alone (#810's review): the shot's thump is usually still ringing when its
        /// ball lands (0.08 s of flight on Pennant), and a release that summed onto it reached the heavy motor's ceiling
        /// from about 32 balls. Whatever ordinary kick is ringing below the ceiling, a release short of the full count
        /// stays under it, and more balls are never less.
        /// </summary>
        [Fact]
        public void WhateverIsRingingTheCeilingIsReachedOnlyAtTheFullCount()
        {
            for (int r = 0; r <= 17; r++)
            {
                float ringing = r * 0.02f; //up to 0.34: with the tap on it, under both ceilings
                float previousLeft = 0f, previousRight = 0f;

                foreach (int count in new[] { 1, 3, 5, 10, 20, 32, 47, 80, 150, 200, LandingRumble.FULL_COUNT - 1 })
                {
                    (PadChannel left, PadChannel right) = Ringing(ringing);
                    LandingRumble.Apply(ref left, ref right, count);

                    Assert.True(left.Level < LandingRumble.CEILING_LEFT,
                        $"{count} balls over {ringing} ringing: the heavy motor at its ceiling ({left.Level})");
                    Assert.True(right.Level < LandingRumble.CEILING_RIGHT,
                        $"{count} balls over {ringing} ringing: the light motor at its ceiling ({right.Level})");
                    Assert.True(left.Level >= previousLeft && right.Level >= previousRight,
                        $"{count} balls over {ringing} ringing felt less than fewer did");

                    previousLeft = left.Level;
                    previousRight = right.Level;
                }

                (PadChannel fullLeft, PadChannel fullRight) = Ringing(ringing);
                LandingRumble.Apply(ref fullLeft, ref fullRight, LandingRumble.FULL_COUNT);
                Assert.Equal(LandingRumble.CEILING_LEFT, fullLeft.Level, 4);
                Assert.Equal(LandingRumble.CEILING_RIGHT, fullRight.Level, 4);
            }

            //And over the measured flight's thump, a big release still reads bigger than the median one: a tenth of the
            //heavy motor's full at least between twenty balls and the full count
            float twenty = AfterAThump(20), full = AfterAThump(LandingRumble.FULL_COUNT);
            Assert.True(full - twenty >= 0.1f, $"over the shot's thump the heavy motor gains only {full - twenty} past twenty");
        }

        private static (PadChannel Left, PadChannel Right) Ringing(float level)
        {
            PadChannel left = default, right = default;
            left.Kick(level, 1f);
            right.Kick(level, 1f);
            return (left, right);
        }

        //The heavy motor at a landing 0.08 s after a shot-shaped kick, the flight measured on Pennant
        private static float AfterAThump(int released)
        {
            PadChannel left = default, right = default;
            left.Kick(HEAVY_KICK, HEAVY_KICK_SECONDS);
            left.Decay(0.08f);
            LandingRumble.Apply(ref left, ref right, released);
            return left.Level;
        }

        /// <summary>
        /// Longer rather than harder has to survive play (#810's review): every ordinary kick sets its own fall, and when
        /// one fall was shared by everything on a motor the gun's rattle (0.035 s) cut a big release's 0.9 s tail to its
        /// own length the moment the gun rolled. With the rattle and a shot-shaped kick going off over it, a release is
        /// never felt less than it is on its own.
        /// </summary>
        [Fact]
        public void NoOrdinaryKickCutsAReleaseShort()
        {
            foreach (float fps in FrameRates)
            {
                float dt = 1f / fps, sinceRattle = 0f;
                bool shot = false;

                PadChannel left = default, right = default, aloneLeft = default, aloneRight = default;
                LandingRumble.Apply(ref left, ref right, LandingRumble.FULL_COUNT);
                LandingRumble.Apply(ref aloneLeft, ref aloneRight, LandingRumble.FULL_COUNT);

                for (float clock = 0f; clock < 1.2f; clock += dt)
                {
                    left.Decay(dt);
                    right.Decay(dt);
                    aloneLeft.Decay(dt);
                    aloneRight.Decay(dt);

                    //The rattle from a tenth of a second on, after the tap has gone, and a shot at 0.3 s
                    sinceRattle += dt;
                    if (clock >= RATTLE_EVERY && sinceRattle >= RATTLE_EVERY)
                    {
                        right.Kick(RATTLE, RATTLE_SECONDS);
                        sinceRattle = 0f;
                    }

                    if (!shot && clock >= 0.3f)
                    {
                        left.Kick(HEAVY_KICK, HEAVY_KICK_SECONDS);
                        right.Kick(HEAVY_KICK / 2f, HEAVY_KICK_SECONDS);
                        shot = true;
                    }

                    Assert.True(left.Level >= aloneLeft.Level - EPSILON,
                        $"{fps} fps at {clock:F2} s: the heavy motor at {left.Level} under the release alone's {aloneLeft.Level}");
                    Assert.True(right.Level >= aloneRight.Level - EPSILON,
                        $"{fps} fps at {clock:F2} s: the light motor at {right.Level} under the release alone's {aloneRight.Level}");
                }
            }

            //And the next landing does not cut it short either: a small release 0.3 s into a full one, whose own 0.26 s
            //would be over by 0.56 s, leaves the full one ringing at 0.8 s
            PadChannel tail = default, other = default;
            LandingRumble.Apply(ref tail, ref other, LandingRumble.FULL_COUNT);
            tail.Decay(0.3f);
            LandingRumble.Apply(ref tail, ref other, 3);
            tail.Decay(0.5f);
            Assert.True(tail.Level > 0f, "a small release on a big one's tail cut it short");
        }

        /// <summary>
        /// The ceiling holds for the sum (#810, "count the landing tap and the release together under one cap"): a burst of
        /// landings, big ones on the tails of big ones, through <see cref="LandingRumble.Apply"/> onto the mixer's own
        /// channels and decayed a frame at a time, at four frame rates and four cadences — on their own never past the
        /// ceiling, and with a shot-shaped kick fired between them (nothing gates how soon the next shot may go) never
        /// past the higher of the ceiling and what those same kicks ask for without any release.
        /// </summary>
        [Fact]
        public void ABurstOfReleasesNeverStacksPastTheCeiling()
        {
            int[] counts = { LandingRumble.FULL_COUNT, 3, 120, LandingRumble.FULL_COUNT, 0, 40, MANY, 20 };

            foreach (bool shots in new[] { false, true })
                foreach (float fps in FrameRates)
                    foreach (float cadence in new[] { 0.05f, 0.1f, 0.25f, 0.5f })
                    {
                        PadChannel left = default, right = default, plainLeft = default, plainRight = default;
                        float dt = 1f / fps, untilNext = 0f;
                        int next = 0;

                        for (float clock = 0f; clock < 4f; clock += dt)
                        {
                            untilNext -= dt;
                            if (untilNext <= 0f)
                            {
                                //The shot a little before its own landing: the same ordinary kicks into both pairs, the
                                //releases into one
                                if (shots)
                                {
                                    left.Kick(HEAVY_KICK, HEAVY_KICK_SECONDS);
                                    right.Kick(HEAVY_KICK / 2f, HEAVY_KICK_SECONDS);
                                    plainLeft.Kick(HEAVY_KICK, HEAVY_KICK_SECONDS);
                                    plainRight.Kick(HEAVY_KICK / 2f, HEAVY_KICK_SECONDS);
                                }

                                LandingRumble.Apply(ref left, ref right, counts[next++ % counts.Length]);
                                LandingRumble.Apply(ref plainLeft, ref plainRight, 0);
                                untilNext += cadence;
                            }

                            float topLeft = MathF.Max(LandingRumble.CEILING_LEFT, plainLeft.Level);
                            float topRight = MathF.Max(LandingRumble.CEILING_RIGHT, plainRight.Level);
                            string run = $"{fps} fps, a landing every {cadence} s{(shots ? " with shots" : "")}";

                            Assert.True(left.Level <= topLeft + EPSILON, $"{run}: the heavy motor at {left.Level} at {clock:F2} s");
                            Assert.True(right.Level <= topRight + EPSILON, $"{run}: the light motor at {right.Level} at {clock:F2} s");
                            if (!shots)
                            {
                                Assert.True(left.Level <= LandingRumble.CEILING_LEFT + EPSILON, $"{run}: the heavy motor at {left.Level}");
                                Assert.True(right.Level <= LandingRumble.CEILING_RIGHT + EPSILON, $"{run}: the light motor at {right.Level}");
                            }

                            left.Decay(dt);
                            right.Decay(dt);
                            plainLeft.Decay(dt);
                            plainRight.Decay(dt);
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
                    PadChannel channel = default;
                    channel.Kick(ringing, 1f);
                    channel.Kick(amount, 1f);
                    Assert.Equal(Math.Clamp(ringing + amount, 0f, 1f), channel.Level, 5);
                }
        }

        [Fact]
        public void TheBlendNeverLiftsPastTheCeilingNorBelowEitherLayer()
        {
            const float ceiling = LandingRumble.CEILING_LEFT;

            //The line loss's heavy tail ringing when a release comes in under a lower ceiling: neither raised nor lowered
            Assert.Equal(0.9f, PadChannel.Blend(0.9f, 0.3f, ceiling));

            for (int f = 0; f <= 20; f++)
                for (int b = 0; b <= 20; b++)
                {
                    float free = f / 20f, bounded = b / 20f * ceiling;
                    float level = PadChannel.Blend(free, bounded, ceiling);

                    Assert.True(level <= MathF.Max(free, ceiling) + EPSILON, $"{free} and {bounded}: {level} past the ceiling");
                    Assert.True(level >= MathF.Max(free, bounded) - EPSILON, $"{free} and {bounded}: {level} under one of them");
                    if (free < ceiling && bounded < ceiling)
                        Assert.True(level < ceiling, $"{free} and {bounded}: the ceiling reached by neither alone");
                }
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
            channel.KickUnder(0.5f, 0f, LandingRumble.CEILING_LEFT);
            channel.Decay(0.05f);
            Assert.Equal(0f, channel.Level);

            //And a stop is silence at once, both layers
            channel.Kick(0.4f, 1f);
            channel.KickUnder(0.4f, 1f, LandingRumble.CEILING_LEFT);
            channel.Stop();
            Assert.Equal(0f, channel.Level);
        }
    }
}
