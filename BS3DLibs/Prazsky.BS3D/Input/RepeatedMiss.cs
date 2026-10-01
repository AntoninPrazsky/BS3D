using Microsoft.Xna.Framework;
using System;

namespace Prazsky.BS3D.Input
{
    /// <summary>
    /// <b>The impossible shot's trigger</b> — another of #230's easter eggs: <see cref="SHOTS"/> shots in a row fired
    /// from the same place along the same line, every one of them flying clean out past the island, open a small
    /// wormhole in the air that swallows them. The session reports each shot that <b>escapes</b> (leaves the arena
    /// still undecided, so nothing can be in its way any more) with the pose it was fired from, and anything else a
    /// shot can do — land, strike the stone — breaks the run.
    /// <para>
    /// "The same pose" is held against the <b>first</b> shot of the run, not the one before: compared pairwise, a
    /// player turning the barrel a degree between shots would walk a run of misses round the whole sky and still
    /// count as aiming at one spot. Within <see cref="ANGLE_TOLERANCE_DEGREES"/> and <see cref="MUZZLE_TOLERANCE"/> is
    /// a hand that did not touch the mouse between clicks — or that put it back on purpose, which is the point.
    /// </para>
    /// <para>
    /// Like every other egg of #230 it decides nothing: the shots it reports are misses either way, and what the
    /// Game does with a completed run is drawn and heard, never scored.
    /// </para>
    /// </summary>
    public sealed class RepeatedMiss
    {
        /// <summary>How many escaped shots in a row from one pose complete the run.</summary>
        public const int SHOTS = 3;

        /// <summary>How far a shot's line may stand off the first one's and still be "the same angle", in degrees.</summary>
        public const float ANGLE_TOLERANCE_DEGREES = 1.5f;

        /// <summary>How far the muzzle may stand off the first shot's and still be "the same place", in world units.</summary>
        public const float MUZZLE_TOLERANCE = 0.5f;

        private static readonly float CosTolerance = MathF.Cos(MathHelper.ToRadians(ANGLE_TOLERANCE_DEGREES));

        private Vector3 _muzzle, _direction;
        private int _count;

        /// <summary>How many shots of the current run have escaped so far.</summary>
        public int Count => _count;

        /// <summary>Forget the run — a level was built.</summary>
        public void Reset() => _count = 0;

        /// <summary>A shot did something other than escape: it landed, or it struck the stone. The run is over.</summary>
        public void Break() => _count = 0;

        /// <summary>
        /// A shot fired from <paramref name="muzzle"/> along <paramref name="direction"/> (unit length) has escaped
        /// the arena. True when it completes a run, which then starts again from nothing, so one run cannot fire twice.
        /// </summary>
        public bool Escaped(Vector3 muzzle, Vector3 direction)
        {
            if (_count > 0 && SamePose(_muzzle, _direction, muzzle, direction)) _count++;
            else
            {
                //A different pose is not a broken run but the first shot of a new one
                _count = 1;
                _muzzle = muzzle;
                _direction = direction;
            }

            if (_count < SHOTS) return false;

            _count = 0;
            return true;
        }

        /// <summary>Whether two shots were fired from the same place along the same line, within the tolerances.</summary>
        public static bool SamePose(Vector3 muzzleA, Vector3 directionA, Vector3 muzzleB, Vector3 directionB) =>
            Vector3.DistanceSquared(muzzleA, muzzleB) <= MUZZLE_TOLERANCE * MUZZLE_TOLERANCE
            && Vector3.Dot(directionA, directionB) >= CosTolerance;
    }
}
