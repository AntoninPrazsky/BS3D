using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// Leaves grow out of wood (#782). The owner, on the savanna: "on some trees there are leaves in the air that do not
    /// connect to any branch — leaves and needles always grow out of branches, never out of the air!" A tree is built
    /// with no device (<see cref="AcaciaMesh.Build"/>, <see cref="BaobabMesh.Build"/>) and every leaf card's stem — the
    /// middle of its <c>u</c> = 0 edge, where <c>Acacia.fx</c>'s mask starts the spray — is measured against the tree's
    /// skeleton, every tube it was swept along: no stem may stand farther from the wood than the card is long.
    /// </summary>
    public class LeafAttachmentTests
    {
        //The savanna's own figures (SavannaSceneConfig: Width 6, Height 9, TreeSize 1.7; the scatter's proportions)
        private const float ACACIA_WIDTH = 6f * 1.7f, ACACIA_HEIGHT = 9f * 1.7f, BAOBAB_HEIGHT = 24f;

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
