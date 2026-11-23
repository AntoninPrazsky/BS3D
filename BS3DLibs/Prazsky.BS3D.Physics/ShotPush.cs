using BepuPhysics;
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
    /// push is the same whatever the frame rate and whatever step the caller takes (the Testbed's varies). It is given
    /// <b>after</b> the landing is decided, the body put in its cell and its sockets built, and after the match and the
    /// three removals have taken what they take: the cell the shot landed in was chosen before anything moved, the sockets
    /// start with no error, and only balls still hanging are pushed. What a shot knocks out falls as it always fell.
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
    /// <b>One copy, called wherever a shot strikes the structure</b>: the contact handler's landing, its first refusal of
    /// a shot and a cutter's strike, and <c>Tools/LevelGen/SagProbe</c>'s landing, which repeats the handler step for step
    /// so the sag gate measures the game played. Its walk's scratch is held per instance, as
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

        /// <summary>What one push did: how many balls it reached and how much momentum it handed them in all.</summary>
        public readonly record struct Result(int Balls, float Impulse)
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

        /// <summary>
        /// Hands <paramref name="impulse"/> to the balls round <paramref name="origin"/>. The origin seeds the walk
        /// whether or not a ball stands in it: a shot that completed a group left with it, and the push is then the
        /// neighbours' alone. Every ball reached is woken, since a sleeping island is not integrated.
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
            if (balls == null || impulse.LengthSquared() <= 0f || !Walk(balls, origin, reach)) return Result.None;

            float weights = 0f;
            for (int i = 0; i < _reachedHops.Count; i++) weights += MathF.Pow(hopFalloff, _reachedHops[i]);

            Vector3 delivered = Vector3.Zero;

            for (int i = 0; i < _reached.Count; i++)
            {
                BodyReference body = simulation.Bodies[_reached[i].BallReference.Handle];
                float inverseMass = body.LocalInertia.InverseMass;
                if (inverseMass <= 0f) continue;

                Vector3 kick = impulse * (MathF.Pow(hopFalloff, _reachedHops[i]) / weights * inverseMass);

                float speed = kick.Length();
                if (speed > maxKick) kick *= maxKick / speed;

                //Woken first: a sleeping body's velocity is not integrated, and the island it is in may have slept
                simulation.Awakener.AwakenBody(body.Handle);
                body.Velocity.Linear += kick;

                delivered += kick / inverseMass;
            }

            return new Result(_reached.Count, delivered.Length());
        }

        /// <summary>
        /// The balls within <paramref name="reach"/> hops of <paramref name="origin"/> through balls that touch, each with
        /// its hop, into <see cref="_reached"/>. An empty cell stops the walk, so it goes round holes as the ripple does.
        /// </summary>
        private bool Walk(PhysicsBall[,,] balls, XZLevel origin, int reach)
        {
            XZLevel size = XZLevel.FromArray(balls);

            if (_hops == null || _hops.GetLength(0) != size.X || _hops.GetLength(1) != size.Z || _hops.GetLength(2) != size.Level)
                _hops = new int[size.X, size.Z, size.Level];
            else Array.Clear(_hops);

            _queue.Clear();
            _reached.Clear();
            _reachedHops.Clear();

            if (origin.X < 0 || origin.Z < 0 || origin.Level < 0 || origin.X >= size.X || origin.Z >= size.Z || origin.Level >= size.Level)
                return false;

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

            return _reached.Count > 0;
        }
    }
}
