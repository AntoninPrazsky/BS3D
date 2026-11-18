using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;

namespace BS3D.Capture
{
    /// <summary>
    /// The recorder of a capture's sound (#1179, <c>captureaudio</c>). A capture runs on game time, not the wall clock, so what the
    /// hardware plays during it belongs to no frame; instead every play is written down here, on the capture's clock, and
    /// <see cref="SoundMix"/> renders them after the last frame into <c>audio.wav</c>, in step with the frames to the sample.
    /// <para>
    /// <b>Three doors feed it, and it is idle unless <see cref="Recording"/></b>, so a run that is not such a capture tests one bool
    /// at each and does nothing else:
    /// <list type="bullet">
    /// <item><c>ProceduralAudio.ToSoundEffect</c>, where every effect's buffer is made, hands it the signal (<see cref="Register"/>);
    /// <c>VoiceRing</c> and the looped hum say which buffer each instance plays (<see cref="RegisterInstance"/>).</item>
    /// <item><c>ProceduralAudio.Speak</c> says where a placed voice stands in the stereo image (<see cref="Place"/>).</item>
    /// <item><c>VoiceBudget</c>, the one door every sound goes through (<c>VoiceLedgerTests</c>), reports each play of the Play,
    /// Event and Crowd classes (<see cref="Played(SoundEffectInstance)"/>, <see cref="Played(SoundEffect, float, float, float)"/>).
    /// The Music class (the theme, the fanfares, the scene's bed) is left out: the trailer has music of its own.</item>
    /// </list>
    /// Once a frame <see cref="Poll"/> reads each sounding instance's volume, pitch and state, so a fade is kept and a stop is
    /// where it was.
    /// </para>
    /// <para>
    /// Only the game's thread plays and polls; the buffers may be made on a bake's worker, so everything is under one lock.
    /// </para>
    /// </summary>
    internal static class CaptureSounds
    {
        private static readonly object Gate = new();
        private static readonly SoundMix Mix = new();
        private static readonly Dictionary<SoundEffect, int> BufferOf = new(ReferenceEqualityComparer.Instance);
        private static readonly Dictionary<SoundEffectInstance, SoundEffect> EffectOf = new(ReferenceEqualityComparer.Instance);
        private static readonly Dictionary<SoundEffectInstance, float> PlacedSide = new(ReferenceEqualityComparer.Instance);
        private static readonly Dictionary<SoundEffectInstance, Sounding> Active = new(ReferenceEqualityComparer.Instance);
        private static readonly List<SoundEffectInstance> Ended = new();
        private static int _unknown;

        //A class, so a poll can write the last reading back while it walks the dictionary
        private sealed class Sounding
        {
            internal int Voice;
            internal double Start;
            internal float Volume, Pitch;
        }

        //The clock at the previous poll: a change found now is held at its old value until then, so it ramps over one frame and
        //not over everything since the voice's last point
        private static double _lastPoll;

        /// <summary>Whether a capture is recording its sound: false in every other run, and every door asks it first.</summary>
        internal static bool Recording { get; private set; }

        /// <summary>The capture's clock on this update, in seconds of game time: what a play is stamped with (<c>CaptureRun.FixStep</c> sets it).</summary>
        internal static double Now { get; set; }

        /// <summary>Starts recording, before any buffer is made (the host's constructor, ahead of <c>LoadContent</c>).</summary>
        internal static void Start() => Recording = true;

        /// <summary>A buffer made from <paramref name="signal"/> at <paramref name="rate"/>: kept, so the mix can play it again.</summary>
        internal static void Register(SoundEffect effect, float[] signal, int rate)
        {
            lock (Gate)
                if (!BufferOf.ContainsKey(effect)) BufferOf[effect] = Mix.AddBuffer(signal, rate);
        }

        /// <summary>An instance made of <paramref name="effect"/>: what it plays when it is played.</summary>
        internal static void RegisterInstance(SoundEffectInstance voice, SoundEffect effect)
        {
            lock (Gate) EffectOf[voice] = effect;
        }

        /// <summary>
        /// Where a placed voice stands, just before it plays: <paramref name="side"/> is the emitter's offset along the listener's
        /// right over its distance, as <c>Speak</c> hands the emitter to <c>Apply3D</c> (see <see cref="SoundMix.PlacedGains"/>).
        /// </summary>
        internal static void Place(SoundEffectInstance voice, float side)
        {
            lock (Gate) PlacedSide[voice] = float.IsFinite(side) ? side : 0f;
        }

        /// <summary>An instance plays now, at its volume, pitch and loop as they are set: a new voice, and the instance's previous one ends.</summary>
        internal static void Played(SoundEffectInstance voice)
        {
            lock (Gate)
            {
                if (Active.Remove(voice, out Sounding previous)) Mix.End(previous.Voice, Now);
                if (!EffectOf.TryGetValue(voice, out SoundEffect effect) || !BufferOf.TryGetValue(effect, out int buffer))
                {
                    _unknown++;
                    return;
                }

                (float left, float right) = PlacedSide.Remove(voice, out float side) ? SoundMix.PlacedGains(side) : SoundMix.CentreHotGains(voice.Pan);
                int index = Mix.Begin(buffer, Now, voice.Volume, voice.Pitch, left, right, voice.IsLooped);
                Active[voice] = new Sounding { Voice = index, Start = Now, Volume = voice.Volume, Pitch = voice.Pitch };
            }
        }

        /// <summary>A fire-and-forget play (<see cref="SoundEffect.Play(float, float, float)"/>): a voice that plays its buffer out, unplaced.</summary>
        internal static void Played(SoundEffect effect, float volume, float pitch, float pan)
        {
            lock (Gate)
            {
                if (!BufferOf.TryGetValue(effect, out int buffer))
                {
                    _unknown++;
                    return;
                }

                (float left, float right) = SoundMix.CentreHotGains(pan);
                Mix.Begin(buffer, Now, volume, pitch, left, right, looped: false);
            }
        }

        /// <summary>
        /// Once a frame: each sounding instance's volume and pitch where they changed, and its end where it stopped. Allocates
        /// nothing in the steady state (a capture's frame, never a player's).
        /// </summary>
        internal static void Poll()
        {
            lock (Gate)
            {
                Ended.Clear();
                foreach (KeyValuePair<SoundEffectInstance, Sounding> entry in Active)
                {
                    SoundEffectInstance voice = entry.Key;
                    if (voice.IsDisposed || voice.State == SoundState.Stopped)
                    {
                        Mix.End(entry.Value.Voice, Now);
                        Ended.Add(voice);
                        continue;
                    }

                    Sounding sounding = entry.Value;
                    float volume = voice.Volume, pitch = voice.Pitch;
                    if (volume == sounding.Volume && pitch == sounding.Pitch) continue;

                    if (_lastPoll > sounding.Start) Mix.Point(sounding.Voice, _lastPoll, sounding.Volume, sounding.Pitch);
                    Mix.Point(sounding.Voice, Now, volume, pitch);
                    sounding.Volume = volume;
                    sounding.Pitch = pitch;
                }

                foreach (SoundEffectInstance voice in Ended) Active.Remove(voice);
                _lastPoll = Now;
            }
        }

        /// <summary>
        /// Renders the window's mix (<paramref name="samples"/> stereo samples at <see cref="SoundMix.OUTPUT_RATE"/> from
        /// <paramref name="from"/> seconds) and writes it to <paramref name="path"/>; returns a line for the log.
        /// </summary>
        internal static string Write(string path, double from, int samples)
        {
            lock (Gate)
            {
                float[] mix = Mix.Render(from, samples);
                SoundMix.WriteWav(path, mix, SoundMix.OUTPUT_RATE);

                float peak = 0f;
                double sum = 0;
                foreach (float s in mix)
                {
                    peak = Math.Max(peak, Math.Abs(s));
                    sum += s * s;
                }
                double rms = Math.Sqrt(sum / Math.Max(1, mix.Length));

                return FormattableString.Invariant(
                    $"[capture] the game's sound: {Mix.VoiceCount} voice(s) of {Mix.BufferCount} buffer(s), {samples} samples at {SoundMix.OUTPUT_RATE} Hz, peak {20 * Math.Log10(peak + 1e-12):0.0} dBFS, RMS {20 * Math.Log10(rms + 1e-12):0.0} dBFS")
                    + (_unknown > 0 ? FormattableString.Invariant($", {_unknown} play(s) of a buffer it was never shown left out") : "");
            }
        }
    }
}
