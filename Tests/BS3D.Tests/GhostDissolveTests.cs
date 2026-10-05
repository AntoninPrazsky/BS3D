using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The aim ghost's size, carried in the dissolve channel below -1 (#794), and the arrangement the Potato draw makes
    /// of a ball bucket from it. The shaders are not run here; what is held is the encoding they decode and the one
    /// property every pixel shader's clip leans on - that a value under -1 keeps every pixel.
    /// </summary>
    public class GhostDissolveTests
    {
        [Theory]
        [InlineData(0.28f)]
        [InlineData(0.5f)]
        [InlineData(0.72f)]
        [InlineData(1f)]
        public void AGhostRoundTripsItsScale(float scale)
        {
            float dissolve = ModelInstance.GhostDissolve(scale);

            Assert.True(ModelInstance.IsGhost(dissolve));
            Assert.Equal(scale, ModelInstance.GhostScale(dissolve), 5);
        }

        [Fact]
        public void TheSmallestGhostIsStillAGhostAndNeverTheDithersEnd()
        {
            //-1 is the arriving dither at its end - a whole ball - so a ghost of radius 0 must not be encoded as it
            foreach (float scale in new[] { 0f, -3f, float.NegativeInfinity })
            {
                float dissolve = ModelInstance.GhostDissolve(scale);

                Assert.True(ModelInstance.IsGhost(dissolve), $"scale {scale} became {dissolve}");
                Assert.Equal(ModelInstance.GHOST_MIN_SCALE, ModelInstance.GhostScale(dissolve), 5);
            }

            Assert.Equal(1f, ModelInstance.GhostScale(ModelInstance.GhostDissolve(7f)), 5);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(0.5f)]
        [InlineData(1f)]
        [InlineData(-0.5f)]
        [InlineData(-1f)]
        public void NoDitherValueIsAGhostAndEveryOneIsWholeSized(float dissolve)
        {
            Assert.False(ModelInstance.IsGhost(dissolve));
            Assert.Equal(1f, ModelInstance.GhostScale(dissolve));
        }

        [Fact]
        public void EveryDesktopClipKeepsAllPixelsOfAGhost()
        {
            //BallCommon.fxh's contract: every style's pixel shader clips with this expression, over a hash that is
            //never negative and stays under 1. A ghost needs no change in any of them only while this holds.
            for (float scale = ModelInstance.GHOST_MIN_SCALE; scale <= 1f; scale += 0.01f)
            {
                float dissolve = ModelInstance.GhostDissolve(scale);

                for (float noise = 0f; noise < 1f; noise += 0.01f)
                {
                    float clip = dissolve >= 0 ? noise - dissolve : -dissolve - noise;

                    Assert.True(clip >= 0f, $"scale {scale}, noise {noise}: the clip would discard (clip < 0)");
                }
            }
        }

        [Theory]
        [InlineData(0f, false)]
        [InlineData(0.5f, true)]
        [InlineData(-0.5f, true)]
        [InlineData(1f, true)]
        [InlineData(-1f, true)]
        public void OnlyADitheredBallNeedsTheClip(float dissolve, bool needs) =>
            Assert.Equal(needs, InstancedModelRenderer.NeedsDither(dissolve));

        [Fact]
        public void AGhostNeedsNoClipEither() =>
            Assert.False(InstancedModelRenderer.NeedsDither(ModelInstance.GhostDissolve(0.5f)));

        private static ModelInstance Ball(float x, float dissolve) =>
            new(Matrix.CreateTranslation(x, 0f, 0f), Vector4.Zero, dissolve);

        [Fact]
        public void ThePotatoOrderPutsTheCleanBallsFirstNearestFirstAndTheDitheredAfterThem()
        {
            //Eye at the origin, so the distance is |x|. Mixed on purpose: a dithered one in front, a ghost, a far clean
            //ball and a near one, a cross-fade's pair at both signs
            ModelInstance[] source =
            {
                Ball(5f, 0.5f),                                  //dithered, going
                Ball(30f, 0f),                                   //clean, far
                Ball(2f, ModelInstance.GhostDissolve(0.5f)),     //ghost: clean, nearest
                Ball(9f, -0.25f),                                //dithered, arriving
                Ball(10f, 0f),                                   //clean, middle
                Ball(7f, 0f),                                    //clean, near
            };
            ModelInstance[] ordered = new ModelInstance[source.Length];
            float[] depths = new float[source.Length];

            int clean = InstancedModelRenderer.OrderForPotato(source, source.Length, Vector3.Zero, ref ordered, ref depths);

            Assert.Equal(4, clean);
            Assert.Equal(new[] { 2f, 7f, 10f, 30f }, new[] { ordered[0].World.M41, ordered[1].World.M41, ordered[2].World.M41, ordered[3].World.M41 });
            //The dithered ones keep the order they came in
            Assert.Equal(new[] { 5f, 9f }, new[] { ordered[4].World.M41, ordered[5].World.M41 });
            Assert.All(new[] { 4, 5 }, i => Assert.True(InstancedModelRenderer.NeedsDither(ordered[i].Dissolve)));
            Assert.All(new[] { 0, 1, 2, 3 }, i => Assert.False(InstancedModelRenderer.NeedsDither(ordered[i].Dissolve)));
        }

        [Fact]
        public void ThePotatoOrderOnlyUsesTheFirstCountAndKeepsTheSourceAsItWas()
        {
            ModelInstance[] source = { Ball(3f, 0f), Ball(1f, 0.5f), Ball(2f, 0f), Ball(99f, 0f) };
            ModelInstance[] copy = (ModelInstance[])source.Clone();
            ModelInstance[] ordered = Array.Empty<ModelInstance>();
            float[] depths = Array.Empty<float>();

            int clean = InstancedModelRenderer.OrderForPotato(source, 3, Vector3.Zero, ref ordered, ref depths);

            Assert.Equal(2, clean);
            Assert.Equal(new[] { 2f, 3f, 1f }, new[] { ordered[0].World.M41, ordered[1].World.M41, ordered[2].World.M41 });
            //The buffers grew to the source's length, and the source is the caller's, untouched
            Assert.True(ordered.Length >= source.Length);
            Assert.Equal(copy.Length, source.Length);
            for (int i = 0; i < source.Length; i++) Assert.Equal(copy[i].World.M41, source[i].World.M41);
        }

        [Fact]
        public void ThePotatoOrderOfNothingOrOfOneIsTrivial()
        {
            ModelInstance[] source = { Ball(4f, 0.5f) };
            ModelInstance[] ordered = Array.Empty<ModelInstance>();
            float[] depths = Array.Empty<float>();

            Assert.Equal(0, InstancedModelRenderer.OrderForPotato(source, 0, Vector3.Zero, ref ordered, ref depths));
            Assert.Equal(0, InstancedModelRenderer.OrderForPotato(source, 1, Vector3.Zero, ref ordered, ref depths));
            Assert.Equal(4f, ordered[0].World.M41);
        }
    }
}
