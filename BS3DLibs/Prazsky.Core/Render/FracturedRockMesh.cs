using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A broken rock as High and Ultra draw it (#777): a few big fracture faces meeting at worn edges, where Low and Medium
    /// keep <see cref="VolcanoPlanting.BlockMesh"/>'s twenty flat facets. The owner, of the Moon on High and Ultra: "I still
    /// see triangular rocks on the ground. That is a bug" - a squashed icosahedron shaded flat is one tone per face, twenty
    /// faces of one size, and under a low sun it reads as a cut crystal. The klein references
    /// (<c>C:\Users\panrd\AI\sd\out\777-klein</c>) are a block of few big planes, rounded at the edges, chipped, and a
    /// boulder rounded all over; neither has a face of one flat tone, which is the shader's half (<c>Acacia.fx</c>'s stone).
    /// <para>
    /// <b>How it is cut.</b> A subdivided icosahedron, every vertex pushed out along its direction to the point where that
    /// ray leaves a solid bounded by a handful of rolled planes, the fracture faces, with the corners that stand out
    /// furthest chipped by a few small planes more. The planes are met with a SOFT minimum (<see cref="BLOCK_BEVEL"/>), so an
    /// edge is a narrow rounded arris rather than a crease, and every face carries a slow undulation (a fracture face is
    /// conchoidal, never flat). ⚠ <b>Each normal is the surface's own</b>, taken across two small steps over the sphere: averaged
    /// over the triangles round a vertex, an edge turned over two rows of triangles and every block came out a soft pillow.
    /// Each ray is pushed out along its own direction, so the surface stays the one closed, consistently wound skin the
    /// sphere was: no seam, no open end (<c>WindingCheck --openness</c>).
    /// </para>
    /// <para>
    /// <b>The two kinds</b>, by the mesh each stands in for: a <i>block</i> fits <see cref="VolcanoPlanting.BlockMesh"/>'s
    /// frame (about 0.45 out from a centre 0.18 over the origin, squashed in Y), so it seats where the coarse block did off
    /// the same instance; a <i>boulder</i> fits a <see cref="RockMesh"/> of radius 1 and the given height on the origin,
    /// fewer faces and a wide rounding, the rock the regolith has worn.
    /// </para>
    /// </summary>
    internal static class FracturedRockMesh
    {
        /// <summary>The block's mean surface radius and its centre's height over the origin: the coarse block's
        /// (<see cref="VolcanoPlanting.BlockMesh"/>), whose corners stand 0.31 to 0.585 out and whose faces lie inside them.</summary>
        private const float BLOCK_RADIUS = 0.42f, BLOCK_CENTRE_Y = 0.18f;

        /// <summary>How soft a block's edges are, in units of its radius: a narrow worn arris, about a triangle wide at the
        /// subdivision a big block is drawn at. A boulder's (<see cref="BOULDER_BEVEL"/>) is six times wider.</summary>
        private const float BLOCK_BEVEL = 0.025f, BOULDER_BEVEL = 0.15f;

        /// <summary>
        /// The block or the boulder as built: vertices and indices, on no device (the tests read them). <paramref name="seed"/>
        /// decides the shape and <paramref name="subdivisions"/> only how finely it is sampled, so one seed at two
        /// subdivisions is the same rock at two resolutions.
        /// </summary>
        /// <param name="boulderHeight">0 for a block; a boulder's height over its base (a <see cref="RockMesh"/>'s).</param>
        internal static (List<VertexPositionNormalTexture> Vertices, List<short> Indices) Build(int seed, int subdivisions, float boulderHeight = 0f)
        {
            bool boulder = boulderHeight > 0f;
            var rng = new Random(seed);

            //--- The fracture planes: a block has nine to twelve, a boulder six to eight, spread over the sphere so no two
            //are near one face (two planes a few degrees apart make a sliver of a face, which reads as a facet again)
            int planeCount = boulder ? 6 + rng.Next(3) : 9 + rng.Next(4);
            float minCos = MathF.Cos(MathHelper.ToRadians(boulder ? 48f : 32f));
            var normals = new List<Vector3>(planeCount + CHIPS_MAX);
            var distances = new List<float>(planeCount + CHIPS_MAX);
            for (int tries = 0; normals.Count < planeCount && tries < 2000; tries++)
            {
                Vector3 n = RandomDirection(rng);
                bool apart = true;
                foreach (Vector3 other in normals)
                    if (Vector3.Dot(n, other) > minCos) { apart = false; break; }
                if (!apart) continue;
                normals.Add(n);
                //Some cut deep and some shallow: the deep ones are the big faces, the shallow ones the narrow ones between
                distances.Add(boulder ? 0.86f + 0.14f * (float)rng.NextDouble() : 0.6f + 0.4f * (float)rng.NextDouble());
            }

            float bevel = boulder ? BOULDER_BEVEL : BLOCK_BEVEL;
            float envelope = boulder ? 1.12f : 1.5f;

            //--- Chips: the corners that stand out furthest lose a flake each (the references' broken corners), a small
            //plane a little inside the corner, so a block is never all clean planes meeting at points
            (List<Vector3> probe, _) = Icosphere(2);
            var reaches = new Dictionary<Vector3, float>();
            foreach (Vector3 d in probe) reaches[d] = SoftExit(d, normals, distances, envelope, bevel);
            probe.Sort((a, b) => reaches[b].CompareTo(reaches[a]));
            int chips = boulder ? 0 : 2 + rng.Next(CHIPS_MAX - 1);
            var chipped = new List<Vector3>();
            foreach (Vector3 corner in probe)
            {
                if (chipped.Count == chips) break;
                bool apart = true;
                foreach (Vector3 other in chipped)
                    if (Vector3.Dot(corner, other) > CHIP_APART) { apart = false; break; }
                if (!apart) continue;
                chipped.Add(corner);
                normals.Add(Vector3.Normalize(corner + RandomDirection(rng) * 0.25f));
                distances.Add(reaches[corner] * (0.84f + 0.08f * (float)rng.NextDouble()));
            }

            float undulation = boulder ? 0.06f : 0.05f;
            Vector3 offset = new((float)rng.NextDouble() * 50f, (float)rng.NextDouble() * 50f, (float)rng.NextDouble() * 50f);
            float squash = boulder ? 1f : 0.55f + 0.3f * (float)rng.NextDouble();

            //Where the surface stands along a direction: the planes' soft exit, undulated (a fracture face is conchoidal)
            Vector3 Surface(Vector3 d) =>
                d * (SoftExit(d, normals, distances, envelope, bevel) * (1f + undulation * CavernNoise.Fbm(d * 1.6f + offset, 2)));

            //--- Into the frame of the mesh it stands in for, measured on one fixed sampling, so a rock built at two
            //densities is one rock
            (List<Vector3> frame, _) = Icosphere(3);
            float meanRadius = 0f, meanFlat = 0f, minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector3 d in frame)
            {
                Vector3 p = Surface(d);
                meanRadius += p.Length();
                meanFlat += MathF.Sqrt(p.X * p.X + p.Z * p.Z);
                minY = MathF.Min(minY, p.Y);
                maxY = MathF.Max(maxY, p.Y);
            }
            meanRadius /= frame.Count;
            meanFlat /= frame.Count;

            //A block scales evenly and squashes Y; a boulder stands about radius 1 across the ground like the lathe, its base
            //a little under the origin and its crown at the height (a sphere's mean distance from its axis is pi/4 of its
            //radius, so 0.8 over the mean is about 1 at the equator)
            Vector3 scale = boulder
                ? new Vector3(0.8f / meanFlat, 1.12f * boulderHeight / (maxY - minY), 0.8f / meanFlat)
                : new Vector3(BLOCK_RADIUS / meanRadius, BLOCK_RADIUS / meanRadius * squash, BLOCK_RADIUS / meanRadius);
            Vector3 shift = boulder ? new Vector3(0f, -0.12f * boulderHeight - minY * scale.Y, 0f) : new Vector3(0f, BLOCK_CENTRE_Y, 0f);

            //--- The skin: the sphere's points pushed out to the surface, each normal the SURFACE's own, taken across two small
            //steps over the sphere and not averaged over the triangles round it. Averaged, an edge turned over two rows of
            //triangles and every block came out a soft pillow; taken off the surface, a face is flat to its edge and the edge
            //turns over the bevel, wherever the triangles happen to lie
            (List<Vector3> points, List<int> triangles) = Icosphere(subdivisions);
            var vertices = new List<VertexPositionNormalTexture>(points.Count);
            foreach (Vector3 d in points)
            {
                Vector3 side = Vector3.Normalize(Vector3.Cross(d, MathF.Abs(d.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX));
                Vector3 up = Vector3.Cross(d, side);
                Vector3 alongSide = Surface(Vector3.Normalize(d + side * NORMAL_STEP)) - Surface(Vector3.Normalize(d - side * NORMAL_STEP));
                Vector3 alongUp = Surface(Vector3.Normalize(d + up * NORMAL_STEP)) - Surface(Vector3.Normalize(d - up * NORMAL_STEP));
                Vector3 normal = Vector3.Cross(alongSide, alongUp);
                if (Vector3.Dot(normal, d) < 0f) normal = -normal;
                //A scale turns a normal by its inverse
                normal = Vector3.Normalize(new Vector3(normal.X / scale.X, normal.Y / scale.Y, normal.Z / scale.Z));
                vertices.Add(new VertexPositionNormalTexture(Surface(d) * scale + shift, normal, Vector2.Zero));
            }

            //The sphere is wound one way throughout; turn it to this renderer's front face (CLAUDE.md, "Triangle winding":
            //clockwise from outside, so (b - a) x (c - a) points INWARD) by the sign of the volume it encloses
            float volume = 0f;
            for (int t = 0; t < triangles.Count; t += 3)
                volume += Vector3.Dot(points[triangles[t]], Vector3.Cross(points[triangles[t + 1]], points[triangles[t + 2]]));
            bool flip = volume > 0f;

            var indices = new List<short>(triangles.Count);
            for (int t = 0; t < triangles.Count; t += 3)
            {
                indices.Add((short)triangles[t]);
                indices.Add((short)(flip ? triangles[t + 2] : triangles[t + 1]));
                indices.Add((short)(flip ? triangles[t + 1] : triangles[t + 2]));
            }
            return (vertices, indices);
        }

        /// <summary>How many corners a block may have chipped, and how far apart two chips stand (a cosine).</summary>
        private const int CHIPS_MAX = 4;
        private const float CHIP_APART = 0.7f;

        /// <summary>The step over the unit sphere a vertex's normal is taken across: far under a triangle, well over float noise.</summary>
        private const float NORMAL_STEP = 2e-3f;

        /// <summary><see cref="Build(int, int, float)"/> uploaded, with the bounding sphere round it.</summary>
        internal static UploadedMesh Upload(GraphicsDevice device, int seed, int subdivisions, float boulderHeight = 0f)
        {
            (List<VertexPositionNormalTexture> v, List<short> idx) = Build(seed, subdivisions, boulderHeight);
            Vector3 centre = boulderHeight > 0f ? new Vector3(0f, boulderHeight * 0.45f, 0f) : new Vector3(0f, BLOCK_CENTRE_Y, 0f);
            float radius = 0f;
            foreach (VertexPositionNormalTexture vertex in v) radius = MathF.Max(radius, Vector3.Distance(vertex.Position, centre));
            return new UploadedMesh(device, v, idx, new BoundingSphere(centre, radius));
        }

        /// <summary>
        /// How far along <paramref name="d"/> the ray from the centre leaves the solid: the nearest of the planes' exits and
        /// the envelope's, met with a soft minimum of width <paramref name="bevel"/> (log-sum-exp about the nearest, which is
        /// exact far from an edge and rounds it near one). A plane the ray runs away from is never left.
        /// </summary>
        private static float SoftExit(Vector3 d, List<Vector3> normals, List<float> distances, float envelope, float bevel)
        {
            float nearest = envelope;
            for (int i = 0; i < normals.Count; i++)
            {
                float facing = Vector3.Dot(d, normals[i]);
                if (facing > 1e-3f) nearest = MathF.Min(nearest, distances[i] / facing);
            }
            float sum = MathF.Exp(-(envelope - nearest) / bevel);
            for (int i = 0; i < normals.Count; i++)
            {
                float facing = Vector3.Dot(d, normals[i]);
                if (facing > 1e-3f) sum += MathF.Exp(-(distances[i] / facing - nearest) / bevel);
            }
            return nearest - bevel * MathF.Log(sum);
        }

        private static Vector3 RandomDirection(Random rng)
        {
            while (true)
            {
                var v = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f);
                float l = v.LengthSquared();
                if (l > 0.01f && l <= 1f) return v / MathF.Sqrt(l);
            }
        }

        /// <summary>
        /// The unit sphere as an icosahedron split <paramref name="subdivisions"/> times (every triangle into four, the new
        /// points on the sphere), its points shared so the skin is closed, every triangle wound the same way round.
        /// 0 is 20 triangles, 1 is 80, 2 is 320, 3 is 1280, 4 is 5120 (2562 points, still 16-bit).
        /// </summary>
        internal static (List<Vector3> Points, List<int> Triangles) Icosphere(int subdivisions)
        {
            float t = (1f + MathF.Sqrt(5f)) * 0.5f;
            var points = new List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            for (int i = 0; i < points.Count; i++) points[i] = Vector3.Normalize(points[i]);

            //The usual twenty, all wound one way round (Build turns them to the front face by the volume)
            var triangles = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            for (int s = 0; s < subdivisions; s++)
            {
                var midpoints = new Dictionary<long, int>();
                int Midpoint(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (midpoints.TryGetValue(key, out int index)) return index;
                    points.Add(Vector3.Normalize(points[a] + points[b]));
                    midpoints[key] = points.Count - 1;
                    return points.Count - 1;
                }

                var next = new List<int>(triangles.Count * 4);
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Midpoint(a, b), bc = Midpoint(b, c), ca = Midpoint(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                triangles = next;
            }
            return (points, triangles);
        }
    }

    /// <summary>
    /// High and Ultra's rocks for a planting (#777): a <see cref="FracturedRockMesh"/> for every coarse block and boulder
    /// variant, at the density each instance's size takes, as <see cref="ScatterBucket"/>s with <see cref="ScatterBucket.FineMeshes"/>
    /// true, shaded as <see cref="ScatterBucket.Stone"/>. The planting hands it its instances already sorted (by variant and by
    /// <see cref="Density(float)"/>), each carrying the ground over its pivot in <see cref="ModelInstance.Custom"/> <c>z</c>.
    /// <para>
    /// <b>Four densities, by size</b>, because one would be wrong at one end: 320 triangles is a smooth pebble and a
    /// polygon's outline round a landmark seen from twenty units, and 5120 on each of two thousand pebbles is ten million
    /// triangles a frame, two passes, for rocks a few pixels wide. The cost is the triangles, at the primitive rate, wherever
    /// the camera is: the play camera sees a handful of these rocks and paid +0.54 ms for three densities a step finer, and
    /// +0.27 for these (desktop, 3840 x 1600, ssaa 2). <b>The sun's map takes the next coarser sampling of the same rock</b>
    /// (<see cref="ScatterBucket.CasterMesh"/>; its points are the finer one's own, so it lies inside it), which took that
    /// to +0.19.
    /// </para>
    /// </summary>
    internal sealed class FracturedRockSet : IDisposable
    {
        /// <summary>How many densities a variant is built at, and how many times each subdivides the icosahedron.</summary>
        internal const int DENSITIES = 4;
        private static readonly int[] SUBDIVISIONS = { 1, 2, 3, 4 };

        /// <summary>How far a rock reaches from its centre at scale 1: the block's frame and a boulder's radius.</summary>
        internal const float BLOCK_REACH = 0.45f, BOULDER_REACH = 1f;

        /// <summary>The density a rock of <paramref name="reach"/> world units takes: 80 triangles under 0.4, 320 under 1,
        /// 1280 under 2, 5120 above (a block of size 0.9, 2.2 and 4.4; a boulder of 0.4, 1 and 2).</summary>
        internal static int Density(float reach) => reach < 0.4f ? 0 : reach < 1f ? 1 : reach < 2f ? 2 : 3;

        public ScatterBucket[] Buckets { get; }

        private readonly List<IDisposable> _owned = new();

        /// <param name="blocks">The blocks' instances by coarse variant and density.</param>
        /// <param name="boulders">The boulders' instances the same way (the volcano's bombs).</param>
        /// <param name="boulderHeight">Each boulder variant's height over its base, its coarse <see cref="RockMesh"/>'s.</param>
        /// <param name="seed">The shapes' own seed, never the planting's stream, so where the rocks lie does not move.</param>
        /// <param name="dust">The ground's colour (linear) dusted on the foot and the top, <see cref="ScatterBucket.FootDust"/>.</param>
        /// <param name="stone">How deep the stone's surface goes, <see cref="ScatterBucket.Stone"/>.</param>
        public FracturedRockSet(GraphicsDevice device, List<ModelInstance>[,] blocks, List<ModelInstance>[,] boulders,
            Func<int, float> boulderHeight, int seed, Vector3 rock, Vector3 rockDusted, Vector3 dust, float stone = 1f)
        {
            var buckets = new List<ScatterBucket>();
            void Add(List<ModelInstance>[,] instances, int salt, Func<int, float> height)
            {
                for (int m = 0; m < instances.GetLength(0); m++)
                    for (int k = 0; k < DENSITIES; k++)
                    {
                        if (instances[m, k].Count == 0) continue;
                        int shape = seed + salt + 101 * m;
                        UploadedMesh mesh = FracturedRockMesh.Upload(device, shape, SUBDIVISIONS[k], height(m));
                        UploadedMesh caster = FracturedRockMesh.Upload(device, shape, Math.Max(1, SUBDIVISIONS[k] - 1), height(m));
                        _owned.Add(mesh);
                        _owned.Add(caster);
                        var bucket = new ScatterBucket(device, mesh, instances[m, k], rock, rockDusted, dapple: 0f, bark: 0f,
                            detailOnly: false, fineMeshes: true, stone: stone, footDust: dust, casterMesh: caster);
                        _owned.Add(bucket);
                        buckets.Add(bucket);
                    }
            }
            Add(blocks, 0, _ => 0f);
            Add(boulders, 5000, boulderHeight);
            Buckets = buckets.ToArray();
        }

        public void Dispose()
        {
            foreach (IDisposable d in _owned) d.Dispose();
            _owned.Clear();
        }
    }
}
