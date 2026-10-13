using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The kinds' looks on the Potato path (#863): what InstancedModelRenderer.PotatoKindLookFor gives each BallShading in
    /// place of the desktop's technique for it. The shaders are not run; what is held is the rule the issue states - an
    /// ordinary ball is untouched, and each special kind differs from it in the way its description says.
    /// </summary>
    public class PotatoKindLookTests
    {
        private static readonly Vector3 Navy = new(0.02f, 0.03f, 0.25f);
        private const float DEPTH = 0.6f;

        private static float Luminance(Vector3 c) => c.X * 0.2126f + c.Y * 0.7152f + c.Z * 0.0722f;

        [Fact]
        public void EveryShadingHasAFiniteLookInRange()
        {
            foreach (BallShading shading in Enum.GetValues<BallShading>())
            {
                InstancedModelRenderer.PotatoKindLook look = InstancedModelRenderer.PotatoKindLookFor(shading, Navy, DEPTH);

                foreach (float f in new[] { look.Casing.X, look.Casing.Y, look.Casing.Z, look.Emission.X, look.Emission.Y, look.Emission.Z })
                    Assert.True(float.IsFinite(f) && f >= 0f, $"{shading}: {f}");
                Assert.InRange(look.StillShare, 0f, 1f);
                Assert.InRange(look.BeatShare, 0f, 1f);
                Assert.True(look.Alpha > 0f && look.Alpha <= 1f, $"{shading}: alpha {look.Alpha}");
                Assert.True(look.ReflectanceScale > 0f, $"{shading}: reflectance scale {look.ReflectanceScale}");
                if (look.Roughness.HasValue) Assert.InRange(look.Roughness.Value, 0f, 1f);
            }
        }

        [Theory]
        [InlineData(BallShading.Vinyl)]
        [InlineData(BallShading.Bubble)]
        [InlineData(BallShading.Marble)]
        [InlineData(BallShading.Wool)]
        [InlineData(BallShading.Metal)]
        [InlineData(BallShading.Ice)]
        [InlineData(BallShading.Gem)]
        [InlineData(BallShading.Plasma)]
        [InlineData(BallShading.Lava)]
        [InlineData(BallShading.Porcelain)]
        [InlineData(BallShading.Stone)]
        [InlineData(BallShading.Wildcard)]
        public void AnOrdinaryBallIsItsColourUntouched(BallShading shading)
        {
            InstancedModelRenderer.PotatoKindLook look = InstancedModelRenderer.PotatoKindLookFor(shading, Navy, DEPTH);

            Assert.Equal(Navy, look.Casing);
            Assert.Equal(Navy, look.Emission);
            Assert.Equal(1f - DEPTH, look.StillShare, 5);
            Assert.Equal(DEPTH, look.BeatShare, 5);
            Assert.Null(look.Roughness);
            Assert.Equal(1f, look.ReflectanceScale);
            Assert.Equal(1f, look.Alpha);
            Assert.Equal(0f, look.ToneWeight);
            Assert.Equal(Navy, look.ToneAmbient(Navy));
        }

        [Fact]
        public void AToneMovesTheAmbientsHueAndKeepsItsPeak()
        {
            InstancedModelRenderer.PotatoKindLook frozen = InstancedModelRenderer.PotatoKindLookFor(BallShading.Frozen, Navy, DEPTH);
            Vector3 red = new(0.8f, 0.02f, 0.02f);
            Vector3 toned = frozen.ToneAmbient(red);

            Assert.True(frozen.ToneWeight > 0f);
            Assert.True(toned.Z > red.Z && toned.Y > red.Y, "pulled towards the ice: bluer and greener");
            Assert.True(toned.X < red.X, "and less red");
            Assert.InRange(Math.Max(toned.X, Math.Max(toned.Y, toned.Z)), 0.7f * red.X, red.X);
        }

        [Fact]
        public void TheBombBeatsRedAndRestsRedDark()
        {
            InstancedModelRenderer.PotatoKindLook look = InstancedModelRenderer.PotatoKindLookFor(BallShading.Bomb, new Vector3(0.01f), DEPTH);

            Assert.True(look.Emission.X > 5f * look.Emission.Y && look.Emission.X > 5f * look.Emission.Z, "the bomb's glow is red");
            Assert.True(look.StillShare > 0f, "a floor of red between the beats");
            Assert.True(look.BeatShare >= look.StillShare, "the beat is the brighter");
        }

        [Fact]
        public void TheZapIsColdAndTheAcidGreen()
        {
            Vector3 dark = new(0.02f);
            InstancedModelRenderer.PotatoKindLook zap = InstancedModelRenderer.PotatoKindLookFor(BallShading.Zap, dark, DEPTH);
            InstancedModelRenderer.PotatoKindLook acid = InstancedModelRenderer.PotatoKindLookFor(BallShading.Acid, dark, DEPTH);

            Assert.True(zap.Emission.Z > zap.Emission.X, "the zap's glow leans blue");
            Assert.True(acid.Emission.Y > 2f * Math.Max(acid.Emission.X, acid.Emission.Z), "the acid's glow is green");
        }

        [Fact]
        public void TheGlassShowsThroughAndTheIronIsMatte()
        {
            InstancedModelRenderer.PotatoKindLook glass = InstancedModelRenderer.PotatoKindLookFor(BallShading.Hollow, new Vector3(0.6f), DEPTH);
            InstancedModelRenderer.PotatoKindLook heavy = InstancedModelRenderer.PotatoKindLookFor(BallShading.Heavy, Navy, DEPTH);
            InstancedModelRenderer.PotatoKindLook frozen = InstancedModelRenderer.PotatoKindLookFor(BallShading.Frozen, Navy, DEPTH);

            Assert.True(glass.Alpha < 1f, "the glass is not opaque");
            Assert.Equal(0f, glass.BeatShare);
            Assert.Equal(1f, heavy.Roughness);
            Assert.True(heavy.ReflectanceScale < 1f, "iron does not mirror the sky");
            Assert.True(Luminance(heavy.Casing) < Luminance(Navy) + 0.05f && Luminance(heavy.Casing) > Luminance(Navy), "greyer: a dark navy goes up towards iron grey, not down");
            Assert.True(Luminance(frozen.Casing) > Luminance(Navy), "ice is paler than the colour in it");
            Assert.True(frozen.Roughness < 0.2f, "ice is glass-smooth");
        }

        [Fact]
        public void TheSpecialKindsDifferFromAnOrdinaryBall()
        {
            InstancedModelRenderer.PotatoKindLook plain = InstancedModelRenderer.PotatoKindLookFor(BallShading.Vinyl, Navy, DEPTH);

            foreach (BallShading shading in new[] { BallShading.Bomb, BallShading.Zap, BallShading.Acid, BallShading.Hollow,
                BallShading.Frozen, BallShading.Infectious, BallShading.Gravity, BallShading.Heavy })
            {
                InstancedModelRenderer.PotatoKindLook look = InstancedModelRenderer.PotatoKindLookFor(shading, Navy, DEPTH);
                Assert.NotEqual(plain, look);
            }
        }
    }
}
