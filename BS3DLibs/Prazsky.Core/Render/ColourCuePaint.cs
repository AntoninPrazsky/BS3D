using Microsoft.Xna.Framework;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One colour's <b>painted figure</b> (#737), as one draw hands it to the ball shader: which figure, read in which
    /// frame, and the two inks it is painted in. A player could not tell two wool balls apart by colour alone, so
    /// every colour carries a figure of its own beside its hue; which figure a colour carries is a set's business
    /// (<c>Prazsky.BS3D.GameStructure.ColourCues</c>), and this is only what a set says about one colour, packed
    /// the way <c>BallColourCueTechniques.fxh</c> reads it.
    /// <para>
    /// <see cref="A"/>, <see cref="B"/> and <see cref="C"/> are the figure's kind and parameters, whose meaning per
    /// kind is tabulated beside <c>ColourCuePaint</c> in <c>BallColourCue.fxh</c>; <see cref="Frame"/>'s first three
    /// rows are the axes the figure is read along, in object space. The default value is <see cref="IsNone"/>: no
    /// figure, the shipped program draws.
    /// </para>
    /// </summary>
    public readonly struct ColourCuePaint
    {
        /// <summary>The figure's kind in X (1 bands, 2 spots, 3 checks, 4 emblems; 0 none) and its first parameters.</summary>
        public Vector4 A { get; }

        /// <summary>The figure's second parameters.</summary>
        public Vector4 B { get; }

        /// <summary>The figure's third parameters; W is the share of the ball the play camera sees it cover.</summary>
        public Vector4 C { get; }

        /// <summary>The frame the figure is read in: its first three rows are the axes, in object space.</summary>
        public Matrix Frame { get; }

        /// <summary>What the figure is painted in, linear RGB.</summary>
        public Vector3 FigureInk { get; }

        /// <summary>What the rest of the ball is painted in, linear RGB; always the plain colour scaled.</summary>
        public Vector3 GroundInk { get; }

        /// <summary>Whether this is no figure at all: the shipped program draws.</summary>
        public bool IsNone => A.X == 0f;

        public ColourCuePaint(Vector4 a, Vector4 b, Vector4 c, Matrix frame, Vector3 figureInk, Vector3 groundInk)
        {
            A = a;
            B = b;
            C = c;
            Frame = frame;
            FigureInk = figureInk;
            GroundInk = groundInk;
        }
    }
}
