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
    }
}
