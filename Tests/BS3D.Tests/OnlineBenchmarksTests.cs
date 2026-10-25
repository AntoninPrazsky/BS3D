using BS3D.Online;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The benchmark's result on its way to the author (#988), the real <see cref="OnlineBenchmarks"/> against the loopback
    /// stand-in: written to the outbox first, sent without a token, gone only once the service names it, kept on no answer, a
    /// stranger's 2xx or a 429 and sent at the next start, dropped on a refusal with a reason, and kept on the machine by a
    /// build that reaches no server, the outbox held to its capacity.
    /// </summary>
    [Collection(OnlineStatsCollection.Name)]
    public sealed class OnlineBenchmarksTests : IDisposable
    {
        private readonly TempDirectory _temp = new();
        private readonly StandIn _server = new();
        private string Outbox => _temp.File(OnlineBenchmarks.FolderName);

        public void Dispose()
        {
            _server.Dispose();
            _temp.Dispose();
        }

        private OnlineBenchmarks Client(string server = "") =>
            OnlineBenchmarks.Start(new GameSettings { Server = server == "" ? _server.Url : server }, Outbox);

        private static JsonObject Result(string gpu = "Test GPU") => new()
        {
            ["budgetMs"] = 13.333,
            ["system"] = new JsonObject { ["gpu"] = gpu, ["cpu"] = "Test CPU", ["backBuffer"] = "1920x1080" },
            ["segments"] = new JsonArray(new JsonObject { ["scene"] = "Cavern", ["medianMs"] = 3.06 }),
        };

        private string[] Waiting() => Directory.Exists(Outbox) ? Directory.GetFiles(Outbox, "*.json") : Array.Empty<string>();

        /// <summary>The worker's word on <paramref name="id"/>, within a few seconds.</summary>
        private static BenchmarkDelivery Outcome(OnlineBenchmarks client, Guid id)
        {
            Stopwatch clock = Stopwatch.StartNew();
            while (clock.Elapsed < TimeSpan.FromSeconds(20))
            {
                if (client.TryTakeOutcome(out BenchmarkOutcome outcome))
                {
                    Assert.Equal(id, outcome.BenchmarkId);
                    return outcome.Delivery;
                }
                Thread.Sleep(20);
            }
            throw new TimeoutException("no outcome");
        }

        private static void Until(Func<bool> done)
        {
            Stopwatch clock = Stopwatch.StartNew();
            while (!done())
            {
                if (clock.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException();
                Thread.Sleep(20);
            }
        }

        [Fact]
        public void A_result_goes_with_no_token_and_leaves_the_outbox_once_the_service_names_it()
        {
            using OnlineBenchmarks client = Client();
            Guid id = client.Send(Result());

            Assert.Equal(BenchmarkDelivery.Sent, Outcome(client, id));
            Assert.Empty(Waiting());

            StandIn.Request request = Assert.Single(_server.Requests);
            Assert.Equal("POST", request.Method);
            Assert.Equal("/v1/benchmarks", request.Path);
            Assert.Null(request.Header("Authorization"));
            using JsonDocument body = JsonDocument.Parse(request.Body);
            Assert.Equal(id.ToString(), body.RootElement.GetProperty("benchmarkId").GetString());
            Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("gameVersion").GetString()));
            Assert.Equal("Test GPU", body.RootElement.GetProperty("result").GetProperty("system").GetProperty("gpu").GetString());
            //Nothing that names a player: the body is the id, the version and the result
            Assert.Equal(["benchmarkId", "gameVersion", "result"], body.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
        }

        [Theory]
        [InlineData(503, "{\"reason\":\"busy\"}")]
        [InlineData(429, "{\"reason\":\"rate-limited\"}")]
        [InlineData(404, null)]
        [InlineData(200, "{\"benchmarkId\":\"00000000-0000-0000-0000-000000000001\",\"duplicate\":false}")]
        [InlineData(422, "<html>a portal's page</html>")]
        public void Without_the_service_s_answer_the_result_is_kept_and_goes_at_the_next_start(int status, string answer)
        {
            Guid id;
            using (OnlineBenchmarks first = Client())
            {
                _server.BenchmarkAnswers.Enqueue((status, answer));
                id = first.Send(Result());
                Assert.Equal(BenchmarkDelivery.Kept, Outcome(first, id));
                Assert.Single(Waiting());
            }

            //The next start drains it: the stand-in answers as the service does now
            using OnlineBenchmarks next = Client();
            Until(() => Waiting().Length == 0);
            Assert.Equal(2, _server.Requests.Count);
            Assert.All(_server.Requests, r => Assert.Equal(id.ToString(), StandIn.BenchmarkIdOf(r.Body)));
        }

        [Fact]
        public void A_refusal_with_the_service_s_reason_drops_it()
        {
            using OnlineBenchmarks client = Client();
            _server.BenchmarkAnswers.Enqueue((422, "{\"reason\":\"bad-result\"}"));
            Guid id = client.Send(Result());

            Assert.Equal(BenchmarkDelivery.Refused, Outcome(client, id));
            Assert.Empty(Waiting());
        }

        [Fact]
        public void A_build_that_reaches_no_server_keeps_its_results_and_holds_them_to_the_capacity()
        {
            using OnlineBenchmarks client = OnlineBenchmarks.Start(new GameSettings { Server = "http://example.com/" }, Outbox);
            Assert.False(client.CanReachServer);

            for (int i = 0; i < OnlineBenchmarks.OutboxCapacity + 3; i++)
            {
                Guid id = client.Send(Result($"GPU {i}"));
                Assert.Equal(BenchmarkDelivery.NoServer, Outcome(client, id));
            }

            Assert.Equal(OnlineBenchmarks.OutboxCapacity, Waiting().Length);
            Assert.Empty(_server.Requests);
        }
    }
}
