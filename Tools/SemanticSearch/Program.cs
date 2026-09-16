using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BS3D.Tools.SemanticSearch
{
    /// <summary>
    /// Finds the GitHub issues, and optionally the agent-journal entries, that <b>mean</b> the same as a piece of text,
    /// through a local embedding model served by LM Studio. It exists for the check before an issue is filed: grep and
    /// <c>gh issue list</c> only find the words you thought of, and #319 was filed an hour after #316 and #317 described
    /// the same three levels in other words.
    /// <para>
    /// <b>Measured before it was kept (2026-09-16), and the two halves came out very differently.</b> Over the 422
    /// issues, the known partner of 4 out of 7 probes ranked 1st or 2nd, and the other three ranked 9, 11 and 53, each
    /// under issues on the same subject. Over the Czech journal, paraphrased Czech questions put the answer at ranks 62,
    /// 201, 70 and 9 of 532 chunks. The default model is English-centric, which is why the journal is behind a switch and
    /// says so when it answers.
    /// </para>
    /// </summary>
    internal static class Program
    {
        private const string DEFAULT_ENDPOINT = "http://localhost:1234/v1";
        private const string DEFAULT_MODEL = "text-embedding-nomic-embed-text-v1.5";

        //nomic-embed-text reads at most 2048 tokens; 2500 characters of this project's English and Czech stays under it
        private const int MAX_CHARS = 2500;

        private static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            Options options = Options.Parse(args, DEFAULT_MODEL, DEFAULT_ENDPOINT);
            if (options == null) { PrintUsage(); return 2; }

            string repo = FindRepo();
            if (repo == null) { Console.Error.WriteLine("[search] no directory holding Game.sln above this tool"); return 2; }

            //Live on every run, never cached: a duplicate check has to see the issue somebody filed a minute ago
            List<Doc> issues;
            try { issues = LoadIssues(repo); }
            catch (Exception e) { Console.Error.WriteLine($"[search] gh issue list failed: {e.Message}"); return 2; }

            string queryText, queryLabel;
            int exclude = 0;
            if (options.Issue > 0)
            {
                Doc self = issues.FirstOrDefault(d => d.Number == options.Issue);
                if (self == null) { Console.Error.WriteLine($"[search] there is no issue #{options.Issue}"); return 2; }
                queryText = self.Text;
                queryLabel = $"issue #{self.Number} \"{Trim(self.Title, 80)}\"";
                exclude = self.Number;
            }
            else if (options.File != null)
            {
                queryText = File.ReadAllText(options.File);
                queryLabel = $"file {options.File}";
            }
            else
            {
                queryText = options.Query;
                queryLabel = $"\"{Trim(options.Query, 80)}\"";
            }

            var client = new EmbeddingClient(options.Endpoint, options.Model);

            //One cache per corpus, so a run that searches only the issues cannot throw away the journal's vectors
            EmbeddingCache issueCache = EmbeddingCache.Load(options.Model, "issues");
            EmbeddingCache journalCache = options.Journal ? EmbeddingCache.Load(options.Model, "journal") : null;

            try
            {
                float[] query = await client.EmbedQuery(Clip(queryText));

                //The whole corpus goes through the cache, not only the pool ranked below, so a filtered run (--open,
                //--issue) does not drop the vectors the next unfiltered one needs
                (int embedded, int cached) = await client.EmbedDocuments(issues, issueCache);
                List<Doc> pool = issues.Where(d => d.Number != exclude && (!options.OpenOnly || d.Open)).ToList();

                Console.WriteLine($"[search] {options.Model}: {pool.Count} issues ranked, {embedded} embedded now, {cached} from cache");
                Console.WriteLine($"[search] query: {queryLabel}");
                foreach ((Doc doc, float score) in Rank(pool, query).Take(options.Top))
                    Console.WriteLine($"  {score:F3}  #{doc.Number,-4} {(doc.Open ? "open  " : "closed")}  {Trim(doc.Title, 110)}");

                if (options.Journal)
                {
                    List<Doc> chunks = ChunkJournal(repo);
                    (embedded, cached) = await client.EmbedDocuments(chunks, journalCache);

                    Console.WriteLine();
                    Console.WriteLine($"[search] journal: {chunks.Count} chunks, {embedded} embedded now, {cached} from cache");
                    if (options.Model == DEFAULT_MODEL)
                        Console.WriteLine("[search] the default model is English-centric: Czech questions ranked the answer 9th to 201st of 532 in the trial. Ask in English, or pass a multilingual --model.");
                    foreach ((Doc doc, float score) in Rank(chunks, query).Take(options.Top))
                        Console.WriteLine($"  {score:F3}  {doc.Title}");
                }
            }
            catch (EmbeddingException e)
            {
                Console.Error.WriteLine(e.Message);
                return 3;
            }
            finally
            {
                issueCache.Save();
                journalCache?.Save();
            }

            return 0;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("SemanticSearch: GitHub issues (and journal entries) that mean the same as a text, via a local embedding model in LM Studio.");
            Console.WriteLine();
            Console.WriteLine("  dotnet run --project Tools\\SemanticSearch -- \"text to look for\"");
            Console.WriteLine("  dotnet run --project Tools\\SemanticSearch -- --issue 425        the issues most like #425");
            Console.WriteLine("  dotnet run --project Tools\\SemanticSearch -- --file draft.md    a drafted issue, before it is filed");
            Console.WriteLine();
            Console.WriteLine("  --open           open issues only");
            Console.WriteLine("  --journal        also search docs/agent-notes.md and docs/agent-notes-archive/");
            Console.WriteLine("  --top N          results per list (8)");
            Console.WriteLine($"  --model KEY      embedding model ({DEFAULT_MODEL})");
            Console.WriteLine($"  --endpoint URL   LM Studio's OpenAI-compatible base ({DEFAULT_ENDPOINT})");
            Console.WriteLine();
            Console.WriteLine($"Needs LM Studio running with the model loaded: lms load {DEFAULT_MODEL}");
        }

        //By landmark rather than by counting "..", as LevelGen and ScoreSim do
        private static string FindRepo()
        {
            for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Game.sln")) && Directory.Exists(Path.Combine(dir.FullName, "docs")))
                    return dir.FullName;
            return null;
        }

        private static List<Doc> LoadIssues(string repo)
        {
            var start = new ProcessStartInfo("gh", "issue list --state all --limit 5000 --json number,title,body,state")
            {
                WorkingDirectory = repo,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                UseShellExecute = false
            };

            using Process gh = Process.Start(start);
            Task<string> error = gh.StandardError.ReadToEndAsync();
            string json = gh.StandardOutput.ReadToEnd();
            gh.WaitForExit();
            if (gh.ExitCode != 0) throw new InvalidOperationException(error.Result.Trim());

            var issues = new List<Doc>();
            using JsonDocument doc = JsonDocument.Parse(json);
            foreach (JsonElement e in doc.RootElement.EnumerateArray())
            {
                int number = e.GetProperty("number").GetInt32();
                string title = e.GetProperty("title").GetString();
                string body = e.GetProperty("body").GetString() ?? "";
                issues.Add(new Doc
                {
                    Number = number,
                    Title = title,
                    Open = e.GetProperty("state").GetString() == "OPEN",
                    Text = Clip($"#{number} {title}\n{body}")
                });
            }
            return issues;
        }

        /// <summary>
        /// The journal cut into entries at its <c>## </c> headings, and each entry into pieces short enough for the model,
        /// at paragraph breaks. Every piece carries its entry's heading, which is what dates it and says whose it is.
        /// </summary>
        private static List<Doc> ChunkJournal(string repo)
        {
            var files = new List<string> { Path.Combine(repo, "docs", "agent-notes.md") };
            string archive = Path.Combine(repo, "docs", "agent-notes-archive");
            if (Directory.Exists(archive)) files.AddRange(Directory.GetFiles(archive, "*.md").OrderBy(f => f));

            var chunks = new List<Doc>();
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                string heading = "(before the first entry)";
                var entry = new StringBuilder();

                void Flush()
                {
                    if (entry.Length == 0) return;
                    var pieces = new List<string>();
                    var piece = new StringBuilder();
                    int room = MAX_CHARS - 200;
                    foreach (string paragraph in entry.ToString().Split("\n\n"))
                    {
                        string p = paragraph.Length > room ? paragraph[..room] : paragraph;
                        if (piece.Length > 0 && piece.Length + p.Length + 2 > room) { pieces.Add(piece.ToString()); piece.Clear(); }
                        piece.Append(p).Append("\n\n");
                    }
                    if (piece.Length > 0) pieces.Add(piece.ToString());

                    for (int k = 0; k < pieces.Count; k++)
                        chunks.Add(new Doc { Title = $"{name} | {heading} | part {k + 1}/{pieces.Count}", Text = heading + "\n" + pieces[k] });
                    entry.Clear();
                }

                foreach (string line in File.ReadLines(file))
                {
                    if (line.StartsWith("## ")) { Flush(); heading = line[3..].Trim(); continue; }
                    entry.Append(line).Append('\n');
                }
                Flush();
            }
            return chunks;
        }

        private static IEnumerable<(Doc doc, float score)> Rank(IEnumerable<Doc> docs, float[] query) =>
            docs.Select(d => (d, Dot(d.Vector, query))).OrderByDescending(x => x.Item2);

        private static float Dot(float[] a, float[] b)
        {
            float sum = 0f;
            for (int i = 0; i < a.Length; i++) sum += a[i] * b[i];
            return sum;
        }

        private static string Clip(string s) => s.Length > MAX_CHARS ? s[..MAX_CHARS] : s;

        private static string Trim(string s, int length) => s.Length <= length ? s : s[..length] + "…";
    }

    internal sealed class Doc
    {
        public int Number;
        public string Title;
        public bool Open;
        public string Text;
        public float[] Vector;
    }

    internal sealed class Options
    {
        public string Query = "";
        public int Issue;
        public string File;
        public bool Journal;
        public bool OpenOnly;
        public int Top = 8;
        public string Model;
        public string Endpoint;

        //Null for anything malformed, and for anything that does not name exactly one thing to search for
        public static Options Parse(string[] args, string model, string endpoint)
        {
            var options = new Options { Model = model, Endpoint = endpoint };
            var words = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--issue":
                        if (++i >= args.Length || !int.TryParse(args[i].TrimStart('#'), out options.Issue)) return null;
                        break;
                    case "--file":
                        if (++i >= args.Length) return null;
                        options.File = args[i];
                        break;
                    case "--top":
                        if (++i >= args.Length || !int.TryParse(args[i], out options.Top) || options.Top < 1) return null;
                        break;
                    case "--model":
                        if (++i >= args.Length) return null;
                        options.Model = args[i];
                        break;
                    case "--endpoint":
                        if (++i >= args.Length) return null;
                        options.Endpoint = args[i].TrimEnd('/');
                        break;
                    case "--journal": options.Journal = true; break;
                    case "--open": options.OpenOnly = true; break;
                    default:
                        if (args[i].StartsWith("-")) return null;
                        words.Add(args[i]);
                        break;
                }
            }

            options.Query = string.Join(" ", words).Trim();
            int named = (options.Issue > 0 ? 1 : 0) + (options.File != null ? 1 : 0) + (options.Query.Length > 0 ? 1 : 0);
            return named == 1 ? options : null;
        }
    }

    internal sealed class EmbeddingException : Exception
    {
        public EmbeddingException(string message) : base(message) { }
    }

    /// <summary>
    /// LM Studio's <c>/embeddings</c>. Documents go through an <see cref="EmbeddingCache"/>, so only a text it has not
    /// seen is sent; the query is one request and is not cached. The prefixes are the model family's own —
    /// nomic-embed-text is trained with <c>search_document:</c> and <c>search_query:</c>, e5 with <c>passage:</c> and
    /// <c>query:</c> — and a family this does not know gets none.
    /// </summary>
    internal sealed class EmbeddingClient
    {
        private const int BATCH = 16;

        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };
        private readonly string _endpoint;
        private readonly string _model;
        private readonly string _documentPrefix;
        private readonly string _queryPrefix;

        public EmbeddingClient(string endpoint, string model)
        {
            _endpoint = endpoint;
            _model = model;

            string family = model.ToLowerInvariant();
            (_documentPrefix, _queryPrefix) = family.Contains("nomic") ? ("search_document: ", "search_query: ")
                : family.Contains("e5") ? ("passage: ", "query: ")
                : ("", "");
        }

        public async Task<float[]> EmbedQuery(string text) => (await Request(new List<string> { _queryPrefix + text }))[0];

        /// <summary>Fills every document's vector, from the cache where it can. Returns how many were sent and how many were not.</summary>
        public async Task<(int embedded, int cached)> EmbedDocuments(List<Doc> docs, EmbeddingCache cache)
        {
            var missing = new List<Doc>();
            foreach (Doc doc in docs)
                if (cache.TryGet(_documentPrefix + doc.Text, out float[] vector)) doc.Vector = vector;
                else missing.Add(doc);

            for (int start = 0; start < missing.Count; start += BATCH)
            {
                List<Doc> batch = missing.Skip(start).Take(BATCH).ToList();
                List<float[]> vectors = await Request(batch.Select(d => _documentPrefix + d.Text).ToList());
                for (int i = 0; i < batch.Count; i++)
                {
                    batch[i].Vector = vectors[i];
                    cache.Put(_documentPrefix + batch[i].Text, vectors[i]);
                }
            }

            return (missing.Count, docs.Count - missing.Count);
        }

        private async Task<List<float[]>> Request(List<string> texts)
        {
            string body = JsonSerializer.Serialize(new { model = _model, input = texts });

            HttpResponseMessage response;
            try
            {
                response = await _http.PostAsync(_endpoint + "/embeddings", new StringContent(body, Encoding.UTF8, "application/json"));
            }
            catch (HttpRequestException e)
            {
                throw new EmbeddingException($"[search] LM Studio is not answering at {_endpoint} ({e.Message}). Start it and load the model: lms load {_model}");
            }

            string json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new EmbeddingException($"[search] LM Studio refused the request ({(int)response.StatusCode}): {json.Trim()}\n[search] is the model loaded? lms load {_model}");

            var vectors = new float[texts.Count][];
            using JsonDocument doc = JsonDocument.Parse(json);
            foreach (JsonElement item in doc.RootElement.GetProperty("data").EnumerateArray())
                vectors[item.GetProperty("index").GetInt32()] = Normalize(item.GetProperty("embedding").EnumerateArray().Select(x => x.GetSingle()).ToArray());

            return vectors.ToList();
        }

        private static float[] Normalize(float[] v)
        {
            double sum = 0;
            foreach (float x in v) sum += x * (double)x;
            float scale = sum > 0 ? (float)(1.0 / Math.Sqrt(sum)) : 0f;
            for (int i = 0; i < v.Length; i++) v[i] *= scale;
            return v;
        }
    }

    /// <summary>
    /// Vectors already paid for, keyed by a hash of the exact text sent (prefix included), one file per model and
    /// corpus, kept under <c>%LOCALAPPDATA%\BS3D-Tools</c> and never in the repository. Only what the corpus holds
    /// this run is written back, so an edited issue's old vector does not linger.
    /// </summary>
    internal sealed class EmbeddingCache
    {
        private readonly string _path;
        private readonly Dictionary<string, float[]> _stored = new();
        private readonly Dictionary<string, float[]> _used = new();

        private EmbeddingCache(string path) { _path = path; }

        public static EmbeddingCache Load(string model, string corpus)
        {
            string safe = string.Concat(model.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '.' ? c : '_'));
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BS3D-Tools", "SemanticSearch");
            var cache = new EmbeddingCache(Path.Combine(dir, $"{safe}.{corpus}.bin"));

            if (!File.Exists(cache._path)) return cache;

            try
            {
                using var reader = new BinaryReader(File.OpenRead(cache._path));
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string key = reader.ReadString();
                    var vector = new float[reader.ReadInt32()];
                    for (int j = 0; j < vector.Length; j++) vector[j] = reader.ReadSingle();
                    cache._stored[key] = vector;
                }
            }
            catch (Exception)
            {
                //A cache that cannot be read is only lost time: start empty and write a good one at the end
                cache._stored.Clear();
            }
            return cache;
        }

        public bool TryGet(string text, out float[] vector)
        {
            string key = Key(text);
            if (!_stored.TryGetValue(key, out vector)) return false;
            _used[key] = vector;
            return true;
        }

        public void Put(string text, float[] vector) => _used[Key(text)] = vector;

        public void Save()
        {
            if (_used.Count == 0) return;

            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            string temp = _path + ".tmp";
            using (var writer = new BinaryWriter(File.Create(temp)))
            {
                writer.Write(_used.Count);
                foreach ((string key, float[] vector) in _used)
                {
                    writer.Write(key);
                    writer.Write(vector.Length);
                    foreach (float x in vector) writer.Write(x);
                }
            }
            File.Move(temp, _path, overwrite: true);
        }

        private static string Key(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
