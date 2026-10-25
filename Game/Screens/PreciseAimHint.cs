namespace BS3D.Screens
{
    /// <summary>
    /// <b>When the precise-aim hint is due</b> (#1012). The owner's ruling (2026-10-24), after an outside player played all
    /// 140 levels from the overview and, the owner believes, never used precise aim after its card: when a shot fired
    /// <b>outside</b> precise aim drops nothing, the player is shown that they can aim precisely; and shown again if they
    /// used it only for a moment, or have not used it for a long time.
    /// <para>
    /// <b>Counted in shots, not seconds</b>, so a player who stops to think is not nagged for thinking, and every frame rate
    /// counts alike. A <b>real lean</b> is a hold of <see cref="REAL_LEAN_SECONDS"/> unbroken, a look down the barrel and not
    /// a tap; a player who has made one within the last <see cref="LONG_SHOTS"/> shots is using the lean and is never told.
    /// Otherwise the first shot that drops nothing outside the lean shows the hint, and after it at least
    /// <see cref="MIN_SHOTS_BETWEEN"/> shots pass before it may come back. The tutorial's own once-a-level arming
    /// (<see cref="Tutorial.Lesson.AimHint"/>) caps it again.
    /// </para>
    /// <para>
    /// Kept for the life of the gameplay screen, so across the levels of one run of the game, and never saved: a player
    /// who comes back another day and still does not lean is told again once. Pure, a handful of fields, no allocation.
    /// </para>
    /// </summary>
    internal sealed class PreciseAimHint
    {
        /// <summary>An unbroken hold this long, in seconds, is a real look down the barrel; shorter is "only for a moment".</summary>
        public const float REAL_LEAN_SECONDS = 1f;

        /// <summary>
        /// Shots without a real lean that are "a long time": about half a level of the campaign's budgets (a median of 48),
        /// so a player who leans now and then never sees the hint, and one who has stopped leaning sees it within a level.
        /// </summary>
        public const int LONG_SHOTS = 25;

        /// <summary>The fewest shots between two showings: the hint is a reminder, not a running commentary.</summary>
        public const int MIN_SHOTS_BETWEEN = 10;

        /// <summary>How far precise aim's blend must be in when a shot leaves for the shot to count as fired from the lean.</summary>
        public const float LEANED_BLEND = 0.5f;

        //Far enough past LONG_SHOTS to read as "never", and small enough that counting on cannot overflow
        private const int NEVER = 1 << 20;

        private float _hold;
        private bool _holdCounted;
        private int _shotsSinceRealLean = NEVER;
        private int _shotsSinceShown;
        private bool _shown;
        private bool _lastShotLeaned;

        /// <summary>The shots fired since the last real lean, for the log line; <c>-1</c> when there has been none.</summary>
        public int ShotsSinceRealLean => _shotsSinceRealLean >= NEVER ? -1 : _shotsSinceRealLean;

        /// <summary>Precise aim's button held or not this frame, over <paramref name="elapsedSeconds"/> of real time.</summary>
        public void NoteLean(bool held, float elapsedSeconds)
        {
            if (!held)
            {
                _hold = 0f;
                _holdCounted = false;
                return;
            }

            _hold += elapsedSeconds;

            if (!_holdCounted && _hold >= REAL_LEAN_SECONDS)
            {
                _holdCounted = true;
                _shotsSinceRealLean = 0;
            }
        }

        /// <summary>A shot has left the barrel; <paramref name="leaned"/> says it was fired from precise aim.</summary>
        public void NoteShotFired(bool leaned)
        {
            _lastShotLeaned = leaned;
            if (_shotsSinceRealLean < NEVER) _shotsSinceRealLean++;
            if (_shotsSinceShown < NEVER) _shotsSinceShown++;
        }

        /// <summary>
        /// Whether a shot that has just resolved dropping nothing calls for the hint: it was fired outside precise aim, the
        /// player has made no real lean for <see cref="LONG_SHOTS"/> shots (or ever), and it has not been shown within
        /// <see cref="MIN_SHOTS_BETWEEN"/> shots.
        /// </summary>
        public bool DueAfterUnscoredShot =>
            !_lastShotLeaned
            && _shotsSinceRealLean >= LONG_SHOTS
            && (!_shown || _shotsSinceShown >= MIN_SHOTS_BETWEEN);

        /// <summary>The hint has been put on screen.</summary>
        public void NoteShown()
        {
            _shown = true;
            _shotsSinceShown = 0;
        }
    }
}
