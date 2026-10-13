using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The 10-bit back buffer's readback (#859): a word of <c>R10G10B10A2_UNORM</c> decodes to the colour it holds, and
    /// the same word read as four bytes — what <c>GetBackBufferData</c> into <c>Color</c>s did — is the wrong colour
    /// the Game's own run showed (cornflower blue cleared, (145, 89, 121) read back). The decode is pinned at both ends
    /// of every channel and against that measured misread, so the writer's shot and the note's picture cannot go back
    /// to reading the words as bytes unseen.
    /// </summary>
    public class BackBufferPixelsTests
    {
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(1023, 1023, 1023, 255, 255, 255)]
        [InlineData(512, 256, 128, 128, 64, 32)]
        [InlineData(401, 598, 951, 100, 149, 237)]
        public void A_word_decodes_to_the_colour_it_holds(int r10, int g10, int b10, int r8, int g8, int b8)
        {
            uint packed = BackBufferPixels.Encode1010102(r10, g10, b10, 3);

            Assert.Equal(new Color(r8, g8, b8, 255), BackBufferPixels.Decode1010102(packed));
        }

        [Fact]
        public void The_word_read_as_bytes_is_the_misread_the_game_showed()
        {
            //Cornflower blue (100, 149, 237) as a 10-bit back buffer holds it, read the old way: the four bytes of the word
            uint packed = BackBufferPixels.Encode1010102(401, 598, 951, 3);
            var asBytes = new Color((int)(packed & 0xFF), (int)((packed >> 8) & 0xFF), (int)((packed >> 16) & 0xFF), (int)(packed >> 24));

            Assert.Equal(new Color(145, 89, 121, 251), asBytes);
            Assert.NotEqual(asBytes, BackBufferPixels.Decode1010102(packed));
        }

        [Fact]
        public void Alpha_is_opaque_whatever_the_two_bits_say()
        {
            Assert.Equal(255, BackBufferPixels.Decode1010102(BackBufferPixels.Encode1010102(10, 20, 30, 0)).A);
        }
    }
}
