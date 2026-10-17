using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// How thick an acacia's crown tiers are (#884). The owner, on the savanna: some crowns are "far too flat ... stretch the
    /// crowns up a little". A tier's radius and the tree's height are rolled independently (<c>SavannaScatter</c>), and its
    /// thickness used to be a fixed share of the height whatever the roll, so a mature tier ran from 3.0 to 7.0 times wider than
    /// its half-thickness. Now each kind's tier has one thickness, never thinner than the cap lets its radius be, and it is
    /// stretched <i>upward</i> only: the nominal underside (the volume slab's bottom), the radius and the trunk stay where #663 left
    /// them - the drawn plate's own underside is flattened and rises by about 0.6 of what the tier grew, which
    /// <see cref="TheDrawnPlateReachesAboutFourTenthsOfItsHalfThicknessUnderItsCentre"/> pins. Built with no device
    /// (<see cref="AcaciaMesh.Build"/>), at the roll the savanna draws trees at.
    /// </summary>
    public class AcaciaCrownTests
    {
        //The savanna's own figures (SavannaSceneConfig: Width 6, Height 9, TreeSize 1.7) and the proportions the scatter rolls each
        //kind at (SavannaScatter): the width roll, the height roll, the trunk's share of the width and the crown's
        private const float WIDTH = 6f * 1.7f, HEIGHT = 9f * 1.7f;

        //What #663 left, as shares of the tree's height: where each kind's first tier's nominal underside stood (the centre less
        //the half-thickness), the half-thickness it had, and the same for the second tier of a mature tree
        private const float MATURE_FLOOR = 0.64f, MATURE_WAS = 0.14f;
        private const float BROKEN_FLOOR = 0.63f, BROKEN_WAS = 0.13f;
        private const float YOUNG_FLOOR = 0.73f, YOUNG_WAS = 0.11f;
        private const float SECOND_FLOOR = 0.84f, SECOND_WAS = 0.10f;

        /// <summary>Every kind at the ends and the middle of its roll, with the structural seeds the scatter builds it with.</summary>
        public static TheoryData<AcaciaKind, float, float, int> Rolls()
        {
            var data = new TheoryData<AcaciaKind, float, float, int>();
            foreach (int seed in new[] { 4100, 4101, 4102, 4103 })
                foreach (float w in new[] { 0.8f, 1f, 1.25f })
                    foreach (float h in new[] { 0.85f, 1f, 1.25f })
                        data.Add(AcaciaKind.Mature, w, h, seed);
            foreach (float w in new[] { 0.9f, 1.05f, 1.2f })
                foreach (float h in new[] { 0.9f, 1.1f })
                    data.Add(AcaciaKind.Broken, w, h, 4110);
            foreach (int seed in new[] { 4120, 4121 })
                foreach (float w in new[] { 0.7f, 0.85f, 1f })
                    foreach (float h in new[] { 0.55f, 0.7f })
                        data.Add(AcaciaKind.Young, w, h, seed);
            return data;
        }

        private static (AcaciaGeometry Tree, float TreeHeight) Grow(AcaciaKind kind, float w, float h, int seed)
        {
            float width = WIDTH * w;
            float treeHeight = HEIGHT * h;
            float trunk = width * (kind == AcaciaKind.Young ? 0.055f : 0.09f);
            float canopy = kind == AcaciaKind.Young ? width * 0.9f : width;
            return (AcaciaMesh.Build(kind, trunk, treeHeight, canopy, seed), treeHeight);
        }

        private static float Radius(Slab tier) => tier.Radius / AcaciaMesh.CROWN_SOLID;
        private static float Half(Slab tier) => (tier.Top - tier.Bottom) * 0.5f;

        [Theory]
        [MemberData(nameof(Rolls))]
        public void NoTierRunsWiderAgainstItsThicknessThanTheCap(AcaciaKind kind, float w, float h, int seed)
        {
            (AcaciaGeometry tree, _) = Grow(kind, w, h, seed);

            for (int t = 1; t < tree.Volume.Length; t++)
            {
                float aspect = Radius(tree.Volume[t]) / Half(tree.Volume[t]);
                Assert.True(aspect <= AcaciaMesh.MAX_TIER_ASPECT + 1e-3f,
                    $"{kind} {seed} at w {w} h {h}: tier {t} is {aspect:0.00} times wider than its half-thickness, over the cap {AcaciaMesh.MAX_TIER_ASPECT}");
            }
        }

        [Theory]
        [MemberData(nameof(Rolls))]
        public void TheNominalUndersideStaysWhereItStood(AcaciaKind kind, float w, float h, int seed)
        {
            (AcaciaGeometry tree, float treeHeight) = Grow(kind, w, h, seed);
            (float floor, float was) = kind switch
            {
                AcaciaKind.Mature => (MATURE_FLOOR, MATURE_WAS),
                AcaciaKind.Broken => (BROKEN_FLOOR, BROKEN_WAS),
                _ => (YOUNG_FLOOR, YOUNG_WAS),
            };

            Assert.Equal(floor * treeHeight, tree.Volume[1].Bottom, 1e-3f * treeHeight);

            //A second tier stands on the first and rides up with the first's top (the top stands 1 - TopFlatten of the half-thickness
            //over the centre, so what the tier grew is counted twice less the flattening), keeping its place on it
            if (tree.Volume.Length > 2)
            {
                float rise = (Half(tree.Volume[1]) - was * treeHeight) * (2f - FoliageStyle.Tier.TopFlatten);
                Assert.Equal(SECOND_FLOOR * treeHeight + rise, tree.Volume[2].Bottom, 1e-3f * treeHeight);
            }
        }

        /// <summary>
        /// The nominal underside above is the volume slab's, not the mesh's: <see cref="FoliageStyle.Tier"/> flattens the plate's
        /// underside (<c>BottomFlatten</c>) and the rim's lobes swell it, so its lowest point stands only 0.40 to 0.45 of the
        /// half-thickness under its centre (measured over these rolls; asserted between 0.3 and 0.55). That is why the drawn
        /// underside of a tier that grew by <c>g</c> rises by about <c>(1 - 0.4) g</c> while the slab's does not, and the
        /// slab, always the lower, is the conservative one for the planting to keep apart by.
        /// </summary>
        [Theory]
        [MemberData(nameof(Rolls))]
        public void TheDrawnPlateReachesAboutFourTenthsOfItsHalfThicknessUnderItsCentre(AcaciaKind kind, float w, float h, int seed)
        {
            (AcaciaGeometry tree, _) = Grow(kind, w, h, seed);

            //Every tier is the same tessellation, so each has the same share of the canopy's vertices
            int tiers = tree.Volume.Length - 1;
            int perTier = tree.CanopyVertices.Count / tiers;
            Assert.Equal(0, tree.CanopyVertices.Count % tiers);

            for (int t = 1; t <= tiers; t++)
            {
                float lowest = float.MaxValue;
                for (int i = (t - 1) * perTier; i < t * perTier; i++)
                    lowest = MathF.Min(lowest, tree.CanopyVertices[i].Position.Y);

                float centre = (tree.Volume[t].Top + tree.Volume[t].Bottom) * 0.5f;
                float reach = (centre - lowest) / Half(tree.Volume[t]);
                Assert.True(reach > 0.3f && reach < 0.55f,
                    $"{kind} {seed} at w {w} h {h}: tier {t}'s drawn plate reaches {reach:0.00} of its half-thickness under its centre, not about 0.4");
                Assert.True(lowest > tree.Volume[t].Bottom, $"{kind} {seed}: tier {t}'s drawn plate dips under the slab the planting keeps apart by");
            }
        }

        [Theory]
        [MemberData(nameof(Rolls))]
        public void NoTierIsThinnerThanItWasAndTheNominalOneIsThicker(AcaciaKind kind, float w, float h, int seed)
        {
            (AcaciaGeometry tree, float treeHeight) = Grow(kind, w, h, seed);

            Assert.True(Half(tree.Volume[1]) >= (kind switch { AcaciaKind.Mature => MATURE_WAS, AcaciaKind.Broken => BROKEN_WAS, _ => YOUNG_WAS }) * treeHeight - 1e-3f);
            if (tree.Volume.Length > 2) Assert.True(Half(tree.Volume[2]) >= SECOND_WAS * treeHeight - 1e-3f);

            //The middle of the roll is thicker than #663's, the owner's "a little"
            if (kind == AcaciaKind.Mature && w == 1f && h == 1f)
                Assert.True(Half(tree.Volume[1]) > MATURE_WAS * treeHeight * 1.15f,
                    $"a mature tier at the middle of the roll is {Half(tree.Volume[1]) / treeHeight:0.000} of the tree's height half-thick, no thicker than #663's {MATURE_WAS}");
        }

        [Theory]
        [MemberData(nameof(Rolls))]
        public void TheWidthIsWhatItWas(AcaciaKind kind, float w, float h, int seed)
        {
            (AcaciaGeometry tree, _) = Grow(kind, w, h, seed);

            //A mature tree's first tier is the canopy radius itself: only the thickness is stretched, never the width
            if (kind == AcaciaKind.Mature) Assert.Equal(WIDTH * w, Radius(tree.Volume[1]), 1e-3f);
        }
    }
}
