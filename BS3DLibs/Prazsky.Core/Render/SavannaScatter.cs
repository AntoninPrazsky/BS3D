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
            Instances = new VertexBuffer(device, ModelInstance.VertexDeclaration, instances.Count, BufferUsage.WriteOnly);
            Instances.SetData(instances.ToArray());
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

        /// <summary>Every draw of the scatter, in draw order. Never empty buckets — a kind with a count of
        /// zero simply has none.</summary>
        public ScatterBucket[] Buckets { get; private set; }

        private readonly List<IDisposable> _meshes = new();

        /// <param name="device">The device the meshes and instance buffers live on.</param>
        /// <param name="config">The savanna's config: the acacias' and the dressing's counts, sizes and colours.</param>
        /// <param name="terrainHeight">The ground under (x, z) — <c>SceneRenderer.SavannaTerrainHeight</c>,
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

            //One proposal of a spot: near a cluster centre (denser towards it) or anywhere in the ring.
            (float, float) Propose(float minR, float maxR, float clusterShare, float spread)
            {
                float cx, cz;
                if (rng.NextDouble() < clusterShare)
                {
                    int c = rng.Next(ac.Clusters);
                    float off = (float)rng.NextDouble();
                    float d = off * off * spread;
                    float da = (float)rng.NextDouble() * MathHelper.TwoPi;
                    cx = clusterX[c] + MathF.Cos(da) * d;
                    cz = clusterZ[c] + MathF.Sin(da) * d;
                }
                else
                {
                    float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                    float r = minR + (float)rng.NextDouble() * (maxR - minR);
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

            //The best of a few proposals against what stands already: the first clear one, else the least
            //crowded. Records the footprint and returns where it landed.
            (float x, float z) Place(float halfWidth, float minR, float maxR, float clusterShare, float spread)
            {
                float x = 0f, z = 0f;
                float bestClearance = float.NegativeInfinity;
                for (int attempt = 0; attempt < ScatterSpacing.TRIES; attempt++)
                {
                    (float cx, float cz) = Propose(minR, maxR, clusterShare, spread);
                    float clearance = ScatterSpacing.Clearance(cx, cz, halfWidth, standing);
                    if (clearance > bestClearance)
                    {
                        bestClearance = clearance;
                        x = cx;
                        z = cz;
                    }
                    if (clearance >= 0f) break;
                }
                standing.Add(new ScatterSpacing.Footprint(x, z, halfWidth));
                return (x, z);
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
                    treeInstances[variant].Add(Plant(x, z, treeScale, lean, 0f, dryness, Jitter()));
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
                baobabInstances[variant].Add(Plant(x, z, s, 0.03f * (float)rng.NextDouble(), 0f, (float)rng.NextDouble(), Jitter()));
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
