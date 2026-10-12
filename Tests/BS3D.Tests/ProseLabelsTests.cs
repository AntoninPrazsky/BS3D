using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// Every label that wraps in the Game's pages is written through <c>MenuPage.SetProse</c> (#855). FontStashSharp's own
    /// wrap takes a full stop as a break opportunity as well as a space, and when the glyph that overflows the line is the
    /// space after a sentence, the last opportunity before it is the stop: the line breaks there and the next one starts
    /// with that space. Help's copy laid out raw, as it was before #806, printed " The aim preview shows the bend." in a
    /// 1600×900 window (the owner's sighting); through <c>SpaceWrap</c>, which breaks at spaces only, the same copy gave
    /// no such line at any of eight heights. So the rule is that no wrapping label is ever handed raw text, and this holds
    /// it the only way it can be held: the pages are built inside Myra trees no test can construct, so it reads the
    /// sources. For every object initializer in <c>Game/Screens</c> that sets <c>Wrap = true</c>, the initializer writes
    /// no <c>Text</c> (an empty one apart), and nothing in the file assigns that label's <c>Text</c> — <c>SetProse</c> is
    /// how it is written. <c>Wrap</c> itself stays on as the backstop for a line the breaker left wider than the label.
    /// <para>
    /// Seen to fail on the seven labels #855 found before they were converted: the boards' status lines on the picker and
    /// the result page, the result's unlock note, the main menu's quality notice, the note page's status and hint, and the
    /// quit page's line.
    /// </para>
    /// </summary>
    public class ProseLabelsTests
    {
        //The one wrapping label that takes its text directly: the note page's typing field. FieldTail cuts the player's
        //text into lines measured to the field's width, with the caret on the end, so the label's own wrap never fires;
        //and the text is the player's, spaces included
        private static readonly (string File, string Label)[] EXCEPTIONS = { ("NotePage.cs", "_field") };

        //How many wrapping initializers the pages hold today (twelve): a regex that stopped matching would otherwise pass
        //by finding nothing
        private const int AT_LEAST = 10;

        //An object initializer, with the name it is assigned to when there is one: "_status = new Label { … }",
        //"Label words = new() { … }", or an expression-bodied helper "Label BodyLabel(…) => new() { … }", whose name is
        //the helper's. Initializers here nest no braces (a Thickness or a brush is a call), which [^{}] relies on;
        //AT_LEAST catches the day one does
        private static readonly Regex Initializer =
            new(@"(?:(?<name>[\w.]+)\s*=\s*|(?<helper>\w+)\s*\([^()]*\)\s*=>\s*)?\bnew(?:\s+Label)?\s*(?:\(\))?\s*\{(?<body>[^{}]*)\}",
                RegexOptions.Compiled);

        [Fact]
        public void Every_wrapping_label_is_written_through_SetProse()
        {
            List<string> faults = new();
            int found = 0;

            foreach (string path in Directory.GetFiles(ScreensDirectory(), "*.cs").OrderBy(p => p, StringComparer.Ordinal))
            {
                string source = File.ReadAllText(path);
                string file = Path.GetFileName(path);

                foreach (Match initializer in Initializer.Matches(source))
                {
                    string body = initializer.Groups["body"].Value;
                    if (!Regex.IsMatch(body, @"\bWrap\s*=\s*true\b")) continue;

                    found++;
                    int line = source.AsSpan(0, initializer.Index).Count('\n') + 1;

                    Match text = Regex.Match(body, @"\bText\s*=\s*(?<value>[^,\r\n]+)");
                    if (text.Success && text.Groups["value"].Value.Trim() != "string.Empty")
                        faults.Add($"{file}:{line}: the initializer writes Text = {text.Groups["value"].Value.Trim()}; build the label and SetProse it");

                    //The label's names: the one it is assigned to, or every one a helper's result is assigned to
                    //("_hint = BodyLabel(…)"), so a label made by a helper is held like one made in place
                    IEnumerable<string> names = initializer.Groups["name"].Success
                        ? new[] { initializer.Groups["name"].Value }
                        : initializer.Groups["helper"].Success
                            ? Regex.Matches(source, $@"(?<var>[\w.]+)\s*=\s*{Regex.Escape(initializer.Groups["helper"].Value)}\(")
                                .Select(m => m.Groups["var"].Value)
                            : Array.Empty<string>();

                    foreach (string name in names)
                    {
                        if (EXCEPTIONS.Contains((file, name))) continue;

                        //The one assignment allowed is SetProse's own, which writes the breaker's lines
                        if (Regex.IsMatch(source, $@"\b{Regex.Escape(name)}\.Text\s*=(?!\s*SpaceWrap\.Wrap\()"))
                            faults.Add($"{file}:{line}: {name}.Text is assigned somewhere in the file; write it with SetProse({name}, …)");
                    }
                }
            }

            Assert.True(found >= AT_LEAST, $"only {found} wrapping initializers found under Game/Screens; the pattern no longer matches them");
            Assert.True(faults.Count == 0, string.Join(Environment.NewLine, faults));
        }

        private static string ScreensDirectory()
        {
            string relative = Path.Combine("Game", "Screens");

            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (Directory.Exists(candidate)) return candidate;
            }

            throw new DirectoryNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}
