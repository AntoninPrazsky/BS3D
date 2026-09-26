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
        private readonly Dictionary<(int N, float Extent), Grid> _grids = new();

        public TerrainGridCache(GraphicsDevice device) => _device = device;

        /// <summary>The grid of <paramref name="n"/> vertices a side over <paramref name="extent"/>, built on its first request.</summary>
        public Grid Acquire(int n, float extent)
        {
            if (!_grids.TryGetValue((n, extent), out Grid grid))
            {
                grid = Build(n, extent);
                _grids.Add((n, extent), grid);
            }

            grid.References++;
            return grid;
        }

        /// <summary>Gives back one reference to the grid <see cref="Acquire"/> handed out for the pair; the last one frees it.</summary>
        public void Release(int n, float extent)
        {
            if (!_grids.TryGetValue((n, extent), out Grid grid))
                throw new InvalidOperationException($"No terrain grid {n} a side over {extent} is held; released twice?");

            if (--grid.References > 0) return;

            grid.Vertices.Dispose();
            grid.Indices.Dispose();
            _grids.Remove((n, extent));
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
        private Grid Build(int n, float extent)
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
            int[] indices = new int[(n - 1) * (n - 1) * 6];
            int i = 0;
            for (int z = 0; z < n - 1; z++)
                for (int x = 0; x < n - 1; x++)
                {
                    int a = z * n + x;
                    int b = z * n + x + 1;
                    int c = (z + 1) * n + x;
                    int d = (z + 1) * n + x + 1;

                    indices[i++] = a; indices[i++] = c; indices[i++] = b;
                    indices[i++] = b; indices[i++] = c; indices[i++] = d;
                }

            IndexBuffer indexBuffer = new(_device, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            indexBuffer.SetData(indices);

            return new Grid { Vertices = vertexBuffer, Indices = indexBuffer, IndexCount = i };
        }
    }
}
