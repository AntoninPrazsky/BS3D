using Prazsky.Core.Render;
using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The outback's air (#1123). The owner saw "a strange low pale fog creeping along the ground", and two things in
    /// <c>Outback.fx</c> made it: a "heat shimmer", gradient noise of the view ray added to the haze and sliding upwards with
    /// the clock, which laid pale blotches over the whole middle plain that a moving lens saw creep; and an extinction short
    /// enough that the air, far brighter than the red ground, veiled the plain a few hundred units out, with the far field's
    /// last fade laying a pale band along the skyline from 600 units. No test can run the pixel shader, so these hold the
    /// three places the cure lives: the effect has no clock and no noise in its haze, the config's air is long, and the
    /// outback's pass may begin its last fade later than the far field lets any other ground.
    /// </summary>
    public class OutbackAirTests
    {
        private static readonly string Source = FindFile(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Outback.fx"));

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

        private static string Code() => CodeOf(Source);

        //Prazsky.Core's folder, for the C# side of the lift
        private static string CoreDirectory() =>
            Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(
                FindFile(Path.Combine("BS3DLibs", "Prazsky.Core", "Render", "Scenes", "TerrainPass.cs")))));

        /// <summary>
        /// Nothing in the outback's ground moves with the clock: a uniform named for time is what the shimmer slid on, and
        /// the cloud shadows that do move take their own clock from the cloud field (<c>CloudSunlight</c>), not a scene's.
        /// </summary>
        [Fact]
        public void TheGroundHasNoClock()
        {
            string code = Code();

            Assert.DoesNotMatch(new Regex(@"^\s*float\s+\w*Time\s*;", RegexOptions.Multiline), code);
            Assert.DoesNotContain("HeatShimmer", code);
        }

        /// <summary>
        /// The haze is an extinction by distance and nothing else: between the haze's own figure and its lerp to the air
        /// no noise takes part, so the air cannot lie on the plain in patches that stay with the lens's directions.
        /// </summary>
        [Fact]
        public void TheHazeIsDistanceAlone()
        {
            string code = Code();

            int haze = code.IndexOf("float haze = 1.0 - exp(-dist / HazeDistance);", StringComparison.Ordinal);
            Assert.True(haze >= 0, "Outback.fx no longer states its haze as an extinction by distance");

            int fade = code.IndexOf("FarFadeToSky(", haze, StringComparison.Ordinal);
            Assert.True(fade > haze, "Outback.fx's last fade no longer follows its haze");

            string air = code.Substring(haze, fade - haze);
            Assert.DoesNotContain("Noise", air);
            Assert.Matches(@"lerp\(\s*color\s*,\s*air\s*,\s*haze\s*\)", air);
        }

        /// <summary>
        /// The plain keeps its red: 300 units out, the middle of the intro's wide shot, at most a twentieth of its light is air
        /// (at the old 3 000 it was a tenth, and read as a pale fog lying on it), at most a sixth where its last fade begins,
        /// and the fade begins no nearer than 1 000 units (it began at 600) and short of where it must be complete.
        /// </summary>
        [Fact]
        public void ThePlainKeepsItsRedToTheLastFade()
        {
            OutbackAirConfig air = new OutbackSceneConfig().Air;

            float Haze(float distance) => 1f - MathF.Exp(-distance / air.HazeDistance);

            Assert.True(Haze(300f) <= 0.05f, $"the plain 300 units out is {Haze(300f):F3} air");

            //Where FarField.Begin starts the plain's fade: the effect's HorizonHazeDistance read back (the config's figure on
            //DirectX; null on OpenGL ES, whose build drops the uniform Outback.fx never reads) under the cap the outback's pass
            //states, which its backdrop sets to the same figure (TheOutbackPassAloneLiftsTheCap)
            float latest = air.HorizonHazeDistance;
            foreach (float? effectHaze in new float?[] { air.HorizonHazeDistance, null })
            {
                float start = FarField.FadeStart(effectHaze, latest);
                string api = effectHaze == null ? "OpenGL ES" : "DirectX";
                Assert.True(start >= 1000f, $"on {api} the plain's last fade begins {start} units out");
                Assert.True(start < FarField.FAR_FADE_END, $"on {api} the plain's last fade begins at {start}, not short of its end at {FarField.FAR_FADE_END}");
                Assert.True(Haze(start) <= 1f / 6f, $"on {api} the plain is {Haze(start):F3} air where its last fade begins");
            }

            //Under the far field's own cap the plain would begin at 600 again: the lift is what moves it
            Assert.Equal(FarField.FAR_FADE_START_LATEST, FarField.FadeStart(air.HorizonHazeDistance, FarField.FAR_FADE_START_LATEST));
        }

        /// <summary>
        /// The lift reaches the far field, and only from the outback: a <c>TerrainPass</c>'s cap is the far field's own unless
        /// a backdrop states another, the pass hands it to <c>FarField.Begin</c>, the outback's backdrop sets it to its config's
        /// figure, and nothing else in Prazsky.Core sets it. No test can build a pass (it needs a graphics device), so these are
        /// read off the source with its comments taken out.
        /// </summary>
        [Fact]
        public void TheOutbackPassAloneLiftsTheCap()
        {
            string core = CoreDirectory();
            string scenes = Path.Combine(core, "Render", "Scenes");

            string pass = CodeOf(Path.Combine(scenes, "TerrainPass.cs"));
            Assert.Matches(@"public\s+float\s+FarFadeStartLatest\s*\{\s*get\s*;\s*set\s*;\s*\}\s*=\s*FarField\.FAR_FADE_START_LATEST\s*;", pass);
            Assert.Matches(@"_farField\.Begin\(\s*Effect\s*,\s*frame\s*,\s*_extent\s*,\s*FarFadeStartLatest\s*\)", pass);

            Assert.Matches(@"_outbackPass\.FarFadeStartLatest\s*=\s*air\.HorizonHazeDistance\s*;", CodeOf(Path.Combine(scenes, "OutbackBackdrop.cs")));

            foreach (string file in Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(core, file);
                if (relative.StartsWith("bin", StringComparison.Ordinal) || relative.StartsWith("obj", StringComparison.Ordinal)) continue;

                bool sets = Regex.IsMatch(CodeOf(file), @"\.FarFadeStartLatest\s*=[^=]");
                bool outback = Path.GetFileName(file) == "OutbackBackdrop.cs";
                Assert.True(sets == outback, $"{relative} {(sets ? "sets" : "does not set")} a pass's FarFadeStartLatest");
            }
        }

        /// <summary>
        /// Every other ground still begins its fade by the far field's 600, whatever haze distance it states: the icesheet's
        /// teal line (a fade begun at 900, where its haze had made the ground wholly <c>HorizonColor</c>) stays fixed.
        /// </summary>
        [Fact]
        public void OtherGroundsStillFadeBy600()
        {
            Assert.Equal(600f, FarField.FAR_FADE_START_LATEST);
            Assert.Equal(600f, FarField.FadeStart(900f, FarField.FAR_FADE_START_LATEST));
            Assert.Equal(480f, FarField.FadeStart(480f, FarField.FAR_FADE_START_LATEST));
            Assert.Equal(FarField.FAR_FADE_START_LATEST, FarField.FadeStart(null, FarField.FAR_FADE_START_LATEST));
        }
    }
}
