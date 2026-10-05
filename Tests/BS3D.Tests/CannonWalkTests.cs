using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameObjects;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The carriage's walk answers the size of what is asked (#802): a key asks 1 and gets the ramp it always had, and the
    /// pad's left stick asks as much as it is pushed, so a partial push settles at a partial speed rather than ramping on
    /// to full.
    /// </summary>
    public class CannonWalkTests
    {
        private static readonly GameTime Frame = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(16));

        //How far the gun moves in one more frame of the same ask, after a second and a half of it — past the one-second
        //ramp, so both have settled
        private static float SettledStep(float ask)
        {
            Cannon cannon = new(new Vector3(0f, 5f, 0f));

            for (int i = 0; i < 94; i++)
            {
                cannon.Orbit(ask);
                cannon.Update(Frame);
            }

            Vector3 before = cannon.Position;
            cannon.Orbit(ask);
            cannon.Update(Frame);

            return Vector3.Distance(before, cannon.Position);
        }

        [Fact]
        public void APartialPushSettlesAtAPartialSpeed()
        {
            float full = SettledStep(1f);
            float part = SettledStep(0.3f);

            Assert.True(full > 0f);
            Assert.InRange(part / full, 0.28f, 0.32f);
        }

        [Fact]
        public void APartialPushIsUnderWayAtOnce()
        {
            //Reaching a small speed takes the ramp a small time: a third of full speed in about a third of a second,
            //where the old ramp took the whole second to full whatever was asked
            Cannon cannon = new(new Vector3(0f, 5f, 0f));
            Vector3 start = cannon.Position;

            for (int i = 0; i < 25; i++)
            {
                cannon.Orbit(0.3f);
                cannon.Update(Frame);
            }

            Vector3 before = cannon.Position;
            cannon.Orbit(0.3f);
            cannon.Update(Frame);
            float stepNow = Vector3.Distance(before, cannon.Position);

            Assert.True(Vector3.Distance(start, before) > 0f);
            Assert.InRange(stepNow / SettledStep(0.3f), 0.97f, 1.03f);
        }
    }
}
