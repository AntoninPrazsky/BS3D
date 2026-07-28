using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A procedurally generated detail texture for the scene's stone surfaces — concrete and cut stone —
    /// built once at load and <b>tiling exactly</b>, because its noise lattice wraps at the texture edge
    /// rather than being cut out of a photograph.
    /// <para>
    /// It exists because the photographic ground texture it replaces does not tile at all: the stone in
    /// <c>GameObjects/Ground_8.png</c> covers only the left ~1073 columns of a 2048² canvas and the rest is
    /// pure black, so the triplanar projection multiplied roughly half of every surface it was mapped onto
    /// by zero. That is what left the island's wall reading as a black band at <i>every</i> detail scale —
    /// no scale avoids the black, so the asset could not be rescued by tuning.
    /// </para>
    /// <para>
    /// The texture is a <b>modulation</b> of the albedo, not the albedo itself. <see cref="LinearMean"/> is
    /// the mean of its decoded radiance, so a caller that sets
    /// <c>DetailBoost = 1 / texture.LinearMean</c> gets a field that varies about 1 and neither brightens
    /// nor dims the material colour it multiplies — the material colour stays the honest albedo and
    /// <c>DetailStrength</c> becomes a pure contrast dial. Handed over unnormalised it darkens every
    /// surface it touches by however dark the generator happened to author it, which is the same mistake
    /// in a subtler form.
    /// </para>
    /// </summary>
    public sealed class SurfaceTexture : IDisposable
    {
        public Texture2D Texture { get; private set; }

        /// <summary>
        /// Mean linear radiance of the top mip. <c>DetailBoost = 1 / LinearMean</c> makes the texture a
        /// modulation about 1 (see the class remarks).
        /// </summary>
        public float LinearMean { get; }

        /// <summary>
        /// Cast concrete: broad formwork staining and damp patches, a sand grain over it, darker aggregate
        /// showing through where the skin is thin, and scattered pinholes left by air against the mould.
        /// </summary>
        public static SurfaceTexture Concrete(GraphicsDevice graphicsDevice, int size = 512, int seed = 5417) =>
            new(graphicsDevice, size, seed,
                mid: 0.80f,
                broadPeriod: 4, broadContrast: 0.14f,
                grainPeriod: 48, grainContrast: 0.075f,
                aggregateDepth: 0.09f, pitDepth: 0.24f,
                tint: new Vector3(0.985f, 1f, 1.02f));

        /// <summary>
        /// Cut stone for a dressed surface: a coarser, higher-contrast mottle with a fine grain and no
        /// pinholes — the same field as <see cref="Concrete"/> with the cast surface's blemishes left out.
        /// </summary>
        public static SurfaceTexture Stone(GraphicsDevice graphicsDevice, int size = 512, int seed = 91733) =>
            new(graphicsDevice, size, seed,
                mid: 0.84f,
                broadPeriod: 3, broadContrast: 0.17f,
                grainPeriod: 40, grainContrast: 0.055f,
                aggregateDepth: 0.05f, pitDepth: 0f,
                tint: new Vector3(1.02f, 1f, 0.972f));

        /// <param name="size">Edge of the square texture. A power of two, so the mip chain halves cleanly.</param>
        /// <param name="mid">Mean value in sRGB, before the tint.</param>
        /// <param name="broadPeriod">Lattice cells across the texture for the coarsest octave of the mottle.</param>
        /// <param name="grainPeriod">Lattice cells across the texture for the fine grain.</param>
        /// <param name="aggregateDepth">How far the darker aggregate blotches sink below the mean.</param>
        /// <param name="pitDepth">How dark a pinhole goes. 0 leaves the surface unpitted.</param>
        /// <param name="tint">Per-channel multiplier, near 1 — the material's own colour carries the hue.</param>
        private SurfaceTexture(GraphicsDevice graphicsDevice, int size, int seed, float mid,
            int broadPeriod, float broadContrast, int grainPeriod, float grainContrast,
            float aggregateDepth, float pitDepth, Vector3 tint)
        {
            //The whole chain is built from one linear-space field: a mip is an average of the light its
            //parent stands for, and averaging display-encoded values instead is the same error this
            //renderer converts the light rig and the sky palette on the CPU to avoid.
            var linear = new Vector3[size * size];
            double sum = 0.0;

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;

                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;

                    float broad = Fbm(u, v, broadPeriod, 5, 0.55f, seed);
                    float grain = Fbm(u, v, grainPeriod, 3, 0.5f, seed + 7717);
                    float aggregate = Fbm(u, v, 24, 2, 0.5f, seed + 15473);

                    float value = mid
                        + broadContrast * (broad - 0.5f)
                        + grainContrast * (grain - 0.5f)
                        - aggregateDepth * Smoothstep(0.58f, 0.78f, aggregate);

                    if (pitDepth > 0f)
                    {
                        float pits = Fbm(u, v, 96, 1, 0.5f, seed + 23011);
                        value -= pitDepth * Smoothstep(0.80f, 0.90f, pits);
                    }

                    Vector3 srgb = new Vector3(value) * tint;
                    Vector3 radiance = new(
                        ColorSpace.SrgbToLinear(MathHelper.Clamp(srgb.X, 0.02f, 1f)),
                        ColorSpace.SrgbToLinear(MathHelper.Clamp(srgb.Y, 0.02f, 1f)),
                        ColorSpace.SrgbToLinear(MathHelper.Clamp(srgb.Z, 0.02f, 1f)));

                    linear[y * size + x] = radiance;
                    sum += (radiance.X + radiance.Y + radiance.Z) / 3.0;
                }
            }

            LinearMean = (float)(sum / (size * (double)size));

            //Mips matter more here than on a photograph: the island is seen from the far side of the field
            //at a grazing angle, where an unmipped grain of this frequency crawls.
            int levels = 1;
            for (int edge = size; edge > 1; edge >>= 1) levels++;

            Texture = new Texture2D(graphicsDevice, size, size, true, SurfaceFormat.Color);

            int levelSize = size;
            Vector3[] levelData = linear;

            for (int level = 0; level < levels; level++)
            {
                Texture.SetData(level, null, Encode(levelData, levelSize), 0, levelSize * levelSize);

                if (levelSize == 1) break;

                levelData = Downsample(levelData, levelSize);
                levelSize >>= 1;
            }
        }

        private static Color[] Encode(Vector3[] radiance, int size)
        {
            var pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(
                    ColorSpace.LinearToSrgb(radiance[i].X),
                    ColorSpace.LinearToSrgb(radiance[i].Y),
                    ColorSpace.LinearToSrgb(radiance[i].Z));
            }

            return pixels;
        }

        private static Vector3[] Downsample(Vector3[] source, int size)
        {
            int half = size >> 1;
            var result = new Vector3[half * half];

            for (int y = 0; y < half; y++)
            {
                for (int x = 0; x < half; x++)
                {
                    int x0 = x * 2, y0 = y * 2;

                    result[y * half + x] = (source[y0 * size + x0] + source[y0 * size + x0 + 1]
                        + source[(y0 + 1) * size + x0] + source[(y0 + 1) * size + x0 + 1]) * 0.25f;
                }
            }

            return result;
        }

        /// <summary>
        /// Octaves of value noise on a lattice that wraps at its own period, so every octave — and
        /// therefore the sum — tiles exactly across the texture. <paramref name="basePeriod"/> is in
        /// lattice cells across the whole texture; each further octave doubles it.
        /// </summary>
        private static float Fbm(float u, float v, int basePeriod, int octaves, float gain, int seed)
        {
            float sum = 0f, amplitude = 1f, total = 0f;
            int period = basePeriod;

            for (int octave = 0; octave < octaves; octave++)
            {
                sum += amplitude * ValueNoise(u, v, period, seed + octave * 1013);
                total += amplitude;
                amplitude *= gain;
                period <<= 1;
            }

            return sum / total;
        }

        private static float ValueNoise(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);

            float fx = Fade(x - x0), fy = Fade(y - y0);

            int xa = Wrap(x0, period), xb = Wrap(x0 + 1, period);
            int ya = Wrap(y0, period), yb = Wrap(y0 + 1, period);

            float top = MathHelper.Lerp(Hash(xa, ya, seed), Hash(xb, ya, seed), fx);
            float bottom = MathHelper.Lerp(Hash(xa, yb, seed), Hash(xb, yb, seed), fx);

            return MathHelper.Lerp(top, bottom, fy);
        }

        private static int Wrap(int value, int period) => ((value % period) + period) % period;

        private static float Fade(float t) => t * t * (3f - 2f * t);

        private static float Smoothstep(float edge0, float edge1, float x) =>
            Fade(MathHelper.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f));

        //Integer avalanche rather than a sine-based hash: a sine hash is where two implementations of the
        //same field part company, and this one is generated once but has to stay the same texture across
        //machines and runs.
        private static float Hash(int x, int y, int seed)
        {
            uint h = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)(seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;

            return h * (1f / 4294967296f);
        }

        public void Dispose()
        {
            Texture?.Dispose();
            Texture = null;
        }
    }
}
