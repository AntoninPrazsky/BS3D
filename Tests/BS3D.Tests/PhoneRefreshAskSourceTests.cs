using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #1141: the phone's display is asked for a refresh through the window's preferred display mode, which Android has had
    /// since 6 (API 23). Both asks, <c>MainActivity</c>'s fastest at start and the FPS limit row's 60 Hz (#1074), were gated
    /// on Android 11 because the activity's own display is 11's: on 7 to 10 nothing was asked, the row still read "60 Hz",
    /// and the row's ask was retried every frame since the gate returned before it was cleared. GameAndroid needs the
    /// Android SDK, so it is read here as source: no member that writes the preferred mode or clears the pending ask may
    /// ask which Android it runs on, and the project may not install below the mode's Android. The rule is seen to fire on
    /// the two members as they stood.
    /// </summary>
    public class PhoneRefreshAskSourceTests
    {
        //The oldest Android with WindowManager.LayoutParams.preferredDisplayModeId, Display.getMode and getSupportedModes
        private const int PREFERRED_MODE_API = 23;

        //What makes a member one of the asks: the mode written, or the pending ask taken
        private static readonly Regex Asks = new(@"PreferredDisplayModeId\s*=|_askPending\s*=\s*false", RegexOptions.Compiled);

        //A gate on the Android version
        private static readonly Regex VersionGate = new(@"IsAndroidVersionAtLeast\(", RegexOptions.Compiled);

        //A member's first line inside a class inside a namespace: eight spaces, an access modifier and a parameter list
        private static readonly Regex MemberStart = new(@"^ {8}(?:public|internal|protected|private)\b[^=;]*\(", RegexOptions.Compiled);

        [Fact]
        public void No_refresh_ask_is_gated_on_the_android_version()
        {
            string android = FindUnderRepository("GameAndroid");
            int asks = 0;

            foreach (string path in new[] { Path.Combine(android, "MainActivity.cs"), Path.Combine(android, "Platform", "DisplayRefresh.cs") })
            {
                foreach ((string name, string body) in AskingMembers(File.ReadAllText(path)))
                {
                    asks++;
                    Assert.False(VersionGate.IsMatch(body),
                        $"{Path.GetFileName(path)}: '{name.Trim()}' asks the display for a mode only on some Androids; the preferred "
                        + $"mode is API {PREFERRED_MODE_API}'s, so it is asked on every one the app installs on");
                }
            }

            //The mode written and the pending ask taken: a reading that found neither would pass by finding nothing
            Assert.True(asks >= 2, $"only {asks} asking members were found");
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
            //DisplayRefresh.ApplyAsk and MainActivity.OnCreate before #1141, cut to their gates
            string applyAsk = string.Join("\n",
                "        private static void ApplyAsk()",
                "        {",
                "            Android.App.Activity activity = Game.Activity;",
                "            if (activity == null || !OperatingSystem.IsAndroidVersionAtLeast(30)) return;",
                "            _askPending = false;",
                "        }");
            string onCreate = string.Join("\n",
                "        protected override void OnCreate(Bundle savedInstanceState)",
                "        {",
                "            if (OperatingSystem.IsAndroidVersionAtLeast(30) && Display != null)",
                "            {",
                "                attributes.PreferredDisplayModeId = fastest.ModeId;",
                "            }",
                "        }");

            foreach (string source in new[] { applyAsk, onCreate })
            {
                List<(string Name, string Body)> members = AskingMembers(source);
                Assert.Single(members);
                Assert.Matches(VersionGate, members[0].Body);
            }

            //And a member that only reads the display, as CurrentDisplay does, is not an ask
            Assert.Empty(AskingMembers(string.Join("\n",
                "        private static Display CurrentDisplay(Android.App.Activity activity)",
                "        {",
                "            if (OperatingSystem.IsAndroidVersionAtLeast(30)) return activity.Display;",
                "        }")));
        }

        /// <summary>Every member of <paramref name="source"/> whose body writes the preferred mode or takes the pending ask.</summary>
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
