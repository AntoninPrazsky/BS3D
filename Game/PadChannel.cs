using System;

namespace BS3D
{
    /// <summary>
    /// <b>One motor's pulse</b> (#378): the level a pad motor is asked for and how fast it falls, which is all
    /// <see cref="GamepadRumble"/> keeps per motor. An ordinary <see cref="Kick"/> is <b>added</b> to what is already
    /// ringing, held at the motor's full, and the sum then falls linearly to zero over the kick's own length
    /// (<see cref="Prazsky.Core.Camera.CameraShake"/>'s shape) — so two events on one frame blend instead of the second
    /// overwriting the first, and the latest kick sets how long they take to go quiet.
    /// <para>
    /// <b>And a second layer, for a pulse that promised a ceiling (#810)</b>: the group a landing let go
    /// (<see cref="LandingRumble"/>), sent by <see cref="KickUnder"/>. It rings <b>apart</b> from the ordinary kicks,
    /// with its own fall, for two reasons found in review. A single fall reset by every kick let the gun's rattle
    /// (0.035 s) or the next shot (0.12 s) cut a big release's 0.9 s tail to their own length, so "longer rather than
    /// harder" never happened while the gun rolled. And a sum let the next shot's thump land on that tail and stand
    /// far above the ceiling (0.55 on a 0.33 tail, 0.88). So the two layers are blended (<see cref="Blend"/>): the
    /// ceilinged one fills its share of what is left under its ceiling above everything else ringing. It never lifts
    /// the motor past the ceiling, nor past what the ordinary kicks ask for on their own, and it reaches the ceiling
    /// only when it is at the ceiling itself, whatever else is ringing.
    /// </para>
    /// <para>
    /// Pure arithmetic over a few floats, so the tests drive this very code through bursts of landings, shots and the
    /// rattle at several frame rates (<c>LandingRumbleTests</c>) instead of a copy of it.
    /// </para>
    /// </summary>
    internal struct PadChannel
    {
        //A kick with nothing to decay it over would divide by zero; nothing this is fed is ever this short on
        //purpose, but a caller's typo should decay fast rather than throw.
        private const float MIN_SECONDS = 0.02f;

        //Every ordinary kick: summed, held at 1, falling to zero over the latest kick's length
        private float _free, _freeRate;

        //The pulses that promised a ceiling: their level (never above _ceiling), their fall, and that ceiling
        private float _bounded, _boundedRate, _ceiling;

        /// <summary>What the motor is asked for now, 0 to 1, before the player's own Rumble row scales it.</summary>
        public readonly float Level => Blend(_free, _bounded, _ceiling);

        /// <summary>
        /// An ordinary event's pulse: adds <paramref name="amount"/> (0 to 1) to what is ringing, held at the motor's full,
        /// falling to zero over <paramref name="seconds"/> from there.
        /// </summary>
        public void Kick(float amount, float seconds)
        {
            if (amount <= 0f) return;

            _free = MathF.Min(_free + amount, 1f);
            _freeRate = _free / MathF.Max(seconds, MIN_SECONDS);
        }

        /// <summary>
        /// An ordinary event's pulse that promises a peak (#767, the Cut's knock): raises what is ringing TO
        /// <paramref name="level"/> (0 to 1) and lets it fall to zero over <paramref name="seconds"/> from there, rather than
        /// adding to it as <see cref="Kick"/> does; nothing when the ordinary kicks already ring at or above it. So a knock that
        /// arrives on the shot's own thump, still ringing a few hundredths of a second after the trigger, is felt as itself
        /// and never stacks past its level.
        /// </summary>
        public void KickUpTo(float level, float seconds)
        {
            float target = MathF.Min(level, 1f);
            if (target <= _free) return;

            _free = target;
            _freeRate = _free / MathF.Max(seconds, MIN_SECONDS);
        }

        /// <summary>
        /// A pulse that promises the player a bound (#810): <paramref name="amount"/> (0 to <paramref name="ceiling"/>)
        /// rings in its own layer, falling over <paramref name="seconds"/>, and is felt only as its share of what is left
        /// under <paramref name="ceiling"/> above the ordinary kicks (<see cref="Blend"/>). Two such pulses compose the same
        /// way, each filling its share of what the other leaves, and the later of their two ends is kept: the next one
        /// never cuts a long one short, and the pair reaches the ceiling only if one of them is already there.
        /// </summary>
        public void KickUnder(float amount, float seconds, float ceiling)
        {
            float top = MathF.Min(ceiling, 1f);
            if (amount <= 0f || top <= 0f) return;

            float ringing = MathF.Min(_bounded, top), added = MathF.Min(amount, top);
            float remaining = _boundedRate > 0f ? _bounded / _boundedRate : 0f;

            _bounded = ringing + added - ringing * added / top;
            _ceiling = top;
            _boundedRate = _bounded / MathF.Max(MathF.Max(seconds, MIN_SECONDS), remaining);
        }

        /// <summary>The fall over <paramref name="elapsedSeconds"/>, each layer at its own rate, never below zero.</summary>
        public void Decay(float elapsedSeconds)
        {
            _free = MathF.Max(0f, _free - _freeRate * elapsedSeconds);
            _bounded = MathF.Max(0f, _bounded - _boundedRate * elapsedSeconds);
        }

        /// <summary>Silent at once, not by decay: a pause, the window losing focus.</summary>
        public void Stop() => _free = _bounded = 0f;

        /// <summary>
        /// What a motor is asked for with <paramref name="free"/> ringing from the ordinary kicks and
        /// <paramref name="bounded"/> from the pulses under <paramref name="ceiling"/>: the bounded level as a share of its
        /// ceiling, filling that share of the room left above <paramref name="free"/>, so
        /// free + bounded × (1 − free / ceiling). Never below the louder of the two and never above the higher of
        /// <paramref name="free"/> and the ceiling; a motor already at or over the ceiling from the ordinary kicks (the
        /// shot's thump, the line loss) is just that.
        /// </summary>
        public static float Blend(float free, float bounded, float ceiling)
        {
            if (bounded <= 0f || free >= ceiling) return free;

            return free + MathF.Min(bounded, ceiling) * (1f - free / ceiling);
        }
    }
}
