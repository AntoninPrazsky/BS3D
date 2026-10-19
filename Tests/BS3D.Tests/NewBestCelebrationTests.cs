using BS3D.Screens;
using Microsoft.Xna.Framework;
using System;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// #926: the result page's new best is celebrated at the loud end, and what that means is held as numbers the tests can read: the word
    /// strikes with a bounce that overshoots well past its size and comes to rest at it, its fill is a pastel rainbow that flows slowly
    /// and is never dark or loud, and its sparkles burst and then glint for as long as the page is up. Whether it is a good celebration is
    /// the owner's eye (and he has said which way to err); what a test can refuse is a bounce that does not bounce, a fill that is not
    /// pastel, and sparkles that are not there.
    /// </summary>
    public class NewBestCelebrationTests
    {
        private readonly ITestOutputHelper _output;

        public NewBestCelebrationTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void It_is_not_there_before_it_strikes_and_starts_small()
        {
            Assert.Equal(0f, NewBestCelebration.Scale(-0.1f));
            Assert.Equal(0f, NewBestCelebration.Alpha(-0.1f));
            Assert.Equal(0f, NewBestCelebration.Alpha(0f));
            Assert.Equal(NewBestCelebration.BIRTH_FROM, NewBestCelebration.Scale(0f), 3);
            Assert.Equal(1f, NewBestCelebration.Alpha(NewBestCelebration.FADE_IN));
        }

        /// <summary>
        /// The bounce: it grows past its size by well over a fifth, comes back under it, and rests at it - and the first peak is quick, so
        /// the word is struck and not eased in.
        /// </summary>
        [Fact]
        public void It_overshoots_and_rings_and_comes_to_rest_at_its_size()
        {
            float peak = 0f, peakAt = 0f, trough = float.MaxValue;
            bool pastPeak = false;

            for (float age = 0f; age <= 3f; age += 1f / 240f)
            {
                float scale = NewBestCelebration.Scale(age);
                Assert.True(float.IsFinite(scale) && scale > 0f, $"scale {scale} at {age:F3} s");

                if (scale > peak)
                {
                    peak = scale;
                    peakAt = age;
                }
                else if (!pastPeak && scale < peak - 0.05f) pastPeak = true;

                if (pastPeak) trough = MathF.Min(trough, scale);
            }

            _output.WriteLine($"first peak {peak:F2}x at {peakAt:F2} s, then down to {trough:F2}x");

            Assert.InRange(peak, 1.25f, 1.6f);
            Assert.InRange(peakAt, 0.1f, 0.35f);
            Assert.True(trough < 1f, "it never comes back under its size, so it does not bounce");
            Assert.InRange(NewBestCelebration.Scale(1.5f), 0.99f, 1.01f);
            Assert.InRange(NewBestCelebration.Scale(30f), 0.999f, 1.001f);
        }

        [Fact]
        public void The_bounce_has_no_jump_in_it()
        {
            float previous = NewBestCelebration.Scale(0f);

            //A 240 Hz step: nothing moves further than a tenth of the word's size between two frames
            for (float age = 1f / 240f; age <= 2f; age += 1f / 240f)
            {
                float scale = NewBestCelebration.Scale(age);
                Assert.True(MathF.Abs(scale - previous) < 0.1f, $"jumps {previous:F2} to {scale:F2} at {age:F3} s");
                previous = scale;
            }
        }

        /// <summary>It strikes after the last earned star has begun to seat, and before that star is fully done with its own travel plus a beat.</summary>
        [Fact]
        public void It_strikes_just_after_the_last_star_seats()
        {
            float arrival = NewBestCelebration.Arrival(1.65f, 0.34f);

            Assert.True(arrival > 1.65f + 0.34f * 0.5f, $"strikes at {arrival:F2} s, before the star has seated");
            Assert.True(arrival < 1.65f + 0.34f + 0.25f, $"strikes at {arrival:F2} s, a long wait after the star");
        }

        /// <summary>
        /// Pastel by construction, held across the whole word, its height and time: no channel darker than the lift and none over one, and when the
        /// gloss is not on it the three channels always add to the same lightness, so the hue goes round without the fill swinging dark and light.
        /// </summary>
        [Fact]
        public void The_fill_is_pastel_everywhere_and_always()
        {
            float lightness = 3f * NewBestCelebration.LIFT + (1f - NewBestCelebration.LIFT) * 1.5f;
            float least = float.MaxValue, most = float.MinValue;

            for (float clock = 0f; clock < 40f; clock += 0.37f)
                for (float across = -0.2f; across <= 1.2f; across += 0.05f)
                    for (float down = 0f; down <= 1f; down += 0.5f)
                    {
                        Vector3 colour = NewBestCelebration.Fill(across, down, clock);

                        Assert.InRange(colour.X, NewBestCelebration.LIFT - 1e-3f, 1f + 1e-3f);
                        Assert.InRange(colour.Y, NewBestCelebration.LIFT - 1e-3f, 1f + 1e-3f);
                        Assert.InRange(colour.Z, NewBestCelebration.LIFT - 1e-3f, 1f + 1e-3f);

                        least = MathF.Min(least, MathF.Min(colour.X, MathF.Min(colour.Y, colour.Z)));
                        most = MathF.Max(most, MathF.Max(colour.X, MathF.Max(colour.Y, colour.Z)));
                    }

            //Outside a sweep of the gloss (the period's last two thirds) the three channels sum to the constant
            float quiet = NewBestCelebration.GLOSS_SWEEP + 0.4f;
            for (float across = 0f; across <= 1f; across += 0.1f)
            {
                Vector3 colour = NewBestCelebration.Fill(across, 0.5f, quiet);
                Assert.Equal(lightness, colour.X + colour.Y + colour.Z, 2);
            }

            _output.WriteLine($"channels run from {least:F2} to {most:F2}");
            Assert.InRange(least, NewBestCelebration.LIFT - 1e-3f, NewBestCelebration.LIFT + 0.02f);
        }

        /// <summary>It is a rainbow and not one pastel: across the word the strongest channel goes round red, green and blue.</summary>
        [Fact]
        public void The_fill_runs_through_the_whole_rainbow_across_the_word()
        {
            bool red = false, green = false, blue = false;
            float quiet = NewBestCelebration.GLOSS_SWEEP + 0.4f;

            for (float across = 0f; across <= 1f; across += 0.02f)
            {
                Vector3 colour = NewBestCelebration.Fill(across, 0.5f, quiet);
                if (colour.X > colour.Y + 0.15f && colour.X > colour.Z + 0.15f) red = true;
                if (colour.Y > colour.X + 0.15f && colour.Y > colour.Z + 0.15f) green = true;
                if (colour.Z > colour.X + 0.15f && colour.Z > colour.Y + 0.15f) blue = true;
            }

            Assert.True(red && green && blue, $"the word's fill misses a primary: red {red}, green {green}, blue {blue}");
        }

        /// <summary>It flows slowly: a letter takes ten seconds to go once round the rainbow, so over a second the fill has moved a tenth of a turn.</summary>
        [Fact]
        public void The_rainbow_flows_slowly()
        {
            //Two moments out of the gloss's sweep, a second apart
            float t0 = NewBestCelebration.GLOSS_SWEEP + 0.3f;
            Vector3 now = NewBestCelebration.Fill(0.4f, 0.5f, t0);
            Vector3 later = NewBestCelebration.Fill(0.4f, 0.5f, t0 + 1f);

            Vector3 expected = NewBestCelebration.Pastel(0.4f * NewBestCelebration.SPAN + 0.5f * NewBestCelebration.TILT
                - (t0 + 1f) * NewBestCelebration.FLOW_CYCLES_PER_SECOND);

            Assert.True((later - expected).Length() < 1e-3f);
            Assert.True((later - now).Length() > 0.02f, "the rainbow does not move");
            Assert.True((later - now).Length() < 0.5f, "the rainbow moves too fast to be slow");
            Assert.InRange(NewBestCelebration.FLOW_CYCLES_PER_SECOND, 0.05f, 0.2f);
        }

        /// <summary>The gloss sweeps across the word, taking it a good way to white, and then leaves it alone until its period comes round.</summary>
        [Fact]
        public void The_gloss_crosses_the_word_and_then_rests()
        {
            float whitest = 0f;
            for (float t = 0f; t <= NewBestCelebration.GLOSS_SWEEP; t += 0.02f)
                for (float across = 0f; across <= 1f; across += 0.02f)
                {
                    Vector3 colour = NewBestCelebration.Fill(across, 0.5f, t);
                    whitest = MathF.Max(whitest, MathF.Min(colour.X, MathF.Min(colour.Y, colour.Z)));
                }

            _output.WriteLine($"in a sweep the palest channel reaches {whitest:F2}");
            Assert.True(whitest > 0.85f, "the gloss never gets near white");

            //Out of its sweep the fill is the bare rainbow, to the last digit
            float rest = NewBestCelebration.GLOSS_SWEEP + 0.5f;
            for (float across = 0f; across <= 1f; across += 0.05f)
            {
                Vector3 bare = NewBestCelebration.Pastel(across * NewBestCelebration.SPAN + 0.5f * NewBestCelebration.TILT
                    - rest * NewBestCelebration.FLOW_CYCLES_PER_SECOND);
                Assert.True((NewBestCelebration.Fill(across, 0.5f, rest) - bare).Length() < 1e-4f);
            }
        }

        private const float ASPECT = 3.6f;

        [Fact]
        public void The_burst_is_there_after_it_strikes_and_gone_before_the_glints_need_the_room()
        {
            int mostAlive = 0;
            for (float age = 0f; age <= NewBestCelebration.BURST_END + 0.5f; age += 0.02f)
            {
                int alive = 0;
                for (int i = 0; i < NewBestCelebration.BURST_COUNT; i++)
                {
                    NewBestCelebration.Sparkle sparkle = NewBestCelebration.Burst(i, age, ASPECT);

                    Assert.True(float.IsFinite(sparkle.X) && float.IsFinite(sparkle.Y) && float.IsFinite(sparkle.Spin));
                    Assert.InRange(sparkle.Strength, 0f, 1f);
                    Assert.InRange(sparkle.Size, 0f, 1.6f);

                    if (sparkle.Strength > 0.01f) alive++;
                    if (age >= NewBestCelebration.BURST_END) Assert.Equal(0f, sparkle.Strength);
                }

                mostAlive = Math.Max(mostAlive, alive);
            }

            _output.WriteLine($"{mostAlive} of {NewBestCelebration.BURST_COUNT} burst sparkles at once, at the most");
            Assert.True(mostAlive >= NewBestCelebration.BURST_COUNT / 2, $"only {mostAlive} sparkles are ever there at once");
        }

        /// <summary>It is a burst: the sparkles are thrown outward from the word's middle, so a sparkle ends further from it than it began.</summary>
        [Fact]
        public void The_burst_flies_outward()
        {
            int outward = 0, seen = 0;

            for (int i = 0; i < NewBestCelebration.BURST_COUNT; i++)
            {
                NewBestCelebration.Sparkle first = default, last = default;
                bool any = false;

                for (float age = 0f; age <= NewBestCelebration.BURST_END; age += 0.01f)
                {
                    NewBestCelebration.Sparkle sparkle = NewBestCelebration.Burst(i, age, ASPECT);
                    if (sparkle.Strength <= 0.01f) continue;

                    if (!any) first = sparkle;
                    last = sparkle;
                    any = true;
                }

                if (!any) continue;

                seen++;
                if (Math.Abs(last.X) > Math.Abs(first.X) || Math.Abs(last.Y) > Math.Abs(first.Y)) outward++;
            }

            _output.WriteLine($"{outward} of {seen} sparkles end further out");
            Assert.Equal(NewBestCelebration.BURST_COUNT, seen);
            Assert.True(outward >= seen * 0.9f, $"only {outward} of {seen} sparkles fly outward");
        }

        /// <summary>The burst is a function of its index and the clock: the same call gives the same sparkle, and different ones differ.</summary>
        [Fact]
        public void A_sparkle_is_a_function_of_its_index_and_the_clock()
        {
            Assert.Equal(NewBestCelebration.Burst(3, 0.5f, ASPECT), NewBestCelebration.Burst(3, 0.5f, ASPECT));
            Assert.Equal(NewBestCelebration.Glint(2, 4.2f, ASPECT), NewBestCelebration.Glint(2, 4.2f, ASPECT));
            Assert.NotEqual(NewBestCelebration.Burst(3, 0.5f, ASPECT), NewBestCelebration.Burst(4, 0.5f, ASPECT));
        }

        /// <summary>After the burst the word keeps glinting for as long as the page is up: some glint is on at nearly every moment, and a few at once on average.</summary>
        [Fact]
        public void Glints_keep_twinkling_on_the_word_after_the_burst()
        {
            Assert.Equal(0f, NewBestCelebration.Glint(0, NewBestCelebration.GLINT_FROM - 0.1f, ASPECT).Strength);

            int moments = 0, empty = 0, total = 0;
            for (float age = 1.5f; age <= 60f; age += 0.05f)
            {
                int alive = 0;
                for (int i = 0; i < NewBestCelebration.GLINT_COUNT; i++)
                {
                    NewBestCelebration.Sparkle glint = NewBestCelebration.Glint(i, age, ASPECT);
                    Assert.InRange(glint.Strength, 0f, 1f);
                    if (glint.Strength > 0.01f)
                    {
                        alive++;
                        Assert.InRange(MathF.Abs(glint.X), 0f, ASPECT * 0.5f);
                        Assert.InRange(MathF.Abs(glint.Y), 0f, 0.5f);
                    }
                }

                moments++;
                total += alive;
                if (alive == 0) empty++;
            }

            float average = total / (float)moments;
            _output.WriteLine($"{average:F1} glints at a time on average, none at {empty} of {moments} moments");
            Assert.True(average >= 1.5f, $"only {average:F1} glints at a time");
            Assert.True(empty < moments * 0.1f, $"the word is bare at {empty} of {moments} moments");
        }

        [Fact]
        public void The_hash_is_in_the_unit_interval_and_spread()
        {
            float sum = 0f;
            for (uint seed = 0; seed < 4000; seed++)
            {
                float unit = NewBestCelebration.Unit(seed);
                Assert.InRange(unit, 0f, 0.99999994f);
                sum += unit;
            }

            Assert.InRange(sum / 4000f, 0.45f, 0.55f);
        }
    }
}
