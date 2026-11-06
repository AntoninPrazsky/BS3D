using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The grid's light cycles turn only right angles (#1069, the owner: "they sometimes turn by 180 degrees - only by 90").
    /// A rider makes its route node by node; where the move it rolled would leave the ring it tries another, and the first
    /// picker reversed (the way it came) after trying only two of the three, so a cycle at the ring's edge turned round.
    /// </summary>
    public class LightCycleRouteTests
    {
        //Many riders and a long run each: a reversal at the ring's edge is rare per node and common per minute
        private const int SEEDS = 200;
        private const float RUN = 10000f;
        private const float STEP = 4f;

        [Fact]
        public void No_rider_ever_reverses()
        {
            int reversals = 0, turns = 0;
            for (int seed = 0; seed < SEEDS; seed++)
            {
                var rider = new LightCycles.Rider(seed * 7919 + 13);
                rider.At(0f, out _, out Vector2 previous);
                for (float d = STEP; d < RUN; d += STEP)
                {
                    rider.At(d, out _, out Vector2 heading);
                    float dot = Vector2.Dot(previous, heading);
                    if (dot < -0.5f) reversals++;
                    else if (dot < 0.5f) turns++;
                    previous = heading;
                }
            }

            Assert.True(turns > 1000, $"the riders turned only {turns} times: the test is not exercising the picker");
            Assert.Equal(0, reversals);
        }
    }
}
