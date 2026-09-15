using System;
using System.Numerics;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One Game of Life board behind one of the Grid scene's distant solids (#393): which of the solid's window
    /// panes are lit this generation, and which were lit the generation before — the second is what
    /// <c>Grid.fx</c> fades out as a phosphor's afterglow.
    /// <para>
    /// <b>Named patterns, not random soup.</b> The scene's whole thesis is mathematics an eye can name once pointed
    /// at it, and the owner's review asked for "different nice variants" per object. A random start on a board this
    /// small is neither: it settles into anonymous still lifes and blinkers within a few hundred generations
    /// (measured over 400 soups at the 28 % density the first cut seeded: median 365 generations, three minutes at
    /// the shipped step). So a board is stamped with one of a short deck of classic patterns — a pulsar, a
    /// pentadecathlon, Kok's galaxy, a convoy of spaceships, a methuselah — every one of them run against the rules
    /// on this exact torus in every orientation when the deck was written (see "The Grid" in
    /// <c>docs/scenes.md</c> for the periods measured). A tall tower draws from <see cref="TALL_DECK"/>, patterns
    /// that travel or stand along its length; a block from <see cref="BLOCK_DECK"/>.
    /// </para>
    /// <para>
    /// <b>A stale board moves on rather than being stirred.</b> The first cut flipped random cells every generation
    /// to keep a soup from freezing, which also wrecks any oscillator a flip lands beside. Here a board whose
    /// generation equals the one two before it — a still life, a field of blinkers, or extinction — holds that for
    /// <see cref="STALE_HOLD_GENERATIONS"/> and is then stamped with its next pattern. Every oscillator of period
    /// three or more and every spaceship is left running untouched, and nothing in either deck has period one or
    /// two. A pattern also yields after its generation budget, so a long level shows more than one.
    /// </para>
    /// <para>
    /// Pure logic with no graphics device — the scene renderer owns the texture — so the rules and the decks can
    /// be compiled and checked on their own.
    /// </para>
    /// </summary>
    public sealed class GridLife
    {
        /// <summary>
        /// The board's side, in cells. Exactly the bit width of a <see cref="uint"/> on purpose: a row IS a uint, bit
        /// x being cell x, so the torus's horizontal wrap is a rotation and a whole generation is a few dozen
        /// operations per row (<see cref="StepRows"/>). <c>Grid.fx</c>'s <c>GRID_LIFE_SIZE</c> must match it.
        /// </summary>
        public const int SIZE = 32;

        /// <summary>How many generations a stale board shows its ash before the next pattern replaces it — long enough to read as the pattern having run its course, short enough that a dead face does not read as a fault.</summary>
        private const int STALE_HOLD_GENERATIONS = 4;

        private const string GLIDER = ".O./..O/OOO";

        //The three orthogonal spaceships, each as written travelling towards -x at c/2.
        private const string LIGHTWEIGHT_SPACESHIP = ".O..O/O..../O...O/OOOO.";
        private const string MIDDLEWEIGHT_SPACESHIP = "...O../.O...O/O...../O....O/OOOOO.";
        private const string HEAVYWEIGHT_SPACESHIP = "...OO../.O....O/O....../O.....O/OOOOOO.";

        private const string PULSAR = "..OOO...OOO../............./O....O.O....O/O....O.O....O/O....O.O....O/..OOO...OOO../" +
            "............./..OOO...OOO../O....O.O....O/O....O.O....O/O....O.O....O/............./..OOO...OOO..";

        //Ten cells in a row become a pentadecathlon within a few generations - the classic way to start one.
        private const string TEN_CELL_ROW = "OOOOOOOOOO";

        private const string KOKS_GALAXY = "OOOOOO.OO/OOOOOO.OO/.......OO/OO.....OO/OO.....OO/OO.....OO/OO......./OO.OOOOOO/OO.OOOOOO";
        private const string FIGURE_EIGHT = "OOO.../OOO.../OOO.../...OOO/...OOO/...OOO";
        private const string TUMBLER = ".O.....O./O.O...O.O/O..O.O..O/..O...O../..OO.OO..";
        private const string OCTAGON_2 = "...OO.../..O..O../.O....O./O......O/O......O/.O....O./..O..O../...OO...";

        //Methuselahs: a handful of cells that burn for a long while before settling (or, for diehard, vanishing).
        private const string R_PENTOMINO = ".OO/OO./.O.";
        private const string DIEHARD = "......O./OO....../.O...OOO";
        private const string ACORN = ".O...../...O.../OO..OOO";

        private readonly struct Part
        {
            public readonly string Cells;
            public readonly int X, Y;

            public Part(string cells, int x, int y)
            {
                Cells = cells;
                X = x;
                Y = y;
            }
        }

        private sealed class Pattern
        {
            public readonly string Name;

            /// <summary>Turned so its own x axis — a spaceship's travel, a pentadecathlon's length — runs along a tall tower's height rather than across it.</summary>
            public readonly bool AlongLength;

            public readonly Part[] Parts;

            public Pattern(string name, bool alongLength, params Part[] parts)
            {
                Name = name;
                AlongLength = alongLength;
                Parts = parts;
            }
        }

        //Several ships of one pattern travel in lockstep, so their spacing never changes and they never meet - the
        //spacings are chosen to clear each ship's sparks, the wrap across the torus included.
        private static readonly Pattern GLIDER_FLEET = new("glider fleet", true,
            new Part(GLIDER, 0, 0), new Part(GLIDER, 8, 5), new Part(GLIDER, 16, 10), new Part(GLIDER, 24, 15));
        private static readonly Pattern LIGHTWEIGHT_CONVOY = new("lightweight convoy", true,
            new Part(LIGHTWEIGHT_SPACESHIP, 0, 0), new Part(LIGHTWEIGHT_SPACESHIP, 3, 11), new Part(LIGHTWEIGHT_SPACESHIP, 1, 22));
        private static readonly Pattern MIDDLEWEIGHT_PAIR = new("middleweight pair", true,
            new Part(MIDDLEWEIGHT_SPACESHIP, 0, 0), new Part(MIDDLEWEIGHT_SPACESHIP, 6, 16));
        private static readonly Pattern HEAVYWEIGHT_PAIR = new("heavyweight pair", true,
            new Part(HEAVYWEIGHT_SPACESHIP, 0, 0), new Part(HEAVYWEIGHT_SPACESHIP, 8, 16));
        private static readonly Pattern MIXED_CONVOY = new("mixed convoy", true,
            new Part(LIGHTWEIGHT_SPACESHIP, 0, 0), new Part(MIDDLEWEIGHT_SPACESHIP, 4, 10), new Part(HEAVYWEIGHT_SPACESHIP, 2, 21));
        private static readonly Pattern PENTADECATHLON = new("pentadecathlon", true, new Part(TEN_CELL_ROW, 0, 0));
        private static readonly Pattern PENTADECATHLON_PAIR = new("pentadecathlon pair", true,
            new Part(TEN_CELL_ROW, 0, 0), new Part(TEN_CELL_ROW, 0, 16));

        private static readonly Pattern PULSAR_SINGLE = new("pulsar", false, new Part(PULSAR, 0, 0));
        private static readonly Pattern PULSAR_QUARTET = new("pulsar quartet", false,
            new Part(PULSAR, 0, 0), new Part(PULSAR, 16, 0), new Part(PULSAR, 0, 16), new Part(PULSAR, 16, 16));
        private static readonly Pattern KOKS_GALAXY_SINGLE = new("Kok's galaxy", false, new Part(KOKS_GALAXY, 0, 0));
        private static readonly Pattern FIGURE_EIGHT_SINGLE = new("figure eight", false, new Part(FIGURE_EIGHT, 0, 0));
        private static readonly Pattern TUMBLER_SINGLE = new("tumbler", false, new Part(TUMBLER, 0, 0));
        private static readonly Pattern OCTAGON_2_SINGLE = new("octagon 2", false, new Part(OCTAGON_2, 0, 0));
        private static readonly Pattern R_PENTOMINO_SINGLE = new("R-pentomino", false, new Part(R_PENTOMINO, 0, 0));
        private static readonly Pattern DIEHARD_SINGLE = new("diehard", false, new Part(DIEHARD, 0, 0));
        private static readonly Pattern ACORN_SINGLE = new("acorn", false, new Part(ACORN, 0, 0));

        /// <summary>What a tall, narrow tower shows: traffic running up or down its height, or a long oscillator standing along it.</summary>
        private static readonly Pattern[] TALL_DECK =
        {
            GLIDER_FLEET, LIGHTWEIGHT_CONVOY, MIDDLEWEIGHT_PAIR, HEAVYWEIGHT_PAIR, MIXED_CONVOY, PENTADECATHLON, PENTADECATHLON_PAIR,
        };

        /// <summary>What a block shows: oscillators that fill a big face, methuselahs, and some traffic.</summary>
        private static readonly Pattern[] BLOCK_DECK =
        {
            PULSAR_SINGLE, PULSAR_QUARTET, KOKS_GALAXY_SINGLE, FIGURE_EIGHT_SINGLE, TUMBLER_SINGLE, OCTAGON_2_SINGLE,
            PENTADECATHLON, R_PENTOMINO_SINGLE, DIEHARD_SINGLE, ACORN_SINGLE, GLIDER_FLEET, MIXED_CONVOY,
        };

        //Three generations, rotated rather than copied (Step): the current one, the one before it (the afterglow
        //the texture carries), and the one before that (the stale test). No allocation after construction.
        private uint[] _current = new uint[SIZE];
        private uint[] _previous = new uint[SIZE];
        private uint[] _older = new uint[SIZE];

        private readonly Random _random;
        private readonly Pattern[] _deck;
        private readonly bool _tall;
        private readonly int _patternGenerations;

        private Pattern _pattern;
        private int _staleGenerations;

        /// <param name="seed">This board's own seed — the pattern it starts on, the orientation of each, and the order it moves through its deck.</param>
        /// <param name="tall">A tall tower rather than a block: which deck, and whether a pattern is turned along the length.</param>
        /// <param name="patternGenerations">How many generations one pattern runs before the next replaces it, stale or not.</param>
        public GridLife(int seed, bool tall, int patternGenerations)
        {
            _random = new Random(seed);
            _tall = tall;
            _deck = tall ? TALL_DECK : BLOCK_DECK;
            _patternGenerations = Math.Max(patternGenerations, 1);

            StampNextPattern();
        }

        /// <summary>Generations since the current pattern was stamped.</summary>
        public int Generation { get; private set; }

        /// <summary>The pattern currently running, for a log line or a probe.</summary>
        public string PatternName => _pattern?.Name;

        /// <summary>Row <paramref name="y"/> of the current generation, bit x being cell x.</summary>
        public uint Row(int y) => _current[y];

        /// <summary>Row <paramref name="y"/> of the generation before — what the afterglow fades out.</summary>
        public uint PreviousRow(int y) => _previous[y];

        /// <summary>Advances one generation, and moves on to the next pattern when this one has gone stale or run its budget.</summary>
        public void Step()
        {
            //The new generation is written over the oldest one, which nothing needs any more, and the three
            //references then rotate.
            StepRows(_current, _older);
            (_current, _previous, _older) = (_older, _current, _previous);
            Generation++;

            _staleGenerations = _current.AsSpan().SequenceEqual(_older) ? _staleGenerations + 1 : 0;

            if (_staleGenerations >= STALE_HOLD_GENERATIONS || Generation >= _patternGenerations)
                StampNextPattern();
        }

        /// <summary>
        /// One generation of the ordinary rules (B3/S23) on the 32×32 torus, row-parallel: for each row the eight
        /// neighbour rows — the rows above and below and the row itself, each shifted a cell either way, the shift
        /// wrapping — are summed bit position by bit position with carry-save addition, so all 32 cells of a row are
        /// counted at once. <c>ones</c> and <c>twos</c> hold each cell's count modulo four and <c>fours</c> latches
        /// once it reaches four, which is all the rule needs: a cell lives on exactly three, or on two while alive.
        /// </summary>
        public static void StepRows(ReadOnlySpan<uint> current, Span<uint> next)
        {
            for (int y = 0; y < SIZE; y++)
            {
                uint above = current[(y - 1) & (SIZE - 1)];
                uint row = current[y];
                uint below = current[(y + 1) & (SIZE - 1)];

                uint ones = 0, twos = 0, fours = 0;

                Count(BitOperations.RotateLeft(above, 1), ref ones, ref twos, ref fours);
                Count(above, ref ones, ref twos, ref fours);
                Count(BitOperations.RotateRight(above, 1), ref ones, ref twos, ref fours);
                Count(BitOperations.RotateLeft(row, 1), ref ones, ref twos, ref fours);
                Count(BitOperations.RotateRight(row, 1), ref ones, ref twos, ref fours);
                Count(BitOperations.RotateLeft(below, 1), ref ones, ref twos, ref fours);
                Count(below, ref ones, ref twos, ref fours);
                Count(BitOperations.RotateRight(below, 1), ref ones, ref twos, ref fours);

                next[y] = twos & (ones | row) & ~fours;
            }
        }

        private static void Count(uint neighbours, ref uint ones, ref uint twos, ref uint fours)
        {
            uint carry = ones & neighbours;
            ones ^= neighbours;
            fours |= twos & carry;
            twos ^= carry;
        }

        private void StampNextPattern()
        {
            Pattern next;
            do next = _deck[_random.Next(_deck.Length)];
            while (next == _pattern && _deck.Length > 1);

            //Orientations 4-7 swap the axes, which is what turns a pattern's own x along a tower's height; a block
            //takes any of the eight. Life is symmetric under all eight, and so is a square torus, so every one of
            //them is the same pattern.
            int orientation = next.AlongLength && _tall ? 4 + _random.Next(4) : _random.Next(8);

            Stamp(next, orientation);
        }

        private void Stamp(Pattern pattern, int orientation)
        {
            //What was showing becomes the previous generation, so it fades out as the new pattern appears, and the
            //stale test starts over.
            Array.Copy(_current, _previous, SIZE);
            Array.Clear(_current);
            Array.Clear(_older);

            //Centred on the board, so a face that maps the board's centre to its own shows the whole pattern.
            int width = 0, height = 0;
            foreach (Part part in pattern.Parts)
            {
                Measure(part.Cells, out int partWidth, out int partHeight);
                width = Math.Max(width, part.X + partWidth);
                height = Math.Max(height, part.Y + partHeight);
            }

            int originX = (SIZE - width) / 2;
            int originY = (SIZE - height) / 2;

            foreach (Part part in pattern.Parts)
            {
                int row = 0, column = 0;

                foreach (char c in part.Cells)
                {
                    if (c == '/')
                    {
                        row++;
                        column = 0;
                        continue;
                    }

                    if (c == 'O') Set(originX + part.X + column, originY + part.Y + row, orientation);
                    column++;
                }
            }

            _pattern = pattern;
            _staleGenerations = 0;
            Generation = 0;
        }

        private static void Measure(string cells, out int width, out int height)
        {
            width = 0;
            height = 1;

            int column = 0;
            foreach (char c in cells)
            {
                if (c == '/')
                {
                    height++;
                    column = 0;
                    continue;
                }

                column++;
                width = Math.Max(width, column);
            }
        }

        private void Set(int x, int y, int orientation)
        {
            x &= SIZE - 1;
            y &= SIZE - 1;

            if ((orientation & 4) != 0) (x, y) = (y, x);
            if ((orientation & 1) != 0) x = SIZE - 1 - x;
            if ((orientation & 2) != 0) y = SIZE - 1 - y;

            _current[y] |= 1u << x;
        }
    }
}
