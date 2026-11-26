using BepuPhysics;
using BepuUtilities;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// <b>The push a shot gives the cluster it hits</b> (#1192, the owner's decision on #1007): a share of the momentum the
    /// shot carried, handed to the balls round the place it struck as an impulse along its flight, so the map moves when the
    /// player fires into it, whether the ball sticks or not.
    /// <para>
    /// <b>Why it has to be given.</b> A landing puts the shot's body in its lattice cell with both velocities zeroed
    /// (<see cref="BallContactEventHandler"/>, #265) and builds its sockets with no error, so until this the momentum the
    /// ball flew in with was simply deleted, and a cluster hit at 200 units a second did not answer at all: "players say I
    /// promised physics and there is none". A ball that sticks to what it hits hands that thing ALL of its momentum (a
    /// perfectly inelastic collision), so <see cref="MOMENTUM_SHARE"/> is a share of what physics would carry, not a force
    /// made up.
    /// </para>
    /// <para>
    /// <b>An impulse, applied once, between two steps</b>: a change of velocity, not a force integrated per step, so the
    /// push is the same whatever the frame rate and whatever step the caller takes (the Testbed's varies). A landing's is
    /// <b>walked before</b> the match and the three removals take what they take (<see cref="Prepare"/>) and <b>given
    /// after</b> them (<see cref="Deliver"/>): the cell the shot landed in was chosen before anything moved, the sockets
    /// start with no error, and the walk still finds what a released group hung from.
    /// </para>
    /// <para>
    /// <b>What the shot knocks out takes its share with it</b> (#1249, the owner: "throwing the impact energy away makes it
    /// seem there is no physics in the game"): a ball the walk found that the match, the zap, the acid, the blast or a cut
    /// has let go of since is pushed like one still hanging, by its hop's share along the flight, so a released group
    /// leaves in the direction the shot flew, fastest where it struck, and gravity takes it from there. Until then its
    /// share was discarded and a group dropped as if nothing had hit it. The shares are what they were, so the balls still
    /// hanging take exactly the push they took.
    /// </para>
    /// <para>
    /// <b>Spread by connectivity, falling off through the sockets</b>: a breadth-first walk over the balls that touch,
    /// outwards from the cell the shot struck to <see cref="REACH"/> hops, each hop taking <see cref="HOP_FALLOFF"/> of
    /// the one before. The stiff lattice spreads momentum to the whole cluster within a few steps anyway, so where it
    /// starts decides only how hard the first sockets are stretched: handed to one ball, the whole share stretched its
    /// sockets past what a heavy ball does (<see cref="BallsConstraintsBuilder.HEAVY_MASS_RATIO"/>'s envelope). And no
    /// ball's velocity changes by more than <see cref="MAX_KICK"/>, which bounds a shot into the tip of a strand, where
    /// the walk finds three balls to share what a block shares among thirty, and every shot a kind may one day carry much
    /// more momentum in (#1033's Fire Ball).
    /// </para>
    /// <para>
    /// <b>And the swing it starts is settled</b>: nothing in the lattice damps a hanging cluster's swing (the sockets damp
    /// only their own stretch, and the integrator has no drag), so a sheet a push set swaying swayed on for more than fourteen
    /// seconds, awake and stepped the whole time, where the same landing without the push slept within about five, and the
    /// swings of shot after shot added up. For <see cref="SETTLE_SECONDS"/> after a push, once its first
    /// <see cref="SETTLE_HOLD"/> has shown the hit at full size, every awake ball of the structure is dragged at
    /// <see cref="SETTLE_RATE"/>, as air and the joints would drag it. It runs on the simulation's own step (its timestepper,
    /// as <see cref="ContactEvents"/> takes its snapshot), so every caller that pushes settles alike, at any frame rate, and
    /// it unhooks itself when it is done. ⚠ It drags whatever moves the cluster, so on a tier with wind
    /// (<c>QualityPreset.ClusterWind</c>) the wind's sway is calmed for those seconds too; the wind keeps the cluster awake
    /// anyway, so there it is the swing's end and not the sleep the drag is for.
    /// </para>
    /// <para>
    /// <b>One copy, called wherever a shot strikes the structure</b>: the contact handler's landing, its first refusal of
    /// a shot and a cutter's strike, and <c>Tools/LevelGen/SagProbe</c>'s landing and refusal, which repeat the handler step
    /// for step so the sag gate measures the game played. Its walk's scratch is held per instance, as
    /// <see cref="ClusterRipple"/>'s is, so a push allocates nothing.
    /// </para>
    /// </summary>
    public sealed class ShotPush
    {
        /// <summary>
        /// The share of the shot's momentum (its mass times its velocity as it struck) the cluster takes, for every kind
        /// that does not state its own (<see cref="ShareFor"/>). One is a ball that sticks to what it hits, all of its
        /// momentum carried into the structure.
        /// </summary>
        public const float MOMENTUM_SHARE = 1f;

        /// <summary>How many hops through the sockets the push is handed out over, the struck place being hop 0.</summary>
        public const int REACH = 5;

        /// <summary>What each hop of the walk weighs against the one before it.</summary>
        public const float HOP_FALLOFF = 0.7f;

        /// <summary>
        /// The most one ball's velocity is changed by, in units a second, whatever the shot carried and however few balls
        /// the walk found to share it.
        /// </summary>
        public const float MAX_KICK = 8f;

        /// <summary>
        /// How long after a push the structure swings free before it is settled, in seconds of the simulation: the hit's
        /// first lean and its swing back are seen at the size the push gave them.
        /// </summary>
        public const float SETTLE_HOLD = 1f;

        /// <summary>
        /// The drag on every awake ball of the structure while a push settles, per second: each step keeps
        /// <c>exp(−SETTLE_RATE·dt)</c> of its velocity, so a swing's height dies away at half this rate. Chosen against the
        /// sleep it buys back (<c>ShotPushTests.APushedClusterFallsAsleepAgainWithinSeconds</c>): ten pushed landings on five
        /// levels slept again 1.0 to 5.5 s after the hit, where the same landings without the push slept in 0.3 to 5.1 s (or,
        /// on Pendant and Sail, not within fourteen), and with the push but no drag nine of the ten were still swinging
        /// fourteen seconds later.
        /// </summary>
        public const float SETTLE_RATE = 1.5f;

        /// <summary>How long the drag acts after <see cref="SETTLE_HOLD"/>, in seconds of the simulation.</summary>
        public const float SETTLE_SECONDS = 4f;

        /// <summary>
        /// <b>How hard a hit reads</b> (#1248): the share of the shot's momentum the cluster takes (over its kind's own,
        /// <see cref="ShareFor"/>), the most one ball's velocity changes by, and how long the swing runs free before it is
        /// settled and how fast it is settled then. The owner accepted <see cref="Original"/> on 2026-11-24, asked to try more
        /// energy, compared the three in play (the Game's <c>pushfeel=</c>) and picked <see cref="Stronger"/> on 2026-11-26:
        /// it is the <see cref="Default"/> the game, the Testbed and the sag gate push with. A share over 1 is more than the
        /// ball carried, the exaggeration a game may make; the longer hold is the same momentum shown longer.
        /// </summary>
        public readonly record struct Feel(string Name, float Share, float MaxKick, float SettleHold, float SettleRate)
        {
            /// <summary>The push of #1192, as the owner accepted it on 2026-11-24: the shot's whole momentum,
            /// <see cref="MAX_KICK"/>, a second free. <c>pushfeel=1</c>.</summary>
            public static Feel Original => new("original", 1f, MAX_KICK, SETTLE_HOLD, SETTLE_RATE);

            /// <summary>Three quarters again of the momentum, a half again of the kick, half a second longer free: the owner's
            /// pick of 2026-11-26 (#1248), the <see cref="Default"/>. <c>pushfeel=2</c>.</summary>
            public static Feel Stronger => new("stronger", 1.75f, 12f, 1.5f, SETTLE_RATE);

            /// <summary>The push every hit reads with unless a test or <c>pushfeel=</c> says otherwise (#1248).</summary>
            public static Feel Default => Stronger;

            /// <summary>Two and a half times the momentum, twice the kick, two seconds free and a gentler settle after.</summary>
            public static Feel Strongest => new("strongest", 2.5f, 12f, 1.5f, 1.8f);

            /// <summary>The feels by their number in <c>pushfeel=</c>: 1 the original, 2 stronger (the default), 3 strongest.</summary>
            public static Feel ByNumber(int number) => number switch
            {
                1 => Original,
                2 => Stronger,
                3 => Strongest,
                _ => throw new ArgumentOutOfRangeException(nameof(number), number, "pushfeel is 1, 2 or 3"),
            };
        }

        /// <summary>How long a push's swing runs free before it is settled (<see cref="Feel.SettleHold"/>; <see cref="SETTLE_HOLD"/> as shipped).</summary>
        public float SettleHold { get; set; } = SETTLE_HOLD;

        /// <summary>The settle's drag per second (<see cref="Feel.SettleRate"/>; <see cref="SETTLE_RATE"/> as shipped).</summary>
        public float SettleRate { get; set; } = SETTLE_RATE;

        /// <summary>
        /// The share of its momentum a shot of <paramref name="kind"/> hands the cluster. Every kind the gun fires today
        /// takes <see cref="MOMENTUM_SHARE"/>; a kind built to hit harder (#1033's Fire Ball, which tunnels through the
        /// map) states its own here, and <see cref="MAX_KICK"/> still bounds what any one ball takes.
        /// </summary>
        public static float ShareFor(BallKind kind) => kind switch
        {
            _ => MOMENTUM_SHARE,
        };

        /// <summary>
        /// The impulse a shot of <paramref name="kind"/> hands the cluster: its momentum, <paramref name="mass"/> times
        /// <paramref name="velocity"/>, times its kind's share, times <paramref name="scale"/> (1 as shipped; the Game's
        /// <c>shotpush=</c> testing argument).
        /// </summary>
        public static Vector3 ImpulseOf(float mass, Vector3 velocity, BallKind kind, float scale = 1f) =>
            velocity * (mass * ShareFor(kind) * scale);

        /// <summary>
        /// What one push did: how many balls still hanging it reached and how much momentum it handed them, and the same
        /// for the balls it found that had been released by then (#1249), which carry their share away with them.
        /// </summary>
        public readonly record struct Result(int Balls, float Impulse, int Released = 0, float ReleasedImpulse = 0f)
        {
            /// <summary>No push: nothing was hit, or nothing was left standing to take it.</summary>
            public static Result None => default;
        }

        //Hop count + 1 per cell, 0 meaning "not reached", as ClusterRipple keeps it: a visited mark that needs one clear a
        //push. Sized to the field and reused, so a push allocates nothing once the field is known.
        private int[,,] _hops;
        private readonly Queue<XZLevel> _queue = new();
        private readonly List<PhysicsBall> _reached = new(64);
        private readonly List<int> _reachedHops = new(64);

        //The settle: the simulation whose step it is hooked on (null when none is), the structure it drags and how long ago
        //the last push was given. The step's handler is built once, so hooking it allocates nothing.
        private Simulation _settling;
        private PhysicsBall[,,] _settleBalls;
        private float _settleAge;
        private readonly TimestepperStageHandler _settleStep;

        public ShotPush() => _settleStep = SettleStep;

        /// <summary>Whether a push's swing is being settled now (<see cref="SETTLE_SECONDS"/>).</summary>
        public bool Settling => _settling != null;

        /// <summary>
        /// Walks to <paramref name="origin"/>'s balls and hands them <paramref name="impulse"/> at once:
        /// <see cref="Prepare"/> and <see cref="Deliver"/> together, for a strike that removes nothing before its push
        /// (a refusal) or has already removed what it removes (a cutter's).
        /// </summary>
        /// <param name="balls">The live structure; null does nothing.</param>
        /// <param name="origin">The cell the shot struck: the one it landed in, or the ball it was refused at.</param>
        /// <param name="impulse">World space, see <see cref="ImpulseOf"/>.</param>
        /// <param name="reach">Hops through the sockets; <see cref="REACH"/> as shipped.</param>
        /// <param name="maxKick">The most one ball's velocity changes by; <see cref="MAX_KICK"/> as shipped.</param>
        /// <param name="hopFalloff">What each hop weighs against the one before; <see cref="HOP_FALLOFF"/> as shipped.</param>
        public Result Apply(Simulation simulation, PhysicsBall[,,] balls, XZLevel origin, Vector3 impulse,
            int reach = REACH, float maxKick = MAX_KICK, float hopFalloff = HOP_FALLOFF)
        {
            Prepare(balls, origin, reach);
            return Deliver(simulation, balls, impulse, maxKick, hopFalloff);
        }

        /// <summary>
        /// The walk, on the structure as it stands: the balls within <paramref name="reach"/> hops of
        /// <paramref name="origin"/> through balls that touch, each with its hop. The origin seeds it whether or not a ball
        /// stands in it. A landing walks here, after its body is in its cell and before the match takes anything, so the
        /// balls a released group hung from are still found (<see cref="Deliver"/> then skips what fell).
        /// </summary>
        public void Prepare(PhysicsBall[,,] balls, XZLevel origin, int reach = REACH)
        {
            _reached.Clear();
            _reachedHops.Clear();
            if (balls != null) Walk(balls, origin, reach);
        }

        /// <summary>
        /// Hands <paramref name="impulse"/> to the balls <see cref="Prepare"/> found: each its hop's share of the whole walk's
        /// weight, the ones still standing in their cells and the ones released since alike (#1249), so a released ball
        /// leaves along the flight with its share. A ball gone from the world takes nothing. Every ball pushed is woken,
        /// since a sleeping island is not integrated, and the structure is then settled (<see cref="SETTLE_SECONDS"/>) if
        /// any of it still hangs. The walk is spent by it.
        /// </summary>
        /// <param name="maxKick">The most one ball's velocity changes by; <see cref="MAX_KICK"/> as shipped.</param>
        /// <param name="hopFalloff">What each hop weighs against the one before; <see cref="HOP_FALLOFF"/> as shipped.</param>
        public Result Deliver(Simulation simulation, PhysicsBall[,,] balls, Vector3 impulse, float maxKick = MAX_KICK,
            float hopFalloff = HOP_FALLOFF)
        {
            if (balls == null || _reached.Count == 0 || impulse.LengthSquared() <= 0f)
            {
                _reached.Clear();
                _reachedHops.Clear();
                return Result.None;
            }

            float weights = 0f;
            for (int i = 0; i < _reachedHops.Count; i++) weights += MathF.Pow(hopFalloff, _reachedHops[i]);

            Vector3 delivered = Vector3.Zero, carried = Vector3.Zero;
            int pushed = 0, released = 0;

            for (int i = 0; i < _reached.Count; i++)
            {
                PhysicsBall ball = _reached[i];
                XZLevel at = ball.ArrayPosition;

                //Released since the walk, it falls with its share (#1249); one already gone from the world takes nothing
                bool hanging = ReferenceEquals(balls[at.X, at.Z, at.Level], ball);
                if (!hanging && !simulation.Bodies.BodyExists(ball.BallReference.Handle)) continue;

                BodyReference body = simulation.Bodies[ball.BallReference.Handle];
                float inverseMass = body.LocalInertia.InverseMass;
                if (inverseMass <= 0f) continue;

                Vector3 kick = impulse * (MathF.Pow(hopFalloff, _reachedHops[i]) / weights * inverseMass);

                float speed = kick.Length();
                if (speed > maxKick) kick *= maxKick / speed;

                //Woken first: a sleeping body's velocity is not integrated, and the island it is in may have slept
                simulation.Awakener.AwakenBody(body.Handle);
                body.Velocity.Linear += kick;

                if (hanging)
                {
                    delivered += kick / inverseMass;
                    pushed++;
                }
                else
                {
                    carried += kick / inverseMass;
                    released++;
                }
            }

            _reached.Clear();
            _reachedHops.Clear();

            if (pushed == 0 && released == 0) return Result.None;

            //Only what still hangs swings; a released ball is the drain's
            if (pushed > 0) BeginSettle(simulation, balls);
            return new Result(pushed, delivered.Length(), released, carried.Length());
        }

        /// <summary>
        /// The balls within <paramref name="reach"/> hops of <paramref name="origin"/> through balls that touch, each with
        /// its hop, into <see cref="_reached"/>. An empty cell stops the walk, so it goes round holes as the ripple does.
        /// </summary>
        private void Walk(PhysicsBall[,,] balls, XZLevel origin, int reach)
        {
            XZLevel size = XZLevel.FromArray(balls);

            if (_hops == null || _hops.GetLength(0) != size.X || _hops.GetLength(1) != size.Z || _hops.GetLength(2) != size.Level)
                _hops = new int[size.X, size.Z, size.Level];
            else Array.Clear(_hops);

            _queue.Clear();

            if (origin.X < 0 || origin.Z < 0 || origin.Level < 0 || origin.X >= size.X || origin.Z >= size.Z || origin.Level >= size.Level)
                return;

            _hops[origin.X, origin.Z, origin.Level] = 1;
            _queue.Enqueue(origin);

            PhysicsBall struck = balls[origin.X, origin.Z, origin.Level];
            if (struck != null)
            {
                _reached.Add(struck);
                _reachedHops.Add(0);
            }

            while (_queue.Count > 0)
            {
                XZLevel cell = _queue.Dequeue();
                int hops = _hops[cell.X, cell.Z, cell.Level] - 1;
                if (hops >= reach) continue;

                foreach (XZLevel next in BallsMap.GetNeighboringCells(cell, size))
                {
                    if (_hops[next.X, next.Z, next.Level] != 0) continue;

                    PhysicsBall ball = balls[next.X, next.Z, next.Level];
                    if (ball == null) continue;

                    _hops[next.X, next.Z, next.Level] = hops + 2;
                    _queue.Enqueue(next);

                    _reached.Add(ball);
                    _reachedHops.Add(hops + 1);
                }
            }
        }

        /// <summary>
        /// Starts (or restarts) the settle of <paramref name="balls"/> on <paramref name="simulation"/>'s step. A push given
        /// while one settles starts its hold again: each hit is seen at its own size.
        /// </summary>
        private void BeginSettle(Simulation simulation, PhysicsBall[,,] balls)
        {
            if (!ReferenceEquals(_settling, simulation))
            {
                EndSettle();
                simulation.Timestepper.BeforeCollisionDetection += _settleStep;
                _settling = simulation;
            }

            _settleBalls = balls;
            _settleAge = 0f;
        }

        private void EndSettle()
        {
            //A simulation disposed under a settle keeps its timestepper object, so unhooking from it is safe
            if (_settling?.Timestepper is ITimestepper timestepper) timestepper.BeforeCollisionDetection -= _settleStep;
            _settling = null;
            _settleBalls = null;
        }

        /// <summary>
        /// One step of the settle, on the step's own clock, before its collision detection and solve: past the hold, every
        /// awake ball of the structure keeps <c>exp(-rate·dt)</c> of its velocity, linear and angular alike, so the
        /// cluster's swing is damped as a body and its sockets are not set against each other.
        /// </summary>
        private void SettleStep(float dt, IThreadDispatcher threadDispatcher)
        {
            _settleAge += dt;

            if (_settleAge > SettleHold + SETTLE_SECONDS || _settleBalls == null)
            {
                EndSettle();
                return;
            }

            if (_settleAge <= SettleHold) return;

            float keep = MathF.Exp(-SettleRate * dt);
            Bodies bodies = _settling.Bodies;
            PhysicsBall[,,] balls = _settleBalls;

            for (int x = 0; x < balls.GetLength(0); x++)
                for (int z = 0; z < balls.GetLength(1); z++)
                    for (int level = 0; level < balls.GetLength(2); level++)
                    {
                        PhysicsBall ball = balls[x, z, level];
                        if (ball == null) continue;

                        BodyReference body = bodies[ball.BallReference.Handle];
                        if (!body.Awake) continue;

                        body.Velocity.Linear *= keep;
                        body.Velocity.Angular *= keep;
                    }
        }
    }
}
