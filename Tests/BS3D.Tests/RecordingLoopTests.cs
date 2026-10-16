using BS3D.Audio;
using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The Jukebox's recording plays round and round until the player picks another or leaves (#899). A voice and a device
    /// cannot be had here, so what is held is the arithmetic the player queues by (<see cref="RecordingLoop"/>: the chunks
    /// that walk a recording and come back to its first frame, the clock that wraps with it) and, by reading the player's
    /// source as <see cref="JukeboxPageSourceTests"/> reads the page's, that the player is built on it and no longer ends a
    /// recording by itself. That the voice really sounds across the seam is seen in a Game run (the issue's evidence).
    /// </summary>
    public class RecordingLoopTests
    {
        //The player's own figures: 48 kHz and half a second a chunk
        private const int CHUNK = 24000;

        [Theory]
        [InlineData(100_000)]      //a few chunks and a short one at the end
        [InlineData(CHUNK * 4)]    //a whole number of chunks: no short one
        [InlineData(CHUNK - 1)]    //shorter than one chunk
        [InlineData(1)]            //a single frame
        public void The_queue_walks_the_recording_over_and_over_without_a_gap_or_a_repeat(int total)
        {
            const int ROUNDS = 3;

            int[] heard = new int[total];
            int start = 0, queued = 0, chunks = 0;

            //Chunk after chunk, as the player queues them, until ROUNDS whole rounds of the recording are in
            while (queued < total * ROUNDS)
            {
                Assert.InRange(start, 0, total - 1);

                int count = RecordingLoop.ChunkFrames(start, total, CHUNK);
                Assert.InRange(count, 1, CHUNK);
                Assert.True(start + count <= total, "a chunk reads past the last frame");

                for (int i = 0; i < count; i++) heard[start + i]++;

                int next = RecordingLoop.NextStart(start, count, total);

                //Right behind the chunk, or the first frame once the last has been queued: the seam
                Assert.Equal(start + count == total ? 0 : start + count, next);

                queued += count;
                start = next;
                chunks++;
                Assert.True(chunks < 100_000, "the queue never comes round to the end of the recording");
            }

            //Every frame exactly once a round: nothing skipped at the seam, nothing played twice
            Assert.Equal(total * ROUNDS, queued);
            Assert.All(heard, count => Assert.Equal(ROUNDS, count));
            Assert.Equal(0, start);
        }

        [Fact]
        public void The_chunk_after_the_last_frame_is_the_first_again()
        {
            //The old behaviour: the last chunk was the end. Now the chunk after the last frame is the first frame again
            Assert.Equal(0, RecordingLoop.NextStart(100_000 - 4000, 4000, 100_000));
            Assert.Equal(4000, RecordingLoop.NextStart(0, 4000, 100_000));
        }

        [Fact]
        public void The_position_clock_wraps_by_the_recordings_length()
        {
            const double LENGTH = 40.0;

            Assert.Equal(0.0, RecordingLoop.WrapPosition(0.0, LENGTH));
            Assert.Equal(12.5, RecordingLoop.WrapPosition(12.5, LENGTH));

            //At the end it is the start of the next round, not the end held (the old clock stopped at the end)
            Assert.Equal(0.0, RecordingLoop.WrapPosition(LENGTH, LENGTH));
            Assert.Equal(0.25, RecordingLoop.WrapPosition(LENGTH + 0.25, LENGTH), 9);

            //A frame that hitched for longer than the recording is long still lands inside it
            Assert.Equal(5.0, RecordingLoop.WrapPosition(LENGTH * 7 + 5.0, LENGTH), 9);
        }

        [Fact]
        public void The_clock_stepped_by_frames_stays_inside_the_recording_and_comes_back_to_the_top()
        {
            const double LENGTH = 10.0;
            const double FRAME = 1.0 / 75.0;

            double position = 0;
            int wraps = 0;

            //65 seconds of 75 Hz frames over a ten second recording: it comes back to the top six times
            for (int frame = 0; frame < 75 * 65; frame++)
            {
                double before = position;
                position = RecordingLoop.WrapPosition(position + FRAME, LENGTH);

                Assert.InRange(position, 0.0, LENGTH);
                Assert.NotEqual(LENGTH, position);

                if (position < before) wraps++;
            }

            Assert.Equal(6, wraps);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        public void A_recording_with_no_length_has_no_position_but_the_start(double length)
        {
            Assert.Equal(0.0, RecordingLoop.WrapPosition(3.0, length));
        }

        [Fact]
        public void A_position_before_the_start_is_the_start()
        {
            Assert.Equal(0.0, RecordingLoop.WrapPosition(-0.5, 40.0));
        }

        [Fact]
        public void The_player_loops_and_no_longer_moves_on_when_a_recording_ends()
        {
            string code = WithoutComments(File.ReadAllText(PlayerPath()));

            //Built on the loop's arithmetic, both in the feed and in the clock
            Assert.Contains("RecordingLoop.ChunkFrames(", code);
            Assert.Contains("RecordingLoop.NextStart(", code);
            Assert.Contains("RecordingLoop.WrapPosition(", code);

            //The bars cross the seam, and the old played-once reading is gone
            Assert.Contains("loops: true", code);
            Assert.DoesNotContain("loops: false", code);

            //Nothing ends a recording by itself: the feed does not call Next, and does not stop at the recording's end
            string feed = Between(code, "private void Feed()", "private void ReleaseVoice()");
            Assert.DoesNotContain("Next()", feed);
            Assert.DoesNotContain("PlayAt(", feed);
            Assert.DoesNotContain("_submittedFrames < _totalFrames", feed);

            //The manual switches are still the way on, and a recording that cannot be read is still stepped past
            Assert.Contains("public void Next() => PlayAt(_index + 1);", code);
            Assert.Contains("PlayAt(_index + 1);", Between(code, "private void Open()", "private void Feed()"));
        }

        private static string Between(string source, string from, string to)
        {
            int start = source.IndexOf(from, StringComparison.Ordinal);
            Assert.True(start >= 0, $"'{from}' is not in the source: the test needs updating");

            int end = source.IndexOf(to, start, StringComparison.Ordinal);
            Assert.True(end > start, $"'{to}' does not follow '{from}': the test needs updating");

            return source.Substring(start, end - start);
        }

        private static string WithoutComments(string source) => Regex.Replace(source, @"//[^\r\n]*", string.Empty);

        private static string PlayerPath()
        {
            string relative = Path.Combine("Game", "Audio", "RecordingPlayer.cs");

            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}
