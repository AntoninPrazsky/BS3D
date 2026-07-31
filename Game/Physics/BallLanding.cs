using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;

namespace BS3D.Physics
{
    /// <summary>
    /// What a shot did when it landed in the lattice — everything the game needs to answer for one attach, in
    /// one payload.
    /// <para>
    /// A struct rather than a longer and longer parameter list on <see cref="BallContactEventHandler.BallLanded"/>:
    /// the event started as "what fell", grew a world position for the floating score, then a ball type for its
    /// tint, then the cell for the ripple. Four positional arguments of which two are three-component vectors
    /// is a signature nobody can read at the call site, and the next thing this moment needs would make it five.
    /// </para>
    /// </summary>
    public readonly struct BallLanding
    {
        /// <summary>The group the shot completed and everything that fell with it. Zero of both means it stuck.</summary>
        public readonly BallsReleased Released;

        /// <summary>
        /// Where the ball came to rest, in <b>world</b> space — the cell it was snapped into, not the raw
        /// contact point a diameter off it and not the body position the constraints are about to drag. It is
        /// what a floating score rises from.
        /// </summary>
        public readonly Vector3 World;

        /// <summary>
        /// The shot ball's type, which is also the colour of whatever group it completed — a match is three or
        /// more of <i>one</i> colour touching, so there is only ever one colour to report.
        /// </summary>
        public readonly BallType Type;

        /// <summary>
        /// The same place in the <b>lattice</b> frame. The ripple walks outwards from here over the balls that
        /// touch each other, which is a question about the grid and cannot be asked of a world position.
        /// </summary>
        public readonly XZLevel Cell;

        public BallLanding(BallsReleased released, Vector3 world, BallType type, XZLevel cell)
        {
            Released = released;
            World = world;
            Type = type;
            Cell = cell;
        }
    }
}
