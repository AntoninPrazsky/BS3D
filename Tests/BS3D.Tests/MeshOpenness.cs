using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace BS3D.Tests
{
    /// <summary>
    /// Whether a procedural mesh is closed (#889, #943): the check a winding test cannot make. Under back-face culling a
    /// hole in a solid shows what is behind it, through the wall and out the far side, and nothing but a count of the
    /// triangles each edge belongs to finds one.
    /// </summary>
    public static class MeshOpenness
    {
        /// <summary>Welded within this whichever side of a grid line two copies of a point fall: a rounded key alone split a
        /// knuckle's tip, whose copies differ in the eighth decimal, into two points and called its triangles open.</summary>
        private const float WELD = 1e-4f;

        /// <summary>The edges (of welded positions, normals and UVs ignored) that exactly one triangle uses: the boundary of
        /// the surface. A closed solid has none.</summary>
        public static List<(Vector3 A, Vector3 B)> OpenEdges(IReadOnlyList<VertexPositionNormalTexture> vertices, IReadOnlyList<short> indices) =>
            Edges(vertices, indices.Count, i => (ushort)indices[i]).Open;

        /// <summary><see cref="OpenEdges(IReadOnlyList{VertexPositionNormalTexture}, IReadOnlyList{short})"/> for a mesh with 32-bit indices.</summary>
        public static List<(Vector3 A, Vector3 B)> OpenEdges(IReadOnlyList<VertexPositionNormalTexture> vertices, IReadOnlyList<int> indices) =>
            Edges(vertices, indices.Count, i => indices[i]).Open;

        /// <summary>The edges that three or more triangles use or that two triangles use the same way round (a fold, or a
        /// surface wound against itself): a closed, consistently wound solid has none.</summary>
        public static int NonManifoldEdges(IReadOnlyList<VertexPositionNormalTexture> vertices, IReadOnlyList<short> indices) =>
            Edges(vertices, indices.Count, i => (ushort)indices[i]).NonManifold;

        private static (List<(Vector3 A, Vector3 B)> Open, int NonManifold) Edges(IReadOnlyList<VertexPositionNormalTexture> vertices, int indexCount,
            Func<int, int> index)
        {
            var cells = new Dictionary<(int, int, int), List<int>>();
            var points = new List<Vector3>();
            var ids = new int[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p = vertices[i].Position;
                var cell = ((int)MathF.Floor(p.X / WELD), (int)MathF.Floor(p.Y / WELD), (int)MathF.Floor(p.Z / WELD));
                int id = -1;
                for (int dx = -1; dx <= 1 && id < 0; dx++)
                    for (int dy = -1; dy <= 1 && id < 0; dy++)
                        for (int dz = -1; dz <= 1 && id < 0; dz++)
                            if (cells.TryGetValue((cell.Item1 + dx, cell.Item2 + dy, cell.Item3 + dz), out List<int> near))
                                foreach (int other in near)
                                    if (Vector3.DistanceSquared(points[other], p) <= WELD * WELD) { id = other; break; }
                if (id < 0)
                {
                    id = points.Count;
                    points.Add(p);
                    if (!cells.TryGetValue(cell, out List<int> list)) cells[cell] = list = new List<int>();
                    list.Add(id);
                }
                ids[i] = id;
            }

            //Per undirected edge: how many triangles use it, how many run it from the lower id to the higher, and where it is
            var edges = new Dictionary<(int, int), (int Count, int Forward, Vector3 A, Vector3 B)>();
            for (int t = 0; t + 2 < indexCount; t += 3)
            {
                for (int e = 0; e < 3; e++)
                {
                    int i = index(t + e), j = index(t + (e + 1) % 3);
                    int a = ids[i], b = ids[j];
                    if (a == b) continue;
                    bool forward = a < b;
                    var key = forward ? (a, b) : (b, a);
                    edges[key] = edges.TryGetValue(key, out var seen)
                        ? (seen.Count + 1, seen.Forward + (forward ? 1 : 0), seen.A, seen.B)
                        : (1, forward ? 1 : 0, vertices[i].Position, vertices[j].Position);
                }
            }

            var open = new List<(Vector3, Vector3)>();
            int nonManifold = 0;
            foreach ((int count, int forward, Vector3 a, Vector3 b) in edges.Values)
            {
                if (count == 1) open.Add((a, b));
                else if (count != 2 || forward != 1) nonManifold++;
            }
            return (open, nonManifold);
        }
    }
}
