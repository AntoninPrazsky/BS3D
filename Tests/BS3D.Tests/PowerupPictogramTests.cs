using System;
using System.Threading.Tasks;
using BS3D.Screens;
using Microsoft.Xna.Framework;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The power-ups' pictograms (#991): each must read, and read as itself, at the chip's size, by shape before colour (they are
    /// all drawn white). Held on the rasters <see cref="PowerupPictograms.Raster"/> bakes: every picture covers a sane share of its
    /// square, and no two overlap so much that 40 pixels could not tell them apart. And since #1146, that the raster written for
    /// cold code draws exactly what the first one drew, and that the drawing's version moves with the drawing.
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

        //The bake as it was written before #1095 shared it out by rows and paired the picture with its backing: one pixel at a
        //time, the same sixteen samples in the same order, over the drawing as #991 wrote it (FirstDrawing). The rows are
        //shared out here only to keep the sweep below short: no row reads another's.
        private static Color[] PlainRaster(PowerupKind kind, int size, int pad, float soft)
        {
            int side = size + 2 * pad;
            var pixels = new Color[side * side];
            const int SUB = 4;
            Parallel.For(0, side, y =>
            {
                for (int x = 0; x < side; x++)
                {
                    float cover = 0f;
                    for (int sy = 0; sy < SUB; sy++)
                        for (int sx = 0; sx < SUB; sx++)
                        {
                            var p = new Vector2((x - pad + (sx + 0.5f) / SUB) / size, (y - pad + (sy + 0.5f) / SUB) / size);
                            float d = FirstDrawing.Distance(kind, p) * size;
                            float t = MathHelper.Clamp((d + 0.5f) / (soft + 0.5f), 0f, 1f);
                            cover += soft > 0f ? 1f - t * t * (3f - 2f * t) : MathHelper.Clamp(0.5f - d, 0f, 1f);
                        }
                    cover /= SUB * SUB;
                    byte a = (byte)MathF.Round(cover * 255f);
                    pixels[x + side * y] = new Color(a, a, a, a);
                }
            });
            return pixels;
        }

        /// <summary>
        /// The chip's pictures are baked in parallel rows, a picture and its backing from one pass (#1095): each must still be
        /// exactly the plain one-pixel-at-a-time bake, the soft backing included.
        /// </summary>
        [Theory]
        [InlineData(40, 6, 4.5f)]
        [InlineData(125, 15, 10.5f)]
        public void ThePairedBakeIsThePlainBakePixelForPixel(int size, int pad, float soft)
        {
            foreach (PowerupKind kind in Kinds)
            {
                (Color[] picture, Color[] backing) = PowerupPictograms.RasterWithBacking(kind, size, pad, soft);
                Assert.Equal(PlainRaster(kind, size, pad, 0f), picture);
                Assert.Equal(PlainRaster(kind, size, pad, soft), backing);
                Assert.Equal(PlainRaster(kind, size, pad, soft), PowerupPictograms.Raster(kind, size, pad, soft));
                Assert.Equal(PlainRaster(kind, size, pad, 0f), PowerupPictograms.Raster(kind, size, pad, 0f));
            }
        }

        /// <summary>
        /// The raster written for cold code (#1146: plain floats, the arrows' figures worked out once) is the first drawing
        /// bit for bit: every distance on a grid finer than any chip samples, inside the square and round it.
        /// </summary>
        [Fact]
        public void TheDrawingIsTheFirstOneBitForBit()
        {
            int differ = 0;
            Parallel.For(-300, 1301, i =>
            {
                int here = 0;
                for (int j = -300; j <= 1300; j++)
                {
                    float x = i / 1000f + 0.000123f, y = j / 1000f - 0.000077f;
                    foreach (PowerupKind kind in Kinds)
                        if (BitConverter.SingleToInt32Bits(FirstDrawing.Distance(kind, new Vector2(x, y)))
                            != BitConverter.SingleToInt32Bits(PowerupPictograms.Distance(kind, x, y))) here++;
                }
                System.Threading.Interlocked.Add(ref differ, here);
            });
            Assert.Equal(0, differ);
        }

        /// <summary>
        /// Every size the HUD asks for, with the backing it asks for, is the plain bake pixel for pixel (#1146): the chips at
        /// 1600x900, 1920x1080 (the Pi and Potato), 3840x1500 and the phone's layout as the game measured them, every size up to
        /// 64 and every seventh one past it to 260 (above a 4K panel's), each with the backing the HUD pairs with it. The
        /// distances themselves are held bit for bit above, at every size at once; this is the pixels' own word for it.
        /// </summary>
        [Fact]
        public void EverySizeTheHudAsksForIsThePlainBake()
        {
            var keys = new System.Collections.Generic.List<(int Size, int Blur)> { (49, 5), (58, 6), (81, 8), (124, 13) };
            for (int size = 8; size <= 260; size += size < 64 ? 1 : 7)
                keys.Add((size, Math.Max(1, (int)MathF.Round(size * 12f / 171f))));

            foreach ((int size, int blur) in keys)
            {
                int pad = blur * 2 + 2;
                float soft = blur * 1.5f;
                foreach (PowerupKind kind in Kinds)
                {
                    (Color[] picture, Color[] backing) = PowerupPictograms.RasterWithBacking(kind, size, pad, soft);
                    Assert.True(PlainRaster(kind, size, pad, 0f).AsSpan().SequenceEqual(picture), $"{kind} at {size} px differs from the plain bake");
                    Assert.True(PlainRaster(kind, size, pad, soft).AsSpan().SequenceEqual(backing), $"{kind}'s backing at {size} px differs from the plain bake");
                }
            }
        }

        /// <summary>
        /// The drawing's version (<see cref="PowerupPictograms.RASTER_VERSION"/>) names every pictogram a player's cache keeps
        /// (#1146), so a change to the drawing that left it alone would keep showing the old pictures. Pinned together with how
        /// much each picture and backing covers at 64 pixels: a change to any shape, stroke or falloff moves these sums by
        /// thousands, while another platform's last bit of a sine moves them by a few at most. If this fails after a change to
        /// the drawing, raise RASTER_VERSION and pin the new sums with it.
        /// </summary>
        [Fact]
        public void TheVersionMovesWithTheDrawing()
        {
            var sums = new long[Kinds.Length * 2];
            for (int k = 0; k < Kinds.Length; k++)
            {
                (Color[] picture, Color[] backing) = PowerupPictograms.RasterWithBacking(Kinds[k], 64, 8, 4.5f);
                foreach (Color c in picture) sums[2 * k] += c.A;
                foreach (Color c in backing) sums[2 * k + 1] += c.A;
            }

            //Swap's picture and backing, the Brake's, the Cut's
            long[] pinned = { 286016, 446166, 303960, 483238, 456872, 558890 };
            Assert.Equal(1, PowerupPictograms.RASTER_VERSION);
            for (int i = 0; i < sums.Length; i++)
                Assert.True(Math.Abs(sums[i] - pinned[i]) <= 64,
                    $"the drawing changed under version {PowerupPictograms.RASTER_VERSION}: {Kinds[i / 2]}'s {(i % 2 == 0 ? "picture" : "backing")} covers {sums[i]}, pinned {pinned[i]} (all: {string.Join(", ", sums)})");
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

        /// <summary>
        /// The drawing as #991 wrote it, in MonoGame's <see cref="Vector2"/>, kept as the reference the raster written for cold
        /// code is held to (#1146). A change to the drawing changes both, and the version with them.
        /// </summary>
        private static class FirstDrawing
        {
            private const float STROKE = 0.055f;
            private const float BALL = 0.12f;

            internal static float Distance(PowerupKind kind, Vector2 p) => kind switch
            {
                PowerupKind.Swap => Swap(p),
                PowerupKind.Brake => Brake(p),
                PowerupKind.Cut => Cut(p),
                _ => 1f,
            };

            private static float Swap(Vector2 p)
            {
                float d = MathF.Min(Disc(p, new Vector2(0.25f, 0.5f), BALL), Disc(p, new Vector2(0.75f, 0.5f), BALL));
                d = MathF.Min(d, ArrowArc(p, new Vector2(0.5f, 0.66f), 0.46f, 148f, 42f));
                d = MathF.Min(d, ArrowArc(p, new Vector2(0.5f, 0.34f), 0.46f, -32f, -138f));
                return d;
            }

            private static float Brake(Vector2 p)
            {
                float d = RoundedBar(p, new Vector2(0.5f, 0.42f), new Vector2(0.4f, 0.055f));
                d = MathF.Min(d, Segment(p, new Vector2(0.32f, 0.27f), new Vector2(0.5f, 0.1f)) - STROKE);
                d = MathF.Min(d, Segment(p, new Vector2(0.5f, 0.1f), new Vector2(0.68f, 0.27f)) - STROKE);
                const float r = 0.095f;
                d = MathF.Min(d, Disc(p, new Vector2(0.29f, 0.66f), r));
                d = MathF.Min(d, Disc(p, new Vector2(0.5f, 0.66f), r));
                d = MathF.Min(d, Disc(p, new Vector2(0.71f, 0.66f), r));
                d = MathF.Min(d, Disc(p, new Vector2(0.395f, 0.84f), r));
                d = MathF.Min(d, Disc(p, new Vector2(0.605f, 0.84f), r));
                return d;
            }

            private static float Cut(Vector2 p)
            {
                Vector2 q = p - new Vector2(0.5f, 0.5f);
                float r = q.Length();
                float angle = MathF.Atan2(q.Y, q.X);
                const int TEETH = 10;
                float tooth = angle / MathHelper.TwoPi * TEETH;
                tooth -= MathF.Floor(tooth);
                float outer = 0.33f + 0.11f * tooth;
                float d = (r - outer) / MathF.Sqrt(1f + MathF.Pow(0.11f * TEETH / MathHelper.TwoPi / MathF.Max(r, 0.05f), 2f) * 0.35f);
                return MathF.Max(d, 0.1f - r);
            }

            private static float Disc(Vector2 p, Vector2 centre, float radius) => Vector2.Distance(p, centre) - radius;

            private static float Segment(Vector2 p, Vector2 a, Vector2 b)
            {
                Vector2 ab = b - a;
                float t = MathHelper.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f);
                return Vector2.Distance(p, a + ab * t);
            }

            private static float RoundedBar(Vector2 p, Vector2 centre, Vector2 half)
            {
                Vector2 q = new(MathF.Abs(p.X - centre.X) - (half.X - half.Y), MathF.Abs(p.Y - centre.Y) - half.Y + half.Y);
                Vector2 outside = Vector2.Max(q, Vector2.Zero);
                return outside.Length() + MathF.Min(MathF.Max(q.X, q.Y), 0f) - half.Y;
            }

            private static float ArrowArc(Vector2 p, Vector2 centre, float radius, float from, float to)
            {
                Vector2 q = new(p.X - centre.X, centre.Y - p.Y);
                float a0 = MathHelper.ToRadians(MathF.Min(from, to)), a1 = MathHelper.ToRadians(MathF.Max(from, to));
                float angle = MathF.Atan2(q.Y, q.X);
                float ring = MathF.Abs(q.Length() - radius) - STROKE * 0.85f;
                Vector2 e0 = new(MathF.Cos(a0) * radius, MathF.Sin(a0) * radius), e1 = new(MathF.Cos(a1) * radius, MathF.Sin(a1) * radius);
                float arc = angle >= a0 && angle <= a1 ? ring : MathF.Min(Vector2.Distance(q, e0), Vector2.Distance(q, e1)) - STROKE * 0.85f;

                float end = MathHelper.ToRadians(to);
                float sense = to > from ? 1f : -1f;
                Vector2 tip0 = new(MathF.Cos(end) * radius, MathF.Sin(end) * radius);
                Vector2 tangent = new Vector2(-MathF.Sin(end), MathF.Cos(end)) * sense;
                Vector2 normal = new(tip0.X / radius, tip0.Y / radius);
                Vector2 tip = tip0 + tangent * 0.09f;
                Vector2 left = tip0 - tangent * 0.06f + normal * 0.1f, right = tip0 - tangent * 0.06f - normal * 0.1f;
                return MathF.Min(arc, Triangle(q, tip, left, right));
            }

            private static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
            {
                float d = MathF.Min(Segment(p, a, b), MathF.Min(Segment(p, b, c), Segment(p, c, a)));
                float s0 = Cross(b - a, p - a), s1 = Cross(c - b, p - b), s2 = Cross(a - c, p - c);
                bool inside = (s0 >= 0f && s1 >= 0f && s2 >= 0f) || (s0 <= 0f && s1 <= 0f && s2 <= 0f);
                return inside ? -d : d;
            }

            private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
        }
    }
}
