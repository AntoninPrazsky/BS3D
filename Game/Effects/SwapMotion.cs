using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// <b>What a Swap looks like</b> (#871): the two rounds at the muzzle trade places by sliding <i>through each other</i> along
    /// the bore, each on the faintest of arcs to its own side, smeared along its path, with the cross-fade at the moment they interpenetrate
    /// at its heaviest so no clipping reads. Until #871 the swap was the re-colour's dither alone - the two balls stayed where they
    /// were and each changed colour - and the owner asked for the balls to be seen to "blur through each other, as if tunnelling".
    /// <para>
    /// It is <b>the picture of a move the magazine has already made</b>. <see cref="Magazine.SwapSlots"/> exchanges the two slots
    /// at once, and the next shot is the round that now stands at the muzzle however far this has run; nothing here is a rule, so the
    /// swap costs the player no time. What the player sees is only the way the rounds get there: <see cref="Round"/> says where the
    /// round now held by a slot is drawn at an age, and how fast it is going (for the smear). A shot fired mid-swap
    /// <see cref="Cancel"/>s it: the queue has moved on and the rounds are simply where the magazine says.
    /// </para>
    /// <para>
    /// <b>Closed forms of the age</b>, so the same picture at 30, 60, 75 or 144 Hz; the age is the frame's own elapsed time
    /// (the clock the magazine's dissolve runs on, <see cref="Magazine.Step"/>), so a pause holds it.
    /// </para>
    /// <para>
    /// <b>The cross-fade is the dither's</b> (<see cref="Round"/>'s <c>Dissolve</c>): the round that left the muzzle goes by the
    /// noise's lower cells and the one that came to it keeps the rest, which are exact complements at <see cref="MAX_FADE"/> = a
    /// half - so where the two spheres overlap every pixel is written by exactly one of them, and no intersection line is drawn.
    /// It ramps in over <see cref="FADE_RAMP"/> of the swap (the two start touching, and a half-dissolved ball on the first frame
    /// would pop) and out again the same way. The dither is blurred where it is not hidden: by the Game's motion blur, which
    /// takes each round's own velocity (<see cref="SMEAR_GAIN"/> over its shutter), and by the streak behind it.
    /// </para>
    /// </summary>
    internal struct SwapMotion
    {
        /// <summary>How long a swap's picture takes, in seconds: the magazine's own swap dissolve, so the strip's discs, which still
        /// cross-fade (the 2D readout is out of this picture's reach), settle on the same beat.</summary>
        public const float SECONDS = Magazine.SWAP_FADE_SECONDS;

        /// <summary>How far to its own side each round bows at the crossing, in the magazine's slot spacings: all the bore leaves.
        /// A round's radius is <c>Constants.HALF</c> (0.5) and the bore's <c>CannonRig.BORE_RADIUS</c> is 0.6, so a bow of a tenth
        /// is the most a round can make and stay inside the steel; at 0.38 each pushed through the barrel's wall and its dithered
        /// surface showed on the outside of the tube (the first build). The two rounds therefore pass through nearly the same
        /// line, which is the tunnelling the owner asked for, and the cross-fade is what hides it.</summary>
        public const float ARC = 0.08f;

        /// <summary>The most either round's pixels are cut at the crossing: a half, where the two dithers are exact complements.</summary>
        public const float MAX_FADE = 0.5f;

        /// <summary>The share of the swap the fade takes to arrive, and again to leave.</summary>
        public const float FADE_RAMP = 0.2f;

        /// <summary>How many times the honest distance covered over the blur's shutter each round is smeared by: the blur must be
        /// seen (<c>docs/rendering/motion-blur-and-resolution.md</c>), and a round crossing a slot spacing in a third of a second
        /// is slow by a shot's standards.</summary>
        public const float SMEAR_GAIN = 2.5f;

        private float _age;
        private bool _running;

        /// <summary>Whether a swap is being shown.</summary>
        public readonly bool Active => _running;

        /// <summary>How long the swap has been shown, in seconds; 0 when none is.</summary>
        public readonly float Age => _running ? _age : 0f;

        /// <summary>How far through the swap, 0 to 1; 0 when none is shown.</summary>
        public readonly float Progress => _running ? MathF.Min(1f, _age / SECONDS) : 0f;

        /// <summary>The Swap's press: a swap begins, over one already running or not.</summary>
        public void Start()
        {
            _age = 0f;
            _running = true;
        }

        /// <summary>Ends the picture at once - a shot has moved the queue on, or the session has ended.</summary>
        public void Cancel()
        {
            _running = false;
            _age = 0f;
        }

        /// <summary>Ages the swap by the frame's own seconds; it ends by itself at <see cref="SECONDS"/>.</summary>
        public void Step(float elapsedSeconds)
        {
            if (!_running) return;

            _age += elapsedSeconds;
            if (_age >= SECONDS) Cancel();
        }

        /// <summary>
        /// Testing only (<c>swapshot=</c>): the age is held at <paramref name="seconds"/> instead of running on, so a frame can be
        /// saved at a stated moment of the picture however long a capture takes.
        /// </summary>
        internal void Pin(float seconds)
        {
            if (_running) _age = MathF.Min(seconds, SECONDS - 1e-4f);
        }

        /// <summary>How far along its way a round is at <paramref name="progress"/>, 0 to 1: eased in and out, so the rounds leave
        /// their slots softly and cross quickly.</summary>
        public static float Travel(float progress)
        {
            float s = Math.Clamp(progress, 0f, 1f);
            return s * s * (3f - 2f * s);
        }

        /// <summary>How fast a round travels at <paramref name="progress"/>, in its whole way per second.</summary>
        public static float Rate(float progress)
        {
            float s = Math.Clamp(progress, 0f, 1f);
            return 6f * s * (1f - s) / SECONDS;
        }

        /// <summary>How much of each round's pixels are cut at <paramref name="progress"/>: 0 at both ends, <see cref="MAX_FADE"/>
        /// through the middle, smoothly between.</summary>
        public static float Fade(float progress)
        {
            float arrive = SmoothStep(progress / FADE_RAMP);
            float leave = SmoothStep((1f - progress) / FADE_RAMP);
            return MAX_FADE * MathF.Min(arrive, leave);
        }

        private static float SmoothStep(float x)
        {
            x = Math.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        /// <summary>
        /// Where the round now held by magazine <paramref name="slot"/> (0, the muzzle, or 1) is drawn at <paramref name="progress"/>
        /// of a swap, where it is heading and how the dither cuts it.
        /// <para>
        /// The round at the muzzle came from slot 1 and the one in slot 1 from the muzzle, so each travels the other's way; each bows
        /// to its own side (<paramref name="side"/>, a unit vector square to the bore - the barrel's own right), the muzzle's to the
        /// positive and the other's to the negative, so they pass each other rather than through the same line.
        /// </para>
        /// </summary>
        /// <param name="slot">0 for the round at the muzzle, 1 for the one behind it.</param>
        /// <param name="progress">0 to 1 through the swap.</param>
        /// <param name="muzzle">Where slot 0 stands.</param>
        /// <param name="behind">Where slot 1 stands.</param>
        /// <param name="side">The unit vector the arc bows along.</param>
        /// <param name="position">Where the round is drawn.</param>
        /// <param name="velocity">Its velocity, in world units a second.</param>
        /// <param name="dissolve">The value for <c>ModelInstance.Dissolve</c>: the muzzle's round goes (positive), the other's keeps
        /// what that leaves (negative), so the two are complements at the crossing.</param>
        public static void Round(int slot, float progress, in Vector3 muzzle, in Vector3 behind, in Vector3 side,
            out Vector3 position, out Vector3 velocity, out float dissolve)
        {
            float travel = Travel(progress);
            float rate = Rate(progress);

            //The way from the round's old slot to its new one: slot 1 to 0 for the round now at the muzzle
            Vector3 from = slot == 0 ? behind : muzzle;
            Vector3 to = slot == 0 ? muzzle : behind;

            float sign = slot == 0 ? 1f : -1f;
            float bow = sign * ARC * Magazine.SPACING;

            position = Vector3.Lerp(from, to, travel) + side * (bow * MathF.Sin(MathF.PI * travel));
            velocity = (to - from) * rate + side * (bow * MathF.PI * MathF.Cos(MathF.PI * travel) * rate);

            //The muzzle's round goes and the other keeps the rest: the complements
            float fade = Fade(progress);
            dissolve = fade <= 0f ? 0f : slot == 0 ? fade : -(1f - fade);
        }
    }
}
