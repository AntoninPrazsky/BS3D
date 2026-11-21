using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The session's side of the time bonus (#1045), held on the gameplay screen's sources, which the tests do not compile:
    /// the scorer is built with the level's par, the clear awards the time bonus beside the completion bonus and throws it over
    /// the aim point, and the HUD is handed the clock its readout drains on. Each removed alone left every other test and
    /// ScoreSim passing (they call <c>ScoreKeeper</c> themselves), so each is checked here, and each check is seen to fire on
    /// its own line removed from the real source.
    /// </summary>
    public class TimeBonusWiringTests
    {
        //The body of CheckLevelCleared up to a point: the method ends at the first brace at its own indentation
        private const string IN_CHECK_LEVEL_CLEARED = @"private void CheckLevelCleared\(\)(?:(?!\n        \}).)*?";

        private static readonly (string What, Regex Pattern, string Line, string Without)[] Wiring =
        {
            ("the scorer built with the level's par", new Regex(@"new ScoreKeeper\([^;]*, LevelPar\(index\)\);"),
                ", _run.InitialBallCount, LevelPar(index));", ", _run.InitialBallCount);"),
            ("the time bonus awarded at the clear, after the completion bonus",
                new Regex(IN_CHECK_LEVEL_CLEARED + @"_run\.Score\.AwardCompletionBonus\(\);(?:(?!\n        \}).)*?"
                    + @"_run\.Score\.AwardTimeBonus\(_run\.Seconds\);", RegexOptions.Singleline),
                "int timeBonus = _run.Score.AwardTimeBonus(_run.Seconds);", "int timeBonus = 0;"),
            ("the time bonus thrown over the aim point at the clear",
                new Regex(IN_CHECK_LEVEL_CLEARED + @"_hud\.AddTimeBonus\(timeBonus\);", RegexOptions.Singleline),
                "if (timeBonus > 0) _hud.AddTimeBonus(timeBonus);", string.Empty),
            ("the HUD handed the readout's clock",
                new Regex(@"_hud\.Update\([^;]*, TimeBonusReadout\.Clock\(_phase, _run\.Seconds, _run\.EndSeconds\)(?:,[^;]*)?\);"),
                "TimeBonusReadout.Clock(_phase, _run.Seconds, _run.EndSeconds)", "_run.Seconds"),
            ("the HUD told the drop camera holds the clock on a level still played (#1197)",
                new Regex(@"_hud\.Update\([^;]*clockHeld: _cinematic\.Engaged && !_releasePending && !LevelDecided\);"),
                "clockHeld: _cinematic.Engaged && !_releasePending && !LevelDecided", "clockHeld: false"),
        };

        [Fact]
        public void The_session_hands_the_scorer_its_par_and_pays_and_shows_the_time_bonus()
        {
            string source = ScreenSource();

            foreach ((string what, Regex pattern, _, _) in Wiring)
                Assert.True(pattern.Matches(source).Count == 1, $"the gameplay screen no longer has {what}");
        }

        [Fact]
        public void Each_check_fires_on_its_own_line_removed()
        {
            string source = ScreenSource();

            foreach ((string what, Regex pattern, string line, string without) in Wiring)
            {
                Assert.True(source.Contains(line, StringComparison.Ordinal), $"the line behind '{what}' is not in the source as written here");
                Assert.False(pattern.IsMatch(source.Replace(line, without, StringComparison.Ordinal)), $"'{what}' still matched with its line removed");
            }
        }

        //Every part of the gameplay screen, in one string with LF line ends, as the patterns read it
        private static string ScreenSource()
        {
            string screens = Path.Combine(FindUnderRepository("Game"), "Screens");
            string[] parts = Directory.GetFiles(screens, "GameplayScreen*.cs").OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Assert.Contains(parts, path => path.EndsWith("GameplayScreen.Rules.cs", StringComparison.Ordinal));

            return string.Join("\n", parts.Select(File.ReadAllText)).Replace("\r\n", "\n");
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
