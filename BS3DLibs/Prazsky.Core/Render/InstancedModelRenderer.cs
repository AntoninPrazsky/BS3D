using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Renders many instances of a single three-dimensional model using GPU (hardware) instancing:
    /// one draw call per model mesh part, no matter how many instances there are.
    /// Lighting mirrors <see cref="BasicEffect"/> with default lighting enabled and per-pixel shading,
    /// so instanced models look the same as those rendered through <see cref="ModelRenderer"/>.
    /// </summary>
    public class InstancedModelRenderer : IDisposable
    {
        private static readonly Vector3 DEFAULT_SPECULAR_COLOR = Vector3.One;
        private const float DEFAULT_SPECULAR_POWER = 16f;

        private struct MeshPartData
        {
            public VertexBuffer VertexBuffer;
            public IndexBuffer IndexBuffer;
            public int VertexOffset;
            public int StartIndex;
            public int PrimitiveCount;
            public Matrix BoneTransform;
            public Vector4 DiffuseColor;
            public Vector3 EmissiveColor;
            public Vector3 SpecularColor;
            public float SpecularPower;
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Effect _effect;

        /// <summary>
        /// Whether <see cref="_effect"/> is GamePi's Potato effect (#789, <c>GamePi/Shaders/PotatoModel.fx</c>) rather
        /// than InstancedModel.fx: then <see cref="Draw(ICamera, ModelInstance[], int, BasicEffectParams, Vector3?)"/>
        /// goes through <see cref="DrawPotato"/>, the depth and refraction passes do nothing, and only the uniforms
        /// <see cref="InitializePotatoEffect"/> caches exist. Recognised by the effect, set once at construction.
        /// </summary>
        private readonly bool _potato;

        /// <summary>
        /// Whether this renderer draws through GamePi's Potato effect (#789). Every surface is opaque there and the
        /// bubble's shell shading does not exist (<see cref="BubbleShell"/> is never read), so a caller that puts a
        /// transparent material out in two walls has one wall's worth of work to do (#797).
        /// </summary>
        public bool Potato => _potato;

        //The lava on the Potato path (#795): BallLava.fxh's LavaCrustTint and LavaCrustDark, the near-black basalt its seams
        //glow through, and the share of a ball's surface those seams and the hot crust round them light, on average. Potato
        //draws no seams, and the lava's EmissiveStrength is 0 (its seams ARE its glow), so without these a lava ball was a
        //plain lit ball in the scene's dusk and the volcano's cluster near black. The share is matched to the desktop's
        //Caldera captured at the same moment (#795): see "The Potato path" in docs/rendering.md.
        private const float POTATO_LAVA_CRUST_TINT = 0.62f;
        private const float POTATO_LAVA_CRUST_DARK = 0.16f;
        private const float POTATO_LAVA_SEAM_SHARE = 0.3f;

        //The balls' rims on the Potato path (#804): see PotatoModel.fx's PotatoBallRim for what a rim is
        private EffectTechnique _potatoBallRimTechnique, _potatoBallFlatTechnique;
        private EffectParameter _rimShapeParam, _rimSecantParam;

        //The fins of this renderer's mesh on the Potato path (#804): its outline's anti-aliasing, see EdgeFinMesh. Found
        //by the mesh's vertex buffer when the renderer is built; null for a mesh that has none (every mesh, unless the
        //host turned EdgeFins on before building its scene).
        private EdgeFinMesh _fins;
        private EffectTechnique _potatoFinLitTechnique, _potatoFinTexturedTechnique;
        private EffectTechnique _potatoBandTechnique;
        private EffectParameter _potatoBandSeamParam;
        private EffectParameter _finShapeParam;

        /// <summary>
        /// The width of the fins' ramp in pixels of the target (#804), and their switch: 0, the default, draws none; 1
        /// is the exact coverage of a pixel by an edge. One figure for every renderer, like
        /// <see cref="RimRampPixels"/>, which it is the companion of: the balls have rims, everything else has fins.
        /// </summary>
        public static float FinRampPixels { get; set; }

        //What is constant over a Potato draw, multiplied out here and not per pixel (#804): see PotatoModel.fx's uniform
        //block for what each is. DrawPotato computes them; the effect has no uniform for the figures they are made of.
        private EffectParameter _potatoAmbientMidParam, _potatoAmbientTiltParam, _potatoEnvironmentMidParam,
            _potatoEnvironmentTiltParam, _potatoReflectanceParam, _potatoReflectanceRiseParam;
        private EffectParameter _potatoBallCrustParam, _potatoBallEmissionStillParam, _potatoBallEmissionBeatParam,
            _potatoBallGlowStillParam, _potatoBallGlowBeatParam, _potatoBallFlashParam, _potatoBallAlarmParam,
            _potatoPulsePhaseParam;

        /// <summary>
        /// The strip this renderer's balls' rims are drawn on (#804), and the switch: with one set, and
        /// <see cref="RimPass"/> on, <see cref="Draw(ICamera, ModelInstance[], int, BasicEffectParams, Vector3?)"/> on the
        /// Potato path draws the outlines' rings in place of the balls themselves. Null, the default, is no rims; only a
        /// ball renderer is ever given one. The mesh is the caller's to dispose.
        /// </summary>
        public BallRimMesh RimMesh { get; set; }

        /// <summary>
        /// Whether a draw is the balls' rims and not the balls (#804). The caller - <c>BallRenderSet</c> - draws every
        /// bucket once as it always did and then, with this on, every bucket once more: the same calls with the same
        /// per-bucket uniforms, so a ring is shaded exactly as the ball it finishes, and all of them after all of the
        /// balls, which is what a ring's blend needs to find behind it.
        /// </summary>
        public bool RimPass { get; set; }

        /// <summary>
        /// How far this renderer's mesh may fall short of the true circle, as a share of the radius:
        /// <c>1 - cos(pi / slices)</c> for a sphere of that many slices. The rim's strip reaches that far inside the
        /// outline (and a pixel more), so it fills the slivers a coarse mesh leaves and the outline is a circle.
        /// </summary>
        public float RimMeshShortfall { get; set; }

        /// <summary>The sphere mesh's radius in its own units, which the rim's shader scales by the instance's.</summary>
        public float RimMeshRadius { get; set; } = 0.5f;

        /// <summary>
        /// The width of the rims' ramp in pixels of the target (#804): 1 is the exact coverage of a pixel by an edge,
        /// more is softer. One figure for every renderer, being a property of the picture and not of a mesh.
        /// </summary>
        public static float RimRampPixels { get; set; } = 1f;

        /// <summary>
        /// Testing only (#804): every Potato ball drawn as its flat colour, no light at all - the floor under any cheaper
        /// ball shader, measured on the Pi before one is written.
        /// </summary>
        public static bool PotatoFlatBalls { get; set; }

        private EffectTechnique _potatoTexturedTechnique, _potatoBallTechnique, _potatoBallDitherTechnique;
        private readonly MeshPartData[] _parts;
        private DynamicVertexBuffer _instanceBuffer;
        private readonly ModelInstance[] _singleInstance = new ModelInstance[1];

        private EffectParameter _viewParam;
        private EffectParameter _projectionParam;
        private EffectParameter _boneParam;
        private EffectParameter _eyePositionParam;
        private EffectParameter _diffuseColorParam;
        private EffectParameter _emissiveColorParam;
        private EffectParameter _ambientColorParam;
        private EffectParameter _specularColorParam;
        private EffectParameter _specularPowerParam;
        private EffectParameter _skyColorParam;
        private EffectParameter _groundColorParam;
        private EffectParameter _keyLightPositionParam;
        private EffectParameter _lightViewProjectionParam;
        private EffectParameter _groundHeightParam;
        private EffectParameter _textureParam;
        private EffectParameter _detailScaleParam;
        private EffectParameter _detailStrengthParam;
        private EffectParameter _detailBoostParam;
        private EffectParameter _surfaceReliefStrengthParam;
        private EffectParameter _slabSizeParam;
        private EffectParameter _slabJointWidthParam;
        private EffectParameter _slabJointDepthParam;
        private EffectParameter _jointGlowParam, _jointGlowPatchinessParam, _topDustTintParam, _topDustStrengthParam;
        private EffectParameter _slabWarpParam, _sideDustTintParam, _sideDustStrengthParam;
        private EffectParameter _bandTopYParam, _bandFadeParam, _bandTintParam, _bandWetParam, _bandStrengthParam,
            _strataSpacingParam, _strataStrengthParam;
        private EffectParameter _topDustClearParam;
        private EffectParameter _cavityStrengthParam;
        private EffectParameter _specularAmbientStrengthParam;
        private EffectParameter _metalnessParam;
        private EffectParameter _twoSidedNormalsParam;
        private EffectParameter _specularAlphaWeightParam;
        private EffectParameter _dirLightStrengthParam;
        private EffectParameter _emissiveStrengthParam;
        private EffectParameter _translucencyStrengthParam;
        private EffectParameter _pulseTimeParam;
        private EffectParameter _dissolvePixelSizeParam;
        private EffectParameter _pulseSpeedParam;
        private EffectParameter _pulseDepthParam;
        private EffectParameter _stillEmissionParam;
        private EffectParameter _pulseDirectionParam;
        private EffectParameter _pulseWavelengthParam;
        private EffectParameter _rippleStrengthParam;
        private EffectParameter _rippleAlarmColorParam;
        private EffectParameter _emissiveTintParam;
        private EffectParameter _dirLight0DiffuseParam;
        private EffectParameter _dirLight0SpecularParam;
        private EffectParameter _dirLight1DiffuseParam;
        private EffectParameter _dirLight1SpecularParam;
        private EffectParameter _dirLight2DiffuseParam;
        private EffectParameter _dirLight2SpecularParam;
        private EffectParameter _surfaceReliefFrequencyParam;
        private EffectParameter _patternPrimaryColorParam;
        private EffectParameter _patternSecondaryColorParam;
        private EffectParameter _wildcardProgressParam;
        private EffectParameter _patternGoreCountParam;
        private EffectParameter _patternGoreThresholdParam;
        private EffectParameter _patternCapExtentParam;
        private EffectParameter _patternReliefStrengthParam;
        private EffectTechnique _mainTechnique;
        private EffectTechnique _triplanarTechnique;
        private EffectTechnique _triplanarCoarseTechnique;
        private EffectTechnique[] _triplanarProbeTechniques;
        private EffectTechnique[] _ballTechniques;
        private EffectParameter _bubbleShellParam;
        private EffectParameter _bubbleFilmThicknessParam;
        private EffectParameter _bubbleTintStrengthParam;
        private EffectParameter _bubbleBodyOpacityParam;
        private EffectParameter _marbleVeinFrequencyParam;
        private EffectParameter _marbleVeinWarpParam;
        private EffectParameter _marbleVeinContrastParam;
        private EffectParameter _woolStrandFrequencyParam;
        private EffectParameter _woolStrandDepthParam;
        private EffectParameter _woolHaloParam;
        private EffectParameter _metalBrushFrequencyParam;
        private EffectParameter _metalBrushDepthParam;
        private EffectParameter _metalReflectanceParam;
        private EffectParameter _iceCrackFrequencyParam;
        private EffectParameter _iceCrackWidthParam;
        private EffectParameter _iceRimParam;
        private EffectParameter _icePlateContrastParam;
        private EffectParameter _gemFacetCountParam;
        private EffectParameter _gemFacetDepthParam;
        private EffectParameter _gemAbsorptionParam;
        private EffectParameter _plasmaWarpParam;
        private EffectParameter _plasmaGlowParam;
        private EffectParameter _plasmaSpeedParam;
        private EffectParameter _lavaSeamFrequencyParam;
        private EffectParameter _lavaSeamWidthParam;
        private EffectParameter _lavaGlowParam;
        private EffectParameter _porcelainCrackFrequencyParam;
        private EffectParameter _porcelainCrackWidthParam;
        private EffectParameter _porcelainGlazeParam;
        private EffectParameter _hollowSeamFrequencyParam;
        private EffectParameter _stoneGrainFrequencyParam;
        private EffectParameter _stoneGrainContrastParam;
        private EffectParameter _stoneRoughnessParam;
        private EffectParameter _stoneShapeDepthParam;
        private EffectTechnique _cityTechnique;
        private EffectParameter _cityWindowBrightnessParam;

        /// <summary>
        /// How brightly the lit windows of a city building glow (0 = this renderer is not a city, and
        /// takes whichever technique its material would otherwise select). The facade needs no texture
        /// and no UVs: the window grid is evaluated from world position, so one box mesh scaled per
        /// instance gives a hundred-storey tower a hundred storeys rather than stretching one facade.
        /// </summary>
        public float CityWindowBrightness { get; set; }

        /// <summary>
        /// Wall clock, in seconds, driving the city's windows switching on and off. Each window keeps its
        /// own rhythm off this one clock, so it has to be fed from real time rather than from the
        /// simulation — the lamps of a city do not stop when the physics is paused.
        /// </summary>
        public float CityWindowTime { get; set; }

        private EffectParameter _cityWindowTimeParam;

        /// <summary>
        /// 0 = ordinary city (warm and cool lamps), 1 = neon: near-black facades and vivid saturated signs,
        /// one hue per tower. Meant to run with a window brightness well over the glare threshold so the
        /// signs bloom into neon.
        /// </summary>
        public float CityNeon { get; set; }

        private EffectParameter _cityNeonParam;

        /// <summary>
        /// The city's window/look configuration, pushed to the shader on each city draw. Its defaults
        /// reproduce the values that used to be hard-coded in the shader, so a renderer that is never given a
        /// config still draws the original city. Only the city technique reads these.
        /// </summary>
        public CitySceneConfig CityConfig { get; set; } = new();

        private EffectParameter _windowPitchXParam, _windowPitchYParam, _windowFillXParam, _windowFillYParam,
            _windowFrameWidthParam, _windowFrameHeightParam, _windowFrameShadingParam,
            _windowFrameToneParam, _windowSillHeightParam, _windowSillOverhangParam, _windowSillShadowParam,
            _windowSillShadingParam, _windowRevealDepthParam, _windowBarWidthParam,
            _windowMarginParam, _windowLitFractionParam, _windowWarmParam, _windowCoolParam,
            _windowHoldSecondsParam, _windowHoldVariationParam, _windowSwitchSecondsParam,
            _windowRestlessFractionParam, _windowBuzzFractionParam;

        //The plaster the windows sit in: its albedo, its grain, and how it answers light against how the glass
        //does. Only the city technique reads these, and the same CityConfig pushes them, so all three
        //executables get one facade rather than three that drift.
        private EffectParameter _facadeColorParam, _facadeNeonColorParam, _facadeColorVariationParam,
            _facadeGrainStrengthParam, _facadeGrainFrequencyParam, _facadeGrainShadingParam,
            _facadeSmoothnessParam, _facadeHighlightParam,
            _windowHighlightBoostParam, _windowReflectionBoostParam, _windowGlassColorParam;

        private EffectTechnique _depthTechnique;
        private EffectTechnique _refractionTechnique;
        private EffectParameter _refractionDepthParam;

        //The refracting glass (#541): its technique and the four figures only it reads
        private EffectTechnique _glassTechnique;
        private EffectParameter _glassBehindParam, _glassHalfExtentsParam, _glassCutPeriodParam, _glassCutSlopeParam,
            _glassCornerParam, _glassCrownInsetParam, _glassCrownDropParam, _glassRimParam;

        //The result page's polished metal (#602): reads only uniforms every plain draw already sets
        private EffectTechnique _polishedMetalTechnique;
        private EffectTechnique _crystalTechnique;
        private EffectTechnique _drainGlassTechnique;

        /// <summary>
        /// Optional detail texture modulating the material colors of the mesh. Applied to opaque meshes
        /// only; translucent ones (e.g. glass) stay clean. It is projected along the three world axes and
        /// blended by the surface normal (the triplanar techniques), so it needs no UVs — the procedural
        /// meshes carry none — but it is fixed in world space and only suits objects that never move.
        /// (A UV-mapped path, <c>DetailMapping.ModelUVs</c> with its normal map, existed for the loaded
        /// cannon model and was deleted in #581 with everything else only a loaded model could reach.)
        /// </summary>
        public Texture2D DetailTexture { get; set; }

        /// <summary>
        /// Size of the detail texture: world units per tile = 1 / <see cref="DetailScale"/>.
        /// </summary>
        public float DetailScale { get; set; } = 0.25f;

        /// <summary>How strongly the detail texture modulates the material color (0 = not at all, 1 = fully).</summary>
        public float DetailStrength { get; set; } = 0.5f;

        /// <summary>Brightness compensation so a mid-gray detail texture does not darken the material.</summary>
        public float DetailBoost { get; set; } = 1f;

        /// <summary>
        /// Peak height of the procedural micro-relief of this model's surface, in world units
        /// (0 = the flat shading of a geometrically perfect surface). It only tilts the normal, so the
        /// silhouette stays exactly as modeled; what changes is that the surface catches light
        /// unevenly the way a real material does. Unlike a normal map it needs no
        /// texture, does not tile, and keeps its detail right down to the pixel that can still show it.
        /// </summary>
        public float SurfaceReliefStrength { get; set; }

        /// <summary>
        /// Base wave count per world unit of <see cref="SurfaceReliefStrength"/>: larger values give a
        /// finer grain. Three more octaves of projected gradient noise ride on top at rising frequencies (#674; it
        /// was six more sines), each fading out on its own once a pixel grows past half a noise cell.
        /// </summary>
        public float SurfaceReliefFrequency { get; set; } = 10f;

        /// <summary>
        /// The coarse height field on the triplanar path: two relief octaves instead of four (three sines of seven until
        /// #674), which is
        /// what a quality tier below <c>High</c> draws on the arena's stone cap and on nothing else. The
        /// coursed slab joints are unaffected — they are cut by <c>SlabGroove</c>, not by the octaves — so
        /// what is given up is the stone's finest grain and not its structure.
        /// <para>
        /// A separate compiled technique rather than a uniform, for the reason #155 measured on the scene
        /// shaders and the glass bubble restates: a runtime branch over an alternative shading path costs
        /// the union of both register allocations in every wavefront, and these passes are occupancy-bound.
        /// <b>Measured</b> at 0.336 ms of a 10.971 ms frame — see the technique's own header in
        /// <c>InstancedModel.fx</c> for the whole isolation, and <see cref="ArenaIsland.SurfaceDetail"/>
        /// for the dial the host actually turns.
        /// </para>
        /// </summary>
        public bool CoarseSurfaceRelief { get; set; }

        /// <summary>
        /// #151's measurement probes, and only the triplanar path reads it. 0 draws the shipped
        /// <c>InstancedModelTriplanar</c>, 3 the coarse technique <see cref="CoarseSurfaceRelief"/> selects,
        /// and 1/2/4/5/6 one of the cut-down copies declared beside them in <c>InstancedModel.fx</c>, each
        /// with one named suspect taken out. It exists so the stone cap's per-pixel cost can be split up
        /// within ONE build (the Testbed's <c>capprobe=N</c>) rather than by several builds measured against
        /// each other, and it is kept for the same reason <see cref="ArenaIsland.Members"/> is: the ratio
        /// #151 was opened on is the weak machine's and still has to be re-derived there.
        /// </summary>
        public int TriplanarProbe { get; set; }

        /// <summary>
        /// Edge length of one floor slab in world units (0 = no slabs). The joints between slabs are cut
        /// into the same height field as the micro-relief, so they are real recesses: they darken in
        /// their own shade and catch the light along their bevels — structure at a scale the eye can see,
        /// where micro-relief alone is far too shallow to read.
        /// </summary>
        public float SlabSize { get; set; }

        /// <summary>Half-width of the flat floor of a slab joint, in world units.</summary>
        public float SlabJointWidth { get; set; } = 0.02f;

        /// <summary>How far a slab joint sinks below the slab faces, in world units.</summary>
        public float SlabJointDepth { get; set; } = 0.05f;

        /// <summary>
        /// What the slab joints' floors put out, as linear radiance (#535) — the volcano island's cooling
        /// cracks, the cavern island's mineral veins. Zero, the default, costs the triplanar paths nothing:
        /// the shader's branch on it skips. Read at the groove's floor squared, so only the deepest part of a
        /// joint glows and its bevel stays dark.
        /// </summary>
        public Vector3 JointGlow { get; set; }

        /// <summary>
        /// How much of <see cref="JointGlow"/> is gated by the shader's world-space patch noise (#537): 1, the
        /// default, lights about a third of the joint grid in patches — a cooling crack, an ore vein; 0 lights
        /// every joint evenly along its whole length — a seam in a made thing.
        /// </summary>
        public float JointGlowPatchiness { get; set; } = 1f;

        /// <summary>
        /// Dust settled on the up-facing faces of a triplanar surface (#535): a modulation of the albedo — the
        /// ratio of the dust's linear colour to the material's, so <see cref="Vector3.One"/> is no change —
        /// and <see cref="TopDustStrength"/> how much of it, 0 none. Keyed to the geometric normal.
        /// </summary>
        public Vector3 TopDustTint { get; set; } = Vector3.One;

        /// <inheritdoc cref="TopDustTint"/>
        public float TopDustStrength { get; set; }

        /// <summary>
        /// The same modulation for the side faces (#534) — rime on the ice islands' drums — keyed to how far
        /// the geometric normal is from vertical. <see cref="Vector3.One"/> and 0 are no change.
        /// </summary>
        public Vector3 SideDustTint { get; set; } = Vector3.One;

        /// <inheritdoc cref="SideDustTint"/>
        public float SideDustStrength { get; set; }

        /// <summary>
        /// A band keyed to world height on the side faces (#536) — the sea stack's tide line, the beach
        /// platform's sand crust round its foot: everything under <see cref="BandTopY"/> takes
        /// <see cref="BandTint"/> (an albedo ratio, <see cref="Vector3.One"/> none), fading out over
        /// <see cref="BandFade"/> world units above it along a wandering line, and mirrors
        /// <see cref="BandWet"/> times more of the sky there. <see cref="BandStrength"/> 0, the default, is none.
        /// </summary>
        public float BandTopY { get; set; }

        /// <inheritdoc cref="BandTopY"/>
        public float BandFade { get; set; } = 1f;

        /// <inheritdoc cref="BandTopY"/>
        public Vector3 BandTint { get; set; } = Vector3.One;

        /// <inheritdoc cref="BandTopY"/>
        public float BandWet { get; set; }

        /// <inheritdoc cref="BandTopY"/>
        public float BandStrength { get; set; }

        /// <summary>
        /// Bedding planes on the side faces (#536): layers <see cref="StrataSpacing"/> world units thick, each a
        /// course of its own shade with a dark line where two meet, darkened by up to <see cref="StrataStrength"/>.
        /// 0, the default, is none.
        /// </summary>
        public float StrataSpacing { get; set; } = 1f;

        /// <inheritdoc cref="StrataSpacing"/>
        public float StrataStrength { get; set; }

        /// <summary>
        /// The top dust blown clear round the world's axis (#538) — the lunar pad's powder swept off in a ring round
        /// the drain: X the radius inside which <see cref="TopDustTint"/> is gone, Y how far outside it the dust
        /// fades back in. Zero clears nothing.
        /// </summary>
        public Vector2 TopDustClear { get; set; }

        /// <summary>
        /// How far the slab joint grid is bent by a world-space noise, in world units (#534): 0, the default,
        /// is the square grid; about a unit turns it into the net of wandering fractures a sheet of ice has.
        /// One noise read per pixel of a surface that asks for it.
        /// </summary>
        public float SlabWarp { get; set; }

        /// <summary>
        /// How dark the pits of the relief go from being shaded by their own walls (0 = off, 1 = black).
        /// Without it a normal-perturbed surface has its bumps lit but its hollows just as bright as its
        /// peaks, which is most of why relief-by-normal reads as a painted-on texture rather than shape.
        /// </summary>
        public float CavityStrength { get; set; }

        /// <summary>
        /// How strongly the surface reflects the sky as an environment, on top of the direct lights'
        /// highlights (0 = off, 1 = the material's own specular reflectance). Roughness is taken from the
        /// material's Blinn-Phong exponent, so nothing has to be re-authored: a polished material mirrors
        /// the sky, a rough one gathers its average, and every material turns reflective at a grazing
        /// angle through the Fresnel term. This is most of what separates a real surface from a plastic
        /// one — a lit object mostly shows its surroundings, not the lamp.
        /// </summary>
        public float SpecularAmbientStrength { get; set; } = 1f;

        /// <summary>
        /// 0 = a dielectric (stone, glass, vinyl, paint — the default for everything), whose environment
        /// reflection is the ~4% dielectric F0 tinted by the specular color and only turns mirror-like at
        /// grazing angles. 1 = bare metal, whose reflectance <i>is</i> its specular color, so the whole
        /// surface reflects the sky in that tint (gold reflects gold). Used by the funnel's gold rims.
        /// </summary>
        public float Metalness { get; set; }

        /// <summary>
        /// Draws the plain material as <b>polished metal</b> (#602): the <c>InstancedPolishedMetal</c> technique,
        /// which mirrors a sharp version of the dome — a horizon that is an edge, a bright band along it, a dark
        /// one under it, the sun as a glint — with no diffuse at all, the <see cref="BasicEffectParams.SpecularColor"/>
        /// as the alloy's reflectance and <see cref="SpecularAmbientStrength"/> as how much of it is mirrored. The
        /// plain material's own reflection is the dome's linear ramp, which is right for every other surface in the
        /// game and is why a polished object drawn through it reads as painted. Only the result page's metal cups
        /// and their metal jewellery set it; a part that selects a city, ball, triplanar or glass technique ignores
        /// it. See <c>InstancedModel/PolishedMetal.fxh</c> for the measurements behind each figure.
        /// </summary>
        public bool PolishedMetal { get; set; }

        /// <summary>
        /// Draws the plain material with <b>natural inclusions</b> laid into it (#640): the <c>InstancedCrystal</c>
        /// technique, the plain material's own shading plus a veil with a thin film's rainbow, rutile threads and a
        /// dusting of specks, in the mesh's space so they turn with it. The result page's crystal cup sets it.
        /// </summary>
        public bool Crystal { get; set; }

        /// <summary>
        /// Draws the plain material as <b>used glass</b> (#640): the <c>InstancedDrainGlass</c> technique, the plain
        /// material's shading plus the marks the drain's traffic leaves - scuffs running downhill, a frosted landing
        /// band inside the mouth - and a few seeds and a faint cord. The drain funnel sets it.
        /// </summary>
        public bool DrainGlass { get; set; }

        /// <summary>
        /// Draws through <c>PotatoBand</c> on the Potato path (#804): <c>PotatoLit</c> for a mesh whose vertices carry
        /// their height off the surface they lie on, faded out over one pixel where that height comes down to it, so
        /// the seam where the mesh goes under another is not left to the depth test (the drain's gold band,
        /// <see cref="FunnelRimsMesh"/>). Nothing on the desktop, which multisamples.
        /// </summary>
        public bool PotatoBand { get; set; }

        /// <summary>Whether a <see cref="PotatoBand"/>'s runs of the kind that goes under the pit have the pit under
        /// them this frame: <see cref="FunnelRimsMesh.SEAM_PIT"/>.</summary>
        public bool PotatoBandUnderPit { get; set; }

        /// <summary>
        /// 1 flips the shading normal on back faces, for a mesh that is one <b>open single-sided wall</b>
        /// drawn with culling off — the drain's glass funnel and its dark pit shaft, whose only normal
        /// points at the concave side the balls rest on. Without it the outside of such a wall is shaded
        /// with the inside's normal, and under the grazing-angle sky sheen the glass cone read from below
        /// as an opaque milky sheet (#291) — the very shot the drop cinematic's dive films. 0 (the default)
        /// for everything else: a closed mesh never shows a back face worth shading.
        /// </summary>
        public float TwoSidedNormals { get; set; }

        /// <summary>
        /// How far this material's specular terms — the direct highlight and the reflected environment —
        /// are attenuated by its own alpha. 1 (the default, and what every opaque surface wants because
        /// alpha is 1 there anyway) scales both by the material alpha, which is what the shader always did.
        /// <b>0 leaves them at full strength, which is what a transparent surface actually does</b>: alpha
        /// says how much of what is behind a surface comes through, and a reflection is light coming off the
        /// front of it — the same argument <see cref="EmissiveTint"/> already makes for a glowing pane.
        /// <para>
        /// Set to 0 by the result screen's crystal trophy (#228) and nothing else. Attenuated, a
        /// 38 %-transparent cup keeps 38 % of its own sparkle and reads as a coloured film; unattenuated,
        /// the sky flares off it and the frame behind it still shows through, which is cut glass.
        /// </para>
        /// </summary>
        public float SpecularAlphaWeight { get; set; } = 1f;

        /// <summary>
        /// How much of the three-light rig (key, fill, back — DirLight0–2) reaches this renderer's surface,
        /// 1 by default. This is per-renderer where the tints cannot be: <see cref="SetLightTint"/> writes
        /// the shared effect's DirLight* colors, one set of values for the whole scene, so a dimmed tint on
        /// one renderer would dim every renderer drawn after it. The ceiling glass — a pane whose backdrop is
        /// the sky itself — is dimmed to that sky's own brightness through this figure instead (#156,
        /// <c>SkyLightRig.ApplyToGlass</c>). The scene point lights are deliberately not scaled by it: they
        /// sit on top of the rig in the shader, and a cave's own glow still reaches a pane under a dark sky.
        /// </summary>
        public float DirLightStrength { get; set; } = 1f;

        /// <summary>
        /// The frame behind this surface, when the surface is a slab of glass that bends it (#541): a copy of the
        /// scene drawn so far, which the <c>InstancedGlass</c> technique shows where each pixel's ray leaves the
        /// slab, under the surface's own lit colour — the ceiling's plate, handed the copy by the Game for the one
        /// draw that uses it. <b>Null (the default) draws the surface as it always was</b>: the plain lit material,
        /// alpha-blended over an undisplaced frame. Only a plain part reads it — no texture, no pattern, no detail.
        /// <para>
        /// A slab and nothing else: the technique traces the ray through the box <see cref="GlassHalfExtents"/>
        /// describes, in the mesh's own space, and assumes the instance is not rotated (the ceiling only ever
        /// moves down). It writes its pixel whole (alpha 1) rather than blending, since the copy already holds
        /// what the blend would have laid it over — so anything drawn between the copy and this draw that stands
        /// behind the glass is covered, and the caller takes the copy at the point that separates what is behind
        /// the glass from what is in front of it.
        /// </para>
        /// </summary>
        public Texture2D GlassBehind { get; set; }

        /// <summary>The refracting slab's half size along each axis, about the mesh's origin (#541).</summary>
        public Vector3 GlassHalfExtents { get; set; } = new(0.5f, 0.5f, 0.5f);

        /// <summary>The spacing of the diamond cut the refracting technique cuts into the slab's top face, in world units (#541).</summary>
        public float GlassCutPeriod { get; set; } = 1f;

        /// <summary>How steep that cut's facets are, as the tangent of their tilt; 0 leaves the top face flat (#541).</summary>
        public float GlassCutSlope { get; set; }

        /// <summary>
        /// The outline the refracting slab was built on (#541): the radius every corner's cuts are tangent to, and
        /// how many cuts round each corner — <see cref="CutSlabMesh"/>'s own two figures, so the trace follows the
        /// outline the mesh draws.
        /// </summary>
        public float GlassCornerRadius { get; set; }

        /// <inheritdoc cref="GlassCornerRadius"/>
        public int GlassCornerFacets { get; set; }

        /// <summary>
        /// The slab's top edge in section (#541), four points from the side band up to the top face's rim: X how far
        /// in from the outline, Y how far below the top face. A ray the trace sends out through the top's plane
        /// within that edge leaves through the facet between two of them.
        /// </summary>
        public Vector4 GlassCrownInset { get; set; }

        /// <inheritdoc cref="GlassCrownInset"/>
        public Vector4 GlassCrownDrop { get; set; }

        /// <summary>
        /// The border of flutes the refracting technique cuts round the top face inside its rim (#541): the border's
        /// width, the flutes' spacing (both world units), their facets' tilt and the border's lean down towards the
        /// rim (both tangents). A zero width leaves the diamond cut running to the rim.
        /// </summary>
        public Vector4 GlassRim { get; set; }

        /// <summary>
        /// Number of primary-colored gores of the procedural beach-ball pattern (segments around
        /// the object = twice this; 0 = no pattern). The pattern is evaluated in the model's own
        /// object space, so it turns with the object and makes a rolling ball's rotation readable.
        /// Applies to untextured opaque mesh parts; the diffuse tint passed to
        /// <see cref="Draw(ICamera, ModelInstance[], int, BasicEffectParams, Vector3?)"/> becomes
        /// the primary gore color and the material diffuse shades the whole pattern.
        /// </summary>
        public int PatternGoreCount { get; set; }

        /// <summary>Color of the other gores and of the polar discs of the beach-ball pattern.</summary>
        public Vector3 PatternSecondaryColor { get; set; } = Vector3.One;

        /// <summary>
        /// Fraction of each pair of segments taken by the primary-colored gore: 0.5 gives gores of
        /// equal width, more leaves the secondary color as the narrower strip between them.
        /// </summary>
        public float PatternGoreWidth { get; set; } = 0.62f;

        /// <summary>
        /// Where the polar discs of the beach-ball pattern start, as the |Y| of the object-space
        /// direction (1 = the pole itself).
        /// </summary>
        public float PatternCapExtent { get; set; } = 0.9f;

        /// <summary>
        /// Amplitude of the molded micro-relief of the patterned surface, in world units
        /// (0 = a mathematically smooth sphere). It only tilts the normal, so what it changes is the
        /// way the highlight breaks up — the silhouette stays a clean circle.
        /// </summary>
        public float PatternReliefStrength { get; set; } = 0.007f;

        /// <summary>How far through the current colour crossing a wildcard is (#632), read by
        /// <see cref="BallShading.Wildcard"/> alone: 0 shows the colour it is leaving (the draw's tint), 1 the one it is
        /// going to (<see cref="PatternSecondaryColor"/>).</summary>
        public float WildcardProgress { get; set; }

        /// <summary>
        /// <b>What the patterned parts are made of</b> — which of the shader's ball techniques shades them
        /// (#258, #304). Every one of them replaces the beach-ball skin rather than joining it: the gores, the
        /// polar discs, the welds and the moulding are all vinyl, and no other material has any of them, so
        /// each is a technique of its own selected here wherever <see cref="PatternGoreCount"/> would
        /// otherwise have selected the pattern's.
        /// <para>
        /// <b>A shading does not carry its drawing states, and for a transparent one they are not optional.</b>
        /// A bubble's shell must be put out as two passes with opposite cull modes and the right depth states
        /// between them, and this property does not and cannot do that — it says how a pixel is shaded, not in
        /// what order the pixels arrive. <c>Prazsky.BS3D.BallRenderSet.Draw</c> is the one caller that sets
        /// this, and it carries the whole of that argument.
        /// </para>
        /// <para>
        /// This was a <c>bool GlassBubble</c> until #304. Two shadings fit in a flag; the styles split out of
        /// #272 do not, and eight flags would be eight ways to ask for two materials at once.
        /// </para>
        /// </summary>
        public BallShading Shading { get; set; }

        /// <summary>
        /// Which wall of a <see cref="BallShading.Bubble"/> shell the next draw puts out: <c>+1</c> the near one seen
        /// from outside, <c>-1</c> the far one seen from inside. It pairs with the caller's cull mode and must
        /// agree with it — the shader turns the geometric normal by this sign so both walls go through one
        /// piece of arithmetic, and a sign that disagrees with the cull lights the shell inside out.
        /// </summary>
        public float BubbleShell { get; set; } = 1f;

        /// <summary>
        /// Optical thickness of the film seen face-on, in whole waves of the reference wavelength — the pitch
        /// of the interference, and so the whole character of the soap rainbow. Under about 1 the film shows
        /// broad single-colour washes; over about 4 the fringes crowd into a fine oily marbling.
        /// </summary>
        public float BubbleFilmThickness { get; set; } = 2.2f;

        /// <summary>
        /// How much of its type colour the film carries into what it transmits. The dial that decides whether
        /// a cluster of thirteen colours is still readable at a glance.
        /// </summary>
        public float BubbleTintStrength { get; set; } = 1.4f;

        /// <summary>
        /// What the film hides where it is seen face-on, before the rim adds its own — and the figure that
        /// decides whether a ball's colour is nameable, since what a film does <i>not</i> hide is the backdrop
        /// arriving untinted. <c>Prazsky.BS3D.BallRenderSet.BUBBLE_BODY_OPACITY</c> is the one that states it
        /// for the balls and carries the arithmetic; this default only covers a caller that never says.
        /// </summary>
        public float BubbleBodyOpacity { get; set; } = 0.84f;

        /// <summary>
        /// Wave count of <see cref="BallShading.Marble"/>'s vein bands over the ball, before the turbulence
        /// bends them — the coarse spacing of the figure. Under about 3 the ball reads as two-tone rather than
        /// veined; far over it the bands crowd into a mottle that stops looking like stone.
        /// </summary>
        public float MarbleVeinFrequency { get; set; } = 5f;

        /// <summary>
        /// How far the turbulence bends those bands out of their parallel course, in the same units. The dial
        /// that separates marble from a barber's pole: at zero the bands are perfect circles round one axis,
        /// and it is the warp alone that makes them wander, split and rejoin the way a mineral seam does.
        /// </summary>
        public float MarbleVeinWarp { get; set; } = 6f;

        /// <summary>
        /// How far a vein carries the type colour towards white (0 = invisible, 1 = white). The figure the
        /// thirteen colours are spent on: a vein is the tint lightened and never a fixed grey, so a magenta
        /// ball gets pale magenta veins. <c>Prazsky.BS3D.BallRenderSet.MARBLE_VEIN_CONTRAST</c> states it for
        /// the balls and carries the argument; this default only covers a caller that never says.
        /// </summary>
        public float MarbleVeinContrast { get; set; } = 0.55f;

        /// <summary>
        /// How many strands are wound across a <see cref="BallShading.Wool"/> ball, as a wave count over the
        /// object-space direction — the diameter shows about a third of this many crossings. Under about 12 the
        /// strands read as fat tubes; over about 40 they cross into a felt with no strand in it.
        /// </summary>
        public float WoolStrandFrequency { get; set; } = 24f;

        /// <summary>
        /// Peak height of a wool strand's ridge in world units, exactly as <see cref="PatternReliefStrength"/>
        /// is for the vinyl's moulding. It only tilts the normal, so the silhouette stays the sphere's.
        /// </summary>
        public float WoolStrandDepth { get; set; } = 0.02f;

        /// <summary>
        /// How brightly the loose fibres at the silhouette catch the light, in the ball's own colour — the cue
        /// that says <i>soft</i>. It is light added over every ball's rim at once, so a cluster is where it is
        /// judged and never a single ball; <c>Prazsky.BS3D.BallRenderSet.WOOL_HALO</c> states it for the balls.
        /// </summary>
        public float WoolHalo { get; set; } = 0.25f;

        /// <summary>
        /// Wave count of the brush grain over a <see cref="BallShading.Metal"/> ball. It is this style's whole
        /// rotation cue and not a detail: a perfect mirror sphere spinning looks identical frame to frame,
        /// because the reflection is view- and world-dependent and nothing on the surface turns.
        /// </summary>
        public float MetalBrushFrequency { get; set; } = 70f;

        /// <summary>
        /// Peak height of a brush ridge in world units — a polish direction, not a corrugation. Anything deep
        /// enough to read as ridges stops being brushed metal and becomes a screw thread.
        /// </summary>
        public float MetalBrushDepth { get; set; } = 0.006f;

        /// <summary>
        /// How much of the environment the metal mirrors. There is no diffuse term underneath to carry the ball
        /// if it is set too low, which is the difference between this and every other style's reflection dial.
        /// </summary>
        public float MetalReflectance { get; set; } = 1.6f;

        /// <summary>
        /// How many fracture cells a <see cref="BallShading.Ice"/> ball is broken into — the plate size. The
        /// count goes as the ball's <b>area</b>, about 4·π·f² over the whole of it; far over this a ball reads
        /// as shattered gravel, far under it as two or three continents.
        /// </summary>
        public float IceCrackFrequency { get; set; } = 1.8f;

        /// <summary>
        /// How wide an ice crack opens, as a fraction of a plate. The <b>mean</b> and not the width: every
        /// stretch of crack multiplies it by its own wander, because a net drawn at one width all over a ball
        /// is a drawn net rather than a fracture.
        /// </summary>
        public float IceCrackWidth { get; set; } = 0.09f;

        /// <summary>
        /// How strongly each of an ice ball's plates differs from its neighbours — the low-frequency half of
        /// the figure, and the half that survives to play distance where the hairline between two plates does
        /// not (#337).
        /// </summary>
        public float IcePlateContrast { get; set; } = 0.22f;

        /// <summary>
        /// How brightly an ice ball's silhouette goes cool and pale. At full strength it eats the tint on every
        /// ball's rim at once, and a cluster is mostly rims, so this is judged on a pile and never on one ball.
        /// </summary>
        public float IceRim { get; set; } = 0.5f;

        /// <summary>
        /// How finely a <see cref="BallShading.Gem"/>'s object-space direction is quantized, and so how many
        /// faces the stone is cut into. Each step up adds a shell of lattice directions.
        /// </summary>
        public float GemFacetCount { get; set; } = 2f;

        /// <summary>
        /// How hard the facet height field drives the shading normal towards its own face. The mesh is never
        /// touched — the silhouette stays a circle, which is #271's ruling.
        /// </summary>
        public float GemFacetDepth { get; set; } = 1.4f;

        /// <summary>
        /// How deeply a gem absorbs its own colour along the view, so the stone is darkest where it is
        /// thickest. It decides whether the four dark types stay apart from one another.
        /// </summary>
        public float GemAbsorption { get; set; } = 1.1f;

        /// <summary>
        /// How far the first noise field displaces the second's sample point on a
        /// <see cref="BallShading.Plasma"/> ball — the whole character of the arcs. At zero they are smooth
        /// rings; the warp is what makes them writhe, fork and rejoin.
        /// </summary>
        public float PlasmaWarp { get; set; } = 0.35f;

        /// <summary>
        /// How brightly a plasma filament burns. It is the whole of this style's colour and the only thing
        /// standing between a cluster of them and darkness.
        /// </summary>
        public float PlasmaGlow { get; set; } = 2.4f;

        /// <summary>
        /// How fast the arcs crawl. Slow: anything quick enough to notice as <i>animation</i> stops reading as
        /// something alive and starts reading as a loop.
        /// </summary>
        public float PlasmaSpeed { get; set; } = 0.5f;

        /// <summary>
        /// How many plate seams run over a <see cref="BallShading.Lava"/> ball. Low: a cooling crust breaks
        /// into a handful of big plates, and a dense net reads as gravel rather than a cracked shell.
        /// </summary>
        public float LavaSeamFrequency { get; set; } = 4.5f;

        /// <summary>
        /// How wide a lava seam is — wider than the ice's cracks, because a crack is a plane seen edge-on and
        /// this is a gap with molten rock at the bottom of it.
        /// </summary>
        public float LavaSeamWidth { get; set; } = 0.22f;

        /// <summary>
        /// How brightly the molten interior glows through the seams. It is the whole of this style's colour:
        /// over a black crust there is nothing else to see the ball by.
        /// </summary>
        public float LavaGlow { get; set; } = 2.6f;

        /// <summary>
        /// Wave count of a <see cref="BallShading.Porcelain"/> ball's crazing — the finest figure in this
        /// file, since craquelure <i>is</i> fine, but still bounded by what a ball a few dozen pixels across
        /// can resolve.
        /// </summary>
        public float PorcelainCrackFrequency { get; set; } = 13f;

        /// <summary>
        /// How wide a hairline is. The thinnest in the file: a crack in a glaze has no area at all, and a wide
        /// one reads as a broken egg rather than an antique.
        /// </summary>
        public float PorcelainCrackWidth { get; set; } = 0.045f;

        /// <summary>
        /// How deep and wet the glaze looks — how much of the environment its face mirrors. It decides whether
        /// a dark glaze under a bright dome washes out to sky colour.
        /// </summary>
        public float PorcelainGlaze { get; set; } = 0.9f;

        /// <summary>
        /// How many cast seams run over a <see cref="BallShading.Hollow"/> ball — the clear glass's whole
        /// <b>rotation cue</b>, and the reason it has one at all (#325).
        /// <para>
        /// An undyed shell has no gores, no veining and no grain: everything that names it — the Fresnel rim,
        /// the highlight, what shows through it — is a function of the eye and the sky, so a spinning glass ball
        /// drawn without this is a still glass ball. The contract in <c>InstancedModel.fx</c>'s ball header
        /// calls that out as the trap a mirror falls into, and a mould seam is what a cast glass marble
        /// genuinely has: object-space, so it turns with the ball.
        /// </para>
        /// <para>
        /// Two, because a mould has two halves. More would read as a faceted or grooved ball rather than as a
        /// cast one, and the seam is meant to be the thing you notice only once it moves.
        /// </para>
        /// </summary>
        public float HollowSeamFrequency { get; set; } = 2f;

        /// <summary>
        /// Wave count of a <see cref="BallShading.Stone"/> ball's mineral grain over the ball — how coarse the
        /// granite is cut. High, because a grain is what separates rock from a grey ball: at a low count the
        /// speckle turns into blotches and the thing reads as a mouldy marble.
        /// </summary>
        public float StoneGrainFrequency { get; set; } = 14f;

        /// <summary>
        /// How far the grains carry from the stone's own grey, towards black one way and towards a pale quartz
        /// the other. The one dial that decides whether a rock is a <i>rock</i> or a grey sphere, and the only
        /// figure this shading spends anything on.
        /// </summary>
        public float StoneGrainContrast { get; set; } = 0.55f;

        /// <summary>
        /// Amplitude of the coarse lumpy relief, in world units — the surface's own roughness, tilting the
        /// normal the way the vinyl's moulding does. Larger than any other ball's figure, because it is the
        /// only unfinished surface in the set: everything else here was cast, blown, wound, ground or glazed.
        /// </summary>
        public float StoneRoughness { get; set; } = 0.06f;

        /// <summary>
        /// How deep a <see cref="BallShading.Stone"/> ball is carved <b>out of round</b>, as a fraction of its
        /// radius (#340). The only ball figure in the set that moves a <i>vertex</i>: the rock's silhouette is
        /// deliberately not a circle, where every other style's is. It carves inward only — the lattice packs
        /// balls at exactly two radii apart, so one that grew would push into its neighbour's cell.
        /// </summary>
        public float StoneShapeDepth { get; set; } = 0.20f;

        /// <summary>
        /// How much of its own color the surface radiates, independent of any light falling on it.
        /// Deliberately not occluded: a light source buried inside a pile is the one that should still
        /// show, glowing out past its neighbors.
        /// </summary>
        public float EmissiveStrength { get; set; }

        /// <summary>
        /// How much light is carried through the shell from a source behind it. A translucent ball lit
        /// from the far side glows around its rim rather than going flatly black, which is what tells the
        /// eye it is a skin around a volume instead of a painted solid.
        /// </summary>
        public float TranslucencyStrength { get; set; }

        /// <summary>Seconds since the scene started; drives <see cref="PulseSpeed"/>.</summary>
        public float PulseTime { get; set; }

        /// <summary>
        /// Width of one cell of the patterned surface's dissolve dither, in pixels of the <b>currently bound
        /// render target</b>. The dissolve is what a ball being re-coloured or a ghost of a landing is drawn
        /// with (the per-instance <c>Dissolve</c> element), and this is the size of the blocks it cuts away in.
        /// <para>
        /// Target pixels and not display pixels, because that is what the shader can measure — it reads the
        /// pixel's own <c>SV_POSITION</c>, which is in the bound target's space. A caller that supersamples has
        /// to multiply the display-pixel size it actually wants by its supersampling factor, or the resolve's
        /// box filter averages the dither back into a smooth fade and the pixelation the effect exists for
        /// disappears at exactly the settings that make everything else look better. <c>BallRenderSet.Draw</c>
        /// does that from the device itself and is the only thing that sets this.
        /// </para>
        /// <para>
        /// Defaults to 1 rather than 0 so an unset renderer cuts on single target pixels instead of dividing by
        /// zero — a fine grain is a wrong look, an infinity is a black ball.
        /// </para>
        /// </summary>
        public float DissolvePixelSize { get; set; } = 1f;

        /// <summary>Beats per second of the emissive pulse.</summary>
        public float PulseSpeed { get; set; } = 1f;

        /// <summary>
        /// How deep the pulse swings, as a fraction of <see cref="EmissiveStrength"/>
        /// (0 = a steady glow, 1 = all the way down to dark between beats).
        /// </summary>
        public float PulseDepth { get; set; } = 0.6f;

        /// <summary>
        /// What a ball drawn with <see cref="PulseDepth"/> at zero glows at, as a fraction of what a
        /// breathing one does <b>at rest</b> (#395). One — the no-op — everywhere except the still plane the
        /// loaded rounds are drawn on.
        /// <para>
        /// <b>It exists because "does not breathe" and "glows at the resting level" are not the same
        /// thing</b>, and #252 implemented the first meaning to get the second. Every emissive expression in
        /// the shader reduces to <c>lerp(1 - PulseDepth, 1, beat)</c>, so a depth of zero pins a ball at the
        /// <i>top</i> of the swing while the cluster it is meant to match sits at <c>1 - PulseDepth</c> for
        /// most of a heartbeat that is two short pulses and a long rest.
        /// </para>
        /// <para>
        /// <b>⚠ Not a smaller <see cref="PulseDepth"/>, which is the obvious alternative and is wrong:</b>
        /// that would make the loaded round breathe faintly, which is exactly what #252 took away on the
        /// owner's own ruling. This leaves it perfectly steady and only moves where it is steady.
        /// </para>
        /// </summary>
        public float StillEmission { get; set; } = 1f;

        /// <summary>
        /// Direction the beat travels through the scene, and how many world units one beat spans.
        /// The phase is offset by position along this direction, so a cluster reads as a wave passing
        /// through it rather than every instance flashing at once.
        /// </summary>
        public Vector3 PulseDirection { get; set; } = Vector3.Up;

        /// <summary>How many world units one beat of the emissive pulse spans along <see cref="PulseDirection"/>.</summary>
        public float PulseWavelength { get; set; } = 12f;

        /// <summary>
        /// How hard an instance flares at the peak of its <see cref="ModelInstance.Ripple"/>, as a multiple of
        /// its own colour. <b>Zero — the default — switches the whole term off</b> in the shader, on a branch
        /// over this uniform rather than over the per-instance value, so a renderer that never ripples pays
        /// nothing and cannot diverge inside a draw call.
        /// </summary>
        public float RippleStrength { get; set; }

        /// <summary>
        /// The flat colour a <b>negative</b> <see cref="ModelInstance.Ripple"/> flares in — the wave's other
        /// meaning, which carries no trace of the ball's own colour on purpose so every ball in it says the
        /// same thing. Linear radiance, and it is scaled by the shader's own brightness, so this is a hue and
        /// not a level.
        /// <para>
        /// Red by default, which is what it always was and what a <i>threat</i> should look like. It is a
        /// property rather than a constant because the same wave is used for something that is not a threat:
        /// a tall level's glass steps down to hand the player more of the column when they have cleared a
        /// lot of it, and a red flash there reads as being told off for playing well.
        /// </para>
        /// </summary>
        public Vector3 RippleAlarmColor { get; set; } = new(1f, 0.07f, 0.05f);

        /// <summary>
        /// Light this surface puts out on its own, in <b>linear</b> radiance, on top of everything it
        /// reflects — so it is not clamped to 1, and past <c>GLARE_THRESHOLD</c> it blooms. Zero (the default)
        /// leaves the surface exactly as it was.
        /// <para>
        /// It is not attenuated by the material's alpha: alpha is how much of what is <i>behind</i> a surface
        /// comes through, and a pane that is glowing is emitting rather than transmitting.
        /// </para>
        /// </summary>
        public Vector3 EmissiveTint { get; set; }

        /// <summary>
        /// Sky color of the hemisphere ambient light (received by upward-facing surfaces), in
        /// <b>linear</b> radiance — see <see cref="ColorSpace"/>. It is also the environment the
        /// specular ambient reflects, so it is not clamped to 1: a bright sky legitimately exceeds white.
        /// </summary>
        public Vector3 SkyColor { get; set; } = Vector3.One;

        /// <summary>
        /// Ground color of the hemisphere ambient light (received by downward-facing surfaces), in
        /// <b>linear</b> radiance — see <see cref="ColorSpace"/>.
        /// </summary>
        public Vector3 GroundColor { get; set; } = Vector3.One;

        /// <summary>
        /// World position of the key light (a positional "sun"). The default sits far away along the
        /// default key light direction, which is indistinguishable from a directional light.
        /// </summary>
        public Vector3 KeyLightPosition { get; set; } = -DefaultLighting.Light0Direction * 1000f;

        /// <summary>
        /// Y of the ground plane for the ground-contact part of the ambient occlusion
        /// (downward-facing surface near the ground darkens). The default is far enough below
        /// everything to have no effect.
        /// </summary>
        public float GroundHeight { get; set; } = -10000f;

        /// <summary>
        /// Bounding sphere of the whole model in model space (bone transforms applied). Useful for frustum culling.
        /// </summary>
        public BoundingSphere BoundingSphere { get; private set; }

        /// <summary>
        /// Points a renderer built from one procedural mesh at another (#533): the island keeps one cap and
        /// one drum renderer — with their material, relief and joint settings, and their place in every
        /// host's sky-lit list — and swaps the lathe under them when the scene's shape changes. Only the
        /// buffers, the primitive count, the bounds and the mesh's fins (#804) move; nothing else this renderer
        /// caches refers to the old mesh, since the bindings are built at draw time from the part. The mesh stays
        /// the caller's to dispose, as it always was.
        /// </summary>
        public void SetMesh(IProceduralMesh mesh)
        {
            if (_parts.Length != 1)
                throw new InvalidOperationException("Only a renderer built from one procedural mesh can be re-pointed.");

            MeshPartData part = _parts[0];
            part.VertexBuffer = mesh.VertexBuffer;
            part.IndexBuffer = mesh.IndexBuffer;
            part.PrimitiveCount = mesh.PrimitiveCount;
            _parts[0] = part;

            BoundingSphere = mesh.BoundingSphere;
            _fins = EdgeFins.Find(mesh.VertexBuffer);
        }

        /// <summary>
        /// Creates a renderer for drawing many instances of a procedurally generated mesh
        /// (e.g. a <see cref="SphereMesh"/> or <see cref="BoxMesh"/>) with the given material diffuse color.
        /// An <paramref name="alpha"/> below one makes the mesh translucent
        /// (draw it after the opaque scene, under <see cref="BlendState.AlphaBlend"/>).
        /// </summary>
        public InstancedModelRenderer(GraphicsDevice graphicsDevice, IProceduralMesh mesh, Vector3 materialDiffuseColor, Effect effect, float alpha = 1f)
        {
            _graphicsDevice = graphicsDevice;
            _effect = effect;

            _fins = EdgeFins.Find(mesh.VertexBuffer);

            _parts = new[]
            {
                new MeshPartData
                {
                    VertexBuffer = mesh.VertexBuffer,
                    IndexBuffer = mesh.IndexBuffer,
                    VertexOffset = 0,
                    StartIndex = 0,
                    PrimitiveCount = mesh.PrimitiveCount,
                    BoneTransform = Matrix.Identity,
                    DiffuseColor = new Vector4(materialDiffuseColor, alpha),
                    EmissiveColor = Vector3.Zero,
                    SpecularColor = DEFAULT_SPECULAR_COLOR,
                    SpecularPower = DEFAULT_SPECULAR_POWER
                }
            };

            BoundingSphere = mesh.BoundingSphere;

            //GamePi's Potato effect (#789) is recognised by its own technique, so no caller has to say which of the two
            //it is handing over: the host loads one or the other and every renderer built on it follows
            _potato = effect.Techniques["PotatoLit"] != null;

            if (_potato) InitializePotatoEffect();
            else InitializeEffect();
        }

        private void InitializeEffect()
        {
            _viewParam = _effect.Parameters["View"];
            _projectionParam = _effect.Parameters["Projection"];
            _boneParam = _effect.Parameters["Bone"];
            _eyePositionParam = _effect.Parameters["EyePosition"];
            _diffuseColorParam = _effect.Parameters["DiffuseColor"];
            _emissiveColorParam = _effect.Parameters["EmissiveColor"];
            _ambientColorParam = _effect.Parameters["AmbientColor"];
            _specularColorParam = _effect.Parameters["SpecularColor"];
            _specularPowerParam = _effect.Parameters["SpecularPower"];
            _skyColorParam = _effect.Parameters["SkyColor"];
            _groundColorParam = _effect.Parameters["GroundColor"];

            _effect.Parameters["DirLight1Direction"].SetValue(DefaultLighting.Light1Direction);
            _effect.Parameters["DirLight2Direction"].SetValue(DefaultLighting.Light2Direction);
            _keyLightPositionParam = _effect.Parameters["KeyLightPosition"];
            //Shadows.fxh's own matrix since #470, not a LightViewProjection of this pass's: the caster below
            //and the receivers in ShadePixel have to agree about where the light stands, and two uniforms
            //holding the same matrix is how they stop agreeing.
            _lightViewProjectionParam = _effect.Parameters["ShadowViewProjection"];
            _groundHeightParam = _effect.Parameters["GroundHeight"];

            _textureParam = _effect.Parameters["Texture"];
            _detailScaleParam = _effect.Parameters["DetailScale"];
            _detailStrengthParam = _effect.Parameters["DetailStrength"];
            _detailBoostParam = _effect.Parameters["DetailBoost"];
            _surfaceReliefStrengthParam = _effect.Parameters["SurfaceReliefStrength"];
            _slabSizeParam = _effect.Parameters["SlabSize"];
            _slabJointWidthParam = _effect.Parameters["SlabJointWidth"];
            _slabJointDepthParam = _effect.Parameters["SlabJointDepth"];
            _jointGlowParam = _effect.Parameters["JointGlow"];
            _jointGlowPatchinessParam = _effect.Parameters["JointGlowPatchiness"];
            _topDustTintParam = _effect.Parameters["TopDustTint"];
            _topDustStrengthParam = _effect.Parameters["TopDustStrength"];
            _slabWarpParam = _effect.Parameters["SlabWarp"];
            _sideDustTintParam = _effect.Parameters["SideDustTint"];
            _sideDustStrengthParam = _effect.Parameters["SideDustStrength"];
            _bandTopYParam = _effect.Parameters["BandTopY"];
            _bandFadeParam = _effect.Parameters["BandFade"];
            _bandTintParam = _effect.Parameters["BandTint"];
            _bandWetParam = _effect.Parameters["BandWet"];
            _bandStrengthParam = _effect.Parameters["BandStrength"];
            _strataSpacingParam = _effect.Parameters["StrataSpacing"];
            _strataStrengthParam = _effect.Parameters["StrataStrength"];
            _topDustClearParam = _effect.Parameters["TopDustClear"];
            _cavityStrengthParam = _effect.Parameters["CavityStrength"];
            _specularAmbientStrengthParam = _effect.Parameters["SpecularAmbientStrength"];
            _metalnessParam = _effect.Parameters["Metalness"];
            _twoSidedNormalsParam = _effect.Parameters["TwoSidedNormals"];
            _specularAlphaWeightParam = _effect.Parameters["SpecularAlphaWeight"];
            _dirLightStrengthParam = _effect.Parameters["DirLightStrength"];
            _emissiveTintParam = _effect.Parameters["EmissiveTint"];
            _surfaceReliefFrequencyParam = _effect.Parameters["SurfaceReliefFrequency"];
            _patternPrimaryColorParam = _effect.Parameters["PatternPrimaryColor"];
            _patternSecondaryColorParam = _effect.Parameters["PatternSecondaryColor"];
            _wildcardProgressParam = _effect.Parameters["WildcardProgress"];
            _patternGoreCountParam = _effect.Parameters["PatternGoreCount"];
            _patternGoreThresholdParam = _effect.Parameters["PatternGoreThreshold"];
            _patternCapExtentParam = _effect.Parameters["PatternCapExtent"];
            _patternReliefStrengthParam = _effect.Parameters["PatternReliefStrength"];
            _emissiveStrengthParam = _effect.Parameters["EmissiveStrength"];
            _translucencyStrengthParam = _effect.Parameters["TranslucencyStrength"];
            _pulseTimeParam = _effect.Parameters["PulseTime"];
            _dissolvePixelSizeParam = _effect.Parameters["DissolvePixelSize"];
            _pulseSpeedParam = _effect.Parameters["PulseSpeed"];
            _pulseDepthParam = _effect.Parameters["PulseDepth"];
            _stillEmissionParam = _effect.Parameters["StillEmission"];
            _pulseDirectionParam = _effect.Parameters["PulseDirection"];
            _pulseWavelengthParam = _effect.Parameters["PulseWavelength"];
            _rippleStrengthParam = _effect.Parameters["RippleStrength"];
            _rippleAlarmColorParam = _effect.Parameters["RippleAlarmColor"];
            _mainTechnique = _effect.Techniques["InstancedModel"];
            _triplanarTechnique = _effect.Techniques["InstancedModelTriplanar"];
            _triplanarCoarseTechnique = _effect.Techniques["InstancedModelTriplanarCoarse"];

            //#151's measurement probes, kept on purpose: they are the Testbed's capprobe= contract (see
            //TriplanarProbe). Looked up once here like every other technique, so selecting one costs an
            //array index and never a name scan.
            _triplanarProbeTechniques = new[]
            {
                _effect.Techniques["InstancedModelTriplanarProbe1"],
                _effect.Techniques["InstancedModelTriplanarProbe2"],
                _effect.Techniques["InstancedModelTriplanarCoarse"],  //3 is the coarse technique itself, not a probe
                _effect.Techniques["InstancedModelTriplanarProbe4"],
                _effect.Techniques["InstancedModelTriplanarProbe5"],
                _effect.Techniques["InstancedModelTriplanarProbe6"],
            };
            LoadBallTechniques();
            _bubbleShellParam = _effect.Parameters["BubbleShell"];
            _bubbleFilmThicknessParam = _effect.Parameters["BubbleFilmThickness"];
            _bubbleTintStrengthParam = _effect.Parameters["BubbleTintStrength"];
            _bubbleBodyOpacityParam = _effect.Parameters["BubbleBodyOpacity"];
            _marbleVeinFrequencyParam = _effect.Parameters["MarbleVeinFrequency"];
            _marbleVeinWarpParam = _effect.Parameters["MarbleVeinWarp"];
            _marbleVeinContrastParam = _effect.Parameters["MarbleVeinContrast"];
            _woolStrandFrequencyParam = _effect.Parameters["WoolStrandFrequency"];
            _woolStrandDepthParam = _effect.Parameters["WoolStrandDepth"];
            _woolHaloParam = _effect.Parameters["WoolHalo"];
            _metalBrushFrequencyParam = _effect.Parameters["MetalBrushFrequency"];
            _metalBrushDepthParam = _effect.Parameters["MetalBrushDepth"];
            _metalReflectanceParam = _effect.Parameters["MetalReflectance"];
            _iceCrackFrequencyParam = _effect.Parameters["IceCrackFrequency"];
            _iceCrackWidthParam = _effect.Parameters["IceCrackWidth"];
            _iceRimParam = _effect.Parameters["IceRim"];
            _icePlateContrastParam = _effect.Parameters["IcePlateContrast"];
            _gemFacetCountParam = _effect.Parameters["GemFacetCount"];
            _gemFacetDepthParam = _effect.Parameters["GemFacetDepth"];
            _gemAbsorptionParam = _effect.Parameters["GemAbsorption"];
            _plasmaWarpParam = _effect.Parameters["PlasmaWarp"];
            _plasmaGlowParam = _effect.Parameters["PlasmaGlow"];
            _plasmaSpeedParam = _effect.Parameters["PlasmaSpeed"];
            _lavaSeamFrequencyParam = _effect.Parameters["LavaSeamFrequency"];
            _lavaSeamWidthParam = _effect.Parameters["LavaSeamWidth"];
            _lavaGlowParam = _effect.Parameters["LavaGlow"];
            _porcelainCrackFrequencyParam = _effect.Parameters["PorcelainCrackFrequency"];
            _porcelainCrackWidthParam = _effect.Parameters["PorcelainCrackWidth"];
            _porcelainGlazeParam = _effect.Parameters["PorcelainGlaze"];
            _hollowSeamFrequencyParam = _effect.Parameters["HollowSeamFrequency"];
            _stoneGrainFrequencyParam = _effect.Parameters["StoneGrainFrequency"];
            _stoneGrainContrastParam = _effect.Parameters["StoneGrainContrast"];
            _stoneRoughnessParam = _effect.Parameters["StoneRoughness"];
            _stoneShapeDepthParam = _effect.Parameters["StoneShapeDepth"];
            _cityTechnique = _effect.Techniques["InstancedCity"];
            _cityWindowBrightnessParam = _effect.Parameters["CityWindowBrightness"];
            _cityWindowTimeParam = _effect.Parameters["CityWindowTime"];
            _cityNeonParam = _effect.Parameters["CityNeon"];
            _windowPitchXParam = _effect.Parameters["WindowPitchX"];
            _windowPitchYParam = _effect.Parameters["WindowPitchY"];
            _windowFillXParam = _effect.Parameters["WindowFillX"];
            _windowFillYParam = _effect.Parameters["WindowFillY"];
            _windowFrameWidthParam = _effect.Parameters["WindowFrameWidth"];
            _windowFrameHeightParam = _effect.Parameters["WindowFrameHeight"];
            _windowFrameShadingParam = _effect.Parameters["WindowFrameShading"];
            _windowFrameToneParam = _effect.Parameters["WindowFrameTone"];
            _windowSillHeightParam = _effect.Parameters["WindowSillHeight"];
            _windowSillOverhangParam = _effect.Parameters["WindowSillOverhang"];
            _windowSillShadowParam = _effect.Parameters["WindowSillShadow"];
            _windowSillShadingParam = _effect.Parameters["WindowSillShading"];
            _windowRevealDepthParam = _effect.Parameters["WindowRevealDepth"];
            _windowBarWidthParam = _effect.Parameters["WindowBarWidth"];
            _windowMarginParam = _effect.Parameters["WindowMargin"];
            _windowLitFractionParam = _effect.Parameters["WindowLitFraction"];
            _windowWarmParam = _effect.Parameters["WindowWarm"];
            _windowCoolParam = _effect.Parameters["WindowCool"];
            _windowHoldSecondsParam = _effect.Parameters["WindowHoldSeconds"];
            _windowHoldVariationParam = _effect.Parameters["WindowHoldVariation"];
            _windowSwitchSecondsParam = _effect.Parameters["WindowSwitchSeconds"];
            _windowRestlessFractionParam = _effect.Parameters["WindowRestlessFraction"];
            _windowBuzzFractionParam = _effect.Parameters["WindowBuzzFraction"];
            _facadeColorParam = _effect.Parameters["FacadeColor"];
            _facadeNeonColorParam = _effect.Parameters["FacadeNeonColor"];
            _facadeColorVariationParam = _effect.Parameters["FacadeColorVariation"];
            _facadeGrainStrengthParam = _effect.Parameters["FacadeGrainStrength"];
            _facadeGrainFrequencyParam = _effect.Parameters["FacadeGrainFrequency"];
            _facadeGrainShadingParam = _effect.Parameters["FacadeGrainShading"];
            _facadeSmoothnessParam = _effect.Parameters["FacadeSmoothness"];
            _facadeHighlightParam = _effect.Parameters["FacadeHighlight"];
            _windowHighlightBoostParam = _effect.Parameters["WindowHighlightBoost"];
            _windowReflectionBoostParam = _effect.Parameters["WindowReflectionBoost"];
            _windowGlassColorParam = _effect.Parameters["WindowGlassColor"];
            _depthTechnique = _effect.Techniques["InstancedDepth"];
            _refractionTechnique = _effect.Techniques["InstancedRefraction"];
            _refractionDepthParam = _effect.Parameters["RefractionDepth"];
            _glassTechnique = _effect.Techniques["InstancedGlass"];
            _polishedMetalTechnique = _effect.Techniques["InstancedPolishedMetal"];
            _crystalTechnique = _effect.Techniques["InstancedCrystal"];
            _drainGlassTechnique = _effect.Techniques["InstancedDrainGlass"];
            _glassBehindParam = _effect.Parameters["GlassBehind"];
            _glassHalfExtentsParam = _effect.Parameters["GlassHalfExtents"];
            _glassCutPeriodParam = _effect.Parameters["GlassCutPeriod"];
            _glassCutSlopeParam = _effect.Parameters["GlassCutSlope"];
            _glassCornerParam = _effect.Parameters["GlassCorner"];
            _glassCrownInsetParam = _effect.Parameters["GlassCrownInset"];
            _glassCrownDropParam = _effect.Parameters["GlassCrownDrop"];
            _glassRimParam = _effect.Parameters["GlassRim"];

            //Cached before the SetLightTint call below, which reads them. The rig used to be looked up by
            //name inside SetLightTint, which was fine while it ran once per dome switch — the Testbed's
            //overcast lerp now calls it for every sky-lit renderer every frame, and six linear name scans
            //over this effect's ~70 parameters times ~9 renderers added up to thousands of string compares
            //a frame.
            _dirLight0DiffuseParam = _effect.Parameters["DirLight0DiffuseColor"];
            _dirLight0SpecularParam = _effect.Parameters["DirLight0SpecularColor"];
            _dirLight1DiffuseParam = _effect.Parameters["DirLight1DiffuseColor"];
            _dirLight1SpecularParam = _effect.Parameters["DirLight1SpecularColor"];
            _dirLight2DiffuseParam = _effect.Parameters["DirLight2DiffuseColor"];
            _dirLight2SpecularParam = _effect.Parameters["DirLight2SpecularColor"];

            _effect.CurrentTechnique = _mainTechnique;

            SetLightTint(Vector3.One, Vector3.One);
        }

        /// <summary>
        /// The technique each <see cref="BallShading"/> is drawn by, <b>in the enum's own order</b>, so
        /// selecting one costs an array index and never a name scan — the by-name indexer is a linear scan over
        /// this effect's techniques and <c>BestPractices.md</c> forbids one per frame. Same shape as
        /// <see cref="_triplanarProbeTechniques"/> above.
        /// </summary>
        private static readonly string[] BallTechniqueNames =
        {
            "InstancedModelPattern",  //BallShading.Vinyl
            "InstancedModelBubble",   //BallShading.Bubble
            "InstancedModelMarble",   //BallShading.Marble
            "InstancedModelWool",     //BallShading.Wool
            "InstancedModelMetal",    //BallShading.Metal
            "InstancedModelIce",      //BallShading.Ice
            "InstancedModelGem",      //BallShading.Gem
            "InstancedModelPlasma",   //BallShading.Plasma
            "InstancedModelLava",     //BallShading.Lava
            "InstancedModelPorcelain",//BallShading.Porcelain
            "InstancedModelStone",    //BallShading.Stone
            "InstancedModelHollow",   //BallShading.Hollow
            "InstancedModelBomb",     //BallShading.Bomb
            "InstancedModelZap",      //BallShading.Zap
            "InstancedModelAcid",     //BallShading.Acid
            "InstancedModelFrozen",   //BallShading.Frozen
            "InstancedModelInfectious", //BallShading.Infectious
            "InstancedModelGravity",  //BallShading.Gravity
            "InstancedModelHeavy",    //BallShading.Heavy
            "InstancedModelWildcard", //BallShading.Wildcard
        };

        /// <summary>
        /// Looks the ball techniques up once and checks the table against the enum, which is the whole point of
        /// doing it here rather than inline: #152 found a ball colour hand-pinned as a count in the render set,
        /// where a member added without repointing it existed in logic and physics and was silently never
        /// drawn. A <see cref="BallShading"/> added without a technique beside it would fail the same way — a
        /// style that selects nothing draws whatever the previous draw left bound — so it throws at load
        /// instead, where the message can say what to do.
        /// </summary>
        private void LoadBallTechniques()
        {
            int count = Enum.GetValues<BallShading>().Length;

            if (BallTechniqueNames.Length != count) throw new InvalidOperationException(
                $"{nameof(BallShading)} has {count} members but {nameof(BallTechniqueNames)} names "
                + $"{BallTechniqueNames.Length} techniques; add the new shading's technique to that table, in "
                + "the enum's order, or it would draw whatever technique the previous draw left bound.");

            _ballTechniques = new EffectTechnique[count];

            for (int i = 0; i < count; i++)
            {
                //The by-name indexer answers null for a technique the effect does not carry, and a null here
                //would surface as a NullReferenceException in the middle of a draw call rather than as the
                //missing technique it is.
                _ballTechniques[i] = _effect.Techniques[BallTechniqueNames[i]]
                    ?? throw new InvalidOperationException(
                        $"InstancedModel.fx declares no technique named \"{BallTechniqueNames[i]}\", "
                        + $"which {nameof(BallShading)}.{(BallShading)i} is drawn by.");
            }
        }

        /// <summary>
        /// Draws the given instances into the currently bound shadow map render target:
        /// depth only, from the light's point of view. One draw call per model mesh part.
        /// <para>
        /// <b>It was written before anything called it and had no caller at all until #470.</b> What it was
        /// waiting for was a map to draw into, which <see cref="SunShadowMap"/> is (#469), and things worth
        /// casting, which the island, the gun and the city are. It restores <c>_mainTechnique</c> on the way
        /// out, so a caller may sit it between ordinary draws.
        /// </para>
        /// <para>
        /// The caller states the target and the states: <see cref="SceneRenderer.DrawShadowMaps"/> binds the
        /// map, clears it and sets opaque/depth-default/CullNone, then hands the matrix to whoever casts.
        /// </para>
        /// </summary>
        public void DrawDepth(Matrix lightViewProjection, ModelInstance[] instances, int instanceCount)
        {
            //Potato draws no shadow map (#789), and its effect has no caster to draw one with
            if (instanceCount <= 0 || _potato) return;

            EnsureInstanceBufferCapacity(instances.Length);
            _instanceBuffer.SetData(instances, 0, instanceCount, SetDataOptions.Discard);

            _lightViewProjectionParam.SetValue(lightViewProjection);
            _effect.CurrentTechnique = _depthTechnique;

            for (int i = 0; i < _parts.Length; i++)
            {
                ref MeshPartData part = ref _parts[i];

                _boneParam.SetValue(part.BoneTransform);

                _graphicsDevice.SetVertexBuffers(
                    new VertexBufferBinding(part.VertexBuffer, part.VertexOffset, 0),
                    new VertexBufferBinding(_instanceBuffer, 0, 1));
                _graphicsDevice.Indices = part.IndexBuffer;

                _effect.CurrentTechnique.Passes[0].Apply();

                _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, part.StartIndex, part.PrimitiveCount, instanceCount);
            }

            _effect.CurrentTechnique = _mainTechnique;
        }

        /// <summary>
        /// This model's mesh parts into <see cref="MotionBlur"/>'s velocity pass (#402), through the motion blur's
        /// own effect and instance stream — this renderer lends only its geometry. The caller has already chosen the
        /// technique, bound the target and uploaded <paramref name="instances"/>; only each part's bone goes out here.
        /// </summary>
        internal void DrawMotion(Effect effect, EffectParameter boneParam, VertexBuffer instances, int instanceCount)
        {
            for (int i = 0; i < _parts.Length; i++)
            {
                ref MeshPartData part = ref _parts[i];

                boneParam.SetValue(part.BoneTransform);

                _graphicsDevice.SetVertexBuffers(
                    new VertexBufferBinding(part.VertexBuffer, part.VertexOffset, 0),
                    new VertexBufferBinding(instances, 0, 1));
                _graphicsDevice.Indices = part.IndexBuffer;

                effect.CurrentTechnique.Passes[0].Apply();

                _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, part.StartIndex, part.PrimitiveCount, instanceCount);
            }
        }

        /// <summary>
        /// Draws one instance into the currently bound refraction target (#426): not its light, but where the nearest
        /// surface of each pixel bends the eye - see <c>InstancedRefraction</c> in InstancedModel.fx. The caller states
        /// the render states (depth test and write on, so only the nearest surface survives).
        /// </summary>
        /// <param name="depth">How far through the glass the eye is carried, in world units: the bending's strength.</param>
        public void DrawRefraction(ICamera camera, Matrix world, float depth)
        {
            //Potato bends nothing (#789): its tier turns the refraction off, and its effect has no technique for it
            if (_potato) return;

            _singleInstance[0] = new ModelInstance(world, new Vector4(0f, 0f, 0f, 1f));
            EnsureInstanceBufferCapacity(1);
            _instanceBuffer.SetData(_singleInstance, 0, 1, SetDataOptions.Discard);

            _viewParam.SetValue(camera.View);
            _projectionParam.SetValue(camera.Projection);
            _refractionDepthParam.SetValue(depth);
            _effect.CurrentTechnique = _refractionTechnique;

            for (int i = 0; i < _parts.Length; i++)
            {
                ref MeshPartData part = ref _parts[i];

                _boneParam.SetValue(part.BoneTransform);

                _graphicsDevice.SetVertexBuffers(
                    new VertexBufferBinding(part.VertexBuffer, part.VertexOffset, 0),
                    new VertexBufferBinding(_instanceBuffer, 0, 1));
                _graphicsDevice.Indices = part.IndexBuffer;

                _effect.CurrentTechnique.Passes[0].Apply();

                _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, part.StartIndex, part.PrimitiveCount, 1);
            }

            _effect.CurrentTechnique = _mainTechnique;
        }

        /// <summary>
        /// Whether the caller renders into a linear HDR target and tonemaps at the end of the frame. It
        /// decides whether the light rig is decoded out of its sRGB authoring before the tints are applied.
        /// Every current executable renders linear and sets this true (the map editor moved to the full
        /// linear pipeline too); the false default only exists so an unconverted future caller drawing
        /// straight to an 8-bit back buffer in gamma space keeps the legacy look.
        /// </summary>
        public bool LinearLightRig { get; set; }

        /// <summary>
        /// Tints the default three-light rig, e.g. by the sky dome palette: the key and fill lights
        /// (the "sun" side) by <paramref name="keyTint"/>, the back light by <paramref name="backTint"/>.
        /// White tints reproduce the untinted <see cref="BasicEffect"/> default lighting.
        /// </summary>
        /// <param name="keyTint">Tint of the key and fill lights, in the space <see cref="LinearLightRig"/> selects.</param>
        /// <param name="backTint">Tint of the back light, in that same space.</param>
        /// <remarks>
        /// The rig is decoded here rather than in the shader because a tint is a multiplication, and
        /// multiplying two sRGB values does not multiply the light they stand for. Doing it on this side
        /// also keeps the conversion off the per-pixel path — note the Testbed now calls this per frame
        /// for every sky-lit renderer (the overcast lerp), which is why the six parameters are cached.
        /// </remarks>
        public void SetLightTint(Vector3 keyTint, Vector3 backTint)
        {
            Vector3 Rig(Vector3 color) => LinearLightRig ? ColorSpace.SrgbToLinear(color) : color;

            _dirLight0DiffuseParam.SetValue(Rig(DefaultLighting.Light0Diffuse) * keyTint);
            _dirLight0SpecularParam.SetValue(Rig(DefaultLighting.Light0Specular) * keyTint);
            _dirLight1DiffuseParam.SetValue(Rig(DefaultLighting.Light1Diffuse) * keyTint);
            _dirLight1SpecularParam.SetValue(Rig(DefaultLighting.Light1Specular) * keyTint);
            _dirLight2DiffuseParam.SetValue(Rig(DefaultLighting.Light2Diffuse) * backTint);
            _dirLight2SpecularParam.SetValue(Rig(DefaultLighting.Light2Specular) * backTint);
        }

        /// <summary>
        /// Draws the given instances of the model in one draw call per model mesh part.
        /// </summary>
        /// <param name="camera">A camera that looks at the resulting rendering.</param>
        /// <param name="instances">Per-instance data (world matrix + custom vector). Only the first <paramref name="instanceCount"/> entries are drawn.</param>
        /// <param name="instanceCount">Number of instances to draw.</param>
        /// <param name="effectParams">Lighting parameters shared by all the instances
        /// (<see cref="BasicEffectParams.AmbientLightColor"/>, specular and emissive colors are applied;
        /// zero vectors fall back to the <see cref="BasicEffect"/> defaults, like in <see cref="ModelRenderer"/>).</param>
        /// <param name="diffuseTint">Optional recolor of the model (e.g. the ball type color): the material
        /// diffuse colors are reduced to their luminance (keeping the patch pattern as shades) and multiplied
        /// by this tint, so the whole instance reads as one color. Null keeps the material colors unchanged.</param>
        public void Draw(ICamera camera, ModelInstance[] instances, int instanceCount, BasicEffectParams effectParams, Vector3? diffuseTint = null)
        {
            if (instanceCount <= 0) return;

            if (_potato)
            {
                DrawPotato(camera, instances, instanceCount, effectParams, diffuseTint);
                return;
            }

            EnsureInstanceBufferCapacity(instances.Length);
            _instanceBuffer.SetData(instances, 0, instanceCount, SetDataOptions.Discard);

            _viewParam.SetValue(camera.View);
            _projectionParam.SetValue(camera.Projection);
            _eyePositionParam.SetValue(camera.Position);

            Vector3 ambientLightColor = DefaultLighting.AmbientLightColor;
            bool overrideSpecular = false;
            Vector3 specularColor = DEFAULT_SPECULAR_COLOR;
            float specularPower = DEFAULT_SPECULAR_POWER;
            Vector3 emissiveColor = Vector3.Zero;

            if (effectParams != null)
            {
                if (effectParams.AmbientLightColor != Vector3.Zero) ambientLightColor = effectParams.AmbientLightColor;
                if (effectParams.SpecularColor != Vector3.Zero)
                {
                    overrideSpecular = true;
                    specularColor = effectParams.SpecularColor;
                    specularPower = effectParams.SpecularPower;
                }
                if (effectParams.EmissiveColor != Vector3.Zero) emissiveColor = effectParams.EmissiveColor;
            }

            _skyColorParam.SetValue(SkyColor);
            _groundColorParam.SetValue(GroundColor);
            _keyLightPositionParam.SetValue(KeyLightPosition);
            _groundHeightParam.SetValue(GroundHeight);

            //Set unconditionally rather than inside a technique branch: several techniques read these and
            //the effect is shared between every renderer, so a value left behind by the previous Draw
            //would show up as relief on a model that asked for none
            _surfaceReliefStrengthParam.SetValue(SurfaceReliefStrength);
            _surfaceReliefFrequencyParam.SetValue(SurfaceReliefFrequency);
            _slabSizeParam.SetValue(SlabSize);
            _slabJointWidthParam.SetValue(SlabJointWidth);
            _slabJointDepthParam.SetValue(SlabJointDepth);
            _jointGlowParam.SetValue(JointGlow);
            _jointGlowPatchinessParam.SetValue(JointGlowPatchiness);
            _topDustTintParam.SetValue(TopDustTint);
            _topDustStrengthParam.SetValue(TopDustStrength);
            _slabWarpParam.SetValue(SlabWarp);
            _sideDustTintParam.SetValue(SideDustTint);
            _sideDustStrengthParam.SetValue(SideDustStrength);
            _bandTopYParam.SetValue(BandTopY);
            _bandFadeParam.SetValue(BandFade);
            _bandTintParam.SetValue(BandTint);
            _bandWetParam.SetValue(BandWet);
            _bandStrengthParam.SetValue(BandStrength);
            _strataSpacingParam.SetValue(StrataSpacing);
            _strataStrengthParam.SetValue(StrataStrength);
            _topDustClearParam.SetValue(TopDustClear);
            _cavityStrengthParam.SetValue(CavityStrength);
            _specularAmbientStrengthParam.SetValue(SpecularAmbientStrength);
            _metalnessParam.SetValue(Metalness);

            //Unconditionally, for Metalness's reason: the two renderers that turn this on are the drain's
            //glass and its pit, and a 1 left standing would flip the back-face normals of whatever open
            //surface draws next
            _twoSidedNormalsParam.SetValue(TwoSidedNormals);

            //Unconditionally, for Metalness's own reason: the one renderer that turns this off is the crystal
            //cup, and a zero left standing on the shared effect would strip every following surface's
            //reflection of the alpha it is supposed to be scaled by
            _specularAlphaWeightParam.SetValue(SpecularAlphaWeight);

            //Unconditionally, for Metalness's reason: a glow left over from the renderer drawn before this one
            //would set the next surface alight
            _emissiveTintParam.SetValue(EmissiveTint);

            //Unconditionally, for Metalness's reason again: the one renderer that dims this is the ceiling
            //glass, and its figure left standing would put the rest of the scene under the same dark sky
            _dirLightStrengthParam.SetValue(DirLightStrength);

            for (int i = 0; i < _parts.Length; i++)
            {
                ref MeshPartData part = ref _parts[i];

                _boneParam.SetValue(part.BoneTransform);

                Vector3 diffuse = new(part.DiffuseColor.X, part.DiffuseColor.Y, part.DiffuseColor.Z);

                //With the beach-ball pattern the tint colors the pattern instead of the material:
                //the material diffuse stays the neutral shade multiplying both pattern colors
                bool usePattern = PatternGoreCount > 0 && part.DiffuseColor.W >= 1f;

                if (diffuseTint.HasValue && !usePattern)
                {
                    //Luminance (Rec. 601) preserves the patch pattern as shades; the boost compensates
                    //for the brightest material being 0.8 instead of pure white
                    float luminance = diffuse.X * 0.299f + diffuse.Y * 0.587f + diffuse.Z * 0.114f;
                    diffuse = diffuseTint.Value * (luminance * 1.25f);
                }

                //BasicEffect premultiplies the non-specular terms by alpha on the CPU; do the same
                //so translucent mesh parts blend identically under BlendState.AlphaBlend
                float alpha = part.DiffuseColor.W;
                _diffuseColorParam.SetValue(new Vector4(diffuse * alpha, alpha));

                //The ambient tint is premultiplied by the material diffuse (like BasicEffect does on the CPU side);
                //the shader modulates it per pixel by the sky/ground hemisphere colors
                _ambientColorParam.SetValue(ambientLightColor * diffuse * alpha);
                _emissiveColorParam.SetValue((part.EmissiveColor + emissiveColor) * alpha);
                _specularColorParam.SetValue(overrideSpecular ? specularColor : part.SpecularColor);
                _specularPowerParam.SetValue(overrideSpecular ? specularPower : part.SpecularPower);

                SelectTechniqueAndParameters(in part, usePattern, diffuseTint);

                _graphicsDevice.SetVertexBuffers(
                    new VertexBufferBinding(part.VertexBuffer, part.VertexOffset, 0),
                    new VertexBufferBinding(_instanceBuffer, 0, 1));
                _graphicsDevice.Indices = part.IndexBuffer;

                _effect.CurrentTechnique.Passes[0].Apply();

                _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, part.StartIndex, part.PrimitiveCount, instanceCount);
            }

            _effect.CurrentTechnique = _mainTechnique;
        }

        /// <summary>
        /// Picks the technique for one mesh part and sets the parameters that technique reads. The branches
        /// are mutually exclusive: a city facade, a ball shading, a triplanar detail-textured part, the
        /// refracting glass, or the plain lit material (polished metal where <see cref="PolishedMetal"/> asks).
        /// </summary>
        private void SelectTechniqueAndParameters(in MeshPartData part, bool usePattern, Vector3? diffuseTint)
        {
            if (CityWindowBrightness > 0f)
            {
                //A city building: no texture, no UVs, its facade drawn from world position
                _effect.CurrentTechnique = _cityTechnique;
                _cityWindowBrightnessParam.SetValue(CityWindowBrightness);
                _cityWindowTimeParam.SetValue(CityWindowTime);
                _cityNeonParam.SetValue(CityNeon);

                var city = CityConfig;
                _windowPitchXParam.SetValue(city.WindowPitchX);
                _windowPitchYParam.SetValue(city.WindowPitchY);
                _windowFillXParam.SetValue(city.WindowFillX);
                _windowFillYParam.SetValue(city.WindowFillY);
                _windowFrameWidthParam.SetValue(city.WindowFrameWidth);
                _windowFrameHeightParam.SetValue(city.WindowFrameHeight);
                _windowFrameShadingParam.SetValue(city.WindowFrameShading);
                _windowFrameToneParam.SetValue(city.WindowFrameTone);
                _windowSillHeightParam.SetValue(city.WindowSillHeight);
                _windowSillOverhangParam.SetValue(city.WindowSillOverhang);
                _windowSillShadowParam.SetValue(city.WindowSillShadow);
                _windowSillShadingParam.SetValue(city.WindowSillShading);
                _windowRevealDepthParam.SetValue(city.WindowRevealDepth);
                _windowBarWidthParam.SetValue(city.WindowBarWidth);
                _windowMarginParam.SetValue(city.WindowMargin);
                _windowLitFractionParam.SetValue(city.WindowLitFraction);
                _windowWarmParam.SetValue(city.WindowWarm.ToVector3());
                _windowCoolParam.SetValue(city.WindowCool.ToVector3());
                _windowHoldSecondsParam.SetValue(city.WindowHoldSeconds);
                _windowHoldVariationParam.SetValue(city.WindowHoldVariation);
                _windowSwitchSecondsParam.SetValue(city.WindowSwitchSeconds);
                _windowRestlessFractionParam.SetValue(city.WindowRestlessFraction);
                _windowBuzzFractionParam.SetValue(city.NeonLook.WindowBuzzFraction);
                _facadeColorParam.SetValue(city.FacadeColor.ToVector3());
                _facadeNeonColorParam.SetValue(city.FacadeNeonColor.ToVector3());
                _facadeColorVariationParam.SetValue(city.FacadeColorVariation);
                _facadeGrainStrengthParam.SetValue(city.FacadeGrainStrength);
                _facadeGrainFrequencyParam.SetValue(city.FacadeGrainFrequency);
                _facadeGrainShadingParam.SetValue(city.FacadeGrainShading);
                _facadeSmoothnessParam.SetValue(city.FacadeSmoothness);
                _facadeHighlightParam.SetValue(city.FacadeHighlight);
                _windowHighlightBoostParam.SetValue(city.WindowHighlightBoost);
                _windowReflectionBoostParam.SetValue(city.WindowReflectionBoost);
                _windowGlassColorParam.SetValue(city.WindowGlassColor.ToVector3());
            }
            else if (usePattern)
            {
                //What the ball is made of (#258, #304). Every shading shares everything a BALL is — its colour,
                //its heartbeat, its ripple, its dissolve — and they differ in what the surface does with light,
                //which is why they are separate techniques indexed here rather than one shader with a mode in
                //it: a runtime branch over an alternative shading model costs the union of both register
                //allocations in every wavefront, and these passes are occupancy-bound.
                _effect.CurrentTechnique = _ballTechniques[(int)Shading];
                _patternPrimaryColorParam.SetValue(diffuseTint ?? Vector3.One);

                //One case per shading, each setting what ITS technique reads and nothing else — a uniform the
                //bound program never declares is a wasted SetValue, and one it does declare but the case
                //forgot is whatever the last renderer left there. The shared tail below is the other half of
                //that split: what every ball technique reads whatever it is made of.
                switch (Shading)
                {
                    case BallShading.Bubble:
                        _bubbleShellParam.SetValue(BubbleShell);
                        _bubbleFilmThicknessParam.SetValue(BubbleFilmThickness);
                        _bubbleTintStrengthParam.SetValue(BubbleTintStrength);
                        _bubbleBodyOpacityParam.SetValue(BubbleBodyOpacity);
                        break;

                    case BallShading.Hollow:
                        //The shell sign and the body weight are the film's own uniforms, shared rather than
                        //duplicated: which wall is being drawn is a fact about the two-pass draw and not about
                        //what the glass is made of, and both transparent shadings are put out by the same pair
                        //of passes. The seam is this shading's alone.
                        _bubbleShellParam.SetValue(BubbleShell);
                        _bubbleBodyOpacityParam.SetValue(BubbleBodyOpacity);
                        _hollowSeamFrequencyParam.SetValue(HollowSeamFrequency);
                        break;

                    case BallShading.Porcelain:
                        _porcelainCrackFrequencyParam.SetValue(PorcelainCrackFrequency);
                        _porcelainCrackWidthParam.SetValue(PorcelainCrackWidth);
                        _porcelainGlazeParam.SetValue(PorcelainGlaze);
                        _translucencyStrengthParam.SetValue(TranslucencyStrength);
                        break;

                    case BallShading.Stone:
                        _stoneGrainFrequencyParam.SetValue(StoneGrainFrequency);
                        _stoneGrainContrastParam.SetValue(StoneGrainContrast);
                        _stoneRoughnessParam.SetValue(StoneRoughness);
                        _stoneShapeDepthParam.SetValue(StoneShapeDepth);
                        break;

                    case BallShading.Lava:
                        _lavaSeamFrequencyParam.SetValue(LavaSeamFrequency);
                        _lavaSeamWidthParam.SetValue(LavaSeamWidth);
                        _lavaGlowParam.SetValue(LavaGlow);
                        break;

                    case BallShading.Plasma:
                        _plasmaWarpParam.SetValue(PlasmaWarp);
                        _plasmaGlowParam.SetValue(PlasmaGlow);
                        _plasmaSpeedParam.SetValue(PlasmaSpeed);
                        break;

                    case BallShading.Gem:
                        _gemFacetCountParam.SetValue(GemFacetCount);
                        _gemFacetDepthParam.SetValue(GemFacetDepth);
                        _gemAbsorptionParam.SetValue(GemAbsorption);
                        break;

                    case BallShading.Ice:
                        _iceCrackFrequencyParam.SetValue(IceCrackFrequency);
                        _iceCrackWidthParam.SetValue(IceCrackWidth);
                        _iceRimParam.SetValue(IceRim);
                        _icePlateContrastParam.SetValue(IcePlateContrast);
                        _translucencyStrengthParam.SetValue(TranslucencyStrength);
                        break;

                    case BallShading.Metal:
                        _metalBrushFrequencyParam.SetValue(MetalBrushFrequency);
                        _metalBrushDepthParam.SetValue(MetalBrushDepth);
                        _metalReflectanceParam.SetValue(MetalReflectance);
                        break;

                    case BallShading.Wool:
                        _woolStrandFrequencyParam.SetValue(WoolStrandFrequency);
                        _woolStrandDepthParam.SetValue(WoolStrandDepth);
                        _woolHaloParam.SetValue(WoolHalo);
                        break;

                    case BallShading.Marble:
                        _marbleVeinFrequencyParam.SetValue(MarbleVeinFrequency);
                        _marbleVeinWarpParam.SetValue(MarbleVeinWarp);
                        _marbleVeinContrastParam.SetValue(MarbleVeinContrast);
                        break;

                    case BallShading.Wildcard:
                        _patternSecondaryColorParam.SetValue(PatternSecondaryColor);
                        _wildcardProgressParam.SetValue(WildcardProgress);
                        break;

                    case BallShading.Vinyl:
                        _patternSecondaryColorParam.SetValue(PatternSecondaryColor);
                        _patternGoreCountParam.SetValue((float)PatternGoreCount);

                        //Thresholding sin(azimuth) at -cos(pi * width) hands the primary gore exactly that
                        //fraction of each pair of segments; the even split lands on zero, as before
                        _patternGoreThresholdParam.SetValue(-MathF.Cos(MathF.PI * PatternGoreWidth));
                        _patternCapExtentParam.SetValue(PatternCapExtent);
                        _patternReliefStrengthParam.SetValue(PatternReliefStrength);
                        _translucencyStrengthParam.SetValue(TranslucencyStrength);
                        break;
                }

                _emissiveStrengthParam.SetValue(EmissiveStrength);
                _pulseTimeParam.SetValue(PulseTime);
                _dissolvePixelSizeParam.SetValue(DissolvePixelSize);
                _pulseSpeedParam.SetValue(PulseSpeed);
                _pulseDepthParam.SetValue(PulseDepth);
                _stillEmissionParam.SetValue(StillEmission);
                _pulseDirectionParam.SetValue(PulseDirection);
                _pulseWavelengthParam.SetValue(PulseWavelength);

                //Unconditionally, like Metalness and SpecularAmbientStrength: a value left over from the
                //renderer drawn before this one would make the next surface flare on somebody else's wave —
                //and, since the colour joined it, in somebody else's colour
                _rippleStrengthParam.SetValue(RippleStrength);
                _rippleAlarmColorParam.SetValue(RippleAlarmColor);
            }
            else if (DetailTexture != null && part.DiffuseColor.W >= 1f)
            {
                _effect.CurrentTechnique = TriplanarProbe > 0 ? _triplanarProbeTechniques[TriplanarProbe - 1]
                    : CoarseSurfaceRelief ? _triplanarCoarseTechnique
                    : _triplanarTechnique;

                _textureParam.SetValue(DetailTexture);
                _detailScaleParam.SetValue(DetailScale);
                _detailStrengthParam.SetValue(DetailStrength);
                _detailBoostParam.SetValue(DetailBoost);
            }
            else if (GlassBehind != null)
            {
                //The ceiling's glass bending the frame behind it (#541)
                _effect.CurrentTechnique = _glassTechnique;
                _glassBehindParam.SetValue(GlassBehind);
                _glassHalfExtentsParam.SetValue(GlassHalfExtents);
                _glassCutPeriodParam.SetValue(GlassCutPeriod);
                _glassCutSlopeParam.SetValue(GlassCutSlope);
                _glassCornerParam.SetValue(new Vector2(GlassCornerRadius, MathHelper.PiOver2 / (GlassCornerFacets + 1)));
                _glassCrownInsetParam.SetValue(GlassCrownInset);
                _glassCrownDropParam.SetValue(GlassCrownDrop);
                _glassRimParam.SetValue(GlassRim);
            }
            else
            {
                _effect.CurrentTechnique = Crystal ? _crystalTechnique : DrainGlass ? _drainGlassTechnique
                    : PolishedMetal ? _polishedMetalTechnique : _mainTechnique;
            }
        }

        /// <summary>
        /// Draws a single instance of the model at the given world matrix, with no ambient occlusion.
        /// Meant for unique scene objects (cannon, backdrops, ground), so they receive the same lighting
        /// (hemisphere sky ambient, positional key light, per-pixel shading) as the instanced balls.
        /// </summary>
        public void Draw(ICamera camera, Matrix world, BasicEffectParams effectParams)
        {
            _singleInstance[0] = new ModelInstance(world, new Vector4(0f, 0f, 0f, 1f));
            Draw(camera, _singleInstance, 1, effectParams);
        }

        /// <summary>
        /// One instance into the bound shadow map (#470) — <see cref="DrawDepth(Matrix, ModelInstance[], int)"/>
        /// for a prop that is drawn from a single world matrix, which is most of what casts: the island's cap
        /// and drum, the gun's barrel and carriage.
        /// </summary>
        public void DrawDepth(Matrix shadowViewProjection, Matrix world)
        {
            _singleInstance[0] = new ModelInstance(world, new Vector4(0f, 0f, 0f, 1f));
            DrawDepth(shadowViewProjection, _singleInstance, 1);
        }

        /// <summary>
        /// The Potato effect's uniforms (#789): the lighting model's, the island's texture and every ball's colour,
        /// heartbeat, ripple and dissolve - the subset of InstancedModel.fx's that PotatoModel.fx keeps, cached into the
        /// same fields so <see cref="SetLightTint"/> serves both. Each must exist: a uniform the shader compiler found
        /// unused is stripped and comes back null, and that is said here, at load, rather than as a null reference in
        /// the middle of a frame.
        /// </summary>
        private void InitializePotatoEffect()
        {
            EffectParameter Required(string name) => _effect.Parameters[name] ?? throw new InvalidOperationException(
                $"PotatoModel.fx has no uniform \"{name}\", or the compiler stripped it as unused; InstancedModelRenderer's Potato path sets it.");

            EffectTechnique RequiredTechnique(string name) => _effect.Techniques[name] ?? throw new InvalidOperationException(
                $"PotatoModel.fx has no technique \"{name}\", which InstancedModelRenderer's Potato path draws with.");

            _viewParam = Required("View");
            _projectionParam = Required("Projection");
            _eyePositionParam = Required("EyePosition");
            _diffuseColorParam = Required("DiffuseColor");
            _emissiveColorParam = Required("EmissiveColor");
            _specularColorParam = Required("SpecularColor");
            _specularPowerParam = Required("SpecularPower");
            _potatoAmbientMidParam = Required("AmbientMid");
            _potatoAmbientTiltParam = Required("AmbientTilt");
            _potatoEnvironmentMidParam = Required("EnvironmentMid");
            _potatoEnvironmentTiltParam = Required("EnvironmentTilt");
            _potatoReflectanceParam = Required("Reflectance");
            _potatoReflectanceRiseParam = Required("ReflectanceRise");
            _keyLightPositionParam = Required("KeyLightPosition");
            _groundHeightParam = Required("GroundHeight");
            _twoSidedNormalsParam = Required("TwoSidedNormals");
            _specularAlphaWeightParam = Required("SpecularAlphaWeight");
            _dirLightStrengthParam = Required("DirLightStrength");
            _emissiveTintParam = Required("EmissiveTint");

            _textureParam = Required("Texture");
            _detailScaleParam = Required("DetailScale");
            _detailStrengthParam = Required("DetailStrength");
            _detailBoostParam = Required("DetailBoost");

            _potatoBallCrustParam = Required("BallCrust");
            _potatoBallEmissionStillParam = Required("BallEmissionStill");
            _potatoBallEmissionBeatParam = Required("BallEmissionBeat");
            _potatoBallGlowStillParam = Required("BallGlowStill");
            _potatoBallGlowBeatParam = Required("BallGlowBeat");
            _potatoBallFlashParam = Required("BallFlash");
            _potatoBallAlarmParam = Required("BallAlarm");
            _potatoPulsePhaseParam = Required("PulsePhase");

            Required("DirLight1Direction").SetValue(DefaultLighting.Light1Direction);
            Required("DirLight2Direction").SetValue(DefaultLighting.Light2Direction);
            _dirLight0DiffuseParam = Required("DirLight0DiffuseColor");
            _dirLight0SpecularParam = Required("DirLight0SpecularColor");
            _dirLight1DiffuseParam = Required("DirLight1DiffuseColor");
            _dirLight1SpecularParam = Required("DirLight1SpecularColor");
            _dirLight2DiffuseParam = Required("DirLight2DiffuseColor");
            _dirLight2SpecularParam = Required("DirLight2SpecularColor");

            _mainTechnique = RequiredTechnique("PotatoLit");
            _potatoTexturedTechnique = RequiredTechnique("PotatoTextured");
            _potatoBallTechnique = RequiredTechnique("PotatoBall");
            _potatoBallDitherTechnique = RequiredTechnique("PotatoBallDither");
            _dissolvePixelSizeParam = Required("DissolvePixelSize");
            _potatoFinLitTechnique = RequiredTechnique("PotatoFinLit");
            _potatoFinTexturedTechnique = RequiredTechnique("PotatoFinTextured");
            _finShapeParam = Required("FinShape");
            _potatoBandTechnique = RequiredTechnique("PotatoBand");
            _potatoBandSeamParam = Required("BandSeam");
            _potatoBallRimTechnique = RequiredTechnique("PotatoBallRim");
            _potatoBallFlatTechnique = RequiredTechnique("PotatoBallFlat");
            _rimShapeParam = Required("RimShape");
            _rimSecantParam = Required("RimSecant");
            _effect.CurrentTechnique = _mainTechnique;

            SetLightTint(Vector3.One, Vector3.One);
        }

        /// <summary>
        /// The one colour a Potato ball is (#789), its desktop technique's look being out of reach: the tint it is drawn
        /// with - flowing into the next colour across a wildcard's crossing, as the desktop's wildcard does, rather than
        /// holding the colour it is leaving while the aim beam already shows the next (#789's review) - and, for the kinds
        /// the desktop draws with no tint at all because their technique supplies the colour (the rock, the bomb, the zap
        /// and the Cut round drawn as one, the acid, the hollow glass), a stand-in of their own, so they are not every one
        /// a white ball glowing at its own pulse. Authored sRGB, as a tint is.
        /// </summary>
        private Vector3 PotatoBallColor(Vector3? tint)
        {
            if (tint.HasValue)
                return Shading == BallShading.Wildcard
                    ? Vector3.Lerp(tint.Value, PatternSecondaryColor, MathHelper.Clamp(WildcardProgress, 0f, 1f))
                    : tint.Value;

            return Shading switch
            {
                BallShading.Stone => new Vector3(0.55f, 0.53f, 0.50f),
                BallShading.Bomb => new Vector3(0.16f, 0.16f, 0.18f),
                BallShading.Zap => new Vector3(0.22f, 0.25f, 0.42f),
                BallShading.Acid => new Vector3(0.45f, 0.85f, 0.15f),
                BallShading.Hollow => new Vector3(0.80f, 0.86f, 0.92f),
                _ => new Vector3(0.85f, 0.85f, 0.85f),
            };
        }

        /// <summary>
        /// Whether a ball's <see cref="ModelInstance.Dissolve"/> asks for the dither (#794): anything but a settled ball
        /// (zero) and a ghost (a size, not a cut). The one test that decides which Potato draw a ball goes in.
        /// </summary>
        internal static bool NeedsDither(float dissolve) => dissolve != 0f && !ModelInstance.IsGhost(dissolve);

        /// <summary>
        /// <paramref name="source"/>'s first <paramref name="count"/> arranged for the Potato draw (#789, #794) into
        /// <paramref name="ordered"/>: first the balls that need no dither, <b>nearest to <paramref name="eye"/> first</b>,
        /// then the dithered ones in the order they came. Returns how many of the first kind there are, which is where
        /// the second kind begins. A pure function of its arguments so the arrangement can be checked without a device;
        /// the two buffers are the caller's, grown here when <paramref name="count"/> outruns them (no allocation on a
        /// frame that does not).
        /// <para>
        /// Nearest first because a cluster is mostly balls behind other balls, and drawn near to far the GPU rejects their
        /// hidden pixels by depth before shading them (the desktop draws in the order they come, its pixels being cheap
        /// enough; on the Pi the balls were two thirds of a heavy level's frame). The dithered ones last and apart,
        /// because a pixel shader with a <c>clip()</c> forfeits exactly that rejection for every draw that holds one:
        /// the few balls that need it pay for it in a draw of their own, and the rest keep the gain.
        /// </para>
        /// </summary>
        internal static int OrderForPotato(ModelInstance[] source, int count, Vector3 eye,
            ref ModelInstance[] ordered, ref float[] depths)
        {
            if (ordered.Length < count)
            {
                ordered = new ModelInstance[source.Length];
                depths = new float[source.Length];
            }

            int clean = 0;
            for (int i = 0; i < count; i++)
                if (!NeedsDither(source[i].Dissolve)) clean++;

            int cleanAt = 0, ditheredAt = clean;
            for (int i = 0; i < count; i++)
            {
                if (NeedsDither(source[i].Dissolve))
                {
                    ordered[ditheredAt++] = source[i];
                    continue;
                }

                ordered[cleanAt] = source[i];
                depths[cleanAt] = Vector3.DistanceSquared(eye, source[i].World.Translation);
                cleanAt++;
            }

            if (clean > 1) Array.Sort(depths, ordered, 0, clean);

            return clean;
        }

        private ModelInstance[] _sortedInstances = Array.Empty<ModelInstance>();
        private float[] _sortDepths = Array.Empty<float>();

        /// <summary>
        /// <see cref="Draw(ICamera, ModelInstance[], int, BasicEffectParams, Vector3?)"/> through the Potato effect (#789):
        /// the same instance upload, the same material arithmetic (the tint's luminance, BasicEffect's premultiply) and
        /// the same per-draw restatement of every shared uniform, with four techniques in place of InstancedModel.fx's
        /// thirty-five - a ball of any <see cref="BallShading"/> is <c>PotatoBall</c> (and, for the few being dithered,
        /// <c>PotatoBallDither</c> in a run of its own, #794), a detail-textured part is <c>PotatoTextured</c>,
        /// everything else (city, glass, metal, crystal) is <c>PotatoLit</c>. Its own method
        /// rather than branches through the desktop draw, so the desktop path reads exactly as it did.
        /// </summary>
        private void DrawPotato(ICamera camera, ModelInstance[] instances, int instanceCount, BasicEffectParams effectParams, Vector3? diffuseTint)
        {
            //The rims' pass (#804) draws a ring for every ball of a bucket in place of the bucket: only a ball renderer
            //that was given the strip has any
            bool rims = RimPass;
            if (rims && (RimMesh == null || PatternGoreCount <= 0)) return;

            EnsureInstanceBufferCapacity(instances.Length);

            //Balls nearest first, and the dithered ones apart at the end (#789, #794): see OrderForPotato. Arranged
            //into this renderer's own buffer, so the caller's array keeps its order. Everything that is not a ball
            //renderer's is one run, in the order it came - and so are the rims, whose shader drops the ring of a ball
            //being dithered and whose blend, depth-tested against every ball already drawn, needs no order.
            int clean = instanceCount;
            ModelInstance[] ordered = instances;

            if (PatternGoreCount > 0 && !rims)
            {
                clean = OrderForPotato(instances, instanceCount, camera.Position, ref _sortedInstances, ref _sortDepths);
                ordered = _sortedInstances;
            }

            _instanceBuffer.SetData(ordered, 0, instanceCount, SetDataOptions.Discard);

            _viewParam.SetValue(camera.View);
            _projectionParam.SetValue(camera.Projection);
            _eyePositionParam.SetValue(camera.Position);

            Vector3 ambientLightColor = DefaultLighting.AmbientLightColor;
            bool overrideSpecular = false;
            Vector3 specularColor = DEFAULT_SPECULAR_COLOR;
            float specularPower = DEFAULT_SPECULAR_POWER;
            Vector3 emissiveColor = Vector3.Zero;

            if (effectParams != null)
            {
                if (effectParams.AmbientLightColor != Vector3.Zero) ambientLightColor = effectParams.AmbientLightColor;
                if (effectParams.SpecularColor != Vector3.Zero)
                {
                    overrideSpecular = true;
                    specularColor = effectParams.SpecularColor;
                    specularPower = effectParams.SpecularPower;
                }
                if (effectParams.EmissiveColor != Vector3.Zero) emissiveColor = effectParams.EmissiveColor;
            }

            //Every shared uniform, every draw, for the desktop draw's reason: the effect is one object under every
            //renderer, and a value one renderer left standing would show on the next
            _keyLightPositionParam.SetValue(KeyLightPosition);
            _groundHeightParam.SetValue(GroundHeight);
            _twoSidedNormalsParam.SetValue(TwoSidedNormals);
            _specularAlphaWeightParam.SetValue(SpecularAlphaWeight);
            _emissiveTintParam.SetValue(EmissiveTint);
            _dirLightStrengthParam.SetValue(DirLightStrength);

            //The dome as its middle and half its range: what lerp(Ground, Sky, y / 2 + 1 / 2) is, as a + b * y
            Vector3 domeMid = (SkyColor + GroundColor) * 0.5f;
            Vector3 domeTilt = (SkyColor - GroundColor) * 0.5f;

            for (int i = 0; i < _parts.Length; i++)
            {
                ref MeshPartData part = ref _parts[i];

                Vector3 diffuse = new(part.DiffuseColor.X, part.DiffuseColor.Y, part.DiffuseColor.Z);
                bool ball = PatternGoreCount > 0 && part.DiffuseColor.W >= 1f;

                if (diffuseTint.HasValue && !ball)
                {
                    float luminance = diffuse.X * 0.299f + diffuse.Y * 0.587f + diffuse.Z * 0.114f;
                    diffuse = diffuseTint.Value * (luminance * 1.25f);
                }

                //The material, decoded and multiplied out HERE (#804): what the pixel shader did with these for every
                //pixel, done once a draw, in its order and with its constants - see PotatoModel.fx's uniform block
                float alpha = part.DiffuseColor.W;
                Vector3 ambient = PotatoSrgbToLinear(ambientLightColor * diffuse * alpha);
                Vector3 specular = PotatoSrgbToLinear(overrideSpecular ? specularColor : part.SpecularColor);
                float power = overrideSpecular ? specularPower : part.SpecularPower;

                _diffuseColorParam.SetValue(new Vector4(PotatoSrgbToLinear(diffuse * alpha), alpha));
                _emissiveColorParam.SetValue(PotatoSrgbToLinear((part.EmissiveColor + emissiveColor) * alpha));
                _specularColorParam.SetValue(specular);
                _specularPowerParam.SetValue(power);

                _potatoAmbientMidParam.SetValue(domeMid * ambient);
                _potatoAmbientTiltParam.SetValue(domeTilt * ambient);

                //A rough surface reflects the dome's average, a sharp one the dome at its mirror direction
                float roughness = MathHelper.Clamp(MathF.Sqrt(2f / (power + 2f)), 0f, 1f);
                _potatoEnvironmentMidParam.SetValue(domeMid);
                _potatoEnvironmentTiltParam.SetValue(domeTilt * (1f - roughness));

                Vector3 reflectance = Vector3.Lerp(0.04f * specular, specular, Metalness);
                _potatoReflectanceParam.SetValue(reflectance * SpecularAmbientStrength);
                _potatoReflectanceRiseParam.SetValue((Vector3.Max(Vector3.One, reflectance) - reflectance) * SpecularAmbientStrength);

                if (ball)
                {
                    _effect.CurrentTechnique = PotatoFlatBalls ? _potatoBallFlatTechnique : _potatoBallTechnique;
                    SetPotatoBallUniforms(PotatoSrgbToLinear(PotatoBallColor(diffuseTint)));
                }
                else if (DetailTexture != null && alpha >= 1f)
                {
                    _effect.CurrentTechnique = _potatoTexturedTechnique;
                    _textureParam.SetValue(DetailTexture);
                    _detailScaleParam.SetValue(DetailScale);
                    _detailStrengthParam.SetValue(DetailStrength);
                    _detailBoostParam.SetValue(DetailBoost);
                }
                else if (PotatoBand)
                {
                    //The seams' height, and whether the runs that go under the pit have it under them (#804)
                    _effect.CurrentTechnique = _potatoBandTechnique;
                    _potatoBandSeamParam.SetValue(new Vector2(FunnelRimsMesh.SEAM_HEIGHT, PotatoBandUnderPit ? 1f : 0f));
                }
                else _effect.CurrentTechnique = _mainTechnique;

                //A ball part is two runs of the one instance buffer: the balls with no dither (nearest first), then the
                //dithered ones through the technique that has the clip. Anything else is one run. In the rims' pass a ball
                //part is its rings, under the very uniforms just set for it, and anything else is nothing.
                if (rims)
                {
                    if (ball) DrawPotatoRims(camera, instanceCount);
                }
                else if (!ball)
                {
                    DrawPotatoRun(part, 0, instanceCount);

                    //And its outline's fins (#804), under the uniforms just set and by the pixel shader just used
                    if (_fins != null && FinRampPixels > 0f)
                        DrawPotatoFins(_effect.CurrentTechnique == _potatoTexturedTechnique, camera.Position, ordered, instanceCount);
                }
                else
                {
                    if (clean > 0) DrawPotatoRun(part, 0, clean);

                    if (clean < instanceCount)
                    {
                        _effect.CurrentTechnique = _potatoBallDitherTechnique;
                        _dissolvePixelSizeParam.SetValue(DissolvePixelSize);
                        DrawPotatoRun(part, clean, instanceCount - clean);
                    }
                }
            }

            _effect.CurrentTechnique = _mainTechnique;
        }

        /// <summary>
        /// One instanced draw of <paramref name="part"/> through the effect's current technique, for
        /// <paramref name="count"/> instances of the instance buffer starting at <paramref name="firstInstance"/>
        /// (the binding's offset, in instances: DesktopGL has no base-instance draw on the Pi's GL 3.1).
        /// </summary>
        private void DrawPotatoRun(in MeshPartData part, int firstInstance, int count)
        {
            _graphicsDevice.SetVertexBuffers(
                new VertexBufferBinding(part.VertexBuffer, part.VertexOffset, 0),
                new VertexBufferBinding(_instanceBuffer, firstInstance, 1));
            _graphicsDevice.Indices = part.IndexBuffer;

            _effect.CurrentTechnique.Passes[0].Apply();

            _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, part.StartIndex, part.PrimitiveCount, count);
        }

        /// <summary>
        /// The fins of the first <paramref name="instanceCount"/> instances of the instance buffer (#804): one instanced
        /// draw of the mesh's <see cref="EdgeFinMesh"/> through <c>PotatoFinLit</c> or <c>PotatoFinTextured</c> - the
        /// mesh's own pixel shader times the fin's coverage - blended, depth-tested and not depth-written, with no
        /// culling. The states are put back as they were found, and the technique too: a caller's next part reads it.
        /// <para>
        /// One instance - which is every mesh that has fins today - is handed only the fins that can be open from this
        /// eye (<see cref="EdgeFinMesh.SelectLive(Vector3)"/>, and why it exists); several are handed every fin, each
        /// instance being seen from a side of its own.
        /// </para>
        /// </summary>
        private void DrawPotatoFins(bool textured, Vector3 eye, ModelInstance[] instances, int instanceCount)
        {
            IndexBuffer finIndices = _fins.IndexBuffer;
            int finPrimitives = _fins.PrimitiveCount;

            if (instanceCount == 1 && EdgeFins.SelectOnCpu)
            {
                //The eye in the mesh's own space, where its edges and their faces' normals are
                Matrix.Invert(ref instances[0].World, out Matrix toMesh);
                Vector3.Transform(ref eye, ref toMesh, out Vector3 eyeInMesh);

                finPrimitives = _fins.SelectLive(eyeInMesh);
                if (finPrimitives == 0) return;

                finIndices = _fins.LiveIndexBuffer;
            }

            Viewport viewport = _graphicsDevice.Viewport;
            _finShapeParam.SetValue(new Vector4(viewport.Width * 0.5f, viewport.Height * 0.5f, FinRampPixels, 0f));

            BlendState blend = _graphicsDevice.BlendState;
            DepthStencilState depth = _graphicsDevice.DepthStencilState;
            RasterizerState raster = _graphicsDevice.RasterizerState;
            EffectTechnique technique = _effect.CurrentTechnique;

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _effect.CurrentTechnique = textured ? _potatoFinTexturedTechnique : _potatoFinLitTechnique;

            _graphicsDevice.SetVertexBuffers(
                new VertexBufferBinding(_fins.VertexBuffer, 0, 0),
                new VertexBufferBinding(_instanceBuffer, 0, 1));
            _graphicsDevice.Indices = finIndices;

            _effect.CurrentTechnique.Passes[0].Apply();

            _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, finPrimitives, instanceCount);

            _effect.CurrentTechnique = technique;
            _graphicsDevice.BlendState = blend;
            _graphicsDevice.DepthStencilState = depth;
            _graphicsDevice.RasterizerState = raster;
        }

        /// <summary>
        /// <c>PotatoOutput.fxh</c>'s <c>SrgbToLinear</c> (Jim Hejl's cubic fit), the same arithmetic on the CPU (#804):
        /// what the Potato pixel shader decoded its material colours with, now done once a draw. The same fit and not
        /// the exact curve, so a colour comes out as it did when the shader decoded it.
        /// </summary>
        internal static Vector3 PotatoSrgbToLinear(Vector3 color) =>
            color * (color * (color * 0.305306011f + new Vector3(0.682171111f)) + new Vector3(0.012522878f));

        /// <summary>
        /// A Potato ball's uniforms from its linear colour (#804): the crust the light falls on, the emission at rest and
        /// on the beat, the lava's glow, the ripple's flash and alarm and the heartbeat's phase - everything
        /// <c>PotatoModel.fx</c>'s <c>BallColour</c> multiplied out per pixel from the style's figures, in its order.
        /// </summary>
        private void SetPotatoBallUniforms(Vector3 primary)
        {
            bool lava = Shading == BallShading.Lava;

            //The lava's crust (#795): a near-black basalt carrying a little of the ball's colour. Every other style's
            //crust is its colour.
            float crustTint = lava ? POTATO_LAVA_CRUST_TINT : 1f;
            float crustDark = lava ? POTATO_LAVA_CRUST_DARK : 1f;
            _potatoBallCrustParam.SetValue(primary * (crustTint * crustDark) + new Vector3(crustDark * (1f - crustTint)));

            Vector3 emission = primary * (EmissiveStrength * StillEmission);
            _potatoBallEmissionStillParam.SetValue(emission * (1f - PulseDepth));
            _potatoBallEmissionBeatParam.SetValue(emission * PulseDepth);

            float peak = MathF.Max(primary.X, MathF.Max(primary.Y, primary.Z));
            Vector3 hue = primary / MathF.Max(peak, 1e-3f);

            //The lava's seams as their average (#795), BallLava.fxh's arithmetic without the seams: the hue cut by
            //LavaHuePower, as bright as the tint's luminance lets it (LavaTintEmission)
            Vector3 glow = Vector3.Zero;
            if (lava)
            {
                Vector3 cut = new(MathF.Pow(MathHelper.Clamp(hue.X, 0f, 1f), 1.7f), MathF.Pow(MathHelper.Clamp(hue.Y, 0f, 1f), 1.7f),
                    MathF.Pow(MathHelper.Clamp(hue.Z, 0f, 1f), 1.7f));
                float luminance = MathHelper.Clamp(Vector3.Dot(primary, new Vector3(0.2126f, 0.7152f, 0.0722f)), 0f, 1f);

                glow = cut * (LavaGlow * POTATO_LAVA_SEAM_SHARE * MathHelper.Lerp(0.18f, 1f, luminance) * StillEmission);
            }

            _potatoBallGlowStillParam.SetValue(glow * (1f - PulseDepth));
            _potatoBallGlowBeatParam.SetValue(glow * PulseDepth);

            //Both ripples are nothing when the renderer has no ripple at all (the shader's step(1e-4, RippleStrength))
            float rippling = RippleStrength >= 1e-4f ? 1f : 0f;
            _potatoBallFlashParam.SetValue(Vector3.Lerp(hue, Vector3.One, 0.5f) * (RippleStrength * rippling));
            _potatoBallAlarmParam.SetValue(new Vector4(RippleAlarmColor * 1.7f, 0.95f * rippling));

            Vector3 phase = PulseDirection / MathF.Max(PulseWavelength, 1e-4f);
            _potatoPulsePhaseParam.SetValue(new Vector4(phase, PulseTime * PulseSpeed));
        }

        /// <summary>
        /// The rings of the first <paramref name="instanceCount"/> balls of the instance buffer (#804), one instanced draw
        /// of <see cref="RimMesh"/> through <c>PotatoBallRim</c> under the ball uniforms the caller has just set: blended
        /// (premultiplied, as every Potato surface is), depth-tested and not depth-written, with no culling since the
        /// strip has no facing. The states are put back as they were found.
        /// </summary>
        private void DrawPotatoRims(ICamera camera, int instanceCount)
        {
            //What one pixel of the BOUND target spans in the world at a clip w of 1: the scene target's own height below
            //native (#801), the back buffer's otherwise - the viewport is whichever is bound
            float pixel = 2f / (camera.Projection.M22 * _graphicsDevice.Viewport.Height);

            _rimShapeParam.SetValue(new Vector4(pixel, RimMeshRadius, RimMeshShortfall, Math.Max(RimRampPixels, 0.25f)));
            _rimSecantParam.SetValue(RimMesh.Secant);

            BlendState blend = _graphicsDevice.BlendState;
            DepthStencilState depth = _graphicsDevice.DepthStencilState;
            RasterizerState raster = _graphicsDevice.RasterizerState;

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _effect.CurrentTechnique = _potatoBallRimTechnique;

            _graphicsDevice.SetVertexBuffers(
                new VertexBufferBinding(RimMesh.VertexBuffer, 0, 0),
                new VertexBufferBinding(_instanceBuffer, 0, 1));
            _graphicsDevice.Indices = RimMesh.IndexBuffer;

            _effect.CurrentTechnique.Passes[0].Apply();

            _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, RimMesh.PrimitiveCount, instanceCount);

            _graphicsDevice.BlendState = blend;
            _graphicsDevice.DepthStencilState = depth;
            _graphicsDevice.RasterizerState = raster;
        }

        private void EnsureInstanceBufferCapacity(int instanceCapacity)
        {
            if (_instanceBuffer != null && _instanceBuffer.VertexCount >= instanceCapacity) return;

            _instanceBuffer?.Dispose();
            _instanceBuffer = new DynamicVertexBuffer(_graphicsDevice, ModelInstance.VertexDeclaration, instanceCapacity, BufferUsage.WriteOnly);
        }

        public void Dispose()
        {
            _instanceBuffer?.Dispose();
            _instanceBuffer = null;
        }
    }
}
