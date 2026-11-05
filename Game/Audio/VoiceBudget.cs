using Microsoft.Xna.Framework.Audio;
using System;

namespace BS3D.Audio
{
    /// <summary>
    /// The one door every playing voice goes through (#1048): <see cref="SoundEffectInstance.Play"/> when the platform has a
    /// voice to give, and nothing when it has none.
    /// <para>
    /// ⚠ <b>MonoGame's OpenAL backend on Android plays at most 32 sounds at once and throws past that</b>
    /// (<see cref="InstancePlayLimitException"/>, from <c>SoundEffectInstancePool.SoundsAvailable</c>, read in its decompiled
    /// <c>SoundEffectInstance.Play</c>). Windows' XAudio2 has no such limit and the Pi's OpenAL one far above what the game
    /// asks, so nothing here had ever met it: on the phone the victory's fireworks did, and the exception ended the game
    /// on every level won. A sound refused here is dropped, as OpenAL drops a source it has not got: among thirty other
    /// voices nobody hears one crackle fewer.
    /// </para>
    /// <para>
    /// After a refusal every play is refused for <see cref="BACKOFF_MS"/> without asking, so a burst of sounds at the limit
    /// does not throw once per sound. A voice that is started again every frame while it is stopped (a loop, the music's
    /// stream) is then retried that often. <c>SoundEffect.Play(volume, pitch, pan)</c> needs none of this: it answers false
    /// at the limit rather than throwing.
    /// </para>
    /// </summary>
    internal static class VoiceBudget
    {
        /// <summary>How long a refusal holds every play off, in milliseconds: a few frames, a moment for voices to end.</summary>
        internal const long BACKOFF_MS = 100;

        private static long _refusedUntil;
        private static bool _said;

        /// <summary>Plays <paramref name="voice"/>, or drops it when the platform has no voice left. True when it plays.</summary>
        internal static bool TryPlay(SoundEffectInstance voice)
        {
            long now = Environment.TickCount64;
            if (now < _refusedUntil) return false;

            try
            {
                voice.Play();
                return true;
            }
            catch (InstancePlayLimitException)
            {
                _refusedUntil = now + BACKOFF_MS;
                if (!_said)
                {
                    _said = true;
                    Console.WriteLine("[audio] every voice the platform allows is playing: sounds past it are dropped");
                }

                return false;
            }
        }
    }
}
