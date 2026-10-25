using System;
using BS3D.Screens;
using Microsoft.Xna.Framework;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The power-ups' pictograms (#991): each must read, and read as itself, at the chip's size, by shape before colour (they are
    /// all drawn white). Held on the rasters <see cref="PowerupPictograms.Raster"/> bakes: every picture covers a sane share of its
    /// square, and no two overlap so much that 40 pixels could not tell them apart.
    /// </summary>
    public class PowerupPictogramTests
    {
        private static readonly PowerupKind[] Kinds = { PowerupKind.Swap, PowerupKind.Brake, PowerupKind.Cut };

        private static float[] Coverage(PowerupKind kind, int size)
        {
            Color[] pixels = PowerupPictograms.Raster(kind, size, 0, 0f);
            var cover = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++) cover[i] = pixels[i].A / 255f;
            return cover;
        }

        //Intersection over union of two coverages
        private static float Overlap(float[] a, float[] b)
        {
            float both = 0f, either = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                both += MathF.Min(a[i], b[i]);
                either += MathF.Max(a[i], b[i]);
            }
            return both / either;
        }

        [Theory]
        [InlineData(40)]
        [InlineData(64)]
        public void EachReadsAndNoTwoAreAlike(int size)
        {
            var covers = new float[Kinds.Length][];
            for (int k = 0; k < Kinds.Length; k++)
            {
                covers[k] = Coverage(Kinds[k], size);
                float share = 0f;
                foreach (float c in covers[k]) share += c;
                share /= covers[k].Length;
                Assert.InRange(share, 0.15f, 0.6f);
            }

            for (int a = 0; a < Kinds.Length; a++)
                for (int b = a + 1; b < Kinds.Length; b++)
                {
                    float overlap = Overlap(covers[a], covers[b]);
                    Assert.True(overlap < 0.55f, $"{Kinds[a]} and {Kinds[b]} overlap {overlap:0.00} at {size} px");
                }

            //Seen to fire: a picture against itself overlaps wholly
            Assert.Equal(1f, Overlap(covers[0], covers[0]), 3);
        }

        /// <summary>The picture stays inside its square: nothing is cut off at the edge it is baked to.</summary>
        [Fact]
        public void EachStaysInsideItsSquare()
        {
            foreach (PowerupKind kind in Kinds)
            {
                const int size = 64;
                float[] cover = Coverage(kind, size);
                for (int i = 0; i < size; i++)
                {
                    Assert.True(cover[i] < 0.5f && cover[(size - 1) * size + i] < 0.5f && cover[i * size] < 0.5f && cover[i * size + size - 1] < 0.5f,
                        $"{kind} reaches its square's edge");
                }
            }
        }
    }
}
