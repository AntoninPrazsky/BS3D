using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Testbed.Physics
{
    /// <summary>
    /// What happens when a shot ball touches something: it is snapped into the free lattice cell nearest the
    /// contact, wired into the hanging structure, and — if that completed a group of at least
    /// <see cref="BallsConstraintsBuilder.MINIMUM_CLUSTER_SIZE"/> of its own colour — the whole group is cut
    /// loose and falls, along with anything it was the last anchor for.
    /// <para>
    /// <b>The split between the two halves of this class is the whole point of it.</b> Bepu runs contact
    /// callbacks on its worker threads, from inside <c>Simulation.Timestep</c>; touching the simulation, the
    /// constraint set, the ball map or the listener registrations from there corrupts state the solver is in
    /// the middle of using (it used to surface as occasional <see cref="NullReferenceException"/>s). So
    /// <see cref="OnContactAdded"/> only <i>records</i> the contact, and <see cref="ProcessQueuedContacts"/>
    /// does all the work on the main thread once the step has finished — the order
    /// <see cref="PhysicsWorld.Step"/> owns and enforces.
    /// </para>
    /// <para>
    /// <b>It stood as a second public top-level type inside <c>Testbed.cs</c> until #73</b>, which is the whole
    /// of what moving it here changes: the flow, the queue and the placement calls are untouched. It is still
    /// ~90% of <c>BS3D.Physics.BallContactEventHandler</c> written a second time, and closing that is
    /// deliberately <b>#68</b>'s and not this file's — the two differ in more than layout (the Game's copy
    /// listens on <c>OnTouching</c> rather than <c>OnContactAdded</c>, so it never attaches off a
    /// <i>speculative</i> contact, and it asks <c>ShotPlacement</c> which cell a shot lands in so its aim
    /// preview cannot disagree with the outcome), and converging them is a gameplay change to verify by hand in
    /// both executables rather than a file move. What this move buys #68 is two comparable files instead of one
    /// of them being a nested class nobody reads.
    /// </para>
    /// </summary>
    public class BallContactEventHandler : IContactEventHandler
    {
        public Simulation Simulation;
        private readonly ContactEvents _contactEvents;
        public BallsMap Map;
        public PhysicsBall[,,] PhysicsBalls;
        public List<PhysicsBall> ShotBalls;
        public List<PhysicsBall> FallingBalls;

        /// <summary>
        /// The plate the cluster hangs from, which a shot may attach straight onto. <b>Settable, and it has to
        /// be</b> (#73): <c>FitCeilingToMap</c> replaces the whole <see cref="KinematicBody"/> wrapper on every
        /// map load so its world matrix matches the new pose, and a handler holding the wrapper it was
        /// constructed with would go on answering out of the previous map's object. It survived only because
        /// the body and its handle outlive the wrapper — the two things read here — so nothing pose-derived was
        /// ever asked of the stale one; a single reference to <c>Ceiling.World</c> added later would have made
        /// it a real bug with nothing to hint at it. <c>InstallMap</c> pushes this beside
        /// <see cref="Map"/> and <see cref="PhysicsBalls"/>.
        /// </summary>
        public KinematicBody Ceiling;

        public BallContactEventHandler(Simulation simulation, ContactEvents contactEvents, KinematicBody ceiling, PhysicsBall[,,] physicsBalls, List<PhysicsBall> shotBalls, List<PhysicsBall> fallingBalls)
        {
            Simulation = simulation;
            _contactEvents = contactEvents;
            Ceiling = ceiling;
            PhysicsBalls = physicsBalls;
            ShotBalls = shotBalls;
            FallingBalls = fallingBalls;
        }

        //Contact callbacks run inside Simulation.Timestep, potentially from multiple worker threads at once.
        //Mutating the simulation (constraints, velocities) or the ContactEvents listener set from there corrupts state
        //the solver and the event system are using (this used to cause occasional NullReferenceExceptions).
        //Contacts are therefore only recorded here and processed on the main thread by ProcessQueuedContacts after the timestep.
        private readonly ConcurrentQueue<QueuedContact> _queuedContacts = new();

        private readonly struct QueuedContact
        {
            public readonly CollidableReference EventSource;
            public readonly CollidablePair Pair;
            public readonly Vector3 ContactOffset;
            public readonly Vector3 ContactNormal;
            public readonly float Depth;
            public readonly int FeatureId;
            public readonly int ContactIndex;
            public readonly int WorkerIndex;

            public QueuedContact(CollidableReference eventSource, CollidablePair pair, Vector3 contactOffset, Vector3 contactNormal,
                float depth, int featureId, int contactIndex, int workerIndex)
            {
                EventSource = eventSource;
                Pair = pair;
                ContactOffset = contactOffset;
                ContactNormal = contactNormal;
                Depth = depth;
                FeatureId = featureId;
                ContactIndex = contactIndex;
                WorkerIndex = workerIndex;
            }
        }

        public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
            Vector3 contactOffset, Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold>
        {
            _queuedContacts.Enqueue(new QueuedContact(eventSource, pair, contactOffset, contactNormal, depth, featureId, contactIndex, workerIndex));
        }

        /// <summary>
        /// Processes contacts recorded during the last timestep. Must be called from the main thread while the simulation is not stepping,
        /// after <see cref="ContactEvents.Flush"/>.
        /// </summary>
        /// <returns>Number of balls attached to the ceiling.</returns>
        public int ProcessQueuedContacts()
        {
            int attachedBalls = 0;
            while (_queuedContacts.TryDequeue(out QueuedContact contact))
                if (ProcessContact(contact)) attachedBalls++;
            return attachedBalls;
        }

        private bool ProcessContact(in QueuedContact contact)
        {
            CollidablePair pair = contact.Pair;

#if DEBUG
            Console.WriteLine(" → Ball collided!");
            Console.WriteLine(nameof(contact.EventSource) + " : " + contact.EventSource.ToString());
            Console.WriteLine(nameof(pair.A) + " : " + pair.A.ToString());
            Console.WriteLine(nameof(pair.B) + " : " + pair.B.ToString());
            Console.WriteLine(nameof(contact.ContactOffset) + " : " + contact.ContactOffset.ToString());
            Console.WriteLine(nameof(contact.ContactNormal) + " : " + contact.ContactNormal.ToString());
            Console.WriteLine(nameof(contact.Depth) + " : " + contact.Depth.ToString());
            Console.WriteLine(nameof(contact.FeatureId) + " : " + contact.FeatureId.ToString());
            Console.WriteLine(nameof(contact.ContactIndex) + " : " + contact.ContactIndex.ToString());
            Console.WriteLine(nameof(contact.WorkerIndex) + " : " + contact.WorkerIndex.ToString());
            Console.WriteLine();
#endif

            //Once ball touches the ground or ceiling, unregister collision event
            //TODO: This might be possible to do by checking if the Static/Kinematic body is specific object (ground block, ceiling block by BodyReference)
            if (pair.A.Mobility == CollidableMobility.Static || pair.B.Mobility == CollidableMobility.Static ||
                pair.A.Mobility == CollidableMobility.Kinematic || pair.B.Mobility == CollidableMobility.Kinematic)
            {
                //A single timestep can queue several contacts for the same ball, so the listener may have been unregistered by a previous one
                if (pair.A.Mobility == CollidableMobility.Dynamic && _contactEvents.IsListener(pair.A)) _contactEvents.Unregister(pair.A);
                if (pair.B.Mobility == CollidableMobility.Dynamic && _contactEvents.IsListener(pair.B)) _contactEvents.Unregister(pair.B);
            }

            //Silent, and deliberately so since #73: the Testbed installs an empty map at startup precisely so it
            //is playable with nothing on the command line, so a null map is a state the program is not normally
            //in at all — and this line used to be an UNGUARDED console write on the contact path, which is one
            //write per queued contact per step for as long as the condition holds (BestPractices.md §2).
            if (Map == null) return false;

            //The event source is the registered listener, i.e. the shot ball
            BodyHandle shotBallHandle = contact.EventSource.BodyHandle;

            //An indexed walk rather than LINQ, the Game's FindShotBall reasoning: this runs per queued contact
            //on the shot path, and Where().FirstOrDefault() allocated a closure and two iterators per call for
            //a list that rarely holds more than a ball or two (#80)
            PhysicsBall physicsBall = null;
            for (int i = 0; i < ShotBalls.Count; i++)
                if (ShotBalls[i].BallReference.Handle == shotBallHandle) { physicsBall = ShotBalls[i]; break; }

            if (physicsBall == null)
            {
#if DEBUG
                Console.WriteLine("Ball already attached or no longer tracked as shot, skipping");
#endif
                return false;
            }

            CollidableReference other = pair.A.Packed == contact.EventSource.Packed ? pair.B : pair.A;

            #region Find a free cell for the ball

            Vector3 allowedPosition;
            XZLevel arrayPosition;

            if (other.Mobility == CollidableMobility.Kinematic && other.BodyHandle == Ceiling.BodyHandle)
            {
#if DEBUG
                Console.WriteLine(" → CEILING HIT");
#endif
                allowedPosition = Map.PutBallAtClosestEmptyCeilingPosition(contact.ContactOffset, out arrayPosition, physicsBall.Type);
            }
            else if (other.Mobility == CollidableMobility.Dynamic && TryFindMapBall(other.BodyHandle, out PhysicsBall hitBall))
            {
#if DEBUG
                Console.WriteLine(" → STRUCTURE BALL HIT");
#endif
                //Manifold offsets are relative to the position of the pair's first collidable
                var worldContact = Simulation.Bodies[pair.A.BodyHandle].Pose.Position + contact.ContactOffset.ToNumerics();
                allowedPosition = Map.PutBallAtClosestEmptyPositionNextTo(worldContact, hitBall.ArrayPosition, out arrayPosition, physicsBall.Type);
            }
            else return false; //Ground, a loose shot ball, …

            if (allowedPosition.X == float.MinValue)
            {
#if DEBUG
                Console.WriteLine("Outside of the map or every neighboring cell already occupied by another ball");
#endif
                return false;
            }

#if DEBUG
            Console.WriteLine("Ball placed at: " + allowedPosition);
#endif

            #endregion

            #region Attach the ball to the structure

            physicsBall.ArrayPosition = arrayPosition;

            ShotBalls.Remove(physicsBall); //Not shot anymore

            PhysicsBalls[arrayPosition.X, arrayPosition.Z, arrayPosition.Level] = physicsBall; //Part of the map now

            physicsBall.BallReference.Velocity.Linear = default; //Removing velocity from the shot
            physicsBall.BallReference.Velocity.Angular = default; //Also stop spinning, so the freshly created constraint anchors are not dragged around by residual rotation

            //The ball is snapped to the nearest free cell rather than to where it hit, so the constraints created
            //below drag it across up to several ball diameters within a frame or two. Drawing it gliding in from
            //where it actually hit hides that click without touching the simulation.
            physicsBall.StartRenderGlide(allowedPosition.ToNumerics());

            //Constraint anchors are computed from the static map grid (ideal positions) and rotated into each body's current local frame,
            //so they are correct even after the simulation has been running
            BallsConstraintsBuilder.AttachBallToStructure(physicsBall, PhysicsBalls, Map, Simulation, Ceiling.BodyReference);

            //Attached to the structure – no need to listen for its contacts anymore
            if (_contactEvents.IsListener(contact.EventSource)) _contactEvents.Unregister(contact.EventSource);

            #region Same-type cluster removal

            BallsReleased releasedBalls = BallsConstraintsBuilder.ReleaseSameTypeCluster(physicsBall, PhysicsBalls, Map, Simulation, FallingBalls);

#if DEBUG
            if (releasedBalls.Any) Console.WriteLine($"Released a cluster of type {physicsBall.Type}: {releasedBalls}");
#endif

            #endregion

            #endregion

            return true;
        }

        /// <summary>
        /// The structure ball with this body, or none. A full walk of the array per structure-ball contact, and
        /// the cost is real on a large field — 20×20×20 is 8 000 cells, all of them examined whenever the answer
        /// is <c>false</c> (a shot that struck another loose ball). <b>Left as it stands on purpose (#73):</b> the
        /// Game's <c>TryFindStructureBall</c> is this same walk, so a handle index built here alone would be a
        /// divergence between two copies that #68 exists to merge — and a lookup table over the structure is
        /// state to keep in step with every attach, release and map load, which is exactly the kind of thing that
        /// wants one owner rather than two. The measured shape to fix, once there is one copy of it, is the
        /// <i>miss</i>: a hit returns as soon as it is found.
        /// </summary>
        private bool TryFindMapBall(BodyHandle handle, out PhysicsBall ball)
        {
            ball = null;
            if (PhysicsBalls == null) return false;

            XZLevel size = XZLevel.FromArray(PhysicsBalls);

            for (byte level = 0; level < size.Level; level++)
                for (byte x = 0; x < size.X; x++)
                    for (byte z = 0; z < size.Z; z++)
                    {
                        PhysicsBall candidate = PhysicsBalls[x, z, level];
                        if (candidate != null && candidate.BallReference.Handle == handle)
                        {
                            ball = candidate;
                            return true;
                        }
                    }

            return false;
        }
    }
}
