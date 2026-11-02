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
    /// are wall-clock seconds; the strike's glows are the world's, so their age is the simulation's seconds, as the blasts'
    /// (#389). <b>The world always runs in real time</b> (#1046, the owner's ruling: no hit-stop, no slow motion, anywhere),
    /// so the two clocks run together; they are kept apart because the glows belong to what the simulation moves.
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
        /// (<see cref="LandingRumble.Size"/>, #810), so the pad and the effect grow along one curve and cannot drift apart.
        /// </summary>
        public static float Size(int freed) => LandingRumble.Size(freed);

        //NO SLOW MOTION (#1046). Until then every cut began with a hit-stop (the world at 0.15 for 0.07 s) and let what it
        //freed fall at down to 0.35 of the world's speed for up to 1.35 s. The owner took it out: "the game always runs in
        //real time; the slowing makes no sense and players do not like it". The fall is the world's at full speed, and the
        //moment is the slash, the flash, the sparks, the glowing faces, the slice's ring and the knock in the hand.

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

        #region The severing's picture: the slash, the flash, the sparks and the light

        //The strike's picture is Blasts' blade style (Blast.fx on both paths), on the blasts' clock: the WORLD's seconds, so
        //it plays out the same at any frame rate. Its figures are here so the tests can hold them; the two
        //Blast.fx files carry the same values under the names in each remark, and a test reads them back (they cannot be
        //handed over as uniforms without a fourth vector per blast). #767's verdict set their size ("bigger and last a bit
        //longer"; the sparks "barely perceptible, so few and so briefly are they shown"): a thick slash living nearly two
        //seconds, 192 sparks living about a second, and the flash and the light lasting with them.

        /// <summary>The slash's whole life, simulated seconds: it shoots out, holds white-hot, thins, cools to amber and burns
        /// through over this (Blast.fx <c>SLASH_SECONDS</c>).</summary>
        public const float SLASH_SECONDS = 1.8f;

        /// <summary>The blade's flash, simulated seconds (Blast.fx <c>BLADE_FLASH_SECONDS</c>; a bomb's is 0.16). Cubed as it
        /// fades, so it is under a tenth of its light after 0.16 s (<see cref="FlashLeft"/>): the flash is the one part bright
        /// and wide enough to hide the field, and it is gone almost at once.</summary>
        public const float FLASH_SECONDS = 0.3f;

        /// <summary>A spark's mean life, simulated seconds (Blast.fx <c>BLADE_SPARK_SECONDS</c>), jittered per spark between
        /// <see cref="SPARK_LIFE_LEAST"/> and <see cref="SPARK_LIFE_MOST"/> of it: seven sparks in ten live a second or more.</summary>
        public const float SPARK_SECONDS = 1.25f;
        public const float SPARK_LIFE_LEAST = 0.55f;
        public const float SPARK_LIFE_MOST = 1.45f;

        /// <summary>How long the sparks take to light along the slash from the struck ball to its ends, simulated seconds
        /// (Blast.fx <c>BLADE_SPARK_RUN</c>): they come off the line as it shoots out, never ahead of it.</summary>
        public const float SPARK_RUN_SECONDS = 0.1f;

        /// <summary>The blade's light on the balls round it, simulated seconds (Blasts' <c>SceneLights</c> slot; a bomb's 0.45).</summary>
        public const float LIGHT_SECONDS = 1.0f;

        /// <summary>How long a blade holds its slot in the blasts' buffer: past the slash and the longest spark.</summary>
        public const float LIFE_SECONDS = 1.95f;

        /// <summary>
        /// How many times as fast a blade's picture plays out once a shot is fired while it burns (#765: skippable): the rest
        /// of the slash, the sparks and the light run through every age they would have passed, so nothing vanishes in one
        /// frame, and all of it is gone within <see cref="LIFE_SECONDS"/> / this of the shot (a third of a second) however
        /// early the shot came (<see cref="HurriedSecondsLeft"/>).
        /// </summary>
        public const float HURRY_RATE = 6f;

        /// <summary>How much of the blade's flash is left <paramref name="age"/> simulated seconds after the strike, 1 to 0
        /// (Blast.fx's <c>fade * fade * fade</c>).</summary>
        public static float FlashLeft(float age)
        {
            if (age < 0f || age >= FLASH_SECONDS) return age < 0f ? 1f : 0f;
            float fade = 1f - age / FLASH_SECONDS;
            return fade * fade * fade;
        }

        /// <summary>
        /// How strong the blade's light is <paramref name="age"/> simulated seconds after the strike, as a multiple of its
        /// colour: never under half for a cut that freed nothing (the strike itself is the event), full for a full-size one,
        /// thinning as the square of what is left of it, the ceiling flash's rule.
        /// </summary>
        public static float LightStrength(float age, float size)
        {
            if (age < 0f || age >= LIGHT_SECONDS) return 0f;
            float left = 1f - age / LIGHT_SECONDS;
            return (0.5f + 0.5f * Math.Clamp(size, 0f, 1f)) * left * left;
        }

        /// <summary>How long a blade's picture still burns once a shot hurries it at <paramref name="age"/> simulated seconds:
        /// what was left of its slot's life, played <see cref="HURRY_RATE"/> times as fast.</summary>
        public static float HurriedSecondsLeft(float age) => MathF.Max(0f, LIFE_SECONDS - age) / HURRY_RATE;

        #endregion

        private static float SmoothStep(float x)
        {
            x = Math.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }
    }
}
