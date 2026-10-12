using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace BS3D.Tools.DocDrift
{
    /// <summary>
    /// The second sweep of this tool (<c>--citations</c>, #400's ninth pass): every section title the code and the
    /// documents cite beside a document's path — <c>"Sun shadows" in `docs/rendering.md`</c>, the form the repository's
    /// comments use — must still name a heading of that document, a heading of one of its sub-documents
    /// (<c>docs/&lt;area&gt;/*.md</c> under an index since #839), or a row of the index's "Where the old sections went"
    /// table. The docs diet renamed and moved sections by the dozen, and a citation that resolves to nothing sends a
    /// reader to a title that no longer exists; passes 8 and 9 each found such citations with a script that lived in
    /// a session's scratchpad, which is why it is here now.
    /// <para>
    /// Like the number sweep, never a gate: it exits 0 and prints what did not resolve for a person to fix. A title is
    /// matched loosely — case apart, a trailing issue number apart, backticks and emphasis apart, and a heading that
    /// begins with or contains the cited title counts — because citations shorten titles on purpose. The journal and
    /// its archive are left out: their citations are history.
    /// </para>
    /// <para>
    /// <c>--selftest</c> is #10's "seen to fire": a citation of a section no document has is planted in memory and must
    /// come back unresolved, and a real one must resolve, before the sweep's silence means anything.
    /// </para>
    /// </summary>
    internal static class Citations
    {
        //"<Title>" in docs/<file>.md — straight or typographic quotes, the path with or without backticks (written with
        //the angle brackets here so this comment is not itself a citation the sweep reads)
        private static readonly Regex CiteRx = new(
            "[\"“]([^\"”\\n]{3,90})[\"”]\\s+in\\s+`?(docs/[\\w./-]+\\.md)`?", RegexOptions.Compiled);

        private static readonly Regex HeadingRx = new(@"^#{1,6}\s+(.+?)\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex TableRowRx = new(@"^\|\s*([^|]+?)\s*\|", RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly string[] SKIPPED_DIRS = { ".git", "bin", "obj", ".claude", "Research", "node_modules" };

        internal static int Run(string root, bool selfTest)
        {
            var titles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var problems = new List<string>();
            int count = 0;

            foreach (string path in SourceFiles(root))
            {
                string rel = Rel(root, path);
                if (rel.StartsWith("docs/agent-notes", StringComparison.OrdinalIgnoreCase)) continue;

                string text = File.ReadAllText(path);
                foreach (Match m in CiteRx.Matches(text))
                {
                    count++;
                    string title = m.Groups[1].Value, doc = m.Groups[2].Value;
                    int line = text.AsSpan(0, m.Index).Count('\n') + 1;

                    if (!titles.TryGetValue(doc, out HashSet<string> known))
                        titles[doc] = known = TitlesOf(root, doc);

                    if (known == null)
                        problems.Add($"{rel}:{line}  \"{title}\" in {doc} - no such document");
                    else if (!Resolves(title, known))
                        problems.Add($"{rel}:{line}  \"{title}\" in {doc} - no such heading or table row");
                }
            }

            if (selfTest)
            {
                HashSet<string> rendering = TitlesOf(root, "docs/rendering.md");
                bool planted = rendering != null && !Resolves("No Such Section Anywhere", rendering);
                bool real = rendering != null && Resolves("Sun shadows", rendering);
                Console.WriteLine(planted && real
                    ? "[cite] selftest ok: a planted citation does not resolve, a real one does"
                    : "[cite] SELFTEST FAILED: the check cannot tell a planted citation from a real one");
            }

            Console.WriteLine($"[cite] {count} citations of a section beside a document's path, {problems.Count} unresolved");
            foreach (string p in problems) Console.WriteLine("  " + p);
            Console.WriteLine("[cite] a list for a person to fix, not a verdict: this tool always exits 0");
            return 0;
        }

        /// <summary>
        /// The titles a citation of <paramref name="doc"/> may name: its headings, its sub-documents' headings when it is
        /// an index with a folder of its own name, and the first cell of every table row (the index's map of old titles).
        /// Null for a document that does not exist.
        /// </summary>
        private static HashSet<string> TitlesOf(string root, string doc)
        {
            string path = Path.Combine(root, doc.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return null;

            var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string text = File.ReadAllText(path);
            foreach (Match h in HeadingRx.Matches(text)) titles.Add(Norm(h.Groups[1].Value));
            foreach (Match r in TableRowRx.Matches(text)) titles.Add(Norm(r.Groups[1].Value));

            string area = Path.Combine(Path.GetDirectoryName(path) ?? root, Path.GetFileNameWithoutExtension(path));
            if (Directory.Exists(area))
                foreach (string sub in Directory.GetFiles(area, "*.md"))
                    foreach (Match h in HeadingRx.Matches(File.ReadAllText(sub)))
                        titles.Add(Norm(h.Groups[1].Value));

            return titles;
        }

        private static bool Resolves(string title, HashSet<string> titles)
        {
            string wanted = Norm(title);
            if (wanted.Length == 0) return false;
            if (titles.Contains(wanted)) return true;

            foreach (string t in titles)
                if (t.StartsWith(wanted, StringComparison.OrdinalIgnoreCase) || t.Contains(wanted, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        /// <summary>A title for comparison: no trailing issue numbers, no backticks or emphasis, one space between words, no final dot.</summary>
        private static string Norm(string s)
        {
            s = Regex.Replace(s, @"\s*\((?:#\d+[,\s]*)+\)\s*$", "");
            s = Regex.Replace(s, @"\s*\(#\d+\)", "");
            s = s.Replace('’', '\'').Replace('“', '"').Replace('”', '"');
            s = Regex.Replace(s, "[`*_]", "");
            s = Regex.Replace(s, @"\s+", " ").Trim().TrimEnd('.');
            return s.ToLowerInvariant();
        }

        private static IEnumerable<string> SourceFiles(string root)
        {
            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                string dir = stack.Pop();
                foreach (string sub in Directory.GetDirectories(dir))
                    if (!SKIPPED_DIRS.Contains(Path.GetFileName(sub), StringComparer.OrdinalIgnoreCase)) stack.Push(sub);

                foreach (string file in Directory.GetFiles(dir))
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext is ".cs" or ".md" or ".fx" or ".fxh") yield return file;
                }
            }
        }

        private static string Rel(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
    }
}
