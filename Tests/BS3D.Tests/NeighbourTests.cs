using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The lattice's neighbour rule, which CLAUDE.md states holds "by construction" and which the parity
    /// arithmetic is written out for in several places: a neighbour of a neighbour is the cell itself, every
    /// neighbour touches (exactly one ball diameter away in <see cref="BallsMap.GetRealPosition"/>), and nothing
    /// the rule leaves out touches. Two field sizes, so both an even and an odd number of levels are walked and
    /// every cell of both parities is a centre at least once.
    /// </summary>
    public class NeighbourTests
    {
        private const float DIAMETER = 1f;
        private const float EPSILON = 1e-4f;

        public static IEnumerable<object[]> Fields() =>
        [
            [5, 5, 6],
            [6, 7, 7],
        ];

        private static List<XZLevel> Neighbours(XZLevel cell, XZLevel size)
        {
            Span<XZLevel> buffer = stackalloc XZLevel[BallsMap.MAX_NEIGHBORS];
            int count = BallsMap.FillNeighboringCells(cell, size, buffer);

            List<XZLevel> list = new(count);
            for (int i = 0; i < count; i++) list.Add(buffer[i]);

            return list;
        }

        private static IEnumerable<XZLevel> Cells(XZLevel size)
        {
            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                        yield return new XZLevel(x, z, level);
        }

        private static Vector3 Real(XZLevel cell) => BallsMap.GetRealPosition((byte)cell.X, (byte)cell.Z, (byte)cell.Level);

        [Theory]
        [MemberData(nameof(Fields))]
        public void NeighbourRelationIsSymmetric(int sx, int sz, int levels)
        {
            XZLevel size = new(sx, sz, levels);

            foreach (XZLevel a in Cells(size))
                foreach (XZLevel b in Neighbours(a, size))
                    Assert.True(Neighbours(b, size).Contains(a),
                        $"{b.X},{b.Z},{b.Level} is a neighbour of {a.X},{a.Z},{a.Level} but not the other way round");
        }

        [Theory]
        [MemberData(nameof(Fields))]
        public void AtMostFourPerGroupAndNoCellTwice(int sx, int sz, int levels)
        {
            XZLevel size = new(sx, sz, levels);

            foreach (XZLevel a in Cells(size))
            {
                List<XZLevel> n = Neighbours(a, size);

                Assert.Equal(n.Count, new HashSet<XZLevel>(n).Count);
                Assert.DoesNotContain(a, n);
                Assert.True(n.FindAll(c => c.Level == a.Level - 1).Count <= 4);
                Assert.True(n.FindAll(c => c.Level == a.Level).Count <= 4);
                Assert.True(n.FindAll(c => c.Level == a.Level + 1).Count <= 4);
                Assert.True(n.TrueForAll(c => Math.Abs(c.Level - a.Level) <= 1));
            }
        }

        /// <summary>
        /// Every neighbour at exactly one diameter, and every other cell of the 3×3×3 window round a cell further
        /// than that — so the rule is the touching relation itself, not merely a subset of it.
        /// </summary>
        [Theory]
        [MemberData(nameof(Fields))]
        public void NeighboursAreExactlyTheCellsOneDiameterAway(int sx, int sz, int levels)
        {
            XZLevel size = new(sx, sz, levels);

            foreach (XZLevel a in Cells(size))
            {
                List<XZLevel> n = Neighbours(a, size);

                foreach (XZLevel b in n)
                    Assert.Equal(DIAMETER, Vector3.Distance(Real(a), Real(b)), EPSILON);

                for (int dl = -1; dl <= 1; dl++)
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            XZLevel b = new(a.X + dx, a.Z + dz, a.Level + dl);
                            if (b.X < 0 || b.Z < 0 || b.Level < 0 || b.X >= size.X || b.Z >= size.Z || b.Level >= size.Level) continue;
                            if (b.Equals(a) || n.Contains(b)) continue;

                            Assert.True(Vector3.Distance(Real(a), Real(b)) > DIAMETER + EPSILON,
                                $"{b.X},{b.Z},{b.Level} touches {a.X},{a.Z},{a.Level} but is not listed as its neighbour");
                        }
            }
        }

        /// <summary>
        /// The two other walks of the same neighbourhood agree with <see cref="BallsMap.FillNeighboringCells"/>:
        /// the allocation-free enumerator (same cells, same order — the order is load-bearing, see its doc) and
        /// the occlusion count, which walks the parity arithmetic itself, over a seeded random occupancy.
        /// </summary>
        [Theory]
        [MemberData(nameof(Fields))]
        public void OtherWalksAgreeWithFillNeighboringCells(int sx, int sz, int levels)
        {
            XZLevel size = new(sx, sz, levels);
            Random random = new(586);
            object[,,] occupied = new object[sx, sz, levels];

            foreach (XZLevel c in Cells(size))
                if (random.NextDouble() < 0.5) occupied[c.X, c.Z, c.Level] = new object();

            foreach (XZLevel a in Cells(size))
            {
                List<XZLevel> filled = Neighbours(a, size);

                List<XZLevel> enumerated = [];
                foreach (XZLevel c in BallsMap.GetNeighboringCells(a, size)) enumerated.Add(c);
                Assert.Equal(filled, enumerated);

                int expected = filled.FindAll(c => occupied[c.X, c.Z, c.Level] != null).Count;
                Assert.Equal(expected, BallsMap.CountOccupiedNeighbors(occupied, a, size, out _));
            }
        }
    }
}
