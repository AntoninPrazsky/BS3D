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
    /// display space. The one optional stage is the <b>defocus</b> — the whole frame taken out of focus by a
    /// caller's amount, built on demand out of the very same target and mixed in by the resolve (see the
    /// DEFOCUS block below) — with one exemption: the <b>sharp foreground</b> layer, a second HDR target for
    /// the one object a caller wants presented over the softening frame rather than softened with it.
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

        //THE DEFOCUS: the whole frame taken out of focus by a caller's amount, for the moment a level ends and
        //for a pause over one (the game's result and pause screens, #178). It is the frame going soft rather
        //than the frame being dimmed, which is the point of it — the fireworks go on flaring and the camera
        //goes on swinging around the island, and the numbers over them are read against an arena that has
        //stopped competing for the eye.
        //
        //Everything about it is sized off the back buffer at a QUARTER per axis, and that divisor is the
        //whole trick: a tap's spacing there reaches four back-buffer pixels, so the thirteen-tap kernel in
        //Glare.fx spans a width no full-resolution blur could afford, and the bilinear upsample the tonemap's
        //read performs costs nothing on an image that is blurred anyway. The scene comes down to it in two
        //dual-filter steps rather than one, so each working texel is an average of the whole block it stands
        //for instead of a sparse sample of it.
        private const int DEFOCUS_DIVISOR = 4;

        //Peak spacing between the blur's taps, in working-resolution texels — so the reach at full effect is
        //6 taps * this * DEFOCUS_DIVISOR back-buffer pixels either side, and the sigma about a third of that.
        //Kept near one texel: the working image already averages a DEFOCUS_DIVISOR-wide block per texel, and
        //taps spread much wider than what each of them covers would show as separate ghosts of a bright point
        //rather than as one smooth spread of it.
        private const float DEFOCUS_MAX_STEP = 1.4f;

        //How far into the ramp the blurred copy has fully taken over. It is deliberately EARLY: a mix held at
        //a middling value is the sharp frame and its own blur visible at once, which reads as a double
        //exposure, so the crossfade is got out of the way while the blur is still narrow and the rest of the
        //ramp is the radius growing — one image going soft, which is what a lens does.
        private const float DEFOCUS_MIX_IN = 0.3f;

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

        //The defocus's four targets, all in linear radiance like the scene they hold: the halfway step down,
        //the working copy at DEFOCUS_DIVISOR, and the two halves of the separable blur over it. Created
        //LAZILY (see EnsureDefocusChain) — the effect is one executable's, and the two that never ask for it
        //never pay the memory.
        private RenderTarget2D _defocusHalf, _defocusSmall, _defocusAcross, _defocusBlurred;

        //THE SHARP FOREGROUND: a second HDR layer the defocus cannot reach (#225). The game's result page
        //presents the won cup over an arena the defocus is melting into bokeh, and the cup is the thing
        //being watched — so it draws here instead of into the scene, the blur is built from a scene that
        //does not contain it (no blurred ghost standing behind the sharp cup), and
        // <see cref="CompositeForeground"/> tonemaps it onto the resolved frame after the fact, through the
        //same exposure, curve and grain. Created as LAZILY as the defocus chain, and for the same reason:
        //only one executable's one page ever shows it.
        private RenderTarget2D _foregroundTarget;

        private VertexBuffer _fullScreenQuad;

        //Cached in the constructor: the resolve runs every frame and the by-name indexer is a linear scan
        //over the effect's parameter list. Values that never change after startup are set once through the
        //properties below and never touched again; the textures and texel sizes still go out per frame
        //through these references, because the render targets are recreated on every resize.
        private readonly EffectTechnique _glareBrightPassTechnique;
        private readonly EffectTechnique _bloomDownTechnique;
        private readonly EffectTechnique _bloomUpTechnique;
        private readonly EffectTechnique _defocusBlurTechnique;
        private readonly EffectTechnique _tonemapTechnique;
        private readonly EffectTechnique _foregroundCompositeTechnique;
        private readonly EffectParameter _glareSourceTextureParam;
        private readonly EffectParameter _glareSourceTexelSizeParam;
        private readonly EffectParameter _glareThresholdParam;
        private readonly EffectParameter _blurStepParam;
        private readonly EffectParameter _tonemapDefocusTextureParam;
        private readonly EffectParameter _tonemapDefocusAmountParam;
        private readonly EffectParameter _tonemapForegroundTextureParam;
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

        //What the tonemap was last told the defocus mix is. Held so the uniform is written on the frames it
        //actually moves — which in play is none of them, the effect being off — per the caching discipline in
        //BestPractices.md. The starting value is the zero the constructor sends.
        private float _defocusMix;

        //The composite's blend: the foreground layer rides PREMULTIPLIED alpha (an opaque write's colour is
        //its full colour whatever partial coverage antialiasing left the sample), so covering the frame is
        //source as-is plus destination by what the coverage has not taken. Static rather than per frame,
        //like every state object here — see BestPractices.md.
        private static readonly BlendState PremultipliedForegroundBlend = new()
        {
            ColorSourceBlend = Blend.One,
            ColorDestinationBlend = Blend.InverseSourceAlpha,
            AlphaSourceBlend = Blend.One,
            AlphaDestinationBlend = Blend.InverseSourceAlpha
        };

        public PostProcessPipeline(GraphicsDevice device, Effect tonemapEffect, Effect glareEffect)
        {
            _device = device;
            _tonemapEffect = tonemapEffect;
            _glareEffect = glareEffect;

            _glareBrightPassTechnique = glareEffect.Techniques["BrightPass"];
            _bloomDownTechnique = glareEffect.Techniques["BloomDown"];
            _bloomUpTechnique = glareEffect.Techniques["BloomUp"];
            _defocusBlurTechnique = glareEffect.Techniques["DefocusBlur"];
            _tonemapTechnique = tonemapEffect.Techniques["Tonemap"];
            _foregroundCompositeTechnique = tonemapEffect.Techniques["ForegroundComposite"];
            _glareSourceTextureParam = glareEffect.Parameters["SourceTexture"];
            _glareSourceTexelSizeParam = glareEffect.Parameters["SourceTexelSize"];
            _glareThresholdParam = glareEffect.Parameters["GlareThreshold"];
            _blurStepParam = glareEffect.Parameters["BlurStep"];
            _tonemapDefocusTextureParam = tonemapEffect.Parameters["DefocusTexture"];
            _tonemapDefocusAmountParam = tonemapEffect.Parameters["DefocusAmount"];
            _tonemapForegroundTextureParam = tonemapEffect.Parameters["ForegroundTexture"];
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

            //And the defocus starts off. Stated rather than assumed: an unwritten uniform is whatever the
            //compiled constant buffer happens to hold, and the one it gates is a texture that does not exist
            //yet — the chain is not built until something first asks to blur.
            _tonemapDefocusAmountParam.SetValue(0f);

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
        /// The sharp foreground layer's own HDR target (see the SHARP FOREGROUND block at the top): the
        /// caller binds it, clears it transparent and draws the one object the defocus must not take.
        /// Configured exactly like the scene's own target — supersampled or multisampled by the same rule,
        /// so the object's edges come out as antialiased as they were inside the HDR pass, and the
        /// composite's box filter reads them the same way — and as lazy about existing: the getter builds it
        /// on first use and carries a resize the same way <see cref="EnsureDefocusChain"/> does, so the
        /// executables that never present one never allocate it.
        /// </summary>
        public RenderTarget2D ForegroundTarget
        {
            get
            {
                int width = _device.PresentationParameters.BackBufferWidth * _supersampleFactor;
                int height = _device.PresentationParameters.BackBufferHeight * _supersampleFactor;

                //The minimized-window guard is EnsureTarget's own reasoning: a zero-sized target is a
                //device error, and the restore path asks again anyway.
                if (width <= 0 || height <= 0) return _foregroundTarget;

                if (_foregroundTarget == null || _foregroundTarget.Width != width || _foregroundTarget.Height != height)
                {
                    _foregroundTarget?.Dispose();

                    //MSAA with supersampling off and none with it on, exactly as the scene target: the
                    //layer's silhouette is the one thing the composite's coverage read has to get right,
                    //and the rule's whole point is that geometry edges stay antialiased either way.
                    _foregroundTarget = new RenderTarget2D(_device, width, height, false, SurfaceFormat.HdrBlendable,
                        DepthFormat.Depth24Stencil8, _supersampleFactor > 1 ? 0 : MSAA_SAMPLES, RenderTargetUsage.DiscardContents);
                }

                return _foregroundTarget;
            }
        }

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
        /// <param name="defocusAmount">How far the frame has gone out of focus, 0–1; zero is a no-op in the
        /// shader and skips the blur's passes entirely, so a frame that is not blurring pays nothing. Whose
        /// moment it is and how it is timed stays with the caller — see the DEFOCUS block at the top.</param>
        /// <param name="foreground">The sharp foreground layer this frame's <see cref="ForegroundTarget"/>
        /// holds, or null when nothing is being presented. Not the defocus's business — the layer is never
        /// in the scene the blur reads — but the glare's: the pass below feeds the layer's own bright pass
        /// into the bloom pyramid, so the object keeps the glints it was lit for. Null, which is every
        /// frame of every executable but one page of the game's, skips it and costs nothing.</param>
        public void Resolve(float clockSeconds, float underwaterAmount, float defocusAmount, Texture2D foreground = null)
        {
            //Before the glare, though either order would do: both read the scene target and neither writes
            //the other's, and all that is required of both is that they run while the back buffer is still
            //unbound. The glare is left to go last so it is the pass that leaves the states the resolve wants.
            if (defocusAmount > 0f) DrawDefocus(defocusAmount);

            DrawGlare(foreground);

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

            //How much of the blurred copy shows. Reached early on purpose (see DEFOCUS_MIX_IN), and written
            //only when it moves — so an unblurred frame, which is every frame of play, sends nothing at all.
            //The texture itself is bound where it can change, in EnsureDefocusChain, and not here.
            float defocusMix = defocusAmount > 0f ? Math.Min(1f, defocusAmount / DEFOCUS_MIX_IN) : 0f;

            if (defocusMix != _defocusMix)
            {
                _defocusMix = defocusMix;
                _tonemapDefocusAmountParam.SetValue(defocusMix);
            }

            _device.BlendState = BlendState.Opaque;
            _device.DepthStencilState = DepthStencilState.None;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SetVertexBuffer(_fullScreenQuad);

            DrawFullScreenQuad(_tonemapEffect);
        }

        /// <summary>
        /// Composites the sharp foreground layer over the resolved frame — the layer's own exit from linear
        /// light, run by the same effect and the same figures (exposure, ACES, grain, sRGB) as the resolve
        /// itself, so all the move out of the HDR pass changed about the object is that the defocus no
        /// longer takes it. Call immediately after <see cref="Resolve"/> handed it the layer: the composite
        /// samples with the texel size the resolve just sent, which is the layer's own (the two targets are
        /// the same size by construction), and it draws on top of everything the resolve left behind.
        /// </summary>
        /// <param name="foreground">The layer this frame drew into <see cref="ForegroundTarget"/>. What is
        /// being presented is the caller's knowledge; where it lives is the pipeline's.</param>
        public void CompositeForeground(Texture2D foreground)
        {
            _device.SetRenderTarget(null);

            //The technique goes back to the resolve's own afterwards, and for a real reason rather than
            //tidiness: nothing else in this class ever sets it, so a composite that left its own behind
            //would have the next frame's resolve quietly resolving with it.
            _device.BlendState = PremultipliedForegroundBlend;
            _device.DepthStencilState = DepthStencilState.None;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SetVertexBuffer(_fullScreenQuad);

            _tonemapForegroundTextureParam.SetValue(foreground);
            _tonemapEffect.CurrentTechnique = _foregroundCompositeTechnique;
            DrawFullScreenQuad(_tonemapEffect);
            _tonemapEffect.CurrentTechnique = _tonemapTechnique;

            //The blend especially: the resolve leaves Opaque behind on purpose, and every caller after
            //this draws expecting it
            _device.BlendState = BlendState.Opaque;
        }

        /// <summary>
        /// The bloom pyramid (#69): the bright pass lands in the half-resolution head, is downsampled level
        /// by level to the foot, and each level is tent-upsampled ADDITIVELY into the one above — the head
        /// ends carrying its own tight halo plus every wider one, and the tonemap reads just the head.
        /// </summary>
        /// <param name="foreground">The sharp foreground layer, if a frame is presenting one: bright-passed
        /// ADDITIVELY into the head the scene's own bright pass has just filled, so its glints ride the same
        /// pyramid with the same threshold as everything that emits. Safe to add because
        /// <c>BrightPassPS</c> keeps only the excess over the threshold, which is never negative.</param>
        private void DrawGlare(Texture2D foreground)
        {
            _device.BlendState = BlendState.Opaque;
            _device.DepthStencilState = DepthStencilState.None;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SetVertexBuffer(_fullScreenQuad);

            _device.SetRenderTarget(_bloomChain[0]);
            _glareEffect.CurrentTechnique = _glareBrightPassTechnique;
            _glareSourceTextureParam.SetValue(_sceneTarget);
            DrawFullScreenQuad(_glareEffect);

            //The sharp foreground's glints, before the down pass so they widen through the whole pyramid
            //exactly as the scene's own do — this is the one thing the move out of the HDR pass would
            //otherwise have cost the presented object.
            if (foreground != null)
            {
                _device.BlendState = BlendState.Additive;
                _glareSourceTextureParam.SetValue(foreground);
                DrawFullScreenQuad(_glareEffect);
                _device.BlendState = BlendState.Opaque;
            }

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

        /// <summary>
        /// Builds the blurred copy of the scene the tonemap mixes in: two dual-filter steps down to the
        /// working resolution, then the separable Gaussian across it and down it. Reads the same HDR scene
        /// target the glare does and writes only its own four.
        /// </summary>
        /// <param name="amount">How far the effect has come, 0–1. It drives the blur's <b>width</b> here,
        /// where <see cref="Resolve"/> uses it for the mix — two curves off one dial, deliberately, and the
        /// reason is in <see cref="DEFOCUS_MIX_IN"/>.</param>
        private void DrawDefocus(float amount)
        {
            EnsureDefocusChain();

            _device.BlendState = BlendState.Opaque;
            _device.DepthStencilState = DepthStencilState.None;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SetVertexBuffer(_fullScreenQuad);

            _glareEffect.CurrentTechnique = _bloomDownTechnique;

            DrawDefocusStepDown(_sceneTarget, _defocusHalf);
            DrawDefocusStepDown(_defocusHalf, _defocusSmall);

            //The separable Gaussian: across, then down. The spacing is what grows with the effect and the tap
            //count is fixed — see Glare.fx's DefocusBlur for why round that way. Each pass reads what the
            //previous one wrote and writes a target of its own, so nothing is ever a pass's source and its
            //destination at once.
            float step = amount * DEFOCUS_MAX_STEP;

            _glareEffect.CurrentTechnique = _defocusBlurTechnique;

            _device.SetRenderTarget(_defocusAcross);
            _glareSourceTextureParam.SetValue(_defocusSmall);
            _blurStepParam.SetValue(new Vector2(step / _defocusSmall.Width, 0f));
            DrawFullScreenQuad(_glareEffect);

            _device.SetRenderTarget(_defocusBlurred);
            _glareSourceTextureParam.SetValue(_defocusAcross);
            _blurStepParam.SetValue(new Vector2(0f, step / _defocusAcross.Height));
            DrawFullScreenQuad(_glareEffect);
        }

        /// <summary>
        /// One step of the way down to the defocus's working resolution, through the bloom's own 5-tap
        /// downsample.
        /// </summary>
        /// <remarks>
        /// The kernel's four corner taps are placed off the <b>destination's</b> texel and not the source's,
        /// which is the one thing this could get wrong: that kernel is written for a source at exactly twice
        /// the destination, where one source texel is half a destination texel — but the scene target is
        /// <see cref="SupersampleFactor"/> times larger again, so offsets sized off it would sample a corner
        /// of the block an output texel stands for and alias the rest of it away.
        /// </remarks>
        private void DrawDefocusStepDown(RenderTarget2D source, RenderTarget2D destination)
        {
            _device.SetRenderTarget(destination);
            _glareSourceTextureParam.SetValue(source);
            _glareSourceTexelSizeParam.SetValue(new Vector2(0.5f / destination.Width, 0.5f / destination.Height));
            DrawFullScreenQuad(_glareEffect);
        }

        /// <summary>
        /// (Re)creates the defocus chain, sized off the back buffer. Called from <see cref="DrawDefocus"/> and
        /// nowhere else, which is what makes it <b>lazy</b>: the effect belongs to one executable's end-of-level
        /// screen, so the map editor and the testbed never build these targets at all. The size test carries a
        /// resize as well — a window that changed shape while nothing was blurring simply gets a new chain on
        /// the first frame that is, which is also the one frame the allocation could ever be noticed on, and it
        /// is a frame whose blur is still at nothing.
        /// </summary>
        private void EnsureDefocusChain()
        {
            int width = Math.Max(_device.PresentationParameters.BackBufferWidth / DEFOCUS_DIVISOR, 1);
            int height = Math.Max(_device.PresentationParameters.BackBufferHeight / DEFOCUS_DIVISOR, 1);

            if (_defocusBlurred != null && _defocusBlurred.Width == width && _defocusBlurred.Height == height) return;

            _defocusHalf?.Dispose();
            _defocusSmall?.Dispose();
            _defocusAcross?.Dispose();
            _defocusBlurred?.Dispose();

            //Half of the back buffer, whatever the divisor is, since it is only the halfway house the
            //dual-filter kernel wants on the way down
            _defocusHalf = NewDefocusTarget(
                Math.Max(_device.PresentationParameters.BackBufferWidth / 2, 1),
                Math.Max(_device.PresentationParameters.BackBufferHeight / 2, 1));

            _defocusSmall = NewDefocusTarget(width, height);
            _defocusAcross = NewDefocusTarget(width, height);
            _defocusBlurred = NewDefocusTarget(width, height);

            //Bound here, where it can change, rather than per frame in the resolve: this is the only moment
            //the tonemap's defocus texture is ever a different object, and a rebuild that left the shader
            //pointing at the disposed one would show as a blurred frame going black on a resize.
            _tonemapDefocusTextureParam.SetValue(_defocusBlurred);
        }

        /// <summary>
        /// One of the defocus's targets: HDR like the scene it holds — it is the same linear radiance, blurred
        /// — with no depth buffer and no multisampling, every one of them being written whole by a full-screen
        /// quad.
        /// </summary>
        private RenderTarget2D NewDefocusTarget(int width, int height) =>
            new(_device, width, height, false, SurfaceFormat.HdrBlendable, DepthFormat.None);

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

            //Null in an executable that never blurred — see EnsureDefocusChain. The foreground target's
            //null is the same story in an executable that never presented anything over the defocus
            _defocusHalf?.Dispose();
            _defocusSmall?.Dispose();
            _defocusAcross?.Dispose();
            _defocusBlurred?.Dispose();
            _foregroundTarget?.Dispose();

            _fullScreenQuad?.Dispose();
        }
    }
}
