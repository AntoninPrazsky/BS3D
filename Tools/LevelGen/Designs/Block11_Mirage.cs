using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>The Mirage</b>, block 11 of the campaign: its designs, and the helpers no other block's designs use, in
    /// the order <c>Program.cs</c> held them — the play order is <see cref="Main"/>'s, and the block's name, music
    /// and ball style are in the tables there. Split out of <c>Program.cs</c> in #386.
    /// </summary>
    internal static partial class Program
    {

        #region The mirage levels (#323/#325)

        //THE ELEVENTH BLOCK'S FIVE GLASS LEVELS, and the one law all five obey:
        //
        //  ⚠ GLASS GOES ON THE SKIN. Never buried, never on the anchor course.
        //
        //Both halves are load-bearing and neither is taste. A transparent ball is coloured by a shot landing
        //in a cell BESIDE it (BallsMap.ColourTransparentNeighbours takes every transparent neighbour of the
        //landing cell), so a glass ball with no empty neighbour is a ball the player can never colour - and
        //glass counts against the level being cleared, so that is a level that cannot be finished while
        //every other gate in this file passes it. FindStrandedSpecials refuses it. And glass on the field's top
        //level is a CEILING ANCHOR THAT DISSOLVES: one shot beside it and the cluster's hanging width drops,
        //which is #301/#302's failure mode with the one ingredient those issues did not have, namely that
        //the ball gave no warning because it had no colour to warn with. Refused as well.
        //
        //WHAT MAKES A GLASS BALL WORTH PLACING is the arithmetic of one landing. A shot into an empty cell
        //colours EVERY transparent neighbour of that cell at once and only then counts the group, so a
        //pocket with one glass ball in it pays the shot plus that ball - two, which is under the three a
        //match needs and drops nothing by itself - and a pocket with two pays three and clears on the spot.
        //Every design below is drawn so that its glass stands in twos along a surface a shot can reach: the
        //validator prints the deepest pocket it can find, and that number is the design's payoff rather
        //than a diagnostic.
        //
        //AND THE ONE THING GLASS MAY DO THAT COLOUR MAY NOT: stand one cell thick on a diagonal. Cells on
        //one level touch only their four ORTHOGONAL neighbours, so a colour band one cell wide running
        //diagonally is a string of balls that do not touch each other - the defect FindLonelyBalls exists
        //for and the one the first Gem shipped 28 of. Glass has no colour and therefore no group, so the
        //rule does not reach it: a single-cell diagonal arris of glass is legal, and three of the five
        //levels below are built on exactly that. It is what lets a gem be outlined in clear glass along
        //edges no colour could have followed.

        /// <summary>
        /// <b>The block's opener, and the level that teaches what the glass IS.</b> A terraced octahedron —
        /// <see cref="Gem"/>'s own solid, four facet steps of taxicab rings — with the outermost ring of
        /// every second course drawn in clear glass, so the stone is a coloured gem wearing a clear rim
        /// round each of its terraces.
        /// <para>
        /// The teaching is in the geometry rather than in a message. The rim is the lowest, outermost thing
        /// on each step, so it is the first place a shot arrives; and a cell just outside the rim has
        /// <b>two</b> rim cells as its on-level neighbours, because a taxicab ring runs diagonally and a
        /// cell one step out from it touches it twice. So the player's very first shot into the rim colours
        /// two glass balls, makes three with itself, and all three fall — the rule states itself on shot
        /// one, with no isolated glass anywhere to teach it the slow way.
        /// </para>
        /// <para>
        /// The bottom course is the whole tip, glass and nothing else: four balls of clear hanging under the
        /// stone as the lowest thing in the level, which is what the gun is pointing at when it opens.
        /// </para>
        /// </summary>
        private static Design Facet() => new()
        {
            File = "Facet.json",
            Name = "Facet",
            Grid = FACET_GRID,
            Depth = FACET_DEPTH,
            Scene = SceneKind.Dream,
            Sky = MIRAGE_SKY,
            Music = MUSIC_MIRAGE,
            Balls = BALLS_MIRAGE,
            Shots = 34,
            CeilingStep = 7,
            OccupiedBlock = (x, z, i, depth) => MirageTaxicab(x, z, i, FACET_GRID) <= FacetRim(i),
            //Gem's own ring rule and its own roll of two: rings two taxicab units wide (one is a bare
            //diagonal), turned two palette steps a facet step, because turning by one carries a ring's
            //colour straight out onto the step below and welds them into one group down the stone.
            BlockColour = (x, z, i) =>
                Band((int)MathF.Floor(MirageTaxicab(x, z, i, FACET_GRID) * HALF) + 2 * (i / 2), FACET_PALETTE),
            BlockKind = FacetKind,
        };

        /// <summary>
        /// <b>Three small pyramids grown into one another</b> — the owner's own asking, and the block's
        /// second level. Each is <see cref="One"/>'s perfect cannonball pyramid hanging point down; their
        /// centres stand six cells apart on a triangle, so the three hang separately for their lower half
        /// and merge into one plate over their top four courses.
        /// <para>
        /// <b>The glass is the wireframe.</b> Each pyramid's four corner columns — its arrises, where two
        /// walls meet — are clear from the point up, so what the player sees is three coloured solids drawn
        /// in outline by something that is not a colour. A corner column is a single line of balls running
        /// diagonally up the solid, which is precisely the shape no colour may take here
        /// (<see cref="FindLonelyBalls"/>) and glass may, because it has no group to be alone in. It is the
        /// clearest statement in the block of what the new kind buys a designer.
        /// </para>
        /// <para>
        /// The arrises stop below the merge (<see cref="TREFOIL_ARRIS_TOP"/>). Where two pyramids grow
        /// together their facing corners land in the same column and the third pyramid closes the gap beside
        /// it, which is how a glass ball ends up with every neighbour occupied — walled in, refused, and the
        /// one place in this block where that could happen by geometry rather than by carelessness.
        /// </para>
        /// </summary>
        private static Design Trefoil() => new()
        {
            File = "Trefoil.json",
            Name = "Trefoil",
            Grid = TREFOIL_GRID,
            Depth = TREFOIL_DEPTH,
            Scene = SceneKind.Dream,
            Sky = MIRAGE_SKY,
            Music = MUSIC_MIRAGE,
            Balls = BALLS_MIRAGE,
            Shots = 46,
            CeilingStep = 8,
            OccupiedBlock = (x, z, i, depth) => TrefoilPyramid(x, z, i) >= 0,
            BlockColour = TrefoilColour,
            BlockKind = TrefoilKind,
        };

        /// <summary>
        /// <b>An argyle drum</b>: a hollow, gently flared shell tiled all round with the diamonds of a
        /// harlequin's coat — four jewel colours in a lattice of rhombi — with the pattern's own
        /// cross-stitch, every sixth diagonal, drawn in clear glass.
        /// <para>
        /// It is hollow for the block's law rather than for the silhouette. A stitch line is one cell wide by
        /// definition, and a single-cell line through a SOLID body is glass with an occupied neighbour on
        /// every side — walled in, unreachable, refused. A shell two cells thick is the thinnest wall this
        /// lattice holds together at either parity (the Pictures region's own lesson) and every cell of one
        /// touches either the hollow or the open air, so the whole pattern is reachable by construction
        /// rather than by inspection.
        /// </para>
        /// <para>
        /// The tiling is read in the rotated frame (<c>u = dx + dz</c>, <c>v = dx − dz</c>), where a
        /// lattice-aligned square is a diamond — which is why an argyle can be drawn on this lattice at all
        /// — and rolled one palette step every two courses, so no tile is ever stacked on its own colour and
        /// no vertical column of the drum is a single group.
        /// </para>
        /// </summary>
        private static Design Harlequin() => new()
        {
            File = "Harlequin.json",
            Name = "Harlequin",
            Grid = HARLEQUIN_GRID,
            Depth = HARLEQUIN_DEPTH,
            Scene = SceneKind.Dream,
            Sky = MIRAGE_SKY,
            Music = MUSIC_MIRAGE,
            Balls = BALLS_MIRAGE,
            Shots = 60,
            CeilingStep = 10,
            OccupiedBlock = (x, z, i, depth) =>
            {
                float r = MirageRadius(x, z, i, HARLEQUIN_GRID);
                return r <= HarlequinOuter(i) && r >= HarlequinOuter(i) - HARLEQUIN_WALL;
            },
            //MirageBand and not Band: an argyle tile index is floor(u / tile) and goes negative the moment
            //u is left of the axis, which the bare % in Band would have indexed the palette with.
            BlockColour = (x, z, i) => MirageBand(HarlequinTile(x, z, i), HARLEQUIN_PALETTE),
            BlockKind = HarlequinKind,
        };

        /// <summary>
        /// <b>A coronet</b>: a ring band against the glass with four pyramids hanging under it at the
        /// compass points, each stone set in a clear collar from its point up to its waist.
        /// <para>
        /// The glass is the SETTING here, which is the third thing the block does with it — the rim of a
        /// terrace, then the arris of a solid, and now the casing round one. A stone's collar is the outer
        /// ring of its own course (<see cref="OneShell"/>'s shell zero), so it is the stone's skin and
        /// reachable by definition; and it stops at <see cref="DIADEM_COLLAR_TOP"/>, leaving the shoulders
        /// coloured where they meet the band, so nothing structural is ever clear.
        /// </para>
        /// <para>
        /// The four stones touch at their corners on their widest course, so the coronet closes into a ring
        /// twice over — once through the band above and once round the stones themselves.
        /// </para>
        /// <para>
        /// <b>⚠ The band is wide because of what the sag probe said, and the diagnosis was wrong twice before
        /// it was right.</b> This design was the one level of the whole 105 that the probe reported — 5 runs
        /// of 5, with the glass stepped — and it looked like the obvious thing: four pendulums six courses
        /// long hanging off a ring. Shortening the stems did not move it (still 4 of 5), and neither did
        /// widening the ceiling step. What the trace actually showed is that the untouched cluster starts
        /// **7.7 above the line** and goes under on the shot that releases sixty balls at once, which is not
        /// a sag but a **swing** — and what a ring is short of, against a solid body of the same width, is
        /// <b>ceiling sockets</b>. Its inner radius went from 3.6 to <see cref="DIADEM_BAND_INNER"/>, i.e.
        /// 80 anchors to 100, which is Anvil's own order (120, 0 of 5) — and the level reads **0 of 5** at
        /// the first probe after it. The hole down the middle is still five cells across, so it is still a
        /// coronet; it is simply not a thin one. See "The sag gate" in <c>docs/formats-and-tools.md</c>.
        /// </para>
        /// <para>
        /// <b>⚠ AND IT NEEDED THE SAME LEVER A SECOND TIME, FOR A DIFFERENT CAUSE (#344).</b> When the
        /// colouring stopped at the panes touching the landing, one shot into a collar was worth a handful;
        /// once it runs through the connected glass, <b>one landing here colours 50</b> — half the level's
        /// glass, because the four collars meet where the stones do. That is the swing above with a much
        /// bigger weight on it, and measured in one session against the same build the day the rule changed:
        /// <b>2 of 5 before, 5 of 5 after</b>. The inner radius went 2.6 to 2.0 (100 anchors to 112) and it
        /// reads <b>3 of 5</b>, twice, which is where Harlequin and Solitaire sit and below the threshold.
        /// </para>
        /// <para>
        /// <b>The anchors are the lever that WORKED and not the one that fits</b>, and that is worth saying
        /// plainly: widening the band inward adds balls almost as fast as it adds sockets (732 to 792 here,
        /// anchor load 8.0 to 7.7), so it buys much less than it did the first time. The lever that fits the
        /// new cause is the payout — four collars that do not touch would cap one landing at about 25 — and
        /// that is a change to what the coronet looks like, so it is the owner's and not this file's. If this
        /// design is opened again, start there.
        /// </para>
        /// </summary>
        private static Design Diadem() => new()
        {
            File = "Diadem.json",
            Name = "Diadem",
            Grid = DIADEM_GRID,
            Depth = DIADEM_DEPTH,
            Scene = SceneKind.Dream,
            Sky = MIRAGE_SKY,
            Music = MUSIC_MIRAGE,
            Balls = BALLS_MIRAGE,
            Shots = 54,
            CeilingStep = 13,
            OccupiedBlock = (x, z, i, depth) => DiademOccupied(x, z, i),
            BlockColour = DiademColour,
            BlockKind = DiademKind,
        };

        /// <summary>
        /// <b>The glass half's finale, and the level that inverts the whole idea.</b> A hollow stepped
        /// octahedron — a solitaire, cut in three-course steps — whose four facets are clear and whose
        /// colour lives one ring in and along its four edges, so what hangs over the island is a colourless
        /// stone with its fire somewhere inside it and its cut drawn on the outside.
        /// <para>
        /// Every level before this used the glass as a mark ON a coloured body; this one makes the body
        /// glass and the colour the mark — the edges being the only mark the surface carries. It plays as a
        /// different game for it: a shot has almost no colour to reach until it has made one, so the first
        /// act of most exchanges is turning a patch of a facet into something, and the drop that follows
        /// takes the coloured ring behind it as well.
        /// </para>
        /// <para>
        /// <b>The anchor course is solid colour</b>, which is the block's law and also the reason this shape
        /// can be this bold: the top course is the one place the outer ring is NOT clear, so the whole
        /// cluster hangs on balls the player can see, count and plan for, however much of what is under them
        /// cannot be seen at all.
        /// </para>
        /// </summary>
        private static Design Solitaire() => new()
        {
            File = "Solitaire.json",
            Name = "Solitaire",
            Grid = SOLITAIRE_GRID,
            Depth = SOLITAIRE_DEPTH,
            Scene = SceneKind.Dream,
            Sky = MIRAGE_SKY,
            Music = MUSIC_MIRAGE,
            Balls = BALLS_MIRAGE,
            Shots = 68,
            CeilingStep = 12,
            OccupiedBlock = (x, z, i, depth) =>
            {
                float m = MirageTaxicab(x, z, i, SOLITAIRE_GRID);
                return m <= SolitaireRim(i) && m >= SolitaireInner(i);
            },
            //Four facets against four colours, turned one step a course: a quadrant never stands on its own
            //colour and never touches it sideways either, so the shell comes out cut rather than banded.
            BlockColour = (x, z, i) => Band(MirageQuadrant(x, z, i, SOLITAIRE_GRID) + i, SOLITAIRE_PALETTE),
            BlockKind = SolitaireKind,
        };

        //THE ELEVENTH BLOCK'S FIVE ROCK LEVELS, and their law is the mirror of the glass's:
        //
        //  ⚠ STONE REACHES THE CEILING ON ITS OWN, and what is left standing at the end is the stone.
        //
        //A rock matches with nothing and no colour removes it, so it is not an obstacle in the sense a
        //colour is - it is a piece of ARCHITECTURE the player plays around. Three consequences, and every
        //design below is built out of them:
        //
        //  1. IT STOPS THE FLOOD FILL EXACTLY LIKE AN EMPTY CELL. BallsMap.GetConnectedSameTypeCells skips
        //     a non-matchable neighbour, so a wall of stone divides one colour into two groups as surely as
        //     a gap would - and unlike a gap the player can see it and count on it. Two of the five below
        //     are about nothing else. ⚠ A wall has to be thick enough: a cross-level step changes dx+dz by
        //     -1, 0 or +1, so a DIAGONAL plane one cell thick leaks and needs three, where an AXIS-ALIGNED
        //     one does not - an on-level step changes x by exactly one and a cross-level step by nought or
        //     one, so no path crosses a single missing column.
        //  2. A ROCK ON THE ANCHOR COURSE IS AN ANCHOR NOTHING CAN TAKE, which is the exact opposite of
        //     what glass does up there, and it is the one thing in this game that can only IMPROVE what
        //     WorstAnchorLoad measures. The finale hangs on stone for that reason.
        //  3. IT IS NOT A BALL THE LEVEL IS WAITING FOR. GetRemovableBallsCount passes over it, so a field
        //     of nothing but rock is a CLEARED field: CheckLevelCleared cuts the stone loose and it falls,
        //     drains and scores nothing. So a rock design can end with a skeleton still hanging and that is
        //     a finish, not a stall.
        //
        //THE ROCKS ARE ALWAYS VISIBLE. Every one of the five puts its stone where the gun can see it before
        //the first shot - the underside, a wall across the face, piers, a spiral - because a rule the
        //player discovers by wasting a ball on it is a rule taught the one way this block is trying not to
        //teach anything.

        /// <summary>
        /// <b>The rock half's opener, and the level that teaches what the stone IS.</b> A bell of colour
        /// with its whole point — the bottom three courses, the lowest thing in the level and the first
        /// thing the gun is aimed at — cast in stone.
        /// <para>
        /// It teaches by refusing. The player's opening shot goes where every level of the campaign has
        /// taught them to send it, at the nearest mass under the cluster, and nothing happens: the ball
        /// sticks beside a thing that will not match. The lesson costs one ball, is over in three seconds,
        /// and no level after this one has to spend anything on it.
        /// </para>
        /// <para>
        /// And it teaches the other half at the end without a word. The anvil is not something to be
        /// cleared: when the last colour comes off the bell the stone is cut loose and falls with it, which
        /// is <c>CheckLevelCleared</c>'s own doing — so the player's first rock level ends on the sight of
        /// the thing they could not shoot dropping through the drain anyway.
        /// </para>
        /// </summary>
        private static Design Anvil() => new()
        {
            File = "Anvil.json",
            Name = "Anvil",
            Grid = ANVIL_GRID,
            Depth = ANVIL_DEPTH,
            Scene = SceneKind.Dream,
            Sky = MIRAGE_SKY,
            Music = MUSIC_MIRAGE,
            Balls = BALLS_MIRAGE,
            Shots = 50,
            CeilingStep = 9,
            OccupiedBlock = (x, z, i, depth) => MirageRadius(x, z, i, ANVIL_GRID) <= AnvilRadius(i),
            //Six staves round the bell, turned a step a course so a stave is a pane rather than a column -
            //Lantern's rule, and for Lantern's reason: six vertical staves of dozens is six groups.
            //
            //TWO steps every TWO courses, and both numbers are measured. The two STEPS are Keystone's
            //lesson arriving here as well: at one step a stave's colour at course i lands back on its
            //NEIGHBOUR's at course i-1, and those two cells are cross-level neighbours - so the panes welded
            //into a helix and the first cut shipped one colour standing in a single group of 126, a fifth of
            //the cluster gone on one lucky ball. The two COURSES are what turning every course cost: it
            //shattered the bell into 36 groups of fifteen, which is a shot budget of ninety on the block's
            //gentlest level. A pane two courses tall breaks the weld both ways - the diagonal pairs differ
            //by one and the two-course jump by two - and leaves the bell at half the group count.
            BlockColour = (x, z, i) =>
                Band(MirageSector(x, z, i, ANVIL_GRID, ANVIL_STAVES) + 2 * (i / 2), ANVIL_PALETTE),
            BlockKind = (x, z, i, depth) => i < ANVIL_STONE_COURSES ? BallKind.Rock : BallKind.Normal,
        };

        /// <summary>
        /// <b>One wall of stone, straight through the middle.</b> A broad rounded body cut corner to corner
        /// by a diagonal seam three cells thick, and the two halves it leaves are painted in two different
        /// families — the cool blues one side, the warm reds the other — so that what the wall does is
        /// <i>legible before it is discovered</i>.
        /// <para>
        /// The seam is the mechanic stated as plainly as it can be: a rock stops the flood fill exactly like
        /// an empty cell, so a colour on one side of the wall and the same colour on the other are two
        /// groups and always will be. Two palettes make that a fact the player reads off the cluster instead
        /// of one they work out by watching a match fail to travel.
        /// </para>
        /// <para>
        /// <b>Three cells thick and not one</b>, which is the diagonal-plane rule in the region header
        /// above: a cross-level step changes <c>dx + dz</c> by −1, 0 or +1, so a diagonal wall one cell wide
        /// is a wall with a door on every course.
        /// </para>
        /// <para>
        /// <b>⚠ The seam runs to one course short of the glass, not to the anchor course</b> (#343). It used
        /// to reach the top, and the paragraph here used to argue for that: both halves then hung on their
        /// own and neither depended on the other for the ceiling. The cost of it was 26 rocks the player
        /// could never bring down. The wall's cells on the anchor course are still there — they carry their
        /// side's colour now — so the top course is one span the colour crosses, exactly as
        /// <see cref="Keystone"/>'s bays meet over its piers, and what the wall does to the flood fill on
        /// every course below is unchanged.
        /// </para>
        /// </summary>
        private static Design Seam() => new()
        {
            File = "Seam.json",
            Name = "Seam",
            Grid = SEAM_GRID,
            Depth = SEAM_DEPTH,
            Scene = SceneKind.Dream,
            Sky = MIRAGE_SKY,
            Music = MUSIC_MIRAGE,
            Balls = BALLS_MIRAGE,
            Shots = 54,
            CeilingStep = 9,
            OccupiedBlock = (x, z, i, depth) => MirageRadius(x, z, i, SEAM_GRID) <= SeamRadius(i),
            BlockColour = SeamColour,
            //One course short of the glass (#343), which is Keystone's rule arriving here — see ObsidianKind
            //for the whole argument. The seam's cells on the anchor course are still there and still drawn;
            //they simply carry their side's colour instead of granite.
            BlockKind = (x, z, i, depth) => i < depth - 1 && SeamStone(x, z, i) ? BallKind.Rock : BallKind.Normal,
        };

        /// <summary>
        /// <b>An arcade.</b> A slab of colour standing across the player's view, divided into four bays by
        /// three piers of stone — and the piers stop short of the glass, so the bays are separate chambers
        /// at the bottom and one continuous span at the top. The keystone is that span.
        /// <para>
        /// It is the first level where the stone is <i>worth</i> something to the player rather than merely
        /// in the way. A colour reaches from one bay into the next only over the top of a pier, so the shot
        /// that opens a bay's ceiling is worth two bays' worth of group — and the same fact says that
        /// working a bay from the bottom up gains nothing, because below the springing the bays cannot reach
        /// each other at all.
        /// </para>
        /// <para>
        /// <b>The piers are one cell thick, and that is not <see cref="Seam"/>'s mistake repeated.</b> A
        /// pier is AXIS-ALIGNED: an on-level step changes <c>x</c> by exactly one and a cross-level step by
        /// nought or one, so nothing crosses a single missing column of <c>x</c>. It is only the DIAGONAL
        /// wall that leaks at one cell, which is why the seam's is three and these are one.
        /// </para>
        /// </summary>
        private static Design Keystone() => new()
        {
            File = "Keystone.json",
            Name = "Keystone",
            Grid = KEYSTONE_GRID,
            Depth = KEYSTONE_DEPTH,
            Scene = SceneKind.Dream,
            Sky = MIRAGE_SKY,
            Music = MUSIC_MIRAGE,
            Balls = BALLS_MIRAGE,
            Shots = 60,
            CeilingStep = 10,
            OccupiedBlock = (x, z, i, depth) => KeystoneOccupied(x, z, i),
            BlockColour = KeystoneColour,
            BlockKind = (x, z, i, depth) => KeystonePier(x, z, i) ? BallKind.Rock : BallKind.Normal,
        };

        /// <summary>
        /// <b>Four chambers under one roof.</b> A round body quartered by two crossed walls of stone that
        /// run from the bottom to one course short of the glass, each quarter carrying the same four colours
        /// in a different order.
        /// <para>
        /// This is <see cref="Seam"/>'s lesson taken as far as it goes: one colour standing in all four
        /// quarters is four separate groups on every course but the topmost, and the level is four small
        /// levels played inside one silhouette. What it asks of the player is bookkeeping of a kind nothing
        /// before it has asked for — which quarter still has red in it, and how much — rather than the
        /// reading of a single shape.
        /// </para>
        /// <para>
        /// <b>⚠ "Quartered top to bottom" is what this used to say, and #343 is why it no longer is.</b> The
        /// walls reached the anchor course, which cost 44 rocks the player could never bring down — the most
        /// of any level in the campaign. They stop one course short now, so the anchor course is a single
        /// roof the four chambers meet under, which is <see cref="Keystone"/>'s span arriving in a design
        /// that did not ask for it. It is a real change to the level and not only to its rocks: the roof is
        /// the one place a colour can be worked across two quarters at once.
        /// </para>
        /// <para>
        /// <b>The walls are one cell thick and they still hold</b>, for <see cref="Keystone"/>'s reason:
        /// they are axis-aligned. They are stated as <c>|dx| ≤ 0.5</c> rather than <c>dx == 0</c> because
        /// the lattice shifts every other level by half a cell — on the shifted courses there is no column
        /// at <c>dx = 0</c> at all, and a wall written as an equality would have been a wall with a hole in
        /// every second course. The half-unit form is one column on the unshifted levels and two on the
        /// shifted ones, and closed on both.
        /// </para>
        /// </summary>
        private static Design Cairn() => new()
        {
            File = "Cairn.json",
            Name = "Cairn",
            Grid = CAIRN_GRID,
            Depth = CAIRN_DEPTH,
            Scene = SceneKind.Dream,
            Sky = MIRAGE_SKY,
            Music = MUSIC_MIRAGE,
            Balls = BALLS_MIRAGE,
            Shots = 72,
            CeilingStep = 12,
            OccupiedBlock = (x, z, i, depth) => MirageRadius(x, z, i, CAIRN_GRID) <= CairnRadius(i),
            BlockColour = CairnColour,
            //One course short of the glass (#343) — see Seam's own line and ObsidianKind for the argument.
            BlockKind = (x, z, i, depth) => i < depth - 1 && CairnWall(x, z, i) ? BallKind.Rock : BallKind.Normal,
        };

        /// <summary>
        /// <b>The chapter's finale and the campaign's last level.</b> A solid stepped octahedron with a vein
        /// of stone winding up through it — one wedge of every course, turned a fraction of a turn a course,
        /// so the vein spirals from the point to within one course of the glass.
        /// <para>
        /// <b>⚠ It used to cast the anchor course's outer ring in stone as well, and that was the finale's
        /// stated whole point — "the cluster hangs on rock no shot can take". #343 retracts it</b>, because a
        /// support the player cannot touch is also 58 balls of the last level's 113 anchors that the player
        /// can never clear. What the level keeps of that idea is the half that was always the player's
        /// problem rather than the ceiling's: a spiral of stone they have to work around. What it gives up is
        /// the guarantee that <see cref="WorstAnchorLoad"/> could not be made worse by anything they do — so
        /// the finale is now held to that gate on measurement like every other level in the campaign, which
        /// is the honest way to end on the densest solid in the block.
        /// </para>
        /// <para>
        /// The spiral is the other half. A wedge of stone at every course means the colour has to be worked
        /// AROUND rather than down, and since the wedge turns, the way round changes as the player descends
        /// — the shape reads as a screw and plays as one.
        /// </para>
        /// </summary>
        private static Design Obsidian() => new()
        {
            File = "Obsidian.json",
            Name = "Obsidian",
            Grid = OBSIDIAN_GRID,
            Depth = OBSIDIAN_DEPTH,
            Scene = SceneKind.Dream,
            Sky = MIRAGE_SKY,
            Music = MUSIC_MIRAGE,
            Balls = BALLS_MIRAGE,
            Shots = 50,
            CeilingStep = 9,
            OccupiedBlock = (x, z, i, depth) => MirageTaxicab(x, z, i, OBSIDIAN_GRID) <= ObsidianRim(i),
            BlockColour = ObsidianColour,
            BlockKind = ObsidianKind,
        };

        #endregion

        #region The mirage levels' own geometry (#323/#325)

        //THE DOME NUMBER THE WHOLE BLOCK NAMES, and it is INERT: the dream is one of the four sky-replacing
        //scenes (SceneRenderer.ReplacesSky), so no dome is drawn and the scene states its own light rig
        //instead of taking the palette's - a violet sky ambient over a teal ground bounce, a rose key
        //against a cyan fill. The field is stated anyway, and stated identically on all ten, for the reason
        //Colossus's was moved to 13: DescribeBlock reads the block's four properties off the written files
        //and would report MIXED for ever over a difference nothing can render. Ten is the dream's own index
        //in SceneKind and means nothing else.
        private const byte MIRAGE_SKY = 10;

        //THE FRAME EVERY DESIGN IN THIS BLOCK IS DRAWN IN, and the block uses the RAW LATTICE one
        //(OccupiedBlock/BlockColour/BlockKind) throughout where most of the file solves a radius. There is
        //one reason and it is the glass law: "on the skin" and "on the arris" are questions about the cell
        //BELOW and the cell BESIDE, and a design handed only (r, ang) cannot ask either - the cell below
        //sits at a different shift, hence a different radius and a different angle, so the polar frame has
        //no way to name it. Drawn on (x, z, i) the neighbours are arithmetic. One() takes this frame for a
        //related reason (a course edge has to land on the half-unit exactly) and its helpers are reused
        //below wherever a pyramid appears.
        //
        //⚠ THE ARITHMETIC IS EXACT AND THE EQUALITY TESTS BELOW DEPEND ON IT. A cell's offset from the axis
        //is (index + shift - axis) where shift is 0 or 1/2 and the axis is a whole multiple of 1/2, so every
        //dx, dz, radius-squared and taxicab sum here is a whole multiple of 1/2 and exact in binary. That is
        //why an arris can be tested with == against a half-width and a taxicab rim with == against an
        //integer, with no tolerance anywhere - the same argument OneCourse makes for the opener's courses.

        private static float MirageShift(int i) => (i % 2) * HALF;

        /// <summary>The axis a design centres on, in the units the emitter's own shift is measured in.</summary>
        private static float MirageAxis(int grid) => (grid - 1) * HALF + HALF;

        private static float MirageDx(int x, int i, int grid) => x + MirageShift(i) - MirageAxis(grid);

        private static float MirageDz(int z, int i, int grid) => z + MirageShift(i) - MirageAxis(grid);

        private static float MirageRadius(int x, int z, int i, int grid)
        {
            float dx = MirageDx(x, i, grid);
            float dz = MirageDz(z, i, grid);

            return MathF.Sqrt(dx * dx + dz * dz);
        }

        private static float MirageTaxicab(int x, int z, int i, int grid) =>
            MathF.Abs(MirageDx(x, i, grid)) + MathF.Abs(MirageDz(z, i, grid));

        private static float MirageAngle(int x, int z, int i, int grid) =>
            MathF.Atan2(MirageDz(z, i, grid), MirageDx(x, i, grid));

        private static int MirageSector(int x, int z, int i, int grid, int sectors) =>
            SectorIndex(MirageAngle(x, z, i, grid), 0f, sectors);

        /// <summary>
        /// Which of the four taxicab quadrants a cell is in — the facets of an octahedron, numbered so that
        /// no two adjacent ones differ by a multiple of four and a four-colour palette therefore never puts
        /// the same colour on both sides of an edge.
        /// </summary>
        private static int MirageQuadrant(int x, int z, int i, int grid) =>
            (MirageDx(x, i, grid) >= 0f ? 1 : 0) + (MirageDz(z, i, grid) >= 0f ? 2 : 0);

        /// <summary>
        /// <see cref="Band"/> for an index that can go negative — which the argyle's <c>floor(u / tile)</c>
        /// does the moment <c>u</c> is left of the axis, and <c>Band</c>'s bare <c>%</c> would have indexed
        /// the palette at a negative subscript. Everything else in this file builds its band out of radii,
        /// shells and sector indices, all of them non-negative by construction, which is why the plain form
        /// has served until now.
        /// </summary>
        private static BallType MirageBand(int band, BallType[] palette) =>
            palette[((band % palette.Length) + palette.Length) % palette.Length];

        private static int MirageFloorDiv(int a, int b) => (int)MathF.Floor(a / (float)b);

        //THE FACET. Gem's terraced octahedron at four steps of two courses, with the outer taxicab ring of
        //every second course cast in clear glass. Seventeen wide for Gem's own measured reason: a taxicab
        //rim of 7 reaches seven whole cells along each axis where a round radius of 5.5 reaches five, and
        //fifteen put the diamond's four points ON the field wall with no lateral margin at all.
        private const byte FACET_GRID = 17;
        private const byte FACET_DEPTH = 8;

        /// <summary>
        /// The stone's taxicab rim at a course: two courses a step, every step an ODD rim. Gem's rule and
        /// Gem's reason — an odd rim means the outermost COLOURED ring of a step is a complete two units
        /// wide and never a bare diagonal, which is the lonely-ball defect the first Gem shipped 28 of.
        /// </summary>
        private static int FacetRim(int i) => 1 + 2 * (i / 2);

        //Cyan, blue, navy, magenta - a cool sweep with one warm stone in it. The three cool members are the
        //#246 re-spacing's own trio (Type3 and Type12 were pulled apart precisely so a design could carry
        //both), and the magenta is what stops four rings of one family reading as a gradient.
        private static readonly BallType[] FACET_PALETTE =
            { BallType.Type5, BallType.Type3, BallType.Type12, BallType.Type6 };

        /// <summary>
        /// The clear rim: the outermost taxicab ring of a step's LOWER course, one cell thick.
        /// <para>
        /// One cell is the whole point and it is legal only because the ball is glass — a taxicab ring runs
        /// diagonally and a colour band one cell wide along it would be a string of balls that do not touch
        /// each other at all (<see cref="FindLonelyBalls"/>). It is also what makes the level teach itself:
        /// a cell one step outside a diagonal ring touches it TWICE, so the first shot into the rim colours
        /// two glass balls and takes all three down with it.
        /// </para>
        /// <para>
        /// The lower course of a step, because that course overhangs the narrower step below it — so the
        /// ring has empty cells under it as well as beside it, and is on the skin twice over. Never the top
        /// course: <c>i</c> is even and the depth is even, so the anchor course is odd and excluded by
        /// construction; the test is written out anyway, because the day someone changes the depth to an odd
        /// number is the day that stops being true.
        /// </para>
        /// </summary>
        private static BallKind FacetKind(int x, int z, int i, int depth) =>
            i % 2 == 0 && i < depth - 1 && MirageTaxicab(x, z, i, FACET_GRID) == FacetRim(i)
                ? BallKind.Transparent
                : BallKind.Normal;

        //THE TREFOIL. Three of the opener's perfect cannonball pyramids, centres six apart on a triangle,
        //eight courses deep - so each is 3.5 half-widths across at the plate and the three merge over their
        //top four courses while hanging separately below. Sixteen wide, which is an EVEN grid and therefore
        //a WHOLE-NUMBER axis: the centres below are whole offsets from it, so each pyramid's course lands on
        //the half-unit exactly at either parity and comes out i+1 cells a side, which is the arithmetic
        //OneCourse depends on. An odd grid would have put the axis on a half and cost the two off-axis
        //pyramids a cell a side on alternate courses.
        private const byte TREFOIL_GRID = 16;
        private const byte TREFOIL_DEPTH = 8;

        private static readonly (float X, float Z)[] TREFOIL_CENTRES =
            { (-3f, -2f), (3f, -2f), (0f, 3f) };

        /// <summary>
        /// The highest course carrying a glass arris. Below the merge on purpose: the two lower pyramids
        /// grow together from course six and the third reaches them from course five, and where two
        /// pyramids meet their facing corner columns land in the SAME cell with the third closing the gap
        /// beside it — a glass ball with every neighbour occupied, which <see cref="FindStrandedSpecials"/>
        /// refuses and which is the only way this block could have produced one by geometry.
        /// </summary>
        private const int TREFOIL_ARRIS_TOP = 4;

        //Red, orange, yellow, magenta. A warm family with one member off the far side of red, so the three
        //stones read as one material lit three ways rather than as three unrelated objects - which a cool
        //accent in here would have broken.
        private static readonly BallType[] TREFOIL_PALETTE =
            { BallType.Type1, BallType.Type9, BallType.Type7, BallType.Type6 };

        private static bool InTrefoilPyramid(int x, int z, int i, int k)
        {
            float half = i * HALF;

            return MathF.Abs(MirageDx(x, i, TREFOIL_GRID) - TREFOIL_CENTRES[k].X) <= half
                   && MathF.Abs(MirageDz(z, i, TREFOIL_GRID) - TREFOIL_CENTRES[k].Z) <= half;
        }

        /// <summary>Which pyramid owns a cell — the first that contains it, or −1 for empty space.</summary>
        private static int TrefoilPyramid(int x, int z, int i)
        {
            for (int k = 0; k < TREFOIL_CENTRES.Length; k++)
                if (InTrefoilPyramid(x, z, i, k)) return k;

            return -1;
        }

        /// <summary>
        /// The opener's own colouring — one colour a wall, turned a step every shell inward — run three
        /// times, once per pyramid, with the palette turned a further step per pyramid so the three stones
        /// are the same rule wearing different faces. <see cref="ONE_WALLS"/> comes across whole: four walls
        /// against a palette, the repeat on the FACING pair so no two neighbouring faces are alike and the
        /// repeat never meets itself at a corner.
        /// </summary>
        private static BallType TrefoilColour(int x, int z, int i)
        {
            int k = TrefoilPyramid(x, z, i);
            if (k < 0) return TREFOIL_PALETTE[0];        //unreachable: only occupied cells are coloured

            float half = i * HALF;
            float bx = MirageDx(x, i, TREFOIL_GRID) - TREFOIL_CENTRES[k].X;
            float bz = MirageDz(z, i, TREFOIL_GRID) - TREFOIL_CENTRES[k].Z;

            int shell = (int)(half - MathF.Max(MathF.Abs(bx), MathF.Abs(bz)));

            int wall = MathF.Abs(bx) > MathF.Abs(bz) ? (bx > 0 ? 1 : 3)
                : MathF.Abs(bz) > MathF.Abs(bx) ? (bz > 0 ? 2 : 0)
                : bx > 0 ? (bz > 0 ? 1 : 0) : (bz > 0 ? 2 : 3);

            return MirageBand(ONE_WALLS[wall] + shell + k, TREFOIL_PALETTE);
        }

        /// <summary>
        /// The wireframe: a cell standing on a pyramid's corner column, where its distance from that
        /// pyramid's own centre is the course's half-width in BOTH axes at once. At the apex the half-width
        /// is nought and the test picks the single ball the pyramid comes to a point on, so each stone is
        /// outlined from its tip.
        /// <para>
        /// It asks every pyramid rather than only the owning one, so a stone standing inside the merge is
        /// still drawn in full — and a cell that satisfies the test for pyramid <i>k</i> is by definition
        /// inside pyramid <i>k</i>, the equality being the boundary of the <c>&lt;=</c> that defines it.
        /// </para>
        /// </summary>
        private static BallKind TrefoilKind(int x, int z, int i, int depth)
        {
            if (i > TREFOIL_ARRIS_TOP) return BallKind.Normal;

            float half = i * HALF;

            for (int k = 0; k < TREFOIL_CENTRES.Length; k++)
                if (MathF.Abs(MirageDx(x, i, TREFOIL_GRID) - TREFOIL_CENTRES[k].X) == half
                    && MathF.Abs(MirageDz(z, i, TREFOIL_GRID) - TREFOIL_CENTRES[k].Z) == half)
                    return BallKind.Transparent;

            return BallKind.Normal;
        }

        //THE HARLEQUIN. A hollow drum flaring gently towards the glass, wall two cells thick - the thinnest
        //this lattice holds together whatever the parity, and the reason the argyle can be stitched at all:
        //every cell of a two-thick shell touches the hollow or the open air, so a one-cell stitch line is
        //reachable everywhere by construction rather than by inspection.
        private const byte HARLEQUIN_GRID = 16;
        private const byte HARLEQUIN_DEPTH = 10;
        private const float HARLEQUIN_BASE = 5.2f;
        private const float HARLEQUIN_FLARE = 0.16f;
        private const float HARLEQUIN_WALL = 1.9f;

        private static float HarlequinOuter(int i) => HARLEQUIN_BASE + i * HARLEQUIN_FLARE;

        //THE TILE, in the rotated frame's own units. Six is what makes a diamond read as a diamond after the
        //lattice has rounded it: a tile spans six along each rotated axis, which is about four cells across
        //its waist, and it carries a stitch line every sixth diagonal - a sixth of the drum in glass, which
        //is a stitch rather than a second pattern.
        private const int HARLEQUIN_TILE = 6;

        //Magenta, navy, cyan, yellow: the secondary triad with the set's dark blue standing in for the black
        //diamonds a harlequin's coat actually carries. Bright, bright, bright, dark - which is what makes an
        //argyle read as an argyle rather than as four colours in a grid.
        private static readonly BallType[] HARLEQUIN_PALETTE =
            { BallType.Type6, BallType.Type12, BallType.Type5, BallType.Type7 };

        //THE ROTATED FRAME. u = dx + dz and v = dx - dz turn the lattice 45 degrees, so a square tile in
        //(u, v) is a DIAMOND in (x, z) - which is the only way to draw a rhombus on a square lattice without
        //stepping it. Both are whole numbers at either parity: on the unshifted courses dx and dz are whole
        //and on the shifted ones they are both a half, and a half plus or minus a half is a whole.
        private static int HarlequinU(int x, int z, int i) =>
            (int)MathF.Round(MirageDx(x, i, HARLEQUIN_GRID) + MirageDz(z, i, HARLEQUIN_GRID));

        private static int HarlequinV(int x, int z, int i) =>
            (int)MathF.Round(MirageDx(x, i, HARLEQUIN_GRID) - MirageDz(z, i, HARLEQUIN_GRID));

        /// <summary>
        /// Which argyle tile a cell falls in, rolled one palette step every two courses — so a tile is two
        /// courses tall and never stands on its own colour, which is what keeps a vertical column of the
        /// drum from being one group all the way up.
        /// </summary>
        private static int HarlequinTile(int x, int z, int i) =>
            MirageFloorDiv(HarlequinU(x, z, i), HARLEQUIN_TILE)
            + MirageFloorDiv(HarlequinV(x, z, i), HARLEQUIN_TILE)
            + i / 2;

        /// <summary>
        /// The cross-stitch: the diagonal where one family of tile boundaries falls, in clear glass. One
        /// family and not both — both would be 44 % of the drum and a lattice rather than a stitch.
        /// </summary>
        private static BallKind HarlequinKind(int x, int z, int i, int depth) =>
            i < depth - 1 && ((HarlequinU(x, z, i) % HARLEQUIN_TILE) + HARLEQUIN_TILE) % HARLEQUIN_TILE == 0
                ? BallKind.Transparent
                : BallKind.Normal;

        //THE DIADEM. A ring band against the glass over four pyramids at the compass points, whose widest
        //courses touch at the corners - so the coronet closes twice, through the band and round the stones.
        private const byte DIADEM_GRID = 16;
        private const byte DIADEM_DEPTH = 10;
        private const int DIADEM_BAND_FIRST = 5;
        private const float DIADEM_BAND_INNER = 2.0f;
        private const float DIADEM_BAND_OUTER = 6.4f;
        private const int DIADEM_STONE_DEPTH = 5;
        private const int DIADEM_BAND_SECTORS = 8;

        /// <summary>
        /// The highest course wearing a clear collar. Three of the stone's six, so the setting covers it
        /// from the point to the waist and its shoulders — the courses that actually meet the band — stay
        /// coloured. Nothing structural in this block is ever glass.
        /// </summary>
        private const int DIADEM_COLLAR_TOP = 3;

        private static readonly (float X, float Z)[] DIADEM_STONES =
            { (4f, 0f), (-4f, 0f), (0f, 4f), (0f, -4f) };

        //Green, olive, yellow, orange: emerald stones in a gold band, which is what a diadem is. Olive was
        //re-spaced towards green in #294 for exactly this kind of pairing - it had been dark enough to leave
        //the greens entirely and land among black, brown and silver.
        private static readonly BallType[] DIADEM_PALETTE =
            { BallType.Type2, BallType.Type13, BallType.Type7, BallType.Type9 };

        /// <summary>Which stone owns a cell, or −1 — the band's own courses have none.</summary>
        private static int DiademStone(int x, int z, int i)
        {
            if (i >= DIADEM_STONE_DEPTH) return -1;

            float half = i * HALF;

            for (int k = 0; k < DIADEM_STONES.Length; k++)
                if (MathF.Abs(MirageDx(x, i, DIADEM_GRID) - DIADEM_STONES[k].X) <= half
                    && MathF.Abs(MirageDz(z, i, DIADEM_GRID) - DIADEM_STONES[k].Z) <= half) return k;

            return -1;
        }

        private static bool DiademOccupied(int x, int z, int i)
        {
            if (i < DIADEM_BAND_FIRST) return DiademStone(x, z, i) >= 0;

            float r = MirageRadius(x, z, i, DIADEM_GRID);

            return r >= DIADEM_BAND_INNER && r <= DIADEM_BAND_OUTER;
        }

        /// <summary>
        /// The band takes sectors turned a step a course — Lantern's rule, so eight staves of dozens come
        /// out as panes of a few — and each stone takes the opener's walls-by-shell with the palette turned
        /// one further step per stone, so the four are visibly four stones and not one shape repeated.
        /// </summary>
        private static BallType DiademColour(int x, int z, int i)
        {
            int k = DiademStone(x, z, i);

            if (k < 0)
                return MirageBand(MirageSector(x, z, i, DIADEM_GRID, DIADEM_BAND_SECTORS) + i, DIADEM_PALETTE);

            float half = i * HALF;
            float bx = MirageDx(x, i, DIADEM_GRID) - DIADEM_STONES[k].X;
            float bz = MirageDz(z, i, DIADEM_GRID) - DIADEM_STONES[k].Z;

            int shell = (int)(half - MathF.Max(MathF.Abs(bx), MathF.Abs(bz)));

            int wall = MathF.Abs(bx) > MathF.Abs(bz) ? (bx > 0 ? 1 : 3)
                : MathF.Abs(bz) > MathF.Abs(bx) ? (bz > 0 ? 2 : 0)
                : bx > 0 ? (bz > 0 ? 1 : 0) : (bz > 0 ? 2 : 3);

            return MirageBand(ONE_WALLS[wall] + shell + k, DIADEM_PALETTE);
        }

        /// <summary>
        /// The setting: the outer ring of a stone's own course — <see cref="OneShell"/>'s shell zero, which
        /// is the stone's skin and therefore reachable by definition — from the apex up to
        /// <see cref="DIADEM_COLLAR_TOP"/>.
        /// </summary>
        private static BallKind DiademKind(int x, int z, int i, int depth)
        {
            if (i > DIADEM_COLLAR_TOP) return BallKind.Normal;

            int k = DiademStone(x, z, i);
            if (k < 0) return BallKind.Normal;

            float half = i * HALF;
            float bx = MathF.Abs(MirageDx(x, i, DIADEM_GRID) - DIADEM_STONES[k].X);
            float bz = MathF.Abs(MirageDz(z, i, DIADEM_GRID) - DIADEM_STONES[k].Z);

            return MathF.Max(bx, bz) == half ? BallKind.Transparent : BallKind.Normal;
        }

        //THE SOLITAIRE. A hollow stepped octahedron, three courses a step, wall two taxicab units thick -
        //and the outer of those two is the clear one, so the whole surface of the stone is glass and its
        //colour is the ring behind it. Seventeen wide for the Facet's reason and for Gem's.
        private const byte SOLITAIRE_GRID = 17;
        private const byte SOLITAIRE_DEPTH = 12;
        private const float SOLITAIRE_WALL = 2f;

        private static int SolitaireRim(int i) => 1 + 2 * (i / 3);

        /// <summary>
        /// The shell's inner face — the rim less the wall, but <b>never below one</b>, which is the tip
        /// step's own correction and the second half of the fix <see cref="SolitaireKind"/> records.
        /// <para>
        /// At a rim of one, <c>rim − wall</c> is −1 and the "shell" is a solid ball five cells across on
        /// the courses whose lattice has a centre. Clearing the tip made every one of those glass, and the
        /// centre cell then had glass on all four sides, glass above and glass below: a transparent ball
        /// with no empty neighbour anywhere, which no shot can ever land beside and therefore no shot can
        /// ever colour — <see cref="FindStrandedSpecials"/>'s walled-in case, arriving from the one direction
        /// nobody looks, which is a hollow body that stops being hollow when it gets small enough. Held at
        /// one the tip is hollow like the rest of the stone and there is no cell to strand.
        /// </para>
        /// </summary>
        private static float SolitaireInner(int i) => MathF.Max(SolitaireRim(i) - SOLITAIRE_WALL, 1f);

        //Magenta, navy, blue, cyan - a cool sweep with the magenta closing the wheel back on itself, so the
        //four facets of a course are four different colours and the wrap between the last and the first is
        //a step like any other rather than a jump.
        private static readonly BallType[] SOLITAIRE_PALETTE =
            { BallType.Type6, BallType.Type12, BallType.Type3, BallType.Type5 };

        /// <summary>
        /// The clear surface: the shell's outermost taxicab ring, everywhere but the anchor course.
        /// <para>
        /// The exception is the block's law and it is what makes the rest of the design safe. Glass on the
        /// field's top level is a ceiling anchor that dissolves the moment a shot colours it, so this one
        /// course keeps its colour and the whole cluster hangs on balls the player can see — which is the
        /// only reason a level can afford to be invisible everywhere else.
        /// </para>
        /// <para>
        /// <b>The tip step is solid glass, and that is a fix rather than a flourish.</b> At a rim of one the
        /// shell has no inside — the ring IS the body — so the outer-ring rule left exactly one coloured
        /// ball at the point, on the course whose lattice has a centre cell, with clear glass on all four
        /// sides of it and clear glass above and below. That is a ball standing alone in a group of one,
        /// which needs two landed balls of its own colour before anything happens and which
        /// <see cref="FindLonelyBalls"/> refuses. Clearing the whole tip is the honest answer: a body one
        /// unit across has no room for a coloured core and should not pretend to.
        /// </para>
        /// <para>
        /// <b>⚠ THE FOUR EDGES OF THE STONE STAY COLOURED, and that is what keeps one landing from taking a
        /// quarter of the level (#362).</b> A taxicab ring is <i>not</i> connected within its own course —
        /// a same-level step changes the taxicab distance by exactly one, so no two cells of a ring touch —
        /// which means a glass body here is a ring <b>stacked</b> through the three courses of a step. With
        /// the rings whole, the four steps were four bodies of 12, 36, 60 and 56, and a landing in the riser
        /// between two steps reaches <b>both</b>: measured, the best landing on this level coloured
        /// <b>116 of its 444 balls</b> and took 186 of them down, where the rest of the block pays 5 to 50.
        /// Cutting the ring at the stone's edges — the cells within one unit of either axis, which are the
        /// diamond's four points — turns each step's ring into four stacked <i>facets</i>, so a riser landing
        /// takes two facets of one quadrant instead of two whole steps: <b>21 coloured and 86 down</b>, which
        /// is where Harlequin and Diadem sit. It reads as a step-cut stone rather than as a repair, the
        /// coloured edges being what a cut stone shows anyway.
        /// </para>
        /// <para>
        /// <b>The rule is min(|dx|, |dz|) &lt; 1 and the threshold is exact rather than tuned</b>: an odd
        /// course's cells sit on integers and an even course's on half-integers, so the only values that
        /// exist below 1 are 0 and 0.5 — the edge is one cell wide on an odd course and two on an even one,
        /// and nothing between 0.5 and 1 changes anything. At 1 <i>inclusive</i> the cut takes a cell too
        /// many and leaves single panes standing alone, which <see cref="FindStrandedSpecials"/> refuses.
        /// The tip is spared for its own reason above: a body one unit across has no facets to cut.
        /// </para>
        /// </summary>
        private static BallKind SolitaireKind(int x, int z, int i, int depth)
        {
            if (i >= depth - 1) return BallKind.Normal;

            float rim = SolitaireRim(i);
            if (rim <= 1f) return BallKind.Transparent;

            if (MirageTaxicab(x, z, i, SOLITAIRE_GRID) != rim) return BallKind.Normal;

            float dx = MathF.Abs(MirageDx(x, i, SOLITAIRE_GRID));
            float dz = MathF.Abs(MirageDz(z, i, SOLITAIRE_GRID));

            return MathF.Min(dx, dz) < 1f ? BallKind.Normal : BallKind.Transparent;
        }

        //THE ANVIL. A bell of colour on a stone point: the bottom three courses, which are the lowest and
        //nearest thing in the level and therefore the first thing anybody shoots at.
        private const byte ANVIL_GRID = 16;
        private const byte ANVIL_DEPTH = 10;
        private const float ANVIL_BASE = 2.4f;
        private const float ANVIL_FLARE = 0.42f;
        private const int ANVIL_STAVES = 6;

        /// <summary>
        /// How much of the bell is stone. Three courses is the smallest anvil that reads as one from the
        /// island — one course is a smudge under the colour and two still reads as a shadow — and it is
        /// about an eighth of the level, so the lesson costs the player a ball and not a chapter.
        /// </summary>
        private const int ANVIL_STONE_COURSES = 3;

        private static float AnvilRadius(int i) => ANVIL_BASE + i * ANVIL_FLARE;

        //Orange, yellow, red, magenta: a sunset family, chosen against the granite rather than against the
        //dream. The rock is drawn a flat matte grey and every one of these is saturated and warm, so no
        //stave is ever a colour a player might mistake for stone - which is why neither slate nor black is
        //anywhere in the rock half's five palettes.
        private static readonly BallType[] ANVIL_PALETTE =
            { BallType.Type9, BallType.Type7, BallType.Type1, BallType.Type6 };

        //THE SEAM. A broad body cut corner to corner by a diagonal wall of stone three cells thick, with a
        //different colour family each side of it.
        private const byte SEAM_GRID = 16;
        private const byte SEAM_DEPTH = 10;
        private const float SEAM_BASE = 2.6f;
        private const float SEAM_FLARE = 0.36f;
        private const int SEAM_SECTORS = 6;

        /// <summary>
        /// Half the seam's width, in the units <c>dx − dz</c> is measured in. One gives three columns of
        /// stone and three is the minimum: a cross-level step changes <c>dx + dz</c> by −1, 0 or +1 and
        /// leaves <c>dx − dz</c> free to move with it, so a diagonal wall thinner than this has a door in
        /// every course. The axis-aligned walls in <see cref="Keystone"/> and <see cref="Cairn"/> are one
        /// cell for exactly the reason this one cannot be.
        /// </summary>
        private const float SEAM_HALF_WIDTH = 1f;

        private static float SeamRadius(int i) => SEAM_BASE + i * SEAM_FLARE;

        //The two families, and they are as far apart as this palette goes: no member of one is within two
        //steps of any member of the other on the wheel, so which side of the wall a ball is on is readable
        //at a glance and from any angle - which is the whole design.
        private static readonly BallType[] SEAM_COOL =
            { BallType.Type3, BallType.Type5, BallType.Type12 };

        private static readonly BallType[] SEAM_WARM =
            { BallType.Type1, BallType.Type9, BallType.Type7 };

        /// <summary>
        /// Whether a cell is in the wall. Note what <c>dx − dz</c> reduces to: the shift and the axis cancel,
        /// so it is exactly <c>x − z</c> — a whole number on every course whatever the parity, which is why
        /// this seam is three columns wide everywhere and not three on one course and two on the next.
        /// </summary>
        private static bool SeamStone(int x, int z, int i) =>
            MathF.Abs(MirageDx(x, i, SEAM_GRID) - MirageDz(z, i, SEAM_GRID)) <= SEAM_HALF_WIDTH;

        private static BallType SeamColour(int x, int z, int i) =>
            MirageBand(MirageSector(x, z, i, SEAM_GRID, SEAM_SECTORS) + i,
                x - z > 0 ? SEAM_WARM : SEAM_COOL);

        //THE KEYSTONE. A slab standing across the player's view, four bays under three piers of stone, and
        //the piers stopping short of the glass so the top three courses are one continuous span.
        private const byte KEYSTONE_GRID = 16;
        private const byte KEYSTONE_DEPTH = 10;
        private const float KEYSTONE_HALF_X = 6.5f;

        //Two cells thick at the thinner parity, which is the minimum a wall holds together at: a slab one
        //cell thick in Z has half its cross-level neighbours reaching to a Z that is not there. The Pictures
        //region records the same figure for the same reason.
        private const float KEYSTONE_HALF_Z = 2f;

        /// <summary>The first course above the piers — the arch springing, and the keystone's own floor.</summary>
        private const int KEYSTONE_SPRING = 7;

        private static readonly float[] KEYSTONE_PIERS = { -3f, 0f, 3f };

        //Green, cyan, yellow, orange. Four bays, four colours, and the roll below is by TWO a course rather
        //than one - see KeystoneColour for the diagonal weld that costs.
        private static readonly BallType[] KEYSTONE_PALETTE =
            { BallType.Type2, BallType.Type5, BallType.Type7, BallType.Type9 };

        private static bool KeystoneOccupied(int x, int z, int i) =>
            MathF.Abs(MirageDx(x, i, KEYSTONE_GRID)) <= KEYSTONE_HALF_X
            && MathF.Abs(MirageDz(z, i, KEYSTONE_GRID)) <= KEYSTONE_HALF_Z;

        /// <summary>
        /// Whether a cell stands in a pier. Written as <c>|dx − p| ≤ ½</c> rather than <c>dx == p</c>
        /// because the lattice shifts every other course by half a cell: an equality would have found no
        /// column at all on the shifted ones and left a pier with a hole in every second course. The
        /// half-unit form is one column where the course is unshifted and two where it is not, and it blocks
        /// on both — an on-level step changes <c>x</c> by exactly one and a cross-level step by nought or
        /// one, so nothing crosses a missing column.
        /// </summary>
        private static bool KeystonePier(int x, int z, int i)
        {
            if (i >= KEYSTONE_SPRING) return false;

            float dx = MirageDx(x, i, KEYSTONE_GRID);

            foreach (float pier in KEYSTONE_PIERS)
                if (MathF.Abs(dx - pier) <= HALF) return true;

            return false;
        }

        private static int KeystoneBay(int x, int i)
        {
            float dx = MirageDx(x, i, KEYSTONE_GRID);
            int bay = 0;

            foreach (float pier in KEYSTONE_PIERS)
                if (dx > pier) bay++;

            return bay;
        }

        /// <summary>
        /// One colour a bay, turned <b>two</b> palette steps a course. One step would put a bay's colour at
        /// course <i>i</i> back on its neighbour's at course <i>i</i>−1, and above the springing — where
        /// there is no pier between them — those two are cross-level neighbours: the four bays would weld
        /// into one diagonal group across the whole span. At two steps a course no neighbour of a cell wears
        /// its colour, sideways, vertically or diagonally.
        /// </summary>
        private static BallType KeystoneColour(int x, int z, int i) =>
            MirageBand(KeystoneBay(x, i) + 2 * i, KEYSTONE_PALETTE);

        //THE CAIRN. A round body quartered top to bottom by two crossed walls of stone - the block's last
        //statement about what a rock does to a colour group, and the only level in the game where one colour
        //standing in one silhouette is four groups that can never meet.
        private const byte CAIRN_GRID = 16;
        private const byte CAIRN_DEPTH = 10;
        private const float CAIRN_BASE = 3.4f;
        private const float CAIRN_FLARE = 0.26f;
        private const int CAIRN_SECTORS = 8;

        private static float CairnRadius(int i) => CAIRN_BASE + i * CAIRN_FLARE;

        //Green, cyan, blue, magenta - four colours the chambers carry in four different orders, which is
        //what makes the division READ: the same colour is in a different place in every quarter, so the eye
        //cannot join two quarters into one pattern and stops trying.
        private static readonly BallType[] CAIRN_PALETTE =
            { BallType.Type2, BallType.Type5, BallType.Type3, BallType.Type6 };

        /// <inheritdoc cref="KeystonePier"/>
        private static bool CairnWall(int x, int z, int i) =>
            MathF.Abs(MirageDx(x, i, CAIRN_GRID)) <= HALF || MathF.Abs(MirageDz(z, i, CAIRN_GRID)) <= HALF;

        private static BallType CairnColour(int x, int z, int i)
        {
            //Unambiguous on every cell this is asked about: a cell off the walls is at least a whole unit
            //from both axes, so neither sign is ever read off a zero.
            int quarter = (MirageDx(x, i, CAIRN_GRID) > 0f ? 1 : 0)
                          + (MirageDz(z, i, CAIRN_GRID) > 0f ? 2 : 0);

            return MirageBand(MirageSector(x, z, i, CAIRN_GRID, CAIRN_SECTORS) + i + quarter, CAIRN_PALETTE);
        }

        //THE OBSIDIAN. A solid stepped octahedron with a vein of stone spiralling up it, stopping one course
        //short of the glass (#343) - the campaign's last level.
        private const byte OBSIDIAN_GRID = 17;
        private const byte OBSIDIAN_DEPTH = 12;
        private const int OBSIDIAN_VEIN_SECTORS = 8;
        private const int OBSIDIAN_COLOUR_SECTORS = 4;

        //Turns a course. A ninth of a turn walks the vein a full sector every one and a bit courses, so the
        //stone is a screw rather than a stripe - and the number is deliberately NOT a neat fraction of a
        //sector, or the vein would step in place and read as a broken column.
        private const float OBSIDIAN_TWIST = 0.09f;

        private static int ObsidianRim(int i) => 1 + 2 * (i / 3);

        //Red, orange, magenta, navy, blue - five for a finale, and warm through cool so the wedges of a
        //course read as a wheel turning rather than as a list. Nothing here is near the granite's grey.
        private static readonly BallType[] OBSIDIAN_PALETTE =
        {
            BallType.Type1, BallType.Type9, BallType.Type6, BallType.Type12, BallType.Type3,
        };

        private static bool ObsidianVein(int x, int z, int i) =>
            SectorIndex(MirageAngle(x, z, i, OBSIDIAN_GRID), OBSIDIAN_TWIST * i, OBSIDIAN_VEIN_SECTORS) == 0;

        private static BallType ObsidianColour(int x, int z, int i) =>
            MirageBand((int)MathF.Floor(MirageTaxicab(x, z, i, OBSIDIAN_GRID) * HALF)
                       + MirageSector(x, z, i, OBSIDIAN_GRID, OBSIDIAN_COLOUR_SECTORS) + i, OBSIDIAN_PALETTE);

        /// <summary>
        /// The vein, and <b>nothing on the anchor course</b> (#343).
        /// <para>
        /// <b>⚠ This retracts the finale's original statement, and the retraction is the owner's.</b> It used
        /// to cast the anchor course's outer rim in stone as well, and the argument for it was sound as far as
        /// it went: the top level is the only one bonded to the ceiling plate, so a stone rim there is a set
        /// of anchors no shot can ever take and <see cref="WorstAnchorLoad"/> cannot be made worse by
        /// anything the player does. What it missed is who that is a good deal for. Fifty-eight of this
        /// level's 113 anchors were rock, and the player finished the campaign looking at a ring of stone
        /// they had no way to touch — see the ANCHORING paragraph of <see cref="FindStrandedSpecials"/>.
        /// </para>
        /// <para>
        /// The vein stops one course short for the same reason and by the same rule as
        /// <see cref="Seam"/> and <see cref="Cairn"/>, which is <see cref="Keystone"/>'s: <b>stone stops
        /// short of the glass</b>. It costs the finale no cell and no silhouette — the rim's cells and the
        /// vein's top wedge are still occupied and still drawn, they simply carry a colour now — and what
        /// the level gives up is a guarantee about the ceiling that it buys back the ordinary way, by
        /// measurement (the anchor load below).
        /// </para>
        /// </summary>
        private static BallKind ObsidianKind(int x, int z, int i, int depth) =>
            i < depth - 1 && ObsidianVein(x, z, i) ? BallKind.Rock : BallKind.Normal;

        #endregion
    }
}
