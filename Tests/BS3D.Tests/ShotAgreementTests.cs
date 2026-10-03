using Prazsky.BS3D.Physics;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
using XVector3 = Microsoft.Xna.Framework.Vector3;

namespace BS3D.Tests
{
    /// <summary>
    /// #696: the landing ghost and the real landing agree. A shot is aimed at a random ball of a real level from a ring
    /// under the cluster, off its centre by up to 1.2 units perpendicular to the line (a "glance": from dead centre to just
    /// past a graze), the preview is asked what it promises, the shot is fired through the real simulation and handler, and
    /// the cell it stuck in is compared with the cell promised (<see cref="ShotRig"/>). Every shot gets a freshly hung level:
    /// a landing changes the structure and starts it swinging, and the ghost is a promise about the cluster as it stands.
    /// <para>
    /// <b>Measured before and after, on the same 238 aims (six shipped levels, 40 shots each, 1.5 s of settling):</b>
    /// the shot stuck in the ghost's cell 63.6 % of the time before the change (152 of 239, 76 in another cell, 11 never
    /// resolved) and 97.9 % after (233 of 238), at every glance down to a dead-centre aim. The three parts, each alone and
    /// together: a segment test that lost balls (the dominant one), a landing decided by the preview's own sweep every step,
    /// and the world's gravity in the ghost's flight - sweep alone 93.3 %, sweep and gravity 95.8 %, with the narrow phase
    /// no longer turning a shot by a contact it had not reached 97.9 %.
    /// </para>
    /// </summary>
    public class ShotAgreementTests(ITestOutputHelper output)
    {
        private static readonly string[] RigLevels =
            ["Pennant.json", "One.json", "Colossus.json", "Heart.json", "Rainbow.json", "Zigzag.json"];

        /// <summary>
        /// Aims at a random ball of the structure from a ring under the cluster, off its centre by up to 1.2 units
        /// perpendicular to the line. The offset is the "glance" the report bins by.
        /// </summary>
        private static (XVector3 Muzzle, XVector3 Target, float Glance) RandomAim(PhysicsBall[,,] balls, Random rng)
        {
            List<PhysicsBall> all = new();
            float lowest = float.MaxValue;
            foreach (PhysicsBall ball in balls)
            {
                if (ball == null) continue;
                all.Add(ball);
                lowest = MathF.Min(lowest, ball.BallReference.Pose.Position.Y);
            }

            PhysicsBall aimed = all[rng.Next(all.Count)];
            XVector3 centre = aimed.BallReference.Pose.Position.ToXna();

            double phi = rng.NextDouble() * 2.0 * Math.PI;
            XVector3 muzzle = new((float)Math.Cos(phi) * 18f, lowest - 10f, (float)Math.Sin(phi) * 18f);

            XVector3 direction = XVector3.Normalize(centre - muzzle);
            XVector3 helper = MathF.Abs(direction.Y) < 0.9f ? XVector3.UnitY : XVector3.UnitX;
            XVector3 sideways = XVector3.Normalize(XVector3.Cross(direction, helper));
            XVector3 upwards = XVector3.Cross(direction, sideways);

            double angle = rng.NextDouble() * 2.0 * Math.PI;
            float offset = (float)(rng.NextDouble() * 1.2);
            XVector3 target = centre + (sideways * (float)Math.Cos(angle) + upwards * (float)Math.Sin(angle)) * offset;

            return (muzzle, target, offset);
        }

        private static List<ShotResult> Run(IEnumerable<string> levels, int shotsPerLevel)
        {
            List<ShotResult> results = new();

            foreach (string level in levels)
            {
                Random rng = new(696 + level.Length);

                for (int i = 0; i < shotsPerLevel; i++)
                {
                    using ShotRig rig = new(level);

                    (XVector3 muzzle, XVector3 target, float glance) = RandomAim(rig.Hung.Balls, rng);
                    ShotResult result = rig.Fire(muzzle, target, glance);

                    if (result.Outcome != ShotOutcome.NoGhost) results.Add(result);
                }
            }

            return results;
        }

        /// <summary>
        /// The promise, in a small sample: three levels, twelve aims each. Held at 85 %, where the change measured 97.9 % over
        /// 238 and the handling it replaced 63.6 %: far enough from both that a run of thirty-odd aims on a machine with another
        /// core count cannot tip it either way.
        /// </summary>
        [Fact]
        public void AShotLandsInTheCellItsGhostShowed()
        {
            List<ShotResult> results = Run(["Pennant.json", "One.json", "Colossus.json"], 12);
            Assert.True(results.Count >= 30, $"only {results.Count} of 36 aims had a ghost to compare");

            int same = results.FindAll(r => r.Outcome == ShotOutcome.SameCell).Count;
            string report = string.Join(", ", results.FindAll(r => r.Outcome != ShotOutcome.SameCell)
                .ConvertAll(r => $"{r.Outcome} (glance {r.GlanceOffset:0.00}, promised {r.Promised}, landed {r.Landed})"));

            Assert.True(same >= 0.85 * results.Count, $"{same} of {results.Count} shots landed in the ghost's cell; the others: {report}");
        }

        /// <summary>
        /// What the ghost's honesty rests on: the simulated shot flies the flight the preview integrates (semi-implicit Euler at
        /// the simulation's step, gravity included) to far better than a thousandth of a unit, at every step before it lands.
        /// Nothing in the step turns it - the narrow phase makes no constraint for a contact it has not reached - and gravity
        /// is the only force on it.
        /// </summary>
        [Fact]
        public void TheSimulatedFlightIsTheFlightThePreviewIntegrates()
        {
            List<ShotResult> results = Run(["Pennant.json", "Colossus.json"], 4);
            Assert.NotEmpty(results);

            foreach (ShotResult result in results)
                Assert.True(result.FlightDeviation < 1e-3f, $"the flight left the preview's by {result.FlightDeviation} over {result.Steps} steps");
        }

        /// <summary>
        /// Bank shots (#257 and #696): a shot aimed at the mirror image of a ball in the face of a tall crate beside the
        /// level, so it reaches the cluster off the crate at a slant. The bounce is the crates' own reflection, run in the
        /// step before the integrator, which moves the ball - so the step's recorded start pose is not where the flight
        /// began, and the handler's sweep over it has to find the same ball the preview's two-legged flight finds.
        /// Measured: 23 of 24 land in the ghost's cell (the same 23 in five runs on the desktop); with the landing handed back
        /// to Bepu's contacts and the narrow phase's constraints restored, but the segment fix and the gravity in the ghost
        /// kept, 18 of 24. Held at 85 %.
        /// </summary>
        [Fact]
        public void ABankShotLandsInTheCellItsGhostShowed()
        {
            int compared = 0, same = 0;
            List<string> others = new();
            Random rng = new(257);

            for (int i = 0; i < 24; i++)
            {
                using ShotRig rig = new("Pennant.json");

                float lowest = float.MaxValue;
                List<PhysicsBall> low = new();
                foreach (PhysicsBall ball in rig.Hung.Balls)
                    if (ball != null) lowest = MathF.Min(lowest, ball.BallReference.Pose.Position.Y);
                foreach (PhysicsBall ball in rig.Hung.Balls)
                    if (ball != null && ball.BallReference.Pose.Position.Y < lowest + 4f) low.Add(ball);

                float y = lowest + 1f;
                rig.AddCrate(new System.Numerics.Vector3(7f, y, -12f), new System.Numerics.Vector3(1f, 4f, 16f));

                //From out in front, along the line to the mirror image of a ball in the face at x = 5.5 (the crate grown by a radius)
                PhysicsBall aimed = low[rng.Next(low.Count)];
                XVector3 b = aimed.BallReference.Pose.Position.ToXna();
                XVector3 muzzle = new(1f + (float)rng.NextDouble() * 3f, y, -30f);
                XVector3 mirrored = new(2f * 5.5f - b.X, b.Y, b.Z);

                ShotResult result = rig.Fire(muzzle, mirrored, 0f);
                if (result.Outcome == ShotOutcome.NoGhost) continue;

                compared++;
                if (result.Outcome == ShotOutcome.SameCell) same++;
                else others.Add($"{result.Outcome} promised {result.Promised} landed {result.Landed}");
            }

            output.WriteLine($"bank shots with a ghost: {compared}, same cell {same}; the others: {string.Join("; ", others)}");
            Assert.True(compared >= 8, $"only {compared} of 24 bank aims had a ghost to compare");
            Assert.True(same >= 0.85 * compared, $"{same} of {compared} bank shots landed in the ghost's cell; the others: {string.Join("; ", others)}");
        }

        /// <summary>
        /// What the ghost costs a frame (#696): the straight sweep it was, and the stepped flight with the world's gravity it is
        /// now, on the biggest shipped levels, per call. Off unless <c>BS3D_SHOT_COST=1</c>: a timing is not an assertion.
        /// </summary>
        [Fact]
        public void TheGhostsCostPerFrame()
        {
            if (Environment.GetEnvironmentVariable("BS3D_SHOT_COST") == null)
            {
                output.WriteLine("BS3D_SHOT_COST is not set: the timing is skipped.");
                return;
            }

            foreach (string level in new[] { "Pennant.json", "Colossus.json", "Cairn.json" })
            {
                using HungLevel hung = HungLevel.FromLevelFile(ShotRig.LevelPath(level));
                hung.Run(1f);

                int balls = 0;
                float lowest = float.MaxValue;
                foreach (PhysicsBall ball in hung.Balls)
                {
                    if (ball == null) continue;
                    balls++;
                    lowest = MathF.Min(lowest, ball.BallReference.Pose.Position.Y);
                }

                Random rng = new(1);
                XVector3[] muzzles = new XVector3[64], velocities = new XVector3[64];
                for (int i = 0; i < 64; i++)
                {
                    (XVector3 muzzle, XVector3 target, _) = RandomAim(hung.Balls, rng);
                    muzzles[i] = muzzle;
                    velocities[i] = XVector3.Normalize(target - muzzle) * ShotRig.SHOOT_SPEED;
                }

                foreach (bool gravity in new[] { false, true })
                {
                    //Warmed up, then timed over many calls
                    for (int i = 0; i < 64; i++)
                        ShotPlacement.TryFindFirstHitCurved(hung.Balls, muzzles[i], velocities[i], 1f, null, out _, out _, worldGravity: gravity);

                    int calls = 4000;
                    long start = System.Diagnostics.Stopwatch.GetTimestamp();
                    for (int i = 0; i < calls; i++)
                        ShotPlacement.TryFindFirstHitCurved(hung.Balls, muzzles[i % 64], velocities[i % 64], 1f, null, out _, out _, worldGravity: gravity);
                    double microseconds = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds * 1000.0 / calls;

                    output.WriteLine($"{level} ({balls} balls) {(gravity ? "stepped with gravity" : "straight sweep")}: {microseconds:0.0} us a call");
                }
            }
        }

        /// <summary>
        /// The measurement itself, off unless <c>BS3D_SHOT_RIG=&lt;aims per level&gt;</c> is set (it takes minutes): the same aims
        /// over six levels, binned by glance. Run it before and after any change to how a shot is handled.
        /// </summary>
        [Fact]
        public void TheRigReportsHowOftenTheGhostIsTheLanding()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("BS3D_SHOT_RIG"), out int shotsPerLevel) || shotsPerLevel <= 0)
            {
                output.WriteLine("BS3D_SHOT_RIG is not set: the measurement is skipped.");
                return;
            }

            List<ShotResult> results = Run(RigLevels, shotsPerLevel);

            string[] names = ["glance 0.0-0.4", "glance 0.4-0.8", "glance 0.8-1.2"];
            for (int bin = 0; bin < 3; bin++)
            {
                List<ShotResult> inBin = results.FindAll(r => (r.GlanceOffset < 0.4f ? 0 : r.GlanceOffset < 0.8f ? 1 : 2) == bin);

                string line = $"{names[bin]}: {inBin.Count} shots";
                foreach (ShotOutcome outcome in Enum.GetValues<ShotOutcome>())
                {
                    int n = inBin.FindAll(r => r.Outcome == outcome).Count;
                    if (n > 0) line += $", {outcome} {n} ({100.0 * n / Math.Max(1, inBin.Count):0.0} %)";
                }

                output.WriteLine(line);
            }

            output.WriteLine($"total compared: {results.Count}");
        }
    }
}
