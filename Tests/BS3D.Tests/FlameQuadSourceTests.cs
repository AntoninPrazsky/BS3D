using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The savanna fire's quad is wider than the fire and its heat tapers to nothing before the quad's border (#886): a
    /// billboard is a rectangle, and a tongue that reached its side showed a straight vertical cut in some phases. No
    /// test can run the pixel shader without a graphics device, but the cure is three places in <c>Flame.fx</c> that have
    /// to agree on one number, and a change to one of them alone brings the cut back: the vertex shader's widening of the
    /// corner (position and UV), and the pixel shader's taper, which has to end exactly at the widened border and run
    /// BEFORE the clip that discards a faint pixel (a taper after it would leave the clip to cut at the heat's own level).
    /// </summary>
    public class FlameQuadSourceTests
    {
        private static readonly string Source = FindFile(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Flame.fx"));

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
            Assert.True(start >= 0, $"Flame.fx has no {signature}");

            int open = code.IndexOf('{', start), depth = 0;
            for (int i = open; i < code.Length; i++)
            {
                if (code[i] == '{') depth++;
                else if (code[i] == '}' && --depth == 0) return code.Substring(open, i - open + 1);
            }

            throw new InvalidDataException($"The body of {signature} never closes.");
        }

        [Fact]
        public void TheQuadIsWiderThanTheFire()
        {
            Match widen = Regex.Match(Code(), @"#define\s+FLAME_QUAD_WIDEN\s+([0-9.]+)");
            Assert.True(widen.Success, "Flame.fx defines no FLAME_QUAD_WIDEN: the quad is exactly as wide as the fire's U range of -1..1 and a tongue reaching its side is a straight cut");
            Assert.True(float.Parse(widen.Groups[1].Value, CultureInfo.InvariantCulture) > 1f, "FLAME_QUAD_WIDEN must exceed 1, or there is no margin for the taper");
        }

        [Fact]
        public void TheVertexShaderWidensThePositionAndTheUvTogether()
        {
            string body = Body(Code(), "FlameVertexOutput FlameVS(");

            //One widened U feeds both: a position widened without the UV would stretch the tongues, a UV without the position would shrink them
            Match widened = Regex.Match(body, @"float\s+(\w+)\s*=\s*input\.Data\.x\s*\*\s*FLAME_QUAD_WIDEN\s*;");
            Assert.True(widened.Success, "FlameVS does not scale the corner's U by FLAME_QUAD_WIDEN");

            string name = widened.Groups[1].Value;
            Assert.Matches($@"FacingRight\(\s*subPosition\s*\)\s*\*\s*\(\s*{name}\s*\*\s*w\s*\)", body);
            Assert.Matches($@"output\.UV\s*=\s*float2\(\s*{name}\s*,\s*input\.Data\.y\s*\)\s*;", body);
        }

        [Fact]
        public void TheHeatTapersToNothingAtTheBorderBeforeTheClip()
        {
            string body = Body(Code(), "float4 FlamePS(");

            int taper = body.IndexOf("heat *= smoothstep(FLAME_QUAD_WIDEN, 1.0, abs(u));", StringComparison.Ordinal);
            int clip = body.IndexOf("clip(heat", StringComparison.Ordinal);

            Assert.True(taper >= 0, "FlamePS does not fade the heat from |u| = 1 to nothing at |u| = FLAME_QUAD_WIDEN");
            Assert.True(clip >= 0, "FlamePS no longer clips a faint pixel");
            Assert.True(taper < clip, "The taper has to come before the clip, or the clip is what ends the fire at the border");
        }
    }
}
