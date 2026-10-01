using System;

namespace BS3D.Audio
{
    /// <summary>
    /// A recording's drums, as a layer the game can turn down (#495): the music relaxes while the cluster hangs safely
    /// high and tightens as it nears the line. The layer is the recording's own drums, separated out of the shipped
    /// loop by Demucs (<c>C:/Users/panrd/AI/stems/separate_drums.py</c>) and written beside it by
    /// <c>Tools/MusicBake --drums</c> as <c>Music/Drums/&lt;recording&gt;.ogg</c> — the same length to the frame and
    /// aligned to it sample for sample, so what plays is <c>full − (1 − g) × drums</c>: at <c>g = 1</c> exactly the
    /// recording as it always was, and below it the same piece with its drums turned down. Not a second generation
    /// of the piece, which the issue first proposed: ACE-Step has come back in another tempo than it was asked for,
    /// and two renders could never be cross-faded without one of them being heard to drift.
    /// <para>
    /// Pure arithmetic over the 16-bit buffers <c>OggTrack</c> decodes, in a file of its own so that MusicBake
    /// (which writes the layer at <see cref="STORED_SCALE"/>) and the tests compile the same lines the game plays.
    /// </para>
    /// </summary>
    internal static class DrumLayer
    {
        /// <summary>
        /// The level a drums layer is stored at against its recording: half. A separated stem can peak over full scale
        /// where the mix it came out of does not — the parts it was separated from cancel some of it in the mix — and
        /// Meadow's first recording measured 1.10 against a mix under 1, so stored at its own level it would clip.
        /// <see cref="Mix"/> scales it back up; at 16 bits, the step lost is far under anything a drum kit is.
        /// </summary>
        public const float STORED_SCALE = 0.5f;

        /// <summary>
        /// How loud the drums play at no danger at all, against their full level at the line. Not silent: a groove with
        /// its drums gone entirely is a different piece, and the point is the same piece tightening. The owner's ear
        /// decides. Here rather than in <c>GameMusic</c> because MusicBake checks that the recording mixed at it does
        /// not clip: the mix is linear in the gain, so its loudest moment lies at one end - full gain, the recording as
        /// it always was, or this one.
        /// </summary>
        public const float CALM_GAIN = 0.2f;

        /// <summary>
        /// Writes <paramref name="count"/> frames of the loop, from frame <paramref name="start"/> on (wrapping at the
        /// loop's end, which is where a seamless loop runs straight on), into <paramref name="output"/>: the recording
        /// with its drums at a gain ramped linearly from <paramref name="gainFrom"/> to <paramref name="gainTo"/> across
        /// the chunk, so a change of gain is never a step. Interleaved 16-bit stereo, little-endian, as
        /// <c>OggTrack.Decode</c> hands it; clamped, though a mix between the recording
        /// and the recording without its drums stays within the larger of the two.
        /// </summary>
        /// <returns>The frame after the last one written, wrapped — where the next chunk starts.</returns>
        public static int Mix(byte[] full, byte[] drums, int start, int count, float gainFrom, float gainTo, byte[] output)
        {
            int frames = full.Length / 4;
            if (frames == 0 || drums.Length != full.Length) throw new ArgumentException("the layer is not its recording's length");
            if (output.Length < count * 4) throw new ArgumentException("the output is shorter than the chunk");

            int at = start % frames;
            float step = (gainTo - gainFrom) / count;

            for (int i = 0; i < count; i++)
            {
                float cut = (1f - (gainFrom + step * (i + 1))) / STORED_SCALE;
                int b = at * 4;

                for (int channel = 0; channel < 4; channel += 2)
                {
                    short f = (short)(full[b + channel] | (full[b + channel + 1] << 8));
                    short d = (short)(drums[b + channel] | (drums[b + channel + 1] << 8));

                    int v = (int)MathF.Round(f - cut * d);
                    if (v > short.MaxValue) v = short.MaxValue;
                    else if (v < short.MinValue) v = short.MinValue;

                    output[i * 4 + channel] = (byte)(v & 0xff);
                    output[i * 4 + channel + 1] = (byte)((v >> 8) & 0xff);
                }

                if (++at == frames) at = 0;
            }

            return at;
        }
    }
}
