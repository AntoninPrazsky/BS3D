using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameObjects;
using Prazsky.Core;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The floor net ends on a boundary through its four corners (#941): no beam goes past the edge it ends on, the
    /// outermost beams are that edge, the corners are where the emitters aim, and the bend folds nothing. Over the real
    /// path of every beam (<see cref="LaserNetShape.BeamPoint"/>, the one the grid builds its vertices from) for every pair
    /// of stage sizes a level can have.
    /// </summary>
    public class LaserNetShapeTests
    {
        private static readonly int[] STAGES = { 5, 9, 11, 12, 13, 15, 16, 17 };

        //The level's own footprint is what the session fits the net at (GameplayScreen.BuildLevel)
        private static float CornerFor(int stage) => LaserNetShape.CornerFor(CeilingPlate.FootprintFor(stage) * Constants.HALF);

        private static IEnumerable<(float X, float Z)> Nets()
        {
            foreach (int sx in STAGES)
                foreach (int sz in STAGES)
                    yield return (CornerFor(sx), CornerFor(sz));
        }

        //Every beam of a net: its path in plan, end to end
        private static IEnumerable<(bool AlongX, int Index, int Count, Vector2[] Path)> Beams(float cornerX, float cornerZ)
        {
            foreach (bool alongX in new[] { true, false })
            {
                int count = LaserNetShape.BeamCount(alongX ? cornerZ : cornerX);

                for (int i = 0; i < count; i++)
                {
                    float offset = LaserNetShape.BeamOffset(i, count);
                    Vector2[] path = new Vector2[LaserNetShape.BEAM_SEGMENTS + 1];

                    for (int s = 0; s < path.Length; s++)
                        path[s] = LaserNetShape.BeamPoint(alongX, offset, s, cornerX, cornerZ);

                    yield return (alongX, i, count, path);
                }
            }
        }

        [Fact]
        public void TheCornersAreTheLatticesOwnWholePitches()
        {
            //What a level's net has always had: a beam on every whole step out to the last one within the footprint and its margin
            Assert.Equal(6f, CornerFor(11));
            Assert.Equal(7f, CornerFor(12));
            Assert.Equal(9f, CornerFor(16));
            Assert.Equal(9f, CornerFor(17));

            foreach ((float cornerX, float cornerZ) in Nets())
            {
                Assert.Equal(cornerX, MathF.Floor(cornerX));
                Assert.Equal(cornerZ, MathF.Floor(cornerZ));
                Assert.Equal((int)cornerX * 2 + 1, LaserNetShape.BeamCount(cornerX));
            }
        }

        [Fact]
        public void EveryBeamEndsOnTheBoundaryAndNoneGoesPastIt()
        {
            foreach ((float cornerX, float cornerZ) in Nets())
            {
                foreach ((bool alongX, int index, int count, Vector2[] path) in Beams(cornerX, cornerZ))
                {
                    //Both ends are ON the boundary, and every point between is inside it
                    Assert.True(LaserNetShape.IsOnBoundary(path[0], cornerX, cornerZ), $"{cornerX}x{cornerZ} beam {index} of {count} (along X {alongX}): start {path[0]} is not on the boundary");
                    Assert.True(LaserNetShape.IsOnBoundary(path[^1], cornerX, cornerZ), $"{cornerX}x{cornerZ} beam {index} of {count} (along X {alongX}): end {path[^1]} is not on the boundary");

                    foreach (Vector2 p in path)
                        Assert.True(LaserNetShape.IsInside(p, cornerX, cornerZ), $"{cornerX}x{cornerZ} beam {index} of {count} (along X {alongX}): {p} is outside the boundary");

                    //And, without leaning on the boundary's own formula: no tip stands further out than the corner, or than
                    //the corner plus the swell if the bow is positive - the old net's lines ran a margin (0.25 to 0.75)
                    //past the outermost beam they ruled
                    float reach = MathF.Max(1f, 1f + LaserNetShape.BOW);

                    foreach (Vector2 p in path)
                    {
                        Assert.True(MathF.Abs(p.X) <= cornerX * reach + 1e-4f, $"{cornerX}x{cornerZ}: {p} is out past the net's reach in X");
                        Assert.True(MathF.Abs(p.Y) <= cornerZ * reach + 1e-4f, $"{cornerX}x{cornerZ}: {p} is out past the net's reach in Z");
                    }
                }
            }
        }

        [Fact]
        public void TheOutermostBeamsAreTheBoundaryAndMeetAtTheCornersTheEmittersAimAt()
        {
            foreach ((float cornerX, float cornerZ) in Nets())
            {
                foreach ((bool alongX, int index, int count, Vector2[] path) in Beams(cornerX, cornerZ))
                {
                    if (index != 0 && index != count - 1) continue;

                    foreach (Vector2 p in path)
                        Assert.True(LaserNetShape.IsOnBoundary(p, cornerX, cornerZ), $"{cornerX}x{cornerZ}: the outermost beam's {p} is not on the boundary");
                }

                //The four corners are the ends of the outermost beams, and where each emitter's axis lands (NetCorner)
                for (int k = 0; k < LaserEmitterRing.COUNT; k++)
                {
                    Vector3 corner = LaserEmitterRing.NetCorner(k, cornerX, cornerZ, -8f);
                    Vector2 corner2 = new(corner.X, corner.Z);
                    int last = LaserNetShape.BEAM_SEGMENTS;
                    Vector2[] ends =
                    {
                        LaserNetShape.BeamPoint(true, corner.Z, corner.X > 0f ? last : 0, cornerX, cornerZ),
                        LaserNetShape.BeamPoint(false, corner.X, corner.Z > 0f ? last : 0, cornerX, cornerZ),
                    };

                    foreach (Vector2 end in ends)
                        Assert.True(Vector2.Distance(end, corner2) < 1e-4f, $"{cornerX}x{cornerZ}: corner {k} is {corner2}, an outermost beam ends at {end}");
                }
            }
        }

        [Fact]
        public void TheNetIsBowedBetweenItsCornersAndFoldsNothing()
        {
            foreach ((float cornerX, float cornerZ) in Nets())
            {
                //The middle of each side stands off the straight line between its corners by the bow (in, where it is negative)
                Assert.True(MathF.Abs(cornerZ * (1f + LaserNetShape.BOW) - LaserNetShape.EdgeZ(0f, cornerX, cornerZ)) < 1e-4f);
                Assert.True(MathF.Abs(cornerX * (1f + LaserNetShape.BOW) - LaserNetShape.EdgeX(0f, cornerX, cornerZ)) < 1e-4f);

                //Every cell of the lattice keeps its turn: a fold would put two beams across each other where they are not a crossing
                int nx = LaserNetShape.BeamCount(cornerX), nz = LaserNetShape.BeamCount(cornerZ);

                for (int i = 0; i + 1 < nx; i++)
                {
                    for (int j = 0; j + 1 < nz; j++)
                    {
                        float x0 = LaserNetShape.BeamOffset(i, nx), x1 = LaserNetShape.BeamOffset(i + 1, nx);
                        float z0 = LaserNetShape.BeamOffset(j, nz), z1 = LaserNetShape.BeamOffset(j + 1, nz);

                        Vector2 a = LaserNetShape.Place(x0, z0, cornerX, cornerZ);
                        Vector2 b = LaserNetShape.Place(x1, z0, cornerX, cornerZ);
                        Vector2 c = LaserNetShape.Place(x1, z1, cornerX, cornerZ);
                        Vector2 d = LaserNetShape.Place(x0, z1, cornerX, cornerZ);

                        //Counter-clockwise on the lattice, so each of its four corners turns the same way here
                        foreach ((Vector2 p, Vector2 q, Vector2 r) in new[] { (a, b, c), (b, c, d), (c, d, a), (d, a, b) })
                        {
                            Vector2 pq = q - p, qr = r - q;
                            Assert.True(pq.X * qr.Y - pq.Y * qr.X > 0f, $"{cornerX}x{cornerZ}: the lattice cell ({i}, {j}) folds");
                        }
                    }
                }
            }
        }

        [Fact]
        public void TheNetStaysInsideTheDrainsMouthAndWithinSixteenBitIndices()
        {
            foreach ((float cornerX, float cornerZ) in Nets())
            {
                foreach ((bool _, int _, int _, Vector2[] path) in Beams(cornerX, cornerZ))
                    foreach (Vector2 p in path)
                        Assert.True(p.Length() < ArenaIsland.FUNNEL_TOP_RADIUS - 1f, $"{cornerX}x{cornerZ}: {p} is at the rim of the drain's mouth");

                //Two vertices a row and a row per beam segment edge, and the four cones' (7 rows each); the widest stage there is
                int beams = LaserNetShape.BeamCount(cornerX) + LaserNetShape.BeamCount(cornerZ);
                Assert.True((beams * (LaserNetShape.BEAM_SEGMENTS + 1) + 4 * 7) * 2 < ushort.MaxValue);
            }

            //A net twice as wide as the widest the set draws still fits one buffer
            int wide = LaserNetShape.BeamCount(LaserNetShape.CornerFor(CeilingPlate.FootprintFor(34) * Constants.HALF));
            Assert.True((2 * wide * (LaserNetShape.BEAM_SEGMENTS + 1) + 4 * 7) * 2 < ushort.MaxValue);
        }
    }
}
