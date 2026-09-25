using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>The Silhouettes</b> (#491): ten picture levels whose shapes were drawn by the local image generator
    /// rather than by hand, hung under the aurora. Its designs, and the helpers no other block's designs use; the
    /// play order is <see cref="Main"/>'s and the block's name, music and ball style are in the tables there, as
    /// every other block file states. The picture machinery itself — <see cref="Picture"/>, the wall, the check —
    /// is the Gallery's (<c>Block02_Gallery.cs</c>) and is used unchanged.
    /// </summary>
    internal static partial class Program
    {
        #region The silhouette levels (#491)

        //WHERE THE SHAPES CAME FROM. Every bitmap here started as a render: Z-Image-Turbo drew "a solid black
        //silhouette of <thing>, centred on a plain white background, flat vector icon" and the design-references
        //skill's silhouette-to-picture.py quantized it onto the wall's grid (area average, the sqrt(2) row
        //stretch, a fill threshold). The prompts are C:\Users\panrd\AI\sd\prompts-491-silhouettes.json and
        //prompts-491-lying.json on the owner's desktop; the images stay out of this public repository.
        //
        //THEN EVERY BITMAP WAS FINISHED BY HAND, and that is not a failure of the pipeline but its measured
        //limit. The script's area average is what a silhouette looks like at this resolution, and at this
        //resolution a thin part — an umbrella's shaft, a bell's crown loop, an anchor's shank — covers less than
        //half of any cell it crosses and disappears at every fill threshold that keeps the body clean (#491's
        //first round: the umbrella came out a canopy with no handle, the anchor in eight pieces). So the render
        //decides the PROPORTIONS and the outline, and a thin part it lost is drawn back one cell wide. That is
        //the same bargain Zebra's legs already make: a single column of a two-cell-thick wall is a chain of
        //balls, not a string of loners.
        //
        //FOURTEEN ROWS AT MOST, and the script's own cap (18) is the wrong one to use here. PICTURE_FIELD_LEVELS
        //is 18 and it is what gives a 14-row wall its 3.96 units of air over the line; an 18-row bitmap would fill
        //the field to its floor and cross the line on its first or second descent. So every shape is drawn at 14
        //rows or fewer, and the TALL subjects of #491's first round (key, guitar, rocket, cat), which the row cap
        //and the stretch starved to five or six columns, were re-rendered LYING DOWN, where the wall's 15 columns
        //are the long axis: the key and the guitar on their sides, the cat walking and the rocket flying level. The
        //anchor would not lie down and read, so it is drawn upright at the render's proportions, narrower than
        //the wall.
        //
        //THE CHAPTER'S LOOK IS ONE IDEA: black cut-outs against a lit screen, a shadow play under the northern
        //lights. The silhouette is always black (Type8) and the ground is always a quiet two-colour check of pale
        //colours (white, cyan, yellow), which is the Gallery's legibility rule held to its strictest: the
        //darkest colour in the palette against the palest, nothing saturated competing with the shape. Colour
        //arrives INSIDE the silhouette as the chapter goes on — a teapot's red lid, a crown's jewels, a guitar's
        //neck — the way a shadow puppet is painted, and that is also the chapter's difficulty dial (see the
        //Pictures region: how many inks the symbol is drawn in is the difficulty, and the ground's palette is
        //not). One ink for the three openers, two in the middle, three at the end.
        //
        //WHY THE AURORA. It is one of the eight backdrops no chapter had ever used, it was rebuilt from
        //references in #462 into a snow-floored boreal night, and its light comes from the whole sky rather than
        //from a sun low on one side — so a flat wall facing the gun is lit evenly across its width, which a
        //picture needs and a dusk scene does not give it. And it is the step the light ramp was missing: the
        //Tower's violet dusk, then night under the aurora, then the Reveal's cavern underground.

        /// <summary>
        /// One silhouette level: a <see cref="Picture"/> hung under the aurora in the chapter's material, drawn in
        /// black with <paramref name="paint"/> as the details' inks (<see cref="SYMBOL_INK"/>'s second on), over a
        /// two-colour ground.
        /// </summary>
        private static Design Silhouette(string name, int shots, int ceilingStep, string[] bitmap,
            BallType[] paint, BallType groundA, BallType groundB)
        {
            BallType[] symbol = new BallType[1 + paint.Length];
            symbol[0] = SILHOUETTE_INK;
            paint.CopyTo(symbol, 1);

            Design design = Picture(name + ".json", name, SceneKind.Aurora, SKY_SILHOUETTES, MUSIC_SILHOUETTES,
                shots, ceilingStep, bitmap, grid: (byte)(bitmap[0].Length + 2),
                symbol: symbol, background: new[] { groundA, groundB });

            design.Balls = BALLS_SILHOUETTES;

            //NO GROUND UNDER THE DRAWING: a background cell with ink anywhere above it in its column is left
            //EMPTY, so the wall's lower edge is the silhouette's own outline and the shape hangs free like a
            //cut-out. It is a load rule before it is a look, and the sag probe is what said so. The first cut kept
            //the Gallery's full rectangle, and on the three shortest walls (Fish, Key, Rocket) the band of check
            //under the drawing hung by nothing but the margins once the black went: the probe cleared the body
            //in one or two shots and the band swung down through the line - "sagged with the glass at rest", 4
            //of 5 runs on Fish and Rocket. Moon and Paw in the Gallery carry the same band and the same finding
            //(3 and 2 of 5), which is why this is written here rather than as a fix to one bitmap. Holes inside
            //a shape (the key's bow, the teapot's handle) are under ink too, so they come out as real holes.
            string[] drawing = bitmap;
            design.OccupiedBlock = (x, z, i, d) =>
                OnWall(x, z, i, d, drawing[0].Length, design.Grid, out int column, out int row)
                && !UnderInk(drawing, column, row);

            return design;
        }

        /// <summary>
        /// Whether a background cell of <paramref name="bitmap"/> has ink above it in its own column — the cells
        /// <see cref="Silhouette"/> leaves empty.
        /// </summary>
        private static bool UnderInk(string[] bitmap, int column, int row)
        {
            if (SYMBOL_INK.IndexOf(PixelAt(bitmap, column, row)) >= 0) return false;

            for (int above = row - 1; above >= 0; above--)
                if (SYMBOL_INK.IndexOf(PixelAt(bitmap, column, above)) >= 0) return true;

            return false;
        }

        /// <summary>What every silhouette is cut from: black, the one ink the chapter never changes.</summary>
        private const BallType SILHOUETTE_INK = BallType.Type8;

        /// <summary>
        /// Inert: the aurora replaces the sky and states its own light rig (<c>SceneRenderer.ReplacesSky</c>), so
        /// the dome number changes nothing. Every level names the same one anyway, so DescribeBlock has something
        /// to agree with — the Grid's and the Mirage's own practice.
        /// </summary>
        private const byte SKY_SILHOUETTES = 13;

        private static readonly BallType[] NO_PAINT = { };

        /// <summary>
        /// A fish, and the chapter's opener: the plainest shape in it and the easiest to name, one black ink over
        /// a white-and-cyan check that reads as water. The whole body is one group, so the first matching ball
        /// takes half the wall — the Heart's own gentleness, on purpose.
        /// </summary>
        private static Design Fish() => Silhouette("Fish", shots: 60, ceilingStep: 10, FISH, NO_PAINT,
            BallType.Type4, BallType.Type5);

        private static readonly string[] FISH =
        {
            "...............",
            "...............",
            "......###......",
            ".....####......",
            "...######...#..",
            "..########.##..",
            "..###########..",
            "..###########..",
            "..########.##..",
            "...######...#..",
            ".....###.......",
            "...............",
        };

        /// <summary>
        /// An umbrella, the first render that came back as a canopy with no handle (#491) — the shaft and the hook
        /// are drawn back one cell wide, and they are what makes it an umbrella rather than a hat. One ink.
        /// </summary>
        private static Design Umbrella() => Silhouette("Umbrella", shots: 58, ceilingStep: 10, UMBRELLA, NO_PAINT,
            BallType.Type4, BallType.Type7);

        private static readonly string[] UMBRELLA =
        {
            "...............",
            "...............",
            ".......#.......",
            "......###......",
            "....#######....",
            "...#########...",
            "..###########..",
            "..#.##.#.##.#..",
            ".......#.......",
            ".......#.......",
            ".......#.......",
            ".......#.......",
            "....#..#.......",
            "....####.......",
        };

        /// <summary>
        /// A bell, flared at the lip, with its crown loop above and the clapper showing below. One ink; the last
        /// of the three gentle openers.
        /// </summary>
        private static Design Bell() => Silhouette("Bell", shots: 56, ceilingStep: 9, BELL, NO_PAINT,
            BallType.Type5, BallType.Type4);

        private static readonly string[] BELL =
        {
            "...............",
            "...............",
            ".......#.......",
            "......###......",
            "......###......",
            ".....#####.....",
            "....#######....",
            "....#######....",
            "....#######....",
            "...#########...",
            "...#########...",
            "..###########..",
            ".#############.",
            "......###......",
        };

        /// <summary>
        /// A teapot, spout left and handle right, and the first silhouette with paint on it: a red lid. The lid is
        /// small and sits on top, so it is the one part of the pot that can be cut away cleanly, and a player who
        /// takes it first has spent a shot on four balls — the first choice of order the chapter asks for.
        /// </summary>
        private static Design Teapot() => Silhouette("Teapot", shots: 54, ceilingStep: 9, TEAPOT,
            new[] { BallType.Type1 }, BallType.Type4, BallType.Type7);

        private static readonly string[] TEAPOT =
        {
            "...............",
            "...............",
            ".......o.......",
            "......ooo......",
            "....#######....",
            ".#..#######.##.",
            ".##.#######..#.",
            "..##########.#.",
            "...#########.#.",
            "...###########.",
            "...#########...",
            "...#########...",
            "....#######....",
            ".....#####.....",
        };

        /// <summary>
        /// A cat walking right, tail up — a tall subject in #491's first round that came back as "an animal, not a
        /// cat" at eight columns, re-rendered walking so its length lies along the wall. Black, with one yellow eye:
        /// the first paint in the chapter, and the smallest — two balls, which a player can take or leave.
        /// </summary>
        private static Design Cat() => Silhouette("Cat", shots: 54, ceilingStep: 9, CAT,
            new[] { BallType.Type7 }, BallType.Type4, BallType.Type5);

        private static readonly string[] CAT =
        {
            "...............",
            "...............",
            ".#.............",
            ".#.............",
            ".#.........#.#.",
            ".##........###.",
            "..#.......##o#.",
            "..############.",
            "..###########..",
            "..##########...",
            "..##########...",
            "..##..##..##...",
            ".##...##..##...",
            ".#....##...##..",
        };

        /// <summary>
        /// A cartoon rocket flying right: the render drew it climbing on a diagonal, which a wall of fifteen by
        /// twelve cannot hold, so it is laid level at the render's proportions. Black hull, a lit yellow porthole
        /// and an orange flame out of the tail: three inks, the flame hanging off the wall's left edge where the
        /// shape is thinnest.
        /// </summary>
        private static Design Rocket() => Silhouette("Rocket", shots: 50, ceilingStep: 9, ROCKET,
            new[] { BallType.Type7, BallType.Type9 }, BallType.Type5, BallType.Type4);

        private static readonly string[] ROCKET =
        {
            "...............",
            "...............",
            "..##...........",
            "..###.#####....",
            "...#########...",
            "..+#########...",
            "..+####oo####..",
            "..+####oo####..",
            "..+#########...",
            "...#########...",
            "..###.#####....",
            "..##...........",
        };

        /// <summary>
        /// An old door key lying on its side — one of the tall subjects of #491's first round that came back as a
        /// stalk with a ring, re-rendered lying down so the wall's width is its length. Black bow, red shank and
        /// bit: two inks that split the key where a key is naturally two parts.
        /// </summary>
        private static Design Key() => Silhouette("Key", shots: 50, ceilingStep: 10, KEY,
            new[] { BallType.Type1 }, BallType.Type5, BallType.Type4);

        private static readonly string[] KEY =
        {
            "...............",
            "...............",
            "...##..........",
            "..####.........",
            "..#..#ooooooo..",
            "..#..#ooooooo..",
            "..####...o.oo..",
            "...##....o.oo..",
            ".........oooo..",
            "...............",
        };

        /// <summary>
        /// A crown, five points and a cross on top, set with jewels: black, with a red cross and red and blue
        /// stones. Three inks, and the stones are small — the kind of detail a careless shot leaves stranded.
        /// </summary>
        private static Design Coronet() => Silhouette("Coronet", shots: 50, ceilingStep: 8, CORONET,
            new[] { BallType.Type1, BallType.Type3 }, BallType.Type4, BallType.Type7);

        private static readonly string[] CORONET =
        {
            "...............",
            "...............",
            ".......o.......",
            "......ooo......",
            ".#.....#.....#.",
            ".##...###...##.",
            ".###..###..###.",
            "..###.###.###..",
            "..###########..",
            "..#o##+++##o#..",
            "..###########..",
            "...#########...",
            "...#########...",
            "...#########...",
        };

        /// <summary>
        /// An acoustic guitar lying on its back, rendered that way for the same reason as the key. Black body,
        /// red neck and sound hole, orange headstock: three inks, the neck running the whole right half of the
        /// wall, so the level is a long thin reach as well as a body.
        /// </summary>
        private static Design Guitar() => Silhouette("Guitar", shots: 48, ceilingStep: 8, GUITAR,
            new[] { BallType.Type1, BallType.Type9 }, BallType.Type4, BallType.Type5);

        private static readonly string[] GUITAR =
        {
            "...............",
            "...............",
            "..####.........",
            ".######.###....",
            ".###########.++",
            ".####oo####ooo+",
            ".####oo####ooo+",
            ".###########.++",
            ".######.###....",
            "..####.........",
        };

        /// <summary>
        /// An anchor, and the chapter's closer. The render drew it tall and thin and the quantizer broke its arms
        /// into dotted islands (#491's first round); it is drawn here from the render's shape, eleven columns wide,
        /// with the ring, the stock, a one-column shank and the curved arms. Eleven and not thirteen because the
        /// sag probe said so: with the arms reaching the wall's second column, what was left of the margin beside
        /// them was a strip one cell wide and five tall, and it swung through the line in 2 of 5 runs. The hardest level in
        /// the chapter for the reason the Zebra is the Gallery's: the shape is cut into several parts of several
        /// inks, so no single shot takes it.
        /// </summary>
        private static Design Anchor() => Silhouette("Anchor", shots: 44, ceilingStep: 8, ANCHOR,
            new[] { BallType.Type1, BallType.Type3 }, BallType.Type4, BallType.Type7);

        private static readonly string[] ANCHOR =
        {
            "...............",
            "...............",
            ".....ooooo.....",
            ".....o...o.....",
            ".....ooooo.....",
            "...+++++++++...",
            ".......#.......",
            ".......#.......",
            ".......#.......",
            "..##...#...##..",
            "..###..#..###..",
            "...###.#.###...",
            "....#######....",
            ".....#####.....",
        };

        #endregion
    }
}
