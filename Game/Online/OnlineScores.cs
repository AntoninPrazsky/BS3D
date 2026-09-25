using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Scoring;
using Prazsky.Core.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace BS3D.Online
{
    /// <summary>
    /// The game's side of the online score boards (#546, the decisions and the contract in #542): every cleared
    /// level goes to the score service, <b>off the frame</b>, and <b>survives the service being away</b> — and
    /// since #548 the player's two requests of it besides, a new nickname and the removal of everything they sent.
    /// <para>
    /// <b>Every submission is written to <c>Outbox.json</c> before it is sent</b>, and leaves it only when the
    /// service has answered for it. So there is one path, not two: a clear is appended and the outbox is
    /// drained from its head, in order, stopping at the first submission that gets no answer. A game that
    /// crashes or is closed mid-send has lost nothing — the entry is on disk and goes again at the next start,
    /// and the service's idempotence on the submission id makes that resend harmless. The drain runs at start
    /// and after each clear, which is what #546 asks for and all it asks for: there is no retry timer, so a Pi
    /// that is off costs one failed request per clear, not a request a second.
    /// </para>
    /// <para>
    /// <b>What an answer means is decided by its status, and the one judgment in it is which refusals are
    /// final.</b> A 2xx carrying the service's JSON is delivered. A 408, a 429, a 5xx, a timeout and no network
    /// at all are not the submission's fault, so it stays. Any other 4xx is the service saying no for good — an
    /// unknown level, a score over the ceiling, a wrong token — and resending it for ever would only block every
    /// clear queued behind it, so it is dropped and <b>logged</b>: a 422 here means the game and the service
    /// disagree about a level, which is a thing somebody has to go and look at. A 2xx that is not the service's
    /// JSON (a captive portal answering everything with its login page) is treated as no answer rather than as
    /// delivery, because a hotel's Wi-Fi must not eat a clear.
    /// </para>
    /// <para>
    /// <b>Nothing of this runs on the frame.</b> One worker task owns the outbox, the file and the
    /// <see cref="HttpClient"/>; the frame hands it a submission through a queue and takes answers back through
    /// another, drained once a frame by <see cref="TryTakeAnswer"/> and <see cref="TryTakeNotice"/> — no
    /// <c>Wait()</c>, no <c>Result</c>, and no allocation when there is nothing to take, which is every frame but a
    /// handful. Even the outbox's file is written by the worker.
    /// </para>
    /// <para>
    /// <b>Two answers are decided once, at start</b> (<see cref="Start"/>), and they are not the same question.
    /// <see cref="CanReachServer"/>: this install holds an identity and a server resolves — the one
    /// <c>Settings.json</c> names, or, for a build that came out of a release and only for one, the built-in
    /// <see cref="DefaultServer"/>. <see cref="Enabled"/>: that, and the player has turned online scores on. Only an
    /// enabled client submits or drains the outbox; a client that merely reaches the server still carries a
    /// rename and a removal, because a player who switched the boards off must still be able to take their name
    /// off them (#548). A local build with no server named reaches nothing, so a developer's runs never land on
    /// the public boards by accident. Whatever it decides, it says so in one <c>[online]</c> line.
    /// </para>
    /// <para>
    /// <b>A client is replaced, never reconfigured</b>: a settings change (#548) stops this one and starts another
    /// with <see cref="Start"/>, handing it this one's worker to wait for, so two workers are never on the outbox
    /// at once.
    /// </para>
    /// </summary>
    internal sealed class OnlineScores : IDisposable
    {
        /// <summary>
        /// The score service a <b>release</b> build submits to when <c>Settings.json</c> names none. <b>Null until
        /// the service has a public hostname</b> — it needs a domain on Cloudflare's DNS for its tunnel (#544),
        /// which does not exist yet — so today a release build submits nowhere unless its settings name a
        /// server. Set it to the hostname, HTTPS, the day the tunnel answers.
        /// </summary>
        internal const string DefaultServer = null;

        internal const string OutboxFileName = "Outbox.json";
        internal const string OutboxBackupSuffix = ".bak";

        /// <summary>
        /// Submissions the outbox holds at most; past it the oldest go. Two hundred clears is weeks of play with
        /// the service away, and a file that could grow without bound is a file that eventually does.
        /// </summary>
        internal const int OutboxCapacity = 200;

        /// <summary>How long one request may take, connecting included, before it counts as no answer.</summary>
        internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The assembly metadata key <c>release.yml</c> stamps the tag under (<c>-p:BS3DReleaseVersion=</c>,
        /// turned into an attribute by <c>Game.csproj</c>) — on a tag build and only then.
        /// </summary>
        private const string ReleaseMetadataKey = "BS3DReleaseVersion";

        private static readonly JsonSerializerOptions RequestJson = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private static readonly JsonSerializerOptions OutboxJson = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Whether clears go anywhere this run: the player turned it on and <see cref="CanReachServer"/>. Decided
        /// once, in <see cref="Start"/>.
        /// </summary>
        internal bool Enabled { get; }

        /// <summary>
        /// Whether a request can be sent at all — an identity and a server — which is what a rename and a removal
        /// need, whether or not the player has the boards switched on (#548).
        /// </summary>
        internal bool CanReachServer { get; }

        /// <summary>The server this client talks to, or null when none resolves.</summary>
        internal Uri Server { get; }

        /// <summary>
        /// The release this build came out of (the tag, <c>v0.2.0</c>), or null for any other build. What
        /// decides whether the built-in server may be used.
        /// </summary>
        internal static string ReleaseVersion { get; } = ReadReleaseVersion();

        /// <summary>
        /// What a submission says the game is: <see cref="ReleaseVersion"/>, or <c>dev-&lt;short sha&gt;</c> —
        /// the name <c>release.yml</c> gives a rehearsal run — so a server log can still tell which build sent it.
        /// </summary>
        internal static string GameVersion { get; } = ReleaseVersion ?? DevVersion();

        private readonly OnlineIdentity _identity;
        private readonly string _outboxPath;
        private readonly HttpClient _http;
        private readonly Task _previous;
        private readonly Task _worker;

        private readonly ConcurrentQueue<ScoreSubmission> _incoming = new();
        private readonly ConcurrentQueue<OnlineAnswer> _answers = new();
        private readonly ConcurrentQueue<OnlineNotice> _notices = new();
        private readonly SemaphoreSlim _wake = new(0);
        private readonly CancellationTokenSource _stop = new();

        private volatile bool _removalRequested;
        private string _pendingRename;

        private OnlineScores(bool on, Uri server, OnlineIdentity identity, string outboxPath, Task previous)
        {
            Server = server;
            _identity = identity;
            _outboxPath = outboxPath;
            _previous = previous ?? Task.CompletedTask;

            CanReachServer = server != null && identity != null;
            Enabled = on && CanReachServer;

            //No worker, but the chain still holds: whoever replaces this one waits for the worker before it
            if (!CanReachServer)
            {
                _worker = _previous;
                return;
            }

            _http = new HttpClient(new SocketsHttpHandler
            {
                ConnectTimeout = RequestTimeout,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            })
            {
                //Each request carries its own deadline (SendAsync), so the client's own never fires
                Timeout = Timeout.InfiniteTimeSpan,
            };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd($"BS3D/{GameVersion}");
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");

            //Its own task for the whole run: it waits on the semaphore between requests, so it costs nothing idle
            _worker = Task.Run(RunAsync);
        }

        /// <summary>
        /// Decides whether this run submits, and whether it can reach a server at all, and says so. No I/O: the
        /// identity is the caller's, read once at start (and changed only by the settings page, #548).
        /// </summary>
        /// <param name="previous">The worker of the client this one replaces, which is waited for before this
        /// one touches the outbox — see <see cref="Stop"/>.</param>
        internal static OnlineScores Start(GameSettings settings, OnlineIdentity identity, string outboxPath, Task previous = null)
        {
            bool usable = identity != null && identity.IsUsable;
            bool resolved = TryResolveServer(settings, out Uri server, out string serverProblem, out bool named);

            if (!settings.Online)
                Console.WriteLine("[online] Off: online scores are not turned on in the settings"
                    + (usable && resolved ? $" (a rename or a removal still reaches {server})" : ""));
            else if (!usable)
                Console.WriteLine("[online] Off: turned on, but Online.json holds no usable identity (id, token and nickname)");
            else if (!resolved)
                Console.WriteLine($"[online] Off: {serverProblem}");
            else
                Console.WriteLine($"[online] On: submitting to {server} as '{identity.Name}' (player {identity.PlayerId.ToString()[..8]}),"
                    + $" game {GameVersion}, rules v{ScoreKeeper.RulesVersion}"
                    + (named ? ", server named by the settings" : ", the built-in server"));

            return new OnlineScores(settings.Online, resolved ? server : null, usable ? identity : null, outboxPath, previous);
        }

        /// <summary>
        /// A cleared level, ready to hand to <see cref="Submit"/>: stamped with this install's identity, a fresh
        /// submission id and this build's version. Null when this run submits nowhere.
        /// </summary>
        internal ScoreSubmission NewSubmission(LevelIdentity level, int score, int stars, int shotsUsed, float seconds)
        {
            if (!Enabled || level == null) return null;

            return new ScoreSubmission
            {
                SubmissionId = Guid.NewGuid(),
                PlayerId = _identity.PlayerId,
                Name = _identity.Name,
                Level = new SubmittedLevel { File = level.File, Hash = level.Hash },
                RulesVersion = ScoreKeeper.RulesVersion,
                Score = score,
                Stars = stars,
                ShotsUsed = shotsUsed,
                DurationSeconds = MathF.Round(seconds, 2),
                GameVersion = GameVersion,
            };
        }

        /// <summary>
        /// Hands a clear to the worker, which writes it to the outbox and drains. Returns at once: a queue and a
        /// semaphore, and nothing on this thread touches the disk or the network.
        /// </summary>
        internal void Submit(ScoreSubmission submission)
        {
            if (!Enabled || submission == null) return;

            _incoming.Enqueue(submission);
            _wake.Release();
        }

        /// <summary>
        /// Asks the service to forget this player (#548) — <c>DELETE /v1/players/{id}</c>, and on success the outbox
        /// with it, so nothing queued can put the name back. The answer comes back as a notice: removed, or failed
        /// with the reason, and then nothing was removed anywhere.
        /// </summary>
        internal void RequestRemoval()
        {
            if (!CanReachServer) return;

            _removalRequested = true;
            _wake.Release();
        }

        /// <summary>
        /// Tells the service the player's new nickname (#548) — <c>PUT /v1/players/{id}</c>. Best effort: a rename
        /// that gets no answer is not queued, because every submission carries the name as it stands anyway.
        /// </summary>
        internal void RequestRename(string name)
        {
            if (!CanReachServer || string.IsNullOrEmpty(name)) return;

            Interlocked.Exchange(ref _pendingRename, name);
            _wake.Release();
        }

        /// <summary>The next answer the worker has for the frame, if any. Allocates nothing when there is none.</summary>
        internal bool TryTakeAnswer(out OnlineAnswer answer) => _answers.TryDequeue(out answer);

        /// <summary>The next notice about the player's own requests (#548), if any. Allocates nothing when there is none.</summary>
        internal bool TryTakeNotice(out OnlineNotice notice) => _notices.TryDequeue(out notice);

        /// <summary>
        /// Stops the worker without waiting for it, and returns it — to be handed to the client that replaces this
        /// one, which waits for it before it reads the outbox. Whatever was in flight is still in the outbox and
        /// goes again.
        /// </summary>
        internal Task Stop()
        {
            _stop.Cancel();
            return _worker;
        }

        public void Dispose() => Stop();

        /// <summary>
        /// The one sentence that says what online scores send and what the service keeps (#548) — on the settings
        /// page and on the About page, from here, so the two cannot drift apart. <b>It must stay exactly true of
        /// <see cref="ScoreSubmission"/> and of the service's audit row (#544)</b>: change one, change this in the
        /// same commit, the way the documents are held to the code.
        /// </summary>
        internal static string PrivacySentence(GameSettings settings)
        {
            string host = TryResolveServer(settings, out Uri server, out _, out _) ? server.Authority : "the score server";

            return $"With online scores on, each cleared level sends your nickname, a random player id, the level, its score, stars, shots, time and the "
                + $"game's version to {host}, which adds only when it arrived and a hashed network address. Remove scores deletes it all.";
        }

        #region The worker

        private async Task RunAsync()
        {
            CancellationToken stop = _stop.Token;

            //The client this one replaced must be off the outbox before this one reads it
            try
            {
                await _previous;
            }
            catch (Exception)
            {
                //Its own failure was its own to log
            }

            try
            {
                List<ScoreSubmission> outbox = Enabled ? LoadOutbox() : new List<ScoreSubmission>();

                if (outbox.Count > 0)
                    Console.WriteLine($"[online] {outbox.Count} clear(s) waiting in the outbox from an earlier run; sending");

                if (Enabled) await DrainAsync(outbox, stop);

                while (!stop.IsCancellationRequested)
                {
                    await _wake.WaitAsync(stop);

                    //Before anything queued is sent: a removal must not be overtaken by a clear that puts the name back
                    if (_removalRequested)
                    {
                        if (await RemoveAsync(outbox, stop)) return;

                        _removalRequested = false;
                        continue;
                    }

                    string rename = Interlocked.Exchange(ref _pendingRename, null);
                    if (rename != null) await RenameAsync(rename, stop);

                    if (!Enabled) continue;

                    bool added = false;
                    while (_incoming.TryDequeue(out ScoreSubmission submission))
                    {
                        outbox.Add(submission);
                        added = true;
                    }

                    if (outbox.Count > OutboxCapacity)
                    {
                        int dropped = outbox.Count - OutboxCapacity;
                        outbox.RemoveRange(0, dropped);
                        Console.WriteLine($"[online] The outbox is full ({OutboxCapacity}); dropped the {dropped} oldest clear(s)");
                    }

                    //On disk BEFORE the first byte goes out — see the class doc
                    if (added) SaveOutbox(outbox);

                    await DrainAsync(outbox, stop);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                //Replaced or closing; the outbox is on disk as it stands
            }
            catch (Exception e)
            {
                //The one thing this must never do is take the game down with it. What was queued is on disk.
                Console.WriteLine($"[online] The submitter stopped: {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                _http?.Dispose();
            }
        }

        /// <summary>
        /// Sends from the head of the outbox until it is empty or a submission gets no answer. A delivered or a
        /// finally refused one leaves the file there and then; the first unanswered one stops the drain with it
        /// and everything behind it still queued, in order, and every one of those is reported offline.
        /// </summary>
        private async Task DrainAsync(List<ScoreSubmission> outbox, CancellationToken stop)
        {
            while (outbox.Count > 0)
            {
                ScoreSubmission submission = outbox[0];

                //Queued under an identity this install no longer holds: its token is gone with it, so the service
                //would refuse it anyway. "Remove scores" wipes the outbox with the identity (#548); this is the
                //hand-edited or half-removed case.
                if (submission.PlayerId != _identity.PlayerId)
                {
                    Console.WriteLine($"[online] {Describe(submission)}: queued for another player id; dropped");
                    outbox.RemoveAt(0);
                    SaveOutbox(outbox);
                    continue;
                }

                Delivery delivery = await SendAsync(HttpMethod.Post, "v1/scores",
                    JsonSerializer.Serialize(submission, RequestJson), stop);

                switch (delivery.Kind)
                {
                    case DeliveryKind.Delivered:
                        outbox.RemoveAt(0);
                        SaveOutbox(outbox);
                        _answers.Enqueue(new OnlineAnswer(submission.SubmissionId, OnlineOutcome.Accepted, delivery.Body));
                        NoticeNormalizedName(delivery.Body);
                        Console.WriteLine($"[online] {Describe(submission)}: {delivery.Status} {RankText(delivery.Body)}");
                        break;

                    case DeliveryKind.Refused:
                        outbox.RemoveAt(0);
                        SaveOutbox(outbox);
                        _answers.Enqueue(new OnlineAnswer(submission.SubmissionId, OnlineOutcome.Refused, delivery.Body));
                        NoticeRefusedName(delivery.Body);
                        Console.WriteLine($"[online] {Describe(submission)}: REFUSED {delivery.Status}"
                            + (string.IsNullOrEmpty(delivery.Body?.Reason) ? "" : $" ({delivery.Body.Reason})")
                            + " — dropped; the game and the service disagree about this clear");
                        break;

                    default:
                        Console.WriteLine($"[online] {Describe(submission)}: no answer ({delivery.Problem}) — kept,"
                            + $" {outbox.Count} clear(s) waiting for the next start or the next clear");

                        foreach (ScoreSubmission waiting in outbox)
                            _answers.Enqueue(new OnlineAnswer(waiting.SubmissionId, OnlineOutcome.Offline));
                        return;
                }
            }
        }

        /// <summary>
        /// The player's "Remove scores" (#548). A 2xx is removed; so is a 404, which is a player the service has
        /// never heard of — nothing of theirs is there to remove. Only then does the outbox go, file and all; on
        /// anything else nothing is touched, so the player can try again and still holds the token that proves the
        /// scores are theirs.
        /// </summary>
        /// <returns>Whether the removal happened, which ends this worker: the identity it sends under is gone.</returns>
        private async Task<bool> RemoveAsync(List<ScoreSubmission> outbox, CancellationToken stop)
        {
            Delivery delivery = await SendAsync(HttpMethod.Delete, $"v1/players/{_identity.PlayerId}", null, stop,
                acceptWithoutBody: true);

            bool removed = delivery.Kind == DeliveryKind.Delivered || delivery.Status == (int)HttpStatusCode.NotFound;

            if (!removed)
            {
                string problem = delivery.Kind == DeliveryKind.Refused ? $"the server refused ({delivery.Status})" : delivery.Problem;
                Console.WriteLine($"[online] Removal of player {_identity.PlayerId.ToString()[..8]}: NOT done ({problem}); nothing was removed");
                _notices.Enqueue(new OnlineNotice(OnlineNoticeKind.RemoveFailed, problem));
                return false;
            }

            outbox.Clear();
            DeleteQuietly(_outboxPath);
            DeleteQuietly(_outboxPath + OutboxBackupSuffix);

            Console.WriteLine($"[online] Removal of player {_identity.PlayerId.ToString()[..8]}: {delivery.Status}, removed from {Server.Authority};"
                + " the outbox is gone with it");
            _notices.Enqueue(new OnlineNotice(OnlineNoticeKind.Removed, Server.Authority));
            return true;
        }

        /// <summary>
        /// The player's new nickname (#548). The service's own form of it comes back and is written back; a 422 is
        /// the service refusing the name, which the settings page shows rather than swallows. Anything else is
        /// logged and left: the next clear carries the name regardless.
        /// </summary>
        private async Task RenameAsync(string name, CancellationToken stop)
        {
            Delivery delivery = await SendAsync(HttpMethod.Put, $"v1/players/{_identity.PlayerId}",
                JsonSerializer.Serialize(new PlayerNameBody { Name = name }, RequestJson), stop, acceptWithoutBody: true);

            switch (delivery.Kind)
            {
                case DeliveryKind.Delivered:
                    Console.WriteLine($"[online] Renamed to '{delivery.Body?.Name ?? name}': {delivery.Status}");
                    NoticeNormalizedName(delivery.Body);
                    break;

                case DeliveryKind.Refused when delivery.Status == (int)HttpStatusCode.NotFound:
                    Console.WriteLine($"[online] Rename to '{name}': not on the server yet; the name goes with the next clear");
                    break;

                case DeliveryKind.Refused:
                    Console.WriteLine($"[online] Rename to '{name}': REFUSED {delivery.Status} ({delivery.Body?.Reason ?? "no reason given"})");
                    _notices.Enqueue(new OnlineNotice(OnlineNoticeKind.NameRefused, delivery.Body?.Reason ?? $"refused ({delivery.Status})"));
                    break;

                default:
                    Console.WriteLine($"[online] Rename to '{name}': no answer ({delivery.Problem}); the name goes with the next clear");
                    break;
            }
        }

        private void NoticeNormalizedName(ScoreAnswerBody body)
        {
            if (!string.IsNullOrWhiteSpace(body?.Name) && body.Name != _identity.Name)
                _notices.Enqueue(new OnlineNotice(OnlineNoticeKind.NameNormalized, body.Name));
        }

        private void NoticeRefusedName(ScoreAnswerBody body)
        {
            if (body?.Reason != null && body.Reason.Contains("name", StringComparison.OrdinalIgnoreCase))
                _notices.Enqueue(new OnlineNotice(OnlineNoticeKind.NameRefused, body.Reason));
        }

        /// <param name="acceptWithoutBody">For a DELETE or a PUT: a 2xx is a delivery even with no JSON (a 204).
        /// A POST's 2xx must carry the service's answer — see the class doc on captive portals.</param>
        private async Task<Delivery> SendAsync(HttpMethod method, string path, string json, CancellationToken stop,
            bool acceptWithoutBody = false)
        {
            using HttpRequestMessage request = new(method, new Uri(Server, path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _identity.Token);
            if (json != null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(stop);
            deadline.CancelAfter(RequestTimeout);

            try
            {
                using HttpResponseMessage response = await _http.SendAsync(request, deadline.Token);
                int status = (int)response.StatusCode;
                string text = await response.Content.ReadAsStringAsync(deadline.Token);
                ScoreAnswerBody body = TryParseAnswer(response, text);

                if (status >= 200 && status < 300)
                {
                    bool answered = body?.Accepted != null || (acceptWithoutBody && (body != null || text.Length == 0));
                    return answered
                        ? new Delivery(DeliveryKind.Delivered, status, body)
                        : new Delivery(DeliveryKind.NoAnswer, status, problem: $"{status} without the service's answer (a captive portal?)");
                }

                if (status == (int)HttpStatusCode.RequestTimeout || status == (int)HttpStatusCode.TooManyRequests || status >= 500)
                    return new Delivery(DeliveryKind.NoAnswer, status, problem: $"{status} {response.ReasonPhrase}");

                return new Delivery(DeliveryKind.Refused, status, body);
            }
            catch (OperationCanceledException) when (!stop.IsCancellationRequested)
            {
                return new Delivery(DeliveryKind.NoAnswer, 0, problem: $"nothing within {RequestTimeout.TotalSeconds:0} s");
            }
            catch (HttpRequestException e)
            {
                return new Delivery(DeliveryKind.NoAnswer, 0, problem: e.Message);
            }
        }

        private static ScoreAnswerBody TryParseAnswer(HttpResponseMessage response, string text)
        {
            if (response.Content.Headers.ContentType?.MediaType is not "application/json") return null;

            try
            {
                return JsonSerializer.Deserialize<ScoreAnswerBody>(text, RequestJson);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private List<ScoreSubmission> LoadOutbox()
        {
            OutboxFile file = TryReadOutbox(_outboxPath) ?? TryReadOutbox(_outboxPath + OutboxBackupSuffix);
            return file?.Submissions ?? new List<ScoreSubmission>();
        }

        private static OutboxFile TryReadOutbox(string path)
        {
            try
            {
                using FileStream stream = File.OpenRead(path);
                OutboxFile file = JsonSerializer.Deserialize<OutboxFile>(stream, OutboxJson);
                if (file?.Format == OutboxFile.FormatMarker && file.Version <= OutboxFile.CurrentVersion) return file;
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException
                or ArgumentException or NotSupportedException)
            {
            }

            return null;
        }

        private void SaveOutbox(List<ScoreSubmission> outbox)
        {
            try
            {
                AtomicFile.WriteText(_outboxPath,
                    JsonSerializer.Serialize(new OutboxFile { Submissions = outbox }, OutboxJson), OutboxBackupSuffix);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                //The submission is still in memory and still goes; only its survival of a crash is lost
                Console.WriteLine($"[online] Could not write the outbox '{_outboxPath}': {e.Message}");
            }
        }

        /// <summary>A file removed, or already absent; a failure is said and costs only the file.</summary>
        internal static void DeleteQuietly(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"[online] Could not delete '{path}': {e.Message}");
            }
        }

        //Invariant: the first run of this printed "10,0 s" on a Czech machine
        private static string Describe(ScoreSubmission submission) => string.Create(CultureInfo.InvariantCulture,
            $"{submission.Level.File}#{submission.Level.Hash} score {submission.Score} ({submission.Stars}*,"
            + $" {submission.ShotsUsed} shots, {submission.DurationSeconds:0.0} s)");

        private static string RankText(ScoreAnswerBody body) =>
            $"accepted — this month #{body.Month?.Rank ?? 0} of {body.Month?.Total ?? 0},"
            + $" all time #{body.AllTime?.Rank ?? 0} of {body.AllTime?.Total ?? 0}"
            + (body.PersonalBest ? ", a personal best" : "");

        private enum DeliveryKind : byte { Delivered, Refused, NoAnswer }

        private readonly struct Delivery
        {
            public readonly DeliveryKind Kind;
            public readonly int Status;
            public readonly ScoreAnswerBody Body;
            public readonly string Problem;

            public Delivery(DeliveryKind kind, int status, ScoreAnswerBody body = null, string problem = null)
            {
                Kind = kind;
                Status = status;
                Body = body;
                Problem = problem;
            }
        }

        #endregion

        #region The server and the build

        /// <summary>
        /// The server this build may talk to: the one <c>Settings.json</c> names, or for a release build the
        /// built-in <see cref="DefaultServer"/>. False with the reason, worded for the <c>[online]</c> line, when
        /// there is none or the one named is refused.
        /// </summary>
        internal static bool TryResolveServer(GameSettings settings, out Uri server, out string problem, out bool named)
        {
            server = null;
            string address = string.IsNullOrWhiteSpace(settings.Server) ? null : settings.Server.Trim();
            named = address != null;
            address ??= ReleaseVersion != null ? DefaultServer : null;

            if (address == null)
            {
                problem = ReleaseVersion != null
                    ? "this release has no built-in score server yet, and the settings name none"
                    : $"a local build ({GameVersion}) submits only to a server Settings.json names";
                return false;
            }

            if (!TryParseServer(address, out server, out string refused))
            {
                problem = $"refusing the score server '{address}' — {refused}";
                return false;
            }

            problem = null;
            return true;
        }

        /// <summary>
        /// A server the client will talk to: absolute, <c>https://</c> anywhere, or <c>http://</c> to this machine
        /// only — a local run of the API answers on <c>http://localhost:5000</c>, and anything else in plain HTTP
        /// would send the player's token across the network in the clear. Normalized to end in a slash, so the
        /// contract's relative paths land under any base path the server is mounted at.
        /// </summary>
        internal static bool TryParseServer(string address, out Uri server, out string problem)
        {
            server = null;
            problem = null;

            if (!Uri.TryCreate(address, UriKind.Absolute, out Uri uri))
            {
                problem = "not an absolute address";
                return false;
            }

            bool secure = uri.Scheme == Uri.UriSchemeHttps;
            bool local = uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;

            if (!secure && !local)
            {
                problem = "only https://, or http:// to this machine, would keep the token off the network in the clear";
                return false;
            }

            server = uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");
            return true;
        }

        private static string ReadReleaseVersion()
        {
            foreach (AssemblyMetadataAttribute metadata in typeof(OnlineScores).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
                if (metadata.Key == ReleaseMetadataKey && !string.IsNullOrWhiteSpace(metadata.Value))
                    return metadata.Value;

            return null;
        }

        /// <summary>
        /// <c>dev-&lt;short sha&gt;</c> off the informational version the SDK stamps (<c>1.0.0+&lt;sha&gt;</c>),
        /// or plain <c>dev</c> when the build carried no commit.
        /// </summary>
        private static string DevVersion()
        {
            string informational = typeof(OnlineScores).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            int plus = informational?.IndexOf('+') ?? -1;
            if (plus < 0 || plus + 1 >= informational.Length) return "dev";

            string sha = informational[(plus + 1)..];
            return "dev-" + (sha.Length > 7 ? sha[..7] : sha);
        }

        #endregion
    }
}
