using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D.GameObjects;
using Prazsky.Core.Camera;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The floor alarm: a grid of pulsing red laser beams hovering on the plane a ball's surface would
    /// touch at the moment the level is lost, shown while the descending ceiling is within
    /// <c>GameplayScreen.LASER_WARN_STEPS</c> more steps of pushing the cluster past the death line. It is the ceiling flash's counterpart at the other end
    /// of the field — the plate says "the glass stepped", this says "and this is where it ends" — so it
    /// deliberately shares that flash's red.
    /// <para>
    /// Session-owned (the trigger is the cluster's own geometry, and <see cref="Screens.GameplayScreen"/> reads it
    /// off the same walk the loss is decided on), but everything time-driven in it is stamped in wall-clock
    /// seconds and evaluated in <see cref="Draw"/>: a lost level pushes the result screen over the session,
    /// which stops its <c>Update</c> while <c>Draw</c> keeps running — a fade advanced in an update would
    /// freeze at full brightness at the exact moment the player is being told what it was warning about.
    /// The linger-and-fade after the loss is a pure function of the clock instead, the same contract the
    /// fireworks' sparks have with their age.
    /// </para>
    /// <para>
    /// One static vertex buffer per level, rebuilt in <see cref="Fit"/> at the field's own footprint, one
    /// draw call, three per-frame uniforms. Each beam is a ribbon, a strip of quads along its path, turned about its own
    /// axis to face the camera in the vertex shader (see <c>LaserGrid.fx</c>) — the play camera stands barely below the
    /// grid's plane, and a flat ribbon lying in it would be seen edge-on and vanish.
    /// </para>
    /// <para>
    /// <b>The net ends on a boundary through its four corners</b> (#941): the lattice is bowed in between the corners
    /// (<see cref="LaserNetShape"/>), the outermost beams are the boundary and every other beam stops exactly where it
    /// meets one, so no line goes past an edge and the cones land on the real ends of the two lines that make each corner.
    /// The bend is in plan only: every vertex is on <see cref="PlaneY"/>.
    /// </para>
    /// <para>
    /// <b>The net is projected</b> (#874): from each of the four emitters' lenses in the funnel
    /// (<see cref="LaserEmitterRing.Lens"/>) a soft cone of the same red runs to its corner of the net, a pencil as thin
    /// as a beam where it leaves the centre of the lens and widening as it goes (<see cref="CONE_SPREAD"/>), dimmer the
    /// wider (<see cref="CONE_GAIN_POWER"/>), landing on its corner and dissolving just past it. The cones are quads of the
    /// same buffer and the same draw, so they carry the net's own pulse, wave and flare, and are lit only while it is.
    /// </para>
    /// </summary>
    internal sealed class LaserGrid : IDisposable
    {
        //Half-thickness of a beam. Thin on purpose: the red halo and the glare's bloom carry the visual
        //weight, and a fat beam reads as a glowing bar rather than a laser.
        private const float BEAM_HALF_WIDTH = 0.055f;

        //A beam's soft ends, as a length and as most a share of the beam: barely there, because every end is on the
        //boundary (#941) - a T where it meets the outermost beam, a corner where two of those meet and a cone lands - and a
        //fade longer than the ribbon is wide would leave a gap in each. It only takes the edge off a cut across the ribbon.
        private const float BEAM_FADE_LENGTH = 0.08f;
        private const float BEAM_FADE_MAX_SHARE = 0.25f;

        //The projection cones (#874). A cone leaves the lens as thin as a beam and widens by this much half-width per unit
        //of its length (a half-angle of about 15 degrees), its width at the corner held between the two figures after it:
        //a 17-wide net's corner stands almost over its emitter, under a unit off, and a cone that short still has to read
        //as spreading; an 11-wide one's is four units off, and a cone that wide would hide the corner it lands on.
        private const float CONE_SPREAD = 0.27f;
        private const float CONE_CORNER_MIN_HALF_WIDTH = 0.45f;
        private const float CONE_CORNER_MAX_HALF_WIDTH = 1.0f;

        //How a cone dims as it widens: the radiance goes as (start / half-width) to this power, so the pencil at the lens
        //is as hot as a beam and the wide end a red haze. Under 1, so the cone's whole light still grows a little as it
        //opens and the landing reads.
        private const float CONE_GAIN_POWER = 0.6f;

        //A cone does not stop at the corner: it lands on the net there, half its light left, and dissolves this share of
        //its width at the corner beyond it, so the corner is lit by it rather than the cone fading out short of it. Its
        //axis is the lens's through the corner all the same.
        private const float CONE_LANDING = 0.5f;

        //It starts at once at the lens, a share of its length
        private const float CONE_FADE_IN = 0.02f;

        //The quads each cone is cut into along its length: the gain is not linear in the length, and each quad's corners
        //carry it, so a few of them make the falloff a curve
        private const int CONE_SECTIONS = 6;

        //The envelope's two slopes. Arrival is fast — it is an alarm — and departure slow enough to read
        //as the net standing down rather than flickering out when the player frees the bottom level.
        private const float FADE_IN_SECONDS = 0.35f;
        private const float FADE_OUT_SECONDS = 0.9f;

        //How long the net keeps pulsing under the result page before it fades. Measured from the moment
        //the page is actually presented (GameplayScreen.ShowResultScreen), not from the level's logical
        //end — a clear's page arrives a whole collapse beat and possibly a cinematic later — so the player
        //who looks back at the arena for the why still finds the net standing where the loss it warned
        //about happened.
        private const float LINGER_SECONDS = 2.5f;

        //The pulse: a raised cosine squared, so the peaks are sharp and the troughs wide — an alarm's
        //shape, not a breath. The floor keeps the net readable between peaks; the peak is what blooms
        //(at the floor the beams' luminance sits just under the glare threshold, at the peak far over it,
        //so the pulse visibly breathes the bloom itself).
        private const float PULSE_HZ = 1.6f;
        private const float PULSE_FLOOR = 0.35f;

        /// <summary>
        /// One edge vertex of one row of a beam's or a cone's ribbon (#941): the point of the path the row stands on, which
        /// way the path runs there, and the row's own half-width, gain, fades and place along the path, so the shader needs
        /// no per-beam uniforms and a bent beam is as much a ribbon as a straight one. The two vertices of a row differ in
        /// their side only; the strip's quads share them.
        /// </summary>
        private struct LaserVertex : IVertexType
        {
            public Vector3 Position;    //on the path
            public Vector3 Tangent;     //unit, which way the path runs here: the axis the ribbon turns about to face the camera
            public Vector4 Data;        //(side in {-1,1}, along the whole path in [0,1], half-width here, gain here)
            public Vector3 Fade;        //(fade-in, fade-out) as shares of the path, and the distance along it in world units

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 0),
                new VertexElement(24, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
                new VertexElement(40, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 2));

            readonly VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }

        private readonly GraphicsDevice _device;
        private readonly Effect _effect;

        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;
        private int _quadCount;

        //Cached parameter handles: the by-name indexer is a linear scan, and these are set every frame.
        private readonly EffectParameter _viewParam, _projectionParam, _cameraPositionParam;
        private readonly EffectParameter _intensityParam, _timeParam;

        //The visibility state, stamped rather than eased: which way the envelope is going, when it turned,
        //and where it stood at that moment — so EnvelopeAt is a pure function of the wall clock and keeps
        //moving while the covered session receives no updates. _expireAt is the scheduled end of the
        //linger after a level ends, PositiveInfinity while no end is scheduled.
        private bool _visible;
        private float _transitionAt;
        private float _envelopeAtTransition;
        private float _expireAt = float.PositiveInfinity;

        /// <summary>Whether the net is currently armed — the trigger reads this for its hysteresis.</summary>
        internal bool Visible => _visible;

        /// <summary>
        /// The net that ended the level, standing under the result page (#702): it flared on a loss to the line and
        /// the page has gone up over it (<see cref="NoticeLevelEnded"/>). Nowhere else - not on a clear, not on an
        /// out-of-balls loss, not during the loss's own camera beat, where the cut is the sound.
        /// </summary>
        internal bool HoldsTheLoss => _visible && _flareAt < float.PositiveInfinity && _settleAt < float.PositiveInfinity;

        //The kill flare (#434): how bright the net goes when the cluster crosses it, and how long it takes
        //to get there. Well over the warning's own ceiling of 1 - the beam is drawn into the HDR target and
        //the glare pass reads it, so this is what makes the line BLOOM across the frame rather than merely
        //brighten. The rise is a third of a second: long enough not to be one frame's flash, short enough
        //that it is already happening while the camera is still on its way.
        private const float FLARE_INTENSITY = 3.2f;
        private const float FLARE_IN = 0.33f;

        //When the flare began, or +inf for a net that has not killed anything.
        private float _flareAt = float.PositiveInfinity;

        //The net lit for the telling and not the warning (#933): how brightly the session asked for it last, and when. It is
        //not the alarm - Visible, the trigger's hysteresis, the contextual card and the HUD never read it - only a light the
        //beams, the cones and the lenses are drawn at, over the alarm's own by whichever is more. Stamped, and let go of when
        //it is not renewed: the session asks again every frame it is updated, and a result page that covers it stops those
        //updates, so a light asked for at the moment the level ended would otherwise burn on under the page for ever.
        private float _preview;
        private float _previewAt = float.NegativeInfinity;
        private const float PREVIEW_HOLD_SECONDS = 0.2f;
        private const float PREVIEW_FADE_SECONDS = 0.3f;

        //When the result page of a loss went up, or +inf: from then the flare settles back to the warning's own
        //pulse over SETTLE_SECONDS and the net holds there for as long as the page is up (#614)
        private float _settleAt = float.PositiveInfinity;
        private const float SETTLE_SECONDS = 1f;

        public LaserGrid(GraphicsDevice device, Effect effect, Vector3 color)
        {
            _device = device;
            _effect = effect;

            _viewParam = effect.Parameters["View"];
            _projectionParam = effect.Parameters["Projection"];
            _cameraPositionParam = effect.Parameters["CameraPosition"];
            _intensityParam = effect.Parameters["LaserIntensity"];
            _timeParam = effect.Parameters["Time"];

            //Set once: the colour is handed in (the ceiling alarm's own red, so the two warnings cannot
            //drift apart). A parameter's value persists on the effect; the widths ride on the vertices.
            effect.Parameters["LaserColor"].SetValue(color);
        }

        /// <summary>
        /// Rebuilds the net at a level's field footprint (half-extents about the origin the field is
        /// centred on) and the height of the plane it guards. Per level, like the ceiling's own renderer —
        /// never per frame.
        /// </summary>
        internal void Fit(float halfX, float halfZ, float y)
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();

            //Where the outermost beams stand: the net's corners, which the four emitters in the funnel aim at and project
            //the net onto (#874), and which the boundary of the net passes through (#941).
            CornerX = LaserNetShape.CornerFor(halfX);
            CornerZ = LaserNetShape.CornerFor(halfZ);
            PlaneY = y;

            //Symmetric about the origin: a centre beam on each axis and as many more as whole pitches fit
            int alongX = LaserNetShape.BeamCount(CornerZ);      //beams running along X are spaced in Z
            int alongZ = LaserNetShape.BeamCount(CornerX);

            //Every row of every strip is two vertices, and a strip of n + 1 rows is n quads: a beam is BEAM_SEGMENTS quads
            //and a cone CONE_SECTIONS
            int beams = alongX + alongZ;
            _quadCount = beams * LaserNetShape.BEAM_SEGMENTS + LaserEmitterRing.COUNT * CONE_SECTIONS;
            int vertexCount = (beams * (LaserNetShape.BEAM_SEGMENTS + 1) + LaserEmitterRing.COUNT * (CONE_SECTIONS + 1)) * 2;

            //The indices are sixteen bits: a net this fine would have to be cut into several buffers (the widest the set
            //draws is 17 across, with about a tenth of this)
            if (vertexCount > ushort.MaxValue)
                throw new InvalidOperationException($"The floor net of a {CornerX} x {CornerZ} field needs {vertexCount} vertices, more than a 16-bit index reaches.");

            LaserVertex[] vertices = new LaserVertex[vertexCount];
            short[] indices = new short[_quadCount * 6];

            int vertex = 0;
            int index = 0;

            for (int i = 0; i < alongX; i++)
                AddBeam(vertices, indices, ref vertex, ref index, true, LaserNetShape.BeamOffset(i, alongX), y);

            for (int i = 0; i < alongZ; i++)
                AddBeam(vertices, indices, ref vertex, ref index, false, LaserNetShape.BeamOffset(i, alongZ), y);

            //The four projection cones, each from the centre of its emitter's lens to its corner of the net
            for (int k = 0; k < LaserEmitterRing.COUNT; k++)
                AddCone(vertices, indices, ref vertex, ref index, LaserEmitterRing.Lens(k, CornerX, CornerZ, y),
                    LaserEmitterRing.NetCorner(k, CornerX, CornerZ, y));

            _vertexBuffer = new VertexBuffer(_device, LaserVertex.Declaration, vertices.Length, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(vertices);

            _indexBuffer = new IndexBuffer(_device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices);
        }

        /// <summary>Where the net's outermost beams along Z stand in X, the net's corners' half-extent (#874); set by <see cref="Fit"/>.</summary>
        internal float CornerX { get; private set; }

        /// <summary>Where the outermost beams along X stand in Z; set by <see cref="Fit"/>.</summary>
        internal float CornerZ { get; private set; }

        /// <summary>The height of the net's plane, which the emitters aim at with its corners; set by <see cref="Fit"/>.</summary>
        internal float PlaneY { get; private set; }

        /// <summary>
        /// One beam of the net (#941): <see cref="LaserNetShape.BEAM_SEGMENTS"/> + 1 rows along its bowed path
        /// (<see cref="LaserNetShape.BeamPoint"/>), the ribbon's axis at each row the path's direction there (the neighbours'
        /// chord, so the rows of two segments agree on one edge and a bend has no crack in it). The beam fades over
        /// <see cref="BEAM_FADE_LENGTH"/> at each end, which is where it meets the boundary or, for the outermost beams, the corner.
        /// </summary>
        private void AddBeam(LaserVertex[] vertices, short[] indices, ref int vertex, ref int index, bool alongX, float offset, float y)
        {
            const int N = LaserNetShape.BEAM_SEGMENTS;

            Span<Vector3> points = stackalloc Vector3[N + 1];
            Span<float> distance = stackalloc float[N + 1];

            for (int s = 0; s <= N; s++)
            {
                Vector2 p = LaserNetShape.BeamPoint(alongX, offset, s, CornerX, CornerZ);

                points[s] = new Vector3(p.X, y, p.Y);
                distance[s] = s == 0 ? 0f : distance[s - 1] + Vector3.Distance(points[s - 1], points[s]);
            }

            float length = distance[N];
            float fade = MathF.Min(BEAM_FADE_LENGTH / length, BEAM_FADE_MAX_SHARE);

            for (int s = 0; s <= N; s++)
            {
                Vector3 tangent = Vector3.Normalize(points[Math.Min(s + 1, N)] - points[Math.Max(s - 1, 0)]);

                AddRow(vertices, indices, ref vertex, ref index, s > 0, points[s], tangent, BEAM_HALF_WIDTH, 1f,
                    distance[s] / length, distance[s], fade, fade);
            }
        }

        /// <summary>
        /// One projection cone from <paramref name="lens"/> through <paramref name="corner"/>: <see cref="CONE_SECTIONS"/> + 1
        /// rows of one straight axis, the half-width growing linearly from a beam's at the lens (a true cone, billboarded) and the
        /// gain falling with it (<see cref="CONE_GAIN_POWER"/>), landing on the corner and dissolving just past it
        /// (<see cref="CONE_LANDING"/>).
        /// </summary>
        private static void AddCone(LaserVertex[] vertices, short[] indices, ref int vertex, ref int index, Vector3 lens, Vector3 corner)
        {
            float length = Vector3.Distance(lens, corner);
            float cornerHalfWidth = Math.Clamp(length * CONE_SPREAD, CONE_CORNER_MIN_HALF_WIDTH, CONE_CORNER_MAX_HALF_WIDTH);

            //The axis runs on past the corner by the landing, and the fade-out is twice that, so the corner sits halfway down it
            float total = length + CONE_LANDING * cornerHalfWidth;
            Vector3 axis = Vector3.Normalize(corner - lens);
            Vector3 end = lens + (corner - lens) * (total / length);
            float endHalfWidth = BEAM_HALF_WIDTH + (cornerHalfWidth - BEAM_HALF_WIDTH) * (total / length);
            float fadeOut = MathF.Min(2f * (total - length) / total, 0.8f);

            for (int i = 0; i <= CONE_SECTIONS; i++)
            {
                float a = (float)i / CONE_SECTIONS;
                float w = MathHelper.Lerp(BEAM_HALF_WIDTH, endHalfWidth, a);

                AddRow(vertices, indices, ref vertex, ref index, i > 0, Vector3.Lerp(lens, end, a), axis, w, ConeGain(w),
                    a, a * total, CONE_FADE_IN, fadeOut);
            }
        }

        private static float ConeGain(float halfWidth) => MathF.Pow(BEAM_HALF_WIDTH / halfWidth, CONE_GAIN_POWER);

        /// <summary>
        /// One row of a strip: two vertices on either side of <paramref name="position"/>, and, when <paramref name="joins"/>
        /// (it is not the strip's first), the quad that joins it to the row before.
        /// </summary>
        private static void AddRow(LaserVertex[] vertices, short[] indices, ref int vertex, ref int index, bool joins,
            Vector3 position, Vector3 tangent, float halfWidth, float gain, float along, float distance, float fadeIn, float fadeOut)
        {
            Vector3 fade = new(fadeIn, fadeOut, distance);

            vertices[vertex + 0] = new LaserVertex { Position = position, Tangent = tangent, Data = new Vector4(-1f, along, halfWidth, gain), Fade = fade };
            vertices[vertex + 1] = new LaserVertex { Position = position, Tangent = tangent, Data = new Vector4(1f, along, halfWidth, gain), Fade = fade };

            if (joins)
            {
                int previous = vertex - 2;

                indices[index + 0] = (short)(previous + 0);
                indices[index + 1] = (short)(previous + 1);
                indices[index + 2] = (short)(vertex + 0);
                indices[index + 3] = (short)(vertex + 0);
                indices[index + 4] = (short)(previous + 1);
                indices[index + 5] = (short)(vertex + 1);

                index += 6;
            }

            vertex += 2;
        }

        /// <summary>
        /// Arms or stands the net down. The envelope restarts from wherever it stands, so a warning that
        /// flips mid-fade reverses from there rather than snapping — the one-reversible-scalar idiom, done
        /// in stamped time because the fade must keep running while the covered session gets no updates.
        /// </summary>
        internal void SetVisible(bool visible, float now)
        {
            if (_visible == visible) return;

            _envelopeAtTransition = EnvelopeAt(now);
            _visible = visible;
            _transitionAt = now;

            //Standing down clears any scheduled expiry: it has already happened.
            if (!visible) _expireAt = float.PositiveInfinity;
        }

        /// <summary>
        /// The cluster has just crossed the line and the level is lost (#434): the net stops warning and
        /// starts <b>cutting</b>. It flares to <see cref="FLARE_INTENSITY"/> over <see cref="FLARE_IN"/> and
        /// stays there while the loss is being shown, which is the beat the camera spends flying at the
        /// crossing point.
        /// <para>
        /// It rides <b>over</b> the ordinary envelope rather than replacing it, so a net already at full
        /// warning does not dip on the frame it kills: the envelope is what the net's arrival and departure
        /// are made of, and the flare is a different statement laid on top of the same beam.
        /// </para>
        /// </summary>
        internal void Flare(float now)
        {
            if (_flareAt < float.PositiveInfinity) return;   //once; a loss cannot be lost twice
            _flareAt = now;
        }

        /// <summary>
        /// The level's ending has just been put in front of the player — the result screen is going up. A net
        /// still standing keeps pulsing for <see cref="LINGER_SECONDS"/> and then goes out — <see cref="Draw"/>
        /// executes the expiry, because it is the only thing still running under the result page. <c>Min</c>,
        /// so a second presentation cannot push a running linger out.
        /// <para>
        /// <b>Except on a loss to the line, where it stays</b> (#614): the owner lost Amphora and watched the net go
        /// out under FAILED, taking with it the one thing on screen that said why, while the cluster still hung
        /// past it. A net that has flared is the net that ended the level, so it sets no expiry; it settles from the
        /// flare back to the warning's pulse over <see cref="SETTLE_SECONDS"/>, which the page's text stands over,
        /// and goes when the player leaves the page (<see cref="Reset"/> at the next level, or the session torn down).
        /// </para>
        /// </summary>
        internal void NoticeLevelEnded(float now)
        {
            if (!_visible) return;

            if (_flareAt < float.PositiveInfinity)
            {
                _settleAt = MathF.Min(_settleAt, now);
                return;
            }

            _expireAt = MathF.Min(_expireAt, now + LINGER_SECONDS);
        }

        /// <summary>
        /// Lights the net at <paramref name="amount"/> (0 for none) for the telling of it, not as the alarm (#933): the level's
        /// opening reveal and the line card's blink. Asked of again every frame it should last; a light not renewed for
        /// <see cref="PREVIEW_HOLD_SECONDS"/> goes out over <see cref="PREVIEW_FADE_SECONDS"/> (see the field).
        /// </summary>
        internal void SetPreview(float amount, float now)
        {
            _preview = amount;
            _previewAt = now;
        }

        /// <summary>Back to dark, instantly — a fresh level starts with no leftover warning. Called from BuildLevel.</summary>
        internal void Reset()
        {
            _preview = 0f;
            _previewAt = float.NegativeInfinity;
            _visible = false;
            _transitionAt = 0f;
            _envelopeAtTransition = 0f;
            _flareAt = float.PositiveInfinity;
            _settleAt = float.PositiveInfinity;
            _expireAt = float.PositiveInfinity;
        }

        //Smoothstepped between the value captured at the last flip and the current target, so the fade
        //leaves and arrives at rest. The wall clock is never negative, so a Reset (transition at 0 from 0
        //towards 0) evaluates to dark at any time.
        private float EnvelopeAt(float now)
        {
            float target = _visible ? 1f : 0f;
            float seconds = _visible ? FADE_IN_SECONDS : FADE_OUT_SECONDS;

            float t = Math.Clamp((now - _transitionAt) / seconds, 0f, 1f);
            float smooth = t * t * (3f - 2f * t);

            return _envelopeAtTransition + (target - _envelopeAtTransition) * smooth;
        }

        /// <summary>
        /// The whole net in one draw call. Additive and depth-read but writing no depth, so the island, the
        /// gun and any ball in front hide the beams — the launch smear's states, but not its slot: the net
        /// is drawn <b>after</b> the drain's glass (the funnel lies entirely below it, and glass drawn later
        /// would composite over the depth-less beams and dim them in any shot down the throat) and
        /// <b>before</b> the ceiling plate (which is above, and must composite over it) — see the call site
        /// in <see cref="Screens.GameplayScreen"/>.Draw for the one authoritative statement of that order.
        /// </summary>
        public void Draw(ICamera camera, float now)
        {
            if (_vertexBuffer == null) return;

            float intensity = IntensityAt(now);
            if (intensity <= 0f) return;

            _viewParam.SetValue(camera.View);
            _projectionParam.SetValue(camera.Projection);
            _cameraPositionParam.SetValue(camera.Position);
            _intensityParam.SetValue(intensity);
            _timeParam.SetValue(now);

            BlendState blend = _device.BlendState;
            DepthStencilState depth = _device.DepthStencilState;
            RasterizerState raster = _device.RasterizerState;

            _device.BlendState = BlendState.Additive;
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullNone;

            _device.SetVertexBuffer(_vertexBuffer);
            _device.Indices = _indexBuffer;

            _effect.CurrentTechnique.Passes[0].Apply();
            _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _quadCount * 2);

            _device.BlendState = blend;
            _device.DepthStencilState = depth;
            _device.RasterizerState = raster;
        }

        /// <summary>
        /// How brightly the net burns at <paramref name="now"/>, 0 when it is dark: the envelope times the pulse, with the
        /// kill flare over it, or the light the session asks for to tell of the net (<see cref="SetPreview"/>) if that is
        /// more. The beams and the cones are drawn at exactly this, and so are the four emitters' lenses (#874), which is
        /// what makes the glass and the light leaving it one light. Pure of the wall clock, but it executes the linger's
        /// expiry like <see cref="Draw"/> (the expiry is stamped, and running it twice in a frame changes nothing).
        /// </summary>
        internal float IntensityAt(float now)
        {
            //The linger after a level ends is executed here rather than in an update: the alarm is a rule of
            //the level, and the session stops running its rules the moment the result screen covers it — the
            //frame that does go on under that page is the world's alone (#241). Draws keep coming either way
            //(DrawsUnderlying), so this is the one place still running.
            if (_visible && now >= _expireAt) SetVisible(false, now);

            float preview = PreviewAt(now);

            float envelope = EnvelopeAt(now);
            if (envelope <= 0.001f) return preview;

            float pulse = 0.5f - 0.5f * MathF.Cos(MathHelper.TwoPi * PULSE_HZ * now);
            float intensity = envelope * (PULSE_FLOOR + (1f - PULSE_FLOOR) * pulse * pulse);

            //The kill flare (#434), over the top of all of it: the net that has been warning for the last
            //few shots is now the thing that ended the level, and it should read as cutting rather than as
            //pulsing a little brighter. Eased in, because a step would land on one frame and be gone before
            //the camera arrives.
            if (_flareAt < float.PositiveInfinity)
            {
                float t = MathHelper.Clamp((now - _flareAt) / FLARE_IN, 0f, 1f);
                float flare = MathHelper.SmoothStep(0f, 1f, t);

                //Under a loss's result page, back down to the warning's pulse (#614)
                if (_settleAt < float.PositiveInfinity)
                    flare *= 1f - MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((now - _settleAt) / SETTLE_SECONDS, 0f, 1f));

                intensity = MathHelper.Lerp(intensity, FLARE_INTENSITY, flare);
            }

            return MathF.Max(intensity, preview);
        }

        //The light asked for to tell of the net (SetPreview), as it stands now: as asked while it is renewed, then gone
        private float PreviewAt(float now)
        {
            if (_preview <= 0f) return 0f;

            float stale = now - _previewAt;

            return _preview * (1f - MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((stale - PREVIEW_HOLD_SECONDS) / PREVIEW_FADE_SECONDS, 0f, 1f)));
        }

        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
        }
    }
}
