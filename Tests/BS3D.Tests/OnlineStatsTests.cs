using BS3D.Online;
using Prazsky.BS3D.Levels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The online client's side of the play counts (#709), the real <see cref="OnlineScores"/> against a loopback stand-in
    /// that records every request whole: nothing sent or kept without consent, a batch alone (no token, no version, its own
    /// connection, a quiet after a clear), sent again under its id until answered, kept on a bare 404, dropped on a refusal,
    /// and dropped from the outbox when online scores are turned off.
    /// <para>
    /// <b>Alone, not beside the other tests</b> (<see cref="OnlineStatsCollection"/>): the worker, its timer and the stand-in
    /// all wait on the thread pool, and on CI's two cores the physics tests running beside them starved it for longer than a
    /// wait here allowed (a batch due in 300 ms was not frozen within 5 s, 2026-10-15).
    /// </para>
    /// </summary>
    [Collection(OnlineStatsCollection.Name)]
    public sealed class OnlineStatsTests : IDisposable
    {
        //Fast, so a test waits a moment: a batch at least 300 ms after the last, and 250 to 350 ms after a request that names the player
        private static readonly StatsSchedule Fast = new(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(350));

        //The same quiet, but a batch tried again only an hour after the last try: a test that counts requests across a Stop
        //has exactly the one the batch's first try made, because no timer of the worker's is due while it runs
        private static readonly StatsSchedule OneTry = new(TimeSpan.FromHours(1), Fast.QuietMin, Fast.QuietMax);

        private readonly TempDirectory _temp = new();
        private readonly StandIn _server = new();
        private string Outbox => _temp.File(OnlineScores.OutboxFileName);

        public void Dispose()
        {
            _server.Dispose();
            _temp.Dispose();
        }

        private OnlineScores Client(bool? online, StatsSchedule schedule = null) =>
            OnlineScores.Start(new GameSettings { Online = online, Server = _server.Url }, OnlineIdentity.Create("Tester"), Outbox,
                schedule: schedule ?? Fast);

        private static LevelPlayCounts Change(int starts = 1, int startsToFirstClear = 0) => new()
        {
            File = "One.json", Hash = "0123456789abcdef", Rules = 1, Starts = starts, StartsToFirstClear = startsToFirstClear,
            Shots = 4, Seconds = 12.5,
        };

        private static void Stop(OnlineScores client)
        {
            Task worker = client.Stop();
            try { worker.Wait(TimeSpan.FromSeconds(20)); } catch (AggregateException) { }
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

        private OutboxFile ReadOutbox()
        {
            //Read so as to let the worker swap the file in under the reader: File.ReadAllText holds the file against
            //File.Replace, and a write that collided with a poll failed (the worker logs it and goes on, as a player's does
            //with a virus scanner on the file), so the file kept its old counts and a test waiting for the new ones waited
            //for nothing. A read caught mid-swap is no file yet.
            try
            {
                if (!File.Exists(Outbox)) return null;

                using FileStream stream = new(Outbox, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return JsonSerializer.Deserialize<OutboxFile>(stream);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        private List<StandIn.Request> Stats => _server.Requests.Where(r => r.Path == "/v1/stats").ToList();

        [Theory]
        [InlineData(false)]
        [InlineData(null)]
        public void Without_consent_nothing_is_sent_or_queued(bool? online)
        {
            OnlineScores client = Client(online);
            client.SubmitStats(Change());

            Thread.Sleep(1500);
            Stop(client);

            Assert.Empty(_server.Requests);
            Assert.True(ReadOutbox()?.StatsOpen == null && ReadOutbox()?.StatsSending == null, "no counts in the outbox");
        }

        [Fact]
        public void A_batch_goes_alone_without_a_token_or_a_version_on_its_own_connection_a_quiet_after_a_clear()
        {
            OnlineScores client = Client(true);

            //Past the quiet the worker's start sets, so what holds the batch back is the clear's own
            Thread.Sleep(600);
            client.Submit(client.NewSubmission(new LevelIdentity("One.json", "0123456789abcdef"), 300, 3, 4, 12.5f));
            client.SubmitStats(Change(startsToFirstClear: 7));

            Assert.True(Within(() => Stats.Count == 1), "a batch was sent");
            Stop(client);

            StandIn.Request score = Assert.Single(_server.Requests, r => r.Path == "/v1/scores");
            StandIn.Request stats = Stats[0];

            //Never in the step a clear went in, nor on its connection: a quiet of at least QuietMin after it
            Assert.True(stats.At - score.At >= Fast.QuietMin - TimeSpan.FromMilliseconds(100),
                $"the batch went {(stats.At - score.At).TotalMilliseconds:0} ms after the clear");
            Assert.NotEqual(score.Connection, stats.Connection);
            Assert.Equal("close", stats.Header("Connection"), ignoreCase: true);

            //Nothing that names the player or the build
            Assert.Null(stats.Header("Authorization"));
            Assert.Equal("BS3D", stats.Header("User-Agent"));
            Assert.NotNull(score.Header("Authorization"));

            using JsonDocument body = JsonDocument.Parse(stats.Body);
            Assert.Equal(["batchId", "levels"], body.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
            JsonElement row = Assert.Single(body.RootElement.GetProperty("levels").EnumerateArray());
            Assert.False(row.TryGetProperty("startsToFirstClear", out _), "the starts to a first clear stay on the machine");
            Assert.Equal(1, row.GetProperty("starts").GetInt32());

            Assert.True(ReadOutbox().StatsSending == null && ReadOutbox().StatsOpen == null, "a delivered batch leaves the outbox");
        }

        [Fact]
        public void A_batch_with_no_answer_goes_again_under_the_same_id()
        {
            _server.StatsAnswers.Enqueue((503, null));
            OnlineScores client = Client(true);
            client.SubmitStats(Change());

            Assert.True(Within(() => Stats.Count >= 2), "the batch went again");
            Assert.True(Within(() => ReadOutbox()?.StatsSending == null), "and left the outbox once answered");
            Stop(client);

            Assert.Equal(BatchId(Stats[0]), BatchId(Stats[1]));
        }

        [Fact]
        public void A_bare_404_keeps_the_batch_under_its_id()
        {
            for (int i = 0; i < 10; i++) _server.StatsAnswers.Enqueue((404, null));
            OnlineScores client = Client(true);
            client.SubmitStats(Change());

            Assert.True(Within(() => Stats.Count >= 2), "a service without the endpoint is asked again");
            Stop(client);

            Assert.Equal(BatchId(Stats[0]), BatchId(Stats[1]));
            Assert.Equal(BatchId(Stats[0]), ReadOutbox().StatsSending?.BatchId?.ToString());
        }

        [Fact]
        public void A_refusal_drops_the_batch()
        {
            _server.StatsAnswers.Enqueue((422, "{\"reason\":\"bad-counts\"}"));
            OnlineScores client = Client(true);
            client.SubmitStats(Change());

            Assert.True(Within(() => Stats.Count == 1), "the batch was sent");
            Thread.Sleep(1000);
            Stop(client);

            Assert.Single(Stats);
            Assert.True(ReadOutbox().StatsSending == null && ReadOutbox().StatsOpen == null, "a refused batch leaves the outbox");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(null)]
        public void Turning_scores_off_drops_the_queued_counts_and_keeps_the_clears(bool? online)
        {
            //What a client with scores on leaves behind: a clear the service never answered, a frozen batch and an open one
            for (int i = 0; i < 20; i++)
            {
                _server.ScoreAnswers.Enqueue((503, null));
                _server.StatsAnswers.Enqueue((503, null));
            }
            OnlineScores on = Client(true, OneTry);
            on.Submit(on.NewSubmission(new LevelIdentity("One.json", "0123456789abcdef"), 300, 3, 4, 12.5f));
            on.SubmitStats(Change());
            Assert.True(Within(() => Stats.Count == 1 && ReadOutbox()?.StatsSending != null), "a batch frozen, kept and tried once");
            on.SubmitStats(Change());
            Assert.True(Within(() => ReadOutbox() is { StatsOpen: not null, StatsSending: not null } f && f.Submissions.Count == 1), "queued");

            //That worker is over before anything is counted: a request it had in flight when it was stopped (a retry of the
            //frozen batch on a timer) is its own, and would reach the stand-in after the count, whichever way it was read
            Task worker = on.Stop();
            try { worker.Wait(TimeSpan.FromSeconds(20)); } catch (AggregateException) { }
            Assert.True(worker.IsCompleted, "the first client's worker stopped");
            int sent = Stats.Count;
            Assert.Equal(1, sent);

            //The switch turned off: the client that replaces it is started off, after that worker
            OnlineScores off = OnlineScores.Start(new GameSettings { Online = online, Server = _server.Url }, OnlineIdentity.Create("Tester"),
                Outbox, worker, Fast);
            Stop(off);

            OutboxFile file = ReadOutbox();
            Assert.Null(file.StatsOpen);
            Assert.Null(file.StatsSending);
            Assert.Single(file.Submissions);
            Assert.DoesNotContain("stats", File.Exists(Outbox + OnlineScores.OutboxBackupSuffix)
                ? File.ReadAllText(Outbox + OnlineScores.OutboxBackupSuffix) : "");
            Assert.Equal(sent, Stats.Count);
        }

        private static string BatchId(StandIn.Request request)
        {
            using JsonDocument body = JsonDocument.Parse(request.Body);
            return body.RootElement.GetProperty("batchId").GetString();
        }

        /// <summary>
        /// A loopback HTTP/1.1 server that records every request whole, with the connection it came on: <c>/v1/stats</c> answers
        /// from <see cref="StatsAnswers"/> and then 201, <c>/v1/scores</c> from <see cref="ScoreAnswers"/> and then 201 with a
        /// rank body. A null body is a bare answer, no content and no type.
        /// </summary>
        private sealed class StandIn : IDisposable
        {
            internal sealed record Request(int Connection, string Method, string Path, Dictionary<string, string> Headers, string Body, TimeSpan At)
            {
                internal string Header(string name) => Headers.TryGetValue(name.ToLowerInvariant(), out string value) ? value : null;
            }

            private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
            private readonly CancellationTokenSource _stop = new();
            private readonly Stopwatch _clock = Stopwatch.StartNew();
            private readonly ConcurrentQueue<Request> _requests = new();
            private int _connections;

            internal ConcurrentQueue<(int Status, string Body)> StatsAnswers { get; } = new();
            internal ConcurrentQueue<(int Status, string Body)> ScoreAnswers { get; } = new();
            internal string Url { get; }
            internal List<Request> Requests => _requests.ToList();

            internal StandIn()
            {
                _listener.Start();
                Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/";
                _ = Task.Run(AcceptAsync);
            }

            private async Task AcceptAsync()
            {
                while (!_stop.IsCancellationRequested)
                {
                    TcpClient client;
                    try { client = await _listener.AcceptTcpClientAsync(_stop.Token); }
                    catch (Exception) { return; }

                    int connection = Interlocked.Increment(ref _connections);
                    _ = Task.Run(() => Serve(client, connection));
                }
            }

            private void Serve(TcpClient client, int connection)
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    try
                    {
                        while (!_stop.IsCancellationRequested)
                        {
                            string head = ReadHead(stream);
                            if (head == null) return;

                            string[] lines = head.Split("\r\n");
                            string[] start = lines[0].Split(' ');
                            Dictionary<string, string> headers = new();
                            foreach (string line in lines.Skip(1))
                            {
                                int colon = line.IndexOf(':');
                                if (colon > 0) headers[line[..colon].Trim().ToLowerInvariant()] = line[(colon + 1)..].Trim();
                            }

                            int length = headers.TryGetValue("content-length", out string l) ? int.Parse(l) : 0;
                            byte[] body = new byte[length];
                            for (int read = 0; read < length;)
                            {
                                int n = stream.Read(body, read, length - read);
                                if (n == 0) return;
                                read += n;
                            }

                            Request request = new(connection, start[0], start[1], headers, Encoding.UTF8.GetString(body), _clock.Elapsed);
                            _requests.Enqueue(request);

                            (int status, string answer) = request.Path switch
                            {
                                "/v1/stats" => StatsAnswers.TryDequeue(out var s) ? s : (201, "{\"accepted\":true,\"counted\":1,\"ignored\":0,\"duplicate\":false}"),
                                "/v1/scores" => ScoreAnswers.TryDequeue(out var c) ? c
                                    : (201, "{\"accepted\":true,\"personalBest\":true,\"month\":{\"rank\":1,\"total\":1},\"allTime\":{\"rank\":1,\"total\":1}}"),
                                _ => (404, null),
                            };

                            byte[] payload = Encoding.UTF8.GetBytes(answer ?? "");
                            string response = $"HTTP/1.1 {status} X\r\nContent-Length: {payload.Length}\r\n"
                                + (answer != null ? "Content-Type: application/json\r\n" : "") + "\r\n";
                            stream.Write(Encoding.ASCII.GetBytes(response));
                            stream.Write(payload);

                            if (request.Header("Connection")?.Equals("close", StringComparison.OrdinalIgnoreCase) == true) return;
                        }
                    }
                    catch (IOException)
                    {
                    }
                }
            }

            private static string ReadHead(NetworkStream stream)
            {
                StringBuilder head = new();
                int b;
                while ((b = stream.ReadByte()) >= 0)
                {
                    head.Append((char)b);
                    if (head.Length >= 4 && head[^1] == '\n' && head[^2] == '\r' && head[^3] == '\n' && head[^4] == '\r')
                        return head.ToString(0, head.Length - 4);
                }
                return null;
            }

            public void Dispose()
            {
                _stop.Cancel();
                _listener.Stop();
            }
        }
    }

    /// <summary>The online client's tests run after every parallel test, one at a time: see <see cref="OnlineStatsTests"/>.</summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class OnlineStatsCollection
    {
        public const string Name = "Online client, alone";
    }
}
