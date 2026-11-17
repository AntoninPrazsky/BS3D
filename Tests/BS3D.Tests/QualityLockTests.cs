using BS3D.Platform;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #1141: a phone whose GPU cannot carry the desktop's renderer is held at Potato with its rows locked (#1064), and a
    /// Potato the player had stored from the Quality row (#808) must not undo that. <c>Program.Start</c> holds for the
    /// device first and reads the stored choice after; taken up there, the choice set <see cref="QualityLock.ChosenInSettings"/>
    /// again, so the row unlocked on a phone that cannot leave Potato and the run named "chosen in Settings" as its reason.
    /// <para>
    /// The Windows and Android <c>Game/Platform/QualityLock</c> is compiled in. Its state is static, as a run's is, so the
    /// whole of it is one test, in the order a start calls it, and no other test touches it.
    /// </para>
    /// </summary>
    public class QualityLockTests
    {
        private const string DEVICE_WHY = "OpenGL ES 3.0 without EXT_color_buffer_float";

        [Fact]
        public void A_stored_potato_leaves_the_devices_hold_locked()
        {
            //A desktop that can draw either renderer: the stored choice holds the renderer and leaves the row the player's
            Assert.True(QualityLock.ChooseAtPotato());
            Assert.Equal(QualityLevel.Potato, QualityLock.Tier);
            Assert.True(QualityLock.ChosenInSettings);

            //The device's hold, as Program.Start makes it before it reads Settings.json
            Assert.True(QualityLock.HoldForDevice(DEVICE_WHY));
            Assert.False(QualityLock.ChosenInSettings);

            //Then the stored Potato: nothing switched (no second overlay line), the rows still locked (BS3DGame.IsQualityLocked
            //is Tier set and not ChosenInSettings) and the run still says why the device holds it
            Assert.False(QualityLock.ChooseAtPotato());
            Assert.Equal(QualityLevel.Potato, QualityLock.Tier);
            Assert.False(QualityLock.ChosenInSettings);
            Assert.Equal(DEVICE_WHY, QualityLock.Why);
        }
    }
}
