using Microsoft.Xna.Framework.Audio;
using System;

namespace BS3D.Audio
{
    /// <summary>
    /// A fixed ring of <see cref="SoundEffectInstance"/>s over <b>one</b> baked buffer — the playing handle that
    /// <see cref="SoundEffect.Play(float, float, float)"/> does not give, and which
    /// <see cref="SoundEffectInstance.Apply3D(AudioListener, AudioEmitter)"/> requires.
    /// <para>
    /// Every instance is created once, in the constructor, and reused for the life of the process. Nothing is
    /// created, disposed, enumerated or state-polled at play time, so a <see cref="Take"/> costs the two native
    /// calls it makes and nothing on the managed heap. (The <c>Apply3D</c> that follows it does allocate, inside
    /// the framework — see the bounded exception under rule 3 of <c>BestPractices.md</c>.)
    /// </para>
    /// <para>
    /// <b>Bounded by construction, because the framework will not bound it.</b> MonoGame only ever reclaims the
    /// instances it pooled itself — the ones <see cref="SoundEffect.Play()"/> borrows — so an instance created
    /// here is never recycled, and <c>MAX_PLAYING_INSTANCES</c> is <see cref="int.MaxValue"/> on WindowsDX. A
    /// create-one-per-play design would therefore leak an XAudio2 source voice per sound with no exception, no
    /// log line and no framework failure to notice it by. A fixed ring cannot do that.
    /// </para>
    /// <para>
    /// <b>One ring per buffer, never shared.</b> Besides keeping the steal rule simple, an instance that has
    /// ever played a stereo buffer keeps a channel-azimuth array around and allocates unmanaged memory on every
    /// later <c>Apply3D</c>; a ring bound to a single mono buffer cannot reach that path.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <b>The invariant this type exists to protect: <see cref="SoundEffectInstance.Pan"/> is never written on
    /// a voice that has been PLACED.</b> Pan and <c>Apply3D</c> write the same XAudio2 output matrix, so one
    /// assignment throws the whole 3D placement away. There is exactly one deliberate write, in
    /// <c>ProceduralAudio.Speak</c>'s no-listener-yet branch, where Pan is the only way to address that matrix
    /// because there is nothing to place against — and a later <c>Apply3D</c> on the same slot overwrites it
    /// completely, so it cannot leak into a placed sound.
    /// </remarks>
    internal sealed class VoiceRing : IDisposable
    {
        private readonly SoundEffectInstance[] _ring;

        //Where the next Take() starts looking. Round-robin over a fixed ring IS age order, which is the whole
        //reason the steal rule needs no bookkeeping: the slot this reaches is always the one that started
        //longest ago.
        private int _next;

        internal VoiceRing(SoundEffect effect, int voices)
        {
            _ring = new SoundEffectInstance[voices];
            for (int i = 0; i < voices; i++) _ring[i] = effect.CreateInstance();
        }

        /// <summary>
        /// The next slot, stopped and ready to be posed and played.
        /// <para>
        /// <b>The <see cref="SoundEffectInstance.Stop()"/> is correctness, not tidiness.</b>
        /// <see cref="SoundEffectInstance.Play()"/> returns <i>early</i> when the instance is already playing —
        /// so a slot taken while it is still sounding would silently swallow the new sound rather than replace
        /// it. Stopping unconditionally costs two native calls per sound <b>event</b> (never per frame) and
        /// means the worst a ring that is too small can do is cut its own oldest voice short, which is audible
        /// and recoverable, instead of dropping a sound that was asked for, which is neither.
        /// </para>
        /// </summary>
        internal SoundEffectInstance Take()
        {
            SoundEffectInstance voice = _ring[_next];
            if (++_next >= _ring.Length) _next = 0;

            voice.Stop();
            return voice;
        }

        /// <summary>
        /// Disposes the ring's instances. <b>Call this before disposing the <see cref="SoundEffect"/> the ring
        /// was built from</b> — the instances hold voices onto that buffer.
        /// </summary>
        public void Dispose()
        {
            for (int i = 0; i < _ring.Length; i++) _ring[i]?.Dispose();
        }
    }
}
