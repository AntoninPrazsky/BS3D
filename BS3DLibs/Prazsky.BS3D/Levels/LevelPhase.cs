namespace Prazsky.BS3D.Levels
{
    /// <summary>
    /// Where a level being played stands in its own flow (#582): still being played, won and holding its beat,
    /// lost on the line and holding the camera on it, or over with its result page up. One value where the Game
    /// used to infer the phase from a countdown, two flags and an outcome field — every door that must refuse a
    /// decided level (the gun, a landing, a miss, the infection, the ceiling's step, a powerup) asks this one.
    /// <para>
    /// <b>The order is load-bearing:</b> the phases run forward and never back, except that building a level
    /// starts it over at <see cref="Playing"/> from wherever the last one stood. <see cref="LevelPhases.CanEnter"/>
    /// is that rule, and the Game's one transition method refuses anything else.
    /// </para>
    /// <para>
    /// <b>What is deliberately not a phase:</b> the camera takeovers — the chapter's establishing tour, the drop
    /// cinematic and the line loss's own flight. They are poses and blends that run <i>beside</i> a phase (a drop
    /// cinematic can play during <see cref="Playing"/> and hold the <see cref="ClearedBeat"/>'s countdown), so a
    /// phase for each would be a product of states rather than a sequence of them.
    /// </para>
    /// </summary>
    public enum LevelPhase
    {
        /// <summary>The level is being played: the gun answers, landings score and the endings are tested.</summary>
        Playing,

        /// <summary>
        /// The field has emptied and the celebration is running, but no page is up yet: the collapse the player
        /// earned is held on screen for a beat (longer while a drop cinematic holds it) before the result.
        /// </summary>
        ClearedBeat,

        /// <summary>
        /// The cluster crossed the line and the camera is flown at the crossing (#434): the level is lost and
        /// its figures are final, but the page waits for the flight's hold to end.
        /// </summary>
        LossHold,

        /// <summary>
        /// The result page is up, the record written and the figures snapshotted. The world goes on running
        /// under the page (#241), the rules do not.
        /// </summary>
        Over,
    }

    /// <summary>The rules over <see cref="LevelPhase"/> that do not need a running level — see its remarks.</summary>
    public static class LevelPhases
    {
        /// <summary>
        /// Whether a level may go from <paramref name="from"/> to <paramref name="to"/>: forward along
        /// Playing → ClearedBeat → Over or Playing → LossHold → Over (or straight Playing → Over, a loss with
        /// no flight to hold it), and back to <see cref="LevelPhase.Playing"/> from anywhere, which is a level
        /// being built. Nothing else — above all nothing out of <see cref="LevelPhase.Over"/> but a new build,
        /// and no second ending on top of the first.
        /// </summary>
        public static bool CanEnter(LevelPhase from, LevelPhase to) => (from, to) switch
        {
            (_, LevelPhase.Playing) => true,
            (LevelPhase.Playing, LevelPhase.ClearedBeat or LevelPhase.LossHold or LevelPhase.Over) => true,
            (LevelPhase.ClearedBeat or LevelPhase.LossHold, LevelPhase.Over) => true,
            _ => false,
        };

        /// <summary>
        /// The level's outcome is settled — anything but <see cref="LevelPhase.Playing"/>. Nothing belonging to
        /// playing it may happen past this.
        /// </summary>
        public static bool IsDecided(LevelPhase phase) => phase != LevelPhase.Playing;

        /// <summary>
        /// The level's arithmetic is final: the result page is up, or the line loss is holding it back
        /// (<see cref="LevelPhase.LossHold"/>, about 1.7 s in which the level is lost and nothing may score but
        /// the page does not exist yet). Not true during <see cref="LevelPhase.ClearedBeat"/>, whose figures a
        /// shot still in the air may yet move.
        /// </summary>
        public static bool IsFinal(LevelPhase phase) => phase is LevelPhase.LossHold or LevelPhase.Over;
    }
}
