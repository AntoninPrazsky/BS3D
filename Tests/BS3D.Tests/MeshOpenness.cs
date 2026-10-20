using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

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

        /// <summary>
        /// The open rings of a wood built of overlapping solids (#938): a limb starts inside its bough and a bough inside the
        /// trunk, so each solid has an open end and no edge count can tell a ring buried in its parent from a hole. The edges
        /// of the surface that one triangle uses are chained into loops, and a loop is <b>exposed</b> unless every vertex of it,
        /// drawn a tenth of the way in to its own centre (a ring lying on its parent's skin passes), lies inside another
        /// separate solid: a point is inside one when most of three skew rays out of it cross that solid's triangles an odd
        /// number of times. The solids are the surface's edge-connected pieces; the one a loop belongs to is never asked.
        /// <paramref name="belowY"/> excuses a loop whose every vertex lies at or under it: the foot ring under the ground.
        /// </summary>
        public static List<OpenLoop> ExposedLoops(IReadOnlyList<VertexPositionNormalTexture> vertices, IReadOnlyList<short> indices,
            float belowY = float.NegativeInfinity) => ExposedLoops(vertices, indices.Count, i => (ushort)indices[i], belowY);

        /// <summary><see cref="ExposedLoops(IReadOnlyList{VertexPositionNormalTexture}, IReadOnlyList{short}, float)"/> for 32-bit indices.</summary>
        public static List<OpenLoop> ExposedLoops(IReadOnlyList<VertexPositionNormalTexture> vertices, IReadOnlyList<int> indices,
            float belowY = float.NegativeInfinity) => ExposedLoops(vertices, indices.Count, i => indices[i], belowY);

        /// <summary>One loop of open edges: its vertices and their mean.</summary>
        public sealed record OpenLoop(IReadOnlyList<Vector3> Points, Vector3 Centre);

        private static List<OpenLoop> ExposedLoops(IReadOnlyList<VertexPositionNormalTexture> vertices, int indexCount, Func<int, int> index, float belowY)
        {
            (List<Vector3> points, int[] ids) = Weld(vertices);
            int triangles = indexCount / 3;
            var tri = new int[triangles][];
            for (int t = 0; t < triangles; t++) tri[t] = new[] { ids[index(3 * t)], ids[index(3 * t + 1)], ids[index(3 * t + 2)] };

            //Edge use, and the pieces: triangles that share an edge are one solid
            var parent = Enumerable.Range(0, triangles).ToArray();
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            var edgeUse = new Dictionary<(int, int), (int Count, int Triangle)>();
            for (int t = 0; t < triangles; t++)
                for (int e = 0; e < 3; e++)
                {
                    int a = tri[t][e], b = tri[t][(e + 1) % 3];
                    if (a == b) continue;
                    var key = a < b ? (a, b) : (b, a);
                    if (edgeUse.TryGetValue(key, out var seen))
                    {
                        edgeUse[key] = (seen.Count + 1, seen.Triangle);
                        parent[Find(t)] = Find(seen.Triangle);
                    }
                    else edgeUse[key] = (1, t);
                }

            //The open edges into loops: edges that meet at a vertex are one loop
            var loopParent = new Dictionary<int, int>();
            int LoopFind(int x) { while (loopParent[x] != x) x = loopParent[x] = loopParent[loopParent[x]]; return x; }
            var openEdges = new List<(int A, int B, int Piece)>();
            foreach (var entry in edgeUse)
                if (entry.Value.Count == 1) openEdges.Add((entry.Key.Item1, entry.Key.Item2, Find(entry.Value.Triangle)));
            foreach ((int a, int b, _) in openEdges)
            {
                if (!loopParent.ContainsKey(a)) loopParent[a] = a;
                if (!loopParent.ContainsKey(b)) loopParent[b] = b;
                loopParent[LoopFind(a)] = LoopFind(b);
            }
            var loops = new Dictionary<int, (HashSet<int> Vertices, int Piece)>();
            foreach ((int a, int b, int piece) in openEdges)
            {
                int root = LoopFind(a);
                if (!loops.TryGetValue(root, out var loop)) loops[root] = loop = (new HashSet<int>(), piece);
                loop.Vertices.Add(a);
                loop.Vertices.Add(b);
            }

            //Each piece's triangles and box, for the rays
            var pieces = new Dictionary<int, List<int>>();
            for (int t = 0; t < triangles; t++)
            {
                int piece = Find(t);
                if (!pieces.TryGetValue(piece, out List<int> list)) pieces[piece] = list = new List<int>();
                list.Add(t);
            }
            var boxes = new Dictionary<int, (Vector3 Min, Vector3 Max)>();
            foreach ((int piece, List<int> list) in pieces)
            {
                Vector3 min = new(float.MaxValue), max = new(float.MinValue);
                foreach (int t in list)
                    foreach (int v in tri[t]) { min = Vector3.Min(min, points[v]); max = Vector3.Max(max, points[v]); }
                boxes[piece] = (min, max);
            }

            var exposed = new List<OpenLoop>();
            foreach ((HashSet<int> members, int own) in loops.Values)
            {
                Vector3 centre = Vector3.Zero;
                foreach (int v in members) centre += points[v];
                centre /= members.Count;
                if (members.All(v => points[v].Y <= belowY)) continue;

                bool buried = true;
                foreach (int v in members)
                {
                    Vector3 p = centre + (points[v] - centre) * 0.9f;
                    bool inside = false;
                    foreach ((int piece, List<int> list) in pieces)
                    {
                        if (piece == own) continue;
                        (Vector3 min, Vector3 max) = boxes[piece];
                        if (p.X < min.X || p.Y < min.Y || p.Z < min.Z || p.X > max.X || p.Y > max.Y || p.Z > max.Z) continue;
                        if (InsideSolid(p, list, tri, points)) { inside = true; break; }
                    }
                    if (!inside) { buried = false; break; }
                }
                if (!buried) exposed.Add(new OpenLoop(members.Select(v => points[v]).ToList(), centre));
            }
            return exposed;
        }

        //Three skew directions: no edge of a lattice-built mesh lies along one
        private static readonly Vector3[] RAYS =
        {
            Vector3.Normalize(new Vector3(0.5773f, 0.3171f, 0.7524f)),
            Vector3.Normalize(new Vector3(-0.2917f, 0.8203f, 0.4931f)),
            Vector3.Normalize(new Vector3(0.1623f, -0.4419f, 0.8821f)),
        };

        private static bool InsideSolid(Vector3 p, List<int> list, int[][] tri, List<Vector3> points)
        {
            int odd = 0;
            foreach (Vector3 d in RAYS)
            {
                int hits = 0;
                foreach (int t in list)
                    if (RayHits(p, d, points[tri[t][0]], points[tri[t][1]], points[tri[t][2]])) hits++;
                if ((hits & 1) == 1) odd++;
            }
            return odd >= 2;
        }

        /// <summary>Moeller-Trumbore, a hit only in front of the origin.</summary>
        private static bool RayHits(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 e1 = b - a, e2 = c - a;
            Vector3 h = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, h);
            if (MathF.Abs(det) < 1e-12f) return false;
            float f = 1f / det;
            Vector3 s = o - a;
            float u = f * Vector3.Dot(s, h);
            if (u < 0f || u > 1f) return false;
            Vector3 q = Vector3.Cross(s, e1);
            float v = f * Vector3.Dot(d, q);
            if (v < 0f || u + v > 1f) return false;
            return f * Vector3.Dot(e2, q) > 1e-6f;
        }

        private static (List<Vector3> Points, int[] Ids) Weld(IReadOnlyList<VertexPositionNormalTexture> vertices)
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
            return (points, ids);
        }

        private static (List<(Vector3 A, Vector3 B)> Open, int NonManifold) Edges(IReadOnlyList<VertexPositionNormalTexture> vertices, int indexCount,
            Func<int, int> index)
        {
            (_, int[] ids) = Weld(vertices);

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
