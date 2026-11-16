using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The forest's twilight grade (#1121). The far spires went azure against a violet sky because the haze carried them to the
    /// light rig's HorizonColor, the average of the bottom fifth of the dome's capture and under the forest's dome 15 a teal
    /// that no part of the drawn sky has; and the broadleaf crowns read lighter than anything else in the wood. What can be
    /// held without a frame: the haze's last stage is the sky drawn behind the point (Forest.fx), the murk before it is worked
    /// out of that drawn sky and never of the rig's horizon, and the broadleaves are authored the same step lighter than the
    /// conifers near and on the far hills. How it looks is the owner's eye.
    /// </summary>
    public class ForestGradeTests
    {
        //Dome 15 as SkyDome draws it at direction.y 0 (FarSky's slot 2), and the rig's horizon for that dome, in linear
        //radiance: the violet the ridges stand against and the teal that is drawn nowhere above the skyline
        private static readonly Vector3 DrawnHorizon = new(0.217f, 0.092f, 0.438f);
        private static readonly Vector3 RigHorizon = new(0.055f, 0.282f, 0.315f);

        private static SceneFrame Frame(Vector3 drawnHorizon, Vector3 rigHorizon)
        {
            var farSky = new Vector3[SkyLightRig.FAR_SKY_STEPS];
            Array.Fill(farSky, drawnHorizon);
            return new SceneFrame(null, Vector3.UnitY, new Vector3(0.082f, 0.012f, 0.229f), rigHorizon, Vector3.One, 0f, null, farSky);
        }

        [Fact]
        public void TheMurkIsMadeOfTheSkyDrawnAndNotOfTheRigsHorizon()
        {
            var config = new ForestSceneConfig();

            //The rig's horizon is no part of it: whatever it says, the murk is the same
            Vector3 murk = ForestBackdrop.Murk(Frame(DrawnHorizon, RigHorizon), config);
            Assert.Equal(murk, ForestBackdrop.Murk(Frame(DrawnHorizon, new Vector3(0.9f, 0.1f, 0.1f)), config));

            //And the drawn sky is: a different sky is a different murk
            Assert.NotEqual(murk, ForestBackdrop.Murk(Frame(new Vector3(0.40f, 0.30f, 0.20f), RigHorizon), config));
        }

        [Fact]
        public void TheMurkIsADarkerShadeOfTheVioletSkyNotTheTeal()
        {
            Vector3 murk = ForestBackdrop.Murk(Frame(DrawnHorizon, RigHorizon), new ForestSceneConfig());

            //Darker than the sky it is made of, so a ridge reads as a ridge until the sky takes it
            Assert.True(ColorSpace.Luminance(murk) < 0.5f * ColorSpace.Luminance(DrawnHorizon),
                $"the murk {murk} is not much darker than the sky {DrawnHorizon}");

            //Blue over green, as the violet is: the teal's green over its blue is what the far spires wore
            Assert.True(murk.Z > 2f * murk.Y, $"the murk {murk} is not the violet sky's blue");
            Assert.True(murk.X > 0.5f * murk.Y, $"the murk {murk} has lost the violet's red and gone teal");
        }

        [Fact]
        public void ABroadleafIsTheSameStepLighterThanAConiferNearAndFar()
        {
            var config = new ForestSceneConfig();
            float near = ColorSpace.Luminance(config.Trees.FoliageColor.ToVector3()) / ColorSpace.Luminance(config.Trees.ConiferColor.ToVector3());
            float far = ColorSpace.Luminance(config.FarStand.BroadleafColor.ToVector3()) / ColorSpace.Luminance(config.FarStand.SpruceColor.ToVector3());

            Assert.InRange(near, ForestTreeConfig.BROADLEAF_LIGHTER - 0.03f, ForestTreeConfig.BROADLEAF_LIGHTER + 0.03f);
            Assert.InRange(far, ForestTreeConfig.BROADLEAF_LIGHTER - 0.03f, ForestTreeConfig.BROADLEAF_LIGHTER + 0.03f);
        }

        [Fact]
        public void ABroadleafIsOliveNotLeafGreen()
        {
            //Every dusk reference draws a beech's or a birch's crown yellower than a spruce's, and a pure leaf green (red a
            //third of the green) read as lit by a sun the rest of the wood did not have
            var config = new ForestSceneConfig();
            foreach (Vector3 leaf in new[] { config.Trees.FoliageColor.ToVector3(), config.FarStand.BroadleafColor.ToVector3() })
                Assert.True(leaf.X > 0.45f * leaf.Y, $"the broadleaf colour {leaf} is a leaf green, not an olive");
        }

        [Fact]
        public void TheHazeEndsAtTheSkyDrawnBehindThePoint()
        {
            string haze = Body(Code(), "float3 ForestHaze(");

            //Its last stage is the dome as drawn along the point's own direction
            Assert.Contains("FarSkyAt(", haze);

            //The rig's horizon only as the stand-in for a host that hands no drawn dome over, never as what the haze ends at
            Assert.DoesNotMatch(@"lerp\(\s*color\s*,\s*HorizonColor", haze);
            Assert.Matches(@"FarFade\.y\s*>\s*FarFade\.x\s*\?\s*FarSkyAt\([^;]*:\s*HorizonColor", haze);

            //And the murk is the one worked out of the drawn sky (ForestBackdrop.Murk), not one of its own
            Assert.Contains("ForestMurk", haze);
            Assert.DoesNotMatch(@"ZenithColor|HorizonColor\s*\*", haze);
        }

        private static readonly string Source = FindFile(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Forest.fx"));

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
            Assert.True(start >= 0, $"Forest.fx has no {signature}");

            int open = code.IndexOf('{', start), depth = 0;
            for (int i = open; i < code.Length; i++)
            {
                if (code[i] == '{') depth++;
                else if (code[i] == '}' && --depth == 0) return code.Substring(open, i - open + 1);
            }

            throw new InvalidDataException($"The body of {signature} never closes.");
        }
    }
}
