using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>The Spectrum</b>, block 9 of the campaign: its designs, and the helpers no other block's designs use, in
    /// the order <c>Program.cs</c> held them — the play order is <see cref="Main"/>'s, and the block's name, music
    /// and ball style are in the tables there. Split out of <c>Program.cs</c> in #386.
    /// </summary>
    internal static partial class Program
    {


        #region The spectrum levels (#253)

        //THE NINTH BLOCK in play order (#300): one HUE FAMILY a level, swept through the whole body as a gradient. The owner's
        //brief is a chapter whose levels read as ONE colour rather than as a set of arbitrary matching ones -
        //a level sweeping white -> light blue -> blue -> dark blue and back to white as it climbs, and the
        //same principle on a warm ramp, a green one, and so on to the wheel entire.
        //
        //NO NEW COLOUR IS INVOLVED and that is the first thing to know about the block. Every family below is
        //a SUBSET AND AN ORDERING of the thirteen BallType already has - the seed example needs none, White,
        //Cyan, Blue and NavyBlue sitting in exactly that order - and the enum is deliberately not grown for
        //it: the five that joined in #152 were each placed at a measured distance from an existing colour
        //(silver a cool slate so it does not vanish into the white gores, navy re-spaced against black in
        //#246), so a hue picked to fill a gap in a ramp is the one way back into the trap those spacings
        //exist to avoid. What the chapter adds is ORDER over the palette, not paint.
        //
        //THE GRADIENT CANNOT BE FLOORS OF FLAT COLOUR, and that is the second. A same-colour group spanning
        //the top level is the last anchor for everything under it, so one matching ball ends the level -
        //Mosaic and Gem were both drawn as horizontal bands first and both measured 100 % in the drop test.
        //A gradient stacked as clean rings is that trap by construction, which is why every sweep here is
        //TILTED: the boundary between two stops is a slanted plane, a helicoid or a comb, never a floor. See
        //Sweep and the five colour rules in the block's geometry region - the tilt is what makes the chapter
        //possible at all, and only afterwards what makes it look like a gradient rather than a layer cake.
        //
        //Every level is TALL (Design.FieldLevels past GameplayScreen.FRAMED_LEVELS), because a multi-stop
        //ramp needs the height to read as a ramp: four stops out and back is fourteen lattice levels before
        //a single one repeats, and in a 16-level field that is the whole level with no body left over.
        //
        //Each is a different KIND of tall and a different KIND of sweep, which is the Tower's own rule (#160)
        //taken to the colouring as well as to the silhouette:
        //  Icicle    - a plain cone, swept by a bare HELICOID: one turn is the whole family.
        //  Hourglass - a solid of revolution with a WAIST, swept in conical shells wound into a screw.
        //  Trellis   - two counter-wound ribbons, each swept along ITSELF and half a family apart.
        //  Kiln      - a column of lobes, the bare helicoid with a COMB cut into it so the stops interlock.
        //  Turbine   - blades on a turning core, swept helically, so every blade wears its own hue.
        //
        //AND THE ONE RULE ALL FIVE OBEY, which was found by breaking it twice: EVERY STOP OF THE FAMILY HAS
        //TO STAND ON THE TOP LEVEL. A stop that exists only further down hangs off the stop above it and
        //nothing else, so cutting that one takes it and everything under it - the Icicle measured 80 % that
        //way and the Kiln 92 %, over the gate, both on sweeps that were tilted but not wound. A helicoid
        //whose pitch is one folded period a turn puts the whole family on the anchor level in wedges, and
        //what a cut leaves behind is a screw thread that still reaches the glass. The tilt is what makes the
        //gradient 3D; the PITCH is what makes it a level.
        //
        //THE BANDED TIERS (#302), and how they live with the two rules above rather than overriding them.
        //Four of the block's designs - Pinecone, Pleat, Bolt, Totem - lost to gravity after a few shots:
        //a wound sweep makes a colour group a RIBBON of a fifth to a third of the level, so every shot was
        //a catastrophic release, and the sheared remainder stretched past the death line with the glass at
        //rest (the sag probe of #301 measured all four at 5 of 5 losing orders; the owner's ask was
        //"shootable tiers"). The resolution is that A TIER IS NOT A FLOOR: the forbidden thing was always a
        //course of ONE colour - the anchor of everything under it, gone on one ball - and every tier here
        //is at least TWO INTERLEAVED INKS of the level's own family (the Arcade cap rule, arrived at from
        //the other end), so no single ball can take a course and severing one is a designed multi-shot cut
        //whose cascade is earned. Each design wears its tier as its own architecture - quartered hoops on
        //the pinecone, guarded belts across the pleat, striped elbow plates and a quadrant-banded hex head
        //on the bolt, sector collars at the totem's junctions - and each also SEVERS the ribbons crossing
        //it, which is the structural half of the job: releases fell from 133-334 balls to course-sized
        //bites on all four. Where a tier's inks would touch their own colour in the sweep, the contact is
        //designed away (Pleat's guard courses pull the family's ends one stop in beside each belt; Totem's
        //collars are deliberately jewellery on the pole, outside the gradient), and every stop of every
        //family still stands on the top level - the pitch rule holds untouched. Measured after: Pinecone
        //and Pleat and Totem read 0 of 5 losing orders with every order clearing, Bolt 1 of 5 at a
        //hairline -1.00; the four had read 5 of 5, Bolt on its first shot.

        /// <summary>
        /// The block's opener and the owner's own seed: a cone of ice hanging point-down off the glass,
        /// painted <b>white to cyan to blue to navy and back to white</b> - the family the brief names, in
        /// the order the enum already holds them. The plainest body in the block on purpose, the way
        /// <see cref="Column"/> opens the Tower: what the level is here to state is the SWEEP, so nothing
        /// else about it has a second idea in it.
        /// <para>
        /// <b>The sweep is a bare helicoid whose pitch is one whole folded family a turn</b>
        /// (<see cref="ICICLE_PITCH"/>). Two things follow, and the block rests on both. Every colour of the
        /// ramp stands on the top level, in wedges, so the level every ball hangs from is four colours and
        /// not one - and walking the gun once round the cone walks the family from white to navy and home
        /// again, which is the chapter's reading taught in its first minute.
        /// </para>
        /// <para>
        /// Gentle for an opener the way the Meadow's plates are: a stop is a broad ribbon winding down the
        /// cone, so one matching ball takes a whole turn of it. What is <i>not</i> gentle is what a cut
        /// leaves behind, and that is the point of the helicoid - see the block's own rule.
        /// </para>
        /// <para>
        /// Measured: 547 balls, margin 2, nothing standing alone (4 in pairs), nothing recoloured; <b>6
        /// standing groups</b> - a ribbon is one connected spiral, so a colour is one or two of them - and
        /// best single shots of 15-34 %, against the 80 % the tilted-plane draft of this level took. Colour
        /// counts 85/91/183/188, the fold's two ends at about half its middles' count, which is what the
        /// fold does and is left alone. PASS on the Game's own <c>aimcheck</c> (steepest cell 46.7 deg of a
        /// 49.6 limit) and hung unshot for 35 s in the running game.
        /// </para>
        /// </summary>
        private static Design Icicle() => new()
        {
            File = "Icicle.json",
            Name = "Icicle",
            Grid = ICICLE_GRID,
            Depth = ICICLE_DEPTH,
            FieldLevels = ICICLE_FIELD_LEVELS,
            Scene = SPECTRUM_SCENE,
            Sky = SPECTRUM_SKY,
            Music = MUSIC_SPECTRUM,
                Balls = BALLS_SPECTRUM,
            Shots = 40,
            CeilingStep = 6,
            Occupied = (r, ang, i, depth) => r <= IcicleRadius(i, depth),
            Colour = (r, ang, i, depth) => Sweep(IcicleSweep(ang, i, depth), FROST),
        };

        /// <summary>
        /// A column of swelling lobes - a kiln's flue, wide where it breathes and pinched between - carrying
        /// the heat ramp: <b>white to yellow to orange to red to brown</b> and back up again. Five stops
        /// rather than four, and the two in the middle are the #152 rival pair met head on, orange against
        /// red with nothing between them but a boundary.
        /// <para>
        /// <b>Its sweep is the same slanted plane with a comb cut into it</b> (<see cref="KILN_TOOTH"/>): the
        /// boundary zigzags across the body rather than running straight, so consecutive stops interlock like
        /// a zip instead of butting up against each other. That is the brief's "bleeding between floors" made
        /// literal, and it costs the level its big plates - a band cut by teeth is several groups rather than
        /// one, which is the whole of the step up from <see cref="Icicle"/>.
        /// </para>
        /// <para>
        /// The lobes are what keeps that legible. A comb on a straight column reads as noise; a comb on a
        /// silhouette that swells every <see cref="KILN_LOBE"/> levels reads as the colour pooling in the
        /// wide part and pinching through the narrow one, which is what a gradient down a shaped body does.
        /// </para>
        /// <para>
        /// Measured: 484 balls, margin 2, nothing alone or paired, 6 recoloured by the repair pass (the
        /// teeth are what the lattice rounds off here); <b>22 standing groups</b> against the Icicle's 6,
        /// which is the comb's whole effect stated as a number, and best single shots of 2-25 %. Counts
        /// 56-124, white lowest and in groups of at most 14 - the fold's end cut into slivers by the teeth.
        /// PASS on <c>aimcheck</c> (44.1 deg of a 47.0 limit, the block's narrowest field) and hung unshot
        /// for 35 s.
        /// </para>
        /// </summary>
        private static Design Kiln() => new()
        {
            File = "Kiln.json",
            Name = "Kiln",
            Grid = KILN_GRID,
            Depth = KILN_DEPTH,
            FieldLevels = KILN_FIELD_LEVELS,
            Scene = SPECTRUM_SCENE,
            Sky = SPECTRUM_SKY,
            Music = MUSIC_SPECTRUM,
                Balls = BALLS_SPECTRUM,
            Shots = 56,
            CeilingStep = 5,
            Occupied = (r, ang, i, depth) => r <= KilnRadius(i, depth),
            Colour = KilnColour,
        };

        /// <summary>
        /// Two ribbons wound in opposite directions down the same axis, one inside the other, passing each
        /// other on the way - and each carries the green family <b>white to green to olive to black</b> along
        /// its own length, the two started half a family apart. So the pair facing each other across the axis
        /// are never the same stop, and the gradient is read by walking the gun round the level as much as by
        /// looking up it.
        /// <para>
        /// <b>The two orbits differ, and that is Garland's lesson taken rather than re-learned</b> (#182): on
        /// one shared orbit a crossing merges into a single disc that is the only thing on its level, and
        /// shooting that disc's colour cuts BOTH ribbons at once - measured at 85 % of the level there. Here
        /// the inner ribbon runs at <see cref="TRELLIS_ORBIT_INNER"/> and the outer at
        /// <see cref="TRELLIS_ORBIT_OUTER"/>, so they touch at a pass without ever occupying the same cells,
        /// and each pass is where a ribbon cut above it stops falling.
        /// </para>
        /// <para>
        /// The stop boundaries are sheared along the arc as well as down (<see cref="TRELLIS_SKEW"/>), so a
        /// band crosses its ribbon on the diagonal rather than sitting square across it - the same reason
        /// every other sweep in the block is tilted, applied to a body that is already a spiral.
        /// </para>
        /// <para>
        /// The one <b>open</b> silhouette in the block, and the only one whose shape reads whole from the
        /// gun rather than needing the map: two bands crossing over a gap. Measured: 277 balls - the
        /// lightest here - margin 1, nothing alone or paired, 3 recoloured; 15 standing groups, counts
        /// 51-89, best single shots 7-34 % (green's 34 is a high band taking the ribbon under it as far as
        /// the pass, which is what the pass is for). PASS on <c>aimcheck</c> (47.5 deg of a 50.4 limit) and
        /// hung unshot for 38 s.
        /// <para>
        /// <b>Winding it faster to buy a second pass improves every one of those numbers and loses the
        /// level</b> - see <see cref="TrellisCentre"/>, which is where that measurement lives. It is the one
        /// design here whose tuning is bounded by the physics rather than by the drop test.
        /// </para>
        /// </para>
        /// </summary>
        private static Design Trellis() => new()
        {
            File = "Trellis.json",
            Name = "Trellis",
            Grid = TRELLIS_GRID,
            Depth = TRELLIS_DEPTH,
            FieldLevels = TRELLIS_FIELD_LEVELS,
            Scene = SPECTRUM_SCENE,
            Sky = SPECTRUM_SKY,
            Music = MUSIC_SPECTRUM,
                Balls = BALLS_SPECTRUM,
            Shots = 52,
            CeilingStep = 5,
            Occupied = (r, ang, i, depth) => TrellisRibbon(r, ang, i) != 0,
            Colour = TrellisColour,
        };

        /// <summary>
        /// An hourglass: a wide plate against the glass drawing in to a waist halfway down and flaring out
        /// again to a foot, in the twilight family <b>white to magenta to navy to black</b>. The one
        /// silhouette in the game with a waist in it, and the one sweep in the block that is not a plane at
        /// all.
        /// <para>
        /// <b>The stops are NESTED CONES</b>: the sweep runs on height and radius together
        /// (<see cref="HOURGLASS_RISE"/> levels a cell outward), so a stop is a conical shell and the top
        /// plate reads as concentric rings of the ramp - Onion's shells stood on end and given a direction.
        /// The rings are <see cref="HOURGLASS_PER_STOP"/> divided by that rise wide, which is deliberately
        /// over two cells: a ring one cell wide running diagonally is a string of balls that do not touch
        /// each other, which is the lonely-ball rule and what the first Gem got wrong.
        /// </para>
        /// <para>
        /// The waist is the level's difficulty and it is geometric rather than chromatic: everything below it
        /// hangs through a few cells' worth of neck, so the foot is the part of the cluster a player has to
        /// think about before cutting into. The colours there are the darkest of the family, which is the
        /// ramp arriving where the shape does.
        /// </para>
        /// <para>
        /// Measured: 447 balls, margin 2, nothing alone (2 in pairs), nothing recoloured; 9 standing groups,
        /// counts 71-152, best single shots <b>8-17 %</b> - against the <b>89 %</b> the same body took with
        /// the shells left unwound, which is the whole argument for <see cref="HOURGLASS_PITCH"/>. PASS on
        /// <c>aimcheck</c> (46.4 deg of a 49.3 limit) and hung unshot for 35 s.
        /// </para>
        /// </summary>
        private static Design Hourglass() => new()
        {
            File = "Hourglass.json",
            Name = "Hourglass",
            Grid = HOURGLASS_GRID,
            Depth = HOURGLASS_DEPTH,
            FieldLevels = HOURGLASS_FIELD_LEVELS,
            Scene = SPECTRUM_SCENE,
            Sky = SPECTRUM_SKY,
            Music = MUSIC_SPECTRUM,
                Balls = BALLS_SPECTRUM,
            Shots = 48,
            CeilingStep = 6,
            Occupied = (r, ang, i, depth) => r <= HourglassRadius(i, depth),
            Colour = (r, ang, i, depth) => Sweep(HourglassSweep(r, ang, i, depth), TWILIGHT),
        };

        /// <summary>
        /// The block's finale and the campaign's last level: blades on a slowly turning core wearing the
        /// <b>whole wheel</b> - red, orange, yellow, green, cyan, blue, magenta - swept HELICALLY, so every
        /// blade is its own hue at any height and the wheel turns as it descends. The chapter's thesis stated
        /// once and entire: the family here is the spectrum itself, and the ordering is the only thing
        /// holding seven colours apart.
        /// <para>
        /// <b>Seven live colours is the difficulty, and it is scarcity rather than mass</b> - the magazine
        /// draws evenly among the colours still standing, so the wanted ball arrives one time in seven, and
        /// the level is priced against that rather than against its size. <see cref="Garland"/> is the only
        /// harder draw in the game (thirteen), and it closes the Nebula for the same reason.
        /// </para>
        /// <para>
        /// <b>The helix is why the blades are safe to cut.</b> A blade is a radial slab reaching the top
        /// plate, so each hangs on its own and nothing rides another down; and because the sweep turns with
        /// the core (<see cref="TURBINE_TURNS_PER_LEVEL"/>) rather than standing still, a blade's colour
        /// walks the wheel down its own length instead of standing as one vertical stave of dozens - the
        /// trap <see cref="Lantern"/> records from the other side.
        /// </para>
        /// <para>
        /// <b>The blades were opened up by looking, and the numbers came with it.</b> Drawn first at
        /// <see cref="TURBINE_REACH"/> 4.1 they passed every gate and photographed as a <i>column</i> - five
        /// slabs that short project to a filled disc from any angle, so the level had the finale's colours
        /// and Column's silhouette. Longer and a shade thinner they read as arms with sky between them, and
        /// the level went from 591 balls in 23 groups to 658 in 31: the sweep's radial term now has three
        /// and a half cells of blade to run along instead of two, so a blade wears two hues rather than one.
        /// Four blades were tried in the same pass and refused - 47 groups at 1.11 shots a group, tighter
        /// than Colossus, with 24 balls standing in pairs.
        /// </para>
        /// <para>
        /// Measured: 658 balls (only <see cref="Onion"/>'s 959 is larger), margin 1, nothing alone or
        /// paired, 2 recoloured; 31 standing groups at <b>1.68 shots a group</b> - the tightest in the block
        /// and inside the Arcade's 1.37-1.65 band - counts 51-126 over the seven, best single shots 3-14 %.
        /// PASS on <c>aimcheck</c> (47.5 deg of a 50.4 limit) and hung unshot for 35 s.
        /// </para>
        /// </summary>
        private static Design Turbine() => new()
        {
            File = "Turbine.json",
            Name = "Turbine",
            Grid = TURBINE_GRID,
            Depth = TURBINE_DEPTH,
            FieldLevels = TURBINE_FIELD_LEVELS,
            Scene = SPECTRUM_SCENE,
            Sky = SPECTRUM_SKY,
            Music = MUSIC_SPECTRUM,
                Balls = BALLS_SPECTRUM,
            Shots = 52,
            CeilingStep = 4,
            Occupied = (r, ang, i, depth) => TurbineCore(r) || TurbineBlade(r, ang, i) != 0,
            Colour = (r, ang, i, depth) => Sweep(TurbineSweep(r, ang, i, depth), SPECTRUM),
        };

        #endregion

        #region The Spectrum's second hang (#255)

        //THE SPECTRUM'S SECOND FIVE (#255), hung after Turbine in the same place at the same hour: the city
        //under dome 11, MUSIC_SPECTRUM, and the block's own law verbatim - one HUE FAMILY a level, swept
        //through the whole body, no new colour anywhere, and NO SWEEP THAT IS A STACK OF FLOORS. Each is
        //TALL for the first block's reason (a multi-stop ramp needs the height to read as a ramp) and each
        //is a different KIND of body under a different KIND of sweep, which is what the first five
        //established and what these five continue:
        //  Totem     - four different solids on one spine, crossed by ONE unbroken sweep.
        //  Pinecone  - a phyllotaxis whose scale arms and colour stops are the SAME object.
        //  Bolt      - a column that jogs, the sweep running about each broken segment's OWN axis.
        //  Girandole - five candles of five lengths on one crown, each burning down the family alone.
        //  Pleat     - a wall creased in plan, where the sweep folds exactly where the geometry does.
        //
        //THE ONE DEPARTURE FROM THE FIRST FIVE IS THE FOLD ITSELF - see Fold, which is not Sweep. Sweep
        //folds the stop INDEX over a period of 2n-2 and therefore hits the family's two ends once against
        //its middles' twice; every one of these five is priced against an EVEN share (a quarter of every
        //level to each of four stops), because four of them put a whole structural member - an arm, a
        //candle, a panel - on one stop and a member at half count is a member the player cannot match.
        //Both are folds and both obey the block's rule that the only colours to touch are neighbours in
        //the ramp; they differ only in what is folded.
        //
        //THE BLOCK'S OWN RULE HOLDS ON ALL FIVE, and it is the first thing to check on any of them: EVERY
        //STOP OF THE FAMILY STANDS ON THE TOP LEVEL. Totem's drum, Pinecone's crown, Bolt's top segment,
        //Girandole's crown disc and Pleat's header each span their sweep's whole coordinate at d = 0, so
        //the level everything hangs from is four colours and not one.
        //
        //NUMBERS BELOW ARE THE DRAWINGS', NOT MEASUREMENTS. Each doc names the checks its judged spec's
        //gate notes flagged and the fallback constants pre-authorised for each; the generator's own report
        //is what settles them, and no figure here goes into a doc as measured until it has been.

        //THE FIVE FAMILIES OF THE SECOND HANG, stated together the way FROST and its four are, because the
        //FAMILY is what a level of this chapter is. Same law: each is a SUBSET AND AN ORDERING of the fixed
        //thirteen and never a new hue, and each was judged on which adjacencies it puts next to each other.
        private static readonly BallType[] REEF =
        {
            BallType.Type5,    //cyan
            BallType.Type4,    //white
            BallType.Type9,    //orange
            BallType.Type1,    //red
        };

        private static readonly BallType[] BRONZE =
        {
            BallType.Type7,    //yellow
            BallType.Type13,   //olive
            BallType.Type10,   //brown
            BallType.Type8,    //black
        };

        private static readonly BallType[] VOLT =
        {
            BallType.Type7,    //yellow
            BallType.Type2,    //green
            BallType.Type5,    //cyan
            BallType.Type3,    //blue
        };

        private static readonly BallType[] GARNET =
        {
            BallType.Type11,   //silver
            BallType.Type6,    //magenta
            BallType.Type1,    //red
            BallType.Type10,   //brown
        };

        private static readonly BallType[] AURORA =
        {
            BallType.Type12,   //navy
            BallType.Type2,    //green
            BallType.Type5,    //cyan
            BallType.Type6,    //magenta
        };

        /// <summary>
        /// The second hang's fold: a sweep coordinate in <b>turns</b> folded into one stop of the family -
        /// out through the ramp and back again, so the only colours that ever touch are neighbours in it.
        /// The block's law (#253) held with a different arithmetic from <see cref="Sweep"/>'s, and the
        /// difference is the whole reason this exists rather than a call to that.
        /// <para>
        /// <b><see cref="Sweep"/> folds the stop INDEX; this folds the COORDINATE.</b> A fold of
        /// <c>n</c> indices has period <c>2n - 2</c> and lands on the two ENDS once against the middles'
        /// twice, which the first five accept deliberately - a family's extremes being rarer is what makes
        /// them read as extremes. Folding the coordinate instead runs a triangle wave over one turn and
        /// cuts it into <c>n</c> equal slices, so every stop takes an equal share of every level. Four of
        /// these five need that share to be equal because a structural member - a scale arm, a candle, a
        /// pleat panel - is painted one stop for its whole length, and a member at half count is a member
        /// the player cannot draw a ball for.
        /// </para>
        /// <para>
        /// <b>The wedge boundaries fall on exact multiples of <c>1 / (2n)</c> of the coordinate</b>, which
        /// is a trap for anything that wants to sit ON a stop rather than across a boundary: eight arms at
        /// eighths of a turn land on all eight boundaries of a four-stop fold, where the floor takes
        /// whichever side the float arithmetic happens to fall on. Anything wanting a solid stop has to be
        /// phased half a wedge onto the wedge's centre. Nothing here needs that today - the one design that
        /// did, <see cref="Pinecone"/>, was measured into a different colouring for a different reason (see
        /// its own doc) - and the trap is recorded because the next member painted on a structural axis
        /// will meet it.
        /// </para>
        /// </summary>
        private static BallType Fold(float turns, BallType[] family)
        {
            //frac(turns), which MathF.Floor gets right for a negative coordinate - every sweep here is
            //built on an atan2 and half of that function's range is below zero
            float frac = turns - MathF.Floor(turns);

            //The triangle: 0 at the family's first stop, 1 at its last, and back again over one turn
            float f = 1f - 2f * MathF.Abs(frac - HALF);
            int stop = (int)MathF.Floor(f * family.Length);

            //f reaches 1 exactly at the fold's turning point, which floors to family.Length itself
            return family[Math.Min(stop, family.Length - 1)];
        }

        /// <summary>The centred taxicab distance |dx| + |dz|, read back off the polar frame Emit hands in.</summary>
        private static float PlanTaxicab(float r, float ang) =>
            r * (MathF.Abs(MathF.Cos(ang)) + MathF.Abs(MathF.Sin(ang)));

        /// <summary>The centred Chebyshev distance max(|dx|, |dz|) - a plan square - off the same frame.</summary>
        private static float PlanChebyshev(float r, float ang) =>
            r * MathF.Max(MathF.Abs(MathF.Cos(ang)), MathF.Abs(MathF.Sin(ang)));

        /// <summary>
        /// The plan distance from a cell at (<paramref name="r"/>, <paramref name="ang"/>) to a point
        /// standing at radius <paramref name="ringR"/> on the bearing <paramref name="seat"/> - the cosine
        /// rule, which is the whole of what a body hung OFF the axis needs from the polar frame.
        /// </summary>
        private static float PlanDistance(float r, float ang, float ringR, float seat)
        {
            float squared = r * r + ringR * ringR - 2f * r * ringR * MathF.Cos(ang - seat);

            //The cosine rule can round a cell sitting exactly on the seat to a hair below zero
            return MathF.Sqrt(MathF.Max(squared, 0f));
        }

        /// <summary>
        /// A drum, a diamond, a sphere and a box stacked on one spine and finished with a tenon - four
        /// solids that would each be a whole level elsewhere, spent as beads on one necklace - painted by a
        /// single sweep that ignores where one bead ends and the next begins. The block's purest style
        /// statement and its safest build: a solid axial stack whose narrowest waists are 12-cell discs,
        /// nowhere near mush or single-path territory.
        /// <para>
        /// <b>The beads, top to bottom by levels below the glass</b> (see <see cref="LevelsBelowGlass"/>):
        /// the DRUM at d 0..5, a disc of <see cref="TOTEM_DRUM"/> flaring through a
        /// <see cref="TOTEM_SHOULDER"/> course into a two-level <see cref="TOTEM_SKIRT"/> eave; the
        /// OCTAHEDRON at d 6..13, taxicab courses <see cref="TOTEM_DIAMOND"/> out to 5 and back - the
        /// lattice confessing, since its taxicab faces are the true close-packing planes; the SPHERE at
        /// d 14..20 on <see cref="TOTEM_SPHERE"/>; the BOX at d 21..27, a plan square of
        /// <see cref="TOTEM_BOX"/>; and the two-level TENON at <see cref="TOTEM_TENON"/>. Every junction
        /// overlaps in plan, so the stack is one connected spine and no bead hangs off a point — and since
        /// #302 every junction course IS a painted COLLAR (<see cref="TOTEM_COLLAR_DS"/>): a full disc of
        /// 3.0 in four sectors of a ramp-neighbour pair, the shootable tiers the owner asked for. A collar
        /// severs the two ribbons its pair excludes (the sag fix — see the constant's own comment for the
        /// trace), is a wider plate than the necks it replaced, and cleared whole it is a deliberate
        /// guillotine that sheds every bead below it.
        /// </para>
        /// <para>
        /// <b>The eave is the packing made visible</b> and it is the one graft in the design: the skirt
        /// courses stand a full cell proud of the shoulder above them, and their rim cells nest half a cell
        /// into that course's pockets - inside the lattice's 0.71-cell cross-level reach, which is why
        /// <see cref="TOTEM_SHOULDER"/> is 3.7 and not 3.4. It turns the drum into a proper bead cap and
        /// adds the silhouette's fifth event.
        /// </para>
        /// <para>
        /// The sweep is the block's plain helicoid - one stop every <see cref="TOTEM_PER_STOP"/> levels of
        /// climb, sheared round the one axis all four beads share at <see cref="TOTEM_PITCH"/> levels a
        /// turn - and the point of the level is that it does not know about the beads at all: it bends over
        /// the diamond's facets, pinches over the sphere's equator and squares off round the box, one
        /// gradient against four discontinuities. <see cref="REEF"/> puts white MID-ramp between cyan and
        /// orange, which no family in the first hang does. It was drawn as a fold over the ANGLE first and
        /// measured four standing groups; <see cref="TotemColour"/> carries that finding.
        /// </para>
        /// <para>
        /// Gate watch (#255), updated by #302: BOX-CORNER CONNECTIVITY at d 21 is now the collar's - the
        /// box's top course hooks the d 20 collar (a 3.0 disc against the corners' 2.12, wider than the
        /// sphere's closing 2.4 ever was), so the pre-authorised sphere-widening remedy was never needed
        /// and is retired. Second: the two-level tenon is 21 cells over two levels against four stops and
        /// is the one place here the repair pass may churn - if it does, deepen it to three levels
        /// (TOTEM_DEPTH 31 with TOTEM_FIELD_LEVELS 33, which keeps the offset even). Third: the grafted
        /// skirt takes the widest radius to 4.2, so occupied columns should read 2..10 on the shifted
        /// levels and 3..10 on the unshifted ones - the margin is the diamond's, not the drum's, and
        /// taxicab 5 puts that at exactly one clear column.
        /// </para>
        /// <para>
        /// Measured (#302, with the collars): 979 balls in 26 standing groups (1.92 shots a group against
        /// the budget of 50), 37 ceiling anchors at 26.5 each (load 34.4), margin 1, nothing alone, 8 in
        /// pairs, 6 recoloured; largest standing groups white 184 (18 %), orange 168 (17 %), cyan 95,
        /// red 45 - the sweep alone had white at 308 and orange at 305, each ONE ribbon the length of the
        /// stack, and the sag probe lost 5 orders of 5 on exactly those releases (worst at shot 2, glass at
        /// rest). With the collars it reads <b>0 of 5, the worst order clearing the level whole at shot
        /// 21</b>, closest approach -0.59 - a transient inside the game's own allowance.
        /// </para>
        /// </summary>
        private static Design Totem() => new()
        {
            File = "Totem.json",
            Name = "Totem",
            Grid = TOTEM_GRID,
            Depth = TOTEM_DEPTH,
            FieldLevels = TOTEM_FIELD_LEVELS,
            Scene = SPECTRUM_SCENE,
            Sky = SPECTRUM_SKY,
            Music = MUSIC_SPECTRUM,
                Balls = BALLS_SPECTRUM,
            Shots = 50,
            CeilingStep = 5,
            Occupied = TotemOccupied,
            Colour = TotemColour,
        };

        //THE TOTEM'S OWN FIGURES. Thirty levels in a field of 32 - the offset is 2 and even, which is what
        //keeps every course's level parity where it was drawn.
        private const byte TOTEM_GRID = 13;
        private const byte TOTEM_DEPTH = 30;
        private const byte TOTEM_FIELD_LEVELS = 32;

        //Where one bead ends, counted in levels below the glass: drum 0..5, octahedron 6..13, sphere
        //14..20, box 21..27, tenon 28..29. Courses 6, 13, 20 and 27 are the collars now
        //(TOTEM_COLLAR_DS), so the shapes those four would have drawn - TOTEM_DIAMOND's two end entries,
        //TOTEM_SPHERE's last and the box's bottom course - are stated but no longer drawn.
        private const int TOTEM_DRUM_LAST = 5;
        private const int TOTEM_DIAMOND_LAST = 13;
        private const int TOTEM_SPHERE_LAST = 20;
        private const int TOTEM_BOX_LAST = 27;

        //The drum and its grafted eave: three courses of DRUM, one SHOULDER course, then two of SKIRT. The
        //shoulder exists only so the skirt's rim has an up-neighbour - see the design's doc.
        private const float TOTEM_DRUM = 3.4f;
        private const float TOTEM_SHOULDER = 3.7f;
        private const float TOTEM_SKIRT = 4.2f;
        private const int TOTEM_SHOULDER_D = 3;

        //The octahedron's taxicab half-widths, one per course from d 6. Five is the widest thing in the
        //level and it is what sets the lateral margin: taxicab 5 reaches column 1 and column 11 of 13.
        private static readonly int[] TOTEM_DIAMOND = { 2, 3, 4, 5, 5, 4, 3, 2 };

        //The sphere's radii, one per course from d 14. Its last entry is the box's only anchor, which is
        //the gate's first check.
        private static readonly float[] TOTEM_SPHERE = { 2.4f, 3.1f, 3.5f, 3.6f, 3.5f, 3.1f, 2.4f };

        //The box's half-width as a PLAN SQUARE in the centred frame rather than a raw index range. Both
        //readings of a square wobble by half a cell against the lattice - the shifted levels sample it at
        //integers and the unshifted ones at half-integers - and this one wobbles about the axis the other
        //four beads are turned on, where a fixed index range would sit half a cell off it on every second
        //course. At 2.2 the courses come out 5 and 4 cells a side in alternation, which is what the same
        //alternation already does to every disc in the block.
        private const float TOTEM_BOX = 2.2f;
        private const float TOTEM_TENON = 1.8f;

        //THE SWEEP, in the block's own two figures (see TotemColour for what they replaced and why). PER_STOP
        //is how many levels of climb one stop is worth and PITCH how many levels of sweep a full turn round
        //the axis carries: PITCH over PER_STOP is 6, which is REEF's folded period, so one turn walks the
        //family out and back and every stop stands on the top level in a wedge. Over thirty levels that is
        //two whole folds down the totem, which is the gradient crossing all four bead junctions twice.
        private const float TOTEM_PER_STOP = 2.5f;
        private const float TOTEM_PITCH = 15f;

        //The post the beads are threaded on, and the sweep's own step across it - three stops is half of
        //REEF's folded six, the offset that cannot land on the colour it is separating. See TotemColour.
        private const float TOTEM_SPINE = 1.4f;
        private const float TOTEM_SPINE_STEP = 3f;

        //THE COLLARS (#302): the four bead junctions redrawn as full discs of one radius, each painted as
        //four sectors of a ramp-NEIGHBOUR ink pair - the shootable tiers the owner asked for, and the sag
        //probe's own trace is why they are here. The sweep made each of REEF's two middle stops ONE ribbon
        //winding all thirty levels - white measured 308 balls in a single standing group and orange 305, a
        //third of the level apiece - and every losing probe order died on the shot that took one: 306 gone
        //in a frame, nothing orphaned, and the sheared remainder stretched a metre past the line with the
        //glass at rest (ends 131/131, and a 131-ball release read +2.0 of clearance where the 306 read
        //-1.0). A collar whose pair does NOT contain a colour severs that colour's ribbon at that depth, so
        //the pairs alternate - cyan/white at d 6 and 20, orange/red at d 13 and 27 - and each colour is cut
        //at the two collars that exclude it: the largest group falls from 308 to ~140 by arithmetic, and the
        //measured figures are in the design's doc. What a collar buys besides the cut: it is a full disc, so
        //clearing its four sectors is a deliberate two-ink guillotine that drops every bead below it (the
        //pressure valve the owner's "impossible to shoot the parts off fast enough" is asking for), and at
        //3.0 it is a wider plate than the 13-cell necks it replaces, so every junction carries more links
        //than it did. ⚠ The pair MUST be ramp neighbours (the collar's own two inks touch along its sector
        //seams), and a collar ink does fuse with its own colour's ribbons where they meet it - that is the
        //"fused" half of the arithmetic above, priced in, and the reason the pairs alternate rather than
        //repeat.
        private static readonly int[] TOTEM_COLLAR_DS = { 6, 13, 20, 27 };
        private const float TOTEM_COLLAR = 3.0f;
        private const int TOTEM_COLLAR_SECTORS = 4;
        private static readonly BallType[][] TOTEM_COLLAR_INKS =
        {
            new[] { BallType.Type5, BallType.Type4 },   //cyan, white  - d 6, severs orange and red there
            new[] { BallType.Type9, BallType.Type1 },   //orange, red  - d 13, severs cyan and white
            new[] { BallType.Type5, BallType.Type4 },   //cyan, white  - d 20
            new[] { BallType.Type9, BallType.Type1 },   //orange, red  - d 27
        };

        /// <summary>Which collar a level is, 0..3, or -1 for none - the index into the ink table.</summary>
        private static int TotemCollar(int d) => Array.IndexOf(TOTEM_COLLAR_DS, d);

        private static bool TotemOccupied(float r, float ang, int i, int depth)
        {
            int d = LevelsBelowGlass(i, depth);

            //The collars replace the junction courses outright - see TOTEM_COLLAR_DS. Each sits under a
            //course that fully covers it (skirt 4.2, diamond 3.5, sphere 3.1, box 2.2-with-corners) and
            //over one it fully covers, so every junction still overlaps in plan and now does it wider.
            if (TotemCollar(d) >= 0) return r <= TOTEM_COLLAR;

            if (d <= TOTEM_DRUM_LAST)
                return r <= (d < TOTEM_SHOULDER_D ? TOTEM_DRUM
                    : d == TOTEM_SHOULDER_D ? TOTEM_SHOULDER
                    : TOTEM_SKIRT);

            //The taxicab distance is an exact integer at BOTH parities (half-integer dx and dz sum to a
            //whole one), so the test is written against m + HALF: a whole half-cell of slack round a value
            //the float arithmetic would otherwise be free to land a hair either side of.
            if (d <= TOTEM_DIAMOND_LAST)
                return PlanTaxicab(r, ang) <= TOTEM_DIAMOND[d - TOTEM_DRUM_LAST - 1] + HALF;

            if (d <= TOTEM_SPHERE_LAST) return r <= TOTEM_SPHERE[d - TOTEM_DIAMOND_LAST - 1];
            if (d <= TOTEM_BOX_LAST) return PlanChebyshev(r, ang) <= TOTEM_BOX;

            return r <= TOTEM_TENON;
        }

        //The whole level in one line, which is the design: the sweep is a function of the angle and the
        //height and of nothing the beads know about.
        //
        //IT IS THE BLOCK'S CANONICAL HELICOID and it was NOT, first time round. Written as a fold over the
        //ANGLE with a slow drift down (a quarter turn over the whole stack), every stop came out as a
        //near-vertical stave running the full thirty levels - and the beads are SOLID, so the staves of a
        //colour met each other through the spine and the level measured FOUR standing groups, one per
        //colour, a 919-ball tower a perfect player takes in four shots. That is the Icicle's own recorded
        //failure arriving from the other side (its doc: "the six standing groups become four - one per
        //colour, the whole level in four shots"), and the fix is the rule the block already states: the
        //sweep runs DOWN at TOTEM_PER_STOP levels a stop with the angle only shearing it, at a pitch of one
        //whole folded family a turn. A stop is then a ribbon one turn long whose neighbour above and below
        //is a different stop, so it cannot touch itself, and the gradient crossing the four bead junctions
        //- the point of the level - reads harder rather than softer for running down the totem's length.
        //THE SPINE IS WHAT KEEPS THE WEDGES APART, and it is the second thing this level had to be told.
        //With the helicoid alone it still measured four standing groups, and the axis is the reason: a
        //wedge is a radial slab from the surface to the middle, so ALL of them meet where the beads are
        //solid, and the two wedges a folded family gives a colour - stop k and stop (period - k) - fuse
        //there however cleanly the sweep divides the shell. The Icicle never met this because its cone
        //draws in to a tip barely a cell across; a drum 4.2 wide has a real core. So the spine takes the
        //sweep HALF A FOLD out of step (TOTEM_SPINE_STEP of REEF's six), which is both a fix and the honest
        //reading of the object: a totem's beads are threaded on a post, and the post is painted too.
        private static BallType TotemColour(float r, float ang, int i, int depth)
        {
            int d = LevelsBelowGlass(i, depth);

            //A collar wears its own pair in four sectors, not the sweep - severing the ribbons is its whole
            //job (see TOTEM_COLLAR_DS) and a swept collar would sever nothing
            int collar = TotemCollar(d);
            if (collar >= 0)
                return TOTEM_COLLAR_INKS[collar][SectorIndex(ang, 0f, TOTEM_COLLAR_SECTORS) % 2];

            float stops = (d + TOTEM_PITCH * (WrapAngle(ang) / MathF.Tau)) / TOTEM_PER_STOP;

            return Sweep(r <= TOTEM_SPINE ? stops + TOTEM_SPINE_STEP : stops, REEF);
        }

        /// <summary>
        /// A pinecone whose spiral scale arms are each one pure colour - <b>the gradient is the
        /// phyllotaxis</b>. Every other level in the chapter sweeps its family across a body; here the
        /// body's own anatomy and the sweep are the same object, and what the player sees is the lattice's
        /// own close packing standing up as eight helical arms of frozen colour.
        /// <para>
        /// <b>The gradient runs ALONG the arms, and the first cut of this design ran it across them.</b>
        /// Written to hold one stop constant down each arm - an arm a pure colour, which is what the pitch
        /// promised - it measured <b>four standing groups on 917 balls</b>: eight arms fold onto four stops,
        /// so the two arms of a colour met each other through the solid core and every colour became one
        /// group a perfect player takes in a single shot. The phyllotaxis is untouched by the fix, because
        /// what carries the family is still the arm and nothing else: the stop steps one every
        /// <see cref="PINECONE_PER_STOP"/> levels down an arm's own descent, and <see cref="PineconeArmIndex"/>
        /// offsets it by a whole stop per arm, so the eight arms show eight consecutive stops at any height
        /// and no two neighbours ever agree. The gradient is read along the spirals now instead of across
        /// them - which is the one reading in which a pinecone's own geometry is doing the work.
        /// </para>
        /// <para>
        /// <see cref="PINECONE_TURN"/> is therefore geometry alone now, where it used to be geometry and
        /// colour at once: it is how fast the eight arms wind, and <see cref="PineconeArmIndex"/> reads the
        /// same figure only to know which arm a cell is on.
        /// </para>
        /// <para>
        /// The body is a solid ovoid core (<see cref="PineconeCore"/>, a half sine from
        /// <see cref="PINECONE_CORE"/> to 3.6 and cut short at the tip) with the scales as same-level
        /// lateral appendages on every other course - so there is nothing hanging free anywhere and the
        /// physical risk is near zero. The core spirals with the arms, because it is painted by the same
        /// rule.
        /// </para>
        /// <para>
        /// Gate watch (#255), in the spec's order: THE ARM-LOCK ARITHMETIC against
        /// <c>BallsMap.GetRealPosition</c> first - the odd levels' half-shift enters the atan2 the sweep is
        /// built on, so dump one arm's colour column before believing anything else about this level. A
        /// sign slip in the sweep's minus sign is the whole failure mode. Second: lonely balls where an
        /// 18-degree wedge catches only two cells at small core radius - if the repair pass churns the top
        /// courses, raise <see cref="PINECONE_SCALE_FIRST"/> to 6, where the core is already 2.7 across;
        /// the half-angle may go to 22 degrees (0.384) before that, and <see cref="PINECONE_TURN"/> must
        /// not move at all or the lock breaks. Third and unflagged by the spec: the core carries the arms'
        /// own stripe colours, so a stop is one connected body from crown to tip and the report will show
        /// very FEW standing groups - if it shows four, the level is four shots long whatever the budget
        /// says, and the fix is to break the core's stripe rather than to spend shots.
        /// </para>
        /// <para>
        /// <b>The three tiers are #302's answer to that same warning arriving in the simulation.</b> Eight
        /// groups passed the paper reading, but the fold hands the family's middles two stops a period, so
        /// brown and olive each stood as ONE spiral network of ~310 balls — and the sag probe lost the
        /// level five orders of five, every one two or three such releases followed by the remainder
        /// stretching past the line with the glass at rest (the owner's "yellow and black drag the map
        /// down", which the arithmetic corrects to the middles). <see cref="PINECONE_TIER_LEVELS"/> holds
        /// the fix and the reasoning; measured after it: 917 balls in <b>17 standing groups</b> (3.06
        /// shots a group), counts 200–257 against the shipped 145–312, worst single shot 19 % against 34,
        /// anchor load 43.6, and the probe reads <b>0 of 5 — every order clears the level</b> (twice
        /// independently, worst dips −0.91/−0.87 inside the 1.00 allowance). Clearing a whole tier drops
        /// everything under it in one cascade, which is the shot the level never offered before.
        /// </para>
        /// </summary>
        private static Design Pinecone() => new()
        {
            File = "Pinecone.json",
            Name = "Pinecone",
            Grid = PINECONE_GRID,
            Depth = PINECONE_DEPTH,
            FieldLevels = PINECONE_FIELD_LEVELS,
            Scene = SPECTRUM_SCENE,
            Sky = SPECTRUM_SKY,
            Music = MUSIC_SPECTRUM,
                Balls = BALLS_SPECTRUM,
            Shots = 52,
            CeilingStep = 4,
            Occupied = PineconeOccupied,
            Colour = PineconeColour,
        };

        //THE PINECONE'S OWN FIGURES. Twenty-six levels in a field of 30: the offset is 4 and even.
        private const byte PINECONE_GRID = 13;
        private const byte PINECONE_DEPTH = 26;
        private const byte PINECONE_FIELD_LEVELS = 30;

        //Crown at d 0..1, body at d 2..23, tip below it. The crown is a plain disc because it is the level
        //everything hangs from and it has to carry all four stops across a full circle.
        private const float PINECONE_CROWN = 3.0f;
        private const int PINECONE_BODY_FIRST = 2;
        private const int PINECONE_BODY_LAST = 23;
        private const float PINECONE_TIP = 1.5f;

        //The ovoid: a half sine from CORE at the shoulder out to CORE + SWELL at d 14 and back. SPAN is a
        //couple of levels longer than the body, so the profile is cut short at 2.49 rather than closing to
        //the core radius - the tip disc takes it from there.
        private const float PINECONE_CORE = 1.8f;
        private const float PINECONE_SWELL = 1.8f;
        private const float PINECONE_BODY_SPAN = 24f;

        //The scales: eight bumps on every other course, standing SCALE_OUT proud of the core and
        //SCALE_HALF either side of their arm's centre. 18 degrees at a core of 3.6 is two and a half cells
        //of arc, which is a bump rather than a strand; 22 degrees (0.384) is the pre-authorised widening.
        private const int PINECONE_SCALES = 8;
        private const int PINECONE_SCALE_FIRST = 4;
        private const int PINECONE_SCALE_LAST = 22;
        private const float PINECONE_SCALE_OUT = 1.2f;
        private const float PINECONE_SCALE_HALF = 0.314f;

        //THE LOCK. One figure, read twice: the arms turn this much a level and the sweep unwinds by exactly
        //the same, so the fold coordinate is constant along an arm. It is the design and it does not move.
        private const float PINECONE_TURN = 0.045f;

        //How many levels of an arm's own descent one stop is worth. Twenty-two body levels over a fold of
        //six stops is a little under four folds down an arm, so a scale arm is read as a colour ramp
        //rather than as a stripe - and neighbouring arms, offset a stop by their index, never agree.
        private const float PINECONE_PER_STOP = 4f;

        //THE TIERS (#302): three one-level courses whose colour leaves the sweep entirely, each cut into
        //four arcs alternating the family's two END inks. The owner's "shootable tiers", and what they buy
        //is measured in the release size: the fold hands the family's middles two stops a period, so brown
        //and olive each stood as ONE spiral network of ~310 balls - a third of the level apiece - and all
        //five of the probe's losing orders were two or three such releases followed by the remainder
        //stretching past the line with the glass at rest. A colour network cannot skip a level, so one
        //recoloured course cuts every network that crosses it; three of them turn the middles' thirds into
        //segments a tier apart. ONE level tall, deliberately: a two-level tier of a two-ink band can weld
        //its own blocks through the cross-level diagonal (#301's fuse, three times over), and a single
        //level has no inside to weld through. The ENDS take the tiers because the fold already makes them
        //the scarce pair, so the tier cells walk their counts toward the middles' rather than away. FOUR
        //arcs, not two: a semicircle of one ink is half the premise-trap this block's header names, and at
        //four the largest tier group is a quadrant arc a shot takes without dropping anything - clearing a
        //WHOLE tier is the designed cascade (everything under it goes), which is exactly the "not the
        //spiral" way of removing mass the owner asked for. Seams roll half an arc a tier so no two tiers
        //share a boundary bearing.
        private static readonly int[] PINECONE_TIER_LEVELS = { 6, 12, 18 };
        private const int PINECONE_TIER_ARCS = 4;

        private static readonly BallType[] PINECONE_TIER_INKS = { BallType.Type7, BallType.Type8 };   //yellow, black

        private static float PineconeCore(int d) =>
            PINECONE_CORE + PINECONE_SWELL
            * MathF.Sin(MathF.PI * (d - PINECONE_BODY_FIRST) / PINECONE_BODY_SPAN);

        /// <summary>The centre of whichever of the eight arms this bearing is nearest, at this height.</summary>
        private static float PineconeArm(float ang, int d)
        {
            float step = MathF.Tau / PINECONE_SCALES;
            float seat = d * PINECONE_TURN * MathF.Tau;

            return seat + MathF.Round((ang - seat) / step) * step;
        }

        private static bool PineconeOccupied(float r, float ang, int i, int depth)
        {
            int d = LevelsBelowGlass(i, depth);

            if (d < PINECONE_BODY_FIRST) return r <= PINECONE_CROWN;
            if (d > PINECONE_BODY_LAST) return r <= PINECONE_TIP;

            float core = PineconeCore(d);
            if (r <= core) return true;

            //A scale course every other level, which is what makes each course nest into the pockets of
            //the one two levels above it. d is never negative, so the parity test is safe as written.
            if (d % 2 != 0 || d < PINECONE_SCALE_FIRST || d > PINECONE_SCALE_LAST) return false;
            if (r > core + PINECONE_SCALE_OUT) return false;

            return MathF.Abs(WrapAngle(ang - PineconeArm(ang, d))) <= PINECONE_SCALE_HALF;
        }

        //THE GRADIENT RUNS ALONG THE ARMS, and that is the design after the first cut of it was measured.
        //Written to hold one stop CONSTANT down each arm - the "lock" - the eight arms came out as eight
        //pure staves, and since eight arms fold onto four stops the two arms of a colour met each other
        //through the solid core: FOUR standing groups on 917 balls, a level a perfect player takes in four
        //shots. The phyllotaxis survives the fix intact, because what carries the family is still the arm
        //and nothing else: the stop walks the ramp DOWN an arm's own length, one every PINECONE_PER_STOP
        //levels, and the arm INDEX offsets it by a stop, so the eight arms show eight consecutive stops at
        //any height and no two neighbours ever agree. The gradient is still the phyllotaxis - it is read
        //along the spirals now instead of across them.
        private static BallType PineconeColour(float r, float ang, int i, int depth)
        {
            int d = LevelsBelowGlass(i, depth);

            //The tiers stand outside the sweep - see PINECONE_TIER_LEVELS for what that buys and why they
            //are one level tall and four arcs round
            int tier = Array.IndexOf(PINECONE_TIER_LEVELS, d);
            if (tier >= 0)
                return PINECONE_TIER_INKS[SectorIndex(ang, tier / (2f * PINECONE_TIER_ARCS), PINECONE_TIER_ARCS) % 2];

            return Sweep(d / PINECONE_PER_STOP + PineconeArmIndex(ang, d), BRONZE);
        }

        /// <summary>
        /// Which of the eight arms a bearing belongs to at this height, as a signed integer - the same
        /// rounding <see cref="PineconeArm"/> does, kept as the index rather than turned back into an
        /// angle. It goes straight into <see cref="Sweep"/>, which normalises a negative stop itself.
        /// </summary>
        private static int PineconeArmIndex(float ang, int d)
        {
            float step = MathF.Tau / PINECONE_SCALES;
            float seat = d * PINECONE_TURN * MathF.Tau;

            return (int)MathF.Round((ang - seat) / step);
        }

        /// <summary>
        /// A lightning bolt frozen mid-strike: a chamfered square column that jogs sideways at gusseted
        /// elbows and never hangs still, under its own hex HEAD since #302. Everything else in the block
        /// is a solid of revolution or a woven surface - this is a column that refuses its own axis, and
        /// the sweep corkscrews around each broken segment separately.
        /// <para>
        /// <b>Five segments of <see cref="BOLT_SEGMENT"/> levels</b>, their centres jogging three columns
        /// at a time (<see cref="BOLT_CENTRES"/>), each a 5x5 with the four corner cells cut - 21 cells a
        /// level, the smallest cross-section in the block and the reason this is the tightest budget of the
        /// five - hanging from a two-level chamfered 7x7 head (<see cref="BOLT_HEAD_LEVELS"/>).
        /// </para>
        /// <para>
        /// <b>The elbows are plates, not hinges, and since #302 they are also the SHOOTABLE TIERS</b> the
        /// owner's report asked for. Each jog is taken by the LOWER segment's top three levels spread
        /// across both footprints with the corners kept, plus a row at z 4 and z 10 across the overlap
        /// (<see cref="BOLT_KNOT"/>) - 50 to 70 cells of socket-rich junction a level. Banded in two inks
        /// striped ACROSS the jog (<see cref="BOLT_TIER_STRIPE"/>), a plate is clearable in course-shaped
        /// bites, it cuts the corkscrew's ribbons at every joint, either ink alone still bridges the jog -
        /// and clearing BOTH inks of one plate is the player's own lever to drop everything below it.
        /// </para>
        /// <para>
        /// The sweep is a helicoid about the COLUMN'S OWN axis: the angle is measured from that level's
        /// segment centre, so it steps sideways with the jog and the gradient starts again round each
        /// broken piece. <see cref="VOLT"/> runs hot filament to cold sky and the yellow start is the
        /// family's identity; the head carries all four stops in quadrants, so the family stands whole on
        /// the anchor.
        /// </para>
        /// <para>
        /// Measured (#302, after the head, the tiers and the two extra field levels): 1050 balls in 19
        /// standing groups (2.32 shots a group against 44), 45 ceiling anchors at 23.3 each (load 29.6,
        /// from 21 at 47.7 and load 64.7), margin 2, nothing alone, 0 in pairs, 0 recoloured, best single
        /// shots 7-16 %. The sag probe read the shipped shape <b>5 of 5 losing on its FIRST shot with the
        /// glass at rest</b> - one 167-238-ball corkscrew ribbon and the remainder through a line the
        /// column started ~1.9 above - and reads this one <b>1 of 5 across two independent sweeps</b>
        /// (a single hairline at exactly -1.00), the other four orders clearing the level outright.
        /// Third of the old gate watches, still standing: VOLT ends in blue where FROST's
        /// <see cref="Icicle"/> ends in navy, so read the two against the dawn dome at distance; if the
        /// cold tail muddles, reorder the family to yellow, green, blue, cyan rather than changing any hue.
        /// </para>
        /// </summary>
        private static Design Bolt() => new()
        {
            File = "Bolt.json",
            Name = "Bolt",
            Grid = BOLT_GRID,
            Depth = BOLT_DEPTH,
            FieldLevels = BOLT_FIELD_LEVELS,
            Scene = SPECTRUM_SCENE,
            Sky = SPECTRUM_SKY,
            Music = MUSIC_SPECTRUM,
                Balls = BALLS_SPECTRUM,
            Shots = 44,
            CeilingStep = 6,
            OccupiedBlock = (x, z, i, depth) => BoltOccupied(x, z, LevelsBelowGlass(i, depth)),

            //BlockColour is not handed the layout depth - the raw frame was written for a bitmap, which has
            //none - so the sweep's own axis is measured off BOLT_DEPTH, which is the figure Depth above is
            //set from. Stating it twice is what the constant is for.
            BlockColour = (x, z, i) => BoltColour(x, z, LevelsBelowGlass(i, BOLT_DEPTH)),
        };

        //THE BOLT'S OWN FIGURES. Thirty levels in a field of 34: the offset is 4 and even. The column is
        //drawn in the RAW frame like the Quarry's blocks, because a jog is a statement about indices.
        //The field grew from 32 with #302's tiers, and the two levels are the fix's other half: at 32 the
        //column's lowest ball started ~1.9 over the line, and both of the probe's remaining losing orders
        //were an early 50-70-ball release dipping the still-full column ~3 - dead before the tiers could
        //matter. Thirty-four starts it ~3.3 over, which is what those same dips clear.
        private const byte BOLT_GRID = 15;
        private const byte BOLT_DEPTH = 30;
        private const byte BOLT_FIELD_LEVELS = 34;

        //The cross-section: five wide about the segment centre, z 5..9 about z 7, corners cut.
        private const int BOLT_HALF = 2;
        private const int BOLT_Z_LO = 5;
        private const int BOLT_Z_MID = 7;
        private const int BOLT_Z_HI = 9;

        //Six levels a segment and the top THREE of each lower segment given over to its elbow plate; the
        //knot reaches one column past the overlap and one row past the section, at z 4 and z 10.
        //
        //THREE AND NOT TWO, and the level had to be hung in the running game to find out. Every gate here
        //passed at two - nothing floated, nothing stood alone, no colour took the cluster - and the level
        //still LOST ITSELF in 1.1 seconds with no shot fired, a ball crossing the death line at -7.84
        //against -7.50. A jog puts the whole of the column below an elbow on that elbow's plate, and a
        //two-level plate under nine hundred balls is a hinge rather than a gusset: the links exist, which
        //is exactly why the disconnection gate is happy, and there are too few of them to carry the weight.
        //That is the Trellis's own finding (docs/formats-and-tools.md: "the gate that says the links exist
        //cannot say there are enough of them") arriving on a different shape, and the only thing that finds
        //it is hanging the level unshot.
        private const int BOLT_SEGMENT = 6;
        private const int BOLT_PLATE = 3;
        private const int BOLT_KNOT = 1;

        //Where each segment stands, top to bottom - x 7 is the field's own centre and every jog is THREE
        //columns, so the extremes are x 2 and x 12 of 15 and the margin is a clear column either side.
        //
        //THE JOG IS THE OTHER HALF OF THE SAG. It ran 7, 10, 5, 9, 6 first, which is a jog of FIVE between
        //the second and third segments - and a section reaching two columns either side of its centre has
        //no overlap at all across a jog of five, so those two segments met nowhere and the plate between
        //them was carrying the join on its own. Held to three, consecutive footprints share two whole
        //columns and the plate braces a joint that is already made; the zigzag reads as it did (right,
        //left, left, right rather than a metronome), and the extremes move in by one column.
        private static readonly int[] BOLT_CENTRES = { 7, 10, 7, 4, 7 };

        //Turns of the fold a level about the segment's own axis. 0.04 is the pre-authorised drop if the
        //two-cell wedges come out under the lonely-ball floor.
        private const float BOLT_PITCH = 0.05f;

        //THE HEAD (#302): two levels of chamfered 7x7 plate at the crown, the bolt's own hex head. It is
        //the tier the sag asked for put where a tier buys the most - at the anchor. The shank's 21-cell
        //section was the whole bond to the glass, 47.7 balls a cell with 1002 hanging, and the probe's
        //baseline lost all five orders ON THE FIRST SHOT with the glass at rest: one wedge release of
        //167-238 balls (see BOLT_TIER_STRIPE for why they were that size) and the remainder stretched
        //24-38 balls under a line the whole column starts only ~1.9 above. The head more than doubles the
        //anchor row and carries ALL FOUR stops, so the block's own rule - every stop of the family stands
        //on the top level - holds on the head literally. In QUADRANTS, one stop each, and not in dither
        //blocks, which were measured first: 2x2 blocks of four inks over a chamfered 7x7 came out as 41
        //standing groups for the whole level (1.07 shots a group - unfinishable arithmetic) with three
        //2-ball fragments on the chamfer for the repair pass to churn on; four quadrants are four clean
        //course-bites of ~22, and the centre row and column fall to the low side the way Pylon's cap
        //splits.
        private const int BOLT_HEAD_LEVELS = 2;
        private const int BOLT_HEAD_HALF = 3;

        //THE TIERS (#302): the elbow plates, recoloured. They were swept with the corkscrew like the
        //shank, and that is what made the baseline's releases enormous - a stop's wedge welded THROUGH
        //every plate into one ribbon spanning segments, largest standing groups 110-237 of 1002. Banded
        //as stripes this wide along z, two inks a plate, full plate height, the plates become
        //the shootable tiers the owner asked for: a stripe is a course-shaped bite (~40-60 balls), the
        //corkscrew is cut at every joint (a wedge is one segment's now), and the joint stays braced on
        //the other ink when one is cleared. The pairs alternate per plate - odd plates carry the family's
        //ends-of-halves {yellow, cyan}, even ones {green, blue} - so no colour can bridge two joints and
        //the longest same-colour chain is wedge + stripe + wedge across ONE plate. Severing a joint
        //outright means clearing BOTH its inks, which is never one shot, so the one-shot gate cannot see
        //it; done deliberately it drops everything below the joint, which is the fast mass removal the
        //report asked for, chosen rather than suffered.
        //
        //ALONG Z AND NOT ALONG X, and the drop test priced both mistakes before this stuck: a jog is an
        //x-affair, so the two footprints overlap in exactly TWO x-columns - and an x-stripe therefore owns
        //that whole overlap whatever its width (at x/4 the owning ink's best single shot read 82 %, at x/2
        //it read 84 %: removing it left the plate's side chunks with no path across the jog but a single
        //parity-dependent diagonal, and everything below orphaned). A z-stripe runs the plate's full x
        //span, overlap included, so either ink alone still bridges the jog and severing is strictly a
        //both-inks affair - never one shot, invisible to the one-shot gate, and the deliberate two-ink
        //unscrewing the report asked for when the player wants a joint gone.
        private const int BOLT_TIER_STRIPE = 2;

        //The two tier pairs, stated once: interleaving the family's halves keeps a tier's two inks far
        //apart in hue - a tier reads as hardware against the gradient, not as two more of its stops.
        private static readonly BallType[] BOLT_TIER_ODD = { VOLT[0], VOLT[2] };    //yellow, cyan
        private static readonly BallType[] BOLT_TIER_EVEN = { VOLT[1], VOLT[3] };   //green, blue

        /// <summary>Which column this level's segment stands on, plates included - they belong to the segment BELOW.</summary>
        private static int BoltCentre(int d) =>
            BOLT_CENTRES[Math.Min(d / BOLT_SEGMENT, BOLT_CENTRES.Length - 1)];

        private static bool BoltOccupied(int x, int z, int d)
        {
            int xc = BoltCentre(d);

            //The head: a chamfered plate about the top segment's centre, BOLT_HEAD_HALF each way with the
            //four extreme corners cut the way the shank's are
            if (d < BOLT_HEAD_LEVELS)
                return Math.Abs(x - xc) <= BOLT_HEAD_HALF && Math.Abs(z - BOLT_Z_MID) <= BOLT_HEAD_HALF
                       && !(Math.Abs(x - xc) == BOLT_HEAD_HALF && Math.Abs(z - BOLT_Z_MID) == BOLT_HEAD_HALF);

            //An elbow plate: the lower segment's top BOLT_PLATE levels, spanning both footprints with the
            //corners KEPT, plus the knot's two rows across the overlap
            if (d >= BOLT_SEGMENT && d % BOLT_SEGMENT < BOLT_PLATE)
            {
                int up = BoltCentre(d - BOLT_SEGMENT);
                int lo = Math.Min(xc, up), hi = Math.Max(xc, up);

                if (z == BOLT_Z_LO - 1 || z == BOLT_Z_HI + 1)
                    return x >= lo - BOLT_KNOT && x <= hi + BOLT_KNOT;

                return x >= lo - BOLT_HALF && x <= hi + BOLT_HALF && z >= BOLT_Z_LO && z <= BOLT_Z_HI;
            }

            if (x < xc - BOLT_HALF || x > xc + BOLT_HALF || z < BOLT_Z_LO || z > BOLT_Z_HI) return false;

            //The chamfer: the four corners of the square, which is what takes the section from 25 to 21
            return Math.Abs(x - xc) != BOLT_HALF || Math.Abs(z - BOLT_Z_MID) != BOLT_HALF;
        }

        private static BallType BoltColour(int x, int z, int d)
        {
            //The head: one stop a quadrant, all four on the anchor - see BOLT_HEAD_LEVELS for why not blocks
            if (d < BOLT_HEAD_LEVELS)
                return VOLT[(x > BOLT_CENTRES[0] ? 1 : 0) + (z > BOLT_Z_MID ? 2 : 0)];

            //A tier: stripes of the plate's ink pair running ACROSS the jog - see BOLT_TIER_STRIPE for why z
            if (d >= BOLT_SEGMENT && d % BOLT_SEGMENT < BOLT_PLATE)
                return Band(z / BOLT_TIER_STRIPE,
                    (d / BOLT_SEGMENT) % 2 == 1 ? BOLT_TIER_ODD : BOLT_TIER_EVEN);

            //Measured from the level's OWN segment centre, which is what makes the gradient corkscrew round
            //each broken piece rather than round a line the column left behind three jogs ago
            float ang = MathF.Atan2(z - BOLT_Z_MID, x - BoltCentre(d));

            return Fold(ang / MathF.Tau + BOLT_PITCH * d, VOLT);
        }

        /// <summary>
        /// Five garnet candles of five different lengths hanging from one crown: cut a candle's arm and the
        /// whole fixture rocks. The pack's best player agency - the player chooses which candle to
        /// extinguish, in what order, against the ceiling clock - and every extinguishing visibly
        /// rebalances what is left.
        /// <para>
        /// <b>The crown is a solid disc bonded to the glass across its whole area</b>
        /// (<see cref="GIRANDOLE_CROWN"/>, d 0..1), and under it two arm levels: a hub plus an annulus
        /// which exists only within <see cref="GIRANDOLE_ARM_HALF"/> of each spire's bearing. The middle
        /// stays open below - there is no central spire - so the thing reads as a fixture rather than as a
        /// chandelier-shaped solid.
        /// </para>
        /// <para>
        /// <b>The arms carry the grafted weld knot</b>, which is Shackle's one great idea applied to the
        /// design whose whole risk lives in its junctions: the annulus is widened to
        /// <see cref="GIRANDOLE_ARM_IN"/>..<see cref="GIRANDOLE_ARM_OUT"/> across 26 degrees rather than 20,
        /// so each arm level is 14 to 18 cells welding upward into the full crown disc.
        /// </para>
        /// <para>
        /// Each spire is a solid column of plan radius <see cref="GIRANDOLE_ROOT"/> tapering to
        /// <see cref="GIRANDOLE_TIP"/> - above the 1.3 mush limit, <see cref="Column"/>'s precedent for
        /// hanging a solid column - standing at <see cref="GIRANDOLE_RING"/> off the axis, and the five run
        /// <see cref="GIRANDOLE_LENGTHS"/> levels. Below the arms they are deliberately independent of one
        /// another: that is the physics. The sweep is the plain helicoid, so a spire sits at a fixed bearing
        /// and burns DOWN the family, changing stop about every three levels into rings of some twenty
        /// balls, and the five bearings are a fifth of a turn apart so no two candles show one colour at one
        /// height.
        /// </para>
        /// <para>
        /// Gate watch (#255), in the spec's order: THE UNSHOT SAG TEST ON THE ARMS first and before
        /// anything else - a two-level wedge carrying a 24-level solid spire is this design's entire risk
        /// budget, and the knots are a countermeasure rather than a proof. If it sags even so, deepen the
        /// arms to three levels (push the spires to d 5, GIRANDOLE_DEPTH 29 with GIRANDOLE_FIELD_LEVELS 33,
        /// which keeps the offset even) before touching a spire length. Second: confirm a dropped 24-level
        /// spire stays under the drop gate - it is about a fifth of the cluster, so it will, but run it.
        /// Third: check that no two ADJACENT candles wear one stop at one height, or the drop test can
        /// chain them through the crown. And one the spec did not flag: at ROOT 1.9 the five discs stand
        /// 4.47 apart centre to centre and can KISS through a lattice diagonal near their tops, which would
        /// weld the fixture into one body and take the level's whole physics moment with it - if the report
        /// shows one giant group where five were drawn, push GIRANDOLE_RING to 4.2, which the margin still
        /// carries.
        /// </para>
        /// </summary>
        private static Design Girandole() => new()
        {
            File = "Girandole.json",
            Name = "Girandole",
            Grid = GIRANDOLE_GRID,
            Depth = GIRANDOLE_DEPTH,
            FieldLevels = GIRANDOLE_FIELD_LEVELS,
            Scene = SPECTRUM_SCENE,
            Sky = SPECTRUM_SKY,
            Music = MUSIC_SPECTRUM,
                Balls = BALLS_SPECTRUM,
            Shots = 54,
            CeilingStep = 5,
            Occupied = GirandoleOccupied,
            Colour = GirandoleColour,
        };

        //THE GIRANDOLE'S OWN FIGURES. Twenty-eight levels in a field of 32: the offset is 4 and even.
        private const byte GIRANDOLE_GRID = 15;
        private const byte GIRANDOLE_DEPTH = 28;
        private const byte GIRANDOLE_FIELD_LEVELS = 32;

        //The crown, its two arm levels, and the hub in the middle of them.
        private const int GIRANDOLE_CROWN_LAST = 1;
        private const int GIRANDOLE_ARM_LAST = 3;
        private const float GIRANDOLE_CROWN = 4.6f;
        private const float GIRANDOLE_HUB = 1.6f;

        //The grafted knot: the annulus each arm cuts out of its two levels, and how far either side of its
        //spire's bearing it reaches. 26 degrees rather than the drawn 20, 2.8..5.0 rather than 3.0..4.8.
        private const float GIRANDOLE_ARM_IN = 2.8f;
        private const float GIRANDOLE_ARM_OUT = 5.0f;
        private const float GIRANDOLE_ARM_HALF = 0.454f;

        //The candles: five of them on a ring of RING, starting at d 4, ROOT across at the arm and tapering
        //to TIP at whatever level each one ends on. RING is the one number to move if the five weld into
        //one body - see the design's doc.
        private const int GIRANDOLE_SPIRES = 5;
        private const int GIRANDOLE_SPIRE_FIRST = 4;
        private const float GIRANDOLE_RING = 3.8f;
        private const float GIRANDOLE_ROOT = 1.9f;
        private const float GIRANDOLE_TIP = 1.6f;

        //Five lengths, so five fates and five different silhouettes as the level comes down: tips at
        //d 27, 19, 23, 27 and 19. Two long, two short and one between is what keeps the fixture asymmetric
        //whichever candle goes first.
        private static readonly int[] GIRANDOLE_LENGTHS = { 24, 16, 20, 24, 16 };

        //Turns of the fold a level: one stop about every three levels, which is a ring of some twenty balls
        //on a spire and a wedge of the crown up top.
        private const float GIRANDOLE_PITCH = 0.04f;

        /// <summary>The bearing of whichever of the five spires this angle is nearest.</summary>
        private static float GirandoleSeat(float ang)
        {
            float step = MathF.Tau / GIRANDOLE_SPIRES;

            return MathF.Round(ang / step) * step;
        }

        /// <summary>Whether the cell is inside spire <paramref name="k"/> at this height.</summary>
        private static bool GirandoleSpire(float r, float ang, int d, int k)
        {
            int length = GIRANDOLE_LENGTHS[k];
            if (d >= GIRANDOLE_SPIRE_FIRST + length) return false;

            float seat = k * MathF.Tau / GIRANDOLE_SPIRES;
            float taper = (d - GIRANDOLE_SPIRE_FIRST) / (length - 1f);

            return PlanDistance(r, ang, GIRANDOLE_RING, seat)
                   <= GIRANDOLE_ROOT + (GIRANDOLE_TIP - GIRANDOLE_ROOT) * taper;
        }

        private static bool GirandoleOccupied(float r, float ang, int i, int depth)
        {
            int d = LevelsBelowGlass(i, depth);

            if (d <= GIRANDOLE_CROWN_LAST) return r <= GIRANDOLE_CROWN;

            if (d <= GIRANDOLE_ARM_LAST)
            {
                if (r <= GIRANDOLE_HUB) return true;
                if (r < GIRANDOLE_ARM_IN || r > GIRANDOLE_ARM_OUT) return false;

                return MathF.Abs(WrapAngle(ang - GirandoleSeat(ang))) <= GIRANDOLE_ARM_HALF;
            }

            for (int k = 0; k < GIRANDOLE_SPIRES; k++)
                if (GirandoleSpire(r, ang, d, k)) return true;

            return false;
        }

        //The plain helicoid about the fixture's own axis, so a candle standing at a fixed bearing burns
        //down the family and the crown above it shows all four stops at once.
        private static BallType GirandoleColour(float r, float ang, int i, int depth) =>
            Fold(ang / MathF.Tau + GIRANDOLE_PITCH * LevelsBelowGlass(i, depth), GARNET);

        /// <summary>
        /// An aurora curtain folded in plan: <b>the colour folds exactly where the wall creases</b>, then
        /// shears diagonally as it falls. The block's own law - a sweep runs out through the family and
        /// back, never wrapping - built here as architecture, so the player can stand at a crease and watch
        /// the gradient physically turn around. The chapter's most tell-a-friend idea and its riskiest
        /// build, which is why it sits last and behind the sternest gate.
        /// <para>
        /// <b>The wall is a plan polyline</b> (<see cref="PLEAT_X"/>, <see cref="PLEAT_Z"/>): a W of three
        /// equal panels, each (3, 6) and 6.71 cells long, thickened to <see cref="PLEAT_THICK"/> - about
        /// two cells - and run the full height. <b><see cref="PLEAT_FOLDS"/> fold periods over three
        /// panels</b> is the whole trick: half a period a panel means each panel sweeps the entire family
        /// once and the sweep reverses at t = 1/3 and t = 2/3, which is exactly where the wall creases.
        /// The <see cref="PLEAT_PITCH"/> per level then shears the pattern sideways, so luminous diagonals
        /// rake across the pleats instead of standing square on them.
        /// </para>
        /// <para>
        /// <b>Everything else about the silhouette is a countermeasure to the curtain-sag death mode</b> -
        /// a free-hanging two-thick wall is what killed the first Ziggurat, and this level is the block's
        /// purest case of it. The two creases are full-height folded-plate stiffeners by construction; the
        /// header is widened to <see cref="PLEAT_HEADER"/> (the grafted eave shoulder, 1.8 rather than the
        /// drawn 1.5, so the top course welds to the glass on a genuinely wider foot and both crease columns
        /// inherit double-width anchors); the two end posts run the full height so no panel is ever a free
        /// sheet edge; and since #302 the wall is twenty levels rather than twenty-six (see
        /// <see cref="PLEAT_DEPTH"/> for the probe traces) and carries <b>two shootable belts</b> - the
        /// owner's asked-for tiers - banded navy/magenta over guard courses (<see cref="PLEAT_TIER_FIRST"/>,
        /// <see cref="PleatGuardCourse"/>), so a fold strip ends at a belt by construction, a course can be
        /// taken as a course, and severing a belt drops everything under it as a designed cut.
        /// </para>
        /// <para>
        /// Measured after #302 (sag probe, three independent sweeps): <b>0 of 5 losing orders, every order
        /// clearing the level outright</b> at shots 10-18 of 48, worst transient dip -0.05 (inside the 1.00
        /// allowance) - against the shipped curtain's 5 of 5 with deaths at shots 1-5, mostly glass at rest,
        /// off single releases of 133-334 balls. Now 1172 balls in 35 standing groups; the naive ratio is
        /// 1.37 shots a group, and it is not the figure to price this level by: the belts' designed cuts
        /// cascade (orphan drops of 34-141 a shot in the probe's own clears), which is why the probe
        /// finishes on a third of the budget. Best single shots 4-13 %, anchor load 17.1, margin 1, nothing
        /// alone, 0 recoloured. t is the arclength along the WHOLE polyline and not per segment (see
        /// <see cref="PleatWall"/>) - a per-segment t puts a colour tear at each crease, exactly where the
        /// design promises a fold. The wall's occupancy per level still bears watching if the polyline
        /// moves: a 1.0 predicate can pinch where it runs diagonally through cell centres; the drawing puts
        /// three cells on the even rows and two on the odd ones, which is clear of a pinch.
        /// </para>
        /// </summary>
        private static Design Pleat() => new()
        {
            File = "Pleat.json",
            Name = "Pleat",
            Grid = PLEAT_GRID,
            Depth = PLEAT_DEPTH,
            FieldLevels = PLEAT_FIELD_LEVELS,
            Scene = SPECTRUM_SCENE,
            Sky = SPECTRUM_SKY,
            Music = MUSIC_SPECTRUM,
                Balls = BALLS_SPECTRUM,
            Shots = 48,
            CeilingStep = 4,
            OccupiedBlock = (x, z, i, depth) => PleatOccupied(x, z, LevelsBelowGlass(i, depth)),

            //As with the Bolt: BlockColour is not handed the layout depth, so the sweep's axis is measured
            //off PLEAT_DEPTH - the same figure Depth above is set from.
            BlockColour = (x, z, i) => PleatColour(x, z, LevelsBelowGlass(i, PLEAT_DEPTH)),
        };

        //THE PLEAT'S OWN FIGURES. Twenty levels in a field of 30: the offset is 10 and even. Drawn in the
        //RAW frame, because a wall is a plan drawing and not a solid of revolution.
        //
        //TWENTY AND NOT THE TWENTY-SIX IT SHIPPED WITH (#302). The sag probe's trace on the shipped curtain
        //is unambiguous: single releases of 133-334 balls - a fold stop's strip winds the full height, so
        //one colour is a diagonal ribbon of 10-23 % of the level - and after any one of them the gashed
        //remainder stretched past the death line, mostly with the glass at rest (5 of 5 losing orders,
        //deaths at shots 1-5). A curtain is the block's purest case of #301's one-cell-wall finding: the
        //wall is a sheet of BallSocket chains and every link yields a little under sustained load, so its
        //height IS its stretch. Six levels off the bottom shortens every strip by a quarter, sheds ~330
        //balls of the mass doing the stretching, and starts the foot 4.24 higher over the line. The fold
        //still reads: 1.5 periods run along the POLYLINE (the horizontal axis), and twenty levels of
        //0.04-pitch shear still rake the diagonals across all three panels.
        private const byte PLEAT_GRID = 15;
        private const byte PLEAT_DEPTH = 20;
        private const byte PLEAT_FIELD_LEVELS = 30;

        //The plan polyline, corner by corner: (2,4) to (5,10) to (8,4) to (11,10). Three EQUAL panels, which
        //is what lets the arclength be read as (panel + fraction) / panels rather than measured.
        private static readonly float[] PLEAT_X = { 2f, 5f, 8f, 11f };
        private static readonly float[] PLEAT_Z = { 4f, 10f, 4f, 10f };

        //The wall's own thickness, the header's (grafted from 1.5, so the top course welds wider), the
        //hem's, and the end posts' - each a plan distance to the polyline. The hem sits at 14..15 of the
        //twenty-level wall - the lower third, where it always was proportionally (12..13 of 26) - and since
        //#302 it is the SECOND BELT: same thickness it always had, banded like the tier above it, so it
        //costs nothing it was not already paying.
        private const float PLEAT_THICK = 1.0f;
        private const float PLEAT_HEADER = 1.8f;
        private const int PLEAT_HEADER_LAST = 1;
        private const float PLEAT_HEM = 1.4f;
        private const int PLEAT_HEM_FIRST = 14;
        private const int PLEAT_HEM_LAST = 15;
        private const float PLEAT_POST = 1.5f;

        //THE SHOOTABLE TIER (#302, the owner's own ask): a two-level belt at upper-mid height, banded in
        //blocks of the family's two END stops - horizontal courses a player can clear as courses, so a
        //slanted strip is no longer the only way to take mass off the curtain. It is deliberately NOT a
        //floor of flat colour (the block's stated trap): six blocks of two interleaved inks means no single
        //ball takes the course, the Arcade cap rule arriving on a curtain. Structurally it is a second hem
        //(PLEAT_HEM thick), so the belt braces the wall it interrupts.
        //
        //⚠ THE TIER ALSO CUTS EVERY STRIP, and the cut is guaranteed by PLEAT_TIER_SHIFT rather than hoped
        //for: the sweep below the belt is phased half a wedge on - 1/(2*4) of a turn - so a stop's strip
        //arriving at the belt from above never continues in its own ink below it, whatever block it lands
        //on. Without the shift a strip bridges wherever its crossing happens to hit a same-ink block (the
        //diagonal-fuse trap of #301, in one dimension). Measured on the shipped wall, the strips were the
        //whole disease: 133-334 balls a release; cut at both belts they shrink to a band's height.
        //
        //Navy and magenta, the family's two ends, because the belt has to read as a BELT against the flow -
        //the mid-family greens would read as more aurora. Inside the belt the two ends alternate as blocks,
        //a contact the fold itself never makes, and the one place this level trades the family-neighbours
        //law for architecture; the block header carries the resolution.
        private const int PLEAT_TIER_FIRST = 8;
        private const int PLEAT_TIER_LAST = 9;
        private const int PLEAT_TIER_BLOCKS = 6;
        private static readonly float PLEAT_TIER_SHIFT = 1f / (2f * AURORA.Length);
        private static readonly BallType[] PLEAT_TIER_INKS = { BallType.Type12, BallType.Type6 };   //navy, magenta

        //⚠ THE GUARD COURSES, and they are what makes the belts CUT rather than WELD - measured, not
        //reasoned: without them the probe read 3 of 5 with releases of 104-211 balls, because a belt block
        //spans a sixth of the polyline while a sweep wedge spans about a twelfth, so the course above a
        //navy block crosses navy somewhere over the block's span every other time - and the block welded
        //its strip above to its strip below into exactly the mega-release the belts exist to prevent. The
        //one course each side of each belt therefore pulls the family's two ends one step in (navy reads
        //green, magenta reads cyan): a belt ink has nothing of its own colour to touch vertically, so every
        //strip ends AT a belt and every belt block is its own group, by construction rather than by luck.
        //Visually the aurora narrows to its middle hues as it approaches a belt, which reads as the belt
        //absorbing the poles. Every contact this leaves is ramp-adjacent except the braid inside the belts.
        private static bool PleatGuardCourse(int d) =>
            d == PLEAT_TIER_FIRST - 1 || d == PLEAT_TIER_LAST + 1
            || d == PLEAT_HEM_FIRST - 1 || d == PLEAT_HEM_LAST + 1;

        //How far round each end of the polyline the post reaches. A post is the header's treatment run the
        //whole height, so the two free vertical edges of the curtain are the two stiffest things in it.
        private const float PLEAT_POST_REACH = 1.5f;

        //THE FOLD, IN PANELS. 1.5 periods over three equal panels is half a period each, so a panel sweeps
        //the family once and the sweep turns round AT the crease rather than somewhere along a panel. It is
        //the one number here that may not move without moving the wall with it.
        private const float PLEAT_FOLDS = 1.5f;
        private const float PLEAT_PITCH = 0.04f;

        /// <summary>
        /// How far the cell stands from the wall's plan polyline, and - through <paramref name="t"/> - how
        /// far along it, 0 at the first corner and 1 at the last. <b>Both answers are taken over the WHOLE
        /// polyline in one pass</b>, which is the gate's second check: a per-segment reading gives a crease
        /// cell two different positions and puts a colour tear exactly where the design promises the fold.
        /// The panels are equal by construction, so (panel + fraction) / panels IS the normalised
        /// arclength and nothing has to be measured.
        /// </summary>
        private static float PleatWall(int x, int z, out float t)
        {
            int panels = PLEAT_X.Length - 1;
            float best = float.MaxValue;

            t = 0f;

            for (int p = 0; p < panels; p++)
            {
                float ax = PLEAT_X[p], az = PLEAT_Z[p];
                float bx = PLEAT_X[p + 1] - ax, bz = PLEAT_Z[p + 1] - az;

                //The projection onto the panel, CLAMPED to it, so a cell off the end of one answers to the
                //corner itself rather than to a point out beyond it
                float u = ((x - ax) * bx + (z - az) * bz) / (bx * bx + bz * bz);
                u = MathF.Min(MathF.Max(u, 0f), 1f);

                float dx = x - (ax + u * bx), dz = z - (az + u * bz);
                float dist = MathF.Sqrt(dx * dx + dz * dz);

                if (dist >= best) continue;

                best = dist;
                t = (p + u) / panels;
            }

            return best;
        }

        /// <summary>Whether the cell stands within the post's reach of the polyline's end <paramref name="corner"/>.</summary>
        private static bool PleatEndPost(int x, int z, int corner)
        {
            float dx = x - PLEAT_X[corner], dz = z - PLEAT_Z[corner];

            return dx * dx + dz * dz <= PLEAT_POST_REACH * PLEAT_POST_REACH;
        }

        private static bool PleatOccupied(int x, int z, int d)
        {
            float dist = PleatWall(x, z, out _);

            //The two end posts run the FULL height at the header's own thickness - the curtain's free
            //vertical edges, and the first thing a sag test leans on
            if (dist <= PLEAT_POST
                && (PleatEndPost(x, z, 0) || PleatEndPost(x, z, PLEAT_X.Length - 1)))
                return true;

            float thickness =
                d <= PLEAT_HEADER_LAST ? PLEAT_HEADER
                : d >= PLEAT_TIER_FIRST && d <= PLEAT_TIER_LAST ? PLEAT_HEM
                : d >= PLEAT_HEM_FIRST && d <= PLEAT_HEM_LAST ? PLEAT_HEM
                : PLEAT_THICK;

            return dist <= thickness;
        }

        //The sweep rides the WALL'S own coordinate rather than an angle: 1.5 folds along it, sheared by the
        //pitch as it falls. The creases are at t = 1/3 and 2/3, where 1.5 * t is exactly half and one whole
        //fold period - which is what makes the colour turn round where the wall does. The two belts take
        //their own banding - the lower one's blocks stepped one along, so no belt column wears the same ink
        //twice - and the sweep is phased half a wedge on below EACH belt, so a strip is cut at both; see
        //PLEAT_TIER_FIRST for why both halves of that sentence are load-bearing.
        private static BallType PleatColour(int x, int z, int d)
        {
            PleatWall(x, z, out float t);

            if (d >= PLEAT_TIER_FIRST && d <= PLEAT_TIER_LAST)
                return Band((int)MathF.Floor(t * PLEAT_TIER_BLOCKS), PLEAT_TIER_INKS);

            if (d >= PLEAT_HEM_FIRST && d <= PLEAT_HEM_LAST)
                return Band((int)MathF.Floor(t * PLEAT_TIER_BLOCKS) + 1, PLEAT_TIER_INKS);

            float shift = PLEAT_TIER_SHIFT * ((d > PLEAT_TIER_LAST ? 1 : 0) + (d > PLEAT_HEM_LAST ? 1 : 0));

            BallType ink = Fold(PLEAT_FOLDS * t + PLEAT_PITCH * d + shift, AURORA);

            //The guard courses pull the family's ends one step in - see PleatGuardCourse for the measured
            //reason this is load-bearing and not a colour preference
            if (PleatGuardCourse(d))
                ink = ink == BallType.Type12 ? BallType.Type2    //navy -> green
                    : ink == BallType.Type6 ? BallType.Type5     //magenta -> cyan
                    : ink;

            return ink;
        }

        #endregion


        #region The spectrum levels' own geometry (#253)

        //THE BLOCK'S SETTING, stated once and named on all five designs so DescribeBlock has nothing to
        //report. THE CITY, under a morning dome - and it is deliberately the SAME city the Arcade plays in,
        //which is the one scene pairing in the campaign that repeats a place on purpose. The light ramp's
        //return completes here: after the volcano's geological glow (#295) this is light RECEIVED again,
        //and the strongest way to say so is this skyline at dawn with its neon off. Since #300 the dawn
        //comes BEFORE the neon - the pairing reads as one day in the city, morning here, the Arcade's made
        //light after dark closing the campaign - where it used to close it itself as the morning after the
        //Arcade's night. The arena has always stood here either way.
        //
        //IT IS ALSO THE BACKDROP THIS BLOCK NEEDS, and that was settled by looking rather than by argument.
        //A chapter whose whole subject is telling neighbouring hues apart cannot be played in front of a
        //backdrop that tints them, and the sea - the obvious candidate, the one scene of the original seven
        //the campaign has never used - is the worst of them for exactly the reason it looked right: its
        //water MIRRORS the dome, so the sky's hue fills the top of the frame and the bottom of it too. Shot
        //under four domes it came out one colour throughout every time, and under dome 4 (this block's first
        //pick) it was a magenta sky over a magenta sea - the trap the record already holds against the dream
        //scene and magenta balls. The city's facades are the largest low-chroma surface in the game and its
        //ground is grey, so only the top third of the frame carries the dome's colour at all.
        //
        //DOME 11 was chosen over 1, 3 and 9 on the same shots - the Icicle and the Hourglass, the block's
        //coolest family and its most fragile one, in each. 1 and 3 put a cyan and a lavender sky behind a
        //blue-and-cyan cluster and behind a magenta one respectively, each swallowing the family it was
        //supposed to set off; 9 is a dusk, which reads as a step back into the dark rather than as light
        //returning. 11 is a morning - a blue zenith with warm cirrus over a sandstone city - and is the one
        //of the four where all five families stood clear of what was behind them.
        private const SceneKind SPECTRUM_SCENE = SceneKind.City;
        private const byte SPECTRUM_SKY = 11;

        //THE FIVE FAMILIES. Each is a SUBSET AND AN ORDERING of the fixed thirteen and never a new hue
        //(#253) - see the block's own comment for why the enum is not grown for a ramp. They are stated here
        //rather than inline at each design, unlike every other block's palettes, because the FAMILY is what
        //the chapter is: a level is one of these swept, and two levels differing only in which one they take
        //is the whole of the block's variety.
        private static readonly BallType[] FROST = { BallType.Type4, BallType.Type5, BallType.Type3, BallType.Type12 };
        private static readonly BallType[] HEAT = { BallType.Type4, BallType.Type7, BallType.Type9, BallType.Type1, BallType.Type10 };
        private static readonly BallType[] MOSS = { BallType.Type4, BallType.Type2, BallType.Type13, BallType.Type8 };
        private static readonly BallType[] TWILIGHT = { BallType.Type4, BallType.Type6, BallType.Type12, BallType.Type8 };

        //The wheel, in hue order rather than in enum order, which is the one place in this file where the
        //ORDER of a palette is the design. Seven of the thirteen: the six that sit on the wheel at even
        //spacings plus orange between red and yellow, so no two neighbours here are further apart than the
        //#152 rival pairs already are.
        private static readonly BallType[] SPECTRUM =
        {
            BallType.Type1, BallType.Type9, BallType.Type7, BallType.Type2, BallType.Type5, BallType.Type3,
            BallType.Type6,
        };

        /// <summary>
        /// The block's one colour rule: a position along a family, in <b>stops</b>, turned into the colour
        /// standing at that position - and turned back again past the far end, so a sweep runs out through
        /// the family and home again rather than jumping from its last colour to its first.
        /// <para>
        /// <b>The fold is the owner's brief and not a nicety</b>: the seed example sweeps white to dark blue
        /// <i>and back to white</i> as it climbs. Wrapped instead, every period would put navy against white
        /// along one hard seam, which is the one boundary in the family that does not read as a gradient at
        /// all. Folded, the only colours that ever touch are neighbours in the ramp.
        /// </para>
        /// <para>
        /// What it costs is an even colour count: a fold of <c>n</c> stops has period <c>2n − 2</c> and hits
        /// the two ENDS once against the middles' twice, so white and navy come out at roughly half the
        /// count of cyan and blue. That is visible in every one of these levels' reports and is left alone -
        /// a family's ends are its extremes and being rarer is what makes them read as the ends of something.
        /// </para>
        /// </summary>
        private static BallType Sweep(float stops, BallType[] family)
        {
            int span = 2 * family.Length - 2;
            int stop = (int)MathF.Floor(stops);
            int phase = ((stop % span) + span) % span;

            return family[phase < family.Length ? phase : span - phase];
        }

        /// <summary>
        /// A triangle wave of <paramref name="x"/> over <paramref name="period"/>, −1 to +1 and back. The
        /// comb that cuts <see cref="Kiln"/>'s stop boundaries into teeth: added to a sweep coordinate it
        /// makes the boundary zigzag rather than run straight, so consecutive stops interlock.
        /// </summary>
        private static float Tooth(float x, float period)
        {
            float turns = x / period;
            return MathF.Abs(turns - MathF.Floor(turns) - HALF) * 4f - 1f;
        }

        //THE ICICLE. A cone hanging point-down: a wide plate against the glass drawing in evenly to a tip
        //TIP across. The plainest tall body in the block, and it opens the block for the reason Column opens
        //the Tower - a chapter states its premise with the level that has no second idea in it, and here the
        //premise is the SWEEP.
        private const byte ICICLE_GRID = 13;
        private const byte ICICLE_DEPTH = 22;
        private const byte ICICLE_FIELD_LEVELS = 32;
        private const float ICICLE_TOP = 4.2f;
        private const float ICICLE_TIP = 1.2f;

        //THE SWEEP, and the figure that decides whether this block is possible at all. PITCH is how many
        //LEVELS of sweep one full turn round the axis is worth: at 14.4 against 2.4 levels a stop, one turn
        //is exactly six stops, which is the whole folded family - so every colour of the ramp stands on the
        //TOP LEVEL, in wedges, and going once round the cone walks white to navy and back.
        //
        //THAT is the property, and it took a failed first cut to find it. Swept by a tilted PLANE instead,
        //the top level carried two or three stops out of the six and the rest existed only further down -
        //where they hang off the stop above them and nothing else. Cutting one then takes everything under
        //it: 80 % on the first draft of this level and 92 % on the Kiln's, which is a level that ends on one
        //lucky ball. A helicoid has no such orphan. Removing one stop leaves a screw thread that still
        //spirals from the tip to the glass, so the rest of the body hangs exactly as it did - measured here
        //as a best single shot of a few per cent where the plane gave eighty.
        private const float ICICLE_PER_STOP = 2.4f;
        private const float ICICLE_PITCH = 14.4f;

        private static float IcicleRadius(int i, int depth) =>
            ICICLE_TIP + (ICICLE_TOP - ICICLE_TIP) * i / (depth - 1f);

        //NO RADIAL TERM, and that is measured rather than left out. Shearing the sweep outward as well
        //(0.9 levels a cell was tried) MERGES the level instead of dividing it: the shear carries each
        //stop's ribbon across the gap to the turn of itself outside it, and the six standing groups become
        //four - one per colour, the whole level in four shots. Every other design in the block wants that
        //term; a bare cone under a bare helicoid is the one place it does the opposite of what it looks like.
        private static float IcicleSweep(float ang, int i, int depth) =>
            (LevelsBelowGlass(i, depth) + ICICLE_PITCH * (WrapAngle(ang) / MathF.Tau)) / ICICLE_PER_STOP;

        //THE KILN. A solid column whose radius breathes between WAIST and BULGE every LOBE levels, so the
        //silhouette is a stack of swellings rather than a pipe. The lobe period is odd on purpose: at an even
        //one every bulge would land on the same level parity and the lattice would round all of them off
        //identically, which reads as a repeated artefact rather than as a body.
        private const byte KILN_GRID = 11;
        private const byte KILN_DEPTH = 24;
        private const byte KILN_FIELD_LEVELS = 34;
        private const float KILN_WAIST = 1.5f;
        private const float KILN_BULGE = 3.2f;
        private const int KILN_LOBE = 5;

        //THE SWEEP: the Icicle's helicoid, with a COMB cut into it. PITCH over PER_STOP is eight stops a
        //turn, which is this family's whole folded period - the Icicle's rule, and for the Icicle's reason:
        //every stop has to stand on the top level or the ones that do not hang off the ones that do. The
        //first cut of this level swept a tilted plane instead and measured 92 %, over the gate.
        //
        //TOOTH is how many levels of sweep the boundary swings either way and TEETH how many swings there
        //are in a full turn - a swing of well over half a stop is what makes two stops actually interlock
        //rather than merely wobble at the seam. The comb runs on the ANGLE, which is what makes it a
        //statement about floors: a tooth is the colour above reaching a level down and the colour below
        //reaching a level up, in alternation round the body.
        private const float KILN_PER_STOP = 2.6f;
        private const float KILN_PITCH = 20.8f;
        private const float KILN_TOOTH = 1.7f;
        private const int KILN_TEETH = 7;

        private static float KilnRadius(int i, int depth)
        {
            float lobe = MathF.Cos(MathF.Tau * LevelsBelowGlass(i, depth) / KILN_LOBE);

            return KILN_WAIST + (KILN_BULGE - KILN_WAIST) * (HALF + HALF * lobe);
        }

        private static BallType KilnColour(float r, float ang, int i, int depth)
        {
            float local = WrapAngle(ang);

            //The comb's period divides a full turn exactly, so the teeth meet themselves at the seam the
            //helicoid steps across rather than leaving one wider or narrower tooth there
            float stops = (LevelsBelowGlass(i, depth) + KILN_PITCH * (local / MathF.Tau)
                           + KILN_TOOTH * Tooth(local, MathF.Tau / KILN_TEETH)) / KILN_PER_STOP;

            return Sweep(stops, HEAT);
        }

        //THE TRELLIS. Two ribbons - wide along the circumference, thin across it - wound in opposite
        //directions on orbits a cell and a half apart, so they touch where they pass without ever sharing a
        //cell. That separation is Garland's, and it is there for Garland's reason: two counter-turning bodies
        //on ONE orbit merge at a pass into a single disc which is the only thing on its level, and the colour
        //of that disc cuts both of them at once (85 % of the level, measured, two levels under the glass).
        //ARC_HALF is the ribbon's half-width in radians at its own orbit; at the inner orbit that is about
        //two and a half cells of arc, which is a band rather than a strand.
        private const byte TRELLIS_GRID = 13;
        private const byte TRELLIS_DEPTH = 22;
        private const byte TRELLIS_FIELD_LEVELS = 32;
        private const float TRELLIS_ORBIT_INNER = 2.3f;
        private const float TRELLIS_ORBIT_OUTER = 3.8f;
        private const float TRELLIS_HALF_THICK = 0.85f;
        private const float TRELLIS_ARC_HALF = 0.62f;
        private const float TRELLIS_TURNS_PER_LEVEL = 0.032f;

        //THE SWEEP runs along each ribbon's own length, 3.2 levels a stop, sheared along the arc as well so a
        //band crosses its ribbon on the diagonal. The two ribbons start OPPOSITE_STOPS apart - half of the
        //fold's six-stop period - so the pair facing each other across the axis are never the same colour
        //and a shot that suits one never suits the other at the same height.
        private const float TRELLIS_PER_STOP = 3.2f;
        private const float TRELLIS_SKEW = 1.1f;
        private const float TRELLIS_OPPOSITE_STOPS = 3f;

        /// <summary>Which ribbon the cell is inside, 1 (inner) or 2 (outer), or 0 for neither.</summary>
        private static int TrellisRibbon(float r, float ang, int i)
        {
            if (MathF.Abs(r - TRELLIS_ORBIT_INNER) <= TRELLIS_HALF_THICK
                && MathF.Abs(WrapAngle(ang - TrellisCentre(i, 1))) <= TRELLIS_ARC_HALF)
                return 1;

            return MathF.Abs(r - TRELLIS_ORBIT_OUTER) <= TRELLIS_HALF_THICK
                   && MathF.Abs(WrapAngle(ang - TrellisCentre(i, 2))) <= TRELLIS_ARC_HALF
                ? 2 : 0;
        }

        //Where a ribbon's middle sits at this height. The two turn OPPOSITE ways from a half-turn apart, so
        //they close at a relative half-turn: 2*TURNS_PER_LEVEL is 0.064 turns a level, which puts ONE pass
        //at level 8 of the 22 and the next one past the bottom.
        //
        //A SECOND PASS WAS TRIED AND IT LOST THE LEVEL. At 0.055 turns a level the alignments fall at levels
        //6 and 18, and the numbers all improved - green's best single shot 34 % -> 19 %, 15 standing groups
        //-> 14 - and the level then FAILED IN THE RUNNING GAME AT TEN SECONDS WITH NO SHOT FIRED, on "the
        //cluster reached the line". A ribbon that turns that fast steps its cells a whole cell and a
        //quarter sideways per level at the outer orbit, so consecutive levels barely overlap: the links
        //exist (the disconnection gate passes, which is exactly the point) but there are too few of them to
        //carry the weight, and the whole thing stretches. THE GATE THAT SAYS THE LINKS EXIST CANNOT SAY
        //THERE ARE ENOUGH OF THEM - Garland (#182) and the Ziggurat found the same wall by thickness where
        //this one found it by pitch, and only hanging the level unshot in the game finds any of them.
        private static float TrellisCentre(int i, int ribbon) =>
            ribbon == 1
                ? i * TRELLIS_TURNS_PER_LEVEL * MathF.Tau
                : MathF.PI - i * TRELLIS_TURNS_PER_LEVEL * MathF.Tau;

        private static BallType TrellisColour(float r, float ang, int i, int depth)
        {
            int ribbon = TrellisRibbon(r, ang, i);
            float arc = WrapAngle(ang - TrellisCentre(i, ribbon)) * r;

            float stops = (LevelsBelowGlass(i, depth) + TRELLIS_SKEW * arc) / TRELLIS_PER_STOP
                          + (ribbon == 1 ? 0f : TRELLIS_OPPOSITE_STOPS);

            return Sweep(stops, MOSS);
        }

        //THE HOURGLASS. A solid of revolution whose radius is a parabola in the fraction of the way down:
        //widest at both ends, WAIST at the middle. Written on the SIGNED fraction so the shape is exactly
        //symmetric about the waist - a hand-tuned pair of cones would not be, and the level's whole silhouette
        //is that symmetry.
        private const byte HOURGLASS_GRID = 13;
        private const byte HOURGLASS_DEPTH = 22;
        private const byte HOURGLASS_FIELD_LEVELS = 32;
        private const float HOURGLASS_WAIST = 1.4f;
        private const float HOURGLASS_FLARE = 2.7f;

        //THE SWEEP: height, radius AND angle at once, so a stop is a conical shell wound into a screw. RISE
        //is how many levels of sweep one cell of radius is worth; PER_STOP over RISE is therefore how wide a
        //ring comes out on any horizontal section - 2.9 cells here, deliberately over the two the lonely-ball
        //rule needs (a ring one cell wide running round the lattice is a string of balls that do not touch
        //each other, which is what the first Gem shipped 28 of).
        //
        //THE PITCH IS WHAT MAKES THE SHELLS SAFE TO CUT, and the version of this level without it is why the
        //figure is stated in that order. Nested shells with no twist in them are closed SLEEVES, and this
        //body has a waist: a sleeve round the waist is the only thing the whole lower cone hangs through, so
        //cutting one colour took 89 % of the level. Wound instead, a shell is a spiral ramp that reaches the
        //top plate at some angle, and what a cut leaves behind still hangs off it.
        private const float HOURGLASS_PER_STOP = 2.9f;
        private const float HOURGLASS_RISE = 1f;
        private const float HOURGLASS_PITCH = 17.4f;

        private static float HourglassRadius(int i, int depth)
        {
            float fromWaist = 2f * LevelsBelowGlass(i, depth) / (depth - 1f) - 1f;

            return HOURGLASS_WAIST + HOURGLASS_FLARE * fromWaist * fromWaist;
        }

        private static float HourglassSweep(float r, float ang, int i, int depth) =>
            (LevelsBelowGlass(i, depth) + HOURGLASS_RISE * r + HOURGLASS_PITCH * (WrapAngle(ang) / MathF.Tau))
            / HOURGLASS_PER_STOP;

        //THE TURBINE. A thin core with BLADES radial slabs standing off it, the whole assembly turning
        //TURNS_PER_LEVEL a level. A blade is a slab rather than a fin: BLADE_HALF is measured ACROSS the
        //blade, in cells, so the two-cell thickness the lattice needs for its cross-level neighbours holds
        //at every radius rather than only at the tip. The core is what the blades hang off each other
        //through - without it five slabs meeting at a point would be five separate bodies sharing an axis.
        private const byte TURBINE_GRID = 13;
        private const byte TURBINE_DEPTH = 20;
        private const byte TURBINE_FIELD_LEVELS = 30;
        private const int TURBINE_BLADES = 5;
        private const float TURBINE_CORE = 1.3f;
        private const float TURBINE_REACH = 4.6f;
        private const float TURBINE_BLADE_HALF = 0.8f;
        private const float TURBINE_TURNS_PER_LEVEL = 0.026f;

        //THE SWEEP is HELICAL: the angle carries PITCH levels of sweep per full turn, so at any height the
        //five blades stand at five different places along the wheel, and a blade walks the wheel as it
        //descends. PITCH against PER_STOP decides how many stops a turn shows - 8.4 over 1.5 is 5.6 stops a
        //turn, so no two blades of the five wear the same colour at the same height. The small radial term
        //tilts the boundary along each blade as well, for the reason every sweep here is tilted.
        private const float TURBINE_PER_STOP = 1.5f;
        private const float TURBINE_PITCH = 8.4f;
        private const float TURBINE_RISE = 0.35f;

        private static bool TurbineCore(float r) => r <= TURBINE_CORE;

        /// <summary>Which blade the cell is inside, 1..<see cref="TURBINE_BLADES"/>, or 0 for none.</summary>
        private static int TurbineBlade(float r, float ang, int i)
        {
            if (r > TURBINE_REACH) return 0;

            float turns = ang / MathF.Tau - i * TURBINE_TURNS_PER_LEVEL;
            int blade = (int)MathF.Round(turns * TURBINE_BLADES);
            float centre = (blade / (float)TURBINE_BLADES + i * TURBINE_TURNS_PER_LEVEL) * MathF.Tau;

            //The perpendicular distance from the blade's own axis, which is what a slab is thin in. Straight
            //angular width would be a fin - a wedge that thickens with radius and pinches to nothing at the core.
            if (MathF.Abs(r * MathF.Sin(WrapAngle(ang - centre))) > TURBINE_BLADE_HALF) return 0;

            return ((blade % TURBINE_BLADES) + TURBINE_BLADES) % TURBINE_BLADES + 1;
        }

        private static float TurbineSweep(float r, float ang, int i, int depth)
        {
            float local = WrapAngle(ang - i * TURBINE_TURNS_PER_LEVEL * MathF.Tau);

            return (LevelsBelowGlass(i, depth) + TURBINE_PITCH * (local / MathF.Tau) + TURBINE_RISE * r)
                   / TURBINE_PER_STOP;
        }

        #endregion
    }
}
