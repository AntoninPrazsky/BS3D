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
    /// Leaves grow out of wood (#782). The owner, on the savanna: "on some trees there are leaves in the air that do not
    /// connect to any branch — leaves and needles always grow out of branches, never out of the air!" A tree is built
    /// with no device (<see cref="AcaciaMesh.Build"/>, <see cref="BaobabMesh.Build"/>) and every leaf card's stem — the
    /// middle of its <c>u</c> = 0 edge, where <c>Acacia.fx</c>'s mask starts the spray — is measured against the tree's
    /// skeleton, every tube it was swept along: no stem may stand farther from the wood than the card is long.
    /// <para>
    /// And they grow <i>along</i> their wood (#882): the owner, on the same crowns, "the leaves do not point along the
    /// direction the branches grow, the way they would in reality". A card's direction (its stem to its tip) is
    /// measured against the direction of the skeleton piece nearest its stem — the twig it hangs on, as a stem is on it —
    /// and no more than a few in a hundred may point back towards the twig's root.
    /// </para>
    /// </summary>
    public class LeafAttachmentTests
    {
        private readonly ITestOutputHelper _output;

        public LeafAttachmentTests(ITestOutputHelper output) => _output = output;

        //The savanna's own figures (SavannaSceneConfig: Width 6, Height 9, TreeSize 1.7; the scatter's proportions)
        private const float ACACIA_WIDTH = 6f * 1.7f, ACACIA_HEIGHT = 9f * 1.7f, BAOBAB_HEIGHT = 24f;

        //A spray leaves its twig forwards, a lean off the tangent of some forty degrees; it is the twig's root a card must
        //not point to (more than a right angle off the tangent). None does as built (40 to 65 % of an acacia's cards did when
        //every spray pointed out of the tier's centre); a few in a hundred is slack for a seed where two pieces of wood
        //cross at a stem and the nearest is not the twig's own
        private const float MAX_BACKWARDS_SHARE = 0.03f;

        //And lie near flat: the pitch off the horizontal the steepest tenth of the cards reach (19 to 23 degrees as built; 13 to 17
        //before the sprays followed their twigs)
        private const float MAX_PITCH_P90 = 30f;

        //A thorn bush's leaf is a mound, not a plate (#883): its twigs climb from the branch ends to the top layer and the cards follow
        //half of that, so the steepest tenth reach 31 to 33 degrees where a tier's 21 to 25; still near flat, a spray never stands on end
        private const float MAX_BUSH_PITCH_P90 = 36f;

        public static TheoryData<AcaciaKind, int> Acacias() => new()
        {
            { AcaciaKind.Mature, 4100 }, { AcaciaKind.Mature, 4101 }, { AcaciaKind.Mature, 4102 }, { AcaciaKind.Mature, 4103 },
            { AcaciaKind.Broken, 4110 }, { AcaciaKind.Young, 4120 }, { AcaciaKind.Young, 4121 },
        };

        [Theory]
        [MemberData(nameof(Acacias))]
        public void EveryAcaciaLeafGrowsFromItsWood(AcaciaKind kind, int seed)
        {
            float trunk = ACACIA_WIDTH * (kind == AcaciaKind.Young ? 0.055f : 0.09f);
            float height = ACACIA_HEIGHT * (kind == AcaciaKind.Young ? 0.6f : 1f);
            float canopy = ACACIA_WIDTH * (kind == AcaciaKind.Young ? 0.9f : 1f);
            AcaciaGeometry tree = AcaciaMesh.Build(kind, trunk, height, canopy, seed);

            AssertAttached(tree.LeafVertices, tree.Skeleton, $"{kind} {seed}");
            AssertFitsShortIndices(tree.WoodVertices.Count, $"{kind} {seed}");
        }

        [Theory]
        [InlineData(4700)]
        [InlineData(4701)]
        public void EveryBaobabLeafGrowsFromItsWood(int seed)
        {
            BaobabGeometry tree = BaobabMesh.Build(BAOBAB_HEIGHT, seed);

            AssertAttached(tree.LeafVertices, tree.Skeleton, $"baobab {seed}");
            AssertFitsShortIndices(tree.WoodVertices.Count, $"baobab {seed}");
        }

        [Theory]
        [MemberData(nameof(Acacias))]
        public void EveryAcaciaLeafPointsAlongItsTwig(AcaciaKind kind, int seed)
        {
            float trunk = ACACIA_WIDTH * (kind == AcaciaKind.Young ? 0.055f : 0.09f);
            float height = ACACIA_HEIGHT * (kind == AcaciaKind.Young ? 0.6f : 1f);
            float canopy = ACACIA_WIDTH * (kind == AcaciaKind.Young ? 0.9f : 1f);
            AcaciaGeometry tree = AcaciaMesh.Build(kind, trunk, height, canopy, seed);

            AssertAlongWood(tree.LeafVertices, tree.Skeleton, $"{kind} {seed}");
        }

        [Theory]
        [InlineData(4700)]
        [InlineData(4701)]
        public void EveryBaobabLeafPointsAlongItsTwig(int seed)
        {
            BaobabGeometry tree = BaobabMesh.Build(BAOBAB_HEIGHT, seed);

            AssertAlongWood(tree.LeafVertices, tree.Skeleton, $"baobab {seed}");
        }

        //The savanna's thorn bushes (#883): the sizes the scatter makes them at under the dome's own roll (SavannaScatter.ThornSize: a
        //reach of 2.34 to 3.70, from 0.78 to 0.88 of a dome radius of 3.0 to 4.2, and a top of 0.75 to 0.97 of that reach, never over
        //0.8 of the dome's height, 3.17 at the lowest), with the structural seeds it builds them with. The two ends of each roll are
        //in it: the smallest and the largest reach, and the lowest and the highest share of it
        public static TheoryData<float, float, int> ThornBushes() => new()
        {
            { 2.34f, 1.76f, 4250 }, { 2.60f, 2.10f, 4251 }, { 3.70f, 3.17f, 4252 },
            { 3.70f, 3.59f, 4253 }, { 3.00f, 2.91f, 4254 }, { 3.40f, 2.55f, 4255 },
        };

        [Theory]
        [MemberData(nameof(ThornBushes))]
        public void EveryThornBushLeafGrowsFromItsWood(float radius, float height, int seed)
        {
            ThornBushGeometry bush = ThornBushMesh.Build(radius, height, seed);

            AssertAttached(bush.LeafVertices, bush.Skeleton, $"thorn bush {seed}");
            AssertFitsShortIndices(bush.WoodVertices.Count, $"thorn bush {seed}");
        }

        [Theory]
        [MemberData(nameof(ThornBushes))]
        public void EveryThornBushLeafPointsAlongItsTwig(float radius, float height, int seed)
        {
            ThornBushGeometry bush = ThornBushMesh.Build(radius, height, seed);

            AssertAlongWood(bush.LeafVertices, bush.Skeleton, $"thorn bush {seed}", MAX_BUSH_PITCH_P90);
        }

        private void AssertAlongWood(IReadOnlyList<VertexPositionNormalTexture> leaves, IReadOnlyList<(Vector3 From, Vector3 To)> skeleton, string tree,
            float maxPitchP90 = MAX_PITCH_P90)
        {
            Assert.True(leaves.Count > 0, $"{tree}: no leaves");

            int cards = leaves.Count / 4, backwards = 0;
            var angles = new List<float>(cards);
            var pitches = new List<float>(cards);
            for (int card = 0; card + 3 < leaves.Count; card += 4)
            {
                Vector3 stem = (leaves[card].Position + leaves[card + 1].Position) * 0.5f;
                Vector3 end = (leaves[card + 2].Position + leaves[card + 3].Position) * 0.5f;
                Vector3 along = Vector3.Normalize(end - stem);

                float nearest = float.MaxValue;
                Vector3 tangent = Vector3.Up;
                foreach ((Vector3 from, Vector3 to) in skeleton)
                {
                    float d = DistanceToSegment(stem, from, to);
                    if (d < nearest && (to - from).LengthSquared() > 1e-12f) { nearest = d; tangent = Vector3.Normalize(to - from); }
                }

                float dot = Vector3.Dot(along, tangent);
                if (dot < 0f) backwards++;
                angles.Add(MathHelper.ToDegrees(MathF.Acos(Math.Clamp(dot, -1f, 1f))));
                pitches.Add(MathHelper.ToDegrees(MathF.Asin(Math.Clamp(MathF.Abs(along.Y), 0f, 1f))));
            }

            angles.Sort();
            pitches.Sort();
            float share = backwards / (float)cards;
            _output.WriteLine($"{tree}: {backwards} of {cards} cards ({share * 100f:0.0} %) point back along their twig, median {angles[angles.Count / 2]:0} degrees off its tangent, 90th percentile {angles[angles.Count * 9 / 10]:0}; pitch off the horizontal median {pitches[pitches.Count / 2]:0}, 90th percentile {pitches[pitches.Count * 9 / 10]:0}, steepest {pitches[^1]:0}");

            Assert.True(share <= MAX_BACKWARDS_SHARE,
                $"{tree}: {backwards} of {cards} leaf cards ({share * 100f:0.0} %) point against the wood they hang on, the median {angles[angles.Count / 2]:0} degrees off its direction");

            //Along the twig, but still laid near flat: the crown is thin tiers of foliage (#610), and a spray that followed every
            //twig's climb in full would stand them on end
            float steep = pitches[pitches.Count * 9 / 10];
            Assert.True(steep <= maxPitchP90, $"{tree}: the steepest tenth of the leaf cards are tilted {steep:0} degrees or more off the horizontal");
        }

        private static void AssertAttached(IReadOnlyList<VertexPositionNormalTexture> leaves, IReadOnlyList<(Vector3 From, Vector3 To)> skeleton, string tree)
        {
            Assert.True(leaves.Count > 0, $"{tree}: no leaves");

            int detached = 0;
            float worst = 0f;
            for (int card = 0; card + 3 < leaves.Count; card += 4)
            {
                Vector3 stem = (leaves[card].Position + leaves[card + 1].Position) * 0.5f;
                Vector3 end = (leaves[card + 2].Position + leaves[card + 3].Position) * 0.5f;
                float length = Vector3.Distance(stem, end);

                float nearest = float.MaxValue;
                foreach ((Vector3 from, Vector3 to) in skeleton) nearest = MathF.Min(nearest, DistanceToSegment(stem, from, to));

                if (nearest > length) detached++;
                worst = MathF.Max(worst, nearest / MathF.Max(length, 1e-4f));
            }

            Assert.True(detached == 0, $"{tree}: {detached} of {leaves.Count / 4} leaf cards stand off the wood, the worst {worst:0.0} card lengths away");
        }

        //The wood is uploaded with 16-bit indices, and the twigs (#782) add to it: past 65 536 vertices the indices would
        //wrap onto the first ones without a word, which is how a baobab's leaves once lost a sixth of their cards (#670)
        private static void AssertFitsShortIndices(int vertices, string tree) =>
            Assert.True(vertices <= ushort.MaxValue + 1, $"{tree}: {vertices} wood vertices, past what 16-bit indices reach");

        private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
        {
            Vector3 along = to - from;
            float t = along.LengthSquared() > 1e-12f ? Math.Clamp(Vector3.Dot(point - from, along) / along.LengthSquared(), 0f, 1f) : 0f;
            return Vector3.Distance(point, from + along * t);
        }
    }
}
