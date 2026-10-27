using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The land past the volcano's young field (#748), held on the shipped config: the river runs downhill and keeps clear of
    /// the field, its water lies level across its channel, the field's edge stands past the field's own furniture (the
    /// fumaroles and the blocks, which keep inside it), and the mirror the lamps, the intro lenses and the planting stand on
    /// is the cone alone on the field and only adds the land past it. <c>mirrorcheck</c> holds the mirror against the GPU;
    /// these hold the shapes the mirror and the shader agree on.
    /// </summary>
    public class VolcanoCountryTests
    {
        private static readonly VolcanoSceneConfig CONFIG = new();
        private static readonly VolcanoCountry COUNTRY = new(CONFIG);

        [Fact]
        public void TheRiverNeverRunsUphill()
        {
            for (int i = 1; i < COUNTRY.RiverPoints; i++)
                Assert.True(COUNTRY.RiverPath[i].W <= COUNTRY.RiverPath[i - 1].W,
                    $"the water rises from {COUNTRY.RiverPath[i - 1].W} to {COUNTRY.RiverPath[i].W} at point {i}");
        }

        [Fact]
        public void TheRiverKeepsClearOfTheField()
        {
            //Along the whole course, its meanders included: its valley must open on land, never on lava
            Vector4 last = COUNTRY.RiverPath[COUNTRY.RiverPoints - 1];
            for (float along = 0f; along <= last.Z; along += 5f)
            {
                Vector2 at = PointAlong(along, out Vector2 across);
                Vector2 centre = at + across * VolcanoCountry.MeanderAt(along);
                float outside = COUNTRY.Outside(centre.X, centre.Y);
                Assert.True(outside > 100f, $"the river's centre {along} along stands only {outside} past the field's edge");
            }
        }

        [Fact]
        public void TheWaterLiesLevelAcrossItsChannel()
        {
            //The channel is laid level, so the water drawn on it is: across the half-width the mirror's height stays one
            for (float along = 200f; along < 2200f; along += 150f)
            {
                Vector2 at = PointAlong(along, out Vector2 across);
                Vector2 centre = at + across * VolcanoCountry.MeanderAt(along);
                (float measuredAcross, float _, float level) = COUNTRY.River(centre.X, centre.Y);
                Assert.True(MathF.Abs(measuredAcross) < 1f, $"the course's centre at {along} measures {measuredAcross} across");

                float h0 = TerrainMirror.Volcano(centre.X, centre.Y, CONFIG, COUNTRY);
                Assert.Equal(CONFIG.LevelY + level, h0, 3);
                foreach (float off in new[] { -0.6f, 0.6f })
                {
                    Vector2 p = centre + across * (off * CONFIG.Country.RiverHalfWidth);
                    float h = TerrainMirror.Volcano(p.X, p.Y, CONFIG, COUNTRY);
                    Assert.True(MathF.Abs(h - h0) < 0.05f, $"the channel at {along} is not level: {h0} at its centre, {h} at {off} of its half-width");
                }
            }
        }

        [Fact]
        public void TheFieldReachesPastItsOwnFurniture()
        {
            //The fumaroles stand up to SteamConfig.FarthestVent from the arena and the field's blocks to 300: the field keeps
            //them (they also keep inside it themselves, VolcanoBackdrop.FIELD_MARGIN), and the clearing round the island is
            //the field's, so on every bearing the edge stands past the vents' reach
            float reach = CONFIG.Steam.FarthestVent;
            for (int b = 0; b < 72; b++)
            {
                float a = MathHelper.TwoPi * b / 72f;
                for (float r = 0f; r <= reach; r += 10f)
                {
                    float x = MathF.Sin(a) * r, z = MathF.Cos(a) * r;
                    Assert.True(COUNTRY.Outside(x, z) < 0f, $"the field ends {r} from the arena at bearing {b * 5} degrees");
                }
            }
        }

        [Fact]
        public void OnTheFieldTheGroundIsTheConeAlone()
        {
            //Inside the levee's run the mirror is the cone's own massing: the lamps and the intro's lenses over the field are
            //where they were before the land was added
            for (float x = -600f; x <= 600f; x += 37f)
                for (float z = -700f; z <= 300f; z += 41f)
                {
                    if (COUNTRY.Outside(x, z) > -VolcanoCountry.LEVEE_RUN - 1f) continue;
                    Assert.Equal(TerrainMirror.VolcanoCone(x, z, CONFIG), TerrainMirror.Volcano(x, z, CONFIG, COUNTRY), 4);
                }
        }

        //The point the course passes at a distance along it, and the unit vector across it towards positive Across
        private static Vector2 PointAlong(float along, out Vector2 across)
        {
            for (int i = 1; i < COUNTRY.RiverPoints; i++)
            {
                Vector4 a = COUNTRY.RiverPath[i - 1], b = COUNTRY.RiverPath[i];
                if (along > b.Z && i < COUNTRY.RiverPoints - 1) continue;
                float t = Math.Clamp((along - a.Z) / MathF.Max(b.Z - a.Z, 1e-3f), 0f, 1f);
                Vector2 dir = Vector2.Normalize(new Vector2(b.X - a.X, b.Y - a.Y));
                //River's side: e x w > 0 is positive, e x w = e.x w.y - e.y w.x, so +Across is (-e.y, e.x)
                across = new Vector2(-dir.Y, dir.X);
                return Vector2.Lerp(new Vector2(a.X, a.Y), new Vector2(b.X, b.Y), t);
            }
            throw new InvalidOperationException("no course");
        }
    }
}
