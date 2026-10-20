using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The savanna's wood is shut (#938). The owner, on the chapter intro and again on High and Ultra: "trees where the branches
    /// do not connect - there are visible holes". The plant pass culls back faces, so a ring of wood left open shows the sky
    /// through the branch, and the fine wood's boughs, limbs and twigs, the baobab's limbs, the thorn bush's stems and the doum's
    /// forks all ended in one, or met the next piece in one. Built with no device, as <see cref="LeafAttachmentTests"/> builds
    /// them, at the scatter's own figures.
    /// <para>
    /// A wood is overlapping solids, a limb starting inside its bough and a bough inside the trunk, so some rings are open on
    /// purpose, and no count of edges tells those from a hole. <see cref="MeshOpenness.ExposedLoops(IReadOnlyList{VertexPositionNormalTexture}, IReadOnlyList{short}, float)"/>
    /// does: it passes a ring only where every point of it lies inside another solid. Not covered, on purpose: the leaf cards,
    /// the grass tufts and the doum's fans (ribbons, two-sided and open by design), and the boulders, the termite mounds and
    /// the soil heaps (lathes whose buffers cannot be read back, each traced from a pole to a pole, so shut by construction).
    /// </para>
    /// </summary>
    public class SavannaMeshClosureTests
    {
        private const float ACACIA_WIDTH = 6f * 1.7f, ACACIA_HEIGHT = 9f * 1.7f, BAOBAB_HEIGHT = 24f;

        public static TheoryData<AcaciaKind, int> Acacias() => new()
        {
            { AcaciaKind.Mature, 4100 }, { AcaciaKind.Mature, 4101 }, { AcaciaKind.Mature, 4102 }, { AcaciaKind.Mature, 4103 },
            { AcaciaKind.Broken, 4110 }, { AcaciaKind.Young, 4120 }, { AcaciaKind.Young, 4121 },
            { AcaciaKind.Dead, 4130 }, { AcaciaKind.Dead, 4131 },
        };

        private static AcaciaGeometry Acacia(AcaciaKind kind, int seed)
        {
            float trunk = ACACIA_WIDTH * kind switch { AcaciaKind.Young => 0.055f, AcaciaKind.Dead => 0.085f, _ => 0.09f };
            float height = ACACIA_HEIGHT * (kind == AcaciaKind.Young ? 0.6f : 1f);
            float canopy = ACACIA_WIDTH * (kind == AcaciaKind.Young ? 0.9f : 1f);
            return AcaciaMesh.Build(kind, trunk, height, canopy, seed);
        }

        private static string First(List<MeshOpenness.OpenLoop> loops) =>
            $"{loops.Count} exposed rings, the first centred at {(loops.Count > 0 ? loops[0].Centre : Vector3.Zero)} with {(loops.Count > 0 ? loops[0].Points.Count : 0)} points";

        // ---- the savanna's meshes, as built ----

        [Theory]
        [MemberData(nameof(Acacias))]
        public void TheAcaciasCoarseWoodHasNoExposedRing(AcaciaKind kind, int seed)
        {
            AcaciaGeometry tree = Acacia(kind, seed);
            List<MeshOpenness.OpenLoop> loops = MeshOpenness.ExposedLoops(tree.WoodVertices, tree.WoodIndices);
            Assert.True(loops.Count == 0, $"{kind} {seed}, coarse wood: {First(loops)}");
        }

        [Theory]
        [MemberData(nameof(Acacias))]
        public void TheAcaciasFineWoodHasNoExposedRing(AcaciaKind kind, int seed)
        {
            AcaciaGeometry tree = Acacia(kind, seed);
            List<MeshOpenness.OpenLoop> loops = MeshOpenness.ExposedLoops(tree.FineWoodVertices, tree.FineWoodIndices);
            Assert.True(loops.Count == 0, $"{kind} {seed}, fine wood: {First(loops)}");
        }

        /// <summary>The crown's plate (the Low tier's leaf) is a closed lobed ellipsoid per tier.</summary>
        [Theory]
        [MemberData(nameof(Acacias))]
        public void TheAcaciasCanopyPlateIsClosed(AcaciaKind kind, int seed)
        {
            AcaciaGeometry tree = Acacia(kind, seed);
            if (tree.CanopyVertices == null) return;
            Assert.Empty(MeshOpenness.OpenEdges(tree.CanopyVertices, tree.CanopyIndices));
        }

        [Theory]
        [InlineData(4700)]
        [InlineData(4701)]
        [InlineData(4702)]
        public void TheBaobabsWoodHasNoExposedRing(int seed)
        {
            BaobabGeometry tree = BaobabMesh.Build(BAOBAB_HEIGHT, seed);
            List<MeshOpenness.OpenLoop> loops = MeshOpenness.ExposedLoops(tree.WoodVertices, tree.WoodIndices);
            Assert.True(loops.Count == 0, $"baobab {seed}: {First(loops)}");
        }

        public static TheoryData<float, float, int> ThornBushes() => LeafAttachmentTests.ThornBushes();

        [Theory]
        [MemberData(nameof(ThornBushes))]
        public void TheThornBushesWoodHasNoExposedRing(float radius, float height, int seed)
        {
            ThornBushGeometry bush = ThornBushMesh.Build(radius, height, seed);
            List<MeshOpenness.OpenLoop> loops = MeshOpenness.ExposedLoops(bush.WoodVertices, bush.WoodIndices);
            Assert.True(loops.Count == 0, $"thorn bush {seed}: {First(loops)}");
        }

        [Theory]
        [InlineData(4800, 0.85f)]
        [InlineData(4801, 1f)]
        [InlineData(4802, 1.15f)]
        [InlineData(4803, 1f)]
        public void TheDoumPalmsWoodHasNoExposedRing(int seed, float scale)
        {
            DoumGeometry palm = DoumPalmMesh.Build(18f * scale, seed);
            List<MeshOpenness.OpenLoop> loops = MeshOpenness.ExposedLoops(palm.WoodVertices, palm.WoodIndices);
            Assert.True(loops.Count == 0, $"doum {seed}: {First(loops)}");
        }

        /// <summary>The log and its stubs: every end sawn, each stub's first ring inside the log's wall.</summary>
        [Theory]
        [InlineData(9f, 0.9f, 4500)]
        [InlineData(7.2f, 0.58f, 4501)]
        [InlineData(10.8f, 1.2f, 4502)]
        [InlineData(9f, 0.9f, 4503)]
        public void TheFallenTreeHasNoExposedRing(float length, float radius, int seed)
        {
            var (vertices, indices, _) = DeadwoodMesh.Build(length, radius, seed);
            List<MeshOpenness.OpenLoop> loops = MeshOpenness.ExposedLoops(vertices, indices);
            Assert.True(loops.Count == 0, $"log {seed}: {First(loops)}");
        }

        /// <summary>The bushes, scrub and tree-line masses (a lobed ellipsoid with its poles, three styles).</summary>
        [Theory]
        [InlineData(0, 3.0f, 2.4f)]
        [InlineData(1, 2.2f, 1.8f)]
        [InlineData(2, 5.0f, 3.0f)]
        public void TheFoliageMassesAreClosed(int style, float radius, float half)
        {
            FoliageStyle s = style switch { 0 => FoliageStyle.Crown, 1 => FoliageStyle.Scrub, _ => FoliageStyle.Tier };
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            FoliageMesh.Generate(v, idx, radius, half, new Vector3(0f, half, 0f), 4200 + style, s);
            Assert.Empty(MeshOpenness.OpenEdges(v, idx));
        }

        // ---- each old construction, seen to be found ----

        /// <summary>The fault: a bough ended in an open ring and the crown's twig started at the ring's centre, leaving an open ring round it.</summary>
        [Fact]
        public void AnOpenBoughEndWithATwigOnItWasExposed()
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            Vector3[] bough = { new(0f, 0f, 0f), new(3f, 3f, 0f), new(6f, 4f, 0f) };
            TubeGeometry.AddSweep(v, idx, 12, bough, new[] { 0.55f, 0.4f, 0.13f });
            TubeGeometry.AddSweep(v, idx, 3, new[] { bough[2], bough[2] + new Vector3(-0.5f, 1f, 0.5f), bough[2] + new Vector3(-1f, 1.5f, 1f) }, new[] { 0.07f, 0.05f, 0.014f });
            List<MeshOpenness.OpenLoop> loops = MeshOpenness.ExposedLoops(v, idx);
            Assert.True(loops.Count >= 3, $"the old bough: {First(loops)} - the check is not seeing the open ends (the bough's two, the twig's tip)");

            //The same, the way it is built now: a dome at the end the twig starts inside, the twig's tip a point, the bough's root buried in a parent
            var built = new List<VertexPositionNormalTexture>();
            var builtIdx = new List<short>();
            TubeGeometry.AddSweep(built, builtIdx, 20, new[] { new Vector3(0f, -1f, 0f), new Vector3(0f, 1f, 0f) }, new[] { 1.2f, 1.1f }, capStart: true, capEnd: true, endDome: 1f);
            TubeGeometry.AddSweep(built, builtIdx, 12, bough, new[] { 0.55f, 0.4f, 0.13f }, capEnd: true, endDome: 1f);
            TubeGeometry.AddSweep(built, builtIdx, 3, new[] { bough[2], bough[2] + new Vector3(-0.5f, 1f, 0.5f), bough[2] + new Vector3(-1f, 1.5f, 1f) }, new[] { 0.07f, 0.05f, 0f });
            loops = MeshOpenness.ExposedLoops(built, builtIdx);
            Assert.True(loops.Count == 0, $"the closed bough: {First(loops)}");
        }

        /// <summary>The fault: two tubes laid end to end at an angle (the coarse wood's boughs, the baobab's limbs, the log) open a slit on the outer side of the knee.</summary>
        [Fact]
        public void TwoTubesLaidAtAnAngleWereAKneeWithASlit()
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            Vector3 a = new(0f, 0f, 0f), b = new(3f, 3f, 0f), c = new(6.5f, 5.5f, 0f);
            TubeGeometry.AddTube(v, idx, 7, a, 0.6f, b, 0.42f);
            TubeGeometry.AddTube(v, idx, 7, b, 0.42f, c, 0.14f);
            Assert.True(MeshOpenness.OpenEdges(v, idx).Count >= 3 * 7, "the knee's two open rings were not found by the edge count");

            var one = new List<VertexPositionNormalTexture>();
            var oneIdx = new List<short>();
            TubeGeometry.AddSweep(one, oneIdx, 7, new[] { a, b, c }, new[] { 0.6f, 0.42f, 0.14f }, capStart: true, capEnd: true);
            Assert.Empty(MeshOpenness.OpenEdges(one, oneIdx));
            Assert.Equal(0, MeshOpenness.NonManifoldEdges(one, oneIdx));
        }

        /// <summary>The fault: a trunk cut off flat at the fork with the boughs' tubes starting at its centre; its cut end stood open between them.</summary>
        [Fact]
        public void ATrunkCutFlatBetweenItsBoughsWasExposed()
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            Vector3 fork = new(0f, 5f, 0f);
            TubeGeometry.AddTube(v, idx, 7, Vector3.Zero, 1.4f, fork, 0.9f);
            foreach (float a in new[] { 0f, 2.1f, 4.2f })
                TubeGeometry.AddTube(v, idx, 7, fork, 0.55f, fork + new Vector3(MathF.Cos(a) * 3f, 3f, MathF.Sin(a) * 3f), 0.3f);
            Assert.NotEmpty(MeshOpenness.ExposedLoops(v, idx));

            var built = new List<VertexPositionNormalTexture>();
            var builtIdx = new List<short>();
            TubeGeometry.AddSweep(built, builtIdx, 7, new[] { Vector3.Zero, fork }, new[] { 1.4f, 0.9f }, capStart: true, capEnd: true, endDome: 0.8f);
            foreach (float a in new[] { 0f, 2.1f, 4.2f })
                TubeGeometry.AddSweep(built, builtIdx, 7, new[] { fork, fork + new Vector3(MathF.Cos(a) * 3f, 3f, MathF.Sin(a) * 3f) }, new[] { 0.55f, 0.3f }, capEnd: true);
            Assert.Empty(MeshOpenness.ExposedLoops(built, builtIdx));
        }

        /// <summary>The fault: a twig thicker than the branch end it grows from, so its first ring stood out of the wood.</summary>
        [Fact]
        public void ATwigThickerThanItsBranchEndWasExposed()
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            Vector3 end = new(2f, 2f, 0f);
            TubeGeometry.AddSweep(v, idx, 5, new[] { Vector3.Zero, end }, new[] { 0.16f, 0.08f }, capEnd: true, endDome: 1f);
            TubeGeometry.AddSweep(v, idx, 3, new[] { end, end + new Vector3(0.6f, 0.6f, 0f) }, new[] { 0.12f, 0f });
            Assert.NotEmpty(MeshOpenness.ExposedLoops(v, idx));
        }

        /// <summary>The log's bend: two tubes and a pair of separate sawn discs, the knee's outer side a slit.</summary>
        [Fact]
        public void TheOldLogsBendWasASlit()
        {
            Vector3 root = new(-4.5f, 0.7f, 0f), middle = new(0f, 0.65f, 0.7f), tip = new(4.5f, 0.4f, 0f);
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            TubeGeometry.AddTube(v, idx, 8, root, 0.9f, middle, 0.74f);
            TubeGeometry.AddTube(v, idx, 8, middle, 0.74f, tip, 0.5f);
            TubeGeometry.AddCap(v, idx, 8, root, 0.9f, root - middle);
            TubeGeometry.AddCap(v, idx, 8, tip, 0.5f, tip - middle);
            Assert.True(MeshOpenness.OpenEdges(v, idx).Count >= 16, "the knee's two open rings were not found");
        }
    }
}
