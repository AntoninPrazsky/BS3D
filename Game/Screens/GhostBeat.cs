using System;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>The landing preview's beat</b> (#831): how big the aim ghost is at a given second, as a share of a whole ball's
    /// radius. A rubber ball's bounce rather than a sway: a fast rise that lands on <b>exactly a whole ball</b>, holds
    /// there, sags once and comes back to it, then a slower elastic settle down past the rest size to <see cref="LOW"/>
    /// and a little way back, and the next beat.
    /// <para>
    /// <b>Why the top is a whole ball.</b> A smaller sphere in the same place reads as <i>distance</i>, not as "not there
    /// yet": at 0.28 to 0.72 of a ball (a sine about one half, until #831) the ghost looked farther away than the pocket
    /// it was in (the owner, 2026-10-08). So the size tells the truth about where the ghost is, and the cue for "not yet"
    /// is the motion. <b>Never above 1</b>: the owner first allowed an overshoot of a few per cent and withdrew it the same
    /// day, because at the lattice's spacing (two radii) anything bigger pushes into the neighbours. So the rubber is in
    /// the approach and in the way it leaves, never in going past: the rise is a damped spring whose overshoot the one
    /// <c>MathF.Min</c> in <see cref="Scale"/> turns into a hold against the ceiling, and its rebound into a sag under it.
    /// <see cref="Prazsky.Core.Render.ModelInstance.GhostDissolve"/> clamps to 1 as well, so the encoding stops there
    /// whatever any curve asks for.
    /// </para>
    /// <para>
    /// <b>A pure function of the wall clock</b>, like the beam's crawl and the cluster's heartbeat: no state on the ghost
    /// (the frame visits every ball once, see <c>GameplayScreen.Draw.cs</c>'s header), and so the same curve at every frame
    /// rate. The hold at 1 lasts about 120 ms and the deep end about as long, so even 24 frames a second show both ends of
    /// every beat (<c>GhostBeatTests</c>). Faster than the cluster's 1.1 Hz breath, so the ghost reads as unsettled rather
    /// than as alive in step with the balls round it.
    /// </para>
    /// </summary>
    internal static class GhostBeat
    {
        /// <summary>Beats a second: one rise, its hold and its settle, 0.625 s.</summary>
        public const float HZ = 1.6f;

        /// <summary>The smallest the ghost gets, as a share of a whole ball's radius: the deepest point of the settle,
        /// reached once a beat. Small enough to read as "not yet" beside the balls round it at the overview stand-off.</summary>
        public const float LOW = 0.3f;

        /// <summary>From the start of a beat to the moment the ghost first touches a whole ball: the snap.</summary>
        public const float LAND_SECONDS = 0.06f;

        /// <summary>How far under the ceiling the one rebound sags, as a share of the climb (about; the spring sets it).</summary>
        public const float REBOUND = 0.15f;

        /// <summary>The settle's damping ratio: under 1, so the size falls past where it rests and springs back.</summary>
        public const float SETTLE_DAMPING = 0.5f;

        /// <summary>From leaving the ceiling to the deepest point of the settle (<see cref="LOW"/>).</summary>
        public const float SETTLE_DEEPEST_SECONDS = 0.2f;

        //The rise: y = 1 - e^(-kt)·cos(ωt), a spring released towards 1. It first reaches 1 at ωt = π/2 (LAND_SECONDS), would
        //overshoot until 3π/2 (held at 1 by the ceiling), sags under 1 to its deepest near 2π (REBOUND) and is back on 1 at 5π/2,
        //where the settle takes over: from 1, with no slope, so leaving the ceiling has no corner in it
        private static readonly float RiseOmega = MathF.PI / (2f * LAND_SECONDS);
        private static readonly float RiseDecay = -MathF.Log(REBOUND) / (2f * MathF.PI / RiseOmega);

        /// <summary>Seconds into a beat at which the settle takes over from the rise: back on the ceiling after the rebound.</summary>
        public const float TOP_SECONDS = 5f * LAND_SECONDS;

        //The settle: an under-damped spring's step response, from 1 towards REST with zero slope at the start
        private static readonly float SettleOmegaDamped = MathF.PI / SETTLE_DEEPEST_SECONDS;
        private static readonly float SettleOmega = SettleOmegaDamped / MathF.Sqrt(1f - SETTLE_DAMPING * SETTLE_DAMPING);
        private static readonly float SettleUndershoot = MathF.Exp(-SETTLE_DAMPING * MathF.PI / MathF.Sqrt(1f - SETTLE_DAMPING * SETTLE_DAMPING));

        //Where the settle comes to rest, chosen so that its undershoot bottoms out at LOW exactly
        private static readonly float Rest = (LOW + SettleUndershoot) / (1f + SettleUndershoot);

        //Where the settle has got to when the beat ends, which is where the next rise starts from: the curve is continuous
        //across the beat's wrap rather than jumping to a round number
        private static readonly float Start = Rest + (1f - Rest) * Settle(1f / HZ - TOP_SECONDS);

        /// <summary>
        /// The ghost's radius as a share of a whole ball's at <paramref name="seconds"/> on the wall clock: in
        /// <c>[LOW, 1]</c>, exactly 1 for a stretch of every beat and never above it.
        /// </summary>
        public static float Scale(float seconds)
        {
            float beats = seconds * HZ;
            float into = (beats - MathF.Floor(beats)) / HZ;

            float size = into < TOP_SECONDS
                ? Start + (1f - Start) * (1f - MathF.Exp(-RiseDecay * into) * MathF.Cos(RiseOmega * into))
                : Rest + (1f - Rest) * Settle(into - TOP_SECONDS);

            //THE CEILING, and the one place it is: the rise's spring wants past 1 and this is what turns that into a hold
            return MathF.Min(1f, size);
        }

        /// <summary>The settle's share of the way still to go, from 1 at the start to 0 at rest, through a negative undershoot.</summary>
        private static float Settle(float t) =>
            MathF.Exp(-SETTLE_DAMPING * SettleOmega * t)
            * (MathF.Cos(SettleOmegaDamped * t) + SETTLE_DAMPING * SettleOmega / SettleOmegaDamped * MathF.Sin(SettleOmegaDamped * t));
    }
}
