using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The acacia's fork grown, not assembled (#758), for High and Ultra (<see cref="AcaciaMesh.FineWood"/>). The owner, on
    /// the savanna: "the places where the tree trunks turn into branches look like they do not fit: not smooth like on real
    /// trees". The coarse wood Low and Medium keep is a trunk tube cut off flat at the fork with the boughs' tubes starting
    /// at its centre; the fine wood closes the trunk inside the boughs it divides into. Built with no device, as
    /// <see cref="LeafAttachmentTests"/> builds it, at the scatter's own proportions.
    /// </summary>
    public class AcaciaForkTests
    {
        private const float ACACIA_WIDTH = 6f * 1.7f, ACACIA_HEIGHT = 9f * 1.7f;

        public static TheoryData<AcaciaKind, int> Acacias() => new()
        {
            { AcaciaKind.Mature, 4100 }, { AcaciaKind.Mature, 4101 }, { AcaciaKind.Mature, 4102 }, { AcaciaKind.Mature, 4103 },
            { AcaciaKind.Broken, 4110 }, { AcaciaKind.Young, 4120 }, { AcaciaKind.Young, 4121 },
            { AcaciaKind.Dead, 4130 }, { AcaciaKind.Dead, 4131 },
        };

        private static (AcaciaGeometry Tree, float Trunk) Build(AcaciaKind kind, int seed)
        {
            float trunk = ACACIA_WIDTH * kind switch { AcaciaKind.Young => 0.055f, AcaciaKind.Dead => 0.085f, _ => 0.09f };
            float height = ACACIA_HEIGHT * (kind == AcaciaKind.Young ? 0.6f : 1f);
            float canopy = ACACIA_WIDTH * (kind == AcaciaKind.Young ? 0.9f : 1f);
            return (AcaciaMesh.Build(kind, trunk, height, canopy, seed), trunk);
        }

        /// <summary>
        /// No open end of the wood near the fork is wider than a twig's unless the trunk closes over it: the coarse wood's
        /// flat-cut trunk top and the boughs' root rings standing in it are exactly that, and the fine wood's boughs root
        /// inside the trunk. The same check is run over the coarse wood and must find its cut end, or a check that passes
        /// the fine wood says nothing.
        /// </summary>
        [Theory]
        [MemberData(nameof(Acacias))]
        public void TheForkHasNoCutEnd(AcaciaKind kind, int seed)
        {
            (AcaciaGeometry tree, float trunk) = Build(kind, seed);
            float forkY = tree.Volume[0].Top;

            int fine = CutEnds(tree.FineWoodVertices, tree.FineWoodIndices, tree.FineTrunkRadius, forkY, trunk);
            int coarse = CutEnds(tree.WoodVertices, tree.WoodIndices, tree.FineTrunkRadius, forkY, trunk);

            Assert.True(fine == 0, $"{kind} {seed}: {fine} open edges of the fine wood stand out of the trunk at the fork");
            Assert.True(coarse > 0, $"{kind} {seed}: the check found no cut end on the coarse wood, which has one - it is not seeing it");
        }

        /// <summary>
        /// The fine wood's boughs and limbs end where the coarse wood's do, so the crown's twigs - one set, in both woods -
        /// grow out of wood on every tier (#782's promise, held for the second wood).
        /// </summary>
        [Theory]
        [MemberData(nameof(Acacias))]
        public void EveryTwigOfTheFineWoodGrowsFromItsBoughs(AcaciaKind kind, int seed)
        {
            (AcaciaGeometry tree, float trunk) = Build(kind, seed);

            foreach (Vector3 support in tree.Supports)
            {
                float nearest = float.MaxValue;
                foreach ((Vector3 from, Vector3 to) in tree.FineSkeleton) nearest = MathF.Min(nearest, DistanceToSegment(support, from, to));
                Assert.True(nearest < trunk * 0.01f, $"{kind} {seed}: a twig's root stands {nearest:0.000} off the fine wood");
            }
            Assert.True(tree.FineWoodVertices.Count <= ushort.MaxValue + 1, $"{kind} {seed}: {tree.FineWoodVertices.Count} fine wood vertices, past what 16-bit indices reach");
        }

        /// <summary>
        /// The open edges (welded positions used by one triangle) between the crotch's foot and a little over the fork that
        /// are longer than a twig ring's and do not lie inside the grown trunk.
        /// </summary>
        private static int CutEnds(List<VertexPositionNormalTexture> vertices, List<short> indices, Func<float, float, float> trunkRadius,
            float forkY, float trunk)
        {
            var weld = new Dictionary<(int, int, int), int>();
            var ids = new int[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p = vertices[i].Position;
                var key = ((int)MathF.Round(p.X * 1e4f), (int)MathF.Round(p.Y * 1e4f), (int)MathF.Round(p.Z * 1e4f));
                if (!weld.TryGetValue(key, out int id)) weld[key] = id = weld.Count;
                ids[i] = id;
            }

            var edges = new Dictionary<(int, int), (int Count, Vector3 A, Vector3 B)>();
            for (int t = 0; t + 2 < indices.Count; t += 3)
            {
                for (int e = 0; e < 3; e++)
                {
                    int i = (ushort)indices[t + e], j = (ushort)indices[t + (e + 1) % 3];
                    int a = ids[i], b = ids[j];
                    if (a == b) continue;
                    var key = a < b ? (a, b) : (b, a);
                    edges[key] = edges.TryGetValue(key, out var seen)
                        ? (seen.Count + 1, seen.A, seen.B)
                        : (1, vertices[i].Position, vertices[j].Position);
                }
            }

            int cut = 0;
            float twigEdge = trunk * 0.15f;
            foreach ((int count, Vector3 a, Vector3 b) in edges.Values)
            {
                if (count != 1 || Vector3.Distance(a, b) <= twigEdge) continue;
                Vector3 m = (a + b) * 0.5f;
                if (m.Y < forkY - 2f * trunk || m.Y > forkY + trunk) continue;
                bool inside = m.Y < forkY - 1e-3f
                    && new Vector2(m.X, m.Z).Length() < trunkRadius(m.Y, MathF.Atan2(m.Z, m.X));
                if (!inside) cut++;
            }
            return cut;
        }

        private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
        {
            Vector3 along = to - from;
            float t = along.LengthSquared() > 1e-12f ? Math.Clamp(Vector3.Dot(point - from, along) / along.LengthSquared(), 0f, 1f) : 0f;
            return Vector3.Distance(point, from + along * t);
        }
    }
}
