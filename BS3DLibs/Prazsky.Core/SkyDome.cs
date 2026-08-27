using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;
using System.Runtime.InteropServices;

namespace Prazsky.Core
{
    /// <summary>
    /// The sky: one procedurally built dome mesh and a stored palette per sky (#113). The first eighteen
    /// skies used to be eighteen .dae models built by three content pipelines; they were one shared
    /// geometry that only ever differed in vertex colour, so the geometry now lives here once
    /// (<c>SkyDome.Data.cs</c>, captured byte-for-byte from the built models) and a sky is just a palette
    /// entry — which is what let the nineteenth (#277's Mars sky) arrive as pure data, no <c>.dae</c>
    /// anywhere, coloured as a function of the very same captured geometry (see <c>PALETTES</c>'s comment).
    /// </summary>
    /// <remarks>
    /// <b>What is drawn is no longer the capture.</b> Sixteen latitude rings, with consecutive rings sharing
    /// a colour, made the gradient a flat plate joined to the next by a ramp — and a slope that goes zero,
    /// steep, zero is a Mach band, which photographed as a straight horizontal edge across every sky in the
    /// game. So the dome is generated finely (<see cref="BuildDrawnDome"/>) and the palette resampled into a
    /// smooth ramp (<see cref="BuildRamp"/>).
    /// <para>
    /// The capture stays exactly where it was and stays the authority on what a sky <i>is</i>:
    /// <see cref="ZenithColor"/> and <see cref="HorizonColor"/> are averages over its 92 entries in its own
    /// order, so the light rig is bit-for-bit what it was. What else is unchanged: the vertex layout, the
    /// winding (measured off the capture and reproduced by the generator), the sRGB palette the rig decodes,
    /// the sRGB-to-linear buffer rewrite, and both draw paths.
    /// </para>
    /// </remarks>
    public partial class SkyDome : IDisposable
    {
        /// <summary>How many skies there are — the palette table's length, counted 1-based by callers.</summary>
        public const byte Count = 19;

        //The CAPTURED mesh: what the eighteen .dae models were, and still the authority on what a sky's colours
        //ARE. Nothing is drawn from it since the banding fix - see BuildDrawnDome - but the palette is read off
        //it vertex by vertex, in this order, because ZenithColor and HorizonColor are averages over it and the
        //whole light rig was tuned against the values that averaging produces.
        private const int VERTEX_COUNT = 92;

        //THE DRAWN DOME, and why it is not the captured one any more. The capture is sixteen latitude rings,
        //each a single flat colour, and CONSECUTIVE RINGS SHARE ONE - measured on every palette: the rings at
        //52.6 and 46.6 degrees are one colour, so are 31.0 and 26.6, so are 10.8 and 9.9, and so on down. The
        //drawn gradient was therefore a flat plate, a ramp, a flat plate, a ramp - and the eye finds every
        //boundary between the two, because the slope goes from zero to steep and back. That is a Mach band, and
        //it photographs as a straight horizontal edge across the sky. It was reported as a faceted wedge and is
        //plainest under a closed deck (#221), where no cloud stands in front of it to distract from it.
        //
        //Two things fix it and both are needed. The plates go, by resampling the palette into a SMOOTH ramp (see
        //BuildRamp); and the rings go from sixteen to this, so no single straight segment spans the 19.8 degrees
        //the capture put across the horizon. Neither touches the capture: the extraction still walks the same 92
        //entries in the same order, so the light rig is bit-for-bit what it was.
        private const int DOME_RINGS = 64;
        private const int DOME_SEGMENTS = 48;
        private const int DRAWN_VERTEX_COUNT = (DOME_RINGS + 1) * (DOME_SEGMENTS + 1);
        private const int TRIANGLE_COUNT = DOME_RINGS * DOME_SEGMENTS * 2;

        //The radius is arbitrary and this is the capture's own, kept so nothing reasoning about the dome's size
        //has to be re-read: the dome is translated to the camera every frame and the sky shader recovers the view
        //ray as the world position minus the camera, so only the DIRECTION to a vertex has ever mattered.
        private const float DOME_RADIUS = 12.5f;

        /// <summary>
        /// The exact layout the content pipeline used to emit for the dome models, kept so the buffer stays
        /// byte-identical to what shipped: position, normal, colour, stride 28.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct DomeVertex : IVertexType
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Color Color;

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
                new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0));

            readonly VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly bool _linearVertexColors;

        //The captured vertices. Only their Y is read now - the extraction thresholds are shares of the mesh's
        //own vertical extent - and only the palette written beside them is what the light rig averages.
        private readonly Vector3[] _capturedPositions = new Vector3[VERTEX_COUNT];

        //What is actually drawn, and the CPU copy the ramp is written into on every dome change; both reused, so
        //a change allocates nothing.
        private readonly DomeVertex[] _vertices = new DomeVertex[DRAWN_VERTEX_COUNT];

        //The gradient as a function of height, resampled from the palette and smoothed - see BuildRamp. Sized so
        //one entry is well under a drawn ring, and reused like everything else here.
        private const int RAMP_STEPS = 256;
        private readonly Vector3[] _ramp = new Vector3[RAMP_STEPS];

        //The heights the ramp spans - the outermost stops, which is a little inside the mesh because a stop
        //sits at the middle of the rings it collapsed. Outside them the ramp is flat, which is what the poles
        //want anyway.
        private float _rampLowest;
        private float _rampHighest;

        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;

        //The Effect == null draw path used to run through the BasicEffects the content pipeline baked from
        //the .dae's white material; this instance replicates that state (unlit vertex colour, verbatim).
        private BasicEffect _basicEffect;

        private Effect _effect;
        private EffectParameter _worldParam;
        private EffectParameter _viewParam;
        private EffectParameter _projectionParam;
        private EffectPass _effectPass;

        private int _domeNumber;

        /// <summary>
        /// Draws the dome with this effect instead of the owned <see cref="BasicEffect"/>, which is how the
        /// Testbed and the game put procedural clouds over the baked gradient. The effect is expected to
        /// declare <c>World</c>, <c>View</c> and <c>Projection</c> (cached here on assignment — the by-name
        /// indexer is a linear scan and this draws every frame); everything else it needs is the caller's
        /// business and is set on the effect directly. Leave it null for the plain gradient — the map editor
        /// does, and does not build a sky shader at all.
        /// </summary>
        public Effect Effect
        {
            get => _effect;
            set
            {
                _effect = value;
                _worldParam = value?.Parameters["World"];
                _viewParam = value?.Parameters["View"];
                _projectionParam = value?.Parameters["Projection"];
                _effectPass = value?.CurrentTechnique.Passes[0];
            }
        }

        /// <summary>
        /// Which sky is up, 1-based like the level format's <c>"sky"</c> byte. Assignment rewrites the
        /// vertex colours from the palette, re-extracts <see cref="ZenithColor"/>/<see cref="HorizonColor"/>
        /// and re-derives <see cref="SunDirection"/> — the contract the hosts' SetSkyDome paths rely on
        /// before re-deriving the light rig. Cheap enough to set redundantly (the game does, on every scene
        /// change): the ramp rebuilt and the drawn dome recoloured and re-uploaded.
        /// </summary>
        public int DomeNumber
        {
            get => _domeNumber;
            set
            {
                if (value < 1 || value > Count)
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"Sky dome numbers run 1..{Count}.");

                _domeNumber = value;

                (float elevation, float azimuth) = SUNS[_domeNumber - 1];
                float elevationRadians = MathHelper.ToRadians(elevation);
                float azimuthRadians = MathHelper.ToRadians(azimuth);
                float horizontal = MathF.Cos(elevationRadians);

                SunDirection = new Vector3(
                    horizontal * MathF.Sin(azimuthRadians),
                    MathF.Sin(elevationRadians),
                    horizontal * MathF.Cos(azimuthRadians));

                ApplyPalette();
            }
        }

        /// <summary>
        /// Towards this sky's sun, normalized by construction — the direction the whole scene is lit and
        /// shadowed along, and where <c>Sky.fx</c> draws the disc. A dome states its own since #220; before
        /// that one constant lit all eighteen, so nothing in a frame said whether it was morning or dusk but
        /// the two palette colours. The figures, and what an elevation and an azimuth each decide, are in
        /// <c>SUNS</c>.
        /// <para>
        /// The light rig reads it in <c>SetSky</c> and is where callers should take it from, not from here:
        /// a scene that replaces the sky has no dome to read a sun off and the rig substitutes its own.
        /// </para>
        /// </summary>
        public Vector3 SunDirection { get; private set; }

        /// <summary>Average vertex colour near the top of the dome, sRGB (see <see cref="HorizonColor"/>).</summary>
        public Vector3 ZenithColor { get; private set; }

        /// <summary>
        /// Average vertex colour near the base of the dome. Both palette colours stay sRGB deliberately:
        /// the caller decodes them to linear on the CPU (ApplySkyLighting, through ColorSpace.SrgbToLinear)
        /// along with the rest of the light rig — extracted after the buffer's own linearization they would
        /// be decoded twice. Only the drawn geometry, which the effects write into the HDR target without
        /// knowing about any of this, is converted (see <see cref="ApplyPalette"/>).
        /// </summary>
        public Vector3 HorizonColor { get; private set; }

        /// <param name="domeNumber">The starting sky, 1..<see cref="Count"/>.</param>
        /// <param name="linearVertexColors">
        /// Converts the dome's vertex colors from sRGB to linear when the buffer is (re)built. Set this when
        /// the caller renders into a linear HDR target and tonemaps at the end of the frame — every current
        /// executable does; leave it off only for a caller drawing straight to an 8-bit back buffer in gamma
        /// space.
        /// </param>
        public SkyDome(GraphicsDevice graphicsDevice, int domeNumber, bool linearVertexColors = false)
        {
            _graphicsDevice = graphicsDevice;
            _linearVertexColors = linearVertexColors;

            for (int i = 0; i < VERTEX_COUNT; i++)
                _capturedPositions[i] = new Vector3(GEOMETRY[i * 6], GEOMETRY[i * 6 + 1], GEOMETRY[i * 6 + 2]);

            BuildDrawnDome(out ushort[] indices);

            _vertexBuffer = new VertexBuffer(graphicsDevice, DomeVertex.Declaration, DRAWN_VERTEX_COUNT, BufferUsage.WriteOnly);
            _indexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices);

            //What the content pipeline used to bake from the .dae's white phong material: unlit vertex
            //colour, verbatim (diffuse (1,1,1) and alpha 1 are BasicEffect's own defaults; lighting and
            //texturing are off by default). The specular the material carried was inert with lighting off.
            _basicEffect = new BasicEffect(graphicsDevice) { VertexColorEnabled = true };

            DomeNumber = domeNumber;
        }

        /// <summary>
        /// Builds the dome that is actually drawn: a UV sphere of <see cref="DOME_RINGS"/> latitude rings by
        /// <see cref="DOME_SEGMENTS"/> longitude segments, positions only - the colours are the palette's and
        /// arrive with every <see cref="ApplyPalette"/>.
        /// <para>
        /// <b>The winding is load-bearing and is the capture's own</b>, measured off it rather than assumed:
        /// all 180 captured triangles have their geometric normal pointing OUTWARD, away from the origin, which
        /// under MonoGame's default <c>CullCounterClockwiseFace</c> is a front face to a camera standing INSIDE
        /// (the viewport flips Y, so a normal pointing at the viewer is the one that gets culled - see "Triangle
        /// winding" in CLAUDE.md). The Testbed and the map editor draw the sky under an inherited rasterizer
        /// state that is frequently that default, so a dome wound the other way is a sky that vanishes in two
        /// executables and not in the third.
        /// </para>
        /// <para>
        /// The seam column is duplicated (<see cref="DOME_SEGMENTS"/> + 1 of them) rather than shared, which
        /// costs one column of vertices and buys a mesh with no wrap-around index arithmetic in it. Nothing
        /// reads the normals - <c>Sky.fx</c> takes POSITION0 and COLOR0, <see cref="BasicEffect"/> draws unlit -
        /// but the layout carries them, so they are filled with the outward direction rather than left at zero.
        /// </para>
        /// </summary>
        private void BuildDrawnDome(out ushort[] indices)
        {
            for (int ring = 0; ring <= DOME_RINGS; ring++)
            {
                //From the zenith down to the nadir: the capture is a whole sphere and not a hemisphere, and the
                //lower half is what a wide aspect looks into past the horizon.
                float polar = MathF.PI * ring / DOME_RINGS;
                float y = MathF.Cos(polar);
                float r = MathF.Sin(polar);

                for (int segment = 0; segment <= DOME_SEGMENTS; segment++)
                {
                    float around = MathHelper.TwoPi * segment / DOME_SEGMENTS;
                    Vector3 direction = new(r * MathF.Cos(around), y, r * MathF.Sin(around));

                    _vertices[ring * (DOME_SEGMENTS + 1) + segment].Position = direction * DOME_RADIUS;
                    _vertices[ring * (DOME_SEGMENTS + 1) + segment].Normal = direction;
                }
            }

            indices = new ushort[TRIANGLE_COUNT * 3];
            int at = 0;

            for (int ring = 0; ring < DOME_RINGS; ring++)
            {
                for (int segment = 0; segment < DOME_SEGMENTS; segment++)
                {
                    int top = ring * (DOME_SEGMENTS + 1) + segment;
                    int bottom = top + DOME_SEGMENTS + 1;

                    //Both triangles wound so (b - a) x (c - a) points away from the origin - the capture's own
                    //convention, and the whole of what keeps the sky visible from inside it.
                    indices[at++] = (ushort)top;
                    indices[at++] = (ushort)(top + 1);
                    indices[at++] = (ushort)bottom;

                    indices[at++] = (ushort)(top + 1);
                    indices[at++] = (ushort)(bottom + 1);
                    indices[at++] = (ushort)bottom;
                }
            }
        }

        /// <summary>
        /// Resamples the palette into a smooth gradient over height, which is the half of the banding fix a
        /// finer mesh cannot do on its own.
        /// <para>
        /// The capture's sixteen rings carry about ten distinct colours, in PAIRS: two rings the same, then a
        /// step. Interpolated as they stand that is a flat plate, a ramp, a flat plate - and it is the plates
        /// the eye finds, because a slope going zero, steep, zero is a Mach band whatever the mesh under it is.
        /// So consecutive rings sharing a colour are collapsed into ONE stop at the middle of their run, the
        /// stops are interpolated linearly into this table, and the table is then boxed twice - which rounds
        /// the corner at every stop without moving the colours themselves anywhere the eye can find them.
        /// </para>
        /// <para>
        /// It runs on the palette's sRGB bytes, before the linear rewrite, for the reason the whole file is
        /// careful about: what is being smoothed is the AUTHORED gradient, and the authored gradient is what
        /// the .dae files painted in display space.
        /// </para>
        /// </summary>
        private void BuildRamp(string palette)
        {
            //The capture's rings, as (height, first vertex of it). One pass over the 92 entries taking each new
            //Y as it appears, and then SORTED from the zenith down - the capture's vertex order is the content
            //pipeline's welding order and is nothing like top to bottom (it opens at y = -9.09 and its second
            //entry is y = -9.93, four rings apart in the middle of the lower half). Everything below reads
            //these as a descending ladder, so leaving them in file order paints the sky its own ground colour,
            //which is exactly what the first cut of this did.
            Span<float> ringY = stackalloc float[VERTEX_COUNT];
            Span<int> ringFirst = stackalloc int[VERTEX_COUNT];
            int rings = 0;

            for (int i = 0; i < VERTEX_COUNT; i++)
            {
                float y = _capturedPositions[i].Y;
                bool seen = false;

                for (int r = 0; r < rings; r++)
                    if (ringY[r] == y) { seen = true; break; }

                if (seen) continue;

                ringY[rings] = y;
                ringFirst[rings] = i;
                rings++;
            }

            //Sixteen entries, so the plainest sort there is; a Span cannot be handed to Array.Sort with a
            //second one along for the ride anyway.
            for (int a = 0; a < rings - 1; a++)
                for (int b = a + 1; b < rings; b++)
                    if (ringY[b] > ringY[a])
                    {
                        (ringY[a], ringY[b]) = (ringY[b], ringY[a]);
                        (ringFirst[a], ringFirst[b]) = (ringFirst[b], ringFirst[a]);
                    }

            //Collapse the pairs: consecutive rings whose colour is the same become one stop, seated at the
            //middle of the run. That is what deletes the plates.
            Span<float> stopY = stackalloc float[VERTEX_COUNT];
            Span<Vector3> stopColor = stackalloc Vector3[VERTEX_COUNT];
            int stops = 0;

            for (int r = 0; r < rings;)
            {
                Vector3 colour = ColourAt(palette, ringFirst[r]);

                int last = r;
                while (last + 1 < rings && ColourAt(palette, ringFirst[last + 1]) == colour) last++;

                stopY[stops] = (ringY[r] + ringY[last]) * 0.5f;
                stopColor[stops] = colour;
                stops++;

                r = last + 1;
            }

            //Into the table, linearly between the stops and flat outside the outermost pair - past the top and
            //the bottom stop there is nothing left to interpolate towards.
            _rampLowest = MathF.Min(stopY[0], stopY[stops - 1]);
            _rampHighest = MathF.Max(stopY[0], stopY[stops - 1]);

            for (int i = 0; i < RAMP_STEPS; i++)
            {
                float y = MathHelper.Lerp(_rampLowest, _rampHighest, i / (RAMP_STEPS - 1f));
                _ramp[i] = SampleStops(stopY, stopColor, stops, y);
            }

            //Two box passes, which is what rounds the corner linear interpolation leaves at every stop. The
            //window is a fraction of the table rather than a fixed count, so it does not change meaning if the
            //table is ever resized - and it is wide enough to reach across the gap between two stops.
            BoxBlurRamp(RAMP_STEPS / 12);
            BoxBlurRamp(RAMP_STEPS / 12);
        }

        /// <summary>The palette colour of one captured vertex, as a 0..1 triple.</summary>
        private static Vector3 ColourAt(string palette, int vertex)
        {
            int at = vertex * 6;

            return new Vector3(
                HexByte(palette, at) / 255f,
                HexByte(palette, at + 2) / 255f,
                HexByte(palette, at + 4) / 255f);
        }

        /// <summary>The stops read at one height, linearly between the two straddling it.</summary>
        private static Vector3 SampleStops(Span<float> stopY, Span<Vector3> stopColor, int stops, float y)
        {
            //The stops run from the zenith DOWN, so the first is the highest.
            if (y >= stopY[0]) return stopColor[0];
            if (y <= stopY[stops - 1]) return stopColor[stops - 1];

            for (int s = 0; s + 1 < stops; s++)
            {
                if (y > stopY[s + 1])
                    return Vector3.Lerp(stopColor[s], stopColor[s + 1], (stopY[s] - y) / (stopY[s] - stopY[s + 1]));
            }

            return stopColor[stops - 1];
        }

        /// <summary>One box pass over the ramp, clamped at both ends so the extremes do not creep inward.</summary>
        private void BoxBlurRamp(int window)
        {
            Vector3[] source = (Vector3[])_ramp.Clone();

            for (int i = 0; i < RAMP_STEPS; i++)
            {
                Vector3 sum = Vector3.Zero;

                for (int k = -window; k <= window; k++)
                    sum += source[Math.Clamp(i + k, 0, RAMP_STEPS - 1)];

                _ramp[i] = sum / (2 * window + 1);
            }
        }

        /// <summary>The ramp read at one world height, between the two entries straddling it.</summary>
        private Vector3 SampleRamp(float y)
        {
            float at = MathHelper.Clamp((y - _rampLowest) / (_rampHighest - _rampLowest), 0f, 1f) * (RAMP_STEPS - 1);
            int index = (int)at;

            return index >= RAMP_STEPS - 1
                ? _ramp[RAMP_STEPS - 1]
                : Vector3.Lerp(_ramp[index], _ramp[index + 1], at - index);
        }
        /// <summary>
        /// Writes the current dome's palette into the vertices and re-uploads the buffer. The palette is
        /// read for <see cref="ZenithColor"/>/<see cref="HorizonColor"/> first, in sRGB; only then are the
        /// buffer's colours converted to linear (when asked to) — the same order the Model-based load used,
        /// and for the same reason (the palette's consumer decodes it itself). Rewriting from the stored
        /// sRGB table every time also makes the conversion idempotent by construction, which the old code
        /// needed a static already-linearized guard for, the content manager handing back cached buffers.
        /// </summary>
        private void ApplyPalette()
        {
            string palette = PALETTES[_domeNumber - 1];

            //The extraction the light rig was tuned against, reproduced exactly: thresholds from the mesh's
            //own vertical extent, colours as byte/255 floats, averaged in vertex order (float accumulation
            //order matters for bit-equality with the values every recorded look was derived under).
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < VERTEX_COUNT; i++)
            {
                float y = _capturedPositions[i].Y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            float range = maxY - minY;
            float zenithThreshold = minY + range * 0.75f;
            float horizonThreshold = minY + range * 0.2f;

            Vector3 zenithSum = Vector3.Zero;
            Vector3 horizonSum = Vector3.Zero;
            int zenithCount = 0;
            int horizonCount = 0;

            for (int i = 0; i < VERTEX_COUNT; i++)
            {
                byte r = HexByte(palette, i * 6);
                byte g = HexByte(palette, i * 6 + 2);
                byte b = HexByte(palette, i * 6 + 4);

                Vector3 color = new(r / 255f, g / 255f, b / 255f);
                float y = _capturedPositions[i].Y;

                if (y >= zenithThreshold) { zenithSum += color; zenithCount++; }
                if (y <= horizonThreshold) { horizonSum += color; horizonCount++; }
            }

            ZenithColor = zenithSum / zenithCount;
            HorizonColor = horizonSum / horizonCount;

            //And only now the drawn dome, off a ramp resampled from the very palette the loop above averaged.
            //The two readings are deliberately separate: the rig wants the colours the .dae files stated, and the
            //eye wants a gradient with no plate in it, and trying to serve both from one set of vertices is what
            //put a straight horizontal edge across every sky in the game.
            BuildRamp(palette);

            for (int i = 0; i < DRAWN_VERTEX_COUNT; i++)
            {
                Vector3 colour = SampleRamp(_vertices[i].Position.Y);

                byte r = (byte)Math.Round(MathHelper.Clamp(colour.X, 0f, 1f) * 255f);
                byte g = (byte)Math.Round(MathHelper.Clamp(colour.Y, 0f, 1f) * 255f);
                byte b = (byte)Math.Round(MathHelper.Clamp(colour.Z, 0f, 1f) * 255f);

                //RGB only ever gets encoded - the fourth byte is alpha, a coverage fraction, and stays opaque
                if (_linearVertexColors)
                {
                    r = SrgbToLinearByte(r);
                    g = SrgbToLinearByte(g);
                    b = SrgbToLinearByte(b);
                }

                _vertices[i].Color = new Color(r, g, b, byte.MaxValue);
            }

            _vertexBuffer.SetData(_vertices);
        }

        /// <summary>
        /// The exact sRGB decoding curve, quantized back to a byte. Eight bits of linear light crush the
        /// darks badly in principle, but the domes are smooth gradients that never go near black, and
        /// keeping the byte-sized vertex colour avoids widening every vertex.
        /// </summary>
        private static byte SrgbToLinearByte(byte encoded)
        {
            float c = encoded / 255f;
            float linear = c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4f);

            return (byte)Math.Round(MathHelper.Clamp(linear, 0f, 1f) * 255f);
        }

        private static byte HexByte(string hex, int index) => (byte)((Nibble(hex[index]) << 4) | Nibble(hex[index + 1]));

        private static int Nibble(char c) => c <= '9' ? c - '0' : c - 'A' + 10;

        public void Draw(ICamera camera)
        {
            //The framework's cached states, never fresh instances: this runs every frame in all three
            //executables, and a state object constructed here backs a native D3D11 state that is never
            //disposed — per-frame construction is a steady leak of finalizer-queue objects.
            _graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
            _graphicsDevice.DepthStencilState = DepthStencilState.None;

            if (_effect == null) DrawWithBasicEffect(camera); else DrawWithCustomEffect(camera);

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
        }

        /// <summary>The plain path: the owned <see cref="BasicEffect"/>, gradient and nothing else.</summary>
        private void DrawWithBasicEffect(ICamera camera)
        {
            _basicEffect.World = Matrix.CreateTranslation(camera.Position);
            _basicEffect.View = camera.View;
            _basicEffect.Projection = camera.Projection;

            _graphicsDevice.SetVertexBuffer(_vertexBuffer);
            _graphicsDevice.Indices = _indexBuffer;

            _basicEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, TRIANGLE_COUNT);
        }

        /// <summary>
        /// Issues the dome against <see cref="Effect"/>. The dome is translated to the camera every frame,
        /// which is what puts it at infinity — and it is also what lets the sky shader recover the view ray
        /// as nothing more than the world position minus the camera. (The loaded models carried an identity
        /// bone transform on top of this; there is deliberately no equivalent left to multiply in.)
        /// </summary>
        private void DrawWithCustomEffect(ICamera camera)
        {
            _worldParam.SetValue(Matrix.CreateTranslation(camera.Position));
            _viewParam.SetValue(camera.View);
            _projectionParam.SetValue(camera.Projection);

            _graphicsDevice.SetVertexBuffer(_vertexBuffer);
            _graphicsDevice.Indices = _indexBuffer;

            _effectPass.Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, TRIANGLE_COUNT);
        }

        /// <summary>
        /// Both buffers and the owned <see cref="BasicEffect"/> — never <see cref="Effect"/>, which the
        /// caller's content manager owns.
        /// </summary>
        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _vertexBuffer = null;
            _indexBuffer?.Dispose();
            _indexBuffer = null;
            _basicEffect?.Dispose();
            _basicEffect = null;
        }
    }
}
