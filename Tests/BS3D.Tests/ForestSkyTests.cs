using Microsoft.Xna.Framework;
using Prazsky.Core;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The forest's sky (#1165). The owner: "the sky at the horizon is very purple; in reality it would not be". Its dome drew a
    /// violet skyline (displayed hue about 275 degrees) under an indigo-violet zenith that the overcast lid is tinted by, where
    /// every reference of a real dusk over a spruce forest draws a blue-grey lid over a pale, nearly neutral strip at the
    /// horizon. What can be held without a frame: the dome as it is drawn, through the resolve's exposure and per-channel ACES,
    /// is blue-grey and quiet from the skyline up through the band the lid closes over, brightest at the skyline; the lid's
    /// tint, the dome's zenith, is blue-grey too; and the light rig keeps the horizon colour the wood was graded under. How it
    /// looks is the owner's eye.
    /// </summary>
    public class ForestSkyTests
    {
        private static readonly int Dome = SkyDome.SceneDefault(SceneKind.Forest);

        //The band a player sees of the dome: from the lowest direction the far fade reads (FarSky's first slot, 0.1 under the
        //skyline) up past the height where the overcast lid has closed over it (CloudField.HORIZON_FADE)
        private const float BAND_LOW = -0.10f;
        private const float BAND_HIGH = 0.30f;
        private const float BAND_STEP = 0.01f;

        [Fact]
        public void TheSkylineIsBlueGreyNotViolet()
        {
            //The issue's measure: the displayed hue at the skyline under about 240 degrees (it was 275), and not round the
            //wheel into green or warm either; across the whole band the eye sees
            for (float y = BAND_LOW; y <= BAND_HIGH + 1e-4f; y += BAND_STEP)
            {
                (float hue, float saturation) = HueOf(Displayed(SkyDome.DrawnLinearOf(Dome, y)));
                Assert.True(saturation < 0.03f || (hue > 195f && hue < 235f),
                    $"at direction.y {y:0.00} the forest's sky is shown at hue {hue:0} (saturation {saturation:0.00}), not a blue-grey");
            }
        }

        [Fact]
        public void NoGaudyBandAtTheSkyline()
        {
            //Quiet: no saturated colour anywhere in the band (the violet it replaced was shown at 0.43 to 0.66 here; the
            //references' lids at 0.1 to 0.3)...
            for (float y = BAND_LOW; y <= BAND_HIGH + 1e-4f; y += BAND_STEP)
            {
                (_, float saturation) = HueOf(Displayed(SkyDome.DrawnLinearOf(Dome, y)));
                Assert.True(saturation < 0.35f, $"at direction.y {y:0.00} the forest's sky is shown at saturation {saturation:0.00}");
            }

            //...and no stripe: from the skyline up the sky only darkens, the strip under the cloud base the brightest of it
            float previous = float.MaxValue;
            for (float y = 0f; y <= BAND_HIGH + 1e-4f; y += BAND_STEP)
            {
                float luminance = ColorSpace.Luminance(SkyDome.DrawnLinearOf(Dome, y));
                Assert.True(luminance <= previous + 1e-4f, $"the forest's sky brightens again at direction.y {y:0.00}");
                previous = luminance;
            }
        }

        [Fact]
        public void TheLidIsTintedBlueGreyNotViolet()
        {
            //The overcast lid's underside is the weather's shadow colour times the dome's zenith (CloudField.ApplyDome), and over
            //the ridges the play camera looks at it is most of the sky drawn: an indigo-violet zenith made a violet lid
            Vector3 zenith = ColorSpace.SrgbToLinear(SkyDome.RigColoursOf(Dome).Zenith);
            (float hue, float saturation) = HueOf(Displayed(zenith));
            Assert.InRange(hue, 205f, 235f);
            Assert.True(saturation < 0.6f, $"the forest's zenith is shown at saturation {saturation:0.00}");
        }

        [Fact]
        public void TheLightRigKeepsTheHorizonAndTheZenithsBrightness()
        {
            //The rig's horizon (the sun's tint and the ground's bounce, the average of the capture's bottom fifth) is the one the
            //wood's twilight grade (#1121) was made under: the re-ramp leaves those vertices as they were
            Vector3 horizon = SkyDome.RigColoursOf(Dome).Horizon;
            Assert.Equal(new Vector3(66f, 145f, 152f), Round(horizon * 255f));

            //And the zenith, the hemisphere's light from above, keeps its brightness while it loses the violet: the wood, the
            //island and the balls are not lit brighter or darker than before, only less purple
            float luminance = ColorSpace.Luminance(ColorSpace.SrgbToLinear(SkyDome.RigColoursOf(Dome).Zenith));
            Assert.InRange(luminance, 0.040f, 0.045f);
        }

        //The resolve's own exit from linear light (Tonemap.fx): the exposure, Narkowicz's ACES fit per channel, the sRGB curve
        private static Vector3 Displayed(Vector3 linear)
        {
            static float Map(float x)
            {
                x *= PostProcessPipeline.DEFAULT_EXPOSURE;
                return ColorSpace.LinearToSrgb(Math.Clamp(x * (2.51f * x + 0.03f) / (x * (2.43f * x + 0.59f) + 0.14f), 0f, 1f));
            }

            return new Vector3(Map(linear.X), Map(linear.Y), Map(linear.Z));
        }

        //Hue in degrees and HSV saturation of a displayed colour
        private static (float Hue, float Saturation) HueOf(Vector3 c)
        {
            float max = MathF.Max(c.X, MathF.Max(c.Y, c.Z)), min = MathF.Min(c.X, MathF.Min(c.Y, c.Z)), span = max - min;
            if (span <= 0f) return (0f, 0f);

            float hue = max == c.X ? (c.Y - c.Z) / span : max == c.Y ? 2f + (c.Z - c.X) / span : 4f + (c.X - c.Y) / span;
            return ((hue * 60f + 360f) % 360f, span / max);
        }

        private static Vector3 Round(Vector3 v) => new(MathF.Round(v.X), MathF.Round(v.Y), MathF.Round(v.Z));
    }
}
