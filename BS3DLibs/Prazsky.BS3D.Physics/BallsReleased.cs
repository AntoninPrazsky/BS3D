namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// What one landed shot cut loose, split into the two kinds of ball — because the difference between them
    /// is the difference between a lucky shot and a good one, and only a scorer that can tell them apart can
    /// say so.
    /// <list type="bullet">
    /// <item><b>Matched</b> — the same-colour group the shot completed. The player aimed at these.</item>
    /// <item><b>Orphaned</b> — everything that fell afterwards because the group was its last anchor to the
    /// ceiling. The player aimed at the <i>support</i>, and these are what reading the cluster earns.</item>
    /// </list>
    /// <para>
    /// Orphans cannot happen without a match: the disconnected walk only runs once a group has actually been
    /// taken out, so <see cref="Orphaned"/> is zero whenever <see cref="Matched"/> is.
    /// </para>
    /// </summary>
    public readonly struct BallsReleased
    {
        public readonly int Matched;
        public readonly int Orphaned;

        public BallsReleased(int matched, int orphaned)
        {
            Matched = matched;
            Orphaned = orphaned;
        }

        /// <summary>Every ball that left the structure — what the old single-number return value reported.</summary>
        public int Total => Matched + Orphaned;

        /// <summary>Whether the shot completed a group at all. A landing that did not is a spent shot.</summary>
        public bool Any => Matched > 0;

        public override string ToString() => $"{Matched} matched, {Orphaned} orphaned";
    }
}
