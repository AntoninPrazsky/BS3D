using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>The Arcade</b>, block 10 of the campaign: its designs, and the helpers no other block's designs use, in
    /// the order <c>Program.cs</c> held them — the play order is <see cref="Main"/>'s, and the block's name, music
    /// and ball style are in the tables there. Split out of <c>Program.cs</c> in #386.
    /// </summary>
    internal static partial class Program
    {


        #region The arcade levels

        //THE TENTH AND LAST BLOCK in play order (#300): five HOLLOW SOLIDS with pixel art wrapped onto them, hanging over a neon city.
        //It is THE GALLERY GIVEN A THIRD DIMENSION, and that is the whole statement of it. Block 2 draws a
        //symbol on a flat wall and the player reads all of it from where the gun starts; these levels draw
        //the same kind of picture onto a body that HAS sides - a cube of arcade glyphs, a stepped temple, a
        //slot reel, a donut, a globe - so the picture is read by walking the gun round the level and no
        //single vantage shows the whole of it. Nothing here is a wall.
        //
        //EVERY SOLID IS HOLLOW: the layout is its SURFACE, and what stands inside it is FURNITURE, not fill.
        //Two things follow. A body that fills the frame costs the ball count of an ordinary level rather than
        //of a quarry (a solid ten-cell cube is 1200 balls; its shell is 560), and the first panel to come away
        //opens a window into an empty room, which is what makes a level read as an object rather than as a
        //mass. The sentence said "nothing stands inside it" until the sag probe amended it twice over: a
        //one-cell wall is a chain that yields under sustained load, and two of the five now carry a measured
        //interior brace — Cabinet's CRT shelf (#301) and Cube's corner posts (#317) — each diegetic, each a
        //few dozen balls, each there because the probe read the bare shell losing. The hollowness that
        //matters (the window into the room, the shell's ball price) survives both.
        //
        //A HOLLOW BODY'S ANCHOR IS WHATEVER ITS OWN TOP HAPPENS TO BE, and that is the trap this block had to
        //answer five times. Only the field's topmost level is bonded to the glass, so the anchor is a 100-cell
        //plate on the cube, a 49-cell plate on the temple, a disc on the reel's head, an annulus on the donut
        //and the globe's arctic plateau - which the sphere's own geometry made NINE cells until #301's probe
        //watched a single match leave 550 balls on four of them; GLOBE_POLE_CAP flattens the pole into 37
        //(measured, 16.2 balls each). Painted in ONE colour, any of them is a level that ends on the first
        //lucky ball of it. Every cap here therefore carries at least two colours, interleaved - the globe's
        //ice is broken into floes for exactly this reason and not for geography's - and the drop test is the
        //check: the whole block's best single shot is 11 %.
        //
        //EVERY LEVEL IS FRAMED WHOLE - field 18, the deepest the camera frames (GameplayScreen.FRAMED_LEVELS)
        //- and that is a deliberate answer to the tall blocks before it (the Tower's, the Nebula's and,
        //since #300 reordered the cities, the Spectrum's ten as the campaign's second-to-last word). A tall
        //level's premise is that
        //you cannot see all of it; an object meant to be RECOGNISED has to be in shot, all of it, from the
        //first second. The 18 is also what buys the ceiling somewhere to descend into: the empty levels under
        //a layout are the level's clearance, and these five leave four, six or ten of them (ARCADE_FIELD).
        //
        //THE PIXELS ARE BLOCKS OF CELLS, NEVER SINGLE ONES. A colour region one cell across is a group of one
        //that the repair pass recolours - the drawing would be quietly rewritten between the source and the
        //file - so every ground here is dithered in blocks and every glyph hole is at least 2x2. How coarse
        //those blocks are is the block's real difficulty dial, because it sets the GROUP COUNT and the group
        //count sets the budget: see CUBE_GROUND_BLOCK, where the same cube measured 75, 44 and 34 groups at
        //three block sizes. All five budgets are priced off the ratio Validate prints. THEY SHIPPED AS A
        //RAMP - 1.65 -> 1.58 -> 1.50 -> 1.44 -> 1.37 - and the sag fixes have since bent it rather than
        //retuned it: the opener reads 1.33 (Cube's corner posts bought eight groups, #317), the reel and
        //ziggurat stand where they shipped, the donut reads 1.49 (#317 restriped its bands, 35 groups on the
        //same 52 shots) and the finale 1.40 (#301's two-cell wall merged blocks through its thickness and
        //the budget was re-priced - see Globe's own doc). Nothing was re-slotted on the number: the naive
        //ratio undercounts every level whose fix added designed cascades (the Spectrum's ramp comment records
        //why the count ranks the wrong quantity), and the play order is the owner's, not the arithmetic's.
        //
        //THE COLOURS, and what each level puts the #152 five next to:
        //  Cube     silver and orange - the three ground pairs of its faces, warm against cool,
        //           with yellow/green corner trim on the posts (#317 - see CUBE_POST_RUN).
        //  Ziggurat brown and olive   - a sandstone temple, both against gold and white.
        //  Reel     silver and navy   - a white reel with red sevens, its heads in cold metal.
        //  Donut    brown and orange  - the dough under a magenta glaze, sprinkles over it.
        //  Globe    navy, olive, brown - the ocean, the forest and the dry land of a pixel Earth,
        //           with black for the water between the polar floes (#301 - see GLOBE_POLAR).

        /// <summary>
        /// A hollow cube ten cells on a side with an arcade glyph on every face the player can see: an
        /// invader, a key, a coin and a lightning bolt round the four walls, and a cross on the bottom plate
        /// the game's low camera reads best. The block's opener and its plainest statement — a cube is the
        /// shape whose pixel grid needs no explaining, and one big symbol a face is exactly what the Gallery
        /// drew on a flat wall, put where it has to be walked around.
        /// <para>
        /// <b>The four walls are drawn in two ground pairs and the plates in a third.</b> A single dithered
        /// ground over a whole cube is one colour reaching every face through the edges, the top plate — the
        /// anchor — included. Opposite faces share a pair, so the gun's orbit alternates warm and cool and no
        /// ground colour meets its own kind round a corner. <b>Both pairs are bright</b>, which is a
        /// legibility finding rather than a taste: the walls were cyan-and-navy first and the black glyph on
        /// them was invisible in the running game — half of every face was as dark as the drawing on it.
        /// </para>
        /// <para>
        /// The glyphs are all black, which is a balance decision rather than a palette one: five glyphs in
        /// five colours would be five colours holding one group each, and the magazine draws evenly among the
        /// colours still standing rather than among the balls.
        /// </para>
        /// <para>
        /// <b>Four corner posts stand inside it since #317</b> — a second cell at each corner, plate to
        /// plate, wearing their own yellow/green trim — because the one-cell walls are chains and the sag
        /// probe watched one wall block take the line down five units in a single shot; see
        /// <see cref="CUBE_POST_RUN"/> for both halves of that finding, the mezzanine that measured worse
        /// included.
        /// </para>
        /// <para>
        /// Measured (#317): 600 balls in 42 groups (1.33 shots a group, Cabinet's own ratio), margin 1,
        /// nothing alone, 8 in pairs, 2 recoloured; colour counts 20–120 and every colour's best single
        /// shot at most 6 % (the black of all five glyphs). Sag probe: <b>2 of 5, twice independently</b>,
        /// neither loss with the glass at rest — Saturn's band, from the shipped shape's 4 of 5 with two
        /// first-phase deaths on a resting glass.
        /// </para>
        /// </summary>
        private static Design Cube() => new()
        {
            File = "Cube.json",
            Name = "Cube",
            Grid = CUBE_GRID,
            Depth = CUBE_DEPTH,
            FieldLevels = ARCADE_FIELD,
            Scene = SceneKind.NeonCity,
            Sky = ARCADE_SKY,
            Music = MUSIC_ARCADE,
                Balls = BALLS_ARCADE,
            Shots = 56,
            //#288: 8 until the owner reported this level among the four that lose to the line (eight left
            //1.18 of headroom; nine bought 1.78). #317 then moved it again, and in Cabinet's exact shape:
            //with the corner posts in, the box's at-rest fault is cured structurally (settled line 5.45,
            //was 4.94) and every remaining probe loss was a stepped hairline -1.02..-1.06 - within one
            //step's 0.60 of surviving, all four of them. Sixteen is that step handed back: 56 shots buy
            //three descents, 1.80, headroom 3.65 - and the probe reads 2 of 5, twice independently, where
            //nine read 4 of 5 (Saturn's own band, and neither remaining loss is at rest).
            CeilingStep = 16,
            OccupiedBlock = (x, z, i, depth) => CubeFace(x, z, i) != 0 || CubePost(x, z, i) != 0,
            BlockColour = CubeColour,
        };

        /// <summary>
        /// A stepped temple: four square courses widening as they descend, hollow, hanging from the plate of
        /// its own flat top. The one level in the block whose shape is the lattice's own — a square course is
        /// exactly what a ring of cells is — so nothing here is rounded off and every step edge is a straight
        /// line of balls.
        /// <para>
        /// <b>It hangs the way a hanging cluster wants to and not the way a pyramid stands.</b> Point-up, a
        /// pyramid's whole mass would hang from the four cells of its apex; the courses widen downward
        /// instead, so each hangs off the wider parallel run of links above it and the top plate — 49 cells of
        /// it — carries the level. What that costs is the postcard silhouette; what it buys is a temple you
        /// can look up into.
        /// </para>
        /// <para>
        /// <b>It is also the level that taught this block what a hollow body may weigh.</b> Drawn first as
        /// one-cell rings twelve levels deep it sagged past the death line in eight seconds with no shot
        /// fired — see <see cref="ZIGGURAT_WALL"/>, which is the fix and the whole story.
        /// </para>
        /// <para>
        /// <b>The colouring runs down the courses and never round them.</b> A course in one colour is the sole
        /// anchor of everything below it — the horizontal-band trap, and here it would have been a one-shot
        /// level — so the ring is cut into nine columns around and the palette advances two per column and one
        /// per course. Five colours against that stride leaves no two touching blocks the same, diagonals
        /// included, so the pattern is a staircase turning as it descends rather than a set of stripes.
        /// </para>
        /// <para>
        /// Measured: 521 balls in 33 groups (1.58 shots a group), margin 1, nothing alone or paired, 1
        /// recoloured; counts 97–116, best single shots 3–6 %. Hung unshot for 35 s in the running game.
        /// </para>
        /// </summary>
        private static Design Ziggurat() => new()
        {
            File = "Ziggurat.json",
            Name = "Ziggurat",
            Grid = ZIGGURAT_GRID,
            Depth = ZIGGURAT_DEPTH,
            FieldLevels = ARCADE_FIELD,
            Scene = SceneKind.NeonCity,
            Sky = ARCADE_SKY,
            Music = MUSIC_ARCADE,
                Balls = BALLS_ARCADE,
            Shots = 52,
            CeilingStep = 7,
            OccupiedBlock = (x, z, i, depth) => ZigguratCourseWall(x, z, i),
            BlockColour = ZigguratColour,
        };

        /// <summary>
        /// A slot machine's reel: a hollow drum with a head at each end and four panels round its wall showing
        /// a seven, a diamond, a seven and a diamond. The block's first curved body, and the one whose picture
        /// is <b>wrapped</b> rather than laid on a flat face — the wall is cut into twenty-four sectors, six to
        /// a panel, and the symbol is drawn in those sectors exactly as the Gallery's bitmaps are drawn in
        /// lattice columns.
        /// <para>
        /// The wall is two cells thick and the symbol is a function of the sector alone, so it goes right
        /// through the wall: a panel's inside carries the same drawing as its outside, which doubles every
        /// stroke's group the way <c>PICTURE_THICKNESS</c> does on a flat one, and means the picture is still
        /// there when the outer skin has gone.
        /// </para>
        /// <para>
        /// The two heads are turned <see cref="Ring"/>s of cold metal rather than a dither, which is a
        /// gameplay answer as much as a look: a block dither on a horizontal plate has no diagonal neighbours
        /// to percolate through, so it came out as three dozen groups of four — half a budget spent on the
        /// drum's lids. Concentric rings are one group each and read as a machined end.
        /// </para>
        /// <para>
        /// Measured: 537 balls in 40 groups (1.50 shots a group), margin 1, nothing alone (24 in pairs, the
        /// inner rim of the wall where a sector holds one cell), 4 recoloured; counts 65–114, and the block's
        /// biggest single shots are here — a diamond at 10 % and a seven at 8 %, which is what a symbol drawn
        /// in one ink is worth. Hung unshot for 35 s in the running game.
        /// </para>
        /// </summary>
        private static Design Reel() => new()
        {
            File = "Reel.json",
            Name = "Reel",
            Grid = REEL_GRID,
            Depth = REEL_DEPTH,
            FieldLevels = ARCADE_FIELD,
            Scene = SceneKind.NeonCity,
            Sky = ARCADE_SKY,
            Music = MUSIC_ARCADE,
                Balls = BALLS_ARCADE,
            Shots = 60,
            CeilingStep = 8,
            Occupied = (r, ang, i, depth) => ReelShell(r, i, depth),
            Colour = ReelColour,
        };

        /// <summary>
        /// A glazed donut: a hollow ring, dough below and a magenta glaze that has run down its sides, with
        /// sprinkles scattered over the glaze. The only level in the game with a <b>hole</b> through it — the
        /// gun can shoot clean through the middle of this one — and the only one whose surface faces inward as
        /// well as out.
        /// <para>
        /// <b>The glaze line is wavy on purpose and the waves are load-bearing twice.</b> A glaze cut at a
        /// level boundary would be a horizontal band round the whole ring, which is both the drop-test trap
        /// and, on a body whose top surface is its anchor, the one colouring that could take the level in a
        /// single shot. Waved, glaze and dough interlock at eight places round the ring (#317; six until the
        /// probe asked for shorter free arcs), so each is anchored on
        /// its own and neither can drop the other — <see cref="OnionShell"/>'s staves, arrived at from a
        /// different direction.
        /// </para>
        /// <para>
        /// The sprinkles are hashed rather than drawn, one block of the glaze in four. They are the level's
        /// scarce colours — 40 balls each against 66 to 100 of the other six — so the shots that spend them
        /// are the ones worth waiting for.
        /// </para>
        /// <para>
        /// <b>⚠ The body is coloured in stripes and three inks a band, and #317 is why</b>: as shipped (two
        /// inks on 2×2 blocks) each body colour fused into one diagonal staircase winding round the ring —
        /// 49 to 66 balls a network — and the sag probe read the level 4–5 of 5 losing orders: two such
        /// releases sever the ring's path to the top annulus across several bearings, the arc between them
        /// dangles (a body from level 15 measured eight units under its own seat), and the third descent
        /// finishes it. Stripes make percolation impossible by construction, the eight-drip wave bridges a
        /// dangling arc every three sectors, and the step went 6 → 10 for the endgame the sum cannot see —
        /// the three constants carry the measured ladder, including the two block-grid attempts that failed.
        /// </para>
        /// <para>
        /// Measured (#317): 588 balls in 35 groups (1.49 shots a group) — the biggest layout in the block —
        /// margin 1, nothing alone, 4 in pairs, 1 recoloured (was 9; the stripes suit the inner face);
        /// counts 39–100, best single shots 2–5 %; anchor load 13.4. Sag probe: a stable 2 of 5 across three
        /// sweeps, every loss a stepped-glass hairline at −1.0 past shot 18 — Saturn's own reading, where the
        /// shipped level read 4–5 of 5 with full-weight dangles from shot 12.
        /// </para>
        /// </summary>
        private static Design Donut() => new()
        {
            File = "Donut.json",
            Name = "Donut",
            Grid = DONUT_GRID,
            Depth = DONUT_DEPTH,
            FieldLevels = ARCADE_FIELD,
            Scene = SceneKind.NeonCity,
            Sky = ARCADE_SKY,
            Music = MUSIC_ARCADE,
                Balls = BALLS_ARCADE,
            Shots = 52,
            //#317: 6 until the sag probe read the level 4-5 of 5 losing orders. The paper #288 sum passes at
            //either step (clearance 9.0 at settle; eight descents of 0.60 consume 4.80), and that is exactly
            //the sum's stated blind spot: a torus eaten to half its mass hangs its surviving arcs four to six
            //units below their lattice seats, so the REAL late-game margin is a fraction of the authored one.
            //With the stripe recolour in (see DONUT_DOUGH) every remaining loss was an endgame hairline: a
            //ring eaten to 150-300 balls whips an arc to exactly -1.0 with the glass two or three steps down
            //(-1.8 at six, -1.2 at eight; both read 2-3 of 5, losses at shots 17-19). Ten hands the death
            //window a step back - 52 shots buy five descents, 3.00 of the 9.0 the level settles with - and
            //with the eight-drip wave the probe reads a STABLE 2 of 5 across three sweeps, every loss a
            //stepped-glass hairline at shots 18-23: Saturn's own reading, the calibrated finishable control.
            //A cleared-down torus can always whip its last arcs; what #317 removed is the full-weight dangle.
            //The pressure dial moves from the block's fastest to third (Reel 8, Ziggurat 9, Cube 9, Globe 14).
            CeilingStep = 10,
            Occupied = (r, ang, i, depth) => DonutShell(r, i, depth),
            Colour = DonutColour,
        };

        /// <summary>
        /// The finale: a pixel Earth. A hollow globe with a world drawn round it in sixteen columns of
        /// longitude and fourteen rows of latitude, one row per level of the layout — two pole-to-pole
        /// continents mottled in three land inks, two narrow oceans, broken ice at both poles —
        /// and the last thing the campaign shows, after a chapter spent in the void, is the planet it left.
        /// <para>
        /// Hard by <b>fineness</b> rather than by scarcity, which is <see cref="Garland"/>'s job five levels
        /// earlier and deliberately not repeated: eight colours over 30 groups, on a budget re-priced to the
        /// block's second-tightest ratio. There is no big payoff anywhere in it — the best single shot is 6 %,
        /// where the reel offers 10 (the donut offered 11 until #317 restriped it down to 5 — its staircases
        /// were the sag fault, not a designed payoff) — and the ceiling arrives on schedule while it is worked.
        /// </para>
        /// <para>
        /// <b>The ice caps are broken, and that is the anchor rule rather than geography.</b> The top level is
        /// the arctic plateau (<see cref="GLOBE_POLE_CAP"/>, 37 cells measured) — paint it one colour and the
        /// first lucky ball of it takes everything hanging underneath. Both caps are therefore ice in floes
        /// with the polar water between them in its own ink (<see cref="GLOBE_POLAR"/>), which reads as pack
        /// ice and leaves most of the plateau standing whatever is shot: measured, the worst single shot
        /// leaves 562 balls on 16 anchors.
        /// </para>
        /// <para>
        /// <b>It is the level #301 rebuilt, and the record of why is on its constants</b>: the polar water's
        /// own ink and the narrow oceans (<see cref="GLOBE_WORLD"/>'s last two rules), the two-cell wall
        /// (<see cref="GLOBE_SHELL"/>), the flattened anchor (<see cref="GLOBE_POLE_CAP"/>) and the re-priced
        /// budget. The sag probe read 5 of 5 losing orders on the shipped drawing, dying at shots 5–10 with
        /// the glass at rest; after the rebuild it reads 2–3 of 5 across repeat runs (the multithreaded
        /// solver is not bit-deterministic, so borderline orders flip) — the band <see cref="Saturn"/> (2)
        /// and <see cref="Amphora"/> (3) sit in, and both are finishable from play — with the surviving
        /// orders clearing the level outright at shot 28 of 42.
        /// </para>
        /// <para>
        /// Measured: 599 balls in 30 groups (1.40 shots a group), margin 1, nothing alone, 0 recoloured;
        /// counts 52–99, best single shots 3–6 %; 37 ceiling anchors carrying 16.2 balls each. Still the
        /// shallowest clearance in the block — ~4 above the line, with the 42-shot budget buying three
        /// descents (1.8) — so the ceiling is on the campaign's last level's shoulder all the way down,
        /// which is the pressure it is meant to end under, and it no longer arrives first.
        /// </para>
        /// </summary>
        private static Design Globe() => new()
        {
            File = "Globe.json",
            Name = "Globe",
            Grid = GLOBE_GRID,
            Depth = GLOBE_DEPTH,
            FieldLevels = ARCADE_FIELD,
            Scene = SceneKind.NeonCity,
            Sky = ARCADE_SKY,
            Music = MUSIC_ARCADE,
                Balls = BALLS_ARCADE,
            //42, re-priced off the measured ratio after the #301 rebuild: the two-cell wall merged the dither
            //blocks through its thickness and the standing groups went 38 to 30, so the shipped 52 read 1.73
            //shots a group — outside the block's 1.3–1.7 band and gentler than the opener. 42 reads 1.40,
            //back beside the finale's priced 1.37 and still tighter than Donut's 1.44.
            Shots = 42,
            //14, not the 9 it shipped with (#260): the globe hangs deep, so the fit leaves its lowest ball
            //only ~4 above the death line, and at a step every 9 landings a 52-shot budget spent ~5.8
            //steps = 3.5 units of descent — the endgame cluster sat INSIDE the band a swing reaches (the 1.0
            //allowance and the 1 s grace), which is how every loss in the owner's dozens of attempts ended:
            //not out of balls, but "the cluster reached the line" off a swing. At 14 the 42-shot budget
            //spends 3 steps = 1.8 units and the cluster stays ~2.2 above the line at budget's end — past the
            //swing band, so a competent run is decided by the shots it lands and not by the pendulum, while
            //the pressure itself stays (three descents still come).
            CeilingStep = 14,
            Occupied = (r, ang, i, depth) => GlobeShell(r, i, depth),
            Colour = GlobeColour,
        };

        #endregion

        #region The arcade levels' second hang (#255)

        //THE ARCADE'S SECOND FIVE. Same statement as the first - a hollow solid with pixel art wrapped onto
        //it, hanging over the neon city - and the same three rules, which are the ones that cost the block
        //its first round: EVERY SOLID IS HOLLOW (the layout is its surface), EVERY CAP CARRIES AT LEAST TWO
        //COLOURS INTERLEAVED (only the field's topmost level is bonded to the glass, so a one-colour anchor
        //is a level that ends on the first lucky ball of it) and THE PIXELS ARE BLOCKS OF CELLS, NEVER SINGLE
        //ONES (a colour region one cell across is a group of one that the repair pass rewrites).
        //
        //WHAT THESE FIVE ADD is a body that is RECOGNISED rather than merely read: a Pac-Man ghost, an arcade
        //cabinet, a Tetris piece, a pyramid and the high-score cup. Four of the five therefore carry a
        //DESIGNATED CUT or a designated collapse - the ghost's four feet arcs, the cabinet's bezel loop, the
        //tetromino's rim weld, the trophy's brown stem - which is the block's own physics-as-character idea
        //taken past decoration: the shot that changes the silhouette is a shot the player can see coming.
        //
        //THE BUDGETS ARE PRICED OFF THE RATIO Validate PRINTS and they continue the first five's ramp
        //(1.65 -> 1.58 -> 1.50 -> 1.44 -> 1.37 as the first five shipped; the sag fixes have since bent
        //that ramp - see the first hang's own comment) rather than restarting it, so the chapter reads as one clock:
        //Ghost is the gentlest of the second hang and Trophy the tightest. Every ratio below is a TARGET, and
        //the group-count dial for each is named in its own doc - GHOST_BLOCK_ARC, CABINET_BLOCK, TETRA_BLOCK,
        //GIZA_COLUMNS, TROPHY_BLOCK_ARC. The first run of the tool settles them; a design that lands outside
        //1.3-1.7 gets its dial moved, not its budget, for CUBE_GROUND_BLOCK's reason.
        //
        //THE COLOURS, and what each level puts the #152 thirteen next to:
        //  Ghost    red and magenta  - the body check, with white eye rings and black pupils embedded in it.
        //  Cabinet  navy, brown, olive - woodgrain, under a magenta/cyan marquee and a five-ink screen.
        //  Tetra    magenta and red  - a hot purple field between white and navy bevel lines.
        //  Giza     white and brown  - sandstone under a yellow/orange gold cap.
        //  Trophy   yellow and orange- gold, over a navy/silver marble foot on one brown stem.

        //THE GHOST. A hollow tube of outer radius GHOST_OUTER and inner GHOST_INNER - a wall two cells thick
        //at either level parity - closing into a nesting dome and a solid cap. Grid 15 leaves two free
        //columns all round at that radius, which is one more than the layout rule asks for and is spent on
        //the hem: the feet swing.
        private const byte GHOST_GRID = 15;
        private const byte GHOST_DEPTH = 14;
        private const float GHOST_OUTER = 5.5f;
        private const float GHOST_INNER = 3.5f;

        //The hem. Two levels of the tube present only in four arcs, so the skirt hangs in four separate
        //lumps and the gaps sit at the four CARDINALS - the notch at +Z is under the eyes, which is the
        //ghost's own front. GHOST_FOOT_HALF is the half-width of an arc as a fraction of its quadrant, and
        //it is the 50-degree arc its own gate sentence held in reserve (25 of 90), taken by #301's sag
        //probe rather than by the unshot test the sentence guessed at: the feet held fine untouched, but
        //late-game releases near the anchor left the remainder dipping 0.06-0.07 past the swing allowance
        //(three orders of five, glass at rest), and the hem is the chain's bottommost dead weight - the
        //cheapest mass to shed. At 60 degrees (1/3) the same orders read 3 of 5 sagged; do not widen it
        //back without re-measuring --sag=Ghost.
        private const int GHOST_FEET_LEVELS = 2;
        private const float GHOST_FOOT_HALF = 25f / 90f;

        //The dome, level by level from GHOST_DOME_FROM up: the wall stays two cells thick and the outer
        //radius steps in about half a cell a level, which is what nests each ring into the pockets of the
        //ring above at the lattice's own 1/sqrt(2) spacing. The last entry is effectively a disc - its inner
        //radius is under the smallest radius any cell of either parity can have - and that is deliberate:
        //a dome that stayed an annulus to the top would bond a RING to the glass and the cap has to be solid.
        private const int GHOST_DOME_FROM = 9;
        private static readonly float[] GHOST_DOME_INNER = { 3.0f, 2.5f, 1.5f, 0.5f };
        private static readonly float[] GHOST_DOME_OUTER = { 5.0f, 4.5f, 3.8f, 3.0f };

        private const int GHOST_CAP_LEVEL = GHOST_DEPTH - 1;
        private const float GHOST_CAP_RADIUS = 2.5f;

        //Where the colour rule changes KIND. The cap and the level under it are discs, so the sector frame's
        //wedges converge on the axis there and a sector block would end in single cells - the reel's heads
        //and the globe's poles found the same wall from two directions. Those two levels take concentric
        //Rings instead, which is two colours interleaved at the glass and one group each.
        private const int GHOST_RING_FROM = 12;

        //The body check. Twenty-four sectors round a wall whose mid radius is about 4.5, i.e. a sector is
        //about 1.2 cells wide. GHOST_BLOCK_ARC was 4 - the spec's "four consecutive cells along the ring" -
        //until the ratio was finally measured on an unfused body (#301), and the whole dial ladder was
        //measured with it: 4 reads 25 standing groups against 56 shots (2.24 a group), 3 reads 29 (1.93),
        //2 reads 34 (1.65) - the only rung inside the block's stated 1.3-1.7 band, and the block's own rule
        //is that this dial moves rather than the budget. The sag probe read the same ladder 2, 2, 1 of 5:
        //finer blocks mean smaller releases and gentler re-hangs, so the pricing rung and the safest rung
        //are the same one. TWELVE blocks go round - even, so the seam does not put a colour against itself,
        //and 12 mod 3 = 0 means the wrap diagonal still fuses exactly one same-colour pair a band, nothing
        //more. The cost, printed by the tool: four balls in pairs and 3 cells recoloured where the dome's
        //wedges converge (Donut ships with 9), against zero at the coarser rungs.
        private const int GHOST_SECTORS = 24;
        private const int GHOST_BLOCK_ARC = 2;
        private const int GHOST_BLOCK_LEVELS = 2;

        //Red, magenta AND black - the spec's three-colour cycle, taken because the two-colour check's risk
        //note on this very line came true and the sag probe is what caught it (#301): a strict two-colour
        //check has its same-colour blocks on the DIAGONAL, and a cross-level neighbour IS a diagonal in
        //(x, z), so the two networks FUSED - single releases of 291 and 299 balls, 45 % of the level, and
        //the remainder stretched past the death line on the first shot in two orders of five. Three colours
        //against blocks stepping one per arc and one per band means no two same-colour blocks touch even
        //diagonally (differences 1 and 2, never 0 mod 3), so a release is one block (GHOST_BLOCK_ARC sets
        //its size) - plus at most its one seam partner, the single same-colour diagonal pair per band the
        //wrap leaves whatever the block count is. Black needs no new colour in the level.
        //⚠ One honest cost, measured on the bitmap rather than assumed away: the pupils are NOT fully ringed
        //by white - each eye's pupil pair sits on its bitmap's +X edge - so one black body block borders the
        //right eye's pupils at their i=8 course whatever the palette order does (every (block+band) residue
        //occurs next to a pupil somewhere; this order makes it exactly one contact). That black block's
        //release takes the pupils with it, which softens the eyes-orphaned-whole joke on that one side and
        //changes nothing structural.
        private static readonly BallType[] GHOST_BODY = { BallType.Type1, BallType.Type6, BallType.Type8 };   //red, magenta, black
        private const BallType GHOST_EYE_WHITE = BallType.Type4;                              //white
        private const BallType GHOST_EYE_PUPIL = BallType.Type8;                              //black

        //The eyes, three columns by four levels each, top row first, on both cells of the wall's thickness.
        //W is the white of the eye and B the pupil; both pupils sit on the +X side of their own eye, so the
        //ghost looks right the way the sprite does. Nothing here is one cell across in either direction.
        private static readonly string[] GHOST_EYE =
        {
            "WWW",
            "WBB",
            "WBB",
            "WWW",
        };

        private const int GHOST_EYE_TOP = 9;
        private const int GHOST_EYE_BOTTOM = 6;

        //Where an eye's three columns start and end, measured along dx. Neither figure can land ON a cell of
        //either level parity - the unshifted levels put dx on a half cell and the shifted ones on a whole one
        //- which is PolarBlock's quarter-cell rule arriving on a bitmap: an edge exactly on a row of cells
        //lets the float noise in r*cos(ang) decide which column a ball is in, and the eye would fray by
        //parity. At 0.6 and 3.6 both parities give exactly three columns and the eyes keep at least one body
        //column between them.
        private const float GHOST_EYE_NEAR = 0.6f;
        private const float GHOST_EYE_FAR = 3.6f;

        /// <summary>
        /// A Pac-Man ghost the size of a house: a closed two-cell tube of a body over four arcs of hem,
        /// closing into a nesting dome and a solid cap, with the eyes painted through both layers of the wall
        /// on its front. The block's opener for the same reason <see cref="Cube"/> was the first hang's - it
        /// is the most recognisable arcade silhouette after Pac-Man himself and it is built on the safest
        /// construction in the five.
        /// <para>
        /// <b>Every level of it is a closed loop, and that is the second load path.</b> A closed tube is
        /// self-bracing where an open curtain is not (<see cref="ZIGGURAT_WALL"/> is what that cost when it
        /// was learned), so nothing here hangs off a strand: the body is a ring, the dome is a ring, and the
        /// cap is solid. What hangs off anything is the hem, deliberately - see below.
        /// </para>
        /// <para>
        /// <b>The physics IS the character animation.</b> The four feet are dead weight in four separate
        /// arcs at <see cref="GHOST_FOOT_HALF"/>, so shooting one out leaves the skirt asymmetric and the
        /// ghost's wavy hem literally waves; clear a tall seam of the body and the C-shaped tube springs open
        /// and shivers. It is the one hollow solid in the game whose bottom edge is SUPPOSED to wobble.
        /// </para>
        /// <para>
        /// <b>The eyes are colour, not geometry, and they are meant to be orphaned.</b> Each is an island of
        /// white and black embedded in the body wall, so popping the wall around one drops the eye
        /// whole - about two dozen balls, nowhere near the drop test's line, and the level's best small joke.
        /// (One asterisk since the body took its third colour: a single black body block borders the right
        /// eye's pupils, so that one release takes the pupils along - see <see cref="GHOST_BODY"/>.)
        /// </para>
        /// <para>
        /// Measured (#301, after the three-colour body, the 50-degree hem and the two-sector check):
        /// 661 balls in 34 standing groups, 1.65 shots a group against the budget of 56 - in the block's
        /// 1.3-1.7 band - margin 2, nothing alone, 4 in pairs, 3 recoloured; best single shots 3-12 %.
        /// Both of the checks this doc used to ask for have been made and both came back guilty: the
        /// red/magenta percolation was real (single releases of 45 % of the level; the sag probe read
        /// 4 of 5 orders lost, two on their first shot) and the hem's weight was the margin the late-game
        /// re-hangs were missing. The probe reads the shipped shape 1 of 5 across repeated sweeps (the dip
        /// depth wobbles run to run, the count does not) - better than Saturn's 2 of 5, a level the owner
        /// finishes comfortably.
        /// </para>
        /// </summary>
        private static Design Ghost() => new()
        {
            File = "Ghost.json",
            Name = "Ghost",
            Grid = GHOST_GRID,
            Depth = GHOST_DEPTH,
            FieldLevels = ARCADE_FIELD,
            Scene = SceneKind.NeonCity,
            Sky = ARCADE_SKY,
            Music = MUSIC_ARCADE,
                Balls = BALLS_ARCADE,
            Shots = 56,
            //#288: 9 until the owner reported this level losing to the line rather than to the budget. Ghost
            //has the block's SMALLEST clearance - GHOST_DEPTH 14 in ARCADE_FIELD 18 leaves four empty levels,
            //measured 3.96 units over the line - and at 9 the budget bought six descents, 3.60 of that 3.96.
            //It ended the level 0.36 above the line, where the swing PROBE ON CHEST measured dips of up to
            //0.82 (see GameplayScreen.CLUSTER_SWING_ALLOWANCE): an ordinary swing was under the line, and the
            //hold rule then decided the level. Fourteen buys four descents, 2.40, and leaves 1.56 - clear of
            //the measured swing and of the allowance both. The field is NOT the lever here and that is
            //deliberate: this block is framed whole at 18 on purpose (see the block's note).
            CeilingStep = 14,
            Occupied = (r, ang, i, depth) => GhostShell(r, ang, i),
            Colour = GhostColour,
        };

        private static bool GhostShell(float r, float ang, int i)
        {
            if (i >= GHOST_CAP_LEVEL) return r <= GHOST_CAP_RADIUS;

            if (i >= GHOST_DOME_FROM)
            {
                int course = i - GHOST_DOME_FROM;
                return r >= GHOST_DOME_INNER[course] && r <= GHOST_DOME_OUTER[course];
            }

            if (r < GHOST_INNER || r > GHOST_OUTER) return false;

            //The hem is the body's own annulus kept only in four arcs, so a foot is two cells thick like
            //everything above it and hangs off the ring it was cut out of
            return i >= GHOST_FEET_LEVELS || GhostFootArc(ang);
        }

        /// <summary>
        /// Whether an angle is inside one of the four feet. Measured in QUARTER turns from +X, so a foot sits
        /// on each diagonal and the four gaps sit on the cardinals - which puts the notch the player reads as
        /// the ghost's front gap directly under the eyes, on +Z.
        /// </summary>
        private static bool GhostFootArc(float ang)
        {
            //Lifted a whole turn before the fraction is taken: ang is atan2's, so it goes negative, and a
            //bare % of a negative would fold two of the four arcs onto the wrong side of their quadrant
            float quarter = (ang / MathF.Tau + 1f) * 4f % 1f;

            return MathF.Abs(quarter - HALF) <= GHOST_FOOT_HALF;
        }

        private static BallType GhostColour(float r, float ang, int i, int depth)
        {
            //The eyes: a recolour of BOTH cells of the wall's thickness on the +Z half, so the drawing is
            //still there when the outer skin has gone and every stroke is twice the group
            if (i >= GHOST_EYE_BOTTOM && i <= GHOST_EYE_TOP && r * MathF.Sin(ang) > 0f)
            {
                int column = GhostEyeColumn(r * MathF.Cos(ang));

                if (column >= 0)
                {
                    char ink = PixelAt(GHOST_EYE, column, GHOST_EYE_TOP - i);

                    if (ink == 'W') return GHOST_EYE_WHITE;
                    if (ink == 'B') return GHOST_EYE_PUPIL;
                }
            }

            //The cap and the level under it are discs - see GHOST_RING_FROM
            if (i >= GHOST_RING_FROM) return Ring(r, GHOST_BODY);

            return Band(SectorIndex(ang, 0f, GHOST_SECTORS) / GHOST_BLOCK_ARC
                        + (depth - 1 - i) / GHOST_BLOCK_LEVELS, GHOST_BODY);
        }

        /// <summary>
        /// Which of an eye's three columns a cell is in, or -1 for neither eye. Both eyes read their bitmap
        /// LEFT to RIGHT in +X, which is what makes one three-column bitmap serve both and both pupils look
        /// the same way; the left eye's window is therefore measured from its far edge.
        /// </summary>
        private static int GhostEyeColumn(float dx)
        {
            if (dx > GHOST_EYE_NEAR && dx < GHOST_EYE_FAR) return (int)MathF.Floor(dx - GHOST_EYE_NEAR);
            if (dx < -GHOST_EYE_NEAR && dx > -GHOST_EYE_FAR) return (int)MathF.Floor(dx + GHOST_EYE_FAR);

            return -1;
        }

        //THE CABINET. A closed box nine cells across, seven deep and twelve levels tall, with a slab of
        //control panel hanging off its front. The shell is one cell thick everywhere EXCEPT the front
        //wall, which is two: the front carries the picture, and PICTURE_THICKNESS's rule is the same here as
        //it is on the Gallery's flat walls - a wall one cell thick in the picture's own depth has half its
        //cross-level neighbours reaching to a cell that is not there.
        //
        //⚠ TWELVE LEVELS, NOT FOURTEEN, and it is #301's structural finding: the block's two reported-
        //unfinishable members were exactly its two fourteen-deep layouts (this and Ghost), while Cube at ten
        //and Giza at eleven read clean in the sag probe. A one-cell wall is a chain of BallSocket links from
        //the glass to the plate, every link yields a little under sustained load, and fourteen links starting
        //3.96 over the death line drooped past it mid-game once releases had opened the walls up - the probe's
        //traces bottom out at the lower walls and plate. Two levels shorter is two links less yield, ~56
        //balls less wall mass on the chains, and 5.37 of clearance instead of 3.96.
        private const byte CABINET_GRID = 13;
        private const byte CABINET_DEPTH = 12;
        private const int CABINET_X0 = 2;
        private const int CABINET_X1 = 10;
        private const int CABINET_BACK = 3;
        private const int CABINET_FRONT_INNER = 8;
        private const int CABINET_FRONT = 9;
        private const int CABINET_TOP = CABINET_DEPTH - 1;

        //The control panel: a slab protruding TOWARD the gun, hanging off the front wall. It is the one thing
        //in the level that overhangs, so it is the one thing to put through AimReachability - if cells behind
        //it shadow, the fix is z of CABINET_PANEL_Z0 alone (one cell deep) before the slab is moved anywhere.
        //It also sets the level's lateral margin: z reaches CABINET_PANEL_Z1 in a grid of 13, which leaves
        //exactly the one free column the field's edge needs.
        private const int CABINET_PANEL_X0 = 3;
        private const int CABINET_PANEL_X1 = 9;
        private const int CABINET_PANEL_Z0 = 10;
        private const int CABINET_PANEL_Z1 = 11;
        private const int CABINET_PANEL_LOW = 3;
        private const int CABINET_PANEL_HIGH = 4;

        //The lit marquee band: the top two levels of all four walls AND the whole top face. The top face is
        //the anchor, so this palette is the one that has to carry two colours interleaved. Stated off the
        //top so the #301 depth change could not strand it.
        private const int CABINET_MARQUEE_FROM = CABINET_TOP - 1;

        //The CRT shelf (#301): an internal plate on the screen's own bottom course, tying all four walls at
        //mid-height. The level sits where the bitmap puts the screen's bottom row (CABINET_SCREEN_TOP minus
        //the bitmap's four lower rows), so the shelf reads as the thing the monitor stands on when a broken
        //wall opens a window onto it. (A mid-bay spine partition - Tetra's bay-seam answer - was tried on
        //top of it for the back wall's nine-cell span and measured NO gain on the probe for 36 balls of
        //cost, so the shelf stands alone.)
        private const int CABINET_SHELF_LEVEL = CABINET_SCREEN_TOP - 4;

        //How coarse the woodgrain and the marquee dither is, in cells, and the design's group-count dial.
        //Four rather than the spec's two, and the reason is CUBE_GROUND_BLOCK's finding measured on a
        //one-cell shell: a 2x2x2 block on a wall one cell thick is FOUR balls, not eight, and four-ball
        //blocks over four hundred body cells is a hundred standing groups against a budget of sixty - a
        //level that cannot be finished at all. At four the same body is blocks of sixteen. This is the knob
        //to turn if the first run's ratio lands outside the block's 1.3-1.7 band, in either direction.
        private const int CABINET_BLOCK = 4;

        //The cabinet body: navy, brown and olive woodgrain, and BOTH figures of that are #301's findings.
        //⚠ NO WOOD ENTRY MAY BE BLACK (Type8) - a structural rule rather than a palette one: black is the
        //bitmaps' ink (the screen bezel, the cherry art's outline), and a black wood block welds them and every
        //other black block into ONE network. Shipped that way, black measured 239 balls in a single standing
        //group - 46% of the level, the sag probe's own 240-ball release - and the 307-ball remainder
        //stretched past the death line with the glass at rest. The set piece needs the bezel's ink to itself.
        //⚠ AND THE BAND NEEDS THREE ENTRIES, NOT TWO: cross-level neighbours are diagonal in (x, z), so at a
        //block corner the band index steps by 0 or ±2 - both the same colour out of a two-entry band - and
        //the two-ink body welded itself into per-colour networks of 120-240 balls this way (a two-colour
        //check's diagonal, CUBE's "no ground colour meets its own kind" finding arriving through the walls).
        //Three entries leave only the rare Δ=0 corner, and the largest wood group measures 32.
        private static readonly BallType[] CABINET_WOOD = { BallType.Type12, BallType.Type10, BallType.Type13 };
        private static readonly BallType[] CABINET_MARQUEE = { BallType.Type6, BallType.Type5 };   //the marquee, in neon: magenta and cyan
        private static readonly BallType[] CABINET_PANEL = { BallType.Type11, BallType.Type1 };     //the control panel: brushed silver with a red button run

        /// <summary>
        /// The screen, seven wide by five tall, top row first, drawn on BOTH cells of the front wall's
        /// thickness. <c>k</c> is the black bezel, <c>M</c> and <c>C</c> the two brick rows, <c>W</c> the
        /// ball and <c>G</c> the paddle. Every ink is at least two cells across in its own row and is
        /// therefore at least four balls through the wall; no sprite reaches the bezel's side columns, so the
        /// bezel is one connected piece and the sprites inside it are islands hanging off it.
        /// </summary>
        private static readonly string[] CABINET_SCREEN =
        {
            "kMMMMMk",
            "kCCCCCk",
            "kkWWkkk",
            "kkkkkkk",
            "kkGGGkk",
        };

        private const int CABINET_SCREEN_COLUMN = 3;
        private const int CABINET_SCREEN_TOP = 9;

        /// <summary>
        /// Classic cabinet art, two cherries on a stem: seven columns of x by six levels, top row first, on
        /// the BACK wall. <b>The black outline is load-bearing twice.</b> It keeps the cherries' red fenced
        /// off the woodgrain so the drawing stays its own groups, the wall here being one cell thick with no
        /// second layer to double anything; and black is the bezel's ink, so the outline must never reach the
        /// screen - see below.
        /// <para>
        /// <b>The back wall, not the flank, and #301's depth change is why.</b> At fourteen levels the art
        /// sat on the -X wall one level below the screen and their blacks stayed apart; at twelve the screen
        /// came two levels down and the art's stem fence at (2, 7) reached the bezel's bottom corner at
        /// (3, 8) through the odd field level's cross-level diagonal (<c>x..x+1, z..z+1</c>) - the two welded
        /// into one 47-ball network, and a flank shot could smash the screen. Dropping the art to top out at
        /// 5 dodges the weld by parity but lands its lone top pixel where the repair pass rewrites it
        /// (measured: 1 recoloured - the drawing quietly rewritten, the exact thing the block's bitmap rule
        /// exists to forbid). The back wall is nine cells wide, so the bitmap fits with a spare column each
        /// side and sits a full box away from the bezel, 0 recoloured; the gun orbits the level, so the back
        /// reads as well as the flank ever did. Black measures as two separate networks, the bezel and this.
        /// </para>
        /// </summary>
        private static readonly string[] CABINET_CHERRY =
        {
            "...k...",
            "..kGk..",
            "..kGk..",
            "kkkGkkk",
            "kRRkRRk",
            "kRRkRRk",
        };

        private const int CABINET_ART_COLUMN = 3;
        private const int CABINET_ART_TOP = 6;

        /// <summary>
        /// An arcade cabinet inside the arcade: a closed woodgrain box under a lit marquee, with a Breakout
        /// game on its screen, a control panel hanging off its front and cherry art on its back panel. The
        /// block's register made literal - the cabinet is the level, and the one game you can play on it is
        /// the shot that destroys it.
        /// <para>
        /// <b>Structurally it believed itself the dullest thing in the five, and #301 is the bill for
        /// that.</b> Six faces of a closed box do mutually brace - as a rigid body. What the sentence missed
        /// is that a one-cell wall is not rigid: it is a sheet of BallSocket chains, every link yields a
        /// little under sustained load, and fourteen levels of it starting 3.96 over the death line drooped
        /// past the line mid-game once releases had opened the walls up. The box is twelve levels now (see
        /// <see cref="CABINET_DEPTH"/>), braced by the CRT shelf (see <see cref="CabinetShell"/>), and paced
        /// a step slower (see the CeilingStep note below) - the same shape, read honestly.
        /// </para>
        /// <para>
        /// <b>Smashing the screen is the set piece.</b> The bezel is one connected black loop round the
        /// sprites, and every sprite inside it hangs off that loop alone: one landed black ball releases the
        /// lot and the whole Breakout scene rains out of the cabinet in one shower, which is how a CRT dies.
        /// Measured, it is the level's biggest single shot - 40 bezel balls plus the sprites they orphan,
        /// about an eighth of the cluster - priced and deliberate, and far under the drop test's line. ⚠ It
        /// was 46% when the woodgrain carried black too, which is #301's whole story: see
        /// <see cref="CABINET_WOOD"/> before touching any ink here.
        /// </para>
        /// <para>
        /// <b>The marquee is the anchor and it carries two colours by construction.</b> The top face is the
        /// only level bonded to the glass; magenta and cyan alternate across it in blocks of
        /// <see cref="CABINET_BLOCK"/>, so clearing either leaves the other still holding four full walls.
        /// </para>
        /// <para>
        /// Measured: 532 balls in 45 standing groups (1.33 shots a group, inside the block's 1.3-1.7 band),
        /// anchor load 12.0, margin 1, nothing alone, 0 recoloured; every colour's best single shot 7% or
        /// less, the bezel's the biggest. Sag probe: 3 losing orders of 5 - Amphora's own reading, and the
        /// three traces are late-game wall-flap dips of 1.0-1.1 at the back wall's lower courses, the same
        /// kind the known-finishable levels show. <b>Check first</b>: the sag probe on any layout or ink
        /// change (no wood entry may be black, and the wood band needs its three entries - both findings
        /// carry their arithmetic on <see cref="CABINET_WOOD"/>). <b>Then</b>: AimReachability on the panel
        /// slab, and the black network's own drop test with the art's border counted in.
        /// </para>
        /// </summary>
        private static Design Cabinet() => new()
        {
            File = "Cabinet.json",
            Name = "Cabinet",
            Grid = CABINET_GRID,
            Depth = CABINET_DEPTH,
            FieldLevels = ARCADE_FIELD,
            Scene = SceneKind.NeonCity,
            Sky = ARCADE_SKY,
            Music = MUSIC_ARCADE,
                Balls = BALLS_ARCADE,
            Shots = 60,
            //#301: 12 until the sag probe's traces showed the remainder under the line late-game with the
            //glass stepped - and 12 was under the block's own #288 arithmetic all along: clearance 3.96 less
            //five descents (60/12) of 0.60 is 0.96 of final headroom, under the 1.00 swing allowance the
            //header says every level must clear. Sixteen, not fourteen: at fourteen the probe's remaining
            //losing orders all dipped -1.00 to -1.07 late-game with the glass two or three steps down -
            //within one step's 0.60 of surviving, every one of them - and sixteen is that step handed back
            //(three descents, 1.80 consumed, 3.57 of headroom against the 12-deep box's 5.37 clearance).
            //It also keeps this the block's slowest clock, which its own summary promises.
            CeilingStep = 16,
            OccupiedBlock = (x, z, i, depth) => CabinetShell(x, z, i),
            BlockColour = CabinetColour,
        };

        private static bool CabinetShell(int x, int z, int i)
        {
            if (InCabinetPanel(x, z, i)) return true;

            if (x < CABINET_X0 || x > CABINET_X1 || z < CABINET_BACK || z > CABINET_FRONT) return false;

            //Closed box: both plates, both sides, the back - and the front twice over, because the front is
            //where the picture is. Plus the CRT shelf, #301's one addition: a run of one-cell wall was the
            //tallest unbraced span in the block, and the probe's traces showed the lower walls, the bottom
            //plate and the panel slab drooping past the death line mid-game once releases had opened the
            //walls up. The shelf ties all four walls at the screen's own bottom course - where a cabinet
            //really keeps its monitor shelf - and is the same answer Tetra's bay partitions already gave:
            //a long thin span is braced by a plate on its seam, not by hoping. (A two-course pedestal base
            //was tried for the same traces and measured COUNTERPRODUCTIVE - 28 balls added at the bottom of
            //the longest chains moved one losing order eight shots earlier - so the plate stays one course.)
            return i == 0 || i == CABINET_TOP || i == CABINET_SHELF_LEVEL
                   || x == CABINET_X0 || x == CABINET_X1
                   || z == CABINET_BACK || z >= CABINET_FRONT_INNER;
        }

        private static bool InCabinetPanel(int x, int z, int i) =>
            x >= CABINET_PANEL_X0 && x <= CABINET_PANEL_X1
            && z >= CABINET_PANEL_Z0 && z <= CABINET_PANEL_Z1
            && i >= CABINET_PANEL_LOW && i <= CABINET_PANEL_HIGH;

        private static BallType CabinetColour(int x, int z, int i)
        {
            //The panel is banded on x and on the LEVEL rather than on a block of both, so its two levels
            //cannot come out the same colour: a 28-ball ledge in one piece is a ledge that drops whole
            if (InCabinetPanel(x, z, i)) return Band(x / CABINET_BLOCK + i, CABINET_PANEL);

            if (i >= CABINET_MARQUEE_FROM) return Band(x / CABINET_BLOCK + z / CABINET_BLOCK, CABINET_MARQUEE);

            if (z >= CABINET_FRONT_INNER)
            {
                char ink = PixelAt(CABINET_SCREEN, x - CABINET_SCREEN_COLUMN, CABINET_SCREEN_TOP - i);
                if (ink != '.') return CabinetInk(ink);
            }

            if (z == CABINET_BACK)
            {
                char ink = PixelAt(CABINET_CHERRY, x - CABINET_ART_COLUMN, CABINET_ART_TOP - i);
                if (ink != '.') return CabinetInk(ink);
            }

            return Band(x / CABINET_BLOCK + z / CABINET_BLOCK + i / CABINET_BLOCK, CABINET_WOOD);
        }

        /// <summary>What each character of the two bitmaps is painted in. One table for both, so the side
        /// art's outline and the screen's bezel cannot drift apart into two blacks.</summary>
        private static BallType CabinetInk(char ink) => ink switch
        {
            'M' => BallType.Type6,   //magenta, the upper brick row
            'C' => BallType.Type5,   //cyan, the lower brick row
            'W' => BallType.Type4,   //white, the ball
            'G' => BallType.Type2,   //green, the paddle and the cherry stem
            'R' => BallType.Type1,   //red, the cherries
            _ => BallType.Type8,     //black, the bezel and the back art's outline
        };

        //THE TETROMINO. Two hollow boxes welded into one T: a bar fifteen cells across, five deep and seven
        //levels tall - three five-cell bays, and seven levels of 1/sqrt(2) is 4.95 units, so a bay reads as a
        //true cube - and a stem of the same cube hanging under its middle. Both shells are one cell thick and
        //closed, which is what braces them; the bar is stiffened further by two internal partitions on the
        //bay seams, so the fifteen-wide span is three short cells rather than one long one.
        private const byte TETRA_GRID = 17;
        private const byte TETRA_DEPTH = 14;
        private const int TETRA_BAR_X0 = 1;
        private const int TETRA_BAR_X1 = 15;
        private const int TETRA_STEM_X0 = 6;
        private const int TETRA_STEM_X1 = 10;
        private const int TETRA_Z0 = 6;
        private const int TETRA_Z1 = 10;
        private const int TETRA_BAR_FLOOR = 7;
        private const int TETRA_TOP = TETRA_DEPTH - 1;

        //The window in the bar's floor, over the stem: three by three, so a player can see down into the
        //hanging cube. It is cut out of the FLOOR PLATE and not out of the weld - the stem's own top ring at
        //TETRA_BAR_FLOOR - 1 sits under the plate all the way round the window, sixteen cells of it, and that
        //ring is the rim weld the design's physics is about.
        private const int TETRA_WINDOW_X0 = 7;
        private const int TETRA_WINDOW_X1 = 9;
        private const int TETRA_WINDOW_Z0 = 7;
        private const int TETRA_WINDOW_Z1 = 9;

        private const int TETRA_PARTITION_LEFT = 5;
        private const int TETRA_PARTITION_RIGHT = 11;

        //How coarse the field check is, in face cells, and this design's group-count dial. Two, as specced,
        //puts about eighty standing groups against a budget of fifty-eight - CUBE_GROUND_BLOCK's arithmetic
        //again, and worse here because every face is fenced in by its own bevel lines and so cannot merge
        //with its neighbours. At four the field comes out near forty blocks. Move THIS if the ratio misses.
        private const int TETRA_BLOCK = 4;

        private static readonly BallType[] TETRA_FIELD = { BallType.Type6, BallType.Type1 };   //magenta, red
        private const BallType TETRA_HIGHLIGHT = BallType.Type4;                               //white
        private const BallType TETRA_SHADOW = BallType.Type12;                                 //navy

        /// <summary>
        /// One giant hollow T-tetromino in classic bevel-shaded pixel purple: a Tetris piece that fell into
        /// the wrong game and has to be cleared by Puzzle-Bobble rules. Genre collision as a level, and the
        /// 90s bevel every kid knows painted in balls.
        /// <para>
        /// <b>The bevel does not run across a wall, and that is not a drawing decision.</b> Drawn the way a
        /// sprite is - a highlight along each face's top border - the top ROW of every wall came out one
        /// colour, and a wall's top row is the only thing the run below it hangs from: the drop test made it
        /// a one-shot level outright. So the highlight lives on the faces' vertical borders and on the
        /// plates' own edges, where a line can be cleared without severing anything, and the walls' top rows
        /// stay field check. It still reads as a bevel because the plates carry theirs.
        /// </para>
        /// <para>
        /// <b>The rim weld is the earned swing.</b> The stem hangs from the ring of its own top level, welded
        /// all the way round to the bar's floor plate - sixteen cells. Chew that ring away along the front and
        /// one side and the remaining L carries the whole cube: it heels over and springs on every subsequent
        /// hit until the last of the rim goes and the cube drops as one piece.
        /// </para>
        /// <para>
        /// <b>Four colours stand at the glass.</b> The bar's top plate carries the magenta/red field, the
        /// white of its two upper borders and the navy of its two lower ones, so no single colour is the
        /// anchor. Navy is the level's biggest network - both bevels plus the two internal partitions - and
        /// it orphans face interiors only, everything else being held by a perimeter.
        /// </para>
        /// <para>
        /// Priced at 58 shots against a target near 40 standing groups (1.45 a group). <b>Check first</b>:
        /// magenta/red percolation over the one continuous exterior shell - bar and stem are a single surface
        /// - whose tuned fallback is the spec's own, promoting every third field block to yellow(7) AND
        /// swapping <see cref="TETRA_HIGHLIGHT"/> to cyan(5) in the same edit, white and yellow being a
        /// confusable pair that must not end up adjacent. <b>Then</b>: the navy drop test, and the stem's rim
        /// weld measured after discretisation rather than off the formula.
        /// </para>
        /// </summary>
        private static Design Tetra() => new()
        {
            File = "Tetra.json",
            Name = "Tetra",
            Grid = TETRA_GRID,
            Depth = TETRA_DEPTH,
            FieldLevels = ARCADE_FIELD,
            Scene = SceneKind.NeonCity,
            Sky = ARCADE_SKY,
            Music = MUSIC_ARCADE,
                Balls = BALLS_ARCADE,
            Shots = 58,
            CeilingStep = 10,
            OccupiedBlock = (x, z, i, depth) => TetraShell(x, z, i),
            BlockColour = TetraColour,
        };

        private static bool TetraShell(int x, int z, int i)
        {
            if (TetraPartition(x, z, i)) return true;

            if (z < TETRA_Z0 || z > TETRA_Z1) return false;

            if (i >= TETRA_BAR_FLOOR)
            {
                if (x < TETRA_BAR_X0 || x > TETRA_BAR_X1) return false;

                if (i == TETRA_BAR_FLOOR)
                    return x < TETRA_WINDOW_X0 || x > TETRA_WINDOW_X1
                           || z < TETRA_WINDOW_Z0 || z > TETRA_WINDOW_Z1;

                return i == TETRA_TOP || x == TETRA_BAR_X0 || x == TETRA_BAR_X1
                       || z == TETRA_Z0 || z == TETRA_Z1;
            }

            if (x < TETRA_STEM_X0 || x > TETRA_STEM_X1) return false;

            //Every face of the stem but its top one, which is the bar's floor plate: the two boxes share
            //that surface rather than each having one, so the stem hangs from a four-sided weld
            return i == 0 || x == TETRA_STEM_X0 || x == TETRA_STEM_X1
                   || z == TETRA_Z0 || z == TETRA_Z1;
        }

        private static bool TetraPartition(int x, int z, int i) =>
            (x == TETRA_PARTITION_LEFT || x == TETRA_PARTITION_RIGHT)
            && z > TETRA_Z0 && z < TETRA_Z1
            && i > TETRA_BAR_FLOOR && i < TETRA_TOP;

        private static BallType TetraColour(int x, int z, int i)
        {
            if (TetraPartition(x, z, i)) return TETRA_SHADOW;

            bool plate = TetraFace(x, z, i, out int u, out int v, out int uLast, out int vLast);

            //A plate is bevelled on all four of its edges, a wall only on its two vertical ones - see the
            //design's own remarks for the row that took the level in one shot
            if (u == 0 || (plate && v == 0)) return TETRA_HIGHLIGHT;
            if (u == uLast || (plate && v == vLast)) return TETRA_SHADOW;

            return Band(u / TETRA_BLOCK + v / TETRA_BLOCK, TETRA_FIELD);
        }

        /// <summary>
        /// Which face a cell is on, as the face's own <c>(u, v)</c> and their last indices: <c>u</c> reads
        /// left to right and <c>v</c> reads TOP DOWN on every one of them, which is what lets a single bevel
        /// rule serve all eleven. Returns whether the face is a horizontal plate, the two being bevelled
        /// differently.
        /// </summary>
        private static bool TetraFace(int x, int z, int i, out int u, out int v, out int uLast, out int vLast)
        {
            //The three plates first, so a plate's own border wins over the wall it meets there
            if (i == TETRA_TOP || i == TETRA_BAR_FLOOR)
            {
                u = x - TETRA_BAR_X0; uLast = TETRA_BAR_X1 - TETRA_BAR_X0;
                v = z - TETRA_Z0; vLast = TETRA_Z1 - TETRA_Z0;
                return true;
            }

            if (i == 0)
            {
                u = x - TETRA_STEM_X0; uLast = TETRA_STEM_X1 - TETRA_STEM_X0;
                v = z - TETRA_Z0; vLast = TETRA_Z1 - TETRA_Z0;
                return true;
            }

            bool bar = i > TETRA_BAR_FLOOR;
            int x0 = bar ? TETRA_BAR_X0 : TETRA_STEM_X0;
            int x1 = bar ? TETRA_BAR_X1 : TETRA_STEM_X1;
            int top = bar ? TETRA_TOP - 1 : TETRA_BAR_FLOOR - 1;
            int bottom = bar ? TETRA_BAR_FLOOR + 1 : 1;

            v = top - i; vLast = top - bottom;

            //The two X walls claim the corner columns, so the Z walls run between them and every bevel line
            //belongs to exactly one face
            if (x == x0 || x == x1)
            {
                u = z - TETRA_Z0; uLast = TETRA_Z1 - TETRA_Z0;
                return false;
            }

            u = x - x0; uLast = x1 - x0;
            return false;
        }

        //THE PYRAMID. Eleven courses, each a square one cell smaller on a side than the one below it: side
        //13 - i, trimmed from alternating sides (GizaLow steps on even i, GizaHigh on odd) so the course's
        //PHYSICAL centre stays at 7.0 on every level once the shifted levels' half cell is counted. That is
        //the whole packing showcase - a side that shrinks by exactly one drops every course into the pockets
        //of the one above at the lattice's own 1/sqrt(2) spacing, so all four faces read as tetrahedral
        //packing planes when the gun walks round. One is point-down and solid, Ziggurat is terraced; this is
        //smooth, point-up-truncated and hollow.
        private const byte GIZA_GRID = 15;
        private const byte GIZA_DEPTH = 11;

        //Fifteen and NOT ARCADE_FIELD, which is the one place this design steps out of the block, and it is
        //arithmetic rather than taste: Emit refuses an odd field-less-layout offset, GIZA_DEPTH is odd, so
        //the field has to be odd too. Fifteen leaves the layout FOUR empty levels of clearance underneath -
        //exactly what the block's 18-over-14 leaves - and it is well under GameplayScreen.FRAMED_LEVELS, so
        //the monument is still framed whole. If the capstone ever has to be truncated a level earlier (see
        //the doc below), this goes to 14 with GIZA_DEPTH at 10 to keep the parity.
        private const byte GIZA_FIELD = 15;

        private const int GIZA_BASE_LOW = 1;
        private const int GIZA_BASE_HIGH = 13;
        private const int GIZA_WALL = 2;

        //Where the hollow courses stop and the gold capstone mass begins. THREE solid levels rather than a
        //point, and a 3x3 top rather than a 2x2, because about six hundred balls funnel into whatever the
        //top level is: nine glass bonds is the design's whole structural bet, and the unshot sag test is
        //what settles it. If it stretches past the line, truncate one level earlier - GIZA_DEPTH 10 and
        //GIZA_FIELD 14 - which tops it at 4x4 and sixteen bonds.
        private const int GIZA_SOLID_FROM = 8;

        //The hidden burial shaft: a nested column straight up the middle from the base into the capstone,
        //3x3 on the levels whose cells sit ON the axis and 2x2 on the levels standing half a cell off it.
        //It is nine-on-four cannonball nesting, and it is a real second load path - if a whole face of
        //courses is chewed away the shaft still carries the base. The parity test is the LAYOUT level's
        //because Emit's offset is even by construction, so layout parity and field parity are the same.
        private const int GIZA_SHAFT_LOW = 6;
        private const int GIZA_SHAFT_HIGH_EVEN = 8;
        private const int GIZA_SHAFT_HIGH_ODD = 7;

        //How many blocks of colour go round a course, as a FRACTION of its perimeter rather than as a cell
        //count - so a block is the same wedge of the monument on the thirteen-wide base as on the six-wide
        //course near the top, and the check runs straight down the building instead of shearing round it.
        //Eight is even, which is what keeps the seam honest: an odd count round a closed ring puts the same
        //colour on both sides of it. It is also this design's group-count dial.
        private const int GIZA_COLUMNS = 8;

        //Sandstone and gold. The sand pair carries the flagged percolation risk every two-colour check in
        //this block carries; the fallback is a third entry of BallType.Type8 (black) and NOT silver, white
        //and silver being a confusable pair.
        private static readonly BallType[] GIZA_SAND = { BallType.Type4, BallType.Type10 };   //white, brown
        private static readonly BallType[] GIZA_GOLD = { BallType.Type7, BallType.Type9 };    //yellow, orange
        private const BallType GIZA_EYE_RING = BallType.Type5;                                //cyan
        private const BallType GIZA_PUPIL = BallType.Type12;                                  //navy
        private const BallType GIZA_DOOR = BallType.Type12;                                   //navy

        /// <summary>
        /// The eye on the +Z face, five columns by three levels, top row first. It is a CLOSED cyan ring
        /// where the spec drew an open one, and the lattice is why: a cell's cross-level neighbours run to
        /// <c>x-1..x</c> from an unshifted level and to <c>x..x+1</c> from a shifted one, never both, so the
        /// open drawing's right-hand middle cell touched nothing but its own layer partner and stood as a
        /// pair. Closed, every cell of the ring reaches the row above or below it.
        /// </summary>
        private static readonly string[] GIZA_EYE =
        {
            "CCCCC",
            "CBBBC",
            "CCCCC",
        };

        private const int GIZA_EYE_COLUMN = 5;
        private const int GIZA_EYE_TOP = 6;
        private const int GIZA_DOOR_TOP = 2;
        private const int GIZA_DOOR_X0 = 6;
        private const int GIZA_DOOR_X1 = 8;

        /// <summary>
        /// A smooth-faced golden-capped pyramid with a door, an eye and a hidden shaft, hanging from its own
        /// capstone. The brief's dare taken literally: the packing rule that built the opener <see cref="One"/>
        /// resurfaced as a monument you walk around.
        /// <para>
        /// <b>Every course nests into the one above it, visibly.</b> The side shrinks by exactly one per
        /// level and the trim alternates sides, so the physical centre never moves and each course drops into
        /// the pockets of its neighbour - see the region's own remarks. It is the showcase the block was
        /// asked for and it is distinct from all three of its rivals in the pack.
        /// </para>
        /// <para>
        /// <b>Six hundred balls hang from nine glass bonds.</b> That is the design's bet and the first thing
        /// to measure: any big release makes the whole monument sway as one rigid mass on its apex, a
        /// pendulum the size of the screen, and shooting the door open shows the shaft swinging inside it.
        /// The apex is truncated at 3x3 and the cap is three solid levels precisely so that bet is payable;
        /// <see cref="GIZA_SOLID_FROM"/> carries the fallback if it is not.
        /// </para>
        /// <para>
        /// <b>The anchor is gold and only gold, by design.</b> The capstone IS the separation between the two
        /// palettes: yellow and orange interleave in 2x2x2 blocks right onto the top level, so clearing the
        /// largest yellow leaves the orange half still bonded, with the shell and the shaft both still
        /// carried - two independent paths down to the base.
        /// </para>
        /// <para>
        /// Priced at 56 shots against a target near 40 standing groups (1.4 a group). <b>Check first</b>: the
        /// unshot sag test on the capstone. <b>Then</b>: white/brown percolation on the hollow shell, and the
        /// gold cap's yellow drop test.
        /// </para>
        /// </summary>
        private static Design Giza() => new()
        {
            File = "Giza.json",
            Name = "Giza",
            Grid = GIZA_GRID,
            Depth = GIZA_DEPTH,
            FieldLevels = GIZA_FIELD,
            Scene = SceneKind.NeonCity,
            Sky = ARCADE_SKY,
            Music = MUSIC_ARCADE,
                Balls = BALLS_ARCADE,
            Shots = 56,
            CeilingStep = 8,
            OccupiedBlock = (x, z, i, depth) => GizaCourse(x, z, i),
            BlockColour = GizaColour,
        };

        private static int GizaLow(int i) => GIZA_BASE_LOW + i / 2;

        private static int GizaHigh(int i) => GIZA_BASE_HIGH - (i + 1) / 2;

        private static bool GizaShaft(int x, int z, int i)
        {
            if (i >= GIZA_SOLID_FROM) return false;   //the shaft runs INTO the capstone, which is solid anyway

            int high = i % 2 == 0 ? GIZA_SHAFT_HIGH_EVEN : GIZA_SHAFT_HIGH_ODD;

            return x >= GIZA_SHAFT_LOW && x <= high && z >= GIZA_SHAFT_LOW && z <= high;
        }

        private static bool GizaCourse(int x, int z, int i)
        {
            if (GizaShaft(x, z, i)) return true;

            int a = GizaLow(i), b = GizaHigh(i);
            if (x < a || x > b || z < a || z > b) return false;

            //The courses are hollow picture-frame rings two cells wide; only the gold cap is solid, and no
            //course has a tread - the flat top of a step faces the glass, so nothing under the level sees one
            return i >= GIZA_SOLID_FROM || GizaInset(x, z, a, b) < GIZA_WALL;
        }

        /// <summary>How many cells in from its course's own outer edge a cell sits - 0 on the outer ring of
        /// the two, 1 on the inner one.</summary>
        private static int GizaInset(int x, int z, int a, int b) =>
            Math.Min(Math.Min(x - a, b - x), Math.Min(z - a, b - z));

        private static BallType GizaColour(int x, int z, int i)
        {
            //The capstone and the shaft are lattice-blocked rather than walked round a perimeter, neither
            //being a ring: the cap is a solid mass and the shaft is a column four cells across at most
            if (i >= GIZA_SOLID_FROM) return Band(x / 2 + z / 2 + i / 2, GIZA_GOLD);
            if (GizaShaft(x, z, i)) return Band(i / 2, GIZA_SAND);

            int a = GizaLow(i), b = GizaHigh(i);

            //The door and the eye are recoloured through BOTH cells of the wall on the +Z face, so each
            //stroke is twice the group and the drawing survives the outer course coming away
            if (z >= b - (GIZA_WALL - 1))
            {
                char ink = PixelAt(GIZA_EYE, x - GIZA_EYE_COLUMN, GIZA_EYE_TOP - i);

                if (ink == 'C') return GIZA_EYE_RING;
                if (ink == 'B') return GIZA_PUPIL;

                if (i <= GIZA_DOOR_TOP && x >= GIZA_DOOR_X0 && x <= GIZA_DOOR_X1) return GIZA_DOOR;
            }

            int inset = GizaInset(x, z, a, b);

            return Band(GizaColumn(x, z, a + inset, b - inset) + i / 2, GIZA_SAND);
        }

        /// <summary>
        /// Which of <see cref="GIZA_COLUMNS"/> wedges a ring cell is in, as the fraction of the way round its
        /// OWN square from the course's near corner. Its own and not the course's outer one, for
        /// <see cref="ZigguratColumn"/>'s reason: a two-cell course is two nested rings, each walking its own
        /// perimeter, and that is what keeps a column the same wedge on both of them.
        /// </summary>
        private static int GizaColumn(int x, int z, int a, int b)
        {
            int side = b - a;

            int around =
                z == a ? x - a :
                x == b ? side + (z - a) :
                z == b ? 2 * side + (b - x) :
                3 * side + (b - z);

            return around * GIZA_COLUMNS / (4 * side);
        }

        //THE TROPHY. A bowl hanging by its whole rim circle - the strongest anchor in the batch - over a
        //nested stem and a marble foot, with two handle loops tying the lower bowl back up to the rim. The
        //bowl flares 0.3 to 0.5 a level, which steps every ring into the pockets of the one above; the stem
        //is the same nine-on-four nesting the pyramid's shaft is, and for the same reason - a one-wide strand
        //would not survive its own load, let alone a 69-ball foot bobbing on it.
        private const byte TROPHY_GRID = 17;
        private const byte TROPHY_DEPTH = 14;
        private const int TROPHY_RIM = TROPHY_DEPTH - 1;
        private const int TROPHY_BOWL_FROM = 8;

        //The bowl, course by course from TROPHY_BOWL_FROM up: a wall two cells thick at either level parity,
        //flaring to the rim, whose top TWO levels share the widest annulus so the anchor is a full ring
        //rather than a single course of one.
        private static readonly float[] TROPHY_BOWL_INNER = { 2.0f, 2.5f, 3.0f, 3.2f, 3.5f, 3.5f };
        private static readonly float[] TROPHY_BOWL_OUTER = { 4.0f, 4.5f, 5.0f, 5.2f, 5.5f, 5.5f };

        private const int TROPHY_FLOOR_TOP = 7;
        private const int TROPHY_FLOOR_LOW = 6;
        private const float TROPHY_FLOOR_WIDE = 3.2f;
        private const float TROPHY_FLOOR_NARROW = 2.4f;

        //The stem: 1.45 is the one radius that gives the nested column both ways round. On the levels whose
        //cells sit on the axis it admits the 3x3 out to its corner at sqrt(2); on the levels standing half a
        //cell off it, it admits the 2x2 at 0.707 and refuses the next ring at 1.58. One constant, both
        //courses, and no parity test anywhere.
        private const int TROPHY_STEM_FROM = 2;
        private const float TROPHY_STEM = 1.45f;
        private const float TROPHY_FOOT = 3.2f;

        //The handles. Two mirrored strands two cells deep in z and two out in radius, running down the +X and
        //-X flanks. Every bound is a QUARTER cell off the lattice on purpose - no cell of either parity can
        //land on one - so each strand is exactly two by two on every level instead of fraying by parity; it
        //is PolarBlock's rule again. TROPHY_HANDLE_OUTER also sets the level's lateral margin, reaching the
        //last but one column of the field.
        private const int TROPHY_HANDLE_BOTTOM = TROPHY_BOWL_FROM;
        private const int TROPHY_HANDLE_TOP = TROPHY_RIM;
        private const float TROPHY_HANDLE_Z0 = -1.7f;
        private const float TROPHY_HANDLE_Z1 = 0.3f;
        private const float TROPHY_HANDLE_INNER = 5.2f;
        private const float TROPHY_HANDLE_OUTER = 7.2f;

        //And the weld, which is the design's first gate answered IN THE GEOMETRY rather than measured after
        //it. On the two levels where a handle meets the shell the strand runs inward to here instead, which
        //is inside the bowl's own annulus on both of them - so the run of cells from shell to handle is
        //unbroken by construction and no discretisation of the annulus can leave a handle end hanging free.
        private const float TROPHY_WELD_INNER = 3.4f;

        //Twenty-four sectors round a wall whose mid radius is about 4.5, so a sector is about 1.2 cells and
        //TROPHY_BLOCK_ARC of 3 is the spec's three arc cells. EIGHT blocks go round, which is even, so the
        //seam of the closed rim cannot put one colour against itself. This is the design's group dial.
        private const int TROPHY_SECTORS = 24;
        private const int TROPHY_BLOCK_ARC = 3;
        private const int TROPHY_BLOCK_LEVELS = 2;

        //The jewel studs, grafted off the rejected Saucer's re-kicking lamps: one arc cell every third,
        //eight of them round the bowl's flare, two levels tall through both wall layers. Two clear sectors
        //between neighbours, which is about two and a half cells at that radius, so all eight stay separate
        //groups round the closed rim. MAGENTA and not Saucer's red, because red would sit directly on the
        //orange half of the gold check and red/orange is a listed confusable pair.
        private const int TROPHY_STUD_LOW = 10;
        private const int TROPHY_STUD_HIGH = 11;
        private const int TROPHY_STUD_STRIDE = 3;
        private const int TROPHY_STUD_PHASE = 1;

        private static readonly BallType[] TROPHY_GOLD = { BallType.Type7, BallType.Type9 };     //yellow, orange
        private static readonly BallType[] TROPHY_MARBLE = { BallType.Type12, BallType.Type11 }; //navy, silver
        private const BallType TROPHY_CUT = BallType.Type10;                                     //brown, the stem
        private const BallType TROPHY_ENGRAVING = BallType.Type12;                               //navy
        private const BallType TROPHY_STUD = BallType.Type6;                                     //magenta

        /// <summary>
        /// The engraved digit 1 on the bowl's +Z face, three sectors by five levels, top row first, cut
        /// through both cells of the wall. <c>N</c> is the navy of the engraving and everything else is the
        /// gold under it. It is one connected group of sixteen: a same-index vertical stack IS a lattice
        /// contact, the shifted levels standing half a cell over in both x and z, so the digit's stem holds
        /// together without being two cells wide.
        /// </summary>
        private static readonly string[] TROPHY_ONE =
        {
            ".N.",
            "NN.",
            ".N.",
            ".N.",
            "NNN",
        };

        private const int TROPHY_ENGRAVING_COLUMN = 17;
        private const int TROPHY_ENGRAVING_TOP = 12;

        /// <summary>
        /// The high-score cup: a gold bowl hanging by its whole rim, engraved with a big 1, studded with
        /// eight jewels, handles tying its lower wall back up to the rim, and a marble foot on one brown
        /// stem. The finale-adjacent slot belongs to the arcade's own prize, and this is the batch's best
        /// earned-physics payoff.
        /// <para>
        /// <b>The counterweight release is the level.</b> The foot bobs on the springy nested stem from the
        /// first second, and the stem is the game's only brown - one group, one shot, the designated cut.
        /// Sever it and the foot drops away and the suddenly unloaded cup recoils UPWARD and rings on its
        /// rim: the podium hop, played by the simulation rather than animated. Popping the magenta studs one
        /// by one afterwards keeps re-kicking the freshly unloaded cup.
        /// </para>
        /// <para>
        /// <b>Three load paths, which is why it can afford a designated cut at all.</b> The bowl is a closed
        /// tube hanging by its entire rim circle; the two handle loops tie the lower bowl back up to that rim,
        /// so even a fully breached wall still hangs by them; and the stem is a nested column rather than a
        /// strand. Only the foot hangs by one thing, and that is the point of it.
        /// </para>
        /// <para>
        /// <b>The floor discs and the foot are turned Rings, not block checks.</b> That is
        /// <see cref="ReelColour"/>'s measured finding rather than a preference: sectors converge on the axis
        /// and a block dither on a horizontal plate has no diagonal neighbours to percolate through, so a
        /// checked foot came out as three dozen groups of four - half the budget spent on the base. Rings
        /// read as turned marble and cost two groups.
        /// </para>
        /// <para>
        /// Priced at 58 shots against a target near 42 standing groups (1.38 a group, the tightest of the
        /// second hang) on a step-8 clock. <b>Check first</b>: both handle welds on the measured contact
        /// graph - <see cref="TROPHY_WELD_INNER"/> is the construction that should make them unconditional,
        /// and if a strand still measures free the next move is fattening the bottom course's outer radius to
        /// 4.2. <b>Then</b>: AimReachability on the brown stem from low orbit angles behind the foot's
        /// shadow, since a designated cut that cannot be shot is a dead set piece - if it shadows, shrink
        /// <see cref="TROPHY_FOOT"/> to 2.8. <b>Then</b>: yellow/orange percolation over bowl and handles,
        /// whose fallback adds brown blocks and recolours the stem green(2) so the cut stays unique.
        /// </para>
        /// </summary>
        private static Design Trophy() => new()
        {
            File = "Trophy.json",
            Name = "Trophy",
            Grid = TROPHY_GRID,
            Depth = TROPHY_DEPTH,
            FieldLevels = ARCADE_FIELD,
            Scene = SceneKind.NeonCity,
            Sky = ARCADE_SKY,
            Music = MUSIC_ARCADE,
                Balls = BALLS_ARCADE,
            Shots = 58,
            CeilingStep = 8,
            Occupied = (r, ang, i, depth) => TrophyShell(r, ang, i),
            Colour = TrophyColour,
        };

        private static bool TrophyShell(float r, float ang, int i)
        {
            if (TrophyHandle(r, ang, i)) return true;

            if (i >= TROPHY_BOWL_FROM)
            {
                int course = i - TROPHY_BOWL_FROM;
                return r >= TROPHY_BOWL_INNER[course] && r <= TROPHY_BOWL_OUTER[course];
            }

            if (i == TROPHY_FLOOR_TOP) return r <= TROPHY_FLOOR_WIDE;
            if (i == TROPHY_FLOOR_LOW) return r <= TROPHY_FLOOR_NARROW;
            if (i >= TROPHY_STEM_FROM) return r <= TROPHY_STEM;

            return r <= TROPHY_FOOT;
        }

        /// <summary>
        /// Whether a cell is on one of the two handles. The strand is read in the CENTRED CARTESIAN pair
        /// recovered from the polar one - <c>r*cos(ang)</c> and <c>r*sin(ang)</c> are the very dx and dz the
        /// emitter built them from, which is <see cref="PolarBlock"/>'s lossless direction - because a handle
        /// is a raw-frame strand welded onto a solid of revolution and the emitter hands a design one frame.
        /// </summary>
        private static bool TrophyHandle(float r, float ang, int i)
        {
            if (i < TROPHY_HANDLE_BOTTOM || i > TROPHY_HANDLE_TOP) return false;

            float dz = r * MathF.Sin(ang);
            if (dz < TROPHY_HANDLE_Z0 || dz > TROPHY_HANDLE_Z1) return false;

            //On the two weld levels the strand reaches inward into the bowl's own annulus - see
            //TROPHY_WELD_INNER, which is this design's first gate answered in the geometry
            float inner = i == TROPHY_HANDLE_TOP || i == TROPHY_HANDLE_BOTTOM
                ? TROPHY_WELD_INNER
                : TROPHY_HANDLE_INNER;

            float dx = MathF.Abs(r * MathF.Cos(ang));

            return dx >= inner && dx <= TROPHY_HANDLE_OUTER;
        }

        private static BallType TrophyColour(float r, float ang, int i, int depth)
        {
            if (i < TROPHY_STEM_FROM) return Ring(r, TROPHY_MARBLE);
            if (i < TROPHY_FLOOR_LOW) return TROPHY_CUT;
            if (i < TROPHY_BOWL_FROM) return Ring(r, TROPHY_GOLD);

            int column = SectorIndex(ang, 0f, TROPHY_SECTORS);

            //The handles wear the plain gold check and carry no ornament: a stud landing on a strand would
            //break the loop's colour into pieces and read as a fault rather than as a jewel
            if (!TrophyHandle(r, ang, i))
            {
                if (PixelAt(TROPHY_ONE, column - TROPHY_ENGRAVING_COLUMN, TROPHY_ENGRAVING_TOP - i) == 'N')
                    return TROPHY_ENGRAVING;

                if (i >= TROPHY_STUD_LOW && i <= TROPHY_STUD_HIGH
                    && column % TROPHY_STUD_STRIDE == TROPHY_STUD_PHASE)
                    return TROPHY_STUD;
            }

            return Band(column / TROPHY_BLOCK_ARC + (TROPHY_RIM - i) / TROPHY_BLOCK_LEVELS, TROPHY_GOLD);
        }

        #endregion

        //Concentric shells one and a bit cells thick: thick enough that a ring is a solid band of colour
        //rather than a dotted circle once the lattice has rounded it off.
        private static BallType Ring(float r, BallType[] palette) =>
            palette[(int)MathF.Floor(r / 1.9f) % palette.Length];

        #region The arcade levels' own geometry

        //THE BLOCK'S FIELD. Eighteen for every level of it, which is two things at once: the deepest field
        //the camera frames whole (GameplayScreen.FRAMED_LEVELS), so an object is never clipped, and the room
        //the ceiling descends into - the empty levels UNDER a layout are a level's clearance, and a 12-deep
        //solid in an 18-level field leaves six of them, nine descents. That is the Reveal's own arithmetic and
        //the budgets here are priced against it: a level whose two clocks disagree is the fault
        //PICTURE_FIELD_LEVELS was written to record. (Eight descents when this block was built, nine since the
        //death line was lowered onto the island - the block's clearances are 3.96 to 8.21 now, all of them
        //over what their budgets spend, so nothing here is ended by the ceiling before its balls run out.)
        private const byte ARCADE_FIELD = 18;

        //The block's dome. Unlike the three sky-replacing blocks before it the neon city has a real sky over
        //it, so this number is SEEN: it lights the balls through SkyLightRig and it is the whole upper half
        //of the frame behind a body that is meant to read as a silhouette. 16 is the darkest of the eighteen
        //(a near-black zenith), which is what the city's own magenta and cyan want behind them - the Coil
        //passed 16 over for being darker than the block that followed it, and nothing follows this one.
        private const byte ARCADE_SKY = 16;

        //THE CUBE. Ten cells on a side and twelve levels deep, which reads as a cube: nine world units across
        //a face against eleven level steps of 1/sqrt(2), i.e. 7.78 tall. Its walls and both its plates are one
        //cell thick, so a 1200-cell box costs 560 balls, and each of the six faces carries one glyph.
        private const byte CUBE_GRID = 12;
        private const byte CUBE_DEPTH = 12;
        private const int CUBE_SIDE = 10;
        private const int CUBE_ORIGIN = (CUBE_GRID - CUBE_SIDE) / 2;

        //How coarse the dithered ground is, in cells, and the block's LOUDEST tuning knob: it sets the group
        //count, which sets the budget. The number was measured rather than judged. A shot clears one standing
        //group, so a level of G groups needs a budget above G to be finishable at all, and the pack's hardest
        //ratios are Colossus at 0.98 and Static at 1.43 - anything finer than 5 here puts this cube past both.
        //Measured, same cube and same glyphs, at ground blocks of 3 / 4x5 / 5: 75, 44 and 34 standing groups.
        //Five is the coarsest of the three and the only one whose budget stays in family (56 shots, 1.65 a
        //group, the block's gentlest); it costs the fine dither - a face is a 2x2 check of five-cell blocks
        //rather than a hatch - and the glyph, not the ground, is what carries the drawing.
        private const int CUBE_GROUND_BLOCK = 5;

        //Where a glyph sits on its face: two cells in from the left, one row down on a wall and two on a
        //plate. The inset is not margin for its own sake - a 6x8 glyph on a 10x10 face leaves a border two
        //cells wide, and that border IS the face's ground. Drawn 8 wide the same glyphs left a border of one
        //cell, which the block dither then cut into slivers: 58 standing groups against a budget of 56.
        private const int CUBE_GLYPH_COLUMN = 2;

        /// <summary>
        /// An invader, six columns by eight rows — the block's own emblem, and the reason its chapter is
        /// called what it is. It is drawn eight rows to six columns rather than square because a level step
        /// is 1/√2: a glyph drawn as many rows as columns comes out squashed to 71 % of its height, which is
        /// the Gallery's own rule arriving on a cube.
        /// <para>
        /// Its strokes may be one cell wide because a glyph is <b>one connected group</b> — the lonely-ball
        /// rule is about a ball's own colour, which <see cref="FERN"/> records from the other side. What may
        /// <i>not</i> be one cell is a hole in it: a single enclosed ground cell has no ground neighbour and
        /// the repair pass would fill it in, quietly redrawing the face between the source and the file. The
        /// visor is therefore two cells wide, which is why this invader wears one instead of having eyes.
        /// </para>
        /// </summary>
        private static readonly string[] CUBE_INVADER =
        {
            ".#..#.",
            ".#..#.",
            ".####.",
            "######",
            "##..##",
            "######",
            ".#..#.",
            ".#..#.",
        };

        /// <summary>A key, six by eight: the bow, the shaft and two wards.</summary>
        private static readonly string[] CUBE_KEY =
        {
            ".####.",
            ".#..#.",
            ".#..#.",
            ".####.",
            "..##..",
            "..###.",
            "..##..",
            "..###.",
        };

        /// <summary>
        /// A coin, six by eight — a ring with its middle open. The hole is the one place in the block where a
        /// patch of ground is <b>enclosed</b> by a glyph, which is deliberate: those cells hang off the ring
        /// itself, so shooting the ring out drops them, and that is the cube's one real cascade.
        /// </summary>
        private static readonly string[] CUBE_COIN =
        {
            "..##..",
            ".####.",
            "##..##",
            "##..##",
            "##..##",
            "##..##",
            ".####.",
            "..##..",
        };

        /// <summary>A lightning bolt, six by eight, the one glyph that is not symmetrical.</summary>
        private static readonly string[] CUBE_BOLT =
        {
            "...##.",
            "..##..",
            ".##...",
            "####..",
            "..####",
            "...##.",
            "..##..",
            ".##...",
        };

        /// <summary>
        /// A cross, six by six, on the bottom plate — the face the game's camera reads best, looking up at
        /// the cluster from under it, and the one glyph drawn square because a plate's rows are cells rather
        /// than level steps. <b>Two cells in from every edge of the plate</b>, and that inset is load-bearing:
        /// a cross reaching the plate's border would be a cross touching the bottom row of all four walls,
        /// where their glyphs are the same black — measured before the inset, four glyphs and the cross were
        /// ONE group of 144, a third of the level in a single shot.
        /// </summary>
        private static readonly string[] CUBE_CROSS =
        {
            "..##..",
            "..##..",
            "######",
            "######",
            "..##..",
            "..##..",
        };

        //Which glyph each face carries, indexed by CubeFace's number less one: top, bottom, -X, +X, -Z, +Z.
        //The top plate hangs against the glass and no camera in the game can see it, so it carries none.
        private static readonly string[][] CUBE_GLYPHS =
        {
            null, CUBE_CROSS, CUBE_INVADER, CUBE_KEY, CUBE_COIN, CUBE_BOLT,
        };

        //The grounds, one pair per face in the same order, and THREE pairs rather than one. A single dither
        //over the whole cube is one colour reaching every face through the edges - the top plate, which is
        //the anchor, included - so opposite faces share a pair and neighbouring faces never do: the gun's
        //orbit alternates warm and cool, and no ground colour meets its own kind round a corner. Every glyph
        //is black against all three, which also keeps ONE colour carrying five groups rather than five
        //colours carrying one each - the magazine draws evenly among the colours still standing, so a colour
        //with far more groups than its share of the draw is a colour the player cannot spend.
        private static readonly BallType[][] CUBE_GROUNDS =
        {
            new[] { BallType.Type4, BallType.Type11 },   //top:    white and silver
            new[] { BallType.Type4, BallType.Type11 },   //bottom: white and silver
            new[] { BallType.Type1, BallType.Type9 },    //-X:     red and orange
            new[] { BallType.Type1, BallType.Type9 },    //+X:     red and orange
            new[] { BallType.Type5, BallType.Type6 },    //-Z:     cyan and magenta
            new[] { BallType.Type5, BallType.Type6 },    //+Z:     cyan and magenta
        };

        //THE CORNER POSTS (#317), and both halves of the finding are measured. The box's walls are one cell
        //thick, so each is a sheet of BallSocket chains, and the wall dither's 5x5 quadrants let a single
        //shot sever five columns' whole upper half from the anchor plate: the probe watched one 22-ball
        //wall block take the line from +3.93 to -1.06 IN ONE SHOT with nothing orphaned, the bottom corner
        //diving five units on the lateral chains that were left — and three more orders lost to the same
        //stretch arriving cumulatively (4 of 5, twice on a resting glass). ⚠ The obvious remedy was
        //Cabinet's mezzanine shelf and IT MEASURED WORSE (5 of 5): sixty-four interior balls at mid-height
        //cured the one-shot dive but hung a third more dead weight off the very chains that were yielding —
        //the line reached +1.5 by shot 5 where the shipped box still held +3.9. What the trace wanted was
        //stiffness without load: a SECOND CELL inside each corner, top plate to bottom plate, cross-braced
        //to both adjacent walls on every level — Pylon's 2x2 leg section standing at the box's own corners,
        //forty balls, four direct load paths to the anchor that no wall-block release can cut (the dither
        //never spans a corner and the glyphs are inset two columns). Diegetically it is the box's corner
        //framing, seen edge-on once a face is opened — the hollow rule bends the way Cabinet's already
        //did, by the smallest amount that carries.
        //
        //THE POSTS WEAR THEIR OWN PAIR (yellow and green, in runs of five, the top run alternating per
        //corner), and the pair is forced rather than chosen: every ink already in the level fuses with a
        //neighbour of the post — the wall pairs touch it through the walls it braces, the plate pair
        //through both plates it spans — and a post fused into a wall block is a post one wall shot removes.
        //Two fresh inks keep all eight post runs their own groups; five-ball runs clear MIN_GROUP, and
        //gold-and-green corner trim on an arcade cabinet is period-correct.
        private const int CUBE_POST_RUN = 5;

        private static readonly BallType[] CUBE_POST_PAIR = { BallType.Type7, BallType.Type2 };   //yellow, green

        /// <summary>
        /// Which corner post a cell is in (1..4, or 0 for none): the interior cell diagonal to each of the
        /// box's four corners, on every wall level — see <see cref="CUBE_POST_RUN"/> for what the posts
        /// carry and the measured trace that asked for them.
        /// </summary>
        private static int CubePost(int x, int z, int i)
        {
            if (i == 0 || i >= CUBE_DEPTH - 1) return 0;   //the plates own their levels

            int cx = x - CUBE_ORIGIN;
            int cz = z - CUBE_ORIGIN;

            bool xLow = cx == 1;
            bool xHigh = cx == CUBE_SIDE - 2;
            bool zLow = cz == 1;
            bool zHigh = cz == CUBE_SIDE - 2;

            if (xLow && zLow) return 1;
            if (xHigh && zLow) return 2;
            if (xLow && zHigh) return 3;
            if (xHigh && zHigh) return 4;
            return 0;
        }

        /// <summary>
        /// Which face of the cube a cell is on: 0 for none (the hollow inside, and everything off the box),
        /// 1 the top plate, 2 the bottom plate, 3 −X, 4 +X, 5 −Z, 6 +Z.
        /// <para>
        /// <b>The box is drawn in lattice indices rather than in the centred frame</b>, which is the opposite
        /// of what <see cref="ChestPart"/> does with its own crate and is a decision about the drawing. A
        /// centred extent gives physically flat faces at the price of alternating 8 and 9 cells per row (the
        /// close packing; see <see cref="CHEST_HALF"/>), and a glyph cannot be laid on a face whose columns
        /// change with the level parity. In indices every face is exactly ten columns on every level and the
        /// stagger goes into the surface instead — the odd levels stand half a cell out, so the faces are
        /// brick-bonded and the vertical edges saw by a quarter of a ball. That is what the Gallery's flat
        /// walls have always looked like, and on a cube it reads as courses of blocks.
        /// </para>
        /// <para>
        /// The ±X faces claim the corner columns (they are tested first), so the ±Z faces run from column 1
        /// to 8 — which is exactly the span a glyph occupies, so all four walls carry theirs whole.
        /// </para>
        /// </summary>
        private static int CubeFace(int x, int z, int i)
        {
            int cx = x - CUBE_ORIGIN;
            int cz = z - CUBE_ORIGIN;

            if (cx < 0 || cx >= CUBE_SIDE || cz < 0 || cz >= CUBE_SIDE) return 0;

            if (i == CUBE_DEPTH - 1) return 1;
            if (i == 0) return 2;

            if (cx == 0) return 3;
            if (cx == CUBE_SIDE - 1) return 4;
            if (cz == 0) return 5;
            if (cz == CUBE_SIDE - 1) return 6;

            return 0;
        }

        private static BallType CubeColour(int x, int z, int i)
        {
            int face = CubeFace(x, z, i);
            int cx = x - CUBE_ORIGIN;
            int cz = z - CUBE_ORIGIN;

            //The corner posts, before the faces: their cells are interior, so no face owns them. Runs of
            //CUBE_POST_RUN levels alternate the pair, and diagonal corners start on the same ink so the
            //trim reads as a scheme rather than a scatter.
            int post = CubePost(x, z, i);
            if (face == 0 && post != 0)
                return Band((i - 1) / CUBE_POST_RUN + (post == 2 || post == 3 ? 1 : 0), CUBE_POST_PAIR);

            //A plate is read across the lattice; a wall is read along its own run and DOWN from under the top
            //plate, so every wall's row 0 is at the same height and the four glyphs line up round the cube
            int column = face <= 2 ? cx : face <= 4 ? cz : cx;
            int row = face <= 2 ? cz : CUBE_DEPTH - 2 - i;

            string[] glyph = CUBE_GLYPHS[face - 1];

            if (glyph != null
                && PixelAt(glyph, column - CUBE_GLYPH_COLUMN, row - (face <= 2 ? 2 : 1)) == '#')
                return BallType.Type8;

            return Band(column / CUBE_GROUND_BLOCK + row / CUBE_GROUND_BLOCK, CUBE_GROUNDS[face - 1]);
        }

        //THE TEMPLE. Four courses of two levels each, widening by one cell a side as they descend: 7, 9, 11
        //and 13 across, in a grid of 15 so the base still leaves the free column every layout needs. The top
        //is a full 7x7 plate rather than a ring - 49 cells of anchor, and the one place on this body where
        //the colouring is not a course.
        //
        //THE COURSE IS TWO CELLS THICK AND EIGHT LEVELS TALL, AND BOTH FIGURES ARE PHYSICS. Drawn first as
        //one-cell rings twelve levels deep it did not survive its own weight: a ring that thin is a curtain
        //of BallSocket links with nothing bracing it across, and the level LOST ITSELF IN EIGHT SECONDS with
        //no shot fired - the whole temple sagged past the death line while the camera was still settling.
        //(Garland found the same wall from the other side, at 1.15 cells of strand; the Chest's two-cell box
        //and the Vortex's two-cell wall are the ones that hold.) Two cells is parallel chains sharing the
        //load, and halving the height halves what the top course has to carry.
        private const byte ZIGGURAT_GRID = 15;
        private const byte ZIGGURAT_DEPTH = 8;
        private const int ZIGGURAT_CENTRE = (ZIGGURAT_GRID - 1) / 2;
        private const int ZIGGURAT_TOP_SIDE = 7;
        private const int ZIGGURAT_COURSE_LEVELS = 2;
        private const int ZIGGURAT_WALL = 2;

        //How many vertical columns the ring is cut into for colouring, and it is bounded from BOTH sides.
        //Fewer makes each block a large slab of a course - a plate on the level whose colouring exists to
        //avoid one - and more starves the top course, which is 40 cells round over its two nested rings.
        //Measured on the finished temple: 7 columns give 29 standing groups, 9 give 33 and 11 give 42, and
        //the budget follows the count. Nine also satisfies the wrap: the palette advances two per column, so
        //eight columns on from the seam is entry 16, which modulo five is not entry 0 - at six columns the
        //last column and the first would have met in the same colour and merged into one group across it.
        private const int ZIGGURAT_COLUMNS = 9;

        //Five, against a stride of two per column and one per course. The stride is what keeps every pair of
        //touching blocks - sideways, downward AND both diagonals - on different entries: +2, +1, +3 and -1,
        //none of them 0 modulo 5. Four colours would fail on the -1 diagonal and the courses would fuse.
        private static readonly BallType[] ZIGGURAT_PALETTE =
        {
            BallType.Type10, BallType.Type7, BallType.Type13, BallType.Type9, BallType.Type4,
        };

        private static int ZigguratCourse(int i) => (ZIGGURAT_DEPTH - 1 - i) / ZIGGURAT_COURSE_LEVELS;

        private static int ZigguratHalf(int i) => ZIGGURAT_TOP_SIDE / 2 + ZigguratCourse(i);

        /// <summary>
        /// Whether a cell is on the temple: the ring of its own course, or anywhere on the top plate. The
        /// courses are hollow and there are no treads — the flat top of a step faces the glass, so nothing
        /// standing under the level can see one, and a ring hangs off the wider ring above it perfectly well
        /// without (a cross-level neighbour reaches one cell out, which is exactly what a course steps by).
        /// </summary>
        private static bool ZigguratCourseWall(int x, int z, int i)
        {
            int half = ZigguratHalf(i);
            int box = Math.Max(Math.Abs(x - ZIGGURAT_CENTRE), Math.Abs(z - ZIGGURAT_CENTRE));

            if (box > half) return false;

            return i == ZIGGURAT_DEPTH - 1 || box > half - ZIGGURAT_WALL;
        }

        private static BallType ZigguratColour(int x, int z, int i)
        {
            int half = ZigguratHalf(i);
            int dx = x - ZIGGURAT_CENTRE;
            int dz = z - ZIGGURAT_CENTRE;

            int box = Math.Max(Math.Abs(dx), Math.Abs(dz));

            //The plate's interior is not on a ring, so it takes the lattice blocks instead - same palette,
            //same stride, so the anchor layer carries four of the five colours and no one of them can drop
            //the temple. Only the top level has one; every other course is wall all the way through.
            if (box <= half - ZIGGURAT_WALL) return Band(2 * (x / 2) + z / 2, ZIGGURAT_PALETTE);

            //Off the cell's OWN square, not the course's outer one: a two-cell course is two nested rings and
            //each walks its own perimeter, which is what keeps a column the same wedge on both of them.
            return Band(2 * ZigguratColumn(dx, dz, box) + ZigguratCourse(i), ZIGGURAT_PALETTE);
        }

        /// <summary>
        /// Which of <see cref="ZIGGURAT_COLUMNS"/> columns a ring cell is in, as the fraction of the way
        /// round the square from one corner — <b>a fraction and not a cell count</b>, so a column is the same
        /// wedge of the temple on every course however much longer the course's own perimeter is, and the
        /// colouring runs straight down the building rather than shearing round it.
        /// </summary>
        private static int ZigguratColumn(int dx, int dz, int half)
        {
            int side = 2 * half;

            int around =
                dz == -half ? dx + half :
                dx == half ? side + (dz + half) :
                dz == half ? 2 * side + (half - dx) :
                3 * side + (half - dz);

            return around * ZIGGURAT_COLUMNS / (4 * side);
        }

        //THE REEL. A drum 4.6 out with a wall 1.6 thick (two cells at either level parity) and a head at each
        //end, twelve levels deep. Twenty-four sectors round it, six to a panel: at the rim a sector is about
        //one cell wide, which is what makes a sector a PIXEL and lets a bitmap be wrapped onto the wall the
        //way the Gallery lays one on a flat one.
        private const byte REEL_GRID = 13;
        private const byte REEL_DEPTH = 12;
        private const float REEL_RADIUS = 4.6f;
        private const float REEL_WALL = 1.6f;
        private const int REEL_PANELS = 4;
        private const int REEL_PANEL_COLUMNS = 6;
        private const int REEL_SECTORS = REEL_PANELS * REEL_PANEL_COLUMNS;

        //The white reel, its red sevens and black diamonds, and the cold metal of the two heads.
        private static readonly BallType[] REEL_GROUND = { BallType.Type4, BallType.Type11 };
        private static readonly BallType[] REEL_HEAD = { BallType.Type12, BallType.Type5 };

        /// <summary>
        /// A seven, six sectors by ten levels — the reel's own symbol, drawn top-first like every bitmap in
        /// the generator. Two cells thick everywhere for the lonely-ball rule, and it never reaches a panel's
        /// edge, so the four symbols round the drum stay four groups.
        /// </summary>
        private static readonly string[] REEL_SEVEN =
        {
            "######",
            "######",
            "....##",
            "....##",
            "...##.",
            "...##.",
            "..##..",
            "..##..",
            "..##..",
            "..##..",
        };

        /// <summary>A diamond, six by ten, on the two panels between the sevens.</summary>
        private static readonly string[] REEL_DIAMOND =
        {
            "..##..",
            "..##..",
            ".####.",
            ".####.",
            "######",
            "######",
            ".####.",
            ".####.",
            "..##..",
            "..##..",
        };

        private static bool ReelShell(float r, int i, int depth) =>
            r <= REEL_RADIUS && (i == 0 || i == depth - 1 || r >= REEL_RADIUS - REEL_WALL);

        private static BallType ReelColour(float r, float ang, int i, int depth)
        {
            //The heads take the LATTICE blocks and not the sectors, and that is the difference between a
            //machined end and a dartboard: sectors converge on the axis, so a sector dither would end in
            //single cells there - the trap the globe's poles answer the same way
            if (i == 0 || i == depth - 1) return Ring(r, REEL_HEAD);

            int column = SectorIndex(ang, 0f, REEL_SECTORS);
            int panel = column / REEL_PANEL_COLUMNS;
            int row = depth - 2 - i;

            bool seven = (panel & 1) == 0;

            //The symbol is a function of the sector alone, so it goes through both cells of the wall - the
            //picture is still there when the outer skin has gone, and every stroke is twice the group
            if (PixelAt(seven ? REEL_SEVEN : REEL_DIAMOND, column % REEL_PANEL_COLUMNS, row) == '#')
                return seven ? BallType.Type1 : BallType.Type8;

            return Band(column / 4 + row / 4, REEL_GROUND);
        }

        //THE DONUT. A ring of major radius 4 with a tube of 2.6 hollowed to a shell of 1.2, eight levels
        //deep. The depth is set BY the tube and not by taste: the topmost level sits 2.47 above the tube's
        //own middle, so a tube any thinner than that would have no cells on the level the ceiling holds and
        //the whole layout would hang off nothing (an empty top level is the one thing a hanging design can
        //get catastrophically wrong, and no gate but the drop test would say so).
        private const byte DONUT_GRID = 17;
        private const byte DONUT_DEPTH = 8;
        private const float DONUT_MAJOR = 4.4f;
        private const float DONUT_TUBE = 2.6f;
        private const float DONUT_SHELL = 1.2f;
        private const int DONUT_SECTORS = 24;

        //The glaze line, in world units above the tube's middle, and how far it runs down between its drips.
        //A straight line here is a horizontal band round the whole ring - the drop test's trap, and on a body
        //whose top surface is its anchor it is the one colouring that could take the level in a single shot.
        //Waved, glaze and dough interlock at every drip and each is anchored on its own.
        private const float DONUT_GLAZE_LINE = -0.2f;
        private const float DONUT_DRIP = 0.9f;
        //Eight drips, not the six it shipped with (#317): the drips are the glaze/dough interlocks, i.e. the
        //bridges a dangling arc re-hangs across, and at six the probe's endgame traces still whipped a free
        //arc to the instant-loss line by shot 17. Eight puts a bridge every three sectors instead of four:
        //the deaths moved to shots 18-23 with a whole extra bite of the ring spent first, and — measured, not
        //expected — the repair pass dropped from 6 rewrites to 1, because the shorter wave segments land on
        //the stripe boundaries more evenly. Still reads as drips; slightly nicer drips, if anything.
        private const int DONUT_DRIPS = 8;

        //Three inks a band, not the two it shipped with, and the third entries are half of #317's fix: on
        //two inks the 2x2 blocks fused along the band grid's diagonal into staircases winding round the ring
        //— orange measured 66 balls in ONE standing group, white 61, brown 55, magenta 49 — and the sag
        //probe's traces showed what such a release does to a torus: it severs the ring's local path to the
        //top annulus across several bearings at once, a second one leaves the arc between them DANGLING, and
        //a mid-run ball from level 15 ended up the cluster's lowest body, eight units under its own lattice
        //seat, through the death line with the glass two steps down (4-5 of 5 losing orders, shots 12-16).
        //⚠ Three inks on the BLOCK grid were measured and are not enough on this body: at 2x2 blocks the
        //count exploded (66 groups, 0.79 a shot, 19 repair rewrites at the inner face) and at 3x2 the null
        //diagonal refused brown back into a 59-ball net. The grid itself is the fault on a torus, so the
        //colour is a STRIPE now — see DonutColour. Black is chocolate dough; navy is a berry glaze — and the
        //anchor annulus is glaze, so the level's top interleaves three colours instead of two.
        private static readonly BallType[] DONUT_DOUGH = { BallType.Type10, BallType.Type9, BallType.Type8 };
        private static readonly BallType[] DONUT_GLAZE = { BallType.Type6, BallType.Type4, BallType.Type12 };
        private static readonly BallType[] DONUT_SPRINKLES = { BallType.Type7, BallType.Type5 };

        /// <summary>Distance from the tube's own core circle — the ring's radius in its cross-section.</summary>
        private static float DonutTube(float r, int i, int depth)
        {
            float dr = r - DONUT_MAJOR;
            float dy = OnionVertical(i, depth);

            return MathF.Sqrt(dr * dr + dy * dy);
        }

        private static bool DonutShell(float r, int i, int depth)
        {
            float d = DonutTube(r, i, depth);

            return d <= DONUT_TUBE && d >= DONUT_TUBE - DONUT_SHELL;
        }

        private static BallType DonutColour(float r, float ang, int i, int depth)
        {
            int column = SectorIndex(ang, 0f, DONUT_SECTORS);
            int row = depth - 1 - i;

            //STRIPES, not blocks — the colour is a function of the sector alone, two sectors a stripe over
            //three inks. Same-ink stripes stand six sectors apart with two other inks between them, so no
            //colour can percolate anywhere BY CONSTRUCTION: there is no block grid left to carry #301's
            //diagonal fuse, and the tube's inner face — where 24 sectors converge and a 2x2 block used to
            //fragment into the pairs the repair pass rewrote — inherits its colour from the stripe above it
            //instead of becoming an island. Both measured alternatives are recorded on DONUT_DOUGH's comment.
            //Twelve stripes over three inks divide evenly, so the wrap seam is a stripe boundary like any
            //other; the glaze rotation is offset one step so the two bands' inks never pair up per stripe.
            float line = DONUT_GLAZE_LINE - DONUT_DRIP * MathF.Cos(DONUT_DRIPS * ang);
            if (OnionVertical(i, depth) <= line) return Band(column / 2, DONUT_DOUGH);

            return DonutSprinkle(column / 2, row / 2, out BallType sprinkle)
                ? sprinkle
                : Band(column / 2 + 1, DONUT_GLAZE);
        }

        /// <summary>
        /// Whether a block of the glaze carries a sprinkle, and which colour it is — hashed rather than
        /// drawn, for <see cref="Scatter"/>'s reason (a level has to be the same every time it is played, and
        /// a <c>Random</c> walked in the emitter's loop order is not). One block of the glaze in four, over
        /// two colours, which is <b>above</b> the density where a hashed dice percolates: measured, the
        /// sprinkles come out as 39–40 balls a colour in groups of up to 27 rather than as islands of four.
        /// They are the level's scarce colours either way — well under half of what a dough or glaze ink
        /// carries since #317 split the bands three ways.
        /// </summary>
        /// <remarks>
        /// The presence and the colour are read off <b>different</b> bits of the same hash. Taken off the
        /// same ones they correlate — a test of <c>h % 4</c> only ever admits even hashes, so a colour picked
        /// with <c>h % 2</c> would be the first entry every time and the second would never be drawn at all.
        /// </remarks>
        private static bool DonutSprinkle(int blockColumn, int blockRow, out BallType colour)
        {
            uint h = (uint)(blockColumn * 73856093 ^ blockRow * 19349663);
            h ^= h >> 13;
            h *= 2654435761;
            h ^= h >> 16;

            colour = DONUT_SPRINKLES[h % (uint)DONUT_SPRINKLES.Length];

            return ((h >> 5) & 3) == 0;
        }

        //THE GLOBE. A shell two cells thick on a radius of 4.6, twelve levels deep - which cuts both poles
        //off a hair (the layout reaches 3.89 above and below its middle against a radius of 4.6), and that
        //is wanted: it turns the pole from a point into a disc. ⚠ The sphere's own disc measured NINE cells,
        //not the twenty this comment used to claim, and nine is what #301 was about - the top level is now
        //the flat arctic plateau of GLOBE_POLE_CAP, 37 cells measured. The map is sixteen columns of
        //longitude by fourteen rows of latitude - one row per LEVEL of the layout; at the equator a column
        //is about two cells wide, and the ground dithers on 2x2 blocks of the map, which is the pixel.
        private const byte GLOBE_GRID = 13;
        private const byte GLOBE_DEPTH = 14;
        private const float GLOBE_RADIUS = 5f;

        //Two cells of wall at either parity - GHOST_OUTER minus GHOST_INNER, the figure the ghost's tube
        //already stands on - and not the 1.5 it shipped with, which read ONE cell at the mid-latitudes.
        //A curved shell one cell thick has half its cross-level neighbours reaching to a cell that is not
        //there (CABINET's PICTURE_THICKNESS rule; the flat-walled Cube gets away with one cell because a
        //vertical plane keeps its column in diagonal reach at both parities, and a sphere does not), so
        //every colour match tore a gash whose flanks hung on sparse links: the #301 probe watched strips
        //of the picture peel and swing 3 units under their authored seat with the anchors intact. At two
        //cells the wall carries Ghost's own second load path through its thickness.
        private const float GLOBE_SHELL = 2f;
        private const int GLOBE_SECTORS = 16;

        private static readonly BallType[] GLOBE_SEA = { BallType.Type3, BallType.Type12, BallType.Type5 };

        //Three land inks, not two, and the third is the old dry-interior brown promoted into the rotation:
        //green, olive and brown mottle over the continents the way a satellite photograph reads. Three
        //matters structurally - see the fifth rule on GLOBE_WORLD: a two-ink dither fuses on BOTH diagonals,
        //so one release could take half a spine's cells; on three the fuse has one null diagonal only, and
        //the spines are too narrow for it to run anywhere.
        private static readonly BallType[] GLOBE_LAND = { BallType.Type2, BallType.Type13, BallType.Type10 };
        private static readonly BallType[] GLOBE_ICE = { BallType.Type4 };

        //The water between the polar floes, and it is a DIFFERENT water from the ocean on purpose - black,
        //the dark arctic sea between pack ice, an ink the rest of the level never uses. See GLOBE_WORLD's
        //fourth rule for what it prevents; white-on-black is the block's own proven pairing (Ghost's eyes,
        //Cabinet's bezel), where silver next to the floes' white is a listed confusable pair.
        private static readonly BallType[] GLOBE_POLAR = { BallType.Type8 };

        /// <summary>
        /// The world, sixteen columns of longitude by <b>fourteen</b> rows of latitude — one row per level of
        /// the layout, which is the first thing to check if the depth is ever changed: drawn twelve rows deep
        /// against a fourteen-level globe, both polar rows fell off the end of the bitmap and the south cap
        /// came out as open ocean. North first: <c>#</c> is land, <c>*</c> ice, <c>o</c> the polar water
        /// between the floes and anything else is sea. Five rules drew it and all five are the lattice's
        /// rather than geography's.
        /// <list type="bullet">
        /// <item><b>Nothing is narrower than two columns or shorter than two rows</b>, because the ground
        /// dithers on 2×2 blocks of the map: a one-column cape would be half a block, which near the poles is
        /// a single ball and a job for the repair pass.</item>
        /// <item><b>Both ice caps are broken into floes with sea between them.</b> The top row IS the anchor —
        /// a disc of about twenty cells bonded to the glass — and one colour across it is a level that ends
        /// on the first lucky ball of that colour. Broken, most of the disc stands whatever is shot and the
        /// shell under it hangs on: measured, the ice's own best shot is 4 % and drops nothing with it. It
        /// reads as pack ice, which is what the Arctic looks like anyway.</item>
        /// <item><b>The ice is the one material here drawn in a single colour</b>, and that is the poles
        /// again rather than a preference. Dithered white and silver like everything else it did not survive
        /// the repair pass: where the sectors converge a 2×2 block of the map holds one cell, so measured,
        /// white came out at <i>zero</i> balls and twelve cells were recoloured — the drawing rewritten
        /// between the source and the file, which is the thing that pass exists to make visible.</item>
        /// <item><b>The water between the floes (<c>o</c>) is not the ocean (<c>.</c>), and the split is half
        /// of the #301 sag fix.</b> The sea's three colours band on <c>block = column/2 + row/2</c>, so every
        /// block on an anti-diagonal wears the same colour — and a cross-level lattice neighbour IS a diagonal
        /// in (x, z) (the trap <see cref="GHOST_BODY"/> records), so the ocean fuses into diagonal ribbons.
        /// While the polar water was ocean, those ribbons REACHED THE ANCHOR DISC: one underside ocean match
        /// released the pole's sea cells remotely — anchors lost with nothing orphaned — and 480 balls slid
        /// 4+ units through the line in one shot with the glass at rest. Polar water in its own ink means no
        /// match landed anywhere below can release a cell of the anchor level.</item>
        /// <item><b>The oceans are narrow and the land is wide, and that is the other half of the #301 fix —
        /// a colour fuse cannot run far inside a narrow region.</b> The first drawing's south was two thirds
        /// water, so its ocean ribbons ran 40 balls each; the probe still read 5 of 5 with the anchors held,
        /// dying at shots 5–9 to gradual unrolling — two ribbon matches left the southern shreds hanging on
        /// near-single-file chains, 3 units of stretch per shot, glass at rest. Redrawn, the two oceans are
        /// two column-pairs wide for most of their run, so a same-colour diagonal chain exits into land after
        /// two or three blocks and the biggest water match is a dozen balls; the two continents run pole to
        /// pole, four to ten columns wide, so the south hangs on land webs in three inks of which no single
        /// release can take the load path (the null diagonal of <c>column/2 + row</c> steps out of a narrow
        /// spine as fast as the sea's does).</item>
        /// </list>
        /// </summary>
        private static readonly string[] GLOBE_WORLD =
        {
            "o**oo**oo**oo**o",
            "o**oo**oo**oo**o",
            "..####..##..##..",
            "..####..##..##..",
            "..######..####..",
            "..######..####..",
            "..####....####..",
            "..####....####..",
            "..####....####..",
            "..####....####..",
            "..####..######..",
            "..####..######..",
            "o**oo**oo**oo**o",
            "o**oo**oo**oo**o",
        };

        //The arctic plateau: the top level is a flat cap of this radius rather than the sphere's own r 1.97
        //disc, because THE TOP LEVEL IS THE ANCHOR and the sphere's honest figure is nine cells - the probe
        //(#301) watched a single match leave 550 balls on four of them. At 3.2 the cap matches the width of
        //the course under it (the sphere at level 12 reaches 3.14), so the silhouette gains one flat course
        //at the very top and the glass holds ~2.5x the cells. The south pole is deliberately NOT widened:
        //cells there are dead weight at the level's lowest point, where clearance is the scarce figure.
        private const float GLOBE_POLE_CAP = 3.2f;

        private static bool GlobeShell(float r, int i, int depth)
        {
            if (i == depth - 1) return r <= GLOBE_POLE_CAP;

            float d = SphereDistance(r, i, depth);

            return d <= GLOBE_RADIUS && d >= GLOBE_RADIUS - GLOBE_SHELL;
        }

        private static BallType GlobeColour(float r, float ang, int i, int depth)
        {
            int column = SectorIndex(ang, 0f, GLOBE_SECTORS);
            int row = depth - 1 - i;
            int block = column / 2 + row / 2;

            return PixelAt(GLOBE_WORLD, column, row) switch
            {
                //Land rotates on its own stride - block + row/2 is column/2 + row, whose null diagonal
                //(down-right) exits a spine as fast as the sea's anti-diagonal does; on the shared block
                //the three inks would fuse down-left instead, no better and no worse, but land and sea
                //striding DIFFERENTLY keeps their nulls crossed, so no shot can ride both at once
                '#' => Band(block + row / 2, GLOBE_LAND),
                '*' => Band(block, GLOBE_ICE),
                'o' => Band(block, GLOBE_POLAR),
                _ => Band(block, GLOBE_SEA),
            };
        }

        #endregion
    }
}
