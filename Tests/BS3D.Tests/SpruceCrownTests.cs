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
            return TreeMesh.PlanConifer(trees.TrunkBaseRadius * v.Width, trees.ConiferTrunkHeight * v.Height * stand,
                trees.ConiferCrownRadius * v.Width, trees.ConiferCrownHeight * v.Height, v.Seed,
                trees.ConiferTiers, trees.ConiferTierSpread, trees.ConiferRaggedness);
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
