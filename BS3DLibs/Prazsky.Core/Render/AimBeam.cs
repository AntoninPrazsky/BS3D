using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>Where the gun is pointing</b> — a dashed line of light out of the muzzle along the aim, crawling
    /// towards its far end. It answers the question the overview camera cannot: that lens looks <i>at</i> the
    /// cluster rather than along the bore, so the screen's centre is not where the gun points and a crosshair
    /// there would name the wrong spot (which is why the game only draws one while precise aim is leaning in).
    /// Without this, aiming from the overview is read off the barrel's foreshortened angle alone.
    /// <para>
    /// <b>The far end carries the meaning.</b> The caller ends the beam at whatever a shot fired now would
    /// reach, so it stops at the ball it would touch, and hands the colour to match: the loaded ball's tint
    /// where the shot sticks, a refusal colour where it will not. Ended in open air instead — the aim reaching
    /// nothing at all — the dashes are faded out along the way (<c>openEnded</c>) so the line dies away rather
    /// than stopping in mid-air as if something were there.
    /// </para>
    /// </summary>
    /// <remarks>
    /// It borrows <c>Shaders/ShotTrail.fx</c> from <see cref="LaunchSmears"/> rather than bringing a shader of
    /// its own: that shader is already a camera-facing billboard between two world points, and it fades each
    /// segment in over its first quarter and out over its last, so a <i>short</i> segment comes out as a dash
    /// with soft ends and no new code at all. One dash is one draw of the shared quad.
    /// <para>
    /// <b>The widths are pushed per draw, and they have to be.</b> <see cref="LaunchSmears"/> set them once in
    /// its constructor, correctly, on the reasoning that a compile-time constant need not be re-sent per frame
    /// (<c>BestPractices.md</c> §1 names those two parameters as its own example). That stopped being true the
    /// moment a second component shared the effect: the two want different widths, an <c>Effect</c>'s parameter
    /// values are the effect's and not the caller's, and whichever constructor ran last would otherwise decide
    /// how both look. Both now set them once per <c>Draw</c>, which is per frame per component and still not
    /// per primitive.
    /// </para>
    /// </remarks>
    public sealed class AimBeam : IDisposable
    {
        #region The dials

        /// <summary>World length of one dash plus the gap after it — the pattern's period along the line.</summary>
        private const float SPACING = 1.1f;

        /// <summary>How much of a period is lit. Under a half so the line reads as dashes rather than as a rod.</summary>
        private const float DASH_FRACTION = 0.42f;

        /// <summary>
        /// Half-width of a dash. A ball's radius is <c>0.5</c>, so this is a line about a fifth of a ball
        /// across: enough to read at the overview's stand-off, thin enough not to hide the cell it points at.
        /// </summary>
        private const float WIDTH = 0.085f;

        /// <summary>
        /// Radiance boost. Well under <see cref="LaunchSmears"/>'s, deliberately — the smear is a shot going
        /// off and should flare, while this is up the whole time the gun is aimed and would be exhausting at
        /// the same strength. It still sits over 1 so the line glows rather than looking painted on.
        /// </summary>
        private const float BRIGHTNESS = 1.35f;

        /// <summary>Lowest peak channel, so the near-black ball still gives a visible grey line.</summary>
        private const float COLOR_FLOOR = 0.2f;

        /// <summary>World units a second the dash pattern crawls towards the far end. Slow: a hint of travel, not a barber's pole.</summary>
        private const float CRAWL = 1.6f;

        /// <summary>
        /// A dash shorter than this fraction of a full one is skipped. The pattern is clipped at both ends of
        /// the line, so without this the ends flicker a one-pixel sliver in and out as the crawl moves.
        /// </summary>
        private const float MIN_DASH_FRACTION = 0.25f;

        /// <summary>
        /// Where the fade starts on an open-ended beam, as a fraction of its length. Before it the line is
        /// full strength; after it the dashes ease to nothing by the tip.
        /// </summary>
        private const float OPEN_FADE_FROM = 0.35f;

        /// <summary>
        /// A hard cap on dashes per beam, so a caller handing over an absurd length cannot turn one frame into
        /// thousands of draws. At <see cref="SPACING"/> this is ~70 world units, well past anything the gun can
        /// see across the field.
        /// </summary>
        private const int MAX_DASHES = 64;

        #endregion

        private readonly GraphicsDevice _device;

        //Cached at construction, per BestPractices.md §1: the by-name indexer is a string scan of the whole
        //parameter collection, and the first three go out once a frame while the last two go out PER DASH.
        private readonly EffectParameter _viewParam, _projectionParam, _cameraPositionParam;
        private readonly EffectParameter _headParam, _tailParam, _colorParam, _alphaParam;
        private readonly EffectParameter _headWidthParam, _tailWidthParam;

        private readonly EffectPass _pass;

        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;

        /// <param name="shotTrailEffect">The compiled <c>Shaders/ShotTrail.fx</c>, the same instance
        /// <see cref="LaunchSmears"/> is given. Handed in and never disposed here — the caller's content
        /// manager owns its lifetime.</param>
        public AimBeam(GraphicsDevice device, Effect shotTrailEffect)
        {
            _device = device;

            _viewParam = shotTrailEffect.Parameters["View"];
            _projectionParam = shotTrailEffect.Parameters["Projection"];
            _cameraPositionParam = shotTrailEffect.Parameters["CameraPosition"];
            _headParam = shotTrailEffect.Parameters["TrailHead"];
            _tailParam = shotTrailEffect.Parameters["TrailTail"];
            _colorParam = shotTrailEffect.Parameters["TrailColor"];
            _alphaParam = shotTrailEffect.Parameters["TrailAlpha"];
            _headWidthParam = shotTrailEffect.Parameters["TrailHeadWidth"];
            _tailWidthParam = shotTrailEffect.Parameters["TrailTailWidth"];

            _pass = shotTrailEffect.CurrentTechnique.Passes[0];

            CreateQuad();
        }

        /// <summary>
        /// Draws the beam from <paramref name="muzzle"/> to <paramref name="target"/>.
        /// </summary>
        /// <param name="camera">The frame's camera; the dashes turn to face it.</param>
        /// <param name="muzzle">Where a ball would leave the bore — the same point the shot itself starts from.</param>
        /// <param name="target">Where a shot fired now would arrive, or a point out along the aim when it arrives nowhere.</param>
        /// <param name="srgbTint">The line's colour as authored (sRGB). Decoded to linear here, its hue kept but
        /// its peak lifted to <see cref="COLOR_FLOOR"/> and then boosted, which is the same rule
        /// <see cref="LaunchSmears"/> applies to a smear and for the same reason.</param>
        /// <param name="phase">A seconds clock. Only its fractional position within a period matters, so the
        /// wall clock serves — and being the wall clock, the crawl keeps going while a pause holds the session.</param>
        /// <param name="openEnded">True when <paramref name="target"/> is open air rather than something the shot
        /// would hit, which fades the dashes out along the way instead of ending them at a point.</param>
        public void Draw(ICamera camera, Vector3 muzzle, Vector3 target, Vector3 srgbTint, float phase,
            bool openEnded)
        {
            Vector3 axis = target - muzzle;
            float length = axis.Length();
            if (length < SPACING * MIN_DASH_FRACTION) return;

            Vector3 direction = axis / length;

            Vector3 linear = ColorSpace.SrgbToLinear(srgbTint);
            float peak = MathF.Max(linear.X, MathF.Max(linear.Y, linear.Z));
            if (peak < COLOR_FLOOR) linear *= COLOR_FLOOR / MathF.Max(peak, Constants.THOUSANDTH);

            _viewParam.SetValue(camera.View);
            _projectionParam.SetValue(camera.Projection);
            _cameraPositionParam.SetValue(camera.Position);
            _colorParam.SetValue(linear * BRIGHTNESS);

            //Equal, so a dash is a parallel-sided line rather than a wedge. Per draw and not per construction --
            //see the remarks: the effect is shared with LaunchSmears, which wants its own two widths.
            _headWidthParam.SetValue(WIDTH);
            _tailWidthParam.SetValue(WIDTH);

            BlendState blend = _device.BlendState;
            DepthStencilState depth = _device.DepthStencilState;
            RasterizerState raster = _device.RasterizerState;

            _device.BlendState = BlendState.Additive;
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullNone;

            _device.SetVertexBuffer(_vertexBuffer);
            _device.Indices = _indexBuffer;

            float dashLength = SPACING * DASH_FRACTION;
            float minDash = SPACING * MIN_DASH_FRACTION;

            //The pattern's origin walks towards the target and wraps every period, so the dashes appear to
            //travel without any one of them being tracked. Starting one period behind the muzzle is what lets a
            //dash be clipped INTO existence at the near end rather than popping in whole.
            float origin = (phase * CRAWL) % SPACING - SPACING;

            int drawn = 0;

            for (float start = origin; start < length && drawn < MAX_DASHES; start += SPACING)
            {
                float from = MathF.Max(start, 0f);
                float to = MathF.Min(start + dashLength, length);
                if (to - from < minDash) continue;

                float alpha = 1f;

                if (openEnded)
                {
                    //Eased over the tail of the line rather than cut, so the beam thins out into the air
                    float t = (to / length - OPEN_FADE_FROM) / (1f - OPEN_FADE_FROM);
                    if (t > 0f) alpha = 1f - t * t;
                    if (alpha <= 0f) continue;
                }

                _tailParam.SetValue(muzzle + direction * from);
                _headParam.SetValue(muzzle + direction * to);
                _alphaParam.SetValue(alpha);

                _pass.Apply();
                _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);

                drawn++;
            }

            _device.BlendState = blend;
            _device.DepthStencilState = depth;
            _device.RasterizerState = raster;
        }

        /// <summary>
        /// The billboard, exactly <see cref="LaunchSmears"/>'s: a unit quad whose texture channel carries
        /// (side in {-1,1}, along in {0 tail, 1 head}), placed in world space by the shader from the two ends.
        /// The vertex positions are unused, so this one quad serves every dash of every frame.
        /// </summary>
        private void CreateQuad()
        {
            VertexPositionTexture[] corners =
            {
                new(Vector3.Zero, new Vector2(-1f, 0f)), //tail, left
                new(Vector3.Zero, new Vector2(1f, 0f)),  //tail, right
                new(Vector3.Zero, new Vector2(-1f, 1f)), //head, left
                new(Vector3.Zero, new Vector2(1f, 1f))   //head, right
            };

            _vertexBuffer = new VertexBuffer(_device, VertexPositionTexture.VertexDeclaration, corners.Length,
                BufferUsage.WriteOnly);
            _vertexBuffer.SetData(corners);

            short[] indices = { 0, 1, 2, 2, 1, 3 };

            _indexBuffer = new IndexBuffer(_device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices);
        }

        /// <summary>
        /// The quad's two buffers, which are everything this component made — <b>not</b> the effect, which the
        /// caller's content manager owns.
        /// </summary>
        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();

            _vertexBuffer = null;
            _indexBuffer = null;
        }
    }
}
