using BS3D.Online;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The names a player may not take (#1020), told while typing: the service's denied names and words, a word anywhere in
    /// the name however it is cased, accented, split or spelled with digits, and a short word only as a whole part, so the
    /// ordinary names that hold one stay. The same cases as BS3D-API's <c>NicknameTests</c>, whose lists these mirror.
    /// </summary>
    public class NicknameDeniedTests
    {
        [Theory]
        [InlineData("KuloMrd")]
        [InlineData("zmrd")]
        [InlineData("MRDKA")]
        [InlineData("k_u_r_v_a")]
        [InlineData("kurv4")]
        [InlineData("Kokot 7")]
        [InlineData("Čůrák")]
        [InlineData("Píčus")]
        [InlineData("FuckYou")]
        [InlineData("sh1thead")]
        [InlineData("Hitler88")]
        [InlineData("BigAss")]
        [InlineData("pica123")]
        [InlineData("Píča")]
        [InlineData("kunda")]
        [InlineData("Sex Machine")]
        [InlineData("Admin")]
        [InlineData("Kulo Mrd")]
        [InlineData("Ne Gr")]
        [InlineData("Mr D")]
        [InlineData("f u c k")]
        [InlineData("Kurwa")]
        public void ANameHoldingADeniedWordIsRefused(string raw)
        {
            Assert.False(Nickname.TryNormalize(raw, out string name, out string problem));
            Assert.Null(name);
            Assert.Equal("That name is not allowed.", problem);
        }

        [Theory]
        [InlineData("Picard")]
        [InlineData("Bass")]
        [InlineData("Kundera")]
        [InlineData("Fickle")]
        [InlineData("Analog")]
        [InlineData("Sexton")]
        [InlineData("Hancock")]
        [InlineData("Peacock")]
        [InlineData("Dickens")]
        [InlineData("Swanky")]
        [InlineData("Antonín Pražský")]
        [InlineData("Titan")]
        [InlineData("Cucumber")]
        public void AnOrdinaryNameThatHoldsAShortWordIsKept(string raw)
        {
            Assert.True(Nickname.TryNormalize(raw, out string name, out _));
            Assert.Equal(raw, name);
        }

        /// <summary>
        /// Two ordinary words whose join holds a denied word (#1036): the words are looked in one at a time, and only runs of
        /// one- and two-letter words are joined, so the gap between two real words is never read across.
        /// </summary>
        [Theory]
        [InlineData("Jane Grey")]
        [InlineData("Anne Green")]
        [InlineData("Mr Darcy")]
        [InlineData("Mr_Dobbs")]
        [InlineData("Hans Lutz")]
        [InlineData("Chris Hitchens")]
        [InlineData("Wash It")]
        [InlineData("Pen Isaac")]
        [InlineData("Ben Iggy")]
        [InlineData("Java Gina")]
        public void TwoOrdinaryWordsAreNotReadAcrossTheirGap(string raw)
        {
            Assert.True(Nickname.TryNormalize(raw, out string name, out _));
            Assert.Equal(raw, name);
        }
    }
}
