using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The sun shadow's receivers read the map through four gathers on DirectX and lift their lookup by a texel in world
    /// units the CPU hands them (#1101). Neither can be run without a device, so this holds what can be held: the
    /// texel <see cref="SunShadowMap.FitWindow"/> hands over is the very one the shader used to work out of the
    /// matrix per pixel, at every sun the scenes stand under; and <c>Shadows.fxh</c>'s DirectX path reads its block in
    /// four gathers with no point tap left, each stepped to its own quarter of the block and its components laid
    /// into the block's rows in the order a gather returns them, while the GL path (Shader Model 3.0, no gather)
    /// keeps its sixteen taps. How the map's figures reach a receiver is held by the types rather than by a test:
    /// <see cref="SunShadowMap.Fit"/> stores <see cref="SunShadowMap.FitWindow"/>'s answer whole, and
    /// <c>ShadowReceiver.Push</c> reads the texels off the map, so no two of its floats can trade places.
    /// </summary>
    public class SunShadowTexelTests
    {
        //The suns: high and low, every quarter of the compass, and one near the zenith where Fit turns its up vector
        public static TheoryData<float, float> Suns() => new()
        {
            { 70f, 0f }, { 45f, 45f }, { 30f, 135f }, { 14f, 200f }, { 8f, 300f }, { 5f, 90f }, { 85f, 10f }, { 89.5f, 60f },
        };

        [Theory]
        [MemberData(nameof(Suns))]
        public void TheTexelInWorldUnitsIsTheOneTheMatrixGives(float elevationDegrees, float azimuthDegrees)
        {
            float elevation = MathHelper.ToRadians(elevationDegrees), azimuth = MathHelper.ToRadians(azimuthDegrees);
            Vector3 sun = new(MathF.Cos(elevation) * MathF.Cos(azimuth), MathF.Sin(elevation), MathF.Cos(elevation) * MathF.Sin(azimuth));

            foreach (int size in new[] { 2048, 4096, 8192 })
            {
                SunShadowWindow window = SunShadowMap.FitWindow(size, new Vector3(37f, 0f, -112f), sun, 260f, -40f, 30f);
                Matrix viewProjection = window.ViewProjection;
                float texelWorld = window.TexelWorld;

                //What Shadows.fxh computed per pixel until #1101: ShadowTexel * 2 / length of the view-projection's x
                //column (the orthographic projection takes the window's width to two clip units)
                float column = new Vector3(viewProjection.M11, viewProjection.M21, viewProjection.M31).Length();
                float fromMatrix = (1f / size) * 2f / column;

                Assert.True(MathF.Abs(texelWorld - fromMatrix) <= 1e-5f * fromMatrix,
                    $"sun {elevationDegrees} deg up at {azimuthDegrees} deg, map {size}: the uniform says {texelWorld} world units a texel, the matrix {fromMatrix}");
            }
        }

        private static readonly string Header = FindFile(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Shadows.fxh"));

        private static string FindFile(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }

        //The header without its comments, so a word in a note is not taken for code
        private static string Code() => Regex.Replace(File.ReadAllText(Header), @"//[^\n]*", string.Empty);

        //SunShadow's body, split at its #if OPENGL / #else / #endif into the GL half and the DirectX half
        private static (string gl, string directX, string shared) SunShadowHalves()
        {
            string code = Code();
            int start = code.IndexOf("float SunShadow(", StringComparison.Ordinal);
            Assert.True(start >= 0, "Shadows.fxh has no SunShadow");

            int open = code.IndexOf('{', start), depth = 0, close = -1;
            for (int i = open; i < code.Length && close < 0; i++)
            {
                if (code[i] == '{') depth++;
                else if (code[i] == '}' && --depth == 0) close = i;
            }

            string body = code.Substring(open, close - open + 1);
            Match split = Regex.Match(body, @"#if OPENGL(?<gl>.*?)#else(?<dx>.*?)#endif", RegexOptions.Singleline);
            Assert.True(split.Success, "SunShadow has no #if OPENGL / #else / #endif around its reads of the map");

            string shared = body.Remove(split.Index, split.Length);
            return (split.Groups["gl"].Value, split.Groups["dx"].Value, shared);
        }

        [Fact]
        public void DirectXReadsTheBlockInFourGathers()
        {
            (string gl, string directX, string shared) = SunShadowHalves();

            Assert.Equal(4, Regex.Matches(directX, @"ShadowMap\.GatherRed\(").Count);
            Assert.DoesNotMatch(@"tex2Dlod|\.Sample", directX);

            //The shared part reads nothing: every read of the map is in one half or the other
            Assert.DoesNotMatch(@"tex2Dlod|GatherRed|\.Sample", shared);

            //Shader Model 3.0 has no gather, so the GL half keeps the point taps
            Assert.DoesNotMatch(@"Gather", gl);
            Assert.Contains("tex2Dlod(ShadowSampler", gl);
        }

        [Fact]
        public void EachGatherFillsItsOwnQuarterOfTheBlockInTheOrderAGatherReturns()
        {
            //Without whitespace, so a reflowed line is the same code
            string directX = Regex.Replace(SunShadowHalves().directX, @"\s+", string.Empty);

            //Read at the corner the top-left quarter's four texels share, the other three stepped two texels along
            //x, along y and along both - the block is texelBase - 1 .. texelBase + 2 on each axis
            const string corner = "ShadowMap.GatherRed(ShadowSampler,texelBase*ShadowTexel";
            Assert.Contains($"float4topLeft={corner});", directX);
            Assert.Contains($"float4topRight={corner},int2(2,0));", directX);
            Assert.Contains($"float4bottomLeft={corner},int2(0,2));", directX);
            Assert.Contains($"float4bottomRight={corner},int2(2,2));", directX);

            //A gather's components go anticlockwise from the bottom left: x (u0, v1), y (u1, v1), z (u1, v0), w (u0, v0).
            //So a quarter's upper row (the smaller v) is (w, z) and its lower row (x, y), and the block's four rows are
            //those of the two upper gathers and then of the two lower ones, left before right - the order the weights
            //are indexed in (weightX[x] by column, weightY[y] by row)
            Assert.Contains(
                "float4(topLeft.w,topLeft.z,topRight.w,topRight.z)," +
                "float4(topLeft.x,topLeft.y,topRight.x,topRight.y)," +
                "float4(bottomLeft.w,bottomLeft.z,bottomRight.w,bottomRight.z)," +
                "float4(bottomLeft.x,bottomLeft.y,bottomRight.x,bottomRight.y)", directX);
            Assert.Contains("(reference<=block[y][x]?1.0:0.0)*weightX[x]*weightY[y]", directX);
        }

        [Fact]
        public void TheTexelInWorldUnitsIsAUniformNotWorkedOutPerPixel()
        {
            string code = Code();
            Assert.Matches(@"float\s+ShadowTexelWorld\s*;", code);

            (string gl, string directX, string shared) = SunShadowHalves();
            Assert.Contains("ShadowTexelWorld", shared);
            Assert.DoesNotMatch(@"length\(", shared + gl + directX);
        }
    }
}
