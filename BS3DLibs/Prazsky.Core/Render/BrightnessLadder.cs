using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The rungs of the Game's Brightness row (#711), in <b>percent of the authored look</b>:
    /// <see cref="PostProcessPipeline.DEFAULT_EXPOSURE"/> is 100 % and the player's setting only scales it, the volume
    /// rows' idiom, so retuning the authored figure never changes what a setting means. The row used to show the raw
    /// multiplier (0.7, 0.9, 1.1, 1.3, 1.5), which asks a player to know that exposure is a multiplier, which value is
    /// normal (it is not 1.0, which is a slightly <i>darker</i> picture than the game was made to show) and which way
    /// is brighter.
    /// <para>
    /// Seven rungs with the default in the middle, wide enough either way to matter on a dim laptop panel and on a
    /// bright monitor without crushing or blowing the frame (the old ladder ran 64 to 136 %, this 70 to 130). A row
    /// is one click and has no back-step, so the walk goes <b>up and wraps</b>: from the default the first click is a
    /// small brighter step, and the dim side comes after the wrap. Pure arithmetic, so the tests can state what the
    /// row promises without a window.
    /// </para>
    /// </summary>
    public static class BrightnessLadder
    {
        private static readonly int[] RUNGS = [70, 80, 90, 100, 110, 120, 130];

        /// <summary>The percent of the authored look the default is: the middle rung.</summary>
        public const int DEFAULT_PERCENT = 100;

        /// <summary>The rungs, lowest first.</summary>
        public static ReadOnlySpan<int> Rungs => RUNGS;

        /// <summary>The exposure a rung of <paramref name="percent"/> applies: the authored look scaled by it.</summary>
        public static float ExposureOf(int percent) => PostProcessPipeline.DEFAULT_EXPOSURE * percent / 100f;

        /// <summary>
        /// A raw exposure as the whole percent of the authored look the row shows - on or off the ladder, since a
        /// command-line <c>exposure=1.0</c> (the capture contract: a raw multiplier) is 91 %.
        /// </summary>
        public static int PercentOf(float exposure) => (int)MathF.Round(exposure / PostProcessPipeline.DEFAULT_EXPOSURE * 100f);

        /// <summary>
        /// The rung one click up from <paramref name="percent"/>: <b>the first rung above it</b>, wrapping from the top
        /// to the bottom. Not the rung after an index, so a value that is not on the ladder walks onto it - 91 goes
        /// to 100 - and the row is back on its ladder after one click.
        /// </summary>
        public static int NextAbove(int percent)
        {
            foreach (int rung in RUNGS)
            {
                if (rung > percent) return rung;
            }

            return RUNGS[0];
        }

        /// <summary>
        /// The exposure of the rung nearest <paramref name="stored"/>: what the settings file holds is a raw
        /// multiplier, written by the ladder the row had before (0.7, 0.9, 1.1, 1.3, 1.5 - 64, 82, 100, 118 and 136 %)
        /// or by hand, and it is read onto this ladder once so nobody's dial resets (the old 1.3 opens on 120 %, the
        /// old 0.9 on 80). Nearest <b>by difference in percent</b>, since this ladder's steps are an even ten points
        /// (the sensitivity ladder's are ratios, which is why that one is nearest by ratio). Anything unusable
        /// (zero, negative, NaN out of a corrupted file) comes back as the authored look.
        /// </summary>
        public static float NearestExposure(float stored)
        {
            if (!(stored > 0f)) return PostProcessPipeline.DEFAULT_EXPOSURE;   //written against NaN, which fails every ordinary comparison

            float percent = stored / PostProcessPipeline.DEFAULT_EXPOSURE * 100f;
            int best = DEFAULT_PERCENT;
            float bestDistance = float.MaxValue;

            foreach (int rung in RUNGS)
            {
                float distance = MathF.Abs(percent - rung);

                if (distance < bestDistance) { bestDistance = distance; best = rung; }
            }

            return ExposureOf(best);
        }
    }
}
