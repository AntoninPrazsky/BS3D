using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The aurora's sky on the Potato path (#1044): the desktop's night of #963 (<c>Aurora.fx</c>'s <c>AuroraSkyPS</c>) in
    /// place of #829's band, which was the look the owner rejected on the desktop. The desktop intersects every view ray
    /// with three curtains per pixel, a dozen noises an arc. Here the same arcs, on the same config, are worked out on the
    /// CPU (<see cref="Bake"/>, a port of <c>AuroraArc</c> term for term) into a texture over the upper hemisphere, which
    /// <c>PotatoSky.fx</c>'s <c>PotatoSkyAurora</c> reads once a pixel. The night under them - the gradient, the glow along
    /// the horizon, the mist at it and the air lit round the Moon - is smooth and the vertex's (<see cref="Night"/>,
    /// <see cref="Halo"/>), through the curve there, as the dome's gradient is.
    /// <para>
    /// ⚠ <b>The texture holds the curtains already through the curve</b>: what they add to the night on the display, at
    /// the effect's exposure, so the pixel adds and does nothing else. Curved per pixel - the exposure, the ACES fit and the
    /// sRGB power on the sum - with the Moon's roots and two sines, the pass cost 5.6 ms at native on Anchor. A change of
    /// the exposure bakes again. Each arc breathes on its own clock (<see cref="ArcBreaths"/>, the desktop's
    /// <c>ArcBrightness</c>, carried to the display's scale; the texture's alpha says which arc a texel is), and the rays
    /// shimmer by two beats sliding round the sky in place of running along each curtain. The folds do not re-form. The
    /// Moon's disc is the pixel's, as on the desktop, by the squared chord to it.
    /// </para>
    /// The bake (a quarter to half a second on the Pi's cores) runs off the frame, and the curtains brighten in over
    /// <see cref="FADE_IN"/> once it is in: on the frame it held the scene's build at 0.6 s, a stall in the scene picker,
    /// which applies a scene at once.
    /// </summary>
    public sealed class PotatoAuroraSky : IDisposable
    {
        /// <summary>
        /// The texture: columns round the sky from +X towards +Z (<see cref="PotatoSkyFigure.BuildSphere"/>'s longitude),
        /// rows from the horizon to the zenith evenly in elevation, as the sphere's rings are, so the vertex's coordinates
        /// interpolate exactly. A column is a tenth of a degree and a row an eighth, two to four pixels at the play view's
        /// lens; rays finer than a column are faded as the desktop fades those finer than a pixel.
        /// </summary>
        public const int WIDTH = 4096, HEIGHT = 768;

        /// <summary>How deep the rays' shimmer cuts the curtains, on the display.</summary>
        public const float SHIMMER = 0.15f;

        /// <summary>How long the curtains take to brighten in once the bake is in, seconds of real time.</summary>
        public const float FADE_IN = 1.5f;

        /// <summary>
        /// The power that carries an arc's breath (a share of its light) to the display's scale: the texture's light is
        /// curved, and a dim curtain scales with its light where a bright fold, near the curve's shoulder, hardly does.
        /// </summary>
        public const float BREATH_POWER = 0.6f;

        /// <summary>Where the disc's edge runs, as shares of its radius: a band a sixth of it wide, a pixel or two at the play view's lens.</summary>
        private const float DISC_INNER = 0.92f, DISC_OUTER = 1.08f;

        private readonly AuroraSceneConfig _config;
        private Texture2D _texture;
        private readonly EffectParameter _textureParameter, _shown, _breaths, _moonDisc, _moonHalo, _exposure;

        //The bake running off the frame, null when none is; the exposure the texture holds (NaN before the first bake is
        //in, when the curtains are drawn at nought and the texture's contents are never seen) and when it went in
        private Task<Color[]> _bake;
        private float _bakeExposure, _shownExposure = float.NaN;
        private long _bakedAt;

        //Cancelled when the sky is disposed: the picker builds a scene's figure as it passes over it, and a bake left
        //running for a sky nobody draws holds three of the Pi's four cores for half a second and 12.6 MB of texels.
        //Never disposed itself, so a bake that has not yet registered with it cannot meet a disposed source; it owns no timer
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();

        //A failed bake is tried once more, so a passing fault does not leave the sky without its curtains
        private bool _retried;

        private PotatoAuroraSky(GraphicsDevice device, Effect effect, AuroraSceneConfig config)
        {
            _config = config;
            _texture = new Texture2D(device, WIDTH, HEIGHT, false, SurfaceFormat.Color);

            EffectParameterCollection parameters = effect.Parameters;
            _textureParameter = parameters["AuroraTexture"];
            _shown = parameters["AuroraShown"];
            _breaths = parameters["ArcBreath"];
            _moonDisc = parameters["MoonDiscDisplay"];
            _moonHalo = parameters["MoonHalo"];
            _exposure = parameters["Exposure"];
            StartBake(ExposureNow());
        }

        /// <summary>The aurora's sky through <paramref name="effect"/> (<c>PotatoSky</c>), baked on the config's defaults; null when the effect has no texture for it yet.</summary>
        public static PotatoAuroraSky Create(GraphicsDevice device, Effect effect)
        {
            if (effect.Parameters["AuroraTexture"] == null) return null;
            return new PotatoAuroraSky(device, effect, new AuroraSceneConfig());
        }

        //The exposure the host last pushed into the effect (ApplyPotatoExposure), which the texture must be baked at
        private float ExposureNow() => _exposure?.GetValueSingle() ?? PostProcessPipeline.DEFAULT_EXPOSURE;

        private void StartBake(float exposure)
        {
            AuroraSceneConfig config = _config;
            CancellationToken cancel = _cancel.Token;
            _bakeExposure = exposure;
            _bake = Task.Run(() => Bake(config, WIDTH, HEIGHT, exposure, cancel), cancel);
        }

        /// <summary>
        /// Its uniforms at <paramref name="time"/>: the texture, the arcs' breaths, the Moon's light. The first call after a
        /// bake is done hands its texels to the texture, on the thread that draws; a call that finds the exposure changed
        /// starts another, and the old texture stays up until it is in. A bake that failed is logged and, the first time,
        /// started again.
        /// </summary>
        public void Push(float time)
        {
            if (_bake != null && _bake.IsCompleted)
            {
                if (_bake.IsCompletedSuccessfully)
                {
                    _texture.SetData(_bake.Result);
                    if (float.IsNaN(_shownExposure)) _bakedAt = Stopwatch.GetTimestamp();
                    _shownExposure = _bakeExposure;
                }
                else if (_bake.IsFaulted)
                {
                    Console.WriteLine($"[aurora] bake failed{(_retried ? "" : ", trying once more")}: {_bake.Exception.GetBaseException().Message}");
                    if (!_retried) _bakeExposure = float.NaN;
                    _retried = true;
                }
                _bake = null;
            }
            float exposure = ExposureNow();
            if (_bake == null && exposure != _bakeExposure) StartBake(exposure);

            float shown = _bakedAt == 0 ? 0f : MathHelper.Clamp((float)Stopwatch.GetElapsedTime(_bakedAt).TotalSeconds / FADE_IN, 0f, 1f);
            Vector3 breaths = ArcBreaths(_config, time);

            _textureParameter?.SetValue(_texture);
            _shown?.SetValue(shown * shown * (3f - 2f * shown));
            _breaths?.SetValue(new Vector3(MathF.Pow(breaths.X, BREATH_POWER), MathF.Pow(breaths.Y, BREATH_POWER), MathF.Pow(breaths.Z, BREATH_POWER)));
            _moonDisc?.SetValue(PotatoDisplay.ToDisplay(_config.Sky.MoonDisc.ToVector3(), exposure));
            _moonHalo?.SetValue(_config.Sky.MoonHalo.ToVector3());
        }

        /// <summary>
        /// The disc's edges for the pixel (<c>SkyParams2</c>'s y to w): the squared chord where it starts to fade, one over
        /// the band it fades across, and one over its radius squared, for the limb.
        /// </summary>
        public static Vector3 DiscEdges(AuroraSceneConfig config)
        {
            float radius = 2f * MathF.Sin(MathHelper.ToRadians(config.Sky.MoonRadiusDegrees) * 0.5f);
            float inner = radius * DISC_INNER, outer = radius * DISC_OUTER;
            return new Vector3(inner * inner, 1f / (outer * outer - inner * inner), 1f / (radius * radius));
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
        /// The night along <paramref name="dir"/>, linear: the gradient from the horizon up, the glow along the horizon,
        /// strongest under the curtains, and the mist the horizon fades into (Aurora.fx's <c>AuroraSkyPS</c>).
        /// ⚠ <c>PotatoSky.fx</c>'s <c>AuroraVS</c> is this line for line: the bake curves the curtains over it.
        /// </summary>
        public static Vector3 Night(AuroraSceneConfig config, Vector3 dir)
        {
            AuroraNightSkyConfig sky = config.Sky;
            float up = MathHelper.Clamp(dir.Y, 0f, 1f);
            Vector3 night = Vector3.Lerp(sky.Horizon.ToVector3(), sky.Zenith.ToVector3(), MathF.Pow(up, sky.GradientPower));

            float bearing = MathHelper.ToRadians(config.Curtains.BearingDegrees);
            Vector2 flat = Vector2.Normalize(new Vector2(dir.X + 1e-5f, dir.Z));
            float towards = MathHelper.Clamp(Vector2.Dot(flat, new Vector2(MathF.Sin(bearing), MathF.Cos(bearing))) * 0.5f + 0.5f, 0f, 1f);
            night += sky.HorizonGlow.ToVector3() * (MathF.Exp(-up / sky.HorizonGlowWidth) * (0.45f + 0.9f * towards * towards));

            return Vector3.Lerp(night, config.Mist.Color.ToVector3(), MathHelper.Clamp(1f - up / 0.035f, 0f, 1f) * 0.85f);
        }

        /// <summary>The air lit round the Moon along <paramref name="dir"/>, linear, by the chord to it. ⚠ <c>AuroraVS</c>'s too.</summary>
        public static Vector3 Halo(AuroraSceneConfig config, Vector3 dir, Vector3 moon)
        {
            float chord = MathF.Sqrt(MathF.Max(2f - 2f * Vector3.Dot(dir, moon), 0f));
            return config.Sky.MoonHalo.ToVector3() * MathF.Exp(-chord / config.Sky.MoonHaloWidth);
        }

        /// <summary>Where the Moon stands, as the desktop's backdrop places it.</summary>
        public static Vector3 MoonDirection(AuroraSceneConfig config) =>
            SceneRenderer.DirectionFromElevationAzimuth(Math.Clamp(config.Sky.MoonElevationDegrees, 1f, 89f), config.Sky.MoonAzimuthDegrees);

        /// <summary>
        /// The texels at <paramref name="exposure"/>, no device: for the tests. Each holds what the arcs at their full
        /// brightness add on the display to the night and the halo along that direction (the curve of the sum less the curve
        /// of the night; none at all where no curtain is), and in alpha which arc it is - 0 the first, a half the second,
        /// 1 the third, between where two overlap. <paramref name="cancel"/> stops it between rows, with
        /// <see cref="OperationCanceledException"/>.
        /// </summary>
        public static Color[] Bake(AuroraSceneConfig config, int width, int height, float exposure,
            CancellationToken cancel = default)
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
            Vector3 moon = MoonDirection(config);

            //A core left to the game's thread, which draws on while the bake runs
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount - 1, 1),
                CancellationToken = cancel,
            };
            Parallel.For(0, height, options, row => BakeRow(texels, row, width, height, columns, arcs, config, moon, exposure));

            return texels;
        }

        //One row of the texture. The bake runs as the scene is built, so its methods are compiled optimised at once: at the
        //runtime's first tier the first bake took twice as long as a warm one
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static void BakeRow(Color[] texels, int row, int width, int height, Vector2[] columns, Arc[] arcs,
            AuroraSceneConfig config, Vector3 moon, float exposure)
        {
            float elevation = MathHelper.PiOver2 * (row + 0.5f) / height;
            float sinElevation = MathF.Sin(elevation), cosElevation = MathF.Cos(elevation);
            float texelAngle = MathHelper.TwoPi / width;
            int count = arcs.Length;
            for (int column = 0; column < width; column++)
            {
                var dir = new Vector3(cosElevation * columns[column].X, sinElevation, cosElevation * columns[column].Y);

                Vector3 light = Vector3.Zero;
                float weight = 0f, which = 0f;
                for (int k = 0; k < count; k++)
                {
                    Vector3 c = ArcLight(dir, arcs[k], config.Curtains, texelAngle);
                    light += c;
                    float luma = ColorSpace.Luminance(c);
                    weight += luma;
                    which += luma * (count > 1 ? k / (count - 1f) : 0f);
                }
                if (weight <= 1e-6f)
                {
                    texels[row * width + column] = new Color(0, 0, 0, 0);
                    continue;
                }

                Vector3 under = Night(config, dir) + Halo(config, dir, moon);
                Vector3 added = PotatoDisplay.ToDisplay(under + light, exposure) - PotatoDisplay.ToDisplay(under, exposure);
                texels[row * width + column] = new Color(Vector3.Max(added, Vector3.Zero)) { A = (byte)MathF.Round(which / weight * 255f) };
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
            _cancel.Cancel();
            _texture?.Dispose();
            _texture = null;
        }
    }
}
