using Microsoft.Xna.Framework;
using Prazsky.Core.Render;

namespace BS3D.Audio
{
    /// <summary>
    /// <b>Light first, sound a beat behind</b> (#219's thunder, #223's eruption boom): the one place that
    /// watches a scene's own staged events and speaks them once their sound has had time to arrive.
    /// <para>
    /// Two issues owed this and it was written once for both, which was #223's explicit condition for
    /// deferring its own sound. What the two share is their whole shape — an event the scene stages on a
    /// hashed clock, seen instantly and heard late — and the only thing that differs between them is which
    /// one-shot is played and whether it is placed. So the schedule lives here and the sounds live in
    /// <see cref="ProceduralAudio"/>, beside every other bake.
    /// </para>
    /// <para>
    /// <b>⚠ It must not be a bed, and that is the whole reason this class exists rather than a few lines in
    /// <c>ProceduralAmbience</c>.</b> The scene beds are sealed 16-second loops, so an event baked into one
    /// repeats on a fixed interval — a metronome, which is precisely the failure
    /// <c>SceneRenderer.VolcanoEruption</c>'s hashed schedule was written to avoid. A one-shot fired off the
    /// scene's own event index cannot become one.
    /// </para>
    /// <para>
    /// <b>It rides the LIGHT's own schedule and never its brightness.</b> Both envelopes flicker within a
    /// single event on purpose — a strike has return strokes — so an edge detector on brightness would hear
    /// one strike as a stutter of four. <see cref="SceneRenderer.TryGetSceneEvent"/> hands over the event's
    /// index and the second its light began, both off the very hash the flash is drawn from, so a flash and
    /// its thunder cannot name two different strikes.
    /// </para>
    /// </summary>
    internal sealed class SceneEventSounds
    {
        /// <summary>
        /// World units a second. The world is metric by construction — a ball is one unit across and the
        /// island is 26 in radius — so this is the real 343 m/s and the delays it produces are the real
        /// ones: a strike at the cloud field's inner radius (105) arrives in 0.31 s and one at its outer
        /// (780) in 2.3 s. That spread IS how a player tells a near strike from a far one, and it is why the
        /// delay is computed rather than authored.
        /// </summary>
        private const float SPEED_OF_SOUND = 343f;

        //An event whose onset did not fall inside THIS frame is not spoken at all. It is the guard against
        //arriving in the middle of a period - a scene switch, a level built, a game just launched - and
        //playing thunder for a flash nobody saw. The alternative (seed the index on the first tick) loses
        //the first event of every scene instead, and losing a flash that was seen is worse than losing one
        //that was not.
        private const float ONSET_GRACE_SECONDS = 0.05f;

        //Past this the event is out of earshot and the wait stops being a delay and becomes a mystery. Well
        //past the storm field's own outer radius at the speed above, so nothing shipped reaches it.
        private const float MAX_DELAY_SECONDS = 8f;

        private readonly ProceduralAudio _audio;

        //The last event index SPOKEN FOR, so one event is scheduled once however many frames it spans.
        //int.MinValue is "none yet", and is not a valid index for any clock this game will ever run.
        private int _scheduled = int.MinValue;

        //The one sound in flight. There is at most one by construction: a scene stages one event per period
        //and no shipped period (6 s for the storm, 19 for the volcano) is shorter than MAX_DELAY_SECONDS.
        private bool _pending;
        private float _dueAt;
        private Vector3 _at;
        private float _size;
        private SceneKind _scene;

        public SceneEventSounds(ProceduralAudio audio) => _audio = audio;

        /// <summary>
        /// One frame. <paramref name="time"/> is the host's wall clock — the very one the scene draws its
        /// event from — and <paramref name="elapsed"/> is this frame's own length, which is what "the onset
        /// fell inside this frame" is measured against.
        /// </summary>
        public void Update(SceneKind scene, SceneRenderer scenes, float time, float elapsed)
        {
            if (_audio == null) return;

            //A scene change drops whatever was in flight: the sound of a storm that is no longer on screen
            //arriving over a meadow is worse than a strike going unheard.
            if (_pending && _scene != scene) _pending = false;

            if (scenes != null && scenes.TryGetSceneEvent(scene, time, out SceneEvent staged)
                && staged.Index != _scheduled)
            {
                float since = time - staged.OnsetTime;

                if (since >= 0f && since <= elapsed + ONSET_GRACE_SECONDS)
                {
                    _scheduled = staged.Index;

                    //Measured from the ARENA and not from the lens, deliberately. The listener never leaves
                    //the island's neighbourhood — the play camera stands about thirty units out and the
                    //menu's orbit less — while these events are hundreds of units away, so the difference is
                    //under a tenth of a second at the near end and nothing at the far one. Asking the audio
                    //for its listener would put a camera's position into the schedule, and then a shot fired
                    //while a strike was in flight could change when the thunder arrives.
                    float delay = MathHelper.Clamp(staged.At.Length() / SPEED_OF_SOUND, 0f, MAX_DELAY_SECONDS);

                    _pending = true;
                    _scene = scene;
                    _dueAt = staged.OnsetTime + delay;
                    _at = staged.At;
                    _size = staged.Size;
                }
            }

            if (!_pending || time < _dueAt) return;

            _pending = false;

            switch (_scene)
            {
                //Unplaced: by the time a strike's sound has spread over a deck that surrounds the arena there
                //is no direction left in it. Placed: a crater is somewhere, and saying where is half of what
                //the boom is for. See the two play sites for the argument in full.
                case SceneKind.Storm: _audio.PlayThunder(_at.Length(), _size); break;
                case SceneKind.Volcano: _audio.PlayEruption(_at, _size); break;
            }
        }

        /// <summary>Drops anything in flight and forgets what has been spoken — for a scene being replaced
        /// under it, or a session torn down.</summary>
        public void Reset()
        {
            _pending = false;
            _scheduled = int.MinValue;
        }
    }
}
