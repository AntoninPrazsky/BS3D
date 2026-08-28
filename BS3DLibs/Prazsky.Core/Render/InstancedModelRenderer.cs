using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

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
            public Texture2D Texture;
            public string MeshName;
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Effect _effect;
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
        private EffectParameter _normalMapParam;
        private EffectParameter _normalStrengthParam;
        private EffectParameter _surfaceReliefStrengthParam;
        private EffectParameter _slabSizeParam;
        private EffectParameter _slabJointWidthParam;
        private EffectParameter _slabJointDepthParam;
        private EffectParameter _cavityStrengthParam;
        private EffectParameter _reliefShadowStrengthParam;
        private EffectParameter _parallaxScaleParam;
        private EffectParameter _specularAmbientStrengthParam;
        private EffectParameter _metalnessParam;
        private EffectParameter _specularAlphaWeightParam;
        private EffectParameter _dirLightStrengthParam;
        private EffectParameter _emissiveStrengthParam;
        private EffectParameter _translucencyStrengthParam;
        private EffectParameter _pulseTimeParam;
        private EffectParameter _dissolvePixelSizeParam;
        private EffectParameter _pulseSpeedParam;
        private EffectParameter _pulseDepthParam;
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
        private EffectParameter _patternGoreCountParam;
        private EffectParameter _patternGoreThresholdParam;
        private EffectParameter _patternCapExtentParam;
        private EffectParameter _patternReliefStrengthParam;
        private EffectParameter _patternSheenStrengthParam;
        private EffectTechnique _mainTechnique;
        private EffectTechnique _texturedTechnique;
        private EffectTechnique _triplanarTechnique;
        private EffectTechnique _triplanarCoarseTechnique;
        private EffectTechnique[] _triplanarProbeTechniques;
        private EffectTechnique _detailUVTechnique;
        private EffectTechnique _detailUVNormalTechnique;
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
            _windowMarginParam, _windowLitFractionParam, _windowWarmParam, _windowCoolParam,
            _windowHoldSecondsParam, _windowHoldVariationParam, _windowSwitchFadeParam;

        //The plaster the windows sit in: its albedo, its grain, and how it answers light against how the glass
        //does. Only the city technique reads these, and the same CityConfig pushes them, so all three
        //executables get one facade rather than three that drift.
        private EffectParameter _facadeColorParam, _facadeNeonColorParam, _facadeColorVariationParam,
            _facadeGrainStrengthParam, _facadeGrainFrequencyParam, _facadeGrainShadingParam,
            _facadeSmoothnessParam, _facadeHighlightParam,
            _windowHighlightBoostParam, _windowReflectionBoostParam, _windowGlassColorParam;

        private EffectTechnique _depthTechnique;

        /// <summary>
        /// Optional detail texture modulating the material colors of a model that carries no texture
        /// of its own. Applied to the opaque mesh parts only; translucent parts (e.g. glass) stay clean.
        /// See <see cref="DetailTextureMapping"/> for how it is placed on the surface.
        /// </summary>
        public Texture2D DetailTexture { get; set; }

        /// <summary>
        /// How <see cref="DetailTexture"/> is mapped onto the surface. Objects that move or rotate must
        /// use <see cref="Render.DetailMapping.ModelUVs"/>, otherwise the world-space projection makes
        /// the texture swim across them.
        /// </summary>
        public DetailMapping DetailTextureMapping { get; set; } = DetailMapping.Triplanar;

        /// <summary>
        /// Size of the detail texture: world units per tile = 1 / <see cref="DetailScale"/> for
        /// <see cref="Render.DetailMapping.Triplanar"/>, tiles per UV span for <see cref="Render.DetailMapping.ModelUVs"/>.
        /// </summary>
        public float DetailScale { get; set; } = 0.25f;

        /// <summary>How strongly the detail texture modulates the material color (0 = not at all, 1 = fully).</summary>
        public float DetailStrength { get; set; } = 0.5f;

        /// <summary>Brightness compensation so a mid-gray detail texture does not darken the material.</summary>
        public float DetailBoost { get; set; } = 1f;

        /// <summary>
        /// Optional tangent-space normal map accompanying <see cref="DetailTexture"/>, giving the surface
        /// relief instead of just color variation. Only applies to <see cref="Render.DetailMapping.ModelUVs"/>;
        /// the tangent frame is derived in the shader, so the model needs no tangent vertex data.
        /// </summary>
        public Texture2D DetailNormalMap { get; set; }

        /// <summary>How far <see cref="DetailNormalMap"/> tilts the surface normal (0 = flat).</summary>
        public float DetailNormalStrength { get; set; } = 1f;

        /// <summary>
        /// Peak height of the procedural micro-relief of this model's surface, in world units
        /// (0 = the flat shading of a geometrically perfect surface). It only tilts the normal, so the
        /// silhouette stays exactly as modeled; what changes is that the surface catches light
        /// unevenly the way a real material does. Unlike <see cref="DetailNormalMap"/> it needs no
        /// texture, does not tile, and keeps its detail right down to the pixel that can still show it.
        /// </summary>
        public float SurfaceReliefStrength { get; set; }

        /// <summary>
        /// Base wave count per world unit of <see cref="SurfaceReliefStrength"/>: larger values give a
        /// finer grain. Six more octaves ride on top at rising frequencies, each fading out on its own
        /// once a screen pixel grows past half its wavelength.
        /// </summary>
        public float SurfaceReliefFrequency { get; set; } = 10f;

        /// <summary>
        /// The coarse height field on the triplanar path: three relief octaves instead of seven, which is
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
        /// their own shade, take the key light's shadow and shift under the view. This is the structure
        /// <see cref="ParallaxScale"/> and <see cref="ReliefShadowStrength"/> need to have any visible
        /// effect at all — micro-relief alone is far too shallow for either to read.
        /// </summary>
        public float SlabSize { get; set; }

        /// <summary>Half-width of the flat floor of a slab joint, in world units.</summary>
        public float SlabJointWidth { get; set; } = 0.02f;

        /// <summary>How far a slab joint sinks below the slab faces, in world units.</summary>
        public float SlabJointDepth { get; set; } = 0.05f;

        /// <summary>
        /// How dark the pits of the relief go from being shaded by their own walls (0 = off, 1 = black).
        /// Without it a normal-perturbed surface has its bumps lit but its hollows just as bright as its
        /// peaks, which is most of why relief-by-normal reads as a painted-on texture rather than shape.
        /// </summary>
        public float CavityStrength { get; set; }

        /// <summary>
        /// How strongly the relief casts the key light's shadow across itself (0 = off). Costs a short
        /// ray march per pixel; only surfaces with relief deep enough to shadow anything should ask for it.
        /// </summary>
        public float ReliefShadowStrength { get; set; }

        /// <summary>
        /// Depth range the parallax march covers, as a fraction of the relief's own amplitude (0 = off).
        /// Moving the shading point along the view ray is what gives a surface real depth: the near wall
        /// of a groove starts hiding its far wall as the camera moves, which is the one cue normal
        /// mapping cannot fake. The most expensive of the three — a ray march of up to 28 steps.
        /// </summary>
        public float ParallaxScale { get; set; }

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

        /// <summary>
        /// How strongly the patterned surface catches the sky color at grazing angles (0 = matte).
        /// </summary>
        public float PatternSheenStrength { get; set; } = 0.12f;

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
        public BoundingSphere BoundingSphere { get; }

        /// <summary>
        /// Creates a renderer for drawing many instances of the given model.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device to draw with (requires <see cref="GraphicsProfile.HiDef"/>).</param>
        /// <param name="model">Three-dimensional model whose instances will be rendered.
        /// Material colors are taken from the <see cref="BasicEffect"/>s the model was loaded with.</param>
        /// <param name="effect">The instancing effect (Shaders/InstancedModel.fx compiled by the content pipeline).</param>
        public InstancedModelRenderer(GraphicsDevice graphicsDevice, Model model, Effect effect)
        {
            _graphicsDevice = graphicsDevice;
            _effect = effect;

            Matrix[] boneTransforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(boneTransforms);

            List<MeshPartData> parts = new();
            BoundingSphere bounds = default;
            bool firstMesh = true;

            foreach (ModelMesh mesh in model.Meshes)
            {
                Matrix boneTransform = boneTransforms[mesh.ParentBone.Index];

                BoundingSphere meshBounds = mesh.BoundingSphere.Transform(boneTransform);
                bounds = firstMesh ? meshBounds : BoundingSphere.CreateMerged(bounds, meshBounds);
                firstMesh = false;

                foreach (ModelMeshPart part in mesh.MeshParts)
                {
                    Vector3 diffuse = Vector3.One;
                    Vector3 emissive = Vector3.Zero;
                    Vector3 specular = DEFAULT_SPECULAR_COLOR;
                    float specularPower = DEFAULT_SPECULAR_POWER;
                    float alpha = 1f;
                    Texture2D texture = null;

                    if (part.Effect is BasicEffect material)
                    {
                        diffuse = material.DiffuseColor;
                        emissive = material.EmissiveColor;
                        specular = material.SpecularColor;
                        specularPower = material.SpecularPower;
                        alpha = material.Alpha;
                        if (material.TextureEnabled) texture = material.Texture;
                    }

                    parts.Add(new MeshPartData
                    {
                        VertexBuffer = part.VertexBuffer,
                        IndexBuffer = part.IndexBuffer,
                        VertexOffset = part.VertexOffset,
                        StartIndex = part.StartIndex,
                        PrimitiveCount = part.PrimitiveCount,
                        BoneTransform = boneTransform,
                        DiffuseColor = new Vector4(diffuse, alpha),
                        EmissiveColor = emissive,
                        SpecularColor = specular,
                        SpecularPower = specularPower,
                        Texture = texture,
                        MeshName = mesh.Name
                    });
                }
            }

            _parts = parts.ToArray();
            BoundingSphere = bounds;

            InitializeEffect();
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

            InitializeEffect();
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
            _lightViewProjectionParam = _effect.Parameters["LightViewProjection"];
            _groundHeightParam = _effect.Parameters["GroundHeight"];

            _textureParam = _effect.Parameters["Texture"];
            _detailScaleParam = _effect.Parameters["DetailScale"];
            _detailStrengthParam = _effect.Parameters["DetailStrength"];
            _detailBoostParam = _effect.Parameters["DetailBoost"];
            _normalMapParam = _effect.Parameters["NormalMapTexture"];
            _normalStrengthParam = _effect.Parameters["NormalStrength"];
            _surfaceReliefStrengthParam = _effect.Parameters["SurfaceReliefStrength"];
            _slabSizeParam = _effect.Parameters["SlabSize"];
            _slabJointWidthParam = _effect.Parameters["SlabJointWidth"];
            _slabJointDepthParam = _effect.Parameters["SlabJointDepth"];
            _cavityStrengthParam = _effect.Parameters["CavityStrength"];
            _reliefShadowStrengthParam = _effect.Parameters["ReliefShadowStrength"];
            _parallaxScaleParam = _effect.Parameters["ParallaxScale"];
            _specularAmbientStrengthParam = _effect.Parameters["SpecularAmbientStrength"];
            _metalnessParam = _effect.Parameters["Metalness"];
            _specularAlphaWeightParam = _effect.Parameters["SpecularAlphaWeight"];
            _dirLightStrengthParam = _effect.Parameters["DirLightStrength"];
            _emissiveTintParam = _effect.Parameters["EmissiveTint"];
            _surfaceReliefFrequencyParam = _effect.Parameters["SurfaceReliefFrequency"];
            _patternPrimaryColorParam = _effect.Parameters["PatternPrimaryColor"];
            _patternSecondaryColorParam = _effect.Parameters["PatternSecondaryColor"];
            _patternGoreCountParam = _effect.Parameters["PatternGoreCount"];
            _patternGoreThresholdParam = _effect.Parameters["PatternGoreThreshold"];
            _patternCapExtentParam = _effect.Parameters["PatternCapExtent"];
            _patternReliefStrengthParam = _effect.Parameters["PatternReliefStrength"];
            _patternSheenStrengthParam = _effect.Parameters["PatternSheenStrength"];
            _emissiveStrengthParam = _effect.Parameters["EmissiveStrength"];
            _translucencyStrengthParam = _effect.Parameters["TranslucencyStrength"];
            _pulseTimeParam = _effect.Parameters["PulseTime"];
            _dissolvePixelSizeParam = _effect.Parameters["DissolvePixelSize"];
            _pulseSpeedParam = _effect.Parameters["PulseSpeed"];
            _pulseDepthParam = _effect.Parameters["PulseDepth"];
            _pulseDirectionParam = _effect.Parameters["PulseDirection"];
            _pulseWavelengthParam = _effect.Parameters["PulseWavelength"];
            _rippleStrengthParam = _effect.Parameters["RippleStrength"];
            _rippleAlarmColorParam = _effect.Parameters["RippleAlarmColor"];
            _mainTechnique = _effect.Techniques["InstancedModel"];
            _texturedTechnique = _effect.Techniques["InstancedModelTextured"];
            _triplanarTechnique = _effect.Techniques["InstancedModelTriplanar"];
            _triplanarCoarseTechnique = _effect.Techniques["InstancedModelTriplanarCoarse"];

            //#151 PROBE - TEMPORARY, delete with TriplanarProbe. Looked up once here like every other
            //technique, so selecting one costs an array index and never a name scan.
            _triplanarProbeTechniques = new[]
            {
                _effect.Techniques["InstancedModelTriplanarProbe1"],
                _effect.Techniques["InstancedModelTriplanarProbe2"],
                _effect.Techniques["InstancedModelTriplanarCoarse"],  //3 is the coarse technique itself, not a probe
                _effect.Techniques["InstancedModelTriplanarProbe4"],
                _effect.Techniques["InstancedModelTriplanarProbe5"],
                _effect.Techniques["InstancedModelTriplanarProbe6"],
            };
            _detailUVTechnique = _effect.Techniques["InstancedModelDetailUV"];
            _detailUVNormalTechnique = _effect.Techniques["InstancedModelDetailUVNormal"];
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
            _windowMarginParam = _effect.Parameters["WindowMargin"];
            _windowLitFractionParam = _effect.Parameters["WindowLitFraction"];
            _windowWarmParam = _effect.Parameters["WindowWarm"];
            _windowCoolParam = _effect.Parameters["WindowCool"];
            _windowHoldSecondsParam = _effect.Parameters["WindowHoldSeconds"];
            _windowHoldVariationParam = _effect.Parameters["WindowHoldVariation"];
            _windowSwitchFadeParam = _effect.Parameters["WindowSwitchFade"];
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
        /// </summary>
        public void DrawDepth(Matrix lightViewProjection, ModelInstance[] instances, int instanceCount)
        {
            if (instanceCount <= 0) return;

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
            _cavityStrengthParam.SetValue(CavityStrength);
            _reliefShadowStrengthParam.SetValue(ReliefShadowStrength);
            _parallaxScaleParam.SetValue(ParallaxScale);
            _specularAmbientStrengthParam.SetValue(SpecularAmbientStrength);
            _metalnessParam.SetValue(Metalness);

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
                bool usePattern = PatternGoreCount > 0 && part.Texture == null && part.DiffuseColor.W >= 1f;

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
        /// are mutually exclusive: a city facade, a UV-textured part, the beach-ball pattern, a detail-textured
        /// part (triplanar or through the model's own UVs), or the plain lit material.
        /// </summary>
        private void SelectTechniqueAndParameters(in MeshPartData part, bool usePattern, Vector3? diffuseTint)
        {
            //Mesh parts with their own texture sample it through UVs; parts of a UV-less model
            //can get a triplanar world-space detail texture instead (opaque parts only)
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
                _windowMarginParam.SetValue(city.WindowMargin);
                _windowLitFractionParam.SetValue(city.WindowLitFraction);
                _windowWarmParam.SetValue(city.WindowWarm.ToVector3());
                _windowCoolParam.SetValue(city.WindowCool.ToVector3());
                _windowHoldSecondsParam.SetValue(city.WindowHoldSeconds);
                _windowHoldVariationParam.SetValue(city.WindowHoldVariation);
                _windowSwitchFadeParam.SetValue(city.WindowSwitchFade);
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
            else if (part.Texture != null)
            {
                _effect.CurrentTechnique = _texturedTechnique;
                _textureParam.SetValue(part.Texture);
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

                    case BallShading.Vinyl:
                        _patternSecondaryColorParam.SetValue(PatternSecondaryColor);
                        _patternGoreCountParam.SetValue((float)PatternGoreCount);

                        //Thresholding sin(azimuth) at -cos(pi * width) hands the primary gore exactly that
                        //fraction of each pair of segments; the even split lands on zero, as before
                        _patternGoreThresholdParam.SetValue(-MathF.Cos(MathF.PI * PatternGoreWidth));
                        _patternCapExtentParam.SetValue(PatternCapExtent);
                        _patternReliefStrengthParam.SetValue(PatternReliefStrength);
                        _patternSheenStrengthParam.SetValue(PatternSheenStrength);
                        _translucencyStrengthParam.SetValue(TranslucencyStrength);
                        break;
                }

                _emissiveStrengthParam.SetValue(EmissiveStrength);
                _pulseTimeParam.SetValue(PulseTime);
                _dissolvePixelSizeParam.SetValue(DissolvePixelSize);
                _pulseSpeedParam.SetValue(PulseSpeed);
                _pulseDepthParam.SetValue(PulseDepth);
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
                bool useModelUVs = DetailTextureMapping == DetailMapping.ModelUVs;
                bool useNormalMap = useModelUVs && DetailNormalMap != null;

                _effect.CurrentTechnique = useNormalMap ? _detailUVNormalTechnique
                    : useModelUVs ? _detailUVTechnique
                    : TriplanarProbe > 0 ? _triplanarProbeTechniques[TriplanarProbe - 1]
                    : CoarseSurfaceRelief ? _triplanarCoarseTechnique
                    : _triplanarTechnique;

                _textureParam.SetValue(DetailTexture);
                _detailScaleParam.SetValue(DetailScale);
                _detailStrengthParam.SetValue(DetailStrength);
                _detailBoostParam.SetValue(DetailBoost);

                if (useNormalMap)
                {
                    _normalMapParam.SetValue(DetailNormalMap);
                    _normalStrengthParam.SetValue(DetailNormalStrength);
                }
            }
            else
            {
                _effect.CurrentTechnique = _mainTechnique;
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
