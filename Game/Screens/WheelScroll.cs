using System;

namespace BS3D.Screens
{
    /// <summary>
    /// How far a mouse-wheel notch moves a menu list (#952): a share of the list's own viewport, scaled by the notches the
    /// wheel reported and by how fast they are arriving, the arithmetic of <see cref="MenuScrollViewer"/> with nothing of
    /// Myra in it so a test can hold it.
    /// <para>
    /// <b>Why it is not Myra's.</b> <c>ScrollViewer.OnMouseWheel</c> (1.6.3) moves <c>ScrollMultiplier × ScrollMaximum.Y /
    /// thumb travel</c> pixels for any non-zero delta, whatever its size: with the knob above its minimum length that is ten
    /// times content height over viewport height, in pixels, so the same twenty-odd pixels at 1080p and at 3840x1600, a
    /// smaller share of the screen as the display grows, and a spin of several notches inside one frame moved one step. The
    /// owner's note (2026-10-19): "I have to turn it a lot and very fast". The numbers measured on the real lists are in
    /// "The mouse wheel over a list" in <c>docs/game-shell/menu-navigation.md</c>.
    /// </para>
    /// <para>
    /// <b>A share of the viewport and not of the content or a count of design units.</b> The viewport is what the player
    /// sees, so a notch is the same part of the picture whatever the resolution (a design unit is not: the menus' scale is
    /// held by the width on a wide display, and the viewport's height is not a constant number of units) and a long list
    /// does not jump further than a short one (what raising <c>ScrollMultiplier</c> alone would have done).
    /// </para>
    /// </summary>
    internal static class WheelScroll
    {
        /// <summary>
        /// What <c>MouseState.ScrollWheelValue</c> moves by for one detent of the wheel: Windows' <c>WHEEL_DELTA</c>, which
        /// MonoGame adds unchanged. A free-spinning or high-resolution wheel reports other multiples or fractions of it,
        /// which is why the step is proportional to the delta and not to its sign.
        /// </summary>
        internal const float NOTCH = 120f;

        /// <summary>
        /// The share of the viewport's height one slow notch moves: a fifth, so a single detent is a readable step of a few
        /// rows and not a jump, and five of them turn a page.
        /// </summary>
        internal const float VIEWPORT_SHARE = 0.2f;

        /// <summary>
        /// The longest gap, in seconds, between two events of one spin: notches arriving faster than this are one spin and
        /// speed each other up. A free-spinning wheel delivers a detent every 10 to 30 ms, a hand turning a ratcheted one
        /// quickly every 40 to 80, a deliberate one-at-a-time scroll 150 and more.
        /// </summary>
        internal const double STREAK_WINDOW = 0.1;

        /// <summary>The boost each notch already turned in the spin adds to the next event's step.</summary>
        internal const float BOOST_PER_NOTCH = 0.5f;

        /// <summary>
        /// The most a spin multiplies a notch by: four, which is 0.8 of the viewport, the share the right stick and Page Down
        /// on Help move (<c>HelpPage.PAGE_KEY_SHARE</c>), so even the fastest spin leaves a fifth of what was on screen on
        /// it and no line passes unseen between two events.
        /// </summary>
        internal const float MAX_BOOST = 4f;

        /// <summary>
        /// The pixels the content moves for a wheel delta of <paramref name="delta"/> over a viewport
        /// <paramref name="viewportHeight"/> tall, <paramref name="boost"/> times a slow notch's step
        /// (<see cref="Momentum.Take"/>): positive down the list. The wheel's sign is the OS's, positive away from the player,
        /// which is up the list, so it is negated here and nowhere else.
        /// </summary>
        internal static float Pixels(float delta, int viewportHeight, float boost = 1f) =>
            -delta / NOTCH * viewportHeight * VIEWPORT_SHARE * boost;

        /// <summary>
        /// Moves <paramref name="position"/> by <paramref name="pixels"/> inside [0, <paramref name="maximum"/>], whole
        /// pixels at a time: the fraction a step cannot place waits in <paramref name="carry"/> for the next one, so a
        /// high-resolution wheel's small deltas add up instead of each rounding to nothing. A step stopped by either end
        /// drops its carry, so turning back from the end does not first pay off pixels that were never moved.
        /// </summary>
        /// <returns>The new position.</returns>
        internal static int Move(int position, int maximum, float pixels, ref float carry)
        {
            float total = pixels + carry;
            int whole = (int)total;
            int target = Math.Clamp(position + whole, 0, Math.Max(0, maximum));

            carry = target == position + whole ? total - whole : 0f;

            return target;
        }

        /// <summary>
        /// What the last few wheel events add up to, so a fast spin accelerates: the notches turned so far in one run of
        /// events no further than <see cref="STREAK_WINDOW"/> apart and in one direction. The clock is handed in, so a test
        /// holds the rule without waiting.
        /// </summary>
        internal struct Momentum
        {
            private float _notches;
            private double _lastAt;
            private int _sign;

            /// <summary>
            /// The boost for an event of <paramref name="delta"/> that arrived at <paramref name="now"/> seconds: one for
            /// the first event of a spin, then <see cref="BOOST_PER_NOTCH"/> more for each notch the run has already
            /// turned, up to <see cref="MAX_BOOST"/>. A pause longer than the window or a turn the other way starts the
            /// run again. The event's own notches count for the next one, so a single frame that lands three detents is a
            /// slow step three times and not an accelerated one.
            /// </summary>
            internal float Take(double now, float delta)
            {
                int sign = Math.Sign(delta);
                if (sign == 0) return 1f;

                if (sign != _sign || now - _lastAt > STREAK_WINDOW) _notches = 0f;

                float boost = Math.Min(MAX_BOOST, 1f + _notches * BOOST_PER_NOTCH);

                _notches += Math.Abs(delta) / NOTCH;
                _sign = sign;
                _lastAt = now;

                return boost;
            }
        }
    }
}
