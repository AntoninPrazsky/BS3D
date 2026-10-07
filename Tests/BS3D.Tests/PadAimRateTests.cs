using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The right stick's aim (#819): a curve that makes the first part of the travel fine, a full push that is still
    /// the whole rate, a diagonal that keeps its direction, and a lens scale that slows it exactly.
    /// </summary>
    public class PadAimRateTests
    {
        private const float TOLERANCE = 1e-5f;

        [Fact]
        public void A_full_push_is_the_whole_rate()
        {
            Vector2 up = MouseAim.PadAimRate(new Vector2(0f, 1f), 1f);
            Assert.Equal(MouseAim.PAD_RATE, up.X, TOLERANCE);   //stick up -> pitch up
            Assert.Equal(0f, up.Y, TOLERANCE);

            Vector2 left = MouseAim.PadAimRate(new Vector2(-1f, 0f), 1f);
            Assert.Equal(MouseAim.PAD_RATE, left.Y, TOLERANCE); //stick left -> yaw left
        }

        [Theory]
        [InlineData(0.1f)]
        [InlineData(0.3f)]
        [InlineData(0.5f)]
        [InlineData(0.8f)]
        public void Part_of_the_travel_asks_less_than_its_share(float push)
        {
            float rate = MouseAim.PadAimRate(new Vector2(push, 0f), 1f).Length();
            Assert.True(rate < push * MouseAim.PAD_RATE, $"a push of {push} asked {rate}");
            Assert.True(rate > 0f);
        }

        [Fact]
        public void A_diagonal_keeps_its_direction()
        {
            Vector2 stick = new(0.4f, 0.3f);
            Vector2 rate = MouseAim.PadAimRate(stick, 1f);

            //rate is (pitch, yaw) = (stick.Y, -stick.X) scaled: the ratio of the two survives the curve
            Assert.Equal(stick.Y / stick.X, rate.X / -rate.Y, TOLERANCE);
        }

        [Fact]
        public void The_lens_scale_slows_it_exactly()
        {
            Vector2 stick = new(0.6f, -0.2f);
            Vector2 overview = MouseAim.PadAimRate(stick, 1f);
            Vector2 leaned = MouseAim.PadAimRate(stick, 0.3f);

            Assert.Equal(overview.X * 0.3f, leaned.X, TOLERANCE);
            Assert.Equal(overview.Y * 0.3f, leaned.Y, TOLERANCE);
        }

        [Fact]
        public void A_resting_stick_asks_nothing() => Assert.Equal(Vector2.Zero, MouseAim.PadAimRate(Vector2.Zero, 1f));
    }
}
