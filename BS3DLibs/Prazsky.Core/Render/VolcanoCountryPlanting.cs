using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>What stands on the land past the volcano's young field</b> (#748), as <see cref="ScatterBucket"/>s for the planted
    /// scenes' <see cref="PlantPass"/>. The references (<c>C:\Users\panrd\AI\sd\out\748r2-zimage</c>, <c>748r2-klein</c>) stand
    /// the same things on it every time:
    /// <list type="bullet">
    /// <item><b>Snags</b> — the trees the flow burned on its way, standing charred and broken in the scorched band before the
    /// front, with a few fallen ones lying among them (<see cref="SnagMesh"/>, <see cref="DeadwoodMesh"/>, in char).</item>
    /// <item><b>The woods</b> — dark spruce belts where <see cref="VolcanoCountry.ForestCover"/> says (the very mask the ground
    /// darkens to a forest floor by): the forest's <see cref="TreeMesh"/> spruces near, a cheap spire of the same silhouette
    /// past <see cref="NEAR_REACH"/>. High and Ultra add broadleaves along the river and round the village.</item>
    /// <item><b>The village</b> — houses with pitched roofs of tile and slate and a church with a tower, on the river's far
    /// bank, most windows lit warm and a few houses dark.</item>
    /// </list>
    /// Planted by quarters round the arena (<see cref="Quarters"/>), each with the box it fills, so the backdrop draws only the
    /// quarters the camera looks into: from the play pose the land is behind the lens and none of it is drawn. Nothing
    /// casts into the sun's map: the map covers the field round the camera, and the land starts past it. Deterministic off
    /// one seed. Not drawn on the Low tier (<see cref="ScatterBucket.DetailOnly"/>).
    /// </summary>
    public sealed class VolcanoCountryPlanting : IDisposable
    {
        //The forest's spruces (TreeMesh), named so for WindingCheck's trunk-and-crown assembly and declared before the
        //quarters, so that is the path its walk reaches a spruce's open-topped trunk by; and High and Ultra's broadleaves
        //round the village (the banks' are BankTree's, owned with the rest)
        private readonly TreeMesh[] _coniferMeshes;
        private readonly TreeMesh[] _broadleafMeshes;

        /// <summary>One quarter of the land round the arena: its buckets and the box they stand in.</summary>
        public readonly record struct Quarter(ScatterBucket[] Buckets, BoundingBox Bounds);

        /// <summary>The land's four quarters by bearing from the arena, and the village as a fifth.</summary>
        public Quarter[] Quarters { get; }

        private readonly List<IDisposable> _owned = new();


        //How many of each, and how far out from the arena the near spruces reach before the cheap spire takes over (a spruce
        //350 out is a few dozen pixels tall at 4K; past 520 its tiers are under a pixel each)
        private const int SNAGS = 380, LOGS = 90, BLOCKS = 700, NEAR_CONIFERS = 1300, FAR_CONIFERS = 3600, BROADLEAVES = 170, HOUSES = 22;
        private const float NEAR_FROM = 240f, NEAR_REACH = 420f, FAR_REACH = 720f;

        //How many of the broadleaves are the village's own, grown as the forest's are (TreeMesh, four thousand triangles a
        //tree); the rest line the banks as a crown and a stem of a few hundred (BankTree). Grown, the 170 cost the far vantage
        //0.46 ms of the planting's 0.89 (High, docs/scenes/volcano.md)
        private const int VILLAGE_TREES = 40;

        //Where the far spruces thin out to nothing: past it the ground's own forest floor (Volcano.fx's ForestCover) is the
        //wood, which is all a wood is from a kilometre off, and the far ground there is fading into the sky (FarField.fxh),
        //which the plant effect does not do
        private const float FAR_THINNING = 600f;

        //The front's rubble: the field's own basalt and its dusted grey (VolcanoPlanting's)
        private static readonly Vector3 BASALT = new(0.028f, 0.026f, 0.025f);
        private static readonly Vector3 BASALT_DUSTED = new(0.060f, 0.052f, 0.045f);

        //Char: what a burned trunk is, darker than any bark and a shade warm, with the ash on it greyer
        private static readonly Vector3 CHAR = new(0.020f, 0.017f, 0.015f);
        private static readonly Vector3 CHAR_ASHED = new(0.055f, 0.052f, 0.049f);

        //The living woods: the forest scene's spruce under this sky, darker (a dusk wood is near black), and its bark
        private static readonly Vector3 SPRUCE = new(0.020f, 0.042f, 0.020f);
        private static readonly Vector3 SPRUCE_DRY = new(0.032f, 0.040f, 0.022f);
        private static readonly Vector3 BARK = new(0.045f, 0.036f, 0.028f);
        private static readonly Vector3 LEAF = new(0.022f, 0.036f, 0.014f);
        private static readonly Vector3 LEAF_DRY = new(0.042f, 0.042f, 0.020f);

        //The village: lime-washed walls a little dirty, red tile and grey slate, glass that is dark where no lamp is lit
        private static readonly Vector3 PLASTER = new(0.165f, 0.150f, 0.125f);
        private static readonly Vector3 PLASTER_DIRTY = new(0.110f, 0.100f, 0.088f);
        private static readonly Vector3 TILE = new(0.085f, 0.030f, 0.020f);
        private static readonly Vector3 TILE_DRY = new(0.065f, 0.034f, 0.026f);
        private static readonly Vector3 SLATE = new(0.032f, 0.032f, 0.036f);
        private static readonly Vector3 GLASS = new(0.010f, 0.011f, 0.013f);

        /// <param name="height">The volcano's ground (<see cref="TerrainMirror.Volcano(float, float, VolcanoSceneConfig, VolcanoCountry)"/>).</param>
        public VolcanoCountryPlanting(GraphicsDevice device, VolcanoSceneConfig config, VolcanoCountry country,
            Func<float, float, float> height, int seed)
        {
            Random rng = new(seed);
            VolcanoCountryConfig land = config.Country;
            Vector2 cone = config.ConeCenter.ToVector2();
            Vector2 village = land.Village.ToVector2();

            //--- The meshes. Spruces in the forest's proportions (ForestTreeConfig's defaults), three silhouettes of them
            var trees = new ForestTreeConfig();
            (float Width, float Height, int Seed)[] spruces = { (1f, 1f, 11), (0.78f, 1.24f, 23), (1.12f, 0.94f, 53) };
            _coniferMeshes = new TreeMesh[spruces.Length];
            for (int m = 0; m < spruces.Length; m++)
                _coniferMeshes[m] = new TreeMesh(device, TreeSpecies.Conifer, trees.TrunkBaseRadius * spruces[m].Width,
                    trees.TrunkTopRadius * spruces[m].Width, trees.ConiferTrunkHeight * spruces[m].Height,
                    trees.ConiferCrownRadius * spruces[m].Width, trees.ConiferCrownHeight * spruces[m].Height, spruces[m].Seed,
                    coniferTiers: trees.ConiferTiers, coniferTierSpread: trees.ConiferTierSpread, coniferRaggedness: trees.ConiferRaggedness);

            IProceduralMesh[] spires = { Own(SpireMesh(device, 17f, 3.6f, 4, 1.3f)), Own(SpireMesh(device, 20f, 3.1f, 5, 4.1f)) };

            //Low-crowned, as trees in the open grow (an alder on a bank, a lime by a church), not the forest's tall boles: on the
            //forest's figures they stood along the river as crowns on bare sticks
            _broadleafMeshes = new[]
            {
                new TreeMesh(device, TreeSpecies.Broadleaf, trees.TrunkBaseRadius, trees.TrunkTopRadius, trees.TrunkHeight * 0.5f,
                    trees.CrownRadius * 1.1f, trees.CrownHeight * 1.15f, seed: 79),
                new TreeMesh(device, TreeSpecies.Broadleaf, trees.TrunkBaseRadius * 1.15f, trees.TrunkTopRadius * 1.15f, trees.TrunkHeight * 0.5f,
                    trees.CrownRadius * 1.2f, trees.CrownHeight * 0.95f, seed: 97),
            };

            (IProceduralMesh Stem, IProceduralMesh Crown)[] bankTrees = { BankTree(device, 2.6f, 3.4f, 5.0f, 211), BankTree(device, 2.2f, 4.0f, 4.4f, 223) };

            IProceduralMesh[] snags = { Own(new SnagMesh(device, 12f, 0.42f, 131)), Own(new SnagMesh(device, 15f, 0.5f, 137)), Own(new SnagMesh(device, 7f, 0.4f, 139)) };
            IProceduralMesh[] logs = { Own(new DeadwoodMesh(device, 9f, 0.38f, 151)), Own(new DeadwoodMesh(device, 6.5f, 0.32f, 157)) };

            //--- Where everything goes: a quarter by bearing from the arena (the +Z quarter is the far side the play camera
            //turns its back on), each a set of instance lists, and the box round what it holds
            var quarters = new QuarterBuilder[4];
            for (int q = 0; q < quarters.Length; q++) quarters[q] = new QuarterBuilder();
            QuarterBuilder QuarterOf(float x, float z) => quarters[QuarterIndex(x, z)];

            var occupied = new SpacingGrid(12f);

            float Ground(float x, float z, float r) =>
                MathF.Min(height(x, z), MathF.Min(MathF.Min(height(x + r, z), height(x - r, z)), MathF.Min(height(x, z + r), height(x, z - r))));

            float Slope(float x, float z)
            {
                const float e = 2f;
                float dx = height(x + e, z) - height(x - e, z), dz = height(x, z + e) - height(x, z - e);
                return MathF.Sqrt(dx * dx + dz * dz) / (2f * e);
            }

            //A point at a random bearing round the arena between two radii, uniform by area
            (float X, float Z) Around(float from, float to)
            {
                float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                float d = MathF.Sqrt(MathHelper.Lerp(from * from, to * to, (float)rng.NextDouble()));
                return (MathF.Cos(a) * d, MathF.Sin(a) * d);
            }

            //--- The snags: thickest at the front and thinning out over the scorched band, in stands (a fire takes a wood
            //by the stand, not tree by tree), leaning as burned trunks do
            for (int i = 0, tries = 0; i < SNAGS && tries < SNAGS * 60; tries++)
            {
                (float x, float z) = Around(NEAR_FROM, FAR_REACH);
                float outside = country.Outside(x, z);
                float stand = 0.5f + 0.5f * ShaderMath.Noise(new Vector2(x * 0.02f + 5.1f, z * 0.02f + 5.1f));
                float want = (1f - ShaderMath.SmoothStep(6f, 70f, outside)) * ShaderMath.SmoothStep(4f, 8f, outside) * stand;
                if ((float)rng.NextDouble() > want || Slope(x, z) > 0.5f) continue;
                if (MathF.Abs(country.River(x, z).Across) < land.RiverHalfWidth + 6f) continue;
                if (occupied.Clearance(x, z, 0.8f) < 0f) continue;
                occupied.Add(x, z, 0.8f);

                int variant = rng.Next(snags.Length);
                float scale = 0.7f + 0.6f * (float)rng.NextDouble();
                Matrix world = Matrix.CreateScale(scale) * Matrix.CreateRotationX(((float)rng.NextDouble() - 0.5f) * 0.25f)
                    * Matrix.CreateRotationZ(((float)rng.NextDouble() - 0.5f) * 0.25f) * Matrix.CreateRotationY((float)rng.NextDouble() * MathHelper.TwoPi)
                    * Matrix.CreateTranslation(x, Ground(x, z, 0.6f) - 0.3f, z);
                QuarterOf(x, z).Add(snags[variant], new ModelInstance(world, new Vector4((float)rng.NextDouble(), ((float)rng.NextDouble() - 0.5f) * 0.3f, 0.3f, 0f)), x, z, 15f * scale);
                i++;
            }

            //--- The front's rubble: blocks tumbled off the flow's margin and lying along its foot and up its face, most of
            //them small and a few as big as a cart - aa clinker, the field's own broken basalt (VolcanoPlanting.BlockMesh). The
            //geometry's front is one smooth step; the rubble is what breaks its line into the jumble the references draw
            var blocks = new IProceduralMesh[4];
            for (int m = 0; m < blocks.Length; m++) blocks[m] = Own(VolcanoPlanting.BlockMesh(device, rng));
            for (int i = 0, tries = 0; i < BLOCKS && tries < BLOCKS * 60; tries++)
            {
                (float x, float z) = Around(NEAR_FROM - 40f, FAR_REACH + 150f);
                float outside = country.Outside(x, z);
                if (outside < -VolcanoCountry.FRONT_LIP - 4f || outside > VolcanoCountry.FRONT_FOOT + 9f) continue;
                if (MathF.Abs(country.River(x, z).Across) < land.RiverHalfWidth + 6f) continue;
                float size = MathHelper.Lerp(0.6f, 4.5f, MathF.Pow((float)rng.NextDouble(), 3f));
                Matrix world = Matrix.CreateScale(size) * Matrix.CreateRotationX(((float)rng.NextDouble() - 0.5f) * 0.6f)
                    * Matrix.CreateRotationY((float)rng.NextDouble() * MathHelper.TwoPi)
                    * Matrix.CreateTranslation(x, Ground(x, z, size * 0.6f) - size * 0.25f, z);
                QuarterOf(x, z).Add(blocks[rng.Next(blocks.Length)], new ModelInstance(world,
                    new Vector4((float)rng.NextDouble(), ((float)rng.NextDouble() - 0.5f) * 0.3f, size * 0.25f, 0f)), x, z, size, Material.Basalt);
                i++;
            }

            //--- The fallen: trunks lying in the scorched band, where the fire dropped them
            for (int i = 0, tries = 0; i < LOGS && tries < LOGS * 60; tries++)
            {
                (float x, float z) = Around(NEAR_FROM, FAR_REACH);
                float outside = country.Outside(x, z);
                if (outside < 6f || outside > 50f || Slope(x, z) > 0.35f) continue;
                if (MathF.Abs(country.River(x, z).Across) < land.RiverHalfWidth + 8f) continue;
                int variant = rng.Next(logs.Length);
                Matrix world = Matrix.CreateRotationY((float)rng.NextDouble() * MathHelper.TwoPi)
                    * Matrix.CreateTranslation(x, Ground(x, z, 3f) - 0.15f, z);
                QuarterOf(x, z).Add(logs[variant], new ModelInstance(world, new Vector4((float)rng.NextDouble(), ((float)rng.NextDouble() - 0.5f) * 0.3f, 0.15f, 0f)), x, z, 6f);
                i++;
            }

            //--- The village first, so the woods keep out of it: the church near its middle, then the houses round it on the
            //bank, every one square to the river's run there, give or take
            var houseKinds = new HouseKind[] { new(11f, 7f, 4.2f, 3.4f, 3), new(8f, 6.5f, 3.6f, 3.0f, 2), new(14f, 8f, 5.6f, 3.8f, 4) };
            var houseMeshes = new (IProceduralMesh Body, IProceduralMesh Roof, IProceduralMesh Windows)[houseKinds.Length];
            for (int k = 0; k < houseKinds.Length; k++) houseMeshes[k] = HouseMeshes(device, houseKinds[k]);
            (IProceduralMesh churchBody, IProceduralMesh churchRoof, IProceduralMesh churchWindows) = ChurchMeshes(device);

            var villageBuilder = new QuarterBuilder();
            float riverYaw = RiverYaw(country, village.X, village.Y);
            bool Bankside(float x, float z, float margin) => MathF.Abs(country.River(x, z).Across) > land.RiverHalfWidth + margin;

            //The church: on the highest ground of the village's middle, where a church stands
            {
                float bestX = village.X, bestZ = village.Y, bestY = float.NegativeInfinity;
                for (int t = 0; t < 40; t++)
                {
                    float x = village.X + ((float)rng.NextDouble() - 0.5f) * 50f, z = village.Y + ((float)rng.NextDouble() - 0.5f) * 50f;
                    if (!Bankside(x, z, 34f) || Slope(x, z) > 0.25f) continue;
                    float y = height(x, z);
                    if (y > bestY) { bestX = x; bestZ = z; bestY = y; }
                }
                float yaw = riverYaw + (float)(rng.NextDouble() - 0.5) * 0.2f;
                Matrix world = Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(bestX, Ground(bestX, bestZ, 10f) - 0.2f, bestZ);
                var at = new ModelInstance(world, new Vector4(0.2f, 0f, 0.2f, 0f));
                villageBuilder.Add(churchBody, at, bestX, bestZ, 32f, Material.Plaster);
                villageBuilder.Add(churchRoof, at, bestX, bestZ, 32f, Material.Slate);
                villageBuilder.Add(churchWindows, at, bestX, bestZ, 32f, Material.Lit);
                occupied.Add(bestX, bestZ, 15f);
            }

            for (int i = 0, tries = 0; i < HOUSES && tries < HOUSES * 80; tries++)
            {
                float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                float r = VolcanoCountry.VILLAGE_CORE * MathF.Sqrt((float)rng.NextDouble());
                float x = village.X + MathF.Cos(a) * r, z = village.Y + MathF.Sin(a) * r;
                int kind = rng.Next(houseKinds.Length);
                float reach = houseKinds[kind].Length * 0.6f;
                if (!Bankside(x, z, 20f + reach) || Slope(x, z) > 0.3f || occupied.Clearance(x, z, reach) < 1.5f) continue;
                occupied.Add(x, z, reach);

                float yaw = riverYaw + (rng.Next(4) == 0 ? MathHelper.PiOver2 : 0f) + (float)(rng.NextDouble() - 0.5) * 0.3f;
                Matrix world = Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(x, Ground(x, z, reach) - 0.2f, z);
                var at = new ModelInstance(world, new Vector4((float)rng.NextDouble(), ((float)rng.NextDouble() - 0.5f) * 0.2f, 0.2f, 0f));
                villageBuilder.Add(houseMeshes[kind].Body, at, x, z, 14f, Material.Plaster);
                villageBuilder.Add(houseMeshes[kind].Roof, at, x, z, 14f, rng.Next(3) == 0 ? Material.Slate : Material.Tile);
                //Most windows lit at dusk, a house in five dark - nobody home, or abed
                villageBuilder.Add(houseMeshes[kind].Windows, at, x, z, 14f, rng.Next(5) == 0 ? Material.Dark : Material.Lit);
                i++;
            }

            //--- The woods: spruces where the mask says, near and far; the mask's own value is the chance a spot keeps its
            //tree, so a belt thins out at its edges instead of ending on a line
            void PlantSpruces(int count, float from, float to, bool near)
            {
                for (int i = 0, tries = 0; i < count && tries < count * 30; tries++)
                {
                    (float x, float z) = Around(from, to);
                    float outside = country.Outside(x, z);
                    if (outside < VolcanoCountry.FOREST_FROM) continue;
                    float across = MathF.Abs(country.River(x, z).Across);
                    float cover = country.ForestCover(x, z, outside, across);
                    if (!near) cover *= 1f - ShaderMath.SmoothStep(FAR_THINNING, FAR_REACH, MathF.Sqrt(x * x + z * z));
                    if ((float)rng.NextDouble() > cover || Slope(x, z) > 0.6f) continue;
                    float scale = 0.7f + 0.55f * (float)rng.NextDouble();
                    float radius = trees.ConiferCrownRadius * scale * (near ? 0.75f : 0.9f);
                    if (occupied.Clearance(x, z, radius) < 0f) continue;
                    occupied.Add(x, z, radius);

                    var instance = new ModelInstance(
                        Matrix.CreateScale(scale) * Matrix.CreateRotationY((float)rng.NextDouble() * MathHelper.TwoPi)
                            * Matrix.CreateTranslation(x, Ground(x, z, 1f) - 0.4f, z),
                        new Vector4((float)rng.NextDouble(), ((float)rng.NextDouble() - 0.5f) * 0.35f, 0.4f, 0f));
                    if (near)
                    {
                        TreeMesh tree = _coniferMeshes[rng.Next(_coniferMeshes.Length)];
                        QuarterOf(x, z).Add(tree.Trunk, instance, x, z, 20f * scale, Material.Bark);
                        QuarterOf(x, z).Add(tree.Crown, instance, x, z, 20f * scale, Material.Spruce);
                    }
                    else QuarterOf(x, z).Add(spires[rng.Next(spires.Length)], instance, x, z, 20f * scale, Material.Spruce);
                    i++;
                }
            }

            PlantSpruces(NEAR_CONIFERS, NEAR_FROM, NEAR_REACH, near: true);
            PlantSpruces(FAR_CONIFERS, NEAR_REACH, FAR_REACH, near: false);

            //--- High and Ultra's broadleaves: alders and willows along the banks, and the village's own trees round it
            for (int i = 0, tries = 0; i < BROADLEAVES && tries < BROADLEAVES * 60; tries++)
            {
                bool byVillage = i < VILLAGE_TREES;
                float x, z;
                if (byVillage)
                {
                    float a = (float)rng.NextDouble() * MathHelper.TwoPi, r = VolcanoCountry.VILLAGE_CORE * (0.4f + 0.9f * (float)rng.NextDouble());
                    x = village.X + MathF.Cos(a) * r;
                    z = village.Y + MathF.Sin(a) * r;
                }
                else (x, z) = Around(NEAR_FROM, NEAR_REACH + 150f);

                float outside = country.Outside(x, z);
                float across = MathF.Abs(country.River(x, z).Across);
                bool bank = across > land.RiverHalfWidth + 10f && across < land.RiverHalfWidth + 36f;
                if (outside < 60f || (!byVillage && !bank) || across < land.RiverHalfWidth + 10f || Slope(x, z) > 0.4f) continue;
                float scale = 0.65f + 0.5f * (float)rng.NextDouble();
                float radius = trees.CrownRadius * scale;
                if (occupied.Clearance(x, z, radius) < 0f) continue;
                occupied.Add(x, z, radius);

                var instance = new ModelInstance(
                    Matrix.CreateScale(scale) * Matrix.CreateRotationY((float)rng.NextDouble() * MathHelper.TwoPi)
                        * Matrix.CreateTranslation(x, Ground(x, z, 1f) - 0.3f, z),
                    new Vector4((float)rng.NextDouble(), ((float)rng.NextDouble() - 0.5f) * 0.3f, 0.3f, 0f));
                if (byVillage)
                {
                    TreeMesh tree = _broadleafMeshes[rng.Next(_broadleafMeshes.Length)];
                    villageBuilder.Add(tree.Trunk, instance, x, z, 14f * scale, Material.BroadleafBark);
                    villageBuilder.Add(tree.Crown, instance, x, z, 14f * scale, Material.Leaf);
                }
                else
                {
                    (IProceduralMesh stem, IProceduralMesh crown) = bankTrees[rng.Next(bankTrees.Length)];
                    QuarterOf(x, z).Add(stem, instance, x, z, 14f * scale, Material.BroadleafBark);
                    QuarterOf(x, z).Add(crown, instance, x, z, 14f * scale, Material.Leaf);
                }
                i++;
            }

            //--- The buckets, quarter by quarter
            Vector3 window = land.WindowLight.ToVector3();
            Quarters = new Quarter[quarters.Length + 1];
            for (int q = 0; q < quarters.Length; q++) Quarters[q] = quarters[q].Build(this, device, window);
            Quarters[quarters.Length] = villageBuilder.Build(this, device, window);
        }

        /// <summary>Which quarter round the arena a point is in: 0 the far side (+Z), then round through +X, -Z and -X.</summary>
        internal static int QuarterIndex(float x, float z)
        {
            float bearing = MathF.Atan2(x, z);   //0 along +Z
            return (int)MathF.Floor((bearing + MathHelper.PiOver4) / MathHelper.PiOver2 + 4f) & 3;
        }

        //The course's direction where it passes nearest the village, for the houses to stand square to
        private static float RiverYaw(VolcanoCountry country, float x, float z)
        {
            const float e = 4f;
            float ax = country.River(x + e, z).Along - country.River(x - e, z).Along;
            float az = country.River(x, z + e).Along - country.River(x, z - e).Along;
            return MathF.Atan2(-az, ax);
        }

        /// <summary>The materials the land's buckets are drawn in.</summary>
        private enum Material { Char, Basalt, Bark, Spruce, BroadleafBark, Leaf, Plaster, Tile, Slate, Lit, Dark }

        /// <summary>A bucket for a mesh's instances in one material, and the High-and-Ultra or every-tier switch it carries.</summary>
        private ScatterBucket Bucket(GraphicsDevice device, IProceduralMesh mesh, List<ModelInstance> instances, Material material, Vector3 window)
        {
            ScatterBucket bucket = material switch
            {
                //No bark relief: the bark's lichen (Acacia.fx) went pale grey-green on char and stood the snags up as white sticks
                Material.Char => new ScatterBucket(device, mesh, instances, CHAR, CHAR_ASHED, dapple: 0.15f, bark: 0f, detailOnly: true, castsShadow: false),
                Material.Basalt => new ScatterBucket(device, mesh, instances, BASALT, BASALT_DUSTED, dapple: 0.3f, bark: 0.35f, detailOnly: true, castsShadow: false),
                Material.Bark => new ScatterBucket(device, mesh, instances, BARK, BARK * 1.2f, dapple: 0f, bark: 0.5f, detailOnly: true, castsShadow: false),
                Material.Spruce => new ScatterBucket(device, mesh, instances, SPRUCE, SPRUCE_DRY, dapple: 0.5f, bark: 0f, detailOnly: true, castsShadow: false),
                Material.BroadleafBark => new ScatterBucket(device, mesh, instances, BARK, BARK * 1.2f, dapple: 0f, bark: 0.5f, detailOnly: true,
                    castsShadow: false, fineMeshes: true),
                Material.Leaf => new ScatterBucket(device, mesh, instances, LEAF, LEAF_DRY, dapple: 0.7f, bark: 0f, detailOnly: true,
                    castsShadow: false, fineMeshes: true),
                Material.Plaster => new ScatterBucket(device, mesh, instances, PLASTER, PLASTER_DIRTY, dapple: 0f, bark: 0.12f, detailOnly: true, castsShadow: false),
                Material.Tile => new ScatterBucket(device, mesh, instances, TILE, TILE_DRY, dapple: 0f, bark: 0.2f, detailOnly: true, castsShadow: false),
                Material.Slate => new ScatterBucket(device, mesh, instances, SLATE, SLATE * 1.3f, dapple: 0f, bark: 0.15f, detailOnly: true, castsShadow: false),
                Material.Lit => new ScatterBucket(device, mesh, instances, GLASS, GLASS, dapple: 0f, bark: 0f, detailOnly: true, castsShadow: false,
                    emissive: window),
                _ => new ScatterBucket(device, mesh, instances, GLASS, GLASS, dapple: 0f, bark: 0f, detailOnly: true, castsShadow: false),
            };
            _owned.Add(bucket);
            return bucket;
        }

        /// <summary>One quarter's instance lists as they are planted, by mesh and material, and the box round them.</summary>
        private sealed class QuarterBuilder
        {
            private readonly Dictionary<(IProceduralMesh, Material), List<ModelInstance>> _lists = new();
            private readonly List<(IProceduralMesh, Material)> _order = new();
            private Vector3 _min = new(float.MaxValue), _max = new(float.MinValue);

            public void Add(IProceduralMesh mesh, ModelInstance instance, float x, float z, float tall, Material material = Material.Char)
            {
                if (!_lists.TryGetValue((mesh, material), out List<ModelInstance> list))
                {
                    list = new List<ModelInstance>();
                    _lists[(mesh, material)] = list;
                    _order.Add((mesh, material));
                }
                list.Add(instance);
                float y = instance.World.M42;
                _min = Vector3.Min(_min, new Vector3(x - tall, y - 2f, z - tall));
                _max = Vector3.Max(_max, new Vector3(x + tall, y + tall, z + tall));
            }

            public Quarter Build(VolcanoCountryPlanting owner, GraphicsDevice device, Vector3 window)
            {
                var buckets = new List<ScatterBucket>();
                foreach ((IProceduralMesh mesh, Material material) in _order)
                    buckets.Add(owner.Bucket(device, mesh, _lists[(mesh, material)], material, window));
                return new Quarter(buckets.ToArray(), buckets.Count > 0 ? new BoundingBox(_min, _max) : new BoundingBox());
            }
        }

        /// <summary>Footprints on a grid of cells, for "do not stand inside your neighbour" (ScatterSpacing's rule) over thousands.</summary>
        private sealed class SpacingGrid
        {
            private readonly float _cell;
            private readonly Dictionary<(int, int), List<Vector3>> _cells = new();

            public SpacingGrid(float cell) { _cell = cell; }

            public void Add(float x, float z, float radius)
            {
                var key = ((int)MathF.Floor(x / _cell), (int)MathF.Floor(z / _cell));
                if (!_cells.TryGetValue(key, out List<Vector3> list)) _cells[key] = list = new List<Vector3>();
                list.Add(new Vector3(x, radius, z));
            }

            /// <summary>How far a footprint here clears its worst neighbour by, at <see cref="ScatterSpacing.PACKING"/>.</summary>
            public float Clearance(float x, float z, float radius)
            {
                int cx = (int)MathF.Floor(x / _cell), cz = (int)MathF.Floor(z / _cell);
                float worst = float.PositiveInfinity;
                for (int i = -2; i <= 2; i++)
                    for (int j = -2; j <= 2; j++)
                    {
                        if (!_cells.TryGetValue((cx + i, cz + j), out List<Vector3> list)) continue;
                        foreach (Vector3 other in list)
                        {
                            float dx = x - other.X, dz = z - other.Z;
                            worst = MathF.Min(worst, MathF.Sqrt(dx * dx + dz * dz) - (radius + other.Y) * ScatterSpacing.PACKING);
                        }
                    }
                return worst;
            }
        }

        /// <summary>
        /// The far woods' spruce (#748): a spire of <paramref name="tiers"/> drooping skirts on a short stem, one lathe of
        /// seven facets - a spruce past <see cref="NEAR_REACH"/> is a silhouette a few dozen pixels tall, and a silhouette is
        /// its tiers. About a hundred triangles against the near spruce's four hundred. Traced top, outside, underside, as
        /// <see cref="LatheMesh"/> takes a profile, and shut at both poles.
        /// </summary>
        private static LatheMesh SpireMesh(GraphicsDevice device, float height, float radius, int tiers, float phase)
        {
            var profile = new List<LathePoint> { new(0f, height) };
            float stem = height * 0.12f;
            for (int t = 1; t <= tiers; t++)
            {
                float u = t / (float)tiers;
                float y = stem + (height - stem) * (1f - u);
                profile.Add(new LathePoint(radius * MathF.Pow(u, 0.85f), y - 0.6f, crease: true, wobble: 1f));
                if (t < tiers) profile.Add(new LathePoint(radius * MathF.Pow(u, 0.85f) * 0.45f, y - 0.6f - (height - stem) / tiers * 0.18f));
            }
            profile.Add(new LathePoint(0.35f, stem - 0.6f));
            profile.Add(new LathePoint(0.35f, 0f));
            profile.Add(new LathePoint(0f, 0f));
            return new LatheMesh(device, profile, 7, irregularityAmplitude: radius * 0.12f, irregularityPhase: phase);
        }

        /// <summary>
        /// A tree on the river's bank (#748), an alder or a willow seen from across the valley: a tall lobed crown
        /// (<see cref="FoliageMesh"/> in <see cref="BANK_CROWN"/>, about three hundred triangles) low over a short stem that
        /// runs up into it, both shut. The village's own trees are grown (<see cref="TreeMesh"/>); a bank of them at four
        /// thousand triangles a tree was half of what the whole planting cost. ⚠ In the round-topped, tucked
        /// <see cref="FoliageStyle.Crown"/>, wider than tall and on a longer stem, they lined the river as mushrooms.
        /// </summary>
        private (IProceduralMesh Stem, IProceduralMesh Crown) BankTree(GraphicsDevice device, float stemHeight, float crownRadius,
            float crownHalfHeight, int seed)
        {
            var rng = new Random(seed);
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            Vector3 lean = new((float)(rng.NextDouble() - 0.5) * 0.6f, 0f, (float)(rng.NextDouble() - 0.5) * 0.6f);
            TubeGeometry.AddSweep(v, idx, 6,
                new[] { new Vector3(0f, -0.4f, 0f), new Vector3(0f, stemHeight * 0.5f, 0f) + lean * 0.5f, new Vector3(0f, stemHeight + crownHalfHeight * 0.6f, 0f) + lean },
                new[] { 0.5f, 0.36f, 0.22f }, capStart: true, capEnd: true);
            var stem = new UploadedMesh(device, v, idx, new BoundingSphere(new Vector3(0f, stemHeight * 0.5f, 0f), stemHeight + crownHalfHeight));
            var crown = new FoliageMesh(device, crownRadius, crownHalfHeight, stemHeight + crownHalfHeight * 0.75f, seed, BANK_CROWN);
            return (Own(stem), Own(crown));
        }

        //A bank tree's crown: an oval taller than it is wide, hardly flattened and hardly tucked, lobed all round
        private static readonly FoliageStyle BANK_CROWN = new(0.05f, 0f, 0.15f, 0.2f, 7, 10, 0.15f, 0.35f, 2f, 4.5f, -0.3f, 0.9f);

        /// <summary>A house's figures: its length along the ridge, its depth, its walls' height, its roof's rise and windows a side.</summary>
        private readonly record struct HouseKind(float Length, float Depth, float WallHeight, float RoofRise, int WindowsASide);

        /// <summary>
        /// A house (#748): a lime-washed body with gable ends, a roof of two slabs overhanging it with a chimney, and its
        /// windows - three meshes, three materials, one set of instances. Every piece a closed solid wound clockwise from
        /// outside (<see cref="MeshBuilder"/> winds by the face normal it is handed); the body runs a metre and a half under
        /// its ground so a slope cannot show its underside. The ridge runs along local X.
        /// </summary>
        private (IProceduralMesh Body, IProceduralMesh Roof, IProceduralMesh Windows) HouseMeshes(GraphicsDevice device, HouseKind kind)
        {
            float halfL = kind.Length * 0.5f, halfW = kind.Depth * 0.5f;
            var body = new MeshBuilder();
            AddGabledBody(body, Vector3.Zero, halfL, halfW, kind.WallHeight, kind.RoofRise);

            var roof = new MeshBuilder();
            AddRoof(roof, Vector3.Zero, halfL, halfW, kind.WallHeight, kind.RoofRise);
            //The chimney, through the back slope near one gable
            roof.AddBox(new Vector3(halfL * 0.55f, kind.WallHeight + kind.RoofRise * 0.75f, halfW * 0.35f),
                new Vector3(0.45f, 0f, 0f), new Vector3(0f, kind.RoofRise * 0.55f, 0f), new Vector3(0f, 0f, 0.45f));

            //The windows: a row down each long wall at the ground floor, another under the eaves on a house tall enough for
            //an upper floor, and one in each gable
            var windows = new MeshBuilder();
            for (int side = -1; side <= 1; side += 2)
                for (int w = 0; w < kind.WindowsASide; w++)
                {
                    float x = -halfL + kind.Length * (w + 0.5f) / kind.WindowsASide;
                    AddWindow(windows, new Vector3(x, 1.7f, side * halfW), new Vector3(0f, 0f, side), 0.5f, 0.6f);
                    if (kind.WallHeight > 5f) AddWindow(windows, new Vector3(x, 4.1f, side * halfW), new Vector3(0f, 0f, side), 0.45f, 0.5f);
                }
            for (int end = -1; end <= 1; end += 2)
                AddWindow(windows, new Vector3(end * halfL, kind.WallHeight + kind.RoofRise * 0.3f, 0f), new Vector3(end, 0f, 0f), 0.4f, 0.45f);

            float reach = MathF.Sqrt(halfL * halfL + halfW * halfW) + 1f;
            var bounds = new BoundingSphere(new Vector3(0f, kind.WallHeight * 0.6f, 0f), reach + kind.WallHeight);
            return (Own(Upload(device, body, bounds)), Own(Upload(device, roof, bounds)), Own(Upload(device, windows, bounds)));
        }

        /// <summary>
        /// The church (#748): a nave under a slate roof and a tower at its west end with a spire, its windows tall - the one
        /// building every reference village has, and the one that says "village" from a kilometre off.
        /// </summary>
        private (IProceduralMesh Body, IProceduralMesh Roof, IProceduralMesh Windows) ChurchMeshes(GraphicsDevice device)
        {
            const float NAVE_HALF_L = 10f, NAVE_HALF_W = 4.6f, NAVE_WALL = 7f, NAVE_RISE = 5f;
            const float TOWER_HALF = 2.6f, TOWER_TOP = 21f, SPIRE = 10f;
            Vector3 tower = new(-NAVE_HALF_L - TOWER_HALF + 0.4f, 0f, 0f);

            var body = new MeshBuilder();
            AddGabledBody(body, Vector3.Zero, NAVE_HALF_L, NAVE_HALF_W, NAVE_WALL, NAVE_RISE);
            body.AddBox(tower + new Vector3(0f, (TOWER_TOP - 1.5f) * 0.5f, 0f), new Vector3(TOWER_HALF, 0f, 0f),
                new Vector3(0f, (TOWER_TOP + 1.5f) * 0.5f, 0f), new Vector3(0f, 0f, TOWER_HALF));

            var roof = new MeshBuilder();
            AddRoof(roof, Vector3.Zero, NAVE_HALF_L, NAVE_HALF_W, NAVE_WALL, NAVE_RISE);
            //The spire: a four-sided pyramid on the tower, its base a little proud of the tower's walls
            Vector3 apex = tower + new Vector3(0f, TOWER_TOP + SPIRE, 0f);
            float b = TOWER_HALF + 0.3f;
            Vector3[] corners =
            {
                tower + new Vector3(-b, TOWER_TOP - 0.2f, -b), tower + new Vector3(b, TOWER_TOP - 0.2f, -b),
                tower + new Vector3(b, TOWER_TOP - 0.2f, b), tower + new Vector3(-b, TOWER_TOP - 0.2f, b),
            };
            for (int c = 0; c < 4; c++)
            {
                Vector3 p = corners[c], q = corners[(c + 1) % 4];
                Vector3 n = Vector3.Normalize(Vector3.Cross(q - p, apex - p));
                Vector3 outward = (p + q) * 0.5f - tower;
                outward.Y = 0f;
                if (Vector3.Dot(n, outward) < 0f) n = -n;
                roof.AddTriangle(p, q, apex, n, n, n, n);
            }
            roof.AddQuad(corners[0], corners[1], corners[2], corners[3], -Vector3.UnitY, -Vector3.UnitY, -Vector3.UnitY, -Vector3.UnitY, -Vector3.UnitY);

            var windows = new MeshBuilder();
            for (int side = -1; side <= 1; side += 2)
                for (int w = 0; w < 3; w++)
                    AddWindow(windows, new Vector3(-NAVE_HALF_L + 2 * NAVE_HALF_L * (w + 0.5f) / 3f, 3.6f, side * NAVE_HALF_W), new Vector3(0f, 0f, side), 0.55f, 1.6f);
            //The belfry's openings, one a face, under the spire
            for (int f = 0; f < 4; f++)
            {
                Vector3 normal = f switch { 0 => Vector3.UnitX, 1 => -Vector3.UnitX, 2 => Vector3.UnitZ, _ => -Vector3.UnitZ };
                AddWindow(windows, tower + new Vector3(0f, TOWER_TOP - 3.2f, 0f) + normal * TOWER_HALF, normal, 0.6f, 1.2f);
            }
            //The east window over the altar, in the far gable
            AddWindow(windows, new Vector3(NAVE_HALF_L, 4.2f, 0f), Vector3.UnitX, 0.8f, 1.8f);

            var bounds = new BoundingSphere(new Vector3(-4f, 12f, 0f), 26f);
            return (Own(Upload(device, body, bounds)), Own(Upload(device, roof, bounds)), Own(Upload(device, windows, bounds)));
        }

        //A walled body under a gable: two long walls, two gable ends, the floor under the ground and the two slopes the roof
        //slabs lie on (they close the solid; the slabs cover them)
        private static void AddGabledBody(MeshBuilder mesh, Vector3 at, float halfL, float halfW, float wall, float rise)
        {
            const float SUNK = -1.5f;
            Vector3 P(float x, float y, float z) => at + new Vector3(x, y, z);
            Vector3 nFront = -Vector3.UnitZ, nBack = Vector3.UnitZ, nLeft = -Vector3.UnitX, nRight = Vector3.UnitX;

            mesh.AddQuad(P(-halfL, SUNK, -halfW), P(halfL, SUNK, -halfW), P(halfL, wall, -halfW), P(-halfL, wall, -halfW), nFront, nFront, nFront, nFront, nFront);
            mesh.AddQuad(P(-halfL, SUNK, halfW), P(halfL, SUNK, halfW), P(halfL, wall, halfW), P(-halfL, wall, halfW), nBack, nBack, nBack, nBack, nBack);
            for (int end = -1; end <= 1; end += 2)
            {
                Vector3 n = end < 0 ? nLeft : nRight;
                float x = end * halfL;
                Vector3 a = P(x, SUNK, -halfW), b = P(x, SUNK, halfW), c = P(x, wall, halfW), d = P(x, wall + rise, 0f), e = P(x, wall, -halfW);
                mesh.AddTriangle(a, b, c, n, n, n, n);
                mesh.AddTriangle(a, c, d, n, n, n, n);
                mesh.AddTriangle(a, d, e, n, n, n, n);
            }
            mesh.AddQuad(P(-halfL, SUNK, -halfW), P(halfL, SUNK, -halfW), P(halfL, SUNK, halfW), P(-halfL, SUNK, halfW),
                -Vector3.UnitY, -Vector3.UnitY, -Vector3.UnitY, -Vector3.UnitY, -Vector3.UnitY);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 n = Vector3.Normalize(new Vector3(0f, halfW, side * rise));
                mesh.AddQuad(P(-halfL, wall, side * halfW), P(halfL, wall, side * halfW), P(halfL, wall + rise, 0f), P(-halfL, wall + rise, 0f), n, n, n, n, n);
            }
        }

        //The roof: two slabs laid on the slopes, each a closed box, overhanging the eaves and the gables and crossing at the
        //ridge so no slit of sky opens along it
        private static void AddRoof(MeshBuilder mesh, Vector3 at, float halfL, float halfW, float wall, float rise)
        {
            const float THICK = 0.28f, EAVE = 0.7f, GABLE = 0.45f, PAST_RIDGE = 0.35f;
            float slope = MathF.Sqrt(halfW * halfW + rise * rise);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 eave = at + new Vector3(0f, wall, side * halfW);
                Vector3 up = Vector3.Normalize(new Vector3(0f, rise, -side * halfW));        //from the eave towards the ridge
                Vector3 normal = Vector3.Normalize(new Vector3(0f, halfW, side * rise));     //out of the slope
                float from = -EAVE, to = slope + PAST_RIDGE;
                Vector3 centre = eave + up * ((from + to) * 0.5f) + normal * (THICK * 0.5f);
                mesh.AddBox(centre, new Vector3(halfL + GABLE, 0f, 0f), up * ((to - from) * 0.5f), normal * (THICK * 0.5f));
            }
        }

        //A window: a pane of glass in a shallow box standing a few centimetres proud of its wall, so it is a solid of its own
        private static void AddWindow(MeshBuilder mesh, Vector3 centre, Vector3 outward, float halfWidth, float halfHeight)
        {
            Vector3 along = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, outward));
            mesh.AddBox(centre + outward * 0.03f, along * halfWidth, Vector3.UnitY * halfHeight, outward * 0.06f);
        }

        private static UploadedMesh Upload(GraphicsDevice device, MeshBuilder mesh, BoundingSphere bounds)
        {
            (List<VertexPositionNormalTexture> vertices, List<int> indices) = mesh.Geometry;
            return new UploadedMesh(device, vertices, indices, bounds);
        }

        private T Own<T>(T item) where T : IDisposable
        {
            _owned.Add(item);
            return item;
        }

        public void Dispose()
        {
            foreach (IDisposable d in _owned) d.Dispose();
            _owned.Clear();
            foreach (TreeMesh tree in _coniferMeshes) tree.Dispose();
            foreach (TreeMesh tree in _broadleafMeshes) tree.Dispose();
        }
    }
}
