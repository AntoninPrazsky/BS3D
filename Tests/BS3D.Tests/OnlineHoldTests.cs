using BS3D.Online;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Scoring;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// A clear the score service refuses because it does not know the board yet (#1045): a release with new scoring rules
    /// arrives before the service has loaded their ceilings, and every clear it sends until then is refused as
    /// <c>unknown-board</c>. The real <see cref="OnlineScores"/> against the loopback stand-in: such a clear of this build's
    /// rules is held in the outbox and tried once a start of the game, after every clear never held, so it neither blocks nor
    /// spends the per-minute allowance a fresh clear needs; it goes once the service knows the board, is dropped after
    /// <see cref="OnlineScores.MaxHolds"/> starts, and is the first to go from a full outbox. A clear of older rules on an
    /// unknown board is dropped as it always was. Alone, with the other online tests (<see cref="OnlineStatsCollection"/>).
    /// </summary>
    [Collection(OnlineStatsCollection.Name)]
    public sealed class OnlineHoldTests : IDisposable
    {
        private const string UNKNOWN_BOARD = "{\"reason\":\"unknown-board\"}";
        private const string HASH = "0123456789abcdef";

        private readonly TempDirectory _temp = new();
        private readonly StandIn _server = new();

        //One player across every start of a test, as the game keeps one across its starts
        private readonly OnlineIdentity _identity = OnlineIdentity.Create("Tester");
        private string Outbox => _temp.File(OnlineScores.OutboxFileName);

        public void Dispose()
        {
            _server.Dispose();
            _temp.Dispose();
        }

        private OnlineScores Client() =>
            OnlineScores.Start(new GameSettings { Online = true, Server = _server.Url }, _identity, Outbox);

        private static ScoreSubmission Clear(OnlineScores client, string file, int rules = ScoreKeeper.RulesVersion)
        {
            ScoreSubmission submission = client.NewSubmission(new LevelIdentity(file, HASH), 900, 3, 4, 12.5f);
            submission.RulesVersion = rules;
            return submission;
        }

        private List<StandIn.Request> Scores => _server.Requests.Where(r => r.Path == "/v1/scores").ToList();

        private int Sent(string file) => Scores.Count(r => FileOf(r) == file);

        private static string FileOf(StandIn.Request request)
        {
            using JsonDocument body = JsonDocument.Parse(request.Body);
            return body.RootElement.GetProperty("level").GetProperty("file").GetString();
        }

        private OutboxFile ReadOutbox()
        {
            try
            {
                if (!File.Exists(Outbox)) return null;

                using FileStream stream = new(Outbox, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return JsonSerializer.Deserialize<OutboxFile>(stream);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
        }

        private List<string> Queued() => ReadOutbox()?.Submissions.Select(s => s.Level.File).ToList();

        //How many starts the clear queued for a file has been held, as the outbox says on disk; -1 when none is queued
        private int Holds(string file)
        {
            OutboxFile outbox = ReadOutbox();
            ScoreSubmission queued = outbox?.Submissions.FirstOrDefault(s => s.Level.File == file);
            if (queued == null) return -1;

            return outbox.Held != null && outbox.Held.TryGetValue(queued.SubmissionId, out int holds) ? holds : 0;
        }

        //The worker ended, within the wait: its own failure is its own to log
        private static void Stop(OnlineScores client)
        {
            try { client.Stop().Wait(TimeSpan.FromSeconds(20)); } catch (AggregateException) { }
        }

        private static bool Within(Func<bool> condition, double seconds = 20)
        {
            Stopwatch clock = Stopwatch.StartNew();
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                if (condition()) return true;
                Thread.Sleep(20);
            }
            return condition();
        }

        [Fact]
        public void A_clear_on_a_board_the_service_does_not_know_yet_is_held_and_goes_at_a_start_once_it_does()
        {
            //The service without the new table for one board: it refuses that board's clears and takes every other
            bool known = false;
            _server.ScoreAnswerFor = body => !Volatile.Read(ref known) && body.Contains("Held.json") ? (422, UNKNOWN_BOARD) : null;
            OnlineScores client = Client();

            client.Submit(Clear(client, "Held.json"));
            Assert.True(Within(() => Sent("Held.json") == 1 && Holds("Held.json") == 1), "the refused clear is held, not dropped");

            //A clear on a board the service knows goes past it, and the held one stays, not tried again this start
            client.Submit(Clear(client, "Known.json"));
            Assert.True(Within(() => Sent("Known.json") == 1 && Queued() is ["Held.json"]), "the known board's clear went past the held one");
            Stop(client);
            Assert.Equal(1, Sent("Held.json"));

            //The service learns the board, and the next start sends the held clear, which leaves the outbox
            Volatile.Write(ref known, true);
            client = Client();
            Assert.True(Within(() => Queued() is { Count: 0 }), "the held clear went at the next start once the board was known");
            Stop(client);

            Assert.Equal(2, Sent("Held.json"));
            Assert.Null(ReadOutbox().Held);
        }

        /// <summary>
        /// The service counts every submission against the player's allowance a minute, before it looks at the board
        /// (BS3D-API), so held clears tried again at every drain used the allowance up and a fresh clear behind them was
        /// answered 429 and kept as if the service were away. A fresh clear goes first, and the held ones are not tried again
        /// before the next start.
        /// </summary>
        [Fact]
        public void A_fresh_clear_goes_before_the_held_ones_and_they_do_not_spend_its_allowance()
        {
            //The allowance: three submissions, then 429 until it is handed back (a minute passing)
            const int ALLOWANCE = 3;
            int used = 0;
            _server.ScoreAnswerFor = body =>
                Interlocked.Increment(ref used) > ALLOWANCE ? (429, null)
                : body.Contains("Unknown") ? (422, UNKNOWN_BOARD)
                : null;

            OnlineScores client = Client();
            foreach (string file in new[] { "Unknown1.json", "Unknown2.json", "Unknown3.json" })
                client.Submit(Clear(client, file));
            Assert.True(Within(() => Holds("Unknown1.json") == 1 && Holds("Unknown2.json") == 1 && Holds("Unknown3.json") == 1),
                "the three clears on unknown boards are held");

            //A minute later, a fresh clear: it goes, and none of the held ones is sent again this start
            Interlocked.Exchange(ref used, 0);
            client.Submit(Clear(client, "Fresh.json"));
            Assert.True(Within(() => Sent("Fresh.json") == 1 && Queued() is { Count: 3 } queued && !queued.Contains("Fresh.json")),
                "the fresh clear was delivered");
            Thread.Sleep(300);
            Stop(client);

            Assert.Equal(4, Scores.Count);
            Assert.Equal(1, Volatile.Read(ref used));

            //The next start, the allowance handed back and a fresh clear made at once: it goes before the allowance is spent on
            //the held ones, which go around it until it is
            Interlocked.Exchange(ref used, 0);
            client = Client();
            client.Submit(Clear(client, "Fresh2.json"));
            Assert.True(Within(() => Sent("Fresh2.json") == 1 && Queued() is { } left && !left.Contains("Fresh2.json")),
                "the second fresh clear was delivered");
            Stop(client);
        }

        [Fact]
        public void A_clear_held_for_MaxHolds_starts_is_dropped()
        {
            _server.ScoreAnswerFor = _ => (422, UNKNOWN_BOARD);

            for (int start = 1; start <= OnlineScores.MaxHolds; start++)
            {
                OnlineScores client = Client();
                if (start == 1) client.Submit(Clear(client, "Never.json"));

                int tried = start, expected = start < OnlineScores.MaxHolds ? start : -1;
                Assert.True(Within(() => Sent("Never.json") == tried && Holds("Never.json") == expected),
                    $"start {start}: tried once, and " + (expected < 0 ? "dropped" : $"held {expected} time(s)"));
                Stop(client);
            }

            Assert.Equal(OnlineScores.MaxHolds, Sent("Never.json"));
            Assert.Empty(Queued());
        }

        [Fact]
        public void A_full_outbox_drops_a_held_clear_before_one_that_would_go()
        {
            //The service away: nothing leaves
            _server.ScoreAnswerFor = _ => (503, null);

            //The outbox as an earlier run left it: full, its oldest clear never held and the one after it held once
            List<ScoreSubmission> waiting = Enumerable.Range(0, OnlineScores.OutboxCapacity).Select(i => new ScoreSubmission
            {
                SubmissionId = Guid.NewGuid(),
                PlayerId = _identity.PlayerId,
                Level = new SubmittedLevel { File = $"L{i}.json", Hash = HASH },
                RulesVersion = ScoreKeeper.RulesVersion,
                Score = 900,
                Stars = 3,
                ShotsUsed = 4,
                DurationSeconds = 12.5f,
                GameVersion = "dev",
            }).ToList();
            File.WriteAllText(Outbox, JsonSerializer.Serialize(new OutboxFile
            {
                Submissions = waiting,
                Held = new Dictionary<Guid, int> { [waiting[1].SubmissionId] = 1 },
            }));

            OnlineScores client = Client();
            Assert.True(Within(() => Scores.Count >= 1), "the start's drain tried the oldest clear");

            client.Submit(Clear(client, "New.json"));
            Assert.True(Within(() => Queued() is { Count: OnlineScores.OutboxCapacity } queued && queued[^1] == "New.json"),
                "the new clear is queued at the back of a full outbox");
            Stop(client);

            List<string> left = Queued();
            Assert.Contains("L0.json", left);
            Assert.DoesNotContain("L1.json", left);
            Assert.Null(ReadOutbox().Held);
        }

        [Fact]
        public void A_clear_of_older_rules_on_an_unknown_board_is_dropped()
        {
            _server.ScoreAnswers.Enqueue((422, UNKNOWN_BOARD));
            OnlineScores client = Client();

            client.Submit(Clear(client, "Old.json", rules: ScoreKeeper.RulesVersion - 1));
            Assert.True(Within(() => Scores.Count == 1 && Queued() is { Count: 0 }), "the old rules' refusal is final");
            Stop(client);

            Assert.Single(Scores);
        }

        [Theory]
        [InlineData(ScoreKeeper.RulesVersion, "unknown-board", true)]
        [InlineData(ScoreKeeper.RulesVersion - 1, "unknown-board", false)]
        [InlineData(ScoreKeeper.RulesVersion, "over-ceiling", false)]
        [InlineData(ScoreKeeper.RulesVersion, null, false)]
        public void Only_an_unknown_board_of_this_builds_rules_is_held(int rules, string reason, bool held) =>
            Assert.Equal(held, OnlineScores.IsHeldForBoard(new ScoreSubmission { RulesVersion = rules }, new ScoreAnswerBody { Reason = reason }));
    }
}
