using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using Prazsky.BS3D.Physics;
using Xunit;
using NVector3 = System.Numerics.Vector3;

namespace BS3D.Tests
{
    /// <summary>
    /// A shot whose landing was refused is marked bounced (#696): it is turned by every contact again and no longer swept,
    /// and the mark must not outlive the shot, because Bepu recycles a body handle and the next shot must not inherit it.
    /// </summary>
    public class BouncedShotTests
    {
        private sealed class Silent : IContactEventHandler
        {
        }

        [Fact]
        public void TheMarkIsReadableAndGoesWhenTheShotStopsListening()
        {
            using PhysicsWorld world = new();

            BodyReference shot = world.AddShotBall(NVector3.Zero, NVector3.Zero, new Silent());
            CollidableReference reference = shot.CollidableReference;

            Assert.False(world.Events.IsBounced(reference));

            world.Events.MarkBounced(shot.Handle);
            Assert.True(world.Events.IsBounced(reference));

            world.Events.Unregister(reference);
            Assert.False(world.Events.IsBounced(reference));
        }

        [Fact]
        public void ARetiredShotLeavesNoMarkForTheBodyThatGetsItsHandle()
        {
            using PhysicsWorld world = new();

            BodyReference first = world.AddShotBall(NVector3.Zero, NVector3.Zero, new Silent());
            BodyHandle handle = first.Handle;
            world.Events.MarkBounced(handle);
            world.RetireBall(first);

            //Bepu hands the freed handle to the next body added
            BodyReference second = world.AddShotBall(new NVector3(5f, 0f, 0f), NVector3.Zero, new Silent());
            Assert.Equal(handle, second.Handle);
            Assert.False(world.Events.IsBounced(second.CollidableReference));
        }

        /// <summary>
        /// The leak the review of #696 found: a shot that touched the stone has stopped listening but stays in the handler's
        /// list for the rest of the step, and a refusal of a structure contact queued behind the stone's marked it bounced. The
        /// mark must go with the body all the same (<c>RetireBall</c> clears it), or the next shot to be handed the handle is
        /// born bounced and never swept.
        /// </summary>
        [Fact]
        public void AMarkOnAShotThatHadAlreadyStoppedListeningIsClearedWithItsBody()
        {
            using PhysicsWorld world = new();

            BodyReference first = world.AddShotBall(NVector3.Zero, NVector3.Zero, new Silent());
            BodyHandle handle = first.Handle;

            world.Events.Unregister(first.CollidableReference);
            world.Events.MarkBounced(handle);
            world.RetireBall(first);

            BodyReference second = world.AddShotBall(new NVector3(5f, 0f, 0f), NVector3.Zero, new Silent());
            Assert.Equal(handle, second.Handle);
            Assert.False(world.Events.IsBounced(second.CollidableReference));
        }
    }
}
