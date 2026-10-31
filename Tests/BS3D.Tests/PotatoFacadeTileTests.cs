using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The cities' window tile on the Potato path (#1044), with no device: the glass is the config's fill, the lit windows
    /// its lit share, and the lamps' pick is half and half by day and one in six in the neon city.
    /// </summary>
    public class PotatoFacadeTileTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TheTileIsTheConfigsWindows(bool neon)
        {
            var config = new CitySceneConfig();
            Color[] tile = PotatoBackdrop.BakeFacadeTile(config, neon);
            int size = PotatoBackdrop.FACADE_TILE_WINDOWS * PotatoBackdrop.FACADE_TILE_TEXELS;
            Assert.Equal(size * size, tile.Length);

            double glass = 0, lit = 0;
            int windows = PotatoBackdrop.FACADE_TILE_WINDOWS * PotatoBackdrop.FACADE_TILE_WINDOWS, litWindows = 0, picked = 0;
            foreach (Color texel in tile) { glass += texel.R / 255.0; lit += texel.G / 255.0; }
            for (int y = 0; y < PotatoBackdrop.FACADE_TILE_WINDOWS; y++)
                for (int x = 0; x < PotatoBackdrop.FACADE_TILE_WINDOWS; x++)
                {
                    //The middle of each window's cell: glass, and lit or not, one colour or the other, for the whole window
                    Color middle = tile[(y * PotatoBackdrop.FACADE_TILE_TEXELS + 8) * size + x * PotatoBackdrop.FACADE_TILE_TEXELS + 8];
                    Assert.Equal(255, middle.R);
                    Assert.True(middle.G == 0 || middle.G == 255);
                    if (middle.G == 255) litWindows++;
                    if (middle.B == 255) picked++;
                }

            //The glass is the fill's share of the wall, to the 64 samples a cell has across (a sample's width either way on
            //each axis), and the lit glass about the lit share of that, over 64 windows
            Assert.InRange(glass / tile.Length, config.WindowFillX * config.WindowFillY - 0.015, config.WindowFillX * config.WindowFillY + 0.015);
            Assert.InRange((double)litWindows / windows, config.WindowLitFraction - 0.15, config.WindowLitFraction + 0.15);
            Assert.Equal(lit / glass, (double)litWindows / windows, 2);

            //The pick: by day the warm and the cool about even, in the neon city about one window in six sparking
            double share = (double)picked / windows;
            if (neon) Assert.InRange(share, 0.05, 0.3);
            else Assert.InRange(share, 0.35, 0.65);
        }
    }
}
