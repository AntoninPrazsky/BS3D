using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The burst an intro cues (#975, <see cref="VolcanoLava.CueBurst"/>): it goes off at the stated second at its full
    /// size, the event the sound reads is that burst, the natural bursts that would crowd it give way, a burst already
    /// going is never cut off, and once the cue's quiet is over the natural schedule is back exactly as it was.
    /// </summary>
    public class VolcanoBurstCueTests
    {
        private const float STEP = 0.05f;

        private static VolcanoLava Lava() => new(new VolcanoSceneConfig(), 0);

        //The next second at or after `from` at which a natural burst starts, off the lava's own event
        private static float NextNaturalOnset(VolcanoLava lava, float from)
        {
            for (float t = from; ; t += STEP)
                if (lava.TryGetBurst(t, out _, out float onset, out _) && onset >= from) return onset;
        }

        [Fact]
        public void A_cued_burst_goes_off_at_its_second_at_full_size()
        {
            VolcanoLava lava = Lava();
            float length = new VolcanoSceneConfig().Eruption.Length;

            //Cued just after a natural burst has run out, so no natural one can stand under this one either way
            float now = NextNaturalOnset(lava, 500f) + length + 0.5f;
            float onset = now + 1f;
            lava.CueBurst(now, onset);

            Assert.Equal(0f, lava.Eruption(onset - 0.01f), 3);
            Assert.Equal(1f, lava.Eruption(onset + 0.14f * length), 3);

            Assert.True(lava.TryGetBurst(onset + 0.01f, out int index, out float staged, out float size));
            Assert.True(index < 0, $"the cue's event index {index} collides with a period's");
            Assert.Equal(onset, staged);
            Assert.Equal(1f, size);
        }

        [Fact]
        public void The_natural_bursts_round_a_cue_give_way()
        {
            VolcanoLava lava = Lava();
            VolcanoConfigFigures(out float length, out float quiet);

            //A moment at which a natural burst is about to start, three seconds before the cue: without the cue, it
            //would spend itself in the shot before the eruption
            float natural = NextNaturalOnset(lava, 300f);
            float now = natural - 1f;
            float onset = natural + 3f;
            lava.CueBurst(now, onset);

            for (float t = now; t < onset; t += STEP)
            {
                Assert.Equal(0f, lava.Eruption(t));
                Assert.True(!lava.TryGetBurst(t, out int index, out _, out _) || index < 0, $"a natural burst answers at {t:F2}");
            }

            //And none answers the cue a few seconds later
            for (float t = onset + length; t < onset + quiet; t += STEP)
                Assert.Equal(0f, lava.Eruption(t));
        }

        [Fact]
        public void A_burst_already_going_is_not_cut_off()
        {
            VolcanoLava lava = Lava();
            VolcanoLava untouched = Lava();

            float natural = NextNaturalOnset(lava, 700f);
            lava.CueBurst(natural + 0.5f, natural + 5f);

            for (float t = natural; t < natural + 4f; t += STEP)
                Assert.True(lava.Eruption(t) >= untouched.Eruption(t), $"the running burst was cut at {t:F2}");
        }

        [Fact]
        public void After_its_quiet_the_schedule_is_as_it_was()
        {
            VolcanoLava lava = Lava();
            VolcanoLava untouched = Lava();
            VolcanoConfigFigures(out float length, out float quiet);

            const float now = 900f, onset = 904f;
            lava.CueBurst(now, onset);

            //From the first natural burst to start after the quiet, every figure matches the uncued lava's
            float back = NextNaturalOnset(untouched, onset + quiet);
            for (float t = back; t < back + 120f; t += STEP)
            {
                Assert.Equal(untouched.Eruption(t), lava.Eruption(t));
                Assert.Equal(untouched.TryGetBurst(t, out int expected, out _, out _), lava.TryGetBurst(t, out int actual, out _, out _));
                Assert.Equal(expected, actual);
            }
        }

        private static void VolcanoConfigFigures(out float length, out float quiet)
        {
            EruptionConfig eruption = new VolcanoSceneConfig().Eruption;
            length = eruption.Length;

            //The schedule's shortest gap between two natural onsets (VolcanoLava.START_SPAN)
            quiet = (1f - 0.55f) * eruption.Period;
        }
    }
}
