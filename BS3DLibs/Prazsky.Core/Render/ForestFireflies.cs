using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A scatter of firefly-like lights drifting over the forest floor (#487) — the naturalistic scenes'
    /// own answer to "a light visibly turning on and off," which until now only the city (its windows, its
    /// neon buzz, the roof beacon, #436) and gameplay events (a blast's flash, #389) had. Built and drawn
    /// like <see cref="CityRooftops"/>'s beacon: a small sphere whose <see
    /// cref="InstancedModelRenderer.EmissiveTint"/> is driven over <c>GLARE_THRESHOLD</c>, so the bloom pass
    /// makes the glow round it. Emissive only — it lights nothing around it, it only shows, which is why this
    /// never touches <see cref="SceneLights"/>.
    /// <para>
    /// <b>What makes it read as a firefly and not as a lamp that blinks (#961).</b> The first build was five
    /// lit spheres 40 to 75 units out, each on for a fifth of its period, and the owner saw "something that
    /// flickers on the ground and I cannot tell what": measured, a hard-edged dot of six pixels that appeared
    /// as an olive disc (the sphere's own lit body, visible the moment the sine left zero), swelled to white in
    /// a third of a second and vanished as abruptly, once in three seconds somewhere on the far floor. So now:
    /// the body is <b>black</b> and shows only as the light it gives out, so nothing is there between glows and
    /// a glow starts from nothing; the swell is the sine <b>squared</b>, which leaves zero with no slope and
    /// lasts a second or more; there are a dozen of them, nearer in, hovering a man's height up and each
    /// <b>wandering</b> a little on its own slow circuit, so the eye finds a handful of living lights rather
    /// than one lamp; and the glow is carried far over the glare threshold, which is what the bloom makes a
    /// soft halo of.
    /// </para>
    /// <para>
    /// Where the beacons on a roof flash by <b>cluster</b>, on the wall clock (#902), each firefly is rolled
    /// its own period, phase and wander at construction, so the handful of them never beat in unison.
    /// </para>
    /// </summary>
    public sealed class ForestFireflies : IDisposable
    {
        private readonly struct Firefly
        {
            public readonly Vector3 Home;
            public readonly float Period;
            public readonly float Phase;
            public readonly Vector3 WanderPhase;
            public readonly Vector3 WanderRate;

            public Firefly(Vector3 home, float period, float phase, Vector3 wanderPhase, Vector3 wanderRate)
            {
                Home = home;
                Period = period;
                Phase = phase;
                WanderPhase = wanderPhase;
                WanderRate = wanderRate;
            }
        }

        /// <summary>The slowest and fastest rate (radians per second) a firefly's wander turns at, per axis: one
        /// circuit takes between about 16 and 42 seconds, so a glow of a second or two moves a little and a
        /// firefly seen twice has moved on.</summary>
        private const float WANDER_RATE_MIN = 0.15f;
        private const float WANDER_RATE_MAX = 0.4f;

        /// <summary>The swell below which a firefly is not drawn: a body that is black and a glow under this is
        /// a dot nobody could see against the floor, and not drawing it is what keeps a pulse's two ends from
        /// popping.</summary>
        private const float MIN_SWELL = 0.002f;

        private readonly SphereMesh _mesh;
        private readonly InstancedModelRenderer _renderer;
        private readonly BasicEffectParams _lamp;
        private readonly ModelInstance[] _instance = new ModelInstance[1];
        private static readonly Vector4 NO_OCCLUSION = new(0f, 0f, 0f, 1f);

        private Firefly[] _fireflies;
        private float _onFraction;
        private float _wanderRadius;
        private float _wanderBob;
        private Vector3 _peakColor;

        /// <param name="device">Graphics device the sphere and its instance buffer live on.</param>
        /// <param name="instancingEffect">The shared instancing effect, exactly as every other scene fixture
        /// draws through it — that is what gives the firefly the beacon's own bloom for free.</param>
        /// <param name="config">The forest scene's configuration, read here only — the same contract
        /// <see cref="ForestScatterRenderer"/> keeps.</param>
        /// <param name="sceneAmbientIntensity">Kept for the three hosts' common call (they pass the figure they
        /// hand <see cref="ForestScatterRenderer"/>); the body is black and takes no ambient, so it is unused.</param>
        /// <param name="seed">Seed for each firefly's position, period, phase and wander; the same seed always
        /// plants the same ones in the same places.</param>
        public ForestFireflies(GraphicsDevice device, Effect instancingEffect, ForestSceneConfig config,
            float sceneAmbientIntensity, int seed = ForestScatterRenderer.DEFAULT_SEED)
        {
            ForestFireflyConfig fireflyConfig = config.Fireflies;

            _mesh = new SphereMesh(device, MathF.Max(0.01f, fireflyConfig.BodyRadius), 12, 8);

            //A black, matte body: no ambient, no diffuse, and a specular so dim it cannot show (a ZERO specular
            //means "no override", which is the renderer's white one) - so what the pixel holds is the emissive
            //tint and nothing else, and a firefly between glows is not there at all
            _lamp = new BasicEffectParams(Vector3.Zero, new Vector3(0.004f), 8f, Vector3.Zero);
            _renderer = new InstancedModelRenderer(device, _mesh, Vector3.Zero, instancingEffect);

            Plant(config, seed);
        }

        private void Plant(ForestSceneConfig config, int seed)
        {
            ForestFireflyConfig fireflyConfig = config.Fireflies;
            _onFraction = MathHelper.Clamp(fireflyConfig.OnFraction, 0.01f, 1f);
            _wanderRadius = MathF.Max(0f, fireflyConfig.WanderRadius);
            _wanderBob = MathF.Min(MathF.Max(0f, fireflyConfig.WanderBob), fireflyConfig.HoverHeight);
            _peakColor = fireflyConfig.Color.ToVector3() * fireflyConfig.Brightness;

            var random = new Random(seed);
            var fireflies = new Firefly[Math.Max(0, fireflyConfig.Count)];

            for (int i = 0; i < fireflies.Length; i++)
            {
                float angle = (float)(random.NextDouble() * MathHelper.TwoPi);
                float radius = MathHelper.Lerp(fireflyConfig.MinRadius, fireflyConfig.MaxRadius, (float)random.NextDouble());
                float x = MathF.Cos(angle) * radius;
                float z = MathF.Sin(angle) * radius;
                float y = TerrainMirror.Forest(x, z, config) + fireflyConfig.HoverHeight;

                float period = MathHelper.Lerp(fireflyConfig.MinPeriod, fireflyConfig.MaxPeriod, (float)random.NextDouble());
                float phase = (float)(random.NextDouble() * period);

                var wanderPhase = new Vector3(
                    (float)(random.NextDouble() * MathHelper.TwoPi),
                    (float)(random.NextDouble() * MathHelper.TwoPi),
                    (float)(random.NextDouble() * MathHelper.TwoPi));
                var wanderRate = new Vector3(
                    MathHelper.Lerp(WANDER_RATE_MIN, WANDER_RATE_MAX, (float)random.NextDouble()),
                    MathHelper.Lerp(WANDER_RATE_MIN, WANDER_RATE_MAX, (float)random.NextDouble()),
                    MathHelper.Lerp(WANDER_RATE_MIN, WANDER_RATE_MAX, (float)random.NextDouble()));

                fireflies[i] = new Firefly(new Vector3(x, y, z), period, phase, wanderPhase, wanderRate);
            }

            _fireflies = fireflies;
        }

        /// <summary>
        /// Draws every firefly currently glowing; one it leaves dark this frame costs nothing beyond the gate
        /// check that says so. Touches no GPU state beyond the renderer's own draw, and allocates nothing —
        /// the instance array is the one field, overwritten in place.
        /// </summary>
        /// <param name="wallClock">The wall clock, like the roof beacon's and the campfire's own — a firefly
        /// keeps glowing and wandering while the simulation is paused.</param>
        public void Draw(ICamera camera, float wallClock)
        {
            foreach (Firefly firefly in _fireflies)
            {
                float phase = (wallClock + firefly.Phase) / firefly.Period;
                phase -= MathF.Floor(phase);
                if (phase >= _onFraction) continue;

                //The sine squared: it leaves zero with no slope, so a glow comes up out of the dark and goes back
                //into it, where the plain sine's straight start read as a switch
                float swell = MathF.Sin(phase / _onFraction * MathHelper.Pi);
                swell *= swell;
                if (swell < MIN_SWELL) continue;

                //Its own slow circuit round where it hovers: sideways a little, up and down less
                Vector3 position = firefly.Home + new Vector3(
                    MathF.Sin(wallClock * firefly.WanderRate.X + firefly.WanderPhase.X) * _wanderRadius,
                    MathF.Sin(wallClock * firefly.WanderRate.Y + firefly.WanderPhase.Y) * _wanderBob,
                    MathF.Cos(wallClock * firefly.WanderRate.Z + firefly.WanderPhase.Z) * _wanderRadius);

                _renderer.EmissiveTint = _peakColor * swell;
                _instance[0] = new ModelInstance(Matrix.CreateTranslation(position), NO_OCCLUSION);
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
