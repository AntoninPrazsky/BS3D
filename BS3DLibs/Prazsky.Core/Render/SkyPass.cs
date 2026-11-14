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
    /// </summary>
    public sealed class SkyPass
    {
        private readonly GraphicsDevice _device;
        private readonly SkyDome _dome;

        //The heaps this frame marched, or null where none are drawn (Prepare)
        private CloudVolumes _volumes;

        public SkyPass(GraphicsDevice device, SkyDome dome)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _dome = dome ?? throw new ArgumentNullException(nameof(dome));
        }

        /// <summary>
        /// The frame's sky work that needs a target of its own: the heaps marched into their layer
        /// (<see cref="CloudVolumes.March"/>). Call it <b>before the scene target is bound</b> (a target is bound once a frame),
        /// with this frame's sky already stated on <paramref name="skyEffect"/> (<see cref="CloudField.ApplyTo"/>, the camera's
        /// position, <see cref="CloudVolumes.ApplyToSky"/>), and only in a frame that draws the dome.
        /// </summary>
        /// <param name="volumes">The heaps, or null where none are drawn this frame (below High, a sky that is a lid).</param>
        public void Prepare(ICamera camera, CloudField clouds, Effect skyEffect, CloudVolumes volumes)
        {
            _volumes = volumes;
            _volumes?.March(camera, clouds, skyEffect);
        }

        /// <summary>
        /// Draws the sky <see cref="Prepare"/> made ready into the scene target bound now. <paramref name="behindScene"/>: behind
        /// everything opaque drawn so far, at the far plane (the class's remarks), in the gap between the environment's two
        /// halves; otherwise over everything, depth off, as the frame's first draw after the clear (a phone,
        /// <see cref="GraphicsSupport.SkyBehindScene"/>, and the Testbed's <c>skyorder=first</c>). States everything it draws
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

            //Opaque, and with no culling: the dome is wound for a lens inside it (SkyDome.BuildDrawnDome), and what ran last is
            //this frame's scene or the last frame's overlay, so neither is inherited
            _device.BlendState = BlendState.Opaque;
            _device.RasterizerState = RasterizerState.CullNone;
            _dome.Draw(camera, depth);

            //The heaps over the deck, premultiplied, on the same depth
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
    }
}
