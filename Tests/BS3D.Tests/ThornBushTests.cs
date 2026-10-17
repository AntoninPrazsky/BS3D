using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The savanna's bush on High and Ultra (#883). The owner: "green balls between the trees ... like giant green crumpled melons".
    /// The bush was a lobed sphere with nothing inside its outline; the thorn bush (<see cref="ThornBushMesh"/>) is stems and
    /// branches with the leaf hung on twigs grown from them, smaller, drawn instead of the dome at the finer tier off the same
    /// instances. The planting is the dome's, so what is held here is that the bush stands inside the volume the dome was planted by
    /// (<c>PlantVolumes</c>, #653: 0 overlapping pairs over <c>sceneseed</c> 0 to 4 stays true because of it), that it has wood and leaf
    /// all through its outline and not only on its skin, and that the shape the leaf is laid in is a new argument and not a change: the
    /// trees' sprays are the sprays they were. Built with no device (<see cref="ThornBushMesh.Build"/>).
    /// </summary>
    public class ThornBushTests
    {
        private readonly ITestOutputHelper _output;

        public ThornBushTests(ITestOutputHelper output) => _output = output;

        //The savanna's own figures (AcaciaConfig: Width 6, Height 9): the dome's radius and half-height rolls (SavannaScatter) and the
        //slab the planting keeps it apart by, a radius of 0.9 of the dome's and a height of twice its half-height
        private static readonly float DOME_WIDTH = new AcaciaConfig().Width, DOME_HEIGHT = new AcaciaConfig().Height;
        private const float DOME_RADIUS_MIN = 0.5f, DOME_RADIUS_SPREAD = 0.2f, DOME_HALF_MIN = 0.22f, DOME_HALF_SPREAD = 0.08f, SLAB_RADIUS = 0.9f;

        private static readonly int[] SEEDS = { 4250, 4251, 4252, 4253, 4254, 4255 };

        [Fact]
        public void TheBushStandsInsideTheVolumeTheDomeWasPlantedBy()
        {
            //Every dome the scatter can roll at its smallest and largest in each of its two rolls (they are independent, so the widest
            //dome can stand lowest), and every bush the roll can make under it, at each of the six structural seeds
            float worstReach = 0f, worstTop = 0f, worstSlabReach = 0f, worstSlabTop = 0f;
            int cases = 0;
            foreach (float domeRadiusRoll in new[] { 0f, 1f })
                foreach (float domeHalfRoll in new[] { 0f, 1f })
                    foreach (float reachRoll in new[] { 0f, 0.5f, 1f })
                        foreach (float heightRoll in new[] { 0f, 0.5f, 1f })
                            foreach (int seed in SEEDS)
                            {
                                float domeRadius = DOME_WIDTH * (DOME_RADIUS_MIN + DOME_RADIUS_SPREAD * domeRadiusRoll);
                                float domeHeight = 2f * DOME_HEIGHT * (DOME_HALF_MIN + DOME_HALF_SPREAD * domeHalfRoll);
                                (float reach, float topHeight) = SavannaScatter.ThornSize(domeRadius, domeHeight, reachRoll, heightRoll);
                                ThornBushGeometry bush = ThornBushMesh.Build(reach, topHeight, seed);

                                float widest = 0f, top = float.MinValue, bottom = float.MaxValue;
                                foreach (var list in new[] { bush.WoodVertices, bush.LeafVertices })
                                    foreach (VertexPositionNormalTexture v in list)
                                    {
                                        widest = MathF.Max(widest, MathF.Sqrt(v.Position.X * v.Position.X + v.Position.Z * v.Position.Z));
                                        top = MathF.Max(top, v.Position.Y);
                                        bottom = MathF.Min(bottom, v.Position.Y);
                                    }

                                string at = $"seed {seed}, dome {domeRadius:0.00} x {domeHeight:0.00}, rolls {reachRoll} {heightRoll}";

                                //The mesh keeps what it was asked for (its reach and its top are of the whole bush, cards included)...
                                Assert.True(widest <= reach, $"{at}: the bush reaches {widest:0.00} from its axis, over the {reach:0.00} it was built to");
                                Assert.True(top <= topHeight, $"{at}: the bush stands {top:0.00} high, over the {topHeight:0.00} it was built to");
                                Assert.True(bottom >= -0.1f * topHeight, $"{at}: the bush reaches {-bottom:0.00} under the ground");

                                //...and what it was asked for is inside the slab the dome was planted by: a cylinder of 0.9 of the dome's
                                //radius from the ground to the dome's whole height (a stem's foot is buried a little, and nothing of the bush
                                //is to stand over its rim or lid)
                                Assert.True(widest <= SLAB_RADIUS * domeRadius, $"{at}: the bush reaches {widest:0.00}, over the slab's {SLAB_RADIUS * domeRadius:0.00}");
                                Assert.True(top <= domeHeight, $"{at}: the bush stands {top:0.00} high, over the slab's {domeHeight:0.00}");

                                worstReach = MathF.Max(worstReach, widest / reach);
                                worstTop = MathF.Max(worstTop, top / topHeight);
                                worstSlabReach = MathF.Max(worstSlabReach, widest / (SLAB_RADIUS * domeRadius));
                                worstSlabTop = MathF.Max(worstSlabTop, top / domeHeight);
                                cases++;
                            }

            _output.WriteLine($"{cases} bushes: at most {worstReach * 100f:0} % of the reach and {worstTop * 100f:0} % of the top they were built to; {worstSlabReach * 100f:0} % of the slab's radius and {worstSlabTop * 100f:0} % of its height");
        }

        [Theory]
        [InlineData(4250)]
        [InlineData(4253)]
        [InlineData(4255)]
        public void TheLeafFillsTheCloudAndNotJustItsSkin(int seed)
        {
            //The dome had nothing inside its outline. Here a spray's stem is spread through the whole of the cloud: about a quarter of
            //an evenly laid disc's lie inside half its radius, and so do a fair share of these (the layers reach less far under the top
            //one, and the rim is thinner than the middle)
            const float REACH = 3f, TOP = 3f;
            ThornBushGeometry bush = ThornBushMesh.Build(REACH, TOP, seed);

            int cards = bush.LeafVertices.Count / 4, inner = 0, low = 0;
            for (int card = 0; card + 3 < bush.LeafVertices.Count; card += 4)
            {
                Vector3 stem = (bush.LeafVertices[card].Position + bush.LeafVertices[card + 1].Position) * 0.5f;
                if (MathF.Sqrt(stem.X * stem.X + stem.Z * stem.Z) < 0.5f * REACH) inner++;
                if (stem.Y < 0.5f * TOP) low++;
            }

            float innerShare = inner / (float)cards, lowShare = low / (float)cards;
            _output.WriteLine($"{seed}: {cards} cards, {innerShare * 100f:0} % of the stems inside half the reach, {lowShare * 100f:0} % under half the height; {bush.Supports.Count} branch ends, {bush.WoodVertices.Count} wood vertices");
            Assert.True(cards > 100, $"{seed}: only {cards} cards");
            Assert.True(innerShare >= 0.2f, $"{seed}: only {innerShare * 100f:0} % of the leaf is inside half the reach");
            Assert.True(lowShare >= 0.15f, $"{seed}: only {lowShare * 100f:0} % of the leaf is under half the height");
            Assert.True(bush.Supports.Count >= 8, $"{seed}: {bush.Supports.Count} branch ends");
        }

        [Theory]
        [InlineData(4250)]
        [InlineData(4251)]
        [InlineData(4252)]
        [InlineData(4253)]
        [InlineData(4254)]
        [InlineData(4255)]
        public void TheBushIsCheapAndFitsShortIndices(int seed)
        {
            //The wood is uploaded with 16-bit indices (the trap of #670), and the bush is drawn about forty times a pass, three times
            //over with the sun's map: a few thousand triangles each, not the tens of thousands of a tree
            ThornBushGeometry bush = ThornBushMesh.Build(3f, 3f, seed);

            Assert.True(bush.WoodVertices.Count <= ushort.MaxValue + 1, $"{seed}: {bush.WoodVertices.Count} wood vertices");
            int triangles = bush.WoodIndices.Count / 3 + bush.LeafIndices.Count / 3;
            _output.WriteLine($"{seed}: {bush.WoodIndices.Count / 3} wood and {bush.LeafIndices.Count / 3} leaf triangles");
            Assert.True(triangles <= 6000, $"{seed}: {triangles} triangles");
        }

        [Fact]
        public void TheTreesLeafShapeIsStillTheTiersAndTheirCardCountsAreWhatTheyWere()
        {
            //LeafSprays.HangOnTwigs takes its shape as a new argument (#883) and defaults to the tier's; the acacias and baobabs call it
            //without. Their card counts are what #882 measured (636 for a one-tier acacia, 1272 for a two-tier one, 19 716 and 21 624 for
            //the two test baobabs), so nothing a tree is laid in has moved
            const float W = 6f * 1.7f, H = 9f * 1.7f;
            Assert.Equal(636, AcaciaMesh.Build(AcaciaKind.Mature, W * 0.09f, H, W, 4100).LeafVertices.Count / 4);
            Assert.Equal(1272, AcaciaMesh.Build(AcaciaKind.Mature, W * 0.09f, H, W, 4101).LeafVertices.Count / 4);
            Assert.Equal(19716, BaobabMesh.Build(24f, 4700).LeafVertices.Count / 4);
            Assert.Equal(21624, BaobabMesh.Build(24f, 4701).LeafVertices.Count / 4);
        }
    }
}
