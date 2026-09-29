using System;
using System.Numerics;

namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// <b>The air over a scene, as an acceleration that changes with time</b> (#95): a fixed heading in the ground plane
    /// and a strength that gusts. Read once a step by the caller and handed to <see cref="PhysicsWorld.Wind"/>.
    /// <para>
    /// <b>It has to vary, and that is the whole design.</b> A constant push on a structure hung from sockets is
    /// absorbed: the cluster leans a few degrees and stays leant, which reads as a crooked level rather than as air.
    /// What a hanging thing does in real wind is sway, and only a load that comes and goes makes it. So the strength is
    /// two sines of unrelated periods (a slow swell and a quicker flutter) that never line up, clamped into 0–1 of the
    /// strength: the wind always pushes the same way and is sometimes almost still, which is what a gust is, and it
    /// never reverses, which a real ground wind rarely does either.
    /// </para>
    /// <para>
    /// A plain value type's worth of arithmetic with no Bepu in it, so the sag gate (<c>Tools/LevelGen</c>) and the game
    /// share the very field: a gate that hung a level in a different wind from the one the player has would be checking
    /// a different world, which is the whole complaint <c>SagProbe</c> exists to have answered.
    /// </para>
    /// </summary>
    public readonly struct WindField
    {
        /// <summary>The slow swell's period, in seconds: long enough to read as weather and not as a metronome.</summary>
        public const float SWELL_SECONDS = 6.3f;

        /// <summary>The flutter's period. Not a fraction of the swell's, so the two do not repeat as a pattern.</summary>
        public const float FLUTTER_SECONDS = 2.3f;

        /// <summary>How much of the gust the swell carries; the flutter carries <c>0.5 − SWELL_SHARE</c>.</summary>
        private const float SWELL_SHARE = 0.32f;

        private readonly Vector2 _heading;
        private readonly float _strength;

        /// <summary>No wind: acceleration zero at every time.</summary>
        public static readonly WindField None = default;

        /// <param name="heading">The direction the air moves in the ground plane (x, z); need not be normalised, and
        /// a zero vector is no wind.</param>
        /// <param name="strength">The acceleration at the top of a gust, in units per second squared.</param>
        public WindField(Vector2 heading, float strength)
        {
            float length = heading.Length();
            _heading = length > 1e-4f ? heading / length : Vector2.Zero;
            _strength = _heading == Vector2.Zero ? 0f : MathF.Max(0f, strength);
        }

        /// <summary>The acceleration at the top of a gust, in units per second squared; zero for no wind.</summary>
        public float Strength => _strength;

        /// <summary>The unit heading in the ground plane, or zero for no wind.</summary>
        public Vector2 Heading => _heading;

        /// <summary>
        /// The gust at <paramref name="seconds"/>, 0–1: the share of the strength the air pushes with at that instant.
        /// Two sines and a constant, so it is smooth (no step in it for the cluster to ring at) and bounded (the
        /// strength is a ceiling, not a typical value).
        /// </summary>
        public static float Gust(float seconds)
        {
            float swell = MathF.Sin(MathF.Tau * seconds / SWELL_SECONDS);
            float flutter = MathF.Sin(MathF.Tau * seconds / FLUTTER_SECONDS + 1.7f);

            return Math.Clamp(0.5f + SWELL_SHARE * swell + (0.5f - SWELL_SHARE) * flutter, 0f, 1f);
        }

        /// <summary>The wind's acceleration at <paramref name="seconds"/> of the scene's play clock: horizontal, along the heading.</summary>
        public Vector3 Acceleration(float seconds)
        {
            if (_strength <= 0f) return Vector3.Zero;

            float push = _strength * Gust(seconds);
            return new Vector3(_heading.X * push, 0f, _heading.Y * push);
        }
    }
}
