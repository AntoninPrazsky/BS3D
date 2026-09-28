using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The analog lean (#520): the left trigger leans the lens in as far as it is pulled, the right button is all or
    /// nothing, and a full pull or a button still settles exactly where the binary snap did.
    /// </summary>
    public class PreciseAimTests
    {
        private static GamePadState Pad(float left) =>
            new(new GamePadThumbSticks(), new GamePadTriggers(left, 0f), new GamePadButtons(), new GamePadDPad());

        private static MouseState Mouse(bool right) =>
            new(0, 0, 0, ButtonState.Released, ButtonState.Released,
                right ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);

        [Fact]
        public void TriggerLeansAsFarAsItIsPulled()
        {
            Assert.Equal(0f, PreciseAim.LeanAmount(Mouse(false), Pad(0f)));
            Assert.Equal(0f, PreciseAim.LeanAmount(Mouse(false), Pad(PreciseAim.TRIGGER_REST)));
            Assert.Equal(1f, PreciseAim.LeanAmount(Mouse(false), Pad(PreciseAim.TRIGGER_FULL)));
            Assert.Equal(1f, PreciseAim.LeanAmount(Mouse(false), Pad(1f)));

            float mid = (PreciseAim.TRIGGER_REST + PreciseAim.TRIGGER_FULL) / 2f;
            Assert.Equal(0.5f, PreciseAim.LeanAmount(Mouse(false), Pad(mid)), 4);
        }

        [Fact]
        public void TheButtonIsAllOrNothing()
        {
            Assert.Equal(1f, PreciseAim.LeanAmount(Mouse(true), Pad(0f)));
            Assert.Equal(1f, PreciseAim.LeanAmount(Mouse(true), Pad(0.3f)));
        }

        [Fact]
        public void AHalfPullHoldsAHalfLean()
        {
            var aim = new PreciseAim();
            for (int i = 0; i < 120; i++) aim.Step(0.5f, 1f / 60f, 20f);
            Assert.Equal(0.5f, aim.Blend, 4);

            for (int i = 0; i < 120; i++) aim.Step(1f, 1f / 60f, 20f);
            Assert.Equal(1f, aim.Blend);

            for (int i = 0; i < 120; i++) aim.Step(false, 1f / 60f, 20f);
            Assert.Equal(0f, aim.Blend);
        }
    }
}
