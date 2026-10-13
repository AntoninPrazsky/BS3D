using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;
using static BS3D.Tools.LevelGen.CampaignSet;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>The Quarry</b>, block 6 of the campaign: its designs, and the helpers no other block's designs use, in
    /// the order <c>Program.cs</c> held them — the play order is <see cref="Main"/>'s, and the block's name, music
    /// and ball style are in <see cref="CampaignSet"/>'s tables. Split out of <c>Program.cs</c> in #386.
    /// </summary>
    internal static partial class Program
    {
        //THE QUARRY'S TILE (#398), and it is the block's second density lever rather than a spelling of
        //the first. The owner's verdict on all nine of the block's designs then was one sentence repeated:
        //the level takes too long to finish and is not much of a challenge while it does. How long a level
        //takes is its STANDING-GROUP count far more than its ball count - a shot takes one group - so
        //hollowing the bodies (Design.Hollow) could not answer it alone: a skin cuts a tile in half, which
        //leaves just as many groups paying half as much, and the measurement said exactly that (Mosaic 34
        //groups -> 46, and Highwall, a level here until #742, 45 -> 53, the shortest clear rising from 10 to
        //16 and 23 to 32). So the tile widens with the hollowing: three cells across and two courses tall
        //where the block shipped two and one or two.
        //
        //IT IS THE BLOCK'S OWN RECORDED REMEDY, not a new idea - see Prism, whose tile was doubled in
        //height for this complaint's first appearance ("the widest step is the last thing left standing and
        //it took two dozen shots on its own"). What a tile must not become is a PLATE: the block's identity
        //is that no shot triggers the whole body, and an 18-cell tile hollowed to its skin pays six to ten
        //balls where a plate elsewhere in the campaign pays dozens.
        private const int QUARRY_TILE = 3;
        private const int QUARRY_COURSE = 2;


        /// <summary>
        /// A cylinder tiled in 2x2x2 blocks of colour — a chunky mosaic column. Horizontal colour bands
        /// were the first try and they were a <b>one-shot level</b>: a band is a single group, and the top
        /// band is what holds the cluster to the glass, so one matching ball dropped all 387 balls at once.
        /// Blocks keep the graphic look and put all three colours on the anchor layer, which is the rule
        /// every design here has to satisfy — see the drop test in <see cref="LevelGates.Validate"/>.
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
            //56 until #398, against 34 standing groups. The skin and the wider tile left 27, and the
            //budget follows them down: 1.48 shots a group, the same trade as before on a body a third
            //lighter.
            Shots = 40,
            CeilingStep = 8,
            Music = MUSIC_QUARRY,
                Balls = BALLS_QUARRY,
            Hollow = 2,
            Occupied = (r, ang, i, depth) => r <= 4.5f,
            //Blocked on the raw indices rather than on the centred position: the blocks are meant to be
            //lattice-aligned, and the half-cell stagger between levels is the packing showing through.
            BlockColour = (x, z, i) => Band((x / QUARRY_TILE) + (z / QUARRY_TILE) + (i / QUARRY_COURSE),
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
            //60 until #398, when the tile went to three cells across and the body to a skin: 22 groups
            //became 17, and the old budget was 3.5 shots a group - a level that could be played twice
            //over inside its own allowance.
            Shots = 30,
            CeilingStep = 6,
            //Stepped, so the silhouette is not another cylinder and the lower steps are reachable early
            Hollow = 1,
            Occupied = (r, ang, i, depth) => r <= 5.5f - (depth - 1 - i) * 0.55f,
            //Blocks TWO levels tall (x / 2, z / 2, i / 2), where this shipped with one. One level made the
            //group four balls, and against five colours that is a shot for every four — the widest step is
            //the last thing left standing and it took two dozen shots on its own, which is the "the last
            //storey drags" this was reported as. Two levels doubles every group to eight without touching
            //the shape or the palette, which are the parts worth keeping.
            BlockColour = (x, z, i) => Scatter(x / QUARRY_TILE, z / QUARRY_TILE, i / QUARRY_COURSE,
                new[] { BallType.Type1, BallType.Type2, BallType.Type3, BallType.Type5, BallType.Type7 }),
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
        /// Mosaic's silhouette, two levels earlier; a square pit would leave the bottom
        /// bench 0.9 of a cell wide the whole way round, where a round pit inside a square wall runs 0.9 at the
        /// axis directions and 2.8 at the corners — so the bench that is thinnest where the player is aiming
        /// straight up is thickest at the corners, and every level of it is carried by the wider ring above anyway.
        /// </para>
        /// <para>
        /// Measured since #398's skin (LevelGen, 2026-10-12): 345 balls, against Mosaic's 346 and Colossus's 337,
        /// margin 1, nothing alone, 4 in pairs, nothing recoloured, six colours running 21 to 104 balls, 29
        /// standing groups and a best single shot of 41 (11 %). Per level from the bottom 56, 60, 56, 52, 40 and
        /// 81: the skin took the buried middle of the benches, and the lid against the glass stays whole.
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
            //layout, so the budget and the descent run out together. Six colours against Prism's five is where
            //the difficulty is.
            //60 until #398. The pit lost its buried mass to the skin and its tiles widened to three; 29
            //groups of six colours price at 46, 7.5 balls a shot against the hand-drawn Colossus's 8.1 (until #849).
            Shots = 46,
            CeilingStep = 6,
            //Chebyshev at x.75 rather than on the half: the polar round-trip makes an exactly attainable
            //threshold a coin toss per cell, which is what costs One.json's slab four of its hundred balls.
            //The pit's own threshold is compared against r itself, which the emitter builds without
            //trigonometry, and 3.6 - 0.9i never lands on an attainable radius.
            Hollow = 1,
            Occupied = (r, ang, i, depth) => Chebyshev(r, ang) <= HOPPER_WALL
                                             && r >= HOPPER_MOUTH - i * HOPPER_BENCH,
            BlockColour = (x, z, i) => Scatter(x / QUARRY_TILE, z / QUARRY_TILE, i / QUARRY_COURSE,
                new[] { BallType.Type1, BallType.Type2, BallType.Type3, BallType.Type5, BallType.Type6, BallType.Type7 }),
        };

        #region The quarry's ramp (#742)

        //THE QUARRY'S RAMP (#742). The owner's playtest of 2026-10-03, on the whole chapter: "The Moon levels are very
        //similar and lack hollowness (empty spaces inside the map) and spatial depth. They are often cubes / blocks of
        //same-coloured balls. They can stay, but as the chapter goes on the levels should differ and be more
        //interesting, in the same style." What stood here were #255's five structures and Static, and photographed
        //from the gun they said what he said: each was a wall of tiles, because what made it a structure (a seam, a
        //corner seat, a sling one level tall, a dike) was behind the face, and the camera looks up at the face.
        //Trilithon's doorway was the one hole a player could see.
        //
        //SO FROM THE FOURTH LEVEL ON, EACH LEVEL ADDS ONE IDEA THE ONE BEFORE IT LACKS, AND EVERY IDEA IS AIR THE GUN
        //CAN SEE. Mosaic, Prism and Hopper stay (his "they can stay") and teach the tile. Then:
        //  Archway  a hole through the body: a round arch the stars show through, and its soffit is a target
        //  Cornice  an overhang: a roof running out towards the gun over a pocket of air
        //  Hoist    a floating member: a stone hung under the deck on ONE column, in a colour nothing else wears
        //  Eclipse  two depths: a cool disc hung in front of a larger warm one, a gap of air between them
        //  Geode    a cavity: a dull boulder with a crystal sealed in it (The Reveal's payoff, #743)
        //  Locket   the column and the cavity at once: cut the stone's white neck and it falls away from its crystal
        //  Colossus the closer (#849), two of them in a figure: a giant holding the glass up on both arms, an arch
        //           between its legs and a crystal buried in its chest on a cord up through its head
        //It was hand-drawn until the owner's ruling of 2026-10-12 asked for a generated closer with an idea of its own.
        //
        //THE STYLE IS KEPT: lattice-aligned tiles of colour QUARRY_TILE across and QUARRY_COURSE tall, so a body is
        //still worked a tile at a time and no shot triggers the whole of it. What changed is the silhouette, and
        //that a member with a job (a voussoir, a column, a shell, a crystal) wears colours of its own, so the
        //structure reads off the colours from the stand.
        //
        //THE TILES ARE COLOURED BY A SUM OF THEIR INDICES (QuarryBand), NOT BY Scatter's hash. Three colours hashed
        //per tile percolate: a tile has a neighbour on every face and a third of them agree, and the first draft of
        //these levels measured a 217-ball group in a 533-ball shell (40 % of the level on one shot) and ten groups in
        //a whole level. A sum gives every face neighbour another colour. ⚠ Where the sum runs over all three axes
        //with three colours, the tiles meeting corner to corner across a course (+x, +z, one course up) still agree
        //and weld in pairs and threes: no stride on three colours avoids it, because a tile touches its +x, +z and
        //+x+z neighbours on the course above (the lattice's cross-level step is (0 or +1, 0 or +1) up from an odd
        //level, and a course starts on an even one). Four colours avoid it; two colours weld whole diagonals into
        //stripes, which Cornice's body uses on purpose.
        //
        //WHAT THEY MEASURE (LevelGen, 2026-10-12): balls / standing groups / shots, and the one shot that drops most.
        //  Archway  228 / 22 / 32   10 %                    Cornice  253 / 13 / 22   32 %
        //  Hoist    252 / 19 / 25   66 %, the white column  Eclipse  261 / 23 / 36    6 %
        //  Geode    288 / 28 / 41    9 %                    Locket   301 / 32 / 37   77 %, the white neck
        //  Colossus 337 / 33 / 42    8 %, the heart
        //⚠ NO HEAVIER THAN WHAT THEY REPLACED. #398's ruling on this chapter was "less dense", and the owner's note on
        //Colossus asked for fewer balls, so the six together carry 1,583 balls against the 1,590 of the six levels before
        //them, in 137 standing groups against 185 and 193 shots against 262: each body is the size its idea needs and
        //no larger. Every budget keeps its design's shots a group (1.16 on Locket to 1.69 on Cornice), and every
        //ceiling step about the number of steps down it takes over the budget. Each hung 0 times in 5 in the sag probe (--sag), and a white ball can be
        //landed against either key from the start (from the stand 12 landings on Hoist's column and 7 on Locket's neck,
        //and 14 and 21 more after a walk round). Colossus is lighter than the block it replaced (337 balls against 364)
        //and the chapter's hardest by its other figures: the most standing groups and six colours (with Hopper and
        //Eclipse), and the glass every four shots.
        //
        //THE FILES ARE THE OLD ONES (Trilithon, Gantry, Fault, Crib, Highwall, Nine): a save is keyed by the file,
        //and a file the set stops listing would put six uncleared levels in front of every player already past them
        //(BS3DGame.IsLevelUnlocked opens only up to the first unfinished level), where a level that changes under
        //its file keeps the road open. The online boards start again either way: LevelIdentity hashes the bytes.

        //The block's dome, the one the quarry has always played under: the Moon replaces the sky with black and
        //MoonSceneConfig drives the whole light rig, so the blocks of colour stay the only saturated thing in the
        //frame (see Mosaic's Scene comment for the finding that chose it).
        private const byte QUARRY_SKY = 13;

        /// <summary>
        /// A tile's colour as a sum of its indices along the axes given (a zero stride leaves that axis out), each
        /// index counted from the body's own low corner so no tile is cut short by the field's origin. See the
        /// region's header for which sums weld and which do not.
        /// </summary>
        private static BallType QuarryBand(int x, int z, int i, int x0, int z0, int sx, int sz, int sc, BallType[] palette, int phase = 0) =>
            Band(sx * ((x - x0) / QUARRY_TILE) + sz * ((z - z0) / QUARRY_TILE) + sc * (i / QUARRY_COURSE) + phase, palette);

        /// <summary>
        /// The lowest raw x a body <c>|dx| &lt;= <paramref name="half"/></c> wide reaches on any course (the shifted
        /// courses reach half a cell further down), for <see cref="QuarryBand"/>'s <c>x0</c>: counted from there, the
        /// tiles start on the body's own edge rather than leaving a sliver one cell wide at it.
        /// </summary>
        private static int QuarryLow(float half, byte grid) => (int)MathF.Ceiling(grid * HALF - HALF - half);

        /// <summary>
        /// A wall of quarried blocks with a round arch through it: the chapter's first hole, and one the gun looks
        /// straight through from where it stands, the stars showing in the opening and the soffit the first thing a
        /// shot up the middle lands on. Three cells thick, so the arch is a short tunnel rather than a slot in a sheet,
        /// and open at the bottom, so the jambs hang either side of it like the piers of a bridge.
        /// <para>
        /// <b>The ring is in colours of its own.</b> <see cref="ARCHWAY_VOUSSOIRS"/> wedges alternate two colours the
        /// wall never uses, so the arch reads as an arch from the stand and each voussoir is a group of its own; the
        /// keystone is the middle one. The wall round it is the block's tile in three colours, one tile through its
        /// whole thickness.
        /// </para>
        /// </summary>
        private static Design Archway() => new()
        {
            File = "Trilithon.json",
            Name = "Archway",
            Grid = ARCHWAY_GRID,
            Depth = ARCHWAY_DEPTH,
            Scene = SceneKind.Moon,
            Sky = QUARRY_SKY,
            Music = MUSIC_QUARRY,
            Balls = BALLS_QUARRY,
            Shots = 32,
            CeilingStep = 6,
            OccupiedBlock = (x, z, i, depth) => ArchwayPart(x, z, i) != 0,
            BlockColour = ArchwayColour,
        };

        private const byte ARCHWAY_GRID = 13;
        private const byte ARCHWAY_DEPTH = 10;

        //The wall's half width in world units: 11 cells on the shifted levels and 10 on the others, a brick bond
        private const float ARCHWAY_HALF = 5f;

        //Three cells thick, raw z about the field's middle row
        private const int ARCHWAY_Z_LO = 5;
        private const int ARCHWAY_Z_HI = 7;

        //The opening's half width, which is also its round head's radius, and how high above the layout's bottom
        //course the head springs
        private const float ARCHWAY_SPAN = 2.6f;
        private const float ARCHWAY_SPRING = 1.5f;

        //How deep the ring of voussoirs is, and how many wedges it is cut into: odd, so the middle one is a keystone
        private const float ARCHWAY_RING = 1.6f;
        private const int ARCHWAY_VOUSSOIRS = 7;

        private static readonly BallType[] ARCHWAY_STONE = { BallType.Type1, BallType.Type3, BallType.Type7 };   //red, blue, yellow
        private static readonly BallType[] ARCHWAY_RING_COLOURS = { BallType.Type5, BallType.Type6 };           //cyan, magenta

        /// <summary>0 for air, 1 for the wall, 2 for the ring of voussoirs round the arch's head.</summary>
        private static int ArchwayPart(int x, int z, int i)
        {
            if (z < ARCHWAY_Z_LO || z > ARCHWAY_Z_HI) return 0;

            Centred(x, z, i, ARCHWAY_GRID, out float dx, out _);
            if (MathF.Abs(dx) > ARCHWAY_HALF) return 0;

            //Below the springing the opening is straight-sided and above it a half disc, so the reach from the head's
            //centre is the horizontal offset alone until the head starts
            float rise = i * INV_SQRT_TWO - ARCHWAY_SPRING;
            float reach = MathF.Sqrt(dx * dx + MathF.Max(rise, 0f) * MathF.Max(rise, 0f));

            if (reach < ARCHWAY_SPAN) return 0;
            return rise >= 0f && reach < ARCHWAY_SPAN + ARCHWAY_RING ? 2 : 1;
        }

        private static BallType ArchwayColour(int x, int z, int i)
        {
            if (ArchwayPart(x, z, i) == 2)
            {
                Centred(x, z, i, ARCHWAY_GRID, out float dx, out _);

                //0 at the right-hand springer, pi at the left, so the wedge index runs across the head
                float angle = MathF.Atan2(i * INV_SQRT_TWO - ARCHWAY_SPRING, dx);
                int wedge = Math.Clamp((int)(angle / MathF.PI * ARCHWAY_VOUSSOIRS), 0, ARCHWAY_VOUSSOIRS - 1);

                return ARCHWAY_RING_COLOURS[wedge % 2];
            }

            //One tile through the wall's thickness, so the sum is over x and the course: on three colours that is a
            //tile of its own colour on every side
            return QuarryBand(x, z, i, 0, ARCHWAY_Z_LO, 1, 0, 1, ARCHWAY_STONE);
        }

        /// <summary>
        /// A roof of quarried blocks running out towards the gun over a pocket of air, with a lip hanging from its
        /// front edge and the body it rests on standing back against the far side: the chapter's first OVERHANG, read
        /// from the stand as a ceiling the player is standing under. The body is narrower than the roof, so the roof
        /// overhangs the flanks too and a walk round shows the same thing from the side.
        /// <para>
        /// <b>Roof and body wear different colours</b>, warm over cool, so the pocket between the lip and the body is
        /// read as depth rather than as one wall. The body is two colours on a sum, which welds its tiles along one
        /// diagonal into stripes: the body comes down in a few long pieces and the roof is the work.
        /// </para>
        /// </summary>
        private static Design Cornice() => new()
        {
            File = "Gantry.json",
            Name = "Cornice",
            Grid = CORNICE_GRID,
            Depth = CORNICE_DEPTH,
            Scene = SceneKind.Moon,
            Sky = QUARRY_SKY,
            Music = MUSIC_QUARRY,
            Balls = BALLS_QUARRY,
            Shots = 22,
            CeilingStep = 5,
            OccupiedBlock = (x, z, i, depth) => CornicePart(x, z, i) != 0,
            BlockColour = CorniceColour,
        };

        private const byte CORNICE_GRID = 13;
        private const byte CORNICE_DEPTH = 8;

        //Half widths in world units: the roof, and the narrower body under it. Their low edges are where their tiles
        //are counted from (QuarryLow), so the roof is three tiles across and the body two.
        private const float CORNICE_HALF = 4f;
        private const float CORNICE_BODY_HALF = 2.5f;

        //Raw z: the back of everything, the body's front face, and the roof's front edge
        private const int CORNICE_BACK = 2;
        private const int CORNICE_BODY_FRONT = 4;
        private const int CORNICE_ROOF_FRONT = 9;

        //The roof is the top two courses; the lip hangs one course under its front edge, two cells deep, which is the
        //roof's front row of tiles
        private const int CORNICE_ROOF_BASE = 6;
        private const int CORNICE_LIP_BASE = 5;
        private const int CORNICE_LIP = 2;

        private static readonly BallType[] CORNICE_ROOF = { BallType.Type1, BallType.Type7, BallType.Type6 };   //red, yellow, magenta
        private static readonly BallType[] CORNICE_BODY = { BallType.Type3, BallType.Type5 };                   //blue, cyan

        /// <summary>0 for air, 1 for the body, 2 for the roof, 3 for the lip under its front edge.</summary>
        private static int CornicePart(int x, int z, int i)
        {
            Centred(x, z, i, CORNICE_GRID, out float dx, out _);
            if (z < CORNICE_BACK || z > CORNICE_ROOF_FRONT || MathF.Abs(dx) > CORNICE_HALF) return 0;

            if (i >= CORNICE_ROOF_BASE) return 2;
            if (i >= CORNICE_LIP_BASE && z > CORNICE_ROOF_FRONT - CORNICE_LIP) return 3;

            return z <= CORNICE_BODY_FRONT && MathF.Abs(dx) <= CORNICE_BODY_HALF ? 1 : 0;
        }

        //The roof is one course, so its sum is over x and z; the lip takes the colour of the roof tile it hangs from
        private static BallType CorniceColour(int x, int z, int i) =>
            CornicePart(x, z, i) == 1
                ? QuarryBand(x, z, i, QuarryLow(CORNICE_BODY_HALF, CORNICE_GRID), CORNICE_BACK, 1, 0, 1, CORNICE_BODY)
                : QuarryBand(x, z, i, QuarryLow(CORNICE_HALF, CORNICE_GRID), CORNICE_BACK, 1, 1, 0, CORNICE_ROOF);

        /// <summary>
        /// A stone hung under the deck on one column: the chapter's first FLOATING member, with air all round the
        /// column so the player sees what the stone hangs by. <b>The column is white and nothing else is</b>, so
        /// white is the key: one white ball landed against it lets the whole stone go, and the player who holds one
        /// back for it (Swap is theirs by now) trades a lot of quarrying for one shot. The stone sways on the column
        /// when it is hit.
        /// </summary>
        private static Design Hoist() => new()
        {
            File = "Fault.json",
            Name = "Hoist",
            Grid = HOIST_GRID,
            Depth = HOIST_DEPTH,
            Scene = SceneKind.Moon,
            Sky = QUARRY_SKY,
            Music = MUSIC_QUARRY,
            Balls = BALLS_QUARRY,
            Shots = 25,
            CeilingStep = 5,
            OccupiedBlock = (x, z, i, depth) => HoistPart(x, z, i) != 0,
            BlockColour = HoistColour,
        };

        private const byte HOIST_GRID = 13;
        private const byte HOIST_DEPTH = 10;

        //The deck: two courses against the glass, HOIST_DECK_HALF either side in x (three tiles) and raw z 4..8
        private const int HOIST_DECK_BASE = 8;
        private const float HOIST_DECK_HALF = 4f;
        private const int HOIST_DECK_Z_LO = 4;
        private const int HOIST_DECK_Z_HI = 8;

        //The column: 3x3 raw cells under the deck's middle, three courses tall
        private const int HOIST_COLUMN_BASE = 5;
        private const int HOIST_COLUMN_LO = 5;
        private const int HOIST_COLUMN_HI = 7;

        //The stone: everything below the column, HOIST_STONE_HALF either side in x (two tiles) and raw z 4..8, under
        //the deck's footprint and narrower than it, so from the stand the deck shows past the stone on both sides
        private const float HOIST_STONE_HALF = 2.5f;
        private const int HOIST_STONE_Z_LO = 4;
        private const int HOIST_STONE_Z_HI = 8;

        //Four, because the stone is tiled in all three axes and a sum of three on four colours welds nothing
        private static readonly BallType[] HOIST_STONE = { BallType.Type1, BallType.Type3, BallType.Type7, BallType.Type2 };   //red, blue, yellow, green

        /// <summary>0 for air, 1 for the deck, 2 for the column, 3 for the stone.</summary>
        private static int HoistPart(int x, int z, int i)
        {
            Centred(x, z, i, HOIST_GRID, out float dx, out _);

            if (i >= HOIST_DECK_BASE)
                return MathF.Abs(dx) <= HOIST_DECK_HALF && z >= HOIST_DECK_Z_LO && z <= HOIST_DECK_Z_HI ? 1 : 0;

            if (i >= HOIST_COLUMN_BASE)
                return x >= HOIST_COLUMN_LO && x <= HOIST_COLUMN_HI && z >= HOIST_COLUMN_LO && z <= HOIST_COLUMN_HI ? 2 : 0;

            return MathF.Abs(dx) <= HOIST_STONE_HALF && z >= HOIST_STONE_Z_LO && z <= HOIST_STONE_Z_HI ? 3 : 0;
        }

        private static BallType HoistColour(int x, int z, int i) => HoistPart(x, z, i) switch
        {
            2 => BallType.Type4,   //white, the key
            1 => QuarryBand(x, z, i, QuarryLow(HOIST_DECK_HALF, HOIST_GRID), HOIST_DECK_Z_LO, 1, 1, 0, HOIST_STONE),
            _ => QuarryBand(x, z, i, QuarryLow(HOIST_STONE_HALF, HOIST_GRID), HOIST_STONE_Z_LO, 1, 1, 1, HOIST_STONE, 2),
        };

        /// <summary>
        /// Two bodies at two depths: a cool disc hung near the glass on the gun's side and a larger warm disc behind
        /// it, with a gap between them three balls wide. From the stand the near disc covers the far one's upper left and
        /// the far one shows round it below and to the right, which is the level's name; a walk round slides one
        /// across the other.
        /// <b>Each disc has a palette of its own</b>, so the two never share a group and the eye separates them at
        /// once.
        /// </summary>
        private static Design Eclipse() => new()
        {
            File = "Crib.json",
            Name = "Eclipse",
            Grid = ECLIPSE_GRID,
            Depth = ECLIPSE_DEPTH,
            FieldLevels = ECLIPSE_FIELD_LEVELS,
            Scene = SceneKind.Moon,
            Sky = QUARRY_SKY,
            Music = MUSIC_QUARRY,
            Balls = BALLS_QUARRY,
            Shots = 36,
            CeilingStep = 6,
            OccupiedBlock = (x, z, i, depth) => EclipsePart(x, z, i, depth) != 0,
            BlockColour = EclipseColour,
        };

        private const byte ECLIPSE_GRID = 15;
        private const byte ECLIPSE_DEPTH = 11;
        private const byte ECLIPSE_FIELD_LEVELS = 17;

        //Each disc stands in the (x, height) plane, three cells thick in raw z: radius, how far below the glass its
        //centre hangs, and its sideways offset. Both centres are close enough to the glass that the glass cuts a
        //wide chord off each, which is what each hangs by.
        private const float ECLIPSE_FAR_RADIUS = 4f;
        private const float ECLIPSE_FAR_CENTRE = 2.2f;
        private const int ECLIPSE_FAR_Z_LO = 3;
        private const float ECLIPSE_NEAR_RADIUS = 2.6f;
        private const float ECLIPSE_NEAR_CENTRE = 1.7f;
        private const float ECLIPSE_NEAR_OFFSET = -1.2f;
        private const int ECLIPSE_NEAR_Z_LO = 9;

        private static readonly BallType[] ECLIPSE_FAR = { BallType.Type1, BallType.Type7, BallType.Type6 };    //red, yellow, magenta
        private static readonly BallType[] ECLIPSE_NEAR = { BallType.Type3, BallType.Type5, BallType.Type2 };   //blue, cyan, green

        /// <summary>0 for air, 1 for the near disc, 2 for the far one.</summary>
        private static int EclipsePart(int x, int z, int i, int depth)
        {
            Centred(x, z, i, ECLIPSE_GRID, out float dx, out _);
            float below = BelowGlass(i, depth);

            if (z >= ECLIPSE_NEAR_Z_LO && z < ECLIPSE_NEAR_Z_LO + QUARRY_TILE)
            {
                float ox = dx - ECLIPSE_NEAR_OFFSET, oy = below - ECLIPSE_NEAR_CENTRE;
                return ox * ox + oy * oy <= ECLIPSE_NEAR_RADIUS * ECLIPSE_NEAR_RADIUS ? 1 : 0;
            }

            if (z >= ECLIPSE_FAR_Z_LO && z < ECLIPSE_FAR_Z_LO + QUARRY_TILE)
            {
                float oy = below - ECLIPSE_FAR_CENTRE;
                return dx * dx + oy * oy <= ECLIPSE_FAR_RADIUS * ECLIPSE_FAR_RADIUS ? 2 : 0;
            }

            return 0;
        }

        //Each disc is one tile thick, so the sum is over x and the course
        private static BallType EclipseColour(int x, int z, int i) =>
            z >= ECLIPSE_NEAR_Z_LO
                ? QuarryBand(x, z, i, 0, ECLIPSE_NEAR_Z_LO, 1, 0, 1, ECLIPSE_NEAR)
                : QuarryBand(x, z, i, 0, ECLIPSE_FAR_Z_LO, 1, 0, 1, ECLIPSE_FAR);

        /// <summary>
        /// A dull boulder that is hollow, with a crystal hung in the hollow: The Reveal's payoff (#743) brought to the
        /// quarry. The shell is quarried in three stone colours, dark to light, and the crystal in two the shell never wears, so
        /// the first tile that comes away shows colour where there was stone. Sealed at the start: no straight shot
        /// from anywhere on the gun's walk touches the crystal, and it hangs from the glass on a cord of its own through
        /// the lid, so peeling the shell never takes it down (<see cref="LevelGates.RevealFaults"/>).
        /// </summary>
        private static Design Geode() => new()
        {
            File = "Highwall.json",
            Name = "Geode",
            Grid = GEODE_GRID,
            Depth = GEODE_DEPTH,
            Scene = SceneKind.Moon,
            Sky = QUARRY_SKY,
            Music = MUSIC_QUARRY,
            Balls = BALLS_QUARRY,
            Shots = 41,
            CeilingStep = 7,
            Payoff = (x, z, r, ang, i, depth) => GeodePart(x, z, i, depth) == 2,
            OccupiedBlock = (x, z, i, depth) => GeodePart(x, z, i, depth) != 0,
            BlockColour = (x, z, i) => GeodeColour(x, z, i, GEODE_DEPTH),
        };

        private const byte GEODE_GRID = 13;
        private const byte GEODE_DEPTH = 10;

        //The boulder: an ellipsoid whose centre hangs GEODE_CENTRE below the glass, so the glass cuts a wide flat top
        //off it to hang by. Its foot stops short of the course under its floor, which would otherwise carry one ball.
        private const float GEODE_RADIUS = 4.15f;
        private const float GEODE_HEIGHT = 3.3f;
        private const float GEODE_CENTRE = 2.3f;

        //The cavity: a flatter ellipsoid with two cells of shell round it at its widest, three courses tall between a
        //floor of two and a lid of three. Wide enough that the cells beside the crystal on its own courses are air (the
        //one beside its corner, (2, 1), at 2.24); across a course it still touches the lid, as the cord through it does.
        private const float GEODE_CAVITY_RADIUS = 2.25f;
        private const float GEODE_CAVITY_HEIGHT = 1.3f;
        private const float GEODE_CAVITY_CENTRE = 2.9f;

        //The crystal: a square column of GEODE_CORD from the glass down into the cavity, to GEODE_TIP below the glass
        //(a course of air clear of the floor), magenta down to GEODE_TIP_COLOUR and cyan below it
        private const float GEODE_CORD = 1f;
        private const float GEODE_TIP = 3.4f;
        private const float GEODE_TIP_COLOUR = 2.5f;

        //Three stone colours at three lightnesses, none of them white: white is the key the levels either side of this
        //one teach (Hoist's column, Locket's neck), and here it would be filler. Navy stood in the shell until the first
        //capture: brown and navy both go nearly black on the Moon's metal and could not be told apart at the stand.
        private static readonly BallType[] GEODE_SHELL = { BallType.Type10, BallType.Type11, BallType.Type7 };   //brown, slate, ochre (yellow)

        /// <summary>0 for air, 1 for the shell, 2 for the crystal and its cord.</summary>
        private static int GeodePart(int x, int z, int i, int depth)
        {
            Centred(x, z, i, GEODE_GRID, out float dx, out float dz);
            float below = BelowGlass(i, depth);
            float flat = dx * dx + dz * dz;

            float oy = (below - GEODE_CENTRE) / GEODE_HEIGHT;
            if (flat / (GEODE_RADIUS * GEODE_RADIUS) + oy * oy > 1f) return 0;

            if (MathF.Max(MathF.Abs(dx), MathF.Abs(dz)) <= GEODE_CORD && below <= GEODE_TIP) return 2;

            float cy = (below - GEODE_CAVITY_CENTRE) / GEODE_CAVITY_HEIGHT;
            return flat / (GEODE_CAVITY_RADIUS * GEODE_CAVITY_RADIUS) + cy * cy <= 1f ? 0 : 1;
        }

        private static BallType GeodeColour(int x, int z, int i, int depth)
        {
            if (GeodePart(x, z, i, depth) == 2)
                return BelowGlass(i, depth) <= GEODE_TIP_COLOUR ? BallType.Type6 : BallType.Type5;   //magenta : cyan

            return QuarryBand(x, z, i, 0, 0, 1, 1, 1, GEODE_SHELL);
        }

        /// <summary>
        /// A sealed stone on a white neck under a mount against the glass, with a crystal hung inside it on a cord that
        /// runs down the neck's hollow heart: Hoist's key and Geode's cavity in one level. <b>The neck and the cord never
        /// touch</b> (a sleeve of air stands between them all the way down, and the crystal stands clear of the cavity's
        /// walls), so a white ball landed on the neck lets the whole stone fall away and leaves the crystal hanging in the
        /// open on its cord. Peeling the stone tile by tile gets there too, slower; which way is the player's.
        /// </summary>
        private static Design Locket() => new()
        {
            File = "Nine.json",
            Name = "Locket",
            Grid = LOCKET_GRID,
            Depth = LOCKET_DEPTH,
            Scene = SceneKind.Moon,
            Sky = QUARRY_SKY,
            Music = MUSIC_QUARRY,
            Balls = BALLS_QUARRY,
            Shots = 37,
            CeilingStep = 6,
            Payoff = (x, z, r, ang, i, depth) => LocketPart(x, z, i) == 4,
            OccupiedBlock = (x, z, i, depth) => LocketPart(x, z, i) != 0,
            BlockColour = LocketColour,
        };

        private const byte LOCKET_GRID = 13;
        private const byte LOCKET_DEPTH = 10;

        //From the glass down: the mount (one course), the neck (two), then the stone: its lid (two), the cavity
        //(three, the crystal in its top two) and its floor (two)
        private const int LOCKET_MOUNT_BASE = 9;
        private const int LOCKET_NECK_BASE = 7;
        private const int LOCKET_LID_BASE = 5;
        private const int LOCKET_CAVITY_BASE = 2;

        //Square half extents, about the axis, of the cord (one cell on the shifted courses, two by two on the others)
        //and of the crystal it ends in. Round radii of the air round each: LOCKET_SLEEVE is the least radius holding
        //every cell that touches a cord cell on either parity (the even cord's (0.5, 0.5) touches (1.5, 0.5) on its own
        //course, at 1.58), and LOCKET_CAVITY the same for the crystal (its corner (1, 1) touches (2, 1), at 2.24), so
        //no stone ball can touch either. The neck is the ring outside the sleeve, one cell thick (twelve a course): three
        //courses of it tipped over as the stone was peeled lopsided (the sag probe, 2 of 5), and two hold.
        private const float LOCKET_CORD = 0.5f;
        private const float LOCKET_CRYSTAL = 1f;
        private const float LOCKET_SLEEVE = 1.6f;
        private const float LOCKET_CAVITY = 2.3f;
        private const float LOCKET_NECK = 2.6f;

        //The mount and the stone, round in plan; the stone's top two courses and bottom two are drawn in to round it.
        //The mount is a collar a cell or so wider than the neck. It and the crystal are all a cut neck leaves standing,
        //so it is what keeps the key's drop under the one-shot gate.
        private const float LOCKET_MOUNT = 3.7f;
        private const float LOCKET_STONE = 4f;

        //Where the stone's tiles are counted from, in raw x and z. ⚠ The key must stay the only white: a tile the round
        //rim clips to one ball is recoloured by the repair pass to whatever it touches most of, and beside the neck that
        //is white. Counted from 0 (and with a narrower mount) three balls of the lid and the mount came out white; from
        //1 the rim leaves two balls alone, neither of them beside the neck.
        private const int LOCKET_STONE_TILE_FROM = 1;

        private static readonly BallType[] LOCKET_BODY = { BallType.Type1, BallType.Type3, BallType.Type2 };   //red, blue, green

        /// <summary>0 for air, 1 for the mount, 2 for the neck, 3 for the stone, 4 for the crystal and its cord.</summary>
        private static int LocketPart(int x, int z, int i)
        {
            Centred(x, z, i, LOCKET_GRID, out float dx, out float dz);
            float square = MathF.Max(MathF.Abs(dx), MathF.Abs(dz));
            float r = MathF.Sqrt(dx * dx + dz * dz);

            if (i >= LOCKET_MOUNT_BASE) return square <= LOCKET_CORD ? 4 : r <= LOCKET_MOUNT ? 1 : 0;
            if (i >= LOCKET_NECK_BASE) return square <= LOCKET_CORD ? 4 : r <= LOCKET_SLEEVE ? 0 : r <= LOCKET_NECK ? 2 : 0;

            //Round above as well as below, so the neck stands clear of the lid where the camera looks up at it
            float outer = i switch
            {
                0 => LOCKET_STONE - 1.5f,
                1 => LOCKET_STONE - 0.5f,
                LOCKET_LID_BASE => LOCKET_STONE - 0.3f,
                LOCKET_LID_BASE + 1 => LOCKET_STONE - 0.9f,
                _ => LOCKET_STONE,
            };
            if (r > outer) return 0;

            //The lid: the sleeve runs on through it, so not one stone ball touches the cord
            if (i >= LOCKET_LID_BASE) return square <= LOCKET_CORD ? 4 : r <= LOCKET_SLEEVE ? 0 : 3;

            //The cavity, and the crystal in its top two courses, a course of air clear of the floor
            if (i >= LOCKET_CAVITY_BASE)
                return r > LOCKET_CAVITY ? 3 : square <= LOCKET_CRYSTAL && i > LOCKET_CAVITY_BASE ? 4 : 0;

            return 3;
        }

        private static BallType LocketColour(int x, int z, int i) => LocketPart(x, z, i) switch
        {
            4 => BallType.Type6,   //magenta, the crystal and its cord
            2 => BallType.Type4,   //white, the key
            1 => QuarryBand(x, z, i, 0, 0, 1, 1, 0, LOCKET_BODY, 1),
            _ => QuarryBand(x, z, i, LOCKET_STONE_TILE_FROM, LOCKET_STONE_TILE_FROM, 1, 1, 1, LOCKET_BODY),
        };

        /// <summary>
        /// The chapter's closer (#849): a giant of quarried blocks holding the glass up on both arms, its head between
        /// them and its legs astride under it, with its heart buried in its chest. <b>Two of the ramp's ideas in one body
        /// and the body is the third</b>: the gap between the legs is Archway's opening, the heart is Geode's crystal (a
        /// magenta crystal on a cord of its own that climbs through the shoulders and the head to the glass, so quarrying
        /// the stone never takes it down, <see cref="LevelGates.RevealFaults"/>), and the figure is air the gun can see:
        /// the stars show between each arm and the head and between the legs, from the stand and from a walk round either
        /// way, since the figure is a mirror image across the gun's axis (<see cref="Design.MirrorOutline"/>).
        /// <para>
        /// <b>The limbs wear colours of their own</b>, two in bands a course pair deep, so the arms read as arms and the
        /// legs as legs against the body's tiles; the head, the chest with its shoulders and the hips are the block's tile in
        /// three more, and the heart a sixth. The chapter's key colour is not used (white is Hoist's and Locket's key, and no
        /// member here is one).
        /// </para>
        /// <para>
        /// <b>The legs are the clock and the heart is the prize.</b> The feet hang lowest, so the descending glass brings
        /// them to the line first: cut a thigh band and the shin under it falls with it. The heart's colour is dealt from the
        /// start and can be matched nowhere until the stone round the crystal or its cord is cut into, so digging for it early
        /// turns dead draws live; the player chooses which to spend the first shots on.
        /// </para>
        /// <para>
        /// <b>The chest and the head are solid, and both were hollow first.</b> Drawn as Geode and Locket are, the crystal in
        /// a cavity two cells inside a shell and the cord in a sleeve of air up a ring of a head, the sag probe lost two to
        /// four runs in five: a shell eaten a tile at a time comes apart in strips, and a strip hanging from a few balls of a
        /// lid that hangs from the arms and a one-cell ring unrolls under the line (a dump of one run found a shoulder nine
        /// units below where it hung). Buried in stone with no air round it the crystal is still sealed (no empty cell
        /// touches it) and still hangs by its cord alone, and the same weight in a solid chest and head hung 0 and 1 times
        /// in 5.
        /// </para>
        /// </summary>
        private static Design Colossus() => new()
        {
            File = "Colossus.json",
            Name = "Colossus",
            Grid = COLOSSUS_GRID,
            Depth = COLOSSUS_DEPTH,
            Scene = SceneKind.Moon,
            Sky = QUARRY_SKY,
            Music = MUSIC_QUARRY,
            Balls = BALLS_QUARRY,
            Shots = 42,
            CeilingStep = 4,
            MirrorOutline = true,
            Payoff = (x, z, r, ang, i, depth) => ColossusPart(x, z, i) == COLOSSUS_HEART,
            OccupiedBlock = (x, z, i, depth) => ColossusPart(x, z, i) != 0,
            BlockColour = ColossusColour,
        };

        //Fifteen, for arms reaching six cells either side of the axis and a free column beyond them
        private const byte COLOSSUS_GRID = 15;

        //Twelve courses, from the glass down: the arms and the head (four), the shoulders (one), the chest with the crystal
        //in it (three), the hips (one) and the legs (three). Twelve and not more because the lowest ball starts 5.38 above
        //the line and the glass comes down 0.6 a step (SagProbe.CEILING_DESCENT_PER_STEP, the game's figure), so feet left
        //untouched reach it on the ninth of the budget's ten steps, shot 36: that is the clock.
        private const byte COLOSSUS_DEPTH = 12;
        private const int COLOSSUS_HEAD_BASE = 8;
        private const int COLOSSUS_SHOULDERS = 7;
        private const int COLOSSUS_CHEST_BASE = 4;
        private const int COLOSSUS_HIPS_COURSE = 3;

        //The parts, as ColossusPart answers them
        private const int COLOSSUS_HEAD = 1;
        private const int COLOSSUS_ARM = 2;
        private const int COLOSSUS_STONE = 3;
        private const int COLOSSUS_LEG = 4;
        private const int COLOSSUS_HEART = 5;

        //The cord and the crystal as square half extents about the axis: the cord one ball on the shifted courses and two
        //by two on the others, the crystal three by three and two by two, with two cells of stone in front of it
        private const float COLOSSUS_CORD = 0.5f;
        private const float COLOSSUS_CRYSTAL = 1f;

        //The head, round in plan and as wide as Locket's neck: the arms meet it at the shoulder and lean away from it
        private const float COLOSSUS_HEAD_RADIUS = 2.6f;

        //The arms, raised in a V: the middle of each is COLOSSUS_ARM_BASE off the axis where it leaves the shoulders and
        //leans out COLOSSUS_ARM_LEAN a course to the glass, two cells wide and two or three deep. Against the head at the
        //shoulder, with air opening between them to two cells at the glass.
        private const float COLOSSUS_ARM_BASE = 4f;
        private const float COLOSSUS_ARM_LEAN = 0.5f;
        private const float COLOSSUS_ARM_HALF = 0.75f;
        private const float COLOSSUS_ARM_DEPTH = 1f;

        //The chest, round in plan; the shoulders' course runs out under the arms within their depth
        private const float COLOSSUS_CHEST = 3.2f;
        private const float COLOSSUS_SHOULDERS_HALF = 4f;

        //The hips, round and narrower than the chest
        private const float COLOSSUS_HIPS = 2.6f;

        //The legs at the hips, |dx| between the first two, stepping out by the third a course down: astride, so the gap
        //between them widens to the feet
        private const float COLOSSUS_LEG_IN = 1.5f;
        private const float COLOSSUS_LEG_OUT = 3.5f;
        private const float COLOSSUS_LEG_SPREAD = 0.5f;
        private const float COLOSSUS_LEG_DEPTH = 1f;

        //Where the stone's tiles are counted from, in raw x and z. Of the three phases a tile can take, 1 leaves one ball
        //alone (the repair pass recolours it) and three pairs in 33 groups; 0 leaves none alone but four groups fewer, and
        //2 leaves four alone.
        private const int COLOSSUS_TILE_FROM = 1;

        private static readonly BallType[] COLOSSUS_BODY = { BallType.Type1, BallType.Type3, BallType.Type7 };   //red, blue, yellow
        private static readonly BallType[] COLOSSUS_LIMBS = { BallType.Type5, BallType.Type2 };                 //cyan, green

        /// <summary>0 for air, else one of the <c>COLOSSUS_</c> parts: head, arm, stone (the chest, the shoulders and the
        /// hips), leg, or the heart (the crystal and its cord).</summary>
        private static int ColossusPart(int x, int z, int i)
        {
            Centred(x, z, i, COLOSSUS_GRID, out float dx, out float dz);
            float across = MathF.Abs(dx), deep = MathF.Abs(dz);
            float square = MathF.Max(across, deep);
            float r = MathF.Sqrt(dx * dx + dz * dz);

            if (i >= COLOSSUS_HEAD_BASE)
            {
                float arm = COLOSSUS_ARM_BASE + COLOSSUS_ARM_LEAN * (i - COLOSSUS_HEAD_BASE);
                if (MathF.Abs(across - arm) <= COLOSSUS_ARM_HALF && deep <= COLOSSUS_ARM_DEPTH) return COLOSSUS_ARM;
                if (square <= COLOSSUS_CORD) return COLOSSUS_HEART;
                return r <= COLOSSUS_HEAD_RADIUS ? COLOSSUS_HEAD : 0;
            }

            if (i >= COLOSSUS_CHEST_BASE)
            {
                if (r > COLOSSUS_CHEST)
                    return i == COLOSSUS_SHOULDERS && across <= COLOSSUS_SHOULDERS_HALF && deep <= COLOSSUS_ARM_DEPTH ? COLOSSUS_STONE : 0;

                //The cord through the shoulders and the crystal under it, stone touching both on every side
                if (i == COLOSSUS_SHOULDERS) return square <= COLOSSUS_CORD ? COLOSSUS_HEART : COLOSSUS_STONE;
                return square <= COLOSSUS_CRYSTAL ? COLOSSUS_HEART : COLOSSUS_STONE;
            }

            if (i == COLOSSUS_HIPS_COURSE) return r <= COLOSSUS_HIPS ? COLOSSUS_STONE : 0;

            float step = COLOSSUS_LEG_SPREAD * (COLOSSUS_HIPS_COURSE - 1 - i);
            return across >= COLOSSUS_LEG_IN + step && across <= COLOSSUS_LEG_OUT + step && deep <= COLOSSUS_LEG_DEPTH
                ? COLOSSUS_LEG
                : 0;
        }

        //The limbs are banded by the course pair, so each band is a ring round the limb and the next one down is the other
        //colour; the stone is the block's tile summed over all three axes
        private static BallType ColossusColour(int x, int z, int i) => ColossusPart(x, z, i) switch
        {
            COLOSSUS_HEART => BallType.Type6,   //magenta, the crystal and its cord
            COLOSSUS_ARM or COLOSSUS_LEG => COLOSSUS_LIMBS[i / QUARRY_COURSE % COLOSSUS_LIMBS.Length],
            _ => QuarryBand(x, z, i, COLOSSUS_TILE_FROM, COLOSSUS_TILE_FROM, 1, 1, 1, COLOSSUS_BODY),
        };

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
