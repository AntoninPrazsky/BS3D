using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>The sky figures' geometry (#829): the sphere and the star batch, with no device.</summary>
    public class PotatoSkyFigureTests
    {
        [Fact]
        public void TheSphereHasItsRingsAndSegmentsAtItsRadius()
        {
            (VertexPositionColor[] vertices, int[] indices) = PotatoSkyFigure.BuildSphere(8, 16, 12.5f);

            Assert.Equal(9 * 17, vertices.Length);
            Assert.Equal(8 * 16 * 6, indices.Length);
            foreach (VertexPositionColor vertex in vertices) Assert.Equal(12.5f, vertex.Position.Length(), 3);
            foreach (int index in indices) Assert.InRange(index, 0, vertices.Length - 1);
        }

        [Fact]
        public void TheStarsAreFourCornersEachOnTheSphereMostlyFaint()
        {
            VertexPositionColorTexture[] stars = PotatoSkyFigure.BuildStars(300, 7, null, 0f);

            Assert.Equal(300 * 4, stars.Length);
            int faint = 0;
            for (int i = 0; i < stars.Length; i += 4)
            {
                Assert.Equal(stars[i].Position, stars[i + 1].Position);
                Assert.Equal(stars[i].Position, stars[i + 3].Position);
                Assert.Equal(12.5f, stars[i].Position.Length(), 2);
                Assert.Equal(new Vector2(-1f, -1f), stars[i].TextureCoordinate);
                Assert.Equal(new Vector2(1f, 1f), stars[i + 2].TextureCoordinate);
                Assert.True(stars[i].Color.A > 0);
                if (stars[i].Color.A < 128) faint++;
            }
            Assert.True(faint > 150, $"most stars are faint, {faint} of 300 here");
        }

        [Fact]
        public void ABandThickensTheFieldAlongItsAxis()
        {
            Vector3 axis = Vector3.Up;
            VertexPositionColorTexture[] plain = PotatoSkyFigure.BuildStars(2000, 3, null, 0f);
            VertexPositionColorTexture[] banded = PotatoSkyFigure.BuildStars(2000, 3, axis, 0.75f);

            static float InBand(VertexPositionColorTexture[] stars, Vector3 axis)
            {
                int within = 0;
                for (int i = 0; i < stars.Length; i += 4)
                    if (MathF.Abs(Vector3.Dot(Vector3.Normalize(stars[i].Position), axis)) < 0.15f) within++;
                return (float)within / (stars.Length / 4);
            }

            Assert.True(InBand(banded, axis) > InBand(plain, axis) * 1.8f, "the band holds far more of the stars");
        }

        [Fact]
        public void OnlyTheSceneWithALookHasAFigure()
        {
            Assert.Null(PotatoSceneRig.SkyLookOf(SceneKind.Savanna));
            Assert.NotNull(PotatoSceneRig.SkyLookOf(SceneKind.Space));
        }
    }
}
