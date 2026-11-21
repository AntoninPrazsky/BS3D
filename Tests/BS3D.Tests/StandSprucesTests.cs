using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The aurora's far stand drawn as the planted wood's spruces where the lens comes near it (#1163, <see cref="StandSpruces"/>):
    /// on High and Ultra every spire within <see cref="StandSpruces.SWAP_RADIUS"/> of the lens that the camera's frustum meets is
    /// drawn as its spruce, in the crown its distance gives it, none further and none into the Moon's map; each spruce stands on
    /// its spire's foot, turned as it is and as tall; Low and Medium, the dial and the play camera swap none, and the radius
    /// <c>StandVS</c> is handed is 0 exactly where the passes take none; no lens is within the swap of more spruces than the
    /// passes have room for; and <c>Aurora.fx</c>'s <c>StandVS</c> leaves out exactly the spires the passes swap.
    /// </summary>
    public class StandSprucesTests
    {
        //The lenses the issue was about: the chapter intro's run through the wood at its end (sceneseed=0 seed=7, the
        //[introshot] lines), and the scene's viewpoint over the treeline (AuroraBackdrop.TryGetViewpoint) looking out across the stand
        public static IEnumerable<object[]> Lenses() => new[]
        {
            new object[] { new Vector3(-57.79f, -1.68f, -133.67f), new Vector3(-62.45f, 2.42f, -141.51f) },
            new object[] { new Vector3(-26.03f, -9.46f, -80.27f), new Vector3(-31.09f, -8.03f, -88.78f) },
            new object[] { new Vector3(155f, 20f, 0f), new Vector3(300f, 10f, 40f) },
        };

        private static readonly BoundingSphere TREE = new(new Vector3(0f, 15f, 0f), 18f);

        [Theory]
        [MemberData(nameof(Lenses))]
        public void Every_spire_within_the_swap_that_a_pass_meets_is_drawn_as_its_spruce_and_none_beyond(Vector3 lens, Vector3 target)
        {
            //High's radius, then Ultra's (#1208), from one planting: the tier is read at every use, as a tier change reads it
            bool ultra = false;
            ForestPasses passes = ForestCullingTests.AuroraPasses(out ForestScatter scatter, () => ultra);
            StandSpruces stand = scatter.Stand;
            int highSwapped = 0;
            foreach (bool far in new[] { false, true })
            {
                ultra = far;
                float radius = far ? StandSpruces.FAR_DETAIL_SWAP_RADIUS : StandSpruces.SWAP_RADIUS;
                Assert.Equal(radius, stand.SwapRadiusFor(true));
                int swapped = SwappedFrom(passes, stand, lens, target, radius);
                if (far) Assert.True(swapped > highSwapped, $"Ultra swapped {swapped} spires from a lens beside the stand, High {highSwapped}");
                else highSwapped = swapped;
            }
        }

        private static int SwappedFrom(ForestPasses passes, StandSpruces stand, Vector3 lens, Vector3 target, float radius)
        {
            Assert.True(stand.Reaches(lens));
            Matrix camera = ForestCullingTests.ViewProjectionFrom(lens, target);

            //The Moon's pass first, as SceneRenderer.DrawShadowMaps runs it: the stand's spruces cast nothing, as the spires never did
            passes.SunPass(ForestCullingTests.AuroraMoonMap(lens, target), camera, lens, true);
            HashSet<Vector3> cast = ForestCullingTests.WhereDrawn(passes.Conifers);
            foreach (ModelInstance[] bucket in stand.Spruces)
                foreach (ModelInstance spruce in bucket) Assert.DoesNotContain(spruce.World.Translation, cast);

            passes.CameraPass(camera, lens, true);
            HashSet<Vector3> drawn = ForestCullingTests.WhereDrawn(passes.Conifers);
            HashSet<Vector3> near = ForestCullingTests.WhereDrawn(passes.NearConifers);
            var frustum = new BoundingFrustum(camera);

            int swapped = 0;
            foreach (ModelInstance[] bucket in stand.Spruces)
                foreach (ModelInstance spruce in bucket)
                {
                    Vector3 at = spruce.World.Translation;
                    float fromLens = new Vector2(at.X - lens.X, at.Z - lens.Z).Length();
                    bool due = fromLens < radius && ForestScatterRenderer.Meets(frustum, TREE, spruce.World);
                    Assert.True(due == drawn.Contains(at), $"a spire {fromLens:F1} from the lens is {(due ? "not drawn as its spruce" : "drawn as a spruce")}");
                    if (!due) continue;
                    swapped++;
                    Assert.Equal(fromLens < ForestScatterRenderer.NEAR_LENS_RADIUS, near.Contains(at));
                }
            Assert.True(swapped > 0, "no spire was swapped from a lens beside the stand");
            return swapped;
        }

        [Fact]
        public void Each_spruce_stands_on_its_spire_s_foot_as_tall_as_the_spire()
        {
            var config = new AuroraSceneConfig();
            ForestScatter planting = ForestScatterRenderer.Plant(ForestScatterRenderer.DEFAULT_SEED, config.Terrain,
                (x, z) => TerrainMirror.AuroraBase(x, z, config));
            (AuroraBackdrop.StandInstance[] spires, _) = AuroraBackdrop.PlantStandInstances(config, config.FarStand.InnerRadius,
                config.FarStand.NearRadius, config.FarStand.Count, 0, AuroraBackdrop.TakenCells(planting));
            StandSpruces stand = AuroraBackdrop.StandSprucesOf(spires, config);

            int variants = ForestScatterRenderer.CONIFER_VARIANTS.Length;
            var next = new int[variants];
            var used = new int[variants];
            int total = 0, planted = 0;
            for (int i = 0; i < spires.Length; i++)
            {
                Vector4 place = spires[i].Place, look = spires[i].Look;
                float spireTall = config.FarStand.Height * place.W * look.Z;
                int v = StandSpruces.VariantFor(i, spireTall, config.Terrain.Trees);
                used[v]++;
                Vector2 foot = stand.Feet[v][next[v]];
                Matrix world = stand.Spruces[v][next[v]++].World;
                total++;

                //The very floats the shader's swap test reads, where the passes test them and where the spruce stands
                Assert.Equal(place.X, foot.X);
                Assert.Equal(place.Z, foot.Y);
                Assert.Equal(place.X, world.M41);
                Assert.Equal(place.Z, world.M43);

                //Turned as the spire is: StandVS takes a spire's x axis to (cos, 0, sin) of its look, and the spruce's goes
                //there too, but for its lean (a few hundredths of a radian)
                var spireX = new Vector2(look.Y, look.X);
                Vector2 spruceX = Vector2.Normalize(new Vector2(world.M11, world.M13));
                Assert.True(Vector2.Dot(spireX, spruceX) > 0.99f, $"spire {i}: turned towards {spruceX}, its spire towards {spireX}");

                float scale = new Vector3(world.M11, world.M12, world.M13).Length();
                float spruceTall = StandSpruces.SpruceHeight(config.Terrain.Trees, ForestScatterRenderer.CONIFER_VARIANTS[v]) * scale;
                Assert.InRange(spruceTall, spireTall * 0.999f, spireTall * 1.001f);

                //At a scale the planting rolls, wherever a variant stands that tall at one
                if (scale >= config.Terrain.Trees.MinScale * 0.999f && scale <= config.Terrain.Trees.MaxScale * 1.001f) planted++;

                //Planted as a planted spruce is: a little under the snow the spire stood on, never floating
                float ground = TerrainMirror.AuroraBase(place.X, place.Z, config);
                Assert.InRange(world.M42, ground - 0.4f * scale, ground - 0.1f * scale);
            }
            Assert.Equal(spires.Length, total);
            Assert.True(planted >= spires.Length * 0.99f, $"only {planted} of {spires.Length} spruces stand at a scale the planting rolls");
            //And the stand mixes every variant, as a grove does (the tallest two, on their long stems, are the rarest: few spires
            //stand that tall at a planted scale)
            foreach (int count in used) Assert.True(count >= spires.Length / 50, $"a variant wears only {count} of {spires.Length}: {string.Join(", ", used)}");
            Assert.True(stand.InnerRadius >= config.FarStand.InnerRadius - 0.01f);
        }

        [Fact]
        public void Low_and_Medium_the_dial_and_the_play_camera_swap_none()
        {
            ForestPasses passes = ForestCullingTests.AuroraPasses(out ForestScatter scatter);
            var stand = new HashSet<Vector3>();
            foreach (ModelInstance[] bucket in scatter.Stand.Spruces)
                foreach (ModelInstance spruce in bucket) stand.Add(spruce.World.Translation);

            var lens = new Vector3(-57.79f, -1.68f, -133.67f);
            Matrix camera = ForestCullingTests.ViewProjectionFrom(lens, new Vector3(-62.45f, 2.42f, -141.51f));

            //High and Ultra with the dial on is the one case the swap is made in: the radius the backdrop hands StandVS
            //(AuroraBackdrop.Draw) is SwapRadiusFor's, and the passes take spruces exactly where it is not 0
            foreach (bool fine in new[] { false, true })
                foreach (bool dial in new[] { false, true })
                {
                    scatter.Stand.Enabled = dial;
                    float radius = scatter.Stand.SwapRadiusFor(fine);
                    Assert.Equal(fine && dial ? StandSpruces.SWAP_RADIUS : 0f, radius);

                    //Low and Medium draw the lathe wood and the spires whole; the dial out (aurora=all/-standspruces) leaves
                    //the spires spires on High and Ultra too
                    passes.CameraPass(camera, lens, fine);
                    int taken = 0;
                    foreach (Vector3 at in ForestCullingTests.WhereDrawn(passes.Conifers)) if (stand.Contains(at)) taken++;
                    Assert.True(radius > 0f == taken > 0, $"fine meshes {fine}, the dial {dial}: StandVS is handed {radius} and the passes take {taken} spruces");
                }
            scatter.Stand.Enabled = true;

            //The play camera, walked round the island at the stand-offs a level is played from, sees no spire within the swap
            foreach (float standOff in new[] { 30f, 55f })
                for (int step = 0; step < 72; step++)
                {
                    float bearing = step * MathHelper.TwoPi / 72f;
                    var play = new Vector3(MathF.Sin(bearing) * standOff, -4f, MathF.Cos(bearing) * standOff);
                    passes.CameraPass(ForestCullingTests.ViewProjectionFrom(play, new Vector3(0f, -2f, 0f)), play, true);
                    foreach (Vector3 at in ForestCullingTests.WhereDrawn(passes.Conifers)) Assert.DoesNotContain(at, stand);
                }
        }

        //A pass takes no more of a variant's spruces than StandSpruces.MostTaken, which the arrays it fills are sized by (and
        //an overrun would throw mid-frame): every lens on a grid over the ring and past it, each variant's spruces within the
        //swap counted against the bound, which is well under the ring's whole count
        [Fact]
        public void No_lens_is_within_the_swap_of_more_spruces_than_the_passes_have_room_for()
        {
            var config = new AuroraSceneConfig();
            ForestPasses passes = ForestCullingTests.AuroraPasses(out ForestScatter scatter, () => true);
            StandSpruces stand = scatter.Stand;
            //Out to the widest radius the stand is sized for (Ultra's, #1208), from which a pass may take the most
            Assert.Equal(StandSpruces.FAR_DETAIL_SWAP_RADIUS, stand.Radius);
            float reach = config.FarStand.NearRadius + stand.Radius;
            var most = new int[stand.Feet.Length];
            int busiest = -1;
            Vector3 busiestLens = Vector3.Zero;
            for (float x = -reach; x <= reach; x += 12.5f)
                for (float z = -reach; z <= reach; z += 12.5f)
                {
                    var lens = new Vector3(x, 0f, z);
                    int all = 0;
                    for (int v = 0; v < stand.Feet.Length; v++)
                    {
                        int within = 0;
                        foreach (Vector2 foot in stand.Feet[v]) if (StandSpruces.Swaps(foot.X, foot.Y, lens, stand.Radius)) within++;
                        Assert.True(within <= stand.MostTaken[v], $"from {lens}, {within} spruces of variant {v} are within the swap, room for {stand.MostTaken[v]}");
                        most[v] = Math.Max(most[v], within);
                        all += within;
                    }
                    if (all > busiest) (busiest, busiestLens) = (all, lens);
                }

            int ring = 0, room = 0, needed = 0;
            for (int v = 0; v < stand.Feet.Length; v++)
            {
                ring += stand.Feet[v].Length;
                room += stand.MostTaken[v];
                needed += most[v];
            }
            Assert.True(room < ring / 2, $"room for {room} of the ring's {ring} spruces: the bound buys little");
            Assert.True(room < needed * 1.6f, $"room for {room} where the most any lens on the grid took was {needed}: the bound is loose");

            //And the passes from the busiest lens, each through a lens of nearly half a turn, all fit (an overrun throws)
            var spruces = new HashSet<Vector3>();
            foreach (ModelInstance[] bucket in stand.Spruces)
                foreach (ModelInstance spruce in bucket) spruces.Add(spruce.World.Translation);
            var taken = new HashSet<Vector3>();
            foreach (Vector3 look in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitZ })
            {
                Matrix wide = Matrix.CreateLookAt(busiestLens, busiestLens + look, Vector3.Up) * Matrix.CreatePerspectiveFieldOfView(3f, 1f, 0.1f, 3000f);
                passes.CameraPass(wide, busiestLens, true);
                foreach (Vector3 at in ForestCullingTests.WhereDrawn(passes.Conifers)) if (spruces.Contains(at)) taken.Add(at);
            }
            Assert.True(taken.Count > busiest / 2, $"the wide passes from {busiestLens} took {taken.Count} of the {busiest} spruces within the swap");
        }

        [Fact]
        public void The_stand_s_vertex_program_leaves_out_exactly_the_spires_the_passes_swap()
        {
            //StandSpruces.Swaps is the CPU's side: a foot nearer the lens than the radius, horizontally, by the instance's own floats.
            //The shader's side keeps a spire at or past it and multiplies its size by that, from the same uniform. No test runs a
            //shader, so this side is a tripwire on the source's text: a rewording breaks it as surely as a change of rule, and
            //either way the two sides are to be read together again
            string source = File.ReadAllText(Path.Combine(RepoRoot(), "BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Aurora.fx"));
            Assert.Contains("float2 fromLens = instance.Place.xz - CameraPosition.xz;", source);
            Assert.Contains("float kept = step(StandSwapRadius * StandSwapRadius, dot(fromLens, fromLens));", source);
            Assert.Contains("* (instance.Place.w * onGround * kept)", source);

            Assert.True(StandSpruces.Swaps(10f, 0f, new Vector3(10f + StandSpruces.SWAP_RADIUS * 0.999f, 5f, 0f), StandSpruces.SWAP_RADIUS));
            Assert.False(StandSpruces.Swaps(10f, 0f, new Vector3(10f + StandSpruces.SWAP_RADIUS, 5f, 0f), StandSpruces.SWAP_RADIUS));
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Game.sln"))) dir = dir.Parent;
            return dir?.FullName ?? throw new DirectoryNotFoundException("Game.sln not found above the test's output");
        }
    }
}
