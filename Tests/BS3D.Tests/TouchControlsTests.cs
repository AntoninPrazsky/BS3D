using BS3D.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The touch scheme the owner chose (#1050), on a 3168 x 1440 landscape phone: the aiming side's drag turns the gun,
    /// a tap on the firing side fires and a drag there is a stick, a slipped tap still fires and a resting thumb does not,
    /// one finger per control, a button takes the touch that lands on it, and swapping the sides swaps them.
    /// </summary>
    public class TouchControlsTests
    {
        private const int W = 3168, H = 1440;
        private const float FRAME = 1f / 60f;

        private static readonly Vector2 AIM = new(800f, 900f), FIRE = new(2400f, 900f);

        private static void Step(TouchControls controls, params TouchLocation[] touches) =>
            controls.Update(new TouchCollection(touches), W, H, FRAME, _ => TouchControls.Button.None);

        private static TouchLocation Down(int id, Vector2 at) => new(id, TouchLocationState.Pressed, at);
        private static TouchLocation Move(int id, Vector2 at) => new(id, TouchLocationState.Moved, at);
        private static TouchLocation Up(int id, Vector2 at) => new(id, TouchLocationState.Released, at);

        [Fact]
        public void TheAimingSidesDragIsTheAim()
        {
            TouchControls controls = new();
            Step(controls, Down(1, AIM));
            Assert.Equal(Vector2.Zero, controls.AimDelta);
            Assert.True(controls.AnyPress);

            Step(controls, Move(1, AIM + new Vector2(30f, -12f)));
            Assert.Equal(new Vector2(30f, -12f), controls.AimDelta);
            Assert.False(controls.Fire);

            //Lifting the aiming finger fires nothing
            Step(controls, Up(1, AIM + new Vector2(30f, -12f)));
            Assert.False(controls.Fire);
            Assert.False(controls.Touching);
        }

        [Fact]
        public void ATapOnTheFiringSideFiresAndASlippedOneStill()
        {
            TouchControls controls = new();
            Step(controls, Down(1, FIRE));
            Assert.False(controls.Fire);
            Step(controls, Up(1, FIRE));
            Assert.True(controls.Fire);

            //Slipped by less than the slop
            float slip = TouchControls.TAP_SLOP * H * 0.8f;
            Step(controls, Down(2, FIRE));
            Step(controls, Move(2, FIRE + new Vector2(slip, 0f)));
            Assert.False(controls.StickActive);
            Step(controls, Up(2, FIRE + new Vector2(slip, 0f)));
            Assert.True(controls.Fire);

            //Down and up between two frames
            Step(controls, Up(3, FIRE));
            Assert.True(controls.Fire);
        }

        [Fact]
        public void AThumbRestingOnTheFiringSideFiresNothing()
        {
            TouchControls controls = new();
            Step(controls, Down(1, FIRE));
            for (float t = 0f; t < TouchControls.TAP_SECONDS + 0.1f; t += FRAME) Step(controls, Move(1, FIRE));
            Step(controls, Up(1, FIRE));
            Assert.False(controls.Fire);
        }

        [Fact]
        public void ADragOnTheFiringSideIsAStickAndNeverFires()
        {
            TouchControls controls = new();
            float slop = TouchControls.TAP_SLOP * H, radius = TouchControls.STICK_RADIUS * H;

            Step(controls, Down(1, FIRE));
            //Just past the slop the stick asks almost nothing, so a slow walk is possible
            Step(controls, Move(1, FIRE + new Vector2(0f, -(slop + 1f))));
            Assert.True(controls.StickActive);
            Assert.InRange(controls.Stick.Y, 0f, 0.02f);

            //Up the screen is up the stick: half the radius past the slop is half a push
            Step(controls, Move(1, FIRE + new Vector2(0f, -(slop + radius * 0.5f))));
            Assert.Equal(0.5f, controls.Stick.Y, 3);
            Assert.Equal(0f, controls.Stick.X, 3);

            //Past the full push it is a full push, and the base follows, so turning back answers at once
            Step(controls, Move(1, FIRE + new Vector2(0f, -(slop + radius * 3f))));
            Assert.Equal(1f, controls.Stick.Y, 3);
            Step(controls, Move(1, FIRE + new Vector2(0f, -(slop + radius * 2.5f))));
            Assert.True(controls.Stick.Y < 0.7f);

            Step(controls, Up(1, FIRE));
            Assert.False(controls.Fire);
            Assert.False(controls.StickActive);
            Assert.Equal(Vector2.Zero, controls.Stick);
        }

        [Fact]
        public void OneFingerAimsAtATimeAndATapBesideAStickStillFires()
        {
            TouchControls controls = new();
            Step(controls, Down(1, AIM), Down(2, AIM + new Vector2(200f, 0f)));
            Step(controls, Move(1, AIM + new Vector2(10f, 0f)), Move(2, AIM + new Vector2(260f, 0f)));
            Assert.Equal(new Vector2(10f, 0f), controls.AimDelta);

            //The thumb walking the gun, and another finger tapping beside it
            float far = (TouchControls.TAP_SLOP + TouchControls.STICK_RADIUS) * H;
            Step(controls, Move(1, AIM), Move(2, AIM), Down(3, FIRE));
            Step(controls, Move(1, AIM), Move(2, AIM), Move(3, FIRE + new Vector2(far, 0f)));
            Assert.True(controls.StickActive);
            Step(controls, Move(1, AIM), Move(2, AIM), Move(3, FIRE + new Vector2(far, 0f)), Down(4, new Vector2(2900f, 300f)));
            Step(controls, Move(1, AIM), Move(2, AIM), Move(3, FIRE + new Vector2(far, 0f)), Up(4, new Vector2(2900f, 300f)));
            Assert.True(controls.Fire);
            Assert.True(controls.StickActive);
        }

        [Fact]
        public void AButtonTakesTheTouchThatLandsOnIt()
        {
            TouchControls controls = new();
            Rectangle toggle = new(2900, 500, 200, 200);
            TouchControls.Button Hit(Vector2 at) =>
                toggle.Contains(at) ? TouchControls.Button.PreciseAim : TouchControls.Button.None;

            controls.Update(new TouchCollection(new[] { Down(1, new Vector2(3000f, 600f)) }), W, H, FRAME, Hit);
            Assert.Equal(TouchControls.Button.PreciseAim, controls.Pressed);

            //Held, dragged off and lifted, it neither fires nor moves the gun nor presses again
            controls.Update(new TouchCollection(new[] { Move(1, new Vector2(2400f, 900f)) }), W, H, FRAME, Hit);
            Assert.Equal(TouchControls.Button.None, controls.Pressed);
            Assert.False(controls.StickActive);
            controls.Update(new TouchCollection(new[] { Up(1, new Vector2(2400f, 900f)) }), W, H, FRAME, Hit);
            Assert.False(controls.Fire);
        }

        [Fact]
        public void SwappingTheSidesSwapsThem()
        {
            TouchControls controls = new() { AimOnRight = true };
            Step(controls, Down(1, FIRE));
            Step(controls, Move(1, FIRE + new Vector2(25f, 0f)));
            Assert.Equal(new Vector2(25f, 0f), controls.AimDelta);
            Step(controls, Up(1, FIRE));
            Assert.False(controls.Fire);

            Step(controls, Down(2, AIM));
            Step(controls, Up(2, AIM));
            Assert.True(controls.Fire);
        }

        [Fact]
        public void PlayAdoptsWhatIsAlreadyDownAndALiftIsNotATap()
        {
            TouchControls controls = new();
            //The finger that pressed Resume lifting on play's first frame, and a thumb still resting on each side
            controls.Adopt(new TouchCollection(new[] { Up(9, FIRE), Move(1, AIM), Move(2, FIRE) }));
            Assert.False(controls.Fire);

            Step(controls, Move(1, AIM + new Vector2(40f, 0f)), Move(2, FIRE));
            Assert.Equal(Vector2.Zero, controls.AimDelta);
            Step(controls, Up(1, AIM), Up(2, FIRE));
            Assert.False(controls.Fire);

            //And the next touch is play's
            Step(controls, Down(3, FIRE));
            Step(controls, Up(3, FIRE));
            Assert.True(controls.Fire);
        }
    }
}
