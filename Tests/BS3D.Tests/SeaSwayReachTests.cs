using Prazsky.Core.Render;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The sea's grid is culled to the lens in blocks, each box grown sideways by how far <c>Sea.fx</c>'s waves move a vertex
    /// (#1035's review): <see cref="SeaBackdrop.SwayReach"/> carries the shader's <c>WAVE_STEEP_WEIGHT</c> and the sum of its
    /// <c>WAVE_AMP</c> table by hand. A reach short of the shader's skips a block at the frame's edge while a crest swings into
    /// view, so these read the shader's text and hold the two copies together; the check is run on a changed table too.
    /// </summary>
    public class SeaSwayReachTests
    {
        private static string FindUnderRepository(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }

        private static string SeaShader() =>
            File.ReadAllText(FindUnderRepository(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Sea.fx")));

        /// <summary>The farthest the shader's text moves a vertex sideways at that steepness and amplitude.</summary>
        private static float ShaderReach(string shader, float steepness, float amplitude)
        {
            Match weight = Regex.Match(shader, @"static const float WAVE_STEEP_WEIGHT = ([0-9.]+);");
            Match table = Regex.Match(shader, @"static const float WAVE_AMP\[WAVE_COUNT\] = \{([^}]*)\};");
            Assert.True(weight.Success && table.Success, "Sea.fx no longer states WAVE_STEEP_WEIGHT and WAVE_AMP as SeaSwayReachTests reads them");

            float sum = table.Groups[1].Value.Split(',').Sum(a => float.Parse(a.Trim(), CultureInfo.InvariantCulture));
            return steepness * float.Parse(weight.Groups[1].Value, CultureInfo.InvariantCulture) * amplitude * sum;
        }

        [Theory]
        [InlineData(0.95f, 0.6f)]  //the sea
        [InlineData(0.85f, 0.35f)] //the tropical lagoon
        [InlineData(1f, 1f)]
        public void TheReachIsTheShadersOwn(float steepness, float amplitude)
        {
            Assert.Equal(ShaderReach(SeaShader(), steepness, amplitude), SeaBackdrop.SwayReach(steepness, amplitude), 3);
        }

        [Fact]
        public void TheCheckFiresOnAChangedTableAndWeight()
        {
            //Seen to fire (BestPractices.md, section 10): the likeliest drifts, a swell made taller and a heavier pinch
            string shader = SeaShader();
            string taller = shader.Replace("1.0000, 0.3573,", "1.2000, 0.3573,", StringComparison.Ordinal);
            string heavier = shader.Replace("WAVE_STEEP_WEIGHT = 0.70;", "WAVE_STEEP_WEIGHT = 0.80;", StringComparison.Ordinal);
            Assert.NotEqual(shader, taller);
            Assert.NotEqual(shader, heavier);

            float reach = SeaBackdrop.SwayReach(1f, 1f);
            Assert.NotEqual(Math.Round(ShaderReach(taller, 1f, 1f), 3), Math.Round(reach, 3));
            Assert.NotEqual(Math.Round(ShaderReach(heavier, 1f, 1f), 3), Math.Round(reach, 3));
        }
    }
}
