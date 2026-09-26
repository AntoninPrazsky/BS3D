using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One instanced draw of the savanna scatter: a mesh, a static buffer of its instances, and the per-draw
    /// material <c>Acacia.fx</c> shades them with. The instances are uploaded <b>once</b>, at build — the
    /// scatter never moves, so the one shared dynamic buffer the acacias used to re-upload on every draw
    /// (#202) was a per-frame copy of data that never changed, thirty times a frame once the plain had
    /// thirty kinds of thing on it.
    /// </summary>
    public sealed class ScatterBucket : IDisposable
    {
        public IProceduralMesh Mesh { get; }
        public VertexBuffer Instances { get; private set; }
        public int Count { get; }

        /// <summary>The material's colour (linear), and the drier shade an instance's dryness leans it towards.</summary>
        public Vector3 Diffuse { get; }
        public Vector3 DiffuseDry { get; }

        /// <summary>How strongly the leaf mottle breaks the colour up (0 on wood and stone).</summary>
        public float Dapple { get; }

        /// <summary>How strongly the bark's fissures ridge it (0 on foliage and stone).</summary>
        public float Bark { get; }

        /// <summary>Drawn only at full <see cref="SceneRenderer.SceneDetail"/> — the grass tufts, which are
        /// the most draws for the least silhouette and the first thing the Low tier can spare.</summary>
        public bool DetailOnly { get; }

        /// <summary>The instances as planted, on the CPU — what <see cref="Instances"/> was uploaded from.</summary>
        public IReadOnlyList<ModelInstance> Placed { get; }

        internal ScatterBucket(GraphicsDevice device, IProceduralMesh mesh, List<ModelInstance> instances,
            Vector3 diffuse, Vector3 diffuseDry, float dapple, float bark, bool detailOnly)
        {
            Mesh = mesh;
            Count = instances.Count;
            Diffuse = diffuse;
            DiffuseDry = diffuseDry;
            Dapple = dapple;
            Bark = bark;
            DetailOnly = detailOnly;
            ModelInstance[] placed = instances.ToArray();
            Placed = placed;
            Instances = new VertexBuffer(device, ModelInstance.VertexDeclaration, placed.Length, BufferUsage.WriteOnly);
            Instances.SetData(placed);
        }

        public void Dispose()
        {
            Instances?.Dispose();
            Instances = null;
        }
    }

    /// <summary>
    /// Where everything on the savanna stands and what it is (#451): the acacias in their four kinds, the
    /// bushes, the scrub, the grass tufts, the termite mounds, the kopjes, the fallen trees and the treeline
    /// at the horizon — every mesh variant of every kind, planted on the terrain in one deterministic pass
    /// off one seed and one occupancy list, and handed back as <see cref="Buckets"/> for
    /// <see cref="SceneRenderer"/> to draw on the acacia's instanced path.
    /// <para>
    /// <b>Where + what live here; with stays the renderer's</b>, the forest's split (<c>ForestScatter</c> /
    /// <c>ForestScatterRenderer</c>): the effect, its cached parameters and the frame's light are the
    /// <see cref="SceneRenderer"/>'s, and this class touches no GPU state and knows no camera. What it owns
    /// is the meshes and the instance buffers, and it disposes them.
    /// </para>
    /// <para>
    /// <b>The variety is in the mesh, the colour and the size, in that order.</b> Each kind is built in a few
    /// variants at rolled proportions and its own structural seed (the eye counts a repeated silhouette
    /// before it counts the trees — the forest's lesson, #108), each instance carries its own dryness and
    /// brightness in <see cref="ModelInstance.Custom"/> so one variant is several shades, and a uniform scale
    /// and a free yaw do the rest. Non-uniform scale is never used: the shader turns normals by the world
    /// matrix itself.
    /// </para>
    /// <para>
    /// The reference savannas rendered for #451 (and #281 before it) all say the same three things about
    /// what makes the plain read: the trees are <i>thin flat layers</i> wide against their height with the
    /// bough structure showing beneath; the ground between them carries <i>things</i> — bunches of grass,
    /// scrub, mounds, rock, deadwood — rather than only grass; and the horizon closes on a <i>dark wooded
    /// edge</i> in the haze. Each of those is a planting here.
    /// </para>
    /// </summary>
    public sealed class SavannaScatter : IDisposable
    {
        /// <summary>The scatter seed the savanna shipped with in #202; the same seed always plants the same plain.</summary>
        public const int DEFAULT_SEED = 90125;

        /// <summary>
        /// How trodden the ground under a plant may be before the site is refused (#476), on
        /// <see cref="SavannaTrails.Trodden(float, float, IReadOnlyList{ScatterSpacing.Footprint}, SavannaSceneConfig)"/>'s 0…1 — 1 being the bare middle of a path and 0 the grass
        /// beside it. Low, because it is the <b>worn earth</b> that nothing should be standing in, and the
        /// outermost fringe of the band is grass that has merely been walked on.
        /// <para>
        /// It costs next to nothing to be strict here. A path is about four world units wide against a plain
        /// some seven hundred across, so only a few per cent of the ground is refused, and a plant gets
        /// <see cref="ScatterSpacing.TRIES"/> proposals — all eight landing on a track is a case that does
        /// not arise. A constant rather than a dial on the config for that reason: unlike the warp's reach,
        /// which is an authored judgement about how early a person steps aside, there is nothing here to
        /// tune towards. Nothing grows in the middle of a worn path.
        /// </para>
        /// </summary>
        public const float TRAIL_REFUSE = 0.12f;

        /// <summary>
        /// How many times the plain is planted (#476). The first sweep finds out where the paths will run
        /// and the rest plant clear of them — see the sweep loop, where the argument that forced the shape
        /// is recorded. A sweep is arithmetic and nothing else: the meshes and the instance buffers, which
        /// are what a scatter build actually costs, are made once however many sweeps there are.
        /// <para>
        /// <b>Three, measured</b> — plants left standing on a path, over six scene seeds (trees and the
        /// like + ground cover, out of some 230 + 255):
        /// </para>
        /// <list type="table">
        /// <item><description>1 sweep, which is no test at all — <b>19+14, 19+15, 19+17, 12+15, 16+13, 17+9</b></description></item>
        /// <item><description>2 sweeps — 3+2, 6+8, 6+5, …</description></item>
        /// <item><description><b>3 sweeps — 1+0, 5+1, 3+2, 1+0, 0+0, 4+0</b></description></item>
        /// <item><description>4 and 5 — no better: 2+0, 6+4, 3+1 and 1+0, 2+1, 2+0, which straddle three</description></item>
        /// </list>
        /// <para>
        /// ⚠ <b>It does not converge to nothing, and it is not supposed to.</b> Each sweep moves a few
        /// plants, and moving a plant moves the paths near it — so a sweep both clears offenders and makes
        /// a couple of new ones, and past three the two roughly balance. What is left is a handful of stems
        /// at the <i>fringe</i> of a track on a plain of some 480 plants — 14 of them over those six seeds
        /// against 102 with one sweep, a seventh of what the untested planting leaves.
        /// </para>
        /// <para>
        /// The cost is the planting's own arithmetic, three times over: <b>10 ms for one sweep, 18 for two,
        /// 24 for three</b> on this project's slow machine (Vega 10 APU), at scene load and in the editor's
        /// re-plant, which rebuilds twenty-five meshes in the same breath.
        /// </para>
        /// </summary>
        private const int PLANTING_SWEEPS = 3;

        /// <summary>Keeps the planting's dice clear of the mesh variants' — the two are rolled off the same
        /// seed and must not be the same sequence.</summary>
        private const int PLANTING_STREAM = 0x5CA7;

        /// <summary>Every draw of the scatter, in draw order. Never empty buckets — a kind with a count of
        /// zero simply has none.</summary>
        public ScatterBucket[] Buckets { get; private set; }

        /// <summary>
        /// Everything the planting put on the ground, with its own footprint radius - the list
        /// <see cref="ScatterSpacing"/> kept plants out of each other with, handed on rather than thrown
        /// away. <see cref="TrailWarpField"/> reads it to know what a worn path has to go round (#476); the
        /// hearths the caller reserved are in it too, which is right - nobody walks through a campfire.
        /// <para>
        /// The planting reads the paths back the other way round as it goes, through
        /// <see cref="SavannaTrails"/> and <see cref="TRAIL_REFUSE"/>: what a path bends round and what may
        /// stand on a path are two questions, and the answers differ. A tuft of grass is too small for a
        /// track to go round - it is walked straight through - but it is also the very thing a worn path is
        /// worn out OF, so a tuft standing in the bare middle of one is exactly the artefact. Hence the
        /// asymmetry: the warp is built from what is big enough to walk round
        /// (<see cref="SavannaSceneConfig.TrailAvoidMinRadius"/>), the refusal applies to everything planted.
        /// </para>
        /// </summary>
        public IReadOnlyList<ScatterSpacing.Footprint> Standing { get; private set; }

        /// <summary>
        /// The living acacias of the plain (mature and broken, the umbrella-crowned ones — not the young, the
        /// dead or the far treeline's), each with its crown's own figure: what the chapter intro's prologue
        /// takes a shot of (#559). Only the last sweep's, like the buckets.
        /// </summary>
        public IReadOnlyList<PlantFigure> Acacias { get; private set; }

        /// <summary>The baobabs, each with its whole figure — the plain's biggest single things (#559).</summary>
        public IReadOnlyList<PlantFigure> Baobabs { get; private set; }

        /// <summary>
        /// Every instance of every mesh planted, the far treeline's included, as the sphere round its mesh at
        /// the instance (a tree twice over: its wood's sphere and its canopy's) — what a camera path is held
        /// clear of (#559). ⚠ Not <see cref="Standing"/>: a footprint is the SPACING radius, and a bush's
        /// crown overhangs its own; a lens kept clear of the footprints photographed as a frame of foliage.
        /// </summary>
        public IReadOnlyList<PlantFigure> Solids { get; private set; }

        private readonly List<IDisposable> _meshes = new();

        /// <param name="device">The device the meshes and instance buffers live on.</param>
        /// <param name="config">The savanna's config: the acacias' and the dressing's counts, sizes and colours.</param>
        /// <param name="terrainHeight">The ground under (x, z) — <c>TerrainMirror.Savanna</c>,
        /// the C# mirror of the shader's field, so a plant stands on the ground it is drawn over.</param>
        /// <param name="reserved">Ground already taken before anything is planted: the campfires and their
        /// hearths, which nothing may land in.</param>
        /// <param name="seed">Scatter seed; the same seed always plants the same savanna.</param>
        public SavannaScatter(GraphicsDevice device, SavannaSceneConfig config, Func<float, float, float> terrainHeight,
            IReadOnlyList<ScatterSpacing.Footprint> reserved, int seed = DEFAULT_SEED)
        {
            AcaciaConfig ac = config.Acacia;
            SavannaDressingConfig dr = config.Dressing;
            Random rng = new(seed);
            var buckets = new List<ScatterBucket>();

            //--- The meshes: a few variants of every kind, each at rolled proportions and its own structural seed.
            const int MATURE = 4, BROKEN = 1, YOUNG = 2, DEAD = 2, BUSH = 2, SCRUB = 2, TUFT = 3, MOUND = 2, ROCK = 3, LOG = 2, TREELINE = 2, BAOBAB = 2, DOUM = 2;

            var trees = new List<AcaciaMesh>();
            for (int m = 0; m < MATURE; m++)
            {
                float w = 0.8f + 0.45f * (float)rng.NextDouble();
                float h = 0.85f + 0.4f * (float)rng.NextDouble();
                trees.Add(Own(new AcaciaMesh(device, AcaciaKind.Mature, ac.Width * 0.09f * w, ac.Height * h, ac.Width * w, 4100 + m)));
            }
            for (int m = 0; m < BROKEN; m++)
            {
                float w = 0.9f + 0.3f * (float)rng.NextDouble();
                trees.Add(Own(new AcaciaMesh(device, AcaciaKind.Broken, ac.Width * 0.09f * w, ac.Height * (0.9f + 0.2f * (float)rng.NextDouble()), ac.Width * w, 4110 + m)));
            }
            for (int m = 0; m < YOUNG; m++)
            {
                float w = 0.7f + 0.3f * (float)rng.NextDouble();
                trees.Add(Own(new AcaciaMesh(device, AcaciaKind.Young, ac.Width * 0.055f * w, ac.Height * (0.55f + 0.15f * (float)rng.NextDouble()), ac.Width * 0.9f * w, 4120 + m)));
            }
            for (int m = 0; m < DEAD; m++)
            {
                float w = 0.8f + 0.4f * (float)rng.NextDouble();
                trees.Add(Own(new AcaciaMesh(device, AcaciaKind.Dead, ac.Width * 0.085f * w, ac.Height * (0.8f + 0.3f * (float)rng.NextDouble()), ac.Width * 0.85f * w, 4130 + m)));
            }

            var bushes = new FoliageMesh[BUSH];
            for (int m = 0; m < BUSH; m++)
            {
                float br = ac.Width * (0.5f + 0.2f * (float)rng.NextDouble());
                float bh = ac.Height * (0.22f + 0.08f * (float)rng.NextDouble());
                bushes[m] = Own(new FoliageMesh(device, br, bh, centreY: bh, seed: 4200 + m));
            }

            var scrub = new FoliageMesh[SCRUB];
            for (int m = 0; m < SCRUB; m++)
            {
                float r = dr.ScrubSize * (0.8f + 0.4f * (float)rng.NextDouble());
                float hh = r * (0.55f + 0.2f * (float)rng.NextDouble());
                scrub[m] = Own(new FoliageMesh(device, r, hh, centreY: hh * 0.8f, seed: 4300 + m, FoliageStyle.Scrub));
            }

            var tufts = new GrassTuftMesh[TUFT];
            for (int m = 0; m < TUFT; m++)
            {
                float r = dr.TuftSize * (0.8f + 0.4f * (float)rng.NextDouble());
                tufts[m] = Own(new GrassTuftMesh(device, r, r * (1.1f + 0.4f * (float)rng.NextDouble()), 4400 + m));
            }

            var mounds = new TermiteMoundMesh[MOUND];
            for (int m = 0; m < MOUND; m++)
            {
                //The references' mounds are spires: three to four times as tall as their column is wide.
                float h = dr.MoundHeight * (0.85f + 0.3f * (float)rng.NextDouble());
                mounds[m] = Own(new TermiteMoundMesh(device, h * 0.18f * (0.9f + 0.2f * (float)rng.NextDouble()), h, irregularityPhase: 1.3f * m));
            }

            //Rounder than the forest's flattened boulders: a kopje's rocks are eggs of granite as tall as they
            //are wide, and the same three meshes serve the lone boulders at a fraction of the size.
            var rocks = new RockMesh[ROCK];
            for (int m = 0; m < ROCK; m++)
            {
                float r = dr.KopjeRockSize * (0.75f + 0.12f * m);
                rocks[m] = Own(new RockMesh(device, r, r * (0.85f + 0.3f * (float)rng.NextDouble()), 16, irregularityPhase: 1.7f * m + 0.4f));
            }

            var logs = new DeadwoodMesh[LOG];
            for (int m = 0; m < LOG; m++)
            {
                float len = dr.LogLength * (0.8f + 0.4f * (float)rng.NextDouble());
                logs[m] = Own(new DeadwoodMesh(device, len, len * 0.1f * (0.8f + 0.4f * (float)rng.NextDouble()), 4500 + m));
            }

            var baobabs = new BaobabMesh[BAOBAB];
            for (int m = 0; m < BAOBAB; m++)
                baobabs[m] = Own(new BaobabMesh(device, dr.BaobabHeight * (0.85f + 0.3f * (float)rng.NextDouble()), 4700 + m));

            var doums = new DoumPalmMesh[DOUM];
            for (int m = 0; m < DOUM; m++)
                doums[m] = Own(new DoumPalmMesh(device, dr.DoumPalmHeight * (0.85f + 0.3f * (float)rng.NextDouble()), 4800 + m));

            var treeline = new FoliageMesh[TREELINE];
            for (int m = 0; m < TREELINE; m++)
            {
                float r = dr.TreelineSize * (0.8f + 0.4f * (float)rng.NextDouble());
                float hh = r * (0.35f + 0.15f * (float)rng.NextDouble());
                treeline[m] = Own(new FoliageMesh(device, r, hh, centreY: hh * 0.6f, seed: 4600 + m, FoliageStyle.Scrub));
            }

            //--- The instance lists, one per mesh (a tree's canopy and wood share one).
            var treeInstances = new List<ModelInstance>[trees.Count];
            for (int m = 0; m < trees.Count; m++) treeInstances[m] = new List<ModelInstance>();
            var bushInstances = Lists(BUSH);
            var scrubInstances = Lists(SCRUB);
            var tuftInstances = Lists(TUFT);
            var moundInstances = Lists(MOUND);
            var rockInstances = Lists(ROCK);
            var logInstances = Lists(LOG);
            var baobabInstances = Lists(BAOBAB);
            var doumInstances = Lists(DOUM);
            var treelineInstances = Lists(TREELINE);
            var acaciaFigures = new List<PlantFigure>();
            var baobabFigures = new List<PlantFigure>();
            Acacias = acaciaFigures;
            Baobabs = baobabFigures;

            //Cluster centres the plants gather around, so the savanna reads as groves rather than an even
            //scatter. A minority of plants are placed solo.
            float[] clusterX = new float[ac.Clusters];
            float[] clusterZ = new float[ac.Clusters];
            for (int c = 0; c < ac.Clusters; c++)
            {
                float ca = (float)rng.NextDouble() * MathHelper.TwoPi;
                float cr = ac.MinRadius + (float)rng.NextDouble() * (ac.MaxRadius - ac.MinRadius);
                clusterX[c] = MathF.Cos(ca) * cr;
                clusterZ[c] = MathF.Sin(ca) * cr;
            }

            //What is already standing, so the next thing can be kept out of it — ScatterSpacing's rule, one
            //copy with the forest's (#108). One list for the whole plain: a mound does not stand inside a
            //tree, and nothing stands in a hearth.
            var standing = new List<ScatterSpacing.Footprint>(reserved);
            Standing = standing;

            //And the subset of it a worn path goes ROUND (#476) — what the trail test below bends the paths
            //by. It holds the PREVIOUS sweep's finished plain rather than the one being planted; see the
            //sweep loop for why, and note that TrailWarpField filters by the same radius when it bakes the
            //texture the shader samples, so the two sides mean the same thing by "worth going round".
            var bendBy = new List<ScatterSpacing.Footprint>();

            //Which site is being placed, counted from the start of each sweep: it is what keys a site's own
            //stream of dice (see Propose), so the n-th plant of one sweep is the n-th plant of the next.
            int sitesPlaced = 0;

            //One proposal of a spot: near a cluster centre (denser towards it) or anywhere in the ring.
            //
            //⚠ It draws from the SITE's own stream, not from the planting's (#476). A site that needs a
            //second proposal must not shift the dice for everything planted after it: the sweep below
            //plants the same plain several times over and keeps the last, and that only converges if a
            //plant moved in one sweep moves ALONE. On a shared stream it would not — one extra proposal
            //re-rolls every plant after it, and each sweep would be a fresh savanna tested against the
            //paths of a plain that no longer exists.
            (float, float) Propose(Random site, float minR, float maxR, float clusterShare, float spread)
            {
                float cx, cz;
                if (site.NextDouble() < clusterShare)
                {
                    int c = site.Next(ac.Clusters);
                    float off = (float)site.NextDouble();
                    float d = off * off * spread;
                    float da = (float)site.NextDouble() * MathHelper.TwoPi;
                    cx = clusterX[c] + MathF.Cos(da) * d;
                    cz = clusterZ[c] + MathF.Sin(da) * d;
                }
                else
                {
                    float a = (float)site.NextDouble() * MathHelper.TwoPi;
                    float r = minR + (float)site.NextDouble() * (maxR - minR);
                    cx = MathF.Cos(a) * r;
                    cz = MathF.Sin(a) * r;
                }
                //Keep clear of the island, and inside the ring.
                float dist = MathF.Sqrt(cx * cx + cz * cz);
                if (dist > 0.01f && (dist < minR || dist > maxR))
                {
                    float to = MathHelper.Clamp(dist, minR, maxR);
                    cx *= to / dist;
                    cz *= to / dist;
                }
                return (cx, cz);
            }

            //A plant's own frame: a small lean off vertical (a leaning tree reads as a tree, a tilted one as a
            //felled one — the forest's TREE_LEAN), a free yaw, the uniform size, and planted on the ground.
            //Scale first so it stays uniform, then the tilt and spin, then the translation. The instance's own
            //dryness and brightness ride in Custom (Acacia.fx reads them).
            ModelInstance Plant(float x, float z, float scale, float lean, float sink, float dryness, float jitter)
            {
                float yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                float leanDir = (float)rng.NextDouble() * MathHelper.TwoPi;
                Matrix world = Matrix.CreateScale(scale)
                    * Matrix.CreateFromAxisAngle(new Vector3(MathF.Cos(leanDir), 0f, MathF.Sin(leanDir)), lean)
                    * Matrix.CreateRotationY(yaw)
                    * Matrix.CreateTranslation(x, terrainHeight(x, z) - sink, z);
                return new ModelInstance(world, new Vector4(dryness, jitter, 0f, 0f));
            }

            float Jitter() => (float)(rng.NextDouble() - 0.5) * 0.24f;

            //Everything on the plain, in one sweep — and the plain is planted SEVERAL TIMES OVER (#476).
            //The shape is odd enough to be worth the paragraph, because a measurement forced it rather than
            //taste choosing it.
            //
            //A path bends round what stands on the plain, so "is this site on a path?" cannot be answered
            //until the plain is planted — and the answer moves the plants. The two depend on each other.
            //Bending the paths by whatever happened to be standing already (the obvious way: one sweep, the
            //list growing as it goes) fixed the GROUND COVER, which goes in last when nearly everything is
            //already in, and did next to nothing for the TREES, which go in FIRST, when there is nothing
            //for a path to bend round yet — measured over three seeds, trees on a path went 23 → 12,
            //22 → 20 and 14 → 15. The trees are what the owner's report is about.
            //
            //So the first sweep plants the plain with no trail test at all, and what it puts down is what
            //the paths are bent by in the next, which plants the same plain again from the same dice and
            //this time declines the sites those paths run through. Only the last sweep's instances are
            //kept; the ones before it exist to find out where the tracks will be. PLANTING_SWEEPS carries
            //the count and what each one is worth.
            void PlantThePlain(bool avoidTrails)
            {
                //The same dice every sweep, so each one is the last with its offenders moved rather than a
                //fresh savanna. Reset here and not at the top: the meshes and the cluster centres above are
                //rolled once and stay.
                rng = new Random(seed ^ PLANTING_STREAM);
                sitesPlaced = 0;

                standing.Clear();
                standing.AddRange(reserved);

                for (int m = 0; m < treeInstances.Length; m++) treeInstances[m].Clear();
                Clear(bushInstances); Clear(scrubInstances); Clear(tuftInstances); Clear(moundInstances);
                Clear(rockInstances); Clear(logInstances); Clear(baobabInstances); Clear(doumInstances);
                acaciaFigures.Clear();
                baobabFigures.Clear();

                //The best of a few proposals against what stands already: the first clear one, else the
                //least crowded. Records the footprint and returns where it landed.
                //
                //"Clear" is two questions since #476 — room to stand, and NOT ON A PATH. The second is the
                //belt and braces behind TrailWarpField: the path already bends round what is planted, and
                //this refuses the sites where the bend could not carry it clear (a dense clump, or a track
                //threading between two trunks). In the first sweep there are no paths to answer to yet.
                (float x, float z) Place(float halfWidth, float minR, float maxR, float clusterShare, float spread)
                {
                    Random site = new(seed * 397 + sitesPlaced++);

                    float x = 0f, z = 0f;
                    float bestClearance = float.NegativeInfinity;
                    bool bestOnPath = true;     //nothing is chosen yet, and anything at all beats that
                    for (int attempt = 0; attempt < ScatterSpacing.TRIES; attempt++)
                    {
                        (float cx, float cz) = Propose(site, minR, maxR, clusterShare, spread);
                        float clearance = ScatterSpacing.Clearance(cx, cz, halfWidth, standing);

                        //⚠ The STEM, not the crown: the trodden ground is tested at the one point the thing
                        //actually stands on, and deliberately not over the spacing footprint, which is the
                        //crown's reach. A path running under a canopy is what a path does — people walk
                        //under trees — and refusing every site whose crown oversails a track would empty
                        //the groves along every path on the plain. What is nonsense is the TRUNK in the
                        //worn earth.
                        float trodden = avoidTrails ? SavannaTrails.Trodden(cx, cz, bendBy, config) : 0f;

                        bool onPath = trodden > TRAIL_REFUSE;

                        //⚠ Two keys, and the PATH is the first of them — not a penalty added to the
                        //clearance, which is what this was until it was measured. Where no proposal is both
                        //clear and off a path, a priced trail loses to elbow room every time: a tree with
                        //twenty units of space on a track beat one that had to interlace a crown to stand
                        //beside it, and 16 to 19 plants a seed were put back onto a path that way, against
                        //19 to 25 still standing on one at the end — nearly all of what was left. And in
                        //not one case were all eight proposals on a track: there was always somewhere else
                        //to stand. Ranking the path first costs little, and the measurement says how much:
                        //it is the gap between the roomiest proposal and the roomiest off-path one, and
                        //only about one proposal in ten is on a path at all.
                        if ((bestOnPath && !onPath) || (onPath == bestOnPath && clearance > bestClearance))
                        {
                            bestOnPath = onPath;
                            bestClearance = clearance;
                            x = cx;
                            z = cz;
                        }
                        if (clearance >= 0f && !onPath) break;
                    }
                    standing.Add(new ScatterSpacing.Footprint(x, z, halfWidth));

                    return (x, z);
                }

                //--- The acacias and the bushes: #202's planting, with the trees split across their kinds.
                for (int i = 0; i < ac.Count; i++)
                {
                    //Rolled BEFORE the position, because the position depends on how wide this plant is: a bush
                    //needs a third of a tree's room and should not be pushed out as though it needed all of it.
                    float rand = (float)rng.NextDouble();
                    bool isBush = rng.NextDouble() < ac.BushFraction;

                    if (isBush)
                    {
                        float sizeScale = 0.7f + 0.6f * rand;
                        (float x, float z) = Place(ac.Width * 0.5f * sizeScale, ac.MinRadius, ac.MaxRadius, 0.82f, ac.ClusterSpread);
                        bushInstances[rng.Next(BUSH)].Add(Plant(x, z, sizeScale, 0.06f * (float)rng.NextDouble(), 0f, (float)rng.NextDouble(), Jitter()));
                        continue;
                    }

                    //Which kind of tree: the fractions are of the trees, taken in turn.
                    float kindRoll = (float)rng.NextDouble();
                    AcaciaKind kind = kindRoll < ac.DeadFraction ? AcaciaKind.Dead
                        : kindRoll < ac.DeadFraction + ac.YoungFraction ? AcaciaKind.Young
                        : kindRoll < ac.DeadFraction + ac.YoungFraction + ac.BrokenFraction ? AcaciaKind.Broken
                        : AcaciaKind.Mature;

                    int first = kind switch { AcaciaKind.Mature => 0, AcaciaKind.Broken => MATURE, AcaciaKind.Young => MATURE + BROKEN, _ => MATURE + BROKEN + YOUNG };
                    int count = kind switch { AcaciaKind.Mature => MATURE, AcaciaKind.Broken => BROKEN, AcaciaKind.Young => YOUNG, _ => DEAD };
                    int variant = first + rng.Next(count);

                    float treeScale = 0.8f + 0.5f * rand;
                    float halfWidth = ac.Width * treeScale * (kind == AcaciaKind.Young ? 0.6f : 1f);
                    {
                        (float x, float z) = Place(halfWidth, ac.MinRadius, ac.MaxRadius, 0.82f, ac.ClusterSpread);
                        //A dead tree is one shade of bleached wood; the living ones each lean their own way towards dry.
                        float dryness = kind == AcaciaKind.Dead ? 0f : (float)rng.NextDouble();
                        float lean = (kind == AcaciaKind.Broken ? 0.10f : 0.06f) * (float)rng.NextDouble();
                        ModelInstance planted = Plant(x, z, treeScale, lean, 0f, dryness, Jitter());
                        treeInstances[variant].Add(planted);

                        //The umbrella-crowned ones, for a camera to point at (#559): the canopy's own sphere.
                        if (trees[variant].Canopy != null && (kind == AcaciaKind.Mature || kind == AcaciaKind.Broken))
                            acaciaFigures.Add(PlantFigure.Of(trees[variant].Canopy.BoundingSphere, planted.World, ac.Width * 0.09f));
                    }
                }

                //--- The scrub: thickets round the groves (a tighter spread than the trees') and the odd one alone.
                for (int i = 0; i < dr.ScrubCount; i++)
                {
                    float s = 0.7f + 0.6f * (float)rng.NextDouble();
                    (float x, float z) = Place(dr.ScrubSize * s, ac.MinRadius, ac.MaxRadius, 0.7f, ac.ClusterSpread * 0.8f);
                    scrubInstances[rng.Next(SCRUB)].Add(Plant(x, z, s, 0.08f * (float)rng.NextDouble(), 0f, (float)rng.NextDouble(), Jitter()));
                }

                //--- The termite mounds: alone in the open, never in a grove, sunk a little into the earth they are made of.
                for (int i = 0; i < dr.MoundCount; i++)
                {
                    float s = 0.7f + 0.6f * (float)rng.NextDouble();
                    (float x, float z) = Place(dr.MoundHeight * 0.4f * s, ac.MinRadius + 20f, ac.MaxRadius, 0f, 0f);
                    moundInstances[rng.Next(MOUND)].Add(Plant(x, z, s, 0.05f * (float)rng.NextDouble(), 0.15f * s, 0.3f * (float)rng.NextDouble(), Jitter()));
                }

                //--- The kopjes: a pile of boulders each, the biggest at the middle and set into the ground, the
                //rest round it and one or two up on the big one - a heap, which is what a kopje is.
                for (int k = 0; k < dr.KopjeCount; k++)
                {
                    int rockCount = dr.KopjeRocksMin + rng.Next(Math.Max(1, dr.KopjeRocksMax - dr.KopjeRocksMin + 1));
                    float pileRadius = dr.KopjeRockSize * 2.2f;
                    (float kx, float kz) = Place(pileRadius, ac.MinRadius + 40f, ac.MaxRadius, 0f, 0f);
                    float ground = terrainHeight(kx, kz);
                    float baseHeight = 0f;
                    for (int r = 0; r < rockCount; r++)
                    {
                        int variant = rng.Next(ROCK);
                        float s, x, z, y, tilt;
                        if (r == 0)
                        {
                            s = 0.9f + 0.2f * (float)rng.NextDouble();
                            x = kx; z = kz;
                            baseHeight = rocks[variant].BoundingSphere.Radius * 0.9f * s;
                            y = ground - baseHeight * 0.3f;
                            tilt = 0.05f + 0.1f * (float)rng.NextDouble();
                        }
                        else if (r < 3)
                        {
                            //Up on the base rock, smaller, tipped over its shoulder.
                            s = 0.45f + 0.25f * (float)rng.NextDouble();
                            float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                            float d = dr.KopjeRockSize * (0.3f + 0.4f * (float)rng.NextDouble());
                            x = kx + MathF.Cos(a) * d; z = kz + MathF.Sin(a) * d;
                            y = ground + baseHeight * 0.55f;
                            tilt = 0.15f + 0.3f * (float)rng.NextDouble();
                        }
                        else
                        {
                            //Round the foot, half-buried.
                            s = 0.4f + 0.4f * (float)rng.NextDouble();
                            float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                            float d = dr.KopjeRockSize * (1.0f + 0.6f * (float)rng.NextDouble());
                            x = kx + MathF.Cos(a) * d; z = kz + MathF.Sin(a) * d;
                            y = terrainHeight(x, z) - dr.KopjeRockSize * s * 0.25f;
                            tilt = 0.1f + 0.25f * (float)rng.NextDouble();
                        }
                        float yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                        float tiltDir = (float)rng.NextDouble() * MathHelper.TwoPi;
                        Matrix world = Matrix.CreateScale(s)
                            * Matrix.CreateFromAxisAngle(new Vector3(MathF.Cos(tiltDir), 0f, MathF.Sin(tiltDir)), tilt)
                            * Matrix.CreateRotationY(yaw)
                            * Matrix.CreateTranslation(x, y, z);
                        rockInstances[variant].Add(new ModelInstance(world, new Vector4(0.4f * (float)rng.NextDouble(), Jitter(), 0f, 0f)));
                    }
                }

                //--- The baobabs: alone in the open like the mounds, never in a grove, and big - the footprint is
                //the crown's reach, so nothing else stands under one.
                for (int i = 0; i < dr.BaobabCount; i++)
                {
                    int variant = rng.Next(BAOBAB);
                    float s = 0.85f + 0.3f * (float)rng.NextDouble();
                    (float x, float z) = Place(dr.BaobabHeight * 0.45f * s, ac.MinRadius + 30f, ac.MaxRadius, 0f, 0f);
                    ModelInstance planted = Plant(x, z, s, 0.03f * (float)rng.NextDouble(), 0f, (float)rng.NextDouble(), Jitter());
                    baobabInstances[variant].Add(planted);
                    BoundingSphere whole = BoundingSphere.CreateMerged(baobabs[variant].Wood.BoundingSphere, baobabs[variant].Foliage.BoundingSphere);
                    baobabFigures.Add(PlantFigure.Of(whole, planted.World, dr.BaobabHeight * 0.2f));
                }

                //--- The doum palms: in clumps of two or three, the way they grow, each clump placed as a whole
                //and its palms kept out of each other inside it.
                for (int i = 0; i < dr.DoumPalmCount;)
                {
                    int clump = Math.Min(2 + rng.Next(2), dr.DoumPalmCount - i);
                    (float cx, float cz) = Place(dr.DoumPalmHeight * 0.5f, ac.MinRadius + 10f, ac.MaxRadius, 0.5f, ac.ClusterSpread);
                    for (int p = 0; p < clump; p++, i++)
                    {
                        float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                        float d = p == 0 ? 0f : dr.DoumPalmHeight * (0.25f + 0.25f * (float)rng.NextDouble());
                        float s = 0.8f + 0.4f * (float)rng.NextDouble();
                        doumInstances[rng.Next(DOUM)].Add(Plant(cx + MathF.Cos(a) * d, cz + MathF.Sin(a) * d, s, 0.08f * (float)rng.NextDouble(), 0f, (float)rng.NextDouble(), Jitter()));
                    }
                }

                //--- The lone boulders: the kopjes' own meshes at a smaller size, each alone and half-buried.
                for (int i = 0; i < dr.BoulderCount; i++)
                {
                    int variant = rng.Next(ROCK);
                    float s = dr.BoulderSize / dr.KopjeRockSize * (0.6f + 0.5f * (float)rng.NextDouble());
                    float r = rocks[variant].BoundingSphere.Radius * s;
                    (float x, float z) = Place(r, ac.MinRadius, ac.MaxRadius, 0.4f, ac.ClusterSpread);
                    rockInstances[variant].Add(Plant(x, z, s, 0.1f + 0.2f * (float)rng.NextDouble(), r * 0.3f, 0.4f * (float)rng.NextDouble(), Jitter()));
                }

                //--- The fallen trees: in the open and at the groves' edges alike, sunk a quarter of their thickness.
                for (int i = 0; i < dr.LogCount; i++)
                {
                    float s = 0.8f + 0.4f * (float)rng.NextDouble();
                    (float x, float z) = Place(dr.LogLength * 0.5f * s, ac.MinRadius, ac.MaxRadius, 0.5f, ac.ClusterSpread);
                    logInstances[rng.Next(LOG)].Add(Plant(x, z, s, 0.04f * (float)rng.NextDouble(), 0f, 0.5f * (float)rng.NextDouble(), Jitter()));
                }

                //--- The tufts: everywhere, nearest the island of anything, and cheap to lose (DetailOnly).
                for (int i = 0; i < dr.TuftCount; i++)
                {
                    float s = 0.7f + 0.6f * (float)rng.NextDouble();
                    (float x, float z) = Place(dr.TuftSize * s, dr.TuftMinRadius, dr.TuftMaxRadius, 0.3f, ac.ClusterSpread);
                    tuftInstances[rng.Next(TUFT)].Add(Plant(x, z, s, 0.1f * (float)rng.NextDouble(), 0f, (float)rng.NextDouble(), Jitter()));
                }
            }

            for (int sweep = 0; sweep < PLANTING_SWEEPS; sweep++)
            {
                PlantThePlain(avoidTrails: sweep > 0);

                //What the next sweep's paths bend round: this sweep's finished plain, whole, rather than a
                //list that grew as it went. A path is bent by the whole plain or it is bent by an accident
                //of planting order, and the texture the shader samples is built from the whole plain too.
                bendBy.Clear();
                for (int i = 0; i < standing.Count; i++)
                    if (standing[i].Radius >= config.TrailAvoidMinRadius) bendBy.Add(standing[i]);
            }

            //--- The treeline: its own band beyond the plain and its own occupancy - nothing out there meets
            //anything in here - the masses first and the far acacias between them.
            var farStanding = new List<ScatterSpacing.Footprint>();
            for (int i = 0; i < dr.TreelineCount; i++)
            {
                float s = 0.7f + 0.6f * (float)rng.NextDouble();
                float x = 0f, z = 0f, best = float.NegativeInfinity;
                for (int attempt = 0; attempt < ScatterSpacing.TRIES; attempt++)
                {
                    float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                    float r = dr.TreelineMinRadius + (float)rng.NextDouble() * (dr.TreelineMaxRadius - dr.TreelineMinRadius);
                    float cx = MathF.Cos(a) * r, cz = MathF.Sin(a) * r;
                    float clearance = ScatterSpacing.Clearance(cx, cz, dr.TreelineSize * s, farStanding);
                    if (clearance > best) { best = clearance; x = cx; z = cz; }
                    if (clearance >= 0f) break;
                }
                farStanding.Add(new ScatterSpacing.Footprint(x, z, dr.TreelineSize * s));
                treelineInstances[rng.Next(TREELINE)].Add(Plant(x, z, s, 0f, dr.TreelineSize * s * 0.2f, 0.5f * (float)rng.NextDouble(), Jitter()));
            }
            for (int i = 0; i < dr.TreelineTreeCount; i++)
            {
                float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                float r = dr.TreelineMinRadius + (float)rng.NextDouble() * (dr.TreelineMaxRadius - dr.TreelineMinRadius);
                float s = 1.3f + 0.5f * (float)rng.NextDouble();
                treeInstances[rng.Next(MATURE)].Add(Plant(MathF.Cos(a) * r, MathF.Sin(a) * r, s, 0f, 0f, (float)rng.NextDouble(), Jitter()));
            }

            //--- The buckets, in draw order: a tree's canopy then its wood, then everything else by kind.
            Vector3 canopy = ac.CanopyColor.ToVector3();
            Vector3 canopyDry = ac.CanopyDry.ToVector3();
            Vector3 trunk = ac.TrunkColor.ToVector3();
            Vector3 deadwood = ac.DeadwoodColor.ToVector3();
            Vector3 deadwoodDry = deadwood * 1.2f;
            for (int m = 0; m < trees.Count; m++)
            {
                if (treeInstances[m].Count == 0) continue;
                AcaciaMesh tree = trees[m];
                if (tree.Canopy != null)
                    buckets.Add(new ScatterBucket(device, tree.Canopy, treeInstances[m], canopy, canopyDry, dapple: 0.6f, bark: 0f, detailOnly: false));
                bool dead = tree.Kind == AcaciaKind.Dead;
                buckets.Add(new ScatterBucket(device, tree.Wood, treeInstances[m], dead ? deadwood : trunk, dead ? deadwoodDry : trunk * 1.15f,
                    dapple: 0f, bark: 0.6f, detailOnly: false));
            }
            Add(buckets, device, bushes, bushInstances, canopyDry, canopy, dapple: 0.5f, bark: 0f, detailOnly: false);
            Vector3 scrubColor = dr.ScrubColor.ToVector3();
            Add(buckets, device, scrub, scrubInstances, scrubColor, scrubColor * new Vector3(1.5f, 1.25f, 0.9f), dapple: 0.5f, bark: 0f, detailOnly: false);
            Vector3 moundColor = dr.MoundColor.ToVector3();
            Add(buckets, device, mounds, moundInstances, moundColor, moundColor * new Vector3(1.15f, 1.2f, 1.3f), dapple: 0f, bark: 0.45f, detailOnly: false);
            Vector3 rockColor = dr.RockColor.ToVector3();
            //The stone takes a little of the foliage's mottle: lichen, the patches every reference boulder wears.
            Add(buckets, device, rocks, rockInstances, rockColor, rockColor * new Vector3(1.1f, 1.05f, 0.95f), dapple: 0.35f, bark: 0f, detailOnly: false);
            Add(buckets, device, logs, logInstances, deadwood, deadwoodDry, dapple: 0f, bark: 0.6f, detailOnly: false);
            Vector3 baobabColor = dr.BaobabColor.ToVector3();
            for (int m = 0; m < BAOBAB; m++)
            {
                if (baobabInstances[m].Count == 0) continue;
                buckets.Add(new ScatterBucket(device, baobabs[m].Wood, baobabInstances[m], baobabColor, baobabColor * 1.08f, dapple: 0f, bark: 0.25f, detailOnly: false));
                buckets.Add(new ScatterBucket(device, baobabs[m].Foliage, baobabInstances[m], canopy, canopyDry, dapple: 0.5f, bark: 0f, detailOnly: false));
            }
            for (int m = 0; m < DOUM; m++)
            {
                if (doumInstances[m].Count == 0) continue;
                buckets.Add(new ScatterBucket(device, doums[m].Wood, doumInstances[m], trunk, trunk * 1.15f, dapple: 0f, bark: 0.6f, detailOnly: false));
                buckets.Add(new ScatterBucket(device, doums[m].Fronds, doumInstances[m], canopy * new Vector3(1.1f, 1.15f, 0.9f), canopyDry, dapple: 0.4f, bark: 0f, detailOnly: false));
            }
            Vector3 tuftColor = dr.TuftColor.ToVector3();
            Add(buckets, device, tufts, tuftInstances, tuftColor, tuftColor * new Vector3(0.6f, 0.8f, 0.6f), dapple: 0.8f, bark: 0f, detailOnly: true);
            Vector3 treelineColor = dr.TreelineColor.ToVector3();
            Add(buckets, device, treeline, treelineInstances, treelineColor, treelineColor * 1.6f, dapple: 0.4f, bark: 0f, detailOnly: false);

            Buckets = buckets.ToArray();

            var solids = new List<PlantFigure>();
            foreach (ScatterBucket bucket in Buckets)
                foreach (ModelInstance instance in bucket.Placed)
                    solids.Add(PlantFigure.Of(bucket.Mesh.BoundingSphere, instance.World, 0f));
            Solids = solids;
        }

        private static void Clear(List<ModelInstance>[] lists)
        {
            for (int i = 0; i < lists.Length; i++) lists[i].Clear();
        }

        private static List<ModelInstance>[] Lists(int count)
        {
            var lists = new List<ModelInstance>[count];
            for (int i = 0; i < count; i++) lists[i] = new List<ModelInstance>();
            return lists;
        }

        private static void Add<T>(List<ScatterBucket> buckets, GraphicsDevice device, T[] meshes, List<ModelInstance>[] instances,
            Vector3 diffuse, Vector3 diffuseDry, float dapple, float bark, bool detailOnly) where T : IProceduralMesh
        {
            for (int m = 0; m < meshes.Length; m++)
            {
                if (instances[m].Count == 0) continue;
                buckets.Add(new ScatterBucket(device, meshes[m], instances[m], diffuse, diffuseDry, dapple, bark, detailOnly));
            }
        }

        private T Own<T>(T mesh) where T : IDisposable
        {
            _meshes.Add(mesh);
            return mesh;
        }

        public void Dispose()
        {
            if (Buckets != null) foreach (ScatterBucket bucket in Buckets) bucket.Dispose();
            Buckets = null;
            foreach (IDisposable mesh in _meshes) mesh.Dispose();
            _meshes.Clear();
        }
    }
}
