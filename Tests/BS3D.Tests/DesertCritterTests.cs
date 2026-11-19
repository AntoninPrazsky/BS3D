using System;
using System.Collections.Generic;
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

        /// <summary>
        /// The featured beetle and scorpion the intro's close shots cue (#966, #1110): each on its own timeline from its onset,
        /// hidden before it and gone after, up out of the sand where it was cued, walking straight on along its heading on the
        /// sand without a jump, and down where its walk ended; its walk a few of its lengths, so a shot holding it through its
        /// walk sees it go somewhere.
        /// </summary>
        [Fact]
        public void A_featured_creature_comes_up_where_it_was_cued_walks_straight_on_and_digs_in_where_it_stops()
        {
            foreach (bool scorpion in new[] { false, true })
            {
                var at = new Vector2(40f, 10f);
                const float YAW = 0.7f, ONSET = 3f;
                float walk = DesertCritters.FeatureWalkSeconds(scorpion);
                float life = DesertCritters.EMERGE_SECONDS + walk + DesertCritters.BURROW_SECONDS;
                var heading = new Vector2(MathF.Sin(YAW), MathF.Cos(YAW));
                string what = scorpion ? "the scorpion" : "the beetle";

                Assert.False(DesertCritters.TryFeaturePose(at, YAW, ONSET, ONSET - 0.02f, Ground, out _, scorpion), $"{what} before its onset");
                Assert.False(DesertCritters.TryFeaturePose(at, YAW, ONSET, ONSET + life + 0.02f, Ground, out _, scorpion), $"{what} after its life");
                Assert.False(DesertCritters.TryFeaturePose(at, YAW, float.NaN, ONSET + 1f, Ground, out _, scorpion), $"{what} never cued");

                Vector3 last = default;
                float walked = 0f;
                for (float t = ONSET; t <= ONSET + life; t += 1f / 60f)
                {
                    Assert.True(DesertCritters.TryFeaturePose(at, YAW, ONSET, t, Ground, out DesertCritters.Pose pose, scorpion), $"{what} at {t - ONSET:F2} s");
                    Vector2 d = new Vector2(pose.Position.X, pose.Position.Z) - at;
                    Assert.True(MathF.Abs(d.X * heading.Y - d.Y * heading.X) < 1e-3f, $"{what} off its line at {t - ONSET:F2} s");
                    Assert.Equal(Ground(pose.Position.X, pose.Position.Z), pose.Position.Y, 4);
                    if (t > ONSET) Assert.True(Vector3.Distance(pose.Position, last) < 0.05f, $"{what} jumped at {t - ONSET:F2} s");

                    float local = t - ONSET;
                    if (local > 0.02f && local < DesertCritters.EMERGE_SECONDS - 0.05f) Assert.True(pose.Emerging > 0f && d.Length() < 1e-3f, $"{what} not coming up where it was cued");
                    if (local > DesertCritters.EMERGE_SECONDS + walk + 0.05f) Assert.True(pose.Burrowing > 0f, $"{what} not digging in after its walk");
                    walked = MathF.Max(walked, d.Length());
                    last = pose.Position;
                }

                float size = scorpion ? DesertCritters.SCORPION_SIZE : DesertCritters.BEETLE_SIZE;
                _output.WriteLine($"{what}: walks {walked:F2} units, {walked / size:F1} of its lengths, in {walk:F1} s");
                Assert.InRange(walked / size, 1.5f, 6f);
            }
        }

        /// <summary>
        /// A beetle's prints are laid, not dragged (#1186): from one frame to the next every print is where a print already lay,
        /// fading no faster than the beetle walks, except the newest, just laid behind it at nearly full strength; and a print
        /// that goes has faded to almost nothing first. Every roaming beetle and the intro's featured one, over their cycles.
        /// </summary>
        [Fact]
        public void A_beetles_prints_stay_where_they_were_laid()
        {
            const float STEP = 1f / 60f;
            var cue = new Vector2(40f, 10f);
            int held = 0, laid = 0, dropped = 0;
            var last = new List<(Vector2 At, float Fade)>();
            var now = new List<(Vector2 At, float Fade)>();
            for (int i = -1; i < DesertCritters.Count; i++)
            {
                if (i >= 0 && DesertCritters.IsScorpion(i)) continue;
                last.Clear();
                Vector2 start = new(float.NaN);
                for (float t = 0f; t < 120f; t += STEP)
                {
                    DesertCritters.Pose pose;
                    bool up = i < 0
                        ? DesertCritters.TryFeaturePose(cue, 0.7f, 3f, t, Ground, out pose, scorpion: false)
                        : DesertCritters.TryPose(i, t, Ground, out pose);
                    if (!up || pose.Start != start) { last.Clear(); start = up ? pose.Start : new(float.NaN); }
                    if (!up) continue;

                    now.Clear();
                    for (int k = 1; DesertCritters.TrailPrint(pose, DesertCritters.BEETLE_SIZE, k, out Vector2 at, out float fade); k++)
                    {
                        Assert.InRange(fade, 0f, 1f);
                        now.Add((at, fade));
                    }
                    if (last.Count > 0)
                    {
                        for (int k = 0; k < now.Count; k++)
                        {
                            int was = last.FindIndex(p => Vector2.Distance(p.At, now[k].At) < 1e-4f);
                            if (was >= 0)
                            {
                                Assert.True(now[k].Fade <= last[was].Fade + 1e-5f, $"beetle {i}: a print grew darker at {t:0.00} s");
                                held++;
                            }
                            else
                            {
                                Assert.True(k == 0 && now[k].Fade > 0.9f, $"beetle {i}: print {k + 1} at {t:0.00} s lies where no print lay a frame before");
                                laid++;
                            }
                        }
                        foreach (var p in last)
                            if (!now.Exists(q => Vector2.Distance(p.At, q.At) < 1e-4f))
                            {
                                Assert.True(p.Fade < 0.1f, $"beetle {i}: a print at {p.Fade:0.00} vanished at {t:0.00} s");
                                dropped++;
                            }
                    }
                    (last, now) = (now, last);
                }
            }
            _output.WriteLine($"prints held {held}, laid {laid}, dropped {dropped}");
            Assert.True(held > 1000 && laid > 50 && dropped > 20, $"too little walking to judge: held {held}, laid {laid}, dropped {dropped}");
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
