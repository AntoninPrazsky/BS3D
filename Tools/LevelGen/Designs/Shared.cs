using Prazsky.BS3D.GameStructure;
using System;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// The helpers the designs of <b>more than one block</b> use. Every other helper lives in the one block file
    /// whose designs use it, so a new design looks here first and then in the block it resembles; a helper moves
    /// here the day a second block uses it. Split out of <c>Program.cs</c> in #386.
    /// </summary>
    internal static partial class Program
    {

        //WHICH COLOUR EACH WALL WEARS, indexed by OneWall and turned one step a shell by OneColour. Four
        //walls against three colours means one colour is on two of them, and WHICH two decides whether the
        //player can see all three at once. The gun starts on +Z looking at the axis and the pyramid hangs
        //point-down, so what it faces is the +Z wall with the -X and +X walls falling away on either side
        //and -Z hidden behind the mass: the repeat goes on the pair the player cannot see together (-Z and
        //+Z), which puts the other two colours on the flanks, in view from the first shot. Putting the
        //repeat on the FLANKS instead would show red and green only, which is the fault the banded design
        //was written to fix. Facing pair also means the repeat never meets itself at a corner, where one
        //colour on two adjoining faces would be one group down the whole edge.
        private static readonly int[] ONE_WALLS = { 0, 2, 0, 1 };

        #region Colour helpers

        private static BallType Band(int band, BallType[] palette) => palette[band % palette.Length];

        /// <summary>
        /// Reads a bitmap written as text — the <see cref="SYMBOL_INK"/> characters are the symbol's inks and
        /// anything else is background. Rows are top-first, so the array reads in source exactly as the level
        /// reads in the game, which is the whole reason for spelling a picture out rather than solving it from a
        /// formula: a mistake in it is visible in the diff.
        /// </summary>
        private static char PixelAt(string[] bitmap, int column, int row) =>
            row >= 0 && row < bitmap.Length && column >= 0 && column < bitmap[row].Length
                ? bitmap[row][column]
                : '.';

        //Angular wedges. twist shears the boundary with radius, which is what turns a cross into a spiral.
        private static BallType Sector(float ang, float twist, int sectors, BallType[] palette) =>
            palette[SectorIndex(ang, twist, sectors) % palette.Length];

        /// <summary>
        /// Which wedge a point is in, 0 to <paramref name="sectors"/> − 1. Split out of <see cref="Sector"/>,
        /// whose behaviour is unchanged, because a wedge index is also a <i>term</i> a design can add something
        /// to: <see cref="Lantern"/> rolls the palette by the course as well as by the wedge, which turns six
        /// vertical staves of dozens into panes of a dozen without a horizontal band anywhere.
        /// </summary>
        private static int SectorIndex(float ang, float twist, int sectors)
        {
            float turns = (ang / MathF.Tau) + 0.5f + twist;      //0..1 around the disc, plus the shear
            int index = (int)MathF.Floor(turns * sectors);
            return ((index % sectors) + sectors) % sectors;      //MathF.Floor of a negative turns
        }

        private const float INV_SQRT_TWO = 0.70710678f;

        //A level index's vertical world offset from the layout's own centre, in the same units r is
        //already in (BallsMap.GetRealPosition puts a level at Y = level / sqrt(2)).
        private static float OnionVertical(int i, int depth) => (i - (depth - 1) * HALF) * INV_SQRT_TWO;

        private static float SphereDistance(float r, int i, int depth)
        {
            float dy = OnionVertical(i, depth);
            return MathF.Sqrt(r * r + dy * dy);
        }

        /// <summary>
        /// How far below the <b>anchor layer</b> a level hangs, in the same world units <c>r</c> is already in.
        /// The natural vertical coordinate for a body hung from the glass by its widest section, where
        /// <see cref="OnionVertical"/>'s measure from the layout's own middle is the one for a body centred on its
        /// equator. Both are a level index times <c>1/sqrt(2)</c>, which is the spacing
        /// <c>BallsMap.GetRealPosition</c> puts between levels.
        /// </summary>
        private static float BelowGlass(int i, int depth) => (depth - 1 - i) * INV_SQRT_TWO;

        /// <summary>
        /// The emitter's own centred offsets, rebuilt from the raw lattice indices — the <c>x + shift - axis</c>
        /// line for line, for a design that has to read BOTH a shape and the lattice (see <see cref="ChestPart"/>
        /// and <see cref="LeanRadius"/> for the same rebuild). The shifted-level offset may be taken off the
        /// <i>layout</i> index because <see cref="Emit"/> refuses an odd layout offset, so a layout level and its
        /// field level always agree in parity.
        /// </summary>
        private static void Centred(int x, int z, int i, byte grid, out float dx, out float dz)
        {
            float axis = (grid - 1) * HALF + HALF;
            float shift = (i % 2) * HALF;

            dx = x + shift - axis;
            dz = z + shift - axis;
        }

        /// <summary>
        /// A point in a shape's <b>own</b> frame at a given height: the emitter's polar pair read at an angle
        /// rotated back by <paramref name="turns"/> whole turns. The shape stays a plain pair of discs and only
        /// the frame it is measured in turns with the level, so the occupancy and the colouring cannot disagree
        /// about where the shape is pointing.
        /// </summary>
        /// <param name="along">Signed distance along the shape's long axis.</param>
        /// <param name="across">Signed distance across it.</param>
        private static void Untwist(float r, float ang, float turns, out float along, out float across)
        {
            float local = ang - turns * MathF.Tau;
            along = r * MathF.Cos(local);
            across = r * MathF.Sin(local);
        }

        /// <summary>
        /// An angle folded back into −π…π, so a difference between two of them is the <b>short</b> way round.
        /// A shape that turns with the level is a window on an angle, and without this the window silently
        /// stops working the first time the turn passes π.
        /// </summary>
        private static float WrapAngle(float angle)
        {
            float wrapped = angle % MathF.Tau;

            if (wrapped > MathF.PI) wrapped -= MathF.Tau;
            else if (wrapped < -MathF.PI) wrapped += MathF.Tau;

            return wrapped;
        }

        /// <summary>
        /// Squared lateral distance between a cell at polar (<paramref name="r"/>, <paramref name="ang"/>)
        /// and a point at polar (<paramref name="orbit"/>, <paramref name="centre"/>) — the law of cosines,
        /// shared by every design that hangs a body on an orbiting path.
        /// </summary>
        private static float LateralDistanceSquared(float r, float ang, float orbit, float centre) =>
            r * r + orbit * orbit - 2f * r * orbit * MathF.Cos(ang - centre);

        /// <summary>How many lattice levels below the glass plate this layout level sits - the sweep's own axis.</summary>
        private static int LevelsBelowGlass(int i, int depth) => depth - 1 - i;

        #endregion
    }
}
