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
    /// The meadow's free-standing shrubs on High and Ultra (#890). The owner: the green loaves "should be more detailed shrubs"
    /// there, and the far hedgerows may stay as they are. The loaf (<see cref="FoliageMesh"/> in <see cref="FoliageStyle.Scrub"/>)
    /// stays every tier's hedgerow and Low's and Medium's shrub; <see cref="MeadowShrubMesh"/> draws the free-standing ones at the
    /// finer tier, off the same instances. What is held here, with no device: every shrub is drawn once whichever the tier and the
    /// fine shrub only behind <see cref="SceneRenderer.FineMeshes"/>; it stands inside the loaf it replaces (the volume the meadow
    /// was planted by, and the sphere a camera keeps out of, #893); and its base is rooted - every stem goes into the ground near
    /// the axis and every branch grows out of a stem - which is the join the owner judges (a part that floats is a bug).
    /// </summary>
    public class MeadowShrubTests
    {
        private readonly ITestOutputHelper _output;

        public MeadowShrubTests(ITestOutputHelper output) => _output = output;

        //The loaves' radius roll (MeadowScatter: 1.6 plus up to 0.5) at its two ends and its middle, and the three variants
        private static readonly float[] LOAF_RADII = { 1.6f, 1.85f, 2.1f };
        private const int VARIANTS = 3;

        //The scale a shrub is planted at (MeadowScatter: 0.8 to 1.4, free-standing)
        private const float SCALE_MIN = 0.8f, SCALE_MAX = 1.4f;

        [Fact]
        public void EveryShrubIsDrawnOnceAndTheFineShrubOnlyBehindFineMeshes()
        {
            foreach ((MeadowScatter.ShrubPart part, _, bool? fineMeshes) in MeadowScatter.SHRUB_DRAWS)
                if (part != MeadowScatter.ShrubPart.Loaf)
                    Assert.True(fineMeshes == true, $"the fine shrub's {part} is drawn with FineMeshes {fineMeshes?.ToString() ?? "null"}, not only at High and Ultra");

            //Low's and Medium's draw is the one they always had: every shrub's loaf, first, in one bucket a variant
            Assert.Equal((MeadowScatter.ShrubPart.Loaf, MeadowScatter.ShrubSet.All, (bool?)false), MeadowScatter.SHRUB_DRAWS[0]);

            foreach (bool fine in new[] { false, true })
                foreach (MeadowScatter.ShrubSet shrub in new[] { MeadowScatter.ShrubSet.FreeStanding, MeadowScatter.ShrubSet.Hedgerows })
                {
                    int loaves = 0, wood = 0, leaves = 0;
                    foreach ((MeadowScatter.ShrubPart part, MeadowScatter.ShrubSet set, bool? fineMeshes) in MeadowScatter.SHRUB_DRAWS)
                    {
                        if (!ScatterBucket.DrawnAt(fineMeshes, fine)) continue;
                        if (set != MeadowScatter.ShrubSet.All && set != shrub) continue;
                        if (part == MeadowScatter.ShrubPart.Loaf) loaves++;
                        if (part == MeadowScatter.ShrubPart.Wood) wood++;
                        if (part == MeadowScatter.ShrubPart.Leaves) leaves++;
                    }

                    bool detailed = fine && shrub == MeadowScatter.ShrubSet.FreeStanding;
                    string at = $"{shrub} shrubs with FineMeshes {fine}";
                    Assert.True(loaves == (detailed ? 0 : 1), $"{at}: drawn as a loaf {loaves} times");
                    Assert.True(wood == (detailed ? 1 : 0) && leaves == (detailed ? 1 : 0), $"{at}: the fine shrub's wood drawn {wood} times, its leaves {leaves}");
                }
        }

        [Fact]
        public void TheFineShrubStandsInsideTheLoafItReplaces()
        {
            float worstSphere = 0f, worstReach = 0f, worstTop = 0f;
            foreach (float radius in LOAF_RADII)
                for (int m = 0; m < VARIANTS; m++)
                {
                    //The loaf as the scatter builds it, and the sphere round it its mesh reports (what a camera keeps out of)
                    var loaf = new List<VertexPositionNormalTexture>();
                    Vector3 loafCentre = new(0f, radius * 0.7f, 0f);
                    float sphere = FoliageMesh.Generate(loaf, new List<short>(), radius, radius * 0.8f, loafCentre, MeadowScatter.LOAF_SEED + m, FoliageStyle.Scrub);
                    (float reach, float top) = MeadowScatter.FineShrubSize(radius, MeadowScatter.LOAF_SEED + m);

                    MeadowShrubGeometry shrub = MeadowShrubMesh.Build(reach, top, MeadowScatter.FINE_SHRUB_SEED + m);
                    float widest = 0f, highest = float.MinValue, farthest = 0f;
                    foreach (var list in new[] { shrub.WoodVertices, shrub.LeafVertices })
                        foreach (VertexPositionNormalTexture v in list)
                        {
                            widest = MathF.Max(widest, MathF.Sqrt(v.Position.X * v.Position.X + v.Position.Z * v.Position.Z));
                            highest = MathF.Max(highest, v.Position.Y);
                            farthest = MathF.Max(farthest, Vector3.Distance(v.Position, loafCentre));
                        }

                    string at = $"variant {m}, loaf radius {radius:0.00}";
                    Assert.True(widest <= reach * 1.0001f, $"{at}: the shrub reaches {widest:0.00} from its axis, past the loaf's {reach:0.00}");
                    Assert.True(highest <= top * 1.0001f, $"{at}: the shrub stands {highest:0.00} high, over the loaf's {top:0.00}");
                    Assert.True(farthest <= sphere, $"{at}: a vertex of the shrub is {farthest:0.00} from the loaf's centre, outside its sphere of {sphere:0.00}");

                    int triangles = (shrub.WoodIndices.Count + shrub.LeafIndices.Count) / 3;
                    Assert.True(shrub.WoodVertices.Count <= ushort.MaxValue + 1 && shrub.LeafVertices.Count <= ushort.MaxValue + 1,
                        $"{at}: {shrub.WoodVertices.Count} wood and {shrub.LeafVertices.Count} leaf vertices, past what 16-bit indices reach");
                    Assert.True(triangles <= MAX_TRIANGLES, $"{at}: {triangles} triangles, over the {MAX_TRIANGLES} a shrub may cost");

                    worstSphere = MathF.Max(worstSphere, farthest / sphere);
                    worstReach = MathF.Max(worstReach, widest / reach);
                    worstTop = MathF.Max(worstTop, highest / top);
                    _output.WriteLine($"{at}: loaf {reach:0.00} x {top:0.00} (sphere {sphere:0.00}); shrub {widest:0.00} x {highest:0.00}, " +
                        $"{shrub.Stems.Count} stems, {shrub.SprayBases.Count} clumps, {shrub.LeafVertices.Count / 4} cards, " +
                        $"{shrub.WoodIndices.Count / 3} wood and {shrub.LeafIndices.Count / 3} leaf triangles");
                }
            _output.WriteLine($"at most {worstReach * 100f:0} % of the loaf's reach, {worstTop * 100f:0} % of its top, {worstSphere * 100f:0.0} % of its sphere");
        }

        //What one shrub may cost: the thorn bush's (#883) budget, 3 200 to 3 400 triangles, with room
        private const int MAX_TRIANGLES = 9000;

        [Fact]
        public void TheFineShrubsBaseIsRootedInTheGround()
        {
            foreach (float radius in LOAF_RADII)
                for (int m = 0; m < VARIANTS; m++)
                {
                    (float reach, float top) = MeadowScatter.FineShrubSize(radius, MeadowScatter.LOAF_SEED + m);
                    MeadowShrubGeometry shrub = MeadowShrubMesh.Build(reach, top, MeadowScatter.FINE_SHRUB_SEED + m);
                    string at = $"variant {m}, loaf radius {radius:0.00}";
                    Assert.True(shrub.Stems.Count >= 3, $"{at}: {shrub.Stems.Count} stems");

                    //The ground in the mesh's own space: the pivot is sunk SHRUB_SINK world units under it, at the scale planted
                    float groundLow = MeadowScatter.SHRUB_SINK / SCALE_MAX, groundHigh = MeadowScatter.SHRUB_SINK / SCALE_MIN;

                    float lowestLeaf = float.MaxValue;
                    foreach (VertexPositionNormalTexture v in shrub.LeafVertices) lowestLeaf = MathF.Min(lowestLeaf, v.Position.Y);

                    foreach (Vector3[] stem in shrub.Stems)
                    {
                        //Its foot under the pivot, so under the ground at any scale, and near the axis: one rooted spot
                        Vector3 foot = stem[0];
                        Assert.True(foot.Y < 0f, $"{at}: a stem's foot is {foot.Y:0.00} over the pivot, not in the ground");
                        Assert.True(Horizontal(foot) <= MeadowShrubMesh.ROOT_SPREAD * reach, $"{at}: a stem leaves the ground {Horizontal(foot):0.00} from the axis");

                        //Where it comes out of the ground, at every scale it is planted at, it is still near the axis: the stems spring
                        //from one spot rather than stand round the shrub as legs
                        foreach (float ground in new[] { groundLow, groundHigh })
                        {
                            float across = AcrossAt(stem, ground);
                            Assert.True(across <= 2f * MeadowShrubMesh.ROOT_SPREAD * reach,
                                $"{at}: a stem comes out of the ground {across:0.00} from the axis at the ground {ground:0.00}");
                        }

                        //And it reaches up into the leaf, not out of it into the air: its top is a clump's spray base
                        Vector3 tip = stem[^1];
                        Assert.Contains(shrub.SprayBases, s => Vector3.Distance(s, tip) < 1e-3f * reach);
                    }

                    //Every branch grows out of a stem: its first ring's centre on a stem's centreline
                    foreach ((Vector3 from, float stemRadius) in shrub.Branches)
                    {
                        float nearest = float.MaxValue;
                        foreach (Vector3[] stem in shrub.Stems)
                            for (int k = 0; k + 1 < stem.Length; k++) nearest = MathF.Min(nearest, DistanceToSegment(from, stem[k], stem[k + 1]));
                        Assert.True(nearest <= stemRadius * 0.01f + 1e-5f, $"{at}: a branch leaves {nearest:0.000} off its stem's centreline (radius {stemRadius:0.000})");
                    }

                    _output.WriteLine($"{at}: {shrub.Stems.Count} stems, {shrub.Branches.Count} branches; the lowest leaf {lowestLeaf:0.00} over the pivot, " +
                        $"the ground at {groundLow:0.00} to {groundHigh:0.00}");
                }
        }

        private static float Horizontal(Vector3 p) => MathF.Sqrt(p.X * p.X + p.Z * p.Z);

        //How far from the axis a centreline is where it crosses the height y (it rises the whole way, so it crosses once)
        private static float AcrossAt(Vector3[] line, float y)
        {
            for (int k = 0; k + 1 < line.Length; k++)
                if (line[k].Y <= y && line[k + 1].Y >= y)
                {
                    float t = (y - line[k].Y) / MathF.Max(line[k + 1].Y - line[k].Y, 1e-6f);
                    return Horizontal(Vector3.Lerp(line[k], line[k + 1], t));
                }
            return float.MaxValue;
        }

        private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
        {
            Vector3 along = to - from;
            float t = along.LengthSquared() > 1e-12f ? Math.Clamp(Vector3.Dot(point - from, along) / along.LengthSquared(), 0f, 1f) : 0f;
            return Vector3.Distance(point, from + along * t);
        }
    }
}
