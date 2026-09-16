using Microsoft.Xna.Framework;

namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// One bomb going off (#389): where it was, how far down a chain it came, and how much it took with it.
    /// <para>
    /// A blast had no record of its own until #389. <see cref="BallsReleased.Destroyed"/> is shared with the zap
    /// and the acid, and a landing's <see cref="BallLanding.World"/> is the cell the <i>shot</i> stuck to, which
    /// is beside a bomb and never the bomb itself — so nothing downstream could put a flash, a sound or a jolt
    /// where a blast actually happened, and a chain of five read exactly like a single bomb. This is that record,
    /// one per detonation, in the order the chain reached them.
    /// </para>
    /// </summary>
    public readonly struct Detonation
    {
        /// <summary>
        /// Where the bomb's <b>body</b> was the instant it went off, in world space — the point its victims were
        /// thrown from, and so the one point a flash and a report may be centred on. World and not the lattice
        /// frame on purpose: see <c>BallsConstraintsBuilder.BLAST_SPEED</c> for what mixing the two cost.
        /// </summary>
        public readonly Vector3 World;

        /// <summary>
        /// How many blasts it took to reach this bomb: 0 for one the landing set off itself, 1 for one caught in
        /// such a blast, and so on. Two bombs beside the same landing both read 0 — they were set off by the same
        /// event, and a chain is what reaches a bomb <i>through</i> a blast.
        /// </summary>
        public readonly int Link;

        /// <summary>Balls this detonation took out by geometry, the bomb itself included.</summary>
        public readonly int Destroyed;

        public Detonation(Vector3 world, int link, int destroyed)
        {
            World = world;
            Link = link;
            Destroyed = destroyed;
        }
    }
}
