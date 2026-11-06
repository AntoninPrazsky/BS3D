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
    }
}
