using BS3D.Platform;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;

namespace BS3D
{
    /// <summary>
    /// The Potato render path (#789): how GamePi, the Raspberry Pi build, draws a frame. Everything of it that is the
    /// host's is in this file; the rest of the host branches into it at the few places a frame is put together.
    /// <para>
    /// <b>What it is.</b> A forward path straight into the 8-bit back buffer. None of the desktop's 43 effects exists in
    /// GamePi (Shader Model 5.0, which DesktopGL cannot compile), so it draws with its own small set
    /// (<c>GamePi/Shaders</c>): <c>PotatoModel</c> under every <see cref="InstancedModelRenderer"/> - the island, the
    /// gun, the balls, the glass, the wordmark - which recognises it and draws through its Potato path, and SM 3.0 ports
    /// of the six small effects (the trails, the laser net, the blasts, the wormhole, the fireworks, the confetti)
    /// under their own names. Each ends in the exposure, the ACES curve and the sRGB encoding the desktop's resolve does
    /// once (<c>PotatoOutput.fxh</c>), because there is no HDR target to resolve.
    /// </para>
    /// <para>
    /// <b>What it does not build.</b> No <see cref="SceneRenderer"/> - the twenty-one backdrops are the desktop's most
    /// expensive pixels and each is its own SM 5.0 effect - so the island stands under the dome's gradient alone, drawn
    /// through <c>PotatoSky</c>, the player's exposure and all (no <c>Sky.fx</c>, so no cloud deck and no sun disc). No
    /// <see cref="PostProcessPipeline"/>: no bloom, no defocus, no motion blur, no grain, and the HUD's layer is drawn
    /// straight. No street level, and no forest or aurora wood (they are planted from the scene renderer's configs, and
    /// only those scenes' backdrops would show them). The city's towers and roofs are still generated, being plain
    /// instanced boxes the host's scene switch holds on to, but nothing draws them.
    /// </para>
    /// <para>
    /// <b>When it runs.</b> In the build that is locked to <see cref="QualityLevel.Potato"/> (<see cref="QualityLock"/>),
    /// which is GamePi. The Windows build has none of these effects, so <c>quality=potato</c> there is the tier's preset
    /// row on the desktop path and nothing more.
    /// </para>
    /// </summary>
    public partial class BS3DGame
    {
        /// <summary>Whether this build draws through the Potato path. See the class doc.</summary>
        internal static bool PotatoPath => QualityLock.Tier == QualityLevel.Potato;

        /// <summary>
        /// Every Potato effect that ends a pixel with <c>PotatoOutput.fxh</c>'s curve, and so carries an
        /// <c>Exposure</c> of its own where the desktop has the one in the resolve. The content manager hands every
        /// loader the same instance, so setting it once here reaches every owner.
        /// </summary>
        private static readonly string[] POTATO_EXPOSED_EFFECTS =
        {
            "Shaders/PotatoModel", "Shaders/PotatoSky", "Shaders/ShotTrail", "Shaders/LaserGrid", "Shaders/Blast",
            "Shaders/Wormhole", "Shaders/Fireworks", "Shaders/Confetti",
        };

        /// <summary>
        /// <see cref="Prazsky.BS3D.BallRenderSet.LodBias"/> on the Potato path: twice the desktop's silhouette budget. See
        /// there for the measurement.
        /// </summary>
        private const float POTATO_BALL_LOD_BIAS = 2f;

        /// <summary>
        /// The width of the balls' rims on the Potato path, in pixels of the target (#804), when the command line names
        /// none: <b>0, off, until its cost has been measured on the Pi</b> - the picture was judged on Windows through
        /// DesktopGL, the price on V3D is the Pi's to say. <c>rims=1</c> is the exact coverage of a pixel by an edge.
        /// </summary>
        private const float POTATO_RIM_PIXELS = 0f;

        /// <summary>
        /// Whether the frame being drawn presents the cup or the confetti, for <see cref="CompositeForegroundLast"/>.
        /// </summary>
        private bool _potatoPresenting;

        /// <summary>
        /// The 3D below native (#801): an 8-bit target of the render size the scene is drawn into instead of the back
        /// buffer, then scaled up onto it, and the menus and the HUD drawn over that at the display's own size. Null at
        /// native, where the scene goes straight into the back buffer as it always did. Rebuilt when the size changes.
        /// </summary>
        private RenderTarget2D _potatoSceneTarget;

        //The pass that scales it up (#801, PotatoUpscale.fx's Bilinear: its Fxaa measured 3.3-3.8 ms more on the Pi at a
        //1080p output, see docs/rendering.md) and the quad it is drawn on: the whole target in clip space, the texture's
        //top-left at the top-left. Loaded with the rest of the Potato effects.
        private Effect _potatoUpscale;
        private EffectParameter _potatoUpscaleSource;
        private static readonly VertexPositionTexture[] POTATO_UPSCALE_QUAD =
        {
            new(new Vector3(-1f, 1f, 0f), new Vector2(0f, 0f)),
            new(new Vector3(1f, 1f, 0f), new Vector2(1f, 0f)),
            new(new Vector3(-1f, -1f, 0f), new Vector2(0f, 1f)),
            new(new Vector3(1f, -1f, 0f), new Vector2(1f, 1f)),
        };

        /// <summary>
        /// The player's brightness (#711) onto every Potato effect - the Potato path's half of what
        /// <see cref="PostProcessPipeline.Exposure"/> is on the desktop's. Run at load and on every change of it.
        /// </summary>
        private void ApplyPotatoExposure()
        {
            foreach (string name in POTATO_EXPOSED_EFFECTS)
                Content.Load<Effect>(name).Parameters["Exposure"]?.SetValue(_exposure);
        }

        /// <summary>
        /// <see cref="BeginSceneDraw"/> on the Potato path: the back buffer cleared to the dome's horizon, the dome's
        /// gradient, then the island and its pit - and the frame the rest of the draw needs. No shadow maps, no clouds,
        /// no backdrop, no sea or kill-plane fade; of the scene's own lamps only those no backdrop has to be built for
        /// (#795, <see cref="SceneLights.Apply"/> with no renderer). The cup and the confetti are drawn last of all
        /// instead of first into a layer (<see cref="CompositeForegroundLast"/>).
        /// </summary>
        private SceneFrame BeginPotatoSceneDraw()
        {
            _ceilingGlassBehind = null;

            GraphicsDevice.SetRenderTarget(EnsurePotatoSceneTarget());

            //The dome below covers the whole frame (a full sphere, drawn with no depth test and no culling), so the clear is
            //never seen; it is the horizon, display-encoded, only so that a frame without the dome would still read as sky
            GraphicsDevice.Clear(new Color(ColorSpace.LinearToSrgb(_rig.HorizonLinear)));

            //Stated rather than inherited, for BeginSceneDraw's reason: SkyDome.Draw sets neither
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            _sky.Draw(_camera);

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            SceneFrame sceneFrame = BuildSceneFrame();

            //The volcano's flows and the neon ring (#795): the two scenes whose dome is dark and whose own lamps carry the
            //cluster on the desktop, and the two whose lamps need no backdrop. A blast's flash rides the same push.
            _sceneLights.Apply(_scene, null, _cityConfig.NeonLook, _wallClock);

            _island.EventGlow = 0f;
            _island.DrawIsland(_camera, _sceneEffectParams, _scene);
            _island.DrawPit(_camera, _sceneEffectParams, _scene);

            //The translucent baseline the caller's glass draws inherit (#667), as BeginSceneDraw promises it
            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            return sceneFrame;
        }

        /// <summary>
        /// <see cref="FinishSceneDraw"/> on the Potato path: the on-top draw over a cleared depth buffer, and nothing to
        /// resolve. Notes whether the cup or the confetti is up, which <see cref="CompositeForegroundLast"/> then draws.
        /// </summary>
        private void FinishPotatoSceneDraw(Action drawOnTop)
        {
            if (_potatoSceneTarget != null) PresentPotatoSceneTarget();

            if (drawOnTop != null)
            {
                GraphicsDevice.Clear(ClearOptions.DepthBuffer, Vector4.Zero, 1f, 0);
                drawOnTop();
            }

            _potatoPresenting = (_trophy != null && _trophy.Active) || (_confetti != null && _confetti.Active);
        }

        /// <summary>The scale-up pass (#801), loaded once with the Potato path's other effects.</summary>
        private void LoadPotatoUpscale()
        {
            _potatoUpscale = Content.Load<Effect>("Shaders/PotatoUpscale");
            _potatoUpscale.CurrentTechnique = _potatoUpscale.Techniques["Bilinear"];
            _potatoUpscaleSource = _potatoUpscale.Parameters["Source"];
        }

        /// <summary>
        /// The target the 3D is drawn into this frame (#801): null, the back buffer, at native; otherwise one of the render
        /// size, colour and depth, made on first use and again whenever the size changes. No MSAA and no stencil: the
        /// Potato path has neither.
        /// </summary>
        private RenderTarget2D EnsurePotatoSceneTarget()
        {
            PresentationParameters buffer = GraphicsDevice.PresentationParameters;
            bool native = _renderSize.X >= buffer.BackBufferWidth && _renderSize.Y >= buffer.BackBufferHeight;

            if (native || (_potatoSceneTarget != null
                && (_potatoSceneTarget.Width != _renderSize.X || _potatoSceneTarget.Height != _renderSize.Y)))
            {
                _potatoSceneTarget?.Dispose();
                _potatoSceneTarget = null;
            }

            if (!native)
            {
                _potatoSceneTarget ??= new RenderTarget2D(GraphicsDevice, _renderSize.X, _renderSize.Y, false,
                    SurfaceFormat.Color, DepthFormat.Depth24, 0, RenderTargetUsage.DiscardContents);
            }

            return _potatoSceneTarget;
        }

        /// <summary>
        /// The 3D drawn below native scaled up onto the back buffer (#801), bilinear, covering every pixel of it - so
        /// nothing the back buffer held matters - and the states <see cref="BeginPotatoSceneDraw"/> promised put back for
        /// what is drawn after, as <c>SceneRenderer</c>'s own backdrop blit does.
        /// </summary>
        private void PresentPotatoSceneTarget()
        {
            //The scene's depth is spent: said before leaving, so the tiler does not write it out (TargetDiscard has the
            //measurement). Nothing on the back buffer is cleared first - the quad below covers every pixel of it.
            TargetDiscard.DiscardDepth();
            GraphicsDevice.SetRenderTarget(null);

            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;

            _potatoUpscaleSource.SetValue(_potatoSceneTarget);
            _potatoUpscale.CurrentTechnique.Passes[0].Apply();
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, POTATO_UPSCALE_QUAD, 0, 2);

            //Unbound, so the next frame can render into the target again without GL sampling what it writes
            GraphicsDevice.Textures[0] = null;

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// <see cref="CompositeForegroundLast"/> on the Potato path: the confetti and the cup drawn straight over the
        /// finished frame, the UI included (#242's ruling), on a cleared depth buffer - the desktop composites the
        /// same two from a layer of their own, which Potato does not have. No crystal refraction.
        /// </summary>
        private void DrawPotatoForeground()
        {
            if (!_potatoPresenting) return;
            _potatoPresenting = false;

            BlendState blend = GraphicsDevice.BlendState;
            DepthStencilState depth = GraphicsDevice.DepthStencilState;
            RasterizerState raster = GraphicsDevice.RasterizerState;

            GraphicsDevice.Clear(ClearOptions.DepthBuffer, Vector4.Zero, 1f, 0);

            if (_confetti != null && _confetti.Active) _confetti.Draw(_camera);
            if (_trophy != null && _trophy.Active) _trophy.Draw(_camera);

            GraphicsDevice.BlendState = blend;
            GraphicsDevice.DepthStencilState = depth;
            GraphicsDevice.RasterizerState = raster;
        }
    }
}
