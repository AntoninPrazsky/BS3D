using Microsoft.Xna.Framework;
using Prazsky.Core;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The sea's sky and water (#1171). The owner: "the sea and also the sky ... seem to me too blue ... the sea is actually
    /// darker, especially when it is overcast". The sea stood under dome 13, whose saturated blue-violet zenith the water
    /// multiplies and mirrors (Sea.fx: the body is a reflectance times (zenith + horizon) / 2, and a grazing look mirrors that
    /// same mean), and the references of a real dusk sea under broken cloud draw a grey-blue sky over slate water. What can be
    /// held without a frame: the sea brings a dome of its own and leaves 13 to the scenes that stand under it; that dome, as the
    /// resolve shows it, is a quiet grey-blue from the skyline up through the band the play camera sees, its zenith too; the
    /// light the water takes from it is near grey where 13's was blue; the sun stands where 13's stood and is tinted warm, not
    /// teal, at the strength it had. How it looks is the owner's eye.
    /// </summary>
    public class SeaSkyTests
    {
        private static readonly int Dome = SkyDome.SceneDefault(SceneKind.Sea);

        //The dome the sea stood under until #1171, the measure every figure here is held against
        private const int OLD_DOME = 13;

        //The band of the dome a player sees over the sea: from the lowest direction the far fade reads (FarSky's first slot) up
        //past the height the play camera's frame reaches
        private const float BAND_LOW = -0.10f;
        private const float BAND_HIGH = 0.50f;
        private const float BAND_STEP = 0.01f;

        [Fact]
        public void TheSeaBringsItsOwnDomeAndLeavesThirteenToTheRest()
        {
            //13 is the icesheet's, the Game's own default and sixty levels' (aurora, cavern, big top, Grid, Moon, space):
            //re-ramped in place, every one of them would have been relit with the sea
            Assert.NotEqual(OLD_DOME, Dome);
            Assert.Equal(OLD_DOME, SkyDome.SceneDefault(SceneKind.Polar));
            Assert.InRange(Dome, 1, SkyDome.Count);
        }

        [Fact]
        public void TheSkyIsAQuietGreyBlueNotViolet()
        {
            //Seen to fail on dome 13: shown at hue 213-253 and saturation up to 0.43 across this band. The references' open
            //sky: hue 205-228 at saturation 0.1-0.16 in the top band, over a strip at the skyline that is near neutral away from
            //the sun and warm by it; a dome has no azimuth, so the strip is a faint warmth all round, only at the skyline
            for (float y = BAND_LOW; y <= BAND_HIGH + 1e-4f; y += BAND_STEP)
            {
                (float hue, float saturation) = HueOf(Displayed(SkyDome.DrawnLinearOf(Dome, y)));
                Assert.True(saturation < 0.20f, $"at direction.y {y:0.00} the sea's sky is shown at saturation {saturation:0.00}");

                bool greyBlue = hue > 205f && hue < 230f, warmStrip = y <= 0.05f && hue > 15f && hue < 40f && saturation < 0.10f;
                Assert.True(saturation < 0.06f || greyBlue || warmStrip,
                    $"at direction.y {y:0.00} the sea's sky is shown at hue {hue:0} (saturation {saturation:0.00}), not a grey-blue");
            }
        }

        [Fact]
        public void TheZenithIsSlateNotBlueViolet()
        {
            //The zenith is the light from above, the upper half of what the water mirrors and the tint of the deck's undersides
            //(CloudField.ApplyDome). Dome 13's is shown at hue 251 and saturation 0.78
            Vector3 zenith = ColorSpace.SrgbToLinear(SkyDome.RigColoursOf(Dome).Zenith);
            (float hue, float saturation) = HueOf(Displayed(zenith));
            Assert.InRange(hue, 205f, 230f);
            Assert.True(saturation < 0.35f, $"the sea's zenith is shown at saturation {saturation:0.00}");
        }

        [Fact]
        public void TheWaterTakesAGreyLightNotABlueOne()
        {
            //What the water is lit by and, at a grazing look, mirrors: (zenith + horizon) / 2 (Sea.fx's ambient, and its sky
            //reflection where the reflected ray is level). Under dome 13 its blue was 2.6 times its green, and the body - the
            //sea's reflectances times it - came out blue 3.7 times the green (the issue's arithmetic)
            Vector3 light = WaterLight(Dome);
            Assert.True(light.Z < 1.15f * light.Y, $"the sea's water is lit blue {light.Z / light.Y:0.00} times the green");
            Assert.True(WaterLight(OLD_DOME).Z > 2f * WaterLight(OLD_DOME).Y, "the measure no longer tells dome 13's blue");

            var sea = new SeaSceneConfig();
            Vector3 body = Body(sea, Dome), oldBody = Body(sea, OLD_DOME);
            Assert.True(body.Z < 1.5f * body.Y, $"the sea's body is blue {body.Z / body.Y:0.00} times the green");
            Assert.True(oldBody.Z > 3f * oldBody.Y, "the measure no longer tells dome 13's blue body");

            //...and darker by luminance, as the owner asked ("the sea is actually darker"), not only greyer: at most four fifths
            //of the body dome 13 lit with the old reflectances. Seen to fail with the reflectances undarkened (0.98 of it: the
            //new dome's light is as bright as 13's, so a greyer body alone is not a darker one)
            var oldSea = new SeaSceneConfig
            {
                WaterDeep = new Rgb(0.02f, 0.05f, 0.07f),
                WaterShallow = new Rgb(0.07f, 0.17f, 0.19f),
            };
            Vector3 mainBody = Body(oldSea, OLD_DOME);
            Assert.True(ColorSpace.Luminance(body) <= ColorSpace.Luminance(mainBody) * 0.8f,
                $"the sea's body is {ColorSpace.Luminance(body):0.0000} where it was {ColorSpace.Luminance(mainBody):0.0000}");
        }

        [Fact]
        public void TheSunStandsWhereItStoodAndIsWarm()
        {
            //The sea's prologue climbs to it and ends on its glitter path, and the platforms, the lighthouse and the vessels were
            //placed against it: the sun stands exactly where dome 13's did
            Assert.Equal(SkyDome.SunOf(OLD_DOME), SkyDome.SunOf(Dome));

            //Its tint is the rig's horizon, the bottom fifth's average (SkyLightRig: lerp(1, HorizonLinear, SKY_TINT_STRENGTH)).
            //13's teal turned the sun green (red under green); a dusk sun is warm, red over green over blue...
            Vector3 tint = KeyTint(Dome), oldTint = KeyTint(OLD_DOME);
            Assert.True(tint.X > tint.Y && tint.Y > tint.Z, $"the sea's sun is tinted {tint}");
            Assert.True(oldTint.X < oldTint.Y, "the measure no longer tells dome 13's green sun");

            //...at the strength it had, so the island, the gun and the balls are lit as bright as before
            Assert.InRange(ColorSpace.Luminance(tint), ColorSpace.Luminance(oldTint) * 0.95f, ColorSpace.Luminance(oldTint) * 1.05f);
        }

        [Fact]
        public void TheLightFromAboveKeepsItsBrightness()
        {
            //The zenith is the sky's ambient on every up-facing surface: within a tenth of 13's, so only its colour changed
            float luminance = ColorSpace.Luminance(ColorSpace.SrgbToLinear(SkyDome.RigColoursOf(Dome).Zenith));
            float old = ColorSpace.Luminance(ColorSpace.SrgbToLinear(SkyDome.RigColoursOf(OLD_DOME).Zenith));
            Assert.InRange(luminance, old * 0.9f, old * 1.1f);
        }

        //Sea.fx's ambient: the mean of the rig's zenith and horizon, linear
        private static Vector3 WaterLight(int dome)
        {
            (Vector3 zenith, Vector3 horizon) = SkyDome.RigColoursOf(dome);
            return (ColorSpace.SrgbToLinear(zenith) + ColorSpace.SrgbToLinear(horizon)) * 0.5f;
        }

        //Sea.fx's body at the open sea's mean mix of the shallow colour (the crest-keyed lerp's mean, 0.485, on a level face):
        //lerp(WaterColorDeep, WaterColorShallow, mix) * ambient + ZenithColor * 0.05
        private static Vector3 Body(SeaSceneConfig sea, int dome)
        {
            const float MEAN_MIX = 0.485f;
            Vector3 zenith = ColorSpace.SrgbToLinear(SkyDome.RigColoursOf(dome).Zenith);
            return Vector3.Lerp(sea.WaterDeep.ToVector3(), sea.WaterShallow.ToVector3(), MEAN_MIX) * WaterLight(dome) + zenith * 0.05f;
        }

        private static Vector3 KeyTint(int dome) =>
            Vector3.Lerp(Vector3.One, ColorSpace.SrgbToLinear(SkyDome.RigColoursOf(dome).Horizon), SkyLightRig.SKY_TINT_STRENGTH);

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
    }
}
