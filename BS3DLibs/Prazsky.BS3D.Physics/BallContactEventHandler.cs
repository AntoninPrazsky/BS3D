using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.Core.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Prazsky.BS3D.Physics
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
    /// the middle of using. So <see cref="OnTouching"/> only <i>records</i> the contact, and
    /// <see cref="ProcessQueuedContacts"/> does all the work on the main thread once the step has finished.
    /// </para>
    /// <para>
    /// <b>One copy since #68</b>, and it was two for a long time — this flow, its two helpers and its call
    /// sequence stood in <c>Game/Physics/</c> and again inside <c>Testbed.cs</c>, where #73 at least got it into
    /// a file of its own. The stated cost of that was exact: "a fix or a rule change made in one — the
    /// second-ring search, say — silently misses the other." What made merging them a <i>gameplay</i> change
    /// rather than a file move is that the two were not the same rule, and this copy is the Game's, so the
    /// Testbed's contact path changed rather than moved. The three differences, each of which the Game had right:
    /// </para>
    /// <list type="bullet">
    /// <item><b>It listens on <see cref="OnTouching"/>, not <c>OnContactAdded</c></b>, so it never attaches off a
    /// speculative contact — see that method, which carries the measurement.</item>
    /// <item><b><see cref="ShotPlacement"/> decides the cell and this places it</b>, in two steps, where the
    /// Testbed asked <c>BallsMap</c> to decide and write at once. Deciding without writing is what lets the aim
    /// preview ask the identical question every frame (#70), and it is why the two <c>BallsMap</c> methods that
    /// did both — <c>PutBallAtClosestEmptyCeilingPosition</c> and <c>PutBallAtClosestEmptyPositionNextTo</c> —
    /// are gone with this: they had no other caller, and their own remarks recorded the bug the shape invites.</item>
    /// <item><b>A shot resolves exactly once</b>, reported through <see cref="BallLanded"/> and
    /// <see cref="ShotSpent"/>. The Testbed subscribes to neither and nothing there keeps score, so it takes the
    /// events as two null checks per landing.</item>
    /// </list>
    /// <para>
    /// What is <b>not</b> shared, deliberately: the pairing of a <see cref="BallsMap"/> with its
    /// <see cref="PhysicsBall"/> array. #68's "one owner for the map+physics pairing" would mean reshaping
    /// <see cref="BallsConstraintsBuilder"/>'s whole static API and every walk over the structure in both
    /// programs — the ripple, the loss check, the cluster profile, the aim preview, the teardown — and none of
    /// that is where the duplication was. The defect #68 names is fixed by there being one contact rule; the
    /// threading of two parameters is a wider change with nothing broken under it, so it is not made here.
    /// </para>
    /// <para>
    /// Both callers build one <b>per field</b> and let the previous one go — the Game per level, the Testbed per
    /// map load (it swaps maps inside a live simulation). That is why every field here is <c>readonly</c>: #73
    /// had to give the Testbed's copy a settable ceiling, because <c>FitCeilingToMap</c> replaces the
    /// <see cref="KinematicBody"/> wrapper on every load and the handler went on holding the old one. Rebuilding
    /// removes the whole class of staleness instead of pushing each field as it changes, and it is safe because a
    /// map load retires every ball in flight first, so no listener outlives the handler it was registered with.
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
        /// <para>
        /// It is the offset of the <i>frame</i> and not of any particular ball: the hanging cluster does not sit
        /// on its lattice, and how far off it is varies through it and changes as it sways. A contact is
        /// therefore anchored at the ball it was made against as well — see <see cref="ProcessContact"/>.
        /// </para>
        /// <para>
        /// <b>And this offset alone is not enough to bring a cell back up</b>, which is what makes it a snapshot
        /// worth being careful with: it is fixed for the session, while the glass ceiling <i>descends</i> over a
        /// level and drags the whole structure down with it. Nothing here is drawn or announced from
        /// <c>cell + this</c> any more — every such position goes through
        /// <see cref="ShotPlacement.CellWorldPosition"/> with the drift the solve measured, which is the term that
        /// carries the descent.
        /// </para>
        /// <para>
        /// <b>The Testbed passes <see cref="Vector3.Zero"/> here</b>, and that is not a special case to work
        /// around: its lattice frame <i>is</i> the world frame (the field's floor sits at y = 0 and there is no
        /// death line to hang it off), so every conversion below reduces to an identity for it while the code
        /// stays one copy. The Game hangs each field where its own fit decided, so its offset is real.
        /// </para>
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
        /// <para>
        /// <b>This is <see cref="IContactEventHandler.OnTouching"/> and not <c>OnContactAdded</c>, and the
        /// difference is the whole accuracy of the game.</b> <c>OnContactAdded</c> is edge-triggered on a
        /// feature id appearing and is raised for <i>speculative</i> contacts too, whose <c>depth</c> is simply
        /// negative — so attaching from there put the ball in a cell chosen around a contact that had not
        /// happened, against whichever ball the narrow phase paired first rather than the one the shot would
        /// have reached.
        /// </para>
        /// <para>
        /// Measured before this changed, in a played level at <c>SHOOT_SPEED</c> 200 with a 1/120 s step
        /// (1.667 units of travel per step): <b>23 of 23</b> attaches fired on a negative depth, mean −1.03 and
        /// worst −1.60 — bounded by that per-step travel, as the mechanism predicts. The ball was placed a mean
        /// 1.34 and a worst <b>3.79</b> units from the contact that chose the cell, in a lattice whose cells are
        /// 1.0 across, with a vertical scatter of −1.8…+2.9 levels. A control run at 60 u/s (0.5 per step) scaled
        /// every one of those figures by the speed almost exactly: worst depth −0.43, worst placement 1.14.
        /// </para>
        /// <para>
        /// <b>Those figures are the reason the shot's collidable is swept rather than merely speculative.</b>
        /// The shot used to be stamped from a bare shape index, i.e. <c>ContinuousDetection.Passive</c> — an
        /// unbounded speculative margin — and on a cluster whose face the shot meets at a glance that margin
        /// turns the ball away with the manifold never reaching <c>depth &gt;= 0</c>, so this method is never
        /// raised and the shot cannot land at all. It is <see cref="PhysicsWorld"/>'s constructor that fixes
        /// it, and the measurement that forced it is recorded there.
        /// </para>
        /// <para>
        /// <see cref="ContactEvents"/> raises this only once a manifold contact has <c>depth &gt;= 0</c>, so the
        /// gate is Bepu's own and there is no tolerance here to tune. It fires every step the pair keeps
        /// touching, which is deliberate: a refusal (see <see cref="ProcessContact"/>) is then retried on the
        /// next step as the ball slides, instead of being the ball's one and only chance.
        /// </para>
        /// </summary>
        public void OnTouching<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
            int workerIndex)
            where TManifold : unmanaged, IContactManifold<TManifold>
        {
            //The deepest contact is the one that describes the touch. A sphere pair has exactly one, so this
            //is a formality here — but it is the honest way to read a manifold, and the ceiling is a box.
            float deepest = float.MinValue;
            System.Numerics.Vector3 offset = default;

            for (int i = 0; i < contactManifold.Count; i++)
            {
                contactManifold.GetContact(i, out System.Numerics.Vector3 candidate, out _, out float depth, out _);

                if (depth <= deepest) continue;

                deepest = depth;
                offset = candidate;
            }

            _queuedContacts.Enqueue(new QueuedContact(eventSource, pair, offset));
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

            //WHICH CELL is ShotPlacement's answer, and deliberately not this file's any more (#70): the aim
            //preview asks the very same function from the barrel's line every frame, and a preview that is a
            //second implementation of this rule is a preview that eventually lies about where a shot will go.
            //Deciding and placing are separate steps for the same reason — the preview must decide without
            //writing anything into the map.
            bool solved;
            XZLevel cell;

            //And WHERE that cell is, which the cell itself does not say: the lattice is where the level hung the
            //field, while the structure hangs stretched under the glass and is dragged further down with every
            //descent. Both branches answer it, each from what it has — the hit ball's own pose, or the plate's.
            Vector3 clusterDrift;

            if (other.Mobility == CollidableMobility.Kinematic && other.BodyHandle == _ceiling.BodyHandle)
            {
                //Straight into the glass, past the whole cluster: it lands on the field's top level. The plate's
                //live centre, not the height the level hung it at — a ball attaching after six descents comes to
                //rest six descents lower, and that is the only thing the descent changes here.
                solved = ShotPlacement.TrySolveAgainstCeiling(_map, worldContact, _worldOffset,
                    _ceiling.BodyReference.Pose.Position.Y, out cell, out clusterDrift);
            }
            else if (other.Mobility == CollidableMobility.Dynamic && TryFindStructureBall(other.BodyHandle, out PhysicsBall hitBall))
            {
                solved = ShotPlacement.TrySolveAgainstBall(_map, hitBall, worldContact, _worldOffset, out cell,
                    out clusterDrift);
            }
            else
            {
                //Another loose shot ball, or a released one on its way to the drain: neither is in the
                //structure, so neither has a cell to offer and the shot bounces off it. A rare-event line in
                //the manner of [cinematic] — a refusal happens a handful of times a level at worst, and when
                //one is reported from play this is the only thing that says which of the two kinds it was.
                Console.WriteLine($"[shot] bounced off a loose ball (mobility {other.Mobility})");
                return false;
            }

            //Nothing free in either ring around what it hit, or a ceiling cell outside the field or taken. The
            //shot does not stick, and that is an answer rather than a fault — see TryFindEmptyCellInSecondRing
            //on why the search is not simply widened until it succeeds.
            if (!solved)
            {
                //The "both rings full" refusal, and on a level with empty growth room under the cluster it
                //should be all but impossible — so when it fires it is nearly always saying that the layout
                //reaches the field's WALL, where a flank ball has no lateral neighbour to offer. That is
                //exactly what it said the first time it was switched on: one refusal in 34 varied-angle shots
                //on Pinwheel, whose disc was drawn to the edge of its field. LevelGen refuses that shape now
                //(LateralMargin), and this line is what would catch the next one.
                Console.WriteLine("[shot] bounced: no free cell in either ring");
                return false;
            }

            //Only now is anything written. The cell came back valid, so this cannot land out of bounds or on a
            //live ball — which used to be possible: the old ceiling path filled its out-cell in from the rounded
            //contact BEFORE testing bounds and occupancy, so a refused placement handed back a plausible-looking
            //cell, and testing the cell rather than the returned position indexed the structure array out of
            //bounds (a crash) or overwrote a ball that then stayed in the simulation for ever, untracked,
            //undrawn and unreleasable.
            _map.PutBallAt((byte)cell.X, (byte)cell.Z, (byte)cell.Level, physicsBall.Type);

            //Where the ball will actually come to rest, which is the cell taken up into the frame the cluster is
            //hanging in this instant rather than into the one the level hung the lattice in. The map's own returned
            //position (the raw centred lattice cell) used to be offset and used directly for both of the things
            //below, and both drifted with the ceiling: with the glass eleven steps down on Colossus.json that is ~6.6
            //units, so the glide launched the ball from that far BELOW its own impact and the award was born above
            //the cluster's roof. See ShotPlacement.CellWorldPosition.
            Vector3 restPosition = ShotPlacement.CellWorldPosition(_map, cell, _worldOffset, clusterDrift);

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
            //constraints exist, and in world frame, which is where the body is — the LIVE one, or the glide is
            //measured against a cell the cluster no longer hangs at and moves the ball the wrong way.
            physicsBall.StartRenderGlide(restPosition.ToNumerics());

            //Anchors come from the ideal lattice and are rotated into each body's current local frame, so
            //they are right even after the simulation has been running for a while
            BallsConstraintsBuilder.AttachBallToStructure(physicsBall, _physicsBalls, _map, _simulation, _ceiling.BodyReference);

            if (_contactEvents.IsListener(contact.EventSource)) _contactEvents.Unregister(contact.EventSource);

            //And the game rule: three or more of a colour touching each other let go, and so does anything
            //that was only held up by them
            BallsReleased released = BallsConstraintsBuilder.ReleaseSameTypeCluster(physicsBall, _physicsBalls, _map, _simulation, _fallingBalls);

            //Reported whether or not anything fell: a shot that stuck without completing a group is still a
            //resolved shot, and the streak rule has to hear about it. Taken before the release above could
            //have moved anything, and in world frame — where the cell the ball landed in actually is, since
            //everything downstream of this position is heard or seen at it (the thunk's panning, the award's
            //flight) and none of it may be placed where the cluster merely used to hang.
            BallLanded?.Invoke(new BallLanding(released, restPosition, physicsBall.Type, cell));

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

    }
}
