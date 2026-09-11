using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>The Quarry</b>, block 6 of the campaign: its designs, and the helpers no other block's designs use, in
    /// the order <c>Program.cs</c> held them — the play order is <see cref="Main"/>'s, and the block's name, music
    /// and ball style are in the tables there. Split out of <c>Program.cs</c> in #386.
    /// </summary>
    internal static partial class Program
    {

        /// <summary>
        /// A cylinder tiled in 2x2x2 blocks of colour — a chunky mosaic column. Horizontal colour bands
        /// were the first try and they were a <b>one-shot level</b>: a band is a single group, and the top
        /// band is what holds the cluster to the glass, so one matching ball dropped all 387 balls at once.
        /// Blocks keep the graphic look and put all three colours on the anchor layer, which is the rule
        /// every design here has to satisfy — see the drop test in <see cref="Validate"/>.
        /// </summary>
        private static Design Mosaic() => new()
        {
            File = "Four.json",
            Name = "Mosaic",
            Grid = 13,
            Depth = 6,
            //A dark room for the brightest cluster in the game. It played over the sea, which was the wrong
            //pairing twice over: the sea mirrors its dome, so it is a large area of whatever the sky is doing
            //and never a backdrop, and against a lit dusk the CMY blocks had nothing to be vivid against.
            //The requirement that pairing states is DARK, and the Moon meets it: #194 gave the cavern to the
            //Reveal block and moved this to the Quarry, where the sky is replaced with black and MoonSceneConfig
            //drives the whole light rig, so the CMY blocks are still the only saturated thing in the frame. What
            //is given up is "enclosed" — a cavern wraps the cluster and the Moon puts it against a horizon — and
            //that was never the part doing the work here.
            Scene = SceneKind.Moon,
            Sky = 13,
            //The one design that has to be worked rather than triggered: its best shot takes 24 of 387
            //balls, so it wants a budget nearer two shots per block than the four-shot cascades elsewhere
            Shots = 56,
            CeilingStep = 8,
            Music = MUSIC_QUARRY,
                Balls = BALLS_QUARRY,
            Occupied = (r, ang, i, depth) => r <= 4.5f,
            //Blocked on the raw indices rather than on the centred position: the blocks are meant to be
            //lattice-aligned, and the half-cell stagger between levels is the packing showing through.
            BlockColour = (x, z, i) => Band((x / 2) + (z / 2) + (i / 2),
                new[] { BallType.Type5, BallType.Type6, BallType.Type7 }),
        };

        /// <summary>
        /// A stepped cone tiled in 2×2×1 blocks of five colours — the first level of the pack that has to be
        /// aimed rather than triggered. Everything before it is built out of plates and wedges of dozens;
        /// here a block is four balls and there are five colours to draw from, so the useful ball arrives a
        /// fifth of the time and clears four when it does.
        /// </summary>
        private static Design Prism() => new()
        {
            File = "Eight.json",
            Name = "Prism",
            Grid = 15,
            Depth = 6,
            //The Quarry's, since #194. The sea gave this nothing it was chosen for — no note here ever argued
            //it — and it gave up something: the sea mirrors its dome, so it is a large area of whatever the sky
            //is doing rather than a backdrop, which is the same objection Mosaic's own comment makes about
            //having played there. Five colours in blocks of eight want a ground that stays still behind them.
            Scene = SceneKind.Moon,
            Sky = 13,
            Music = MUSIC_QUARRY,
                Balls = BALLS_QUARRY,
            Shots = 60,
            CeilingStep = 6,
            //Stepped, so the silhouette is not another cylinder and the lower steps are reachable early
            Occupied = (r, ang, i, depth) => r <= 5.5f - (depth - 1 - i) * 0.55f,
            //Blocks TWO levels tall (x / 2, z / 2, i / 2), where this shipped with one. One level made the
            //group four balls, and against five colours that is a shot for every four — the widest step is
            //the last thing left standing and it took two dozen shots on its own, which is the "the last
            //storey drags" this was reported as. Two levels doubles every group to eight without touching
            //the shape or the palette, which are the parts worth keeping.
            BlockColour = (x, z, i) => Scatter(x / 2, z / 2, i / 2,
                new[] { BallType.Type1, BallType.Type2, BallType.Type3, BallType.Type5, BallType.Type7 }),
        };

        /// <summary>
        /// The hardest of the generated set and the one that stands in front of Colossus: a full cylinder, blocks
        /// of four, and <b>six</b> colours scattered so no two neighbouring blocks agree by design. There is
        /// no plate to trigger anywhere on it — every shot is a shot at four balls, and the ceiling steps
        /// every five while you take them.
        /// </summary>
        private static Design Static() => new()
        {
            File = "Nine.json",
            Name = "Static",
            Grid = 15,
            //Four deep and not six. The difficulty here is the six colours and the four-ball group, not the
            //tonnage: six deep came out at 555 balls, half again as many as Colossus, and the pack has a weak
            //laptop to run on. Four keeps it in Colossus's family at ~370 while every shot still costs the same.
            Depth = 4,
            //Space until #194, and the Moon is the same airless family with a ground under it — which is what
            //this level wanted from space and did not get: six colours of small blocks against nothing at all
            //read as noise floating in a void, where the Moon's lit horizon gives the cluster somewhere to be.
            Scene = SceneKind.Moon,
            Sky = 13,
            Music = MUSIC_QUARRY,
                Balls = BALLS_QUARRY,
            Shots = 60,
            CeilingStep = 5,
            Occupied = (r, ang, i, depth) => r <= 5.5f,
            BlockColour = (x, z, i) => Scatter(x / 2, z / 2, i,
                new[] { BallType.Type1, BallType.Type2, BallType.Type3, BallType.Type5, BallType.Type6, BallType.Type7 }),
        };

        /// <summary>
        /// A square block with a terraced pit bored up into it from underneath — the quarry itself rather than
        /// another thing cut out of it. The outer wall is a plain square prism and all the geometry is on the
        /// <b>inside</b>: the void is widest at the bottom level and steps in by <see cref="HOPPER_BENCH"/> a level
        /// until it closes, so the player is looking up into four benches of diced colour.
        /// <para>
        /// <b>The hole is the point, and it is the opposite of <see cref="Crown"/>'s.</b> Crown's ring lets a shot
        /// up the axis pass clean through, which is what stops the player spraying at the middle; this one is a
        /// funnel that <i>catches</i> — a ball fired up the axis goes into the pit and lands on whichever bench it
        /// reaches first, so the middle of the level is a target with a shape rather than a hole or a wall.
        /// </para>
        /// <para>
        /// <b>The void widens downwards, which is the only direction a void may widen.</b> The mass is what hangs
        /// off the glass, so the top two levels are left solid — the mouth is 3.6 less 0.9 a level and reaches zero
        /// at i = 4 — and everything below hangs off that lid. A pit widening the other way would hollow out the
        /// anchor layer and stand the whole block on its own rim.
        /// </para>
        /// <para>
        /// <b>The wall is square and the pit is round, and swapping either is a worse level.</b> A round wall is
        /// Mosaic's and Static's silhouette, which the block already has twice; a square pit would leave the bottom
        /// bench 0.9 of a cell wide the whole way round, where a round pit inside a square wall runs 0.9 at the
        /// axis directions and 2.8 at the corners — so the bench that is thinnest where the player is aiming
        /// straight up is thickest at the corners, and every level of it is carried by the wider ring above anyway.
        /// </para>
        /// <para>
        /// Measured: 465 balls — the heaviest thing in the Quarry, against Mosaic's 387, Static's 370 and
        /// Colossus's 364 — margin 1, nothing alone, nothing in a pair, 1 recoloured, six colours running 53 to
        /// 122 balls, thirty standing groups from 4 to 63 with a median of 15, and a best single shot of 63
        /// (13 %). Per level 56, 60, 88, 80, 100 and 81, so the terraces are as drawn. <b>No empty cell is sealed
        /// in</b>, which is the check that says the pit is open from below as designed and no colour is parked
        /// where nothing can reach it.
        /// </para>
        /// </summary>
        private static Design Hopper() => new()
        {
            File = "Hopper.json",
            Name = "Hopper",
            //Thirteen, as Mosaic has, for a wall reaching 4.75: one free column all round
            Grid = 13,
            Depth = 6,
            Scene = SceneKind.Moon,
            Sky = 13,
            Music = MUSIC_QUARRY,
                Balls = BALLS_QUARRY,
            //Prism's pair of numbers: ten steps of ceiling against the ten empty field levels under a six-deep
            //layout, so the budget and the descent run out together. 465 balls against Prism's 351 and six
            //colours against five is where the difficulty is — 7.8 balls a shot, between Static's 6.2 and
            //Colossus's 8.1.
            Shots = 60,
            CeilingStep = 6,
            //Chebyshev at x.75 rather than on the half: the polar round-trip makes an exactly attainable
            //threshold a coin toss per cell, which is what costs One.json's slab four of its hundred balls.
            //The pit's own threshold is compared against r itself, which the emitter builds without
            //trigonometry, and 3.6 - 0.9i never lands on an attainable radius.
            Occupied = (r, ang, i, depth) => Chebyshev(r, ang) <= HOPPER_WALL
                                             && r >= HOPPER_MOUTH - i * HOPPER_BENCH,
            BlockColour = (x, z, i) => Scatter(x / 2, z / 2, i / 2,
                new[] { BallType.Type1, BallType.Type2, BallType.Type3, BallType.Type5, BallType.Type6, BallType.Type7 }),
        };

        #region The quarry levels (#255)

        //THE QUARRY REWORKED (#255): five STRUCTURES hanging over the Moon where the old block hung
        //masses. The block keeps its identity - chunky lattice-aligned blocks of colour, five or six of
        //them, no plate to trigger anywhere - and adds the thing a quarry is actually about: WHAT CARRIES
        //WHAT. Every level here has members (a lintel on pillars, loads on slings, a hanging wall on a
        //seam, courses on corner seats, benches solid behind a face), so a shot is a demolition choice
        //rather than excavation, and the physics moments are earned by cutting the right member instead of
        //granted by a lucky colour.
        //
        //THE ORDER IS A RAMP OF WHAT THE STRUCTURE ASKS FOR: Trilithon (229 balls, pick which pillar
        //falls), Gantry (322, find the four-ball sling under each load), Fault (330, quarry the seam the
        //hanging wall hangs on), Crib (432, unseat the corner welds), Highwall (428, no theatre at all -
        //the near-Colossus ratio does the work).
        //
        //SERVICE COLOURS are the block's one new colour idea: a colour that exists ONLY in a structural
        //member (Gantry's slings and hinge, Fault's breccia seam), so releasing it is a statement about
        //the structure and never a percolation accident. Every service colour is glass-absent by design
        //and its worst release is bounded by construction - Validate's drop test is what holds them to it.

        //The block's dome, the one the quarry has always played under: the Moon replaces the sky with
        //black and MoonSceneConfig drives the whole light rig, so the blocks of colour stay the only
        //saturated thing in the frame (see Mosaic's Scene comment for the finding that chose it).
        private const byte QUARRY_SKY = 13;

        /// <summary>
        /// A megalithic doorway hanging under the glass - the Quarry turned from masses to ARCHITECTURE.
        /// Three lattice-aligned members: a cap slab bonded to the glass (11x5x2, 110 balls), two 3x3x3
        /// pillars (54), and a lintel slung across the bottom (11x3x2, 66 - two courses thick per the sag
        /// rule); 229 balls with the kerf cut. The doorway void between the pillars is open straight
        /// through Z, so the opening +Z camera looks THROUGH the portal.
        /// <para>
        /// <b>The player authors the collapse.</b> The portal is a closed structural loop
        /// (cap-pillar-lintel-pillar-cap): rob one pillar block by block - about six shots - and the
        /// lintel plus the far pillar become a cantilevered L heeling over and springing on the survivor's
        /// socket links, the biggest earned sway in the block, on the side the player chose. The surviving
        /// 3x3 pillar section carries it through roughly thirty cross-level links into cap and lintel:
        /// massive, not slender. The kerf (<see cref="TRILITHON_KERF_X"/>) ships the monument
        /// mid-demolition and quietly tells the player which side is meant to fall first.
        /// </para>
        /// <para>
        /// The gate's checks, in order: measure the loaded L's HEELED-OVER EXTREME against the death line
        /// rather than its rest pose (the lintel bottom is two levels above layout bottom, with eight
        /// growth levels under it in the 15-level field) - the tune is <see cref="TRILITHON_PILLAR_WIDTH"/>
        /// to 4. Then run the drop test on any colour landing tiles in both pillars at the same courses
        /// (the lintel orphan, bounded near 29 % of the cluster by construction - well under
        /// <see cref="ONE_SHOT_PERCENT"/>, verify it stays there). Then count standing groups after
        /// half-shift tile fusion: if fused groups push the shots-per-group ratio under 1.0, re-order
        /// <see cref="TRILITHON_PALETTE"/> or the 3 * ti stride in <see cref="TrilithonColour"/> to
        /// desynchronise the two pillars.
        /// </para>
        /// </summary>
        private static Design Trilithon() => new()
        {
            File = "Trilithon.json",
            Name = "Trilithon",
            Grid = 13,
            Depth = 7,
            //Fifteen rather than the pack's sixteen: the layout is seven deep and Emit refuses an odd
            //offset, so the field gives up one level to keep the parity - eight growth levels stay under
            //the lintel, which is what the heeled-over L swings into.
            FieldLevels = 15,
            Scene = SceneKind.Moon,
            Sky = QUARRY_SKY,
            Music = MUSIC_QUARRY,
                Balls = BALLS_QUARRY,
            //About six shots buy a pillar; the spec prices the whole level at about 1.4 shots a group,
            //the friendliest in the block, which is what makes the smallest cluster its natural opener
            Shots = 42,
            CeilingStep = 5,
            OccupiedBlock = (x, z, i, depth) => TrilithonStone(x, z, i),
            BlockColour = TrilithonColour,
        };

        //THE TRILITHON'S OWN FIGURES. All three members live between x 1 and 11 in a grid of 13, so one
        //free column stands all round (LateralMargin's rule). The cap and the lintel are TWO courses thick
        //per the sag rule; the pillars are TRILITHON_PILLAR_WIDTH by three in plan and three courses tall.
        private const int TRILITHON_SPAN_LO = 1;
        private const int TRILITHON_SPAN_HI = 11;

        //How wide a pillar is in x - THE TUNING CONSTANT the gate named: if the loaded L (lintel plus far
        //pillar on one surviving pillar) stretches past the death line, widen this to 4. The pillars grow
        //inward, to x 1..4 and 8..11, and the doorway narrows by two cells.
        private const int TRILITHON_PILLAR_WIDTH = 3;

        //The kerf (the graft from the judged Kerf design): ONE saw-cut air cell in the outer face of the
        //west pillar, so the monument ships mid-demolition. The pillar stays fully connected through its
        //other 26 cells; it costs nothing structurally and sells the block's fiction from the first view.
        private const int TRILITHON_KERF_X = 1;
        private const int TRILITHON_KERF_Z = 6;
        private const int TRILITHON_KERF_I = 3;

        /// <summary>
        /// Whether a cell is on the monument. Layout level 0 is the BOTTOM, so the lintel is at the bottom
        /// (i 0..1, the full span, three deep in z), the pillars stand on it (i 2..4, one at either end of
        /// the span), and the cap is what bonds to the glass (i 5..6, the full span, five deep). The
        /// doorway void between the pillars is open straight through Z.
        /// </summary>
        private static bool TrilithonStone(int x, int z, int i)
        {
            if (x == TRILITHON_KERF_X && z == TRILITHON_KERF_Z && i == TRILITHON_KERF_I) return false;
            if (x < TRILITHON_SPAN_LO || x > TRILITHON_SPAN_HI) return false;

            //The cap slab: two courses against the glass, five cells deep in z
            if (i >= 5) return z >= 4 && z <= 8;

            //Pillars and lintel share the narrower footprint, three cells deep in z
            if (z < 5 || z > 7) return false;

            //The lintel: two courses across the whole span at the bottom of the layout
            if (i <= 1) return true;

            //The two pillars, one at either end of the span
            return x < TRILITHON_SPAN_LO + TRILITHON_PILLAR_WIDTH
                || x > TRILITHON_SPAN_HI - TRILITHON_PILLAR_WIDTH;
        }

        //Five mutually non-confusable colours for one 2x2x2 tile hash over all three members. The ORDER is
        //part of the design: the tile index walks +1 per x tile, +2 per z tile and +3 per level pair, so
        //no two face-adjacent tiles agree - and the gate says to re-order THIS array (or the 3 * ti stride
        //in TrilithonColour) if half-shift tile fusion welds the two pillars' courses together.
        private static readonly BallType[] TRILITHON_PALETTE =
        {
            BallType.Type1,   //red
            BallType.Type5,   //cyan
            BallType.Type3,   //blue
            BallType.Type7,   //yellow
            BallType.Type2,   //green
        };

        /// <summary>
        /// One 2x2x2 tile hash over cap, pillars and lintel alike, anchored at the monument's own corner
        /// (x 1, z 4, i 0) so every tile index is non-negative - <see cref="Band"/> indexes with a bare %.
        /// Tiles alternate every two cells in every axis, so no horizontal one-colour band exists
        /// anywhere, and the cap's top course spans enough tiles that all five colours stand on the glass
        /// level.
        /// </summary>
        private static BallType TrilithonColour(int x, int z, int i) =>
            Band((x - TRILITHON_SPAN_LO) / 2 + 2 * ((z - 4) / 2) + 3 * (i / 2), TRILITHON_PALETTE);

        /// <summary>
        /// Four quarried blocks hanging from a crane deck on four-ball slings - cut the sling, not the
        /// stone. The deck is two glass-bonded courses (11x9x2, 198 balls); four 3x3x3 loads (108) hang
        /// below it and each load's ONLY route up is its 2x2x1 sling neck (16), so the smartest shot in a
        /// level of 322 balls is at four of them: one sling shot drops a 31-ball load and neck - 10 % of
        /// the cluster, self-contained by construction - where quarrying the stone itself costs about
        /// seven. The loaded deck rides visibly stretched at spawn and recoils upward the instant a sling
        /// is cut: the owner's "bounces like a spring", staged as the block's whole thesis in one image.
        /// <para>
        /// <b>Three slings teach the lesson; the fourth narrows.</b> The +X/+Z sling is the HINGE NECK
        /// (the Kerf graft): black and white 1x2x2 columns running from the gap level down into the load's
        /// own top course. Cutting either column drops that load onto a 1x2 hinge that sags and pendulums;
        /// the second cut releases it. The spec's four sling colours were cyan/orange/black/white with the
        /// graft REPLACING the orange sling by black plus white - that spends two service colours twice
        /// over and lets one cut stage two loads at once, so the single slings here are cyan, orange and
        /// silver, and black and white belong to the hinge alone: every service colour names exactly one
        /// function and none stands on the glass.
        /// </para>
        /// <para>
        /// The gate's checks, in order: spawn stretch of four 27-ball loads on 2x2x1 necks of about ten
        /// links each - the Trellis mode, links exist but too few; the tune is
        /// <see cref="GANTRY_SLING_COURSES"/> to 2, which thickens every sling into a 2x2x2 weld without
        /// touching the occupancy. Then the swinging loads' lowest excursion against the death line (load
        /// bottoms at layout i 2 in a field of 16) - note the spec's raise-the-loads tune would weld the
        /// load tops straight to the deck across the gap, so trim the loads to two courses instead if it
        /// comes to that. Then eyeball black-vs-blue and white-vs-yellow under the Moon dome: the hinge
        /// puts black and white side by side over the deck's blue and yellow tiles.
        /// </para>
        /// </summary>
        private static Design Gantry() => new()
        {
            File = "Gantry.json",
            Name = "Gantry",
            Grid = 15,
            Depth = 8,
            Scene = SceneKind.Moon,
            Sky = QUARRY_SKY,
            Music = MUSIC_QUARRY,
                Balls = BALLS_QUARRY,
            //Sixty-four and not the fifty-six this was priced at, which measured the level at exactly
            //1.00 shots a standing group - the tool's own stated floor, where "a design under 1 with no
            //cascade in it cannot be finished". This one HAS cascades and is built of nothing else (a
            //cut sling drops a whole stone: a nine-ball group takes twenty-seven with it, measured), so
            //it was never the unfinishable case - but sitting a design ON that line leaves it no room
            //for the shot that misses, and the ratio is not comparable with Static's 1.43 in any case,
            //Static having no cascade anywhere on it by design. 1.14 keeps this the tightest of the
            //five and off the floor.
            Shots = 64,
            CeilingStep = 6,
            OccupiedBlock = (x, z, i, depth) => GantrySteel(x, z, i),
            BlockColour = GantryColour,
        };

        //THE GANTRY'S OWN FIGURES. The deck is two courses bonded to the glass (i 6..7, 11 by 9); the four
        //loads are 3x3x3 at i 2..4, their footprints' low corners crossed from the two arrays below, so
        //they clear each other by three cells and every sling is shootable from every orbit angle. The
        //slings sit alone on the gap level between load tops and deck.
        private const int GANTRY_DECK_BASE = 6;
        private const int GANTRY_LOAD_BASE = 2;
        private const int GANTRY_LOAD_TOP = 4;
        private const int GANTRY_SLING_LEVEL = 5;
        private static readonly int[] GANTRY_LOAD_X = { 3, 9 };
        private static readonly int[] GANTRY_LOAD_Z = { 4, 8 };

        //How many courses a sling's COLOUR reaches down from GANTRY_SLING_LEVEL - THE TUNING CONSTANT the
        //gate named. At 1 a sling is the spec's 2x2x1 neck of about ten links; if any of the four
        //stretches at spawn, set 2 and every sling becomes a 2x2x2 weld whose lower course recolours four
        //cells of its own load's top - still one colour, still one shot to prime. Occupancy never changes
        //with it: the i 4 cells are the load's own.
        private const int GANTRY_SLING_COURSES = 1;

        //Which load carries the hinge neck: 3 is the +X/+Z corner, the last the orbit reads
        private const int GANTRY_HINGE_LOAD = 3;

        /// <summary>Which load's 3x3 footprint a cell is over - 0..3 as bx + 2 * bz - or -1 for none.</summary>
        private static int GantryLoad(int x, int z)
        {
            int bx = x >= GANTRY_LOAD_X[1] ? 1 : 0;
            int bz = z >= GANTRY_LOAD_Z[1] ? 1 : 0;

            int dx = x - GANTRY_LOAD_X[bx];
            int dz = z - GANTRY_LOAD_Z[bz];

            return dx >= 0 && dx < 3 && dz >= 0 && dz < 3 ? bx + 2 * bz : -1;
        }

        /// <summary>
        /// Whether a cell is over a load's 2x2 sling footprint - the low corner of the load's own 3x3, so
        /// the neck's down-neighbours land in the load and its up-neighbours in the deck on both level
        /// parities - and which load: 0..3, or -1 for none.
        /// </summary>
        private static int GantrySling(int x, int z)
        {
            int load = GantryLoad(x, z);
            if (load < 0) return -1;

            return x - GANTRY_LOAD_X[load & 1] < 2 && z - GANTRY_LOAD_Z[load >> 1] < 2 ? load : -1;
        }

        private static bool GantrySteel(int x, int z, int i)
        {
            //The deck: two courses bonded to the glass, 11 by 9
            if (i >= GANTRY_DECK_BASE) return x >= 2 && x <= 12 && z >= 3 && z <= 11;

            //The four loads
            if (i >= GANTRY_LOAD_BASE && i <= GANTRY_LOAD_TOP) return GantryLoad(x, z) >= 0;

            //The four slings on the gap level: each load's only route up
            return i == GANTRY_SLING_LEVEL && GantrySling(x, z) >= 0;
        }

        //Deck and loads share these five; the slings take SERVICE colours used nowhere else, so cutting
        //one is a statement about the crane and never a percolation accident.
        private static readonly BallType[] GANTRY_PALETTE =
        {
            BallType.Type1,   //red
            BallType.Type3,   //blue
            BallType.Type7,   //yellow
            BallType.Type2,   //green
            BallType.Type6,   //magenta
        };

        //One service colour per single sling, indexed by GantrySling. Load 3 never reads this array - the
        //hinge neck in GantryColour owns its footprint at every sling course.
        private static readonly BallType[] GANTRY_SLING_COLOURS =
        {
            BallType.Type5,    //cyan
            BallType.Type9,    //orange
            BallType.Type11,   //silver (a cool slate grey - see BallType's own note on why it reads)
        };

        private static BallType GantryColour(int x, int z, int i)
        {
            //The deck first, so nothing below can claim its cells: a 2x2x2 hash of the five shared
            //colours, anchored at its own corner (x 2, z 3, i 6) so every index is non-negative for
            //Band's bare %. The two courses sit three palette steps apart, so no tile continues down.
            if (i >= GANTRY_DECK_BASE)
                return Band((x - 2) / 2 + 2 * ((z - 3) / 2) + 3 * (i - GANTRY_DECK_BASE), GANTRY_PALETTE);

            int sling = GantrySling(x, z);

            //The hinge neck: black and white 1x2x2 columns from the gap level down into the load's own
            //top course - the low-x column black, the high-x one white
            if (sling == GANTRY_HINGE_LOAD && i >= GANTRY_LOAD_TOP)
                return x == GANTRY_LOAD_X[1] ? BallType.Type8 : BallType.Type4;   //black : white

            //The other three slings, GANTRY_SLING_COURSES deep
            if (sling >= 0 && i > GANTRY_SLING_LEVEL - GANTRY_SLING_COURSES)
                return GANTRY_SLING_COLOURS[sling];

            //A load is three 3x3x1 courses of the shared five, cycling +1 per load and +2 per course, so
            //vertical neighbours differ and no two loads read as the same stone
            return Band(GantryLoad(x, z) + 2 * (i - GANTRY_LOAD_BASE), GANTRY_PALETTE);
        }

        /// <summary>
        /// A geological fault: the striped strata on the downthrown block sit two courses lower than on
        /// the upthrown one, and the hanging wall hangs on nothing but the breccia seam. Three volumes,
        /// all five cells deep in z (330 balls): the glass-bonded upthrown slab (5x5x5), the full-height
        /// two-thick seam (2x5x8, 80 - two cells per the wall rule), and the downthrown slab (5x5x5) whose
        /// top NEVER touches the glass. The 125-ball mountainside rides visibly lower from the first
        /// frame, sways when hit, and every seam block quarried makes it lurch harder. The displaced
        /// strata are the packing made legible: the same red/cyan course visibly continues
        /// <see cref="FAULT_THROW"/> lattice levels lower across the seam, so the player reads the throw
        /// straight off the ball rows.
        /// <para>
        /// <b>No fuse here on purpose.</b> The seam is black and white in 2x2x2 blocks, each spanning the
        /// seam's whole two-cell thickness, so every surviving block still bridges glass, upthrown and
        /// downthrown when the other colour is released - the two independent populations are exactly what
        /// makes the level safe, which is why the judged pair's single-colour fuse was refused for it.
        /// </para>
        /// <para>
        /// The gate's checks, in order - and this is the block's closest call: spawn droop and post-hit
        /// lurch of the 125-ball cantilever on the two-thick seam, measured against the death line at the
        /// swing's extreme (the named Trellis failure mode); the tuning constants are
        /// <see cref="FAULT_SEAM_HI"/> to 8 with <see cref="FAULT_DOWN_LO"/> to 9. Then black-vs-blue and
        /// white-vs-yellow legibility in the editor under the Moon dome (they are segregated: a vertical
        /// seam against horizontal strata). Then the drop test on BOTH seam colours. If playtests feel
        /// rushed, spend the clock as CeilingStep 6 to 7 rather than fewer shots - the downthrown slab
        /// already rides low.
        /// </para>
        /// </summary>
        private static Design Fault() => new()
        {
            File = "Fault.json",
            Name = "Fault",
            Grid = 15,
            Depth = 8,
            Scene = SceneKind.Moon,
            Sky = QUARRY_SKY,
            Music = MUSIC_QUARRY,
                Balls = BALLS_QUARRY,
            Shots = 58,
            CeilingStep = 6,
            OccupiedBlock = (x, z, i, depth) => FaultBody(x, z, i) != 0,
            BlockColour = FaultColour,
        };

        //THE FAULT'S OWN FIGURES. The throw is how many levels the downthrown side reads lower; the seam's
        //x extent and the downthrown slab's low edge are THE TUNING CONSTANTS the gate named: if the
        //cantilever stretches, widen the seam (FAULT_SEAM_HI to 8) and narrow the slab (FAULT_DOWN_LO
        //to 9) to compensate. The upthrown slab's own edge follows FAULT_SEAM_LO.
        private const int FAULT_THROW = 2;
        private const int FAULT_SEAM_LO = 6;
        private const int FAULT_SEAM_HI = 7;
        private const int FAULT_DOWN_LO = 8;

        /// <summary>
        /// Which volume a cell is in: 0 none, 1 the upthrown slab, 2 the breccia seam, 3 the downthrown
        /// slab. Layout level 0 is the BOTTOM: the upthrown slab rides high (i 3..7, bonded to the glass),
        /// the downthrown one two levels lower (i 1..5), and the seam runs the full eight levels between
        /// them, so the downthrown slab's only route up is the seam across its x face.
        /// </summary>
        private static int FaultBody(int x, int z, int i)
        {
            if (z < 5 || z > 9) return 0;

            if (x >= 1 && x < FAULT_SEAM_LO) return i >= 3 ? 1 : 0;
            if (x >= FAULT_SEAM_LO && x <= FAULT_SEAM_HI) return 2;
            if (x >= FAULT_DOWN_LO && x <= 12) return i >= 1 && i <= 5 ? 3 : 0;

            return 0;
        }

        //The strata pairs, s3 to s6 bottom-up; s6 reprises s3's pair because the two never touch (s4 and
        //s5 stand between them on both sides of the fault). Adjacent strata use disjoint pairs, so no
        //colour crosses a bedding plane.
        private static readonly BallType[][] FAULT_STRATA =
        {
            new[] { BallType.Type1, BallType.Type5 },   //s3: red and cyan
            new[] { BallType.Type3, BallType.Type7 },   //s4: blue and yellow
            new[] { BallType.Type2, BallType.Type6 },   //s5: green and magenta
            new[] { BallType.Type1, BallType.Type5 },   //s6: red and cyan again
        };

        //The mixed overburden on each slab's top stratum: all six strata colours in 2x2x1 blocks. Note
        //that magenta reaches the glass level only through the upthrown s5 chain, not through this hash -
        //the up-slab's top course spans block sums 0..4 - which Validate's drop test is the check on.
        private static readonly BallType[] FAULT_OVERBURDEN =
        {
            BallType.Type1,   //red
            BallType.Type5,   //cyan
            BallType.Type3,   //blue
            BallType.Type7,   //yellow
            BallType.Type2,   //green
            BallType.Type6,   //magenta
        };

        /// <summary>
        /// The geology, displaced by the throw so the fault reads: a cell's stratum id is its level on the
        /// upthrown side and its level plus <see cref="FAULT_THROW"/> on the downthrown one, both landing
        /// in s3..s7. Strata s3..s6 are one level thick, striped in 2-wide x-strips of their own pair with
        /// one anchor for both slabs, so the strip phase continues across the fault; each slab's top
        /// stratum (s7) is the mixed overburden; the seam is black and white - SERVICE colours used
        /// nowhere else - in 2x2x2 blocks that each span the seam's full thickness.
        /// </summary>
        private static BallType FaultColour(int x, int z, int i)
        {
            int body = FaultBody(x, z, i);

            //The breccia seam: a block is the whole two-cell x thickness, alternating in z and level
            //pairs, so releasing either colour leaves the other's blocks still touching both slabs
            if (body == 2)
                return ((z - 5) / 2 + i / 2) % 2 == 0 ? BallType.Type8 : BallType.Type4;   //black : white

            int stratum = body == 1 ? i : i + FAULT_THROW;   //3..7 on both sides of the fault

            //The overburden: the slab's top course carries the six colours in 2x2x1 blocks, which is what
            //stops either slab's anchor course being a single group
            if (stratum == 7)
                return Band((x - 1) / 2 + (z - 5) / 2, FAULT_OVERBURDEN);

            //A stratum one level thick, striped in 2-wide x-strips of its own pair
            return FAULT_STRATA[stratum - 3][((x - 1) / 2) % 2];
        }

        /// <summary>
        /// Eight courses of quarried slabs stacked log-cabin style and hung upside down: every course is
        /// two 9x3x1 slabs crossing the course above, and consecutive courses overlap ONLY in the four 3x3
        /// corner seats - every cross-level contact in the whole 432-ball design lives in those seats,
        /// which are the lattice's own pocket-nesting exhibited as architecture. Shoot out one seat and
        /// that corner of the stack below drops half a cell and the crib TWISTS - a slow torsional sway no
        /// other level has, because every course is a rigid ring hung at four points. Two DIAGONAL seats
        /// of one interface first (the Stope graft's staging) and the stack see-saws on the remaining pair
        /// before the third cut turns it into the torsion that is Crib's own: two earned-physics beats
        /// from one mechanism.
        /// <para>
        /// <b>No single colour ever unseats a course.</b> The four seats at any interface come out four
        /// different colours by construction (<see cref="CribColour"/>'s rotating phase), so the
        /// one-colour-cap trap cannot occur and the worst release is scattered 9-ball blocks; course 7's
        /// seats and middles land palette indices {1,2,3,4} and {5,0}, so all six colours stand against
        /// the glass.
        /// </para>
        /// <para>
        /// The gate's checks, in order: cumulative load on the top interface FIRST - 378 balls hanging
        /// through four 3x3 seats of about fourteen links each - measured as spawn sag and twist
        /// amplitude; the tune is lengthening the top two courses' slabs so their seats thicken into
        /// larger welds (see <see cref="CribCourse"/>). Then the drop test per corner-phase colour: each
        /// removes one seat per interface on a rotating corner, believed non-severing, verify. Then
        /// confirm the 3x3x1 middles never fuse with seats through the half-shift into a group that
        /// breaks the shots-per-group ratio.
        /// </para>
        /// </summary>
        private static Design Crib() => new()
        {
            File = "Crib.json",
            Name = "Crib",
            Grid = 13,
            Depth = 8,
            Scene = SceneKind.Moon,
            Sky = QUARRY_SKY,
            Music = MUSIC_QUARRY,
                Balls = BALLS_QUARRY,
            Shots = 60,
            CeilingStep = 6,
            OccupiedBlock = (x, z, i, depth) => CribCourse(x, z, i),
            BlockColour = CribColour,
        };

        //THE CRIB'S OWN FIGURES. Eight courses in a 9x9 footprint, one layout level each: even courses are
        //two slabs running along X, odd courses two along Z, 54 balls a course. The top course (i 7, along
        //Z) is bonded to the glass along its full length, and the corner seats are the 3x3 overlaps at the
        //footprint's four corners.
        private const int CRIB_LO = 2;
        private const int CRIB_HI = 10;

        /// <summary>
        /// Whether a cell is on its course's pair of slabs: a course runs the full footprint along its own
        /// axis and keeps only the two outer thirds across it, so the middle third is open straight
        /// through on every level - the see-through crib. The gate's tune lives here: if the top interface
        /// sags, lengthen the top two courses' slabs (widen the kept thirds on i 6 and 7) so their corner
        /// seats thicken into larger welds.
        /// </summary>
        private static bool CribCourse(int x, int z, int i)
        {
            if (x < CRIB_LO || x > CRIB_HI || z < CRIB_LO || z > CRIB_HI) return false;

            //Across the course's own run: even courses run along X and are cut in z, odd the reverse
            int across = i % 2 == 0 ? z : x;

            return across <= CRIB_LO + 2 || across >= CRIB_HI - 2;
        }

        //Six mutually non-confusable colours. The order matters only through the phase walk in CribColour:
        //one step per course and one per corner keeps the four seats of any interface four DIFFERENT
        //colours, which is the design's whole safety argument.
        private static readonly BallType[] CRIB_PALETTE =
        {
            BallType.Type1,   //red
            BallType.Type3,   //blue
            BallType.Type7,   //yellow
            BallType.Type2,   //green
            BallType.Type5,   //cyan
            BallType.Type6,   //magenta
        };

        /// <summary>
        /// Each slab is three 3x3x1 blocks: two corner seats and one middle. A seat's colour is
        /// P6[(i + k) mod 6] with k walking the geometric corners NW, NE, SE, SW, so within a course all
        /// six blocks differ and vertically adjacent seats sit one palette step apart; a middle touches no
        /// other course at all and takes the slab phase (i + 4 + slab) instead.
        /// </summary>
        private static BallType CribColour(int x, int z, int i)
        {
            int cx = (x - CRIB_LO) / 3;   //0, 1, 2 across the footprint's thirds
            int cz = (z - CRIB_LO) / 3;

            //The middle third along the course's own run
            if ((i % 2 == 0 ? cx : cz) == 1)
                return Band(i + 4 + ((i % 2 == 0 ? z : x) >= CRIB_HI - 2 ? 1 : 0), CRIB_PALETTE);

            //A corner seat: k = 0..3 round NW, NE, SE, SW
            int k = cx == 0 ? (cz == 0 ? 0 : 3) : (cz == 0 ? 1 : 2);

            return Band(i + k, CRIB_PALETTE);
        }

        /// <summary>
        /// An open-pit bench wall facing the gun: four terraces of two levels each with two-cell treads,
        /// deep at the top and stepping back as it descends so the staircase faces the opening +Z camera -
        /// 428 balls with the adit cut, the most quarry-literal image in the set. The bench risers expose
        /// the packing in section: each course seats into the pockets of the course behind it, so the
        /// terrace edges render the cannonball stacking as clean 2:2 steps read from the cannon. A
        /// brown-and-silver dike climbs diagonally through all four benches to the glass, a two-wide
        /// thread of treasure. This is the block's STABLE BANKER and its finale-adjacent level: every
        /// column is solid to the glass, no physics theatre by design, and the ratio - priced at about
        /// 1.09 shots a group, deliberately just above Colossus's 0.98 - does the difficulty instead.
        /// <para>
        /// The adit (see <see cref="HighwallBench"/>) is the Stope graft miniaturised: a 3-wide, 2-tall
        /// drift punched clean through the bottom bench's two-cell depth, a mine entrance at the wall's
        /// foot at zero structural cost - every column beside and above it is still solid to the glass.
        /// </para>
        /// <para>
        /// The gate's checks, in order: THE RATIO RAZOR first - count standing groups off the tool's own
        /// printout before committing, and add two to four shots if they exceed 57. Then drop-test brown
        /// and silver explicitly: the dike's blocks fusing with bench blocks through the half-shift into
        /// one giant diagonal component is the level's one percolation risk. Then AimReachability on the
        /// z 4 cells from the -Z orbit side and on the adit's interior faces, re-running gate 1 after the
        /// cut.
        /// </para>
        /// </summary>
        private static Design Highwall() => new()
        {
            File = "Highwall.json",
            Name = "Highwall",
            Grid = 15,
            Depth = 8,
            Scene = SceneKind.Moon,
            Sky = QUARRY_SKY,
            Music = MUSIC_QUARRY,
                Balls = BALLS_QUARRY,
            Shots = 60,
            CeilingStep = 7,
            OccupiedBlock = (x, z, i, depth) => HighwallBench(x, z, i),
            BlockColour = HighwallColour,
        };

        //THE HIGHWALL'S OWN FIGURES. The face is 11 wide (x 2..12) in a grid of 15 and its back stands at
        //z 4; the benches reach from there to HighwallFace(i), so the z-extents per level run 2, 2, 4, 4,
        //6, 6, 8, 8 cells deep from the bottom up and the top course (11x8) is fully bonded to the glass.
        private const int HIGHWALL_X_LO = 2;
        private const int HIGHWALL_X_HI = 12;
        private const int HIGHWALL_Z_LO = 4;

        //Where the dike enters at the wall's foot: two columns climbing one cell in x per level, from
        //x 3..4 at i 0 to x 10..11 against the glass - through all four benches, so the intrusion is
        //present on the anchor level like everything else.
        private const int HIGHWALL_DIKE_LO = 3;

        /// <summary>How far back a level's bench reaches: z 5 at the bottom, stepping +2 a bench.</summary>
        private static int HighwallFace(int i) => HIGHWALL_Z_LO + 1 + 2 * (i / 2);

        /// <summary>
        /// Whether a cell is in the wall, less the adit: a 3-wide, 2-tall drift portal at the wall's foot
        /// (x 6..8, levels 0..1 - the bottom bench is only two cells deep, so the cut punches clean
        /// through and reads as a mine entrance).
        /// </summary>
        private static bool HighwallBench(int x, int z, int i)
        {
            //The adit spans the bottom bench's whole depth, so no z test is needed on the cut itself
            if (i <= 1 && x >= 6 && x <= 8) return false;

            return x >= HIGHWALL_X_LO && x <= HIGHWALL_X_HI
                && z >= HIGHWALL_Z_LO && z <= HighwallFace(i);
        }

        private static bool HighwallDike(int x, int i) =>
            x == HIGHWALL_DIKE_LO + i || x == HIGHWALL_DIKE_LO + 1 + i;

        //Each bench owns a disjoint colour pair (the banded percolation answer), laid as 2x2 blocks in a
        //checker across the bench - a bench is exactly one block tall, so the check is two-dimensional.
        //Consecutive benches share no colour, so nothing crosses a bench boundary.
        private static readonly BallType[][] HIGHWALL_BENCH_PAIRS =
        {
            new[] { BallType.Type1, BallType.Type5 },   //bench 0: red and cyan
            new[] { BallType.Type3, BallType.Type7 },   //bench 1: blue and yellow
            new[] { BallType.Type2, BallType.Type6 },   //bench 2: green and magenta
        };

        //The glass bench's full hash: every lower bench's colour also stands on the top level through it
        private static readonly BallType[] HIGHWALL_PALETTE =
        {
            BallType.Type1,   //red
            BallType.Type5,   //cyan
            BallType.Type3,   //blue
            BallType.Type7,   //yellow
            BallType.Type2,   //green
            BallType.Type6,   //magenta
        };

        private static BallType HighwallColour(int x, int z, int i)
        {
            //The dike overrides every bench: brown and silver in 2x2x2 blocks (a block is the dike's own
            //two-cell width in x), the only colours outside the bench pairs. Drop-test BOTH, per the
            //gate - the one percolation risk is dike blocks fusing diagonally with bench blocks through
            //the half-shift.
            if (HighwallDike(x, i))
                return ((z - HIGHWALL_Z_LO) / 2 + i / 2) % 2 == 0
                    ? BallType.Type10    //brown
                    : BallType.Type11;   //silver

            int bench = i / 2;

            //The glass bench: the six-colour hash, +1 per x block and +2 per z block, so no two touching
            //blocks agree - diagonals step by 3 and -1, never 0 modulo 6
            if (bench == 3)
                return Band((x - HIGHWALL_X_LO) / 2 + 2 * ((z - HIGHWALL_Z_LO) / 2), HIGHWALL_PALETTE);

            //A lower bench: its own pair in a 2x2 checker
            return HIGHWALL_BENCH_PAIRS[bench][
                ((x - HIGHWALL_X_LO) / 2 + (z - HIGHWALL_Z_LO) / 2) % 2];
        }

        #endregion

        /// <summary>
        /// The square (Chebyshev) half-extent of a point the emitter hands over in polar form — the distance
        /// a <b>square</b> shape measures in, as opposed to <see cref="Ring"/>'s round one. Recovered from
        /// the pair rather than passed as a third argument: every design but the slab wants the round radius,
        /// and a shape function taking both would have to ignore one of them at every call site.
        /// </summary>
        private static float Chebyshev(float r, float ang) =>
            r * MathF.Max(MathF.Abs(MathF.Cos(ang)), MathF.Abs(MathF.Sin(ang)));

        /// <summary>
        /// A colour per block that looks unpatterned but is a pure function of the block's coordinates, so a
        /// level is the same every time it is played. An integer hash rather than a <see cref="Random"/>
        /// walked in loop order: the walk's order is an implementation detail of the emitter, and a layout
        /// that changes when that loop is reordered is a layout nobody can reason about.
        /// <para>
        /// Deliberately NOT anti-clustered. Two neighbouring blocks that happen to agree merge into a group
        /// of eight, and those accidents are the level's only breathing room — a scatter forced to alternate
        /// would be uniformly four everywhere, which is a grind rather than a difficulty.
        /// </para>
        /// </summary>
        private static BallType Scatter(int blockX, int blockZ, int level, BallType[] palette)
        {
            //Odd multipliers well apart in magnitude, then a couple of xorshift rounds: enough mixing that
            //neighbouring blocks land on unrelated colours, and cheap enough not to matter at generation time
            uint h = (uint)(blockX * 73856093 ^ blockZ * 19349663 ^ level * 83492791);
            h ^= h >> 13;
            h *= 2654435761;
            h ^= h >> 16;

            return palette[h % (uint)palette.Length];
        }

        //The hopper's pit: how far the wall stands out, how wide the mouth is at the bottom level, and how much
        //the void steps in a level. 3.6 and 0.9 close the pit at i = 4, which leaves the top two levels solid -
        //that lid is what the rest of the block hangs from.
        private const float HOPPER_WALL = 4.75f;
        private const float HOPPER_MOUTH = 3.6f;
        private const float HOPPER_BENCH = 0.9f;
    }
}
