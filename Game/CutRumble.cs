using System;
using BS3D.Effects;

namespace BS3D
{
    /// <summary>
    /// <b>What the Cut says in the hand</b> (#767; the owner, 2026-10-05: emphasise it "with sound, picture and vibration"):
    /// three shapes, one a beat, through the existing mixer and never a <c>SetVibration</c> of its own.
    /// <list type="bullet">
    /// <item><description><b>The press</b>: a low pulse rising under the charge (<see cref="Charge"/>), a steady level asked
    /// every frame (<see cref="GamepadRumble.Hold"/> and <see cref="GamepadRumble.HoldTriggers"/>), so it follows the glow's
    /// own curve (<see cref="CutBeats.Charge"/>) and is gone the frame the charge is; the trigger motors take most of it, the
    /// heavy motor a little, and a short click on both triggers as it tops out (<see cref="CHARGED_TRIGGER"/>).</description></item>
    /// <item><description><b>The strike</b>: one hard knock as the storey is severed (<see cref="Strike"/>), raised TO a level
    /// rather than added on (<see cref="PadChannel.KickUpTo"/>), so the shot's own thump, still ringing when its ball strikes
    /// (a flight of under a tenth of a second), cannot stack the two past it.</description></item>
    /// <item><description><b>The fall</b>: what the cut freed rings on under the release's ceiling along the release's own
    /// curve (<see cref="LandingRumble.Release"/>, #810), so a cut and a landing that free the same count feel the same and
    /// the two cannot drift apart. A cut that freed nothing is the knock alone.</description></item>
    /// </list>
    /// <b>The joints</b> (the owner, #810: the old release "shakes my joints"): the knock is under the landing's ceilings on
    /// the heavy motor and at them on the light one, the press under a third of the heavy motor, so the heaviest thing the
    /// Cut asks of the body motors is <see cref="LandingRumble.CEILING_LEFT"/> and <see cref="LandingRumble.CEILING_RIGHT"/>,
    /// under the line loss's 0.9 (still the heaviest pulse the pad says). The Settings Rumble row scales all of it, as it
    /// scales everything. Pure arithmetic over <see cref="PadChannel"/>, so <c>CutRumbleTests</c> drive the very sums the pad
    /// is asked for.
    /// </summary>
    internal static class CutRumble
    {
        /// <summary>The press's hold at the top of the charge: the heavy and light body motors, and both trigger motors.</summary>
        public const float CHARGE_LEFT = 0.22f, CHARGE_RIGHT = 0.12f, CHARGE_TRIGGER = 0.45f;

        /// <summary>The press's floor at the moment of the press, so the hand feels it begin: a share of the top.</summary>
        public const float CHARGE_FLOOR = 0.2f;

        /// <summary>The click on both triggers as the charge tops out, and its length.</summary>
        public const float CHARGED_TRIGGER = 0.7f, CHARGED_TRIGGER_SECONDS = 0.06f;

        /// <summary>
        /// The strike's knock, raised to on the two body motors: the heavy one a little under the landing's ceiling, the
        /// light one at it, short and firm - one knock, not a roll.
        /// </summary>
        public const float KNOCK_LEFT = 0.45f, KNOCK_RIGHT = 0.65f, KNOCK_SECONDS = 0.16f;

        /// <summary>The strike's knock on both trigger motors: a hard short break under each finger.</summary>
        public const float KNOCK_TRIGGER = 0.8f, KNOCK_TRIGGER_SECONDS = 0.08f;

        /// <summary>
        /// The press's hold <paramref name="age"/> wall-clock seconds after it, each motor 0 to 1: rising with the charge to
        /// its top, then falling away with the glow; nothing before the press or once the charge has settled.
        /// </summary>
        public static (float Left, float Right, float LeftTrigger, float RightTrigger) Charge(float age)
        {
            //Only the rise and a quick fall: the pad says "charging", it does not hum through the whole settle of the glow
            if (age < 0f || age >= CutBeats.CHARGE_SECONDS + FALL_SECONDS) return (0f, 0f, 0f, 0f);

            float level = age < CutBeats.CHARGE_SECONDS
                ? CHARGE_FLOOR + (1f - CHARGE_FLOOR) * CutBeats.Charge(age)
                : 1f - (age - CutBeats.CHARGE_SECONDS) / FALL_SECONDS;

            return (CHARGE_LEFT * level, CHARGE_RIGHT * level, CHARGE_TRIGGER * level, CHARGE_TRIGGER * level);
        }

        //How fast the press's hold falls away after its top, seconds
        private const float FALL_SECONDS = 0.15f;

        /// <summary>
        /// The strike onto the two body motors' channels, as <see cref="GamepadRumble"/> sends it: the knock raised to and,
        /// when the cut freed <paramref name="freed"/> balls (the storey and what hung on it), the fall under the release's
        /// ceilings along its curve. Nothing but the knock for a cut that freed nothing.
        /// </summary>
        public static void Strike(ref PadChannel left, ref PadChannel right, int freed)
        {
            left.KickUpTo(KNOCK_LEFT, KNOCK_SECONDS);
            right.KickUpTo(KNOCK_RIGHT, KNOCK_SECONDS);

            if (freed <= 0) return;

            (float fallLeft, float fallRight, float seconds) = LandingRumble.Release(freed);
            left.KickUnder(fallLeft, seconds, LandingRumble.CEILING_LEFT);
            right.KickUnder(fallRight, seconds, LandingRumble.CEILING_RIGHT);
        }
    }
}
