using BepuPhysics;
using BepuPhysics.Collidables;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>The gate that hangs the level instead of reading it</b> — the answer to #301 and #302, and to the
    /// third recurrence of one fault.
    /// <para>
    /// Everything else in this tool reads the layout <i>as authored</i>: nothing floats, no colour's best shot
    /// takes the cluster, no ball stands alone, one free column all round. All four are true of a level that
    /// cannot be finished, because none of them knows what the remainder <b>weighs</b>. Three separate
    /// findings had to be made by hanging a level in the running game and watching it — the Ziggurat's ring
    /// thickness, Garland's strand (#182), the Trellis's pitch (#253) and the Bolt's elbow plates — and each
    /// time the note written down was the same sentence: <i>the gate that says the links exist cannot say
    /// there are enough of them to carry the weight</i>. #301 and #302 are that sentence a step later still,
    /// after a few shots have removed part of the mass, on nine shipped levels at once.
    /// </para>
    /// <para>
    /// <b>So this one does not read the layout at all.</b> It stands up the very simulation the game does —
    /// <see cref="PhysicsWorld"/>, the kinematic glass at <see cref="CeilingPlate.CentreYAbove"/>, the island's
    /// funnel floor, <see cref="BallsConstraintsBuilder.BuildBallsStructure"/> — hangs the level where
    /// <see cref="ClusterHang.FitWorldOffset"/> hangs it, and then <b>plays it</b>: a group is released, the
    /// world is stepped while the group falls and the remainder re-hangs, and the death line is asked the
    /// game's own question through the game's own <see cref="ClusterLineWatch"/>. It has no graphics device
    /// and needs none; the simulation was never the part that drew anything.
    /// </para>
    /// <para>
    /// <b>⚠ Why a playthrough and not a per-group drop test.</b> The obvious cheaper gate is to clear one
    /// plausible group, hang the remainder and look — and it would have missed the reports. The owner's words
    /// are <i>"as soon as a part of the map has been shot away"</i> and <i>"the player always loses after a few
    /// shots"</i>: the sag is cumulative, each cut leaving the next remainder thinner, and a level whose first
    /// cut is harmless can hinge on its third. So the probe removes groups <b>in sequence</b> and keeps
    /// stepping, which is the same shape as a game and costs the same as one run rather than one run a group.
    /// </para>
    /// <para>
    /// <b>What it is not.</b> It is not a player. It picks its groups at random from what is standing rather
    /// than by any notion of a good shot, so it says nothing about whether a level is <i>fun</i> or whether a
    /// budget is generous — <c>Tools/ScoreSim</c> is where that question lives. What it says is narrower and
    /// is exactly what was missing: <b>whether a level can be taken apart without the rest of it sagging into
    /// the drain</b>. And it under-reports rather than over-reports, which is the right way round for a gate
    /// that will be run over a shipped pack: a real player's shot also <i>adds</i> a ball to the cluster and
    /// can leave a group half-cleared, and neither is modelled here.
    /// </para>
    /// </summary>
    internal static class SagProbe
    {
        /// <summary>
        /// The step the game itself spends its frame time in — <c>GameplayScreen.PHYSICS_TIMESTEP</c>, 1/120 s.
        /// Stated rather than borrowed because that constant is private to the screen, and repeated here
        /// deliberately with the reason: a probe stepping a different dt is simulating a different world, and
        /// the whole worth of this file is that it is not.
        /// </summary>
        private const float TIMESTEP = 1f / 120f;

        /// <summary>
        /// What this probe does inside a step, which is nothing: it fires no shots, so there are no contacts
        /// to resolve. <see cref="PhysicsWorld.Step"/> calls the work unconditionally — that is the contract
        /// it exists to enforce, since a queued contact describes a world the following step has moved on
        /// from — so it is given a delegate rather than being taught to accept null, and the delegate is
        /// built once because it is called some six thousand times a run.
        /// </summary>
        private static readonly Action NO_CONTACT_WORK = () => { };

        /// <summary>
        /// How long the untouched cluster is hung before a shot is taken. It reproduces the check that has so
        /// far been made by hand — <i>"35 s unshot in the running game"</i> — and it is shorter than that
        /// because every failure ever found this way arrived fast: the temple <b>lost itself in eight
        /// seconds</b>, the Bolt at two levels of elbow plate in <b>1.1 s with no shot fired</b>, the Trellis at
        /// the faster pitch in ten. A structure that is going to stretch has stretched by four seconds; what
        /// happens over the next thirty is the swing the game already forgives.
        /// </summary>
        private const float SETTLE_SECONDS = 4f;

        /// <summary>
        /// How long the world runs after each group is released, i.e. how fast this player shoots. It has to be
        /// past <see cref="ClusterHang.BELOW_LINE_GRACE"/> or a held crossing could never be seen at all — the
        /// probe would step past the very verdict it exists to collect — and past the fall itself, so the
        /// remainder has re-hung rather than merely started to.
        /// </summary>
        private const float SHOT_SECONDS = 1.6f;

        /// <summary>
        /// How many differently-ordered runs each level gets. The order matters and that is the point: a
        /// spiral cut from the top leaves a different remainder than the same spiral cut from the middle, and
        /// one run says nothing about whether a level has a losing order in it. Three is what a run over the
        /// pack showed to be worth paying for — the fourth and fifth seeds found nothing the first three had
        /// not — and every seed is fixed, so two runs of this tool on the same pack print the same verdicts.
        /// </summary>
        private const int RUNS_PER_LEVEL = 3;

        /// <summary>
        /// The ceiling's descent, mirrored off <c>GameplayScreen</c>'s pair — the glass drops this far every
        /// <c>CeilingStep</c> shots and drags the cluster with it, and it slides rather than teleporting
        /// because a hundred constrained bodies jerked in one write can throw the solver.
        /// <para>
        /// <b>It is here even though the owner ruled it out of both reports</b> (<i>"the rate the ceiling
        /// descends is not it at all"</i>) — precisely so that ruling can be <i>checked</i> rather than
        /// assumed. A run reports which of the two pressures ended it, so a loss at shot three with the glass
        /// still at rest is visibly not a ceiling problem, and a loss after the budget's tenth descent visibly
        /// is. #288 was closed by moving this figure on five levels that were losing to the other one.
        /// </para>
        /// </summary>
        private const float CEILING_DESCENT_PER_STEP = 0.6f;

        /// <inheritdoc cref="CEILING_DESCENT_PER_STEP"/>
        private const float CEILING_DESCENT_SPEED = 1.5f;

        /// <summary>How a run ended.</summary>
        internal enum Outcome
        {
            /// <summary>Every ball came down inside the budget. What a level is supposed to do.</summary>
            Cleared,

            /// <summary>
            /// The cluster reached the death line — the failure both reports are about. The one verdict this
            /// gate refuses a level for.
            /// </summary>
            Sagged,

            /// <summary>
            /// The budget ran out with balls still hanging. <b>Not a gate failure</b>: this probe shoots at
            /// random and a random player is worse than the one the budget is priced for, so its running out
            /// says nothing. It is printed because a level whose every run ends this way is worth a look.
            /// </summary>
            OutOfShots,
        }

        /// <summary>What one simulated run of one level did.</summary>
        internal readonly struct Run(Outcome outcome, int shots, float worstClearance, float clearanceAtEnd, bool ceilingHadMoved)
        {
            public Outcome Outcome { get; } = outcome;

            /// <summary>Groups released before it ended.</summary>
            public int Shots { get; } = shots;

            /// <summary>
            /// The least the lowest ball ever cleared the death line by — <b>the number this whole file
            /// exists to print</b>. Negative means it went under, which is not yet a loss (see
            /// <see cref="ClusterHang.SWING_ALLOWANCE"/>), and a level whose worst run leaves under a unit is
            /// one shot away from the report even if it survived this one.
            /// </summary>
            public float WorstClearance { get; } = worstClearance;

            /// <summary>Where the clearance stood when the run ended, so a slow sag reads differently from a swing.</summary>
            public float ClearanceAtEnd { get; } = clearanceAtEnd;

            /// <summary>
            /// Whether the glass had taken a step by the time it ended — the one bit that separates "this
            /// level sags on its own" from "the ceiling brought it down", which is the distinction #288 got
            /// the wrong way round.
            /// </summary>
            public bool CeilingHadMoved { get; } = ceilingHadMoved;
        }

        /// <summary>
        /// Hangs the level at <paramref name="path"/> and plays it <see cref="RUNS_PER_LEVEL"/> ways.
        /// </summary>
        /// <param name="shots">The design's budget, so the probe stops where the level would.</param>
        /// <param name="ceilingStep">Shots between the glass's steps, as the level file's own entry states it.</param>
        /// <param name="trace">
        /// Print a line per shot — how many balls came down, what was left standing, and where the line
        /// stood. A verdict alone says a level sags; the trace says <b>at which cut</b>, which is the only
        /// form of the answer an author can act on, and it is what separates a body that hinges from a
        /// remnant that was going to swing whatever the design did.
        /// </param>
        internal static Run[] Play(string path, int shots, int ceilingStep, bool trace = false)
        {
            Run[] runs = new Run[RUNS_PER_LEVEL];

            for (int run = 0; run < RUNS_PER_LEVEL; run++)
            {
                //Seeded off the run alone and not off the clock: the pack's verdicts have to be reproducible,
                //or a level that fails once and passes the next time reads as a flaky gate rather than as a
                //level with a losing order in it.
                if (trace) Console.WriteLine($"      --- run {run + 1} ---");

                runs[run] = PlayOnce(path, shots, ceilingStep, new Random(run + 1), trace);
            }

            return runs;
        }

        private static Run PlayOnce(string path, int shots, int ceilingStep, Random random, bool trace)
        {
            Level level = Level.Load(path);
            BallsMap map = new(level.Map);
            map.Center();

            System.Numerics.Vector3 worldOffset = ClusterHang.FitWorldOffset(map, out float fieldTopY).ToNumerics();

            using PhysicsWorld world = new();

            //The glass, exactly as GameplayScreen.BuildCeilingBody makes it: kinematic rather than static
            //because a BallSocket needs a body at both ends, and sized off CeilingPlate's own footprint so the
            //plate the cluster hangs from is the plate the game hangs it from.
            float ceilingY = CeilingPlate.CentreYAbove(fieldTopY);
            Box box = new(CeilingPlate.FootprintFor(map.StageSizeX), CeilingPlate.THICKNESS,
                CeilingPlate.FootprintFor(map.StageSizeZ));
            BodyHandle ceilingHandle = world.Simulation.Bodies.Add(BodyDescription.CreateKinematic(
                new System.Numerics.Vector3(0f, ceilingY, 0f),
                new CollidableDescription(world.Simulation.Shapes.Add(box), 0.1f),
                new BodyActivityDescription(PhysicsWorld.SLEEP_THRESHOLD)));
            BodyReference ceiling = new(ceilingHandle, world.Simulation.Bodies);

            //The island's floor. A cluster that has sagged this far has already lost, so nothing here turns on
            //it — but a released group landing on stone instead of falling through the world is what the game
            //does, and a probe that let its debris fall forever would be simulating a lighter island.
            FunnelPhysics.Build(world.Simulation, world.BufferPool, ArenaIsland.TOP_Y, ArenaIsland.FUNNEL_BOTTOM_Y,
                ArenaIsland.FUNNEL_TOP_RADIUS, ArenaIsland.FUNNEL_HOLE_RADIUS, ArenaIsland.FLOOR_RADIUS,
                ArenaIsland.DISH_DEPTH, ArenaIsland.FUNNEL_SEGMENTS);

            PhysicsBall[,,] balls = BallsConstraintsBuilder.BuildBallsStructure(
                map.GetStaticBallsArray(), world.Simulation, ceiling, worldOffset);

            //Released balls are collected so they can be culled once they are past the island: left in the
            //simulation they pile up in the drain and go on generating contact constraints for the rest of the
            //run, which is the very leak the Testbed's ReleaseAllBalls doc records.
            List<PhysicsBall> falling = new();

            ClusterLineWatch watch = new();
            float worstClearance = float.MaxValue;
            float ceilingTargetY = ceilingY;
            int shotsFired = 0;

            //The untouched cluster first. Everything found by hand so far failed here.
            Outcome? outcome = Advance(world, ceiling, balls, falling, ref watch, ref worstClearance,
                ref ceilingY, ceilingTargetY, SETTLE_SECONDS);

            while (outcome == null)
            {
                if (map.GetBallsCount() == 0) { outcome = Outcome.Cleared; break; }
                if (shotsFired >= shots) { outcome = Outcome.OutOfShots; break; }

                XZLevel? target = PickGroup(map, random);

                //Nothing left big enough to match. A real player would still have shots to place and could
                //build a group up; this probe cannot, so the run simply ends and says so.
                if (target == null) { outcome = Outcome.OutOfShots; break; }

                //⚠ WAKE THE CLUSTER FIRST, and this is the one divergence from the game that a probe HAS to
                //repair by hand rather than inherit. In the game a group is only ever released from inside the
                //contact handler, i.e. on the frame a shot ball has just touched the structure - and a contact
                //wakes the sleeping set it lands in, so the cluster is always awake by the time its
                //constraints are cut. This probe fires nothing, so after SETTLE_SECONDS of quiet Bepu has put
                //the whole thing to sleep (both the balls and the plate carry PhysicsWorld.SLEEP_THRESHOLD),
                //and cutting constraints out of a sleeping island leaves the survivors holding a graph the
                //solver never re-examined: the remainder came apart and fell, and it read as a sag on levels
                //nobody has ever failed to finish. Waking one ball is enough - the structure is a single
                //connected constraint graph and its whole island comes up with whichever member is touched -
                //and the plate is woken beside it for the reason GameplayScreen.WakeForDescent gives, that a
                //kinematic can be referenced by several sleeping sets at once and is not islanded with them.
                world.Simulation.Awakener.AwakenBody(ceiling.Handle);
                WakeCluster(world, balls);

                BallsReleased released = BallsConstraintsBuilder.ReleaseSameTypeCluster(
                    balls[target.Value.X, target.Value.Z, target.Value.Level], balls, map, world.Simulation, falling);

                shotsFired++;

                //The glass steps on the same cadence the level file states, and the step is a slide rather than
                //a write: see CEILING_DESCENT_PER_STEP. Clamped at the line so an overlong run cannot drive the
                //plate through the gun.
                if (ceilingStep > 0 && shotsFired % ceilingStep == 0)
                    ceilingTargetY = MathF.Max(ClusterHang.DEATH_Y, ceilingTargetY - CEILING_DESCENT_PER_STEP);

                outcome = Advance(world, ceiling, balls, falling, ref watch, ref worstClearance,
                    ref ceilingY, ceilingTargetY, SHOT_SECONDS);

                if (!trace) continue;

                float lowest = LowestBallY(balls, out XZLevel lowestCell);
                int under = CountUnderLine(balls);

                Console.WriteLine($"      shot {shotsFired,3}: {released.Matched,4} matched"
                    + $" + {released.Orphaned,4} orphaned, {map.GetBallsCount(),4} left standing, line"
                    + (lowest == float.MaxValue ? "      -" : $" {lowest - ClusterHang.DEATH_Y,6:F2}")
                    + $", {under,4} under it, lowest cell {lowestCell.X},{lowestCell.Z},{lowestCell.Level}"
                    + (outcome == Outcome.Sagged ? "  <-- SAGGED HERE" : string.Empty));
            }

            float clearanceAtEnd = LowestBallY(balls) - ClusterHang.DEATH_Y;

            //A cleared field has no lowest ball, so its "clearance" would be a sentinel rather than a figure.
            if (map.GetBallsCount() == 0) clearanceAtEnd = float.NaN;
            if (worstClearance == float.MaxValue) worstClearance = float.NaN;

            return new Run(outcome ?? Outcome.OutOfShots, shotsFired, worstClearance, clearanceAtEnd,
                ceilingTargetY < CeilingPlate.CentreYAbove(fieldTopY));
        }

        /// <summary>
        /// Steps the world for <paramref name="seconds"/>, sliding the glass towards its target and asking the
        /// death line the game's question every step. Returns the verdict that ended it, or null if it survived.
        /// </summary>
        /// <remarks>
        /// The work handed to <see cref="PhysicsWorld.Step"/> is <see cref="NO_CONTACT_WORK"/>: this probe
        /// fires nothing, so there are no shot contacts to resolve, and the cluster's own contacts are the
        /// solver's business. That is the one place it deliberately does less than a frame of the game.
        /// </remarks>
        private static Outcome? Advance(PhysicsWorld world, BodyReference ceiling, PhysicsBall[,,] balls,
            List<PhysicsBall> falling, ref ClusterLineWatch watch, ref float worstClearance,
            ref float ceilingY, float ceilingTargetY, float seconds)
        {
            int steps = (int)MathF.Round(seconds / TIMESTEP);

            for (int step = 0; step < steps; step++)
            {
                if (ceilingY > ceilingTargetY)
                {
                    ceilingY = MathF.Max(ceilingTargetY, ceilingY - CEILING_DESCENT_SPEED * TIMESTEP);

                    //A sleeping body does not answer a kinematic move (#78): Bepu will not drag a sleeping
                    //cluster down because a kinematic's pose was overwritten under it, and the cluster is
                    //DESIGNED to fall asleep between shots. Both are woken for the same reason the game wakes
                    //both - a kinematic can be referenced by several sleeping sets at once.
                    world.Simulation.Awakener.AwakenBody(ceiling.Handle);
                    WakeCluster(world, balls);

                    ceiling.Pose.Position.Y = ceilingY;
                }

                world.Step(TIMESTEP, NO_CONTACT_WORK);

                CullFallen(world, falling);

                float lowest = LowestBallY(balls);

                //An emptied cluster has nothing to measure and nothing left to lose with.
                if (lowest == float.MaxValue) return null;

                float clearance = lowest - ClusterHang.DEATH_Y;
                if (clearance < worstClearance) worstClearance = clearance;

                if (watch.Update(lowest, TIMESTEP) != ClusterLineVerdict.Alive) return Outcome.Sagged;
            }

            return null;
        }

        /// <summary>
        /// The lowest ball still <b>in the structure</b> — the walk <c>GameplayScreen.CheckLevelLost</c> makes,
        /// over the same array and with the same null check for cells a release has emptied. Released balls are
        /// deliberately not in it: a group falling into the drain is not the cluster arriving at the line.
        /// </summary>
        private static float LowestBallY(PhysicsBall[,,] balls) => LowestBallY(balls, out _);

        /// <inheritdoc cref="LowestBallY(PhysicsBall[,,])"/>
        /// <param name="cell">
        /// Which cell it was — the trace's most useful column by a distance, because it says <b>what part of
        /// the design</b> is going under. A cell high in the layout is a body hinging; a cell out at the rim
        /// is an appendage swinging on too few links, and the two want opposite fixes.
        /// </param>
        private static float LowestBallY(PhysicsBall[,,] balls, out XZLevel cell)
        {
            XZLevel size = XZLevel.FromArray(balls);
            float lowest = float.MaxValue;
            cell = default;

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        PhysicsBall ball = balls[x, z, level];
                        if (ball == null) continue;

                        float y = ball.BallReference.Pose.Position.Y;
                        if (y >= lowest) continue;

                        lowest = y;
                        cell = new XZLevel(x, z, level);
                    }

            return lowest;
        }

        /// <summary>
        /// How many balls of the structure are under the line — <b>the figure that separates a body sagging
        /// from a single ball being thrown</b>, and the trace is worth nothing without it: the loss rule reads
        /// a minimum, so one flung ball and a whole cluster arriving are the same number to it.
        /// </summary>
        private static int CountUnderLine(PhysicsBall[,,] balls)
        {
            XZLevel size = XZLevel.FromArray(balls);
            int under = 0;

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                        if (balls[x, z, level] != null
                            && balls[x, z, level].BallReference.Pose.Position.Y <= ClusterHang.DEATH_Y) under++;

            return under;
        }

        /// <summary>
        /// Wakes <b>every</b> ball of the structure.
        /// <para>
        /// <b>⚠ Every, and not one — this is the finding that made the probe honest, and it cost most of a
        /// session.</b> The obvious version wakes one ball on the reasoning the game itself writes down
        /// (<c>GameplayScreen.WakeForDescent</c>): <i>"waking a body wakes the whole sleeping set it belongs
        /// to, so one ball is enough for the cluster — the structure is a single connected constraint
        /// graph"</i>. That is true of the game and <b>false here</b>, and the difference is the probe's own
        /// quiet: with nothing being fired, Bepu sleeps the cluster in pieces as it settles, and the solver's
        /// active constraint count on a 502-ball level falls from 3836 to 2740 over four seconds of hanging
        /// still. So one ball wakes one island of several.
        /// </para>
        /// <para>
        /// It matters because <see cref="BallsConstraintsBuilder.ReleaseSameTypeCluster"/> cuts constraints
        /// and only then wakes the balls it cut them from. In the game that order is safe by construction —
        /// a group is only ever released from inside the contact handler, on the frame a shot has just
        /// touched the structure, and the contact woke it. Cutting them out of a <i>sleeping</i> island
        /// instead left the survivors holding a graph the solver never re-examined: the remainder came apart
        /// and fell at three quarters of gravity, and it read as a sag on levels nobody has ever failed to
        /// finish — <see cref="Amphora"/>, the Gallery's pictures, the whole Coil. Every verdict this probe
        /// printed before this loop wound past the first ball was about a bug in the probe.
        /// </para>
        /// </summary>
        private static void WakeCluster(PhysicsWorld world, PhysicsBall[,,] balls)
        {
            XZLevel size = XZLevel.FromArray(balls);

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        if (balls[x, z, level] == null) continue;

                        world.Simulation.Awakener.AwakenBody(balls[x, z, level].BallReference.Handle);
                    }
        }

        /// <summary>
        /// Removes released balls once they are past the island, which is the game's kill plane in the one
        /// respect that matters here: a run is forty seconds of simulation and a drain filling with debris
        /// would cost more than the cluster does.
        /// </summary>
        private static void CullFallen(PhysicsWorld world, List<PhysicsBall> falling)
        {
            for (int i = falling.Count - 1; i >= 0; i--)
            {
                if (falling[i].BallReference.Pose.Position.Y > ArenaIsland.FUNNEL_BOTTOM_Y) continue;

                world.Simulation.Bodies.Remove(falling[i].BallReference.Handle);
                falling.RemoveAt(i);
            }
        }

        /// <summary>
        /// One standing group, picked at random from those big enough to match. Random and not clever on
        /// purpose — see the class doc: the question is whether the level survives being taken apart, not
        /// whether a good player would take it apart this way.
        /// </summary>
        private static XZLevel? PickGroup(BallsMap map, Random random)
        {
            StaticBall[,,] array = map.GetStaticBallsArray();
            HashSet<XZLevel> seen = new();
            List<XZLevel> groups = new();

            for (byte level = 0; level < map.Levels; level++)
                for (byte x = 0; x < map.StageSizeX; x++)
                    for (byte z = 0; z < map.StageSizeZ; z++)
                    {
                        XZLevel cell = new(x, z, level);
                        if (array[x, z, level] == null || seen.Contains(cell)) continue;

                        List<XZLevel> group = map.GetConnectedSameTypeCells(cell);
                        foreach (XZLevel member in group) seen.Add(member);

                        if (group.Count >= BallsConstraintsBuilder.MINIMUM_CLUSTER_SIZE) groups.Add(cell);
                    }

            return groups.Count == 0 ? null : groups[random.Next(groups.Count)];
        }

        /// <summary>
        /// The pack's worst run of a level, which is what a gate has to be decided on: a level with a losing
        /// order in it is a level a player can lose, however well the other orders went.
        /// </summary>
        internal static Run Worst(Run[] runs) =>
            runs.OrderBy(r => r.Outcome == Outcome.Sagged ? 0 : 1).ThenBy(r => r.WorstClearance).First();
    }
}
