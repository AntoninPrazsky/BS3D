using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The terrain grids' triangle orders (<see cref="TerrainGridCache"/>): the row order every grid has always had, the
    /// near-first order the mountain's High and Ultra grid draws in so the depth test can reject a hidden ridge before it
    /// is shaded (#781), and the blocks a pass draws only where its lens looks (#1035). Every order must hold exactly the
    /// row order's triangles - a quad left out is a hole in the ground, a quad drawn twice a seam. And the blocks must
    /// lie and be drawn in their rows (#1097): where two triangles land on one depth value the last drawn wins, so that
    /// order is part of the picture, and the runs the lens cuts a draw into may never change it.
    /// </summary>
    public class TerrainGridOrderTests
    {
        private readonly ITestOutputHelper _output;

        public TerrainGridOrderTests(ITestOutputHelper output) => _output = output;

        [Theory]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(61)]
        [InlineData(720)]
        public void NearFirstHoldsEveryTriangleOfTheRowOrderOnce(int n)
        {
            int[] rows = TerrainGridCache.Indices(n, nearFirst: false);
            int[] rings = TerrainGridCache.Indices(n, nearFirst: true);

            Assert.Equal(rows.Length, rings.Length);
            Assert.Equal(Triangles(rows).OrderBy(t => t).ToArray(), Triangles(rings).OrderBy(t => t).ToArray());
        }

        [Theory]
        [InlineData(5)]
        [InlineData(61)]
        [InlineData(720)]
        public void NearFirstRunsOutwardFromTheCentre(int n)
        {
            int[] rings = TerrainGridCache.Indices(n, nearFirst: true);
            int centre = (n - 2) / 2;

            int previous = 0;
            for (int quad = 0; quad < rings.Length / 6; quad++)
            {
                //The first corner of a quad's first triangle is its lower-left vertex, z * n + x
                int a = rings[quad * 6];
                int ring = Math.Max(Math.Abs(a % n - centre), Math.Abs(a / n - centre));

                if (quad == 0) Assert.Equal(0, ring);
                Assert.True(ring >= previous, $"quad {quad} is in ring {ring} after a quad in ring {previous}");
                previous = ring;
            }
        }

        [Fact]
        public void TheRowOrderIsTheOneTheGridsWereMeasuredWith()
        {
            //Low and Medium's grids, and every other scene's, keep this order to the index: two triangles a quad, row by row
            int n = 4;
            var expected = new List<int>();
            for (int z = 0; z < n - 1; z++)
                for (int x = 0; x < n - 1; x++)
                {
                    int a = z * n + x, b = a + 1, c = a + n, d = c + 1;
                    expected.AddRange(new[] { a, c, b, b, c, d });
                }

            Assert.Equal(expected.ToArray(), TerrainGridCache.Indices(n, nearFirst: false));
        }

        //The block order (#1032): the row order's triangles once each, every block one run of the buffer with the quads it
        //names, the runs back to back - a block whose range strayed would draw another's quads, or leave a hole. And the
        //order itself (#1097): the blocks row by row and each block's quads row by row, the same two triangles a quad.
        //It is the order the committed picture was drawn in, and equal depths on a far skyline are settled by it
        [Theory]
        [InlineData(4, 2)]
        [InlineData(61, 32)]
        [InlineData(256, 32)]
        [InlineData(360, 32)]
        [InlineData(220, 16)]
        [InlineData(360, 16)]
        [InlineData(400, 16)]
        public void TheBlockOrderHoldsEveryTriangleOnceInRunsOfItsBlocks(int n, int block)
        {
            int[] rows = TerrainGridCache.Indices(n, nearFirst: false);
            int[] blocks = TerrainGridCache.BlockIndices(n, block, out int side, out int[] start, out int[] count);

            Assert.Equal(rows.Length, blocks.Length);
            Assert.Equal(Triangles(rows).OrderBy(t => t).ToArray(), Triangles(blocks).OrderBy(t => t).ToArray());

            int next = 0;
            for (int b = 0; b < side * side; b++)
            {
                Assert.Equal(next, start[b]);
                int previous = -1;
                for (int q = start[b]; q < start[b] + count[b]; q += 6)
                {
                    //The quad's lower-left vertex, z * n + x, lies in this block, after the block's quad before it
                    int a = blocks[q];
                    Assert.Equal(b, (a / n / block) * side + (a % n / block));
                    Assert.True(a > previous, $"block {b}: the quad at vertex {a} follows the quad at vertex {previous}");
                    previous = a;

                    int right = a + 1, up = a + n, across = up + 1;
                    Assert.Equal(new[] { a, up, right, right, up, across }, blocks[q..(q + 6)]);
                }
                next = start[b] + count[b];
            }
            Assert.Equal(blocks.Length, next);
        }

        //What a lens draws (#1035, #1097): every block its frustum touches exactly once, whole, in the rows' order, and in
        //as few draws as that order allows - a run ends only where the next block in the buffer is not seen. The counts
        //are printed, for the record of what a frame's ground costs in calls
        [Theory]
        [InlineData(220, 1200f)]
        [InlineData(360, 1000f)]
        [InlineData(360, 1200f)]
        [InlineData(400, 1000f)]
        [InlineData(420, 1200f)]
        public void ALensDrawsEverySeenBlockOnceInTheRowsOrder(int n, float extent)
        {
            TerrainGridCache.BlockIndices(n, TerrainGridCache.BLOCK_QUADS, out int side, out int[] start, out int[] count);
            int[] runs = new int[TerrainGridCache.RunCapacity(side * side)];

            foreach ((Vector3 eye, Vector3 target, float fov, string name) in Lenses())
            {
                BoundingFrustum frustum = Frustum(eye, target, fov);
                (float originX, float originZ) = Origin(eye, n, extent);

                int entries = TerrainGridCache.SeenRuns(n, side, start, count, frustum, originX, originZ, extent, -1000f, 3000f, 0f, runs);
                List<int> drawn = BlocksOf(runs, entries, start, count);

                Assert.Equal(SeenBlocks(n, side, frustum, originX, originZ, extent), drawn);
                for (int i = TerrainGridCache.RUN_ENTRIES; i < entries; i += TerrainGridCache.RUN_ENTRIES)
                    Assert.True(runs[i] > runs[i - TerrainGridCache.RUN_ENTRIES] + runs[i - TerrainGridCache.RUN_ENTRIES + 1],
                        $"{name}: two runs lie back to back in the buffer and were not drawn as one");

                _output.WriteLine($"{n} over {extent}, {name}: {drawn.Count} of {side * side} blocks in {entries / TerrainGridCache.RUN_ENTRIES} draws");
            }
        }

        //The play camera at eight bearings round the arena (the Testbed's game-mode pose and field of view), a low lens
        //along the ground, and the free camera high over the island looking down its drain
        private static IEnumerable<(Vector3 Eye, Vector3 Target, float Fov, string Name)> Lenses()
        {
            for (int i = 0; i < 8; i++)
            {
                float a = MathF.Tau * i / 8f;
                yield return (new Vector3(MathF.Sin(a) * 36.2f, -7.4f, MathF.Cos(a) * 36.2f), new Vector3(0f, 1.65f, 0f), MathF.PI / 4.2f, $"play camera at {i * 45} degrees");
            }

            yield return (new Vector3(60f, -11f, 25f), new Vector3(0f, -10f, -400f), MathF.PI / 2.5f, "low along the ground");
            yield return (new Vector3(3f, 60f, 4f), new Vector3(0f, -9f, 0f), MathF.PI / 2.5f, "down the drain");
        }

        private static BoundingFrustum Frustum(Vector3 eye, Vector3 target, float fov) =>
            new(Matrix.CreateLookAt(eye, target, Vector3.Up) * Matrix.CreatePerspectiveFieldOfView(fov, 3840f / 1500f, 0.5f, 2000f));

        //A grid's origin, snapped to a cell under the lens as every terrain pass snaps it
        private static (float X, float Z) Origin(Vector3 eye, int n, float extent)
        {
            float cell = extent / (n - 1);
            return (MathF.Round(eye.X / cell) * cell, MathF.Round(eye.Z / cell) * cell);
        }

        //The blocks whose box the lens touches, row by row, counted off the grid's figures alone
        private static List<int> SeenBlocks(int n, int side, BoundingFrustum frustum, float originX, float originZ, float extent)
        {
            float cell = extent / (n - 1), half = extent * 0.5f, span = TerrainGridCache.BLOCK_QUADS * cell;
            var seen = new List<int>();
            for (int b = 0; b < side * side; b++)
            {
                float x0 = originX - half + b % side * span, z0 = originZ - half + b / side * span;
                if (frustum.Contains(new BoundingBox(new Vector3(x0, -1000f, z0), new Vector3(x0 + span, 3000f, z0 + span))) != ContainmentType.Disjoint)
                    seen.Add(b);
            }
            return seen;
        }

        //The blocks a list of runs covers in the order it draws them, each run whole blocks back to back in the buffer
        private static List<int> BlocksOf(int[] runs, int entries, int[] start, int[] count)
        {
            var blocks = new List<int>();
            for (int i = 0; i < entries; i += TerrainGridCache.RUN_ENTRIES)
            {
                int at = runs[i], end = runs[i] + runs[i + 1];
                int b = Array.IndexOf(start, at);
                Assert.True(b >= 0, $"a run starts at index {at}, where no block does");
                while (at < end)
                {
                    Assert.Equal(at, start[b]);
                    blocks.Add(b);
                    at += count[b];
                    b++;
                }
                Assert.Equal(end, at);
            }
            return blocks;
        }

        private static IEnumerable<(int, int, int)> Triangles(int[] indices)
        {
            for (int i = 0; i < indices.Length; i += 3)
                yield return (indices[i], indices[i + 1], indices[i + 2]);
        }
    }
}
