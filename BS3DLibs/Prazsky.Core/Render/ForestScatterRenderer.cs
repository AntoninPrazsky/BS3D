using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The forest scatter's rendering hardware: everything the trees, boulders, stumps and dead wood are
    /// drawn <b>with</b>, as <see cref="ForestScatter"/> is everything about <b>where</b> they stand. The two
    /// procedural <see cref="SurfaceTexture"/>s, the twenty mesh variants (six spruces, four broadleaves,
    /// three boulders, two stumps, and since #462 three snags and two fallen logs), the seventy-two
    /// <see cref="InstancedModelRenderer"/>s that dress them as bark, foliage, snow, weathered stone and sawn wood
    /// (forty-two of them High and Ultra's spruce crowns, #733, and their stems, #960), the three matte
    /// <see cref="BasicEffectParams"/>,
    /// the per-variant tints and the instanced draws.
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
    /// <c>GetSceneConfig</c> plus a <c>ToVector3</c> per kind and a <see cref="ColorSpace.LinearToSrgb(Vector3)"/> per
    /// variant — for values nothing can change once the wood is built. So the config is read at construction
    /// and nowhere else: a scene's config is fixed in code, and the one thing that used to edit one at runtime
    /// (the map editor's live panel, which this component grew a <c>Replant</c> for) went in #522.
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

        /// <summary>
        /// One of the spruce variants of <see cref="CONIFER_VARIANTS"/>: its width and height as factors of the config's
        /// conifer figures, the structural roll its <see cref="TreeMesh"/> is built with, and whether it stands on the
        /// long bare stem of a stand's crowded trees (<see cref="ForestTreeConfig.ConiferStandTrunk"/>).
        /// </summary>
        public readonly record struct SpruceVariant(float Width, float Height, int Seed, bool Stand);

        /// <summary>
        /// The six spruces every forest is planted with, in this order (the planting splits the conifers between them by
        /// index). The one copy of the table: the Potato path's stand (<c>PotatoBackdrop.SpruceProxies</c>) stands its
        /// cones in these proportions, so a change here moves both woods. Each variant is FOR a silhouette, not a size.
        /// </summary>
        public static readonly SpruceVariant[] CONIFER_VARIANTS =
        {
            new(1f, 1f, 11, false),          //the authored spruce
            new(0.78f, 1.24f, 23, true),     //a narrow, taller one — the crowded stems of a stand, bare low down
            new(1.3f, 0.76f, 37, false),     //a broad, squat one — an old tree with room around it
            new(0.9f, 1.1f, 41, false),      //an ordinary tree that is nobody's copy
            new(1.12f, 0.94f, 53, false),    //slightly stout
            new(0.7f, 1.38f, 67, true),      //a spire — the one that carries the skyline, on a long bare stem
        };

        /// <summary>How many broadleaf meshes every planting is split between (<see cref="Plant"/>).</summary>
        public const int BROADLEAF_VARIANTS = 4;

        /// <summary>
        /// The three boulders' proportions against <see cref="ForestRockConfig.Radius"/> and <c>Height</c> (X radius, Y
        /// height), one copy: the meshes are built from it, and the aurora's drifts round them are sized by it (#963).
        /// </summary>
        public static readonly Vector2[] ROCK_SHAPES = { new(1f, 1f), new(1.25f, 0.7f), new(0.8f, 1.5f) };

        /// <summary>The two stumps' proportions against <see cref="ForestStumpConfig.Radius"/> and <c>Height</c> (X radius, Y height).</summary>
        public static readonly Vector2[] STUMP_SHAPES = { new(1f, 1f), new(1.3f, 0.55f) };

        /// <summary>The three snags' proportions against <see cref="ForestDeadwoodConfig.Length"/> and <c>Radius</c> (X length, Y radius).</summary>
        public static readonly Vector2[] SNAG_SHAPES = { new(1f, 1f), new(1.3f, 1.1f), new(0.65f, 0.95f) };

        /// <summary>The two logs' proportions against <see cref="ForestDeadwoodConfig.Length"/> and <c>Radius</c> (X length, Y radius).</summary>
        public static readonly Vector2[] LOG_SHAPES = { new(1f, 1f), new(0.7f, 0.85f) };

        /// <summary>
        /// Where a wood of <paramref name="config"/> stands, planted on <paramref name="terrainHeight"/> with every kind
        /// split between as many variants as the renderer builds meshes for: the one planting call, so a planting made
        /// ahead of the renderer (the aurora's, which its snow is drifted round, #963) is the one the renderer draws.
        /// </summary>
        public static ForestScatter Plant(int seed, ForestSceneConfig config, Func<float, float, float> terrainHeight) =>
            new(seed, config, CONIFER_VARIANTS.Length, BROADLEAF_VARIANTS, ROCK_SHAPES.Length, STUMP_SHAPES.Length,
                SNAG_SHAPES.Length, LOG_SHAPES.Length, terrainHeight);

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

        /// <summary>
        /// How dark the interior of High and Ultra's spruce crown (<see cref="TreeMesh.FineShade"/>, #733) is against its
        /// boughs, as a share of the needles' linear colour, and its stem (<see cref="TreeMesh.FineStem"/>, #960) against
        /// the trunk's bark. The shader has no occlusion term a mesh could feed, so the old needles and the stem inside the
        /// crown, shaded by every bough above them, take it as pigment; and no snow, which the boughs over them catch.
        /// </summary>
        private const float SPRUCE_SHADE = 0.55f;

        /// <summary>
        /// How far from the arena's axis a spruce wears the finer of High and Ultra's two crowns (<see cref="TreeMesh.FineCrown"/>,
        /// #733); past it the lighter one (<see cref="TreeMesh.FineCrownFar"/>). Every host keeps its camera on or near the
        /// island, so a tree out here is never large on the screen, and most of a wood of eight hundred stands out here.
        /// </summary>
        public const float FINE_NEAR_RADIUS = 110f;


        //Everything below is derived from the config, built by Build out of the constructor (so none of it can
        //be readonly) and released by DisposeBuilt. The mesh variants: a bark lathe under a lathed tiered cone
        //for a spruce or a lobed sphere for a broadleaf, plus a rock lathe and a stump lathe.
        private TreeMesh[] _coniferMeshes, _broadleafMeshes;
        private RockMesh[] _rockMeshes;
        private StumpMesh[] _stumpMeshes;

        //The dead wood (#462): standing snags and fallen logs, bark-dressed like the stumps. Built for every
        //planting, the daytime forest's included, whose config plants none of either — two small tube meshes
        //each and five renderers that never draw (DrawScatter skips an empty bucket), against a second code
        //path for a wood without them.
        private SnagMesh[] _snagMeshes;
        private DeadwoodMesh[] _logMeshes;

        //A trunk and a crown are two renderers because they are two materials — bark and foliage — and the
        //diffuse tint is a per-draw uniform, not per-instance. Every variant is its own instanced draw, which
        //is why these are arrays rather than single renderers.
        private InstancedModelRenderer[] _coniferTrunkRenderers, _coniferCrownRenderers;

        //High and Ultra's spruce crowns (#733): the boughs and the darker interior round them, drawn instead of the lathe
        //above off the same instances when the caller asks for the finer meshes (see Draw), and the bark stem they grow out
        //of, one for both details (#960)
        private InstancedModelRenderer[] _coniferFineCrownRenderers, _coniferFineShadeRenderers, _coniferFineSnowRenderers;
        private InstancedModelRenderer[] _coniferFineCrownFarRenderers, _coniferFineSnowFarRenderers, _coniferFineShadeFarRenderers;
        private InstancedModelRenderer[] _coniferFineStemRenderers;

        //The spruces split by how far they stand from the arena (FINE_NEAR_RADIUS), per variant: the near ones wear the
        //finer of High and Ultra's two crowns, the rest the lighter one (#733)
        private ModelInstance[][] _coniferNear, _coniferFar;
        private InstancedModelRenderer[] _broadleafTrunkRenderers, _broadleafCrownRenderers;
        private InstancedModelRenderer[] _rockRenderers, _stumpRenderers;
        private InstancedModelRenderer[] _snagRenderers, _logRenderers;

        //The flat list of all seventy-two, in draw order. See Renderers.
        private InstancedModelRenderer[] _renderers;

        //One already-encoded sRGB tint per mesh variant per draw — the whole point of the cache. Eleven arrays
        //rather than a few colours and multiplier tables, so the draw is one uniform path with no branch and
        //no arithmetic in it (see EncodeTints, which holds the colour-space reasoning).
        private Vector3[] _coniferTrunkTints, _coniferCrownTints, _coniferShadeTints, _coniferSnowTints, _coniferStemTints;
        private Vector3[] _broadleafTrunkTints, _broadleafCrownTints;
        private Vector3[] _rockTints, _stumpTints;
        private Vector3[] _snagTints, _logTints;

        //The dome hue the tints above were last encoded against, so re-encoding is skipped on every frame the
        //dome has not moved — which is nearly all of them, since ApplySkyTint is called from the same
        //per-frame pass the overcast lerp drives. Starts white, which is "no shift" and matches a caller that
        //never calls it at all.
        private Vector3 _skyTint = Vector3.One;

        //The authored linear colours, kept because the encoded tints above can no longer be re-derived from
        //themselves: the sky shift is applied on the way in, so re-encoding needs the originals.
        private Vector3 _barkColorLinear, _coniferColorLinear, _foliageColorLinear, _rockColorLinear, _stumpColorLinear;
        private Vector3 _snagColorLinear, _logColorLinear;

        //Snow lying on the crowns (#647): its linear colour and how much of an up-facing skirt it covers, 0 none; and on
        //the tops of the boulders, the stumps and the dead wood (#963), in the same colour, 0 everywhere but the aurora
        private Vector3 _crownSnowLinear;
        private float _crownSnow;
        private float _rockSnow, _stumpSnow, _snagSnow, _logSnow;

        //The share of the crown snow High and Ultra's boughs take (ForestTreeConfig.BoughFrost)
        private float _boughFrost;

        private ForestScatter _scatter;

        /// <summary>
        /// Builds the whole wood here and now: both procedural textures, the twenty mesh variants, the seventy-two
        /// renderers with their per-material dressing, the encoded tints and the scatter itself. There is
        /// nothing runtime about any of it — the scatter is a fixed default forest, as the city is a fixed
        /// default city — so a caller with a static config builds it once at load and never touches it again.
        /// <para>
        /// <b>The caller must run its own sky lighting after this</b>: a fresh
        /// <see cref="InstancedModelRenderer"/> has never been told the dome's
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
        /// Read here only; the instance stays the caller's (it is the
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
        /// <param name="seed">Scatter seed; the same seed always plants the same forest.</param>
        /// <param name="planting">A planting made ahead (<see cref="Plant"/>), drawn instead of one of this renderer's own:
        /// the aurora's (<c>SceneRenderer.AuroraPlanting</c>), which the backdrop drifts its snow round (#963). Null plants
        /// <paramref name="config"/> on <c>TerrainMirror.Forest</c> with <paramref name="seed"/>, as every other wood is.</param>
        public ForestScatterRenderer(GraphicsDevice device, Effect instancingEffect, ForestSceneConfig config,
            float sceneAmbientIntensity, SurfaceTexture stoneTexture = null, int seed = DEFAULT_SEED, ForestScatter planting = null)
        {
            _device = device;
            _instancingEffect = instancingEffect;
            _seed = seed;
            _scatter = planting;

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
        /// </summary>
        public InstancedModelRenderer[] Renderers => _renderers;

        /// <summary>
        /// Where everything stands — the planting the renderers draw — for a host laying a camera path through
        /// the wood (the aurora's chapter intro threads its spruces, #531). Read-only by construction: the
        /// arrays are the instance buffers' own sources, and a caller that wrote into them would move a tree
        /// the buffer has already been filled from.
        /// </summary>
        public ForestScatter Scatter => _scatter;

        /// <summary>The conifer mesh variants, indexed as <see cref="ForestScatter.Conifers"/>' buckets are — how
        /// wide and tall each variant is, which the instance matrix alone does not say (#559).</summary>
        public IReadOnlyList<TreeMesh> ConiferMeshes => _coniferMeshes;

        /// <summary>The broadleaf mesh variants, indexed as <see cref="ForestScatter.Broadleaves"/>' buckets are.</summary>
        public IReadOnlyList<TreeMesh> BroadleafMeshes => _broadleafMeshes;

        /// <summary>
        /// The instanced draws, in order: conifer trunks, conifer crowns, broadleaf trunks, broadleaf
        /// crowns, boulders, stumps, snags, logs — one draw per kind per mesh variant, and per material within a tree. On
        /// High and Ultra a conifer's crown is seven draws instead of one (#733): the near and the far boughs, their snow, the
        /// two interiors, drawn after the boughs that hide most of them, and the bark stem they all grow out of (#960). A
        /// species' trunk and crown read that variant's own scatter of world matrices (the crown mesh is built
        /// sitting on its trunk's top, so one position places both) and are two draws because their tints
        /// differ — bark and foliage — and the diffuse tint is a per-draw uniform.
        /// <para>
        /// <b>Touches no GPU state, on purpose</b>, and needs none of its own: every mesh here is a closed
        /// solid wound clockwise seen from outside (the convention in CLAUDE.md) — High and Ultra's boughs are open only
        /// at their roots, which stand inside the stem — so the scene's ordinary
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
        /// <param name="fineMeshes">Whether the spruces wear High and Ultra's crown (#733, <see cref="TreeMesh.FineCrown"/>
        /// over <see cref="TreeMesh.FineShade"/> round <see cref="TreeMesh.FineStem"/>, and <see cref="TreeMesh.FineSnow"/>
        /// where the config lays snow on the crowns) or Low and Medium's lathe: the caller's <c>SceneRenderer.FineMeshes</c>,
        /// read per draw, so a tier change rebuilds nothing.</param>
        public void Draw(ICamera camera, bool fineMeshes)
        {
            DrawScatter(camera, _coniferTrunkRenderers, _scatter.Conifers, _barkEffectParams, _coniferTrunkTints);
            if (fineMeshes)
            {
                //The boughs before the interior they hide, so the depth test turns most of its pixels away unshaded
                DrawScatter(camera, _coniferFineCrownRenderers, _coniferNear, _foliageEffectParams, _coniferCrownTints);
                DrawScatter(camera, _coniferFineCrownFarRenderers, _coniferFar, _foliageEffectParams, _coniferCrownTints);
                if (_crownSnow > 0f)
                {
                    DrawScatter(camera, _coniferFineSnowRenderers, _coniferNear, _foliageEffectParams, _coniferSnowTints);
                    DrawScatter(camera, _coniferFineSnowFarRenderers, _coniferFar, _foliageEffectParams, _coniferSnowTints);
                }
                DrawScatter(camera, _coniferFineShadeRenderers, _coniferNear, _foliageEffectParams, _coniferShadeTints);
                DrawScatter(camera, _coniferFineShadeFarRenderers, _coniferFar, _foliageEffectParams, _coniferShadeTints);
                DrawScatter(camera, _coniferFineStemRenderers, _scatter.Conifers, _barkEffectParams, _coniferStemTints);
            }
            else DrawScatter(camera, _coniferCrownRenderers, _scatter.Conifers, _foliageEffectParams, _coniferCrownTints);
            DrawScatter(camera, _broadleafTrunkRenderers, _scatter.Broadleaves, _barkEffectParams, _broadleafTrunkTints);
            DrawScatter(camera, _broadleafCrownRenderers, _scatter.Broadleaves, _foliageEffectParams, _broadleafCrownTints);
            DrawScatter(camera, _rockRenderers, _scatter.Rocks, _rockEffectParams, _rockTints);
            DrawScatter(camera, _stumpRenderers, _scatter.Stumps, _barkEffectParams, _stumpTints);
            DrawScatter(camera, _snagRenderers, _scatter.Snags, _barkEffectParams, _snagTints);
            DrawScatter(camera, _logRenderers, _scatter.Logs, _barkEffectParams, _logTints);
        }

        /// <summary>
        /// The same eight kinds drawn again from the sun, into the shadow map <c>SceneRenderer.DrawShadowMaps</c>
        /// has bound (#471): depth only, no material and no light, through
        /// <see cref="InstancedModelRenderer.DrawDepth(Matrix, ModelInstance[], int)"/> and <c>InstancedModel.fx</c>'s <c>InstancedDepth</c>
        /// technique. The densest scatter in the project is the one whose shadow matters most — a wood where
        /// every trunk stands on evenly lit floor is what #469 was opened about, in another scene.
        /// <para>
        /// <b>Trunks and crowns are one draw each here, not two.</b> The drawn pass splits them because their
        /// TINTS differ (bark against foliage, a per-draw uniform); a depth pass has no tint, so the crown's
        /// and the trunk's meshes are simply two more sets of geometry over the same instances. It stays eight
        /// calls per variant rather than sixteen for that reason.
        /// </para>
        /// <para>
        /// <b>The caller states everything</b>: the map bound, cleared, opaque, depth-default and
        /// <see cref="RasterizerState.CullNone"/> — see <c>DrawShadowMaps</c>. Like <see cref="Draw"/> this
        /// touches no state of its own, allocates nothing, and leaves the renderers on their main technique
        /// (<c>DrawDepth</c> restores it). And like it, the scene gate is the caller's: this casts the wood
        /// whenever it is called.
        /// </para>
        /// </summary>
        /// <param name="shadowViewProjection">World → the map's clip space, from
        /// <c>SunShadowMap.ViewProjection</c>.</param>
        /// <param name="fineMeshes">Which spruce crown casts: the one <see cref="Draw"/> draws, or the lathe's skirts would
        /// shade the boughs inside them (#733).</param>
        public void DrawShadow(Matrix shadowViewProjection, bool fineMeshes)
        {
            CastScatter(shadowViewProjection, _coniferTrunkRenderers, _scatter.Conifers);
            if (fineMeshes)
            {
                CastScatter(shadowViewProjection, _coniferFineShadeRenderers, _coniferNear);
                CastScatter(shadowViewProjection, _coniferFineShadeFarRenderers, _coniferFar);
                CastScatter(shadowViewProjection, _coniferFineCrownRenderers, _coniferNear);
                CastScatter(shadowViewProjection, _coniferFineCrownFarRenderers, _coniferFar);
                CastScatter(shadowViewProjection, _coniferFineStemRenderers, _scatter.Conifers);
                if (_crownSnow > 0f)
                {
                    CastScatter(shadowViewProjection, _coniferFineSnowRenderers, _coniferNear);
                    CastScatter(shadowViewProjection, _coniferFineSnowFarRenderers, _coniferFar);
                }
            }
            else CastScatter(shadowViewProjection, _coniferCrownRenderers, _scatter.Conifers);
            CastScatter(shadowViewProjection, _broadleafTrunkRenderers, _scatter.Broadleaves);
            CastScatter(shadowViewProjection, _broadleafCrownRenderers, _scatter.Broadleaves);
            CastScatter(shadowViewProjection, _rockRenderers, _scatter.Rocks);
            CastScatter(shadowViewProjection, _stumpRenderers, _scatter.Stumps);
            CastScatter(shadowViewProjection, _snagRenderers, _scatter.Snags);
            CastScatter(shadowViewProjection, _logRenderers, _scatter.Logs);
        }

        /// <summary>
        /// Everything this component made: the twenty mesh variants, all seventy-two renderers (each holding a
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

        //Everything the config decides, built in one place — the constructor's whole job beyond keeping what
        //it was handed.
        private void Build(ForestSceneConfig config)
        {
            ForestTreeConfig trees = config.Trees;

            //Each kind at several sets of proportions and its own structural roll — the reasoning is in the
            //class remarks, and it is the whole reason a grove does not read as one tree stamped out fifty
            //times. The spruces' table (CONIFER_VARIANTS) names what each variant is FOR: the silhouettes have to
            //differ, not merely the sizes, and a set of variants that all read as the same tree is a set that has drifted.
            _coniferMeshes = new TreeMesh[CONIFER_VARIANTS.Length];
            for (int v = 0; v < CONIFER_VARIANTS.Length; v++)
            {
                SpruceVariant variant = CONIFER_VARIANTS[v];
                _coniferMeshes[v] = NewConifer(trees, variant.Width, variant.Height, variant.Seed, variant.Stand ? trees.ConiferStandTrunk : 1f);
            }

            _broadleafMeshes = new[]
            {
                NewBroadleaf(trees, 1f, 1f, seed: 79),        //the authored broadleaf
                NewBroadleaf(trees, 0.82f, 1.3f, seed: 83),   //narrower and taller, its crown carried higher
                NewBroadleaf(trees, 1.22f, 0.85f, seed: 97),  //broad and low — an old oak's spread
                NewBroadleaf(trees, 0.95f, 1.12f, seed: 101)  //an ordinary tree between them
            };

            _rockMeshes = new[]
            {
                new RockMesh(_device, config.Rocks.Radius * ROCK_SHAPES[0].X, config.Rocks.Height * ROCK_SHAPES[0].Y),
                new RockMesh(_device, config.Rocks.Radius * ROCK_SHAPES[1].X, config.Rocks.Height * ROCK_SHAPES[1].Y, irregularityPhase: 2.1f),
                new RockMesh(_device, config.Rocks.Radius * ROCK_SHAPES[2].X, config.Rocks.Height * ROCK_SHAPES[2].Y, irregularityPhase: 4.2f)
            };

            _stumpMeshes = new[]
            {
                new StumpMesh(_device, config.Stumps.Radius * STUMP_SHAPES[0].X, config.Stumps.Height * STUMP_SHAPES[0].Y),
                new StumpMesh(_device, config.Stumps.Radius * STUMP_SHAPES[1].X, config.Stumps.Height * STUMP_SHAPES[1].Y, irregularityPhase: 3.3f)
            };

            //Three snags and two logs: a snag is one bent trunk and its stubs, and it is the stubs' layout and
            //the lean the scatter gives it that tell one from another, so fewer variants than the spruces need.
            ForestDeadwoodConfig snags = config.Snags, logs = config.Logs;
            _snagMeshes = new[]
            {
                new SnagMesh(_device, snags.Length * SNAG_SHAPES[0].X, snags.Radius * SNAG_SHAPES[0].Y, seed: 131),
                new SnagMesh(_device, snags.Length * SNAG_SHAPES[1].X, snags.Radius * SNAG_SHAPES[1].Y, seed: 137),   //a tall one, broken high
                new SnagMesh(_device, snags.Length * SNAG_SHAPES[2].X, snags.Radius * SNAG_SHAPES[2].Y, seed: 139)  //broken low - most of it gone
            };

            _logMeshes = new[]
            {
                new DeadwoodMesh(_device, logs.Length * LOG_SHAPES[0].X, logs.Radius * LOG_SHAPES[0].Y, seed: 149),
                new DeadwoodMesh(_device, logs.Length * LOG_SHAPES[1].X, logs.Radius * LOG_SHAPES[1].Y, seed: 151)
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
            _coniferFineCrownRenderers = Array.ConvertAll(_coniferMeshes, m => NewBoughRenderer(m.FineCrown));
            _coniferFineShadeRenderers = Array.ConvertAll(_coniferMeshes, m => NewCrownRenderer(m.FineShade));
            _coniferFineSnowRenderers = Array.ConvertAll(_coniferMeshes, m => NewSnowRenderer(m.FineSnow));
            _coniferFineCrownFarRenderers = Array.ConvertAll(_coniferMeshes, m => NewBoughRenderer(m.FineCrownFar));
            _coniferFineSnowFarRenderers = Array.ConvertAll(_coniferMeshes, m => NewSnowRenderer(m.FineSnowFar));
            _coniferFineShadeFarRenderers = Array.ConvertAll(_coniferMeshes, m => NewCrownRenderer(m.FineShadeFar));
            _coniferFineStemRenderers = Array.ConvertAll(_coniferMeshes, m => NewTrunkRenderer(m.FineStem));
            _broadleafTrunkRenderers = Array.ConvertAll(_broadleafMeshes, m => NewTrunkRenderer(m.Trunk));
            _broadleafCrownRenderers = Array.ConvertAll(_broadleafMeshes, m => NewCrownRenderer(m.Crown));
            _rockRenderers = Array.ConvertAll(_rockMeshes, m => NewRockRenderer(m));
            _stumpRenderers = Array.ConvertAll(_stumpMeshes, m => NewStumpRenderer(m));
            _snagRenderers = Array.ConvertAll(_snagMeshes, m => NewStumpRenderer(m));
            _logRenderers = Array.ConvertAll(_logMeshes, m => NewStumpRenderer(m));

            //The one flat list, in draw order (see Renderers). Built from the eight arrays rather than filled
            //alongside them, so a kind added later joins the sky lighting and the disposal by being added here
            //once instead of in three places.
            _renderers = new InstancedModelRenderer[
                _coniferTrunkRenderers.Length + _coniferCrownRenderers.Length +
                _coniferFineCrownRenderers.Length + _coniferFineShadeRenderers.Length + _coniferFineSnowRenderers.Length +
                _coniferFineCrownFarRenderers.Length + _coniferFineSnowFarRenderers.Length + _coniferFineShadeFarRenderers.Length +
                _coniferFineStemRenderers.Length +
                _broadleafTrunkRenderers.Length + _broadleafCrownRenderers.Length +
                _rockRenderers.Length + _stumpRenderers.Length + _snagRenderers.Length + _logRenderers.Length];

            int next = 0;
            next = CopyInto(_coniferTrunkRenderers, next);
            next = CopyInto(_coniferCrownRenderers, next);
            next = CopyInto(_coniferFineCrownRenderers, next);
            next = CopyInto(_coniferFineShadeRenderers, next);
            next = CopyInto(_coniferFineSnowRenderers, next);
            next = CopyInto(_coniferFineCrownFarRenderers, next);
            next = CopyInto(_coniferFineSnowFarRenderers, next);
            next = CopyInto(_coniferFineShadeFarRenderers, next);
            next = CopyInto(_coniferFineStemRenderers, next);
            next = CopyInto(_broadleafTrunkRenderers, next);
            next = CopyInto(_broadleafCrownRenderers, next);
            next = CopyInto(_rockRenderers, next);
            next = CopyInto(_stumpRenderers, next);
            next = CopyInto(_snagRenderers, next);
            CopyInto(_logRenderers, next);

            //The trunks of both species share the bark colour; the stumps have a lighter wood of their own,
            //being mostly the pale sawn face. Only the two canopies take per-variant multipliers.
            _barkColorLinear = trees.TrunkColor.ToVector3();
            _coniferColorLinear = trees.ConiferColor.ToVector3();
            _foliageColorLinear = trees.FoliageColor.ToVector3();
            _rockColorLinear = config.Rocks.Color.ToVector3();
            _stumpColorLinear = config.Stumps.Color.ToVector3();
            _snagColorLinear = snags.Color.ToVector3();
            _logColorLinear = logs.Color.ToVector3();
            _crownSnowLinear = trees.CrownSnowColor.ToVector3();
            _crownSnow = trees.CrownSnow;
            _boughFrost = trees.BoughFrost;
            _rockSnow = config.Rocks.TopSnow;
            _stumpSnow = config.Stumps.TopSnow;
            _snagSnow = snags.TopSnow;
            _logSnow = logs.TopSnow;

            EncodeAllTints();

            //Planted on the forest floor the terrain shader draws: TerrainMirror.Forest mirrors Forest.fx's
            //height field on the CPU, so a tree's base sits on the ground the player sees. The scatter is told
            //how many variants each kind has so it can hand back one instance array per variant — the renderer
            //draws a contiguous prefix of what it is given, so the split has to happen at plant time rather
            //than at the draw.
            _scatter ??= Plant(_seed, config, (x, z) => TerrainMirror.Forest(x, z, config));

            //High and Ultra's two spruce crowns stand by distance from the arena, decided here once: a tree never changes
            //crown as the camera moves, and the near crown is spent where a tree can be large on the screen (#733)
            SplitByDistance(_scatter.Conifers, FINE_NEAR_RADIUS, out _coniferNear, out _coniferFar);
        }

        //Each variant's instances into those standing within `radius` of the arena's axis and the rest, in their order
        private static void SplitByDistance(ModelInstance[][] variants, float radius, out ModelInstance[][] near,
            out ModelInstance[][] far)
        {
            near = new ModelInstance[variants.Length][];
            far = new ModelInstance[variants.Length][];
            for (int v = 0; v < variants.Length; v++)
            {
                var inside = new List<ModelInstance>();
                var outside = new List<ModelInstance>();
                foreach (ModelInstance instance in variants[v])
                {
                    Vector3 at = instance.World.Translation;
                    (at.X * at.X + at.Z * at.Z < radius * radius ? inside : outside).Add(instance);
                }
                near[v] = inside.ToArray();
                far[v] = outside.ToArray();
            }
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
        /// <param name="tints">The table, written in place once it exists: the aurora's glow drifts every frame and
        /// <see cref="ApplySkyTint"/> re-encodes on every move, so a fresh table each time was garbage every frame.</param>
        private void EncodeTints(Vector3 linearColor, int variants, Vector3[] variantTints, ref Vector3[] tints)
        {
            if (tints == null || tints.Length != variants) tints = new Vector3[variants];

            for (int variant = 0; variant < variants; variant++)
            {
                Vector3 linear = variantTints == null ? linearColor : linearColor * variantTints[variant];
                tints[variant] = ColorSpace.LinearToSrgb(ShiftTowardsSky(linear));
            }
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
            float tintLuminance = ColorSpace.Luminance(_skyTint);
            if (tintLuminance <= 1e-4f) return linear;

            Vector3 domeHued = _skyTint * (ColorSpace.Luminance(linear) / tintLuminance);

            return Vector3.Lerp(linear, domeHued, SKY_TINT_STRENGTH);
        }

        //Re-encodes every tint table from the authored linear colours, in place: eleven small arrays, forty-eight entries
        //between them, run at build and whenever the dome's hue moves — which under the aurora's drifting glow is every
        //frame, so nothing here may allocate once the tables exist.
        private void EncodeAllTints()
        {
            //The trunks of both species share the bark colour; the stumps have a lighter wood of their own,
            //being mostly the pale sawn face. Only the two canopies take per-variant multipliers.
            EncodeTints(_barkColorLinear, _coniferMeshes.Length, null, ref _coniferTrunkTints);
            EncodeTints(_coniferColorLinear, _coniferMeshes.Length, CONIFER_TINTS, ref _coniferCrownTints);
            EncodeTints(_coniferColorLinear * SPRUCE_SHADE, _coniferMeshes.Length, CONIFER_TINTS, ref _coniferShadeTints);
            //High and Ultra's stem (#960) is the bark under every bough above it, shaded as the interior round it is
            EncodeTints(_barkColorLinear * SPRUCE_SHADE, _coniferMeshes.Length, null, ref _coniferStemTints);
            //The boughs' lumps of snow are the snow's own colour, unshifted: what the lathe's dust term arrives at too
            _coniferSnowTints ??= new Vector3[_coniferMeshes.Length];
            Array.Fill(_coniferSnowTints, ColorSpace.LinearToSrgb(_crownSnowLinear));
            EncodeTints(_barkColorLinear, _broadleafMeshes.Length, null, ref _broadleafTrunkTints);
            EncodeTints(_foliageColorLinear, _broadleafMeshes.Length, BROADLEAF_TINTS, ref _broadleafCrownTints);
            EncodeTints(_rockColorLinear, _rockMeshes.Length, null, ref _rockTints);
            EncodeTints(_stumpColorLinear, _stumpMeshes.Length, null, ref _stumpTints);
            EncodeTints(_snagColorLinear, _snagMeshes.Length, null, ref _snagTints);
            EncodeTints(_logColorLinear, _logMeshes.Length, null, ref _logTints);

            LaySnow(_coniferCrownRenderers, _coniferCrownTints, _crownSnow);
            LaySnow(_coniferFineCrownRenderers, _coniferCrownTints, _crownSnow * _boughFrost);
            LaySnow(_coniferFineCrownFarRenderers, _coniferCrownTints, _crownSnow * _boughFrost);
            LaySnow(_broadleafCrownRenderers, _broadleafCrownTints, _crownSnow);
            LaySnow(_rockRenderers, _rockTints, _rockSnow);
            LaySnow(_stumpRenderers, _stumpTints, _stumpSnow);
            LaySnow(_snagRenderers, _snagTints, _snagSnow);
            LaySnow(_logRenderers, _logTints, _logSnow);
        }

        /// <summary>
        /// Snow lying on the crowns (#647), through the triplanar path's own settled-dust term (#535) rather than a new
        /// one: the dust is already a modulation of the albedo on the up-facing faces by the geometric normal, and the
        /// modulation that turns a crown's needles into the snow's colour is the ratio of the two — per variant, since
        /// each variant's pigment is its own, and re-worked whenever the pigments are (the sky's shift moves them). No
        /// shader change, so the island and every other triplanar surface keep their registers and their cost. On a
        /// spruce the up-facing faces are the whorls' drooping tops, so the snow lies along each skirt and the tucks
        /// under them stay dark — the references' snow-laden branches.
        /// <para>
        /// High and Ultra's boughs take only <see cref="ForestTreeConfig.BoughFrost"/> of it (#733): their tops are flat plates, and the
        /// term's <c>up²</c> falloff is too soft to leave anything of a flat plate's needles, so at the lathe's strength a
        /// whole wood of boughs came out snow-pale and lost the near-black spruces #462 put on the snow. Their snow lies in
        /// lumps of geometry instead (<see cref="TreeMesh.FineSnow"/>), over a frost.
        /// </para>
        /// <para>
        /// The boulders, the stumps and the dead wood take it too where the config lays any (<c>TopSnow</c>, #963): the
        /// aurora's logs lie under a cap of snow and its stones are snow-covered humps. 0 on every other wood.
        /// </para>
        /// </summary>
        private void LaySnow(InstancedModelRenderer[] renderers, Vector3[] srgbTints, float strength)
        {
            if (renderers == null) return;

            for (int variant = 0; variant < renderers.Length; variant++)
            {
                if (strength <= 0f)
                {
                    renderers[variant].TopDustStrength = 0f;
                    continue;
                }

                Vector3 pigment = ColorSpace.SrgbToLinear(srgbTints[variant]);
                renderers[variant].TopDustTint = _crownSnowLinear / Vector3.Max(pigment, new Vector3(1e-4f));
                renderers[variant].TopDustStrength = strength;
            }
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
        /// rig every frame) and re-encoding forty-two tints every frame for a dome that has not moved is
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

        //DrawScatter's depth twin (#471): the same per-variant loop with no material to push. A second small
        //method rather than a flag on DrawScatter, because the two share only the loop - the drawn one needs a
        //camera and a tint, this one needs neither and cannot be given them.
        private void CastScatter(Matrix shadowViewProjection, InstancedModelRenderer[] renderers,
            ModelInstance[][] instances)
        {
            for (int variant = 0; variant < renderers.Length; variant++)
            {
                ModelInstance[] bucket = instances[variant];
                if (bucket.Length == 0) continue;

                renderers[variant].DrawDepth(shadowViewProjection, bucket, bucket.Length);
            }
        }

        //A species at one set of proportions and one structural roll. The two factors scale the crown's width
        //and its height against the config's authored figures; the trunk follows the crown's height, so a
        //taller tree is not a taller crown on the same stump of a trunk. `trunk` lengthens the bare stem under the
        //crown on its own (#647): the stand's crowded stems have shed their lower branches, the edge's have not.
        private TreeMesh NewConifer(ForestTreeConfig cfg, float width, float height, int seed, float trunk = 1f) =>
            new(_device, TreeSpecies.Conifer,
                trunkBaseRadius: cfg.TrunkBaseRadius * width, trunkTopRadius: cfg.TrunkTopRadius * width,
                trunkHeight: cfg.ConiferTrunkHeight * height * trunk,
                crownRadius: cfg.ConiferCrownRadius * width, crownHeight: cfg.ConiferCrownHeight * height,
                seed: seed, coniferTiers: cfg.ConiferTiers, coniferTierSpread: cfg.ConiferTierSpread,
                coniferRaggedness: cfg.ConiferRaggedness);

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
                DetailScale = 1f / CROWN_FOLIAGE_SPAN,
                DetailBoost = 1f / _foliageTexture.LinearMean,
                DetailStrength = 0.5f,
                SurfaceReliefFrequency = 6f,
                SurfaceReliefStrength = 0.006f,
                CavityStrength = 0.4f,
                SpecularAmbientStrength = 0.06f
            };

        //High and Ultra's spruce boughs (#733): the canopy's dressing with a needle-scale grain. A bough is a few broad flat
        //faces, and at the canopy's relief (a swing a unit and a half long, a whisper deep) they read as smooth green leaves;
        //a relief a quarter of a unit long and deep enough to tilt the normal, with the cavity term darkening its pits, breaks
        //each face into the bristle of the references' needles. The relief is band-limited, so it fades out on a far tree
        //instead of shimmering.
        private InstancedModelRenderer NewBoughRenderer(IProceduralMesh mesh) =>
            new(_device, mesh, SCATTER_MATERIAL_DIFFUSE, _instancingEffect)
            {
                DetailTexture = _foliageTexture.Texture,
                DetailScale = 1f / CROWN_FOLIAGE_SPAN,
                DetailBoost = 1f / _foliageTexture.LinearMean,
                DetailStrength = 0.5f,
                SurfaceReliefFrequency = 24f,
                SurfaceReliefStrength = 0.012f,
                CavityStrength = 0.6f,
                SpecularAmbientStrength = 0.06f
            };

        //The snow lying in lumps on High and Ultra's spruce boughs (#733): the canopy's dressing, smoothed — packed snow
        //keeps only a hint of the texture's mottle and almost none of its relief. The texture stays because it is what
        //selects the triplanar technique the rest of the wood is lit through.
        private InstancedModelRenderer NewSnowRenderer(IProceduralMesh mesh) =>
            new(_device, mesh, SCATTER_MATERIAL_DIFFUSE, _instancingEffect)
            {
                DetailTexture = _foliageTexture.Texture,
                DetailScale = 1f / CROWN_FOLIAGE_SPAN,
                DetailBoost = 1f / _foliageTexture.LinearMean,
                DetailStrength = 0.15f,
                SurfaceReliefFrequency = 6f,
                SurfaceReliefStrength = 0.002f,
                CavityStrength = 0.2f,
                SpecularAmbientStrength = 0.1f
            };

        //The rocks: the same dressed-stone setup the island's cap uses (the stone texture, the same relief and
        //cavity), only rougher — a forest boulder is weathered, not dressed.
        private InstancedModelRenderer NewRockRenderer(IProceduralMesh mesh) =>
            new(_device, mesh, SCATTER_MATERIAL_DIFFUSE, _instancingEffect)
            {
                DetailTexture = _stoneTexture.Texture,
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
                DetailScale = 1f / STUMP_BARK_SPAN,
                DetailBoost = 1f / _barkTexture.LinearMean,
                DetailStrength = 0.5f,
                SurfaceReliefFrequency = 7f,
                SurfaceReliefStrength = 0.008f,
                CavityStrength = 0.6f,
                SpecularAmbientStrength = 0.08f
            };

        //Everything Build made, and nothing else — what Dispose releases. The renderers go
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
            if (_snagMeshes != null) foreach (SnagMesh mesh in _snagMeshes) mesh?.Dispose();
            if (_logMeshes != null) foreach (DeadwoodMesh mesh in _logMeshes) mesh?.Dispose();
        }
    }
}
