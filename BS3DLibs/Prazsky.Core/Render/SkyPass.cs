using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The sky of a scene under the dome (#1104): the dome (<see cref="SkyDome"/> through <c>Sky.fx</c>: the gradient, the
    /// sun's disc, the cloud deck) and on High and Ultra the heaps over it (<see cref="CloudVolumes"/>, #989), drawn by the
    /// Game and the Testbed <b>behind the opaque scene</b>. The map editor and the Potato path draw their dome first, as
    /// they always have.
    /// <para>
    /// <b>Behind, at the far plane.</b> <c>Sky.fx</c> is among the dearest pixel programs in the frame, and drawn first with
    /// the depth test off it shaded every pixel above the horizon, most of which the ground, the trees, the towers and the
    /// island then painted over. Drawn after them, depth-read, with the viewport's depth range closed onto the far plane
    /// (<see cref="Viewport.MinDepth"/> and <see cref="Viewport.MaxDepth"/> both 1, so the dome's mesh and the heaps' quad
    /// land at depth 1 whatever their own), every pixel something opaque already owns is turned away before the program
    /// runs, the way the Moon's and the aurora's skies have always been drawn. The vertex program and the clipping are
    /// untouched, so where the sky is drawn it is the same sky to the bit.
    /// </para>
    /// <para>
    /// <b>Why no pixel moves.</b> A pixel is either sky or something opaque that wrote its depth. Before, the sky was painted
    /// first and the opaque thing over it; now the opaque thing is painted over the clear colour and the sky is turned away
    /// there. Where nothing opaque stands, the sky passes the test against the cleared depth (1, less-or-equal) and is
    /// painted as before. What the environment blends over the sky without writing depth (the sea, the storm, Mars's moons,
    /// the volcano's fountains) is its second half, drawn after this (the gap between <c>SceneRenderer.DrawEnvironmentOpaque</c>
    /// and <c>DrawEnvironmentBlended</c>, #1098), so it still blends over the sky. Shown on the tonemapped frame, old order
    /// against new in one process ("The sky is drawn behind the opaque scene" in docs/scenes/backdrop-architecture.md).
    /// </para>
    /// <para>
    /// ⚠ <b>What would break it</b>: a draw before the gap that lets what is behind it show through (a blend, an alpha fade)
    /// or writes no depth where the sky is behind it. It would have blended over the sky and now meets the clear colour, or
    /// is painted over by the sky. Every scene's first half, the city, the hosts' wood and the fireflies are opaque and write
    /// depth. And the sky cannot wait for the gun and the cluster: from under the plate the ceiling's glass bends a copy of
    /// the frame taken before they are drawn (#541), which must hold the sky.
    /// </para>
    /// <para>
    /// ⚠ <b>The far plane itself</b>: what stands within half a 24-bit depth step of it stores a depth of exactly 1, and the
    /// sky at 1 passes less-or-equal there and takes it, where the sky drawn first was painted over by it. The one thing a
    /// host's lens sees cross the far plane is the city's street, which is painted back right after the sky
    /// (<see cref="CityStreets.DrawAtFarPlane"/>). Anything new that reaches past the far plane needs the same.
    /// </para>
    /// <para>
    /// <b>At display resolution on the tiers that supersample</b> (<see cref="DISPLAY_RESOLUTION"/>): the dome is shaded once a
    /// display pixel into a target of its own, before the scene target is bound, and that target is what the late draw brings
    /// in, one texel to each of the supersample's samples of its display pixel. The deck and the sun's disc get the one sample
    /// a display pixel Medium and Low give them; the footprint the deck band-limits against is a display pixel's, so its finest
    /// grain is retired nearer the lens ("The sky at display resolution" in docs/rendering/weather.md). The heaps are laid over
    /// it in the scene target as before, through the linear stretch of their own display-size layer.
    /// </para>
    /// </summary>
    public sealed class SkyPass : IDisposable
    {
        /// <summary>
        /// <b>THE SWITCH (#1104), the one line that turns the look back:</b> true shades the dome once a display pixel on the
        /// tiers that supersample (High and Ultra), false shades it at every sample of the scene target, as every frame did
        /// before #1104. Nothing of it below supersampling 2 or on a phone (<see cref="GraphicsSupport.SkyBehindScene"/>).
        /// The default of <see cref="AtDisplayResolution"/>, which the Testbed's <c>skyres=</c> overrides.
        /// </summary>
        public const bool DISPLAY_RESOLUTION = true;

        private readonly GraphicsDevice _device;
        private readonly SkyDome _dome;

        //The heaps this frame marched, or null where none are drawn (Prepare)
        private CloudVolumes _volumes;

        //The sky at display resolution: its own target, the scene target's size over the supersample factor exactly (so a
        //texel is one display pixel and the supersample's samples of it), let go in any frame that does not use it; and the
        //batch that brings it in
        private RenderTarget2D _target;
        private SpriteBatch _batch;
        private Rectangle _sceneRectangle;

        /// <summary>
        /// Whether a supersampled frame shades the sky at display resolution: <see cref="DISPLAY_RESOLUTION"/> unless a caller
        /// says otherwise (the Testbed's <c>skyres=</c>, an <c>alt=</c> dial). Read by every <see cref="Prepare"/>, so a change
        /// lands in the next frame.
        /// </summary>
        public bool AtDisplayResolution { get; set; } = DISPLAY_RESOLUTION;

        /// <summary>Whether the last <see cref="Prepare"/> shaded the sky into its display-resolution target.</summary>
        public bool DrawsAtDisplayResolution { get; private set; }

        /// <summary>
        /// Whether a host draws the sky behind the opaque scene into <paramref name="sceneTarget"/>, or first over the clear as
        /// before #1104: behind on the desktop (<see cref="GraphicsSupport.SkyBehindScene"/>) and <b>never into a multisampled
        /// target</b> (Medium and Low on Windows, the Testbed at <c>ssaa=1</c> unless <c>msaa=0</c>), whatever the scene.
        /// <para>
        /// ⚠ Under multisampling the two orders are not one picture: the late order moved 137 to 10,904 pixels of a 1600x900
        /// frame (at most 15 to 51 steps of 255) along the silhouettes the environment holds against the sky (the forest's
        /// trees, the mountains' ridges, the city's towers), where two frames of one order differ in 55 to 193. Without
        /// multisampling the two orders are the same picture to the pixel. The target is passed rather than
        /// remembered, for <c>SceneRenderer.EnvironmentFollowsIslandDepth</c>'s reason: a quality step replaces it.
        /// </para>
        /// </summary>
        public static bool BehindScene(RenderTarget2D sceneTarget) =>
            GraphicsSupport.SkyBehindScene && sceneTarget != null && sceneTarget.MultiSampleCount <= 1;

        /// <summary>
        /// Whether shading the dome at display resolution pays in <paramref name="scene"/>: everywhere but under the two cities'
        /// towers (<paramref name="sceneryShown"/>, the Game's Scenery row). The display-resolution target is shaded whole,
        /// before anything stands in front of it, where the late draw at every sample shades only the samples the sky shows
        /// through; the towers hide so much of the sky that the second is the cheaper. Measured at the review's pin (Testbed,
        /// 3840x1500, <c>ssaa=2</c>, the game-mode camera, one scene a process, the sky behind the scene at every sample against
        /// it at display resolution): the neon city 8.74 against 9.30 ms, the city 6.61 against 6.65, and the other twelve
        /// scenes under a dome 0.80 to 1.93 ms the cheaper at display resolution. In the Game at Ultra (a 3840x1500 window,
        /// one process a figure) the neon city's Trophy 6.96 against 7.55 ms, the city's Girandole 8.75 against 8.86.
        /// </summary>
        public static bool DisplayResolutionPays(SceneKind scene, bool sceneryShown) =>
            !(sceneryShown && (scene == SceneKind.City || scene == SceneKind.NeonCity));

        public SkyPass(GraphicsDevice device, SkyDome dome)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _dome = dome ?? throw new ArgumentNullException(nameof(dome));
        }

        /// <summary>
        /// The frame's sky work that needs a target of its own: the heaps marched into their layer
        /// (<see cref="CloudVolumes.March"/>), and on a supersampled frame the dome shaded at display resolution into the sky's
        /// own target (<see cref="AtDisplayResolution"/>). Call it <b>before the scene target is bound</b> (a
        /// target is bound once a frame), with this frame's sky already stated on <paramref name="skyEffect"/>
        /// (<see cref="CloudField.ApplyTo"/>, the camera's position, <see cref="CloudVolumes.ApplyToSky"/>), in a frame that
        /// draws the dome; a frame that does not calls <see cref="Release"/>. Leaves a target of its own bound.
        /// </summary>
        /// <param name="volumes">The heaps, or null where none are drawn this frame (below High, a sky that is a lid).</param>
        /// <param name="sceneTarget">The target the scene is drawn into this frame, whose size over
        /// <paramref name="supersampleFactor"/> the sky's own target takes: the back buffer's, or the player's lower Resolution
        /// row (#801). Passed rather than remembered, so a resize or a quality step cannot leave the sky at the old size.</param>
        /// <param name="supersampleFactor">The scene target's supersample factor: the sky goes into its own target above 1.</param>
        /// <param name="displayResolutionPays">The scene's answer, <see cref="DisplayResolutionPays"/>: false keeps the dome at
        /// every sample whatever <see cref="AtDisplayResolution"/> says.</param>
        public void Prepare(ICamera camera, CloudField clouds, Effect skyEffect, CloudVolumes volumes, RenderTarget2D sceneTarget,
            int supersampleFactor, bool displayResolutionPays)
        {
            _volumes = volumes;
            _volumes?.March(camera, clouds, skyEffect);

            int width = supersampleFactor > 1 ? sceneTarget.Width / supersampleFactor : 0;
            int height = supersampleFactor > 1 ? sceneTarget.Height / supersampleFactor : 0;

            //The pipeline's supersampled target is a whole multiple of the factor and never zero-sized (a minimized window
            //keeps the one it had: PostProcessPipeline.EnsureTarget), but a target narrower than the factor would size this
            //one to zero, a device error: the sky is then drawn into the scene target, which is correct and merely dearer
            DrawsAtDisplayResolution = AtDisplayResolution && displayResolutionPays && GraphicsSupport.SkyBehindScene
                && width > 0 && height > 0;
            if (!DrawsAtDisplayResolution)
            {
                Release();
                return;
            }

            if (_target == null || _target.Width != width || _target.Height != height)
            {
                _target?.Dispose();

                //Linear radiance like the scene target; no depth (the sky is a full-screen fill with no edges of its own, and
                //what stands in front of it is the scene's business) and no samples
                _target = new RenderTarget2D(_device, width, height, false, SurfaceFormat.HdrBlendable, DepthFormat.None, 0,
                    RenderTargetUsage.DiscardContents);
            }

            _sceneRectangle = new Rectangle(0, 0, sceneTarget.Width, sceneTarget.Height);

            //No clear: the dome is a whole sphere round the lens (SkyDome.BuildDrawnDome) and covers every pixel.
            //⚠ ONLY THE DOME. The heaps are not laid into this target: each of their pixels is a jittered march (CloudVolume.fx),
            //and read one texel a sample their rims showed the jitter's checker at full contrast, up to 160 steps of 255 in the
            //Game's frame, which the linear stretch of their layer over the supersampled target has always smoothed. They are
            //marched at the display's size already, so laying them here would save one texture read a sky sample.
            _device.SetRenderTarget(_target);
            _device.BlendState = BlendState.Opaque;
            _device.RasterizerState = RasterizerState.CullNone;
            _dome.Draw(camera, DepthStencilState.None);
        }

        /// <summary>
        /// Lets the display-resolution target go: called by every <see cref="Prepare"/> that does not use it, and by a host in a
        /// frame with no dome (a sky-replacing scene), so a level under its own sky does not keep it (about 46 MB at
        /// 3840x1500). Rebuilt by the next frame that wants it.
        /// </summary>
        public void Release()
        {
            DrawsAtDisplayResolution = false;
            _target?.Dispose();
            _target = null;
        }

        /// <summary>
        /// Draws the sky <see cref="Prepare"/> made ready into the scene target bound now. <paramref name="behindScene"/>: behind
        /// everything opaque drawn so far, at the far plane (the class's remarks), in the gap between the environment's two
        /// halves; otherwise over everything, depth off, as the frame's first draw after the clear (a phone, a multisampled
        /// target, the Testbed's <c>skyorder=first</c>: <see cref="BehindScene"/>). States everything it draws
        /// with, and leaves the scene's baseline behind: alpha blend, the default depth state, back faces culled, the viewport
        /// as it found it.
        /// </summary>
        public void Draw(ICamera camera, bool behindScene)
        {
            Viewport viewport = _device.Viewport;

            //The far plane: the depth range closed onto 1, so every depth the dome's mesh and the heaps' quad come out at is 1,
            //which passes the cleared depth (less-or-equal) and fails everything opaque in front of it
            if (behindScene)
            {
                Viewport far = viewport;
                far.MinDepth = 1f;
                far.MaxDepth = 1f;
                _device.Viewport = far;
            }

            DepthStencilState depth = behindScene ? DepthStencilState.DepthRead : DepthStencilState.None;

            if (DrawsAtDisplayResolution)
            {
                //The sky Prepare shaded, one texel to each sample of its display pixel. POINT, not linear: the factor between
                //the two targets is exactly the supersample factor, so a sample's texel is its own display pixel's, and the
                //resolve's box over a display pixel's samples returns that texel exactly: one sample a display pixel, which is
                //what Medium and Low show. Linear would blend each sample with the neighbouring texel a quarter of the way
                //over, and the box over them is a blur of the sky the resolve would add on top.
                _batch ??= new SpriteBatch(_device);
                _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, depth, RasterizerState.CullNone);
                _batch.Draw(_target, _sceneRectangle, Color.White);
                _batch.End();
            }
            else
            {
                //Opaque, and with no culling: the dome is wound for a lens inside it (SkyDome.BuildDrawnDome), and what ran last
                //is this frame's scene or the last frame's overlay, so neither is inherited
                _device.BlendState = BlendState.Opaque;
                _device.RasterizerState = RasterizerState.CullNone;
                _dome.Draw(camera, depth);
            }

            //The heaps over the deck, premultiplied, on the same depth, the linear stretch of their display-size layer whichever
            //way the dome was drawn
            if (_volumes != null)
            {
                _device.DepthStencilState = depth;
                _volumes.Composite();
            }

            _device.Viewport = viewport;
            _device.BlendState = BlendState.AlphaBlend;
            _device.DepthStencilState = DepthStencilState.Default;
            _device.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>The display-resolution target and the batch: never the dome or the heaps, which are their owners'.</summary>
        public void Dispose()
        {
            Release();
            _batch?.Dispose();
            _batch = null;
        }
    }
}
