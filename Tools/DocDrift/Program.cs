using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace BS3D.Tools.DocDrift
{
    /// <summary>
    /// A hand-run triage sweep for numbers the documents quote beside a name that the code defines with a
    /// different value (#525). It pulls every named numeric out of the tree — a C# <c>const</c>, a config
    /// property's initialiser (<c>{ get; set; } = n;</c>), a <c>static readonly</c> field, a shader
    /// <c>static const</c> — keeps the names defined exactly once, finds the lines in <c>docs/*.md</c> and
    /// <c>CLAUDE.md</c> that quote such a name in backticks with a number close after it, and prints the pairs
    /// that disagree as candidates with file:line.
    ///
    /// It is deliberately NOT a gate and always exits 0: the documents record on purpose what a value used to
    /// be and what was tried and dropped, the same figure appears in other units, ranges and half-extents are
    /// quoted beside a single constant, and a long paragraph carries numbers that have nothing to do with the
    /// name beside them. The filters below remove most of that, never all of it — so the output is a short
    /// list for a person to read, and each line is a candidate, not a finding. The journal
    /// (<c>docs/agent-notes.md</c>) and its archive are left out outright: every entry is history by
    /// construction, and it is Czech with a decimal comma.
    /// </summary>
    internal static class Program
    {
        /// <summary>How far after a quoted name (in characters) the number it is paired with may start.</summary>
        private const int WINDOW = 50;

        /// <summary>Extra reach per additional name in a list such as <c>`A`/`B`/`C` (0.2 / 0.12 / 0.1)</c>.</summary>
        private const int WINDOW_PER_EXTRA_NAME = 12;

        /// <summary>A gap between two quoted names at most this long, made of separators only, joins them into one list.</summary>
        private const int MAX_LIST_GAP = 7;

        /// <summary>Relative slack accepted for a figure marked approximate (<c>~</c>, <c>about</c>, …).</summary>
        private const double APPROX_SLACK = 0.1;

        private const string NUM_LIT = @"[-+]?(?:\d[\d_]*(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?[fFdDmMuUlLhH]*";

        //A C# const, a shader (static) const: numeric scalar types only, a plain literal only
        private static readonly Regex ConstRx = new(
            @"\bconst\s+(?:float|double|decimal|int|uint|long|ulong|short|ushort|byte|sbyte|half)\s+(\w+)\s*=\s*(" + NUM_LIT + @")\s*;",
            RegexOptions.Compiled);

        //A config property with an initialiser: public float X { get; set; } = 0.3f;
        private static readonly Regex PropRx = new(
            @"\b(?:float|double|int)\s+(\w+)\s*\{\s*get;\s*(?:set|init);\s*\}\s*=\s*(" + NUM_LIT + @")\s*;",
            RegexOptions.Compiled);

        private static readonly Regex ReadonlyRx = new(
            @"\bstatic\s+readonly\s+(?:float|double|int)\s+(\w+)\s*=\s*(" + NUM_LIT + @")\s*;",
            RegexOptions.Compiled);

        private static readonly Regex SpanRx = new("`([^`\n]+)`", RegexOptions.Compiled);

        private static readonly Regex IdentSpanRx = new(
            @"^(?:[A-Za-z_]\w*\.)*([A-Za-z_]\w*)(?:\(\))?(?:\s*=\s*(" + NUM_LIT + @"))?$", RegexOptions.Compiled);

        private static readonly Regex NumericSpanRx = new(@"^[-+−±~]?\s*[\d.,]+[fF]?\s*%?$", RegexOptions.Compiled);

        //A number in prose. Not part of a word or identifier (4K, x64, Block01), not an issue number (#505),
        //not the tail of a decimal or a date, not a multiplier (×5) nor a compound word (8-ball); a leading minus
        //only where it cannot be a range dash.
        private static readonly Regex NumberRx = new(
            @"(?<![\w.#×])(?<sign>[-−](?=\d))?(?<num>\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?|\.\d+)(?:[eE][-+]?\d+)?(?:f)?(?![\w]|\.\d|,\d{3}|-[A-Za-z])",
            RegexOptions.Compiled);

        private static readonly Regex DateRx = new(@"\b\d{4}-\d{2}-\d{2}\b", RegexOptions.Compiled);

        //The words that mark a sentence as history (or as a road not taken) rather than as a statement of the
        //current value (#525). An issue number counts: a sentence citing one is usually telling what it changed.
        //Not a bare "read as" nor "rather than": both are present tense here as often as not, and together
        //they hid the storm's `PuffOpacity` (0.40 against 0.30) — "what makes the scene read as cloud rather
        //than as a ceiling" — one of the three drifts this tool has to find.
        private static readonly Regex HistoryRx = new(
            @"\b(?:was|were|until|instead of|would have|before|no longer|used to|it read as|reads as|had been|" +
            @"formerly|previously|originally|dropped from|raised from|lowered from)\b|#\d{2,4}\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        //Words that may stand between a name and its figure ("`X` is set to 0.3", "`X` of about 0.3"). Any
        //more than MAX_OTHER_WORDS other words between them and the figure is about something else — "at
        //`SHOOT_SPEED` the shot crosses 1.667 units per step" is a consequence of the constant, not its value.
        private static readonly HashSet<string> Connectors = new(StringComparer.OrdinalIgnoreCase)
        {
            "is", "are", "at", "of", "to", "set", "stays", "sits", "kept", "now", "by", "from", "default", "defaults",
            "equals", "about", "around", "roughly", "approximately", "nearly", "almost", "only", "just", "and", "the", "a", "its",
        };

        private const int MAX_OTHER_WORDS = 1;

        private static readonly Regex ApproxBeforeRx = new(@"(?:~|≈|\babout|\baround|\broughly|\bapproximately|\bnearly|\balmost)\s*\**\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ArrowAfterRx = new(@"^[\s*_]*(?:→|->|⇒)[\s*_]*", RegexOptions.Compiled);
        private static readonly Regex RangeAfterRx = new(@"^[\s*_]*(?:–|—|-|\bto\b|\.\.)[\s*_]*(?=[-−]?\d|\.\d)", RegexOptions.Compiled);

        private sealed record Def(string Name, double Value, string Literal, string File, int Line);

        private sealed record Candidate(string DocFile, int DocLine, string Name, string DocFigure, Def Def, string Context, string Reason);

        private sealed class Token
        {
            public int Start, End;
            public double Value;
            public int Decimals;
            public string Text;
            public bool Half, Approx, Percent, Degrees, Millis;
            public Token RangeEnd;
            public Token RangeStart;  //set on a list's flattened range ends: `A`/`B` 30–72 pairs A with 30 inside a range
            public Token ArrowNext;
        }

        private sealed class Span
        {
            public int Start, End;       //including the backticks
            public string Name;          //last segment of a dotted identifier, null for a non-identifier span
            public string InlineNumber;  //`NAME = 0.3`
        }

        private static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            string root = null;
            bool showAll = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--root" when i + 1 < args.Length: root = args[++i]; break;
                    case "--all": showAll = true; break;
                    case "-h":
                    case "--help":
                        Usage();
                        return 0;
                    default:
                        Console.WriteLine($"[drift] unknown argument '{args[i]}'");
                        Usage();
                        return 0;
                }
            }

            root ??= FindRepo();
            if (root == null || !Directory.Exists(Path.Combine(root, "docs")))
            {
                Console.WriteLine("[drift] no directory holding Game.sln and docs/ above this tool; pass --root <repository>");
                return 0;
            }
            root = Path.GetFullPath(root);

            Dictionary<string, List<Def>> defs = CollectDefinitions(root, out int filesRead);
            Dictionary<string, Def> unique = defs.Where(p => p.Value.Count == 1).ToDictionary(p => p.Key, p => p.Value[0]);
            Console.WriteLine($"[drift] {defs.Values.Sum(l => l.Count)} named numerics in {filesRead} source files, {unique.Count} names defined exactly once");

            List<string> docs = Directory.GetFiles(Path.Combine(root, "docs"), "*.md", SearchOption.TopDirectoryOnly)
                .Where(f => !Path.GetFileName(f).Equals("agent-notes.md", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
            string claude = Path.Combine(root, "CLAUDE.md");
            if (File.Exists(claude)) docs.Insert(0, claude);

            var stats = new Stats();
            var candidates = new List<Candidate>();
            var filtered = new List<Candidate>();
            foreach (string doc in docs)
            {
                string rel = Rel(root, doc);
                string[] lines = File.ReadAllLines(doc);
                for (int n = 0; n < lines.Length; n++)
                    ScanLine(lines[n], rel, n + 1, unique, stats, candidates, filtered);
            }

            Console.WriteLine($"[drift] {docs.Count} documents: {stats.Quotes} quotes of a name with a figure beside it, {stats.Agree} agree, " +
                              $"{filtered.Count} disagree in a history sentence (filtered), {stats.AmbiguousLists} name lists without a figure for every name (skipped)");
            Console.WriteLine();

            Print(candidates, "CANDIDATE");
            if (showAll) Print(filtered, "HISTORY");

            Console.WriteLine();
            Console.WriteLine($"[drift] {candidates.Count} candidate(s) to read" + (showAll ? "" : $" ({filtered.Count} more in history sentences: --all shows them)") +
                              ". A triage list, not a verdict: this tool always exits 0.");
            return 0;
        }

        private sealed class Stats
        {
            public int Quotes, Agree, AmbiguousLists;
        }

        private static void Usage()
        {
            Console.WriteLine("DocDrift — the numbers the documents quote beside a name the code defines differently (#525)");
            Console.WriteLine();
            Console.WriteLine("  dotnet run --project Tools/DocDrift [-- --all] [-- --root <repository>]");
            Console.WriteLine();
            Console.WriteLine("  --all          also print the disagreements the history filter removed, with their sentence");
            Console.WriteLine("  --root DIR     the repository to read (default: found above the tool by Game.sln + docs/)");
            Console.WriteLine();
            Console.WriteLine("Always exits 0: it reports candidates for a person to read, it is not a gate.");
        }

        private static void Print(List<Candidate> list, string label)
        {
            foreach (Candidate c in list.OrderBy(c => c.DocFile, StringComparer.Ordinal).ThenBy(c => c.DocLine))
            {
                Console.WriteLine($"{c.DocFile}:{c.DocLine}  {label}  {c.Name}: doc {c.DocFigure}, code {c.Def.Literal}  ({c.Def.File}:{c.Def.Line})");
                if (c.Reason != null) Console.WriteLine($"    filtered: {c.Reason}");
                Console.WriteLine($"    …{c.Context}…");
            }
        }

        // ---------------------------------------------------------------- the code's side

        private static Dictionary<string, List<Def>> CollectDefinitions(string root, out int filesRead)
        {
            var defs = new Dictionary<string, List<Def>>(StringComparer.Ordinal);
            filesRead = 0;
            foreach (string file in EnumerateSources(root))
            {
                filesRead++;
                string rel = Rel(root, file);
                string[] lines = File.ReadAllLines(file);
                for (int n = 0; n < lines.Length; n++)
                {
                    string line = lines[n];
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) continue;
                    foreach (Regex rx in new[] { ConstRx, PropRx, ReadonlyRx })
                    {
                        foreach (Match m in rx.Matches(line))
                        {
                            if (!TryParseLiteral(m.Groups[2].Value, out double v)) continue;
                            string name = m.Groups[1].Value;
                            if (!defs.TryGetValue(name, out List<Def> list)) defs[name] = list = new List<Def>();
                            list.Add(new Def(name, v, m.Groups[2].Value, rel, n + 1));
                        }
                    }
                }
            }
            return defs;
        }

        private static readonly HashSet<string> SkippedDirs = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj", ".git", ".vs", ".claude", "node_modules" };

        private static IEnumerable<string> EnumerateSources(string root)
        {
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                string dir = stack.Pop();
                foreach (string sub in Directory.GetDirectories(dir))
                    if (!SkippedDirs.Contains(Path.GetFileName(sub))) stack.Push(sub);
                foreach (string f in Directory.GetFiles(dir))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext is ".cs" or ".fx" or ".fxh" or ".hlsl") yield return f;
                }
            }
        }

        private static bool TryParseLiteral(string literal, out double value)
        {
            string s = literal.Replace("_", "").TrimEnd('f', 'F', 'd', 'D', 'm', 'M', 'u', 'U', 'l', 'L', 'h', 'H');
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        // ---------------------------------------------------------------- the documents' side

        private static void ScanLine(string line, string docFile, int lineNo, Dictionary<string, Def> unique, Stats stats,
                                     List<Candidate> candidates, List<Candidate> filtered)
        {
            if (line.IndexOf('`') < 0) return;

            //A date is not a range of two numbers
            string text = DateRx.Replace(line, m => new string(' ', m.Length));

            //The backtick spans: identifiers are the names; numeric spans are figures; anything else (an
            //expression, a path, a command line) is masked so its digits are never read as a figure
            var spans = new List<Span>();
            char[] masked = text.ToCharArray();
            foreach (Match m in SpanRx.Matches(text))
            {
                string content = m.Groups[1].Value.Trim();
                Match id = IdentSpanRx.Match(content);
                bool numeric = NumericSpanRx.IsMatch(content);
                masked[m.Index] = ' ';
                masked[m.Index + m.Length - 1] = ' ';
                if (id.Success)
                {
                    spans.Add(new Span
                    {
                        Start = m.Index, End = m.Index + m.Length, Name = id.Groups[1].Value,
                        InlineNumber = id.Groups[2].Success ? id.Groups[2].Value : null
                    });
                    for (int i = m.Index; i < m.Index + m.Length; i++) masked[i] = ' ';
                }
                else if (!numeric)
                {
                    for (int i = m.Index; i < m.Index + m.Length; i++) masked[i] = ' ';
                }
            }
            if (spans.Count == 0) return;
            string maskedText = new(masked);

            //Consecutive names joined only by separators are one list: `A`/`B`/`C` (0.2 / 0.12 / 0.1)
            var groups = new List<List<Span>>();
            foreach (Span s in spans)
            {
                List<Span> last = groups.Count > 0 ? groups[^1] : null;
                if (last != null && last[^1].InlineNumber == null && s.InlineNumber == null && IsListGap(text, last[^1].End, s.Start))
                    last.Add(s);
                else
                    groups.Add(new List<Span> { s });
            }

            for (int g = 0; g < groups.Count; g++)
            {
                List<Span> group = groups[g];
                if (!group.Any(s => unique.ContainsKey(s.Name))) continue;

                if (group.Count == 1 && group[0].InlineNumber != null)
                {
                    Span s = group[0];
                    if (!unique.TryGetValue(s.Name, out Def def)) continue;
                    if (!TryParseLiteral(s.InlineNumber, out double v)) continue;
                    var tok = new Token { Start = s.Start, End = s.End, Value = v, Decimals = DecimalsOf(s.InlineNumber), Text = s.InlineNumber };
                    Judge(text, docFile, lineNo, s, tok, def, stats, candidates, filtered, inList: false);
                    continue;
                }

                int from = group[^1].End;
                int limit = from + WINDOW + WINDOW_PER_EXTRA_NAME * (group.Count - 1);
                int nextSpan = g + 1 < groups.Count ? groups[g + 1][0].Start : text.Length;
                limit = Math.Min(Math.Min(limit, nextSpan), text.Length);
                //A figure in the next sentence is not this name's
                int stop = NextSentenceEnd(maskedText, from);
                if (stop >= 0 && stop < limit) limit = stop;
                List<Token> tokens = Tokens(maskedText, from, limit);
                if (tokens.Count == 0) continue;
                if (OtherWords(maskedText, from, tokens[0].Start) > MAX_OTHER_WORDS) continue;

                if (group.Count == 1)
                {
                    Span s = group[0];
                    if (unique.TryGetValue(s.Name, out Def def))
                        Judge(text, docFile, lineNo, s, tokens[0], def, stats, candidates, filtered, inList: false);
                    continue;
                }

                //The n-th name with the n-th figure; a list with fewer figures than names is ambiguous
                List<Token> flat = Flatten(tokens);
                if (flat.Count < group.Count) { stats.AmbiguousLists++; continue; }
                for (int k = 0; k < group.Count; k++)
                {
                    if (!unique.TryGetValue(group[k].Name, out Def def)) continue;
                    Token t = flat[k];
                    Judge(text, docFile, lineNo, group[k], t, def, stats, candidates, filtered, inList: true);
                }
            }
        }

        private static bool IsListGap(string text, int from, int to)
        {
            if (to - from > MAX_LIST_GAP) return false;
            string gap = text.Substring(from, to - from);
            return Regex.IsMatch(gap, @"^[\s/,&]*(?:and|or)?[\s/,&]*$");
        }

        /// <summary>The figures in [from, limit), each with what stands round it: a range, an arrow, a unit.</summary>
        private static List<Token> Tokens(string masked, int from, int limit)
        {
            var result = new List<Token>();
            int pos = from;
            while (true)
            {
                Token t = NextToken(masked, pos, masked.Length);
                if (t == null || t.Start >= limit) break;
                result.Add(t);
                //A range or an arrow chain is one figure; carry on after its last number
                Token tail = t;
                while (tail.RangeEnd != null || tail.ArrowNext != null) tail = tail.RangeEnd ?? tail.ArrowNext;
                pos = tail.End;
            }
            return result;
        }

        private static List<Token> Flatten(List<Token> tokens)
        {
            var flat = new List<Token>();
            foreach (Token t in tokens)
                for (Token c = t; c != null; c = c.RangeEnd ?? c.ArrowNext)
                {
                    if (t.RangeEnd != null) c.RangeStart = t;
                    flat.Add(c);
                }
            return flat;
        }

        private static int OtherWords(string masked, int from, int to)
        {
            int count = 0;
            foreach (Match w in Regex.Matches(masked.Substring(from, Math.Max(0, to - from)), @"[A-Za-z]+"))
                if (!Connectors.Contains(w.Value)) count++;
            return count;
        }

        private static Token NextToken(string masked, int from, int limit)
        {
            Match m = NumberRx.Match(masked, from);
            if (!m.Success || m.Index >= limit) return null;

            string num = m.Groups["num"].Value;
            if (!double.TryParse(num.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) return null;
            if (m.Groups["sign"].Success) v = -v;
            var t = new Token { Start = m.Index, End = m.Index + m.Length, Value = v, Decimals = DecimalsOf(num), Text = m.Value };

            string before = masked.Substring(Math.Max(0, t.Start - 16), t.Start - Math.Max(0, t.Start - 16));
            string after = masked.Substring(t.End, Math.Min(16, masked.Length - t.End));
            t.Half = Regex.IsMatch(before, @"±\s*\**\s*$");
            t.Approx = ApproxBeforeRx.IsMatch(before);
            t.Percent = Regex.IsMatch(after, @"^\s?%");
            t.Degrees = Regex.IsMatch(after, @"^\s?(?:°|deg\b|degrees?\b)");
            t.Millis = Regex.IsMatch(after, @"^\s?ms\b");

            Match arrow = ArrowAfterRx.Match(after);
            if (arrow.Success)
            {
                Token next = NextToken(masked, t.End + arrow.Length, t.End + arrow.Length + 1);
                if (next != null) t.ArrowNext = next;
            }
            else
            {
                Match range = RangeAfterRx.Match(after);
                if (range.Success)
                {
                    Token end = NextToken(masked, t.End + range.Length, t.End + range.Length + 2);
                    if (end != null) t.RangeEnd = end;
                }
            }
            return t;
        }

        private static int DecimalsOf(string num)
        {
            string s = num.TrimEnd('f', 'F');
            int dot = s.IndexOf('.');
            return dot < 0 ? 0 : s.Length - dot - 1;
        }

        private static void Judge(string text, string docFile, int lineNo, Span name, Token token, Def def, Stats stats,
                                  List<Candidate> candidates, List<Candidate> filtered, bool inList)
        {
            stats.Quotes++;

            //"0.22 → 0.12": the figure the arrow ends on is the current one. In a name list each name has its
            //own figure, so neither an arrow nor a range is followed there; a figure inside a range is accepted
            //anywhere in that range.
            Token current = token;
            Token range = inList ? token.RangeStart : token;
            if (!inList)
                while (current.ArrowNext != null) current = current.ArrowNext;
            if (!inList) range = current;

            bool inRange = range?.RangeEnd != null && InRange(range, range.RangeEnd, def.Value);
            if (Agrees(current, def.Value) || inRange)
            {
                stats.Agree++;
                return;
            }

            string figure = !inList && current.RangeEnd != null ? $"{current.Text}–{current.RangeEnd.Text}" : current.Text;
            string context = Context(text, name.Start, (!inList ? current.RangeEnd?.End : null) ?? current.End);
            string sentence = Sentence(text, name.Start, current.End);
            Match history = HistoryRx.Match(sentence);
            if (history.Success)
                filtered.Add(new Candidate(docFile, lineNo, name.Name, figure, def, context, $"history word \"{history.Value}\""));
            else
                candidates.Add(new Candidate(docFile, lineNo, name.Name, figure, def, context, null));
        }

        private static bool Agrees(Token t, double v)
        {
            double tol = 0.5 * Math.Pow(10, -t.Decimals) * (1 + 1e-9) + 1e-12;
            bool Near(double code) =>
                Math.Abs(code - t.Value) <= tol || (t.Approx && Math.Abs(code - t.Value) <= APPROX_SLACK * Math.Abs(code));

            if (Near(v)) return true;
            if (t.Percent && Near(v * 100)) return true;
            if (t.Degrees && Near(v * 180 / Math.PI)) return true;
            if (t.Millis && Near(v * 1000)) return true;
            if (t.Half && Near(v / 2)) return true;       //MOON_EXTENT 1200 described as ±600
            return false;
        }

        private static bool InRange(Token a, Token b, double v)
        {
            double lo = Math.Min(a.Value, b.Value), hi = Math.Max(a.Value, b.Value);
            double tol = 0.5 * Math.Pow(10, -Math.Max(a.Decimals, b.Decimals)) + 1e-12;
            if (v >= lo - tol && v <= hi + tol) return true;
            if (a.Percent || b.Percent) return v * 100 >= lo - tol && v * 100 <= hi + tol;
            return false;
        }

        /// <summary>The sentence holding the quote, which is what the history filter reads — not the whole line,
        /// since a paragraph here is one line of several thousand characters.</summary>
        private static string Sentence(string text, int from, int to)
        {
            int start = 0;
            foreach (Match m in SentenceEndRx.Matches(text))
            {
                if (m.Index + m.Length <= from) start = m.Index + m.Length;
                else if (m.Index >= to) return text.Substring(start, m.Index + 1 - start);
            }
            return text.Substring(start);
        }

        /// <summary>The first sentence end at or after <paramref name="from"/>, or -1.</summary>
        private static int NextSentenceEnd(string text, int from)
        {
            Match m = SentenceEndRx.Match(text, from);
            return m.Success ? m.Index : -1;
        }

        //A sentence ends at . ! ? or ; followed by space — with any closing bold, italics, quote or bracket
        //between (".** ", ".) ") — so "0.30" is never a sentence end and "low.** Next" is
        private static readonly Regex SentenceEndRx = new(@"[.!?;][*_""'”)\]]*\s", RegexOptions.Compiled);

        private static string Context(string text, int from, int to)
        {
            int start = Math.Max(0, from - 40);
            int end = Math.Min(text.Length, to + 40);
            return text.Substring(start, end - start).Replace('\t', ' ');
        }

        // ---------------------------------------------------------------- plumbing

        private static string Rel(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

        //By landmark rather than by counting "..", as LevelGen, ScoreSim and SemanticSearch do
        private static string FindRepo()
        {
            foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
                for (DirectoryInfo dir = new(start); dir != null; dir = dir.Parent)
                    if (File.Exists(Path.Combine(dir.FullName, "Game.sln")) && Directory.Exists(Path.Combine(dir.FullName, "docs")))
                        return dir.FullName;
            return null;
        }
    }
}
