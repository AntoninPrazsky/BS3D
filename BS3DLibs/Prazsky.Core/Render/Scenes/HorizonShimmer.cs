using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The outback's heat shimmer (#1135): the far horizon refracted in a thin strip at the skyline. The frame drawn so far
    /// is read again above or below where each pixel of the strip looks, so the plain's edge and the feet of the far
    /// formations boil up and down by a few pixels from the play camera, and under the skyline a sliver of the sky and of the
    /// rock on it is mirrored, as hot ground mirrors it. Nothing over the middle plain and nothing near: the strip is a ring
    /// round the lens <see cref="RING_SHARE"/> of the way to where the plain's last fade begins, drawn depth-tested, so
    /// everything nearer hides it. See "The heat shimmer" in docs/scenes/outback.md and <c>HorizonShimmer.fx</c>, which
    /// carries the passes' own argument.
    /// <para>
    /// <b>Three passes, all inside the scene target's frame</b>: the ring marks where it passes the depth test (an alpha of
    /// 0), <see cref="PostProcessPipeline.GrabSceneFar"/> copies the rows the ring covers with every sample weighed by that
    /// mark, and the ring is drawn again reading the copy at displaced angles and compositing only its far share, so a read
    /// beside a near silhouette brings none of the near thing's colour into the strip. That holds because every sample is
    /// weighed before anything averages it, which a multisampled scene target cannot give (leaving it resolves colour and
    /// mark together), so <see cref="SceneRenderer.DrawHorizonRefraction"/> never draws the shimmer on one. The copy is
    /// scissored to the rows the ring's projection reaches (<see cref="TryRows"/>), so it costs a strip of the frame, not the frame.
    /// </para>
    /// <para>
    /// ⚠ <b>Every figure here is an angle or a time</b>: the offset is radians of a direction's elevation, projected back
    /// through the camera, never pixels, and the clock is the scene's (<see cref="SceneFrame.Time"/>: the Game's wall clock,
    /// the Testbed's scene clock), never a frame count. The rows the copy is scissored to are the one thing counted in
    /// pixels, and they follow from the angles (<see cref="PadRows"/>).
    /// </para>
    /// </summary>
    internal sealed class HorizonShimmer : IDisposable
    {
        /// <summary>The ring's segments round the lens: a chord's middle stands 0.03 % nearer than its ends.</summary>
        public const int SEGMENTS = 128;

        /// <summary>The ring's vertices on the unit circle, x and z, the first repeated at the end: the vertex buffer and
        /// <see cref="TryRows"/> read the same points, so the rows bound the very chords the GPU draws.</summary>
        public static readonly Vector2[] Round = BuildRound();

        private static Vector2[] BuildRound()
        {
            var round = new Vector2[SEGMENTS + 1];
            for (int i = 0; i <= SEGMENTS; i++)
            {
                float angle = MathHelper.TwoPi * i / SEGMENTS;
                round[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }

            return round;
        }

        /// <summary>
        /// The largest offset, an angle: 0.28 degrees, 6.7 pixels at 1080 rows through the play camera's 42.9-degree lens
        /// (<c>GameplayScreen.GAME_FOV</c>) and 9.9 at 1600, and under half that most of the time (the field's spread,
        /// <c>SWELL_GAIN</c> and <c>BOIL_GAIN</c> in the shader). Seven tenths of it is the shader's swell, cells tall enough
        /// that a column's offset barely changes up the strip, so the skyline rises and sinks whole; three tenths the boil,
        /// the fine field rising through it (0.084 degrees). ⚠ The fine field alone at three times its first 0.07 degrees
        /// shredded the formations' feet into torn horizontal slivers: its cells are 0.15 degrees tall, and an offset larger
        /// than a cell reads its rows out of order (docs/scenes/outback.md, "The heat shimmer").
        /// </summary>
        public static readonly float AMPLITUDE = MathHelper.ToRadians(0.28f);

        /// <summary>How far over the skyline's axis the waver reaches before it has faded out: the strip's upper half.</summary>
        public static readonly float REACH = MathHelper.ToRadians(1.5f);

        /// <summary>
        /// The lowest a read may land, as a share of the way from the band's bottom (the plain at the ring) to the axis: a
        /// read lower than that would fall on the plain nearer than the ring, which brings nothing back, and the skyline would
        /// tear there. So a read downward is held at this floor (<c>Band.w</c> in the shader) instead of fading in from the
        /// band's bottom, as a read upward does: from the play camera the far plain under the axis is a fifth of a degree,
        /// less than the offset, and a fade that steep turned the order of the rows over at the horizon.
        /// </summary>
        public const float FLOOR_SHARE = 0.3f;

        /// <summary>How deep under the skyline's axis the mirage reaches: a fifth of a degree, five pixels at 1080 rows through
        /// the play camera's lens and seven at 1600. It fades out before the band's bottom (the floor, <see cref="FLOOR_SHARE"/>),
        /// so a lens low over the plain, where the plain from the ring to the axis is thinner than this, sees less of it.</summary>
        public static readonly float MIRAGE_DEPTH = MathHelper.ToRadians(0.2f);

        /// <summary>How much of the mirrored image shows at the axis, fading to nothing at <see cref="MIRAGE_DEPTH"/>.</summary>
        public const float MIRAGE_STRENGTH = 1f;

        /// <summary>
        /// The steepest look down at the skyline's axis the shimmer keeps its whole strength at: 0.8 degrees, a lens up to 14
        /// over the plain (the play camera at 6, the approach at 2.9). Steeper, the waver falls as the square of this over the
        /// angle and the mirage as its fourth power (<see cref="Grazing"/>): a ray that grazes the hot air over the ground
        /// crosses a long path of it, a steep one a short path, and a mirage is a grazing ray's alone. The intro's wide shot
        /// and its crane (about 40 over the plain, 2.3 degrees down) keep an eighth of the waver, 0.034 degrees. ⚠ More was
        /// a row of dark teeth along the whole skyline from there, the plain's red edge boiling up into the pale fade: at the
        /// whole of the first 0.07 degrees, and again at 0.12 (this amplitude falling as one over the angle).
        /// </summary>
        public static readonly float GRAZE = MathHelper.ToRadians(0.8f);

        /// <summary>The share of its strength the waver keeps for a band (<see cref="GRAZE"/>): 1 where the axis lies within
        /// it under the horizontal, falling as the square of it over the angle past it. The mirage keeps the square of this.</summary>
        public static float Grazing(Elevations band)
        {
            float share = Math.Clamp(GRAZE / MathF.Max(-band.Axis, 1e-6f), 0f, 1f);
            return share * share;
        }

        /// <summary>How far the ring's lower edge reaches under the band's bottom, so the far plain right at the ring's
        /// radius is inside it: the plain nearer than the radius hides what is under it anyway.</summary>
        public static readonly float UNDER = MathHelper.ToRadians(0.25f);

        /// <summary>The lowest height over the plain the band is reckoned from, so a lens at the ground still has a skyline under it.</summary>
        public const float MIN_LENS_HEIGHT = 0.5f;

        /// <summary>
        /// The scene's clock is handed over modulo this many seconds: the field slides a few cells a second, and a float
        /// grown for hours would leave it too few bits to slide by. The wrap is a jump in a field nothing tracks, once in ten
        /// minutes.
        /// </summary>
        public const float CLOCK_WRAP = 600f;

        /// <summary>
        /// The band's three elevations over the horizontal, radians: <see cref="Bottom"/> where the plain lies at the ring's
        /// radius (a read upward grows from nothing there, and a read downward is held over it, <see cref="FLOOR_SHARE"/>),
        /// <see cref="Axis"/> the skyline, the plain where its last fade begins, where the waver is strongest and the mirage's
        /// mirror runs, and <see cref="Top"/>, <see cref="REACH"/> over the axis, where it has faded out.
        /// </summary>
        public readonly record struct Elevations(float Bottom, float Axis, float Top);

        /// <summary>
        /// The ring's radius, as a share of the distance where the plain's last fade begins (the skyline's axis).
        /// <para>
        /// ⚠ <b>Not the fade's start itself.</b> The line the eye reads as the horizon is where the plain's own colour ends and
        /// its fade into the sky begins (<see cref="OutbackAirConfig.HorizonHazeDistance"/>): the last few hundred units past it
        /// are already half sky, squeezed into a pixel or two. A ring there made that line the strip's lower edge, every read
        /// under it fell on plain the gate calls near, and from the play camera and a low lens the line moved a twentieth of a
        /// pixel on average (a quarter of a second apart, 0.33 at the 95th percentile): only the faded sliver over it took the
        /// waver. 0.62 of the way out (620 units) puts that line inside the strip with a fifth of a degree of the far plain
        /// under it from the play camera, room for the mirage (<see cref="MIRAGE_DEPTH"/>) and the floor a read downward is
        /// held at; and it stays past <see cref="FarField.FAR_FADE_START_LATEST"/>, where every other ground's last fade
        /// begins, so the plain it may touch is still the far plain.
        /// </para>
        /// </summary>
        public const float RING_SHARE = 0.62f;

        /// <summary>The ring's radius for a plain whose last fade begins at <paramref name="fadeStart"/>, held short of the
        /// fade's end (<see cref="FarField.FAR_FADE_END"/>).</summary>
        public static float RingRadius(float fadeStart) => SkylineDistance(fadeStart) * RING_SHARE;

        private static float SkylineDistance(float fadeStart) => Math.Clamp(fadeStart, 1f, FarField.FAR_FADE_END);

        /// <summary>
        /// The band for a lens <paramref name="lensHeight"/> over a flat plain whose last fade begins at
        /// <paramref name="fadeStart"/>: its axis the plain there, its bottom the plain at the ring (<see cref="RingRadius"/>).
        /// </summary>
        public static Elevations BandFor(float lensHeight, float fadeStart)
        {
            float height = MathF.Max(lensHeight, MIN_LENS_HEIGHT);
            float axis = -MathF.Atan(height / SkylineDistance(fadeStart));

            return new Elevations(-MathF.Atan(height / RingRadius(fadeStart)), axis, axis + REACH);
        }

        /// <summary>The lowest elevation a read of <paramref name="band"/> may land at (<see cref="FLOOR_SHARE"/>): over the
        /// band's bottom, so on the plain beyond the ring.</summary>
        public static float Floor(Elevations band) => band.Bottom + FLOOR_SHARE * (band.Axis - band.Bottom);

        /// <summary>
        /// The rows the copy must hold beyond the ring's own projection, for a frame <paramref name="height"/> rows tall
        /// through <paramref name="projection"/>: the largest offset in rows where a radian is longest on the screen (its
        /// edge, a third more than its middle at a 45-degree lens), and the bilinear read's neighbour. It follows from the
        /// angle; nothing else in the shimmer is counted in pixels.
        /// </summary>
        public static int PadRows(Matrix projection, int height) =>
            (int)MathF.Ceiling(AMPLITUDE * height * 0.5f * MathF.Abs(projection.M22) * PAD_EDGE_STRETCH) + 2;

        //How much longer a radian of elevation is at a frame's top or bottom edge than at its middle, with room: 1/cos^2 of half
        //the lens, 1.17 at a 45-degree lens and 1.33 at 60
        private const float PAD_EDGE_STRETCH = 1.5f;

        /// <summary>
        /// The rows of a frame <paramref name="height"/> rows tall that the ring round <paramref name="lens"/> covers through
        /// <paramref name="viewProjection"/>, widened by <paramref name="pad"/> each way and held inside the frame; false when
        /// none of the ring is in front of the lens and on the screen. The ring's projection is bounded by its chords' ends
        /// and where they leave the screen's sides, each chord clipped at the near plane first.
        /// </summary>
        public static bool TryRows(Matrix viewProjection, Vector3 lens, float radius, float tanBottom, float tanTop, int height, int pad,
            out int first, out int count)
        {
            float yMin = float.MaxValue, yMax = float.MinValue;

            Vector4 lowPrevious = default, highPrevious = default;
            for (int i = 0; i <= SEGMENTS; i++)
            {
                float x = Round[i].X * radius, z = Round[i].Y * radius;

                Vector4 low = Vector4.Transform(new Vector4(lens.X + x, lens.Y + radius * tanBottom, lens.Z + z, 1f), viewProjection);
                Vector4 high = Vector4.Transform(new Vector4(lens.X + x, lens.Y + radius * tanTop, lens.Z + z, 1f), viewProjection);

                ExtendBySegment(low, high, ref yMin, ref yMax);
                if (i > 0)
                {
                    ExtendBySegment(lowPrevious, low, ref yMin, ref yMax);
                    ExtendBySegment(highPrevious, high, ref yMin, ref yMax);
                }

                lowPrevious = low;
                highPrevious = high;
            }

            first = count = 0;
            if (yMax < yMin) return false;

            //Normalized y runs up, rows run down
            int top = (int)MathF.Floor((1f - Math.Clamp(yMax, -1f, 1f)) * 0.5f * height) - pad;
            int bottom = (int)MathF.Ceiling((1f - Math.Clamp(yMin, -1f, 1f)) * 0.5f * height) + pad;
            top = Math.Clamp(top, 0, height);
            bottom = Math.Clamp(bottom, 0, height);

            first = top;
            count = bottom - top;
            return count > 0;
        }

        //Clip-space w under which a point counts as behind the lens
        private const float NEAR_W = 1e-3f;

        //Widens [yMin, yMax] by the part of the segment a..b (clip space) in front of the lens and between the screen's sides
        private static void ExtendBySegment(Vector4 a, Vector4 b, ref float yMin, ref float yMax)
        {
            if (a.W < NEAR_W && b.W < NEAR_W) return;

            if (a.W < NEAR_W) a = Vector4.Lerp(a, b, (NEAR_W - a.W) / (b.W - a.W));
            else if (b.W < NEAR_W) b = Vector4.Lerp(b, a, (NEAR_W - b.W) / (a.W - b.W));

            float ax = a.X / a.W, ay = a.Y / a.W, bx = b.X / b.W, by = b.Y / b.W;

            //Held between the screen's sides: what lies past them is never drawn
            if ((ax < -1f && bx < -1f) || (ax > 1f && bx > 1f)) return;

            if (ax != bx)
            {
                float t0 = (Math.Clamp(ax, -1f, 1f) - ax) / (bx - ax);
                float t1 = (Math.Clamp(bx, -1f, 1f) - ax) / (bx - ax);
                float y0 = ay + (by - ay) * t0, y1 = ay + (by - ay) * t1;
                ay = y0;
                by = y1;
            }

            //Wholly over or under the screen between its sides: nothing of it is drawn either. (A quad of the band could
            //still hold the whole screen with every edge outside it, but the band is a few degrees tall and no lens is.)
            if ((ay > 1f && by > 1f) || (ay < -1f && by < -1f)) return;

            yMin = MathF.Min(yMin, MathF.Min(ay, by));
            yMax = MathF.Max(yMax, MathF.Max(ay, by));
        }

        /// <summary>The ring's depth state: tested against what the scene drew, never written, so the passes leave the
        /// depth buffer as they found it and everything nearer than the ring hides it.</summary>
        public static readonly DepthStencilState Gate = DepthStencilState.DepthRead;

        /// <summary>The mark's blend: the alpha alone, written whole (the 0 the far grab reads as "far"), the colour untouched.</summary>
        public static readonly BlendState MarkBlend = new()
        {
            ColorSourceBlend = Blend.One,
            ColorDestinationBlend = Blend.Zero,
            AlphaSourceBlend = Blend.One,
            AlphaDestinationBlend = Blend.Zero,
            ColorWriteChannels = ColorWriteChannels.Alpha
        };

        private readonly GraphicsDevice _device;
        private readonly VertexBuffer _ring;
        private readonly EffectPass _markPass, _refractPass;
        private readonly EffectParameter _viewProjection, _cameraPosition, _ringParameter, _band, _time, _grab, _grabSize, _amplitude, _mirage;

        /// <summary>Loads <c>Shaders/HorizonShimmer</c>, sets its constant figures once and builds the ring.</summary>
        public HorizonShimmer(GraphicsDevice device, ContentManager content)
        {
            _device = device;

            Effect effect = content.Load<Effect>("Shaders/HorizonShimmer");
            _markPass = effect.Techniques["Mark"].Passes[0];
            _refractPass = effect.Techniques["Refract"].Passes[0];
            _viewProjection = effect.Parameters["ViewProjection"];
            _cameraPosition = effect.Parameters["CameraPosition"];
            _ringParameter = effect.Parameters["Ring"];
            _band = effect.Parameters["Band"];
            _time = effect.Parameters["Time"];
            _grab = effect.Parameters["Grab"];
            _grabSize = effect.Parameters["GrabSize"];
            _amplitude = effect.Parameters["Amplitude"];
            _mirage = effect.Parameters["Mirage"];

            //A strip round the unit circle: x and z the circle, y 0 on the lower edge and 1 on the upper (HorizonShimmer.fx's
            //BandVS scales and lifts it). Seen from inside and drawn with no culling, so its winding is nobody's.
            var vertices = new VertexPosition[(SEGMENTS + 1) * 2];
            for (int i = 0; i <= SEGMENTS; i++)
            {
                Vector3 round = new(Round[i].X, 0f, Round[i].Y);
                vertices[i * 2] = new VertexPosition(round);
                vertices[i * 2 + 1] = new VertexPosition(round + Vector3.UnitY);
            }

            _ring = new VertexBuffer(device, VertexPosition.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            _ring.SetData(vertices);
        }

        /// <summary>
        /// Draws the shimmer into the bound scene target: the plain at <paramref name="groundY"/>, its last fade beginning at
        /// <paramref name="fadeStart"/> (the skyline's axis; the ring at <see cref="RingRadius"/>). Call it with the scene
        /// target bound, after everything the strip may show (the far plain, the formations on the skyline, the sky) and
        /// before what must stand in front of it untouched: the island's colour, the gun, the cluster. Puts the device's
        /// states back; the vertex buffer is not.
        /// </summary>
        public void Draw(in SceneFrame frame, PostProcessPipeline pipeline, float groundY, float fadeStart)
        {
            Point grabSize = pipeline.GrabSize;
            if (grabSize.X <= 0 || grabSize.Y <= 0) return;

            ICamera camera = frame.Camera;
            Vector3 lens = camera.Position;
            Elevations band = BandFor(lens.Y - groundY, fadeStart);
            float ring = RingRadius(fadeStart);
            float tanBottom = MathF.Tan(band.Bottom - UNDER);
            float tanTop = MathF.Tan(band.Top);
            Matrix viewProjection = camera.View * camera.Projection;

            //Nothing of the ring in front of the lens and on the screen: no copy at all
            if (!TryRows(viewProjection, lens, ring, tanBottom, tanTop, grabSize.Y, PadRows(camera.Projection, grabSize.Y),
                out int firstRow, out int rowCount)) return;

            _viewProjection.SetValue(viewProjection);
            _cameraPosition.SetValue(lens);
            _ringParameter.SetValue(new Vector3(ring, tanBottom, tanTop));
            _band.SetValue(new Vector4(band.Bottom, band.Axis, band.Top, Floor(band)));
            _time.SetValue(frame.Time % CLOCK_WRAP);

            //Weaker the steeper the lens looks down at the skyline (GRAZE), the mirage as the square of the waver's share:
            //with the lens's height, so stated every frame
            float grazing = Grazing(band);
            _amplitude.SetValue(AMPLITUDE * grazing);
            _mirage.SetValue(new Vector2(MIRAGE_DEPTH, MIRAGE_STRENGTH * grazing * grazing));

            BlendState blend = _device.BlendState;
            DepthStencilState depth = _device.DepthStencilState;
            RasterizerState raster = _device.RasterizerState;

            _device.BlendState = MarkBlend;
            _device.DepthStencilState = Gate;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SetVertexBuffer(_ring);
            _markPass.Apply();
            _device.DrawPrimitives(PrimitiveType.TriangleStrip, 0, SEGMENTS * 2);

            //The grab puts the gate and the rasterizer back as it found them, and leaves the quad bound
            Texture2D grab = pipeline.GrabSceneFar(firstRow, rowCount);
            if (grab != null)
            {
                _grab.SetValue(grab);
                _grabSize.SetValue(new Vector2(grabSize.X, grabSize.Y));

                _device.BlendState = BlendState.Opaque;
                _device.SetVertexBuffer(_ring);
                _refractPass.Apply();
                _device.DrawPrimitives(PrimitiveType.TriangleStrip, 0, SEGMENTS * 2);
            }

            _device.BlendState = blend;
            _device.DepthStencilState = depth;
            _device.RasterizerState = raster;
        }

        /// <inheritdoc/>
        public void Dispose() => _ring.Dispose();
    }
}
