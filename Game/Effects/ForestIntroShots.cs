using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// The forest's own shots for a chapter intro's prologue (#559): the wood from over its crowns with the
    /// clearing and the arena in it, a dolly in at eye height on its thickest grove, and a crane up the tallest tree beside its
    /// crown — cut together, then cut to the tour's last leg. The spline it replaces flew a circle round the
    /// clearing at stand-off height and never went in among the trees the scene is named for.
    /// <para>
    /// <b>Built off the planting on screen</b> (<see cref="ForestScatterRenderer.Scatter"/>, rolled per launch
    /// and pinned by <c>sceneseed=</c>), as the aurora's wood shot is off its own. Each tree is taken as a column
    /// of its crown's radius from the root to its top, at its own mesh variant's proportions
    /// (<see cref="ForestScatterRenderer.ConiferMeshes"/>) and the instance's scale, widened for the lobes and
    /// the lean — so a path held clear of the columns is clear of the needles. Rocks, stumps, snags and logs
    /// are columns too. Heights come off <see cref="SceneRenderer.ForestTerrainHeight"/>, the mirror the
    /// trees were planted on. Built once when the intro begins; nothing here runs per frame.
    /// </para>
    /// </summary>
    internal static class ForestIntroShots
    {
        //How far a crown's lobes and whorls wander past its authored radius, and the lean ForestScatter gives
        //a trunk (TREE_LEAN), as reach at the top.
        private const float LOBES = 1.15f;
        private const float LEAN = 0.075f;

        //The snags' tallest variant (ForestScatterRenderer builds one at 1.3 of the length).
        private const float TALLEST_SNAG = 1.3f;

        //The forest: the establishing dolly in over the crowns towards the clearing.
        private const float FOREST_FROM = 210f;
        private const float FOREST_TO = 170f;
        private const float FOREST_ABOVE_FROM = 44f;
        private const float FOREST_ABOVE_TO = 36f;
        private const float FOREST_SECONDS = 3.4f;

        //The grove: a slow dolly in at eye height towards the thickest grove of the planting, the lens on its
        //heart, from this much air outside its outermost crown to a few units nearer — trunks and crowns
        //filling the frame. ⚠ A run THROUGH the wood (the aurora's shot, #531) was tried first and does not
        //carry here: this wood is 240 trees over a disc 340 in radius, one per 1 500 square units, and of 240
        //rolled runs the few that cleared every crown had five trees within twelve units — a slope with a
        //skyline of crowns, not a wood. The groves are where the trees are, and the shot goes to one.
        private const float GROVE_REACH = 22f;
        private const float GROVE_AIR = 9f;
        private const float GROVE_DOLLY = 8f;
        private const float GROVE_ABOVE = 2.4f;
        private const float GROVE_SECONDS = 3.4f;

        //The tree: a crane from the floor to over the crown of one of the tallest trees, this much air off its
        //crown's edge, the lens on the crown.
        private const float TREE_AIR = 4.5f;
        private const float TREE_ABOVE_FROM = 1.8f;
        private const float TREE_OVER_TOP = 3f;
        private const float TREE_SECONDS = 3.2f;

        //Two units off every column: one, and the first cut's run passed a unit under a broadleaf's crown,
        //whose underside filled a third of the frame as a black mass.
        private const float MARGIN = 2f;
        private const float ABOVE_GROUND = 1.2f;
        private const int TRIES = 24;

        private readonly struct Tree(Vector3 root, float top, float crown, bool broadleaf)
        {
            public readonly Vector3 Root = root;
            public readonly float Top = top;
            public readonly float Crown = crown;
            public readonly bool Broadleaf = broadleaf;
        }

        /// <summary>The prologue for the forest on screen, or null when there is no wood.</summary>
        public static IntroShot[] Build(ForestScatterRenderer wood, ForestSceneConfig forest, float fieldOfView, Random random)
        {
            if (wood?.Scatter == null || forest == null) return null;

            ForestScatter scatter = wood.Scatter;
            var ground = new IntroGround((x, z) => SceneRenderer.ForestTerrainHeight(x, z, forest));
            var trees = new List<Tree>();

            void Trees(ModelInstance[][] buckets, IReadOnlyList<TreeMesh> meshes, bool broadleaf)
            {
                for (int m = 0; m < buckets.Length && m < meshes.Count; m++)
                    foreach (ModelInstance tree in buckets[m])
                    {
                        float scale = Scale(tree.World);
                        float top = meshes[m].Height * scale;
                        float radius = meshes[m].CrownRadius * LOBES * scale + top * LEAN;
                        Vector3 root = tree.World.Translation;
                        ground.Add(root - Vector3.Up, root + Vector3.Up * top, radius);
                        trees.Add(new Tree(root, top, radius, broadleaf));
                    }
            }

            Trees(scatter.Conifers, wood.ConiferMeshes, broadleaf: false);
            Trees(scatter.Broadleaves, wood.BroadleafMeshes, broadleaf: true);

            //A snag leans far more than a living tree (SNAG_LEAN, 0.28): its column is as wide as that lean
            //carries its top.
            Columns(ground, scatter.Snags, forest.Snags.Length * TALLEST_SNAG, forest.Snags.Length * TALLEST_SNAG * 0.3f);
            Columns(ground, scatter.Logs, forest.Logs.Radius * 3f, forest.Logs.Length * 0.6f);
            Columns(ground, scatter.Rocks, forest.Rocks.Radius * 2f, forest.Rocks.Radius * 1.3f);
            Columns(ground, scatter.Stumps, 2f, 1.5f);

            IntroShot establishing = ground.Establishing("the forest", FOREST_FROM, FOREST_TO, FOREST_ABOVE_FROM, FOREST_ABOVE_TO,
                FOREST_SECONDS, fieldOfView * 1.1f, random, TRIES, MARGIN);

            return IntroGround.Cut(establishing, Grove(ground, trees, fieldOfView, random), Tall(ground, trees, fieldOfView, random));
        }

        /// <summary>A slow dolly in at eye height towards the thickest grove, the lens on its heart.</summary>
        private static IntroShot Grove(IntroGround ground, List<Tree> trees, float fieldOfView, Random random)
        {
            //The trees by how many others stand within a grove's reach of them, thickest first.
            var order = new List<(int Index, int Grove)>(trees.Count);
            for (int i = 0; i < trees.Count; i++)
            {
                int grove = 0;
                for (int j = 0; j < trees.Count; j++)
                    if (j != i && Vector3.Distance(trees[i].Root, trees[j].Root) < GROVE_REACH) grove++;
                order.Add((i, grove));
            }
            order.Sort((a, b) => b.Grove.CompareTo(a.Grove));

            for (int n = 0; n < Math.Min(order.Count, 12); n++)
            {
                //The grove's heart is the middle of the trees round the seed tree, and its edge the furthest
                //crown from that heart.
                Vector3 seed = trees[order[n].Index].Root;
                Vector3 heart = Vector3.Zero;
                int members = 0;
                foreach (Tree tree in trees)
                    if (Vector3.Distance(tree.Root, seed) < GROVE_REACH) { heart += tree.Root; members++; }
                heart /= members;

                float edge = 0f;
                float crowns = 0f;
                foreach (Tree tree in trees)
                    if (Vector3.Distance(tree.Root, seed) < GROVE_REACH)
                    {
                        edge = MathF.Max(edge, Vector2.Distance(new Vector2(tree.Root.X, tree.Root.Z), new Vector2(heart.X, heart.Z)) + tree.Crown);
                        crowns += tree.Top;
                    }

                Vector3 look = heart + Vector3.Up * (0.45f * crowns / members);
                Vector2 centre = new(heart.X, heart.Z);

                for (int attempt = 0; attempt < 10; attempt++)
                {
                    Vector2 away = IntroGround.Heading((float)random.NextDouble() * MathHelper.TwoPi);
                    Vector3[] path = ground.Hug(centre + away * (edge + GROVE_AIR + GROVE_DOLLY), centre + away * (edge + GROVE_AIR),
                        GROVE_ABOVE, GROVE_ABOVE, IntroGround.PATH_POINTS);
                    if (!ground.Clear(path, MARGIN, ABOVE_GROUND)) continue;

                    return new IntroShot("the grove", path, GROVE_SECONDS, fieldOfView * 1.15f, lookAt: look);
                }
            }

            return null;
        }

        /// <summary>A crane from the floor to over the crown of one of the tallest trees, the lens on the crown.</summary>
        private static IntroShot Tall(IntroGround ground, List<Tree> trees, float fieldOfView, Random random)
        {
            if (trees.Count == 0) return null;

            var order = new List<int>(trees.Count);
            for (int i = 0; i < trees.Count; i++) order.Add(i);
            order.Sort((a, b) => trees[b].Top.CompareTo(trees[a].Top));

            for (int n = 0; n < Math.Min(order.Count, 16); n++)
            {
                Tree tree = trees[order[n]];
                float distance = tree.Crown + TREE_AIR;
                Vector3 look = tree.Root + Vector3.Up * (tree.Top * (tree.Broadleaf ? 0.7f : 0.62f));

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    Vector2 away = IntroGround.Heading((float)random.NextDouble() * MathHelper.TwoPi);
                    Vector2 plan = new Vector2(tree.Root.X, tree.Root.Z) + away * distance;
                    Vector2 back = plan + away * 3f;

                    //Up from the floor to a little over the top, drawing back a few units as it rises.
                    float floor = ground.Height(plan.X, plan.Y);
                    Vector3[] path = ground.Hug(plan, back, TREE_ABOVE_FROM, tree.Root.Y + tree.Top + TREE_OVER_TOP - floor, IntroGround.PATH_POINTS);
                    if (!ground.Clear(path, MARGIN, ABOVE_GROUND)) continue;

                    return new IntroShot("the tree", path, TREE_SECONDS, fieldOfView * 1.15f, lookAt: look);
                }
            }

            return null;
        }

        private static void Columns(IntroGround ground, ModelInstance[][] buckets, float height, float radius)
        {
            foreach (ModelInstance[] bucket in buckets)
                foreach (ModelInstance thing in bucket)
                {
                    float scale = Scale(thing.World);
                    Vector3 root = thing.World.Translation;
                    ground.Add(root - Vector3.Up, root + Vector3.Up * (height * scale), radius * scale);
                }
        }

        private static float Scale(Matrix world) => new Vector3(world.M11, world.M12, world.M13).Length();
    }
}
