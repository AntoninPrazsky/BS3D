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
    /// disposes one itself, it <see cref="Release"/>s it, and <see cref="Dispose"/> frees whatever is left once.
    /// </para>
    /// <para>
    /// <b>A grid can also be asked for NEAR FIRST</b> (#781): the same vertices, the quads in rings from the centre out,
    /// so a camera at the centre draws its terrain near to far whichever way it looks and the depth test rejects what a
    /// nearer ridge hides before it is shaded. A separate entry, because the order is the only difference and a scene
    /// that does not ask keeps the row order it was measured with. It pays only where the depth test can run before
    /// the pixel shader, which a shader that may <c>clip</c> forfeits: the mountain's High and Ultra program cuts its
    /// island hole in the vertex shader for that reason.
    /// </para>
    /// </summary>
    internal sealed class TerrainGridCache : IDisposable
    {
        /// <summary>One shared grid: the two buffers and the index count a draw takes.</summary>
        internal sealed class Grid
        {
            public VertexBuffer Vertices { get; init; }
            public IndexBuffer Indices { get; init; }
            public int IndexCount { get; init; }
            internal int References;
        }

        private readonly GraphicsDevice _device;
        private readonly Dictionary<(int N, float Extent, bool NearFirst), Grid> _grids = new();

        public TerrainGridCache(GraphicsDevice device) => _device = device;

        /// <summary>
        /// The grid of <paramref name="n"/> vertices a side over <paramref name="extent"/>, built on its first request;
        /// <paramref name="nearFirst"/> asks for its quads in rings from the centre out (see the class).
        /// </summary>
        public Grid Acquire(int n, float extent, bool nearFirst = false)
        {
            if (!_grids.TryGetValue((n, extent, nearFirst), out Grid grid))
            {
                grid = Build(n, extent, nearFirst);
                _grids.Add((n, extent, nearFirst), grid);
            }

            grid.References++;
            return grid;
        }

        /// <summary>Gives back one reference to the grid <see cref="Acquire"/> handed out for the pair; the last one frees it.</summary>
        public void Release(int n, float extent, bool nearFirst = false)
        {
            if (!_grids.TryGetValue((n, extent, nearFirst), out Grid grid))
                throw new InvalidOperationException($"No terrain grid {n} a side over {extent} (near first: {nearFirst}) is held; released twice?");

            if (--grid.References > 0) return;

            grid.Vertices.Dispose();
            grid.Indices.Dispose();
            _grids.Remove((n, extent, nearFirst));
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
        private Grid Build(int n, float extent, bool nearFirst)
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
            int[] indices = Indices(n, nearFirst);

            IndexBuffer indexBuffer = new(_device, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            indexBuffer.SetData(indices);

            return new Grid { Vertices = vertexBuffer, Indices = indexBuffer, IndexCount = indices.Length };
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
