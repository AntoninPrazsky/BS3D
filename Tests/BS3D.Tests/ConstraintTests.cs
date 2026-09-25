using BepuPhysics;
using BepuPhysics.Constraints;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// "Every pair gets exactly one constraint by construction" (CLAUDE.md, "Constraint handle bookkeeping"):
    /// the build pass connects same-level pairs towards +X/+Z only and cross-level pairs from the even level
    /// only, and every top-level ball gets one socket to the glass. So the solver must hold exactly the
    /// neighbour graph's edges plus the top level's balls — and every handle must have found a slot on both
    /// of its balls, since a <see cref="ConstraintHandles.TryStore"/> that failed would leave a constraint no
    /// release can ever remove.
    /// </summary>
    public class ConstraintTests
    {
        public static IEnumerable<object[]> Levels() => Shipped.LevelFileNames();

        [Theory]
        [MemberData(nameof(Levels))]
        public void OneConstraintPerNeighbouringPairOnEveryShippedLevel(string file)
        {
            using HungLevel hung = HungLevel.FromLevelFile(Shipped.Level(file));
            AssertOneConstraintPerPair(hung);
        }

        /// <summary>The Testbed's legacy <c>Full.json</c>, a completely filled field — every cell has its full neighbourhood.</summary>
        [Fact]
        public void OneConstraintPerNeighbouringPairOnTheFullTestMap()
        {
            string full = Path.Combine(Shipped.LevelsDirectory, "..", "..", "Testbed", "Maps", "Full.json");
            Assert.True(File.Exists(full), full);

            using HungLevel hung = new(new BallsMap(full));
            AssertOneConstraintPerPair(hung);
        }

        private static void AssertOneConstraintPerPair(HungLevel hung)
        {
            StaticBall[,,] balls = hung.Map.GetStaticBallsArray();
            XZLevel size = XZLevel.FromArray(balls);

            int ends = 0, top = 0, ballCount = 0;
            Span<XZLevel> buffer = stackalloc XZLevel[BallsMap.MAX_NEIGHBORS];

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        if (balls[x, z, level] == null) continue;
                        ballCount++;

                        int count = BallsMap.FillNeighboringCells(new XZLevel(x, z, level), size, buffer);
                        int degree = 0;
                        for (int i = 0; i < count; i++)
                            if (balls[buffer[i].X, buffer[i].Z, buffer[i].Level] != null) degree++;

                        bool isTop = level == size.Level - 1;
                        if (isTop) top++;
                        ends += degree;

                        //Every constraint this ball takes part in found a slot: a neighbour socket each, plus the glass
                        List<ConstraintHandle> stored = [];
                        hung.Balls[x, z, level].CollectConstraintHandles(stored);
                        Assert.Equal(degree + (isTop ? 1 : 0), stored.Count);

                        foreach (ConstraintHandle handle in stored)
                            Assert.True(hung.World.Simulation.Solver.ConstraintExists(handle));
                    }

            Assert.True(ballCount > 0);
            Assert.True(top > 0, "a hanging level with nothing on its top level hangs from nothing");
            Assert.Equal(0, ends % 2);

            int edges = ends / 2;
            Assert.Equal(edges + top, hung.World.Simulation.Solver.CountConstraints());
        }
    }
}
