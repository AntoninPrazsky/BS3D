using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #877: the drop cinematic runs at ordinary game time. A fall in slow motion under a moved camera reads as a replay, not
    /// as the player's own success, so the world's clock is 1 while the cinematic has the lens. <c>DropCinematic</c> needs the
    /// gameplay screen's field of view and cannot be compiled into this project, so what is held is the source: the cinematic
    /// owns no time scale, no slow-motion roll or ramp, and the screen's one choice of the world's speed does not ask it for one.
    /// A check is evidence only once its failing branch has been seen to fire (BestPractices section 10), so each rule is also
    /// run over the text this change removed.
    /// </summary>
    public class DropCinematicClockSourceTests
    {
        private static readonly Regex ClockInTheCinematic = new(@"\bTimeScale\b|SLOWMO|_slowMotion|slow-mo\b", RegexOptions.Compiled);

        /// <summary>The line that chose the world's speed before #877: the cinematic's own scale while it has the lens.</summary>
        private const string OLD_CHOICE = "private float WorldTimeScale => _cinematic.Engaged ? _cinematic.TimeScale : _cutStaging.TimeScale;";

        [Fact]
        public void The_cinematic_owns_no_clock()
        {
            string source = File.ReadAllText(FindUnderRepository("Game/Effects/DropCinematic.cs"));

            Match match = ClockInTheCinematic.Match(source);
            Assert.False(match.Success, $"DropCinematic.cs still carries a clock of its own: '{match.Value}'");
        }

        [Fact]
        public void The_world_runs_at_one_while_the_cinematic_has_the_lens()
        {
            string source = File.ReadAllText(FindUnderRepository("Game/Screens/GameplayScreen.cs"));

            Assert.Contains("private float WorldTimeScale => _cinematic.Engaged ? 1f : _cutStaging.TimeScale;", source);
            Assert.DoesNotContain("_cinematic.TimeScale", source);
        }

        [Fact]
        public void The_checks_fire_on_the_clock_they_replaced()
        {
            Assert.Matches(ClockInTheCinematic, "public float TimeScale { get; private set; } = 1f;");
            Assert.Matches(ClockInTheCinematic, "private const float SLOWMO_DELAY = 0.18f;");
            Assert.Matches(ClockInTheCinematic, "_slowMotion = Lerp(random, 0.32f, 0.55f);");
            Assert.Matches(ClockInTheCinematic, "tilt {4:F1}deg, slow-mo {5:F2}, {6}");

            Assert.Contains("_cinematic.TimeScale", OLD_CHOICE);
            Assert.DoesNotContain("_cinematic.Engaged ? 1f :", OLD_CHOICE);
        }

        private static string FindUnderRepository(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}
