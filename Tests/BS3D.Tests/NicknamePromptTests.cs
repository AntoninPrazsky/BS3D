using BS3D.Online;
using BS3D.Screens;
using System.IO;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The first-launch nickname question (#763): when the game asks, what Esc on it counts for, what the field accepts and
    /// the setting's three states through the file. The plate itself is Myra and needs a window; everything it decides by
    /// is plain values and is held here.
    /// </summary>
    public class NicknamePromptTests
    {
        private static NicknameAsk Decide(bool? online, bool server = true, bool scripted = false, bool internet = true) =>
            NicknamePrompt.Decide(online, server, scripted, () => internet);

        #region Whether to ask

        [Fact]
        public void AnUndecidedPlayerWithAServerAndInternetIsAsked() =>
            Assert.Equal(NicknameAsk.Ask, Decide(null));

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AnyDecisionSilencesTheQuestion(bool decided) =>
            Assert.Equal(NicknameAsk.Decided, Decide(decided));

        [Fact]
        public void AScriptedRunIsNeverAsked() =>
            Assert.Equal(NicknameAsk.Scripted, Decide(null, scripted: true));

        [Fact]
        public void ABuildWithNoServerIsNeverAsked() =>
            Assert.Equal(NicknameAsk.NoServer, Decide(null, server: false));

        [Fact]
        public void AMachineOffTheInternetIsNotAskedYet() =>
            Assert.Equal(NicknameAsk.NoInternet, Decide(null, internet: false));

        [Fact]
        public void TheGatesAreReadCheapestFirstAndTheConnectionOnlyWhenEverythingElseSaysAsk()
        {
            //Every verdict but Ask and NoInternet is reached without asking Windows about the network: the check is a
            //call into the OS, and it is not made for a player who has answered, a script, or a build with no server
            foreach ((bool? online, bool server, bool scripted) in new (bool?, bool, bool)[]
                { (true, true, false), (false, true, false), (null, true, true), (null, false, false), (false, false, true) })
            {
                int calls = 0;
                NicknamePrompt.Decide(online, server, scripted, () => { calls++; return true; });
                Assert.Equal(0, calls);
            }

            int asked = 0;
            Assert.Equal(NicknameAsk.Ask, NicknamePrompt.Decide(null, true, false, () => { asked++; return true; }));
            Assert.Equal(1, asked);
        }

        [Fact]
        public void ADecisionBeatsEveryOtherReason()
        {
            Assert.Equal(NicknameAsk.Decided, Decide(false, server: false, scripted: true, internet: false));
            Assert.Equal(NicknameAsk.Scripted, Decide(null, server: false, scripted: true, internet: false));
            Assert.Equal(NicknameAsk.NoServer, Decide(null, server: false, internet: false));
        }

        #endregion

        #region What Esc counts for

        [Fact]
        public void TheFirstEscLeavesItUndecidedAndTheSecondIsSkip()
        {
            Assert.False(NicknamePrompt.Dismiss(0, out int first));
            Assert.Equal(1, first);

            Assert.True(NicknamePrompt.Dismiss(first, out int second));
            Assert.Equal(2, second);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(7)]
        public void AnyCountPastTheSecondIsSkipToo(int before) =>
            Assert.True(NicknamePrompt.Dismiss(before, out _));

        [Fact]
        public void AHandEditedCountCannotWrapRoundOrGoNegative()
        {
            Assert.True(NicknamePrompt.Dismiss(int.MaxValue, out int saturated));
            Assert.Equal(int.MaxValue, saturated);

            //A negative count is read as none: the first Esc after it is the first
            Assert.False(NicknamePrompt.Dismiss(-5, out int afterNegative));
            Assert.Equal(1, afterNegative);
        }

        #endregion

        #region The field

        private static NicknameEntry Typed(string text)
        {
            NicknameEntry entry = new();
            entry.Begin(null);
            foreach (char c in text) entry.Type(c);
            return entry;
        }

        [Fact]
        public void LettersAreTypedAndBackspaceTakesOneBack()
        {
            NicknameEntry entry = Typed("Novak");
            Assert.Equal("Novak", entry.Draft);

            Assert.Equal(NicknameKey.Edited, entry.Type('\b'));
            Assert.Equal("Nova", entry.Draft);

            //Backspace on nothing is still an edit, and does nothing
            NicknameEntry empty = Typed("");
            Assert.Equal(NicknameKey.Edited, empty.Type('\b'));
            Assert.Equal(string.Empty, empty.Draft);
        }

        [Fact]
        public void EnterAndEscapeAreReportedAndNeverTyped()
        {
            NicknameEntry entry = Typed("Novak");

            Assert.Equal(NicknameKey.Enter, entry.Type('\r'));
            Assert.Equal(NicknameKey.Escape, entry.Type('\x1b'));
            Assert.Equal("Novak", entry.Draft);
        }

        [Fact]
        public void ACharacterNoNicknameMayHoldIsIgnored()
        {
            NicknameEntry entry = Typed("Ab");

            Assert.Equal(NicknameKey.Ignored, entry.Type('!'));
            Assert.Equal(NicknameKey.Ignored, entry.Type('\t'));
            Assert.Equal("Ab", entry.Draft);
        }

        [Fact]
        public void NothingPastTheLongestNameIsTaken()
        {
            NicknameEntry entry = Typed(new string('a', Nickname.MaxLength));
            Assert.Equal(Nickname.MaxLength, entry.Draft.Length);

            Assert.Equal(NicknameKey.Ignored, entry.Type('b'));
            Assert.Equal(Nickname.MaxLength, entry.Draft.Length);
        }

        [Fact]
        public void ANameThatIsOneIsKeptAndOneThatIsNotSaysWhatIsWrong()
        {
            NicknameEntry good = Typed("  Karel   Novak ");
            Assert.True(good.TryKeep(out string name));
            Assert.Equal("Karel Novak", name);
            Assert.Null(good.Problem);

            NicknameEntry tooShort = Typed("ab");
            Assert.False(tooShort.TryKeep(out _));
            Assert.False(string.IsNullOrEmpty(tooShort.Problem));

            //A complaint is for the name that was kept, so the next key takes it away
            tooShort.Type('c');
            Assert.Null(tooShort.Problem);
            Assert.True(tooShort.TryKeep(out string fixedName));
            Assert.Equal("abc", fixedName);
        }

        [Fact]
        public void KeepableIsSaidWithoutLeavingAComplaint()
        {
            NicknameEntry entry = Typed("ab");

            Assert.False(entry.IsKeepable);
            Assert.Null(entry.Problem);

            entry.Type('c');
            Assert.True(entry.IsKeepable);
        }

        [Fact]
        public void BeginStartsFromTheGivenNameWithTheCaretOnAndNoComplaint()
        {
            NicknameEntry entry = Typed("ab");
            entry.TryKeep(out _);
            Assert.NotNull(entry.Problem);

            entry.Begin("Anton");
            Assert.Equal("Anton", entry.Draft);
            Assert.Null(entry.Problem);
            Assert.True(entry.CaretShown);
            Assert.Equal("Anton" + NicknameEntry.CARET, entry.Field);
        }

        [Fact]
        public void TheCaretBlinksOnceASecondAndEveryKeyBringsItBack()
        {
            NicknameEntry entry = Typed("Ab");
            Assert.True(entry.CaretShown);
            Assert.Equal("Ab" + NicknameEntry.CARET, entry.Field);

            //On for the first half of the second, off for the second half; the label is rewritten only when it flips
            Assert.False(entry.Tick(0.3f));
            Assert.False(entry.Tick(0.1f));
            Assert.True(entry.Tick(0.2f));
            Assert.False(entry.CaretShown);
            Assert.Equal("Ab", entry.Field);
            Assert.Equal("Ab" + NicknameEntry.CARET, entry.FieldWithCaret);

            Assert.False(entry.Tick(0.1f));
            Assert.True(entry.Tick(0.4f));
            Assert.True(entry.CaretShown);

            //A key restarts the clock with the caret on, so it is never off under the player's fingers
            entry.Tick(0.6f);
            Assert.False(entry.CaretShown);
            entry.Type('c');
            Assert.True(entry.CaretShown);
        }

        #endregion

        #region The setting, through the file

        private static GameSettings Reload(GameSettings settings) => GameSettings.Load(settings.Path);

        private static string Written(GameSettings settings) => File.ReadAllText(settings.Path);

        [Fact]
        public void AFreshInstallIsUndecidedAndWritesNoOnlineKey()
        {
            using TempDirectory temp = new();
            GameSettings settings = GameSettings.Load(temp.File("Settings.json"));

            Assert.Null(settings.Online);
            Assert.Equal(0, settings.NicknameDismissed);

            settings.Save();
            Assert.DoesNotContain("\"online\"", Written(settings));
            Assert.DoesNotContain("nicknameDismissed", Written(settings));
            Assert.Null(Reload(settings).Online);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ADecisionSurvivesTheFileEitherWay(bool decided)
        {
            using TempDirectory temp = new();
            GameSettings settings = GameSettings.Load(temp.File("Settings.json"));

            settings.Online = decided;
            settings.Save();

            //False is written as a key too: it is a decision, not an absence
            Assert.Contains($"\"online\": {(decided ? "true" : "false")}", Written(settings));
            Assert.Equal(decided, Reload(settings).Online);
        }

        [Fact]
        public void AFileWrittenBeforeTheQuestionThatSaysFalseReadsAsADecision()
        {
            //Every file written since #546 carries the key, so a false there cannot be told from an untouched one and is
            //taken as a decision: not asking is the safe way to be wrong
            using TempDirectory temp = new();
            string path = temp.File("Settings.json");
            File.WriteAllText(path, "{ \"format\": \"bs3d-settings\", \"version\": 1, \"online\": false }");

            GameSettings settings = GameSettings.Load(path);

            Assert.False(settings.Online);
            Assert.Equal(NicknameAsk.Decided, NicknamePrompt.Decide(settings.Online, true, false, () => true));
        }

        [Fact]
        public void AFileThatNeverSaidAnythingAboutItReadsAsUndecided()
        {
            using TempDirectory temp = new();
            string path = temp.File("Settings.json");
            File.WriteAllText(path, "{ \"format\": \"bs3d-settings\", \"version\": 1, \"quality\": \"High\" }");

            Assert.Null(GameSettings.Load(path).Online);
        }

        [Fact]
        public void TheDismissalCountIsKeptWhileNonzeroAndGoneAtZero()
        {
            using TempDirectory temp = new();
            GameSettings settings = GameSettings.Load(temp.File("Settings.json"));

            settings.NicknameDismissed = 1;
            settings.Save();
            Assert.Contains("\"nicknameDismissed\": 1", Written(settings));
            Assert.Equal(1, Reload(settings).NicknameDismissed);
            Assert.Null(Reload(settings).Online);

            settings.NicknameDismissed = 0;
            settings.Save();
            Assert.DoesNotContain("nicknameDismissed", Written(settings));
        }

        [Fact]
        public void TwoEscsThroughTheSettingsEndAsAnExplicitOff()
        {
            //What OnlineSession.DismissNickname does to the file, step by step: the first Esc keeps the count and no
            //decision; the second turns it into a false and drops the count, and the question is never asked again
            using TempDirectory temp = new();
            GameSettings settings = GameSettings.Load(temp.File("Settings.json"));

            bool skip = NicknamePrompt.Dismiss(settings.NicknameDismissed, out int count);
            settings.NicknameDismissed = skip ? 0 : count;
            settings.Save();

            GameSettings afterFirst = Reload(settings);
            Assert.False(skip);
            Assert.Null(afterFirst.Online);
            Assert.Equal(NicknameAsk.Ask, NicknamePrompt.Decide(afterFirst.Online, true, false, () => true));

            skip = NicknamePrompt.Dismiss(afterFirst.NicknameDismissed, out count);
            afterFirst.NicknameDismissed = skip ? 0 : count;
            if (skip) afterFirst.Online = false;
            afterFirst.Save();

            GameSettings afterSecond = Reload(settings);
            Assert.True(skip);
            Assert.False(afterSecond.Online);
            Assert.Equal(0, afterSecond.NicknameDismissed);
            Assert.Equal(NicknameAsk.Decided, NicknamePrompt.Decide(afterSecond.Online, true, false, () => true));
        }

        #endregion
    }
}
