using Prazsky.BS3D.GameStructure;
using System;
using System.Collections.Generic;

namespace BS3D.Screens
{
    /// <summary>
    /// The testing levers the command line hands the play session (#582), as one immutable value built once from
    /// <see cref="LaunchOptions"/> and given to <see cref="GameplayScreen"/> at construction.
    /// <para>
    /// Until #582 each was a property of its own on <see cref="BS3DGame"/> — <c>ForcedStreak</c>,
    /// <c>ForcedWildcardEvery</c>, <c>ForcedPowerups</c>, <c>ForceLaserWarning</c>, <c>StagedLineLossSeconds</c>,
    /// <c>TryTakeForcedDetonation</c>, <c>TutorialMode</c>, <c>BallStyleOverride</c>, <c>StartupLevelFile</c> —
    /// which the session read off the host wherever it happened to need one, so "what can a test change about
    /// play" was a question answered by grepping for <c>Game.</c>. Now it is this type's member list. Every one
    /// is a <b>standing</b> state of the run rather than an action — the actions are <see cref="StartupScript"/>'s
    /// — and none is ever set by a player: an ordinary launch gets <see cref="From"/> of an empty command line,
    /// every member at its "nobody said" value.
    /// </para>
    /// <para>
    /// The host keeps the one instance too, because two of the levers are also read outside the session: the
    /// front end's preview honours <see cref="BallStyleOverride"/>, and the startup script is told
    /// <see cref="StartupLevelFile"/>. One value read by all three is what stops them disagreeing.
    /// </para>
    /// </summary>
    /// <param name="ForcedStreak">The <c>streak=</c> argument (#180): the multiplier the HUD's streak readout
    /// shows, or null for the keeper's own. The capped state takes five consecutive scoring shots to reach, and
    /// this changes the display only, never the scoring, so the lever cannot alter the thing it is there to look
    /// at.</param>
    /// <param name="ForcedWildcardEvery">The <c>wildcard=</c> argument (#330): one in how many loaded balls is a
    /// wildcard, overriding every level entry's own <c>wildcardEvery</c>; 0 leaves each level's rule alone. The
    /// kind is built and no shipped level hands one out, and a wildcard cannot be authored into a map — it is a
    /// ball the gun loads — so this is the only door to it. Unlike <c>streak=</c> it <i>does</i> change play,
    /// which is the point.</param>
    /// <param name="ForcedPowerups">The <c>powerups=</c> argument (#392): each level's starting power-up charges
    /// as <c>"kind:count"</c> pairs separated by commas (<c>powerups=swap:1</c>), or null for none — every
    /// shipped level today. Kept as the raw string: only the session knows the <c>PowerupKind</c> enum it
    /// names, and parses it leniently per level.</param>
    /// <param name="ForceLaserWarning">The <c>lasers</c> argument: pins the floor alarm's laser net on. Reaching it
    /// honestly means playing a level to within two ceiling steps of losing it, which can no more be scripted
    /// than clearing one can.</param>
    /// <param name="StagedLineLossSeconds">The <c>lineloss=</c> argument (#434): seconds into a level at which the
    /// line's loss is staged, or 0 for never. A real one takes a descending ceiling and a couple of dozen shots,
    /// and the Game takes no synthetic input, so without this the moment is unphotographable.</param>
    /// <param name="DetonateSeconds">The <c>detonate=</c> argument (#389): wall-clock seconds — <c>shot=</c>'s
    /// clock, so the two can be written against each other — at which the session sets off one of the level's
    /// bombs, in order; empty for none. A blast needs a shot landed beside a bomb, which no script can aim. It
    /// changes play, because the bomb really goes. How far through the schedule a run has got is the session's
    /// own state, not this value's.</param>
    /// <param name="TutorialMode">The <c>tutorial</c> argument (#189): offer every tutorial card as if none had
    /// been taught and record none — with the real detection, or as a reel on a clock (<c>tutorial=demo</c>).
    /// The cards are gated on the save, and a run that taught them for real would write to the owner's
    /// save.</param>
    /// <param name="BallStyleOverride">The <c>balls=</c> argument (#258): what every ball is drawn as whatever its
    /// level says, or null for each map in its own material. A testing lever and not a setting: the style is a
    /// property of the map, and a player who could override it globally would be overriding its author.</param>
    /// <param name="StartupLevelFile">The <c>levelfile=</c> argument (#332): a level file to play <b>instead of
    /// the set's</b>, pinned for the whole run, or null. The set is the campaign, and a level built to try a
    /// mechanic out is not part of it. It replaces the path for <b>every</b> entry, so a run cannot wander off
    /// the file it was pinned to by finishing one — which is why the session asks it per level.</param>
    /// <param name="Seed">The <c>seed=</c> argument (#582): the seed every level's session generator is built
    /// from — the magazine's deal, the transmute's replacements, the drop cinematic's and the chapter intro's
    /// rolls — or null to roll a fresh one per level, which is what a player gets. Either way the session prints
    /// it as <c>[session] seed N</c>, so a playtest report names the seed that replays its deal.</param>
    internal sealed record SessionTestOptions(
        int? ForcedStreak,
        int ForcedWildcardEvery,
        string ForcedPowerups,
        bool ForceLaserWarning,
        float StagedLineLossSeconds,
        IReadOnlyList<float> DetonateSeconds,
        Tutorial.Mode TutorialMode,
        BallStyle? BallStyleOverride,
        string StartupLevelFile,
        int? Seed)
    {
        /// <summary>
        /// Reads the levers out of what the command line said. The one place their interpretation lives: any
        /// spelling of <c>tutorial=</c> but <c>demo</c> is the plain force (a mistyped reel still shows the cards,
        /// and says so by waiting for the player rather than running on), and a blank <c>levelfile=</c> is none.
        /// </summary>
        internal static SessionTestOptions From(LaunchOptions launch) => new(
            ForcedStreak: launch.Streak,
            ForcedWildcardEvery: launch.WildcardEvery,
            ForcedPowerups: launch.Powerups,
            ForceLaserWarning: launch.Lasers,
            StagedLineLossSeconds: launch.LineLoss,
            DetonateSeconds: launch.DetonateSeconds ?? Array.Empty<float>(),
            TutorialMode: launch.Tutorial == null ? Tutorial.Mode.Normal
                : string.Equals(launch.Tutorial, "demo", StringComparison.OrdinalIgnoreCase) ? Tutorial.Mode.Demo
                : Tutorial.Mode.Force,
            BallStyleOverride: launch.BallStyle,
            StartupLevelFile: string.IsNullOrWhiteSpace(launch.LevelFile) ? null : launch.LevelFile,
            Seed: launch.Seed);
    }
}
