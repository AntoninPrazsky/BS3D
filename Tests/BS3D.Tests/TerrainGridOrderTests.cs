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
    /// order is part of the picture, and the runs a draw is cut into - by the lens, and by which of a scene's two
    /// programs a block takes - may never change it.
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

                int entries = TerrainGridCache.SeenRuns(n, side, start, count, frustum, originX, originZ, extent, -1000f, 3000f, 0f, null, runs);
                List<int> drawn = BlocksOf(runs, entries, start, count, out List<int> kinds);

                Assert.Equal(SeenBlocks(n, side, frustum, originX, originZ, extent), drawn);
                Assert.All(kinds, kind => Assert.Equal(0, kind));
                for (int i = TerrainGridCache.RUN_ENTRIES; i < entries; i += TerrainGridCache.RUN_ENTRIES)
                    Assert.True(runs[i] > runs[i - TerrainGridCache.RUN_ENTRIES] + runs[i - TerrainGridCache.RUN_ENTRIES + 1],
                        $"{name}: two runs lie back to back in the buffer and were not drawn as one");

                _output.WriteLine($"{n} over {extent}, {name}: {drawn.Count} of {side * side} blocks in {entries / TerrainGridCache.RUN_ENTRIES} draws");
            }
        }

        //A grid drawn with two programs (#1097): the same blocks in the same rows' order as with one, each run of one kind
        //only - a block a cut reaches in a run of the cutting program's, every other in one of the open program's - and a
        //run ends only where the kind changes or the next block is not seen
        [Theory]
        [InlineData(220, 1200f)]
        [InlineData(360, 1000f)]
        [InlineData(360, 1200f)]
        [InlineData(400, 1000f)]
        public void TwoProgramsDrawTheSameBlocksInTheSameOrder(int n, float extent)
        {
            TerrainGridCache.BlockIndices(n, TerrainGridCache.BLOCK_QUADS, out int side, out int[] start, out int[] count);
            int[] runs = new int[TerrainGridCache.RunCapacity(side * side)];
            bool[] cut = new bool[side * side];
            int cutDrawn = 0;

            foreach ((Vector3 eye, Vector3 target, float fov, string name) in Lenses())
            {
                BoundingFrustum frustum = Frustum(eye, target, fov);
                (float originX, float originZ) = Origin(eye, n, extent);
                TerrainGridCache.MarkCutBlocks(n, side, originX, originZ, extent, new[] { new Vector3(0f, 0f, IslandBerm.COVER_RADIUS_MAX) }, cut);
                Assert.InRange(cut.Count(c => c), 1, 16);

                int entries = TerrainGridCache.SeenRuns(n, side, start, count, frustum, originX, originZ, extent, -1000f, 3000f, 0f, cut, runs);
                List<int> drawn = BlocksOf(runs, entries, start, count, out List<int> kinds);

                Assert.Equal(SeenBlocks(n, side, frustum, originX, originZ, extent), drawn);
                for (int k = 0; k < drawn.Count; k++)
                    Assert.True(kinds[k] == (cut[drawn[k]] ? 1 : 0),
                        $"{name}: block {drawn[k]} is drawn with the {(kinds[k] == 1 ? "cutting" : "open")} program and a cut {(cut[drawn[k]] ? "reaches" : "does not reach")} it");
                for (int i = TerrainGridCache.RUN_ENTRIES; i < entries; i += TerrainGridCache.RUN_ENTRIES)
                    Assert.True(runs[i] > runs[i - TerrainGridCache.RUN_ENTRIES] + runs[i - TerrainGridCache.RUN_ENTRIES + 1] || runs[i + 2] != runs[i - 1],
                        $"{name}: two runs of one kind lie back to back in the buffer and were not drawn as one");

                cutDrawn += kinds.Count(kind => kind == 1);
                _output.WriteLine($"{n} over {extent}, {name}: {kinds.Count(kind => kind == 1)} blocks with the cuts and {kinds.Count(kind => kind == 0)} open in {entries / TerrainGridCache.RUN_ENTRIES} draws");
            }

            Assert.True(cutDrawn > 0, "no lens drew a block a cut reaches: the kinds were never told apart");
        }

        //No pixel a cut could take may land in a block drawn without the cuts: every point of the ground inside a circle lies
        //in a marked block (in both, on a shared edge) - for the island's two circles and for circles anywhere on the grid,
        //with the grid's origin anywhere a camera snaps it. And it can fail: against the blocks a circle too small by a
        //block's width marks, points of the real one are left in unmarked blocks
        [Theory]
        [InlineData(220, 1200f)]
        [InlineData(256, 1200f)]
        [InlineData(360, 1000f)]
        [InlineData(400, 1000f)]
        [InlineData(420, 1200f)]
        public void EveryPointACutReachesIsInAMarkedBlock(int n, float extent)
        {
            int block = TerrainGridCache.BLOCK_QUADS, quads = n - 1, side = (quads + block - 1) / block;
            float cell = extent / (n - 1), half = extent * 0.5f, span = block * cell;
            bool[] cut = new bool[side * side];
            var random = new Random(1097);

            int strays = 0;
            for (int trial = 0; trial < 120; trial++)
            {
                float originX = MathF.Round((float)(random.NextDouble() * 180 - 90) / cell) * cell;
                float originZ = MathF.Round((float)(random.NextDouble() * 180 - 90) / cell) * cell;
                var circle = trial % 2 == 0
                    ? new Vector3(0f, 0f, trial % 4 == 0 ? ArenaIsland.TERRAIN_HOLE_RADIUS : IslandBerm.COVER_RADIUS_MAX)
                    : new Vector3((float)(random.NextDouble() * 900 - 450), (float)(random.NextDouble() * 900 - 450), 5f + (float)random.NextDouble() * 120f);

                TerrainGridCache.MarkCutBlocks(n, side, originX, originZ, extent, new[] { circle }, cut);
                Assert.Equal(0, Strays(cut));

                TerrainGridCache.MarkCutBlocks(n, side, originX, originZ, extent, new[] { new Vector3(circle.X, circle.Y, MathF.Max(circle.Z - span, 0.01f)) }, cut);
                strays += Strays(cut);

                //How many sampled points of the circle (its rim included) lie in a block that is not marked
                int Strays(bool[] marked)
                {
                    int found = 0;
                    for (int a = 0; a < 720; a++)
                        for (int r = 0; r <= 24; r++)
                        {
                            float px = circle.X + MathF.Cos(MathF.Tau * a / 720f) * circle.Z * r / 24f;
                            float pz = circle.Y + MathF.Sin(MathF.Tau * a / 720f) * circle.Z * r / 24f;
                            float gx = (px - (originX - half)) / span, gz = (pz - (originZ - half)) / span;

                            //A point within a thousandth of a block's edge is taken as lying in the blocks on both sides of it
                            for (int bz = (int)MathF.Floor(gz - 0.001f); bz <= (int)MathF.Floor(gz + 0.001f); bz++)
                                for (int bx = (int)MathF.Floor(gx - 0.001f); bx <= (int)MathF.Floor(gx + 0.001f); bx++)
                                    if (bx >= 0 && bx < side && bz >= 0 && bz < side && !marked[bz * side + bx]) found++;
                        }
                    return found;
                }
            }

            Assert.True(strays > 0, "a circle a block's width too small left no point in an unmarked block: the check cannot fail");
        }

        //The map editor draws its ground whole (IslandHoleRadius 0, where the cutting program's clip() takes nothing): no
        //block is marked, whatever stood in the table before, so the whole grid takes the open program
        [Fact]
        public void NoHoleMarksNoBlock()
        {
            TerrainGridCache.BlockIndices(360, TerrainGridCache.BLOCK_QUADS, out int side, out _, out _);
            bool[] cut = new bool[side * side];
            Array.Fill(cut, true);

            TerrainGridCache.MarkCutBlocks(360, side, 5.57f, -2.79f, 1000f, new[] { new Vector3(0f, 0f, 0f) }, cut);

            Assert.DoesNotContain(true, cut);
        }

        //The marks are kept between frames and worked out again only when something moved (TerrainCuts): after a change
        //of the grid's snapped origin, of the radius or of the grid itself they must be the marks a fresh count gives. A
        //stale table would draw the hole's own block with the program that cannot cut it. ONE thing moves a step, so a
        //table that forgot to look at any one of them is caught by the step that moved it alone - and each of the four
        //must have moved the marks by itself some time, or that step proved nothing
        [Fact]
        public void TheCutBlocksFollowTheGridTheLensAndTheRadius()
        {
            const float extent = 1000f;

            //Mars's two grids, and a third with the first one's count of blocks and another cell: a table that told two
            //grids apart by its length alone would be stale between those two
            var grids = new[] { Grid(360), Grid(256), Grid(354) };
            Assert.Equal(grids[0].BlocksPerSide, grids[2].BlocksPerSide);

            //No hole at all (the map editor), the island's hole, and the bank's cover
            float[] radii = { 0f, ArenaIsland.TERRAIN_HOLE_RADIUS, IslandBerm.COVER_RADIUS_MAX };
            var cuts = new TerrainCuts();
            var random = new Random(1097);
            bool[] fresh = Array.Empty<bool>();

            int grid = 0, radius = 1;
            float originX = 0f, originZ = 0f;
            int[] moved = new int[4];
            bool[] before = null;
            for (int step = 0; step < 2000; step++)
            {
                //The origin walks by whole cells, as a camera's snapped origin does, a block's width or so a step
                float cell = extent / (grids[grid].N - 1);
                int what = step == 0 ? -1 : random.Next(4);
                switch (what)
                {
                    case 0: originX = MathF.Round((float)(random.NextDouble() * 200 - 100) / cell) * cell; break;
                    case 1: originZ = MathF.Round((float)(random.NextDouble() * 200 - 100) / cell) * cell; break;
                    case 2: radius = (radius + 1 + random.Next(2)) % radii.Length; break;
                    case 3: grid = (grid + 1 + random.Next(2)) % grids.Length; break;
                }

                bool[] marks = cuts.Blocks(grids[grid], originX, originZ, extent, radii[radius]);
                if (fresh.Length != marks.Length) fresh = new bool[marks.Length];
                TerrainGridCache.MarkCutBlocks(grids[grid].N, grids[grid].BlocksPerSide, originX, originZ, extent,
                    new[] { new Vector3(0f, 0f, radii[radius]) }, fresh);
                Assert.True(fresh.SequenceEqual(marks),
                    $"step {step}: the marks are not a fresh count's after {(what < 0 ? "the start" : new[] { "the origin's X", "the origin's Z", "the radius", "the grid" }[what])} moved");
                Assert.Same(marks, cuts.Blocks(grids[grid], originX, originZ, extent, radii[radius]));

                if (what >= 0 && !before.SequenceEqual(marks)) moved[what]++;
                before = (bool[])marks.Clone();
            }

            Assert.All(moved, count => Assert.True(count > 20, $"the marks moved {string.Join(", ", moved)} times with the origin's X, its Z, the radius and the grid: one of them hardly ever alone"));

            static TerrainGridCache.Grid Grid(int n)
            {
                TerrainGridCache.BlockIndices(n, TerrainGridCache.BLOCK_QUADS, out int side, out int[] start, out int[] count);
                return new TerrainGridCache.Grid { N = n, BlocksPerSide = side, BlockStart = start, BlockCount = count };
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

        //The blocks a list of runs covers in the order it draws them, each run whole blocks back to back in the buffer,
        //and beside each block the kind of the run it came in
        private static List<int> BlocksOf(int[] runs, int entries, int[] start, int[] count, out List<int> kinds)
        {
            var blocks = new List<int>();
            kinds = new List<int>();
            for (int i = 0; i < entries; i += TerrainGridCache.RUN_ENTRIES)
            {
                int at = runs[i], end = runs[i] + runs[i + 1];
                int b = Array.IndexOf(start, at);
                Assert.True(b >= 0, $"a run starts at index {at}, where no block does");
                while (at < end)
                {
                    Assert.Equal(at, start[b]);
                    blocks.Add(b);
                    kinds.Add(runs[i + 2]);
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
