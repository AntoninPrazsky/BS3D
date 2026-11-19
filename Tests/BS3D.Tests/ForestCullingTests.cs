using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The forest's wood drawn by what a frustum meets (#1099) and its near crowns round the lens (#1120, #1163): a sector is
    /// skipped only when nothing in it can reach the frustum, the play camera's share is a run or two, a planted tree is tested
    /// by its placed bounds, and a spruce the camera sees wears the crown its distance from the lens gives it on every frame
    /// of a flight, while the play camera walked round the island swaps none in either pass. <see cref="ForestPasses"/> is
    /// reached whole: a pass takes what its frustum meets and nothing else, and the sun's pass, which comes first, gives a tree
    /// the camera sees the crown it is drawn in and one it does not see the crown of the arena's reach alone.
    /// </summary>
    public class ForestCullingTests
    {
        private static BoundingFrustum Lens(Vector3 at, Vector3 target, float fov, float aspect, float far) =>
            new(Matrix.CreateLookAt(at, target, Vector3.Up) * Matrix.CreatePerspectiveFieldOfView(fov, aspect, 0.1f, far));

        //A wood like the forest's: points in a ring round the axis, every one with a sphere of its own
        private static (Vector3[] Centres, float[] Radii) Ring(int seed, int count, float inner, float outer)
        {
            var rng = new Random(seed);
            var centres = new Vector3[count];
            var radii = new float[count];
            for (int i = 0; i < count; i++)
            {
                float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                float r = inner + (float)rng.NextDouble() * (outer - inner);
                centres[i] = new Vector3(MathF.Cos(a) * r, -10f + (float)rng.NextDouble() * 30f, MathF.Sin(a) * r);
                radii[i] = 0.5f + (float)rng.NextDouble() * 12f;
            }
            return (centres, radii);
        }

        //Every lens the hosts use, roughly: the play camera, the menu's orbit, an intro's flight among the trees, a look down
        private static IEnumerable<(Vector3 At, Vector3 Target)> Lenses()
        {
            yield return (new Vector3(0f, -4f, 30f), new Vector3(0f, -2f, -60f));
            yield return (new Vector3(35f, -8f, 45f), new Vector3(70f, -14f, 110f));
            yield return (new Vector3(0f, 25f, 60f), new Vector3(30f, -14f, 140f));
            yield return (new Vector3(-150f, 2f, 40f), new Vector3(-120f, 0f, 80f));
            yield return (new Vector3(10f, 120f, 10f), new Vector3(0f, -10f, -40f));
            yield return (new Vector3(200f, 10f, -200f), new Vector3(0f, 0f, 0f));
        }

        [Theory]
        [InlineData(7, 34f, 560f)]
        [InlineData(13, 270f, 560f)]
        [InlineData(17, 34f, 150f)]
        public void Every_instance_that_meets_the_frustum_is_in_a_run(int seed, float inner, float outer)
        {
            (Vector3[] centres, float[] radii) = Ring(seed, 3000, inner, outer);
            InstanceCells cells = InstanceCells.Build(centres, radii, out int[] order);

            Assert.Equal(centres.Length, cells.Total);
            Assert.Equal(centres.Length, new HashSet<int>(order).Count);

            foreach ((Vector3 at, Vector3 target) in Lenses())
                foreach (float fov in new[] { 0.5f, 1.1f })
                {
                    BoundingFrustum frustum = Lens(at, target, fov, 2.4f, 3000f);
                    int runs = cells.Runs(frustum);
                    var drawn = new bool[centres.Length];
                    for (int r = 0; r < runs; r++)
                    {
                        Assert.True(cells.RunCount[r] > 0);
                        if (r > 0) Assert.True(cells.RunStart[r] > cells.RunStart[r - 1] + cells.RunCount[r - 1] - 1);
                        for (int k = cells.RunStart[r]; k < cells.RunStart[r] + cells.RunCount[r]; k++) drawn[order[k]] = true;
                    }

                    for (int i = 0; i < centres.Length; i++)
                    {
                        var sphere = new BoundingSphere(centres[i], radii[i]);
                        if (frustum.Contains(sphere) == ContainmentType.Disjoint) continue;
                        Assert.True(drawn[i], $"instance {i} at {centres[i]} meets the lens at {at} but is in no run");
                    }
                }
        }

        [Fact]
        public void The_play_camera_draws_under_half_of_the_ring_in_a_run_or_two_a_ring()
        {
            (Vector3[] centres, float[] radii) = Ring(11, 2000, 34f, 300f);
            InstanceCells cells = InstanceCells.Build(centres, radii, out _);
            var at = new Vector3(0f, -4f, 30f);
            int runs = cells.Runs(Lens(at, new Vector3(0f, -2f, -60f), 0.7f, 2.4f, 3000f));
            int drawn = 0;
            for (int r = 0; r < runs; r++) drawn += cells.RunCount[r];
            Assert.True(runs >= 1 && runs <= 2 * cells.Rings && drawn < centres.Length * 0.5f,
                $"the play camera drew {drawn} of {centres.Length} in {runs} runs over {cells.Rings} rings");
        }

        [Fact]
        public void A_bucket_laid_out_by_cell_keeps_every_instance_once()
        {
            (Vector3[] centres, _) = Ring(3, 500, 34f, 300f);
            var instances = new List<ModelInstance>();
            for (int i = 0; i < centres.Length; i++)
                instances.Add(new ModelInstance(Matrix.CreateScale(1f + i % 3) * Matrix.CreateTranslation(centres[i]), new Vector4(i, 0f, 0f, 0f)));
            var mesh = new BoundsOnly(new BoundingSphere(new Vector3(0f, 0.5f, 0f), 1.6f));

            var bucket = new ScatterBucket(null, mesh, instances, Vector3.One, Vector3.One, 0f, 0f, false, cells: true);

            Assert.NotNull(bucket.Cells);
            Assert.Equal(instances.Count, bucket.Placed.Count);
            var seen = new HashSet<float>();
            foreach (ModelInstance placed in bucket.Placed) Assert.True(seen.Add(placed.Custom.X));
            //In the cells' order: each instance stands in the cell its run is counted in
            for (int c = 0; c < bucket.Cells.Start.Length; c++)
                for (int k = bucket.Cells.Start[c]; k < bucket.Cells.Start[c] + bucket.Cells.Count[c]; k++)
                    Assert.Equal(c, bucket.Cells.CellOf(bucket.Placed[k].World.Translation));

            //And a bucket asked for no cells is as planted
            var plain = new ScatterBucket(null, mesh, instances, Vector3.One, Vector3.One, 0f, 0f, false);
            Assert.Null(plain.Cells);
            for (int i = 0; i < instances.Count; i++) Assert.Equal(instances[i].Custom.X, plain.Placed[i].Custom.X);
        }

        private sealed class BoundsOnly : IProceduralMesh
        {
            public BoundsOnly(BoundingSphere sphere) => BoundingSphere = sphere;
            public Microsoft.Xna.Framework.Graphics.VertexBuffer VertexBuffer => null;
            public Microsoft.Xna.Framework.Graphics.IndexBuffer IndexBuffer => null;
            public int PrimitiveCount => 0;
            public BoundingSphere BoundingSphere { get; }
        }

        [Fact]
        public void A_planted_mesh_meets_the_frustum_exactly_when_its_placed_sphere_does()
        {
            var local = new BoundingSphere(new Vector3(0.3f, 9f, -0.2f), 7f);
            BoundingFrustum frustum = Lens(new Vector3(0f, -4f, 30f), new Vector3(0f, -2f, -60f), 1.0f, 2.4f, 3000f);
            var rng = new Random(5);
            int met = 0;
            for (int i = 0; i < 4000; i++)
            {
                float scale = 0.2f + (float)rng.NextDouble() * 1.5f;
                Matrix world = Matrix.CreateScale(scale) * Matrix.CreateRotationY((float)rng.NextDouble() * 6f)
                    * Matrix.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1f, 0f, (float)rng.NextDouble())), 0.3f * (float)rng.NextDouble())
                    * Matrix.CreateTranslation((float)rng.NextDouble() * 600f - 300f, -12f, (float)rng.NextDouble() * 600f - 300f);
                var placed = new BoundingSphere(Vector3.Transform(local.Center, world), local.Radius * scale);
                bool expected = frustum.Contains(placed) != ContainmentType.Disjoint;
                Assert.Equal(expected, ForestScatterRenderer.Meets(frustum, local, world));
                if (expected) met++;
            }
            Assert.InRange(met, 200, 3800);
        }

        //The cull is only as exact as the bounds it is handed: every vertex the dressing draws lies inside the sphere its bucket
        //is culled by, or a crown at the screen's edge loses its outer leaves
        [Fact]
        public void Every_vertex_of_the_dressing_lies_inside_the_bounds_it_is_culled_by()
        {
            var config = new ForestSceneConfig();
            foreach (ForestScatterRenderer.BroadleafVariant variant in ForestScatterRenderer.BROADLEAF_SHAPES)
            {
                (float trunkRadius, float trunkHeight, float crownRadius, float crownHeight) =
                    ForestScatterRenderer.BroadleafFigures(config.Trees, variant);
                MeadowTreeGeometry g = MeadowTreeMesh.ForestGeometry(trunkRadius, trunkHeight, crownRadius, crownHeight, variant.Seed, inLeaf: true);
                foreach (List<Microsoft.Xna.Framework.Graphics.VertexPositionNormalTexture> part in
                    new[] { g.WoodVertices, g.FineWoodVertices, g.CrownVertices, g.TwigVertices, g.LeafVertices })
                    foreach (var vertex in part)
                        Assert.True(Vector3.Distance(vertex.Position, g.Bounds.Center) <= g.Bounds.Radius * 1.0001f,
                            $"broadleaf {variant.Seed}: a vertex at {vertex.Position} lies {Vector3.Distance(vertex.Position, g.Bounds.Center):F2} from the bounds' centre, radius {g.Bounds.Radius:F2}");
            }

            //The ferns at the seeds the hosts roll (the default plus a random offset a launch, unless sceneseed= pins it): a sphere
            //of 1.6 left a frond tip outside at about one in twelve
            var offsets = new List<int>();
            for (int o = 0; o < 500; o++) offsets.Add(o);
            var rng = new Random(1099);
            for (int o = 0; o < 300; o++) offsets.Add(rng.Next());
            foreach (int offset in offsets)
                for (int k = 0; k < 3; k++)
                {
                    (List<Microsoft.Xna.Framework.Graphics.VertexPositionNormalTexture> vertices, _, BoundingSphere bounds) =
                        ForestDressing.Fern(ForestScatterRenderer.DEFAULT_SEED + offset, k);
                    foreach (var vertex in vertices)
                        Assert.True(Vector3.Distance(vertex.Position, bounds.Center) <= bounds.Radius * 1.0001f,
                            $"fern {k} at offset {offset}: a vertex at {vertex.Position} lies {Vector3.Distance(vertex.Position, bounds.Center):F3} from the bounds' centre, radius {bounds.Radius:F3}");
                }
        }

        [Fact]
        public void A_spruce_wears_the_near_crown_round_the_arena_and_round_the_lens()
        {
            Vector3 lens = new(-150f, 0f, 40f);
            Assert.True(ForestScatterRenderer.WearsNearCrown(new Vector3(60f, -12f, -60f), lens));
            Assert.False(ForestScatterRenderer.WearsNearCrown(new Vector3(150f, -12f, 40f), lens));
            Assert.True(ForestScatterRenderer.WearsNearCrown(new Vector3(-150f + ForestScatterRenderer.NEAR_LENS_RADIUS * 0.9f, -12f, 40f), lens));
            Assert.False(ForestScatterRenderer.WearsNearCrown(new Vector3(-150f - ForestScatterRenderer.NEAR_LENS_RADIUS * 1.1f, -12f, 40f), lens));
            //The height does not count: a tree on a hill under the lens is as near as one on the floor
            Assert.True(ForestScatterRenderer.WearsNearCrown(new Vector3(-150f, 40f, 40f + ForestScatterRenderer.NEAR_LENS_RADIUS * 0.9f), lens));
        }

        //The forest's own planting, with stand-in bounds for the meshes a device would build: a tree some twenty units tall
        private static ForestPasses Passes(out ForestScatter scatter)
        {
            var config = new ForestSceneConfig();
            scatter = ForestScatterRenderer.Plant(ForestScatterRenderer.DEFAULT_SEED, config, (x, z) => TerrainMirror.Forest(x, z, config));
            var tree = new BoundingSphere(new Vector3(0f, 9f, 0f), 10f);
            var low = new BoundingSphere(new Vector3(0f, 0.5f, 0f), 2f);
            static BoundingSphere[] Each(ModelInstance[][] kind, BoundingSphere sphere)
            {
                var all = new BoundingSphere[kind.Length];
                Array.Fill(all, sphere);
                return all;
            }
            return new ForestPasses(scatter, Each(scatter.FineConifers, tree), Each(scatter.FineBroadleaves, tree),
                Each(scatter.FineRocks, low), Each(scatter.FineStumps, low), Each(scatter.FineSnags, tree), Each(scatter.FineLogs, low));
        }

        private static Matrix ViewProjection(Vector3 at, Vector3 target) =>
            Matrix.CreateLookAt(at, target, Vector3.Up) * Matrix.CreatePerspectiveFieldOfView(0.9f, 2.4f, 0.1f, 3000f);

        //The sun's map: a box round the clearing seen from the forest's low sun, as SunShadowMap lays it
        private static readonly Matrix SUN = Matrix.CreateLookAt(new Vector3(-490f, 95f, -150f), Vector3.Zero, Vector3.Up)
            * Matrix.CreateOrthographic(700f, 700f, 1f, 1200f);

        private static HashSet<Vector3> Where(ForestPasses.Culled set)
        {
            var at = new HashSet<Vector3>();
            for (int v = 0; v < set.Items.Length; v++)
                for (int i = 0; i < set.Counts[v]; i++) at.Add(set.Items[v][i].World.Translation);
            return at;
        }

        [Fact]
        public void Every_frame_of_a_flight_gives_each_spruce_the_camera_sees_its_crown_by_the_lens_and_casts_it()
        {
            ForestPasses passes = Passes(out ForestScatter scatter);
            var tree = new BoundingSphere(new Vector3(0f, 9f, 0f), 10f);

            //From the play camera out through the wood, a unit a frame and no cut, the frame's sun pass first, as
            //SceneRenderer.DrawShadowMaps runs it
            var start = new Vector3(0f, -4f, 30f);
            var heading = Vector3.Normalize(new Vector3(-0.8f, 0.02f, 0.6f));
            int nearByLens = 0, both = 0, unseenByLens = 0;
            for (int frame = 0; frame < 240; frame++)
            {
                Vector3 lens = start + heading * frame;
                Matrix camera = ViewProjection(lens, lens + heading * 30f);
                passes.SunPass(SUN, camera, lens, true);
                HashSet<Vector3> castNear = Where(passes.NearConifers), castFar = Where(passes.FarConifers);
                passes.CameraPass(camera, lens, true);
                HashSet<Vector3> drawnNear = Where(passes.NearConifers), drawnFar = Where(passes.FarConifers);

                //What the camera's frustum meets is drawn, and nothing else, each spruce in the crown its distance from this
                //frame's lens gives it
                var frustum = new BoundingFrustum(camera);
                foreach (ModelInstance[] bucket in scatter.FineConifers)
                    foreach (ModelInstance planted in bucket)
                    {
                        Vector3 at = planted.World.Translation;
                        bool seen = ForestScatterRenderer.Meets(frustum, tree, planted.World);
                        float fromLens = new Vector2(at.X - lens.X, at.Z - lens.Z).Length();
                        bool fromArena = new Vector2(at.X, at.Z).Length() < ForestScatterRenderer.FINE_NEAR_RADIUS;
                        Assert.Equal(seen, drawnNear.Contains(at) || drawnFar.Contains(at));
                        if (!seen)
                        {
                            //One the camera does not see casts by the arena's reach alone, however near the lens: its shadow
                            //does not follow the lens from off the screen
                            if (!castNear.Contains(at) && !castFar.Contains(at)) continue;
                            Assert.True(fromArena == castNear.Contains(at),
                                $"frame {frame}: a spruce off the screen {fromLens:F1} from the lens, {(fromArena ? "inside" : "outside")} the arena's reach, casts the wrong crown");
                            if (!fromArena && fromLens < ForestScatterRenderer.NEAR_LENS_RADIUS) unseenByLens++;
                            continue;
                        }
                        bool near = fromArena || fromLens < ForestScatterRenderer.NEAR_LENS_RADIUS;
                        Assert.True(near == drawnNear.Contains(at),
                            $"frame {frame}: a spruce {fromLens:F1} from the lens, {(fromArena ? "inside" : "outside")} the arena's reach, is drawn in the wrong crown");
                        //Near the lens, out in the wood, a long way from where the flight began: the crown follows the lens itself
                        if (near && !fromArena && Vector3.Distance(lens, start) > 2f * ForestScatterRenderer.NEAR_LENS_RADIUS) nearByLens++;
                    }
                Assert.Equal(passes.Conifers.Counts.Sum(), drawnNear.Count + drawnFar.Count);

                //And every spruce both passes take casts the crown it is drawn in
                foreach (Vector3 at in drawnNear) if (castNear.Contains(at) || castFar.Contains(at)) { both++; Assert.Contains(at, castNear); }
                foreach (Vector3 at in drawnFar) if (castNear.Contains(at) || castFar.Contains(at)) { both++; Assert.Contains(at, castFar); }
            }
            Assert.True(nearByLens > 0, "no spruce near the lens out in the wood, far from where the flight began, wears the near crown");
            Assert.True(both > 0);
            Assert.True(unseenByLens > 0, "no spruce the camera does not see stood near the lens outside the arena's reach in the map");
        }

        //The play camera stands near the axis and looks in; walked round the island it must swap no crown in either pass, so
        //every near crown it draws or casts is one the arena's reach gives anyway, in both woods. The sun's map is laid round
        //the lens as the hosts lay it, so it holds trees behind the lens that are within its reach and outside the arena's
        [Theory]
        [InlineData(false, 28f)]
        [InlineData(false, 55f)]
        [InlineData(true, 28f)]
        [InlineData(true, 45f)]
        [InlineData(true, 55f)]
        public void The_play_camera_walked_round_the_island_swaps_no_crown_in_either_pass(bool aurora, float standOff)
        {
            ForestPasses passes = aurora ? AuroraPasses(out _) : Passes(out _);
            int nearLensInMap = 0;
            foreach (Vector3 target in new[] { new Vector3(0f, -2f, 0f), new Vector3(0f, 6f, 0f) })
                for (int step = 0; step < 144; step++)
                {
                    float bearing = step * MathHelper.TwoPi / 144f;
                    var lens = new Vector3(MathF.Sin(bearing) * standOff, -1.5f, MathF.Cos(bearing) * standOff);
                    Matrix camera = ViewProjection(lens, target);
                    passes.SunPass(aurora ? AuroraMoonMap(lens, target) : ForestSunMap(lens, target), camera, lens, true);
                    foreach (Vector3 at in Where(passes.Conifers))
                        if (!ForestScatterRenderer.InArenaReach(at) && new Vector2(at.X - lens.X, at.Z - lens.Z).Length() < ForestScatterRenderer.NEAR_LENS_RADIUS)
                            nearLensInMap++;
                    foreach (Vector3 at in Where(passes.NearConifers))
                        Assert.True(ForestScatterRenderer.InArenaReach(at),
                            $"from {lens}, a spruce at {at} outside the arena's reach casts the near crown: its shadow swaps as the gun walks");

                    passes.CameraPass(camera, lens, true);
                    foreach (Vector3 at in Where(passes.NearConifers))
                        Assert.True(ForestScatterRenderer.InArenaReach(at),
                            $"from {lens}, a spruce at {at} outside the arena's reach wears the near crown: it swaps as the gun walks");
                }
            //The map did hold trees the lens's reach would have swapped had the camera seen them
            Assert.True(nearLensInMap > 0, "the sun's map held no spruce near the lens outside the arena's reach");
        }

        //The sun's map round a lens as SceneRenderer lays it (TryShadowFit, SunShadowMap.FitWindow at Ultra's 8192), over a
        //wood's ground as its backdrop states it: the aurora's under its Moon, the forest's under the low sun SUN stands in for
        internal static Matrix AuroraMoonMap(Vector3 lens, Vector3 target)
        {
            var config = new AuroraSceneConfig();
            Vector3 moon = SceneRenderer.DirectionFromElevationAzimuth(
                Math.Clamp(config.Sky.MoonElevationDegrees, 1f, 89f), config.Sky.MoonAzimuthDegrees);
            return MapOver(config.Terrain, config.Shadows.Extent, moon, lens, target);
        }

        private static Matrix ForestSunMap(Vector3 lens, Vector3 target)
        {
            var config = new ForestSceneConfig();
            return MapOver(config, config.Shadows.Extent, Vector3.Normalize(new Vector3(-490f, 95f, -150f)), lens, target);
        }

        private static Matrix MapOver(ForestSceneConfig ground, float extent, Vector3 sun, Vector3 lens, Vector3 target)
        {
            (Vector3 centre, float yMin, float yMax) = SceneRenderer.ShadowBox(lens, target, extent,
                ground.LevelY, ground.HillHeight * 0.5f, ground.HillHeight);
            return SunShadowMap.FitWindow(8192, centre, sun, extent, yMin, yMax).ViewProjection;
        }

        //The aurora's planting with its far stand's spruces, as AuroraBackdrop hands them over (#1163)
        internal static ForestPasses AuroraPasses(out ForestScatter scatter)
        {
            var config = new AuroraSceneConfig();
            scatter = ForestScatterRenderer.Plant(ForestScatterRenderer.DEFAULT_SEED, config.Terrain, (x, z) => TerrainMirror.AuroraBase(x, z, config));
            (AuroraBackdrop.StandInstance[] nearRing, _) = AuroraBackdrop.PlantStandInstances(config, config.FarStand.InnerRadius,
                config.FarStand.NearRadius, config.FarStand.Count, 0, AuroraBackdrop.TakenCells(scatter));
            scatter.Stand = AuroraBackdrop.StandSprucesOf(nearRing, config);
            var tree = new BoundingSphere(new Vector3(0f, 15f, 0f), 18f);
            var low = new BoundingSphere(new Vector3(0f, 0.5f, 0f), 2f);
            static BoundingSphere[] Each(ModelInstance[][] kind, BoundingSphere sphere)
            {
                var all = new BoundingSphere[kind.Length];
                Array.Fill(all, sphere);
                return all;
            }
            return new ForestPasses(scatter, Each(scatter.FineConifers, tree), Each(scatter.FineBroadleaves, tree),
                Each(scatter.FineRocks, low), Each(scatter.FineStumps, low), Each(scatter.FineSnags, tree), Each(scatter.FineLogs, low));
        }

        internal static Matrix ViewProjectionFrom(Vector3 at, Vector3 target) => ViewProjection(at, target);

        internal static HashSet<Vector3> WhereDrawn(ForestPasses.Culled set) => Where(set);
    }
}
