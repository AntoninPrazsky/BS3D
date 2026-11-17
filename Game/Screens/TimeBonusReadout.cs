using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Scoring;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>What the time bonus readout under the score shows</b> (#1045), apart from how it is drawn (<c>PlayHud.DrawTimeBonus</c>)
    /// so a test can hold it: the figure is <see cref="ScoreKeeper.TimeBonusAt"/> itself, so what the player watches drain is
    /// what the clear throws into the score, and the bar between the figure's once-a-second steps drains smoothly.
    /// </summary>
    internal static class TimeBonusReadout
    {
        /// <summary>The readout's words, lower case as the balls left's are; the award at the clear says them in capitals
        /// (<see cref="AwardZoom.TIME_BONUS_CAPTION"/>), and the result page's row is called by them.</summary>
        internal const string LABEL = "time bonus";

        /// <summary>
        /// The clock the readout is read off in <paramref name="phase"/>: the play clock (<paramref name="seconds"/>) while the
        /// level is played; through a won level's cleared beat the clock frozen at the clear (<paramref name="endSeconds"/>,
        /// <c>LevelRun.EndSeconds</c>), so the readout stands at the figure the clear was paid and fades with the other playing
        /// readouts while the streak keeps its place under it; and none (−1) once a level is lost or its page is up, whose
        /// bonus was never earned or has been counted.
        /// </summary>
        internal static float Clock(LevelPhase phase, float seconds, float endSeconds) =>
            phase == LevelPhase.ClearedBeat ? endSeconds : LevelPhases.IsDecided(phase) ? -1f : seconds;

        /// <summary>
        /// The figure shown at <paramref name="playSeconds"/> on the clock <see cref="Clock"/> hands over: the bonus a clear then
        /// is paid. −1 when there is nothing to show: a level with no par, or no clock (<paramref name="playSeconds"/> negative).
        /// </summary>
        internal static int Shown(ScoreKeeper score, float playSeconds) =>
            playSeconds < 0f || score.TimeBonusMax <= 0 ? -1 : score.TimeBonusAt(playSeconds);

        /// <summary>
        /// How full the bar is, 1 at the start to 0 at <see cref="ScoreKeeper.TimeBonusZeroAtPars"/> pars: continuous, so it
        /// drains smoothly between the figure's steps. 0 on a level with no par.
        /// </summary>
        internal static float Share(ScoreKeeper score, float playSeconds) =>
            score.ParSeconds is int par && playSeconds >= 0f
                ? Math.Clamp(1f - playSeconds / (ScoreKeeper.TimeBonusZeroAtPars * par), 0f, 1f)
                : 0f;
    }
}
