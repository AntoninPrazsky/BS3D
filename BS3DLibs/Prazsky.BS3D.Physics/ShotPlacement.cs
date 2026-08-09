using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// <b>Where a shot lands</b> — the one answer, asked twice. The contact handler asks it when a ball actually
    /// touches the cluster and attaches with the cell it gets back; the aim preview asks it every frame from the
    /// barrel's own line and draws a ghost in that cell.
    /// <para>
    /// That they are one function is the whole point of the type existing, and it is a correctness requirement
    /// rather than tidiness. #70 was opened because shots landed somewhere other than where they were aimed, and it
    /// records the trap in so many words: a line drawn over behaviour it does not share would disagree with the
    /// outcome and make the frustration <i>worse</i> than showing nothing. A preview that is a second
    /// implementation of the rule is a preview that lies, eventually.
    /// </para>
    /// </summary>
    public static class ShotPlacement
    {
        /// <summary>
        /// Which cell a shot that touched <paramref name="hitBall"/> at <paramref name="worldContact"/> lands in,
        /// or <c>false</c> when it lands nowhere at all and the shot does not stick.
        /// </summary>
        /// <remarks>
        /// <b>The lattice frame is not a fixed offset from the world</b>, and treating it as one was the second
        /// cause found in #70. The cluster is a soft <c>BallSocket</c> network rather than a rigid body: the ceiling
        /// constraint anchors a ball's top to the plate's underside, which holds the top level a full unit above its
        /// lattice position, and that hangs off towards zero further down — measured on the structure balls
        /// actually hit, +1.10 / +1.03 / +1.02 / +1.00 high in the cluster against −0.04 lower down. It also sways
        /// whenever the cluster is struck. So a contact converted by <paramref name="worldOffset"/> alone is
        /// compared against ideal cells it can be more than a level away from, and no constant could correct it.
        /// <para>
        /// The frame is only true <i>at</i> the ball that was hit, so that is where the contact is anchored: the
        /// drift is measured on that one ball and taken out of the contact. Every candidate cell is one of its
        /// neighbours, so its drift is the right local estimate for all of them, and taking the whole vector rather
        /// than just its Y takes out the sway with it.
        /// </para>
        /// <para>
        /// <b><paramref name="worldOffset"/> cancels out of the decision entirely</b>, and that is worth knowing
        /// rather than tidying away: substitute the drift and the anchored contact reduces to
        /// <c>worldContact − hitBallPose + idealHit</c>. So the cell this answers was already immune to anything
        /// that moves the whole cluster — the descending glass ceiling included, which drags the structure a
        /// <c>CEILING_DESCENT_PER_STEP</c> further off its lattice with every step. What was <i>not</i> immune was
        /// the world position drawn at that cell, which is why the drift is now an output as well: see
        /// <see cref="CellWorldPosition"/>.
        /// </para>
        /// </remarks>
        /// <param name="map">The field. <b>Read only</b> — this decides, it does not place.</param>
        /// <param name="hitBall">The structure ball the shot touched, live pose and all.</param>
        /// <param name="worldContact">Where the touch happened, in world space.</param>
        /// <param name="worldOffset">The lattice-to-world offset the cluster was built with.</param>
        /// <param name="cell">The cell, <b>only meaningful when this returns <c>true</c></b>.</param>
        /// <param name="clusterDrift">
        /// How far the cluster hangs off its lattice <i>here, right now</i> — measured on the ball that was hit
        /// and therefore the local truth for every cell around it. Handed back rather than kept private because
        /// it is the only thing that turns the answered cell into a world position: see
        /// <see cref="CellWorldPosition"/>, which is what the ghost, the arrival glide and the landing report
        /// are all placed by. Zero when this returns <c>false</c>.
        /// </param>
        public static bool TrySolveAgainstBall(BallsMap map, PhysicsBall hitBall, Vector3 worldContact,
            Vector3 worldOffset, out XZLevel cell, out Vector3 clusterDrift)
        {
            cell = new XZLevel(-1, -1, -1);
            clusterDrift = Vector3.Zero;

            if (map == null || hitBall == null) return false;

            Vector3 drift = hitBall.BallReference.Pose.Position.ToXna()
                - (map.GetRealCenteredPosition(hitBall.ArrayPosition) + worldOffset);

            Vector3 anchoredContact = worldContact - worldOffset - drift;

            //The first ring, then one ring further out. Both rings full means the shot does not stick, and that
            //is the answer rather than a failure - see TryFindEmptyCellInSecondRing on why it is not three rings.
            if (!map.TryFindEmptyCellNextTo(anchoredContact, hitBall.ArrayPosition, out cell)
                && !map.TryFindEmptyCellInSecondRing(anchoredContact, hitBall.ArrayPosition, out cell))
                return false;

            //Published only once there is a cell for it to place, so the two out parameters carry the same
            //contract as each other and as TrySolveAgainstCeiling's: on a refusal neither means anything. The
            //drift itself is a property of the ball that was hit and would be perfectly well defined here, which
            //is exactly why it is withheld - a caller reading it past a false return would be placing a ghost at
            //a cell of (-1, -1, -1).
            clusterDrift = drift;
            return true;
        }

        /// <summary>
        /// Which cell a shot that reached the glass lands in — straight up past the whole cluster, so the field's
        /// top level. False when the cell it rounds to is outside the field or already taken.
        /// </summary>
        /// <remarks>
        /// The <i>choice</i> reads only X and Z (<see cref="BallsMap.TryFindEmptyCeilingCell"/> pins the level to
        /// the field's top and throws the contact's Y away), and the plate only ever moves in Y — so nothing about
        /// where a ball attaching to the glass lands depends on how far the ceiling has come down. The drift below
        /// does, and it is the whole reason this takes the plate's live height.
        /// </remarks>
        /// <param name="ceilingCentreY">
        /// Centre Y of the glass plate <b>as it stands now</b>, not where the level hung it — the body's own pose.
        /// A level walks the plate down and the cluster with it, so this is what keeps the drift honest as the
        /// descent accumulates.
        /// </param>
        /// <param name="clusterDrift">
        /// How far the top level hangs off its lattice, from the plate's own height rather than from a ball —
        /// there is no hit ball on this path, and a ball attaching to the glass is going to end up exactly where
        /// the ceiling anchor puts it (<see cref="BallsConstraintsBuilder.CeilingRestY"/>). Zero when this
        /// returns <c>false</c>. See <see cref="CellWorldPosition"/> for what it is for.
        /// </param>
        public static bool TrySolveAgainstCeiling(BallsMap map, Vector3 worldContact, Vector3 worldOffset,
            float ceilingCentreY, out XZLevel cell, out Vector3 clusterDrift)
        {
            cell = new XZLevel(-1, -1, -1);
            clusterDrift = Vector3.Zero;

            if (map == null) return false;
            if (!map.TryFindEmptyCeilingCell(worldContact - worldOffset, out cell)) return false;

            //Vertical only: the plate never moves in X or Z, so the cell's own lattice X/Z are already right.
            clusterDrift = new Vector3(0f,
                BallsConstraintsBuilder.CeilingRestY(ceilingCentreY)
                    - (map.GetRealCenteredPosition(cell).Y + worldOffset.Y),
                0f);

            return true;
        }

        /// <summary>
        /// Where a solved cell actually <b>is</b>, in world space, as the cluster hangs this instant: the cell's
        /// ideal lattice position, taken up into the world frame and then corrected by the local drift the solve
        /// measured.
        /// </summary>
        /// <remarks>
        /// It exists because the ideal lattice position is <i>not</i> where a ball in that cell comes to rest, and
        /// treating it as though it were is a defect that grows over a level rather than a small constant error.
        /// Two things move the cluster off its lattice. The structure hangs from the plate by an anchor that holds
        /// the top level a ball diameter under it, so at rest the cluster is already stretched — measured in #70 as
        /// +1.10 at the top decaying to −0.04 deep down. And the glass <b>descends</b>: every step drags the whole
        /// structure a further <c>CEILING_DESCENT_PER_STEP</c> down while the lattice stays exactly where the level
        /// hung it, so by the end of an authored level the two are the better part of ten levels apart.
        /// <para>
        /// Everything a solved cell is drawn or reported at goes through here, for the reason the type exists at
        /// all: the ghost of the landing preview, the glide a landed ball is drawn arriving with, and the world
        /// position the landing is announced at (the thunk's panning, the award's birth point). Placed from the
        /// lattice alone, the ghost floated where the cluster used to hang, the glide launched the ball from
        /// several diameters below its own impact, and the award flew in from above the cluster's roof.
        /// </para>
        /// <para>
        /// <paramref name="worldOffset"/> and the offset folded into <paramref name="clusterDrift"/> cancel, so
        /// this is really "the hit ball's live pose plus the cell's lattice offset from it". Both are taken anyway,
        /// because a caller holding one and not the other would have to know that to be safe.
        /// </para>
        /// </remarks>
        public static Vector3 CellWorldPosition(BallsMap map, XZLevel cell, Vector3 worldOffset, Vector3 clusterDrift)
            => map.GetRealCenteredPosition(cell) + worldOffset + clusterDrift;

        /// <summary>
        /// The first structure ball a shot leaving <paramref name="origin"/> along <paramref name="direction"/>
        /// would touch, and where it would touch it. False when the line reaches none of them.
        /// </summary>
        /// <remarks>
        /// This is a <b>swept sphere</b> and not a ray, which is the difference between a preview that is right and
        /// one that is nearly right: the shot has a radius, so it touches the first ball whose surface the moving
        /// surface reaches, and a thin line down the middle of the bore misses a ball sitting just off to the side
        /// that the ball itself would clip. Testing the line against each structure ball grown by the sum of the two
        /// radii is exactly that question, in closed form.
        /// <para>
        /// It walks the live <c>PhysicsBall</c> array rather than asking the simulation, for two reasons. It is the
        /// same array the attach path consults, so the two cannot disagree about which balls exist. And a Bepu
        /// sweep would need a shape, a pool and a hit handler per frame where this needs a few hundred dot products
        /// and <b>allocates nothing</b> — it is called every frame the gun is aimed, which is the per-frame budget
        /// <c>BestPractices.md</c> §3 is about.
        /// </para>
        /// <para>
        /// The one thing it cannot promise is the stepped simulation's own answer. The shot travels in discrete
        /// steps (1.667 world units at <c>SHOOT_SPEED</c> against a 1/120 s step), and the cluster goes on
        /// swaying during the ~0.1 s of flight. Both are small against a cell of 1.0, and both were measured in
        /// #70 — but this is why the preview is a ghost rather than a promise.
        /// <para>
        /// The step term is the smaller of the two now: the shot's collidable is <b>swept</b>
        /// (<see cref="PhysicsWorld"/>'s constructor, and the measurement that forced it), so the contact is
        /// placed at the solved time of impact inside the step rather than wherever the first overlapping step
        /// happened to leave the ball. #70's figures predate that change and have not been re-measured against
        /// it, so read them as the worst case rather than the current one.
        /// </para>
        /// </para>
        /// </remarks>
        /// <param name="balls">The live structure. Null entries are cells a release has emptied.</param>
        /// <param name="origin">The muzzle, in world space.</param>
        /// <param name="direction">The aim. Need not be normalised.</param>
        /// <param name="radiusSum">The shot's radius plus a structure ball's — the grown sphere's radius.</param>
        /// <param name="hit">The ball reached first, or null.</param>
        /// <param name="worldContact">Where the two surfaces meet, on the line between the centres.</param>
        public static bool TryFindFirstHit(PhysicsBall[,,] balls, Vector3 origin, Vector3 direction,
            float radiusSum, out PhysicsBall hit, out Vector3 worldContact)
        {
            hit = null;
            worldContact = Vector3.Zero;

            if (balls == null) return false;

            //A degenerate aim, guarding the normalise below. The barrel always points somewhere in play, so this
            //is a guard against a caller with nothing aimed yet rather than a case the gun produces.
            float lengthSquared = direction.LengthSquared();
            if (lengthSquared < Constants.THOUSANDTH) return false;

            Vector3 aim = direction / MathF.Sqrt(lengthSquared);
            float grownSquared = radiusSum * radiusSum;
            float nearest = float.MaxValue;
            Vector3 nearestCentre = Vector3.Zero;

            int sizeX = balls.GetLength(0), sizeZ = balls.GetLength(1), sizeLevel = balls.GetLength(2);

            for (int level = 0; level < sizeLevel; level++)
                for (int x = 0; x < sizeX; x++)
                    for (int z = 0; z < sizeZ; z++)
                    {
                        PhysicsBall candidate = balls[x, z, level];
                        if (candidate == null) continue;

                        Vector3 centre = candidate.BallReference.Pose.Position.ToXna();
                        Vector3 toCentre = centre - origin;

                        //How far along the aim the closest approach is. Behind the muzzle is not a hit: the gun
                        //sits under the cluster and half of it would otherwise be "hit" out of the back of the bore.
                        float along = Vector3.Dot(toCentre, aim);
                        if (along <= 0f || along >= nearest) continue;

                        //Closest approach, squared. Grown by both radii, so this is the moving surface's touch.
                        float perpendicularSquared = toCentre.LengthSquared() - along * along;
                        if (perpendicularSquared > grownSquared) continue;

                        //Back off to where the surfaces first meet rather than to the closest approach
                        float halfChord = MathF.Sqrt(MathF.Max(grownSquared - perpendicularSquared, 0f));
                        float entry = along - halfChord;
                        if (entry < 0f || entry >= nearest) continue;

                        nearest = entry;
                        nearestCentre = centre;
                        hit = candidate;
                    }

            if (hit == null) return false;

            //The contact is on the line between the two centres, a structure ball's radius in from its centre --
            //which is where the narrow phase would put it, and what the cell search measures against.
            Vector3 shotCentre = origin + aim * nearest;
            Vector3 toShot = shotCentre - nearestCentre;
            float distance = toShot.Length();

            worldContact = distance < Constants.THOUSANDTH
                ? nearestCentre
                : nearestCentre + toShot * (BallsConstraintsBuilder.BALL_RADIUS / distance);

            return true;
        }
    }
}
