using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>The Tower</b>, block 4 of the campaign: its designs, and the helpers no other block's designs use, in
    /// the order <c>Program.cs</c> held them — the play order is <see cref="Main"/>'s, and the block's name, music
    /// and ball style are in the tables there. Split out of <c>Program.cs</c> in #386.
    /// </summary>
    internal static partial class Program
    {

        /// <summary>
        /// A crown: a hollow ring in vertical bars of colour, tapering gently wider toward the top, with six
        /// pointed teeth rising a pair of levels above the band — one per bar — each tipped with a magenta
        /// accent. The drain is still visible straight up the middle (the inner radius never closes), so a shot
        /// fired up the axis still goes clean through and the player still has to work the ring rather than
        /// spray at the centre. The teeth are the silhouette read: a plain constant-radius ring read as a
        /// napkin ring, plainer than its neighbours despite carrying more balls (#174), so the band now tapers
        /// the way <see cref="Bullseye"/> and <see cref="Prism"/> do and the teeth extend it upward, narrowing
        /// to the accent — the same shape a crown is, in the same occupancy arithmetic the other solids of
        /// revolution already use. See <see cref="CrownOccupied"/> and <see cref="CrownColour"/> for the band,
        /// the teeth and the tip.
        /// </summary>
        private static Design Crown() => new()
        {
            File = "Six.json",
            Name = "Crown",
            Grid = 15,
            Depth = 8,
            Scene = SceneKind.Mountain,
            //Dome 8, a deep violet dusk, and not the 10 this shipped with. Under 10 the peaks came out pale
            //sand against a candy-pink sky and the whole frame read as kitsch; under 8 they read as snow and
            //the sky as weather, which is the same scene doing what it was built to do. The crown's gold and
            //red carry against a dark sky, where against pink they were competing with it. Since #194 that dome
            //is the whole Tower block's, for this level's own reason.
            //
            //It USED to open the block as well, because it is the one member the camera frames whole: a hollow
            //ring teaches the axis, and the drain visible straight up the middle of it teaches why the axis
            //matters. #206 turned that round, and the argument it lost to is that the very same fact — being
            //framed whole — makes this the one level in the block that does not show what the block IS. It
            //sits second now and teaches exactly as it did; what it no longer does is stand in front of the
            //block's premise. See the play order for the whole of it.
            Sky = 8,
            Music = MUSIC_TOWER,
                Balls = BALLS_TOWER,
            Shots = 44,
            CeilingStep = 9,
            Occupied = CrownOccupied,
            Colour = CrownColour,
        };

        //Crown's shape, in layout levels (i = 0 at the bottom, depth-1 at the anchor bonded to the glass).
        //The band occupies i = 0..CROWN_BAND_TOP; the teeth rise the two levels above it, their tips on the
        //anchor level. The inner radius never closes, so the drain up the axis survives every level.
        private const int CROWN_SECTORS = 6;
        private const int CROWN_BAND_TOP = 5;
        private const float CROWN_INNER = 2.9f;
        private const float CROWN_BAND_OUTER = 5.3f;       //widest at the band's own top, tapering down from it
        private const float CROWN_BAND_TAPER = 0.15f;      //per layout level below the band top
        private const float CROWN_TOOTH_INNER = 3.0f;
        private const float CROWN_TOOTH_OUTER = 5.0f;
        private const float CROWN_TOOTH_HALFFRAC = 0.28f;  //~34° — a chunky body the tip tapers out of
        private const float CROWN_TIP_INNER = 3.3f;
        private const float CROWN_TIP_OUTER = 4.8f;
        private const float CROWN_TIP_HALFFRAC = 0.25f;    //30° — narrower than the body, reads as the point
        private static readonly BallType CROWN_ACCENT = BallType.Type6;   //magenta — not in the bar palette
        private static readonly BallType[] CROWN_BARS =
        {
            BallType.Type7, BallType.Type3, BallType.Type1,
            BallType.Type7, BallType.Type3, BallType.Type1,
        };

        /// <summary>
        /// Crown's occupancy: a gently tapering band (the ring itself, with the drain up the middle), six
        /// teeth rising above it — one per bar — and each tooth narrowing to a tip on the anchor level. The
        /// band's outer radius uses the same <c>(top − i) · taper</c> idiom <see cref="Bullseye"/> and
        /// <see cref="Prism"/> do; the teeth reuse <see cref="Sector"/>'s own <c>+0.5</c> framing through
        /// <see cref="InCrownToothWedge"/>. The tip lives on the anchor level, so it bonds straight to the
        /// glass — no tooth floats.
        /// </summary>
        private static bool CrownOccupied(float r, float ang, int i, int depth)
        {
            if (i <= CROWN_BAND_TOP)
                return r >= CROWN_INNER && r <= CROWN_BAND_OUTER - (CROWN_BAND_TOP - i) * CROWN_BAND_TAPER;

            //The tooth tip on the anchor level: the narrowest, outermost point of each tooth, drawn in the
            //accent colour by CrownColour. Kept to >=2 cells by CROWN_TIP_HALFFRAC/OUTER so it is its own
            //connected group and not a lonely ball the repair pass would recolour back into the bar.
            if (i == depth - 1)
                return r >= CROWN_TIP_INNER && r <= CROWN_TIP_OUTER && InCrownToothWedge(ang, CROWN_TIP_HALFFRAC);

            //The tooth body one level below the tip: wider, in the bar's own colour, so it extends that bar
            //upward into the tooth and the tooth reads as the bar growing into a point rather than as a
            //separate stud sat on top of the ring.
            return r >= CROWN_TOOTH_INNER && r <= CROWN_TOOTH_OUTER && InCrownToothWedge(ang, CROWN_TOOTH_HALFFRAC);
        }

        /// <summary>
        /// Crown's colour: the bar palette everywhere except the tooth tips, which take the accent — a set
        /// jewel at each point, distinct from the bar it grows out of, so the points read as deliberate
        /// marks rather than as the bar colour trailing off into the tooth.
        /// </summary>
        private static BallType CrownColour(float r, float ang, int i, int depth)
        {
            if (i == depth - 1 && InCrownToothWedge(ang, CROWN_TIP_HALFFRAC))
                return CROWN_ACCENT;

            return Sector(ang, 0f, CROWN_SECTORS, CROWN_BARS);
        }

        /// <summary>
        /// Whether <paramref name="ang"/> falls within the middle of a Crown sector — i.e. within a tooth,
        /// since one tooth sits at each sector's centre (where the bars are). <paramref name="halfFrac"/> is
        /// half the tooth's width as a fraction of the sector (0.5 would fill the whole sector, 0 the
        /// boundary line), built on <see cref="SectorIndex"/>'s own <c>+0.5</c> framing so the tooth and the
        /// bar share the same centre.
        /// </summary>
        private static bool InCrownToothWedge(float ang, float halfFrac)
        {
            float turns = (ang / MathF.Tau) + 0.5f;
            float frac = turns * CROWN_SECTORS - MathF.Floor(turns * CROWN_SECTORS);
            return frac >= 0.5f - halfFrac && frac <= 0.5f + halfFrac;
        }

        /// <summary>
        /// The first <b>tall</b> level: a column reaching up out of shot, played from the bottom as the glass
        /// brings it down. The camera frames only the lowest <c>FRAMED_LEVELS</c> of a field this deep (see
        /// <c>GameplayScreen.FRAMED_LEVELS</c>), so the level's length is its height rather than its
        /// footprint, and the player never sees the top of what they are working through.
        /// <para>
        /// <b>Narrow on purpose.</b> Nine wide against the pack's thirteen: the column is four times the
        /// depth, so at thirteen it would be well over a thousand constrained bodies, and a tall level is
        /// meant to last through its height and not through its mass. Nine also keeps the whole visible face
        /// within an easy traverse, which matters when the interesting cells are all at the bottom.
        /// </para>
        /// <para>
        /// The descent is the ordinary <c>ceilingStep</c> and no new mechanic — but it is doing a second job
        /// here. On every other level it is the pressure; on this one it is also how the level is <i>fed</i>,
        /// so it is set fast (every 5, against the 8 to 10 the gentle levels take) alongside the largest budget
        /// in the game. That coupling is the thing to watch when this is tuned: a step too slow leaves the
        /// player with nothing in reach, and too fast is a level that arrives at the death line with most of
        /// its column still overhead.
        /// </para>
        /// <para>
        /// Since #194 it belongs to the Tower block rather than standing as a lone tall level in a flat ramp,
        /// and it is that block's <b>endurance</b> beat: the largest budget, the plainest silhouette, and a
        /// colour rule that is nothing but reading what is coming. <see cref="Crown"/>, <see cref="Horn"/>,
        /// <see cref="Helix"/> and <see cref="Lean"/> are each tall — or, in Crown's case, deliberately not —
        /// in a way this one is not.
        /// <para>
        /// It <b>opens</b> the block since #206, where it was its middle level. Being the plainest of them is
        /// what qualifies it: the chapter's premise is a layout deeper than the camera frames, and this states
        /// that premise with no second idea in it. What the move costs is that the chapter now opens on the
        /// longest level in the game, which is the thing to weigh if the block is ever paced again (#98).
        /// </para>
        /// </summary>
        private static Design Column() => new()
        {
            File = "Ten.json",
            Name = "Column",
            Grid = 11,
            //The whole point. FIELD_LEVELS is 16 everywhere else; this field is 34 deep, of which 24 carry
            //balls — a layout half again as tall as an ordinary level's entire field, and the camera frames
            //18 of it (GameplayScreen.FRAMED_LEVELS). The ten empty levels under it are the usual growth room.
            Depth = 24,
            FieldLevels = 34,
            Scene = SceneKind.Mountain,
            //Dome 8, the block's, where this shipped under 1. Nothing here argued for 1; dome 8 is Crown's
            //measured pairing (see Crown) and a block is one place at one hour.
            Sky = 8,
            Music = MUSIC_TOWER,
                Balls = BALLS_TOWER,
            Shots = 90,
            CeilingStep = 5,
            //Five cells across, against the pack's eleven. Twenty-four levels of a thirteen-wide disc would
            //be well over a thousand constrained bodies; this is ~500, in Colossus's family, and a tall level is
            //meant to last through its height rather than its mass.
            Occupied = (r, ang, i, depth) => r <= 2.6f,
            //Bands two levels thick around four colours: reading the column is reading what is coming, and a
            //band is a group of ~50, so the descent keeps handing the player something worth hitting. Both of
            //those still hold - the band is still two courses and its shell is still 45 balls - but the band
            //is now cut into a SHELL and a CORE, one palette step apart, and that is not decoration. See
            //COLUMN_CORE.
            Colour = (r, ang, i, depth) => Band(i / 2 + (r > COLUMN_CORE ? 1 : 0),
                new[] { BallType.Type1, BallType.Type5, BallType.Type7, BallType.Type3 }),
        };

        /// <summary>
        /// Where the column's core ends and its shell begins — and the whole reason this design has a second
        /// dimension of colour at all (#98).
        /// <para>
        /// <b>Plain horizontal bands made this a ONE-SHOT level.</b> A column hangs by its top course alone,
        /// and a band a colour makes that course a single group, so completing it took all 540 balls at once;
        /// every other band was load-bearing for everything under it too, and the four colours measured 75 %,
        /// 83 %, 91 % and 100 %. It is exactly the fault <see cref="One"/> records against banding a pyramid
        /// a course a colour, arriving in a shape where it is worse: a pyramid at least widens towards the
        /// glass, where a column is the same 21 cells all the way up.
        /// </para>
        /// <para>
        /// Splitting each band into two concentric regions fixes it <b>without touching the bands</b>, which is
        /// why it was chosen over cutting the column into vertical wedges: the wedges measure the same but they
        /// destroy the premise, and the premise here is that reading the column is reading what is coming. The
        /// anchor course is now a ring and a plug of different colours, so whichever a shot takes, the other
        /// still holds the column up. Measured: worst single shot 540 → 45 balls (100 % → 8 %), a perfect
        /// player 1 shot → 12, largest group 45 either way.
        /// </para>
        /// <para>
        /// <b>1.2 is a gap, not a radius.</b> The cells this must separate sit at 0.71 and 1.0 from the axis
        /// (shifted and unshifted levels) and the next ring out sits at 1.41 and 1.58, so anything in
        /// (1.0, 1.41) cuts the same five-cell plug on one parity and four on the other, and 1.2 is the middle
        /// of that gap. Landing ON a ring is what it is placed to avoid — swept, a core of 1.5 with three-course
        /// bands puts the level straight back to 92 % in one shot.
        /// </para>
        /// </summary>
        private const float COLUMN_CORE = 1.2f;

        #region The tall levels (#160)

        //Column was the only tall level in the pack, and #160's complaint about adding more was specific: "not
        //just Column stretched differently". These three are each a different KIND of tall, and what separates
        //them is not the silhouette but what the height does to the PLAY:
        //
        //  Horn  - the level gets BIGGER as it descends. The mass is back-loaded, so the budget is spent on a
        //          stalk and then the bell arrives.
        //  Helix - the level is OPEN and turns, so its width is a function of height and the player can read a
        //          whole turn of what is coming.
        //  Lean  - the level WALKS SIDEWAYS out of the middle of the frame, so the gun has to follow it.
        //
        //All three are 24 or 20 levels deep in a field of 34 or 30 against the 18 the camera frames
        //(GameplayScreen.FRAMED_LEVELS), so the top is out of shot at the start on all of them.

        /// <summary>
        /// A needle hanging point-down that opens into a bell out of shot. The <b>flare is quadratic</b> —
        /// <see cref="HORN_FLARE"/> times the level index squared — which is the whole design in one number:
        /// the lowest levels barely widen at all and the top ones carry most of the cluster, so the level does
        /// not get <i>longer</i> as the glass hands it down, it gets <i>bigger</i>. A linear taper reaches the
        /// same width having spent half of it on the middle, where it reads as a plain cone.
        /// <para>
        /// <b>It has to widen upwards, not downwards.</b> The layout hangs off the field's top level, so a cone
        /// tapering to its point up there would stand every ball in the level behind the few cells of the tip —
        /// and whatever colour that tip is takes the lot on one shot.
        /// </para>
        /// <para>
        /// Coloured by <see cref="HornShell"/>, which rings each level by <b>its own</b> rim rather than by an
        /// absolute radius — <see cref="OnionShell"/>'s trick, needed here for a second reason. An absolute ring
        /// would put the outermost colour only on the top few levels, and the magazine draws evenly among the
        /// colours still <i>alive</i>: a third of the level's shots would be unspendable until the mouth came
        /// into reach. Ringed by its own rim, the narrow stalk carries all three colours in the same proportions
        /// the mouth does, and each shell is one piece from the point to the glass, so peeling any one off never
        /// stands the other two on nothing.
        /// </para>
        /// <para>
        /// Measured: 506 balls, margin 2, nothing standing alone or in a pair, 12 recoloured by the repair pass
        /// (all in the stalk, where a shell boundary falls between two rows of a nine-cell level — the rim
        /// artefact that pass exists for), best single shots 23 %, 30 % and 37 %. If that repair count ever grows
        /// past about twenty the tip has gone too narrow for three shells and <see cref="HORN_TIP"/> is the
        /// number to raise, not the boundaries.
        /// </para>
        /// </summary>
        private static Design Horn() => new()
        {
            File = "Horn.json",
            Name = "Horn",
            //Thirteen for a rim reaching 4.67 at the mouth, which leaves the free column LateralMargin asks for
            Grid = 13,
            Depth = 20,
            //Ten levels of growth room under the layout, as Column has. Eight layout levels sit inside the
            //camera's window at the start, and those eight are the stalk - the bell is the part nobody has seen.
            FieldLevels = 30,
            Scene = SceneKind.Mountain,
            Sky = 8,
            Music = MUSIC_TOWER,
                Balls = BALLS_TOWER,
            Shots = 80,
            CeilingStep = 6,
            Occupied = (r, ang, i, depth) => r <= HornRim(i),
            Colour = (r, ang, i, depth) => HornShell(r, i),
        };

        /// <summary>
        /// Two strands winding around a common axis, tied by a rung every <see cref="HELIX_RUNG_EVERY"/>th
        /// level — a double helix, and the one shape in the pack whose <b>width</b> is a function of height:
        /// nine cells across where the strands stand side by side, three where one is directly behind the
        /// other, and a little over one whole turn from the glass to the floor. What that buys is the thing a
        /// tall level is for — the player can see a full turn of what is descending and knows which strand will
        /// be facing them when it arrives.
        /// <para>
        /// <b>The rungs are why this is a level and not a pair of falling ribbons.</b> A strand is bonded to the
        /// glass at its own top cell and nowhere else, so a colour cut anywhere along it would drop everything
        /// below the cut — the whole strand, on one shot near the top. With a rung every fourth level each
        /// strand also hangs off the other, and the worst group anywhere is a good cascade rather than a level
        /// ending.
        /// </para>
        /// <para>
        /// <b>A rung has no colour of its own, deliberately.</b> Given one it would be a handful of groups of
        /// four or five balls apiece, and the magazine draws evenly among the colours still standing — so a
        /// quarter of every shot would be spent on a colour worth five balls a hit, which is not a difficulty,
        /// it is a tax. Each half of a rung takes the colour of the strand it reaches from instead, so a rung
        /// reads as the two strands touching, which is exactly what it is.
        /// </para>
        /// <para>
        /// Segments of <see cref="HELIX_SEGMENT"/> levels rather than a whole strand in one colour: a strand's
        /// own colour would be a group of about 200, near half the level on one ball. The two strands are offset
        /// by two palette entries so the halves of every rung differ.
        /// </para>
        /// <para>
        /// Measured: 438 balls, margin 2, and the cleanest report in the pack — <b>nothing alone, nothing in a
        /// pair and nothing recoloured at all</b>, because the colouring only ever changes at a segment boundary
        /// or across the axis and both are crossings between blocks of dozens. Colour counts 105/107/112/114,
        /// best single shots 6 %, 6 %, 7 % and 12 %. Thinning <see cref="HELIX_STRAND"/> is what would end that:
        /// at 1.5 a strand drops to four cells on the unshifted levels against nine on the shifted ones, which
        /// pinches every other level and starts stranding rim cells. It is also the lightest tall level here, so
        /// the figure to watch in play is the opposite one — whether 66 shots is a loose budget for 438 balls.
        /// </para>
        /// </summary>
        private static Design Helix() => new()
        {
            File = "Helix.json",
            Name = "Helix",
            Grid = 13,
            Depth = 24,
            FieldLevels = 34,
            Scene = SceneKind.Mountain,
            Sky = 8,
            Music = MUSIC_TOWER,
                Balls = BALLS_TOWER,
            Shots = 66,
            CeilingStep = 5,
            Occupied = (r, ang, i, depth) => HelixStrand(r, ang, i) != 0 || HelixRung(r, ang, i, depth),
            Colour = (r, ang, i, depth) =>
            {
                int strand = HelixStrand(r, ang, i);

                //A rung cell outside both strands: it takes the colour of the strand it reaches from, so the
                //two halves meet at the axis in different colours and neither is a group of its own
                if (strand == 0)
                {
                    Untwist(r, ang, i * HELIX_TURNS_PER_LEVEL, out float along, out _);
                    strand = along >= 0f ? 1 : -1;
                }

                return Band(i / HELIX_SEGMENT + (strand > 0 ? 0 : 2),
                    new[] { BallType.Type1, BallType.Type7, BallType.Type3, BallType.Type5 });
            },
        };

        /// <summary>
        /// A round tower that <b>leans</b>: its axis walks <see cref="LEAN_PER_LEVEL"/> of a cell along X for
        /// every level it climbs, so over twenty-four levels it steps five and a bit cells sideways — a shade
        /// over one full width, where the tower at Pisa manages four degrees. It is the one tall level the gun
        /// has to <i>follow</i>: X runs across the screen (the gun starts at +Z looking at the origin), so the
        /// lean is on the axis the player can actually see, and the column walks out of the middle of the frame
        /// as the glass hands it down.
        /// <para>
        /// Drawn on the <b>raw lattice indices</b> and not on the emitter's centred radius, which is what a
        /// leaning shape needs: the centre it is measured from moves per level, so the polar pair the emitter
        /// offers is the wrong frame. <see cref="LeanRadius"/> rebuilds the emitter's own <c>dx</c>/<c>dz</c>
        /// around the drifted centre — and it may take the shifted-level offset straight off the <i>layout</i>
        /// index because <see cref="Emit"/> refuses an odd layout offset, so a layout level and its field level
        /// always agree in parity.
        /// </para>
        /// <para>
        /// Coloured in 3×3×3 blocks of masonry — <see cref="Mosaic"/>'s rule, and the only colouring already in
        /// the pack that reads as a <b>built</b> thing rather than a turned one, which is what a leaning tower
        /// wants. Blocks put all three colours on the anchor layer, the rule every design here answers, and 27
        /// cells is the size that keeps a group worth shooting at over twenty-four levels: the same blocks two
        /// levels tall rather than three make a tower this long a grind.
        /// </para>
        /// <para>
        /// Measured: 515 balls, best single shots 9 %, 20 % and 27 %, 4 balls standing in pairs and 7 recoloured
        /// — blocks clipped by the drifting rim, which is the repair pass's own remit (Prism ships with 4 in
        /// pairs and Static with 14). A couple of dozen recoloured would mean the block size and the drift are
        /// fighting each other, and the fix then is a block size that divides the drift rather than a bigger pass.
        /// </para>
        /// <para>
        /// <b>Margin 1 — the tightest in the pack, and the lean is the reason.</b> The tower's envelope is ten
        /// cells across X against five in Z, so the field is square around a shape that is not. One free column
        /// is what <see cref="LateralMargin"/> asks for and it is enough (it gives every flank ball a lateral
        /// neighbour to offer, which is the whole of what the trap needs), and Gem, One and Star all ship at 1 —
        /// but this is the one design reaching for the wall over twenty-four levels rather than six, so it is the
        /// first place to look if a shot is ever reported bouncing off a flank. <see cref="LEAN_GRID"/> at 15 is
        /// the lever, and it costs a wider glass plate and a longer camera stand-off.
        /// </para>
        /// </summary>
        private static Design Lean() => new()
        {
            File = "Lean.json",
            Name = "Lean",
            Grid = LEAN_GRID,
            Depth = 24,
            FieldLevels = 34,
            Scene = SceneKind.Mountain,
            Sky = 8,
            Music = MUSIC_TOWER,
                Balls = BALLS_TOWER,
            Shots = 78,
            CeilingStep = 6,
            OccupiedBlock = (x, z, i, depth) => LeanRadius(x, z, i, depth) <= LEAN_RADIUS,
            BlockColour = (x, z, i) => Band((x / 3) + (z / 3) + (i / 3),
                new[] { BallType.Type1, BallType.Type4, BallType.Type3 }),
        };

        #endregion

        #region The monument levels (#255)

        //THE SECOND TOWER BLOCK (#255): five more talls over the mountains - dome 8, the Tower's own
        //measured dusk (see Crown for why), and MUSIC_TOWER, because this block is #160's chapter answered
        //at campaign scale. That issue's complaint ("not just Column stretched differently") binds here
        //too, and what separates the five is what the height DOES:
        //
        //  Pagoda   - the lattice's own close packing built as architecture: every course nests into the
        //             pockets of the one above, eaves cantilevering out a single ball at a time.
        //  Spyglass - the tower that collapses INWARD: three concentric sleeves, each hiding a thinner one
        //             already hanging deeper inside it.
        //  Belfry   - an openwork frame with a live payload: a bell on a rope, and one true brown shot
        //             rings it down.
        //  Organ    - a rank of independent verticals under one deck: cut a pipe's collar and the whole
        //             pipe orphans and falls full-length out of the facade.
        //  Pylon    - a splaying truss felled leg by leg, re-hanging itself off its own girdle rings.
        //
        //All five are 22 or 24 levels deep in fields of 30 or 32 against the 18 the camera frames
        //(GameplayScreen.FRAMED_LEVELS), so the top is out of shot at the start on every one of them, and
        //every field-less-layout offset is 8 - even, which is what Emit demands to keep the level parity.

        /// <summary>
        /// A pagoda whose every storey, eave and finial nests into the pockets of the course above - the
        /// lattice's close packing built as architecture, and the block's opener because solid slabs
        /// everywhere make it the safest build of the five. Twenty-four centred square courses whose width
        /// parity follows the level parity (odd widths on even layout levels, even on odd -
        /// <see cref="PAGODA_WIDTHS"/>), so a course's balls sit in the pockets of its neighbours and the
        /// three eave canopies cantilever outward one ball at a time. Read from below: a point-down finial
        /// tip, the widest four-step roof, core, a three-step roof, core, a two-step roof, core to the
        /// glass - each roof one course shorter going up. ~724 balls.
        /// <para>
        /// <b>The course-width schedule is the design and the only lever.</b> The gate's own warning: one
        /// off-by-one in <see cref="PAGODA_WIDTHS"/> turns a pocket nest into a face-stack that sags, so a
        /// change there is verified interface by interface (width parity against level parity), and the
        /// spacing is never the thing to tune.
        /// </para>
        /// <para>
        /// <b>The top level carries only the four core colours</b>, with brown, red and cyan living below
        /// on always-shootable eaves and the tip - so the gate's FIRST check is the drop test, and the
        /// agreed fallback if the endgame hangs off a single colour is recolouring the i=22..23 core band
        /// into brown/red halves, never touching the schedule. The other measured worry is Roof C's brown
        /// orphaning the red eave edge and the finial under it (~36 % on paper): if fusion across roof
        /// courses pushes that up, its brown splits in two at the z-midline.
        /// </para>
        /// <para>
        /// Cores cannot chokepoint by construction: each storey stands on four independent quadrant
        /// columns (<see cref="PAGODA_CORE_COLOURS"/>, rotated a step per storey), so releasing one colour
        /// leaves three columns carrying the storey.
        /// </para>
        /// </summary>
        private static Design Pagoda() => new()
        {
            File = "Pagoda.json",
            Name = "Pagoda",
            Grid = 13,
            Depth = 24,
            FieldLevels = 32,
            Scene = SceneKind.Mountain,
            Sky = 8,
            Music = MUSIC_TOWER,
                Balls = BALLS_TOWER,
            Shots = 78,
            CeilingStep = 5,
            //A course is a centred square, so the one span test serves both indices
            OccupiedBlock = (x, z, i, depth) => InPagodaCourse(x, i) && InPagodaCourse(z, i),
            BlockColour = PagodaColour,
        };

        //The pagoda's plan: centre column 6 of a 13-wide grid, and the width schedule indexed by layout
        //level (i = 0 the finial tip, 23 against the glass). ITS PARITY IS THE DESIGN - odd widths on even
        //levels, even widths on odd - so every course nests ball-in-pocket into its neighbours and every
        //in-section interface steps by exactly one ball. Widest course 9 (x2..10), margin 2.
        private const int PAGODA_CENTRE = 6;
        private static readonly int[] PAGODA_WIDTHS =
        {
            1, 2, 3, 4,      //the finial, i=0..3: a point-down mini pyramid ending in a single ball
            9, 8, 7, 6,      //Roof C, i=4..7: the widest canopy, red eave edge on its bottom course
            5, 4, 5,         //the low core storey, i=8..10
            8, 7, 6,         //Roof B, i=11..13
            5, 4, 5, 4,      //the mid core storey, i=14..17
            7, 6,            //Roof A, i=18..19 - one course shorter than B, as B is than C
            5, 4, 5, 4,      //the top core storey, i=20..23, bonded to the glass
        };

        //Where the roofs and core storeys sit, in layout levels (Bottom..Top inclusive), top storey first.
        //A roof's Bottom course is its widest - the painted eave edge PagodaColour turns red. The index
        //into PAGODA_CORES is also the rotation PagodaColour applies to the quadrant palette.
        private static readonly (int Bottom, int Top)[] PAGODA_ROOFS = { (18, 19), (11, 13), (4, 7) };
        private static readonly (int Bottom, int Top)[] PAGODA_CORES = { (20, 23), (14, 17), (8, 10) };
        private const int PAGODA_FINIAL_TOP = 3;

        //The core quadrants' palette, clockwise from NE, rotated one step per storey going down
        //#285: {green, blue, yellow, magenta} until the owner reported this tower reading as randomly
        //coloured. The storeys are the QUIET half of a pagoda - plastered wall and timber post, with two
        //painted panels - and they have to stay out of the roofs, which carry the level's colour: the eaves
        //are lacquer and gilt and the tiles are slate. None of these four is a roof ink, so no storey can
        //merge into the roof above or below it, which is what the quadrant cut exists to prevent.
        private static readonly BallType[] PAGODA_CORE_COLOURS =
        {
            BallType.Type4,   //white - plastered wall
            BallType.Type3,   //blue - a painted panel
            BallType.Type10,  //brown - timber post
            BallType.Type2,   //green - a painted panel
        };

        //The roofs' own two palettes, cut by quadrant and rotated a step per roof - see PagodaColour for
        //the measurement that put them there. Two entries against four quadrants means a colour is two
        //OPPOSITE sheets, which touch nowhere: the arrangement that cannot make a plate out of a course.
        private static readonly BallType[] PAGODA_EAVE_COLOURS = { BallType.Type1, BallType.Type7 };   //lacquered eaves: red and gold. Red/orange measures 14.1 dE, the palette's TIGHTEST pair (#285); red/gold is 44.8
        private static readonly BallType[] PAGODA_TILE_COLOURS = { BallType.Type11, BallType.Type12 }; //slate tiles: silver and navy. Brown/black measures 19.2 dE, the palette's fourth tightest (#285); silver/navy is 24.9

        /// <summary>
        /// Which quadrant of the tower a cell is in, 0..3 clockwise from NE - measured against the course's
        /// own centre with the level's half-cell shift applied, so the cut lands in the same place at
        /// either parity. Shared by the cores and the roofs since #255.
        /// </summary>
        private static int PagodaQuadrant(int x, int z, int i)
        {
            float dx = x + (i % 2) * HALF - PAGODA_CENTRE;
            float dz = z + (i % 2) * HALF - PAGODA_CENTRE;

            return dx >= 0f ? (dz >= 0f ? 0 : 1) : (dz >= 0f ? 3 : 2);
        }

        /// <summary>
        /// Whether one index sits inside its level's course span. A course of width w starts at
        /// <c>PAGODA_CENTRE - w / 2</c>, which centres BOTH parities on world column 6: an odd width on an
        /// unshifted level spans it symmetrically, and an even width on a shifted level starts half a cell
        /// low so the level's own +0.5 shift walks it back onto the centre.
        /// </summary>
        private static bool InPagodaCourse(int c, int i)
        {
            int start = PAGODA_CENTRE - PAGODA_WIDTHS[i] / 2;
            return c >= start && c < start + PAGODA_WIDTHS[i];
        }

        /// <summary>
        /// Brown roofs with a red eave edge on each roof's widest course, quadrant-columned core storeys,
        /// and a cyan finial. The quadrant is read off the WORLD offset from the course centre - taking
        /// the shift off the layout index is safe because <see cref="Emit"/> refuses an odd layout offset,
        /// the same argument <see cref="LeanRadius"/> records - and the centre row and column of an
        /// odd-width course fall to the >= side, so the quadrants are near-quarters, never below
        /// <see cref="MIN_GROUP"/>.
        /// </summary>
        private static BallType PagodaColour(int x, int z, int i)
        {
            if (i <= PAGODA_FINIAL_TOP) return BallType.Type7;                       //gold - the gilded finial, one 30-ball group

            //THE ROOFS ARE CUT BY QUADRANT, and that is measured rather than drawn. A pagoda is a vertical
            //CHAIN - core storey, roof, core storey, roof - so every roof is the sole link between what is
            //above it and everything below, and painted one colour a course it was a plate: the top roof's
            //brown course took 642 balls of 724 in one ball (88 %) and its red eave 606 (83 %), against a
            //pack whose worst was 52. Cut into quadrants the same way the cores are, a colour is two
            //opposite sheets that meet nowhere, so taking one leaves the other half of the roof holding the
            //storey below - Bullseye's own fix, arriving on a tower.
            for (int roof = 0; roof < PAGODA_ROOFS.Length; roof++)
            {
                (int bottom, int top) = PAGODA_ROOFS[roof];
                if (i < bottom || i > top) continue;

                int quadrant = PagodaQuadrant(x, z, i);

                return i == bottom
                    ? Band(quadrant + roof, PAGODA_EAVE_COLOURS)
                    : Band(quadrant + roof, PAGODA_TILE_COLOURS);
            }

            for (int storey = 0; storey < PAGODA_CORES.Length; storey++)
            {
                (int bottom, int top) = PAGODA_CORES[storey];
                if (i < bottom || i > top) continue;

                return PAGODA_CORE_COLOURS[(PagodaQuadrant(x, z, i) + storey) % PAGODA_CORE_COLOURS.Length];
            }

            //Unreachable: every course above the finial is a roof or a core by the schedule
            return BallType.Type10;
        }

        /// <summary>
        /// A spyglass hanging objective-up: three concentric telescoping sections, every one bonded to the
        /// glass on its own, each inner one reaching deeper - clear a sleeve and a thinner tube is already
        /// hanging inside it, so the next tower is literally visible through the current one's rim.
        /// Descending: the cyan eyepiece bulb, the bare core rod, a hard step out to the middle sleeve's
        /// rim at i=8..9, a second step out to the outer sleeve at i=15, the glass.
        /// <para>
        /// <b>The boundary radii force radial lamination and the gate's first check is that it took</b>:
        /// 3.5 against 3.6 and 1.7 against 1.8 put the sleeves' facing cells in lattice contact, so the
        /// wall is laminated wherever sleeves overlap and the level has three independent load paths. If
        /// the measured contact graph shows a gap on any level, the agreed tune is widening the INNER
        /// sleeve's outer radius by 0.2 (<see cref="SPYGLASS_MID_OUT"/> to 3.7, or
        /// <see cref="SPYGLASS_CORE"/> to 1.9) - never the outer sleeve, whose 5.5 is already this grid's
        /// margin-1 extent.
        /// </para>
        /// <para>
        /// <b>The bare core below the middle sleeve is the one slender thing</b>: a ~9-cell-section rod
        /// carrying the 63-ball bulb for eight unlaminated levels. The gate's second check is its rest sag
        /// against the death line with the bulb attached; if it rides low, <see cref="SPYGLASS_BULB"/>
        /// shrinks to 2.2 before the rod is shortened. Third: the middle sleeve's rim must read as a hard
        /// step in the opening frame - the step IS the kind-statement - and drops its floor to i=7 if it
        /// reads mushy.
        /// </para>
        /// <para>
        /// Coloured per sleeve in alternating quadrant arcs on the diagonals
        /// (<see cref="SPYGLASS_QUADRANT_TWIST"/>), the core in two halves, the bulb all cyan. Opposite
        /// same-colour quadrants never touch, so every group is one arc and releasing any of them orphans
        /// nothing. The top level carries all six sleeve colours; only cyan lives below, on the
        /// always-shootable tip.
        /// </para>
        /// </summary>
        private static Design Spyglass() => new()
        {
            File = "Spyglass.json",
            Name = "Spyglass",
            Grid = 13,
            Depth = 22,
            FieldLevels = 30,
            Scene = SceneKind.Mountain,
            Sky = 8,
            Music = MUSIC_TOWER,
                Balls = BALLS_TOWER,
            Shots = 72,
            CeilingStep = 6,
            Occupied = (r, ang, i, depth) => SpyglassOccupied(r, i),
            Colour = (r, ang, i, depth) => SpyglassColour(r, ang, i),
        };

        //The three tubes' radii. Each facing pair (3.5/3.6 and 1.7/1.8) is deliberately one lattice step
        //apart so the sleeves stand in radial contact; 5.5 is the widest annulus a 13-wide field holds at
        //margin 1. The bulb replaces the core over the layout's lowest three levels.
        private const float SPYGLASS_CORE = 1.7f;
        private const float SPYGLASS_MID_IN = 1.8f;
        private const float SPYGLASS_MID_OUT = 3.5f;
        private const float SPYGLASS_OUTER_IN = 3.6f;
        private const float SPYGLASS_OUTER_OUT = 5.5f;
        private const float SPYGLASS_BULB = 2.6f;

        //How deep each section reaches. Every section's TOP is the glass - each bonds on its own, which is
        //the three-load-path forgiveness - and these are the floors, each inner section reaching deeper.
        private const int SPYGLASS_BULB_TOP = 2;
        private const int SPYGLASS_MID_FLOOR = 8;
        private const int SPYGLASS_OUTER_FLOOR = 15;

        //0.125 of a turn shears SectorIndex's boundaries onto the diagonals (45/135/225/315 degrees), so
        //each quadrant is centred on a lattice axis and its arc is rounded off symmetrically
        private const float SPYGLASS_QUADRANT_TWIST = 0.125f;

        private static readonly BallType[] SPYGLASS_OUTER_PAIR = { BallType.Type12, BallType.Type4 };  //navy, white
        private static readonly BallType[] SPYGLASS_MID_PAIR = { BallType.Type1, BallType.Type2 };     //red, green
        private static readonly BallType[] SPYGLASS_CORE_HALVES = { BallType.Type6, BallType.Type7 };  //magenta, yellow

        /// <summary>The three tubes and the bulb - the constants above are the whole of it.</summary>
        private static bool SpyglassOccupied(float r, int i)
        {
            if (i <= SPYGLASS_BULB_TOP) return r <= SPYGLASS_BULB;    //the eyepiece bulb replaces the core here

            if (r <= SPYGLASS_CORE) return true;                      //the core rod, glass to floor

            if (r <= SPYGLASS_MID_OUT) return i >= SPYGLASS_MID_FLOOR && r >= SPYGLASS_MID_IN;

            return i >= SPYGLASS_OUTER_FLOOR && r >= SPYGLASS_OUTER_IN && r <= SPYGLASS_OUTER_OUT;
        }

        /// <summary>
        /// Cyan bulb, halved core, quadrant-arced sleeves. The radius picks the section exactly as
        /// <see cref="SpyglassOccupied"/> does, so occupancy and colour cannot disagree about which tube
        /// a cell is in.
        /// </summary>
        private static BallType SpyglassColour(float r, float ang, int i)
        {
            if (i <= SPYGLASS_BULB_TOP) return BallType.Type5;        //cyan - the bulb, one 63-ball group

            if (r <= SPYGLASS_CORE) return Sector(ang, 0f, 2, SPYGLASS_CORE_HALVES);

            return r <= SPYGLASS_MID_OUT
                ? Sector(ang, SPYGLASS_QUADRANT_TWIST, 4, SPYGLASS_MID_PAIR)
                : Sector(ang, SPYGLASS_QUADRANT_TWIST, 4, SPYGLASS_OUTER_PAIR);
        }

        /// <summary>
        /// An open bell tower with a real bell hanging on a rope inside. Four full-height 2x2 corner
        /// piers, three closed ring beams tying them (the feet, the waist and the course at the glass),
        /// and inside the arcade a 160-ball bell on a 2x2 rope - rope and bell ONE fused brown group of
        /// 188 (~21 %), so one true brown shot releases the whole payload and the bell falls out of its
        /// tower. The bell swings in plain sight from the first frame while its rope is still above the
        /// camera: you see WHAT before you can reach what HOLDS it. ~876 balls, the block's heaviest.
        /// <para>
        /// <b>Bell isolation is the gate's first check, before anything else</b>: the skirt and neck must
        /// touch nothing but the rope in the measured contact graph, because one accidental lattice
        /// contact with a pier or a ring turns the one-shot finale into a partial release no validator
        /// flags. The skirt's four corner cells are chamfered off for exactly that
        /// (<see cref="BelfryBell"/>) - cross-level diagonals reach one cell, and (4,4) would reach the SW
        /// pier's (3,3). If a contact still appears, the agreed tune is shrinking the skirt or sliding the
        /// bell up one level - never moving the piers.
        /// </para>
        /// <para>
        /// <b>Rope stretch is the second check</b>: 160 balls hang on a seven-level 2x2 spring column, so
        /// the bell's rest sag is measured unshot against the death line, and
        /// <see cref="BELFRY_ROPE_FLOOR"/> rises by two (raising the bell's spans with it) if it rides
        /// low. Third: confirm the worst release really is bell-plus-rope at ~21 % and that no pier band
        /// fuses with a ring beam - ring cells at pier corners belong to the piers, which
        /// <see cref="BelfryColour"/>'s test order enforces in the writer, as the gate asked.
        /// </para>
        /// <para>
        /// The three closed ring loops are the second load path: any pier band cut leaves the pier's lower
        /// half hanging through the bottom ring off the other three piers. The bell deliberately has one
        /// path - it is the bell.
        /// </para>
        /// </summary>
        private static Design Belfry() => new()
        {
            File = "Belfry.json",
            Name = "Belfry",
            Grid = 15,
            Depth = 22,
            FieldLevels = 30,
            Scene = SceneKind.Mountain,
            Sky = 8,
            Music = MUSIC_TOWER,
                Balls = BALLS_TOWER,
            Shots = 84,
            CeilingStep = 5,
            OccupiedBlock = (x, z, i, depth) =>
                BelfryPier(x, z)
                || (BelfryRingLevel(i) && BelfryRingCell(x, z))
                || BelfryRope(x, z, i)
                || BelfryBell(x, z, i),
            BlockColour = BelfryColour,
        };

        //The frame's plan on a 15-wide grid: the arcade spans [2..12] in both indices (margins 2), the
        //piers are its 2x2 corners, and a ring beam is the 2-cell-thick perimeter of the same square.
        //Pier bands are BELFRY_PIER_BAND levels tall; the rope hangs from BELFRY_ROPE_FLOOR to the glass,
        //and it is the gate's rope-stretch lever - raised by two, the bell's own spans move up with it.
        private const int BELFRY_FRAME_MIN = 2;
        private const int BELFRY_FRAME_MAX = 12;
        private const int BELFRY_PIER_BAND = 4;
        private const int BELFRY_ROPE_FLOOR = 15;

        /// <summary>Whether one index runs through a pier: the two 2-cell strips the corners stand on.</summary>
        private static bool BelfryPierColumn(int c) => c == 2 || c == 3 || c == 11 || c == 12;

        /// <summary>A pier cell: both indices on pier strips. Full height - the piers ARE the tower.</summary>
        private static bool BelfryPier(int x, int z) => BelfryPierColumn(x) && BelfryPierColumn(z);

        /// <summary>
        /// A ring beam cell: inside the arcade square with either index on a pier strip - the square's
        /// perimeter at wall thickness 2, pier corners included (the colour rule hands those to the piers).
        /// </summary>
        private static bool BelfryRingCell(int x, int z) =>
            x >= BELFRY_FRAME_MIN && x <= BELFRY_FRAME_MAX && z >= BELFRY_FRAME_MIN && z <= BELFRY_FRAME_MAX
            && (BelfryPierColumn(x) || BelfryPierColumn(z));

        /// <summary>The three ring courses: the feet, the waist, and the two levels bonded to the glass.</summary>
        private static bool BelfryRingLevel(int i) => i <= 1 || i == 13 || i == 14 || i >= 20;

        /// <summary>The rope: a 2x2 column down the axis, from the glass to the bell's neck.</summary>
        private static bool BelfryRope(int x, int z, int i) =>
            i >= BELFRY_ROPE_FLOOR && x >= 6 && x <= 7 && z >= 6 && z <= 7;

        /// <summary>
        /// The bell, free-hanging under the rope and attached to nothing else: a 4x4 neck and shoulder
        /// (i=9..14, the neck meeting the rope's floor) and a 6x6 skirt (i=7..8) with its four corner
        /// cells chamfered off - the chamfer keeps the skirt out of cross-level diagonal reach of the
        /// corner piers, which is the one-shot finale surviving generation. If
        /// <see cref="BELFRY_ROPE_FLOOR"/> is ever raised, every level figure here rises with it.
        /// </summary>
        private static bool BelfryBell(int x, int z, int i)
        {
            //Neck (i=11..14) and shoulder (i=9..10) share the 4x4 footprint, so one test serves both
            if (i >= 9 && i <= 14) return x >= 5 && x <= 8 && z >= 5 && z <= 8;

            if (i == 7 || i == 8)
                return x >= 4 && x <= 9 && z >= 4 && z <= 9
                    && !((x == 4 || x == 9) && (z == 4 || z == 9));

            return false;
        }

        /// <summary>
        /// Pier cells first - ring cells at pier corners belong to the piers, the writer-side rule the
        /// gate asked for - then ring green, and everything left is the fused brown rope-and-bell. Pier
        /// bands alternate red and white in <see cref="BELFRY_PIER_BAND"/>-level courses; the NE/SW
        /// diagonal pair starts red and NW/SE white, so the top band shows both colours at the glass and
        /// every colour in the level stands there.
        /// </summary>
        private static BallType BelfryColour(int x, int z, int i)
        {
            if (BelfryPier(x, z))
            {
                int band = i / BELFRY_PIER_BAND;                             //0..5, the sixth the short one
                int diagonal = ((x >= 11 ? 1 : 0) + (z >= 11 ? 1 : 0)) % 2;  //0 the NE/SW pair, 1 NW/SE

                return (band + diagonal) % 2 == 0 ? BallType.Type1 : BallType.Type4;   //red / white
            }

            if (BelfryRingLevel(i) && BelfryRingCell(x, z)) return BallType.Type2;     //green ring beams

            return BallType.Type10;   //brown - rope and bell, ONE group, the level's whole payoff
        }

        /// <summary>
        /// A church organ facade: a solid three-level windchest bonded to the glass, and under it two
        /// ranks of free-hanging 2x2 pipes - five in front in a facade V (tallest at the centre), four
        /// behind peeking through the front gaps - every pipe hung independently from the chest and
        /// touching nothing else. Each pipe is silver-bodied with its voice colour on the collar (its top
        /// three levels) and mouth (its bottom three): release a 12-ball collar and the entire pipe below
        /// it orphans and falls out of the facade in one rigid piece, up to 56 balls at a time. ~646 balls.
        /// <para>
        /// <b>The top-level rule is this design's nearest edge and the gate's first check</b>: the voice
        /// colours never reach the glass, mitigated only by the always-shootable collars at i=16..18. The
        /// drop test runs immediately, and if the stop flags an endgame hang the agreed fallback is
        /// recolouring the windchest's two silver slabs into voice colours - NEVER the pipe bodies,
        /// because silver-body-per-pipe (kept off the chest's silver by the collars in between) is what
        /// makes each pipe one orphan.
        /// </para>
        /// <para>
        /// <b>The ranks must clear each other</b>: the empty z8 column separates them, and cross-level
        /// diagonals reach only one cell, so they are safe by construction - but the gate says verify it
        /// in the measured contact graph, because the whole orphan mechanic dies on one touch. Front pipes
        /// stand two empty columns apart in x for the same reason (one column, plus the reach).
        /// </para>
        /// <para>
        /// The tallest centre pipe wears a cyan tuning boot over its mouth's lower half
        /// (<see cref="ORGAN_BOOT_TOP"/>) - the rejected Chain's pendant-amplifier graft - so the heaviest
        /// pipe reads heaviest and cutting its collar drops the biggest single mass in the level onto the
        /// glass springs. The gate's third check is that pipe's rest sag with the boot: if it rides the
        /// death line, its bottom rises from i=2 to i=3 rather than the boot shrinking.
        /// </para>
        /// <para>
        /// Worst release on paper is a chest slab orphaning the three pipes hung under it (~33 %); the
        /// design's point is the mid-size full-length fall, staged nine times over.
        /// </para>
        /// </summary>
        private static Design Organ() => new()
        {
            File = "Organ.json",
            Name = "Organ",
            Grid = 17,
            Depth = 22,
            FieldLevels = 30,
            Scene = SceneKind.Mountain,
            Sky = 8,
            Music = MUSIC_TOWER,
                Balls = BALLS_TOWER,
            Shots = 86,
            CeilingStep = 5,
            OccupiedBlock = (x, z, i, depth) => OrganChest(x, z, i) || OrganPipe(x, z, i, out _, out _),
            BlockColour = OrganColour,
        };

        //The facade's plan on a 17-wide grid. The chest is a 14x5 slab (x2..15, z6..10) on i=19..21; the
        //pipe tops all sit at ORGAN_PIPE_TOP directly under it. The ranks are parallel arrays of each
        //pipe's left column and bottom level: the front V is tallest at the centre and the back rank's
        //bottoms peek through the front gaps. z8 stays empty - that column is what keeps the two ranks
        //out of each other's contact graphs.
        private const int ORGAN_CHEST_FLOOR = 19;
        private const int ORGAN_PIPE_TOP = 18;
        private const int ORGAN_BOOT_TOP = 3;   //the centre pipe's cyan boot: i=2..3, its mouth's lower half
        private static readonly int[] ORGAN_FRONT_X = { 2, 5, 8, 11, 14 };
        private static readonly int[] ORGAN_FRONT_BOTTOM = { 8, 5, 2, 5, 8 };
        private static readonly int[] ORGAN_BACK_X = { 3, 6, 9, 12 };
        private static readonly int[] ORGAN_BACK_BOTTOM = { 10, 7, 7, 10 };

        //Each pipe's voice - the colour of its collar and mouth, symmetric about the centre pipe. Chosen
        //so no confusable pair coexists: silver never meets white or yellow (absent from the level), and
        //orange never meets red (absent).
        private static readonly BallType[] ORGAN_FRONT_VOICES =
        {
            BallType.Type3,   //blue
            BallType.Type5,   //cyan
            BallType.Type9,   //orange - the tallest pipe, centre of the V
            BallType.Type5,   //cyan
            BallType.Type3,   //blue
        };
        private static readonly BallType[] ORGAN_BACK_VOICES =
        {
            BallType.Type2,   //green
            BallType.Type6,   //magenta
            BallType.Type6,   //magenta
            BallType.Type2,   //green
        };

        /// <summary>The windchest: the solid slab every pipe hangs from, bonded to the glass.</summary>
        private static bool OrganChest(int x, int z, int i) =>
            i >= ORGAN_CHEST_FLOOR && x >= 2 && x <= 15 && z >= 6 && z <= 10;

        /// <summary>
        /// Which pipe a cell is in, if any, with the pipe's voice and its bottom level - the one walk of
        /// the rank tables, shared by occupancy and colour so the two cannot disagree.
        /// </summary>
        private static bool OrganPipe(int x, int z, int i, out BallType voice, out int bottom)
        {
            voice = default;
            bottom = 0;

            if (i > ORGAN_PIPE_TOP) return false;

            int[] lefts, bottoms;
            BallType[] voices;

            if (z == 6 || z == 7) { lefts = ORGAN_FRONT_X; bottoms = ORGAN_FRONT_BOTTOM; voices = ORGAN_FRONT_VOICES; }
            else if (z == 9 || z == 10) { lefts = ORGAN_BACK_X; bottoms = ORGAN_BACK_BOTTOM; voices = ORGAN_BACK_VOICES; }
            else return false;

            for (int p = 0; p < lefts.Length; p++)
            {
                if (x < lefts[p] || x > lefts[p] + 1 || i < bottoms[p]) continue;

                voice = voices[p];
                bottom = bottoms[p];
                return true;
            }

            return false;
        }

        /// <summary>
        /// The chest in four brown/silver slabs, then per pipe: voice collar, silver body, voice mouth,
        /// and the centre pipe's cyan boot over the mouth's lower half. The chest's silver never touches a
        /// pipe's - the collars sit between them - so every silver body is its own group, which is the
        /// orphan mechanic itself.
        /// </summary>
        private static BallType OrganColour(int x, int z, int i)
        {
            //The windchest's slab boundaries: x2..5, 6..8, 9..12, 13..15
            if (i >= ORGAN_CHEST_FLOOR)
                return x <= 5 ? BallType.Type10       //brown
                    : x <= 8 ? BallType.Type11        //silver
                    : x <= 12 ? BallType.Type10       //brown
                    : BallType.Type11;                //silver

            OrganPipe(x, z, i, out BallType voice, out int bottom);

            //The tuning boot: the centre front pipe (ORGAN_FRONT_X[2], the V's deepest) at its lowest two
            //levels, fused cyan - a colour of its own so the boot reads as a fitting rather than as more mouth
            if (i <= ORGAN_BOOT_TOP && z <= 7 && x >= ORGAN_FRONT_X[2] && x <= ORGAN_FRONT_X[2] + 1)
                return BallType.Type5;                //cyan

            if (i > ORGAN_PIPE_TOP - 3 || i < bottom + 3) return voice;   //collar / mouth

            return BallType.Type11;                   //silver body - one orphanable group per pipe
        }

        /// <summary>
        /// A four-legged mountain pylon felled leg by leg - the block's finale and its physics thesis
        /// stated loudest. A solid 5x5 cap bonds to the glass; four 2x2 legs splay outward from under it,
        /// stepping one cell out in x AND z every <see cref="PYLON_LEG_BAND"/>-level band with a
        /// one-column overlap at every step, so a leg is bonded vertically through every step and never
        /// hangs on diagonals alone - which is the gate's SECOND check, in the measured contact graph,
        /// because a step landed on the wrong parity fails silently. Three closed girdle rings and a cap
        /// collar tie all four legs: cut a leg band and the leg's lower run re-hangs from the ring below
        /// the cut, off the other three legs - the structure argues back, and every felled leg makes the
        /// survivor stance springier. 580 balls drawn.
        /// <para>
        /// <b>⚠ This is the level #301 was reported on, and its present figures are the sag probe's</b> -
        /// the shipped truss (24 deep, feet at anchor 12, two-level rings) lost all five probe orders, three
        /// of them on the FIRST shot, the glass at rest. What ships now reads <b>1 of 5, four orders
        /// clearing the level outright</b> - see Depth and PYLON_RINGS for what moved and what was measured
        /// out on the way (the twin-loop graft among the casualties).
        /// </para>
        /// <para>
        /// Third check: the opening frame must show spread feet plus two rings, so it reads as a truss and
        /// not as Carousel's rails-and-decks - the splay is the distinctness argument, protect it.
        /// </para>
        /// <para>
        /// Legs alternate their primary with white down four four-level bands, primary at the FEET - the
        /// end the opening frame reads - and white at the top, where the cap's quadrant columns carry all
        /// four primaries against the glass instead; the rings are brown closed loops of their own. Worst
        /// single shot is the widest ring whole, 96 balls (16 %) - a designed release, the "re-hangs off
        /// its own girdle rings" premise - and the difficulty is sequencing, not any one group.
        /// </para>
        /// </summary>
        private static Design Pylon() => new()
        {
            File = "Pylon.json",
            Name = "Pylon",
            Grid = 15,
            //24 until #301, and the four levels came off the BOTTOM - the probe's finding, after both of the
            //recorded ring levers were measured and neither closed it, is that the fault was never one
            //member: the shipped truss started its feet 5.78 over the line and every asymmetric cut near
            //them - an opened ring corner, a severed band's foot cantilevered on a ring, a swinging bare
            //leg - dipped 6 to 7 units, three distinct mechanisms against one 1-unit margin. No bracing
            //scheme closed that gap (the readings are on the issue); height did. Shedding the lowest leg
            //band starts the feet 8.90 over the line (measured at settle) and shortens every pendulum, and
            //with the gentler splay (PYLON_FOOT_ANCHOR 11) and the taller rings it is what took the probe
            //from 5 of 5 losing on the first shot to 1 of 5 (four orders clearing the level whole; the one
            //loss lands near shot ten, its exact number wobbling with the solver's thread order). Ball
            //count 612 -> 580: one band traded for the rings' third levels.
            Depth = 20,
            FieldLevels = 32,
            Scene = SceneKind.Mountain,
            Sky = 8,
            Music = MUSIC_TOWER,
                Balls = BALLS_TOWER,
            Shots = 74,
            //#288: 6 until the owner reported nearly every shot ending in "The cluster reached the line", and
            //this one is not a tight margin - the two clocks disagreed outright, the fault LevelGen's own
            //header records for the pictures. At the shipped depth of 24 the lowest ball started 6.16 over
            //the line and six bought TWELVE descents at 0.60, i.e. 7.20 - an UNTOUCHED Pylon was past the
            //line on its eleventh descent, at shot 66 of 74, the last eight shots unusable however well
            //aimed. That is what the owner was working around by throwing shots away: a miss does not grow
            //the cluster downwards. Nine buys eight descents, 4.80, against the 8.90 the depth-20 truss now
            //starts with (#301) - about 4.1 left at budget's end, clear of any measured swing. Nine is kept
            //rather than re-tightened: this level's swings are the deepest in the pack, and the headroom is
            //what they land in.
            CeilingStep = 9,
            OccupiedBlock = (x, z, i, depth) =>
                PylonLeg(x, z, i) != 0 || PylonCap(x, z, i) || PylonRing(x, z, i),
            BlockColour = PylonColour,
        };

        //The truss's plan on a 15-wide grid, mirrored about centre 7 (an index and its mirror sum to
        //PYLON_MIRROR). The NE leg's low corner sits at 8 under the cap and steps one cell out, in both
        //indices, per PYLON_LEG_BAND levels going down - 0.25 cells a level - reaching PYLON_FOOT_ANCHOR
        //at the feet. The cap owns i >= PYLON_CAP_FLOOR.
        private const int PYLON_MIRROR = 13;
        private const int PYLON_LEG_BAND = 4;
        private const int PYLON_FOOT_ANCHOR = 11;
        private const int PYLON_CAP_FLOOR = 16;

        //The girdle rings: (Bottom level, Min index, Levels) triples, each the 1-cell closed square
        //perimeter of [Min .. 14-Min]. Ring corners coincide with the legs on the two levels that share the
        //leg's band, and those cells belong to the legs (PylonColour's test order). The widest ring [2..12]
        //is the margin-2 extent.
        //
        //All three levels tall - the Bolt's "THREE AND NOT TWO", measured here too (#301), and in BOTH
        //directions: at two levels a ring's corners fall inside one leg band, so a foot release opens the
        //whole loop and the C unrolls past the line (the shipped level's first-shot death); at three, the
        //third level crosses the band boundary and keeps a closed loop through any single release. Thinning
        //just the bottom ring back to two was re-tried for its pendulum mass and read 5 of 5 - the stiffness
        //is worth more than the weight costs. The first entry is the CAP COLLAR, new with #301: its two
        //lower levels tie the leg tops the way the girdles do, and its top level rings the cap's own base
        //course, so the hinge the bare legs swing about once the girdles release is braced against the
        //anchor itself.
        private static readonly (int Bottom, int Min, int Levels)[] PYLON_RINGS =
            { (14, 5, 3), (10, 4, 3), (6, 3, 3), (2, 2, 3) };

        //⚠ THE TWIN-LOOP GRAFT THAT USED TO SIT HERE IS MEASURED OUT (#301), not merely switched off. Its
        //premise was that a second loop one cell inside the first keeps a closed ring when the outer is cut
        //- but the probe read it 5 of 5 alone AND 5 of 5 combined with three-level rings (dying later, at
        //shots 7-15, to 41-121-ball releases), because both laminae's corners sit inside the legs' 2x2
        //footprints and one leg band therefore opens BOTH loops at once, while the doubled mass makes every
        //ring a heavier one-ink target. What answered the sag instead was height off the line, the gentler
        //splay, the taller rings and the cap collar - see Depth and PYLON_RINGS.

        //Leg primaries, indexed NE, SE, SW, NW - the order PylonLeg and the cap quadrants both speak
        private static readonly BallType[] PYLON_PRIMARIES =
        {
            BallType.Type1,   //red - NE
            BallType.Type3,   //blue - SE
            BallType.Type2,   //green - SW
            BallType.Type6,   //magenta - NW
        };

        /// <summary>The NE leg's low corner index at one layout level - the splay schedule in one line.</summary>
        private static int PylonLegAnchor(int i) => PYLON_FOOT_ANCHOR - i / PYLON_LEG_BAND;

        /// <summary>
        /// Which leg a cell is on: 0 none, then 1 NE, 2 SE, 3 SW, 4 NW - <see cref="PYLON_PRIMARIES"/>'s
        /// order plus one. The other three legs are the NE leg mirrored about the centre in x, z or both,
        /// so there is one splay schedule to get wrong rather than four.
        /// </summary>
        private static int PylonLeg(int x, int z, int i)
        {
            if (i >= PYLON_CAP_FLOOR) return 0;    //the cap owns these levels

            int high = PylonLegAnchor(i);
            int low = PYLON_MIRROR - high;

            bool xHigh = x == high || x == high + 1;
            bool xLow = x == low || x == low + 1;
            bool zHigh = z == high || z == high + 1;
            bool zLow = z == low || z == low + 1;

            if (xHigh && zHigh) return 1;   //NE
            if (xHigh && zLow) return 2;    //SE
            if (xLow && zLow) return 3;     //SW
            if (xLow && zHigh) return 4;    //NW
            return 0;
        }

        /// <summary>The cap: a solid 5x5 (x5..9 by z5..9) on the four levels bonded to the glass.</summary>
        private static bool PylonCap(int x, int z, int i) =>
            i >= PYLON_CAP_FLOOR && x >= 5 && x <= 9 && z >= 5 && z <= 9;

        /// <summary>Whether a cell is on a girdle ring: the 1-cell closed square perimeter of its entry in
        /// <see cref="PYLON_RINGS"/>, that entry's own number of levels tall.</summary>
        private static bool PylonRing(int x, int z, int i)
        {
            foreach ((int bottom, int min, int levels) in PYLON_RINGS)
            {
                if (i < bottom || i >= bottom + levels) continue;

                int max = PYLON_MIRROR + 1 - min;
                if (x < min || x > max || z < min || z > max) return false;

                return x == min || x == max || z == min || z == max;
            }

            return false;
        }

        /// <summary>
        /// Legs first - ring corners belong to them, so the test order IS that rule - then the cap's
        /// quadrant columns, then the rings. A leg's four bands alternate primary/white from the FEET up
        /// (primary at the feet, white at the top, the cap carrying the primaries against the glass); the
        /// cap's centre row and column (index 7) fall to the low side, so the quadrant columns are
        /// 36/24/24/16 over its four levels, the smallest still far above <see cref="MIN_GROUP"/>.
        /// </summary>
        private static BallType PylonColour(int x, int z, int i)
        {
            int leg = PylonLeg(x, z, i);
            if (leg != 0)
                return (i / PYLON_LEG_BAND) % 2 == 0 ? PYLON_PRIMARIES[leg - 1] : BallType.Type4;  //white

            if (PylonCap(x, z, i))
            {
                int quadrant = x >= 8 ? (z >= 8 ? 0 : 1) : (z >= 8 ? 3 : 2);   //NE, SE, SW, NW
                return PYLON_PRIMARIES[quadrant];
            }

            //Whole brown loops, deliberately, and it is a measured choice (#301): cut into two inks so that
            //no shot could take a whole ring, the probe read 5 of 5 in every such configuration - an OPENED
            //loop is worse than a missing one, because a C-ring swings 7-10 units where a closed loop cannot
            //lengthen, and a released loop at least goes symmetrically. The loop being a one-shot 25-121 ball
            //release is the design's own "re-hanging itself off its own girdle rings" premise.
            return BallType.Type10;   //brown
        }

        #endregion

        #region The tall levels' own geometry (#160)

        //The horn's own geometry: the radius at the point, and how hard it opens. The rim grows with the SQUARE
        //of the level index, so the mouth is the last thing to arrive - see Horn() for why it opens upwards.
        private const float HORN_TIP = 1.6f;
        private const float HORN_FLARE = 0.0085f;

        //Where the two shell boundaries sit, as a share of the level's OWN rim. Not thirds: equal shares put
        //five ninths of the balls in the outer shell, and the drop test then reads that colour as more than half
        //the level on one ball. At 0.45 and 0.78 the three shells come out very nearly level with each other.
        private const float HORN_CORE = 0.45f;
        private const float HORN_SKIN = 0.78f;

        /// <summary>The horn's radius at one layout level — <see cref="HORN_TIP"/> at the point, opening quadratically.</summary>
        private static float HornRim(int i) => HORN_TIP + HORN_FLARE * i * i;

        /// <summary>
        /// White core, red flesh, gold skin — ringed by <b>each level's own rim</b> rather than by an absolute
        /// radius, so a narrow level down at the point shows the same three-colour proportions the mouth does.
        /// See <see cref="Horn"/> for the two reasons that matters (the anchor rule and the magazine's draw) and
        /// <see cref="OnionShell"/> for the same trick answering the same trap on a sphere.
        /// </summary>
        private static BallType HornShell(float r, int i)
        {
            float shell = r / HornRim(i);

            if (shell <= HORN_CORE) return BallType.Type4;  //white core
            if (shell <= HORN_SKIN) return BallType.Type1;  //red flesh
            return BallType.Type7;                          //gold skin
        }

        //Each strand's centre runs at HELIX_RADIUS from the axis and the strand itself is a disc of
        //HELIX_STRAND. 2.6 and 1.7 leaves a 1.8-cell gap between the two strands - enough that they read as two,
        //and not so much that either is thin enough to go lonely. The pair reaches 4.3 from the axis, inside the
        //4.5 a 13-wide field allows.
        private const float HELIX_RADIUS = 2.6f;
        private const float HELIX_STRAND = 1.7f;

        //18 degrees a level: 1.2 turns over the layout, and a little under a full turn across the eighteen
        //levels the camera frames, so what the player can see IS one turn of the helix
        private const float HELIX_TURNS_PER_LEVEL = 0.05f;

        //1.1 and not 1. At 0.05 turns a level the strand frame comes back onto the lattice axes every fifth
        //level, where a rung's half-width is compared against whole-number cell offsets: a threshold of exactly
        //1 is then a coin toss on float dust out of the polar round-trip.
        private const float HELIX_RUNG_HALF = 1.1f;
        private const int HELIX_RUNG_EVERY = 4;
        private const int HELIX_SEGMENT = 3;

        /// <summary>
        /// Which strand a cell belongs to at this height: <c>+1</c> the one at <c>+along</c>, <c>-1</c> the one
        /// opposite, <c>0</c> neither. The two centres are a half-turn apart by construction — they are
        /// <c>(±HELIX_RADIUS, 0)</c> in the frame <see cref="Untwist"/> hands back — so there is one rotation to
        /// get wrong rather than two.
        /// </summary>
        private static int HelixStrand(float r, float ang, int i)
        {
            Untwist(r, ang, i * HELIX_TURNS_PER_LEVEL, out float along, out float across);

            float near = (along - HELIX_RADIUS) * (along - HELIX_RADIUS) + across * across;
            if (near <= HELIX_STRAND * HELIX_STRAND) return 1;

            float far = (along + HELIX_RADIUS) * (along + HELIX_RADIUS) + across * across;
            return far <= HELIX_STRAND * HELIX_STRAND ? -1 : 0;
        }

        /// <summary>
        /// A rung: the bar across the axis joining the two strands, on every <see cref="HELIX_RUNG_EVERY"/>th
        /// level <b>counted from the glass down</b> — so the top level carries one, and the level is tied at the
        /// one place where a failure would cost everything.
        /// </summary>
        private static bool HelixRung(float r, float ang, int i, int depth)
        {
            if ((depth - 1 - i) % HELIX_RUNG_EVERY != 0) return false;

            Untwist(r, ang, i * HELIX_TURNS_PER_LEVEL, out float along, out float across);
            return MathF.Abs(across) <= HELIX_RUNG_HALF && MathF.Abs(along) <= HELIX_RADIUS;
        }

        //The leaning tower's own geometry. LEAN_GRID is stated here and not only on the design because the shape
        //function needs the field's width to find its axis, and two copies of that number are two places for it
        //to be wrong. 0.22 of a cell per level against a level's own height of 1/sqrt(2) is a lean of 17 degrees.
        private const byte LEAN_GRID = 13;
        private const float LEAN_RADIUS = 2.6f;
        private const float LEAN_PER_LEVEL = 0.22f;

        /// <summary>
        /// How far one layout level's centre has walked from the layout's own middle, in cells. Measured from
        /// the middle rather than from the bottom so the tower leans <i>through</i> the field's axis instead of
        /// off one side of it — which is what keeps the lateral margin the same on both flanks.
        /// </summary>
        private static float LeanShift(int i, int depth) => (i - (depth - 1) * HALF) * LEAN_PER_LEVEL;

        /// <summary>
        /// The emitter's own <c>r</c>, rebuilt around the drifted centre: the <c>x + shift - axis</c> line for
        /// line, with the shift taken off the layout index (see <see cref="Lean"/> for why that is the same
        /// parity). The drift is in X alone; Z stays on the field's axis, so the tower leans across the screen
        /// rather than away from the gun, where it would only look narrow.
        /// </summary>
        private static float LeanRadius(int x, int z, int i, int depth)
        {
            float axis = (LEAN_GRID - 1) * HALF + HALF;
            float shift = (i % 2) * HALF;

            float dx = x + shift - axis - LeanShift(i, depth);
            float dz = z + shift - axis;

            return MathF.Sqrt(dx * dx + dz * dz);
        }

        #endregion
    }
}
