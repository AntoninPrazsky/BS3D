using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The forest scatter's rendering hardware: everything the trees, boulders and stumps are drawn
    /// <b>with</b>, as <see cref="ForestScatter"/> is everything about <b>where</b> they stand. The two
    /// procedural <see cref="SurfaceTexture"/>s, the fifteen mesh variants (six spruces, four broadleaves,
    /// three boulders, two stumps), the twenty-five <see cref="InstancedModelRenderer"/>s that dress them as
    /// bark, foliage, weathered stone and sawn wood, the three matte <see cref="BasicEffectParams"/>, the
    /// per-variant tints and the six instanced draws.
    /// <para>
    /// It lived in the Game alone until #75, which is exactly why the Testbed and the map editor drew the
    /// forest as a bare clearing — the terrain under it was always the shared <see cref="SceneRenderer"/>'s,
    /// so all three executables had the glade and only one of them had the wood standing in it. Nothing here
    /// is the Game's by nature; the split was an accident of where the scene was built.
    /// </para>
    /// <para>
    /// <b>Each kind is built at several sets of proportions AND its own structural seed</b>, and the scatter
    /// splits its instances between them. One mesh per kind is what makes a grove read as one tree stamped out
    /// fifty times — a uniform scale and a yaw do not change a silhouette, and the silhouette is what the eye
    /// counts. The variety is in the <b>mesh</b> rather than in a per-instance stretch because the shader
    /// transforms normals by the world matrix itself, with no inverse transpose, so a non-uniform scale would
    /// shade a squashed tree as though it were still the shape it was authored at. The seed rolls what
    /// proportions cannot: the spruce's tier layout, the broadleaf's leaf lobes, every part's wobble phase
    /// (see <see cref="TreeMesh"/>) — proportions alone made three copies of one tree at three sizes, and the
    /// eye caught it. Six spruces, four broadleaves, three boulders and two stumps: some two dozen instanced
    /// draws over the whole scene, against a scatter that measured thirteen frames out of nine hundred.
    /// </para>
    /// <para>
    /// <b>The per-draw tints are encoded once and cached</b>, which is the one thing the hoist changes rather
    /// than moves. The Game rebuilt all twenty-five of them on every frame the forest was on screen — a
    /// <c>GetSceneConfig</c> plus a <c>ToVector3</c> per kind and a <see cref="ColorSpace.LinearToSrgb"/> per
    /// variant — for values that only a config edit can change. So the config is read at construction and in
    /// <see cref="Replant"/>, and nowhere else: <b>a caller that mutates the config in place must call
    /// <see cref="Replant"/></b> or it will go on drawing the colours the old config asked for.
    /// </para>
    /// <para>
    /// <b>Sky-lit enrolment stays the caller's</b>, for the reasons <see cref="SkyLightRig"/> gives — each
    /// executable has its own list and its own reasons. What this contributes is <see cref="Renderers"/>, the
    /// one array the enrolment and the disposal both walk, so neither can fall out of step with the variant
    /// lists. Every variant of every kind takes part, or a spruce of the variant that was missed would stand
    /// under the light rig of whatever dome was up when it was made.
    /// </para>
    /// <para>
    /// <b>The frame sequence stays the caller's too.</b> <see cref="Draw"/> touches no GPU state at all and
    /// gates on no scene: the caller keeps the <c>if (scene == SceneKind.Forest)</c> test and places the draw
    /// after the forest terrain the scatter stands on and before the island (see <see cref="Draw"/> for the
    /// state it assumes on entry).
    /// </para>
    /// <para>
    /// <b>It owns no content.</b> The shared instancing effect (<c>Shaders/InstancedModel.fx</c>) is compiled
    /// by the caller's content pipeline, handed in, and never disposed here — the libraries have no content
    /// pipeline of their own (see CLAUDE.md). Nothing here looks an effect parameter up by name; every draw
    /// goes through <see cref="InstancedModelRenderer"/>, which caches its own handles at construction, and
    /// every never-changing per-renderer value is set once in its object initialiser.
    /// </para>
    /// </summary>
    public sealed class ForestScatterRenderer : IDisposable
    {
        /// <summary>
        /// The scatter seed the Game shipped, and the same seed always plants the same forest. A
        /// <c>const</c> so it can be a default argument; nothing has ever wanted another one.
        /// </summary>
        public const int DEFAULT_SEED = 77125;

        //The material diffuse the scatter's meshes are built with, and it is 0.8 rather than white on
        //purpose: passing a diffuse tint makes the renderer reduce the material colour to its luminance and
        //multiply by 1.25, a boost calibrated for models whose brightest material is 0.8. On a white
        //material that lands the tint 1.25x brighter than it was authored — so the encoded colours below
        //would render half again as bright as the linear radiance the config documents. At 0.8 the
        //luminance and the boost cancel and the tint arrives exactly as passed.
        private static readonly Vector3 SCATTER_MATERIAL_DIFFUSE = Vector3.One * 0.8f;

        //Per-variant foliage tint multipliers, applied to the config's LINEAR colour before it is encoded
        //for the draw — a real stand is never one green, and two variants side by side should differ in
        //colour as well as in silhouette. Means sit near 1 so the config colour stays the authored average;
        //indexed by mesh variant, so they must be at least as long as the variant arrays — which is a
        //constructor-time check now (see Build), because a seventh spruce with no seventh tint used to throw
        //inside the draw and nothing said so.
        private static readonly Vector3[] CONIFER_TINTS =
        {
            new(1f, 1f, 1f),
            new(0.82f, 0.92f, 0.88f),   //darker, cooler — an older tree in shade
            new(1.14f, 1.06f, 0.92f),   //warmer — the sunlit edge of the stand
            new(0.9f, 1.02f, 1.08f),    //the blue-green a spruce reads as
            new(1.06f, 0.96f, 0.88f),
            new(0.78f, 0.9f, 0.95f)
        };

        private static readonly Vector3[] BROADLEAF_TINTS =
        {
            new(1f, 1f, 1f),
            new(1.18f, 1.08f, 0.85f),   //yellow-green — a birch against the oaks
            new(0.84f, 0.94f, 0.9f),
            new(1.04f, 1.12f, 0.9f)
        };

        //World units one tile of each detail texture spans on the surface it dresses; DetailScale is the
        //reciprocal, as the island's STONE_SPAN and CONCRETE_SPAN are. A trunk gets the coarsest tiling of the
        //four, a canopy the finest — a leaf mass has more detail per unit than a bark flank does.
        private const float TRUNK_BARK_SPAN = 2.2f;
        private const float CROWN_FOLIAGE_SPAN = 1.6f;
        private const float ROCK_STONE_SPAN = 1.5f;
        private const float STUMP_BARK_SPAN = 1.8f;

        //The scatter's materials. A procedural mesh defaults to the white specular that suits vinyl and
        //glass, and on a dark crown that white highlight is what read as wet obsidian; foliage and bark are
        //matte and a weathered boulder nearly so. Zero cannot be passed for it — a zero specular in
        //BasicEffectParams means "no override" and falls back to the white default — so matte here is a dim
        //tinted specular at a modest power, never zero. The ambient keeps the scene's flat fill, which is why
        //it is the one thing the constructor is told about the scene.
        private static readonly Vector3 FOLIAGE_SPECULAR = new(0.05f, 0.06f, 0.05f);
        private const float FOLIAGE_SPECULAR_POWER = 10f;
        private static readonly Vector3 BARK_SPECULAR = new(0.07f, 0.06f, 0.05f);
        private const float BARK_SPECULAR_POWER = 12f;
        private static readonly Vector3 ROCK_SPECULAR = new(0.16f, 0.16f, 0.15f);
        private const float ROCK_SPECULAR_POWER = 24f;

        private readonly GraphicsDevice _device;
        private readonly Effect _instancingEffect;
        private readonly int _seed;

        //The bark and the foliage are always this component's own. The stone may be the caller's — see the
        //constructor's remarks and _ownsStoneTexture, which is the only thing Dispose consults.
        private readonly SurfaceTexture _barkTexture, _foliageTexture, _stoneTexture;
        private readonly bool _ownsStoneTexture;

        private readonly BasicEffectParams _foliageEffectParams, _barkEffectParams, _rockEffectParams;

        /// <summary>
        /// How far a pigment is carried towards the dome's hue by <see cref="ApplySkyTint"/>. A third: enough
        /// that a sunset warms the wood and a cold dome cools it unmistakably, and far enough short of 1 that
        /// a spruce is still recognisably a spruce rather than whatever colour the sky happens to be. It is
        /// the same figure and the same idea as <c>SkyLightRig.SKY_TINT_STRENGTH</c>, which lerps the key
        /// light half of the way to the horizon rather than all of it, and for the same reason.
        /// </summary>
        private const float SKY_TINT_STRENGTH = 0.34f;

        //Everything below is derived from the config, so all of it is rebuilt by Replant and none of it can be
        //readonly. The mesh variants: a bark lathe under a lathed tiered cone for a spruce or a lobed sphere
        //for a broadleaf, plus a rock lathe and a stump lathe.
        private TreeMesh[] _coniferMeshes, _broadleafMeshes;
        private RockMesh[] _rockMeshes;
        private StumpMesh[] _stumpMeshes;

        //A trunk and a crown are two renderers because they are two materials — bark and foliage — and the
        //diffuse tint is a per-draw uniform, not per-instance. Every variant is its own instanced draw, which
        //is why these are arrays rather than single renderers.
        private InstancedModelRenderer[] _coniferTrunkRenderers, _coniferCrownRenderers;
        private InstancedModelRenderer[] _broadleafTrunkRenderers, _broadleafCrownRenderers;
        private InstancedModelRenderer[] _rockRenderers, _stumpRenderers;

        //The flat list of all twenty-five, in draw order. See Renderers.
        private InstancedModelRenderer[] _renderers;

        //One already-encoded sRGB tint per mesh variant per draw — the whole point of the cache. Six arrays
        //rather than two colours and two multiplier tables, so the draw is one uniform path with no branch and
        //no arithmetic in it (see EncodeTints, which holds the colour-space reasoning).
        private Vector3[] _coniferTrunkTints, _coniferCrownTints;
        private Vector3[] _broadleafTrunkTints, _broadleafCrownTints;
        private Vector3[] _rockTints, _stumpTints;

        //The dome hue the tints above were last encoded against, so re-encoding is skipped on every frame the
        //dome has not moved — which is nearly all of them, since ApplySkyTint is called from the same
        //per-frame pass the overcast lerp drives. Starts white, which is "no shift" and matches a caller that
        //never calls it at all.
        private Vector3 _skyTint = Vector3.One;

        //The authored linear colours, kept because the encoded tints above can no longer be re-derived from
        //themselves: the sky shift is applied on the way in, so re-encoding needs the originals.
        private Vector3 _barkColorLinear, _coniferColorLinear, _foliageColorLinear, _rockColorLinear, _stumpColorLinear;

        private ForestScatter _scatter;

        /// <summary>
        /// Builds the whole wood here and now: both procedural textures, the fifteen meshes, the twenty-five
        /// renderers with their per-material dressing, the encoded tints and the scatter itself. There is
        /// nothing runtime about any of it — the scatter is a fixed default forest, as the city is a fixed
        /// default city — so a caller with a static config builds it once at load and never touches it again.
        /// <para>
        /// <b>The caller must run its own sky lighting after this</b>, and again after every
        /// <see cref="Replant"/>: a fresh <see cref="InstancedModelRenderer"/> has never been told the dome's
        /// palette, so until the caller's pass reaches <see cref="Renderers"/> the whole wood is lit by a white
        /// sky through a rig that was never decoded into radiance.
        /// </para>
        /// </summary>
        /// <param name="device">Graphics device the meshes, textures and instance buffers live on.</param>
        /// <param name="instancingEffect">The shared instancing effect (<c>Shaders/InstancedModel.fx</c>),
        /// compiled by the caller's content pipeline and handed in. <b>Not disposed here</b> — see the class
        /// remarks on content. It is what makes the scatter take the dome's sky lighting, the clouds' shadows
        /// and the scene's point lights automatically, exactly as the island and the city do.</param>
        /// <param name="config">The forest scene's configuration — the proportions every mesh is built from,
        /// the counts and radii the scatter is planted by, and the linear colours the tints are encoded from.
        /// Read here and in <see cref="Replant"/> only; the instance stays the caller's (it is the
        /// <see cref="SceneRenderer"/>'s in all three executables).</param>
        /// <param name="sceneAmbientIntensity">The caller's flat ambient fill for scene objects, and all the
        /// three matte <see cref="BasicEffectParams"/> need from the scene: their reason for existing is to
        /// carry a dim specular instead of the renderer's white default, and they must not otherwise light the
        /// wood differently from everything around it. All three executables pass 0.25.</param>
        /// <param name="stoneTexture">The caller's dressed-stone <see cref="SurfaceTexture"/>, which the
        /// boulders borrow — they are the island's cap dressing, only rougher. <b>A texture handed in here is
        /// never disposed by this component</b>; pass <c>null</c> (the normal case now that
        /// <see cref="ArenaIsland"/> keeps its own copy private) and one is built and owned internally.
        /// <see cref="SurfaceTexture.Stone"/> is deterministic at a fixed default seed, so an internal one is
        /// the very same 512² tile the island's is — one more copy in memory and not a pixel of
        /// difference.</param>
        /// <param name="seed">Scatter seed; the same seed always plants the same forest. Kept for the life of
        /// the component, so a <see cref="Replant"/> after a config edit re-plants <i>this</i> forest rather
        /// than rolling a different one.</param>
        public ForestScatterRenderer(GraphicsDevice device, Effect instancingEffect, ForestSceneConfig config,
            float sceneAmbientIntensity, SurfaceTexture stoneTexture = null, int seed = DEFAULT_SEED)
        {
            _device = device;
            _instancingEffect = instancingEffect;
            _seed = seed;

            //The detail texture is what selects the triplanar technique — without one the renderer falls
            //through to plain and every relief and cavity setting below is silently dead, as the island's own
            //comment warns. All of these are generated rather than loaded, which is a fix rather than a
            //flourish: the marble photograph this world used to project covers only the left half of its
            //canvas and the rest is black, so a triplanar projection multiplied roughly half of any surface by
            //zero at any detail scale. These tile exactly.
            _barkTexture = SurfaceTexture.Bark(device);
            _foliageTexture = SurfaceTexture.Foliage(device);

            _ownsStoneTexture = stoneTexture == null;
            _stoneTexture = stoneTexture ?? SurfaceTexture.Stone(device);

            _foliageEffectParams = new BasicEffectParams(Vector3.One * sceneAmbientIntensity,
                FOLIAGE_SPECULAR, FOLIAGE_SPECULAR_POWER, Vector3.Zero);
            _barkEffectParams = new BasicEffectParams(Vector3.One * sceneAmbientIntensity,
                BARK_SPECULAR, BARK_SPECULAR_POWER, Vector3.Zero);
            _rockEffectParams = new BasicEffectParams(Vector3.One * sceneAmbientIntensity,
                ROCK_SPECULAR, ROCK_SPECULAR_POWER, Vector3.Zero);

            Build(config);
        }

        /// <summary>
        /// Every renderer the scatter draws through — each kind at each of its mesh variants, in draw order.
        /// One place, so the caller's sky-lighting enrolment and this component's own disposal cannot fall out
        /// of step with the variant lists. This is the only reason the renderers are exposed at all; nothing
        /// else needs them.
        /// <para>
        /// The array itself, not a read-only view, and that is measured rather than careless — the precedent
        /// is <c>BallRenderSet.Renderers</c>, whose doc records the same incident: the Testbed refills its
        /// enrolment list <i>every frame</i> (the overcast lerp re-applies the rig), and <c>foreach</c> over an
        /// array allocates no enumerator while <c>foreach</c> over an <c>IReadOnlyList</c> boxes one per call.
        /// BestPractices.md §3 is the rule.
        /// </para>
        /// <para>
        /// <see cref="Replant"/> builds a fresh array of fresh renderers, so read this through the property
        /// rather than caching the reference — and re-run the sky lighting over it afterwards.
        /// </para>
        /// </summary>
        public InstancedModelRenderer[] Renderers => _renderers;

        /// <summary>
        /// The six instanced draws, in order: conifer trunks, conifer crowns, broadleaf trunks, broadleaf
        /// crowns, boulders, stumps — one draw per kind per mesh variant, and per material within a tree. A
        /// species' trunk and crown read that variant's own scatter of world matrices (the crown mesh is built
        /// sitting on its trunk's top, so one position places both) and are two draws because their tints
        /// differ — bark and foliage — and the diffuse tint is a per-draw uniform.
        /// <para>
        /// <b>Touches no GPU state, on purpose</b>, and needs none of its own: every mesh here is a closed
        /// solid wound clockwise seen from outside (the convention in CLAUDE.md), so the scene's ordinary
        /// back-face culling is exactly right and drawing it that way is what would <i>show</i> a winding
        /// mistake rather than hide one. What it assumes on entry is the opaque scene state the callers already
        /// set before their backdrop: the HDR scene target bound, <see cref="BlendState.AlphaBlend"/>,
        /// <see cref="DepthStencilState.Default"/> (depth test <b>and</b> write — the trees are opaque solids
        /// and must occlude each other) and <see cref="RasterizerState.CullCounterClockwise"/>, with this
        /// frame's cloud uniforms, sun and scene point lights already on the shared effect. It leaves all of it
        /// exactly as it found it.
        /// </para>
        /// <para>
        /// <b>Where in the frame is the caller's</b>: after the forest terrain the scatter stands on — with
        /// depth, or the trees draw through it — and before the island. And so is the scene gate: this draws
        /// the wood whenever it is called, so the caller keeps its <c>if (scene == SceneKind.Forest)</c>.
        /// </para>
        /// <para>
        /// Allocation-free: plain indexed loops, and the tints were encoded at build time. An iterator or a zip
        /// over the renderer and instance arrays would allocate one enumerator per kind per frame.
        /// </para>
        /// </summary>
        /// <param name="camera">This frame's camera, for the view and projection each draw needs.</param>
        public void Draw(ICamera camera)
        {
            DrawScatter(camera, _coniferTrunkRenderers, _scatter.Conifers, _barkEffectParams, _coniferTrunkTints);
            DrawScatter(camera, _coniferCrownRenderers, _scatter.Conifers, _foliageEffectParams, _coniferCrownTints);
            DrawScatter(camera, _broadleafTrunkRenderers, _scatter.Broadleaves, _barkEffectParams, _broadleafTrunkTints);
            DrawScatter(camera, _broadleafCrownRenderers, _scatter.Broadleaves, _foliageEffectParams, _broadleafCrownTints);
            DrawScatter(camera, _rockRenderers, _scatter.Rocks, _rockEffectParams, _rockTints);
            DrawScatter(camera, _stumpRenderers, _scatter.Stumps, _barkEffectParams, _stumpTints);
        }

        /// <summary>
        /// Rebuilds everything the config decides — the meshes, the renderers, the encoded tints and the
        /// scatter — for a config that has changed under the component. <b>This is what the map editor's live
        /// scene-config grid needs</b> and the Game never has: an edit there re-applies the config to the
        /// <see cref="SceneRenderer"/> in place, and the wood standing on the terrain has to follow it.
        /// <para>
        /// The whole build rather than the scatter alone, because a config edit reaches further than the
        /// planting does: <c>Count</c>, the radii and the cluster figures are the scatter's, but
        /// <c>TrunkBaseRadius</c>, the crown sizes and the rocks' and stumps' proportions are <b>baked into
        /// the meshes</b>, and the three colours into the cached tints. Re-planting alone would answer a
        /// changed count while quietly ignoring a changed shape, which is the sort of half-refresh that reads
        /// as a bug in the editor rather than in here.
        /// </para>
        /// <para>
        /// <b>A load-time cost, not a per-frame one</b> — fifteen meshes and twenty-five instance buffers are
        /// released and rebuilt — so call it when a config actually changed and not on the draw path. The two
        /// textures and the three <see cref="BasicEffectParams"/> survive it: neither depends on the config.
        /// </para>
        /// <para>
        /// <b>The caller must re-run its sky lighting afterwards.</b> Every renderer here is new and none of
        /// them has been told the dome's palette; skip it and the whole wood goes flat under a white sky while
        /// the terrain beside it stays lit. The seed is the one this was constructed with, so it is the same
        /// forest re-planted rather than a new one.
        /// </para>
        /// </summary>
        /// <param name="config">The changed configuration — normally the same instance the constructor was
        /// given, since a live editor mutates the <see cref="SceneRenderer"/>'s own config in place.</param>
        public void Replant(ForestSceneConfig config)
        {
            DisposeBuilt();
            Build(config);
        }

        /// <summary>
        /// Everything this component made: the fifteen meshes, all twenty-five renderers (each holding a
        /// native instance buffer), the bark and foliage textures — and the stone texture only if it built
        /// that one itself. A stone texture handed to the constructor belongs to the caller and is left
        /// alone; the Game's used to be the island's own, disposed with the island's block, and disposing a
        /// borrowed texture from here would have pulled the platform's dressing out from under it.
        /// <para>
        /// The instancing effect is the caller's content manager's and is left alone as well.
        /// </para>
        /// </summary>
        public void Dispose()
        {
            DisposeBuilt();

            _barkTexture?.Dispose();
            _foliageTexture?.Dispose();

            if (_ownsStoneTexture) _stoneTexture?.Dispose();
        }

        //Everything the config decides, built in one place so the constructor and Replant cannot drift apart.
        private void Build(ForestSceneConfig config)
        {
            ForestTreeConfig trees = config.Trees;

            //Each kind at several sets of proportions and its own structural roll — the reasoning is in the
            //class remarks, and it is the whole reason a grove does not read as one tree stamped out fifty
            //times. The comments name what each variant is FOR: the silhouettes have to differ, not merely the
            //sizes, and a set of variants that all read as the same tree is a set that has drifted.
            _coniferMeshes = new[]
            {
                NewConifer(trees, 1f, 1f, seed: 11),          //the authored spruce
                NewConifer(trees, 0.78f, 1.24f, seed: 23),    //a narrow, taller one — the crowded stems of a stand
                NewConifer(trees, 1.3f, 0.76f, seed: 37),     //a broad, squat one — an old tree with room around it
                NewConifer(trees, 0.9f, 1.1f, seed: 41),      //an ordinary tree that is nobody's copy
                NewConifer(trees, 1.12f, 0.94f, seed: 53),    //slightly stout
                NewConifer(trees, 0.7f, 1.38f, seed: 67)      //a spire — the one that carries the skyline
            };

            _broadleafMeshes = new[]
            {
                NewBroadleaf(trees, 1f, 1f, seed: 79),        //the authored broadleaf
                NewBroadleaf(trees, 0.82f, 1.3f, seed: 83),   //narrower and taller, its crown carried higher
                NewBroadleaf(trees, 1.22f, 0.85f, seed: 97),  //broad and low — an old oak's spread
                NewBroadleaf(trees, 0.95f, 1.12f, seed: 101)  //an ordinary tree between them
            };

            _rockMeshes = new[]
            {
                new RockMesh(_device, config.Rocks.Radius, config.Rocks.Height),
                new RockMesh(_device, config.Rocks.Radius * 1.25f, config.Rocks.Height * 0.7f, irregularityPhase: 2.1f),
                new RockMesh(_device, config.Rocks.Radius * 0.8f, config.Rocks.Height * 1.5f, irregularityPhase: 4.2f)
            };

            _stumpMeshes = new[]
            {
                new StumpMesh(_device, config.Stumps.Radius, config.Stumps.Height),
                new StumpMesh(_device, config.Stumps.Radius * 1.3f, config.Stumps.Height * 0.55f, irregularityPhase: 3.3f)
            };

            //The tint tables are indexed by mesh variant, so a variant past the end of its table throws inside
            //the draw — where the forest is only ever on screen in one scene, so it would be a crash nobody met
            //until they picked the forest. It was an unchecked coupling between two lists in one file for as
            //long as the piece existed; a seventh spruce is exactly the change that would trip it, and this is
            //where that now fails at once and says what to add.
            if (CONIFER_TINTS.Length < _coniferMeshes.Length || BROADLEAF_TINTS.Length < _broadleafMeshes.Length)
            {
                throw new InvalidOperationException(
                    $"Forest scatter tint tables are shorter than their mesh-variant arrays: " +
                    $"{CONIFER_TINTS.Length} conifer tints for {_coniferMeshes.Length} conifer meshes, " +
                    $"{BROADLEAF_TINTS.Length} broadleaf tints for {_broadleafMeshes.Length} broadleaf meshes. " +
                    $"Every variant needs a tint multiplier of its own.");
            }

            _coniferTrunkRenderers = Array.ConvertAll(_coniferMeshes, m => NewTrunkRenderer(m.Trunk));
            _coniferCrownRenderers = Array.ConvertAll(_coniferMeshes, m => NewCrownRenderer(m.Crown));
            _broadleafTrunkRenderers = Array.ConvertAll(_broadleafMeshes, m => NewTrunkRenderer(m.Trunk));
            _broadleafCrownRenderers = Array.ConvertAll(_broadleafMeshes, m => NewCrownRenderer(m.Crown));
            _rockRenderers = Array.ConvertAll(_rockMeshes, m => NewRockRenderer(m));
            _stumpRenderers = Array.ConvertAll(_stumpMeshes, m => NewStumpRenderer(m));

            //The one flat list, in draw order (see Renderers). Built from the six arrays rather than filled
            //alongside them, so a kind added later joins the sky lighting and the disposal by being added here
            //once instead of in three places.
            _renderers = new InstancedModelRenderer[
                _coniferTrunkRenderers.Length + _coniferCrownRenderers.Length +
                _broadleafTrunkRenderers.Length + _broadleafCrownRenderers.Length +
                _rockRenderers.Length + _stumpRenderers.Length];

            int next = 0;
            next = CopyInto(_coniferTrunkRenderers, next);
            next = CopyInto(_coniferCrownRenderers, next);
            next = CopyInto(_broadleafTrunkRenderers, next);
            next = CopyInto(_broadleafCrownRenderers, next);
            next = CopyInto(_rockRenderers, next);
            CopyInto(_stumpRenderers, next);

            //The trunks of both species share the bark colour; the stumps have a lighter wood of their own,
            //being mostly the pale sawn face. Only the two canopies take per-variant multipliers.
            _barkColorLinear = trees.TrunkColor.ToVector3();
            _coniferColorLinear = trees.ConiferColor.ToVector3();
            _foliageColorLinear = trees.FoliageColor.ToVector3();
            _rockColorLinear = config.Rocks.Color.ToVector3();
            _stumpColorLinear = config.Stumps.Color.ToVector3();

            EncodeAllTints();

            //Planted on the forest floor the terrain shader draws: ForestTerrainHeight mirrors Forest.fx's
            //height field on the CPU, so a tree's base sits on the ground the player sees. The scatter is told
            //how many variants each kind has so it can hand back one instance array per variant — the renderer
            //draws a contiguous prefix of what it is given, so the split has to happen at plant time rather
            //than at the draw.
            _scatter = new ForestScatter(_seed, config,
                _coniferMeshes.Length, _broadleafMeshes.Length, _rockMeshes.Length, _stumpMeshes.Length,
                (x, z) => SceneRenderer.ForestTerrainHeight(x, z, config));
        }

        private int CopyInto(InstancedModelRenderer[] source, int at)
        {
            Array.Copy(source, 0, _renderers, at, source.Length);
            return at + source.Length;
        }

        /// <summary>
        /// One already-encoded sRGB tint per mesh variant, so the draw carries no arithmetic at all.
        /// <para>
        /// The config colours are linear radiance, like every scene config's — but they ride the same
        /// material-diffuse uniform the balls' sRGB tints do, which the shader decodes from sRGB at the tap.
        /// Handing the linear values over raw is what turned the first forest's crowns near-black: decoded as
        /// if they were display values, a 0.16 became a 0.02. So each is encoded once, here — and
        /// <b>after</b> the per-variant multiplier, which has to work on the linear value, because scaling an
        /// encoded sRGB number is not a scale of the light it stands for.
        /// </para>
        /// </summary>
        /// <param name="linearColor">The config's colour for this kind, in linear radiance.</param>
        /// <param name="variants">How many mesh variants the kind has.</param>
        /// <param name="variantTints">Per-variant multipliers on the linear colour, or null for a kind whose
        /// variants all take the config colour as it stands (the bark and the stone).</param>
        private Vector3[] EncodeTints(Vector3 linearColor, int variants, Vector3[] variantTints)
        {
            var tints = new Vector3[variants];

            for (int variant = 0; variant < variants; variant++)
            {
                Vector3 linear = variantTints == null ? linearColor : linearColor * variantTints[variant];
                tints[variant] = ColorSpace.LinearToSrgb(ShiftTowardsSky(linear));
            }

            return tints;
        }

        /// <summary>
        /// Moves a pigment part of the way towards the dome's own hue, <b>at its own brightness</b>.
        /// <para>
        /// <b>Why a colour needs shifting at all when the lighting already carries the dome.</b> It does carry
        /// it — the rig tints the key light and the hemisphere ambient, and the wood is enrolled for both in
        /// all three executables. The trouble is the pigment: the authored foliage is
        /// <c>(0.042, 0.115, 0.026)</c> linear, whose red and blue channels are near zero, so a coloured light
        /// has almost nothing to multiply there and the crowns stay the same green under every sky while the
        /// ground around them turns with the dome (#108). Proved by making the pigment neutral grey for one
        /// run: the very same trees then took a sunset dome fully, salmon-pink, with not a line of the
        /// lighting path changed. A saturated pigment not taking a coloured light is correct physics — it is
        /// simply not the picture this scene wants, so the pigment is what has to move.
        /// </para>
        /// <para>
        /// Brightness is preserved deliberately: the shift rescales the dome's tint to the pigment's own
        /// luminance before blending, so a dark dome cannot darken the wood and a bright one cannot light it.
        /// What changes is hue alone, which is what "takes the scene's mood" means and all it should mean.
        /// </para>
        /// </summary>
        private Vector3 ShiftTowardsSky(Vector3 linear)
        {
            float tintLuminance = _skyTint.X * 0.2126f + _skyTint.Y * 0.7152f + _skyTint.Z * 0.0722f;
            if (tintLuminance <= 1e-4f) return linear;

            float luminance = linear.X * 0.2126f + linear.Y * 0.7152f + linear.Z * 0.0722f;
            Vector3 domeHued = _skyTint * (luminance / tintLuminance);

            return Vector3.Lerp(linear, domeHued, SKY_TINT_STRENGTH);
        }

        //Re-encodes every tint table from the authored linear colours. Cheap and rare: six small arrays,
        //twenty-five entries between them, run at build, at a replant and when the dome's hue actually moves.
        private void EncodeAllTints()
        {
            //The trunks of both species share the bark colour; the stumps have a lighter wood of their own,
            //being mostly the pale sawn face. Only the two canopies take per-variant multipliers.
            _coniferTrunkTints = EncodeTints(_barkColorLinear, _coniferMeshes.Length, null);
            _coniferCrownTints = EncodeTints(_coniferColorLinear, _coniferMeshes.Length, CONIFER_TINTS);
            _broadleafTrunkTints = EncodeTints(_barkColorLinear, _broadleafMeshes.Length, null);
            _broadleafCrownTints = EncodeTints(_foliageColorLinear, _broadleafMeshes.Length, BROADLEAF_TINTS);
            _rockTints = EncodeTints(_rockColorLinear, _rockMeshes.Length, null);
            _stumpTints = EncodeTints(_stumpColorLinear, _stumpMeshes.Length, null);
        }

        /// <summary>
        /// Tells the wood which sky it is standing under, so its pigments can take that sky's hue — see
        /// <see cref="ShiftTowardsSky"/> for why the lighting alone cannot do it. Call it from the same place
        /// the sky light rig is applied; the executables all have one.
        /// </summary>
        /// <param name="domeTint">
        /// The dome's tint, in linear radiance — <c>SkyLightRig.KeyTint</c> is what every caller passes, so the
        /// wood shifts by the very tint the sun over it is taking. White means no shift.
        /// </param>
        /// <remarks>
        /// Guarded on the value, because this is called from a per-frame pass (the overcast lerp re-applies the
        /// rig every frame) and re-encoding twenty-five tints every frame for a dome that has not moved is
        /// exactly the kind of per-frame arithmetic the tint cache exists to avoid.
        /// </remarks>
        public void ApplySkyTint(Vector3 domeTint)
        {
            if (domeTint == _skyTint) return;

            //Stored before the guard below rather than after it, so a tint that arrives while there is nothing
            //to encode is remembered rather than dropped — the next Build reads it and the wood comes up
            //already under the right sky. Nothing calls it that early today (Build runs in the constructor);
            //this is the ordering that stays correct if something ever does.
            _skyTint = domeTint;

            if (_coniferMeshes != null) EncodeAllTints();
        }

        //One instanced draw per mesh variant of a scattered kind, each with that variant's own instances and
        //its own already-encoded tint. A plain indexed loop rather than a zip over the arrays: it runs every
        //frame the forest is on screen, and an iterator would allocate one enumerator per kind per frame.
        private void DrawScatter(ICamera camera, InstancedModelRenderer[] renderers, ModelInstance[][] instances,
            BasicEffectParams effectParams, Vector3[] srgbTints)
        {
            for (int variant = 0; variant < renderers.Length; variant++)
            {
                ModelInstance[] bucket = instances[variant];
                if (bucket.Length == 0) continue;   //a variant no instance fell to; Draw would no-op anyway

                renderers[variant].Draw(camera, bucket, bucket.Length, effectParams, srgbTints[variant]);
            }
        }

        //A species at one set of proportions and one structural roll. The two factors scale the crown's width
        //and its height against the config's authored figures; the trunk follows the crown's height, so a
        //taller tree is not a taller crown on the same stump of a trunk.
        private TreeMesh NewConifer(ForestTreeConfig cfg, float width, float height, int seed) =>
            new(_device, TreeSpecies.Conifer,
                trunkBaseRadius: cfg.TrunkBaseRadius * width, trunkTopRadius: cfg.TrunkTopRadius * width,
                trunkHeight: cfg.ConiferTrunkHeight * height,
                crownRadius: cfg.ConiferCrownRadius * width, crownHeight: cfg.ConiferCrownHeight * height,
                seed: seed);

        private TreeMesh NewBroadleaf(ForestTreeConfig cfg, float width, float height, int seed) =>
            new(_device, TreeSpecies.Broadleaf,
                trunkBaseRadius: cfg.TrunkBaseRadius * width, trunkTopRadius: cfg.TrunkTopRadius * width,
                trunkHeight: cfg.TrunkHeight * height,
                crownRadius: cfg.CrownRadius * width, crownHeight: cfg.CrownHeight * height,
                seed: seed);

        //A trunk: bark projected triplanar (a trunk is a static vertical cylinder, so a world-fixed projection
        //stays put), with the grain the bark texture's vertical streaks carry, and a coarse relief so the bark
        //catches the light unevenly. The bark texture is the grain — which is why the sawn-timber surface style
        //this once declined to declare was no loss when #151 deleted it along with the rest of the castle's
        //construction patterns. One per species — the two trunks are different meshes under the same dressing.
        private InstancedModelRenderer NewTrunkRenderer(IProceduralMesh mesh) =>
            new(_device, mesh, SCATTER_MATERIAL_DIFFUSE, _instancingEffect)
            {
                DetailTexture = _barkTexture.Texture,
                DetailTextureMapping = DetailMapping.Triplanar,
                DetailScale = 1f / TRUNK_BARK_SPAN,
                DetailBoost = 1f / _barkTexture.LinearMean,
                DetailStrength = 0.55f,
                SurfaceReliefFrequency = 9f,
                SurfaceReliefStrength = 0.01f,
                CavityStrength = 0.5f,
                SpecularAmbientStrength = 0.08f
            };

        //A canopy: foliage projected triplanar over the crown, lighter relief than the bark (a leaf mass is
        //softer than a trunk) and only a whisper of sky reflection — a canopy scatters what hits it, it does
        //not mirror the dome.
        private InstancedModelRenderer NewCrownRenderer(IProceduralMesh mesh) =>
            new(_device, mesh, SCATTER_MATERIAL_DIFFUSE, _instancingEffect)
            {
                DetailTexture = _foliageTexture.Texture,
                DetailTextureMapping = DetailMapping.Triplanar,
                DetailScale = 1f / CROWN_FOLIAGE_SPAN,
                DetailBoost = 1f / _foliageTexture.LinearMean,
                DetailStrength = 0.5f,
                SurfaceReliefFrequency = 6f,
                SurfaceReliefStrength = 0.006f,
                CavityStrength = 0.4f,
                SpecularAmbientStrength = 0.06f
            };

        //The rocks: the same dressed-stone setup the island's cap uses (the stone texture, the same relief and
        //cavity), only rougher — a forest boulder is weathered, not dressed.
        private InstancedModelRenderer NewRockRenderer(IProceduralMesh mesh) =>
            new(_device, mesh, SCATTER_MATERIAL_DIFFUSE, _instancingEffect)
            {
                DetailTexture = _stoneTexture.Texture,
                DetailTextureMapping = DetailMapping.Triplanar,
                DetailScale = 1f / ROCK_STONE_SPAN,
                DetailBoost = 1f / _stoneTexture.LinearMean,
                DetailStrength = 0.6f,
                SurfaceReliefFrequency = 8f,
                SurfaceReliefStrength = 0.012f,
                CavityStrength = 0.7f,
                SpecularAmbientStrength = 0.12f
            };

        //The stumps: bark, like the trunks, but flatter — a cut surface has no relief to speak of beyond the
        //grain, and the heartwood inset the mesh carves reads off the cavity term.
        private InstancedModelRenderer NewStumpRenderer(IProceduralMesh mesh) =>
            new(_device, mesh, SCATTER_MATERIAL_DIFFUSE, _instancingEffect)
            {
                DetailTexture = _barkTexture.Texture,
                DetailTextureMapping = DetailMapping.Triplanar,
                DetailScale = 1f / STUMP_BARK_SPAN,
                DetailBoost = 1f / _barkTexture.LinearMean,
                DetailStrength = 0.5f,
                SurfaceReliefFrequency = 7f,
                SurfaceReliefStrength = 0.008f,
                CavityStrength = 0.6f,
                SpecularAmbientStrength = 0.08f
            };

        //Everything Build made, and nothing else — so Replant can run it and start over. The renderers go
        //through the one flat list the sky lighting walks, so a variant added later cannot be lit and then
        //leaked. Not the textures and not the effect-params: neither depends on the config.
        private void DisposeBuilt()
        {
            if (_renderers != null)
                foreach (InstancedModelRenderer renderer in _renderers) renderer?.Dispose();

            if (_coniferMeshes != null) foreach (TreeMesh mesh in _coniferMeshes) mesh?.Dispose();
            if (_broadleafMeshes != null) foreach (TreeMesh mesh in _broadleafMeshes) mesh?.Dispose();
            if (_rockMeshes != null) foreach (RockMesh mesh in _rockMeshes) mesh?.Dispose();
            if (_stumpMeshes != null) foreach (StumpMesh mesh in _stumpMeshes) mesh?.Dispose();
        }
    }
}
