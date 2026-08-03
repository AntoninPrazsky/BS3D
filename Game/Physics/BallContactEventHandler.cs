using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace BS3D.Physics
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
    /// the middle of using. So <see cref="OnContactAdded"/> only <i>records</i> the contact, and
    /// <see cref="ProcessQueuedContacts"/> does all the work on the main thread once the step has finished.
    /// </para>
    /// </summary>
    public sealed class BallContactEventHandler : IContactEventHandler
    {
        private readonly Simulation _simulation;
        private readonly ContactEvents _contactEvents;
        private readonly KinematicBody _ceiling;
        private readonly List<PhysicsBall> _shotBalls;
        private readonly List<PhysicsBall> _fallingBalls;

        /// <summary>
        /// How far the drawn (and simulated) world sits above the lattice frame the <see cref="BallsMap"/>
        /// reckons in. The bodies live in world coordinates, because everything else the simulation touches —
        /// the floor, the ceiling, the muzzle, the kill plane — does; the map's own positions do not, so a
        /// contact is converted down into the lattice frame before the map is asked about it, and the cell it
        /// answers with is converted back up before anything is drawn at it.
        /// </summary>
        private readonly Vector3 _worldOffset;

        private readonly BallsMap _map;
        private readonly PhysicsBall[,,] _physicsBalls;

        public BallContactEventHandler(Simulation simulation, ContactEvents contactEvents, KinematicBody ceiling,
            BallsMap map, PhysicsBall[,,] physicsBalls, List<PhysicsBall> shotBalls, List<PhysicsBall> fallingBalls,
            Vector3 worldOffset)
        {
            _simulation = simulation;
            _contactEvents = contactEvents;
            _ceiling = ceiling;
            _map = map;
            _physicsBalls = physicsBalls;
            _shotBalls = shotBalls;
            _fallingBalls = fallingBalls;
            _worldOffset = worldOffset;
        }

        /// <summary>
        /// Raised once for a shot that landed in the lattice, with what it cut loose (zero of both when it
        /// stuck without completing a group). The handler reports and does not score: what a landing is worth
        /// is a rule, and rules live in <c>ScoreKeeper</c>.
        /// </summary>
        /// <remarks>
        /// Everything one attach has to answer for travels in <see cref="BallLanding"/> — see there for what
        /// each field is and why it is that frame rather than another.
        /// </remarks>
        public event Action<BallLanding> BallLanded;

        /// <summary>
        /// Raised once for a shot that is over without having landed — it hit the island, the drain or the
        /// glass. Together with <see cref="BallLanded"/> and the kill-plane cull in the game, every shot
        /// resolves exactly once, which is what a streak rule needs to be able to rely on.
        /// </summary>
        public event Action ShotSpent;

        private readonly ConcurrentQueue<QueuedContact> _queuedContacts = new();

        private readonly struct QueuedContact
        {
            public readonly CollidableReference EventSource;
            public readonly CollidablePair Pair;
            public readonly Vector3 ContactOffset;

            public QueuedContact(CollidableReference eventSource, CollidablePair pair, Vector3 contactOffset)
            {
                EventSource = eventSource;
                Pair = pair;
                ContactOffset = contactOffset;
            }
        }

        /// <summary>
        /// Runs on a Bepu worker thread, inside the timestep. Records and returns — see the class remarks.
        /// </summary>
        public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
            Vector3 contactOffset, Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex)
            where TManifold : unmanaged, IContactManifold<TManifold>
        {
            _queuedContacts.Enqueue(new QueuedContact(eventSource, pair, contactOffset));
        }

        /// <summary>
        /// Handles the contacts recorded during the last timestep. Main thread only, after
        /// <see cref="ContactEvents.Flush"/> and while the simulation is not stepping.
        /// </summary>
        /// <returns>How many balls attached to the structure.</returns>
        public int ProcessQueuedContacts()
        {
            int attached = 0;

            while (_queuedContacts.TryDequeue(out QueuedContact contact))
                if (ProcessContact(contact)) attached++;

            return attached;
        }

        private bool ProcessContact(in QueuedContact contact)
        {
            CollidablePair pair = contact.Pair;

            //Read BEFORE the unregister below, because that is what makes reporting a spent shot once-only:
            //the same timestep can queue several contacts for one ball, and only the first of them finds it
            //still listening. A ball stops being a listener exactly when its shot is over — on attaching, on
            //touching something it cannot attach to, or on being culled — so this flag is the resolution
            //guard for free, with no per-ball state to keep anywhere.
            bool wasListening = _contactEvents.IsListener(contact.EventSource);

            //A ball that has touched anything static or kinematic has had its shot: it hit the island, the
            //drain or the glass, and it is no longer a candidate for attaching. Stop listening to it — the
            //same timestep can queue several contacts for one ball, so the listener may already be gone.
            if (pair.A.Mobility != CollidableMobility.Dynamic || pair.B.Mobility != CollidableMobility.Dynamic)
            {
                if (pair.A.Mobility == CollidableMobility.Dynamic && _contactEvents.IsListener(pair.A)) _contactEvents.Unregister(pair.A);
                if (pair.B.Mobility == CollidableMobility.Dynamic && _contactEvents.IsListener(pair.B)) _contactEvents.Unregister(pair.B);
            }

            //The event source is the registered listener, which is only ever a ball still in flight
            BodyHandle shotHandle = contact.EventSource.BodyHandle;
            PhysicsBall physicsBall = FindShotBall(shotHandle);

            //Already attached by an earlier contact of the same step, or already culled
            if (physicsBall == null) return false;

            CollidableReference other = pair.A.Packed == contact.EventSource.Packed ? pair.B : pair.A;

            //The island's stone and the drain cone are statics, and there is no cell to put a ball into on
            //either — so this is where a shot that missed the cluster ends. It also has to come before the
            //world contact is rebuilt below: a static's CollidableReference carries no meaningful BodyHandle,
            //and indexing Bodies with one reads an unrelated slot of an unchecked buffer.
            if (pair.A.Mobility == CollidableMobility.Static || pair.B.Mobility == CollidableMobility.Static)
            {
                //Resolved here rather than when the ball is finally culled, and that is the point: the player
                //knows they missed the instant the ball strikes the stone, so the streak has to break then and
                //not forty units of falling later. It also closes the case of a shot that comes to rest ON the
                //island and is therefore never culled at all — it touched the stone, so it is already spent.
                if (wasListening) ShotSpent?.Invoke();

                return false;
            }

            //A manifold offset is relative to the position of the pair's FIRST collidable, not to either
            //body's own — so the world contact is pair.A's position plus the offset, whichever of the two
            //the shot ball happens to be
            Vector3 worldContact = _simulation.Bodies[pair.A.BodyHandle].Pose.Position.ToXna() + contact.ContactOffset;

            //Everything the map is asked about is in its own lattice frame
            Vector3 mapContact = worldContact - _worldOffset;

            XZLevel cell;
            Vector3 placed;

            if (other.Mobility == CollidableMobility.Kinematic && other.BodyHandle == _ceiling.BodyHandle)
            {
                //Straight into the glass, past the whole cluster: it lands on the field's top level
                placed = _map.PutBallAtClosestEmptyCeilingPosition(mapContact, out cell, physicsBall.Type);
            }
            else if (other.Mobility == CollidableMobility.Dynamic && TryFindStructureBall(other.BodyHandle, out PhysicsBall hitBall))
            {
                placed = _map.PutBallAtClosestEmptyPositionNextTo(mapContact, hitBall.ArrayPosition, out cell, physicsBall.Type);

                //Nothing free touching the ball it hit. Not an exotic case: the ball a shot reaches first is
                //on the cluster's outer face, and where that face is the field's own wall there is no cell
                //beyond it — so the pocket around an edge ball fills after a handful of shots and every ball
                //after that would be silently eaten. Widen the search by one ring, nearest the contact first;
                //local by construction, so the ball never lands somewhere it could not have rolled to.
                if (placed.X == float.MinValue && TryFindCellInSecondRing(mapContact, hitBall.ArrayPosition, out XZLevel ringCell))
                {
                    placed = _map.PutBallAt((byte)ringCell.X, (byte)ringCell.Z, (byte)ringCell.Level, physicsBall.Type).Position;
                    cell = ringCell;
                }
            }
            else return false; //another loose shot ball, or something with no cell to offer

            //Refusal is reported by the RETURNED position, and testing the cell instead is not equivalent:
            //PutBallAtClosestEmptyPositionNextTo leaves the cell at -1 when it refuses, but
            //PutBallAtClosestEmptyCeilingPosition fills the cell in from the rounded contact *before* it checks
            //whether that cell is inside the field and unoccupied — so a refused ceiling placement comes back
            //with a perfectly plausible-looking cell. Testing only the cell let an out-of-bounds ceiling hit
            //through to index the structure array (a crash), and an occupied one overwrite a ball that then
            //stayed in the simulation for ever, untracked, undrawn and unreleasable.
            if (placed.X == float.MinValue) return false;

            physicsBall.ArrayPosition = cell;

            _shotBalls.Remove(physicsBall);                                  //not in flight any more
            _physicsBalls[cell.X, cell.Z, cell.Level] = physicsBall;         //part of the structure now

            //Both velocities, not just the linear one: residual spin would drag the constraint anchors that
            //are about to be created around with it
            physicsBall.BallReference.Velocity.Linear = default;
            physicsBall.BallReference.Velocity.Angular = default;

            //The ball is snapped to the nearest free cell rather than to where it hit, so the constraints
            //below drag its body across up to several diameters within a frame or two. Drawing it gliding in
            //from where it actually hit hides that click without touching the simulation. Armed before the
            //constraints exist, and in world frame, which is where the body is.
            physicsBall.StartRenderGlide((placed + _worldOffset).ToNumerics());

            //Anchors come from the ideal lattice and are rotated into each body's current local frame, so
            //they are right even after the simulation has been running for a while
            BallsConstraintsBuilder.AttachBallToStructure(physicsBall, _physicsBalls, _map, _simulation, _ceiling.BodyReference);

            if (_contactEvents.IsListener(contact.EventSource)) _contactEvents.Unregister(contact.EventSource);

            //And the game rule: three or more of a colour touching each other let go, and so does anything
            //that was only held up by them
            BallsReleased released = BallsConstraintsBuilder.ReleaseSameTypeCluster(physicsBall, _physicsBalls, _map, _simulation, _fallingBalls);

            //Reported whether or not anything fell: a shot that stuck without completing a group is still a
            //resolved shot, and the streak rule has to hear about it. Taken before the release above could
            //have moved anything, and in world frame — the lattice cell the ball landed in.
            BallLanded?.Invoke(new BallLanding(released, placed + _worldOffset, physicsBall.Type, cell));

            return true;
        }

        /// <summary>
        /// The ball in flight with this body, or null. An indexed walk rather than LINQ: this is a gameplay
        /// path, and the list holds at most a handful of balls.
        /// </summary>
        private PhysicsBall FindShotBall(BodyHandle handle)
        {
            for (int i = 0; i < _shotBalls.Count; i++)
                if (_shotBalls[i].BallReference.Handle.Value == handle.Value) return _shotBalls[i];

            return null;
        }

        private bool TryFindStructureBall(BodyHandle handle, out PhysicsBall ball)
        {
            ball = null;
            if (_physicsBalls == null) return false;

            XZLevel size = XZLevel.FromArray(_physicsBalls);

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        PhysicsBall candidate = _physicsBalls[x, z, level];
                        if (candidate == null || candidate.BallReference.Handle.Value != handle.Value) continue;

                        ball = candidate;
                        return true;
                    }

            return false;
        }

        /// <summary>
        /// The free cell nearest <paramref name="mapContact"/> among those touching a ball that itself touches
        /// <paramref name="hitCell"/> — one ring further out than
        /// <see cref="BallsMap.PutBallAtClosestEmptyPositionNextTo"/> looks.
        /// </summary>
        private bool TryFindCellInSecondRing(Vector3 mapContact, XZLevel hitCell, out XZLevel best)
        {
            best = new XZLevel(-1, -1, -1);

            StaticBall[,,] balls = _map.GetStaticBallsArray();
            XZLevel size = _map.GetStaticBallsArraySize();

            float closest = float.MaxValue;

            foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(hitCell, size))
            {
                if (balls[neighbour.X, neighbour.Z, neighbour.Level] == null) continue; //free cells were the first ring's business

                foreach (XZLevel candidate in BallsMap.GetNeighboringCells(neighbour, size))
                {
                    if (balls[candidate.X, candidate.Z, candidate.Level] != null) continue;

                    float distance = Vector3.DistanceSquared(_map.GetRealCenteredPosition(candidate), mapContact);
                    if (distance >= closest) continue;

                    closest = distance;
                    best = candidate;
                }
            }

            return best.X >= 0;
        }
    }
}
