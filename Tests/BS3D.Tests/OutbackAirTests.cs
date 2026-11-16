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

        //The text of the effect without its comments, so a word in a note is not taken for code
        private static string Code() => Regex.Replace(File.ReadAllText(Source), @"//[^\n]*", string.Empty);

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

            //The pass lifts the far field's cap to the config's figure; read off the backdrop, which no test can build
            string backdrop = Regex.Replace(File.ReadAllText(FindFile(Path.Combine("BS3DLibs", "Prazsky.Core", "Render", "Scenes", "OutbackBackdrop.cs"))),
                @"//[^\n]*", string.Empty);
            Assert.Matches(@"_outbackPass\.FarFadeStartLatest\s*=\s*air\.HorizonHazeDistance\s*;", backdrop);

            float start = FarField.FadeStart(air.HorizonHazeDistance, air.HorizonHazeDistance);
            Assert.True(start >= 1000f, $"the plain's last fade begins {start} units out");
            Assert.True(start < FarField.FAR_FADE_END, $"the plain's last fade begins at {start}, not short of its end at {FarField.FAR_FADE_END}");
            Assert.True(Haze(start) <= 1f / 6f, $"the plain is {Haze(start):F3} air where its last fade begins");
        }

        /// <summary>
        /// Every other ground still begins its fade by the far field's 600, whatever haze distance it states: the lift is the
        /// outback pass's own, and the icesheet's teal line (a fade begun at 900 under a haze that ends in another colour) stays
        /// fixed.
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
