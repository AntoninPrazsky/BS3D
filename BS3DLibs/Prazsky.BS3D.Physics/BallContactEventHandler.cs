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
    /// <item><b>It listens on <see cref="OnTouching"/>, not on every <c>OnContactAdded</c></b>, so it never attaches
    /// off a contact a whole step early — see that method, which carries the measurement. (Since #410 it does take a
    /// ball contact that stops within the speculative margin, which the swept shot bounds to a tenth of a unit — see
    /// <see cref="OnContactAdded"/>.)</item>
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

        /// <summary>
        /// Scratch for <see cref="BallsMap.ColourTransparentNeighbours"/>, reused by every shot of the level:
        /// the walk fills it with at most the twelve cells that touch a landing, and this is a gameplay path
        /// where a fresh list per shot is a per-landing allocation for nothing (BestPractices.md §5).
        /// </summary>
        private readonly List<XZLevel> _colouredCells = new(12);

        /// <summary>
        /// The bombs standing beside the cell a shot just landed in (#326), collected <b>before</b> the group
        /// release so a bomb that the release orphans is not still on the list when the blast runs. Reused per
        /// shot for <see cref="_colouredCells"/>' reason, and the same size: a cell touches at most twelve
        /// others.
        /// </summary>
        private readonly List<XZLevel> _armedBombs = new(12);

        /// <summary>
        /// The zaps standing beside that same cell (#327), on exactly <see cref="_armedBombs"/>' terms —
        /// collected in the same walk, before the group release, and for the same reason.
        /// </summary>
        private readonly List<XZLevel> _triggeredZaps = new(12);

        //And the acids (#328), off the same walk and on the same trigger. A cell touches at most twelve, so a
        //landing can arm no more than that many of anything.
        private readonly List<XZLevel> _triggeredAcids = new(12);

        /// <summary>
        /// Scratch for the ice this landing's group broke (#329), reused by every shot of the level the way
        /// <see cref="_colouredCells"/> is. It is <b>filled by the release</b> rather than collected here,
        /// which is the whole difference between this kind and the three above it: a thaw is keyed to the
        /// group leaving, not to the landing, so the rule lives in
        /// <c>BallsConstraintsBuilder.ReleaseSameTypeCluster</c> and this list is only how the count gets back
        /// out to the log and to <see cref="BallLanding"/>.
        /// </summary>
        private readonly List<XZLevel> _thawedCells = new(12);

        /// <summary>
        /// Every bomb this landing set off, as a <see cref="Detonation"/> — where its body was, how far down a
        /// chain it came and what it took (#389) — filled by the release, like <see cref="_thawedCells"/> and
        /// unlike the three armed lists, because since #396 a bomb has a trigger the landing cannot see: losing
        /// its last path to the glass. Handed out on <see cref="BallLanding.Detonations"/>, which is why it is
        /// emptied on every landing rather than only on one that arms a bomb, and a field rather than a list per
        /// shot on <see cref="_colouredCells"/>' reasoning: a landing is the one moment that must not stall.
        /// <para>
        /// <b>It is handed to all FOUR removals and emptied once</b>, which is why it is not sized off a
        /// cell's twelve neighbours the way the armed lists are: a match, a zap, a shaft and a blast can each
        /// orphan bombs, and a chain through the disconnection walk has no neighbour count to bound it — its
        /// bound is the number of bombs on the field.
        /// </para>
        /// <para>
        /// <b>Why the count is not read off <c>released.Destroyed</c></b>, which is what the landing line used
        /// before: an orphan-triggered blast in the middle of a region that was already falling destroys
        /// nothing at all (its victims were leaving anyway and stay orphans, see
        /// <c>BallsConstraintsBuilder.ResolveDisconnected</c>), so that test answers "no bomb went off" about a
        /// bomb that visibly exploded.
        /// </para>
        /// <para>
        /// <b>#396 and #389 met here.</b> #396 kept this a list of cells for the log alone and left the record to
        /// #389, which had been handing out a list filled by the blast only; merged, it is one list of
        /// <see cref="Detonation"/>s filled by all four removals, so a bomb the walk fires gets its flash, its
        /// report and its jolt like any other — they come out of the same loop.
        /// </para>
        /// </summary>
        private readonly List<Detonation> _detonations = new(8);

        /// <summary>
        /// The body handle of the last shot that was logged bouncing off the glass (#432), so the line prints once
        /// per shot and not once per step the ball stays in touch. -1 for none. A handle Bepu later recycles for
        /// another shot only costs that shot its log line, which is all this is for.
        /// </summary>
        private int _lastGlassBounce = -1;

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
        /// have reached. (What that measured was an <i>unbounded</i> margin. <see cref="OnContactAdded"/> now takes
        /// the near-touches the swept shot's bounded margin produces against a ball, which is a different and much
        /// smaller thing — see there, #410.)
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
        /// <summary>
        /// Runs on a Bepu worker thread, inside the timestep, for every contact a listening shot gains — and records
        /// the ones <see cref="OnTouching"/> will never see: a shot meeting a <b>ball</b> within the speculative
        /// margin without reaching <c>depth &gt;= 0</c> (#410). Records and returns, like its sibling.
        /// <para>
        /// <b>Why a near-touch counts.</b> The landing preview decides a shot hits a ball when the two surfaces
        /// meet (<see cref="ShotPlacement.TryFindFirstHitCurved"/> sweeps the sum of the radii), while
        /// <c>OnTouching</c> is only raised once the solver lets the pair actually overlap. A grazing shot sits
        /// exactly between the two: the speculative contact turns it away a fraction of a unit short of the
        /// surface, so the preview drew a ghost and the shot flew on — past the ball to the glass, out through the
        /// bottom, or wedged at rest in a pocket with nothing ever touching. Measured with the real handler and
        /// simulation on twelve shipped levels (a scratchpad rig: the preview's own two calls, then a real shot, a
        /// pause of 2.5 s between shots): <b>about 3 % of the shots the preview promised a cell were refused</b>
        /// (43 of 1441 over three runs), fifteen of seventeen in one run with the preview's hit in the outer fifth
        /// of the radius sum. Counting a contact down to the margin, with loose balls no longer in the way (see
        /// <c>NarrowPhaseCallbacks</c>), took that to 0.8 % (8 of 987, two runs), and the share of shots landing in
        /// the very cell the ghost showed did not get worse (43 % pooled before, 48 % after, inside what single runs
        /// scatter by).
        /// </para>
        /// <para>
        /// <b>It is not #70's early attach coming back</b>, and the difference is the bound. That attach fired on
        /// contacts generated up to a whole step of travel early by an unbounded margin (worst placement 3.79
        /// units); the shot's collidable is swept now and its margin is the structure's own
        /// <see cref="BallsConstraintsBuilder.SPECULATIVE_MARGIN"/>, so a contact that reaches here is at most that
        /// far from the surface. Balls only: a near-touch of the stone or the drain is not a touch of anything a
        /// shot can land on, and the glass decides nothing (#432).
        /// </para>
        /// </summary>
        public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
            Vector3 contactOffset, Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex)
            where TManifold : unmanaged, IContactManifold<TManifold>
        {
            //A real touch is OnTouching's, which also retries it every step the pair stays in contact
            if (depth >= 0f || depth < -BallsConstraintsBuilder.SPECULATIVE_MARGIN) return;
            if (pair.A.Mobility != CollidableMobility.Dynamic || pair.B.Mobility != CollidableMobility.Dynamic) return;

            _queuedContacts.Enqueue(new QueuedContact(eventSource, pair, contactOffset));
        }

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

            _queuedContacts.Enqueue(new QueuedContact(eventSource, pair, offset.ToXna()));
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

            CollidableReference other = pair.A.Packed == contact.EventSource.Packed ? pair.B : pair.A;

            //THE GLASS DECIDES NOTHING (#432). A shot that strikes the ceiling used to be attached to it, on the
            //field's top level under the point it hit — and where that was open glass it hung there ALONE, off
            //nothing but its ceiling socket, often right at the plate's edge: a ball that dangles and jiggles, can
            //only be cleared by building a group around it, and was never shown by the landing preview, which
            //has only ever solved against a ball. The owner's ruling is that a player's shot attaches to balls
            //and to nothing else; the level's own top course stays hung from the glass exactly as it was built.
            //
            //So the contact is simply not an event: the ball bounces off the plate as the solver already makes it,
            //and it goes on LISTENING — deliberately, and for two reasons. A shot that grazes the glass and meets a
            //ball on the way down has touched a ball, which is what a shot is allowed to stick to; and ending the
            //shot here would make a glass contact and a ball contact queued in the same step race each other, the
            //outcome depending on which the worker threads enqueued first. And the miss is then resolved where
            //every other miss is — on the island's stone below, or at the kill plane — which this path never did:
            //it unregistered the ball on touching the glass, so a shot that bounced off a taken ceiling cell was
            //never reported spent at all.
            if (other.Mobility == CollidableMobility.Kinematic && other.BodyHandle == _ceiling.BodyHandle)
            {
                //Once per shot rather than once per step it stays in touch, in the manner of the rare-event lines
                //below: the only record, when a player reports a shot that "should have stuck", that it met glass.
                if (wasListening && _lastGlassBounce != contact.EventSource.BodyHandle.Value)
                {
                    _lastGlassBounce = contact.EventSource.BodyHandle.Value;
                    Console.WriteLine("[shot] bounced off the glass");
                }

                return false;
            }

            //A ball that has touched anything else static or kinematic has had its shot: it hit the island or
            //the drain, and it is no longer a candidate for attaching. Stop listening to it — the same timestep
            //can queue several contacts for one ball, so the listener may already be gone.
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
            //descent. The solve answers it from the hit ball's own pose. (There was a second branch here that
            //answered it from the plate's, for a shot attaching to the glass itself — gone with #432, above.)
            Vector3 clusterDrift;

            if (other.Mobility == CollidableMobility.Dynamic && TryFindStructureBall(other.BodyHandle, out PhysicsBall hitBall))
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

            //Nothing free in either ring around what it hit. The shot does not stick, and that is an answer rather than a fault — see TryFindEmptyCellInSecondRing
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

            //A WILDCARD STOPS BEING ONE HERE (#330), and this is the only place it can: everything below reads
            //the shot ball's Type — the cell it is written into, the colour the glass takes, the colour a zap
            //fires on, the group that is counted, the tint the award flies in — so the collapse has to be
            //finished before the first of them. It is #323's seams 1 and 3 in one step, which is what a
            //wildcard is.
            //
            //Beside nothing matchable it keeps the colour it already had, and that is not a fallback so much as
            //the honest answer: the ball's Type IS what the cycle was showing (GameplayScreen keeps it there
            //every frame it is in the air), so a wildcard that completes nothing lands as the colour the player
            //was looking at when it arrived.
            if (physicsBall.Kind == BallKind.Wildcard)
            {
                bool joined = _map.TryChooseWildcardColour(cell, out BallType wildcardColour, out int wildcardGroup);

                //THE MOMENT IT STOPS BEING ONE IS THE MOMENT WORTH SEEING (#437). Until here the ball has been
                //visibly, continuously dissolving between colours and never settling, which is the whole of how
                //a wildcard says what it is; the instant it resolves, that motion simply stopped wherever the
                //shared cycle happened to be, and an onlooker could not tell it from any other ball landing.
                //So the ball crosses into its new colour the way every other change of look in this game does
                //— two draws of it partitioning one ball's pixels, the colour it was wearing going out while
                //the colour it has become comes in — over ClusterCollector.LOCK_FADE_SECONDS.
                //
                //The outgoing colour is the Type it arrives with, which the comment above already explains is
                //what the cycle was showing, so this asks WildcardCycle nothing and #330's one clock stays the
                //one clock. Only when the colour actually changes: a wildcard that completes nothing keeps
                //what it had, and crossing a colour with itself draws two identical halves of one ball.
                if (joined && wildcardColour != physicsBall.Type)
                {
                    physicsBall.LockFromType = physicsBall.Type;
                    physicsBall.LockFadeRemaining = ClusterCollector.LOCK_FADE_SECONDS;
                }

                if (joined) physicsBall.Type = wildcardColour;

                physicsBall.Kind = BallKind.Normal;

                //A rare-event line in the manner of the two below it: a handful a level at most, and it is what
                //says whether the wildcards a level hands out are landing on anything worth completing
                Console.WriteLine(joined
                    ? $"[shot] wildcard landed as {physicsBall.Type} (was showing {physicsBall.LockFromType}), group of {wildcardGroup}"
                    : $"[shot] wildcard landed beside nothing matchable, kept {physicsBall.Type}");
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

            //The ball is snapped to the nearest free cell rather than to where it hit, so it has to cross up
            //to several diameters to get there. Drawing it gliding in from where it actually hit hides that
            //click without touching the simulation. Armed BEFORE the body is moved below — the glide is the
            //difference between the two, so it has to read the body while it is still at the impact point —
            //and in world frame, which is where the body is: the LIVE one, or the glide is measured against a
            //cell the cluster no longer hangs at and moves the ball the wrong way.
            physicsBall.StartRenderGlide(restPosition.ToNumerics());

            //And then the body itself is PUT in the cell, which it was not until #265. Letting the new
            //constraints drag it across instead looks equivalent and is not, because of what the ceiling
            //socket anchors: the ball's own north pole (local +BALL_RADIUS) against a fixed point under the
            //plate. That pair has TWO solutions — the ball hanging under the anchor, and the ball sitting
            //inverted on top of it — and a ball dragged in from above and to one side settles into the wrong
            //one whenever it has too few neighbours to break the tie. It is a perfectly valid solution, so it
            //is stable, and the body goes to SLEEP there: measured at 1.16 units above its cell, which puts a
            //top-level ball's centre above the plate's own centre. That is #265's "attaches visually outside
            //the ceiling plate", and it stays put because nothing is wrong enough to move it.
            //
            //The same drag-in produced the other half of the report as well. A socket anchored on the ball's
            //surface turns the correcting impulse into a TORQUE, and nothing in this simulation damps angular
            //velocity — PoseIntegratorCallbacks.IntegrateVelocity adds gravity and nothing else — so a ball
            //spun up on the way in keeps spinning: 1-3 rad/s still, fifteen seconds later, never sleeping.
            //
            //Placing the body first makes the constraint error zero at the moment it is created, so there is
            //no impulse, no torque and no second solution to fall into. The orientation is deliberately left
            //alone: a shot has no torque on it in flight, so it arrives near identity, and the measured fix
            //needs no reset — while resetting it would snap the drawn ball's procedural pattern in place.
            physicsBall.BallReference.Pose.Position = restPosition.ToNumerics();

            //The pose history must not straddle that placement: the snapshot taken before this step holds the
            //flight pose, and interpolating between it and the cell (#293) would replay the very drag-in the
            //placement above exists to remove. The glide armed above owns the visual journey instead; the
            //history re-arms on the next step's snapshot.
            physicsBall.ResetPoseHistory();

            //Anchors come from the ideal lattice and are rotated into each body's current local frame, so
            //they are right even after the simulation has been running for a while
            BallsConstraintsBuilder.AttachBallToStructure(physicsBall, _physicsBalls, _map, _simulation, _ceiling.BodyReference,
                _worldOffset.ToNumerics());

            if (_contactEvents.IsListener(contact.EventSource)) _contactEvents.Unregister(contact.EventSource);

            //The glass takes the colour that just arrived (#325), BEFORE the group is counted and after the
            //attach above — which is the whole of where this may go. Before, and the ball is not in the lattice
            //yet so there is nothing to be a neighbour of; after the release, and a shot that completes a group
            //THROUGH a transparent ball would have been counted without it, which is precisely the shot the
            //kind exists to make worth aiming. So the colouring is a step of the landing rather than a
            //consequence of it, and the group check that follows sees a cluster the colouring has already
            //finished changing.
            //
            //⚠ AND IT IS THE WHOLE CONNECTED BODY OF GLASS, not the panes touching the landing (#344). That
            //makes this one step of the landing rather more than it was — a pane of a dozen can become a
            //dozen-strong group of one colour here, which the release below then takes in full — but it does
            //not move it: the colouring still finishes before anything is counted, so the census and the group
            //check never see a half-coloured pane.
            int coloured = _map.ColourTransparentGroup(cell, physicsBall.Type, _colouredCells);

            for (int i = 0; i < coloured; i++)
            {
                XZLevel at = _colouredCells[i];
                PhysicsBall glass = _physicsBalls[at.X, at.Z, at.Level];

                //The map is the truth about what a ball IS and the physics array is its mirror (#323); both
                //have to move together or the flood fill and the draw would disagree about the same cell for
                //as long as the level lasts.
                if (glass == null) continue;

                glass.Type = physicsBall.Type;
                glass.Kind = BallKind.Normal;
                glass.ColourFadeRemaining = ClusterCollector.COLOUR_FADE_SECONDS;
            }

            //WHICH BOMBS THIS LANDING ARMED (#326), read here and set off further down. It is the transparent
            //ball's rule with a different effect on the end of it: a landing is a PLACE, and what goes off is
            //what stands next to that place - which is the only version of "when it is hit" the player can
            //watch happen and aim at, since they aim at the gap beside a ball and never at a ball itself.
            //EVERY bomb beside the cell goes, not a chosen one, for ColourTransparentNeighbours' own reason:
            //choosing among several would be invisible dice, two identical-looking landings doing different
            //things.
            //
            //⚠ Collected BEFORE the release below and detonated AFTER it, and both halves are deliberate. The
            //shot's own match runs FIRST: a player who lands beside a bomb while completing a group has earned
            //that group, and a blast that ate it before it was counted would read as the game refusing a good
            //shot. So the two compose, in the order the player did them. Reading the list afterwards is what
            //lets the detonation re-check each cell, since the release may have taken it.
            //The zaps beside it come off the same walk (#327): same trigger, same "collect before, fire after".
            //
            //⚠ THIS USED TO SAY "a bomb the release ORPHANS has already fallen and must not go off in mid-air",
            //and #396 REVERSED that: a bomb that loses its last path to the glass now goes off where it hangs,
            //because "it is separated, so it goes off" is the rule the kind always meant and a shot landing
            //beside it was only the case that happened to be implemented. The rule is not stated here — the
            //release itself carries it, in BallsConstraintsBuilder.ResolveDisconnected, so none of the four
            //removals below (nor Tools/LevelGen/SagProbe, which repeats this whole sequence) needed a line.
            CollectArmedSpecials(cell);

            //And the game rule: three or more of a colour touching each other let go, and so does anything
            //that was only held up by them
            //THE ICE BREAKS INSIDE THIS CALL (#329), not around it like the three destroyers below. A frozen
            //ball's trigger is the GROUP leaving rather than the landing, and this is the only place in the
            //game where a group leaves — so the rule sits there and this list is only how the count gets back
            //out. Nothing else on this path had to be added, and nothing in the Testbed or the sag probe did
            //either, which is the whole argument for putting it there; see that method's remarks.
            //Where the list of loose balls stood before this landing, so what it releases can be marked below (#410)
            int fallingBefore = _fallingBalls.Count;

            BallsReleased released = BallsConstraintsBuilder.ReleaseSameTypeCluster(physicsBall, _physicsBalls,
                _map, _simulation, _fallingBalls, _thawedCells, _detonations);

            //Then the zap, over whatever the match left standing (#327). ⚠ BEFORE the blast, and the order is
            //a ruling: a blast that ate the zap ball first would silently swallow one of the two effects the
            //player armed with one landing, while a zap can never take a bomb (it only takes MATCHABLE balls,
            //and a bomb's colour is one nothing may read). Wide first, then local, so both always happen.
            if (_triggeredZaps.Count > 0)
                released = released.Plus(BallsConstraintsBuilder.ZapColour(
                    _triggeredZaps, physicsBall.Type, _physicsBalls, _map, _simulation, _fallingBalls,
                    _detonations));

            //Then the acid (#328), still ahead of the blast and on the zap's argument exactly: a blast that ate
            //the acid ball first would silently swallow one of the effects the player armed with one landing,
            //and a blast reaches two cells in every direction while a shaft only ever meets a bomb standing
            //DIRECTLY beneath its acid — so this order swallows less, by a wide margin, than the other one.
            //⚠ What it costs is stated rather than hidden: an armed bomb caught in a shaft is dissolved instead
            //of going off. DetonateBombs re-checks each of its cells and skips the ones that have left, so that
            //is safe rather than merely survivable, and it is the acid's own flavour — it eats what is under it.
            if (_triggeredAcids.Count > 0)
                released = released.Plus(BallsConstraintsBuilder.DissolveAcids(
                    _triggeredAcids, _physicsBalls, _map, _simulation, _fallingBalls, _detonations));

            //Then the blast, over whatever the match left standing - and its own disconnection pass runs
            //inside it, so a hole opened under half the cluster brings that half down as well.
            //⚠ _detonations is NOT emptied here, although #389 first did exactly that: since #396 the three
            //removals above can fire bombs too, and a clear at this point would throw their records away — an
            //orphaned bomb would then go off with no flash and no report. It is emptied once per landing, in
            //CollectArmedSpecials.
            if (_armedBombs.Count > 0)
                released = released.Plus(BallsConstraintsBuilder.DetonateBombs(
                    _armedBombs, _physicsBalls, _map, _simulation, _fallingBalls, _detonations));

            //Everything the four removals just let go of is LOOSE from here on, and a shot still in the air passes
            //through it (#410; the filter is NarrowPhaseCallbacks'). Marked here because this is the one place a
            //player's shot releases anything; PhysicsWorld.RetireBall clears the mark when the ball is culled.
            for (int i = fallingBefore; i < _fallingBalls.Count; i++)
                _contactEvents.MarkLoose(_fallingBalls[i].BallReference.Handle);

            //Reported whether or not anything fell: a shot that stuck without completing a group is still a
            //resolved shot, and the streak rule has to hear about it. Taken before the release above could
            //have moved anything, and in world frame — where the cell the ball landed in actually is, since
            //everything downstream of this position is heard or seen at it (the thunk's panning, the award's
            //flight) and none of it may be placed where the cluster merely used to hang.
            //One line for the whole landing, and only when there was glass in it (#325): a level whose
            //transparent balls are not being coloured says so here instead of being diagnosed from
            //screenshots, and printing the release beside the count is what says whether the colouring
            //COMPLETED anything — which is the shot the kind exists to make worth aiming.
            if (coloured > 0) Console.WriteLine($"[shot] coloured {coloured} transparent ball(s) {physicsBall.Type}"
                + $"; released {released.Matched} matched, {released.Orphaned} orphaned");

            //The blast's own line, on the same terms and for the same reason (#326): a level whose bombs are
            //not going off says so here rather than being diagnosed from a screenshot. How many WENT OFF beside
            //how many the landing armed is what tells a chain apart from a single big radius — which the armed
            //count alone never could (#389): one landing that sets off a chain of five arms exactly one bomb.
            //⚠ The test is the number of bombs that FIRED and no longer "destroyed > 0" (#396): an orphaned
            //bomb going off inside a region that was already falling destroys nothing, so the old test printed
            //nothing for it. Fired beside armed is what tells a chain from a single radius, which is what the
            //armed count was always for — it does NOT say which of the two indirect triggers reached a bomb,
            //since a blast's radius has chained into one since #326 and the walk only since #396. What says
            //the walk fired one is "fired > 0 with nothing armed", and that is the shape of a match, a zap or
            //a shaft that cut a bomb's last support.
            if (_detonations.Count > 0 || released.Destroyed > 0)
                Console.WriteLine($"[shot] {_armedBombs.Count} bomb(s) armed,"
                + $" {_triggeredZaps.Count} zap(s) and {_triggeredAcids.Count} acid(s) triggered at the landing;"
                + $" {_detonations.Count} bomb(s) fired,"
                + $" destroyed {released.Destroyed}, orphaned {released.Orphaned}");

            //And the ice's own line, on the glass's terms and for its reason (#329): a level whose frozen balls
            //are never thawing says so here rather than being diagnosed from screenshots, and printing the
            //MATCHED count beside it is what tells "the group was too far from the ice" apart from "no group
            //completed at all" — the two ways a level of ice fails to open, which look identical on screen.
            if (_thawedCells.Count > 0) Console.WriteLine($"[shot] thawed {_thawedCells.Count} frozen ball(s)"
                + $" beside a group of {released.Matched}");

            BallLanded?.Invoke(new BallLanding(released, restPosition, physicsBall.Type, cell, coloured,
                _thawedCells.Count, _detonations));

            return true;
        }

        /// <summary>
        /// Fills <see cref="_armedBombs"/> and <see cref="_triggeredZaps"/> with every
        /// <see cref="BallKind.Bomb"/> and <see cref="BallKind.Zap"/> touching <paramref name="cell"/> — the
        /// landing's own neighbours, by the map's neighbour rule and not by a second copy of it, which is the
        /// same discipline <see cref="BallsMap.ColourTransparentGroup"/> keeps for the glass.
        /// <para>
        /// <b>One walk for both</b>, because they share a trigger exactly: what sets either off is a shot
        /// landing in a cell beside it. Two walks would be two chances for the two kinds to drift apart about
        /// what "beside" means, which is the whole reason neither of them counts neighbours itself.
        /// </para>
        /// </summary>
        private void CollectArmedSpecials(XZLevel cell)
        {
            _armedBombs.Clear();
            _triggeredZaps.Clear();
            _triggeredAcids.Clear();

            //Emptied HERE although nothing here fills it (#396): it is the one point every landing passes
            //through before any removal runs, and the four removals below all append to it rather than clear
            //it — a detonation belongs to a landing, not to one of its four steps. Clearing it inside any of
            //them would throw away the bombs an earlier step had already set off. And it has to be emptied on
            //EVERY landing, not only on one that arms a bomb (#389): BallLanding hands this list out, and a
            //landing that set nothing off must not report the previous one's blasts.
            _detonations.Clear();

            StaticBall[,,] cells = _map.GetStaticBallsArray();
            XZLevel size = new(_map.StageSizeX, _map.StageSizeZ, _map.Levels);

            foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(cell, size))
            {
                StaticBall ball = cells[neighbour.X, neighbour.Z, neighbour.Level];
                if (ball == null) continue;

                if (ball.Kind == BallKind.Bomb) _armedBombs.Add(neighbour);
                else if (ball.Kind == BallKind.Zap) _triggeredZaps.Add(neighbour);
                else if (ball.Kind == BallKind.Acid) _triggeredAcids.Add(neighbour);
            }
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
