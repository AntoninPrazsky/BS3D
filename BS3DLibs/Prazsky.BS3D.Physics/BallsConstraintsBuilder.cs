using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Prazsky.BS3D.Physics
{
    public static class BallsConstraintsBuilder
    {
        public static readonly float BALL_RADIUS = Constants.HALF;
        public static readonly float BALL_MASS = Constants.ONE;

        /// <summary>
        /// What a <see cref="BallKind.Heavy"/> ball weighs, as a multiple of <see cref="BALL_MASS"/> (#333) —
        /// <b>the one dial of that kind, and it was measured rather than picked</b>.
        /// <para>
        /// A <c>BallSocket</c> between two bodies of very different mass is the classic case that jitters or
        /// explodes, and this lattice was tuned against a uniform mass, so the figure that matters is not "how
        /// dramatic" but "how far from the cliff". The sweep that found it hung two fixtures headlessly in this
        /// very simulation — a bare strand (a 7×7 slab with an eight-deep column under it, the heavy ball at
        /// its tip: the most load a single socket chain ever carries) and a shipped level with its lowest ball
        /// made heavy — and ran each until the cluster slept or 20 s had passed. Two readings say where the
        /// cliff is, and neither is the sag:
        /// </para>
        /// <para>
        /// <b>Stretch</b> — the longest distance between two constrained neighbours, nominally 1 ball diameter.
        /// A real cluster's own figure is <b>1.044</b> (Kiln hanging untouched, every ball at
        /// <see cref="BALL_MASS"/>), so that is the envelope the mass has to stay inside. The strand reads
        /// 1.021 at ×6, <b>1.027 at ×12</b>, 1.031 at ×20, then 1.057 at ×25, 1.069 at ×30, 1.083 at ×35,
        /// 1.154 at ×60 and <b>1.402 at ×100 — a lattice visibly torn open</b>.
        /// </para>
        /// <para>
        /// <b>Whether the cluster can still come to rest.</b> A dense level sleeps at every ratio up to ×100
        /// (Kiln: 8.8 s untouched, 11.1 s at ×12, 13.0 s at ×100) and <b>stops sleeping at ×200</b>, where the
        /// strand's peak speed also leaves the rails entirely (40 u/s against ~2.7 at every ratio below ×35).
        /// </para>
        /// <para>
        /// <b>×12 is therefore a factor of two below where the lattice starts to deform</b> and an order of
        /// magnitude below where it breaks. What it buys is legible: the strand's tip hangs <b>0.46 units</b>
        /// lower than the same strand of ordinary balls (0.786 → 0.322), which is two-thirds of a lattice
        /// level — a branch that visibly droops, from a ball whose neighbours are not being pulled apart.
        /// </para>
        /// <para>
        /// <b>⚠ If a design wants a deeper droop, it hangs more off the ball rather than raising this.</b> The
        /// sag is a property of the load, not of the mass alone: the same ×12 inside a dense cluster (Kiln's
        /// lowest cell, braced on every side) moves it 0.02 units and is invisible, which is the mechanic
        /// behaving correctly rather than a shortfall. Raising the ratio is the one change here that trades
        /// the cluster's own physical honesty for drama.
        /// </para>
        /// <para>
        /// ⚠ <b>The figure is dt-insensitive across the band</b>, which is the second thing #333 asked for: the
        /// same sweep at 1/240, 1/120 and 1/60 s agrees to about 0.01 units at every ratio up to ×30 (×12 reads
        /// 0.324 / 0.322 / 0.313). The game steps a fixed <c>GameplayScreen.PHYSICS_TIMESTEP</c> of 1/120 s out
        /// of an accumulator whatever the display does, so a refresh rate cannot reach this at all — the sweep
        /// is the margin, not the mechanism.
        /// </para>
        /// </summary>
        public const float HEAVY_MASS_RATIO = 12f;

        /// <summary>
        /// How far apart two collidables may still be and have a contact generated between them, so the
        /// solver can start resisting before they actually touch rather than after they overlap.
        /// <para>
        /// A tenth of a unit here — a fifth of a ball's radius — which is the structure's figure and, since
        /// the shot was swept, the shot's as well: <see cref="PhysicsWorld"/> bounds the shot's margin to
        /// this instead of leaving it unbounded, because an unbounded margin is what let a contact be
        /// generated a whole step of travel before the ball arrived.
        /// </para>
        /// </summary>
        public const float SPECULATIVE_MARGIN = 0.1f;

        /// <summary>
        /// Threshold of squared velocity under which the body is allowed to go to sleep.
        /// </summary>
        private static readonly float SLEEP_THRESHOLD = Constants.HUNDREDTH;

        public static readonly SpringSettings SPRING_SETTINGS = new(frequency: 15f, dampingRatio: 1f);

        /// <summary>
        /// Minimum number of touching same-type balls required for the cluster to be released.
        /// </summary>
        public static readonly int MINIMUM_CLUSTER_SIZE = 3;

        private static Simulation _sphereShapeSimulation;
        private static TypedIndex _sphereShapeIndex;

        /// <summary>
        /// Shape index of the shared ball sphere (<see cref="BALL_RADIUS"/>) in the given simulation.
        /// The shape is added on first use and reused by every ball afterwards — adding a fresh one
        /// per <see cref="BuildBallsStructure"/> call would leak a shape on every map load.
        /// The cache resets when a different simulation instance is passed.
        /// </summary>
        public static TypedIndex GetSphereShapeIndex(Simulation simulation)
        {
            if (!ReferenceEquals(simulation, _sphereShapeSimulation))
            {
                _sphereShapeSimulation = simulation;
                _sphereShapeIndex = simulation.Shapes.Add(new Sphere(BALL_RADIUS));
            }

            return _sphereShapeIndex;
        }

        /// <param name="worldOffset">
        /// Added to every body's position, and to nothing else. A <see cref="BallsMap"/> lives in its own grid
        /// frame, and a caller may draw that frame somewhere other than the world origin — the game offsets it
        /// in Y so the empty field levels below the layout do not raise the cluster. The bodies then have to be
        /// created where they are <i>drawn</i>, because everything else the simulation touches (the floor, the
        /// ceiling, the muzzle a shot leaves from, the kill plane) is in world coordinates.
        /// <para>
        /// <b>Must be vertical: X and Z have to be zero.</b> A ball-to-ball anchor survives any translation,
        /// because <see cref="ConnectBalls"/> builds it from the <i>difference</i> of two positions read in the
        /// same frame. The ceiling anchor does not, and the reason is easy to miss: the two paths that build it
        /// read <i>different</i> frames — the build pass below hands
        /// <see cref="ConnectBallToCeiling"/> the body's world position, while
        /// <see cref="AttachBallToStructure"/> hands it the raw grid position — and they agree only because the
        /// one component they differ in is the Y that method throws away. Give this an X or a Z and the initial
        /// structure still builds correctly, but every ball that later attaches to the top level gets a ceiling
        /// anchor offset laterally and drags the whole cluster sideways.
        /// </para>
        /// <para>
        /// Nothing else in this class takes the offset, and adding it elsewhere is a bug rather than
        /// consistency: applying it twice tears the structure apart on the first timestep.
        /// </para>
        /// </param>
        //By value, not by ref: the simulation is only ever read here (it is a class, so the reference is all
        //that is needed), and the ref it used to take was what stopped a caller passing a property — which
        //PhysicsWorld's Simulation is, since #76.
        public static PhysicsBall[,,] BuildBallsStructure(StaticBall[,,] staticBalls, Simulation simulation, BodyReference ceilingReference, Vector3 worldOffset = default)
        {
            if (staticBalls == null) throw new NullReferenceException(nameof(staticBalls));
            if (simulation == null) throw new NullReferenceException(nameof(simulation));
            if (staticBalls.Rank != 3) throw new ArgumentOutOfRangeException(nameof(staticBalls.Rank));

            XZLevel size = XZLevel.FromArray(staticBalls);

            //Same [x, z, level] dimensions as the static balls array
            PhysicsBall[,,] physicsBalls = new PhysicsBall[size.X, size.Z, size.Level];

            #region Create physical representation for each ball (without connecting them)

            //Two inertias and not one per ball: the shape is the same sphere either way, only the mass differs
            //(#333), and ComputeInertia is arithmetic nobody needs to repeat some nine hundred times.
            Sphere ballShape = new(BALL_RADIUS);
            BodyInertia bodyInertia = ballShape.ComputeInertia(BALL_MASS);
            BodyInertia heavyInertia = ballShape.ComputeInertia(BALL_MASS * HEAVY_MASS_RATIO);

            CollidableDescription collidableDescription = new(GetSphereShapeIndex(simulation), SPECULATIVE_MARGIN);
            BodyActivityDescription bodyActivityDescription = new(SLEEP_THRESHOLD);

            for (byte level = 0; level < size.Level; level++)
            {
                for (byte x = 0; x < size.X; x++)
                {
                    for (int z = 0; z < size.Z; z++)
                    {
                        if (staticBalls[x, z, level] != null) //Is there even a ball here?
                        {
                            //⚠ THE ONE PLACE A HEAVY BALL IS HEAVY (#333). The kind travels on the StaticBall
                            //and nothing else in the simulation reads it: the mass IS the mechanic, so it is
                            //applied where a body is made and never asked about again. The shot's own body is
                            //built in PhysicsWorld against BALL_MASS and stays there — a heavy ball is placed
                            //by a level and is never loaded into the gun (specials never are), so there is no
                            //second door for this to be missed at.
                            BodyDescription bodyDescription = BodyDescription.CreateDynamic(
                                staticBalls[x, z, level].GetPosition() + worldOffset,
                                staticBalls[x, z, level].Kind == BallKind.Heavy ? heavyInertia : bodyInertia,
                                collidableDescription,
                                bodyActivityDescription);

                            BodyHandle bodyHandle = simulation.Bodies.Add(in bodyDescription);

                            BodyReference bodyReference = new(bodyHandle, simulation.Bodies);

                            PhysicsBall ball = new()
                            {
                                BallReference = bodyReference,
                                Type = staticBalls[x, z, level].Type,
                                Kind = staticBalls[x, z, level].Kind,
                                ArrayPosition = new(x, z, level)
                            };

                            physicsBalls[x, z, level] = ball;
                        }
                    }
                }
            }

            #endregion Create physical representation for each ball (without connecting them)

            for (byte level = 0; level < size.Level; level++)
            {
                for (byte x = 0; x < size.X; x++)
                {
                    for (int z = 0; z < size.Z; z++)
                    {
                        if (staticBalls[x, z, level] == null) continue; //Is there a ball?

                        PhysicsBall currentPhysicsBall = physicsBalls[x, z, level];

                        //Same level: connect only towards +X and +Z, so every pair is connected exactly once
                        if (x + 1 < size.X && physicsBalls[x + 1, z, level] != null)
                            ConnectOnSameLevel(currentPhysicsBall, physicsBalls[x + 1, z, level], simulation);
                        if (z + 1 < size.Z && physicsBalls[x, z + 1, level] != null)
                            ConnectOnSameLevel(currentPhysicsBall, physicsBalls[x, z + 1, level], simulation);

                        //Cross-level connections are created only from even (unshifted) levels: adjacent levels always
                        //differ in parity, so every cross-level pair has exactly one even endpoint and is connected exactly once
                        if ((level % 2) == 0)
                            ConnectToNeighborsOnOtherLevels(currentPhysicsBall, physicsBalls, simulation, size);

                        //Highest level - also attach to ceiling
                        if (level == size.Level - 1)
                            currentPhysicsBall.HandlesTop.TryStore(ConnectBallToCeiling(currentPhysicsBall, ceilingReference, simulation));
                    }
                }
            }

            return physicsBalls;
        }

        //The System.Numerics wrapper around BallsMap.CountOccupiedNeighbors that used to stand here is gone with
        //#76. It was the two executables' entry point into the occlusion count, and once BallRenderSet took that
        //walk over it had no callers left — but the reason to delete it rather than leave it sitting is sharper
        //than tidiness: it handed back the occluder direction as the raw SUM of unit vectors, and handing that to
        //the shader undivided is the mistake that cost the cluster its whole look once already. There is now no
        //public path to an undivided sum anywhere: BallRenderSet.OcclusionTarget is the only thing that builds
        //that vector, and it divides.

        /// <summary>
        /// Checks whether the freshly attached ball completed a cluster of at least <see cref="MINIMUM_CLUSTER_SIZE"/>
        /// touching balls of the same <see cref="BallType"/> and if so, releases the whole cluster: removes all constraints
        /// of its balls (so they fall freely) and removes the balls from both the logical map and <paramref name="physicsBalls"/>.
        /// Balls that lose their connection to the ceiling by that are then released the same way, so everything that
        /// falls, falls as individual unconstrained balls.
        /// </summary>
        /// <param name="releasedInto">Released balls are added here so the caller can keep drawing (and later dispose of) them.</param>
        /// <param name="thawedInto">Filled with the cells whose ice this release broke (#329), cleared first;
        /// left alone entirely when null, which is every caller that does not care. See the remarks on why the
        /// thaw is <b>in here</b> rather than repeated by each caller the way the glass and the bombs are.</param>
        /// <returns>
        /// The kinds of released ball, kept apart rather than summed (see <see cref="BallsReleased"/>): a
        /// scorer has to be able to tell the group the player aimed at from everything that fell because they
        /// cut its support. Zero of all of them when the cluster is below the minimum size.
        /// <para>
        /// <b>A match can now report <c>Destroyed</c> too</b> (#396), which it never could before: a bomb the
        /// release orphans goes off where it hangs, and a blast that reaches cluster still standing takes it
        /// out. Zero on every release that orphans no bomb, which is every release on all but three of the
        /// shipped levels.
        /// </para>
        /// </returns>
        /// <remarks>
        /// <b>THE THAW OF #329 LIVES HERE, and that is the one design decision in this method worth arguing.</b>
        /// A frozen ball's ice breaks when the player clears a group beside it — and this is the only place in
        /// the whole game where a group is cleared, so it is the only place the rule can be stated once. Every
        /// earlier special was collected and fired by the <i>caller</i> (the contact handler collects the armed
        /// bombs, the zaps and the acids and sets them off around this call), and the cost of that shape is on
        /// the record: <c>Tools/LevelGen/SagProbe</c> had to learn the glass colouring, then the bombs, then
        /// the zaps, then the acids, one by one, and the Mirage's five glass levels were measured wrong for a
        /// release because it had not yet learned the first of them. A rule keyed to <i>the release itself</i>
        /// has no such seam: the probe, the Testbed and the Game all thaw by calling this, and none of them had
        /// a line added.
        /// <para>
        /// It runs <b>before</b> the disconnection pass, and the order does not change any count — a kind never
        /// affects what hangs from what, so the walk below sees the same field either way. It is stated in this
        /// order because it is the honest one: the ice broke because of the match, and what falls afterwards
        /// falls because of the match too. A frozen ball that thaws and is then orphaned by the same release is
        /// reported in both, which is exactly what happened to it.
        /// </para>
        /// <para>
        /// <b>Only the matched group thaws — never the orphans</b> (#329's second ruling). A region that falls
        /// because it lost its support passes plenty of ice on the way down; if that broke ice, a thaw would be
        /// something the physics did rather than something the player aimed at, and the whole point of this
        /// kind is that it is a plan.
        /// </para>
        /// </remarks>
        public static BallsReleased ReleaseSameTypeCluster(PhysicsBall attachedBall, PhysicsBall[,,] physicsBalls,
            BallsMap map, Simulation simulation, List<PhysicsBall> releasedInto, List<XZLevel> thawedInto = null,
            List<Detonation> detonationsInto = null)
        {
            //Emptied HERE and not inside the thaw, because the early return below is the common case and a
            //caller reading a stale list would report the ice the shot BEFORE this one broke. It is the same
            //trap the armed-bomb lists avoid by clearing at the top of their own collect.
            thawedInto?.Clear();

            List<XZLevel> cluster = map.GetConnectedSameTypeCells(attachedBall.ArrayPosition);
            if (cluster.Count < MINIMUM_CLUSTER_SIZE) return default;

            XZLevel size = map.GetStaticBallsArraySize();
            List<ConstraintHandle> handleBuffer = new();

            foreach (XZLevel cell in cluster)
                ReleaseBall(cell, physicsBalls, map, simulation, size, handleBuffer, releasedInto);

            ThawFrozen(cluster, physicsBalls, map, thawedInto);

            //Balls no longer connected to the ceiling would fall as chains still constrained to each other;
            //releasing them explicitly cuts those constraints so they fall as individual balls. Since #396 the
            //walk also sets off any BOMB it finds hanging on nothing, which is why this is one shared method
            //rather than the four copies of the same foreach it used to be — see ResolveDisconnected.
            BallsReleased fell = ResolveDisconnected(null, physicsBalls, map, simulation, size, handleBuffer,
                releasedInto, detonationsInto);

            return new BallsReleased(cluster.Count, fell.Orphaned, fell.Destroyed);
        }

        /// <summary>
        /// How long a thawing ball takes to cross from ice to its own colour, in seconds (#329). The glass
        /// colouring's <c>ClusterCollector.COLOUR_FADE_SECONDS</c> exactly, and deliberately so: they are the
        /// same event seen twice — a ball stopping being one kind and becoming an ordinary one — and two
        /// different durations for that would read as two different mechanics.
        /// </summary>
        public const float THAW_FADE_SECONDS = 0.35f;

        /// <summary>
        /// <see cref="ReleaseSameTypeCluster"/>'s thaw, split out so the rule reads as one thing: the map
        /// decides <b>which</b> cells (<see cref="BallsMap.ThawFrozenBesideGroup"/>, one ring around the group),
        /// and this mirrors each of them onto the physics side and starts its crossing.
        /// <para>
        /// <b>The map and the physics array have to move together</b> — that is #323's own rule and it is not a
        /// nicety here: the flood fill reads the map and the draw reads the physics ball, so a cell thawed in
        /// one and not the other is a ball that matches but is still drawn in ice, for the rest of the level.
        /// </para>
        /// <para>
        /// The scratch list is static and reused, so a release with no ice near it allocates nothing after the
        /// first one. That is safe for the reason the rest of this class is single-threaded: a landing is
        /// resolved inside one simulation step, and there is exactly one of those at a time.
        /// </para>
        /// </summary>
        private static readonly List<XZLevel> _thawScratch = new(12);

        /// <inheritdoc cref="_thawScratch"/>
        private static void ThawFrozen(List<XZLevel> cluster, PhysicsBall[,,] physicsBalls, BallsMap map,
            List<XZLevel> thawedInto)
        {
            List<XZLevel> thawed = thawedInto ?? _thawScratch;

            if (map.ThawFrozenBesideGroup(cluster, thawed) == 0) return;

            for (int i = 0; i < thawed.Count; i++)
            {
                XZLevel at = thawed[i];
                PhysicsBall ball = physicsBalls[at.X, at.Z, at.Level];
                if (ball == null) continue;

                ball.Kind = BallKind.Normal;
                ball.ThawFadeRemaining = THAW_FADE_SECONDS;
            }
        }

        /// <summary>
        /// How long a ball takes to cross into the kind an infection tick just made it, in seconds (#331) —
        /// <see cref="THAW_FADE_SECONDS"/>'s figure for its reason: a ball stopping being one kind and becoming
        /// another is one event however it happens, and several durations for it would read as several
        /// mechanics.
        /// </summary>
        public const float INFECTION_FADE_SECONDS = 0.35f;

        /// <summary>
        /// One tick of the infection (#331), mirrored onto both sides: the map decides <b>who</b>
        /// (<see cref="BallsMap.SpreadInfection"/> — one healthy neighbour each, and every spreader hardens),
        /// and this carries each change onto the physics ball and starts its crossing.
        /// <para>
        /// <b>The map and the physics array have to move together</b>, #323's own rule and not a nicety: the
        /// flood fill reads the map and the draw reads the physics ball, so a cell sick in one and healthy in
        /// the other is a ball that matches but is drawn well for the rest of the level — or, worse for the
        /// hardening half, one the player can still match while the map says it is stone.
        /// </para>
        /// <para>
        /// <b>It is NOT called from here.</b> Every other special in this class fires off a landing and is
        /// called by the handler resolving one; a tick is not a landing, and where it sits in the shot
        /// sequence is the Game's statement to make (<c>GameplayScreen.Rules</c>) rather than a consequence of
        /// which method happened to notice. This is the rule; the caller owns the clock.
        /// </para>
        /// </summary>
        /// <param name="infected">Cells that just became sick; cleared first. May be null.</param>
        /// <param name="hardened">Cells that just became stone; cleared first. May be null.</param>
        /// <returns>How many balls hardened — what this tick cost the player, and zero on every field with no
        /// infection in it, which is all of them today.</returns>
        public static int SpreadInfection(PhysicsBall[,,] physicsBalls, BallsMap map,
            List<XZLevel> infected = null, List<XZLevel> hardened = null)
        {
            List<XZLevel> newlySick = infected ?? _infectedScratch;
            List<XZLevel> newlyStone = hardened ?? _hardenedScratch;

            if (map.SpreadInfection(newlySick, newlyStone) == 0) return 0;

            //The order is the map's own and it matters here too: a cell can appear in both lists only if one
            //tick both infected it and hardened it, which SpreadInfection makes impossible by reading the
            //population before acting. Mirroring the sick first and the stone second would still be right if
            //it ever did, since that is the order the map applied them in.
            for (int i = 0; i < newlySick.Count; i++) MirrorKind(physicsBalls, map, newlySick[i]);
            for (int i = 0; i < newlyStone.Count; i++) MirrorKind(physicsBalls, map, newlyStone[i]);

            return newlyStone.Count;
        }

        /// <inheritdoc cref="SpreadInfection"/>
        private static readonly List<XZLevel> _infectedScratch = new(16);

        /// <inheritdoc cref="SpreadInfection"/>
        private static readonly List<XZLevel> _hardenedScratch = new(16);

        /// <summary>
        /// Copies one cell's kind from the map onto its physics ball and starts the crossing. The map is the
        /// truth about what a ball IS (#323) and this is the one direction that copy ever runs.
        /// </summary>
        private static void MirrorKind(PhysicsBall[,,] physicsBalls, BallsMap map, XZLevel cell)
        {
            PhysicsBall ball = physicsBalls[cell.X, cell.Z, cell.Level];
            if (ball == null) return;

            StaticBall authored = map.GetStaticBallsArray()[cell.X, cell.Z, cell.Level];
            if (authored == null) return;

            ball.Kind = authored.Kind;
            ball.InfectFadeRemaining = INFECTION_FADE_SECONDS;
        }

        /// <summary>
        /// How far a blast reaches, <b>in world units</b> (#326). Two, which is two rings of cells sideways and
        /// — the lattice being 1/√2 apart vertically — nearly three levels up and down.
        /// <para>
        /// World units and not a cell count, deliberately, and it is the one number a level author reasons
        /// about: the lattice is anisotropic, so "two cells" means two different distances depending on which
        /// way you count, and a radius stated in the grid's own indices would be a different shape in the two
        /// axes. Stated as a distance it is a <b>sphere</b>, which is what the player sees and what the rule
        /// says. The arithmetic that turns it into candidate cells lives in <see cref="DetonateBombs"/> and
        /// nowhere else.
        /// </para>
        /// </summary>
        public const float BLAST_RADIUS = 2f;

        /// <summary>
        /// How hard a blast throws its victims, in units a second at the centre, tapering to
        /// <see cref="BLAST_EDGE_SPEED_FRACTION"/> of it at the rim.
        /// <para>
        /// The throw is the whole reason the victims <i>fall</i> rather than vanish. A ball that pops out of
        /// existence throws away the best feedback this game has — the fall, the drain, the sound, the drop
        /// cinematic all already exist — and a ball that merely starts falling reads as a ball whose support
        /// went, which is a different event and one the player already knows. It is added as a velocity rather
        /// than applied as an impulse because the body is unconstrained and awake by the time this runs, and a
        /// velocity is what the release path leaves it at anyway (zero).
        /// </para>
        /// <para>
        /// <b>⚠ Thrown from where the bomb's BODY is, in world space — and until #389 it was thrown from its grid
        /// cell.</b> <see cref="BallsMap.GetRealPosition"/> answers in the raw lattice frame, before
        /// <see cref="BallsMap.Center"/> and before the offset <see cref="BuildBallsStructure"/> places the bodies
        /// by, and on the shipped bomb levels those two frames stand (−7.5, −5.4, −7.5) apart: twelve units,
        /// against a radius of two. So every victim measured as past the rim and took the rim's speed, and every
        /// one of them took it in very nearly the same direction — measured on all nineteen bombs of Vent, Sill
        /// and Paroxysm, 2.46 u/s for every victim, a direction coherence of 0.99–1.00 and a mean outward cosine
        /// near zero. The blast was a slab of balls drifting towards one corner of the arena, which is what was
        /// reported as "it just falls". The <b>radius</b> is still measured in the grid frame, where both of its
        /// ends are: which cells a blast takes is a rule about the lattice and must not move with the swing.
        /// </para>
        /// </summary>
        private const float BLAST_SPEED = 7f;

        /// <inheritdoc cref="BLAST_SPEED"/>
        private const float BLAST_EDGE_SPEED_FRACTION = 0.35f;

        /// <summary>
        /// The slowest a blast throws the balls it <b>orphans</b>, in units a second (#389).
        /// <para>
        /// What the disconnection pass finds after a detonation used to start from rest, and a blast that orphaned
        /// much read as two events: a ring of balls thrown out and, around it, an ordinary collapse. So an orphan
        /// is thrown too, away from the <b>nearest</b> detonation of the chain, on the victims' own taper carried
        /// on past the rim — the rim's speed scaled down by how much further out the ball stands — and this is
        /// where that taper stops falling, so that a far orphan still parts from its neighbours on the way down
        /// instead of dropping as a slab. It cannot throw an orphan harder than the rim throws a victim: an orphan
        /// is outside every radius of the chain by definition, or the blast would have taken it.
        /// </para>
        /// </summary>
        private const float BLAST_ORPHAN_MIN_SPEED = 0.8f;

        /// <summary>
        /// Sets off every bomb in <paramref name="armed"/> and everything their blasts reach (#326) — <b>the
        /// game's second removal path</b>, and the one #327 (Zap) and #328 (Acid) are built on.
        /// <para>
        /// Everything the game took away before this left through <see cref="ReleaseSameTypeCluster"/>: a group
        /// of one colour, plus whatever the disconnection walk then reported. A blast's victims were never a
        /// group, so the shape here is <b>choose a set of cells by geometry, remove them, and run the
        /// disconnection pass over what is left</b>. That last step is not optional and is why this cannot be a
        /// loop at the call site: a blast that opens a hole under half the cluster orphans it, and nothing else
        /// would notice.
        /// </para>
        /// <para>
        /// <b>Since #396 the whole of it lives in <see cref="ResolveDisconnected"/></b>, which the other three
        /// removals also end in — this is that pass with a list of bombs to fire before the first walk, and
        /// they are that pass with nothing to fire. It reads as a demotion and is not one: the walk had to
        /// learn that a bomb it finds hanging on nothing goes off rather than falls, and a rule stated in four
        /// copies is a rule three of them will eventually be missing. Everything below still describes what
        /// happens to <paramref name="armed"/>; the loop, the radius, the chain and the counting are documented
        /// where they now are.
        /// </para>
        /// <para>
        /// <b>The radius is a lattice walk, not an index range.</b> Odd levels are shifted half a cell in X and
        /// Z and the levels sit 1/√2 apart, so "within two units of this ball" is not a box of indices. The
        /// index range is used only to <i>bound</i> the walk — <see cref="BLAST_RADIUS"/> cells sideways and as
        /// many levels as that distance can span — and every candidate is then measured with
        /// <see cref="BallsMap.GetRealPosition"/>, which is the one copy of where a cell actually is.
        /// </para>
        /// <para>
        /// <b>Blasts CHAIN, through a worklist rather than through recursion</b> — the issue's own warning, and
        /// the worklist is also what makes termination obvious: a bomb reached by another blast is queued and
        /// deliberately <i>not</i> destroyed as a victim, so that it still gets to go off; when it is popped it
        /// destroys itself, being inside its own radius at distance zero. The map only ever shrinks and a cell
        /// popped after it has already been destroyed is skipped, so the loop cannot revisit anything. Since
        /// #396 the disconnection walk is a <i>second</i> way onto that same worklist, and one bomb can be
        /// reached both ways — which is why the queued set, not the list, is what decides.
        /// </para>
        /// <para>
        /// The victims of one detonation are <b>collected before any of them is released</b>, because
        /// <see cref="ReleaseBall"/> empties the cell it takes and a walk that released as it went would stop
        /// seeing its own neighbours. Each detonation then runs on the field the previous one left, which is
        /// what makes a chain read as a sequence rather than as one simultaneous erasure.
        /// </para>
        /// </summary>
        /// <param name="armed">The bombs to set off — the cells beside the landing, already filtered to the
        /// ones still standing. Cells that no longer hold a bomb are skipped rather than refused.</param>
        /// <param name="releasedInto">Every destroyed and orphaned ball is added here, exactly as a match's
        /// releases are, so the caller keeps drawing them and its cleanup culls them when they settle.</param>
        /// <param name="detonationsInto">Every bomb that actually went off, appended in firing order as a
        /// <see cref="Detonation"/> — where its body was, how far down a chain it came and what it took (#389),
        /// which is what a flash, a report and a jolt answer a blast with. Left alone entirely when null, which
        /// is every caller that does not care: the sag probe has nothing to draw.
        /// <para>
        /// <b>It is also the count, because the count cannot be read off <see cref="BallsReleased"/> any
        /// more</b> (#396). Before
        /// #396 a detonation always showed up there — nothing else produced a <c>Destroyed</c> — so
        /// <c>Destroyed &gt; 0</c> was a sound test for "a bomb went off", and the contact handler's landing
        /// line used it as one. An orphan-triggered blast in the middle of a region that was already falling
        /// destroys <i>nothing</i> (see the remarks on <see cref="ResolveDisconnected"/> for why its victims
        /// stay orphans), so that test now answers no to a bomb that visibly exploded. A level whose bombs are
        /// not going off has to be able to say so in the log rather than be diagnosed from a screenshot, which
        /// is the same argument the glass (#325) and the ice (#329) made for their own counts.
        /// </para>
        /// <para>
        /// <b>⚠ It is NOT cleared here</b>, and that is the one way it differs from <c>thawedInto</c> on
        /// <see cref="ReleaseSameTypeCluster"/>. A thaw belongs to one release; a detonation belongs to one
        /// <i>landing</i>, and a landing runs all four removals in turn — any of which can now orphan a bomb.
        /// So the list accumulates across the four calls and the caller empties it once, where it empties the
        /// armed lists. (#389 had cleared it here, before #396 gave the other three removals a way to fire a
        /// bomb; a caller that reads one call on its own, like the Game's <c>detonate=</c> lever, clears it
        /// itself.)
        /// </para>
        /// </param>
        /// <returns>What the blast cost the field: no matches (a blast completes no group), the balls it
        /// destroyed by geometry, and everything the disconnection pass then found hanging on nothing.</returns>
        public static BallsReleased DetonateBombs(
            IReadOnlyList<XZLevel> armed,
            PhysicsBall[,,] physicsBalls,
            BallsMap map,
            Simulation simulation,
            List<PhysicsBall> releasedInto,
            List<Detonation> detonationsInto = null)
        {
            if (armed == null || armed.Count == 0) return default;

            return ResolveDisconnected(armed, physicsBalls, map, simulation, map.GetStaticBallsArraySize(),
                new List<ConstraintHandle>(), releasedInto, detonationsInto);
        }

        /// <summary>
        /// <b>The one pass that answers "what is hanging on nothing now?" — and, since #396, sets off any bomb
        /// the answer contains.</b> Every removal in this game ends here: a match
        /// (<see cref="ReleaseSameTypeCluster"/>), a blast (<see cref="DetonateBombs"/>), a zap
        /// (<see cref="ZapColour"/>) and a shaft (<see cref="DissolveAcids"/>) all choose a set of cells their
        /// own way, take them, and then hand the field over to this.
        /// <para>
        /// <b>It is one method because the rule has to be stated once</b>, and that is #329's lesson taken at
        /// face value rather than admired: the four callers used to carry a verbatim copy of
        /// <c>foreach (cell in GetCellsDisconnectedFromCeiling()) ReleaseBall(cell)</c>, so a rule about what a
        /// disconnected cell does would have had to be written four times — and a fifth time in
        /// <c>Tools/LevelGen/SagProbe</c>, which plays the landing itself. The thaw avoided exactly that seam by
        /// living inside the release; this does the same for the walk. <b>No caller was given a line.</b>
        /// </para>
        /// <para>
        /// <b>A BOMB IS NOT RELEASED, IT IS FIRED</b> (#396). "It is separated, so it goes off" is the rule the
        /// bomb always meant — a shot landing beside it and another bomb's radius were merely the two cases
        /// that happened to be implemented, and both are geometric. Losing the last path to the glass is the
        /// third, and it is the structural one: cut the right support and a bomb the player never aimed at
        /// still goes off. This <b>reverses</b> a rule that was stated twice in comments (the contact handler's
        /// and the sag probe's "a bomb the release orphans has already fallen and must not go off in mid-air"),
        /// so both of those were rewritten in the same change rather than left standing as a wrong "why".
        /// </para>
        /// <para>
        /// <b>Why the walk runs INSIDE the loop rather than once at the end.</b> An orphan-triggered blast can
        /// cut a support of its own, which orphans a bomb that was standing perfectly well a moment ago — so
        /// the question has to be asked again after every round of blasts. The loop ends on the first walk that
        /// finds no bomb, and what that walk found is then released as ordinary orphans.
        /// </para>
        /// <para>
        /// <b>It terminates, and the argument is the map's:</b> <paramref name="armed"/> apart, a bomb only ever
        /// enters <c>pending</c> through <c>queued</c>, which never forgets — so there can be at most one round
        /// per bomb in the field, plus the final one that finds none. The map only shrinks (a popped bomb
        /// destroys itself, being at distance zero from its own centre), so a cell can never be taken twice.
        /// </para>
        /// <para>
        /// <b>⚠ AN ORPHAN-TRIGGERED BLAST CAN ONLY EVER ADD TO WHAT A SHOT IS WORTH</b>, and that invariant is
        /// what <paramref name="armed"/>-less calls are counted against: a ball the walk had ALREADY found
        /// hanging on nothing left because its support was cut, and the blast only chose which way it flew, so
        /// it stays <see cref="BallsReleased.Orphaned"/> — at the orphan's double rate — even though a blast is
        /// what physically took it. Only what the blast takes from cluster that was still STANDING is
        /// <see cref="BallsReleased.Destroyed"/>. Counting the whole radius as destroyed would have paid a
        /// player <i>less</i> for the better-looking outcome (#326 set Destroyed at the matched rate and
        /// Orphaned at double), which is the one result this rule must not produce. The bomb itself is in that
        /// falling set too, so it is still worth exactly what it was worth before #396.
        /// </para>
        /// <para>
        /// <b>Nothing about the existing paths moved.</b> On the first round of a
        /// <see cref="DetonateBombs"/> call the falling set is empty — the walk has not run yet — so every
        /// victim of a landing-armed blast counts as destroyed exactly as it did before, and a removal that
        /// orphans no bomb at all runs one walk and one release, which is what all four callers used to do
        /// inline.
        /// </para>
        /// </summary>
        /// <param name="armed">Bombs to set off before the first walk — the cells beside a landing, for
        /// <see cref="DetonateBombs"/>. <b>Null for every other caller</b>, which is the whole difference
        /// between "a blast, then its consequences" and "just the consequences".</param>
        /// <param name="detonationsInto">Every bomb that actually went off is added here, in the order it fired.
        /// See the same parameter on <see cref="DetonateBombs"/> for what a record carries, why the count cannot
        /// be read off <see cref="BallsReleased"/> and why this list is <b>not</b> cleared here.</param>
        /// <returns>No matches (this pass completes no group), what the blasts destroyed and what fell.</returns>
        private static BallsReleased ResolveDisconnected(
            IReadOnlyList<XZLevel> armed,
            PhysicsBall[,,] physicsBalls,
            BallsMap map,
            Simulation simulation,
            XZLevel size,
            List<ConstraintHandle> handleBuffer,
            List<PhysicsBall> releasedInto,
            List<Detonation> detonationsInto)
        {
            StaticBall[,,] cells = map.GetStaticBallsArray();

            //The worklist and the set that keeps a bomb from being queued twice — two bombs whose radii cover
            //each other would otherwise put each other back on it for as long as the loop ran, and since #396
            //the walk is a second way onto it, so the set is also what stops a bomb already queued by a radius
            //from being queued again the moment it is found disconnected. Beside the worklist, how many blasts
            //it took to reach each entry (Detonation.Link, #389).
            List<XZLevel> pending = new();
            List<int> links = new();
            HashSet<int> queued = new();

            //Every point that went off, in world space: what the orphans are thrown away from once the chain has
            //run out (BLAST_ORPHAN_MIN_SPEED).
            List<Vector3> blasts = new();

            //The deepest link fired so far, or -1 before anything has gone off. A bomb the WALK fires was cut
            //loose by what went before it, so it comes one link after all of that (and at link 0 when nothing
            //has gone off yet — a match, a zap or a shaft that orphaned it is the landing's own event, exactly
            //as a bomb armed beside the landing is). This is what staggers it in the Game's chain playback.
            int deepestLink = -1;

            int Key(XZLevel cell) => (cell.Level * size.X + cell.X) * size.Z + cell.Z;

            bool IsBomb(XZLevel cell) =>
                cells[cell.X, cell.Z, cell.Level] != null
                && cells[cell.X, cell.Z, cell.Level].Kind == BallKind.Bomb;

            if (armed != null)
                foreach (XZLevel cell in armed)
                {
                    if (!IsBomb(cell) || !queued.Add(Key(cell))) continue;

                    pending.Add(cell);
                    links.Add(0);
                }

            //How far to look, in indices. Sideways the cell pitch is one, so the radius IS the reach; upwards
            //the levels sit 1/sqrt(2) apart, so the same distance spans sqrt(2) times as many of them.
            int reach = (int)MathF.Ceiling(BLAST_RADIUS);
            int reachLevels = (int)MathF.Ceiling(BLAST_RADIUS * Constants.SQRT_TWO);

            List<XZLevel> victims = new();

            //What the last walk found hanging on nothing — the cells a blast may take but must not CHARGE for,
            //see the remarks. Empty on the first round, so a landing-armed blast counts exactly as it did
            //before #396.
            HashSet<int> falling = new();

            int destroyed = 0;
            int orphaned = 0;

            //Kept outside the loop: `pending` grows in both halves of a round, and a bomb already fired must
            //not be popped a second time when the next walk adds to the list behind it.
            int next = 0;

            while (true)
            {
                for (; next < pending.Count; next++)
                {
                    XZLevel bomb = pending[next];

                    //Already gone: an earlier blast in this same chain reached it as a victim before it was
                    //popped. Not possible today, since a chained bomb is skipped as a victim - but the guard is
                    //what lets that rule change without this loop becoming a use-after-free.
                    if (!IsBomb(bomb)) continue;

                    //TWO CENTRES, ONE PER FRAME, and #389 was the two being mixed. The radius is a rule about the
                    //lattice, so it is measured between grid positions and cannot move with the cluster's swing;
                    //the throw is a velocity handed to bodies, so it is measured from the bomb's own body. See
                    //BLAST_SPEED.
                    Vector3 centre = BallsMap.GetRealPosition((byte)bomb.X, (byte)bomb.Z, (byte)bomb.Level).ToNumerics();

                    //The map and the physics array move together (#323), so a bomb still standing in the map has
                    //a body. Skipped rather than thrown from a guess if that ever stops being true: a blast
                    //centred on the wrong frame is precisely the fault this line exists to end.
                    PhysicsBall bombBall = physicsBalls[bomb.X, bomb.Z, bomb.Level];
                    if (bombBall == null) continue;

                    Vector3 blast = bombBall.BallReference.Pose.Position;
                    int link = links[next];

                    victims.Clear();

                    for (int level = bomb.Level - reachLevels; level <= bomb.Level + reachLevels; level++)
                    {
                        if (level < 0 || level >= size.Level) continue;

                        for (int x = bomb.X - reach; x <= bomb.X + reach; x++)
                        {
                            if (x < 0 || x >= size.X) continue;

                            for (int z = bomb.Z - reach; z <= bomb.Z + reach; z++)
                            {
                                if (z < 0 || z >= size.Z) continue;
                                if (cells[x, z, level] == null) continue;

                                XZLevel cell = new(x, z, level);

                                Vector3 at = BallsMap.GetRealPosition((byte)x, (byte)z, (byte)level).ToNumerics();
                                if (Vector3.DistanceSquared(at, centre) > BLAST_RADIUS * BLAST_RADIUS) continue;

                                //A bomb inside the blast is a CHAIN and not a victim: queued so it gets to go
                                //off itself, and left standing until it does. It destroys itself when it is
                                //popped, being at distance zero from its own centre.
                                if (cells[x, z, level].Kind == BallKind.Bomb && Key(cell) != Key(bomb))
                                {
                                    if (queued.Add(Key(cell)))
                                    {
                                        pending.Add(cell);
                                        links.Add(link + 1);
                                    }

                                    continue;
                                }

                                victims.Add(cell);
                            }
                        }
                    }

                    foreach (XZLevel cell in victims)
                    {
                        PhysicsBall ball = physicsBalls[cell.X, cell.Z, cell.Level];

                        //Which number it goes on is decided BEFORE the release, because ReleaseBall empties the
                        //cell and the key would then name nothing.
                        bool wasFalling = falling.Contains(Key(cell));

                        ReleaseBall(cell, physicsBalls, map, simulation, size, handleBuffer, releasedInto);

                        if (wasFalling) orphaned++;
                        else destroyed++;

                        if (ball != null) Throw(ball, blast);
                    }

                    //Recorded where it is decided that the bomb WENT OFF, past both guards above (#396's rule) —
                    //a cell queued by one round and eaten by the next one's blast before it was popped never
                    //fired, and a record of it would be a fireball over a hole. ⚠ This is also the line that
                    //gives a bomb the WALK fired its flash and its report (#389 meeting #396): without it an
                    //orphaned bomb would throw its victims in silence.
                    blasts.Add(blast);
                    deepestLink = Math.Max(deepestLink, link);
                    detonationsInto?.Add(new Detonation(blast.ToXna(), link, victims.Count));
                }

                //And the half every removal in this game shares: what was only held up by what just went takes
                //the same path down — unless it is a bomb, which goes off where it hangs instead.
                List<XZLevel> disconnected = map.GetCellsDisconnectedFromCeiling();

                falling.Clear();
                bool firedByTheWalk = false;

                foreach (XZLevel cell in disconnected)
                {
                    falling.Add(Key(cell));

                    if (IsBomb(cell) && queued.Add(Key(cell)))
                    {
                        pending.Add(cell);
                        links.Add(deepestLink + 1);
                        firedByTheWalk = true;
                    }
                }

                //Round again: the bombs this walk found go off over a field that still holds everything else it
                //found, so a blast in the middle of a falling region throws that region apart instead of
                //watching it drop. Then the walk asks once more, over whatever the blasts left.
                if (firedByTheWalk) continue;

                //What is left falls as ordinary orphans — thrown on its way from the nearest blast when anything
                //went off in this pass (#389), rather than dropped as a slab around a thrown ring.
                foreach (XZLevel cell in disconnected)
                {
                    PhysicsBall ball = physicsBalls[cell.X, cell.Z, cell.Level];

                    ReleaseBall(cell, physicsBalls, map, simulation, size, handleBuffer, releasedInto);
                    orphaned++;

                    if (ball != null && blasts.Count > 0) ThrowOrphan(ball, blasts);
                }

                return new BallsReleased(0, orphaned, destroyed);
            }
        }

        /// <summary>
        /// How hard a zap throws what it takes, in units a second (#327). Far gentler than
        /// <see cref="BLAST_SPEED"/>, and the difference is the point: a blast is a <b>place</b> that shoves
        /// everything away from it, while a zap is a rule that reaches the whole field at once and has no
        /// centre to throw from. What its victims do is <b>let go</b> — a nudge sideways off the lattice so
        /// they part before they fall, rather than a column dropping as one slab.
        /// <para>
        /// The direction is the ball's own offset from the field's vertical axis, so a wall of them opens
        /// outwards; on the axis itself it is straight down, which is the same degenerate case the blast
        /// answers the same way and for the same reason (normalising a zero vector is a NaN velocity Bepu
        /// never recovers from).
        /// </para>
        /// </summary>
        private const float ZAP_SPEED = 1.6f;

        /// <summary>
        /// Takes <b>every ordinary ball of one colour off the whole field</b> (#327) — the bomb's destruction
        /// path at the largest scale the game has.
        /// <para>
        /// <b>What is chosen differs; what happens to it does not.</b> <see cref="DetonateBombs"/> picks its
        /// victims by geometry and this picks them by colour, and after that the two are the same three steps:
        /// remove the set, throw what was removed so it falls rather than vanishing, and run the disconnection
        /// pass over what is left. That last step matters more here than anywhere else in the game — losing a
        /// whole ink can orphan most of a cluster in one frame, which is precisely the mass change #301 and
        /// #302 built the sag probe to measure.
        /// </para>
        /// <para>
        /// <b>⚠ Only <see cref="BallKinds.Matchable"/> balls go.</b> A rock, a glass ball, a bomb and another
        /// zap all carry a <see cref="BallType"/> that <b>nothing may read</b> — it is stored because every
        /// cell has the field, not because it means anything — so taking them "because they are that colour"
        /// would be acting on a field the player cannot see. It also keeps the two specials from eating each
        /// other: a zap can never remove a bomb, whatever colours they happen to hold.
        /// </para>
        /// <para>
        /// The cells are collected in one pass <b>before</b> any of them is released, for
        /// <see cref="DetonateBombs"/>' own reason: <see cref="ReleaseBall"/> empties the cell it takes, and a
        /// walk that released as it went would be reading a map it is changing.
        /// </para>
        /// </summary>
        /// <param name="zaps">The zap balls to fire — the cells beside the landing, already filtered to the
        /// ones still standing. A cell that no longer holds a zap is skipped rather than refused, and every
        /// zap fired is destroyed with the colour it takes.</param>
        /// <param name="colour">The shot's own colour, which is the whole of what a zap decides. See
        /// <see cref="BallKind.Zap"/> for why it is the shot's and not the field's.</param>
        /// <param name="releasedInto">Every destroyed and orphaned ball is added here, exactly as a match's
        /// releases are, so the caller keeps drawing them and its cleanup culls them when they settle.</param>
        /// <returns>What the zap cost the field: no matches (a zap completes no group), the balls it destroyed
        /// by colour — the zap balls themselves included — and everything the disconnection pass then found
        /// hanging on nothing.</returns>
        public static BallsReleased ZapColour(
            IReadOnlyList<XZLevel> zaps,
            BallType colour,
            PhysicsBall[,,] physicsBalls,
            BallsMap map,
            Simulation simulation,
            List<PhysicsBall> releasedInto,
            List<Detonation> detonationsInto = null)
        {
            if (zaps == null || zaps.Count == 0) return default;

            XZLevel size = map.GetStaticBallsArraySize();
            StaticBall[,,] cells = map.GetStaticBallsArray();
            List<ConstraintHandle> handleBuffer = new();

            List<XZLevel> victims = new();

            //The zaps themselves first, so a zap that has already left (orphaned by the match this landing
            //completed) takes nothing with it, and one that is still standing is destroyed by its own firing.
            bool fired = false;

            foreach (XZLevel at in zaps)
            {
                if (cells[at.X, at.Z, at.Level] == null || cells[at.X, at.Z, at.Level].Kind != BallKind.Zap) continue;

                victims.Add(at);
                fired = true;
            }

            if (!fired) return default;

            //And then the colour, over the whole field. One walk, no early exit: the point of this kind is
            //that distance does not enter into it.
            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        StaticBall ball = cells[x, z, level];

                        if (ball == null || ball.Type != colour || !BallKinds.Matchable(ball.Kind)) continue;

                        victims.Add(new XZLevel(x, z, level));
                    }

            int destroyed = 0;

            foreach (XZLevel at in victims)
            {
                PhysicsBall ball = physicsBalls[at.X, at.Z, at.Level];

                ReleaseBall(at, physicsBalls, map, simulation, size, handleBuffer, releasedInto);
                destroyed++;

                if (ball != null) Loosen(ball);
            }

            BallsReleased fell = ResolveDisconnected(null, physicsBalls, map, simulation, size, handleBuffer,
                releasedInto, detonationsInto);

            return new BallsReleased(0, fell.Orphaned, destroyed + fell.Destroyed);
        }

        /// <summary>
        /// Nudges one zapped ball off the lattice — see <see cref="ZAP_SPEED"/> for why this is a nudge and
        /// the blast's <see cref="Throw"/> is a shove.
        /// </summary>
        private static void Loosen(PhysicsBall ball)
        {
            Vector3 position = ball.BallReference.Pose.Position;
            Vector2 outward = new(position.X, position.Z);

            float distance = outward.Length();

            Vector3 direction = distance > 1e-4f
                ? new Vector3(outward.X / distance, -0.35f, outward.Y / distance)
                : new Vector3(0f, -1f, 0f);

            ball.BallReference.Velocity.Linear = Vector3.Normalize(direction) * ZAP_SPEED;
        }

        /// <summary>
        /// How hard an acid's shaft drops what it eats, in units a second (#328) — a push straight <b>down</b>
        /// rather than away from a centre, which is the whole difference between this and a blast. A shaft's
        /// contents are not thrown apart; they fall out of the hole, and the column reads as a column doing it.
        /// Gentle, on <see cref="ZAP_SPEED"/>'s reasoning: what the balls mostly do is let go.
        /// </summary>
        private const float ACID_SPEED = 1.4f;

        /// <summary>
        /// Eats <b>downward</b> from every acid the landing triggered (#328), drilling a shaft through the
        /// cluster until it reaches a gap — the bomb's destruction path with its victims chosen by a walk down
        /// the packing instead of by a radius.
        /// <para>
        /// <b>Which cells the shaft takes is <see cref="BallsMap.CollectAcidShaft"/>'s</b> and deliberately not
        /// this file's: what is underneath a cell is a question about the lattice, and it is asked of the map
        /// beside its other walks over the grid — the same split the glass keeps, where
        /// <see cref="BallsMap.ColourTransparentGroup"/> chooses and the landing applies. It also means the rule
        /// can be exercised without a simulation, which is what its own remarks carry the measurements from.
        /// What is left here is the half this file owns and the bomb already proved: remove them, drop them,
        /// run the disconnection pass over what is left.
        /// </para>
        /// <para>
        /// <b>An acid inside another acid's shaft is destroyed rather than chained</b>, which is the one place
        /// this deviates from <see cref="DetonateBombs"/>, and it costs nothing: a chained bomb matters because
        /// its blast reaches cells the first one did not, while a second acid standing in the shaft has the
        /// <i>same</i> column beneath it and would stop at the same gap. Chaining would only re-centre the hole
        /// half a cell mid-drill, moving a shaft the player watched start.
        /// </para>
        /// </summary>
        /// <param name="triggered">Cells the landing armed, collected before the release ran — re-checked here,
        /// because one the release orphaned has already fallen and must not eat anything on its way down.</param>
        /// <returns>No matches (a shaft completes no group), the balls it destroyed — the acids themselves
        /// included — and everything the disconnection pass then found hanging on nothing, which on a hanging
        /// picture is usually the larger half.</returns>
        public static BallsReleased DissolveAcids(
            IReadOnlyList<XZLevel> triggered,
            PhysicsBall[,,] physicsBalls,
            BallsMap map,
            Simulation simulation,
            List<PhysicsBall> releasedInto,
            List<Detonation> detonationsInto = null)
        {
            if (triggered == null || triggered.Count == 0) return default;

            XZLevel size = map.GetStaticBallsArraySize();
            StaticBall[,,] cells = map.GetStaticBallsArray();
            List<ConstraintHandle> handleBuffer = new();
            List<XZLevel> shaft = new();

            int destroyed = 0;

            foreach (XZLevel acid in triggered)
            {
                if (cells[acid.X, acid.Z, acid.Level] == null
                    || cells[acid.X, acid.Z, acid.Level].Kind != BallKind.Acid) continue;

                map.CollectAcidShaft(acid, shaft);

                foreach (XZLevel cell in shaft)
                {
                    PhysicsBall ball = physicsBalls[cell.X, cell.Z, cell.Level];

                    ReleaseBall(cell, physicsBalls, map, simulation, size, handleBuffer, releasedInto);
                    destroyed++;

                    if (ball != null) ball.BallReference.Velocity.Linear += new Vector3(0f, -ACID_SPEED, 0f);
                }
            }

            //And the half every removal in this game shares: what was only held up by what just went takes the
            //same path down. On a shaft this is usually the larger number of the two.
            BallsReleased fell = ResolveDisconnected(null, physicsBalls, map, simulation, size, handleBuffer,
                releasedInto, detonationsInto);

            return new BallsReleased(0, fell.Orphaned, destroyed + fell.Destroyed);
        }

        /// <summary>
        /// Throws one freed ball away from <paramref name="centre"/> — the bomb's <b>body</b>, in world space. See
        /// <see cref="BLAST_SPEED"/> for why a blast's victims are thrown rather than merely dropped, and for what
        /// a centre in any other frame did.
        /// <para>
        /// The bomb itself is at distance zero and has no outward direction to take, so it goes <b>down</b>:
        /// the thing that exploded drops out of the hole it made, which is both the only defined answer and
        /// the one that reads.
        /// </para>
        /// </summary>
        private static void Throw(PhysicsBall ball, Vector3 centre)
        {
            Vector3 delta = ball.BallReference.Pose.Position - centre;
            float distance = delta.Length();

            float speed = BLAST_SPEED * (distance >= BLAST_RADIUS
                ? BLAST_EDGE_SPEED_FRACTION
                : 1f - (1f - BLAST_EDGE_SPEED_FRACTION) * (distance / BLAST_RADIUS));

            //A hair off zero rather than exactly zero: the bomb's own body sits at the centre, and normalising
            //a zero vector is a NaN velocity, which Bepu carries straight into the pose and never recovers from.
            Vector3 direction = distance > 1e-4f ? delta / distance : new Vector3(0f, -1f, 0f);

            ball.BallReference.Velocity.Linear += direction * speed;
        }

        /// <summary>
        /// Throws one ball a blast <b>orphaned</b> away from the nearest of <paramref name="blasts"/> — see
        /// <see cref="BLAST_ORPHAN_MIN_SPEED"/> for why it is thrown at all.
        /// <para>
        /// The nearest detonation and not the chain's centroid: a chain can run several radii across a cluster,
        /// and a ball hanging off its far end was cut loose by the bomb beside it, not by an average of five.
        /// </para>
        /// </summary>
        private static void ThrowOrphan(PhysicsBall ball, List<Vector3> blasts)
        {
            Vector3 position = ball.BallReference.Pose.Position;

            Vector3 nearest = blasts[0];
            float nearestSquared = Vector3.DistanceSquared(position, nearest);

            for (int i = 1; i < blasts.Count; i++)
            {
                float squared = Vector3.DistanceSquared(position, blasts[i]);
                if (squared >= nearestSquared) continue;

                nearestSquared = squared;
                nearest = blasts[i];
            }

            float distance = MathF.Sqrt(nearestSquared);

            //The rim's own speed, falling off as one over the distance beyond it — so an orphan standing just
            //outside the radius leaves at the speed the outermost victim did, and the two read as one shove.
            float rim = BLAST_SPEED * BLAST_EDGE_SPEED_FRACTION;
            float speed = MathF.Max(BLAST_ORPHAN_MIN_SPEED, rim * BLAST_RADIUS / MathF.Max(distance, BLAST_RADIUS));

            //Throw's guard, kept for Throw's reason, although an orphan can never stand on a centre.
            Vector3 direction = distance > 1e-4f ? (position - nearest) / distance : new Vector3(0f, -1f, 0f);

            ball.BallReference.Velocity.Linear += direction * speed;
        }

        /// <summary>
        /// Releases every ball of the structure at once (the End debug action): all constraints are removed
        /// and the balls move out of the map and <paramref name="physicsBalls"/> into <paramref name="releasedInto"/>,
        /// so the caller keeps drawing them and its fallen-ball cleanup can cull them once they come to rest.
        /// (Merely removing the constraints while leaving the balls in <paramref name="physicsBalls"/> kept the
        /// pile on the ground alive forever, generating contact constraints.)
        /// </summary>
        /// <returns>Number of released balls.</returns>
        public static int ReleaseAllBalls(PhysicsBall[,,] physicsBalls, BallsMap map, Simulation simulation, List<PhysicsBall> releasedInto)
        {
            XZLevel size = map.GetStaticBallsArraySize();
            List<ConstraintHandle> handleBuffer = new();
            int released = 0;

            for (byte level = 0; level < size.Level; level++)
                for (byte x = 0; x < size.X; x++)
                    for (byte z = 0; z < size.Z; z++)
                    {
                        if (physicsBalls[x, z, level] == null) continue;

                        ReleaseBall(new XZLevel(x, z, level), physicsBalls, map, simulation, size, handleBuffer, releasedInto);
                        released++;
                    }

            return released;
        }

        /// <summary>
        /// Releases a single ball from the structure: removes all its constraints, clears their handles from the
        /// neighboring balls' slots (a stale value could alias a different constraint once the solver reuses the index),
        /// wakes the body up so it starts falling and removes the ball from the logical map and <paramref name="physicsBalls"/>.
        /// </summary>
        private static void ReleaseBall(
            XZLevel cell,
            PhysicsBall[,,] physicsBalls,
            BallsMap map,
            Simulation simulation,
            XZLevel size,
            List<ConstraintHandle> handleBuffer,
            List<PhysicsBall> releasedInto)
        {
            PhysicsBall ball = physicsBalls[cell.X, cell.Z, cell.Level];
            if (ball == null) return;

            handleBuffer.Clear();
            ball.CollectConstraintHandles(handleBuffer);
            ball.RemoveAllConstraints(simulation);

            foreach (XZLevel neighborCell in BallsMap.GetNeighboringCells(cell, size))
            {
                PhysicsBall neighbor = physicsBalls[neighborCell.X, neighborCell.Z, neighborCell.Level];
                if (neighbor == null) continue;

                foreach (ConstraintHandle handle in handleBuffer) neighbor.ClearStoredHandle(handle);
            }

            simulation.Awakener.AwakenBody(ball.BallReference.Handle); //Make sure the released ball starts falling even if it was asleep

            physicsBalls[cell.X, cell.Z, cell.Level] = null;
            map.RemoveBallAt((byte)cell.X, (byte)cell.Z, (byte)cell.Level);

            releasedInto?.Add(ball);
        }

        /// <summary>
        /// Attaches a freshly placed ball to everything it should be connected to: the ceiling (when on the top level),
        /// neighbors on the same level and neighbors on the levels directly above and below.
        /// The ball must already have its <see cref="PhysicsBall.ArrayPosition"/> set, be present in the static map and in <paramref name="physicsBalls"/>.
        /// </summary>
        public static void AttachBallToStructure(PhysicsBall physicsBall, PhysicsBall[,,] physicsBalls, BallsMap map, Simulation simulation, BodyReference ceilingReference)
        {
            XZLevel size = map.GetStaticBallsArraySize();

            if (physicsBall.ArrayPosition.Level == size.Level - 1)
                physicsBall.HandlesTop.TryStore(ConnectBallToCeiling(physicsBall, ceilingReference, simulation, map.GetRealCenteredPosition(physicsBall.ArrayPosition).ToNumerics()));

            ConnectToNeighborsOnSameLevel(physicsBall, physicsBalls, simulation, size, map);
            ConnectToNeighborsOnOtherLevels(physicsBall, physicsBalls, simulation, size, map);
        }

        /// <summary>
        /// Connects a ball to the occupied neighboring cells on its own level in all four directions.
        /// Meant for a freshly attached ball, which has no same-level constraints yet, so every neighbor needs a new constraint.
        /// (The build-time pass instead connects only towards +X/+Z from each ball so pairs are not visited twice.)
        /// </summary>
        public static void ConnectToNeighborsOnSameLevel(PhysicsBall physicsBall, PhysicsBall[,,] physicsBalls, Simulation simulation, XZLevel size, BallsMap map = null)
        {
            XZLevel position = physicsBall.ArrayPosition;

            if (position.X - 1 >= 0 && physicsBalls[position.X - 1, position.Z, position.Level] != null)
                ConnectOnSameLevel(physicsBall, physicsBalls[position.X - 1, position.Z, position.Level], simulation, map);

            if (position.X + 1 < size.X && physicsBalls[position.X + 1, position.Z, position.Level] != null)
                ConnectOnSameLevel(physicsBall, physicsBalls[position.X + 1, position.Z, position.Level], simulation, map);

            if (position.Z - 1 >= 0 && physicsBalls[position.X, position.Z - 1, position.Level] != null)
                ConnectOnSameLevel(physicsBall, physicsBalls[position.X, position.Z - 1, position.Level], simulation, map);

            if (position.Z + 1 < size.Z && physicsBalls[position.X, position.Z + 1, position.Level] != null)
                ConnectOnSameLevel(physicsBall, physicsBalls[position.X, position.Z + 1, position.Level], simulation, map);
        }

        /// <summary>
        /// Creates a constraint between two balls on the same level and stores its handle on both of them.
        /// </summary>
        private static void ConnectOnSameLevel(PhysicsBall ballA, PhysicsBall ballB, Simulation simulation, BallsMap map = null)
        {
            ConstraintHandle handle = ConnectBalls(ballA, ballB, simulation, map);

            ballA.HandlesMiddle.TryStore(handle);
            ballB.HandlesMiddle.TryStore(handle);
        }

        /// <summary>
        /// Connects a ball to the occupied neighboring cells on the levels directly above and below.
        /// Takes level parity into account: odd levels are shifted by +0.5 in X and Z, so their neighbors on adjacent
        /// levels sit towards +X/+Z indices, while even levels neighbor towards -X/-Z.
        /// Used both by the build-time pass (from even levels only, so every cross-level pair is visited exactly once)
        /// and when attaching a freshly shot ball (which has no constraints yet).
        /// </summary>
        public static void ConnectToNeighborsOnOtherLevels(PhysicsBall physicsBall, PhysicsBall[,,] physicsBalls, Simulation simulation, XZLevel size, BallsMap map = null)
        {
            XZLevel position = physicsBall.ArrayPosition;
            int diagonalShift = (position.Level % 2) > 0 ? 0 : -1;

            for (int levelOffset = -1; levelOffset <= 1; levelOffset += 2)
            {
                int level = position.Level + levelOffset;
                if (level < 0 || level >= size.Level) continue;

                for (int dX = 0; dX <= 1; dX++)
                {
                    for (int dZ = 0; dZ <= 1; dZ++)
                    {
                        int x = position.X + dX + diagonalShift;
                        int z = position.Z + dZ + diagonalShift;

                        if (x < 0 || z < 0 || x >= size.X || z >= size.Z) continue;

                        PhysicsBall neighbor = physicsBalls[x, z, level];
                        if (neighbor == null) continue;

                        ConstraintHandle handle = ConnectBalls(physicsBall, neighbor, simulation, map);

                        //Constraints to balls below are stored in HandlesBottom, to balls above in HandlesTop, on both sides
                        if (levelOffset < 0)
                        {
                            physicsBall.HandlesBottom.TryStore(handle);
                            neighbor.HandlesTop.TryStore(handle);
                        }
                        else
                        {
                            physicsBall.HandlesTop.TryStore(handle);
                            neighbor.HandlesBottom.TryStore(handle);
                        }
                    }
                }
            }
        }


        private static ConstraintHandle ConnectBalls(
            PhysicsBall physicsBallA,
            PhysicsBall physicsBallB,
            Simulation simulation,
            BallsMap map)
        {
            Vector3 ballAPosition = map == null ? physicsBallA.BallReference.Pose.Position : map.GetRealCenteredPosition(physicsBallA.ArrayPosition).ToNumerics();
            Vector3 ballBPosition = map == null ? physicsBallB.BallReference.Pose.Position : map.GetRealCenteredPosition(physicsBallB.ArrayPosition).ToNumerics();

            //The constraint anchor sits halfway between the (ideal) positions of both balls
            Vector3 anchor = (ballAPosition + ballBPosition) / 2;

            BallSocket ballSocket = new()
            {
                LocalOffsetA = WorldToLocalOffset(physicsBallA.BallReference.Pose.Orientation, anchor - ballAPosition),
                LocalOffsetB = WorldToLocalOffset(physicsBallB.BallReference.Pose.Orientation, anchor - ballBPosition),
                SpringSettings = SPRING_SETTINGS
            };

            return simulation.Solver.Add(physicsBallA.BallReference.Handle, physicsBallB.BallReference.Handle, ballSocket);
        }

        private static ConstraintHandle ConnectBallToCeiling(PhysicsBall physicsBall, BodyReference ceilingReference, Simulation simulation)
        {
            return ConnectBallToCeiling(physicsBall, ceilingReference, simulation, physicsBall.BallReference.Pose.Position);
        }

        /// <summary>
        /// World Y a ball held by <see cref="ConnectBallToCeiling"/> comes to rest at, given the plate's centre Y.
        /// The anchor pair below is what decides it: the ball's own top (local <c>+BALL_RADIUS</c>) is tied to a
        /// point <c>BALL_RADIUS</c> under the plate's <i>centre</i> — under its centre and not its underside, so
        /// the plate's thickness has no say — which leaves the ball's centre a whole diameter below that centre.
        /// <para>
        /// Exposed because it is the only way to know where the top level of a hanging cluster actually is without
        /// asking a body. A caller that walks the plate down over a level (the Game's descending ceiling) needs it
        /// to place anything at a top-level cell: the lattice does not move with the glass, and the structure does.
        /// </para>
        /// </summary>
        public static float CeilingRestY(float ceilingCentreY) => ceilingCentreY - 2f * BALL_RADIUS;

        public static ConstraintHandle ConnectBallToCeiling(PhysicsBall physicsBall, BodyReference ceilingReference, Simulation simulation, Vector3 ceilingPosition)
        {
            Vector3 offsetBall = WorldToLocalOffset(physicsBall.BallReference.Pose.Orientation, new Vector3(0f, BALL_RADIUS, 0f));
            Vector3 offsetCeiling = WorldToLocalOffset(ceilingReference.Pose.Orientation, new Vector3(ceilingPosition.X, -BALL_RADIUS, ceilingPosition.Z));

            BallSocket ballSocket = new()
            {
                LocalOffsetA = offsetBall,
                LocalOffsetB = offsetCeiling,
                SpringSettings = SPRING_SETTINGS
            };

            return simulation.Solver.Add(physicsBall.BallReference.Handle, ceilingReference.Handle, ballSocket);
        }

        /// <summary>
        /// Rotates a world-space anchor offset into the body's local space. <see cref="BallSocket"/> offsets are local to the body,
        /// so a world-space offset is only usable directly while the body still has identity orientation (before the simulation has run).
        /// </summary>
        private static Vector3 WorldToLocalOffset(Quaternion orientation, Vector3 worldOffset)
        {
            return Vector3.Transform(worldOffset, Quaternion.Conjugate(orientation));
        }
    }
}

// Level is built from left back to right front
// 1 2 3
// 4 5 6
// 7 8 9
