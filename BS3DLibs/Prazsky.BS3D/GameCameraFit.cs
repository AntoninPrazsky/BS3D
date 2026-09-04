using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameObjects;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.BS3D
{
    /// <summary>
    /// Where the game camera stands and how high it aims, and how far out the gun orbits — solved together for
    /// one field and one display. It stood about 120 lines line-for-line in both executables until #76, every
    /// dial value-identical, with a single documented difference between the copies (the Y frame, see below).
    /// <para>
    /// This has to be <b>solved</b> rather than tuned, because both of its inputs move. The field is sized per
    /// map — <c>20x20x20</c> stands 14 units tall against <c>Full.json</c>'s 10 — so a stand-off that frames
    /// one crops the other off the top of the screen, which is exactly what the hand-tuned number it replaced
    /// did. And the frustum is sized per display: <c>CreatePerspectiveFieldOfView</c> takes the <b>vertical</b>
    /// FOV, so a wider screen only adds width. That is the behaviour wanted — the map keeps its size on an
    /// ultrawide and the extra width goes to scenery — but it also means the horizontal fit is generous at 21:9
    /// and tightest on the narrowest display, so both axes have to be checked.
    /// </para>
    /// <para>
    /// <b>The field's vertical extent arrives as two parameters and never as an assumption</b>, because that is
    /// the one place the two callers genuinely differ: in the Testbed the lattice frame <i>is</i> the world
    /// frame, so the field's floor is <c>y = 0</c>, while in the Game level 0 sits at the cluster's world
    /// offset. "Levels in the game" in docs/game-session.md records it: <i>"The one thing that differs from the
    /// Testbed's copy is the Y frame."</i> Everything else this reads off the gun is already world-framed
    /// (its orbit centre; its trunnion height it does not read so much as re-derive — the gun stands on the
    /// island's dish, so the height is <c>CannonRig.TrunnionHeightAt</c> of whatever radius the round is
    /// considering), so nothing else had to be opened up.
    /// </para>
    /// <para>
    /// <b>Pure, and no <see cref="Cannon"/> is touched.</b> The solve alternates — the lens is placed to frame
    /// the field <i>and the gun</i>, and the gun is placed a fixed distance in front of the lens — so it walks
    /// candidate orbit radii, and both pre-hoist copies walked them by <i>assigning</i>
    /// <see cref="Cannon.OrbitRadius"/> mid-solve and reading the position the setter slid the gun to. That
    /// worked, but it left the gun standing at every intermediate guess and made a pure geometry problem
    /// mutate the scene three times per level load. Here the gun's pose at a candidate radius is computed
    /// instead (<see cref="CannonPositionAt"/>), the radius comes back in the result, and the caller assigns it
    /// <b>once</b>, after the solve.
    /// </para>
    /// <para>
    /// <b>What deliberately stayed with the callers.</b> The field of view: each executable owns its own
    /// <c>GAME_FOV</c>, animates it (the Testbed's free/game-mode transition) and lerps it towards the
    /// precise-aim lens's own narrower one, so the fit has to be handed the very FOV the camera is using rather
    /// than a second copy of the figure. The field's <b>footprint</b>: both callers take the half-extents off
    /// <c>CeilingPlate.FootprintFor</c>, because those corners <i>are</i> the plate's — a margin written out
    /// again here would silently stop agreeing with the drawn plate and the collidable the moment it is
    /// retuned. The ceiling's height, which they derive differently (the Testbed from the map's level count,
    /// the Game from a plate that slides down during play). The pre-first-load defaults for the stand-off and
    /// the aim height, which only cover the frames before a field exists. The <c>[camera]</c> log line, which
    /// names the field's dimensions — something this never learns. And the gun's barrel reach
    /// (<see cref="Solve"/>'s <c>cannonReach</c>): its owner is the gun's geometry — both callers pass
    /// <c>CannonRig.BarrelReach</c>, read off the mesh actually built — and hoisting it into the fit would
    /// give one number two homes.
    /// </para>
    /// </summary>
    public static class GameCameraFit
    {
        /// <summary>
        /// Where the camera stands relative to the gun's trunnions: back along <see cref="Cannon.StandBearing"/> (the
        /// horizontal away from the field) and up — here <i>below</i> them, near the arena floor, because the
        /// player is aiming at the underside of a cluster hanging overhead and needs to see that face, not the
        /// top of the gun.
        /// <para>
        /// Note how little of the angle on the cluster is actually the camera's to give: it is
        /// <c>atan((clusterY - cameraY) / horizontal distance)</c>, and a camera behind the gun is always
        /// further out horizontally than the gun itself, so it sees the cluster <i>flatter</i> than the 30
        /// degrees the barrel has. On the floor it is about 25; the 40-odd of a real from-below view would need
        /// it at <c>y = -20</c>. What the drop actually buys is the range from 5 degrees (edge-on, the cluster a
        /// strip at the top of the frame) to 25, and that is the whole difference between reading the layout
        /// and not.
        /// </para>
        /// </summary>
        public const float CAMERA_HEIGHT = -1.5f;

        /// <summary>
        /// How much of the frustum the fit is allowed to fill, on both axes. Under 1 so nothing sits hard
        /// against the frame's edges, which reads as cropped even when nothing is.
        /// </summary>
        public const float FIT_MARGIN = 0.92f;

        /// <summary>
        /// How far in front of the camera the gun stands. This, and not its distance from the field, is what
        /// the orbit radius is solved for: the queue in the barrel is really a HUD element, and it has to stay
        /// the same size on screen whatever map is loaded, which a fixed distance from the lens gives and a
        /// fixed distance from the field does not (the camera itself moves per map).
        /// <para>
        /// Two lower bounds override it — <see cref="CANNON_FIELD_CLEARANCE"/> and
        /// <see cref="CANNON_MAX_REST_ELEVATION"/> — and which one binds changes with the map, so the orbit
        /// radius can never be a single number.
        /// </para>
        /// </summary>
        public const float CANNON_CAMERA_STANDOFF = 15f;

        /// <summary>
        /// Clearance the gun keeps outside the field's corner. It must clear the field's <b>footprint</b> at
        /// every orbit angle — closer than the field's corner and it would stand under the cluster it is
        /// shooting at.
        /// </summary>
        public const float CANNON_FIELD_CLEARANCE = 2f;

        /// <summary>
        /// The steepest <b>resting</b> aim the gun is placed for, in radians (about 40°). The closer it stands
        /// the more steeply it looks up, so it is held far enough out that the rest pose sits mid-range within
        /// <see cref="Cannon.MaxElevation"/> (which reaches ~80°), leaving headroom to elevate onto the high
        /// cells — the top corners of a large map need ~50-65° facing them, see the Testbed's <c>aimcheck</c>.
        /// Ignore this bound and the gun is silently held below the angle it is trying to sit at.
        /// </summary>
        public const float CANNON_MAX_REST_ELEVATION = 0.70f;

        /// <summary>
        /// How far the player may walk the gun from its solved rest radius, either way (W/S — see
        /// <see cref="Cannon.Advance"/>). A fixed stroke rather than a per-level fraction, so the walk feels
        /// the same on every map, and bounded twice at the near end: the range's minimum never crosses the
        /// field-footprint clearance (the one bound that is about geometry rather than feel — a gun under the
        /// cluster is shooting at its own ceiling). The <b>elevation</b> bound is deliberately NOT applied to
        /// the range: walking in past <see cref="CANNON_MAX_REST_ELEVATION"/> steepens the resting aim and
        /// pulls the highest near cells out of the clamp's reach, and that trade is the player's to make and
        /// walk back out of — the rest pose the level opens on is still placed by the full four-bound solve.
        /// At the far end the stroke also keeps the magazine readable — the gun backs off <b>towards</b> the
        /// lens, so a retreat is what makes it loom — and it may close to
        /// <see cref="CANNON_CAMERA_STANDOFF"/> − stroke = 11 units of it and no nearer. That bound used to
        /// hold only where the stand-off bound had placed the gun, which on the shipped set is nowhere:
        /// <see cref="CANNON_DRAIN_CLEARANCE"/> parks every level's gun at 15.5 and its rest gap measures
        /// 12.3 to 15.0, so a full retreat brought it to 8.3. <see cref="CANNON_ADVANCE_FOLLOW"/> is what
        /// restores it, by moving the lens rather than by shortening this.
        /// </summary>
        public const float CANNON_ADVANCE_STROKE = 4f;

        /// <summary>
        /// How much of a <b>retreat</b> the overview lens goes along with (#322): half of it, the walk keeping
        /// the other half as its own on-screen answer. Only the retreat — walking in leaves the lens where the
        /// solve put it, and the asymmetry is the geometry rather than a taste: the solved stand-off is the
        /// <i>smallest</i> distance at which the field, its glass and the gun all fit, so a lens that followed
        /// the gun in would eat <see cref="FIT_MARGIN"/> and then crop the field — while a lens that follows it
        /// out only ever makes everything smaller and can crop nothing.
        /// <para>
        /// <b>Why the frame has to move at all.</b> Precise aim anchors the lens to the muzzle, so while RMB is
        /// held the walk moves the world perfectly and the frame snapped back to a dead stand-off the moment it
        /// was released — the walk stopped reading as motion, which is what #322 reported. A fraction rather
        /// than a full follow because the gun's own change of size is the walk's only other feedback, and a
        /// lens that held the gun exactly still would take it away (the queue in the bore is effectively a HUD
        /// element at a fixed distance from the lens, and that is <see cref="CANNON_CAMERA_STANDOFF"/>'s job).
        /// </para>
        /// <para>
        /// <b>It is a floor as well as a fraction</b>, and the floor is what binds on a tight level: the lens
        /// takes at least as much of the retreat as it must to keep the gun
        /// <see cref="CANNON_CAMERA_STANDOFF"/> − <see cref="CANNON_ADVANCE_STROKE"/> = 11 units away, the
        /// figure the stroke was capped for. Measured on the shipped set, this half-follow moves the lens 2.0
        /// of the 4-unit retreat on a wide level (rest gap 15.0) and 2.7 on the tightest (12.3, where the floor
        /// takes over), and the gun ends the walk 11.0 to 13.0 units out instead of 8.3 to 11.0.
        /// </para>
        /// </summary>
        public const float CANNON_ADVANCE_FOLLOW = 0.5f;

        /// <summary>
        /// Clearance the gun's stand keeps outside the drain's mouth (<c>ArenaIsland.FUNNEL_TOP_RADIUS</c>),
        /// bounding the rest radius and the walk's near end both: the wheels must stay on stone, never roll
        /// out over the glass funnel the released balls fall through. It is 1.5 and not a hair because of two
        /// measured figures, both about the gold bead that rings the mouth (tube 0.5 about the rim circle, so
        /// it bulges ~0.45 proud of the dish out to radius ~14.45): a wheel of <c>CannonRig</c>'s radius
        /// resting nearer than ~1.1 outside the mouth leans its leading face into that bulge, and at full
        /// traverse (±45°) the carriage's inner wheel swings up to <c>WHEEL_TRACK · sin 45° ≈ 1.06</c> inside
        /// the stand's own radius. 1.5 clears the bead in the radial stance and keeps the traversed inner
        /// wheel's contact on stone at once — the muzzle overhanging the glass at the near end is deliberate
        /// and reads as leaning over the drain; the wheels crossing the gold curb would read as driving in.
        /// </summary>
        public const float CANNON_DRAIN_CLEARANCE = 1.5f;

        /// <summary>
        /// How many times the two solves alternate, each depending on the other. It very nearly settles on the
        /// first round — whichever of the three bounds decides the orbit radius, the camera's solve then sees a
        /// gun whose angular footprint barely moves: pinned at <see cref="CANNON_CAMERA_STANDOFF"/> from the lens
        /// when the stand-off binds, and at a constant radius when either lower bound does.
        /// <para>
        /// <b>But it does not converge on the first round, and saying it did is the mistake worth not repeating
        /// here.</b> Both pre-hoist copies asserted exactly that in their own comments, and the measurement taken
        /// while hoisting them says otherwise: a sub-0.1 residual survives to the third round, in <i>both</i>
        /// directions, because the alternation lands at a marginally different point depending on what round one
        /// was seeded from. Two figures move at one decimal because of it — see "The gun's hardware, in one copy"
        /// in docs/testbed.md. So this is 3 for a reason, and dropping it to 1 as dead work would quietly move
        /// the fit's output on some fields.
        /// </para>
        /// </summary>
        private const int CONVERGENCE_ROUNDS = 3;

        //The bisection over the stand-off: enough steps to settle a 400-unit bracket well past float precision
        //(400 / 2^32), and a far bound no supported field comes near. The near bound is the lens right behind
        //the gun, a margin clear of it.
        private const int BISECTION_STEPS = 32;
        private const float FAR_BOUND = 400f;
        private const float NEAR_BOUND_MARGIN = 2f;

        //The overview lens's floor: the height it has when the gun stands at the island's outer arris — the
        //stance every gun had before the dish, so it is exactly the lens height the whole overview was tuned
        //at. The gun rides the dish down (up to DISH_DEPTH below the arris near the drain) and the lens
        //deliberately does NOT ride with it: CAMERA_HEIGHT puts it 1.5 below the trunnions, which against a
        //sunken gun is below the arris itself — from out past the island's edge the lens then looks AT the
        //coping instead of over it, and the stone swallowed the bottom half of the frame (measured; it reads
        //as lying on the ground). Floored here, the overview holds the height it always had and the gun
        //visibly sinks and climbs the dish under a lens that stays level. The walk's follow (#322) is
        //horizontal for exactly this reason: it slides the lens along the stand bearing and never bobs it.
        private static readonly float LENS_FLOOR_Y =
            CannonRig.TrunnionHeightAt(ArenaIsland.FLOOR_RADIUS) + CAMERA_HEIGHT;

        //One home for the lens's height rule: LensPositionAt and the solved aim height must apply the same
        //floor or the camera would aim about a lens it does not stand at.
        private static float LensHeightFor(float trunnionY) =>
            MathF.Max(trunnionY + CAMERA_HEIGHT, LENS_FLOOR_Y);


        /// <summary>
        /// Where the lens sits for a given stand-off — the pose the fit searches over, and the same pose the
        /// callers put their overview camera at every frame, so the one expression has one home.
        /// <para>
        /// The two figures it stands off from are the gun's own geometry and are read from it
        /// (<see cref="Cannon.OrbitCenterGround"/>, <see cref="Cannon.StandBearing"/>) rather than recomputed
        /// here — they were briefly spelled out in both places during #76, which is the very duplication this
        /// batch exists to remove. <b>Both are invariant under the solve:</b> sliding the gun along its orbit
        /// changes the radius and never the angle, so each is read once and reused for every candidate.
        /// </para>
        /// <para>
        /// <b>The stand-off it is given is the level's, and what it stands at is that plus the share of a
        /// retreat the lens takes</b> (<see cref="WalkedDistance"/>) — which is why the walk moves the frame
        /// while the fit stays something solved once. The solve itself goes to <see cref="LensPositionAt"/>
        /// and never through here, so it keeps searching over the rest pose and cannot chase its own gun.
        /// </para>
        /// </summary>
        public static Vector3 CameraPosition(Cannon cannon, float distance) =>
            LensPositionAt(cannon.OrbitCenterGround, cannon.StandBearing, cannon.Position.Y,
                WalkedDistance(cannon, distance));

        /// <summary>
        /// The stand-off the lens actually holds while the gun is off its rest radius: the level's own, plus
        /// <see cref="CANNON_ADVANCE_FOLLOW"/> of however far the gun has backed away — never less than what
        /// keeps it <see cref="CANNON_CAMERA_STANDOFF"/> − <see cref="CANNON_ADVANCE_STROKE"/> units ahead of
        /// the lens, and never more than the gun itself moved, so the lens cannot overtake what it follows.
        /// <para>
        /// A walk <i>towards</i> the field returns the level's stand-off untouched: see
        /// <see cref="CANNON_ADVANCE_FOLLOW"/> for why that half is not symmetrical. Four float operations on
        /// a per-frame path, and it reads the gun's own <see cref="Cannon.RestRadius"/> rather than taking a
        /// second copy of it from the caller — a mid-level re-solve moves that mark with the gun.
        /// </para>
        /// </summary>
        private static float WalkedDistance(Cannon cannon, float distance)
        {
            float backedOff = cannon.OrbitRadius - cannon.RestRadius;

            if (backedOff <= 0f) return distance;

            //How much of the retreat the gun may spend looming before the lens has to give way at all
            float slack = MathF.Max(0f, distance - cannon.RestRadius - (CANNON_CAMERA_STANDOFF - CANNON_ADVANCE_STROKE));

            return distance + MathF.Max(CANNON_ADVANCE_FOLLOW * backedOff, backedOff - slack);
        }

        /// <summary>
        /// Solves the gun's orbit radius and the camera's stand-off and aim height together. Run on every map
        /// or level load and every aspect change — not per frame — and allocates nothing regardless: the result
        /// is a readonly struct and every step of the solve is stack arithmetic.
        /// <para>
        /// The <paramref name="cannon"/> is read and never written: see the class remarks for why that is the
        /// whole point rather than tidiness.
        /// </para>
        /// </summary>
        /// <param name="cannon">The gun being placed. Only its orbit centre and its current orbit radius and
        /// height are read — the latter two purely to seed the first round; every later round re-derives the
        /// height from its own candidate radius, the dish deciding it.</param>
        /// <param name="cannonReach">Half-extent of the box the gun is framed as: large enough to hold the
        /// barrel at any aim, so the fit does not change as the player elevates or traverses. Both callers pass
        /// <c>CannonRig.BarrelReach</c> — the trunnions-to-cascabel-pole distance, the tube's longer half since
        /// the dome closed the breech.</param>
        /// <param name="halfX">Half the field's footprint in X, <b>as the ceiling plate measures it</b> — off
        /// <c>CeilingPlate.FootprintFor</c>, never a margin written out again at the call site.</param>
        /// <param name="halfZ">Half the field's footprint in Z, from the same helper.</param>
        /// <param name="bottomY">The floor of the play space in <b>world</b> Y. A deep level's empty growth
        /// levels belong inside this on purpose — the cluster grows down into them, so they have to be in frame
        /// before the first ball ever lands there.</param>
        /// <param name="topY">The upper face of the glass ceiling in world Y.</param>
        /// <param name="verticalFov">The camera's vertical field of view, in radians — the one it is actually
        /// rendering with.</param>
        /// <param name="aspectRatio">The camera's aspect ratio. Which frustum axis binds flips with it, which
        /// is why a resize has to re-solve.</param>
        public static CameraFit Solve(Cannon cannon, float cannonReach, float halfX, float halfZ,
            float bottomY, float topY, float verticalFov, float aspectRatio)
        {
            Problem problem = new(
                cannon.OrbitCenterGround, cannon.StandBearing, cannonReach,
                halfX, halfZ, bottomY, topY,
                verticalFov * Constants.HALF * FIT_MARGIN,
                MathF.Atan(MathF.Tan(verticalFov * Constants.HALF) * aspectRatio) * FIT_MARGIN);

            //Hoisted out of the alternation: the footprint and drain clearances each bound the rest radius
            //AND the near end of the advance range below, and writing either twice is how the two would drift.
            float clearFootprint = MathF.Sqrt(halfX * halfX + halfZ * halfZ) + CANNON_FIELD_CLEARANCE;
            float clearDrain = ArenaIsland.FUNNEL_TOP_RADIUS + CANNON_DRAIN_CLEARANCE;

            //Round one is seeded off where the gun stands now rather than off the caller's last stand-off,
            //which is what keeps this from wanting a ninth parameter for a field the caller owns. It is the
            //same seed either way: the gun was itself placed CANNON_CAMERA_STANDOFF in front of the previous
            //solve's lens, and where a lower bound had held it further out instead, that same bound is about to
            //hold it there again below. Since the advance walk arrived the gun may also stand up to
            //CANNON_ADVANCE_STROKE off that rest when a mid-level re-solve lands — still only a seed: the
            //bounds decide where the alternation settles, to within its documented sub-0.1 residual. Read off
            //the property, not re-derived: both copies measured the orbit radius back out of the gun's
            //position with a sqrt (issue #72), and the walk and the setter both move the gun only along the
            //orbit, so the property IS that distance. The trunnion height is seeded the same way — the gun
            //stands on the island's dish, so its height is a function of its radius (CannonRig
            //.TrunnionHeightAt), re-derived below from each round's radius exactly as Cannon itself will
            //re-seat it when the caller assigns the result.
            float orbitRadius = cannon.OrbitRadius;
            float distance = orbitRadius + CANNON_CAMERA_STANDOFF;
            float trunnionY = cannon.Position.Y;

            //Overwritten by every round below; the compiler cannot know CONVERGENCE_ROUNDS is never zero.
            float targetY = 0f;

            for (int round = 0; round < CONVERGENCE_ROUNDS; round++)
            {
                //How high the gun looks up from its trunnions to the centre it aims at, which is what the
                //resting elevation bound is measured against. World-framed on both sides, so the Y frame does
                //not enter. Inside the alternation since the dish: the trunnions ride it, so the rise is a
                //function of the radius the previous round settled on — the elevation bound is
                //self-referential through the dish's profile, and the alternation resolves that coupling the
                //same way it resolves the lens–gun one (it contracts at ~0.13 a round, the dish's grade over
                //tan(CANNON_MAX_REST_ELEVATION), so three rounds settle it well past the documented residual).
                float aimRise = cannon.OrbitCenter.Y - trunnionY;

                orbitRadius = SolveOrbitRadius(distance, clearFootprint, clearDrain, aimRise);
                trunnionY = CannonRig.TrunnionHeightAt(orbitRadius);
                distance = problem.SolveStandOff(orbitRadius, trunnionY, out targetY);
            }

            //The walk the player gets around that rest: a fixed stroke either way, its near end held off the
            //field's footprint and the drain's mouth by the same clearances the rest radius itself respects —
            //see CANNON_ADVANCE_STROKE for what deliberately does and does not bound it.
            return new CameraFit(distance, targetY, orbitRadius,
                MathF.Max(MathF.Max(clearFootprint, clearDrain), orbitRadius - CANNON_ADVANCE_STROKE),
                orbitRadius + CANNON_ADVANCE_STROKE);
        }

        /// <summary>
        /// Puts the gun <see cref="CANNON_CAMERA_STANDOFF"/> in front of the camera, held off by the three
        /// lower bounds documented with that constant and <see cref="CANNON_DRAIN_CLEARANCE"/>.
        /// </summary>
        private static float SolveOrbitRadius(float cameraDistance, float clearFootprint, float clearDrain,
            float aimRise)
        {
            float clearElevation = aimRise / MathF.Tan(CANNON_MAX_REST_ELEVATION);

            return MathF.Max(cameraDistance - CANNON_CAMERA_STANDOFF,
                MathF.Max(clearDrain, MathF.Max(clearFootprint, clearElevation)));
        }

        /// <summary>
        /// The lens for a stand-off, from the pieces rather than from a <see cref="Cannon"/>: the field's centre
        /// at ground level, back along the bearing, and up to <see cref="CAMERA_HEIGHT"/> relative to the
        /// trunnions. The public <see cref="CameraPosition"/> and the solve both come through here so the
        /// expression cannot drift into two versions.
        /// </summary>
        private static Vector3 LensPositionAt(Vector3 centreGround, Vector3 bearing, float trunnionY, float distance) =>
            centreGround + bearing * distance + Vector3.Up * LensHeightFor(trunnionY);

        /// <summary>
        /// Where the gun's trunnions stand at a candidate orbit radius. This is exactly what
        /// <see cref="Cannon.OrbitRadius"/>'s setter would slide the gun to — it moves along the current orbit
        /// angle and re-seats the height on the island's dish, which is why <paramref name="trunnionY"/>
        /// arrives already derived from the candidate radius (<c>CannonRig.TrunnionHeightAt</c>, the same
        /// figure the setter would land on) — computed instead of assigned, which is what lets the alternation
        /// walk candidates without touching the scene.
        /// </summary>
        private static Vector3 CannonPositionAt(Vector3 centreGround, Vector3 bearing, float trunnionY, float orbitRadius) =>
            centreGround + bearing * orbitRadius + Vector3.Up * trunnionY;

        /// <summary>
        /// The solve's invariants — everything that does not change between rounds, reduced to numbers and
        /// directions. No <see cref="Cannon"/> goes in, which is what makes the alternation structurally unable
        /// to move the gun. A readonly struct on the stack: it is built once per solve and costs no allocation.
        /// </summary>
        private readonly struct Problem
        {
            private readonly Vector3 _centreGround;
            private readonly Vector3 _bearing;
            private readonly float _cannonReach;
            private readonly float _halfX;
            private readonly float _halfZ;
            private readonly float _bottomY;
            private readonly float _topY;
            private readonly float _verticalHalf;
            private readonly float _horizontalHalf;

            internal Problem(Vector3 centreGround, Vector3 bearing, float cannonReach,
                float halfX, float halfZ, float bottomY, float topY, float verticalHalf, float horizontalHalf)
            {
                _centreGround = centreGround;
                _bearing = bearing;
                _cannonReach = cannonReach;
                _halfX = halfX;
                _halfZ = halfZ;
                _bottomY = bottomY;
                _topY = topY;
                _verticalHalf = verticalHalf;
                _horizontalHalf = horizontalHalf;
            }

            /// <summary>
            /// The smallest stand-off at which everything fits, and the aim height that goes with it.
            /// </summary>
            /// <param name="trunnionY">The gun's height at <paramref name="orbitRadius"/> — a per-round
            /// argument rather than an invariant of the problem, because the trunnions ride the island's dish
            /// and so move with the very radius the alternation is walking.</param>
            /// <param name="targetY">World Y the camera looks at, over the field's centre: the axis elevation
            /// carried out to the stand-off it was solved at.</param>
            internal float SolveStandOff(float orbitRadius, float trunnionY, out float targetY)
            {
                //Everything fits from far enough away and nothing does from close in, so the smallest distance
                //that fits can be bisected for. The near bound is the lens right behind the gun.
                float near = orbitRadius + NEAR_BOUND_MARGIN;
                float far = FAR_BOUND;

                for (int i = 0; i < BISECTION_STEPS; i++)
                {
                    float middle = (near + far) * Constants.HALF;
                    if (FitsAt(middle, orbitRadius, trunnionY, out _)) far = middle;
                    else near = middle;
                }

                FitsAt(far, orbitRadius, trunnionY, out float axisElevation);

                targetY = LensHeightFor(trunnionY) + far * MathF.Tan(axisElevation);

                return far;
            }

            /// <summary>
            /// Whether the field, its ceiling and the gun all land inside the frustum with the lens that far
            /// out, and at what elevation the view axis has to sit for it. Elevations are measured off the
            /// horizontal and bisected, which is what centres the subject between the top and bottom edges.
            /// </summary>
            private bool FitsAt(float distance, float orbitRadius, float trunnionY, out float axisElevation)
            {
                //Copied into locals before the local function below, so it captures nothing but locals
                Vector3 back = _bearing;
                Vector3 camera = LensPositionAt(_centreGround, back, trunnionY, distance);
                Vector3 forward = -back;
                Vector3 right = Vector3.Cross(Vector3.Up, forward);

                float minElevation = float.MaxValue;
                float maxElevation = float.MinValue;
                float maxSide = 0f;
                bool ahead = true;

                void Consider(Vector3 point)
                {
                    Vector3 offset = point - camera;
                    float depth = Vector3.Dot(offset, forward);

                    //On or behind the lens: there is no angle to measure, and the pose is rejected outright
                    if (depth <= Constants.ONE) { ahead = false; return; }

                    float elevation = MathF.Atan2(offset.Y, depth);
                    minElevation = MathF.Min(minElevation, elevation);
                    maxElevation = MathF.Max(maxElevation, elevation);
                    maxSide = MathF.Max(maxSide, MathF.Atan2(MathF.Abs(Vector3.Dot(offset, right)), depth));
                }

                //The field's eight corners, from the floor of the play space to the top of the glass ceiling.
                //The two Y values are the caller's, never assumed: see the class remarks on the Y frame. The
                //box is centred on the same point the camera stands off from, which is the only coherent
                //reading of a half-extent here; both pre-hoist copies wrote the corners about the world origin
                //instead, and that is the identical value while the gun orbits x = z = 0, as it does in both.
                for (int cornerX = -1; cornerX <= 1; cornerX += 2)
                    for (int cornerZ = -1; cornerZ <= 1; cornerZ += 2)
                    {
                        Consider(new Vector3(_centreGround.X + cornerX * _halfX, _bottomY, _centreGround.Z + cornerZ * _halfZ));
                        Consider(new Vector3(_centreGround.X + cornerX * _halfX, _topY, _centreGround.Z + cornerZ * _halfZ));
                    }

                //The gun, as a box around its trunnions large enough to hold the barrel at any aim, so the fit
                //does not change as the player elevates or traverses
                Vector3 cannon = CannonPositionAt(_centreGround, back, trunnionY, orbitRadius);

                Consider(cannon + Vector3.Up * _cannonReach);

                //Downwards the gun reaches its WHEELS and stops: the tube only ever elevates, so a point a
                //whole barrel's length under the trunnions is a pose it cannot strike. Reserving frame for it
                //cost 1.14 units of nothing — measured on Onion, where the frame's lower bound sat at -10.51
                //against a ground line of -9.37 — and because the frame's TOP is capped (FRAMED_LEVELS), that
                //reservation and not the field was deciding how little of a tall cluster fitted (#135). The
                //ground line is asked of the island rather than written out here: CannonRig.TrunnionHeightAt
                //is this same expression read the other way, so the two cannot drift apart as the dish is
                //retuned. Upwards the full reach stands — the barrel really can point there.
                Consider(new Vector3(cannon.X, ArenaIsland.FloorHeightAt(orbitRadius), cannon.Z));
                Consider(cannon + back * _cannonReach);
                Consider(cannon - back * _cannonReach);
                Consider(cannon + right * _cannonReach);
                Consider(cannon - right * _cannonReach);

                axisElevation = (minElevation + maxElevation) * Constants.HALF;

                return ahead
                    && (maxElevation - minElevation) * Constants.HALF <= _verticalHalf
                    && maxSide <= _horizontalHalf;
            }
        }
    }

    /// <summary>
    /// What the fit decided: how far back the camera stands, how high it aims, how far out the gun orbits and
    /// how far the player may walk it from there. A readonly struct, so a solve allocates nothing — and a
    /// value rather than four assignments, so the gun is moved once, by the caller, after the solve has
    /// finished with it.
    /// </summary>
    public readonly struct CameraFit
    {
        /// <summary>The camera's stand-off from the field's centre, along <see cref="Cannon.StandBearing"/>.</summary>
        public readonly float CameraDistance;

        /// <summary>World Y the camera looks at, over the field's centre.</summary>
        public readonly float CameraTargetY;

        /// <summary>What to assign to <see cref="Cannon.OrbitRadius"/> — the one write the solve implies.</summary>
        public readonly float CannonOrbitRadius;

        /// <summary>Near end of the advance walk (W) — hand both ends to <see cref="Cannon.SetAdvanceRange"/>
        /// with the radius above. See <see cref="GameCameraFit.CANNON_ADVANCE_STROKE"/> for the bounds.</summary>
        public readonly float CannonMinRadius;

        /// <summary>Far end of the advance walk (S).</summary>
        public readonly float CannonMaxRadius;

        public CameraFit(float cameraDistance, float cameraTargetY, float cannonOrbitRadius,
            float cannonMinRadius, float cannonMaxRadius)
        {
            CameraDistance = cameraDistance;
            CameraTargetY = cameraTargetY;
            CannonOrbitRadius = cannonOrbitRadius;
            CannonMinRadius = cannonMinRadius;
            CannonMaxRadius = cannonMaxRadius;
        }
    }
}
