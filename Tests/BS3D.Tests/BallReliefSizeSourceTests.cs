using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static BS3D.Tests.MeadowStrokesSourceTests;

namespace BS3D.Tests
{
    /// <summary>
    /// A ball's material looks the same close up and from afar (#1216). The owner saw some styles carry their surface
    /// detail only from afar and others only close up, and two causes were measured in the Testbed's sweep of the ten
    /// styles over seven distances: <c>PerturbNormalFromHeight</c>'s determinant floor is a world-unit area, which on a
    /// ball half a unit in radius scaled every style's relief by (pixel / 0.01)² once a target pixel was under 0.01 units
    /// (a seventh of itself on the menu's close pass, whole in play); and the band limits took the metal's brush and the
    /// ice's hairlines out at the play camera without leaving what they average to ("What fades where" in
    /// <c>docs/rendering/ball-styles.md</c>). No test can run a pixel shader without a graphics device and a tidy-up that
    /// put either back would look right in a still at one distance, so these read the shader files and hold the
    /// construction, as <see cref="MeadowStrokesSourceTests"/> holds the meadow's strokes.
    /// </summary>
    public class BallReliefSizeSourceTests
    {
        private static readonly string BallDirectory = Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "InstancedModel");

        //Every ball technique's file: the ten styles and the special kinds, each a Ball<Name>.fxh beside InstancedModel.fx
        private static string[] BallFiles()
        {
            string any = FindFile(Path.Combine(BallDirectory, "BallCommon.fxh"));
            string[] files = Directory.GetFiles(Path.GetDirectoryName(any)!, "Ball*.fxh").Select(Path.GetFileName).ToArray()!;
            Assert.True(files.Length >= 20, $"Only {files.Length} ball files were found beside BallCommon.fxh");

            return files;
        }

        [Fact]
        public void NoBallTiltsItsNormalUnderTheWorldFloor()
        {
            string[] offenders = BallFiles()
                .Where(file => Regex.IsMatch(Code(Path.Combine("InstancedModel", file)), @"\bPerturbNormalFromHeight\s*\("))
                .ToArray();

            Assert.True(offenders.Length == 0,
                "These ball files tilt their normal under the world-unit floor, which flattens their relief on every ball larger "
                + "than about 25 display pixels in radius: " + string.Join(", ", offenders)
                + ". Call PerturbNormalFromHeightAtAnySize (Noise.fxh).");
        }

        [Fact]
        public void EveryRelievedStyleTiltsAtAnySize()
        {
            //The styles whose figure lives in the normal; plasma has no relief and marble is the sphere itself
            foreach (string file in new[] { "BallVinyl.fxh", "BallWool.fxh", "BallMetal.fxh", "BallIce.fxh", "BallGem.fxh", "BallLava.fxh", "BallPorcelain.fxh" })
                Assert.True(Calls(Code(Path.Combine("InstancedModel", file)), "PerturbNormalFromHeightAtAnySize").Length > 0,
                    $"{file} draws no relief through PerturbNormalFromHeightAtAnySize");
        }

        [Fact]
        public void TheBallsFloorIsAShareOfThePixelAndTheScenesCapItAtTheWorldFloor()
        {
            string noise = Code("Noise.fxh");

            string pixelFloor = Body(noise, "float ReliefFloorOfPixel(");
            Assert.Contains("RELIEF_FLOOR_SHARE", pixelFloor);

            double share = Constant(noise, "RELIEF_FLOOR_SHARE");
            Assert.InRange(share, 0.05, 1.0);

            string atAnySize = Body(noise, "float3 PerturbNormalFromHeightAtAnySize(");
            Assert.Contains("ReliefFloorOfPixel(", atAnySize);
            Assert.Contains("TiltNormalByHeight(", atAnySize);
            Assert.DoesNotContain("SCENE_RELIEF_FLOOR", atAnySize);
            Assert.DoesNotContain("1e-4", atAnySize);

            //The scenes' relief stays whole close to the lens too (#1236): their floor is the pixel's share, capped at the
            //world floor they were tuned under, so a pixel larger than that (everything far) is floored as it always was
            string scenes = Body(noise, "float3 PerturbNormalFromHeight(");
            Assert.Contains("TiltNormalByHeight(", scenes);
            Assert.Matches(@"min\(\s*ReliefFloorOfPixel\([^)]*\)\s*,\s*SCENE_RELIEF_FLOOR\s*\)", scenes);
            Assert.Matches(@"static\s+const\s+float\s+SCENE_RELIEF_FLOOR\s*=\s*1e-4\s*;", noise);
        }

        [Fact]
        public void TheMetalsStreakTakesExactlyWhatItsGrainLoses()
        {
            string metal = Body(Code(Path.Combine("InstancedModel", "BallMetal.fxh")), "float4 MetalShade(");

            //One list of octaves and one fade for both halves, so a retuned octave moves the grain and the streak together
            Assert.Contains("MetalBrushWeights * brushKept", metal);
            Assert.Contains("1 - brushKept * brushKept", metal);
            Assert.DoesNotContain(Calls(metal, "ReliefOctave"), call => call.Contains("MetalBrushA"));

            Assert.Single(Calls(metal, "MetalSkyBrushed"));
            Assert.Empty(Calls(metal, "MetalSky"));
        }

        [Fact]
        public void AnIceCrackUnderAPixelKeepsItsCoverage()
        {
            string fracture = Body(Code(Path.Combine("InstancedModel", "BallIce.fxh")), "float3 IceFracture(");

            //Widened to the pixel and dimmed by the same ratio, never faded out on a limit of its own
            Assert.Matches(@"max\(\s*width\s*,\s*cells\s*\)", fracture);
            Assert.Matches(@"\(\s*width\s*/\s*drawn\s*\)", fracture);
            Assert.DoesNotContain("BandLimit -", fracture);
        }
    }
}
