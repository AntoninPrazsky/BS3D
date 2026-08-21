using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The floor alarm: a grid of pulsing red laser beams hovering on the plane a ball's surface would
    /// touch at the moment the level is lost, shown while the descending ceiling is within two more steps
    /// of pushing the cluster past the death line. It is the ceiling flash's counterpart at the other end
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
    /// draw call, three per-frame uniforms. Each beam is a quad billboarded about its own axis in the
    /// vertex shader (see <c>LaserGrid.fx</c>) — the play camera stands barely below the grid's plane, and
    /// a flat quad lying in it would be seen edge-on and vanish.
    /// </para>
    /// </summary>
    internal sealed class LaserGrid : IDisposable
    {
        //Beam spacing is the lattice's own cell pitch, so the net reads as sized to the balls it is
        //waiting for; the margin bleeds it a little past the footprint so the outermost cells do not sit
        //on its very edge. The whole grid must stay inside the drain's mouth (radius 14) so it never
        //crosses the gold bead — the footprint of every authored level does, by a wide margin.
        private const float BEAM_PITCH = 1f;
        private const float FOOTPRINT_MARGIN = 0.75f;

        //Half-thickness of a beam. Thin on purpose: the red halo and the glare's bloom carry the visual
        //weight, and a fat beam reads as a glowing bar rather than a laser.
        private const float BEAM_HALF_WIDTH = 0.055f;

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

        /// <summary>One corner of one beam's quad. Both endpoints ride along so the shader needs no per-beam uniforms.</summary>
        private struct LaserVertex : IVertexType
        {
            public Vector3 Start;
            public Vector3 End;
            public Vector2 Data;    //(side in {-1,1}, along in {0,1})

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 0),
                new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 1));

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
            //drift apart) and the width never changes. A parameter's value persists on the effect.
            effect.Parameters["LaserColor"].SetValue(color);
            effect.Parameters["LaserHalfWidth"].SetValue(BEAM_HALF_WIDTH);
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

            halfX += FOOTPRINT_MARGIN;
            halfZ += FOOTPRINT_MARGIN;

            //Symmetric about the origin: a centre beam on each axis and as many more as whole pitches fit
            //inside the half-extent, so the outermost pair always lands inside the footprint.
            int alongX = (int)(halfZ / BEAM_PITCH) * 2 + 1;     //beams running along X are spaced in Z
            int alongZ = (int)(halfX / BEAM_PITCH) * 2 + 1;
            _quadCount = alongX + alongZ;

            LaserVertex[] vertices = new LaserVertex[_quadCount * 4];
            short[] indices = new short[_quadCount * 6];

            int quad = 0;

            for (int i = 0; i < alongX; i++)
            {
                float z = (i - (alongX - 1) / 2) * BEAM_PITCH;
                AddBeam(vertices, indices, ref quad, new Vector3(-halfX, y, z), new Vector3(halfX, y, z));
            }

            for (int i = 0; i < alongZ; i++)
            {
                float x = (i - (alongZ - 1) / 2) * BEAM_PITCH;
                AddBeam(vertices, indices, ref quad, new Vector3(x, y, -halfZ), new Vector3(x, y, halfZ));
            }

            _vertexBuffer = new VertexBuffer(_device, LaserVertex.Declaration, vertices.Length, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(vertices);

            _indexBuffer = new IndexBuffer(_device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices);
        }

        private static void AddBeam(LaserVertex[] vertices, short[] indices, ref int quad, Vector3 start, Vector3 end)
        {
            int v = quad * 4;
            int n = quad * 6;

            vertices[v + 0] = new LaserVertex { Start = start, End = end, Data = new Vector2(-1f, 0f) };
            vertices[v + 1] = new LaserVertex { Start = start, End = end, Data = new Vector2(1f, 0f) };
            vertices[v + 2] = new LaserVertex { Start = start, End = end, Data = new Vector2(-1f, 1f) };
            vertices[v + 3] = new LaserVertex { Start = start, End = end, Data = new Vector2(1f, 1f) };

            indices[n + 0] = (short)(v + 0);
            indices[n + 1] = (short)(v + 1);
            indices[n + 2] = (short)(v + 2);
            indices[n + 3] = (short)(v + 2);
            indices[n + 4] = (short)(v + 1);
            indices[n + 5] = (short)(v + 3);

            quad++;
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
        /// The level's ending has just been put in front of the player — the result screen is going up. A net
        /// still standing keeps pulsing for <see cref="LINGER_SECONDS"/> and then goes out — <see cref="Draw"/>
        /// executes the expiry, because it is the only thing still running under the result page. <c>Min</c>,
        /// so a second presentation cannot push a running linger out.
        /// </summary>
        internal void NoticeLevelEnded(float now)
        {
            if (!_visible) return;

            _expireAt = MathF.Min(_expireAt, now + LINGER_SECONDS);
        }

        /// <summary>Back to dark, instantly — a fresh level starts with no leftover warning. Called from BuildLevel.</summary>
        internal void Reset()
        {
            _visible = false;
            _transitionAt = 0f;
            _envelopeAtTransition = 0f;
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

            //The linger after a level ends is executed here rather than in an update: the alarm is a rule of
            //the level, and the session stops running its rules the moment the result screen covers it — the
            //frame that does go on under that page is the world's alone (#241). Draws keep coming either way
            //(DrawsUnderlying), so this is the one place still running.
            if (_visible && now >= _expireAt) SetVisible(false, now);

            float envelope = EnvelopeAt(now);
            if (envelope <= 0.001f) return;

            float pulse = 0.5f - 0.5f * MathF.Cos(MathHelper.TwoPi * PULSE_HZ * now);
            float intensity = envelope * (PULSE_FLOOR + (1f - PULSE_FLOOR) * pulse * pulse);

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

        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
        }
    }
}
