using BS3D.Audio;
using System;
using System.Collections.Generic;
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
        public void The_chunk_book_is_a_queue_oldest_first()
        {
            ChunkQueue book = new(3);
            Assert.Equal(0, book.Count);

            book.Push(0);
            book.Push(24000);
            book.Push(48000);
            Assert.Equal(3, book.Count);
            Assert.Equal(0, book.HeadStart);

            //The voice has played the first: two are left queued and the second is what sounds
            Assert.Equal(1, book.Retire(2));
            Assert.Equal(2, book.Count);
            Assert.Equal(24000, book.HeadStart);

            //The voice holds as many as the book does, or more: nothing to strike off
            Assert.Equal(0, book.Retire(2));
            Assert.Equal(0, book.Retire(3));
            Assert.Equal(24000, book.HeadStart);

            //A stall: both of the others played in one frame
            Assert.Equal(2, book.Retire(0));
            Assert.Equal(0, book.Count);

            book.Push(7);
            Assert.Equal(7, book.HeadStart);

            book.Clear();
            Assert.Equal(0, book.Count);
        }

        [Fact]
        public void The_chunk_book_follows_a_plain_queue_through_the_wrap_of_its_ring()
        {
            ChunkQueue book = new(3);
            Queue<int> expected = new();
            Random random = new(7);
            int next = 0;

            for (int step = 0; step < 5000; step++)
            {
                //What the voice does: plays the oldest chunks, then takes new ones up to its depth
                int pending = random.Next(0, expected.Count + 1);
                int retired = book.Retire(pending);
                Assert.Equal(expected.Count - pending, retired);
                while (expected.Count > pending) expected.Dequeue();

                while (expected.Count < 3 && random.Next(3) != 0)
                {
                    book.Push(next);
                    expected.Enqueue(next);
                    next += 24000;
                }

                Assert.Equal(expected.Count, book.Count);
                if (expected.Count > 0) Assert.Equal(expected.Peek(), book.HeadStart);
            }
        }

        [Fact]
        public void A_chunk_book_that_is_overfilled_forgets_its_oldest_chunk_and_a_negative_count_is_none()
        {
            ChunkQueue book = new(2);
            book.Push(1);
            book.Push(2);
            book.Push(3);

            Assert.Equal(2, book.Count);
            Assert.Equal(2, book.HeadStart);

            Assert.Equal(2, book.Retire(-5));
            Assert.Equal(0, book.Count);

            Assert.Throws<ArgumentOutOfRangeException>(() => new ChunkQueue(0));
        }

        //The voice a recording is queued onto, as far as the clock cares: chunks played in order at the sample rate, a
        //count that falls when one has been played, and silence (the frames lost) when the queue has run dry
        private sealed class SimulatedVoice
        {
            private readonly Queue<(int Start, int Count)> _queue = new();
            private double _playedInHead;

            public int PendingBufferCount => _queue.Count;

            public bool Sounding => _queue.Count > 0;

            /// <summary>Where in the recording the voice is audibly, in frames.</summary>
            public double AudibleFrame => _queue.Peek().Start + _playedInHead;

            public void Submit(int start, int count) => _queue.Enqueue((start, count));

            public void Advance(double frames)
            {
                while (frames > 0 && _queue.Count > 0)
                {
                    double left = _queue.Peek().Count - _playedInHead;

                    if (frames >= left)
                    {
                        frames -= left;
                        _queue.Dequeue();
                        _playedInHead = 0;
                    }
                    else
                    {
                        _playedInHead += frames;
                        frames = 0;
                    }
                }
            }
        }

        //RecordingPlayer.Feed's book-keeping, line for line, with the pin switched off for the old clock
        private static void Feed(SimulatedVoice voice, ChunkQueue chunks, ref int submitted, int total, bool pinning, ref double clock)
        {
            bool pin = false;

            for (int queued = 0; queued < QUEUE_DEPTH; queued++)
            {
                int pending = voice.PendingBufferCount;

                if (chunks.Retire(pending) > 0) pin = true;
                if (pending >= QUEUE_DEPTH) break;

                int count = RecordingLoop.ChunkFrames(submitted, total, CHUNK);
                voice.Submit(submitted, count);
                chunks.Push(submitted);

                if (chunks.Count == 1) pin = true;

                submitted = RecordingLoop.NextStart(submitted, count, total);
            }

            if (pinning && pin && chunks.Count > 0) clock = chunks.HeadStart / (double)RATE;
        }

        private static double Distance(double a, double b, double period)
        {
            double distance = Math.Abs(a - b) % period;
            return Math.Min(distance, period - distance);
        }

        /// <summary>
        /// Runs the player's clock beside the voice at 75 Hz and returns the worst distance between where the bars are and
        /// where the music audibly is, over the seconds from <paramref name="checkFrom"/>. One frame stalls at
        /// <paramref name="stallAt"/> for <paramref name="stallLength"/> seconds of real time (the voice plays on, or runs
        /// dry and stands silent); the clock is given the stall as well, or not (a frame time the game clamps).
        /// </summary>
        private static double WorstClockError(int total, bool pinning, double stallAt, double stallLength, bool clockSeesStall, double checkFrom, double until)
        {
            const double FRAME = 1.0 / 75.0;

            SimulatedVoice voice = new();
            ChunkQueue chunks = new(QUEUE_DEPTH);
            double duration = total / (double)RATE;
            int submitted = 0;
            double clock = 0;
            double now = 0;
            double worst = 0;
            bool stalled = false;

            Feed(voice, chunks, ref submitted, total, pinning, ref clock);

            while (now < until)
            {
                double real = FRAME;
                double given = FRAME;

                if (!stalled && now >= stallAt)
                {
                    stalled = true;
                    real += stallLength;
                    if (clockSeesStall) given += stallLength;
                }

                now += real;
                voice.Advance(real * RATE);

                //The player's order: the clock moves, then the feed may pin it
                clock = RecordingLoop.WrapPosition(clock + given, duration);
                Feed(voice, chunks, ref submitted, total, pinning, ref clock);

                if (now >= checkFrom && voice.Sounding)
                    worst = Math.Max(worst, Distance(clock, voice.AudibleFrame / RATE, duration));
            }

            return worst;
        }

        private const int RATE = 48000;
        private const int QUEUE_DEPTH = 3;

        [Theory]
        [InlineData(100_000)]
        [InlineData(CHUNK * 4)]
        public void Without_a_stall_the_clock_keeps_with_the_voice_whether_or_not_it_is_pinned(int total)
        {
            const double ONE_AND_A_HALF_FRAMES = 1.5 / 75.0;

            //The simulation itself holds: a plain wall clock is right until something stalls, and a pinned one is never worse
            Assert.True(WorstClockError(total, pinning: false, 1e9, 0, false, 1.0, 40.0) < ONE_AND_A_HALF_FRAMES);
            Assert.True(WorstClockError(total, pinning: true, 1e9, 0, false, 1.0, 40.0) < ONE_AND_A_HALF_FRAMES);
        }

        [Theory]
        [InlineData(100_000, 0.4)]
        [InlineData(100_000, 1.2)]
        [InlineData(CHUNK * 4, 0.4)]
        public void After_a_stall_the_clock_is_put_right_at_the_next_chunk_and_the_old_clock_never_was(int total, double stall)
        {
            const double ONE_AND_A_HALF_FRAMES = 1.5 / 75.0;

            //The stall is a frame time the game does not give the clock: the voice played on, the bars did not
            double stalledClock = WorstClockError(total, pinning: false, 10.0, stall, clockSeesStall: false, 11.5, 60.0);
            double pinnedClock = WorstClockError(total, pinning: true, 10.0, stall, clockSeesStall: false, 11.5, 60.0);

            //Seen to fail first: the clock that only adds up wall time stays off by the stall for the rest of the playing...
            Assert.True(stalledClock > 0.3, $"the old clock is off by only {stalledClock:F3} s: the stall is too short to tell the two apart");

            //... and the pinned one is right again within one frame, from a chunk's length after the stall on
            Assert.True(pinnedClock < ONE_AND_A_HALF_FRAMES, $"the pinned clock is off by {pinnedClock:F3} s");
        }

        [Theory]
        [InlineData(100_000)]
        [InlineData(CHUNK * 4)]
        public void After_a_stall_that_ran_the_queue_dry_the_clock_is_right_from_the_first_chunk_queued_again(int total)
        {
            const double ONE_AND_A_HALF_FRAMES = 1.5 / 75.0;

            //Four seconds against a queue of 1.5: the voice stood silent for two and a half while the wall clock, given the
            //whole stall, went on
            double stalledClock = WorstClockError(total, pinning: false, 10.0, 4.0, clockSeesStall: true, 14.0, 60.0);
            double pinnedClock = WorstClockError(total, pinning: true, 10.0, 4.0, clockSeesStall: true, 14.0, 60.0);

            Assert.True(stalledClock > 0.3, $"the old clock is off by only {stalledClock:F3} s: the run-dry is too short to tell the two apart");
            Assert.True(pinnedClock < ONE_AND_A_HALF_FRAMES, $"the pinned clock is off by {pinnedClock:F3} s");
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

        [Fact]
        public void The_players_clock_is_pinned_to_the_chunk_the_voice_is_playing()
        {
            string code = WithoutComments(File.ReadAllText(PlayerPath()));

            //The feed keeps the book of chunk starts and pins the clock to its head when a chunk has gone
            string feed = Between(code, "private void Feed()", "private void ReleaseVoice()");
            Assert.Contains("_chunks.Retire(pending)", feed);
            Assert.Contains("_chunks.Push(_submittedFrames);", feed);
            Assert.Contains("_position = _chunks.HeadStart / (double)SAMPLE_RATE;", feed);

            //The clock moves before the feed, so a pin is not pushed on by a frame that stalled
            string update = Between(code, "public void Update(float elapsed)", "private Task<byte[]> Decode(");
            Assert.True(update.IndexOf("RecordingLoop.WrapPosition(", StringComparison.Ordinal) < update.IndexOf("Feed();", StringComparison.Ordinal),
                "the clock must move before the feed pins it");

            //A voice let go leaves nothing in the book
            Assert.Contains("_chunks.Clear();", Between(code, "private void ReleaseVoice()", "public void Dispose()"));
        }

        [Fact]
        public void The_about_players_clock_is_pinned_the_same_way()
        {
            string code = WithoutComments(File.ReadAllText(SourcePath("ProceduralJukebox.cs")));

            string feed = Between(code, "private void Feed()", "private void Release()");
            Assert.Contains("_chunks.Retire(pending)", feed);
            Assert.Contains("_chunks.Push(_submittedFrames);", feed);
            Assert.Contains("_position = _chunks.HeadStart / (double)ProceduralMusic.SAMPLE_RATE;", feed);

            string update = Between(code, "public void Update(float elapsed)", "private void Start()");
            Assert.True(update.IndexOf("_position = (_position + elapsed)", StringComparison.Ordinal) < update.IndexOf("Feed();", StringComparison.Ordinal),
                "the clock must move before the feed pins it");

            Assert.Contains("_chunks.Clear();", Between(code, "private void Release()", "public void Dispose()"));
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

        private static string PlayerPath() => SourcePath("RecordingPlayer.cs");

        private static string SourcePath(string fileInGameAudio)
        {
            string relative = Path.Combine("Game", "Audio", fileInGameAudio);

            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}
