using System;

namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// How long a ball the cluster has let go of may stay, and its going (#1191): a released ball still up in the field
    /// <see cref="FIELD_SECONDS"/> after its release, or anywhere <see cref="BACKSTOP_SECONDS"/> after it, dissolves the
    /// whole way over <see cref="VANISH_SECONDS"/> in the dither the dead weight is drawn with, and is retired.
    /// <para>
    /// <b>Why it exists.</b> A released ball is loose: a shot passes through it (#410) and nothing can attach to it, so one
    /// that wedges up among the balls instead of falling hangs in the player's way as a ball that cannot be hit. The dither
    /// already says so (#620), and the owner's QA notes asked for the rest: such balls confused players, so after a few
    /// seconds they go. Only the kill plane removed them until then, and a ball resting on something never reached it.
    /// </para>
    /// <para>
    /// <b>Two figures, because two kinds of ball outstay.</b> One wedged in the field, above the field's floor, is the one
    /// in the way, and it goes after the owner's figure. One below the floor is on its way out through the drain or over
    /// the island's edge, and its fall is the drop the cinematic films (#616) and the player watches: it is left its
    /// natural end, and only the backstop, far past any measured fall, takes a ball that wedged in the funnel or on the
    /// stone. The floor is the one the dead weight is marked above (<see cref="ClusterCollector"/>'s
    /// <c>deadWeightAboveY</c>), so a ball that wears the mark is a ball this takes.
    /// </para>
    /// <para>
    /// <b>The world's clock</b>, not the draw's: the age is what the simulation lived through (a pause holds it, a capped
    /// frame counts only the steps it took), because the timeout ends a body and the body lives on that clock.
    /// </para>
    /// <para>
    /// <b>Its going counts nothing</b>: what a released ball is worth was settled when the landing released it
    /// (<c>ScoreKeeper.Landed</c>), and retiring it is only <c>PhysicsWorld.RetireBall</c>, the door the kill plane uses.
    /// </para>
    /// </summary>
    public static class LooseBallTimeout
    {
        /// <summary>
        /// How long after its release a ball still in the field starts to dissolve, in seconds: the owner's figure ("say 5
        /// seconds"). A released ball that falls clears the field in well under a second, so what is still above the floor
        /// at this age has stopped there; the dead weight's mark (<c>ClusterCollector.DEAD_WEIGHT_HOLD_SECONDS</c> and its
        /// fade) is on it by then, and the tutorial's card that explains the mark (#1023) has had its chance to fire.
        /// </summary>
        public const float FIELD_SECONDS = 5f;

        /// <summary>
        /// How long after its release a ball <b>anywhere</b> starts to dissolve, in seconds: the catch for one that wedged
        /// below the field, in the funnel or on the stone, which the kill plane would never take. Far past a released
        /// group's natural fall to the kill plane and past the drop cinematic's own backstop (6.5 s), so it never cuts a
        /// fall short.
        /// </summary>
        public const float BACKSTOP_SECONDS = 12f;

        /// <summary>
        /// How long the last dissolve takes, in seconds: long enough to be seen happening rather than to read as a pop
        /// (a ball vanishing in shot reads as a bug), short enough to be over before the next shot lands. Twice the dead
        /// weight's own ease (<c>ClusterCollector.DEAD_WEIGHT_FADE_SECONDS</c>), since it carries the dither the whole way.
        /// </summary>
        public const float VANISH_SECONDS = 1f;

        /// <summary>
        /// Advances one released ball's clock by <paramref name="seconds"/> of the world's time and its last dissolve
        /// once it is due, and answers whether it has gone the whole way and must be retired now. Called once a frame for
        /// every ball the Game holds as released, and only for those: visiting a ball twice would age it at double speed.
        /// </summary>
        /// <param name="ball">A released ball. An <see cref="PhysicsBall.Untimed"/> one is left alone and never goes.</param>
        /// <param name="seconds">What the world lived through since the last call: the frame's whole steps, zero under a
        /// pause.</param>
        /// <param name="fieldFloorY">The field's floor in world Y, the height above which a ball is in the field.</param>
        public static bool Advance(PhysicsBall ball, float seconds, float fieldFloorY)
        {
            if (ball.Untimed) return false;

            ball.LooseSeconds += seconds;

            //Once begun it runs to the end wherever the ball goes: a half-dissolved ball that rolled off the floor and came
            //whole again would read as a glitch, and so would one that stopped half-way
            if (ball.Vanish <= 0f && !IsDue(ball.LooseSeconds, ball.BallReference.Pose.Position.Y, fieldFloorY)) return false;

            ball.Vanish = MathF.Min(1f, ball.Vanish + seconds / VANISH_SECONDS);
            return ball.Vanish >= 1f;
        }

        /// <summary>
        /// Whether a released ball of this age, standing at <paramref name="y"/>, has outstayed its time: past
        /// <see cref="FIELD_SECONDS"/> above the field's floor, or past <see cref="BACKSTOP_SECONDS"/> anywhere.
        /// </summary>
        public static bool IsDue(float looseSeconds, float y, float fieldFloorY) =>
            looseSeconds >= BACKSTOP_SECONDS || (looseSeconds >= FIELD_SECONDS && y > fieldFloorY);
    }
}
