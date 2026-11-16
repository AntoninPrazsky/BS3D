using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.IO;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The probe that makes the terrain mirrors round the noise hash the way the GPU does (#1134). The GPU half is the
    /// Testbed's <c>mirrorcheck</c>; what is pinned here, with no device, is the verdict: a GPU that rounds one way is
    /// told apart from one that rounds the other, and anything that is not clearly either changes nothing.
    /// </summary>
    public class HashRoundingProbeTests
    {
        /// <summary>The probe's pixels as a GPU that rounds <paramref name="rounding"/> would hand them back.</summary>
        private static Vector4[] Samples(HashRounding rounding)
        {
            var samples = new Vector4[HashRoundingProbe.SIZE * HashRoundingProbe.SIZE];
            for (int i = 0; i < samples.Length; i++)
            {
                Vector2 p = HashRoundingProbe.InputAt(i % HashRoundingProbe.SIZE, i / HashRoundingProbe.SIZE);
                Vector2 h = ShaderMath.Hash22(p.X, p.Y, rounding);
                samples[i] = new Vector4(h, p.X, p.Y);
            }

            return samples;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void EachRoundingIsToldApart(bool fused)
        {
            //A bool, not the internal enum: a public test method cannot take an internal type
            HashRounding rounding = fused ? HashRounding.Fused : HashRounding.Unfused;
            HashRoundingProbe.Verdict verdict = HashRoundingProbe.Decide(Samples(rounding));

            Assert.Equal(rounding, verdict.Rounding);
            Assert.Equal(HashRoundingProbe.SIZE * HashRoundingProbe.SIZE, verdict.Inputs);
        }

        [Fact]
        public void TheTwoRoundingsDisagreeOnManyOfTheProbesInputs()
        {
            //Otherwise the probe could not tell them apart: a handful of disagreeing samples would decide on a few quanta.
            //992 of the 4096 inputs tell them apart (the laptop's GPU read 3104 fused, 4096 unfused, the same split)
            Vector4[] fused = Samples(HashRounding.Fused);
            HashRoundingProbe.Verdict verdict = HashRoundingProbe.Decide(fused);

            Assert.True(fused.Length - verdict.Unfused >= 256, $"only {fused.Length - verdict.Unfused} of {fused.Length} inputs tell the roundings apart");
        }

        [Fact]
        public void AGpuThatRoundsSomeThirdWayDecidesNothing()
        {
            //Fused on the first half, unfused on the second: neither agrees everywhere
            Vector4[] fused = Samples(HashRounding.Fused);
            Vector4[] unfused = Samples(HashRounding.Unfused);
            var mixed = new Vector4[fused.Length];
            for (int i = 0; i < mixed.Length; i++) mixed[i] = i < mixed.Length / 2 ? fused[i] : unfused[i];

            Assert.Null(HashRoundingProbe.Decide(mixed).Rounding);
        }

        [Fact]
        public void AReadbackThatLostItsInputsDecidesNothing()
        {
            //A target read back flipped (or not at all) hands the right hashes for the wrong pixels: no evidence
            Vector4[] unfused = Samples(HashRounding.Unfused);
            var flipped = new Vector4[unfused.Length];
            int size = HashRoundingProbe.SIZE;
            for (int row = 0; row < size; row++)
                Array.Copy(unfused, row * size, flipped, (size - 1 - row) * size, size);

            Assert.Null(HashRoundingProbe.Decide(flipped).Rounding);
            Assert.Null(HashRoundingProbe.Decide(new Vector4[unfused.Length]).Rounding);
        }

        [Fact]
        public void TheShaderHashesTheSameSquareOfInputs()
        {
            //HashProbe.fx's side and its input, as InputAt states them
            string shader = File.ReadAllText(Path.Combine(RepoRoot(), "BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "HashProbe.fx"));

            Assert.Contains($"static const float HASH_PROBE_SIZE = {HashRoundingProbe.SIZE}.0;", shader);
            Assert.Contains("float2 p = cell * float2(19.0, 23.0) + float2(23.7, 57.1);", shader);
            Assert.Equal(new Vector2(-32 * 19f + 23.7f, -32 * 23f + 57.1f), HashRoundingProbe.InputAt(0, 0));
        }

        private static string RepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "Testbed.sln"))) dir = Path.GetDirectoryName(dir);
            return dir ?? throw new DirectoryNotFoundException("no Testbed.sln above " + AppContext.BaseDirectory);
        }
    }
}
