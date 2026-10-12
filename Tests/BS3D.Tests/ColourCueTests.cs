using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The colour cue's figure sets (#737). What is checkable without a device: every set gives every colour a figure
    /// and no two colours in a set the same one, the two inks keep each ball's average colour (a dark ink on the plain
    /// colour pulls the light colours towards the dark ones), the frames are rotations, and the shader file that holds
    /// the figures declares no uniform - which is what lets it sit in front of the three ball bodies without moving a
    /// single offset in the constant buffer, and so what keeps the shipped programs' code what it was.
    /// </summary>
    public class ColourCueTests
    {
        private static IEnumerable<int> AllSets => Enumerable.Range(1, ColourCues.SET_COUNT);

        public static TheoryData<int> Sets()
        {
            TheoryData<int> data = new();
            foreach (int set in AllSets) data.Add(set);
            return data;
        }

        [Theory]
        [MemberData(nameof(Sets))]
        public void EverySetGivesEveryColourAFigure(int set)
        {
            foreach (BallType type in Enum.GetValues<BallType>())
            {
                ColourCues.Figure figure = ColourCues.FigureOf(set, type);

                Assert.InRange(figure.A.X, (float)ColourCues.Kind.Bands, (float)ColourCues.Kind.Emblem);
                Assert.False(string.IsNullOrWhiteSpace(figure.Name), $"set {set}, {type}: a figure without a name");
                Assert.InRange(figure.C.W, 0.05f, 0.55f);
                Assert.False(ColourCues.Paint(set, type, Vector3.One).IsNone, $"set {set}, {type} paints nothing");
            }
        }

        [Theory]
        [MemberData(nameof(Sets))]
        public void NoTwoColoursInASetShareAFigure(int set)
        {
            BallType[] types = Enum.GetValues<BallType>();

            foreach (BallType a in types)
                foreach (BallType b in types.Where(other => other > a))
                {
                    ColourCues.Figure first = ColourCues.FigureOf(set, a), second = ColourCues.FigureOf(set, b);

                    Assert.False(first.Name == second.Name, $"set {set}: {a} and {b} are both \"{first.Name}\"");
                    Assert.False(first.A == second.A && first.B == second.B && first.C == second.C && first.Frame == second.Frame,
                        $"set {set}: {a} and {b} carry the same figure");
                }
        }

        //The owner chose the prototype's painted figures; the first set keeps the four he saw on the colours he saw them on
        [Theory]
        [InlineData(BallType.Type11, "three stripes")]
        [InlineData(BallType.Type10, "twelve dots")]
        [InlineData(BallType.Type4, "checks")]
        [InlineData(BallType.Type7, "stars")]
        public void TheFirstSetKeepsThePrototypesFigures(BallType type, string name) =>
            Assert.Equal(name, ColourCues.FigureOf(1, type).Name);

        //A figure is no figure if its inks are too alike to tell apart: a luminance ratio under 2.5 between the two
        //is a figure only up close (the prototype's palest pair, the vinyl silver's white stripes, did not read)
        [Theory]
        [MemberData(nameof(Sets))]
        public void TheFiguresInkStandsOffItsGround(int set)
        {
            foreach (BallType type in Enum.GetValues<BallType>())
            {
                (Vector3 figure, Vector3 ground) = Inks(set, type, out _);
                float a = ColorSpace.Luminance(figure), b = ColorSpace.Luminance(ground);

                Assert.True(MathF.Max(a, b) / MathF.Max(MathF.Min(a, b), 1e-6f) >= 2.5f,
                    $"set {set}, {type}: figure {a:0.0000} against ground {b:0.0000}");
            }
        }

        //The inks average to the plain colour, so a figure too small to see leaves the ball the colour it was. Channel
        //by channel, not in luminance alone: a grey in a pale ink keeps the luminance and loses the hue (it took the
        //navy 10-12 dE towards the black). Black alone cannot (nothing is darker than its ground); its figure is held
        //dim instead.
        [Theory]
        [MemberData(nameof(Sets))]
        public void TheInksKeepTheBallsAverageColour(int set)
        {
            foreach (BallType type in Enum.GetValues<BallType>())
            {
                (Vector3 figure, Vector3 ground) = Inks(set, type, out Vector3 plain);
                Vector3 average = AverageOf(figure, ground, ColourCues.FigureOf(set, type).C.W);

                if (type == BallType.Type8)
                    Assert.True(ColorSpace.Luminance(average) <= 0.02f,
                        $"set {set}: black averages {ColorSpace.Luminance(average):0.0000}, lighter than L* 15");
                else
                    Assert.True(AveragesTo(average, plain), $"set {set}, {type}: averages {average} against its plain {plain}");
            }
        }

        //The check above is evidence only if it fires on what it is meant to catch (BestPractices.md section 10): the
        //navy's pale ink with a grey of 0.05 in it, which keeps the navy's luminance to the third digit
        [Fact]
        public void TheAverageCheckFindsAGreyInAPaleInk()
        {
            Vector3 plain = ColorSpace.SrgbToLinear(BasicEffectParamsProvider.GetDiffuseTintByType(BallType.Type12));
            const float coverage = 0.2f;
            Vector3 figure = plain * 2.6f + new Vector3(0.05f);
            float ground = (ColorSpace.Luminance(plain) - coverage * ColorSpace.Luminance(figure))
                / ((1f - coverage) * ColorSpace.Luminance(plain));
            Vector3 average = AverageOf(figure, plain * ground, coverage);

            Assert.True(MathF.Abs(ColorSpace.Luminance(average) - ColorSpace.Luminance(plain)) < 1e-4f);
            Assert.False(AveragesTo(average, plain));
        }

        private static Vector3 AverageOf(Vector3 figure, Vector3 ground, float coverage) => figure * coverage + ground * (1f - coverage);

        //Every channel within 2 % of the plain colour's
        private static bool AveragesTo(Vector3 average, Vector3 plain) =>
            MathF.Abs(average.X - plain.X) <= 0.02f * plain.X + 1e-6f
            && MathF.Abs(average.Y - plain.Y) <= 0.02f * plain.Y + 1e-6f
            && MathF.Abs(average.Z - plain.Z) <= 0.02f * plain.Z + 1e-6f;

        //...and the ground is the plain colour scaled, never another hue: the marble scales its veined stone by it
        [Theory]
        [MemberData(nameof(Sets))]
        public void TheGroundIsThePlainColourScaled(int set)
        {
            foreach (BallType type in Enum.GetValues<BallType>())
            {
                (_, Vector3 ground) = Inks(set, type, out Vector3 plain);
                float scale = ground.X / plain.X;

                Assert.Equal(scale, ground.Y / plain.Y, 3);
                Assert.Equal(scale, ground.Z / plain.Z, 3);
            }
        }

        //The frames are typed in as numbers; one mistyped digit would shear a figure rather than turn it
        [Theory]
        [MemberData(nameof(Sets))]
        public void EveryFrameIsARotation(int set)
        {
            foreach (BallType type in Enum.GetValues<BallType>())
            {
                Matrix frame = ColourCues.FrameOf(ColourCues.FigureOf(set, type));
                Vector3[] rows = { new(frame.M11, frame.M12, frame.M13), new(frame.M21, frame.M22, frame.M23), new(frame.M31, frame.M32, frame.M33) };

                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                        Assert.True(MathF.Abs(Vector3.Dot(rows[i], rows[j]) - (i == j ? 1f : 0f)) < 1e-4f,
                            $"set {set}, {type}: frame row {i} . row {j} = {Vector3.Dot(rows[i], rows[j])}");

                Assert.True(MathF.Abs(frame.Determinant() - 1f) < 1e-4f, $"set {set}, {type}: a frame is a reflection");
            }
        }

        //No family of bands covers its frame's poles: a cap there inside the next band round is a disc in a ring, the
        //gravity well's bullseye. BallColourCue.fxh's band field, ported, over a cap 14 degrees round each pole.
        [Theory]
        [MemberData(nameof(Sets))]
        public void NoBandsCoverTheirPoles(int set)
        {
            foreach (BallType type in Enum.GetValues<BallType>())
            {
                ColourCues.Figure figure = ColourCues.FigureOf(set, type);
                if (figure.A.X != (float)ColourCues.Kind.Bands) continue;

                Assert.True(PoleCapOutside(figure.A, figure.B), $"set {set}, {type}: \"{figure.Name}\" covers its frame's pole");
            }
        }

        //...and the check fires on four bands at phase 0, whose poles fall inside a band
        [Fact]
        public void ThePoleCheckFindsACap() =>
            Assert.False(PoleCapOutside(new Vector4((float)ColourCues.Kind.Bands, 4f, 0f, 0.5f), Vector4.Zero));

        private static bool PoleCapOutside(Vector4 a, Vector4 b)
        {
            for (float along = 0.97f; along <= 1.0001f; along += 0.01f)
                for (int sign = -1; sign <= 1; sign += 2)
                    for (int step = 0; step < 72; step++)
                    {
                        float around = step * MathF.PI / 36f;
                        float t = a.Y * along * sign + a.Z + b.X * MathF.Sin(b.Y * around);
                        float field = MathF.Cos(MathF.PI * t) - MathF.Cos(MathF.PI * a.W);
                        if (b.Z > 0f) field = MathF.Min(field, MathF.Cos(MathF.PI * b.Z) - MathF.Cos(MathF.PI * t));
                        if (field > 0f) return false;
                    }

            return true;
        }

        [Fact]
        public void NoSetPaintsNothing() => Assert.True(ColourCues.Paint(ColourCues.NONE, BallType.Type1, Vector3.One).IsNone);

        [Theory]
        [InlineData("none", 0)]
        [InlineData("off", 0)]
        [InlineData("motif", 1)]
        [InlineData("A", 1)]
        [InlineData("motif:1", 1)]
        [InlineData("motif:2", 2)]
        [InlineData("Motif:3", 3)]
        [InlineData("motif:4", 4)]
        [InlineData("emblems", 2)]
        [InlineData("spots", 4)]
        public void TheArgumentsSpellingsParse(string text, int expected)
        {
            Assert.True(ColourCues.TryParse(text, out int set));
            Assert.Equal(expected, set);
            Assert.True(ColourCues.TryParse(ColourCues.Name(set), out int back));
            Assert.Equal(set, back);
        }

        //The carved prototype is gone, and a set that does not exist is not quietly the first one
        [Theory]
        [InlineData("shape")]
        [InlineData("b")]
        [InlineData("motif:0")]
        [InlineData("motif:5")]
        [InlineData("motif:")]
        [InlineData("paint")]
        public void AnUnknownSpellingIsRefused(string text) => Assert.False(ColourCues.TryParse(text, out _));

        private static (Vector3 figure, Vector3 ground) Inks(int set, BallType type, out Vector3 plain)
        {
            plain = ColorSpace.SrgbToLinear(BasicEffectParamsProvider.GetDiffuseTintByType(type));
            return ColourCues.Inks(type, plain, ColourCues.FigureOf(set, type).C.W);
        }

        [Fact]
        public void TheFiguresFileDeclaresNoUniform()
        {
            string[] uniforms = GlobalDeclarations(File.ReadAllText(FiguresFile()));

            Assert.True(uniforms.Length == 0,
                "BallColourCue.fxh declares " + string.Join(", ", uniforms) + ": a uniform here moves the constant buffer "
                + "of every program after it; it belongs in BallColourCueTechniques.fxh, last in the effect");
        }

        //The check above is evidence only if it fires on what it is meant to catch (BestPractices.md section 10).
        //Every one of these goes into $Globals, the non-static const too (d3dcompiler_47 warns X3207 and lays it
        //out at offset 0), and none of them is a line the earlier one-line pattern of this test caught.
        [Theory]
        [InlineData("const float NotStatic = 0.5;")]
        [InlineData("extern float E;")]
        [InlineData("matrix M;")]
        [InlineData("int2 I;")]
        [InlineData("    float Indented;")]
        [InlineData("uniform float U;")]
        [InlineData("float4 V = float4(1, 2, 3, 4);")]
        [InlineData("Texture2D T;")]
        [InlineData("static const float S = 1;\nfloat F(float3 d) { return d.x; }\nfloat AfterAFunction;")]
        [InlineData("struct P { float a; };\nP AStructUniform;")]
        public void TheUniformCheckFindsAGlobal(string hlsl) => Assert.Single(GlobalDeclarations(hlsl));

        //...and stays quiet on everything the figures' file is allowed to hold
        [Theory]
        [InlineData("static const float S = 1;")]
        [InlineData("static const float3 A[2] =\n{\n    float3(1, 0, 0),\n    float3(0, 1, 0)\n};")]
        [InlineData("static const float3x3 F = float3x3(1, 0, 0, 0, 1, 0, 0, 0, 1);")]
        [InlineData("float F(float3 d, float c, out float o)\n{\n    o = 0;\n    [branch] if (c < 1) { o = 1; }\n    return d.x;\n}")]
        [InlineData("float4 PS(VSOut input) : COLOR { return 0; }")]
        [InlineData("struct S { float a; };")]
        [InlineData("struct S\n{\n    float4 a;\n    float3x3 frame;\n};")]
        [InlineData("float G(float3 d, S cue) { return d.x; }")]
        [InlineData("#include \"x.fxh\"\n// float Commented;\n/* float Blocked; */")]
        public void TheUniformCheckPassesWhatIsNotAGlobal(string hlsl) => Assert.Empty(GlobalDeclarations(hlsl));

        /// <summary>
        /// The top-level declarations of an HLSL source that are neither a static constant nor a function (nor a struct
        /// or a technique): what the compiler lays out in <c>$Globals</c>. Comments and preprocessor lines go first;
        /// every brace block at the top level is then cut down to a statement end, so a function body and an
        /// initializer list both leave their head behind as a statement of its own, and each statement is judged whole.
        /// </summary>
        internal static string[] GlobalDeclarations(string hlsl)
        {
            string code = Regex.Replace(hlsl, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            code = Regex.Replace(code, @"//[^\n]*", string.Empty);
            code = Regex.Replace(code, @"^[ \t]*#[^\n]*", string.Empty, RegexOptions.Multiline);

            StringBuilder topLevel = new();
            int depth = 0;

            foreach (char c in code)
            {
                if (c == '{')
                {
                    if (depth++ == 0) topLevel.Append(';');
                }
                else if (c == '}') depth--;
                else if (depth == 0) topLevel.Append(c);
            }

            return topLevel.ToString().Split(';')
                .Select(statement => Regex.Replace(statement, @"\s+", " ").Trim())
                .Where(statement => statement.Length > 0
                    && !Regex.IsMatch(statement, @"^(?:static|struct|technique\w*)\b")
                    && !Regex.IsMatch(statement, @"^[A-Za-z_][\w ]*\([^;]*\)(?: ?: ?\w+)?$"))
                .ToArray();
        }

        private static string FiguresFile()
        {
            string relative = Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "InstancedModel", "BallColourCue.fxh");

            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}
