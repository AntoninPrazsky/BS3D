using System;

namespace BS3D
{
    /// <summary>
    /// <b>What a landing says in the hand</b> (#378, #810): the tap of a ball sticking and, over it, whatever the shot let
    /// go, as <b>one</b> pulse under <b>one</b> ceiling well below the motors' full. A pure function of how many balls
    /// left, so the tests pin the curve (<c>LandingRumbleTests</c>).
    /// <para>
    /// <b>Why a curve and a ceiling.</b> The release was a straight line until #810, 0.22 + 0.04 a ball on the heavy motor
    /// and 0.7 of it on the light one, with the landing's tap kicked on top: it reached the motor's full at 20 balls, so
    /// every release from 20 up was the same pulse with both motors asked for everything the pad can do, and in
    /// <c>LevelGen --drops</c>'s modelled plays of the campaign (2026-10-12) about half of all releases are 20 balls or
    /// more (the median is 19 for the casual player, 27 for the greedy one). The owner's verdict on it with the pad in
    /// hand (2026-10-05): pleasant, but too strong, "it shakes my joints"; the more balls the stronger, but even a big one
    /// must not be too strong. So the size is a logarithm of the count: a light tap for three to five balls, still
    /// growing through the tens and the hundreds but more slowly, and at the ceiling only from
    /// <see cref="FULL_COUNT"/>, which one or two releases in a hundred reach in those plays.
    /// </para>
    /// <para>
    /// <b>Longer rather than harder.</b> What a big release gains past the middle goes into its length as much as its
    /// strength (<see cref="RELEASE_SECONDS_SMALL"/> to <see cref="RELEASE_SECONDS_FULL"/>), and <b>the light motor leads
    /// at every size</b>, ending higher than the heavy one (<see cref="CEILING_RIGHT"/> over <see cref="CEILING_LEFT"/>):
    /// the wrist-shaking is taken to be the heavy motor's mass, and the light one buzzes without the thump. Under the line
    /// loss on both motors and in length (<c>GameplayScreen</c>'s <c>LINE_LOSS_RUMBLE_*</c>), which stays the heaviest
    /// thing the pad says.
    /// </para>
    /// <para>
    /// <b>The ceiling holds for the sum, not just for one landing.</b> The pulse is sent through
    /// <see cref="GamepadRumble.KickUnder"/>, which adds it to what is still ringing only up to <see cref="CEILING_LEFT"/>
    /// and <see cref="CEILING_RIGHT"/> (<see cref="PadChannel.Stack"/>), so a big release landing on the tail of the last
    /// one is not felt at twice the ceiling. The player's Rumble row scales all of it afterwards, as it scales everything.
    /// </para>
    /// <para>
    /// ⚠ <b>The shot's own thump is usually still ringing when its ball lands.</b> Measured in a scripted run of Pennant
    /// (2026-10-12, a flight of about 0.08 s): a plain landing put the motors at 0.29 and 0.38 where the tap alone is
    /// <see cref="TAP_LEFT"/> and <see cref="TAP_RIGHT"/>, and a 47-ball release stood at the heavy ceiling where its own
    /// pulse is 0.35. So on a short flight the heavy motor's peak tops out from about 30 balls, and past that a release
    /// grows in its length and on the light motor. Summing rather than replacing is deliberate: it keeps the plain landing
    /// the owner felt and kept (#378) exactly as it was, and keeps a small release above a plain landing.
    /// </para>
    /// </summary>
    internal static class LandingRumble
    {
        /// <summary>A ball sticking and nothing more: a light tap, mostly the light motor's buzz, matching the landing's
        /// own quick, bright thunk (<c>ProceduralAudio.PlayLanded</c>). Felt and kept by the owner (#378).</summary>
        public const float TAP_LEFT = 0.12f, TAP_RIGHT = 0.3f, TAP_SECONDS = 0.08f;

        /// <summary>What any release adds over the tap however small it is, so three balls leaving is still more than one
        /// ball sticking.</summary>
        public const float RELEASE_FLOOR_LEFT = 0.05f, RELEASE_FLOOR_RIGHT = 0.05f;

        /// <summary>
        /// The most the heavy (left) motor is asked for by a landing and everything ringing under it: about half, the
        /// issue's starting figure, to be tuned with the pad in hand. Reached at <see cref="FULL_COUNT"/> balls.
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

        /// <summary>
        /// The one pulse a landing sends, the tap and the release together, for <paramref name="released"/> balls let go
        /// (0 for a plain attach): the heavy and the light motor's levels and how long they take to fall. Never above
        /// <see cref="CEILING_LEFT"/> and <see cref="CEILING_RIGHT"/>.
        /// </summary>
        public static (float Left, float Right, float Seconds) Pulse(int released)
        {
            if (released <= 0) return (TAP_LEFT, TAP_RIGHT, TAP_SECONDS);

            float size = Size(released);
            float left = TAP_LEFT + RELEASE_FLOOR_LEFT + (CEILING_LEFT - TAP_LEFT - RELEASE_FLOOR_LEFT) * size;
            float right = TAP_RIGHT + RELEASE_FLOOR_RIGHT + (CEILING_RIGHT - TAP_RIGHT - RELEASE_FLOOR_RIGHT) * size;
            float seconds = RELEASE_SECONDS_SMALL + (RELEASE_SECONDS_FULL - RELEASE_SECONDS_SMALL) * size;

            return (MathF.Min(left, CEILING_LEFT), MathF.Min(right, CEILING_RIGHT), seconds);
        }
    }
}
