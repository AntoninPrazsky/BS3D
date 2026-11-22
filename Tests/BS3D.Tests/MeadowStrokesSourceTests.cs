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

        //The readers below are SavannaStrokesSourceTests' too: the savanna draws the same GrassStrokes (#1234)
        internal static string FindFile(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }

        //The text of an effect file without its comments, so a word in a note is not taken for code
        internal static string Code(string file) =>
            Regex.Replace(File.ReadAllText(FindFile(Path.Combine(ShaderDirectory, file))), @"//[^\n]*", string.Empty);

        //The body of a function, from its signature to the closing brace at the same depth
        internal static string Body(string code, string signature)
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
        internal static double Constant(string code, string name)
        {
            Match match = Regex.Match(code, $@"static\s+const\s+float\s+{name}\s*=\s*([^;]+);");
            Assert.True(match.Success, $"No {name} is defined");

            return Convert.ToDouble(new DataTable().Compute(match.Groups[1].Value.Trim(), null), CultureInfo.InvariantCulture);
        }

        //Every call of a function, with its whole argument list (nested parentheses included)
        internal static string[] Calls(string code, string function)
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

            string strokes = Body(grass, "float GrassStrokes(");
            Assert.Matches(@"float\s+flank\s*=\s*1\.0\s*-\s*smoothstep\(\s*GRASS_STROKE_FLANK_FULL\s*,\s*GRASS_STROKE_FLANK_GONE\s*,\s*dot\(\s*normal\s*,\s*toCamera\s*\)\s*\)",
                strokes);

            //And the flank fades what is drawn, not only whether the branch runs: gating alone would draw the strokes whole up
            //to the angle where they vanish at once
            Assert.Matches(@"strokes\s*=\s*flank\s*\*\s*sum\s*\*\s*rsqrt\(\s*spread\s*\)\s*;", strokes);
        }

        [Fact]
        public void TheBandLimitSeesTheStrokesAxisAndItsSpin()
        {
            string grass = Code("Grass.fxh");
            string strokes = Body(grass, "float GrassStrokes(");

            //The footprint is measured in the strokes' own stretched domain, and grown by the bearing's turn over a pixel: the
            //domain is turned about an anchor up to a spacing and a half away, so a pixel's step turns it by that over the
            //distance to the lens. Without the growth the axis's spin near the lens aliases, which the screen steps alone do
            //not see (#1190's second "Wanted")
            Match footprint = Regex.Match(strokes,
                @"float\s+footprint\s*=\s*GrassStrokeFootprint\(\s*axis\s*,\s*dpdx\s*,\s*dpdy\s*\)\s*\*\s*frequency\s*\*\s*"
                + @"\(\s*1\.0\s*\+\s*([0-9.]+)\s*\*\s*spacing\s*/\s*max\(\s*lensDistance\s*,\s*[0-9.e-]+\s*\)\s*\)\s*;");
            Assert.True(footprint.Success, "GrassStrokes' footprint is not GrassStrokeFootprint(...) * frequency * (1.0 + k * spacing / max(lensDistance, ...))");
            double reach = double.Parse(footprint.Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.True(reach >= 1.0, $"The spin's reach is {reach:0.##} spacings: the blend reaches anchors a spacing away and more");

            //Every anchor's noise is band-limited by that footprint, and so is the branch that skips them
            Assert.Matches(@"Fbm2BandLimited\(\s*p\s*,\s*2\s*,\s*footprint\s*\)", Body(grass, "float GrassStrokeAnchor("));
            Assert.Matches(@"if\s*\(\s*flank\s*\*\s*saturate\(\s*1\.0\s*-\s*2\.0\s*\*\s*footprint\s*\)\s*>\s*0\.0\s*\)", strokes);
        }

        [Fact]
        public void TheStrokesFootprintIsAnisotropic()
        {
            string footprint = Body(Code("Grass.fxh"), "float GrassStrokeFootprint(");

            //Each screen step measured across the grain as it is and along it over the stretch, as the domain is: the strokes
            //run along the very direction a grazing view foreshortens, so an isotropic footprint (the longer side) faded them
            //where they still had pixels across them
            foreach (string step in new[] { "dpdx", "dpdy" })
                Assert.Matches($@"float2\(\s*dot\(\s*{step}\s*,\s*axis\s*\)\s*/\s*GRASS_STROKE_STRETCH\s*,\s*dot\(\s*{step}\s*,\s*across\s*\)\s*\)", footprint);
            Assert.Matches(@"max\(\s*length\(\s*stepX\s*\)\s*,\s*length\(\s*stepY\s*\)\s*\)", footprint);
        }
    }
}
