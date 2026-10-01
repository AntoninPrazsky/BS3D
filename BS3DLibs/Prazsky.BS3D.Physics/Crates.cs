using BepuPhysics;
using BepuPhysics.Collidables;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// <b>The crates</b> (#257): boxes standing in the play space that a shot banks off like a mirror. The point of a crate
    /// is its flat faces — a ball's bounce off another ball is a curve nobody can read, a bounce off a wall is one anybody
    /// can — so a level can put the gap the player needs round a corner and hand them the wall to reach it by.
    /// <para>
    /// <b>A shot's bounce is solved here, not by Bepu, and that is the whole design.</b> Bepu has no restitution (its
    /// contact is a spring that loses what it likes), and the landing preview has to draw the bank shot before it is
    /// fired: so the bounce is a <b>reflection</b>, exact and lossless, computed by the one function both sides call —
    /// <see cref="TryFindFirstFace"/> and <see cref="Reflect"/>, by <see cref="ShotPlacement"/> for the ghost and by
    /// <see cref="BounceShots"/> for the ball. The narrow phase never pairs a shot in flight with a crate
    /// (<see cref="ContactEvents.IsCrate"/>), so Bepu cannot add a second, different answer. Everything else — a ball
    /// released from the cluster, a spent shot — meets the crate as the static box it also is.
    /// </para>
    /// <para>
    /// A ball is a sphere and a crate a box, and their touch is the box grown by the ball's radius — taken here with
    /// square edges rather than the rounded ones the exact sum has, so a ball grazing a crate's corner turns a few tenths
    /// of a unit early. That is the readable choice as well as the cheap one: every bounce is off one of six planes.
    /// </para>
    /// </summary>
    public sealed class Crates
    {
        /// <summary>One crate: its centre and its half size, world units, axis-aligned.</summary>
        public readonly struct Crate
        {
            public readonly Vector3 Centre;
            public readonly Vector3 HalfSize;

            public Crate(Vector3 centre, Vector3 halfSize)
            {
                Centre = centre;
                HalfSize = halfSize;
            }
        }

        /// <summary>
        /// The most faces one straight leg of flight is followed off. A shot between two crates could otherwise bounce
        /// between them for ever inside one step; four is more than any layout a player could read.
        /// </summary>
        public const int MAX_BOUNCES = 4;

        //A face a ball has just left is not met again: a leg starting on a face, moving away, finds its exit at zero
        private const float EPSILON = 1e-4f;

        private readonly List<Crate> _crates = new();

        public int Count => _crates.Count;

        public Crate this[int index] => _crates[index];

        public void Clear() => _crates.Clear();

        public void Add(Crate crate) => _crates.Add(crate);

        /// <summary>
        /// The first crate face a ball of <paramref name="radius"/> meets flying from <paramref name="origin"/> along the
        /// unit <paramref name="heading"/> within <paramref name="maxDistance"/>: how far along, and the face's outward
        /// normal. A ball starting inside a crate meets nothing — it can only be there by being put there, and pushing it
        /// out is not a bounce.
        /// </summary>
        public bool TryFindFirstFace(Vector3 origin, Vector3 heading, float maxDistance, float radius,
            out float distance, out Vector3 normal)
        {
            distance = maxDistance;
            normal = Vector3.Zero;
            bool found = false;

            for (int i = 0; i < _crates.Count; i++)
            {
                Crate crate = _crates[i];
                Vector3 min = crate.Centre - crate.HalfSize - new Vector3(radius);
                Vector3 max = crate.Centre + crate.HalfSize + new Vector3(radius);

                float enter = float.NegativeInfinity, leave = float.PositiveInfinity;
                int enterAxis = -1;
                bool miss = false;

                for (int axis = 0; axis < 3 && !miss; axis++)
                {
                    float o = Component(origin, axis), d = Component(heading, axis);
                    float lo = Component(min, axis), hi = Component(max, axis);

                    if (MathF.Abs(d) < 1e-9f)
                    {
                        //Parallel to this pair of faces: inside the slab or never in the box
                        if (o < lo || o > hi) miss = true;
                        continue;
                    }

                    float t1 = (lo - o) / d, t2 = (hi - o) / d;
                    if (t1 > t2) (t1, t2) = (t2, t1);

                    if (t1 > enter)
                    {
                        enter = t1;
                        enterAxis = axis;
                    }

                    if (t2 < leave) leave = t2;
                    if (enter > leave) miss = true;
                }

                //Behind, already inside (entered before the start), or beyond the reach of this leg
                if (miss || enterAxis < 0 || leave <= EPSILON || enter < 0f || enter >= distance) continue;

                distance = enter;
                normal = Vector3.Zero;
                SetComponent(ref normal, enterAxis, Component(heading, enterAxis) > 0f ? -1f : 1f);
                found = true;
            }

            return found;
        }

        /// <summary>A velocity mirrored in a face: its part along the normal reversed, the rest kept. Lossless.</summary>
        public static Vector3 Reflect(Vector3 velocity, Vector3 normal) => velocity - 2f * Vector3.Dot(velocity, normal) * normal;

        /// <summary>
        /// Flies a ball straight for <paramref name="time"/> seconds, bouncing off every face it meets on the way (up to
        /// <see cref="MAX_BOUNCES"/>); <paramref name="position"/> and <paramref name="velocity"/> become where it ends
        /// and how it is then moving. Returns how many faces it met. No force acts on the way, which is what the
        /// simulation does inside one step: the step's forces have been applied before it, and the pose integrates the
        /// velocity they left.
        /// </summary>
        public int Fly(ref Vector3 position, ref Vector3 velocity, float time, float radius)
        {
            int bounces = 0;
            float remaining = time;

            while (remaining > 0f && bounces < MAX_BOUNCES)
            {
                float speed = velocity.Length();
                if (speed < 1e-6f) break;

                Vector3 heading = velocity / speed;
                float reach = speed * remaining;

                if (!TryFindFirstFace(position, heading, reach, radius, out float distance, out Vector3 normal))
                {
                    position += heading * reach;
                    return bounces;
                }

                position += heading * distance;
                velocity = Reflect(velocity, normal);
                remaining -= distance / speed;
                bounces++;
            }

            if (remaining > 0f) position += velocity * remaining;
            return bounces;
        }

        /// <summary>
        /// The simulation's half (#257): every shot still in flight that would meet a crate inside the coming step is
        /// bounced off it — the step's whole flight followed off the crates by <see cref="Fly"/>, and the ball put where
        /// that leaves it <i>less</i> one step of its new velocity, so the integrator's own <c>p += v·dt</c> lands it at
        /// the end of the bounced flight exactly. Call it from <see cref="PhysicsWorld.PerStepForces"/>, after any force
        /// on the shots: the landing preview applies the step's forces first and then flies the step, in that order.
        /// The world's <paramref name="gravityY"/> is folded in the same way: the integrator adds it to the velocity
        /// during the step, so the flight bounced here is the one it will integrate, and the velocity handed back has it
        /// taken off again for the integrator to add.
        /// <para>
        /// Only shots still listening: they are the ones the narrow phase keeps off the crates, so they are the ones that
        /// would otherwise fly through. The pose the frame interpolates from was taken before the step, so a bounced
        /// ball is drawn cutting the corner of one step — a sixtieth of a second, at a speed the eye sees as a streak.
        /// </para>
        /// </summary>
        public void BounceShots(List<PhysicsBall> shots, ContactEvents events, float dt, float gravityY)
        {
            if (_crates.Count == 0 || shots == null) return;

            float radius = BallsConstraintsBuilder.BALL_RADIUS;

            for (int i = 0; i < shots.Count; i++)
            {
                PhysicsBall ball = shots[i];
                if (ball == null || !ball.BallReference.Exists) continue;

                BodyReference body = ball.BallReference;
                if (!body.Awake || !events.IsListener(body.CollidableReference)) continue;

                Vector3 gravity = new(0f, gravityY * dt, 0f);
                Vector3 position = body.Pose.Position;
                Vector3 velocity = body.Velocity.Linear + gravity;

                if (Fly(ref position, ref velocity, dt, radius) == 0) continue;

                body.Pose.Position = position - velocity * dt;
                body.Velocity.Linear = velocity - gravity;
            }
        }

        /// <summary>
        /// Stands every crate in <paramref name="simulation"/> as a static box and marks it a crate, so the narrow phase
        /// keeps shots in flight off it (<see cref="ContactEvents.MarkCrate"/>). Once, when the level's world is built.
        /// </summary>
        public void AddStatics(Simulation simulation, ContactEvents events)
        {
            for (int i = 0; i < _crates.Count; i++)
            {
                Crate crate = _crates[i];
                TypedIndex shape = simulation.Shapes.Add(new Box(crate.HalfSize.X * 2f, crate.HalfSize.Y * 2f, crate.HalfSize.Z * 2f));
                StaticHandle handle = simulation.Statics.Add(new StaticDescription(crate.Centre, shape));
                events.MarkCrate(handle);
            }
        }

        private static float Component(Vector3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;

        private static void SetComponent(ref Vector3 v, int axis, float value)
        {
            if (axis == 0) v.X = value;
            else if (axis == 1) v.Y = value;
            else v.Z = value;
        }
    }
}
