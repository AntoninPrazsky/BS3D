using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>The Grid</b>, block 11 of the campaign (#420): its designs, and the helpers no other block's designs
    /// use. The play order is <see cref="Main"/>'s and the block's name, music and ball style are in the tables
    /// there, exactly as every other block file states.
    /// </summary>
    internal static partial class Program
    {
        #region The grid levels (#420)

        //THE GRID (#420): ten levels in the twentieth scene, and the one block whose style is a THESIS
        //rather than a silhouette family - every level is a NAMED MATHEMATICAL CONSTRUCTION, something an
        //eye can name once it is pointed at, neither a picture nor noise. The scene itself is built on that
        //sentence (#393 draws its floor from a Hilbert curve rather than from a noise field, "named
        //mathematics rather than noise"), and this chapter is the cluster answering the backdrop.
        //
        //WHY THE CONSTRUCTIONS ARE THE ONES THEY ARE. Three of the four families the issue listed are here -
        //self-similar solids (Menger, Sierpinski, Cantor, Koch), a space-filling curve (Hilbert, and the
        //scene's own motif), and a wireframe solid (Tesseract, the finale) - and the fourth, Life as time
        //stacked into height, closes the run of the intricate ones. What they are NOT is decoration: every
        //one of them is a rule stated in a few lines of arithmetic, which is the same claim the scene makes
        //about its floor.
        //
        //⚠ THE BLOCK'S OWN DANGER IS THINNESS, and this file was written against it. Wireframes and thin
        //curves are exactly what the disconnection gate cannot judge - #253's Trellis and #255's Bolt both
        //passed every static check and then stretched and fell in the running game with no shot fired - so
        //every construction here is drawn THICK where the mathematics would allow a line: the Hilbert curve
        //is carved out of a slab rather than built as a tube, Cantor's comb hangs from a solid plate, and
        //the Tesseract's edges are two cells square. The sag probe is the instrument that says whether that
        //was enough, and it is run on all ten rather than on the ones that look risky.
        //
        //THE SCENE'S LIGHT DECIDES THE PALETTE. The Grid lights the balls with its own cool cyan-blue rig,
        //which no other shipped chapter does, so blue, cyan, navy, silver and white are the five colours
        //most likely to converge into each other here. The block draws from the warm end instead - magenta,
        //yellow, green, orange, red - which is also what stands out against a floor that is itself cyan.
        //
        //THE DOME IS INERT and stated rather than defaulted: the Grid replaces the sky (ReplacesSky, its own
        //light rig through TryGetLightRig), exactly as the Mirage's dream does, so GRID_SKY exists to give
        //DescribeBlock something to agree with and changes nothing a player can see.

        private const byte GRID_SKY = 13;

        //Every level of the block draws from these four. Warm against the scene's cyan floor, and no two of
        //them are a confusable pair under the rig (see the block comment above).
        private static readonly BallType[] GRID_PALETTE =
        {
            BallType.Type6,   //magenta
            BallType.Type7,   //yellow
            BallType.Type2,   //green
            BallType.Type9,   //orange
        };

        /// <summary>
        /// <b>The block's opener, and the plainest statement it has: one recursion of a Menger sponge.</b> A
        /// nine-cell cube with the middle third of every axis bored out of it - a cell is gone when two of
        /// its three base-three digits are the middle one - which leaves six square windows, one through
        /// the centre of each face, and twenty smaller cubes joined at their edges.
        /// <para>
        /// It opens the chapter for <see cref="Column"/>'s reason: a chapter states its premise with the
        /// level that has no second idea in it, and the premise here is <i>the shape is a rule</i>. The
        /// sponge is the rule a player can check by eye from the first frame - the holes go all the way
        /// through, and the same hole is in the same place on every face.
        /// </para>
        /// <para>
        /// <b>It hangs by its own top face</b>, which one recursion leaves eight ninths intact, so the
        /// anchor course is the widest and most complete thing in the level - the opposite of the thin
        /// structures the rest of the block has to be careful about. The colouring is by macro-cell: each of
        /// the twenty surviving cubes takes a colour off the palette hash, so a group is a cube of the
        /// sponge and the player takes it apart cube by cube.
        /// </para>
        /// </summary>
        private static Design Menger() => new()
        {
            File = "Menger.json",
            Name = "Menger",
            Grid = 13,
            //Ten courses for a nine-cell sponge: the bottom course is left empty so the layout depth stays
            //even, which Emit requires (an odd offset would move the whole design off where it was drawn).
            Depth = 10,
            Scene = SceneKind.Grid,
            Sky = GRID_SKY,
            Music = MUSIC_GRID,
            Balls = BALLS_GRID,
            Shots = 44,
            CeilingStep = 8,
            OccupiedBlock = (x, z, i, depth) => MengerCell(x, z, i),
            BlockColour = (x, z, i) => Scatter(MengerMacro(x), MengerMacro(z), MengerMacro(i - 1), GRID_PALETTE),
        };

        //The sponge's own figures. NINE cells a side is one recursion, which is the only one that fits: a
        //second would need twenty-seven and a field to match. LOW is where it starts in x and z on a
        //thirteen-wide grid, which leaves the two free columns the lateral-margin gate asks for.
        private const int MENGER_SIDE = 9;
        private const int MENGER_LOW = 2;

        /// <summary>
        /// Whether a cell survives one recursion of the sponge: gone when at least two of its three
        /// coordinates sit in the middle third. The vertical coordinate is the layout level less one, the
        /// empty bottom course being the even-depth padding.
        /// </summary>
        private static bool MengerCell(int x, int z, int i)
        {
            int cx = x - MENGER_LOW;
            int cz = z - MENGER_LOW;
            int cy = i - 1;

            if (cx < 0 || cx >= MENGER_SIDE || cz < 0 || cz >= MENGER_SIDE
                || cy < 0 || cy >= MENGER_SIDE) return false;

            int middles = (cx / 3 == 1 ? 1 : 0) + (cz / 3 == 1 ? 1 : 0) + (cy / 3 == 1 ? 1 : 0);

            return middles < 2;
        }

        /// <summary>Which third of the sponge a raw index is in - the macro-cell the colouring hashes.</summary>
        private static int MengerMacro(int raw) => (raw - MENGER_LOW + 3) / 3;

        /// <summary>
        /// <b>The Sierpinski pyramid, point down, and the chapter's first wildcard.</b> Every course is the
        /// classic parity rule - a cell stands where its two coordinates share no bit, <c>(x &amp; z) == 0</c>
        /// - over a square that widens by two a course, so the solid corner near the point opens into four
        /// quarters, then sixteen, and the widest course against the glass is the fractal entire.
        /// <para>
        /// <b>The wildcard arrives here and it arrives cheap</b>, which is the Eruption's own teaching order
        /// (#369): one ball in <see cref="Design.WildcardEvery"/> is a joker that matches whatever it lands
        /// beside, and on a level whose holes are everywhere it is unmissable - the player gets one before
        /// they have finished reading the shape, and it pays whatever they aim it at. <see cref="Gyroid"/>
        /// is where the same ball becomes the tool rather than the present.
        /// </para>
        /// </summary>
        private static Design Sierpinski() => new()
        {
            File = "Sierpinski.json",
            Name = "Sierpinski",
            Grid = 15,
            Depth = 12,
            Scene = SceneKind.Grid,
            Sky = GRID_SKY,
            Music = MUSIC_GRID,
            Balls = BALLS_GRID,
            Shots = 40,
            CeilingStep = 7,
            //One ball in five is a joker: the campaign's first, and deliberately the most generous cadence
            //it will ever run at (see this design's doc).
            WildcardEvery = 5,
            OccupiedBlock = SierpinskiCell,
            BlockColour = (x, z, i) => Scatter(SierpinskiUnit(x), SierpinskiUnit(z), i / SIERPINSKI_COURSE,
                GRID_PALETTE),
        };

        //THE PYRAMID'S OWN FIGURES, and UNIT is the one that matters. The parity rule draws the triangle
        //out of single cells, and a single-cell Sierpinski is DUST in this lattice: its sub-triangles meet
        //at their corners, corners are diagonals, and a diagonal is not a neighbour on a level - so the
        //first cut measured 114 standing groups of 3.1 balls each, 142 of them standing in pairs, against a
        //budget that priced at 0.35 shots a group. The rule is drawn on a UNIT of two cells instead: eight
        //units across the widest course, every unit four balls that touch each other, and the fractal is
        //the same fractal one order coarser. A construction has to survive being built out of spheres.
        private const int SIERPINSKI_UNIT = 2;
        private const int SIERPINSKI_COURSE = 3;
        private const int SIERPINSKI_AXIS = 7;

        /// <summary>
        /// Whether a cell stands on the pyramid at this course: inside the square that widens with height,
        /// and passing the parity rule that makes the pattern self-similar.
        /// </summary>
        private static bool SierpinskiCell(int x, int z, int i, int depth)
        {
            //How many UNITS wide this course is: one at the point, doubling every fourth course, so the
            //widest course is eight units - the parity rule's own order-three triangle.
            int units = 1 << (i / (depth / 3) > 2 ? 2 : i / (depth / 3));
            int side = units * SIERPINSKI_UNIT;
            int low = SIERPINSKI_AXIS - side / 2;

            int cx = x - low;
            int cz = z - low;

            if (cx < 0 || cx >= side || cz < 0 || cz >= side) return false;

            return ((cx / SIERPINSKI_UNIT) & (cz / SIERPINSKI_UNIT)) == 0;
        }

        /// <summary>Which unit of the fractal a raw index is in - what the colouring hashes, so a group is
        /// a unit or a few of them rather than a scatter of single cells.</summary>
        private static int SierpinskiUnit(int raw) => (raw - SIERPINSKI_AXIS + 8) / SIERPINSKI_UNIT;

        /// <summary>
        /// <b>Cantor's comb: the middle third taken out of a bar, in the plan and again down the height,
        /// hung from a plate.</b> The set itself is dust - take the middle third away for ever and nothing of positive
        /// length survives, which is the whole point of it and is also a layout with no balls in it. What
        /// hangs here is two recursions of the construction in x, drawn as columns of full height, and the
        /// plate they hang from is the thing that makes a set of disconnected intervals into a level.
        /// <para>
        /// <b>The plate is not a cheat and it is where the design is.</b> Cantor's set is disconnected by
        /// construction; a chapter of named constructions cannot answer that by pretending otherwise, so
        /// the level states it: the dust hangs from a solid, and the player sees the middle third missing
        /// while the thing still holds together. Cutting a column drops only that column.
        /// </para>
        /// <para>
        /// <b>⚠ The SECOND recursion is drawn in colour rather than cut out, and that was measured before it
        /// was decided.</b> Taking the middle third out of each column's height is the same construction
        /// one scale down and it reads beautifully - and it left <b>108 balls hanging on nothing</b>, every
        /// column's lower third orphaned the moment the level was built, which is the one thing no gate here
        /// forgives. A column's middle third is therefore a colour of its own: the rule is visible at both
        /// scales, and the load path is whole. The construction says what may be removed; the lattice says
        /// what may be removed and still hang, and where they disagree this block draws the second one.
        /// </para>
        /// </summary>
        private static Design Cantor() => new()
        {
            File = "Cantor.json",
            Name = "Cantor",
            Grid = 15,
            Depth = CANTOR_DEPTH,
            Scene = SceneKind.Grid,
            Sky = GRID_SKY,
            Music = MUSIC_GRID,
            Balls = BALLS_GRID,
            Shots = 42,
            CeilingStep = 7,
            OccupiedBlock = CantorCell,
            BlockColour = CantorColour,
        };

        //The comb's own figures. NINE cells of bar over two recursions (nine thirds into three into one),
        //the plate PLATE_COURSES deep against the glass, and the columns hanging the rest of the way.
        //⚠ THE UNIT IS THREE CELLS AND THE SECOND RECURSION IS VERTICAL, both for the reason the
        //Sierpinski's unit is two: one recursion of the construction on a nine-cell bar leaves thirds of
        //three cells, and a second leaves single cells - sixteen one-cell columns, which is a level made of
        //strings. So the plan carries ONE recursion at three cells a third (four columns, each three by
        //three), and the second is taken out of the HEIGHT of those columns instead: the middle third of
        //every column is missing. The same construction, twice, in two directions, and nothing thinner than
        //a three-cell post.
        private const int CANTOR_DEPTH = 12;
        private const int CANTOR_LOW = 3;
        private const int CANTOR_SIDE = 9;
        private const int CANTOR_UNIT = 3;
        private const int CANTOR_PLATE = 3;

        /// <summary>
        /// Whether a cell is on the plate or on one of the comb's teeth. The plate is the top
        /// <see cref="CANTOR_PLATE"/> courses entire; below it a cell stands only where both x and z survive
        /// two rounds of "take the middle third away".
        /// </summary>
        private static bool CantorCell(int x, int z, int i, int depth)
        {
            int cx = x - CANTOR_LOW;
            int cz = z - CANTOR_LOW;

            if (cx < 0 || cx >= CANTOR_SIDE || cz < 0 || cz >= CANTOR_SIDE) return false;

            if (i >= depth - CANTOR_PLATE) return true;

            //The plan: the middle third of the bar gone, in both axes, at a unit of three cells.
            return cx / CANTOR_UNIT != 1 && cz / CANTOR_UNIT != 1;
        }

        /// <summary>
        /// The plate by quadrant and the columns by the third of their height they stand in - so the middle
        /// third of every column is its own colour, which is the construction's second scale drawn rather
        /// than cut (see the design's doc for the 108 balls that decided it).
        /// </summary>
        private static BallType CantorColour(int x, int z, int i)
        {
            int cx = (x - CANTOR_LOW) / CANTOR_UNIT;
            int cz = (z - CANTOR_LOW) / CANTOR_UNIT;

            //ONE FORMULA FOR BOTH, and the section is what separates them: the plate is section three and a
            //column's three thirds are nought to two, so the middle third of every post is a colour of its
            //own and the plate above it is a fourth. The strides are 1 and 2 across the plan and 3 down the
            //sections, none of them zero modulo four, so no two touching pieces agree - Trilithon's rule.
            //
            //⚠ THE PLATE HAS TO CARRY ALL FOUR COLOURS and that is not decoration: it is the whole anchor
            //course, so the number of colours standing on it is the fewest shots that can ever empty this
            //level (ClearProbe's floor). Drawn as a two-colour check it read three matched shots for 42 % of
            //the field - two for the plate and the comb falling behind it.
            int post = CANTOR_DEPTH - CANTOR_PLATE;
            int section = i >= post ? 3 : i * 3 / post;

            return Band(cx + 2 * cz + 3 * section, GRID_PALETTE);
        }


        /// <summary>
        /// <b>The quadratic Koch island, order one, standing as a prism.</b> Take a square, put a bump of a
        /// third of its side on the middle of every edge, and the outline stops being a square without ever
        /// stopping being made of squares - the plainest fractal boundary there is, and the only one in the
        /// block whose construction is about the EDGE rather than about what is inside it.
        /// <para>
        /// It is a prism because the island is a plan: every course is the same outline, so what the player
        /// reads from any angle is the boundary itself, and what hangs from the glass is that whole outline
        /// at once. The thickest, calmest level of the chapter, and the last one before the constructions
        /// start folding into each other.
        /// </para>
        /// </summary>
        private static Design Koch() => new()
        {
            File = "Koch.json",
            Name = "Koch",
            Grid = 15,
            //SIX COURSES AND NOT TEN: the island is a plan, so the prism's height is free - and at ten it
            //weighed 1170 balls, which is the heaviest thing in the campaign and a level that takes an age
            //to play (#398's whole lesson). Six says the same shape at two thirds the tonnage.
            Depth = 6,
            Scene = SceneKind.Grid,
            Sky = GRID_SKY,
            Music = MUSIC_GRID,
            Balls = BALLS_GRID,
            Shots = 40,
            CeilingStep = 8,
            OccupiedBlock = (x, z, i, depth) => KochIsland(x, z),
            //Strides of 1, 2 and 3 over four colours: no two touching blocks agree, and the top course
            //carries all four, which is what the shortest-clear floor reads.
            BlockColour = (x, z, i) => Band((x - KOCH_LOW) / 3 + 2 * ((z - KOCH_LOW) / 3) + 3 * (i / 3),
                GRID_PALETTE),
        };

        //The island's own figures: a nine-cell square with three-cell bumps, so the outline spans fifteen at
        //its widest and the grid has to be fifteen with the bumps reaching the wall's own column - LOW is
        //where the square starts and the bumps stand proud of it by one unit.
        //⚠ THE BUMP IS TWO CELLS AND THE EDGE DIVISION IS THREE, which is not the construction being sloppy
        //but the field being fifteen wide: a bump of a full third would stand the island's widest span on
        //the field wall itself, and the lateral-margin gate refuses that for a reason nothing here outranks
        //(a ball on the wall has no lateral neighbour to offer, so a shot into that pocket bounces). The
        //outline is the quadratic Koch island with its bumps a third shorter than the construction draws
        //them; what it is a picture of survives intact.
        private const int KOCH_LOW = 3;
        private const int KOCH_SIDE = 9;
        private const int KOCH_UNIT = 3;
        private const int KOCH_BUMP = 2;

        /// <summary>
        /// Whether a cell is inside the island: the square itself, or one of the four bumps standing on the
        /// middle third of its edges. One recursion is all a fifteen-wide field has room for, and it is
        /// enough - the shape is already not a square.
        /// </summary>
        private static bool KochIsland(int x, int z)
        {
            int cx = x - KOCH_LOW;
            int cz = z - KOCH_LOW;

            bool square = cx >= 0 && cx < KOCH_SIDE && cz >= 0 && cz < KOCH_SIDE;

            //The bumps: the middle third of each edge pushed out, which is the same test read past the
            //square's own bounds on one axis while the other stays inside the middle third.
            bool bumpX = cz >= 0 && cz < KOCH_SIDE && cz / KOCH_UNIT == 1
                         && (cx >= -KOCH_BUMP && cx < 0 || cx >= KOCH_SIDE && cx < KOCH_SIDE + KOCH_BUMP);
            bool bumpZ = cx >= 0 && cx < KOCH_SIDE && cx / KOCH_UNIT == 1
                         && (cz >= -KOCH_BUMP && cz < 0 || cz >= KOCH_SIDE && cz < KOCH_SIDE + KOCH_BUMP);

            return square || bumpX || bumpZ;
        }

        /// <summary>
        /// <b>The Hilbert curve, order two, standing in the air as a ribbon</b> - and the one level in the
        /// chapter that is the scene's own motif. The floor of the Grid is a Hilbert curve traced through
        /// its lattice (#393 chose it for the reason this level reuses: no chart, no angle, nothing to
        /// seam), so the cluster hanging over it is the same curve one order coarser, three cells wide and
        /// three courses deep, folded so that a single path fills the square.
        /// <para>
        /// <b>It is a ribbon and not a tube, which is this block's standing rule about thinness.</b> A
        /// space-filling curve drawn as a line of balls is exactly what #253's Trellis was, and it fell in
        /// the running game with no shot fired. Three cells of width and three courses of depth make every
        /// segment a block with neighbours in every direction, and the whole top face is against the glass.
        /// </para>
        /// </summary>
        private static Design Hilbert() => new()
        {
            File = "Hilbert.json",
            Name = "Hilbert",
            Grid = 15,
            Depth = 10,
            Scene = SceneKind.Grid,
            Sky = GRID_SKY,
            Music = MUSIC_GRID,
            Balls = BALLS_GRID,
            Shots = 44,
            CeilingStep = 8,
            OccupiedBlock = (x, z, i, depth) => HilbertRibbon(x, z, i, depth),
            BlockColour = (x, z, i) => Band(HilbertIndex((x - HILBERT_LOW) / HILBERT_UNIT,
                (z - HILBERT_LOW) / HILBERT_UNIT) / 2 + 3 * (i / 3), GRID_PALETTE),
        };

        //The curve's own figures: order two is a four-by-four of nodes, each node HILBERT_UNIT cells square,
        //so the ribbon spans twelve of the fifteen columns. THICK is how many courses of it hang.
        private const int HILBERT_LOW = 2;
        private const int HILBERT_ORDER = 4;
        private const int HILBERT_UNIT = 3;
        private const int HILBERT_THICK = 6;

        /// <summary>
        /// Whether a cell is on the ribbon: inside the curve's square at all, and in the top
        /// <see cref="HILBERT_THICK"/> courses. Every node of the order-two curve is occupied - the curve
        /// visits all sixteen - so what the shape says is not WHERE the ribbon goes but how the colouring
        /// walks it, which is the index below.
        /// </summary>
        private static bool HilbertRibbon(int x, int z, int i, int depth)
        {
            int cx = x - HILBERT_LOW;
            int cz = z - HILBERT_LOW;
            int side = HILBERT_ORDER * HILBERT_UNIT;

            if (cx < 0 || cx >= side || cz < 0 || cz >= side) return false;
            if (i < depth - HILBERT_THICK) return false;

            //The groove: the curve's own path is a channel one cell wide cut down the middle of every node
            //it passes through, on the bottom three courses only, so the ribbon reads as a folded path from
            //below and as a solid plate from above - which is where it hangs from.
            bool groove = i < depth - HILBERT_THICK / 2
                          && cx % HILBERT_UNIT == 1 && cz % HILBERT_UNIT == 1;

            return !groove;
        }

        /// <summary>
        /// Where a node sits along the order-two Hilbert curve, by the standard bit-recursion (Wikipedia's
        /// <c>xy2d</c>, the same walk <c>Grid.fx</c> traces on the floor). The colouring reads it so that
        /// consecutive nodes take consecutive colours: the path is visible as a gradient round the square
        /// rather than as a shape, which is the only honest way to draw a curve that fills everything.
        /// </summary>
        private static int HilbertIndex(int nx, int nz)
        {
            int rx, rz, d = 0;

            for (int s = HILBERT_ORDER / 2; s > 0; s /= 2)
            {
                rx = (nx & s) > 0 ? 1 : 0;
                rz = (nz & s) > 0 ? 1 : 0;
                d += s * s * ((3 * rx) ^ rz);

                //Rotate the quadrant
                if (rz == 0)
                {
                    if (rx == 1)
                    {
                        nx = s - 1 - nx;
                        nz = s - 1 - nz;
                    }

                    (nx, nz) = (nz, nx);
                }
            }

            return d;
        }

        /// <summary>
        /// <b>A helicoid: the minimal surface a straight line sweeps when it turns as it rises.</b> A ramp
        /// winding once round a solid core, one unit thick and three cells wide, so what hangs is a spiral
        /// staircase with no steps - the surface itself, which is what the mathematics means by it.
        /// <para>
        /// <b>The core is the load path and the ramp hangs off it</b>, which is the same answer Cantor's
        /// plate gives: a surface of revolution about an axis is only as strong as the axis, and a helicoid
        /// with nothing down the middle is a ribbon spiralling into the drain. Cutting a turn of the ramp
        /// drops that turn and nothing else; cutting the core is what the player has to earn.
        /// </para>
        /// </summary>
        private static Design Helicoid() => new()
        {
            File = "Helicoid.json",
            Name = "Helicoid",
            Grid = 15,
            Depth = 14,
            Scene = SceneKind.Grid,
            Sky = GRID_SKY,
            Music = MUSIC_GRID,
            Balls = BALLS_GRID,
            Shots = 44,
            CeilingStep = 7,
            Occupied = (r, ang, i, depth) => HelicoidCell(r, ang, i, depth),
            //The ramp is cut into landings by the angle as well as by the height: one colour a quarter turn,
            //stepped again every third course, so a group is a stretch of ramp rather than the whole spiral.
            //At seven groups (the first cut, coloured by radius and height alone) the level was four shots
            //and a shrug.
            Colour = (r, ang, i, depth) => Band(SectorIndex(ang, 0f, HELICOID_LANDINGS) + 2 * (i / 3)
                                                + 3 * (r <= HELICOID_CORE ? 1 : 0), GRID_PALETTE),
        };

        //The helicoid's own figures. CORE is the axis the ramp is welded to, RAMP how far out it reaches,
        //and TURNS how many times it goes round over the layout's height - one, so the whole surface is
        //readable from the gun's opening bearing.
        private const float HELICOID_CORE = 1.9f;
        private const float HELICOID_RAMP = 5.4f;
        private const float HELICOID_TURNS = 1f;
        private const float HELICOID_HALF = 0.22f;

        //How many landings the ramp is cut into round one turn - four, so a group is a quarter of it.
        private const int HELICOID_LANDINGS = 4;

        /// <summary>
        /// Whether a cell is on the core or on the ramp: the ramp is the set of cells whose angle matches
        /// the height's own turn within <see cref="HELICOID_HALF"/> of a turn, which is the surface
        /// <c>angle = k * height</c> given a thickness the lattice can hold.
        /// </summary>
        private static bool HelicoidCell(float r, float ang, int i, int depth)
        {
            if (r <= HELICOID_CORE) return true;
            if (r > HELICOID_RAMP) return false;

            float turns = ang / MathF.Tau + 0.5f;
            float wanted = HELICOID_TURNS * i / (float)depth;
            float apart = MathF.Abs(turns - wanted + 1.5f) % 1f - 0.5f;

            return MathF.Abs(apart) <= HELICOID_HALF;
        }

        /// <summary>
        /// <b>Phyllotaxis: the golden angle, which is how a sunflower packs its seeds and why the spirals in
        /// one are never the same number clockwise and anticlockwise.</b> A solid disc four courses deep,
        /// coloured by which of the golden-angle families a cell falls in - so the construction is in the
        /// COLOUR here rather than in the shape, and it is the one level of the chapter where that is true.
        /// <para>
        /// <b>It is drawn that way on purpose.</b> Every other construction in the block is a rule about
        /// which cells exist; this one is a rule about which cells belong together, and a chapter about
        /// named mathematics should say at least once that a colouring is a construction too. The spiral
        /// arms are what the player takes off, one family at a time, and the disc under them never stops
        /// hanging.
        /// </para>
        /// </summary>
        private static Design Phyllotaxis() => new()
        {
            File = "Phyllotaxis.json",
            Name = "Phyllotaxis",
            Grid = 15,
            Depth = 8,
            Scene = SceneKind.Grid,
            Sky = GRID_SKY,
            Music = MUSIC_GRID,
            Balls = BALLS_GRID,
            Shots = 48,
            CeilingStep = 8,
            Occupied = (r, ang, i, depth) => r <= PHYLLO_RADIUS,
            Colour = (r, ang, i, depth) => PhylloColour(r, ang, i),
        };

        private const float PHYLLO_RADIUS = 5.4f;

        //The golden angle in turns: one minus the reciprocal of the golden ratio, which is what a sunflower
        //puts between one seed and the next and is the only irrational that packs without lanes.
        private const float PHYLLO_GOLDEN = 0.381966f;

        //How many balls to a ring of the seed head - see PhylloArm for what one buys.
        private const float PHYLLO_RING = 1.6f;

        //How far the pattern turns every second course. A quarter would stack the arms into four columns
        //through the whole disc - four standing groups, which is what the first correction overshot to.
        private const float PHYLLO_COURSE = 0.37f;

        /// <summary>
        /// Which spiral arm a cell belongs to: the golden angle stepped once per ring, so the families wind
        /// out from the middle the way a seed head does. The ring is the cell's own radius in units of one
        /// ball, and the level index turns the pattern a little a course so the disc is not four copies of
        /// one plate.
        /// </summary>
        private static BallType PhylloColour(float r, float ang, int i)
        {
            //⚠ HASHED AND NOT BANDED, which took three measurements to accept. A smooth colouring of a
            //solid body is a smooth PARTITION of it: four colours over a turning wedge gave four spiral
            //arms, each connected from the middle to the rim and each a quarter of the level in ONE shot.
            //Stepping the index by the ring and the course did not fix it, and neither did strides of 1, 2
            //and 3 - against four colours some neighbour step is always zero modulo four, and a cross-level
            //neighbour is a diagonal, so the arms welded straight back together every time. A hash of the
            //three indices has no arithmetic for a neighbour step to cancel, which is the same answer the
            //Quarry's Scatter gives its blocks and for the same reason.
            return Scatter((int)MathF.Floor(r / PHYLLO_RING),
                PhylloArm(r, ang, i),
                i / 2,
                GRID_PALETTE);
        }

        /// <summary>Which spiral arm a cell belongs to - the golden angle stepped once per ring, so the
        /// families wind out from the middle the way a seed head does.</summary>
        private static int PhylloArm(float r, float ang, int i)
        {
            //The ring is TWO balls wide and the course pair is what turns the pattern, not every course:
            //at one ball and every course the disc came out as 44 standing groups of 16 balls, which is a
            //sunflower drawn in confetti. The spiral is the same spiral read at half the frequency.
            int ring = (int)MathF.Floor(r / PHYLLO_RING);
            float turns = ang / MathF.Tau + 0.5f + PHYLLO_GOLDEN * ring + PHYLLO_COURSE * (i / 2) + 0.17f * ring;

            return (int)MathF.Floor(turns * GRID_PALETTE.Length);
        }

        /// <summary>
        /// <b>A gyroid: the triply periodic minimal surface that divides space into two interlocking halves
        /// that never touch.</b> One inequality draws the whole thing -
        /// <c>|sin x cos z + sin z cos y + sin y cos x| &lt; t</c> - and what comes out is a body with no
        /// flat face anywhere, no straight edge and no centre, which is the furthest this chapter gets from
        /// a shape a player could have drawn.
        /// <para>
        /// <b>The wildcard is the tool here</b>, which is the other half of the Eruption's teaching order:
        /// <see cref="Sierpinski"/> hands the joker over on a level where anything it touches pays, and this
        /// is the level where the player needs it, because a gyroid has no plate anywhere and every group is
        /// a piece of surface curving out of reach.
        /// </para>
        /// </summary>
        private static Design Gyroid() => new()
        {
            File = "Gyroid.json",
            Name = "Gyroid",
            Grid = 15,
            Depth = 12,
            Scene = SceneKind.Grid,
            Sky = GRID_SKY,
            Music = MUSIC_GRID,
            Balls = BALLS_GRID,
            Shots = 52,
            CeilingStep = 6,
            //One ball in nine: the joker as a tool rather than as a present (see the design's doc).
            WildcardEvery = 9,
            Occupied = (r, ang, i, depth) => false,
            OccupiedBlock = GyroidCell,
            BlockColour = (x, z, i) => Band(x / 4 + 2 * (z / 4) + 3 * (i / 4), GRID_PALETTE),
        };

        //The gyroid's own figures: PERIOD cells to a full wave, THICK how far from the surface a cell may
        //stand and still be part of it, and the box it is cut out of.
        private const float GYROID_PERIOD = 7f;
        //⚠ 1.05, AND THE TWO NUMBERS BEFORE IT ARE WHY THE BLOCK'S THINNESS RULE EXISTS. 0.62 left exactly
        //one cell standing alone out of 597, which the disconnection walk and the lonely-ball gate both
        //refuse. 0.78 carried the whole sheet - and then the SAG PROBE lost the level on five orders out of
        //five, the worst reading in the campaign: a minimal surface is a sheet, a sheet two cells thick has
        //no column anywhere in it, and the remainder folded into the drain as soon as a few shots had gone.
        //At 1.05 the sheet is three cells through its thickest fold and the probe holds it. That is the
        //whole of the block's standing rule arriving on the level that states the rule best.
        private const float GYROID_THICK = 1.05f;
        private const float GYROID_PHASE = 0.7f;
        //⚠ The box is NINE cells and was eleven: the surface grazing a corner of a wider box left exactly
        //one cell standing alone out of 597, which two gates refuse at once (it floats, and it is a ball no
        //single landing can reach). A gyroid is periodic, so a smaller window of it is the same surface -
        //nine cells is one period and a third at GYROID_PERIOD, which is what the shape needs to read.
        private const int GYROID_LOW = 3;
        private const int GYROID_SIDE = 9;

        /// <summary>
        /// Whether a cell is on the surface: the gyroid's own implicit equation, evaluated at the cell's
        /// centre and accepted within <see cref="GYROID_THICK"/>, which is what gives a surface of no
        /// thickness a body two cells deep.
        /// </summary>
        private static bool GyroidCell(int x, int z, int i, int depth)
        {
            int cx = x - GYROID_LOW;
            int cz = z - GYROID_LOW;

            if (cx < 0 || cx >= GYROID_SIDE || cz < 0 || cz >= GYROID_SIDE) return false;

            //The phase is what keeps the surface off the box's own corners: without it the sheet grazed one
            //and left a single ball standing alone, which both the disconnection walk and the lonely-ball
            //gate refuse. A gyroid is periodic, so a phase is a window onto the same surface.
            float a = MathF.Tau / GYROID_PERIOD;
            float fx = a * cx + GYROID_PHASE;
            float fz = a * cz + GYROID_PHASE;
            float fy = a * i;

            float g = MathF.Sin(fx) * MathF.Cos(fz)
                      + MathF.Sin(fz) * MathF.Cos(fy)
                      + MathF.Sin(fy) * MathF.Cos(fx);

            return MathF.Abs(g) < GYROID_THICK;
        }

        /// <summary>
        /// <b>Time as height: twelve generations of Conway's Life, stacked into the twelve courses of the
        /// field.</b> The bottom course is the seed, every course above it is the generation after the one
        /// below, and what the player is looking at is not a shape but a HISTORY - a glider is a staircase
        /// climbing sideways through the cluster, an oscillator is a column that breathes, and a
        /// methuselah's chaos is the part in the middle that nobody could have predicted from the seed.
        /// <para>
        /// <b>⚠ A generation is drawn as itself UNION the one before it, and that is a connectivity fix
        /// rather than a flourish.</b> Two consecutive generations of Life can share almost nothing - that
        /// is what makes the rule interesting - and a course sharing nothing with the course above it is a
        /// course hanging on nothing the moment the level is built. The union guarantees every live cell has
        /// the cell that produced it somewhere above, which is exactly the trail a player reads as time
        /// anyway: what the shape shows is where the pattern HAS been, not only where it is.
        /// </para>
        /// </summary>
        private static Design Life() => new()
        {
            File = "Life.json",
            Name = "Life",
            Grid = 15,
            Depth = 12,
            Scene = SceneKind.Grid,
            Sky = GRID_SKY,
            Music = MUSIC_GRID,
            Balls = BALLS_GRID,
            Shots = 50,
            CeilingStep = 7,
            OccupiedBlock = (x, z, i, depth) => LifeCell(x, z, i, depth),
            BlockColour = (x, z, i) => Band(x / 3 + 2 * (z / 3) + 3 * (i / 3), GRID_PALETTE),
        };

        //The board and the seed. The R-pentomino is the methuselah every book opens with - five cells that
        //take eleven hundred generations to settle - and eleven of its generations are what fit here.
        private const int LIFE_SIDE = 11;
        private const int LIFE_LOW = 2;

        private static bool[,] _lifeStack;

        /// <summary>
        /// Whether a cell is live in the generation this course carries. The whole stack is computed once,
        /// on the first call, because a course is a function of every course below it and Emit asks cell by
        /// cell - the alternative is re-running the automaton for every ball in the level.
        /// </summary>
        private static bool LifeCell(int x, int z, int i, int depth)
        {
            _lifeStack ??= LifeStack(depth);

            int cx = x - LIFE_LOW;
            int cz = z - LIFE_LOW;

            return cx >= 0 && cx < LIFE_SIDE && cz >= 0 && cz < LIFE_SIDE && _lifeStack[cx + cz * LIFE_SIDE, i];
        }

        /// <summary>
        /// Runs the automaton once and keeps every generation: course 0 is the seed and course i is the i-th
        /// generation UNION everything before it, which is what keeps the stack connected (see the design's
        /// own doc for why that union is not optional).
        /// </summary>
        private static bool[,] LifeStack(int depth)
        {
            bool[,] stack = new bool[LIFE_SIDE * LIFE_SIDE, depth];
            bool[] live = new bool[LIFE_SIDE * LIFE_SIDE];

            //The R-pentomino, centred
            int mid = LIFE_SIDE / 2;
            foreach ((int dx, int dz) in new[] { (0, -1), (1, -1), (-1, 0), (0, 0), (0, 1) })
                live[mid + dx + (mid + dz) * LIFE_SIDE] = true;

            for (int gen = 0; gen < depth; gen++)
            {
                for (int c = 0; c < live.Length; c++)
                    if (live[c])
                        stack[c, gen] = true;

                //Every course carries everything that came before it
                if (gen > 0)
                    for (int c = 0; c < live.Length; c++)
                        if (stack[c, gen - 1])
                            stack[c, gen] = true;

                live = LifeStep(live);
            }

            return stack;
        }

        /// <summary>One generation of Conway's rule on a bounded board: three neighbours make a cell, two
        /// keep it, anything else clears it.</summary>
        private static bool[] LifeStep(bool[] live)
        {
            bool[] next = new bool[live.Length];

            for (int z = 0; z < LIFE_SIDE; z++)
                for (int x = 0; x < LIFE_SIDE; x++)
                {
                    int neighbours = 0;

                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dz == 0) continue;

                            int nx = x + dx, nz = z + dz;
                            if (nx < 0 || nx >= LIFE_SIDE || nz < 0 || nz >= LIFE_SIDE) continue;
                            if (live[nx + nz * LIFE_SIDE]) neighbours++;
                        }

                    bool alive = live[x + z * LIFE_SIDE];
                    next[x + z * LIFE_SIDE] = neighbours == 3 || alive && neighbours == 2;
                }

            return next;
        }

        /// <summary>
        /// <b>The chapter's finale: a tesseract, which is to say the shadow one casts.</b> A four-dimensional
        /// cube has no shape a player can be shown; what everyone means by the word is its projection - a
        /// cube inside a cube with the corresponding corners joined - and that is what hangs here, every
        /// edge two cells square, the outer frame against the glass and the inner one slung inside it on
        /// eight struts.
        /// <para>
        /// <b>It is the block's one true wireframe and therefore the level its whole thinness rule was
        /// written for.</b> Twelve outer edges, twelve inner ones and eight diagonals is a closed frame -
        /// <see cref="Knot"/>'s own argument, the one topology where a single cut drops nothing - and every
        /// member of it is a square post rather than a line of balls. The four colours go one to a family
        /// (the outer frame, the inner frame, the struts, the corners they meet at), so what the player
        /// takes apart is the drawing rather than a region of it.
        /// </para>
        /// </summary>
        private static Design Tesseract() => new()
        {
            File = "Tesseract.json",
            Name = "Tesseract",
            Grid = 15,
            Depth = 12,
            Scene = SceneKind.Grid,
            Sky = GRID_SKY,
            Music = MUSIC_GRID,
            Balls = BALLS_GRID,
            Shots = 54,
            CeilingStep = 6,
            //One ball in eleven: the joker is a rarity by the finale, which is the cadence the block ends on.
            WildcardEvery = 11,
            OccupiedBlock = (x, z, i, depth) => TesseractPart(x, z, i, depth) != 0,
            //⚠ ONE COLOUR A FAMILY WAS A ONE-SHOT LEVEL. The outer frame is a closed loop of 432 balls, so
            //painting it one colour made it a single standing group holding everything else: one ball
            //anywhere on it took 640 of 640. The frames are cut into sections instead - the hash is over the
            //part AND the quarter of the box a cell stands in - so an edge is a group and the drawing has to
            //be taken apart edge by edge, which is what a wireframe should ask for.
            BlockColour = (x, z, i) => Scatter(TesseractPart(x, z, i, TESSERACT_DEPTH) * 7 + x / 4,
                z / 4, i / 4, GRID_PALETTE),
        };

        //The tesseract's own figures: the outer cube's span, the inner cube's, and the two-cell post every
        //edge is drawn with.
        private const int TESSERACT_DEPTH = 12;
        //TWO AND TWELVE, not one and thirteen: a strut is a distance test with a reach of just over a
        //cell, so a corner standing in column one puts balls in column zero and the lateral-margin gate
        //refuses the layout outright. The frame is a cell narrower than the field allows on purpose.
        private const int TESSERACT_LOW = 2;
        private const int TESSERACT_HIGH = 12;
        private const int TESSERACT_INNER_LOW = 5;
        private const int TESSERACT_INNER_HIGH = 9;
        //THREE AND NOT TWO, on the sag probe's word: at two cells the frame lost four orders in five - a
        //wireframe is all edge and no body, which is exactly what #253's Trellis was. Three cells square is
        //still a post rather than a slab, and the drawing reads the same.
        private const int TESSERACT_POST = 3;

        /// <summary>
        /// What a cell is: 1 the outer frame, 2 the inner frame, 3 a strut between them, 0 nothing. An edge
        /// is a cell within <see cref="TESSERACT_POST"/> of two of the three axes' own extremes, which is
        /// the same test for both cubes with different bounds.
        /// </summary>
        private static int TesseractPart(int x, int z, int i, int depth)
        {
            if (TesseractFrame(x, z, i, TESSERACT_LOW, TESSERACT_HIGH, 0, depth - 1)) return 1;
            if (TesseractFrame(x, z, i, TESSERACT_INNER_LOW, TESSERACT_INNER_HIGH,
                    depth / 4, depth - 1 - depth / 4)) return 2;

            return TesseractStrut(x, z, i, depth) ? 3 : 0;
        }

        /// <summary>Whether a cell is on a cube's wireframe: near the extreme of at least two of its three
        /// axes, which is what an edge of a box IS.</summary>
        private static bool TesseractFrame(int x, int z, int i, int low, int high, int floor, int ceiling)
        {
            if (x < low || x > high || z < low || z > high || i < floor || i > ceiling) return false;

            int atX = x < low + TESSERACT_POST || x > high - TESSERACT_POST ? 1 : 0;
            int atZ = z < low + TESSERACT_POST || z > high - TESSERACT_POST ? 1 : 0;
            int atY = i < floor + TESSERACT_POST || i > ceiling - TESSERACT_POST ? 1 : 0;

            return atX + atZ + atY >= 2;
        }

        /// <summary>
        /// Whether a cell is on one of the eight struts joining a corner of the outer cube to the matching
        /// corner of the inner one - the projection's own diagonals, and the part of the drawing that makes
        /// it a tesseract rather than two cubes.
        /// </summary>
        /// <remarks>
        /// <b>⚠ A lattice has no diagonals, so a strut is a DISTANCE TEST rather than a line.</b> The first
        /// cut tried to draw them as a run of cells near the corners and produced two things at once: 128
        /// balls hanging on nothing, the inner cube among them, and a layout standing on the field wall,
        /// because the corner test forgot its own lower bound and reached column zero. What is drawn now is
        /// every cell within <see cref="TESSERACT_STRUT"/> of the segment between the two corners, which is
        /// a staircase in the lattice and a straight line to the eye.
        /// </remarks>
        private static bool TesseractStrut(int x, int z, int i, int depth)
        {
            int floor = depth / 4;
            int ceiling = depth - 1 - floor;

            for (int corner = 0; corner < 8; corner++)
            {
                float ox = (corner & 1) == 0 ? TESSERACT_LOW : TESSERACT_HIGH;
                float oz = (corner & 2) == 0 ? TESSERACT_LOW : TESSERACT_HIGH;
                float oy = (corner & 4) == 0 ? 0 : depth - 1;

                float ix = (corner & 1) == 0 ? TESSERACT_INNER_LOW : TESSERACT_INNER_HIGH;
                float iz = (corner & 2) == 0 ? TESSERACT_INNER_LOW : TESSERACT_INNER_HIGH;
                float iy = (corner & 4) == 0 ? floor : ceiling;

                if (NearSegment(x, z, i, ox, oz, oy, ix, iz, iy, TESSERACT_STRUT)) return true;
            }

            return false;
        }

        /// <summary>How far from the segment between two points a cell may stand and be part of it. The
        /// vertical axis is measured in level steps rather than world units, which is what makes a strut
        /// read as a straight run in the picture the player sees rather than in the world.</summary>
        private const float TESSERACT_STRUT = 1.45f;

        /// <summary>
        /// Whether a lattice cell lies within <paramref name="reach"/> of the segment joining two points -
        /// the standard point-to-segment distance, clamped to the segment's own ends.
        /// </summary>
        private static bool NearSegment(int x, int z, int i,
            float ax, float az, float ay, float bx, float bz, float by, float reach)
        {
            float dx = bx - ax, dz = bz - az, dy = by - ay;
            float length = dx * dx + dz * dz + dy * dy;

            if (length <= 0f) return false;

            float t = ((x - ax) * dx + (z - az) * dz + (i - ay) * dy) / length;
            t = MathF.Max(0f, MathF.Min(1f, t));

            float px = ax + t * dx - x;
            float pz = az + t * dz - z;
            float py = ay + t * dy - i;

            return px * px + pz * pz + py * py <= reach * reach;
        }

        #endregion
    }
}
