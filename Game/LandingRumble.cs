using System;

namespace BS3D
{
    /// <summary>
    /// <b>What a landing says in the hand</b> (#378, #810): the tap of a ball sticking and, over it, whatever the shot let
    /// go, under <b>one</b> ceiling well below the motors' full. Pure functions of how many balls left and one
    /// <see cref="Apply"/> onto the mixer's own channels, so the tests pin the curve and the very sum the pad is asked for
    /// (<c>LandingRumbleTests</c>).
    /// <para>
    /// <b>Why a curve and a ceiling.</b> The release was a straight line until #810, 0.22 + 0.04 a ball on the heavy motor
    /// and 0.7 of it on the light one, with the landing's tap kicked on top: it reached the motor's full at 20 balls, so
    /// every release from 20 up was the same pulse with both motors asked for everything the pad can do, and in
    /// <c>LevelGen --drops</c>'s modelled plays of the campaign (2026-10-12) about half of all releases are 20 balls or
    /// more (the median is 19 for the casual player, 27 for the greedy one). The owner's verdict on it with the pad in
    /// hand (2026-10-05): pleasant, but too strong, "it shakes my joints"; the more balls the stronger, but even a big one
    /// must not be too strong. So a release asks for a <b>share of the ceiling</b> (<see cref="Share"/>) that grows with a
    /// logarithm of the count: a light tap for three to five balls, still growing through the tens and the hundreds but
    /// more slowly, and the whole ceiling only from <see cref="FULL_COUNT"/>, which one or two releases in a hundred reach
    /// in those plays.
    /// </para>
    /// <para>
    /// <b>Longer rather than harder.</b> What a big release gains goes into its length as much as its strength
    /// (<see cref="RELEASE_SECONDS_SMALL"/> to <see cref="RELEASE_SECONDS_FULL"/>), and <b>the light motor leads at every
    /// size</b>, its ceiling (<see cref="CEILING_RIGHT"/>) above the heavy one's (<see cref="CEILING_LEFT"/>): the
    /// wrist-shaking is taken to be the heavy motor's mass, and the light one buzzes without the thump. Under the line loss
    /// on both motors and in length (<c>GameplayScreen</c>'s <c>LINE_LOSS_RUMBLE_*</c>), which stays the heaviest thing
    /// the pad says.
    /// </para>
    /// <para>
    /// <b>The tap is the tap, the release rings under the ceiling.</b> The tap is an ordinary kick, exactly the one the
    /// owner felt and kept (#378), so a plain landing is what it always was, the shot's thump still ringing under it
    /// included (measured on Pennant, a flight of about 0.08 s: 0.29 and 0.38 where the tap alone is
    /// <see cref="TAP_LEFT"/> and <see cref="TAP_RIGHT"/>). The release goes into the channel's ceilinged layer
    /// (<see cref="PadChannel.KickUnder"/>), where it fills its share of what is left under the ceiling above everything
    /// else ringing (<see cref="PadChannel.Blend"/>). So the tap and the release are felt together under one ceiling; a
    /// release is never less than a plain landing and rings on after it; the ceiling is reached only at
    /// <see cref="FULL_COUNT"/> however much of the shot is still ringing; and neither the next shot nor the gun's rattle
    /// lifts a release's tail past the ceiling or cuts it short.
    /// </para>
    /// </summary>
    internal static class LandingRumble
    {
        /// <summary>A ball sticking and nothing more: a light tap, mostly the light motor's buzz, matching the landing's
        /// own quick, bright thunk (<c>ProceduralAudio.PlayLanded</c>). Felt and kept by the owner (#378).</summary>
        public const float TAP_LEFT = 0.12f, TAP_RIGHT = 0.3f, TAP_SECONDS = 0.08f;

        /// <summary>
        /// The most the heavy (left) motor is asked for by a landing and its release together: about half, the issue's
        /// starting figure, to be tuned with the pad in hand. Reached at <see cref="FULL_COUNT"/> balls.
        /// </summary>
        public const float CEILING_LEFT = 0.5f;

        /// <summary>The same for the light (right) motor: above <see cref="CEILING_LEFT"/>, since the top end's weight is
        /// carried by the buzz rather than the thump, and under the line loss's light motor.</summary>
        public const float CEILING_RIGHT = 0.65f;

        /// <summary>The release that reaches the ceiling: about the 99th percentile of the modelled releases (above; 239 balls
        /// for the casual player, 293 for the greedy one).</summary>
        public const int FULL_COUNT = 250;

        /// <summary>The logarithm's knee, in balls: below it the size grows nearly in proportion to the count, above it
        /// it slows. Ten puts a three-ball group near the tap and the median release about a third of the way up.</summary>
        public const float KNEE = 10f;

        /// <summary>The share of the ceiling any release asks for however small, so three balls leaving is still more than
        /// one ball sticking.</summary>
        public const float RELEASE_FLOOR = 0.14f;

        /// <summary>How long a release takes to fall silent: a short one for a small group, up to the longest at
        /// <see cref="FULL_COUNT"/>, still shorter than the line loss's heavy tail.</summary>
        public const float RELEASE_SECONDS_SMALL = 0.2f, RELEASE_SECONDS_FULL = 0.9f;

        private static readonly float FullLog = MathF.Log(1f + FULL_COUNT / KNEE);

        /// <summary>
        /// How big a release of <paramref name="released"/> balls is, 0 to 1: 0 for none, then
        /// ln(1 + n / <see cref="KNEE"/>) over its value at <see cref="FULL_COUNT"/>, and 1 from there on. Rises with every
        /// ball up to the full count, by less each time.
        /// </summary>
        public static float Size(int released)
        {
            if (released <= 0) return 0f;
            if (released >= FULL_COUNT) return 1f;

            return MathF.Min(1f, MathF.Log(1f + released / KNEE) / FullLog);
        }

        /// <summary>The share of the ceiling a release of <paramref name="released"/> balls asks for: 0 for none, then from
        /// <see cref="RELEASE_FLOOR"/> up along <see cref="Size"/> to 1 at <see cref="FULL_COUNT"/>.</summary>
        public static float Share(int released) =>
            released <= 0 ? 0f : RELEASE_FLOOR + (1f - RELEASE_FLOOR) * Size(released);

        /// <summary>
        /// The release's own pulse in the ceilinged layer, for <paramref name="released"/> balls (nothing for none): each
        /// motor at its <see cref="Share"/> of its ceiling, and how long it takes to fall.
        /// </summary>
        public static (float Left, float Right, float Seconds) Release(int released)
        {
            if (released <= 0) return (0f, 0f, 0f);

            float share = Share(released);
            float seconds = RELEASE_SECONDS_SMALL + (RELEASE_SECONDS_FULL - RELEASE_SECONDS_SMALL) * Size(released);

            return (CEILING_LEFT * share, CEILING_RIGHT * share, seconds);
        }

        /// <summary>
        /// A landing onto the two body motors' channels, as <see cref="GamepadRumble"/> sends it: the tap as an ordinary
        /// kick and, when <paramref name="released"/> balls let go, the release under the ceilings.
        /// </summary>
        public static void Apply(ref PadChannel left, ref PadChannel right, int released)
        {
            left.Kick(TAP_LEFT, TAP_SECONDS);
            right.Kick(TAP_RIGHT, TAP_SECONDS);

            if (released <= 0) return;

            (float releaseLeft, float releaseRight, float seconds) = Release(released);
            left.KickUnder(releaseLeft, seconds, CEILING_LEFT);
            right.KickUnder(releaseRight, seconds, CEILING_RIGHT);
        }

        /// <summary>
        /// What a landing of <paramref name="released"/> balls asks of a pad that was silent: each motor at the landing's
        /// instant (<see cref="Apply"/> onto two quiet channels) and how long the longer of the tap and the release rings.
        /// The figures the issue's table and the tests read; never above <see cref="CEILING_LEFT"/> and
        /// <see cref="CEILING_RIGHT"/>.
        /// </summary>
        public static (float Left, float Right, float Seconds) Pulse(int released)
        {
            PadChannel left = default, right = default;
            Apply(ref left, ref right, released);

            return (left.Level, right.Level, MathF.Max(TAP_SECONDS, Release(released).Seconds));
        }
    }
}
