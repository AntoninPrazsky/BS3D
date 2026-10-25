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

namespace BS3D.Tests
{
    /// <summary>
    /// A loopback HTTP/1.1 server that records every request whole, with the connection it came on: <c>/v1/stats</c> answers
    /// from <see cref="StatsAnswers"/> and then 201, <c>/v1/scores</c> from <see cref="ScoreAnswers"/> and then 201 with a
    /// rank body, <c>/v1/benchmarks</c> (#988) from <see cref="BenchmarkAnswers"/> and then 201 naming the id the request
    /// carried. A null body is a bare answer, no content and no type. Shared by <c>OnlineStatsTests</c> and
    /// <c>OnlineBenchmarksTests</c>.
    /// </summary>
    internal sealed class StandIn : IDisposable
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
        internal ConcurrentQueue<(int Status, string Body)> BenchmarkAnswers { get; } = new();

        //How long a clear's answer is held back: a far or a busy service, which a slow CI runner is too
        internal TimeSpan ScoreDelay { get; set; }
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
                            "/v1/benchmarks" => BenchmarkAnswers.TryDequeue(out var b) ? b
                                : (201, $"{{\"benchmarkId\":\"{BenchmarkIdOf(request.Body)}\",\"duplicate\":false}}"),
                            _ => (404, null),
                        };

                        if (request.Path == "/v1/scores" && ScoreDelay > TimeSpan.Zero) Thread.Sleep(ScoreDelay);

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

        /// <summary>The id a benchmark's request carried, which the service's answer names.</summary>
        internal static string BenchmarkIdOf(string body)
        {
            using JsonDocument json = JsonDocument.Parse(body);
            return json.RootElement.GetProperty("benchmarkId").GetString();
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
