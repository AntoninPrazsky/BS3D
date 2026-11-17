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
    /// rules is held in the outbox and goes once the service knows the board, a clear on a known board is not stuck behind it,
    /// and a clear of older rules on an unknown board is dropped as it always was. Alone, with the other online tests
    /// (<see cref="OnlineStatsCollection"/>).
    /// </summary>
    [Collection(OnlineStatsCollection.Name)]
    public sealed class OnlineHoldTests : IDisposable
    {
        private const string UNKNOWN_BOARD = "{\"reason\":\"unknown-board\"}";

        private readonly TempDirectory _temp = new();
        private readonly StandIn _server = new();
        private string Outbox => _temp.File(OnlineScores.OutboxFileName);

        public void Dispose()
        {
            _server.Dispose();
            _temp.Dispose();
        }

        private OnlineScores Client() =>
            OnlineScores.Start(new GameSettings { Online = true, Server = _server.Url }, OnlineIdentity.Create("Tester"), Outbox);

        private static ScoreSubmission Clear(OnlineScores client, string file, int rules = ScoreKeeper.RulesVersion)
        {
            ScoreSubmission submission = client.NewSubmission(new LevelIdentity(file, "0123456789abcdef"), 900, 3, 4, 12.5f);
            submission.RulesVersion = rules;
            return submission;
        }

        private List<StandIn.Request> Scores => _server.Requests.Where(r => r.Path == "/v1/scores").ToList();

        private static string FileOf(StandIn.Request request)
        {
            using JsonDocument body = JsonDocument.Parse(request.Body);
            return body.RootElement.GetProperty("level").GetProperty("file").GetString();
        }

        private List<string> Queued()
        {
            try
            {
                if (!File.Exists(Outbox)) return null;

                using FileStream stream = new(Outbox, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return JsonSerializer.Deserialize<OutboxFile>(stream)?.Submissions.Select(s => s.Level.File).ToList();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
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
        public void A_clear_on_a_board_the_service_does_not_know_yet_is_held_and_goes_once_it_does()
        {
            //The service without the new table for one board: it refuses that board's clears and takes every other
            bool known = false;
            _server.ScoreAnswerFor = body => !Volatile.Read(ref known) && body.Contains("Held.json") ? (422, UNKNOWN_BOARD) : null;
            OnlineScores client = Client();

            client.Submit(Clear(client, "Held.json"));
            Assert.True(Within(() => Scores.Count >= 1 && Queued() is ["Held.json"]), "the refused clear is held, not dropped");

            //A clear on a board the service knows goes past it, and the held one stays
            client.Submit(Clear(client, "Known.json"));
            Assert.True(Within(() => Scores.Any(r => FileOf(r) == "Known.json") && Queued() is ["Held.json"]),
                "the known board's clear went past the held one, which stayed");

            //And at the next drain the service knows the board: the held clear goes and leaves the outbox
            Volatile.Write(ref known, true);
            client.Submit(Clear(client, "Later.json"));
            Assert.True(Within(() => Queued() is { Count: 0 }), "the held clear went once the board was known");
            Stop(client);

            Assert.Contains(Scores, r => FileOf(r) == "Later.json");
            Assert.True(Scores.Count(r => FileOf(r) == "Held.json") >= 2, "the held clear was sent again");
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
