using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// How much of each savanna campfire the island's rim lets the play camera see (#887). The owner's note of
    /// 2026-10-16: only the flames' tips show. The play camera stands about 30 units out and level with the island's
    /// stone, 1.1 over the plate (<c>[tilt] pos 0,-7.4,30</c> on every savanna level), and the fires burn on grass 3 to
    /// 7 units below the plate 33 units out, so the far arris hides the lower part of every flame. The arithmetic here
    /// is a sight line from the lens over that arris to the fire's vertical axis, with no device, because the
    /// question is geometry: a point of the flame is hidden when the segment from the lens to it is under the plate's
    /// height where it leaves the disc of the walkable top (the dish and the coping only fall away from there, so the
    /// arris is the highest rim the line can meet).
    /// </summary>
    public class CampfireRimClearanceTests
    {
        private readonly ITestOutputHelper _output;

        public CampfireRimClearanceTests(ITestOutputHelper output) => _output = output;

        //The play camera on the savanna levels, read off the Game's logtilt line at the rest pose: 30.0 to 30.6 out from the
        //island's axis and 7.4 up from the world's zero, the gun at rest. The share is read at the nearest and the farthest.
        private const float LENS_HEIGHT = -7.4f;
        private static readonly float[] LENS_OUT = { 29.9f, 30.6f };

        //The Game's vertical field of view and the two widths it plays at (16:9, the 3840 x 1600 desk monitor): which fires are in the frame
        private static readonly float GAME_FOV = MathF.PI / 4.2f;
        private static readonly float[] ASPECTS = { 16f / 9f, 3840f / 1600f };

        /// <summary>The height of the lowest point of the vertical line over <paramref name="fire"/> the lens at <paramref name="lens"/> can see, or the line's base when all of it shows.</summary>
        private static float ClearHeight(Vector3 lens, Vector3 fire)
        {
            //Where the segment lens -> fire leaves the walkable top's disc: the larger root of |lens + t (fire - lens)|^2 = r^2 over the horizontal
            Vector2 from = new(lens.X, lens.Z), along = new(fire.X - lens.X, fire.Z - lens.Z);
            float a = Vector2.Dot(along, along), b = 2f * Vector2.Dot(from, along);
            float c = Vector2.Dot(from, from) - ArenaIsland.FLOOR_RADIUS * ArenaIsland.FLOOR_RADIUS;
            float discriminant = b * b - 4f * a * c;
            if (discriminant <= 0f) return float.NegativeInfinity; //the line misses the disc: nothing hides the fire

            float exit = (-b + MathF.Sqrt(discriminant)) / (2f * a);
            if (exit <= 0f || exit >= 1f) return float.NegativeInfinity;

            //The point on the fire's axis whose sight line is exactly at the plate's height where it leaves the disc
            return lens.Y + (ArenaIsland.TOP_Y - lens.Y) / exit;
        }

        /// <summary>The share of the flame billboard's height the rim leaves in view, 0 to 1.</summary>
        private static float VisibleShare(SavannaSceneConfig config, Vector3 lens, int fire)
        {
            Vector3 foot = SavannaBackdrop.CampfireRingPoint(config, fire, config.Campfire.HeightAboveTerrain);
            float height = config.Campfire.FlameSize * config.Campfire.FlameHeightScale;
            float clear = ClearHeight(lens, foot);
            return MathHelper.Clamp((foot.Y + height - MathF.Max(clear, foot.Y)) / height, 0f, 1f);
        }

        /// <summary>Whether fire <paramref name="fire"/> is inside the horizontal frustum of a lens at <paramref name="lens"/> looking at the axis.</summary>
        private static bool InFrame(Vector3 lens, Vector3 fire, float aspect)
        {
            Vector2 forward = Vector2.Normalize(new Vector2(-lens.X, -lens.Z));
            Vector2 toFire = new(fire.X - lens.X, fire.Z - lens.Z);
            float ahead = Vector2.Dot(toFire, forward);
            if (ahead <= 0f) return false;

            float across = MathF.Abs(toFire.X * forward.Y - toFire.Y * forward.X);
            return across / ahead < MathF.Tan(GAME_FOV * 0.5f) * aspect;
        }

        /// <summary>The share of every fire a lens at some bearing sees, over the bearings the gun can stand at (the camera orbits with it) and the two aspects.</summary>
        private static (float least, float mean, int samples) SharesOverTheOrbit(SavannaSceneConfig config)
        {
            float least = 1f, sum = 0f;
            int samples = 0, fires = SavannaBackdrop.CampfireRingCount(config);

            foreach (float outDistance in LENS_OUT)
                foreach (float aspect in ASPECTS)
                    for (int bearing = 0; bearing < 72; bearing++)
                    {
                        float angle = bearing * MathHelper.TwoPi / 72f;
                        Vector3 lens = new(MathF.Cos(angle) * outDistance, LENS_HEIGHT, MathF.Sin(angle) * outDistance);

                        for (int fire = 0; fire < fires; fire++)
                        {
                            Vector3 foot = SavannaBackdrop.CampfireRingPoint(config, fire, config.Campfire.HeightAboveTerrain);
                            if (!InFrame(lens, foot, aspect)) continue;

                            float share = VisibleShare(config, lens, fire);
                            least = MathF.Min(least, share);
                            sum += share;
                            samples++;
                        }
                    }

            return (least, sum / samples, samples);
        }

        [Fact]
        public void TheRimLeavesTheFlameMostOfItsHeightInView()
        {
            //The measurement that chose the fix: from the lens over the far arris, what share of the 13.8-tall billboard shows at every fire in
            //the frame, over the whole orbit. The fires stand on mounds at one seat, so the least is no longer the fire in the lowest relief.
            var config = new SavannaSceneConfig();
            (float least, float mean, int samples) = SharesOverTheOrbit(config);
            _output.WriteLine($"{samples} fire sightings: least {least:0.000}, mean {mean:0.000} of the flame's height in view");

            Assert.True(least >= 0.7f, $"the least a fire shows from the play camera is {least:0.000} of its height, under 0.7");
            Assert.True(mean >= 0.8f, $"fires show {mean:0.000} of their height on average, under 0.8");
        }

        [Fact]
        public void OnTheBareReliefTheRimHidesHalfOfTheLowestFlame()
        {
            //The control the test above is read against, seen to fail before the mounds: with the fires on the plain's own ground (3 to 7 units under
            //the plate) the least a fire shows is 0.52 of its height and the mean 0.67 - the owner's "only their tips"
            var config = new SavannaSceneConfig();
            config.Campfire.MoundFootScale = 0f;

            (float least, float mean, int samples) = SharesOverTheOrbit(config);
            _output.WriteLine($"{samples} fire sightings without the mounds: least {least:0.000}, mean {mean:0.000}");

            Assert.InRange(least, 0.45f, 0.60f);
            Assert.InRange(mean, 0.60f, 0.72f);
        }

        [Fact]
        public void MovingTheRingOutBuysAlmostNothing()
        {
            //The owner's own suggestion (2026-10-16, "a little farther from the island") and why it was not taken: the sight line over the arris falls by
            //0.02 per unit of distance, so ten units out lowers the hidden line by 0.2 and shrinks the fire on the screen. Measured on the bare relief.
            var config = new SavannaSceneConfig();
            config.Campfire.MoundFootScale = 0f;
            (float leastNear, float meanNear, _) = SharesOverTheOrbit(config);

            float scale = 43.3f / MathF.Sqrt(config.Campfire.GroundXZ.X * config.Campfire.GroundXZ.X + config.Campfire.GroundXZ.Y * config.Campfire.GroundXZ.Y);
            config.Campfire.GroundXZ = new Vec2(config.Campfire.GroundXZ.X * scale, config.Campfire.GroundXZ.Y * scale);
            (float leastFar, float meanFar, _) = SharesOverTheOrbit(config);
            _output.WriteLine($"ring at 33.3: least {leastNear:0.000} mean {meanNear:0.000}; ring at 43.3: least {leastFar:0.000} mean {meanFar:0.000}");

            Assert.True(meanFar - meanNear < 0.02f, $"ten units out moved the mean share by {meanFar - meanNear:0.000}");
        }

        [Fact]
        public void TheClearHeightIsTheSightLineOverTheFarArris()
        {
            //The control: a lens level with the plate's top (y = TOP_Y) sees nothing under the plate's own height whatever the distance, and
            //a lens 7 over it sees the line fall with distance. Both from the formula's own definition.
            Vector3 level = new(0f, ArenaIsland.TOP_Y, 30f);
            Assert.Equal(ArenaIsland.TOP_Y, ClearHeight(level, new Vector3(0f, -13f, -33f)), 3);
            Assert.Equal(ArenaIsland.TOP_Y, ClearHeight(level, new Vector3(0f, -13f, -43f)), 3);

            Vector3 high = new(0f, ArenaIsland.TOP_Y + 7f, 30f);
            float near = ClearHeight(high, new Vector3(0f, -13f, -33f));
            float far = ClearHeight(high, new Vector3(0f, -13f, -43f));
            Assert.True(near > far, "a lens above the plate sees lower down a fire that stands further out");
            Assert.True(near < ArenaIsland.TOP_Y);
        }
    }
}
