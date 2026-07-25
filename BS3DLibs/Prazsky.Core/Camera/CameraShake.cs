using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Camera
{
    /// <summary>
    /// A camera's reaction to a single violent event — a shot leaving a barrel, an impact, a blast. Two
    /// things at once, because a real gun does two things at once: a <b>recoil</b>, which is directional
    /// (the lens is thrown back along the view and the muzzle rises) and settles quickly, and a
    /// <b>shake</b>, which is a random rattle of the whole frame, roll included, decaying to nothing.
    /// <para>
    /// It produces offsets only — it owns no pose and knows nothing about where the camera is. The camera
    /// adds them onto whatever pose it computed for the frame (see the game's <c>RecoilCamera</c>), so a
    /// kick can never fight with the pose logic or leave the camera displaced once it has decayed: at
    /// rest every output here is exactly zero.
    /// </para>
    /// <para>
    /// Both decays are <b>linear in time</b>, not exponential, so the shake genuinely ends rather than
    /// approaching zero forever and leaving a permanent sub-pixel jitter in the frame. The rattle's phase
    /// runs off accumulated seconds, so its shape is the same at 30 and at 300 FPS — nothing here is tied
    /// to the frame rate.
    /// </para>
    /// </summary>
    public sealed class CameraShake
    {
        /// <summary>How fast the rattle dies, in trauma per second (1 ÷ this is its length: ~0.3 s).</summary>
        public float TraumaDecay { get; set; } = 3.4f;

        /// <summary>
        /// How fast the directional kick recovers, per second. Shorter than the rattle: the barrel is
        /// thrown back and comes home quickly, while the frame is still ringing from it.
        /// </summary>
        public float RecoilDecay { get; set; } = 5.5f;

        /// <summary>
        /// Rattle frequency. High enough to read as a hard jolt rather than a wobble; a slow shake reads
        /// as a camera on a loose mount, which is a different event from a gun firing.
        /// </summary>
        public float ShakeFrequency { get; set; } = 24f;

        /// <summary>Peak random pitch of the rattle, radians (~1.1°).</summary>
        public float MaxPitch { get; set; } = 0.020f;

        /// <summary>Peak random yaw of the rattle, radians (~0.9°).</summary>
        public float MaxYaw { get; set; } = 0.016f;

        /// <summary>
        /// Peak random roll, radians (~2.6°). Deliberately the largest of the three: rolling the horizon
        /// is what makes the eye read "the whole camera was hit" instead of "the aim moved a little".
        /// </summary>
        public float MaxRoll { get; set; } = 0.045f;

        /// <summary>Peak lateral/vertical displacement of the lens, world units.</summary>
        public float MaxOffset { get; set; } = 0.28f;

        /// <summary>How far the recoil throws the lens straight back along the view, world units.</summary>
        public float MaxRecoilBack { get; set; } = 0.9f;

        /// <summary>
        /// Muzzle rise: an upward pitch from the recoil, radians (~2°). Unlike the rattle this is not
        /// random — a gun always kicks the same way, and that consistency is what separates recoil from
        /// a generic shake.
        /// </summary>
        public float MaxRecoilPitch { get; set; } = 0.035f;

        /// <summary>Fraction the field of view widens at the peak of the kick (a small punch outwards).</summary>
        public float MaxFovPunch { get; set; } = 0.035f;

        private float _trauma;
        private float _recoil;
        private float _time;

        /// <summary>
        /// Whether anything is still moving. Zero here means every offset below is exactly zero, so a
        /// caller may skip the work entirely.
        /// </summary>
        public bool IsActive => _trauma > 0f || _recoil > 0f;

        /// <summary>
        /// Adds one event's worth of kick. Accumulates and saturates at 1, so firing repeatedly keeps the
        /// camera lively without the shake growing without bound.
        /// </summary>
        /// <param name="strength">How hard, 0 to 1 for a full kick.</param>
        public void Kick(float strength = 1f)
        {
            if (strength <= 0f) return;

            _trauma = MathHelper.Clamp(_trauma + strength, 0f, 1f);
            _recoil = MathHelper.Clamp(_recoil + strength, 0f, 1f);
        }

        public void Update(float elapsedSeconds)
        {
            _time += elapsedSeconds;

            _trauma = MathF.Max(0f, _trauma - TraumaDecay * elapsedSeconds);
            _recoil = MathF.Max(0f, _recoil - RecoilDecay * elapsedSeconds);
        }

        /// <summary>Drops the kick immediately (a cut, a respawn — anywhere continuity is not wanted).</summary>
        public void Reset()
        {
            _trauma = 0f;
            _recoil = 0f;
        }

        /// <summary>
        /// The rattle's amplitude. Squared rather than linear: a linear decay spends most of its life in
        /// the middle amplitudes, which reads as a lingering wobble, while the square drops away fast and
        /// leaves a sharp jolt.
        /// </summary>
        private float ShakeAmount => _trauma * _trauma;

        private float RecoilAmount => _recoil * _recoil;

        /// <summary>
        /// Angular offset of the frame: X pitch, Y yaw, Z roll, in radians. The pitch carries the
        /// recoil's muzzle rise on top of the random rattle.
        /// </summary>
        public Vector3 RotationOffset
        {
            get
            {
                float shake = ShakeAmount;
                float t = _time * ShakeFrequency;

                return new Vector3(
                    Noise(t, 0f) * MaxPitch * shake + RecoilAmount * MaxRecoilPitch,
                    Noise(t, 11.3f) * MaxYaw * shake,
                    Noise(t, 23.7f) * MaxRoll * shake);
            }
        }

        /// <summary>Displacement of the lens in its own frame: X to the right, Y up, world units.</summary>
        public Vector2 LensOffset
        {
            get
            {
                float shake = ShakeAmount;
                float t = _time * ShakeFrequency;

                return new Vector2(Noise(t, 5.1f), Noise(t, 17.9f)) * MaxOffset * shake;
            }
        }

        /// <summary>How far back along the view the lens is thrown, world units.</summary>
        public float RecoilBack => RecoilAmount * MaxRecoilBack;

        /// <summary>Fraction to widen the field of view by this frame.</summary>
        public float FovPunch => RecoilAmount * MaxFovPunch;

        /// <summary>
        /// Two sines at incommensurable frequencies: smooth, deterministic, allocation-free, and with no
        /// period short enough for the eye to catch it repeating. A random number per frame would be
        /// frame-rate dependent (a fast machine would shake at a higher frequency than a slow one).
        /// </summary>
        private static float Noise(float t, float seed) =>
            MathF.Sin(t + seed) * 0.62f + MathF.Sin(t * 1.71f + seed * 2.3f) * 0.38f;
    }
}
