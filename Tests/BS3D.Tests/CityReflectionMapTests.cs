using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The map the city's windows reflect from (#1065, <c>CityReflectionMap</c>), on both cities: what the shader's march
    /// reads of each texel is what stands there. A texel a tower covers holds at least that tower's roof (another tower
    /// box of the plot may stand higher over it) and a tower's own id and kind; a texel no tower covers holds the street
    /// level; the top is the highest roof.
    /// </summary>
    public class CityReflectionMapTests
    {
        [Fact]
        public void Every_texel_holds_what_stands_over_it()
        {
            foreach (bool neon in new[] { false, true })
            {
                var city = new City(new CitySceneConfig(), neon, ArenaIsland.RADIUS);
                Vector4[] texels = CityReflectionMap.Rasterise(city, out int width, out int height, out Vector2 origin, out float top);
                var covered = new bool[texels.Length];
                float highest = city.GroundY;

                for (int b = 0; b < city.TowerCount; b++)
                {
                    Matrix world = city.Buildings[b].World;
                    Vector4 style = city.Buildings[b].Custom;
                    float roof = world.M42 + world.M22 * 0.5f + city.RoofRise(b);
                    highest = MathF.Max(highest, roof);

                    //The texel under the box's middle, which every box covers (the narrowest is over two texels across)
                    int x = (int)MathF.Floor((world.M41 - origin.X) / CityReflectionMap.TEXEL);
                    int z = (int)MathF.Floor((world.M43 - origin.Y) / CityReflectionMap.TEXEL);
                    Vector4 texel = texels[z * width + x];
                    Assert.True(texel.X >= roof - 1e-3f, $"neon {neon}, box {b}: its middle's texel holds {texel.X}, under its roof {roof}");

                    //All of a plot's boxes carry one anchor and one kind, so whichever box won the texel, they are this one's;
                    //and a tower texel is no distance from a tower
                    float id = (MathF.Floor(style.Z * 0.37f) + CityReflectionMap.ID_BIAS) * CityReflectionMap.ID_SPAN
                        + MathF.Floor(style.W * 0.37f) + CityReflectionMap.ID_BIAS;
                    Assert.Equal(id, texel.Y);
                    Assert.Equal(style.X, texel.Z);
                    Assert.Equal(0f, texel.W);

                    for (int tz = 0; tz < height; tz++)
                    {
                        float cz = origin.Y + (tz + 0.5f) * CityReflectionMap.TEXEL;
                        if (MathF.Abs(cz - world.M43) > world.M33 * 0.5f) continue;
                        for (int tx = 0; tx < width; tx++)
                        {
                            float cx = origin.X + (tx + 0.5f) * CityReflectionMap.TEXEL;
                            if (MathF.Abs(cx - world.M41) <= world.M11 * 0.5f) covered[tz * width + tx] = true;
                        }
                    }
                }

                for (int i = 0; i < texels.Length; i++)
                    if (!covered[i]) Assert.Equal(city.GroundY, texels[i].X);
                Assert.Equal(highest, top);

                //The distance the march jumps by is the true Chebyshev distance to the nearest tower texel (capped), checked
                //by brute force on a spread of texels: a figure too big would let a jump land inside a tower
                var random = new Random(1065);
                for (int sample = 0; sample < 400; sample++)
                {
                    int x = random.Next(width), z = random.Next(height);
                    int nearest = CityReflectionMap.DISTANCE_CAP;
                    for (int dz = -CityReflectionMap.DISTANCE_CAP; dz <= CityReflectionMap.DISTANCE_CAP; dz++)
                        for (int dx = -CityReflectionMap.DISTANCE_CAP; dx <= CityReflectionMap.DISTANCE_CAP; dx++)
                        {
                            int ox = x + dx, oz = z + dz;
                            if (ox < 0 || oz < 0 || ox >= width || oz >= height || texels[oz * width + ox].X <= city.GroundY) continue;
                            nearest = Math.Min(nearest, Math.Max(Math.Abs(dx), Math.Abs(dz)));
                        }
                    Assert.Equal(nearest, (int)texels[z * width + x].W);
                }
            }
        }

        /// <summary>
        /// The march on the CPU (<c>CityReflectionField</c>, #1119), which the city's chapter intro frames its facades by: from
        /// panes on the faces of the towers, mirror rays rolled across the street and up, marched with the loop's whole bound,
        /// meet a tower where a ray through the towers' boxes does (but for the rasterising's half texel at a box's edge and
        /// the slivers of sky the march cannot see between two texels); and the step budget the Game draws with is what it
        /// is: some grazing rays that do reach a tower in the whole bound run out of the Game's sixteen steps first, which is
        /// why the intro asks the march and not the boxes.
        /// </summary>
        [Fact]
        public void The_cpu_march_meets_the_towers_the_boxes_put_there_in_the_steps_it_is_given()
        {
            foreach (bool neon in new[] { false, true })
            {
                var city = new City(new CitySceneConfig(), neon, ArenaIsland.RADIUS);
                var field = new CityReflectionField(city);
                var random = new Random(1119);
                int rays = 0, agree = 0, hits = 0, outOfSteps = 0;

                for (int sample = 0; sample < 4000; sample++)
                {
                    //A pane on a face of a tower that stands on the street level, a few units over it
                    int b = random.Next(city.TowerCount);
                    Matrix world = city.Buildings[b].World;
                    Vector3 centre = new(world.M41, world.M42, world.M43);
                    Vector3 half = new Vector3(world.M11, world.M22, world.M33) * 0.5f;
                    if (centre.Y - half.Y > city.GroundY + 0.5f) continue;

                    int face = random.Next(4);
                    Vector3 normal = face switch { 0 => Vector3.UnitX, 1 => -Vector3.UnitX, 2 => Vector3.UnitZ, _ => -Vector3.UnitZ };
                    Vector3 tangent = new(MathF.Abs(normal.Z), 0f, MathF.Abs(normal.X));
                    float spread = MathF.Abs(normal.X) > 0.5f ? half.Z : half.X;
                    float height = city.GroundY + 2f + (float)random.NextDouble() * MathF.Min(30f, 2f * half.Y - 4f);
                    Vector3 pane = centre + normal * Vector3.Dot(half, new Vector3(MathF.Abs(normal.X), 0f, MathF.Abs(normal.Z)))
                        + tangent * ((float)random.NextDouble() * 2f - 1f) * (spread - 1f);
                    pane.Y = height;

                    //A mirror ray out of the face: across it by 0.2 to 1, along it either way, and up
                    float across = 0.2f + 0.8f * (float)random.NextDouble();
                    Vector3 direction = Vector3.Normalize(normal * across + tangent * ((float)random.NextDouble() * 2f - 1f)
                        + Vector3.UnitY * (0.1f + 0.5f * (float)random.NextDouble()));

                    bool box = TowerHit(city, pane + normal * 1.5f, direction, b);
                    bool whole = field.March(pane, normal, direction, CityReflectionField.MAX_STEPS, out _);
                    bool game = field.March(pane, normal, direction, InstancedModelRenderer.DEFAULT_CITY_REFLECTION_STEPS, out _);

                    rays++;
                    if (box == whole) agree++;
                    if (whole) hits++;
                    if (whole && !game) outOfSteps++;
                }

                Assert.True(rays >= 1000, $"neon {neon}: only {rays} rays");
                Assert.True(hits >= rays / 4, $"neon {neon}: only {hits} of {rays} rays met a tower");
                Assert.True(agree >= 0.95f * rays, $"neon {neon}: the march and the boxes agree on {agree} of {rays} rays");
                Assert.True(outOfSteps > 0, $"neon {neon}: no ray ran out of the Game's steps; the budget is not what is being asked");
            }
        }

        //Whether a ray from `origin` meets a tower's box other than `except` before it climbs over the highest roof or leaves the
        //city: the boxes the map is rasterised from, each up to its roof's cornice
        private static bool TowerHit(City city, Vector3 origin, Vector3 direction, int except)
        {
            for (int b = 0; b < city.TowerCount; b++)
            {
                if (b == except) continue;

                Matrix world = city.Buildings[b].World;
                float roof = world.M42 + world.M22 * 0.5f + city.RoofRise(b);
                Vector3 low = new(world.M41 - world.M11 * 0.5f, city.GroundY, world.M43 - world.M33 * 0.5f);
                Vector3 high = new(world.M41 + world.M11 * 0.5f, roof, world.M43 + world.M33 * 0.5f);

                float enter = 0f, exit = 1000f;
                bool through = true;
                for (int a = 0; a < 3 && through; a++)
                {
                    float o = a == 0 ? origin.X : a == 1 ? origin.Y : origin.Z;
                    float d = a == 0 ? direction.X : a == 1 ? direction.Y : direction.Z;
                    float l = a == 0 ? low.X : a == 1 ? low.Y : low.Z, h = a == 0 ? high.X : a == 1 ? high.Y : high.Z;
                    if (MathF.Abs(d) < 1e-6f) { through = o >= l && o <= h; continue; }
                    float t0 = (l - o) / d, t1 = (h - o) / d;
                    if (t0 > t1) (t0, t1) = (t1, t0);
                    enter = MathF.Max(enter, t0);
                    exit = MathF.Min(exit, t1);
                    through = enter <= exit;
                }

                if (through) return true;
            }

            return false;
        }
    }
}
