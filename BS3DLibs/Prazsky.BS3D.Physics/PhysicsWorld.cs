using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities;
using BepuUtilities.Memory;
using Prazsky.Core.Tools;
using System;
using System.Numerics;

namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// The physics session's own <b>hardware</b>: the worker pool the solver runs on, the buffer pool everything
    /// in the simulation is allocated from, the <see cref="BepuPhysics.Simulation"/> itself, and the
    /// <see cref="ContactEvents"/> stream wired into its narrow phase. Four objects with two orders between
    /// them, and both orders are easy to get wrong in a way that does not fail loudly — which is the whole
    /// reason this is a type and not four fields.
    /// <para>
    /// It stood in both executables until #76 — the same <c>ThreadDispatcher(Environment.ProcessorCount)</c>,
    /// the same <see cref="BufferPool"/>, the same <see cref="ContactEvents"/> over both, the same
    /// <see cref="Simulation.Create"/> with <see cref="Simu.NarrowPhaseCallbacks"/>,
    /// <see cref="Simu.PoseIntegratorCallbacks"/> at <see cref="Constants.EARTH_GRAVITY"/> and
    /// <c>SolveDescription(8, 1)</c>, value-identical down to the argument order. The <b>two</b> places the
    /// copies had drifted are both resolved here in the Game's favour, because in both the Game is right:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// <b>The Testbed called <c>_events.Initialize(_simulation)</c> a second time</b> (issue #73).
    /// <see cref="Simulation.Create"/> already runs <see cref="Simu.NarrowPhaseCallbacks.Initialize"/>, which
    /// <i>is</i> what initialises the stream; the Game's copy carries the correction as a comment where the
    /// call used to be — <i>"No _events.Initialize(_simulation) here: Simulation.Create has already called
    /// NarrowPhaseCallbacks.Initialize, which is what initialises it. Calling it again would hook its
    /// BeforeCollisionDetection handler onto the timestepper a second time."</i> A second hook means the
    /// freshness pass runs twice per step, which does not crash and does not visibly misbehave — it silently
    /// costs a full walk of every listener's previous collisions on every step. There is one
    /// <see cref="ContactEvents.Initialize"/> call in the program now and the constructor below owns it.
    /// </description></item>
    /// <item><description>
    /// <b>The Testbed never disposed the contact events at all.</b> Its teardown was
    /// <c>Simulation.Dispose() → ThreadDispatcher.Dispose() → BufferPool.Clear()</c>, leaking the stream's two
    /// <c>IndexSet</c>s and its <c>CollidableProperty</c> back to nobody and leaving its
    /// <c>BeforeCollisionDetection</c> hook on a disposed timestepper. Harmless at process exit, which is the
    /// only place the Testbed tore down — and not harmless at all for a per-level session, which is why the
    /// Game found it. <see cref="Dispose"/> below is the Game's order.
    /// </description></item>
    /// </list>
    /// <para>
    /// <b>The stepping policy is deliberately NOT unified, and this type is shaped so that it cannot be.</b>
    /// "Physics in the game" in docs/game-session.md records the decision: <i>"The one place the game
    /// deliberately does not do what the Testbed does is the timestep. The Testbed takes one
    /// <c>Timestep(min(frameTime, 1/60))</c> per rendered frame, which runs the simulation in slow motion below
    /// 60 FPS (at 30 FPS everything moves at half speed) and with a dt that varies with the display and the
    /// load above it — and Bepu's own guidance is to keep the step constant. The game accumulates the frame
    /// time and spends it in whole steps of <c>PHYSICS_TIMESTEP</c> (1/120 s), capped at
    /// <c>PHYSICS_MAX_STEPS_PER_FRAME</c> so a frame that hitched drops the remainder instead of spiralling. It
    /// runs <c>IsFixedTimeStep = false</c> and offers <c>nocap</c>, i.e. precisely the configuration the
    /// Testbed's approach breaks under."</i> So <see cref="Step"/> takes <b>one</b> step of the length it is
    /// given and returns: how long a step is, how many are taken per frame, what happens to a remainder and
    /// whether the frame's time was scaled first (the Testbed's F9 slow motion, the Game's drop cinematic) are
    /// every bit the caller's. What this owns is only the order <i>inside</i> a step — see <see cref="Step"/>.
    /// </para>
    /// <para>
    /// <b>One entry point rather than two.</b> A second overload that accumulated for the caller was the
    /// obvious alternative and is the wrong shape twice over: it would put one executable's policy inside the
    /// shared type, and it would offer the other an accumulator it must not have — the Testbed's slow-motion
    /// and stop-the-world debug keys are exactly what a fixed-step accumulator would fight. Two entry points
    /// would also read as a menu of interchangeable options, when what is actually recorded above is a measured
    /// decision that one of them is worse. One <see cref="Step"/> the caller drives keeps the divergence
    /// visible where it belongs — in each caller's own loop, next to the comment explaining it.
    /// </para>
    /// <para>
    /// <b>The thread dispatcher is deliberately not exposed.</b> Nothing outside ever needed it for anything but
    /// the <c>Timestep</c> call this type now owns, and keeping it private removes the easy path to a
    /// multithreaded step taken behind <see cref="Step"/>'s back — which would skip the flush and the contact
    /// handling and leave a queue of contacts describing a world that has already moved on. The buffer pool and
    /// the simulation <i>are</i> exposed, because both are handed to the shared builders (
    /// <see cref="FunnelPhysics.Build"/> takes the pool; every one of
    /// <see cref="BallsConstraintsBuilder"/>'s entry points takes the simulation), and so is
    /// <see cref="Events"/>, because each executable's contact handler is constructed with it.
    /// </para>
    /// <para>
    /// <b>What deliberately stayed with the callers.</b> The <b>lifetime</b>: the Testbed builds one of these in
    /// <c>Initialize</c> and keeps it for the whole run, swapping maps inside it; the Game builds one per level
    /// in <c>BuildPhysicsWorld</c> and disposes it in <c>TearDown</c>, because tearing a level down by disposing
    /// the simulation outright is cheaper and more certain than emptying it ball by ball. Both are served by
    /// constructing and disposing this, and neither lifetime is written into it. The <b>static geometry</b>: the
    /// island's funnel floor is <see cref="FunnelPhysics"/>' already and is handed figures off
    /// <c>ArenaIsland</c>, which this library cannot see; the kinematic glass ceiling is a genuine divergence
    /// (the Testbed keeps one body for the whole run and swaps its box shape per map, since constraints from a
    /// previous structure may still reference it, while the Game gives each level a fresh body on a fresh
    /// simulation), so neither is built here. The <b>structure teardown</b>
    /// (<c>RemoveCurrentBallsStructure</c>, <c>RemoveAllConstraints</c>, <c>RemoveDynamicBalls</c>) exists in
    /// the Testbed <i>only</i>, and for that same reason — the Game has no map to swap under a live simulation
    /// — so hoisting it would not remove a copy, and what it walks is the ball structure rather than the
    /// hardware. And the <b>kill-plane cull</b> stays split, deliberately: the two disagree on whether a
    /// sleeping ball is culled, and docs/game-session.md records that as a policy rather than a drift —
    /// <i>"a ball that settles on the island's stone winks out in front of the player, which reads as a bug
    /// whatever it saves."</i> What both culls share to the line is one primitive, and that is
    /// <see cref="RetireBall"/>.
    /// </para>
    /// </summary>
    public sealed class PhysicsWorld : IDisposable
    {
        /// <summary>
        /// The solver's velocity iterations and substeps, i.e. <c>SolveDescription(8, 1)</c> in that order.
        /// Named because the two positional integers say nothing at the call site, and kept together because
        /// they are tuned <b>with</b> the contact material in <see cref="Simu.NarrowPhaseCallbacks"/> and the
        /// <see cref="BallsConstraintsBuilder.SPRING_SETTINGS"/> of the constraints holding the cluster up: a
        /// hundreds-of-bodies structure hanging off one kinematic plate is a stiff constraint graph, and these
        /// three move together or not at all.
        /// </summary>
        public const int VELOCITY_ITERATION_COUNT = 8;

        /// <inheritdoc cref="VELOCITY_ITERATION_COUNT"/>
        public const int SUBSTEP_COUNT = 1;

        /// <summary>
        /// Squared velocity under which a shot ball's body is allowed to fall asleep — the same figure
        /// <see cref="BallsConstraintsBuilder"/> gives the structure's balls and the ceiling body. A ball that
        /// has come to rest on the stone leaves Bepu's active set and is then drawn but barely simulated, which
        /// is what makes the Game's decision not to cull such a ball cheap enough to take.
        /// </summary>
        public const float SLEEP_THRESHOLD = Constants.HUNDREDTH;

        //Private on purpose: see the class remarks on why nothing outside may reach the dispatcher.
        private readonly ThreadDispatcher _threadDispatcher;

        /// <summary>
        /// The pool everything in the simulation is allocated from, including the shared builders' buffers —
        /// <see cref="FunnelPhysics.Build"/> takes its triangles from it and hands them to a <see cref="Mesh"/>
        /// that the pool's own teardown releases. Exposed for exactly that; it must outlive the simulation, so
        /// it is cleared last (see <see cref="Dispose"/>).
        /// </summary>
        public BufferPool BufferPool { get; }

        /// <summary>The simulation itself — every shared builder takes it, so it is public and read-only.</summary>
        public Simulation Simulation { get; }

        /// <summary>
        /// The contact stream, already initialised: <see cref="Simulation.Create"/> ran
        /// <see cref="Simu.NarrowPhaseCallbacks.Initialize"/> during construction, which is the one and only
        /// <see cref="ContactEvents.Initialize"/> the program performs — see the class remarks and issue #73.
        /// <para>
        /// Exposed because each executable's <c>BallContactEventHandler</c> is constructed with it and reads
        /// <see cref="ContactEvents.IsListener"/> to ask whether a shot has resolved.
        /// <b><see cref="ContactEvents.Flush"/> is this type's to call and nobody else's</b> — it belongs to the
        /// per-step order <see cref="Step"/> owns.
        /// </para>
        /// </summary>
        public ContactEvents Events { get; }

        //The template every shot is stamped from, built once with the simulation. Kept private and copied per
        //shot rather than mutated in place: both pre-hoist copies held it as a field and wrote this shot's pose
        //and velocity over the last one's, which is equivalent only for as long as every field is rewritten
        //every time.
        private readonly BodyDescription _shotBallTemplate;

        private bool _disposed;

        /// <summary>
        /// Stands the whole apparatus up, in the one order that works: the dispatcher first, because
        /// <see cref="ContactEvents"/> sizes its per-worker queues from the dispatcher's thread count; then the
        /// stream; then the simulation, whose construction is what initialises the stream against it.
        /// </summary>
        /// <param name="gravityY">
        /// Downward acceleration, in units per second squared. One axis rather than a vector because that is
        /// the only one that means anything to a game whose cluster hangs from a ceiling and whose balls drain
        /// through a hole in the floor; both callers passed <see cref="Constants.EARTH_GRAVITY"/>, which is the
        /// default.
        /// </param>
        public PhysicsWorld(float gravityY = Constants.EARTH_GRAVITY)
        {
            _threadDispatcher = new ThreadDispatcher(Environment.ProcessorCount);
            BufferPool = new BufferPool();
            Events = new ContactEvents(_threadDispatcher, BufferPool);

            //Both callback types are structs, copied by value into the simulation; the stream survives that
            //because it is a class reference held inside one of them.
            Simulation = Simulation.Create(
                BufferPool,
                new Simu.NarrowPhaseCallbacks(Events),
                new Simu.PoseIntegratorCallbacks(new Vector3(0f, gravityY, 0f)),
                new SolveDescription(VELOCITY_ITERATION_COUNT, SUBSTEP_COUNT));

            //The template a shot is stamped from. The collidable comes from the bare shape index rather than
            //from a CollidableDescription with a speculative margin, and that is load-bearing rather than
            //sloppy: it is what gives the shot continuous collision detection. At the shot speeds both
            //executables use a ball crosses several diameters in one step, and a discrete test would let it
            //pass clean through the cluster. The shape index is the shared sphere's, so a shot ball and a
            //structure ball are the same collidable to Bepu.
            Sphere ballShape = new(BallsConstraintsBuilder.BALL_RADIUS);

            _shotBallTemplate = BodyDescription.CreateDynamic(
                new Vector3(),
                ballShape.ComputeInertia(BallsConstraintsBuilder.BALL_MASS),
                BallsConstraintsBuilder.GetSphereShapeIndex(Simulation),
                SLEEP_THRESHOLD); //via the implicit conversion to BodyActivityDescription
        }

        /// <summary>
        /// Takes <b>one</b> step of <paramref name="dt"/> and runs the frame's contact work inside it, in the
        /// order that order has to be:
        /// <c>Timestep</c> → <see cref="ContactEvents.Flush"/> → the caller's work.
        /// <para>
        /// <b>That order is mandatory and it is per step, not per frame.</b> Bepu runs the contact callbacks on
        /// worker threads from inside <c>Timestep</c>, so a handler may only record what happened; the flush is
        /// what applies the per-worker adds those threads collected, and unregistering a listener is only safe
        /// once it has. Handling the contacts of two steps together is wrong for a second reason: a contact
        /// queued during a step describes a world the following step has already moved on from. Both
        /// executables got this right and both spelled the rule out in a comment beside it; it is a parameter
        /// here so that a caller <i>cannot</i> get it wrong — there is no way to take a step without handing
        /// over the work that belongs inside it.
        /// </para>
        /// <para>
        /// The step's <b>policy</b> is entirely the caller's, and the two callers deliberately differ — see the
        /// class remarks and "Physics in the game" in docs/game-session.md. Nothing here accumulates, clamps or
        /// counts: <paramref name="dt"/> is used exactly as given.
        /// </para>
        /// </summary>
        /// <param name="dt">
        /// The step's length in seconds, <b>already</b> whatever the caller's policy makes it — clamped,
        /// fixed, scaled for slow motion, all of it. Must be positive: a zero-length step divides by zero
        /// inside the solver, and the guard for a zero frame time belongs to the caller that can have one.
        /// </param>
        /// <param name="perStepWork">
        /// What the caller does with this step's contacts once they are flushed — in practice its handler's
        /// <c>ProcessQueuedContacts</c> and whatever it wants around that.
        /// <b>Hold this in a field rather than writing a method group or a lambda at the call site</b>: a
        /// delegate expression allocates a fresh delegate every time it is <i>evaluated</i>, and this is
        /// evaluated up to several times per frame. A delegate built once but reading its handler out of a
        /// field on each call also survives the handler being replaced per map or per level, which both
        /// callers do.
        /// </param>
        public void Step(float dt, Action perStepWork)
        {
            Simulation.Timestep(dt, _threadDispatcher);

            Events.Flush();
            perStepWork();
        }

        /// <summary>
        /// Adds a shot ball to the simulation from the shared template and registers it as a contact listener,
        /// in that order — a listener is keyed on a collidable reference, so the body has to exist first.
        /// <para>
        /// Registration is not optional and is not a separate call, because a shot ball that listens for
        /// nothing is a shot that never resolves. "Scoring" in docs/game-session.md leans on the converse of
        /// that: <i>"a ball stops listening at exactly the moment its shot resolves, and <c>Shoot</c> is the
        /// only place anything is registered, so every listener is a shot in the air"</i>. This method is that
        /// only place.
        /// </para>
        /// </summary>
        /// <param name="velocity">
        /// The shot's launch velocity — direction times speed, the speed being the caller's own (the two
        /// executables use different ones, and where the ball leaves from differs too: a muzzle in game mode, a
        /// free camera in the Testbed's default one).
        /// </param>
        /// <param name="listener">The handler the ball's contacts go to for as long as its shot is unresolved.</param>
        /// <returns>A reference to the new body, which the caller hangs on its <see cref="PhysicsBall"/>.</returns>
        public BodyReference AddShotBall(Vector3 position, Vector3 velocity, IContactEventHandler listener)
        {
            //A copy of the template, so nothing of this shot is left standing in it for the next one
            BodyDescription description = _shotBallTemplate;

            description.Pose.Position = position;
            description.Velocity.Linear = velocity;

            BodyHandle handle = Simulation.Bodies.Add(description);

            Events.Register(Simulation.Bodies[handle].CollidableReference, listener);

            return new BodyReference(handle, Simulation.Bodies);
        }

        /// <summary>
        /// Takes a ball's body out of the simulation, unregistering its contact listener first if it still has
        /// one. The single primitive under every removal path in both executables — the Testbed's
        /// <c>RemoveDynamicBalls</c> and <c>RemoveFallenBalls</c> and the Game's <c>RemoveFallenBalls</c> — all
        /// three of which had these same three lines in this same order.
        /// <para>
        /// <b>The order is the point.</b> A listener is keyed on the body's collidable reference; remove the
        /// body first and the flag stays set for a handle Bepu is free to hand to the next body added, which
        /// then silently <i>is</i> a listener with somebody else's handler. Nothing fails at the moment of the
        /// mistake, which is why it is worth having one place that cannot make it.
        /// </para>
        /// <para>
        /// The listener check is unconditional, where the pre-hoist copies took an <c>unregisterListeners</c>
        /// flag and passed <c>false</c> for released balls. That flag was only ever a statement of intent — a
        /// ball released from the structure was unregistered when it attached, so the probe answers false for
        /// it anyway — and the flag could only ever hide a genuinely still-registered ball, i.e. leave a
        /// dangling listener behind a removed body. The probe is a bitset lookup; the statement of intent
        /// belongs in a comment at the call site.
        /// </para>
        /// </summary>
        /// <returns>
        /// Whether the ball was <b>still listening</b> when it was retired, which is exactly the question the
        /// Game's kill-plane cull asks before it scores a miss: a shot ball that is still a listener at the
        /// bottom of the world resolved as nothing at all.
        /// </returns>
        public bool RetireBall(BodyReference ball)
        {
            bool wasListening = Events.IsListener(ball.CollidableReference);

            if (wasListening) Events.Unregister(ball.CollidableReference);

            Simulation.Bodies.Remove(ball.Handle);

            return wasListening;
        }

        /// <summary>
        /// Tears the four down in the one order that is safe, which is the reverse of the order they were built
        /// in. <see cref="ContactEvents"/> first: it unhooks its own <c>BeforeCollisionDetection</c> handler
        /// from the simulation's timestepper and returns its index sets to the pool, so it has to go while both
        /// are still there. Then the simulation, which releases its bodies, constraints, shapes and every
        /// buffer they held. Then the dispatcher and its per-worker pools. The buffer pool both the stream and
        /// the simulation allocated from is cleared <b>last</b>, because it has to outlive everything that took
        /// memory from it.
        /// <para>
        /// Idempotent, so a caller with two teardown paths (the Game has a per-level one and a shutdown one
        /// that runs it) cannot double-return the stream's buffers.
        /// </para>
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Events.Dispose();
            Simulation.Dispose();
            _threadDispatcher.Dispose();
            BufferPool.Clear();
        }
    }
}
