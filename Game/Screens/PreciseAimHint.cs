namespace BS3D.Screens
{
    /// <summary>
    /// <b>When the precise-aim hint is due</b> (#1012). The owner's rule (2026-10-28), after the first one - a dial of
    /// "real leans" and gaps that let him fire miss after miss and never see it: <b>when a shot makes no match more than
    /// twice, show the hint to aim with the right button.</b> So the hint is due on the <see cref="MISSES_IN_A_ROW"/>th
    /// shot in a row that drops nothing, fired outside precise aim.
    /// <para>
    /// A shot that scores, or one fired from the lean (<see cref="LEANED_BLEND"/>), starts the count again: the player is
    /// matching, or already doing what the hint says. Showing the hint starts it again too, so a player who goes on missing
    /// outside the lean is shown it every <see cref="MISSES_IN_A_ROW"/> misses, and never between. Counted in shots, never
    /// in seconds, so a player who stops to think is not nagged for thinking. Pure, a handful of fields, no allocation.
    /// </para>
    /// </summary>
    internal sealed class PreciseAimHint
    {
        /// <summary>The unscored shots in a row, fired outside the lean, that show the hint: "more than twice".</summary>
        public const int MISSES_IN_A_ROW = 3;

        /// <summary>How far precise aim's blend must be in when a shot leaves for the shot to count as fired from the lean.</summary>
        public const float LEANED_BLEND = 0.5f;

        private int _misses;
        private bool _lastShotLeaned;

        /// <summary>The unscored shots in a row fired outside the lean, for the log line.</summary>
        public int MissesInARow => _misses;

        /// <summary>A shot has left the barrel; <paramref name="leaned"/> says it was fired from precise aim.</summary>
        public void NoteShotFired(bool leaned) => _lastShotLeaned = leaned;

        /// <summary>
        /// The shot last fired has resolved, <paramref name="scored"/> or dropping nothing. Returns whether the hint is due:
        /// this is the <see cref="MISSES_IN_A_ROW"/>th shot in a row that dropped nothing outside the lean.
        /// </summary>
        public bool NoteShotResolved(bool scored)
        {
            if (scored || _lastShotLeaned)
            {
                _misses = 0;
                return false;
            }

            return ++_misses >= MISSES_IN_A_ROW;
        }

        /// <summary>The hint has been put on screen: the next one needs a fresh run of misses.</summary>
        public void NoteShown() => _misses = 0;

        /// <summary>A level is starting: a run of misses on the last level is not this level's.</summary>
        public void BeginLevel()
        {
            _misses = 0;
            _lastShotLeaned = false;
        }
    }
}
