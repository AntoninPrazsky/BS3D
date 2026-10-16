using Prazsky.BS3D.GameStructure;
using System;

namespace BS3D.Screens
{
    /// <summary>The session's half of the dev build's debug row (#876): what the pause page's buttons do to a paused level.</summary>
    internal sealed partial class GameplayScreen
    {
        /// <summary>
        /// What the round in the bore is - the next one to fire - for the pause page's line under the debug row. Ordinary
        /// until something else was dealt, power-up'd or loaded there.
        /// </summary>
        internal BallKind NextRoundKind => _magazine?.Slot(0).Kind ?? BallKind.Normal;

        /// <summary>
        /// A press on one of the debug row's buttons (#876): turns the round in the bore into a <paramref name="kind"/>
        /// (<see cref="DebugKindRow.Load"/>, which refuses in a release) and marks the run, so that the ending sends no score
        /// to the online boards (<c>ShowResultScreen</c>, <see cref="LevelRun.DebugKindLoaded"/>). It is no power-up and no
        /// shot - nothing is spent, no charge, no ball, nothing the score or the ceiling's cadence counts - and it writes
        /// nothing into the save. One <c>[debug]</c> line says what was loaded, over what, and that the run is marked.
        /// </summary>
        internal void LoadDebugKind(BallKind kind)
        {
            if (_magazine == null) return;

            BallKind was = _magazine.Slot(0).Kind;
            if (!DebugKindRow.Load(BuildVersion.Release, kind, _magazine))
            {
                Console.WriteLine($"[debug] {kind} was not loaded: " + (DebugKindRow.Available(BuildVersion.Release)
                    ? "a shot cannot carry that kind"
                    : $"this build is a release ({BuildVersion.Release})"));
                return;
            }

            _run.DebugKindLoaded = true;

            Console.WriteLine($"[debug] loaded {kind} into the bore (it was {DebugKindRow.Describe(was)}), colour {_magazine.Slot(0).Type};"
                + " no charge, shot or score is spent, and this run will not be sent to the online boards");
        }
    }
}
