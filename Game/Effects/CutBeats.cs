using System;

namespace BS3D.Effects
{
    /// <summary>
    /// <b>The Cut as a moment</b> (#767): the curves its four beats are drawn, heard and felt by, as pure functions of how long
    /// ago each beat began. Kept apart from the session's state (<see cref="CutStaging"/>) so the tests can ask the very
    /// curves the game plays at any time and step them at any frame rate: nothing here integrates over frames, so a beat
    /// is the same at 30, 60, 75 and 144 Hz by construction, and a pause (which stops the clocks feeding the ages) holds it.
    /// <para>
    /// <b>Which clock each beat runs on</b> (#767's rule): the press and the flight are the gun and the lens, so their ages
    /// are wall-clock seconds; the strike's glows are the world's, so their age is the simulation's seconds (slow motion
    /// included, as the blasts', #389); the slow motion itself is a property of time, so its own timeline runs on the wall
    /// clock, as the drop cinematic's does.
    /// </para>
    /// </summary>
    internal static class CutBeats
    {
        #region The press: the round in the bore charges

        /// <summary>How long the charge takes to reach its top, in seconds: the glow's peak, the saw's top speed, the pad's
        /// rising hold and the charge sound's "ting" (<see cref="Audio.CutSynth.CHARGE_RISE_SECONDS"/>) all land here.</summary>
        public const float CHARGE_SECONDS = 0.42f;

        /// <summary>How long the glow and the spin take to settle back to the loaded round's rest after the top, in seconds.</summary>
        public const float CHARGE_SETTLE_SECONDS = 0.8f;

        /// <summary>The whole press, in seconds: nothing of it is left after this.</summary>
        public const float PRESS_SECONDS = CHARGE_SECONDS + CHARGE_SETTLE_SECONDS;

        /// <summary>
        /// The saw's top speed while it charges, radians a second: the flight's own speed
        /// (<see cref="Prazsky.BS3D.BallRenderSet.CUTTER_SPIN_FLYING"/>), the fastest #766 measured to keep the teeth from
        /// aliasing down to 30 frames a second (under 0.4 of a tooth's pitch a frame). A spin-up from rest to it and back to the
        /// loaded idle reads as the blade coming up to speed; any faster would strobe on a slow machine, which is the one thing
        /// #766 had to fix.
        /// </summary>
        public const float SPIN_PEAK = Prazsky.BS3D.BallRenderSet.CUTTER_SPIN_FLYING;

        /// <summary>The loaded round's idle turn, radians a second (<see cref="Prazsky.BS3D.BallRenderSet.CUTTER_SPIN_LOADED"/>).</summary>
        public const float SPIN_LOADED = Prazsky.BS3D.BallRenderSet.CUTTER_SPIN_LOADED;

        /// <summary>
        /// How charged the round is, 0 to 1: rising from the press to 1 at <see cref="CHARGE_SECONDS"/> (squared, so it
        /// gathers and then surges), then settling smoothly back to 0 over <see cref="CHARGE_SETTLE_SECONDS"/>. 0 before
        /// the press (a negative age) and after it.
        /// </summary>
        public static float Charge(float age)
        {
            if (age < 0f || age >= PRESS_SECONDS) return 0f;

            if (age < CHARGE_SECONDS)
            {
                float u = age / CHARGE_SECONDS;
                return u * u;
            }

            return 1f - SmoothStep((age - CHARGE_SECONDS) / CHARGE_SETTLE_SECONDS);
        }

        /// <summary>
        /// How fast the saw turns <paramref name="age"/> seconds after the press, radians a second: from rest up to
        /// <see cref="SPIN_PEAK"/> at the top of the charge, then easing back to the loaded idle <see cref="SPIN_LOADED"/>.
        /// </summary>
        public static float SpinRate(float age)
        {
            if (age < 0f) return 0f;

            if (age < CHARGE_SECONDS)
            {
                float u = age / CHARGE_SECONDS;
                return SPIN_PEAK * u * u;
            }

            if (age >= PRESS_SECONDS) return SPIN_LOADED;

            float v = (age - CHARGE_SECONDS) / CHARGE_SETTLE_SECONDS;
            return SPIN_LOADED + (SPIN_PEAK - SPIN_LOADED) * (1f - SmoothStep(v));
        }

        /// <summary>
        /// How far the saw has turned since the press, radians: <see cref="SpinRate"/> integrated in closed form, so the
        /// blade's angle is the same at any frame rate and never jumps (changing the speed inside <c>clock × speed</c>
        /// would make it leap, #766's note).
        /// </summary>
        public static float SpinAngle(float age)
        {
            if (age <= 0f) return 0f;

            if (age < CHARGE_SECONDS)
            {
                float u = age / CHARGE_SECONDS;
                return SPIN_PEAK * CHARGE_SECONDS * u * u * u / 3f;
            }

            float atTop = SPIN_PEAK * CHARGE_SECONDS / 3f;
            float v = MathF.Min(1f, (age - CHARGE_SECONDS) / CHARGE_SETTLE_SECONDS);

            //∫(1 - smoothstep) over [0, v] is v - v³ + v⁴/2
            float settled = SPIN_LOADED * MathF.Min(age - CHARGE_SECONDS, CHARGE_SETTLE_SECONDS)
                + (SPIN_PEAK - SPIN_LOADED) * CHARGE_SETTLE_SECONDS * (v - v * v * v + 0.5f * v * v * v * v);

            return atTop + settled + SPIN_LOADED * MathF.Max(0f, age - PRESS_SECONDS);
        }

        /// <summary>
        /// What the saw's turn is offset by against the loaded round's steady <c>clock × SPIN_LOADED</c>: the charge's whole
        /// story as one angle, constant once the press is over, so the round is handed back to its idle turn without a step.
        /// </summary>
        public static float SpinOffset(float age) => SpinAngle(age) - SPIN_LOADED * MathF.Max(0f, age);

        #endregion

        #region The flight: the lens pinches

        /// <summary>How far the field of view narrows as a cutter leaves the barrel, as a share of it: a slight pinch.</summary>
        public const float PINCH = 0.05f;

        /// <summary>How fast the pinch comes on and how long it takes to let go, in seconds.</summary>
        public const float PINCH_IN_SECONDS = 0.04f;
        public const float PINCH_OUT_SECONDS = 0.38f;

        /// <summary>The field of view's multiplier <paramref name="age"/> seconds after the cutter was fired: 1 at rest.</summary>
        public static float FovScale(float age)
        {
            if (age < 0f || age >= PINCH_IN_SECONDS + PINCH_OUT_SECONDS) return 1f;

            float amount = age < PINCH_IN_SECONDS
                ? SmoothStep(age / PINCH_IN_SECONDS)
                : 1f - SmoothStep((age - PINCH_IN_SECONDS) / PINCH_OUT_SECONDS);

            return 1f - PINCH * amount;
        }

        #endregion

        #region The strike and the fall

        /// <summary>
        /// How big a cut's result is, 0 to 1, from the balls it freed: the release rumble's own logarithm
        /// (<see cref="LandingRumble.Size"/>, #810), so the pad, the slow motion and the effect grow along one curve and
        /// cannot drift apart.
        /// </summary>
        public static float Size(int freed) => LandingRumble.Size(freed);

        //THE SLOW MOTION. Every cut starts with a hit-stop - the world almost held for a beat as the blade goes through - and
        //a cut that freed something then lets it go in slow motion, deeper and longer the more it freed; one that freed
        //nothing comes straight back up. On the wall clock (see the class remarks), and only ever the world: the HUD, the lens
        //and the gun run on.

        /// <summary>The world's speed through the hit-stop, and how long it holds there, in seconds.</summary>
        public const float HIT_STOP_SCALE = 0.15f;
        public const float HIT_STOP_SECONDS = 0.07f;

        /// <summary>How slow the fall runs for the smallest and for a full-size result, as the world's speed.</summary>
        public const float FALL_SCALE_SMALL = 0.6f;
        public const float FALL_SCALE_FULL = 0.35f;

        /// <summary>How long the fall's slow motion holds for the smallest and for a full-size result, in seconds.</summary>
        public const float FALL_HOLD_SMALL = 0.25f;
        public const float FALL_HOLD_FULL = 0.75f;

        /// <summary>How long the world takes to come back up to speed, in seconds.</summary>
        public const float RECOVER_SECONDS = 0.3f;

        /// <summary>The whole slow motion for a result of <paramref name="size"/>, in seconds.</summary>
        public static float SlowSeconds(float size, bool freed) =>
            HIT_STOP_SECONDS + (freed ? Lerp(FALL_HOLD_SMALL, FALL_HOLD_FULL, size) : 0f) + RECOVER_SECONDS;

        /// <summary>
        /// The world's speed <paramref name="age"/> wall-clock seconds after the strike, 0 to 1: the hit-stop, then (when
        /// something was <paramref name="freed"/>) the fall's slow motion for a result of <paramref name="size"/>, then a
        /// smooth recovery to 1. 1 before the strike and after it.
        /// </summary>
        public static float TimeScale(float age, float size, bool freed)
        {
            if (age < 0f) return 1f;

            float hold = freed ? Lerp(FALL_HOLD_SMALL, FALL_HOLD_FULL, size) : 0f;
            float slow = freed ? Lerp(FALL_SCALE_SMALL, FALL_SCALE_FULL, size) : 1f;

            if (age < HIT_STOP_SECONDS) return HIT_STOP_SCALE;

            float since = age - HIT_STOP_SECONDS;

            //Out of the hit-stop into the fall's pace (or straight up, for a cut that freed nothing), over the recovery
            //when there is no fall to hold
            if (since < hold)
            {
                //Eased from the hit-stop into the fall's pace over its first tenth of a second
                float into = SmoothStep(MathF.Min(1f, since / 0.1f));
                return Lerp(HIT_STOP_SCALE, slow, into);
            }

            float from = hold > 0f ? slow : HIT_STOP_SCALE;
            float back = (since - hold) / RECOVER_SECONDS;
            if (back >= 1f) return 1f;

            return Lerp(from, 1f, SmoothStep(back));
        }

        /// <summary>How long the freed part's cut face glows, simulated seconds: held briefly, then cooling.</summary>
        public const float FALL_GLOW_HOLD_SECONDS = 0.12f;
        public const float FALL_GLOW_COOL_SECONDS = 0.75f;
        public const float FALL_GLOW_SECONDS = FALL_GLOW_HOLD_SECONDS + 4f * FALL_GLOW_COOL_SECONDS;

        /// <summary>
        /// How hot the cut faces are <paramref name="age"/> simulated seconds after the strike, 0 to 1: full for a beat, then
        /// cooling exponentially (white heat does not fade linearly), and cut to 0 at <see cref="FALL_GLOW_SECONDS"/>.
        /// </summary>
        public static float Heat(float age)
        {
            if (age < 0f || age >= FALL_GLOW_SECONDS) return 0f;
            if (age < FALL_GLOW_HOLD_SECONDS) return 1f;

            //The exponential's value at the end is taken off and the rest stretched to 1, so it ends at exactly 0
            float tail = MathF.Exp(-(FALL_GLOW_SECONDS - FALL_GLOW_HOLD_SECONDS) / FALL_GLOW_COOL_SECONDS);
            float cooling = MathF.Exp(-(age - FALL_GLOW_HOLD_SECONDS) / FALL_GLOW_COOL_SECONDS);

            return (cooling - tail) / (1f - tail);
        }

        /// <summary>
        /// How much of the cut face's heat a freed ball takes, by how far under the cut it hung (world units): the storey
        /// the blade went through at full, falling off down the freed part, so its top glows like a cut face and the rest
        /// only catches it.
        /// </summary>
        public static float HeatAtDepth(float depthUnderCut) => MathF.Exp(-MathF.Max(0f, depthUnderCut) / FALL_GLOW_DEPTH);

        /// <summary>How far down the freed part the heat reaches, world units: a storey down (1/√2) keeps about 45 % of it.</summary>
        public const float FALL_GLOW_DEPTH = 0.9f;

        #endregion

        private static float SmoothStep(float x)
        {
            x = Math.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
