using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The volcano's far ranges at night (#1164). The owner: the mountains on the horizon had "a very light tinge, lighter the
    /// farther they are", in a scene that takes place at night. Three things in the land's air did it: the haze carried the
    /// land to <c>LandHazeColor</c>, more than half the drawn horizon's own colour; the far field's last fade into the sky began
    /// at 600, so a range 1 000 out was three fifths sky whatever its air was; and that fade ended in the dome without its
    /// cloud deck, brighter than the sky drawn over the ranges' crests. No test can run the pixel shader, so these hold the
    /// shader's arithmetic as a model (and the source to that arithmetic) and the figures it is run on: the near ranges stand
    /// under 0.6 of the drawn horizon, each farther one a shade paler, and the last fade still has the land wholly sky by
    /// <see cref="FarField.FAR_FADE_END"/>, so its geometry never shows an end (#551).
    /// ⚠ The model's sky is the drawn horizon (<c>FarSky</c> level with the skyline) without the deck, which darkens the sky
    /// round a crest standing over the skyline: it is optimistic there, and it guards the constants, not the ratio on screen
    /// (measured in <c>docs/scenes/volcano.md</c>: the near ranges up to 0.65). And the bar holds only until the last fade is
    /// about half done: from the arena much of the skyline stands 1 100-1 480 out (all of it right of the cone in the play view, the
    /// nearest ridge there about 1 150), where that fade has the land half to wholly sky whatever its air. How it looks is the
    /// owner's eye.
    /// </summary>
    public class VolcanoFarHazeTests
    {
        private static readonly string Source = FindFile(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Volcano.fx"));

        private static string FindFile(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }

        //The text of a source file without its comments, so a word in a note is not taken for code
        private static string CodeOf(string path) => Regex.Replace(File.ReadAllText(path), @"//[^\n]*", string.Empty);

        //The near ranges: the ranges stand 250-650 past the field's edge (VolcanoCountry's RIDGES_FROM and RIDGES_TO), and the
        //edge stands 270-300 out on the arena's far side and 330-550 on its flanks, so from a lens at the arena the near ranges
        //stand 520-1 000 off. The far ones, 1 100-1 480 off and much of the skyline from the arena (TerrainMirror.Volcano marched
        //out from the play, dormant-cone and left-flank lenses), the last fade has half to wholly sky: no air holds them under
        //0.6, since that fade must be complete by FAR_FADE_END (#551)
        private const float NEAR_RANGES_TO = 1050f;

        //A range's own colour as a share of the sky behind it: dark heath and scree (albedo about 0.05) lit by the dusk dome and
        //a sun 9 degrees up; a tenth is a lit face's, generously
        private const float RANGE_OWN = 0.1f;

        //What the land's pixels come to in Volcano.fx (VolcanoSurface), as a share of the drawn horizon (FarSky at direction.y
        //0, which is 1 here; the deck VolcanoFadeToSky closes over it is left out): the range's own colour hazed towards the
        //land's air (landAir, a share of the drawn horizon) by HazeStrength * saturate(d / HorizonHazeDistance)^2, then the last
        //fade's smoothstep from where FarField.Begin starts it to FAR_FADE_END. The source this mirrors is held by
        //TheModelIsTheShadersArithmetic.
        private static float RangeOverSky(float distance, VolcanoSceneConfig volcano, float landAir, float fadeStart)
        {
            float haze = Math.Clamp(distance / volcano.HorizonHazeDistance, 0f, 1f);
            float land = MathHelper.Lerp(RANGE_OWN, landAir, haze * haze * volcano.HazeStrength);
            return MathHelper.Lerp(land, 1f, ShaderMath.SmoothStep(fadeStart, FarField.FAR_FADE_END, distance));
        }

        //Where FarField.Begin starts the volcano's last fade: the effect's HorizonHazeDistance read back under the cap the
        //backdrop hands it
        private static float FadeStart(VolcanoSceneConfig volcano) =>
            FarField.FadeStart(volcano.HorizonHazeDistance, VolcanoBackdrop.FarFadeStartLatest(volcano));

        [Fact]
        public void TheNearRangesStandUnderTheDrawnHorizon()
        {
            var volcano = new VolcanoSceneConfig();
            float start = FadeStart(volcano);

            for (float d = 300f; d <= NEAR_RANGES_TO; d += 25f)
            {
                float share = RangeOverSky(d, volcano, VolcanoBackdrop.LAND_HAZE_SKY, start);
                Assert.True(share < 0.6f, $"a range {d} out is {share:F2} of the drawn horizon");
            }
        }

        [Fact]
        public void EachRangeIsAShadePalerThanTheOneBefore()
        {
            //Aerial perspective is what makes ranges read as ranges, one behind another: darkened, they keep it
            var volcano = new VolcanoSceneConfig();
            float start = FadeStart(volcano);

            float before = RangeOverSky(500f, volcano, VolcanoBackdrop.LAND_HAZE_SKY, start);
            for (float d = 600f; d <= FarField.FAR_FADE_END; d += 100f)
            {
                float share = RangeOverSky(d, volcano, VolcanoBackdrop.LAND_HAZE_SKY, start);
                Assert.True(share > before, $"a range {d} out ({share:F3} of the sky) is no paler than one 100 nearer ({before:F3})");
                before = share;
            }
        }

        [Fact]
        public void TheLandStillEndsInTheSky()
        {
            //The fade still runs, from where the scene's own haze has finished, and it is complete by the far field's end, where
            //the land IS the sky: whatever the ring or the far plane does past it cannot show (#551). On DirectX the effect's
            //haze distance is read back; on OpenGL ES too, since Volcano.fx reads it (a null stands for a build that dropped it).
            var volcano = new VolcanoSceneConfig();
            foreach (float? effectHaze in new float?[] { volcano.HorizonHazeDistance, null })
            {
                float start = FarField.FadeStart(effectHaze, VolcanoBackdrop.FarFadeStartLatest(volcano));
                Assert.True(start <= volcano.HorizonHazeDistance, $"the last fade begins at {start}, past where the scene's haze ends");
                Assert.True(start < FarField.FAR_FADE_END - 300f, $"the last fade begins at {start}, too near its end at {FarField.FAR_FADE_END} to be a fade");
                Assert.Equal(1f, RangeOverSky(FarField.FAR_FADE_END, volcano, VolcanoBackdrop.LAND_HAZE_SKY, start), 3);
            }
        }

        [Fact]
        public void TheLandsAirIsTheDrawnHorizonDarkened()
        {
            //The dome as drawn level with the horizon, every other slot of the table another colour, and the rig's horizon
            //another again: the land's air is made of the one slot, and darker than half of it
            var farSky = new Vector3[SkyLightRig.FAR_SKY_STEPS];
            for (int i = 0; i < farSky.Length; i++) farSky[i] = new Vector3(0.9f - 0.05f * i, 0.1f + 0.05f * i, 0.3f);
            Vector3 drawnHorizon = new(0.07f, 0.075f, 0.14f);
            farSky[SkyLightRig.FAR_SKY_HORIZON] = drawnHorizon;
            var frame = new SceneFrame(null, Vector3.UnitY, new Vector3(0.02f, 0.02f, 0.05f), new Vector3(0.5f, 0.3f, 0.1f), Vector3.One, 0f, null, farSky);

            Vector3 air = VolcanoBackdrop.CountryHaze(frame);
            Assert.Equal(drawnHorizon * VolcanoBackdrop.LAND_HAZE_SKY, air);
            Assert.True(VolcanoBackdrop.LAND_HAZE_SKY < 0.5f, $"the land's air is {VolcanoBackdrop.LAND_HAZE_SKY} of the drawn horizon");
        }

        /// <summary>
        /// The model above is the shader's: the land is hazed towards <c>LandHazeColor</c> by the square of the distance over
        /// <c>HorizonHazeDistance</c> times <c>HazeStrength</c>, and the far fade follows; and the backdrop hands the far field
        /// its lift. Read off the source with its comments taken out.
        /// </summary>
        [Fact]
        public void TheModelIsTheShadersArithmetic()
        {
            string code = CodeOf(Source);
            Assert.Matches(@"hazeColor\s*=\s*lerp\(\s*hazeColor\s*,\s*LandHazeColor\s*,", code);
            Assert.Matches(@"float\s+haze\s*=\s*saturate\(\s*length\(\s*CameraPosition\s*-\s*worldPosition\s*\)\s*/\s*HorizonHazeDistance\s*\)\s*;", code);

            //The haze, and straight after it the last fade on its result
            Assert.Matches(@"color\s*=\s*lerp\(\s*color\s*,\s*hazeColor\s*,\s*haze\s*\*\s*haze\s*\*\s*HazeStrength\s*\)\s*;"
                + @"\s*return\s+float4\(\s*VolcanoFadeToSky\(\s*color\s*,\s*worldPosition\s*\)\s*,\s*1\.0\s*\)\s*;", code);

            //Which is FarFadeToSky's fade, over the far field's distances and on its smoothstep
            string fade = Body(code, "float3 VolcanoFadeToSky(");
            Assert.Matches(@"if\s*\(\s*FarFade\.y\s*>\s*FarFade\.x\s*\)", fade);
            Assert.Matches(@"smoothstep\(\s*FarFade\.x\s*,\s*FarFade\.y\s*,\s*dist\s*\)", fade);

            string backdrop = CodeOf(FindFile(Path.Combine("BS3DLibs", "Prazsky.Core", "Render", "Scenes", "VolcanoBackdrop.cs")));
            Assert.Matches(@"Services\.FarField\.Begin\(\s*_volcanoEffect\s*,\s*frame\s*,\s*VOLCANO_EXTENT\s*,\s*FarFadeStartLatest\(\s*_volcanoConfig\s*\)\s*\)", backdrop);
        }

        /// <summary>
        /// The last fade ends in the sky as it is drawn behind the point: the dome (<c>FarSkyAt</c> along the point's own
        /// direction) with the deck closed over it as <c>Sky.fx</c> closes it, from nothing at the skyline
        /// (<c>smoothstep(0, CloudHorizonFade, y)</c>, which is 0 for every direction at or under it, so the land's end below the
        /// skyline still ends in the dome as every ground's does) to its full cover by <c>CloudField.HORIZON_FADE</c>, which the
        /// backdrop states. Faded to the dome alone, the ranges a few degrees over the skyline stood paler than the deck round them.
        /// </summary>
        [Fact]
        public void TheLastFadeIsToTheSkyAsDrawn()
        {
            string code = CodeOf(Source);
            string fade = Body(code, "float3 VolcanoFadeToSky(");

            Assert.Matches(@"float\s+directionY\s*=\s*toPixel\.y\s*/\s*max\(\s*dist\s*,", fade);
            Assert.Matches(@"smoothstep\(\s*0\.0\s*,\s*CloudHorizonFade\s*,\s*directionY\s*\)", fade);
            Assert.Matches(@"saturate\(\s*saturate\(\s*CloudCoverageBias\s*\*\s*CloudCoverageGain\s*\)\s*\*\s*CloudOpacity\s*\)", fade);
            Assert.Matches(@"lerp\(\s*FarSkyAt\(\s*directionY\s*\)\s*,\s*CloudShadowColor\s*,\s*deck\s*\)", fade);

            //The deck's closing figure is the sky shader's own, stated where the volcano's other figures are
            string backdrop = CodeOf(FindFile(Path.Combine("BS3DLibs", "Prazsky.Core", "Render", "Scenes", "VolcanoBackdrop.cs")));
            Assert.Matches(@"_volcanoEffect\.Parameters\[""CloudHorizonFade""\]\.SetValue\(\s*CloudField\.HORIZON_FADE\s*\)", backdrop);
            Assert.Matches(@"smoothstep\(\s*0\.0\s*,\s*CloudHorizonFade\s*,\s*direction\.y\s*\)",
                CodeOf(FindFile(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Sky.fx"))));
        }

        /// <summary>
        /// The far field's cap is lifted in two places and no others: a <c>TerrainPass</c> hands <c>FarField.Begin</c> its own
        /// <c>FarFadeStartLatest</c> (which only the outback sets, <c>OutbackAirTests</c>), and the volcano hands it its haze
        /// distance. Every other call leaves the latest start at the far field's 600, which keeps the icesheet's teal line fixed.
        /// Read off Prazsky.Core's source with its comments taken out: a call with a fourth argument anywhere else fails.
        /// </summary>
        [Fact]
        public void OnlyTheOutbackAndTheVolcanoLiftTheCap()
        {
            string core = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(
                FindFile(Path.Combine("BS3DLibs", "Prazsky.Core", "Render", "Scenes", "TerrainPass.cs")))));

            int lifts = 0;
            foreach (string file in Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(core, file);
                if (relative.StartsWith("bin", StringComparison.Ordinal) || relative.StartsWith("obj", StringComparison.Ordinal)) continue;

                string code = CodeOf(file);
                foreach (Match call in Regex.Matches(code, @"(?:FarField|_farField)\.Begin\("))
                {
                    //The call's arguments: commas at its own depth, up to its closing parenthesis
                    int depth = 0, commas = 0;
                    for (int i = call.Index + call.Length - 1; i < code.Length; i++)
                    {
                        if (code[i] == '(') depth++;
                        else if (code[i] == ')' && --depth == 0) break;
                        else if (code[i] == ',' && depth == 1) commas++;
                    }

                    if (commas < 3) continue;
                    string name = Path.GetFileName(file);
                    Assert.True(name is "TerrainPass.cs" or "VolcanoBackdrop.cs", $"{relative} hands FarField.Begin a latest start of its own");
                    lifts++;
                }
            }

            Assert.Equal(2, lifts);
        }

        //The body of the function whose signature begins at the marker: from its opening brace to the matching closing one
        private static string Body(string code, string signature)
        {
            int at = code.IndexOf(signature, StringComparison.Ordinal);
            Assert.True(at >= 0, $"Volcano.fx has no {signature}");
            int open = code.IndexOf('{', at), depth = 0;
            for (int i = open; i < code.Length; i++)
            {
                if (code[i] == '{') depth++;
                else if (code[i] == '}' && --depth == 0) return code.Substring(open, i - open + 1);
            }

            throw new InvalidDataException($"{signature} never closes.");
        }
    }
}
