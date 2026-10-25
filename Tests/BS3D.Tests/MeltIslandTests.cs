using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The dream island's melting rim (#779, <see cref="IslandShape.Melt"/>): the drips <see cref="IslandMesh"/> pushes out of
    /// the side leave the rings every shape keeps exact where they were, never reach past the platform's widest point, and
    /// do run down the wall.
    /// </summary>
    public class MeltIslandTests
    {
        private const float RADIUS = ArenaIsland.RADIUS;
        private const int SAMPLES = 4096;

        private static readonly LathePoint[] RUN = IslandMesh.MeltRun(RADIUS);
        private static readonly Func<float, float, float> DRIPS = IslandMesh.MeltDrips(RUN);

        //The run's own radius at a height, between its points
        private static float ProfileAt(float y)
        {
            for (int k = 1; k < RUN.Length; k++)
            {
                if (y >= RUN[k].Y)
                    return MathHelper.Lerp(RUN[k - 1].Radius, RUN[k].Radius, (RUN[k - 1].Y - y) / (RUN[k - 1].Y - RUN[k].Y));
            }
            return RUN[^1].Radius;
        }

        [Fact]
        public void NothingAboveTheRollsEquatorMoves()
        {
            //The floor's arris (y 0), the lip and the roll's crown: the physics floor and the gold bead read these
            foreach (float y in new[] { 0f, -0.05f, -0.32f, RUN[0].Y })
            {
                for (int s = 0; s < SAMPLES; s++)
                    Assert.Equal(0f, DRIPS(MathHelper.TwoPi * s / SAMPLES, y));
            }
        }

        [Fact]
        public void NothingUnderTheRunMoves()
        {
            //The drum's turn under and the underside: a drip never runs round under the platform
            foreach (float y in new[] { RUN[^1].Y - 0.01f, -4.95f, -5.2f })
            {
                for (int s = 0; s < SAMPLES; s++)
                    Assert.Equal(0f, DRIPS(MathHelper.TwoPi * s / SAMPLES, y));
            }
        }

        [Fact]
        public void NoDripReachesPastTheRim()
        {
            //The roll's widest is the platform's widest point: the bounding sphere and every camera's clearance read it
            for (float y = RUN[0].Y; y >= RUN[^1].Y; y -= 0.05f)
            {
                float profile = ProfileAt(y);
                for (int s = 0; s < SAMPLES; s++)
                {
                    float radius = profile + DRIPS(MathHelper.TwoPi * s / SAMPLES, y);
                    Assert.True(radius < RADIUS, $"a drip at y {y:F2} stands at radius {radius:F3}, past the rim's {RADIUS}");
                }
            }
        }

        [Fact]
        public void LongDripsRunDownTheWall()
        {
            //At the wall's top, under the hollow, some drips still stand well proud of it, and not all of the ring does
            int proud = 0;
            for (int s = 0; s < SAMPLES; s++)
            {
                if (DRIPS(MathHelper.TwoPi * s / SAMPLES, -2.6f) > 0.4f) proud++;
            }
            Assert.InRange(proud, SAMPLES / 50, SAMPLES / 2);
        }
    }
}
