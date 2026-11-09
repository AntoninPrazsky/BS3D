using System.Runtime.InteropServices;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The mark the menu sets beside a build's name (#788, #1053): display only, so the Raspberry Pi's and the phone's
    /// builds stay the same release to the score server while a screenshot still says which one drew it.
    /// </summary>
    public class BuildVersionTests
    {
        [Theory]
        [InlineData(false, Architecture.X64, "v0.5.1")]
        [InlineData(false, Architecture.Arm64, "v0.5.1 ARM64")]
        //A phone's processor is ARM64 too: its system is what the mark names, on any processor
        [InlineData(true, Architecture.Arm64, "v0.5.1 Android")]
        [InlineData(true, Architecture.X64, "v0.5.1 Android")]
        public void TheMenuMarksThePiAndThePhone(bool android, Architecture architecture, string expected) =>
            Assert.Equal(expected, BuildVersion.Marked("v0.5.1", android, architecture));
    }
}
