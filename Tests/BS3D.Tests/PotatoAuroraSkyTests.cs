using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>The Pi's aurora curtains baked from the desktop's arcs (#1044), with no device.</summary>
    public class PotatoAuroraSkyTests
    {
        //A small bake: a column every 1.4 degrees, enough to find the curtains
        private const int WIDTH = 256, HEIGHT = 64;

        [Fact]
        public void TheCurtainsHangOverTheirBearingAndNotAtTheHorizon()
        {
            var config = new AuroraSceneConfig();
            Color[] texels = PotatoAuroraSky.Bake(config, WIDTH, HEIGHT, 1.1f);

            //The horizon's row is dark: the desktop's air fades every curtain to nothing there
            for (int column = 0; column < WIDTH; column++)
                Assert.True(texels[column].G < 24, $"column {column} lit at the horizon");

            //The brightest texel stands over the curtains' side of the sky (the bearing, 0 is +Z), green over red
            int best = 0;
            for (int i = 1; i < texels.Length; i++) if (texels[i].G > texels[best].G) best = i;
            Assert.True(texels[best].G > 150);
            Assert.True(texels[best].G > texels[best].R);
            float longitude = MathHelper.TwoPi * (best % WIDTH + 0.5f) / WIDTH;
            float bearing = MathHelper.ToRadians(config.Curtains.BearingDegrees);
            float towards = MathF.Cos(longitude) * MathF.Sin(bearing) + MathF.Sin(longitude) * MathF.Cos(bearing);
            Assert.True(towards > 0.3f, $"the brightest curtain stands {MathHelper.ToDegrees(MathF.Acos(towards)):F0} degrees off the bearing");
        }

        [Fact]
        public void TheAlphaTellsTheArcsApart()
        {
            Color[] texels = PotatoAuroraSky.Bake(new AuroraSceneConfig(), WIDTH, HEIGHT, 1.1f);
            bool first = false, last = false;
            foreach (Color texel in texels)
            {
                if (texel.G < 60) continue;
                first |= texel.A < 40;
                last |= texel.A > 215;
            }
            Assert.True(first && last);
        }

        [Fact]
        public void TheBreathsStayWithinThePulsesDepth()
        {
            var config = new AuroraSceneConfig();
            float floor = 1f - config.Curtains.PulseDepth - 1e-4f;
            for (float t = 0f; t < 120f; t += 0.7f)
            {
                Vector3 breaths = PotatoAuroraSky.ArcBreaths(config, t);
                foreach (float b in new[] { breaths.X, breaths.Y, breaths.Z }) Assert.InRange(b, floor, 1.0001f);
            }
        }
    }
}
