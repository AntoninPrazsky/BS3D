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
    /// <para>
    /// <b>Two decodes, one conversion (#919).</b> <see cref="Decode(string, int)"/> is the effects' and the fanfares': the
    /// samples as the codec made them. <see cref="DecodeMusic(string, int)"/> is the music's, scaled by
    /// <see cref="MusicLevels.DECODE_HEADROOM"/> before they become 16 bits, because Vorbis overshoots the PCM it was
    /// given and a sample over full scale can only be pressed onto the rail. <b>That clip was NVorbis's own</b>
    /// (<c>ClipSamples</c> is on by default and cuts every sample at ±0.99999994 before <c>ReadSamples</c> returns it), so
    /// the clamp below never saw an overshoot and a gain applied after the read would have scaled samples already pressed
    /// flat. The reader is therefore told not to clip, and the one clip is ours, after the gain, at the level NVorbis used:
    /// a decode at unity is byte for byte what it was.
    /// </para>
    /// </summary>
    internal static class OggTrack
    {
        /// <summary>How much is decoded between conversions: a quarter of a second of float scratch, not the whole track.</summary>
        private const int CHUNK_FRAMES = 12000;

        /// <summary>
        /// Where NVorbis itself cuts a sample when it is left to clip: the float just under 1, which converts to 32766. Kept
        /// as the level of the one clip here so a decode at unity stays what it always was (<c>Game/Sfx</c> decodes to
        /// the same bytes), and a music sample never gets near it once the headroom is applied.
        /// </summary>
        private const float CLIP_LEVEL = 0.99999994f;

        /// <summary>
        /// The 16-bit value a clipped sample is left at, either way round: <see cref="CLIP_LEVEL"/> converted. Never 32767,
        /// which a clip at 1.0 would give and this one cannot, so a count of samples on the rail asks for this or more.
        /// </summary>
        public const int RAIL_SAMPLE = 32766;

        /// <summary>Decodes at unity: the sound effects' and the fanfares', and what <c>Tools/MusicBake</c> measures a file by.</summary>
        public static byte[] Decode(string path, int sampleRate)
        {
            using FileStream file = File.OpenRead(path);
            return Decode(file, sampleRate);
        }

        /// <summary>Decodes a recording of the music with <see cref="MusicLevels.DECODE_HEADROOM"/>, the way the game plays it.</summary>
        public static byte[] DecodeMusic(string path, int sampleRate)
        {
            using FileStream file = File.OpenRead(path);
            return DecodeMusic(file, sampleRate);
        }

        /// <inheritdoc cref="DecodeMusic(string, int)"/>
        public static byte[] DecodeMusic(Stream stream, int sampleRate) => Decode(stream, sampleRate, MusicLevels.DECODE_HEADROOM, out _);

        /// <summary>Decodes at unity: see <see cref="Decode(string, int)"/>.</summary>
        public static byte[] Decode(Stream stream, int sampleRate) => Decode(stream, sampleRate, 1f, out _);

        /// <summary>
        /// Decodes a stereo Ogg Vorbis stream at <paramref name="sampleRate"/>, refusing any other shape rather than
        /// playing it at the wrong speed. Converted as <see cref="ProceduralMusic.ToPcm(float[])"/> converts — clamp,
        /// then scale to shorts — so a track decodes to what the .wav it replaced held, give or take the codec.
        /// <paramref name="gain"/> multiplies every sample before the clamp, and <paramref name="peak"/> is the largest
        /// sample the codec produced before it, unclipped and ungained, as a fraction of full scale: over 1 is an overshoot,
        /// which is what <c>Tools/MusicBake</c> holds to <see cref="MusicLevels.MAX_CODEC_PEAK"/>.
        /// </summary>
        public static byte[] Decode(Stream stream, int sampleRate, float gain, out float peak)
        {
            using VorbisReader reader = new(stream, closeOnDispose: false);

            //Ours is the one clip, after the gain (see the class remarks)
            reader.ClipSamples = false;

            if (reader.Channels != 2 || reader.SampleRate != sampleRate)
                throw new InvalidDataException($"expected stereo at {sampleRate} Hz, found {reader.Channels} channels at {reader.SampleRate} Hz");

            long frames = reader.TotalSamples;
            if (frames <= 0 || frames > int.MaxValue / 4)
                throw new InvalidDataException($"implausible length of {frames} frames");

            //Sized off the stream's own count, which is also where the decode stops (see the class remarks)
            byte[] pcm = new byte[frames * 4];
            float[] chunk = new float[CHUNK_FRAMES * 2];
            int written = 0;
            peak = 0f;

            for (int read; written < pcm.Length && (read = reader.ReadSamples(chunk, 0, chunk.Length)) > 0;)
            {
                int take = Math.Min(read, (pcm.Length - written) / 2);

                for (int i = 0; i < take; i++)
                {
                    float a = MathF.Abs(chunk[i]);
                    if (a > peak) peak = a;

                    short v = (short)(Math.Clamp(chunk[i] * gain, -CLIP_LEVEL, CLIP_LEVEL) * short.MaxValue);
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
