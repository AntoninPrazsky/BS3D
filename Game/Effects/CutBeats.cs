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

        #region The severing's picture: the slash, the flash, the sparks and the light

        //The strike's picture is Blasts' blade style (Blast.fx on both paths), on the blasts' clock: the WORLD's seconds, slow
        //motion included, so it plays out the same at any frame rate. Its figures are here so the tests can hold them; the two
        //Blast.fx files carry the same values under the names in each remark, and a test reads them back (they cannot be
        //handed over as uniforms without a fourth vector per blast). #767's verdict set their size: the owner found the cut
        //"not very prominent - it should be bigger and last a bit longer - the sparks are barely perceptible, so few and so
        //briefly are they shown", so the slash is twice as thick and twice as long-lived as the first cut, the sparks twice
        //as many, bigger and brighter and living about a second, and the flash and the light last with them.

        /// <summary>The slash's whole life, simulated seconds: it shoots out, holds white-hot, thins, cools to amber and burns
        /// through over this (Blast.fx <c>SLASH_SECONDS</c>; it was 0.9).</summary>
        public const float SLASH_SECONDS = 1.8f;

        /// <summary>The blade's flash, simulated seconds (Blast.fx <c>BLADE_FLASH_SECONDS</c>; a bomb's is 0.16). Cubed as it
        /// fades, so it is under a tenth of its light by the end of the shortest slow motion (<see cref="FlashLeft"/>): the
        /// flash is the one part bright and wide enough to hide the field, and it never outlasts the slow motion.</summary>
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

        /// <summary>What is left of a blade's picture once a shot is fired (#765: skippable), simulated seconds: the slash jumps
        /// to its last stretch of cooling amber and is gone in this, and the player's next shot crosses a clear field.</summary>
        public const float HURRY_LEFT_SECONDS = 0.3f;

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

        /// <summary>The age a fired shot hurries a blade's picture to, simulated seconds (<see cref="HURRY_LEFT_SECONDS"/>).</summary>
        public static float Hurried(float age) => MathF.Max(age, SLASH_SECONDS - HURRY_LEFT_SECONDS);

        /// <summary>The world time a slow motion of <paramref name="size"/> lets pass while it runs, simulated seconds: the
        /// integral of <see cref="TimeScale"/> over <see cref="SlowSeconds"/>, by the midpoint rule at a millisecond.</summary>
        public static float WorldSecondsOfSlowMotion(float size, bool freed)
        {
            float wall = SlowSeconds(size, freed);
            const float step = 0.001f;
            double world = 0.0;
            for (float t = step * 0.5f; t < wall; t += step) world += TimeScale(t, size, freed) * step;
            return (float)world;
        }

        #endregion

        private static float SmoothStep(float x)
        {
            x = Math.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
