using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.BS3D.GameStructure
{
    /// <summary>
    /// The <b>colour cue</b> (#737): a figure painted on every ball colour beside its hue, so two colours that are
    /// close under one material (the wool's black/brown/silver, its beige white and yellow) also differ in a figure.
    /// The owner chose the painted reading over a carved one (2026-10-12) and asked for several <b>sets</b> of
    /// figures to pick from, so this holds four, each giving all thirteen colours a figure of its own, and nothing a
    /// player can reach draws any of them until he has picked one (<c>colourcue=motif:N</c> on the command line).
    /// <list type="number">
    /// <item><b>Figures</b>: thirteen different geometric figures, the prototype's four (the silver's stripes, the
    /// brown's dots, the white's checks, the yellow's stars) among them.</item>
    /// <item><b>Emblems</b>: one printed symbol on each side of the ball, like the pips of a card (the four suits, a
    /// star, a moon, a drop...).</item>
    /// <item><b>Lines</b>: bands only, told apart by count, width, a wave, a dash or a double line - the calmest.</item>
    /// <item><b>Spots</b>: polka dots, told apart by count, size, rings and two sizes.</item>
    /// </list>
    /// <para>
    /// <b>The inks keep the ball's average colour</b> (<see cref="Inks"/>): the figure is a darker or a paler shade of
    /// the ball's own colour and the ground the plain colour scaled the other way, by the share of the ball the
    /// figure covers, so where the figure is too small to see the ball still shows the colour it always had and no
    /// two colours move closer. The prototype painted its figure on the plain colour, which darkened the light
    /// colours towards the dark ones (white/silver fell from 18-25 to 8-12 dE). Only black cannot be balanced -
    /// nothing is darker than its ground - so its figure is dim and small.
    /// </para>
    /// </summary>
    public static class ColourCues
    {
        /// <summary>The set that paints nothing: the shipped look.</summary>
        public const int NONE = 0;

        /// <summary>How many figure sets there are, numbered 1..this.</summary>
        public const int SET_COUNT = 4;

        /// <summary>A darker-ink figure's ink is the plain colour times this, before the balance (the prototype's).</summary>
        public const float DARK_INK = 0.28f;

        /// <summary>How much brighter than the plain colour a darker-ink figure's ground may go to balance it.</summary>
        public const float GROUND_MAX = 1.5f;

        /// <summary>How dark a paler-ink figure's ground may go to balance it; below it the ball's average rises.</summary>
        public const float GROUND_MIN = 0.3f;

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
        /// shader reads them (<see cref="ColourCuePaint"/>; <c>C.W</c> is the share of the sphere it covers), and
        /// which of the frames it is read in. The coverage is the field's own, measured on 200,000 even points of the
        /// sphere with a line-for-line port of <c>BallColourCue.fxh</c>: the ink balance rests on it, and a figure
        /// changed without measuring it again leaves its ball's average colour off by the difference.
        /// </summary>
        public readonly record struct Figure(string Name, Vector4 A, Vector4 B, Vector4 C, int Frame);

        //The frames a figure is read in, their rows the figure's axes in object space. Each is turned off the object Y
        //the vinyl's gores converge on, so no band reads as the beach ball's own pattern, and off each other, so a set
        //does not line every colour's figure up the same way.
        private static readonly Matrix[] Frames =
        {
            //0: the prototype's stripes, tilted off Y and off the horizontal (the bomb's latitude bands)
            Rows(0.871575f, -0.490262f, 0f, 0.449439f, 0.799002f, -0.399501f, 0.195860f, 0.348195f, 0.916733f),
            //1: the prototype's checks
            Rows(0.837812f, -0.427769f, 0.339242f, 0.477673f, 0.875240f, -0.076051f, -0.264386f, 0.225763f, 0.937620f),
            //2: the prototype's stars
            Rows(0.959194f, -0.236530f, 0.154919f, 0.154919f, 0.897985f, 0.411852f, -0.236530f, -0.371046f, 0.897985f),
            //3: the emblems', nearly upright (turned 20 degrees round Y and tipped 8 forward), so on a ball at rest
            //the side faces' emblems stand the way they were drawn
            Rows(0.939693f, 0f, 0.342020f, 0.047600f, 0.990268f, -0.130780f, -0.338692f, 0.139173f, 0.930548f),
            //4: a second lean, the other way from frame 0
            Rows(0.640057f, 0.446494f, -0.625276f, -0.541067f, 0.839733f, 0.045776f, 0.545504f, 0.309017f, 0.779060f),
            //5: nearly upright, tipped just enough that a band is not a line of latitude
            Rows(0.970296f, -0.238247f, -0.042009f, 0.241922f, 0.955555f, 0.168490f, 0f, -0.173648f, 0.984808f),
            //6: object space itself (the prototype's dots)
            Matrix.Identity,
        };

        private static Matrix Rows(float m11, float m12, float m13, float m21, float m22, float m23, float m31, float m32, float m33) =>
            new(m11, m12, m13, 0f, m21, m22, m23, 0f, m31, m32, m33, 0f, 0f, 0f, 0f, 1f);

        private static Figure Bands(string name, int frame, float count, float share, float coverage, float phase = 0f,
            float waveAmplitude = 0f, float waves = 0f, float hollow = 0f, float dashes = 0f, float dashShare = 0.5f,
            bool grid = false) =>
            new(name, new Vector4((float)Kind.Bands, count, phase, share), new Vector4(waveAmplitude, waves, hollow, dashes),
                new Vector4(dashShare, grid ? 1f : 0f, 0f, coverage), frame);

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
        //No figure spirals, zigzags or runs along meridians (the gravity well, the zap, the vinyl's gores), and no ring
        //sits inside another (the gravity well's bullseye); "BallColourCue.fxh" lists what each must not look like.
        private static readonly Figure[][] Sets =
        {
            new[]
            {
                Bands("one wavy band", 5, 1f, 0.20f, 0.200f, waveAmplitude: 0.16f, waves: 5f),
                Mark("chevrons", Emblem.Chevron, 0.62f, 0.149f),
                Mark("diamonds", Emblem.Diamond, 0.62f, 0.214f),
                Checks("checks", 1, 0.500f),
                Mark("crosses", Emblem.Saltire, 0.58f, 0.218f),
                Mark("hexagons", Emblem.Hexagon, 0.60f, 0.287f),
                Mark("stars", Emblem.Star, 0.62f, 0.228f, frame: 2),
                Mark("small squares", Emblem.Square, 0.42f, 0.121f),
                Spots("six rings", 1, 6, 27f, 0.211f, ringInnerDegrees: 16f),
                Spots("twelve dots", 6, 12, 19.5f, 0.344f),
                Bands("three stripes", 0, 3f, 0.42f, 0.420f),
                Bands("a grid", 4, 1f, 0.14f, 0.267f, grid: true),
                Mark("triangles", Emblem.Triangle, 0.60f, 0.164f),
            },
            new[]
            {
                Mark("hearts", Emblem.Heart, 0.62f, 0.254f),
                Mark("clubs", Emblem.Club, 0.62f, 0.204f),
                Mark("hexagons", Emblem.Hexagon, 0.62f, 0.304f),
                Mark("flowers", Emblem.Flower, 0.62f, 0.271f),
                Mark("drops", Emblem.Drop, 0.62f, 0.198f),
                Mark("spades", Emblem.Spade, 0.62f, 0.207f),
                Mark("stars", Emblem.Star, 0.62f, 0.228f),
                Mark("rings", Emblem.Ring, 0.62f, 0.212f),
                Mark("chevrons", Emblem.Chevron, 0.62f, 0.149f),
                Mark("diamonds", Emblem.Diamond, 0.62f, 0.214f),
                Mark("moons", Emblem.Moon, 0.62f, 0.169f),
                Mark("crosses", Emblem.Saltire, 0.62f, 0.244f),
                Mark("triangles", Emblem.Triangle, 0.62f, 0.174f),
            },
            new[]
            {
                Bands("one broad band", 5, 1f, 0.40f, 0.400f),
                Bands("a dashed band", 5, 1f, 0.24f, 0.120f, dashes: 8f, dashShare: 0.5f),
                Bands("two wavy bands", 5, 2f, 0.20f, 0.200f, phase: 1f, waveAmplitude: 0.10f, waves: 5f),
                Bands("one wavy band", 5, 1f, 0.22f, 0.220f, waveAmplitude: 0.14f, waves: 4f),
                Bands("a double line", 5, 1f, 0.36f, 0.220f, hollow: 0.14f),
                Bands("a dotted line", 5, 1f, 0.16f, 0.067f, dashes: 14f, dashShare: 0.42f),
                Bands("four broad bands", 5, 4f, 0.50f, 0.500f),
                Bands("one thin line", 5, 1f, 0.12f, 0.120f),
                Bands("a seam", 5, 1f, 0.16f, 0.160f, waveAmplitude: 0.55f, waves: 2f),
                Bands("two bands", 5, 2f, 0.30f, 0.300f, phase: 1f),
                Bands("three bands", 5, 3f, 0.42f, 0.420f),
                Bands("a grid", 4, 1f, 0.14f, 0.267f, grid: true),
                Bands("pinstripes", 4, 7f, 0.22f, 0.220f),
            },
            new[]
            {
                Spots("eight dots", 1, 8, 22f, 0.291f),
                Spots("thirty-two tiny dots", 1, 20, 9f, 0.176f, secondSolid: 12, secondRadiusDegrees: 9f),
                Spots("six rings and eight dots", 1, 6, 26f, 0.260f, ringInnerDegrees: 17f, secondSolid: 8, secondRadiusDegrees: 12f),
                Spots("twenty small dots", 2, 20, 13f, 0.256f),
                Spots("four big and four small", 2, 4, 33f, 0.400f, secondSolid: -4, secondRadiusDegrees: 16f),
                Spots("eight rings", 0, 8, 23f, 0.182f, ringInnerDegrees: 15f),
                Spots("six big and eight small", 2, 6, 27f, 0.430f, secondSolid: 8, secondRadiusDegrees: 13f),
                Spots("six small rings", 2, 6, 22f, 0.116f, ringInnerDegrees: 15f),
                Spots("four big rings", 1, 4, 36f, 0.209f, ringInnerDegrees: 24f),
                Spots("twelve dots", 6, 12, 19.5f, 0.344f),
                Spots("four big spots", 1, 4, 34f, 0.342f),
                Spots("six dots", 0, 6, 26f, 0.304f),
                Spots("twelve rings", 2, 12, 17f, 0.171f, ringInnerDegrees: 10f),
            },
        };

        private static readonly string[] SetNames = { "none", "figures", "emblems", "lines", "spots" };

        /// <summary>
        /// The colours that take a PALER figure, with how: their colour times a gain, plus a grey. Every other colour
        /// takes a darker one (<see cref="DARK_INK"/>). Black's is a dim grey and nothing more, because its average
        /// can only rise: its ground cannot get darker than black to pay for the figure.
        /// </summary>
        private static bool PaleInk(BallType type, out float gain, out float grey)
        {
            (gain, grey) = type switch
            {
                BallType.Type8 => (1.0f, 0.08f),  //black
                BallType.Type10 => (2.2f, 0.01f), //brown
                BallType.Type11 => (1.9f, 0f),    //silver
                BallType.Type12 => (1.6f, 0.04f), //navy
                _ => (0f, 0f),
            };

            return gain > 0f;
        }

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

            float groundScale = MathF.Min((1f - coverage * DARK_INK) / (1f - coverage), GROUND_MAX);
            float figureScale = MathF.Max((1f - (1f - coverage) * groundScale) / coverage, 0f);

            return (linearColour * figureScale, linearColour * groundScale);
        }

        /// <summary>
        /// What one draw of a <paramref name="type"/> ball hands the shader in figure set <paramref name="set"/>:
        /// the figure and its two inks, from the colour <paramref name="srgbTint"/> the draw is tinted with.
        /// <see cref="NONE"/> gives the empty paint, which draws the shipped program.
        /// </summary>
        public static ColourCuePaint Paint(int set, BallType type, Vector3 srgbTint)
        {
            if (set == NONE) return default;

            Figure figure = FigureOf(set, type);
            (Vector3 figureInk, Vector3 groundInk) = Inks(type, ColorSpace.SrgbToLinear(srgbTint), figure.C.W);

            return new ColourCuePaint(figure.A, figure.B, figure.C, Frames[figure.Frame], figureInk, groundInk);
        }

        /// <summary>
        /// The <c>colourcue=</c> argument's spellings, shared by the Testbed and the Game so one script drives either:
        /// <c>none</c> (or <c>off</c>), <c>motif</c> (or <c>a</c>, the owner's name for the painted reading) for the
        /// first set, <c>motif:N</c> for set N, or the set's own name (<c>figures</c>, <c>emblems</c>, <c>lines</c>,
        /// <c>spots</c>). The carved reading the prototypes also had (<c>shape</c>, <c>b</c>) is gone and refused.
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
                    set = 1;
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

        /// <summary>The set in <see cref="TryParse"/>'s spelling, for a log line: <c>none</c> or <c>motif:N</c>.</summary>
        public static string Name(int set) =>
            set == NONE ? "none"
            : set >= 1 && set <= SET_COUNT ? $"motif:{set}"
            : throw new ArgumentOutOfRangeException(nameof(set), set, null);

        /// <summary>The set's own name: <c>figures</c>, <c>emblems</c>, <c>lines</c> or <c>spots</c> (<c>none</c> for 0).</summary>
        public static string SetName(int set) =>
            set >= NONE && set <= SET_COUNT ? SetNames[set] : throw new ArgumentOutOfRangeException(nameof(set), set, null);
    }
}
