using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The big top's prologue (#752, #1118) keeps every lens inside the tent: under the canvas, over the floor and the seats'
    /// rake wherever it is not inside the entrance's gap, off the island, and the gate's push down the gap. The tent has no
    /// CPU geometry to test a lens against but its figures, so these are the figures. The gate home that ends the reel where
    /// a flight onto the pose is clear is held further, to the poles, the curb and the ride, by <see cref="CircusLandingTests"/>.
    /// </summary>
    public class CircusIntroShotsTests
    {
        //The level's own pose, where the reel lands (the gate home), and the menu's crane over the island, where no flight
        //home is laid and the reel ends on the gate
        private static readonly Vector3[] Poses = { CircusLandingTests.GamePosition, new(0f, -2f, 15f) };

        [Fact]
        public void The_big_top_prologue_keeps_its_lenses_inside_the_tent()
        {
            var circus = new CircusSceneConfig();
            CircusTentConfig tent = circus.Tent;
            float roofB = (tent.CrownRadius - tent.WallRadius) / (tent.CrownY - tent.WallTopY);
            float roofA = tent.WallRadius - roofB * tent.WallTopY;
            float entrance = MathHelper.ToRadians(circus.Seating.EntranceBearingDegrees);
            bool landed = false, gate = false;

            for (int roll = 0; roll < 24; roll++)
                foreach (Vector3 pose in Poses)
                {
                    IntroShot[] shots = CircusIntroShots.Build(circus, pose, CircusLandingTests.GameTarget, MathHelper.ToRadians(45f), new Random(roll));
                    Assert.Equal(3, shots.Length);
                    landed |= shots[^1].Name == CircusLanding.NAME;
                    gate |= shots[^1].Name == CircusIntroShots.GATE_NAME;

                    foreach (IntroShot shot in shots)
                        for (int k = 0; k <= 400; k++)
                        {
                            shot.Pose(k / 400f, out Vector3 p, out _);
                            float r = new Vector2(p.X, p.Z).Length();
                            float across = MathF.Abs(MathHelper.WrapAngle(MathF.Atan2(p.Z, p.X) - entrance)) * r;
                            string at = $"roll {roll}: '{shot.Name}' at {p}";

                            Assert.True(r < tent.WallRadius - 1f, at + " is outside the wall");
                            Assert.True(r > ArenaIsland.RADIUS + 2f, at + " is inside the island");
                            //Under the canvas: the roof's radius at this height is wider than the lens's
                            Assert.True(roofA + roofB * p.Y > r + 2f, at + " is through the canvas");
                            Assert.True(p.Y > circus.Ring.FloorY + 1f, at + " is under the floor");
                            //Over the rake, unless inside the entrance's gap (the 14-wide strip down its middle)
                            if (r > circus.Seating.InnerRadius && across >= circus.Seating.EntranceWidth * 0.5f)
                                Assert.True(p.Y > CircusIntroShots.SeatY(circus, r) + 0.8f, at + " is in the seats' rake");
                            if (shot.Name == CircusIntroShots.GATE_NAME)
                                Assert.True(across < circus.Seating.EntranceWidth * 0.5f, at + " is outside the entrance gap");
                        }
                }

            Assert.True(landed, "no reel landed on the level's own pose");
            Assert.True(gate, "no reel ended on the gate");
        }
    }
}
