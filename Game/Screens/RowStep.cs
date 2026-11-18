using System;

namespace BS3D.Screens
{
    /// <summary>
    /// The − and + ends of a cycling Settings row (#1151): one rung less or one rung more of what the row sets. A press on
    /// the row itself walks its ladder one way and wraps round (a volume goes 100, 75, 50, 25, Off and back to 100), so on
    /// a touch screen, where there is no other way to move it, back to 100 % was four presses; the ends step either way
    /// and are <b>held at the ladder's ends</b>, where the press wraps, so a + never lands on Off. Pure arithmetic, so the
    /// tests can state what the ends promise without a window.
    /// </summary>
    internal static class RowStep
    {
        /// <summary>
        /// The rung one step from <paramref name="value"/> on <paramref name="rungs"/>, in whatever order they stand (the
        /// brightness lowest first, the Resolution row's native first): the nearest rung above it for a positive
        /// <paramref name="direction"/>, the nearest below it for a negative one, and <paramref name="value"/> itself past
        /// the ladder's end. A value between two rungs (a command line's <c>exposure=1.0</c>, 91 %) steps onto the ladder,
        /// as the row's own press does.
        /// </summary>
        internal static int OnRungs(ReadOnlySpan<int> rungs, int value, int direction)
        {
            int best = value;
            foreach (int rung in rungs)
            {
                bool way = direction > 0 ? rung > value : direction < 0 && rung < value;
                if (way && (best == value || Math.Abs(rung - value) < Math.Abs(best - value))) best = rung;
            }

            return best;
        }

        /// <summary>
        /// <see cref="OnRungs(ReadOnlySpan{int}, int, int)"/> for a ladder of ratios (the sensitivities): a rung within
        /// <see cref="EPSILON"/> of <paramref name="value"/> is the rung it stands on, not one to step to.
        /// </summary>
        internal static float OnRungs(ReadOnlySpan<float> rungs, float value, int direction)
        {
            float best = value;
            bool found = false;
            foreach (float rung in rungs)
            {
                bool way = direction > 0 ? rung > value + EPSILON : direction < 0 && rung < value - EPSILON;
                if (way && (!found || MathF.Abs(rung - value) < MathF.Abs(best - value)))
                {
                    best = rung;
                    found = true;
                }
            }

            return best;
        }

        /// <summary>
        /// One step from <paramref name="value"/> along the whole numbers <paramref name="first"/> to <paramref name="last"/>
        /// (the Sky row's domes), held at either end where the row's own press wraps round.
        /// </summary>
        internal static int InRange(int value, int first, int last, int direction) =>
            Math.Clamp(value + Math.Sign(direction), first, last);

        /// <summary>
        /// A gain on a ladder of even steps from Off (0) to full (1), the volumes' and the rumble's: one
        /// <paramref name="step"/> down or up, held at Off and at full.
        /// </summary>
        internal static float Gain(float gain, float step, int direction) =>
            direction == 0 ? gain : Math.Clamp(gain + Math.Sign(direction) * step, 0f, 1f);

        /// <summary>Whether a gain can step that way at all: what the end's button dims by.</summary>
        internal static bool CanStepGain(float gain, int direction) =>
            direction < 0 ? gain > EPSILON : direction > 0 && gain < 1f - EPSILON;

        //What two stored floats may differ by and still be the same rung: a settings file round-trips them through text
        private const float EPSILON = 1e-4f;
    }
}
