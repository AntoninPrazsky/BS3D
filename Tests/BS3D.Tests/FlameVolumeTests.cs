using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The savanna fire as a volume (#886): on High and Ultra each fire's pixels are its bounding box's, marched through the
    /// fire's own 3D field. What can be held without a device: the box is wound as every procedural mesh is (a face that
    /// came out the wrong way round would be culled, and the fire would be drawn from the box's far side, or not at all);
    /// the box and the baked noise are the sizes <c>Flame.fx</c> reads them at; the noise repeats without a seam.
    /// </summary>
    public class FlameVolumeTests
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

        private static float Constant(string name)
        {
            Match match = Regex.Match(File.ReadAllText(Source), $@"static const float {name} = ([0-9.]+);");
            Assert.True(match.Success, $"Flame.fx has no {name}");
            return float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        //How many triangles of a list are NOT wound clockwise seen from outside the unit box (their (b - a) x (c - a) points
        //out of it, which MonoGame's default cull throws away from the outside)
        private static int OutwardWound(SceneRenderer.BillboardVertex[] vertices, short[] indices)
        {
            int wrong = 0;
            var centre = new Vector3(0f, 0.5f, 0f);
            for (int t = 0; t < indices.Length; t += 3)
            {
                Vector3 a = vertices[indices[t]].Position, b = vertices[indices[t + 1]].Position, c = vertices[indices[t + 2]].Position;
                Vector3 outward = (a + b + c) / 3f - centre;
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) >= 0f) wrong++;
            }
            return wrong;
        }

        [Fact]
        public void TheBoxIsWoundAsEveryProceduralMeshIs()
        {
            (SceneRenderer.BillboardVertex[] vertices, short[] indices) = SavannaBackdrop.FlameBox();
            Assert.Equal(36, indices.Length);
            Assert.Equal(0, OutwardWound(vertices, indices));

            //Seen to fire: the same box with every triangle turned over is all wrong
            var flipped = (short[])indices.Clone();
            for (int t = 0; t < flipped.Length; t += 3) (flipped[t + 1], flipped[t + 2]) = (flipped[t + 2], flipped[t + 1]);
            Assert.Equal(12, OutwardWound(vertices, flipped));
        }

        [Fact]
        public void TheBoxAndTheNoiseAreTheSizesTheShaderReads()
        {
            Assert.Equal(SavannaBackdrop.FLAME_VOLUME_RADIUS, Constant("FLAME_VOLUME_RADIUS"));
            Assert.Equal(SavannaBackdrop.FLAME_NOISE_PERIOD, Constant("FLAME_NOISE_PERIOD"));
        }

        /// <summary>
        /// The wrap sampler tiles the noise, so the step from the last texel of a row back round to the first must be one more
        /// step of the same field, not a seam: no larger than the field's own steps inside the volume.
        /// </summary>
        [Fact]
        public void TheNoiseRepeatsWithoutASeam()
        {
            float[] texels = SavannaBackdrop.FlameNoiseTexels();
            int side = SavannaBackdrop.FLAME_NOISE_PERIOD * SavannaBackdrop.FLAME_NOISE_TEXELS_PER_UNIT;
            Assert.Equal(side * side * side, texels.Length);

            float Step(int x0, int x1, int y, int z) => MathF.Abs(texels[x1 + side * (y + side * z)] - texels[x0 + side * (y + side * z)]);
            float inside = 0f, wrap = 0f, low = float.MaxValue, high = float.MinValue;
            for (int z = 0; z < side; z++)
                for (int y = 0; y < side; y++)
                {
                    for (int x = 0; x + 1 < side; x++) inside = MathF.Max(inside, Step(x, x + 1, y, z));
                    wrap = MathF.Max(wrap, Step(side - 1, 0, y, z));
                }
            foreach (float value in texels) { low = MathF.Min(low, value); high = MathF.Max(high, value); }

            Assert.True(wrap <= inside * 1.01f, $"the step round the wrap is {wrap:0.000}, the largest inside {inside:0.000}");
            Assert.True(low > -1f && high < 1f && high - low > 0.6f, $"the noise runs {low:0.00} to {high:0.00}");
        }
    }
}
