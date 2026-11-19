using BS3D.Screens;
using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// A phone's menus start drawn for a finger (#1181): before the first touch the phone pass of 2026-11-18 found the
    /// keyboard's Esc drawn on the Quit confirm's Cancel and on Back, a key a phone has not got. The rule is
    /// <see cref="MenuHand.StartsOnTouch"/>; that the game starts its prompts by it is held by reading the host's source, since
    /// the host is a MonoGame game no test constructs.
    /// </summary>
    public class MenuHandTests
    {
        [Theory]
        [InlineData(true, false, false, true)]
        [InlineData(false, false, false, false)]
        [InlineData(false, true, false, true)]
        [InlineData(true, false, true, false)]
        [InlineData(false, true, true, false)]
        public void A_phone_starts_on_a_fingers_hand_and_the_pad_argument_wins(bool onPhone, bool touch, bool pad, bool expected) =>
            Assert.Equal(expected, MenuHand.StartsOnTouch(onPhone, touch, pad));

        [Fact]
        public void The_game_starts_its_prompts_by_the_rule()
        {
            Assert.True(StartsByTheRule(HostSource()), "BS3DGame.cs no longer starts the prompts' hand through MenuHand.StartsOnTouch(OnPhone, ...).");

            //The line as the pass found it: the touch hand only where the argument pinned it
            Assert.False(StartsByTheRule("_promptsForTouch = _touchPinned = launch.TouchPrompts && !launch.PadPrompts;"));
        }

        private static bool StartsByTheRule(string code) =>
            Regex.IsMatch(code, @"_promptsForTouch\s*=\s*(?:Screens\.)?MenuHand\.StartsOnTouch\(\s*OnPhone\s*,");

        private static string HostSource()
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Game", "BS3DGame.cs");
                if (File.Exists(candidate)) return File.ReadAllText(candidate);
            }

            throw new FileNotFoundException("No Game/BS3DGame.cs was found above the test assembly.");
        }
    }
}
