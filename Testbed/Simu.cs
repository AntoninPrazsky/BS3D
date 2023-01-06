using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Testbed
{
	//Třída zkopírovaná z Bepu demo projektu
	public static class Simu
	{
		//The simulation has a variety of extension points that must be defined.
		//The demos tend to reuse a few types like the DemoNarrowPhaseCallbacks, but this demo will provide its own (super simple) versions.
		//If you're wondering why the callbacks are interface implementing structs rather than classes or events, it's because
		//the compiler can specialize the implementation using the compile time type information. That avoids dispatch overhead associated
		//with delegates or virtual dispatch and allows inlining, which is valuable for extremely high frequency logic like contact callbacks.
		public unsafe struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
		{
			/// <summary>
			/// Performs any required initialization logic after the Simulation instance has been constructed.
			/// </summary>
			/// <param name="simulation">Simulation that owns these callbacks.</param>
			public void Initialize(Simulation simulation)
			{
				//Often, the callbacks type is created before the simulation instance is fully constructed, so the simulation will call this function when it's ready.
				//Any logic which depends on the simulation existing can be put here.
			}

			/// <summary>
			/// Chooses whether to allow contact generation to proceed for two overlapping collidables.
			/// </summary>
			/// <param name="workerIndex">Index of the worker that identified the overlap.</param>
			/// <param name="a">Reference to the first collidable in the pair.</param>
			/// <param name="b">Reference to the second collidable in the pair.</param>
			/// <returns>True if collision detection should proceed, false otherwise.</returns>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b)
			{
				//Before creating a narrow phase pair, the broad phase asks this callback whether to bother with a given pair of objects.
				//This can be used to implement arbitrary forms of collision filtering. See the RagdollDemo or NewtDemo for examples.
				return true;
			}

			/// <summary>
			/// Chooses whether to allow contact generation to proceed for the children of two overlapping collidables in a compound-including pair.
			/// </summary>
			/// <param name="pair">Parent pair of the two child collidables.</param>
			/// <param name="childIndexA">Index of the child of collidable A in the pair. If collidable A is not compound, then this is always 0.</param>
			/// <param name="childIndexB">Index of the child of collidable B in the pair. If collidable B is not compound, then this is always 0.</param>
			/// <returns>True if collision detection should proceed, false otherwise.</returns>
			/// <remarks>This is called for each sub-overlap in a collidable pair involving compound collidables. If neither collidable in a pair is compound, this will not be called.
			/// For compound-including pairs, if the earlier call to AllowContactGeneration returns false for owning pair, this will not be called. Note that it is possible
			/// for this function to be called twice for the same subpair if the pair has continuous collision detection enabled; 
			/// the CCD sweep test that runs before the contact generation test also asks before performing child pair tests.</remarks>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
			{
				//This is similar to the top level broad phase callback above. It's called by the narrow phase before generating
				//subpairs between children in parent shapes. 
				//This only gets called in pairs that involve at least one shape type that can contain multiple children, like a Compound.
				return true;
			}

			/// <summary>
			/// Provides a notification that a manifold has been created for a pair. Offers an opportunity to change the manifold's details. 
			/// </summary>
			/// <param name="workerIndex">Index of the worker thread that created this manifold.</param>
			/// <param name="pair">Pair of collidables that the manifold was detected between.</param>
			/// <param name="manifold">Set of contacts detected between the collidables.</param>
			/// <param name="pairMaterial">Material properties of the manifold.</param>
			/// <returns>True if a constraint should be created for the manifold, false otherwise.</returns>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : unmanaged, IContactManifold<TManifold>
            {
				//The IContactManifold parameter includes functions for accessing contact data regardless of what the underlying type of the manifold is.
				//If you want to have direct access to the underlying type, you can use the manifold.Convex property and a cast like Unsafe.As<TManifold, ConvexContactManifold or NonconvexContactManifold>(ref manifold).

				//The engine does not define any per-body material properties. Instead, all material lookup and blending operations are handled by the callbacks.
				//For the purposes of this demo, we'll use the same settings for all pairs.
				//(Note that there's no bounciness property! See here for more details: https://github.com/bepu/bepuphysics2/issues/3)
				pairMaterial.FrictionCoefficient = 1f;
				pairMaterial.MaximumRecoveryVelocity = 2f;
				pairMaterial.SpringSettings = new SpringSettings(30, 1);
				//For the purposes of the demo, contact constraints are always generated.
				return true;
			}

			/// <summary>
			/// Provides a notification that a manifold has been created between the children of two collidables in a compound-including pair.
			/// Offers an opportunity to change the manifold's details. 
			/// </summary>
			/// <param name="workerIndex">Index of the worker thread that created this manifold.</param>
			/// <param name="pair">Pair of collidables that the manifold was detected between.</param>
			/// <param name="childIndexA">Index of the child of collidable A in the pair. If collidable A is not compound, then this is always 0.</param>
			/// <param name="childIndexB">Index of the child of collidable B in the pair. If collidable B is not compound, then this is always 0.</param>
			/// <param name="manifold">Set of contacts detected between the collidables.</param>
			/// <returns>True if this manifold should be considered for constraint generation, false otherwise.</returns>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
			{
				return true;
			}

			/// <summary>
			/// Releases any resources held by the callbacks. Called by the owning narrow phase when it is being disposed.
			/// </summary>
			public void Dispose()
			{
			}

            /// <summary>
            /// Chooses whether to allow contact generation to proceed for two overlapping collidables.
            /// </summary>
            /// <param name="workerIndex">Index of the worker that identified the overlap.</param>
            /// <param name="a">Reference to the first collidable in the pair.</param>
            /// <param name="b">Reference to the second collidable in the pair.</param>
            /// <param name="speculativeMargin">Reference to the speculative margin used by the pair.
            /// The value was already initialized by the narrowphase by examining the speculative margins of the involved collidables, but it can be modified.</param>
            /// <returns>True if collision detection should proceed, false otherwise.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
            {
                //Before creating a narrow phase pair, the broad phase asks this callback whether to bother with a given pair of objects.
                //This can be used to implement arbitrary forms of collision filtering. See the RagdollDemo or NewtDemo for examples.
                //Here, we'll make sure at least one of the two bodies is dynamic.
                //The engine won't generate static-static pairs, but it will generate kinematic-kinematic pairs.
                //That's useful if you're trying to make some sort of sensor/trigger object, but since kinematic-kinematic pairs
                //can't generate constraints (both bodies have infinite inertia), simple simulations can just ignore such pairs.

                //This function also exposes the speculative margin. It can be validly written to, but that is a very rare use case.
                //Most of the time, you can ignore this function's speculativeMargin parameter entirely.
                return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
            }
        }

		//Note that the engine does not require any particular form of gravity- it, like all the contact callbacks, is managed by a callback.
		public struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
		{
			public Vector3 Gravity;
			private Vector3 gravityDt;

			/// <summary>
			/// Gets how the pose integrator should handle angular velocity integration.
			/// </summary>
			public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving; //Don't care about fidelity in this demo!

            public bool AllowSubstepsForUnconstrainedBodies => false;

            public bool IntegrateVelocityForKinematics => false;

            public PoseIntegratorCallbacks(Vector3 gravity) : this()
			{
				Gravity = gravity;
			}

			/// <summary>
			/// Called prior to integrating the simulation's active bodies. When used with a substepping timestepper, this could be called multiple times per frame with different time step values.
			/// </summary>
			/// <param name="dt">Current time step duration.</param>
			public void PrepareForIntegration(float dt)
			{
				//No reason to recalculate gravity * dt for every body; just cache it ahead of time.
				gravityDt = Gravity * dt;
			}

			/// <summary>
			/// Callback called for each active body within the simulation during body integration.
			/// </summary>
			/// <param name="bodyIndex">Index of the body being visited.</param>
			/// <param name="pose">Body's current pose.</param>
			/// <param name="localInertia">Body's current local inertia.</param>
			/// <param name="workerIndex">Index of the worker thread processing this body.</param>
			/// <param name="velocity">Reference to the body's current velocity to integrate.</param>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void IntegrateVelocity(int bodyIndex, in RigidPose pose, in BodyInertia localInertia, int workerIndex, ref BodyVelocity velocity)
			{
				//Note that we avoid accelerating kinematics. Kinematics are any body with an inverse mass of zero (so a mass of ~infinity). No force can move them.
				if (localInertia.InverseMass > 0)
				{
					velocity.Linear = velocity.Linear + gravityDt;
				}
			}

            public void Initialize(Simulation simulation)
            {
            }

            public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
            {
                throw new NotImplementedException();
            }
        }

		public struct DemoPoseIntegratorCallbacks : IPoseIntegratorCallbacks
		{
			public Vector3 Gravity;
			public float LinearDamping;
			public float AngularDamping;
			Vector3 gravityDt;
			float linearDampingDt;
			float angularDampingDt;

			public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;

            public bool AllowSubstepsForUnconstrainedBodies => false;

            public bool IntegrateVelocityForKinematics => false;

            public DemoPoseIntegratorCallbacks(Vector3 gravity, float linearDamping = .03f, float angularDamping = .03f) : this()
			{
				Gravity = gravity;
				LinearDamping = linearDamping;
				AngularDamping = angularDamping;
			}

			public void PrepareForIntegration(float dt)
			{
				//No reason to recalculate gravity * dt for every body; just cache it ahead of time.
				gravityDt = Gravity * dt;
				//Since this doesn't use per-body damping, we can precalculate everything.
				linearDampingDt = MathF.Pow(MathHelper.Clamp(1 - LinearDamping, 0, 1), dt);
				angularDampingDt = MathF.Pow(MathHelper.Clamp(1 - AngularDamping, 0, 1), dt);
			}
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void IntegrateVelocity(int bodyIndex, in RigidPose pose, in BodyInertia localInertia, int workerIndex, ref BodyVelocity velocity)
			{
				//Note that we avoid accelerating kinematics. Kinematics are any body with an inverse mass of zero (so a mass of ~infinity). No force can move them.
				if (localInertia.InverseMass > 0)
				{
					velocity.Linear = (velocity.Linear + gravityDt) * linearDampingDt;
					velocity.Angular = velocity.Angular * angularDampingDt;
				}
				//Implementation sidenote: Why aren't kinematics all bundled together separately from dynamics to avoid this per-body condition?
				//Because kinematics can have a velocity- that is what distinguishes them from a static object. The solver must read velocities of all bodies involved in a constraint.
				//Under ideal conditions, those bodies will be near in memory to increase the chances of a cache hit. If kinematics are separately bundled, the the number of cache
				//misses necessarily increases. Slowing down the solver in order to speed up the pose integrator is a really, really bad trade, especially when the benefit is a few ALU ops.

				//Note that you CAN technically modify the pose in IntegrateVelocity by directly accessing it through the Simulation.Bodies.ActiveSet.Poses, it just requires a little care and isn't directly exposed.
				//If the PositionFirstTimestepper is being used, then the pose integrator has already integrated the pose.
				//If the PositionLastTimestepper or SubsteppingTimestepper are in use, the pose has not yet been integrated.
				//If your pose modification depends on the order of integration, you'll want to take this into account.

				//This is also a handy spot to implement things like position dependent gravity or per-body damping.
			}

            public void Initialize(Simulation simulation)
            {
            }

            //Note that velocity integration uses "wide" types. These are array-of-struct-of-arrays types that use SIMD accelerated types underneath.
            //Rather than handling a single body at a time, the callback handles up to Vector<float>.Count bodies simultaneously.
            Vector3Wide gravityWideDt;

            public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
            {
                //This also is a handy spot to implement things like position dependent gravity or per-body damping.
                //We don't have to check for kinematics; IntegrateVelocityForKinematics returns false in this type, so we'll never see them in this callback.
                //Note that these are SIMD operations and "Wide" types. There are Vector<float>.Count lanes of execution being evaluated simultaneously.
                //The types are laid out in array-of-structures-of-arrays (AOSOA) format. That's because this function is frequently called from vectorized contexts within the solver.
                //Transforming to "array of structures" (AOS) format for the callback and then back to AOSOA would involve a lot of overhead, so instead the callback works on the AOSOA representation directly.
                velocity.Linear += gravityWideDt;
            }
        }

	}
}