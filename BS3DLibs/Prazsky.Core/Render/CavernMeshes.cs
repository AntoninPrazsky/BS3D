using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The cavern's geometry as vertex arrays (#958), built off a <see cref="CavernChamber"/> with no device: the rock (the
    /// shell's cube sphere and the lake's bed in one mesh), the formations (stalactites, stalagmites and columns), the
    /// crystals, the glowworms and the light shafts. The desktop's backdrop wraps them in buffers (<see cref="CavernMeshes"/>),
    /// the Potato path draws the rock and the crystals at its own detail. Winding: every triangle is wound so its front
    /// faces the side its normal shows, by the rule <see cref="MeshBuilder"/> states (the front face's
    /// <c>(b - a) × (c - a)</c> points away from the eye), judged per triangle against the normal it is meant to show.
    /// </summary>
    public static class CavernGeometry
    {
        /// <summary>The detail one tier asks for: the shell's cells along a cube face's edge, the bed's cells across, and
        /// the facets round a formation.</summary>
        public readonly struct Detail
        {
            public readonly int ShellCells, FloorCells, Around, MaxHanging, ColumnRings;
            public readonly float ShortestHanging;

            public Detail(int shellCells, int floorCells, int around, float shortestHanging, int maxHanging, int columnRings)
            {
                ShellCells = shellCells;
                FloorCells = floorCells;
                Around = around;
                ShortestHanging = shortestHanging;
                MaxHanging = maxHanging;
                ColumnRings = columnRings;
            }

            /// <summary>High and Ultra's (<see cref="SceneRenderer.FineMeshes"/>).</summary>
            public static Detail Fine => new(176, 300, 14, 0f, int.MaxValue, 140);

            /// <summary>Low and Medium's: the same hall on fewer vertices, and the smallest stalactites left out.</summary>
            public static Detail Coarse => new(104, 180, 9, 8f, int.MaxValue, 80);

            /// <summary>The Raspberry Pi's: the hall's shape, its columns and its longest stalactites.</summary>
            public static Detail Potato => new(18, 40, 6, 22f, 50, 14);
        }

        private static void AddTriangle(List<int> indices, IList<Vector3> positions, int a, int b, int c, Vector3 shown)
        {
            Vector3 cross = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
            if (Vector3.Dot(cross, shown) > 0f) (b, c) = (c, b);
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
        }

        #region The rock

        /// <summary>
        /// The shell and the lake's bed as one mesh. Texture coordinate X is the vertex's occlusion (1 open, towards 0 in a
        /// recess), Y is 0 on the shell and 1 on the bed.
        /// </summary>
        public static (VertexPositionNormalTexture[] Vertices, int[] Indices) BuildRock(CavernChamber chamber, Detail detail)
        {
            var vertices = new List<VertexPositionNormalTexture>();
            var indices = new List<int>();
            AddShell(chamber, detail.ShellCells, vertices, indices);
            AddFloor(chamber, detail.FloorCells, vertices, indices);
            return (vertices.ToArray(), indices.ToArray());
        }

        //The cube's faces as (out, right, up), the bottom left out: straight down from the centre the shell stands sixty units
        //under the bed, which covers it from every lens inside the hall
        private static readonly (Vector3 Out, Vector3 Right, Vector3 Up)[] FACES =
        {
            (Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY),
            (-Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY),
            (Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY),
            (-Vector3.UnitZ, -Vector3.UnitX, Vector3.UnitY),
            (Vector3.UnitY, Vector3.UnitX, -Vector3.UnitZ),
        };

        private static void AddShell(CavernChamber chamber, int n, List<VertexPositionNormalTexture> vertices, List<int> indices)
        {
            int side = n + 1;
            float deepest = chamber.WaterY - 46f;
            foreach ((Vector3 axisOut, Vector3 right, Vector3 up) in FACES)
            {
                var face = new VertexPositionNormalTexture[side * side];
                //The equi-angular cube map: cells of nearly equal angle across the face, where a plain cube's corners crowd
                Parallel.For(0, side, j =>
                {
                    float b = MathF.Tan((j / (float)n * 2f - 1f) * MathHelper.PiOver4);
                    for (int i = 0; i < side; i++)
                    {
                        float a = MathF.Tan((i / (float)n * 2f - 1f) * MathHelper.PiOver4);
                        Vector3 u = Vector3.Normalize(axisOut + right * a + up * b);
                        float radius = chamber.ShellRadius(u, out float medium);
                        Vector3 position = chamber.Centre + u * radius;
                        Vector3 normal = chamber.ShellNormal(u);
                        //A recess is darker: the relief's own lumps below the medium surface, and the passages' mouths
                        float recess = MathF.Max(0f, (radius - medium) / medium);
                        float occlusion = Math.Clamp(1f - 14f * recess, 0.35f, 1f);
                        face[j * side + i] = new VertexPositionNormalTexture(position, normal, new Vector2(occlusion, 0f));
                    }
                });

                int first = vertices.Count;
                vertices.AddRange(face);
                var positions = new Vector3[face.Length];
                for (int k = 0; k < face.Length; k++) positions[k] = face[k].Position;
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        int v00 = j * side + i, v10 = v00 + 1, v01 = v00 + side, v11 = v01 + 1;
                        if (positions[v00].Y < deepest && positions[v10].Y < deepest && positions[v01].Y < deepest && positions[v11].Y < deepest) continue;
                        //Shown towards the centre, where every lens is
                        Vector3 shown = chamber.Centre - positions[v00];
                        AddTriangleOffset(indices, positions, first, v00, v10, v11, shown);
                        AddTriangleOffset(indices, positions, first, v00, v11, v01, shown);
                    }
            }
        }

        private static void AddTriangleOffset(List<int> indices, Vector3[] positions, int first, int a, int b, int c, Vector3 shown)
        {
            Vector3 cross = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
            if (Vector3.Dot(cross, shown) > 0f) (b, c) = (c, b);
            indices.Add(first + a);
            indices.Add(first + b);
            indices.Add(first + c);
        }

        private static void AddFloor(CavernChamber chamber, int n, List<VertexPositionNormalTexture> vertices, List<int> indices)
        {
            float extent = 0f;
            for (int k = 0; k < 360; k++)
            {
                float bearing = k * MathHelper.TwoPi / 360f;
                extent = MathF.Max(extent, chamber.WallAtWater(MathF.Cos(bearing), MathF.Sin(bearing)));
            }
            extent = MathF.Min(extent + 30f, 700f);

            int side = n + 1;
            float cell = 2f * extent / n;
            var grid = new VertexPositionNormalTexture[side * side];
            var keep = new bool[side * side];
            Parallel.For(0, side, j =>
            {
                float z = -extent + j * cell;
                for (int i = 0; i < side; i++)
                {
                    float x = -extent + i * cell;
                    float wall = chamber.WallAtWater(x, z);
                    keep[j * side + i] = x * x + z * z < (wall + 30f) * (wall + 30f);
                    float h = chamber.FloorHeight(x, z);
                    float e = cell * 0.5f;
                    float hx = chamber.FloorHeight(x + e, z) - chamber.FloorHeight(x - e, z);
                    float hz = chamber.FloorHeight(x, z + e) - chamber.FloorHeight(x, z - e);
                    Vector3 normal = Vector3.Normalize(new Vector3(-hx, 2f * e, -hz));
                    //A hollow of the bed is darker, and so is the foot of the wall
                    float around = 0.25f * (chamber.FloorHeight(x + 3f * cell, z) + chamber.FloorHeight(x - 3f * cell, z)
                        + chamber.FloorHeight(x, z + 3f * cell) + chamber.FloorHeight(x, z - 3f * cell));
                    float hollow = Math.Clamp((around - h) * 0.25f, 0f, 0.5f);
                    float foot = ShaderMath.SmoothStep(wall - 14f, wall + 2f, MathF.Sqrt(x * x + z * z));
                    float occlusion = Math.Clamp(1f - hollow - 0.45f * foot, 0.3f, 1f);
                    grid[j * side + i] = new VertexPositionNormalTexture(new Vector3(x, h, z), normal, new Vector2(occlusion, 1f));
                }
            });

            int first = vertices.Count;
            vertices.AddRange(grid);
            var positions = new Vector3[grid.Length];
            for (int k = 0; k < grid.Length; k++) positions[k] = grid[k].Position;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int v00 = j * side + i, v10 = v00 + 1, v01 = v00 + side, v11 = v01 + 1;
                    if (!keep[v00] && !keep[v10] && !keep[v01] && !keep[v11]) continue;
                    AddTriangleOffset(indices, positions, first, v00, v10, v11, Vector3.Up);
                    AddTriangleOffset(indices, positions, first, v00, v11, v01, Vector3.Up);
                }
        }

        #endregion

        #region The formations

        /// <summary>
        /// Every stalactite, stalagmite and column as one mesh, each displaced by world-space noise so no two are alike.
        /// Texture coordinate X is how far along the formation the vertex is (0 at the root, 1 at the tip; a column's
        /// waist is 0.5), Y is a per-formation roll; the normal is the surface's, outward.
        /// </summary>
        public static (VertexPositionNormalTexture[] Vertices, int[] Indices) BuildFormations(CavernChamber chamber, Detail detail)
        {
            var vertices = new List<VertexPositionNormalTexture>();
            var indices = new List<int>();
            int hanging = 0;
            foreach (CavernHanging s in chamber.Stalactites)
                if (s.Length >= detail.ShortestHanging && hanging++ < detail.MaxHanging) AddCone(s, detail.Around, false, detail.ColumnRings, vertices, indices);
            foreach (CavernHanging s in chamber.Stalagmites)
                if (s.Length >= detail.ShortestHanging * 0.6f && hanging++ < detail.MaxHanging) AddCone(s, detail.Around, true, detail.ColumnRings, vertices, indices);
            foreach (CavernColumn c in chamber.Columns) AddColumn(c, detail.Around * 3, detail.ColumnRings, vertices, indices);
            return (vertices.ToArray(), indices.ToArray());
        }

        //A stalactite (hanging) or a stalagmite (standing) as a lathe round its own axis: a thick root, a taper, drip rings and
        //lumps, and for a stalactite the soda straw at its tip. Shapes: 0 a slender ringed cone, 1 a fat carrot, 2 a straw-tipped
        //needle. Displaced by world noise, so the same shape twice is two formations.
        private static void AddCone(CavernHanging s, int around, bool standing, int maxRings, List<VertexPositionNormalTexture> vertices, List<int> indices)
        {
            Vector3 axis = s.Direction;
            Vector3 helper = MathF.Abs(axis.Y) < 0.9f ? Vector3.Up : Vector3.Right;
            Vector3 side = Vector3.Normalize(Vector3.Cross(axis, helper));
            Vector3 other = Vector3.Cross(axis, side);
            int rings = Math.Clamp((int)(s.Length / 1.3f), Math.Min(5, maxRings), Math.Min(40, maxRings));
            float straw = s.Shape == 2 ? 0.20f : 0.08f;
            float power = s.Shape == 1 ? 0.65f : standing ? 0.55f : 0.95f;

            int first = vertices.Count;
            var positions = new List<Vector3>();
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float taper = MathF.Pow(1f - t, power);
                float radius = s.Girth * taper;
                if (!standing && t > 1f - straw) radius = MathF.Max(radius, MathF.Min(0.22f, s.Girth * 0.3f));
                //A stalagmite's crown is rounded, a stack of plates, never a point
                if (standing) radius = MathF.Max(radius, s.Girth * MathF.Max(0.35f * MathF.Sqrt(MathF.Max(0f, 1f - t)), 0.12f));
                float ring = 1f + 0.10f * MathF.Sin(t * s.Length * 0.85f + s.Seed) + 0.05f * MathF.Sin(t * s.Length * 2.3f + s.Seed * 1.7f);
                Vector3 centre = s.Root + axis * (t * s.Length);
                for (int a = 0; a < around; a++)
                {
                    float angle = a * MathHelper.TwoPi / around;
                    Vector3 radial = side * MathF.Cos(angle) + other * MathF.Sin(angle);
                    Vector3 p = centre + radial * (radius * ring);
                    float lump = CavernNoise.Fbm(p * 0.45f + new Vector3(s.Seed, 0f, 0f), 2);
                    p += radial * (radius * 0.28f * lump);
                    positions.Add(p);
                }
            }
            Vector3 tip = s.Root + axis * (s.Length + (standing ? s.Girth * 0.15f : 0.05f));
            positions.Add(tip);

            //Normals off the displaced rings: across the ring and along the axis
            for (int r = 0; r <= rings; r++)
                for (int a = 0; a < around; a++)
                {
                    Vector3 p = positions[r * around + a];
                    Vector3 next = positions[r * around + (a + 1) % around] - positions[r * around + (a + around - 1) % around];
                    Vector3 down = positions[Math.Min(r + 1, rings) * around + a] - positions[Math.Max(r - 1, 0) * around + a];
                    Vector3 normal = Vector3.Cross(next, down);
                    Vector3 outward = p - (s.Root + axis * Vector3.Dot(p - s.Root, axis));
                    if (Vector3.Dot(normal, outward) < 0f) normal = -normal;
                    if (normal.LengthSquared() < 1e-10f) normal = outward;
                    if (normal.LengthSquared() < 1e-10f) normal = axis;
                    vertices.Add(new VertexPositionNormalTexture(p, Vector3.Normalize(normal), new Vector2(r / (float)rings, s.Seed % 1f)));
                }
            vertices.Add(new VertexPositionNormalTexture(tip, axis, new Vector2(1f, s.Seed % 1f)));

            int tipIndex = positions.Count - 1;
            for (int r = 0; r < rings; r++)
                for (int a = 0; a < around; a++)
                {
                    int v00 = r * around + a, v10 = r * around + (a + 1) % around;
                    int v01 = v00 + around, v11 = v10 + around;
                    Vector3 shown = positions[v00] - (s.Root + axis * Vector3.Dot(positions[v00] - s.Root, axis));
                    AddTriangleList(indices, positions, first, v00, v10, v11, shown);
                    AddTriangleList(indices, positions, first, v00, v11, v01, shown);
                }
            for (int a = 0; a < around; a++)
                AddTriangleList(indices, positions, first, rings * around + a, rings * around + (a + 1) % around, tipIndex, axis);
        }

        private static void AddTriangleList(List<int> indices, List<Vector3> positions, int first, int a, int b, int c, Vector3 shown)
        {
            Vector3 cross = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
            if (Vector3.Dot(cross, shown) > 0f) (b, c) = (c, b);
            indices.Add(first + a);
            indices.Add(first + b);
            indices.Add(first + c);
        }

        //A column: a stalagmite and a stalactite grown into one. The stalagmite's half is a stack of rounded plates broad at the
        //foot, the stalactite's half drapes down out of the vault in deep folds, and the two meet in a shaft of organ-pipe
        //flowstone. Never a smooth trunk with a fanned head - that read as a crooked tree (#958) - and never a turned post:
        //the folds are ridged noise round it and the plates' spacing is rolled, so no two rings repeat
        private static void AddColumn(CavernColumn c, int around, int maxRings, List<VertexPositionNormalTexture> vertices, List<int> indices)
        {
            float height = c.Top - c.Bottom;
            int rings = Math.Clamp((int)(height / 1.4f), Math.Min(24, maxRings), maxRings);
            int first = vertices.Count;
            var positions = new List<Vector3>();
            var seed = new Vector3(c.Seed, 11f, 3f);
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float y = c.Bottom + t * height;
                //The stalagmite's plates: lobes of rolled spacing that die out up the lower third
                //Plates all the way up, broad and deep at the foot and shallower over the shaft, so the silhouette is a stack
                //of rounded lips rather than a trunk's smooth taper
                float plateCoord = y * 0.30f + 2.4f * CavernNoise.Fbm(new Vector3(y * 0.05f, c.Seed, 1f), 2);
                float plates = MathF.Pow(MathF.Abs(MathF.Sin(plateCoord)), 0.5f);
                float foot = MathF.Exp(-t * 6f) * 0.9f;
                //The stalactite's drapery: wider into the vault, but folded rather than fanned
                float head = MathF.Exp(-(1f - t) * 12f) * 0.6f;
                float radius = c.Girth * (0.74f + foot + head + plates * (0.05f + 0.42f * MathF.Exp(-t * 3.5f)));
                float fold = 0.36f + 0.22f * MathF.Exp(-(1f - t) * 5f);
                //The axis wanders a little, as a column grown from two drip points does
                Vector2 wander = new Vector2(CavernNoise.Fbm(new Vector3(t * 2.2f, c.Seed, 0f), 2), CavernNoise.Fbm(new Vector3(c.Seed, t * 2.2f, 5f), 2)) * c.Girth * 0.7f;
                for (int a = 0; a < around; a++)
                {
                    float angle = a * MathHelper.TwoPi / around;
                    Vector3 radial = new(MathF.Cos(angle), 0f, MathF.Sin(angle));
                    Vector3 p0 = new(c.Axis.X + wander.X + radial.X * radius, y, c.Axis.Y + wander.Y + radial.Z * radius);
                    //Folds: ribs running down the column, their depth and spacing off noise stretched along it (no regular
                    //cos(n theta), which reads as a turned post; no single ring frequency, which read as a corkscrew)
                    float rib = CavernNoise.Ridged(new Vector3(radial.X * 3.0f, y * 0.010f, radial.Z * 3.0f) + seed, 3);
                    float lumps = CavernNoise.Fbm(p0 * 0.11f + seed, 2);
                    float r1 = radius * (1f + fold * (rib - 0.35f) + 0.10f * lumps);
                    positions.Add(new Vector3(c.Axis.X + wander.X + radial.X * r1, y, c.Axis.Y + wander.Y + radial.Z * r1));
                }
            }

            for (int r = 0; r <= rings; r++)
                for (int a = 0; a < around; a++)
                {
                    Vector3 p = positions[r * around + a];
                    Vector3 next = positions[r * around + (a + 1) % around] - positions[r * around + (a + around - 1) % around];
                    Vector3 up = positions[Math.Min(r + 1, rings) * around + a] - positions[Math.Max(r - 1, 0) * around + a];
                    Vector3 centre = 0.5f * (positions[r * around] + positions[r * around + around / 2]);
                    Vector3 outward = new(p.X - centre.X, 0f, p.Z - centre.Z);
                    Vector3 normal = Vector3.Cross(next, up);
                    if (Vector3.Dot(normal, outward) < 0f) normal = -normal;
                    float t = r / (float)rings;
                    vertices.Add(new VertexPositionNormalTexture(p, Vector3.Normalize(normal), new Vector2(t, 2f + c.Seed % 1f)));
                }

            for (int r = 0; r < rings; r++)
                for (int a = 0; a < around; a++)
                {
                    int v00 = r * around + a, v10 = r * around + (a + 1) % around;
                    int v01 = v00 + around, v11 = v10 + around;
                    Vector3 p = positions[v00];
                    Vector3 shown = new(p.X - c.Axis.X, 0f, p.Z - c.Axis.Y);
                    AddTriangleList(indices, positions, first, v00, v10, v11, shown);
                    AddTriangleList(indices, positions, first, v00, v11, v01, shown);
                }
        }

        #endregion

        #region The crystals

        /// <summary>
        /// Every prism of every cluster: six flat faces and a six-sided pyramid, each face its own vertices so the facets
        /// stay flat. Each vertex carries its prism's frame, which the shader traces the view ray through for the thickness
        /// the light crosses.
        /// </summary>
        public static (CavernCrystalVertex[] Vertices, int[] Indices) BuildCrystals(CavernChamber chamber)
        {
            var vertices = new List<CavernCrystalVertex>();
            var indices = new List<int>();
            for (int k = 0; k < chamber.Clusters.Count; k++)
            {
                CavernCluster cluster = chamber.Clusters[k];
                foreach (CavernPrism prism in cluster.Prisms) AddPrism(prism, cluster, k, vertices, indices);
            }
            return (vertices.ToArray(), indices.ToArray());
        }

        /// <summary>One cluster's prisms with nothing but a position, a facet normal and a texture coordinate: the Potato
        /// path's crystals, drawn by the instanced renderer in the cluster's colour.</summary>
        public static (VertexPositionNormalTexture[] Vertices, int[] Indices) BuildClusterPlain(CavernCluster cluster, int clusterIndex)
        {
            var vertices = new List<CavernCrystalVertex>();
            var indices = new List<int>();
            foreach (CavernPrism prism in cluster.Prisms) AddPrism(prism, cluster, clusterIndex, vertices, indices);
            var plain = new VertexPositionNormalTexture[vertices.Count];
            for (int i = 0; i < plain.Length; i++) plain[i] = new VertexPositionNormalTexture(vertices[i].Position, vertices[i].Normal, Vector2.Zero);
            return (plain, indices.ToArray());
        }

        /// <summary>A prism's frame: the side and other axes round <paramref name="axis"/>, turned by <paramref name="turn"/>.
        /// The shader rebuilds the same frame from the same three numbers.</summary>
        public static void PrismFrame(Vector3 axis, float turn, out Vector3 side, out Vector3 other)
        {
            Vector3 helper = MathF.Abs(axis.Y) < 0.9f ? Vector3.Up : Vector3.Right;
            Vector3 s0 = Vector3.Normalize(Vector3.Cross(axis, helper));
            Vector3 o0 = Vector3.Cross(axis, s0);
            side = s0 * MathF.Cos(turn) + o0 * MathF.Sin(turn);
            other = Vector3.Cross(axis, side);
        }

        private static void AddPrism(CavernPrism prism, CavernCluster cluster, int clusterIndex, List<CavernCrystalVertex> vertices, List<int> indices)
        {
            PrismFrame(prism.Axis, prism.Turn, out Vector3 side, out Vector3 other);
            var corner = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float angle = i * MathHelper.Pi / 3f;
                corner[i] = side * MathF.Cos(angle) + other * MathF.Sin(angle);
            }

            Vector3 shoulder = prism.Base + prism.Axis * prism.Length;
            Vector3 apex = shoulder + prism.Axis * prism.Tip;
            var frame0 = new Vector4(prism.Base, prism.Radius);
            var frame1 = new Vector4(prism.Axis, prism.Length);
            var frame2 = new Vector4(prism.Tip, prism.Turn, cluster.Hue, cluster.Phase + clusterIndex * 0.37f);

            for (int i = 0; i < 6; i++)
            {
                Vector3 a = corner[i], b = corner[(i + 1) % 6];
                Vector3 faceNormal = Vector3.Normalize(a + b);
                //The side face: a quad from the base to the shoulder
                Vector3 p0 = prism.Base + a * prism.Radius, p1 = prism.Base + b * prism.Radius;
                Vector3 p2 = shoulder + b * prism.Radius, p3 = shoulder + a * prism.Radius;
                int first = vertices.Count;
                vertices.Add(new CavernCrystalVertex(p0, faceNormal, frame0, frame1, frame2));
                vertices.Add(new CavernCrystalVertex(p1, faceNormal, frame0, frame1, frame2));
                vertices.Add(new CavernCrystalVertex(p2, faceNormal, frame0, frame1, frame2));
                vertices.Add(new CavernCrystalVertex(p3, faceNormal, frame0, frame1, frame2));
                AddQuad(indices, first, p0, p1, p2, p3, faceNormal);

                //The pyramid's face: the shoulder's edge to the apex
                Vector3 slope = Vector3.Normalize(Vector3.Cross(p2 - apex, p3 - apex));
                if (Vector3.Dot(slope, faceNormal) < 0f) slope = -slope;
                int tipFirst = vertices.Count;
                vertices.Add(new CavernCrystalVertex(p3, slope, frame0, frame1, frame2));
                vertices.Add(new CavernCrystalVertex(p2, slope, frame0, frame1, frame2));
                vertices.Add(new CavernCrystalVertex(apex, slope, frame0, frame1, frame2));
                AddTri(indices, tipFirst, p3, p2, apex, slope, 0, 1, 2);
            }
        }

        private static void AddQuad(List<int> indices, int first, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 shown)
        {
            AddTri(indices, first, p0, p1, p2, shown, 0, 1, 2);
            AddTri(indices, first, p0, p2, p3, shown, 0, 2, 3);
        }

        private static void AddTri(List<int> indices, int first, Vector3 pa, Vector3 pb, Vector3 pc, Vector3 shown, int a, int b, int c)
        {
            Vector3 cross = Vector3.Cross(pb - pa, pc - pa);
            if (Vector3.Dot(cross, shown) > 0f) (b, c) = (c, b);
            indices.Add(first + a);
            indices.Add(first + b);
            indices.Add(first + c);
        }

        #endregion

        #region The glowworms and the shafts

        /// <summary>Each glowworm as a quad of four corners at its point; the vertex shader spreads them by a pixel size.</summary>
        public static (CavernGlowwormVertex[] Vertices, int[] Indices) BuildGlowworms(CavernChamber chamber)
        {
            var vertices = new CavernGlowwormVertex[chamber.Glowworms.Count * 4];
            var indices = new int[chamber.Glowworms.Count * 6];
            for (int k = 0; k < chamber.Glowworms.Count; k++)
            {
                CavernGlowworm worm = chamber.Glowworms[k];
                int v = k * 4;
                vertices[v] = new CavernGlowwormVertex(worm.Position, new Vector4(-1f, -1f, worm.Phase, worm.Size));
                vertices[v + 1] = new CavernGlowwormVertex(worm.Position, new Vector4(1f, -1f, worm.Phase, worm.Size));
                vertices[v + 2] = new CavernGlowwormVertex(worm.Position, new Vector4(-1f, 1f, worm.Phase, worm.Size));
                vertices[v + 3] = new CavernGlowwormVertex(worm.Position, new Vector4(1f, 1f, worm.Phase, worm.Size));
                int i = k * 6;
                indices[i] = v; indices[i + 1] = v + 1; indices[i + 2] = v + 2;
                indices[i + 3] = v + 2; indices[i + 4] = v + 1; indices[i + 5] = v + 3;
            }
            return (vertices, indices);
        }

        /// <summary>
        /// Each shaft as an open cone round its beam, wide enough to hold the beam's glow (twice its radius, flaring towards
        /// the lake). Texture coordinate X is which shaft. Drawn with no culling; the shader keeps only the near side.
        /// </summary>
        public static (VertexPositionNormalTexture[] Vertices, int[] Indices) BuildShafts(CavernChamber chamber)
        {
            const int AROUND = 24, ALONG = 12;
            var vertices = new List<VertexPositionNormalTexture>();
            var indices = new List<int>();
            for (int s = 0; s < chamber.Shafts.Count; s++)
            {
                CavernShaft shaft = chamber.Shafts[s];
                Vector3 helper = MathF.Abs(shaft.Direction.Y) < 0.9f ? Vector3.Up : Vector3.Right;
                Vector3 side = Vector3.Normalize(Vector3.Cross(shaft.Direction, helper));
                Vector3 other = Vector3.Cross(shaft.Direction, side);
                int first = vertices.Count;
                for (int r = 0; r <= ALONG; r++)
                {
                    float t = r / (float)ALONG;
                    Vector3 centre = shaft.Source + shaft.Direction * (shaft.Length * t);
                    float radius = shaft.Radius * (1f + 0.5f * t) * 2.4f;
                    for (int a = 0; a < AROUND; a++)
                    {
                        float angle = a * MathHelper.TwoPi / AROUND;
                        Vector3 radial = side * MathF.Cos(angle) + other * MathF.Sin(angle);
                        vertices.Add(new VertexPositionNormalTexture(centre + radial * radius, radial, new Vector2(s, t)));
                    }
                }
                var positions = new List<Vector3>();
                for (int k = first; k < vertices.Count; k++) positions.Add(vertices[k].Position);
                for (int r = 0; r < ALONG; r++)
                    for (int a = 0; a < AROUND; a++)
                    {
                        int v00 = r * AROUND + a, v10 = r * AROUND + (a + 1) % AROUND;
                        int v01 = v00 + AROUND, v11 = v10 + AROUND;
                        Vector3 outward = vertices[first + v00].Normal;
                        AddTriangleList(indices, positions, first, v00, v10, v11, outward);
                        AddTriangleList(indices, positions, first, v00, v11, v01, outward);
                    }
            }
            return (vertices.ToArray(), indices.ToArray());
        }

        #endregion
    }

    /// <summary>A crystal's vertex: its point and facet normal, and its prism's frame (base and corner radius; axis and
    /// length to the shoulder; tip length, turn, the cluster's hue and the phase of its breath).</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CavernCrystalVertex : IVertexType
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector4 Frame0;
        public Vector4 Frame1;
        public Vector4 Frame2;

        public static readonly VertexDeclaration Declaration = new(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(24, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(40, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
            new VertexElement(56, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2));

        public CavernCrystalVertex(Vector3 position, Vector3 normal, Vector4 frame0, Vector4 frame1, Vector4 frame2)
        {
            Position = position;
            Normal = normal;
            Frame0 = frame0;
            Frame1 = frame1;
            Frame2 = frame2;
        }

        readonly VertexDeclaration IVertexType.VertexDeclaration => Declaration;
    }

    /// <summary>A glowworm's corner: its point, and the corner (-1..1 each way), the phase of its breath and its size.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CavernGlowwormVertex : IVertexType
    {
        public Vector3 Position;
        public Vector4 Corner;

        public static readonly VertexDeclaration Declaration = new(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0));

        public CavernGlowwormVertex(Vector3 position, Vector4 corner)
        {
            Position = position;
            Corner = corner;
        }

        readonly VertexDeclaration IVertexType.VertexDeclaration => Declaration;
    }

    /// <summary>A vertex array and its indices as an <see cref="IProceduralMesh"/>: how the Potato path draws the cavern's
    /// rock, formations and crystals through its own instanced renderer.</summary>
    public sealed class CavernProceduralMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        public CavernProceduralMesh(GraphicsDevice device, VertexPositionNormalTexture[] vertices, int[] indices)
        {
            VertexBuffer = new VertexBuffer(device, VertexPositionNormalTexture.VertexDeclaration, Math.Max(vertices.Length, 1), BufferUsage.WriteOnly);
            if (vertices.Length > 0) VertexBuffer.SetData(vertices);
            IndexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, Math.Max(indices.Length, 3), BufferUsage.WriteOnly);
            if (indices.Length > 0) IndexBuffer.SetData(indices);
            PrimitiveCount = indices.Length / 3;

            var points = new Vector3[vertices.Length];
            for (int i = 0; i < points.Length; i++) points[i] = vertices[i].Position;
            BoundingSphere = points.Length > 0 ? BoundingSphere.CreateFromPoints(points) : new BoundingSphere(Vector3.Zero, 1f);
        }

        public void Dispose()
        {
            VertexBuffer?.Dispose();
            VertexBuffer = null;
            IndexBuffer?.Dispose();
            IndexBuffer = null;
        }
    }

    /// <summary>
    /// The cavern's geometry in buffers on one device (#958): one mesh per draw, built from <see cref="CavernGeometry"/>'s
    /// arrays at one detail. The backdrop keeps one of these per detail it has drawn.
    /// </summary>
    public sealed class CavernMeshes : IDisposable
    {
        public sealed class Mesh : IDisposable
        {
            public VertexBuffer Vertices;
            public IndexBuffer Indices;
            public int PrimitiveCount;

            public static Mesh Of<T>(GraphicsDevice device, VertexDeclaration declaration, T[] vertices, int[] indices) where T : struct
            {
                if (vertices.Length == 0 || indices.Length == 0) return null;
                var mesh = new Mesh
                {
                    Vertices = new VertexBuffer(device, declaration, vertices.Length, BufferUsage.WriteOnly),
                    Indices = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly),
                    PrimitiveCount = indices.Length / 3,
                };
                mesh.Vertices.SetData(vertices);
                mesh.Indices.SetData(indices);
                return mesh;
            }

            public void Draw(GraphicsDevice device)
            {
                device.SetVertexBuffer(Vertices);
                device.Indices = Indices;
                device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, PrimitiveCount);
            }

            public void Dispose()
            {
                Vertices?.Dispose();
                Indices?.Dispose();
            }
        }

        public Mesh Rock { get; }
        public Mesh Formations { get; }
        public Mesh Crystals { get; }
        public Mesh Glowworms { get; }
        public Mesh Shafts { get; }

        /// <summary>Arrays built (off the frame thread, as the backdrop does it) and handed in here to become buffers.</summary>
        public sealed class Arrays
        {
            public (VertexPositionNormalTexture[] Vertices, int[] Indices) Rock, Formations, Shafts;
            public (CavernCrystalVertex[] Vertices, int[] Indices) Crystals;
            public (CavernGlowwormVertex[] Vertices, int[] Indices) Glowworms;

            public static Arrays Build(CavernChamber chamber, CavernGeometry.Detail detail) => new()
            {
                Rock = CavernGeometry.BuildRock(chamber, detail),
                Formations = CavernGeometry.BuildFormations(chamber, detail),
                Crystals = CavernGeometry.BuildCrystals(chamber),
                Glowworms = CavernGeometry.BuildGlowworms(chamber),
                Shafts = CavernGeometry.BuildShafts(chamber),
            };
        }

        public CavernMeshes(GraphicsDevice device, Arrays arrays)
        {
            Rock = Mesh.Of(device, VertexPositionNormalTexture.VertexDeclaration, arrays.Rock.Vertices, arrays.Rock.Indices);
            Formations = Mesh.Of(device, VertexPositionNormalTexture.VertexDeclaration, arrays.Formations.Vertices, arrays.Formations.Indices);
            Crystals = Mesh.Of(device, CavernCrystalVertex.Declaration, arrays.Crystals.Vertices, arrays.Crystals.Indices);
            Glowworms = Mesh.Of(device, CavernGlowwormVertex.Declaration, arrays.Glowworms.Vertices, arrays.Glowworms.Indices);
            Shafts = Mesh.Of(device, VertexPositionNormalTexture.VertexDeclaration, arrays.Shafts.Vertices, arrays.Shafts.Indices);
        }

        public void Dispose()
        {
            Rock?.Dispose();
            Formations?.Dispose();
            Crystals?.Dispose();
            Glowworms?.Dispose();
            Shafts?.Dispose();
        }
    }
}
