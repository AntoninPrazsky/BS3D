using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The meadow's blades seen from the side are fixed to the ground (#1190). They were a noise combed along the ground's
    /// bearing to the lens on a domain turned about the WORLD ORIGIN, so every stroke was a whole ray from the point under
    /// the lens: a fan fixed to the screen that the ground slid under, re-rolled with every step of the lens, and finer the
    /// farther the lens stood from the arena. The owner saw it as grass that "undulates and glitters" with "one pattern
    /// that does not move at all" when the intro looked down at it. <c>GrassStrokes</c> in <c>Grass.fxh</c> turns the
    /// domain by the same bearing about anchors of a ground grid instead ("BLADES SEEN FROM THE SIDE" there, and "The
    /// blades seen from the side" in <c>docs/scenes/meadow.md</c>). No test can run the pixel shader without a graphics
    /// device, and a tidy-up that put the old one-line call back would look the same in a still frame; this reads the two
    /// files and holds the construction, as <see cref="PolarSastrugiSourceTests"/> holds the sastrugi's band limit.
    /// </summary>
    public class MeadowStrokesSourceTests
    {
        private static readonly string ShaderDirectory = Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders");

        private static string FindFile(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }

        //The text of an effect file without its comments, so a word in a note is not taken for code
        private static string Code(string file) =>
            Regex.Replace(File.ReadAllText(FindFile(Path.Combine(ShaderDirectory, file))), @"//[^\n]*", string.Empty);

        //The body of a function, from its signature to the closing brace at the same depth
        private static string Body(string code, string signature)
        {
            int start = code.IndexOf(signature, StringComparison.Ordinal);
            Assert.True(start >= 0, $"No {signature} was found");

            int open = code.IndexOf('{', start), depth = 0;
            for (int i = open; i < code.Length; i++)
            {
                if (code[i] == '{') depth++;
                else if (code[i] == '}' && --depth == 0) return code.Substring(open, i - open + 1);
            }

            throw new InvalidDataException($"The body of {signature} never closes.");
        }

        //A static const's value
        private static double Constant(string code, string name)
        {
            Match match = Regex.Match(code, $@"static\s+const\s+float\s+{name}\s*=\s*([^;]+);");
            Assert.True(match.Success, $"No {name} is defined");

            return Convert.ToDouble(new DataTable().Compute(match.Groups[1].Value.Trim(), null), CultureInfo.InvariantCulture);
        }

        //Every call of a function, with its whole argument list (nested parentheses included)
        private static string[] Calls(string code, string function)
        {
            System.Collections.Generic.List<string> calls = new();
            foreach (Match match in Regex.Matches(code, $@"\b{function}\s*\("))
            {
                int depth = 0;
                for (int i = match.Index + match.Length - 1; i < code.Length; i++)
                {
                    if (code[i] == '(') depth++;
                    else if (code[i] == ')' && --depth == 0)
                    {
                        calls.Add(code.Substring(match.Index, i - match.Index + 1));
                        break;
                    }
                }
            }

            return calls.ToArray();
        }

        [Fact]
        public void TheMeadowCombsNoNoiseAlongTheBearingToTheLens()
        {
            string meadow = Code("Meadow.fx");

            //The old strokes: Fbm2Combed(worldPosition.xz * f, CameraPosition.xz - worldPosition.xz, ...), the domain turned
            //about the world origin by the pixel's bearing
            foreach (string call in Calls(meadow, "Fbm2Combed"))
                Assert.DoesNotContain("CameraPosition", call);

            string[] strokes = Calls(meadow, "GrassStrokes");
            Assert.Single(strokes);
            Assert.Contains("CameraPosition.xz - worldPosition.xz", strokes[0]);
        }

        [Fact]
        public void EachAnchorsDomainSeesOnlyTheOffsetFromItsAnchor()
        {
            string anchor = Body(Code("Grass.fxh"), "float GrassStrokeAnchor(");

            //Turned by the pixel's bearing, but about the anchor, and moved into the noise by the anchor's own offset
            Assert.Matches(@"float2\(\s*dot\(\s*scaled\s*,\s*axis\s*\)\s*/\s*GRASS_STROKE_STRETCH\s*,\s*dot\(\s*scaled\s*,\s*across\s*\)\s*\)"
                + @"\s*\+\s*NoiseHash22\(\s*cell\s*\)\s*\*\s*GRASS_STROKE_SCATTER", anchor);
        }

        [Fact]
        public void TheStrokesTakeTheGroundPositionOnlyToFindTheirAnchors()
        {
            string strokes = Body(Code("Grass.fxh"), "float GrassStrokes(");

            //The absolute position goes in once, to the grid; an offset from an anchor is what is turned. A dot of xz itself
            //with the bearing is the world-origin turn this replaced
            Assert.Single(Regex.Matches(strokes, @"\bxz\b"));
            Assert.Matches(@"float2\s+grid\s*=\s*xz\s*/\s*spacing\s*-\s*0\.5\s*;", strokes);
            Assert.Matches(@"float2\s+t\s*=\s*grid\s*-\s*cell\s*;", strokes);
            Assert.Matches(@"float2\s+scaled\s*=\s*t\s*\*\s*GRASS_STROKE_STRETCH\s*;", strokes);

            //Four anchors, blended with the spread kept
            Assert.Equal(4, Calls(strokes, "GrassStrokeAnchor").Length);
            Assert.Matches(@"rsqrt\(\s*spread\s*\)", strokes);
        }

        [Fact]
        public void TheAnchorsAreAStrokesLengthApart()
        {
            string strokes = Body(Code("Grass.fxh"), "float GrassStrokes(");

            //Closer anchors would chop the strokes shorter than they are; farther ones would let them drift more over the
            //ground as the lens moves (about the spacing over the distance to the lens)
            Assert.Matches(@"float\s+spacing\s*=\s*GRASS_STROKE_STRETCH\s*/\s*frequency\s*;", strokes);
        }

        [Fact]
        public void TheStrokesAreGoneBeforeTheViewLooksStraightDown()
        {
            string grass = Code("Grass.fxh");
            double full = Constant(grass, "GRASS_STROKE_FLANK_FULL");
            double gone = Constant(grass, "GRASS_STROKE_FLANK_GONE");

            //The cosine between the ground's normal and the view: the strokes must be gone well short of 1 (straight down),
            //where every bearing meets in the middle of the screen
            Assert.True(gone <= Math.Cos(Math.PI / 6.0),
                $"GRASS_STROKE_FLANK_GONE is {gone:0.###}: the strokes must be gone by 60 degrees over the ground, or a view looking down keeps their fan (#1190)");
            Assert.True(full > 0.0 && full < gone, $"GRASS_STROKE_FLANK_FULL ({full:0.###}) must lie between 0 and GRASS_STROKE_FLANK_GONE ({gone:0.###})");

            Assert.Matches(@"1\.0\s*-\s*smoothstep\(\s*GRASS_STROKE_FLANK_FULL\s*,\s*GRASS_STROKE_FLANK_GONE\s*,\s*dot\(\s*normal\s*,\s*toCamera\s*\)\s*\)",
                Body(grass, "float GrassStrokes("));
        }
    }
}
