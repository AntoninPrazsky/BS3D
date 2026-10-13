using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The kinds' shapes on the Potato path (#863): the desktop's vertex-shader shapes as meshes. No device; what is
    /// held is what the desktop holds by construction - the rock and the ice inward only, the studs past the casing by
    /// what the desktop raises them, every triangle wound clockwise seen from outside (CLAUDE.md's rule, with the
    /// sphere's own words: a triangle whose (b - a) x (c - a) points at the viewer is culled).
    /// </summary>
    public class PotatoKindMeshTests
    {
        private static Vector3 Direction(float x, float y, float z) => Vector3.Normalize(new Vector3(x, y, z));

        [Fact]
        public void TheRockIsCarvedInwardOnlyAndNoDeeperThanItsDepth()
        {
            var random = new Random(863);
            float deepest = 1f, shallowest = 0f;

            for (int i = 0; i < 4000; i++)
            {
                Vector3 d = Direction((float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1);
                PotatoKindGeometry.RockShape(d, 0.2f, out float r, out Vector3 n);

                //The deepest cut is the shallowest plane's offset, 0.74, over a normal that is unit to three places
                Assert.InRange(r, 0.738f, 1f + 1e-5f);
                Assert.Equal(1f, n.Length(), 3);
                Assert.True(Vector3.Dot(n, d) > 0f, "the normal faces out");
                deepest = Math.Min(deepest, r);
                shallowest = Math.Max(shallowest, r);
            }

            Assert.True(deepest < 0.9f, $"something is carved: deepest {deepest}");
            Assert.True(shallowest > 0.99f, $"something is left whole: shallowest {shallowest}");
        }

        [Fact]
        public void TheIceBlockTouchesTheSphereAtItsCornersAndSitsInsideAtItsFaces()
        {
            PotatoKindGeometry.IceBlockShape(Direction(1, 1, 1), out float corner, out Vector3 cornerNormal);
            PotatoKindGeometry.IceBlockShape(Vector3.Up, out float face, out Vector3 faceNormal);
            PotatoKindGeometry.IceBlockShape(Direction(1, 1, 0), out float edge, out _);

            Assert.Equal(1f, corner, 3);
            Assert.Equal(MathF.Pow(3f, 1f / 4.5f - 0.5f), face, 3);
            Assert.InRange(edge, face, corner);
            Assert.Equal(Vector3.Up, faceNormal);
            Assert.Equal(1f, Vector3.Dot(cornerNormal, Direction(1, 1, 1)), 3);
        }

        [Fact]
        public void ADisplacedSphereKeepsTheSpheresCountsAndItsWinding()
        {
            (VertexPositionNormalTexture[] vertices, short[] indices) = PotatoKindGeometry.Displaced(0.5f, 24, 12, PotatoKindGeometry.IceBlockShape);

            Assert.Equal((12 - 1) * 24 + 2, vertices.Length);
            Assert.Equal((24 * 2 + (12 - 2) * 24 * 2) * 3, indices.Length);
            AssertWoundClockwiseFromOutside(vertices, indices);

            (vertices, indices) = PotatoKindGeometry.Displaced(0.5f, 64, 32,
                (Vector3 d, out float r, out Vector3 n) => PotatoKindGeometry.RockShape(d, 0.2f, out r, out n));
            AssertWoundClockwiseFromOutside(vertices, indices);
        }

        [Fact]
        public void TheStudsStandPastTheCasingOnTheEightDiagonalsAndAreWoundOutward()
        {
            (VertexPositionNormalTexture[] vertices, short[] indices) = PotatoKindGeometry.MineStuds(0.5f);

            Assert.Equal(8 * (8 * 3 + 1), vertices.Length);
            Assert.Equal(8 * 24 * 3, indices.Length);

            float highest = 0f, lowest = float.MaxValue;
            foreach (VertexPositionNormalTexture vertex in vertices)
            {
                float r = vertex.Position.Length() / 0.5f;
                highest = Math.Max(highest, r);
                lowest = Math.Min(lowest, r);
                Assert.Equal(1f, vertex.Normal.Length(), 3);

                //Every vertex within the stud's angular radius of one of the eight diagonals
                Vector3 d = Vector3.Normalize(vertex.Position);
                float nearest = MathF.Acos(Math.Clamp(Vector3.Dot(new Vector3(MathF.Abs(d.X), MathF.Abs(d.Y), MathF.Abs(d.Z)), Vector3.Normalize(Vector3.One)), -1f, 1f));
                Assert.True(nearest <= PotatoKindGeometry.MINE_STUD_RADIUS + 1e-3f, $"a stud vertex {nearest} off every diagonal");
            }

            Assert.Equal(1f + PotatoKindGeometry.MINE_STUD_RISE, highest, 3);
            Assert.Equal(1f, lowest, 3);
            AssertWoundClockwiseFromOutside(vertices, indices);
        }

        private static void AssertWoundClockwiseFromOutside(VertexPositionNormalTexture[] vertices, short[] indices)
        {
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]].Position, b = vertices[indices[i + 1]].Position, c = vertices[indices[i + 2]].Position;
                Vector3 cross = Vector3.Cross(b - a, c - a);
                if (cross.LengthSquared() < 1e-12f) continue;

                Vector3 outward = vertices[indices[i]].Normal + vertices[indices[i + 1]].Normal + vertices[indices[i + 2]].Normal;
                Assert.True(Vector3.Dot(cross, outward) < 0f, $"triangle {i / 3} is wound counter-clockwise seen from outside");
            }
        }
    }
}
