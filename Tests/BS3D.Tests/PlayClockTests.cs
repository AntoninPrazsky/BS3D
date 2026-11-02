using BS3D.Screens;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #1045, step one: the level's play clock (<see cref="PlayClock"/>) stands still while a camera takeover has the lens
    /// (the owner, 2026-11-02: "the clock never runs while the camera follows the falling balls"), as it already did while
    /// the chapter's tour held the release (#690). The rule is asked here; that the gameplay screen's clock goes through it
    /// and nowhere else is held on its source, which needs the graphics device and cannot be compiled into this project.
    /// A check is evidence only once its failing branch has been seen to fire (BestPractices section 10), so the source rule
    /// is also run over the line this change replaced.
    /// </summary>
    public class PlayClockTests
    {
        [Fact]
        public void The_clock_runs_only_while_the_player_has_the_gun()
        {
            Assert.Equal(1f / 60f, PlayClock.Counts(1f / 60f, releasePending: false, cameraTakeover: false));
            Assert.Equal(0f, PlayClock.Counts(1f / 60f, releasePending: false, cameraTakeover: true));
            Assert.Equal(0f, PlayClock.Counts(1f / 60f, releasePending: true, cameraTakeover: false));
            Assert.Equal(0f, PlayClock.Counts(1f / 60f, releasePending: true, cameraTakeover: true));
        }

        /// <summary>
        /// A level of ten seconds' play with a 6.5 s drop cinematic (<c>DropCinematic.MAX_SECONDS</c>, its backstop) in the
        /// middle sends ten seconds, at any frame rate; the clock before #1045 sent 16.5.
        /// </summary>
        [Theory]
        [InlineData(30f)]
        [InlineData(60f)]
        [InlineData(144f)]
        public void A_drop_cinematic_costs_the_clock_nothing_at_any_frame_rate(float hz)
        {
            double seconds = 0.0, old = 0.0, wall = 0.0;
            float dt = 1f / hz;

            for (int frame = 0; wall < 16.5 - 1e-6; frame++)
            {
                bool takeover = wall >= 5.0 && wall < 11.5;
                seconds += PlayClock.Counts(dt, releasePending: false, takeover);
                old += dt;
                wall += dt;
            }

            Assert.InRange(seconds, 10.0 - 1.5 / hz, 10.0 + 1.5 / hz);
            Assert.InRange(old, 16.5 - 1e-3, 16.5 + 1.0 / hz);
        }

        //A write to the level's clock that does not go through PlayClock
        private static readonly Regex ClockAdvancedAround = new(@"_run\.Seconds\s*\+=(?!\s*PlayClock\.Counts\()", RegexOptions.Compiled);

        [Fact]
        public void The_gameplay_screen_advances_the_clock_only_through_PlayClock()
        {
            string game = FindUnderRepository("Game");
            string[] screen = Directory.GetFiles(Path.Combine(game, "Screens"), "GameplayScreen*.cs");
            Assert.Contains(screen, path => path.EndsWith("GameplayScreen.cs", StringComparison.Ordinal));

            int through = 0;
            foreach (string path in screen)
            {
                string source = File.ReadAllText(path);
                Match around = ClockAdvancedAround.Match(source);
                Assert.False(around.Success, $"{Path.GetFileName(path)} advances the play clock around PlayClock: '{around.Value}'");
                through += Regex.Matches(source, @"_run\.Seconds\s*\+=\s*PlayClock\.Counts\(elapsed, _releasePending, CameraTakeoverEngaged\)").Count;
            }

            Assert.Equal(1, through);
        }

        [Fact]
        public void The_source_rule_fires_on_the_line_it_replaced()
        {
            Assert.Matches(ClockAdvancedAround, "if (!_releasePending) _run.Seconds += elapsed;");
            Assert.DoesNotMatch(ClockAdvancedAround, "_run.Seconds += PlayClock.Counts(elapsed, _releasePending, CameraTakeoverEngaged);");
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
