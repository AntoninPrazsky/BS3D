using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The Potato path's cloud deck (#1044), with no device: the baked field tiles, the disc is flat and ends over the
    /// horizon, a clear sky draws nothing, and the shader reads the tile with the class's own figure.
    /// </summary>
    public class PotatoCloudDeckTests
    {
        [Theory]
        [InlineData(4, 0)]
        [InlineData(12, 17)]
        [InlineData(8, 41)]
        public void TheFieldRepeatsAtItsPeriod(int period, int offset)
        {
            //Across the seam and along it: a texture read past the tile's edge wraps to the other edge, and the field there
            //has to be the same, or the deck shows the tile's border as a line
            for (float y = 0f; y < period; y += 0.37f)
            {
                Assert.Equal(PotatoCloudDeck.WrappedNoise(0.25f, y, period, offset), PotatoCloudDeck.WrappedNoise(period + 0.25f, y, period, offset), 5);
                Assert.Equal(PotatoCloudDeck.WrappedNoise(y, 0.6f, period, offset), PotatoCloudDeck.WrappedNoise(y, period + 0.6f, period, offset), 5);
            }
        }

        [Fact]
        public void TheBakedTileHasNoSeam()
        {
            const int size = 64;
            Color[] field = PotatoCloudDeck.BakeField(size);

            //A texel's step to its neighbour across the wrap is no bigger than the steps inside the tile
            int largestInside = 0, largestAcross = 0;
            for (int y = 0; y < size; y++)
            {
                for (int x = 1; x < size; x++) largestInside = Math.Max(largestInside, Math.Abs(field[y * size + x].R - field[y * size + x - 1].R));
                largestAcross = Math.Max(largestAcross, Math.Abs(field[y * size].R - field[y * size + size - 1].R));
            }

            Assert.True(largestAcross <= largestInside, $"across the seam {largestAcross}, inside the tile {largestInside}");
        }

        [Fact]
        public void TheDiscIsFlatOneUnitUpAndEndsOverTheHorizon()
        {
            (VertexPositionColor[] vertices, int[] indices) = PotatoCloudDeck.BuildDisc(6, 12, PotatoCloudDeck.HORIZON_RISE);

            Assert.Equal(1 + 6 * 12, vertices.Length);
            Assert.Equal(12 * 3 + 5 * 12 * 6, indices.Length);
            foreach (VertexPositionColor vertex in vertices) Assert.Equal(1f, vertex.Position.Y);
            foreach (int index in indices) Assert.InRange(index, 0, vertices.Length - 1);

            //The outer ring stands at the stated rise over the horizon, and every ring at the same distance all round
            float rise = 1f / vertices[^1].Position.Length();
            Assert.Equal(PotatoCloudDeck.HORIZON_RISE, rise, 4);

            //Drawn round the camera at the class's scale, the whole disc is inside the camera's planes
            float farthest = vertices[^1].Position.Length() * PotatoCloudDeck.DRAW_SCALE;
            Assert.True(farthest < Prazsky.Core.Camera.BasicCamera3D.DEFAULT_FAR_PLANE_DISTANCE, $"the disc reaches {farthest}");
        }

        [Theory]
        [InlineData(WeatherPreset.Clear, false)]
        [InlineData(WeatherPreset.Scattered, true)]
        [InlineData(WeatherPreset.Broken, true)]
        [InlineData(WeatherPreset.Overcast, true)]
        [InlineData(WeatherPreset.Storm, true)]
        public void OnlyAClearSkyDrawsNothing(WeatherPreset preset, bool shows)
        {
            var clouds = new CloudField();
            clouds.SetWeatherImmediately(preset);

            Assert.Equal(shows, PotatoCloudDeck.Shows(clouds));
        }

        [Fact]
        public void TheShaderReadsTheTileWithTheClassesFigure()
        {
            string source = File.ReadAllText(FindUnderRepository(Path.Combine("GamePi", "Shaders", "PotatoSky.fx")));

            Match tile = Regex.Match(source, @"#define\s+CLOUD_TILE_FEATURES\s+([0-9.]+)");
            Assert.True(tile.Success, "PotatoSky.fx defines no CLOUD_TILE_FEATURES");
            Assert.Equal(PotatoCloudDeck.TILE_FEATURES, float.Parse(tile.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        private static string FindUnderRepository(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}
