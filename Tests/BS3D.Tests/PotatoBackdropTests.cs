using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The Potato path's ground (#829): a grid over a terrain's mirror with the arena's hole and the sphere's winding
    /// rule. No device; the geometry alone.
    /// </summary>
    public class PotatoBackdropTests
    {
        private static float Bowl(float x, float z) => 0.001f * (x * x + z * z);

        [Fact]
        public void TheGridLeavesTheArenasHoleAndIsWoundClockwiseFromAbove()
        {
            (VertexPositionNormalTexture[] vertices, int[] indices) = PotatoTerrainMesh.Build(16, 80f, 24f, Bowl);

            Assert.Equal(17 * 17, vertices.Length);
            Assert.True(indices.Length < 16 * 16 * 6, "some quads are left out for the hole");
            Assert.True(indices.Length > 16 * 16 * 6 * 3 / 4, "most quads are drawn");

            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]].Position, b = vertices[indices[i + 1]].Position, c = vertices[indices[i + 2]].Position;
                Vector3 cross = Vector3.Cross(b - a, c - a);
                Assert.True(cross.Y < 0f, $"triangle {i / 3} is wound counter-clockwise seen from above");

                //No triangle lies wholly inside the hole
                Assert.True(Array.Exists(new[] { a, b, c }, p => p.X * p.X + p.Z * p.Z > 24f * 24f), $"triangle {i / 3} is inside the hole");
            }
        }

        [Fact]
        public void AFilterSplitsTheGridWithoutLosingOrDoublingATriangle()
        {
            //The mountains' snow and rock: two grids over one field, every triangle in exactly one of them
            static bool Steep(Vector3 a, Vector3 b, Vector3 c) => MathF.Abs(Vector3.Normalize(Vector3.Cross(b - a, c - a)).Y) < 0.98f;

            (_, int[] all) = PotatoTerrainMesh.Build(16, 80f, 24f, Bowl);
            (_, int[] steep) = PotatoTerrainMesh.Build(16, 80f, 24f, Bowl, Steep);
            (_, int[] flat) = PotatoTerrainMesh.Build(16, 80f, 24f, Bowl, (a, b, c) => !Steep(a, b, c));

            Assert.True(steep.Length > 0 && flat.Length > 0, "a bowl has both");
            Assert.Equal(all.Length, steep.Length + flat.Length);
        }

        [Fact]
        public void TheStripIsAQuadAStepAndIsWoundClockwiseFromAbove()
        {
            var left = new Vector3[5];
            var right = new Vector3[5];
            for (int i = 0; i < 5; i++)
            {
                left[i] = new Vector3(-1f, 0.1f * i, i * 3f);
                right[i] = new Vector3(1f, 0.1f * i, i * 3f);
            }

            (VertexPositionNormalTexture[] vertices, int[] indices) = PotatoStripMesh.Build(left, right);

            Assert.Equal(10, vertices.Length);
            Assert.Equal(4 * 6, indices.Length);
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]].Position, b = vertices[indices[i + 1]].Position, c = vertices[indices[i + 2]].Position;
                Assert.True(Vector3.Cross(b - a, c - a).Y < 0f, $"triangle {i / 3} is wound counter-clockwise seen from above");
            }

            Assert.Throws<ArgumentException>(() => PotatoStripMesh.Build(left, new Vector3[3]));
        }

        [Fact]
        public void OpenedKeepsEveryWorldAndOpensEveryOccluder()
        {
            var instances = new ModelInstance[3];
            for (int i = 0; i < 3; i++) instances[i] = new ModelInstance(Matrix.CreateTranslation(i, 2 * i, 3 * i), new Vector4(0.7f, 0f, 0f, 0f));

            PotatoBackdrop.Opened(instances);

            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(Matrix.CreateTranslation(i, 2 * i, 3 * i), instances[i].World);
                Assert.Equal(new Vector4(0f, 0f, 0f, 1f), instances[i].Custom);
            }
        }

        [Fact]
        public void TheNormalsFollowTheHeightAndPointUp()
        {
            (VertexPositionNormalTexture[] vertices, _) = PotatoTerrainMesh.Build(8, 40f, 0f, Bowl);

            foreach (VertexPositionNormalTexture vertex in vertices)
            {
                Assert.Equal(1f, vertex.Normal.Length(), 3);
                Assert.True(vertex.Normal.Y > 0f, "up");
                Assert.Equal(Bowl(vertex.Position.X, vertex.Position.Z), vertex.Position.Y, 4);

                //A bowl's normal leans back towards the centre
                if (vertex.Position.X > 1f) Assert.True(vertex.Normal.X < 0f);
            }
        }
    }
}
