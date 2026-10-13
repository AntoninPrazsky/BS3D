using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.BS3D.GameStructure
{
    /// <summary>
    /// The <b>colour cue</b> (#737): a figure painted on every ball colour beside its hue, so two colours that are
    /// close under one material (the wool's black/brown/silver, its beige white and yellow) also differ in a figure.
    /// The owner chose the painted reading over a carved one and, having seen four <b>sets</b> of figures, ruled
    /// (2026-10-12) that all four are used, <b>one per ball style</b>, the assignment left to fit the materials
    /// (<see cref="SetOf"/>). The cue is on by default; <c>colourcue=none</c> is the off switch and
    /// <c>colourcue=motif:N</c> puts set N on every style for a comparison.
    /// <list type="number">
    /// <item><b>Figures</b>: thirteen figures of mixed kinds - bands, spots, checks and emblems - so that the tight
    /// pairs differ in the kind of figure and not only in its detail; the silver's stripes, the brown's dots, the
    /// white's checks and the yellow's stars among them.</item>
    /// <item><b>Emblems</b>: one printed symbol on each side of the ball, like the pips of a card (the four suits, a
    /// star, a moon, a drop...).</item>
    /// <item><b>Lines</b>: bands only, told apart by count, width, a wave, a dash or a double line - the calmest.</item>
    /// <item><b>Spots</b>: polka dots, told apart by count, size, rings and two sizes.</item>
    /// </list>
    /// The sets share some figures: eight of the first set's thirteen are emblems, seven of them kinds the second
    /// set also draws (its stars, crosses and triangles are the second set's on the same colours, within a size),
    /// the first and the fourth give brown the same twelve dots, and the first set's silver stripes are the third
    /// set's silver bands in another frame.
    /// <para>
    /// <b>The inks keep the ball's average colour</b> (<see cref="Inks"/>): the figure is a darker or a paler shade of
    /// the ball's own colour - the colour scaled, never with a grey added - and the ground the plain colour scaled
    /// the other way, by the share of the ball the figure covers, so the two average to the plain colour in hue as
    /// well as in luminance, and a ball whose figure is too small to see shows the colour it always had. On screen
    /// the tonemap does not average a bright ground and a dark figure back to exactly the plain colour, so a colour
    /// with a large dark figure still darkens by a few dE and some pairs come closer; "The colour cue" in
    /// <c>docs/rendering/ball-styles.md</c> gives the measured pairs. Only black cannot be balanced - nothing is
    /// darker than its ground - so its figure is a dim grey; on the styles whose colour is a factor rather than a
    /// dye it balances like a darker ink (<see cref="InkScales"/>).
    /// </para>
    /// </summary>
    public static class ColourCues
    {
        /// <summary>The cue off: no figure, the look the balls had before #737 (<c>colourcue=none</c>).</summary>
        public const int NONE = 0;

        /// <summary>
        /// The cue on, every style drawing the set <see cref="SetOf"/> gives it - the default everywhere
        /// (<see cref="DEFAULT"/>), spelled <c>colourcue=motif</c>. A number 1..<see cref="SET_COUNT"/> instead puts
        /// that one set on every style.
        /// </summary>
        public const int BY_STYLE = -1;

        /// <summary>What the Game, the Testbed and the map editor draw unless told otherwise: each style its own set.</summary>
        public const int DEFAULT = BY_STYLE;

        /// <summary>How many figure sets there are, numbered 1..this.</summary>
        public const int SET_COUNT = 4;

        /// <summary>A darker-ink figure's ink is the plain colour times this, before the balance (the prototype's).</summary>
        public const float DARK_INK = 0.28f;

        /// <summary>How much brighter than the plain colour a darker-ink figure's ground may go to balance it.</summary>
        public const float GROUND_MAX = 1.5f;

        /// <summary>
        /// How dark a paler-ink figure's ground may go to balance it; below it the ball's average rises. The silver's
        /// broad pale stripes and bands (about 0.42 of the ball in a 2.0 ink) need 0.26 and the brown's twelve dots
        /// 0.29, so this sits just under them; black is held here whatever its figure (nothing balances black).
        /// </summary>
        public const float GROUND_MIN = 0.25f;

        /// <summary>The figure kinds, numbered as <c>BallColourCue.fxh</c> branches on them.</summary>
        public enum Kind
        {
            Bands = 1,
            Spots = 2,
            Checks = 3,
            Emblem = 4,
        }

        /// <summary>The emblems, numbered as <c>ColourCueEmblem</c> in <c>BallColourCue.fxh</c> branches on them.</summary>
        public enum Emblem
        {
            Star = 1,
            Heart = 2,
            Spade = 3,
            Club = 4,
            Diamond = 5,
            Square = 6,
            Ring = 7,
            Triangle = 8,
            Moon = 9,
            Drop = 10,
            Flower = 11,
            Chevron = 12,
            Saltire = 13,
            Hexagon = 14,
        }

        /// <summary>
        /// One colour's figure in one set: a short name for a page or a log, its kind and parameters packed as the
        /// shader reads them (<see cref="ColourCuePaint"/>; <c>C.W</c> is the share of the ball it covers), and
        /// which of the frames it is read in.
        /// <para>
        /// The coverage is the share of the ball <b>the play camera sees</b> the figure cover, not its share of the
        /// sphere: a hanging ball stays upright and is seen from the side and from below, so a band round the
        /// upright axis faces the camera across its middle and shows 15-18 % more than its share of the sphere
        /// (the broad band covers 0.374 of what is seen against 0.320 of the sphere), while spots and emblems show
        /// their share within 0.01. It was measured with a line-for-line port of <c>BallColourCue.fxh</c> on 400,000
        /// even points, each weighted by its projected area seen from every side at 0 to 30 degrees below the ball,
        /// and <c>ColourCueStyleTests</c> measures it again through <see cref="ColourCueField"/> and holds this table to
        /// it. The ink balance rests on it, and a figure changed without measuring it again leaves its ball's average
        /// colour off by the difference.
        /// </para>
        /// </summary>
        public readonly record struct Figure(string Name, Vector4 A, Vector4 B, Vector4 C, int Frame);

        //The frames a figure is read in, their rows the figure's axes in object space. Frames 0-4 are turned well off
        //the object Y the vinyl's gores converge on, so no band reads as the beach ball's own pattern, and off each
        //other, so a set does not line every colour's figure up the same way. Frame 5 is tipped only 10 degrees, so
        //the lines set's bands and the figures set's red band stand nearly level, and frame 6 is object space
        //itself, which only the brown's dots use: a solid's vertices have no axis a gore could line up with.
        private static readonly Matrix[] Frames =
        {
            //0: the silver's stripes, tilted off Y and off the horizontal (the bomb's latitude bands)
            Rows(0.871575f, -0.490262f, 0f, 0.449439f, 0.799002f, -0.399501f, 0.195860f, 0.348195f, 0.916733f),
            //1: the white's checks
            Rows(0.837812f, -0.427769f, 0.339242f, 0.477673f, 0.875240f, -0.076051f, -0.264386f, 0.225763f, 0.937620f),
            //2: the yellow's stars
            Rows(0.959194f, -0.236530f, 0.154919f, 0.154919f, 0.897985f, 0.411852f, -0.236530f, -0.371046f, 0.897985f),
            //3: the emblems', nearly upright (turned 20 degrees round Y and tipped 8 forward), so on a ball at rest
            //the side faces' emblems stand the way they were drawn
            Rows(0.939693f, 0f, 0.342020f, 0.047600f, 0.990268f, -0.130780f, -0.338692f, 0.139173f, 0.930548f),
            //4: a second lean, the other way from frame 0
            Rows(0.640057f, 0.446494f, -0.625276f, -0.541067f, 0.839733f, 0.045776f, 0.545504f, 0.309017f, 0.779060f),
            //5: nearly upright, tipped 10 degrees, just enough that a band is not a line of latitude
            Rows(0.970296f, -0.238247f, -0.042009f, 0.241922f, 0.955555f, 0.168490f, 0f, -0.173648f, 0.984808f),
            //6: object space itself (the brown's dots)
            Matrix.Identity,
        };

        private static Matrix Rows(float m11, float m12, float m13, float m21, float m22, float m23, float m31, float m32, float m33) =>
            new(m11, m12, m13, 0f, m21, m22, m23, 0f, m31, m32, m33, 0f, 0f, 0f, 0f, 1f);

        private static Figure Bands(string name, int frame, float count, float share, float coverage, float phase = 0f,
            float waveAmplitude = 0f, float waves = 0f, float hollow = 0f, float dashes = 0f, float dashShare = 0.5f) =>
            new(name, new Vector4((float)Kind.Bands, count, phase, share), new Vector4(waveAmplitude, waves, hollow, dashes),
                new Vector4(dashShare, 0f, 0f, coverage), frame);

        private static Figure Spots(string name, int frame, int solid, float radiusDegrees, float coverage,
            float ringInnerDegrees = 0f, int secondSolid = 0, float secondRadiusDegrees = 0f) =>
            new(name, new Vector4((float)Kind.Spots, solid, CosDegrees(radiusDegrees), ringInnerDegrees > 0f ? CosDegrees(ringInnerDegrees) : 2f),
                new Vector4(secondSolid, secondSolid != 0 ? CosDegrees(secondRadiusDegrees) : 2f, 0f, 0f),
                new Vector4(0f, 0f, 0f, coverage), frame);

        private static Figure Checks(string name, int frame, float coverage) =>
            new(name, new Vector4((float)Kind.Checks, 1.5f * MathF.PI, 0f, 0f), Vector4.Zero, new Vector4(0f, 0f, 0f, coverage), frame);

        private static Figure Mark(string name, Emblem emblem, float size, float coverage, int frame = 3) =>
            new(name, new Vector4((float)Kind.Emblem, (float)emblem, size, 0f), Vector4.Zero, new Vector4(0f, 0f, 0f, coverage), frame);

        private static float CosDegrees(float degrees) => MathF.Cos(MathHelper.ToRadians(degrees));

        //[set - 1][type - 1]: red, green, blue, white, cyan, magenta, yellow, black, orange, brown, silver, navy, olive.
        //No figure spirals, zigzags or runs along meridians (the gravity well, the zap, the vinyl's gores), no ring
        //sits inside another (the gravity well's bullseye), no two bands cross round the ball (the mine's two seams,
        //which is why no set has a grid) and no X is pale on a dark colour (the mine's seams again, seen face on);
        //"BallColourCue.fxh" lists what each must not look like. No family of bands covers its frame's poles either:
        //a cap there inside a ring is the bullseye again, so an even count takes phase 1 and an odd one phase 0,
        //which leaves both poles between two bands.
        private static readonly Figure[][] Sets =
        {
            new[]
            {
                Bands("one wavy band", 5, 1f, 0.20f, 0.234f, waveAmplitude: 0.16f, waves: 5f),
                Mark("chevrons", Emblem.Chevron, 0.62f, 0.149f),
                Mark("diamonds", Emblem.Diamond, 0.62f, 0.212f),
                Checks("checks", 1, 0.498f),
                Mark("crosses", Emblem.Saltire, 0.58f, 0.216f),
                Mark("hexagons", Emblem.Hexagon, 0.60f, 0.285f),
                Mark("stars", Emblem.Star, 0.62f, 0.229f, frame: 2),
                Mark("small squares", Emblem.Square, 0.42f, 0.120f),
                Spots("six rings", 1, 6, 27f, 0.210f, ringInnerDegrees: 16f),
                Spots("twelve dots", 6, 12, 19.5f, 0.344f),
                Bands("three stripes", 0, 3f, 0.42f, 0.424f),
                Mark("moons", Emblem.Moon, 0.62f, 0.172f),
                Mark("triangles", Emblem.Triangle, 0.60f, 0.163f),
            },
            new[]
            {
                Mark("hearts", Emblem.Heart, 0.62f, 0.246f),
                Mark("clubs", Emblem.Club, 0.62f, 0.203f),
                Mark("hexagons", Emblem.Hexagon, 0.62f, 0.302f),
                Mark("flowers", Emblem.Flower, 0.62f, 0.269f),
                Mark("crosses", Emblem.Saltire, 0.62f, 0.242f),
                Mark("spades", Emblem.Spade, 0.62f, 0.206f),
                Mark("stars", Emblem.Star, 0.62f, 0.227f),
                Mark("rings", Emblem.Ring, 0.62f, 0.210f),
                Mark("chevrons", Emblem.Chevron, 0.62f, 0.149f),
                Mark("diamonds", Emblem.Diamond, 0.62f, 0.212f),
                Mark("moons", Emblem.Moon, 0.62f, 0.172f),
                Mark("drops", Emblem.Drop, 0.62f, 0.200f),
                Mark("triangles", Emblem.Triangle, 0.62f, 0.173f),
            },
            new[]
            {
                Bands("one broad band", 5, 1f, 0.32f, 0.374f),
                Bands("a dashed band", 5, 1f, 0.24f, 0.141f, dashes: 8f, dashShare: 0.5f),
                Bands("two wavy bands", 5, 2f, 0.20f, 0.211f, phase: 1f, waveAmplitude: 0.10f, waves: 5f),
                Bands("one wavy band", 5, 1f, 0.22f, 0.257f, waveAmplitude: 0.14f, waves: 4f),
                Bands("a double line", 5, 1f, 0.36f, 0.254f, hollow: 0.14f),
                Bands("a dotted line", 5, 1f, 0.16f, 0.079f, dashes: 14f, dashShare: 0.42f),
                Bands("four broad bands", 5, 4f, 0.50f, 0.506f, phase: 1f),
                Bands("one thin line", 5, 1f, 0.12f, 0.141f),
                Bands("a wavy line", 5, 1f, 0.14f, 0.165f, waveAmplitude: 0.12f, waves: 6f),
                Bands("two bands", 5, 2f, 0.30f, 0.315f, phase: 1f),
                Bands("three bands", 5, 3f, 0.42f, 0.427f),
                Bands("a seam", 5, 1f, 0.16f, 0.176f, waveAmplitude: 0.55f, waves: 2f),
                Bands("pinstripes", 4, 7f, 0.22f, 0.220f),
            },
            new[]
            {
                Spots("eight dots", 1, 8, 22f, 0.292f),
                Spots("thirty-two tiny dots", 1, 20, 9f, 0.177f, secondSolid: 12, secondRadiusDegrees: 9f),
                Spots("six rings and eight dots", 1, 6, 26f, 0.260f, ringInnerDegrees: 17f, secondSolid: 8, secondRadiusDegrees: 12f),
                Spots("twenty small dots", 2, 20, 13f, 0.257f),
                Spots("four big and four small", 2, 4, 33f, 0.399f, secondSolid: -4, secondRadiusDegrees: 16f),
                Spots("eight rings", 0, 8, 23f, 0.182f, ringInnerDegrees: 15f),
                Spots("six big and eight small", 2, 6, 27f, 0.431f, secondSolid: 8, secondRadiusDegrees: 13f),
                Spots("six small rings", 2, 6, 22f, 0.117f, ringInnerDegrees: 15f),
                Spots("four big spots", 1, 4, 34f, 0.343f),
                Spots("twelve dots", 6, 12, 19.5f, 0.344f),
                Spots("four big rings", 1, 4, 36f, 0.210f, ringInnerDegrees: 24f),
                Spots("six dots", 0, 6, 21f, 0.200f),
                Spots("twelve rings", 2, 12, 17f, 0.170f, ringInnerDegrees: 10f),
            },
        };

        private static readonly string[] SetNames = { "none", "figures", "emblems", "lines", "spots" };

        /// <summary>
        /// The colours that take a PALER figure, with how: their colour times a gain. Every other colour takes a
        /// darker one (<see cref="DARK_INK"/>). A grey added to the gain would read brighter, but the ground is the
        /// plain colour scaled, so nothing pays for a grey's hue and the ball's average turns greyer: 0.05 of grey
        /// on the navy's figure leaves the navy 10-12 dE greyer, towards the black and the brown. So only black takes
        /// a grey - a dim one and nothing more - because its average can only rise anyway: its ground cannot get
        /// darker than black to pay for the figure. The navy's gain is the most that keeps its blue under 1 (a
        /// reflectance); the luminance ratio <c>ColourCueTests</c> asks of every figure holds in every set.
        /// </summary>
        private static bool PaleInk(BallType type, out float gain, out float grey)
        {
            (gain, grey) = type switch
            {
                BallType.Type8 => (1.0f, 0.08f), //black
                BallType.Type10 => (2.2f, 0f),   //brown
                BallType.Type11 => (2.0f, 0f),   //silver
                BallType.Type12 => (2.9f, 0f),   //navy
                _ => (0f, 0f),
            };

            return gain > 0f;
        }

        /// <summary>
        /// The figure set a ball style draws (#737, the owner's ruling of 2026-10-12: all four sets, one per style,
        /// "so that it fits"). Exhaustive and throwing, like <c>BallRenderSet.ShadingOf</c>: a style added without a
        /// set here would otherwise draw some other style's figures, or none, and nothing would fail.
        /// <para>
        /// Each set went where its kind of figure reads as part of the material, and away from the special kinds the
        /// style's levels hang beside it (counted over <c>Game/Levels</c>):
        /// <list type="bullet">
        /// <item><b>Wool - figures (1)</b>: the reported style, and the tight pairs the player could not tell apart
        /// (black/brown/silver, the beige white and the yellow, red/orange) carry different KINDS of figure in this
        /// set alone, which is what survives at 1920x1080; argyle diamonds, checks, stripes and stars are knitted
        /// colourwork. Six wool levels hang clear glass, and no figure in this set is a seam.</item>
        /// <item><b>Porcelain - figures (1)</b>: hand-painted china carries stripes, checks, dots and motifs alike,
        /// and the Spectrum sweeps one hue family through a level (red/orange/yellow, blue/cyan/navy), the pairs this
        /// set gives different kinds. Not the lines: the Mirage hangs clear glass, whose cast seam the lines' navy
        /// seam follows.</item>
        /// <item><b>Plasma - figures (1)</b>: the Nebula hangs all thirteen colours at once and the plasma is
        /// hue-strong and value-weak (black, white and silver all burn pale arcs), so it takes the set whose
        /// colours differ most in kind.</item>
        /// <item><b>Vinyl - emblems (2)</b>: the Big Top's ball printed like a deck of cards - the four suits, stars
        /// and moons - and, as what every unauthored map draws, the set that moved the colours least.</item>
        /// <item><b>Metal - emblems (2)</b>: die-struck insignia, as on a coin; the Quarry's own pairs (blue/cyan)
        /// differ in outline, and a band would run along the brush, the metal's rolling cue.</item>
        /// <item><b>Marble - lines (3)</b>: banded stone, and the calmest set for the quiet material the Reveal
        /// needs.</item>
        /// <item><b>Ice - lines (3)</b>: strata frozen in glacier ice. Not an emblem set: an emblem on each of six
        /// cube faces is the frozen ball's six faces again. The Tower's orange/yellow, on ten of its levels, is one
        /// wavy line against four broad bands.</item>
        /// <item><b>Gem - lines (3)</b>: banded agate, running across the facets; the emblems' hexagons, diamonds and
        /// triangles would read as more facets.</item>
        /// <item><b>Bubble - spots (4)</b>: polka-dot party balloons, round figures on a round film, for the first
        /// chapter.</item>
        /// <item><b>Lava - none (<see cref="NONE"/>)</b>: the only style that draws no figure. Two carriers were tried
        /// (#737, judged on the laptop's captures on 2026-10-13): the spots set's islands as a scale on the seams' heat,
        /// then also as an ash crust inside the figure (<c>LavaAshDark</c>). Neither read at play distance; the glowing
        /// seams own the ball. <c>colourcue=motif:N</c> still forces a set on it for a comparison.</item>
        /// </list>
        /// </para>
        /// </summary>
        public static int SetOf(BallStyle style) => style switch
        {
            BallStyle.Wool => 1,
            BallStyle.Porcelain => 1,
            BallStyle.Plasma => 1,
            BallStyle.Beach => 2,
            BallStyle.Metal => 2,
            BallStyle.Marble => 3,
            BallStyle.Ice => 3,
            BallStyle.Gem => 3,
            BallStyle.Bubble => 4,
            BallStyle.Lava => NONE,
            _ => throw new ArgumentOutOfRangeException(nameof(style), style,
                $"{style} has no colour-cue set; give it one in {nameof(ColourCues)}.{nameof(SetOf)}"),
        };

        /// <summary>
        /// The set a ball of <paramref name="style"/> draws under the switch <paramref name="cue"/>: the style's own
        /// (<see cref="SetOf"/>) for <see cref="BY_STYLE"/>, otherwise the switch itself (<see cref="NONE"/> or a set).
        /// </summary>
        public static int SetFor(int cue, BallStyle style) => cue == BY_STYLE ? SetOf(style) : cue;

        /// <summary>One colour's figure in one set (1..<see cref="SET_COUNT"/>).</summary>
        public static Figure FigureOf(int set, BallType type)
        {
            if (set < 1 || set > SET_COUNT) throw new ArgumentOutOfRangeException(nameof(set), set, null);

            return Sets[set - 1][(int)type - 1];
        }

        /// <summary>The frame a figure is read in, its first three rows the axes in object space.</summary>
        public static Matrix FrameOf(Figure figure) => Frames[figure.Frame];

        /// <summary>
        /// The two inks a figure covering <paramref name="coverage"/> of a ball of <paramref name="linearColour"/> is
        /// painted in, so that the ball averages its plain colour: a darker shade (<see cref="DARK_INK"/>) on a ground
        /// brightened to pay for it, or, for the colours <see cref="PaleInk"/> names, a paler one on a ground darkened
        /// to pay for it, matched in luminance. The ground is always the plain colour scaled - the marble scales its
        /// own veined stone by the same factor - and is held between <see cref="GROUND_MIN"/> and
        /// <see cref="GROUND_MAX"/>: past them the darker ink is lightened, or the average rises, rather than the
        /// ground losing its colour.
        /// </summary>
        public static (Vector3 figure, Vector3 ground) Inks(BallType type, Vector3 linearColour, float coverage)
        {
            if (PaleInk(type, out float gain, out float grey))
            {
                Vector3 figure = linearColour * gain + new Vector3(grey);
                float luminance = ColorSpace.Luminance(linearColour);
                float ground = luminance > 0f
                    ? (luminance - coverage * ColorSpace.Luminance(figure)) / ((1f - coverage) * luminance)
                    : GROUND_MIN;

                return (figure, linearColour * MathF.Max(ground, GROUND_MIN));
            }

            (float figureScale, float groundScale) = DarkInkScales(coverage);

            return (linearColour * figureScale, linearColour * groundScale);
        }

        /// <summary>
        /// The two inks as SCALES on the plain colour (the figure's, the ground's), for the styles whose colour is a
        /// factor in something else rather than a body to dye: the metal's reflectance, the plasma's discharge and the
        /// lava's molten seams multiply by these. Every ink but black's is the plain colour scaled, so for those the
        /// scales are the inks' own factors. Black's figure ink is a dim grey ADDED to a near-black
        /// (<see cref="PaleInk"/>), whose ratio to it is about 24: an arc or a seam 24 times as bright inside the
        /// figure, a reflectance clipped to a full mirror. On these styles black has something to scale - the metal's
        /// floored gunmetal, the plasma's white arcs, the lava's seams - so it balances like any darker-ink colour,
        /// and the scales average to 1 over the ball for black too.
        /// </summary>
        public static (float figure, float ground) InkScales(BallType type, Vector3 linearColour, float coverage)
        {
            float luminance = ColorSpace.Luminance(linearColour);
            if (type == BallType.Type8 || luminance <= 0f) return DarkInkScales(coverage);

            (Vector3 figureInk, Vector3 groundInk) = Inks(type, linearColour, coverage);

            return (ColorSpace.Luminance(figureInk) / luminance, ColorSpace.Luminance(groundInk) / luminance);
        }

        //A darker-ink figure's factors: the ink DARK_INK of the plain colour on a ground brightened to pay for it, at most
        //GROUND_MAX, past which the ink is lightened instead so that the two still average to 1
        private static (float figure, float ground) DarkInkScales(float coverage)
        {
            float groundScale = MathF.Min((1f - coverage * DARK_INK) / (1f - coverage), GROUND_MAX);
            float figureScale = MathF.Max((1f - (1f - coverage) * groundScale) / coverage, 0f);

            return (figureScale, groundScale);
        }

        /// <summary>
        /// What one draw of a <paramref name="type"/> ball hands the shader in figure set <paramref name="set"/>:
        /// the figure, its two inks and the same as scales (<see cref="InkScales"/>), from the colour
        /// <paramref name="srgbTint"/> the draw is tinted with. <see cref="NONE"/> gives the empty paint, which draws
        /// the shipped program.
        /// </summary>
        public static ColourCuePaint Paint(int set, BallType type, Vector3 srgbTint)
        {
            if (set == NONE) return default;

            Figure figure = FigureOf(set, type);
            Vector3 linear = ColorSpace.SrgbToLinear(srgbTint);
            (Vector3 figureInk, Vector3 groundInk) = Inks(type, linear, figure.C.W);
            (float figureScale, float groundScale) = InkScales(type, linear, figure.C.W);

            return new ColourCuePaint(figure.A, figure.B, figure.C, Frames[figure.Frame], figureInk, groundInk,
                new Vector2(figureScale, groundScale));
        }

        /// <summary>
        /// The <c>colourcue=</c> argument's spellings, shared by the Testbed and the Game so one script drives either:
        /// <c>none</c> (or <c>off</c>) for no figure; <c>motif</c> (or <c>a</c>, the owner's name for the painted
        /// reading, or <c>on</c>, or <c>style</c>) for each style's own set (<see cref="BY_STYLE"/>, the default);
        /// <c>motif:N</c> or the set's own name (<c>figures</c>, <c>emblems</c>, <c>lines</c>, <c>spots</c>) for set N
        /// on every style. The carved reading the prototypes also had (<c>shape</c>, <c>b</c>) is gone and refused.
        /// </summary>
        public static bool TryParse(string text, out int set)
        {
            string name = text?.Trim().ToLowerInvariant() ?? string.Empty;
            set = NONE;

            switch (name)
            {
                case "none":
                case "off":
                    return true;

                case "motif":
                case "a":
                case "on":
                case "style":
                    set = BY_STYLE;
                    return true;
            }

            if (name.StartsWith("motif:", StringComparison.Ordinal)
                && int.TryParse(name.AsSpan("motif:".Length), out int number) && number >= 1 && number <= SET_COUNT)
            {
                set = number;
                return true;
            }

            int named = Array.IndexOf(SetNames, name);
            if (named > 0)
            {
                set = named;
                return true;
            }

            return false;
        }

        /// <summary>The switch in <see cref="TryParse"/>'s spelling, for a log line: <c>none</c>, <c>motif</c> or <c>motif:N</c>.</summary>
        public static string Name(int set) =>
            set == NONE ? "none"
            : set == BY_STYLE ? "motif"
            : set >= 1 && set <= SET_COUNT ? $"motif:{set}"
            : throw new ArgumentOutOfRangeException(nameof(set), set, null);

        /// <summary>
        /// The set's own name: <c>figures</c>, <c>emblems</c>, <c>lines</c> or <c>spots</c> (<c>none</c> for
        /// <see cref="NONE"/>, <c>each style its own</c> for <see cref="BY_STYLE"/>).
        /// </summary>
        public static string SetName(int set) =>
            set == BY_STYLE ? "each style its own"
            : set >= NONE && set <= SET_COUNT ? SetNames[set]
            : throw new ArgumentOutOfRangeException(nameof(set), set, null);
    }
}
