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
    /// What the seam audit closed (#974), each held shut with no device and each shown to fail on the construction it replaced:
    /// the snags' knees, the palm's foot and the crown's heart, the meadow twigs' bases, and the dish ring's seam. The rest of
    /// the audit is <c>Tools/WindingCheck --openness</c>, which CI runs over every mesh of every scene and which fails on an
    /// open one nobody has named.
    /// </summary>
    public class SeamAuditClosureTests
    {
        private static string First(List<MeshOpenness.OpenLoop> loops) =>
            $"{loops.Count} exposed rings, the first centred at {(loops.Count > 0 ? loops[0].Centre : Vector3.Zero)} with {(loops.Count > 0 ? loops[0].Points.Count : 0)} points";

        // ---- the snags (the forest's dead spruces, the aurora's wood) ----

        /// <summary>The forest's three figures (height, radius, seed), at the aurora's snag size and at the daytime default's.</summary>
        public static TheoryData<float, float, int> Snags() => new()
        {
            { 13f, 0.55f, 131 }, { 13f * 1.3f, 0.55f * 1.1f, 137 }, { 13f * 0.65f, 0.55f * 0.95f, 139 },
            { 11f, 0.3f, 131 }, { 11f * 1.3f, 0.3f * 1.1f, 137 }, { 11f * 0.65f, 0.3f * 0.95f, 139 },
        };

        [Theory]
        [MemberData(nameof(Snags))]
        public void ASnagHasNoExposedRing(float height, float radius, int seed)
        {
            var (vertices, indices, _) = SnagMesh.Build(height, radius, seed);
            List<MeshOpenness.OpenLoop> loops = MeshOpenness.ExposedLoops(vertices, indices);
            Assert.True(loops.Count == 0, $"snag {seed} ({height}, {radius}): {First(loops)}");
        }

        /// <summary>
        /// The fault: four straight tubes laid end to end at an angle, their end rings in two planes. The same check must find
        /// the wedge of open ring at each bend (and the root flare's, and the foot), or one that passes says nothing.
        /// </summary>
        [Theory]
        [MemberData(nameof(Snags))]
        public void TheOldSnagsTubesLaidEndToEndWereOpenAtEveryBend(float height, float radius, int seed)
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            Vector3 foot = new(0f, -0.2f * radius, 0f);
            Vector3 kink = new(0.3f, height * 0.55f, 0.1f);
            Vector3 top = new(-0.1f, height, 0.2f);
            TubeGeometry.AddTube(v, idx, 7, foot, radius * 1.45f, foot + new Vector3(0f, radius * 1.6f, 0f), radius);
            TubeGeometry.AddTube(v, idx, 7, foot + new Vector3(0f, radius * 1.6f, 0f), radius, kink, radius * 0.5f);
            TubeGeometry.AddTube(v, idx, 7, kink, radius * 0.5f, top, radius * 0.3f);
            TubeGeometry.AddCap(v, idx, 7, top, radius * 0.3f, top - kink);

            List<MeshOpenness.OpenLoop> loops = MeshOpenness.ExposedLoops(v, idx);
            Assert.True(loops.Count >= 3, $"the old snag {seed}: {First(loops)} - the check is not seeing the bends");

            //And one sweep through the same points, closed at both ends, is shut
            var built = new List<VertexPositionNormalTexture>();
            var builtIdx = new List<short>();
            TubeGeometry.AddSweep(built, builtIdx, 7, new[] { foot, foot + new Vector3(0f, radius * 1.6f, 0f), kink, top },
                new[] { radius * 1.45f, radius, radius * 0.5f, radius * 0.3f }, capStart: true, capEnd: true);
            Assert.Empty(MeshOpenness.ExposedLoops(built, builtIdx));
        }

        // ---- the palm ----

        public static TheoryData<int> Palms() => new() { 6100, 6101, 6102, 6103 };

        /// <summary>The trunk, its boot and the coconuts, at the tropical scene's figures, shut from the foot to the crown's heart.</summary>
        [Theory]
        [MemberData(nameof(Palms))]
        public void ThePalmsWoodHasNoExposedRing(int seed)
        {
            var (vertices, indices) = PalmMesh.WoodGeometry(0.38f, 22f, seed);
            List<MeshOpenness.OpenLoop> loops = MeshOpenness.ExposedLoops(vertices, indices);
            Assert.True(loops.Count == 0, $"palm {seed}: {First(loops)}");
        }

        /// <summary>
        /// The fault, #754's open lip: the trunk's first ring lay on the ground itself. Take the sunk skirt and its disc away
        /// (everything at or under the ground) and the foot ring is a ring at y = 0 that a dune falling away shows the inside of,
        /// which the audit counts as exposed unless the ring is sunk.
        /// </summary>
        [Theory]
        [MemberData(nameof(Palms))]
        public void ThePalmsOldFootRingOnTheGroundWasExposed(int seed)
        {
            var (vertices, indices) = PalmMesh.WoodGeometry(0.38f, 22f, seed);
            var kept = new List<int>();
            for (int t = 0; t + 2 < indices.Count; t += 3)
                if (indices.Skip(t).Take(3).Any(i => vertices[i].Position.Y > 1e-4f)) kept.Add(t);
            var oldIndices = new List<int>();
            foreach (int t in kept) oldIndices.AddRange(new[] { indices[t], indices[t + 1], indices[t + 2] });

            List<MeshOpenness.OpenLoop> loops = MeshOpenness.ExposedLoops(vertices, oldIndices, belowY: -0.01f);
            Assert.True(loops.Count >= 1, $"palm {seed} without its skirt: {First(loops)} - the foot ring on the ground is not seen as exposed");
        }

        // ---- the meadow's twigs ----

        /// <summary>A branch with a clump at its end and one spray of twigs: the scene a twig base has to be buried in.</summary>
        private static List<MeshOpenness.OpenLoop> SprayOn(Vector3 backAlong)
        {
            var wood = new List<VertexPositionNormalTexture>();
            var woodIdx = new List<short>();
            var leaves = new List<VertexPositionNormalTexture>();
            var leafIdx = new List<int>();

            const float TRUNK = 1.6f;
            Vector3 from = Vector3.Zero, to = new(3f, 0f, 0f);
            TubeGeometry.AddSweep(wood, woodIdx, 5, new[] { from, to }, new[] { TRUNK * 0.1f, TRUNK * 0.06f }, capStart: true, capEnd: true);

            const float RADIUS = 2f;
            Vector3 centre = to + Vector3.UnitX * (RADIUS * 0.33f);
            MeadowTreeMesh.AddSpray(wood, woodIdx, leaves, leafIdx, to, backAlong, centre, new Vector3(0f, 1.5f, 0f), RADIUS, RADIUS * 0.8f,
                inner: false, twigRadius: TRUNK * 0.035f, rng: new Random(7));
            return MeshOpenness.ExposedLoops(wood, woodIdx);
        }

        /// <summary>The twigs' bases lie inside the branch they start on, whichever way the twig leaves.</summary>
        [Fact]
        public void ASpraysTwigsStartInsideTheirBranch()
        {
            List<MeshOpenness.OpenLoop> loops = SprayOn(-Vector3.UnitX);
            Assert.True(loops.Count == 0, $"the spray: {First(loops)}");
        }

        /// <summary>
        /// The fault: the twig's first ring laid on the branch end's disc (no depth to bury it in), half of it standing proud of
        /// the disc with its open edge showing. The same spray started there must be found.
        /// </summary>
        [Fact]
        public void ASpraysTwigsStartedOnTheBranchEndWereExposed()
        {
            List<MeshOpenness.OpenLoop> loops = SprayOn(Vector3.Zero);
            Assert.True(loops.Count >= 1, $"the old spray: {First(loops)} - the check is not seeing the bases on the disc");
        }

        public static TheoryData<int, int> Trees() => new()
        {
            { 0, 6090 }, { 0, 6091 }, { 1, 6092 }, { 1, 6093 }, { 2, 6094 },
        };

        private static List<MeshOpenness.OpenLoop> Together(List<VertexPositionNormalTexture> a, List<short> aIdx,
            List<VertexPositionNormalTexture> b, List<short> bIdx)
        {
            var vertices = new List<VertexPositionNormalTexture>(a);
            vertices.AddRange(b);
            var indices = new List<int>(aIdx.Select(i => (int)(ushort)i));
            indices.AddRange(bIdx.Select(i => (int)(ushort)i + a.Count));
            return MeshOpenness.ExposedLoops(vertices, indices);
        }

        /// <summary>The meadow's trees as the scene draws them together: wood and twigs, coarse (Low, Medium, the Potato path) and fine.</summary>
        [Theory]
        [MemberData(nameof(Trees))]
        public void ATreesCoarseWoodAndTwigsHaveNoExposedRing(int kind, int seed)
        {
            MeadowTreeGeometry tree = MeadowTreeMesh.Build((MeadowTreeKind)kind, seed);
            List<MeshOpenness.OpenLoop> loops = Together(tree.WoodVertices, tree.WoodIndices, tree.TwigVertices, tree.TwigIndices);
            Assert.True(loops.Count == 0, $"tree {kind} {seed}: {First(loops)}");
        }

        /// <summary>The fine wood and its twigs. The willow's has two rings left (a fine branch end arrives a few degrees off the coarse
        /// branch's direction), the table in <c>WindingCheck</c> says so; no other tree has any.</summary>
        [Theory]
        [MemberData(nameof(Trees))]
        public void ATreesFineWoodAndTwigsHaveNoMoreExposedRingsThanTheTableAllows(int kind, int seed)
        {
            MeadowTreeGeometry tree = MeadowTreeMesh.Build((MeadowTreeKind)kind, seed);
            List<MeshOpenness.OpenLoop> loops = Together(tree.FineWoodVertices, tree.FineWoodIndices, tree.TwigVertices, tree.TwigIndices);
            int allowed = kind == 2 ? 2 : 0;
            Assert.True(loops.Count <= allowed, $"tree {kind} {seed}: {First(loops)}");
        }

        // ---- the dish ring ----

        /// <summary>The rim of a rooftop dish, a swept tube round a closed path: one torus, not a bar with a crack across it.</summary>
        [Fact]
        public void AClosedSweptTubeIsATorus()
        {
            Vector3[] Circle()
            {
                const int STEPS = 32;
                var path = new Vector3[STEPS + 1];
                for (int i = 0; i <= STEPS; i++)
                {
                    float a = i * MathHelper.TwoPi / STEPS;
                    path[i] = new Vector3(MathF.Cos(a), MathF.Sin(a), 0f);
                }
                return path;
            }

            var closed = new MeshBuilder();
            closed.AddSweptTube(Circle(), Vector3.UnitZ, 0.05f, 6, closed: true);
            Assert.Empty(MeshOpenness.OpenEdges(closed.Geometry.Vertices, closed.Geometry.Indices));

            //The fault: each end took its own one-sided frame, the rings at the seam did not meet
            var open = new MeshBuilder();
            open.AddSweptTube(Circle(), Vector3.UnitZ, 0.05f, 6);
            Assert.NotEmpty(MeshOpenness.OpenEdges(open.Geometry.Vertices, open.Geometry.Indices));
        }
    }
}
