using System;

namespace BS3D.Audio
{
    /// <summary>
    /// <b>A pour climbs</b> (#726): which step of a run of blue ceiling steps this one is, so the blue step's sound can rise
    /// as the run goes on. A band that goes can owe a tall level several feed steps, and they come down one a hold apart
    /// (<c>CeilingDescent</c>'s <c>CEILING_STEP_HOLD</c>): eleven in a row in a scripted Totem run, the same chime every half
    /// second. Each blue step that follows the last within <see cref="WINDOW_SECONDS"/> is played a semitone above it, up to
    /// <see cref="TOP_SEMITONES"/> (a fifth), so a pour rises as it pours (more of the column, higher), and the next one starts
    /// from the bottom again. A red step plays its own sound at its own pitch and leaves the pour where it was.
    /// <para>
    /// <b>On the steps' own clock.</b> <see cref="Advance"/> is handed the gameplay frame's seconds, the very ones
    /// <c>CeilingDescent.Update</c> lets the queued steps go by, so the pour and the steps cannot disagree about how much time
    /// passed. A pause stops the screen dead and so stops both: a pour paused halfway goes on from where it was. A wall clock
    /// read at the step would start it over after any pause longer than the window.
    /// </para>
    /// Pure rules over floats with no device in them, as <see cref="CeilingFeedSynth"/> is, so the tests hold them against
    /// the descent's own timing (the session asks <see cref="Step"/>, <c>ProceduralAudio.PlayCeilingStep</c> plays the answer).
    /// </summary>
    internal sealed class CeilingFeedPour
    {
        /// <summary>
        /// How close behind the last blue step the next has to come to count as the same pour, in seconds of play. Queued
        /// steps come a <c>CEILING_STEP_HOLD</c> apart, well inside it (<c>CeilingFeedPourTests</c> holds the two together); a feed earned by a later landing comes
        /// shots later, well outside it.
        /// </summary>
        public const float WINDOW_SECONDS = 1.0f;

        /// <summary>How far a pour climbs, in semitones: a fifth.</summary>
        public const int TOP_SEMITONES = 7;

        //Seconds of play since the last blue step (infinite before the first, so it can never count as following one), and
        //how far the pour had climbed at that step
        private float _sinceBlue = float.PositiveInfinity;
        private int _climb;

        /// <summary>A level starts with no pour behind it.</summary>
        public void Reset()
        {
            _sinceBlue = float.PositiveInfinity;
            _climb = 0;
        }

        /// <summary>Moves the pour's clock on by one gameplay frame: the frame's seconds, not called at all while paused.</summary>
        public void Advance(float elapsed) => _sinceBlue += elapsed;

        /// <summary>
        /// A ceiling step begins. A blue one (<paramref name="feed"/>: a tall level's feed, or the Brake's lift) is placed in
        /// its pour; a red one is not part of any and leaves the pour alone.
        /// </summary>
        /// <returns>How many semitones above its written pitch the step's sound is played: 0 for a red step and for the
        /// first blue step of a pour, one more for each that follows, up to <see cref="TOP_SEMITONES"/>.</returns>
        public int Step(bool feed)
        {
            if (!feed) return 0;

            _climb = _sinceBlue < WINDOW_SECONDS ? Math.Min(_climb + 1, TOP_SEMITONES) : 0;
            _sinceBlue = 0f;
            return _climb;
        }
    }
}
