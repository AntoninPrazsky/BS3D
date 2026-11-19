using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The sea's cloud and its sun's glitter (#1172). The owner: "the cloud cover seems too large to me - it does not quite
    /// correspond to the sun's reflection on the water". Under Broken the deck stood cloud over nearly the whole sky, so the
    /// sun was behind cloud from almost everywhere, while the glint on the water took the light under the deck, floored at
    /// the weather's 0.3: a glitter path under a sky with no sun in it. What can be held without a frame: the sea stands under
    /// <see cref="WeatherPreset.Mixed"/>, Broken's clouds with about as much open sky, which leaves the sun's disc seen from
    /// nearly half the deck where Broken left it a twentieth; the heaps of #1145 still stand on its cloud; and
    /// <c>Sea.fx</c>'s glint and crest glow take the sun's beam, the cover over the sun times the deck's opacity with no floor,
    /// which is what the sky shader hides the disc behind. How the two read together is the owner's eye.
    /// </summary>
    public class SeaCloudCoverTests
    {
        //A patch of the deck many weather features across (a feature is about 1 / Scale = 520 units), sampled on a grid, at
        //several moments of the drift
        private const float PATCH = 6000f, STEP = 40f;
        private static readonly float[] Times = { 0f, 300f, 1200f, 2400f };

        private static CloudField Sky(WeatherPreset preset, float time)
        {
            var clouds = new CloudField();
            clouds.SetWeatherImmediately(preset);
            clouds.Time = time;
            return clouds;
        }

        //The shares of the deck where the sun's disc is more than half seen through the weather layer (the beam over a half:
        //1 - saturate(cover * opacity), Sky.fx's coverage without the detail octaves) and where it is not seen at all
        private static (float SunSeen, float SunHidden) Shares(WeatherPreset preset)
        {
            float opacity = WeatherLooks.Of(preset).Opacity;
            int seen = 0, hidden = 0, all = 0;
            foreach (float time in Times)
            {
                CloudField clouds = Sky(preset, time);
                for (float x = -PATCH / 2; x < PATCH / 2; x += STEP)
                    for (float z = -PATCH / 2; z < PATCH / 2; z += STEP)
                    {
                        float beam = 1f - MathHelper.Clamp(clouds.Cover(new Vector2(x, z)) * opacity, 0f, 1f);
                        if (beam > 0.5f) seen++;
                        if (beam <= 0f) hidden++;
                        all++;
                    }
            }

            return (seen / (float)all, hidden / (float)all);
        }

        [Fact]
        public void TheSeaStandsUnderMixedAndTheMountainsKeepBroken()
        {
            Assert.Equal(WeatherPreset.Mixed, new SeaSceneConfig().Weather);
            Assert.Equal(WeatherPreset.Broken, new MountainSceneConfig().Weather);
        }

        [Fact]
        public void MixedLeavesTheSunOutAboutAsOftenAsBehindCloud()
        {
            //Seen to fail with Mixed's bias at Broken's 0.30: the disc more than half seen from 0.05 of the deck, none of it
            //from 0.82. At 0.10 a numpy replica of the noise read 0.46-0.48 and 0.27-0.30
            (float seen, float hidden) = Shares(WeatherPreset.Mixed);
            Assert.InRange(seen, 0.40f, 0.55f);
            Assert.InRange(hidden, 0.20f, 0.36f);

            //and the measure tells the sky the sea had
            (float brokenSeen, float brokenHidden) = Shares(WeatherPreset.Broken);
            Assert.True(brokenSeen < 0.1f && brokenHidden > 0.7f, $"Broken: the sun seen from {brokenSeen:0.00}, hidden from {brokenHidden:0.00}");
        }

        [Fact]
        public void MixedIsBrokensCloudsFewerOfThem()
        {
            //Every figure Broken's but the bias, so a fade between them is a change of cover alone; the bias between
            //Scattered's and Broken's
            WeatherLook mixed = WeatherLooks.Of(WeatherPreset.Mixed), broken = WeatherLooks.Of(WeatherPreset.Broken);
            Assert.Equal(broken.PlaneY, mixed.PlaneY);
            Assert.Equal(broken.Scale, mixed.Scale);
            Assert.Equal(broken.Wind, mixed.Wind);
            Assert.Equal(broken.CoverageGain, mixed.CoverageGain);
            Assert.Equal(broken.ShadowFloor, mixed.ShadowFloor);
            Assert.Equal(broken.ShadowGain, mixed.ShadowGain);
            Assert.Equal(broken.DetailStrength, mixed.DetailStrength);
            Assert.Equal(broken.Opacity, mixed.Opacity);
            Assert.Equal(broken.FormStrength, mixed.FormStrength);
            Assert.Equal(broken.ShapeStrength, mixed.ShapeStrength);
            Assert.Equal(broken.CharacterStrength, mixed.CharacterStrength);
            Assert.Equal(broken.ShadowColor, mixed.ShadowColor);
            Assert.InRange(mixed.CoverageBias, WeatherLooks.Of(WeatherPreset.Scattered).CoverageBias + 0.01f, broken.CoverageBias - 0.01f);
        }

        [Fact]
        public void EverySkyIsWrittenAndReadByItsWord()
        {
            //A level file names its sky by a word: every preset's word reads back as itself (seen to fail with "mixed" left out
            //of TryParse, where a level naming it would stand under its scene's sky)
            foreach (WeatherPreset preset in Enum.GetValues<WeatherPreset>())
                Assert.Equal(preset, WeatherLooks.TryParse(WeatherLooks.NameOf(preset)));
        }

        [Fact]
        public void TheHeapsStillStandOnMixedCloud()
        {
            //#1145's heaps stand wherever the deck has cloud: fully under Mixed, fewer than under Broken, never none
            var lens = new Vector3(0f, -4f, 30f);
            foreach (float time in Times)
            {
                CloudField mixed = Sky(WeatherPreset.Mixed, time), broken = Sky(WeatherPreset.Broken, time);
                Assert.Equal(1f, CloudVolumes.Strength(mixed));

                int mixedCount = CloudVolumes.Place(lens, mixed, new CloudVolumes.Heap[CloudVolumes.CAPACITY], new float[CloudVolumes.CAPACITY]);
                int brokenCount = CloudVolumes.Place(lens, broken, new CloudVolumes.Heap[CloudVolumes.CAPACITY], new float[CloudVolumes.CAPACITY]);
                Assert.True(mixedCount > 0 && mixedCount < brokenCount, $"at {time}: {mixedCount} heaps under Mixed, {brokenCount} under Broken");
            }
        }

        [Fact]
        public void TheGlintAndTheCrestGlowTakeTheSunsBeam()
        {
            //Sea.fx cannot run without a device, so its lines are read: one crossing of the sun ray with the deck, the light
            //under it floored as CloudSunlight is, and the beam with no floor, the cover times the deck's opacity. Seen to fail
            //with the glint back on `sunlight`
            string sea = Code("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Sea.fx");
            Assert.Contains("float sunCover = CloudSunCover(worldPosition, SunDirection);", sea);
            Assert.Contains("float sunlight = CloudSunlightUnder(sunCover);", sea);
            Assert.Contains("float sunBeam = 1.0 - saturate(sunCover * CloudOpacity);", sea);
            Assert.Matches(@"float glint = pow\(saturate\(dot\(reflected, SunDirection\)\), SunGlintPower\) \* SunGlintStrength \* sunBeam;", sea);
            Assert.Matches(@"float sss = backlight \* [^;]*\* sunBeam \*", sea);
            Assert.Contains("float CloudOpacity;", sea);

            //And Clouds.fxh: the light under the deck is what CloudSunlight always was, the cover is the weather layer at the
            //sun ray's crossing and nothing with no weather
            string clouds = Code("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Clouds.fxh");
            Assert.Contains("return lerp(1.0, CloudShadowFloor, saturate(cover * CloudShadowGain));", clouds);
            Assert.Contains("return CloudSunlightUnder(CloudCover(CloudSunHit(worldPosition, sunDirection)));", clouds);
            Assert.Contains("if (CloudCoverageGain > 0.0) cover = CloudCover(CloudSunHit(worldPosition, sunDirection));", clouds);
        }

        private static string FindFile(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }

        //A source without its comments and with its whitespace run together, so a word in a note is not taken for code and a
        //line broken differently still reads
        private static string Code(params string[] relative)
        {
            string text = File.ReadAllText(FindFile(Path.Combine(relative)));
            text = Regex.Replace(text, @"//[^\n]*", string.Empty);
            return Regex.Replace(text, @"\s+", " ");
        }
    }
}
