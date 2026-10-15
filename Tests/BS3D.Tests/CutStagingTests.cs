using BS3D.Effects;
using Prazsky.BS3D;
using Prazsky.Core.Render;
using System;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// #767: the Cut's beats are pure functions of how long ago each began (<see cref="CutBeats"/>), and what it asks of the
    /// pad is the mixer's own arithmetic (<see cref="CutRumble"/> over <see cref="PadChannel"/>). These tests hold the rules
    /// the brief binds them to: the same at 30, 60, 75 and 144 frames a second, a saw that never spins fast enough to alias
    /// (#766's lesson), a slow motion that always gives the world back, and a hand that is never asked for more than a shot
    /// or the landing's ceiling asks (#810: "it shakes my joints"), with the line loss still the heaviest thing the pad says.
    /// </summary>
    public class CutStagingTests
    {
        private static readonly float[] FrameRates = { 30f, 60f, 75f, 144f };

        //The shot's thump and the line loss's heavy pulse, GameplayScreen's SHOT_RUMBLE_* and LINE_LOSS_RUMBLE_LEFT: the shot
        //is what rings when its cutter strikes, the line loss the heaviest pulse the pad may say
        private const float SHOT_LEFT = 0.55f, SHOT_RIGHT = 0.25f, SHOT_SECONDS = 0.12f;
        private const float LINE_LOSS_LEFT = 0.9f;

        private const float EPSILON = 1e-4f;

        private readonly ITestOutputHelper _output;

        public CutStagingTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void The_charge_gathers_to_its_top_and_settles_to_nothing()
        {
            Assert.Equal(0f, CutBeats.Charge(-0.01f));
            Assert.Equal(0f, CutBeats.Charge(0f));
            Assert.Equal(1f, CutBeats.Charge(CutBeats.CHARGE_SECONDS), 4);
            Assert.Equal(0f, CutBeats.Charge(CutBeats.PRESS_SECONDS));

            //Rising to the top, falling after it, and never a step between two instants a millisecond apart
            float previous = 0f;
            for (float t = 0f; t <= CutBeats.PRESS_SECONDS + 0.05f; t += 0.001f)
            {
                float charge = CutBeats.Charge(t);
                Assert.InRange(charge, 0f, 1f);
                Assert.True(MathF.Abs(charge - previous) < 0.02f, $"the charge jumps at {t:F3} s: {previous:F3} to {charge:F3}");
                if (t < CutBeats.CHARGE_SECONDS) Assert.True(charge >= previous - EPSILON, $"the charge falls before its top, at {t:F3} s");
                else Assert.True(charge <= previous + EPSILON, $"the charge rises after its top, at {t:F3} s");
                previous = charge;
            }
        }

        /// <summary>
        /// The saw's angle is the closed-form integral of its speed: summed frame by frame at any rate it comes to the same
        /// angle (to the rounding a frame's step leaves), so the blade is where it is whatever the refresh rate, and it never
        /// jumps - the one thing changing the speed inside <c>clock × speed</c> would do.
        /// </summary>
        [Fact]
        public void The_saw_turns_by_its_speed_and_never_jumps()
        {
            foreach (float hz in FrameRates)
            {
                float dt = 1f / hz, summed = 0f, t = 0f;
                float previousAngle = 0f;

                while (t < CutBeats.PRESS_SECONDS + 0.5f)
                {
                    //The midpoint's speed over the frame, which is what the angle's difference across it has to be
                    summed += CutBeats.SpinRate(t + 0.5f * dt) * dt;
                    t += dt;

                    float angle = CutBeats.SpinAngle(t);
                    Assert.True(MathF.Abs(angle - summed) < 0.01f, $"{hz} Hz, {t:F3} s: the angle {angle:F4} is not the speed summed, {summed:F4}");

                    //A frame's turn is the speed's, never more: no leap at the top or at the hand-back to the idle turn
                    Assert.True(angle - previousAngle <= CutBeats.SPIN_PEAK * dt + EPSILON, $"{hz} Hz, {t:F3} s: the saw leapt");
                    previousAngle = angle;
                }
            }

            //And once the press is over the round is handed back to its idle turn with a constant offset
            float settled = CutBeats.SpinOffset(CutBeats.PRESS_SECONDS);
            Assert.Equal(settled, CutBeats.SpinOffset(CutBeats.PRESS_SECONDS + 3f), 4);
            Assert.Equal(CutBeats.SPIN_LOADED, CutBeats.SpinRate(CutBeats.PRESS_SECONDS + 1f));
        }

        /// <summary>
        /// #766's measured bound: a frame may not turn the saw by half a tooth's pitch or more, or the teeth alias (they seemed
        /// to run backwards at 75 Hz and creep at 60). The charge's top speed keeps under it down to 30 frames a second.
        /// </summary>
        [Fact]
        public void The_charge_never_spins_the_saw_fast_enough_to_alias()
        {
            float pitch = MathHelper2.TwoPi / SawBladeMesh.TEETH;
            float fastest = 0f;
            for (float t = 0f; t < CutBeats.PRESS_SECONDS + 0.1f; t += 0.001f) fastest = MathF.Max(fastest, CutBeats.SpinRate(t));

            float perFrame = fastest / 30f / pitch;
            _output.WriteLine($"fastest {fastest:F2} rad/s: {perFrame:F2} of a tooth's pitch a frame at 30 Hz");

            Assert.True(fastest <= BallRenderSet.CUTTER_SPIN_FLYING + EPSILON);
            Assert.True(perFrame < 0.5f, $"the charge turns the saw {perFrame:F2} of a tooth a frame at 30 Hz");
        }

        /// <summary>
        /// The slow motion always gives the world back: a hit-stop, then for a cut that freed something a slower fall the
        /// bigger the result, then 1 again - and the world time it costs is the same at every frame rate, to a frame's worth.
        /// </summary>
        [Fact]
        public void The_slow_motion_gives_the_world_back_and_grows_with_the_result()
        {
            Assert.Equal(1f, CutBeats.TimeScale(-0.1f, 1f, true));
            Assert.Equal(CutBeats.HIT_STOP_SCALE, CutBeats.TimeScale(0f, 0f, false));

            foreach ((float size, bool freed) in new[] { (0f, false), (0f, true), (0.4f, true), (1f, true) })
            {
                float end = CutBeats.SlowSeconds(size, freed);
                Assert.Equal(1f, CutBeats.TimeScale(end, size, freed), 4);
                Assert.Equal(1f, CutBeats.TimeScale(end + 1f, size, freed));

                for (float t = 0f; t < end; t += 0.001f)
                    Assert.InRange(CutBeats.TimeScale(t, size, freed), CutBeats.HIT_STOP_SCALE - EPSILON, 1f);

                double[] lost = new double[FrameRates.Length];
                for (int r = 0; r < FrameRates.Length; r++)
                {
                    float dt = 1f / FrameRates[r];
                    double world = 0.0;
                    float wall = 0f;
                    for (float age = 0f; age < end + 0.2f; age += dt)
                    {
                        world += CutBeats.TimeScale(age, size, freed) * dt;
                        wall += dt;
                    }
                    lost[r] = wall - world;
                }

                _output.WriteLine($"size {size:F1}{(freed ? "" : ", freed nothing")}: {end:F2} s, the world loses "
                    + string.Join(", ", Array.ConvertAll(lost, l => l.ToString("F3"))) + " s at 30/60/75/144 Hz");

                for (int r = 1; r < FrameRates.Length; r++)
                    Assert.True(Math.Abs(lost[r] - lost[0]) < 1.0 / 30.0, $"size {size}: the world loses a different time at {FrameRates[r]} Hz");
            }

            //Bigger is slower and longer
            Assert.True(CutBeats.SlowSeconds(1f, true) > CutBeats.SlowSeconds(0f, true));
            Assert.True(CutBeats.SlowSeconds(0f, true) > CutBeats.SlowSeconds(0f, false));
            Assert.True(CutBeats.TimeScale(0.3f, 1f, true) < CutBeats.TimeScale(0.3f, 0f, true));
        }

        [Fact]
        public void The_cut_faces_cool_from_white_heat_to_nothing()
        {
            Assert.Equal(0f, CutBeats.Heat(-0.01f));
            Assert.Equal(1f, CutBeats.Heat(0f));
            Assert.Equal(0f, CutBeats.Heat(CutBeats.FALL_GLOW_SECONDS));

            float previous = 1f;
            for (float t = 0f; t < CutBeats.FALL_GLOW_SECONDS + 0.1f; t += 0.002f)
            {
                float heat = CutBeats.Heat(t);
                Assert.True(heat <= previous + EPSILON, $"the heat rises at {t:F3} s");
                Assert.True(previous - heat < 0.02f, $"the heat drops by a step at {t:F3} s");
                previous = heat;
            }

            //The storey the blade went through at full, a storey down at under half
            Assert.Equal(1f, CutBeats.HeatAtDepth(0f));
            Assert.InRange(CutBeats.HeatAtDepth(1f / MathF.Sqrt(2f)), 0.3f, 0.6f);
        }

        [Fact]
        public void The_lens_pinches_and_lets_go()
        {
            Assert.Equal(1f, CutBeats.FovScale(-1f));
            Assert.Equal(1f - CutBeats.PINCH, CutBeats.FovScale(CutBeats.PINCH_IN_SECONDS), 4);
            Assert.Equal(1f, CutBeats.FovScale(CutBeats.PINCH_IN_SECONDS + CutBeats.PINCH_OUT_SECONDS));

            for (float t = 0f; t < 0.5f; t += 0.001f) Assert.InRange(CutBeats.FovScale(t), 1f - CutBeats.PINCH - EPSILON, 1f);
        }

        /// <summary>The press's hold rises with the charge to its top, falls away quickly after it, and stays light.</summary>
        [Fact]
        public void The_press_is_a_low_rising_pulse()
        {
            var top = CutRumble.Charge(CutBeats.CHARGE_SECONDS - 1e-4f);
            Assert.Equal(CutRumble.CHARGE_LEFT, top.Left, 3);
            Assert.Equal(CutRumble.CHARGE_TRIGGER, top.LeftTrigger, 3);

            float previous = -1f;
            for (float t = 0f; t < CutBeats.CHARGE_SECONDS; t += 0.005f)
            {
                var hold = CutRumble.Charge(t);
                Assert.True(hold.LeftTrigger >= previous - EPSILON, $"the hold falls at {t:F3} s while it charges");
                Assert.True(hold.Left <= LandingRumble.CEILING_LEFT && hold.Right <= LandingRumble.CEILING_RIGHT);
                previous = hold.LeftTrigger;
            }

            Assert.Equal((0f, 0f, 0f, 0f), CutRumble.Charge(-0.01f));
            Assert.Equal((0f, 0f, 0f, 0f), CutRumble.Charge(CutBeats.CHARGE_SECONDS + 0.2f));
        }

        /// <summary>
        /// The strike, through the mixer as the pad hears it: whatever the cut freed and however short the flight that left the
        /// shot's thump still ringing under it, at every frame rate, the body motors are never asked for more than the shot
        /// itself or the landing's ceiling asks (#810) - and so never near the line loss.
        /// </summary>
        [Fact]
        public void The_strike_never_asks_more_than_a_shot_or_the_ceiling()
        {
            float boundLeft = MathF.Max(SHOT_LEFT, LandingRumble.CEILING_LEFT);
            float boundRight = MathF.Max(SHOT_RIGHT, LandingRumble.CEILING_RIGHT);
            float peakLeft = 0f, peakRight = 0f;

            foreach (float hz in FrameRates)
                foreach (float flight in new[] { 0f, 0.01f, 0.03f, 0.08f, 0.2f })
                    foreach (int freed in new[] { 0, 1, 5, 20, 80, 250, 2000 })
                    {
                        (float left, float right) = Peak(hz, flight, freed, out _);
                        peakLeft = MathF.Max(peakLeft, left);
                        peakRight = MathF.Max(peakRight, right);

                        Assert.True(left <= boundLeft + EPSILON, $"{hz} Hz, flight {flight} s, {freed} freed: heavy motor {left:F3}");
                        Assert.True(right <= boundRight + EPSILON, $"{hz} Hz, flight {flight} s, {freed} freed: light motor {right:F3}");
                    }

            _output.WriteLine($"the strike's highest ask over every case: heavy {peakLeft:F3}, light {peakRight:F3} (line loss heavy {LINE_LOSS_LEFT})");
            Assert.True(peakLeft < LINE_LOSS_LEFT);
        }

        /// <summary>
        /// The planted fault (BestPractices.md §10): the knock ADDED onto the shot's thump, as an ordinary kick would be, goes
        /// past the bound the test above holds on a short flight. So that test's passing says something.
        /// </summary>
        [Fact]
        public void A_knock_added_on_the_shot_would_break_the_bound()
        {
            PadChannel left = default;
            left.Kick(SHOT_LEFT, SHOT_SECONDS);
            left.Decay(0.01f);
            left.Kick(CutRumble.KNOCK_LEFT, CutRumble.KNOCK_SECONDS);

            Assert.True(left.Level > MathF.Max(SHOT_LEFT, LandingRumble.CEILING_LEFT) + EPSILON,
                $"a summed knock stayed under the bound ({left.Level:F3})");
        }

        /// <summary>
        /// The fall grows with what the cut freed, along the release's own curve: more balls ring longer and never weaker, a cut
        /// that freed nothing is the knock alone and silent once the knock has fallen.
        /// </summary>
        [Fact]
        public void The_fall_grows_with_what_was_freed()
        {
            float previousLength = 0f;
            foreach (int freed in new[] { 0, 3, 10, 40, 150, 250 })
            {
                Peak(60f, 0.2f, freed, out float length);
                _output.WriteLine($"{freed} freed: the pad rings {length:F2} s after the strike");
                Assert.True(length >= previousLength - 1f / 60f, $"{freed} freed rings shorter than fewer did");
                previousLength = length;
            }

            Peak(60f, 0.2f, 0, out float knockOnly);
            Assert.InRange(knockOnly, 0f, CutRumble.KNOCK_SECONDS + 2f / 60f);
        }

        //The pad's heavy and light motors through a shot, a flight of the given length and a cut's strike that freed that many,
        //stepped at the given rate as GamepadRumble steps them: their highest asks from the strike on, and how long after the
        //strike they ring
        private static (float Left, float Right) Peak(float hz, float flight, int freed, out float ringsFor)
        {
            float dt = 1f / hz;
            PadChannel left = default, right = default;

            left.Kick(SHOT_LEFT, SHOT_SECONDS);
            right.Kick(SHOT_RIGHT, SHOT_SECONDS);

            for (float t = 0f; t + dt <= flight; t += dt)
            {
                left.Decay(dt);
                right.Decay(dt);
            }

            CutRumble.Strike(ref left, ref right, freed);

            float peakLeft = left.Level, peakRight = right.Level;
            ringsFor = 0f;

            for (float t = 0f; t < 3f; t += dt)
            {
                left.Decay(dt);
                right.Decay(dt);
                peakLeft = MathF.Max(peakLeft, left.Level);
                peakRight = MathF.Max(peakRight, right.Level);
                if (left.Level > 0f || right.Level > 0f) ringsFor = t + dt;
            }

            return (peakLeft, peakRight);
        }

        private static class MathHelper2
        {
            public const float TwoPi = 2f * MathF.PI;
        }
    }
}
