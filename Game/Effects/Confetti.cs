using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The campaign's closing celebration (#215): paper confetti tumbling down around the camera, started when
    /// the last level of the set is cleared and left to run over the result screen behind the score.
    /// <para>
    /// It lives on the <b>host</b> rather than on the gameplay screen, for the reason <see cref="Fireworks"/>
    /// does and which is worth repeating because it is the one structural fact about every celebration here: a
    /// cleared level hands the player a result page pushed <i>over</i> the session, and a covered screen is not
    /// updated — so a celebration owned by the session would freeze at the exact moment it is meant to be
    /// watched.
    /// </para>
    /// <para>
    /// One static vertex buffer and <b>one draw call</b>, the idiom <c>Snow.fx</c> established and
    /// <see cref="Fireworks"/> followed: every piece is animated in <c>Confetti.fx</c>'s vertex shader out of a
    /// clock and its own baked constants, so nothing is rebuilt or re-uploaded per frame and the C# side owns
    /// only the clock, the fade and the camera.
    /// </para>
    /// <para>
    /// <b>Why this is a separate effect rather than a longer barrage.</b> #184 had already established that the
    /// way to make a celebration read as bigger is to lengthen its opening rather than its total, and the
    /// campaign ending needed something past the top of that dial — the fireworks are already at maximum
    /// density for eight seconds when a block completes. More of the same is not more: the campaign's ending
    /// has to be a different *kind* of thing, and confetti is the one that reads as a ceremony rather than as a
    /// bigger display. It runs <i>alongside</i> the fireworks and does not replace them.
    /// </para>
    /// </summary>
    public sealed class Confetti : IDisposable
    {
        //Pieces in the field. The ceiling is the 16-bit index buffer: times four vertices it must stay under
        //65 536, so this may not pass 16 384 without moving to 32-bit indices (the lesson CreateGridMesh's
        //grids taught, and the one Fireworks records from the other side). At 4 000 that is 16 000 vertices.
        //
        //IT SHIPPED AT 4 000 AND THAT WAS TOO FEW, which is the fireworks' own lesson arriving a second time
        //and from the same direction. Photographed on the front end it read as a pleasant confetti fall rather
        //than as a celebration: the pieces were individually legible, evenly spaced, and the eye counted them.
        //A celebration is read as big by DENSITY before anything else — the same finding that took a burst from
        //120 sparks to 320 — so this is 9 000, and what changed is that the frame is now a mess of colour with
        //no gaps for the eye to rest in. It costs nothing measurable (see the figure in docs/game-feedback.md).
        //
        //The ceiling is the 16-bit index buffer: times four vertices it must stay under 65 536, so this may not
        //pass 16 384 without moving to 32-bit indices. At 9 000 that is 36 000 vertices, comfortably inside.
        private const int PIECES = 9000;

        //The volume the pieces fill around the camera. Tighter than Snow's 70x55x70 on purpose: the density
        //that matters is the density near the lens, and spreading the same count over a bigger box spends it
        //where the pieces are a few pixels across.
        private static readonly Vector3 BOX_SIZE = new(58f, 48f, 58f);

        //How far above the lens the box's centre sits. Confetti comes DOWN past you, so the camera belongs low
        //in the volume rather than in the middle of it — and this camera in particular spends the whole
        //celebration looking UP, at the hanging cluster and then at an arena it is orbiting, so a box centred on
        //the lens would spend most of its pieces under the bottom of the frame. At 9 against a box half-height
        //of 24 the lens sits about a third of the way up, which leaves plenty still falling past it.
        private const float BOX_LIFT = 9f;

        //Paper falls slowly and wanders while it does. FALL_SPEED is under half the snow's 9, and the flutter
        //is nearly three units wide against snow's 1.2 sway — a chip of paper stalls and slips sideways, and
        //that wander is most of what separates it from a dropped stone.
        private const float FALL_SPEED = 3.9f;
        private const float FLUTTER = 2.7f, FLUTTER_RATE = 1.35f;

        //A slow lateral drift over the whole field, so the fall has a direction and is not a column.
        private static readonly Vector2 DRIFT = new(1.6f, -1.1f);

        //Half-extents of a piece: 0.34 x 0.20 world units, i.e. a chip about a third of a ball across the
        //tumble axis and rather narrower along it. A square piece reads as a speck however it turns; the
        //oblong is what makes the edge-on flash a *line*.
        private static readonly Vector2 SIZE = new(0.17f, 0.10f);

        //Radians per second of tumble, scaled per piece. Fast enough that a piece flashes several times on
        //its way down the frame — the flash is the effect, and a piece that turns once in three seconds
        //simply reads as a coloured speck drifting.
        private const float SPIN = 4.6f;

        //How close a piece may come to the lens before it is faded out. Far tighter than Snow's 7: a big
        //near piece rushing past is most of what sells this, and fading them as eagerly as snow does throws
        //the best of the effect away. Under about a unit it is a smear across the frame, so that is the line.
        private const float NEAR_FADE = 1.15f;

        //One light for the paper, not the scene's rig, and one ambient floor under it. This runs over any of
        //thirteen scenes and eighteen domes, and a celebration that went black in the cavern would be a hole
        //in the party — so the paper carries its own light and looks the same everywhere.
        private static readonly Vector3 LIGHT = Vector3.Normalize(new Vector3(0.35f, 0.86f, 0.38f));
        private const float AMBIENT = 0.34f;

        //Seconds to fade the field in at the start and out at the end. In rather than on, because a full
        //field appearing between two frames reads as a bug; out rather than off, for the same reason at the
        //other end. The fade-in is quick — this answers a moment — and the fade-out is slow, so the party
        //thins rather than stopping.
        private const float FADE_IN = 0.7f, FADE_OUT = 3.5f;

        //Linear radiance. GLARE_THRESHOLD is 0.55 on luminance, and unlike the fireworks these are meant to
        //sit mostly UNDER it: paper is lit, not luminous, and confetti that bloomed would read as embers
        //falling on the arena. Only the brightest broadside flashes of the white and gold cross over, which
        //is exactly the glint a foil chip catching the light actually has.
        private static readonly Vector3[] PALETTE =
        {
            new(0.94f, 0.16f, 0.22f),   //red
            new(1.00f, 0.55f, 0.10f),   //orange
            new(1.00f, 0.86f, 0.24f),   //gold
            new(0.22f, 0.84f, 0.32f),   //green
            new(0.18f, 0.48f, 0.96f),   //blue
            new(0.76f, 0.24f, 0.94f),   //violet
            new(0.24f, 0.88f, 0.86f),   //cyan
            new(0.98f, 0.96f, 0.92f)    //white
        };

        private readonly GraphicsDevice _device;
        private readonly Effect _effect;

        private readonly VertexBuffer _vertexBuffer;
        private readonly IndexBuffer _indexBuffer;

        //Cached parameter handles: the by-name indexer is a linear scan, and these are set every frame.
        private readonly EffectParameter _viewParam, _projectionParam, _cameraPositionParam;
        private readonly EffectParameter _timeParam, _intensityParam;

        private float _time;
        private float _remaining;     //seconds of fall left before the fade-out begins
        private float _intensity;     //0..1, ramped rather than switched

        /// <summary>True while anything is still falling, so a caller can hold a screen until it is over.</summary>
        public bool Active => _remaining > 0f || _intensity > 0f;

        public Confetti(GraphicsDevice device, Effect effect)
        {
            _device = device;
            _effect = effect;

            _viewParam = effect.Parameters["View"];
            _projectionParam = effect.Parameters["Projection"];
            _cameraPositionParam = effect.Parameters["CameraPosition"];
            _timeParam = effect.Parameters["ConfettiTime"];
            _intensityParam = effect.Parameters["ConfettiIntensity"];

            //Set once: none of these changes for the life of the field.
            effect.Parameters["ConfettiBoxSize"].SetValue(BOX_SIZE);
            effect.Parameters["ConfettiBoxLift"].SetValue(BOX_LIFT);
            effect.Parameters["ConfettiFallSpeed"].SetValue(FALL_SPEED);
            effect.Parameters["ConfettiFlutter"].SetValue(FLUTTER);
            effect.Parameters["ConfettiFlutterRate"].SetValue(FLUTTER_RATE);
            effect.Parameters["ConfettiDrift"].SetValue(DRIFT);
            effect.Parameters["ConfettiSize"].SetValue(SIZE);
            effect.Parameters["ConfettiSpin"].SetValue(SPIN);
            effect.Parameters["ConfettiNearFade"].SetValue(NEAR_FADE);
            effect.Parameters["ConfettiLight"].SetValue(LIGHT);
            effect.Parameters["ConfettiAmbient"].SetValue(AMBIENT);

            BuildBuffers(out _vertexBuffer, out _indexBuffer);
        }

        /// <summary>
        /// Start (or extend) a fall lasting <paramref name="seconds"/>. Safe to call while one is already
        /// running — it takes the longer of the two rather than restarting, so a second call cannot cut the
        /// celebration short. <see cref="Fireworks.Celebrate"/>'s rule, for the same reason.
        /// </summary>
        public void Celebrate(float seconds) => _remaining = MathF.Max(_remaining, seconds);

        /// <summary>Ends the fall at once. Called when a level is built, or the next level opens under it.</summary>
        public void Stop()
        {
            _remaining = 0f;
            _intensity = 0f;
        }

        /// <summary>
        /// Advances the one clock the whole field is a function of, and ramps the fade at either end.
        /// </summary>
        /// <remarks>
        /// The clock is never reset, and that is deliberate: every piece's position is <c>frac()</c> of it, so
        /// a reset would teleport the entire field at once. Starting a second celebration therefore picks the
        /// fall up wherever it happens to be, which is invisible — the pieces are already at every phase of
        /// their cycle — where a reset would be a very visible flicker.
        /// </remarks>
        public void Update(float elapsed)
        {
            if (_remaining <= 0f && _intensity <= 0f) return;

            _time += elapsed;

            if (_remaining > 0f)
            {
                _remaining -= elapsed;
                _intensity = MathF.Min(1f, _intensity + elapsed / FADE_IN);
            }
            else
            {
                _intensity = MathF.Max(0f, _intensity - elapsed / FADE_OUT);
            }
        }

        /// <summary>Draws the whole field in one call. Nothing at all while it is idle.</summary>
        public void Draw(ICamera camera)
        {
            if (_intensity <= 0f) return;

            _viewParam.SetValue(camera.View);
            _projectionParam.SetValue(camera.Projection);
            _cameraPositionParam.SetValue(camera.Position);
            _timeParam.SetValue(_time);
            _intensityParam.SetValue(_intensity);

            BlendState blend = _device.BlendState;
            DepthStencilState depth = _device.DepthStencilState;
            RasterizerState raster = _device.RasterizerState;

            //Alpha-blended and NOT additive, which is the whole difference from the fireworks: a firework is
            //light and adds to what is behind it, where a chip of paper is an object that hides it. Depth-read
            //so the island and the cluster occlude it, and CullNone because a tumbling piece shows both faces
            //by design — it is printed on both, and culling would blink half the field out twice a turn.
            _device.BlendState = BlendState.NonPremultiplied;
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullNone;

            _device.SetVertexBuffer(_vertexBuffer);
            _device.Indices = _indexBuffer;

            _effect.CurrentTechnique.Passes[0].Apply();
            _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, PIECES * 2);

            _device.BlendState = blend;
            _device.DepthStencilState = depth;
            _device.RasterizerState = raster;
        }

        /// <summary>
        /// Builds the one static buffer. Everything that makes one piece differ from another is baked here —
        /// where it starts, what colour it is, how fast it falls and tumbles and about which axis — because the
        /// shader's whole trick is that a piece's pose is a pure function of one clock, and anything hashed per
        /// frame would make it jitter.
        /// </summary>
        private void BuildBuffers(out VertexBuffer vertexBuffer, out IndexBuffer indexBuffer)
        {
            ConfettiVertex[] vertices = new ConfettiVertex[PIECES * 4];
            short[] indices = new short[PIECES * 6];

            //A deterministic generator: the field is fixed for the life of the program, exactly as the
            //fireworks' burst pattern is. Nothing about the celebration is improved by it differing between
            //runs, and a static buffer is what pays for the one draw call.
            Random random = new(20260814);

            int v = 0, n = 0;
            for (int piece = 0; piece < PIECES; piece++)
            {
                Vector3 basePosition = new(
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble());

                float rand = (float)random.NextDouble();

                //The tumble axis, BIASED HORIZONTAL. A chip falling flat spins about a vertical axis and
                //never presents an edge to the lens, so it never flashes — and the flash is the effect. Held
                //off the exact horizontal by a little, or the whole field would flip in one plane like a
                //shoal of fish.
                Vector3 axis = new(
                    (float)(random.NextDouble() * 2.0 - 1.0),
                    (float)(random.NextDouble() * 2.0 - 1.0) * 0.35f,
                    (float)(random.NextDouble() * 2.0 - 1.0));

                if (axis.LengthSquared() < 1e-4f) axis = Vector3.UnitX;
                axis.Normalize();

                float phase = (float)(random.NextDouble() * MathHelper.TwoPi);

                //Spin and fall both spread wide. A field where every piece turns at one rate reads as a
                //mechanism; the spread is what makes it a scatter of paper.
                float spinRate = 0.55f + (float)random.NextDouble() * 0.9f;
                float fallScale = 0.7f + (float)random.NextDouble() * 0.6f;
                float sizeScale = 0.72f + (float)random.NextDouble() * 0.7f;

                Vector3 tint = PALETTE[random.Next(PALETTE.Length)];

                for (int corner = 0; corner < 4; corner++)
                {
                    float cx = (corner == 0 || corner == 3) ? -1f : 1f;
                    float cy = (corner < 2) ? 1f : -1f;

                    vertices[v + corner] = new ConfettiVertex
                    {
                        Base = new Vector4(basePosition, rand),
                        Corner = new Vector4(cx, cy, sizeScale, spinRate),
                        Axis = new Vector4(axis, phase),
                        Tint = new Vector4(tint, fallScale)
                    };
                }

                indices[n++] = (short)(v + 0);
                indices[n++] = (short)(v + 1);
                indices[n++] = (short)(v + 2);
                indices[n++] = (short)(v + 0);
                indices[n++] = (short)(v + 2);
                indices[n++] = (short)(v + 3);

                v += 4;
            }

            vertexBuffer = new VertexBuffer(_device, ConfettiVertex.Declaration, vertices.Length, BufferUsage.WriteOnly);
            vertexBuffer.SetData(vertices);

            indexBuffer = new IndexBuffer(_device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            indexBuffer.SetData(indices);
        }

        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
        }

        /// <summary>One corner of one piece. Everything the vertex shader needs to place and turn it.</summary>
        private struct ConfettiVertex : IVertexType
        {
            public Vector4 Base;     //(base position in the unit cube, per-piece random)
            public Vector4 Corner;   //(corner x, corner y, size scale, spin rate)
            public Vector4 Axis;     //(tumble axis xyz, tumble phase)
            public Vector4 Tint;     //(linear rgb, fall-speed scale)

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0),
                new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
                new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
                new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3));

            readonly VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }
    }
}
