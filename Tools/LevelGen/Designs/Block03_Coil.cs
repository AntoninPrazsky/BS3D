using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using Microsoft.Xna.Framework;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>The Coil</b>, block 3 of the campaign: its designs, and the helpers no other block's designs use, in
    /// the order <c>Program.cs</c> held them — the play order is <see cref="Main"/>'s, and the block's name, music
    /// and ball style are in the tables there. Split out of <c>Program.cs</c> in #386.
    /// </summary>
    internal static partial class Program
    {

        #region The coiled levels (#207)

        //EVERY LEVEL HERE HAS A SUBJECT AND ITS PALETTE HAS TO SAY SO (#285). All five shipped with a colour
        //rule that was nothing but the group topology — one free ink per member, so a crane came out with a
        //green mast, a magenta stay, a silver-and-blue jib and a brown-and-white counterweight, and the owner
        //read the block as "like a child randomly tried colours". The topology is LOAD-BEARING and none of it
        //moved: which member gets its own ink, which pairs are banded so a released colour thins a beam
        //instead of severing it, and which are diagonal so no ink lists a rope both ways, all stand exactly as
        //they were. What changed is only WHICH ink each member wears, so every group, every best-single-shot
        //figure and every lone/pair count in the doc comments below is untouched by construction.
        //
        //The bar is Horn (#33): its rule is nothing but reading what is coming — white core, red flesh, gold
        //skin, three shells ordered by radius, so the object reads as designed rather than as parts. A rig
        //cannot be ringed by radius, so the equivalent here is a SUBJECT: an orange-and-yellow crane on a grey
        //steel jib with a red load; a Calder mobile, primaries hung on black-and-grey wire; a suspension bridge
        //in International Orange over a grey roadway; a jewel on a gold chain and a steel one; a web with the
        //one warm thing in it being the spider. Where two inks touch they are checked against measured
        //CIEDE2000 (.claude/skills/screenshot/palette.ps1 -Focus), which is what kept red off orange and
        //yellow off white.

        //Helix was the level the author singled out - "right after launch it bounces like a spring, which
        //looks great and finally exploits the physics potential of the game" - and this block is the ask that
        //came out of it: MORE of that, variously interconnected, and in the desert, which no level used.
        //
        //WHAT MAKES A LAYOUT SPRING is not its silhouette, it is how few and how slender the links holding it
        //up are. The cluster is one Bepu body per ball tied to its neighbours by BallSocket constraints and to
        //the glass along the field's top level, so a wide solid slab is stiff by construction and a long thin
        //member is not. That is the whole style of the block, and it is also its one structural danger, because
        //slender links are exactly what the drop test refuses: everything below a severed link is orphaned.
        //Every design here therefore carries a SECOND load path, and what that second path is differs per level:
        //
        //  Rope     - three strands that periodically pinch together, so each hangs off the other two.
        //  Minaret  - a ledge and the core it winds round, tied at every level: cut either and the other holds.
        //  Basket   - two families of ribs winding opposite ways, so the shell is a mesh and not a set of lines.
        //  Pendulum - four ropes in two colours, so no one colour can cut the weight loose.
        //  Knot     - a CLOSED loop touching the glass three times: one cut leaves an arc, not a falling piece.
        //
        //None of them is tall. That is deliberate and it is the Tower's boundary being respected rather than an
        //oversight - "the layout is deeper than the camera frames" is that block's whole statement, and a second
        //block of tall levels would take it away. These are framed whole and swing inside the frame. (#182 has
        //since reversed that boundary deliberately - the Nebula is a second tall block, at the owner's ask, and
        //its region says why the Tower's statement survives it. This block's reasoning stays as it was made.)

        /// <summary>
        /// The desert's field: eighteen deep against a layout of <see cref="DUNES_DEPTH"/>, which is the same
        /// arithmetic <see cref="REVEAL_FIELD_LEVELS"/> arrives at and a different reason for it.
        /// <para>
        /// A field deeper than sixteen is <b>raised off the death line</b> rather than pinned at
        /// <c>FIELD_TOP_Y</c> (seventeen is the first depth raised), so the cluster hangs 1.36 higher and its
        /// lowest layout ball starts 4.74 above the line instead of 3.38. On a block whose whole point is that
        /// the cluster <i>swings</i>, that clearance is not a luxury: #203 is the same fact arriving as a bug
        /// report on the picture levels, where gravity pulls part of the layout down into a narrow stalk and
        /// the stalk crosses the line. Six empty levels under the layout are the room that swing needs.
        /// </para>
        /// <para>
        /// <b>Eighteen and not twenty</b>, for the Reveal's reason as much as this one: <c>FRAMED_LEVELS</c> is
        /// 18, so an 18-level field is the deepest one still framed <b>whole</b>, and a block of levels the
        /// camera framed from the floor up would be a second Tower.
        /// </para>
        /// </summary>
        private const byte DUNES_FIELD_LEVELS = 18;

        /// <summary>
        /// How deep a coiled layout is drawn. Twelve leaves the six empty levels <see cref="DUNES_FIELD_LEVELS"/>
        /// is chosen for, and the offset it implies is even, which <see cref="Emit"/> requires. Only
        /// <see cref="Pendulum"/> departs from it, and says why.
        /// </summary>
        private const byte DUNES_DEPTH = 12;

        /// <summary>
        /// The dome the whole block hangs under: a <b>cool turquoise sky over warm sand</b>, the desert's own
        /// late light. Picked by looking, against 2, 10, 11, 16 and 18, and on two things a palette table
        /// cannot say.
        /// <para>
        /// <b>It has to be a dome no other block owns.</b> The meadow's 1 is a clear blue day and the
        /// mountains' 8 a pink violet, and every warm-sunset candidate here turned out to be a <i>pink</i> one
        /// in play — 2, 10 and 18 all read within a hair of the mountains, because a dome's fiery lower rings
        /// sit below the horizon where the scene's own terrain covers them, and only its upper rings are ever
        /// seen. Six is the one dome in the eighteen that is cool where the ground is warm, which is the
        /// desert postcard and belongs to nothing else.
        /// </para>
        /// <para>
        /// <b>And the balls have to read against it.</b> 16 (a near-black zenith over pale sand) is the most
        /// striking frame of the six and makes the cluster pop hardest, and it was passed over for the
        /// campaign's light: it is darker than the violet dusk that follows it, so the block after this one
        /// would be a step back into the light. Six keeps the drain going — green noon, gold afternoon, the
        /// desert's cool late light, violet dusk, underground dark, airless black.
        /// </para>
        /// </summary>
        private const byte DUNES_SKY = 6;

        /// <summary>
        /// Four strands twisted about a common axis, <b>pinching together and spreading apart</b> as they climb.
        /// It opens the block because it is the shape closest to <see cref="Helix"/> — the player is meant to
        /// recognise the register at once — and the one line that separates them is worth stating: Helix's two
        /// strands are tied by <i>manufactured</i> rungs at a fixed spacing, where these four tie <b>themselves</b>,
        /// wherever the weave brings them together.
        /// <para>
        /// Each strand's angle carries two terms. <see cref="ROPE_TWIST"/> is the steady spin the whole rope
        /// makes, and <see cref="ROPE_WEAVE"/> is a slow oscillation given a quarter turn of phase per strand —
        /// so the four do not move together, and their angular separation breathes about its resting 90°. Where
        /// two of them come closest their discs overlap and the strands are one body; where they are furthest
        /// apart the rope is four separate lines with daylight between them, which is the whole reason it swings.
        /// </para>
        /// <para>
        /// <b>The weave amplitude is the load-bearing number</b> and it is bounded on both sides. Too little and
        /// the strands never meet, which is four chains hanging side by side and a level that ends on the first
        /// colour cut near the glass. Too much and they fuse into one blob and the level is a cylinder. 0.60 rad
        /// puts the closest approach at 41°, a chord of 2.12 against two strand radii of 2.6 — an overlap of
        /// half a cell, which is a touch and not a merge. Opposite strands never come nearer than 111°, so the
        /// rope has a hole down the middle rather than a core.
        /// </para>
        /// <para>
        /// <b>It shipped first as THREE strands of 1.6 at a radius of 2.5, and a photograph refused it.</b> Every
        /// gate passed — 285 balls, margin 2, nothing alone, nothing recoloured — and in the running game it was
        /// a shapeless column with no twist visible anywhere in it: at 120° apart, centres 4.3 apart and strands
        /// 3.2 across leave barely a cell between neighbours, and the lattice rounds that away. The check that
        /// catches this is the <c>screenshot</c> skill and nothing else here can. Four thinner strands on a wider
        /// circle cost thirty balls and bought the shape.
        /// </para>
        /// <para>
        /// Measured: 252 balls — the lightest level of the block, and lighter than anything in the pack but One
        /// and Bullseye — margin 2, nothing alone, nothing in a pair, nothing recoloured. Best single shots 9 %,
        /// 9 %, 8 % and 8 % on groups of 21–23, which is as even as this pack gets.
        /// </para>
        /// </summary>
        private static Design Rope() => new()
        {
            File = "Rope.json",
            Name = "Rope",
            Grid = 13,
            Depth = DUNES_DEPTH,
            FieldLevels = DUNES_FIELD_LEVELS,
            Scene = SceneKind.Desert,
            Sky = DUNES_SKY,
            Music = MUSIC_COIL,
                Balls = BALLS_COIL,
            Shots = 40,
            CeilingStep = 8,
            Occupied = (r, ang, i, depth) => RopeStrand(r, ang, i) != 0,
            //Courses along each strand rather than an ink per strand: a whole strand in one ink is a group of
            //sixty and a third of the level goes on one ball. The course is rolled by TWO against four inks and
            //the strand index by one, which is Lantern's rule and the same arithmetic: neighbouring strands
            //differ by one at every level, and a strand meeting its neighbour ACROSS a course boundary differs
            //by one or three. Rolled by one instead — which is what this shipped with first — that diagonal
            //neighbour is the same ink, and the three courses of a colour fuse into one group of 64.
            Colour = (r, ang, i, depth) => Band(2 * (i / ROPE_COURSE) + RopeStrand(r, ang, i),
                new[] { BallType.Type1, BallType.Type7, BallType.Type2, BallType.Type5 }),
        };

        /// <summary>
        /// A slender core with a <b>ledge winding round it</b> — the spiral minaret at Samarra, which is a
        /// desert building and the one shape in the pack that is two things at once. The core is what hangs off
        /// the glass and the ramp is what swings, and they are tied along the ramp's whole inner edge at every
        /// level, which is what makes the pair safe: <b>they are two load paths, not one</b>. Cut a course of
        /// the core and everything under it still hangs by the ramp; cut a run of the ramp and it still hangs
        /// by the core.
        /// <para>
        /// That redundancy is stated in the palettes and not only in the geometry. The core is coloured out of
        /// <b>its own two inks</b> and the ramp out of three others, so no group can ever span both — a core
        /// course and the ramp beside it are always different colours, and a shot that takes one cannot take
        /// the other with it.
        /// </para>
        /// <para>
        /// The wedge is 63° against a turn of 40° a level, so consecutive courses of the ramp still overlap by
        /// 23°. Under 40° they would not overlap at all and the ramp would be a stack of separate shelves
        /// hanging off the core, which is a different level and a worse one.
        /// </para>
        /// <para>
        /// <b>The first cut ran the wedge at 82° and it did not read as a ramp.</b> A quarter of the ring at
        /// every level, over the 1.3 turns a twelve-level layout allows, photographs as scattered lumps rather
        /// than as one thing winding — the same failure <see cref="Rope"/> had and the same check that found it.
        /// A ribbon needs to be narrow and long, so the wedge came in to 63° and <see cref="MINARET_OUTER"/>
        /// went out from 4.6 to 5.3 in the same change; that costs nothing, since a 15-wide field carries
        /// anything under 5.5 at margin 2.
        /// </para>
        /// <para>
        /// Measured: 293 balls, margin 2, nothing alone, nothing in a pair, nothing recoloured, and the
        /// <b>flattest colour spread in the whole pack</b> — best single shots 15 %, 14 %, 14 %, 13 %, 14 %.
        /// That evenness is the two-palette rule paying out rather than luck: five inks over a shape whose two
        /// parts are coloured independently cannot pile up on one of them.
        /// </para>
        /// </summary>
        private static Design Minaret() => new()
        {
            File = "Minaret.json",
            Name = "Minaret",
            Grid = 15,
            Depth = DUNES_DEPTH,
            FieldLevels = DUNES_FIELD_LEVELS,
            Scene = SceneKind.Desert,
            Sky = DUNES_SKY,
            Music = MUSIC_COIL,
                Balls = BALLS_COIL,
            Shots = 44,
            CeilingStep = 8,
            Occupied = (r, ang, i, depth) => r <= MINARET_CORE || MinaretRamp(r, ang, i),
            Colour = (r, ang, i, depth) => r <= MINARET_CORE
                ? Band(i / MINARET_COURSE_CORE, new[] { BallType.Type4, BallType.Type3 })
                : Band(i / MINARET_COURSE_RAMP, new[] { BallType.Type1, BallType.Type7, BallType.Type6 }),
        };

        /// <summary>
        /// A <b>hollow woven shell</b>: two families of ribs on one cylinder, one winding up to the right and
        /// one up to the left, crossing wherever they meet. It is the block's answer to the question the other
        /// four dodge — what a coiled level looks like when it is <i>enclosed</i> rather than a set of lines —
        /// and it is the springiest thing here, because a two-cell wall with holes in it can breathe.
        /// <para>
        /// <b>The weave is its own safety.</b> Every crossing is a join, so the shell is a mesh: there is no
        /// single cut anywhere below the rim that separates a piece of it from the glass, and the drop test
        /// reads it as one of the tightest levels in the pack rather than one of the loosest.
        /// </para>
        /// <para>
        /// <b>The rim is not decoration.</b> The top <see cref="BASKET_RIM"/> levels are a solid course all the
        /// way round, because the ribs alone reach the glass at six small patches and a basket hanging by six
        /// patches is a basket that tears. It is coloured by <i>sector</i> for the reason the whole block
        /// exists: one ink round the rim is one group holding the entire level, which is the anchor trap in its
        /// purest form. Six sectors over four inks leaves no two neighbours alike, wrap included.
        /// </para>
        /// <para>
        /// Measured: 280 balls, margin 2, nothing alone, nothing in a pair, nothing recoloured, best single
        /// shots <b>7 %, 9 %, 10 % and 11 %</b> — the tightest level of the block by a distance and near the
        /// bottom of the pack's whole band, which is the mesh doing exactly what it is for. It is also the
        /// lightest here, so the figure to watch in play is the budget rather than the groups.
        /// </para>
        /// <para>
        /// <b>Thirteen wide and not fifteen.</b> The shell reaches 4.4, which leaves three free columns in a
        /// 15-wide field — a wider glass plate and a longer camera stand-off bought for nothing. Thirteen is
        /// the narrowest field that still gives <see cref="LateralMargin"/> the two columns it wants here.
        /// </para>
        /// </summary>
        private static Design Basket() => new()
        {
            File = "Basket.json",
            Name = "Basket",
            Grid = 13,
            Depth = DUNES_DEPTH,
            FieldLevels = DUNES_FIELD_LEVELS,
            Scene = SceneKind.Desert,
            Sky = DUNES_SKY,
            Music = MUSIC_COIL,
                Balls = BALLS_COIL,
            Shots = 48,
            CeilingStep = 7,
            Occupied = (r, ang, i, depth) =>
                BasketWall(r) && (BasketIsRim(i, depth) || BasketRib(ang, i) != 0),
            Colour = (r, ang, i, depth) =>
            {
                BallType[] palette = { BallType.Type1, BallType.Type7, BallType.Type2, BallType.Type5 };

                return BasketIsRim(i, depth)
                    ? Band(BasketSector(ang), palette)
                    : Band(BasketRib(ang, i) + i / BASKET_COURSE, palette);
            },
        };

        /// <summary>
        /// A weight hanging from the glass on <b>four ropes</b> — the block's pendulum, and the one level here
        /// whose swing the player can start on purpose, because a shot into the bulb shoves a mass that is held
        /// by almost nothing.
        /// <para>
        /// <b>Two inks over four ropes, opposite ropes alike.</b> One ink per rope would be four colours spent
        /// on the thinnest thing in the level; one ink over all four would hand a single shot the whole weight.
        /// Paired diagonally, a colour taken cuts two ropes and leaves the other two — the bulb keeps hanging,
        /// on half the suspension it had, from two corners instead of four. Nothing is orphaned and the level
        /// visibly gets worse to aim at, which is the best thing a shot can do.
        /// </para>
        /// <para>
        /// The bulb is coloured in <see cref="Mosaic"/>'s 3×3×3 masonry rather than in gores. Gores were tried
        /// on paper and are the wrong rule for a solid of revolution hanging by its shoulders: every gore
        /// converges on the pole, so opposite gores of one ink meet there and become a single group spanning
        /// the whole body. Blocks have no pole.
        /// </para>
        /// <para>
        /// <b>Fourteen deep and not <see cref="DUNES_DEPTH"/></b>: the ropes are the level, and six levels of
        /// rope over an eight-level bulb reads as a lamp sitting on a shelf rather than as a weight on a line.
        /// The offset stays even, which is what <see cref="Emit"/> requires.
        /// </para>
        /// <para>
        /// Measured: 384 balls — the heaviest of the block — margin 2, nothing standing alone, <b>2 in pairs
        /// and 1 recoloured</b>, all three at the same cell on the bulb's crown, where a 3×3×3 block is clipped
        /// by the ellipsoid down to a sliver. That is the repair pass's own remit and the count is at the low
        /// end of what the pack ships (Prism 4 in pairs, Static 14). Best single shots 10 %, 11 %, 15 %, 20 %
        /// and 10 %; the 20 % is a rope pair, which is the shot this level is designed around.
        /// </para>
        /// </summary>
        private static Design Pendulum() => new()
        {
            File = "Pendulum.json",
            Name = "Pendulum",
            Grid = PENDULUM_GRID,
            Depth = 14,
            FieldLevels = DUNES_FIELD_LEVELS,
            Scene = SceneKind.Desert,
            Sky = DUNES_SKY,
            Music = MUSIC_COIL,
                Balls = BALLS_COIL,
            Shots = 52,
            CeilingStep = 7,
            OccupiedBlock = (x, z, i, depth) => PendulumRope(x, z, i) != 0 || PendulumBulb(x, z, i),
            BlockColour = (x, z, i) =>
            {
                int rope = PendulumRope(x, z, i);

                //The rope wins in the shoulder, where rope and bulb overlap, so a rope reads as entering the
                //weight rather than as stopping on top of it
                if (rope != 0) return rope is 1 or 4 ? BallType.Type1 : BallType.Type7;

                return Band(x / 3 + z / 3 + i / 3,
                    new[] { BallType.Type3, BallType.Type4, BallType.Type5 });
            },
        };

        /// <summary>
        /// A <b>trefoil</b> — the (2, 3) torus knot — hanging from the three points where it touches the glass.
        /// It closes the block and it is the only <i>closed loop</i> in the game: every other layout in the pack
        /// has ends, and a loop is the one topology on which a single cut cannot drop anything at all. Take an
        /// arc out anywhere and what is left is still an arc hanging off the other two anchors.
        /// <para>
        /// That is what buys the level its difficulty. Six arcs over three inks means no ink holds more than a
        /// sixth of the knot and the three anchors are three different colours by construction (the loop's
        /// three high points fall in arcs 0, 2 and 4), so <b>nothing here cascades</b>: it is worked round the
        /// loop a group at a time, which is why it carries the block's loosest shot budget against its smallest
        /// groups.
        /// </para>
        /// <para>
        /// <b>The knot is hung by its top and not by its middle.</b> The layout's top level is the one bonded to
        /// the glass, so the vertical mapping puts <see cref="KNOT_RISE"/> — the curve's own highest point —
        /// exactly on it. Centred instead, the top level would be empty and the whole level would hang off
        /// nothing; the loader would build it and it would fall on the first frame.
        /// </para>
        /// <para>
        /// It is drawn as the set of cells within <see cref="KNOT_TUBE"/> of a curve sampled
        /// <see cref="KNOT_SAMPLES"/> times, which is the one shape here that cannot be solved from a radius.
        /// <see cref="KNOT_MINOR"/> must stay clear of <see cref="KNOT_TUBE"/>: the loop passes its own far side
        /// at twice the minor radius, so a tube fatter than that welds the crossings shut and the pretzel
        /// becomes a lump.
        /// </para>
        /// <para>
        /// Measured: 325 balls, margin 2, nothing alone, nothing in a pair, nothing recoloured, best single
        /// shots 11 %, 8 % and 10 % on three inks carrying 122, 106 and 97 balls. <b>A fatter tube was tried
        /// and refused</b>: at 1.3 the same knot is 400 balls, which is the weight this level would rather
        /// have, but the extra quarter cell welds enough crossings that twelve arcs no longer colour on three
        /// inks at all — see <see cref="KNOT_INKS"/> for the contact graph both figures come off. A fourth ink
        /// on the block's last level costs more than seventy-five balls are worth.
        /// </para>
        /// </summary>
        private static Design Knot() => new()
        {
            File = "Knot.json",
            Name = "Knot",
            Grid = 15,
            Depth = DUNES_DEPTH,
            FieldLevels = DUNES_FIELD_LEVELS,
            Scene = SceneKind.Desert,
            Sky = DUNES_SKY,
            Music = MUSIC_COIL,
                Balls = BALLS_COIL,
            Shots = 50,
            CeilingStep = 6,
            Occupied = (r, ang, i, depth) => KnotDistance(r, ang, i, depth, out _) <= KNOT_TUBE,
            Colour = (r, ang, i, depth) =>
            {
                KnotDistance(r, ang, i, depth, out int arc);
                return Band(KNOT_INKS[arc], new[] { BallType.Type1, BallType.Type7, BallType.Type3 });
            },
        };

        #endregion

        #region The Coil's second hang (#255)

        //THE COIL'S SECOND FIVE (#255), hung after Knot on the same wire: the desert, DUNES_SKY, MUSIC_COIL
        //and the block's standing rule that a layout hangs on SLENDER LINKS, so the cluster springs and
        //swings instead of sitting there. What the second hang adds is what the first five had no room for -
        //MACHINES. Every level here is members with named jobs (a chain, a thread, a stay, a beam, a cable),
        //so the player takes the structure apart in an order they choose and a different collapse answers
        //each order.
        //
        //THE ORDER IS A RAMP OF HOW MANY LOAD PATHS THE PLAYER HAS TO HOLD AT ONCE:
        //  Pendant - two strands and a tie: one triangle, one hanging weight, one all-or-nothing jackpot.
        //  Web     - one body on five threads: the same earned swing staged five times over, 5-4-3-2-1.
        //  Crane   - two independent glass anchors: cut either and the machine still stands on the other.
        //  Mobile  - beams on ropes on beams: three swings at three depths, and shot ORDER decides them.
        //  Bridge  - two whole systems, cable-and-hanger above and beam-on-bearings below.
        //
        //EVERY MEMBER IS TWO CELLS DEEP IN Z (RIG_ROW_LOW) unless its own doc says otherwise, and every
        //table in the region is written in the CENTRED index frame RigCentre hands back. Two cells is the
        //narrowest member the lattice keeps solid whatever the level parity, and seating them all on one
        //row is what makes these machines read as drawings from the opening +Z view.
        //
        //THE PARITY RULE ALL FIVE ARE SOLVED AGAINST, stated once because every table here depends on it:
        //BallsMap.GetNeighboringCells gives a cell on an ODD level the four cells (x..x+1, z..z+1) on each
        //adjacent level, and a cell on an EVEN level the four cells (x-1..x, z-1..z). So a member that keeps
        //its columns from one level to the next always touches the next one, and a 2-wide row that steps by
        //at most ONE column touches it on BOTH parities - which is why every sloped strand and every stepped
        //stay in the region moves one column at a time and never two. The field offset is even on all five
        //(Emit refuses an odd one), so the layout index and the field level share a parity and the rule can
        //be read straight off i.

        /// <summary>
        /// The z pair every member of this region is two cells deep in - the row seated on the field's own
        /// axis, which falls on the seam between -1 and 0 rather than on a cell.
        /// </summary>
        private const int RIG_ROW_LOW = -1;

        private static bool RigRow(int cz) => cz == RIG_ROW_LOW || cz == RIG_ROW_LOW + 1;

        private static bool RigWithin(int value, int low, int high) => value >= low && value <= high;

        /// <summary>
        /// The centred lattice indices of a raw cell - x and z counted from the middle column instead of
        /// from the field's corner, which is the frame every member table in this region is written in.
        /// <para>
        /// It is the integer sibling of <see cref="PendulumOffsets"/> and deliberately not that function: a
        /// machine here is a set of members whose cells are named one by one, so what a table needs is the
        /// index a drawing would use, not the world offset a disc is measured in. Every grid in the region
        /// is odd, so the middle column is a cell and not a seam, and the grid is passed in because these
        /// designs read the RAW indices and have to find the field's middle themselves - Lean's reason, and
        /// the same trap if a design's own grid constant and its <see cref="Design.Grid"/> ever disagree.
        /// </para>
        /// </summary>
        private static void RigCentre(int x, int z, int grid, out int cx, out int cz)
        {
            int centre = (grid - 1) / 2;

            cx = x - centre;
            cz = z - centre;
        }

        /// <summary>
        /// Which diagonal of a 2x2 member a cell is on, 0 or 1 - <see cref="Pendulum"/>'s trick, stated once
        /// for the three machines that hang something on a four-cell link. A colour taken cuts one diagonal
        /// and leaves the other, so no single ink ever severs a rope, a hanger or a hook.
        /// <para>
        /// A diagonal is a group rather than two lonely balls only because the member is more than one level
        /// tall: two cells that touch nothing on their own level are still both connected to the cell
        /// straight above them, and on an odd level a cell also reaches its diagonal partner one level up.
        /// A one-level 2x2 in two inks would be two balls standing alone, which is why nothing here is one.
        /// </para>
        /// <para>
        /// Normalised before it is returned: the centred indices go negative and <see cref="Band"/> indexes
        /// with a bare %.
        /// </para>
        /// </summary>
        private static int RigDiagonal(int cx, int cz) => ((cx + cz) % 2 + 2) % 2;

        /// <summary>
        /// A jewel on a necklace: a nested point-down pyramid hanging from a V of two slender chains, closed
        /// into a triangle by a tie beam and hung off a jump ring. It opens the second hang because it is the
        /// smallest machine here and the only one that welds both of the owner's asks into ONE object - the
        /// packing pyramid is not the level, it is the WEIGHT the physics swings.
        /// <para>
        /// <b>The stone is the packing made jewellery.</b> Six courses, 6-5-4-3-2-1, each sitting exactly in
        /// the pockets of the one above: the layout offset is even so a course's parity is its layout index's,
        /// and the arithmetic that follows is <c>dx = cx + shift - 0.5</c> — an even course's cells land on
        /// halves and an odd course's on the midpoints between them, which is One's trick miniaturised and
        /// HUNG, so the facets catch every swing.
        /// </para>
        /// <para>
        /// <b>⚠ IT WAS FOUR COURSES AND THAT IS WHY NOBODY COULD SEE WHAT THIS LEVEL WAS (#360).</b> At four
        /// the stone was exactly as wide as the jump ring above it and a third of the drawing's height, so
        /// the eye read a lump on a string; the chains, five levels long and stepping every second one, were
        /// the biggest thing in the picture. A pendant is read by its stone. Six courses and four levels of
        /// chain put two thirds of the height into the jewel and make it half again as wide as its ring —
        /// the same object, drawn at the proportions it actually has.
        /// </para>
        /// <para>
        /// <b>The knot is a jump ring and not a block</b> (the graft off the rejected Chain): a closed loop
        /// two cells thick with a 2x2 eye, its top bar being exactly the two strand feet and its bottom bar
        /// the neck the stone hangs from. Either side of the ring carries the stone if the other is bitten,
        /// so the loop is a structural second load path at the very point the whole weight passes through.
        /// </para>
        /// <para>
        /// <b>The four inks across two strands are the safety.</b> No single colour severs both chains, and
        /// each chain's own 2-level runs mean one release removes at most a 2-level bite; cut a strand
        /// between the tie and the ring and the pendant hangs through the ring off the other one, with the
        /// cut strand's stub still held by the tie. Brown is the jackpot and is deliberately the only
        /// all-or-nothing shot in the level: the ring's 24 balls drop the 91-ball stone with them, 72 % of the
        /// 147 the tables above count - the designed payoff, and still under <see cref="ONE_SHOT_PERCENT"/>.
        /// It was 44 % of 104 while the stone was four courses; a jewel that is most of its own level is a
        /// jackpot that is most of its own level, and that is the trade #360 made deliberately. The stone's
        /// own courses are banded alternately so the SECOND big shot - cutting the neck course and dropping
        /// everything under it - stays at 61 % rather than taking the stone in one ink. The tie is brown too
        /// and stands clear of the ring, so the two never fuse into one group.
        /// </para>
        /// <para>
        /// Gate watch (#255), in the order the spec asked for it. FIRST the sloped strands' cross-level
        /// contacts: both tables step by at most one column per level, which the region's parity rule makes
        /// safe on both parities, but the tool is what says so - if a pair ever loses contact, hold that
        /// x-pair for one extra level rather than widening the row. SECOND the unshot sag of the stone on
        /// two 2-wide strands against the death line; if it stretches, shorten the chains by one level
        /// (raise <c>PENDANT_RING_TOP</c> and drop an entry from the strand table with it) and never touch
        /// the stone. It reads 0 of 5 on the sag probe with the six-course stone and clears in 20 of its 42
        /// shots, which is the same 0 the four-course build read. THIRD that the nested
        /// courses really land in the pockets - the window is the LOW one on both axes, and the even field
        /// offset is what preserves it.
        /// </para>
        /// </summary>
        private static Design Pendant() => new()
        {
            File = "Pendant.json",
            Name = "Pendant",
            Grid = PENDANT_GRID,
            Depth = PENDANT_DEPTH,
            //Eighteen against a layout of fourteen: an even offset, and the four empty field levels plus the
            //empty layout level under the tip are the room a hanging stone needs to swing into
            FieldLevels = DUNES_FIELD_LEVELS,
            Scene = SceneKind.Desert,
            Sky = DUNES_SKY,
            Music = MUSIC_COIL,
                Balls = BALLS_COIL,
            Shots = 42,
            CeilingStep = 8,
            OccupiedBlock = (x, z, i, depth) =>
            {
                RigCentre(x, z, PENDANT_GRID, out int cx, out int cz);

                return PendantStrand(cx, cz, i) != 0 || PendantRing(cx, cz, i)
                    || PendantTie(cx, cz, i) || PendantStone(cx, cz, i);
            },
            BlockColour = PendantColour,
        };

        //THE PENDANT'S OWN FIGURES. Everything lives between centred x -5 and 4 in a grid of 13, so one free
        //column stands all round (LateralMargin's rule) with two on the +X side.
        private const byte PENDANT_GRID = 13;
        private const byte PENDANT_DEPTH = 14;

        //The chains: the left strand's LOW x column at each level it exists on, its foot first. The right
        //strand is this table mirrored about x = -0.5 (low column -low - 2), which is the mirror the lattice
        //itself has, since a member two cells deep is seated on the same seam.
        //
        //⚠ FOUR LEVELS AND NOT FIVE, STEPPING EVERY LEVEL (#360). The chains used to run five levels and
        //step every second one, which made them the longest thing in the drawing - and a pendant is read by
        //its STONE, with the chain as the line that points at it. A playtest read this level as "small,
        //unclear what it is meant to be". Shorter chains over a stone half again as tall put two thirds of
        //the height into the jewel, which is the proportion a pendant actually has.
        private const int PENDANT_STRAND_FOOT = 10;
        private static readonly int[] PENDANT_STRAND_X = { -2, -3, -4, -5 };

        /// <summary>How many levels one ink of a strand runs for. Four inks over two strands, two each.</summary>
        private const int PENDANT_RUN = 2;

        //The jump ring: a closed loop from the neck bar up to the shoulder bar, four columns wide, with only
        //its sides standing between them - which is what leaves the 2x2 eye. The shoulder bar IS the two
        //strand feet (their tables land on exactly these four columns), so it is coloured as the strands are
        //and the brown loop starts one level below it.
        private const int PENDANT_RING_LOW = -2;
        private const int PENDANT_RING_BOTTOM = 7;
        private const int PENDANT_RING_TOP = 10;

        //The tie beam that closes the triangle: ONE level, spanning the gap the two strands leave at that
        //height, so it touches both of them along its own course.
        //
        //⚠ It was two levels and it cannot be any more (#360): the strands step outward every level now, so
        //a bar wide enough to reach them on its lower course falls a column short on the upper one. One
        //course is also the whole of what it has to be - a tie is a line, and drawn two deep between two
        //chains it was the lumpy crossbar that made the top of this level unreadable.
        private const int PENDANT_TIE_BOTTOM = 12;
        private const int PENDANT_TIE_HIGH = 1;

        //The stone: six nested courses, the 6x6 against the neck and the tip at the bottom.
        //
        //⚠ SIX AND NOT FOUR (#360). At four courses the stone was exactly as wide as the ring hanging it and
        //barely a third of its height, so ring and stone read as one lump on a string rather than as a jewel
        //on a chain. Six courses make it half again as tall as the ring and half again as wide, which is the
        //first thing the eye lands on - and it is the level's own subject, so it should be.
        private const int PENDANT_STONE_TOP = 6;
        private const int PENDANT_STONE_COURSES = 6;
        private const int PENDANT_STONE_LOW = -3;

        /// <summary>
        /// Which strand owns a cell - 1 for the left chain, 2 for the right one, 0 for neither. Both are
        /// 2-wide rows one level thick, stepping at most one column a level, which is the region's parity
        /// rule and what makes the cross-level contact exist on both parities.
        /// </summary>
        private static int PendantStrand(int cx, int cz, int i)
        {
            if (!RigRow(cz) || i < PENDANT_STRAND_FOOT || i >= PENDANT_STRAND_FOOT + PENDANT_STRAND_X.Length)
                return 0;

            int low = PENDANT_STRAND_X[i - PENDANT_STRAND_FOOT];

            if (RigWithin(cx, low, low + 1)) return 1;

            int mirrored = -low - 2;

            return RigWithin(cx, mirrored, mirrored + 1) ? 2 : 0;
        }

        /// <summary>The jump ring: two solid bars with only the two side columns between them.</summary>
        private static bool PendantRing(int cx, int cz, int i)
        {
            if (!RigRow(cz) || !RigWithin(i, PENDANT_RING_BOTTOM, PENDANT_RING_TOP)) return false;
            if (!RigWithin(cx, PENDANT_RING_LOW, PENDANT_RING_LOW + 3)) return false;

            return i == PENDANT_RING_BOTTOM || i == PENDANT_RING_TOP
                || cx == PENDANT_RING_LOW || cx == PENDANT_RING_LOW + 3;
        }

        private static bool PendantTie(int cx, int cz, int i) =>
            RigRow(cz) && i == PENDANT_TIE_BOTTOM
            && RigWithin(cx, PENDANT_RING_LOW, PENDANT_TIE_HIGH);

        /// <summary>
        /// The nested stone. Each course is one cell narrower than the one above and starts half a course
        /// in every second level, which is what drops it into the pockets rather than onto the shoulders -
        /// see <see cref="Pendant"/> for the four positions the arithmetic produces.
        /// </summary>
        private static bool PendantStone(int cx, int cz, int i)
        {
            int course = PENDANT_STONE_TOP - i;

            if (course < 0 || course >= PENDANT_STONE_COURSES) return false;

            int low = PENDANT_STONE_LOW + course / 2;
            int high = low + PENDANT_STONE_COURSES - course - 1;

            return RigWithin(cx, low, high) && RigWithin(cz, low, high);
        }

        private static BallType PendantColour(int x, int z, int i)
        {
            RigCentre(x, z, PENDANT_GRID, out int cx, out int cz);

            //The shoulder bar is both strand feet, so it wears their inks: the left half finishes strand A's
            //run and the right half strand B's, and every ball of the ring's brown loop stands below it
            int strand = i == PENDANT_RING_TOP
                ? (cx <= PENDANT_RING_LOW + 1 ? 1 : 2)
                : PendantStrand(cx, cz, i);

            if (strand != 0)
            {
                //Runs counted DOWN from the glass, so the top level carries the first ink of both chains
                int run = (PENDANT_DEPTH - 1 - i) / PENDANT_RUN;

                return strand == 1
                    ? Band(run, new[] { BallType.Type7, BallType.Type9 })    //the gold chain: gold, shadowed gold
                    : Band(run, new[] { BallType.Type5, BallType.Type3 });   //the steel chain: cyan, blue
            }

            //The ring and the tie are one ink in two groups four levels apart: the tie is a redundant member
            //and drops nothing, the ring is the jackpot and drops the stone
            if (PendantRing(cx, cz, i) || PendantTie(cx, cz, i)) return BallType.Type10;   //brown

            //The stone, banded course by course. ⚠ ALTERNATING AND NOT ONE WHITE FACET (#360): a stone of six
            //courses in a single ink is a 66-ball group and the biggest shot in the level twice over, once
            //through the ring that hangs it and once through the stone itself. Nested courses touch only
            //their immediate neighbours, so alternating cuts it into six groups of one course each - the
            //widest of them 36 - without a line of it drawn differently. The tip takes the course above it,
            //because a single ball at the point would otherwise be a group of one.
            int facet = PENDANT_STONE_TOP - i;

            return (facet == PENDANT_STONE_COURSES - 1 ? facet - 1 : facet) % 2 == 1
                ? BallType.Type4    //white
                : BallType.Type6;   //magenta
        }

        /// <summary>
        /// A spider's orb web hanging point-down from the glass - a closed rim against the plate, five
        /// spokes winding down to a hub, a spiral tie ring threading all five, and the spider itself hanging
        /// in the middle of it. It is the one radial-planar layout in the campaign: the lattice plays a NET
        /// here rather than a solid, which is geometry every player recognises on sight.
        /// <para>
        /// <b>It stages the longest earned-swing ramp in the pack.</b> One ink per spoke means one landed
        /// ball takes exactly one whole thread, so the spider's support goes 5-4-3-2-1 and its idle sway
        /// grows into a true pendulum on the last one. Nothing is orphaned on the way: a spoke cut anywhere
        /// leaves its lower half slung through the tie ring off the other four, and the rim closes the top
        /// so no shot at the rim can isolate a spoke root.
        /// </para>
        /// <para>
        /// <b>The rim is the anchor and is therefore never one ink.</b> It alternates white and black over
        /// <see cref="WEB_RIM_SECTORS"/> sectors - an EVEN count, so the alternation survives the wrap - and
        /// the spokes recolour the rim cells they land in, which is what puts all five thread colours on the
        /// glass level as well. The tie ring is banded the same way over <see cref="WEB_TIE_SECTORS"/>.
        /// </para>
        /// <para>
        /// <b>The hub is meant to merge and the threads are not.</b> Five tubes of <see cref="WEB_THREAD"/>
        /// converge, and below about rho 2 their discs overlap - that is the spider's thorax and it is
        /// wanted. At the tie ring the spoke centres stand 3.5 cells apart against two tube radii of 2.6,
        /// which is a cell of daylight, and that cell is the whole reason the web reads as five threads
        /// rather than as Rope's shapeless column.
        /// </para>
        /// <para>
        /// Gate watch (#255), in the spec's order. FIRST spoke separateness on the BUILT lattice at levels
        /// 5-8: if two threads fuse mid-height, raise <see cref="WEB_HUB"/> from 0.6 to 1.0 or drop
        /// <see cref="WEB_THREAD"/> to 1.2 - never above the hub, where the merge is the thorax. SECOND the
        /// rim and tie arcs off the MEASURED contact graph rather than off the angle: closed loops touch
        /// themselves and the spokes cut the arcs where they land, so the sector counts are what get
        /// re-solved if a colour reads as one giant group. THIRD the unshot spider's sag on five slender
        /// threads against the death line - tune it up one level (<see cref="WEB_SPIDER_BOTTOM"/> to 2) if
        /// it hangs low. The winding is 0.02 turns a level, safely under the block's 0.05 cap: do not
        /// increase it for looks.
        /// </para>
        /// </summary>
        private static Design Web() => new()
        {
            File = "Web.json",
            Name = "Web",
            Grid = 15,
            Depth = WEB_DEPTH,
            FieldLevels = DUNES_FIELD_LEVELS,
            Scene = SceneKind.Desert,
            Sky = DUNES_SKY,
            Music = MUSIC_COIL,
                Balls = BALLS_COIL,
            Shots = 46,
            CeilingStep = 7,
            Occupied = (r, ang, i, depth) =>
                WebRim(r, i, depth) || WebTie(r, i) || WebSpider(r, i)
                || (i <= WEB_SPOKE_TOP && WebSpoke(r, ang, i) != 0),
            Colour = (r, ang, i, depth) =>
            {
                //The spider first: the blob is ONE black body and the threads converge into it, so the cells
                //inside WEB_SPIDER are its thorax rather than five thread ends meeting in the open
                if (WebSpider(r, i)) return BallType.Type1;   //red - the one warm thing in the level

                int spoke = WebSpoke(r, ang, i);

                //A thread keeps its own ink where it lands on the rim, which is what stands all five spoke
                //colours on the glass level beside the rim's own two
                if (spoke != 0) return WEB_THREADS[spoke - 1];

                return Band(SectorIndex(ang, 0f, WebRim(r, i, depth) ? WEB_RIM_SECTORS : WEB_TIE_SECTORS),
                    new[] { BallType.Type4, BallType.Type8 });   //white, black
            },
        };

        //THE WEB'S OWN FIGURES. The rim reaches 6.1 cells, which is the widest ring a 15-wide field carries
        //with a free column all round: an unshifted level's outermost cell sits at 6.5 and is left out by
        //construction, and a shifted one's at 6.0 is the flank ball LateralMargin wants a neighbour for.
        private const byte WEB_DEPTH = 12;

        //The two hoops, both 1.8 cells thick so each is a solid band whatever the lattice rounds off: the
        //rim against the glass and the spiral tie that threads every spoke half way down.
        private const float WEB_RIM = 5.2f;
        private const float WEB_HOOP_HALF = 0.9f;
        private const int WEB_RIM_LEVELS = 2;
        private const float WEB_TIE = 3.0f;
        private const int WEB_TIE_BOTTOM = 6;
        private const int WEB_TIE_LEVELS = 2;

        //The threads. Five of them, a tube of WEB_THREAD about a centre that walks out from WEB_HUB to
        //WEB_HUB + WEB_SPAN as it climbs - a radial step of 0.66 a level, so consecutive levels of one
        //thread always overlap, which is the only thing making a thread a thread rather than a stack of
        //discs. The winding is the Helix register at a fifth of its rate: visible, and nowhere near the cap.
        private const int WEB_SPOKES = 5;
        private const float WEB_THREAD = 1.3f;
        private const float WEB_HUB = 0.6f;
        private const float WEB_SPAN = 4.6f;
        private const int WEB_SPOKE_BOTTOM = 3;
        private const int WEB_SPOKE_TOP = 10;
        private const float WEB_TURNS_PER_LEVEL = 0.02f;

        //The spider: a small solid body slung under the convergence, and the layout's lowest level is left
        //empty under it on purpose - a weight on five threads needs somewhere to swing.
        private const float WEB_SPIDER = 1.6f;
        private const int WEB_SPIDER_BOTTOM = 1;
        private const int WEB_SPIDER_TOP = 3;

        //How many arcs each hoop is banded in. BOTH are even, so white and black still alternate across the
        //wrap; twelve is about a 3-cell arc at the rim and six about a 3-cell arc at the tie.
        private const int WEB_RIM_SECTORS = 12;
        private const int WEB_TIE_SECTORS = 6;

        /// <summary>
        /// One ink per thread, so a landed ball takes exactly one whole spoke and the spider's suspension
        /// counts down 5-4-3-2-1. None of the five is the rim's white or black, and none is repeated.
        /// </summary>
        private static readonly BallType[] WEB_THREADS =
        {
            BallType.Type3,    //blue
            BallType.Type5,    //cyan
            BallType.Type6,    //magenta
            BallType.Type2,    //green
            BallType.Type10,   //brown
        };

        private static bool WebRim(float r, int i, int depth) =>
            i >= depth - WEB_RIM_LEVELS && MathF.Abs(r - WEB_RIM) <= WEB_HOOP_HALF;

        private static bool WebTie(float r, int i) =>
            RigWithin(i, WEB_TIE_BOTTOM, WEB_TIE_BOTTOM + WEB_TIE_LEVELS - 1)
            && MathF.Abs(r - WEB_TIE) <= WEB_HOOP_HALF;

        private static bool WebSpider(float r, int i) =>
            RigWithin(i, WEB_SPIDER_BOTTOM, WEB_SPIDER_TOP) && r <= WEB_SPIDER;

        /// <summary>
        /// Where a thread's centre stands at a level. Extrapolated deliberately past
        /// <see cref="WEB_SPOKE_TOP"/>: the two rim levels are drawn by the rim, and the thread's centre
        /// carried on up through them is what recolours the rim cells it lands in.
        /// </summary>
        private static float WebSpokeRadius(int i) =>
            WEB_HUB + WEB_SPAN * (i - WEB_SPOKE_BOTTOM) / (float)(WEB_SPOKE_TOP - WEB_SPOKE_BOTTOM);

        private static float WebSpokeAngle(int k, int i) =>
            MathF.Tau * ((float)k / WEB_SPOKES + WEB_TURNS_PER_LEVEL * (WEB_SPOKE_TOP - i));

        /// <summary>
        /// Which thread owns a cell - 1..<see cref="WEB_SPOKES"/>, or 0 for none. Measured per level with
        /// <see cref="LateralDistanceSquared"/> rather than against a polyline in three dimensions: the
        /// curve passes through every level it is drawn on, so the lateral distance IS the distance there,
        /// and the radial step is small enough that consecutive discs overlap by most of their width.
        /// <para>
        /// <b>The NEAREST thread owns a shared cell</b>, where <see cref="RopeStrand"/> lets the lower index
        /// win. Down at the hub all five tubes overlap, and first-match ownership would hand one ink the
        /// whole convergence - and with it every other thread's lower end, which is exactly the group the
        /// drop test would then read as a quarter of the level. Nearest splits the merge five ways, so no
        /// ink carries more than its own thread and its own share of the fringe round the thorax.
        /// </para>
        /// </summary>
        private static int WebSpoke(float r, float ang, int i)
        {
            if (i < WEB_SPOKE_BOTTOM) return 0;

            float rho = WebSpokeRadius(i);
            float best = WEB_THREAD * WEB_THREAD;
            int nearest = 0;

            for (int k = 0; k < WEB_SPOKES; k++)
            {
                float squared = LateralDistanceSquared(r, ang, rho, WebSpokeAngle(k, i));

                //Not-further rather than nearer, so the cells exactly on the tube's own edge are kept; a tie
                //between two threads goes to the higher index, which is arbitrary but fixed
                if (squared > best) continue;

                best = squared;
                nearest = k + 1;
            }

            return nearest;
        }

        /// <summary>
        /// A tower crane with a load on the hook - the region's first level that is a MACHINE rather than a
        /// shape, and the first anywhere whose statics the player dismantles in an order they choose, with a
        /// different collapse each way. Cut the yellow stay and the jib tip drops a hand-span and springs
        /// on the mast joint, the rope and load whipping under it while the counterweight see-saws; cut the
        /// orange mast instead and the whole crane pendulums from the stay.
        /// <para>
        /// <b>⚠ #360 rebuilt its PROPORTIONS after a playtest read it as "I don't understand this one at
        /// all".</b> Every member a tower crane has was already here and none of them read, because on a
        /// grid of 13 the arm and the tower were the same size and the stay climbed as steeply as the mast
        /// stood: the eye got an arch with lumps on it. The grid is 15 now, the jib twelve columns against a
        /// six-course mast, and the stay falls a column a level instead of one every two - see the figures
        /// below, each of which carries what it cost. The statics did not move: two independent ceiling
        /// anchors, a counterweight on the short arm, a load on the long one.
        /// </para>
        /// <para>
        /// <b>Two independent glass anchors is the second load path, executed structurally.</b> The mast
        /// stands a third of the way along the arm and the stepped stay comes down on its tip, so any single
        /// member - or any single colour - leaves a working triangle. The stay steps one column inward per
        /// level and each row shares a column with the one below, which is the region's parity rule and what
        /// makes its cross-level contacts exist on both parities. Both spread into a four-column plate on the
        /// top course, with one free column between them: sixteen anchor cells rather than eight, which is
        /// what the longer arm cost and what the sag probe made them pay.
        /// </para>
        /// <para>
        /// <b>The mast is orange only ABOVE the jib.</b> Painting its two jib courses orange as well would put
        /// the jib's own root into the mast's group, and cutting orange would then sever the jib and orphan
        /// the counter-jib arm with its counterweight - which is the opposite of what the level is for. Left
        /// as it is, the mast's ink drops nothing at all and hands the player the biggest swing in the
        /// level. A jib segment is the shot that drops the arm: a readable payoff of about a third of the
        /// cluster, priced by the drop test.
        /// </para>
        /// <para>
        /// <b>Nothing here is a single-colour member except the two anchors.</b> The jib alternates silver
        /// and blue in 2-cell segments so a jib shot strands nothing (root and tip are both anchored); the
        /// counterweight is banded in 2-level brown and white courses (the Harp graft), so shooting a band
        /// unbalances the see-saw the OTHER way and springs the tip up - a third staged swing at no
        /// structural cost; and the hook rope is two diagonal inks, cyan holding the empty rope after red
        /// has taken the load it is deliberately fused with. Brown sits against the jib's silver rather than
        /// white against it, which is the one confusable pair this palette could have made.
        /// </para>
        /// <para>
        /// Gate watch (#255), in the spec's order. FIRST every level-to-level contact of the stepped stay,
        /// with the tool and not by eye; if a step loses contact, hold its x-pair a third level rather than
        /// widening the row to three. SECOND unshot equilibrium: the 12-long jib torques the 2x2 mast and
        /// the stay has to be measurably load-bearing, with the tip above the death line - the constants to
        /// tune are the counterweight's depth (<see cref="CRANE_COUNTER_Z_HIGH"/>, spent down to 0 by #360)
        /// and the height the hook hangs at, and never the jib length. It reads 2 of 5 on the sag probe
        /// where the short-armed build read 1, both of them under the threshold that reports and inside the
        /// probe's own run-to-run spread. THIRD the two joints the drawing depends on: the counterweight
        /// hanging under the counter-jib, and the stay's foot meeting the jib at its outer end.
        /// </para>
        /// </summary>
        private static Design Crane() => new()
        {
            File = "Crane.json",
            Name = "Crane",
            Grid = CRANE_GRID,
            Depth = CRANE_DEPTH,
            //Seventeen and not eighteen: the layout is thirteen deep and Emit refuses an odd offset, so the
            //field gives up one level to keep the parity. It is still over sixteen, so the cluster hangs
            //raised off the death line, and still under FRAMED_LEVELS, so the machine is framed whole
            FieldLevels = 17,
            Scene = SceneKind.Desert,
            Sky = DUNES_SKY,
            Music = MUSIC_COIL,
                Balls = BALLS_COIL,
            Shots = 46,
            CeilingStep = 7,
            OccupiedBlock = (x, z, i, depth) =>
            {
                RigCentre(x, z, CRANE_GRID, out int cx, out int cz);

                return CraneSteel(cx, cz, i);
            },
            BlockColour = (x, z, i) =>
            {
                RigCentre(x, z, CRANE_GRID, out int cx, out int cz);

                return CraneColour(cx, cz, i);
            },
        };

        //THE CRANE'S OWN FIGURES. The machine lives between centred x -5 and 6 in a grid of 15 and between
        //z -2 and 1, so one free column stands all round.
        //
        //⚠ THE PROPORTIONS ARE THE LEVEL'S SUBJECT AND NOT ITS DRESSING (#360). The first build drew every
        //member of a real tower crane and a playtest still read it as "I don't understand this one at all":
        //on a grid of 13 the jib spanned ten columns while the mast stood eight courses tall, so the arm and
        //the tower carried the SAME weight in the silhouette and the pair read as an arch with lumps on it.
        //A crane is recognised by one thing before any other - a horizontal far longer than the vertical it
        //crosses - so the grid went to 15, the jib to twelve columns and the mast down to six courses, which
        //is a arm-to-tower ratio of 2:1 where it was 1.2:1. Nothing about the statics moved: two independent
        //ceiling anchors, a counterweight on the short arm and a load on the long one.
        private const byte CRANE_GRID = 15;
        private const byte CRANE_DEPTH = 13;

        //The mast: a 2x2 column from the jib's own courses up to the glass, standing a THIRD of the way
        //along the arm rather than in its middle - which is what makes the short end read as a counter-jib
        //and the long one as the jib. CRANE_MAST_ORANGE is where its INK starts, one level above the jib:
        //see Crane() for what painting the jib's root in the mast's colour would do.
        private const int CRANE_MAST_LOW = -3;
        private const int CRANE_MAST_FOOT = 5;
        private const int CRANE_MAST_ORANGE = 7;

        //Where the mast spreads into its head — two courses, four columns, widening away from the stay so
        //the two anchors never touch and stay two independent load paths.
        private const int CRANE_MAST_HEAD = 11;

        //The jib: two courses across the whole span, the mast's own cells included - a short counter-jib to
        //-5 and a long jib out to 6.
        private const int CRANE_JIB_LOW = -5;
        private const int CRANE_JIB_HIGH = 6;
        private const int CRANE_JIB_BOTTOM = 5;

        //The counterweight under the counter-jib, and the hook rope under the jib tip: both hang from the
        //jib's lower course across levels 4 and 5, and both are three courses tall.
        //
        //⚠ THE COUNTERWEIGHT'S DEPTH IS THE EQUILIBRIUM DIAL AND THE JIB'S LENGTH IS NOT, which this design's
        //own gate watch said before #360 lengthened the jib and had to use it: three cells deep rather than
        //four takes six balls off the far end of the short arm and the probe reads it. The hook hangs one
        //level higher for the same reason - the load is the lowest thing in the level and the line is what
        //it was measured against.
        private const int CRANE_HUNG_BOTTOM = 2;
        private const int CRANE_HUNG_TOP = 4;
        private const int CRANE_COUNTER_LOW = -5;
        private const int CRANE_COUNTER_Z_LOW = -2;
        private const int CRANE_COUNTER_Z_HIGH = 0;
        private const int CRANE_ROPE_LOW = 5;

        //The load on the hook: two courses at the bottom of the layout, wider than the rope in both axes so
        //it reads as a slung block rather than as more rope.
        private const int CRANE_LOAD_TOP = 2;
        private const int CRANE_LOAD_X_LOW = 4;
        private const int CRANE_LOAD_X_HIGH = 6;
        private const int CRANE_LOAD_Z_LOW = -2;
        private const int CRANE_LOAD_Z_HIGH = 0;

        //The stay, the crane's SECOND glass anchor: 2-wide rows from the jib TIP up to the plate, stepping
        //one column inward per level, foot first. Consecutive rows always share a column, which the region's
        //parity rule makes a contact on both parities.
        //
        //⚠ ONE COLUMN A LEVEL AND NOT ONE EVERY TWO (#360). Stepped at half that rate it climbed as steeply
        //as the mast stood, and two near-vertical members of the same width read as the two legs of an arch
        //instead of as a tower and the cable that holds its arm out. At 45 degrees it is unmistakably a
        //diagonal, and it lands where a stay belongs - on the jib's outer end, directly over the hook.
        private const int CRANE_STAY_FOOT = 7;
        //⚠ The last entry repeats rather than stepping on: one more column inward would put the stay's anchor
        //plate against the mast's head at the plate, and the free column between them is what draws the two
        //as two members. Holding an x-pair for an extra level is this region's own stated remedy.
        private static readonly int[] CRANE_STAY_X = { 5, 4, 3, 2, 1, 0 };

        /// <summary>Whether a cell is part of the machine. Every member is stated in the centred frame.</summary>
        private static bool CraneSteel(int cx, int cz, int i)
        {
            if (RigRow(cz))
            {
                //The mast, from the jib's own courses up to the glass, spreading into a HEAD on its top two
                //courses. ⚠ The head is structural before it is a drawing (#360): the longer jib doubled the
                //torque on this level's two anchors and the sag probe answered at once - 4 of 5, and one of
                //them under the line with the glass still at rest, which is the reading that means the
                //layout and not the play. Sixteen anchor cells where there were eight halve the load, and a
                //cat head is what a tower crane has up there anyway.
                if (i >= CRANE_MAST_HEAD && RigWithin(cx, CRANE_MAST_LOW - 2, CRANE_MAST_LOW)) return true;

                if (i >= CRANE_MAST_FOOT && RigWithin(cx, CRANE_MAST_LOW, CRANE_MAST_LOW + 1)) return true;

                //The jib: two courses across the whole span.
                //
                //⚠ ONE COURSE WAS TRIED AND THE PHOTOGRAPH REFUSED IT (#360). Halving the arm's weight cost
                //the sag probe nothing - 2 of 5 either way, the anchors having already been doubled - but a
                //single course of twelve balls hanging off a mast at one third of its length DROOPS, and a
                //jib that sags in the middle is the one thing that stops a crane reading as a crane. The
                //second course is what makes the arm stiff enough to stay a horizontal line in play; the
                //elevation this level is drawn in cannot show that and the running game can.
                if (RigWithin(i, CRANE_JIB_BOTTOM, CRANE_JIB_BOTTOM + 1)
                    && RigWithin(cx, CRANE_JIB_LOW, CRANE_JIB_HIGH)) return true;

                //The stay, one row per level from its own table, spreading outward into its own anchor plate
                //on the top course for the mast head's reason
                if (i >= CRANE_STAY_FOOT)
                {
                    int low = CRANE_STAY_X[i - CRANE_STAY_FOOT];
                    int high = low + (i == CRANE_DEPTH - 1 ? 3 : 1);

                    if (RigWithin(cx, low, high)) return true;
                }

                //The hook rope
                if (RigWithin(i, CRANE_HUNG_BOTTOM, CRANE_HUNG_TOP)
                    && RigWithin(cx, CRANE_ROPE_LOW, CRANE_ROPE_LOW + 1)) return true;
            }

            //The counterweight, four cells deep in z rather than two: it is a mass and not a member
            if (RigWithin(i, CRANE_HUNG_BOTTOM, CRANE_HUNG_TOP)
                && RigWithin(cx, CRANE_COUNTER_LOW, CRANE_COUNTER_LOW + 1)
                && RigWithin(cz, CRANE_COUNTER_Z_LOW, CRANE_COUNTER_Z_HIGH)) return true;

            //The load on the end of the rope: two courses, and stated as a RANGE rather than as everything
            //below its top, so raising the hook leaves the layout's bottom course empty instead of stretching
            //the block down into it (#360)
            return RigWithin(i, CRANE_LOAD_TOP - 1, CRANE_LOAD_TOP)
                && RigWithin(cx, CRANE_LOAD_X_LOW, CRANE_LOAD_X_HIGH)
                && RigWithin(cz, CRANE_LOAD_Z_LOW, CRANE_LOAD_Z_HIGH);
        }

        private static BallType CraneColour(int cx, int cz, int i)
        {
            //The mast above the jib, and the stay: the level's two glass anchors, one ink each, and the only
            //two colours standing on the plate
            //The head's two extra columns are the mast's and have to be asked for here as well, or they fall
            //through to the stay's rule below and the tower comes out painted half in the cable's ink
            if (i >= CRANE_MAST_ORANGE && RigWithin(cx, CRANE_MAST_LOW - 2, CRANE_MAST_LOW + 1))
                return BallType.Type9;    //orange - the tower, in crane paint

            if (i >= CRANE_STAY_FOOT) return BallType.Type7;   //yellow - the stay, the same paint a shade off

            //The jib in 2-cell segments, the mast's own two columns included: the segments alternate so a
            //jib shot strands nothing, and the middle one is the jib ROOT the counter-jib arm hangs on
            if (RigWithin(i, CRANE_JIB_BOTTOM, CRANE_JIB_BOTTOM + 1))
                //⚠ Silver and BLUE, where #285 left silver and black: a black segment reads as a hole in the
                //arm at playing distance, and half the jib in holes is half a jib (#360). Blue is the mid
                //value this palette has spare, and it keeps the two groups the alternation is for.
                return Band((cx - CRANE_JIB_LOW) / 2, new[] { BallType.Type11, BallType.Type3 });   //the steel lattice: silver, blue

            //The counterweight in 2-level courses (the Harp graft), brown against the jib's silver rather
            //than white against it - the one confusable pair this palette could have made
            if (RigWithin(cx, CRANE_COUNTER_LOW, CRANE_COUNTER_LOW + 1))
                return Band((CRANE_HUNG_TOP - i) / 2, new[] { BallType.Type10, BallType.Type4 });   //brown, white

            //The rope's two diagonals: cyan keeps the empty rope hanging after red has gone
            if (i > CRANE_LOAD_TOP)
                return RigDiagonal(cx, cz) == 0 ? BallType.Type5 : BallType.Type1;   //cyan, red

            //The load, fused with the rope's red diagonal on purpose: shoot red, drop the load
            return BallType.Type1;   //red
        }

        /// <summary>
        /// A Calder mobile - beams on ropes on beams - and the first level in the game that visibly rocks,
        /// dips and REBALANCES itself after every payoff shot. It is the owner's earned-swing ask made
        /// recursive: three staged swings at three depths, each one caused and none of them scripted.
        /// <para>
        /// <b>Shot order matters here more than anywhere below it.</b> Take the red leg and the entire
        /// sculpture pendulums from the cyan one; take the blue big bob and the unbalanced long arm dips and
        /// bounces the whole secondary assembly under it; take magenta and bob B drops while the small beam
        /// see-saws on its rope. Doing them in a different order gives a different sculpture to aim at each
        /// time, which is what a two-tier dependency buys.
        /// </para>
        /// <para>
        /// <b>Both beams are split LENGTHWISE and not across.</b> One row white, one row silver: releasing
        /// either ink thins a beam to a single full-length row that visibly sags and springs, and severs
        /// nothing, because every leg, rope and hanger meets the beam through BOTH rows. Split across
        /// instead, one shot would cut an arm off and orphan everything hanging under it.
        /// </para>
        /// <para>
        /// <b>⚠ A BEAM IS ONE COURSE AND THE WIRES ARE COOL SINCE #360</b>, both of them for the same
        /// complaint: a playtest read this level as primitive, and what it was looking at was two 2-course
        /// white slabs with speckled posts hanging off them. A beam drawn one course deep is a ROD, which is
        /// what a Calder mobile is made of, and it halves the weight the two anchors carry; the wires kept
        /// their diagonal safety - no ink severs a link - but navy against cyan reads as one wire where navy
        /// against yellow read as a chequer. The tiers were also pulled apart vertically, so there is sky
        /// between the primary beam, the secondary one and the bobs instead of one crowded block.
        /// </para>
        /// <para>
        /// <b>The glass hang is a closed triangle</b> of two sloped legs, one red and one black, so no single
        /// colour drops the sculpture; every rope link is a 2x2 in <see cref="RigDiagonal"/>'s two inks, so
        /// no colour severs a link either. Bob B is the one deliberate exception - it is cyan and fused
        /// with its own rope's cyan diagonal, so one shot drops the bob while every rope's navy diagonal
        /// goes on holding. Bob A shares red with the leg it stands nowhere near, which is the same ink in
        /// two groups and the reason the drop test is run per group and not per colour.
        /// </para>
        /// <para>
        /// <b>The big bob has its four vertical corner columns taken out</b>, which is 36 balls instead of 48
        /// and a mass that reads as a Calder plate rather than as a brick - and it is also the tuning
        /// constant if the sculpture leans.
        /// </para>
        /// <para>
        /// Gate watch (#255), in the spec's order. FIRST unshot equilibrium: asymmetric torque is the point
        /// AND the danger, so verify every bob stays above the death line at rest and trim the big bob to
        /// two courses (<see cref="MOBILE_BOB_TOP"/> down to 4) if it sags. SECOND both sloped legs'
        /// cross-level contacts, and that the leg feet really land in the primary beam's top row - they are
        /// drawn as beam cells here, which is what makes that union structural rather than hopeful. THIRD
        /// the drop test on red, whose two groups are the leg and bob A: confirm the larger one orphans
        /// nothing and that bob A's own release path is its own rope. If the delayed whip down through the
        /// secondary beam is dead in play (the Chain graft), lengthen the secondary rope by one level
        /// (<see cref="MOBILE_ARM_ROPE_BOTTOM"/> to 6) to give it phase room.
        /// </para>
        /// </summary>
        private static Design Mobile() => new()
        {
            File = "Mobile.json",
            Name = "Mobile",
            Grid = MOBILE_GRID,
            Depth = MOBILE_DEPTH,
            //Seventeen for Crane's reason: a thirteen-deep layout, an even offset, raised off the death line
            //and still framed whole
            FieldLevels = 17,
            Scene = SceneKind.Desert,
            Sky = DUNES_SKY,
            Music = MUSIC_COIL,
                Balls = BALLS_COIL,
            Shots = 46,
            CeilingStep = 7,
            OccupiedBlock = (x, z, i, depth) =>
            {
                RigCentre(x, z, MOBILE_GRID, out int cx, out int cz);

                return MobileSculpture(cx, cz, i);
            },
            BlockColour = (x, z, i) =>
            {
                RigCentre(x, z, MOBILE_GRID, out int cx, out int cz);

                return MobileColour(cx, cz, i);
            },
        };

        //THE MOBILE'S OWN FIGURES. The sculpture reaches centred x -6 and 6 in a grid of 15, so one free
        //column stands either side, and z -2 to 1 at its deepest.
        private const byte MOBILE_GRID = 15;
        private const byte MOBILE_DEPTH = 13;

        //The hanger triangle: the LEFT leg's low column at each level above the beam, foot first. The right
        //leg is this table mirrored about x = 0 (low column -low - 1), which lands both feet on the primary
        //beam's top row - they are drawn as beam cells, so the union is structural.
        private const int MOBILE_LEG_FOOT = 11;
        private static readonly int[] MOBILE_LEG_X = { -2, -3 };

        //The primary beam and the secondary one under the long arm. The hang point is x = -0.5, so the beam
        //carries a short arm to -5 and a long one to 4 with the big bob's rope out on the short side.
        private const int MOBILE_BEAM_LOW = -5;
        private const int MOBILE_BEAM_HIGH = 4;
        private const int MOBILE_BEAM_BOTTOM = 10;
        private const int MOBILE_ARM_LOW = 1;
        private const int MOBILE_ARM_HIGH = 6;
        private const int MOBILE_ARM_BOTTOM = 5;

        //The three suspensions. The big bob's rope is three levels, the secondary beam's two, and the two
        //small bobs' two - MOBILE_ARM_ROPE_BOTTOM is the constant the Chain graft names if the delayed whip
        //through the secondary beam reads dead in play.
        private const int MOBILE_BIG_ROPE_LOW = -5;
        private const int MOBILE_BIG_ROPE_BOTTOM = 6;
        private const int MOBILE_BIG_ROPE_TOP = 9;
        private const int MOBILE_ARM_ROPE_LOW = 3;
        private const int MOBILE_ARM_ROPE_BOTTOM = 6;

        //The big bob: a 4x4 plan three courses tall with its four vertical CORNER columns taken out, which
        //is 36 balls rather than 48. Trim MOBILE_BOB_TOP to 4 if the long arm leans past the death line.
        private const int MOBILE_BOB_X_LOW = -6;
        private const int MOBILE_BOB_Z_LOW = -2;
        private const int MOBILE_BOB_BOTTOM = 3;
        private const int MOBILE_BOB_TOP = 5;

        //The two small bobs and the ropes over them share one footprint each, so a bob and its rope read as
        //one slender column that changes colour where the weight begins.
        private const int MOBILE_BOB_A_LOW = 1;
        private const int MOBILE_BOB_B_LOW = 5;
        private const int MOBILE_SMALL_BOTTOM = 1;
        private const int MOBILE_SMALL_TOP = 4;

        /// <summary>Whether a cell is part of the sculpture, in the centred frame every table uses.</summary>
        private static bool MobileSculpture(int cx, int cz, int i)
        {
            if (RigRow(cz))
            {
                //The two sloped legs, above the beam their feet are drawn into
                if (i >= MOBILE_LEG_FOOT)
                {
                    int low = MOBILE_LEG_X[i - MOBILE_LEG_FOOT];

                    if (RigWithin(cx, low, low + 1) || RigWithin(cx, -low - 1, -low)) return true;
                }

                //The two beams
                if (i == MOBILE_BEAM_BOTTOM && RigWithin(cx, MOBILE_BEAM_LOW, MOBILE_BEAM_HIGH)) return true;

                if (i == MOBILE_ARM_BOTTOM && RigWithin(cx, MOBILE_ARM_LOW, MOBILE_ARM_HIGH)) return true;

                //The big bob's rope, and the secondary beam's
                if (RigWithin(i, MOBILE_BIG_ROPE_BOTTOM, MOBILE_BIG_ROPE_TOP)
                    && RigWithin(cx, MOBILE_BIG_ROPE_LOW, MOBILE_BIG_ROPE_LOW + 1)) return true;

                if (RigWithin(i, MOBILE_ARM_ROPE_BOTTOM, MOBILE_BIG_ROPE_TOP)
                    && RigWithin(cx, MOBILE_ARM_ROPE_LOW, MOBILE_ARM_ROPE_LOW + 1)) return true;

                //The two small bobs and the ropes over them, one footprint each
                if (RigWithin(i, MOBILE_SMALL_BOTTOM, MOBILE_SMALL_TOP)
                    && (RigWithin(cx, MOBILE_BOB_A_LOW, MOBILE_BOB_A_LOW + 1)
                        || RigWithin(cx, MOBILE_BOB_B_LOW, MOBILE_BOB_B_LOW + 1))) return true;
            }

            //The big bob: the 4x4 plan minus its four vertical corner columns
            return RigWithin(i, MOBILE_BOB_BOTTOM, MOBILE_BOB_TOP)
                && RigWithin(cx, MOBILE_BOB_X_LOW, MOBILE_BOB_X_LOW + 3)
                && RigWithin(cz, MOBILE_BOB_Z_LOW, MOBILE_BOB_Z_LOW + 3)
                && !((cx == MOBILE_BOB_X_LOW || cx == MOBILE_BOB_X_LOW + 3)
                     && (cz == MOBILE_BOB_Z_LOW || cz == MOBILE_BOB_Z_LOW + 3));
        }

        private static BallType MobileColour(int cx, int cz, int i)
        {
            //The two legs, above the beam they stand on: one ink each, and the only two on the glass level
            if (i >= MOBILE_LEG_FOOT) return cx < 0 ? BallType.Type1 : BallType.Type8;   //Calder red, black wire

            //The big bob, the one mass in the sculpture and the only thing this far out on the short arm
            if (RigWithin(i, MOBILE_BOB_BOTTOM, MOBILE_BOB_TOP) && cx <= MOBILE_BOB_X_LOW + 3)
                return BallType.Type3;   //blue

            //Both beams split LENGTHWISE, so a released ink thins a beam and never severs an arm. The x
            //bound is what keeps the big bob's rope, which shares the secondary beam's levels, out of it
            if (i == MOBILE_BEAM_BOTTOM
                || (i == MOBILE_ARM_BOTTOM && RigWithin(cx, MOBILE_ARM_LOW, MOBILE_ARM_HIGH)))
                return cz == RIG_ROW_LOW ? BallType.Type4 : BallType.Type11;   //the wire beams: white, silver

            //The two small bobs: bob A is red in its own group, half a sculpture away from the leg that
            //shares the ink; bob B is cyan and fused with its own rope's cyan diagonal on purpose.
            //⚠ It followed the wires when #360 cooled them from navy-and-yellow to navy-and-cyan: the fusion
            //is the mechanic, so the bob's ink is whichever ink its own rope carries, not a colour of its own.
            if (i <= MOBILE_SMALL_BOTTOM + 1)
                return cx <= MOBILE_BOB_A_LOW + 1 ? BallType.Type1 : BallType.Type5;   //Calder red, Calder cyan

            //Every rope link in two diagonal inks, so no colour severs any single one
            return RigDiagonal(cx, cz) == 0 ? BallType.Type12 : BallType.Type5;   //the hanging wires: navy, cyan
        }

        /// <summary>
        /// A suspension bridge hanging under the glass - and the level that breaks the block's deepest
        /// assumption, because every other slender layout in the game hangs DOWN a chain of members and this
        /// one SPANS SIDEWAYS. The player chooses whether it dies as a bridge or as a swing: take a cable
        /// first and the span lists to one side and shivers on the survivor, take the bearings first and the
        /// whole roadway pendulums on four slender hangers.
        /// <para>
        /// <b>Two fully independent structural systems</b> are the block's second-load-path rule built as
        /// the level's entire dramaturgy: cable-and-hanger above, beam-on-bearings below. Each pylon is a
        /// closed portal (two legs under one cap), each cable is one of a pair in its own z plane, and the
        /// deck is a beam seated on both pylons - so no single release anywhere takes a structural system
        /// with it, and the drop test reads the whole level in single figures.
        /// </para>
        /// <para>
        /// <b>The catenary is drawn as 2-level columns that overlap by one level at every step</b>, which is
        /// the Trellis-class trap answered by construction rather than by luck: consecutive columns share a
        /// level, so their contact is a plain same-level neighbour and no parity diagonal has to be right
        /// for the cable to hold. The outermost column stands beside the leg at the same two levels, which
        /// is how each cable reaches its anchorage.
        /// </para>
        /// <para>
        /// <b>The bearings are DECK and not leg.</b> The deck's end cells sit inside the legs' own
        /// footprint, and colouring them with the deck is what makes "unseat the span" a shot rather than a
        /// demolition: the deck alternates white and blue in 2-cell segments, so taking a bearing segment
        /// frees one end and strands no island. The hangers are magenta and olive in
        /// <see cref="RigDiagonal"/>'s pairs (the Harp graft) so no ink ever drops all four posts and the
        /// unseated deck's swing deepens post by post, 4-3-2-1, before the last one lets go - olive is in
        /// nothing else in the level.
        /// </para>
        /// <para>
        /// Gate watch (#255), in the spec's order. FIRST the cable connectivity gate, before any colouring:
        /// every column must reach the next one and both ends must reach their legs; if a step ever loses
        /// contact, flatten the catenary by repeating a level pair across two columns rather than thickening
        /// the cable. SECOND the full per-colour drop test - only the green caps stand at the glass, so
        /// every other colour's survival is argued through the legs and the deck; shorten the deck's
        /// segments to an irregular run if a release strands an island. THIRD the hangers' contacts both
        /// ways, to the cable above through the shared column and to the deck below through the shared row.
        /// The margin is two columns at grid 15 and the pylons must not be widened to spend it.
        /// </para>
        /// </summary>
        private static Design Bridge() => new()
        {
            File = "Bridge.json",
            Name = "Bridge",
            Grid = BRIDGE_GRID,
            Depth = BRIDGE_DEPTH,
            FieldLevels = DUNES_FIELD_LEVELS,
            Scene = SceneKind.Desert,
            Sky = DUNES_SKY,
            Music = MUSIC_COIL,
                Balls = BALLS_COIL,
            //The block's ceiling: the biggest grid, the most members and seven colours, so the budget is the
            //loosest here and still the one that has to be spent in the right order
            Shots = 48,
            CeilingStep = 7,
            OccupiedBlock = (x, z, i, depth) =>
            {
                RigCentre(x, z, BRIDGE_GRID, out int cx, out int cz);

                return BridgeSteel(cx, cz, i);
            },
            BlockColour = (x, z, i) =>
            {
                RigCentre(x, z, BRIDGE_GRID, out int cx, out int cz);

                return BridgeColour(cx, cz, i);
            },
        };

        //THE BRIDGE'S OWN FIGURES. The span reaches centred x -5 to 5 and z -3 to 2 in a grid of 15, so two
        //free columns stand all round - the widest margin in the region, and it is not to be spent.
        private const byte BRIDGE_GRID = 15;
        private const byte BRIDGE_DEPTH = 12;

        //The four legs: two lines in x, folded together by Math.Abs, and two planes in z, folded together by
        //BridgePlane. A leg is 2x2 in plan and runs from the deck's own courses to the glass.
        private const int BRIDGE_LEG_X = 4;
        private const int BRIDGE_LEG_BOTTOM = 3;

        //Which fold index the cable planes are: 1 and 2 are the two rows either side of the deck's middle,
        //and everything structural except the deck and the caps stands in them.
        private const int BRIDGE_PLANE_LOW = 1;

        //The portal caps: two courses joining each pylon's legs across the middle rows, and the only thing
        //in the level that stands ON the glass.
        private const int BRIDGE_CAP_BOTTOM = 10;

        //The deck: two courses seated on the pylons, four rows deep, its end cells shared with the legs.
        //
        //⚠ #360 WIDENED IT BY A COLUMN EACH SIDE AND THE PROBE TOOK IT BACK. The intent was a roadway that
        //reads as a span rather than as a panel between two towers - but the two free columns all round are
        //this region's widest margin and are not to be spent, so the extra column landed INSIDE the legs'
        //own footprint instead of past it: sixteen more balls at the ends of the span, no overhang to show
        //for them, and the sag probe went from 2 of 5 to 3 of 5 and stayed there over three runs. A change
        //that costs a level a step of sag and gives the eye nothing is not a change.
        private const int BRIDGE_DECK_BOTTOM = 3;
        private const int BRIDGE_DECK_X = 4;

        //The four hangers, two columns out from the middle on either side.
        private const int BRIDGE_HANGER_BOTTOM = 5;
        private const int BRIDGE_HANGER_X = 1;

        /// <summary>
        /// The catenary's lower level at each column out from the middle - the cable is a 2-level column at
        /// every step, so consecutive steps OVERLAP by one level and their contact is a same-level
        /// neighbour rather than a parity diagonal. See <see cref="Bridge"/> for why that matters.
        /// </summary>
        //
        //⚠ A DEEPER SAG WAS TRIED AND THE PHOTOGRAPH REFUSED IT (#360). Lifting the ends a level each -
        //{ 6, 7, 8, 9 } - does open daylight under mid-span in elevation, and in the running game it packs
        //the cable's ends against the pylons instead, so the top of the level reads as one orange band from
        //edge to edge where it used to read as two towers with sky between them. The daylight that mattered
        //was bought by the deck's overhang above, which costs the silhouette nothing.
        private static readonly int[] BRIDGE_CABLE_LEVEL = { 6, 6, 7, 8 };

        /// <summary>
        /// Which plane out from the deck's centre a cell is in: 0 is the deck's own middle rows, 1 and 2 are
        /// the rows the legs, cables and hangers stand in. The fold is about z = -0.5 and not about 0
        /// because every member in the region is two cells deep and seated on <see cref="RIG_ROW_LOW"/>, so
        /// the axis falls on the seam between the two middle rows.
        /// </summary>
        private static int BridgePlane(int cz) => cz < RIG_ROW_LOW + 1 ? -cz - 1 : cz;

        /// <summary>Whether a cell is part of the span, in the centred frame every table uses.</summary>
        private static bool BridgeSteel(int cx, int cz, int i)
        {
            int across = Math.Abs(cx);
            int plane = BridgePlane(cz);

            //The four legs, from the deck's courses up to the glass
            if (i >= BRIDGE_LEG_BOTTOM && RigWithin(across, BRIDGE_LEG_X, BRIDGE_LEG_X + 1)
                && RigWithin(plane, BRIDGE_PLANE_LOW, BRIDGE_PLANE_LOW + 1)) return true;

            //The portal caps, closing each pylon across its own middle rows
            if (i >= BRIDGE_CAP_BOTTOM && RigWithin(across, BRIDGE_LEG_X, BRIDGE_LEG_X + 1)
                && plane <= BRIDGE_PLANE_LOW + 1) return true;

            //The four hangers. They win where a post and a cable column want the same cell, so a post stays
            //a whole 2x2x2 and its two diagonal inks are groups of four rather than balls standing alone
            if (RigWithin(i, BRIDGE_HANGER_BOTTOM, BRIDGE_HANGER_BOTTOM + 1)
                && RigWithin(across, BRIDGE_HANGER_X, BRIDGE_HANGER_X + 1)
                && RigWithin(plane, BRIDGE_PLANE_LOW, BRIDGE_PLANE_LOW + 1)) return true;

            //The two cables, one per z plane
            if (across < BRIDGE_CABLE_LEVEL.Length
                && RigWithin(plane, BRIDGE_PLANE_LOW, BRIDGE_PLANE_LOW + 1)
                && RigWithin(i, BRIDGE_CABLE_LEVEL[across], BRIDGE_CABLE_LEVEL[across] + 1)) return true;

            //The deck, its ends inside the legs' own footprint
            return RigWithin(i, BRIDGE_DECK_BOTTOM, BRIDGE_DECK_BOTTOM + 1)
                && across <= BRIDGE_DECK_X && plane <= BRIDGE_PLANE_LOW;
        }

        private static BallType BridgeColour(int cx, int cz, int i)
        {
            int across = Math.Abs(cx);
            int plane = BridgePlane(cz);

            //The caps take the glass and the course under it, so a leg's whole route up is an ink it does
            //not share: release a leg and its cap stands on the twin leg
            if (i >= BRIDGE_CAP_BOTTOM) return BallType.Type10;   //brown - the tower tops, dark over the orange

            //The deck in 2-cell segments, the bearings included - taking a bearing segment unseats one end
            if (RigWithin(i, BRIDGE_DECK_BOTTOM, BRIDGE_DECK_BOTTOM + 1)
                && across <= BRIDGE_DECK_X && plane <= BRIDGE_PLANE_LOW)
                return Band((cx + BRIDGE_DECK_X) / 2, new[] { BallType.Type4, BallType.Type11 });   //the roadway: white, silver

            //The hangers in diagonal pairs (the Harp graft), olive standing nowhere else in the level
            if (RigWithin(i, BRIDGE_HANGER_BOTTOM, BRIDGE_HANGER_BOTTOM + 1)
                && RigWithin(across, BRIDGE_HANGER_X, BRIDGE_HANGER_X + 1))
                return RigDiagonal(cx, cz) == 0 ? BallType.Type12 : BallType.Type5;   //the cold hanger wire: navy, cyan

            //One ink per cable, so no single colour lists the span both ways
            if (across < BRIDGE_CABLE_LEVEL.Length && plane >= BRIDGE_PLANE_LOW
                && RigWithin(i, BRIDGE_CABLE_LEVEL[across], BRIDGE_CABLE_LEVEL[across] + 1))
                return cz < RIG_ROW_LOW + 1 ? BallType.Type1 : BallType.Type7;   //the main cables, lit: red, gold

            //The legs, four of them and four separate groups: the deck's own cells stand between the two
            //legs of a pylon, so no pair of them is ever one brown group
            return BallType.Type9;   //orange - the towers, International Orange
        }

        #endregion

        #region The coiled levels' own geometry (#207)

        //THE ROPE'S OWN GEOMETRY, and its one hard lesson: a rope has to have DAYLIGHT in it. The first cut ran
        //three strands of 1.6 at a radius of 2.5, which passes every gate and photographs as a shapeless
        //column - at 120 degrees apart, centres 4.3 apart and strands 3.2 wide leave barely a cell between
        //them, and the lattice rounds that away. Four strands of 1.3 at 3.0 leave 1.6 cells of sky between
        //neighbours at rest, which is what makes them read as four things twisted together.
        private const int ROPE_STRANDS = 4;
        private const float ROPE_RADIUS = 3.0f;
        private const float ROPE_STRAND = 1.3f;

        //The steady spin: 0.05 turns a level is 18 degrees, which walks a strand centre 0.94 cells. Added to
        //the weave's own worst 0.79 that is 1.73 against a strand 2.6 across, so consecutive levels of one
        //strand always overlap - which is the only thing making a strand a strand rather than a stack of discs.
        private const float ROPE_TWIST = 0.05f;

        //The weave: how far a strand's angle swings either side of its resting quarter turn, and how fast.
        //See Rope() for why the amplitude is bounded on both sides; the rate is what keeps the per-level walk
        //inside what the strand's own width can bridge.
        private const float ROPE_WEAVE = 0.60f;
        private const float ROPE_WEAVE_RATE = 0.07f;

        /// <summary>How many levels one ink of a strand runs for. See <see cref="Rope"/>.</summary>
        private const int ROPE_COURSE = 4;

        /// <summary>Where strand <paramref name="k"/> points at layout level <paramref name="i"/>.</summary>
        private static float RopeAngle(int k, int i)
        {
            float phase = (float)k / ROPE_STRANDS;
            return MathF.Tau * (phase + ROPE_TWIST * i) + ROPE_WEAVE * MathF.Sin(MathF.Tau * (ROPE_WEAVE_RATE * i + phase));
        }

        /// <summary>
        /// Which strand owns a cell — 1…<see cref="ROPE_STRANDS"/>, or 0 for none. Where two strands overlap the
        /// lower index wins, which matters only to the colouring and is what makes a pinch read as one strand
        /// passing in front of the other rather than as a seam down the middle of the merge.
        /// </summary>
        private static int RopeStrand(float r, float ang, int i)
        {
            float x = r * MathF.Cos(ang);
            float z = r * MathF.Sin(ang);

            for (int k = 0; k < ROPE_STRANDS; k++)
            {
                float theta = RopeAngle(k, i);
                float dx = x - ROPE_RADIUS * MathF.Cos(theta);
                float dz = z - ROPE_RADIUS * MathF.Sin(theta);

                if (dx * dx + dz * dz <= ROPE_STRAND * ROPE_STRAND) return k + 1;
            }

            return 0;
        }

        //The minaret's own geometry. The core is a slim column - 1.6 is four cells across on an unshifted level
        //and nine on a shifted one, which is the thinnest radius that is a solid column whatever the parity -
        //and the ramp is the wedge outside it, out to MINARET_OUTER. 4.6 leaves margin 1 in a 15-wide field.
        private const float MINARET_CORE = 1.6f;
        private const float MINARET_OUTER = 5.3f;

        //0.11 turns a level is 40 degrees, so the ramp makes 1.3 turns over the layout: enough that the player
        //can see it wind, and slow enough that the wedge below still overlaps the one above (see MINARET_WEDGE).
        private const float MINARET_TURN = 0.11f;

        //Half the ramp's angular width, in radians - 31.5 degrees, against a turn of 40 degrees a level, so
        //consecutive courses still overlap by 23. It was 41 degrees and that is what a photograph refused: a
        //wedge 82 wide is nearly a quarter of the ring at every level, and a quarter of a ring on 1.3 turns
        //reads as scattered lumps rather than as one ramp winding. Narrow it and lengthen it - MINARET_OUTER
        //went out to 5.3 in the same change - and the ribbon appears.
        private const float MINARET_WEDGE = 0.55f;

        //How many levels one ink runs for, in the core and on the ramp. They differ so the two never change
        //colour on the same level, which would put a seam straight through the join they are tied at.
        private const int MINARET_COURSE_CORE = 4;
        private const int MINARET_COURSE_RAMP = 3;

        /// <summary>Whether a cell is on the ramp: outside the core, inside the rim, and inside the turning wedge.</summary>
        private static bool MinaretRamp(float r, float ang, int i)
        {
            if (r <= MINARET_CORE || r > MINARET_OUTER) return false;

            return MathF.Abs(WrapAngle(ang - MathF.Tau * MINARET_TURN * i)) <= MINARET_WEDGE;
        }

        //The basket's own geometry: one cylinder of BASKET_RADIUS with a wall BASKET_WALL either side of it, so
        //the shell is two cells thick and reaches 4.4 - margin 2 in a 15-wide field.
        private const float BASKET_RADIUS = 3.4f;
        private const float BASKET_WALL = 1.0f;

        //Three ribs in each family, 120 degrees apart, winding 0.045 turns a level in opposite directions. The
        //two families close on each other at 32 degrees a level, so a rib crosses one of the other family every
        //fourth level - the diamond of the weave is about four levels tall and a quarter of the shell wide.
        private const int BASKET_RIBS = 3;
        private const float BASKET_TURN = 0.045f;

        /// <summary>Half a rib's angular width, in radians — 2.0 cells at <see cref="BASKET_RADIUS"/>.</summary>
        private const float BASKET_RIB = 0.30f;

        /// <summary>How many levels at the glass are a solid course all round. See <see cref="Basket"/>.</summary>
        private const int BASKET_RIM = 2;

        /// <summary>How many levels one ink of a rib runs for.</summary>
        private const int BASKET_COURSE = 4;

        /// <summary>How many sectors the rim is coloured in — six over four inks leaves no two neighbours alike.</summary>
        private const int BASKET_SECTORS = 6;

        private static bool BasketWall(float r) => MathF.Abs(r - BASKET_RADIUS) <= BASKET_WALL;

        private static bool BasketIsRim(int i, int depth) => i >= depth - BASKET_RIM;

        private static int BasketSector(float ang) =>
            (int)MathF.Floor((ang + MathF.PI) / (MathF.Tau / BASKET_SECTORS)) % BASKET_SECTORS;

        /// <summary>
        /// Which rib owns a cell: 1…<see cref="BASKET_RIBS"/> for the family winding one way,
        /// <see cref="BASKET_RIBS"/>+1… for the family winding the other, 0 for the holes between them. A cell
        /// at a crossing belongs to the first family, which is what draws one rib passing over the other.
        /// </summary>
        private static int BasketRib(float ang, int i)
        {
            float twist = MathF.Tau * BASKET_TURN * i;

            for (int k = 0; k < BASKET_RIBS; k++)
            {
                float seat = MathF.Tau * k / BASKET_RIBS;

                if (MathF.Abs(WrapAngle(ang - seat - twist)) <= BASKET_RIB) return k + 1;
                if (MathF.Abs(WrapAngle(ang - seat + twist)) <= BASKET_RIB) return BASKET_RIBS + k + 1;
            }

            return 0;
        }

        //The pendulum's own geometry. The grid is stated here as well as on the design because the rope and the
        //bulb are drawn on the RAW lattice indices and have to find the field's axis themselves - Lean's reason,
        //and the same trap if the two numbers ever disagree.
        private const byte PENDULUM_GRID = 13;

        //Each rope hangs over a corner of a square of side 2*2.4, and is a disc of 1.15 - four or five cells a
        //level, which is the thinnest column that is solid whatever the parity. The pair reaches 3.55.
        private const float PENDULUM_ROPE_SEAT = 2.4f;
        private const float PENDULUM_ROPE = 1.15f;

        /// <summary>
        /// The lowest level a rope is drawn on. Below the bulb's shoulder the ropes would hang <i>outside</i>
        /// the weight down its flanks, which reads as a cage and not as a suspension.
        /// </summary>
        private const int PENDULUM_SHOULDER = 5;

        //The bulb: an ellipsoid of horizontal radius 3.8 (margin 2 in a 13-wide field) and a vertical
        //semi-axis of four LEVELS, centred at 3.5 so it spans the layout's lowest eight and its shoulder
        //reaches 3.52 out at level 5 - wider there than the ropes' own 3.39, so the two meet by construction.
        private const float PENDULUM_BULB = 3.8f;
        private const float PENDULUM_BULB_HALF = 4f;
        private const float PENDULUM_BULB_CENTRE = 3.5f;

        /// <summary>
        /// The emitter's own centred offsets, rebuilt from the raw indices — the <c>x + shift - axis</c> line
        /// for line, with the shift taken off the layout index, which is the same parity as the field level
        /// because <see cref="Emit"/> refuses an odd offset (see <see cref="Lean"/>).
        /// </summary>
        private static void PendulumOffsets(int x, int z, int i, out float dx, out float dz)
        {
            float axis = (PENDULUM_GRID - 1) * HALF + HALF;
            float shift = (i % 2) * HALF;

            dx = x + shift - axis;
            dz = z + shift - axis;
        }

        /// <summary>
        /// Which rope owns a cell — 1…4 by quadrant, 0 for none. The pairing that matters is diagonal: 1 and 4
        /// are opposite corners and so are 2 and 3, which is what <see cref="Pendulum"/>'s two inks are for.
        /// </summary>
        private static int PendulumRope(int x, int z, int i)
        {
            if (i < PENDULUM_SHOULDER) return 0;

            PendulumOffsets(x, z, i, out float dx, out float dz);

            float ex = dx - (dx > 0f ? PENDULUM_ROPE_SEAT : -PENDULUM_ROPE_SEAT);
            float ez = dz - (dz > 0f ? PENDULUM_ROPE_SEAT : -PENDULUM_ROPE_SEAT);

            if (ex * ex + ez * ez > PENDULUM_ROPE * PENDULUM_ROPE) return 0;

            return 1 + (dx > 0f ? 1 : 0) + (dz > 0f ? 2 : 0);
        }

        private static bool PendulumBulb(int x, int z, int i)
        {
            PendulumOffsets(x, z, i, out float dx, out float dz);

            float rise = (i - PENDULUM_BULB_CENTRE) / PENDULUM_BULB_HALF;

            return (dx * dx + dz * dz) / (PENDULUM_BULB * PENDULUM_BULB) + rise * rise <= 1f;
        }

        //The knot's own geometry: the (2, 3) torus knot, which winds twice round the major circle while it goes
        //three times round the minor one. KNOT_RISE is the minor circle's VERTICAL half-axis and is much larger
        //than KNOT_MINOR, its radial one - the torus the knot is drawn on is a tall ellipse in cross-section,
        //because a round one at this major radius would be a flat pretzel lying on its side.
        private const float KNOT_MAJOR = 2.6f;
        private const float KNOT_MINOR = 1.6f;
        private const float KNOT_RISE = 3.6f;

        /// <summary>
        /// How far from the curve a cell may sit and still be part of the knot. Must stay under
        /// <see cref="KNOT_MINOR"/> — see <see cref="Knot"/> for what a fatter tube welds shut.
        /// </summary>
        private const float KNOT_TUBE = 1.15f;

        /// <summary>How finely the curve is sampled. At 1440 the samples are 0.02 apart, well under a cell.</summary>
        private const int KNOT_SAMPLES = 1440;

        /// <summary>How many arcs the loop is coloured in. See <see cref="KNOT_INKS"/> for why twelve.</summary>
        private const int KNOT_ARCS = 12;

        /// <summary>
        /// Which ink each arc takes — <b>a table and not a modulo</b>, because of the one thing about a knot
        /// that no formula on the parameter can express: <b>it touches itself</b>. Arcs half a loop apart are
        /// neighbours in the field, so every such touch has to fall on a colour boundary or the two arcs are
        /// one group, and which arcs touch is a property of the curve rather than of the arithmetic.
        /// <para>
        /// The first cut coloured six arcs by <c>arc % 3</c> and measured <b>33 %</b> best shots against an
        /// expected 12 %, every ink reading as a single group: the loop had welded itself into three pieces.
        /// The two families of touch that do it are the far side (<c>t</c> and <c>t + π</c> share an angle
        /// about the axis and pass <c>2 × KNOT_MINOR</c> apart) and the axis itself (three times a lap the
        /// curve swings in to <c>KNOT_MAJOR − KNOT_MINOR</c>, all three passes at the same height and 120°
        /// apart, so they are <i>mutually</i> in reach — a triangle no <c>% 3</c> can satisfy).
        /// </para>
        /// <para>
        /// <b>The table was solved against the touches this knot actually has, not against those two rules</b>,
        /// and that mattered: measured on the emitted cells there are ten touching arc pairs, nine of them
        /// between arcs that are not loop neighbours at all — half again as many as the two families predict.
        /// Twelve arcs are what let it be solved on three inks, because the rounding in
        /// <see cref="KnotDistance"/> puts every touch at an arc's <b>centre</b> rather than astride a
        /// boundary. It also lands the knot's three <b>anchors</b> — the high points at arcs 1, 5 and 9, the
        /// only cells touching the glass — on three different inks, so no shot can take two of them.
        /// </para>
        /// <para>
        /// <b>It is tied to the four numbers above.</b> Change <see cref="KNOT_MAJOR"/>, <see cref="KNOT_MINOR"/>,
        /// <see cref="KNOT_RISE"/> or <see cref="KNOT_TUBE"/> and the contact graph changes with them; the
        /// figure that says so is the tool's own <b>largest standing group</b>, which is 36 here — about a
        /// twelfth of the level, i.e. one arc. Anything appreciably larger means two arcs have fused and the
        /// table needs re-solving rather than nudging.
        /// </para>
        /// </summary>
        private static readonly int[] KNOT_INKS = { 0, 1, 0, 2, 1, 0, 2, 1, 0, 2, 1, 2 };

        private static readonly Vector3[] KNOT_CURVE = BuildKnotCurve();

        private static Vector3[] BuildKnotCurve()
        {
            Vector3[] curve = new Vector3[KNOT_SAMPLES];

            for (int s = 0; s < KNOT_SAMPLES; s++)
            {
                float t = MathF.Tau * s / KNOT_SAMPLES;
                float radius = KNOT_MAJOR + KNOT_MINOR * MathF.Cos(3f * t);

                curve[s] = new Vector3(
                    radius * MathF.Cos(2f * t),
                    KNOT_RISE * MathF.Sin(3f * t),
                    radius * MathF.Sin(2f * t));
            }

            return curve;
        }

        /// <summary>
        /// How far a cell is from the knot, and which of <see cref="KNOT_ARCS"/> arcs it is nearest — the one
        /// walk that answers both, since the occupancy and the colouring must not be able to disagree about
        /// which part of the loop a ball belongs to.
        /// <para>
        /// The height is measured <b>down from the layout's top level</b> and the curve's own apex is put there:
        /// see <see cref="Knot"/> for why hanging it by its middle would build a level that falls.
        /// </para>
        /// </summary>
        private static float KnotDistance(float r, float ang, int i, int depth, out int arc)
        {
            float x = r * MathF.Cos(ang);
            float z = r * MathF.Sin(ang);
            float y = (i - (depth - 1)) / Constants.SQRT_TWO + KNOT_RISE;

            float best = float.MaxValue;
            int nearest = 0;

            for (int s = 0; s < KNOT_SAMPLES; s++)
            {
                Vector3 point = KNOT_CURVE[s];

                float dx = x - point.X;
                float dy = y - point.Y;
                float dz = z - point.Z;
                float squared = dx * dx + dy * dy + dz * dz;

                if (squared >= best) continue;

                best = squared;
                nearest = s;
            }

            //Rounded to the NEAREST arc rather than floored into one, so an arc is centred on its own share of
            //the loop. That is what puts the curve's six self-touches at arc centres instead of astride
            //boundaries, which is the whole premise KNOT_INKS is solved under
            arc = (int)MathF.Round((float)nearest * KNOT_ARCS / KNOT_SAMPLES) % KNOT_ARCS;

            return MathF.Sqrt(best);
        }

        #endregion
    }
}
