using System;
using System.Collections.Generic;
using System.IO;

namespace BS3D.Capture
{
    /// <summary>
    /// The mixing desk of a capture's sound (#1179, <c>captureaudio</c>): buffers, the voices that played them on the capture's clock,
    /// each voice's volume and pitch over time, and its place in the stereo image, rendered into one stereo signal for exactly the
    /// frames' window. It knows nothing of MonoGame's audio: <c>CaptureSounds</c> records into it, and the tests drive it directly.
    /// <para>
    /// <b>What it reproduces, and what it does not.</b> A voice is its buffer read at the voice's pitch (an octave a unit, as
    /// MonoGame's <c>Pitch</c> is, by linear interpolation), scaled by its volume (interpolated between the points it was polled at,
    /// once a frame, so a fade is a ramp and not a staircase), looped when it was looped, cut where it was stopped, and split into
    /// left and right by a constant pair of gains. The hardware's mix is approximated, not copied: X3DAudio's matrix is replaced by
    /// <see cref="PlacedGains"/>, and nothing of XAudio2's own resampler or limiter is modelled.
    /// </para>
    /// </summary>
    internal sealed class SoundMix
    {
        /// <summary>The rate the mix is rendered and written at: the trailer's audio is AAC at 48 kHz.</summary>
        internal const int OUTPUT_RATE = 48000;

        private readonly List<(float[] Signal, int Rate)> _buffers = new();
        private readonly List<Voice> _voices = new();

        private sealed class Voice
        {
            internal string Name;
            internal int Buffer;
            internal double Start, End = double.PositiveInfinity;
            internal bool Looped;
            internal float Left, Right;
            internal readonly List<(double Time, float Volume, float Pitch)> Points = new();
        }

        /// <summary>How many voices were recorded.</summary>
        internal int VoiceCount => _voices.Count;

        /// <summary>How many buffers are known.</summary>
        internal int BufferCount => _buffers.Count;

        /// <summary>Adds a mono buffer (samples in [-1, 1]; clamped as the 16-bit PCM it was made into is) at its rate, and returns its index.</summary>
        internal int AddBuffer(float[] signal, int rate)
        {
            _buffers.Add((signal ?? Array.Empty<float>(), rate));
            return _buffers.Count - 1;
        }

        /// <summary>
        /// A voice of <paramref name="buffer"/> beginning at <paramref name="start"/> seconds of the capture's clock, at
        /// <paramref name="volume"/> and <paramref name="pitch"/> (octaves), into the left and right channels at the two gains.
        /// Returns the voice's index, for <see cref="Point"/> and <see cref="End"/>.
        /// </summary>
        internal int Begin(int buffer, double start, float volume, float pitch, float left, float right, bool looped, string name = null)
        {
            Voice voice = new() { Name = name, Buffer = buffer, Start = start, Looped = looped, Left = left, Right = right };
            voice.Points.Add((start, volume, pitch));
            _voices.Add(voice);
            return _voices.Count - 1;
        }

        /// <summary>The voice's volume and pitch at <paramref name="time"/>: the voice ramps to them from its previous point.</summary>
        internal void Point(int voice, double time, float volume, float pitch) => _voices[voice].Points.Add((time, volume, pitch));

        /// <summary>The voice stops at <paramref name="time"/> (a voice that is not stopped plays its buffer out, or loops for ever).</summary>
        internal void End(int voice, double time)
        {
            Voice v = _voices[voice];
            if (time < v.End) v.End = Math.Max(time, v.Start);
        }

        /// <summary>
        /// Renders <paramref name="samples"/> stereo samples at <see cref="OUTPUT_RATE"/> from <paramref name="from"/> seconds of the
        /// capture's clock: interleaved left, right. Voices that began before the window and still sound into it are in it.
        /// </summary>
        internal float[] Render(double from, int samples)
        {
            float[] output = new float[samples * 2];
            double windowEnd = from + samples / (double)OUTPUT_RATE;

            foreach (Voice voice in _voices)
            {
                if (voice.Start >= windowEnd) continue;
                (float[] signal, int rate) = _buffers[voice.Buffer];
                if (signal.Length == 0) continue;

                //Sample by sample of the output from the voice's own start, writing only inside the window: a voice begun in the
                //lead-in has its position where the frames have it
                long first = (long)Math.Round(voice.Start * OUTPUT_RATE);
                long last = (long)Math.Min(Math.Round(Math.Min(voice.End, windowEnd) * OUTPUT_RATE), long.MaxValue / 2);
                long windowFirst = (long)Math.Round(from * OUTPUT_RATE);
                double position = 0.0, step = rate / (double)OUTPUT_RATE;
                int point = 0;
                List<(double Time, float Volume, float Pitch)> points = voice.Points;

                for (long s = first; s < last; s++)
                {
                    double time = s / (double)OUTPUT_RATE;
                    while (point + 1 < points.Count && points[point + 1].Time <= time) point++;

                    //The ramp to the next point, or the last point held
                    float volume = points[point].Volume, pitch = points[point].Pitch;
                    if (point + 1 < points.Count)
                    {
                        double span = points[point + 1].Time - points[point].Time;
                        float t = span > 0 ? (float)((time - points[point].Time) / span) : 1f;
                        volume += (points[point + 1].Volume - volume) * t;
                        pitch += (points[point + 1].Pitch - pitch) * t;
                    }

                    if (position >= signal.Length - 1)
                    {
                        if (!voice.Looped) break;
                        position -= signal.Length - 1;
                    }

                    int index = (int)position;
                    double frac = position - index;
                    float value = (float)(Clamp(signal[index]) + (Clamp(signal[index + 1]) - Clamp(signal[index])) * frac) * volume;
                    position += step * Math.Pow(2.0, pitch);

                    long at = s - windowFirst;
                    if (at < 0 || at >= samples) continue;
                    output[at * 2] += value * voice.Left;
                    output[at * 2 + 1] += value * voice.Right;
                }
            }
            return output;
        }

        private static float Clamp(float sample) => sample < -1f ? -1f : sample > 1f ? 1f : sample;

        /// <summary>
        /// The voices that begin inside [<paramref name="from"/>, <paramref name="to"/>) seconds, in order, by the name the game plays
        /// them under (<c>VoiceBudget</c>'s: "blast", "release", "shot"...) and their start in seconds from <paramref name="from"/>: the
        /// moments a cut is looking for, found without looking.
        /// </summary>
        internal List<(string Name, double At)> Events(double from, double to)
        {
            List<(string, double)> events = new();
            foreach (Voice voice in _voices)
                if (voice.Start >= from && voice.Start < to) events.Add((voice.Name ?? "?", voice.Start - from));
            events.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            return events;
        }

        /// <summary>
        /// MonoGame's pan law for a mono voice that is not placed (<c>SoundEffect.Play</c>'s pan, an instance's <c>Pan</c>): the
        /// centre-hot law, both channels at full in the middle and the far channel faded as the pan leaves it.
        /// </summary>
        internal static (float Left, float Right) CentreHotGains(float pan)
        {
            pan = Math.Clamp(pan, -1f, 1f);
            return (pan > 0f ? 1f - pan : 1f, pan < 0f ? 1f + pan : 1f);
        }

        /// <summary>
        /// The stand-in for X3DAudio's stereo matrix on a placed voice: <paramref name="side"/> is the emitter's offset along the
        /// listener's right over its distance (-1 hard left, +1 hard right, 0 ahead, behind or overhead), split so the two gains sum
        /// to 1 as X3DAudio's matrix does (<c>docs/game-feedback/sound-placement-and-mix.md</c>).
        /// </summary>
        internal static (float Left, float Right) PlacedGains(float side)
        {
            side = Math.Clamp(side, -1f, 1f);
            return ((1f - side) * 0.5f, (1f + side) * 0.5f);
        }

        /// <summary>Writes interleaved stereo float samples as a 32-bit float WAV (WAVE_FORMAT_IEEE_FLOAT): no clipping, the mix's level as it is.</summary>
        internal static void WriteWav(string path, float[] interleaved, int rate)
        {
            using FileStream file = File.Create(path);
            WriteWav(file, interleaved, rate);
        }

        /// <inheritdoc cref="WriteWav(string, float[], int)"/>
        internal static void WriteWav(Stream stream, float[] interleaved, int rate)
        {
            const short channels = 2, bits = 32;
            int dataBytes = interleaved.Length * 4;
            using BinaryWriter writer = new(stream, System.Text.Encoding.ASCII, leaveOpen: true);
            writer.Write("RIFF"u8);
            writer.Write(36 + dataBytes);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)3);
            writer.Write(channels);
            writer.Write(rate);
            writer.Write(rate * channels * bits / 8);
            writer.Write((short)(channels * bits / 8));
            writer.Write(bits);
            writer.Write("data"u8);
            writer.Write(dataBytes);
            foreach (float sample in interleaved) writer.Write(sample);
        }
    }
}
