using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// <b>The game always runs in real time</b> (#1046, the owner's ruling: "the slowing makes no sense and players do not like
    /// it"). The drop cinematic gave up its slow motion in #877, and the Cut's hit-stop and slow fall went in #1046, so nothing
    /// scales the time fed to the world. <c>GameplayScreen</c> and <c>DropCinematic</c> need the graphics device and cannot be
    /// compiled into this project, so what is held is the source, two ways: no file of the Game names a world time scale by any
    /// name it has had, and every call in the gameplay screen that moves the world (the step, the blasts, the cut's glows, the
    /// wormhole) is handed the frame's own <c>elapsed</c> and nothing else, whatever a new factor would be called. A check is
    /// evidence only once its failing branch has been seen to fire (BestPractices section 10), so each rule is also run over
    /// the text the two changes removed and over a factor under a new name.
    /// </summary>
    public class RealTimeSourceTests
    {
        //A world time scale by any of the names it has had: the screen's choice, the cinematic's and the cut's own scales, the
        //hit-stop, the slow motion's timeline and length
        private static readonly Regex ScaleByName = new(
            @"\bWorldTimeScale\b|\bTimeScale\b|\bworldScale\b|HIT_STOP|SlowSeconds|_slowAge|SLOWMO|_slowMotion|slow-mo\b",
            RegexOptions.Compiled);

        //A call that moves the world handed anything but the frame's own seconds as its first argument. Not a declaration
        //(`void StepPhysics(float elapsed)`), and only in the gameplay screen's files: the benchmark page steps a world of its own
        private static readonly Regex WorldFedOtherThanElapsed = new(
            @"(?<![\w.])(?<!void )(?:StepPhysics|_blasts\.Update|_cutStaging\.StepWorld|_wormhole\.Update)\((?!elapsed[,)])",
            RegexOptions.Compiled);

        /// <summary>Lines the name rule must refuse: the text #877 and #1046 removed.</summary>
        private static readonly string[] RemovedNames =
        {
            //#1046: the screen's choice, the cut's scale, its hit-stop and its timeline
            "private float WorldTimeScale => _cinematic.Engaged ? 1f : _cutStaging.TimeScale;",
            "float worldScale = WorldTimeScale;",
            "public float TimeScale => _slowAge < 0f ? 1f : CutBeats.TimeScale(_slowAge, _size, _freed);",
            "public const float HIT_STOP_SCALE = 0.15f;",
            "if (_slowAge >= CutBeats.SlowSeconds(_size, _freed)) _slowAge = -1f;",
            //#877: the cinematic's own clock
            "public float TimeScale { get; private set; } = 1f;",
            "private const float SLOWMO_DELAY = 0.18f;",
            "_slowMotion = Lerp(random, 0.32f, 0.55f);",
            "tilt {4:F1}deg, slow-mo {5:F2}, {6}",
        };

        /// <summary>Lines the call rule must refuse: the #1046 feeds, and the same under names no list could guess.</summary>
        private static readonly string[] ScaledFeeds =
        {
            "StepPhysics(elapsed * worldScale);",
            "_blasts.Update(elapsed * worldScale, Game.Audio);",
            "_cutStaging.StepWorld(elapsed * worldScale);",
            "_wormhole.Update(elapsed * worldScale, Game.Audio);",
            "StepPhysics(elapsed * _cutStaging.Speed);",
            "float blastJolt = _blasts.Update(dt * slow, Game.Audio);",
            "StepPhysics(Slowed(elapsed));",
        };

        [Fact]
        public void No_file_of_the_Game_names_a_world_time_scale()
        {
            foreach ((string path, int line, string code) in CodeLines(GameSources()))
            {
                Match match = ScaleByName.Match(code);
                Assert.False(match.Success, $"{Path.GetFileName(path)}:{line} names a world time scale: '{match.Value}' in '{code.Trim()}'");
            }
        }

        [Fact]
        public void Every_call_that_moves_the_world_is_handed_the_frames_own_seconds()
        {
            string[] screen = GameSources().Where(path => Path.GetFileName(path).StartsWith("GameplayScreen", StringComparison.Ordinal)).ToArray();

            int calls = 0;
            foreach ((string path, int line, string code) in CodeLines(screen))
            {
                Match match = WorldFedOtherThanElapsed.Match(code);
                Assert.False(match.Success, $"{Path.GetFileName(path)}:{line} feeds the world something else than elapsed: '{code.Trim()}'");
                calls += Regex.Matches(code, @"(?<![\w.])(?<!void )(?:StepPhysics|_blasts\.Update|_cutStaging\.StepWorld|_wormhole\.Update)\(elapsed[,)]").Count;
            }

            //The frame's four feeds and the result page's four: a sweep that found none would pass by reading nothing
            Assert.True(calls >= 8, $"only {calls} calls that move the world were found in the gameplay screen");
        }

        [Fact]
        public void The_checks_fire_on_the_text_they_replaced()
        {
            foreach (string line in RemovedNames)
                Assert.True(ScaleByName.IsMatch(CodeOf(line)), $"the name rule passes a line it exists to refuse: '{line}'");

            foreach (string line in ScaledFeeds)
                Assert.True(WorldFedOtherThanElapsed.IsMatch(CodeOf(line)), $"the call rule passes a line it exists to refuse: '{line}'");

            //And what they must let through: the feeds as they stand, the declaration, a comment naming the old scale
            Assert.DoesNotMatch(WorldFedOtherThanElapsed, "StepPhysics(elapsed);");
            Assert.DoesNotMatch(WorldFedOtherThanElapsed, "float blastJolt = _blasts.Update(elapsed, Game.Audio);");
            Assert.DoesNotMatch(WorldFedOtherThanElapsed, "private void StepPhysics(float elapsed)");
            Assert.DoesNotMatch(ScaleByName, CodeOf("//the Cut's TimeScale went in #1046"));
        }

        private static string[] GameSources()
        {
            string game = FindUnderRepository("Game");
            string[] sources = Directory.GetFiles(game, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                    && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .ToArray();

            //The files the scale lived in are among those read, or the sweep could pass by reading nothing
            Assert.Contains(sources, path => path.EndsWith("GameplayScreen.cs", StringComparison.Ordinal));
            Assert.Contains(sources, path => path.EndsWith("CutStaging.cs", StringComparison.Ordinal));
            Assert.Contains(sources, path => path.EndsWith("DropCinematic.cs", StringComparison.Ordinal));

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

        /// <summary>The line with its <c>//</c> comment cut off, and nothing of a <c>///</c> doc line: the rules are about code.</summary>
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
