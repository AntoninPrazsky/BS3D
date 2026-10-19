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
    /// The mounds the savanna's campfires stand on (#887): the ground is raised under every fire to one seat under the island's plate, so the
    /// flames rise from the same height whatever the relief and the island's rim hides little of them
    /// (<see cref="CampfireRimClearanceTests"/>). <c>Savanna.fx</c>'s <c>HearthMound</c> is the same arithmetic on the GPU, which the Testbed's
    /// <c>mirrorcheck</c> compares with <see cref="TerrainMirror.Savanna"/>; what is pinned here is the mirror's shape and the places it must
    /// not reach, with no device.
    /// </summary>
    public class CampfireMoundTests
    {
        private readonly ITestOutputHelper _output;

        public CampfireMoundTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void TheMoundIsFlatOnTopAndReachesNothingPastItsFoot()
        {
            var config = new SavannaSceneConfig();
            CampfireConfig fires = config.Campfire;

            for (int fire = 0; fire < SavannaBackdrop.CampfireRingCount(config); fire++)
            {
                Vec2 centre = SavannaBackdrop.CampfireRingXZ(config, fire);
                float lift = TerrainMirror.SavannaMoundLift(config, fire);
                Assert.True(lift > 0f && lift < 6.5f, $"fire {fire}'s mound is {lift:0.00} high");

                //Along a ray out of the fire, the lift is the whole of it on the top, falls as the distance grows and is gone at the foot
                float previous = float.MaxValue;
                for (float d = 0f; d <= fires.MoundFoot + 1f; d += 0.25f)
                {
                    float at = TerrainMirror.SavannaMound(centre.X + d, centre.Y, config);
                    if (d <= fires.MoundTop) Assert.Equal(lift, at, 3);
                    if (d >= fires.MoundFoot) Assert.Equal(0f, at, 5);
                    Assert.True(at <= previous + 1e-4f, $"fire {fire}: the mound rises again {d:0.00} out");
                    previous = at;
                }
            }
        }

        [Fact]
        public void TheMoundsNeverTouchAndTheSlopeIsGentle()
        {
            var config = new SavannaSceneConfig();
            CampfireConfig fires = config.Campfire;
            int count = SavannaBackdrop.CampfireRingCount(config);

            //Centre to centre on the ring against two feet
            Vec2 a = SavannaBackdrop.CampfireRingXZ(config, 0), b = SavannaBackdrop.CampfireRingXZ(config, 1);
            float spacing = MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
            _output.WriteLine($"{count} mounds {spacing:0.0} apart centre to centre, feet {fires.MoundFoot:0.0} each");
            Assert.True(spacing > 2f * fires.MoundFoot + 2f, $"the feet touch: {spacing:0.0} apart against two of {fires.MoundFoot:0.0}");

            //The steepest the ground climbs, over the whole ring of mounds, as an angle (a smoothstep is 1.5 times the average slope at its middle)
            float steepest = 0f;
            for (int fire = 0; fire < count; fire++)
            {
                Vec2 centre = SavannaBackdrop.CampfireRingXZ(config, fire);
                for (float d = 0f; d < fires.MoundFoot; d += 0.1f)
                {
                    float rise = TerrainMirror.SavannaMound(centre.X + d, centre.Y, config) - TerrainMirror.SavannaMound(centre.X + d + 0.1f, centre.Y, config);
                    steepest = MathF.Max(steepest, MathF.Atan(rise / 0.1f));
                }
            }

            _output.WriteLine($"steepest slope of a mound: {steepest * 180f / MathF.PI:0.0} degrees");
            Assert.True(steepest < 44f * MathF.PI / 180f, $"the steepest slope is {steepest * 180f / MathF.PI:0.0} degrees");
        }

        [Fact]
        public void TheMoundBanksAtMostTwoUnitsAgainstTheDrumsWall()
        {
            //The inner foot of a mound reaches in under the island: the terrain is cut at the drum's own radius and the wall stands to ArenaIsland.RADIUS.
            //What is left of the mound there is the grass banked against the stone, and the plain's own relief at the island's foot is 2.6 either way
            var config = new SavannaSceneConfig();
            float worst = 0f;

            for (int fire = 0; fire < SavannaBackdrop.CampfireRingCount(config); fire++)
            {
                Vec2 centre = SavannaBackdrop.CampfireRingXZ(config, fire);
                float bearing = MathF.Atan2(centre.Y, centre.X);
                worst = MathF.Max(worst, TerrainMirror.SavannaMound(MathF.Cos(bearing) * ArenaIsland.RADIUS, MathF.Sin(bearing) * ArenaIsland.RADIUS, config));
            }

            _output.WriteLine($"the most a mound banks against the drum's wall at r = {ArenaIsland.RADIUS}: {worst:0.00}");
            Assert.True(worst <= 2f, $"a mound rises {worst:0.00} against the island's wall");
        }

        [Fact]
        public void WithTheMoundsOffTheGroundIsThePlainsOwn()
        {
            //What the Potato path relies on (it draws no fires and plants the plain with the mounds off): every height is the relief, bit for bit
            var config = new SavannaSceneConfig();
            config.Campfire.MoundFootScale = 0f;

            for (float x = -60f; x <= 60f; x += 3.7f)
                for (float z = -60f; z <= 60f; z += 3.3f)
                {
                    Assert.Equal(TerrainMirror.SavannaPlain(x, z, config), TerrainMirror.Savanna(x, z, config));
                    Assert.Equal(0f, TerrainMirror.SavannaMound(x, z, config));
                }
        }

        [Fact]
        public void ThePotatoPathPlantsThePlainWithoutTheMounds()
        {
            //PotatoBackdrop.Savanna needs a graphics device to run, so what can be pinned is that it switches the mounds off before it builds its ground and
            //its scatter from the one height function. Dropping the line plants eight bumps on a path that draws no fire.
            Assert.Matches(@"config\.Campfire\.MoundFootScale\s*=\s*0f\s*;\s*float Height\(float x, float z\)\s*=>\s*TerrainMirror\.Savanna\(x, z, config\)", Code("PotatoBackdrop.cs"));
        }

        [Fact]
        public void TheHearthStonesAreLaidOnTheFlatTopAtTheSeat()
        {
            //BuildHearthStones plants each stone at GroundHeight(x, z), the mirror, at most 1.12 times the ring's radius out (its jitter). On the flat top the
            //ground is the seat plus the plain's own relief across the hearth, a few tenths: the stones stand level with the fire, not up a slope
            var config = new SavannaSceneConfig();
            float seat = ArenaIsland.TOP_Y - config.Campfire.SeatBelowPlate;
            float stoneRing = config.Campfire.FlameSize * config.Campfire.StoneRingScale * 1.12f;

            Assert.True(stoneRing <= config.Campfire.MoundTop + 1e-3f, $"the farthest a stone is laid ({stoneRing:0.00}) is off the flat top ({config.Campfire.MoundTop:0.00})");

            for (int fire = 0; fire < SavannaBackdrop.CampfireRingCount(config); fire++)
            {
                Vec2 centre = SavannaBackdrop.CampfireRingXZ(config, fire);
                for (float angle = 0f; angle < MathHelper.TwoPi; angle += 0.4f)
                {
                    float ground = TerrainMirror.Savanna(centre.X + MathF.Cos(angle) * stoneRing, centre.Y + MathF.Sin(angle) * stoneRing, config);
                    Assert.InRange(ground, seat - 0.5f, seat + 0.5f);
                }
            }
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
