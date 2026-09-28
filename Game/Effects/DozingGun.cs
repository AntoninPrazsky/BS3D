using Microsoft.Xna.Framework;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// <b>The gun falls asleep</b> — one of #230's easter eggs, and the kind that issue calls free: it is seen and
    /// heard and touches nothing a gate measures. Leave a level alone long enough and the barrel sags below its aim
    /// (<c>Cannon.Droop</c>, drawn only), rises and falls with slow breaths, and every breath lets a letter Z rise
    /// over it and a soft snore out of it. Any touch — the aim moving, the gun walking, a key, a button, a
    /// stick — wakes it at once.
    /// <para>
    /// <b>Nothing about play changes while it sleeps.</b> The aim is where it was (a shot fired from a sleeping gun
    /// goes exactly where the awake one pointed, and firing is itself a touch), the ceiling only steps for shots, and
    /// a level nobody touches is a level nobody loses. The one thing that dims is the aim beam, which would
    /// otherwise run straight out of a barrel pointing somewhere else — so it fades as the gun nods off
    /// (<see cref="Awake"/>) and is back the instant it wakes.
    /// </para>
    /// <para>
    /// Never explained anywhere, on the issue's own terms: a surprise for the player who stops to look.
    /// </para>
    /// </summary>
    internal sealed class DozingGun
    {
        /// <summary>Seconds without a touch before the gun starts to nod off. The issue's "a minute or two".</summary>
        internal const float SLEEP_AFTER = 75f;

        /// <summary>Seconds from the first nod to fast asleep — slow, the way a head goes down.</summary>
        private const float NOD_OFF = 3.5f;

        /// <summary>Seconds from asleep to awake: a start, not a stretch — the player touched it and wants the gun.</summary>
        private const float WAKE = 0.3f;

        /// <summary>How far below its aim a sleeping barrel sags, in radians (about 17°).</summary>
        private const float DROOP = 0.3f;

        /// <summary>How far each breath lifts and lowers it on top of the sag, in radians.</summary>
        private const float BREATH = 0.035f;

        /// <summary>Seconds a breath takes; a snore and a Z at the top of each one.</summary>
        internal const float BREATH_PERIOD = 4.2f;

        /// <summary>How long a Z lives, how far it climbs and how far it wanders sideways while it does (world units).</summary>
        internal const float Z_LIFE = 3.4f;
        private const float Z_RISE = 3.2f, Z_DRIFT = 1.1f;

        private const int MAX_Z = 4;

        /// <summary>One letter on its way up out of the muzzle.</summary>
        internal struct Z
        {
            public Vector3 From;
            public float Age;
            public float Side;
            public bool Live;

            /// <summary>Where it is now: up, and wandering a little to one side as it goes.</summary>
            public readonly Vector3 Position(Vector3 right)
            {
                float t = Age / Z_LIFE;
                return From + Vector3.Up * (Z_RISE * t) + right * (Side * Z_DRIFT * MathF.Sin(t * MathF.PI));
            }
        }

        private readonly Z[] _zs = new Z[MAX_Z];
        private int _nextZ;

        private float _idle;
        private float _sleep;
        private float _breath;

        /// <summary>0 awake, 1 fast asleep — eased, so the nod and the start both read as motion rather than a switch.</summary>
        internal float Asleep => MathHelper.SmoothStep(0f, 1f, _sleep);

        /// <summary>1 - <see cref="Asleep"/>: what the aim beam is scaled by.</summary>
        internal float Awake => 1f - Asleep;

        /// <summary>The barrel's sag this frame, for <c>Cannon.Droop</c>: the sag itself and the breath riding on it.</summary>
        internal float Droop => Asleep * (DROOP + BREATH * MathF.Sin(_breath / BREATH_PERIOD * MathHelper.TwoPi));

        /// <summary>The letters in the air (a fixed pool; check <see cref="Z.Live"/>).</summary>
        internal ReadOnlySpan<Z> Letters => _zs;

        /// <summary>Wide awake, nothing in the air: a level starting.</summary>
        internal void Reset()
        {
            _idle = 0f;
            _sleep = 0f;
            _breath = 0f;
            for (int i = 0; i < _zs.Length; i++) _zs[i].Live = false;
        }

        /// <summary>
        /// One frame of the session's play.
        /// </summary>
        /// <param name="stirred">Whether the player touched anything this frame.</param>
        /// <param name="origin">Where a Z leaves from: over the gun.</param>
        /// <returns>True on the frame a breath peaks while asleep — the moment to snore.</returns>
        internal bool Update(float elapsed, bool stirred, Vector3 origin)
        {
            for (int i = 0; i < _zs.Length; i++)
            {
                if (!_zs[i].Live) continue;
                _zs[i].Age += elapsed;
                if (_zs[i].Age >= Z_LIFE) _zs[i].Live = false;
            }

            if (stirred)
            {
                _idle = 0f;
                _sleep = MathF.Max(0f, _sleep - elapsed / WAKE);
                return false;
            }

            _idle += elapsed;
            if (_idle < SLEEP_AFTER && _sleep <= 0f) return false;

            _sleep = MathF.Min(1f, _sleep + elapsed / NOD_OFF);

            //Breathing starts with the nod, from the bottom of a breath, so the first snore comes a breath in
            float before = _breath;
            _breath += elapsed;
            if (_sleep < 1f) return false;

            //The peak of a breath is a quarter period into it (the sine's top); crossing it snores and lets a Z go
            float peak = BREATH_PERIOD * 0.25f;
            bool crossed = Wrap(before) < peak && Wrap(_breath) >= peak;
            if (!crossed) return false;

            _zs[_nextZ] = new Z { From = origin, Age = 0f, Side = (_nextZ % 2 == 0) ? 1f : -1f, Live = true };
            _nextZ = (_nextZ + 1) % _zs.Length;
            return true;
        }

        private static float Wrap(float t) => t % BREATH_PERIOD;
    }
}
