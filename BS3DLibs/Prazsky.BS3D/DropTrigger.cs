using System;

namespace Prazsky.BS3D
{
    /// <summary>
    /// <b>When a release is worth the camera</b> (#615, #719): the drop cinematic's trigger as one pure rule, so the Game, the
    /// logic tests and <c>LevelGen --drops</c> (which plays the whole campaign to count how often it fires) read the same
    /// three dials instead of one copy each. It stood in <c>GameplayScreen.TryBeginDropCinematic</c> and
    /// <c>DropCinematic</c> until #719, and the harness #615 measured it with was a throwaway that did not outlive its
    /// session; this is the second pass on the same dials, which is why the rule lives where a tool can call it.
    /// <para>
    /// <b>The shot that clears the level is always worth watching</b> (#424), whatever it drops: both bars below are about a
    /// drop in the middle of a level, and the last three balls are the whole ending. Otherwise a release is worth watching
    /// when it is <b>big enough for the level</b> (<see cref="Floor"/>) <b>and</b> bigger than anything the level has already
    /// shown the player (<see cref="MustBeatBestBy"/>).
    /// </para>
    /// </summary>
    public static class DropTrigger
    {
        /// <summary>
        /// The floor under <see cref="MustBeatBestBy"/>: below this many released balls a shot is not a spectacle however
        /// early in the level it lands. Matched and orphaned together — a shot that drops three of its own colour and brings
        /// nine more down with it is exactly the shot worth watching, and the scorer already pays double for the orphans.
        /// </summary>
        public const int MIN_BALLS = 12;

        /// <summary>
        /// The floor's other half (#615): a drop has to take at least this share of the balls the level <b>started</b>
        /// with, as well as <see cref="MIN_BALLS"/>. Twelve is a big drop on a 250-ball level and a small one on a 1600-ball
        /// level, so on its own it let the biggest levels treat every sizeable shot as a spectacle; a share scales "big" with
        /// the level, as the record rule below already does, and needs no per-level authoring. The absolute twelve still
        /// governs the small levels, where the share is fewer.
        /// <para>
        /// <b>5 % until #719, 9 % since — the owner's playtest, "still too often", about 20 % fewer.</b> Measured with
        /// <c>LevelGen --drops</c> (the shipped set, a greedy and a casual player to a clear, six deals, the real match and
        /// orphan rule, on the lattice): cinematics before the clearing shot, a level, 5 % gave 1.34 (greedy) and 1.25 (casual);
        /// 8 % gave 1.17 and 1.02, 9 % <b>1.11 and 0.96 (−17 % and −23 %)</b>, 10 % 1.04 and 0.90, and a margin of ×3 on
        /// 5 % gave 1.13 and 1.00. The share is the lever because it scales with the level, where the margin only thins a run
        /// of ever bigger drops. The same harness reads #615's old rule (12 balls, ×1.25) at 1.94 and 2.16, which is the 2.0–2.1
        /// that pass reported, so the numbers are comparable. The price is at the low end: before the clearing shot, which
        /// always has one, about one play in nine (greedy) to one in six (casual) shows none, against one in forty and one in
        /// seventeen, and #615's own target was one to two a level. The lattice plays no physics, blasts or ceiling, so the
        /// game's own <c>[cinematic]</c> lines will differ somewhat.
        /// </para>
        /// </summary>
        public const float MIN_SHARE_OF_LEVEL = 0.09f;

        /// <summary>
        /// How much a drop has to beat the level's biggest so far to earn a cinematic — the rest of the trigger, and the part
        /// that makes it rare.
        /// <para>
        /// <b>A fixed count cannot do this job, and the pattern levels are the proof.</b> The threshold was six, and on
        /// <c>One.json</c> that measured as three cinematics in ninety seconds because most shots there drop fewer. The pack
        /// that followed is built out of large primed groups — Pinwheel drops 92 balls on a good shot, Crown 72, Bullseye
        /// (retired in #649) 100 — so six fired on essentially every shot that landed, and the reward for a good shot became
        /// the tax on every shot. Raising the number cannot fix it either: any figure that keeps Pinwheel rare is one Mosaic
        /// (whose best possible shot is 24) can never reach, and a level that never shows one is as wrong as a level that
        /// always does.
        /// </para>
        /// <para>
        /// So the bar is the player's own best this level, which needs no per-level authoring and no re-tuning when a level
        /// is added: <b>"the biggest thing you have done here yet"</b> is what "big" means, whatever the level is made of. The
        /// margin stops a level whose drops creep upwards from firing on every one of them; the first qualifying drop of a
        /// level always fires, because there is no record to beat and one cinematic early is how the effect introduces itself.
        /// </para>
        /// </summary>
        public const float MustBeatBestBy = 2f;

        /// <summary>The fewest balls a release must take for this level to be worth a cinematic, before the record is asked.</summary>
        public static int Floor(int initialBallCount, float shareOfLevel) =>
            Math.Max(MIN_BALLS, (int)MathF.Ceiling(initialBallCount * shareOfLevel));

        /// <summary>
        /// Whether a release of <paramref name="total"/> balls (matched, orphaned and a blast's victims together) is worth the
        /// camera, with the game's own dials.
        /// </summary>
        /// <param name="total">Every ball that left the cluster in this shot.</param>
        /// <param name="clearsLevel">The shot left no removable ball standing.</param>
        /// <param name="initialBallCount">The balls the level started with.</param>
        /// <param name="biggestDrop">The biggest release this level has shown so far, this one not yet counted.</param>
        public static bool IsWorthWatching(int total, bool clearsLevel, int initialBallCount, int biggestDrop) =>
            IsWorthWatching(total, clearsLevel, initialBallCount, biggestDrop, MIN_SHARE_OF_LEVEL, MustBeatBestBy);

        /// <summary>The same rule with the dials named, which is what a measurement of other dials needs.</summary>
        public static bool IsWorthWatching(int total, bool clearsLevel, int initialBallCount, int biggestDrop,
            float shareOfLevel, float mustBeatBestBy) =>
            clearsLevel
            || (total >= Floor(initialBallCount, shareOfLevel) && total >= biggestDrop * mustBeatBestBy);
    }
}
