using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// A tree's own outline (<see cref="PlantProfile"/>, <see cref="PlantOutline"/>, #1108): what the savanna's grass home keeps a
    /// lens off where the trees' loose spheres close the plain. It must hold everything the tree draws, wherever and however the
    /// scatter plants it (a scale, a lean, a turn), say how far a point stands off it, and the scatter must hand on every tree
    /// as one and every other mesh as it was.
    /// </summary>
    public class PlantOutlineTests
    {
        private readonly ITestOutputHelper _output;

        public PlantOutlineTests(ITestOutputHelper output) => _output = output;

        //How a scatter plants a tree (SavannaScatter's Plant): a scale, a lean about a level axis, a turn and a place; the
        //lean is past the broken acacia's 0.10, the scale past the planting's 1.3
        private static readonly Matrix World = Matrix.CreateScale(1.4f)
            * Matrix.CreateFromAxisAngle(Vector3.Normalize(new Vector3(0.6f, 0f, -0.8f)), 0.14f)
            * Matrix.CreateRotationY(2.3f)
            * Matrix.CreateTranslation(41f, -12.7f, 73f);

        //Every tree the savanna plants, at the sizes its variants are rolled between (SavannaScatter: an acacia's width and
        //height as AcaciaConfig's times its TreeSize and the variant's roll; the baobab's and the doum's heights), each kind at
        //two seeds, with the triangles it draws
        private static IEnumerable<(string Name, PlantProfile Profile, List<(Vector3, Vector3, Vector3)> Triangles)> Trees()
        {
            var ac = new SavannaSceneConfig().Acacia;
            float width = ac.Width * ac.TreeSize, height = ac.Height * ac.TreeSize;
            foreach (AcaciaKind kind in new[] { AcaciaKind.Mature, AcaciaKind.Broken, AcaciaKind.Young, AcaciaKind.Dead })
                foreach (int seed in new[] { 4100, 4113 })
                {
                    AcaciaGeometry tree = AcaciaMesh.Build(kind, width * 0.09f, height, width, seed);
                    var triangles = new List<(Vector3, Vector3, Vector3)>();
                    Add(triangles, tree.WoodVertices, tree.WoodIndices);
                    Add(triangles, tree.FineWoodVertices, tree.FineWoodIndices);
                    Add(triangles, tree.LeafVertices, tree.LeafIndices);
                    if (tree.CanopyVertices != null) Add(triangles, tree.CanopyVertices, tree.CanopyIndices);
                    yield return ($"{kind} acacia {seed}", AcaciaMesh.Outline(tree), triangles);
                }

            var dr = new SavannaSceneConfig().Dressing;
            foreach (int seed in new[] { 4700, 4701 })
            {
                BaobabGeometry tree = BaobabMesh.Build(dr.BaobabHeight, seed);
                var triangles = new List<(Vector3, Vector3, Vector3)>();
                Add(triangles, tree.WoodVertices, tree.WoodIndices);
                Add(triangles, tree.LeafVertices, tree.LeafIndices);
                yield return ($"baobab {seed}", new BaobabMesh(null, dr.BaobabHeight, seed).Profile, triangles);
            }
            foreach (int seed in new[] { 4800, 4801 })
            {
                DoumGeometry tree = DoumPalmMesh.Build(dr.DoumPalmHeight, seed);
                var triangles = new List<(Vector3, Vector3, Vector3)>();
                Add(triangles, tree.WoodVertices, tree.WoodIndices);
                Add(triangles, tree.LeafVertices, tree.LeafIndices);
                yield return ($"doum {seed}", new DoumPalmMesh(null, dr.DoumPalmHeight, seed).Profile, triangles);
            }
        }

        private static void Add(List<(Vector3, Vector3, Vector3)> triangles, IReadOnlyList<VertexPositionNormalTexture> v, IReadOnlyList<short> idx)
        {
            for (int i = 0; i + 2 < idx.Count; i += 3) triangles.Add((v[idx[i]].Position, v[idx[i + 1]].Position, v[idx[i + 2]].Position));
        }

        private static void Add(List<(Vector3, Vector3, Vector3)> triangles, IReadOnlyList<VertexPositionNormalTexture> v, IReadOnlyList<int> idx)
        {
            for (int i = 0; i + 2 < idx.Count; i += 3) triangles.Add((v[idx[i]].Position, v[idx[i + 1]].Position, v[idx[i + 2]].Position));
        }

        [Fact]
        public void A_trees_outline_holds_every_triangle_it_draws_wherever_it_is_planted()
        {
            //Every corner, every edge's middle and every triangle's middle, planted: inside the outline (to a rounding) and
            //inside its box
            int trees = 0;
            foreach ((string name, PlantProfile profile, List<(Vector3 A, Vector3 B, Vector3 C)> triangles) in Trees())
            {
                Assert.NotNull(profile);
                var outline = new PlantOutline(profile, World);
                float worst = float.MinValue;
                foreach ((Vector3 a, Vector3 b, Vector3 c) in triangles)
                    foreach (Vector3 local in new[] { a, b, c, (a + b) * 0.5f, (b + c) * 0.5f, (c + a) * 0.5f, (a + b + c) / 3f })
                    {
                        Vector3 p = Vector3.Transform(local, World);
                        float off = outline.Off(p);
                        worst = MathF.Max(worst, off);
                        Assert.True(off <= 1e-3f, $"{name}: a point it draws stands {off:F3} outside its outline");
                        Assert.Equal(ContainmentType.Contains, outline.Bounds.Contains(p));
                    }

                trees++;
                _output.WriteLine($"{name}: {triangles.Count} triangles, the widest {profile.Widest:F1} from {profile.Bottom:F1} to {profile.Top:F1}, the worst point {worst:F3}");
            }
            Assert.Equal(4 * 2 + 2 + 2, trees);
        }

        [Fact]
        public void A_point_off_an_outline_is_as_far_off_as_it_stands()
        {
            //An upright stem of radius 1 from 0 to 10 at the mesh's own scale, planted at World's scale: a point straight
            //above its top, one level with its middle and one inside it
            var vertices = new List<VertexPositionNormalTexture>();
            var indices = new List<short>();
            for (int i = 0; i < 16; i++)
            {
                float a = MathHelper.TwoPi * i / 16f;
                Vector3 round = new(MathF.Cos(a), 0f, MathF.Sin(a));
                vertices.Add(new VertexPositionNormalTexture(round, round, Vector2.Zero));
                vertices.Add(new VertexPositionNormalTexture(round + Vector3.Up * 10f, round, Vector2.Zero));
                short k = (short)(2 * i), n = (short)(2 * ((i + 1) % 16));
                indices.AddRange(new[] { k, (short)(k + 1), n, n, (short)(k + 1), (short)(n + 1) });
            }
            PlantProfile profile = new PlantProfile.Builder().Add(vertices, indices).Build();
            var outline = new PlantOutline(profile, World);
            float scale = 1.4f;

            Vector3 above = Vector3.Transform(new Vector3(0f, 14f, 0f), World), beside = Vector3.Transform(new Vector3(5f, 5f, 0f), World);
            Assert.Equal(4f * scale, outline.Off(above), 2);
            Assert.Equal(4f * scale, outline.Off(beside), 2);
            Assert.True(outline.Off(Vector3.Transform(new Vector3(0f, 5.25f, 0f), World)) < 0f);

            //⚠ Asked only within a distance, a point farther off is answered with that distance itself, exactly: carried into the
            //mesh's scale and back it came out a hair short, and every point round the tree read as within it
            foreach (float within in new[] { 0.5f, 1.2f, 3f, 5.5f })
                Assert.Equal(within, outline.Off(Vector3.Transform(new Vector3(30f, 5f, 0f), World), within));
        }

        [Fact]
        public void The_scatter_hands_on_every_tree_as_its_outline_and_every_other_mesh_as_its_sphere()
        {
            //The solids are every bucket's instances in order (SavannaScatter.Solids): the ones planted under a tree's own world
            //(each of its meshes is drawn off one list of instances) are the trees', every other is beside them, in the same
            //order, and every tree has two meshes at least (its two woods). ⚠ Asked by the instance's world, not by where it
            //stands: the heap of earth a trunk rises out of is centred on the trunk, on flat ground at its very root, and is not
            //the tree's
            SavannaScatter planting = new(null, new SavannaSceneConfig(), (x, z) => 0f, new List<ScatterSpacing.Footprint>(), 7);
            var treeWorlds = new Dictionary<Matrix, int>();
            foreach (PlantOutline tree in planting.TreeOutlines) treeWorlds[tree.ToMesh] = 0;

            //A shrub drawn near the lens in a scrub's or a treeline mass's place (#1195) is no solid of its own: it stands inside its
            //dome's sphere (SavannaShapedScrubTests), and the dome's is here
            int index = 0, beside = 0;
            foreach (ScatterBucket bucket in planting.Buckets)
            {
                if (bucket.Swap != null && bucket.FineMeshes == true) continue;
                foreach (ModelInstance instance in bucket.Placed)
                {
                    PlantFigure solid = planting.Solids[index++];
                    Matrix toMesh = Matrix.Invert(instance.World);
                    if (treeWorlds.TryGetValue(toMesh, out int meshes))
                    {
                        treeWorlds[toMesh] = meshes + 1;
                        continue;
                    }
                    Assert.True(beside < planting.SolidsBesideTrees.Count, "more solids beside the trees than it hands on");
                    Assert.Equal(solid, planting.SolidsBesideTrees[beside++]);
                }
            }

            Assert.Equal(planting.Solids.Count, index);
            Assert.Equal(planting.SolidsBesideTrees.Count, beside);
            foreach (int meshes in treeWorlds.Values) Assert.True(meshes >= 2, $"a tree with {meshes} meshes");
            _output.WriteLine($"{planting.TreeOutlines.Count} trees, {planting.Solids.Count - beside} of {planting.Solids.Count} solids theirs");
        }
    }
}
