using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The meadow trees' wood (#889). The owner, on the meadow: "the trunks and the branches of the trees do not connect up,
    /// and it is visible both in the flybys and while playing". Two faults: every limb's knee was an open ring (a bough
    /// leaves it thinner and at an angle, so the sky showed through the wood under back-face culling), plugged on every
    /// tier; and every limb was a tube through the trunk, grown into a crotch for High and Ultra
    /// (<see cref="MeadowTreeMesh.FineWood"/>, the acacia's #758 fork). Built with no device, the five trees
    /// <see cref="MeadowScatter"/> plants.
    /// </summary>
    public class MeadowForkTests
    {
        //The tree kind as its number (MeadowTreeKind is internal, and a public test cannot take it): 0 oak, 1 lime, 2 willow
        public static TheoryData<int, int> Trees() => new()
        {
            { 0, 6090 }, { 0, 6091 }, { 1, 6092 }, { 1, 6093 }, { 2, 6094 },
        };

        private static MeadowTreeGeometry Build(int kind, int seed, bool plugged = true) => MeadowTreeMesh.Build((MeadowTreeKind)kind, seed, plugged);

        /// <summary>
        /// No open edge stands at a knee - a limb's end, a bough's end, the leader's top - in either wood, on every tier. The
        /// same check over the wood built unplugged, as it was before #889, must find them, or a check that passes says
        /// nothing.
        /// </summary>
        [Theory]
        [MemberData(nameof(Trees))]
        public void NoKneeIsOpen(int kind, int seed)
        {
            MeadowTreeGeometry tree = Build(kind, seed);
            MeadowTreeGeometry unplugged = Build(kind, seed, plugged: false);

            int coarse = OpenAtKnees(tree.WoodVertices, tree.WoodIndices, tree.Knees);
            int fine = OpenAtKnees(tree.FineWoodVertices, tree.FineWoodIndices, tree.Knees);
            int before = OpenAtKnees(unplugged.WoodVertices, unplugged.WoodIndices, unplugged.Knees);

            Assert.True(coarse == 0, $"{kind} {seed}: {coarse} open edges at the coarse wood's knees");
            Assert.True(fine == 0, $"{kind} {seed}: {fine} open edges at the fine wood's knees");
            Assert.True(before > 0, $"{kind} {seed}: the check found no open knee on the unplugged wood, which has them - it is not seeing them");
        }

        /// <summary>The fine wood is closed everywhere: every piece ends in a knuckle or a disc made of its own last ring.</summary>
        [Theory]
        [MemberData(nameof(Trees))]
        public void TheFineWoodHasNoOpenEdge(int kind, int seed)
        {
            MeadowTreeGeometry tree = Build(kind, seed);
            List<(Vector3 A, Vector3 B)> open = OpenEdges(tree.FineWoodVertices, tree.FineWoodIndices);
            Assert.True(open.Count == 0, $"{kind} {seed}: {open.Count} open edges in the fine wood, the first at {(open.Count > 0 ? open[0].A : Vector3.Zero)}");
        }

        /// <summary>
        /// Every limb roots inside the grown trunk: its first ring lies within the trunk's surface, so the limb leaves it as
        /// one surface rather than starting outside it in a lip.
        /// </summary>
        [Theory]
        [MemberData(nameof(Trees))]
        public void EveryLimbRootsInsideTheTrunk(int kind, int seed)
        {
            MeadowTreeGeometry tree = Build(kind, seed);
            int outside = 0;
            Vector3 first = Vector3.Zero;
            foreach (Vector3 p in tree.FineLimbRoots)
            {
                if (tree.InsideFineTrunk(p)) continue;
                if (outside++ == 0) first = p;
            }
            Assert.True(outside == 0, $"{kind} {seed}: {outside} of {tree.FineLimbRoots.Count} limb root vertices stand outside the trunk, the first at {first}");
        }

        /// <summary>
        /// The trunk's rim at the fork is inside the limb leaving over it, so no band of the dome stands round a limb as a
        /// lip (#758's trap, photographed here first: a limb rooted inside the trunk and leaving along its surface fell
        /// behind the crotch's swell, and the rim showed as a ledge at the fork from every side).
        /// </summary>
        [Theory]
        [MemberData(nameof(Trees))]
        public void NoRimStandsRoundALimb(int kind, int seed)
        {
            MeadowTreeGeometry tree = Build(kind, seed);
            float worst = Lip(tree, out int at);
            Assert.True(worst < -0.01f * tree.Trunk, $"{kind} {seed}: the trunk's rim stands {worst:0.000} out of limb {at}, or less than a hundredth of the trunk inside it");
        }

        /// <summary>How far the rim stands out of the limbs at their bearings (negative: inside), the worst over the rim
        /// vertices near each limb's own bearing - a third of the way to a neighbour's, where its root circle is the rim -
        /// and which limb.</summary>
        internal static float Lip(MeadowTreeGeometry tree, out int worstLimb)
        {
            float worst = float.MinValue;
            worstLimb = -1;
            for (int l = 0; l < tree.FineLimbs.Count; l++)
            {
                (float angle, _) = tree.FineLimbBearings[l];
                float window = MathHelper.Pi / tree.FineLimbs.Count / 3f;
                (Vector3[] points, float[] radii) = tree.FineLimbs[l];
                foreach ((Vector3 at, float rimAngle) in tree.FineTrunkRim)
                {
                    float off = MathF.Abs(MathHelper.WrapAngle(rimAngle - angle));
                    if (off > window) continue;
                    float outside = float.MaxValue;
                    for (int p = 0; p + 1 < points.Length; p++)
                    {
                        Vector3 along = points[p + 1] - points[p];
                        float t = Math.Clamp(Vector3.Dot(at - points[p], along) / along.LengthSquared(), 0f, 1f);
                        float radius = MathHelper.Lerp(radii[p], radii[p + 1], t);
                        outside = MathF.Min(outside, Vector3.Distance(at, points[p] + along * t) - radius);
                    }
                    if (outside > worst) { worst = outside; worstLimb = l; }
                }
            }
            return worst;
        }

        /// <summary>The fine wood's branches end where the coarse wood's do, so every clump's twigs (#697) grow out of wood on
        /// every tier; and the fine wood stays within 16-bit indices.</summary>
        [Theory]
        [MemberData(nameof(Trees))]
        public void EveryTwigGrowsFromTheFineWood(int kind, int seed)
        {
            MeadowTreeGeometry tree = Build(kind, seed);
            foreach (Vector3 support in tree.SprayBases)
            {
                float nearest = float.MaxValue;
                foreach ((Vector3 from, Vector3 to) in tree.FineSkeleton) nearest = MathF.Min(nearest, DistanceToSegment(support, from, to));
                Assert.True(nearest < tree.Trunk * 0.01f, $"{kind} {seed}: a clump's twigs start {nearest:0.000} off the fine wood");
            }
            Assert.True(tree.FineWoodVertices.Count <= ushort.MaxValue + 1,
                $"{kind} {seed}: {tree.FineWoodVertices.Count} fine wood vertices, past what 16-bit indices reach");
        }

        /// <summary>The open edges near a knee: within its radius and a half of it.</summary>
        private static int OpenAtKnees(List<VertexPositionNormalTexture> vertices, List<short> indices, List<(Vector3 At, float Radius)> knees)
        {
            int open = 0;
            foreach ((Vector3 a, Vector3 b) in OpenEdges(vertices, indices))
            {
                Vector3 m = (a + b) * 0.5f;
                foreach ((Vector3 at, float radius) in knees)
                {
                    if (Vector3.Distance(m, at) > radius * 1.5f) continue;
                    open++;
                    break;
                }
            }
            return open;
        }

        /// <summary>The edges (of welded positions) only one triangle uses.</summary>
        private static List<(Vector3 A, Vector3 B)> OpenEdges(List<VertexPositionNormalTexture> vertices, List<short> indices) =>
            MeshOpenness.OpenEdges(vertices, indices);

        private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
        {
            Vector3 along = to - from;
            float t = along.LengthSquared() > 1e-12f ? Math.Clamp(Vector3.Dot(point - from, along) / along.LengthSquared(), 0f, 1f) : 0f;
            return Vector3.Distance(point, from + along * t);
        }
    }
}
