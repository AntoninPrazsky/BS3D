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
    /// <para>
    /// <b>⚠ THE ORDER OF THE BLOCKS IS PART OF THE PICTURE, and it stays the rows'</b> (#1097). The cameras' near plane
    /// stands at 0.01 and the depth buffer has 24 bits, so one step of depth at distance z is about z² / 168 000 world
    /// units: 1.5 at 500, 6 at 1000. On a far skyline the two slopes of one ridge land on the SAME depth value, the test is
    /// <c>LessEqual</c>, and the pixel keeps whichever triangle was drawn last. Drawing the blocks from the centre out,
    /// near to far from every bearing, is the obvious way to let the depth test reject what a nearer rise hides, and it
    /// moved 417 pixels of a 1920x1080 frame of Mars along the horizon, by up to 13 of 255. A grid in blocks is drawn in
    /// its rows, and no other order may be given it without that account.
    /// </para>
    /// <para>
    /// <b>What a nearer rise hides is rejected by laying the DEPTH FIRST instead</b> (#1097, <see cref="DrawSeenBlocks"/>
    /// with a depth pass): where the scene supersamples, the seen blocks are drawn once with the scene's depth program,
    /// no colour written, and then shaded with <see cref="DepthStencilState.DepthRead"/> in the same rows. A pixel behind
    /// a nearer one fails the test before its program runs, whichever way the lens looks; pixels on one depth value
    /// still pass in the rows' order, so the last still wins and the picture is the one pass's, bit for bit; and a
    /// program that may <c>discard</c> costs nothing once it writes no depth. Both passes run the one vertex program, or
    /// the two depths would not be the same bits.
    /// </para>
    /// <para>
    /// <b>Below the supersampling tiers a block is drawn with one of TWO programs, in one pass</b> (#1097): the depth
    /// pass draws the grid's vertices twice, which those tiers' machines were not measured to afford. A pixel program
    /// that may <c>discard</c> writes its depth after it has run, so nothing in its own draw is rejected early, and every
    /// terrain program cut something: the island's hole, the ground under the island's bank, the far ring's inner
    /// square. All of those are places, and a block none of them reaches cannot lose a pixel to any. So a scene
    /// compiles its ground twice, with the cuts and without (<c>&lt;name&gt;Open</c>, no <c>discard</c> in it), and the
    /// few blocks a cut reaches (<see cref="TerrainCuts"/>) are drawn with the first and every other block with the
    /// second, each as the rows come to it: the same pixels as one cutting program over the whole grid, and the depth
    /// test back in front of the pixel shader everywhere but round the island. In rows that helps from the bearings
    /// where the rows run away from the lens and not from the others.
    /// </para>
    /// <para>
    /// <b>Both twins are DirectX's alone.</b> Two passes over one ground, and two programs over neighbouring blocks, both
    /// need the one vertex program to land every vertex on the same position, bit for bit, in each. Direct3D runs the one
    /// compiled vertex shader under both pixel programs. OpenGL ES links it into two programs and promises nothing of
    /// the kind (the GLSL MojoShader writes declares no <c>invariant</c>), and a position one bit apart is a crack along
    /// a block's edge, or a ground that fails its own depth. So a terrain effect compiles its twins under
    /// <c>#if !OPENGL</c>, and there every grid is drawn in one pass with its one program, as it always was.
    /// </para>
    /// </summary>
    internal sealed class TerrainGridCache : IDisposable
    {
        /// <summary>The order a grid's quads are in: row by row, in rings from the centre out (#781), or in blocks (#1035).</summary>
        internal enum Order { Rows, NearFirst, Blocks }

        /// <summary>How many quads a side a block of a <see cref="Order.Blocks"/> grid holds.</summary>
        public const int BLOCK_QUADS = 16;

        /// <summary>
        /// Writes no colour: what a depth pass is drawn with (<see cref="DrawSeenBlocks"/>, <see cref="FarField.DrawRing"/>).
        /// Made once; a state object is never made per frame.
        /// </summary>
        internal static readonly BlendState DEPTH_ONLY = new() { ColorWriteChannels = ColorWriteChannels.None };

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

            //The runs of one draw (SeenRuns), held here so a frame allocates nothing; one scene draws at a time
            internal int[] Runs;
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
        /// Draws the blocks of <paramref name="grid"/> (a <see cref="Order.Blocks"/> grid, its buffers already set) whose box
        /// the <paramref name="frustum"/> touches with <paramref name="ground"/>, the pass of the scene's ground, in the
        /// rows' order, each run of neighbouring ones in one draw. A block's box spans its cells round the grid's snapped
        /// origin, grown by <paramref name="sideways"/> in X and Z, and every height from <paramref name="lowY"/> to
        /// <paramref name="highY"/>: the caller states both wide enough for anything its vertex program moves the ground
        /// by. Only the sea's waves move a vertex sideways (<see cref="SeaBackdrop.SwayReach"/>).
        /// <para>
        /// With <paramref name="depth"/>, the pass of the same ground's depth alone, the blocks are drawn twice: their
        /// depth first, no colour written, then <paramref name="ground"/> on it with
        /// <see cref="DepthStencilState.DepthRead"/> (see the class; the depth state is put back as it was found, and
        /// it has to have been <see cref="DepthStencilState.Default"/>). Only an opaque ground may ask for it.
        /// </para>
        /// <para>
        /// Without it, and with <paramref name="open"/>, the pass of the ground without its cuts, a block
        /// <paramref name="cut"/> marks is drawn with <paramref name="ground"/> and every other block with
        /// <paramref name="open"/>, each as the rows come to it (<paramref name="cut"/> null marks none). Whichever
        /// pass drew last is left applied.
        /// </para>
        /// </summary>
        public static void DrawSeenBlocks(GraphicsDevice device, Grid grid, BoundingFrustum frustum, float originX, float originZ,
            float extent, float lowY, float highY, float sideways, EffectPass ground, EffectPass open = null, bool[] cut = null,
            EffectPass depth = null)
        {
            if (depth != null)
            {
                int all = SeenRuns(grid.N, grid.BlocksPerSide, grid.BlockStart, grid.BlockCount, frustum, originX, originZ, extent,
                    lowY, highY, sideways, null, grid.Runs);
                BlendState blend = device.BlendState;
                DepthStencilState depthState = device.DepthStencilState;

                device.BlendState = DEPTH_ONLY;
                depth.Apply();
                for (int i = 0; i < all; i += RUN_ENTRIES)
                    device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, grid.Runs[i], grid.Runs[i + 1] / 3);

                device.BlendState = blend;
                device.DepthStencilState = DepthStencilState.DepthRead;
                ground.Apply();
                for (int i = 0; i < all; i += RUN_ENTRIES)
                    device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, grid.Runs[i], grid.Runs[i + 1] / 3);

                device.DepthStencilState = depthState;
                return;
            }

            int entries = SeenRuns(grid.N, grid.BlocksPerSide, grid.BlockStart, grid.BlockCount, frustum, originX, originZ, extent,
                lowY, highY, sideways, open != null ? cut : null, grid.Runs);
            EffectPass applied = null;
            for (int i = 0; i < entries; i += RUN_ENTRIES)
            {
                EffectPass pass = open == null || grid.Runs[i + 2] != 0 ? ground : open;
                if (pass != applied)
                {
                    pass.Apply();
                    applied = pass;
                }

                device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, grid.Runs[i], grid.Runs[i + 1] / 3);
            }
        }

        /// <summary>How many entries of a run array one run takes: its first index, its index count, and 1 when its blocks are cut ones.</summary>
        internal const int RUN_ENTRIES = 3;

        /// <summary>
        /// The runs <see cref="DrawSeenBlocks"/> draws, into <paramref name="runs"/> in the rows' order,
        /// <see cref="RUN_ENTRIES"/> entries each; returns how many entries were written. A run is the seen blocks that
        /// follow each other in the buffer and are of one kind: marked in <paramref name="cut"/> or not (null marks none,
        /// and then a run ends only where a block is not seen). <paramref name="runs"/> needs
        /// <see cref="RunCapacity"/> entries. Static and device-free so the runs are testable.
        /// </summary>
        internal static int SeenRuns(int n, int blocksPerSide, int[] blockStart, int[] blockCount, BoundingFrustum frustum,
            float originX, float originZ, float extent, float lowY, float highY, float sideways, bool[] cut, int[] runs)
        {
            float cell = extent / (n - 1);
            float half = extent * Constants.HALF, span = BLOCK_QUADS * cell;

            int entries = 0, runStart = -1, runEnd = 0, runCut = 0;
            for (int bz = 0; bz < blocksPerSide; bz++)
                for (int bx = 0; bx < blocksPerSide; bx++)
                {
                    int b = bz * blocksPerSide + bx;
                    float x0 = originX - half + bx * span - sideways, z0 = originZ - half + bz * span - sideways;
                    float x1 = x0 + span + 2f * sideways, z1 = z0 + span + 2f * sideways;
                    var box = new BoundingBox(new Vector3(x0, lowY, z0), new Vector3(x1, highY, z1));
                    bool seen = frustum.Contains(box) != ContainmentType.Disjoint;
                    int cuts = cut != null && cut[b] ? 1 : 0;

                    if (runStart >= 0 && (!seen || cuts != runCut))
                    {
                        runs[entries++] = runStart;
                        runs[entries++] = runEnd - runStart;
                        runs[entries++] = runCut;
                        runStart = -1;
                    }

                    if (seen)
                    {
                        if (runStart < 0)
                        {
                            runStart = blockStart[b];
                            runCut = cuts;
                        }
                        runEnd = blockStart[b] + blockCount[b];
                    }
                }

            if (runStart >= 0)
            {
                runs[entries++] = runStart;
                runs[entries++] = runEnd - runStart;
                runs[entries++] = runCut;
            }

            return entries;
        }

        /// <summary>How many entries a run array of <see cref="SeenRuns"/> needs for a grid of <paramref name="blocks"/> blocks: every block a run of its own.</summary>
        internal static int RunCapacity(int blocks) => blocks * RUN_ENTRIES;

        /// <summary>
        /// Marks in <paramref name="cut"/> (an entry a block, as <see cref="Grid.BlockStart"/> is laid out) the blocks any of
        /// <paramref name="circles"/> reaches: each a world X and Z in <c>X</c> and <c>Y</c> and a radius in <c>Z</c>, the
        /// grid's origin snapped at <paramref name="originX"/>, <paramref name="originZ"/>. A block's square is taken half
        /// a cell wider on every side than its vertices stand, so a pixel at its very edge is never on the wrong side.
        /// </summary>
        internal static void MarkCutBlocks(int n, int blocksPerSide, float originX, float originZ, float extent,
            ReadOnlySpan<Vector3> circles, bool[] cut)
        {
            float cell = extent / (n - 1);
            float span = BLOCK_QUADS * cell, margin = cell * Constants.HALF;
            float lowX = originX - extent * Constants.HALF, lowZ = originZ - extent * Constants.HALF;

            Array.Clear(cut);
            for (int c = 0; c < circles.Length; c++)
            {
                float x = circles[c].X, z = circles[c].Y, reach = circles[c].Z;
                if (reach <= 0f) continue;

                int fromX = Math.Max(0, (int)MathF.Floor((x - reach - margin - lowX) / span));
                int toX = Math.Min(blocksPerSide - 1, (int)MathF.Floor((x + reach + margin - lowX) / span));
                int fromZ = Math.Max(0, (int)MathF.Floor((z - reach - margin - lowZ) / span));
                int toZ = Math.Min(blocksPerSide - 1, (int)MathF.Floor((z + reach + margin - lowZ) / span));

                for (int bz = fromZ; bz <= toZ; bz++)
                    for (int bx = fromX; bx <= toX; bx++)
                    {
                        float x0 = lowX + bx * span - margin, z0 = lowZ + bz * span - margin;
                        float dx = x - Math.Clamp(x, x0, x0 + span + 2f * margin);
                        float dz = z - Math.Clamp(z, z0, z0 + span + 2f * margin);
                        if (dx * dx + dz * dz <= reach * reach) cut[bz * blocksPerSide + bx] = true;
                    }
            }
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
                Runs = order == Order.Blocks ? new int[RunCapacity(side * side)] : null,
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

    /// <summary>
    /// Which blocks of a scene's camera grid its cuts reach (#1097, <see cref="TerrainGridCache"/>): the places its
    /// cutting program may <c>discard</c> a pixel at are one circle on the ground round the arena's centre, the island's
    /// hole or, where the scene wears the island's bank as its ground, the bank's cover, whichever reaches the further.
    /// Worked out again only when the grid, its snapped origin or the radius has moved, so a still lens pays nothing.
    /// <para>
    /// ⚠ Every <c>clip()</c> a cutting program holds has to lie inside that circle, or its Open twin draws ground
    /// where the cut was: a scene that cuts anywhere else (the outback's formations, the meadow's brook and pond)
    /// has no Open twin and draws its whole grid with the one program. The far ring's own cut (<c>FarRingClip</c>) is
    /// the exception that needs no circle: it acts only while the ring draws.
    /// </para>
    /// </summary>
    internal sealed class TerrainCuts
    {
        private readonly Vector3[] _circle = new Vector3[1];
        private bool[] _blocks = Array.Empty<bool>();
        private TerrainGridCache.Grid _grid;
        private float _originX, _originZ;
        private bool _known;

        /// <summary>
        /// The blocks of <paramref name="grid"/>, its origin snapped at the given point, that a circle of
        /// <paramref name="radius"/> round the arena's centre reaches: an entry a block.
        /// </summary>
        public bool[] Blocks(TerrainGridCache.Grid grid, float originX, float originZ, float extent, float radius)
        {
            if (_known && grid == _grid && originX == _originX && originZ == _originZ && radius == _circle[0].Z) return _blocks;

            int blocks = grid.BlocksPerSide * grid.BlocksPerSide;
            if (_blocks.Length != blocks) _blocks = new bool[blocks];
            _circle[0] = new Vector3(0f, 0f, radius);
            TerrainGridCache.MarkCutBlocks(grid.N, grid.BlocksPerSide, originX, originZ, extent, _circle, _blocks);

            _grid = grid;
            _originX = originX;
            _originZ = originZ;
            _known = true;
            return _blocks;
        }
    }
}
