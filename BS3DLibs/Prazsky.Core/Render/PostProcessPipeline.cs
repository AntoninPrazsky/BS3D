using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The frame's one exit from linear light, shared by all three executables (it existed line-for-line in
    /// each until #74): the supersampled HDR scene target the caller draws into, the five-level bloom
    /// pyramid (#69), and the tonemap resolve — box filter, glare add, exposure, the ACES curve, film grain,
    /// then the sRGB encode. The scene renders into <see cref="SceneTarget"/> in linear radiance and leaves
    /// it exactly once, in <see cref="Resolve"/>; everything drawn after that (overlays, HUD, gizmos) is in
    /// display space.
    /// <para>
    /// The look values are <c>required</c> on purpose: every one of them is a per-executable decision (the
    /// map editor runs a different glare threshold than the game shipped, the game alone toggles the lens
    /// flaws from Settings), and a pipeline silently running an unset uniform is exactly the black-screen
    /// class of bug the requirement makes uncompilable. Each setter writes its uniform immediately and the
    /// value persists on the effect — never re-sent per frame, per the caching discipline in
    /// BestPractices.md. Only the textures and texel sizes go out per <see cref="Resolve"/>, because the
    /// targets are recreated on every resize.
    /// </para>
    /// <para>
    /// The pipeline owns no content: the caller loads <c>Shaders/Tonemap</c> and <c>Shaders/Glare</c> from
    /// its own <c>ContentManager</c> (the libraries have no content pipeline — see CLAUDE.md) and hands the
    /// compiled effects in.
    /// </para>
    /// </summary>
    public sealed class PostProcessPipeline : IDisposable
    {
        //Half, quarter, eighth, sixteenth and a thirty-second of the back buffer: five levels reach a halo
        //about a quarter of the screen wide around a strong source, which is where a glow stops reading as
        //belonging to the thing that emits it.
        private const int BLOOM_LEVELS = 5;

        /// <summary>
        /// Only used when supersampling is off: multisampling antialiases geometry edges but not shading,
        /// and the balls' procedural relief is shading. Public because a host's device setup makes the same
        /// either/or call for its back buffer.
        /// </summary>
        public const int MSAA_SAMPLES = 8;

        //Being submerged has to read as being submerged, or the frame is the world unchanged with a water
        //plane cutting through it. Applied in linear light before the ACES curve, so the drowned scene rolls
        //through the same highlight response as everything above the surface: the frame is absorbed towards
        //a blue-green (red goes first, so it blues and dims) and the water's own in-scattered glow added,
        //both by the caller's amount. The colours are the pipeline's (they were identical in every copy);
        //how submerged the lens is stays scene knowledge the caller passes to Resolve.
        private static readonly Vector3 UNDERWATER_ABSORB = new(0.10f, 0.42f, 0.52f);
        private static readonly Vector3 UNDERWATER_INSCATTER = new(0.015f, 0.06f, 0.09f);

        private readonly GraphicsDevice _device;
        private readonly Effect _tonemapEffect;
        private readonly Effect _glareEffect;

        /// <summary>
        /// The scene renders into this instead of the back buffer, and always does: it is where linear
        /// radiance lives. A half-float format because linear light is open-ended — a lit highlight is
        /// genuinely several times brighter than white, and an 8-bit target would clip it flat before the
        /// tonemap curve ever got a chance to roll it off.
        /// </summary>
        private RenderTarget2D _sceneTarget;

        //The bloom pyramid (#69): the bright pass lands in the half-resolution head, is downsampled to the
        //foot and accumulated back up with additive tent upsamples — the wide soft halo that replaced the
        //six-armed streak star.
        private RenderTarget2D[] _bloomChain;

        private VertexBuffer _fullScreenQuad;

        //Cached in the constructor: the resolve runs every frame and the by-name indexer is a linear scan
        //over the effect's parameter list. Values that never change after startup are set once through the
        //properties below and never touched again; the textures and texel sizes still go out per frame
        //through these references, because the render targets are recreated on every resize.
        private readonly EffectTechnique _glareBrightPassTechnique;
        private readonly EffectTechnique _bloomDownTechnique;
        private readonly EffectTechnique _bloomUpTechnique;
        private readonly EffectParameter _glareSourceTextureParam;
        private readonly EffectParameter _glareSourceTexelSizeParam;
        private readonly EffectParameter _glareThresholdParam;
        private readonly EffectParameter _tonemapGlareTextureParam;
        private readonly EffectParameter _tonemapGlareIntensityParam;
        private readonly EffectParameter _tonemapSceneTextureParam;
        private readonly EffectParameter _tonemapSourceTexelSizeParam;
        private readonly EffectParameter _tonemapSupersampleFactorParam;
        private readonly EffectParameter _tonemapExposureParam;
        private readonly EffectParameter _tonemapAberrationParam;
        private readonly EffectParameter _tonemapGrainStrengthParam;
        private readonly EffectParameter _tonemapGrainSeedParam;
        private readonly EffectParameter _tonemapOutputSizeParam;
        private readonly EffectParameter _tonemapUnderwaterAmountParam;

        private float _glareThreshold;
        private float _glareIntensity;
        private float _exposure;
        private float _chromaticAberration;
        private float _filmGrain;
        private int _supersampleFactor = 1;

        public PostProcessPipeline(GraphicsDevice device, Effect tonemapEffect, Effect glareEffect)
        {
            _device = device;
            _tonemapEffect = tonemapEffect;
            _glareEffect = glareEffect;

            _glareBrightPassTechnique = glareEffect.Techniques["BrightPass"];
            _bloomDownTechnique = glareEffect.Techniques["BloomDown"];
            _bloomUpTechnique = glareEffect.Techniques["BloomUp"];
            _glareSourceTextureParam = glareEffect.Parameters["SourceTexture"];
            _glareSourceTexelSizeParam = glareEffect.Parameters["SourceTexelSize"];
            _glareThresholdParam = glareEffect.Parameters["GlareThreshold"];
            _tonemapGlareTextureParam = tonemapEffect.Parameters["GlareTexture"];
            _tonemapGlareIntensityParam = tonemapEffect.Parameters["GlareIntensity"];
            _tonemapSceneTextureParam = tonemapEffect.Parameters["SceneTexture"];
            _tonemapSourceTexelSizeParam = tonemapEffect.Parameters["SourceTexelSize"];
            _tonemapSupersampleFactorParam = tonemapEffect.Parameters["SupersampleFactor"];
            _tonemapExposureParam = tonemapEffect.Parameters["Exposure"];
            _tonemapAberrationParam = tonemapEffect.Parameters["ChromaticAberration"];
            _tonemapGrainStrengthParam = tonemapEffect.Parameters["GrainStrength"];
            _tonemapGrainSeedParam = tonemapEffect.Parameters["GrainSeed"];
            _tonemapOutputSizeParam = tonemapEffect.Parameters["OutputSize"];
            _tonemapUnderwaterAmountParam = tonemapEffect.Parameters["UnderwaterAmount"];

            //Constant for the run in every executable; the per-frame amount starts at "not submerged"
            tonemapEffect.Parameters["UnderwaterAbsorb"].SetValue(UNDERWATER_ABSORB);
            tonemapEffect.Parameters["UnderwaterInscatter"].SetValue(UNDERWATER_INSCATTER);
            _tonemapUnderwaterAmountParam.SetValue(0f);

            CreateFullScreenQuad();
        }

        /// <summary>
        /// Radiance a pixel has to exceed before it starts to glare. A per-executable taste decision — see
        /// the constant each one passes, and docs/scenes.md for the per-element discipline against it.
        /// </summary>
        public required float GlareThreshold
        {
            get => _glareThreshold;
            set { _glareThreshold = value; _glareThresholdParam.SetValue(value); }
        }

        /// <summary>
        /// How much of the bloom is added back. The pyramid ACCUMULATES on the way up — the half-resolution
        /// head ends carrying its own tight halo plus every wider level's — so the same subjective glow sits
        /// at a far lower intensity than a single-pass glare needs.
        /// </summary>
        public required float GlareIntensity
        {
            get => _glareIntensity;
            set { _glareIntensity = value; _tonemapGlareIntensityParam.SetValue(value); }
        }

        /// <summary>Linear scale applied before the tonemap curve — the renderer's shutter speed.</summary>
        public required float Exposure
        {
            get => _exposure;
            set { _exposure = value; _tonemapExposureParam.SetValue(value); }
        }

        /// <summary>
        /// Peak red/blue displacement at the frame corners, as a fraction of the frame; the shader grows it
        /// quadratically from zero at the centre. Zero skips the shader's whole branch, so off costs nothing.
        /// </summary>
        public required float ChromaticAberration
        {
            get => _chromaticAberration;
            set { _chromaticAberration = value; _tonemapAberrationParam.SetValue(value); }
        }

        /// <summary>
        /// Peak film-grain modulation at 50% grey, applied after the tonemap curve and before the sRGB
        /// encode — texture on the print, never sensor noise. Zero skips the shader's branch.
        /// </summary>
        public required float FilmGrain
        {
            get => _filmGrain;
            set { _filmGrain = value; _tonemapGrainStrengthParam.SetValue(value); }
        }

        /// <summary>
        /// The scene renders into a target this many times larger per axis and is box-filtered down on the
        /// way to the back buffer; the tonemap has to be told how many samples its filter averages, and the
        /// scene target's size is derived from it — so the setter is what makes <see cref="EnsureTarget"/>
        /// recreate the target rather than recognize it as the one already there.
        /// </summary>
        public required int SupersampleFactor
        {
            get => _supersampleFactor;
            set
            {
                _supersampleFactor = Math.Clamp(value, 1, 4);
                _tonemapSupersampleFactorParam.SetValue(_supersampleFactor);
                EnsureTarget();
            }
        }

        /// <summary>The linear-radiance HDR target the caller binds and draws the whole scene into.</summary>
        public RenderTarget2D SceneTarget => _sceneTarget;

        /// <summary>
        /// (Re)creates the scene target and the bloom chain when their dimensions no longer match the back
        /// buffer × <see cref="SupersampleFactor"/>. Call from every path that resizes the back buffer —
        /// load, resize, fullscreen switch — the early-out makes redundant calls free.
        /// </summary>
        public void EnsureTarget()
        {
            int width = _device.PresentationParameters.BackBufferWidth * _supersampleFactor;
            int height = _device.PresentationParameters.BackBufferHeight * _supersampleFactor;

            //A minimized window can report a zero back buffer; a zero-sized target is a device error, and
            //the restore path calls back here anyway (the map editor's copy learned this first)
            if (width <= 0 || height <= 0) return;

            if (_sceneTarget != null && _sceneTarget.Width == width && _sceneTarget.Height == height) return;

            _sceneTarget?.Dispose();

            //Supersampling already averages its samples per output pixel, geometry edges included, so MSAA
            //only earns its memory with supersampling off — which is exactly the ssaa=1 path this used to
            //leave with no antialiasing of any kind, on the setting a weak machine reaches for first. It
            //goes on the scene target, never the back buffer: nothing but one resolved quad ever reaches
            //that.
            _sceneTarget = new RenderTarget2D(_device, width, height, false, SurfaceFormat.HdrBlendable,
                DepthFormat.Depth24Stencil8, _supersampleFactor > 1 ? 0 : MSAA_SAMPLES, RenderTargetUsage.DiscardContents);

            //Sized off the back buffer, not the supersampled target: the glare is blurred anyway, so the
            //extra samples buy nothing and would only cost fill rate to produce. Level zero is HALF the
            //back buffer and each level halves again — about a third of a back buffer of HDR memory in all.
            if (_bloomChain != null) foreach (RenderTarget2D level in _bloomChain) level?.Dispose();

            _bloomChain = new RenderTarget2D[BLOOM_LEVELS];
            for (int i = 0; i < BLOOM_LEVELS; i++)
            {
                int levelWidth = Math.Max(_device.PresentationParameters.BackBufferWidth >> (i + 1), 1);
                int levelHeight = Math.Max(_device.PresentationParameters.BackBufferHeight >> (i + 1), 1);
                _bloomChain[i] = new RenderTarget2D(_device, levelWidth, levelHeight, false, SurfaceFormat.HdrBlendable, DepthFormat.None);
            }
        }

        /// <summary>
        /// Box-filters the supersampled HDR scene onto the back buffer, tonemaps it from linear radiance
        /// into display range and encodes it to sRGB — the frame's one and only exit from linear light. Runs
        /// the glare first, by construction, because the bright pass reads the scene target and has to
        /// happen before the back buffer is bound. Leaves the back buffer bound with
        /// <see cref="BlendState.Opaque"/>, <see cref="DepthStencilState.None"/> and
        /// <see cref="RasterizerState.CullNone"/> — callers drawing 3D after the resolve restate their own
        /// states, exactly as they did when this lived in each executable.
        /// </summary>
        /// <param name="clockSeconds">A wall clock for the film grain's per-frame re-roll. The modulo keeps
        /// the seed small: the shader takes its fraction, and a float that has grown for an hour has little
        /// left to give.</param>
        /// <param name="underwaterAmount">How submerged the lens is, 0–1; zero is a no-op in the shader.
        /// Scene knowledge (which scene has water, where its surface is) stays with the caller.</param>
        public void Resolve(float clockSeconds, float underwaterAmount)
        {
            DrawGlare();

            _device.SetRenderTarget(null);

            //The constants (exposure, glare intensity, supersample factor, the underwater colours) were set
            //once through the properties and persist on the effect; only what can change goes out per frame
            _tonemapGlareTextureParam.SetValue(_bloomChain[0]);
            _tonemapSceneTextureParam.SetValue(_sceneTarget);
            _tonemapSourceTexelSizeParam.SetValue(new Vector2(1f / _sceneTarget.Width, 1f / _sceneTarget.Height));

            //The grain re-rolls every frame and lands one grain per OUTPUT pixel, so the seed and the
            //back-buffer size go out here
            _tonemapGrainSeedParam.SetValue(clockSeconds % 64f);
            _tonemapOutputSizeParam.SetValue(new Vector2(
                _device.PresentationParameters.BackBufferWidth,
                _device.PresentationParameters.BackBufferHeight));

            _tonemapUnderwaterAmountParam.SetValue(underwaterAmount);

            _device.BlendState = BlendState.Opaque;
            _device.DepthStencilState = DepthStencilState.None;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SetVertexBuffer(_fullScreenQuad);

            DrawFullScreenQuad(_tonemapEffect);
        }

        /// <summary>
        /// The bloom pyramid (#69): the bright pass lands in the half-resolution head, is downsampled level
        /// by level to the foot, and each level is tent-upsampled ADDITIVELY into the one above — the head
        /// ends carrying its own tight halo plus every wider one, and the tonemap reads just the head.
        /// </summary>
        private void DrawGlare()
        {
            _device.BlendState = BlendState.Opaque;
            _device.DepthStencilState = DepthStencilState.None;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SetVertexBuffer(_fullScreenQuad);

            _device.SetRenderTarget(_bloomChain[0]);
            _glareEffect.CurrentTechnique = _glareBrightPassTechnique;
            _glareSourceTextureParam.SetValue(_sceneTarget);
            DrawFullScreenQuad(_glareEffect);

            _glareEffect.CurrentTechnique = _bloomDownTechnique;

            for (int i = 1; i < BLOOM_LEVELS; i++)
            {
                _device.SetRenderTarget(_bloomChain[i]);
                _glareSourceTextureParam.SetValue(_bloomChain[i - 1]);
                _glareSourceTexelSizeParam.SetValue(new Vector2(1f / _bloomChain[i - 1].Width, 1f / _bloomChain[i - 1].Height));
                DrawFullScreenQuad(_glareEffect);
            }

            //Back up the pyramid, ADDING each level into the one above: the down pass's own content still
            //sits in the destination, so the upsample accumulates onto it and no extra target is needed.
            _device.BlendState = BlendState.Additive;
            _glareEffect.CurrentTechnique = _bloomUpTechnique;

            for (int i = BLOOM_LEVELS - 1; i >= 1; i--)
            {
                _device.SetRenderTarget(_bloomChain[i - 1]);
                _glareSourceTextureParam.SetValue(_bloomChain[i]);
                _glareSourceTexelSizeParam.SetValue(new Vector2(1f / _bloomChain[i].Width, 1f / _bloomChain[i].Height));
                DrawFullScreenQuad(_glareEffect);
            }

            _device.BlendState = BlendState.Opaque;
        }

        private void DrawFullScreenQuad(Effect effect)
        {
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
            }
        }

        /// <summary>
        /// The quad the post-processing passes draw. Its corners are already in normalized device
        /// coordinates, so no pass needs a transform of any kind.
        /// </summary>
        private void CreateFullScreenQuad()
        {
            VertexPositionTexture[] corners =
            {
                new(new Vector3(-1f, 1f, 0f), new Vector2(0f, 0f)),
                new(new Vector3(1f, 1f, 0f), new Vector2(1f, 0f)),
                new(new Vector3(-1f, -1f, 0f), new Vector2(0f, 1f)),
                new(new Vector3(1f, -1f, 0f), new Vector2(1f, 1f))
            };

            _fullScreenQuad = new VertexBuffer(_device, VertexPositionTexture.VertexDeclaration, corners.Length, BufferUsage.WriteOnly);
            _fullScreenQuad.SetData(corners);
        }

        /// <summary>The targets and the quad are the pipeline's own; the effects are the content manager's.</summary>
        public void Dispose()
        {
            _sceneTarget?.Dispose();
            if (_bloomChain != null) foreach (RenderTarget2D level in _bloomChain) level?.Dispose();
            _fullScreenQuad?.Dispose();
        }
    }
}
