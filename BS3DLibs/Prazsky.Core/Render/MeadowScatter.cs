using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>What stands in the meadow</b> (#609). The owner, after playing its chapter: <i>"there are no objects on it,
    /// the scene is boring; maybe some shrubs, a little path, a fence, maybe even a brook"</i>. The references
    /// (<c>C:\Users\panrd\AI\sd\out\609-klein</c> and <c>609-zimage</c>) put the same few things in every ordinary
    /// meadow: a lone broad oak, hedgerows along the field edges, round hay bales, a weathered post-and-rail fence,
    /// mossy boulders, and grass that stands up in tufts. The third round (2026-09-29) added what the brook and the path
    /// arrive at — a pond with reeds and willows, an old oak on a knoll with a bench under it — and grew the trees
    /// again from references, old and tall (<see cref="MeadowTreeMesh"/>). This plants them on the field <c>Meadow.fx</c> draws —
    /// every height off <see cref="TerrainMirror.Meadow"/> — outside the arena's flat clearing, as static instance
    /// buckets the savanna's own path draws (<see cref="ScatterBucket"/>, <c>Acacia.fx</c>).
    /// <para>
    /// <b>Where + what live here; with stays the backdrop's</b>, the savanna's split (<see cref="SavannaScatter"/>):
    /// no GPU state, no camera, only the meshes and the instance buffers, which it disposes. One deterministic pass
    /// off one seed and one occupancy list, so a scene seed plants the same meadow every time.
    /// </para>
    /// </summary>
    public sealed class MeadowScatter : IDisposable
    {
        /// <summary>The planting's own seed, offset by the scene seed as the savanna's is.</summary>
        public const int DEFAULT_SEED = 60900;

        //=== How much of what, and where. Radii are from the arena's centre; the clearing is flat to 95 and the
        //hills rise over the next 140 (MeadowSceneConfig), so the play camera sees the rise and the tops.
        private const float INNER = 70f;             //nothing nearer than this (the island is 26 across the radius), but the
                                                     //island's own bank and what the brook and the path arrive at (#609)
        private const float OUTER = 460f;
        private const int TREES = 12, SHRUBS = 55, BALES = 14, BOULDERS = 18, TUFTS = 700;
        private const int HEDGEROWS = 5, FENCES = 2;
        private const int BERM_STONES = 16, BERM_TUFTS = 120;

        //=== The colours, linear, and the drier shade each instance can lean towards (as the savanna's). Set against
        //the meadow's own grass (0.14/0.46/0.05) rather than the savanna's: at the savanna's canopy the first
        //hedges photographed as black lines on the hills.
        private static readonly Vector3 OAK_LEAF = new(0.100f, 0.210f, 0.045f);
        private static readonly Vector3 OAK_LEAF_DRY = new(0.160f, 0.240f, 0.060f);
        private static readonly Vector3 OAK_BARK = new(0.120f, 0.100f, 0.080f);
        //A willow's silver-green, paler and greyer than the oaks' (#609's third round)
        private static readonly Vector3 WILLOW_LEAF = new(0.140f, 0.210f, 0.090f);
        private static readonly Vector3 WILLOW_LEAF_DRY = new(0.180f, 0.230f, 0.110f);
        private static readonly Vector3 BENCH_WOOD = new(0.200f, 0.150f, 0.095f);
        private static readonly Vector3 SHRUB = new(0.085f, 0.200f, 0.040f);
        private static readonly Vector3 SHRUB_DRY = new(0.140f, 0.220f, 0.055f);
        private static readonly Vector3 STRAW = new(0.55f, 0.43f, 0.19f);
        private static readonly Vector3 FENCE_WOOD = new(0.30f, 0.28f, 0.24f);
        private static readonly Vector3 STONE = new(0.30f, 0.30f, 0.27f);
        private static readonly Vector3 TUFT = new(0.12f, 0.36f, 0.05f);
        private static readonly Vector3 REED = new(0.11f, 0.24f, 0.05f);
        //The bank round the island (#608): earth against the drum; the turf over the rest is the field's own grass colour
        private static readonly Vector3 BERM_EARTH = new(0.17f, 0.13f, 0.07f);

        private readonly List<IDisposable> _owned = new();

        /// <summary>One instanced draw each, in <c>Acacia.fx</c>, as the savanna's.</summary>
        public ScatterBucket[] Buckets { get; }

        /// <summary>The old trees, for a camera to point at — the one on the knoll first (#609's third round).</summary>
        public IReadOnlyList<PlantFigure> Trees { get; }

        /// <param name="config">The meadow: its field (<see cref="TerrainMirror.Meadow"/>) and its footpath
        /// (<see cref="MeadowPath"/>) — nothing is planted on the path, and the first fence runs along it.</param>
        public MeadowScatter(GraphicsDevice device, MeadowSceneConfig config, int seed)
        {
            float height(float x, float z) => TerrainMirror.Meadow(x, z, config);
            float pathLateral(float x, float z) => MeadowPath.Lateral(x, z, config);

            Random rng = new(seed);
            var occupied = new List<(float X, float Z, float R)>();
            var buckets = new List<ScatterBucket>();
            var treeFigures = new List<PlantFigure>();

            bool Free(float x, float z, float r)
            {
                if (MathF.Abs(pathLateral(x, z)) < r + PATH_CLEARANCE) return false;
                if (MathF.Abs(MeadowPath.BrookLateral(x, z, config)) < r + config.BrookWidth) return false;
                if (MeadowPath.PondShore(x, z, config) < r + POND_CLEARANCE) return false;
                foreach ((float ox, float oz, float or) in occupied)
                    if ((ox - x) * (ox - x) + (oz - z) * (oz - z) < (or + r) * (or + r)) return false;
                return true;
            }

            (float X, float Z) Site(float inner, float outer)
            {
                float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                float d = MathF.Sqrt(MathHelper.Lerp(inner * inner, outer * outer, (float)rng.NextDouble()));
                return (MathF.Cos(a) * d, MathF.Sin(a) * d);
            }

            //Whatever is planted near the island stands ON its bank (#608), not inside it
            float stand(float x, float z) => MathF.Max(height(x, z), IslandBerm.SurfaceY(height, seed, x, z));

            ModelInstance At(float x, float z, float scale, float yaw, float sink, float dryness, float jitter, float pitch = 0f) =>
                new(Matrix.CreateScale(scale) * Matrix.CreateRotationZ(pitch) * Matrix.CreateRotationY(yaw)
                    * Matrix.CreateTranslation(x, stand(x, z) - sink, z),
                    new Vector4(dryness, jitter, 0f, 0f));

            float Jitter() => (float)(rng.NextDouble() - 0.5) * 0.2f;

            //--- The old trees (#609's third round): two oaks, two limes and a willow, grown from references
            //(MeadowTreeMesh) - the second round's lumpy oaks of 14 units stood out on the rise past 130 and the owner
            //found no tree in the meadow at all
            var treeMeshes = new[]
            {
                Own(new MeadowTreeMesh(device, MeadowTreeKind.Oak, 6090)),
                Own(new MeadowTreeMesh(device, MeadowTreeKind.Oak, 6091)),
                Own(new MeadowTreeMesh(device, MeadowTreeKind.Lime, 6092)),
                Own(new MeadowTreeMesh(device, MeadowTreeKind.Lime, 6093)),
                Own(new MeadowTreeMesh(device, MeadowTreeKind.Willow, 6094)),
            };
            const int WILLOW = 4;
            var treeInstances = new List<ModelInstance>[treeMeshes.Length];
            for (int m = 0; m < treeMeshes.Length; m++) treeInstances[m] = new List<ModelInstance>();

            void PlantTree(int m, float x, float z, float scale)
            {
                MeadowTreeMesh tree = treeMeshes[m];
                ModelInstance planted = At(x, z, scale, (float)rng.NextDouble() * MathHelper.TwoPi, 0.35f * scale,
                    0.3f * (float)rng.NextDouble(), Jitter());
                treeInstances[m].Add(planted);
                treeFigures.Add(PlantFigure.Of(tree.Crown.BoundingSphere, planted.World, tree.BaseRadius));
                occupied.Add((x, z, tree.CrownReach * scale * 0.75f));
            }

            //Where the footpath arrives: the biggest oak on the knoll's top, a bench under it looking back over the
            //clearing, the ground round its foot trodden bare (Meadow.fx) - every reference of a path to a tree
            (float knollX, float knollZ) = MeadowPath.PathEnd(config);
            PlantTree(0, knollX, knollZ, 1.15f);
            var benchInstances = new List<ModelInstance>();
            {
                float toArena = MathF.Atan2(-knollZ, -knollX);
                float side = toArena + 0.55f;
                float bx = knollX + MathF.Cos(side) * BENCH_OUT, bz = knollZ + MathF.Sin(side) * BENCH_OUT;
                //The bench's seat runs along its X; it faces +Z, turned to face the arena
                benchInstances.Add(At(bx, bz, 1f, MathF.PI * 0.5f - toArena, 0.05f, 0f, 0f));
            }

            //By the pond: willows on its far side from the arena, where the brook comes in
            (float pondX, float pondZ) = MeadowPath.PondCentre(config);
            float awayFromArena = MathF.Atan2(pondZ, pondX);
            foreach (float turn in new[] { -0.75f, 0.95f })
            {
                float a = awayFromArena + turn;
                float reach = config.PondRadius * MeadowPath.PondOutline(a) + WILLOW_FROM_SHORE;
                PlantTree(WILLOW, pondX + MathF.Cos(a) * reach, pondZ + MathF.Sin(a) * reach, 0.9f + 0.2f * (float)rng.NextDouble());
            }

            //And out on the rise, oaks and limes - a few near enough to the clearing to stand tall over the play
            for (int i = 0, tries = 0; i < TREES && tries < 600; tries++)
            {
                (float x, float z) = Site(i < 4 ? 95f : 130f, i < 4 ? 150f : OUTER);
                int m = rng.Next(WILLOW);
                float scale = 0.9f + 0.25f * (float)rng.NextDouble();
                float room = treeMeshes[m].CrownReach * scale * 0.75f;
                if (!Free(x, z, room)) continue;
                PlantTree(m, x, z, scale);
                i++;
            }

            //--- The shrubs: three rounded hawthorn-like masses, alone and in the hedgerows
            var shrubMeshes = new FoliageMesh[3];
            var shrubInstances = new List<ModelInstance>[3];
            for (int m = 0; m < shrubMeshes.Length; m++)
            {
                float r = 1.6f + 0.5f * (float)rng.NextDouble();
                shrubMeshes[m] = Own(new FoliageMesh(device, r, r * 0.8f, r * 0.7f, 6100 + m, FoliageStyle.Scrub));
                shrubInstances[m] = new List<ModelInstance>();
            }
            for (int i = 0, tries = 0; i < SHRUBS && tries < 1000; tries++)
            {
                (float x, float z) = Site(INNER + 20f, OUTER);
                if (!Free(x, z, 2.5f)) continue;
                occupied.Add((x, z, 2.5f));
                shrubInstances[rng.Next(3)].Add(At(x, z, 0.8f + 0.6f * (float)rng.NextDouble(), (float)rng.NextDouble() * MathHelper.TwoPi,
                    0.3f, (float)rng.NextDouble(), Jitter()));
                i++;
            }

            //The hedgerows: a field edge is a line, not a scatter. Each a gently bent run of overlapping shrubs.
            for (int h = 0; h < HEDGEROWS; h++)
            {
                float bearing = (h + (float)rng.NextDouble() * 0.7f) * MathHelper.TwoPi / HEDGEROWS;
                float from = 150f + 80f * (float)rng.NextDouble();
                float length = 70f + 60f * (float)rng.NextDouble();
                //Tangential to the arena, so a hedge stands across the view rather than pointing at the lens
                Vector2 centre = new Vector2(MathF.Cos(bearing), MathF.Sin(bearing)) * from;
                Vector2 along = new(-MathF.Sin(bearing), MathF.Cos(bearing));
                Vector2 outward = new(MathF.Cos(bearing), MathF.Sin(bearing));
                float bend = (float)(rng.NextDouble() - 0.5) * 0.004f;
                for (float s = -length * 0.5f; s <= length * 0.5f; s += 2.1f)
                {
                    Vector2 p = centre + along * s + outward * (bend * s * s);
                    if (!Free(p.X, p.Y, 1.2f)) continue;
                    shrubInstances[rng.Next(3)].Add(At(p.X, p.Y, 1.1f + 0.45f * (float)rng.NextDouble(), (float)rng.NextDouble() * MathHelper.TwoPi,
                        0.4f, 0.2f * (float)rng.NextDouble(), Jitter()));
                }
                occupied.Add((centre.X, centre.Y, 0f));
            }

            //--- The hay bales: round, lying on their side, in loose groups on the lower slopes
            IProceduralMesh bale = Own(BaleMesh(device));
            var baleInstances = new List<ModelInstance>();
            for (int i = 0, tries = 0; i < BALES && tries < 600; tries++)
            {
                (float x, float z) = Site(110f, 280f);
                if (!Free(x, z, 1.6f)) continue;
                int group = 1 + rng.Next(3);
                for (int g = 0; g < group && i < BALES; g++)
                {
                    float gx = x + g * 2.2f * (float)Math.Cos(i), gz = z + g * 2.2f * (float)Math.Sin(i);
                    if (!Free(gx, gz, 1.6f)) continue;
                    occupied.Add((gx, gz, 1.6f));
                    baleInstances.Add(At(gx, gz, 0.95f + 0.1f * (float)rng.NextDouble(), (float)rng.NextDouble() * MathHelper.TwoPi,
                        0.12f, (float)rng.NextDouble(), Jitter()));
                    i++;
                }
            }

            //--- The fences: weathered post-and-rail runs, each a line of segments out across the rise, every
            //segment pitched to the ground's own slope so the rails meet the next post
            IProceduralMesh fence = Own(FenceMesh(device));
            var fenceInstances = new List<ModelInstance>();
            //The first runs beside the footpath, the way a path through a meadow keeps to its fence: a point every
            //FENCE_SPAN along the centreline, set FENCE_BESIDE_PATH to its side, each span aimed at the next point
            {
                Vector2 PathSide(float d)
                {
                    float angle = config.PathBearing + (MeadowPath.Wander(d, config) + FENCE_BESIDE_PATH) / d;
                    return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * d;
                }

                Vector2 p = PathSide(config.ClearingRadius * MeadowPath.START + 8f);
                for (float d = config.ClearingRadius * MeadowPath.START + 8f; d < 420f; )
                {
                    //Step the distance so the chord to the next point is one span long
                    float next = d + FENCE_SPAN;
                    Vector2 q = PathSide(next);
                    for (int k = 0; k < 4; k++) { next += FENCE_SPAN - Vector2.Distance(p, q); q = PathSide(next); }

                    Vector2 heading = Vector2.Normalize(q - p);
                    float rise = height(q.X, q.Y) - height(p.X, p.Y);
                    fenceInstances.Add(At(p.X, p.Y, 1f, -MathF.Atan2(heading.Y, heading.X), 0.15f, (float)rng.NextDouble(), Jitter(),
                        MathF.Atan2(rise, FENCE_SPAN)));
                    p = q;
                    d = next;
                }
            }

            for (int f = 0; f < FENCES; f++)
            {
                float bearing = (f + 0.35f + 0.3f * (float)rng.NextDouble()) * MathHelper.TwoPi / FENCES;
                Vector2 dir = new(MathF.Cos(bearing), MathF.Sin(bearing));
                Vector2 side = new(-dir.Y, dir.X);
                float wander = 0.25f * ((float)rng.NextDouble() - 0.5f);
                Vector2 p = dir * 105f;
                for (int s = 0; s < 60; s++)
                {
                    Vector2 heading = Vector2.Normalize(dir + side * (wander * MathF.Sin(s * 0.13f)));
                    Vector2 q = p + heading * FENCE_SPAN;
                    if (Free(p.X, p.Y, 0.3f))
                    {
                        float rise = height(q.X, q.Y) - height(p.X, p.Y);
                        float yaw = -MathF.Atan2(heading.Y, heading.X);
                        fenceInstances.Add(At(p.X, p.Y, 1f, yaw, 0.15f, (float)rng.NextDouble(), Jitter(), MathF.Atan2(rise, FENCE_SPAN)));
                    }
                    p = q;
                }
            }

            //--- The boulders, mossy
            var rockMeshes = new RockMesh[3];
            var rockInstances = new List<ModelInstance>[3];
            for (int m = 0; m < rockMeshes.Length; m++)
            {
                rockMeshes[m] = Own(new RockMesh(device, 1f, 0.6f + 0.25f * (float)rng.NextDouble(), 14, m * 1.7f));
                rockInstances[m] = new List<ModelInstance>();
            }
            for (int i = 0, tries = 0; i < BOULDERS && tries < 600; tries++)
            {
                (float x, float z) = Site(INNER + 10f, OUTER);
                float size = 0.9f + 1.8f * (float)rng.NextDouble();
                if (!Free(x, z, size)) continue;
                occupied.Add((x, z, size));
                rockInstances[rng.Next(3)].Add(At(x, z, size, (float)rng.NextDouble() * MathHelper.TwoPi, size * 0.25f, 0f, Jitter()));
                i++;
            }

            //--- The brook's banks (#609): stones at the water's edge and reeds standing in tufts along it, the
            //references' brook in every one of them. Placed off the centreline directly, not through Free, which
            //keeps everything else clear of the water.
            var reedInstances = new List<ModelInstance>();
            for (float d = config.ClearingRadius * MeadowPath.BROOK_START + 4f; d < 420f; d += 1.6f + 1.8f * (float)rng.NextDouble())
            {
                float side = rng.Next(2) == 0 ? -1f : 1f;
                //Not in the pond, whose own shore is planted below
                (float cx, float cz) = MeadowPath.BrookPoint(d, 0f, config);
                if (MeadowPath.PondShore(cx, cz, config) < config.BrookWidth) continue;
                if (rng.NextDouble() < 0.45)
                {
                    (float sx, float sz) = MeadowPath.BrookPoint(d, side * (config.BrookWidth * 0.5f + 0.2f), config);
                    float size = 0.35f + 0.5f * (float)rng.NextDouble();
                    rockInstances[rng.Next(3)].Add(At(sx, sz, size, (float)rng.NextDouble() * MathHelper.TwoPi, size * 0.3f, 0f, Jitter()));
                }
                (float rx, float rz) = MeadowPath.BrookPoint(d, side * (config.BrookWidth * 0.5f + 0.7f + 0.8f * (float)rng.NextDouble()), config);
                reedInstances.Add(At(rx, rz, 1.0f + 0.8f * (float)rng.NextDouble(), (float)rng.NextDouble() * MathHelper.TwoPi, 0.05f,
                    0.3f * (float)rng.NextDouble(), Jitter()));
            }
            IProceduralMesh reeds = Own(new GrassTuftMesh(device, 0.3f, 2.2f, 6130));

            //The pond's shore (#609's third round), as the references draw it: reeds in clumps round part of it,
            //standing in the shallows as much as on the bank, flat stones between the clumps, and the grass to the
            //water's edge elsewhere
            for (float a = 0f; a < MathHelper.TwoPi; a += 0.09f + 0.08f * (float)rng.NextDouble())
            {
                float shore = config.PondRadius * MeadowPath.PondOutline(a);
                float clump = MathF.Sin(a * 3f + 1.3f) + 0.6f * MathF.Sin(a * 7f + 0.4f);
                if (clump > 0.1f)
                {
                    int reedsHere = 2 + rng.Next(3);
                    for (int k = 0; k < reedsHere; k++)
                    {
                        float r = shore + (float)(rng.NextDouble() * 2.4 - 0.9);
                        float b = a + ((float)rng.NextDouble() - 0.5f) * 0.08f;
                        float rx = pondX + MathF.Cos(b) * r, rz = pondZ + MathF.Sin(b) * r;
                        float size = 0.9f + 0.9f * (float)rng.NextDouble(), yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                        float lean = 0.3f * (float)rng.NextDouble(), jitter = Jitter();
                        //Not across the brook's mouth, where it comes in
                        if (MathF.Abs(MeadowPath.BrookLateral(rx, rz, config)) < config.BrookWidth * 0.5f + 0.6f) continue;
                        reedInstances.Add(At(rx, rz, size, yaw, 0.1f, lean, jitter));
                    }
                }
                else if (rng.NextDouble() < 0.3)
                {
                    float r = shore + 0.2f + 0.5f * (float)rng.NextDouble();
                    float size = 0.35f + 0.55f * (float)rng.NextDouble();
                    float sx = pondX + MathF.Cos(a) * r, sz = pondZ + MathF.Sin(a) * r;
                    int variant = rng.Next(3);
                    float yaw = (float)rng.NextDouble() * MathHelper.TwoPi, jitter = Jitter();
                    if (MathF.Abs(MeadowPath.BrookLateral(sx, sz, config)) >= config.BrookWidth * 0.5f + 0.6f)
                        rockInstances[variant].Add(At(sx, sz, size, yaw, size * 0.35f, 0f, jitter));
                }
            }

            //--- The bank the island sits in (#608): turf over earth round the foot, a few stones set into it and
            //longer grass along its edge, so the stone meets the field as a place and not as a disc laid on it
            IProceduralMesh bermEarth = Own(IslandBerm.Build(device, height, seed, 0, IslandBerm.EARTH_TO));
            IProceduralMesh bermTurf = Own(IslandBerm.Build(device, height, seed, IslandBerm.EARTH_TO, IslandBerm.RINGS - 1));
            for (int i = 0; i < BERM_STONES; i++)
            {
                float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                float d = MathHelper.Lerp(IslandBerm.STONE_RADIUS_MIN, IslandBerm.STONE_RADIUS_MAX, (float)rng.NextDouble());
                float size = 0.35f + 0.6f * (float)rng.NextDouble();
                rockInstances[rng.Next(3)].Add(At(MathF.Cos(a) * d, MathF.Sin(a) * d, size, (float)rng.NextDouble() * MathHelper.TwoPi,
                    size * 0.15f, 0f, Jitter()));
            }

            //--- The grass tufts: blades that stand up, the one thing the shader's grass cannot do. Nearest the
            //play camera where they read, thinning outward; the Low tier skips them.
            var tuftMeshes = new GrassTuftMesh[3];
            var tuftInstances = new List<ModelInstance>[3];
            for (int m = 0; m < tuftMeshes.Length; m++)
            {
                tuftMeshes[m] = Own(new GrassTuftMesh(device, 0.35f, 0.9f + 0.4f * m, 6120 + m));
                tuftInstances[m] = new List<ModelInstance>();
            }
            for (int i = 0; i < TUFTS; i++)
            {
                //A share of them on the bank - half growing against the stone, as the references drew it, half along
                //its outer edge - and the rest out on the field
                (float x, float z) = i < BERM_TUFTS / 2 ? Site(ArenaIsland.RADIUS + 0.2f, ArenaIsland.RADIUS + 1.2f)
                    : i < BERM_TUFTS ? Site(IslandBerm.STONE_RADIUS_MAX - 0.5f, IslandBerm.STONE_RADIUS_MAX + 2.5f)
                    : Site(ArenaIsland.RADIUS + 12f, 260f);
                //Never in the water, on the trodden path or on the bare ground under the old tree (#609's third round)
                //(the drawn shore wanders by the ragged edge and its smoothing either side of the arithmetic one, 0.6 all told)
                if (MeadowPath.PondShore(x, z, config) < 0.9f || MathF.Abs(MeadowPath.BrookLateral(x, z, config)) < config.BrookWidth * 0.5f + 0.6f
                    || MathF.Abs(pathLateral(x, z)) < config.PathWidth * 0.6f
                    || (x - knollX) * (x - knollX) + (z - knollZ) * (z - knollZ) < TREE_BARE * TREE_BARE)
                    continue;
                tuftInstances[rng.Next(3)].Add(At(x, z, 0.8f + 0.7f * (float)rng.NextDouble(), (float)rng.NextDouble() * MathHelper.TwoPi,
                    0.05f, (float)rng.NextDouble() * 0.6f, Jitter()));
            }

            //--- The buckets
            for (int m = 0; m < treeMeshes.Length; m++)
            {
                if (treeInstances[m].Count == 0) continue;
                MeadowTreeMesh tree = treeMeshes[m];
                Vector3 leaf = m == WILLOW ? WILLOW_LEAF : OAK_LEAF, leafDry = m == WILLOW ? WILLOW_LEAF_DRY : OAK_LEAF_DRY;
                buckets.Add(new ScatterBucket(device, tree.Wood, treeInstances[m], OAK_BARK, OAK_BARK * 1.2f, dapple: 0f, bark: 0.6f, detailOnly: false));
                //The solid lumps for the Low tier, which casts nothing (the sun's map exists only at scene detail); at every
                //other, twigs with leaves (#697, the acacias' #610 split)
                buckets.Add(new ScatterBucket(device, tree.Crown, treeInstances[m], leaf, leafDry, dapple: 0.7f, bark: 0f,
                    detailOnly: false, lowOnly: true));
                //The twigs every clump's leaves grow from (#697): bark, too thin to cast anything worth a second draw
                buckets.Add(new ScatterBucket(device, tree.Twigs, treeInstances[m], OAK_BARK, OAK_BARK * 1.2f, dapple: 0f, bark: 0.3f,
                    detailOnly: true, castsShadow: false));
                //Broad leaves, not the acacia's leaflets (LeafStrength 2, Acacia.fx's BroadLeafMask). They cast the crown's
                //shade, cut by their own leaves (ShadowCaster clips the card mask): until #697 a lump under them cast it,
                //and the cards inside its lobes lay in its shadow whatever the sun did
                buckets.Add(new ScatterBucket(device, tree.Leaves, treeInstances[m], leaf, leafDry, dapple: 0f, bark: 0f,
                    detailOnly: true, leaves: 2f));
            }
            buckets.Add(new ScatterBucket(device, Own(BenchMesh(device)), benchInstances, BENCH_WOOD, BENCH_WOOD * 1.2f, dapple: 0f, bark: 0.35f,
                detailOnly: false));
            for (int m = 0; m < shrubMeshes.Length; m++)
                if (shrubInstances[m].Count > 0)
                    buckets.Add(new ScatterBucket(device, shrubMeshes[m], shrubInstances[m], SHRUB, SHRUB_DRY, dapple: 0.6f, bark: 0f, detailOnly: false));
            if (baleInstances.Count > 0)
                buckets.Add(new ScatterBucket(device, bale, baleInstances, STRAW, STRAW * new Vector3(0.9f, 0.85f, 0.7f), dapple: 0.3f, bark: 0.3f, detailOnly: false));
            if (fenceInstances.Count > 0)
                buckets.Add(new ScatterBucket(device, fence, fenceInstances, FENCE_WOOD, FENCE_WOOD * 1.25f, dapple: 0f, bark: 0.5f, detailOnly: false));
            for (int m = 0; m < rockMeshes.Length; m++)
                if (rockInstances[m].Count > 0)
                    buckets.Add(new ScatterBucket(device, rockMeshes[m], rockInstances[m], STONE, STONE * new Vector3(0.8f, 1.0f, 0.7f), dapple: 0.45f, bark: 0f, detailOnly: false));
            var bermAt = new List<ModelInstance> { new(Matrix.Identity, new Vector4(0.3f, 0f, 0f, 0f)) };
            buckets.Add(new ScatterBucket(device, bermEarth, bermAt, BERM_EARTH, BERM_EARTH * 1.2f, dapple: 0.6f, bark: 0f, detailOnly: false));
            buckets.Add(new ScatterBucket(device, bermTurf, bermAt, config.GrassColorDark.ToVector3(), config.GrassColor.ToVector3(),
                dapple: 0.6f, bark: 0f, detailOnly: false));
            if (reedInstances.Count > 0)
                buckets.Add(new ScatterBucket(device, reeds, reedInstances, REED, REED * new Vector3(1.5f, 1.3f, 0.8f), dapple: 0.4f, bark: 0f, detailOnly: false));
            for (int m = 0; m < tuftMeshes.Length; m++)
                buckets.Add(new ScatterBucket(device, tuftMeshes[m], tuftInstances[m], TUFT, TUFT * new Vector3(1.6f, 1.3f, 0.9f), dapple: 0.5f, bark: 0f, detailOnly: true));

            foreach (ScatterBucket b in buckets) _owned.Add(b);
            Buckets = buckets.ToArray();
            Trees = treeFigures;
        }

        //How far everything keeps off the pond's shore, past its own reach, and how far out of the water the willows stand
        private const float POND_CLEARANCE = 4f;
        private const float WILLOW_FROM_SHORE = 3.5f;

        //The bench under the old tree: how far from the trunk's axis it stands. The bare ground round the foot is
        //Meadow.fx's TREE_BARE, which the grass tufts keep off.
        private const float BENCH_OUT = 6f;
        private const float TREE_BARE = 4.8f;

        /// <summary>
        /// A plain wooden bench (#609's third round), the references' bench under the tree at a path's end: a seat of
        /// two planks along X on four legs, a back of one plank behind it, facing +Z.
        /// </summary>
        private static UploadedMesh BenchMesh(GraphicsDevice device)
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            const float LENGTH = 1.9f, SEAT = 0.46f;
            AddBox(v, idx, new Vector3(0f, SEAT, 0.1f), new Vector3(LENGTH * 0.5f, 0.035f, 0.11f));
            AddBox(v, idx, new Vector3(0f, SEAT, -0.14f), new Vector3(LENGTH * 0.5f, 0.035f, 0.11f));
            AddBox(v, idx, new Vector3(0f, SEAT + 0.42f, -0.3f), new Vector3(LENGTH * 0.5f, 0.1f, 0.03f));
            foreach (float x in new[] { -LENGTH * 0.4f, LENGTH * 0.4f })
            {
                AddBox(v, idx, new Vector3(x, SEAT * 0.5f - 0.1f, 0.14f), new Vector3(0.05f, SEAT * 0.5f + 0.1f, 0.05f));
                AddBox(v, idx, new Vector3(x, (SEAT + 0.52f) * 0.5f - 0.1f, -0.3f), new Vector3(0.05f, (SEAT + 0.52f) * 0.5f + 0.1f, 0.05f));
            }
            return new UploadedMesh(device, v, idx, new BoundingSphere(new Vector3(0f, 0.5f, 0f), 1.3f));
        }

        /// <summary>
        /// An axis-aligned box, its faces wound clockwise seen from outside (CLAUDE.md, "Triangle winding"): on each
        /// face (u, w) span it with u × w the outward normal, and the triangles run (−u−w, −u+w, +u+w), whose
        /// (b − a) × (c − a) is w × u — the inward normal, which is what this renderer draws as the front.
        /// </summary>
        private static void AddBox(List<VertexPositionNormalTexture> v, List<short> idx, Vector3 centre, Vector3 half)
        {
            (Vector3 N, Vector3 U, Vector3 W)[] faces =
            {
                (Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ), (-Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY),
                (Vector3.UnitY, Vector3.UnitZ, Vector3.UnitX), (-Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ),
                (Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY), (-Vector3.UnitZ, Vector3.UnitY, Vector3.UnitX),
            };
            foreach ((Vector3 n, Vector3 u, Vector3 w) in faces)
            {
                Vector3 c = centre + n * Vector3.Dot(half, Abs(n));
                Vector3 du = u * Vector3.Dot(half, Abs(u)), dw = w * Vector3.Dot(half, Abs(w));
                short b = (short)v.Count;
                v.Add(new VertexPositionNormalTexture(c - du - dw, n, Vector2.Zero));
                v.Add(new VertexPositionNormalTexture(c - du + dw, n, Vector2.UnitY));
                v.Add(new VertexPositionNormalTexture(c + du + dw, n, Vector2.One));
                v.Add(new VertexPositionNormalTexture(c + du - dw, n, Vector2.UnitX));
                idx.Add(b); idx.Add((short)(b + 1)); idx.Add((short)(b + 2));
                idx.Add(b); idx.Add((short)(b + 2)); idx.Add((short)(b + 3));
            }

            static Vector3 Abs(Vector3 a) => new(MathF.Abs(a.X), MathF.Abs(a.Y), MathF.Abs(a.Z));
        }

        //A fence segment's length, post to post, in world units
        private const float FENCE_SPAN = 3.2f;

        //How far everything planted keeps off the footpath's centreline, past its own reach
        private const float PATH_CLEARANCE = 2.2f;

        //How far the first fence runs to the side of the footpath's centreline
        private const float FENCE_BESIDE_PATH = 2.6f;

        /// <summary>A round bale lying on its side: a straw cylinder along X with its two faces, resting on the ground.</summary>
        private static UploadedMesh BaleMesh(GraphicsDevice device)
        {
            const float RADIUS = 0.8f, HALF = 0.72f;
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            Vector3 a = new(-HALF, RADIUS, 0f), b = new(HALF, RADIUS, 0f);
            TubeGeometry.AddTube(v, idx, 18, a, RADIUS, b, RADIUS);
            TubeGeometry.AddCap(v, idx, 18, b, RADIUS * 0.98f, Vector3.UnitX);
            TubeGeometry.AddCap(v, idx, 18, a, RADIUS * 0.98f, -Vector3.UnitX);
            return new UploadedMesh(device, v, idx, new BoundingSphere(new Vector3(0f, RADIUS, 0f), 1.2f));
        }

        /// <summary>
        /// One span of post-and-rail fence along +X: a post at the origin and two rails to the next post, which is the
        /// next span's own. Weathered by the bark field, as the references' grey timber is.
        /// </summary>
        private static UploadedMesh FenceMesh(GraphicsDevice device)
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            TubeGeometry.AddTube(v, idx, 6, new Vector3(0f, -0.3f, 0f), 0.10f, new Vector3(0f, 1.35f, 0f), 0.085f);
            TubeGeometry.AddCap(v, idx, 6, new Vector3(0f, 1.35f, 0f), 0.085f, Vector3.Up);
            foreach (float y in new[] { 0.55f, 1.05f })
                TubeGeometry.AddTube(v, idx, 5, new Vector3(-0.05f, y, 0.06f), 0.055f, new Vector3(FENCE_SPAN + 0.05f, y, 0.06f), 0.055f);
            return new UploadedMesh(device, v, idx, new BoundingSphere(new Vector3(FENCE_SPAN * 0.5f, 0.6f, 0f), FENCE_SPAN * 0.6f));
        }

        private T Own<T>(T disposable) where T : IDisposable
        {
            _owned.Add(disposable);
            return disposable;
        }

        public void Dispose()
        {
            foreach (IDisposable d in _owned) d.Dispose();
            _owned.Clear();
        }
    }
}
