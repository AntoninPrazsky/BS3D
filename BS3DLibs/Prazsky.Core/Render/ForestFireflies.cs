using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A handful of firefly-like lights hovering over the forest floor (#487) — the naturalistic scenes'
    /// own answer to "a light visibly turning on and off," which until now only the city (its windows, its
    /// neon buzz, the roof beacon, #436) and gameplay events (a blast's flash, #389) had. Built and drawn
    /// exactly like <see cref="CityRooftops"/>'s beacon: a small sphere whose <see
    /// cref="InstancedModelRenderer.EmissiveTint"/> is driven by a hard sine pulse gated to a share of its
    /// own period, over <c>GLARE_THRESHOLD</c> so it blooms. Emissive only, like the beacon — it lights
    /// nothing around it, it only shows, which is why this never touches <see cref="SceneLights"/>.
    /// <para>
    /// Where the beacons on a roof are set to flash <b>together</b>, on the wall clock, because that is how a
    /// city's warning lights are wired, fireflies are not: each is rolled its own period and phase at
    /// construction, so the handful of them never beat in unison.
    /// </para>
    /// </summary>
    public sealed class ForestFireflies : IDisposable
    {
        private readonly struct Firefly
        {
            public readonly Vector3 Position;
            public readonly float Period;
            public readonly float Phase;

            public Firefly(Vector3 position, float period, float phase)
            {
                Position = position;
                Period = period;
                Phase = phase;
            }
        }

        private readonly SphereMesh _mesh;
        private readonly InstancedModelRenderer _renderer;
        private readonly BasicEffectParams _lamp;
        private readonly ModelInstance[] _instance = new ModelInstance[1];
        private static readonly Vector4 NO_OCCLUSION = new(0f, 0f, 0f, 1f);

        private Firefly[] _fireflies;
        private float _onFraction;
        private Vector3 _peakColor;

        /// <param name="device">Graphics device the sphere and its instance buffer live on.</param>
        /// <param name="instancingEffect">The shared instancing effect, exactly as every other scene fixture
        /// draws through it — that is what gives the firefly the beacon's own bloom for free.</param>
        /// <param name="config">The forest scene's configuration, read here only — the same contract
        /// <see cref="ForestScatterRenderer"/> keeps.</param>
        /// <param name="sceneAmbientIntensity">The caller's flat ambient fill, carried only so the sphere is
        /// not pitch black between pulses; it never lights anything else. All three executables pass the same
        /// figure they hand <see cref="ForestScatterRenderer"/>.</param>
        /// <param name="seed">Seed for each firefly's position, period and phase; the same seed always plants
        /// the same handful in the same places.</param>
        public ForestFireflies(GraphicsDevice device, Effect instancingEffect, ForestSceneConfig config,
            float sceneAmbientIntensity, int seed = ForestScatterRenderer.DEFAULT_SEED)
        {
            ForestFireflyConfig fireflyConfig = config.Fireflies;

            _mesh = new SphereMesh(device, MathF.Max(0.01f, fireflyConfig.BodyRadius), 6, 4);
            _lamp = new BasicEffectParams(Vector3.One * sceneAmbientIntensity, new Vector3(0.6f), 60f, Vector3.Zero);
            _renderer = new InstancedModelRenderer(device, _mesh, fireflyConfig.Color.ToVector3(), instancingEffect);

            Plant(config, seed);
        }

        private void Plant(ForestSceneConfig config, int seed)
        {
            ForestFireflyConfig fireflyConfig = config.Fireflies;
            _onFraction = MathHelper.Clamp(fireflyConfig.OnFraction, 0.01f, 1f);
            _peakColor = fireflyConfig.Color.ToVector3() * fireflyConfig.Brightness;

            var random = new Random(seed);
            var fireflies = new Firefly[Math.Max(0, fireflyConfig.Count)];

            for (int i = 0; i < fireflies.Length; i++)
            {
                float angle = (float)(random.NextDouble() * MathHelper.TwoPi);
                float radius = MathHelper.Lerp(fireflyConfig.MinRadius, fireflyConfig.MaxRadius, (float)random.NextDouble());
                float x = MathF.Cos(angle) * radius;
                float z = MathF.Sin(angle) * radius;
                float y = SceneRenderer.ForestTerrainHeight(x, z, config) + fireflyConfig.HoverHeight;

                float period = MathHelper.Lerp(fireflyConfig.MinPeriod, fireflyConfig.MaxPeriod, (float)random.NextDouble());
                float phase = (float)(random.NextDouble() * period);

                fireflies[i] = new Firefly(new Vector3(x, y, z), period, phase);
            }

            _fireflies = fireflies;
        }

        /// <summary>
        /// Draws every firefly currently lit; one it draws dark this frame costs nothing beyond the gate
        /// check that says so. Touches no GPU state beyond the renderer's own draw, and allocates nothing —
        /// the instance array is the one field, overwritten in place.
        /// </summary>
        /// <param name="wallClock">The wall clock, like the roof beacon's and the campfire's own — a firefly
        /// keeps blinking while the simulation is paused.</param>
        public void Draw(ICamera camera, float wallClock)
        {
            foreach (Firefly firefly in _fireflies)
            {
                float phase = (wallClock + firefly.Phase) / firefly.Period;
                phase -= MathF.Floor(phase);
                if (phase >= _onFraction) continue;

                float flash = MathF.Sin(phase / _onFraction * MathHelper.Pi);

                _renderer.EmissiveTint = _peakColor * flash;
                _instance[0] = new ModelInstance(Matrix.CreateTranslation(firefly.Position), NO_OCCLUSION);
                _renderer.Draw(camera, _instance, 1, _lamp);
            }
        }

        public void Dispose()
        {
            _renderer?.Dispose();
            _mesh?.Dispose();
        }
    }
}
