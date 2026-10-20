using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// What rests on a slope rests on it (#938). The savanna's logs lay level at the height of their middle with a random lean,
    /// and its flat-bottomed rocks and mounds stood at the ground under their centre and were then tipped: on a slope an end
    /// hung in the air, the other was buried to the axis, and a lean showed the flat underside over the grass. Held on
    /// grounds of the plain's own kind (a plane at a slope, a rolling sum of sines), each construction seen to fail.
    /// </summary>
    public class GroundSettlingTests
    {
        private static Func<float, float, float> Slope(float rise) => (x, z) => rise * x + 0.3f * rise * z;

        private static readonly Func<float, float, float> ROLLING = (x, z) => 2.2f * MathF.Sin(x * 0.11f + 0.4f) * MathF.Cos(z * 0.07f) + 0.9f * MathF.Sin(z * 0.21f + x * 0.05f);

        /// <summary>The rim of a tipped underside against the ground, the highest point of it over the ground at its own spot (positive is in the air).</summary>
        private static float RimFloat(Func<float, float, float> ground, float x, float z, float y, float rim, Matrix orientation)
        {
            float worst = float.MinValue;
            for (int k = 0; k < 64; k++)
            {
                float a = MathHelper.TwoPi * k / 64;
                Vector3 p = Vector3.TransformNormal(new Vector3(MathF.Cos(a) * rim, 0f, MathF.Sin(a) * rim), orientation);
                worst = MathF.Max(worst, y + p.Y - ground(x + p.X, z + p.Z));
            }
            return worst;
        }

        [Theory]
        [InlineData(0f, 0f, 0.1f)]
        [InlineData(0.15f, 0.2f, 0.3f)]
        [InlineData(0.3f, 0.35f, 0.5f)]
        [InlineData(-0.25f, 0.1f, 0.2f)]
        public void ARockRestsWithItsWholeRimUnderTheGround(float slope, float lean, float burialShare)
        {
            Func<float, float, float> ground = Slope(slope);
            foreach (float yaw in new[] { 0f, 1.1f, 2.7f, 4.4f })
                foreach (float leanDir in new[] { 0f, 1.6f, 3.2f, 4.9f })
                {
                    Matrix orientation = Matrix.CreateScale(0.7f) * Matrix.CreateFromAxisAngle(new Vector3(MathF.Cos(leanDir), 0f, MathF.Sin(leanDir)), lean) * Matrix.CreateRotationY(yaw);
                    float burial = 6f * 0.7f * burialShare;
                    float y = GroundSettling.RestHeight(ground, 40f, -25f, 6f, orientation, burial);
                    //The sampled points are exactly at the burial; between two of them a plane's rim is under it by a hair
                    Assert.True(RimFloat(ground, 40f, -25f, y, 6f, orientation) <= -burial + 0.06f,
                        $"slope {slope}, lean {lean}, yaw {yaw}, lean direction {leanDir}: the rim stands {RimFloat(ground, 40f, -25f, y, 6f, orientation) + burial:0.000} over its burial");
                }
        }

        /// <summary>The fault: at the height of the ground under its centre, tipped, a rock's rim floats on a slope and on a lean.</summary>
        [Fact]
        public void ARockPlantedAtTheGroundUnderItsCentreFloatedOnASlope()
        {
            Func<float, float, float> ground = Slope(0.2f);
            Matrix orientation = Matrix.CreateScale(0.6f) * Matrix.CreateFromAxisAngle(Vector3.UnitX, 0.3f) * Matrix.CreateRotationY(0.8f);
            float old = RimFloat(ground, 40f, -25f, ground(40f, -25f) - 6f * 0.6f * 0.25f, 6f, orientation);
            Assert.True(old > 0.5f, $"the old planting's rim stands {old:0.00} over the ground - the check is not seeing the float");
        }

        [Fact]
        public void ARockOnARollingGroundIsBuriedAndNotSunkPastItsNeed()
        {
            Matrix orientation = Matrix.CreateScale(1f) * Matrix.CreateFromAxisAngle(Vector3.UnitZ, 0.2f) * Matrix.CreateRotationY(0.5f);
            for (float x = -90f; x <= 90f; x += 30f)
            {
                float y = GroundSettling.RestHeight(ROLLING, x, 17f, 5f, orientation, 0.2f);
                float rim = RimFloat(ROLLING, x, 17f, y, 5f, orientation);
                Assert.True(rim <= -0.2f + 0.35f, $"x {x}: the rim stands {rim:0.00} (the samples guard the burial; a hair of the ground's curvature between them)");
                //Sunk past its need: some sample sits at the burial
                Assert.True(rim > -0.2f - 1.5f, $"x {x}: sunk {-rim:0.00} under the ground, more than the slope and the lean ask");
            }
        }

        /// <summary>A rock on a rock: a flat top 10 up and 5 wide, nothing beyond it.</summary>
        private static float Plinth(float x, float z) => MathF.Sqrt(x * x + z * z) < 5f ? 10f : float.NegativeInfinity;

        [Fact]
        public void ARockPerchesOnTheHighestSupportUnderItsTippedRimAndOverhangsWhereThereIsNone()
        {
            //Wider than the plinth: the rim (3.5 from a spot 3 off the middle) reaches past its edge, where there is no support
            Matrix flat = Matrix.CreateScale(1f);
            float y = GroundSettling.PerchHeight(Plinth, 3f, 0f, 3.5f, flat, 0.6f);
            Assert.Equal(10f - 0.6f, y, 3);

            //Tipped, the high side of the rim stands over the plinth and the rock perches on the highest point under it
            Matrix tipped = Matrix.CreateFromAxisAngle(Vector3.UnitZ, 0.3f);
            float perched = GroundSettling.PerchHeight(Plinth, 0f, 0f, 2f, tipped, 0f);
            float lowestRim = float.MinValue;
            for (int k = 0; k < 64; k++)
            {
                float a = MathHelper.TwoPi * k / 64;
                Vector3 p = Vector3.TransformNormal(new Vector3(MathF.Cos(a) * 2f, 0f, MathF.Sin(a) * 2f), tipped);
                lowestRim = MathF.Max(lowestRim, 10f - p.Y);
            }
            Assert.True(perched >= lowestRim - 0.05f, $"perched at {perched:0.000}, a rim point of the tipped rock would stand in the plinth under {lowestRim:0.000}");

            //Nothing under it at all: the caller rests it on the ground instead
            Assert.True(float.IsNaN(GroundSettling.PerchHeight(Plinth, 30f, 0f, 2f, flat, 0f)));
        }

        /// <summary>The fault the first cut of the shoulder rocks had: rested with the ground rule on a rock they overhang, they sink all the way to the ground the overhang reaches.</summary>
        [Fact]
        public void RestingARockThatOverhangsItsSupportSinksItToTheGround()
        {
            Func<float, float, float> terrainAndPlinth = (x, z) => MathF.Max(0f, Plinth(x, z));
            float rested = GroundSettling.RestHeight(terrainAndPlinth, 3f, 0f, 3.5f, Matrix.Identity, 0.6f);
            Assert.True(rested < 0f, $"rested at {rested:0.0}: it sank to the ground under the overhang, where the stack was wanted");
        }

        /// <summary>The underside of a log, as a straight tapered stick laid by the pivot's line.</summary>
        private static float WorstFloat(Func<float, float, float> ground, float x, float z, float yaw, float length, float underside, float pitch, float y, out float worstBuried)
        {
            float dx = MathF.Cos(yaw), dz = -MathF.Sin(yaw);
            float float_ = float.MinValue; worstBuried = float.MaxValue;
            for (int k = 0; k <= 40; k++)
            {
                float t = k / 40f - 0.5f;
                float along = length * t * MathF.Cos(pitch);
                float bottom = y + length * t * MathF.Sin(pitch) + underside;
                float gap = bottom - ground(x + dx * along, z + dz * along);
                float_ = MathF.Max(float_, gap);
                worstBuried = MathF.Min(worstBuried, gap);
            }
            return float_;
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(0.1f)]
        [InlineData(0.25f)]
        [InlineData(-0.2f)]
        public void ALogLiesAlongTheSlopeWithNoEndInTheAir(float rise)
        {
            Func<float, float, float> ground = Slope(rise);
            foreach (float yaw in new[] { 0f, 0.9f, 2.0f, 3.5f, 5.2f })
            {
                (float pitch, float y) = GroundSettling.LayLog(ground, 10f, 20f, yaw, 9f, -0.13f, 0.1f);
                float over = WorstFloat(ground, 10f, 20f, yaw, 9f, -0.13f, pitch, y, out float buried);
                Assert.True(over <= -0.1f + 1e-3f, $"rise {rise}, yaw {yaw}: a part of the log's underside stands {over:0.000} over the ground (it is to be at least 0.1 under)");
                Assert.True(buried > -0.1f - 0.05f, $"rise {rise}, yaw {yaw}: the log is buried {-buried:0.000} at its deepest on a plane, which asks for no more than its sink");
            }
        }

        [Fact]
        public void ALogOnARollingGroundHasNoEndInTheAirEither()
        {
            for (float x = -120f; x <= 120f; x += 17f)
                foreach (float yaw in new[] { 0.3f, 1.9f, 4.1f })
                {
                    (float pitch, float y) = GroundSettling.LayLog(ROLLING, x, -33f, yaw, 10f, -0.15f, 0.12f);
                    float over = WorstFloat(ROLLING, x, -33f, yaw, 10f, -0.15f, pitch, y, out _);
                    Assert.True(over <= -0.12f + 0.03f, $"x {x}, yaw {yaw}: the log stands {over:0.000} over the ground");
                }
        }

        /// <summary>The fault: level at the height of its middle, a log's ends hang and bury by half its length times the slope.</summary>
        [Fact]
        public void ALogLaidLevelAtItsMiddlesHeightHungAnEndInTheAir()
        {
            Func<float, float, float> ground = Slope(0.2f);
            float yaw = 0.7f, length = 9f;
            float old = WorstFloat(ground, 10f, 20f, yaw, length, -0.15f, 0f, ground(10f, 20f), out float buried);
            Assert.True(old > 0.3f, $"the old log's end stands {old:0.00} over the ground - the check is not seeing it");
            Assert.True(buried < -0.3f, $"the old log's other end is buried {-buried:0.00} - the check is not seeing it");
        }

        /// <summary>A log's pitch is the ground's slope along it, not across it.</summary>
        [Fact]
        public void ALogsPitchIsTheSlopeAlongIt()
        {
            Func<float, float, float> ground = (x, z) => 0.2f * x;   //rising along +X only
            (float along, _) = GroundSettling.LayLog(ground, 0f, 0f, 0f, 8f, -0.1f, 0.1f);      //yaw 0: +X is +X
            (float across, _) = GroundSettling.LayLog(ground, 0f, 0f, MathF.PI / 2f, 8f, -0.1f, 0.1f);   //yaw 90: +X is -Z, level
            Assert.Equal(MathF.Atan(0.2f), along, 3);
            Assert.Equal(0f, across, 3);
        }
    }
}
