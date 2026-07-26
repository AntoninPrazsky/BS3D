using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;

namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// What <see cref="ContactEvents"/> dispatches to for a collidable registered with it. Every method has a
    /// default empty body, so a handler implements only the events it cares about.
    /// <para>
    /// <b>These fire on Bepu worker threads, from inside <c>Simulation.Timestep</c>.</b> An implementation may
    /// therefore only record what happened — queue it — and must not touch the simulation, the constraint set,
    /// the ball map or the listener registrations; all of that belongs on the main thread after the step.
    /// </para>
    /// <para>
    /// The vectors are <see cref="Microsoft.Xna.Framework.Vector3"/> rather than the
    /// <see cref="System.Numerics.Vector3"/> Bepu hands out: MonoGame defines an implicit conversion between
    /// the two, so the call site converts for free, and the game code that consumes a contact position is
    /// XNA-side anyway. Spelled out here because both types are in scope in the files that implement this.
    /// </para>
    /// </summary>
    public interface IContactEventHandler
    {
        public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, Microsoft.Xna.Framework.Vector3 contactOffset, Microsoft.Xna.Framework.Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        void OnContactRemoved<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int removedFeatureId, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        void OnStartedTouching<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        void OnTouching<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        void OnStoppedTouching<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        void OnPairCreated<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        void OnPairEnded(CollidableReference eventSource, CollidablePair pair) { }
    }
}
