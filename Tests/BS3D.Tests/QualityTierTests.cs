using System;
using System.IO;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The quality ladder's shape (#788): one preset row per tier, Potato below Low in what it drops, and a Potato
    /// written to the settings file read back as Potato. The lock itself is GamePi's platform seam and has no copy here.
    /// </summary>
    public class QualityTierTests
    {
        [Fact]
        public void EveryTierHasItsPresetRow()
        {
            //The table is indexed by (int)tier, so a tier appended without its row is an IndexOutOfRange at the first
            //ApplyQuality - which on GamePi is the first frame
            Assert.Equal(Enum.GetValues<QualityLevel>().Length, QualityPreset.Presets.Length);
        }

        [Theory]
        [InlineData(QualityLevel.Potato, true)]
        [InlineData(QualityLevel.Low, true)]
        [InlineData(QualityLevel.Medium, false)]
        [InlineData(QualityLevel.High, false)]
        [InlineData(QualityLevel.Ultra, false)]
        public void OnlyLowAndPotatoDropTheSceneDetail(QualityLevel quality, bool drops) =>
            Assert.Equal(drops, quality.DropsSceneDetail());

        [Theory]
        //A phone's floors stop at its 60 FPS target (#1070): the X300 Ultra's display at 120 Hz, and its 144 Hz mode
        [InlineData(120f, true, 60f)]
        [InlineData(144f, true, 60f)]
        //Under the target they follow the display, as a 60 Hz phone's always did, and one that reports nothing stays unknown
        [InlineData(60f, true, 60f)]
        [InlineData(48f, true, 48f)]
        [InlineData(0f, true, 0f)]
        //The desktop and the Pi follow the monitor at every rate
        [InlineData(144f, false, 144f)]
        [InlineData(75f, false, 75f)]
        public void APhonesFloorsStopAtItsTarget(float refreshHz, bool phone, float expected) =>
            Assert.Equal(expected, PhoneTiers.FloorRefresh(refreshHz, phone));

        [Theory]
        //A monitor's cap is never a number (#124), whatever the monitor says
        [InlineData(false, false, 75, 0, "Monitor")]
        [InlineData(false, false, 144, 144, "Monitor")]
        //The player's own choice of no limit reads the same everywhere
        [InlineData(true, false, 75, 0, "Unlimited")]
        [InlineData(true, true, 60, 144, "Unlimited")]
        //A phone names its display's rate, and says when the phone holds it at 60 under a display that offers more (#1070:
        //the X300 Ultra offers 144 and held BS3D at 60 until the app was allowed a high refresh rate, then ran it at 120)
        [InlineData(false, true, 60, 144, "60 Hz (phone setting)")]
        [InlineData(false, true, 60, 90, "60 Hz (phone setting)")]
        [InlineData(false, true, 120, 144, "120 Hz")]
        //A 60 Hz phone is not held, and one whose modes do not answer is not accused of it
        [InlineData(false, true, 60, 60, "60 Hz")]
        [InlineData(false, true, 60, 0, "60 Hz")]
        //A display that says nothing has no rate to name
        [InlineData(false, true, 0, 0, "Monitor")]
        public void TheFpsLimitRowNamesAPhonesRateAndWhoHoldsIt(bool uncapped, bool phone, int refreshHz, int fastestHz, string expected) =>
            Assert.Equal(expected, PhoneTiers.FpsLimitLabel(uncapped, phone, refreshHz, fastestHz));

        [Fact]
        public void PotatoTurnsDownNoLessThanLow()
        {
            QualityPreset potato = QualityPreset.Presets[(int)QualityLevel.Potato];
            QualityPreset low = QualityPreset.Presets[(int)QualityLevel.Low];

            Assert.Equal(1, potato.SupersampleFactor);
            Assert.True(potato.MsaaSamples <= low.MsaaSamples);
            Assert.False(potato.CeilingRefraction);
            Assert.False(potato.MotionBlur);
            Assert.False(potato.ClusterWind);
            Assert.True(potato.ShadowMapCap is > 0 and <= 2048);
        }

        [Fact]
        public void AppendingPotatoMovedNoOlderTier()
        {
            //Settings.json stores names, but the converter accepts a bare number too (#484), so the four keep theirs
            Assert.Equal(0, (int)QualityLevel.Low);
            Assert.Equal(1, (int)QualityLevel.Medium);
            Assert.Equal(2, (int)QualityLevel.High);
            Assert.Equal(3, (int)QualityLevel.Ultra);
        }

        [Fact]
        public void PotatoSurvivesTheSettingsFileByName()
        {
            using TempDirectory temp = new();
            GameSettings settings = GameSettings.Load(temp.File("Settings.json"));

            settings.Quality = QualityLevel.Potato;
            settings.Save();

            Assert.Contains("\"quality\": \"Potato\"", File.ReadAllText(settings.Path));
            Assert.Equal(QualityLevel.Potato, GameSettings.Load(settings.Path).Quality);
        }

        /// <summary>
        /// The Quality row on Windows (#808): Low, Medium, High, Ultra, Potato and round again, by name - the enum's order
        /// is not the ladder's - and every tier reached exactly once in a full turn.
        /// </summary>
        [Fact]
        public void TheRowWalksTheLadderThroughPotatoAndBack()
        {
            QualityLevel[] expected = { QualityLevel.Medium, QualityLevel.High, QualityLevel.Ultra, QualityLevel.Potato, QualityLevel.Low };
            QualityLevel tier = QualityLevel.Low;

            foreach (QualityLevel next in expected)
            {
                tier = tier.NextOnRow();
                Assert.Equal(next, tier);
            }
        }

        /// <summary>
        /// What <c>Program.Main</c> reads before the game exists (#808): Potato named on the row starts the Pi's renderer,
        /// any other tier, none, or no file at all does not; and the backup answers for a file that would not read.
        /// </summary>
        [Fact]
        public void OnlyAStoredPotatoChoosesThePotatoRenderer()
        {
            using TempDirectory temp = new();
            string path = temp.File("Settings.json");

            Assert.False(GameSettings.ChoosesPotato(path));

            GameSettings settings = GameSettings.Load(path);
            settings.Quality = QualityLevel.Ultra;
            settings.Save();
            Assert.False(GameSettings.ChoosesPotato(path));

            settings.Quality = QualityLevel.Potato;
            settings.Save();
            Assert.True(GameSettings.ChoosesPotato(path));

            //A damaged file: the backup answers for it. A save keeps the file it replaces as the backup, so a second save
            //of the same choice is what puts Potato there too
            settings.Save();
            File.WriteAllText(path, "{ not json");
            Assert.True(GameSettings.ChoosesPotato(path));
        }

        /// <summary>The Scenery row (#808): absent from the file while on, stored as false when off, and read back so.</summary>
        [Fact]
        public void SceneryOffIsStoredAndOnIsTheDefault()
        {
            using TempDirectory temp = new();
            GameSettings settings = GameSettings.Load(temp.File("Settings.json"));

            Assert.Null(settings.Scenery);
            settings.Save();
            Assert.DoesNotContain("scenery", File.ReadAllText(settings.Path));

            settings.Scenery = false;
            settings.Save();
            Assert.Contains("\"scenery\": false", File.ReadAllText(settings.Path));
            Assert.False(GameSettings.Load(settings.Path).Scenery);
        }
    }
}
