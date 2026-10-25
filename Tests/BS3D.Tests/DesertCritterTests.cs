using System;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The desert's creatures (#966): every one's life a pure function of the clock, so it is held here without a device.
    /// They stay round the island where they live, stand on the sand, move smoothly within a cycle, and every one spends part
    /// of its cycle hidden and part of it coming up and going down.
    /// </summary>
    public class DesertCritterTests
    {
        private readonly ITestOutputHelper _output;

        public DesertCritterTests(ITestOutputHelper output) => _output = output;

        private static float Ground(float x, float z) => 0.3f * MathF.Sin(x * 0.05f) + 0.2f * MathF.Cos(z * 0.07f);

        [Fact]
        public void EachLivesRoundTheIslandOnTheSandAndMovesSmoothly()
        {
            const float STEP = 1f / 60f;
            for (int i = 0; i < DesertCritters.Count; i++)
            {
                bool wasUp = false, sawHidden = false, sawEmerging = false, sawBurrowing = false;
                Vector3 last = default;
                for (float t = 0f; t < 240f; t += STEP)
                {
                    if (!DesertCritters.TryPose(i, t, Ground, out DesertCritters.Pose pose))
                    {
                        sawHidden = true;
                        wasUp = false;
                        continue;
                    }
                    sawEmerging |= pose.Emerging > 0f;
                    sawBurrowing |= pose.Burrowing > 0f;

                    float r = new Vector2(pose.Position.X, pose.Position.Z).Length();
                    Assert.InRange(r, DesertCritters.RING_IN - 12f, DesertCritters.RING_OUT + 30f);
                    Assert.Equal(Ground(pose.Position.X, pose.Position.Z), pose.Position.Y, 4);
                    if (wasUp)
                        Assert.True(Vector3.Distance(pose.Position, last) < 0.1f, $"creature {i} jumped {Vector3.Distance(pose.Position, last):0.000} at {t:0.00} s");
                    last = pose.Position;
                    wasUp = true;
                }
                Assert.True(sawHidden && sawEmerging && sawBurrowing, $"creature {i}: hidden {sawHidden}, emerging {sawEmerging}, burrowing {sawBurrowing}");
            }
        }

        /// <summary>Not a check: where a beetle has walked furthest within its first walk after 6 s, for a capture's camera.</summary>
        [Fact]
        public void PrintsAWalkingBeetle()
        {
            for (int i = 0; i < 4; i++)
                for (float t = 6f; t < 60f; t += 0.05f)
                    if (DesertCritters.TryPose(i, t, (x, z) => 0f, out DesertCritters.Pose pose) && pose.Walked > 2.5f)
                    {
                        _output.WriteLine($"beetle {i} walked {pose.Walked:0.0} at {t:0.00} s at ({pose.Position.X:0.00}, {pose.Position.Z:0.00}) yaw {pose.Yaw:0.00}");
                        break;
                    }
        }

        /// <summary>Not a check: where and when the first scorpion comes up after 6 s, for a capture's camera.</summary>
        [Fact]
        public void PrintsAScorpionsEmergence()
        {
            for (int i = 0; i < DesertCritters.Count; i++)
            {
                if (!DesertCritters.IsScorpion(i)) continue;
                for (float t = 6f; t < 60f; t += 0.05f)
                    if (DesertCritters.TryPose(i, t, (x, z) => 0f, out DesertCritters.Pose pose) && pose.Emerging > 0.5f)
                    {
                        _output.WriteLine($"scorpion {i} half up at {t:0.00} s at ({pose.Position.X:0.00}, {pose.Position.Z:0.00}) yaw {pose.Yaw:0.00}");
                        break;
                    }
            }
        }
    }
}
