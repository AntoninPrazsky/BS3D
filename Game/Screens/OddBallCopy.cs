using Prazsky.BS3D;
using Prazsky.BS3D.GameStructure;

namespace BS3D.Screens
{
    /// <summary>
    /// What Help's ODD BALLS tab says about each kind of ball that is not ordinary (#633), <b>keyed by the kind</b>, kept
    /// apart from the page so the list is one place and a test can ask whether every <see cref="BallKind"/> has an entry
    /// (<c>OddBallCopyTests</c>) — #584's deferred per-kind check, whose Help half this is. The page draws
    /// <see cref="Entries"/> and nothing else, each with the kind's own ball beside it (#856, <c>HelpPage</c>), so a kind
    /// that is missing here is missing from the page, and the test is what says so.
    /// <para>
    /// <b>Two groups, told apart by the kind and not by a flag.</b> The ten the campaign places in the cluster come first,
    /// in the order a player meets them (the five of the first chapters — glass 42, the Silhouettes' Umbrella, since #736;
    /// buckshot 93, bomb 105, zap 107, stone 136 — then the five no shipped level uses yet). The two the cannon holds, the
    /// rainbow ball (the wildcard) and the Cutter (the Cut's round, #895), come last under a line of their own
    /// (<see cref="GunLead"/>): they arrive in the gun rather than in the cluster, which is what
    /// <see cref="BallKinds.Shootable"/> says (<see cref="Entry.InGun"/>), so a shot-only kind added later joins the second
    /// group by itself and fails the test until it has its entry. <see cref="BallKind.Normal"/> needs none.
    /// </para>
    /// <para>
    /// The look clauses are deliberately short: the looks are being redrawn one kind at a time (#622), and a sentence
    /// describing a finish that has since changed would be wrong on the one page that explains it.
    /// </para>
    /// <para>
    /// The colour is what the live ball beside the entry is drawn in (#856): any for the kinds that have none of their own
    /// (the glass takes the first colour to land on it, the pellets, the bomb, the zap and the stone are drawn colourless),
    /// and a different one down the column for the kinds that keep theirs, so the column reads as a set rather than a row of
    /// red balls. The rainbow ball and the Cutter ignore it: the first shows whatever the page's own cycle is crossing
    /// between (<c>HelpPage</c> steps one), the second is steel.
    /// </para>
    /// </summary>
    internal static class OddBallCopy
    {
        /// <summary>One kind's line: the kind, the colour its ball is drawn in beside it, and what the page says of it.</summary>
        internal readonly record struct Entry(BallKind Kind, BallType Colour, string Text)
        {
            /// <summary>
            /// Whether the cannon holds this kind rather than the cluster (<see cref="BallKinds.Shootable"/>): drawn in
            /// its loaded pose, the way the round in the bore is — it does not breathe, and a Cutter's blade idles.
            /// </summary>
            internal bool InGun => BallKinds.Shootable(Kind);
        }

        /// <summary>The line that stands before the kinds the cannon holds, in the manner of the paragraph that said it before they had a square each.</summary>
        internal const string GunLead = "And two that arrive in your cannon rather than in the cluster.";

        /// <summary>Every kind but <see cref="BallKind.Normal"/>, the cluster's first and the cannon's after them.</summary>
        internal static readonly Entry[] Entries =
        {
            new(BallKind.Transparent, BallType.Type3, "Glass — clear, with no colour of its own yet. The first ball that lands against it colours it, "
                + "and from that moment it is an ordinary ball of that colour. It is a cell you get to choose."),
            new(BallKind.Buckshot, BallType.Type4, "Buckshot — a clump of grey pellets. No colour takes it and the level is not cleared while it "
                + "hangs: cut away whatever holds it up and it pours out."),
            new(BallKind.Bomb, BallType.Type8, "Bomb — dark, and it beats harder than the cluster around it. Set it off and it destroys its "
                + "neighbours outright, colour regardless."),
            new(BallKind.Zap, BallType.Type5, "Zap — dark and flickering, with hard bright arcs across it. It fires along a colour rather "
                + "than a shape."),
            new(BallKind.Rock, BallType.Type4, "Stone — colourless, and it cannot be matched. It never falls to a colour, only to losing "
                + "whatever was holding it up. A wall of stone is a wall you have to go around."),
            new(BallKind.Acid, BallType.Type2, "Acid — land a shot beside it and it eats straight down through the cluster beneath it until it "
                + "reaches a gap. Whatever it was holding up comes down with it."),
            new(BallKind.Frozen, BallType.Type1, "Frozen — a coloured ball sealed in a block of ice. Nothing matches it and no shot removes it "
                + "until you clear a group next to it. Then the ice breaks, and it is an ordinary ball of the colour you "
                + "could see all along."),
            new(BallKind.Infectious, BallType.Type7, "Infectious — an ordinary ball of its colour, and sick. Every shot you fire, it passes the "
                + "sickness to one healthy neighbour, the highest one, and hardens into stone itself. Match it away early: "
                + "the stone it leaves is permanent."),
            new(BallKind.Gravity, BallType.Type6, "Gravity — an ordinary ball that pulls on shots passing near it and bends their flight. The "
                + "aim preview shows the bend."),
            new(BallKind.Heavy, BallType.Type3, "Heavy — an ordinary ball many times heavier than the rest. Whatever hangs from it sits lower in "
                + "the cluster, nearer the line."),

            //The cannon's two (#895). The rainbow ball's sentences are the paragraph that stood here before it had a square,
            //less its lead (which is GunLead's now); the Cutter's are the Controls tab's line for the Cut, less its key
            new(BallKind.Wildcard, BallType.Type1, "Rainbow ball — a ball that never settles on a colour while it is loaded. It becomes whatever "
                + "completes a group where it lands. A few of the later levels hand them out, and a card says so the first "
                + "time one is loaded."),
            new(BallKind.Cutter, BallType.Type1, "Cutter — what the Cut turns your next ball into. It destroys the whole storey it hits, so what "
                + "hung on it falls; what it would cut off lights up as you aim, never more than the lower half; one a "
                + "level, from the fifth chapter."),
        };
    }
}
