using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    /// 201, 70 and 9 of 532 chunks. That model (nomic-embed-text) is English-centric, which is why the journal is behind
    /// a switch and why <c>--model</c> exists.
    /// </para>
    /// <para>
    /// <b>The default became Qwen3-Embedding-0.6B on 2026-09-21 (#439), on the same questions, paired.</b> Issues, the
    /// known partner's rank, nomic against Qwen3: 1/1, 1/1, 1/1, 9/3, 4/3, 2/1, 1/1. The Czech journal, the rank of the
    /// first of 707 chunks carrying the answer's marker: 83/49, 273/8, 1/2, 86/17, 2/8 (the one question asked in
    /// English), 10/2 — the answer in the top ten for 5 of 6 questions against 3 of 6. The documents (#490's fifteen
    /// questions): nomic 11 first, 3 second, one 115th; Qwen3 12 first, 2 second, that one 20th. <c>--mark</c> is how
    /// these were read off, so the next model is measured the same way.
    /// </para>
    /// <para>
    /// <b><c>--ask</c> (#494, measured 2026-09-21):</b> the ten results handed to Gemma 4 12B on the CPU, 53–134 s an
    /// answer. Journal: right and citing the marker's entry 3 of 6, a sibling part of the right entry 2, and once, with
    /// the marker outside the ten, a confident answer to a neighbouring question — the instruction to say that the notes
    /// do not answer was ignored. Documents: 4 of 4. A lead into the cited entry, never a source.
    /// </para>
    /// <para>
    /// <b>The documents (#490, measured 2026-09-21 with nomic):</b> fifteen paraphrased questions whose answer sits in
    /// one known section of <c>docs/</c>, CLAUDE.md or BestPractices.md put that section 1st eleven times and 2nd three
    /// times. The one miss is CLAUDE.md's "Project" at 115th — a piece that packs the merge rule, the three executables
    /// and the Testbed's role into one text, of which the question matched a tenth — while the top hit,
    /// <c>docs/testbed.md</c>'s own opening, answered it as well. 1010 pieces from 1.8 MB, 27 s to embed on the first
    /// run on the GPU; Qwen3 took 112 s for the same on the CPU alone (the GPU figure is unmeasured — the machine reset).
    /// </para>
    /// </summary>
    internal static class Program
    {
        private const string DEFAULT_ENDPOINT = "http://localhost:1234/v1";
        //Qwen3-Embedding-0.6B since #439: never worse than nomic on the English issues and far better on the Czech journal
        private const string DEFAULT_MODEL = "text-embedding-qwen3-embedding-0.6b";
        private const string NOMIC_MODEL = "text-embedding-nomic-embed-text-v1.5";
        private const string DEFAULT_ANSWER_MODEL = "google/gemma-4-12b";

        //Nothing but the notes, the question's language, short, every statement with its note: the answer is a pointer
        //into the journal, and a pointer that guesses is worse than none
        private const string ANSWER_INSTRUCTIONS =
            "You answer questions about the BS3D game project from the numbered notes you are given, and from nothing else. " +
            "Answer in the language of the question, plainly, in at most five sentences. After each statement put the number " +
            "of the note it comes from in square brackets, like [2]. If the notes do not contain the answer, say so in one " +
            "sentence and do not guess.";

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
            EmbeddingCache docsCache = options.Docs ? EmbeddingCache.Load(options.Model, "docs") : null;

            try
            {
                float[] query = await client.EmbedQuery(Clip(queryText));

                //The whole corpus goes through the cache, not only the pool ranked below, so a filtered run (--open,
                //--issue) does not drop the vectors the next unfiltered one needs
                (int embedded, int cached) = await client.EmbedDocuments(issues, issueCache);
                List<Doc> pool = issues.Where(d => d.Number != exclude && (!options.OpenOnly || d.Open)).ToList();

                Console.WriteLine($"[search] {options.Model}: {pool.Count} issues ranked, {embedded} embedded now, {cached} from cache");
                Console.WriteLine($"[search] query: {queryLabel}");
                List<(Doc doc, float score)> ranked = Rank(pool, query).ToList();
                foreach ((Doc doc, float score) in ranked.Take(options.Top))
                    Console.WriteLine($"  {score:F3}  #{doc.Number,-4} {(doc.Open ? "open  " : "closed")}  {Trim(doc.Title, 110)}");
                ReportMark("issues", ranked, options.Mark);

                //What --ask reads: the corpora asked for, the issues only when no other was (#494)
                var context = new List<(Doc doc, float score)>();
                if (!options.Journal && !options.Docs) context.AddRange(ranked);

                if (options.Journal)
                {
                    List<Doc> chunks = ChunkJournal(repo);
                    (embedded, cached) = await client.EmbedDocuments(chunks, journalCache);

                    Console.WriteLine();
                    Console.WriteLine($"[search] journal: {chunks.Count} chunks, {embedded} embedded now, {cached} from cache");
                    if (options.Model == NOMIC_MODEL)
                        Console.WriteLine($"[search] nomic is English-centric: on the six Czech journal questions it ranked the answer 1st to 273rd against {DEFAULT_MODEL}'s 2nd to 49th (#439). Ask in English, or use the default model.");
                    ranked = Rank(chunks, query).ToList();
                    foreach ((Doc doc, float score) in ranked.Take(options.Top))
                        Console.WriteLine($"  {score:F3}  {doc.Title}");
                    ReportMark("journal", ranked, options.Mark);
                    context.AddRange(ranked);
                }

                if (options.Docs)
                {
                    List<Doc> chunks = ChunkDocs(repo);
                    (embedded, cached) = await client.EmbedDocuments(chunks, docsCache);

                    //A section of these documents runs to pages, so a hit says where in it the piece starts
                    Console.WriteLine();
                    Console.WriteLine($"[search] docs: {chunks.Count} chunks, {embedded} embedded now, {cached} from cache");
                    ranked = Rank(chunks, query).ToList();
                    foreach ((Doc doc, float score) in ranked.Take(options.Top))
                    {
                        Console.WriteLine($"  {score:F3}  {doc.Title}");
                        Console.WriteLine($"         {Trim(Opening(doc.Text), 120)}");
                    }
                    ReportMark("docs", ranked, options.Mark);
                    context.AddRange(ranked);
                }

                if (options.Ask)
                    await Answer(options, queryText, context.OrderByDescending(x => x.score).Take(options.Top).Select(x => x.doc).ToList());
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
                docsCache?.Save();
            }

            return 0;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("SemanticSearch: GitHub issues (and journal entries, and sections of the documents) that mean the same as a text, via a local embedding model in LM Studio.");
            Console.WriteLine();
            Console.WriteLine("  dotnet run --project Tools\\SemanticSearch -- \"text to look for\"");
            Console.WriteLine("  dotnet run --project Tools\\SemanticSearch -- --issue 425        the issues most like #425");
            Console.WriteLine("  dotnet run --project Tools\\SemanticSearch -- --file draft.md    a drafted issue, before it is filed");
            Console.WriteLine();
            Console.WriteLine("  --open           open issues only");
            Console.WriteLine("  --journal        also search docs/agent-notes.md and docs/agent-notes-archive/");
            Console.WriteLine("  --docs           also search docs/*.md, CLAUDE.md and BestPractices.md, section by section");
            Console.WriteLine("  --top N          results per list (8)");
            Console.WriteLine("  --mark TEXT      also report the rank of the first result whose text contains TEXT (a known answer's marker)");
            Console.WriteLine("  --ask            answer the question from the results shown, through a local chat model; a lead, never a source");
            Console.WriteLine($"  --answer-model KEY  the chat model --ask uses ({DEFAULT_ANSWER_MODEL})");
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

        /// <summary>How many issues <c>gh issue list</c> is asked for; a corpus that reaches it says so.</summary>
        private const int ISSUE_LIMIT = 5000;

        private static List<Doc> LoadIssues(string repo)
        {
            var start = new ProcessStartInfo("gh", $"issue list --state all --limit {ISSUE_LIMIT} --json number,title,body,state")
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

            //gh stops at --limit without a word; a corpus that reached it is missing its oldest issues
            if (doc.RootElement.GetArrayLength() >= ISSUE_LIMIT)
                Console.Error.WriteLine($"[search] warning: gh returned the --limit of {ISSUE_LIMIT} issues; older ones are missing - raise ISSUE_LIMIT");
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
        /// at paragraph breaks. Every piece carries its entry's heading, which is what dates it and says whose it is. The
        /// <c>### </c> headings inside an entry are not cuts: an entry is one agent's one sitting, and that is the unit.
        /// </summary>
        private static List<Doc> ChunkJournal(string repo)
        {
            var files = new List<string> { Path.Combine(repo, "docs", "agent-notes.md") };
            string archive = Path.Combine(repo, "docs", "agent-notes-archive");
            if (Directory.Exists(archive)) files.AddRange(Directory.GetFiles(archive, "*.md").OrderBy(f => f));

            var chunks = new List<Doc>();
            foreach (string file in files) ChunkMarkdown(file, subsections: false, "(before the first entry)", chunks);
            return chunks;
        }

        /// <summary>
        /// The documents cut at their <c>## </c> and <c>### </c> headings — a subsection is labelled under its section, so
        /// a hit names the place to open — and then into pieces exactly as the journal is. The documents are the ones in
        /// <c>docs/</c> that are not the journal, plus <c>CLAUDE.md</c> and <c>BestPractices.md</c>: what CLAUDE.md tells
        /// an agent to read before working on an area, which is the reading this corpus exists to shorten.
        /// </summary>
        private static List<Doc> ChunkDocs(string repo)
        {
            var files = new List<string> { Path.Combine(repo, "CLAUDE.md"), Path.Combine(repo, "BestPractices.md") };
            files.AddRange(Directory.GetFiles(Path.Combine(repo, "docs"), "*.md")
                .Where(f => !Path.GetFileName(f).StartsWith("agent-notes")).OrderBy(f => f));

            var chunks = new List<Doc>();
            foreach (string file in files) ChunkMarkdown(file, subsections: true, "(before the first heading)", chunks);
            return chunks;
        }

        /// <summary>
        /// One Markdown file into <see cref="Doc"/>s: cut at its <c>## </c> headings (and at <c>### </c> when
        /// <paramref name="subsections"/>, labelled "section › subsection"), each stretch then at paragraph breaks into
        /// pieces under <see cref="MAX_CHARS"/>, and a paragraph longer than a piece at its sentence ends — this
        /// project's documents run to paragraphs of several thousand characters, and clipping one would index only its
        /// opening. Every piece's text opens with its heading, and its label says file, heading and part.
        /// </summary>
        private static void ChunkMarkdown(string file, bool subsections, string opening, List<Doc> chunks)
        {
            string name = Path.GetFileName(file);
            string section = opening, subsection = null;
            var stretch = new StringBuilder();

            void Flush()
            {
                string text = stretch.ToString();
                stretch.Clear();
                if (text.Trim().Length == 0) return;

                string heading = subsection == null ? section : $"{section} › {subsection}";
                List<string> pieces = Pieces(text, MAX_CHARS - 200);
                for (int k = 0; k < pieces.Count; k++)
                    chunks.Add(new Doc { Title = $"{name} | {heading} | part {k + 1}/{pieces.Count}", Text = heading + "\n" + pieces[k] });
            }

            foreach (string line in File.ReadLines(file))
            {
                if (line.StartsWith("## ")) { Flush(); section = line[3..].Trim(); subsection = null; continue; }
                if (subsections && line.StartsWith("### ")) { Flush(); subsection = line[4..].Trim(); continue; }
                stretch.Append(line).Append('\n');
            }
            Flush();
        }

        //Paragraphs packed into pieces of at most `room` characters, a paragraph longer than that cut first
        private static List<string> Pieces(string stretch, int room)
        {
            var pieces = new List<string>();
            var piece = new StringBuilder();
            foreach (string paragraph in stretch.Split("\n\n"))
                foreach (string p in Sentences(paragraph, room))
                {
                    if (piece.Length > 0 && piece.Length + p.Length + 2 > room) { pieces.Add(piece.ToString()); piece.Clear(); }
                    piece.Append(p).Append("\n\n");
                }
            if (piece.Length > 0) pieces.Add(piece.ToString());
            return pieces;
        }

        //A paragraph in runs of at most `room` characters, cut at the last sentence end that fits — at a word when the
        //first half has no sentence end, and mid-word only when it has no space either
        private static IEnumerable<string> Sentences(string paragraph, int room)
        {
            while (paragraph.Length > room)
            {
                int cut = paragraph.LastIndexOf(". ", room - 1, StringComparison.Ordinal);
                if (cut < room / 2) cut = paragraph.LastIndexOf(' ', room - 1);
                if (cut < room / 2) cut = room - 1;
                yield return paragraph[..(cut + 1)].TrimEnd();
                paragraph = paragraph[(cut + 1)..].TrimStart();
            }
            if (paragraph.Length > 0) yield return paragraph;
        }

        //A piece's text after its heading line, on one line: what the section says at that point
        private static string Opening(string text)
        {
            int newline = text.IndexOf('\n');
            string body = newline < 0 ? text : text[(newline + 1)..];
            return string.Join(' ', body.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        /// The top-ranked notes handed to a local chat model with the question (#494): a short answer that names the
        /// notes it came from, so the reader jumps to the entry instead of reading the file. The notes it reads are
        /// exactly the results printed above it, so what it could and could not have known is on the screen. <b>The
        /// answer is a lead and never a source</b> — what a document cites is the entry, not the model.
        /// </summary>
        private static async Task Answer(Options options, string question, List<Doc> notes)
        {
            var prompt = new StringBuilder();
            prompt.Append("Question: ").Append(question.Trim()).Append("\n\nNotes:\n");
            for (int i = 0; i < notes.Count; i++)
                prompt.Append('[').Append(i + 1).Append("] ").Append(Label(notes[i])).Append('\n').Append(notes[i].Text.Trim()).Append("\n\n");

            //reasoning_effort "none" turns a thinking model's thinking off: measured ~20x slower and no better on this
            //project's questions (the local-ai skill), and with a small max_tokens it comes back as an empty answer
            string body = JsonSerializer.Serialize(new
            {
                model = options.AnswerModel,
                temperature = 0,
                max_tokens = 600,
                reasoning_effort = "none",
                messages = new object[]
                {
                    new { role = "system", content = ANSWER_INSTRUCTIONS },
                    new { role = "user", content = prompt.ToString() }
                }
            });

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            var sw = Stopwatch.StartNew();
            HttpResponseMessage response;
            try
            {
                response = await http.PostAsync(options.Endpoint + "/chat/completions", new StringContent(body, Encoding.UTF8, "application/json"));
            }
            catch (HttpRequestException e)
            {
                throw new EmbeddingException($"[ask] LM Studio is not answering at {options.Endpoint} ({e.Message})");
            }
            catch (TaskCanceledException)
            {
                //HttpClient reports its own timeout as a cancellation, which used to end the run in a stack trace
                throw new EmbeddingException($"[ask] LM Studio did not answer within {http.Timeout.TotalMinutes:0} minutes at {options.Endpoint}");
            }
            string json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new EmbeddingException($"[ask] LM Studio refused the question ({(int)response.StatusCode}): {json.Trim()}\n[ask] is the model loaded? lms load {options.AnswerModel}");

            using JsonDocument doc = JsonDocument.Parse(json);
            string answer = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";
            int promptTokens = doc.RootElement.TryGetProperty("usage", out JsonElement usage) && usage.TryGetProperty("prompt_tokens", out JsonElement pt) ? pt.GetInt32() : 0;

            Console.WriteLine();
            Console.WriteLine($"[ask] {options.AnswerModel} over {notes.Count} notes ({promptTokens} prompt tokens), {sw.Elapsed.TotalSeconds:F1} s:");
            Console.WriteLine(answer);

            //"[8]" and "[3, 8]" alike: the model groups its citations when two notes say the same thing
            List<int> cited = Regex.Matches(answer, @"\[(\d+(?:\s*,\s*\d+)*)\]")
                .SelectMany(m => m.Groups[1].Value.Split(',')).Select(s => int.Parse(s.Trim()))
                .Where(n => n >= 1 && n <= notes.Count).Distinct().OrderBy(n => n).ToList();
            foreach (int n in cited) Console.WriteLine($"  [{n}] {Label(notes[n - 1])}");

            if (options.Mark != null)
            {
                int carrying = notes.FindIndex(d => d.Text.Contains(options.Mark, StringComparison.Ordinal)) + 1;
                Console.WriteLine(carrying == 0 ? $"[mark] ask: no note handed to the model carries \"{options.Mark}\""
                    : cited.Contains(carrying) ? $"[mark] ask: note [{carrying}] carries \"{options.Mark}\" and was cited"
                    : $"[mark] ask: note [{carrying}] carries \"{options.Mark}\" and was NOT cited");
            }
        }

        private static string Label(Doc doc) => doc.Number > 0 ? $"#{doc.Number} {doc.Title}" : doc.Title;

        private static IEnumerable<(Doc doc, float score)> Rank(IEnumerable<Doc> docs, float[] query) =>
            docs.Select(d => (d, Dot(d.Vector, query))).OrderByDescending(x => x.Item2);

        /// <summary>
        /// The measurement behind every verdict in this tool's documentation, made repeatable: where in the ranking the
        /// first text carrying <paramref name="mark"/> sits — an issue number, a class name, a marker known to be in the
        /// entry that answers the question — so a model or a chunking can be compared on the same questions later.
        /// </summary>
        private static void ReportMark(string corpus, List<(Doc doc, float score)> ranked, string mark)
        {
            if (mark == null) return;

            int first = 0, carrying = 0;
            string label = null;
            for (int i = 0; i < ranked.Count; i++)
            {
                Doc doc = ranked[i].doc;
                if (!doc.Text.Contains(mark, StringComparison.Ordinal)) continue;
                carrying++;
                if (first == 0) { first = i + 1; label = doc.Number > 0 ? $"#{doc.Number} {Trim(doc.Title, 70)}" : doc.Title; }
            }
            Console.WriteLine(first == 0
                ? $"[mark] {corpus}: \"{mark}\" is in none of the {ranked.Count} texts"
                : $"[mark] {corpus}: \"{mark}\" first at rank {first} of {ranked.Count} ({carrying} carry it) — {label}");
        }

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
        public bool Docs;
        public bool OpenOnly;
        public string Mark;
        public bool Ask;
        public string AnswerModel = "google/gemma-4-12b";
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
                    case "--docs": options.Docs = true; break;
                    case "--mark":
                        if (++i >= args.Length) return null;
                        options.Mark = args[i];
                        break;
                    case "--ask": options.Ask = true; break;
                    case "--answer-model":
                        if (++i >= args.Length) return null;
                        options.AnswerModel = args[i];
                        break;
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
    /// <c>query:</c>, Qwen3-Embedding with an instruction on the query alone — and a family this does not know gets none.
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

            //Qwen3-Embedding takes an instruction on the QUERY side only ("Instruct: <task>\nQuery: <text>") and the
            //documents bare; without it the model is being compared on a footing it was not trained for (#439)
            string family = model.ToLowerInvariant();
            (_documentPrefix, _queryPrefix) = family.Contains("nomic") ? ("search_document: ", "search_query: ")
                : family.Contains("e5") ? ("passage: ", "query: ")
                : family.Contains("qwen3-embedding") ? ("", "Instruct: Given a question, retrieve the notes and issues that answer it\nQuery: ")
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
            catch (TaskCanceledException)
            {
                //HttpClient reports its own timeout as a cancellation, which used to end the run in a stack trace
                throw new EmbeddingException($"[search] LM Studio did not answer within {_http.Timeout.TotalMinutes:0} minutes at {_endpoint}");
            }

            string json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new EmbeddingException($"[search] LM Studio refused the request ({(int)response.StatusCode}): {json.Trim()}\n[search] is the model loaded? lms load {_model}");

            var vectors = new float[texts.Count][];
            using JsonDocument doc = JsonDocument.Parse(json);
            foreach (JsonElement item in doc.RootElement.GetProperty("data").EnumerateArray())
                vectors[item.GetProperty("index").GetInt32()] = Normalize(item.GetProperty("embedding").EnumerateArray().Select(x => x.GetSingle()).ToArray());

            //A reply short of what was asked left a null vector here that only threw later, inside Rank, far from the
            //request that caused it
            int missing = Array.IndexOf(vectors, null);
            if (missing >= 0)
                throw new EmbeddingException($"[search] LM Studio answered {doc.RootElement.GetProperty("data").GetArrayLength()} embeddings for {texts.Count} texts (the first missing is #{missing})");

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
