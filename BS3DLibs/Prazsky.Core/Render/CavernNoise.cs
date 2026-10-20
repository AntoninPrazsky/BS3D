using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The noise the cavern's chamber is shaped with on the CPU (#958): Ken Perlin's improved gradient noise in three
    /// dimensions (quintic fade, twelve edge gradients) and the fractal sums built on it. The chamber's geometry is built
    /// once on the CPU and never re-derived in a shader, so this is not a mirror of <c>Noise.fxh</c> and owes it no
    /// bit-for-bit agreement; what it owes is speed (a fine shell is a few hundred thousand vertices, five evaluations
    /// each) and the absence of a lattice: octaves are rotated between steps as <c>Fbm3</c> rotates them.
    /// </summary>
    internal static class CavernNoise
    {
        //Perlin's own permutation, doubled so a lookup never wraps
        private static readonly int[] P = BuildPermutation();

        private static int[] BuildPermutation()
        {
            int[] permutation =
            {
                151, 160, 137, 91, 90, 15, 131, 13, 201, 95, 96, 53, 194, 233, 7, 225, 140, 36, 103, 30, 69, 142, 8, 99, 37, 240,
                21, 10, 23, 190, 6, 148, 247, 120, 234, 75, 0, 26, 197, 62, 94, 252, 219, 203, 117, 35, 11, 32, 57, 177, 33, 88,
                237, 149, 56, 87, 174, 20, 125, 136, 171, 168, 68, 175, 74, 165, 71, 134, 139, 48, 27, 166, 77, 146, 158, 231, 83,
                111, 229, 122, 60, 211, 133, 230, 220, 105, 92, 41, 55, 46, 245, 40, 244, 102, 143, 54, 65, 25, 63, 161, 1, 216, 80,
                73, 209, 76, 132, 187, 208, 89, 18, 169, 200, 196, 135, 130, 116, 188, 159, 86, 164, 100, 109, 198, 173, 186, 3, 64,
                52, 217, 226, 250, 124, 123, 5, 202, 38, 147, 118, 126, 255, 82, 85, 212, 207, 206, 59, 227, 47, 16, 58, 17, 182,
                189, 28, 42, 223, 183, 170, 213, 119, 248, 152, 2, 44, 154, 163, 70, 221, 153, 101, 155, 167, 43, 172, 9, 129, 22,
                39, 253, 19, 98, 108, 110, 79, 113, 224, 232, 178, 185, 112, 104, 218, 246, 97, 228, 251, 34, 242, 193, 238, 210,
                144, 12, 191, 179, 162, 241, 81, 51, 145, 235, 249, 14, 239, 107, 49, 192, 214, 31, 181, 199, 106, 157, 184, 84,
                204, 176, 115, 121, 50, 45, 127, 4, 150, 254, 138, 236, 205, 93, 222, 114, 67, 29, 24, 72, 243, 141, 128, 195, 78,
                66, 215, 61, 156, 180,
            };

            int[] doubled = new int[512];
            for (int i = 0; i < 512; i++) doubled[i] = permutation[i & 255];
            return doubled;
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float Grad(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        /// <summary>Improved gradient noise at <paramref name="p"/>, roughly -1..1, zero at every lattice point.</summary>
        public static float Gradient(Vector3 p)
        {
            float fx = MathF.Floor(p.X), fy = MathF.Floor(p.Y), fz = MathF.Floor(p.Z);
            int X = (int)fx & 255, Y = (int)fy & 255, Z = (int)fz & 255;
            float x = p.X - fx, y = p.Y - fy, z = p.Z - fz;
            float u = Fade(x), v = Fade(y), w = Fade(z);

            int A = P[X] + Y, AA = P[A] + Z, AB = P[A + 1] + Z;
            int B = P[X + 1] + Y, BA = P[B] + Z, BB = P[B + 1] + Z;

            float x1 = Lerp(Grad(P[AA], x, y, z), Grad(P[BA], x - 1f, y, z), u);
            float x2 = Lerp(Grad(P[AB], x, y - 1f, z), Grad(P[BB], x - 1f, y - 1f, z), u);
            float y1 = Lerp(x1, x2, v);
            float x3 = Lerp(Grad(P[AA + 1], x, y, z - 1f), Grad(P[BA + 1], x - 1f, y, z - 1f), u);
            float x4 = Lerp(Grad(P[AB + 1], x, y - 1f, z - 1f), Grad(P[BB + 1], x - 1f, y - 1f, z - 1f), u);
            float y2 = Lerp(x3, x4, v);
            return Lerp(y1, y2, w);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        //Rotation between octaves (the shader's NOISE_ROTATE3 idea): without it the octaves' lattices align and their
        //common axes read as a grid through the finished field
        private static Vector3 Rotate(Vector3 p) => new(
            0.00f * p.X + 0.80f * p.Y + 0.60f * p.Z,
            -0.80f * p.X + 0.36f * p.Y - 0.48f * p.Z,
            -0.60f * p.X - 0.48f * p.Y + 0.64f * p.Z);

        /// <summary>Fractal sum of <paramref name="octaves"/> octaves, each at 2.03 times the frequency and half the
        /// amplitude of the last, the domain rotated between them. Roughly -0.7..0.7 for several octaves.</summary>
        public static float Fbm(Vector3 p, int octaves)
        {
            float value = 0f, amplitude = 0.5f;
            for (int i = 0; i < octaves; i++)
            {
                value += amplitude * Gradient(p);
                p = Rotate(p) * 2.03f;
                amplitude *= 0.5f;
            }
            return value;
        }

        /// <summary>Ridged fractal: 1 - |noise| per octave, squared, each weighted by the last (Musgrave). Roughly 0..1;
        /// the field for crests, ribs and fracture lines.</summary>
        public static float Ridged(Vector3 p, int octaves)
        {
            float value = 0f, amplitude = 0.5f, weight = 1f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - MathF.Abs(Gradient(p));
                n *= n;
                n *= weight;
                weight = Math.Clamp(n * 2f, 0f, 1f);
                value += n * amplitude;
                p = Rotate(p) * 2.03f;
                amplitude *= 0.5f;
            }
            return value;
        }

        /// <summary>A hash of an integer triple to 0..1, for rolls that must not depend on the order things are placed in.</summary>
        public static float Hash(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)a * 0x8da6b343u ^ (uint)b * 0xd8163841u ^ (uint)c * 0xcb1ab31fu;
                h ^= h >> 16;
                h *= 0x7feb352du;
                h ^= h >> 15;
                h *= 0x846ca68bu;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>The distance to the nearest of a jittered cell lattice's sites, in cells (Worley's F1): 0 at a site,
        /// up to about 0.9 between them. Pockets in the vault are domes round its minima.</summary>
        public static float Cellular(Vector3 p)
        {
            int cx = (int)MathF.Floor(p.X), cy = (int)MathF.Floor(p.Y), cz = (int)MathF.Floor(p.Z);
            float best = 9f;
            for (int z = -1; z <= 1; z++)
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                    {
                        int ix = cx + x, iy = cy + y, iz = cz + z;
                        Vector3 site = new(ix + 0.15f + 0.7f * Hash(ix, iy, iz), iy + 0.15f + 0.7f * Hash(iy, iz, ix + 77), iz + 0.15f + 0.7f * Hash(iz, ix, iy + 191));
                        float d = Vector3.DistanceSquared(site, p);
                        if (d < best) best = d;
                    }
            return MathF.Sqrt(best);
        }
    }
}
