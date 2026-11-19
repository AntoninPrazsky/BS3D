using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The polar sastrugi are band-limited far short of the noise's own Nyquist (#1168): faded on the standard rule, to
    /// nothing at half a cell a pixel of the world footprint, the plain the play view looks across photographed as a comb
    /// of horizontal stripes, which the owner read as a moire. Their normal comes from derivatives taken per 2x2 quad,
    /// <c>1 - |n|</c> makes every crest a knife edge, and the scene's low sun turns any tilt left into a full stripe, so
    /// the comb went only once the field was gone by sixteen pixels a cell (measured, "The sastrugi's band limit" in
    /// <c>docs/scenes/polar.md</c>). No test can run the pixel shader without a graphics device, and nothing on the picture
    /// would say which of the promises below a tidy-up broke; this reads <c>Polar.fx</c> and holds them, as
    /// <see cref="FlameQuadSourceTests"/> holds the flame's.
    /// </summary>
    public class PolarSastrugiSourceTests
    {
        private static readonly string Source = FindFile(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Polar.fx"));

        private static string FindFile(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }

        //The text of the effect without its comments, so a word in a note is not taken for code
        private static string Code() => Regex.Replace(File.ReadAllText(Source), @"//[^\n]*", string.Empty);

        //The body of a function, from its signature to the closing brace at the same depth
        private static string Body(string code, string signature)
        {
            int start = code.IndexOf(signature, StringComparison.Ordinal);
            Assert.True(start >= 0, $"Polar.fx has no {signature}");

            int open = code.IndexOf('{', start), depth = 0;
            for (int i = open; i < code.Length; i++)
            {
                if (code[i] == '{') depth++;
                else if (code[i] == '}' && --depth == 0) return code.Substring(open, i - open + 1);
            }

            throw new InvalidDataException($"The body of {signature} never closes.");
        }

        //A static const's value, its expression evaluated (the thresholds are written as fractions)
        private static double Constant(string code, string name)
        {
            Match match = Regex.Match(code, $@"static\s+const\s+float\s+{name}\s*=\s*([^;]+);");
            Assert.True(match.Success, $"Polar.fx defines no {name}");

            return Convert.ToDouble(new DataTable().Compute(match.Groups[1].Value.Trim(), null), CultureInfo.InvariantCulture);
        }

        [Fact]
        public void TheFieldIsGoneBySixteenPixelsACell()
        {
            string code = Code();
            double whole = Constant(code, "SASTRUGI_WHOLE_CELLS");
            double gone = Constant(code, "SASTRUGI_GONE_CELLS");

            Assert.True(gone <= 1.0 / 16.0 + 1e-9,
                $"SASTRUGI_GONE_CELLS is {gone:0.####}: the sastrugi must be gone by the time a pixel spans a sixteenth of a cell, or the play view's plain keeps the comb (#1168)");
            Assert.True(whole > 0.0 && whole < gone, $"SASTRUGI_WHOLE_CELLS ({whole:0.####}) must lie between nothing and SASTRUGI_GONE_CELLS ({gone:0.####})");

            //The fade itself runs from the first to the second and nowhere else
            Assert.Matches(@"1\.0\s*-\s*smoothstep\(\s*SASTRUGI_WHOLE_CELLS\s*,\s*SASTRUGI_GONE_CELLS\s*,\s*cellsPerPixel\s*\)",
                Body(code, "float SastrugiResolvable("));
        }

        [Fact]
        public void TheSpanIsTakenInTheFieldsOwnCells()
        {
            string body = Body(Code(), "float Sastrugi(");

            //The world footprint is not the yardstick: the field is stretched along the wind, so a pixel's share of a cell
            //is the derivative of the domain the noise is sampled on
            Match domain = Regex.Match(body, @"float2\s+(\w+)\s*=\s*WindSpace\(\s*p\s*\)\s*\*\s*float2\(\s*1\.0\s*/\s*DriftStretch\s*,\s*1\.0\s*\)\s*\*\s*DriftFrequency\s*;");
            Assert.True(domain.Success, "Sastrugi does not sample its noise on WindSpace(p) stretched by DriftStretch and scaled by DriftFrequency");

            string w = domain.Groups[1].Value;
            Match cells = Regex.Match(body, $@"float\s+(\w+)\s*=\s*length\(\s*fwidth\(\s*{w}\s*\)\s*\)\s*;");
            Assert.True(cells.Success, $"Sastrugi does not take the share of a cell a pixel spans as length(fwidth({w}))");

            Assert.DoesNotMatch(@"footprint", body);
            Assert.Matches($@"GradientNoise2\(\s*{w}\s*\)", body);
            Assert.Matches($@"return\s+lerp\(\s*SASTRUGI_RIDGES_MEAN\s*,\s*\w+\s*,\s*SastrugiResolvable\(\s*{cells.Groups[1].Value}\s*\)\s*\)\s*;", body);
        }

        [Fact]
        public void TheFineOctaveFadesOnItsOwnFrequency()
        {
            string body = Body(Code(), "float Sastrugi(");

            //The share of a coarse cell a pixel spans, as TheSpanIsTakenInTheFieldsOwnCells holds it
            Match cells = Regex.Match(body, @"float\s+(\w+)\s*=\s*length\(\s*fwidth\(\s*\w+\s*\)\s*\)\s*;");
            Assert.True(cells.Success, "Sastrugi takes no share of a cell a pixel spans");

            //Sampled at SASTRUGI_FINE_SCALE times the coarse octave's frequency, so its share of a cell is that many times the coarse one's
            Match fine = Regex.Match(body, @"float\s+(\w+)\s*=\s*1\.0\s*-\s*abs\(\s*GradientNoise2\(\s*\w+\s*\*\s*SASTRUGI_FINE_SCALE\b");
            Assert.True(fine.Success, "Sastrugi's fine octave is not sampled at SASTRUGI_FINE_SCALE times the coarse domain");

            string name = fine.Groups[1].Value;
            Assert.Matches(
                $@"{name}\s*=\s*lerp\(\s*SASTRUGI_FINE_MEAN\s*,\s*{name}\s*,\s*SastrugiResolvable\(\s*{cells.Groups[1].Value}\s*\*\s*SASTRUGI_FINE_SCALE\s*\)\s*\)\s*;",
                body);
        }

        /// <summary>
        /// Each octave fades towards the mean of what it replaces. The tone darkens with the sastrugi, so a fade to any
        /// other value moves the brightness of the whole plain inside the fade: towards 0.5, about the field's third
        /// percentile, that plain came out a ninth darker than the carved snow before it. The means are sampled here off
        /// <see cref="ShaderMath.Noise"/>, which is <c>GradientNoise2</c>'s lines in C#, through the ridges' own weights
        /// as <c>Polar.fx</c> writes them, so changing a weight without its mean fails too.
        /// </summary>
        [Fact]
        public void EachOctaveFadesTowardsItsOwnMean()
        {
            string code = Code();
            string body = Body(code, "float Sastrugi(");

            Match ridges = Regex.Match(body,
                @"float\s+(\w+)\s*=\s*saturate\(\s*(\w+)\s*\*\s*\2\s*\*\s*([0-9.]+)\s*\+\s*(\w+)\s*\*\s*([0-9.]+)\s*\)\s*;");
            Assert.True(ridges.Success, "Sastrugi's ridges are not saturate(coarse * coarse * a + fine * b)");
            Assert.Matches($@"return\s+lerp\(\s*SASTRUGI_RIDGES_MEAN\s*,\s*{ridges.Groups[1].Value}\s*,", body);

            double coarseWeight = double.Parse(ridges.Groups[3].Value, CultureInfo.InvariantCulture);
            double fineWeight = double.Parse(ridges.Groups[5].Value, CultureInfo.InvariantCulture);
            float fineScale = (float)Constant(code, "SASTRUGI_FINE_SCALE");

            //Far more cells than the field's correlation length, so the sample's own error is a few ten-thousandths
            Random random = new(1168);
            double fineSum = 0.0, ridgesSum = 0.0;
            const int samples = 1_000_000;
            for (int i = 0; i < samples; i++)
            {
                var p = new Vector2((float)(random.NextDouble() * 800.0 - 400.0), (float)(random.NextDouble() * 800.0 - 400.0));

                double coarse = 1.0 - Math.Abs(ShaderMath.Noise(p));
                double fine = 1.0 - Math.Abs(ShaderMath.Noise(p * fineScale + new Vector2(11.7f)));

                fineSum += fine;
                ridgesSum += Math.Clamp(coarse * coarse * coarseWeight + fine * fineWeight, 0.0, 1.0);
            }

            double fineMean = fineSum / samples, ridgesMean = ridgesSum / samples;

            Assert.InRange(Constant(code, "SASTRUGI_FINE_MEAN"), fineMean - 0.01, fineMean + 0.01);
            Assert.InRange(Constant(code, "SASTRUGI_RIDGES_MEAN"), ridgesMean - 0.01, ridgesMean + 0.01);
        }
    }
}
