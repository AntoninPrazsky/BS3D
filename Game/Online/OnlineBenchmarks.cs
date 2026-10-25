using Prazsky.Core.Tools;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace BS3D.Online
{
    /// <summary>How a benchmark's result fared, as the benchmark page tells the player.</summary>
    internal enum BenchmarkDelivery
    {
        /// <summary>The service has it.</summary>
        Sent,

        /// <summary>The service did not answer: the result stays in the outbox and goes at the next start or the next result.</summary>
        Kept,

        /// <summary>The service refused it for good (its reason is logged); it is gone from the outbox.</summary>
        Refused,

        /// <summary>This build reaches no server (a local build with none named): the result stays in the outbox.</summary>
        NoServer,
    }

    /// <summary>A result's fate, handed back to the frame.</summary>
    internal readonly record struct BenchmarkOutcome(Guid BenchmarkId, BenchmarkDelivery Delivery);

    /// <summary>
    /// The game's side of the benchmark's result (#988, the service's side BS3D-API's <c>POST /v1/benchmarks</c>): a
    /// finished run of Extras &gt; Benchmark, the machine it ran on and its figures, sent to the author. The owner
    /// (2026-10-25): "as soon as the benchmark finishes, the result, including the specification of the player's hardware
    /// it ran on, must be sent to the server".
    /// <para>
    /// <b>The Start press is the consent</b>, as a note's Send is: the benchmark page says before it starts what goes and
    /// that nothing names the player, so this does not wait for the online scores to be switched on. <b>Nothing in it names
    /// the player</b>: no player id, no token, no nickname, and no <c>Authorization</c> header; the result is the object the
    /// game saves as <c>Benchmark.json</c> (<see cref="BS3DGame.BenchmarkSystem"/> holds no paths and no user name).
    /// </para>
    /// <para>
    /// <b>It follows <see cref="OnlineNotes"/>' rules</b>: every result is written to the outbox, <c>Benchmarks/</c> in the
    /// user data folder, before it is sent, and leaves only when the service has answered for it; the server is the one
    /// <see cref="OnlineScores.TryResolveServer"/> resolves, so a developer's runs never post to the owner's service; an
    /// answer is the service's only when it carries its JSON and names the result; a 4xx with the service's reason drops it
    /// and logs why, anything else keeps it. There is no retry timer: the outbox is drained at start and after each new
    /// result. Nothing of this runs on the frame.
    /// </para>
    /// </summary>
    internal sealed class OnlineBenchmarks : IDisposable
    {
        internal const string FolderName = "Benchmarks";

        /// <summary>Results kept waiting at most; past it the oldest go, so a game that never reaches a server does not fill a disk.</summary>
        internal const int OutboxCapacity = 10;

        private const string Format = "bs3d-benchmark";

        /// <summary>A note's patience (<see cref="OnlineNotes.RequestTimeout"/>): a result is a few kilobytes, which a slow uplink sends well within it.</summary>
        internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

        private readonly string _folder;
        private readonly Uri _server;
        private readonly HttpClient _http;
        private readonly ConcurrentQueue<(Guid Id, JsonObject Result)> _incoming = new();
        private readonly ConcurrentQueue<BenchmarkOutcome> _outcomes = new();
        private readonly SemaphoreSlim _wake = new(0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _worker;

        private OnlineBenchmarks(string folder, Uri server)
        {
            _folder = folder;
            _server = server;

            if (server != null)
            {
                _http = new HttpClient(new SocketsHttpHandler { ConnectTimeout = OnlineScores.RequestTimeout })
                {
                    Timeout = Timeout.InfiniteTimeSpan,
                    MaxResponseContentBufferSize = OnlineScores.MaxResponseBytes,
                };
                _http.DefaultRequestHeaders.UserAgent.ParseAdd($"BS3D/{BuildVersion.Name}");
                _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            }

            _worker = Task.Run(RunAsync);
        }

        /// <summary>Whether a result can leave this machine at all: false for a local build with no server named.</summary>
        internal bool CanReachServer => _server != null;

        /// <summary>
        /// Starts the worker, which drains whatever an earlier run left in the outbox. No I/O here. Says in one
        /// <c>[bench]</c> line where results go.
        /// </summary>
        internal static OnlineBenchmarks Start(GameSettings settings, string folder)
        {
            bool resolved = OnlineScores.TryResolveServer(settings, out Uri server, out string problem, out _);
            Console.WriteLine(resolved ? $"[bench] Results go to {server}" : $"[bench] Results stay in {FolderName}/: {problem}");
            return new OnlineBenchmarks(folder, resolved ? server : null);
        }

        /// <summary>Hands a finished result to the worker, which writes it to the outbox and sends it. Returns its id.</summary>
        internal Guid Send(JsonObject result)
        {
            Guid id = Guid.NewGuid();
            _incoming.Enqueue((id, (JsonObject)result.DeepClone()));
            _wake.Release();
            return id;
        }

        /// <summary>A result's fate, once the worker knows it. No allocation when there is nothing to take.</summary>
        internal bool TryTakeOutcome(out BenchmarkOutcome outcome) => _outcomes.TryDequeue(out outcome);

        public void Dispose()
        {
            _stop.Cancel();
            _wake.Release();
            try { _worker.Wait(OnlineScores.DisposeWait); } catch (AggregateException) { }
            _http?.Dispose();
        }

        private async Task RunAsync()
        {
            CancellationToken stop = _stop.Token;
            try
            {
                //At start: whatever an earlier run left
                await DrainAsync(null, stop);

                while (!stop.IsCancellationRequested)
                {
                    await _wake.WaitAsync(stop);
                    while (_incoming.TryDequeue(out (Guid Id, JsonObject Result) result))
                    {
                        Write(result.Id, result.Result);
                        await DrainAsync(result.Id, stop);
                    }
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            catch (Exception e)
            {
                Console.WriteLine($"[bench] The result worker stopped: {e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>The result on disk, before a byte of it goes anywhere, and the outbox cut to its capacity.</summary>
        private void Write(Guid id, JsonObject result)
        {
            try
            {
                Directory.CreateDirectory(_folder);
                JsonObject file = new()
                {
                    ["format"] = Format,
                    ["version"] = 1,
                    ["benchmarkId"] = id,
                    ["gameVersion"] = BuildVersion.Name,
                    ["result"] = result,
                };
                AtomicFile.WriteText(PathOf(id), file.ToJsonString(), null);

                FileInfo[] waiting = Waiting();
                for (int i = 0; i < waiting.Length - OutboxCapacity; i++) OnlineScores.DeleteQuietly(waiting[i].FullName);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"[bench] Could not write the result: {e.Message}");
            }
        }

        /// <summary>
        /// Sends the outbox oldest first and stops at the first result that gets no answer. <paramref name="current"/> is
        /// the result just written, whose fate the page is waiting for; it is told even when nothing could be tried.
        /// </summary>
        private async Task DrainAsync(Guid? current, CancellationToken stop)
        {
            if (_server == null)
            {
                if (current is Guid id) _outcomes.Enqueue(new BenchmarkOutcome(id, BenchmarkDelivery.NoServer));
                return;
            }

            bool told = false;
            foreach (FileInfo file in Waiting())
            {
                if (stop.IsCancellationRequested) return;

                (Guid id, BenchmarkDelivery delivery) = await SendAsync(file, stop);
                if (current == id)
                {
                    _outcomes.Enqueue(new BenchmarkOutcome(id, delivery));
                    told = true;
                }
                if (delivery == BenchmarkDelivery.Kept) break;
            }

            if (!told && current is Guid kept) _outcomes.Enqueue(new BenchmarkOutcome(kept, BenchmarkDelivery.Kept));
        }

        private async Task<(Guid, BenchmarkDelivery)> SendAsync(FileInfo file, CancellationToken stop)
        {
            JsonObject stored;
            try
            {
                stored = JsonNode.Parse(await File.ReadAllTextAsync(file.FullName, stop)) as JsonObject;
            }
            catch (Exception e) when (e is IOException or JsonException)
            {
                Console.WriteLine($"[bench] Dropping an unreadable result {file.Name}: {e.Message}");
                OnlineScores.DeleteQuietly(file.FullName);
                return (Guid.Empty, BenchmarkDelivery.Refused);
            }

            if (stored?["format"]?.GetValue<string>() != Format || !Guid.TryParse(stored["benchmarkId"]?.GetValue<string>(), out Guid id)
                || stored["result"] is not JsonObject result)
            {
                Console.WriteLine($"[bench] Dropping {file.Name}: not a benchmark result");
                OnlineScores.DeleteQuietly(file.FullName);
                return (Guid.Empty, BenchmarkDelivery.Refused);
            }

            JsonObject body = new()
            {
                ["benchmarkId"] = id,
                ["gameVersion"] = stored["gameVersion"]?.GetValue<string>(),
                ["result"] = result.DeepClone(),
            };
            using HttpRequestMessage request = new(HttpMethod.Post, new Uri(_server, "v1/benchmarks"))
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
            };

            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(stop);
            deadline.CancelAfter(RequestTimeout);
            string tag = id.ToString()[..8];
            try
            {
                using HttpResponseMessage response = await _http.SendAsync(request, deadline.Token);
                int status = (int)response.StatusCode;
                string text = await response.Content.ReadAsStringAsync(deadline.Token);
                JsonObject answer = TryParse(response, text);

                if (status >= 200 && status < 300)
                {
                    //A 2xx is the service's only when it names this result: a captive portal answers everything with its page
                    if (answer?["benchmarkId"]?.GetValue<string>() is string named && Guid.TryParse(named, out Guid answered) && answered == id)
                    {
                        OnlineScores.DeleteQuietly(file.FullName);
                        Console.WriteLine($"[bench] Sent {tag} ({status}){(answer["duplicate"]?.GetValue<bool>() == true ? ", the service had it already" : "")}");
                        return (id, BenchmarkDelivery.Sent);
                    }
                    Console.WriteLine($"[bench] Kept {tag}: {status} without the service's answer (a captive portal?)");
                    return (id, BenchmarkDelivery.Kept);
                }

                string reason = answer?["reason"]?.GetValue<string>();
                if (status == (int)HttpStatusCode.RequestTimeout || status == (int)HttpStatusCode.TooManyRequests || status >= 500
                    || string.IsNullOrEmpty(reason))
                {
                    Console.WriteLine($"[bench] Kept {tag}: {status} {(reason ?? response.ReasonPhrase)}");
                    return (id, BenchmarkDelivery.Kept);
                }

                Console.WriteLine($"[bench] The service refused {tag} for good: {status} {OnlineScores.CleanText(reason, 60)}");
                OnlineScores.DeleteQuietly(file.FullName);
                return (id, BenchmarkDelivery.Refused);
            }
            catch (OperationCanceledException) when (!stop.IsCancellationRequested)
            {
                Console.WriteLine($"[bench] Kept {tag}: nothing within {RequestTimeout.TotalSeconds:0} s");
            }
            catch (Exception e) when (!stop.IsCancellationRequested && e is not OperationCanceledException)
            {
                Console.WriteLine($"[bench] Kept {tag}: {e.GetType().Name}: {e.Message}");
            }
            return (id, BenchmarkDelivery.Kept);
        }

        private static JsonObject TryParse(HttpResponseMessage response, string text)
        {
            if (response.Content.Headers.ContentType?.MediaType != "application/json") return null;
            try { return JsonNode.Parse(text) as JsonObject; }
            catch (JsonException) { return null; }
        }

        /// <summary>The results waiting, oldest first.</summary>
        private FileInfo[] Waiting()
        {
            try
            {
                DirectoryInfo folder = new(_folder);
                return folder.Exists
                    ? folder.GetFiles("*.json").OrderBy(f => f.LastWriteTimeUtc).ThenBy(f => f.Name, StringComparer.Ordinal).ToArray()
                    : Array.Empty<FileInfo>();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return Array.Empty<FileInfo>();
            }
        }

        private string PathOf(Guid id) => Path.Combine(_folder, id.ToString("N") + ".json");
    }
}
