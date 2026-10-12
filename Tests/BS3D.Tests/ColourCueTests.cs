using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The colour cue prototypes (#737). Two promises are checkable without a device: the table gives every pair the
    /// prototypes exist for a figure that differs as well as a colour, and the shader file that holds the figures
    /// declares no uniform - which is what lets it sit in front of the three ball bodies without moving a single
    /// offset in the constant buffer, and so what keeps the shipped programs' code what it was.
    /// </summary>
    public class ColourCueTests
    {
        //The tight pairs the issue measured under wool: the dark trio, and the beige white against the yellow
        private static readonly BallType[][] TightGroups =
        {
            new[] { BallType.Type8, BallType.Type10, BallType.Type11 }, //black, brown, silver
            new[] { BallType.Type4, BallType.Type7 },                   //white (beige), yellow
        };

        [Fact]
        public void EveryTightPairDiffersInItsFigure()
        {
            foreach (BallType[] group in TightGroups)
                foreach (BallType a in group)
                    foreach (BallType b in group.Where(other => other != a))
                        Assert.True(ColourCues.SlotOf(a) != ColourCues.SlotOf(b),
                            $"{a} and {b} are one of the tight pairs and carry the same figure ({ColourCues.SlotOf(a)})");
        }

        [Fact]
        public void NoTwoColoursShareAFigureAndEverySlotIsTheShadersOwn()
        {
            int[] slots = Enum.GetValues<BallType>().Select(ColourCues.SlotOf).Where(slot => slot != ColourCues.NONE).ToArray();

            Assert.Equal(slots.Length, slots.Distinct().Count());
            Assert.All(slots, slot => Assert.InRange(slot, 1, ColourCues.SLOT_COUNT));
        }

        [Theory]
        [InlineData("none", ColourCueForm.None)]
        [InlineData("off", ColourCueForm.None)]
        [InlineData("motif", ColourCueForm.Motif)]
        [InlineData("A", ColourCueForm.Motif)]
        [InlineData("shape", ColourCueForm.Shape)]
        [InlineData("b", ColourCueForm.Shape)]
        public void TheArgumentsSpellingsParse(string text, ColourCueForm expected)
        {
            Assert.True(ColourCues.TryParseForm(text, out ColourCueForm form));
            Assert.Equal(expected, form);
            Assert.True(ColourCues.TryParseForm(ColourCues.Name(form), out ColourCueForm back));
            Assert.Equal(form, back);
        }

        [Fact]
        public void AnUnknownSpellingIsRefused() => Assert.False(ColourCues.TryParseForm("paint", out _));

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
        public void TheUniformCheckFindsAGlobal(string hlsl) => Assert.Single(GlobalDeclarations(hlsl));

        //...and stays quiet on everything the figures' file is allowed to hold
        [Theory]
        [InlineData("static const float S = 1;")]
        [InlineData("static const float3 A[2] =\n{\n    float3(1, 0, 0),\n    float3(0, 1, 0)\n};")]
        [InlineData("static const float3x3 F = float3x3(1, 0, 0, 0, 1, 0, 0, 0, 1);")]
        [InlineData("float F(float3 d, float c, out float o)\n{\n    o = 0;\n    [branch] if (c < 1) { o = 1; }\n    return d.x;\n}")]
        [InlineData("float4 PS(VSOut input) : COLOR { return 0; }")]
        [InlineData("struct S { float a; };")]
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
