namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// What one landed shot cut loose, split into the kinds of ball — because the difference between them is
    /// the difference between a lucky shot and a good one, and only a scorer that can tell them apart can say
    /// so.
    /// <list type="bullet">
    /// <item><b>Matched</b> — the same-colour group the shot completed. The player aimed at these.</item>
    /// <item><b>Orphaned</b> — everything that fell afterwards because the group was its last anchor to the
    /// ceiling. The player aimed at the <i>support</i>, and these are what reading the cluster earns.</item>
    /// <item><b>Destroyed</b> — everything a blast took out by geometry (#326): the bombs this landing set off,
    /// and every ball inside their radius whatever its colour or kind. The player aimed at the <i>bomb</i>.
    /// </item>
    /// </list>
    /// <para>
    /// <b>Orphans cannot happen without a match OR a blast.</b> The disconnected walk only runs once something
    /// has actually been taken out, so <see cref="Orphaned"/> is zero whenever both of the others are — and it
    /// is the one field of the three that two different causes contribute to, which is exactly why it is kept
    /// separate from both.
    /// </para>
    /// <para>
    /// <b>Why the blast is a third category and not folded into one of the two</b> (#326's own instruction to
    /// decide this deliberately, since #327 and #328 inherit whatever it decides). Folding it into
    /// <see cref="Matched"/> would say the player completed a group they did not; folding it into
    /// <see cref="Orphaned"/> would pay it at the orphan's DOUBLE rate, and the orphan rate exists to reward
    /// the one shot in this game that has to be read for rather than aimed at — cutting a support — which a
    /// blast is the opposite of. It is its own thing, so it is its own number.
    /// </para>
    /// </summary>
    public readonly struct BallsReleased
    {
        public readonly int Matched;
        public readonly int Orphaned;

        /// <inheritdoc cref="BallsReleased"/>
        public readonly int Destroyed;

        public BallsReleased(int matched, int orphaned, int destroyed = 0)
        {
            Matched = matched;
            Orphaned = orphaned;
            Destroyed = destroyed;
        }

        /// <summary>Every ball that left the structure — what the old single-number return value reported.</summary>
        public int Total => Matched + Orphaned + Destroyed;

        /// <summary>
        /// Whether the shot <b>did</b> something — the question the streak is decided on, and since #326 it is
        /// no longer the same question as "did it complete a group".
        /// <para>
        /// A shot that detonates a bomb and completes no group at all still took a piece out of the cluster
        /// deliberately, so it is a landing and not a spent shot. Reading only <see cref="Matched"/> here would
        /// have broken the streak on the one shot the bomb exists to make worth aiming.
        /// </para>
        /// </summary>
        public bool Any => Matched > 0 || Destroyed > 0;

        /// <summary>
        /// The two halves of one landing added together — a group release and a blast that followed it in the
        /// same shot (#326). The orphan counts sum like the rest: each pass ran its own disconnection walk over
        /// the field as it stood, so no ball is counted twice.
        /// </summary>
        public BallsReleased Plus(in BallsReleased other) =>
            new(Matched + other.Matched, Orphaned + other.Orphaned, Destroyed + other.Destroyed);

        public override string ToString() =>
            $"{Matched} matched, {Orphaned} orphaned" + (Destroyed > 0 ? $", {Destroyed} destroyed" : string.Empty);
    }
}
