using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The forest's scattered trees, rocks and stumps: where each one stands, planted on the terrain the
    /// <see cref="ForestSceneConfig"/> forest shader draws. A seeded generator (the same seed gives the same
    /// forest), the same clumped scatter the savanna's acacias use — most plants gather around a set of
    /// cluster centres so the forest reads as groves rather than a regular grid, with a few solitaries — and
    /// everything kept clear of the island the arena stands on.
    /// <para>
    /// The output is three instance arrays, one per object kind, because each kind is its own instanced draw
    /// (its own mesh and its own tint — a per-draw uniform, not per-instance). Trees share one array between
    /// their trunk and crown renders: both draws use the same world matrices, since the crown mesh is built
    /// sitting on the trunk's top.
    /// </para>
    /// </summary>
    public sealed class ForestScatter
    {
        /// <summary>Trees: one instance per tree. Drawn by both the trunk and the crown renderer.</summary>
        public ModelInstance[] Trees { get; }

        /// <summary>Rocks: one instance per boulder.</summary>
        public ModelInstance[] Rocks { get; }

        /// <summary>Stumps: one instance per cut stump.</summary>
        public ModelInstance[] Stumps { get; }

        //No neighbouring-cell occlusion; the instanced shader still expects the vector (W=1 = fully open).
        private static readonly Vector4 NO_OCCLUSION = new(0f, 0f, 0f, 1f);

        /// <param name="seed">Scatter seed; the same seed always gives the same forest.</param>
        /// <param name="config">The forest's scatter configuration (counts, sizes, cluster count, radii).</param>
        /// <param name="terrainHeight">The forest floor height at a world XZ point — mirrors Forest.fx's
        /// TerrainHeight (see <see cref="SceneRenderer.ForestTerrainHeight"/>), so trees are planted on the
        /// ground the shader draws rather than floating or buried.</param>
        public ForestScatter(int seed, ForestSceneConfig config, Func<float, float, float> terrainHeight)
        {
            ForestTreeConfig trees = config.Trees;
            ForestRockConfig rocks = config.Rocks;
            ForestStumpConfig stumps = config.Stumps;

            Random rng = new(seed);

            Trees = Scatter(trees.Count, trees.MinRadius, trees.MaxRadius, trees.Clusters, trees.ClusterSpread,
                trees.MinScale, trees.MaxScale, terrainHeight, rng);
            Rocks = Scatter(rocks.Count, rocks.MinRadius, rocks.MaxRadius, rocks.Clusters, rocks.ClusterSpread,
                rocks.MinScale, rocks.MaxScale, terrainHeight, rng);
            Stumps = Scatter(stumps.Count, stumps.MinRadius, stumps.MaxRadius, stumps.Clusters, stumps.ClusterSpread,
                stumps.MinScale, stumps.MaxScale, terrainHeight, rng);
        }

        //One clumped scatter of <count> instances between <minRadius> and <maxRadius> from the world origin,
        //kept clear of the island. Most instances clump around a cluster centre (so the scatter reads as
        //groves), a minority stand solo. Each is scaled within <minScale>..<maxScale> and given a random yaw,
        //then planted on the terrain. Deterministic off the shared rng, so the order of the three Scatter
        //calls is the only thing that decides which instance lands where.
        private static ModelInstance[] Scatter(int count, float minRadius, float maxRadius,
            int clusters, float clusterSpread, float minScale, float maxScale,
            Func<float, float, float> terrainHeight, Random rng)
        {
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

            ModelInstance[] instances = new ModelInstance[count];
            for (int i = 0; i < count; i++)
            {
                float x, z;
                if (rng.NextDouble() < 0.82f) //most instances clump around a cluster centre
                {
                    int c = rng.Next(clusters);
                    float off = (float)rng.NextDouble();
                    float d = off * off * clusterSpread; //denser towards the centre
                    float da = (float)rng.NextDouble() * MathHelper.TwoPi;
                    x = clusterX[c] + MathF.Cos(da) * d;
                    z = clusterZ[c] + MathF.Sin(da) * d;
                }
                else //the odd solitary instance, anywhere in the ring
                {
                    float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                    float r = minRadius + (float)rng.NextDouble() * (maxRadius - minRadius);
                    x = MathF.Cos(a) * r;
                    z = MathF.Sin(a) * r;
                }

                //Keep clear of the island the arena stands on: push anything that fell inside the inner
                //radius back out to it, rather than rejecting it (the count is what it is).
                float dist = MathF.Sqrt(x * x + z * z);
                if (dist < minRadius && dist > 0.01f)
                {
                    x *= minRadius / dist;
                    z *= minRadius / dist;
                }

                float scale = minScale + (float)rng.NextDouble() * (maxScale - minScale);
                float yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                float y = terrainHeight(x, z);

                //Scale, then yaw, then translate: the mesh's base sits at Y=0, so translating by the terrain
                //height plants it on the ground. Non-uniform scale is avoided (the meshes are not all
                //axis-aligned boxes), so each instance scales uniformly — a forest's variety comes from the
                //scale range and the per-tree wobble, not from stretching.
                Matrix world = Matrix.CreateScale(scale) * Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(x, y, z);
                instances[i] = new ModelInstance(world, NO_OCCLUSION);
            }

            return instances;
        }
    }
}
