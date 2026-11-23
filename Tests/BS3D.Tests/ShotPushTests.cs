using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;
using Xunit.Abstractions;
using XVector3 = Microsoft.Xna.Framework.Vector3;

namespace BS3D.Tests
{
    /// <summary>
    /// #1192: a shot pushes the cluster it hits. Every check here hangs a level TWICE, on one worker thread each so the two
    /// worlds step identically (<see cref="HungLevel"/>), fires the same shot into both through the Game's own contact
    /// handler (<see cref="ShotRig"/>), one with the push and one without, and reads what the push alone did as the
    /// difference between the twins: how far it moved the balls, how far it stretched the sockets, where the shot landed.
    /// </summary>
    public class ShotPushTests(ITestOutputHelper output)
    {
        /// <summary>One shot into a level and its twin: the push's own effect, step by step.</summary>
        internal sealed record Response(
            XZLevel? Landed, XZLevel? LandedWithout, ShotPush.Result Push,
            float[] MaxDisplacement, float[] MeanDisplacement, float PeakStretch, float PeakStretchWithout,
            float LowestDrop, int Balls)
        {
            /// <summary>The largest any ball stood from its twin, over the whole run.</summary>
            public float Peak
            {
                get
                {
                    float peak = 0f;
                    foreach (float d in MaxDisplacement) peak = MathF.Max(peak, d);
                    return peak;
                }
            }

            public int PeakStep => Array.IndexOf(MaxDisplacement, Peak);

            /// <summary>The largest any ball stood from its twin at <paramref name="seconds"/> after the landing.</summary>
            public float At(float seconds) => MaxDisplacement[Math.Min(MaxDisplacement.Length - 1,
                (int)MathF.Round(seconds / HungLevel.TIMESTEP))];
        }

        /// <summary>A push given by hand in place of the handler's, for the report's sweep of the shape's own figures.</summary>
        internal readonly record struct Manual(float Scale, int Reach, float MaxKick, float Falloff = ShotPush.HOP_FALLOFF);

        /// <summary>
        /// Fires one aim (<see cref="ShotAgreementTests.RandomAim"/> under <paramref name="seed"/>) into a level and its twin
        /// and follows both for <paramref name="seconds"/>. The twin takes no push; the other the handler's own at
        /// <paramref name="scale"/>, or <paramref name="manual"/>'s given by hand right after the landing when it is set
        /// (the handler gives its push at the very end of that step's contact work, so the two are the same moment).
        /// </summary>
        internal static Response Fire(string level, int seed, float seconds, float scale = 1f, Manual? manual = null,
            BallKind kind = BallKind.Normal)
        {
            using ShotRig pushed = new(level, workerThreads: 1);
            using ShotRig still = new(level, workerThreads: 1);

            pushed.Handler.PushScale = manual == null ? scale : 0f;
            still.Handler.PushScale = 0f;

            Random rngA = new(seed), rngB = new(seed);
            (XVector3 muzzle, XVector3 target, float glance) = ShotAgreementTests.RandomAim(pushed.Hung.Balls, rngA);
            ShotAgreementTests.RandomAim(still.Hung.Balls, rngB);

            ShotResult a = pushed.Fire(muzzle, target, glance, kind: kind);
            ShotResult b = still.Fire(muzzle, target, glance, kind: kind);

            ShotPush.Result push = pushed.Handler.LastPush;
            if (manual is Manual m && a.Landed is XZLevel cell)
            {
                XVector3 velocity = XVector3.Normalize(target - muzzle) * ShotRig.SHOOT_SPEED;
                push = new ShotPush().Apply(pushed.Hung.World.Simulation, pushed.Hung.Balls, cell,
                    ShotPush.ImpulseOf(BallsConstraintsBuilder.BALL_MASS, velocity.ToNumerics(), kind, m.Scale), m.Reach, m.MaxKick, m.Falloff);
            }

            int steps = (int)MathF.Round(seconds / HungLevel.TIMESTEP);
            float[] max = new float[steps + 1], mean = new float[steps + 1];
            float stretch = 0f, stretchWithout = 0f, lowestDrop = 0f;
            int balls = 0;

            for (int step = 0; step <= steps; step++)
            {
                if (step > 0)
                {
                    pushed.Hung.Run(HungLevel.TIMESTEP);
                    still.Hung.Run(HungLevel.TIMESTEP);
                }

                Compare(pushed.Hung.Balls, still.Hung.Balls, out max[step], out mean[step], out balls, out float lowestA, out float lowestB);
                stretch = MathF.Max(stretch, PeakStretch(pushed.Hung.Balls));
                stretchWithout = MathF.Max(stretchWithout, PeakStretch(still.Hung.Balls));
                lowestDrop = MathF.Max(lowestDrop, lowestB - lowestA);
            }

            return new Response(a.Landed, b.Landed, push, max, mean, stretch, stretchWithout, lowestDrop, balls);
        }

        /// <summary>How far each ball of one world stands from the same ball of its twin.</summary>
        private static void Compare(PhysicsBall[,,] a, PhysicsBall[,,] b, out float max, out float mean, out int count,
            out float lowestA, out float lowestB)
        {
            max = 0f;
            float sum = 0f;
            count = 0;
            lowestA = lowestB = float.MaxValue;

            for (int x = 0; x < a.GetLength(0); x++)
                for (int z = 0; z < a.GetLength(1); z++)
                    for (int l = 0; l < a.GetLength(2); l++)
                    {
                        PhysicsBall ballA = a[x, z, l], ballB = b[x, z, l];
                        if (ballA == null || ballB == null) continue;

                        XVector3 pa = ballA.BallReference.Pose.Position.ToXna(), pb = ballB.BallReference.Pose.Position.ToXna();
                        float d = (pa - pb).Length();
                        max = MathF.Max(max, d);
                        sum += d;
                        count++;
                        lowestA = MathF.Min(lowestA, pa.Y);
                        lowestB = MathF.Min(lowestB, pb.Y);
                    }

            mean = count > 0 ? sum / count : 0f;
        }

        /// <summary>
        /// The most any socket between two balls stands stretched past its length: every pair of lattice neighbours sits one
        /// diameter apart at rest, whatever the level (same level a unit apart, across levels half a unit each way and 1/√2 up).
        /// </summary>
        internal static float PeakStretch(PhysicsBall[,,] balls)
        {
            XZLevel size = XZLevel.FromArray(balls);
            float peak = 0f;

            for (int x = 0; x < size.X; x++)
                for (int z = 0; z < size.Z; z++)
                    for (int l = 0; l < size.Level; l++)
                    {
                        PhysicsBall ball = balls[x, z, l];
                        if (ball == null) continue;

                        foreach (XZLevel n in BallsMap.GetNeighboringCells(new XZLevel(x, z, l), size))
                        {
                            PhysicsBall other = balls[n.X, n.Z, n.Level];
                            if (other == null) continue;

                            float d = (ball.BallReference.Pose.Position - other.BallReference.Pose.Position).Length();
                            peak = MathF.Max(peak, d - 2f * BallsConstraintsBuilder.BALL_RADIUS);
                        }
                    }

            return peak;
        }

        private static string Cell(XZLevel? cell) => cell is XZLevel c ? $"({c.X},{c.Z},{c.Level})" : "none";

        private static string Row(string level, int seed, string label, Response r) => string.Format(CultureInfo.InvariantCulture,
            "{0,-14} seed {1,3} {2,-22} balls {3,4} push {4,3} balls {5,6:0.0} | peak {6:0.000} at {7:0.00} s | 0.1 s {8:0.000} 0.2 s {9:0.000} 0.3 s {10:0.000} 0.5 s {11:0.000} 1 s {12:0.000} 2 s {13:0.000} | mean at peak {14:0.000} | stretch {15:0.000} (without {16:0.000}) | lowest down {17:0.000} | cell {18}",
            level, seed, label, r.Balls, r.Push.Balls, r.Push.Impulse, r.Peak, r.PeakStep * HungLevel.TIMESTEP,
            r.At(0.1f), r.At(0.2f), r.At(0.3f), r.At(0.5f), r.At(1f), r.At(2f), r.MeanDisplacement[r.PeakStep],
            r.PeakStretch, r.PeakStretchWithout, r.LowestDrop,
            r.Landed == null ? "none" : r.Landed.Equals(r.LandedWithout) ? "same" : "DIFFERS");

        /// <summary>
        /// The measurement the push's figures were chosen from, off unless <c>BS3D_SHOT_PUSH=&lt;aims per level&gt;</c> is set (it
        /// takes minutes): dense blocks, thin sheets and strands, each shot at with the shipped push and a sweep around it.
        /// </summary>
        [Fact]
        public void TheReportSaysHowFarAPushMovesTheCluster()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("BS3D_SHOT_PUSH"), out int aims) || aims <= 0)
            {
                output.WriteLine("BS3D_SHOT_PUSH is not set: the measurement is skipped.");
                return;
            }

            string[] levels = (Environment.GetEnvironmentVariable("BS3D_SHOT_PUSH_LEVELS")
                ?? "One.json,Kiln.json,Colossus.json,Pennant.json,Sail.json,Hammock.json,Pendant.json,Amphora.json,Rope.json,Zigzag.json")
                .Split(',');

            List<(string Label, float Scale, Manual? Manual)> configurations = new()
            {
                ("shipped", 1f, null),
            };

            string sweep = Environment.GetEnvironmentVariable("BS3D_SHOT_PUSH_SWEEP");
            if (!string.IsNullOrEmpty(sweep))
                foreach (string item in sweep.Split(';'))
                {
                    string[] parts = item.Split(':');
                    Manual m = new(float.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture),
                        float.Parse(parts[2], CultureInfo.InvariantCulture),
                        parts.Length > 3 ? float.Parse(parts[3], CultureInfo.InvariantCulture) : ShotPush.HOP_FALLOFF);
                    configurations.Add((string.Create(CultureInfo.InvariantCulture, $"x{m.Scale} r{m.Reach} k{m.MaxKick} f{m.Falloff}"), 0f, m));
                }

            foreach (string level in levels)
                for (int seed = 1; seed <= aims; seed++)
                    foreach ((string label, float scale, Manual? manual) in configurations)
                    {
                        Response r = Fire(level, seed, 2f, scale, manual);
                        if (r.Landed == null) { output.WriteLine($"{level} seed {seed}: no landing"); break; }
                        output.WriteLine(Row(level, seed, label, r));
                    }
        }

        /// <summary>
        /// Shots fired one after another into one hung level, each <paramref name="gap"/> seconds after the last landed, so
        /// every shot after the first meets a cluster the last one pushed (<see cref="ShotRig"/>, one worker thread): how many
        /// landed in the cell their ghost showed as they left. Every landing completes no group, so the cluster only grows.
        /// </summary>
        internal static (int Compared, int Same, List<string> Others) Succession(string level, int seed, int shots, float gap,
            float scale, bool promise = true, bool lead = true)
        {
            using ShotRig rig = new(level, workerThreads: 1);
            rig.Handler.PushScale = scale;
            rig.StampsPromise = promise;
            rig.Leads = lead;

            Random rng = new(seed);
            int compared = 0, same = 0;
            List<string> others = new();

            for (int i = 0; i < shots; i++)
            {
                (XVector3 muzzle, XVector3 target, float glance) = ShotAgreementTests.RandomAim(rig.Hung.Balls, rng);
                ShotResult result = rig.Fire(muzzle, target, glance);

                if (result.Outcome != ShotOutcome.NoGhost)
                {
                    compared++;
                    if (result.Outcome == ShotOutcome.SameCell) same++;
                    else others.Add($"{level} shot {i}: {result.Outcome} (promised {Cell(result.Promised)}, landed {Cell(result.Landed)})");
                }

                rig.Hung.Run(gap);
            }

            return (compared, same, others);
        }

        /// <summary>
        /// The measurement of what the push costs the ghost, off unless <c>BS3D_SHOT_PUSH_SUCCESSION=&lt;shots per level&gt;</c>
        /// is set: <see cref="ShotAgreementTests"/>' six levels, shots in quick succession with the push and without, at
        /// the gaps <c>BS3D_SHOT_PUSH_GAPS</c> lists (seconds between a landing and the next shot; 0.3 and 1 by default).
        /// </summary>
        [Fact]
        public void TheReportSaysHowOftenAPushedClusterStillTakesTheShotWhereTheGhostShowed()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("BS3D_SHOT_PUSH_SUCCESSION"), out int shots) || shots <= 0)
            {
                output.WriteLine("BS3D_SHOT_PUSH_SUCCESSION is not set: the measurement is skipped.");
                return;
            }

            string[] levels = (Environment.GetEnvironmentVariable("BS3D_SHOT_PUSH_LEVELS")
                ?? "Pennant.json,One.json,Colossus.json,Heart.json,Rainbow.json,Zigzag.json").Split(',');
            string[] gaps = (Environment.GetEnvironmentVariable("BS3D_SHOT_PUSH_GAPS") ?? "0.3,1").Split(',');

            //The push's scales to compare, none and the shipped one by default (BS3D_SHOT_PUSH_SCALES, comma-separated)
            static IEnumerable<float> Scales()
            {
                foreach (string scale in (Environment.GetEnvironmentVariable("BS3D_SHOT_PUSH_SCALES") ?? "0,1").Split(','))
                    yield return float.Parse(scale, CultureInfo.InvariantCulture);
            }

            //Which of the ghost's two answers to a moving cluster each pass keeps: the promise (PhysicsBall.PromisedCell) and
            //the lead (ShotPlacement.TryFindFirstHitCurved's), as "promise:lead" pairs of 0 and 1 (BS3D_SHOT_PUSH_MODES)
            static IEnumerable<(bool, bool)> Modes()
            {
                foreach (string mode in (Environment.GetEnvironmentVariable("BS3D_SHOT_PUSH_MODES") ?? "0:0,1:1").Split(','))
                    yield return (mode[0] == '1', mode[2] == '1');
            }

            foreach (string gapText in gaps)
            {
                float gap = float.Parse(gapText, CultureInfo.InvariantCulture);

                foreach (float scale in Scales())
                    foreach ((bool promise, bool lead) in Modes())
                    {
                        int compared = 0, same = 0;
                        List<string> others = new();

                        foreach (string level in levels)
                        {
                            (int c, int s, List<string> o) = Succession(level, 1192 + level.Length, shots, gap, scale, promise, lead);
                            compared += c;
                            same += s;
                            others.AddRange(o);
                        }

                        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                            $"gap {gap:0.0#} s, push x{scale}, promise {(promise ? "kept" : "not kept")}, {(lead ? "leading" : "not leading")}: {same} of {compared} in the ghost's cell ({100.0 * same / Math.Max(1, compared):0.0} %)"));
                        foreach (string other in others) output.WriteLine("  " + other);
                    }
            }
        }
    }
}
