using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The city's towers seen from above, for the windows' reflections (#1065): a texel a <see cref="TEXEL"/> square
    /// holding the highest roof over it (the street level where no tower stands), that tower's building id, its facade
    /// kind, and how far the nearest tower is. <c>InstancedModel.fx</c>'s <c>InstancedCityReflect</c> marches a pane's
    /// mirror ray through it and shades the face it meets, so a window shows the tower across the street and not only
    /// the sky.
    /// <para>
    /// Built once per <see cref="City"/> (a scene change between the two cities builds the other), from the towers'
    /// boxes alone: a cornice stands half a unit past its tier, under a texel's worth, and the far skyline stands off
    /// the map. The id is <c>floor(anchor * 0.37)</c>, as <c>CityPS</c> takes it from <c>Style.zw</c>, so a reflected
    /// tower wears its own tone and lamps; both of its parts packed in one float (<see cref="ID_BIAS"/>).
    /// </para>
    /// <para>
    /// ⚠ <b>The distance is what makes a grazing pane affordable.</b> A pane seen along a street, where glass mirrors
    /// most, sends its ray nearly along the street, and a texel a step it could not cross the street in the steps it had:
    /// it marched them all and kept the sky. Through the empty texels the march jumps as far as the nearest tower allows
    /// and steps a texel at a time only beside one, so a street is crossed in a step or two.
    /// </para>
    /// </summary>
    public sealed class CityReflectionMap : IDisposable
    {
        /// <summary>World units a texel. A street is nine across, and a box's edge lands within half a texel of where it is.</summary>
        public const float TEXEL = 1f;

        /// <summary>
        /// The map, <see cref="SurfaceFormat.Vector4"/>: (roof height, packed id, facade kind, Chebyshev distance in texels
        /// to the nearest tower texel, 0 on one). Full floats, because the packed id is a whole number past a half's.
        /// </summary>
        public Texture2D Texture { get; }

        /// <summary>World xz of the map's outer corner, texel (0, 0)'s.</summary>
        public Vector2 Origin { get; }

        /// <summary>Texels across x and z.</summary>
        public Vector2 Size { get; }

        /// <summary>The highest roof in the city: a ray climbing past it meets nothing more.</summary>
        public float Top { get; }

        /// <summary>The street level, which every texel no tower stands on holds.</summary>
        public float Ground { get; }

        /// <summary>
        /// The id's packing: <c>(x + ID_BIAS) * ID_SPAN + (z + ID_BIAS)</c>, a whole number a float holds exactly for any
        /// id the grid makes (a few hundred either way). <c>CityReflection.fxh</c> unpacks it with the same two figures.
        /// </summary>
        public const float ID_BIAS = 512f, ID_SPAN = 1024f;

        /// <summary>The farthest the distance field counts, in texels; farther is stored as this.</summary>
        public const int DISTANCE_CAP = 64;

        public CityReflectionMap(GraphicsDevice graphicsDevice, City city)
        {
            Vector4[] texels = Rasterise(city, out int width, out int height, out Vector2 origin, out float top);

            Texture = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Vector4);
            Texture.SetData(texels);
            Origin = origin;
            Size = new Vector2(width, height);
            Top = top;
            Ground = city.GroundY;
        }

        /// <summary>
        /// The map as plain floats, row by row in z then x from <paramref name="origin"/>: every tower box's footprint
        /// takes the texels whose centres it covers, the highest roof (its cornice's slab included) winning a texel two
        /// boxes share. No device, for the tests.
        /// </summary>
        internal static Vector4[] Rasterise(City city, out int width, out int height, out Vector2 origin, out float top)
        {
            float half = (city.RadiusBlocks + 0.5f) * city.BlockPitch;
            origin = new Vector2(-half, -half);
            width = height = (int)MathF.Ceiling(2f * half / TEXEL);

            var texels = new Vector4[width * height];
            Array.Fill(texels, new Vector4(city.GroundY, 0f, 0f, 0f));
            top = city.GroundY;

            for (int b = 0; b < city.TowerCount; b++)
            {
                Matrix world = city.Buildings[b].World;
                Vector4 style = city.Buildings[b].Custom;
                float roof = world.M42 + world.M22 * 0.5f + city.RoofRise(b);
                top = MathF.Max(top, roof);

                float minX = world.M41 - world.M11 * 0.5f, maxX = world.M41 + world.M11 * 0.5f;
                float minZ = world.M43 - world.M33 * 0.5f, maxZ = world.M43 + world.M33 * 0.5f;
                int x0 = Math.Max(0, (int)MathF.Ceiling((minX - origin.X) / TEXEL - 0.5f));
                int x1 = Math.Min(width - 1, (int)MathF.Floor((maxX - origin.X) / TEXEL - 0.5f));
                int z0 = Math.Max(0, (int)MathF.Ceiling((minZ - origin.Y) / TEXEL - 0.5f));
                int z1 = Math.Min(height - 1, (int)MathF.Floor((maxZ - origin.Y) / TEXEL - 0.5f));

                float id = (MathF.Floor(style.Z * 0.37f) + ID_BIAS) * ID_SPAN + MathF.Floor(style.W * 0.37f) + ID_BIAS;
                var texel = new Vector4(roof, id, style.X, 0f);
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int i = z * width + x;
                        if (roof > texels[i].X) texels[i] = texel;
                    }
            }

            DistanceField(texels, width, height, city.GroundY);
            return texels;
        }

        //The Chebyshev distance from every texel to the nearest tower texel, into .W: two chamfer passes, forward and
        //back, each taking the smallest of its already-visited neighbours plus one (diagonals included, so the metric is
        //the square one the march's jump assumes)
        private static void DistanceField(Vector4[] texels, int width, int height, float ground)
        {
            var distance = new int[texels.Length];
            for (int i = 0; i < texels.Length; i++) distance[i] = texels[i].X > ground ? 0 : DISTANCE_CAP;

            for (int z = 0; z < height; z++)
                for (int x = 0; x < width; x++)
                {
                    int i = z * width + x, d = distance[i];
                    if (x > 0) d = Math.Min(d, distance[i - 1] + 1);
                    if (z > 0)
                    {
                        d = Math.Min(d, distance[i - width] + 1);
                        if (x > 0) d = Math.Min(d, distance[i - width - 1] + 1);
                        if (x < width - 1) d = Math.Min(d, distance[i - width + 1] + 1);
                    }
                    distance[i] = d;
                }

            for (int z = height - 1; z >= 0; z--)
                for (int x = width - 1; x >= 0; x--)
                {
                    int i = z * width + x, d = distance[i];
                    if (x < width - 1) d = Math.Min(d, distance[i + 1] + 1);
                    if (z < height - 1)
                    {
                        d = Math.Min(d, distance[i + width] + 1);
                        if (x < width - 1) d = Math.Min(d, distance[i + width + 1] + 1);
                        if (x > 0) d = Math.Min(d, distance[i + width - 1] + 1);
                    }
                    distance[i] = d;
                }

            for (int i = 0; i < texels.Length; i++) texels[i].W = distance[i];
        }

        public void Dispose() => Texture.Dispose();
    }
}
