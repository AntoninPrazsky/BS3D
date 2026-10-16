using BS3D.Screens;
using Prazsky.BS3D.GameStructure;

namespace BS3D
{
    /// <summary>
    /// The host's door for the dev build's debug row on the pause page (#876): the row's buttons call it, and it reaches the
    /// paused session. Beside <see cref="ToggleUnlockAll"/> in what it is - a development convenience that a release must not
    /// have - and in how it refuses.
    /// </summary>
    public partial class BS3DGame
    {
        /// <summary>
        /// Loads a <paramref name="kind"/> into the paused level's gun (<see cref="GameplayScreen.LoadDebugKind"/>) and has the
        /// pause page say what the bore holds now. <b>Inert in a release build</b>, as <see cref="ToggleUnlockAll"/> is: the
        /// page draws no row there (<see cref="DebugKindRow.Kinds"/>), and the door refuses here and again where the magazine
        /// is written, so no argument, test hook or future caller can arm a round in a shipped build through a row the player
        /// cannot see. The gate is the build's own word (<see cref="BuildVersion.Release"/>), not <c>#if DEBUG</c>.
        /// </summary>
        internal void LoadDebugKind(BallKind kind)
        {
            if (!DebugKindRow.Available(BuildVersion.Release) || !HasSession) return;

            _gameplayScreen.LoadDebugKind(kind);
            _pausePage.Refresh();
        }

        /// <summary>
        /// What the paused level's gun holds as its next round, in the words of the pause page's line under the debug row
        /// (<see cref="DebugKindRow.Describe"/>).
        /// </summary>
        internal string NextRoundDescription => HasSession ? DebugKindRow.Describe(_gameplayScreen.NextRoundKind) : string.Empty;
    }
}
