using Prazsky.Core.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace BS3D.Online
{
    /// <summary>How a note fared, as the note page tells the player.</summary>
    internal enum NoteDelivery
    {
        /// <summary>The service has it.</summary>
        Sent,

        /// <summary>The service did not answer: the note stays in the outbox and goes at the next start or the next note.</summary>
        Kept,

        /// <summary>The service refused it for good (its reason is logged); it is gone from the outbox.</summary>
        Refused,

        /// <summary>This build reaches no server (a local build with none named): the note stays in the outbox.</summary>
        NoServer,
    }

    /// <summary>A note's fate, handed back to the frame.</summary>
    internal readonly record struct NoteOutcome(Guid NoteId, NoteDelivery Delivery, bool PictureStored);

    /// <summary>
    /// The game's side of <b>Send a Note</b> (#813, the service's side BS3D-API#10): a line the player typed about what
    /// they were looking at, the game's context, and unless they unticked it a JPEG of that frame, to
    /// <c>POST /v1/notes</c>.
    /// <para>
    /// <b>Anyone may send one</b> (the owner's answer, 2026-10-06), so this does not wait for the online scores to be
    /// switched on: pressing Send is the consent for that note. A player with an online identity signs it with it (the
    /// player id and the token, so the service can link it to their scores); anyone else sends it unsigned.
    /// </para>
    /// <para>
    /// <b>It follows <see cref="OnlineScores"/>' rules, not its machinery</b>: every note is written to the outbox,
    /// <c>Notes/</c> in the user data folder, before it is sent (<c>&lt;id&gt;.json</c>, and <c>&lt;id&gt;.jpg</c> with a
    /// picture), and leaves only when the service has answered for it; the server is the one
    /// <see cref="OnlineScores.TryResolveServer"/> resolves, so a local build reaches only a server
    /// <c>Settings.json</c> names and a developer's runs never post to the owner's notes; an answer is the service's
    /// only when it carries its JSON (a captive portal's 200 and a stranger's 404 keep the note); a 4xx with the
    /// service's reason drops it and logs why. There is no retry timer: the outbox is drained at start and after each
    /// new note. A separate worker from the scores' because that one exists only for a player with an identity.
    /// </para>
    /// <para>
    /// <b>Nothing of this runs on the frame</b>: the page hands a note over through a queue, the worker writes the
    /// files and sends them, and the outcome comes back through another queue the page reads once a frame.
    /// </para>
    /// </summary>
    internal sealed class OnlineNotes : IDisposable
    {
        internal const string FolderName = "Notes";

        /// <summary>The longest note, as the service takes it (<c>Scores:NoteMaxLength</c>).</summary>
        internal const int MaxTextLength = 1000;

        /// <summary>Notes kept waiting at most; past it the oldest go, so a game that never reaches a server does not fill a disk.</summary>
        internal const int OutboxCapacity = 50;

        private const string Format = "bs3d-note";

        /// <summary>
        /// Longer than a score's: a note carries up to a few hundred kilobytes of picture, and a slow uplink sends that
        /// in seconds, not in one.
        /// </summary>
        internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly string _folder;
        private readonly Uri _server;
        private readonly Func<OnlineIdentity> _identity;
        private readonly HttpClient _http;
        private readonly ConcurrentQueue<PendingNote> _incoming = new();
        private readonly ConcurrentQueue<NoteOutcome> _outcomes = new();
        private readonly SemaphoreSlim _wake = new(0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _worker;

        private sealed record PendingNote(Guid NoteId, string Text, JsonObject Context, byte[] Picture);

        private OnlineNotes(string folder, Uri server, Func<OnlineIdentity> identity)
        {
            _folder = folder;
            _server = server;
            _identity = identity;

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

        /// <summary>Whether a note can leave this machine at all: false for a local build with no server named.</summary>
        internal bool CanReachServer => _server != null;

        /// <summary>
        /// Starts the worker, which drains whatever an earlier run left in the outbox. No I/O here. Says in one
        /// <c>[note]</c> line where notes go.
        /// </summary>
        /// <param name="identity">Read when a note is sent, so a nickname taken after the note was written still signs it.</param>
        internal static OnlineNotes Start(GameSettings settings, Func<OnlineIdentity> identity, string folder)
        {
            bool resolved = OnlineScores.TryResolveServer(settings, out Uri server, out string problem, out _);
            Console.WriteLine(resolved ? $"[note] Notes go to {server}" : $"[note] Notes stay in {FolderName}/: {problem}");
            return new OnlineNotes(folder, resolved ? server : null, identity);
        }

        /// <summary>
        /// Hands a note to the worker, which writes it to the outbox and sends it. Returns its id, which its
        /// <see cref="NoteOutcome"/> carries.
        /// </summary>
        /// <param name="picture">The JPEG, or null when the player unticked it.</param>
        internal Guid Send(string text, JsonObject context, byte[] picture)
        {
            Guid id = Guid.NewGuid();
            _incoming.Enqueue(new PendingNote(id, text, context, picture));
            _wake.Release();
            return id;
        }

        /// <summary>A note's fate, once the worker knows it. No allocation when there is nothing to take.</summary>
        internal bool TryTakeOutcome(out NoteOutcome outcome) => _outcomes.TryDequeue(out outcome);

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
                    while (_incoming.TryDequeue(out PendingNote note))
                    {
                        Write(note);
                        await DrainAsync(note.NoteId, stop);
                    }
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            catch (Exception e)
            {
                Console.WriteLine($"[note] The note worker stopped: {e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>The note on disk, before a byte of it goes anywhere, and the outbox cut to its capacity.</summary>
        private void Write(PendingNote note)
        {
            try
            {
                Directory.CreateDirectory(_folder);
                //The picture before the note, and whole before it has its name: the note's file is what says a note exists
                if (note.Picture != null)
                {
                    string temp = PicturePath(note.NoteId) + ".tmp";
                    File.WriteAllBytes(temp, note.Picture);
                    File.Move(temp, PicturePath(note.NoteId), overwrite: true);
                }

                JsonObject file = new()
                {
                    ["format"] = Format,
                    ["version"] = 1,
                    ["noteId"] = note.NoteId,
                    ["written"] = DateTimeOffset.UtcNow,
                    ["text"] = note.Text,
                    ["gameVersion"] = BuildVersion.Name,
                    ["context"] = note.Context,
                    ["picture"] = note.Picture != null,
                };
                AtomicFile.WriteText(NotePath(note.NoteId), file.ToJsonString(), null);
                Console.WriteLine($"[note] Written {note.NoteId.ToString()[..8]}: {note.Text.Length} characters"
                    + (note.Picture != null ? $", a picture of {note.Picture.Length / 1024} KB" : ", no picture"));

                FileInfo[] waiting = Waiting();
                for (int i = 0; i < waiting.Length - OutboxCapacity; i++) Delete(waiting[i]);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"[note] Could not write the note: {e.Message}");
            }
        }

        /// <summary>
        /// Sends the outbox oldest first and stops at the first note that gets no answer. <paramref name="current"/> is
        /// the note just written, whose fate the page is waiting for; it is told even when nothing could be tried.
        /// </summary>
        private async Task DrainAsync(Guid? current, CancellationToken stop)
        {
            if (_server == null)
            {
                if (current is Guid id) _outcomes.Enqueue(new NoteOutcome(id, NoteDelivery.NoServer, false));
                return;
            }

            bool told = false;
            foreach (FileInfo file in Waiting())
            {
                if (stop.IsCancellationRequested) return;

                (Guid id, NoteDelivery delivery, bool picture) = await SendAsync(file, stop);
                if (current == id)
                {
                    _outcomes.Enqueue(new NoteOutcome(id, delivery, picture));
                    told = true;
                }
                if (delivery == NoteDelivery.Kept) break;
            }

            if (!told && current is Guid kept) _outcomes.Enqueue(new NoteOutcome(kept, NoteDelivery.Kept, false));
        }

        private async Task<(Guid, NoteDelivery, bool)> SendAsync(FileInfo file, CancellationToken stop)
        {
            JsonObject stored;
            try
            {
                stored = JsonNode.Parse(await File.ReadAllTextAsync(file.FullName, stop)) as JsonObject;
            }
            catch (Exception e) when (e is IOException or JsonException)
            {
                Console.WriteLine($"[note] Dropping an unreadable note {file.Name}: {e.Message}");
                Delete(file);
                return (Guid.Empty, NoteDelivery.Refused, false);
            }

            if (stored?["format"]?.GetValue<string>() != Format || !Guid.TryParse(stored["noteId"]?.GetValue<string>(), out Guid id))
            {
                Console.WriteLine($"[note] Dropping {file.Name}: not a note");
                Delete(file);
                return (Guid.Empty, NoteDelivery.Refused, false);
            }

            JsonObject body = new()
            {
                ["noteId"] = id,
                ["text"] = stored["text"]?.GetValue<string>(),
                ["gameVersion"] = stored["gameVersion"]?.GetValue<string>(),
                ["context"] = stored["context"]?.DeepClone(),
            };
            if (stored["picture"]?.GetValue<bool>() == true && File.Exists(PicturePath(id)))
                body["screenshot"] = Convert.ToBase64String(await File.ReadAllBytesAsync(PicturePath(id), stop));

            //Signed with the identity as it stands now, as a score is (#572)
            OnlineIdentity identity = _identity();
            using HttpRequestMessage request = new(HttpMethod.Post, new Uri(_server, "v1/notes"));
            if (identity is { IsUsable: true })
            {
                body["playerId"] = identity.PlayerId;
                body["name"] = identity.Name;
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", identity.Token);
            }
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

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
                    //A 2xx is the service's only when it names this note: a captive portal answers everything with its page
                    if (answer?["noteId"]?.GetValue<string>() is string named && Guid.TryParse(named, out Guid answered) && answered == id)
                    {
                        bool picture = answer["screenshotStored"]?.GetValue<bool>() == true;
                        Delete(file);
                        Console.WriteLine($"[note] Sent {tag} ({status}){(body.ContainsKey("screenshot") && !picture ? ", its picture not kept by the service" : "")}");
                        return (id, NoteDelivery.Sent, picture);
                    }
                    Console.WriteLine($"[note] Kept {tag}: {status} without the service's answer (a captive portal?)");
                    return (id, NoteDelivery.Kept, false);
                }

                string reason = answer?["reason"]?.GetValue<string>();
                if (status == (int)HttpStatusCode.RequestTimeout || status == (int)HttpStatusCode.TooManyRequests || status >= 500
                    || string.IsNullOrEmpty(reason))
                {
                    Console.WriteLine($"[note] Kept {tag}: {status} {(reason ?? response.ReasonPhrase)}");
                    return (id, NoteDelivery.Kept, false);
                }

                //A refused picture is not a reason to lose the words: the note goes again without it
                if (body.ContainsKey("screenshot") && reason is "bad-picture" or "picture-too-large")
                {
                    Console.WriteLine($"[note] The service refused {tag}'s picture ({reason}): sending it without one");
                    stored["picture"] = false;
                    AtomicFile.WriteText(file.FullName, stored.ToJsonString(), null);
                    OnlineScores.DeleteQuietly(PicturePath(id));
                    return await SendAsync(file, stop);
                }

                Console.WriteLine($"[note] The service refused {tag} for good: {status} {OnlineScores.CleanText(reason, 60)}");
                Delete(file);
                return (id, NoteDelivery.Refused, false);
            }
            catch (OperationCanceledException) when (!stop.IsCancellationRequested)
            {
                Console.WriteLine($"[note] Kept {tag}: nothing within {RequestTimeout.TotalSeconds:0} s");
            }
            catch (Exception e) when (!stop.IsCancellationRequested && e is not OperationCanceledException)
            {
                Console.WriteLine($"[note] Kept {tag}: {e.GetType().Name}: {e.Message}");
            }
            return (id, NoteDelivery.Kept, false);
        }

        private static JsonObject TryParse(HttpResponseMessage response, string text)
        {
            if (response.Content.Headers.ContentType?.MediaType != "application/json") return null;
            try { return JsonNode.Parse(text) as JsonObject; }
            catch (JsonException) { return null; }
        }

        /// <summary>The notes waiting, oldest first.</summary>
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

        private void Delete(FileInfo note)
        {
            OnlineScores.DeleteQuietly(note.FullName);
            OnlineScores.DeleteQuietly(Path.ChangeExtension(note.FullName, ".jpg"));
        }

        private string NotePath(Guid id) => Path.Combine(_folder, id.ToString("N") + ".json");

        private string PicturePath(Guid id) => Path.Combine(_folder, id.ToString("N") + ".jpg");
    }
}
