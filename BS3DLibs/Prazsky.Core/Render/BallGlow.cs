using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A coloured halo around one ball, in that ball's own colour — the cannon saying which round fires next
    /// (#236). One additive camera-facing billboard, drawn depth-read so the ball, the barrel and the cluster
    /// occlude it, and in linear radiance over 1 so it blooms through the glare pass like the emissive balls.
    /// </summary>
    /// <remarks>
    /// <b>The hole in the middle is the depth buffer's, not a figure in the shader.</b> The quad passes through
    /// the ball's centre, so the ball's own front hemisphere is nearer the lens and the depth test discards
    /// that part — what is left is an annulus outside the silhouette. A halo AROUND the ball rather than a wash
    /// OVER it, which is the whole reason this mechanism exists: #236 records two measured dead ends before it
    /// and both fail by adding light to the ball itself (a same-hue flare through the instanced shader's
    /// positive ripple branch was tried at full strength and could not be seen at all, piling energy into a
    /// channel already at the top of the ACES curve; the negative branch that replaces a ball's shading with a
    /// flat colour is one uniform per draw call and cannot carry a single slot's hue). A halo puts colour where
    /// there was <i>none</i> — the dark bore, the sky past the muzzle — and that is why it reads.
    /// <para>
    /// The same test is what makes it read as light escaping the loading window rather than as a sticker on the
    /// gun: from the game camera the barrel is in front of the round, so the tube rejects most of the halo and
    /// what survives comes out of the notch.
    /// </para>
    /// <para>
    /// Shares nothing and owns nothing but its quad: the effect's lifetime is the caller's content manager's,
    /// as with <see cref="LaunchSmears"/> and <c>BallRenderSet</c>.
    /// </para>
    /// </remarks>
    public sealed class BallGlow : IDisposable
    {
        #region The figures

        /// <summary>
        /// The halo's world half-size, as a multiple of a ball's own radius — the <b>default</b>, and what
        /// every figure here was judged against. It has to clear the silhouette by enough that the annulus the
        /// depth test leaves can be seen at all — at 1.0 there would be nothing outside the ball to draw —
        /// while staying inside the loading window, because a halo wider than the notch is one the barrel
        /// simply eats.
        /// <para>
        /// ⚠ <b>That last clause is a defence the OVERVIEW has and a leaned-in lens does not</b> (#321), which
        /// is why <see cref="Draw"/> takes this per call rather than reading the constant. From behind and
        /// above the muzzle the barrel no longer eats anything, so the same figure is not a ring peeking out
        /// of a notch but a screen-space disc over the cells being read. A caller that moves its camera owes
        /// this number an answer; one that does not can leave it alone.
        /// </para>
        /// </summary>
        public const float RADIUS_IN_BALL_RADII = 4.0f;

        /// <summary>
        /// How far over 1 the halo's radiance is pushed. Over the glare threshold on purpose, unlike the mark
        /// it replaces: the point is a coloured bloom around the round, and a halo that stays under the
        /// threshold is a faint ring nobody reads.
        /// </summary>
        private const float BRIGHTNESS = 3.0f;

        /// <summary>
        /// Lowest peak channel the halo may have, so the near-black round still gets a visible grey one rather
        /// than nothing at all. Higher than <see cref="LaunchSmears"/>'s 0.12 for its own reason: a smear is a
        /// bright streak in open air and this sits in a dark bore, where a floor that low would go unseen.
        /// </summary>
        private const float COLOR_FLOOR = 0.22f;

        #endregion

        private readonly GraphicsDevice _device;

        private readonly EffectParameter _viewParam, _projectionParam;
        private readonly EffectParameter _cameraRightParam, _cameraUpParam;
        private readonly EffectParameter _centerParam, _radiusParam, _colorParam, _strengthParam;
        private readonly EffectPass _pass;

        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;

        /// <param name="ballGlowEffect">The compiled <c>Shaders/BallGlow.fx</c>. Handed in and never disposed
        /// here — the caller's content manager owns it.</param>
        public BallGlow(GraphicsDevice device, Effect ballGlowEffect)
        {
            _device = device;

            //Resolved once at load: the by-name indexer is a linear scan, and this is a per-frame draw.
            _viewParam = ballGlowEffect.Parameters["View"];
            _projectionParam = ballGlowEffect.Parameters["Projection"];
            _cameraRightParam = ballGlowEffect.Parameters["CameraRight"];
            _cameraUpParam = ballGlowEffect.Parameters["CameraUp"];
            _centerParam = ballGlowEffect.Parameters["GlowCenter"];
            _radiusParam = ballGlowEffect.Parameters["GlowRadius"];
            _colorParam = ballGlowEffect.Parameters["GlowColor"];
            _strengthParam = ballGlowEffect.Parameters["GlowStrength"];

            _pass = ballGlowEffect.CurrentTechnique.Passes[0];

            CreateQuad();
        }

        /// <summary>
        /// Draws the halo around one ball.
        /// </summary>
        /// <param name="camera">The lens. Its view matrix is where the billboard's basis comes from, so the
        /// quad faces whatever the frame is actually looking through — including a cinematic's.</param>
        /// <param name="center">The ball's world position, which the halo is concentric with.</param>
        /// <param name="ballRadius">The ball's own radius, which <see cref="RADIUS_IN_BALL_RADII"/> scales.</param>
        /// <param name="srgbTint">The ball's diffuse tint as authored, sRGB — the same value
        /// <see cref="LaunchSmears.Add"/> takes, and decoded the same way here so the two agree about what a
        /// ball's colour looks like as light.</param>
        /// <param name="strength">The breath, 0…1. Zero draws nothing, so a caller with no round loaded does
        /// not have to know that.</param>
        /// <param name="radiusInBallRadii">How far the halo reaches, in ball radii. Defaults to
        /// <see cref="RADIUS_IN_BALL_RADII"/>, which is the figure this class is tuned at; a caller whose lens
        /// takes away the barrel's own occlusion of it should pass less (#321).</param>
        public void Draw(ICamera camera, Vector3 center, float ballRadius, Vector3 srgbTint, float strength,
            float radiusInBallRadii = RADIUS_IN_BALL_RADII)
        {
            if (strength <= 0f) return;

            Vector3 linear = ColorSpace.SrgbToLinear(srgbTint);

            //Hue kept, peak lifted to the floor: the near-black round is exactly the one whose colour the
            //player cannot read off the gun, so it is the last one that may glow with nothing.
            float peak = MathF.Max(linear.X, MathF.Max(linear.Y, linear.Z));
            if (peak < COLOR_FLOOR) linear *= COLOR_FLOOR / MathF.Max(peak, 1e-4f);

            //The view basis, off the view matrix's rows: with a row-vector convention the transposed rotation's
            //columns are the camera's axes, which is what M11..M32 read out here.
            Matrix view = camera.View;
            Vector3 right = new(view.M11, view.M21, view.M31);
            Vector3 up = new(view.M12, view.M22, view.M32);

            _viewParam.SetValue(view);
            _projectionParam.SetValue(camera.Projection);
            _cameraRightParam.SetValue(right);
            _cameraUpParam.SetValue(up);
            _centerParam.SetValue(center);
            _radiusParam.SetValue(ballRadius * radiusInBallRadii);
            _colorParam.SetValue(linear * BRIGHTNESS);
            _strengthParam.SetValue(strength);

            //States put back exactly as found, so the frame's translucent baseline still stands for the glass
            //drawn after this — the same contract LaunchSmears.Draw keeps.
            BlendState blend = _device.BlendState;
            DepthStencilState depth = _device.DepthStencilState;
            RasterizerState raster = _device.RasterizerState;

            _device.BlendState = BlendState.Additive;
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullNone;

            _device.SetVertexBuffer(_vertexBuffer);
            _device.Indices = _indexBuffer;

            _pass.Apply();
            _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);

            _device.BlendState = blend;
            _device.DepthStencilState = depth;
            _device.RasterizerState = raster;
        }

        /// <summary>
        /// The billboard: a unit quad whose texture channel carries the corner in −1…1 on both axes, which the
        /// pixel shader takes the round falloff off. The vertex positions are unused — the shader places the
        /// quad from the centre and the view basis — so this one buffer serves every draw.
        /// </summary>
        private void CreateQuad()
        {
            VertexPositionTexture[] corners =
            {
                new(Vector3.Zero, new Vector2(-1f, -1f)),
                new(Vector3.Zero, new Vector2(1f, -1f)),
                new(Vector3.Zero, new Vector2(-1f, 1f)),
                new(Vector3.Zero, new Vector2(1f, 1f))
            };

            _vertexBuffer = new VertexBuffer(_device, VertexPositionTexture.VertexDeclaration, corners.Length,
                BufferUsage.WriteOnly);
            _vertexBuffer.SetData(corners);

            short[] indices = { 0, 1, 2, 2, 1, 3 };

            _indexBuffer = new IndexBuffer(_device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices);
        }

        /// <summary>The two buffers, which are everything this component made.</summary>
        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();

            _vertexBuffer = null;
            _indexBuffer = null;
        }
    }
}
