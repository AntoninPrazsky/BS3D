using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The geometry the brook's groove and the pond's crater are drawn on (#892, <see cref="MeadowCarve"/>), as lists with no
    /// device so the tests can read them. Two meshes, both fixed to the world and built once, <c>Meadow.fx</c> lifting them:
    /// <list type="bullet">
    /// <item><b>The ground</b> (<see cref="Ground"/>): one grid in the brook's own coordinates - distance from the arena by
    /// lateral off the centreline (<see cref="MeadowPath.BrookPoint"/>) - so its columns follow the groove's walls exactly,
    /// <see cref="SIDE_STEP"/> apart, wherever the brook wanders. Its rows are <see cref="POND_ROW_STEP"/> apart over the
    /// pond, whose crater runs in every direction, and <see cref="BROOK_ROW_STEP"/> along the brook, whose walls run along
    /// the rows. It is kept in whole blocks of <see cref="LOD_BLOCK"/> by <see cref="LOD_BLOCK"/> quads, every block with a
    /// vertex within <see cref="MeadowCarve.POND_PATCH_RADIUS"/> of the pond's centre or <see cref="MeadowCarve.BROOK_PATCH_HALF"/>
    /// of the centreline: past the coarse grid's hole on every side, so the hole is always covered, and the vertex shader eases
    /// the patch onto the coarse grid's own triangles there. A block whose every vertex lies under the water by
    /// <see cref="BED_UNDER"/> is the bed and comes last (<c>BedStart</c>), drawn with a program that is the silt and its light
    /// alone, where the field's grass program worked out flowers, clumps and blades only to throw them away.</item>
    /// <item><b>The water</b> (<see cref="Water"/>): a strip down the brook and a disc over the pond, flat across, reaching a
    /// little under the banks so the depth test cuts its edge where the water meets the wall. Each vertex's Y says which
    /// water it is (0 the brook, 1 the pond); the shader sets the level.</item>
    /// </list>
    /// <para>
    /// <b>The ground coarsens with distance</b> (<see cref="LOD_LEVELS"/>), as the terrain of CDLOD does: a vertex carries where
    /// it stands in the brook's coordinates at its own spacing and at two, four and eight times it (<see cref="GroundMesh.Rows"/>,
    /// <see cref="GroundMesh.Sides"/>), and the vertex shader slides it towards the coarser places as the camera draws away,
    /// rows from <see cref="LOD_ROWS_FROM"/> and columns from <see cref="LOD_COLUMNS_FROM"/>, doubling the spacing every time
    /// the distance doubles. A slid vertex lands on another, so the triangles between them close to nothing and are never
    /// rasterized, and since every vertex is slid by its own place alone the mesh never opens. Rows slide towards the arena
    /// and columns towards the centreline, so the edges of a block - whole multiples of the coarsest spacing - never move,
    /// and nor does the patch's outline. Measured in the Game at High (3840 x 1581, Pennant): 0.19 ms off the intro's brook
    /// shot and 0.05 to 0.08 off play. The whole patch at its coarsest saved only 0.06 ms more at the brook's lens, so thin
    /// triangles are not where the rest of its cost goes (docs/scenes/meadow.md, "Costs a change must compare against").
    /// </para>
    /// </summary>
    internal static class MeadowPatchGeometry
    {
        /// <summary>The ground's column spacing, world units of lateral: a groove wall about a quarter of a unit across gets
        /// a vertex or two on it.</summary>
        public const float SIDE_STEP = 0.2f;

        /// <summary>The ground's row spacing over the pond and along the brook, world units.</summary>
        public const float POND_ROW_STEP = 0.25f, BROOK_ROW_STEP = 0.5f;

        /// <summary>How many times the ground's spacing doubles with distance, and the block it is kept in: the coarsest
        /// level's cell, so that a block's vertices all slide onto its corners.</summary>
        public const int LOD_LEVELS = 3, LOD_BLOCK = 1 << LOD_LEVELS;

        /// <summary>The distance at which the rows, and the columns, start to coarsen (<c>Meadow.fx</c>'s
        /// <c>LOD_ROWS_FROM</c>, <c>LOD_COLUMNS_FROM</c>): the spacing doubles at twice it, and so on. The rows go sooner:
        /// along the brook they run across the walls' line, and they are the ones a low lens sees foreshortened.</summary>
        public const float LOD_ROWS_FROM = 14f, LOD_COLUMNS_FROM = 22f;

        /// <summary>The water strip's columns either side of the centreline (under the walls wherever the water is widest)
        /// and their spacing, and the pond disc's spokes and rings and how far past the shore it reaches.</summary>
        public const float WATER_HALF = 2.25f, WATER_SIDE_STEP = 0.25f;
        public const int POND_SPOKES = 160, POND_RINGS = 24;
        public const float POND_WATER_PAST_SHORE = 0.4f;

        /// <summary>How far under its water a block's every vertex must lie for it to be the bed: past the depth at which
        /// the field's program has turned wholly to silt (<c>Meadow.fx</c>'s <c>under</c>), so the two programs agree. A
        /// whole block, because at the coarsest level its triangles span it.</summary>
        public const float BED_UNDER = 0.12f;

        /// <summary>How many block rows make one of the ground's draws (<see cref="GroundMesh.Chunks"/>): the draws go
        /// nearest first, so the early depth test throws away what a nearer bank hides.</summary>
        public const int CHUNK_BLOCK_ROWS = 4;

        /// <summary>The ground patch, as <see cref="Ground"/> builds it.</summary>
        public sealed class GroundMesh
        {
            /// <summary>Each vertex at its finest place, world XZ (Y zero).</summary>
            public Vector3[] Vertices;

            /// <summary>Each vertex's distance out along the brook (x) and at the coarser levels (y, z, w), and its lateral
            /// off the centreline likewise: <see cref="MeadowPath.BrookPoint"/> of a pair is where it stands at that level.</summary>
            public Vector4[] Rows, Sides;

            /// <summary>The triangles, wound clockwise from above: the field's first, by chunks, then from
            /// <see cref="BedStart"/> the bed's.</summary>
            public int[] Indices;
            public int BedStart;

            /// <summary>The field's draws: their first index, their index count and the world XZ the distance to them is
            /// taken from.</summary>
            public (int Start, int Count, Vector2 Centre)[] Chunks;

            /// <summary>Where vertex <paramref name="v"/> stands with its rows at <paramref name="rowLevel"/> and its columns at
            /// <paramref name="sideLevel"/> (0 the finest).</summary>
            public Vector2 At(int v, int rowLevel, int sideLevel, MeadowSceneConfig config)
            {
                (float x, float z) = MeadowPath.BrookPoint(Level(Rows[v], rowLevel), Level(Sides[v], sideLevel), config);
                return new Vector2(x, z);
            }

            private static float Level(Vector4 v, int level) => level switch { 0 => v.X, 1 => v.Y, 2 => v.Z, _ => v.W };
        }

        /// <summary>Builds the ground patch.</summary>
        public static GroundMesh Ground(MeadowSceneConfig config)
        {
            (float pondX, float pondZ) = MeadowPath.PondCentre(config);
            float pondD = MathF.Sqrt(pondX * pondX + pondZ * pondZ);
            float brookStart = config.ClearingRadius * MeadowPath.BROOK_START;
            float blockReach = LOD_BLOCK * MathF.Max(POND_ROW_STEP, SIDE_STEP) * 1.5f;

            //The rows: fine over the pond, coarser along the brook past it, a whole number of blocks
            var rows = new List<float>();
            float pondFrom = pondD - MeadowCarve.POND_PATCH_RADIUS - blockReach, pondTo = pondD + MeadowCarve.POND_PATCH_RADIUS + blockReach;
            for (float d = pondFrom; d < pondTo; d += POND_ROW_STEP) rows.Add(d);
            while (rows[^1] < MeadowCarve.BROOK_PATCH_END + BROOK_ROW_STEP || (rows.Count - 1) % LOD_BLOCK != 0)
                rows.Add(rows[^1] + BROOK_ROW_STEP);

            //The columns: wide enough for the pond, whose centre wanders off the brook's line across its rows, a whole number
            //of blocks either side of the centreline
            float wander = 0f;
            foreach (float d in rows)
                if (d <= pondTo) wander = MathF.Max(wander, MathF.Abs(SideOf(pondX, pondZ, d, config)));
            int half = (int)MathF.Ceiling((MeadowCarve.POND_PATCH_RADIUS + wander + blockReach) / SIDE_STEP);
            half = (half + LOD_BLOCK - 1) / LOD_BLOCK * LOD_BLOCK;
            int columns = 2 * half + 1;

            var at = new Vector2[rows.Count * columns];
            for (int i = 0; i < rows.Count; i++)
                for (int j = 0; j < columns; j++)
                {
                    (float x, float z) = MeadowPath.BrookPoint(rows[i], (j - half) * SIDE_STEP, config);
                    at[i * columns + j] = new Vector2(x, z);
                }

            //Where a vertex slides to: its row down to the level's spacing, its column towards the centreline, so the blocks'
            //edges - whole multiples of the coarsest spacing - never move, and nor does the patch's outline
            static int RowAt(int i, int level) => i - i % (1 << level);
            int SideAt(int j, int level)
            {
                int r = j - half, m = Math.Abs(r);
                return Math.Sign(r) * (m - m % (1 << level));
            }

            float pondKeep2 = MeadowCarve.POND_PATCH_RADIUS * MeadowCarve.POND_PATCH_RADIUS;
            float pondCarve2 = MeadowCarve.POND_CARVE_RADIUS * MeadowCarve.POND_CARVE_RADIUS;
            bool Keep(int i, int j)
            {
                Vector2 p = at[i * columns + j];
                float dx = p.X - pondX, dz = p.Y - pondZ;
                if (dx * dx + dz * dz < pondKeep2) return true;
                return rows[i] >= brookStart && rows[i] <= MeadowCarve.BROOK_PATCH_END
                    && MathF.Abs((j - half) * SIDE_STEP) <= MeadowCarve.BROOK_PATCH_HALF;
            }

            //How far under its water a vertex is: only inside the cut's zone is there any water at all. Each vertex once.
            var under = new float[at.Length];
            Array.Fill(under, float.NaN);
            float Under(int i, int j)
            {
                int v = i * columns + j;
                if (!float.IsNaN(under[v])) return under[v];
                Vector2 p = at[v];
                float dx = p.X - pondX, dz = p.Y - pondZ;
                if (MathF.Abs((j - half) * SIDE_STEP) >= MeadowCarve.BROOK_CARVE_HALF && dx * dx + dz * dz >= pondCarve2) return under[v] = float.MinValue;
                float h = MeadowCarve.Height(p.X, p.Y, config, out float water);
                return under[v] = water - h;
            }

            var remap = new int[at.Length];
            Array.Fill(remap, -1);
            var vertices = new List<Vector3>();
            var rowsAt = new List<Vector4>();
            var sidesAt = new List<Vector4>();
            int Use(int i, int j)
            {
                int v = i * columns + j;
                if (remap[v] < 0)
                {
                    remap[v] = vertices.Count;
                    vertices.Add(new Vector3(at[v].X, 0f, at[v].Y));
                    rowsAt.Add(new Vector4(rows[RowAt(i, 0)], rows[RowAt(i, 1)], rows[RowAt(i, 2)], rows[RowAt(i, 3)]));
                    sidesAt.Add(new Vector4(SideAt(j, 0), SideAt(j, 1), SideAt(j, 2), SideAt(j, 3)) * SIDE_STEP);
                }
                return remap[v];
            }

            var indices = new List<int>();
            var bed = new List<int>();
            var chunks = new List<(int, int, Vector2)>();
            int blockRows = (rows.Count - 1) / LOD_BLOCK, blockColumns = (columns - 1) / LOD_BLOCK;
            int chunkStart = 0, chunkFirstRow = 0;
            for (int bi = 0; bi < blockRows; bi++)
            {
                for (int bj = 0; bj < blockColumns; bj++)
                {
                    int i0 = bi * LOD_BLOCK, j0 = bj * LOD_BLOCK;
                    bool keep = false;
                    for (int i = i0; i <= i0 + LOD_BLOCK && !keep; i++)
                        for (int j = j0; j <= j0 + LOD_BLOCK && !keep; j++)
                            keep = Keep(i, j);
                    if (!keep) continue;

                    bool underAll = true;
                    for (int i = i0; i <= i0 + LOD_BLOCK && underAll; i++)
                        for (int j = j0; j <= j0 + LOD_BLOCK && underAll; j++)
                            underAll = Under(i, j) >= BED_UNDER;

                    //Rows run outward and columns towards +side, which turns clockwise round the arena seen from above
                    //(atan2(z, x) grows from +X towards +Z): a, c, b is clockwise from above, the front face under the default
                    //cull (CLAUDE.md, "Triangle winding"). Drawn without culling, as the coarse grid is, all the same.
                    List<int> into = underAll ? bed : indices;
                    for (int i = i0; i < i0 + LOD_BLOCK; i++)
                        for (int j = j0; j < j0 + LOD_BLOCK; j++)
                        {
                            int a = Use(i, j), b = Use(i, j + 1), c = Use(i + 1, j), d = Use(i + 1, j + 1);
                            into.Add(a); into.Add(c); into.Add(b);
                            into.Add(b); into.Add(c); into.Add(d);
                        }
                }

                //A chunk of the field every few block rows, its distance taken from the centreline halfway along it
                if ((bi + 1) % CHUNK_BLOCK_ROWS == 0 || bi == blockRows - 1)
                {
                    if (indices.Count > chunkStart)
                    {
                        float middle = rows[(chunkFirstRow + (bi + 1) * LOD_BLOCK) / 2];
                        (float cx, float cz) = MeadowPath.BrookPoint(MathF.Max(middle, brookStart), 0f, config);
                        chunks.Add((chunkStart, indices.Count - chunkStart, new Vector2(cx, cz)));
                    }
                    chunkStart = indices.Count;
                    chunkFirstRow = (bi + 1) * LOD_BLOCK;
                }
            }

            int bedStart = indices.Count;
            indices.AddRange(bed);
            return new GroundMesh
            {
                Vertices = vertices.ToArray(),
                Rows = rowsAt.ToArray(),
                Sides = sidesAt.ToArray(),
                Indices = indices.ToArray(),
                BedStart = bedStart,
                Chunks = chunks.ToArray(),
            };
        }

        /// <summary>The water's vertices (world XZ, Y 0 for the brook and 1 for the pond) and its triangles.</summary>
        public static (Vector3[] Vertices, int[] Indices) Water(MeadowSceneConfig config)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();

            //The brook: from its start, under the pond's water, to where the groove has shallowed away
            float brookStart = config.ClearingRadius * MeadowPath.BROOK_START;
            int columns = 2 * (int)MathF.Round(WATER_HALF / WATER_SIDE_STEP) + 1;
            int rows = 0;
            for (float d = brookStart; d < MeadowCarve.BROOK_CARVE_END + BROOK_ROW_STEP; d += BROOK_ROW_STEP, rows++)
                for (int j = 0; j < columns; j++)
                {
                    (float x, float z) = MeadowPath.BrookPoint(d, -WATER_HALF + j * WATER_SIDE_STEP, config);
                    vertices.Add(new Vector3(x, 0f, z));
                }
            for (int i = 0; i < rows - 1; i++)
                for (int j = 0; j < columns - 1; j++)
                {
                    int a = i * columns + j, b = a + 1, c = a + columns, d = c + 1;
                    indices.Add(a); indices.Add(c); indices.Add(b);
                    indices.Add(b); indices.Add(c); indices.Add(d);
                }

            //The pond: rings out to a little past the shore, its own outline, so the disc's edge is under the earth step
            (float pondX, float pondZ) = MeadowPath.PondCentre(config);
            int first = vertices.Count;
            for (int ring = 0; ring <= POND_RINGS; ring++)
                for (int spoke = 0; spoke < POND_SPOKES; spoke++)
                {
                    float a = MathHelper.TwoPi * spoke / POND_SPOKES;
                    float r = (config.PondRadius * MeadowPath.PondOutline(a) + POND_WATER_PAST_SHORE) * ring / POND_RINGS;
                    vertices.Add(new Vector3(pondX + MathF.Cos(a) * r, 1f, pondZ + MathF.Sin(a) * r));
                }
            for (int ring = 0; ring < POND_RINGS; ring++)
                for (int spoke = 0; spoke < POND_SPOKES; spoke++)
                {
                    int next = (spoke + 1) % POND_SPOKES;
                    int a = first + ring * POND_SPOKES + spoke, b = first + ring * POND_SPOKES + next;
                    int c = a + POND_SPOKES, d = b + POND_SPOKES;
                    //Rings run outward and spokes turn as the ground's columns do: the ground's a, c, b
                    indices.Add(a); indices.Add(c); indices.Add(b);
                    indices.Add(b); indices.Add(c); indices.Add(d);
                }

            return (vertices.ToArray(), indices.ToArray());
        }

        /// <summary>Where (x, z) stands across the brook's line at distance <paramref name="d"/>: the side
        /// <see cref="MeadowPath.BrookPoint"/> would put a point on that circle at the same bearing.</summary>
        private static float SideOf(float x, float z, float d, MeadowSceneConfig config)
        {
            float angle = MathF.Atan2(z, x) - config.BrookBearing;
            angle = MathF.IEEERemainder(angle, MathF.PI * 2f);
            return d * angle - MeadowPath.BrookWander(d, config);
        }
    }

    /// <summary>
    /// The two meshes of <see cref="MeadowPatchGeometry"/> on the device (#892): built once with the meadow, drawn by
    /// <see cref="MeadowBackdrop"/> through its own effect - the ground after the coarse grid, nearest chunk first, the water
    /// with the translucent things that stand on the ground.
    /// </summary>
    internal sealed class MeadowPatch : IDisposable
    {
        /// <summary>A ground vertex as <c>Meadow.fx</c>'s <c>MeadowCarvedVS</c> takes it: its rows and its sides at every level.</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct GroundVertex : IVertexType
        {
            public Vector4 Rows, Sides;

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.Position, 0),
                new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0));

            VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }


        private readonly VertexBuffer _groundVertices, _waterVertices;
        private readonly IndexBuffer _groundIndices, _waterIndices;
        private readonly int _groundTriangles, _waterTriangles, _bedStart;

        //The field's chunks and this frame's order of them, nearest first: filled in place, no allocation a frame
        private readonly (int Start, int Count, Vector2 Centre)[] _chunks;
        private readonly int[] _order;
        private readonly float[] _distance;

        public MeadowPatch(GraphicsDevice device, MeadowSceneConfig config)
        {
            MeadowPatchGeometry.GroundMesh ground = MeadowPatchGeometry.Ground(config);
            var vertices = new GroundVertex[ground.Vertices.Length];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = new GroundVertex { Rows = ground.Rows[i], Sides = ground.Sides[i] };
            _groundVertices = new VertexBuffer(device, GroundVertex.Declaration, vertices.Length, BufferUsage.WriteOnly);
            _groundVertices.SetData(vertices);
            _groundIndices = Indices(device, ground.Indices);
            _groundTriangles = ground.Indices.Length / 3;
            _bedStart = ground.BedStart;
            _chunks = ground.Chunks;
            _order = new int[_chunks.Length];
            _distance = new float[_chunks.Length];

            (Vector3[] waterVertices, int[] waterIndices) = MeadowPatchGeometry.Water(config);
            var water = new VertexPosition[waterVertices.Length];
            for (int i = 0; i < water.Length; i++) water[i] = new VertexPosition(waterVertices[i]);
            _waterVertices = new VertexBuffer(device, VertexPosition.VertexDeclaration, water.Length, BufferUsage.WriteOnly);
            _waterVertices.SetData(water);
            _waterIndices = Indices(device, waterIndices);
            _waterTriangles = waterIndices.Length / 3;
        }

        private static IndexBuffer Indices(GraphicsDevice device, int[] indices)
        {
            var buffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            buffer.SetData(indices);
            return buffer;
        }

        /// <summary>Draws the ground patch: its field with <paramref name="field"/>, chunk by chunk from the one nearest
        /// <paramref name="camera"/>, then its bed under the water with <paramref name="bed"/>.</summary>
        public void DrawGround(GraphicsDevice device, EffectPass field, EffectPass bed, Vector3 camera)
        {
            //Nearest first (an insertion sort of a few dozen, in place)
            var eye = new Vector2(camera.X, camera.Z);
            for (int k = 0; k < _chunks.Length; k++)
            {
                float distance = Vector2.DistanceSquared(eye, _chunks[k].Centre);
                int at = k;
                while (at > 0 && _distance[at - 1] > distance)
                {
                    _distance[at] = _distance[at - 1];
                    _order[at] = _order[at - 1];
                    at--;
                }
                _distance[at] = distance;
                _order[at] = k;
            }

            device.SetVertexBuffer(_groundVertices);
            device.Indices = _groundIndices;
            field.Apply();
            for (int k = 0; k < _order.Length; k++)
            {
                (int start, int count, _) = _chunks[_order[k]];
                device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, start, count / 3);
            }
            if (_groundTriangles > _bedStart / 3)
            {
                bed.Apply();
                device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, _bedStart, _groundTriangles - _bedStart / 3);
            }
        }

        /// <summary>Draws the water with whatever pass the caller has applied.</summary>
        public void DrawWater(GraphicsDevice device)
        {
            device.SetVertexBuffer(_waterVertices);
            device.Indices = _waterIndices;
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _waterTriangles);
        }

        public void Dispose()
        {
            _groundVertices.Dispose();
            _groundIndices.Dispose();
            _waterVertices.Dispose();
            _waterIndices.Dispose();
        }
    }
}
