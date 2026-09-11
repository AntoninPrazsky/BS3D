using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>The Nebula</b>, block 7 of the campaign: its designs, and the helpers no other block's designs use, in
    /// the order <c>Program.cs</c> held them — the play order is <see cref="Main"/>'s, and the block's name, music
    /// and ball style are in the tables there. Split out of <c>Program.cs</c> in #386.
    /// </summary>
    internal static partial class Program
    {

        #region The nebula levels (#182)

        //THE SEVENTH BLOCK: the arena hanging in deep space, past the Quarry's airless black — the light ramp
        //continuing outward rather than turning back, which is what lets a block sit after the Moon at all
        //(#207 refused a BRIGHT one there, and that half of its reasoning stands). It is the second tall
        //block, and that reverses the Coil's recorded rule that only the Tower should be one — deliberately,
        //because it is the owner's ask (#182: tall, in the Helix's style) and because what this block states
        //is not the Tower's premise. The Tower's statement is "the layout is deeper than the camera frames";
        //the Nebula's is "the five #152 colours arrive", one or two per level until the finale plays all
        //thirteen, and every silhouette here is OPEN — strands, walls and beads the player reads a whole turn
        //of, never a solid mass.
        //
        //Every level is a different KIND of tall (the Tower's own #160 rule, kept):
        //  Comet    - a head with a tail: the mass is at the TOP and a single strand hangs from it.
        //  Vortex   - a hollow wall around nothing, its window and its panes turning as they descend.
        //  Carousel - three rails and their decks: the mass is a repeating FRAME, not any body.
        //  Wishbone - a trunk that FORKS: one descending front becomes two the gun alternates between.
        //  Garland  - beads on two counter-turning strands: the mass is in PACKETS, and every packet is its
        //             own colour - all thirteen of them, which is the finale's whole difficulty.
        //
        //The colour debuts, and who each stands next to (the #152 rival pairs, met deliberately):
        //  Comet    orange       - between red and gold, its own rivals, in the head's wedges.
        //  Vortex   brown        - against orange one pane over.
        //  Carousel silver       - banded against white and black on the neighbouring rails.
        //  Wishbone navy + olive - the two bulbs, on arms segmented in blue/cyan and green/white.
        //  Garland  all five     - among all thirteen.

        /// <summary>
        /// A comet hanging head-up: a round coma pressed against the glass and a single tail winding three
        /// quarters of a turn down from under it, thinning as it goes — the block's opener and the gentlest
        /// statement of its style. The head is wedged in three colours the way the Meadow's plates taught (a
        /// wedge is dozens of balls, so the opener still pays out big), and <b>orange debuts in the middle
        /// wedge, flanked by red and gold</b> — its own rivals (#152), met as neighbouring plates before any
        /// level asks for them told apart at speed.
        /// <para>
        /// The anchor lesson of <see cref="Horn"/> sideways: the head's wedges run the full body, so peeling
        /// one never stands the others on nothing, and the tail hangs from whichever wedges its root touches.
        /// A tail segment cut drops only the tail below it — the shallowest cascade in the block, which is
        /// the right depth for its first level.
        /// </para>
        /// <para>
        /// Measured: 299 balls, margin 1, nothing alone or paired, nothing recoloured; best single shots
        /// 22 %, 9 %, 19 % and 26 % — plate-sized payoffs, the opener's job. Stable hanging unshot for 40 s
        /// in the running game (the tail carries only its own weight; the sag that sank the first Garland
        /// never threatened it).
        /// </para>
        /// </summary>
        private static Design Comet() => new()
        {
            File = "Comet.json",
            Name = "Comet",
            Grid = 11,
            Depth = 22,
            FieldLevels = 32,
            Scene = SceneKind.Space,
            Sky = NEBULA_SKY,
            Music = MUSIC_NEBULA,
                Balls = BALLS_NEBULA,
            Shots = 56,
            CeilingStep = 6,
            Occupied = (r, ang, i, depth) =>
                CometHead(r, i, depth) <= COMET_HEAD_RADIUS || CometTail(r, ang, i, depth),
            Colour = (r, ang, i, depth) =>
                CometHead(r, i, depth) <= COMET_HEAD_RADIUS
                    ? Sector(ang, 0f, 3, new[] { BallType.Type1, BallType.Type9, BallType.Type7 })
                    : Band((depth - 1 - i) / COMET_TAIL_SEGMENT,
                        new[] { BallType.Type4, BallType.Type9, BallType.Type7, BallType.Type1 }),
        };

        /// <summary>
        /// A hollow funnel wall around nothing — the one level here whose inside is EMPTY, so every shot is a
        /// shot at a curved two-cell wall, and the wall turns as it descends: a window a few cells wide
        /// corkscrews down it, and the colouring is panes that follow the same twist (<see cref="Lantern"/>'s
        /// course-roll trick sheared by height), so nothing on it is either a horizontal band (the
        /// <see cref="DropTest"/> trap) or a vertical stave of dozens. <b>Brown debuts one pane over from
        /// orange</b>, the warm pair of #152, on the scene whose void backdrop keeps every warm tone legible.
        /// The wall pinches slightly toward its tip, so the silhouette reads as a vortex touching down rather
        /// than as a pipe.
        /// <para>
        /// Measured: 472 balls, margin 1, nothing alone or paired, nothing recoloured, colour counts 86–102;
        /// every colour's best single shot is 6 % (29–33 balls) — no plate anywhere, the panes' point. The
        /// budget prices a pane a shot: 20 panes against 64 shots. (The window's width and seat both took a
        /// correction — see the geometry comments — and the counts above are from after it.)
        /// </para>
        /// </summary>
        private static Design Vortex() => new()
        {
            File = "Vortex.json",
            Name = "Vortex",
            Grid = 12,
            Depth = 20,
            FieldLevels = 30,
            Scene = SceneKind.Space,
            Sky = NEBULA_SKY,
            Music = MUSIC_NEBULA,
                Balls = BALLS_NEBULA,
            Shots = 64,
            CeilingStep = 6,
            Occupied = (r, ang, i, depth) => VortexWall(r, ang, i, depth),
            Colour = (r, ang, i, depth) =>
                Band(SectorIndex(ang, -i * VORTEX_TURNS_PER_LEVEL, VORTEX_PANES)
                     + (depth - 1 - i) / VORTEX_COURSE * 2,
                    new[] { BallType.Type10, BallType.Type9, BallType.Type2, BallType.Type5, BallType.Type4 }),
        };

        /// <summary>
        /// Three rails on a slowly turning orbit, tied by a full deck ring every fourth level — a carousel
        /// seen from its axle, and the tallest level of the block. Where <see cref="Helix"/> is two heavy
        /// strands, this is a FRAME: the mass is in the repetition, every rail is thin, and the decks are
        /// what keeps a cut rail's remainder hanging (the rung lesson, taken from two strands to three).
        /// <b>Silver debuts here, banded against white and black on the neighbouring rails</b> — the #152
        /// trio the tints were designed to hold apart, told apart in play for the first time. A deck cell
        /// takes the colour of the rail whose third of the turntable it sits on, the rung rule again: a deck
        /// of its own colour would be a tax of five-ball groups on the magazine's even draw.
        /// <para>
        /// Measured: 586 balls — the biggest of the block (Onion still holds the game's record at 959) —
        /// margin 1, nothing alone or paired, nothing
        /// recoloured (the decks every fourth level are what lets the rails sit at the 1.5 the Helix records
        /// as pinching); best single shots 4–8 %, bands of 26–27. The budget prices its ~22 bands at 72.
        /// </para>
        /// </summary>
        private static Design Carousel() => new()
        {
            File = "Carousel.json",
            Name = "Carousel",
            Grid = 11,
            Depth = 24,
            FieldLevels = 34,
            Scene = SceneKind.Space,
            Sky = NEBULA_SKY,
            Music = MUSIC_NEBULA,
                Balls = BALLS_NEBULA,
            Shots = 72,
            CeilingStep = 5,
            Occupied = (r, ang, i, depth) => CarouselRail(r, ang, i) != 0 || CarouselDeck(r, i, depth),
            Colour = (r, ang, i, depth) =>
            {
                int rail = CarouselRail(r, ang, i);
                if (rail == 0) rail = CarouselNearestRail(ang, i);

                return Band(i / CAROUSEL_SEGMENT + (rail - 1) * 2,
                    new[] { BallType.Type11, BallType.Type3, BallType.Type4, BallType.Type6, BallType.Type8 });
            },
        };

        /// <summary>
        /// A trunk that forks: eight levels of solid column under the glass, then two arms that corkscrew
        /// apart and down for half a turn, each ending in a bulb — one descending front becoming two the gun
        /// has to alternate between, which no other tall level asks. <b>Navy and olive debut as the two
        /// bulbs</b>, hanging off arms segmented in blue/cyan and green/white respectively — each #152 colour
        /// literally growing out of its rival (#152's pairs) — and each is the block's scarce-colour lesson
        /// before the finale: a colour that exists only as the fruit at the bottom of one arm.
        /// <para>
        /// The trunk is wedged vertically, not banded: its three colours each run the full eight levels, so
        /// cutting any one leaves the arms anchored through the others (<see cref="Horn"/>'s shell rule; a
        /// banded trunk would be the block's one-shot trap, the whole level on the top band).
        /// </para>
        /// <para>
        /// Measured: 344 balls, margin 1, nothing alone or paired, nothing recoloured; each bulb is one
        /// 37-ball group whose cut drops exactly itself (10 %), and the deepest cascade is a high cyan
        /// segment taking the arm below it — 101 balls, 29 %, the block's best hidden shot. Stable hanging
        /// unshot for 40 s in the running game.
        /// </para>
        /// </summary>
        private static Design Wishbone() => new()
        {
            File = "Wishbone.json",
            Name = "Wishbone",
            Grid = 13,
            Depth = 22,
            FieldLevels = 32,
            Scene = SceneKind.Space,
            Sky = NEBULA_SKY,
            Music = MUSIC_NEBULA,
                Balls = BALLS_NEBULA,
            Shots = 54,
            CeilingStep = 5,
            Occupied = (r, ang, i, depth) =>
                WishboneTrunk(r, i, depth) || WishboneArm(r, ang, i, depth) != 0,
            Colour = (r, ang, i, depth) =>
            {
                if (WishboneTrunk(r, i, depth))
                    return Sector(ang, 0f, 3, new[] { BallType.Type2, BallType.Type3, BallType.Type4 });

                int arm = WishboneArm(r, ang, i, depth);

                //The bulbs: the bottom levels of each arm, one colour apiece - the debut colours as fruit
                if (i < WISHBONE_BULB_LEVELS) return arm == 1 ? BallType.Type12 : BallType.Type13;

                int below = depth - WISHBONE_TRUNK_LEVELS - i;
                return arm == 1
                    ? Band(below / WISHBONE_SEGMENT, new[] { BallType.Type3, BallType.Type5 })
                    : Band(below / WISHBONE_SEGMENT, new[] { BallType.Type2, BallType.Type4 });
            },
        };

        /// <summary>
        /// The finale, and the one level in the game that plays <b>every colour it has</b>: fourteen beads on
        /// two counter-turning strands, each bead its own colour with the strand below it hanging in
        /// that colour too — thirteen colours across fourteen packets, so nothing anywhere is a plate and the
        /// magazine's even draw over the live colours is the difficulty itself. The strands pass each other
        /// three times on the way down (counter-rotation does what <see cref="Helix"/> needed rungs for), so
        /// cutting a top bead strands nothing: the rest of that strand still hangs off the other at the
        /// passes.
        /// <para>
        /// Hard the way the owner asked the campaign to end (#182), and hard by SCARCITY rather than by mass:
        /// a bead and its strand tail is a group of a couple dozen, the best single shot in the level is a
        /// fraction of what any other level offers, and the ceiling steps at the Quarry finale's own cadence.
        /// </para>
        /// <para>
        /// Measured: 308 balls, margin 1, nothing alone or paired, nothing recoloured; twelve of the
        /// thirteen colours' best shots are 5–10 % and the deepest cascade anywhere is 24 % — no plate, no
        /// guillotine (see the geometry comment for the first cut's 85 % one and what fixed it). Hung unshot
        /// for 35 s in the running game without sagging near the line, where the first cut lost itself in
        /// eight seconds.
        /// </para>
        /// </summary>
        private static Design Garland() => new()
        {
            File = "Garland.json",
            Name = "Garland",
            Grid = 12,
            Depth = 20,
            FieldLevels = 30,
            Scene = SceneKind.Space,
            Sky = NEBULA_SKY,
            Music = MUSIC_NEBULA,
                Balls = BALLS_NEBULA,
            Shots = 54,
            CeilingStep = 4,
            Occupied = (r, ang, i, depth) => GarlandStrand(r, ang, i, depth) != 0,
            Colour = (r, ang, i, depth) =>
            {
                int strand = GarlandStrand(r, ang, i, depth);

                //Which bead this cell hangs from: the bead at or above its own level. The two strands are
                //offset well apart in the palette so the beads facing each other across the axis differ,
                //and 7 beads a strand against 13 colours puts every colour somewhere (0..6 and 7..13 mod 13).
                int bead = (depth - 1 - i) / GARLAND_BEAD_EVERY;

                return ALL_THIRTEEN[((strand == 1 ? 0 : GARLAND_PALETTE_OFFSET) + bead) % ALL_THIRTEEN.Length];
            },
        };

        #endregion

        #region The nebula levels' second hang (#255)

        //THE NEBULA'S SECOND FIVE (#255), hung after Garland in the same place at the same hour: the arena
        //in deep space, dome NEBULA_SKY, MUSIC_NEBULA, and the block's own law kept verbatim - every level
        //TALL (FieldLevels 30-34, framed from its floor up), every silhouette OPEN, and each of the five a
        //different KIND of tall that the first five did not already own:
        //  Sail      - a flown PLANE: a two-ply sheet on two halyards with a probe slung under it.
        //  Analemma  - one closed STRAND that crosses itself at the waist, the sun's year hung as a thread.
        //  Binary    - two BODIES on their own lanyards, welded into one closed loop by a stream.
        //  Kepler    - a PULSING STACK of nested courses, the packing itself as the load-bearing joint.
        //  Orrery    - five FLOORS that touch nothing but three countable pins each.
        //
        //The first five spent this block's colour DEBUTS (orange, brown, silver, navy, olive - #152). These
        //five have nothing left to introduce, so what the judge's gateNotes rule on instead is who stands
        //next to whom: every one of the thirteen is on the table from the first level of the hang.
        //
        //THREE FIGURES IN THE SPECS WERE ARITHMETIC ERRORS AND ARE CORRECTED HERE, each at the line that
        //carries it: the Analemma's centreline radius (4.0 reached the field wall once the graft's knobs
        //were added), the Orrery's ring ladder (its own spec claimed a 6.8 outer radius "keeps |x|,|z| <= 6"
        //on grid 15, which is false - a cell on the axis bearing at r 6.8 IS column 14 of 15), and Binary's
        //secondary seat. Every other figure is the spec's, as amended by its gateNotes.

        /// <summary>
        /// A solar sail flown from two halyards with a probe slung beneath it - <b>the pack's only flown
        /// PLANE</b>, and the one silhouette in the game that is neither a solid nor a strand. Nine columns
        /// wide, thirteen levels tall and two plies thick, hung by its two top corners; cut one halyard's
        /// lower band and the sheet drops that shoulder and slews from the other, the largest swinging AREA
        /// anywhere in the campaign, with the probe pendulating under it.
        /// <para>
        /// <b>The sheet is two cells thick and that is structural, not a look</b> - the Pictures' own rule
        /// (#130), which this reuses rather than re-derives: one ply is a wall whose cross-level contacts
        /// all fall on the same parity offsets, two plies knit through the half-shift. It is drawn in RAW
        /// columns (<see cref="SAIL_X_MIN"/>..<see cref="SAIL_X_MAX"/>, z <see cref="SAIL_Z"/> and the next)
        /// because a rectangle stated in the centred frame wobbles half a cell a level against the lattice;
        /// only the probe, which is round, is measured in the centred frame.
        /// </para>
        /// <para>
        /// <b>The five stripes run DIAGONALLY on (x + level)</b>, <see cref="SAIL_STRIPE_CELLS"/> cells to a
        /// stripe over a span of 0..20, the last one taking the remainder through the min() clamp. A
        /// diagonal band is the file's classic lonely-ball trap (see <see cref="FindLonelyBalls"/>) and is
        /// safe here for one reason worth stating: at four cells wide and two plies deep every stripe cell
        /// has orthogonal neighbours of its own colour on its own level, across the plies and straight up.
        /// </para>
        /// <para>
        /// Gate watch (#255), in the gateNotes' order. UNSHOT SAG FIRST, and the spec's own pre-authorised
        /// remedy is taken up front rather than after a measurement this tool cannot make: each halyard is
        /// <see cref="SAIL_HALYARD_WIDE"/> columns wide instead of the spec's 1.4-radius post, so the
        /// unsupported top edge between the two feet is <b>3 cells, not 7</b>. Measure the belly anyway in
        /// the Testbed; if it still creeps, the next tune is widening the feet inward (a fourth column each)
        /// before anything else is touched. Second, the s3/s4 orphan recount the gateNotes demanded: the
        /// halyard feet root into stripe s3 (port) and s4 (starboard), so green is the guillotine - the
        /// arithmetic over this exact layout gives <b>238 of 352 balls, 67 %</b>, against the spec's
        /// estimate of 72 and the gate's 90, and magenta is second at 50 %. Third, corner remnants: the
        /// stripes come out 18-72 balls in one group each, nothing standing alone, so the clamp's own corner
        /// is not the only one that is safe. Expected (computed, NOT yet measured by the tool): 352 balls,
        /// 10 standing groups, lateral margin 2.
        /// </para>
        /// </summary>
        private static Design Sail() => new()
        {
            File = "Sail.json",
            Name = "Sail",
            Grid = SAIL_GRID,
            Depth = SAIL_DEPTH,
            FieldLevels = SAIL_FIELD_LEVELS,
            Scene = SceneKind.Space,
            Sky = NEBULA_SKY,
            Music = MUSIC_NEBULA,
                Balls = BALLS_NEBULA,
            Shots = 56,
            CeilingStep = 6,
            OccupiedBlock = SailOccupied,
            BlockColour = SailColour,
        };

        //THE SAIL'S OWN FIGURES. Twenty-four levels in a field of 34 - the offset is 10 and even, which is
        //what keeps every course's level parity where it was drawn.
        private const byte SAIL_GRID = 13;
        private const byte SAIL_DEPTH = 24;
        private const byte SAIL_FIELD_LEVELS = 34;

        //The sheet, in raw lattice columns: nine wide out of thirteen (two clear columns a side) and two
        //cells thick in Z, the Pictures' two-ply rule. Levels 4..16 is thirteen courses of cloth.
        private const int SAIL_X_MIN = 2;
        private const int SAIL_X_MAX = 10;
        private const int SAIL_Z = 6;
        private const int SAIL_BOTTOM = 4;
        private const int SAIL_TOP = 16;

        //The halyards. WIDE is three columns rather than the spec's single-cell corner root: it is the
        //gateNotes' own sag remedy taken before the measurement, and it leaves three unsupported cells of
        //top edge instead of seven. FOOT is where they meet the sheet, CAP where the upper band starts.
        private const int SAIL_HALYARD_WIDE = 3;
        private const int SAIL_HALYARD_FOOT = 17;
        private const int SAIL_HALYARD_CAP = 21;

        //The probe, slung under the sheet's middle: a round body, so it is the one part measured in the
        //centred frame, and it hangs on the field axis whatever the level's parity does to the columns.
        private const int SAIL_PROBE_LEVEL = 2;
        private const float SAIL_PROBE = 1.8f;

        //Four cells to a stripe over a span of 0..20, so the fifth takes the remainder through the clamp
        private const int SAIL_STRIPE_CELLS = 4;

        //The sheet's five diagonal stripes, lower-left to upper-right. The port halyard's foot roots into
        //green and the starboard's into navy, which is what makes green the level's guillotine.
        private static readonly BallType[] SAIL_STRIPES =
        {
            BallType.Type5,    //cyan
            BallType.Type7,    //yellow
            BallType.Type6,    //magenta
            BallType.Type2,    //green
            BallType.Type12,   //navy
        };

        private static bool SailOccupied(int x, int z, int i, int depth)
        {
            //The probe first and unconditionally: it is a sphere on the field axis, and at every level the
            //sheet occupies its cells are already the sheet's, so the union costs nothing
            Centred(x, z, i, SAIL_GRID, out float dx, out float dz);
            float dy = (i - SAIL_PROBE_LEVEL) * INV_SQRT_TWO;
            if (dx * dx + dz * dz + dy * dy <= SAIL_PROBE * SAIL_PROBE) return true;

            //Everything else lives on the two plies
            if (z != SAIL_Z && z != SAIL_Z + 1) return false;

            if (i >= SAIL_HALYARD_FOOT)
                return (x >= SAIL_X_MIN && x < SAIL_X_MIN + SAIL_HALYARD_WIDE)
                    || (x > SAIL_X_MAX - SAIL_HALYARD_WIDE && x <= SAIL_X_MAX);

            return i >= SAIL_BOTTOM && i <= SAIL_TOP && x >= SAIL_X_MIN && x <= SAIL_X_MAX;
        }

        private static BallType SailColour(int x, int z, int i)
        {
            //The halyards: a cap band against the glass and a longer band under it. The LOWER band is what
            //the level's physics moment is bought with, so it is the one that gets a colour of its own on
            //each line - cutting it hands the whole sheet to the other halyard and orphans nothing.
            if (i >= SAIL_HALYARD_FOOT)
            {
                bool port = x <= SAIL_X_MIN + SAIL_HALYARD_WIDE - 1;

                if (i >= SAIL_HALYARD_CAP)
                    return port ? BallType.Type10 : BallType.Type11;   //brown / silver, the two anchors

                return port ? BallType.Type1 : BallType.Type8;         //red / black
            }

            //The sheet: five diagonal stripes on x + level, the clamp taking the last one's remainder
            if (i >= SAIL_BOTTOM)
            {
                int diagonal = (x - SAIL_X_MIN) + (i - SAIL_BOTTOM);
                return SAIL_STRIPES[Math.Min(diagonal / SAIL_STRIPE_CELLS, SAIL_STRIPES.Length - 1)];
            }

            return BallType.Type3;   //blue - the probe, one solid body far from the navy stripe
        }

        /// <summary>
        /// The sun's year: the exact figure-eight the sun draws in the sky over twelve months, hung on the
        /// near face of the field as ONE closed strand that crosses itself once at the waist. Twelve months
        /// are twelve bands along the loop, and because a closed loop cut anywhere is still a loop,
        /// <b>no release in this level orphans a single ball</b> - it is the safest design of the hang and
        /// the hardest to aim at, which is exactly the step up from <see cref="Sail"/>'s broad sheet.
        /// <para>
        /// <b>The curve.</b> Radius <see cref="ANALEMMA_RADIUS"/> constant, bearing
        /// <see cref="ANALEMMA_SEAT"/> + <see cref="ANALEMMA_SWING"/> * sin 2t (the seat is +Z, the face the
        /// gun opens on), level <see cref="ANALEMMA_WAIST"/> + <see cref="ANALEMMA_UPPER"/> or
        /// <see cref="ANALEMMA_LOWER"/> times cos t - the asymmetry between the lobes is the real
        /// analemma's and it is what puts the upper lobe on the glass and the lower one at level 2. Near
        /// t = 0 the strand runs almost ALONG the glass, which is why the anchor is a broad arc of 34 cells
        /// rather than a point. The two passes through (seat, waist) at t = pi/2 and 3pi/2 weld into one
        /// four-way knot.
        /// </para>
        /// <para>
        /// <b>The radius is 3.5 and the spec said 4.0.</b> That is a correction, not a taste: at 4.0 with
        /// the grafted knobs the tube reaches 6.0 cells off the axis, which on grid 13 is column 12 of 13 -
        /// <see cref="LateralMargin"/> zero, the documented bounce trap. 3.5 puts the widest cell in column
        /// 11 and leaves the clear column.
        /// </para>
        /// <para>
        /// The graft is Pillars' globule knob: the tube swells by <see cref="ANALEMMA_KNOB"/> where
        /// |cos t| passes <see cref="ANALEMMA_KNOB_FROM"/>, which is the two solstice extremes and about two
        /// levels each. It turns the spec's own worst risk - the lobe sides fusing where they converge -
        /// into a named feature at the only two places they actually converge.
        /// </para>
        /// <para>
        /// Gate watch (#255), in the gateNotes' order. THE LOBE TIPS FIRST: look at the +Z opening view and
        /// confirm the figure still reads as an 8 and not a lumpy column; the tunes, in order, are the lobe
        /// amplitudes (<see cref="ANALEMMA_UPPER"/> / <see cref="ANALEMMA_LOWER"/>) and then dropping
        /// <see cref="ANALEMMA_SWING"/> from 80 to 75 degrees - <b>not</b> the radius, which is now spent on
        /// the margin. Second, the waist crossing runs at about 1.07 cells of lateral offset per level:
        /// run <see cref="Trellis"/>'s contact-count check on levels 10-12, not merely the occupancy gate.
        /// Third, the crossing's four meeting months are cyan, brown, blue and olive - all distinct, so the
        /// weld merges nothing, and the colouring is taken off the measured nearest point on the curve
        /// rather than off the cell's own angle, which is what keeps a band one connected arc. Expected
        /// (computed, NOT yet measured): 492 balls, 12 standing groups, every colour's best single shot
        /// 5-13 %, lateral margin 1.
        /// </para>
        /// </summary>
        private static Design Analemma() => new()
        {
            File = "Analemma.json",
            Name = "Analemma",
            Grid = ANALEMMA_GRID,
            Depth = ANALEMMA_DEPTH,
            FieldLevels = ANALEMMA_FIELD_LEVELS,
            Scene = SceneKind.Space,
            Sky = NEBULA_SKY,
            Music = MUSIC_NEBULA,
                Balls = BALLS_NEBULA,
            Shots = 58,
            CeilingStep = 5,
            Occupied = AnalemmaOccupied,
            Colour = AnalemmaColour,
        };

        //THE ANALEMMA'S OWN FIGURES. Twenty-four levels in a field of 34 - the offset is 10 and even.
        private const byte ANALEMMA_GRID = 13;
        private const byte ANALEMMA_DEPTH = 24;
        private const byte ANALEMMA_FIELD_LEVELS = 34;

        //The centreline. RADIUS is 3.5 and not the spec's 4.0 for the margin's sake - see the design's doc.
        //SEAT is +Z, the face the gun opens on (Cannon.CalculateInitialPositionAndAimTarget), so the whole
        //figure hangs across the opening view rather than edge-on to it.
        private const float ANALEMMA_RADIUS = 3.5f;
        private const float ANALEMMA_SEAT = MathF.PI * HALF;
        private const float ANALEMMA_SWING = 1.3962634f;   //80 degrees of angular half-width
        private const float ANALEMMA_WAIST = 11f;
        private const float ANALEMMA_UPPER = 12f;
        private const float ANALEMMA_LOWER = 9f;

        //The tube and the solstice knobs. KNOB_FROM is where |cos t| starts swelling the tube, which is
        //about two levels at each lobe tip and nowhere else on the loop.
        private const float ANALEMMA_TUBE = 1.4f;
        private const float ANALEMMA_KNOB = 0.5f;
        private const float ANALEMMA_KNOB_FROM = 0.9f;

        //A quarter of a degree a sample. The curve runs about 50 cells of arc, so consecutive samples are
        //a small fraction of a cell apart and the tube cannot be sampled through - and 12 divides it, so a
        //month is a whole number of samples and no band boundary lands mid-sample.
        private const int ANALEMMA_SAMPLES = 1440;

        //The twelve months along t from 0. Adjacent pairs are non-confusable including the wrap (black to
        //white), and the four that meet at the waist crossing - cyan, brown, blue, olive - are all distinct.
        private static readonly BallType[] ANALEMMA_MONTHS =
        {
            BallType.Type4,    //white
            BallType.Type1,    //red
            BallType.Type5,    //cyan
            BallType.Type10,   //brown
            BallType.Type7,    //yellow
            BallType.Type12,   //navy
            BallType.Type2,    //green
            BallType.Type9,    //orange
            BallType.Type3,    //blue
            BallType.Type13,   //olive
            BallType.Type6,    //magenta
            BallType.Type8,    //black
        };

        /// <summary>
        /// The centreline sampled once: x, z, the height in the same units r is in, and the tube's radius at
        /// that sample. Built rather than written out because the shape is a formula, and built ONCE because
        /// every cell of the field asks it the same question twice - are you inside, and which month.
        /// </summary>
        private static readonly float[,] ANALEMMA_CURVE = BuildAnalemmaCurve();

        private static float[,] BuildAnalemmaCurve()
        {
            float[,] curve = new float[ANALEMMA_SAMPLES, 4];

            for (int s = 0; s < ANALEMMA_SAMPLES; s++)
            {
                float t = s * MathF.Tau / ANALEMMA_SAMPLES;
                float cos = MathF.Cos(t);

                float ang = ANALEMMA_SEAT + ANALEMMA_SWING * MathF.Sin(2f * t);
                float level = ANALEMMA_WAIST + (cos >= 0f ? ANALEMMA_UPPER : ANALEMMA_LOWER) * cos;

                curve[s, 0] = ANALEMMA_RADIUS * MathF.Cos(ang);
                curve[s, 1] = ANALEMMA_RADIUS * MathF.Sin(ang);

                //A level index times 1/sqrt(2), the spacing BallsMap.GetRealPosition puts between levels -
                //the same measure the cell is read in below, so only the difference of the two matters
                curve[s, 2] = level * INV_SQRT_TWO;
                curve[s, 3] = ANALEMMA_TUBE
                    + ANALEMMA_KNOB * MathF.Max(0f, MathF.Abs(cos) - ANALEMMA_KNOB_FROM) / (1f - ANALEMMA_KNOB_FROM);
            }

            return curve;
        }

        /// <summary>
        /// The sample of <see cref="ANALEMMA_CURVE"/> this cell is closest to, by CLEARANCE rather than by
        /// distance: the tube's radius varies along the loop, so the sample a cell belongs to is the one it
        /// is furthest inside, and asking for the nearest centreline point instead would colour a knob's
        /// cells off the neighbouring month at the two tips.
        /// </summary>
        /// <param name="clearance">Distance past the tube's own surface, so zero or less is inside it.</param>
        private static int AnalemmaNearest(float r, float ang, int i, out float clearance)
        {
            float px = r * MathF.Cos(ang);
            float pz = r * MathF.Sin(ang);
            float py = i * INV_SQRT_TWO;

            int best = 0;
            clearance = float.MaxValue;

            for (int s = 0; s < ANALEMMA_SAMPLES; s++)
            {
                float dx = px - ANALEMMA_CURVE[s, 0];
                float dz = pz - ANALEMMA_CURVE[s, 1];
                float dy = py - ANALEMMA_CURVE[s, 2];

                float gap = MathF.Sqrt(dx * dx + dz * dz + dy * dy) - ANALEMMA_CURVE[s, 3];
                if (gap >= clearance) continue;

                clearance = gap;
                best = s;
            }

            return best;
        }

        //depth goes unread by both: the curve states its own levels in layout indices, which is the frame
        //Emit hands i in, and the design would have to be redrawn rather than rescaled if the depth moved
        private static bool AnalemmaOccupied(float r, float ang, int i, int depth)
        {
            AnalemmaNearest(r, ang, i, out float clearance);
            return clearance <= 0f;
        }

        private static BallType AnalemmaColour(float r, float ang, int i, int depth) =>
            ANALEMMA_MONTHS[AnalemmaNearest(r, ang, i, out _) * ANALEMMA_MONTHS.Length / ANALEMMA_SAMPLES];

        /// <summary>
        /// Two stars on their own lanyards, joined by an accretion stream: the only level in the campaign
        /// whose whole structure is <b>one closed loop of two masses</b> - glass, lanyard A, primary,
        /// stream, secondary, lanyard B, glass - so every cut rebalances a two-body system instead of
        /// destroying it. Pop the red band and the primary, the heavier body, drops onto the stream's leash
        /// and the pair counter-swings like a real binary; pop the yellow stream first instead and you get
        /// two pendulums that clink.
        /// <para>
        /// <b>The loop is the whole gate-2 argument.</b> Either lanyard alone carries BOTH stars through the
        /// stream, so no band release can strand anything, and the worst single shot in the level is the
        /// primary's brown shell taking its own olive heart with it (38 % of 402 balls by the arithmetic
        /// over this layout). Both stars are shell-and-heart for that reason - a solid star would be one
        /// enormous group, and a hidden heart is the level's only reveal.
        /// </para>
        /// <para>
        /// The graft is Magnetar's down-jet: <see cref="BINARY_JET"/> radius, four levels, hung off the
        /// primary's bottom cap, and it is left in the shell's own brown, which is the first of the graft's
        /// two offered readings - the jet fuses into the shell as one group and the recount above already
        /// includes it. It trails visibly through every counter-swing.
        /// </para>
        /// <para>
        /// Gate watch (#255), in the gateNotes' order. THE POST-CUT CONFIGURATION FIRST, not the unshot one:
        /// after red goes, lanyard B carries both stars and the jet through the stream, and the spec's own
        /// first tune for that stretch is taken up front here - <see cref="BINARY_LANYARD"/> is <b>1.6, not
        /// the spec's 1.4</b>, because this tool cannot make the measurement and a thicker lanyard is
        /// parallel constraint chains sharing the load (the lesson GARLAND_STRAND carries in this same
        /// block). Make the cut in the Testbed and measure it anyway; the next tune is raising the secondary
        /// two levels to shorten B, not thickening further. Second, the stream's 0.75 cells-per-level slope
        /// needs <see cref="Trellis"/>'s contact-count check. Third, band seams on the lanyards spawn lonely
        /// balls - the arithmetic here says the smallest group in the level is the silver heart at 8 balls
        /// and every band is 20 or more, but re-verify the twelve-colour separations after any repair pass.
        /// The secondary's seat is 3.3 and not the spec's 3.5, which is margin arithmetic: at 3.5 with its
        /// own radius it reaches column 13 of 15 on the unshifted levels.
        /// </para>
        /// </summary>
        private static Design Binary() => new()
        {
            File = "Binary.json",
            Name = "Binary",
            Grid = BINARY_GRID,
            Depth = BINARY_DEPTH,
            FieldLevels = BINARY_FIELD_LEVELS,
            Scene = SceneKind.Space,
            Sky = NEBULA_SKY,
            Music = MUSIC_NEBULA,
                Balls = BALLS_NEBULA,
            Shots = 60,
            CeilingStep = 5,
            OccupiedBlock = BinaryOccupied,
            BlockColour = BinaryColour,
        };

        //THE BINARY'S OWN FIGURES. Twenty-four levels in a field of 34 - the offset is 10 and even. Every
        //body is seated on the z = 0 plane, so the pair swings ACROSS the opening view rather than into it.
        private const byte BINARY_GRID = 15;
        private const byte BINARY_DEPTH = 24;
        private const byte BINARY_FIELD_LEVELS = 34;

        //The primary: the heavy body, hung high and to port. HEART is a radius rather than a shell
        //thickness, because what has to be guaranteed is that the hidden group is big enough to shoot -
        //a shell stated as a thickness leaves whatever is left over inside, which on the secondary is 8
        //balls and on a smaller body would be one.
        private const float BINARY_PRIMARY_X = -3.0f;
        private const int BINARY_PRIMARY_LEVEL = 13;
        private const float BINARY_PRIMARY = 2.7f;
        private const float BINARY_PRIMARY_HEART = 1.5f;

        //The secondary: lighter, lower and to starboard. Seated at 3.3 rather than the spec's 3.5 so its
        //rim clears the field wall by two columns.
        private const float BINARY_SECONDARY_X = 3.3f;
        private const int BINARY_SECONDARY_LEVEL = 6;
        private const float BINARY_SECONDARY = 2.0f;
        private const float BINARY_SECONDARY_HEART = 1.25f;

        //The two lanyards. 1.6 is the gateNotes' own first tune against the post-cut stretch, taken up
        //front - see the design's doc.
        private const float BINARY_LANYARD = 1.6f;
        private const int BINARY_LANYARD_A_FOOT = 17;
        private const int BINARY_LANYARD_B_FOOT = 9;

        //The accretion stream, from the secondary's shoulder to the primary's flank: a strand whose centre
        //walks SLOPE cells a level, which is inside the lattice's cross-level reach at every course.
        private const float BINARY_STREAM = 1.3f;
        private const float BINARY_STREAM_X = 2.5f;
        private const float BINARY_STREAM_SLOPE = 0.75f;
        private const int BINARY_STREAM_TOP = 12;
        private const int BINARY_STREAM_FOOT = 8;

        //The grafted polar jet, off the primary's bottom cap and in the shell's own colour
        private const float BINARY_JET = 1.1f;
        private const int BINARY_JET_TOP = 9;
        private const int BINARY_JET_FOOT = 6;

        /// <summary>A value times itself, so a squared difference reads once rather than twice.</summary>
        private static float Squared(float value) => value * value;

        /// <summary>
        /// What a cell of the binary is: <c>0</c> nothing, <c>1</c> and <c>2</c> the lanyards, <c>3</c> the
        /// stream, <c>4</c>/<c>5</c> the primary's shell and heart, <c>6</c>/<c>7</c> the secondary's,
        /// <c>8</c> the jet. One function for the shape and the colouring both, as <see cref="ChestPart"/>
        /// is for its box: a hollow body and its contents cannot be cut in one frame and coloured in
        /// another.
        /// <para>
        /// The stars are tested FIRST, so the stream and the lanyards are clipped by them rather than the
        /// other way round - which is the difference between a stream that ends in the primary's flank and
        /// a yellow rod driven through the middle of a star.
        /// </para>
        /// </summary>
        private static int BinaryPart(int x, int z, int i)
        {
            Centred(x, z, i, BINARY_GRID, out float dx, out float dz);

            float primary = MathF.Sqrt(Squared(dx - BINARY_PRIMARY_X) + dz * dz
                                       + Squared((i - BINARY_PRIMARY_LEVEL) * INV_SQRT_TWO));
            if (primary <= BINARY_PRIMARY) return primary <= BINARY_PRIMARY_HEART ? 5 : 4;

            float secondary = MathF.Sqrt(Squared(dx - BINARY_SECONDARY_X) + dz * dz
                                         + Squared((i - BINARY_SECONDARY_LEVEL) * INV_SQRT_TWO));
            if (secondary <= BINARY_SECONDARY) return secondary <= BINARY_SECONDARY_HEART ? 7 : 6;

            if (i >= BINARY_STREAM_FOOT && i <= BINARY_STREAM_TOP)
            {
                float centre = BINARY_STREAM_X - BINARY_STREAM_SLOPE * (i - BINARY_STREAM_FOOT);
                if (Squared(dx - centre) + dz * dz <= BINARY_STREAM * BINARY_STREAM) return 3;
            }

            float lanyard = BINARY_LANYARD * BINARY_LANYARD;

            if (i >= BINARY_LANYARD_A_FOOT && Squared(dx - BINARY_PRIMARY_X) + dz * dz <= lanyard) return 1;
            if (i >= BINARY_LANYARD_B_FOOT && Squared(dx - BINARY_SECONDARY_X) + dz * dz <= lanyard) return 2;

            return i >= BINARY_JET_FOOT && i <= BINARY_JET_TOP
                   && Squared(dx - BINARY_PRIMARY_X) + dz * dz <= BINARY_JET * BINARY_JET ? 8 : 0;
        }

        private static bool BinaryOccupied(int x, int z, int i, int depth) => BinaryPart(x, z, i) != 0;

        private static BallType BinaryColour(int x, int z, int i)
        {
            int part = BinaryPart(x, z, i);

            //The lanyards are banded and everything else is one body a colour. The bands are what the loop
            //is cut with, and each is 20 balls or more so none of them is a chore.
            if (part == 1)
                return i >= 21 ? BallType.Type4      //white
                    : i >= 19 ? BallType.Type3       //blue
                    : BallType.Type1;                //red - the cut the double pendulum is bought with

            if (part == 2)
                return i >= 20 ? BallType.Type2      //green
                    : i >= 16 ? BallType.Type5       //cyan
                    : i >= 12 ? BallType.Type6       //magenta
                    : BallType.Type9;                //orange

            return part switch
            {
                3 => BallType.Type7,     //yellow - the stream
                4 => BallType.Type10,    //brown  - the primary's shell
                5 => BallType.Type13,    //olive  - its heart
                6 => BallType.Type12,    //navy   - the secondary's shell
                7 => BallType.Type11,    //silver - its heart, hidden until the navy pops
                _ => BallType.Type10,    //brown  - the jet, fused into the shell's group by design
            };
        }

        /// <summary>
        /// A string of cannonball-packed pearls: twenty-two square courses on one spine, each nested
        /// exactly in the pockets of the one above, the side pulsing 5-4-3-2-2-3-4-5 down the column like
        /// Shoemaker-Levy 9. <b>The packing IS the load-bearing joint</b> - this is the level that answers
        /// the owner's packing ask, and no other design in the game shows the nesting continuously from the
        /// glass to the floor.
        /// <para>
        /// <b>One line does the whole shape.</b> A course of side s is the s offsets closest to the axis in
        /// that level's own parity - written as the half-open window <c>[-s/2, s/2)</c>, which is exact in
        /// binary and needs no parity branch: on the unshifted levels the offsets are half-integers and an
        /// even course comes out centred, on the shifted ones they are integers and an odd course does, and
        /// where the 2,2 waist inverts that relationship the window simply lands the course half a cell to
        /// port. That inversion is the spec's own "offset by the parity half-shift" and it is checked
        /// arithmetic, not a hope: every interface in the column carries 9, 16, 36 or 64 socket links and
        /// no ball below the anchor course has fewer than 3 contacts.
        /// </para>
        /// <para>
        /// <b>The anchor course is SPLIT and that is what keeps the level legal.</b> Every course is a full
        /// cross-section, so releasing one guillotines everything below it; if the course bonded to the
        /// glass were one colour, one lucky ball would end the level. Silver and navy halve it, red and
        /// green halve the course under it, and each half alone carries the column.
        /// </para>
        /// <para>
        /// Gate watch (#255), in the gateNotes' order. THE GATE-2 ARITHMETIC FIRST, and the gateNotes were
        /// right to demand the recount: the cyan release at d 2 drops <b>233 of 274 balls, 85 %</b>, not the
        /// spec's 82 - everything below d 2 counts 224, not 217 - and the yellow waist checks out at 81 %.
        /// Both are under <see cref="ONE_SHOT_PERCENT"/> and both are deliberate: this level's declared
        /// mechanic is that the ceiling clock hands the best guillotine down into camera, so every shot is a
        /// gamble on waiting against trimming beads off the bottom. Verify the number stays at 85 after any
        /// recolour, and verify <see cref="DropTest"/>'s semantics on the repeated non-touching colours
        /// (cyan at d 2 and d 15, and five others) - it tries every standing group of a colour and keeps the
        /// worst, which is the reading this design needs. Second, unshot spring stretch at the top yellow
        /// waist carrying 216 balls: measure in the Testbed against the death line, and the tune is
        /// promoting the waists to 2x2 plus four corner stitches rather than fattening the courses.
        /// Expected (computed, NOT yet measured): exactly 274 balls, 21 standing groups, lateral margin 3.
        /// </para>
        /// </summary>
        private static Design Kepler() => new()
        {
            File = "Kepler.json",
            Name = "Kepler",
            Grid = KEPLER_GRID,
            Depth = KEPLER_DEPTH,
            FieldLevels = KEPLER_FIELD_LEVELS,
            Scene = SceneKind.Space,
            Sky = NEBULA_SKY,
            Music = MUSIC_NEBULA,
                Balls = BALLS_NEBULA,
            Shots = 54,
            CeilingStep = 4,
            OccupiedBlock = KeplerOccupied,
            BlockColour = KeplerColour,
        };

        //THE KEPLER'S OWN FIGURES. Twenty-two levels in a field of 32 - the offset is 10 and even, which
        //this design needs more literally than most: the whole column is stated in level PARITY.
        private const byte KEPLER_GRID = 11;
        private const byte KEPLER_DEPTH = 22;
        private const byte KEPLER_FIELD_LEVELS = 32;

        //The side of each course, indexed by levels below the glass. Four full beads: a half one bonded to
        //the glass, two whole ones, and a bottom one flaring back out to 5. 4*25 + 6*16 + 6*9 + 6*4 = 274.
        private static readonly int[] KEPLER_COURSE = { 5, 4, 3, 2, 2, 3, 4, 5, 4, 3, 2, 2, 3, 4, 5, 4, 3, 2, 2, 3, 4, 5 };

        //One colour a course, indexed the same way. The first two entries are the SPLIT anchor courses'
        //second halves - KeplerColour tests those two courses itself and never falls through to them for
        //the port side. Repeats (cyan, red, green, magenta, silver, navy, yellow) are always courses far
        //enough apart to be separate standing groups.
        private static readonly BallType[] KEPLER_COURSE_COLOUR =
        {
            BallType.Type12,   //navy    - anchor course, starboard half
            BallType.Type2,    //green   - second course, starboard half
            BallType.Type5,    //cyan    - the 85 % guillotine, out of camera at the start
            BallType.Type7,    //yellow  - the top waist
            BallType.Type7,    //yellow
            BallType.Type2,    //green
            BallType.Type6,    //magenta
            BallType.Type12,   //navy
            BallType.Type4,    //white
            BallType.Type9,    //orange
            BallType.Type3,    //blue    - the middle waist
            BallType.Type3,    //blue
            BallType.Type10,   //brown
            BallType.Type13,   //olive
            BallType.Type8,    //black
            BallType.Type5,    //cyan
            BallType.Type1,    //red
            BallType.Type6,    //magenta - the bottom waist
            BallType.Type6,    //magenta
            BallType.Type11,   //silver
            BallType.Type2,    //green
            BallType.Type7,    //yellow
        };

        /// <summary>
        /// The s offsets closest to the axis, as the half-open window <c>[-s/2, s/2)</c> in both axes - the
        /// whole of the column's geometry, and the reason it needs no parity branch. See the design's doc
        /// for why the window is half-open and what the 2,2 waists do to the alternation.
        /// </summary>
        private static bool KeplerOccupied(int x, int z, int i, int depth)
        {
            Centred(x, z, i, KEPLER_GRID, out float dx, out float dz);

            float half = KEPLER_COURSE[LevelsBelowGlass(i, depth)] * HALF;

            return dx >= -half && dx < half && dz >= -half && dz < half;
        }

        private static BallType KeplerColour(int x, int z, int i)
        {
            int d = LevelsBelowGlass(i, KEPLER_DEPTH);

            //The two anchor courses are split, so that no release can cut the glass itself: each half alone
            //carries the column, and the halves are stated in the centred frame so the split falls on the
            //axis rather than half a cell off it
            if (d < 2)
            {
                Centred(x, z, i, KEPLER_GRID, out float dx, out _);

                //The 5x5 splits 2/3 on integer offsets, the 4x4 evenly on half-integer ones
                if (d == 0) return dx <= -1f ? BallType.Type11 : BallType.Type12;   //silver / navy
                return dx < 0f ? BallType.Type1 : BallType.Type2;                   //red / green
            }

            return KEPLER_COURSE_COLOUR[d];
        }

        /// <summary>
        /// Five free-floating rings widening as they descend, each hung from the one above by FOUR
        /// staggered pins and by nothing else - the deliberate anti-<see cref="Carousel"/>, whose rails
        /// never let go. Shear three of a gap's four pins and everything under it, up to four rings and
        /// hundreds of balls, is left slewing on one pin; the higher the gap you dare, the heavier the swing
        /// and the harder the last pin is to hit while it gyrates.
        /// <para>
        /// <b>The pin quartet is colour-disjoint by rule and that is the whole gate-2 argument</b>: within
        /// any gap the four pins carry four different colours, each absent from both rings the gap joins, so
        /// no single release can take more than one pin from a floor and the stack never loses a storey to
        /// one ball. A ring arc's release leaves its pin hanging off the ring below, still glassed through
        /// the others. The 45-degree stagger between consecutive gaps means no continuous rail exists
        /// anywhere - the eye reads five separate halos hovering under each other.
        /// </para>
        /// <para>
        /// <b>The ring ladder is 0.4 a floor and the spec said 0.6.</b> That is the block's third arithmetic
        /// correction: the spec's own note that an outer radius of 6.8 "keeps |x|,|z| &lt;= 6" on grid 15 is
        /// false - a cell on the axis bearing at that radius is column 14 of 15, i.e.
        /// <see cref="LateralMargin"/> zero and the documented bounce trap. <see cref="ORRERY_RING_STEP"/>
        /// at 0.4 puts ring 5's rim at exactly 6.0, which is the widest thing that leaves the clear column,
        /// and the rings still widen visibly by a whole cell across the stack. The pin orbits are re-seated
        /// to the middle of each new overlap for the same reason.
        /// </para>
        /// <para>
        /// The graft is Pillars' beams-not-plates discipline, applied to the collar: level d 0 is the glass
        /// pad and nothing else, and neither of its halves owns a single gap-A pin cell (the pins are a
        /// level below it and carry gap A's own quartet) - the same one-release-drops-everything trap Pillars
        /// had already designed out.
        /// </para>
        /// <para>
        /// Gate watch (#255), in the gateNotes' order. UNSHOT SAG FIRST AND IT WAS THE CLOSEST CALL IN THE
        /// BLOCK - the gateNotes' first tune was taken up front (<see cref="ORRERY_PIN"/> at <b>1.5, not
        /// the spec's 1.3</b>) because this tool could not then make the measurement. Now it can, and #301
        /// spent the second tune (the fourth pin) and two levers the gateNotes never listed on what it
        /// measured; the constants' own comments carry the traces. The shipped reading is <b>3 of 5 probe
        /// orders lost to swing overshoots of 0.0-0.1 past the game's own allowance</b> - Amphora's exact
        /// profile, and Amphora plays fine - where the drawn level read 5 of 5 gone on its first shots with
        /// the glass at rest. Second, the ratio: 52 standing groups against 72 shots is <b>1.38</b>, far
        /// under the block's usual 3 and at the hard edge of the game (Static 1.43, Colossus 0.98). That is
        /// the price of 60-degree release quanta and it is a real difficulty statement - if play rates it
        /// too hard, the honest easings are shots (72 to ~80 is ratio 1.54) or fewer rings, because arc size
        /// buys structure now, not colour. Third, lonely balls at the sector seams - re-check the repair
        /// pass count after any change. NAME COLLISION, from the gateNotes: the meadow judge has a 'Halo'
        /// candidate, which is why this level is Orrery. Measured: 685 balls in 52 standing groups, best
        /// single shots 2-6 %, lateral margin 1, 0 recoloured, anchor load 37.0.
        /// </para>
        /// </summary>
        private static Design Orrery() => new()
        {
            File = "Orrery.json",
            Name = "Orrery",
            Grid = ORRERY_GRID,
            Depth = ORRERY_DEPTH,
            FieldLevels = ORRERY_FIELD_LEVELS,
            Scene = SceneKind.Space,
            Sky = NEBULA_SKY,
            Music = MUSIC_NEBULA,
                Balls = BALLS_NEBULA,
            Shots = 72,
            //#288: 6 until the owner reported never clearing this level. Six bought twelve descents, 7.20 of
            //the 7.57 the twenty-level layout started with, ending the level 0.37 above the line - under the
            //0.82 the swing probe measured on Chest (see GameplayScreen.CLUSTER_SWING_ALLOWANCE), so an
            //orrery whose ring has broken loose and is swinging was BELOW the line at the end however well
            //it was played. Seven bought ten descents, 6.00. #301 then measured the authored sum too kind to
            //this level in the simulation: a five-gap pin chain stretches and swings under play like nothing
            //else in the pack, and the probe still lost it to a stepped glass a third of the way in at 7.
            //Fourteen buys five descents, 3.00, and leaves 7.40 of the sixteen-level layout's 10.40 - Ghost's
            //and Globe's own step, taken for their own reason: the pressure stays (five descents still come),
            //and the level is decided by the shots rather than by the pendulum.
            CeilingStep = 14,
            Occupied = OrreryOccupied,
            Colour = OrreryColour,
        };

        //THE ORRERY'S OWN FIGURES. Sixteen levels in a field of 32 - the offset is 16 and even. It was
        //twenty in a field of 30 until #301: see OrreryGap for why every gap is now a single level, and
        //what four levels of hang bought back; the field then grew two because the floor margin still had
        //room and this stack spends clearance on swing like nothing else in the pack.
        private const byte ORRERY_GRID = 15;
        private const byte ORRERY_DEPTH = 16;
        private const byte ORRERY_FIELD_LEVELS = 32;

        //The collar: the glass pad, and a pad only - see the design's doc on the Pillars graft.
        private const float ORRERY_COLLAR = 3.2f;

        //The ring ladder. Each ring is an annulus WIDTH cells across, two courses tall, seated STEP further
        //out than the one above it: ring 1 is [2.4, 4.4] and ring 5 is [4.0, 6.0], which is the widest the
        //field's clear column allows. STEP is 0.4 and not the spec's 0.6 - see the design's doc. ⚠ WIDTH
        //stays 2.0, and #301 measured the tempting alternative so nobody tries it again: thinning to 1.5
        //cut the mass a quarter and made the sag WORSE (losing orders at shot 7-11 against 12-17), because
        //an annulus a cell wide is a slender hoop - its stiffness falls faster than its weight, and the
        //stack went rope-like, standing cells three levels up hanging bodily under the death line.
        private const float ORRERY_RING_INNER = 2.4f;
        private const float ORRERY_RING_STEP = 0.4f;
        private const float ORRERY_RING_WIDTH = 2.0f;

        //The pins. FOUR to a gap - the gateNotes' second sag tune, spent on the probe's measurement
        //(#301): a pin of a two-level gap was a ~16-ball group whose single release orphans nothing yet
        //leaves everything below its gap on the two survivors, and one such shot whipped the bottom ring
        //from a held 3.2 clearance to -1.0 with the glass at rest; three pins read 5 of 5 losing orders
        //however the rest of the level was tuned. Each gap staggers 45 degrees from the one above, each
        //pin seated in the middle of the radial overlap of the two rings it joins so it has cells under
        //BOTH of them. 1.5 is the gateNotes' first sag tune, taken up front when the level was drawn.
        private const int ORRERY_PINS = 4;
        private const float ORRERY_PIN = 1.5f;

        private static readonly float[] ORRERY_PIN_ORBIT = { 2.8f, 3.5f, 4.0f, 4.3f, 4.4f };

        //Each ring in SIX 60-degree arcs over a three-colour palette, the seams rotated half a sector a
        //ring - a slow colour corkscrew down the stack, which is what stops the five halos reading as one
        //striped drum. Opposite arcs share an ink and stand 180 degrees apart, so each is its own group.
        //Six and not the three 120-degree arcs it shipped with, and the halving is #301's main finding on
        //this level: a 120-degree release was ~40 balls gone in one frame, and the probe's trace shows
        //what that does twice over - the freed arc falls onto the wider ring below it (nested annuli MUST
        //overlap radially or no pin could join them, so the bombardment is built in), and the 240-degree
        //C left behind slews on its pins - the bottom ring's rim whipped 6 to 10 units below its held
        //line off a single such shot, with nothing orphaned and the glass at rest. At 60 degrees the
        //impulse halves and a 300-degree C stays nearly balanced. The arc
        //count is a colouring fact and the pin count a load-bearing one - merging them would tie a
        //recolour to the structure - and the sector count is now a THIRD thing, the release quantum,
        //tuned against the sag probe rather than against either.
        private const int ORRERY_ARC_SECTORS = 6;
        private const int ORRERY_ARCS = 3;
        private static readonly BallType[][] ORRERY_RING_ARCS =
        {
            new[] { BallType.Type1, BallType.Type5, BallType.Type4 },      //red, cyan, white
            new[] { BallType.Type2, BallType.Type6, BallType.Type7 },      //green, magenta, yellow
            new[] { BallType.Type12, BallType.Type9, BallType.Type11 },    //navy, orange, silver
            new[] { BallType.Type10, BallType.Type8, BallType.Type13 },    //brown, black, olive
            new[] { BallType.Type3, BallType.Type1, BallType.Type5 },      //blue, red, cyan
        };

        //THE LOAD RULE, and the reason no colour can shear a floor: within a gap the four pins take four
        //DIFFERENT colours, and every quartet is disjoint from both of the rings it joins. The fourth
        //entries also stay off the collar's two inks in gap A and are checked against nothing further:
        //pins of different gaps never touch, two ring courses always standing between them.
        private static readonly BallType[][] ORRERY_PIN_COLOURS =
        {
            new[] { BallType.Type13, BallType.Type8, BallType.Type10, BallType.Type12 },   //olive, black, brown, navy
            new[] { BallType.Type12, BallType.Type13, BallType.Type11, BallType.Type3 },   //navy, olive, silver, blue
            new[] { BallType.Type1, BallType.Type4, BallType.Type3, BallType.Type8 },      //red, white, blue, black
            new[] { BallType.Type2, BallType.Type7, BallType.Type6, BallType.Type4 },      //green, yellow, magenta, white
            new[] { BallType.Type11, BallType.Type4, BallType.Type9, BallType.Type2 },     //silver, white, orange, green
        };

        /// <summary>Which ring's two courses this level is, 1..5, or 0 for the collar or a gap.</summary>
        private static int OrreryRing(int d) => d switch
        {
            2 or 3 => 1,
            5 or 6 => 2,
            8 or 9 => 3,
            11 or 12 => 4,
            14 or 15 => 5,
            _ => 0,
        };

        /// <summary>
        /// Which gap this level is in, 1..5, or 0 for the collar or a ring. <b>Every gap is a single
        /// level</b> — B to E were two until #301, and the probe's trace is why they are not any more:
        /// the stack is five gaps of BallSocket chain in series, and at two levels a gap that chain was
        /// soft enough that releasing ONE pin bounced the bottom ring four units — from a held 3.2
        /// clearance to −1.0, under the line for longer than the game forgives, with nothing orphaned
        /// and the glass at rest. One level takes a constraint hop out of every gap (stiffer by a third),
        /// halves the released pin's mass, and hands the four levels saved back to the hang: the bottom
        /// ring starts 2.83 units higher in the same field. The halos still hover — a gap is still a
        /// full level of air with nothing but the four pin discs in it.
        /// </summary>
        private static int OrreryGap(int d) => d switch
        {
            1 => 1,
            4 => 2,
            7 => 3,
            10 => 4,
            13 => 5,
            _ => 0,
        };

        /// <summary>Which pin of its gap the cell is inside, 1..4, or 0 for none.</summary>
        private static int OrreryPin(float r, float ang, int gap)
        {
            float orbit = ORRERY_PIN_ORBIT[gap - 1];

            //Gaps alternate 0/90/180/270 and 45/135/225/315, so no two consecutive gaps put a pin on the
            //same bearing and there is no continuous rail down the stack anywhere
            float stagger = gap % 2 == 0 ? HALF : 0f;

            for (int p = 0; p < ORRERY_PINS; p++)
            {
                float centre = (p + stagger) / ORRERY_PINS * MathF.Tau;
                if (LateralDistanceSquared(r, ang, orbit, centre) <= ORRERY_PIN * ORRERY_PIN) return p + 1;
            }

            return 0;
        }

        private static bool OrreryOccupied(float r, float ang, int i, int depth)
        {
            int d = LevelsBelowGlass(i, depth);

            if (d == 0) return r <= ORRERY_COLLAR;

            int ring = OrreryRing(d);

            if (ring != 0)
            {
                float inner = ORRERY_RING_INNER + ORRERY_RING_STEP * (ring - 1);
                return r >= inner && r <= inner + ORRERY_RING_WIDTH;
            }

            int gap = OrreryGap(d);

            return gap != 0 && OrreryPin(r, ang, gap) != 0;
        }

        private static BallType OrreryColour(float r, float ang, int i, int depth)
        {
            int d = LevelsBelowGlass(i, depth);

            //The collar in halves, which is the top-level rule: neither half owns a pin, so neither can
            //take the stack with it. #301 tried dicing this into 2x2 blocks on the fear that a half is
            //one shootable group bonding half the pad to the glass - and measured the fear idle: the aim
            //is held to a band above the cluster's underside (the game's own tall-level rule), so no
            //shot ever reaches the collar, and the dice bought nothing but ten groups of budget.
            if (d == 0) return SectorIndex(ang, 0f, 2) == 0 ? BallType.Type6 : BallType.Type9;   //magenta / orange

            int ring = OrreryRing(d);

            //Half a sector of seam roll a ring, so consecutive floors never share a seam bearing; the
            //six sectors index a three-colour palette, which is what puts opposite arcs in one ink
            if (ring != 0)
                return ORRERY_RING_ARCS[ring - 1][
                    SectorIndex(ang, (ring - 1) / (2f * ORRERY_ARC_SECTORS), ORRERY_ARC_SECTORS) % ORRERY_ARCS];

            int gap = OrreryGap(d);

            //Colour is only ever asked of an occupied cell, so a gap level's cell IS one of its four pins
            return ORRERY_PIN_COLOURS[gap - 1][OrreryPin(r, ang, gap) - 1];
        }

        #endregion

        #region The nebula levels' own geometry (#182)

        //The block's dome. Inert on the space scene, which replaces the sky (#142) - stated once and matched
        //on every level so DescribeBlock has nothing to report, the Colossus precedent. 13 is what the two
        //other sky-replacing blocks (the cavern, the Moon) pair with, and it is what feeds the balls' rig.
        private const byte NEBULA_SKY = 13;

        //Every colour the game has, in enum order - the Garland's palette. The other levels state their
        //palettes inline like every design in this file; the finale's IS "all of them", so it is named.
        private static readonly BallType[] ALL_THIRTEEN =
        {
            BallType.Type1, BallType.Type2, BallType.Type3, BallType.Type4, BallType.Type5, BallType.Type6,
            BallType.Type7, BallType.Type8, BallType.Type9, BallType.Type10, BallType.Type11, BallType.Type12,
            BallType.Type13,
        };

        //The comet's own geometry. The head is a ball pressed against the glass (its centre less than its
        //radius below it, so the top level carries a real cap of cells to anchor on); the tail path orbits
        //at the radius where the head still reaches laterally at the tail's topmost levels, so the two always
        //touch. The tail thins linearly from ROOT where it leaves the head to TIP at the bottom.
        private const float COMET_HEAD_RADIUS = 3.2f;
        private const float COMET_HEAD_DROP = 2.2f;
        private const float COMET_ORBIT = 2.3f;
        private const float COMET_TURNS_PER_LEVEL = 0.05f;
        private const float COMET_TAIL_TIP = 1.15f;
        private const float COMET_TAIL_ROOT = 1.75f;
        private const int COMET_TAIL_SEGMENT = 4;

        /// <summary>Distance from the head's centre, <see cref="SphereDistance"/>'s law hung from the glass.</summary>
        private static float CometHead(float r, int i, int depth)
        {
            float dy = BelowGlass(i, depth) - COMET_HEAD_DROP;
            return MathF.Sqrt(r * r + dy * dy);
        }

        private static bool CometTail(float r, float ang, int i, int depth)
        {
            Untwist(r, ang, i * COMET_TURNS_PER_LEVEL, out float along, out float across);

            float radius = COMET_TAIL_TIP + (COMET_TAIL_ROOT - COMET_TAIL_TIP) * i / (depth - 1f);
            float dx = along - COMET_ORBIT;

            return dx * dx + across * across <= radius * radius;
        }

        //The vortex's own geometry. The wall is the annulus between CORE and RIM, two cells thick; both radii
        //pinch by TAPER at the tip (linearly - Horn owns the quadratic bell, and this is a funnel, not a
        //horn). The window is a fixed notch in the frame that turns with the panes: window and pane
        //boundaries co-rotate at the same rate, so the window sits at the same place among the panes the
        //whole way down and never bisects a different pane per level.
        private const float VORTEX_CORE = 2.6f;
        private const float VORTEX_RIM = 4.2f;
        private const float VORTEX_TAPER = 1.1f;
        private const float VORTEX_TURNS_PER_LEVEL = 0.045f;

        //Strictly narrower than one pane's tau/5 = 1.257 rad: at the first cut's 0.65 the window (2 x 0.65 =
        //1.3 rad) was a shade WIDER than a pane, so one wedge was permanently swallowed whole and each course
        //showed four colours, not five - only the palette rolling by course kept every colour alive at all
        //(counts ran 78-113 where five equal panes give ~90 each).
        private const float VORTEX_GAP_HALF = 0.55f;

        //The window sits ON a pane boundary, not mid-pane: centred inside a wedge it left two slivers of
        //0.08 rad - a cell wide, orange two-ball islands down the window's both edges - where astride the
        //boundary it takes 0.55 from each neighbour and leaves both a healthy 0.7 rad stripe.
        private const float VORTEX_GAP_PHASE = MathF.Tau / (2f * VORTEX_PANES);
        private const int VORTEX_PANES = 5;
        private const int VORTEX_COURSE = 5;

        private static bool VortexWall(float r, float ang, int i, int depth)
        {
            float pinch = VORTEX_TAPER * (1f - i / (depth - 1f));
            if (r < VORTEX_CORE - pinch || r > VORTEX_RIM - pinch) return false;

            float local = WrapAngle(ang - i * VORTEX_TURNS_PER_LEVEL * MathF.Tau - VORTEX_GAP_PHASE);
            return MathF.Abs(local) > VORTEX_GAP_HALF;
        }

        //The carousel's own geometry. Three rails a third of a turn apart on a slowly turning orbit, a full
        //deck ring every fourth level. The rail is thinner than the Helix's strands - three rails and their
        //decks share the anchoring two strands had to carry alone. The Helix records 1.5 as the thickness
        //that pinches alternate levels and strands rim cells; the decks every fourth level are what lets
        //this design sit at that figure anyway, and the gates' report is the check on that.
        private const int CAROUSEL_RAILS = 3;
        private const float CAROUSEL_ORBIT = 2.7f;
        private const float CAROUSEL_RAIL = 1.5f;
        private const float CAROUSEL_TURNS_PER_LEVEL = 0.011f;
        private const float CAROUSEL_DECK_HALF = 0.85f;
        private const int CAROUSEL_DECK_EVERY = 4;
        private const int CAROUSEL_SEGMENT = 3;

        /// <summary>Which rail the cell is inside, 1..3, or 0 for none.</summary>
        private static int CarouselRail(float r, float ang, int i)
        {
            for (int k = 0; k < CAROUSEL_RAILS; k++)
            {
                float centre = (k / (float)CAROUSEL_RAILS + i * CAROUSEL_TURNS_PER_LEVEL) * MathF.Tau;
                if (LateralDistanceSquared(r, ang, CAROUSEL_ORBIT, centre) <= CAROUSEL_RAIL * CAROUSEL_RAIL)
                    return k + 1;
            }

            return 0;
        }

        /// <summary>
        /// Which rail's third of the turntable an off-rail deck cell sits in, 1..3 — the deck's colour
        /// answer, so a deck half reads as its own rail reaching over (the Helix's rung rule, at three).
        /// </summary>
        private static int CarouselNearestRail(float ang, int i)
        {
            float turns = ang / MathF.Tau - i * CAROUSEL_TURNS_PER_LEVEL;
            int k = (int)MathF.Round(turns * CAROUSEL_RAILS);

            return ((k % CAROUSEL_RAILS) + CAROUSEL_RAILS) % CAROUSEL_RAILS + 1;
        }

        private static bool CarouselDeck(float r, int i, int depth) =>
            (depth - 1 - i) % CAROUSEL_DECK_EVERY == 0 && MathF.Abs(r - CAROUSEL_ORBIT) <= CAROUSEL_DECK_HALF;

        //The wishbone's own geometry. The arms walk outward by SPREAD a level from where the trunk ends and
        //corkscrew by TURNS a level; the orbit clamps at ORBIT_MAX so the bulbs stay inside the field's
        //margin. The bulb is the arm's own radius swelling over the bottom levels - a fruit on the stem
        //rather than a separate body, so it can never detach from its arm by a rounding artefact.
        private const float WISHBONE_TRUNK = 2.15f;
        private const int WISHBONE_TRUNK_LEVELS = 8;
        private const float WISHBONE_ARM = 1.6f;
        private const float WISHBONE_ARM_START = 0.55f;
        private const float WISHBONE_SPREAD = 0.28f;
        private const float WISHBONE_ARM_ORBIT_MAX = 3.3f;
        private const float WISHBONE_TURNS_PER_LEVEL = 0.035f;
        private const int WISHBONE_BULB_LEVELS = 4;
        private const int WISHBONE_SEGMENT = 3;

        private static bool WishboneTrunk(float r, int i, int depth) =>
            i >= depth - WISHBONE_TRUNK_LEVELS && r <= WISHBONE_TRUNK;

        /// <summary>Which arm the cell is inside, 1 or 2, or 0 for none (and 0 on every trunk level).</summary>
        private static int WishboneArm(float r, float ang, int i, int depth)
        {
            int split = depth - WISHBONE_TRUNK_LEVELS;
            if (i >= split) return 0;

            int below = split - i;
            float orbit = MathF.Min(WISHBONE_ARM_ORBIT_MAX, WISHBONE_ARM_START + WISHBONE_SPREAD * below);
            float radius = WishboneArmRadius(i);
            float centre = below * WISHBONE_TURNS_PER_LEVEL * MathF.Tau;

            if (LateralDistanceSquared(r, ang, orbit, centre) <= radius * radius) return 1;
            if (LateralDistanceSquared(r, ang, orbit, centre + MathF.PI) <= radius * radius) return 2;

            return 0;
        }

        //The bulb: the arm swells over the bottom four levels and rounds off at the tip
        private static float WishboneArmRadius(int i) => i switch
        {
            0 => 1.3f,
            1 => 1.9f,
            2 => 2.0f,
            3 => 1.7f,
            _ => WISHBONE_ARM,
        };

        //The garland's own geometry. Two thin strands turning OPPOSITE ways on DIFFERENT orbits - they pass
        //each other every seventh level, and those passes are the anchoring the Helix needed rungs for. The
        //orbits differ deliberately, and the first cut of this design is why: on one shared orbit a crossing
        //is a single merged disc - the only cells on its level - and shooting out that disc's colour severed
        //BOTH strands at once (measured: the best single shot dropped 85 % of the level, a guillotine two
        //levels under the glass, and the outer strand's top bead came out 10 balls because the merge had
        //swallowed it). Off-set orbits keep both strands' cells present at a pass, so the strands anchor
        //each other there and no single colour is ever a level-wide cut. A bead is the strand's radius
        //swelling on every third level, so a bead can never detach from its strand; the strand between two
        //beads hangs in the colour of the bead above it (the rung rule again: a strand colour of its own
        //would be thin groups the magazine taxes shots on).
        //STRAND is a physics figure before it is a look. At 1.15 the strand was one or two cells across, and
        //a chain of BallSocket links that thin, 24 levels deep with nearly all the mass in the beads,
        //STRETCHED under its own weight until the bottom crossed the death line - the level lost itself in
        //eight seconds with no shot fired. Thicker strands are parallel constraint chains sharing the load
        //(the Helix's 1.7 pair never sagged), and the shorter layout plus the passes every seventh level
        //keep any one link from carrying the garland alone.
        private const float GARLAND_ORBIT_INNER = 2.4f;
        private const float GARLAND_ORBIT_OUTER = 3.0f;
        private const float GARLAND_STRAND = 1.45f;
        private const float GARLAND_BEAD = 1.85f;
        private const int GARLAND_BEAD_EVERY = 3;
        private const float GARLAND_TURNS_PER_LEVEL = 1f / 14f;
        private const int GARLAND_PALETTE_OFFSET = 7;

        /// <summary>Which strand the cell is inside, 1 or 2, or 0 for none. 1 wins where the passes overlap.</summary>
        private static int GarlandStrand(float r, float ang, int i, int depth)
        {
            float radius = (depth - 1 - i) % GARLAND_BEAD_EVERY == 0 ? GARLAND_BEAD : GARLAND_STRAND;
            float square = radius * radius;

            float one = i * GARLAND_TURNS_PER_LEVEL * MathF.Tau;
            if (LateralDistanceSquared(r, ang, GARLAND_ORBIT_INNER, one) <= square) return 1;

            float two = MathF.PI - i * GARLAND_TURNS_PER_LEVEL * MathF.Tau;
            return LateralDistanceSquared(r, ang, GARLAND_ORBIT_OUTER, two) <= square ? 2 : 0;
        }

        #endregion
    }
}
