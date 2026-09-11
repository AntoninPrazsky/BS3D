using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>The Eruption</b>, block 8 of the campaign: its designs, and the helpers no other block's designs use, in
    /// the order <c>Program.cs</c> held them — the play order is <see cref="Main"/>'s, and the block's name, music
    /// and ball style are in the tables there. Split out of <c>Program.cs</c> in #386.
    /// </summary>
    internal static partial class Program
    {


        #region The eruption levels (#295)

        //THE TENTH BLOCK (in play order the eighth): five levels on the volcano, under the darkest dome (9),
        //in molten-crust balls, on the rock ballad. THE GLOW IS THE LOAD: the molten seams, collars, channels
        //and feeds are what everything hangs by — always at least two interleaved hot inks, never one — and
        //the cold basalt mass is what falls when they are cut, so reading where a level shines is reading
        //where it will break. That is a statement about the PALETTE MEANING MECHANICS, which no other block
        //makes: the Spectrum sweeps hue as the level's subject, the Eruption wires it to the load paths.
        //
        //AND EVERY LEVEL HAPPENED IN A DIRECTION. A volcano is built by flow, and flow has a bearing: the
        //flank is torn toward somewhere, the river runs downhill, the bombs rake downwind, the column leans
        //where the wind took its ash. Every design here carries its event's bearing in the shape — the first
        //block deliberately asymmetric about the axis the gun orbits — so part of reading a level is walking
        //round it to find where the event went.
        //
        //THE BLOCK'S ENGINEERING LAW, stated once and obeyed by all five (#288's design-time rule made a
        //block style): A DESIGNED BREAKAWAY IS ALWAYS THE LOWEST THING ON ITS OWN LOAD PATH, so releasing it
        //moves the cluster's lowest point UP. The sag probe's enemy is a remainder that hangs lower after a
        //cut; these five are shaped so the remainder cannot, which is what makes a block full of designed
        //drops measurably safe — every design's doc carries its own #288 sum and its measured probe reading.
        //
        //THE HOT INKS are the ember register (red 1, orange 9, yellow 7, and white 4 for the white-hot);
        //THE COLD ONES the basalt register (black 8, brown 10, olive 13, silver 11 for ash). A cold mass
        //dithers on at least THREE inks (the diagonal fuse, measured three times in #301) and a hot member
        //bands on at least TWO (no course of one colour — the anchor rule); where hot meets its own hue in a
        //sweep, the contact is designed away, never left to luck (#302's guard-course lesson).
        //
        //The arc job (#194): after the Nebula's void, this is the light coming back GEOLOGICALLY — the earth
        //glowing by itself — one step before the dawn hands back the light received, and two before the
        //Arcade's neon closes the campaign after dark (#300 put the two cities in day order). Ten blocks,
        //and this one ships at five levels, the size every block first shipped at (the BLOCKS table carries
        //the size; a #255-style second hang can double it later).

        #region Eruption level 1: Breach

        /// <summary>
        /// The Eruption's opener: a hollow basalt cone hanging crater-up — the player stands under the
        /// volcano looking up into its mouth — with one flank <b>torn open from rim to foot toward a single
        /// bearing</b>, and the block's whole statement in the wound: the tear's lip and the two seam
        /// courses GLOW, and the glow is what everything below them hangs by. It is the campaign's opening
        /// pyramid returned in black at the far end — <see cref="One"/> is a bright solid cone of walls, this
        /// is a dark hollow one of crust — and the rhyme is deliberate and stated, not a reuse: nothing about
        /// the structure repeats (One peels by walls; Breach guillotines by seams).
        /// <para>
        /// <b>The teach.</b> Two full courses of the shell are molten seams (<see cref="BREACH_SEAM_A"/>,
        /// <see cref="BREACH_SEAM_B"/>), each four long arcs in the two hot inks alternating — so no single
        /// ball can take a seam (the anchor rule arrived at hot), but clearing all four arcs of one seam
        /// guillotines every course below it: the block's designed multi-shot cut, taught on the gentlest
        /// budget in the block. Under the block's law the dropped flank is the lowest thing on its own load
        /// path, so every guillotine moves the cluster's lowest point up.
        /// </para>
        /// <para>
        /// <b>The structure.</b> The wall is two cells thick everywhere (#301's measured-safe section — a
        /// one-cell curved wall loses half its cross-level neighbours, Globe's lesson), the crater rim's top
        /// two courses are a complete annulus in the three cold inks (the anchor, never one colour), and the
        /// tear widens going DOWN from a hairline under the rim — breaches are flank events, so the ring
        /// that carries the level is whole by construction. Where the shell is torn open it stops being a
        /// closed loop and becomes a C (Ghost's springing shape), so the tear's two edges carry a
        /// three-cell-thick lip (<see cref="BREACH_LIP_WALL"/> — Bolt's three-not-two arriving on a shell),
        /// and the lip is hot: the freshly torn rock is where the mountain shows what it runs on.
        /// </para>
        /// <para>
        /// <b>#288 sum</b>: depth 14 in field 18 hangs with ~3.96 of clearance (Ghost's identical figures);
        /// 70 shots at a step of 16 buy four descents, 2.40, leaving ~1.56 — clear of the 1.00 allowance and
        /// of the 0.82 measured swing, before counting that both guillotines RAISE the low point (the probe's
        /// traces show the line jumping from +4 to +8 when a seam completes — the block's law on screen).
        /// </para>
        /// <para>
        /// Measured: 564 balls in 41 standing groups (1.71 shots a group, the block's gentlest — and gentler
        /// in play, both seams cascading), margin 1, nothing alone, 2 in pairs, 2 recoloured; anchors 68
        /// (8.3 each), anchor load 9.7 — the best-anchored hang in the block; best single shots 2–10 % (the
        /// 10 is one brown network where the dither's rare corner fuses two plates — measured, gate-clear,
        /// left). Sag probe: <b>1 of 5 losing orders, twice independently</b> (−1.07/−1.05, the same order's
        /// shot 15, a hairline past the allowance after three mid-band cuts with the glass at rest), the
        /// other four surviving the full 70 — the Pylon/Ghost shipped band, under the Saturn control's 2.
        /// </para>
        /// </summary>
        private static Design Breach() => new()
        {
            File = "Breach.json",
            Name = "Breach",
            Grid = BREACH_GRID,
            Depth = BREACH_DEPTH,
            FieldLevels = BREACH_FIELD_LEVELS,
            Scene = SceneKind.Volcano,
            Sky = 9,
            Music = MUSIC_VOLCANO,
            Balls = BALLS_VOLCANO,
            //Priced off the measured 41 groups at the block's gentlest ratio, 1.71 — and gentler in play
            //than the figure says, since both seam guillotines cascade (Pleat's caveat cuts the other way
            //here: the naive ratio undercounts a budget's generosity on a cascade design). The step is 16
            //and not Ghost's 14 because the budget is bigger: 70 shots at 14 buy five descents, 3.00 of the
            //~3.96 this hang starts with (Ghost's clearance arithmetic, same depth in the same field), and
            //0.96 of headroom is under the 1.00 swing allowance the Program header's sum demands. At 16 the
            //budget buys four, 2.40, leaving ~1.56 — Ghost's own shipped margin.
            Shots = 70,
            CeilingStep = 16,
            Occupied = BreachOccupied,
            Colour = BreachColour,
        };

        //THE CONE'S FIGURES. Grid 15 with a rim at 6.0 is Orrery's own margin-1 extent; depth 14 in field
        //18 is Ghost's framed-whole hang and Ghost's clearance arithmetic with it.
        private const byte BREACH_GRID = 15;
        private const byte BREACH_DEPTH = 14;
        private const byte BREACH_FIELD_LEVELS = 18;

        //The shell: outer radius tapering 0.26 a course from the rim's 6.0 (the foot lands at 2.62, a
        //near-disc), the wall two cells at either parity — the section #301 measured as the safe curved
        //wall (GHOST_OUTER minus GHOST_INNER; one cell loses half its cross-level neighbours on a sphere).
        private const float BREACH_RIM_OUTER = 6.0f;
        private const float BREACH_TAPER = 0.26f;
        private const float BREACH_WALL = 2.0f;

        //The crater rim: the top two courses stay a COMPLETE annulus whatever the tear does below - the
        //anchor is a ring, and breaches are flank events. Two courses and not one so the ring is a band,
        //not a line of cells.
        private const int BREACH_RIM_COURSES = 2;

        //The molten seams: two full courses of the shell, the block's guillotines. At 4 and 9 they cut the
        //cone into three cold bands of 2-4 courses each - no chain of shell courses longer than four hangs
        //between hot cuts, which is the Cabinet-depth lesson applied at the drawing board.
        private const int BREACH_SEAM_A = 4;
        private const int BREACH_SEAM_B = 9;

        //A seam is FOUR long arcs, the two hot inks alternating: clearing one ink is two shots and leaves
        //the other two arcs holding everything below (the Arcade cap rule, hot); the full guillotine is an
        //earned four. Four and not eight so an arc is a shot worth taking (~12 balls), four and not two so
        //no single ball ever takes half a seam.
        private const int BREACH_SEAM_ARCS = 4;

        //The tear: on +X (a quarter-turn from the gun's +Z start, so the opener is read the block's way -
        //walk to find where the event went), a hairline under the rim widening 0.05 rad a course to a ~34
        //degree half-angle at the foot. ⚠ 0.05 and not the 0.07 first drawn, measured by the probe: at a
        //96-degree foot bite the shell below a mid-band release was a 264-degree C that SWUNG - a 41-ball
        //plate cut with nothing orphaned took the line from +4.3 to -1.08 in one shot, three orders of
        //five - where at 68 degrees the ring stays closed enough that the same cut reads -0.6. The wound
        //is the statement, but the ring is the structure, and the ring wins the trade.
        private const float BREACH_BEARING = 0f;
        private const int BREACH_TEAR_FROM = BREACH_RIM_COURSES;
        private const float BREACH_TEAR_STEP = 0.05f;

        //The lip: for a third of a radian either side of the tear the wall thickens to three cells and turns
        //hot. Structure and statement in one figure: the tear makes the shell a C and a C springs (Ghost's
        //hem), so the free edges are braced Bolt's way - three, not two - and the brace is the glowing
        //fresh-torn rock that teaches which balls carry the level.
        private const float BREACH_LIP = 0.35f;
        private const float BREACH_LIP_WALL = 3.0f;

        //The cold crust: eight sectors of arc against three basalt inks, the band index advancing one per
        //sector and one per TWO courses - the #301 rule (a two-entry dither welds through the cross-level
        //diagonal; three entries leave only the rare corner). Both figures are the probe's, from opposite
        //failures: at 12 sectors the foot's blocks were 1.3 cells wide and the level read 59 groups on the
        //budget (0.98 a group, the tool's own stated floor) with 14 balls in pairs - confetti; at three
        //courses a slab the plates grew to 41 balls, and a 41-ball cut with nothing orphaned swung the
        //torn shell 5.4 units in one shot, three orders of five. Eight sectors by two courses is the plate
        //a shot is worth spending on that the ring can also afford to lose.
        private const int BREACH_SECTORS = 8;
        private static readonly BallType[] BREACH_COLD = { BallType.Type8, BallType.Type10, BallType.Type13 };   //black, brown, olive
        private static readonly BallType[] BREACH_HOT = { BallType.Type1, BallType.Type9 };                      //red, orange

        /// <summary>The tear's half-angle at a course: zero through the rim, then widening as it descends.</summary>
        private static float BreachTearHalf(int d) =>
            d < BREACH_TEAR_FROM ? 0f : (d - BREACH_TEAR_FROM + 1) * BREACH_TEAR_STEP;

        private static bool BreachOccupied(float r, float ang, int i, int depth)
        {
            int d = LevelsBelowGlass(i, depth);

            float off = MathF.Abs(WrapAngle(ang - BREACH_BEARING));
            if (off < BreachTearHalf(d)) return false;   //the bite

            float outer = BREACH_RIM_OUTER - d * BREACH_TAPER;
            float wall = d >= BREACH_TEAR_FROM && off < BreachTearHalf(d) + BREACH_LIP
                ? BREACH_LIP_WALL
                : BREACH_WALL;

            return r <= outer && r >= outer - wall;
        }

        private static BallType BreachColour(float r, float ang, int i, int depth)
        {
            int d = LevelsBelowGlass(i, depth);

            //The seams first: a seam course is hot along its whole ring, lip zone included - it is the
            //member that carries everything below it, so nothing on it may be cold. The second seam's arcs
            //roll an eighth of a turn so the two guillotines' cuts never stack vertically.
            if (d == BREACH_SEAM_A || d == BREACH_SEAM_B)
                return BREACH_HOT[SectorIndex(ang, d == BREACH_SEAM_A ? 0f : 1f / 8f, BREACH_SEAM_ARCS) % 2];

            //The lip: hot by two-course bands, so each ink's lip run is a slab and not a thread - at course
            //parity the foot's short lip runs measured as pairs the repair pass rewrote
            float off = MathF.Abs(WrapAngle(ang - BREACH_BEARING));
            if (d >= BREACH_TEAR_FROM && off < BreachTearHalf(d) + BREACH_LIP) return BREACH_HOT[d / 2 % 2];

            return Band(SectorIndex(ang, 0f, BREACH_SECTORS) + d / 2, BREACH_COLD);
        }

        #endregion

        #region Eruption level 2: Causeway

        /// <summary>
        /// The Giant's Causeway inverted: twelve basalt column bundles hanging off the glass, each a stack of
        /// the lattice's own cannonball packing — a course of five cells nesting into a course of four, all
        /// the way down — so the packing the whole game is built on is the drawn subject, visible on every
        /// column (the owner's recorded ask, finally a level's whole body). The bundles hang at stepped
        /// depths, lengthening along +X — the bearing the flow that cooled into them ran — so the underside
        /// is a staircase skyline read in one glance, and the slots between bundles are read-ahead air.
        /// <para>
        /// <b>Every bundle is its own load path and its own designed drop, which is the block's law verbatim.</b>
        /// Bundles never touch (axes 3.0 apart against a reach of <see cref="CAUSEWAY_R"/>), each bonds to
        /// the glass through its own top course, and each wears the block's colour law literally: a two-level
        /// glowing collar at the glass (the melt it hangs from — red and orange, alternating per course and
        /// swapping order per bundle) over a shaft of cold basalt banded two levels at a stroke. Cut a collar
        /// course and that bundle drops whole; cut a shaft band and the bundle below it goes — either way the
        /// release is the lowest thing on its own path, so the cluster's lowest point only ever moves up.
        /// </para>
        /// <para>
        /// The shaft pairs rotate through three cold inks (<see cref="CAUSEWAY_SHAFT_PAIRS"/>), so the level
        /// dithers on three as the block header demands — and the diagonal fuse cannot arise at all, because
        /// no two bundles share a cell border anywhere. The gun eats the level bottom-up along the bearing:
        /// the aim band holds shots to the underside, so the short western bundles' collars come into reach
        /// only as the deep eastern ranks are felled — the flow direction is the play direction.
        /// </para>
        /// <para>
        /// Measured: 440 balls in 60 standing groups (the print reads 0.87 shots a group and the floor's own
        /// exception applies — see the Shots comment), 54 ceiling anchors carrying 8.1 each, anchor load 8.7
        /// at the worst single shot, margin 1, nothing alone, nothing in pairs, 0 recoloured; counts 57–115
        /// and best single shots 8–13 % (the deepest bundle whole). Clearance 5.38 over the line (Cube's own
        /// depth-12-in-18 figure). <b>Sag probe: 0 of 5 losing orders, every order clearing the level</b>
        /// (worst at shot 50 of 52), the line never nearer than 3.54 — the block's law doing exactly what it
        /// says, every release raising the lowest point. First priced 46 shots, and the probe's worst order
        /// ran out with 8 balls standing; 52 is the measured correction.
        /// </para>
        /// </summary>
        private static Design Causeway() => new()
        {
            File = "Causeway.json",
            Name = "Causeway",
            Grid = CAUSEWAY_GRID,
            Depth = CAUSEWAY_DEPTH,
            FieldLevels = CAUSEWAY_FIELD_LEVELS,
            Scene = SceneKind.Volcano,
            Sky = 9,
            Music = MUSIC_VOLCANO,
            Balls = BALLS_VOLCANO,
            //Priced off the probe rather than off the ratio print: 60 standing groups against any sane
            //budget reads under the tool's 1.00 floor (0.87), and that floor's own text excepts exactly this
            //shape — a level built of cascades (Gantry's case; the Pleat's #302 caveat) where one band shot
            //drops everything below it in the bundle, so the real cost is ~2 shots a bundle plus the misses.
            //46 was the first pricing and the probe's worst order ran out with 8 balls standing; 52 clears
            //it. The #288 sum: clearance at depth 12 in field 18 is Cube's own figure (5.38 over the line),
            //52 shots at a step of 8 buy six descents of 0.60 = 3.60, leaving 1.78 — clear of the 1.00
            //allowance with a bundle's own swing on top.
            Shots = 52,
            CeilingStep = 8,
            OccupiedBlock = (x, z, i, depth) => CausewayBundle(x, z, i, depth) != 0,
            BlockColour = CausewayColour,
        };

        //The bundle field. Twelve axes in four ranks along +X, lengths ascending with the rank — the flow
        //bearing — and staggered in Z so the slots between bundles never align into one empty corridor.
        //Axes sit 3.0 apart where bundles neighbour, against a reach of CAUSEWAY_R = 1.3: two axes closer
        //than 2.6 could claim one cell for both bundles and weld their load paths, and 3.0 leaves a clear
        //slot besides. 1.3 is Rope's own strand radius (#207), kept for the same reason it worked there:
        //it cuts a five-cell course on the unshifted levels and the four pocket cells on the shifted ones,
        //which is the cannonball nesting drawn at its smallest legible size.
        private const byte CAUSEWAY_GRID = 15;
        private const byte CAUSEWAY_DEPTH = 12;
        private const byte CAUSEWAY_FIELD_LEVELS = 18;
        private const float CAUSEWAY_R = 1.3f;

        private static readonly (float X, float Z, int Levels)[] CAUSEWAY_BUNDLES =
        {
            (-4.5f, -3.5f, 4), (-4.5f, 0.5f, 5), (-4.5f, 4f, 4),      //the young flow: stubs
            (-1.5f, -4.5f, 6), (-1.5f, -1.5f, 6), (-1.5f, 2.5f, 7),   //waist-deep
            (1.5f, -3f, 10), (1.5f, 0.5f, 9), (1.5f, 4f, 8),          //shoulder-deep
            (4.5f, -4.5f, 9), (4.5f, -1.5f, 12), (4.5f, 2.5f, 11),    //the old flow: full columns
        };

        //The collar (two courses of melt at the glass) and the shaft bands (two courses of basalt a stroke).
        //Two levels a band because one course of the packing is 4 or 5 balls and a band has to survive
        //MIN_GROUP whatever the parity deals it; the collar's two courses take the two hot inks one each,
        //swapping order per bundle so the anchor level reads red-orange-red across the field.
        private const int CAUSEWAY_COLLAR = 2;
        private const int CAUSEWAY_BAND = 2;

        private static readonly BallType[] CAUSEWAY_HOT = { BallType.Type1, BallType.Type9 };   //red, orange

        //Shaft pairs rotate per bundle through three cold inks, so neighbouring ranks read as different
        //stone and the level carries the block's three-ink rule even though no fuse is possible here
        private static readonly BallType[][] CAUSEWAY_SHAFT_PAIRS =
        {
            new[] { BallType.Type8, BallType.Type10 },    //black / brown
            new[] { BallType.Type10, BallType.Type13 },   //brown / olive
            new[] { BallType.Type13, BallType.Type8 },    //olive / black
        };

        /// <summary>Which bundle a cell belongs to, 1-based, or 0 for air. Bundles are disjoint by the
        /// spacing rule on <see cref="CAUSEWAY_BUNDLES"/>, so first match is the only match.</summary>
        private static int CausewayBundle(int x, int z, int i, int depth)
        {
            Centred(x, z, i, CAUSEWAY_GRID, out float dx, out float dz);

            int d = LevelsBelowGlass(i, depth);

            for (int b = 0; b < CAUSEWAY_BUNDLES.Length; b++)
            {
                (float bx, float bz, int levels) = CAUSEWAY_BUNDLES[b];

                if (d < levels && (dx - bx) * (dx - bx) + (dz - bz) * (dz - bz) <= CAUSEWAY_R * CAUSEWAY_R)
                    return b + 1;
            }

            return 0;
        }

        private static BallType CausewayColour(int x, int z, int i)
        {
            int b = CausewayBundle(x, z, i, CAUSEWAY_DEPTH) - 1;
            int d = LevelsBelowGlass(i, CAUSEWAY_DEPTH);

            //The collar: one hot ink a course, the order flipping with the bundle so no ink owns the anchor
            if (d < CAUSEWAY_COLLAR) return CAUSEWAY_HOT[(b + d) % 2];

            return Band((d - CAUSEWAY_COLLAR) / CAUSEWAY_BAND, CAUSEWAY_SHAFT_PAIRS[b % 3]);
        }

        #endregion

        #region Eruption level 3: Meander

        /// <summary>
        /// The glowing river, read in PLAN — the first winding plan-form in the campaign: an S of hot
        /// channel crossing the whole field, cold levee walls flanking it, pale stone weirs barring it into
        /// course-shaped bites. The block's bearing is DOWNHILL along the run: the channel deepens one
        /// course past every weir, so the glow steps lower segment by segment and the level's low end says
        /// which way the river flows.
        /// <para>
        /// <b>The load relation is the block's statement verbatim: the river is the anchor.</b> Only the
        /// channel's top course (and the weirs' crossbars) touch the glass — the levees hang off the
        /// channel's flanks through the lattice's diagonal reach and off the weirs' bars, so the glow is
        /// literally what the cold mass hangs by. Cutting one ink of a segment releases that ink's run;
        /// cutting the segment's second ink guillotines the segment and the levee walls it fed — a designed
        /// two-shot cut, and everything it drops is the lowest thing on its own load path (the block's law):
        /// the neighbouring segments keep their own anchors and nothing left behind hangs lower.
        /// </para>
        /// <para>
        /// <b>The weirs are #302's banded tier arriving as diegetic architecture</b>: silver/white bars a
        /// single column thick, spanning the channel and both banks at every depth the river reaches there,
        /// plus the one place the banks touch the glass directly. They sever every hot ink by construction
        /// (no channel colour crosses a weir), quarter the levees' spans, and read as the dams a lava river
        /// would actually crust over.
        /// </para>
        /// <para>
        /// Measured: 356 balls in 36 standing groups (1.44 shots a group at the budget of 52), 59 ceiling
        /// anchors (6.0 each, anchor load 7.0 — the river's whole top course is glass-bonded, so this is
        /// one of the best-anchored levels in the pack), margin 1, nothing alone, 4 in pairs, 7 recoloured
        /// (bank-block corners at the bends; Donut ships with 9); best single shots 2–6 %. Sag probe:
        /// <b>0 of 5 losing orders — every order CLEARS the level</b> in 18–26 shots of the 52, the line
        /// never crossed (closest +3.12), the guillotine cascades visible in the trace (one segment cut
        /// orphaned 113 balls of bank, exactly the designed bite).
        /// </para>
        /// </summary>
        private static Design Meander() => new()
        {
            File = "Meander.json",
            Name = "Meander",
            Grid = MEANDER_GRID,
            Depth = MEANDER_DEPTH,
            FieldLevels = MEANDER_FIELD_LEVELS,
            Scene = SceneKind.Volcano,
            Sky = 9,
            Music = MUSIC_VOLCANO,
            Balls = BALLS_VOLCANO,
            Shots = 52,
            //Priced off the measured 36 standing groups: 52 is 1.44 a group, the mid-block seat, and the
            //real game is cheaper still because the segments cascade (a guillotined segment takes its banks).
            //#288's sum, done at design time as the block header demands: the layout is 6 courses in a field
            //of 18, so the lowest ball starts ~8.5 over the line; 52 shots at a step every 8 buy 6 descents,
            //3.60 of it, leaving ~4.9 — clear of the 1.00 allowance several times over, which is what a
            //pancake owes its two tall neighbouring chapters.
            CeilingStep = 8,
            OccupiedBlock = (x, z, i, depth) => MeanderOccupied(x, z, i),
            BlockColour = MeanderColour,
        };

        //THE MEANDER'S OWN FIGURES. Six courses in a field of 18 — the offset is 12 and even, the block's
        //pancake after two tall chapters, framed whole with eleven levels of clearance under it.
        private const byte MEANDER_GRID = 15;
        private const byte MEANDER_DEPTH = 6;
        private const byte MEANDER_FIELD_LEVELS = 18;

        //The run: dz spans [-RUN_HALF, +RUN_HALF] and the centreline is one full sine period across it —
        //two bends, an S. ⚠ Amplitude 1.8 and not 2.2, and the reason is the slope: the ribbon's x-extent
        //is its perpendicular half-width TIMES sqrt(1 + slope^2), so at 2.2 the inflection swung the outer
        //bank onto the field wall (the gate read margin NONE) even though the ribbon itself is only 3.5
        //half-wide. The bend still reads — the amplitude has to beat the channel's half-width to read as a
        //bend at all, and 1.8 against 1.6 does.
        private const float MEANDER_RUN_HALF = 5.5f;
        private const float MEANDER_AMPLITUDE = 1.8f;

        //The channel and its banks, as half-widths measured PERPENDICULAR to the curve — not along x. The
        //centreline's slope reaches 1.26 at the inflection, and a raw |dx - f(dz)| there fattens the ribbon
        //by two thirds: the bends would come out bloated and the S would read as a blob, so the distance is
        //divided by sqrt(1 + slope^2). Crane's and Bridge's polyline machinery, on a single analytic bend.
        private const float MEANDER_CHANNEL_HALF = 1.6f;
        private const float MEANDER_LEVEE_WIDTH = 1.9f;

        //How many courses of channel the FIRST segment runs; every weir passed adds one (the downhill
        //step). The levees reach one course below the local channel bottom, so the cold walls read as banks
        //standing proud of the glow between them when the level is read from the gun, underneath.
        private const int MEANDER_HEAD_COURSES = 2;

        //The weirs: centres on quarter-cell offsets so no cell of either parity ever lands on the window's
        //edge (PolarBlock's quarter-cell rule arriving on a polyline — a boundary exactly on a row of cells
        //lets float noise decide the column), and the window catches exactly one column per parity.
        private static readonly float[] MEANDER_WEIR_DZ = { -2.75f, 0.25f, 3.25f };
        private const float MEANDER_WEIR_HALF = 0.3f;

        //Each segment's two hot inks, indexed by how many weirs the run has passed. Adjacent segments never
        //matter for grouping — a weir stands between them, so a shared ink is two groups by construction —
        //but the pairs still rotate so the river visibly changes register as it descends.
        private static readonly BallType[][] MEANDER_SEGMENT_PAIRS =
        {
            new[] { BallType.Type1, BallType.Type9 },   //red, orange - the head
            new[] { BallType.Type7, BallType.Type1 },   //yellow, red
            new[] { BallType.Type9, BallType.Type7 },   //orange, yellow
            new[] { BallType.Type1, BallType.Type9 },   //red, orange - the mouth
        };

        //The banks' basalt, three inks against the diagonal fuse (#301, measured three times).
        private static readonly BallType[] MEANDER_LEVEE_INKS =
        {
            BallType.Type8, BallType.Type10, BallType.Type13,   //black, brown, olive
        };

        /// <summary>
        /// The one geometry read both occupancy and colour take, so the two cannot disagree: where the cell
        /// stands relative to the S (perpendicular distance), how many weirs the run has passed there
        /// (<paramref name="seg"/>, which is also the downhill step count), whether the cell is on a weir
        /// bar, and the along-run cell index the channel's banding alternates on.
        /// </summary>
        private static bool MeanderCell(int x, int z, int i,
            out bool weir, out bool channel, out int seg, out int runCell, out int d)
        {
            d = LevelsBelowGlass(i, MEANDER_DEPTH);
            Centred(x, z, i, MEANDER_GRID, out float dx, out float dz);

            weir = false; channel = false; seg = 0; runCell = 0;

            if (MathF.Abs(dz) > MEANDER_RUN_HALF) return false;

            float phase = MathF.PI * dz / MEANDER_RUN_HALF;
            float slope = MEANDER_AMPLITUDE * MathF.PI / MEANDER_RUN_HALF * MathF.Cos(phase);
            float perp = MathF.Abs(dx - MEANDER_AMPLITUDE * MathF.Sin(phase))
                         / MathF.Sqrt(1f + slope * slope);

            foreach (float w in MEANDER_WEIR_DZ)
            {
                if (dz > w + MEANDER_WEIR_HALF) seg++;
                if (MathF.Abs(dz - w) <= MEANDER_WEIR_HALF) weir = true;
            }

            runCell = (int)MathF.Floor(dz + MEANDER_RUN_HALF);

            int channelCourses = MEANDER_HEAD_COURSES + seg;

            //The weir bar: the full ribbon width, every course the local river reaches, and - alone in the
            //level - the banks' own span of the anchor course, which is the second load path the banks get
            if (weir) return perp <= MEANDER_CHANNEL_HALF + MEANDER_LEVEE_WIDTH && d <= channelCourses;

            if (perp <= MEANDER_CHANNEL_HALF)
            {
                channel = true;
                return d < channelCourses;
            }

            //The banks: beside the channel from one course below the glass down to one course below the
            //local channel bottom, so the cold underside tracks the river's descent
            return perp <= MEANDER_CHANNEL_HALF + MEANDER_LEVEE_WIDTH && d >= 1 && d <= channelCourses;
        }

        private static bool MeanderOccupied(int x, int z, int i) =>
            MeanderCell(x, z, i, out _, out _, out _, out _, out _);

        private static BallType MeanderColour(int x, int z, int i)
        {
            MeanderCell(x, z, i, out bool weir, out bool channel, out int seg, out int runCell, out int d);

            //Pale cooled stone in two vertical halves - silver above the waterline, white below it - so a
            //weir is two connected slabs rather than a stack of one-course groups: banded by course it
            //measured a dozen five-ball groups across the three bars, a third of the budget spent on dams
            if (weir) return d < 2 ? BallType.Type11 : BallType.Type4;

            //The river: the segment's pair in ribbons two courses deep - the upper ribbon carries the
            //anchor and the guillotine (cut it and the segment goes with its banks, the designed two-shot
            //bite), the lower is the cheap taste of it. Two finer alternations were measured first: per
            //cell read 63 standing groups on 417 balls and 22 repair rewrites, per 2x2 block still 47 on
            //356 with a 0.94 ratio - a river this short fragments under any banding finer than its own
            //depth ribbons, and the segment IS the group the level is played in.
            if (channel) return MEANDER_SEGMENT_PAIRS[seg][(d / 2) % 2];

            return Band(x / 3 + z / 3 + d / 2, MEANDER_LEVEE_INKS);
        }

        #endregion

        #region Eruption level 4: Volley

        /// <summary>
        /// The sky mid-eruption: a broad ash cloud spanning the glass, and hanging under it a raked field of
        /// <b>nine volcanic bombs</b> — fat teardrops of about thirty balls, the cannonball packing readable
        /// on every one — each on a glowing neck. <b>The anchor IS the cloud</b>: every ball of the sheet's
        /// top course bonds to the glass wall to wall, so nothing in the level hangs off anything narrower
        /// than the sky.
        /// <para>
        /// <b>The bearing is the wind, and it is written twice</b> (the block's direction rule): the cloud
        /// thickens downwind — one course at the upwind edge, three at the downwind one, the ash still
        /// arriving where the wind carries it — and the bombs hang lower the further downwind their column
        /// sits, because they fell through more cloud. One glance across the level reads the wind.
        /// </para>
        /// <para>
        /// <b>The glow is the load, verbatim</b>: each bomb hangs by a three-level neck of red and yellow —
        /// the two inks split along the wind axis, so neither alone can be the link — and the block's law
        /// holds by construction: a bomb is the lowest thing on its own path, every release moves the
        /// cluster's lowest point up. The designed play is two shots into a neck (red, then yellow, or the
        /// other way) to drop a whole bomb; a shot into the bomb's dark body takes only the body, and a shot
        /// into the cloud where it hangs deepest takes an ash block and whatever necks rooted in it — the
        /// cloud sagging its bombs out is the level's one big spectacle, priced below.
        /// </para>
        /// <para>
        /// <b>The neck is a collar, not a two-cell stalk, and its two figures were both set by lone-ball
        /// arithmetic that had to be measured twice</b>: a stalk two cells across gives each ink one ball a
        /// level (lone balls the repair pass rewrites — the drawing quietly changed between the source and
        /// the file), and at a radius of 1.0 the halves' cells sit on opposite fractional sides at the two
        /// level parities, so a half's balls were not each other's cross-level neighbours and the tool read
        /// ten balls in pairs. At <see cref="VOLLEY_NECK"/> (1.2) the shifted levels gain their corner
        /// cells, every half is one vertically connected group of about ten, and the pack reads zero pairs.
        /// </para>
        /// <para>
        /// The ash dithers on <b>three</b> cold inks in column-blocks (<see cref="VOLLEY_ASH_BLOCK"/> cells,
        /// level-independent, so a block is one vertical piece): three against the diagonal fuse (#301,
        /// measured three times), and column-blocks rather than per-level blocks deliberately — a fused
        /// ash ribbon reaching across the underside would carry several necks at once, where a column-block
        /// carries at most one. Silver, black and brown: ash grey, scorch and burnt earth, the register the
        /// molten-crust balls read the seams against. The bomb bodies take one cold ink per wind column
        /// (black, brown, olive upwind to downwind) — a body is ONE deliberate group, the Orrery-pin
        /// precedent, not a dither, so the fuse rule does not apply to it and the body shot is a priced
        /// ~25-ball drop.
        /// </para>
        /// <para>
        /// <b>The #288 sum, per bomb and for the level</b>: the field hangs the layout's eight levels in
        /// eighteen, so the lowest tip starts ten empty levels — 7.07 — over the line; a downwind bomb
        /// released whole falls free (released, not hanging), and the deepest HANGING mass after any cut is
        /// a neighbouring bomb's tip, which the cut only ever raises. Budget: 52 shots at a step of 8 buys
        /// six descents, 3.60, leaving 3.47 of headroom — over three times the allowance.
        /// </para>
        /// <para>
        /// Measured: 531 balls in 36 standing groups (1.44 shots a group against the budget of 52), margin
        /// 1, nothing alone, nothing in pairs, 0 recoloured; 121 ceiling anchors carrying 4.4 each, anchor
        /// load 4.8 — the smallest in the game but the flat teaching levels, which is the cloud doing its
        /// job. Best single shots: the necks' halves 1 % each, a bomb body 3–4 %, and the designed
        /// spectacle — an ash block with a neck rooted in it, the bomb following — 11–15 %. Sag probe:
        /// <b>0 of 5 losing orders, every order clearing the level</b> (worst at shot 22 of 52), and the
        /// closest the line was ever approached is 7.57 — the block's law doing exactly what it claims,
        /// since a level whose every release is its own lowest mass has nothing left to stretch.
        /// </para>
        /// </summary>
        private static Design Volley() => new()
        {
            File = "Volley.json",
            Name = "Volley",
            Grid = VOLLEY_GRID,
            Depth = VOLLEY_DEPTH,
            FieldLevels = VOLLEY_FIELD_LEVELS,
            Scene = SceneKind.Volcano,
            Sky = 9,
            Music = MUSIC_VOLCANO,
            Balls = BALLS_VOLCANO,
            Shots = 52,
            CeilingStep = 8,
            OccupiedBlock = VolleyOccupied,
            BlockColour = VolleyColour,
        };

        //THE VOLLEY'S OWN FIGURES. Eight levels in a field of eighteen — the layout hangs high, and the ten
        //empty levels under it (7.07) are what makes a level of designed drops safe: nothing that hangs can
        //reach the line, and nothing that drops is hanging any more.
        private const byte VOLLEY_GRID = 15;
        private const byte VOLLEY_DEPTH = 8;
        private const byte VOLLEY_FIELD_LEVELS = 18;

        //The cloud: a square sheet at the glass, wall to wall less the lateral margin. Its top course is the
        //level's whole anchor.
        private const float VOLLEY_SHEET_HALF = 5.6f;

        //The wind, in cloud thickness: one course upwind of -VOLLEY_SPACING/2, two through the middle, three
        //downwind of +VOLLEY_SPACING/2 — the thresholds are the bomb columns' own boundaries, so each column
        //of bombs hangs under its own thickness of ash.
        private const int VOLLEY_SHEET_MIN = 1;
        private const int VOLLEY_SHEET_MAX = 3;

        //The bombs: a three-by-three field of spindles, centres VOLLEY_SPACING apart in the centred frame,
        //the wind running +X. 4.2 keeps two clear columns between neighbouring waists at either parity, so
        //no bomb ever braces another and each is the lowest thing on its own load path (the block's law).
        private const int VOLLEY_BOMBS_PER_SIDE = 3;
        private const float VOLLEY_SPACING = 4.2f;

        //One teardrop, top down: three levels of glowing neck, two of waist, no nose — a falling drop, and
        //the shape that keeps every bomb inside the layout's eight levels at all three hang depths. The
        //neck's radius is 1.2 and not a two-cell stalk — see the design doc for the two rounds of lone-ball
        //arithmetic that set it.
        private const int VOLLEY_NECK_LEVELS = 3;
        private const int VOLLEY_WAIST_LEVELS = 2;
        private const float VOLLEY_NECK = 1.2f;
        private const float VOLLEY_WAIST = 1.7f;

        //How coarse the ash dithers, in cells of x and z (level-independent — a block is a vertical piece).
        private const int VOLLEY_ASH_BLOCK = 5;

        private static readonly BallType[] VOLLEY_ASH = { BallType.Type11, BallType.Type8, BallType.Type10 };   //silver, black, brown

        //The bodies' one cold ink per wind column, upwind to downwind — the crust darkening as it cools in
        //flight reads the bearing a third time.
        private static readonly BallType[] VOLLEY_BODY = { BallType.Type8, BallType.Type10, BallType.Type13 };  //black, brown, olive

        private const BallType VOLLEY_NECK_HOT = BallType.Type1;     //red, the upwind half of every neck
        private const BallType VOLLEY_NECK_COOL = BallType.Type7;    //yellow, the downwind half

        /// <summary>How many courses of cloud hang over the wind position <paramref name="dx"/>.</summary>
        private static int VolleySheetDepth(float dx) =>
            dx < -VOLLEY_SPACING * HALF ? VOLLEY_SHEET_MIN
            : dx <= VOLLEY_SPACING * HALF ? VOLLEY_SHEET_MIN + 1
            : VOLLEY_SHEET_MAX;

        /// <summary>
        /// Which bomb the cell belongs to and where on it: the return is 0 for none, else the wind column
        /// 1..3, with <paramref name="neck"/> saying whether the cell is on the glowing neck (against the
        /// body), and <paramref name="windSide"/> the cell's side of the bomb's own wind axis (the neck's
        /// two-ink split). A bomb's five levels start one course under its column's cloud, so the downwind
        /// bombs hang lower — the bearing's second reading.
        /// </summary>
        private static int VolleyBomb(float dx, float dz, int d, out bool neck, out bool windSide)
        {
            neck = false;
            windSide = false;

            for (int col = 0; col < VOLLEY_BOMBS_PER_SIDE; col++)
            {
                float bx = (col - 1) * VOLLEY_SPACING;
                int top = VOLLEY_SHEET_MIN + col;   //the column's cloud is col+1 courses; the bomb starts under it

                if (d < top || d >= top + VOLLEY_NECK_LEVELS + VOLLEY_WAIST_LEVELS) continue;

                for (int row = 0; row < VOLLEY_BOMBS_PER_SIDE; row++)
                {
                    float bz = (row - 1) * VOLLEY_SPACING;
                    float lx = dx - bx;
                    float lz = dz - bz;

                    float reach = d < top + VOLLEY_NECK_LEVELS ? VOLLEY_NECK : VOLLEY_WAIST;

                    if (lx * lx + lz * lz > reach * reach) continue;

                    neck = d < top + VOLLEY_NECK_LEVELS;
                    windSide = lz < 0f;
                    return col + 1;
                }
            }

            return 0;
        }

        private static bool VolleyOccupied(int x, int z, int i, int depth)
        {
            Centred(x, z, i, VOLLEY_GRID, out float dx, out float dz);

            int d = depth - 1 - i;

            if (MathF.Abs(dx) <= VOLLEY_SHEET_HALF && MathF.Abs(dz) <= VOLLEY_SHEET_HALF
                && d < VolleySheetDepth(dx))
                return true;

            return VolleyBomb(dx, dz, d, out _, out _) != 0;
        }

        private static BallType VolleyColour(int x, int z, int i)
        {
            Centred(x, z, i, VOLLEY_GRID, out float dx, out float dz);

            int d = VOLLEY_DEPTH - 1 - i;

            //The cloud first: its blocks are level-independent columns, so an ash block is one piece from
            //the anchor down and a shot into it takes that piece and whatever neck rooted there — at most one
            if (MathF.Abs(dx) <= VOLLEY_SHEET_HALF && MathF.Abs(dz) <= VOLLEY_SHEET_HALF
                && d < VolleySheetDepth(dx))
                return Band(x / VOLLEY_ASH_BLOCK + z / VOLLEY_ASH_BLOCK, VOLLEY_ASH);

            int column = VolleyBomb(dx, dz, d, out bool isNeck, out bool windSide);

            if (isNeck) return windSide ? VOLLEY_NECK_HOT : VOLLEY_NECK_COOL;

            return VOLLEY_BODY[column - 1];
        }

        #endregion

        #region Eruption level 5: Plume

        /// <summary>
        /// The finale: the eruption column entire, and the block's vocabulary composed. A broad <b>ash
        /// umbrella</b> spreads at the field's top — two levels of three-ink ash, the block's best anchor and
        /// diegetically the cloud the mountain built — with the <b>trunk</b> rising to its centre, cold
        /// basalt cut by two glowing <b>hoop tiers</b>, and five <b>fallout arcs</b> hanging off the
        /// umbrella's underside in a fan on the downwind side only, each shearing further downwind as it
        /// descends and thickening into a black <b>bomb</b> at its tip. The bearing is the block's law of
        /// direction at its plainest: the fallout rakes downwind of the vent, so the umbrella's +X rim
        /// carries five glowing sockets and its −X rim carries nothing, and the level is read by orbiting to
        /// the side the eruption threw.
        /// <para>
        /// <b>The glow is the load, told three ways.</b> Each arc hangs from a small hot <b>socket patch</b>
        /// in the umbrella's underside (the one red group whose clearing orphans that arc and its bomb whole
        /// — the level's designed shot, ~25 balls); the trunk's two <b>hoops</b> are orange/white quadrant
        /// discs wider than the trunk they interrupt, so severing one (both inks, never one ball) guillotines
        /// everything below it; and the bombs are cooled black — dead weight at the bottom of its own path,
        /// which is the block's engineering law verbatim: every designed drop here is the lowest thing on
        /// what carries it, so a release moves the cluster's lowest point up (the trunk's own foot, the
        /// level's true lowest, hangs off the umbrella through the whole trunk and is severable only at the
        /// hoops, both priced below).
        /// </para>
        /// <para>
        /// <b>Structure, argued at design time (#288).</b> The anchor is the umbrella's ~190-ball top course;
        /// the trunk is a SOLID column (a tube at r 1.8 would be a one-cell wall — Cabinet's chain — where
        /// the solid section is a fat short chain of ~10 cells a level over 12 levels, Ziggurat-grade); the
        /// arcs are Rampart-grade cantilevers — five levels, ~2×2 cells of section, capped at the roof — and
        /// deliberately NOT the long free curves the block's design round rejected as the probe's prey. The
        /// hoop cascades are priced: the d-8 hoop drops ~70 balls (five trunk levels and the lower hoop), the
        /// d-11 hoop ~25, both starting at least four levels above the trunk's foot so the freed mass falls
        /// clear rather than lengthening anything. Nothing is enclosed: every interior cell (the umbrella's
        /// underside over the trunk) stands directly above structure that play eats from below first, the
        /// tall-level rule's normal course.
        /// </para>
        /// <para>
        /// Measured (#295): 466 balls in 48 standing groups (1.17 naive against 56 shots — see the Shots
        /// comment for the cascade-adjusted price), 97 ceiling anchors carrying 4.8 balls each and an anchor
        /// load of 5.5 — the gentlest hang in the block, which is what lets everything else here be a drop —
        /// margin 1, nothing alone (4 in pairs, 5 recoloured at socket and ash-block seams), best single
        /// shots 1–10 %, clearance 4.24 (lowest occupied level 6 of field 20). The sag probe reads it
        /// <b>0–1 of 5 across four runs, never with the glass at rest</b> — the one flickering order a
        /// late-game dip of −1.0 to −1.1 at shot 34 with the glass twice stepped, sitting exactly at the
        /// allowance the way the calibration's own edge cases do; the other orders clear the level outright
        /// at shots 38–53 of 56. Rejected on the way: a per-level ash/basalt dither (70 groups, ratio 0.69 —
        /// under the tool's floor; the trunk takes vertical five-strip colouring and the umbrella 3×3 column
        /// blocks instead), and 48 shots (1.00 exactly at 48 groups — Gantry's own lesson, no room for a
        /// miss).
        /// </para>
        /// </summary>
        private static Design Plume() => new()
        {
            File = "Plume.json",
            Name = "Plume",
            Grid = PLUME_GRID,
            Depth = PLUME_DEPTH,
            FieldLevels = PLUME_FIELD_LEVELS,
            Scene = SceneKind.Volcano,
            Sky = 9,
            Music = MUSIC_VOLCANO,
            Balls = BALLS_VOLCANO,
            //56 against 47 standing groups is 1.19 naive — over the tool's 1.00 floor — and the naive figure
            //is not this level's price (Pleat's lesson): five of the groups are sockets whose single shot
            //takes an arc and a bomb with it (~20 groups of mass through five shots), and the two hoops
            //guillotine the trunk below them, so the cascade-adjusted read is nearer 2 shots a group. The
            //finale is meant to be the block's tightest; tighter than this fails the floor.
            Shots = 56,
            //The #288 sum, at design time: the field leaves six empty levels under the layout (4.24 of
            //clearance at the [field] line); 56 shots at a step of 12 buy four descents, 2.40, leaving
            //~1.84 — clear of the 1.00 allowance with a bomb's short fall inside it. The finale keeps the
            //block's slowest clock because its designed drops need air to fall through.
            CeilingStep = 12,
            Occupied = PlumeOccupied,
            Colour = PlumeColour,
        };

        //THE PLUME'S OWN FIGURES. Fourteen levels in a field of 20 — six of clearance for the drops.
        private const byte PLUME_GRID = 17;
        private const byte PLUME_DEPTH = 14;
        private const byte PLUME_FIELD_LEVELS = 20;

        //The umbrella: the top two levels, a broad ash disc — the anchor is its whole top course.
        private const int PLUME_UMBRELLA_LEVELS = 2;
        private const float PLUME_UMBRELLA = 5.5f;

        //The trunk: a SOLID column (see the design doc for why not a tube) from under the umbrella to the
        //foot, interrupted by two hoop discs wider than itself — the severable bites. The hoops sit at
        //thirds of the naked lower trunk, both below the arc zone so no hot member can touch another.
        private const float PLUME_TRUNK = 1.8f;
        private const float PLUME_HOOP = 2.6f;
        private const int PLUME_HOOP_A = 8;
        private const int PLUME_HOOP_B = 11;

        //The fallout fan: five arcs on the downwind (+X) side only, bearings ±70° about it — a fan and not a
        //ring, because fallout lands downwind of a vent and the block's statement is the bearing. Each arc
        //descends from the umbrella's underside shearing PLUME_LEAN further +X a level (the wind, made
        //shape), at ~2×2 cells of section (ARC_R 1.05 captures 3–5 cells a level — Rampart-grade, not a
        //thread), and thickens into a bomb (BOMB_R) on its last two levels. Adjacent arcs sit 35° apart —
        //2.77 of chord at the orbit against 2×ARC_R = 2.1 of section — and their colour phases differ (see
        //PlumeColour), so even the closest approach cannot fuse same-ink across arcs.
        private const int PLUME_ARCS = 5;
        private const float PLUME_ARC_STEP = 35f * MathF.PI / 180f;
        private const float PLUME_ARC_ORBIT = 4.6f;
        private const float PLUME_ARC_R = 1.05f;
        private const float PLUME_LEAN = 0.24f;
        private const int PLUME_ARC_FIRST = 2;
        private const int PLUME_BOMB_FIRST = 7;
        private const int PLUME_BOMB_LAST = 8;
        private const float PLUME_BOMB_R = 1.15f;

        //The socket patches: the umbrella-underside cells each arc hangs from, PATCH_R about the arc's
        //unleaned bearing — hot, so the roof's downwind rim reads as five glowing mounts in the ash.
        private const float PLUME_PATCH_R = 1.05f;

        //The registers, per the block header: ash for the umbrella, basalt for the trunk (olive in place of
        //silver so the hoops' white never meets silver — a listed confusable pair — anywhere), the fallout
        //pair for arcs and sockets, orange/white quadrants for both hoops.
        private static readonly BallType[] PLUME_ASH = { BallType.Type8, BallType.Type10, BallType.Type11 };
        private static readonly BallType[] PLUME_BASALT = { BallType.Type8, BallType.Type10, BallType.Type13 };
        private static readonly BallType[] PLUME_FALLOUT = { BallType.Type1, BallType.Type7 };   //red, yellow
        private static readonly BallType[] PLUME_HOOP_INKS = { BallType.Type9, BallType.Type4 }; //orange, white
        private const BallType PLUME_BOMB_INK = BallType.Type8;                                  //cooled black

        /// <summary>Which fallout arc's column the cell is in at this depth, 0..4, or -1 for none. The arc's
        /// centre carries the downwind shear; the section radius widens to the bomb's on the last two
        /// levels.</summary>
        private static int PlumeArcIndex(float r, float ang, int d)
        {
            if (d < PLUME_ARC_FIRST || d > PLUME_BOMB_LAST) return -1;

            float dx = r * MathF.Cos(ang);
            float dz = r * MathF.Sin(ang);
            float radius = d >= PLUME_BOMB_FIRST ? PLUME_BOMB_R : PLUME_ARC_R;
            float lean = PLUME_LEAN * (d - PLUME_ARC_FIRST);

            for (int k = 0; k < PLUME_ARCS; k++)
            {
                float bearing = (k - PLUME_ARCS / 2) * PLUME_ARC_STEP;
                float cx = PLUME_ARC_ORBIT * MathF.Cos(bearing) + lean;
                float cz = PLUME_ARC_ORBIT * MathF.Sin(bearing);

                if ((dx - cx) * (dx - cx) + (dz - cz) * (dz - cz) <= radius * radius) return k;
            }

            return -1;
        }

        /// <summary>The socket patch under arc k, on the umbrella's underside level only — the arc's
        /// unleaned bearing, so the patch sits exactly over the arc's top.</summary>
        private static int PlumePatchIndex(float r, float ang)
        {
            float dx = r * MathF.Cos(ang);
            float dz = r * MathF.Sin(ang);

            for (int k = 0; k < PLUME_ARCS; k++)
            {
                float bearing = (k - PLUME_ARCS / 2) * PLUME_ARC_STEP;
                float cx = PLUME_ARC_ORBIT * MathF.Cos(bearing);
                float cz = PLUME_ARC_ORBIT * MathF.Sin(bearing);

                if ((dx - cx) * (dx - cx) + (dz - cz) * (dz - cz) <= PLUME_PATCH_R * PLUME_PATCH_R) return k;
            }

            return -1;
        }

        private static bool PlumeOccupied(float r, float ang, int i, int depth)
        {
            int d = LevelsBelowGlass(i, depth);

            if (d < PLUME_UMBRELLA_LEVELS) return r <= PLUME_UMBRELLA;
            if (r <= (d == PLUME_HOOP_A || d == PLUME_HOOP_B ? PLUME_HOOP : PLUME_TRUNK)) return true;

            return PlumeArcIndex(r, ang, d) >= 0;
        }

        private static BallType PlumeColour(float r, float ang, int i, int depth)
        {
            int d = LevelsBelowGlass(i, depth);

            //The sockets: hot mounts in the umbrella's underside, one per arc, each phase-matched to differ
            //from the arc band hanging off it — clearing one orphans its arc and bomb whole
            if (d == PLUME_UMBRELLA_LEVELS - 1)
            {
                int socket = PlumePatchIndex(r, ang);
                if (socket >= 0) return Band(socket, PLUME_FALLOUT);
            }

            int arc = PlumeArcIndex(r, ang, d);

            if (arc >= 0 && d >= PLUME_ARC_FIRST)
            {
                if (d >= PLUME_BOMB_FIRST) return PLUME_BOMB_INK;

                //Two bands down each arc, phase-stepped per arc: the +1 keeps a band off its own socket's
                //ink, and the +arc keeps the closest cells of neighbouring arcs off each other's
                return Band(arc + 1 + (d - PLUME_ARC_FIRST) / 3, PLUME_FALLOUT);
            }

            //Both hoops in the same quadrant pair, deliberately: one severance colour to learn, two discs to
            //spend it on. Four sectors of two inks — no single ball takes a course, per the block header.
            if (d == PLUME_HOOP_A || d == PLUME_HOOP_B)
                return Band(SectorIndex(ang, 0f, 4), PLUME_HOOP_INKS);

            //The trunk in five vertical strips over the three basalt inks — five and not a multiple of
            //three, so the wrap seam never puts an ink against itself; a strip is a slender group but the
            //trunk is solid, so nothing structural rides the colouring
            if (d >= PLUME_UMBRELLA_LEVELS) return Band(SectorIndex(ang, 0f, 5), PLUME_BASALT);

            //The umbrella's ash in 3x3 column blocks (i/2 spans both its levels), three inks against the
            //diagonal fuse — broad drifts of ash rather than a checkerboard
            float bx = r * MathF.Cos(ang) + 16f;
            float bz = r * MathF.Sin(ang) + 16f;

            return Band((int)(bx / 3f) + (int)(bz / 3f) + i / 2, PLUME_ASH);
        }

        #endregion

        #region The eruption's second five: the specials arrive (#368, #369)

        //THE ERUPTION'S SECOND FIVE. Two issues meet here and the meeting is the design: #369 asked why this
        //block ships five levels where every other chapter ships ten, and #368 asked where the Bomb (#326)
        //and the Zap (#327) enter a campaign that contains neither — both mechanics built, argued and
        //verified, and reachable only in the Testbed's own test fields. The volcano is where a bomb does not
        //have to explain itself: this block already has a level called Volley about the ballistic kind, its
        //palette already means "this is what the mountain runs on", and its own law — A DESIGNED BREAKAWAY IS
        //ALWAYS THE LOWEST THING ON ITS OWN LOAD PATH — is the sentence a blast has to obey anyway.
        //
        //THE TEACHING ORDER IS THE POINT, and it is the Anvil's precedent (#324's stone): a rule the player
        //discovers by wasting a ball on it is a rule taught the worst way. So each mechanic gets one level
        //where it is UNMISSABLE AND CHEAP before it gets one where it is the tool:
        //
        //  6. Vent      - the bomb, taught. Eight of them ring the cone's mouth on the lowest course (four
        //                 bearings, a pair of cells apiece), where the gun looks first and nothing is in the way.
        //  7. Sill      - the bomb, used. A cold plate too dithered to clear by colour at speed, opened
        //                 through four slots cut up into it, a charge wedged in the head of each - five balls,
        //                 because a slot's head is one cell on one parity and two on the next.
        //  8. Fume      - the zap, taught. Five pipes hang from a cap, each ending in exactly one zap, each
        //                 pipe a different pair of hot inks - so what a zap takes is written on the pipe it
        //                 hangs on.
        //  9. Caldera   - the zap, used. A ring wall on a bench, six zaps set in the bench under the wall's
        //                 breach, over hot inks dithered so that one of them is scattered where no group
        //                 forms: the colour a zap is for.
        // 10. Paroxysm  - both, and the order between them. Six bombs down one flank, eight zaps down the
        //                 other, and a seam at the foot where one landing arms one of each - measured against
        //                 the lattice rather than asserted: FIVE empty cells there touch a bomb AND a zap.
        //                 That is where #327's ruling (the zap goes first, then the blast) is a thing the
        //                 player can watch happen.
        //
        //EVERY SPECIAL HERE HANGS WITH AN EMPTY NEIGHBOUR BY CONSTRUCTION - on a lowest course, at the head of
        //a notch or at the foot of a pipe - which is what FindStrandedSpecials refuses a level for, and the
        //reason each of them is placed at a mouth rather than inside a mass. None is on the anchor course:
        //the glass keeps the cold plate it always did.
        //
        //THE BLOCK'S OWN LAWS ARE NOT RELAXED FOR THEM. The cold masses dither on three inks and the hot
        //members band on two; every level happened in a direction (the cone tears one way, the notches march
        //downwind, the pipes hang in a fan, the caldera's rim is breached on one bearing, the column leans);
        //and every designed drop is the lowest thing on its own path, which a blast satisfies trivially -
        //it takes a sphere out of the bottom of something and what is left hangs higher than it did.

        //THE VENT'S OWN FIGURES. A hollow spatter cone, shell two cells thick, widening from a 2.4 mouth at
        //the bottom to 5.4 at the glass - the mouth is where the bombs sit, and the taper is what puts them
        //where the gun looks first.
        private const byte VENT_GRID = 15;
        //⚠ Eight and not ten. At ten the cone hung a narrow mouth a long way under its anchor and the probe
        //read 3 of 5; stubbier, with the same mouth and the same rim at the glass, it reads 1.
        private const byte VENT_DEPTH = 8;
        private const byte VENT_FIELD_LEVELS = 18;
        private const float VENT_MOUTH = 2.4f;
        private const float VENT_FLARE = 0.43f;
        private const float VENT_WALL = 1.0f;

        //Four bombs on the lowest course, on the quarter bearings so none is behind another from any orbit
        //angle, and a whole sector's width apart - two cells clear of the next at the mouth's radius, which
        //is BLAST_RADIUS itself, so a blast reaches its neighbour's cell without reaching the neighbour.
        private const int VENT_BOMBS = 4;
        private const float VENT_BOMB_HALF_WIDTH = 0.55f;

        private static readonly BallType[] VENT_CRUST = { BallType.Type8, BallType.Type10, BallType.Type13 };  //black, brown, olive
        private static readonly BallType[] VENT_GLOW = { BallType.Type1, BallType.Type9 };                     //red, orange

        private static float VentRadius(int i) => VENT_MOUTH + i * VENT_FLARE;

        /// <summary>How far a bearing is from the nearest bomb's, in sectors of the mouth (0 at a bomb).</summary>
        private static float VentBombOffset(float ang)
        {
            float turns = (ang / MathF.Tau + 0.5f) * VENT_BOMBS;
            return MathF.Abs(turns - MathF.Round(turns));
        }

        /// <summary>
        /// <b>The bomb, taught.</b> A hollow spatter cone hanging mouth-down, and four bombs set in the mouth
        /// itself - the lowest course, the first thing the gun sees and the last thing anything else is in
        /// front of. It is the Anvil's lesson (#324) borrowed for a louder kind: the first blast a player ever
        /// sets off should cost one ball, be impossible to miss and happen where they were already aiming.
        /// <para>
        /// <b>What the level teaches, in the order it teaches it.</b> A bomb is not shot AT - it is armed by a
        /// landing BESIDE it (#326), which is the one sentence about it a player has to learn, and the mouth
        /// is where that sentence is cheapest to try: every cell round a bomb here is open air. The blast then
        /// takes a sphere of <c>BallsConstraintsBuilder.BLAST_RADIUS</c> out of the cone's lip, which is
        /// twenty-odd balls of cold crust and the glow behind them - a payoff the player watches rather than
        /// counts.
        /// </para>
        /// <para>
        /// <b>They stand on four bearings a quarter-turn apart - eight balls, a bearing catching one cell on
        /// an unshifted course and two on a shifted one - and that spacing is measured, not a symmetry.</b> At the
        /// mouth's radius of <see cref="VENT_MOUTH"/> a quarter turn is about 3.8 cells, so a blast reaches
        /// the cells between two bombs and stops short of the next bomb: they do not chain, and a player who
        /// arms one does not get the other three for free. (They chain readily from the second course up,
        /// where the cone is narrower in bomb terms - that is a later level's subject, not this one's.)
        /// </para>
        /// <para>
        /// <b>The glow is at the mouth here, which inverts the block's usual reading and is the direction this
        /// level happened in.</b> A spatter cone is fed from below: its two lowest courses are hot (red and
        /// orange, banded by bearing so no course is one ink), the rest is cold crust dithered on three, and
        /// the cone hangs from a complete cold annulus at the glass. So the load is at the TOP and the light
        /// is at the BOTTOM - the one level in the block where cutting the glow costs nothing structural, and
        /// the reason a player can spend the mouth learning what a bomb does.
        /// </para>
        /// </summary>
        private static Design Vent() => new()
        {
            File = "Vent.json",
            Name = "Vent",
            Grid = VENT_GRID,
            Depth = VENT_DEPTH,
            FieldLevels = VENT_FIELD_LEVELS,
            Scene = SceneKind.Volcano,
            Sky = 9,
            Music = MUSIC_VOLCANO,
            Balls = BALLS_VOLCANO,
            Shots = 46,
            CeilingStep = 9,
            Occupied = (r, ang, i, depth) => MathF.Abs(r - VentRadius(i)) <= VENT_WALL,
            //Two hot courses at the mouth banded by bearing, cold crust above dithered on three inks by a
            //bearing-and-course index - the block's palette law, and the dither is what keeps the crust's
            //groups small enough that the bombs are the fast way through rather than a flourish.
            Colour = (r, ang, i, depth) => i < 2
                ? Band(SectorIndex(ang, 0f, 2 * VENT_BOMBS), VENT_GLOW)
                : Band(SectorIndex(ang, 0f, 3 * VENT_BOMBS) + i, VENT_CRUST),
            Kind = (r, ang, i, depth) => i == 0 && VentBombOffset(ang) * VENT_BOMBS < VENT_BOMB_HALF_WIDTH
                ? BallKind.Bomb
                : BallKind.Normal,
        };

        //THE SILL'S OWN FIGURES. A cold plate at the glass, four courses thick, with four notches cut up into
        //it from below and a bomb at the head of each - the plate is the wall and the notches are the way in.
        private const byte SILL_GRID = 15;
        private const byte SILL_DEPTH = 6;
        private const byte SILL_FIELD_LEVELS = 18;
        private const float SILL_HALF = 5.4f;
        private const int SILL_NOTCH_DEPTH = 3;
        //⚠ A slot is ONE CELL WIDE and not two. At 1.1 the four slots and the plate's own width left strips
        //of a third of a cell between them — so the plate below the cut was slivers, and twenty-two balls
        //floated free where a strip had no column at all. Narrow slots leave strips two and three cells wide,
        //which hang, and a one-cell slot is still a gap the eye reads through.
        private const float SILL_NOTCH_HALF = 0.6f;

        //How near the shaft's own axis a cell has to be to BE the charge rather than merely to stand in the
        //notch - see the kind rule for what asking the looser question cost.
        private const float SILL_CHARGE = 0.55f;

        //The notches march downwind - the level's direction - so their spacing is a run in x and their
        //depth grows with it: the plate is opened shallowest upwind and deepest downwind.
        //⚠ EVERY STRIP BETWEEN TWO SLOTS IS AT LEAST A CELL WIDE, and that is what the "11 balls float free"
        //reading was about rather than the slots themselves. A strip narrower than one cell holds a column on
        //one parity and none on the next, so the cells under the cut have no neighbour above them on the
        //course that would carry them — they hang from nothing and the validator refuses the level. Spaced at
        //2.3 with slots 1.2 wide, the narrowest strip here is 1.1 and the rim strips are wider still.
        private static readonly float[] SILL_NOTCH_X = { -3.5f, -1.2f, 1.1f, 3.4f };

        private static readonly BallType[] SILL_PLATE = { BallType.Type11, BallType.Type8, BallType.Type10 };  //silver, black, brown
        private static readonly BallType[] SILL_MELT = { BallType.Type9, BallType.Type7 };                     //orange, yellow

        /// <summary>
        /// Which notch a cell is in the mouth of, or -1.
        /// <para>
        /// <b>⚠ A notch runs right through the plate in z, and that is a legibility fix rather than a
        /// geometric preference (#368).</b> Cut as a square shaft it was invisible from where the game is
        /// played: the gun looks up at the plate almost edge-on, so a hole in the underside two cells across
        /// is behind the plate's own face and the level read as a plain slab with no way in. A slot across
        /// the whole depth is a gap the player can see daylight through from any orbit angle, with the bomb
        /// glowing at the head of it — which is the level's whole instruction, given without a word.
        /// </para>
        /// </summary>
        private static int SillNotch(float dx, float dz)
        {
            for (int n = 0; n < SILL_NOTCH_X.Length; n++)
                if (MathF.Abs(dx - SILL_NOTCH_X[n]) <= SILL_NOTCH_HALF) return n;

            return -1;
        }

        private static bool SillOccupied(int x, int z, int i, int depth)
        {
            Centred(x, z, i, SILL_GRID, out float dx, out float dz);

            if (MathF.Abs(dx) > SILL_HALF || MathF.Abs(dz) > SILL_HALF) return false;

            //The slot: cut UP into the plate from its underside, so the charge at its head has open air under
            //it and the player has a pocket to land in beside it.
            //
            //⚠ AT THE SLOT'S HEAD THE ONLY CELL IS THE CHARGE ITSELF, and that is the difference between a
            //bomb the player can see and one they have to be told about. Left as a full course, the charge
            //sat in the middle of the plate's depth with plate in front of it: invisible from the low angle
            //this game is played at, and the level read as a slab with nothing to aim at. One ball wedged in
            //the open slot is lit by its own charge and is the level's whole instruction.
            int notch = SillNotch(dx, dz);

            if (notch < 0) return true;

            return i > SILL_NOTCH_DEPTH || (i == SILL_NOTCH_DEPTH && MathF.Abs(dz) <= SILL_CHARGE);
        }

        /// <summary>
        /// <b>The bomb, used.</b> A sill - the flat sheet of rock a volcano intrudes between two beds - hanging
        /// as a cold plate four courses thick, with four slots cut up into its underside and a charge wedged
        /// in the head of each - five balls over the four slots, a head being one cell on one parity and two
        /// on the next. It is the level where the mechanic stops being a spectacle and becomes a tool.
        /// <para>
        /// <b>The plate is deliberately slow to clear by colour.</b> It is three cold inks on a dither, so its
        /// groups are small and scattered and no shot into the face of it takes much - the honest way through
        /// is a long one. The bombs are the short way: each sits inside the plate's own thickness at the head
        /// of its notch, so a landing in the notch takes a sphere out of the plate's middle and the courses
        /// above it come down as one piece.
        /// </para>
        /// <para>
        /// <b>The notch is what makes the bomb legal and is not a decoration.</b> A bomb with no empty
        /// neighbour is a level that cannot be finished while every other gate passes it, which is exactly
        /// what <c>FindStrandedSpecials</c> refuses (#325's walk, widened by #326) - so a bomb buried in a
        /// plate has to be given a mouth, and a notch cut up from the underside is the cheapest mouth that
        /// still reads as part of the rock.
        /// </para>
        /// <para>
        /// <b>The four notches march downwind</b>, which is this level's direction: they are spaced across the
        /// plate in x on one bearing, so the plate opens along a line rather than at random. The plate's
        /// underside course is hot (orange and yellow, banded) - the melt the sill was injected as - so the
        /// level still reads as the block's: the glow is the underside, the load is the cold plate at the glass.
        /// </para>
        /// </summary>
        private static Design Sill() => new()
        {
            File = "Sill.json",
            Name = "Sill",
            Grid = SILL_GRID,
            Depth = SILL_DEPTH,
            FieldLevels = SILL_FIELD_LEVELS,
            Scene = SceneKind.Volcano,
            Sky = 9,
            Music = MUSIC_VOLCANO,
            Balls = BALLS_VOLCANO,
            Shots = 50,
            CeilingStep = 9,
            OccupiedBlock = SillOccupied,
            BlockColour = (x, z, i) =>
            {
                Centred(x, z, i, SILL_GRID, out float dx, out float dz);

                //The melt on the underside, banded across the plate so no course of it is one ink. Indexed
                //off the RAW cell and not off the centred offset: Band takes a bare modulo, and a centred
                //coordinate goes negative on half the plate.
                if (i < 1) return Band(x / 2 + z / 2, SILL_MELT);

                //And the plate itself, dithered on three: the diagonal fuse the block measured three times
                return Band(x + z + i, SILL_PLATE);
            },
            BlockKind = (x, z, i, depth) =>
            {
                Centred(x, z, i, SILL_GRID, out float dx, out float dz);

                //⚠ The bomb is the notch's HEAD CELL and not its whole cross-section. Asking only whether a
                //cell is in the notch put one in every cell of it - thirty on the level where the design says
                //four - so the charge is held to the middle of the shaft in both axes, which is one cell on an
                //unshifted course and the two straddling the axis on a shifted one.
                return SillNotch(dx, dz) >= 0 && i == SILL_NOTCH_DEPTH
                       && MathF.Abs(dz) <= SILL_CHARGE && MathF.Abs(dx - SILL_NOTCH_X[SillNotch(dx, dz)]) <= SILL_CHARGE
                    ? BallKind.Bomb
                    : BallKind.Normal;
            },
        };

        //THE FUME'S OWN FIGURES. A cold cap at the glass with five pipes hanging from it in a fan, each pipe
        //2x2 in plan, each a different length, each ending in one zap.
        private const byte FUME_GRID = 15;
        private const byte FUME_DEPTH = 12;
        private const byte FUME_FIELD_LEVELS = 18;
        private const float FUME_CAP_HALF = 4.6f;
        private const int FUME_CAP_COURSES = 3;
        private const float FUME_PIPE_HALF = 1.1f;

        //How near a pipe's axis the zap at its foot sits - the Sill's SILL_CHARGE, for the same reason.
        private const float FUME_CHARGE = 0.55f;

        //The fan: each pipe's centre and how far below the cap it reaches. They lengthen downwind, which is
        //this level's direction, and no two ends are on the same course - so a zap is always at a mouth of
        //its own and the five reads as a rank rather than as a comb.
        //
        //⚠ EACH CENTRE MATCHES THE PARITY OF ITS OWN BOTTOM COURSE, and that is what makes the zap at the
        //foot ONE ball. Odd courses put cells on integers and even ones on halves, so a centre of the wrong
        //parity has no cell on it and four within half a cell of it - which is how the first build came out
        //with fourteen zaps where the design says five. Centres are also kept 2.2 apart in x or z (two pipe
        //half-widths), so no two pipes share a cell, and inside the cap's own half-width, so every pipe hangs
        //from something.
        private static readonly (float X, float Z, int Bottom)[] FUME_PIPES =
        {
            (-3f, -2f, 7), (-3f, 2f, 5), (0f, -2f, 3), (3f, 2f, 1), (2.5f, -2.5f, 6),
        };

        private static readonly BallType[] FUME_CAP = { BallType.Type8, BallType.Type11, BallType.Type13 };  //black, silver, olive

        //One pair of hot inks per pipe, banded by course - so a pipe is two groups of its own and the five
        //pipes between them put every hot ink in the block's register on the level.
        private static readonly BallType[][] FUME_PIPE_INKS =
        {
            new[] { BallType.Type1, BallType.Type9 },   //red, orange
            new[] { BallType.Type9, BallType.Type7 },   //orange, yellow
            new[] { BallType.Type7, BallType.Type4 },   //yellow, white-hot
            new[] { BallType.Type4, BallType.Type1 },   //white-hot, red
            new[] { BallType.Type1, BallType.Type7 },   //red, yellow
        };

        /// <summary>Which pipe a cell is in, or -1. A pipe runs from under the cap down to its own bottom.</summary>
        private static int FumePipe(float dx, float dz, int i)
        {
            for (int p = 0; p < FUME_PIPES.Length; p++)
            {
                var pipe = FUME_PIPES[p];

                if (i >= pipe.Bottom && i < FUME_DEPTH - FUME_CAP_COURSES
                    && MathF.Abs(dx - pipe.X) <= FUME_PIPE_HALF && MathF.Abs(dz - pipe.Z) <= FUME_PIPE_HALF)
                    return p;
            }

            return -1;
        }

        private static bool FumeOccupied(int x, int z, int i, int depth)
        {
            Centred(x, z, i, FUME_GRID, out float dx, out float dz);

            if (i >= depth - FUME_CAP_COURSES)
                return MathF.Abs(dx) <= FUME_CAP_HALF && MathF.Abs(dz) <= FUME_CAP_HALF;

            return FumePipe(dx, dz, i) >= 0;
        }

        /// <summary>
        /// <b>The zap, taught.</b> Five fumarole pipes hanging from a cold cap, each ending in one zap - and
        /// each pipe drawn in its own pair of hot inks, which is the whole lesson. A zap takes one COLOUR off
        /// the entire field (#327), and the colour it takes is the colour of the ball that lands beside it, so
        /// the question a player has to learn to ask is <i>what am I about to shoot</i> - which the magazine
        /// answers three balls ahead.
        /// <para>
        /// <b>The pipes are what make that visible.</b> Each is two inks and no two pipes share a pair, so the
        /// field's hot colours are laid out in named places rather than mixed through a mass: a player who
        /// zaps red can see, before the shot, that red is two pipes and part of a third. The cap is cold and
        /// dithered on three, and holds every pipe - so a zap never threatens the hang, which is what lets
        /// this level be the one where the mechanic is tried rather than survived.
        /// </para>
        /// <para>
        /// <b>Every zap is at the foot of its own pipe</b>, with open air under it and no pipe ending on the
        /// same course as another - the fan lengthens downwind, this level's direction. That is the
        /// walled-in refusal answered by construction, and it also means the five are armed one at a time:
        /// a landing at one foot is nowhere near another.
        /// </para>
        /// <para>
        /// <b>⚠ A zap on a field of five colours is a big shot and that is the point of teaching it here.</b>
        /// Taking a whole ink off this level removes a pipe and a half; the cap is untouched, being cold, so
        /// the level shortens rather than collapses. The measured figure is in the validator's output rather
        /// than in this comment, because it is the drop test that prices it and the drop test learned to see
        /// a glass landing in #362 - a zap's payout it still reads through the ordinary group path, since a
        /// zapped colour leaves as its own groups.
        /// </para>
        /// </summary>
        private static Design Fume() => new()
        {
            File = "Fume.json",
            Name = "Fume",
            Grid = FUME_GRID,
            Depth = FUME_DEPTH,
            FieldLevels = FUME_FIELD_LEVELS,
            Scene = SceneKind.Volcano,
            Sky = 9,
            Music = MUSIC_VOLCANO,
            Balls = BALLS_VOLCANO,
            Shots = 48,
            CeilingStep = 9,
            OccupiedBlock = FumeOccupied,
            BlockColour = (x, z, i) =>
            {
                Centred(x, z, i, FUME_GRID, out float dx, out float dz);

                int pipe = FumePipe(dx, dz, i);

                return pipe < 0
                    ? Band(x + z + i, FUME_CAP)
                    : Band(i, FUME_PIPE_INKS[pipe]);
            },
            BlockKind = (x, z, i, depth) =>
            {
                Centred(x, z, i, FUME_GRID, out float dx, out float dz);

                int pipe = FumePipe(dx, dz, i);

                //One zap at the foot of each pipe, on the pipe's own axis - the Sill's lesson: asking only
                //whether the cell is in the pipe puts a zap in every cell of its foot.
                return pipe >= 0 && i == FUME_PIPES[pipe].Bottom
                       && MathF.Abs(dx - FUME_PIPES[pipe].X) <= FUME_CHARGE
                       && MathF.Abs(dz - FUME_PIPES[pipe].Z) <= FUME_CHARGE
                    ? BallKind.Zap
                    : BallKind.Normal;
            },
        };

        //THE CALDERA'S OWN FIGURES. A ring wall on a floor: an annulus two cells thick standing from the
        //floor up to the glass, breached on one bearing, with the floor a disc under it and the zaps set in
        //the floor's rim where the wall's foot meets it.
        private const byte CALDERA_GRID = 17;
        private const byte CALDERA_DEPTH = 10;
        private const byte CALDERA_FIELD_LEVELS = 18;
        private const float CALDERA_RIM = 5.2f;
        private const float CALDERA_WALL = 1.0f;
        private const int CALDERA_FLOOR_COURSES = 2;
        private const float CALDERA_BREACH = 0.11f;   //a fraction of a turn, one bearing of wall missing

        private const int CALDERA_ZAPS = 3;

        //THE RESURGENT DOME, and it is structure before it is geology. Without it the floor is a wide disc
        //hanging from its own rim - a trampoline - and the sag probe answered 5 of 5 with one loss while the
        //glass was still at rest, which is the reading that means the LAYOUT and not the play. A column up
        //the middle from the floor to the glass gives the floor a second load path at its centre, which is
        //what a caldera does anyway once the chamber refills: the floor lifts on a dome.
        private const float CALDERA_DOME = 1.9f;

        //And the floor is a TERRACE and not a disc, which is the second half of the same finding: a full disc
        //is a hundred balls of unsupported middle hanging off a breached ring, and the dome alone only took
        //the probe from 5 of 5 to 4. Cut back to a ring between the dome and the wall, the level reads 1.
        //It is also what a caldera looks like from underneath - a bench round a dome, with the chamber's
        //roof gone from the middle.
        private const float CALDERA_BENCH = 3.0f;

        private static readonly BallType[] CALDERA_WALL_INKS = { BallType.Type8, BallType.Type10, BallType.Type13 };  //black, brown, olive
        private static readonly BallType[] CALDERA_FLOOR_INKS = { BallType.Type1, BallType.Type9, BallType.Type7 };   //red, orange, yellow

        /// <summary>How far a bearing is from the breach's, in turns (0 at the breach's middle).</summary>
        private static float CalderaBreachOffset(float ang)
        {
            float turns = ang / MathF.Tau + 0.5f;
            turns -= MathF.Floor(turns);

            return MathF.Min(turns, 1f - turns);
        }

        /// <summary>
        /// <b>The zap, used.</b> A caldera - the ring left when a mountain's roof falls into the chamber it
        /// emptied - as a two-cell wall standing on a molten bench, breached on one bearing, with six zaps
        /// set in the bench under the wall's foot, and a resurgent dome up the middle carrying it.
        /// <para>
        /// <b>The bench is the level and the wall is the clock.</b> It is hot, three inks, and it is
        /// dithered rather than banded, which puts one of those inks - whichever the player is unlucky with -
        /// in scattered cells that form no group worth shooting. That is the colour a zap is FOR: the
        /// mechanic's whole use is taking a colour that has stopped being worth a shot, and a level that
        /// wants it has to contain one. The wall above is cold, dithered on three, and hangs from the glass;
        /// it is what the player is fighting the clock against while they decide.
        /// </para>
        /// <para>
        /// <b>The breach is the direction this level happened in</b>, and it is also what makes the zaps
        /// reachable: the wall is missing over one bearing, so the floor's rim under it is open to the gun
        /// from that side and the three zaps sit in the arc the breach exposes. A zap under a standing wall
        /// would be a special the shot cannot reach, which the gate refuses and which would be an unfinishable
        /// level with every other check green.
        /// </para>
        /// <para>
        /// <b>Under the block's law the floor is the lowest thing on its own load path</b>, so every colour
        /// the player takes out of it - by matching or by zapping - raises the cluster's lowest point. That
        /// is what makes a level whose subject is removing a whole colour at once measurably safe to hang.
        /// </para>
        /// </summary>
        private static Design Caldera() => new()
        {
            File = "Caldera.json",
            Name = "Caldera",
            Grid = CALDERA_GRID,
            Depth = CALDERA_DEPTH,
            FieldLevels = CALDERA_FIELD_LEVELS,
            Scene = SceneKind.Volcano,
            Sky = 9,
            Music = MUSIC_VOLCANO,
            Balls = BALLS_VOLCANO,
            Shots = 52,
            CeilingStep = 10,
            Occupied = (r, ang, i, depth) =>
                i < CALDERA_FLOOR_COURSES
                    ? r <= CALDERA_RIM && (r >= CALDERA_BENCH || r <= CALDERA_DOME)
                    : r <= CALDERA_DOME
                      || (MathF.Abs(r - CALDERA_RIM) <= CALDERA_WALL && CalderaBreachOffset(ang) > CALDERA_BREACH),
            //The dome takes the wall's cold register and the floor the hot one, so the level still reads as
            //the block's: the glow is the floor, the load is the ring and the dome that hold it
            Colour = (r, ang, i, depth) => i < CALDERA_FLOOR_COURSES
                ? Band(SectorIndex(ang, 0f, 7) + (int)MathF.Floor(r) + i, CALDERA_FLOOR_INKS)
                : Band(SectorIndex(ang, 0f, 11) + i + (int)MathF.Floor(r), CALDERA_WALL_INKS),
            Kind = (r, ang, i, depth) =>
                i == CALDERA_FLOOR_COURSES - 1 && r > CALDERA_RIM - 2f
                && CalderaBreachOffset(ang) * CALDERA_ZAPS % 1f < 0.06f
                    ? BallKind.Zap
                    : BallKind.Normal,
        };

        //THE PAROXYSM'S OWN FIGURES. The block's finale and the campaign's first level to carry both new
        //kinds: a leaning column, bombs down the flank it leans away from and zaps down the flank it leans
        //over, meeting at a seam at the foot.
        private const byte PAROXYSM_GRID = 15;
        //⚠ Twelve and not thirteen: the emitter refuses an odd offset (field less layout), because the
        //loader would extend the field a level and move the drawing off where it was put.
        private const byte PAROXYSM_DEPTH = 12;
        private const byte PAROXYSM_FIELD_LEVELS = 18;
        private const float PAROXYSM_RADIUS = 3.1f;
        private const float PAROXYSM_LEAN = 0.26f;    //cells of offset a course, the column's own bearing

        private static readonly BallType[] PAROXYSM_COLUMN = { BallType.Type8, BallType.Type10, BallType.Type11 };  //black, brown, silver
        private static readonly BallType[] PAROXYSM_CORE = { BallType.Type1, BallType.Type9, BallType.Type7, BallType.Type4 };

        //Every third course carries a special, alternating flanks, from the foot up to where the column meets
        //the glass - and the two lowest are a bomb and a zap on the SAME course, which is the level's subject.
        private const int PAROXYSM_STEP = 3;

        private static bool ParoxysmOccupied(int x, int z, int i, int depth)
        {
            Centred(x, z, i, PAROXYSM_GRID, out float dx, out float dz);

            dx -= (depth - 1 - i) * PAROXYSM_LEAN;

            return MathF.Sqrt(dx * dx + dz * dz) <= PAROXYSM_RADIUS;
        }

        /// <summary>Which flank a cell is on, and how far it is from the column's own axis.</summary>
        private static float ParoxysmFlank(int x, int z, int i, out float dz)
        {
            Centred(x, z, i, PAROXYSM_GRID, out float dx, out float dzLocal);

            dz = dzLocal;

            return dx - (PAROXYSM_DEPTH - 1 - i) * PAROXYSM_LEAN;
        }

        /// <summary>
        /// <b>Both, and the order between them.</b> The block's finale and the first level in the campaign to
        /// carry two kinds at once: a leaning column of cold crust round a molten core, bombs set down the
        /// flank it leans away from, zaps down the flank it leans over, and one course at the foot where a
        /// single landing can arm one of each.
        /// <para>
        /// <b>That course is the level's subject, and what it shows is a RULING rather than an effect</b>
        /// (#327): a landing collects every special beside it, and then the zap goes first and the blast
        /// second. The order is not a detail - a blast that ran first would eat the zap as a victim and one
        /// of the two effects the player armed with one ball would vanish without a trace, while a zap can
        /// never take a bomb, because it only ever takes MATCHABLE balls and a bomb's colour is one nothing
        /// may read. So the two compose, always, in that order: wide first, then local. Here the player can
        /// watch it - the field loses a colour, and then the foot of the column blows out.
        /// </para>
        /// <para>
        /// <b>The lean is the direction, and it is what separates the two kinds.</b> The column offsets
        /// <see cref="PAROXYSM_LEAN"/> of a cell a course, so its flanks are not two sides of a cylinder but
        /// an overhanging one and a receding one: the bombs are on the receding flank where the gun sees them
        /// against the sky, the zaps on the overhanging one where the column itself is the backdrop. Every
        /// special sits at a course where the flank it is on is the column's lowest cell in that bearing, so
        /// each has open air beside it and the gate's walled-in refusal is answered by the geometry.
        /// </para>
        /// <para>
        /// <b>The core is the block's glow and carries the load</b> - four hot inks up the column's axis,
        /// banded so no course is one colour - and the crust round it is cold on three. A blast at the foot
        /// takes crust and core together, which is the block's law again: the column's lowest thing goes, and
        /// what is left hangs higher.
        /// </para>
        /// </summary>
        private static Design Paroxysm() => new()
        {
            File = "Paroxysm.json",
            Name = "Paroxysm",
            Grid = PAROXYSM_GRID,
            Depth = PAROXYSM_DEPTH,
            FieldLevels = PAROXYSM_FIELD_LEVELS,
            Scene = SceneKind.Volcano,
            Sky = 9,
            Music = MUSIC_VOLCANO,
            Balls = BALLS_VOLCANO,
            Shots = 54,
            CeilingStep = 10,
            OccupiedBlock = ParoxysmOccupied,
            BlockColour = (x, z, i) =>
            {
                float flank = ParoxysmFlank(x, z, i, out float dz);

                return MathF.Sqrt(flank * flank + dz * dz) <= 1.4f
                    ? Band(i, PAROXYSM_CORE)
                    : Band(x + z + i, PAROXYSM_COLUMN);
            },
            BlockKind = (x, z, i, depth) =>
            {
                float flank = ParoxysmFlank(x, z, i, out float dz);

                //THE SEAM AT THE FOOT, and it is the level's subject rather than a flourish: on the lowest
                //course the two kinds sit SIDE BY SIDE across the column's own depth, so one landing under
                //them is beside both and arms one of each. Without it the two flanks are four and a half
                //cells apart and no single ball can ever reach a bomb and a zap at once - which would have
                //left this design's whole claim untrue of its own geometry.
                if (i == 0 && MathF.Abs(flank) <= 1.4f && MathF.Abs(dz) <= 1.1f)
                    return dz >= 0f ? BallKind.Bomb : BallKind.Zap;

                if (MathF.Abs(dz) > 0.9f || i % PAROXYSM_STEP != 0 || i > depth - 4) return BallKind.Normal;

                //Bombs on the receding flank, zaps on the overhanging one - and at the foot both, which is
                //the one course where a single landing can arm one of each.
                //
                //⚠ THE RIM BAND IS 0.8 AND NOT 1.2, and that is the gate talking. At 1.2 a special could sit
                //one cell in from the flank, and the lean then covered it from the course above: two of them
                //came out WALLED IN, which FindStrandedSpecials refuses outright and rightly - a special with
                //no empty neighbour is a level that cannot be finished with every other check green. Held to
                //the outermost cell, the lean of 0.26 a course cannot reach across the gap it leaves.
                if (flank > PAROXYSM_RADIUS - 0.8f) return BallKind.Bomb;

                return flank < -(PAROXYSM_RADIUS - 0.8f) ? BallKind.Zap : BallKind.Normal;
            },
        };

        #endregion
        #endregion
    }
}
