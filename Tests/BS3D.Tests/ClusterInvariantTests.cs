using BepuPhysics;
using BepuPhysics.Collidables;
using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// <see cref="ClusterInvariants"/> after every operation of a long seeded sequence (#585): a level hung as
    /// the Game hangs it, then hundreds of balls landed into random free cells beside the cluster the way
    /// SagProbe lands a shot, each followed by the match rule and a few steps of the world. The releases free
    /// constraint handles and the attaches after them reuse those handles, which is exactly where a stale slot
    /// would start answering for an unrelated constraint — so the sequence is long on purpose.
    /// </summary>
    public class ClusterInvariantTests(ITestOutputHelper output)
    {
        private const int OPERATIONS = 300;

        /// <summary>Steps of the world between operations, so bodies swing, rotate and fall asleep between attaches.</summary>
        private const int STEPS_BETWEEN = 3;

        /// <summary>How often a landing takes a neighbour's colour rather than a random one — what makes matches frequent.</summary>
        private const double MATCH_BIAS = 0.6;

        /// <summary>
        /// Dense (Kiln), a free top level (Anchor, which the ceiling-anchor test reads too), bombs that the
        /// disconnection pass sets off (Vent), and the Testbed's full field.
        /// </summary>
        public static IEnumerable<object[]> Cases() =>
        [
            ["Anchor.json", 585],
            ["Kiln.json", 1],
            ["Vent.json", 2],
            ["Full", 3],
        ];

        [Theory]
        [MemberData(nameof(Cases))]
        public void BookkeepingHoldsThroughRandomAttachesAndReleases(string level, int seed)
        {
            using HungLevel hung = Hang(level);

            ClusterInvariants.Verify(hung.Balls, hung.Map, hung.World.Simulation, hung.Ceiling.Handle);

            Totals totals = RunSequence(hung, seed, OPERATIONS, verifyEach: true);
            output.WriteLine($"{level}: {totals.Attached} landed, {totals.Matches} matches, {totals.Released} released, {totals.Detonations} bombs set off");

            //The sequence has to have reached the paths it claims to check, or a pass would mean nothing
            Assert.True(totals.Attached >= OPERATIONS / 2, $"only {totals.Attached} of {OPERATIONS} operations landed a ball");
            Assert.True(totals.Matches >= 10, $"only {totals.Matches} landings completed a match");
            Assert.True(totals.Released >= 30, $"only {totals.Released} balls were released");
            if (level == "Vent.json")
                Assert.True(totals.Detonations > 0, "no orphaned bomb went off on the bomb level");
        }

        /// <summary>
        /// Two worlds alive at once each keep their own sphere: asking alternately must not add a shape per
        /// alternation, which the single cached (simulation, index) pair before #585 did.
        /// </summary>
        [Fact]
        public void SphereShapeIsCachedPerSimulation()
        {
            using PhysicsWorld a = new();
            using PhysicsWorld b = new();

            TypedIndex firstA = BallsConstraintsBuilder.GetSphereShapeIndex(a.Simulation);
            TypedIndex firstB = BallsConstraintsBuilder.GetSphereShapeIndex(b.Simulation);

            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(firstA.Packed, BallsConstraintsBuilder.GetSphereShapeIndex(a.Simulation).Packed);
                Assert.Equal(firstB.Packed, BallsConstraintsBuilder.GetSphereShapeIndex(b.Simulation).Packed);
            }
        }

        internal readonly record struct Totals(int Attached, int Matches, int Released, int Detonations);

        internal static HungLevel Hang(string level)
        {
            if (level != "Full") return HungLevel.FromLevelFile(Shipped.Level(level));

            string full = Path.Combine(Shipped.LevelsDirectory, "..", "..", "Testbed", "Maps", "Full.json");
            Assert.True(File.Exists(full), full);
            return new HungLevel(new BallsMap(full));
        }

        /// <summary>
        /// <paramref name="operations"/> landings into random free cells, each followed by
        /// <see cref="BallsConstraintsBuilder.ReleaseSameTypeCluster"/> (which ends in the disconnection pass,
        /// so orphans and orphaned bombs are covered too) and <see cref="STEPS_BETWEEN"/> steps. Released balls
        /// are taken out of the world, so their bodies' handles are reused as well.
        /// </summary>
        internal static Totals RunSequence(HungLevel hung, int seed, int operations, bool verifyEach)
        {
            Random random = new(seed);
            Simulation simulation = hung.World.Simulation;
            XZLevel size = hung.Map.GetStaticBallsArraySize();
            List<PhysicsBall> released = new();
            List<XZLevel> candidates = new();
            Span<XZLevel> neighbours = stackalloc XZLevel[BallsMap.MAX_NEIGHBORS];

            //The colours the level itself uses, so a random landing is one the level could be shot with
            List<BallType> palette = new();
            foreach (StaticBall ball in hung.Map.GetStaticBallsArray())
                if (ball != null && !palette.Contains(ball.Type)) palette.Add(ball.Type);

            int attached = 0, matches = 0, releasedCount = 0;
            List<Detonation> detonations = new(); //accumulates: every bomb an orphaning release set off

            for (int op = 0; op < operations; op++)
            {
                StaticBall[,,] cells = hung.Map.GetStaticBallsArray();

                //Every free cell a shot could stick in: beside an occupied one, or anywhere on the top level
                candidates.Clear();
                for (int level = 0; level < size.Level; level++)
                    for (int x = 0; x < size.X; x++)
                        for (int z = 0; z < size.Z; z++)
                        {
                            if (cells[x, z, level] != null) continue;

                            XZLevel cell = new(x, z, level);
                            bool touches = level == size.Level - 1;
                            int count = BallsMap.FillNeighboringCells(cell, size, neighbours);
                            for (int i = 0; i < count && !touches; i++)
                                touches = cells[neighbours[i].X, neighbours[i].Z, neighbours[i].Level] != null;

                            if (touches) candidates.Add(cell);
                        }

                if (candidates.Count == 0) break;

                XZLevel at = candidates[random.Next(candidates.Count)];
                BallType type = palette[random.Next(palette.Count)];

                if (random.NextDouble() < MATCH_BIAS)
                {
                    int count = BallsMap.FillNeighboringCells(at, size, neighbours);
                    int start = random.Next(Math.Max(count, 1));
                    for (int i = 0; i < count; i++)
                    {
                        XZLevel n = neighbours[(start + i) % count];
                        if (cells[n.X, n.Z, n.Level] == null) continue;
                        type = cells[n.X, n.Z, n.Level].Type;
                        break;
                    }
                }

                PhysicsBall landed = Land(hung, at, type);
                attached++;

                released.Clear();
                BallsReleased result = BallsConstraintsBuilder.ReleaseSameTypeCluster(landed, hung.Balls, hung.Map,
                    simulation, released, detonationsInto: detonations);
                if (result.Matched > 0) matches++;
                releasedCount += released.Count;

                foreach (PhysicsBall gone in released) simulation.Bodies.Remove(gone.BallReference.Handle);

                hung.Run(STEPS_BETWEEN * HungLevel.TIMESTEP);

                if (verifyEach)
                {
                    List<string> problems = ClusterInvariants.Check(hung.Balls, hung.Map, simulation, hung.Ceiling.Handle);
                    Assert.True(problems.Count == 0,
                        $"after operation {op} (a {type} ball into ({at.X}, {at.Z}, {at.Level})): " + string.Join("; ", problems));
                }
            }

            return new Totals(attached, matches, releasedCount, detonations.Count);
        }

        /// <summary>A ball landed into <paramref name="cell"/> exactly as SagProbe lands a shot (and as <c>TopLevelAttachTests</c> does).</summary>
        private static PhysicsBall Land(HungLevel hung, XZLevel cell, BallType type)
        {
            Vector3 rest = hung.Map.GetRealCenteredPosition(cell) + hung.WorldOffset;

            BodyHandle handle = hung.World.Simulation.Bodies.Add(BodyDescription.CreateDynamic(
                rest.ToNumerics(),
                new Sphere(BallsConstraintsBuilder.BALL_RADIUS).ComputeInertia(BallsConstraintsBuilder.BALL_MASS),
                new CollidableDescription(BallsConstraintsBuilder.GetSphereShapeIndex(hung.World.Simulation),
                    BallsConstraintsBuilder.SPECULATIVE_MARGIN),
                new BodyActivityDescription(PhysicsWorld.SLEEP_THRESHOLD)));

            PhysicsBall landed = new()
            {
                BallReference = new BodyReference(handle, hung.World.Simulation.Bodies),
                Type = type,
                ArrayPosition = cell,
            };

            hung.Map.PutBallAt((byte)cell.X, (byte)cell.Z, (byte)cell.Level, type);
            hung.Balls[cell.X, cell.Z, cell.Level] = landed;

            BallsConstraintsBuilder.AttachBallToStructure(landed, hung.Balls, hung.Map, hung.World.Simulation,
                hung.Ceiling, hung.WorldOffset.ToNumerics());

            return landed;
        }
    }
}
