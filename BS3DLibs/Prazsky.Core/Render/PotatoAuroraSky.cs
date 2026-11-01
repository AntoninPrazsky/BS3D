using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The aurora's sky on the Potato path (#1044): the desktop's night of #963 (<c>Aurora.fx</c>'s <c>AuroraSkyPS</c>) in
    /// place of #829's band, which was the look the owner rejected on the desktop. The desktop intersects every view ray
    /// with three curtains per pixel, a dozen noises an arc; the Pi paid 12 ms at 1080p for far less (#829's skies). Here
    /// the same arcs, on the same config, are worked out ONCE on the CPU (<see cref="Bake"/>, a port of <c>AuroraArc</c>
    /// term for term) into a texture over the upper hemisphere, which <c>PotatoSky.fx</c>'s <c>PotatoSkyAurora</c> reads
    /// once a pixel. What moves on the desktop moves here by cheaper means: each arc breathes on its own clock
    /// (<see cref="ArcBreaths"/>, the desktop's <c>ArcBrightness</c>; the texture's alpha says which arc a texel is), and
    /// the rays shimmer by a sliding beat across the sky in place of running along each curtain. The folds do not re-form.
    /// The gradient, the glow along the horizon and the mist at it are the vertex's, as the dome's gradient is; the Moon is
    /// the pixel's.
    /// </summary>
    public sealed class PotatoAuroraSky : IDisposable
    {
        /// <summary>
        /// The texture: columns round the sky from +X towards +Z (<see cref="PotatoSkyFigure.BuildSphere"/>'s longitude),
        /// rows from the horizon up by the square root of the elevation's sine, so the low sky where the curtains' edges
        /// hang has the most of them. At the play view's lens a texel is two to four pixels; rays finer than a texel are
        /// faded as the desktop fades those finer than a pixel.
        /// </summary>
        public const int WIDTH = 4096, HEIGHT = 512;

        /// <summary>The radiance a texel's full white stands for: the brightest folds, the slant and the rays together, are past it.</summary>
        public const float SCALE = 4f;

        /// <summary>How deep the rays' shimmer cuts the curtains.</summary>
        public const float SHIMMER = 0.3f;

        private readonly AuroraSceneConfig _config;
        private Texture2D _texture;
        private readonly EffectParameter _textureParameter, _scale, _breaths, _moonDisc, _moonHalo;

        private PotatoAuroraSky(GraphicsDevice device, Effect effect, AuroraSceneConfig config)
        {
            _config = config;
            _texture = new Texture2D(device, WIDTH, HEIGHT, false, SurfaceFormat.Color);
            _texture.SetData(Bake(config, WIDTH, HEIGHT));

            EffectParameterCollection parameters = effect.Parameters;
            _textureParameter = parameters["AuroraTexture"];
            _scale = parameters["AuroraScale"];
            _breaths = parameters["ArcBreath"];
            _moonDisc = parameters["MoonDisc"];
            _moonHalo = parameters["MoonHalo"];
        }

        /// <summary>The aurora's sky through <paramref name="effect"/> (<c>PotatoSky</c>), baked on the config's defaults; null when the effect has no texture for it yet.</summary>
        public static PotatoAuroraSky Create(GraphicsDevice device, Effect effect)
        {
            if (effect.Parameters["AuroraTexture"] == null) return null;
            return new PotatoAuroraSky(device, effect, new AuroraSceneConfig());
        }

        /// <summary>Its uniforms at <paramref name="time"/>: the texture, the arcs' breaths, the Moon's light.</summary>
        public void Push(float time)
        {
            _textureParameter?.SetValue(_texture);
            _scale?.SetValue(SCALE);
            _breaths?.SetValue(ArcBreaths(_config, time));
            _moonDisc?.SetValue(_config.Sky.MoonDisc.ToVector3());
            _moonHalo?.SetValue(_config.Sky.MoonHalo.ToVector3());
        }

        /// <summary>
        /// Each arc's brightness at <paramref name="time"/> against the one it was baked at (its full,
        /// <see cref="AuroraCurtainsConfig.Intensity"/> times its own): the desktop's <c>ArcBrightness</c> without those two.
        /// </summary>
        public static Vector3 ArcBreaths(AuroraSceneConfig config, float time)
        {
            AuroraCurtainsConfig curtains = config.Curtains;
            Span<float> breath = stackalloc float[3] { 1f, 1f, 1f };
            for (int k = 0; k < Math.Min(curtains.Arcs.Length, 3); k++)
            {
                float phase = time * curtains.PulseSpeed + curtains.Arcs[k].PulsePhase;
                float b = 0.5f + 0.5f * MathF.Sin(phase) * (0.75f + 0.25f * MathF.Sin(phase * 0.37f + 1.3f));
                breath[k] = 1f - curtains.PulseDepth * b;
            }
            return new Vector3(breath[0], breath[1], breath[2]);
        }

        /// <summary>
        /// The texels, no device: for the tests. Each holds the arcs' light over that direction at their full brightness,
        /// as the square root of its share of <see cref="SCALE"/> (the dark sky between the curtains keeps its steps), and in
        /// alpha which arc it is - 0 the first, a half the second, 1 the third, between where two overlap.
        /// </summary>
        public static Color[] Bake(AuroraSceneConfig config, int width, int height)
        {
            AuroraCurtainsConfig curtains = config.Curtains;
            int count = Math.Min(curtains.Arcs.Length, 3);
            var arcs = new Arc[count];
            float bearing = MathHelper.ToRadians(curtains.BearingDegrees);
            for (int k = 0; k < count; k++)
            {
                AuroraArcConfig arc = curtains.Arcs[k];
                //AuroraBackdrop's places: a ray's place across an arc is dir.z cos(a) - dir.x sin(a), along its bearing when a = -turn
                float turn = bearing + MathHelper.ToRadians(arc.AngleDegrees);
                arcs[k] = new Arc(arc.Offset, -turn, arc.FoldDepth, curtains.Intensity * arc.Brightness,
                    arc.LowerEdge, arc.Height, arc.FoldFrequency, arc.Seed, arc.BendDepth, arc.BendFrequency, arc.LitFrequency);
            }

            var texels = new Color[width * height];
            var columns = new Vector2[width];
            for (int column = 0; column < width; column++)
            {
                float longitude = MathHelper.TwoPi * (column + 0.5f) / width;
                columns[column] = new Vector2(MathF.Cos(longitude), MathF.Sin(longitude));
            }
            Parallel.For(0, height, row => BakeRow(texels, row, width, height, columns, arcs, curtains));

            return texels;
        }

        //One row of the texture. The bake runs once, as the scene is built, so its methods are compiled optimised at once:
        //at the runtime's first tier the first bake took twice as long as a warm one
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static void BakeRow(Color[] texels, int row, int width, int height, Vector2[] columns, Arc[] arcs, AuroraCurtainsConfig curtains)
        {
            float s = (row + 0.5f) / height;
            float sinElevation = s * s;
            float cosElevation = MathF.Sqrt(MathF.Max(1f - sinElevation * sinElevation, 0f));
            float texelAngle = MathHelper.TwoPi / width;
            int count = arcs.Length;
            for (int column = 0; column < width; column++)
            {
                var dir = new Vector3(cosElevation * columns[column].X, sinElevation, cosElevation * columns[column].Y);

                Vector3 light = Vector3.Zero;
                float weight = 0f, which = 0f;
                for (int k = 0; k < count; k++)
                {
                    Vector3 c = ArcLight(dir, arcs[k], curtains, texelAngle);
                    light += c;
                    float luma = ColorSpace.Luminance(c);
                    weight += luma;
                    which += luma * (count > 1 ? k / (count - 1f) : 0f);
                }

                Vector3 encoded = new(MathF.Sqrt(MathHelper.Clamp(light.X / SCALE, 0f, 1f)),
                    MathF.Sqrt(MathHelper.Clamp(light.Y / SCALE, 0f, 1f)), MathF.Sqrt(MathHelper.Clamp(light.Z / SCALE, 0f, 1f)));
                texels[row * width + column] = new Color(encoded.X, encoded.Y, encoded.Z, weight > 1e-6f ? which / weight : 0f);
            }
        }

        //One arc as Aurora.fx's uniforms hold it: ArcPlace (offset, axis angle, folds' depth, brightness), ArcShape (lower
        //edge, height, folds' frequency, seed) and ArcBend (bend's depth, its frequency, how often it is lit)
        private readonly record struct Arc(float Offset, float Angle, float FoldDepth, float Brightness,
            float LowerEdge, float Height, float FoldFrequency, float Seed, float BendDepth, float BendFrequency, float LitFrequency);

        //How far under a curtain's edge, in its heights, the glow below it is still worked out
        private const float BELOW_EDGE = 4f;

        private static float Frac(float x) => x - MathF.Floor(x);

        //Aurora.fx's AuroraHash and AuroraNoise
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static float Hash(float n)
        {
            n = Frac(n * 0.1031f);
            n *= n + 33.33f;
            n *= n + n;
            return Frac(n) * 2f - 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static float Noise(float x)
        {
            float i = MathF.Floor(x);
            float f = x - i;
            float u = f * f * (3f - 2f * f);
            return MathHelper.Lerp(Hash(i) * f, Hash(i + 1f) * (f - 1f), u) * 2f;
        }

        private static float Smoothstep(float a, float b, float x)
        {
            float t = MathHelper.Clamp((x - a) / (b - a), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        //AuroraFold at the bake's moment (time 0)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static float Fold(float u, float seed) => Noise(u + seed) + 0.45f * Noise(u * 2.3f + seed * 1.7f);

        //The hang off the arc's mean line at x0 along it: its long bend and its folds
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static float Hang(in Arc arc, float x0) =>
            arc.BendDepth * Noise(x0 * arc.BendFrequency + arc.Seed * 3.7f) + arc.FoldDepth * Fold(x0 * arc.FoldFrequency, arc.Seed);

        /// <summary>
        /// Aurora.fx's <c>AuroraArc</c> at time 0, term for term, except that the rays fade by the texel's footprint (a column
        /// is <paramref name="texelAngle"/> round the sky) where the desktop's fade by the pixel's.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static Vector3 ArcLight(Vector3 dir, in Arc arc, AuroraCurtainsConfig curtains, float texelAngle)
        {
            float s = MathF.Sin(arc.Angle), c = MathF.Cos(arc.Angle);
            float along = dir.X * c + dir.Z * s;
            float across = dir.Z * c - dir.X * s;

            //Only a ray heading the arc's way meets it, and one running nearly along it meets it at the horizon
            float heads = (across * arc.Offset >= 0f ? 1f : 0f) * Smoothstep(0.02f, 0.06f, MathF.Abs(across));
            if (heads <= 0f) return Vector3.Zero;
            float safeAcross = (across < 0f ? -1f : 1f) * MathF.Max(MathF.Abs(across), 0.02f);

            float x0 = arc.Offset / safeAcross * along;
            float hang = Hang(arc, x0);
            float t = MathF.Max((arc.Offset + hang) / safeAcross, 0f);

            float x = t * along;
            float y = t * dir.Y;

            //Where in the curtain the eye looks: 0 at the lower edge (a gentle ripple along it), 1 at the top
            float edge = arc.LowerEdge + 0.03f * arc.Height * Noise(x0 * 0.9f + arc.Seed * 5f);
            float v = (y - edge) / arc.Height;

            //Nothing over the curtain's top, past its ends, or so far under its edge that the glow below it has gone (e^-8):
            //most of the sky, left before the noises that follow
            if (v >= 1f || v < -BELOW_EDGE || MathF.Abs(x) >= curtains.Length) return Vector3.Zero;

            float bottom = Smoothstep(-curtains.EdgeSoftness, 0f, v);
            float body = MathF.Exp(-2.6f * MathF.Max(v, 0f)) * (1f - Smoothstep(0.7f, 1f, v));
            body += 0.35f * MathF.Exp(MathF.Min(v, 0f) / 0.5f) * (1f - bottom);

            //The slant: a sheet seen edge-on is looked through for longer
            const float DELTA = 0.02f;
            float slope = (Hang(arc, x0 + DELTA) - hang) / DELTA;
            Vector2 sheet = Vector2.Normalize(new Vector2(-slope, 1f));
            Vector2 heading = Vector2.Normalize(new Vector2(along, across));
            float slant = MathF.Min(1f / MathF.Max(MathF.Abs(Vector2.Dot(heading, sheet)), 0.2f), 3.5f);

            //The rays, faded where they would be finer than the texel. Along the arc a ray's x is the offset times along over
            //across, which no elevation changes (the rays run straight up the texture), so a texel's footprint is its step
            //round the sky: d(x)/d(azimuth) = offset (along^2 + across^2) / across^2
            float footprint = MathF.Abs(arc.Offset) * (along * along + across * across) / (safeAcross * safeAcross) * texelAngle;
            float rayFade = MathHelper.Clamp(1.5f - footprint * curtains.RayScale * 2.5f, 0f, 1f);
            float rays = MathHelper.Clamp(0.4f + 0.55f * Noise(x * curtains.RayScale * 0.45f + arc.Seed * 1.9f)
                + 0.45f * Noise(x * curtains.RayScale + arc.Seed * 7.1f)
                + 0.3f * Noise(x * curtains.RayScale * 2.7f + arc.Seed * 3.3f), 0f, 1f);
            rays = MathHelper.Lerp(1f, rays * rays * 2.2f, curtains.RayStrength * rayFade);

            //Lit in stretches, not along its whole length
            float stretch = Smoothstep(-0.25f, 0.6f, Noise(x0 * arc.LitFrequency + arc.Seed * 2.3f)
                + 0.5f * Noise(x0 * arc.LitFrequency * 2.6f + arc.Seed * 9.7f));
            rays *= 0.04f + 0.96f * stretch;

            //Out to its ends along the axis, and dimmer far away and low
            float reach = 1f - Smoothstep(curtains.Length * 0.55f, curtains.Length, MathF.Abs(x));
            float air = MathF.Exp(-t * 0.05f) * MathHelper.Clamp(dir.Y * 9f, 0f, 1f);

            Vector3 colour = Vector3.Lerp(curtains.Green.ToVector3(), curtains.Red.ToVector3(), Smoothstep(0.3f, 0.9f, v))
                + curtains.Fringe.ToVector3() * MathF.Exp(-MathF.Max(v, 0f) * 25f);

            return colour * (arc.Brightness * heads * bottom * body * slant * rays * reach * air);
        }

        public void Dispose()
        {
            _texture?.Dispose();
            _texture = null;
        }
    }
}
