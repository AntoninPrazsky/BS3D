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
    /// The meadow's solid props are closed (#943). The owner, on the hay rolls: "a hole along their edges". The bale's
    /// ends were separate discs at 0.98 of the tube's ring, so a ring-shaped slit stood open all round each end, and under
    /// back-face culling one saw through the roll. <c>Tools/WindingCheck</c> checks the way a face points and not whether the
    /// solid is shut, so the check here counts the triangles each edge belongs to. Built with no device, what
    /// <see cref="MeadowScatter"/> plants. Not covered, on purpose: the grass tufts and reeds and the trees' leaf cards
    /// (two-sided ribbons, open by design), and the boulders (a lathe whose buffers cannot be read back; its profile runs
    /// from a pole to a pole, so it is shut by construction).
    /// </summary>
    public class MeadowMeshClosureTests
    {
        [Fact]
        public void TheBaleIsClosed()
        {
            var (vertices, indices) = MeadowScatter.BaleLists();
            List<(Vector3 A, Vector3 B)> open = MeshOpenness.OpenEdges(vertices, indices);
            Assert.True(open.Count == 0, $"{open.Count} open edges on the bale, the first at {(open.Count > 0 ? open[0].A : Vector3.Zero)}");
            Assert.Equal(0, MeshOpenness.NonManifoldEdges(vertices, indices));
        }

        /// <summary>
        /// The fault: an end disc laid at 0.98 of the ring's radius. Rebuilt as it was, the same check must find a ring of open
        /// edges at each end, or a check that passes says nothing.
        /// </summary>
        [Fact]
        public void TheOldBaleWithItsSmallCapsIsOpenAtBothEnds()
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            Vector3 a = new(-MeadowScatter.BALE_HALF_LENGTH, MeadowScatter.BALE_RADIUS, 0f), b = new(MeadowScatter.BALE_HALF_LENGTH, MeadowScatter.BALE_RADIUS, 0f);
            TubeGeometry.AddTube(v, idx, 18, a, MeadowScatter.BALE_RADIUS, b, MeadowScatter.BALE_RADIUS);
            TubeGeometry.AddCap(v, idx, 18, b, MeadowScatter.BALE_RADIUS * 0.98f, Vector3.UnitX);
            TubeGeometry.AddCap(v, idx, 18, a, MeadowScatter.BALE_RADIUS * 0.98f, -Vector3.UnitX);

            List<(Vector3 A, Vector3 B)> open = MeshOpenness.OpenEdges(v, idx);
            int atA = open.Count(e => MathF.Abs(e.A.X - a.X) < 1e-3f && MathF.Abs(e.B.X - a.X) < 1e-3f);
            int atB = open.Count(e => MathF.Abs(e.A.X - b.X) < 1e-3f && MathF.Abs(e.B.X - b.X) < 1e-3f);
            Assert.True(atA >= 18 && atB >= 18, $"the old bale's open edges: {atA} at the first end, {atB} at the second - the check is not seeing the slit");
        }

        /// <summary>Each end's rim is the tube's ring: every vertex of the bale lies inside the tube's radius, and the end rims
        /// reach it all the way round.</summary>
        [Fact]
        public void EachEndsRimIsTheTubesRing()
        {
            var (vertices, _) = MeadowScatter.BaleLists();
            float radius = MeadowScatter.BALE_RADIUS, half = MeadowScatter.BALE_HALF_LENGTH;
            foreach (float end in new[] { -half, half })
            {
                //The vertices on that end's plane, as distances from the axis (which lies a radius over the ground)
                var rim = vertices.Where(p => MathF.Abs(p.Position.X - end) < 1e-4f)
                    .Select(p => new Vector2(p.Position.Y - radius, p.Position.Z).Length()).ToList();
                Assert.True(rim.Count >= 2 * BaleGeometry.SIDES, $"end {end}: {rim.Count} vertices on the plane");
                Assert.True(rim.Max() <= radius + 1e-4f, $"end {end}: a vertex lies {rim.Max()} from the axis, past the radius {radius}");
                Assert.True(rim.Count(r => MathF.Abs(r - radius) < 1e-4f) >= 2 * BaleGeometry.SIDES,
                    $"end {end}: fewer than {2 * BaleGeometry.SIDES} vertices on the ring itself (the tube's and the end's copy of it)");
            }
        }

        /// <summary>The end is a shallow dish, deepest at its centre, and its normals lean toward the axis as a dish's do.</summary>
        [Fact]
        public void TheEndsAreDishedWithTheirOwnNormals()
        {
            var (vertices, _) = MeadowScatter.BaleLists();
            float radius = MeadowScatter.BALE_RADIUS, half = MeadowScatter.BALE_HALF_LENGTH;
            var ends = vertices.Where(p => p.TextureCoordinate.X >= BaleGeometry.CAP_U).ToList();
            Assert.NotEmpty(ends);

            //The far end (+X): the dish recedes toward -X, deepest at the centre
            float rimPlane = ends.Where(p => p.Position.X > 0f).Min(p => half - p.Position.X);
            float centre = ends.Where(p => p.Position.X > 0f).Max(p => half - p.Position.X);
            Assert.Equal(0f, rimPlane, 1e-4f);
            Assert.Equal(BaleGeometry.DISH * radius, centre, 1e-3f);

            foreach (VertexPositionNormalTexture p in ends)
            {
                float facing = p.Position.X > 0f ? p.Normal.X : -p.Normal.X;
                Assert.True(facing > 0.95f, $"an end normal faces {facing} along the bale's axis");
                Assert.Equal(1f, p.Normal.Length(), 1e-4f);
                //The lean is toward the axis: the radial part of the normal points against the vertex's own offset
                Vector2 offset = new(p.Position.Y - radius, p.Position.Z), lean = new(p.Normal.Y, p.Normal.Z);
                if (offset.Length() > 1e-3f) Assert.True(Vector2.Dot(offset, lean) <= 1e-4f, "an end normal leans away from the axis");
            }
        }

        /// <summary>#971's shader tells an end from the side by its UV: the side's u runs 0 to 1, an end's starts past it, and
        /// the end's v runs from the core (0) to the rim (1).</summary>
        [Fact]
        public void TheEndsHaveAPolarUvOfTheirOwn()
        {
            var (vertices, _) = MeadowScatter.BaleLists();
            float radius = MeadowScatter.BALE_RADIUS;
            foreach (VertexPositionNormalTexture p in vertices)
            {
                bool end = p.Normal.X > 0.9f && p.Position.X > 0f || p.Normal.X < -0.9f && p.Position.X < 0f;
                if (end)
                {
                    Assert.InRange(p.TextureCoordinate.X, BaleGeometry.CAP_U, BaleGeometry.CAP_U + 1f);
                    float share = new Vector2(p.Position.Y - radius, p.Position.Z).Length() / radius;
                    Assert.Equal(share, p.TextureCoordinate.Y, 1e-3f);
                }
                else
                {
                    Assert.InRange(p.TextureCoordinate.X, 0f, 1f);
                    Assert.InRange(p.TextureCoordinate.Y, 0f, 1f);
                }
            }
            Assert.Contains(vertices, p => p.Normal.X > 0.9f);
            Assert.Contains(vertices, p => p.Normal.X < -0.9f);
        }

        /// <summary>The only open edges of a fence span are the post's foot, buried under the ground it stands on.</summary>
        [Fact]
        public void TheFenceIsClosedButForThePostsFoot()
        {
            var (vertices, indices, _) = MeadowScatter.FenceGeometry();
            float foot = vertices.Min(p => p.Position.Y);
            List<(Vector3 A, Vector3 B)> open = MeshOpenness.OpenEdges(vertices, indices);
            List<(Vector3 A, Vector3 B)> elsewhere = open.Where(e => e.A.Y > foot + 1e-3f || e.B.Y > foot + 1e-3f).ToList();
            Assert.True(elsewhere.Count == 0, $"{elsewhere.Count} open edges above the post's foot, the first at {(elsewhere.Count > 0 ? elsewhere[0].A : Vector3.Zero)}");
            Assert.True(open.Count > 0, "the post's foot is open and the check found nothing - it is not seeing open edges");
        }

        /// <summary>The fault: the rails ended in open rings, whose far sides reach past the post they end at.</summary>
        [Fact]
        public void TheFencesRailsWereOpenAtBothEndsBeforeTheyWereCapped()
        {
            const float k = 2f;
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            TubeGeometry.AddTube(v, idx, 5, new Vector3(-0.05f * k, 1.1f, 0.12f), 0.11f, new Vector3(6.5f, 1.1f, 0.12f), 0.11f);
            Assert.Equal(10, MeshOpenness.OpenEdges(v, idx).Count);
        }

        [Fact]
        public void TheBenchIsClosed()
        {
            var (vertices, indices, _) = MeadowScatter.BenchGeometry();
            Assert.Empty(MeshOpenness.OpenEdges(vertices, indices));
        }

        /// <summary>The three shrubs' lobed masses: a sphere with its poles, as the planting builds them.</summary>
        [Theory]
        [InlineData(0, 1.6f)]
        [InlineData(1, 1.9f)]
        [InlineData(2, 2.1f)]
        public void TheShrubsAreClosed(int m, float r)
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            FoliageMesh.Generate(v, idx, r, r * 0.8f, new Vector3(0f, r * 0.7f, 0f), 6100 + m, FoliageStyle.Scrub);
            Assert.Empty(MeshOpenness.OpenEdges(v, idx));
        }

        /// <summary>
        /// The free-standing shrubs of High and Ultra (#890): every stem and branch is closed at both ends from its own rings, so
        /// the wood's only open edges are the twigs' base rings, which lie two twig radii back along the stem inside it (#974), so
        /// within three of a spray base, as a tree's are.
        /// </summary>
        [Theory]
        [InlineData(0, 1.6f)]
        [InlineData(1, 1.85f)]
        [InlineData(2, 2.1f)]
        public void TheFineShrubsWoodIsOpenOnlyWhereItsTwigsLeave(int m, float r)
        {
            (float reach, float top) = MeadowScatter.FineShrubSize(r, MeadowScatter.LOAF_SEED + m);
            MeadowShrubGeometry shrub = MeadowShrubMesh.Build(reach, top, MeadowScatter.FINE_SHRUB_SEED + m);
            List<(Vector3 A, Vector3 B)> open = MeshOpenness.OpenEdges(shrub.WoodVertices, shrub.WoodIndices);
            Assert.NotEmpty(open);
            float near = shrub.TwigRadius * 3.05f;
            foreach ((Vector3 a, Vector3 b) in open)
                Assert.True(shrub.SprayBases.Any(s => Vector3.Distance(s, a) <= near && Vector3.Distance(s, b) <= near),
                    $"shrub {m}: an open edge of the wood at {a} is not a twig's base ring");
        }

        public static TheoryData<int, int> Trees() => new()
        {
            { 0, 6090 }, { 0, 6091 }, { 1, 6092 }, { 1, 6093 }, { 2, 6094 },
        };

        /// <summary>
        /// The coarse wood (Low, Medium and the Potato path) has one open end left, and it is buried: each surface root's inner
        /// ring, which starts inside the trunk (#889 plugged the rest). Its open edges lie closer to the trunk's axis than the
        /// trunk is thick there (the stem's girth is 1.03 to 1.6 of <c>Trunk</c> up to the root's height).
        /// </summary>
        [Theory]
        [MemberData(nameof(Trees))]
        public void TheCoarseWoodIsOpenOnlyInsideTheTrunk(int kind, int seed)
        {
            MeadowTreeGeometry tree = MeadowTreeMesh.Build((MeadowTreeKind)kind, seed);
            List<(Vector3 A, Vector3 B)> open = MeshOpenness.OpenEdges(tree.WoodVertices, tree.WoodIndices);
            Assert.NotEmpty(open);
            foreach ((Vector3 a, Vector3 b) in open)
                foreach (Vector3 p in new[] { a, b })
                {
                    float out2 = new Vector2(p.X, p.Z).Length() / tree.Trunk;
                    Assert.True(out2 < 1f, $"{kind} {seed}: an open edge of the coarse wood lies {out2:F3} trunks from the axis, at {p}");
                }
        }

        /// <summary>
        /// A twig's tip comes to a point, and its base starts inside the branch's end, two twig radii back (#974): the only open
        /// edges of the twigs are the base rings, within three twig radii of a spray base (the fault: a tip left as an open ring
        /// a quarter the base's size).
        /// </summary>
        [Theory]
        [MemberData(nameof(Trees))]
        public void TheTwigsAreOpenOnlyWhereTheyLeaveTheirBranch(int kind, int seed)
        {
            MeadowTreeGeometry tree = MeadowTreeMesh.Build((MeadowTreeKind)kind, seed);
            List<(Vector3 A, Vector3 B)> open = MeshOpenness.OpenEdges(tree.TwigVertices, tree.TwigIndices);
            Assert.NotEmpty(open);
            float reach = tree.Trunk * 0.11f;   //three twig radii, a twig's being 0.035 of the trunk
            foreach ((Vector3 a, Vector3 b) in open)
                Assert.True(tree.SprayBases.Any(s => Vector3.Distance(s, a) <= reach && Vector3.Distance(s, b) <= reach),
                    $"{kind} {seed}: an open edge of the twigs at {a} is not at a branch end");
        }
    }
}
