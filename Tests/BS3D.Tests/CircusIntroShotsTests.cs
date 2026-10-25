using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The big top's prologue (#752) keeps every lens inside the tent: under the canvas, over the floor and the seats' rake,
    /// off the island, and the gate's push inside the entrance gap. The tent has no CPU geometry to test a lens against but
    /// its figures, so these are the figures.
    /// </summary>
    public class CircusIntroShotsTests
    {
        [Fact]
        public void The_big_top_prologue_keeps_its_lenses_inside_the_tent()
        {
            var circus = new CircusSceneConfig();
            CircusTentConfig tent = circus.Tent;
            float roofB = (tent.CrownRadius - tent.WallRadius) / (tent.CrownY - tent.WallTopY);
            float roofA = tent.WallRadius - roofB * tent.WallTopY;
            float entrance = MathHelper.ToRadians(circus.Seating.EntranceBearingDegrees);

            for (int roll = 0; roll < 24; roll++)
            {
                IntroShot[] shots = CircusIntroShots.Build(circus, MathHelper.ToRadians(45f), new Random(roll));
                Assert.Equal(3, shots.Length);
                foreach (IntroShot shot in shots)
                    for (int k = 0; k <= 40; k++)
                    {
                        shot.Pose(k / 40f, out Vector3 p, out _);
                        float r = new Vector2(p.X, p.Z).Length();
                        string at = $"roll {roll}: '{shot.Name}' at {p}";

                        Assert.True(r < tent.WallRadius - 1f, at + " is outside the wall");
                        Assert.True(r > ArenaIsland.RADIUS + 2f, at + " is inside the island");
                        //Under the canvas: the roof's radius at this height is wider than the lens's
                        Assert.True(roofA + roofB * p.Y > r + 2f, at + " is through the canvas");
                        Assert.True(p.Y > circus.Ring.FloorY + 1f, at + " is under the floor");
                        if (r > circus.Seating.InnerRadius && MathF.Abs(MathHelper.WrapAngle(MathF.Atan2(p.Z, p.X) - entrance)) > 0.3f)
                            Assert.True(p.Y > CircusIntroShots.SeatY(circus, r) + 0.8f, at + " is in the seats' rake");
                        if (shot.Name == "the gate")
                            Assert.True(MathF.Abs(MathHelper.WrapAngle(MathF.Atan2(p.Z, p.X) - entrance)) * r < circus.Seating.EntranceWidth * 0.5f,
                                at + " is outside the entrance gap");
                    }
            }
        }
    }
}
