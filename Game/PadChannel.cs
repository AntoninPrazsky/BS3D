using System;

namespace BS3D
{
    /// <summary>
    /// <b>One motor's pulse</b> (#378): the level a pad motor is asked for and how fast it falls, which is all
    /// <see cref="GamepadRumble"/> keeps per motor. A kick is <b>added</b> to what is already ringing and the sum held under a
    /// ceiling, and the channel then falls linearly to zero over the kick's own length from wherever the sum stands
    /// (<see cref="Prazsky.Core.Camera.CameraShake"/>'s shape) — so two events on one frame blend instead of the second
    /// overwriting the first, and the latest kick sets how long the channel takes to go quiet.
    /// <para>
    /// <b>Why it is a type of its own (#810).</b> The landing and the group it releases are felt under one ceiling well
    /// below the motor's full (<see cref="LandingRumble"/>), and that promise is about the sum: a landing's own pulse is
    /// under it by construction, but a big release still ringing when the next one lands would add up past it. The
    /// accumulation is pure arithmetic over two floats, so the tests drive this very code through a burst of landings at
    /// several frame rates (<c>LandingRumbleTests</c>) instead of a copy of it.
    /// </para>
    /// </summary>
    internal struct PadChannel
    {
        //A kick with nothing to decay it over would divide by zero; nothing this is fed is ever this short on
        //purpose, but a caller's typo should decay fast rather than throw.
        private const float MIN_SECONDS = 0.02f;

        /// <summary>What the motor is asked for now, 0 to 1, before the player's own Rumble row scales it.</summary>
        public float Level { get; private set; }

        //How much Level falls a second: set by the latest kick, so it reaches zero over that kick's length
        private float _decayRate;

        /// <summary>
        /// Adds <paramref name="amount"/> (0 to 1) to what is ringing, but lifts the channel no higher than
        /// <paramref name="ceiling"/> (1, the motor's full, unless a caller promises less), and lets it fall to zero over
        /// <paramref name="seconds"/> from there. A channel already above the ceiling — something louder still ringing,
        /// like the line loss — is neither lowered nor raised; only its fall takes this kick's length, as at full.
        /// </summary>
        public void Kick(float amount, float seconds, float ceiling = 1f)
        {
            if (amount <= 0f) return;

            Level = Stack(Level, amount, ceiling);
            _decayRate = Level / MathF.Max(seconds, MIN_SECONDS);
        }

        /// <summary>The fall over <paramref name="elapsedSeconds"/>, never below zero.</summary>
        public void Decay(float elapsedSeconds) => Level = MathF.Max(0f, Level - _decayRate * elapsedSeconds);

        /// <summary>Silent at once, not by decay: a pause, the window losing focus.</summary>
        public void Stop() => Level = 0f;

        /// <summary>
        /// Where a channel ringing at <paramref name="ringing"/> stands after a kick of <paramref name="amount"/>: the sum,
        /// held at the lower of <paramref name="ceiling"/> and the motor's full, and never below where it already was.
        /// </summary>
        public static float Stack(float ringing, float amount, float ceiling)
        {
            float top = MathF.Max(ringing, MathF.Min(ceiling, 1f));
            return MathF.Min(ringing + MathF.Max(amount, 0f), top);
        }
    }
}
