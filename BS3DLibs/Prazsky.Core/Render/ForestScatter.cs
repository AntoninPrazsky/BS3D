using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The forest's scattered trees, rocks and stumps: where each one stands, planted on the terrain the
    /// <see cref="ForestSceneConfig"/> forest shader draws. A seeded generator (the same seed gives the same
    /// forest), the same clumped scatter the savanna's acacias use — most plants gather around a set of
    /// cluster centres so the forest reads as groves rather than a regular grid, with a few solitaries — and
    /// everything kept clear of the island the arena stands on.
    /// <para>
    /// The output is one instance array per object kind (two tree species, rocks, stumps) <b>per mesh
    /// variant</b>, because each variant is its own instanced draw (its own mesh, and its own tint — a
    /// per-draw uniform, not per-instance). A kind with several variants is what stops the eye reading a
    /// grove as one tree stamped out fifty times: the caller builds the same species at a few sets of
    /// proportions and this hands back which instances belong to each. Every variant of a species is scattered
    /// in the one pass and only then split, so the variants mix inside each grove rather than forming groves
    /// of their own. A species' array is shared between its trunk and crown renders: both draws use the same
    /// world matrices, since the crown mesh is built sitting on the trunk's top.
    /// </para>
    /// </summary>
    public sealed class ForestScatter
    {
        /// <summary>Conifers by mesh variant. Drawn by that variant's trunk and crown renderers.</summary>
        public ModelInstance[][] Conifers { get; }

        /// <summary>Broadleaves by mesh variant. Drawn by that variant's trunk and crown renderers.</summary>
        public ModelInstance[][] Broadleaves { get; }

        /// <summary>Boulders by mesh variant.</summary>
        public ModelInstance[][] Rocks { get; }

        /// <summary>Cut stumps by mesh variant.</summary>
        public ModelInstance[][] Stumps { get; }

        //No neighbouring-cell occlusion; the instanced shader still expects the vector (W=1 = fully open).
        private static readonly Vector4 NO_OCCLUSION = new(0f, 0f, 0f, 1f);

        //How far a tree may lean off vertical, in radians. Small: a leaning tree reads as a tree, a tilted
        //one reads as a felled one. Rocks take their own, much larger tumble below.
        private const float TREE_LEAN = 0.075f;

        //How far a boulder may lie tipped. A glacial erratic lies however it came to rest, and tumbling it
        //is what stops a scatter of one dome mesh reading as a field of identical mushrooms.
        private const float ROCK_TUMBLE = 0.35f;

        //A stump was cut where it grew, so it keeps a tree's modest lean.
        private const float STUMP_LEAN = 0.07f;

        /// <summary>
        /// The horizontal radius of each kind at scale 1, which is what two instances have to clear between
        /// them. Taken from the crown radii the meshes are actually built at
        /// (<c>ForestTreeConfig.CrownRadius</c> 3.1 and <c>ConiferCrownRadius</c> 2.6) rather than guessed —
        /// the crown is the widest part of a tree and the only part whose overlap the eye catches.
        /// </summary>
        private const float BROADLEAF_FOOTPRINT = 3.1f;

        /// <inheritdoc cref="BROADLEAF_FOOTPRINT"/>
        private const float CONIFER_FOOTPRINT = 2.6f;

        /// <inheritdoc cref="BROADLEAF_FOOTPRINT"/>
        private const float ROCK_FOOTPRINT = 1.6f;

        /// <inheritdoc cref="BROADLEAF_FOOTPRINT"/>
        private const float STUMP_FOOTPRINT = 1.1f;

        /// <param name="seed">Scatter seed; the same seed always gives the same forest.</param>
        /// <param name="config">The forest's scatter configuration (counts, sizes, cluster count, radii).</param>
        /// <param name="coniferVariants">How many conifer meshes the caller built; the conifers are split
        /// evenly and at random between them.</param>
        /// <param name="broadleafVariants">How many broadleaf meshes the caller built.</param>
        /// <param name="rockVariants">How many boulder meshes the caller built.</param>
        /// <param name="stumpVariants">How many stump meshes the caller built.</param>
        /// <param name="terrainHeight">The forest floor height at a world XZ point — mirrors Forest.fx's
        /// TerrainHeight (see <see cref="SceneRenderer.ForestTerrainHeight"/>), so trees are planted on the
        /// ground the shader draws rather than floating or buried.</param>
        public ForestScatter(int seed, ForestSceneConfig config,
            int coniferVariants, int broadleafVariants, int rockVariants, int stumpVariants,
            Func<float, float, float> terrainHeight)
        {
            ForestTreeConfig trees = config.Trees;
            ForestRockConfig rocks = config.Rocks;
            ForestStumpConfig stumps = config.Stumps;

            Random rng = new(seed);

            //The two species scatter separately, each around its own cluster centres, so the forest reads as
            //spruce groves and broadleaf groves rather than an even salt-and-pepper mix — which is also how a
            //real mixed wood grows.
            int coniferCount = (int)MathF.Round(trees.Count * MathHelper.Clamp(trees.ConiferFraction, 0f, 1f));

            //ONE list across both tree calls, which is the whole point of it being a parameter: the two
            //species are scattered separately (for the groves), so a per-call list would have let every
            //spruce stand inside a broadleaf and only stopped each species colliding with itself. The
            //trees are what the eye counts, so rocks and stumps are left out of it — a boulder or a stump
            //at the foot of a tree is what a wood looks like, and holding them off would push them into
            //the open, which is the worse picture.
            List<ScatterSpacing.Footprint> trunks = new(trees.Count);

            Conifers = Scatter(coniferCount, trees.MinRadius, trees.MaxRadius, trees.Clusters, trees.ClusterSpread,
                trees.MinScale, trees.MaxScale, TREE_LEAN, coniferVariants, terrainHeight, rng,
                CONIFER_FOOTPRINT, trunks);
            Broadleaves = Scatter(trees.Count - coniferCount, trees.MinRadius, trees.MaxRadius, trees.Clusters, trees.ClusterSpread,
                trees.MinScale, trees.MaxScale, TREE_LEAN, broadleafVariants, terrainHeight, rng,
                BROADLEAF_FOOTPRINT, trunks);

            //Their own lists: a pile of boulders in one spot reads as a pile, but a boulder inside another
            //boulder reads as a bug, and the same for stumps.
            Rocks = Scatter(rocks.Count, rocks.MinRadius, rocks.MaxRadius, rocks.Clusters, rocks.ClusterSpread,
                rocks.MinScale, rocks.MaxScale, ROCK_TUMBLE, rockVariants, terrainHeight, rng,
                ROCK_FOOTPRINT, new List<ScatterSpacing.Footprint>(rocks.Count));
            Stumps = Scatter(stumps.Count, stumps.MinRadius, stumps.MaxRadius, stumps.Clusters, stumps.ClusterSpread,
                stumps.MinScale, stumps.MaxScale, STUMP_LEAN, stumpVariants, terrainHeight, rng,
                STUMP_FOOTPRINT, new List<ScatterSpacing.Footprint>(stumps.Count));
        }

        //One clumped scatter of <count> instances between <minRadius> and <maxRadius> from the world origin,
        //kept clear of the island, split at random between <variants> buckets. Most instances clump around a
        //cluster centre (so the scatter reads as groves), a minority stand solo. Each is scaled within
        //<minScale>..<maxScale>, given a random yaw and a lean up to <maxLean> radians, then planted on the
        //terrain. Deterministic off the shared rng, so the order of the four Scatter calls is the only thing
        //that decides which instance lands where — splitting the trees into two species re-ordered that
        //stream, so the same seed no longer plants the rocks and stumps of the single-species forest.
        private static ModelInstance[][] Scatter(int count, float minRadius, float maxRadius,
            int clusters, float clusterSpread, float minScale, float maxScale, float maxLean,
            int variants, Func<float, float, float> terrainHeight, Random rng,
            float footprint, List<ScatterSpacing.Footprint> occupied)
        {
            if (variants < 1) throw new ArgumentOutOfRangeException(nameof(variants));

            //Cluster centres the scatter gathers around, scattered across the ring themselves.
            float[] clusterX = new float[clusters];
            float[] clusterZ = new float[clusters];
            for (int c = 0; c < clusters; c++)
            {
                float ca = (float)rng.NextDouble() * MathHelper.TwoPi;
                float cr = minRadius + (float)rng.NextDouble() * (maxRadius - minRadius);
                clusterX[c] = MathF.Cos(ca) * cr;
                clusterZ[c] = MathF.Sin(ca) * cr;
            }

            //Filled in one pass and only then cut into per-variant arrays: the buckets have to be exactly as
            //long as what they hold, since InstancedModelRenderer draws a contiguous prefix of the array it
            //is handed.
            var all = new ModelInstance[count];
            var variantOf = new int[count];
            var perVariant = new int[variants];

            for (int i = 0; i < count; i++)
            {
                float scale = minScale + (float)rng.NextDouble() * (maxScale - minScale);
                float wantRadius = footprint * scale;

                //Try a few positions and keep the roomiest. Nothing here rejects an instance outright: the
                //count is authored, and a forest that quietly plants 180 of the 240 trees it was asked for
                //would be a worse bug than the overlap this exists to stop (#108). Before this there was no
                //separation test at all — instances were placed independently, and since 82% of them clump
                //around a cluster centre with the density rising towards it, two landing on top of each
                //other was the expected case rather than bad luck.
                float x = 0f, z = 0f;
                float bestClearance = float.NegativeInfinity;

                for (int attempt = 0; attempt < ScatterSpacing.TRIES; attempt++)
                {
                    float cx, cz;
                    if (rng.NextDouble() < 0.82f) //most instances clump around a cluster centre
                    {
                        int c = rng.Next(clusters);
                        float off = (float)rng.NextDouble();
                        float d = off * off * clusterSpread; //denser towards the centre
                        float da = (float)rng.NextDouble() * MathHelper.TwoPi;
                        cx = clusterX[c] + MathF.Cos(da) * d;
                        cz = clusterZ[c] + MathF.Sin(da) * d;
                    }
                    else //the odd solitary instance, anywhere in the ring
                    {
                        float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                        float r = minRadius + (float)rng.NextDouble() * (maxRadius - minRadius);
                        cx = MathF.Cos(a) * r;
                        cz = MathF.Sin(a) * r;
                    }

                    //Keep clear of the island the arena stands on: push anything that fell inside the inner
                    //radius back out to it, rather than rejecting it (the count is what it is).
                    float dist = MathF.Sqrt(cx * cx + cz * cz);
                    if (dist < minRadius && dist > 0.01f)
                    {
                        cx *= minRadius / dist;
                        cz *= minRadius / dist;
                    }

                    float clearance = ScatterSpacing.Clearance(cx, cz, wantRadius, occupied);

                    if (clearance > bestClearance)
                    {
                        bestClearance = clearance;
                        x = cx;
                        z = cz;
                    }

                    if (clearance >= 0f) break; //room enough; no need to look further
                }

                occupied.Add(new ScatterSpacing.Footprint(x, z, wantRadius));

                float yaw = (float)rng.NextDouble() * MathHelper.TwoPi;

                //Sunk a little below the sampled height: the floor is lumpy and the sample is one point, so
                //a base planted exactly on it floats off the downhill side of every lump. Sinking reads as
                //ground cover lapping the base; floating reads as a render bug. Leaning sinks a touch more,
                //since tipping a flat base about its centre lifts the edge it tips away from clear of the
                //ground.
                float lean = maxLean * (float)rng.NextDouble();
                float leanAzimuth = (float)rng.NextDouble() * MathHelper.TwoPi;
                float y = terrainHeight(x, z) - (0.15f + lean * 0.9f) * scale;

                //Scale, then yaw about the axis, then lean the axis over, then translate: the mesh's base
                //sits at Y=0, so translating by the terrain height plants it on the ground. The lean is a
                //rotation, so it leaves the normals correct — a non-uniform scale would not (the shader
                //transforms normals by the world matrix itself, with no inverse transpose), which is why the
                //variety in proportions comes from the caller's mesh variants rather than from stretching.
                Matrix world = Matrix.CreateScale(scale)
                    * Matrix.CreateRotationY(yaw)
                    * Matrix.CreateFromAxisAngle(new Vector3(MathF.Cos(leanAzimuth), 0f, MathF.Sin(leanAzimuth)), lean)
                    * Matrix.CreateTranslation(x, y, z);

                all[i] = new ModelInstance(world, NO_OCCLUSION);
                variantOf[i] = rng.Next(variants);
                perVariant[variantOf[i]]++;
            }

            var buckets = new ModelInstance[variants][];
            for (int v = 0; v < variants; v++) buckets[v] = new ModelInstance[perVariant[v]];

            var filled = new int[variants];
            for (int i = 0; i < count; i++)
            {
                int v = variantOf[i];
                buckets[v][filled[v]++] = all[i];
            }

            return buckets;
        }
    }
}
