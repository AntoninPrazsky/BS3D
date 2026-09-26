using BS3D.Effects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Scoring;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>One attempt at one level</b> — the state <see cref="GameplayScreen"/> used to reset by hand, field by
    /// field, across <c>BuildLevel</c>, <c>InstallLevel</c> and <c>TearDown</c> (#582). A level is started by
    /// <c>_run = new LevelRun(index)</c>, so everything here begins at its "nothing has happened yet" value by
    /// construction and a new counter added to it cannot be forgotten by a reset list, because there is no list
    /// any more. The gun-shadow lifetime slip (#562) was that list's kind of failure.
    /// <para>
    /// <b>What is here and what is not.</b> Here: what belongs to the attempt and to nothing longer — which entry
    /// it is and which board, its scorer, its clocks, the shots and seconds at its clear, its biggest release,
    /// its wildcard cadence and count, its power-up charges, its balls' material and the line's grace. Not here:
    /// everything the screen keeps across levels to avoid allocating (the renderers, the reused lists and the
    /// profile array), the simulation (<c>TearDown</c> disposes it outright), the level's flow
    /// (<see cref="LevelPhase"/>, whose one door <c>EnterPhase</c> does each phase's entry work and resets the
    /// ending's data on entering <see cref="LevelPhase.Playing"/> — a phase written here by construction would
    /// skip that door), the ceiling's descent (<c>CeilingDescent.Reset</c>, one call already), the generator
    /// (<c>SeedSession</c> reseeds it per level in one place) and the cinematics, whose own <c>Reset</c> calls
    /// stay with <c>TearDown</c>.
    /// </para>
    /// <para>
    /// Some of it is known only part-way through the install — the board once the file has been read, the ball
    /// count once the map stands, the scorer once that count exists — so those are set by <c>InstallLevel</c>
    /// as it derives them, exactly where the screen's fields were set before. Everything else is never assigned
    /// at the start of a level at all.
    /// </para>
    /// </summary>
    internal sealed class LevelRun
    {
        /// <summary>The run held before any level has been built: entry 0, an unlimited scorer, nothing
        /// counted — the values the screen's own fields started at.</summary>
        internal LevelRun() : this(0) { }

        /// <summary>Starts an attempt at entry <paramref name="index"/> of the host's level set.</summary>
        internal LevelRun(int index) => Index = index;

        /// <summary>Which entry of the host's level set this attempt is playing.</summary>
        internal int Index { get; }

        /// <summary>
        /// Which online board a clear of this level belongs to (#549): the set entry's file and the hash over the
        /// file actually loaded and the entry's rules — computed by the same <see cref="LevelIdentity.Of(LevelSetEntry, byte[])"/>
        /// <c>Tools/ScoreSim</c>'s ceiling table is keyed by. Taken at install, from the bytes of the file that was
        /// played; null for the built-in fallback, which is on no board. A clear is submitted under it
        /// (<c>OnlineSession.SubmitClear</c>, #546), and it is the <c>[levels] Loaded</c> line's last word, which is how
        /// it is compared with the table.
        /// </summary>
        internal LevelIdentity Identity { get; set; }

        /// <summary>
        /// The level's score and ball budget. Built fresh for each level from that entry's rules (at the end of
        /// <c>InstallLevel</c>, once the ball count it rates against exists), so it never carries anything
        /// across; it holds the rules themselves and the screen only feeds it the three events a shot goes
        /// through. An unlimited, empty keeper until then — what the screen's field started at.
        /// </summary>
        internal ScoreKeeper Score { get; set; } = new();

        /// <summary>
        /// How many balls the level <b>started</b> with — the floor the star rating measures the score
        /// against (<see cref="StarRating.Rate"/>). Captured at install, because by the time a rating is
        /// wanted the map is empty: that is what clearing means, and the count is unrecoverable then.
        /// </summary>
        internal int InitialBallCount { get; set; }

        /// <summary>
        /// What this level's balls are made of (#258) — off the level file, and the vinyl beach ball for every
        /// file that says nothing. Kept because the render set is the whole <i>program's</i> and the front end
        /// hangs its own preview through it: what a session draws has to be stated by the session, not left
        /// standing from whatever the menu was showing when Play was pressed.
        /// </summary>
        internal BallStyle BallStyle { get; set; } = BallStyle.Beach;

        /// <summary>
        /// Seconds of play on this level (#546): counted where the frame steps the world, which a pause, an
        /// unfocused window and the page over a finished level never reach — so it is time the player spent
        /// playing, not time the window was open. Real seconds, not the drop cinematic's slowed ones.
        /// </summary>
        internal float Seconds { get; set; }

        /// <summary>
        /// The shots at the moment the field emptied (#546), for the score service; <see cref="ClearSeconds"/>
        /// is the time. Taken then and not when the result page goes up, for <see cref="LevelResult"/>'s own
        /// reason: the level does not stop at the clear, and a player who keeps firing into the empty field
        /// would otherwise send shots that cleared nothing.
        /// </summary>
        internal int ClearShots { get; set; }

        /// <summary>The seconds of play at the moment the field emptied — see <see cref="ClearShots"/>.</summary>
        internal float ClearSeconds { get; set; }

        /// <summary>
        /// The biggest single release of <b>this</b> level so far, which is the bar the drop cinematic has to
        /// clear (see <see cref="DropCinematic.MustBeatBestBy"/>). Per level by construction: carried across, a
        /// small level played after Crown would never show one, and "big" has to mean big <i>here</i> rather
        /// than big in the campaign.
        /// </summary>
        internal int BiggestDrop { get; set; }

        /// <summary>
        /// One in how many loaded balls is a wildcard, or 0 for a level that hands out none — which is every
        /// shipped level today, exactly as no shipped level carried a bomb or a zap the day those were built
        /// (#368 is the issue that put them in the campaign, and this one deliberately leaves that decision
        /// alone). Set at install from the level entry's <c>wildcardEvery</c>, or from the Game's
        /// <c>wildcard=</c> testing argument, <b>before</b> the magazine's refill deals a full queue through it.
        /// <para>
        /// <b>Counted, not diced.</b> The queue shows three balls ahead precisely so the player can plan, and a
        /// wildcard that arrives at a rate they can count is a tool; one that arrives at random is a lottery —
        /// the same argument the zap's colour was decided on (#327).
        /// </para>
        /// </summary>
        internal int WildcardEvery { get; set; }

        /// <summary>
        /// How many balls this level has dealt, which is what <see cref="WildcardEvery"/> counts. Not the shot
        /// count: the queue is dealt <c>Magazine.SIZE</c>-deep before the first shot, so counting shots would put
        /// the first wildcard a whole magazine later than the rule says.
        /// </summary>
        internal int BallsDealt { get; set; }

        /// <summary>
        /// This level's power-up charges (#392), one count per kind, sized off the enum itself so a future kind
        /// added to it needs no second number kept in step by hand. Granted at install
        /// (<c>GrantPowerupCharges</c>) and never persisted to <c>PlayerProgress</c> — every other piece of a
        /// level's state resets exactly this way on a retry, and an inventory that survived one would be the one
        /// exception with no stated reason. One small array a level, never one a frame.
        /// </summary>
        internal readonly int[] PowerupCharges = new int[Enum.GetValues<PowerupKind>().Length];

        /// <summary>
        /// The line's grace — <see cref="ClusterLineWatch"/>'s since #301/#302, so the level generator's sag gate
        /// decides a simulated run by running <i>this</i> rule rather than a second copy of it that could drift
        /// lenient. A field and not a property because it is a mutable struct the screen updates in place. It
        /// needed no reset even as the screen's field — every level begins with its cluster far above the line,
        /// so the first frame zeroes it — and now it has none to need.
        /// </summary>
        internal ClusterLineWatch LineWatch;

        /// <summary>Seconds this level has been running, for the staged loss the <c>lineloss=</c> argument asks
        /// for — advanced by <c>StepLineLoss</c>, on its own rather than read off <see cref="Seconds"/>, as it
        /// was before either moved here.</summary>
        internal float LineLossClock { get; set; }
    }
}
