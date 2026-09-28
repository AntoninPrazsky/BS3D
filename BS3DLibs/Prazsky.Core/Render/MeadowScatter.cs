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
    /// mossy boulders, and grass that stands up in tufts. This plants them on the field <c>Meadow.fx</c> draws —
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
        private const float INNER = 70f;             //nothing nearer than this (the island is 26 across the radius)
        private const float OUTER = 460f;
        private const int OAKS = 9, SHRUBS = 55, BALES = 14, BOULDERS = 18, TUFTS = 700;
        private const int HEDGEROWS = 5, FENCES = 2;
        private const int BERM_STONES = 16, BERM_TUFTS = 120;

        //=== The colours, linear, and the drier shade each instance can lean towards (as the savanna's). Set against
        //the meadow's own grass (0.14/0.46/0.05) rather than the savanna's: at the savanna's canopy the first
        //hedges photographed as black lines on the hills.
        private static readonly Vector3 OAK_LEAF = new(0.100f, 0.210f, 0.045f);
        private static readonly Vector3 OAK_LEAF_DRY = new(0.160f, 0.240f, 0.060f);
        private static readonly Vector3 OAK_BARK = new(0.120f, 0.100f, 0.080f);
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

        /// <summary>The lone oaks, for a camera to point at (the meadow's intro, a later step of #609).</summary>
        public IReadOnlyList<PlantFigure> Oaks { get; }

        /// <param name="config">The meadow: its field (<see cref="TerrainMirror.Meadow"/>) and its footpath
        /// (<see cref="MeadowPath"/>) — nothing is planted on the path, and the first fence runs along it.</param>
        public MeadowScatter(GraphicsDevice device, MeadowSceneConfig config, int seed)
        {
            float height(float x, float z) => TerrainMirror.Meadow(x, z, config);
            float pathLateral(float x, float z) => MeadowPath.Lateral(x, z, config);

            Random rng = new(seed);
            var occupied = new List<(float X, float Z, float R)>();
            var buckets = new List<ScatterBucket>();
            var oakFigures = new List<PlantFigure>();

            bool Free(float x, float z, float r)
            {
                if (MathF.Abs(pathLateral(x, z)) < r + PATH_CLEARANCE) return false;
                if (MathF.Abs(MeadowPath.BrookLateral(x, z, config)) < r + config.BrookWidth) return false;
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

            ModelInstance At(float x, float z, float scale, float yaw, float sink, float dryness, float jitter, float pitch = 0f) =>
                new(Matrix.CreateScale(scale) * Matrix.CreateRotationZ(pitch) * Matrix.CreateRotationY(yaw)
                    * Matrix.CreateTranslation(x, height(x, z) - sink, z),
                    new Vector4(dryness, jitter, 0f, 0f));

            float Jitter() => (float)(rng.NextDouble() - 0.5) * 0.2f;

            //--- The oaks: three trees at rolled proportions, a lobed broad crown on a short thick trunk
            var oakMeshes = new OakMesh[3];
            var oakInstances = new List<ModelInstance>[3];
            for (int m = 0; m < oakMeshes.Length; m++)
            {
                oakMeshes[m] = Own(new OakMesh(device, 6090 + m));
                oakInstances[m] = new List<ModelInstance>();
            }
            for (int i = 0, tries = 0; i < OAKS && tries < 400; tries++)
            {
                (float x, float z) = Site(130f, OUTER);
                if (!Free(x, z, 12f)) continue;
                occupied.Add((x, z, 12f));
                int m = rng.Next(oakMeshes.Length);
                float scale = 0.85f + 0.35f * (float)rng.NextDouble();
                ModelInstance planted = At(x, z, scale, (float)rng.NextDouble() * MathHelper.TwoPi, 0.3f, (float)rng.NextDouble(), Jitter());
                oakInstances[m].Add(planted);
                oakFigures.Add(PlantFigure.Of(oakMeshes[m].Crown.BoundingSphere, planted.World, 1.1f * scale));
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
                //A share of them along the bank's outer edge, the rest out on the field
                (float x, float z) = i < BERM_TUFTS ? Site(IslandBerm.STONE_RADIUS_MAX - 0.5f, IslandBerm.STONE_RADIUS_MAX + 2.5f)
                    : Site(ArenaIsland.RADIUS + 12f, 260f);
                tuftInstances[rng.Next(3)].Add(At(x, z, 0.8f + 0.7f * (float)rng.NextDouble(), (float)rng.NextDouble() * MathHelper.TwoPi,
                    0.05f, (float)rng.NextDouble() * 0.6f, Jitter()));
            }

            //--- The buckets
            for (int m = 0; m < oakMeshes.Length; m++)
            {
                if (oakInstances[m].Count == 0) continue;
                buckets.Add(new ScatterBucket(device, oakMeshes[m].Wood, oakInstances[m], OAK_BARK, OAK_BARK * 1.2f, dapple: 0f, bark: 0.6f, detailOnly: false));
                buckets.Add(new ScatterBucket(device, oakMeshes[m].Crown, oakInstances[m], OAK_LEAF, OAK_LEAF_DRY, dapple: 0.7f, bark: 0f, detailOnly: false));
            }
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
            Oaks = oakFigures;
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

        /// <summary>
        /// A lone field oak, as the references draw one: a short thick trunk forking into a few spreading boughs under
        /// a broad crown of several lumpy masses — never one smooth ellipsoid, which the forest's broadleaf crown is,
        /// and which on a hill brow with its trunk out of sight read as a green pill floating in the sky.
        /// </summary>
        private sealed class OakMesh : IDisposable
        {
            public IProceduralMesh Wood { get; }
            public IProceduralMesh Crown { get; }

            public OakMesh(GraphicsDevice device, int seed)
            {
                Random rng = new(seed);
                float trunkTop = 4.2f + 1.2f * (float)rng.NextDouble();
                float crownY = trunkTop + 5.5f;

                var wv = new List<VertexPositionNormalTexture>();
                var widx = new List<short>();
                TubeGeometry.AddTube(wv, widx, 10, new Vector3(0f, -0.4f, 0f), 1.05f, new Vector3(0f, trunkTop, 0f), 0.75f);

                var fv = new List<VertexPositionNormalTexture>();
                var fidx = new List<short>();

                //The lobes: one on top, the rest in a ring, each on its own bough from the fork
                int lobes = 6 + rng.Next(2);
                float phase = (float)rng.NextDouble() * MathHelper.TwoPi;
                FoliageMesh.Generate(fv, fidx, 5.2f, 3.6f, new Vector3(0f, crownY + 2.2f, 0f), seed * 17 + 1, FoliageStyle.Crown);
                for (int l = 0; l < lobes; l++)
                {
                    float a = phase + MathHelper.TwoPi * l / lobes + ((float)rng.NextDouble() - 0.5f) * 0.5f;
                    float reach = 4.8f + 1.8f * (float)rng.NextDouble();
                    float r = 3.4f + 1.3f * (float)rng.NextDouble();
                    Vector3 centre = new(MathF.Cos(a) * reach, crownY - 0.5f + 1.8f * ((float)rng.NextDouble() - 0.3f), MathF.Sin(a) * reach);
                    FoliageMesh.Generate(fv, fidx, r, r * 0.75f, centre, seed * 17 + 3 + l, FoliageStyle.Crown);

                    //The bough into it, from the fork, ending well inside the mass
                    Vector3 end = new Vector3(0f, crownY - 1.5f, 0f) + (centre - new Vector3(0f, crownY - 1.5f, 0f)) * 0.7f;
                    TubeGeometry.AddTube(wv, widx, 7, new Vector3(0f, trunkTop - 0.4f, 0f), 0.5f, end, 0.2f);
                }

                BoundingSphere bounds = new(new Vector3(0f, crownY, 0f), 11.5f);
                Wood = new UploadedMesh(device, wv, widx, bounds);
                Crown = new UploadedMesh(device, fv, fidx, bounds);
            }

            public void Dispose()
            {
                (Wood as IDisposable)?.Dispose();
                (Crown as IDisposable)?.Dispose();
            }
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
