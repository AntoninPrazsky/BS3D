using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The Potato tier's effect sources (<c>GamePi/Shaders</c>) are compiled twice (#808): for OpenGL, into GamePi,
    /// and for DirectX 11, into the Windows build's <c>potato</c> run. Nothing here compiles one - no test can, without
    /// the Windows SDK's compiler - but the three things that broke the DirectX build are all visible in the text, and
    /// one of them broke it SILENTLY: an effect whose pixel shader's input does not begin with the position compiles,
    /// loads and draws under DirectX with every input reading its neighbour's value (the confetti fell black).
    /// </summary>
    public class PotatoShaderSourceTests
    {
        private static readonly string Sources = FindUnderRepository(Path.Combine("GamePi", "Shaders"));

        private static string FindUnderRepository(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (Directory.Exists(candidate) || File.Exists(candidate)) return candidate;
            }

            throw new DirectoryNotFoundException($"No {relative} was found above the test assembly.");
        }

        public static IEnumerable<object[]> Effects() =>
            Directory.GetFiles(Sources, "*.fx").OrderBy(path => path, StringComparer.Ordinal).Select(path => new object[] { Path.GetFileName(path) });

        //The text of an effect without its comments, so a word in a note is not taken for code
        private static string Code(string fileName) =>
            Regex.Replace(File.ReadAllText(Path.Combine(Sources, fileName)), @"//[^\n]*", string.Empty);

        [Fact]
        public void ThereAreNineOfThem() => Assert.Equal(9, Effects().Count());

        [Theory]
        [MemberData(nameof(Effects))]
        public void ATechniqueNamesItsProfilesThroughTheTwoMacros(string fileName)
        {
            string code = Code(fileName);

            //DirectX 11 takes nothing below Shader Model 4 and MojoShader nothing above 3: a literal profile compiles
            //for one of the two builds only
            MatchCollection compiles = Regex.Matches(code, @"compile\s+(\w+)");
            Assert.NotEmpty(compiles);
            Assert.All(compiles, compile => Assert.Contains(compile.Groups[1].Value, new[] { "POTATO_VS", "POTATO_PS" }));

            Assert.Equal(compiles.Count(c => c.Groups[1].Value == "POTATO_VS"), compiles.Count(c => c.Groups[1].Value == "POTATO_PS"));
        }

        [Theory]
        [MemberData(nameof(Effects))]
        public void APixelShadersInputBeginsWithThePositionUnderDirectX(string fileName)
        {
            string code = Code(fileName);
            int passes = 0;

            foreach (Match pass in Regex.Matches(code,
                @"VertexShader\s*=\s*compile\s+\w+\s+(\w+)\s*\(\s*\)\s*;\s*PixelShader\s*=\s*compile\s+\w+\s+(\w+)\s*\(\s*\)\s*;"))
            {
                passes++;
                string vertexShader = pass.Groups[1].Value, pixelShader = pass.Groups[2].Value;

                //What the vertex shader returns and what the pixel shader takes first
                Match vertex = Regex.Match(code, $@"\b(\w+)\s+{vertexShader}\s*\(");
                Match pixel = Regex.Match(code, $@"\b\w+\s+{pixelShader}\s*\(\s*(\w+)\s+\w+");
                Assert.True(vertex.Success, $"{fileName}: no definition of {vertexShader} was found");
                Assert.True(pixel.Success, $"{fileName}: no definition of {pixelShader} with a structure for its input was found");

                string output = vertex.Groups[1].Value, input = pixel.Groups[1].Value;

                //The vertex output itself begins with the position, and is its own match
                Assert.Matches(@"^\s*float4\s+\w+\s*:\s*POSITION0?\s*;", StructBody(code, output, fileName));
                if (input == output) continue;

                //A structure of the pixel shader's own (Shader Model 3 may not hand it the position) must take the
                //position back under DirectX, FIRST, or every member after it is read one register off
                Assert.True(Regex.IsMatch(StructBody(code, input, fileName), @"^\s*POTATO_PIXEL_HEAD\b"),
                    $"{fileName}: {input} (read by {pixelShader}) does not begin with POTATO_PIXEL_HEAD, so under DirectX "
                    + $"its members are paired with {output}'s one register off");
            }

            Assert.True(passes > 0, $"{fileName}: no pass was found");
        }

        private static string StructBody(string code, string name, string fileName)
        {
            Match body = Regex.Match(code, $@"struct\s+{name}\s*\{{([^}}]*)\}}");
            Assert.True(body.Success, $"{fileName}: no struct {name} was found");

            return body.Groups[1].Value;
        }

        [Fact]
        public void BothBuildsCompileTheSameNine()
        {
            string[] effects = Effects().Select(row => Path.GetFileNameWithoutExtension((string)row[0])).ToArray();

            //GamePi's own list (OpenGL, compile.ps1)...
            string openGl = File.ReadAllText(Path.Combine(Sources, "Shaders.mgcb"));

            //...and the Windows build's, where each lands under Content/Potato/Shaders, apart from the desktop's
            string directX = File.ReadAllText(FindUnderRepository(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders.mgcb")));

            Assert.All(effects, effect =>
            {
                Assert.Contains($"/build:{effect}.fx\n", openGl.Replace("\r", string.Empty));
                Assert.Contains($"/build:../../../GamePi/Shaders/{effect}.fx;Potato/Shaders/{effect}.fx\n", directX.Replace("\r", string.Empty));
            });

            Assert.Equal(effects.Length, Regex.Matches(directX, @";Potato/Shaders/").Count);
        }
    }
}
