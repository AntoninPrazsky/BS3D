using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The colour cue per ball STYLE (#737): the owner ruled that all four figure sets are used, one per style, with
    /// the cue on by default. What is checkable without a device: every style has a set and every set is used, every
    /// style draws a distinct figure per colour, its inks and its ink SCALES keep each ball's average (black's
    /// scales too, which the shader's old luminance ratio did not), every style's shading has a cue program the effect
    /// declares, the CPU port of the figures reproduces the coverage the inks are balanced on, and the HUD's disc
    /// carries the figure upright. Each check is shown failing on the fault it is there for (BestPractices.md 10).
    /// </summary>
    public class ColourCueStyleTests
    {
        private readonly ITestOutputHelper _output;

        public ColourCueStyleTests(ITestOutputHelper output) => _output = output;

        public static TheoryData<BallStyle> Styles()
        {
            TheoryData<BallStyle> data = new();
            foreach (BallStyle style in Enum.GetValues<BallStyle>()) data.Add(style);
            return data;
        }

        [Fact]
        public void TheCueIsOnByDefault()
        {
            Assert.Equal(ColourCues.BY_STYLE, ColourCues.DEFAULT);
            Assert.True(ColourCues.TryParse(ColourCues.Name(ColourCues.DEFAULT), out int back));
            Assert.Equal(ColourCues.DEFAULT, back);
        }

        [Theory]
        [MemberData(nameof(Styles))]
        public void EveryStyleHasAFigureSet(BallStyle style)
        {
            Assert.InRange(ColourCues.SetOf(style), 1, ColourCues.SET_COUNT);
            Assert.Equal(ColourCues.SetOf(style), ColourCues.SetFor(ColourCues.BY_STYLE, style));
            Assert.Equal(ColourCues.NONE, ColourCues.SetFor(ColourCues.NONE, style));
            Assert.Equal(3, ColourCues.SetFor(3, style));
        }

        //The assignment is exhaustive and throws, like BallRenderSet.ShadingOf: a style added without a set must not
        //quietly draw some other style's figures, or none
        [Fact]
        public void AStyleWithoutASetThrows() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => ColourCues.SetOf((BallStyle)200));

        //The owner's ruling: all the sets are used
        [Fact]
        public void EverySetIsUsedBySomeStyle()
        {
            HashSet<int> used = Enum.GetValues<BallStyle>().Select(ColourCues.SetOf).ToHashSet();

            foreach (int set in Enumerable.Range(1, ColourCues.SET_COUNT))
                Assert.True(used.Contains(set), $"figure set {set} ({ColourCues.SetName(set)}) is drawn by no style");
        }

        //No figure that names a special kind the style's own levels hang beside it: an emblem on six cube faces is the
        //frozen ball (ice), a band of brighter seams the mine (lava), the lines' navy seam the clear glass (porcelain)
        [Theory]
        [InlineData(BallStyle.Ice, ColourCues.Kind.Emblem)]
        [InlineData(BallStyle.Lava, ColourCues.Kind.Bands)]
        [InlineData(BallStyle.Lava, ColourCues.Kind.Emblem)]
        public void AStyleAvoidsTheFigureKindItsKindsWouldBeTakenFor(BallStyle style, ColourCues.Kind kind)
        {
            foreach (BallType type in Enum.GetValues<BallType>())
                Assert.NotEqual((float)kind, ColourCues.FigureOf(ColourCues.SetOf(style), type).A.X);
        }

        [Fact]
        public void ThePorcelainDrawsNoSeam()
        {
            foreach (BallType type in Enum.GetValues<BallType>())
                Assert.NotEqual("a seam", ColourCues.FigureOf(ColourCues.SetOf(BallStyle.Porcelain), type).Name);
        }

        [Theory]
        [MemberData(nameof(Styles))]
        public void EachStyleDrawsADistinctFigurePerColour(BallStyle style)
        {
            int set = ColourCues.SetFor(ColourCues.DEFAULT, style);
            BallType[] types = Enum.GetValues<BallType>();

            foreach (BallType type in types)
                Assert.False(Paint(set, type).IsNone, $"{style}: {type} carries no figure");

            foreach (BallType a in types)
                foreach (BallType b in types.Where(other => other > a))
                {
                    ColourCuePaint first = Paint(set, a), second = Paint(set, b);
                    Assert.False(first.A == second.A && first.B == second.B && first.C == second.C && first.Frame == second.Frame,
                        $"{style}: {a} and {b} carry the same figure");
                }
        }

        //The inks average to the plain colour for every style's set (black, which cannot, is held dim), and the SCALES -
        //what the metal's reflectance, the plasma's arcs and the lava's seams multiply by - average to 1 for every colour,
        //black included
        [Theory]
        [MemberData(nameof(Styles))]
        public void EachStylesInksAndScalesKeepTheAverage(BallStyle style)
        {
            int set = ColourCues.SetFor(ColourCues.DEFAULT, style);

            foreach (BallType type in Enum.GetValues<BallType>())
            {
                ColourCuePaint paint = Paint(set, type);
                float coverage = paint.C.W;
                Vector3 plain = ColorSpace.SrgbToLinear(BasicEffectParamsProvider.GetDiffuseTintByType(type));
                Vector3 average = paint.FigureInk * coverage + paint.GroundInk * (1f - coverage);

                if (type != BallType.Type8)
                    Assert.True(Within(average.X, plain.X) && Within(average.Y, plain.Y) && Within(average.Z, plain.Z),
                        $"{style}, {type}: the inks average {average} against the plain {plain}");

                Assert.True(ScalesAverageToOne(paint.InkScales, coverage),
                    $"{style}, {type}: the scales {paint.InkScales} over {coverage:0.000} average {Average(paint.InkScales, coverage):0.000}");

                //...and they are the inks' own factors wherever the inks are the plain colour scaled
                if (type != BallType.Type8)
                {
                    float luminance = ColorSpace.Luminance(plain);
                    Assert.Equal(ColorSpace.Luminance(paint.FigureInk) / luminance, paint.InkScales.X, 3);
                    Assert.Equal(ColorSpace.Luminance(paint.GroundInk) / luminance, paint.InkScales.Y, 3);
                }
            }
        }

        //The scale check fires on what it is there for: black's scales taken as its inks' luminance ratio, as the first
        //build of the metal, the plasma and the lava took them - a dim grey added to a near-black is some 24 times it
        [Fact]
        public void TheScaleCheckFindsBlacksAddedGrey()
        {
            Vector3 black = ColorSpace.SrgbToLinear(BasicEffectParamsProvider.GetDiffuseTintByType(BallType.Type8));

            foreach (int set in Enumerable.Range(1, ColourCues.SET_COUNT))
            {
                float coverage = ColourCues.FigureOf(set, BallType.Type8).C.W;
                (Vector3 figure, Vector3 ground) = ColourCues.Inks(BallType.Type8, black, coverage);
                Vector2 naive = new(ColorSpace.Luminance(figure) / ColorSpace.Luminance(black), ColorSpace.Luminance(ground) / ColorSpace.Luminance(black));

                Assert.True(naive.X > 10f, $"set {set}: black's figure ink is only {naive.X:0.0} times black");
                Assert.False(ScalesAverageToOne(naive, coverage));
            }
        }

        private static bool Within(float value, float target) => MathF.Abs(value - target) <= 0.02f * target + 1e-6f;

        private static float Average(Vector2 scales, float coverage) => scales.X * coverage + scales.Y * (1f - coverage);

        private static bool ScalesAverageToOne(Vector2 scales, float coverage) => MathF.Abs(Average(scales, coverage) - 1f) <= 0.01f;

        private static ColourCuePaint Paint(int set, BallType type) =>
            ColourCues.Paint(set, type, BasicEffectParamsProvider.GetDiffuseTintByType(type));

        //Every style's shading has a cue program in the renderer's table, and the effect declares it: the cue is on by
        //default, and a style without its program would draw its balls in colour alone with nothing failing
        [Theory]
        [MemberData(nameof(Styles))]
        public void EveryStylesShadingHasACueProgram(BallStyle style)
        {
            string missing = MissingCueProgram(BallRenderSet.ShadingOf(style), InstancedModelRenderer.ColourCueTechniqueNames,
                File.ReadAllText(EffectFile("BallColourCueTechniques.fxh")));

            Assert.True(missing == null, $"{style}: {missing}");
        }

        //...and the check fires on a table that lacks the shading, and on a technique the effect does not declare
        [Fact]
        public void TheProgramCheckFindsAGap()
        {
            string effect = File.ReadAllText(EffectFile("BallColourCueTechniques.fxh"));
            (BallShading, string)[] withoutIce = InstancedModelRenderer.ColourCueTechniqueNames.Where(entry => entry.Shading != BallShading.Ice).ToArray();
            (BallShading, string)[] misnamed = { (BallShading.Ice, "InstancedModelIceMotiff") };

            Assert.NotNull(MissingCueProgram(BallShading.Ice, withoutIce, effect));
            Assert.NotNull(MissingCueProgram(BallShading.Ice, misnamed, effect));
        }

        private static string MissingCueProgram(BallShading shading, (BallShading Shading, string Technique)[] table, string effect)
        {
            (BallShading Shading, string Technique)[] rows = table.Where(entry => entry.Shading == shading).ToArray();
            if (rows.Length != 1) return $"{rows.Length} cue programs for {shading} in the renderer's table";

            return Regex.IsMatch(effect, @"^technique\s+" + Regex.Escape(rows[0].Technique) + @"\b", RegexOptions.Multiline)
                ? null
                : $"the effect declares no technique {rows[0].Technique}";
        }

        //THE CPU PORT REPRODUCES THE COVERAGE THE INKS ARE BALANCED ON. Each figure's C.W was measured once with a port
        //of BallColourCue.fxh in Python, as the share of the ball a camera sees, every side round and 0-30 degrees below
        //it; this measures the same share through ColourCueField, the port the HUD paints its discs with. A figure
        //changed in either place without the other, or a coverage typed in wrong, leaves a ball's average colour off.
        [Fact]
        public void TheFieldPortReproducesEveryFiguresCoverage()
        {
            (Vector3[] points, float[] weights) = ViewWeightedSphere(60000);
            float worst = 0f;

            foreach (int set in Enumerable.Range(1, ColourCues.SET_COUNT))
                foreach (BallType type in Enum.GetValues<BallType>())
                {
                    ColourCues.Figure figure = ColourCues.FigureOf(set, type);
                    float measured = ViewCoverage(figure, points, weights);
                    worst = MathF.Max(worst, MathF.Abs(measured - figure.C.W));

                    Assert.True(MathF.Abs(measured - figure.C.W) <= COVERAGE_TOLERANCE,
                        $"set {set}, {type} \"{figure.Name}\": the port covers {measured:0.0000} of the view, the table says {figure.C.W:0.000}");
                }

            _output.WriteLine($"worst coverage difference {worst:0.0000}");
        }

        //...and the measurement tells a coverage off by 0.02 - a figure two per cent too large, which leaves its ball's
        //average about as far off - from the true one
        [Fact]
        public void TheCoverageCheckFindsAWrongCoverage()
        {
            (Vector3[] points, float[] weights) = ViewWeightedSphere(60000);
            ColourCues.Figure stars = ColourCues.FigureOf(1, BallType.Type7);
            ColourCues.Figure wrong = stars with { C = stars.C + new Vector4(0f, 0f, 0f, 0.02f) };

            Assert.True(MathF.Abs(ViewCoverage(wrong, points, weights) - wrong.C.W) > COVERAGE_TOLERANCE);
        }

        private const float COVERAGE_TOLERANCE = 0.006f;

        private static float ViewCoverage(ColourCues.Figure figure, Vector3[] points, float[] weights)
        {
            Matrix frame = ColourCues.FrameOf(figure);
            double inside = 0, total = 0;

            for (int i = 0; i < points.Length; i++)
            {
                total += weights[i];
                if (ColourCueField.Field(figure, ColourCueField.ToFrame(frame, points[i])) > 0f) inside += weights[i];
            }

            return (float)(inside / total);
        }

        /// <summary>
        /// Even points on the unit sphere (a Fibonacci lattice) and each one's weight in what a camera sees of a ball
        /// hanging upright: its projected area, averaged over every side round the ball and over 0, 10, 20 and 30
        /// degrees below it - the Python measurement's weighting, with the average round the ball in closed form.
        /// </summary>
        private static (Vector3[] points, float[] weights) ViewWeightedSphere(int count)
        {
            Vector3[] points = new Vector3[count];
            float[] weights = new float[count];
            float golden = MathF.PI * (3f - MathF.Sqrt(5f));
            float[] elevations = { 0f, 10f, 20f, 30f };

            for (int i = 0; i < count; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / count;
                float r = MathF.Sqrt(1f - y * y);
                points[i] = new Vector3(MathF.Cos(golden * i) * r, y, MathF.Sin(golden * i) * r);

                //A camera at elevation e below the ball looks along v = (cos a cos e, -sin e, sin a cos e); the point's
                //weight is max(0, d . v) = max(0, A cos(a - phi) + B) with A = r cos e, B = -y sin e, averaged over a
                float sum = 0f;
                foreach (float elevation in elevations)
                {
                    float e = MathHelper.ToRadians(elevation);
                    float a = r * MathF.Cos(e), b = -y * MathF.Sin(e);
                    sum += b >= a ? b : b <= -a ? 0f : (MathF.Sqrt(a * a - b * b) + b * MathF.Acos(-b / a)) / MathF.PI;
                }

                weights[i] = sum / elevations.Length;
            }

            return (points, weights);
        }

        //THE HUD'S DISC CARRIES THE FIGURE: the share of the disc it inks is the share of the ball's face-on half the
        //figure covers (the disc is that half seen straight on, so each point counts by how squarely it faces), nothing
        //is inked past the outline, and an emblem stands upright
        [Fact]
        public void TheHudDiscShowsTheFigureFaceOn()
        {
            const int RADIUS = 48;
            (Vector3[] points, _) = ViewWeightedSphere(60000);
            float worst = 0f;

            foreach (int set in Enumerable.Range(1, ColourCues.SET_COUNT))
                foreach (BallType type in Enum.GetValues<BallType>())
                {
                    ColourCues.Figure figure = ColourCues.FigureOf(set, type);
                    byte[] disc = ColourCueField.BakeDisc(figure, RADIUS);

                    Assert.True(NothingPastTheOutline(disc, RADIUS), $"set {set}, {type}: ink past the disc's outline");

                    double inside = 0, total = 0;
                    foreach (Vector3 q in points.Where(point => point.Z > 0f))
                    {
                        total += q.Z;
                        if (ColourCueField.Field(figure, q) > 0f) inside += q.Z;
                    }

                    float expected = (float)(inside / total), baked = DiscShare(disc, RADIUS);
                    worst = MathF.Max(worst, MathF.Abs(baked - expected));

                    Assert.True(MathF.Abs(baked - expected) <= 0.03f,
                        $"set {set}, {type} \"{figure.Name}\": the disc inks {baked:0.000} of itself, the face-on half is {expected:0.000} figure");
                }

            _output.WriteLine($"worst disc share difference {worst:0.0000}");
        }

        [Fact]
        public void TheHudDiscStandsAnEmblemUpright()
        {
            const int RADIUS = 48;
            ColourCues.Figure triangles = ColourCues.FigureOf(2, BallType.Type13);
            Assert.Equal((float)ColourCues.Emblem.Triangle, triangles.A.Y);

            byte[] disc = ColourCueField.BakeDisc(triangles, RADIUS);
            Assert.True(PointsUp(disc, RADIUS), "the triangle on the disc does not point up");

            //...and the check fires on the same disc turned upside down
            Assert.False(PointsUp(UpsideDown(disc, RADIUS), RADIUS));
        }

        //A triangle point up reaches twice as far above its middle as below it (its apex at 0.9 of its size, its base at
        //0.45): inked straight above the disc's centre at 0.4 of the radius, and not straight below it
        private static bool PointsUp(byte[] disc, int radius)
        {
            int size = 2 * radius + 2, centre = radius + 1, offset = (int)MathF.Round(0.4f * radius);

            byte above = disc[((centre - offset) * size + centre) * 4 + 3];
            byte below = disc[((centre + offset) * size + centre) * 4 + 3];

            return above > 128 && below < 128;
        }

        private static byte[] UpsideDown(byte[] disc, int radius)
        {
            int size = 2 * radius + 2, row = size * 4;
            byte[] flipped = new byte[disc.Length];
            for (int y = 0; y < size; y++) Array.Copy(disc, y * row, flipped, (size - 1 - y) * row, row);
            return flipped;
        }

        private static bool NothingPastTheOutline(byte[] disc, int radius)
        {
            int size = 2 * radius + 2;
            float centre = radius + 1f;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - centre, dy = y + 0.5f - centre;
                    if (dx * dx + dy * dy > radius * radius && disc[(y * size + x) * 4 + 3] != 0) return false;
                }

            return true;
        }

        private static float DiscShare(byte[] disc, int radius)
        {
            int size = 2 * radius + 2;
            double ink = 0;
            for (int i = 3; i < disc.Length; i += 4) ink += disc[i] / 255.0;

            return (float)(ink / (MathF.PI * radius * radius));
        }

        //...and the share check fires on a disc that inks nothing
        [Fact]
        public void TheDiscShareCheckFindsAnEmptyDisc()
        {
            ColourCues.Figure stars = ColourCues.FigureOf(1, BallType.Type7);
            byte[] empty = new byte[ColourCueField.BakeDisc(stars, 48).Length];

            Assert.True(DiscShare(ColourCueField.BakeDisc(stars, 48), 48) > 0.1f);
            Assert.True(DiscShare(empty, 48) < 0.01f);
        }

        private static string EffectFile(string name)
        {
            string relative = Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "InstancedModel", name);

            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}
