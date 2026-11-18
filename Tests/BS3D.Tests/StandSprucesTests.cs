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
    /// drawn as its spruce, in the crown its distance gives it, none further and none into the Moon's map; each spruce stands on its spire's foot as tall
    /// as the spire; Low and Medium, the dial and the play camera swap none; and <c>Aurora.fx</c>'s <c>StandVS</c> leaves out
    /// exactly the spires the passes swap.
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

        //The sun's map: a box round the lens seen from the aurora's low Moon, as SunShadowMap lays it
        private static Matrix Moon(Vector3 lens) =>
            Matrix.CreateLookAt(lens + new Vector3(-280f, 95f, 100f), lens, Vector3.Up) * Matrix.CreateOrthographic(300f, 300f, 1f, 1200f);

        private static readonly BoundingSphere TREE = new(new Vector3(0f, 15f, 0f), 18f);

        [Theory]
        [MemberData(nameof(Lenses))]
        public void Every_spire_within_the_swap_that_a_pass_meets_is_drawn_as_its_spruce_and_none_beyond(Vector3 lens, Vector3 target)
        {
            ForestPasses passes = ForestCullingTests.AuroraPasses(out ForestScatter scatter);
            StandSpruces stand = scatter.Stand;
            Assert.True(stand.Reaches(lens));

            //The Moon's pass first, as SceneRenderer.DrawShadowMaps runs it: the stand's spruces cast nothing, as the spires never did
            Matrix moon = Moon(lens);
            passes.Pass(moon, lens, true, false);
            HashSet<Vector3> cast = ForestCullingTests.WhereDrawn(passes.Conifers);
            foreach (ModelInstance[] bucket in stand.Spruces)
                foreach (ModelInstance spruce in bucket) Assert.DoesNotContain(spruce.World.Translation, cast);

            Matrix camera = ForestCullingTests.ViewProjectionFrom(lens, target);
            passes.Pass(camera, lens, true, true);
            HashSet<Vector3> drawn = ForestCullingTests.WhereDrawn(passes.Conifers);
            HashSet<Vector3> near = ForestCullingTests.WhereDrawn(passes.NearConifers);
            var frustum = new BoundingFrustum(camera);

            int swapped = 0;
            foreach (ModelInstance[] bucket in stand.Spruces)
                foreach (ModelInstance spruce in bucket)
                {
                    Vector3 at = spruce.World.Translation;
                    float fromLens = new Vector2(at.X - lens.X, at.Z - lens.Z).Length();
                    bool due = fromLens < StandSpruces.SWAP_RADIUS && ForestScatterRenderer.Meets(frustum, TREE, spruce.World);
                    Assert.True(due == drawn.Contains(at), $"a spire {fromLens:F1} from the lens is {(due ? "not drawn as its spruce" : "drawn as a spruce")}");
                    if (!due) continue;
                    swapped++;
                    Assert.Equal(fromLens < ForestScatterRenderer.NEAR_LENS_RADIUS, near.Contains(at));
                }
            Assert.True(swapped > 0, "no spire was swapped from a lens beside the stand");
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
            int total = 0;
            for (int i = 0; i < spires.Length; i++)
            {
                Vector4 place = spires[i].Place, look = spires[i].Look;
                int v = i % variants;
                Vector2 foot = stand.Feet[v][next[v]];
                Matrix world = stand.Spruces[v][next[v]++].World;
                total++;

                //The very floats the shader's swap test reads, where the passes test them and where the spruce stands
                Assert.Equal(place.X, foot.X);
                Assert.Equal(place.Z, foot.Y);
                Assert.Equal(place.X, world.M41);
                Assert.Equal(place.Z, world.M43);

                float scale = new Vector3(world.M11, world.M12, world.M13).Length();
                float spruceTall = StandSpruces.SpruceHeight(config.Terrain.Trees, ForestScatterRenderer.CONIFER_VARIANTS[v]) * scale;
                float spireTall = config.FarStand.Height * place.W * look.Z;
                Assert.InRange(spruceTall, spireTall * 0.999f, spireTall * 1.001f);

                //Planted as a planted spruce is: a little under the snow the spire stood on, never floating
                float ground = TerrainMirror.AuroraBase(place.X, place.Z, config);
                Assert.InRange(world.M42, ground - 0.4f * scale, ground - 0.1f * scale);
            }
            Assert.Equal(spires.Length, total);
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

            //Low and Medium draw the lathe wood and the spires whole
            passes.Pass(camera, lens, false, true);
            foreach (Vector3 at in ForestCullingTests.WhereDrawn(passes.Conifers)) Assert.DoesNotContain(at, stand);

            //The dial out (aurora=all/-standspruces): the spires stay spires on High and Ultra too
            scatter.Stand.Enabled = false;
            passes.Pass(camera, lens, true, true);
            foreach (Vector3 at in ForestCullingTests.WhereDrawn(passes.Conifers)) Assert.DoesNotContain(at, stand);
            scatter.Stand.Enabled = true;

            //The play camera, walked round the island at the stand-offs a level is played from, sees no spire within the swap
            foreach (float standOff in new[] { 30f, 55f })
                for (int step = 0; step < 72; step++)
                {
                    float bearing = step * MathHelper.TwoPi / 72f;
                    var play = new Vector3(MathF.Sin(bearing) * standOff, -4f, MathF.Cos(bearing) * standOff);
                    passes.Pass(ForestCullingTests.ViewProjectionFrom(play, new Vector3(0f, -2f, 0f)), play, true, true);
                    foreach (Vector3 at in ForestCullingTests.WhereDrawn(passes.Conifers)) Assert.DoesNotContain(at, stand);
                }
        }

        [Fact]
        public void The_stand_s_vertex_program_leaves_out_exactly_the_spires_the_passes_swap()
        {
            //StandSpruces.Swaps is the CPU's side: a foot nearer the lens than the radius, horizontally, by the instance's own floats.
            //The shader's side keeps a spire at or past it and multiplies its size by that, from the same uniform
            string source = File.ReadAllText(Path.Combine(RepoRoot(), "BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Aurora.fx"));
            Assert.Contains("float2 fromLens = instance.Place.xz - CameraPosition.xz;", source);
            Assert.Contains("float kept = step(StandSwapRadius * StandSwapRadius, dot(fromLens, fromLens));", source);
            Assert.Contains("* (instance.Place.w * onGround * kept)", source);

            Assert.True(StandSpruces.Swaps(10f, 0f, new Vector3(10f + StandSpruces.SWAP_RADIUS * 0.999f, 5f, 0f)));
            Assert.False(StandSpruces.Swaps(10f, 0f, new Vector3(10f + StandSpruces.SWAP_RADIUS, 5f, 0f)));
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Game.sln"))) dir = dir.Parent;
            return dir?.FullName ?? throw new DirectoryNotFoundException("Game.sln not found above the test's output");
        }
    }
}
