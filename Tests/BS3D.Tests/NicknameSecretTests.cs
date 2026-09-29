using BS3D.Online;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The code-word egg of #230: a nickname that is one of the secret words puts the party hat on the gun. What is held
    /// here is the rule — which names count and which do not — because the hat itself is drawn by the Game and cannot be
    /// run without a device. The rule must be loose enough that a player who guesses the word finds it (case, the
    /// separators a nickname may hold) and tight enough that nobody wears the hat by accident (a name that merely
    /// contains a secret word, or is a prefix of one).
    /// </summary>
    public class NicknameSecretTests
    {
        [Theory]
        [InlineData("bepu")]
        [InlineData("BEPU")]
        [InlineData("Bepu")]
        [InlineData("bepuphysics")]
        [InlineData("Bepu Physics")]
        [InlineData("bepu_physics")]
        [InlineData("bepu-physics")]
        [InlineData("MonoGame")]
        [InlineData("mono game")]
        [InlineData("Prazsky")]
        [InlineData("PRAZSKY")]
        public void ASecretWordIsRecognisedWhateverItsCaseAndSeparators(string name) =>
            Assert.True(Nickname.IsSecretWord(name));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Anton")]
        [InlineData("bepus")]           //a secret word with a letter more
        [InlineData("bep")]             //and with one fewer
        [InlineData("mono")]            //a prefix of one
        [InlineData("xbepu")]           //one that contains a secret word
        [InlineData("prazsky2")]
        [InlineData("Pražský")]         //the author's name spelled properly is not the word: the accents are letters
        public void AnythingElseIsNot(string name) =>
            Assert.False(Nickname.IsSecretWord(name));
    }
}
