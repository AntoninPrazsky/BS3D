using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The city's roof dressing (#436) holds what <c>CityRooftops.Dress</c> promises: a roof is split into cells and each
    /// piece takes one, so no two pieces of one roof stand in each other. #850: the neon city's extras were dressed on a
    /// cell grid of their own, and stood a second, shorter mast inside the day's mast (whose middle cell both calls take),
    /// a dish or a 5G pole inside another, wherever the two calls rolled the same cell. Held over the real layout
    /// (<c>CityRooftops.ArrangePieces</c>, no device) on both cities and a dozen rolls, the neon extras included in the
    /// day city too (built there, though only the neon city draws them).
    /// </summary>
    public class CityRooftopsTests
    {
        private const int ROLLS = 12;

        //The city and its seeds offset by the roll the way the game adds its scene seed to both (CityIntroShotsTests)
        private static CityRooftops.RoofPiece[] Pieces(bool neon, int roll, out City city)
        {
            var config = new CitySceneConfig();
            config.Seed += roll;
            config.NeonLayout.Seed += roll;

            city = new City(config, neon, ArenaIsland.RADIUS);
            return CityRooftops.ArrangePieces(city, config, CityRooftops.DEFAULT_SEED + roll);
        }

        [Fact]
        public void NoTwoPiecesOfOneRoofShareACell()
        {
            int pieces = 0, extras = 0, shared = 0;
            string example = null;

            for (int roll = 0; roll < ROLLS; roll++)
                foreach (bool neon in new[] { false, true })
                {
                    //A piece's foot is the middle of the cell it took, so two pieces in one cell stand on one foot - and
                    //two roofs never share a cell's middle (the towers' footprints do not overlap, and a setback's tiers
                    //share a centre but not a roof height)
                    var feet = new Dictionary<Vector3, CityRooftops.RoofPiece>();
                    foreach (CityRooftops.RoofPiece piece in Pieces(neon, roll, out _))
                    {
                        pieces++;
                        if (piece.Neon) extras++;

                        if (!feet.TryGetValue(piece.Foot, out CityRooftops.RoofPiece first))
                        {
                            feet.Add(piece.Foot, piece);
                            continue;
                        }

                        shared++;
                        example ??= $"roll {roll}, the {(neon ? "neon" : "day")} city: {Name(piece)} stands in the cell of {Name(first)} at {piece.Foot}";
                    }
                }

            //Not vacuous: the layout was dressed, and the neon extras were among it
            Assert.True(pieces >= ROLLS * 2 * 5000, $"only {pieces} pieces were dressed");
            Assert.True(extras >= ROLLS * 2 * 1000, $"only {extras} of the pieces were the neon city's extras");
            Assert.True(shared == 0, $"{shared} of {pieces} pieces stand in a cell another piece of their roof took; the first: {example}");
        }

        private static string Name(CityRooftops.RoofPiece piece) => (piece.Neon ? "the neon extras' " : "the day's ") + piece.Kind;
    }
}
