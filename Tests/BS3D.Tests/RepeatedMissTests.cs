using Microsoft.Xna.Framework;
using Prazsky.BS3D.Input;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #230's impossible shot: a run of escaped shots off one pose completes on its third, and anything else — another
    /// pose, a shot that did something else — starts it again.
    /// </summary>
    public class RepeatedMissTests
    {
        private static readonly Vector3 Muzzle = new(0f, 5f, 20f);
        private static readonly Vector3 Along = Vector3.Normalize(new Vector3(0.3f, 0.2f, -1f));

        [Fact]
        public void ThreeEscapesOffOnePoseCompleteTheRunAndItStartsAgain()
        {
            RepeatedMiss run = new();

            Assert.False(run.Escaped(Muzzle, Along));
            Assert.False(run.Escaped(Muzzle, Along));
            Assert.True(run.Escaped(Muzzle, Along));

            //Started again from nothing: the next two do not fire it
            Assert.Equal(0, run.Count);
            Assert.False(run.Escaped(Muzzle, Along));
            Assert.False(run.Escaped(Muzzle, Along));
        }

        [Fact]
        public void ABreakStartsTheRunAgain()
        {
            RepeatedMiss run = new();

            run.Escaped(Muzzle, Along);
            run.Escaped(Muzzle, Along);
            run.Break();

            Assert.False(run.Escaped(Muzzle, Along));
            Assert.False(run.Escaped(Muzzle, Along));
            Assert.True(run.Escaped(Muzzle, Along));
        }

        /// <summary>
        /// Inside the tolerances counts as one pose; past either, the shot is the first of a new run. Turned a degree a
        /// shot, the third is two degrees off the first — out — though each is only one off the one before.
        /// </summary>
        [Fact]
        public void ThePoseIsHeldAgainstTheFirstShotOfTheRun()
        {
            RepeatedMiss run = new();
            Vector3 Turned(float degrees) =>
                Vector3.Transform(Along, Matrix.CreateRotationY(MathHelper.ToRadians(degrees)));

            //A hand that barely moved
            run.Escaped(Muzzle, Along);
            run.Escaped(Muzzle + new Vector3(0.2f, 0f, 0f), Turned(0.8f));
            Assert.True(run.Escaped(Muzzle, Turned(-0.8f)));

            //A hand walking the barrel round a degree at a time
            run.Escaped(Muzzle, Along);
            run.Escaped(Muzzle, Turned(1f));
            Assert.False(run.Escaped(Muzzle, Turned(2f)));

            //The gun walked to another place, the line kept
            run.Reset();
            run.Escaped(Muzzle, Along);
            run.Escaped(Muzzle, Along);
            Assert.False(run.Escaped(Muzzle + new Vector3(0f, 0f, 1f), Along));
            Assert.Equal(1, run.Count);
        }
    }
}
