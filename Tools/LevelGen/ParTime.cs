using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Scoring;
using System;
using System.Collections.Generic;
using System.IO;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>Every level's par time</b> (#1045, the owner's decision of 2026-11-17), written into <c>Levels.json</c> as the
    /// entry's <c>par</c>: the seconds of play the time bonus is measured against (<c>ScoreKeeper.TimeBonusAt</c>: whole at
    /// the start, half at par, gone at twice par).
    /// <para>
    /// <b>Par = <see cref="ParRule.FromShots"/> of the shots LevelGen's casual player takes to clear the level</b>: 10 s and
    /// 2.5 s a shot, rounded up to 5 s, the rule in the library so a test can hold it. The casual player (<see cref="ClearProbe.DropPlayer.Casual"/>,
    /// any landing that completes a group, chosen at random) is the one the owner's own clears followed: over his 23 levels
    /// where every attempt was a clear, his shots a clear against this player's had R² 0.81 at a ratio of 1.1, and his time
    /// came to about 10 s and 2 s a shot. The extra half second a shot is a margin for players slower than the author. So
    /// the par says what a level takes from its own shape, and no human play is needed for the 140 levels nobody has
    /// cleared often enough to measure.
    /// </para>
    /// <para>
    /// <b>Played <see cref="DEALS"/> times, seeded by the level's file name</b>, not by its place in the set as
    /// <see cref="DropProbe"/> seeds: the par is in the <see cref="LevelIdentity"/>, so a level moved in the order must keep
    /// its par, or a reorder would start fresh boards for every level it moved.
    /// </para>
    /// <para>
    /// <b>The hook for measured times: <see cref="FromPlay"/>.</b> The proposal the owner decided on says a level with five or
    /// more clears since v0.6.0 (the first release whose play clock stops under the drop camera) takes the median of those
    /// clears instead. The repository holds none of that data (the score service's funnel does, per level, with no version
    /// filter yet), so the table is empty and every par is the formula's. Fill it from an export of clears whose
    /// <c>gameVersion</c> is v0.6.0 or newer, five or more a level, and regenerate: those levels' boards start again.
    /// </para>
    /// </summary>
    internal static class ParTime
    {
        /// <summary>
        /// Plays of the casual player a level, whose mean shots the par is made of. Four times <see cref="DropProbe"/>'s six:
        /// the mean of six is noisy, and seeding the six by file name instead of by place moved 65 of the 140 pars by 5 s or
        /// more (one by 20 s); with 24, 92 of the 140 pars equal the six-play figures the #1045 analysis was fitted with, and
        /// none differ by more than 10 s. It costs LevelGen about 35 s a run.
        /// </summary>
        internal const int DEALS = 24;

        /// <summary>Shots before a play is given up: <see cref="DropProbe"/>'s.</summary>
        private const int SHOT_CAP = 1500;

        /// <summary>
        /// Measured par times by level file (#1045): the median clear, in seconds of the play clock, of a level with five or
        /// more clears from v0.6.0 or newer. Empty: no such export exists yet (see the class doc), and nothing is invented.
        /// A value here replaces the formula for its level, rounded up as the rule's are (<see cref="ParRule.RoundUp"/>).
        /// </summary>
        private static readonly Dictionary<string, float> FromPlay = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The par of the level at <paramref name="levelPath"/>, entered in the set as <paramref name="file"/>, with what it
        /// came from for the log; null when the casual player never clears it on the lattice (a level that only a blast or a
        /// zap can finish), which leaves the level without a time bonus and is printed as a warning — <c>Tools/ScoreSim</c>
        /// then refuses it as unable to earn four stars.
        /// </summary>
        internal static int? Of(string levelPath, string file, out string source)
        {
            if (FromPlay.TryGetValue(file, out float measured))
            {
                source = $"measured {measured:0.#} s";
                return ParRule.RoundUp(measured);
            }

            Level level = Level.Load(levelPath);
            double shots = CasualShots(level, file, out int cleared);

            if (cleared == 0)
            {
                source = "the casual player never cleared it";
                return null;
            }

            int whole = (int)Math.Round(shots, MidpointRounding.AwayFromZero);
            source = $"casual {whole} shots";
            return ParRule.FromShots(whole);
        }

        /// <summary>The casual player's mean shots to clear over <see cref="DEALS"/> plays, and how many of them cleared.</summary>
        private static double CasualShots(Level level, string file, out int cleared)
        {
            int seed = StableSeed(file);
            double sum = 0;
            cleared = 0;

            for (int deal = 0; deal < DEALS; deal++)
            {
                ClearProbe.DropRun run = ClearProbe.PlayDrops(level.Map, ClearProbe.DropPlayer.Casual, unchecked(seed + deal), SHOT_CAP);
                if (!run.Cleared) continue;

                cleared++;
                sum += run.Shots;
            }

            return cleared == 0 ? 0 : sum / cleared;
        }

        /// <summary>
        /// A seed from the level's file name, the same on every machine and every run: FNV-1a over its characters (a
        /// string's own hash code is randomised per process).
        /// </summary>
        internal static int StableSeed(string file)
        {
            uint hash = 2166136261;
            foreach (char c in Path.GetFileName(file).ToLowerInvariant())
            {
                hash ^= c;
                hash *= 16777619;
            }

            return (int)(hash & 0x7fffffff);
        }
    }
}
