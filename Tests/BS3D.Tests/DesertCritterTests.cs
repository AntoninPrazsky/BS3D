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

        //Where the intro's featured beetle is cued in these tests, along which heading and when
        private static readonly Vector2 Cue = new(40f, 10f);
        private const float CUE_YAW = 0.7f, CUE_ONSET = 3f;

        //Steps further back than any trail keeps
        private const int STEPS_ASKED = 32;

        //A beetle the scene draws a trail for, at a moment: -1 the intro's featured one, else a roaming one
        private static bool BeetlePose(int i, float t, out DesertCritters.Pose pose) => i < 0
            ? DesertCritters.TryFeaturePose(Cue, CUE_YAW, CUE_ONSET, t, Ground, out pose, scorpion: false)
            : DesertCritters.TryPose(i, t, Ground, out pose);

        private static IEnumerable<int> Beetles()
        {
            for (int i = -1; i < DesertCritters.Count; i++)
                if (i < 0 || !DesertCritters.IsScorpion(i)) yield return i;
        }

        //Where a beetle's foot stands on the sand in plan as Critter.fx draws it: its place at rest (CritterMesh.BeetleFoot)
        //swung forwards by the gait (`forward`, in strides of `stride` body lengths) and placed by the body's matrix
        private static Vector2 FootOnSand(in DesertCritters.Pose pose, int foot, Func<float, float> forward, float stride)
        {
            int leg = foot >> 1, side = (foot & 1) == 0 ? -1 : 1;
            float phase = pose.Gait + CritterMesh.LegPhase(leg, side);
            Vector3 local = CritterMesh.BeetleFoot(leg, side) + new Vector3(0f, 0f, stride * forward(phase));
            Vector3 world = Vector3.Transform(local, DesertCritters.World(pose, scorpion: false));
            return new Vector2(world.X, world.Z);
        }

        private static Vector2 FootOnSand(in DesertCritters.Pose pose, int foot) =>
            FootOnSand(pose, foot, CritterGait.Forward, CritterGait.Stride(scorpion: false));

        private static float FootPhase(in DesertCritters.Pose pose, int foot) =>
            pose.Gait + CritterMesh.LegPhase(foot >> 1, (foot & 1) == 0 ? -1 : 1);

        /// <summary>
        /// A beetle's prints are laid, not dragged (#1186): from one frame to the next every print is where a print already lay,
        /// fading no faster than the beetle walks, except a foot's newest, just laid at nearly full strength; and a print that
        /// goes has faded to almost nothing first. Every roaming beetle and the intro's featured one, over their cycles.
        /// </summary>
        [Fact]
        public void A_beetles_prints_stay_where_they_were_laid()
        {
            const float STEP = 1f / 60f;
            int held = 0, laid = 0, dropped = 0;
            float closest = float.MaxValue, closestStraight = float.MaxValue;
            var last = new List<(Vector2 At, float Fade)>();
            var now = new List<(Vector2 At, float Fade, int Step)>();
            foreach (int i in Beetles())
            {
                last.Clear();
                Vector2 start = new(float.NaN);
                for (float t = 0f; t < 120f; t += STEP)
                {
                    bool up = BeetlePose(i, t, out DesertCritters.Pose pose);
                    if (!up || pose.Start != start) { last.Clear(); start = up ? pose.Start : new(float.NaN); }
                    if (!up) continue;

                    now.Clear();
                    for (int step = 0; step < STEPS_ASKED; step++)
                        for (int foot = 0; foot < CritterMesh.BEETLE_FEET; foot++)
                            if (DesertCritters.TrailPrint(pose, foot, step, out Vector2 at, out float fade))
                            {
                                Assert.InRange(fade, 0f, 1f);
                                now.Add((at, fade, step));
                            }
                    for (int a = 0; a < now.Count; a++)
                        for (int b = a + 1; b < now.Count; b++)
                        {
                            float apart = Vector2.Distance(now[a].At, now[b].At);
                            closest = MathF.Min(closest, apart);
                            if (i < 0) closestStraight = MathF.Min(closestStraight, apart);
                        }
                    if (last.Count > 0)
                    {
                        foreach (var print in now)
                        {
                            int was = last.FindIndex(p => Vector2.Distance(p.At, print.At) < 1e-4f);
                            if (was >= 0)
                            {
                                Assert.True(print.Fade <= last[was].Fade + 1e-5f, $"beetle {i}: a print grew darker at {t:0.00} s");
                                held++;
                            }
                            else
                            {
                                Assert.True(print.Step == 0 && print.Fade > 0.9f,
                                    $"beetle {i}: a print {print.Step} steps back at {t:0.00} s lies where no print lay a frame before");
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
                    last.Clear();
                    foreach (var print in now) last.Add((print.At, print.Fade));
                }
            }
            _output.WriteLine(FormattableString.Invariant(
                $"prints held {held}, laid {laid}, dropped {dropped}; the closest two {closestStraight / DesertCritters.BEETLE_SIZE:F3} of a length apart on a straight walk, {closest / DesertCritters.BEETLE_SIZE:F3} on a curve"));
            Assert.True(held > 1000 && laid > 50 && dropped > 20, $"too little walking to judge: held {held}, laid {laid}, dropped {dropped}");
        }

        /// <summary>
        /// A print is where a foot stepped (#1217): at the instant a foot's print appears, the foot stands on it, the foot
        /// where Critter.fx draws it (its place at rest in <see cref="CritterMesh.BeetleFoot"/>, swung by
        /// <see cref="CritterGait"/>'s gait, placed by <see cref="DesertCritters.World"/>) within a thousandth of a unit in
        /// plan; and each foot lays one print a step. The instant is found by halving the frame the print appeared in. Every
        /// roaming beetle and the intro's featured one, over their cycles. The zig-zag #1186 laid by arithmetic on the body's
        /// path fails it: every print appeared at least 0.18 units from the nearest foot, and under feet in the air.
        /// </summary>
        [Fact]
        public void Each_print_lies_under_its_foot_as_it_touches_down()
        {
            const float STEP = 1f / 60f;
            int prints = 0, touchdowns = 0;
            float worst = 0f;
            var previous = new Vector2[CritterMesh.BEETLE_FEET];
            var had = new bool[CritterMesh.BEETLE_FEET];
            var wasPlanted = new bool[CritterMesh.BEETLE_FEET];
            foreach (int i in Beetles())
            {
                Vector2 start = new(float.NaN);
                bool wasUp = false;
                float last = 0f;
                for (float t = 0f; t < 120f; t += STEP)
                {
                    bool up = BeetlePose(i, t, out DesertCritters.Pose pose);
                    if (!up || pose.Start != start)
                    {
                        wasUp = false;
                        Array.Clear(had);
                        start = up ? pose.Start : new(float.NaN);
                    }
                    if (!up) continue;

                    for (int foot = 0; foot < CritterMesh.BEETLE_FEET; foot++)
                    {
                        //Its touchdowns, counted off the gait alone: down this frame and in the air the one before
                        bool planted = CritterGait.Planted(FootPhase(pose, foot));
                        if (planted && !wasPlanted[foot] && wasUp) touchdowns++;
                        wasPlanted[foot] = planted;

                        if (!DesertCritters.TrailPrint(pose, foot, 0, out Vector2 at, out _)) { had[foot] = false; continue; }
                        bool laid = !had[foot] || Vector2.Distance(at, previous[foot]) > 1e-4f;
                        if (laid && wasUp)
                        {
                            //The instant it appeared: the first moment of the frame its newest print is this one
                            float lo = last, hi = t;
                            for (int n = 0; n < 40; n++)
                            {
                                float mid = 0.5f * (lo + hi);
                                bool there = BeetlePose(i, mid, out DesertCritters.Pose p) && p.Start == start
                                    && DesertCritters.TrailPrint(p, foot, 0, out Vector2 m, out _) && Vector2.Distance(m, at) < 1e-4f;
                                if (there) hi = mid; else lo = mid;
                            }
                            Assert.True(BeetlePose(i, hi, out DesertCritters.Pose touch));
                            float off = Vector2.Distance(FootOnSand(touch, foot), at);
                            Assert.True(off < 1e-3f, FormattableString.Invariant(
                                $"beetle {i}: foot {foot}'s print laid at {hi:F4} s lies {off:F4} units off the foot"));
                            //And the foot down on it (at the very instant, a float either side of the touchdown)
                            Assert.True(CritterGait.Lift(FootPhase(touch, foot)) < 1e-3f, $"beetle {i}: foot {foot}'s print laid at {hi:F4} s under a foot in the air");
                            worst = MathF.Max(worst, off);
                            prints++;
                        }
                        previous[foot] = at;
                        had[foot] = true;
                    }
                    wasUp = true;
                    last = t;
                }
            }
            _output.WriteLine(FormattableString.Invariant(
                $"{prints} prints laid against {touchdowns} touchdowns, each at most {worst:F6} units off its foot as it touched down"));
            Assert.True(prints > 300, $"only {prints} prints laid");
            Assert.Equal(touchdowns, prints);
        }

        /// <summary>
        /// A foot that stands on the sand stays where it touched down until it lifts (#1217), so it stands on its print the
        /// whole stance: the stance sweeps the foot back as far as the body walks over it (<see cref="CritterGait"/>). Within a
        /// thousandth of a unit for the intro's featured beetle, which walks straight; the roaming ones walk curves, and a
        /// foot turns a little with the body over it, within <see cref="CURVE_DRIFT"/>. The sine stance the shader once swept
        /// with (0.07 body lengths either way, against the 0.23 the body walks over a stance) skates the featured beetle's
        /// foot 0.048 units over the sand.
        /// </summary>
        [Fact]
        public void A_planted_foot_stays_on_its_print_until_it_lifts()
        {
            float straight = 0f, curved = 0f;
            foreach (int i in Beetles())
            {
                float worst = WorstStance(i, CritterGait.Forward, CritterGait.Planted, CritterGait.Stride(scorpion: false), onPrint: true);
                if (i < 0) straight = worst; else curved = MathF.Max(curved, worst);
            }
            float sine = WorstStance(-1, MathF.Sin, phase => MathF.Cos(phase) < 0f, 0.07f, onPrint: false);
            _output.WriteLine(FormattableString.Invariant(
                $"a planted foot off its print: {straight:F5} units walking straight, {curved:F4} on the roaming beetles' curves; the old sine stance skated {sine:F4}"));

            Assert.True(straight < 1e-3f, $"the featured beetle's planted foot moves {straight:F5} units off its print");
            Assert.True(curved < CURVE_DRIFT, $"a roaming beetle's planted foot moves {curved:F4} units off its print");
            //Seen to fire (BestPractices.md, section 10): the old gait's skating is caught by the same measure
            Assert.True(sine > 0.03f, $"the old sine stance skated only {sine:F4}");
        }

        //How far a roaming beetle's planted foot may turn off its print as the body turns over it on its curve (TURN), units
        private const float CURVE_DRIFT = 0.035f;

        //The furthest beetle `i`'s planted feet get from where each touched down (or, `onPrint`, from its newest print) over
        //its cycles, its feet swung by `forward` and down while `planted`
        private static float WorstStance(int i, Func<float, float> forward, Func<float, bool> planted, float stride, bool onPrint)
        {
            const float STEP = 1f / 120f;
            float worst = 0f;
            var anchor = new Vector2[CritterMesh.BEETLE_FEET];
            var down = new bool[CritterMesh.BEETLE_FEET];
            Vector2 start = new(float.NaN);
            int stances = 0;
            for (float t = 0f; t < 120f; t += STEP)
            {
                bool up = BeetlePose(i, t, out DesertCritters.Pose pose);
                if (!up || pose.Start != start) { Array.Clear(down); start = up ? pose.Start : new(float.NaN); }
                //Walking: coming up and digging in, the body tips over its feet and sinks, and they with it
                if (!up || pose.Walked <= 0f || pose.Burrowing > 0f) continue;
                for (int foot = 0; foot < CritterMesh.BEETLE_FEET; foot++)
                {
                    if (!planted(FootPhase(pose, foot))) { down[foot] = false; continue; }
                    Vector2 at = FootOnSand(pose, foot, forward, stride);
                    if (onPrint)
                    {
                        //Before its first touchdown of a walk a foot stands where it stood as the beetle came up: no print
                        if (DesertCritters.TrailPrint(pose, foot, 0, out Vector2 print, out _) && down[foot])
                            worst = MathF.Max(worst, Vector2.Distance(at, print));
                    }
                    else if (down[foot]) worst = MathF.Max(worst, Vector2.Distance(at, anchor[foot]));
                    else { anchor[foot] = at; stances++; }
                    down[foot] = true;
                }
            }
            Assert.True(onPrint || stances > 20, $"beetle {i}: only {stances} stances");
            return worst;
        }

        /// <summary>Not a check: where a beetle has walked furthest within its first walk after 6 s, on the desert's sand,
        /// and a Testbed camera close on its feet from its left side (<c>clock=</c> that second, <c>clockstill</c>).</summary>
        [Fact]
        public void PrintsAWalkingBeetle()
        {
            var desert = new DesertSceneConfig();
            float Sand(float x, float z) => TerrainMirror.Desert(x, z, desert);
            for (int i = 0; i < 4; i++)
                for (float t = 6f; t < 60f; t += 0.05f)
                    if (DesertCritters.TryPose(i, t, Sand, out DesertCritters.Pose pose) && pose.Walked > 2.5f && pose.Burrowing == 0f)
                    {
                        var ahead = new Vector3(MathF.Sin(pose.Yaw), 0f, MathF.Cos(pose.Yaw));
                        var right = new Vector3(-ahead.Z, 0f, ahead.X);
                        Vector3 lens = pose.Position - right * 1.6f - ahead * 0.5f + Vector3.Up * 0.9f;
                        Vector3 look = pose.Position - ahead * 0.35f;
                        _output.WriteLine(FormattableString.Invariant(
                            $"beetle {i} walked {pose.Walked:0.0} at {t:0.00} s at ({pose.Position.X:0.00}, {pose.Position.Y:0.00}, {pose.Position.Z:0.00}) yaw {pose.Yaw:0.00} turn {pose.Turn:0.000}: clock={t:0.00} clockstill campos={lens.X:0.00},{lens.Y:0.00},{lens.Z:0.00} camtarget={look.X:0.00},{look.Y:0.00},{look.Z:0.00}"));
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
