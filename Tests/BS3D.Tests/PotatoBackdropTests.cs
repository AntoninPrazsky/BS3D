using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using System.Linq;
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

        /// <summary>
        /// The aurora's stand on the Potato path (#829): a spruce is a stem and a cone in its variant's proportions, the
        /// cone's skirt on the stem's top and its tip at the trunk's and crown's height, through the planted world; the
        /// spire stands on the stand's long bare stem. Seen to fail: the stem's foot read at the unit column's full width
        /// (its radius is 0.5), which the last line caught.
        /// </summary>
        [Fact]
        public void ASpruceProxyStandsItsCrownOnItsStemInTheVariantsProportions()
        {
            ForestTreeConfig trees = new AuroraSceneConfig().Terrain.Trees;
            ForestScatterRenderer.SpruceVariant spire = ForestScatterRenderer.CONIFER_VARIANTS[^1];
            Assert.True(spire.Stand);
            Matrix planted = Matrix.CreateScale(1.3f) * Matrix.CreateTranslation(10f, -3f, 4f);

            (Matrix stem, Matrix crown) = PotatoBackdrop.SpruceProxies(trees, spire, planted);

            float trunk = trees.ConiferTrunkHeight * spire.Height * trees.ConiferStandTrunk * 1.3f;
            Vector3 stemTop = Vector3.Transform(Vector3.Up, stem);
            Vector3 skirt = Vector3.Transform(Vector3.Right, crown);
            Vector3 tip = Vector3.Transform(Vector3.Up, crown);
            Assert.Equal(-3f + trunk, stemTop.Y, 3);
            Assert.Equal(-3f + trunk, skirt.Y, 3);
            Assert.Equal(10f + trees.ConiferCrownRadius * spire.Width * 1.3f, skirt.X, 3);
            Assert.Equal(-3f + trunk + trees.ConiferCrownHeight * spire.Height * 1.3f, tip.Y, 3);
            //The column's foot is at radius 0.5 of its unit mesh
            Assert.Equal(trees.TrunkBaseRadius * spire.Width * 1.3f, Vector3.Transform(new Vector3(0.5f, 0f, 0f), stem).X - 10f, 3);
        }

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
        public void TheShellFacesItsInsideAndClosesItsTop()
        {
            (VertexPositionNormalTexture[] vertices, int[] indices) = PotatoShellMesh.Build(100f, 100f, -30f, 90f, 12);

            Assert.Equal(12 * 3 + 1, vertices.Length);
            Assert.Equal(12 * 3 * 3, indices.Length);
            for (int i = 0; i < indices.Length; i += 3)
            {
                VertexPositionNormalTexture a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                Vector3 cross = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);
                //Front-facing for an eye on the normal's side: the cross points away from it, as the grid's points down
                Assert.True(Vector3.Dot(cross, a.Normal) < 0f, $"triangle {i / 3} faces away from the inside");
            }

            //The wall's normals point at the axis, the ceiling's down
            for (int i = 0; i < 24; i++) Assert.True(Vector3.Dot(vertices[i].Normal, new Vector3(-vertices[i].Position.X, 0f, -vertices[i].Position.Z)) > 0f);
            for (int i = 24; i < vertices.Length; i++) Assert.Equal(Vector3.Down, vertices[i].Normal);
        }

        [Fact]
        public void AConeLeansItsNormalsInAndARakeLeansThemUpAndAFilterKeepsItsSegments()
        {
            //A canvas narrowing to its crown: seen from under it, the normal points in and down
            (VertexPositionNormalTexture[] canvas, int[] canvasIndices) = PotatoShellMesh.Build(96f, 9f, 12f, 70f, 16, i => i % 2 == 0, closeTop: false);
            Assert.Equal(16 * 2 * 3 / 2, canvasIndices.Length);
            for (int i = 0; i < 32; i++)
            {
                Vector3 n = canvas[i].Normal;
                Assert.True(Vector3.Dot(n, new Vector3(-canvas[i].Position.X, 0f, -canvas[i].Position.Z)) > 0f, "in");
                Assert.True(n.Y < 0f, "down");
            }
            for (int i = 0; i < canvasIndices.Length; i += 3)
            {
                VertexPositionNormalTexture a = canvas[canvasIndices[i]], b = canvas[canvasIndices[i + 1]], c = canvas[canvasIndices[i + 2]];
                Assert.True(Vector3.Dot(Vector3.Cross(b.Position - a.Position, c.Position - a.Position), a.Normal) < 0f, $"triangle {i / 3} faces away from the inside");
            }

            //A rake widening as it rises: in and up
            (VertexPositionNormalTexture[] rake, _) = PotatoShellMesh.Build(51f, 92f, -13.5f, 6f, 8, closeTop: false);
            for (int i = 0; i < 16; i++) Assert.True(rake[i].Normal.Y > 0f, "up");

            //A column seen from outside: out, its top up, and wound for the eye outside
            (VertexPositionNormalTexture[] column, int[] columnIndices) = PotatoShellMesh.Build(0.5f, 0.4f, 0f, 1f, 8, inside: false);
            for (int i = 0; i < 16; i++) Assert.True(Vector3.Dot(column[i].Normal, new Vector3(column[i].Position.X, 0f, column[i].Position.Z)) > 0f, "out");
            for (int i = 16; i < column.Length; i++) Assert.Equal(Vector3.Up, column[i].Normal);
            for (int i = 0; i < columnIndices.Length; i += 3)
            {
                VertexPositionNormalTexture a = column[columnIndices[i]], b = column[columnIndices[i + 1]], c = column[columnIndices[i + 2]];
                Assert.True(Vector3.Dot(Vector3.Cross(b.Position - a.Position, c.Position - a.Position), a.Normal) < 0f, $"triangle {i / 3} faces away from the outside");
            }
        }

        [Fact]
        public void AGridLineThroughTheHoleIsTwoPiecesOutsideIt()
        {
            Assert.Equal(new[] { (-100f, 100f) }, PotatoBackdrop.LineSpans(30f, 24f, 100f).ToArray());
            (float From, float To)[] cut = PotatoBackdrop.LineSpans(0f, 24f, 100f).ToArray();
            Assert.Equal(2, cut.Length);
            Assert.Equal((-100f, -24f), cut[0]);
            Assert.Equal((24f, 100f), cut[1]);
            (float From, float To)[] off = PotatoBackdrop.LineSpans(16f, 20f, 100f).ToArray();
            Assert.Equal(12f, off[1].From, 3);
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
