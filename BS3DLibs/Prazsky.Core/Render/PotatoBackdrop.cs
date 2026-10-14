using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A scene's ground on the Potato path (#829): the terrain the desktop draws through its own Shader Model 5.0
    /// program, as a low-poly grid built once on the CPU from the terrain's mirror (<see cref="TerrainMirror"/>, the
    /// same heights the scatters and lamps are placed by) and drawn through <c>PotatoLit</c> like the island. A square
    /// grid of <c>cells</c> a side over ±<c>extent</c>, with the quads inside the arena's hole left out (the desktop
    /// cuts the same hole, <c>TerrainHoleRadius</c>, so the pit's shaft is not seen through), normals from the
    /// height's own differences, wound clockwise seen from above as every procedural mesh here is.
    /// </summary>
    public sealed class PotatoTerrainMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <param name="keep">Which triangles to keep, by their three corners, or null for all of them: the mountains are
        /// two grids over one field, the snow's triangles and the rock's (#829).</param>
        public PotatoTerrainMesh(GraphicsDevice graphicsDevice, int cells, float extent, float holeRadius, Func<float, float, float> height,
            Func<Vector3, Vector3, Vector3, bool> keep = null)
        {
            (VertexPositionNormalTexture[] vertices, int[] indices) = Build(cells, extent, holeRadius, height, keep);

            PrimitiveCount = indices.Length / 3;

            VertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalTexture.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices);

            IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices);

            BoundingSphere = new BoundingSphere(Vector3.Zero, extent * 1.5f);
        }

        /// <summary>The grid's geometry, with no device: <c>PotatoBackdropTests</c> hold the hole and the winding.</summary>
        public static (VertexPositionNormalTexture[] Vertices, int[] Indices) Build(int cells, float extent, float holeRadius, Func<float, float, float> height,
            Func<Vector3, Vector3, Vector3, bool> keep = null)
        {
            if (cells < 2) throw new ArgumentOutOfRangeException(nameof(cells));

            int side = cells + 1;
            float step = 2f * extent / cells;
            var vertices = new VertexPositionNormalTexture[side * side];

            for (int row = 0; row < side; row++)
                for (int column = 0; column < side; column++)
                {
                    float x = -extent + column * step, z = -extent + row * step;
                    float y = height(x, z);

                    //The slope from the height's own differences, a cell apart: the mesh is as smooth as its grid
                    float dx = (height(x + step, z) - height(x - step, z)) / (2f * step);
                    float dz = (height(x, z + step) - height(x, z - step)) / (2f * step);
                    Vector3 normal = Vector3.Normalize(new Vector3(-dx, 1f, -dz));

                    vertices[row * side + column] = new VertexPositionNormalTexture(new Vector3(x, y, z), normal,
                        new Vector2((float)column / cells, (float)row / cells));
                }

            var indices = new List<int>(cells * cells * 6);
            float hole2 = holeRadius * holeRadius;

            for (int row = 0; row < cells; row++)
                for (int column = 0; column < cells; column++)
                {
                    int a = row * side + column, b = a + 1, c = a + side, d = c + 1;

                    //Clockwise seen from above (+Y is outward): (b - a) x (c - a) must point down. A triangle whose
                    //three corners are all inside the hole is left out
                    Add(vertices, indices, a, c, b, hole2, keep);
                    Add(vertices, indices, b, c, d, hole2, keep);
                }

            return (vertices, indices.ToArray());
        }

        private static void Add(VertexPositionNormalTexture[] vertices, List<int> indices, int a, int b, int c, float hole2,
            Func<Vector3, Vector3, Vector3, bool> keep)
        {
            bool inside = true;
            foreach (int corner in new[] { a, b, c })
            {
                Vector3 p = vertices[corner].Position;
                if (p.X * p.X + p.Z * p.Z > hole2) { inside = false; break; }
            }
            if (inside) return;
            if (keep != null && !keep(vertices[a].Position, vertices[b].Position, vertices[c].Position)) return;

            Vector3 cross = Vector3.Cross(vertices[b].Position - vertices[a].Position, vertices[c].Position - vertices[a].Position);
            if (cross.Y > 0f) (b, c) = (c, b);

            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
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
    /// A ribbon on the Potato path (#829): a strip of quads between two rails of points, its normals up, wound clockwise
    /// seen from above like the grid. The volcano's lava rivers, which the desktop paints into its terrain's pixels.
    /// </summary>
    public sealed class PotatoStripMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        public PotatoStripMesh(GraphicsDevice graphicsDevice, Vector3[] left, Vector3[] right)
        {
            (VertexPositionNormalTexture[] vertices, int[] indices) = Build(left, right);

            PrimitiveCount = indices.Length / 3;

            VertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalTexture.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices);

            IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices);

            Vector3 centre = Vector3.Zero;
            foreach (VertexPositionNormalTexture v in vertices) centre += v.Position;
            centre /= vertices.Length;
            float radius = 0f;
            foreach (VertexPositionNormalTexture v in vertices) radius = MathF.Max(radius, Vector3.Distance(centre, v.Position));
            BoundingSphere = new BoundingSphere(centre, radius);
        }

        /// <summary>The strip's geometry, with no device: two rails of the same length, a quad between each pair of steps.</summary>
        public static (VertexPositionNormalTexture[] Vertices, int[] Indices) Build(Vector3[] left, Vector3[] right)
        {
            if (left.Length != right.Length || left.Length < 2) throw new ArgumentException("two rails of the same length, two steps at least");

            int steps = left.Length;
            var vertices = new VertexPositionNormalTexture[steps * 2];
            for (int i = 0; i < steps; i++)
            {
                float t = (float)i / (steps - 1);
                vertices[i * 2] = new VertexPositionNormalTexture(left[i], Vector3.Up, new Vector2(0f, t));
                vertices[i * 2 + 1] = new VertexPositionNormalTexture(right[i], Vector3.Up, new Vector2(1f, t));
            }

            var indices = new List<int>((steps - 1) * 6);
            for (int i = 0; i < steps - 1; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                AddWound(vertices, indices, a, c, b);
                AddWound(vertices, indices, b, c, d);
            }

            return (vertices, indices.ToArray());
        }

        private static void AddWound(VertexPositionNormalTexture[] vertices, List<int> indices, int a, int b, int c)
        {
            Vector3 cross = Vector3.Cross(vertices[b].Position - vertices[a].Position, vertices[c].Position - vertices[a].Position);
            if (cross.Y > 0f) (b, c) = (c, b);
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
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
    /// A shell seen from inside (#829): a frustum's wall between two heights and two radii - a cylinder, a cone, a
    /// rake - and the disc that closes its top, normals inward (and up or down as the wall leans), wound for an eye
    /// inside it. The cavern's rock and the big top's canvas, wall and seats, which the desktop marches or hits in
    /// closed form.
    /// </summary>
    public sealed class PotatoShellMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <param name="keepSegment">Which of the wall's segments to build, by index, or null for all: the big top's
        /// canvas is two shells over one cone, the red panels and the cream.</param>
        /// <param name="closeTop">Whether the top is closed by a disc.</param>
        /// <param name="inside">Whether the eye is inside (the cavern, the tent) or outside (a tree's trunk as a column).</param>
        public PotatoShellMesh(GraphicsDevice graphicsDevice, float radiusBottom, float radiusTop, float bottom, float top, int segments,
            Func<int, bool> keepSegment = null, bool closeTop = true, bool inside = true)
        {
            (VertexPositionNormalTexture[] vertices, int[] indices) = Build(radiusBottom, radiusTop, bottom, top, segments, keepSegment, closeTop, inside);

            PrimitiveCount = indices.Length / 3;

            VertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalTexture.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices);

            IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices);

            float radius = MathF.Max(radiusBottom, radiusTop);
            BoundingSphere = new BoundingSphere(new Vector3(0f, (bottom + top) * 0.5f, 0f), MathF.Sqrt(radius * radius + (top - bottom) * (top - bottom) * 0.25f));
        }

        /// <summary>
        /// The shell's geometry, with no device: a ring of <paramref name="segments"/> quads for the wall and a fan for the
        /// ceiling. The wall's normal is the slant's perpendicular turned inward - straight in on a cylinder, in and down
        /// under a cone that narrows upward, in and up on a rake that widens. A triangle is front-facing when its cross
        /// product points away from the eye, as the grid's does (seen from above its cross points down); here the eye is
        /// inside, on the side the normal points to.
        /// </summary>
        public static (VertexPositionNormalTexture[] Vertices, int[] Indices) Build(float radiusBottom, float radiusTop, float bottom, float top, int segments,
            Func<int, bool> keepSegment = null, bool closeTop = true, bool inside = true)
        {
            if (segments < 3) throw new ArgumentOutOfRangeException(nameof(segments));

            //The normals face the eye: inward and the top's down for an eye inside, outward and up for one outside; the
            //winding follows the normal in AddFacing either way
            float facing = inside ? 1f : -1f;
            Vector2 slant = Vector2.Normalize(new Vector2(-(top - bottom), radiusTop - radiusBottom)) * facing; //(radial, y)
            Vector3 topNormal = inside ? Vector3.Down : Vector3.Up;
            var vertices = new VertexPositionNormalTexture[segments * 3 + 1];
            for (int i = 0; i < segments; i++)
            {
                float angle = MathHelper.TwoPi * i / segments;
                float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
                Vector3 normal = Vector3.Normalize(new Vector3(slant.X * cos, slant.Y, slant.X * sin));
                float u = (float)i / segments;
                vertices[i] = new VertexPositionNormalTexture(new Vector3(radiusBottom * cos, bottom, radiusBottom * sin), normal, new Vector2(u, 1f));
                vertices[segments + i] = new VertexPositionNormalTexture(new Vector3(radiusTop * cos, top, radiusTop * sin), normal, new Vector2(u, 0f));
                vertices[2 * segments + i] = new VertexPositionNormalTexture(new Vector3(radiusTop * cos, top, radiusTop * sin), topNormal, new Vector2(u, 0f));
            }
            vertices[3 * segments] = new VertexPositionNormalTexture(new Vector3(0f, top, 0f), topNormal, new Vector2(0.5f, 0.5f));

            var indices = new List<int>(segments * 9);
            for (int i = 0; i < segments; i++)
            {
                if (keepSegment != null && !keepSegment(i)) continue;
                int next = (i + 1) % segments;
                int a = i, b = next, c = segments + i, d = segments + next;
                AddFacing(vertices, indices, a, c, b);
                AddFacing(vertices, indices, b, c, d);
                if (closeTop) AddFacing(vertices, indices, 3 * segments, 2 * segments + i, 2 * segments + next);
            }

            return (vertices, indices.ToArray());
        }

        private static void AddFacing(VertexPositionNormalTexture[] vertices, List<int> indices, int a, int b, int c)
        {
            Vector3 cross = Vector3.Cross(vertices[b].Position - vertices[a].Position, vertices[c].Position - vertices[a].Position);
            if (Vector3.Dot(cross, vertices[a].Normal) > 0f) (b, c) = (c, b);
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
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
    /// What of a scene's backdrop the Potato path draws (#829): its ground (<see cref="PotatoTerrainMesh"/>) and its
    /// static scatter, every piece an <see cref="InstancedModelRenderer"/> on the Potato effect drawn through
    /// <c>PotatoLit</c> and lit by the host's sky rig like the island (the host enrols <see cref="Renderers"/>).
    /// Nothing per pixel: a scene's cost here is its vertices, which V3D's binning pays for every vertex handed over
    /// (#804), so a scatter is the desktop's Low tier (the detail buckets left out) and the ground a coarse grid.
    /// <see cref="For"/> says which scenes have one; the rest keep the dome's gradient.
    /// </summary>
    public sealed class PotatoBackdrop : IDisposable
    {
        private readonly List<InstancedModelRenderer> _renderers = new();
        //One draw: its renderer, its instances (a city's are the visible set, prepared per frame), its cull and its glow
        private sealed class DrawItem
        {
            public InstancedModelRenderer Renderer;
            public ModelInstance[] Instances;
            public bool TwoSided;
            public Vector3? Glow;
            public Func<ICamera, int> Prepare;
        }

        private readonly List<DrawItem> _draws = new();
        private readonly List<IDisposable> _owned = new();
        private readonly GraphicsDevice _device;

        /// <summary>Every renderer here, for the host's sky-lit enrolment.</summary>
        public IReadOnlyList<InstancedModelRenderer> Renderers => _renderers;

        /// <summary>How many vertices the backdrop hands V3D a frame, the price #829 measures a scene by.</summary>
        public int VertexCount { get; private set; }

        /// <summary>The same per draw, for the load line: instances x the mesh's vertices, in draw order.</summary>
        public string Census => string.Join(", ", _census);
        private readonly List<string> _census = new();

        private PotatoBackdrop(GraphicsDevice device)
        {
            _device = device;
        }

        /// <summary>
        /// The backdrop of <paramref name="scene"/>, or null for a scene that has none on this path yet. The savanna
        /// (#829's first): the terrain of <see cref="TerrainMirror.Savanna"/> in a blend of the grass's two colours, and
        /// <see cref="SavannaScatter"/>'s trees, scrub, mounds, rocks and logs as the desktop's Low tier plants them. The
        /// desert: its dunes (<see cref="TerrainMirror.Desert"/>) in the sand's two colours, nothing planted. The meadow:
        /// its ground (<see cref="TerrainMirror.Meadow"/>, the pond a dip) in its grass, and <see cref="MeadowScatter"/>'s
        /// oaks, the fence and the rest. The mountains: the range of <see cref="TerrainMirror.Mountain"/> as two grids
        /// over one field, snow where the desktop's rule lays it and rock where not. The Moon: the plain of
        /// <see cref="TerrainMirror.Moon"/> in the regolith's grey and <see cref="MoonPlanting"/>'s boulders. The volcano: the
        /// cone of <see cref="TerrainMirror.Volcano"/> in its basalt, with <see cref="VolcanoLava"/>'s rivers as glowing
        /// ribbons and the lake in its crater - the rivers the lamps of #795 already ride.
        /// </summary>
        /// <param name="seedOffset">The scene's roll for the launch, the desktop's <c>SceneRenderer</c> seed: the Moon's
        /// boulders and the volcano's rivers fall where the desktop's would, and the rivers where the Potato lamps run.</param>
        /// <param name="cityConfig">The host's city config, its seeds already rolled for the launch: the two cities
        /// (<see cref="SceneKind.City"/>, <see cref="SceneKind.NeonCity"/>) are <see cref="City"/>'s towers and cornices
        /// as the desktop lays them out, one unit box each, the facade's colour and no windows (the facade program is the
        /// desktop's dearest pixel), over the street level as a plane of asphalt.</param>
        public static PotatoBackdrop For(SceneKind scene, GraphicsDevice graphicsDevice, Effect potatoEffect, float holeRadius, int seedOffset,
            CitySceneConfig cityConfig = null)
        {
            return scene switch
            {
                SceneKind.Savanna => Savanna(graphicsDevice, potatoEffect, holeRadius),
                SceneKind.Desert => Desert(graphicsDevice, potatoEffect, holeRadius),
                SceneKind.Meadow => Meadow(graphicsDevice, potatoEffect, holeRadius),
                SceneKind.Mountain => Mountain(graphicsDevice, potatoEffect, holeRadius),
                SceneKind.Moon => Moon(graphicsDevice, potatoEffect, holeRadius, seedOffset),
                SceneKind.Volcano => Volcano(graphicsDevice, potatoEffect, holeRadius, seedOffset),
                SceneKind.City when cityConfig != null => CityOf(graphicsDevice, potatoEffect, cityConfig, neon: false),
                SceneKind.NeonCity when cityConfig != null => CityOf(graphicsDevice, potatoEffect, cityConfig, neon: true),
                SceneKind.Cavern => Cavern(graphicsDevice, potatoEffect),
                SceneKind.Grid => Grid(graphicsDevice, potatoEffect, holeRadius, seedOffset),
                SceneKind.Circus => BigTop(graphicsDevice, potatoEffect, holeRadius),
                _ => null,
            };
        }

        /// <summary>
        /// The big top (#829): the desktop's is one analytic full-screen pass (<c>Circus.fx</c>), a handful of quadrics
        /// shaded as cloth, velvet, seats and sawdust. Here the same quadrics as shells seen from inside: the canvas as
        /// two cones over one, the red panels and the cream, closed by the crown; the curtained wall; the seats as the
        /// rake from the ring's edge to the wall; the curb round the ring; the sawdust floor with the arena's hole; and
        /// the king poles as boxes where the shader stands them. No bulbs and no spots: the house light is the rig's.
        /// </summary>
        private static PotatoBackdrop BigTop(GraphicsDevice graphicsDevice, Effect potatoEffect, float holeRadius)
        {
            var config = new CircusSceneConfig();
            CircusTentConfig tent = config.Tent;
            CircusRingConfig ring = config.Ring;
            CircusSeatingConfig seating = config.Seating;
            float floor = ring.FloorY;
            ModelInstance[] one = { new ModelInstance(Matrix.Identity, OPEN) };

            var backdrop = new PotatoBackdrop(graphicsDevice);
            backdrop.AddPiece(graphicsDevice, potatoEffect, backdrop.Own(new PotatoTerrainMesh(graphicsDevice, 2, tent.WallRadius, holeRadius, (x, z) => floor)), one, ring.Sawdust.ToVector3(), null, "floor");
            backdrop.AddPiece(graphicsDevice, potatoEffect, backdrop.Own(new PotatoShellMesh(graphicsDevice, ring.CurbRadius, ring.CurbRadius, floor, floor + ring.CurbHeight, 48, closeTop: false)), one, ring.CurbRed.ToVector3(), null, "curb");

            //The rake: a row's rise for every row's depth from the ring's edge to the wall
            float rows = MathF.Max(1f, (seating.OuterRadius - seating.InnerRadius) / MathF.Max(seating.RowDepth, 0.1f));
            backdrop.AddPiece(graphicsDevice, potatoEffect, backdrop.Own(new PotatoShellMesh(graphicsDevice, seating.InnerRadius, seating.OuterRadius, floor, floor + rows * seating.RowRise, 48, closeTop: false)),
                one, seating.SeatRed.ToVector3(), null, "seats");
            backdrop.AddPiece(graphicsDevice, potatoEffect, backdrop.Own(new PotatoShellMesh(graphicsDevice, tent.WallRadius, tent.WallRadius, floor, tent.WallTopY, 48, closeTop: false)), one, tent.Curtain.ToVector3(), null, "wall");

            //The canvas: the shader's panels, alternating; an odd count is doubled so the ring closes on a pair
            int panels = Math.Max(8, tent.PanelCount);
            if (panels % 2 == 1) panels *= 2;
            backdrop.AddPiece(graphicsDevice, potatoEffect, backdrop.Own(new PotatoShellMesh(graphicsDevice, tent.WallRadius, tent.CrownRadius, tent.WallTopY, tent.CrownY, panels, i => i % 2 == 0)), one, tent.Red.ToVector3(), null, "canvas red");
            backdrop.AddPiece(graphicsDevice, potatoEffect, backdrop.Own(new PotatoShellMesh(graphicsDevice, tent.WallRadius, tent.CrownRadius, tent.WallTopY, tent.CrownY, panels, i => i % 2 == 1, closeTop: false)), one, tent.Cream.ToVector3(), null, "canvas cream");

            //The king poles where Circus.fx stands them: (i + 0.5) of the circle each, at the pole radius, floor to crown
            int poleCount = Math.Max(0, tent.KingPoleCount);
            if (poleCount > 0)
            {
                var poles = new ModelInstance[poleCount];
                float thickness = tent.KingPoleThickness * 2f;
                for (int i = 0; i < poleCount; i++)
                {
                    float angle = (i + 0.5f) * MathHelper.TwoPi / poleCount;
                    poles[i] = new ModelInstance(Matrix.CreateScale(thickness, tent.CrownY - floor, thickness)
                        * Matrix.CreateTranslation(MathF.Cos(angle) * tent.KingPoleRadius, (floor + tent.CrownY) * 0.5f, MathF.Sin(angle) * tent.KingPoleRadius), OPEN);
                }
                backdrop.AddPiece(graphicsDevice, potatoEffect, backdrop.Own(new BoxMesh(graphicsDevice, 1f, 1f, 1f)), poles, tent.PoleColor.ToVector3(), null, "poles");
            }

            return backdrop;
        }

        private const int CAVE_SEGMENTS = 64, CRYSTAL_CLUSTERS = 8, CRYSTALS_PER_CLUSTER = 5;

        /// <summary>
        /// The cavern (#829): the desktop's is one marched full-screen pass (<c>Cavern.fx</c>), a noise-carved cylinder
        /// under a ceiling, the river as a plane and eight crystal clusters on the wall. Here the cylinder and the
        /// ceiling as a shell in the rock's colour, the river as a plane with a little of its glow, and the clusters as
        /// leaning boxes radiating the cluster's colour, at <c>CrystalCenter</c>'s places (the shader's formula, as
        /// <c>CavernBackdrop.CrystalCenter</c> mirrors it).
        /// </summary>
        private static PotatoBackdrop Cavern(GraphicsDevice graphicsDevice, Effect potatoEffect)
        {
            var config = new CavernSceneConfig();
            float water = config.Water.LevelY, ceiling = config.Rock.CeilingY, radius = config.Rock.CaveRadius;

            var backdrop = new PotatoBackdrop(graphicsDevice);
            backdrop.AddPiece(graphicsDevice, potatoEffect, backdrop.Own(new PotatoShellMesh(graphicsDevice, radius, radius, water - 2f, ceiling, CAVE_SEGMENTS)),
                new[] { new ModelInstance(Matrix.Identity, OPEN) }, config.Rock.RockColor.ToVector3(), null, "cave");
            backdrop.AddPiece(graphicsDevice, potatoEffect, backdrop.Own(new PotatoTerrainMesh(graphicsDevice, 2, radius, 0f, (x, z) => water)),
                new[] { new ModelInstance(Matrix.Identity, OPEN) }, config.Water.DeepColor.ToVector3(), config.Water.GlowColor.ToVector3() * 0.12f, "river");

            BoxMesh box = backdrop.Own(new BoxMesh(graphicsDevice, 1f, 1f, 1f));
            for (int k = 0; k < CRYSTAL_CLUSTERS; k++)
            {
                //Cavern.fx's CrystalCenter(k): on the wall at nearly the full radius, at a golden-angle bearing, staggered in height
                float angle = k * 2.39996f + 0.7f;
                Vector3 centre = new(MathF.Cos(angle) * radius * 0.965f, water + 6f + (k * 37f) % 70f, MathF.Sin(angle) * radius * 0.965f);
                Vector3 inward = Vector3.Normalize(new Vector3(-centre.X, 0f, -centre.Z));
                Vector3 colour = Vector3.Lerp(config.Crystals.ColorA.ToVector3(), config.Crystals.ColorB.ToVector3(), ShaderMath.Frac(k * 0.381f));

                var rng = new Random(1131 + k);
                var spikes = new ModelInstance[CRYSTALS_PER_CLUSTER];
                for (int j = 0; j < spikes.Length; j++)
                {
                    //A spike grows out of the wall, leaning off the inward line by its own angle, a few units from its neighbours
                    //Bigger than the desktop's growths: a cluster stands 230 units from the gun, and a spike of the shader's size
                    //was a pale fleck there
                    float height = MathHelper.Lerp(12f, 24f, (float)rng.NextDouble());
                    float width = MathHelper.Lerp(2.5f, 4.5f, (float)rng.NextDouble());
                    Vector3 up = Vector3.Normalize(Vector3.Lerp(Vector3.Up, inward, MathHelper.Lerp(0.3f, 0.8f, (float)rng.NextDouble()))
                        + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.5f);
                    Vector3 right = Vector3.Normalize(Vector3.Cross(up, MathF.Abs(up.Y) > 0.9f ? Vector3.Right : Vector3.Up));
                    Vector3 backward = Vector3.Cross(right, up);
                    Vector3 foot = centre + new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 8f;

                    Matrix world = Matrix.CreateScale(width, height, width);
                    Matrix orient = Matrix.Identity;
                    orient.Right = right;
                    orient.Up = up;
                    orient.Backward = backward;
                    orient.Translation = foot + up * (height * 0.35f);
                    spikes[j] = new ModelInstance(world * orient, OPEN);
                }

                backdrop.AddPiece(graphicsDevice, potatoEffect, box, spikes, colour * 0.25f, Vector3.Clamp(colour * 0.9f, Vector3.Zero, Vector3.One), $"crystals {k}");
            }

            return backdrop;
        }

        /// <summary>
        /// The Grid (#829): the desktop's is <c>Grid.fx</c>, a circuit-board floor, distant solids running Life on their
        /// faces and a void. Here the floor as a plane with the arena's hole, a little of the traces' light in it, and the
        /// solids as boxes in their body colour with a share of their windows' light: as many as the config asks for, in
        /// its band of radii and sizes, cubes first, each kept a gap off the ones before it (the desktop's own retry),
        /// on the config's seed and the launch's roll. Not the desktop's stream to the number: its draws are shared
        /// with the Life boards and the landmark ring, which this path does not build.
        /// </summary>
        private static PotatoBackdrop Grid(GraphicsDevice graphicsDevice, Effect potatoEffect, float holeRadius, int seedOffset)
        {
            var config = new GridSceneConfig();
            GridTowerConfig towers = config.Towers;
            float level = config.Terrain.LevelY;

            var backdrop = new PotatoBackdrop(graphicsDevice);
            backdrop.AddPiece(graphicsDevice, potatoEffect, backdrop.Own(new PotatoTerrainMesh(graphicsDevice, 2, config.Terrain.HorizonHazeDistance, holeRadius, (x, z) => level)),
                new[] { new ModelInstance(Matrix.Identity, OPEN) }, Vector3.Lerp(config.Terrain.BodyColor.ToVector3(), config.Terrain.LineColor.ToVector3(), 0.2f),
                config.Terrain.LineColor.ToVector3() * 0.25f, "floor");

            const float SOLID_GAP = 15f;
            var placement = new Random(towers.Seed + seedOffset);
            int cubeCount = (int)MathF.Round(towers.Count * MathHelper.Clamp(towers.CubeFraction, 0f, 1f));
            var placed = new List<(Vector2 Centre, float Radius)>();
            var solids = new List<ModelInstance>();
            for (int i = 0; i < Math.Max(towers.Count, 0); i++)
            {
                bool cube = i < cubeCount;
                Vector3 size = default;
                Vector2 xz = default;
                float footprint = 0f;
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    float angle = (float)(placement.NextDouble() * MathHelper.TwoPi);
                    float radius = MathHelper.Lerp(towers.RadiusMin, towers.RadiusMax, (float)placement.NextDouble());
                    if (cube)
                    {
                        float side = MathHelper.Lerp(towers.CubeSizeMin, towers.CubeSizeMax, (float)placement.NextDouble());
                        size = new Vector3(side * (0.94f + 0.12f * (float)placement.NextDouble()), side * (0.94f + 0.12f * (float)placement.NextDouble()), side * (0.94f + 0.12f * (float)placement.NextDouble()));
                    }
                    else
                    {
                        size = new Vector3(MathHelper.Lerp(towers.TowerFootprintMin, towers.TowerFootprintMax, (float)placement.NextDouble()),
                            MathHelper.Lerp(towers.TowerHeightMin, towers.TowerHeightMax, (float)placement.NextDouble()),
                            MathHelper.Lerp(towers.TowerFootprintMin, towers.TowerFootprintMax, (float)placement.NextDouble()));
                    }
                    xz = new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
                    footprint = 0.5f * MathF.Sqrt(size.X * size.X + size.Z * size.Z);
                    bool overlaps = false;
                    foreach ((Vector2 other, float otherRadius) in placed)
                        if (Vector2.Distance(xz, other) < footprint + otherRadius + SOLID_GAP) { overlaps = true; break; }
                    if (!overlaps) break;
                }
                placed.Add((xz, footprint));
                solids.Add(new ModelInstance(Matrix.CreateScale(size) * Matrix.CreateTranslation(xz.X, level + size.Y * 0.5f, xz.Y), OPEN));
            }

            BoxMesh box = backdrop.Own(new BoxMesh(graphicsDevice, 1f, 1f, 1f));
            backdrop.AddPiece(graphicsDevice, potatoEffect, box, solids.ToArray(), towers.BodyColor.ToVector3() * 2f,
                Vector3.Clamp(towers.WindowColor.ToVector3() * 0.14f, Vector3.Zero, Vector3.One), "solids");

            return backdrop;
        }

        private const float CITY_REACH = 250f;

        private static PotatoBackdrop CityOf(GraphicsDevice graphicsDevice, Effect potatoEffect, CitySceneConfig config, bool neon)
        {
            var backdrop = new PotatoBackdrop(graphicsDevice);

            //The same generator and seed as the host's own City, so the skyline is the one the desktop would stand under
            //this launch; its own instance, since the host's is the other city until SetScene's EnsureCityLayout runs
            var city = new City(config, neon, ArenaIsland.RADIUS);
            Opened(city.Buildings);

            //The near blocks only, sorted near to far from the island (#829): a tower beyond the near ranks shows only its
            //top over them, if that, and the whole neon city is 5,700 boxes to 16 blocks out. The cull per frame is the
            //frustum's; the order is the island's, which the camera orbits
            var near = new List<(float Distance, ModelInstance Instance, BoundingSphere Bounds)>();
            foreach (ModelInstance building in city.Buildings)
            {
                Vector3 centre = building.World.Translation;
                float distance = MathF.Sqrt(centre.X * centre.X + centre.Z * centre.Z);
                if (distance > CITY_REACH) continue;
                Vector3 scale = new(building.World.Right.Length(), building.World.Up.Length(), building.World.Backward.Length());
                near.Add((distance, building, new BoundingSphere(centre, scale.Length() * 0.5f)));
            }
            near.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            var towers = new ModelInstance[near.Count];
            var bounds = new BoundingSphere[near.Count];
            for (int i = 0; i < near.Count; i++) (towers[i], bounds[i]) = (near[i].Instance, near[i].Bounds);
            var visible = new ModelInstance[near.Count];
            var frustum = new BoundingFrustum(Matrix.Identity);
            int PrepareVisible(ICamera camera)
            {
                frustum.Matrix = camera.View * camera.Projection;
                int count = 0;
                for (int i = 0; i < towers.Length; i++)
                    if (frustum.Contains(bounds[i]) != ContainmentType.Disjoint) visible[count++] = towers[i];
                return count;
            }

            //The street level: a plane at the city's ground, as far as its blocks reach, in the asphalt's colour. The
            //island stands a hundred units over it, so no hole
            float reach = (city.RadiusBlocks + 0.5f) * city.BlockPitch;
            float ground = city.GroundY;
            backdrop.AddGround(graphicsDevice, potatoEffect, new PotatoTerrainMesh(graphicsDevice, 2, reach, 0f, (x, z) => ground), config.Streets.AsphaltColor.ToVector3());

            Vector3 facade = (neon ? config.FacadeNeonColor : config.FacadeColor).ToVector3();
            BoxMesh box = backdrop.Own(new BoxMesh(graphicsDevice, 1f, 1f, 1f));
            var renderer = new InstancedModelRenderer(graphicsDevice, box, ColorSpace.LinearToSrgb(facade), potatoEffect) { PotatoVertexLit = true, SpecularAmbientStrength = 0f };
            backdrop._owned.Add(renderer);
            backdrop._renderers.Add(renderer);
            backdrop._draws.Add(new DrawItem { Renderer = renderer, Instances = visible, Prepare = PrepareVisible });
            backdrop.VertexCount += box.PrimitiveCount * 3 * towers.Length;
            backdrop._census.Add($"city {towers.Length}x{box.PrimitiveCount * 3} of {city.Buildings.Length} within {CITY_REACH:F0} ({city.TowerCount} towers in all; the frustum's set a frame)");

            return backdrop;
        }

        /// <summary>
        /// The instances' Custom made the Potato effect's open occluder, in place: the city writes a building's facade
        /// kind there for the desktop's program, with a w of 0 the Potato effect reads as buried (the Moon's boulders'
        /// lesson, <see cref="AddScatter"/>).
        /// </summary>
        public static void Opened(ModelInstance[] instances)
        {
            var open = new Vector4(0f, 0f, 0f, 1f);
            for (int i = 0; i < instances.Length; i++) instances[i] = new ModelInstance(instances[i].World, open);
        }

        //The grids of the scenes whose relief is the backdrop: finer than the open ground's, and the mountains' out to
        //where their haze would be (HorizonHazeDistance 500), the volcano's to the cone's far foot (ConeCenter.Y -
        //ConeRadius is -465), the Moon's past the highland crest (310) where its curvature has closed the horizon
        private const int RELIEF_CELLS = 96;
        private const float MOUNTAIN_EXTENT = 600f, VOLCANO_EXTENT = 560f, MOON_EXTENT = 480f;

        private static PotatoBackdrop Mountain(GraphicsDevice graphicsDevice, Effect potatoEffect, float holeRadius)
        {
            var config = new MountainSceneConfig();
            float Height(float x, float z) => TerrainMirror.Mountain(x, z, config);

            //Mountain.fx's rule, at the middle of each of its bands: snow lies where the face is gentler than the middle of
            //RockSlope..SnowSlope and either above the middle of the snowline or on the clearing's floor (#504: the basin
            //is snow), rock everywhere else. A triangle is one or the other; the desktop's edge is sharp too
            float slope = (config.RockSlope + config.SnowSlope) * 0.5f;
            float snowline = (config.SnowlineLow + config.SnowlineHigh) * 0.5f;
            float floor = config.ClearingRadius + config.ClearingTransition * 0.3f;
            bool Snowy(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
                if (MathF.Abs(normal.Y) < slope) return false;
                Vector3 centre = (a + b + c) / 3f;
                return centre.Y > snowline || MathF.Sqrt(centre.X * centre.X + centre.Z * centre.Z) < floor;
            }

            var backdrop = new PotatoBackdrop(graphicsDevice);
            Vector3 rock = Vector3.Lerp(config.RockColor.ToVector3(), config.RockColorLight.ToVector3(), 0.5f);
            backdrop.AddGround(graphicsDevice, potatoEffect, new PotatoTerrainMesh(graphicsDevice, RELIEF_CELLS, MOUNTAIN_EXTENT, holeRadius, Height, Snowy), config.SnowColor.ToVector3());
            backdrop.AddGround(graphicsDevice, potatoEffect, new PotatoTerrainMesh(graphicsDevice, RELIEF_CELLS, MOUNTAIN_EXTENT, holeRadius, Height, (a, b, c) => !Snowy(a, b, c)), rock);

            return backdrop;
        }

        //MoonBackdrop's PLANTING_SEED: the same boulders in the same places as the desktop's for a launch
        private const int MOON_PLANTING_SEED = 8125;

        private static PotatoBackdrop Moon(GraphicsDevice graphicsDevice, Effect potatoEffect, float holeRadius, int seedOffset)
        {
            MoonTerrainConfig terrain = new MoonSceneConfig().Terrain;
            float Height(float x, float z) => TerrainMirror.Moon(x, z, terrain);

            var backdrop = new PotatoBackdrop(graphicsDevice);
            Vector3 regolith = Vector3.Lerp(terrain.RegolithColor.ToVector3(), terrain.RegolithColorPale.ToVector3(), 0.3f);
            backdrop.AddGround(graphicsDevice, potatoEffect, new PotatoTerrainMesh(graphicsDevice, RELIEF_CELLS, MOON_EXTENT, holeRadius, Height), regolith);

            //The boulders stand on the ground as THIS grid draws it (MoonPlanting's height), so a rim block sits on the
            //drawn rim: the grid here is a vertex every 10 units, the desktop's every 3.34
            var planting = new MoonPlanting(graphicsDevice,
                (x, z) => TerrainMirror.OnGrid(Height, RELIEF_CELLS + 1, MOON_EXTENT * 2f, x, z), terrain, MOON_PLANTING_SEED + seedOffset);
            backdrop._owned.Add(planting);
            //The weathered boulders are 480 vertices each and 800 of them stand on the plain (384k a frame, +0.7 ms); a
            //rounded boulder is its own ellipsoid. The angular blocks (60) stay themselves
            backdrop.AddScatter(graphicsDevice, potatoEffect, planting.Buckets, blobOver: 400);

            return backdrop;
        }

        private const int RIVER_STEPS = 28;

        private static PotatoBackdrop Volcano(GraphicsDevice graphicsDevice, Effect potatoEffect, float holeRadius, int seedOffset)
        {
            var config = new VolcanoSceneConfig();
            float Height(float x, float z) => TerrainMirror.Volcano(x, z, config);

            var backdrop = new PotatoBackdrop(graphicsDevice);
            Vector3 basalt = Vector3.Lerp(config.RockColor.ToVector3(), config.RockColorLight.ToVector3(), 0.5f);
            backdrop.AddGround(graphicsDevice, potatoEffect, new PotatoTerrainMesh(graphicsDevice, RELIEF_CELLS, VOLCANO_EXTENT, holeRadius, Height), basalt);

            //The rivers as the lamps' own solver lays them (BS3DGame's SceneLights.Volcano is a VolcanoLava on the same
            //seed): a ribbon from the crater's rim down the flank, a hand's breadth over the ground, radiating the crust's
            //cool lava; the lake in the crater a flat lens of the hot. Both are the colours Volcano.fx paints with, linear
            var lava = new VolcanoLava(config, seedOffset);
            Vector2 cone = config.ConeCenter.ToVector2();
            Vector3 cool = ColorSpace.LinearToSrgb(config.LavaCool.ToVector3());
            Vector3 hot = ColorSpace.LinearToSrgb(Vector3.Clamp(config.LavaCool.ToVector3() * 2f, Vector3.Zero, Vector3.One));
            Vector3 crust = ColorSpace.LinearToSrgb(config.CrustColor.ToVector3());

            for (int r = 0; r < lava.RiverCount; r++)
            {
                float bearing = lava.RiverBearing[r];
                Vector2 along = new(MathF.Cos(bearing), MathF.Sin(bearing));
                Vector2 across = new Vector2(-along.Y, along.X) * (config.RiverWidth * 0.5f);
                var left = new Vector3[RIVER_STEPS + 1];
                var right = new Vector3[RIVER_STEPS + 1];
                for (int i = 0; i <= RIVER_STEPS; i++)
                {
                    float d = MathHelper.Lerp(config.CraterRadius * 0.9f, lava.RiverReach[r], (float)i / RIVER_STEPS);
                    Vector2 centre = cone + along * d;
                    //Narrowing towards its front, as a flow does
                    Vector2 half = across * MathHelper.Lerp(1f, 0.45f, (float)i / RIVER_STEPS);
                    Vector2 l = centre - half, rr = centre + half;
                    left[i] = new Vector3(l.X, Height(l.X, l.Y) + 0.6f, l.Y);
                    right[i] = new Vector3(rr.X, Height(rr.X, rr.Y) + 0.6f, rr.Y);
                }
                backdrop.AddGlow(graphicsDevice, potatoEffect, backdrop.Own(new PotatoStripMesh(graphicsDevice, left, right)), Matrix.Identity, crust, cool, $"river {r}");
            }

            float floor = Height(cone.X, cone.Y);
            float lake = config.CraterRadius * 0.7f;
            SphereMesh lens = backdrop.Own(new SphereMesh(graphicsDevice, 1f, PROXY_SLICES, PROXY_STACKS));
            backdrop.AddGlow(graphicsDevice, potatoEffect, lens, Matrix.CreateScale(lake, 1.5f, lake) * Matrix.CreateTranslation(cone.X, floor + 1f, cone.Y), crust, hot, "lake");

            return backdrop;
        }

        //The grid over an open ground: the savanna's HorizonHazeDistance (520) is where the desktop's ground fades into
        //the haze, and the other open scenes reach about as far
        private const int SAVANNA_CELLS = 64;
        private const float SAVANNA_EXTENT = 480f;

        private static PotatoBackdrop Desert(GraphicsDevice graphicsDevice, Effect potatoEffect, float holeRadius)
        {
            var config = new DesertSceneConfig();
            float Height(float x, float z) => TerrainMirror.Desert(x, z, config);

            var backdrop = new PotatoBackdrop(graphicsDevice);
            Vector3 sand = Vector3.Lerp(config.SandColor.ToVector3(), config.SandColorPale.ToVector3(), 0.4f);
            backdrop.AddGround(graphicsDevice, potatoEffect, new PotatoTerrainMesh(graphicsDevice, SAVANNA_CELLS, SAVANNA_EXTENT, holeRadius, Height), sand);

            return backdrop;
        }

        private static PotatoBackdrop Meadow(GraphicsDevice graphicsDevice, Effect potatoEffect, float holeRadius)
        {
            var config = new MeadowSceneConfig();
            float Height(float x, float z) => TerrainMirror.Meadow(x, z, config);

            var backdrop = new PotatoBackdrop(graphicsDevice);
            Vector3 grass = Vector3.Lerp(config.GrassColor.ToVector3(), config.GrassColorDark.ToVector3(), 0.35f);
            backdrop.AddGround(graphicsDevice, potatoEffect, new PotatoTerrainMesh(graphicsDevice, SAVANNA_CELLS, SAVANNA_EXTENT, holeRadius, Height), grass);

            var scatter = new MeadowScatter(graphicsDevice, config, MeadowScatter.DEFAULT_SEED);
            backdrop._owned.Add(scatter);
            backdrop.AddScatter(graphicsDevice, potatoEffect, scatter.Buckets);

            return backdrop;
        }

        //A scatter mesh with more vertices than this is drawn as a blob (#829): the savanna as the desktop plants it is
        //1.2 million vertices a frame (a canopy is 9,000, a termite mound 50,000), and V3D's binning runs the coordinate
        //shader for every vertex handed over (#804), so a far plant is its silhouette - an ellipsoid of its bounding
        //sphere in its own colour, 26 vertices - and only a mesh this small (an acacia's trunk, 960) is itself
        private const int PROXY_OVER_VERTICES = 1200;
        private const int PROXY_SLICES = 8, PROXY_STACKS = 4;
        private const float GROUND_PATCH_RADIUS = 30f;

        private static PotatoBackdrop Savanna(GraphicsDevice graphicsDevice, Effect potatoEffect, float holeRadius)
        {
            var config = new SavannaSceneConfig();
            float Height(float x, float z) => TerrainMirror.Savanna(x, z, config);

            var backdrop = new PotatoBackdrop(graphicsDevice);

            Vector3 ground = Vector3.Lerp(config.GrassSavanna.ToVector3(), config.GrassDry.ToVector3(), 0.45f);
            backdrop.AddGround(graphicsDevice, potatoEffect, new PotatoTerrainMesh(graphicsDevice, SAVANNA_CELLS, SAVANNA_EXTENT, holeRadius, Height), ground);

            var scatter = new SavannaScatter(graphicsDevice, config, Height, Array.Empty<ScatterSpacing.Footprint>());
            backdrop._owned.Add(scatter);
            backdrop.AddScatter(graphicsDevice, potatoEffect, scatter.Buckets);

            return backdrop;
        }

        /// <param name="colour">The ground's colour as the scene's config gives it, linear; encoded here like a bucket's,
        /// since the renderer's material is sRGB and decoded (the first three grounds were handed linear and came out dark
        /// and, under the dome's reflection, mauve)</param>
        private void AddGround(GraphicsDevice graphicsDevice, Effect potatoEffect, PotatoTerrainMesh mesh, Vector3 colour)
        {
            _owned.Add(mesh);

            //Matte: no dome in it. Schlick's rise reaches the whole dome at a grazing angle whatever the reflectance, and a
            //ground seen from a gun's height is grazing from a few dozen units out - the far rim of the first grids was a
            //white band of sky where the desktop has haze, and the desert's dunes the dome's mauve
            var renderer = new InstancedModelRenderer(graphicsDevice, mesh, ColorSpace.LinearToSrgb(colour), potatoEffect) { PotatoVertexLit = true, SpecularAmbientStrength = 0f };
            _owned.Add(renderer);
            _renderers.Add(renderer);
            _draws.Add(new DrawItem { Renderer = renderer, Instances = new[] { new ModelInstance(Matrix.Identity, new Vector4(0f, 0f, 0f, 1f)) } });
            VertexCount += mesh.PrimitiveCount * 3;
            _census.Add($"ground {mesh.PrimitiveCount * 3}");
        }

        private static readonly Vector4 OPEN = new(0f, 0f, 0f, 1f);

        /// <summary>
        /// Any mesh under any instances, matte and lit at the vertex, in <paramref name="colour"/> and radiating
        /// <paramref name="glow"/> if there is one, both linear as a config gives them.
        /// </summary>
        private void AddPiece(GraphicsDevice graphicsDevice, Effect potatoEffect, IProceduralMesh mesh, ModelInstance[] instances, Vector3 colour, Vector3? glow, string what)
        {
            var renderer = new InstancedModelRenderer(graphicsDevice, mesh, ColorSpace.LinearToSrgb(colour), potatoEffect) { PotatoVertexLit = true, SpecularAmbientStrength = 0f };
            _owned.Add(renderer);
            _renderers.Add(renderer);
            _draws.Add(new DrawItem { Renderer = renderer, Instances = instances, Glow = glow.HasValue ? ColorSpace.LinearToSrgb(glow.Value) : null });
            VertexCount += mesh.PrimitiveCount * 3 * instances.Length;
            _census.Add($"{what} {instances.Length}x{mesh.PrimitiveCount * 3}");
        }

        /// <summary>A thing that radiates its own light (the lava): its casing <paramref name="colour"/> and its <paramref name="glow"/>, both sRGB, and no dome on it either.</summary>
        private void AddGlow(GraphicsDevice graphicsDevice, Effect potatoEffect, IProceduralMesh mesh, Matrix world, Vector3 colour, Vector3 glow, string what)
        {
            var renderer = new InstancedModelRenderer(graphicsDevice, mesh, colour, potatoEffect) { PotatoVertexLit = true, SpecularAmbientStrength = 0f };
            _owned.Add(renderer);
            _renderers.Add(renderer);
            _draws.Add(new DrawItem { Renderer = renderer, Instances = new[] { new ModelInstance(world, new Vector4(0f, 0f, 0f, 1f)) }, Glow = glow });
            VertexCount += mesh.PrimitiveCount * 3;
            _census.Add($"{what} {mesh.PrimitiveCount * 3}");
        }

        /// <param name="blobOver">A mesh with more vertices than this is drawn as a blob of its bounding sphere.</param>
        //A trunk's column: a unit cylinder, a little narrower at the top, closed, seen from outside; scaled per tree
        private const int COLUMN_SEGMENTS = 8;
        private const float TRUNK_WIDTH = 0.16f, TRUNK_OVER_CENTRE = 0.2f;

        private void AddScatter(GraphicsDevice graphicsDevice, Effect potatoEffect, ScatterBucket[] buckets, int blobOver = PROXY_OVER_VERTICES)
        {
            SphereMesh proxy = null;
            PotatoShellMesh column = null;

            foreach (ScatterBucket bucket in buckets)
            {
                //The desktop's Low tier: the detail buckets (the grass tufts) left out, the ones it plants instead kept
                if (bucket.DetailOnly || bucket.Count == 0) continue;

                bool leaves = bucket.Leaves > 0f;
                bool blob = bucket.Mesh.PrimitiveCount * 3 > blobOver;
                IProceduralMesh mesh = bucket.Mesh;

                //A bucket of one wide, flat thing is a patch on the ground (the bare earth, the dry grass round the arena):
                //the grid is the ground here, and a blob of it would stand round the camera (seen: a black frame)
                if (blob && bucket.Count == 1 && bucket.Mesh.BoundingSphere.Radius > GROUND_PATCH_RADIUS) continue;

                bool trunkBucket = blob && bucket.Bark >= 0.5f;
                if (blob)
                {
                    if (trunkBucket) mesh = column ??= Own(new PotatoShellMesh(graphicsDevice, 0.5f, 0.4f, 0f, 1f, COLUMN_SEGMENTS, closeTop: true, inside: false));
                    else mesh = proxy ??= Own(new SphereMesh(graphicsDevice, 1f, PROXY_SLICES, PROXY_STACKS));
                    leaves = false;
                }

                //The bucket's colour is linear (Acacia.fx reads it so); a renderer's material colour is authored sRGB and
                //decoded in DrawPotato, so it is encoded here, or a dark green wood came out black
                var renderer = new InstancedModelRenderer(graphicsDevice, mesh, ColorSpace.LinearToSrgb(bucket.Diffuse), potatoEffect) { PotatoVertexLit = true, SpecularAmbientStrength = 0f };
                if (leaves) renderer.TwoSidedNormals = 1f;

                _owned.Add(renderer);
                _renderers.Add(renderer);

                //An instance's Custom is the Potato effect's occluder (xyz) and openness (w, 1 = open), as every other draw
                //passes it; a planting writes its own figures there for the desktop's plant pass (a dryness, a jitter, a
                //stand) and its w is 0, which the effect read as a buried thing: 24 % of its light and none of the sky. The
                //first savanna's trees were that dark, and the Moon's boulders came out black
                var open = new Vector4(0f, 0f, 0f, 1f);
                var instances = new ModelInstance[bucket.Placed.Count];
                for (int i = 0; i < instances.Length; i++)
                {
                    instances[i] = new ModelInstance(bucket.Placed[i].World, open);

                    //The blob stands where the mesh's bounding sphere stands in the instance, shaped by what the bucket
                    //is: a crown, a bush or a mound is an ellipsoid wider than tall; a wood (bark) is a trunk, a column
                    //from the foot up to a little over the mesh's centre, where the crown hides its top - as an ellipsoid
                    //as tall as the branches reach it stood out of the crown as a spike, the owner's first note on the
                    //savanna and the meadow. The savanna's heavy meshes are its woods (a trunk with its branches is 5,000
                    //to 9,000 vertices; the crown over it is 960 and stays itself)
                    if (blob)
                    {
                        BoundingSphere bounds = bucket.Mesh.BoundingSphere;
                        Matrix local = trunkBucket
                            ? Matrix.CreateScale(bounds.Radius * TRUNK_WIDTH, bounds.Center.Y + bounds.Radius * TRUNK_OVER_CENTRE, bounds.Radius * TRUNK_WIDTH)
                            : Matrix.CreateScale(bounds.Radius * 0.75f, bounds.Radius * 0.5f, bounds.Radius * 0.75f) * Matrix.CreateTranslation(bounds.Center);
                        instances[i] = new ModelInstance(local * bucket.Placed[i].World, open);
                    }
                }

                _draws.Add(new DrawItem { Renderer = renderer, Instances = instances, TwoSided = leaves });
                VertexCount += mesh.PrimitiveCount * 3 * instances.Length;
                BoundingSphere census = bucket.Mesh.BoundingSphere;
                _census.Add($"{(trunkBucket ? "column" : blob ? "blob" : leaves ? "leaves" : "solid")} {instances.Length}x{mesh.PrimitiveCount * 3} {bucket.Mesh.GetType().Name} r{census.Radius:F1} c{census.Center.Y:F1} rgb({bucket.Diffuse.X:F2},{bucket.Diffuse.Y:F2},{bucket.Diffuse.Z:F2}) bark{bucket.Bark:F1} leaves{bucket.Leaves:F1}");
            }
        }

        private T Own<T>(T disposable) where T : IDisposable
        {
            _owned.Add(disposable);
            return disposable;
        }

        //The backdrop's own params: the scene's lights and ambient, a specular too small to glint (the renderer takes a
        //non-zero one as an override; the island's material is the scene's and the backdrop is matte) and the draw's
        //emission. Filled from the scene's every draw, never allocated per frame
        private readonly BasicEffectParams _drawParams = new(Vector3.Zero, Vector3.Zero, 1f, Vector3.Zero);
        private static readonly Vector3 MATTE_SPECULAR = new(0.002f);

        /// <summary>
        /// The whole backdrop, opaque and depth-written, under the scene's lights as the island is drawn but matte: no
        /// glint of the key light and (the renderers' own setting) no dome in it, since the first grids' far rim was a
        /// white band of reflected sky and the trees shone. The ground first, then the scatter bucket by bucket, then the
        /// glows with their emission. The leaf cards are one-sided sheets seen from both sides (#610), so their draws
        /// cull nothing; everything else is a wound solid.
        /// </summary>
        public void Draw(ICamera camera, BasicEffectParams sceneParams)
        {
            GraphicsDevice device = _device;
            RasterizerState raster = device.RasterizerState;
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;

            BasicEffectParams effectParams = _drawParams;
            effectParams.AmbientLightColor = sceneParams?.AmbientLightColor ?? Vector3.Zero;
            effectParams.SpecularColor = MATTE_SPECULAR;
            effectParams.SpecularPower = 8f;
            effectParams.DirectionalLight0 = sceneParams?.DirectionalLight0;
            effectParams.DirectionalLight1 = sceneParams?.DirectionalLight1;
            effectParams.DirectionalLight2 = sceneParams?.DirectionalLight2;
            effectParams.Fog = sceneParams?.Fog;

            foreach (DrawItem draw in _draws)
            {
                device.RasterizerState = draw.TwoSided ? RasterizerState.CullNone : RasterizerState.CullCounterClockwise;
                effectParams.EmissiveColor = draw.Glow ?? Vector3.Zero;

                //A city's draw is its visible set, culled to the frustum a frame: a tower behind the camera is its faces' fill saved
                int count = draw.Prepare?.Invoke(camera) ?? draw.Instances.Length;
                if (count > 0) draw.Renderer.Draw(camera, draw.Instances, count, effectParams, null);
            }

            device.RasterizerState = raster;
        }

        public void Dispose()
        {
            foreach (IDisposable owned in _owned) owned.Dispose();
            _owned.Clear();
            _renderers.Clear();
            _draws.Clear();
        }
    }
}
