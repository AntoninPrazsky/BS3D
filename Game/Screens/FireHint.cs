using System;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>The "fire" hint for a player who has stopped</b> (#925): in the first five levels, when no shot has left the gun for
    /// <see cref="FIRST_PAUSE_SECONDS"/> and nothing else is speaking to the player, a short bold chip near the aim point says
    /// <see cref="CAPTION"/> beside the fire button, pulses once a second, leaves the instant a shot is fired and comes back in
    /// that level only after a much longer pause (<see cref="LATER_PAUSE_SECONDS"/>). Pure rules over plain values, so the
    /// tests hold them without a session: the session feeds <see cref="Update"/> its <see cref="Distractions"/> and calls
    /// <see cref="NoteShot"/>, <c>PlayHud</c> draws what <see cref="Presence"/> and <see cref="Age"/> say.
    /// <para>
    /// The owner's playtest of 2026-10-18: Cannonball teaches that the gun can turn all the way round, the player does it, and
    /// then nothing tells a player who has forgotten the other half of the game to fire. The cards teach one thing each and
    /// leave when it is done; nothing watched the player after that.
    /// </para>
    /// <para>
    /// <b>The pause is measured, not guessed.</b> The owner's own play of the first chapter (his <c>Stats.json</c>, 2026-10-18:
    /// the five levels cleared in 46 shots over 231.8 s of play clock) is five seconds a shot including everything that is not
    /// aiming, and the five levels' own averages run from 2.9 s to 13.5 s a shot. Twice the typical gap is
    /// <see cref="FIRST_PAUSE_SECONDS"/>: past what a player who is playing takes between shots, and short of
    /// "this player has stopped". Only a <i>shot</i> ends the pause: turning, walking and aiming are exactly what a hesitating
    /// player does.
    /// </para>
    /// <para>
    /// <b>The count restarts whenever something else has the player's attention</b> (<see cref="Distractions"/>): a tutorial card up or
    /// still queued, a camera takeover, the ceiling's step or the floor's alarm, a shot still in the air, a decided level, the
    /// tutorial switched off. The pause is then counted from the moment all of it is over, so the chip never arrives on the
    /// heels of a card or an alarm, and never over one.
    /// </para>
    /// </summary>
    internal sealed class FireHint
    {
        /// <summary>
        /// Everything that has the player's attention, or would make the chip untrue, as plain flags the session fills in each
        /// frame (a struct, so nothing is allocated). <see cref="Any"/> is the one question <see cref="Update"/> asks.
        /// </summary>
        internal struct Distractions
        {
            /// <summary>The Tutorial row of the Settings is off: the hint is a tutorial's, and off means off.</summary>
            internal bool TutorialOff { get; init; }

            /// <summary>The level is decided (cleared or lost): the result is the news.</summary>
            internal bool LevelDecided { get; init; }

            /// <summary>A drop cinematic, the chapter's tour or the line loss has the camera.</summary>
            internal bool CameraTakeover { get; init; }

            /// <summary>The cluster is still held for the chapter's tour: the level has not begun.</summary>
            internal bool ReleaseWaiting { get; init; }

            /// <summary>A tutorial card is up, arriving, leaving or queued: it is speaking to the player.</summary>
            internal bool CardStands { get; init; }

            /// <summary>The glass is stepping, or its red or blue flash is still lit: the ceiling's alarm.</summary>
            internal bool CeilingAlarm { get; init; }

            /// <summary>The floor's laser net is lit: the line's alarm.</summary>
            internal bool FloorAlarm { get; init; }

            /// <summary>A shot is still in the air: the player has just fired and is watching it.</summary>
            internal bool ShotInAir { get; init; }

            /// <summary>Whether any of it holds.</summary>
            internal readonly bool Any =>
                TutorialOff || LevelDecided || CameraTakeover || ReleaseWaiting || CardStands || CeilingAlarm || FloorAlarm || ShotInAir;
        }

        /// <summary>The first levels of the first chapter that carry the hint: the owner's "first five maps".</summary>
        internal const int LEVELS = 5;

        /// <summary>The chip's one word: a plain imperative (the fire button's glyph stands beside it).</summary>
        internal const string CAPTION = "Fire!";

        /// <summary>
        /// How long a level may go with no shot (and nothing else going on) before the chip first appears: twice the five
        /// seconds a shot the first chapter is played at (see the class remarks).
        /// </summary>
        internal const float FIRST_PAUSE_SECONDS = 10f;

        /// <summary>The pause before it comes back in that level, once it has been shown: three times the first.</summary>
        internal const float LATER_PAUSE_SECONDS = 30f;

        /// <summary>How long it stands if nobody fires: told, it is not repeated at a player who is thinking.</summary>
        internal const float STAND_SECONDS = 12f;

        /// <summary>Its arrival and its leaving, in seconds: the leaving is quick, the shot has been fired.</summary>
        internal const float ARRIVE_SECONDS = 0.25f;
        internal const float LEAVE_SECONDS = 0.15f;

        /// <summary>The beat: one pulse a second.</summary>
        internal const float BEAT_SECONDS = 1f;

        /// <summary>How far a beat swells the chip at its peak, as a fraction of its size.</summary>
        internal const float BEAT_SWELL = 0.14f;

        private bool _eligible;
        private bool _showing;
        private bool _shownThisLevel;
        private float _idle;
        private float _age;
        private float _presence;

        /// <summary>Whether this level carries the hint at all.</summary>
        internal bool Eligible => _eligible;

        /// <summary>Whether the chip is wanted on the frame now (it may still be fading in or out): see <see cref="Presence"/>.</summary>
        internal bool Showing => _showing;

        /// <summary>How present the chip is, 0 gone to 1 fully up: what <c>PlayHud</c> multiplies its colours by.</summary>
        internal float Presence => _presence;

        /// <summary>Seconds since the chip came up, the clock of its beat; held where it was while the chip fades out.</summary>
        internal float Age => _age;

        /// <summary>Game seconds counted towards the next appearance; 0 while something else has the player's attention.</summary>
        internal float Idle => _idle;

        /// <summary>The pause the chip is waiting out now: the first in a level, the longer one once it has been shown.</summary>
        internal float Threshold => _shownThisLevel ? LATER_PAUSE_SECONDS : FIRST_PAUSE_SECONDS;

        /// <summary>Whether level <paramref name="levelInChapter"/> of chapter <paramref name="chapter"/> (both from 0) carries the hint.</summary>
        internal static bool CarriesHint(int chapter, int levelInChapter) =>
            chapter == 0 && levelInChapter >= 0 && levelInChapter < LEVELS;

        /// <summary>A level is starting: forgets the last one's count and its showing, and says whether this one carries the hint.</summary>
        internal void BeginLevel(bool eligible)
        {
            _eligible = eligible;
            _showing = false;
            _shownThisLevel = false;
            _idle = 0f;
            _age = 0f;
            _presence = 0f;
        }

        /// <summary>
        /// A shot has left the gun: the chip leaves at once and the count starts again, at the longer pause if it has been
        /// shown in this level.
        /// </summary>
        internal void NoteShot()
        {
            Hide();
            _idle = 0f;
        }

        /// <summary>
        /// One frame, in game seconds. <paramref name="distractions"/> is anything that has the player's attention or makes a hint
        /// false (see the class remarks): the chip is taken down, and the count stays at zero for as long as any of it holds.
        /// </summary>
        internal void Update(float elapsed, in Distractions distractions)
        {
            if (!_eligible)
            {
                _showing = false;
                _presence = 0f;
                return;
            }

            if (distractions.Any)
            {
                Hide();
                _idle = 0f;
            }
            else if (_showing)
            {
                _age += elapsed;
                if (_age >= STAND_SECONDS) Hide();
            }
            else
            {
                _idle += elapsed;
                if (_idle >= Threshold)
                {
                    _showing = true;
                    _age = 0f;
                }
            }

            _presence = _showing
                ? MathF.Min(1f, _presence + elapsed / ARRIVE_SECONDS)
                : MathF.Max(0f, _presence - elapsed / LEAVE_SECONDS);
        }

        /// <summary>
        /// The beat at <paramref name="age"/>: 1 on the instant a second begins, easing to 0 before the next. A kick and a
        /// settle, not a sine, so each pulse reads as a nudge at the player. Drawn from the age alone, so nothing is stored.
        /// </summary>
        internal static float Beat(float age)
        {
            if (age < 0f) return 0f;

            float phase = age / BEAT_SECONDS;
            phase -= MathF.Floor(phase);
            float rest = 1f - phase;

            return rest * rest * rest;
        }

        private void Hide()
        {
            if (!_showing) return;

            _showing = false;
            _shownThisLevel = true;
            _idle = 0f;
        }
    }
}
