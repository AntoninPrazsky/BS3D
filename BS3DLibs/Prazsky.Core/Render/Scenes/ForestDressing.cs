using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// What the daytime forest's wood wears on High and Ultra (#962), drawn by its backdrop through the plant material
    /// (<c>Acacia.fx</c>, <see cref="PlantPass"/>) over the planting the hosts' <see cref="ForestScatterRenderer"/> draws the
    /// trees of. Every bucket is <see cref="ScatterBucket.FineMeshes"/> true, so Low and Medium draw none of it and keep the
    /// wood as it was.
    /// <list type="bullet">
    /// <item><b>The broadleaves in leaf</b> (<see cref="MeadowTreeMesh.ForestInLeaf"/>, #751's broadleaf half): the very tree Low
    /// and Medium draw with solid clumps, grown here with the meadow's twigs and broad-leaf cards (#697) instead, the grown wood
    /// (#889) on the ones near the arena and the plain wood past them. The renderer draws none of the broadleaves on these
    /// tiers (its <c>broadleavesInLeaf</c>).</item>
    /// <item><b>The ferns</b> (<see cref="FernClump"/>): clumps in damp patches over the floor and at the feet of the trees round
    /// the arena, so a trunk stands in undergrowth rather than on bare painted ground.</item>
    /// </list>
    /// </summary>
    public sealed class ForestDressing : IDisposable
    {
        private readonly List<IDisposable> _owned = new();

        /// <summary>Every bucket, in draw order: the wood first, then the cards.</summary>
        public ScatterBucket[] Buckets { get; }

        /// <summary>How many fern clumps the floor carries.</summary>
        public int Ferns { get; }

        /// <summary>How many triangles the dressing draws a frame, at the tiers that draw it.</summary>
        public long Triangles { get; }

        //The broadleaves' bark (linear): a beech's grey taken warm, because the plant material lights it with the dusk dome's
        //violet and teal at full strength and a neutral grey came out lavender beside the spruces' brown trunks
        private static readonly Vector3 BARK = new(0.075f, 0.058f, 0.042f);

        //The leaves against the config's foliage colour, which Low and Medium's solid crowns are drawn in
        private const float LEAF_SHADE = 0.75f;

        //The ferns (linear): a fresh green, and the rust of the old fronds the references mix among them
        private static readonly Vector3 FERN = new(0.032f, 0.072f, 0.020f);
        private static readonly Vector3 FERN_DRY = new(0.075f, 0.052f, 0.022f);

        //Fern patches: how many, how far from the arena, how wide, how many clumps a patch, and the share of the trees out to
        //FERN_FEET_REACH that stand in a fern or two
        private const int FERN_PATCHES = 80, FERN_PATCH_MIN = 6, FERN_PATCH_SPREAD = 16, FERN_VARIANTS = 3;
        private const float FERN_INNER = 34f, FERN_OUTER = 260f, FERN_PATCH_RADIUS = 9f;
        private const float FERN_AT_FEET = 0.5f, FERN_FEET_REACH = 150f;

        //And the island's foot (#983): clumps on the bank against the drum, how many round the ring, and how far out from the
        //drum's face they stand, nearest and the span past it
        private const int FERN_FOOT_CLUMPS = 14;
        private const float FERN_FOOT_FROM = 0.5f, FERN_FOOT_SPAN = 2.6f;

        public ForestDressing(GraphicsDevice device, ForestSceneConfig config, ForestScatter planting, int seed)
        {
            ForestTreeConfig trees = config.Trees;
            Func<float, float, float> ground = (x, z) => TerrainMirror.Forest(x, z, config);
            var buckets = new List<ScatterBucket>();
            var cards = new List<ScatterBucket>();
            Vector3 foliage = trees.FoliageColor.ToVector3();

            //--- The broadleaves in leaf
            for (int v = 0; v < ForestScatterRenderer.BROADLEAF_SHAPES.Length && v < planting.FineBroadleaves.Length; v++)
            {
                ForestScatterRenderer.BroadleafVariant variant = ForestScatterRenderer.BROADLEAF_SHAPES[v];
                (float trunkRadius, float trunkHeight, float crownRadius, float crownHeight) =
                    ForestScatterRenderer.BroadleafFigures(trees, variant);
                MeadowTreeMesh tree = Own(MeadowTreeMesh.ForestInLeaf(device, trunkRadius, trunkHeight, crownRadius, crownHeight, variant.Seed));

                ForestScatterRenderer.SplitByDistance(new[] { planting.FineBroadleaves[v] }, ForestScatterRenderer.FINE_NEAR_RADIUS,
                    out ModelInstance[][] near, out ModelInstance[][] far);
                List<ModelInstance> nearAt = Dressed(near[0], ground, seed), farAt = Dressed(far[0], ground, seed);

                //A shade under the solid crowns' colour: the plant material lights a card on both faces and lets the sky through it
                Vector3 leaf = foliage * ForestScatterRenderer.BroadleafTint(v) * LEAF_SHADE;
                if (nearAt.Count > 0)
                {
                    buckets.Add(Own(new ScatterBucket(device, tree.FineWood, nearAt, BARK, BARK * 1.15f, dapple: 0f, bark: 0.45f,
                        detailOnly: false, fineMeshes: true)));
                    //The twigs every clump's leaves grow from: bark, too thin to cast anything worth a second draw, and drawn
                    //only where a tree is near enough for a twig to be more than a pixel
                    buckets.Add(Own(new ScatterBucket(device, tree.Twigs, nearAt, BARK, BARK * 1.15f, dapple: 0f, bark: 0.3f,
                        detailOnly: false, castsShadow: false, fineMeshes: true)));
                }
                if (farAt.Count > 0)
                    buckets.Add(Own(new ScatterBucket(device, tree.Wood, farAt, BARK, BARK * 1.15f, dapple: 0f, bark: 0.45f,
                        detailOnly: false, fineMeshes: true)));
                //The leaves cast the crown's shade, cut by their own leaves (the meadow's #697), round the arena; past it the wood
                //alone casts, since the alpha-tested leaves of every crown in the sun's map cost half a millisecond (#962, measured)
                Vector3 leafDry = leaf * new Vector3(1.35f, 1.1f, 0.8f);
                if (nearAt.Count > 0)
                    cards.Add(Own(new ScatterBucket(device, tree.Leaves, nearAt, leaf, leafDry, dapple: 0f, bark: 0f, detailOnly: false,
                        leaves: 2f, fineMeshes: true)));
                if (farAt.Count > 0)
                    cards.Add(Own(new ScatterBucket(device, tree.Leaves, farAt, leaf, leafDry, dapple: 0f, bark: 0f, detailOnly: false,
                        leaves: 2f, castsShadow: false, fineMeshes: true)));
            }

            //--- The ferns
            List<ModelInstance>[] ferns = PlantFerns(planting, ground, seed);
            for (int k = 0; k < FERN_VARIANTS; k++)
            {
                if (ferns[k].Count == 0) continue;
                (List<VertexPositionNormalTexture> vertices, List<int> indices) = FernClump.Build(seed * 31 + 9620 + k);
                var mesh = Own(new UploadedMesh(device, vertices, indices, new BoundingSphere(new Vector3(0f, 0.5f, 0f), 1.6f)));
                cards.Add(Own(new ScatterBucket(device, mesh, ferns[k], FERN, FERN_DRY, dapple: 0f, bark: 0f, detailOnly: false,
                    leaves: 1f, castsShadow: false, fineMeshes: true)));
                Ferns += ferns[k].Count;
            }

            buckets.AddRange(cards);
            Buckets = buckets.ToArray();
            foreach (ScatterBucket bucket in Buckets) Triangles += (long)bucket.Mesh.PrimitiveCount * bucket.Count;
        }

        //The planting's instances as the plant material takes them: each with its own dryness and brightness off a hash of where
        //it stands (one variant is several shades), and the ground at its foot above its pivot, which the bark's foot reads
        private static List<ModelInstance> Dressed(ModelInstance[] placed, Func<float, float, float> ground, int seed)
        {
            var dressed = new List<ModelInstance>(placed.Length);
            foreach (ModelInstance instance in placed)
            {
                Matrix world = instance.World;
                float x = world.M41, z = world.M43;
                dressed.Add(new ModelInstance(world, new Vector4(Hash(x, z, seed), Hash(z, x, seed + 7) * 0.24f - 0.12f,
                    ground(x, z) - world.M42, 0f)));
            }
            return dressed;
        }

        private static float Hash(float x, float z, int seed)
        {
            float h = MathF.Sin(x * 12.9898f + z * 78.233f + seed * 0.618f) * 43758.5453f;
            return h - MathF.Floor(h);
        }

        //The ferns: clumps in patches over the floor (the damp hollows the references grow them in), one or two at the feet
        //of half the trees round the arena, and clumps on the island's bank against the drum (#983), standing on the bank's
        //own surface. Off a stream of their own, so nothing else moves; the bank's come last, so the floor's are as they were.
        private static List<ModelInstance>[] PlantFerns(ForestScatter planting, Func<float, float, float> ground, int seed)
        {
            var rng = new Random(unchecked(seed * 977 + 9621));
            var ferns = new List<ModelInstance>[FERN_VARIANTS];
            for (int k = 0; k < FERN_VARIANTS; k++) ferns[k] = new List<ModelInstance>();

            void Add(float x, float z, float scale)
            {
                if (x * x + z * z < FERN_INNER * FERN_INNER) return;
                AddOn(x, ground(x, z), z, scale);
            }

            void AddOn(float x, float groundY, float z, float scale)
            {
                float yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                float y = groundY - 0.04f * scale;
                ferns[rng.Next(FERN_VARIANTS)].Add(new ModelInstance(
                    Matrix.CreateScale(scale) * Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(x, y, z),
                    new Vector4((float)rng.NextDouble(), (float)rng.NextDouble() * 0.3f - 0.15f, 0.04f * scale, 0f)));
            }

            for (int p = 0; p < FERN_PATCHES; p++)
            {
                float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                float r = MathF.Sqrt(MathHelper.Lerp(FERN_INNER * FERN_INNER, FERN_OUTER * FERN_OUTER, (float)rng.NextDouble()));
                float cx = MathF.Cos(a) * r, cz = MathF.Sin(a) * r;
                int count = FERN_PATCH_MIN + rng.Next(FERN_PATCH_SPREAD);
                for (int i = 0; i < count; i++)
                {
                    float d = FERN_PATCH_RADIUS * MathF.Sqrt((float)rng.NextDouble());
                    float b = (float)rng.NextDouble() * MathHelper.TwoPi;
                    Add(cx + MathF.Cos(b) * d, cz + MathF.Sin(b) * d, 1.1f + 0.8f * (float)rng.NextDouble());
                }
            }

            void AtFeet(ModelInstance[][] buckets)
            {
                foreach (ModelInstance[] bucket in buckets)
                    foreach (ModelInstance tree in bucket)
                    {
                        float x = tree.World.M41, z = tree.World.M43;
                        if (x * x + z * z > FERN_FEET_REACH * FERN_FEET_REACH || rng.NextDouble() > FERN_AT_FEET) continue;
                        float scale = new Vector3(tree.World.M11, tree.World.M12, tree.World.M13).Length();
                        int n = 1 + rng.Next(2);
                        for (int i = 0; i < n; i++)
                        {
                            float b = (float)rng.NextDouble() * MathHelper.TwoPi;
                            float d = (1.3f + 1.6f * (float)rng.NextDouble()) * MathF.Max(scale, 0.5f);
                            Add(x + MathF.Cos(b) * d, z + MathF.Sin(b) * d, 1.0f + 0.7f * (float)rng.NextDouble());
                        }
                    }
            }
            AtFeet(planting.FineConifers);
            AtFeet(planting.FineBroadleaves);

            //The bank's: a clump every so often round the ring, each a few fronds close together against the stone. The bank is
            //built off the same ground and the same seed (ForestBackdrop), so SurfaceY is the very surface it draws
            for (int c = 0; c < FERN_FOOT_CLUMPS; c++)
            {
                float a = (c + 0.6f * (float)rng.NextDouble()) * MathHelper.TwoPi / FERN_FOOT_CLUMPS;
                int n = 4 + rng.Next(5);
                for (int i = 0; i < n; i++)
                {
                    float b = a + ((float)rng.NextDouble() - 0.5f) * 0.18f;
                    float r = ArenaIsland.RADIUS + FERN_FOOT_FROM + (float)rng.NextDouble() * FERN_FOOT_SPAN;
                    float x = MathF.Cos(b) * r, z = MathF.Sin(b) * r;
                    AddOn(x, MathF.Max(ground(x, z), IslandBerm.SurfaceY(ground, seed, x, z)), z, 1.2f + 0.8f * (float)rng.NextDouble());
                }
            }
            return ferns;
        }

        private T Own<T>(T item) where T : IDisposable
        {
            _owned.Add(item);
            return item;
        }

        public void Dispose()
        {
            foreach (IDisposable item in _owned) item.Dispose();
            _owned.Clear();
        }
    }
}
