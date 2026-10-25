using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// What a ray-marched volume is drawn through (#886, #989): the savanna's fires and the clouds on High and Ultra are fields
    /// in the world, and every pixel of a box round one marches its view ray through it. One copy of the box and of the noise
    /// the fields are built from.
    /// </summary>
    internal static class ProxyVolumes
    {
        /// <summary>
        /// The unit box a volume is drawn through: X and Z -1 to 1, Y 0 to 1, each face's two triangles wound clockwise seen
        /// from outside (the repository's rule: <c>(b - a) x (c - a)</c> points into the box), so MonoGame's default cull keeps
        /// the faces nearer the lens and a pixel is shaded once.
        /// </summary>
        internal static (SceneRenderer.BillboardVertex[] Vertices, short[] Indices) UnitBox()
        {
            var vertices = new SceneRenderer.BillboardVertex[8];
            for (int i = 0; i < 8; i++)
                vertices[i] = new(new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? 0f : 1f, (i & 4) == 0 ? -1f : 1f), Vector3.Zero);

            //Each face as four corners round it; the winding is chosen against the face's outward normal
            int[][] faces =
            {
                new[] { 0, 2, 6, 4 }, new[] { 1, 3, 7, 5 },   //X -1, X +1
                new[] { 0, 1, 5, 4 }, new[] { 2, 3, 7, 6 },   //Y 0, Y 1
                new[] { 0, 1, 3, 2 }, new[] { 4, 5, 7, 6 },   //Z -1, Z +1
            };
            var indices = new List<short>(36);
            foreach (int[] f in faces)
            {
                Vector3 a = vertices[f[0]].Position, b = vertices[f[1]].Position, c = vertices[f[2]].Position;
                Vector3 outward = (a + c) * 0.5f - new Vector3(0f, 0.5f, 0f);
                bool inward = Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f;
                int[] order = inward ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 };
                foreach (int k in order) indices.Add((short)f[k]);
            }
            return (vertices, indices.ToArray());
        }

        /// <summary>
        /// Perlin's gradient noise with its lattice wrapped every <paramref name="period"/> units, sampled
        /// <paramref name="perUnit"/> times a unit along each axis, X fastest: a cube of texels a wrap sampler tiles without a
        /// seam. The gradients are a fixed roll off <paramref name="seed"/>, so every run bakes the same field.
        /// </summary>
        internal static float[] GradientNoiseTexels(int period, int perUnit, int seed)
        {
            int side = period * perUnit;
            var rng = new Random(seed);
            var gradients = new Vector3[period * period * period];
            for (int g = 0; g < gradients.Length; g++)
            {
                Vector3 d;
                do d = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f);
                while (d.LengthSquared() < 0.05f || d.LengthSquared() > 1f);
                gradients[g] = Vector3.Normalize(d);
            }
            static float Fade(float f) => f * f * f * (f * (f * 6f - 15f) + 10f);

            var texels = new float[side * side * side];
            for (int z = 0; z < side; z++)
                for (int y = 0; y < side; y++)
                    for (int x = 0; x < side; x++)
                    {
                        Vector3 p = new Vector3(x, y, z) / perUnit;
                        int ix = (int)MathF.Floor(p.X), iy = (int)MathF.Floor(p.Y), iz = (int)MathF.Floor(p.Z);
                        Vector3 f = p - new Vector3(ix, iy, iz);
                        float value = 0f;
                        for (int corner = 0; corner < 8; corner++)
                        {
                            int cx = corner & 1, cy = (corner >> 1) & 1, cz = (corner >> 2) & 1;
                            Vector3 g = gradients[(ix + cx) % period + period * ((iy + cy) % period) + period * period * ((iz + cz) % period)];
                            float weight = (cx == 1 ? Fade(f.X) : 1f - Fade(f.X)) * (cy == 1 ? Fade(f.Y) : 1f - Fade(f.Y))
                                * (cz == 1 ? Fade(f.Z) : 1f - Fade(f.Z));
                            value += weight * Vector3.Dot(g, f - new Vector3(cx, cy, cz));
                        }
                        texels[x + side * (y + side * z)] = value;
                    }
            return texels;
        }
    }
}
