using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// Mars's craters are a plain's, not a highland's (#1167). The field kept the Moon's chances of a crater per cell, under
    /// which every octave covers about the same share of the ground: bowls lay over 22 % of the plain and rims over 56 %, the
    /// owner's "craters cover absolutely everything". These read the octaves out of <c>Mars.fx</c> itself (every
    /// <c>CraterLayer</c> call and every <c>BigCraterLayer</c> call, with the constants they name) and measure each one's share
    /// of the plain on its own rolls: <see cref="ShaderMath.Hash22(float, float)"/> is <c>NoiseHash22</c> line for line, so a
    /// walk over a square of cells rolls exactly the craters the shader cuts there. A crater stands inside its own cell, so an
    /// octave's share is the sum of its bowls' areas over the cells walked.
    /// </summary>
    public class MarsCraterCoverageTests
    {
        //Cells walked per octave, a square this many a side: 65 536 craters' rolls, a share to well under a tenth of a percent
        private const int CELLS = 256;

        private sealed record Octave(string Turn, float Period, float Seed, float Chance, bool Big);

        /// <summary>
        /// The field's bowls (all four <c>CraterLayer</c> octaves: the three of <c>CraterField</c> and the pixel's own) cover
        /// under a tenth of the plain, the issue's own measure: the shares summed, as if no two bowls overlapped.
        /// </summary>
        [Fact]
        public void TheFieldsBowlsCoverUnderATenthOfThePlain()
        {
            Octave[] field = FieldOctaves(out float minRadius, out float maxRadius);

            double bowls = field.Sum(o => BowlShare(o, minRadius, maxRadius));

            Assert.True(bowls < 0.10, $"the field's bowls cover {bowls:P1} of the plain, its rims to 1.6 radii {bowls * 2.56:P1}");
        }

        /// <summary>
        /// What the owner asked for in #638 stays (more big craters) and what he asked for here goes (fewer small ones): the
        /// big craters - the field's largest octave and the three <c>BigCraters</c> lattices - cover more of the plain than the
        /// field's three smaller octaves together.
        /// </summary>
        [Fact]
        public void MostOfTheCrateredGroundIsTheBigCraters()
        {
            Octave[] field = FieldOctaves(out float minRadius, out float maxRadius);
            Octave[] lattices = BigOctaves(out float bigMin, out float bigMax);
            float largest = field.Max(o => o.Period);

            double big = field.Where(o => o.Period == largest).Sum(o => BowlShare(o, minRadius, maxRadius))
                + lattices.Sum(o => BowlShare(o, bigMin, bigMax));
            double smaller = field.Where(o => o.Period != largest).Sum(o => BowlShare(o, minRadius, maxRadius));

            Assert.True(big > smaller, $"the big craters' bowls cover {big:P1} of the plain, the smaller octaves' {smaller:P1}");
        }

        /// <summary>
        /// The figure <c>Mars.fx</c> states an octave's share by - its chance times pi times <c>CraterLayer</c>'s mean squared
        /// radius, 0.0232 of a cell - is what the rolls give, octave by octave, to within a twentieth.
        /// </summary>
        [Fact]
        public void AnOctavesShareIsItsChanceTimesTheMeanBowl()
        {
            Octave[] field = FieldOctaves(out float minRadius, out float maxRadius);

            //radius = lerp(min, max, u * u) with u uniform: E[r^2] = min^2 + 2 min (max - min) / 3 + (max - min)^2 / 5
            float span = maxRadius - minRadius;
            double meanSquare = minRadius * minRadius + 2.0 * minRadius * span / 3.0 + span * span / 5.0;
            Assert.InRange(meanSquare, 0.0231, 0.0233);

            foreach (Octave octave in field)
            {
                double expected = octave.Chance * Math.PI * meanSquare;
                double measured = BowlShare(octave, minRadius, maxRadius);
                Assert.True(Math.Abs(measured - expected) < expected * 0.05,
                    $"the {octave.Period}-unit octave covers {measured:P2} on its rolls, {expected:P2} by its chance");
            }
        }

        /// <summary>
        /// A crater the clearing flattens keeps no pale rim either: <c>MarsHeight</c> multiplies the field's ejecta by the same
        /// ramp as its height, so no flat ring is painted on the clearing round the island.
        /// </summary>
        [Fact]
        public void TheClearingPaintsNoRims()
        {
            string code = Code();
            Match body = Regex.Match(code, @"float\s+MarsHeight\s*\([^)]*\)\s*\{(?<body>.*?)\n\}", RegexOptions.Singleline);
            Assert.True(body.Success, "Mars.fx has no MarsHeight");

            string text = body.Groups["body"].Value;
            Assert.Matches(@"CraterField\(\s*p\s*,\s*ejecta\s*\)", text);
            Assert.Matches(@"ejecta\s*\*=\s*ramp\s*;", text);
            Assert.Matches(@"CraterAmplitude\s*\*\s*ramp\s*\*\s*field", text);
        }

        //Every CraterLayer call of Mars.fx: four octaves, one to each of Craters.fxh's four turns, the pixel octave among them
        //(SmallCraterLayer), so a second call site with figures of its own cannot drift from the first
        private static Octave[] FieldOctaves(out float minRadius, out float maxRadius)
        {
            string code = Code();
            Dictionary<string, float> constants = Constants(code);
            minRadius = constants["CRATER_MIN_RADIUS"];
            maxRadius = constants["CRATER_MAX_RADIUS"];

            Octave[] octaves = Regex.Matches(code,
                    @"(?<!Big)CraterLayer\(\s*TurnCrater\(\s*[^,]+,\s*(CRATER_TURN_\d)\s*\)\s*\*\s*\(\s*1\.0\s*/\s*([A-Za-z_0-9.]+)\s*\)\s*,\s*([A-Za-z_0-9.]+)\s*,\s*([A-Za-z_0-9.]+)\s*,")
                .Select(m => new Octave(m.Groups[1].Value, Value(m.Groups[2].Value, constants), Value(m.Groups[3].Value, constants),
                    Value(m.Groups[4].Value, constants), Big: false))
                .ToArray();

            Assert.Equal(new[] { "CRATER_TURN_0", "CRATER_TURN_1", "CRATER_TURN_2", "CRATER_TURN_3" },
                octaves.Select(o => o.Turn).OrderBy(t => t, StringComparer.Ordinal).ToArray());
            return octaves;
        }

        //BigCraters' three lattices
        private static Octave[] BigOctaves(out float minRadius, out float maxRadius)
        {
            string code = Code();
            Dictionary<string, float> constants = Constants(code);
            minRadius = constants["BIG_CRATER_MIN"];
            maxRadius = constants["BIG_CRATER_MAX"];

            Octave[] octaves = Regex.Matches(code,
                    @"BigCraterLayer\(\s*p\s*,\s*([0-9.]+)\s*,\s*(BIG_CRATER_TURN_\d)\s*,\s*([0-9.]+)\s*,\s*([0-9.]+)\s*,")
                .Select(m => new Octave(m.Groups[2].Value, Value(m.Groups[1].Value, constants), Value(m.Groups[3].Value, constants),
                    Value(m.Groups[4].Value, constants), Big: true))
                .ToArray();

            Assert.Equal(3, octaves.Length);
            return octaves;
        }

        //The share of the plain an octave's bowls cover (inside the rim, d < 1), walked over CELLS x CELLS cells of its lattice:
        //the existence roll and the radius roll exactly as CraterLayer and BigCraterLayer make them, the cell a unit square
        private static double BowlShare(Octave octave, float minRadius, float maxRadius)
        {
            double area = 0.0;
            for (int cy = -CELLS / 2; cy < CELLS / 2; cy++)
            {
                for (int cx = -CELLS / 2; cx < CELLS / 2; cx++)
                {
                    float rollA = ShaderMath.Hash22(cx + octave.Seed, cy + octave.Seed).X * 0.5f + 0.5f;
                    if (rollA > octave.Chance) continue;

                    float rollB = ShaderMath.Hash22(cx + octave.Seed + 47.9f, cy + octave.Seed + 47.9f).X * 0.5f + 0.5f;
                    float radius = minRadius + (maxRadius - minRadius) * (octave.Big ? rollB : rollB * rollB);
                    area += Math.PI * radius * radius;
                }
            }

            return area / ((double)CELLS * CELLS);
        }

        private static float Value(string token, Dictionary<string, float> constants) =>
            float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                ? value
                : constants.TryGetValue(token, out value) ? value : throw new InvalidDataException($"Mars.fx names no constant {token}");

        private static Dictionary<string, float> Constants(string code) =>
            Regex.Matches(code, @"static\s+const\s+float\s+([A-Z_0-9]+)\s*=\s*(-?[0-9.]+)\s*;")
                .ToDictionary(m => m.Groups[1].Value, m => float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));

        //Mars.fx with its comments taken out, so a figure quoted in a comment is never read as code
        private static string Code() =>
            Regex.Replace(File.ReadAllText(FindFile(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Mars.fx"))),
                @"//[^\n]*", string.Empty);

        private static string FindFile(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}
