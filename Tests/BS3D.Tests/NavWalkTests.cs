using BS3D.Screens;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The pad's walk over a grid page (#848, <see cref="NavWalk"/>), on a model of the level picker laid out as it is
    /// drawn: the two chapter arrows either side of the heading, a row of fourteen pips under it, five tiles in each of
    /// two rows, and Back under the grid — in the walk's list order, which is the order the page adds them. What is held
    /// is what the owner asked for with a pad in hand: "down" lights the level under the lit one, and the other three
    /// directions do what they say.
    /// </summary>
    public class NavWalkTests
    {
        private const int PREVIOUS = 0, NEXT = 1, FIRST_PIP = 2, PIPS = 14, FIRST_TILE = FIRST_PIP + PIPS, BACK = FIRST_TILE + 10;

        /// <summary>The picker at 1600×900, in pixels: rounded off a capture of the page.</summary>
        private static List<Rectangle> Picker()
        {
            List<Rectangle> entries = new()
            {
                new Rectangle(500, 130, 67, 67),  //◀
                new Rectangle(1033, 130, 67, 67), //▶
            };

            //The pips' row is narrower than the pager's (758 px against 833 at 1600x900), so it stands inside the arrows
            for (int pip = 0; pip < PIPS; pip++) entries.Add(new Rectangle(520 + pip * 41, 240, 36, 36));
            for (int tile = 0; tile < 10; tile++) entries.Add(new Rectangle(560 + (tile % 5) * 100, 320 + (tile / 5) * 105, 92, 98));
            entries.Add(new Rectangle(640, 560, 320, 42)); //Back

            return entries;
        }

        private static int Tile(int row, int column) => FIRST_TILE + row * 5 + column;

        [Fact]
        public void DownFromATileIsTheTileUnderIt()
        {
            List<Rectangle> picker = Picker();

            for (int column = 0; column < 5; column++)
                Assert.Equal(Tile(1, column), NavWalk.Toward(picker, Tile(0, column), 0, 1));
        }

        [Fact]
        public void UpFromTheSecondRowIsTheTileAboveIt()
        {
            List<Rectangle> picker = Picker();

            for (int column = 0; column < 5; column++)
                Assert.Equal(Tile(0, column), NavWalk.Toward(picker, Tile(1, column), 0, -1));
        }

        [Fact]
        public void LeftAndRightWalkTheRowAndStopAtItsEnds()
        {
            List<Rectangle> picker = Picker();

            Assert.Equal(Tile(0, 3), NavWalk.Toward(picker, Tile(0, 2), 1, 0));
            Assert.Equal(Tile(0, 1), NavWalk.Toward(picker, Tile(0, 2), -1, 0));

            //The row's ends stop: right from the grid's last tile is not the next-chapter arrow far above it (further across
            //than ahead), and left from a row's first tile is nothing
            Assert.Equal(-1, NavWalk.Toward(picker, Tile(1, 4), 1, 0));
            Assert.Equal(-1, NavWalk.Toward(picker, Tile(1, 0), -1, 0));
        }

        [Fact]
        public void UpFromTheFirstRowIsThePipNearestAboveIt()
        {
            List<Rectangle> picker = Picker();

            for (int column = 0; column < 5; column++)
            {
                int pip = NavWalk.Toward(picker, Tile(0, column), 0, -1);

                Assert.InRange(pip, FIRST_PIP, FIRST_PIP + PIPS - 1);
                int tileX = picker[Tile(0, column)].Center.X;
                for (int other = FIRST_PIP; other < FIRST_PIP + PIPS; other++)
                    Assert.True(System.Math.Abs(picker[pip].Center.X - tileX) <= System.Math.Abs(picker[other].Center.X - tileX));
            }
        }

        [Fact]
        public void DownFromAPipPastTheGridsEndStillReachesTheGrid()
        {
            List<Rectangle> picker = Picker();

            //The row's last pip stands right of every tile: nothing in line, so the nearest tile that way, the corner
            Assert.Equal(Tile(0, 4), NavWalk.Toward(picker, FIRST_PIP + PIPS - 1, 0, 1));
            Assert.Equal(Tile(0, 0), NavWalk.Toward(picker, FIRST_PIP, 0, 1));
        }

        [Fact]
        public void DownFromTheLastRowIsBackAndUpFromBackIsTheGrid()
        {
            List<Rectangle> picker = Picker();

            Assert.Equal(BACK, NavWalk.Toward(picker, Tile(1, 2), 0, 1));
            Assert.Equal(BACK, NavWalk.Toward(picker, Tile(1, 0), 0, 1));
            Assert.Equal(Tile(1, 2), NavWalk.Toward(picker, BACK, 0, -1));
        }

        [Fact]
        public void TheArrowsAreReachedFromThePipsAndLeadBackDown()
        {
            List<Rectangle> picker = Picker();

            Assert.Equal(PREVIOUS, NavWalk.Toward(picker, FIRST_PIP + 1, 0, -1));
            Assert.Equal(NEXT, NavWalk.Toward(picker, FIRST_PIP + PIPS - 2, 0, -1));
            Assert.Equal(NEXT, NavWalk.Toward(picker, PREVIOUS, 1, 0));

            int down = NavWalk.Toward(picker, PREVIOUS, 0, 1);
            Assert.InRange(down, FIRST_PIP, FIRST_PIP + PIPS - 1);
        }

        [Fact]
        public void NothingAheadMovesNothing()
        {
            List<Rectangle> picker = Picker();

            Assert.Equal(-1, NavWalk.Toward(picker, BACK, 0, 1));
            Assert.Equal(-1, NavWalk.Toward(picker, PREVIOUS, -1, 0));
            Assert.Equal(-1, NavWalk.Toward(picker, PREVIOUS, 0, -1));
            Assert.Equal(-1, NavWalk.Toward(picker, 0, 1, 1));
        }
    }
}
