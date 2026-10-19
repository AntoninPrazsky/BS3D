using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The savanna's campfires light the island's top plate (#885). The fires burn 2.5 units below the plate on their mounds (#887; on
    /// the plain's own relief, 2.8 to 7.0 below it, <see cref="ThePlainsReliefUnderTheRingStandsThreeToSevenUnitsBelowThePlate"/>), and a point light below an up-facing surface has N.L at or under zero there: with the light at the ground
    /// point the ring lit the coping and the drum, which face outward, and never the plate round the gun (the owner's
    /// note of 2026-10-16). The light the island, the gun and the balls take therefore stands at
    /// <see cref="CampfireConfig.LightHeightAboveTerrain"/>, the flame's height over the plate, while the flame and the
    /// grass's own light stay on the ground. The arithmetic here is the instanced shader's own (<c>AddSceneLights</c>:
    /// <c>saturate(dot(N, L)) * saturate(1 - d / range)^2</c>) over the real config and the real terrain mirror, with no
    /// device, because the question is geometry.
    /// </summary>
    public class CampfirePlateLightTests
    {
        private readonly ITestOutputHelper _output;

        public CampfirePlateLightTests(ITestOutputHelper output) => _output = output;

        private const int BEARINGS = 48;
        private const int RADII = 12;

        /// <summary>
        /// What the ring's scene lights put on the plate at one point: the sum over the fires of N.L times the
        /// quadratic falloff, the shader's diffuse term without the colour. N is the plate's own normal, the dish
        /// tilting <see cref="ArenaIsland.DISH_GRADE"/> towards the axis (away from every fire).
        /// </summary>
        private static float PlateIrradiance(SavannaSceneConfig config, float heightAboveTerrain, float radius, float bearing)
        {
            float x = MathF.Cos(bearing) * radius, z = MathF.Sin(bearing) * radius;
            Vector3 point = new(x, ArenaIsland.FloorHeightAt(radius), z);

            //The dish falls towards the axis, so the surface rises outward and its normal leans inward
            Vector3 outward = new(MathF.Cos(bearing), 0f, MathF.Sin(bearing));
            Vector3 normal = Vector3.Normalize(new Vector3(0f, 1f, 0f) - outward * ArenaIsland.DISH_GRADE);

            float total = 0f;
            int fires = SavannaBackdrop.CampfireRingCount(config);
            for (int fire = 0; fire < fires; fire++)
            {
                Vector3 toLight = SavannaBackdrop.CampfireRingPoint(config, fire, heightAboveTerrain) - point;
                float distance = toLight.Length();

                float falloff = MathHelper.Clamp(1f - distance / config.Campfire.Range, 0f, 1f);
                total += MathHelper.Clamp(Vector3.Dot(normal, toLight / distance), 0f, 1f) * falloff * falloff;
            }

            return total;
        }

        /// <summary>The plate's irradiance over its whole ring (radius and bearing), as its least and its mean.</summary>
        private static (float least, float mean) OverThePlate(SavannaSceneConfig config, float heightAboveTerrain)
        {
            float least = float.MaxValue, sum = 0f;

            for (int r = 0; r < RADII; r++)
            {
                float radius = MathHelper.Lerp(ArenaIsland.FUNNEL_TOP_RADIUS, ArenaIsland.FLOOR_RADIUS, r / (RADII - 1f));

                for (int b = 0; b < BEARINGS; b++)
                {
                    float value = PlateIrradiance(config, heightAboveTerrain, radius, b * MathHelper.TwoPi / BEARINGS);
                    least = MathF.Min(least, value);
                    sum += value;
                }
            }

            return (least, sum / (RADII * BEARINGS));
        }

        [Fact]
        public void AFireAtTheGroundLightsNothingOnThePlate()
        {
            //The fault, stated: at the height the flame and the grass's light stand at, every fire is below the lowest
            //point of the plate, so the plate takes nothing from the ring. This is the control the next test is read against.
            var config = new SavannaSceneConfig();
            float ground = config.Campfire.HeightAboveTerrain;

            (float least, float mean) = OverThePlate(config, ground);
            _output.WriteLine($"light {ground:0.0} above the ground: plate least {least:0.0000}, mean {mean:0.0000}");

            Assert.Equal(0f, mean);
        }

        [Fact]
        public void ThePlainsReliefUnderTheRingStandsThreeToSevenUnitsBelowThePlate()
        {
            //The relief the mounds (#887) stand on, measured so the comments' "3 to 7 units" cannot drift: the plain under the ring (the
            //mounds off, height 0, no lift) against the plate's top, TOP_Y.
            var config = new SavannaSceneConfig();
            config.Campfire.MoundFootScale = 0f;
            int fires = SavannaBackdrop.CampfireRingCount(config);
            float nearest = float.MaxValue, farthest = float.MinValue;

            for (int fire = 0; fire < fires; fire++)
            {
                float ground = SavannaBackdrop.CampfireRingPoint(config, fire, 0f).Y;
                float gap = ArenaIsland.TOP_Y - ground;
                _output.WriteLine($"fire {fire}: ground y {ground:0.00}, {gap:0.00} below the plate");
                nearest = MathF.Min(nearest, gap);
                farthest = MathF.Max(farthest, gap);
            }

            Assert.InRange(nearest, 2.5f, 3.2f);
            Assert.InRange(farthest, 6.5f, 7.5f);
        }

        [Fact]
        public void EveryFireStandsOnItsMoundAtTheSameSeatUnderThePlate()
        {
            //With the mounds on (#887) every fire stands at one height, SeatBelowPlate under the plate, whatever the relief under it
            var config = new SavannaSceneConfig();
            int fires = SavannaBackdrop.CampfireRingCount(config);
            float seat = ArenaIsland.TOP_Y - config.Campfire.SeatBelowPlate;

            for (int fire = 0; fire < fires; fire++)
            {
                float ground = SavannaBackdrop.CampfireRingPoint(config, fire, 0f).Y;
                _output.WriteLine($"fire {fire}: ground y {ground:0.00}, {ArenaIsland.TOP_Y - ground:0.00} below the plate");
                Assert.Equal(seat, ground, 3);
            }
        }

        [Fact]
        public void TheIslandsLightStandsAboveThePlate()
        {
            var config = new SavannaSceneConfig();
            int fires = SavannaBackdrop.CampfireRingCount(config);

            for (int fire = 0; fire < fires; fire++)
            {
                Vector3 light = SavannaBackdrop.CampfireRingPoint(config, fire, config.Campfire.LightHeightAboveTerrain);
                Assert.True(light.Y > ArenaIsland.TOP_Y + 1f,
                    $"fire {fire}'s light stands at y {light.Y:0.00}, not clear of the plate at {ArenaIsland.TOP_Y}: it lights the coping and the drum and never the plate");
            }
        }

        [Fact]
        public void EveryPointOfThePlateTakesLightFromTheRing()
        {
            var config = new SavannaSceneConfig();

            (float least, float mean) = OverThePlate(config, config.Campfire.LightHeightAboveTerrain);
            _output.WriteLine($"light {config.Campfire.LightHeightAboveTerrain:0.0} above the ground: plate least {least:0.0000}, mean {mean:0.0000}");

            //The nearest side of the plate sits well inside the range; the far side of the ring is the weakest point,
            //and it still takes some. A light that did not reach it would leave a dark patch the ring should not.
            Assert.True(least > 0.005f, $"the least lit point of the plate takes {least:0.0000} from the ring");
            Assert.True(mean > 0.03f, $"the plate takes {mean:0.0000} from the ring on average");
        }

        [Fact]
        public void TheMoundsLeaveThePlateTheLightItWasJudgedAt()
        {
            //#885 was judged at a plate mean of 0.079 (least 0.015). Raising the fires onto their mounds (#887) raised the light with them, and at the
            //old 0.65 of the flame it took the mean to 0.122; LightHeightScale 0.45 keeps the light where it stood (y -4.8, was -4.5 on average).
            var config = new SavannaSceneConfig();

            (float least, float mean) = OverThePlate(config, config.Campfire.LightHeightAboveTerrain);
            _output.WriteLine($"light {config.Campfire.LightHeightAboveTerrain:0.0} above the seat: plate least {least:0.0000}, mean {mean:0.0000}");

            Assert.InRange(mean, 0.065f, 0.095f);
            Assert.True(least >= 0.015f, $"the least lit point of the plate takes {least:0.0000}, under the 0.015 it was judged at");
        }

        [Fact]
        public void TheRaisedLightKeepsTheFiresBearingAndGround()
        {
            //Only the height differs: the light stands over the fire, not beside it, and the flame and the grass
            //(CampfirePosition) keep the ground point they always read.
            var config = new SavannaSceneConfig();
            int fires = SavannaBackdrop.CampfireRingCount(config);

            for (int fire = 0; fire < fires; fire++)
            {
                Vector3 low = SavannaBackdrop.CampfireRingPoint(config, fire, config.Campfire.HeightAboveTerrain);
                Vector3 high = SavannaBackdrop.CampfireRingPoint(config, fire, config.Campfire.LightHeightAboveTerrain);

                Assert.Equal(low.X, high.X);
                Assert.Equal(low.Z, high.Z);
                Assert.Equal(config.Campfire.LightHeightAboveTerrain - config.Campfire.HeightAboveTerrain, high.Y - low.Y, 3);
            }
        }

        [Fact]
        public void TheSceneLightsTakeTheRaisedPositionAndTheGrassTheGroundOne()
        {
            //SceneLights and the grass's loop need an Effect to run, so what can be pinned without a device is which
            //accessor each one reads. Swapping them back is the whole regression: the plate goes dark again, or the
            //ground glow lifts off the grass.
            Assert.Matches(@"_lightPosition\[fire\]\s*=\s*sceneRenderer\.SavannaCampfireLightPosition\(fire\)", Code("SceneLights.cs"));
            Assert.Matches(@"_savannaLightPos\[fire\]\s*=\s*CampfirePosition\(fire\)", Code(Path.Combine("Scenes", "SavannaBackdrop.cs")));
        }

        private static string Code(string fileInRender)
        {
            string relative = Path.Combine("BS3DLibs", "Prazsky.Core", "Render", fileInRender);

            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return Regex.Replace(File.ReadAllText(candidate), @"//[^\n]*", string.Empty);
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}
