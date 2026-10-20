using BS3D.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The cavern's hall (#958) is geometry built on the CPU, so what can go wrong with it can be asked of it without a
    /// device: a vertex that is not a number (one NaN normal is a black blot the size of the bloom's kernel on the screen),
    /// a formation standing in the air the arena plays in, and a chapter-intro lens inside rock or a column.
    /// </summary>
    public class CavernChamberTests
    {
        private static readonly int[] Seeds = { 0, 1, 7, 42 };

        private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

        private static void AssertFinite(VertexPositionNormalTexture[] vertices, string what)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                Assert.True(Finite(vertices[i].Position), $"{what}: vertex {i} position {vertices[i].Position}");
                Assert.True(Finite(vertices[i].Normal), $"{what}: vertex {i} normal {vertices[i].Normal}");
                Assert.InRange(vertices[i].Normal.Length(), 0.99f, 1.01f);
            }
        }

        [Fact]
        public void Every_vertex_of_every_mesh_is_a_number_with_a_unit_normal()
        {
            foreach (int seed in new[] { 0, 3 })
            {
                var chamber = new CavernChamber(new CavernSceneConfig(), seed);
                foreach (CavernGeometry.Detail detail in new[] { CavernGeometry.Detail.Coarse, CavernGeometry.Detail.Potato })
                {
                    AssertFinite(CavernGeometry.BuildRock(chamber, detail).Vertices, $"rock, seed {seed}");
                    AssertFinite(CavernGeometry.BuildFormations(chamber, detail).Vertices, $"formations, seed {seed}");
                }
                foreach (CavernCrystalVertex v in CavernGeometry.BuildCrystals(chamber).Vertices)
                {
                    Assert.True(Finite(v.Position) && Finite(v.Normal), $"crystal vertex, seed {seed}");
                    Assert.InRange(v.Normal.Length(), 0.99f, 1.01f);
                }
            }
        }

        [Fact]
        public void Nothing_hangs_or_stands_in_the_air_the_arena_plays_in()
        {
            foreach (int seed in Seeds)
            {
                var chamber = new CavernChamber(new CavernSceneConfig(), seed);
                foreach (CavernHanging s in chamber.Stalactites)
                {
                    Vector3 tip = s.Root + s.Direction * s.Length;
                    float r = new Vector2(tip.X, tip.Z).Length();
                    Assert.False(r < CavernChamber.ARENA_AIR_RADIUS && tip.Y < CavernChamber.ARENA_AIR_TOP,
                        $"seed {seed}: a stalactite's tip at {tip} hangs in the arena's air");
                }
                foreach (CavernColumn c in chamber.Columns)
                    Assert.True(c.Axis.Length() - c.Girth * 2.7f > CavernChamber.ARENA_AIR_RADIUS, $"seed {seed}: a column at {c.Axis} stands in the arena's air");
                foreach (CavernCluster c in chamber.Clusters)
                    Assert.True(new Vector2(c.Base.X, c.Base.Z).Length() > CavernChamber.ARENA_AIR_RADIUS, $"seed {seed}: a cluster at {c.Base} grows in the arena's air");

                //And the arena's own air is open: the island, the funnel, the cluster and the plate stand in it
                foreach (Vector3 p in new[] { new Vector3(0f, -20f, 0f), new Vector3(30f, 0f, 0f), new Vector3(0f, 40f, -30f), new Vector3(-35f, 20f, 25f) })
                    Assert.True(chamber.Clearance(p) > 8f, $"seed {seed}: {p} is only {chamber.Clearance(p)} from rock");
            }
        }

        [Fact]
        public void The_prologue_never_puts_the_lens_inside_the_hall_or_its_formations()
        {
            foreach (int seed in Seeds)
            {
                var chamber = new CavernChamber(new CavernSceneConfig(), seed);
                for (int roll = 0; roll < 6; roll++)
                {
                    IntroShot[] shots = CavernIntroShots.Build(chamber, MathHelper.ToRadians(45f), new Random(seed * 31 + roll));
                    foreach (IntroShot shot in shots)
                    {
                        for (int k = 0; k <= 60; k++)
                        {
                            shot.Pose(k / 60f, out Vector3 position, out _);
                            //The underside's crane stands under the island, which the hall knows nothing of: it is clear of rock
                            Assert.True(chamber.Clearance(position) > 1.5f,
                                $"seed {seed}, roll {roll}: '{shot.Name}' at {position} is {chamber.Clearance(position):0.0} from the hall");
                        }
                    }
                }
            }
        }
    }
}
