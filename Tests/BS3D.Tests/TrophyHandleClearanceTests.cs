using Prazsky.Core.Render;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// Where a beaded moulding has to stop short of a cup's handle (#724). The rows used to leave a hand-picked 0.16 radians
    /// either side of both handles, on the band's two rows alike; <see cref="TrophyMesh.HandleHalfAngle"/> reads the gap off
    /// the handle's own centreline and tube instead, so these are the promises of that reading and not figures to be tuned.
    /// </summary>
    public class TrophyHandleClearanceTests
    {
        //The ornaments' own figures: a bead's radius, and the margin the owner's ruling leaves between its surface and the tube
        private const float BEAD = 0.0065f;
        private const float CLEARANCE = 2f * BEAD;

        [Fact]
        public void TheBandsLowerRowNeedsNoGapAtAll()
        {
            //The tube crosses the band's wall in its upper half, well clear of the lower row
            Assert.Equal(0f, TrophyMesh.HandleHalfAngle(TrophyMesh.BAND_RADIUS, TrophyMesh.BAND_BOTTOM_Y, CLEARANCE));
        }

        [Fact]
        public void TheDrumsRowsAreFarFromTheHandle()
        {
            Assert.Equal(0f, TrophyMesh.HandleHalfAngle(TrophyMesh.DRUM_RADIUS, TrophyMesh.DRUM_BOTTOM_Y, CLEARANCE));
            Assert.Equal(0f, TrophyMesh.HandleHalfAngle(TrophyMesh.DRUM_RADIUS, TrophyMesh.DRUM_TOP_Y, CLEARANCE));
        }

        [Fact]
        public void TheBandsUpperRowStopsWhereTheTubeIsAndNoFurtherThanTheOldGuess()
        {
            float half = TrophyMesh.HandleHalfAngle(TrophyMesh.BAND_RADIUS, TrophyMesh.BAND_TOP_Y, CLEARANCE);

            //The tube is about 0.046 across at the wall, which is 0.15 of a radian of the band's arc at both of its sides
            //taken together: the gap is something, and it is under the 0.16 either side the rows used to leave
            Assert.InRange(half, 0.05f, 0.14f);
        }

        [Fact]
        public void AWiderMarginMakesAWiderGapAndNoMarginTheTubeAlone()
        {
            float tube = TrophyMesh.HandleHalfAngle(TrophyMesh.BAND_RADIUS, TrophyMesh.BAND_TOP_Y, BEAD);
            float margin = TrophyMesh.HandleHalfAngle(TrophyMesh.BAND_RADIUS, TrophyMesh.BAND_TOP_Y, 3f * BEAD);

            Assert.True(tube > 0f);
            Assert.True(margin > tube, $"{margin} should be wider than {tube}");
        }
    }
}
