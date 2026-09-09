using System;
using System.Collections.Generic;
using System.Numerics;
using BepuPhysics;
using Prazsky.BS3D.GameStructure;

namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// Where the <see cref="BallKind.Gravity"/> balls are this frame and what they pull with (#332) — the one
    /// snapshot both the simulation and the aim preview read.
    /// <para>
    /// <b>⚠ ONE SNAPSHOT READ BY BOTH IS THE WHOLE POINT OF THIS TYPE, and it is not tidiness.</b>
    /// <c>ShotPlacement</c> exists so the aim ghost and the attach cannot disagree about where a shot lands.
    /// The simulation bends a shot by applying this field once per step; the preview bends its integration by
    /// the same field every frame. If those two read the wells at different <i>instants</i> — the preview off
    /// live body poses while the step used last frame's, say — the ghost and the attach drift apart by however
    /// far the cluster swayed in between, and the class's one invariant is gone with a curve on it. So the
    /// wells are collected <b>once a frame, before either</b>, and both read this.
    /// </para>
    /// <para>
    /// <b>The field is BOUNDED and that is three decisions at once.</b> An inverse-square well has no edge, is
    /// singular at its centre, and would fling a shot that grazes it at a speed nothing else in this game
    /// moves at. This one is a smooth kernel that reaches exactly zero — value <i>and</i> slope — at
    /// <see cref="Range"/>: so the trajectory is <b>straight outside every well</b>, which is what lets the
    /// preview integrate in a handful of segments instead of sixty; the pull is finite everywhere, so a shot
    /// through the centre is deflected rather than launched; and a level author can see how far a well reaches
    /// because it is a distance rather than an asymptote.
    /// </para>
    /// <para>
    /// <b>Shaped for #95 rather than as a private hack in the contact path</b>, which that issue's own note
    /// asks for: what this is, is a per-body force field sampled at a point, refreshed per frame and applied
    /// inside one step. Wind and per-scene gravity are the same shape with a different
    /// <see cref="Acceleration"/>, and the one thing they would have to keep is where in the step it is
    /// applied — see <see cref="PhysicsWorld.PerStepForces"/>.
    /// </para>
    /// </summary>
    public sealed class GravityWells
    {
        /// <summary>
        /// How hard a well pulls at its own centre, in world units per second squared.
        /// <para>
        /// <b>It is a large number and it has to be, which is worth arithmetic rather than taste.</b> A shot
        /// leaves at 200 u/s and crosses the arena in about an eighth of a second, so the time it spends inside
        /// one well of <see cref="Range"/> is roughly <c>2 × 4 / 200 = 0.04 s</c>. Bending it by half a cell in
        /// that time needs <c>a ≈ 2 × 0.5 / 0.04² = 625</c> u/s². Earth's 9.81 would move the shot by four
        /// <i>thousandths</i> of a cell — which is also why the straight-line preview was honest before this
        /// kind existed, and why nothing about it had to change for world gravity.
        /// </para>
        /// <para>
        /// So the curve is a <b>kink</b> rather than a graceful arc, and that is the honest consequence of a
        /// fast shot rather than a fault: the ball flies straight, swerves as it passes the well, and flies
        /// straight on. It reads as being yanked, which is what a gravity well should look like at this speed.
        /// </para>
        /// </summary>
        /// <remarks>
        /// <b>MEASURED, not chosen.</b> The first value here was 900, from the arithmetic above, and it bends a
        /// shot by <b>a third of a cell</b> — which is a well the player never has to aim around, and no amount
        /// of look makes that a mechanic. The estimate was right about the order and wrong about what matters:
        /// what the player sees is not the deflection at the well but the deflection <i>where the shot lands</i>,
        /// and after passing a well hung under a cluster there are only four or five levels of flight left for
        /// the sideways velocity to turn into sideways distance.
        /// <para>
        /// Deflection is linear in this number, so it was solved rather than swept, and then re-measured at
        /// the value chosen. In free flight, read <b>eight world units past the well</b> — about what a shot
        /// has left after passing one hung under the cluster it is aimed at — a shot passing 1.5 / 2.0 / 2.5 /
        /// 3.0 / 3.5 units out is thrown <b>1.62 / 1.33 / 0.86 / 0.38 / 0.08</b> cells sideways. That is a
        /// gradient the player can read and aim along rather than a wall: threading close costs a cell and a
        /// half, and the rim of the field is barely a nudge.
        /// </para>
        /// <para>
        /// ⚠ <b>How much of that reaches the LANDING is the level's business, not this constant's.</b> The
        /// sideways velocity is gained crossing the field and then turns into distance over whatever flight is
        /// left, so a well hung directly under the slab it guards bends a shot by a fraction of what the same
        /// well bends when it hangs clear of one. That is why the generator refuses a <i>buried</i> well and
        /// why the test map hangs its two on stalks.
        /// </para>
        /// </remarks>
        public const float STRENGTH = 3600f;

        /// <summary>
        /// How far a well reaches, in world units — four, which is four cells sideways and about five and a
        /// half levels up or down. Big enough that a shot has to be aimed <i>around</i> it and not merely past
        /// it, small enough that a field with several wells still has straight corridors between them.
        /// </summary>
        public const float RANGE = 4f;

        private static readonly float RANGE_SQUARED = RANGE * RANGE;

        //Position and nothing else: a well's pull does not depend on its colour, its cell or which ball it is.
        //A list rather than an array because the count changes as wells are shot out, and it is reused across
        //frames so a settled level allocates nothing.
        private readonly List<Vector3> _wells = new(8);

        /// <summary>How many wells are standing. Zero on every level shipped today, which is the fast path
        /// every caller takes first.</summary>
        public int Count => _wells.Count;

        /// <summary>
        /// Re-reads the wells off the live structure. Call it <b>once a frame, before the aim preview and
        /// before the frame's steps</b> — see the class remarks for why that is not a suggestion.
        /// <para>
        /// It walks the whole ball array, which is the same walk the draw's own collector makes, and it makes
        /// it <b>once a frame rather than once a step</b>: at 120 Hz against 60 frames that would be twice the
        /// work for an answer that cannot have changed by more than the cluster's sway inside one frame.
        /// </para>
        /// <para>
        /// Positions come off the <b>live body poses</b> and not off the lattice, because a hanging cluster
        /// sways and a well the size of four cells is aimed around by a player watching where it actually is.
        /// </para>
        /// </summary>
        public void Refresh(PhysicsBall[,,] balls)
        {
            _wells.Clear();

            if (balls == null) return;

            int sizeX = balls.GetLength(0), sizeZ = balls.GetLength(1), sizeLevel = balls.GetLength(2);

            for (int level = 0; level < sizeLevel; level++)
                for (int x = 0; x < sizeX; x++)
                    for (int z = 0; z < sizeZ; z++)
                    {
                        PhysicsBall ball = balls[x, z, level];
                        if (ball == null || ball.Kind != BallKind.Gravity) continue;

                        _wells.Add(ball.BallReference.Pose.Position);
                    }
        }

        /// <summary>Empties the snapshot — a level teardown, or a caller that has no structure yet.</summary>
        public void Clear() => _wells.Clear();

        /// <summary>
        /// Applies the field to every ball <b>in flight</b> for one step of <paramref name="dt"/>. Hand this to
        /// <see cref="PhysicsWorld.PerStepForces"/> and nowhere else — see that property for why the position
        /// in the step is the part that matters.
        /// <para>
        /// <b>Balls in flight and nothing else</b> (#332's own recommendation, taken): not the falling debris,
        /// and not the hanging cluster the well is embedded in. A well that visibly bent the lattice around
        /// itself is a different and much larger feature and belongs to the mass ball, not here. It also bounds
        /// the cost to nothing worth measuring — a handful of shots against a handful of wells, where the
        /// alternative is every body in the simulation.
        /// </para>
        /// <para>
        /// It writes the body's <b>velocity</b> rather than applying an impulse, because that is what the aim
        /// preview can reproduce exactly: <c>v += a·dt</c> here and the same line in
        /// <see cref="ShotPlacement.TryFindFirstHitCurved"/>. An impulse divided by a mass is the same
        /// arithmetic with one more constant in it and one more place for the two to drift.
        /// </para>
        /// <para>
        /// A shot is woken by being fired and never sleeps while it flies, so nothing here has to wake it; a
        /// body whose reference has gone (retired mid-frame) is skipped rather than guarded against, which is
        /// the same check every other walk over this list makes.
        /// </para>
        /// </summary>
        public void ApplyTo(List<PhysicsBall> shotBalls, float dt)
        {
            if (_wells.Count == 0 || shotBalls == null) return;

            for (int i = 0; i < shotBalls.Count; i++)
            {
                PhysicsBall ball = shotBalls[i];
                if (ball == null || !ball.BallReference.Exists) continue;

                BodyReference body = ball.BallReference;
                body.Velocity.Linear += Acceleration(body.Pose.Position) * dt;
            }
        }

        /// <summary>
        /// What the field accelerates a body at <paramref name="point"/> by, summed over every well in range.
        /// Zero outside all of them, exactly — see the class remarks on why the kernel has an edge.
        /// </summary>
        /// <remarks>
        /// The kernel is <c>(1 − t²)²</c> over <c>t = d / RANGE</c>: one at the centre, zero at the rim with a
        /// zero slope there too, so a shot crossing the boundary feels no step in its acceleration and the
        /// integration has nothing to resolve at the edge. It is finite at the centre, which an inverse square
        /// is not, and that is what makes a shot through the middle of a well a deflection rather than a
        /// launch.
        /// </remarks>
        public Vector3 Acceleration(Vector3 point)
        {
            if (_wells.Count == 0) return Vector3.Zero;

            Vector3 total = Vector3.Zero;

            for (int i = 0; i < _wells.Count; i++)
            {
                Vector3 toWell = _wells[i] - point;
                float distanceSquared = toWell.LengthSquared();

                if (distanceSquared >= RANGE_SQUARED) continue;

                //A body sitting exactly on a well's centre has no direction to be pulled in, and the kernel is
                //at its maximum there anyway. Answering zero is the only continuous thing to do.
                float distance = MathF.Sqrt(distanceSquared);
                if (distance < 1e-4f) continue;

                float falloff = 1f - distanceSquared / RANGE_SQUARED;

                total += toWell * (STRENGTH * falloff * falloff / distance);
            }

            return total;
        }

        /// <summary>
        /// How far along <paramref name="direction"/> from <paramref name="origin"/> a body would first come
        /// within reach of any well, or <see cref="float.MaxValue"/> when the ray meets none — the one thing
        /// that lets the preview stay cheap.
        /// <para>
        /// <b>Outside every well the trajectory is exactly straight</b>, so the preview does not integrate
        /// there: it asks this, jumps the whole straight run in one segment, and only steps small inside the
        /// field. A shot that passes one well costs a couple of long segments and about five short ones rather
        /// than sixty short ones, which is the difference between an aim path that can afford this every frame
        /// and one that cannot.
        /// </para>
        /// </summary>
        /// <param name="direction">Unit.</param>
        public float DistanceToField(Vector3 origin, Vector3 direction)
        {
            float nearest = float.MaxValue;

            for (int i = 0; i < _wells.Count; i++)
            {
                //Ray against the well's sphere of influence, in closed form — ShotPlacement's own arithmetic
                //with the radius being the field's reach rather than two balls' surfaces.
                Vector3 toWell = _wells[i] - origin;
                float along = Vector3.Dot(toWell, direction);
                float perpendicularSquared = toWell.LengthSquared() - along * along;

                if (perpendicularSquared >= RANGE_SQUARED) continue;

                float halfChord = MathF.Sqrt(RANGE_SQUARED - perpendicularSquared);
                float entry = along - halfChord;

                //Already inside this one: the field starts here.
                if (entry <= 0f && along + halfChord > 0f) return 0f;

                if (entry > 0f && entry < nearest) nearest = entry;
            }

            return nearest;
        }
    }
}
