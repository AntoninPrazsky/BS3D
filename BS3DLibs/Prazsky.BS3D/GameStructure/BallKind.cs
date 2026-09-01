using System;

namespace Prazsky.BS3D.GameStructure
{
    /// <summary>
    /// What a ball <b>is</b>, beside what colour it is (#323): the second axis the special ball types of #256
    /// are laid out on. <see cref="BallType"/> stays the thirteen colours and nothing else.
    /// <para>
    /// <b>A special is not a fourteenth colour, and that decision is the whole reason this enum exists.</b>
    /// A <c>Type14 = Rock</c> would have been the cheap route and it breaks six things at once, none of them
    /// loudly: <see cref="BallTypes.Count"/> is derived from the enum precisely so a new member is never
    /// forgotten, and it sizes <c>BallRenderSet</c>'s instance buckets; <c>BasicEffectParamsProvider</c> owes
    /// every member a tint; the Game's colour census would count rocks; <c>RandomBallType</c> would
    /// <i>load</i> them into the magazine; <c>Transmute</c> would re-colour balls to them; and the generator's
    /// thirteen inks are a design rule, not an implementation detail. Most specials need a colour of their own
    /// anyway — a frozen ball is a coloured ball inside ice — so the two axes are orthogonal by nature.
    /// </para>
    /// <para>
    /// A kind travels on the <see cref="StaticBall"/>, is mirrored onto <c>PhysicsBall</c> for the walks that
    /// only see the physics array, and is serialized as the map format's optional <c>"k"</c> key — absent
    /// meaning <see cref="Normal"/>, so no level authored before this existed had to be rewritten and none was
    /// (the #258 precedent, taken for the same reason).
    /// </para>
    /// </summary>
    public enum BallKind : byte
    {
        /// <summary>
        /// An ordinary ball of its colour: it matches, it is counted, it can be shot down. What every cell of
        /// every level authored before #323 is, and what a map that says nothing gets.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Stone (#324). It matches with nothing and no colour removes it; it leaves the cluster only as
        /// collateral, when a match elsewhere cuts the last thing holding it up — which needs no code of its
        /// own, because <c>BallsMap.GetCellsDisconnectedFromCeiling</c> has never cared what kind a ball is.
        /// <para>
        /// A rock is a <b>wall</b>: it changes the shape of the problem rather than the colour arithmetic. It
        /// still carries a <see cref="BallType"/> because every cell does, and <b>nothing may read it</b> —
        /// see <see cref="Matchable"/>.
        /// </para>
        /// </summary>
        Rock = 1,

        /// <summary>
        /// Colourless glass (#325). It hangs with no colour at all and takes one the moment a shot lands
        /// beside it — the shot's own — and from that instant it is an ordinary ball of that colour: it
        /// matches, it is counted, it scores. Nothing about the flood fill, the release or the removal path
        /// changes for it, which is why it is the cheapest of #256's ten and why it comes early.
        /// <para>
        /// It is the kind that <b>stops being one</b>, and that is what makes it the first case where
        /// <see cref="Matchable"/> and <see cref="Removable"/> have to give different answers — see the two of
        /// them. It still carries a <see cref="BallType"/> like every cell, and like the rock's <b>nothing may
        /// read it</b> until the colouring: what it is authored with is the colour it would have had, kept
        /// only so that the field is not a special case for the serializer, and the colouring overwrites it.
        /// </para>
        /// </summary>
        Transparent = 2
    }

    /// <summary>
    /// What a <see cref="BallKind"/> means to the rules, in one place. The three seams of #323 are questions
    /// asked of a kind, and this is where they are answered so the flood fill, the census and the two end-of-
    /// level conditions cannot each grow their own opinion.
    /// </summary>
    public static class BallKinds
    {
        /// <summary>
        /// Whether this ball takes part in the colour match rule <b>as the colour it is wearing</b> — the first
        /// of #323's three seams.
        /// <para>
        /// It answers <b>two</b> questions and they have to stay one answer:
        /// <c>BallsMap.GetConnectedSameTypeCells</c> asks it to decide whether a ball joins the group a shot
        /// completed, and the Game's colour census asks it to decide whether this ball's colour is still worth
        /// loading into the magazine. A ball that cannot be matched but whose colour is counted alive costs the
        /// player shots on a colour nothing can clear — which is the exact cost <c>Transmute</c> exists to
        /// prevent. A <see cref="BallKind.Transparent"/> ball is out of both for the same reason from the other
        /// side: the colour it carries is not one the player can see or aim at yet.
        /// </para>
        /// <para>
        /// ⚠ It answered a <b>third</b> question until #325 — whether the level is finished — and that is now
        /// <see cref="Removable"/>. They agreed on everything #323 had (a rock is neither matchable nor
        /// removable) and the transparent ball is the first kind that splits them: it cannot be matched and it
        /// most certainly does not hold the level open for ever. Merging them again would make a level with one
        /// transparent ball in it permanently unfinished — the Rock's own bug, arriving through the opposite
        /// door.
        /// </para>
        /// </summary>
        public static bool Matchable(BallKind kind) => kind == BallKind.Normal;

        /// <summary>
        /// Whether a shot can still do something about this ball — <b>the question the end of a level is
        /// decided on</b> (#323's second seam, split out of <see cref="Matchable"/> by #325).
        /// <para>
        /// A <see cref="BallKind.Transparent"/> ball answers <b>yes</b> even though nothing matches it as it
        /// stands, and that is the ruling rather than an oversight: the player always has a path to removing it
        /// — land a shot beside it, and it becomes an ordinary ball of that colour which the very same landing
        /// can match. A ball one shot away from being ordinary is not a ball that ends the level, and counting
        /// it as one would hand every transparent level the Rock's two opposite failures at once (never
        /// cleared, always lost).
        /// </para>
        /// <para>
        /// A <see cref="BallKind.Rock"/> answers no: no shot the player can aim makes a rock removable, and it
        /// leaves only as collateral.
        /// </para>
        /// </summary>
        public static bool Removable(BallKind kind) => kind != BallKind.Rock;

        /// <summary>
        /// The spellings a kind answers to on a command line or in a hand-edited file. Lenient in the same way
        /// and for the same reason <see cref="BallStyles.TryParse"/> and <c>SceneRenderer.TryParseScene</c>
        /// are: an unknown spelling comes back false and the caller keeps its default.
        /// </summary>
        public static bool TryParse(string name, out BallKind kind)
        {
            kind = BallKind.Normal;

            if (string.IsNullOrWhiteSpace(name)) return false;

            switch (name.Trim().ToLowerInvariant())
            {
                case "normal":
                case "ball":
                case "none":
                    kind = BallKind.Normal;
                    return true;

                case "rock":
                case "stone":
                    kind = BallKind.Rock;
                    return true;

                case "transparent":
                case "clear":
                case "glass":
                    kind = BallKind.Transparent;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// The spelling a kind is written back out as — the enum's own name, lowercased, which is one of the
        /// keys <see cref="TryParse"/> takes.
        /// </summary>
        public static string ToName(BallKind kind) => kind.ToString().ToLowerInvariant();

        /// <summary>The next kind in the enum, wrapping — what a cycling key in an authoring tool wants.</summary>
        public static BallKind Next(BallKind kind)
        {
            BallKind[] all = Enum.GetValues<BallKind>();

            return all[(Array.IndexOf(all, kind) + 1) % all.Length];
        }
    }
}
