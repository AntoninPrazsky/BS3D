using Prazsky.BS3D.GameStructure;

namespace BS3D.Screens
{
    /// <summary>
    /// A player-side, session-scoped resource the player activates by choice — orthogonal to both
    /// <see cref="Prazsky.BS3D.GameStructure.BallType"/> and <see cref="Prazsky.BS3D.GameStructure.BallKind"/>
    /// (#392). Activating one does not create a new kind of ball; it reaches into the existing gun/magazine
    /// machinery instead (a future direct-state kind, touching some other subsystem with no shot involved at
    /// all, is explicitly out of #392's own scope). A small byte enum, the same closed-set idiom every other
    /// axis in this codebase already is.
    /// </summary>
    internal enum PowerupKind : byte
    {
        /// <summary>Not a power-up — the charge array's own default, and what "nothing granted" reads as.</summary>
        None = 0,

        /// <summary>
        /// Exchanges the muzzle slot with the one behind it (#392's own proof of the activation contract,
        /// the shape with the fewest moving parts) — deferring the ball about to fire by one shot. The
        /// queue's only reorder; everything else this game does to a magazine changes a slot's colour, never
        /// its place.
        /// </summary>
        Swap = 1,

        /// <summary>
        /// Lifts the ceiling's glass one step back up (#213, the second of its order after Swap): the only thing the
        /// player can do about the game's one pressure, so it returns time the ceiling took rather than adding a ball.
        /// Not spent, and refused with the gun's dry "no", while the glass is still where the level hung it (there is
        /// no step to give back) or, on a tall level, while the lift would take the column's underside out of reach
        /// (#843, <c>GameplayScreen.BrakeCanLift</c>), and never lifts it past that height. See <c>CeilingDescent.Brake</c> (its own file is not in the tests' compile, which takes this one).
        /// </summary>
        Brake = 2,

        /// <summary>
        /// The anchor cut (#213, the third of its order): turns the round in the bore into a
        /// <see cref="BallKind.Cutter"/>, which destroys the whole storey of the ball it strikes instead of sticking, so what
        /// hung on that storey falls (#692); aimed where it would take more than the lower half of the cluster it is refused like an aim past the
        /// elevation clamp (<c>_cutterRefused</c>). Refused while the round is already a cutter or a wildcard (a wildcard turned into a cutter
        /// would be a charge spent to lose a wildcard).
        /// </summary>
        Cut = 3,
    }
}
