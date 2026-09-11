using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>The Meadow</b>, block 1 of the campaign: its designs, and the helpers no other block's designs use, in
    /// the order <c>Program.cs</c> held them — the play order is <see cref="Main"/>'s, and the block's name, music
    /// and ball style are in the tables there. Split out of <c>Program.cs</c> in #386.
    /// </summary>
    internal static partial class Program
    {

        /// <summary>
        /// The campaign's opener: a <b>perfect</b> pyramid — a centred square course against the glass, one
        /// ball narrower a side on every level down to the single apex ball — in three colours, one to a
        /// wall and turned a step a shell, so the player peels it a layer at a time. It replaces the
        /// hand-drawn
        /// <c>One.json</c>, at the author's request and for two reasons they gave — the pyramid had a
        /// <b>tail</b>, and it had no sky of its own.
        /// <para>
        /// The tail was real and is gone. The old layout narrowed from a 100-ball slab down to a single ball
        /// nine levels below it and then <b>widened again</b>, to 4, 8, 12, 25 and 16 — sixty-five balls
        /// hanging under the point, which reads as something stuck to the pyramid rather than as part of it.
        /// The point is now the bottom of the level.
        /// </para>
        /// <para>
        /// <b>The square slab over it went in #234, and with it the white ring that bordered it.</b> The owner
        /// played the campaign and reported the opener too hard, naming the white balls in the top row as the
        /// ones that had to be shot at more than once — and the tool's own report says exactly that: 32 white
        /// balls standing in groups of at most <b>11</b>, because the square ring rounded off into four
        /// disconnected arcs, against red going in one shot of 81 and yellow in one of 112. So the level asked
        /// a first-time player for four separate hits on the one colour hardest to tell from the white gores
        /// every ball is drawn with. Dropping the slab drops the colour with it: the cone never reaches the
        /// third ring the slab's Chebyshev radius of 4.5 did.
        /// </para>
        /// <para>
        /// <b>#234's first pass cut the slab and got the shape wrong, and the owner said so.</b> What was
        /// left was a round cone stepping 0.375 of a cell a level against the 1/√2 a level climbs — under
        /// half a ball a tier, too fine to read as steps at all, so the level tapered into a spike and
        /// still did not look like a pyramid. The owner's word for what it should be was <b>perfect</b>,
        /// and the lattice itself defines what that is: consecutive levels sit offset by half a cell in X
        /// and Z, which is cannonball packing, so a course one ball narrower a side nests into the pockets
        /// of the course above it <b>exactly</b>. The sides now step 1..10 (<see cref="OneCourse"/>), which
        /// is also the rule the game's own fallback map draws — the opener is the shape the game itself
        /// shipped with, on purpose this time. Measured by the tool: 173 balls in 2 groups became
        /// <b>385 in 13</b>, the lateral margin 2 cells became the one every other level keeps, and
        /// nothing needs the repair pass — a shape whose edges sit on the lattice's own half-units has no
        /// slivers to round off.
        /// </para>
        /// <para>
        /// Two things came free with the regeneration. The old field was 10 wide against a slab occupying
        /// x 0…9 — <b>no lateral margin at all</b>, the wall trap that made shots bounce off the flanks of
        /// the pattern pack; twelve gives it the clear column every other level now has. And a plain map file
        /// carries no scene, so One played in whichever backdrop the player last picked; as a level file it
        /// names one.
        /// </para>
        /// <para>
        /// <b>That scene is the meadow now, and not the savanna it opened in until #194.</b> The savanna was
        /// picked when this design was written and the reason given was only that it is the warmest dome of the
        /// set (14) — nothing about a pyramid needs a savanna. Three things pay for the
        /// move. The block is one place at one hour, and this is the meadow block. The savanna is the one
        /// campaign scene that carries <b>point lights of its own</b> — a ring of flickering campfires that
        /// <c>SceneLights</c> puts onto the balls — and the level that teaches colour matching is the last one
        /// whose colours should be tinted by the backdrop. And the savanna misses 75 FPS at High on a 6900XT
        /// (#165) where the meadow's terrain is the cheapest in the game, which is the right trade on the level
        /// a first-time player meets first. Dome 1 is the block's, for <see cref="Bullseye"/>'s own reason.
        /// </para>
        /// <para>
        /// <b>Three colours — red, green, blue, the owner's own words — and they go on the pyramid's WALLS,
        /// a step round the palette every shell in.</b> The draw odds run one in three, between the coin the
        /// two-colour cone flipped and the quarter the four-colour one drew; what three buys over two is a
        /// colour <i>inside</i> a colour. Where they sit is <see cref="OneColour"/>'s subject and it took
        /// three passes to settle, the last one the owner's: bands showed all three colours but a course of
        /// a pyramid is load-bearing, so the ninth course's band took the whole tip with it at <b>74 %</b>
        /// of the cluster. Walls are not — a shell hangs off the plate, not off the shell inside it.
        /// Measured on the same 385 balls in the same shape: <b>6 standing groups, biggest single shot 30 %</b>
        /// (the gate is 90), colours 134/147/104, nothing standing alone, nothing recoloured by the repair
        /// pass, lateral margin 1.
        /// </para>
        /// </summary>
        private static Design One() => new()
        {
            File = "One.json",
            Name = "One",
            Grid = ONE_GRID,
            Depth = ONE_DEPTH,
            Scene = SceneKind.Meadow,
            Sky = 1,
            Music = MUSIC_RINGS,
            Balls = BALLS_MEADOW,
            Shots = 30,
            CeilingStep = 5,
            //The cannonball pyramid, drawn on purpose. Not the polar frame: a course's edge has to sit on
            //the half-unit, which the emitter's own shift is exact at and a radius walked through atan2
            //is not.
            OccupiedBlock = (x, z, i, depth) => OneCourse(x, z, i),
            BlockColour = OneColour,
        };

        //THE OPENER'S OWN FIGURES. Twelve wide so the base keeps its free column of wall (the measured fix
        //this design shipped with), ten deep so the sides step 1..10, and the field the default sixteen —
        //offset 6, even, which Emit refuses the odd alternative to, so the course rule's parity is the
        //field's own by construction.
        private const byte ONE_GRID = 12;
        private const byte ONE_DEPTH = 10;

        //The axis the courses centre on, in the same real units the lattice shifts odd levels by: the
        //centre of the field's TOP level, which is the level BallsMap.Center() puts on the origin.
        private const float ONE_AXIS = (ONE_GRID - 1) * 0.5f + 0.5f;

        /// <summary>
        /// One course of the opener: a centred square, one ball narrower a side than the level above it —
        /// half a unit a side a level, measured against where each cell <b>actually sits</b> (its index
        /// plus the half-unit shift of its level) and not against the raw index, so every course lands on
        /// the axis exactly at either parity. That half-unit rule is the lattice's own close packing: the
        /// narrower course nests into the pockets of the wider one above it, which is what makes this a
        /// <b>perfect</b> pyramid and not a stepped cone — the game's fallback map
        /// (<c>GameplayScreen.BuildFallbackMap</c>) is this same rule, so the opener is now the shape the
        /// game itself shipped with, drawn deliberately instead of by default.
        /// </summary>
        private static bool OneCourse(int x, int z, int i)
        {
            //The emitter's own parity: fieldLevel is i plus an even offset, so odd i is an odd field level
            //and carries the shift. Both this and the course's half-width are whole multiples of a half,
            //hence exact in binary — the test needs no tolerance for the same reason the fallback's does.
            float shift = (i % 2) * 0.5f;
            float half = i * 0.5f;

            return MathF.Abs(x + shift - ONE_AXIS) <= half && MathF.Abs(z + shift - ONE_AXIS) <= half;
        }

        /// <summary>
        /// The pyramid's colouring: <b>one colour a wall, and every shell of walls one step round the
        /// palette</b>. The skin's four faces are red, green, blue and green again; peel a face off and
        /// what it uncovers is a different colour, because the shell beneath is the same rule rotated a
        /// step. The player takes the pyramid apart a layer at a time and the layer he is working on
        /// always carries all three colours at once — the owner's own prescription for #234.
        /// <para>
        /// It is the third colouring this design has worn and each of the two before it failed on one
        /// half of that sentence. <b>Concentric shells</b> — red skin, green under it, blue under that —
        /// peel, but hide two colours of the three: a player holding blue had nothing blue to shoot at
        /// until someone opened the red for him, the owner's own example. <b>One solid band a course</b>
        /// showed all three at once, but a course is not a layer — every course hangs off the wider one
        /// above it, so a band that went took the whole point of the pyramid down with it and the
        /// biggest single shot was 74 % of the cluster. Walls-by-shell is both halves at once: three
        /// colours on the skin from the first shot, and no shell is load-bearing, because every shell is
        /// hung by the plate it reaches and not by the shell inside it.
        /// </para>
        /// <para>
        /// <b>Four walls against three colours</b>, so one colour goes on two of them — on the two that
        /// face each other (<see cref="ONE_WALLS"/>), which is the only arrangement where no two
        /// neighbouring faces are alike. Which colour is doubled rotates with the shell, so the three
        /// come out level over the pyramid as a whole rather than one of them carrying half of it.
        /// </para>
        /// <para>
        /// <b>The innermost pyramid is one colour</b>, the owner's own last sentence, and the lattice
        /// says where it begins: <see cref="ONE_CORE"/> shells in, a course is down to four cells a side
        /// at the plate and one at its own apex, so a wall of it is a ball or two — too thin to read as
        /// a face at all. What is left is a solid pyramid under the middle of the plate, and it is the
        /// last thing standing.
        /// </para>
        /// </summary>
        private static BallType OneColour(int x, int z, int i)
        {
            int shell = OneShell(x, z, i);

            if (shell >= ONE_CORE) return ONE_CORE_COLOUR;

            //The rotation is the whole design: the wall alone would make each face one group from the
            //skin to the core, and the shell alone would hide two colours underneath the third.
            return Band(ONE_WALLS[OneWall(x, z, i)] + shell, ONE_PALETTE);
        }

        /// <summary>
        /// Which of the pyramid's four walls a cell belongs to, 0..3 — the one it sits nearest to, so the
        /// solid is cut into four wedges along its own diagonals and each wedge carries one face of every
        /// shell it passes through.
        /// <para>
        /// The corner columns, where <c>|dx| == |dz|</c> and two walls meet, go to <b>one</b> of the two
        /// all the way round rather than being split down the middle: a corner is a single line of balls
        /// and halving it would leave slivers for the repair pass to recolour. Giving every corner to the
        /// wall on the same side of it keeps the four wedges within a ball of each other.
        /// </para>
        /// </summary>
        private static int OneWall(int x, int z, int i)
        {
            float shift = (i % 2) * 0.5f;
            float dx = x + shift - ONE_AXIS;
            float dz = z + shift - ONE_AXIS;

            if (MathF.Abs(dx) > MathF.Abs(dz)) return dx > 0 ? 1 : 3;
            if (MathF.Abs(dz) > MathF.Abs(dx)) return dz > 0 ? 2 : 0;

            return dx > 0 ? (dz > 0 ? 1 : 0) : (dz > 0 ? 2 : 3);
        }

        /// <summary>
        /// Which concentric shell of its own course a cell sits in, counted from the skin inward: the
        /// course's own half-width less the cell's distance from the axis. Both are multiples of a half
        /// and differ by a whole one, so the arithmetic is exact in binary and needs no tolerance — the
        /// same argument the fallback map's own test makes.
        /// </summary>
        private static int OneShell(int x, int z, int i)
        {
            float shift = (i % 2) * 0.5f;
            float box = MathF.Max(MathF.Abs(x + shift - ONE_AXIS), MathF.Abs(z + shift - ONE_AXIS));

            return (int)(i * 0.5f - box);
        }

        //Red, green, blue - the owner's own words for what the opener is (#234): "a simple three-colour
        //(RGB) pyramid".
        private static readonly BallType[] ONE_PALETTE = { BallType.Type1, BallType.Type2, BallType.Type3 };

        //WHERE THE WALLS STOP AND THE CORE BEGINS, counted in shells from the skin. See OneColour.
        private const int ONE_CORE = 3;

        //The core's own colour. It touches the shell around it on every side, so it joins whichever wall of
        //shell 2 shares its colour, and the one to join is a SINGLE face rather than the facing pair. Shell
        //2 is ONE_WALLS turned two steps - blue, green, blue, red - so green and red are the singles and
        //blue would fuse the core to two opposite wedges at once. Green over red because it is the choice
        //that leaves the three colours 134/147/104 rather than 164/117/104: the magazine draws evenly among
        //the colours still alive, so a colour carrying a sixth more of the cluster than another is a draw
        //the player feels.
        private const BallType ONE_CORE_COLOUR = BallType.Type2;

        private static Design Bullseye() => new()
        {
            File = "Three.json",
            Name = "Bullseye",
            Grid = 15,
            //Ten deep since #234's second pass, where it was four. Four courses is a PLATE: every ball of it
            //is within four levels of the glass, so nothing can hang and nothing can swing, and the level was
            //over in three shots. Ten is the depth One and Toadstool already have, and it is what buys the
            //target a body to peel.
            Depth = 10,
            Scene = SceneKind.Meadow,
            //Dome 1 is the only clear blue one in the set; most of the rest are warm or magenta, and over
            //green hills those read as a clash rather than as weather. A red-and-gold target wants that blue.
            //Since #194 that is the whole block's dome and not just this level's: the block's scene and sky
            //were chosen to be the pair this design already needed, so its pairing anchors the block rather
            //than surviving it.
            Sky = 1,
            Music = MUSIC_RINGS,
            Balls = BALLS_MEADOW,
            Shots = 40,
            CeilingStep = 8,
            //Widest at the top (that layer anchors the whole cluster to the glass) and narrowing downwards,
            //two courses to a terrace so the taper reads as STEPS from the side and as the target's own rings
            //from below - see BULLSEYE_TERRACE.
            Occupied = (r, ang, i, depth) => r <= BullseyeRim(i, depth),
            //Rings on the plain radius, CUT INTO SECTORS - and the sectors are what make this a level rather
            //than three shots. Depth alone does not: a ring coloured on the radius alone is one shell running
            //from the glass to the point, so three rings are three colours touching the glass and three shots
            //take the cluster however deep it is. Every terrace size, taper step, palette size and per-terrace
            //palette roll was measured, and none of them clears four shots, because none of them changes that.
            //Cutting by the angle does, for One's reason: every group then reaches the ceiling on its own, so
            //taking one leaves the rest hanging instead of dropping it. Measured at three sectors: 6 standing
            //groups, 6 shots, best single shot 33 % - One's own profile, two levels earlier.
            Colour = (r, ang, i, depth) => Band((int)MathF.Floor(r / 1.9f) + SectorIndex(ang, 0f, BULLSEYE_SECTORS),
                new[] { BallType.Type1, BallType.Type4, BallType.Type7 }),
        };

        //THE TARGET'S OWN FIGURES (#234). Two courses to a terrace, five terraces over the ten courses, so the
        //rim steps 5.7 -> 4.55 -> 3.4 -> 2.25 -> 1.1. The rim is unchanged from the four-deep version - three
        //rings of Ring's own 1.9 need a radius past 3.8, the same arithmetic Toadstool's cap is cut to - and
        //the step is the one that lands the bottom terrace on about a ball across.
        private const int BULLSEYE_TERRACE = 2;

        //THREE sectors and not four. Four is the natural cut for a target and it measures better on paper (9
        //shots to 6), but Pinwheel two levels later IS four sectors, and a target cut in quarters standing in
        //the same block under the same dome reads as the same idea told twice. Three keeps the block's variety
        //and lands on One's shot count. Three rings by three sectors is also a Latin square in three colours:
        //every ring carries all three and so does every sector, which is what stops the cut reading as a
        //wedge taken out of the target.
        private const int BULLSEYE_SECTORS = 3;
        private const float BULLSEYE_RIM = 5.7f;
        private const float BULLSEYE_STEP = 1.15f;

        /// <summary>
        /// Which terrace a course belongs to, counted from the top: integer division by
        /// <see cref="BULLSEYE_TERRACE"/>, so two courses share one. It drives the RIM only - the colour is
        /// cut by the angle instead, for the reason recorded on Colour above.
        /// </summary>
        private static int BullseyeTerrace(int i, int depth) => (depth - 1 - i) / BULLSEYE_TERRACE;

        /// <summary>
        /// The target's radius at a course: the rim less one step for every terrace below the top. Two courses
        /// share a radius, which is what makes the taper a flight of steps rather than a smooth cone, and what
        /// leaves an annulus of the wider terrace's underside showing at every step. Those undersides are the
        /// rings the player sees looking up at it.
        /// </summary>
        private static float BullseyeRim(int i, int depth) =>
            BULLSEYE_RIM - BULLSEYE_STEP * BullseyeTerrace(i, depth);

        /// <summary>
        /// A toadstool: a cap of three concentric rings with a stalk hanging under it — the red rim, white gills
        /// and gold core of the thing, seen from underneath, which is where the player is standing.
        /// <para>
        /// <b>The stalk is not a second body bolted on; it is the core ring continued.</b> The occupancy is the
        /// cap's dome <i>or</i> <see cref="TOADSTOOL_STALK"/> of the axis at any height, and the colouring is the
        /// same <see cref="Ring"/> over the whole layout, so the stalk comes out in the core's own colour and in
        /// the core's own group by construction. That is what answers the objection <see cref="One"/> records
        /// against its old <b>tail</b> — "something stuck to the pyramid rather than part of it": a tail is a
        /// lobe of its own hanging off a point, where this is 105 balls of one group running from the glass to
        /// the floor, of which the stalk is the visible bottom half.
        /// </para>
        /// <para>
        /// <see cref="TOADSTOOL_SQUASH"/> is what buys the stalk its room. A true hemisphere of radius 5.3 is
        /// 7.5 levels deep and would fill the whole layout; squashed to 0.68 the cap fills five levels and the
        /// stalk gets the other five, in a field that is still the standard sixteen. The cap's radius is 5.3 and
        /// not less because three rings of <see cref="Ring"/>'s own 1.9 thickness need a rim past 3.8 — at 4.5
        /// the outer ring is 0.7 of a cell wide, which is a dotted circle and the lonely-ball trap
        /// <see cref="Gem"/> was rebuilt for. Here it is 1.5 cells wide and five levels tall.
        /// </para>
        /// <para>
        /// <see cref="TOADSTOOL_STALK"/> is 1.7 rather than a round number because of what the two level
        /// parities do with it: it takes 12 cells on an unshifted level and 9 on a shifted one — three to four
        /// cells across, thin enough to read as a stalk and thick enough that it is never a string of balls
        /// touching nothing. At 1.4 it drops to 4 and 5 cells, which is a stem you can see through.
        /// </para>
        /// <para>
        /// Measured: 389 balls, margin 2, nothing alone, nothing in a pair, nothing recoloured, and per level
        /// 12, 9, 12, 9, 12 for the stalk then 37, 52, 69, 88, 89 for the cap. The three colours come out
        /// 164/120/105 — best single shots 42 %, 30 % and 26 %, so the widest is under <see cref="Bullseye"/>'s
        /// own 45 % two levels earlier. All three hang off the 89-cell anchor layer alone, with nothing falling
        /// when the other two are taken away, which is the check that says the stalk is the core continued.
        /// </para>
        /// </summary>
        private static Design Toadstool() => new()
        {
            File = "Toadstool.json",
            Name = "Toadstool",
            //Fifteen for a cap reaching 5.3, which is Bullseye's and Pinwheel's field: the block frames the same
            Grid = 15,
            //Ten deep, of which the cap is the top five and the stalk the bottom five. Even by necessity — the
            //field is 16 and Emit refuses an odd offset — and it leaves the six empty levels of growth room.
            Depth = 10,
            Scene = SceneKind.Meadow,
            Sky = 1,
            Music = MUSIC_RINGS,
            Balls = BALLS_MEADOW,
            Shots = 44,
            CeilingStep = 9,
            Occupied = (r, ang, i, depth) =>
                DomeDistance(r, i, depth, TOADSTOOL_SQUASH) <= TOADSTOOL_CAP || r <= TOADSTOOL_STALK,
            //Gold core and stalk, white gills, red rim: a fly agaric from underneath. Rings on the plain round
            //radius and NOT on the dome distance the cap is cut from - shells parallel to a curved surface hide
            //two of the three colours behind the third, which is the whole reason Bullseye's rings read.
            //
            //CUT INTO SECTORS since #234, and on this design of all of them the cut is what the thing already
            //is: a gilled mushroom seen from below is radial. It is also the same repair Bullseye needed and
            //for the same measured reason - three rings running the full height of the cap are three groups
            //and three shots, however deep the body hangs. Measured at four: 9 standing groups, 9 shots, best
            //single shot 21 %, and the remains hang nine levels under the glass for eight of them.
            Colour = (r, ang, i, depth) => Band((int)MathF.Floor(r / 1.9f) + SectorIndex(ang, 0f, TOADSTOOL_GILLS),
                new[] { BallType.Type7, BallType.Type4, BallType.Type1 }),
        };

        /// <summary>
        /// A disc cut into four spiral sectors — a pinwheel from below, four vertical wedges from the side.
        /// The twist term is what bends the sector boundaries into a spiral instead of a cross.
        /// <para>
        /// Moved out of the desert into the meadow block by #194, which nothing recorded here argued against —
        /// dome 7 was the blazing red one and this design never gave a reason for it. What to watch is the one
        /// thing the move can cost: a red sector is a quarter of the disc, and dome 1's blue is behind the upper
        /// half of it. If the red loses its edge there the fix is the palette, not the dome, which is the whole
        /// block's; a screenshot decides it, as it did for <see cref="Gem"/>'s ring.
        /// </para>
        /// </summary>
        private static Design Pinwheel() => new()
        {
            File = "Five.json",
            Name = "Pinwheel",
            Grid = 15,
            //Ten deep since #234, where it was four - and those four were a flat DISC of constant radius, the
            //one shape in the block that could not move at all. See PINWHEEL_TWIST for what the depth turns
            //the spiral into.
            Depth = 10,
            Scene = SceneKind.Meadow,
            Sky = 1,
            Music = MUSIC_RINGS,
            Balls = BALLS_MEADOW,
            Shots = 44,
            CeilingStep = 9,
            //A cone now, not a disc: widest against the glass and drawn to a point, so the sectors are
            //tapering vanes rather than slices of a plate.
            Occupied = (r, ang, i, depth) => r <= PINWHEEL_RIM - (depth - 1 - i) * PINWHEEL_TAPER,
            //The twist takes the COURSE as well as the radius, so a vane winds as it descends: the disc's
            //spiral, extruded into a helix. Read from below it is still the pinwheel it is named for.
            Colour = (r, ang, i, depth) => Sector(ang, r * 0.16f + i * PINWHEEL_TWIST, 4,
                new[] { BallType.Type1, BallType.Type7, BallType.Type2, BallType.Type3 }),
        };

        //THE PINWHEEL'S OWN FIGURES (#234). The rim and taper draw the cone to a point over ten courses; the
        //twist is per COURSE and in turns, so 0.05 is a fifth of a sector a level and a vane makes just under
        //half a sector over the whole drop - enough to read as a wind, and not so much that a vane spirals
        //past its neighbour and stops being one face.
        private const float PINWHEEL_RIM = 5.5f;
        private const float PINWHEEL_TAPER = 0.5f;
        private const float PINWHEEL_TWIST = 0.05f;

        /// <summary>
        /// An octahedron hanging point-down, cut into concentric diamond rings — the angular answer to
        /// <see cref="Bullseye"/>'s round ones, and the one design whose silhouette reads from any angle.
        /// Banding it by height was the first try and it lost the level in one shot for the reason given
        /// on <see cref="Mosaic"/>; rings put three colours on the anchor layer.
        /// </summary>
        private static Design Gem() => new()
        {
            File = "Seven.json",
            Name = "Gem",
            //Seventeen where the round designs need fifteen, and the diamond is why: a taxicab rim of m = 7
            //reaches seven whole cells along each axis, where a round radius of 5.5 reaches five. Fifteen
            //put the four points of the diamond exactly ON the field wall — no lateral margin at all, the
            //trap LateralMargin now refuses. Widening the field keeps the shape, which is the thing worth
            //keeping here; capping the rim at m = 5 would have cost the gem two rings of its widest face.
            Grid = 17,
            //Ten deep since #234, where it was six. The four facet steps are unchanged in WIDTH - the widest
            //is still m = 7, which is what the field was widened to 17 for - so the whole of the added depth
            //goes into the bottom step: four courses of m = 1, a five-ball column that is the longest pendulum
            //in the block. See GemStep.
            Depth = 10,
            //The meadow block's, since #194. The recorded decision here was about the ring's COLOUR — magenta
            //sank into the dream's violet soup, which a screenshot said and a palette on paper would not have —
            //and not about the dream itself; yellow reads against everything, so the reason for it survives the
            //move untouched. Under a clear blue dome the octahedron reads as cut glass in daylight, which plays
            //to the one thing this shape is documented for: a silhouette that reads from any angle wants a plain
            //sky behind it rather than a violet fog.
            Scene = SceneKind.Meadow,
            Sky = 1,
            Music = MUSIC_RINGS,
            Balls = BALLS_MEADOW,
            Shots = 44,
            CeilingStep = 9,
            //Diamond cross-section (|dx| + |dz|), widening towards the top, and rings measured on that same
            //taxicab radius so the rings follow the facets instead of cutting across them.
            //
            //Both numbers below are forced by the lattice rather than chosen for looks. The taxicab radius
            //only ever lands on whole numbers, and cells on one level touch only ORTHOGONALLY — so a ring one
            //unit wide is a diagonal staircase of balls that do not touch each other at all. Rings are
            //therefore two units wide (floor(m/2), not the m/2.2 this started with), and every layer's rim
            //stops on an ODD m so its outermost ring is a complete two, never a bare diagonal. The first Gem
            //broke both: its top layer's rim was the single shell m=7, twenty-four balls standing alone
            //against the glass with no level above to connect through, each needing two landed balls of its
            //own colour before anything could fall. It is the hardest defect here to see and the easiest to
            //author by accident, which is why FindLonelyBalls now refuses it.
            OccupiedManhattan = (m, i, depth) => m <= GemRim(i),
            //Yellow rather than the magenta this started with: the dream scene is a violet soup and the
            //magenta ring sank into it, which a screenshot showed and a palette on paper would not have
            //Rolled a step a FACET STEP since #234, and given a FOURTH colour to roll through. Bullseye and
            //Toadstool answer the same three-shot fault by cutting their rings into sectors; this design
            //deliberately does not, because a cut facet is unbroken in life and a radial seam across it is the
            //one thing that would stop the shape reading as a crystal. A fourth colour buys the same groups
            //without touching the geometry: measured at 10 standing groups, 7 shots, best single shot 25 %.
            //
            //Magenta is the colour this design STARTED with, dropped because the dream scene it then played in
            //is a violet soup and the ring sank into it. #194 moved the block to the meadow under dome 1, so
            //that objection has lapsed - and a screenshot is what says so, as it did when the colour was
            //dropped. Rolling by two rather than one for the reason GEM_ROLL records.
            ColourManhattan = (m, i, depth) => Band((int)MathF.Floor(m * HALF) + GEM_ROLL * GemStep(i),
                new[] { BallType.Type7, BallType.Type3, BallType.Type5, BallType.Type6 }),
        };

        //THE STONE'S OWN FIGURES (#234). Four facet steps over ten courses: m = 1 for the bottom FOUR, then
        //two courses each at m = 3, 5 and 7. The widest is unchanged, because 7 is what the field's width was
        //chosen for; the bottom step is four courses on purpose, since m = 1 is five cells on an unshifted
        //level and four on a shifted one - the thinnest column this lattice holds together, and the one that
        //swings furthest once the stone above it opens.
        private const int GEM_COLUMN_COURSES = 4;

        //The palette's turn per facet step, and TWO rather than one because of what one does on a stepped
        //solid: a step widens the rim by one whole ring, so turning the palette by one carries a ring's own
        //colour straight out onto the step below it and welds the two into a single diagonal group running
        //down the stone. Measured at a turn of one: the best single shot took 57 % of the cluster.
        private const int GEM_ROLL = 2;

        /// <summary>
        /// Which facet step a course is on, counted from the bottom: 0 for the column, then one step every
        /// two courses. It is the rim's step AND the colour's roll, the same double duty
        /// <see cref="BullseyeTerrace"/> does - a facet is one width and one palette turn.
        /// </summary>
        private static int GemStep(int i) =>
            i < GEM_COLUMN_COURSES ? 0 : (i - GEM_COLUMN_COURSES) / 2 + 1;

        /// <summary>
        /// The stone's taxicab rim at a course. Every step is an ODD m so the outermost ring of each is a
        /// complete two units wide and never a bare diagonal - the defect <see cref="FindLonelyBalls"/> was
        /// written for, and the reason this steps rather than divides.
        /// </summary>
        private static int GemRim(int i) => 1 + 2 * GemStep(i);

        #region The lathe levels (#255)

        //THE MEADOW'S SECOND FIVE (#255): the chapter that taught what a colour group IS goes back to the
        //lathe. Every body here is a solid of revolution and every colouring is the block's own big sector
        //plate - the grammar One, Bullseye and Toadstool established - and what the five add, in play order,
        //is SUSPENSION: a waist the lower cone pendulums through (Diabolo), feathers whose loss tips the
        //cork they carry (Shuttle), handles that are a second load path the player can see working
        //(Amphora), a ~130-ball hoop hung on four snipeable spokes (Saturn), and three basins on two open
        //stems that bob out of phase from the first launch (Fountain). Same place, same hour, same piece as
        //the first five - the meadow under dome 1, MUSIC_RINGS, BALLS_MEADOW's glass bubbles - a deliberate
        //return the way the Spectrum returns to the Arcade's city: the lessons here stand on the ones the
        //meadow already taught, so they are taught where the player learned them.
        //
        //THE ONE RULE ALL FIVE OBEY is the block's own, restated on curved bodies: a colour is never a
        //horizontal shell alone. Every sector runs the full height of whatever it is painted on, so every
        //plate reaches the glass on its own and taking one leaves the rest hanging - the trap Validate's
        //drop test was written for, and the reason Bullseye's rings are cut by the angle. The two deliberate
        //exceptions (Shuttle's cork, Saturn's ring and spokes) are low-only colours that are the MOST
        //exposed thing on their level, the Toadstool-stalk precedent, and each design's doc says so.
        //
        //NUMBERS BELOW ARE THE DRAWINGS', NOT MEASUREMENTS. Each design's doc names the checks its judged
        //spec flagged - unshot death-line sag, per-sector counts after lattice rounding, the drop test read
        //off the measured contact graph - and the fallback constants pre-authorised for each; the
        //generator's own report is what settles them, and no figure here goes into a doc as measured until
        //it has been.

        /// <summary>
        /// Two cones balanced tip to tip on a waist a fraction of their width - the block's circular answer
        /// to <see cref="One"/>'s pyramid, and the first level whose silhouette is symmetric about a point
        /// in mid-air: the top half is holding an upside-down copy of itself by a two-course neck. Each cone
        /// course is half a cell narrower than the one before it (<see cref="DIABOLO_SLOPE"/> against the
        /// lattice's own half-cell stagger), so the rims nest into the shifted pockets of the adjacent
        /// course in BOTH directions from the waist - the opener's cannonball packing read on a lathe body.
        /// <para>
        /// The profile is <see cref="DiaboloRadius"/>: 4.05 at both ends, 1.55 at the waist (i = 5, 6). The
        /// wide ends are hollow cups <see cref="DIABOLO_WALL"/> thick each way; wherever the profile is at
        /// or under <see cref="DIABOLO_SOLID"/> the course fills solid, which makes i = 3..8 the plug - a
        /// disc still 2.55 in radius at its narrowest - that the bottom cup hangs from, and the torsion
        /// spring every hit winds once the sectors start coming down. Max radius 5.05 keeps indices within
        /// 1..11 of the 13-wide field: the one-column margin, at both parities.
        /// </para>
        /// <para>
        /// Six 60-degree sectors over a FOUR-entry palette, with the lower cone's palette turned one sector
        /// at the waist. Boundaries sit at 30 degrees plus multiples of 60 (<see cref="DIABOLO_TWIST"/>), off
        /// the lattice's own axes.
        /// </para>
        /// <para>
        /// <b>⚠ IT WAS THREE COLOURS AND THAT MADE IT THE EASIEST LEVEL OF THE OPENING SEVEN (#361).</b> With
        /// three, <see cref="Band"/> put sector k and k + 3 on the same entry, so each colour was two
        /// full-height plates running glass to tip and fusing through the solid plug: <b>five standing groups
        /// for 455 balls</b> — 91 balls a shot at par and a budget of 6.8 shots per group, against 4.9 and 5.5
        /// for the two gentler levels before it and 2.4 for the one after. A playtest called it "surprisingly
        /// easy" and it was, arithmetically. Four inks stop opposite sectors sharing, and the turn at the
        /// waist stops a sector's two halves fusing through the plug: <b>7 groups, 65 balls at par, budget
        /// 4.86, and the biggest single shot 33 % → 25 %</b>. The silhouette did not move a cell, which is
        /// what the same playtest liked about it — the turn is visible as a twist across the waist and is
        /// arguably the better drawing.
        /// </para>
        /// <para>
        /// The judged spec's checks, in its order: unshot death-line sag of the cantilevered bottom cup
        /// first (the i = 0..2 annulus is the only mass below the plug; the pre-authorised fixes are
        /// DIABOLO_SOLID to 3.0 or DIABOLO_SLOPE to 0.45), then at least 3 connected balls per sector at
        /// the waist courses (widen DIABOLO_WALL to 1.2 at i = 4..7 only, if one starves), and the ~33 %
        /// drop test read off the report rather than the sector arithmetic - the six sectors may fuse
        /// through the solid waist near the axis, which is harmless because fused opposites are the same
        /// colour by construction.
        /// </para>
        /// </summary>
        private static Design Diabolo() => new()
        {
            File = "Diabolo.json",
            Name = "Diabolo",
            Grid = 13,
            //Twelve deep in the standard sixteen-level field: offset 4, even, and four empty levels of
            //growth room under the lower tip.
            Depth = 12,
            Scene = SceneKind.Meadow,
            Sky = 1,
            Music = MUSIC_RINGS,
            Balls = BALLS_MEADOW,
            Shots = 34,
            CeilingStep = 7,
            //Hollow cups at the wide ends, a solid plug where the profile pinches under DIABOLO_SOLID: the
            //second condition is what turns the middle six courses into a neck instead of a hollow tube.
            Occupied = (r, ang, i, depth) => DiaboloOccupied(r, i),
            //Six sectors onto three colours, and THE LOWER CONE'S PALETTE IS TURNED ONE SECTOR (#361).
            //Band folds sector k and k + 3 onto one entry, so opposite sectors share a colour; the turn at
            //the waist means a sector's upper plate and its lower one are different colours, which is what
            //stops each colour being one glass-to-tip plate through the solid neck.
            Colour = (r, ang, i, depth) => Band(
                SectorIndex(ang, DIABOLO_TWIST, DIABOLO_SECTORS) + (i < DIABOLO_MIDDLE ? 1 : 0),
                new[] { BallType.Type1, BallType.Type7, BallType.Type3, BallType.Type6 }), //red, yellow, blue, magenta
        };

        //THE DIABOLO'S OWN FIGURES (#255). The profile is a V in the course index: WAIST at the fold, SLOPE
        //half a cell a course so consecutive rims nest into each other's shifted pockets, and the fold at
        //MIDDLE - between i = 5 and 6, so the two cones are true mirrors and the waist is two courses, not
        //one. WALL is the cups' thickness each way; SOLID is where hollow gives way to plug (a profile of
        //2.6 or under fills whole, which is i = 3..8 - the spec's own fallback raises it to 3.0 if the
        //bottom cup sags). TWIST is a twelfth of a turn, 30 degrees: the six boundaries land at 30 + 60k,
        //off the lattice's own axes.
        private const float DIABOLO_WAIST = 1.3f;
        private const float DIABOLO_SLOPE = 0.5f;
        private const float DIABOLO_MIDDLE = 5.5f;
        private const float DIABOLO_WALL = 1.0f;
        private const float DIABOLO_SOLID = 2.6f;
        private const int DIABOLO_SECTORS = 6;
        private const float DIABOLO_TWIST = 1f / 12f;

        /// <summary>
        /// The diabolo's centreline radius at a course: <see cref="DIABOLO_WAIST"/> plus
        /// <see cref="DIABOLO_SLOPE"/> for every course away from the fold at <see cref="DIABOLO_MIDDLE"/> -
        /// 4.05 at both ends, 1.55 at i = 5 and 6. One figure drives the cups' walls and the hollow/solid
        /// cut both, so the whole body is a single profile read two ways.
        /// </summary>
        private static float DiaboloRadius(int i) =>
            DIABOLO_WAIST + DIABOLO_SLOPE * MathF.Abs(i - DIABOLO_MIDDLE);

        /// <summary>
        /// Whether a cell is on the diabolo: inside the profile's outer wall always, and past its inner
        /// wall only where the course is wide enough to be hollow - at or under <see cref="DIABOLO_SOLID"/>
        /// the course fills solid, which is the plug the whole lower cone hangs from.
        /// </summary>
        private static bool DiaboloOccupied(float r, int i)
        {
            float rim = DiaboloRadius(i);
            return r <= rim + DIABOLO_WALL && (r >= rim - DIABOLO_WALL || rim <= DIABOLO_SOLID);
        }

        /// <summary>
        /// A giant shuttlecock hanging cork-down, the way a birdie flies: a solid cork sphere at the tip, a
        /// solid collar disc tying it to a two-cell conical skirt that flares to the glass in six feathers.
        /// The block's first ASYMMETRIC suspension lesson - each feather released removes one sixth of the
        /// cork's hanging asymmetrically, so the birdie tips toward the gap and swings back, and with two
        /// feathers left the cork pendulums off a slender C-arc on every hit. The alternative route is the
        /// cork itself: three red shots drop the whole bottom mass at once.
        /// <para>
        /// The skirt's courses widen <see cref="SHUTTLE_FLARE"/> a level, so each ring sits half a cell
        /// offset in the pockets of the ring above - the flare reads as stacked nesting hoops - and the
        /// closed shell means every feather is braced by both neighbours all the way down, while the collar
        /// disc at i = <see cref="SHUTTLE_COLLAR"/> ties all six into the cork so no single feather ever
        /// solely carries it. The widest course (centreline 5.54, outer 6.54) is the glass course i = 11,
        /// which lands on a SHIFTED field level - where the wall column sits at |dx| = 7.0, past reach - and
        /// the unshifted courses stop at 6.02 against the wall's 6.5, so the margin holds at one column by
        /// the parity of the layout itself.
        /// </para>
        /// <para>
        /// The cork and the collar's core are red, one ~45-ball group standing only at the bottom - the
        /// top-level rule broken knowingly, the Toadstool-stalk precedent, because the cork is the most
        /// exposed thing on the level and shootable from any side. The judged spec's checks: gate 3 at the
        /// skirt's narrowest course first, i = 5 (annulus 1.42..3.42) - each feather needs at least 3
        /// CONNECTED balls after rounding, and the pre-authorised fix is SHUTTLE_SKIRT_BASE 1.9 to 2.2
        /// rather than a wider wall; then that the endgame stop can never leave red as the last colour
        /// under the descended ceiling (if it can, tie one red cell into the collar rim at i = 4); and that
        /// the collar - the level's real keystone - stays solid to r = 2.7 after rounding.
        /// </para>
        /// </summary>
        private static Design Shuttle() => new()
        {
            File = "Shuttle.json",
            Name = "Shuttle",
            Grid = 15,
            Depth = 12,
            Scene = SceneKind.Meadow,
            Sky = 1,
            //⚠ 34 AND NOT 38 (#361). The feathers were always six separate groups - opposite ones share an
            //ink but never touch - so the fourth ink changes what the magazine draws and not what the level
            //is made of, and the slack was the rest of the complaint: 38 shots over 7 groups is 5.4 a group
            //where the two gentler levels before this one get 4.9 and 5.5 and the level after it gets 2.4.
            //At 34 it is Diabolo's 4.9, which is the ramp this level sits in the middle of.
            Shots = 34,
            CeilingStep = 7,
            Occupied = (r, ang, i, depth) => ShuttleOccupied(r, i),
            Colour = ShuttleColour,
        };

        //THE SHUTTLECOCK'S OWN FIGURES (#255). The cork is a solid sphere of CORK_RADIUS centred between
        //i = 1 and 2 (CORK_CENTRE), so it spans i = 0..4 and hangs nose-down. The collar is one solid disc
        //of COLLAR_RIM at COLLAR, overlapping both the cork's top and the skirt's bottom in plan, which is
        //what makes it the keystone; CORE is where its red centre gives way to the feathers' rim. The skirt
        //runs from SKIRT_BASE at the collar out FLARE a course to 5.54 at the glass, WALL thick each way -
        //two cells, the Ziggurat rule's floor.
        private const float SHUTTLE_CORK_RADIUS = 2.3f;
        private const float SHUTTLE_CORK_CENTRE = 1.5f;
        private const int SHUTTLE_COLLAR = 4;
        private const float SHUTTLE_COLLAR_RIM = 2.7f;
        private const float SHUTTLE_CORE = 1.6f;
        private const float SHUTTLE_SKIRT_BASE = 1.9f;
        private const float SHUTTLE_FLARE = 0.52f;
        private const float SHUTTLE_WALL = 1.0f;

        //Six feathers, boundaries on the axes (0, 60 .. 300 degrees), over FOUR colours since #361 - six big
        //plates, each glass-to-collar, each ~13 % of the cluster. Opposite feathers used to share an ink; they
        //never touch either way, so the group COUNT was the same at three - what the fourth ink changes is
        //the magazine, which now draws from five colours instead of four. The rest of that issue's tightening
        //here is the shot budget: see Shuttle's own Shots.
        private const int SHUTTLE_FEATHERS = 6;

        /// <summary>
        /// Whether a cell is on the birdie: inside the cork's sphere (whose own test bounds it to i = 0..4,
        /// no course check needed), on the solid collar disc, or within the skirt's two-cell wall above it.
        /// </summary>
        private static bool ShuttleOccupied(float r, int i)
        {
            if (ShuttleCork(r, i)) return true;
            if (i == SHUTTLE_COLLAR) return r <= SHUTTLE_COLLAR_RIM;

            return i > SHUTTLE_COLLAR && MathF.Abs(r - ShuttleSkirt(i)) <= SHUTTLE_WALL;
        }

        /// <summary>
        /// The birdie's colour: red for the cork and the collar's core - one group, the bottom of
        /// everything, so releasing it orphans nothing - and six feathers over the collar's rim and the
        /// whole skirt, each braced glass-to-collar by its neighbours in the closed shell.
        /// </summary>
        private static BallType ShuttleColour(float r, float ang, int i, int depth)
        {
            if (i < SHUTTLE_COLLAR || (i == SHUTTLE_COLLAR && r <= SHUTTLE_CORE))
                return BallType.Type1; //red - the cork, the one low-only colour, maximally exposed

            //⚠ FOUR INKS OVER SIX FEATHERS AND NOT THREE (#361). Folded onto three, sector k and k + 3 shared
            //an ink, so every colour was two whole opposite feathers glass-to-collar and the level's budget
            //came out looser than the two gentler levels before it. Four does not divide six, so no two
            //feathers of a colour are opposite and the wrap puts a fourth ink where the sixth feather closes.
            return Band(SectorIndex(ang, 0f, SHUTTLE_FEATHERS),
                new[] { BallType.Type4, BallType.Type2, BallType.Type3, BallType.Type7 }); //white, green, blue, yellow
        }

        /// <summary>Whether a cell is inside the cork - a true sphere, the vertical in level units scaled
        /// by <see cref="INV_SQRT_TWO"/> the way every round body here measures it.</summary>
        private static bool ShuttleCork(float r, int i)
        {
            float dy = (i - SHUTTLE_CORK_CENTRE) * INV_SQRT_TWO;
            return MathF.Sqrt(r * r + dy * dy) <= SHUTTLE_CORK_RADIUS;
        }

        /// <summary>The skirt's centreline radius at a course: <see cref="SHUTTLE_SKIRT_BASE"/> at the
        /// collar, flaring <see cref="SHUTTLE_FLARE"/> a course to 5.54 at the glass.</summary>
        private static float ShuttleSkirt(int i) =>
            SHUTTLE_SKIRT_BASE + SHUTTLE_FLARE * (i - SHUTTLE_COLLAR);

        /// <summary>
        /// A Greek vase in orange and black that really can be carried by its handles: a body of revolution
        /// with a solid foot, a two-cell wall swelling to a 4.8 belly, and a solid neck to the glass - plus
        /// two mirrored handle tubes on the +/-X flanks, the block's only appendages off a lathe body, and
        /// they are structure rather than decoration. Each handle is a closed loop from the shoulder wall to
        /// the neck at the glass, so a belly that loses a gore still hangs shoulder-to-glass through the
        /// loops - the second-load-path rule made visible as a picture of carrying a vase - and the body
        /// wall is hoop-continuous at every course besides.
        /// <para>
        /// The handles' figures deviate from the judged spec ON its own gate note's authority, and the
        /// reason is deterministic rather than judged: the spec drew the apex centreline at 5.3 with a 1.3
        /// tube, and that pair puts the field-wall column - (|dx|, dz) = (6.5, 0.5) on the UNSHIFTED apex
        /// course i = 10 - exactly ON the tube's boundary: (6.5 - 5.3)^2 + 0.5^2 = 1.69 = 1.3^2, so the
        /// lateral-margin gate would be decided by float noise in r * cos(ang), the trap
        /// <see cref="PolarBlock"/>'s bias comment records. The note's own fallback pair
        /// (<see cref="AMPHORA_TUBE"/> 1.4, apex 5.0) is taken from the start: it clears the wall column by
        /// 0.54 in the squared test and fattens the tube against the Rope-style mush the note names first.
        /// </para>
        /// <para>
        /// Attic pottery in four 90-degree gores, orange and black alternating - opposite same-colour gores
        /// may fuse through the solid neck and foot, accepted because the drop test is run on the measured
        /// contact graph: fused orange at ~45 % must orphan nothing, the black gores still running
        /// glass-to-foot and the white loops still tying shoulder to neck, and symmetrically for black. The
        /// spec's remaining checks: each handle must come out a recognisable connected loop of at least 3
        /// white balls with no orphan crumbs, and its landing (centreline 2.6 at the glass, inner edge 1.2)
        /// must genuinely overlap the neck's solid r = 2.0 after rounding - a handle that misses the neck
        /// is a gate-1 float. One drafting fact to expect in the report: at the glass the handles land
        /// exactly where the orange gores face (+/-X), so orange may stand on the glass only through the
        /// four axis cells whose 45-degree angles sit right on the gore boundaries - the top-level rule is
        /// a should (<see cref="Shuttle"/>'s cork precedent), and the drop test is the arbiter.
        /// </para>
        /// </summary>
        private static Design Amphora() => new()
        {
            File = "Amphora.json",
            Name = "Amphora",
            Grid = 15,
            //Thirteen deep in a seventeen-level field: the offset stays 4, even, the same air under the
            //foot every sixteen-over-twelve design in the block gets.
            Depth = 13,
            FieldLevels = 17,
            Scene = SceneKind.Meadow,
            Sky = 1,
            Music = MUSIC_RINGS,
            Balls = BALLS_MEADOW,
            Shots = 34,
            CeilingStep = 7,
            Occupied = AmphoraOccupied,
            Colour = AmphoraColour,
        };

        //THE VASE'S OWN FIGURES (#255). PROFILE is the radius table by course: solid through the foot
        //(i <= FOOT_TOP) and the neck (i >= NECK_BASE), a WALL-thick shell each way between, the belly
        //widest at i = 6. Stated as a table rather than a formula because a vessel profile is drawn, not
        //solved - the same reason the pictures are bitmaps.
        private static readonly float[] AMPHORA_PROFILE =
        {
            2.1f, 2.1f,                                      //the foot, solid
            2.8f, 3.4f, 4.0f, 4.5f, 4.8f, 4.75f, 4.6f, 3.4f, //the hollow wall, belly max at i = 6
            2.0f, 2.0f, 2.0f,                                //the neck, solid to the glass
        };
        private const int AMPHORA_FOOT_TOP = 1;
        private const int AMPHORA_NECK_BASE = 10;
        private const float AMPHORA_WALL = 1.0f;

        //Where the vase is narrow enough for every gore to meet every other across the axis - the foot
        //and the neck, both of which are solid discs. AmphoraColour paints those out of a palette of
        //their own; see it for the measurement. 2.9 sits between the neck's 2.0 and the first hollow
        //course at 3.4, so the ends are caught exactly and nothing of the belly is.
        private const float AMPHORA_PINCH = 2.9f;

        //The handles' centreline distance off the axis at i = HANDLE_BASE..12: springing from the shoulder
        //wall, arcing over the apex, landing on the neck at the glass. The apex is 5.0 and the tube 1.4 -
        //the gate note's own fallback pair, taken from the start for the margin reason the design doc
        //carries - where the spec drew 5.3 and 1.3.
        private const int AMPHORA_HANDLE_BASE = 8;
        private static readonly float[] AMPHORA_HANDLE_ARC = { 4.8f, 5.2f, 5.0f, 4.6f, 2.6f };
        private const float AMPHORA_TUBE = 1.4f;

        //Four gores with boundaries at 45 degrees plus 90s (an eighth of a turn), so orange faces the +/-X
        //flanks under the handles and black faces the gun; Band over two entries alternates them.
        private const int AMPHORA_GORES = 6;
        private const float AMPHORA_GORE_TWIST = 1f / 12f;

        /// <summary>
        /// The vase's occupancy: the body of revolution - solid where <see cref="AMPHORA_PROFILE"/> is the
        /// foot or the neck, a <see cref="AMPHORA_WALL"/>-thick shell each way between - plus the two
        /// handle tubes on the flanks.
        /// </summary>
        private static bool AmphoraOccupied(float r, float ang, int i, int depth)
        {
            bool body = i <= AMPHORA_FOOT_TOP || i >= AMPHORA_NECK_BASE
                ? r <= AMPHORA_PROFILE[i]
                : MathF.Abs(r - AMPHORA_PROFILE[i]) <= AMPHORA_WALL;

            return body || AmphoraHandle(r, ang, i);
        }

        /// <summary>
        /// The vase's colour: white wherever the handle tubes run - the tube wins over the body where they
        /// overlap at the shoulder and the neck, which is exactly what makes each loop one connected white
        /// group from springing to landing - and the alternating orange/black gores everywhere else.
        /// </summary>
        private static BallType AmphoraColour(float r, float ang, int i, int depth)
        {
            if (AmphoraHandle(r, ang, i)) return BallType.Type4; //white - the two handle loops

            //SIX GORES AND NOT FOUR, and the count is the whole of what keeps this level playable. A vase
            //draws in at its neck and its foot, and where it does the gores meet ACROSS the axis: at four
            //gores the two orange ones were opposite each other, fused through both solid ends into a
            //single 200-ball group, and one ball took 411 of the level's 502 (81 %) against a pack whose
            //worst was 52. Six gores on two inks is the arrangement where neither neighbour agrees: gore k
            //and k + 1 differ because the palette alternates, and gore k and its OPPOSITE k + 3 differ
            //because three is odd. Rolling the palette a step at the pinches was tried first and made it
            //worse in a way worth recording - a roll of one puts the NEIGHBOURING gore's colour across the
            //level boundary, which is a diagonal bridge rather than a cut, and is exactly what Rope's own
            //doc records against rolling by one.
            //
            //AND THE SOLID ENDS ARE PAINTED OUT OF A PALETTE OF THEIR OWN, which is the other half of the
            //same finding. Six gores stop the body fusing across its own hollow, but the foot and the neck
            //are solid discs barely two cells across, and down there every gore is within a cell of every
            //other: whatever colour they carry, all three sheets of it meet. Handing the ends two colours
            //nothing else uses cuts the body's orange and black apart at both ends for good, and it costs
            //nothing to read - unglazed clay is exactly what the foot and the neck of a black-figure vase
            //ARE. The ends stay TWO colours rather than one because the neck is what the whole vase hangs
            //from: one colour on it and a single lucky ball takes the level off the glass.
            int sector = SectorIndex(ang, AMPHORA_GORE_TWIST, AMPHORA_GORES);

            return AMPHORA_PROFILE[i] <= AMPHORA_PINCH
                ? Band(sector, new[] { BallType.Type10, BallType.Type1 })   //brown, red - the clay ends
                : Band(sector, new[] { BallType.Type9, BallType.Type8 });   //orange, black - the gores
        }

        /// <summary>
        /// Whether a cell is inside a handle tube. Both handles in one test: the tube's cross-section is a
        /// disc of <see cref="AMPHORA_TUBE"/> about the centreline <see cref="AMPHORA_HANDLE_ARC"/> holds,
        /// measured in the (|dx|, dz) plane - folding X's sign draws the mirrored pair at ang 0 and 180 at
        /// once.
        /// </summary>
        private static bool AmphoraHandle(float r, float ang, int i)
        {
            if (i < AMPHORA_HANDLE_BASE) return false;

            float away = MathF.Abs(r * MathF.Cos(ang)) - AMPHORA_HANDLE_ARC[i - AMPHORA_HANDLE_BASE];
            float dz = r * MathF.Sin(ang);

            return away * away + dz * dz <= AMPHORA_TUBE * AMPHORA_TUBE;
        }

        /// <summary>
        /// A planet wearing a floating ring on four hidden spokes - the block's mission statement built as
        /// a level: the entire ring is ONE connected yellow group, ~130 balls, so three casual yellow shots
        /// pay off the biggest single group in the block, or the player snipes the four red spokes and
        /// learns cut-the-support-and-the-unmatched-mass-falls, the taught orphan drop
        /// <see cref="Fountain"/> then exploits. With one spoke left the whole hoop swings on a handful of
        /// balls - the most lopsided earned swing in the block - and the closed hoop is its own second load
        /// path: any one spoke can carry it because the hoop distributes the load around itself.
        /// <para>
        /// Both of the judged spec's amendments are taken. It is <b>Saturn</b>, not the working name Halo
        /// (the campaign's nebula block already owns that word); and the planet is TWO 180-degree meridian
        /// halves, blue and green, rather than the drawn three - the five-colour palette trimmed to the
        /// block's four, dropping white, which also removes the white/yellow confusable pair the spec
        /// flagged. The seam (<see cref="SATURN_SEAM"/>, a quarter turn) runs down the +/-Z meridians so
        /// both halves face the gun, each ~29 % and orphaning nothing.
        /// </para>
        /// <para>
        /// The geometry: a sphere of <see cref="SATURN_GLOBE"/> about the equator between the ring's two
        /// courses, flattened into a <see cref="SATURN_CAP"/> anchor cap where it meets the glass (the bare
        /// sphere would touch it in one cell, and one glass bond is a hinge); the annulus
        /// <see cref="SATURN_RING_INNER"/>..<see cref="SATURN_RING_OUTER"/> at the equator courses, two
        /// deep and two thick; and four spoke rays on the lattice axes bridging the deliberate one-cell air
        /// gap between them. The checks, in the spec's order: the ~130-ball ring on four small spokes is
        /// the block's boldest hang, so measure the unshot death line FIRST and widen
        /// <see cref="SATURN_SPOKE_HALF"/> from 0.9 to 1.2 if it droops before touching anything else; each
        /// spoke must count at least 3 after rounding (the drawing gives 4 - two cells a course on each
        /// parity - so there is no slack to lose); and the yellow annulus must come out ONE group on the
        /// measured contact graph, ~35 % in the drop test and orphaning nothing.
        /// </para>
        /// </summary>
        private static Design Saturn() => new()
        {
            File = "Saturn.json",
            Name = "Saturn",
            Grid = 15,
            Depth = 12,
            Scene = SceneKind.Meadow,
            Sky = 1,
            Music = MUSIC_RINGS,
            Balls = BALLS_MEADOW,
            Shots = 36,
            CeilingStep = 7,
            Occupied = SaturnOccupied,
            Colour = SaturnColour,
        };

        //THE PLANET'S OWN FIGURES (#255). The sphere's EQUATOR sits at 6.5, between the ring's two courses
        //(RING_BASE and the one above), so the halo rides the planet's widest line; the sphere spans
        //i = 2..11 at GLOBE = 3.35 and the CAP flattens its top into ~9 glass bonds. The ring is the
        //annulus RING_INNER..RING_OUTER, two cells radially - and 6.3 is what keeps the widest course off
        //the field wall at both parities (the wall column's nearest cell sits at r = 6.52 unshifted). The
        //spokes run SPOKE_INNER..SPOKE_OUTER within SPOKE_HALF of a lattice axis, ~4 balls each after
        //rounding, bridging the one-cell air gap; 1.2 is the pre-authorised width if the unshot ring sags.
        private const float SATURN_GLOBE = 3.35f;
        private const float SATURN_EQUATOR = 6.5f;
        private const float SATURN_CAP = 1.9f;
        private const int SATURN_RING_BASE = 6;
        private const float SATURN_RING_INNER = 4.3f;
        private const float SATURN_RING_OUTER = 6.3f;
        private const float SATURN_SPOKE_INNER = 3.0f;
        private const float SATURN_SPOKE_OUTER = 4.5f;
        private const float SATURN_SPOKE_HALF = 0.9f;

        //A quarter turn: the two-sector seam lands on the +/-Z meridians, so the gun starting on +Z sees
        //both hemispheres from the first shot instead of one face-on and one hidden behind the mass.
        private const float SATURN_SEAM = 0.25f;

        /// <summary>
        /// Whether a cell is on the planet, its cap, its ring or a spoke. The sphere's own test bounds it
        /// to i = 2..11; the cap exists only on the anchor course; ring and spokes only on the two equator
        /// courses.
        /// </summary>
        private static bool SaturnOccupied(float r, float ang, int i, int depth)
        {
            if (SaturnSphere(r, i) <= SATURN_GLOBE) return true;

            //The flattened anchor cap: the bare sphere meets the glass in a single cell, and one glass
            //bond is a hinge, not an anchor.
            if (i == depth - 1 && r <= SATURN_CAP) return true;

            if (!SaturnRingCourse(i)) return false;

            return (r >= SATURN_RING_INNER && r <= SATURN_RING_OUTER) || SaturnSpoke(r, ang);
        }

        /// <summary>
        /// The planet's colour: the ring yellow, the spokes red, the sphere in two meridian halves. Ring
        /// before spoke, because the two windows overlap between <see cref="SATURN_RING_INNER"/> and
        /// <see cref="SATURN_SPOKE_OUTER"/> and the ring must stay uniform - it is coloured as ONE group on
        /// purpose, the block's loudest lesson.
        /// </summary>
        private static BallType SaturnColour(float r, float ang, int i, int depth)
        {
            if (SaturnRingCourse(i) && r >= SATURN_RING_INNER)
                return BallType.Type7; //yellow - the halo, one closed ~130-ball group

            if (SaturnRingCourse(i) && SaturnSpoke(r, ang))
                return BallType.Type1; //red - the four spokes, small isolated groups on purpose

            return Band(SectorIndex(ang, SATURN_SEAM, 2),
                new[] { BallType.Type3, BallType.Type2 }); //blue, green
        }

        /// <summary>The cell's true 3D distance from the sphere's centre on the equator line, the vertical
        /// in level units scaled by <see cref="INV_SQRT_TWO"/>.</summary>
        private static float SaturnSphere(float r, int i)
        {
            float dy = (i - SATURN_EQUATOR) * INV_SQRT_TWO;
            return MathF.Sqrt(r * r + dy * dy);
        }

        /// <summary>The two courses the equator falls between - the only ones carrying ring and spokes.</summary>
        private static bool SaturnRingCourse(int i) =>
            i == SATURN_RING_BASE || i == SATURN_RING_BASE + 1;

        /// <summary>
        /// Whether a cell is on one of the four spokes: within the radial window, and within
        /// <see cref="SATURN_SPOKE_HALF"/> of either lattice axis - one test draws all four rays, since the
        /// radial window already keeps the cell too far out to sit near both axes at once.
        /// </summary>
        private static bool SaturnSpoke(float r, float ang)
        {
            if (r < SATURN_SPOKE_INNER || r > SATURN_SPOKE_OUTER) return false;

            return MathF.Abs(r * MathF.Sin(ang)) <= SATURN_SPOKE_HALF
                || MathF.Abs(r * MathF.Cos(ang)) <= SATURN_SPOKE_HALF;
        }

        /// <summary>
        /// Three stone basins shrinking down a single green spine, each tier floating on open air, with a
        /// detached drop swinging under the lowest - the block's finale-adjacent level and its ideas
        /// compounded: sector-cut plates (three offset turbine tiers), a revolution silhouette, nested-ring
        /// packing on the tier rims (5.0 / 3.6 / 2.3, so each rim course sits in the field lattice's
        /// shifted pockets), and the springiest object in the pack - masses on open stems are a compound
        /// oscillator, so the tiers visibly bob out of phase from the first launch and every mid-stack hit
        /// sends a slow wave down through the basins.
        /// <para>
        /// The judged pick's graft is built in rather than optional: the finial is a detached bob
        /// (i = 0..2) joined to the spine by a one-course green neck at i = <see cref="FOUNTAIN_NECK"/>
        /// with open air around it - rejected Bell's independent pendulum, turning the launch into a
        /// four-mass oscillator. The neck course lands on a shifted field level, which the lattice gives 5
        /// cells at <see cref="FOUNTAIN_STEM"/> - over the spec's floor of 3 before rounding is even asked.
        /// </para>
        /// <para>
        /// The spine - every axis cell through every tier, both stems, the neck and the whole drop - is one
        /// continuous green group from the glass to the bob, so cutting it below a tier drops only what
        /// hangs beneath: releasing the whole spine orphans the middle and bottom tiers, ~61 %, a
        /// legitimate staged collapse under the 90 % gate and the intended finale. Each basin's annulus is
        /// three 120-degree sectors, the middle tier's boundaries turned half a sector against its
        /// neighbours' (<see cref="FOUNTAIN_STAGGER"/>), and all four colours stand on the glass - three
        /// sectors plus the spine's core through the top disc. The spec's checks: unshot death-line sag
        /// FIRST (~110 balls of lower tiers plus the drop hang on the two stem segments; the pre-authorised
        /// fix is widening FOUNTAIN_STEM to 1.7, which the silhouette tolerates, BEFORE shrinking any
        /// basin), the drop clearing the death line (lift the bob one course and keep the air gap if not),
        /// and the one-column margin at the 5.0 top disc - which holds exactly: the glass course is
        /// shifted, its rim cell sits at r = 5.0 in halves that are exact in binary (the
        /// <see cref="OneCourse"/> argument), index 11 of 13.
        /// </para>
        /// </summary>
        private static Design Fountain() => new()
        {
            File = "Fountain.json",
            Name = "Fountain",
            Grid = 13,
            //The deepest level in the block, and framed whole: fourteen in an eighteen-level field is
            //REVEAL_FIELD_LEVELS' own arithmetic - offset 4, even, and 18 is the deepest field the game
            //frames without cropping (see that constant's doc).
            Depth = 14,
            FieldLevels = 18,
            Scene = SceneKind.Meadow,
            Sky = 1,
            Music = MUSIC_RINGS,
            Balls = BALLS_MEADOW,
            //The block's largest budget and slowest ceiling: the most standing groups (the spine plus nine
            //sectors) price it as the honest top of the ramp.
            Shots = 42,
            CeilingStep = 6,
            Occupied = FountainOccupied,
            Colour = FountainColour,
        };

        //THE FOUNTAIN'S OWN FIGURES (#255). Above the neck the body is TIERS read two courses an entry
        //from BASE - the Bullseye terrace idiom - so the discs at i = 4..5, 8..9 and 12..13 and the open
        //STEM segments between them are one table: a stem is simply a tier whose disc is the spine's own
        //radius. Below it, the grafted pendant: NECK is the one-course green joint at i = 3, and the drop
        //is a sphere of DROP about BOB (i = 1), spanning i = 0..2 with air on every side.
        private const int FOUNTAIN_BASE = 4;
        private const int FOUNTAIN_NECK = 3;
        private const int FOUNTAIN_DROP_TOP = 2;
        private const float FOUNTAIN_BOB = 1f;
        private const float FOUNTAIN_DROP = 1.8f;
        private const float FOUNTAIN_STEM = 1.35f;
        private static readonly float[] FOUNTAIN_TIERS = { 2.3f, FOUNTAIN_STEM, 3.6f, FOUNTAIN_STEM, 5.0f };
        private const int FOUNTAIN_MIDDLE_TIER = 2;

        //Three sectors a basin, the middle tier's boundaries turned a sixth of a turn (60 degrees - half a
        //sector) against the top and bottom tiers', so from below the tiers read as offset turbine plates.
        private const int FOUNTAIN_SECTORS = 3;
        private const float FOUNTAIN_STAGGER = 1f / 6f;

        /// <summary>
        /// Whether a cell is on the fountain: the drop sphere at the bottom, the neck course joining it,
        /// and above that whatever radius <see cref="FOUNTAIN_TIERS"/> grants the course's tier - solid
        /// discs and stem segments off one table.
        /// </summary>
        private static bool FountainOccupied(float r, float ang, int i, int depth)
        {
            if (i <= FOUNTAIN_DROP_TOP) return FountainDrop(r, i);
            if (i == FOUNTAIN_NECK) return r <= FOUNTAIN_STEM;

            return r <= FOUNTAIN_TIERS[(i - FOUNTAIN_BASE) / 2];
        }

        /// <summary>
        /// The fountain's colour: green for the spine - every axis cell through every tier, the stems, the
        /// neck and the whole drop, one continuous group from the glass to the bob, which is the
        /// tier-by-tier collapse's own fuse - and each basin's annulus in three 120-degree sectors around
        /// it.
        /// </summary>
        private static BallType FountainColour(float r, float ang, int i, int depth)
        {
            if (i <= FOUNTAIN_NECK || r <= FOUNTAIN_STEM)
                return BallType.Type2; //green - the spine and the pendant drop

            //The middle tier's boundaries sit at 60/180/300 (twist 0) against the top and bottom tiers'
            //0/120/240, so no sector seam runs straight down the stack.
            float spin = (i - FOUNTAIN_BASE) / 2 == FOUNTAIN_MIDDLE_TIER ? 0f : FOUNTAIN_STAGGER;

            return Band(SectorIndex(ang, spin, FOUNTAIN_SECTORS),
                new[] { BallType.Type7, BallType.Type1, BallType.Type3 }); //yellow, red, blue
        }

        /// <summary>Whether a cell is inside the pendant drop - a true sphere about the course at
        /// <see cref="FOUNTAIN_BOB"/>, asked only at i = 0..2 so the neck course stays the neck's.</summary>
        private static bool FountainDrop(float r, int i)
        {
            float dy = (i - FOUNTAIN_BOB) * INV_SQRT_TWO;
            return MathF.Sqrt(r * r + dy * dy) <= FOUNTAIN_DROP;
        }

        #endregion

        //THE HANGING DOMES' OWN GEOMETRY. OnionVertical measures a level's offset from the layout's own middle,
        //which is what a sphere wants; a dome hangs from the GLASS, so it measures how far a level has dropped
        //below the layout's top instead. Same 1/sqrt(2): a distance built from i and r untouched comes out
        //stretched along Y, because a level is not one lattice unit tall.
        private static float DomeDrop(int i, int depth) => (depth - 1 - i) * INV_SQRT_TWO;

        //The toadstool's own geometry. The cap's radius is set by the palette (three rings of Ring's own 1.9
        //want a rim past 3.8), the squash by the stalk (a true hemisphere of this radius would fill the layout),
        //and the stalk by the lattice (1.7 is 12 cells on an unshifted level and 9 on a shifted one).
        private const float TOADSTOOL_CAP = 5.3f;
        private const float TOADSTOOL_SQUASH = 0.68f;
        private const float TOADSTOOL_STALK = 1.7f;

        //Four radial cuts across the cap. More reads better as gills and measures better (eight sectors give
        //15 shots), but it also cuts the level into crumbs: at eight the best single shot is 9 % of the
        //cluster, and this block is the one that teaches what a colour group IS, so its payoffs have to stay
        //big enough to notice. Four keeps the biggest shot at 21 %, in the band One and Bullseye sit in.
        private const int TOADSTOOL_GILLS = 4;

        /// <summary>
        /// The dome's distance with the vertical scaled: <paramref name="stretch"/> is how many times taller than
        /// wide the body is, so 1 is a hemisphere, below 1 squashes it into a cap and above 1 draws the pole down
        /// into a teardrop. Stated as a factor rather than as a second radius because it is the one number that
        /// has to agree with <c>Depth</c>: the body reaches <c>radius * stretch</c> below the glass, and
        /// <c>(Depth - 1) / sqrt(2)</c> is how far there is to reach.
        /// </summary>
        private static float DomeDistance(float r, int i, int depth, float stretch)
        {
            float dy = DomeDrop(i, depth) / stretch;
            return MathF.Sqrt(r * r + dy * dy);
        }
    }
}
