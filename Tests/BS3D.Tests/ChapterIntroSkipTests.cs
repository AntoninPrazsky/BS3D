using BS3D.Effects;
using BS3D.Screens;
using Microsoft.Xna.Framework;
using Prazsky.BS3D.Levels;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// <b>Every opening can be skipped</b> (#1106, the owner: "always", the player's very first opening included). Three
    /// things hold it. The intro refuses a skip only inside its own <see cref="ChapterIntro.SKIP_LOCKOUT"/> and takes the
    /// first one after it, at any frame rate, with a prologue or without. The gameplay screen asks the intro alone: whatever
    /// the start (a new chapter, a Continue, a first launch on a save with nothing cleared), <c>TryBeginChapterIntro</c> reads
    /// nothing of the save, <c>SkipCameraTakeover</c> calls the intro's <c>TrySkip</c> as the whole of its condition, and
    /// the skip hint is handed the intro's own <c>Running</c>. <c>GameplayScreen</c> needs the graphics device and cannot be
    /// compiled into this project, so those are held on its source, as <see cref="RealTimeSourceTests"/> holds the world's
    /// clock, and each rule is also run over the text #1021's hold put there, so its failing branch is seen to fire
    /// (BestPractices section 10). And a save written while that hold stood, which recorded an <c>opening</c> lesson, still
    /// loads, and no lesson of the tutorial answers to that key.
    /// </summary>
    public class ChapterIntroSkipTests
    {
        //The lines #1021 put in the gameplay screen and #1106 took out
        private const string HeldSkip = "            if (!_introWhole && _chapterIntro.TrySkip()) Console.WriteLine(\"[skip] chapter intro\");";
        private const string HeldHint =
            "                _hud.UpdateSkipHint(elapsed, _chapterIntro.Running && !_introWhole, chapterIntro: true, _chapterIntro.Elapsed, _chapterIntro.Remaining);";
        private const string HeldStart =
            "            _introWhole = !_run.Benchmark && Game.TotalStars == 0 && !Game.WasLessonTaught(FIRST_OPENING_LESSON);";

        //The intro's skip as the whole of an if's condition, the line SkipCameraTakeover must carry
        private static readonly Regex BareIntroSkip = new(@"^(?<indent>\s*)if \(_chapterIntro\.TrySkip\(\)\)", RegexOptions.Compiled);

        //A call of the intro's TrySkip anywhere on a line
        private static readonly Regex AnyIntroSkip = new(@"_chapterIntro\.TrySkip\(\)", RegexOptions.Compiled);

        //The skip hint handed the chapter intro's state: its second argument is what says the intro is running
        private static readonly Regex IntroHint = new(@"UpdateSkipHint\(\s*[^,]+,\s*(?<running>[^,]+),\s*chapterIntro:\s*true", RegexOptions.Compiled);

        //What a start would read to hold an opening: the save's stars and lessons, and the names the hold had
        private static readonly Regex SaveRead = new(
            @"\bTotalStars\b|\bWasLessonTaught\b|\bRecordLessonTaught\b|\b_introWhole\b|\bFIRST_OPENING_LESSON\b|\bNoteFirstOpeningSeen\b",
            RegexOptions.Compiled);

        [Theory]
        [InlineData(30, false)]
        [InlineData(60, false)]
        [InlineData(75, false)]
        [InlineData(144, false)]
        [InlineData(60, true)]
        [InlineData(144, true)]
        public void An_opening_refuses_a_skip_inside_the_lockout_and_takes_the_first_after_it(int hertz, bool prologue)
        {
            var intro = new ChapterIntro();
            intro.Begin(Vector3.Zero, 40f, 0.75f, new Vector3(0f, 10f, 40f), Vector3.Zero, null, new Random(1),
                prologue ? new[] { new IntroShot("opener", new[] { new Vector3(0f, 50f, 100f), new Vector3(10f, 50f, 100f) }, 3f, 1f,
                    lookAt: Vector3.Zero) } : null);

            float frame = 1f / hertz;
            Assert.False(intro.TrySkip(), "a skip on the frame the intro began was taken");

            for (int frames = 1; ; frames++)
            {
                intro.Update(frame);
                Assert.True(intro.Running, $"the intro ended on its own after {frames} frames, before a skip could be tried");

                if (intro.Elapsed < ChapterIntro.SKIP_LOCKOUT)
                {
                    Assert.False(intro.TrySkip(), $"a skip {intro.Elapsed:F3} s in, inside the lockout, was taken");
                    continue;
                }

                Assert.True(intro.TrySkip(), $"the first skip past the lockout, {intro.Elapsed:F3} s in, was refused");
                Assert.True(intro.Elapsed < ChapterIntro.SKIP_LOCKOUT + frame, "the skip waited longer than a frame past the lockout");
                break;
            }

            //The flight is over, so the hint goes; the lens is handed back by the ease (or at once, out of a prologue)
            Assert.False(intro.Running);
            Assert.Equal(prologue, !intro.Engaged);
            Assert.False(intro.TrySkip(), "a second skip of the same intro was taken");
        }

        [Fact]
        public void The_skip_of_an_opening_asks_the_intro_alone()
        {
            string[] body = MethodBody(Source("GameplayScreen.cs"), "private void SkipCameraTakeover()");

            string fault = IntroSkipFault(body);
            Assert.True(fault == null, $"SkipCameraTakeover does not ask the intro alone: {fault}");

            string call = body.Single(line => AnyIntroSkip.IsMatch(CodeOf(line)));

            //And the method as #1021 left it fails the rule
            Assert.NotNull(IntroSkipFault(Replaced(body, call, HeldSkip)));

            //As would a hold under a new name, one that wraps the call in a condition of its own, and one that leaves the method
            //before the intro is asked
            Assert.NotNull(IntroSkipFault(Replaced(body, call,
                "            if (OpeningMayBeSkipped && _chapterIntro.TrySkip()) Console.WriteLine(\"[skip] chapter intro\");")));
            Assert.NotNull(IntroSkipFault(Replaced(body, call, "            if (!_holdOpening)", "    " + call)));
            Assert.NotNull(IntroSkipFault(Replaced(body, call, "            if (_holdOpening) return;", call)));
        }

        /// <summary><paramref name="lines"/> with <paramref name="line"/> replaced by <paramref name="with"/>.</summary>
        private static string[] Replaced(string[] lines, string line, params string[] with) =>
            lines.SelectMany(each => each == line ? with : new[] { each }).ToArray();

        [Fact]
        public void The_skip_hint_over_an_opening_is_handed_the_intro_running_alone()
        {
            int calls = 0;
            foreach ((string path, int number, string code) in CodeLines(ScreenSources()))
            {
                Match match = IntroHint.Match(code);
                if (!match.Success) continue;

                calls++;
                Assert.True(match.Groups["running"].Value.Trim() == "_chapterIntro.Running",
                    $"{Path.GetFileName(path)}:{number} hands the opening's skip hint '{match.Groups["running"].Value.Trim()}'");
            }

            Assert.Equal(1, calls);

            //The rule fires on the line #1021 left
            Match held = IntroHint.Match(HeldHint);
            Assert.True(held.Success && held.Groups["running"].Value.Trim() != "_chapterIntro.Running");
        }

        [Fact]
        public void No_start_reads_the_save_to_decide_an_opening()
        {
            string[] start = MethodBody(Source("GameplayScreen.Rules.cs"), "private void TryBeginChapterIntro()");
            Assert.Contains(start, line => line.Contains("_chapterIntro.Begin(", StringComparison.Ordinal));

            foreach (string line in start)
                Assert.False(SaveRead.IsMatch(CodeOf(line)), $"TryBeginChapterIntro reads the save: '{line.Trim()}'");

            //Nor does any file of the screen name the hold
            foreach ((string path, int number, string code) in CodeLines(ScreenSources()))
            {
                Match match = Regex.Match(code, @"\b_introWhole\b|\bFIRST_OPENING_LESSON\b|\bNoteFirstOpeningSeen\b");
                Assert.False(match.Success, $"{Path.GetFileName(path)}:{number} names the first opening's hold: '{code.Trim()}'");
            }

            //The rule fires on the line #1021 put in the start
            Assert.Matches(SaveRead, HeldStart);
        }

        [Fact]
        public void A_save_that_recorded_the_first_opening_loads_and_no_lesson_answers_to_it()
        {
            using TempDirectory temp = new();
            string path = temp.File("Progress.json");

            PlayerProgress written = PlayerProgress.Load(path);
            written.Record("One.json", 1200, 2);
            written.Teach("aim");
            written.Teach("opening");
            written.Save();

            PlayerProgress read = PlayerProgress.Load(path);
            Assert.Equal(ProgressLoad.Loaded, read.Outcome);
            Assert.True(read.WasTaught("aim"));
            Assert.Equal(2, read.TotalStars);

            //What the old build wrote is a key nothing asks for any more: no step of any card is recorded under it
            Assert.DoesNotContain(Tutorial.Steps(), step => step.Key == "opening");
        }

        /// <summary>
        /// Why <paramref name="body"/> (a method's lines, its signature first) does not ask the intro alone, or null when it
        /// does: exactly one line calls the intro's TrySkip, as the whole condition of an if at the body's own depth, with no
        /// return ahead of it.
        /// </summary>
        private static string IntroSkipFault(string[] body)
        {
            string[] calls = body.Where(line => AnyIntroSkip.IsMatch(CodeOf(line))).ToArray();
            if (calls.Length != 1) return $"{calls.Length} lines call the intro's TrySkip";

            Match bare = BareIntroSkip.Match(calls[0]);
            if (!bare.Success) return $"the intro's skip is asked with something else: '{calls[0].Trim()}'";

            int depth = body.Skip(2).First(line => line.Trim().Length > 0 && !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .TakeWhile(char.IsWhiteSpace).Count();
            if (bare.Groups["indent"].Value.Length != depth) return "the intro's skip sits inside another condition";

            int at = Array.IndexOf(body, calls[0]);
            if (body.Take(at).Any(line => Regex.IsMatch(CodeOf(line), @"\breturn\b"))) return "the method can return before the intro is asked";

            return null;
        }

        /// <summary>A method's lines from its signature to its closing brace, found by the brace at the signature's depth.</summary>
        private static string[] MethodBody(string[] lines, string signature)
        {
            int start = Array.FindIndex(lines, line => line.Trim() == signature);
            Assert.True(start >= 0, $"no '{signature}' was found");

            string indent = new(lines[start].TakeWhile(char.IsWhiteSpace).ToArray());
            int end = Array.FindIndex(lines, start + 1, line => line == indent + "}");
            Assert.True(end > start, $"the end of '{signature}' was not found");

            return lines[start..(end + 1)];
        }

        private static string[] Source(string fileName) => File.ReadAllLines(ScreenSources().Single(path => Path.GetFileName(path) == fileName));

        private static string[] ScreenSources()
        {
            string screens = FindUnderRepository("Game/Screens");
            string[] sources = Directory.GetFiles(screens, "GameplayScreen*.cs", SearchOption.TopDirectoryOnly);

            //The files the hold lived in are among those read, or the sweep could pass by reading nothing
            Assert.Contains(sources, path => Path.GetFileName(path) == "GameplayScreen.cs");
            Assert.Contains(sources, path => Path.GetFileName(path) == "GameplayScreen.Rules.cs");
            Assert.Contains(sources, path => Path.GetFileName(path) == "GameplayScreen.Session.cs");

            return sources;
        }

        private static System.Collections.Generic.IEnumerable<(string Path, int Line, string Code)> CodeLines(string[] paths)
        {
            foreach (string path in paths)
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++) yield return (path, i + 1, CodeOf(lines[i]));
            }
        }

        /// <summary>The line with its <c>//</c> comment cut off: the rules are about code.</summary>
        private static string CodeOf(string line)
        {
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            return comment < 0 ? line : line[..comment];
        }

        private static string FindUnderRepository(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate) || Directory.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}
