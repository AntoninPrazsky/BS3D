using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;
using System.Runtime.InteropServices;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The clouds as volumes on High and Ultra (#989): every heap of cumulus the deck's weather layer holds near the lens,
    /// drawn as a box its pixels march through (<c>CloudVolume.fx</c>). The deck (<see cref="CloudField"/>, <c>Sky.fx</c>)
    /// stays: it is what the shadows and the light rig read, and past <see cref="REACH"/> it is the far layer.
    /// <para>
    /// <b>Where a heap stands is the deck's own weather.</b> The heaps sit on a lattice that rides the weather layer's first
    /// octave (<see cref="CloudField.Wind"/> times <see cref="CloudField.Time"/>), so they drift with the deck; each stands
    /// where <see cref="CloudField.Cover"/> says there is cloud and is as big as the cover there, so as the second octave
    /// evolves the field a heap grows, thins and is gone, and the cloud overhead is still the one that shades the ground.
    /// </para>
    /// </summary>
    public sealed class CloudVolumes : IDisposable
    {
        //The lattice: a heap a cell, the cell this share of the weather layer's feature size (1 / Scale); how far out heaps
        //stand (inside the camera's far plane, DEFAULT_FAR_PLANE_DISTANCE 2000) and where they dissolve into the sky
        private const float CELL_SHARE = 0.5f;
        private const float REACH = 1750f, FADE_NEAR = 1250f, FADE_FAR = 1700f;
        private const int MAX_HEAPS = 320;

        //A heap's size against the cover under it: none under COVER_MIN, its radius a share of the cell and its height a
        //share of its radius, both growing with the cover, so a dense patch towers
        private const float COVER_MIN = 0.12f, COVER_FADE = 0.15f;
        private const float RADIUS_MIN = 0.30f, RADIUS_GROWTH = 0.32f;
        private const float HEIGHT_MIN = 0.75f, HEIGHT_GROWTH = 0.9f;

        //Which skies stand as heaps: the weather's coverage bias, below which a sky is cumulus (Scattered 0.02, Broken 0.30) and
        //above which it is a lid (Storm 0.72, Overcast 0.85), the volumes and the deck's cut fading between them as a change of
        //sky does (WeatherLook.Lerp), so nothing pops
        private const float HEAPS_UNTIL_BIAS = 0.45f, LID_FROM_BIAS = 0.65f;

        //The heaps' noise, baked once (CloudVolume.fx's NOISE_PERIOD)
        internal const int NOISE_PERIOD = 8, NOISE_TEXELS_PER_UNIT = 4, NOISE_SEED = 989;
        internal const int CAPACITY = MAX_HEAPS;
        internal const float COVER_THRESHOLD = COVER_MIN, REACH_DISTANCE = REACH;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Heap : IVertexType
        {
            public Vector4 Cloud;   //the base's centre (x, height, z) and the radius
            public Vector4 Shape;   //the height, the seed, the density and how far in it has faded

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
                new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2));

            readonly VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }

        private readonly GraphicsDevice _device;
        private readonly Effect _effect;
        private readonly EffectParameter _view, _projection, _cameraPosition, _sunDirection, _sunColor, _shadowColor, _time;
        private readonly VertexBuffer _box;
        private readonly IndexBuffer _boxIndices;
        private readonly DynamicVertexBuffer _instances;
        private readonly Texture3D _noise;
        private readonly Heap[] _heaps = new Heap[MAX_HEAPS];
        private readonly float[] _order = new float[MAX_HEAPS];

        //THE LAYER: the heaps are marched into a target the display's own size, not the supersampled scene's (a cloud is soft,
        //and four samples a pixel only march one soft edge four times over: a quarter of the cost on High), then laid over
        //the scene target right after the sky. Drawn before the ground, it is covered by whatever stands in front of it, as
        //the deck is. ⚠ Marched into the scene target itself after the ground, depth-tested, a broken sky cost +3.2 ms.
        private readonly EffectTechnique _march, _composite;
        private readonly EffectParameter _layerParam;
        private readonly VertexBuffer _screenQuad;
        private RenderTarget2D _layer;
        private readonly RenderTargetBinding[] _bound = new RenderTargetBinding[4];

        //The sky shader's own cloud light, read back from it each frame (CloudField pushes the dome's colours there), and the
        //deck's cut it is handed
        private Effect _skyFor;
        private EffectParameter _skySunDirection, _skySunColor, _skyShadowColor, _skyCut, _skyCutNear, _skyCutFar;

        public CloudVolumes(GraphicsDevice device, ContentManager content)
        {
            _device = device;
            _effect = content.Load<Effect>("Shaders/CloudVolume");
            EffectParameterCollection p = _effect.Parameters;
            _view = p["View"];
            _projection = p["Projection"];
            _cameraPosition = p["CameraPosition"];
            _sunDirection = p["SunDirection"];
            _sunColor = p["CloudSunColor"];
            _shadowColor = p["CloudShadowColor"];
            _time = p["CloudTime"];
            p["FadeNear"].SetValue(FADE_NEAR);
            p["FadeFar"].SetValue(FADE_FAR);

            (SceneRenderer.BillboardVertex[] vertices, short[] indices) = ProxyVolumes.UnitBox();
            _box = new VertexBuffer(device, SceneRenderer.BillboardVertex.Declaration, vertices.Length, BufferUsage.WriteOnly);
            _box.SetData(vertices);
            _boxIndices = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            _boxIndices.SetData(indices);
            _instances = new DynamicVertexBuffer(device, Heap.Declaration, MAX_HEAPS, BufferUsage.WriteOnly);

            int side = NOISE_PERIOD * NOISE_TEXELS_PER_UNIT;
            _noise = new Texture3D(device, side, side, side, false, SurfaceFormat.Single);
            _noise.SetData(ProxyVolumes.GradientNoiseTexels(NOISE_PERIOD, NOISE_TEXELS_PER_UNIT, NOISE_SEED));
            p["CloudNoiseVolume"].SetValue(_noise);

            _march = _effect.Techniques["CloudVolume"];
            _composite = _effect.Techniques["CloudComposite"];
            _layerParam = p["CloudLayer"];
            _screenQuad = new VertexBuffer(device, SceneRenderer.BillboardVertex.Declaration, 4, BufferUsage.WriteOnly);
            _screenQuad.SetData(new SceneRenderer.BillboardVertex[]
            {
                new(new Vector3(-1f, -1f, 0f), Vector3.Zero), new(new Vector3(-1f, 1f, 0f), Vector3.Zero),
                new(new Vector3(1f, -1f, 0f), Vector3.Zero), new(new Vector3(1f, 1f, 0f), Vector3.Zero),
            });
        }

        /// <summary>How many heaps the last <see cref="Draw"/> drew.</summary>
        public int Count { get; private set; }

        /// <summary>How far this sky stands as heaps: 1 for cumulus, 0 for a lid, between while one fades into the other.</summary>
        public static float Strength(CloudField clouds)
        {
            float t = MathHelper.Clamp((clouds.CoverageBias - HEAPS_UNTIL_BIAS) / (LID_FROM_BIAS - HEAPS_UNTIL_BIAS), 0f, 1f);
            return 1f - t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Tells the sky shader whether the deck gives way to the heaps this frame (<c>Sky.fx</c>'s <c>CloudVolumeCut</c>): state it
        /// before the sky is drawn, every frame, <paramref name="on"/> false wherever no heaps will be drawn, so a tier or a scene
        /// switched to never inherits a cut deck with nothing in its place.
        /// </summary>
        public void ApplyToSky(Effect skyEffect, CloudField clouds, bool on)
        {
            ReadSky(skyEffect);
            _skyCut?.SetValue(on ? Strength(clouds) : 0f);
        }

        /// <summary>
        /// Places this frame's heaps on the deck's weather, marches them, far ones first, into the display-size layer and lays
        /// it over the target bound now (the scene's): call it right after the sky, before anything that may stand in front of
        /// a cloud. Leaves the target, the depth state and the blend as they were found bound (depth on, alpha blend).
        /// </summary>
        public void Draw(ICamera camera, CloudField clouds, Effect skyEffect)
        {
            Count = Place(camera.Position, clouds, _heaps, _order);
            if (Count == 0) return;

            int width = Math.Max(_device.PresentationParameters.BackBufferWidth, 1);
            int height = Math.Max(_device.PresentationParameters.BackBufferHeight, 1);
            if (_layer == null || _layer.Width != width || _layer.Height != height)
            {
                _layer?.Dispose();
                _layer = new RenderTarget2D(_device, width, height, false, SurfaceFormat.HdrBlendable, DepthFormat.None, 0,
                    RenderTargetUsage.DiscardContents);
            }

            ReadSky(skyEffect);
            _view.SetValue(camera.View);
            _projection.SetValue(camera.Projection);
            _cameraPosition.SetValue(camera.Position);
            _sunDirection.SetValue(_skySunDirection.GetValueVector3());
            _sunColor.SetValue(_skySunColor.GetValueVector3());
            _shadowColor.SetValue(_skyShadowColor.GetValueVector3());
            _time.SetValue(clouds.Time);

            //Into the layer: cleared to nothing, the heaps composited over each other far first (premultiplied, so the layer's
            //alpha is how much of the sky they hide), no depth to test against
            int bound = _device.RenderTargetCount;
            _device.GetRenderTargets(_bound);
            _device.SetRenderTarget(_layer);
            _device.Clear(Color.Transparent);

            _instances.SetData(_heaps, 0, Count, SetDataOptions.Discard);
            _device.BlendState = BlendState.AlphaBlend;
            _device.DepthStencilState = DepthStencilState.None;
            _device.RasterizerState = RasterizerState.CullCounterClockwise;
            InstanceBinding.Set(_device, _box, 0, _instances, 0);
            _device.Indices = _boxIndices;
            _effect.CurrentTechnique = _march;
            _effect.CurrentTechnique.Passes[0].Apply();
            _device.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, 12, Count);

            //Back to what was bound (the scene's target; preserved, #541), and the layer over it
            if (bound == 0) _device.SetRenderTarget(null);
            else if (bound == 1) _device.SetRenderTarget(_bound[0].RenderTarget as RenderTarget2D);
            else _device.SetRenderTargets(_bound[..bound]);
            Array.Clear(_bound);

            _layerParam.SetValue(_layer);
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SetVertexBuffer(_screenQuad);
            _effect.CurrentTechnique = _composite;
            _effect.CurrentTechnique.Passes[0].Apply();
            _device.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
            _layerParam.SetValue((Texture2D)null);

            _device.DepthStencilState = DepthStencilState.Default;
            _device.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        private void ReadSky(Effect skyEffect)
        {
            if (ReferenceEquals(skyEffect, _skyFor)) return;
            _skyFor = skyEffect;
            _skySunDirection = skyEffect.Parameters["SunDirection"];
            _skySunColor = skyEffect.Parameters["CloudSunColor"];
            _skyShadowColor = skyEffect.Parameters["CloudShadowColor"];
            _skyCut = skyEffect.Parameters["CloudVolumeCut"];
            _skyCutNear = skyEffect.Parameters["CloudVolumeNear"];
            _skyCutFar = skyEffect.Parameters["CloudVolumeFar"];
            _skyCutNear?.SetValue(FADE_NEAR);
            _skyCutFar?.SetValue(FADE_FAR);
        }

        /// <summary>
        /// The heaps within <see cref="REACH"/> of <paramref name="lens"/>, written into <paramref name="heaps"/> far first
        /// (<paramref name="order"/> is the sort's scratch, as long); how many, at most their length.
        /// </summary>
        internal static int Place(Vector3 lens, CloudField clouds, Heap[] heaps, float[] order)
        {
            float strength = Strength(clouds);
            if (strength <= 0f) return 0;
            float cell = CELL_SHARE / MathF.Max(clouds.Scale, 1e-5f);
            Vector2 drift = clouds.Wind * clouds.Time;
            //The lattice is in the weather's own frame, so it drifts with the deck: a heap's place in the world is its
            //lattice point less the drift
            Vector2 centre = new Vector2(lens.X, lens.Z) + drift;
            int reach = (int)MathF.Ceiling(REACH / cell) + 1;
            int x0 = (int)MathF.Floor(centre.X / cell), z0 = (int)MathF.Floor(centre.Y / cell);

            int count = 0;
            for (int i = x0 - reach; i <= x0 + reach && count < heaps.Length; i++)
                for (int j = z0 - reach; j <= z0 + reach && count < heaps.Length; j++)
                {
                    uint hash = Hash(i, j);
                    Vector2 at = (new Vector2(i + 0.5f, j + 0.5f) + 0.6f * new Vector2(Unit(hash, 0) - 0.5f, Unit(hash, 8) - 0.5f)) * cell - drift;
                    float dx = at.X - lens.X, dz = at.Y - lens.Z;
                    float distance = MathF.Sqrt(dx * dx + dz * dz);
                    if (distance > REACH) continue;

                    float cover = clouds.Cover(at);
                    if (cover < COVER_MIN) continue;

                    float radius = cell * (RADIUS_MIN + RADIUS_GROWTH * cover) * (0.85f + 0.3f * Unit(hash, 16));
                    float height = radius * (HEIGHT_MIN + HEIGHT_GROWTH * cover) * (0.8f + 0.4f * Unit(hash, 24));
                    float fadeIn = MathHelper.Clamp((cover - COVER_MIN) / COVER_FADE, 0f, 1f);
                    heaps[count] = new Heap
                    {
                        Cloud = new Vector4(at.X, clouds.PlaneY - 0.02f * height, at.Y, radius),
                        Shape = new Vector4(height, Unit(hash * 2654435761u, 4) * 10f, 1f, fadeIn * fadeIn * (3f - 2f * fadeIn) * strength),
                    };
                    //Far first: the blend composites each heap over the ones behind it
                    order[count] = -distance;
                    count++;
                }

            Array.Sort(order, heaps, 0, count);
            return count;
        }

        private static uint Hash(int x, int z)
        {
            uint h = (uint)x * 73856093u ^ (uint)z * 19349663u ^ 0x9E3779B9u;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }

        private static float Unit(uint hash, int shift) => ((hash >> shift) & 0xFF) / 255f;

        public void Dispose()
        {
            _box?.Dispose();
            _boxIndices?.Dispose();
            _instances?.Dispose();
            _noise?.Dispose();
            _screenQuad?.Dispose();
            _layer?.Dispose();
        }
    }
}
