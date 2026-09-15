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
    /// <b>⚠ The line between the last two is drawn by what a ball was ALREADY DOING when the blast reached it</b>
    /// (#396), and it has to be, now that a bomb can be set off by losing its last path to the glass rather
    /// than by being aimed at. A ball the disconnection walk had already found hanging on nothing left because
    /// its support was cut — the blast only chose which way it flew — so it stays <see cref="Orphaned"/>, at
    /// the double rate, even though a blast is what physically took it. Only what a blast takes from cluster
    /// that was still STANDING is <see cref="Destroyed"/>.
    /// </para>
    /// <para>
    /// The alternative was tried on paper and refused: counting the whole radius as destroyed makes an
    /// orphan-triggered blast pay the player <i>less</i> than the same shot with no bomb in it, because the
    /// balls it scatters were already earning the orphan's double. A rule under which the better-looking
    /// outcome is worth less is not a scoring rule. So the invariant is stated rather than left to emerge:
    /// <b>an orphan-triggered blast can only ever ADD to what a shot is worth.</b>
    /// </para>
    /// <para>
    /// <b>Orphans cannot happen without a match OR a blast.</b> The disconnected walk only runs once something
    /// has actually been taken out, so <see cref="Orphaned"/> is zero whenever both of the others are — and it
    /// is the one field of the three that two different causes contribute to, which is exactly why it is kept
    /// separate from both. Since #396 it is three causes: a blast that reaches balls the walk had already
    /// found also lands here, by the rule above.
    /// </para>
    /// <para>
    /// <b>⚠ <c>Destroyed &gt; 0</c> is NOT a test for "a bomb went off" any more</b> (#396), and it was one for
    /// long enough to be worth saying. An orphan-triggered blast inside a region that was already falling
    /// destroys nothing at all, so a bomb can explode with all three of these numbers unchanged from what a
    /// plain match would have given. What answers that question is <c>BallLanding.Detonated</c>.
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
