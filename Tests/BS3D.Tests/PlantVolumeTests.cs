using Prazsky.Core.Render;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The rule that keeps planted things apart by what they are made of (#653): slabs that meet in height and whose axes
    /// are closer than their radii clip, less a small tolerance. The owner's flythrough of the savanna found trees standing
    /// through trees, rocks and mounds; measured on the shipped plain over five seeds the footprint rule alone left
    /// 7, 19, 12, 9 and 14 pairs sunk into each other past the tolerance, and with this one none. What is checked here is
    /// the rule's own arithmetic; the plain's placement is measured in <c>docs/scenes.md</c>, because building it needs a
    /// graphics device.
    /// </summary>
    public class PlantVolumeTests
    {
        //A tree: a trunk slab from the ground to 3 and a crown slab from 4 to 5, 2 wide
        private static readonly Slab[] Tree = { new(0f, 3f, 0.3f), new(4f, 5f, 2f) };

        //A low bush: 0 to 1.2, 1.5 wide — it lives under a crown and never meets it
        private static readonly Slab[] Bush = { new(0f, 1.2f, 1.5f) };

        private static float ClearOf(float x, Slab[] slabs, float scale, params (float x, Slab[] slabs, float scale)[] standing)
        {
            var placed = new List<PlantVolumes.Placed>();
            foreach ((float px, Slab[] ps, float pscale) in standing) placed.Add(new PlantVolumes.Placed(px, 0f, 0f, pscale, ps));
            return PlantVolumes.Clearance(x, 0f, 0f, scale, slabs, placed);
        }

        [Fact]
        public void TwoCrownsFarApartAreClear()
        {
            Assert.True(ClearOf(6f, Tree, 1f, (0f, Tree, 1f)) > 0f);
        }

        [Fact]
        public void TwoCrownsThatInterpenetrateClip()
        {
            //Crowns of radius 2 with axes 2.5 apart sink 1.5 into each other, far past the tolerance
            Assert.True(ClearOf(2.5f, Tree, 1f, (0f, Tree, 1f)) < -1f);
        }

        [Fact]
        public void ATrunkInsideANeighboursCrownClips()
        {
            //A trunk standing 1 from another tree's axis is under its crown, and the crown slab is at the trunk's own height
            //only if the trunk reaches it: this tree's trunk stops at 3 and the crown starts at 4, so it does NOT clip
            Assert.True(ClearOf(1f, new Slab[] { new(0f, 3f, 0.3f) }, 1f, (0f, Tree, 1f)) > 0f);

            //A taller trunk goes through the same crown
            Assert.True(ClearOf(1f, new Slab[] { new(0f, 6f, 0.3f) }, 1f, (0f, Tree, 1f)) < 0f);
        }

        [Fact]
        public void ThingsThatNeverMeetInHeightAreNotCompared()
        {
            //A bush sits right under the crown's axis: the two overlap in plan by a mile and never touch
            Assert.True(ClearOf(0.5f, Bush, 1f, (0f, new Slab[] { new(4f, 5f, 2f) }, 1f)) > 0f);
        }

        [Fact]
        public void ASmallSinkIsFine()
        {
            //Two crowns of radius 2, axes 3.9 apart: they sink 0.1, under the tolerance (a share of the smaller radius)
            Assert.True(ClearOf(3.9f, new Slab[] { new(4f, 5f, 2f) }, 1f, (0f, new Slab[] { new(4f, 5f, 2f) }, 1f)) > 0f);

            //3.6 apart sinks 0.4, past the tolerance of 0.15 x 2 = 0.3
            Assert.True(ClearOf(3.6f, new Slab[] { new(4f, 5f, 2f) }, 1f, (0f, new Slab[] { new(4f, 5f, 2f) }, 1f)) < 0f);
        }

        [Fact]
        public void ScaleGrowsTheVolumeAsWellAsTheSize()
        {
            //A tree twice the size takes twice the room: 6 apart clears at scale 1 and clips at scale 2
            Assert.True(ClearOf(6f, Tree, 1f, (0f, Tree, 1f)) > 0f);
            Assert.True(ClearOf(6f, Tree, 2f, (0f, Tree, 2f)) < 0f);
        }

        [Fact]
        public void ACrownOffItsTrunksAxisIsMeasuredWhereItStands()
        {
            //A crown set 3 to the side of its trunk: a neighbour 5 away on the far side is 2 from that crown's axis
            var leaning = new Slab[] { new(0f, 3f, 0.3f), new(4f, 5f, 2f, offsetX: 3f) };
            Assert.True(ClearOf(-5f, Tree, 1f, (0f, leaning, 1f)) > 0f);

            //and on the same side it is 2 from the crown's axis and clips it
            Assert.True(ClearOf(5f, Tree, 1f, (0f, leaning, 1f)) < 0f);
        }

        [Fact]
        public void TheWorstNeighbourDecides()
        {
            //Clear of one, sunk into another: the answer is the second's
            float margin = ClearOf(0f, Tree, 1f, (20f, Tree, 1f), (2.5f, Tree, 1f));
            Assert.True(margin < -1f);
        }

        [Fact]
        public void NothingStandingLeavesEveryPlaceClear()
        {
            Assert.True(float.IsPositiveInfinity(ClearOf(0f, Tree, 1f)));
        }
    }
}
