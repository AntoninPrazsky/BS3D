using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The terrain grids' two triangle orders (#781, <see cref="TerrainGridCache.Indices"/>): the row order every grid has
    /// always had, and the near-first order the mountain's High and Ultra grid draws in so the depth test can reject a
    /// hidden ridge before it is shaded. The near-first order must hold exactly the row order's triangles - a quad left
    /// out is a hole in the range, a quad drawn twice a seam - and must run outward from the centre, which is where the
    /// camera stands.
    /// </summary>
    public class TerrainGridOrderTests
    {
        [Theory]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(61)]
        [InlineData(720)]
        public void NearFirstHoldsEveryTriangleOfTheRowOrderOnce(int n)
        {
            int[] rows = TerrainGridCache.Indices(n, nearFirst: false);
            int[] rings = TerrainGridCache.Indices(n, nearFirst: true);

            Assert.Equal(rows.Length, rings.Length);
            Assert.Equal(Triangles(rows).OrderBy(t => t).ToArray(), Triangles(rings).OrderBy(t => t).ToArray());
        }

        [Theory]
        [InlineData(5)]
        [InlineData(61)]
        [InlineData(720)]
        public void NearFirstRunsOutwardFromTheCentre(int n)
        {
            int[] rings = TerrainGridCache.Indices(n, nearFirst: true);
            int centre = (n - 2) / 2;

            int previous = 0;
            for (int quad = 0; quad < rings.Length / 6; quad++)
            {
                //The first corner of a quad's first triangle is its lower-left vertex, z * n + x
                int a = rings[quad * 6];
                int ring = Math.Max(Math.Abs(a % n - centre), Math.Abs(a / n - centre));

                if (quad == 0) Assert.Equal(0, ring);
                Assert.True(ring >= previous, $"quad {quad} is in ring {ring} after a quad in ring {previous}");
                previous = ring;
            }
        }

        [Fact]
        public void TheRowOrderIsTheOneTheGridsWereMeasuredWith()
        {
            //Low and Medium's grids, and every other scene's, keep this order to the index: two triangles a quad, row by row
            int n = 4;
            var expected = new List<int>();
            for (int z = 0; z < n - 1; z++)
                for (int x = 0; x < n - 1; x++)
                {
                    int a = z * n + x, b = a + 1, c = a + n, d = c + 1;
                    expected.AddRange(new[] { a, c, b, b, c, d });
                }

            Assert.Equal(expected.ToArray(), TerrainGridCache.Indices(n, nearFirst: false));
        }

        private static IEnumerable<(int, int, int)> Triangles(int[] indices)
        {
            for (int i = 0; i < indices.Length; i += 3)
                yield return (indices[i], indices[i + 1], indices[i + 2]);
        }
    }
}
