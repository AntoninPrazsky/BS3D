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

        public PotatoTerrainMesh(GraphicsDevice graphicsDevice, int cells, float extent, float holeRadius, Func<float, float, float> height)
        {
            (VertexPositionNormalTexture[] vertices, int[] indices) = Build(cells, extent, holeRadius, height);

            PrimitiveCount = indices.Length / 3;

            VertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalTexture.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices);

            IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices);

            BoundingSphere = new BoundingSphere(Vector3.Zero, extent * 1.5f);
        }

        /// <summary>The grid's geometry, with no device: <c>PotatoBackdropTests</c> hold the hole and the winding.</summary>
        public static (VertexPositionNormalTexture[] Vertices, int[] Indices) Build(int cells, float extent, float holeRadius, Func<float, float, float> height)
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
                    Add(vertices, indices, a, c, b, hole2);
                    Add(vertices, indices, b, c, d, hole2);
                }

            return (vertices, indices.ToArray());
        }

        private static void Add(VertexPositionNormalTexture[] vertices, List<int> indices, int a, int b, int c, float hole2)
        {
            bool inside = true;
            foreach (int corner in new[] { a, b, c })
            {
                Vector3 p = vertices[corner].Position;
                if (p.X * p.X + p.Z * p.Z > hole2) { inside = false; break; }
            }
            if (inside) return;

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
        private readonly List<(InstancedModelRenderer Renderer, ModelInstance[] Instances, bool TwoSided)> _draws = new();
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
        /// <see cref="SavannaScatter"/>'s trees, scrub, mounds, rocks and logs as the desktop's Low tier plants them.
        /// </summary>
        public static PotatoBackdrop For(SceneKind scene, GraphicsDevice graphicsDevice, Effect potatoEffect, float holeRadius)
        {
            return scene switch
            {
                SceneKind.Savanna => Savanna(graphicsDevice, potatoEffect, holeRadius),
                _ => null,
            };
        }

        //The grid over the savanna's hills: HorizonHazeDistance (520) is where the desktop's ground fades into the haze
        private const int SAVANNA_CELLS = 64;
        private const float SAVANNA_EXTENT = 480f;

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

        private void AddGround(GraphicsDevice graphicsDevice, Effect potatoEffect, PotatoTerrainMesh mesh, Vector3 colour)
        {
            _owned.Add(mesh);

            var renderer = new InstancedModelRenderer(graphicsDevice, mesh, colour, potatoEffect) { PotatoVertexLit = true };
            _owned.Add(renderer);
            _renderers.Add(renderer);
            _draws.Add((renderer, new[] { new ModelInstance(Matrix.Identity, new Vector4(0f, 0f, 0f, 1f)) }, false));
            VertexCount += mesh.PrimitiveCount * 3;
            _census.Add($"ground {mesh.PrimitiveCount * 3}");
        }

        private void AddScatter(GraphicsDevice graphicsDevice, Effect potatoEffect, ScatterBucket[] buckets)
        {
            SphereMesh proxy = null;

            foreach (ScatterBucket bucket in buckets)
            {
                //The desktop's Low tier: the detail buckets (the grass tufts) left out, the ones it plants instead kept
                if (bucket.DetailOnly || bucket.Count == 0) continue;

                bool leaves = bucket.Leaves > 0f;
                bool blob = bucket.Mesh.PrimitiveCount * 3 > PROXY_OVER_VERTICES;
                IProceduralMesh mesh = bucket.Mesh;

                //A bucket of one wide, flat thing is a patch on the ground (the bare earth, the dry grass round the arena):
                //the grid is the ground here, and a blob of it would stand round the camera (seen: a black frame)
                if (blob && bucket.Count == 1 && bucket.Mesh.BoundingSphere.Radius > GROUND_PATCH_RADIUS) continue;

                if (blob)
                {
                    proxy ??= Own(new SphereMesh(graphicsDevice, 1f, PROXY_SLICES, PROXY_STACKS));
                    mesh = proxy;
                    leaves = false;
                }

                //The bucket's colour is linear (Acacia.fx reads it so); a renderer's material colour is authored sRGB and
                //decoded in DrawPotato, so it is encoded here, or a dark green wood came out black
                var renderer = new InstancedModelRenderer(graphicsDevice, mesh, ColorSpace.LinearToSrgb(bucket.Diffuse), potatoEffect) { PotatoVertexLit = true };
                if (leaves) renderer.TwoSidedNormals = 1f;

                _owned.Add(renderer);
                _renderers.Add(renderer);

                var instances = new ModelInstance[bucket.Placed.Count];
                for (int i = 0; i < instances.Length; i++)
                {
                    instances[i] = bucket.Placed[i];

                    //The blob stands where the mesh's bounding sphere stands in the instance, shaped by what the bucket
                    //is: a wood (bark) is a trunk, tall and thin; a crown, a bush or a mound is wider than tall. It keeps
                    //the instance's occlusion and the rest. The savanna's heavy meshes are its woods (a trunk with its
                    //branches is 5,000 to 9,000 vertices; the crown over it is 960 and stays itself)
                    if (blob)
                    {
                        BoundingSphere bounds = bucket.Mesh.BoundingSphere;
                        bool trunk = bucket.Bark >= 0.5f;
                        Vector3 size = trunk
                            ? new Vector3(bounds.Radius * 0.12f, bounds.Radius * 0.7f, bounds.Radius * 0.12f)
                            : new Vector3(bounds.Radius * 0.75f, bounds.Radius * 0.5f, bounds.Radius * 0.75f);
                        Vector3 centre = trunk ? new Vector3(0f, bounds.Center.Y, 0f) : bounds.Center;
                        Matrix local = Matrix.CreateScale(size) * Matrix.CreateTranslation(centre);
                        instances[i] = new ModelInstance(local * bucket.Placed[i].World, bucket.Placed[i].Custom);
                    }
                }

                _draws.Add((renderer, instances, leaves));
                VertexCount += mesh.PrimitiveCount * 3 * instances.Length;
                BoundingSphere census = bucket.Mesh.BoundingSphere;
                _census.Add($"{(blob ? "blob" : leaves ? "leaves" : "solid")} {instances.Length}x{mesh.PrimitiveCount * 3} {bucket.Mesh.GetType().Name} r{census.Radius:F1} c{census.Center.Y:F1} rgb({bucket.Diffuse.X:F2},{bucket.Diffuse.Y:F2},{bucket.Diffuse.Z:F2}) bark{bucket.Bark:F1} leaves{bucket.Leaves:F1}");
            }
        }

        private T Own<T>(T disposable) where T : IDisposable
        {
            _owned.Add(disposable);
            return disposable;
        }

        /// <summary>
        /// The whole backdrop, opaque and depth-written, under the scene's flat ambient as the island is drawn: the
        /// ground first, then the scatter bucket by bucket. The leaf cards are one-sided sheets seen from both sides
        /// (#610), so their draws cull nothing; everything else is a wound solid.
        /// </summary>
        public void Draw(ICamera camera, BasicEffectParams sceneParams)
        {
            GraphicsDevice device = _device;
            RasterizerState raster = device.RasterizerState;
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;

            foreach ((InstancedModelRenderer renderer, ModelInstance[] instances, bool twoSided) in _draws)
            {
                device.RasterizerState = twoSided ? RasterizerState.CullNone : RasterizerState.CullCounterClockwise;
                renderer.Draw(camera, instances, instances.Length, sceneParams, null);
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
