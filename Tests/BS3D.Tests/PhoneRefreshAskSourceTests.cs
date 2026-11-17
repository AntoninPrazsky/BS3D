using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #1141: the phone's display is asked for a refresh through the window's preferred display mode, which Android has had
    /// since 6 (API 23). Both asks, <c>MainActivity</c>'s fastest at start and the FPS limit row's 60 Hz (#1074), were gated
    /// on Android 11 because the activity's own display is 11's: on 7 to 10 nothing was asked, the row still read "60 Hz",
    /// and the row's ask was retried every frame since the gate returned before it was cleared. GameAndroid needs the
    /// Android SDK, so it is read here as source, by two rules. <b>The asks themselves</b>, the members of
    /// <c>DisplayRefresh</c> that write the preferred mode or the pending ask, may not ask which Android they run on
    /// anywhere in their bodies. <b>Every call of one</b> (<c>MainActivity</c>'s at start, the Game's two of
    /// <c>AskRefresh</c>, <c>Watch</c>'s of the pending ask) may not stand under that question: not on its own line, not in
    /// the condition of an <c>if</c> without braces above it, not in the header of a block around it and not in a return
    /// before it. Only the call's own statement is read, because its member may ask for other reasons (<c>OnCreate</c>'s
    /// cutout is Android 9's). And the project may not install below the mode's Android. Both rules are seen to fire on
    /// the asks as they stood and on the gates a change could put back.
    /// </summary>
    public class PhoneRefreshAskSourceTests
    {
        //The oldest Android with WindowManager.LayoutParams.preferredDisplayModeId, Display.getMode and getSupportedModes
        private const int PREFERRED_MODE_API = 23;

        //What makes a member one of the asks: the mode written, or the pending ask set or taken
        private static readonly Regex Asks = new(@"PreferredDisplayModeId\s*=(?!=)|_askPending\s*=(?!=)", RegexOptions.Compiled);

        //What makes a statement a call of one: the mode written, or a call of an ask
        private static readonly Regex CallsAnAsk = new(@"PreferredDisplayModeId\s*=(?!=)|\b(?:PreferMode|AskRefresh|ApplyAsk)\(", RegexOptions.Compiled);

        //A gate on the Android version
        private static readonly Regex VersionGate = new(@"IsAndroidVersionAtLeast\(", RegexOptions.Compiled);

        private static readonly Regex Return = new(@"\breturn\b", RegexOptions.Compiled);

        //A member's first line inside a class inside a namespace: eight spaces, an access modifier and a parameter list
        private static readonly Regex MemberStart = new(@"^ {8}(?:public|internal|protected|private)\b[^=;]*\(", RegexOptions.Compiled);

        [Fact]
        public void No_refresh_ask_is_gated_on_the_android_version()
        {
            string android = FindUnderRepository("GameAndroid");
            string repository = Path.GetDirectoryName(android);
            string displayRefresh = Path.Combine(android, "Platform", "DisplayRefresh.cs");

            List<(string Name, string Body)> asks = AskingMembers(File.ReadAllText(displayRefresh));
            foreach ((string name, string body) in asks)
            {
                Assert.False(VersionGate.IsMatch(body),
                    $"DisplayRefresh.cs: '{name.Trim()}' asks the display for a mode only on some Androids; the preferred mode is "
                    + $"API {PREFERRED_MODE_API}'s, so it is asked on every one the app installs on");
            }

            //The three asks by name: a reading that found none would pass by finding nothing
            foreach (string ask in new[] { "AskRefresh(", "ApplyAsk(", "PreferMode(" })
                Assert.Contains(asks, member => member.Name.Contains(ask, StringComparison.Ordinal));

            //Every call of an ask, wherever it stands: the activity's, DisplayRefresh's own and the shared Game's
            List<string> sources = new() { Path.Combine(android, "MainActivity.cs"), displayRefresh };
            sources.AddRange(Directory.EnumerateFiles(FindUnderRepository("Game"), "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path)));

            Dictionary<string, int> calls = new();
            foreach (string path in sources)
            {
                string file = Path.GetRelativePath(repository, path).Replace('\\', '/');
                foreach ((string line, string guard) in AskStatements(File.ReadAllText(path)))
                {
                    calls[file] = calls.GetValueOrDefault(file) + 1;
                    Assert.False(VersionGate.IsMatch(guard),
                        $"{file}: '{line.Trim()}' stands under a question of the Android version; the preferred mode is "
                        + $"API {PREFERRED_MODE_API}'s, so it is asked on every one the app installs on:\n{guard}");
                }
            }

            //MainActivity's fastest at start; Watch's call of the pending ask, ApplyAsk's of PreferMode and PreferMode's write;
            //the Game's ask at its construction and the FPS limit row's. A reading that missed one would pass without it
            foreach ((string file, int least) in new[]
            {
                ("GameAndroid/MainActivity.cs", 1), ("GameAndroid/Platform/DisplayRefresh.cs", 3),
                ("Game/BS3DGame.cs", 1), ("Game/BS3DGame.Settings.cs", 1),
            })
            {
                Assert.True(calls.GetValueOrDefault(file) >= least,
                    $"{file}: {calls.GetValueOrDefault(file)} calls of an ask were found, {least} expected");
            }
        }

        [Fact]
        public void The_app_installs_on_no_android_without_the_preferred_mode()
        {
            string project = File.ReadAllText(Path.Combine(FindUnderRepository("GameAndroid"), "GameAndroid.csproj"));
            Match version = Regex.Match(project, @"<SupportedOSPlatformVersion>(\d+)</SupportedOSPlatformVersion>");

            Assert.True(version.Success);
            Assert.True(int.Parse(version.Groups[1].Value) >= PREFERRED_MODE_API);
        }

        [Fact]
        public void The_rule_fires_on_the_asks_as_they_stood()
        {
            //DisplayRefresh.ApplyAsk before #1141, cut to its gate, and AskRefresh with a gate put back at its top
            string[] gatedAsks =
            {
                Member("private static void ApplyAsk()",
                    "Android.App.Activity activity = Game.Activity;",
                    "if (activity == null || !OperatingSystem.IsAndroidVersionAtLeast(30)) return;",
                    "_askPending = false;"),
                Member("internal static void AskRefresh(int hz)",
                    "if (!OperatingSystem.IsAndroidVersionAtLeast(30)) return;",
                    "_askedHz = hz;",
                    "_askPending = true;"),
            };
            foreach (string source in gatedAsks)
            {
                List<(string Name, string Body)> members = AskingMembers(source);
                Assert.Single(members);
                Assert.Matches(VersionGate, members[0].Body);
            }

            //MainActivity.OnCreate before #1141, its mode written inside a block gated on Android 11, and its call of
            //PreferMode gated again on its own line and by an if without braces; Watch's call behind a return
            string cutout = "if (OperatingSystem.IsAndroidVersionAtLeast(28))";
            string cutoutSet = "    attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;";
            string onCreate = "protected override void OnCreate(Bundle savedInstanceState)";
            string[] gatedCalls =
            {
                Member(onCreate, cutout, cutoutSet,
                    "if (OperatingSystem.IsAndroidVersionAtLeast(30) && Display != null)",
                    "{",
                    "    Display.Mode fastest = Display.GetMode();",
                    "    attributes.PreferredDisplayModeId = fastest.ModeId;",
                    "}"),
                Member(onCreate, cutout, cutoutSet,
                    "if (OperatingSystem.IsAndroidVersionAtLeast(30)) Platform.DisplayRefresh.PreferMode(this, 0);"),
                Member(onCreate, cutout, cutoutSet,
                    "if (OperatingSystem.IsAndroidVersionAtLeast(30)",
                    "    && Display != null)",
                    "    //The fastest",
                    "    Platform.DisplayRefresh.PreferMode(this, 0);"),
                Member("internal static void Watch(float elapsed)",
                    "if (!OperatingSystem.IsAndroidVersionAtLeast(30))",
                    "    return;",
                    "if (_askPending) ApplyAsk();"),
            };
            foreach (string source in gatedCalls)
            {
                List<(string Line, string Guard)> statements = AskStatements(source);
                Assert.Single(statements);
                Assert.Matches(VersionGate, statements[0].Guard);
            }

            //And a gate the call does not stand under is not one: OnCreate's cutout, ended before the call, and a member
            //that only reads the display, as CurrentDisplay does
            List<(string Line, string Guard)> ungated = AskStatements(Member(onCreate, cutout, cutoutSet,
                "Window.Attributes = attributes;",
                "Platform.DisplayRefresh.PreferMode(this, 0);"));
            Assert.Single(ungated);
            Assert.DoesNotMatch(VersionGate, ungated[0].Guard);

            Assert.Empty(AskingMembers(Member("private static Display CurrentDisplay(Android.App.Activity activity)",
                "if (OperatingSystem.IsAndroidVersionAtLeast(30)) return activity.Display;")));
        }

        /// <summary>A member at a class's indentation inside a namespace, its body's lines one step further in.</summary>
        private static string Member(string signature, params string[] body) => string.Join("\n",
            new[] { "        " + signature, "        {" }.Concat(body.Select(line => "            " + line)).Append("        }"));

        /// <summary>Every member of <paramref name="source"/> whose body writes the preferred mode or the pending ask.</summary>
        private static List<(string Name, string Body)> AskingMembers(string source)
        {
            List<(string, string)> found = new();
            string[] lines = source.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                if (!MemberStart.IsMatch(lines[i])) continue;

                //An expression body or an empty one is its own line
                string line = lines[i].TrimEnd('\r');
                if (line.Contains("=>", StringComparison.Ordinal) || line.EndsWith(';') || line.EndsWith('}'))
                {
                    if (Asks.IsMatch(line)) found.Add((line, line));
                    continue;
                }

                //A block body runs to the closing brace at the member's own indentation
                int end = i + 1;
                while (end < lines.Length && lines[end].TrimEnd('\r') != "        }") end++;

                string body = string.Join("\n", lines, i, Math.Min(end + 1, lines.Length) - i);
                if (Asks.IsMatch(body)) found.Add((lines[i], body));
                i = end;
            }

            return found;
        }

        /// <summary>
        /// Every statement of <paramref name="source"/> that calls an ask or writes the mode, with what it stands under:
        /// its own line, the rest of its statement above it (an <c>if</c> without braces), the header of every block around
        /// it up to its member's signature, and every return before it in its member that asks the Android version.
        /// </summary>
        private static List<(string Line, string Guard)> AskStatements(string source)
        {
            List<(string, string)> found = new();
            string[] lines = source.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();

            for (int call = 0; call < lines.Length; call++)
            {
                string text = lines[call];
                if (IsComment(text)) continue;
                //A member's own line is its declaration, and only an expression body after it can call an ask
                string calling = !MemberStart.IsMatch(text) ? text
                    : text.Contains("=>", StringComparison.Ordinal) ? text[text.IndexOf("=>", StringComparison.Ordinal)..] : "";
                if (!CallsAnAsk.IsMatch(calling)) continue;

                int member = call;
                while (member > 0 && !MemberStart.IsMatch(lines[member])) member--;

                List<string> guard = new() { text };
                guard.AddRange(StatementHead(lines, call));

                //The blocks around the call, inwards to outwards: each opened at a shallower indentation than the last
                int indent = Indentation(text);
                for (int k = call - 1; k > member; k--)
                {
                    if (!lines[k].TrimEnd().EndsWith('{') || IsComment(lines[k]) || Indentation(lines[k]) >= indent) continue;
                    guard.Add(lines[k]);
                    guard.AddRange(StatementHead(lines, k));
                    indent = Indentation(lines[k]);
                }

                //A return before the call that asks the version, on its own line or as the statement of the if it ends
                for (int k = member + 1; k < call; k++)
                {
                    if (!VersionGate.IsMatch(lines[k])) continue;
                    int next = k + 1;
                    while (next < call && (lines[next].Trim().Length == 0 || IsComment(lines[next]))) next++;
                    if (Return.IsMatch(lines[k]) || (next < call && lines[next].TrimStart().StartsWith("return", StringComparison.Ordinal)))
                        guard.Add(lines[k]);
                }

                found.Add((text, string.Join("\n", guard)));
            }

            return found;
        }

        /// <summary>
        /// The lines above <paramref name="index"/> that belong to the statement or the block header ending there: back to
        /// the end of the statement before, or to the brace that opens the block. A brace-less <c>if</c>'s condition, or a
        /// block's header and its member's signature.
        /// </summary>
        private static IEnumerable<string> StatementHead(string[] lines, int index)
        {
            for (int k = index - 1; k >= 0; k--)
            {
                string trimmed = lines[k].Trim();
                if (trimmed.Length == 0 || IsComment(trimmed)) continue;
                if (trimmed.EndsWith(';') || trimmed.EndsWith('{') || trimmed.EndsWith('}')) yield break;
                yield return lines[k];
            }
        }

        private static bool IsComment(string line) => line.TrimStart().StartsWith("//", StringComparison.Ordinal);

        private static int Indentation(string line) => line.Length - line.TrimStart(' ').Length;

        private static bool IsBuildOutput(string path)
        {
            string separator = Path.DirectorySeparatorChar.ToString();
            return path.Contains(separator + "bin" + separator, StringComparison.OrdinalIgnoreCase)
                || path.Contains(separator + "obj" + separator, StringComparison.OrdinalIgnoreCase);
        }

        private static string FindUnderRepository(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (Directory.Exists(candidate)) return candidate;
            }

            throw new DirectoryNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}
