using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The cavern's crystals grow out of the ground (#1017). A cluster is one base seated in the rock and six to eleven prisms
    /// standing up to a few units from it, and the prisms used to be placed on a plane through that one point: rubble, an
    /// island's dome and a ledge's curve put the ground a unit or two under them, and their feet hung in the air (the owner:
    /// "the crystals grow out of the air"). So every prism's foot is measured against the rock <b>as the draw builds it</b>
    /// (<see cref="CavernRockProbe"/>), not against the functions the mesh is sampled from.
    /// </summary>
    public class CavernCrystalSeatTests
    {
        //The shipped hall (0); halls with a cluster on a steep wall or far down a passage (1, 11), and one whose beach stays under
        //the water along the wall (42), which is where a cluster is moved to dry ground
        private static readonly int[] Seeds = { 0, 1, 3, 7, 11, 42 };

        /// <summary>How far the rock's built surface stands from one prism's foot, and where the prism comes out of it.</summary>
        internal readonly struct Seat
        {
            /// <summary>The foot's centre and its six corners at the prism's own turn: the highest of them over the surface
            /// (positive: in the air; negative: inside the rock).</summary>
            public readonly float HighestFoot;
            /// <summary>The foot's centre below the surface (positive: buried this deep).</summary>
            public readonly float Buried;
            /// <summary>Where the prism's axis leaves the rock: its height, and how far along the prism that is.</summary>
            public readonly float ExitY, ExitAlong;

            public Seat(float highestFoot, float buried, float exitY, float exitAlong)
            {
                HighestFoot = highestFoot;
                Buried = buried;
                ExitY = exitY;
                ExitAlong = exitAlong;
            }
        }

        internal static Seat Measure(CavernRockProbe probe, CavernRockProbe.Local local, CavernPrism prism)
        {
            CavernGeometry.PrismFrame(prism.Axis, prism.Turn, out Vector3 side, out Vector3 other);
            float centre = probe.SignedDistance(prism.Base, local, out _);
            float highest = centre;
            for (int i = 0; i < 6; i++)
            {
                float angle = i * MathHelper.Pi / 3f;
                Vector3 corner = prism.Base + (side * MathF.Cos(angle) + other * MathF.Sin(angle)) * prism.Radius;
                highest = MathF.Max(highest, probe.SignedDistance(corner, local, out _));
            }

            //Along the axis from the foot to the first point in the air
            float along = 0f;
            Vector3 exit = prism.Base;
            while (along < prism.Length && probe.SignedDistance(exit, local, out _) <= 0f)
            {
                along += 0.1f;
                exit = prism.Base + prism.Axis * along;
            }
            return new Seat(highest, -centre, exit.Y, along);
        }

        private static void AssertSeated(CavernGeometry.Detail detail, string name, params int[] seeds)
        {
            foreach (int seed in seeds)
            {
                var chamber = new CavernChamber(new CavernSceneConfig(), seed);
                var probe = new CavernRockProbe(chamber, detail);
                for (int c = 0; c < chamber.Clusters.Count; c++)
                {
                    CavernCluster cluster = chamber.Clusters[c];
                    CavernRockProbe.Local local = probe.Near(cluster.Base, 45f);
                    for (int k = 0; k < cluster.Prisms.Count; k++)
                    {
                        CavernPrism prism = cluster.Prisms[k];
                        Seat seat = Measure(probe, local, prism);
                        string at = $"{name}, seed {seed}, cluster {c}, prism {k} (foot {prism.Base})";

                        //Rising out of the surface, the foot buried: every corner of it inside the rock, not a gap under one edge
                        Assert.True(seat.HighestFoot <= 0f, $"{at}: its foot stands {seat.HighestFoot:0.00} over the rock, in the air");
                        //Out of rock above the lake, never standing in or on the water
                        Assert.True(seat.ExitY >= chamber.WaterY, $"{at}: comes out of the rock at {seat.ExitY:0.00}, under the water at {chamber.WaterY}");
                    }
                }
            }
        }

        [Fact]
        public void Every_prism_of_every_cluster_is_seated_in_the_rock_the_draw_builds()
        {
            AssertSeated(CavernGeometry.Detail.Fine, "fine", Seeds);
        }

        [Fact]
        public void The_coarse_mesh_seats_them_too()
        {
            //Low and Medium's mesh: cells twice the size, a crest of the bed or a crag of the wall standing over its chords
            AssertSeated(CavernGeometry.Detail.Coarse, "coarse", 0, 11, 42);
        }
    }
}
