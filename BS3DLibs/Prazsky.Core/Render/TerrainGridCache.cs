using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The flat lattice grids the terrain scenes displace, built once per <c>(vertices a side, extent)</c> and
    /// shared by every scene that asks for the same pair (#589). A grid carries nothing scene-specific — every
    /// terrain shader recentres it on the camera through its own <c>OriginXZ</c> and lifts it in the vertex
    /// shader — so the desert, the tropical beach and Mars (360 over 1000), the mountain, the volcano and the Moon
    /// (360 over 1200) and the meadow, the forest and the aurora (220 over 1200) had each been holding their own
    /// byte-identical copy: fourteen grids built where eight are distinct, 56.1 MB of buffers where 34.0 MB
    /// hold every grid once (measured through the cache at load, #589), and six grids built at every launch for
    /// nothing — about 45 ms of the Testbed's ~510 ms <c>SceneRenderer</c> constructor on the desktop.
    /// <para>
    /// Reference-counted, because two of the grids are not fixed: the volcano's and Mars's step down to 256 a side
    /// under the reduced detail program (#540) and back, and each step has to give its old grid back without
    /// taking it from the scenes still drawing it. The cache owns every buffer it hands out; a holder never
    /// disposes one itself, it <see cref="Release(int, float, bool)"/>s it, and <see cref="Dispose"/> frees whatever is left once.
    /// </para>
    /// <para>
    /// <b>A grid can also be asked for NEAR FIRST</b> (#781): the same vertices, the quads in rings from the centre out,
    /// so a camera at the centre draws its terrain near to far whichever way it looks and the depth test rejects what a
    /// nearer ridge hides before it is shaded. A separate entry, because the order is the only difference and a scene
    /// that does not ask keeps the row order it was measured with. It pays only where the depth test can run before
    /// the pixel shader, which a shader that may <c>clip</c> forfeits: the mountain's High and Ultra program cuts its
    /// island hole in the vertex shader for that reason.
    /// </para>
    /// <para>
    /// <b>And IN BLOCKS</b> (#1032, #1035): the quads in square blocks of <see cref="BLOCK_QUADS"/>, each one run of the
    /// buffer, with the table of where each starts, so a pass draws only the blocks its lens sees
    /// (<see cref="DrawSeenBlocks"/>). A grid closes round the camera and its vertex program ran on every vertex of it,
    /// behind the lens as in front; on the reference APU that was the volcano's whole step under #748's land.
    /// </para>
    /// </summary>
    internal sealed class TerrainGridCache : IDisposable
    {
        /// <summary>The order a grid's quads are in: row by row, in rings from the centre out (#781), or in blocks (#1035).</summary>
        internal enum Order { Rows, NearFirst, Blocks }

        /// <summary>How many quads a side a block of a <see cref="Order.Blocks"/> grid holds.</summary>
        public const int BLOCK_QUADS = 16;

        /// <summary>
        /// One shared grid: the two buffers and the index count a draw takes, and for a grid in blocks its vertices a side and
        /// each block's first index and index count (<see cref="BlockIndices"/>).
        /// </summary>
        internal sealed class Grid
        {
            public VertexBuffer Vertices { get; init; }
            public IndexBuffer Indices { get; init; }
            public int IndexCount { get; init; }
            public int N { get; init; }
            public int BlocksPerSide { get; init; }
            public int[] BlockStart { get; init; }
            public int[] BlockCount { get; init; }
            internal int References;
        }

        private readonly GraphicsDevice _device;
        private readonly Dictionary<(int N, float Extent, Order Order), Grid> _grids = new();

        public TerrainGridCache(GraphicsDevice device) => _device = device;

        /// <summary>
        /// The grid of <paramref name="n"/> vertices a side over <paramref name="extent"/>, built on its first request;
        /// <paramref name="nearFirst"/> asks for its quads in rings from the centre out (see the class).
        /// </summary>
        public Grid Acquire(int n, float extent, bool nearFirst = false) => Acquire(n, extent, nearFirst ? Order.NearFirst : Order.Rows);

        /// <summary>The grid of <paramref name="n"/> vertices a side over <paramref name="extent"/> in <paramref name="order"/>.</summary>
        public Grid Acquire(int n, float extent, Order order)
        {
            if (!_grids.TryGetValue((n, extent, order), out Grid grid))
            {
                grid = Build(n, extent, order);
                _grids.Add((n, extent, order), grid);
            }

            grid.References++;
            return grid;
        }

        /// <summary>Gives back one reference to the grid <see cref="Acquire(int, float, bool)"/> handed out for the pair; the last one frees it.</summary>
        public void Release(int n, float extent, bool nearFirst = false) => Release(n, extent, nearFirst ? Order.NearFirst : Order.Rows);

        /// <summary>Gives back one reference to the grid <see cref="Acquire(int, float, Order)"/> handed out; the last one frees it.</summary>
        public void Release(int n, float extent, Order order)
        {
            if (!_grids.TryGetValue((n, extent, order), out Grid grid))
                throw new InvalidOperationException($"No terrain grid {n} a side over {extent} ({order}) is held; released twice?");

            if (--grid.References > 0) return;

            grid.Vertices.Dispose();
            grid.Indices.Dispose();
            _grids.Remove((n, extent, order));
        }

        /// <summary>
        /// Draws the blocks of <paramref name="grid"/> (a <see cref="Order.Blocks"/> grid, its buffers and the effect's pass
        /// already set) whose box the <paramref name="frustum"/> touches, each run of neighbouring ones in one draw. A block's
        /// box spans its cells round the grid's snapped origin and every height from <paramref name="lowY"/> to
        /// <paramref name="highY"/>, which the caller states wide enough for anything its vertex program lifts the ground to.
        /// </summary>
        public static void DrawSeenBlocks(GraphicsDevice device, Grid grid, BoundingFrustum frustum, float originX, float originZ,
            float extent, float lowY, float highY)
        {
            float cell = extent / (grid.N - 1);
            float half = extent * Constants.HALF, span = BLOCK_QUADS * cell;
            int side = grid.BlocksPerSide;

            int runStart = -1, runEnd = 0;
            for (int bz = 0; bz < side; bz++)
                for (int bx = 0; bx < side; bx++)
                {
                    int b = bz * side + bx;
                    float x0 = originX - half + bx * span, z0 = originZ - half + bz * span;
                    var box = new BoundingBox(new Vector3(x0, lowY, z0), new Vector3(x0 + span, highY, z0 + span));
                    if (frustum.Contains(box) != ContainmentType.Disjoint)
                    {
                        if (runStart < 0) runStart = grid.BlockStart[b];
                        runEnd = grid.BlockStart[b] + grid.BlockCount[b];
                    }
                    else if (runStart >= 0)
                    {
                        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, runStart, (runEnd - runStart) / 3);
                        runStart = -1;
                    }
                }

            if (runStart >= 0) device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, runStart, (runEnd - runStart) / 3);
        }

        public void Dispose()
        {
            foreach (Grid grid in _grids.Values)
            {
                grid.Vertices.Dispose();
                grid.Indices.Dispose();
            }
            _grids.Clear();
        }

        /// <summary>
        /// Builds a flat lattice grid: <paramref name="n"/> vertices per side over <paramref name="extent"/>,
        /// centred on the origin. Every terrain shader recentres it on the camera and lifts it into waves, dunes,
        /// peaks or hills; it is drawn CullNone, so the winding does not matter. Indices are 32-bit: every one of
        /// these grids runs past 255 vertices a side, where a 16-bit index silently wraps (see the inline note
        /// below — that wrap has already cost one long hunt).
        /// </summary>
        private Grid Build(int n, float extent, Order order)
        {
            float half = extent * Constants.HALF;
            float step = extent / (n - 1);

            VertexPosition[] vertices = new VertexPosition[n * n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                    vertices[z * n + x] = new VertexPosition(new Vector3(-half + x * step, 0f, -half + z * step));

            VertexBuffer vertexBuffer = new(_device, VertexPosition.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            vertexBuffer.SetData(vertices);

            //32-bit indices: these grids run to hundreds of vertices a side (the mountain at 360, the savanna
            //at 400 = 160k vertices), well past the 65 536 a 16-bit index can address. A 16-bit index silently
            //wraps at that point, so triangles reference the wrong vertices and stretch into garbage. On a near
            //flat field (sea, savanna) the garbage stays down near the surface and hides; on the mountain's tall
            //peaks it stretched into long dark bands across the whole sky. Wrong cause chased for a while - it
            //looked like a glare/shading artifact - so: a grid over 255 a side MUST use 32-bit indices.
            int side = 0;
            int[] blockStart = null, blockCount = null;
            int[] indices = order == Order.Blocks
                ? BlockIndices(n, BLOCK_QUADS, out side, out blockStart, out blockCount)
                : Indices(n, order == Order.NearFirst);

            IndexBuffer indexBuffer = new(_device, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            indexBuffer.SetData(indices);

            return new Grid
            {
                Vertices = vertexBuffer, Indices = indexBuffer, IndexCount = indices.Length,
                N = n, BlocksPerSide = side, BlockStart = blockStart, BlockCount = blockCount,
            };
        }

        /// <summary>
        /// The triangle list of an <paramref name="n"/>-a-side grid: two triangles a quad, row by row, or with
        /// <paramref name="nearFirst"/> the same quads in square rings round the centre quad, innermost first (#781).
        /// Static and device-free so the order is testable.
        /// </summary>
        internal static int[] Indices(int n, bool nearFirst)
        {
            int[] indices = new int[(n - 1) * (n - 1) * 6];
            int i = 0;
            void Quad(int x, int z)
            {
                int a = z * n + x;
                int b = z * n + x + 1;
                int c = (z + 1) * n + x;
                int d = (z + 1) * n + x + 1;

                indices[i++] = a; indices[i++] = c; indices[i++] = b;
                indices[i++] = b; indices[i++] = c; indices[i++] = d;
            }

            if (nearFirst)
            {
                //Ring r holds the quads whose Chebyshev distance from the centre quad is r. Every quad lands in exactly
                //one ring, so the buffer holds the same triangles as the row order.
                int centre = (n - 2) / 2;
                int last = n - 2;
                for (int ring = 0; ring <= Math.Max(centre, last - centre); ring++)
                    for (int z = Math.Max(0, centre - ring); z <= Math.Min(last, centre + ring); z++)
                        for (int x = Math.Max(0, centre - ring); x <= Math.Min(last, centre + ring); x++)
                            if (Math.Max(Math.Abs(x - centre), Math.Abs(z - centre)) == ring)
                                Quad(x, z);
            }
            else
            {
                for (int z = 0; z < n - 1; z++)
                    for (int x = 0; x < n - 1; x++)
                        Quad(x, z);
            }

            return indices;
        }

        /// <summary>
        /// The same quads in square blocks of <paramref name="block"/> a side (#1032), block row by block row and each block's
        /// quads row by row, so a block is one run of the buffer and a caller can draw only the blocks its lens sees:
        /// <paramref name="blockStart"/> and <paramref name="blockCount"/> are each block's first index and index count, block
        /// <c>bz * blocksPerSide + bx</c> covering the quads from <c>bx * block</c> and <c>bz * block</c> (the last of a row
        /// narrower when <paramref name="block"/> does not divide the grid). Static and device-free so the order is testable.
        /// </summary>
        internal static int[] BlockIndices(int n, int block, out int blocksPerSide, out int[] blockStart, out int[] blockCount)
        {
            int quads = n - 1;
            blocksPerSide = (quads + block - 1) / block;
            blockStart = new int[blocksPerSide * blocksPerSide];
            blockCount = new int[blocksPerSide * blocksPerSide];

            int[] indices = new int[quads * quads * 6];
            int i = 0;
            for (int bz = 0; bz < blocksPerSide; bz++)
                for (int bx = 0; bx < blocksPerSide; bx++)
                {
                    int b = bz * blocksPerSide + bx;
                    blockStart[b] = i;
                    for (int z = bz * block; z < Math.Min(quads, (bz + 1) * block); z++)
                        for (int x = bx * block; x < Math.Min(quads, (bx + 1) * block); x++)
                        {
                            int a = z * n + x, bb = z * n + x + 1, c = (z + 1) * n + x, d = (z + 1) * n + x + 1;
                            indices[i++] = a; indices[i++] = c; indices[i++] = bb;
                            indices[i++] = bb; indices[i++] = c; indices[i++] = d;
                        }
                    blockCount[b] = i - blockStart[b];
                }

            return indices;
        }
    }
}
