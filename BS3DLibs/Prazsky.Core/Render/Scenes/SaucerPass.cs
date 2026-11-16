using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The saucer caught close by a lens (#1117): a pass the space chapter's intro cues on every frame of its still shot of the
    /// planet (<see cref="SceneRenderer.CueSpaceSaucer"/>, its moment counted on the intro's own clock), so the craft crosses
    /// that frame near the lens at a moment the shot is laid round instead of being hoped for. The owner asked for "an
    /// unexpected flying saucer flying through this static shot, relatively close to the camera".
    /// <para>
    /// <b>Two parts, and only the first outlives the pass.</b> From the cue on, the craft's loop is turned on by
    /// <see cref="LoopShift"/>, so the loop itself brings it to the bearing of the pass's <see cref="Point"/> at the pass's
    /// moment (<see cref="Closest"/>): the loop keeps that phase after, the same loop at the same pace, which is what lets the
    /// craft rejoin it beside the lens rather than from the far side of the station. And for <see cref="HOLD_SECONDS"/> either
    /// side of the moment the craft leaves the loop for a straight run through the point along the loop's own way round, level,
    /// at <see cref="SPEED"/>, leaning its outer side (the lens's side, the lens standing outside the loop) down by
    /// <see cref="LEAN"/> so the dome and its crew face the lens; it eases off the loop onto the run and back onto the loop over
    /// <see cref="EASE_SECONDS"/> at each end (<see cref="Weight"/>), where the shot that cued it does not see it. A cue less
    /// than <see cref="HOLD_SECONDS"/> before the moment, as the intro's is, puts the craft straight onto the run: the cut into
    /// the shot hides the jump from wherever the loop had it, and only the ease back is flown.
    /// </para>
    /// <para>
    /// A pure function of the wall clock and the cue, as the loop is: <c>default</c> is no pass, under which
    /// <see cref="SaucerFlight.WorldAt(float, in SaucerPass)"/> is the loop exactly as it flies without one.
    /// </para>
    /// </summary>
    internal readonly struct SaucerPass
    {
        /// <summary>The run's pace, units a second: a little brisker than the loop's (<see cref="SaucerFlight.LOOP_SPEED"/>).</summary>
        internal const float SPEED = 28f;

        /// <summary>
        /// How long the craft holds the run either side of its moment: the whole of the intro's still shot round it
        /// (<c>SpaceIntroShots</c>, its moment and its length), with a little to spare before the shot comes on screen and most
        /// of a second after it cuts away, so the shot sees the craft only on the run and never easing on or off it.
        /// <c>SpaceIntroShotsTests</c> holds the two together.
        /// </summary>
        internal const float HOLD_SECONDS = 2.6f;

        /// <summary>How long the craft takes to leave the loop for the run, and to ease back onto it after.</summary>
        internal const float EASE_SECONDS = 2f;

        /// <summary>
        /// The bank the run eases to, radians about the craft's heading: negative tips its outer side down, where the loop's own
        /// <see cref="SaucerFlight.BANK"/> tips it up and shows a lens outside the loop only its belly.
        /// </summary>
        internal const float LEAN = -0.32f;

        /// <summary>Whether there is a pass at all; <c>default</c> is none.</summary>
        public readonly bool Cued;

        /// <summary>The wall-clock moment the craft is at <see cref="Point"/>.</summary>
        public readonly float Closest;

        /// <summary>Where the craft's middle is at <see cref="Closest"/>.</summary>
        public readonly Vector3 Point;

        /// <summary>The run's direction, level: the loop's way round at the point's bearing.</summary>
        public readonly Vector3 Heading;

        /// <summary>The turn added to the loop's angle from the cue on, radians, so the loop is at the point's bearing at its moment.</summary>
        public readonly float LoopShift;

        private SaucerPass(float closest, Vector3 point, Vector3 heading, float loopShift)
        {
            Cued = true;
            Closest = closest;
            Point = point;
            Heading = heading;
            LoopShift = loopShift;
        }

        /// <summary>The pass that has the craft's middle at <paramref name="point"/> at the wall-clock moment <paramref name="closest"/>.</summary>
        public static SaucerPass Through(Vector3 point, float closest)
        {
            float bearing = MathF.Atan2(point.Z, point.X);
            float shift = MathHelper.WrapAngle(bearing - SaucerFlight.LoopAngle(closest, 0f));
            return new SaucerPass(closest, point, new Vector3(-MathF.Sin(bearing), 0f, MathF.Cos(bearing)), shift);
        }

        /// <summary>
        /// How far the craft is on the run rather than on its loop at <paramref name="time"/>, 0 to 1: 1 through
        /// <see cref="HOLD_SECONDS"/> either side of the moment, eased to 0 over <see cref="EASE_SECONDS"/> beyond, and 0 with
        /// no pass.
        /// </summary>
        public float Weight(float time)
        {
            if (!Cued) return 0f;

            float past = MathF.Abs(time - Closest) - HOLD_SECONDS;
            if (past <= 0f) return 1f;
            if (past >= EASE_SECONDS) return 0f;

            float s = 1f - past / EASE_SECONDS;
            return s * s * (3f - 2f * s);
        }

        /// <summary>Where the run has the craft's middle at <paramref name="time"/>.</summary>
        public Vector3 RunAt(float time) => Point + Heading * (SPEED * (time - Closest));
    }
}
