using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>Motion blur</b> (#402): what moved during a shutter's worth of time is drawn smeared along its own path —
    /// the barrel swinging while it is aimed, the shot in flight, a released group falling, and the whole frame when
    /// the lens itself is kicked or turned. A per-pixel velocity target and McGuire's reconstruction filter over the
    /// HDR scene (<c>Shaders/MotionBlur.fx</c>); <see cref="PostProcessPipeline"/> runs the filter at the head of its
    /// resolve and reads the result wherever it would have read the scene target.
    /// <para>
    /// <b>How a frame uses it.</b> The caller draws its scene as always, then — with the scene complete and before
    /// the resolve — opens the velocity pass (<see cref="BeginVelocity"/>) and draws into it every object whose
    /// motion should smear, each with the pose it had when the shutter opened (<see cref="Draw(InstancedModelRenderer, in Matrix, in Matrix)"/>,
    /// <see cref="Draw(InstancedModelRenderer, MotionInstance[], int)"/>). Everything it does not draw is
    /// <i>background</i> and moves only with the camera. The resolve then consumes the pass; a frame that opened
    /// none resolves exactly as before and pays nothing.
    /// </para>
    /// <para>
    /// <b>The shutter is a time, not a frame</b> — see <see cref="SHUTTER_SECONDS"/> and <see cref="ShutterHistory"/>.
    /// That is the whole difference between this and the effect #402's second comment measured as invisible.
    /// </para>
    /// </summary>
    public sealed class MotionBlur : IDisposable
    {
        /// <summary>
        /// How long the shutter is open, in seconds: every smear is the distance its surface covered over this long,
        /// whatever the frame rate. A twentieth, which is <b>deliberately longer than any real frame this game
        /// runs at</b> — the owner's ruling on #402 was that the blur must be SEEN, and at the 144 Hz of his desktop
        /// a one-frame smear is under a tenth of the barrel's width even on a full-speed flick. A thirtieth was tried
        /// first and photographed: a brisk 118°/s swing left the muzzle soft rather than smeared at 1080 lines. At a
        /// twentieth the same swing drags the muzzle about 0.35 world units — some forty pixels from the play camera
        /// at 1080 lines, sixty at the owner's 1600 — and a released ball falling at 10 units a second leaves a streak
        /// half its own width long. It is the 180° shutter of a 10 fps film camera: stylised, and chosen to be.
        /// </summary>
        public const float SHUTTER_SECONDS = 1f / 20f;

        /// <summary>
        /// The longest smear either side of a surface, as a fraction of the frame's height — and, being the same
        /// figure, the tile the reconstruction's neighbourhood search is sized in. A fraction rather than pixels so
        /// the cap is the same look at 900 and at 2160 lines (hardware-universality: nothing here is tuned for one
        /// screen). Three percent either side is a streak six percent of the frame long, which is past anything but
        /// a violent flick or a teleport; the cap exists for the teleport.
        /// </summary>
        public const float MAX_REACH_FRACTION = 0.03f;

        private readonly GraphicsDevice _device;
        private readonly Effect _effect;
        private readonly VertexBuffer _fullScreenQuad;

        private readonly EffectTechnique _velocityTechnique;
        private readonly EffectTechnique _tileMaxXTechnique;
        private readonly EffectTechnique _tileMaxYTechnique;
        private readonly EffectTechnique _neighborMaxTechnique;
        private readonly EffectTechnique _reconstructTechnique;

        private readonly EffectParameter _viewProjectionParam;
        private readonly EffectParameter _shutterViewProjectionParam;
        private readonly EffectParameter _boneParam;
        private readonly EffectParameter _velocityScaleParam;
        private readonly EffectParameter _maxHalfVelocityParam;
        private readonly EffectParameter _velocityTextureParam;
        private readonly EffectParameter _tileSourceTextureParam;
        private readonly EffectParameter _tileSourceTexelSizeParam;
        private readonly EffectParameter _tileSizeParam;
        private readonly EffectParameter _neighborTextureParam;
        private readonly EffectParameter _sceneTextureParam;
        private readonly EffectParameter _outputSizeParam;
        private readonly EffectParameter _outputTexelSizeParam;
        private readonly EffectParameter _backgroundReprojectParam;
        private readonly EffectParameter _backgroundDepthNdcParam;

        //The velocity target (half-velocity in RG, view depth in B, coverage in A, with a depth buffer of its own so
        //the nearest moving surface wins), the two tile passes, the neighbourhood and the blurred frame. All at the
        //back buffer's own size or a tile's fraction of it, and all built on first use: only the Game asks.
        private RenderTarget2D _velocityTarget, _tilesAcross, _tiles, _neighbors, _output;
        private int _tileSize;

        private DynamicVertexBuffer _instanceBuffer;
        private readonly MotionInstance[] _single = new MotionInstance[1];

        /// <summary>True between <see cref="BeginVelocity"/> and the resolve that consumes it.</summary>
        public bool Pending { get; private set; }

        /// <param name="effect">The compiled <c>Shaders/MotionBlur</c>, from the executable's own content (the
        /// libraries have none).</param>
        /// <param name="fullScreenQuad">The pipeline's NDC quad, shared rather than built twice.</param>
        internal MotionBlur(GraphicsDevice device, Effect effect, VertexBuffer fullScreenQuad)
        {
            _device = device;
            _effect = effect;
            _fullScreenQuad = fullScreenQuad;

            _velocityTechnique = effect.Techniques["Velocity"];
            _tileMaxXTechnique = effect.Techniques["TileMaxX"];
            _tileMaxYTechnique = effect.Techniques["TileMaxY"];
            _neighborMaxTechnique = effect.Techniques["NeighborMax"];
            _reconstructTechnique = effect.Techniques["Reconstruct"];

            _viewProjectionParam = effect.Parameters["ViewProjection"];
            _shutterViewProjectionParam = effect.Parameters["ShutterViewProjection"];
            _boneParam = effect.Parameters["Bone"];
            _velocityScaleParam = effect.Parameters["VelocityScale"];
            _maxHalfVelocityParam = effect.Parameters["MaxHalfVelocity"];
            _velocityTextureParam = effect.Parameters["VelocityTexture"];
            _tileSourceTextureParam = effect.Parameters["TileSourceTexture"];
            _tileSourceTexelSizeParam = effect.Parameters["TileSourceTexelSize"];
            _tileSizeParam = effect.Parameters["TileSize"];
            _neighborTextureParam = effect.Parameters["NeighborTexture"];
            _sceneTextureParam = effect.Parameters["SceneTexture"];
            _outputSizeParam = effect.Parameters["OutputSize"];
            _outputTexelSizeParam = effect.Parameters["OutputTexelSize"];
            _backgroundReprojectParam = effect.Parameters["BackgroundReproject"];
            _backgroundDepthNdcParam = effect.Parameters["BackgroundDepthNdc"];
        }

        /// <summary>
        /// Opens this frame's velocity pass: binds and clears the velocity target and states the camera now and at
        /// the shutter's opening. <b>Call it once the scene is complete and before the resolve</b> — it binds a
        /// target of its own, and leaving the scene target mid-frame costs a multisample resolve for nothing.
        /// </summary>
        /// <param name="view">This frame's view.</param>
        /// <param name="projection">This frame's projection.</param>
        /// <param name="shutterViewProjection">The camera's view × projection when the shutter opened
        /// (a <see cref="ShutterHistory"/> of it, read <see cref="SHUTTER_SECONDS"/> back).</param>
        /// <param name="backgroundDepth">The view depth, in world units, at which everything NOT drawn into the pass
        /// is taken to stand when the camera's own motion is reprojected — see <c>BackgroundReproject</c> in the
        /// shader. The Game passes the cluster's.</param>
        /// <returns>False while the window is minimized and there is nothing to draw into.</returns>
        public bool BeginVelocity(in Matrix view, in Matrix projection, in Matrix shutterViewProjection, float backgroundDepth)
        {
            if (!EnsureTargets()) return false;

            Matrix viewProjection = view * projection;

            _device.SetRenderTarget(_velocityTarget);
            _device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Vector4.Zero, 1f, 0);

            _device.BlendState = BlendState.Opaque;
            _device.DepthStencilState = DepthStencilState.Default;
            //Both windings: the procedural meshes and the loaded ones are wound opposite ways (CLAUDE.md), and the
            //depth test keeps the nearest face whichever side a triangle shows
            _device.RasterizerState = RasterizerState.CullNone;

            _viewProjectionParam.SetValue(viewProjection);
            _shutterViewProjectionParam.SetValue(shutterViewProjection);

            //The background's reprojection: this frame's NDC at the chosen depth back to world space, then through
            //the shutter's camera. The depth's NDC figure comes out of the projection's own two terms, so it is true
            //for whatever lens (precise aim's narrower one, the recoil's FOV punch) is in use.
            float depth = Math.Max(backgroundDepth, 0.1f);
            float ndcZ = (-depth * projection.M33 + projection.M43) / depth;
            _backgroundDepthNdcParam.SetValue(ndcZ);
            _backgroundReprojectParam.SetValue(Matrix.Invert(viewProjection) * shutterViewProjection);

            Pending = true;
            return true;
        }

        /// <summary>One object into the open velocity pass, at its pose now and when the shutter opened.</summary>
        public void Draw(InstancedModelRenderer renderer, in Matrix world, in Matrix shutterWorld)
        {
            _single[0] = new MotionInstance(world, shutterWorld);
            Draw(renderer, _single, 1);
        }

        /// <summary>Many instances of one model into the open velocity pass — the balls.</summary>
        public void Draw(InstancedModelRenderer renderer, MotionInstance[] instances, int count)
        {
            if (!Pending || count <= 0 || renderer == null) return;

            if (_instanceBuffer == null || _instanceBuffer.VertexCount < count)
            {
                _instanceBuffer?.Dispose();
                _instanceBuffer = new DynamicVertexBuffer(_device, MotionInstance.VertexDeclaration,
                    Math.Max(count, 64), BufferUsage.WriteOnly);
            }

            _instanceBuffer.SetData(instances, 0, count, SetDataOptions.Discard);

            _effect.CurrentTechnique = _velocityTechnique;
            renderer.DrawMotion(_effect, _boneParam, _instanceBuffer, count);
        }

        /// <summary>
        /// Runs the tile passes and the reconstruction over <paramref name="scene"/> and returns the blurred frame,
        /// the back buffer's size in linear radiance — or null when no pass was opened this frame. Called by the
        /// pipeline's resolve and nothing else; it binds its own targets and leaves the last one bound.
        /// </summary>
        internal Texture2D Reconstruct(Texture2D scene)
        {
            if (!Pending) return null;
            Pending = false;

            _device.BlendState = BlendState.Opaque;
            _device.DepthStencilState = DepthStencilState.None;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SetVertexBuffer(_fullScreenQuad);

            //The longest smear there can be, across the row and down it: two separable passes rather than one
            //loop over a whole tile, which would be a handful of threads each doing thousands of serial reads
            _tileSizeParam.SetValue(_tileSize);

            _device.SetRenderTarget(_tilesAcross);
            _tileSourceTextureParam.SetValue(_velocityTarget);
            _tileSourceTexelSizeParam.SetValue(new Vector2(1f / _velocityTarget.Width, 1f / _velocityTarget.Height));
            DrawQuad(_tileMaxXTechnique);

            _device.SetRenderTarget(_tiles);
            _tileSourceTextureParam.SetValue(_tilesAcross);
            _tileSourceTexelSizeParam.SetValue(new Vector2(1f / _tilesAcross.Width, 1f / _tilesAcross.Height));
            DrawQuad(_tileMaxYTechnique);

            _device.SetRenderTarget(_neighbors);
            _tileSourceTextureParam.SetValue(_tiles);
            _tileSourceTexelSizeParam.SetValue(new Vector2(1f / _tiles.Width, 1f / _tiles.Height));
            DrawQuad(_neighborMaxTechnique);

            _device.SetRenderTarget(_output);
            _velocityTextureParam.SetValue(_velocityTarget);
            _neighborTextureParam.SetValue(_neighbors);
            _sceneTextureParam.SetValue(scene);
            DrawQuad(_reconstructTechnique);

            return _output;
        }

        private void DrawQuad(EffectTechnique technique)
        {
            _effect.CurrentTechnique = technique;
            foreach (EffectPass pass in technique.Passes)
            {
                pass.Apply();
                _device.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
            }
        }

        /// <summary>
        /// Builds the targets at the back buffer's size the first time they are asked for and after every resize.
        /// The tile is the longest smear, <see cref="MAX_REACH_FRACTION"/> of the height, so it follows the window.
        /// </summary>
        private bool EnsureTargets()
        {
            int width = _device.PresentationParameters.BackBufferWidth;
            int height = _device.PresentationParameters.BackBufferHeight;
            if (width <= 0 || height <= 0) return false;

            if (_velocityTarget != null && _velocityTarget.Width == width && _velocityTarget.Height == height) return true;

            DisposeTargets();

            _tileSize = Math.Max(1, (int)MathF.Ceiling(height * MAX_REACH_FRACTION));
            int tilesX = (width + _tileSize - 1) / _tileSize;
            int tilesY = (height + _tileSize - 1) / _tileSize;

            _velocityTarget = new RenderTarget2D(_device, width, height, false, SurfaceFormat.HalfVector4,
                DepthFormat.Depth24, 0, RenderTargetUsage.DiscardContents);
            _tilesAcross = new RenderTarget2D(_device, tilesX, height, false, SurfaceFormat.HalfVector2, DepthFormat.None);
            _tiles = new RenderTarget2D(_device, tilesX, tilesY, false, SurfaceFormat.HalfVector2, DepthFormat.None);
            _neighbors = new RenderTarget2D(_device, tilesX, tilesY, false, SurfaceFormat.HalfVector2, DepthFormat.None);
            _output = new RenderTarget2D(_device, width, height, false, SurfaceFormat.HdrBlendable, DepthFormat.None);

            //What only a resize changes, written here rather than per frame (BestPractices.md)
            _velocityScaleParam.SetValue(new Vector2(width * 0.25f, -height * 0.25f));
            _maxHalfVelocityParam.SetValue((float)_tileSize);
            _outputSizeParam.SetValue(new Vector2(width, height));
            _outputTexelSizeParam.SetValue(new Vector2(1f / width, 1f / height));

            return true;
        }

        private void DisposeTargets()
        {
            _velocityTarget?.Dispose();
            _tilesAcross?.Dispose();
            _tiles?.Dispose();
            _neighbors?.Dispose();
            _output?.Dispose();
            _velocityTarget = _tilesAcross = _tiles = _neighbors = _output = null;
        }

        public void Dispose()
        {
            DisposeTargets();
            _instanceBuffer?.Dispose();
            _instanceBuffer = null;
        }
    }
}
