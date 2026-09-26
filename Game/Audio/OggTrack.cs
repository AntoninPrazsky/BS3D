using NVorbis;
using System;
using System.IO;

namespace BS3D.Audio
{
    /// <summary>
    /// One music track off disk (#444): an Ogg Vorbis file decoded into the interleaved 16-bit stereo PCM that
    /// <c>GameMusic</c> hands its voices. The same buffer a .wav's data chunk was until then, so nothing
    /// that plays it knows the difference. <c>Tools/MusicBake --tracks</c> compiles this file as source and
    /// decodes every track it has just encoded through it, so the length and loop-edge figures it prints are
    /// this decoder's rather than some other one's.
    /// <para>
    /// <b>The length is what a loop turns on.</b> A track is cut so its last frame runs straight into its first,
    /// and the feed (#212) submits the whole buffer again, so a single frame of padding at either end is a
    /// hole at every repeat. Vorbis records the exact frame count in the last page's granule position, and the
    /// decode stops there itself rather than leaving it to NVorbis: 0.10.5 trims its final packet to that count
    /// but cannot reach back into the samples it still holds from the packet before, and Ember's end fell
    /// there — it came back 80 frames long (measured through the tool, #444), the extra being the encoder's
    /// extrapolation past the end.
    /// </para>
    /// </summary>
    internal static class OggTrack
    {
        /// <summary>How much is decoded between conversions: a quarter of a second of float scratch, not the whole track.</summary>
        private const int CHUNK_FRAMES = 12000;

        /// <summary>
        /// One Vorbis comment off a file (#482): what a recording says about itself that its samples cannot — the
        /// victory fanfare's key (<c>ROOT</c>, a MIDI note) and tempo (<c>BPM</c>), which the star chime tunes to and
        /// paces by. <c>Tools/MusicBake --sfx --music</c> writes them. Null when the file has no such tag.
        /// </summary>
        public static string ReadTag(string path, string name)
        {
            using FileStream file = File.OpenRead(path);
            using VorbisReader reader = new(file, closeOnDispose: false);
            string value = reader.Tags.GetTagSingle(name);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        public static byte[] Decode(string path, int sampleRate)
        {
            using FileStream file = File.OpenRead(path);
            return Decode(file, sampleRate);
        }

        /// <summary>
        /// Decodes a stereo Ogg Vorbis stream at <paramref name="sampleRate"/>, refusing any other shape rather than
        /// playing it at the wrong speed. Converted exactly as <see cref="ProceduralMusic.ToPcm(float[])"/> converts — clamp,
        /// then scale to shorts — so a track decodes to what the .wav it replaced held, give or take the codec.
        /// </summary>
        public static byte[] Decode(Stream stream, int sampleRate)
        {
            using VorbisReader reader = new(stream, closeOnDispose: false);

            if (reader.Channels != 2 || reader.SampleRate != sampleRate)
                throw new InvalidDataException($"expected stereo at {sampleRate} Hz, found {reader.Channels} channels at {reader.SampleRate} Hz");

            long frames = reader.TotalSamples;
            if (frames <= 0 || frames > int.MaxValue / 4)
                throw new InvalidDataException($"implausible length of {frames} frames");

            //Sized off the stream's own count, which is also where the decode stops (see the class remarks)
            byte[] pcm = new byte[frames * 4];
            float[] chunk = new float[CHUNK_FRAMES * 2];
            int written = 0;

            for (int read; written < pcm.Length && (read = reader.ReadSamples(chunk, 0, chunk.Length)) > 0;)
            {
                int take = Math.Min(read, (pcm.Length - written) / 2);

                for (int i = 0; i < take; i++)
                {
                    short v = (short)(Math.Clamp(chunk[i], -1f, 1f) * short.MaxValue);
                    pcm[written++] = (byte)(v & 0xff);
                    pcm[written++] = (byte)((v >> 8) & 0xff);
                }
            }

            //A stream that ends short of its count is played as long as it is; the tool is where that is caught
            written -= written % 4;
            if (written != pcm.Length) Array.Resize(ref pcm, written);

            return pcm;
        }
    }
}
