using Prazsky.Core.Render;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The sky a level stands under (#221), in the one copy the Game, the Testbed and the map editor's game sky (#1210) all
    /// put it up by (<see cref="WeatherLooks.ForLevel"/>): the level's word, else its scene's own, else Scattered. A word
    /// nobody knows is a level under its scene's usual sky, not a level that will not open.
    /// </summary>
    public class LevelWeatherTests
    {
        [Fact]
        public void The_level_s_word_wins_over_the_scene_s_sky()
        {
            Assert.Equal(WeatherPreset.Storm, WeatherLooks.ForLevel("storm", WeatherPreset.Clear));
            Assert.Equal(WeatherPreset.Mixed, WeatherLooks.ForLevel(" Mixed ", WeatherPreset.Overcast));
        }

        [Fact]
        public void A_level_that_names_no_sky_or_one_nobody_knows_stands_under_its_scene_s()
        {
            Assert.Equal(WeatherPreset.Broken, WeatherLooks.ForLevel(null, WeatherPreset.Broken));
            Assert.Equal(WeatherPreset.Broken, WeatherLooks.ForLevel("", WeatherPreset.Broken));
            Assert.Equal(WeatherPreset.Overcast, WeatherLooks.ForLevel("drizzle", WeatherPreset.Overcast));
        }

        [Fact]
        public void With_no_scene_to_ask_it_is_the_scattered_sky()
        {
            Assert.Equal(WeatherPreset.Scattered, WeatherLooks.ForLevel(null, null));
            Assert.Equal(WeatherPreset.Clear, WeatherLooks.ForLevel("clear", null));
        }
    }
}
