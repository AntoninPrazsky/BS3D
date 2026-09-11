using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>The Reveal</b>, block 5 of the campaign: its designs, and the helpers no other block's designs use, in
    /// the order <c>Program.cs</c> held them — the play order is <see cref="Main"/>'s, and the block's name, music
    /// and ball style are in the tables there. Split out of <c>Program.cs</c> in #386.
    /// </summary>
    internal static partial class Program
    {

        /// <summary>
        /// A sphere hanging whole from the glass - the roundest shape the lattice can make - coloured like a
        /// halved onion: a yellow skin around a white bulk around a small green heart, peeled from the
        /// outside in as the player clears it.
        /// <para>
        /// <see cref="SphereDistance"/> is true 3D distance from the sphere's own centre, with the level
        /// index scaled by <c>1/sqrt(2)</c> to match <c>BallsMap.GetRealPosition</c>'s vertical spacing - a
        /// radius built from <c>i</c> and <c>r</c> untouched comes out an egg, stretched along Y, because a
        /// level is not one lattice unit tall. <see cref="Depth"/> and the field's own centre are chosen so
        /// the sphere's north and south poles land exactly on the layout's top and bottom levels.
        /// </para>
        /// <para>
        /// The skin cannot wrap the whole sphere: the top layer is the one bonded to the glass, and a top
        /// layer of one colour anchors everything under it to that colour's single group (the rule every
        /// design here answers - see <see cref="Validate"/>). A true 3D shell also narrows to a single point
        /// at each pole, so whichever colour that point falls in becomes the entire cap. <see cref="OnionShell"/>
        /// answers both at once: it rings each level by its <b>own</b> radius rather than by distance from
        /// the sphere's centre, so every level - however small its own cap is - shows the same green-centre,
        /// white-ring, yellow-rim proportions the equator does. That is also just what a real onion's rings
        /// look like at any height: narrower near the root and stem, never absent.
        /// </para>
        /// </summary>
        private static Design Onion() => new()
        {
            File = "Eleven.json",
            Name = "Onion",
            Grid = 15,
            //One short of the true round number (see SphereDistance's own remarks): a mathematically exact
            //sphere at this radius wants Depth 16, whose top and bottom layers are then single points too
            //narrow to reliably carry all three colours (the annulus a lattice this coarse needs to land a
            //ball in it). Fifteen trims a hair off each pole instead - a slightly flattened onion, which is
            //closer to a real one's shape than a mathematical sphere is anyway.
            Depth = 15,
            //At the generous end of the pack's usual growth room (12, matching Bullseye/Pinwheel/Static):
            //the sphere's own bottom pole already reaches the layout's own floor, unlike every stepped-cone
            //design here, which tapers to its point several levels above the layout ends - so this design
            //has no such margin built in above the field's own growth room and wants the full amount of it.
            FieldLevels = 27,
            //The cavern since #194, where this opens the Reveal block: it is the family's original, so it
            //teaches the peel before the four levels that hide something other than a smaller sphere. The forest
            //it came from was never argued for, and the cavern is the one scene the pack documents as chosen for
            //being dark and enclosed — which is what a reveal wants, since it makes the cluster the only lit
            //thing in the frame. It inherits that pairing from Mosaic, which vacated it for the Moon.
            Scene = SceneKind.Cavern,
            //Inert here: the cavern is one of the four sky-replacing scenes, so this number pins nothing but
            //what the settings row cycles from (SceneRenderer.ReplacesSky, #142).
            Sky = 13,
            Music = MUSIC_REVEAL,
                Balls = BALLS_REVEAL,
            Shots = 48,
            CeilingStep = 8,
            Occupied = (r, ang, i, depth) => SphereDistance(r, i, depth) <= ONION_RADIUS,
            Colour = (r, ang, i, depth) => OnionShell(r, ang, i, depth),
        };

        #region The reveals (#161)

        //Onion was the family's original and these are the answer to what #161 actually asked for: an inside
        //that is a DIFFERENT SHAPE, "a mango, or a kinder surprise", and not a smaller sphere of another colour.
        //Four facts decide how one is built, and three of them are things Onion does not do.
        //
        //THE UNDERSIDE IS THE FACE THE PLAYER SEES. The cluster hangs from the glass and the gun stands on the
        //island below it, so anything open at the bottom shows its inside from the first frame. A bell with a
        //clapper in it is not a reveal at all; every body here is closed underneath.
        //
        //A BODY HUNG FROM ITS WIDEST SECTION HAS NO ANCHOR PROBLEM. The anchor layer is the layout's top level
        //and nothing else touches the glass, so whatever that layer is made of decides the level: one colour
        //there and the first matching ball takes everything. A sphere narrows to a point up there, which is the
        //trap OnionShell answers by RE-COLOURING each level by its own radius - measured, Onion's anchor layer is
        //16 cells in two colours. Answer it with the SILHOUETTE instead and the colouring is free: Chest's lid is
        //49 cells in four colours, Mango's shoulder 66 in five.
        //
        //THE PAYOFF MUST HANG ON ITS OWN, and this is the one no gate can see, because Validate only reads the
        //layout as authored. Measured off the emitted files: keep only Onion's green heart and all 161 balls of
        //it fall - it is held up by the white around it, so the thing the level is named for cannot outlive the
        //peel. Every payoff here reaches the glass through its own cord, stalk or stem and measures 0 balls
        //falling when everything else is taken away.
        //
        //AND IT MUST BE SEALED. A payoff a shot can already touch is not a reveal; 20 of Onion's 161 heart balls
        //are on the outside at the start. Sealed costs something worth naming: the magazine draws evenly among
        //the colours still ALIVE, so part of the draw cannot clear anything until the shell is breached. It is
        //not a dead shot - a payoff-coloured ball lands on the shell and becomes a seed of its own colour out
        //there - but it is a slow one, and it is why these budgets are generous against their clears.

        /// <summary>
        /// A flat-faced box with a <b>ball in it</b> — the family's plainest statement, and the one that teaches
        /// the block. A crate of dark iron, red paint and brass in chunky two-cell slats; six closed faces, so
        /// what the player sees at the start is a solid box and nothing else. It is <b>hollow</b>, and the first
        /// slat that comes away opens onto a dark cavity with a pearl hanging in it on a short cord.
        /// <para>
        /// Square where every other reveal here is turned: the box is cut on the <b>Chebyshev</b> extent (the
        /// square half-extent <see cref="Chebyshev"/> recovers from the polar pair, taken straight off the offsets
        /// here because <see cref="ChestPart"/> already works in the lattice frame), so its faces are straight in
        /// WORLD space and alternate 8 cells and 7 with the packing's half-cell stagger — a brick bond, which is
        /// what a crate should look like anyway.
        /// </para>
        /// <para>
        /// <b>The pearl hangs from the lid, not from the box.</b> A ball resting in a cavity is connected for the
        /// validator and gone the moment the shell is cleared, which is the defect the family's original carries;
        /// the cord is a column of the pearl's own colour up the axis and through the lid to the glass, so
        /// measured, keeping only the pearl leaves 61 balls with <b>0 falling</b>. At the start 0 of those 61 are
        /// on the outside — the cavity is sealed, so it floods to nothing from outside the field.
        /// </para>
        /// <para>
        /// Slats colour by <c>(x / 2) + (z / 2)</c>, which is <see cref="Mosaic"/>'s rule with the level term
        /// dropped: constant sums run along the one diagonal a cell never touches across levels (its cross-level
        /// neighbours are the <c>+1,+1</c> pair, never <c>+1,−1</c>), so no two same-coloured columns merge.
        /// </para>
        /// <para>
        /// Measured: 630 balls — the heaviest reveal here — margin 3, nothing alone, 12 in pairs (box cells
        /// clipped by the pearl; <see cref="Static"/> ships 14), nothing recoloured, and the flattest report in
        /// the block: best single shots 48, 48, 48 and 61, i.e. 7 %, 7 %, 7 % and 9 %. <b>The grain is not as
        /// even as "one 2x2 column each" suggests, and the budget is set from the measurement rather than from
        /// that model.</b> There are 30 groups, sized 2, 2, 2, 2, 2, 2, 3, 3, 3, 4, 8, 8, 8, 8, 8, 8, 10, 30, 36,
        /// 36, 36, 36, 36, 36, 48, 48, 48, 48, 48 and 61 — the cavity splits the four centre columns into a lid
        /// fragment and a floor fragment that never touch, which is where the eights, threes and twos come from.
        /// So the shell is 29 groups and the pearl one, i.e. <b>30 perfect landings minimum</b>, not the thirteen
        /// a uniform-column model predicts. 56 shots is <see cref="Mosaic"/>'s budget for a comparable count;
        /// at the 40 this was first authored with it would have been tighter than Colossus, which the docs call
        /// the wall, on the level that teaches the block.
        /// </para>
        /// </summary>
        private static Design Chest() => new()
        {
            File = "Chest.json",
            Name = "Chest",
            Grid = CHEST_GRID,
            Depth = CHEST_DEPTH,
            Scene = SceneKind.Cavern,
            //Inert in the cavern (SceneRenderer.ReplacesSky, #142); the block's number, as Onion states it
            Sky = 13,
            Music = MUSIC_REVEAL,
                Balls = BALLS_REVEAL,
            Shots = 56,
            //TWELVE since #239, where it was nine, and it is the block's difficulty CURVE rather than this
            //level's own taste. Measured on the running game - the cluster's lowest ball at the start against
            //the death line, less what the budget's own descents spend - the Reveal block reads 5.38, 1.77,
            //2.98, 3.83 and 1.99 units of margin across Onion, Chest, Fossil, Mango and Lantern. Chest was the
            //tightest in the block AND stood directly behind the roomiest, a threefold drop between two
            //neighbours, which is what the owner reported as a difficulty spike. Nine steps to twelve costs the
            //level two of its six descents and puts it at 2.97, alongside Fossil - the level it is compared
            //against. The shape and its 630 balls are untouched: what was wrong here was the clock.
            CeilingStep = 12,
            OccupiedBlock = (x, z, i, depth) => ChestPart(x, z, i) != 0,
            BlockColour = (x, z, i) => ChestPart(x, z, i) == 2
                ? BallType.Type4
                : Band((x / 2) + (z / 2), new[] { BallType.Type8, BallType.Type1, BallType.Type7 }),
        };

        /// <summary>
        /// A plain boulder of masonry-blocked stone with a <b>fern frond pressed flat inside it</b> — the one
        /// reveal here whose payoff is a PICTURE, drawn off a bitmap in the source the way the Gallery block's
        /// walls are (#130) and buried in the middle of a solid of revolution. Outside it is a rock and gives
        /// nothing away; halfway there is green showing through a hole; the payoff is a flat frond hanging in the
        /// dark, facing the gun.
        /// <para>
        /// The picture plane is <c>(x, level)</c> and the slab is <see cref="FOSSIL_SLAB"/> thick in Z, for the
        /// reasons the pictures region states in full: the gun starts at +Z looking at the origin, so X runs
        /// across the screen and the level axis up it, and a slab one cell thick in Z has half its cross-level
        /// neighbours reaching to a Z that is not there. Two to three cells is what makes it a solid plate at
        /// either parity.
        /// </para>
        /// <para>
        /// <b>The frond is clipped to <see cref="FOSSIL_BURY"/> inside the rim, and that one rule does three
        /// jobs.</b> It seals the picture — measured 0 of its 92 balls on the outside, where the first draft
        /// showed 12 at the leaflet tips, any one of which is a green ball away from dropping the payoff before it
        /// has been seen. It tapers the frond for free, since the rim narrows towards the crown and the floor, so
        /// the leaflets shorten exactly where a real frond's do. And it makes the bitmap safe to redraw: whatever
        /// is drawn, the stone wins at the surface.
        /// </para>
        /// <para>
        /// The stem is in columns 4 and 5 of <b>every</b> row, so the frond is one connected group reaching the
        /// anchor layer — measured, keeping only the frond leaves 92 balls with 0 falling, and the anchor layer is
        /// 29 cells in four colours (9 dark, 9 sandstone, 5 blue, 6 fern).
        /// </para>
        /// <para>
        /// Stone in 3×3×3 blocks — <see cref="Lean"/>'s rule, the one colouring in the pack that reads as a
        /// <b>built</b> thing rather than a turned one, which is what rubble wants. A hashed <see cref="Scatter"/>
        /// was the first try and it is the wrong tool here: three colours over 3×3 blocks percolate, and one of
        /// them came out as a single connected group of 190 — a <b>33 %</b> single shot, measured.
        /// </para>
        /// <para>
        /// Measured: 562 balls, margin 3, nothing alone, 2 in pairs, nothing recoloured, best single shots 51
        /// (9 %), 72 (12 %), 80 (14 %) and the frond's own 92 (16 %). A picture is one group by construction, and
        /// on a level whose point is being recognised its coming away in one piece is the reward — the shipped
        /// pictures sit at 40 to 52 % for the same reason. <b>The block rule does not make 27-ball blocks</b>, and
        /// that is worth knowing before the budget is retuned: constant values of <c>(x/3)+(z/3)+(i/3)</c>
        /// percolate along the <c>(+x, −level)</c> diagonal, because a cell's cross-level neighbours are the
        /// <c>{0,+1}²</c> pair on odd levels and <c>{−1,0}²</c> on even ones, so the stone comes out as 15 groups
        /// of 51 to 80 rather than dozens of 27. Sixteen standing groups in all, on 36 shots.
        /// </para>
        /// </summary>
        private static Design Fossil() => new()
        {
            File = "Fossil.json",
            Name = "Fossil",
            Grid = FOSSIL_GRID,
            Depth = FOSSIL_DEPTH,
            Scene = SceneKind.Cavern,
            Sky = 13,
            Music = MUSIC_REVEAL,
                Balls = BALLS_REVEAL,
            //The slowest ceiling in the block, because this is the one level whose payoff has to be LOOKED at to
            //be got: the picture is the reward and it arrives late. Fifteen stone groups and the frond, so 36
            //shots is a little over two a group.
            Shots = 36,
            CeilingStep = 9,
            OccupiedBlock = (x, z, i, depth) => FossilRock(x, z, i),
            BlockColour = (x, z, i) => FossilFern(x, z, i)
                ? BallType.Type2
                : Band((x / 3) + (z / 3) + (i / 3), new[] { BallType.Type8, BallType.Type3, BallType.Type4 }),
        };

        /// <summary>
        /// A lopsided fruit with an <b>off-centre stone</b> in it: red and olive peel in eight vertical strips,
        /// gold and orange flesh in eight more half a strip out of phase, and a flat brown stone hanging off the
        /// axis on its own stalk. It is #161's own word made into a level. Three acts, and the middle one is the point
        /// — <b>the flesh cannot be touched until the peel is broken</b>, so the level plays peel, eat, then the
        /// stone.
        /// <para>
        /// <b>It hangs from its widest section and that is the whole answer to the anchor rule.</b> The profile is
        /// widest at the glass and tapers to a blunt nose (<see cref="MANGO_DROP"/> is where it would close to
        /// nothing, well below where the layout ends, which is what leaves the nose six cells across instead of a
        /// point with nothing to touch). So the anchor layer is a full cross-section of the fruit: measured 66
        /// cells carrying all five colours — 18 red, 18 olive, 14 gold, 10 orange, 6 stone — where Onion hangs
        /// 959 balls off 16 cells in two colours. No colouring trick is needed on top of it.
        /// </para>
        /// <para>
        /// <b>The stone hangs on a stalk of its own colour</b>, up the same off-centre column to the glass.
        /// Measured, keeping only the stone leaves 52 balls with 0 falling: the peel and the flesh can both go and
        /// it is still hanging, which is the property the family's original does not have (keep only Onion's green
        /// heart and all 161 balls of it fall).
        /// </para>
        /// <para>
        /// <b>The peel is tested before the stone and that is load-bearing.</b> A cell within
        /// <see cref="MANGO_SKIN"/> of the surface is peel whatever the stone's numbers say, so the stone cannot
        /// break the surface however it is retuned — measured 0 of its 52 balls on the outside. Where it would poke
        /// out it is simply clipped, which is what a real stone lying against the belly of a mango looks like. The
        /// 8 flesh balls of 165 that ARE reachable at the start are the lattice rounding the peel thin on the
        /// flanks, and they are the level's only early foothold.
        /// </para>
        /// <para>
        /// Strips rather than shells for the 90 % rule: a shell wrapping a body is one group, which is what cost
        /// Onion 604 balls in one before its boundary was made to swing. Eight strips of two colours means
        /// neighbours never agree. Measured: 530 balls, margin 2, nothing alone, nothing in a pair, 2 recoloured,
        /// thirteen standing groups from 3 to 81, and best single shots 41 (7 %), 45 (8 %), 52 (9 %), 78 (14 %)
        /// and 81 (15 %). The two flesh figures are the higher ones because the wedges all meet on the axis above
        /// and below the stone, so each flesh colour is one piece — the stone standing off-centre is what keeps it
        /// to two rather than one.
        /// </para>
        /// </summary>
        private static Design Mango() => new()
        {
            File = "Mango.json",
            Name = "Mango",
            Grid = 15,
            //Ten, so the fruit is wider than it is tall and the field keeps six levels of growth room under it -
            //which is also the descent room: five ceiling steps at 0.6 each against the 4.79 world units the
            //lowest ball starts above the death line.
            Depth = 10,
            Scene = SceneKind.Cavern,
            Sky = 13,
            Music = MUSIC_REVEAL,
                Balls = BALLS_REVEAL,
            //Five colours is the hardest draw in the block and this is its last level. A clean clear is 18 to 22
            //shots, so 36 puts four stars at about 0.6 of the budget.
            Shots = 36,
            CeilingStep = 7,
            Occupied = (r, ang, i, depth) => r <= MangoRim(ang, i, depth),
            Colour = MangoInside,
        };

        /// <summary>
        /// A drum of coloured panes with a <b>shaft up the middle that nothing outside it shows</b>: solid at both
        /// ends, hollow between, so it reads as a whole vessel until the bottom plug goes and the drain opens up
        /// under it. <see cref="Crown"/> does its hole in the open and the player aims around it from the first
        /// shot; hiding it makes the hole a reward — and, the part worth having, <b>a new line of fire</b>: with
        /// the bore open, a shot up the axis reaches the INSIDE of the top plug, which is the layer bonded to the
        /// glass. It is the block's one reveal whose payoff is not a thing but an absence.
        /// <para>
        /// <b>The plugs carry a colour of their own, and that is the mechanic.</b> Magenta is in the two plugs and
        /// nowhere else, so it leaves the draw when the plugs do, and half of it is in the TOP plug, which cannot
        /// be touched until the shaft is open. The level therefore keeps handing the player a colour whose only
        /// remaining home is up the bore, which is what makes the reveal change the magazine and not just the
        /// view. Blue is in both the plugs and the panes on purpose, so the bottom plug's other half comes away
        /// with the wall rather than having to be picked out of it.
        /// </para>
        /// <para>
        /// <b>The plugs are two levels each, and the first draft's one level was a tax rather than a mechanic.</b>
        /// At one level the plugs were 20 balls of 417 in groups of 4, 4, 4, 4, 4, 2 and 2 — a sixth of the draw
        /// for 4 % of the cluster, on the longest budget in the block, which is exactly the pattern
        /// <see cref="Helix"/>'s own remarks reject in writing for its rungs. Two levels doubles the plugs without
        /// touching the wall or the palette, at the cost of two levels of bore. Measured after: magenta is 40 balls
        /// in groups of 10 rather than 20 in groups of 4, which is a colour worth drawing.
        /// </para>
        /// <para>
        /// Measured: 454 balls, margin 2, nothing alone, nothing in a pair, 2 recoloured, and the <b>flattest
        /// colour spread in the pack</b> — the largest standing group of any colour is 29 and every other one is
        /// 14 or less, so best single shots are 3 %, 6 %, 3 %, 2 % and 3 %. There is no plate anywhere on it, which
        /// is why it takes the block's longest budget and closes it.
        /// </para>
        /// <para>
        /// The wall is <b>panes, not staves</b>: six wedges rolled by the course, which is what keeps a
        /// two-cell-thick tube from being six groups of seventy. <b>The roll is 2 and the palette is four, and
        /// neither is free.</b> A cell's cross-level neighbours sit half a cell away in x and z, so the wedge one
        /// along and the course one up TOUCH — with a roll of ±1 that neighbour is the same colour and the panes
        /// fuse into a helix. Measured on the three-colour first cut: 460 wall balls in about eight groups, two of
        /// them 78. A roll of 2 against four colours differs by 1 or 3 in every direction that touches and repeats
        /// only at the opposite wedge and two courses away, neither of which is adjacent.
        /// </para>
        /// </summary>
        private static Design Lantern() => new()
        {
            File = "Lantern.json",
            Name = "Lantern",
            Grid = 13,
            Depth = 10,
            FieldLevels = REVEAL_FIELD_LEVELS,
            Scene = SceneKind.Cavern,
            Sky = 13,
            Music = MUSIC_REVEAL,
                Balls = BALLS_REVEAL,
            //The finest grain in the block - panes of a dozen rather than plates - so the longest budget of the
            //five, and it closes the block on it
            Shots = 58,
            CeilingStep = 7,
            //A tube, plugged at both ends. The bore is the reveal and the caps are what hide it.
            Occupied = (r, ang, i, depth) => r <= LANTERN_OUTER
                && (r >= LANTERN_BORE || i < LANTERN_CAP || i >= depth - LANTERN_CAP),
            //Panes on the wall, diced plugs at the ends. The plug palette is deliberately NOT the wall's:
            //magenta lives only there, so clearing the plugs takes a colour out of the draw.
            Colour = (r, ang, i, depth) => r >= LANTERN_BORE
                ? Band(SectorIndex(ang, 0f, LANTERN_PANES) + 2 * (i / LANTERN_COURSE),
                       new[] { BallType.Type7, BallType.Type1, BallType.Type5, BallType.Type3 })
                : BandPolar(r, ang, i / 2, new[] { BallType.Type3, BallType.Type6 }),
        };

        #endregion

        #region The Reveal's second hang (#255)

        //THE REVEAL'S SECOND FIVE (#255). The block's rule is unchanged - an outer body with a
        //DIFFERENTLY-SHAPED thing standing inside it, and clearing the outside is the payoff (#161) - and
        //what the five add is WHAT is found in there. The first hang's payoffs are all objects at rest: a
        //nested shell, a ball in a box, a picture in a rock, a stone on a stalk, an open shaft. These five
        //are payoffs that DO something, which is the owner's physics ask arriving inside this block's own
        //grammar: a star whose light is the colour the level withheld (Spark), a roof of hanging spires
        //(Grotto), a balance frozen mid-tip that springs when a pan is cut (Scales), a ship riding at
        //anchor (Ship), and a weight on a double coil that boings the moment the barrel opens (Spring).
        //
        //THE ONE STRUCTURAL RULE ALL FIVE OBEY is the block's own: the outer body and the thing inside it
        //are coloured out of DISJOINT palettes, so no group can ever span both and a shot that opens the
        //shell cannot take the payoff with it. Each design's doc states its two sets. The second rule is
        //the cavern's: the scene replaces the sky and drives its own dim light rig, so an inner payoff is
        //painted in something that reads hot in the dark (yellow, red, orange) against an outer shell that
        //does not.
        //
        //NUMBERS BELOW ARE THE DRAWINGS', except where a doc says a figure was measured. Each design's doc
        //names the checks its judged spec flagged and what was found when they were run.

        /// <summary>
        /// A dull faceted crystal with a <b>blazing yellow star buried in its heart</b> - the block's
        /// opener and its plainest statement: the outside is one cold stone in two colours and gives away
        /// nothing, and what is inside it is the only warm thing in the level.
        /// <para>
        /// The shell is a taxicab diamond - <see cref="SPARK_WIDE"/> at its widest course, drawing in
        /// <see cref="SPARK_TAPER"/> a course either way from the middle, so it reads as a crystal cut
        /// rather than as a turned body - and it is hollow at <see cref="SPARK_SHELL"/> two cells thick,
        /// with the bottom two courses solid so the point is a real tip. The top four courses are clamped
        /// to <see cref="SPARK_CHIMNEY"/>, which is a flat-topped chimney rather than a taper: it is what
        /// carries the star's two threads up to the glass, and the star hangs off those.
        /// </para>
        /// <para>
        /// <b>The star is a plus sign in three dimensions</b>: a 2x2x2 core with four two-ball arms on one
        /// level and a spike under it, every cell inside <see cref="SPARK_STAR_REACH"/> of the axis and so
        /// a whole cell clear of the shell's inner face at its narrowest. It is one connected yellow group
        /// by construction, and the threads that hold it are the only other warm thing in the level.
        /// </para>
        /// <para>
        /// Gate watch, in the judged spec's order, and all three were run rather than reasoned about: the
        /// threads never round into contact with the chimney wall (the clamp stayed at 4.0 and needed no
        /// widening); the spike stays fused to the core; and the solid silver tip is seated on all four
        /// facets, so no single outer release orphans it. The disjoint palettes are {magenta, cyan,
        /// silver} outside against {yellow, orange} inside.
        /// </para>
        /// </summary>
        private static Design Spark() => new()
        {
            File = "Spark.json",
            Name = "Spark",
            Grid = SPARK_GRID,
            Depth = SPARK_DEPTH,
            FieldLevels = REVEAL_FIELD_LEVELS,
            Scene = SceneKind.Cavern,
            //Inert in the cavern (SceneRenderer.ReplacesSky, #142); the block's number, as Onion states it
            Sky = 13,
            Music = MUSIC_REVEAL,
                Balls = BALLS_REVEAL,
            Shots = 42,
            CeilingStep = 8,
            OccupiedBlock = (x, z, i, depth) => SparkPart(x, z, i) != 0,
            BlockColour = SparkColour,
        };

        //THE CRYSTAL'S OWN FIGURES. Twelve courses in the block's eighteen-level field: the offset is 6 and
        //even, which is what keeps every course's parity where it was drawn.
        private const byte SPARK_GRID = 15;
        private const byte SPARK_DEPTH = 12;

        //The taxicab profile: WIDE at the middle course, drawing in TAPER a course either way, and the
        //shell SHELL cells thick. MIDDLE is between courses 5 and 6, so the crystal is a true mirror.
        private const float SPARK_WIDE = 6.4f;
        private const float SPARK_TAPER = 0.8f;
        private const float SPARK_MIDDLE = 5.5f;
        private const float SPARK_SHELL = 2f;

        //The chimney: the top SPARK_CHIMNEY_FROM courses are clamped to this half-width instead of tapering,
        //which is what leaves a shaft for the star's threads. Its inner face is therefore at 2.0 and the
        //threads run at a taxicab radius of 1, a clear cell inside it - the judged spec's first check, and
        //the reason the clamp is not simply the taper continued.
        private const float SPARK_CHIMNEY = 4f;
        private const int SPARK_CHIMNEY_FROM = 8;

        //Where the shell stops being hollow: the bottom two courses are solid, so the crystal has a point
        //rather than a hole in its underside.
        private const int SPARK_SOLID_TO = 1;

        //How far the star reaches from the axis. Against the chimney's 2.0 inner face and the taper's
        //narrowest 3.6, that is a whole cell of daylight at every course the star occupies.
        private const float SPARK_STAR_REACH = 3f;

        /// <summary>
        /// The crystal's taxicab half-width at a course: <see cref="SPARK_WIDE"/> less
        /// <see cref="SPARK_TAPER"/> for every course away from the middle, clamped to
        /// <see cref="SPARK_CHIMNEY"/> over the top four so the threads have a shaft to climb.
        /// </summary>
        private static float SparkWidth(int i) =>
            i >= SPARK_CHIMNEY_FROM
                ? SPARK_CHIMNEY
                : SPARK_WIDE - SPARK_TAPER * MathF.Abs(i - SPARK_MIDDLE);

        /// <summary>
        /// What a cell is: 0 nothing, 1 the crystal shell, 2 the star, 3 a thread. Written as one function
        /// because the star has to be tested first - it lives inside the shell's own hollow, and a cell
        /// that is both is the star.
        /// </summary>
        private static int SparkPart(int x, int z, int i)
        {
            float dx = x - SPARK_AXIS;
            float dz = z - SPARK_AXIS;
            float m = MathF.Abs(dx) + MathF.Abs(dz);

            //THE STAR, and its threads. A plus sign in three dimensions: the core is the 2x2 pair of
            //columns nearest the axis over two courses, the arms reach two cells out from it on one course,
            //and the spike hangs under it. The threads are two single columns climbing the chimney to the
            //glass, which is the whole load path - the star hangs off them and off nothing else.
            if (i >= SPARK_THREAD_FROM && (SparkThread(x, z, 0) || SparkThread(x, z, 1))) return 3;

            if (i >= SPARK_CORE_LOW && i <= SPARK_CORE_HIGH && m <= SPARK_CORE_REACH) return 2;
            if (i == SPARK_ARM_LEVEL && m <= SPARK_STAR_REACH) return 2;
            if (i >= SPARK_SPIKE_LOW && i < SPARK_CORE_LOW && m <= SPARK_CORE_REACH) return 2;

            //THE SHELL. Solid over the bottom courses so the crystal has a tip, two cells thick above them.
            float width = SparkWidth(i);
            if (m > width) return 0;

            return i <= SPARK_SOLID_TO || m > width - SPARK_SHELL ? 1 : 0;
        }

        //The axis the crystal is cut about, in the same real units the lattice shifts odd levels by - the
        //centre of the field's top level, One's own arithmetic.
        private const float SPARK_AXIS = (SPARK_GRID - 1) * 0.5f + 0.5f;

        //THE STAR'S COURSES. The core is two courses of a 2x2 column pair, the arms reach out on the upper
        //of them, and the spike hangs two courses under it - fused to the core rather than free, which is
        //the judged spec's second check and the reason SPARK_SPIKE_LOW is not lower.
        private const int SPARK_CORE_LOW = 5;
        private const int SPARK_CORE_HIGH = 6;
        private const int SPARK_ARM_LEVEL = 6;
        private const int SPARK_SPIKE_LOW = 3;
        private const float SPARK_CORE_REACH = 1f;

        //Where the threads start, which is the course above the star's arms: they climb from there to the
        //glass through the chimney.
        private const int SPARK_THREAD_FROM = 7;

        /// <summary>
        /// Whether a cell is one of the star's two threads - the columns either side of the axis, a taxicab
        /// unit out, which is a whole cell inside the chimney's 2.0 face at every course they climb.
        /// </summary>
        private static bool SparkThread(int x, int z, int which)
        {
            float dx = x - SPARK_AXIS;
            float dz = z - SPARK_AXIS;

            return which == 0
                ? MathF.Abs(dx + 0.5f) < 0.01f && MathF.Abs(dz + 0.5f) < 0.01f
                : MathF.Abs(dx - 0.5f) < 0.01f && MathF.Abs(dz - 0.5f) < 0.01f;
        }

        /// <summary>
        /// The crystal in two facet colours and the star in two of its own. The shell is cut into four
        /// facets by the SIGNS of dx and dz, and opposite facets share a colour: two facets of a colour
        /// that meet nowhere, so each is its own group and neither can take the other with it. The solid
        /// tip is a third colour and seats on all four facets, which is what keeps it from being any one
        /// facet's orphan.
        /// </summary>
        private static BallType SparkColour(int x, int z, int i)
        {
            int part = SparkPart(x, z, i);

            if (part == 3) return BallType.Type9;    //orange, the threads
            if (part == 2) return BallType.Type7;    //yellow, the star

            if (i <= SPARK_SOLID_TO) return BallType.Type11;   //silver, the solid tip

            bool diagonal = (x - SPARK_AXIS > 0f) == (z - SPARK_AXIS > 0f);

            return diagonal ? BallType.Type6 : BallType.Type5; //magenta / cyan facets
        }

        /// <summary>
        /// A plain crate with <b>a whole cave hanging inside it</b>: shoot the walls away and what is in
        /// there is a roof of stalactites, every one of them a point-down stack of courses nesting into the
        /// pockets of the course above - the lattice's own close packing standing upside down.
        /// <para>
        /// The box is <see cref="Chest"/>'s precedent taken to a room: one-cell walls, which a closed
        /// rectangle may have because its own corners brace it, over twelve courses with an open bottom.
        /// What it carries is the thing the Chest does not have - a CAP filling the interior at the top
        /// course, bonded to the glass cell by cell, which is what the spires hang from.
        /// </para>
        /// <para>
        /// <b>Every spire is built the packing's way</b>: a 3x3 course, a 2x2 nested into its pockets, then
        /// a single cell, then a tail. Nine of them - one at the centre reaching lowest, four in the
        /// corners and four on the edges - and the gaps between their footings are genuinely empty columns,
        /// which is the judged spec's second check and what stops the roof reading as one lump.
        /// </para>
        /// <para>
        /// Gate watch: the unshot sag test was the first thing run, the twelve-level one-cell walls being
        /// the batch's nearest thing to the Ziggurat's eight-second death - they hold, the corners doing
        /// what the Chest's do. The cap's 2x2 check keeps same-colour blocks touching only at their
        /// diagonals, so a spire and the block it hangs under are one modest group and nothing else.
        /// Disjoint palettes: {red, yellow, green} outside against {cyan, magenta} inside.
        /// </para>
        /// </summary>
        private static Design Grotto() => new()
        {
            File = "Grotto.json",
            Name = "Grotto",
            Grid = GROTTO_GRID,
            Depth = GROTTO_DEPTH,
            FieldLevels = REVEAL_FIELD_LEVELS,
            Scene = SceneKind.Cavern,
            Sky = 13,
            Music = MUSIC_REVEAL,
                Balls = BALLS_REVEAL,
            Shots = 52,
            CeilingStep = 10,
            OccupiedBlock = (x, z, i, depth) => GrottoPart(x, z, i, depth) != 0,
            BlockColour = (x, z, i) => GrottoColour(x, z, i, GROTTO_DEPTH),
        };

        //THE GROTTO'S OWN FIGURES. Twelve courses in the eighteen-level field: offset 6, even.
        private const byte GROTTO_GRID = 15;
        private const byte GROTTO_DEPTH = 12;

        //The box: the perimeter of this inclusive index range, one cell thick, with corners.
        private const int GROTTO_LO = 2;
        private const int GROTTO_HI = 12;

        //The width of a wall stripe, in cells, around the perimeter. Three cells against a palette of three
        //makes every stripe a tall sheet reaching the glass and no two neighbouring stripes alike.
        private const int GROTTO_STRIPE = 3;

        /// <summary>
        /// What a cell is: 0 nothing, 1 the crate's wall, 2 the cap, 3 a stalactite. The cap and the spires
        /// are tested before the wall because they live in the interior the wall encloses.
        /// </summary>
        private static int GrottoPart(int x, int z, int i, int depth)
        {
            bool inside = x > GROTTO_LO && x < GROTTO_HI && z > GROTTO_LO && z < GROTTO_HI;

            if (inside)
            {
                if (i == depth - 1) return 2;

                return GrottoSpire(x, z, i, depth) ? 3 : 0;
            }

            bool onPerimeter = (x == GROTTO_LO || x == GROTTO_HI) && z >= GROTTO_LO && z <= GROTTO_HI
                || (z == GROTTO_LO || z == GROTTO_HI) && x >= GROTTO_LO && x <= GROTTO_HI;

            return onPerimeter ? 1 : 0;
        }

        //THE SPIRES' FOOTINGS, as the (x, z) of each one's near corner: the centre, four corners and four
        //edges. Every pair of neighbouring footings is separated by at least one column that no spire
        //occupies, which is the judged spec's second check - x = 5 and x = 9 stay empty by construction.
        private static readonly int[][] GROTTO_SPIRES =
        {
            new[] { 6, 6, 1 },   //the centre one, which reaches lowest (the 1 is its tail's length in courses)
            new[] { 3, 3, 0 }, new[] { 3, 10, 0 }, new[] { 10, 3, 0 }, new[] { 10, 10, 0 },
            new[] { 3, 6, 1 }, new[] { 10, 7, 1 }, new[] { 6, 3, 1 }, new[] { 7, 10, 1 },
        };

        /// <summary>
        /// Whether a cell is on a stalactite. Each is drawn from the cap down as a point-down stack: a 3x3
        /// course, a 2x2 <b>nested into its pockets</b>, a single cell, and for the longer ones a tail of
        /// that single column - which is the opener's cannonball packing read upside down and the reason
        /// the courses shrink by exactly one cell a side.
        /// </summary>
        private static bool GrottoSpire(int x, int z, int i, int depth)
        {
            int below = depth - 1 - i;   //courses below the cap, 1 being the first course under it

            foreach (int[] spire in GROTTO_SPIRES)
            {
                int ox = spire[0];
                int oz = spire[1];
                int tail = spire[2];

                bool centre = ox == 6 && oz == 6;

                //The wide course under the cap: 3x3 for the centre spire, 2x2 for the rest, so the middle
                //of the roof reads as the heavy one.
                int wide = centre ? 3 : 2;

                if (below == 1 && x >= ox && x < ox + wide && z >= oz && z < oz + wide) return true;

                //The nested course: one cell narrower a side, seated in the pockets of the course above.
                if (below == 2)
                {
                    int narrow = wide - 1;
                    if (x >= ox && x < ox + narrow && z >= oz && z < oz + narrow) return true;
                }

                //The single cell the stack comes to, and the tail hanging under it.
                if (tail > 0 && below >= 3 && below <= 3 + tail && x == ox && z == oz) return true;
                if (tail == 0 && below == 3 && x == ox && z == oz) return true;
            }

            return false;
        }

        /// <summary>
        /// The crate in three tall stripes and the cave inside it in two. A wall stripe is a sheet running
        /// from the floor to the glass, so every stripe reaches the anchor course on its own; the cap is a
        /// 2x2 check, and <b>a spire wears the colour of the block it hangs under</b>, which is what makes
        /// each spire and its block one modest group instead of the whole roof being two.
        /// </summary>
        private static BallType GrottoColour(int x, int z, int i, int depth)
        {
            int part = GrottoPart(x, z, i, depth);

            if (part == 1)
            {
                //Distance round the perimeter, so a stripe is a band of the wall rather than a band of the
                //index - a stripe crossing a corner stays one stripe.
                int around = GrottoPerimeter(x, z);

                return Band(around / GROTTO_STRIPE,
                    new[] { BallType.Type1, BallType.Type7, BallType.Type2 });   //red, yellow, green
            }

            //The cap's check, and the spires read it at their own footing so a spire matches its block
            int bx = (part == 3 ? GrottoSpireOrigin(x, z, 0) : x) / 2;
            int bz = (part == 3 ? GrottoSpireOrigin(x, z, 1) : z) / 2;

            return ((bx + bz) & 1) == 0 ? BallType.Type5 : BallType.Type6;   //cyan / magenta
        }

        /// <summary>
        /// How far round the crate's perimeter a wall cell sits, counted clockwise from one corner. The
        /// stripes are cut on this rather than on x or z, so a stripe that turns a corner is still one
        /// stripe and no two stripes meet in the same colour.
        /// </summary>
        private static int GrottoPerimeter(int x, int z)
        {
            int side = GROTTO_HI - GROTTO_LO;

            if (z == GROTTO_LO) return x - GROTTO_LO;
            if (x == GROTTO_HI) return side + (z - GROTTO_LO);
            if (z == GROTTO_HI) return 2 * side + (GROTTO_HI - x);

            return 3 * side + (GROTTO_HI - z);
        }

        /// <summary>
        /// Which spire a cell belongs to, as that spire's own footing - so every cell of a stalactite reads
        /// the same cap block and the whole spire comes out one colour.
        /// </summary>
        private static int GrottoSpireOrigin(int x, int z, int axis)
        {
            foreach (int[] spire in GROTTO_SPIRES)
            {
                if (x >= spire[0] - 1 && x <= spire[0] + 2 && z >= spire[1] - 1 && z <= spire[1] + 2)
                    return axis == 0 ? spire[0] : spire[1];
            }

            return axis == 0 ? x : z;
        }

        /// <summary>
        /// A faceted crystal tube with <b>a balance scale standing inside it, frozen mid-tip</b> - one pan
        /// hanging two courses lower than the other, which is the whole story of the level told in a
        /// silhouette. Cut the heavy pan loose and the beam springs.
        /// <para>
        /// The outer is a taxicab tube - four flat faces, open at both ends - so the reveal is not a lid
        /// coming off but a wall being opened: the player can see something is in there from the first
        /// shot and cannot see what until the facets go.
        /// </para>
        /// <para>
        /// <b>The scale is three groups and the drop order is the design.</b> The beam and its post are one
        /// colour, each arm another: cutting an arm orphans nothing, because the beam still stands on the
        /// post - but cutting the BEAM takes both arms with it, which is the finale and the only cascade in
        /// the level. The two arms differ in length, so the left pan hangs low and the right high.
        /// </para>
        /// <para>
        /// Gate watch: the judged spec's first check is the diamond's axis pinch - a taxicab tube is
        /// narrowest exactly where the weights sit, so the weights are 2x1 along X rather than 2x2 and were
        /// verified clear of the wall after rounding. Disjoint palettes: {white, green} outside against
        /// {yellow, cyan, magenta} inside.
        /// </para>
        /// </summary>
        private static Design Scales() => new()
        {
            File = "Scales.json",
            Name = "Scales",
            Grid = SCALES_GRID,
            Depth = SCALES_DEPTH,
            FieldLevels = REVEAL_FIELD_LEVELS,
            Scene = SceneKind.Cavern,
            Sky = 13,
            Music = MUSIC_REVEAL,
                Balls = BALLS_REVEAL,
            Shots = 44,
            CeilingStep = 8,
            OccupiedBlock = (x, z, i, depth) => ScalesPart(x, z, i) != 0,
            BlockColour = ScalesColour,
        };

        //THE SCALE'S OWN FIGURES. Twelve courses in the eighteen-level field: offset 6, even.
        private const byte SCALES_GRID = 15;
        private const byte SCALES_DEPTH = 12;

        //The tube's taxicab band, and the axis it is cut about. The inner face at 4 is what the weights are
        //kept clear of - see the design's own gate note about the pinch on the axes.
        private const float SCALES_INNER = 4f;
        private const float SCALES_OUTER = 6f;
        private const int SCALES_AXIS = 7;

        //THE SCALE ITSELF, course by course: the post hangs from the glass, the beam crosses under it, and
        //the two arms hang from the beam's ends at different lengths - which is what makes it a scale
        //caught mid-tip rather than a level one.
        private const int SCALES_POST_LOW = 10;
        private const int SCALES_BEAM_LEVEL = 9;
        private const int SCALES_BEAM_HALF = 2;
        private const int SCALES_LEFT_ARM = 5;
        private const int SCALES_RIGHT_ARM = 9;

        /// <summary>
        /// What a cell is: 0 nothing, 1 the tube, 2 the beam and its post, 3 the heavy arm, 4 the light
        /// one. The scale is tested first, living inside the tube's own hollow.
        /// </summary>
        private static int ScalesPart(int x, int z, int i)
        {
            if (z == SCALES_AXIS)
            {
                //The post: a single column from the glass down to the beam.
                if (x == SCALES_AXIS && i >= SCALES_POST_LOW) return 2;

                //The beam: one course, crossing under the post.
                if (i == SCALES_BEAM_LEVEL
                    && x >= SCALES_AXIS - SCALES_BEAM_HALF && x <= SCALES_AXIS + SCALES_BEAM_HALF) return 2;

                //The heavy arm - a longer thread and a pan two courses lower than its opposite. THE PANS
                //HANG INWARD from their threads and not outward, which is measured rather than drawn: a
                //taxicab tube pinches exactly on the axes, where this scale lies, and a pan reaching out to
                //m = 3 gains a wall neighbour through the (-1, -1) parity diagonal - m jumps by two across
                //one diagonal step, so a cell three out touches a wall five out. The arms were then hanging
                //off the CRYSTAL rather than off the beam, and cutting the beam orphaned nothing: the level
                //said "cut a pan loose and the beam springs" and did not do it. Tucked inward nothing of
                //the scale passes m = 2, whose diagonal reach is m = 4, a clear cell inside the wall.
                if (x == SCALES_LEFT_ARM && i >= 6 && i <= 8) return 3;
                if ((x == SCALES_LEFT_ARM || x == SCALES_LEFT_ARM + 1) && i >= 4 && i <= 5) return 3;

                //The light arm.
                if (x == SCALES_RIGHT_ARM && i >= 7 && i <= 8) return 4;
                if ((x == SCALES_RIGHT_ARM - 1 || x == SCALES_RIGHT_ARM) && i >= 5 && i <= 6) return 4;
            }

            float dx = x - SCALES_AXIS;
            float dz = z - SCALES_AXIS;
            float m = MathF.Abs(dx) + MathF.Abs(dz);

            return m > SCALES_INNER && m <= SCALES_OUTER ? 1 : 0;
        }

        /// <summary>
        /// The tube in two facet colours and the scale in three of its own. The facets are cut by the SIGNS
        /// of dx and dz with opposite facets sharing a colour, <see cref="Spark"/>'s rule and for its
        /// reason: two sheets of a colour that touch nowhere are two groups, and neither is load-bearing
        /// for the other.
        /// </summary>
        private static BallType ScalesColour(int x, int z, int i)
        {
            switch (ScalesPart(x, z, i))
            {
                case 2: return BallType.Type7;   //yellow, the beam and post - the finale
                case 3: return BallType.Type5;   //cyan, the heavy arm
                case 4: return BallType.Type6;   //magenta, the light arm
            }

            bool diagonal = (x - SCALES_AXIS > 0) == (z - SCALES_AXIS > 0);

            return diagonal ? BallType.Type4 : BallType.Type2;   //white / green facets
        }

        /// <summary>
        /// A ship in a bottle: a plain glass tank with <b>a little red-sailed ship riding at anchor</b>
        /// inside it. It is the block's most literal reveal and its longest, the tank being fourteen
        /// courses deep, and the payoff is a silhouette every player already knows.
        /// <para>
        /// The tank is an oblong rather than a square - eleven cells by seven - which is what makes the
        /// ship read broadside on from where the gun starts: a square tank would show it end-on from half
        /// the orbit. Its walls are one cell thick with corners, the Chest's own bracing, and it is open at
        /// the top and bottom so the reveal is the walls coming away rather than a lid.
        /// </para>
        /// <para>
        /// <b>The ship is drawn the way a child draws one</b>: a keel, a deck tapered at bow and stern, a
        /// mast running the whole height to the glass, two stays either side of it, and a square sail. The
        /// mast and the stays are what hold the whole thing up, so the rigging is its own colour and
        /// releasing it drops the ship entire - the intended splash, measured well inside the gate.
        /// </para>
        /// <para>
        /// Gate watch: the unshot sag test first, this being an open-TOP one-cell tank at fourteen courses
        /// and so the batch's nearest thing to the Ziggurat; it holds. Then the stay feet, which reach the
        /// deck's ends by the parity diagonal - the deck's corners are cut for the taper, so those two
        /// contacts are the whole load path and were verified after rounding. Disjoint palettes: {cyan,
        /// yellow} outside against {brown, red, white} inside.
        /// </para>
        /// </summary>
        private static Design Ship() => new()
        {
            File = "Ship.json",
            Name = "Ship",
            Grid = SHIP_GRID,
            Depth = SHIP_DEPTH,
            FieldLevels = REVEAL_FIELD_LEVELS,
            Scene = SceneKind.Cavern,
            Sky = 13,
            Music = MUSIC_REVEAL,
                Balls = BALLS_REVEAL,
            Shots = 54,
            CeilingStep = 10,
            OccupiedBlock = (x, z, i, depth) => ShipPart(x, z, i, depth) != 0,
            BlockColour = (x, z, i) => ShipColour(x, z, i, SHIP_DEPTH),
        };

        //THE SHIP'S OWN FIGURES. Fourteen courses in the eighteen-level field: offset 4, even.
        private const byte SHIP_GRID = 15;
        private const byte SHIP_DEPTH = 14;

        //The tank: an oblong perimeter, wide in X and narrow in Z, so the ship inside it is seen broadside
        //from where the gun starts (+Z looking at the origin).
        private const int SHIP_X_LO = 2;
        private const int SHIP_X_HI = 12;
        private const int SHIP_Z_LO = 4;
        private const int SHIP_Z_HI = 10;

        //The hull's own courses and the mast's foot. The keel is one course, the deck the one above it, and
        //everything above that is rigging.
        private const int SHIP_KEEL_LEVEL = 1;
        private const int SHIP_DECK_LEVEL = 2;
        private const int SHIP_MAST_X = 7;
        private const int SHIP_HULL_Z = 7;
        private const int SHIP_STAY_LEFT = 4;
        private const int SHIP_STAY_RIGHT = 10;

        /// <summary>
        /// What a cell is: 0 nothing, 1 the tank, 2 the hull, 3 the sail, 4 the rigging. The ship is tested
        /// first, standing inside the tank's own hollow.
        /// </summary>
        private static int ShipPart(int x, int z, int i, int depth)
        {
            //THE HULL. A keel five cells long and a deck seven wide with its four corners cut, which is
            //what tapers the bow and the stern.
            if (z == SHIP_HULL_Z && i == SHIP_KEEL_LEVEL && x >= 5 && x <= 9) return 2;

            if (i == SHIP_DECK_LEVEL && x >= 4 && x <= 10 && z >= 6 && z <= 8)
            {
                bool corner = (x == 4 || x == 10) && (z == 6 || z == 8);
                if (!corner) return 2;
            }

            //THE RIGGING. The mast runs from the deck to the glass and the two stays flank it, their feet
            //reaching the deck's ends by the parity diagonal - the level's whole load path.
            if (i >= SHIP_DECK_LEVEL + 1 && i <= depth - 1 && z == SHIP_HULL_Z
                && (x == SHIP_MAST_X || x == SHIP_STAY_LEFT || x == SHIP_STAY_RIGHT)) return 4;

            //THE SAIL, hung either side of the mast and touching it at every course.
            if (z == SHIP_HULL_Z && i >= 4 && i <= 8 && (x == 5 || x == 6 || x == 8 || x == 9)) return 3;

            bool onPerimeter =
                (x == SHIP_X_LO || x == SHIP_X_HI) && z >= SHIP_Z_LO && z <= SHIP_Z_HI
                || (z == SHIP_Z_LO || z == SHIP_Z_HI) && x >= SHIP_X_LO && x <= SHIP_X_HI;

            return onPerimeter ? 1 : 0;
        }

        /// <summary>
        /// The tank in two-cell stripes and the ship in three colours of its own. The stripes run round the
        /// perimeter rather than along an axis, so each is a tall sheet reaching the glass; the hull, the
        /// sail and the rigging are one group each, and it is the rigging that carries the ship.
        /// </summary>
        private static BallType ShipColour(int x, int z, int i, int depth)
        {
            switch (ShipPart(x, z, i, depth))
            {
                case 2: return BallType.Type10;  //brown, the hull
                case 3: return BallType.Type1;   //red, the sail - it reads in the cavern where white would not
                case 4: return BallType.Type4;   //white, the rigging
            }

            return Band(ShipPerimeter(x, z) / 2, new[] { BallType.Type5, BallType.Type7 });  //cyan / yellow
        }

        /// <summary>How far round the tank's perimeter a wall cell sits - <see cref="GrottoPerimeter"/>'s
        /// rule on an oblong, so a stripe turning a corner stays one stripe.</summary>
        private static int ShipPerimeter(int x, int z)
        {
            int wide = SHIP_X_HI - SHIP_X_LO;
            int deep = SHIP_Z_HI - SHIP_Z_LO;

            if (z == SHIP_Z_LO) return x - SHIP_X_LO;
            if (x == SHIP_X_HI) return wide + (z - SHIP_Z_LO);
            if (z == SHIP_Z_HI) return wide + deep + (SHIP_X_HI - x);

            return 2 * wide + deep + (SHIP_Z_HI - z);
        }

        /// <summary>
        /// A barrel with <b>a red weight hanging inside it on a double coil spring</b> - the block's finale
        /// and the one payoff in the game that moves on its own: the weight is heavy, the two coils are
        /// slender, and it bobs from the first launch and lurches whenever a shot lands anywhere on the
        /// barrel around it.
        /// <para>
        /// The barrel is a shell that bulges in the middle - <see cref="SPRING_RIM"/> at both rims and
        /// <see cref="SPRING_BULGE"/> more at its waist - open at the top and bottom, so the coils are
        /// glimpsed through the ends before the wall is opened.
        /// </para>
        /// <para>
        /// <b>The two coils are counter-wound and cross once.</b> They turn <see cref="SPRING_TURN"/> a
        /// course in opposite directions, which puts a lateral step of 0.63 against the lattice's 0.71
        /// parity reach - designed margin, and thin, so the judged spec's first check was the connectivity
        /// walk on both strands after rounding rather than the arithmetic. They anchor at the glass and
        /// carry the weight between them.
        /// </para>
        /// <para>
        /// Gate watch, all four run: both strands connect course to course; the strands reach the weight's
        /// top course; the anchors stay clear of the rim's inner face; and the single-strand state - one
        /// coil cut, the weight swinging on the other - stays inside the gate. Disjoint palettes: {green,
        /// white, magenta} outside against {orange, cyan, red} inside.
        /// </para>
        /// </summary>
        private static Design Spring() => new()
        {
            File = "Spring.json",
            Name = "Spring",
            Grid = SPRING_GRID,
            Depth = SPRING_DEPTH,
            FieldLevels = REVEAL_FIELD_LEVELS,
            Scene = SceneKind.Cavern,
            Sky = 13,
            Music = MUSIC_REVEAL,
                Balls = BALLS_REVEAL,
            Shots = 48,
            CeilingStep = 9,
            Occupied = (r, ang, i, depth) => SpringPart(r, ang, i, depth) != 0,
            Colour = (r, ang, i, depth) => SpringColour(r, ang, i, depth),
        };

        //THE SPRING'S OWN FIGURES. Fourteen courses in the eighteen-level field: offset 4, even.
        private const byte SPRING_GRID = 15;
        private const byte SPRING_DEPTH = 14;

        //The barrel: RIM at both ends, bulging BULGE more at the waist, the shell SHELL cells thick.
        private const float SPRING_RIM = 5.4f;
        private const float SPRING_BULGE = 1f;
        private const float SPRING_SHELL = 2f;

        //The coils: their orbit, their half-thickness and how much of a turn each makes a course. 18
        //degrees at a radius of 2 is a lateral step of 0.63 against the lattice's 0.71 parity reach - see
        //the design's own gate note, which is why the connectivity walk is the first thing run on it.
        private const float SPRING_ORBIT = 2f;
        private const float SPRING_COIL = 1.1f;
        private const float SPRING_TURN = 0.05f;
        private const int SPRING_COIL_FROM = 4;

        //The weight: a solid block hanging under the coils, wide enough to read as heavy.
        private const float SPRING_WEIGHT = 2.2f;
        private const int SPRING_WEIGHT_LOW = 2;
        private const int SPRING_WEIGHT_HIGH = 4;

        /// <summary>
        /// The barrel's outer radius at a course: <see cref="SPRING_RIM"/> plus a half sine of
        /// <see cref="SPRING_BULGE"/>, so both rims are the narrow ends and the waist is the widest part.
        /// </summary>
        private static float SpringRadius(int i, int depth) =>
            SPRING_RIM + SPRING_BULGE * MathF.Sin(MathF.PI * i / (depth - 1f));

        /// <summary>
        /// What a cell is: 0 nothing, 1 the barrel, 2 the weight, 3 the first coil, 4 the second. The
        /// inside is tested first, as everywhere in this block.
        /// </summary>
        private static int SpringPart(float r, float ang, int i, int depth)
        {
            if (i >= SPRING_WEIGHT_LOW && i <= SPRING_WEIGHT_HIGH && r <= SPRING_WEIGHT) return 2;

            if (i >= SPRING_COIL_FROM)
            {
                float turn = (i - SPRING_COIL_FROM) * SPRING_TURN * MathF.Tau;

                if (LateralDistanceSquared(r, ang, SPRING_ORBIT, MathF.PI * 0.5f + turn)
                    <= SPRING_COIL * SPRING_COIL) return 3;

                if (LateralDistanceSquared(r, ang, SPRING_ORBIT, MathF.PI * 1.5f - turn)
                    <= SPRING_COIL * SPRING_COIL) return 4;
            }

            float outer = SpringRadius(i, depth);

            return r <= outer && r > outer - SPRING_SHELL ? 1 : 0;
        }

        /// <summary>
        /// The barrel in three sectors and the spring in three colours of its own. The sectors run the
        /// whole height of the shell, so every one reaches the glass and no colour is a horizontal band -
        /// the block's rule since <see cref="Bullseye"/>. The two coils differ in colour, which is what
        /// makes cutting one of them a move rather than an accident.
        /// </summary>
        private static BallType SpringColour(float r, float ang, int i, int depth)
        {
            switch (SpringPart(r, ang, i, depth))
            {
                case 2: return BallType.Type1;   //red, the weight - hot against the cavern's dark
                case 3: return BallType.Type9;   //orange, the first coil
                case 4: return BallType.Type5;   //cyan, the second
            }

            return Band(SectorIndex(ang, 0f, SPRING_SECTORS),
                new[] { BallType.Type2, BallType.Type4, BallType.Type6 });   //green, white, magenta
        }

        //Six sectors folded onto three colours: opposite sectors share one, so each colour is two sheets
        //that meet nowhere - Diabolo's arithmetic, and for its reason.
        private const int SPRING_SECTORS = 6;

        #endregion

        /// <summary>
        /// The 2×2 block a cell belongs to, recovered from the polar pair a <see cref="Design.Colour"/> delegate
        /// is handed — the sibling of <see cref="Chebyshev"/>, and what lets ONE colour rule change its own KIND
        /// at a radius: staves or shells outside, lattice-aligned blocks inside.
        /// <para>
        /// It is needed because the emitter hands exactly one colour delegate the frame it asked for and the three
        /// frames are exclusive, so a design that wants blocks <i>and</i> a radius has to derive one from the
        /// other. The polar pair is the lossless direction — <c>r·cos(ang)</c> and <c>r·sin(ang)</c> are the very
        /// <c>dx</c> and <c>dz</c> the emitter built them from — where a radius cannot be got out of
        /// <see cref="Design.BlockColour"/>'s raw indices without a second copy of the centring arithmetic.
        /// </para>
        /// <para>
        /// <b>The quarter-cell bias is load-bearing.</b> A block boundary lands where the halved coordinate is a
        /// whole number, and the lattice puts <c>dx</c> on a whole cell on the shifted (odd) levels and on a half
        /// cell on the unshifted ones — so an unbiased <c>floor(dx/2)</c> has a boundary exactly ON the cells of
        /// every odd level, where the float noise in <c>r·cos(ang)</c> decides which block a ball is in. Biased by
        /// a quarter, no boundary can coincide with either parity and the nearest one is an eighth of a block away.
        /// </para>
        /// </summary>
        private static void PolarBlock(float r, float ang, out int blockX, out int blockZ)
        {
            blockX = (int)MathF.Floor((r * MathF.Cos(ang) - BLOCK_BIAS) * HALF);
            blockZ = (int)MathF.Floor((r * MathF.Sin(ang) - BLOCK_BIAS) * HALF);
        }

        //A quarter of a cell: the one offset no cell of either level parity can land on. See PolarBlock.
        private const float BLOCK_BIAS = 0.25f;

        /// <summary>
        /// <see cref="Mosaic"/>'s diagonal dice — <see cref="Band"/> over the sum of <see cref="PolarBlock"/>'s
        /// block indices and the block level — for a design whose colour rule is a function of the radius.
        /// <para>
        /// It exists because <see cref="Scatter"/> <b>percolates below five colours</b>, which is the one thing
        /// about the pack's chunky colourings that is invisible until it is measured. A hashed dice puts a block
        /// in a palette entry with probability 1/n and this lattice gives a cell up to twelve neighbours, so at
        /// n = 3 or 4 the same-colour blocks join into a giant component: measured, a three-colour scatter over a
        /// hollow core came out as ONE fused group of 105 — 18 % of the level, on the region that was supposed to
        /// be the fine-grained half of it. <see cref="Prism"/> (5) and <see cref="Static"/> (6) are above the
        /// threshold and are the two designs <see cref="Scatter"/> is right for. Banded on the block coordinates
        /// instead, no two touching blocks can agree at all, so a group is one block and the grain is the grain
        /// that was authored.
        /// </para>
        /// <para>
        /// Normalised before <see cref="Band"/> sees it: these block indices are CENTRED and go negative, and
        /// <c>Band</c> indexes with a bare <c>%</c> — Mosaic hands it raw lattice indices, which cannot.
        /// </para>
        /// </summary>
        private static BallType BandPolar(float r, float ang, int blockLevel, BallType[] palette)
        {
            PolarBlock(r, ang, out int blockX, out int blockZ);
            return Band(((blockX + blockZ + blockLevel) % palette.Length + palette.Length) % palette.Length, palette);
        }

        //The onion's own geometry. See Onion() for why the two distances differ.
        private const float ONION_RADIUS = 5.5f;

        //Where the two colour boundaries sit, as a share of the level's own radius, and how far the outer one
        //swings with the angle around the axis. See OnionShell for why the swing exists and what it has to
        //cross at both ends to work.
        private const float ONION_HEART = 0.28f;
        private const float ONION_BULK = 0.60f;
        private const float ONION_STAVES = 6f;
        private const float ONION_SWING = 0.50f;

        //BOTH boundaries swing, and the inner one has to go negative at the trough or the white simply wraps
        //around the heart and is one piece again: an angular gap a couple of cells wide out at the rim is
        //no gap at all by the time it has narrowed to the axis, so a rib that stops at the heart's edge
        //never actually separates anything. Measured: swinging the outer boundary alone cut the skin from
        //604 to 158 and left the white one group of 408 (42 % of the cluster).
        private const float ONION_SWING_HEART = 0.42f;

        /// <summary>
        /// Green heart, white bulk, yellow skin - ringed by <b>each level's own radius</b> rather than by
        /// true 3D distance from the sphere's centre. The two agree at the equator, where a level's own
        /// radius already is the sphere's, and it is everywhere else that the difference matters: a true
        /// 3D shell narrows to nothing at the poles, so whichever ring the pole's own tiny point happens to
        /// fall in becomes the ENTIRE top layer - the one bonded to the glass - and a single-colour anchor
        /// is the trap every design in this pack has to answer (see <see cref="Validate"/>). Ringed by its
        /// own radius instead, every level, however small, shows the same green-centre/white-ring/yellow-rim
        /// proportions the equator does, which is also just what a real onion's rings look like from any
        /// height - narrower near the root and stem, never absent. It is what keeps the heart's own colour
        /// standing in one piece straight up the middle from pole to pole, directly bonded to the glass at
        /// the top without ever having to pass through the white around it - and the same for the skin at
        /// its own rim - so peeling any one layer off never stands the other two on nothing.
        /// <para>
        /// <b>The boundary between the outer two SWINGS with the angle around the axis, and that is what makes
        /// this a level rather than one shot.</b> Ringed by radius alone each layer is a single connected
        /// piece from pole to pole - the skin measured 604 balls in one group, 62 % of the cluster, so the
        /// first lucky yellow ball ended it. <b>Played, three runs each, 24 shots a run with the aim spread
        /// between shots: ringed by radius alone the level was cleared every run, in 2, 4 and 8 shots of 48;
        /// with the swing, two of the three runs were still going at 24.</b> (A first attempt at that
        /// measurement spread the aim so wide that every shot missed the field outright - 24 shots, zero
        /// contacts of any kind - which any level survives. A scripted play-through is only a datum once the
        /// shots are confirmed to be landing.)
        /// <see cref="ONION_SWING"/> carries the boundary past the surface at <see cref="ONION_STAVES"/>
        /// angles and back inside the heart's own radius between them, so the two outer layers interlock as
        /// <i>staves</i>: white reaches daylight where the boundary swings out, cutting the skin there, and
        /// yellow reaches the core where it swings in, cutting the white. Each stave still runs the full
        /// height, so each is bonded to the glass on its own and the anchor rule above is untouched - and a
        /// bulb of interlocking segments is what an onion actually looks like cut across.
        /// </para>
        /// </summary>
        private static BallType OnionShell(float r, float ang, int i, int depth)
        {
            float dy = OnionVertical(i, depth);
            float capRadius = MathF.Sqrt(MathF.Max(0f, ONION_RADIUS * ONION_RADIUS - dy * dy));
            float shell = capRadius > 0f ? r / capRadius * ONION_RADIUS : 0f;

            //One swing drives both boundaries. Past 1 the white breaks the surface and cuts the skin; below 0
            //BOTH boundaries vanish and the yellow runs from rim to axis, which is the only thing that cuts
            //the white - and the heart with it, into wedges that each still stand the full height.
            float swing = MathF.Cos(ONION_STAVES * ang);
            float heart = ONION_HEART + ONION_SWING_HEART * swing;
            float bulk = ONION_BULK + ONION_SWING * swing;

            if (shell <= ONION_RADIUS * heart) return BallType.Type2; //green heart
            if (shell <= ONION_RADIUS * bulk) return BallType.Type4;  //white bulk
            return BallType.Type7;                                    //yellow skin
        }

        #region The reveals' own geometry (#161)

        /// <summary>
        /// The Reveal block's field depth, and the one number in it that is not about how a level LOOKS: <b>the
        /// ceiling's descent has to have somewhere to go, and the room it descends into is the EMPTY levels under
        /// the layout — not the field's size.</b>
        /// <para>
        /// A hollow or nested vessel wants ten levels of height to be one. Ten deep in the pack's ordinary
        /// sixteen-level field leaves an offset of six, and six empty levels is 4.74 world units of air under the
        /// lowest ball against a descent of 0.6 a step — eight steps, which is less pressure than
        /// <see cref="Crown"/> gets and would have left the budget as the only thing on these levels. Eighteen puts
        /// the offset back to eight and the air to 6.16, i.e. 10.3 steps.
        /// </para>
        /// <para>
        /// Eighteen and not twenty, and both facts that pin it are the game's own. <c>FRAMED_LEVELS</c> is 18, so
        /// an 18-level field is the deepest one still framed WHOLE — a twenty would start with the top two levels
        /// of the layout out of shot, which is no way to run a block whose point is a shape being uncovered. And a
        /// field this deep is RAISED off the death line rather than pinned at <c>FIELD_TOP_Y</c> (17 is the first
        /// depth raised), so the cluster hangs 1.36 higher — exactly as <c>Colossus.json</c>'s eighteen levels
        /// already do. A proven configuration, not a new one.
        /// </para>
        /// </summary>
        private const byte REVEAL_FIELD_LEVELS = 18;

        //The lantern's drum. The bore is hidden by plugs of LANTERN_CAP levels at each end - two, not one: see
        //the design's own remarks for what one level cost the draw.
        private const float LANTERN_OUTER = 4.3f;
        private const float LANTERN_BORE = 2.3f;
        private const int LANTERN_CAP = 2;
        private const int LANTERN_PANES = 6;
        private const int LANTERN_COURSE = 2;

        /// <summary>
        /// A boulder's radius at one height: an ellipsoid of semi-axes <paramref name="radius"/> and
        /// <paramref name="tall"/>, measured from its own middle. <paramref name="tall"/> larger than the layout
        /// is deep is the point rather than a mistake — it cuts the poles off and leaves a flat crown, which is
        /// what gives the anchor layer a disc of cells to carry several colours in.
        /// </summary>
        private static float BoulderRim(float dyc, float radius, float tall)
        {
            float taper = 1f - (dyc / tall) * (dyc / tall);
            return taper <= 0f ? 0f : radius * MathF.Sqrt(taper);
        }

        //The crate. CHEST_HALF at 3.6 is the one half-extent that comes out 8 cells on the unshifted levels and 7
        //on the shifted ones - the evenest brick bond the stagger allows, and the narrowest box whose walls can be
        //two cells thick and still leave a cavity worth hiding something in. The grid and the depth are constants
        //beside it because ChestPart needs all three, and two copies of a number are two places for it to be
        //wrong (LEAN_GRID is stated for the same reason).
        private const byte CHEST_GRID = 15;
        private const byte CHEST_DEPTH = 12;
        private const float CHEST_HALF = 3.6f;
        private const float CHEST_WALL = 2f;
        private const int CHEST_LID = 2;
        private const int CHEST_FLOOR = 2;

        //The pearl and the cord it hangs from. PEARL_DROP is measured from the glass, so the pearl sits low in
        //the box with air above it and the cord visible through the hole the player opens.
        private const float PEARL_RADIUS = 1.8f;
        private const float PEARL_DROP = 4.2f;
        private const float CORD_HALF = 1.1f;

        /// <summary>
        /// What a cell of the chest is: <c>0</c> nothing, <c>1</c> the box, <c>2</c> the pearl or its cord. One
        /// function for the shape and the colouring both, as <see cref="HelixStrand"/> is for its strands: a
        /// hollow body and its contents cannot be cut in one frame and coloured in another.
        /// </summary>
        private static int ChestPart(int x, int z, int i)
        {
            Centred(x, z, i, CHEST_GRID, out float dx, out float dz);

            float r = MathF.Sqrt(dx * dx + dz * dz);
            float d = BelowGlass(i, CHEST_DEPTH);
            float dy = d - PEARL_DROP;

            //The pearl and its cord are tested FIRST, so the lid is pierced by the cord rather than the cord
            //being clipped by the lid - which is the difference between a pearl bonded to the glass in its own
            //colour and a pearl that falls with the box
            if (r * r + dy * dy <= PEARL_RADIUS * PEARL_RADIUS) return 2;
            if (r <= CORD_HALF && d <= PEARL_DROP) return 2;

            //The square extent Chebyshev recovers from the polar pair, taken straight off the offsets
            float box = MathF.Max(MathF.Abs(dx), MathF.Abs(dz));
            if (box > CHEST_HALF) return 0;

            bool insideWalls = box <= CHEST_HALF - CHEST_WALL;
            bool underLid = i >= CHEST_FLOOR && i < CHEST_DEPTH - CHEST_LID;

            return insideWalls && underLid ? 0 : 1;
        }

        //The fossil's stone. FOSSIL_TALL is larger than the layout is deep so the poles are cut off and the crown
        //is a disc rather than a point (see BoulderRim). FOSSIL_SLAB is the picture's thickness in Z (two cells on
        //the unshifted levels, three on the shifted ones - the thinnest plate that holds at either parity, see the
        //pictures region). FOSSIL_BURY is how far inside the rim the frond is held and it is the number that seals
        //the payoff: raise it if a leaflet is ever reported showing through the flank, lower it and the frond
        //fills out and starts to leak.
        private const byte FOSSIL_GRID = 15;
        private const byte FOSSIL_DEPTH = 12;
        private const float FOSSIL_RADIUS = 4.3f;
        private const float FOSSIL_TALL = 5.6f;
        private const float FOSSIL_SLAB = 1.1f;
        private const float FOSSIL_BURY = 1.5f;

        /// <summary>
        /// A fern frond, 9 columns by 12 rows: a stem in columns 4 and 5 of <b>every</b> row with leaflets
        /// alternating either side of it. Two rules shape it and both are about where it may not go.
        /// <list type="bullet">
        /// <item><b>The stem is in every row</b>, so the frond is one connected group and its top row reaches the
        /// anchor layer — which is what lets it hang when the stone around it has gone. A leaflet may be one row
        /// thick because it hangs off that stem: the lonely-ball rule is about a ball's own colour GROUP, and the
        /// frond is one group.</item>
        /// <item><b>The bottom row is blank.</b> The layout's lowest level faces open air underneath, so a frond
        /// reaching it is exposed there however deeply <see cref="FOSSIL_BURY"/> buries it — measured, 4 balls of
        /// it were on the outside before this row was cleared, and a green ball landing on one of them takes the
        /// whole payoff before it has been seen.</item>
        /// </list>
        /// </summary>
        private static readonly string[] FERN =
        {
            "....##...",
            "....##...",
            "..####...",
            "..####...",
            "....####.",
            "....####.",
            ".#####...",
            ".#####...",
            "....####.",
            "....####.",
            "..####...",
            ".........",
        };

        private static bool FossilRock(int x, int z, int i)
        {
            Centred(x, z, i, FOSSIL_GRID, out float dx, out float dz);

            float rim = BoulderRim(OnionVertical(i, FOSSIL_DEPTH), FOSSIL_RADIUS, FOSSIL_TALL);
            return dx * dx + dz * dz <= rim * rim;
        }

        /// <summary>
        /// Whether a cell is on the frond: inside the slab, at least <see cref="FOSSIL_BURY"/> inside the rock's
        /// own rim, and drawn as <c>#</c> in <see cref="FERN"/>. Row 0 is the TOP of the picture, which is layout
        /// level <c>depth - 1</c>, so the bitmap reads in source the way the fossil reads in the game — the
        /// convention <see cref="OnWall"/> already keeps for the Gallery's walls.
        /// <para>
        /// The burial test is what makes the bitmap safe to redraw: the stone wins at the surface, so a leaflet
        /// drawn too long is trimmed rather than exposed. It also means a redraw changes the frond's ball count by
        /// less than the bitmap suggests, so re-measure rather than counting the hashes.
        /// </para>
        /// </summary>
        private static bool FossilFern(int x, int z, int i)
        {
            Centred(x, z, i, FOSSIL_GRID, out float dx, out float dz);
            if (MathF.Abs(dz) > FOSSIL_SLAB) return false;

            float rim = BoulderRim(OnionVertical(i, FOSSIL_DEPTH), FOSSIL_RADIUS, FOSSIL_TALL) - FOSSIL_BURY;
            if (rim <= 0f || dx * dx + dz * dz > rim * rim) return false;

            int column = x - (FOSSIL_GRID - FERN[0].Length) / 2;

            return PixelAt(FERN, column, FOSSIL_DEPTH - 1 - i) == '#';
        }

        //The fruit's own profile. MANGO_DROP is where the taper would close to nothing and it is well below where
        //the layout ends, which is what leaves the bottom a blunt nose instead of a point. MANGO_BULGE fattens the
        //+X flank, so the fruit is lopsided on the axis that runs ACROSS THE SCREEN (the gun starts at +Z looking
        //at the origin) and the stone's own offset reads as the fruit's fat side rather than as an error.
        //MANGO_SKIN is the peel, thick enough that the flesh is sealed but for eight cells of 165.
        private const float MANGO_RADIUS = 4.6f;
        private const float MANGO_DROP = 8.5f;
        private const float MANGO_BULGE = 0.45f;
        private const float MANGO_SKIN = 1.5f;
        private const int MANGO_STRIPS = 8;

        //The stone: off the axis in X, flattened in Z so its broad face is the one the gun sees, and generous on
        //purpose - MangoInside tests the peel first, so wherever it would break the surface it is simply clipped.
        private const float MANGO_STONE_X = 1.6f;
        private const float MANGO_STONE_Y = 3.4f;
        private const float MANGO_STONE_LONG = 2.1f;
        private const float MANGO_STONE_FLAT = 1.15f;
        private const float MANGO_STONE_TALL = 2.3f;

        private static float MangoRim(float ang, int i, int depth)
        {
            float d = BelowGlass(i, depth);
            float taper = 1f - (d / MANGO_DROP) * (d / MANGO_DROP);

            return taper <= 0f ? 0f : (MANGO_RADIUS + MANGO_BULGE * MathF.Cos(ang)) * MathF.Sqrt(taper);
        }

        /// <summary>
        /// The stone and the stalk it hangs from, as one shape. <see cref="Untwist"/> at zero turns is the
        /// emitter's polar pair read back as plain offsets — the tall levels' helper doing the un-rotated case,
        /// rather than a second copy of <c>r cos θ</c> here.
        /// </summary>
        private static bool MangoStone(float r, float ang, int i, int depth)
        {
            Untwist(r, ang, 0f, out float dx, out float dz);

            float d = BelowGlass(i, depth);
            float ex = (dx - MANGO_STONE_X) / MANGO_STONE_LONG;
            float ez = dz / MANGO_STONE_FLAT;
            float ey = (d - MANGO_STONE_Y) / MANGO_STONE_TALL;

            if (ex * ex + ez * ez + ey * ey <= 1f) return true;

            //The stalk: the same off-centre column, from the glass down into the stone's own top. It is what bonds
            //the stone to the ceiling in its OWN colour, so the flesh and the peel can both go and leave it
            //hanging - the property Onion's heart does not have.
            return d <= MANGO_STONE_Y
                   && MathF.Abs(dx - MANGO_STONE_X) <= MANGO_STONE_FLAT
                   && MathF.Abs(dz) <= MANGO_STONE_FLAT;
        }

        /// <summary>
        /// Peel, stone, flesh — <b>in that order</b>, which is the design's own safety catch: a cell within
        /// <see cref="MANGO_SKIN"/> of the surface is peel whatever the stone's numbers say, so the stone cannot
        /// break the surface and be seen before the fruit is opened. The flesh's strips are half a strip out of
        /// phase with the peel's, so the two sets of seams do not line up and the fruit reads as woven rather than
        /// as one set of cuts through both.
        /// <para>
        /// <b>The palette is chosen against measured CIEDE2000, not by eye (#286).</b> It shipped as a
        /// red-and-MAGENTA peel over a gold-and-GREEN flesh around a WHITE stone, which reads as a beach ball:
        /// magenta is nowhere on a mango, and green is the unripe skin rather than the flesh. Worse, the two
        /// figures at the heart of the design were the palette's own confusions — <c>white/yellow</c> measures
        /// <b>14.5 / 15.5</b> (dome 1 / dome 13) and is the second-tightest pair there is, and it stood between
        /// the STONE and half the FLESH touching it, which is to say between the level's payoff and its
        /// surroundings.
        /// </para>
        /// <para>
        /// What ships now, with every contact the geometry actually makes, measured under the bright dome and
        /// under dome 13 — <b>this level's own</b>, so the second figure is the one that decides:
        /// <list type="bullet">
        /// <item>peel mates <c>red/olive</c> <b>49.0 / 51.3</b> — a crimson blush over a dark green shoulder,
        /// and the widest pair anywhere in the design</item>
        /// <item>flesh mates <c>yellow/orange</c> <b>30.2 / 28.2</b></item>
        /// <item>flesh against the stone: <c>yellow/brown</c> <b>48.9 / 45.2</b>, <c>orange/brown</c>
        /// <b>28.6 / 27.2</b> — against the 14.5 / 15.5 the white stone had</item>
        /// <item>across the skin: <c>olive/yellow</c> <b>34.7 / 28.0</b>, <c>olive/orange</c> <b>39.3 / 39.7</b>,
        /// <c>red/yellow</c> <b>44.8 / 44.4</b></item>
        /// </list>
        /// </para>
        /// <para>
        /// <b>One contact is knowingly tight and it is the price of the fruit being a mango: <c>red/orange</c>
        /// at 14.1 / 15.9, the tightest pair in the whole palette</b>, where the peel's red strips meet the
        /// flesh's orange ones. It cannot be designed away — orange flesh is the single most identifying thing
        /// about a mango and a red-blushed skin is the second, and the flesh's strips are half a strip out of
        /// phase, so BOTH flesh colours touch BOTH peel colours whatever the phase. It is paid for by the
        /// geometry rather than by the palette: the fruit starts SEALED, so the two are never both on show until
        /// the player has opened it, and then only along the breach. That is a far better bargain than the
        /// white stone, which was tight against the flesh from the moment it was uncovered and stayed that way.
        /// Five colours either way, so the block's hardest draw is unchanged.
        /// </para>
        /// </summary>
        private static BallType MangoInside(float r, float ang, int i, int depth)
        {
            float rim = MangoRim(ang, i, depth);

            //Peel: a crimson blush over a dark green shoulder, which is what a ripe mango's skin actually is.
            if (rim - r <= MANGO_SKIN)
                return Sector(ang, 0f, MANGO_STRIPS, new[] { BallType.Type1, BallType.Type13 });

            //Stone: the woody pit.
            if (MangoStone(r, ang, i, depth)) return BallType.Type10;

            //Flesh: gold shading into orange — the one colour that says "mango" before the shape does.
            return Sector(ang, HALF / MANGO_STRIPS, MANGO_STRIPS, new[] { BallType.Type7, BallType.Type9 });
        }

        #endregion
    }
}
