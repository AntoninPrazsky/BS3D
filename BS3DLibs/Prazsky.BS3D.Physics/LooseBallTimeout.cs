using Prazsky.BS3D.GameStructure;
using System;

namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// How long a ball the cluster has let go of may stay up in the field, and its going (#1191): a released ball still
    /// above the field's floor <see cref="FIELD_SECONDS"/> after its release, and not falling out of it
    /// (<see cref="FALLING_SPEED"/>), dissolves the whole way over <see cref="VANISH_SECONDS"/> in the dither the dead
    /// weight is drawn with, and is retired.
    /// <para>
    /// <b>Why it exists.</b> A released ball is loose: a shot passes through it (#410) and nothing can attach to it, so one
    /// that wedges up among the balls instead of falling hangs in the player's way as a ball that cannot be hit. The dither
    /// already says so (#620), and the owner's QA notes asked for the rest: such balls confused players, so after a few
    /// seconds they go. The kill plane alone would never take one: a ball resting on something never reaches it.
    /// </para>
    /// <para>
    /// <b>Only in the field.</b> A ball below the field's floor is on its way out, through the drain or over the island's
    /// edge, and its fall is the drop the cinematic films (#616) and the player watches, so it is left its natural end. That
    /// end can be long: the funnel's hole lets a big drop through a few balls at a time, and measured on Pennant's clearing
    /// shot (232 balls) the pile took up to 13 s to drain to the kill plane, the last balls still falling at the hole. A
    /// timeout on age alone below the field dissolved 52 of them in mid-fall, so there is none. The floor is the one the
    /// dead weight is marked above (<see cref="ClusterCollector"/>'s <c>deadWeightAboveY</c>), so a ball that wears the
    /// mark is a ball this takes; the Game reads it off <see cref="Levels.ClusterHang.FieldFloorY"/>, which follows the
    /// glass down and stops at the death line.
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
        /// How fast a ball in the field past its time has to be moving, in world units a second, for its going to wait: one
        /// knocked off a ledge late, or the last of a big drop rolling off the cluster, is falling out of the field and is
        /// left its fall (the issue's warning: the last balls of a drop must not dissolve just before they reach the
        /// funnel). A free fall from rest passes it in a fifth of a second; a ball lying on a cluster the wind sways or a
        /// landing shakes moves far slower (the stuck balls measured on Koch rested at 0.01). A deferred ball that stops
        /// again in the field goes at once, and one that leaves it is the drain's.
        /// </summary>
        public const float FALLING_SPEED = 2f;

        /// <summary>
        /// How long the last dissolve takes, in seconds: long enough to be seen happening rather than to read as a pop
        /// (a ball vanishing in shot reads as a bug), short enough to be over before the next shot lands. About twice the
        /// dead weight's own ease (<see cref="ClusterCollector.DEAD_WEIGHT_FADE_SECONDS"/>), since it carries the dither the
        /// whole way rather than half of it.
        /// </summary>
        public const float VANISH_SECONDS = 1f;

        /// <summary>
        /// The Game's whole cull of one ball for one frame (<c>GameplayScreen.RemoveFallenBalls</c>), here so the tests run
        /// the very decision the Game makes: any ball under <paramref name="killPlaneY"/> goes (down the drain or over the
        /// island's edge); among the <paramref name="released"/> ones a cutter goes at once, spent on the step it struck
        /// (#823), and every other is aged by <see cref="Advance"/> and goes once its dissolve has run. A shot is never aged:
        /// it is no released ball, and a cutter among the shots is still flying. Asked in that order, so every released ball
        /// that stays is aged exactly once a frame and one that is leaving anyway is not.
        /// </summary>
        /// <param name="ball">A ball the Game holds, a shot or a released one.</param>
        /// <param name="released">Whether it is among the released balls rather than the shots.</param>
        /// <param name="killPlaneY">The world Y under which every ball has left the game.</param>
        /// <param name="seconds">What the world lived through this frame, as <see cref="Advance"/> takes it.</param>
        /// <param name="fieldFloorY">The field's floor in world Y, as <see cref="Advance"/> takes it.</param>
        public static BallExit Cull(PhysicsBall ball, bool released, float killPlaneY, float seconds, float fieldFloorY)
        {
            if (ball.BallReference.Pose.Position.Y < killPlaneY) return BallExit.KillPlane;
            if (!released) return BallExit.Stays;
            if (ball.Kind == BallKind.Cutter) return BallExit.SpentCutter;

            return Advance(ball, seconds, fieldFloorY) ? BallExit.Dissolved : BallExit.Stays;
        }

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
            if (ball.Vanish <= 0f && !IsDue(ball.LooseSeconds, ball.BallReference.Pose.Position.Y, fieldFloorY,
                ball.BallReference.Velocity.Linear.Length())) return false;

            ball.Vanish = MathF.Min(1f, ball.Vanish + seconds / VANISH_SECONDS);
            return ball.Vanish >= 1f;
        }

        /// <summary>
        /// Whether a released ball of this age, standing at <paramref name="y"/> and moving at <paramref name="speed"/>, has
        /// outstayed its time: past <see cref="FIELD_SECONDS"/>, still above the field's floor, and not falling out of it
        /// (<see cref="FALLING_SPEED"/>).
        /// </summary>
        public static bool IsDue(float looseSeconds, float y, float fieldFloorY, float speed) =>
            looseSeconds >= FIELD_SECONDS && y > fieldFloorY && speed < FALLING_SPEED;
    }

    /// <summary>What a ball the Game holds leaves the game by this frame, if anything (<see cref="LooseBallTimeout.Cull"/>).</summary>
    public enum BallExit : byte
    {
        /// <summary>It stays.</summary>
        Stays,

        /// <summary>Under the kill plane: down the drain or over the island's edge.</summary>
        KillPlane,

        /// <summary>A cutter among the released balls, spent on the step it struck (#823).</summary>
        SpentCutter,

        /// <summary>A released ball that outstayed its time in the field and has dissolved the whole way (#1191).</summary>
        Dissolved,
    }
}
