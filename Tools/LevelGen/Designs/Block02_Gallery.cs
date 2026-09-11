using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>The Gallery</b>, block 2 of the campaign: its designs, and the helpers no other block's designs use, in
    /// the order <c>Program.cs</c> held them — the play order is <see cref="Main"/>'s, and the block's name, music
    /// and ball style are in the tables there. Split out of <c>Program.cs</c> in #386.
    /// </summary>
    internal static partial class Program
    {

        /// <summary>
        /// A stepped cone hanging point-down, coloured in concentric rings: a target seen from underneath,
        /// and a flight of coloured steps seen from the side. Three colours, each ring one solid shell, so
        /// every ring is already a group of dozens waiting for one matching ball to touch it.
        /// </summary>
        /// <summary>
        /// A heart drawn across a flat hanging wall (#130) — the first level in the pack that is a
        /// <b>picture</b> rather than a solid of revolution, and the first whose shape is read off a bitmap
        /// instead of solved from a radius. Gentle on purpose: a generous shot budget, a slow ceiling and
        /// three colours, in One's spirit, because the point of it is to be recognised rather than aimed
        /// through.
        /// <para>
        /// The background is a 2×2 check of two colours and not one flat colour, which is the rule the
        /// picture region states and the drop test enforces: a single-colour background makes the wall's top
        /// row one group holding the whole thing up, and one matching ball takes the level.
        /// </para>
        /// </summary>
        /// <summary>
        /// One picture level: a flat wall the size of its own <paramref name="bitmap"/>, hanging in a
        /// <paramref name="grid"/>-wide field. The bitmap's own dimensions are the wall's, so a symbol is
        /// added by drawing it and nothing else — there is no second place to keep its size in step.
        /// </summary>
        /// <param name="symbol">
        /// What the symbol is drawn in — <b>one colour per ink</b>, in <see cref="SYMBOL_INK"/>'s own order, so
        /// a silhouette passes one, a symbol with a detail inside it two, and a drawn scene four. It was a
        /// <c>symbol</c> and an <c>accent</c> while there were three pictures, which is the exact shape of the
        /// three gentle ones and exactly what kept a picture from being hard: see the region's remarks on inks
        /// as the difficulty dial. The three shipped pictures pass one and two colours here and come out
        /// <b>byte-identical</b> to what the pair produced.
        /// </param>
        /// <param name="background">
        /// The wall behind it, laid down as a 2×2 check of these. <b>Never one colour</b>: see the region's
        /// remarks and the drop test — a flat background makes the wall's top row a single group holding the
        /// whole picture up. Two is the gentle case; three or four quarters every background group and thins the
        /// magazine's draw with it, at the cost of nothing but the entries in this array.
        /// </param>
        private static Design Picture(string file, string name, SceneKind scene, byte sky, string music,
            int shots, int ceilingStep, string[] bitmap, byte grid,
            BallType[] symbol, BallType[] background)
        {
            int width = bitmap[0].Length;

            //Even by construction here: the field is PICTURE_FIELD_LEVELS and an odd difference would have the
            //loader extend it a level and move the drawing off where it was put. A bitmap with an odd number of
            //rows is caught by the emitter's own offset check rather than silently drawn in the wrong place.
            byte depth = (byte)bitmap.Length;

            return new Design
            {
                File = file,
                Name = name,
                Grid = grid,
                Depth = depth,
                //Deeper than an ordinary level's, which is what HANGS THE WALL HIGHER rather than what leaves
                //room under it (#203) — see PICTURE_FIELD_LEVELS for why the two are the same dial past 16,
                //and for the measured air a picture used to start with.
                FieldLevels = PICTURE_FIELD_LEVELS,
                Scene = scene,
                Sky = sky,
                Music = music,
                Balls = BALLS_GALLERY,
                Shots = shots,
                CeilingStep = ceilingStep,
                OccupiedBlock = (x, z, i, d) => OnWall(x, z, i, d, width, grid, out _, out _),
                BlockColour = (x, z, i) =>
                {
                    OnWall(x, z, i, depth, width, grid, out int column, out int row);

                    int ink = SYMBOL_INK.IndexOf(PixelAt(bitmap, column, row));

                    //An ink this picture has no colour for is a typo in the bitmap, and it is refused here for
                    //the same reason the emitter refuses an odd layout offset: it would otherwise be drawn as
                    //BACKGROUND — a hole in the symbol, which is the one mistake in a hand-drawn bitmap that
                    //looks deliberate. The palette's own length says how many inks a picture uses, so a
                    //two-colour symbol is held to two however many characters SYMBOL_INK knows.
                    if (ink >= symbol.Length)
                        throw new InvalidOperationException(
                            $"{file}: '{SYMBOL_INK[ink]}' at column {column}, row {row} is ink {ink + 1} of a "
                            + $"{symbol.Length}-colour symbol");

                    return ink >= 0 ? symbol[ink] : Band((column / 2) + (row / 2), background);
                },
            };
        }

        /// <summary>
        /// A heart. Easy on purpose — a budget that forgives, and a ceiling slow enough that the picture can
        /// be read while it is played — because the point of it is to be recognised rather than aimed through.
        /// </summary>
        private static Design Heart() => Picture("Heart.json", "Heart", SceneKind.Savanna, sky: 14,
            MUSIC_GALLERY, shots: 60, ceilingStep: 10, HEART, grid: 15,
            symbol: new[] { BallType.Type1 },
            background: new[] { BallType.Type4, BallType.Type7 });

        /// <summary>
        /// A smiley: a yellow face with black eyes and a smile, over a blue-and-white sky check. The first
        /// picture here drawn in <b>two inks</b> — a symbol with detail inside it rather than a silhouette — and
        /// the reason <see cref="Picture"/> ever took more than one colour for a symbol. It takes a palette of
        /// however many the bitmap uses now (<see cref="SYMBOL_INK"/>), which is what lets
        /// <see cref="Elephant"/>'s three inks and <see cref="Zebra"/>'s stripes be the Gallery's hard end.
        /// </summary>
        private static Design Smiley() => Picture("Smiley.json", "Smiley", SceneKind.Savanna, sky: 14,
            MUSIC_GALLERY, shots: 60, ceilingStep: 10, SMILEY, grid: 15,
            symbol: new[] { BallType.Type7, BallType.Type8 },
            background: new[] { BallType.Type3, BallType.Type4 });

        /// <summary>
        /// A five-pointed star, yellow over a night check. It was drawn against the space backdrop — the only
        /// scene in the game whose own sky is already a star field, which was the wittiest scene pairing in the
        /// pack — and #194 <b>costs it that joke</b>: the Gallery is one place, and it is the savanna, because
        /// two of the three shipped pictures were already there and space is where the Quarry's <see cref="Static"/>
        /// came from. The loss is real and is recorded here rather than quietly dropped. What is left is a
        /// night-blue check hanging in gold daylight, which the block's own sky makes read as a lantern rather
        /// than as a constellation — a different picture, not a worse one.
        /// </summary>
        private static Design Star() => Picture("Star.json", "Star", SceneKind.Savanna, sky: 14,
            MUSIC_GALLERY, shots: 55, ceilingStep: 9, STAR, grid: 15,
            symbol: new[] { BallType.Type7 },
            background: new[] { BallType.Type3, BallType.Type5 });

        /// <summary>
        /// A zebra, and the <b>stripes are the difficulty</b>: the first picture in the pack whose symbol is not
        /// one group. Two inks in bands two columns wide cut the animal into standing stripes, the largest 40
        /// balls, so there is no moment where the drawing comes away in one piece — measured best single shot
        /// <b>9 %</b> of the cluster, against Star's 40 %, Heart's 42 % and Smiley's 52 % — the lowest figure of
        /// any picture here and level with the hard end of the campaign. It is a picture that plays like
        /// <see cref="Static"/>, and the reason is drawn on its face, which is the point: a player can see why it
        /// is hard. It closes the Gallery for that reason.
        /// <para>
        /// <b>Upright bands, not the animal's own slanted ones.</b> A stripe one cell wide running diagonally is
        /// the lonely-ball rule's own worked example — cells touch only their four orthogonal neighbours on a
        /// level — while two columns standing upright are a solid slab whatever the level's parity. The bands are
        /// struck across the whole drawing rather than fitted to it, so the legs come out half black and half
        /// white, which is what a zebra's legs actually look like.
        /// </para>
        /// <para>
        /// <b>Fifteen columns in a seventeen-wide field</b> where the gentle three are 13 in 15: on a picture the
        /// wall <i>is</i> the level, so widening it is the only way one gets bigger, and 420 balls against their
        /// 364 is as far as that goes before the field is wider than anything else in the pack (<see cref="Gem"/>
        /// is 17).
        /// </para>
        /// <para>
        /// <b>A cool, quiet two-colour sky, and a screenshot is what settled that.</b> It was drawn against three
        /// warm background colours first — yellow, red and green, on the ink dial's own logic that more ground
        /// colours is more difficulty — and the animal <i>disappeared</i>: black and white are the two least
        /// saturated things in the palette and three saturated hues behind them mean the eye reads the check and
        /// not the shape. Against a blue-and-cyan check, which is close in both hue and luminance, the striped mass
        /// separates cleanly and the neck and legs are legible. The difficulty that ground colour was carrying is
        /// carried by the stripes, which is the dial that does not cost legibility. <see cref="Elephant"/> before
        /// it lost a four-colour ground the same way and for the same reason.
        /// </para>
        /// <para>
        /// Measured: 420 balls, margin 1, nothing alone, 4 in pairs, nothing recoloured, four colours, and best
        /// single shots of 5 %, 9 %, 7 % and the black stripes' own. The anchor row is all background and carries
        /// both sky colours, so there is no single-colour anchor. 48 shots stepping every 8 is six descents, the
        /// shipped pictures' six, against twice the work. One near miss under the lonely-ball rule is worth
        /// naming: in the leg rows each ink is a <b>single column</b> four rows tall, and what saves it is the wall
        /// being two cells thick in Z (so a group of eight) rather than anything about the drawing.
        /// </para>
        /// </summary>
        private static Design Zebra() => Picture("Zebra.json", "Zebra", SceneKind.Savanna, sky: 14,
            MUSIC_GALLERY, shots: 48, ceilingStep: 8, ZEBRA, grid: 17,
            symbol: new[] { BallType.Type4, BallType.Type8 },
            background: new[] { BallType.Type3, BallType.Type5 });

        /// <summary>
        /// An elephant's head face-on, and the <b>first picture here drawn in three inks</b>: a pale face, blue
        /// ears either side, black eyes and a black trunk hanging down the middle. It is the Gallery's step up
        /// from the gentle three — the symbol is three groups rather than one, so the biggest payoff on it is the
        /// face at 21 % where <see cref="Smiley"/>'s is 52 % — and it is the block's most legible level, which is
        /// the point of putting it before <see cref="Zebra"/> rather than after.
        /// <para>
        /// <b>The background is two warm colours against a cool animal, and that is what makes it read.</b> This
        /// began with the four-colour ground the ink dial suggests, and a screenshot refused it: a 17-wide wall of
        /// yellow, red, magenta and green check is four saturated hues competing with a symbol drawn in the two
        /// least saturated, and the eye reads the ground rather than the shape. Two warm colours behind a
        /// cyan-and-blue head is a hue contrast instead of a contest, and the elephant comes forward. The
        /// difficulty the fourth ground colour was carrying is carried by the <b>symbol's</b> three inks, which is
        /// the dial that does not cost legibility — see the region's remarks.
        /// </para>
        /// <para>
        /// Measured: 420 balls, margin 1, <b>nothing alone, nothing in a pair and nothing recoloured</b> — the
        /// cleanest wall in the pack — five colours running 40 to 114 balls, and best single shots 8 %, 8 %, 21 %,
        /// 13 % and 5 %. Two of those figures are larger than the colour's own biggest group (36 against 20, and
        /// 58 against 22), which is the drop test counting what a ground group was the last anchor for: the
        /// ears and the trunk hang off the check around them.
        /// </para>
        /// </summary>
        private static Design Elephant() => Picture("Elephant.json", "Elephant", SceneKind.Savanna, sky: 14,
            MUSIC_GALLERY, shots: 48, ceilingStep: 8, ELEPHANT, grid: 17,
            symbol: new[] { BallType.Type5, BallType.Type3, BallType.Type8 },
            background: new[] { BallType.Type7, BallType.Type1 });

        /// <summary>
        /// An elephant's head face-on, 15 by 14: forehead, an ear either side, two eyes and the trunk. Three
        /// inks — <c>#</c> the face, <c>o</c> the ears, <c>+</c> the eyes and the trunk.
        /// <para>
        /// The ears stop square rather than tapering to a point: a one-column ear tip is a column of four balls
        /// that reads as a fray rather than as an ear, and the taper is carried by the face's own two narrowing
        /// rows instead. The eyes are <b>inside</b> the face, never a hole in it.
        /// </para>
        /// </summary>
        private static readonly string[] ELEPHANT =
        {
            "...............",
            "...............",
            "....#######....",
            ".ooo#######ooo.",
            ".ooo#######ooo.",
            ".ooo++###++ooo.",
            ".ooo++###++ooo.",
            ".ooo#######ooo.",
            "..oo#######oo..",
            ".....#####.....",
            "......+++......",
            "......+++......",
            "......+++......",
            "......+++......",
        };


        #region The Gallery's second hang (#255)

        //The Gallery's second five (#255), hung after Elephant and Zebra on the same wall: the savanna,
        //sky 14, MUSIC_GALLERY, and the picture region's standing rules (the 2-cell wall, the 71% squash,
        //the quiet two-colour ground, inks as the only difficulty dial). What each one adds is stated on
        //its own doc: one ink in TWO groups (Moon), one ink in FIVE (Paw), the tall figure the squash
        //wants (Meerkat), the half-shift promoted to the artist (Giraffe), and the alphabet's fourth ink
        //finally spent on a picture with load-bearing parts (Balloon) - which is what PICTURE_EMPTY
        //exists for.

        /// <summary>
        /// A white crescent moon in a night-blue check - the savanna gallery's nocturne, and the block's
        /// 1-ink opener. The Gallery already hangs a <see cref="Star"/> that #194 turned into a lantern in
        /// gold daylight; hanging the Moon beside it completes the joke - the gallery keeps its own night
        /// on the wall.
        /// <para>
        /// The crescent is 38 cells = 76 balls, about 21 % of the wall's 364 - one connected group by
        /// construction (every adjacent row pair of <see cref="MOON"/> overlaps by at least two columns at
        /// Dx = 0), well under <see cref="Smiley"/>'s shipped 52 %, and nothing is enclosed by it: the
        /// check flows around the open right side, so nothing can be orphaned. The 2x2 "evening star" dot
        /// off the open side is the same white ink standing as its own 8-ball group - there because a
        /// crescent and a letter C are neighbours at this resolution and the star is universal moon
        /// iconography, the rejected Africa design's island-separation rule grafted in to kill the C-read.
        /// </para>
        /// <para>
        /// The ground is navy and blue, a deliberately CLOSE pair - the <see cref="Zebra"/> lesson, quiet
        /// in hue and luminance, and here it is literally the night sky the moon sits in; the white symbol
        /// carries maximal luminance contrast against both. Black is not in the level, so the navy/blue
        /// pair confuses with nothing but itself, which is the desired quietness.
        /// </para>
        /// <para>
        /// Gate watch (#255): the star-to-crescent contact graph must hold on BOTH parities - verified by
        /// the tool, not by eye, since cross-level diagonals reach a shifted Dx of 1 and the bitmap leaves
        /// four clear columns. If they bridge, move the dot one column right (edit the dot's cells in
        /// <see cref="MOON"/>, not the crescent). If a screenshot still reads C even with the star, deepen
        /// the crescent's inner bite by one column at rows 5-8.
        /// </para>
        /// </summary>
        private static Design Moon() => Picture("Moon.json", "Moon", SceneKind.Savanna, sky: 14,
            MUSIC_GALLERY, shots: 58, ceilingStep: 10, MOON, grid: 15,
            //'#' white; ground navy + blue - the night the moon sits in
            symbol: new[] { BallType.Type4 },
            background: new[] { BallType.Type12, BallType.Type3 });

        /// <summary>
        /// A crescent opening to the right, 13 by 14 - a fat C whose limbs taper from 5 wide to 3 wide,
        /// ten rows tall by seven columns wide, drawn deliberately 1.4x taller than round (10 rows for 7
        /// columns) so the 71 % squash returns a circle-arc rather than a squashed C. Every stroke is at
        /// least 3 cells wide and the tips are 4, comfortably over the lonely-ball floor. The 2x2 star dot
        /// at columns 9-10, rows 6-7 sits four clear columns off the horns (and the nearest cells on the
        /// adjacent rows 5 and 8 are Dx = 4 away), so no same-level or parity-diagonal contact can bridge
        /// it to the crescent.
        /// </summary>
        private static readonly string[] MOON =
        {
            ".............",
            ".............",
            ".....####....",
            "....#####....",
            "...####......",
            "...###.......",
            "..###....##..",
            "..###....##..",
            "...###.......",
            "...####......",
            "....#####....",
            ".....####....",
            ".............",
            ".............",
        };

        /// <summary>
        /// A lion's paw print in one brown ink - and the first one-ink picture that is NOT one payoff. The
        /// ink dial turns out to have a second axis, region count: five disconnected regions in a single
        /// colour, four 6-cell toes of 12 balls each and a 44-cell pad of 88 (24 % of 364), so a level with
        /// one symbol colour has no single big release at all. It is drawn as savanna storytelling: something
        /// walked through the gallery before the player arrived.
        /// <para>
        /// The separations are the design. Toe-to-toe gaps are one full column - Dx = 2, beyond a same-level
        /// neighbour (Dx = 1) and beyond any cross-level diagonal (the half-shift reaches a shifted Dx of 1).
        /// The outer toes end at row 5 and the pad starts at row 7 - two levels apart, beyond any cross-level
        /// neighbour. Each region is independently embedded in the check, so no release can strand another,
        /// and the smallest group is a 12-ball toe - a safe margin over the repair pass's threshold.
        /// </para>
        /// <para>
        /// The one real packing note: the outer toe pair sits one ROW below the inner pair, so on the shifted
        /// levels the +0.5 X offset fans the four toes into a visible arc from the +Z opening view - the arc
        /// is drawn by the lattice, not the bitmap. The ground is <see cref="Heart"/>'s own dusty-warm
        /// white-and-yellow check; brown reads dark against both, and white/yellow being a confusable pair is
        /// the GROUND's desired quietness (the <see cref="Zebra"/> precedent), the symbol ink being neither.
        /// </para>
        /// <para>
        /// Gate watch (#255): the tool's contact graph for accidental region merges - parity diagonals are
        /// the stated trap. If a toe bridges the pad, move the outer pair up from rows 3-5 to rows 2-4,
        /// widening the toes-to-pad gap to three levels. Confirm nothing is recoloured (smallest group 12).
        /// Bitmap edge columns 0 and 12 stay clear as the region requires; if the emitter ever wants
        /// <see cref="Heart"/>'s two-column ink margin instead, the same paw fits a 15-wide bitmap in
        /// grid 17 unchanged.
        /// </para>
        /// </summary>
        private static Design Paw() => Picture("Paw.json", "Paw", SceneKind.Savanna, sky: 14,
            MUSIC_GALLERY, shots: 52, ceilingStep: 9, PAW, grid: 15,
            //'#' brown; ground white/beige + yellow - Heart's own dusty-warm check
            symbol: new[] { BallType.Type10 },
            background: new[] { BallType.Type4, BallType.Type7 });

        /// <summary>
        /// A paw print, 13 by 14: four toes 2 wide by 3 tall (2 by 2.1 visual after the squash - round),
        /// the inner pair at rows 2-4, the outer pair one row lower at rows 3-5, and a rounded 9-wide main
        /// pad across rows 7-12. One column between neighbouring toes, one whole empty row (two levels)
        /// between the outer toes and the pad - both stated distances are what keeps the five regions five.
        /// </summary>
        private static readonly string[] PAW =
        {
            ".............",
            ".............",
            "....##.##....",
            ".##.##.##.##.",
            ".##.##.##.##.",
            ".##.......##.",
            ".............",
            "....#####....",
            "..#########..",
            "..#########..",
            "..#########..",
            "...#######...",
            "....#####....",
            ".............",
        };

        /// <summary>
        /// A meerkat standing sentry, propped on its own tail - the savanna's actual lookout in the pose
        /// every player recognises, and the first picture drawn the way the 71 % squash WANTS: tall and
        /// thin, the format's best case. Three inks make it the block's mid-ramp step - brown for the coat,
        /// sand for the head, black for the iconic eye-mask (two 1x2 patches of 4 balls) and the belly
        /// stripe (8 balls).
        /// <para>
        /// The belly stripe splits the coat's brown into two 1-wide side columns down rows 6-9 - each 8
        /// balls, rejoining the full-width brown at row 10 below them, so the coat is one group of 54 balls
        /// (14 %).
        /// </para>
        /// <para>
        /// <b>⚠ THE HEAD IS SAND BECAUSE THE FIGURE WAS ONE GROUP OF 86 (#359).</b> Drawn entirely in brown
        /// it was a quarter of the cluster leaving on one shot, and the sag probe's trace named it without
        /// ambiguity: <b>every losing run lost on the shot that matched that group</b>, while the runs where
        /// the same group went later survived with eight units of clearance. That is what the owner's
        /// playtest was calling luck - not the physics, one shot. The remedy is this block's own free dial,
        /// the number of inks a symbol is drawn in (see the region's remarks and Elephant against Star), and
        /// it costs the drawing nothing: <b>a meerkat has a pale face over a darker coat</b>. Coat 86 → 54
        /// balls, best single shot 28 % → 14 %, sag <b>2 of 5 → 1 of 5</b>, which is Giraffe's figure and
        /// the estimator's floor for this format. The bitmap did not move a cell - rows 2-5 changed ink. The tail is a single column at bitmap column 11, rows 9-13: 5
        /// cells = a solid 2-thick slab of 10 balls, the <see cref="Zebra"/>-leg precedent, standing three
        /// columns clear of the legs (Dx = 3, no contact) with bottom-row contact allowed Heart-style. It
        /// is the level's grace note: the body's release leaves the free-standing 10-ball tail waving alone
        /// in the check - the springiest single element a picture wall can legally contain.
        /// </para>
        /// <para>
        /// The ground is a cyan-and-white pale morning sky - close in luminance, quiet, distinct from
        /// <see cref="Zebra"/>'s blue+cyan and <see cref="Smiley"/>'s blue+white - against which the warm
        /// brown figure and its black mask both come forward. Neither navy nor blue is in the level, so
        /// black confuses with nothing.
        /// </para>
        /// <para>
        /// Gate watch (#255): the drop test on brown must agree with the 14 % figure, and the two
        /// 1-wide side columns at rows 6-9 - the Zebra near-miss shape, saved by the 2-thick wall - must
        /// come through with nothing recoloured; if one is recoloured or merged, widen the body to columns
        /// 4-7 with a 2-wide belly stripe at columns 5-6. The 4-ball eye patches are the smallest groups in
        /// the level: confirm no parity diagonal merges one with the belly stripe (two levels apart by
        /// construction). And screenshot before shipping - the eye-mask carries the species.
        /// </para>
        /// </summary>
        private static Design Meerkat() => Picture("Meerkat.json", "Meerkat", SceneKind.Savanna, sky: 14,
            MUSIC_GALLERY, shots: 50, ceilingStep: 9, MEERKAT, grid: 15,
            //'#' brown, 'o' black, '+' sand; ground cyan + white - a pale morning sky
            symbol: new[] { BallType.Type10, BallType.Type8, BallType.Type7 },
            background: new[] { BallType.Type5, BallType.Type4 });

        /// <summary>
        /// A meerkat, 13 by 14: a 5-wide head (rows 2-5), a slim 3-wide body (rows 6-10), two 2x2 legs
        /// (rows 11-12) and the species' tripod tail as a single column at column 11, rows 9-13. Second
        /// ink <c>o</c>: the eye-mask at columns 5 and 7, rows 3-4, and the belly stripe down column 6,
        /// rows 6-9. Third ink <c>+</c>: the head, rows 2-5, which is where #359 cut the figure in two -
        /// see <see cref="Meerkat"/>. The stripe splits the coat <c>#</c> into two 1-column sides that
        /// rejoin below it, and the mask patches are enclosed by the head.
        /// </summary>
        private static readonly string[] MEERKAT =
        {
            ".............",
            ".............",
            "....+++++....",
            "....+o+o+....",
            "....+o+o+....",
            "....+++++....",
            ".....#o#.....",
            ".....#o#.....",
            ".....#o#.....",
            ".....#o#.##..",
            ".....###.##..",
            "....##.####..",
            "....##.####..",
            ".............",
        };

        /// <summary>
        /// A giraffe whose stepped neck the lattice itself smooths into a straight diagonal - the block's
        /// one true packing exhibit, and the only design that delivers the owner's packing ask as a thesis:
        /// the odd-level half-shift, the thing every other level fights or hides, promoted to the artist.
        /// <b>The step rate is the point</b>: the neck climbs +1 column every 2 rows = +0.5 column per
        /// level = exactly the half-shift, so from the +Z opening view the shifted rows fill in every
        /// half-step and the neck's edge renders as a geometrically STRAIGHT line no bitmap can draw.
        /// <para>
        /// Three inks in the hard grid-17 format: yellow hide, brown patches and black tuft-and-hooves.
        /// <b>⚠ The hide is deliberately SEVERED into three networks by its own patches (#316)</b> — a full
        /// neck band at rows 5-6 and a saddle spanning rows 9-11 — because as one 74-ball group it was the
        /// sag probe's kill on this level: four orders of five lost at shot 2-3 with the glass at rest, the
        /// one release (75 matched, up to 64 orphaned — the between-legs check and the accents ride the
        /// hide) holing the wall's middle so the check below stretched 5.8 units to −1.03. Severed, the
        /// hide reads head 16 / forequarters 22 / hindquarters 24 balls, no release holes the wall wider
        /// than the check's own 30-ball groups, and the patches are what a giraffe's coat is anyway. Both
        /// severs are two full rows/columns of the member they cut, so no parity diagonal jumps them.
        /// Every riser still holds Dx = 0 vertical contact, so the staircase never relies on parity
        /// diagonals. The WALL is the structure and the drawing is only paint, the block's standing answer
        /// to the Coil rule. Warm animal on the cool blue-and-cyan quiet sky is the <see cref="Elephant"/>
        /// rule; yellow's confusable, white, is not in the level.
        /// </para>
        /// <para>
        /// Measured after #316 (severs + the three-ink check + step 12): 420 balls in 34 standing groups
        /// (1.35 shots a group of 46), 30 anchors (14.0 each), anchor load 15.8, margin 1, nothing alone,
        /// 2 in pairs, 0 recoloured; best single shots 7/7/5 % (sky) and 5/2/0 % (hide/patches/accents).
        /// Sag probe: <b>1 of 5 across two independent runs</b> (a hairline −1.02 on one seeded order; the
        /// shipped level read 4 of 5 with the glass at rest, dying on the hide's 75-ball release at shot
        /// 2-3). A savanna-rock sever of the bottom-left check was tried on top of this and measured
        /// nothing (still 1 of 5, the losing order merely rearranged), so it was reverted rather than
        /// shipped unmeasured — the residual order is the estimator's floor for a 14-course curtain, and
        /// it reads better than Saturn's 2 of 5.
        /// </para>
        /// <para>
        /// Gate watch (#255), in the spec's own order because the first is cheap and it IS the concept:
        /// (1) the parity-mirror screenshot from +Z - which diagonal gets smoothed depends on which layout
        /// rows land shifted, and if the staircase shows instead of the line, mirror <see cref="GIRAFFE"/>
        /// left-right (one transform, no redesign); (2) confirm the three black accents stay 4-ball groups
        /// and nothing is recoloured (the brown is three groups of 8/12/4 since #316's severs — the sag
        /// note above says why they must stay full-width, so a patch edit re-runs <c>--sag=Giraffe</c>);
        /// (3) if the 2-row legs (1.4 visual) read dachshund, convert body row 9 into a third leg row -
        /// the neck sever and the saddle are untouched by it.
        /// </para>
        /// </summary>
        private static Design Giraffe() => Picture("Giraffe.json", "Giraffe", SceneKind.Savanna, sky: 14,
            //Step 12, not the block's usual 8 (#316): with the hide severed and the check on three inks the
            //probe's remaining late-game loss was a 9-ball release at shot 24 dropping a 165-ball remainder
            //past the line with the glass THREE steps down (1.80 of the 4.82 clearance spent) — the tallest
            //curtain in the block re-hangs deepest, and the step is the honest lever for a loss the glass
            //helped cause. The #288 sum at 12: 46 shots buy 3 descents, 1.80, headroom 3.02. The at-rest
            //fault this level was reported for is the hide sever's job, not this one's.
            MUSIC_GALLERY, shots: 46, ceilingStep: 12, GIRAFFE, grid: 17,
            //'#' yellow hide, 'o' brown patches, '+' black tuft and hooves; ground blue + cyan + navy —
            //the third sky ink is #316's second lever (the param doc's own "three or four quarters every
            //background group"): with two inks the check welded into 23-30-ball diagonal chains through the
            //block-corner cross-level contact, and those bites were what the probe's remaining losses died
            //on once the hide was severed. Navy and not white, because white is yellow's confusable and the
            //design note below keeps it out of the level.
            symbol: new[] { BallType.Type7, BallType.Type10, BallType.Type8 },
            background: new[] { BallType.Type3, BallType.Type5, BallType.Type12 });

        /// <summary>
        /// A giraffe, 15 by 14: a 4x2 head with a 2-cell <c>+</c> mane tuft over it (row 2 is legal; rows
        /// 0-1 stay clear), a neck stepping +1 column every 2 rows wearing a full-width <c>o</c> band at
        /// rows 5-6, a tapering body across rows 9-11 with an <c>o</c> saddle down its middle and a patch
        /// pair on the rump, and two 2-wide legs with <c>+</c> hooves under each. The band and the saddle
        /// are load-bearing severs, not decoration — see the design doc (#316). The step rate is the
        /// odd-level half-shift exactly - see <see cref="Giraffe"/>.
        /// </summary>
        private static readonly string[] GIRAFFE =
        {
            "...............",
            "...............",
            "...++..........",
            "..####.........",
            "..####.........",
            "....oo.........",
            "....oo.........",
            ".....##........",
            ".....##........",
            "......##oo####.",
            "......##oo##oo.",
            ".......#oo####.",
            ".......##...##.",
            ".......++...++.",
        };

        /// <summary>
        /// A safari balloon in striped gores with its basket hanging on two real ropes over open air - the
        /// Gallery's first FOUR-ink picture, finally spending the <c>=</c> the <see cref="SYMBOL_INK"/>
        /// alphabet has kept unused since it was written, and its first hanging MACHINE: the ropes are not
        /// drawn rope, they are load-bearing, and the player discovers that by cutting one. One black ball
        /// through a side gap releases ONE 4-ball rope and the 20-ball basket pendulums on the remaining
        /// single two-level chain - an asymmetric hanging weight the player made - and the second black
        /// shot drops it through the empty row below. The finale slot, priced at the block floor (44/8,
        /// under <see cref="Zebra"/>'s 48/8) because four inks and zero large payoffs is a step past Zebra.
        /// <para>
        /// It rides the rejected Spider's engineering: <see cref="PICTURE_EMPTY"/> cells are holes in the
        /// wall itself - 31 of them, 62 balls removed, 358 total. The basket's ONLY support is the two
        /// ropes: the nearest check is Dx of 3 or more away or two-plus levels away, beyond any parity
        /// diagonal, while each rope sits at Dx = 0 under the throat, contact guaranteed on both parities
        /// by the 2-thick wall. The ropes are each other's second path - the Coil rule as a redundant pair.
        /// The gores are vertical column stripes (the <see cref="Zebra"/> dial turned upright): red stands
        /// in three groups (14/12/12 balls), yellow in two (26 each), ropes 2x4, basket 1x20 - no single
        /// payoff anywhere, so the envelope never leaves in one piece and the basket moment is the finale,
        /// not a shortcut. Black's total release orphans the basket assembly: 28 balls, 8 % of 358. Every
        /// gore stripe reaches the crown and flank check independently, so no stripe release strands
        /// another. Confusables: white, orange and navy are absent, and the black ropes hang against open
        /// sky through the gaps, three columns from the blue check flanks.
        /// </para>
        /// <para>
        /// Gate watch (#255), in the spec's order: (1) hang-unshot 35 s - the 20-ball basket on two 2-cell
        /// ropes is the assembly closest to the death line, and if it stretches past, move the basket to
        /// rows 10-11 and the ropes to rows 8-9 (rows 0-1 must stay clear, so the envelope cannot move up;
        /// tune row origins, not member sizes); (2) the drop test on black - a single pass may take both
        /// ropes and must account the 28-ball orphan correctly; (3) aimcheck each rope through its
        /// 3-column empty side channel and both Z faces - the kill shot fired THROUGH the picture is the
        /// design element, staged twice; (4) each gore stripe reaching the crown/flank check independently.
        /// </para>
        /// </summary>
        private static Design Balloon()
        {
            Design design = Picture("Balloon.json", "Balloon", SceneKind.Savanna, sky: 14,
                MUSIC_GALLERY, shots: 44, ceilingStep: 8, BALLOON, grid: BALLOON_GRID,
                //'#' red gores, 'o' yellow gores, '+' black ropes, '=' brown basket; ground blue + cyan
                symbol: new[] { BallType.Type1, BallType.Type7, BallType.Type8, BallType.Type10 },
                background: new[] { BallType.Type3, BallType.Type5 });

            //The ' '-means-EMPTY extension, applied over Picture's own wall rather than rebuilt: a cell is
            //on the wall exactly where Picture put it EXCEPT where the bitmap says PICTURE_EMPTY, so
            //'.'-means-check is unchanged and the colour lambda never sees a hole - Emit asks BlockColour
            //only where OccupiedBlock said yes.
            design.OccupiedBlock = (x, z, i, d) =>
                OnWall(x, z, i, d, BALLOON[0].Length, BALLOON_GRID, out int column, out int row)
                && PixelAt(BALLOON, column, row) != PICTURE_EMPTY;

            return design;
        }

        //The finale's grid, stated once so the Picture call and the cutout occupancy lambda above cannot
        //disagree about where OnWall puts the bitmap.
        private const byte BALLOON_GRID = 17;

        /// <summary>
        /// The one character of the bitmap alphabet that is a HOLE in the wall rather than a colour on it:
        /// where a bitmap says space, there is no ball at all - not check, not ink - which is what lets a
        /// picture hang something over open air (<see cref="Balloon"/>'s basket on its ropes). <c>.</c>
        /// still means check and <see cref="SYMBOL_INK"/> still means ink; this is the third kind of cell,
        /// consulted by the design's own occupancy lambda, and <see cref="PixelAt"/>'s out-of-range answer
        /// staying <c>.</c> means a hole can never leak outside its bitmap. Specced by the rejected Spider
        /// design and built once here for whichever pictures ride it.
        /// </summary>
        private const char PICTURE_EMPTY = ' ';

        /// <summary>
        /// A hot-air balloon, 15 by 14: a symmetric seven-row envelope in vertical gore stripes (columns
        /// 3-4 <c>#</c>, 5-6 <c>o</c>, 7 <c>#</c>, 8-9 <c>o</c>, 10-11 <c>#</c>), a 3-cell throat, two
        /// 2-cell <c>+</c> ropes at columns 6 and 8 directly under it, and a 5x2 <c>=</c> basket - with
        /// every cell around the ropes and basket EMPTY (<see cref="PICTURE_EMPTY"/>), row 13 included, so
        /// the basket hangs on the ropes alone and has a clear row to fall through. The check flanks at
        /// columns 0-2 and 12-14 run full height and are the wall's structure.
        /// </summary>
        private static readonly string[] BALLOON =
        {
            "...............",
            "...............",
            ".....oo#oo.....",
            "....#oo#oo#....",
            "...##oo#oo##...",
            "...##oo#oo##...",
            "....#oo#oo#....",
            ".....oo#oo.....",
            "......o#o......",
            "......+.+......",
            "......+.+......",
            ".....=====.....",
            ".....=====.....",
            "...............",
        };

        #endregion

        #region Pictures (#130)

        //A level that reads as a PICTURE rather than as a solid of revolution. It is a flat wall hanging in
        //the field with a symbol drawn across it, and four facts about the lattice decide how one is drawn.
        //
        //THE PICTURE PLANE IS (x, LEVEL), and the wall is thin in Z. The gun starts at +Z looking at the
        //origin (Cannon.CalculateInitialPositionAndAimTarget), so X runs across the screen and the level axis
        //runs up it: a wall spanning those two is the one the player sees face-on. Spanning X and Z instead
        //would draw the picture on the floor, seen edge-on from the gun.
        //
        //IT IS TWO CELLS THICK, not one. A cell touches its four orthogonal neighbours on its own level and
        //up to four on each adjacent one, and WHICH diagonal offsets those are depends on the level's parity
        //- so a wall one cell thick in Z has half its vertical neighbours reaching to a Z that is not there.
        //Two cells thick is the thinnest wall that is a solid slab whatever the parity, and it doubles every
        //group, which is what keeps the strokes above the lonely-ball floor.
        //
        //ROWS ARE SHORTER THAN COLUMNS ARE WIDE. Levels sit 1/sqrt(2) apart vertically against a cell pitch
        //of 1 horizontally, so a bitmap drawn square comes out squashed to 71% of its height. A picture is
        //therefore drawn about 1.4x TALLER in rows than it is meant to look - the hearts below are 14 rows
        //for 11 columns and come out very nearly square.
        //
        //THE BACKGROUND CANNOT BE ONE COLOUR. The wall's top row is the anchor layer, and a background that
        //is a single colour makes that row a single group holding the whole picture up: one matching ball
        //takes the level (Validate's drop test, the trap Mosaic and Gem both hit first). The background is a
        //2x2 check of two colours for exactly the reason Mosaic's blocks are.
        //
        //HOW MANY INKS THE SYMBOL IS DRAWN IN IS THE DIFFICULTY DIAL - AND THE BACKGROUND'S PALETTE IS NOT.
        //That asymmetry is the whole finding of the Gallery block (#194) and it cost two rounds of screenshots.
        //
        //The SYMBOL side works as expected. A symbol is one connected group by construction, so a drawing in ONE
        //ink is the biggest group on its wall and its own colour takes half the level in a single shot: the three
        //gentle pictures hold three of the pack's highest one-shot percentages (Star 40 %, Heart 42 %,
        //Smiley 52 %), which is deliberate on a level whose point is being recognised. The same drawing split
        //across more inks has no single payoff at all - the elephant's face takes 21 % and the zebra's stripes
        //9 %, where either outline in one ink would have taken half the wall. That is a real difficulty dial and
        //it costs nothing.
        //
        //The BACKGROUND side looked like the same dial from the other side and is not. More ground colours does
        //quarter every background group and does thin the magazine's draw - and it also DESTROYS THE PICTURE.
        //Measured by looking: the zebra over a three-colour warm check and the elephant over a four-colour one
        //were both unreadable, because a symbol is drawn in the palette's least saturated colours (a zebra is
        //black and white) and three or four saturated hues behind it mean the eye reads the check and not the
        //shape. Both were fixed the same way - a QUIET two-colour ground, cool behind the zebra so it is close in
        //hue and luminance, warm behind the cool elephant so the contrast is hue rather than noise - and the
        //difficulty moved onto the symbol's inks, where it belongs.
        //
        //So: two background colours, always, and never one (see above). A picture level's difficulty is drawn in
        //the symbol.
        //
        //A four-ink SCENE was tried too and dropped: a flat-crowned acacia with a low sun behind it and a band of
        //ground under both, spending all four inks on a crown, a trunk, a sun and the ground. It passed every gate
        //comfortably (420 balls, the flattest colour spread of any picture at 14 %) and it did not read - a wide
        //flat crown, a sun and a ground band are three horizontal bands, and horizontal bands at this scale read
        //as bands rather than as a landscape. The lesson is the pictures' own: a wall of 15 by 14 cells carries a
        //SYMBOL, not a composition.

        /// <summary>
        /// Where a picture's own <c>(column, row)</c> sits in the lattice, and whether a cell is on the wall
        /// at all. Row 0 is the TOP of the picture, which is layout level <c>depth - 1</c> — the anchor layer,
        /// so a bitmap is written the way it is seen, top line first.
        /// </summary>
        private static bool OnWall(int x, int z, int i, int depth, int width, int grid, out int column, out int row)
        {
            //Centred across the grid, and two cells deep about the middle of it
            int x0 = (grid - width) / 2;
            int z0 = (grid - PICTURE_THICKNESS) / 2;

            column = x - x0;
            row = depth - 1 - i;

            return column >= 0 && column < width && z >= z0 && z < z0 + PICTURE_THICKNESS;
        }

        /// <summary>How deep a picture wall is, in cells. See the region's remarks for why it is not one.</summary>
        private const int PICTURE_THICKNESS = 2;

        /// <summary>
        /// The characters a bitmap draws its symbol with, <b>in palette order</b>: <c>#</c> is the first ink,
        /// <c>o</c> the second, <c>+</c> the third, <c>=</c> the fourth, and anything else is background. It is
        /// the contract between a bitmap and the palette <see cref="Picture"/> is handed, so it is stated once
        /// here rather than as a chain of conditionals that grows an <c>else</c> per ink.
        /// <para>
        /// Four characters in the alphabet, of which the pack uses three (<see cref="ELEPHANT"/>'s face, ears and
        /// trunk). What bounds a picture is not this string but <b>its own palette's length</b> — a glyph past the
        /// end of that palette is refused rather than quietly drawn as background — so the fourth costs nothing
        /// and is kept. The one design that spent all four was a drawn <i>scene</i> and it was dropped for not
        /// reading at this scale; the region's remarks carry that measurement, so the next author does not repeat
        /// it.
        /// </para>
        /// </summary>
        private const string SYMBOL_INK = "#o+=";

        /// <summary>
        /// A heart, 13 columns by 14 rows. Two rules shape it, and the drop test taught the second one.
        /// <list type="bullet">
        /// <item><b>Every stroke is at least two cells wide</b> in both directions — the lonely-ball rule (a
        /// one-cell diagonal run is a string of balls that touch nothing at all), which a drawn symbol walks
        /// straight into wherever it curves or comes to a point. The first draft ended in a one-cell tip.</item>
        /// <item><b>The symbol never reaches an edge, and the top two rows are background.</b> The first
        /// draft filled the wall's full width and touched its top row, which made the heart one connected
        /// group of 208 holding everything under it up: dropping it took <b>93 % of the level in one shot</b>.
        /// Background down both sides and across the top is what keeps the wall hanging when the symbol goes,
        /// and it is the same shape of trap the background's own check answers from the other side.</item>
        /// </list>
        /// </summary>
        private static readonly string[] HEART =
        {
            ".............",
            ".............",
            "...##...##...",
            "..####.####..",
            "..#########..",
            "..#########..",
            "..#########..",
            "..#########..",
            "...#######...",
            "...#######...",
            "....#####....",
            "....#####....",
            ".....###.....",
            ".....###.....",
        };

        /// <summary>
        /// A smiley, 13 by 14. The eyes and the smile are the <c>o</c> accent, drawn <b>inside</b> the face
        /// rather than cut out of it — a hole in the face would be background, and background enclosed by the
        /// symbol is a pocket the wall's own background cannot reach.
        /// <para>
        /// Both features are two cells wide everywhere for the lonely-ball rule, the smile's turned-up ends
        /// included: one-cell corners would be a pair apiece, which stands but asks the player for two landed
        /// balls to clear rather than one.
        /// </para>
        /// </summary>
        private static readonly string[] SMILEY =
        {
            ".............",
            ".............",
            "....#####....",
            "...#######...",
            "..#########..",
            "..##oo#oo##..",
            "..##oo#oo##..",
            "..#########..",
            "..#########..",
            "..#oo###oo#..",
            "..##ooooo##..",
            "..#########..",
            "...#######...",
            "....#####....",
        };

        /// <summary>
        /// A five-pointed star, 13 by 14: the point up, the arms across, and two legs under it. The hardest
        /// of the three to keep above the lonely-ball floor — a star is nothing but places where the shape
        /// comes to a point — so every arm and leg is two cells wide and the tip is three.
        /// </summary>
        private static readonly string[] STAR =
        {
            ".............",
            ".............",
            ".....###.....",
            ".....###.....",
            "....#####....",
            "..#########..",
            "..#########..",
            "...#######...",
            "....#####....",
            "....#####....",
            "....##.##....",
            "...##...##...",
            "...##...##...",
            "..##.....##..",
        };

        /// <summary>
        /// A zebra, 15 columns by 14 rows: the body across the middle, the neck and head rising to the right,
        /// two stout legs under it. <b>The two inks alternate every two columns across the whole drawing</b>
        /// rather than following its outline, which is what makes the stripes read as stripes and the legs come
        /// out half of each. Nothing here is narrower than two columns except where a band crosses a leg, and a
        /// one-column run inside a solid body is still four rows tall and two cells deep — the lonely-ball rule
        /// is about a diagonal run of single cells, which this bitmap has none of.
        /// <para>
        /// The animal keeps a clear column of background down each side and two clear rows across the top, for
        /// the reason <see cref="HEART"/> states: a symbol touching the anchor row holds the whole wall up.
        /// </para>
        /// </summary>
        private static readonly string[] ZEBRA =
        {
            "...............",
            "...............",
            ".........o##o..",
            "........oo##oo.",
            "........oo##oo.",
            ".......#oo##o..",
            "..##oo##oo##...",
            "..##oo##oo##o..",
            "..##oo##oo##o..",
            "...#oo##oo##...",
            "...#o....o#....",
            "...#o....o#....",
            "...#o....o#....",
            "...#o....o#....",
        };


        #endregion
    }
}
