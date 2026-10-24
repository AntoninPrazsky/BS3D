using BS3D.Screens;
using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The overview's focus on the aim (#1006), the owner's ruling: the full frame at a level's opening, a focus on the aim
    /// after a moment that then stays, the full frame again whenever precise aim or a takeover has held the lens and a focus
    /// after a shorter moment, the same at every frame rate and never past either end; and a focused pose that is still
    /// "longer" than precise aim and keeps some of the gun in the frame (#720's condition).
    /// </summary>
    public class OverviewFocusTests
    {
        //The Game's overview field of view (GameplayScreen.GAME_FOV, pi / 4.2) and the Zigzag opening pose of the
        //[tilt] lines of #1006: the lens, the look-at, the muzzle and where the beam first met the cluster
        private static readonly float GAME_FOV = MathF.PI / 4.2f;
        private static readonly Vector3 LENS = new(0f, -7.4f, 30.6f);
        private static readonly Vector3 LOOK_AT = new(0f, -1.2f, 0f);
        private static readonly Vector3 MUZZLE = new(0f, -5.1f, 15f);
        private static readonly Vector3 AIM_POINT = new(0f, 3.5f, 7.5f);

        //Zigzag's top row: the glass's underside over the plate footprint's four corners (17 cells, a half-extent of 9)
        private static readonly Vector3[] TOP_ROW =
        {
            new(-9f, 5.3f, -9f), new(9f, 5.3f, -9f), new(-9f, 5.3f, 9f), new(9f, 5.3f, 9f),
        };

        //Runs the focus for a span of seconds at a frame rate, the overview holding the lens or not
        private static void Run(OverviewFocus focus, bool overview, float seconds, float hz)
        {
            int frames = (int)MathF.Round(seconds * hz);
            for (int i = 0; i < frames; i++) focus.Step(overview, false, 1f / hz);
        }

        [Fact]
        public void TheOpeningHoldsTheFullFrameThenFocusesAndStays()
        {
            var focus = new OverviewFocus();

            Run(focus, true, OverviewFocus.HOLD_OPENING_SECONDS - 0.05f, 120f);
            Assert.Equal(0f, focus.Amount);

            Run(focus, true, 3f, 120f);
            Assert.True(focus.Amount > 0.99f, $"three seconds after the hold the focus was only {focus.Amount}");

            Run(focus, true, 30f, 120f);
            Assert.Equal(1f, focus.Amount);
        }

        [Fact]
        public void LeavingTheOverviewOpensOnTheFullFrameAndAReturnFocusesSooner()
        {
            var focus = new OverviewFocus();
            Run(focus, true, 10f, 120f);
            Assert.Equal(1f, focus.Amount);

            //A press of precise aim: most of the way out in a quarter second, all the way out while it is held
            Run(focus, false, 0.25f, 120f);
            Assert.True(focus.Amount < 0.1f, $"a quarter second into the lean the focus was still {focus.Amount}");
            Run(focus, false, 1f, 120f);
            Assert.Equal(0f, focus.Amount);

            //The release lands on the full frame, which holds for the return's moment and not the opening's
            Run(focus, true, OverviewFocus.HOLD_RETURN_SECONDS - 0.05f, 120f);
            Assert.Equal(0f, focus.Amount);
            Run(focus, true, 0.6f, 120f);
            Assert.True(focus.Amount > 0f, "the focus did not start after the return's hold");
            Assert.True(OverviewFocus.HOLD_RETURN_SECONDS < OverviewFocus.HOLD_OPENING_SECONDS);
        }

        [Fact]
        public void TheChaptersTourHandsOverAnOpeningNotAReturn()
        {
            var focus = new OverviewFocus();
            for (int i = 0; i < 120 * 8; i++) focus.Step(false, true, 1f / 120f);

            //The tour's end is the level's opening: the full frame holds through the cluster's start swing
            Run(focus, true, OverviewFocus.HOLD_RETURN_SECONDS + 0.5f, 120f);
            Assert.Equal(0f, focus.Amount);
            Run(focus, true, OverviewFocus.HOLD_OPENING_SECONDS, 120f);
            Assert.True(focus.Amount > 0f);
        }

        [Fact]
        public void ALoadStartsOverOnTheOpeningsHold()
        {
            var focus = new OverviewFocus();
            Run(focus, true, 10f, 120f);

            //Leaned for a frame: still most of the way focused, and the next hold would be a return's
            focus.Step(false, false, 1f / 120f);
            Assert.True(focus.Amount > 0.5f);

            focus.Reset();
            Assert.Equal(0f, focus.Amount);
            Run(focus, true, OverviewFocus.HOLD_RETURN_SECONDS + 0.5f, 120f);
            Assert.Equal(0f, focus.Amount);
        }

        [Fact]
        public void TheFocusIsTheSameAtEveryFrameRateAndNeverOvershoots()
        {
            var slow = new OverviewFocus();
            var fast = new OverviewFocus();
            float previous = 0f;

            //Sampled on the slow clock's frames, at 7 and at 252 a second: the 2.5 s hold ends half-way through a slow frame,
            //so a frame that rose for the whole of its length would be a third of a frame ahead
            for (int frame = 0; frame < 7 * 6; frame++)
            {
                slow.Step(true, false, 1f / 7f);
                for (int i = 0; i < 36; i++) fast.Step(true, false, 1f / 252f);

                Assert.True(MathF.Abs(slow.Amount - fast.Amount) < 0.01f,
                    $"at {frame / 7f:F2} s the focus was {slow.Amount} at 7 Hz and {fast.Amount} at 252 Hz");
                Assert.True(fast.Amount >= previous - 1e-6f, $"the rise fell back from {previous} to {fast.Amount}");
                previous = fast.Amount;
            }
        }

        [Fact]
        public void TheFocusedPoseIsLongerThanPreciseAimAndKeepsSomeOfTheGun()
        {
            AimPose focused = OverviewFocus.Focused(LENS, LOOK_AT, GAME_FOV, AIM_POINT, MUZZLE, TOP_ROW);

            //The owner's "longer than precise aim": a wider field of view, and a lens further back from the muzzle
            Assert.True(focused.FieldOfView > PreciseAim.FOV, $"the focused field of view {focused.FieldOfView} is not wider than precise aim's");
            Vector3 aim = Vector3.Normalize(AIM_POINT - MUZZLE);
            Assert.True(Vector3.Distance(focused.Position, MUZZLE) > Vector3.Distance(PreciseAim.LensPosition(MUZZLE, aim), MUZZLE));

            //Closer than the overview, and higher: the rise is what takes the gun down the frame
            Assert.True(Vector3.Distance(focused.Position, AIM_POINT) < Vector3.Distance(LENS, AIM_POINT));
            Assert.True(focused.Position.Y > LENS.Y);

            //#720's condition: the muzzle stays at least the gun keep over the frame's bottom edge
            Assert.True(AimTilt.GunKeepLimit(focused.Position, focused.Target, MUZZLE, focused.FieldOfView) >= -1e-4f);
        }

        [Fact]
        public void TheGunKeepTurnsTheFocusDownWhenTheAimWouldTakeTheGunOutOfTheFrame()
        {
            //An aim point high over the field pulls the look-at up past where the muzzle could stay in the frame
            Vector3 high = new(0f, 30f, 0f);
            Vector3 lens = LENS + Vector3.Up * OverviewFocus.RISE + Vector3.Forward * OverviewFocus.ADVANCE;
            Vector3 pulled = Vector3.Lerp(LOOK_AT, high, OverviewFocus.PULL);
            Assert.True(AimTilt.GunKeepLimit(lens, pulled, MUZZLE, GAME_FOV * OverviewFocus.FOV_SCALE) < 0f,
                "the rig does not reach the case: the pulled look-at keeps the muzzle on its own");

            AimPose focused = OverviewFocus.Focused(LENS, LOOK_AT, GAME_FOV, high, MUZZLE, TOP_ROW);
            Assert.Equal(0f, AimTilt.GunKeepLimit(focused.Position, focused.Target, MUZZLE, focused.FieldOfView), 3);
        }

        [Fact]
        public void ALowAimKeepsTheTopRowWhereTheGunAllows()
        {
            //An aim low on the field pulls the look-at down until the top row leaves the frame's top
            Vector3 low = new(0f, -3f, 10f);
            AimPose bare = OverviewFocus.Focused(LENS, LOOK_AT, GAME_FOV, low, MUZZLE, ReadOnlySpan<Vector3>.Empty);
            float lostBare = TopRowLost(bare);
            Assert.True(lostBare > 1e-3f, "the rig does not reach the case: the low aim keeps the top row on its own");

            //With the row handed in, the lens turns back up: the row is on its line, or the gun keep stopped it
            AimPose kept = OverviewFocus.Focused(LENS, LOOK_AT, GAME_FOV, low, MUZZLE, TOP_ROW);
            float gunRoom = AimTilt.GunKeepLimit(kept.Position, kept.Target, MUZZLE, kept.FieldOfView);
            Assert.True(TopRowLost(kept) < 1e-3f || MathF.Abs(gunRoom) < 1e-3f,
                $"the top row is still {TopRowLost(kept)} rad off the frame with {gunRoom} rad of the gun's room left");
            Assert.True(gunRoom >= -1e-4f, "keeping the top row broke the gun keep");
            Assert.True(TopRowLost(kept) < lostBare);
        }

        //How far past the top keep's line the worst top corner stands in a pose, in radians (0 when all are inside)
        private static float TopRowLost(AimPose pose)
        {
            float lost = 0f;
            foreach (Vector3 corner in TOP_ROW)
                lost = MathF.Max(lost, -AimTilt.TopKeepLimit(pose.Position, pose.Target, corner, pose.FieldOfView));
            return lost;
        }

        [Fact]
        public void ALeanBeforeTheFirstFocusKeepsTheOpeningsHold()
        {
            //RMB tried in the first second: the cluster's start swing is still to be seen whole
            var focus = new OverviewFocus();
            Run(focus, true, 0.5f, 120f);
            Run(focus, false, 0.5f, 120f);
            Run(focus, true, OverviewFocus.HOLD_RETURN_SECONDS + 0.5f, 120f);
            Assert.Equal(0f, focus.Amount);
            Run(focus, true, OverviewFocus.HOLD_OPENING_SECONDS, 120f);
            Assert.True(focus.Amount > 0f);
        }

        [Fact]
        public void TheAimPointIsChasedNotJumpedTo()
        {
            var focus = new OverviewFocus();
            Vector3 aim = Vector3.Normalize(AIM_POINT - MUZZLE);

            Vector3 first = focus.FollowAim(MUZZLE, aim, 10f, 1f / 120f);
            Assert.Equal(MUZZLE + aim * 10f, first);

            //The beam's contact jumps from a near ball to a far one: the point moves a little on the next frame, not the jump
            Vector3 next = focus.FollowAim(MUZZLE, aim, 25f, 1f / 120f);
            float moved = Vector3.Distance(first, next);
            Assert.True(moved > 0f && moved < 1f, $"the point moved {moved} of a 15-unit jump in one frame");

            //And arrives in the end
            for (int i = 0; i < 120 * 5; i++) next = focus.FollowAim(MUZZLE, aim, 25f, 1f / 120f);
            Assert.True(Vector3.Distance(next, MUZZLE + aim * 25f) < 0.01f);

            //A depth behind the muzzle (an aim swung far off the cluster) is held to precise aim's own range, and a NaN is
            //not taken at all
            for (int i = 0; i < 120 * 5; i++) next = focus.FollowAim(MUZZLE, aim, -40f, 1f / 120f);
            Assert.True(Vector3.Distance(next, MUZZLE + aim * PreciseAim.CONVERGE_MIN) < 0.01f);
            next = focus.FollowAim(MUZZLE, aim, float.NaN, 1f / 120f);
            Assert.True(float.IsFinite(next.X) && float.IsFinite(next.Y) && float.IsFinite(next.Z));
        }
    }
}
