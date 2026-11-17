using BS3D.Screens;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// <b>The precise-aim lean as the hands ask for it</b> (<see cref="LeanInput"/>): a press toggles it in and the next takes it
    /// out (#1054), a trigger wavering on the threshold flips it once, Settings' Hold makes a hold of the right button and the
    /// trigger (#520), the touch screen's button toggles under either mode (#1050), a pause and a takeover keep a toggled lean,
    /// and a switch of the mode lets go of it (#1140): toggled in under Toggle and switched to Hold from the pause, the lean used
    /// to stay in with nothing to take it out. And the screen reads the lean through that rule alone: <c>GameplayScreen</c> needs
    /// the graphics device and cannot be compiled here, so <c>UpdateAim</c> is held on its source, and the check is also run over
    /// the toggle honoured beside the rule, the shape #1140 had, so its failing branch is seen to fire (BestPractices section 10).
    /// </summary>
    public class LeanInputTests
    {
        private const bool TOGGLE = true, HOLD = false;

        private const string UPDATE_AIM = "private void UpdateAim(GameTime gameTime, bool edgeInputAllowed, GamePadState pad)";
        private const string SCRIPTED_HANDS = "private void StepScriptedHands()";

        private static MouseState Mouse(bool right) =>
            new(0, 0, 0, ButtonState.Released, ButtonState.Released, right ? ButtonState.Pressed : ButtonState.Released,
                ButtonState.Released, ButtonState.Released);

        private static GamePadState Pad(float left) =>
            new(new GamePadThumbSticks(), new GamePadTriggers(left, 0f), new GamePadButtons(), new GamePadDPad());

        /// <summary>One frame read by <paramref name="lean"/>, its answer handed back.</summary>
        private static (bool Held, float Pull) Frame(LeanInput lean, bool toggles, bool right = false, float trigger = 0f,
            bool touch = false, bool edge = true, bool captured = true)
        {
            lean.Read(toggles, edge, touch, captured, Mouse(right), Pad(trigger), out bool held, out float pull);
            return (held, pull);
        }

        /// <summary>A lean read for one frame with nothing down, as the first frame of a level is.</summary>
        private static LeanInput Settled(bool toggles)
        {
            LeanInput lean = new();
            Assert.False(Frame(lean, toggles).Held);
            return lean;
        }

        /// <summary>A lean toggled in by the right button under Toggle, the button let go.</summary>
        private static LeanInput ToggledIn()
        {
            LeanInput lean = Settled(TOGGLE);
            Frame(lean, TOGGLE, right: true);
            Assert.Equal((true, 1f), Frame(lean, TOGGLE));
            Assert.True(lean.Toggled);
            return lean;
        }

        [Fact]
        public void A_press_toggles_the_lean_in_and_the_next_takes_it_out()
        {
            LeanInput lean = Settled(TOGGLE);

            //In on the press, and in while the button is held: a hold is one press
            for (int frame = 0; frame < 5; frame++) Assert.Equal((true, 1f), Frame(lean, TOGGLE, right: true));

            //And in once it is let go
            for (int frame = 0; frame < 5; frame++) Assert.Equal((true, 1f), Frame(lean, TOGGLE));

            //Out on the next press, and out once that is let go
            Assert.Equal((false, 0f), Frame(lean, TOGGLE, right: true));
            Assert.Equal((false, 0f), Frame(lean, TOGGLE));
            Assert.False(lean.Toggled);

            //The trigger does the same
            Assert.Equal((true, 1f), Frame(lean, TOGGLE, trigger: 1f));
            Assert.Equal((true, 1f), Frame(lean, TOGGLE));
            Assert.Equal((false, 0f), Frame(lean, TOGGLE, trigger: 1f));
        }

        [Fact]
        public void A_trigger_wavering_on_the_threshold_flips_the_lean_once()
        {
            LeanInput lean = Settled(TOGGLE);
            float over = PreciseAim.TRIGGER_THRESHOLD + 0.05f, under = PreciseAim.TRIGGER_THRESHOLD - 0.05f;

            //Down past the threshold, then wavering under and over it: one press
            Frame(lean, TOGGLE, trigger: over);
            Frame(lean, TOGGLE, trigger: under);
            Frame(lean, TOGGLE, trigger: over);
            Frame(lean, TOGGLE, trigger: under);
            Assert.True(lean.Toggled);

            //Let go under half of it, then pulled short of the threshold: no press
            Frame(lean, TOGGLE, trigger: PreciseAim.TRIGGER_THRESHOLD * 0.5f - 0.05f);
            Frame(lean, TOGGLE, trigger: under);
            Assert.True(lean.Toggled);

            //Past it again: the next press
            Frame(lean, TOGGLE, trigger: over);
            Assert.False(lean.Toggled);
        }

        [Fact]
        public void Under_Hold_the_lean_follows_the_button_and_the_trigger()
        {
            LeanInput lean = Settled(HOLD);

            Assert.Equal((true, 1f), Frame(lean, HOLD, right: true));
            Assert.Equal((false, 0f), Frame(lean, HOLD));

            //The trigger's lean follows its pull, and it is held past the threshold
            Assert.Equal((false, PreciseAim.LeanAmount(default, Pad(0.3f))), Frame(lean, HOLD, trigger: 0.3f));
            Assert.Equal((true, PreciseAim.LeanAmount(default, Pad(0.7f))), Frame(lean, HOLD, trigger: 0.7f));
            Assert.Equal((false, 0f), Frame(lean, HOLD));

            //And neither of them toggles anything
            Assert.False(lean.Toggled);
        }

        [Fact]
        public void A_switch_to_Hold_lets_go_of_a_lean_the_right_button_toggled_in()
        {
            LeanInput lean = ToggledIn();

            //The pause, Settings' Precise aim switched to Hold, the resume
            lean.SpendPress();

            //The first frame under Hold with nothing down: out
            Assert.Equal((false, 0f), Frame(lean, HOLD));
            Assert.False(lean.Toggled);

            //And the button is a hold from there
            Assert.Equal((true, 1f), Frame(lean, HOLD, right: true));
            Assert.Equal((false, 0f), Frame(lean, HOLD));
        }

        [Fact]
        public void A_switch_to_Hold_lets_go_of_a_lean_the_trigger_toggled_in()
        {
            LeanInput lean = Settled(TOGGLE);
            Frame(lean, TOGGLE, trigger: 1f);
            Assert.Equal((true, 1f), Frame(lean, TOGGLE));

            lean.SpendPress();

            //A part-pulled trigger under Hold leans as far as it is pulled, not all the way
            Assert.Equal((false, PreciseAim.LeanAmount(default, Pad(0.3f))), Frame(lean, HOLD, trigger: 0.3f));
            Assert.Equal((false, 0f), Frame(lean, HOLD));
        }

        [Fact]
        public void A_switch_to_Toggle_lets_go_of_a_lean_the_touch_button_toggled_in()
        {
            LeanInput lean = Settled(HOLD);
            Assert.Equal((true, 1f), Frame(lean, HOLD, touch: true));
            Assert.Equal((true, 1f), Frame(lean, HOLD));

            lean.SpendPress();

            Assert.Equal((false, 0f), Frame(lean, TOGGLE));
            Assert.False(lean.Toggled);
        }

        [Fact]
        public void The_touch_button_toggles_under_either_mode()
        {
            foreach (bool toggles in new[] { TOGGLE, HOLD })
            {
                LeanInput lean = Settled(toggles);

                Assert.Equal((true, 1f), Frame(lean, toggles, touch: true));
                Assert.Equal((true, 1f), Frame(lean, toggles));
                Assert.Equal((false, 0f), Frame(lean, toggles, touch: true));
                Assert.Equal((false, 0f), Frame(lean, toggles));
            }
        }

        [Fact]
        public void A_lean_toggled_in_is_kept_through_a_pause_and_a_takeover()
        {
            LeanInput lean = ToggledIn();

            //Covered, or a takeover's frames: the toggle is kept, and the screen asks again once play is back
            lean.SpendPress();
            lean.SpendPress();
            Assert.Equal((true, 1f), Frame(lean, TOGGLE));

            //A button still down when play comes back is not a press until it has been let go
            lean.SpendPress();
            Assert.Equal((true, 1f), Frame(lean, TOGGLE, right: true));
            Assert.Equal((true, 1f), Frame(lean, TOGGLE));
            Assert.Equal((false, 0f), Frame(lean, TOGGLE, right: true));
        }

        [Fact]
        public void Release_lets_go_of_the_toggle()
        {
            LeanInput lean = ToggledIn();
            lean.Release();

            Assert.False(lean.Toggled);
            Assert.Equal((false, 0f), Frame(lean, TOGGLE));
        }

        [Fact]
        public void The_press_that_opens_a_level_and_one_the_edge_gate_refuses_flip_nothing()
        {
            //A button already down on the first frame read
            LeanInput lean = new();
            Assert.Equal((false, 0f), Frame(lean, TOGGLE, right: true));
            Assert.Equal((false, 0f), Frame(lean, TOGGLE));

            //A press on the frame focus returns, and the frames it is held after
            Assert.Equal((false, 0f), Frame(lean, TOGGLE, right: true, edge: false));
            Assert.Equal((false, 0f), Frame(lean, TOGGLE, right: true));
            Assert.False(lean.Toggled);

            //Nor the touch button on that frame
            Assert.Equal((false, 0f), Frame(lean, TOGGLE, touch: true, edge: false));
        }

        [Fact]
        public void With_the_cursor_free_the_right_button_is_the_desktops()
        {
            LeanInput toggled = Settled(TOGGLE);
            Assert.Equal((false, 0f), Frame(toggled, TOGGLE, right: true, captured: false));
            Frame(toggled, TOGGLE, captured: false);
            Assert.Equal((true, 1f), Frame(toggled, TOGGLE, trigger: 1f, captured: false));

            LeanInput held = Settled(HOLD);
            Assert.Equal((false, 0f), Frame(held, HOLD, right: true, captured: false));
            Assert.Equal((true, 1f), Frame(held, HOLD, trigger: 1f, captured: false));
        }

        [Fact]
        public void The_screen_reads_the_lean_through_the_rule_alone()
        {
            Dictionary<string, string[]> sources = ChapterIntroSkipTests.ScreenSources()
                .ToDictionary(path => Path.GetFileName(path), File.ReadAllLines);

            string fault = ScreenFault(sources);
            Assert.True(fault == null, $"The gameplay screen does not read the lean through LeanInput alone: {fault}");

            //The toggle honoured beside the rule, in every mode, as #1140's lines did
            Assert.NotNull(ScreenFault(WithRead(sources,
                "            _lean.Read(Game.PreciseAimToggles, edgeInputAllowed, _touch.Pressed == TouchControls.Button.PreciseAim, _cursorCaptured,",
                "                mouse, pad, out _adsHeld, out _adsPull);",
                "            if (_lean.Toggled)",
                "            {",
                "                _adsHeld = true;",
                "                _adsPull = 1f;",
                "            }")));

            //The rule asked under a mode of the screen's own rather than Settings' as it stands
            Assert.NotNull(ScreenFault(WithRead(sources,
                "            _lean.Read(true, edgeInputAllowed, _touch.Pressed == TouchControls.Button.PreciseAim, _cursorCaptured,",
                "                mouse, pad, out _adsHeld, out _adsPull);")));

            //And not asked at all
            Assert.NotNull(ScreenFault(WithRead(sources)));
        }

        /// <summary>
        /// Why the screen's <paramref name="sources"/> (each file's lines by its name) do not read the lean through
        /// <see cref="LeanInput"/> alone, or null when they do: <c>UpdateAim</c> asks <see cref="LeanInput.Read"/> once, under
        /// <c>Game.PreciseAimToggles</c>, and takes the frame's hold and pull from it, and nothing else in the screen leans the
        /// lens in but a scripted run's <c>rmb=</c>.
        /// </summary>
        private static string ScreenFault(Dictionary<string, string[]> sources)
        {
            string[] updateAim = ChapterIntroSkipTests.MethodBody(sources["GameplayScreen.Input.cs"], UPDATE_AIM)
                .Select(line => ChapterIntroSkipTests.CodeOf(line).Trim()).ToArray();
            string joined = string.Join(" ", updateAim);

            int reads = Regex.Matches(joined, @"_lean\.Read\(").Count;
            if (reads != 1) return $"UpdateAim asks the lean {reads} times";

            if (!Regex.IsMatch(joined, @"_lean\.Read\(Game\.PreciseAimToggles,[^;]*\bout _adsHeld, out _adsPull\);"))
                return "UpdateAim does not ask the lean under Settings' mode for the frame's hold and pull";

            //A lean in is the rule's answer, or the scripted hands': every other write of the hold or the pull lets go
            int scripted = ChapterIntroSkipTests.MethodBody(sources["GameplayScreen.Input.cs"], SCRIPTED_HANDS)
                .Select(ChapterIntroSkipTests.CodeOf).Count(LeansIn);
            if (scripted == 0) return "the scripted hands' rmb= was not found, so the sweep would be reading nothing";

            int all = sources.Values.Sum(lines => lines.Select(ChapterIntroSkipTests.CodeOf).Count(LeansIn));
            if (all != scripted) return $"{all - scripted} lines beside the rule and rmb= lean the lens in";

            return null;
        }

        /// <summary>Whether a line of code writes the hold or the pull anything but let go.</summary>
        private static bool LeansIn(string code) => Regex.IsMatch(code, @"\b_adsHeld = (?!false;)|\b_adsPull = (?!0f;)");

        /// <summary><paramref name="sources"/> with UpdateAim's read of the lean (its two lines) replaced by <paramref name="with"/>.</summary>
        private static Dictionary<string, string[]> WithRead(Dictionary<string, string[]> sources, params string[] with)
        {
            string[] input = sources["GameplayScreen.Input.cs"];
            int at = Array.FindIndex(input, line => line.Contains("_lean.Read(", StringComparison.Ordinal));
            Assert.True(at >= 0, "UpdateAim does not ask _lean.Read");
            Assert.Contains("out _adsHeld, out _adsPull);", input[at + 1]);

            return new Dictionary<string, string[]>(sources)
            {
                ["GameplayScreen.Input.cs"] = input[..at].Concat(with).Concat(input[(at + 2)..]).ToArray()
            };
        }
    }
}
