using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System;

namespace BS3D.Audio
{
    /// <summary>
    /// The one door every sound goes through (#1048, #1077): it plays when the platform has a voice to give, and when it
    /// has none a sound of a higher <see cref="VoiceClass"/> takes the voice of the oldest sound of the lowest class below
    /// it. Only a sound with nothing below it to take from is dropped.
    /// <para>
    /// ⚠ <b>MonoGame's OpenAL backend on Android plays at most 32 sounds at once</b> (<see cref="Platform.AudioBackend.VoiceLimit"/>),
    /// a constant in both <c>SoundEffectInstancePool.SoundsAvailable</c> and <c>OpenALSoundController.MAX_NUMBER_OF_SOURCES</c>
    /// (read in MonoGame 3.8.5's Android assembly), so it cannot be raised without a fork. An instance's <c>Play</c> throws
    /// past it (<see cref="InstancePlayLimitException"/>, which ended the game on every level won until #1048), and
    /// <c>SoundEffect.Play(volume, pitch, pan)</c> answers false. Windows' XAudio2 has no limit and the Pi's is 256, so
    /// there the first try always plays and nothing below it runs.
    /// </para>
    /// <para>
    /// ⚠ <b>A stopped voice still counts until MonoGame's next update</b>: <c>Stop</c> gives its OpenAL source back at
    /// once, but the pool lists it as playing until <c>FrameworkDispatcher.Update</c> (at the top of every frame) sees
    /// it stopped. That includes the slot every <c>VoiceRing.Take</c> stops. So a refused sound first has the
    /// pool updated once and is tried again, and a voice taken from a lower class is stopped and the pool updated before
    /// the second try. Nothing in this game listens to <c>BufferNeeded</c>, the one event that update can raise.
    /// </para>
    /// <para>
    /// After a sound finds no voice, every sound of its class and below is dropped without trying for
    /// <see cref="BACKOFF_MS"/>, so a burst at the limit does not throw once a sound; a higher class still tries, and a
    /// voice started again every frame while it is stopped (a loop, the music's stream) is retried after it. What was
    /// stopped and dropped is said in one <c>[audio]</c> line once the limit has been quiet for
    /// <see cref="REPORT_QUIET_MS"/> (<see cref="Update"/>), never on the frame it happened.
    /// </para>
    /// </summary>
    internal static class VoiceBudget
    {
        /// <summary>How long a sound that found no voice holds its class and the ones below it off, in milliseconds: a few
        /// frames, a moment for voices to end.</summary>
        internal const long BACKOFF_MS = 100;

        /// <summary>How long the limit must have been quiet before its <c>[audio]</c> line is written, in milliseconds.</summary>
        internal const long REPORT_QUIET_MS = 1500;

        //More than the 32 Android can play: the ledger also holds voices that have ended and not yet been asked
        private const int LEDGER_VOICES = 48;

        private static readonly long[] _refusedUntil = new long[(int)VoiceClass.Music + 1];
        private static readonly VoiceLedger<SoundEffectInstance> _ledger = new(LEDGER_VOICES, Holds);
        private static readonly VoiceReport _report = new();

        /// <summary>
        /// Whether the platform's voice limit is one the game reaches (Android's 32): the shares
        /// (<see cref="Share"/>) and the volley's tick ring exist only then.
        /// </summary>
        internal static bool Tight => Platform.AudioBackend.VoiceLimit <= 64;

        /// <summary>
        /// How many voices a sound that comes many at once may hold (#1077): <paramref name="wanted"/>, or on a platform
        /// whose limit the game reaches (<see cref="Tight"/>) <paramref name="ofThirtyTwo"/> of every thirty-two voices it
        /// has, if that is fewer. The loud moments keep the headroom for the fanfare this way, rather than reaching the
        /// limit and having a voice taken.
        /// </summary>
        internal static int Share(int wanted, int ofThirtyTwo) =>
            Tight ? Math.Min(wanted, Math.Max(1, Platform.AudioBackend.VoiceLimit * ofThirtyTwo / 32)) : wanted;

        /// <summary>
        /// Plays <paramref name="voice"/> as a sound of <paramref name="voiceClass"/>, taking a lower class's voice when
        /// the platform has none left; <paramref name="name"/> is what the <c>[audio]</c> line calls it. True when it plays.
        /// </summary>
        internal static bool TryPlay(SoundEffectInstance voice, VoiceClass voiceClass, string name)
        {
            bool played = TryPlay(new Start(voice), voiceClass, name);
            if (played) _ledger.Began(voice, voiceClass, name, Environment.TickCount64);
            return played;
        }

        /// <summary>
        /// Plays <paramref name="effect"/> fire-and-forget, as <see cref="SoundEffect.Play(float, float, float)"/> does,
        /// as a sound of <paramref name="voiceClass"/>. Such a voice cannot be taken back, so it is never one that gives
        /// way. True when it plays.
        /// </summary>
        internal static bool TryPlay(SoundEffect effect, float volume, float pitch, float pan, VoiceClass voiceClass, string name) =>
            TryPlay(new Start(effect, volume, pitch, pan), voiceClass, name);

        /// <summary>
        /// Resumes <paramref name="voice"/>, paused by its player (the Jukebox's play/pause), as a sound of
        /// <paramref name="voiceClass"/>. A paused <c>DynamicSoundEffectInstance</c> asks the pool again on
        /// <c>Resume</c> and throws when it is full, as <c>Play</c> does. True when it plays.
        /// </summary>
        internal static bool TryResume(SoundEffectInstance voice, VoiceClass voiceClass, string name)
        {
            bool played = TryPlay(new Start(voice, resume: true), voiceClass, name);
            if (played) _ledger.Began(voice, voiceClass, name, Environment.TickCount64);
            return played;
        }

        /// <summary>
        /// Whether a sound of <paramref name="voiceClass"/> would be dropped without trying now (a sound of its class
        /// or above found no voice under <see cref="BACKOFF_MS"/> ago), counted as dropped when it would. A
        /// <c>VoiceRing</c> asks before its <c>Take</c>, which stops the ring's oldest voice: a sound that is dropped
        /// must not cut one short on its way.
        /// </summary>
        internal static bool HeldOff(VoiceClass voiceClass, string name)
        {
            long now = Environment.TickCount64;
            if (now >= _refusedUntil[(int)voiceClass]) return false;

            _report.Dropped(name, now);
            return true;
        }

        /// <summary>Writes the <c>[audio]</c> line once the limit has been quiet for <see cref="REPORT_QUIET_MS"/>. Once a frame.</summary>
        internal static void Update()
        {
            string line = _report.Flush(Environment.TickCount64, REPORT_QUIET_MS);
            if (line != null) Console.WriteLine(line);
        }

        private static bool TryPlay(in Start start, VoiceClass voiceClass, string name)
        {
            long now = Environment.TickCount64;
            if (now < _refusedUntil[(int)voiceClass])
            {
                _report.Dropped(name, now);
                return false;
            }

            if (start.Play()) return true;

            //The voices stopped since the frame began (a ring's Take, a stolen one) and the ones that ended, given back
            FrameworkDispatcher.Update();
            if (start.Play()) return true;

            //What was playing as the limit was met, once an episode, before anything is stopped
            if (!_report.Pending) _ledger.CountHolding(_report.Held);

            int victim = _ledger.Victim(voiceClass);
            if (victim >= 0)
            {
                _report.Stopped(_ledger.NameAt(victim), name, now);
                _ledger.VoiceAt(victim).Stop();
                _ledger.Forget(victim);
                FrameworkDispatcher.Update();
                if (start.Play()) return true;
            }

            _report.Dropped(name, now);
            for (int below = 0; below <= (int)voiceClass; below++) _refusedUntil[below] = now + BACKOFF_MS;
            return false;
        }

        //Whether a voice still holds one of the platform's voices: a paused one does, as the pool counts it
        private static bool Holds(SoundEffectInstance voice) =>
            voice != null && !voice.IsDisposed && voice.State != SoundState.Stopped;

        /// <summary>One play, of an instance or fire-and-forget, so the steal is written once and allocates nothing.</summary>
        private readonly struct Start
        {
            private readonly SoundEffectInstance _voice;
            private readonly SoundEffect _effect;
            private readonly float _volume, _pitch, _pan;
            private readonly bool _resume;

            internal Start(SoundEffectInstance voice, bool resume = false)
            {
                _voice = voice;
                _effect = null;
                _volume = _pitch = _pan = 0f;
                _resume = resume;
            }

            internal Start(SoundEffect effect, float volume, float pitch, float pan)
            {
                _voice = null;
                _effect = effect;
                _volume = volume;
                _pitch = pitch;
                _pan = pan;
                _resume = false;
            }

            //Both throw when the pool had room and the OpenAL sources did not: a DynamicSoundEffectInstance holds its
            //source for its whole life, playing or not
            internal bool Play()
            {
                try
                {
                    if (_voice == null) return _effect.Play(_volume, _pitch, _pan);
                    if (_resume) _voice.Resume();
                    else _voice.Play();
                    return true;
                }
                catch (InstancePlayLimitException)
                {
                    return false;
                }
            }
        }
    }
}
