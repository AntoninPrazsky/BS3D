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
    /// through <see cref="Prazsky.Core.SkyDome"/>'s own <c>BasicEffect</c> (no <c>Sky.fx</c>, so no cloud deck and no sun disc). No
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
            "Shaders/PotatoModel", "Shaders/ShotTrail", "Shaders/LaserGrid", "Shaders/Blast", "Shaders/Wormhole",
            "Shaders/Fireworks", "Shaders/Confetti",
        };

        /// <summary>Whether the frame being drawn presents the cup or the confetti, for <see cref="CompositeForegroundLast"/>.</summary>
        private bool _potatoPresenting;

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
        /// no backdrop, no scene lights, no sea or kill-plane fade. The cup and the confetti are drawn last of all
        /// instead of first into a layer (<see cref="CompositeForegroundLast"/>).
        /// </summary>
        private SceneFrame BeginPotatoSceneDraw()
        {
            _ceilingGlassBehind = null;

            GraphicsDevice.SetRenderTarget(null);

            //The horizon as the dome's own gradient has it, display-encoded, which is what the back buffer holds: the
            //dome below is drawn from its stored sRGB palette (built with linearVertexColors: false) and the clear has
            //to meet it where the dome ends
            GraphicsDevice.Clear(new Color(ColorSpace.LinearToSrgb(_rig.HorizonLinear)));

            //Stated rather than inherited, for BeginSceneDraw's reason: SkyDome.Draw sets neither
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            _sky.Draw(_camera);

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            SceneFrame sceneFrame = BuildSceneFrame();

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
            if (drawOnTop != null)
            {
                GraphicsDevice.Clear(ClearOptions.DepthBuffer, Vector4.Zero, 1f, 0);
                drawOnTop();
            }

            _potatoPresenting = (_trophy != null && _trophy.Active) || (_confetti != null && _confetti.Active);
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
