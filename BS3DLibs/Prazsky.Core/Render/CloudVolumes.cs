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
    /// only where <see cref="CloudField.Cover"/> says there is cloud, so as the second octave evolves the field a heap grows,
    /// thins and is gone. They stand on the deck's cover but do not fill it: the lanes between them are cover the deck still
    /// shades the ground with.
    /// </para>
    /// <para>
    /// <b>A field, not a grid of buns</b> (#1145). Each cell's heap draws its size from a spread (many small, a few big; a
    /// dense patch more big ones), is drawn out down the wind and, if big, may tower and lean down it; where the weather
    /// organises them the heaps line up in streets along the wind, and elsewhere a dense patch gathers them in clumps with
    /// open sky between. Then each is no bigger than the room its neighbours leave it, the bigger keeping most of it, so a
    /// big heap stands among small ones and the march stays at about one heap a pixel.
    /// </para>
    /// </summary>
    public sealed class CloudVolumes : IDisposable
    {
        //The lattice: a heap a cell at most, the cell this share of the weather layer's feature size (1 / Scale); how far out
        //heaps stand (past the camera's far plane, DEFAULT_FAR_PLANE_DISTANCE 2000: the layer has a far plane of its own,
        //FarProjection) and where they dissolve into the sky
        private const float CELL_SHARE = 0.5f;
        private const float REACH = 4000f, FADE_NEAR = 2600f, FADE_FAR = 3900f;
        //The layer's own far plane (FarProjection): anywhere past FADE_FAR clips nothing that shows. The shader dissolves a
        //heap by where its march starts, which is never nearer the lens than the face of the box the pixel is drawn for, so a
        //face past the plane would start its march where the heap is dissolved whole (CloudVolumeTests)
        internal const float LAYER_FAR = REACH + 500f;
        private const int MAX_HEAPS = 900;

        //A heap stands where the cover is at least COVER_MIN, fading in over COVER_FADE; from there to COVER_FULL the patch
        //grows from thin (only small heaps) to dense (the whole spread)
        private const float COVER_MIN = 0.12f, COVER_FADE = 0.15f, COVER_FULL = 0.75f;

        //THE SPREAD (#1145; the owner: "similarly sized little buns laid out on a grid"). A heap's radius across its axis, a
        //share of the cell, drawn from SIZE_SMALL to SIZE_BIG and skewed towards the small end, as a real field is: many
        //small heaps and fragments, a few big ones. A thin patch skews hard (SKEW_THIN) and draws only from the first
        //THIN_SPREAD of the range; a dense one draws towards the big end (SKEW_DENSE) and from no smaller than SIZE_DENSE,
        //so a broken sky is big heaps with lanes between them, not a scatter of small ones.
        private const float SIZE_SMALL = 0.14f, SIZE_DENSE = 0.32f, SIZE_BIG = 0.8f, SKEW_THIN = 1.9f, SKEW_DENSE = 0.6f, THIN_SPREAD = 0.6f;
        //How much longer than wide a heap is along its axis, which lies within AXIS_SPREAD radians of down the wind (the way
        //the heaps travel, minus CloudField.Wind): a real one is drawn out down the wind
        private const float STRETCH_MIN = 1.0f, STRETCH_MAX = 1.6f, AXIS_SPREAD = 0.6f;
        //Its height against its radius: a small heap flat, a big one domed, and TOWER_SHARE of the big ones (from TOWER_FROM
        //of SIZE_BIG, fully by TOWER_FULL) towers, narrower and leaning down the wind, its top ahead of its base the way it
        //travels, as the wind aloft is stronger (LEAN_TOWER radii at the top; LEAN_ANY any heap)
        private const float ASPECT_FLAT = 0.75f, ASPECT_BIG = 1.5f, ASPECT_TOWER = 2.4f, TOWER_SHARE = 0.3f, TOWER_FROM = 0.4f, TOWER_FULL = 0.8f;
        private const float STRETCH_TOWER = 1.1f, LEAN_ANY = 0.08f, LEAN_TOWER = 0.3f;

        //CLOUD STREETS: where the weather organises them (a slow field, ORGANISED_SCALE of the weather's frequency, over
        //ORGANISED_FROM), the heaps line up in rows along the wind, STREET_SPACING cells apart, bent by STREET_BEND of a
        //spacing: a heap in a row grows to STREET_CREST of its draw, one between rows shrinks to STREET_GAP, so open lanes run
        //between the rows. The rows are the weather's own frame, so they drift with the heaps.
        private const float STREET_SPACING = 2.6f, STREET_BEND = 0.35f, STREET_GAP = 0.3f, STREET_CREST = 1.15f;
        private const float ORGANISED_SCALE = 0.3f, ORGANISED_FROM = -0.05f, ORGANISED_FULL = 0.3f;
        //CLUMPS: elsewhere a patch gathers its heaps in clumps with open sky between them, the more the denser it is (one heap
        //a jittered cell, all kept, spaced a broken sky as evenly as a grid). A second slow field of the weather (CLUMP_SCALE
        //of its frequency) says where a clump stands, from CLUMP_FROM to CLUMP_FULL: in one, a heap draws its size as the
        //densest patch does and grows to CLUMP_CREST of its draw, the room packing the clump's heaps against each other; out
        //of one it shrinks to CLUMP_GAP and is squeezed out (SQUEEZED_TO), leaving open sky and a fragment or two at a
        //clump's edge. A thin patch keeps the cover's own patchiness. The weather's own frame, as the streets are.
        private const float CLUMP_SCALE = 0.8f, CLUMP_FROM = -0.15f, CLUMP_FULL = 0.1f, CLUMP_GAP = 0.05f, CLUMP_CREST = 1.3f;

        //THE ROOM: a heap is no bigger than the room its neighbours leave it, so a big one stands with small ones squeezed
        //round it and the march stays at about one heap a pixel. Two outlines may overlap by ROOM_OVERLAP of the distance
        //between their centres, their billows meeting; the room is shared out by the square of size (twice the size, four
        //times the share), so the bigger keeps most of it, and every term is continuous in the cover, so nothing pops. The
        //neighbours are the cells ROOM_CELLS round; a heap squeezed under SQUEEZED_TO of the cell fades out by SQUEEZED_FROM.
        //⚠ Not much more: the layer composites whole heaps far first by their centres, so where two heaps stand in each
        //other the farther one's flank is laid over the nearer one's, and at 0.4 the heaps overhead read as columns of smoke.
        private const float ROOM_OVERLAP = 0.15f;
        private const int ROOM_CELLS = 2;
        private const float SQUEEZED_FROM = 0.03f, SQUEEZED_TO = 0.06f;

        //Which skies stand as heaps: the weather's coverage bias, below which a sky is cumulus (Scattered 0.02, Broken 0.30) and
        //above which it is a lid (Storm 0.72, Overcast 0.85), the volumes and the deck's cut fading between them as a change of
        //sky does (WeatherLook.Lerp), so nothing pops
        private const float HEAPS_UNTIL_BIAS = 0.45f, LID_FROM_BIAS = 0.65f;

        //The heaps' noise, baked once (CloudVolume.fx's NOISE_PERIOD)
        internal const int NOISE_PERIOD = 8, NOISE_TEXELS_PER_UNIT = 4, NOISE_SEED = 989;
        internal const int CAPACITY = MAX_HEAPS;
        internal const float COVER_THRESHOLD = COVER_MIN, REACH_DISTANCE = REACH, DISSOLVED_BY = FADE_FAR;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Heap : IVertexType
        {
            public Vector4 Cloud;   //the base's centre (x, height, z) and the radius across its axis
            public Vector4 Shape;   //the height, the seed, the density and how far in it has faded
            public Vector4 Form;    //its axis (x, z, a unit vector), how much longer it is along it, how far it leans along it

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
                new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
                new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3));

            readonly VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }

        private readonly GraphicsDevice _device;
        private readonly Effect _effect;
        private readonly EffectParameter _view, _projection, _cameraPosition, _sunDirection, _sunColor, _shadowColor, _time;
        private readonly VertexBuffer _box;
        private readonly IndexBuffer _boxIndices;
        private readonly DynamicVertexBuffer _instances;
        private readonly Texture3D _noise;
        //The same noise on the CPU, for the lens's sun ray (SunPast)
        private readonly float[] _noiseTexels;
        private readonly Heap[] _heaps = new Heap[MAX_HEAPS];
        private readonly float[] _order = new float[MAX_HEAPS];

        //THE LAYER: the heaps are marched into a target the display's own size, not the supersampled scene's (a cloud is soft,
        //and four samples a pixel only march one soft edge four times over: a quarter of the cost on High), before the scene
        //target is bound (March), then laid over the sky in the scene target wherever the sky is (Composite, SkyPass: behind
        //the opaque scene at the far plane, or depth off over the clear where the dome is drawn first). Never into the sky's
        //own display-resolution target (SkyPass.Prepare holds why). Covered by whatever stands in front of the sky, as the
        //deck is. ⚠ Marched into the scene target itself after the ground, depth-tested, a broken sky cost +3.2 ms.
        private readonly EffectTechnique _march, _composite;
        private readonly EffectParameter _layerParam;
        private readonly VertexBuffer _screenQuad;
        private RenderTarget2D _layer;

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
            _noiseTexels = ProxyVolumes.GradientNoiseTexels(NOISE_PERIOD, NOISE_TEXELS_PER_UNIT, NOISE_SEED);
            _noise.SetData(_noiseTexels);
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

        /// <summary>How many heaps the last <see cref="March"/> marched: <see cref="Composite"/> has nothing to lay while it is 0.</summary>
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
        /// switched to never inherits a cut deck with nothing in its place. The field is told the same
        /// (<see cref="CloudField.SunHeaps"/>, #1172), the sun seen whole until <see cref="March"/> says what the heaps hide of it.
        /// </summary>
        public void ApplyToSky(Effect skyEffect, CloudField clouds, bool on)
        {
            ReadSky(skyEffect);
            float cut = on ? Strength(clouds) : 0f;
            _skyCut?.SetValue(cut);
            clouds.SetSunHeaps(cut, 1f);
        }

        /// <summary>
        /// Places this frame's heaps on the deck's weather and marches them, far ones first, into the display-size layer, which
        /// it leaves bound: call it before the scene target is bound (a target is bound once a frame), with this frame's sky
        /// stated on <paramref name="skyEffect"/> (<see cref="CloudField.ApplyTo"/>), whose cloud light it reads. Then
        /// <see cref="Composite"/> lays the layer over the sky. Draws nothing, and binds nothing, where no heap stands
        /// (<see cref="Count"/> 0). Tells <paramref name="clouds"/> how much of the sun's disc the lens sees past the heaps
        /// (<see cref="CloudField.SunPastHeaps"/>, #1172), heaps or none.
        /// </summary>
        public void March(ICamera camera, CloudField clouds, Effect skyEffect)
        {
            ReadSky(skyEffect);
            Count = Place(camera.Position, clouds, _heaps, _order, _lattice);
            clouds.SetSunHeaps(Strength(clouds), SunPast(camera.Position, _skySunDirection.GetValueVector3(), clouds.Time, _heaps, Count, _noiseTexels));
            if (Count == 0) return;

            int width = Math.Max(_device.PresentationParameters.BackBufferWidth, 1);
            int height = Math.Max(_device.PresentationParameters.BackBufferHeight, 1);
            if (_layer == null || _layer.Width != width || _layer.Height != height)
            {
                _layer?.Dispose();
                _layer = new RenderTarget2D(_device, width, height, false, SurfaceFormat.HdrBlendable, DepthFormat.None, 0,
                    RenderTargetUsage.DiscardContents);
            }

            _view.SetValue(camera.View);
            _projection.SetValue(FarProjection(camera.Projection, LAYER_FAR));
            _cameraPosition.SetValue(camera.Position);
            _sunDirection.SetValue(_skySunDirection.GetValueVector3());
            _sunColor.SetValue(_skySunColor.GetValueVector3());
            _shadowColor.SetValue(_skyShadowColor.GetValueVector3());
            _time.SetValue(clouds.Time);

            //Into the layer: cleared to nothing, the heaps composited over each other far first (premultiplied, so the layer's
            //alpha is how much of the sky they hide), no depth to test against
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
            _device.DrawInstanced(PrimitiveType.TriangleList, 0, 0, 12, Count);
        }

        /// <summary>
        /// Lays the layer <see cref="March"/> filled this frame over the target bound now, premultiplied, under the depth state
        /// and the viewport the caller stated (<see cref="SkyPass"/>, always into the scene target: depth-read at the far plane
        /// behind the opaque scene, or depth off over the clear where the dome is drawn first, <see cref="SkyPass.BehindScene"/>
        /// false). A full-screen quad, linear: the layer is the display's size and the target either that or a supersampled
        /// multiple of it. Leaves behind the alpha blend it draws with and the culling off. A no-op
        /// where no heap stands.
        /// </summary>
        public void Composite()
        {
            if (Count == 0 || _layer == null) return;

            _layerParam.SetValue(_layer);
            _device.BlendState = BlendState.AlphaBlend;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SetVertexBuffer(_screenQuad);
            _effect.CurrentTechnique = _composite;
            _effect.CurrentTechnique.Passes[0].Apply();
            _device.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
            _layerParam.SetValue((Texture2D)null);
        }

        /// <summary>
        /// The camera's projection with its far plane moved to <paramref name="far"/>: the layer has no depth buffer, so the
        /// heaps need not stop at the scene's far plane (2000), and the band just over the skyline, where every reference has
        /// small heaps down to the horizon, is past it. The near plane is the camera's own (M43 / M33 of a right-handed
        /// perspective matrix).
        /// </summary>
        internal static Matrix FarProjection(Matrix projection, float far)
        {
            float near = projection.M43 / projection.M33;
            projection.M33 = far / (near - far);
            projection.M43 = near * far / (near - far);
            return projection;
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
        /// What <see cref="Place"/> works in: the candidate heap of every cell round the lens, kept from frame to frame so a
        /// frame allocates nothing (it grows only when a weather with smaller cells needs more of them).
        /// </summary>
        internal sealed class Lattice
        {
            public Vector2[] At = Array.Empty<Vector2>();
            public Vector2[] Axis = Array.Empty<Vector2>();
            public float[] Want = Array.Empty<float>(), Stretch = Array.Empty<float>(), FadeIn = Array.Empty<float>(), Distance = Array.Empty<float>();
            public uint[] Seed = Array.Empty<uint>();

            public void Ensure(int cells)
            {
                if (At.Length >= cells) return;
                At = new Vector2[cells];
                Axis = new Vector2[cells];
                Want = new float[cells];
                Stretch = new float[cells];
                FadeIn = new float[cells];
                Distance = new float[cells];
                Seed = new uint[cells];
            }
        }

        private readonly Lattice _lattice = new();

        /// <summary>
        /// The heaps within <see cref="REACH"/> of <paramref name="lens"/>, written into <paramref name="heaps"/> far first
        /// (<paramref name="order"/> is the sort's scratch, as long); how many, at most their length. Past their length the
        /// nearest are kept. <paramref name="lattice"/> is the scratch the cells are worked in (a fresh one when null).
        /// </summary>
        internal static int Place(Vector3 lens, CloudField clouds, Heap[] heaps, float[] order, Lattice lattice = null)
        {
            float strength = Strength(clouds);
            if (strength <= 0f) return 0;
            lattice ??= new Lattice();
            float cell = CELL_SHARE / MathF.Max(clouds.Scale, 1e-5f);
            Vector2 drift = clouds.Wind * clouds.Time;
            //The lattice is in the weather's own frame, so it drifts with the deck: a heap's place in the world is its
            //lattice point less the drift
            Vector2 centre = new Vector2(lens.X, lens.Z) + drift;
            int reach = (int)MathF.Ceiling(REACH / cell) + 1 + ROOM_CELLS;
            int side = 2 * reach + 1;
            lattice.Ensure(side * side);
            int x0 = (int)MathF.Floor(centre.X / cell) - reach, z0 = (int)MathF.Floor(centre.Y / cell) - reach;
            Vector2 wind = clouds.Wind.LengthSquared() > 1e-8f ? Vector2.Normalize(clouds.Wind) : Vector2.UnitX;
            //Down the wind is the way the heaps travel, minus the wind vector: the deck reads its noise at the world plus the
            //drift, so its cloud moves the other way. A heap's axis points that way, so its lean does (Form.w is along the axis)
            float travelAngle = MathF.Atan2(-wind.Y, -wind.X);

            //The candidates: every cell's heap as big as it would like to be, its cover and its own draw deciding
            for (int i = 0; i < side; i++)
                for (int j = 0; j < side; j++)
                {
                    int k = i * side + j;
                    uint hash = Hash(x0 + i, z0 + j);
                    Vector2 at = (new Vector2(x0 + i + 0.5f, z0 + j + 0.5f) + 0.7f * new Vector2(Unit(hash, 0) - 0.5f, Unit(hash, 8) - 0.5f)) * cell - drift;
                    lattice.At[k] = at;
                    lattice.Want[k] = 0f;
                    float distance = Vector2.Distance(at, new Vector2(lens.X, lens.Z));
                    lattice.Distance[k] = distance;
                    if (distance > REACH + ROOM_CELLS * cell) continue;

                    float cover = clouds.Cover(at);
                    if (cover < COVER_MIN) continue;

                    uint second = Hash((int)hash, (int)(hash >> 7));
                    float grow = SmoothStep((cover - COVER_MIN) / (COVER_FULL - COVER_MIN));
                    //Where it stands in the field's arrangement, in the weather's own frame: on a street where the weather
                    //organises the heaps, in a clump or the open sky between clumps elsewhere, the more the denser the patch
                    Vector2 weatherAt = at + drift;
                    float organised = SmoothStep((ShaderMath.Noise(weatherAt * (clouds.Scale * ORGANISED_SCALE) + new Vector2(57.3f, 11.9f)) - ORGANISED_FROM)
                        / (ORGANISED_FULL - ORGANISED_FROM));
                    float clustered = (1f - organised) * grow;
                    float clump = clustered > 0f ? SmoothStep((ShaderMath.Noise(weatherAt * (clouds.Scale * CLUMP_SCALE) + new Vector2(-71.9f, 5.3f)) - CLUMP_FROM)
                        / (CLUMP_FULL - CLUMP_FROM)) : 0f;
                    //A clump draws its sizes as the densest patch does
                    float spread = MathHelper.Lerp(grow, MathF.Max(grow, clump), clustered);
                    float draw = MathF.Pow(Unit(hash, 16), MathHelper.Lerp(SKEW_THIN, SKEW_DENSE, spread)) * MathHelper.Lerp(THIN_SPREAD, 1f, spread);
                    lattice.Want[k] = cell * MathHelper.Lerp(MathHelper.Lerp(SIZE_SMALL, SIZE_DENSE, spread), SIZE_BIG, draw)
                        * MathHelper.Lerp(1f, MathHelper.Lerp(CLUMP_GAP, CLUMP_CREST, clump), clustered) * Street(weatherAt, wind, cell, clouds.Scale, organised);
                    lattice.FadeIn[k] = SmoothStep((cover - COVER_MIN) / COVER_FADE);
                    lattice.Stretch[k] = MathHelper.Lerp(STRETCH_MIN, STRETCH_MAX, Unit(second, 0));
                    float angle = travelAngle + (Unit(second, 8) - 0.5f) * 2f * AXIS_SPREAD;
                    lattice.Axis[k] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    lattice.Seed[k] = hash;
                }

            //Each heap as big as the room its neighbours leave it, then shaped by the size it has
            int count = 0;
            for (int i = ROOM_CELLS; i < side - ROOM_CELLS; i++)
                for (int j = ROOM_CELLS; j < side - ROOM_CELLS; j++)
                {
                    int k = i * side + j;
                    float want = lattice.Want[k];
                    if (want <= 0f || lattice.Distance[k] > REACH) continue;

                    Vector2 at = lattice.At[k], axis = lattice.Axis[k];
                    float stretch = lattice.Stretch[k], fadeIn = lattice.FadeIn[k];
                    float radius = want, ownMost = want * fadeIn * fadeIn;
                    for (int di = -ROOM_CELLS; di <= ROOM_CELLS; di++)
                        for (int dj = -ROOM_CELLS; dj <= ROOM_CELLS; dj++)
                        {
                            int n = k + di * side + dj;
                            //A neighbour not faded in at all leaves the room whole (the lerp below at 0)
                            float fadeN = lattice.FadeIn[n];
                            if (n == k || lattice.Want[n] <= 0f || fadeN <= 0f) continue;
                            Vector2 toward = lattice.At[n] - at;
                            float apart = toward.Length();
                            if (apart < 1e-3f) continue;
                            float roomy = apart * (1f + ROOM_OVERLAP);
                            //Skipped where the two stand too far apart for the neighbour to squeeze this one however they lie.
                            //Exact: the room below is no less than `want` wherever want * fadeIn^2 * own * (roomy - want * own)
                            //is at least the square of the neighbour's reach at its longest, and that term is concave in own,
                            //so it is least at one of own's two ends, 1 and the stretch
                            float theirsMost = lattice.Want[n] * lattice.Stretch[n] * fadeN;
                            if (MathF.Min(ownMost * (roomy - want), ownMost * stretch * (roomy - want * stretch)) >= theirsMost * theirsMost) continue;
                            toward /= apart;
                            //Each one's reach towards the other: its radius across, stretched by how near its axis the line between them runs
                            float own = Extent(axis, stretch, toward), theirs = Extent(lattice.Axis[n], lattice.Stretch[n], toward);
                            //Shared out by the square of each one's reach: the bigger keeps most of the room
                            float mine = want * own * fadeIn, other = lattice.Want[n] * theirs * fadeN;
                            mine *= mine;
                            other *= other;
                            float share = mine + other > 1e-20f ? mine / (mine + other) : 0.5f;
                            //A neighbour only fading in makes room for itself as it does, or a big heap shrank all at once
                            //the moment the cover under its neighbour crossed COVER_MIN
                            radius = MathF.Min(radius, MathHelper.Lerp(want, roomy * share / own, fadeN));
                        }

                    float fade = fadeIn * SmoothStep((radius / cell - SQUEEZED_FROM) / (SQUEEZED_TO - SQUEEZED_FROM)) * strength;
                    if (fade < 1e-3f) continue;

                    uint hash = lattice.Seed[k], third = Hash((int)(hash >> 3), (int)hash ^ 0x5bd1e995);
                    float size = radius / (SIZE_BIG * cell);
                    float tower = Unit(third, 0) < TOWER_SHARE ? SmoothStep((size - TOWER_FROM) / (TOWER_FULL - TOWER_FROM)) : 0f;
                    float aspect = MathHelper.Lerp(ASPECT_FLAT, ASPECT_BIG, SmoothStep((size - 0.1f) / 0.8f)) * (0.85f + 0.3f * Unit(third, 8));
                    aspect = MathHelper.Lerp(aspect, ASPECT_TOWER, tower);
                    float height = radius * aspect;
                    float lean = MathHelper.Lerp(LEAN_ANY, LEAN_TOWER, tower) * Unit(third, 16);

                    Heap heap = new()
                    {
                        Cloud = new Vector4(at.X, clouds.PlaneY - 0.02f * height, at.Y, radius),
                        Shape = new Vector4(height, Unit(hash * 2654435761u, 4) * 10f, 1f, fade),
                        //A tower is narrower, never longer: the room was shared out for the stretch it drew
                        Form = new Vector4(axis.X, axis.Y, MathF.Min(stretch, MathHelper.Lerp(stretch, STRETCH_TOWER, tower)), lean),
                    };
                    //Far first: the blend composites each heap over the ones behind it
                    float key = -lattice.Distance[k];
                    if (count < heaps.Length)
                    {
                        heaps[count] = heap;
                        order[count] = key;
                        count++;
                    }
                    else
                    {
                        //Full: the nearest are kept (the farthest is the highest key's opposite, the lowest key)
                        int farthest = 0;
                        for (int f = 1; f < count; f++)
                            if (order[f] < order[farthest]) farthest = f;
                        if (key > order[farthest])
                        {
                            heaps[farthest] = heap;
                            order[farthest] = key;
                        }
                    }
                }

            Array.Sort(order, heaps, 0, count);
            return count;
        }

        //How much of its draw a heap at this point of the weather's own frame keeps for the street it stands in, as far as
        //the weather organises the heaps there: more on a row, less between rows
        private static float Street(Vector2 weatherAt, Vector2 wind, float cell, float scale, float organised)
        {
            if (organised <= 0f) return 1f;
            float across = Vector2.Dot(weatherAt, new Vector2(-wind.Y, wind.X)) / (STREET_SPACING * cell);
            float bend = STREET_BEND * ShaderMath.Noise(weatherAt * scale + new Vector2(-23.1f, 41.7f));
            float row = 0.5f + 0.5f * MathF.Cos(MathHelper.TwoPi * (across + bend));
            return MathHelper.Lerp(1f, MathHelper.Lerp(STREET_GAP, STREET_CREST, row), organised);
        }

        //THE SUN PAST THE HEAPS (#1172). On High and Ultra the deck near the lens gives way to the heaps (ApplyToSky), so the
        //sun's disc the sky draws is hidden by them alone, and Sea.fx's glint, which the owner asked to agree with the sun,
        //takes what the lens sees of the disc past them. That is one ray, the lens's own sun ray, marched here as
        //CloudVolume.fx marches a pixel's ray through each heap - its bound, its steps, its density with the billows of the
        //same baked noise, its fade - less the per-pixel jitter (each step at its middle). Its figures are the shader's own,
        //named as there (CloudVolumeTests reads them in the shader): keep the two in step.
        internal const int CLOUD_STEPS = 10, CLOUD_STEPS_FAR = 5;
        internal const float STEPS_FAR_FROM = 1500f, STEPS_FAR_AT = 3000f;
        internal const float CLOUD_SIGMA = 0.09f, CLOUD_WAIST = 0.3f, CLOUD_BASE_WIDTH = 0.78f;
        internal const float CLOUD_BILLOW = 0.45f, BILLOW_RADIUS = 90f, BILLOW_GROWTH = 0.6f, CLOUD_EDGE = 10f;
        internal const float CLOUD_BILLOW_PUSH = 0.8f, CLOUD_PUFF_BIAS = 0.35f, CLOUD_PUFF_FROM = 0.35f, CLOUD_PUFF_TO = 0.7f;
        internal const float CLOUD_RIM_PUSH = 0.25f, CLOUD_MARGIN = 0.45f, CLOUD_PUSH_FADE_FROM = 0.9f;

        /// <summary>
        /// How much of the sun's disc <paramref name="lens"/> sees past the first <paramref name="count"/> of
        /// <paramref name="heaps"/>, 0 to 1: the sky behind the heaps' layer shows through each heap's alpha in turn, so the
        /// product of what each leaves. <paramref name="sun"/> points at the sun; <paramref name="time"/> is the clouds' clock
        /// (the billows roll with it); <paramref name="noise"/> the baked noise the shader samples. 1 with no heaps.
        /// </summary>
        internal static float SunPast(Vector3 lens, Vector3 sun, float time, Heap[] heaps, int count, float[] noise)
        {
            float seen = 1f;
            for (int i = 0; i < count && seen > 1e-4f; i++)
                seen *= 1f - HeapAlpha(lens, sun, time, heaps[i], noise);
            return seen;
        }

        //CloudVolumePS's alpha for one ray through one heap: the bound (the cap's ellipsoid grown by the margin, over the
        //base plane), the march, the fade by distance and by how far the heap has faded in. 0 for a ray the bound misses.
        private static float HeapAlpha(Vector3 lens, Vector3 direction, float time, in Heap heap, float[] noise)
        {
            Vector4 cloud = heap.Cloud, shape = heap.Shape, form = heap.Form;
            float radius = cloud.W;
            if (radius <= 0f || shape.W <= 0f) return 0f;
            float aspect = shape.X / radius;
            float billowSize = CLOUD_BILLOW * MathF.Pow(MathF.Sqrt(radius * shape.X), BILLOW_GROWTH) * MathF.Pow(BILLOW_RADIUS, 1f - BILLOW_GROWTH);
            float edge = CLOUD_EDGE / radius;

            Vector3 o = HeapLocalDirection(lens - new Vector3(cloud.X, cloud.Y, cloud.Z), cloud, form, aspect);
            Vector3 d = HeapLocalDirection(direction, cloud, form, aspect);
            float dy = MathF.Abs(d.Y) > 1e-9f ? d.Y : 1e-9f;
            float reach = 1f + CLOUD_MARGIN;
            float waistY = CLOUD_WAIST * aspect, capY = (1f - CLOUD_WAIST) * aspect + CLOUD_MARGIN;

            float squash = reach / capY;
            Vector3 oe = new(o.X, (o.Y - waistY) * squash, o.Z);
            Vector3 de = new(d.X, d.Y * squash, d.Z);
            float ea = MathF.Max(Vector3.Dot(de, de), 1e-12f), eb = Vector3.Dot(oe, de);
            float ed = eb * eb - ea * (Vector3.Dot(oe, oe) - reach * reach);
            if (ed < 0f) return 0f;
            float eroot = MathF.Sqrt(ed);
            float ya = -o.Y / dy, yc = (waistY + capY - o.Y) / dy;
            float t0 = MathF.Max(MathF.Max((-eb - eroot) / ea, MathF.Min(ya, yc)), 0f);
            float t1 = MathF.Min((-eb + eroot) / ea, MathF.Max(ya, yc));
            if (t1 - t0 <= 0f) return 0f;

            int steps = (int)MathF.Round(MathHelper.Lerp(CLOUD_STEPS, CLOUD_STEPS_FAR, MathHelper.Clamp((t0 - STEPS_FAR_FROM) / (STEPS_FAR_AT - STEPS_FAR_FROM), 0f, 1f)));
            float dt = (t1 - t0) / steps;
            float transmit = 1f;
            for (int i = 0; i < steps; i++)
            {
                float t = t0 + (i + 0.5f) * dt;
                float density = HeapDensity(o + d * t, lens + direction * t, shape, aspect, billowSize, edge, time, noise);
                if (density > 0.02f) transmit *= MathF.Exp(-CLOUD_SIGMA * density * dt);
                if (transmit < 0.03f) break;
            }

            float fade = (1f - HlslSmoothStep(FADE_NEAR, FADE_FAR, t0)) * shape.W;
            float alpha = (1f - transmit) * fade;
            //The shader clips what would hide less than this
            return alpha > 0.002f ? alpha : 0f;
        }

        //CloudVolume.fx's HeapLocalDirection: a direction in the heap's own measure (radii, x along its axis, its lean out)
        private static Vector3 HeapLocalDirection(Vector3 v, Vector4 cloud, Vector4 form, float aspect)
        {
            float along = v.X * form.X + v.Z * form.Y - form.W * v.Y / aspect;
            float across = -v.X * form.Y + v.Z * form.X;
            return new Vector3(along / form.Z, v.Y, across) / cloud.W;
        }

        //CloudVolume.fx's HeapInside: how far inside the outline, in radii; h how high up the heap, e how far out to the margin
        private static float HeapInside(Vector3 l, float aspect, out float h, out float e)
        {
            float waistY = CLOUD_WAIST * aspect, capY = (1f - CLOUD_WAIST) * aspect;
            h = l.Y / aspect;
            float waist = MathHelper.Lerp(CLOUD_BASE_WIDTH, 1f, h / CLOUD_WAIST);
            Vector3 c = new(l.X, l.Y - waistY, l.Z);
            Vector3 axes = new(1f, capY, 1f);
            float k0 = (c / axes).Length(), k1 = MathF.Max((c / (axes * axes)).Length(), 1e-6f);
            e = new Vector3(l.X, c.Y * (1f + CLOUD_MARGIN) / (capY + CLOUD_MARGIN), l.Z).Length() / (1f + CLOUD_MARGIN);
            float acrossAxis = MathF.Sqrt(l.X * l.X + l.Z * l.Z);
            return h < CLOUD_WAIST ? waist - acrossAxis : -k0 * (k0 - 1f) / k1;
        }

        //CloudVolume.fx's HeapDensity with the rim's erosion (fine 1): the outline pushed by the billows
        private static float HeapDensity(Vector3 l, Vector3 p, Vector4 shape, float aspect, float billowSize, float edge, float time, float[] noise)
        {
            float inside = HeapInside(l, aspect, out float h, out float e);
            if (!(e < 1f && h > 0f)) return 0f;

            Vector3 q = p / billowSize + shape.Y * new Vector3(13.7f, 5.3f, 9.1f) + new Vector3(0f, -time * 0.004f, 0f);
            float n = VolumeNoise(noise, q);
            float billow = MathHelper.Lerp(n, MathF.Abs(n) * 2f - CLOUD_PUFF_BIAS * MathHelper.Clamp(1f - inside * 3f, 0f, 1f),
                HlslSmoothStep(CLOUD_PUFF_FROM, CLOUD_PUFF_TO, h));
            float rim = VolumeNoise(noise, q * 3.1f + new Vector3(11.3f));
            float push = 1f - HlslSmoothStep(CLOUD_PUSH_FADE_FROM, 1f, e);
            float body = inside + push * (CLOUD_BILLOW_PUSH * billow + CLOUD_RIM_PUSH * rim * MathHelper.Clamp(1f - inside * 1.5f, 0f, 1f));
            body *= HlslSmoothStep(0f, 0.04f, h);
            return MathHelper.Clamp(body / edge, 0f, 1f) * shape.Z;
        }

        //CloudVolume.fx's VolumeNoise: the baked texels read as the sampler reads them, at p / NOISE_PERIOD, wrapped, trilinear
        //between texel centres (texel i's centre stands at (i + 0.5) / side)
        internal static float VolumeNoise(float[] texels, Vector3 p)
        {
            const int side = NOISE_PERIOD * NOISE_TEXELS_PER_UNIT;
            float x = p.X * NOISE_TEXELS_PER_UNIT - 0.5f, y = p.Y * NOISE_TEXELS_PER_UNIT - 0.5f, z = p.Z * NOISE_TEXELS_PER_UNIT - 0.5f;
            float fx = MathF.Floor(x), fy = MathF.Floor(y), fz = MathF.Floor(z);
            float tx = x - fx, ty = y - fy, tz = z - fz;
            int x0 = Wrap((int)fx, side), y0 = Wrap((int)fy, side), z0 = Wrap((int)fz, side);
            int x1 = (x0 + 1) % side, y1 = (y0 + 1) % side, z1 = (z0 + 1) % side;

            float At(int ix, int iy, int iz) => texels[ix + side * (iy + side * iz)];
            float near = MathHelper.Lerp(MathHelper.Lerp(At(x0, y0, z0), At(x1, y0, z0), tx), MathHelper.Lerp(At(x0, y1, z0), At(x1, y1, z0), tx), ty);
            float far = MathHelper.Lerp(MathHelper.Lerp(At(x0, y0, z1), At(x1, y0, z1), tx), MathHelper.Lerp(At(x0, y1, z1), At(x1, y1, z1), tx), ty);
            return MathHelper.Lerp(near, far, tz);
        }

        private static int Wrap(int i, int side) => ((i % side) + side) % side;

        //HLSL's smoothstep, edges and all
        private static float HlslSmoothStep(float from, float to, float x) => SmoothStep((x - from) / (to - from));

        //How far a heap reaches from its centre along a unit direction, in radii across: 1 across its axis, its stretch
        //along it, an ellipse between
        private static float Extent(Vector2 axis, float stretch, Vector2 direction)
        {
            float along = Vector2.Dot(axis, direction);
            float across2 = MathF.Max(1f - along * along, 0f);
            return stretch / MathF.Sqrt(stretch * stretch * across2 + along * along);
        }

        private static float SmoothStep(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
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
