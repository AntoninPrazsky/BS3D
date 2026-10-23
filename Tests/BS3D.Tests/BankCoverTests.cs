using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The line inside which a scene that wears the island's bank as its ground turns its terrain away (#983,
    /// <see cref="IslandBerm.CoverRadius"/>, <c>BankCover.fxh</c>): the bank must stand over the ground everywhere inside it,
    /// or the terrain turned away there would leave a hole the bank does not fill.
    /// </summary>
    public class BankCoverTests
    {
        private const float GROUND = -13.5f;
        private static float Flat(float x, float z) => GROUND;

        [Fact]
        public void The_bank_stands_over_the_ground_everywhere_inside_its_cover()
        {
            foreach (int seed in new[] { 9831, 9200, 9300, 0, 1, 12345 })
                for (int i = 0; i < 1440; i++)
                {
                    float angle = MathF.Tau * i / 1440f;
                    float cover = IslandBerm.CoverRadius(seed, angle);

                    for (float r = ArenaIsland.RADIUS + 0.1f; r < cover; r += 0.05f)
                    {
                        float surface = IslandBerm.SurfaceY(Flat, seed, MathF.Cos(angle) * r, MathF.Sin(angle) * r);
                        Assert.True(surface > GROUND + 0.05f,
                            $"seed {seed}, {angle:0.000} rad, r {r:0.00}: the bank at {surface:0.000} over ground {GROUND} inside its cover {cover:0.00}");
                    }
                }
        }

        //And the measure can fail: past the cover the bank goes under the ground, so a cover drawn too far out would be caught
        [Fact]
        public void Past_its_last_ring_the_bank_is_under_the_ground()
        {
            foreach (int seed in new[] { 9831, 9200, 9300 })
                for (int i = 0; i < 360; i++)
                {
                    float angle = MathF.Tau * i / 360f;
                    float r = ArenaIsland.RADIUS + (IslandBerm.CoverRadius(seed, angle) - ArenaIsland.RADIUS) / IslandBerm.COVER_REACH * 4.3f;
                    float surface = IslandBerm.SurfaceY(Flat, seed, MathF.Cos(angle) * r, MathF.Sin(angle) * r);
                    Assert.True(surface < GROUND, $"seed {seed}, {angle:0.000} rad: the bank at {surface:0.000} past its last ring");
                }
        }
    }
}
