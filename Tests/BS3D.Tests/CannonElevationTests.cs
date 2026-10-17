using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameObjects;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The two ends of the elevation clamp are not alike (#872). The upper one keeps its alarm and its refusal to fire
    /// (#431, #811, #842): it closes a band of cluster the camera does not frame. The lower one is a soft stop and no
    /// more: pushing under it never raises the strain, never refuses a shot, and the rubber lets go without swinging
    /// back past the clamp.
    /// </summary>
    public class CannonElevationTests
    {
        private static readonly GameTime Frame = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(16));

        private static Cannon NewCannon() => new(new Vector3(0f, 5f, 0f));

        //Pushes the aim by `direction` (+1 up, -1 down) for `frames` frames, calling `each` after every one
        private static void Push(Cannon cannon, float direction, int frames, Action each = null)
        {
            for (int i = 0; i < frames; i++)
            {
                cannon.Aim(new Vector2(direction, 0f), Frame);
                cannon.Update(Frame);
                each?.Invoke();
            }
        }

        private static void Idle(Cannon cannon, int frames, Action each = null)
        {
            for (int i = 0; i < frames; i++)
            {
                cannon.Update(Frame);
                each?.Invoke();
            }
        }

        [Fact]
        public void APushUnderTheLowerLimitNeverRaisesTheStrain()
        {
            Cannon cannon = NewCannon();
            float worst = 0f;

            Push(cannon, -1f, 200, () => worst = MathF.Max(worst, cannon.ElevationStrain));
            Idle(cannon, 60, () => worst = MathF.Max(worst, cannon.ElevationStrain));

            Assert.Equal(0f, worst);
        }

        [Fact]
        public void TheLowerLimitNeverRefusesAShot()
        {
            Cannon cannon = NewCannon();
            bool refused = false;

            Push(cannon, -1f, 200, () => refused |= cannon.ElevationRefusesShot);
            Idle(cannon, 120, () => refused |= cannon.ElevationRefusesShot);

            Assert.False(refused);
        }

        [Fact]
        public void TheLowerRubberStretchesUnderTheClampAndEasesBackWithoutAnyRebound()
        {
            Cannon cannon = NewCannon();

            Push(cannon, -1f, 200);

            //Pushed under the clamp: stretched, but never beyond the rubber
            Assert.True(cannon.ElevationOvershoot < 0f);
            Assert.True(cannon.ElevationOvershoot >= -Cannon.ELEVATION_OVERSHOOT - 1e-4f);

            //Let go: it runs back onto the clamp and never crosses it, so the overshoot stays on its own side
            float highest = float.NegativeInfinity;
            Idle(cannon, 120, () => highest = MathF.Max(highest, cannon.ElevationOvershoot));

            Assert.True(highest <= 1e-6f, $"the lower rubber rebounded to {highest}");
            Assert.Equal(0f, cannon.ElevationOvershoot);
            Assert.Equal(Cannon.MinElevation, cannon.Elevation, 4);
        }

        [Fact]
        public void AShotAtTheFloorLeavesAtTheClampedElevationWhateverTheRubberShows()
        {
            //The owner asked for the lower alarm to go (#872), not for a gun that fires under its floor: the stretch is drawn,
            //the shot is the clamp's. The reference is the same gun put exactly on the clamp.
            Cannon reference = NewCannon();
            reference.AimTo(Cannon.MinElevation, 0f);

            Cannon cannon = NewCannon();
            Push(cannon, -1f, 200);

            Assert.True(cannon.ElevationOvershoot < -0.05f, "the rubber is not stretched, so the test checks nothing");
            Assert.False(cannon.ElevationRefusesShot);

            //The drawn pose is under the floor, the shot is not
            Assert.True(cannon.AimDirection.Y < reference.AimDirection.Y - 0.05f);
            Assert.Equal(reference.AimDirection.X, cannon.FireDirection.X, 4);
            Assert.Equal(reference.AimDirection.Y, cannon.FireDirection.Y, 4);
            Assert.Equal(reference.AimDirection.Z, cannon.FireDirection.Z, 4);
            Assert.Equal(reference.MuzzlePosition(2f).Y, cannon.FireMuzzle(2f).Y, 4);

            //And while it eases back onto the clamp, every frame of it
            Idle(cannon, 120, () => Assert.Equal(reference.AimDirection.Y, cannon.FireDirection.Y, 4));
        }

        [Fact]
        public void AnythingAboveTheFloorFiresAlongTheDrawnPose()
        {
            Cannon cannon = NewCannon();
            Assert.Equal(cannon.AimDirection, cannon.FireDirection);

            cannon.ElevationLimit = 0.5f;
            Push(cannon, 1f, 200);
            Assert.True(cannon.ElevationOvershoot > 0f);
            Assert.Equal(cannon.AimDirection, cannon.FireDirection);

            //The upper rubber's rebound under its clamp is still the pose that fires, not the floor's rule
            Idle(cannon, 40, () => Assert.Equal(cannon.AimDirection, cannon.FireDirection));
        }

        [Fact]
        public void TheUpperLimitKeepsItsStrainRefusalAndRebound()
        {
            Cannon cannon = NewCannon();
            cannon.ElevationLimit = 0.5f;

            Push(cannon, 1f, 200);

            Assert.Equal(1f, cannon.ElevationStrain);
            Assert.True(cannon.ElevationRefusesShot);
            Assert.True(cannon.ElevationOvershoot > 0f);

            //Let go: the strain runs out and the spring swings the barrel back past the clamp, as it always did
            float lowest = float.PositiveInfinity;
            Idle(cannon, 120, () => lowest = MathF.Min(lowest, cannon.ElevationOvershoot));

            Assert.True(lowest < -1e-4f, $"the upper rubber no longer swings back past the clamp ({lowest})");
            Assert.Equal(0f, cannon.ElevationStrain);
        }
    }
}
