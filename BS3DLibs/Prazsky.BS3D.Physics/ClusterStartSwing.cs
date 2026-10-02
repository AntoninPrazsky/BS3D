using BepuPhysics;
using BepuPhysics.Constraints;
using System;
using System.Collections.Generic;

namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// <b>The cluster springs from the glass when a level starts, and this is what makes it visible</b> (#617). The
    /// owner's note: <i>"I like how, when a new level starts, the map swings, as if it were springing from the
    /// ceiling before it settles — you can see we use a physics simulation. Emphasise it."</i>
    /// <para>
    /// <b>What that motion is.</b> The lattice is built a unit under where it hangs (the ceiling socket rests a
    /// diameter under the plate), so the first steps pull the whole cluster up into its seat. Measured over the
    /// shipped pack with the level's own hang (a scratch probe doing what <c>Tools/LevelGen/SagProbe</c> does): a
    /// dense block — Colossus, One — made that move in under a quarter of a second and stopped dead, because the
    /// sockets are critically damped at 15 Hz and a dense lattice is effectively one rigid body; a strand —
    /// Pendant, Crane — went on swaying by its own weight for seconds. So what the owner saw was mostly the
    /// strands, and a snap on everything else.
    /// </para>
    /// <para>
    /// <b>The lever is the sockets to the glass, and only those.</b> Softening every socket was tried first and
    /// is wrong: a strand is a chain of them in series, and at 4 Hz Pendant and Amphora sagged eight to thirteen
    /// units, far through the line, while Colossus barely moved. Under-damping them alone at 15 Hz changed nothing
    /// measurable on a dense block. The top row's sockets carry the whole cluster, so softening just them is a
    /// bungee on the block — the lattice stays as stiff as it is and the cluster rises, overshoots and settles on
    /// them. <b>Their frequency is solved per level from the load each carries</b>, because that runs tenfold
    /// across the pack (Colossus about 2.5 balls a socket, Amphora 25, Saturn 42): a spring of angular frequency
    /// ω stretches g·L/ω² under L balls, so asking every level for the same <see cref="TARGET_STRETCH"/> gives
    /// every level a swing of the same size rather than a wobble on one and a collapse on another.
    /// </para>
    /// <para>
    /// <b>Measured, all 130 levels, the first four seconds</b>: no level dips under the line (none did before);
    /// the tightest start clearance goes from 1.71 above the line to 1.20 (Umbrella); the cluster's mean distance
    /// from its rest pose two seconds in, median over the pack, goes from 0.02 to 0.07 — a dense block now
    /// rebounds once or twice and settles by about 1.5 s where it stopped at 0.25 s. The resting pose is the same
    /// by construction: the sockets are handed back their own <see cref="BallsConstraintsBuilder.SPRING_SETTINGS"/>.
    /// </para>
    /// <para>
    /// <b>A pure function of simulated time</b>, advanced by <see cref="PhysicsWorld.Step"/> on the fixed step, so
    /// it is the same motion at any refresh rate; and <b>in the world rather than in the Game</b>, so every
    /// caller that hangs a level hangs it the same way — the Game, the Testbed and the sag gate, which would
    /// otherwise be measuring a start the game no longer has (#301/#302). The Game begins it when it lets the cluster
    /// go, which on a chapter's first level waits for the establishing tour to hand the lens back, the world held
    /// unstepped and the cluster as built until then (#690) — so it is seen on every start and every Retry.
    /// </para>
    /// </summary>
    public sealed class ClusterStartSwing
    {
        /// <summary>
        /// How far the glass's sockets stretch under their load while soft, in world units — what sizes their
        /// frequency per level. 0.4 was tried too: bigger, and Amphora swung 3.2 units; this is the gentler one.
        /// </summary>
        public const float TARGET_STRETCH = 0.25f;

        /// <summary>The soft sockets' damping ratio: well under critical, so the block rebounds instead of stopping.</summary>
        public const float DAMPING_RATIO = 0.2f;

        /// <summary>How long the sockets stay soft before they start to stiffen.</summary>
        public const float HOLD_SECONDS = 0.8f;

        /// <summary>
        /// How long they take to stiffen back to <see cref="BallsConstraintsBuilder.SPRING_SETTINGS"/> — an ease and
        /// not a switch, since a sudden return to 15 Hz while the block is still moving would snap it to rest.
        /// </summary>
        public const float EASE_SECONDS = 1.0f;

        private readonly ConstraintHandle[] _sockets;
        private readonly float _softFrequency;
        private float _clock;

        /// <summary>Whether the sockets have been handed back their own settings.</summary>
        public bool Finished { get; private set; }

        /// <summary>The frequency the glass's sockets were softened to, for the <c>[swing]</c> line.</summary>
        public float SoftFrequency => _softFrequency;

        /// <summary>How many sockets hold the cluster to the glass.</summary>
        public int SocketCount => _sockets.Length;

        //The top-level ball each socket belongs to, beside it: what Apply checks a handle against (see there)
        private readonly BodyHandle[] _owners;

        private ClusterStartSwing(ConstraintHandle[] sockets, BodyHandle[] owners, float softFrequency)
        {
            _sockets = sockets;
            _owners = owners;
            _softFrequency = softFrequency;
        }

        /// <summary>
        /// Softens the sockets a freshly built cluster hangs from the glass by — the top level's
        /// <see cref="PhysicsBall.HandlesTop"/>, which on that level hold nothing else. Null when there is nothing
        /// to soften (an empty field, or one that hangs from nothing).
        /// </summary>
        internal static ClusterStartSwing Begin(Simulation simulation, PhysicsBall[,,] balls)
        {
            List<ConstraintHandle> sockets = new();
            List<BodyHandle> owners = new();
            int top = balls.GetLength(2) - 1;
            float mass = 0f;

            for (int x = 0; x < balls.GetLength(0); x++)
                for (int z = 0; z < balls.GetLength(1); z++)
                {
                    if (top >= 0 && balls[x, z, top] != null)
                    {
                        int before = sockets.Count;
                        balls[x, z, top].HandlesTop.CollectStored(sockets);
                        for (int i = before; i < sockets.Count; i++) owners.Add(balls[x, z, top].BallReference.Handle);
                    }

                    for (int l = 0; l <= top; l++)
                    {
                        PhysicsBall ball = balls[x, z, l];
                        if (ball == null) continue;

                        //By mass and not by count: a heavy ball (#333) is twelve of the others on the glass
                        float inverse = ball.BallReference.LocalInertia.InverseMass;
                        if (inverse > 0f) mass += 1f / inverse;
                    }
                }

            if (sockets.Count == 0 || mass <= 0f) return null;

            float load = mass / BallsConstraintsBuilder.BALL_MASS / sockets.Count;
            float omega = MathF.Sqrt(MathF.Abs(Prazsky.Core.Tools.Constants.EARTH_GRAVITY) * load / TARGET_STRETCH);
            float frequency = MathF.Min(BallsConstraintsBuilder.SPRING_SETTINGS.Frequency, omega / (2f * MathF.PI));

            ClusterStartSwing swing = new(sockets.ToArray(), owners.ToArray(), frequency);
            swing.Apply(simulation, frequency, DAMPING_RATIO);
            return swing;
        }

        /// <summary>
        /// One step of the ease: nothing while the sockets hold soft, then their frequency climbs geometrically
        /// and their damping linearly back to the builder's own, smoothstepped over <see cref="EASE_SECONDS"/>.
        /// </summary>
        internal void Advance(Simulation simulation, float dt)
        {
            if (Finished) return;

            _clock += dt;
            if (_clock < HOLD_SECONDS) return;

            float k = Math.Clamp((_clock - HOLD_SECONDS) / EASE_SECONDS, 0f, 1f);
            k = k * k * (3f - 2f * k);

            SpringSettings rest = BallsConstraintsBuilder.SPRING_SETTINGS;
            float frequency = _softFrequency * MathF.Pow(rest.Frequency / _softFrequency, k);
            float damping = DAMPING_RATIO + (rest.DampingRatio - DAMPING_RATIO) * k;

            if (k >= 1f)
            {
                Apply(simulation, rest.Frequency, rest.DampingRatio);
                Finished = true;
                return;
            }

            Apply(simulation, frequency, damping);
        }

        /// <summary>
        /// Re-describes each socket in place with new spring settings, its anchors untouched. A socket a shot has
        /// already cut in the first two seconds is skipped — its handle may even have been reused by a new one,
        /// which is why the type is checked and not only the existence; a ball socket reused that way is simply
        /// softened for the rest of the ease with everything else, and handed back the same settings.
        /// </summary>
        /// <summary>Whether <paramref name="owner"/> still exists and still takes part in <paramref name="socket"/>.</summary>
        private static bool Owns(Simulation simulation, BodyHandle owner, ConstraintHandle socket)
        {
            if (!simulation.Bodies.BodyExists(owner)) return false;

            ref var constraints = ref simulation.Bodies[owner].Constraints;
            for (int i = 0; i < constraints.Count; i++)
                if (constraints[i].ConnectingConstraintHandle.Value == socket.Value) return true;

            return false;
        }

        private void Apply(Simulation simulation, float frequency, float damping)
        {
            SpringSettings settings = new(frequency, damping);
            Solver solver = simulation.Solver;

            for (int i = 0; i < _sockets.Length; i++)
            {
                ConstraintHandle handle = _sockets[i];
                if (!solver.ConstraintExists(handle)) continue;
                if (solver.HandleToConstraint[handle.Value].TypeId != BallSocket.ConstraintTypeId) continue;

                //⚠ AND STILL THIS BALL'S (#690's review). Bepu hands a freed handle to the next constraint made, so a match in
                //the swing's two seconds that releases a top-level ball can put the next landing's ball-to-ball socket on its
                //anchor's handle - and this would soften that socket and then hand it the builder's spring, a stiff knot in a
                //lattice LatticeSoftness had softened. Harmless while every socket was the builder's; not since #690.
                if (!Owns(simulation, _owners[i], handle)) continue;

                solver.GetDescription(handle, out BallSocket socket);
                socket.SpringSettings = settings;
                solver.ApplyDescription(handle, socket);
            }
        }
    }
}
