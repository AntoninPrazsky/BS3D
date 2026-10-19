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
    /// High and Ultra's spruce crown (#733): the whorls as separate boughs over a dark interior, laid out by the lathe's own
    /// whorl plan (<see cref="SpruceCrownGeometry"/>). Built with no device, at the scatter's own proportions, for the two
    /// woods that plant it — the aurora's and the daytime forest's.
    /// </summary>
    public class SpruceCrownTests
    {
        private readonly ITestOutputHelper _output;

        public SpruceCrownTests(ITestOutputHelper output) => _output = output;

        public static TheoryData<string, int> Spruces()
        {
            var data = new TheoryData<string, int>();
            for (int v = 0; v < ForestScatterRenderer.CONIFER_VARIANTS.Length; v++)
            {
                data.Add("aurora", v);
                data.Add("forest", v);
            }
            return data;
        }

        //A variant's plan as ForestScatterRenderer.NewConifer builds it
        private static SpruceWhorlPlan Plan(string wood, int variant)
        {
            ForestTreeConfig trees = wood == "aurora" ? new AuroraSceneConfig().Terrain.Trees : new ForestSceneConfig().Trees;
            ForestScatterRenderer.SpruceVariant v = ForestScatterRenderer.CONIFER_VARIANTS[variant];
            float stand = v.Stand ? trees.ConiferStandTrunk : 1f;
            return TreeMesh.PlanConifer(trees.TrunkBaseRadius * v.Width, trees.TrunkTopRadius * v.Width,
                trees.ConiferTrunkHeight * v.Height * stand, trees.ConiferCrownRadius * v.Width, trees.ConiferCrownHeight * v.Height,
                v.Seed, trees.ConiferTiers, trees.ConiferTierSpread, trees.ConiferRaggedness);
        }

        //The same variant's bark trunk, every vertex where TreeMesh places it (eight facets, NewConifer's default)
        private static Vector3[,] Trunk(string wood, int variant)
        {
            ForestTreeConfig trees = wood == "aurora" ? new AuroraSceneConfig().Terrain.Trees : new ForestSceneConfig().Trees;
            ForestScatterRenderer.SpruceVariant v = ForestScatterRenderer.CONIFER_VARIANTS[variant];
            float stand = v.Stand ? trees.ConiferStandTrunk : 1f;
            (List<LathePoint> profile, float wobble, float phase) = TreeMesh.ConiferTrunk(trees.TrunkBaseRadius * v.Width,
                trees.TrunkTopRadius * v.Width, trees.ConiferTrunkHeight * v.Height * stand, trees.ConiferCrownRadius * v.Width,
                trees.ConiferCrownHeight * v.Height, v.Seed, trees.ConiferTiers, trees.ConiferTierSpread, trees.ConiferRaggedness);
            return LatheMesh.RingPositions(profile, 8, wobble, phase);
        }

        /// <summary>
        /// The tip grows out of the trunk (#960): from the crown's foot up to the leader's, the axis carries wood — the
        /// interior lathe as it is built, wobble and facets included, or the leader round it — at least half as thick as the
        /// leader's foot, and above that the leader itself. The owner saw the tips of both woods levitate: the first build
        /// carried no wood at all for 1.3 to 7 units under the leader, and the aurora's interior turned inside out there.
        /// </summary>
        [Theory]
        [MemberData(nameof(Spruces))]
        public void TheStemCarriesTheTipDownToTheTrunk(string wood, int variant)
        {
            SpruceWhorlPlan plan = Plan(wood, variant);
            foreach (SpruceDetail detail in new[] { SpruceDetail.Near, SpruceDetail.Far })
            {
                SpruceCrownGeometry crown = SpruceCrownGeometry.Build(plan, ForestScatterRenderer.CONIFER_VARIANTS[variant].Seed, detail);
                Wood axis = new(crown, plan, detail, Trunk(wood, variant));
                float floor = 0.5f * axis.LeaderFootApothem / MathF.Cos(MathF.PI / 5f);

                float thinnest = float.MaxValue, at = 0f;
                for (float y = plan.BaseY; y < plan.Top - 0.05f; y += 0.05f)
                {
                    //Under the leader's foot the floor; up the leader its own taper, which the leader alone meets
                    float need = y <= axis.LeaderFoot ? floor : floor * (plan.Top - y) / (plan.Top - axis.LeaderFoot);
                    float share = axis.At(y) / need;
                    if (share < thinnest) (thinnest, at) = (share, y);
                }
                _output.WriteLine($"{wood} {variant} {Name(detail)}: thinnest {thinnest:0.00} of the floor {floor:0.000}, " +
                    $"{plan.Top - at:0.00} under the tip");
                Assert.True(thinnest >= 1f, $"{wood} {variant} {Name(detail)}: the axis carries {thinnest:0.00} of the wood it needs " +
                    $"{plan.Top - at:0.00} under the tip");
            }
        }

        /// <summary>
        /// Every arm grows out of the stem (#960): each vertex of its open root ring stands inside the wood round the axis
        /// at its height, so no root ring hangs beside the stem with daylight between them — the top whorls of the first build
        /// rooted a twentieth of the crown's radius off an axis that had nothing on it.
        /// </summary>
        [Theory]
        [MemberData(nameof(Spruces))]
        public void EveryArmRootsInsideTheStem(string wood, int variant)
        {
            SpruceWhorlPlan plan = Plan(wood, variant);
            foreach (SpruceDetail detail in new[] { SpruceDetail.Near, SpruceDetail.Far })
            {
                SpruceCrownGeometry crown = SpruceCrownGeometry.Build(plan, ForestScatterRenderer.CONIFER_VARIANTS[variant].Seed, detail);
                Wood axis = new(crown, plan, detail, Trunk(wood, variant));

                int outside = 0, worst = -1;
                float worstShare = 0f;
                foreach (int root in crown.ArmRoots)
                {
                    for (int k = root; k < root + 6; k++)
                    {
                        Vector3 p = crown.Vertices[k].Position;
                        float share = new Vector2(p.X, p.Z).Length() / MathF.Max(axis.At(p.Y), 1e-4f);
                        if (share >= 1f) outside++;
                        if (share > worstShare) (worstShare, worst) = (share, k);
                    }
                }
                _output.WriteLine($"{wood} {variant} {Name(detail)}: {crown.ArmRoots.Count} roots, the furthest out at " +
                    $"{worstShare:0.00} of the wood round it");
                Assert.True(outside == 0, $"{wood} {variant} {Name(detail)}: {outside} root-ring vertices outside the stem, the worst " +
                    $"at {worstShare:0.00} of it, {plan.Top - crown.Vertices[worst].Position.Y:0.00} under the tip");
            }
        }

        /// <summary>
        /// Neither crown reaches past the lathe it stands in for: no bough or lump further from the axis than the widest
        /// skirt and the wobble's peak, none over the tip and none under the lowest skirt by more than a bough's hang. A host
        /// keeping a camera out of a tree by the lathe's figures (#559) keeps it out of the boughs. The same check run against
        /// a plan half as wide must find boughs outside it, or a pass says nothing.
        /// </summary>
        [Theory]
        [MemberData(nameof(Spruces))]
        public void TheBoughsStayInsideTheLathesReach(string wood, int variant)
        {
            SpruceWhorlPlan plan = Plan(wood, variant);
            foreach (SpruceDetail detail in new[] { SpruceDetail.Near, SpruceDetail.Far })
            {
                SpruceCrownGeometry crown = SpruceCrownGeometry.Build(plan, ForestScatterRenderer.CONIFER_VARIANTS[variant].Seed, detail);

                int outside = Outside(crown.Vertices, plan, plan.Reach) + Outside(crown.SnowVertices, plan, plan.Reach);
                Assert.True(outside == 0, $"{wood} {variant} {Name(detail)}: {outside} vertices outside the lathe's reach {plan.Reach:0.00}");

                int narrower = Outside(crown.Vertices, plan, plan.Reach * 0.5f);
                Assert.True(narrower > 0, $"{wood} {variant} {Name(detail)}: nothing outside half the reach - the check is not seeing the boughs");
            }
        }

        /// <summary>
        /// The far crown is the light one: at most two fifths of the near crown's triangles, snow included — the reason it
        /// exists is that a wood of eight hundred near crowns cost a millisecond a frame (#733). And both fit 16-bit indices.
        /// </summary>
        [Theory]
        [MemberData(nameof(Spruces))]
        public void TheFarCrownIsTheLightOne(string wood, int variant)
        {
            SpruceWhorlPlan plan = Plan(wood, variant);
            int seed = ForestScatterRenderer.CONIFER_VARIANTS[variant].Seed;
            SpruceCrownGeometry near = SpruceCrownGeometry.Build(plan, seed, SpruceDetail.Near);
            SpruceCrownGeometry far = SpruceCrownGeometry.Build(plan, seed, SpruceDetail.Far);

            int nearTriangles = (near.Indices.Count + near.SnowIndices.Count) / 3;
            int farTriangles = (far.Indices.Count + far.SnowIndices.Count) / 3;
            _output.WriteLine($"{wood} {variant}: near {near.Boughs} boughs, {near.SnowLumps} lumps, {nearTriangles} triangles; " +
                $"far {far.Boughs} boughs, {far.SnowLumps} lumps, {farTriangles} triangles");

            Assert.True(farTriangles * 5 <= nearTriangles * 2, $"{wood} {variant}: far {farTriangles} triangles against near {nearTriangles}");
            Assert.True(near.Vertices.Count <= ushort.MaxValue + 1 && far.Vertices.Count <= ushort.MaxValue + 1);
        }

        /// <summary>
        /// Every triangle of both parts faces the way its normals do: the outward face normal the winding gives (clockwise
        /// seen from outside, so <c>(b - a) × (c - a)</c> points in) against the vertex normals the lighting uses. The
        /// device-free twin of what <c>Tools/WindingCheck</c> judges on the uploaded buffers.
        /// </summary>
        [Theory]
        [MemberData(nameof(Spruces))]
        public void EveryTriangleFacesItsNormals(string wood, int variant)
        {
            SpruceWhorlPlan plan = Plan(wood, variant);
            SpruceCrownGeometry crown = SpruceCrownGeometry.Build(plan, ForestScatterRenderer.CONIFER_VARIANTS[variant].Seed, SpruceDetail.Near);

            Assert.Equal(0, Backwards(crown.Vertices, crown.Indices));
            Assert.Equal(0, Backwards(crown.SnowVertices, crown.SnowIndices));
        }

        private static string Name(SpruceDetail detail) => detail == SpruceDetail.Near ? "near" : "far";

        /// <summary>
        /// The wood round a spruce's axis as it is built: at a height, the nearest the surface of the stem, the interior or
        /// the bark trunk comes to the axis (each lathe's vertices as <see cref="LatheMesh"/> places them, cut between two
        /// rings along the diagonal its quads are split by; zero where that cut does not go round the axis), or the leader's
        /// five sides, whichever is thickest.
        /// </summary>
        private sealed class Wood
        {
            private readonly Vector3[,] _shade, _stem;
            private readonly Vector3[] _leaderFoot;
            private readonly Vector3 _tip;
            private readonly Vector3[,] _trunk;

            public float LeaderFoot => _leaderFoot[0].Y;
            public float LeaderFootApothem { get; }

            public Wood(SpruceCrownGeometry crown, SpruceWhorlPlan plan, SpruceDetail detail, Vector3[,] trunk)
            {
                _trunk = trunk;
                _shade = LatheMesh.RingPositions(SpruceCrownGeometry.ShadeProfileFor(plan, detail), SpruceCrownGeometry.SHADE_SEGMENTS,
                    plan.WobbleAmplitude * detail.Shade, plan.Phase);
                _stem = LatheMesh.RingPositions(SpruceCrownGeometry.StemProfileFor(plan), SpruceCrownGeometry.STEM_SEGMENTS);

                //The leader is the last thing built: five vertices round its foot and the tip
                List<VertexPositionNormalTexture> v = crown.Vertices;
                _leaderFoot = new Vector3[5];
                for (int s = 0; s < 5; s++) _leaderFoot[s] = v[v.Count - 6 + s].Position;
                _tip = v[v.Count - 1].Position;
                LeaderFootApothem = Apothem(_leaderFoot);
            }

            public float At(float y)
            {
                float leader = y >= LeaderFoot && y < _tip.Y ? LeaderFootApothem * (_tip.Y - y) / (_tip.Y - LeaderFoot) : 0f;
                return MathF.Max(MathF.Max(leader, Cut(_stem, y)), MathF.Max(Cut(_shade, y), Cut(_trunk, y)));
            }

            private static float Cut(Vector3[,] lathe, float y)
            {
                int rings = lathe.GetLength(0), segments = lathe.GetLength(1);
                for (int k = 0; k + 1 < rings; k++)
                {
                    float y0 = lathe[k, 0].Y, y1 = lathe[k + 1, 0].Y;
                    if (y > MathF.Max(y0, y1) || y < MathF.Min(y0, y1) || MathF.Abs(y1 - y0) < 1e-5f) continue;
                    float t = (y - y0) / (y1 - y0);

                    var cut = new Vector2[segments * 2];
                    for (int s = 0; s < segments; s++)
                    {
                        int next = (s + 1) % segments;
                        cut[2 * s] = Flat(Vector3.Lerp(lathe[k, s], lathe[k + 1, s], t));
                        cut[2 * s + 1] = Flat(Vector3.Lerp(lathe[k, next], lathe[k + 1, s], t));
                    }

                    //It holds the axis only if it goes round it
                    float turned = 0f;
                    for (int i = 0; i < cut.Length; i++)
                    {
                        Vector2 a = cut[i], b = cut[(i + 1) % cut.Length];
                        turned += MathF.Atan2(a.X * b.Y - a.Y * b.X, Vector2.Dot(a, b));
                    }
                    if (MathF.Abs(turned) < MathF.PI) return 0f;

                    float nearest = float.MaxValue;
                    for (int i = 0; i < cut.Length; i++) nearest = MathF.Min(nearest, Distance(cut[i], cut[(i + 1) % cut.Length]));
                    return nearest;
                }
                return 0f;
            }

            private static float Apothem(Vector3[] ring)
            {
                float nearest = float.MaxValue;
                for (int i = 0; i < ring.Length; i++) nearest = MathF.Min(nearest, Distance(Flat(ring[i]), Flat(ring[(i + 1) % ring.Length])));
                return nearest;
            }

            private static Vector2 Flat(Vector3 p) => new(p.X, p.Z);

            //The distance from the axis to a segment
            private static float Distance(Vector2 a, Vector2 b)
            {
                Vector2 ab = b - a;
                float t = ab.LengthSquared() > 1e-12f ? Math.Clamp(-Vector2.Dot(a, ab) / ab.LengthSquared(), 0f, 1f) : 0f;
                return (a + ab * t).Length();
            }
        }

        private static int Outside(List<VertexPositionNormalTexture> vertices, SpruceWhorlPlan plan, float reach)
        {
            float bottom = plan.SkirtHeights[plan.SkirtHeights.Count - 1] - plan.Radius;
            int outside = 0;
            foreach (VertexPositionNormalTexture v in vertices)
            {
                Vector3 p = v.Position;
                if (new Vector2(p.X, p.Z).Length() > reach + 1e-3f || p.Y > plan.Top + 1e-3f || p.Y < bottom) outside++;
            }
            return outside;
        }

        private static int Backwards(List<VertexPositionNormalTexture> vertices, List<int> indices)
        {
            int backwards = 0;
            for (int t = 0; t + 2 < indices.Count; t += 3)
            {
                VertexPositionNormalTexture a = vertices[indices[t]], b = vertices[indices[t + 1]], c = vertices[indices[t + 2]];
                Vector3 inward = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);
                if (inward.LengthSquared() < 1e-14f) continue;
                Vector3 normals = a.Normal + b.Normal + c.Normal;
                if (Vector3.Dot(Vector3.Normalize(inward), Vector3.Normalize(normals)) > 0.25f) backwards++;
            }
            return backwards;
        }
    }
}
