using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The forest's wood drawn by what a frustum meets (#1099) and its near crowns round the focus (#1120): a cell is skipped only
    /// when nothing in it can reach the frustum, a run never splits what is drawn, a reach drops only what lies wholly past
    /// it, and the focus moves on a cut and never during a flight.
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
        [InlineData(0f, 0f)]
        [InlineData(30f, 0f)]
        [InlineData(30f, 90f)]
        [InlineData(25f, 140f)]
        public void Every_instance_that_meets_the_frustum_and_the_reach_is_in_a_run(float ringWidth, float reach)
        {
            (Vector3[] centres, float[] radii) = Ring(7, 3000, 34f, 560f);
            InstanceCells cells = InstanceCells.Build(centres, radii, ringWidth, out int[] order);

            Assert.Equal(centres.Length, cells.Total);
            Assert.Equal(centres.Length, new HashSet<int>(order).Count);

            foreach ((Vector3 at, Vector3 target) in Lenses())
                foreach (float fov in new[] { 0.5f, 1.1f })
                {
                    BoundingFrustum frustum = Lens(at, target, fov, 2.4f, 3000f);
                    int runs = cells.Runs(frustum, at, reach);
                    var drawn = new bool[centres.Length];
                    for (int r = 0; r < runs; r++)
                    {
                        Assert.True(cells.RunCount[r] > 0);
                        if (r > 0) Assert.True(cells.RunStart[r] > cells.RunStart[r - 1] + cells.RunCount[r - 1] - 1);
                        for (int k = cells.RunStart[r]; k < cells.RunStart[r] + cells.RunCount[r]; k++) drawn[order[k]] = true;
                    }

                    int needed = 0, kept = 0;
                    for (int i = 0; i < centres.Length; i++)
                    {
                        var sphere = new BoundingSphere(centres[i], radii[i]);
                        bool meets = frustum.Contains(sphere) != ContainmentType.Disjoint;
                        bool inReach = reach <= 0f || Vector3.Distance(centres[i], at) - radii[i] <= reach;
                        if (!meets || !inReach) continue;
                        needed++;
                        Assert.True(drawn[i], $"instance {i} at {centres[i]} meets the lens at {at} but is in no run");
                    }
                    for (int i = 0; i < centres.Length; i++) if (drawn[i]) kept++;
                    //And it cuts: a lens in the clearing looking out across the wood sees a part of the ring (one looking down
                    //on the axis, or across it from the far side, meets every sector, each of which reaches in towards it)
                    bool inClearing = at.X * at.X + at.Z * at.Z < 60f * 60f && at.Y < 100f;
                    if (needed > 0 && inClearing) Assert.True(kept < centres.Length, $"the lens at {at} drew all {centres.Length}");
                }
        }

        [Fact]
        public void A_reach_drops_the_rings_wholly_past_it()
        {
            (Vector3[] centres, float[] radii) = Ring(11, 2000, 34f, 300f);
            for (int i = 0; i < radii.Length; i++) radii[i] = 1.6f;
            InstanceCells cells = InstanceCells.Build(centres, radii, 30f, out int[] order);
            var at = new Vector3(0f, -4f, 30f);
            BoundingFrustum frustum = Lens(at, new Vector3(0f, -2f, -60f), 1.1f, 2.4f, 3000f);

            int all = Count(cells, cells.Runs(frustum, at)), near = Count(cells, cells.Runs(frustum, at, 90f));
            Assert.True(near < all / 2, $"a reach of 90 kept {near} of the {all} the frustum alone keeps");

            //Nothing drawn lies farther than the reach and the ring's own width allow
            int runs = cells.Runs(frustum, at, 90f);
            for (int r = 0; r < runs; r++)
                for (int k = cells.RunStart[r]; k < cells.RunStart[r] + cells.RunCount[r]; k++)
                    Assert.True(Vector3.Distance(centres[order[k]], at) < 90f + 2f * 30f + 40f);
        }

        private static int Count(InstanceCells cells, int runs)
        {
            int n = 0;
            for (int r = 0; r < runs; r++) n += cells.RunCount[r];
            return n;
        }

        [Fact]
        public void A_bucket_laid_out_by_cell_keeps_every_instance_once()
        {
            (Vector3[] centres, _) = Ring(3, 500, 34f, 300f);
            var instances = new List<ModelInstance>();
            for (int i = 0; i < centres.Length; i++)
                instances.Add(new ModelInstance(Matrix.CreateScale(1f + i % 3) * Matrix.CreateTranslation(centres[i]), new Vector4(i, 0f, 0f, 0f)));
            var mesh = new BoundsOnly(new BoundingSphere(new Vector3(0f, 0.5f, 0f), 1.6f));

            var bucket = new ScatterBucket(null, mesh, instances, Vector3.One, Vector3.One, 0f, 0f, false, cellRing: 30f, reach: 90f);

            Assert.NotNull(bucket.Cells);
            Assert.Equal(90f, bucket.Reach);
            Assert.Equal(instances.Count, bucket.Placed.Count);
            var seen = new HashSet<float>();
            foreach (ModelInstance placed in bucket.Placed) Assert.True(seen.Add(placed.Custom.X));
            //In the cells' order: each instance stands in the cell its run is counted in
            for (int c = 0; c < bucket.Cells.Count.Length; c++)
                for (int k = bucket.Cells.Start[c]; k < bucket.Cells.Start[c] + bucket.Cells.Count[c]; k++)
                    Assert.Equal(c, bucket.Cells.CellOf(bucket.Placed[k].World.Translation + new Vector3(0f, 0.5f * bucket.Placed[k].World.M11, 0f)));

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

        [Fact]
        public void A_spruce_wears_the_near_crown_round_the_arena_and_round_the_focus()
        {
            Vector3 focus = new(-150f, 0f, 40f);
            Assert.True(ForestScatterRenderer.WearsNearCrown(new Vector3(60f, -12f, -60f), focus));
            Assert.False(ForestScatterRenderer.WearsNearCrown(new Vector3(150f, -12f, 40f), focus));
            Assert.True(ForestScatterRenderer.WearsNearCrown(new Vector3(-150f + ForestScatterRenderer.FOCUS_RADIUS * 0.9f, -12f, 40f), focus));
            Assert.False(ForestScatterRenderer.WearsNearCrown(new Vector3(-150f - ForestScatterRenderer.FOCUS_RADIUS * 1.1f, -12f, 40f), focus));
            //The height does not count: a tree on a hill under the lens is as near as one on the floor
            Assert.True(ForestScatterRenderer.WearsNearCrown(new Vector3(-150f, 40f, 40f + ForestScatterRenderer.FOCUS_RADIUS * 0.9f), focus));
        }

        [Fact]
        public void The_focus_moves_on_a_cut_and_never_during_a_flight()
        {
            var start = new Vector3(-150f, 2f, 40f);
            Vector3 focus = ForestScatterRenderer.NextFocus(false, Vector3.Zero, Vector3.Zero, start);
            Assert.Equal(start, focus);

            //A flight of 60 units at a unit a frame (an intro shot at 15 frames a second is under that) keeps it
            Vector3 lens = start, last = start;
            for (int frame = 0; frame < 60; frame++)
            {
                lens += new Vector3(0.7f, 0.1f, 0.7f);
                focus = ForestScatterRenderer.NextFocus(true, focus, last, lens);
                last = lens;
                Assert.Equal(start, focus);
            }

            //A cut puts it on the lens
            Vector3 cut = new(30f, -4f, 0f);
            focus = ForestScatterRenderer.NextFocus(true, focus, last, cut);
            Assert.Equal(cut, focus);
        }
    }
}
