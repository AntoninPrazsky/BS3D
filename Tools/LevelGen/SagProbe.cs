using BepuPhysics;
using BepuPhysics.Collidables;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using Microsoft.Xna.Framework;
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
    /// <see cref="ClusterHang.FitWorldOffset"/> hangs it, and then <b>plays it</b>. The whole of the physics
    /// is real and is the game's: the same Bepu at the same <c>SolveDescription(8, 1)</c>, the same
    /// <c>BallSocket</c> lattice, the same 1/120 s step, the glass dragging the cluster down as it descends.
    /// After every shot the world is stepped while the matched group falls and <b>the remainder rotates into
    /// a new rest pose</b>, and the death line is read off the <i>live body poses</i> through the game's own
    /// <see cref="ClusterLineWatch"/>. No graphics device is needed; the simulation was never the part that
    /// drew anything.
    /// </para>
    /// <para>
    /// <b>What is NOT simulated is the shot's flight.</b> Nothing is launched and no contact is waited for:
    /// the barrel's line is swept against the live structure by
    /// <see cref="ShotPlacement.TryFindFirstHit"/> — the same sweep the game's own landing preview runs — and
    /// the cell is <see cref="ShotPlacement"/>'s answer, so the ghost, the game and this cannot disagree
    /// about where a ball ends up. The landing then mirrors <c>BallContactEventHandler</c> step for step,
    /// including the one order it had to learn the hard way (#265): the body is <b>put</b> in its cell before
    /// its constraints are made.
    /// </para>
    /// <para>
    /// <b>⚠ Why a playthrough and not a per-group drop test.</b> The obvious cheaper gate is to clear one
    /// plausible group, hang the remainder and look — and it would have missed the reports. The owner's words
    /// are <i>"as soon as a part of the map has been shot away"</i> and <i>"the player always loses after a few
    /// shots"</i>: the sag is cumulative, each cut leaving the next remainder thinner, and a level whose first
    /// cut is harmless can hinge on its third. So it plays the level through, which is the same shape as a
    /// game and costs one run rather than one run a group.
    /// </para>
    /// <para>
    /// <b>⚠ The physics was never the hard part; the PLAYER was, and every wrong verdict this file has ever
    /// printed came from that half.</b> Three faults in a row, each found only by being refuted: it picked a
    /// group and deleted it, which let it cut a vase's waist and leave the foot dangling — a cut no gun can
    /// make; it aimed at balls it could not see, so <see cref="ShotPlacement"/> dutifully landed the shot
    /// beside whatever the line met first and the cluster <i>grew</i> instead of clearing; and it was handed
    /// a zero orbit centre, which aims <see cref="Cannon"/> at its own trunnions and normalises the aim to
    /// NaN, so it fired nothing at all and scored every level as survived. The model now loads a colour the
    /// way the game does, aims only at balls the sweep comes back holding, prefers the shot that completes a
    /// group, and lets the two throws in three that complete nothing <b>stick</b> — which is what puts balls
    /// back onto the underside, and is the difference between a probe and a wrecking ball.
    /// </para>
    /// <para>
    /// <b>What it still is not.</b> It says nothing about whether a level is <i>fun</i> or whether a budget is
    /// generous — <c>Tools/ScoreSim</c> is where that question lives. What it says is narrower and is exactly
    /// what was missing: <b>whether a level can be taken apart without the rest of it sagging into the
    /// drain</b>.
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
        /// How long the world runs after each shot lands, i.e. how fast this player shoots. It has to be past
        /// <see cref="ClusterHang.BELOW_LINE_GRACE"/> or a held crossing could never be seen at all — the
        /// probe would step past the very verdict it exists to collect — and past the fall itself, so the
        /// remainder has re-hung rather than merely started to.
        /// </summary>
        private const float SHOT_SECONDS = 1.6f;

        /// <summary>
        /// How many of <see cref="RUNS_PER_LEVEL"/> losing orders make a level worth reporting — <b>the one
        /// threshold here that is calibrated against play rather than chosen</b>, and it is the owner's
        /// playtest that set it.
        /// <para>
        /// Three levels are known finishable (Amphora, Giza, Saturn) and nine are known not to be. Against
        /// that set the probe reads <b>Giza 0, Saturn 2, Amphora 3</b> against <b>Ghost and Cabinet at 4</b>
        /// and Pylon, Orrery, Globe, Pinecone, Pleat, Bolt and Totem at <b>5 of 5</b>. Four losing orders in
        /// five therefore separates them <b>completely</b>: all nine reported named, none of the three.
        /// </para>
        /// <para>
        /// <b>⚠ Three was the answer while the player was a poor shot, and it is worth knowing why it
        /// moved.</b> Aiming at a ball rather than at the gap beside it matched 29 % of shots; the level then
        /// mostly lost to the probe's own misses stacking up, which is noise, and the two hardest reported
        /// levels (Ghost, Cabinet) sat down at 1 while Amphora sat at 2. Teaching it to aim at the gap took
        /// the match rate to 86 % and moved every one of those the right way at once — Ghost and Cabinet to
        /// 4, Amphora to 3. <b>The separation is a property of a competent player and not of a threshold.</b>
        /// </para>
        /// <para>
        /// A threshold fitted to twelve levels is still a threshold fitted to twelve levels, which is why
        /// this reports rather than refuses — see <c>Program.RunSagGate</c>.
        /// </para>
        /// </summary>
        internal const int SAG_RUNS_TO_REPORT = 4;

        /// <summary>
        /// How many differently-ordered runs each level gets. The order matters and that is the point: a
        /// spiral cut from the top leaves a different remainder than the same spiral cut from the middle.
        /// <para>
        /// <b>Five, and it was three until the reading changed from "did any order lose" to "how many did".</b>
        /// That change is the owner's playtest: asked the first question the probe lost 38 of the 90 shipped
        /// levels, three of which were then confirmed finishable, so a single losing order says nothing on
        /// its own and the useful figure is the fraction. Five gives that fraction somewhere to live. Every
        /// seed is fixed, so the shot <i>sequences</i> are reproducible — but the count is not bit-stable,
        /// and this sentence used to claim it was: Bepu's threaded solve is not deterministic across runs,
        /// so an order whose deepest dip sits within a few hundredths of the allowance can flip, and #301
        /// measured a borderline level reading 2, 3, 3, 3, 2 across five identical sweeps. A reading at the
        /// threshold's edge is therefore worth repeating before anything is concluded from it; a 0 or a 5 is
        /// not.
        /// </para>
        /// </summary>
        private const int RUNS_PER_LEVEL = 5;

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

        /// <summary>What one pull of the trigger did.</summary>
        private enum Shot
        {
            /// <summary>A ball is in the structure that was not there before, and the match rule has been asked.</summary>
            Landed,

            /// <summary>
            /// The shot found no free cell in either ring around what it hit, so it did not stick — the
            /// game's own bounce. It costs the shot and nothing else.
            /// </summary>
            Bounced,

            /// <summary>
            /// There is nothing to load or nowhere to aim at all. The run genuinely cannot go on, which is
            /// the only thing that may end one early.
            /// </summary>
            Nothing,
        }

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

            Vector3 worldOffsetXna = ClusterHang.FitWorldOffset(map, out float fieldTopY);
            System.Numerics.Vector3 worldOffset = worldOffsetXna.ToNumerics();

            //THE GUN, and it is the real one: Cannon is pose arithmetic with no renderer in it, and it seats
            //its own trunnions off the island's stone (CannonRig.TrunnionHeightAt is static). What cannot be
            //asked for here is GameCameraFit's full four-bound solve, because that reads the built rig's
            //BarrelReach and the viewport's aspect - so the radius is the two bounds that are pure geometry:
            //the gun must clear the field's own footprint at every orbit angle, and its wheels must stay
            //outside the drain's mouth. That is the CLOSEST stance the solve can ever return, i.e. the
            //steepest, and it is stated rather than guessed: a gun standing further out only shoots flatter,
            //and which ball a shot meets first is ShotPlacement's answer against the live cluster either way.
            float footprint = MathF.Max(CeilingPlate.FootprintFor(map.StageSizeX),
                CeilingPlate.FootprintFor(map.StageSizeZ)) * Constants.HALF;
            float orbitRadius = MathF.Max(
                footprint * Constants.SQRT_TWO + GameCameraFit.CANNON_FIELD_CLEARANCE,
                ArenaIsland.FUNNEL_TOP_RADIUS + GameCameraFit.CANNON_DRAIN_CLEARANCE);

            //⚠ The orbit centre is (0, 5, 0) and NOT Vector3.Zero, exactly as both executables construct it.
            //Cannon.RecalculateRotation builds its aim target as Position + Transform(OrbitCenter, rotation),
            //so the vector is doing double duty - a direction AND the distance the aim point is thrown out to.
            //A zero centre therefore aims the gun at its own trunnions, AimDirection normalises (0,0,0) to NaN,
            //and every shot silently misses the whole cluster: the probe fired nothing at all and scored the
            //level as survived.
            Cannon cannon = new(CANNON_ORBIT_CENTRE, orbitRadius);

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
                //⚠ REMOVABLE and not ALL, which is CheckLevelCleared's own test (#323). A rock is not a ball
                //the level is waiting for: the game clears the moment nothing removable is left and cuts the
                //stone loose afterwards, so a probe asking for an EMPTY field would play every rock level to
                //the end of its budget and report OutOfShots on a level the player had finished. It did
                //exactly that on the Mirage's Seam and Cairn before this line was written.
                if (map.GetRemovableBallsCount() == 0) { outcome = Outcome.Cleared; break; }
                if (shotsFired >= shots) { outcome = Outcome.OutOfShots; break; }

                //⚠ WAKE THE CLUSTER FIRST, and this is the one divergence from the game that a probe HAS to
                //repair by hand rather than inherit. In the game a ball only ever attaches from inside the
                //contact handler, i.e. on the frame a shot has just touched the structure - and a contact
                //wakes the sleeping set it lands in, so the cluster is always awake by the time its
                //constraints are cut and made. This probe adds its ball by hand, so after SETTLE_SECONDS of
                //quiet Bepu has put the whole thing to sleep (both the balls and the plate carry
                //PhysicsWorld.SLEEP_THRESHOLD), and cutting constraints out of a sleeping island leaves the
                //survivors holding a graph the solver never re-examined: the remainder came apart and fell.
                //Every ball is woken and not one - see WakeCluster, where the reason is measured.
                world.Simulation.Awakener.AwakenBody(ceiling.Handle);
                WakeCluster(world, balls);

                //One shot, the game's way: aim, sweep the line, land in the cell ShotPlacement answers with,
                //attach, and only then ask the match rule. A shot that completes nothing STAYS - which is
                //two throws in three and is the whole reason this replaced picking a group to delete.
                Shot shot = FireOneShot(world, cannon, ceiling, ceilingY, map, balls, falling, worldOffsetXna,
                    random, out BallsReleased released);

                //⚠ A BOUNCE COSTS A SHOT AND NOTHING ELSE, and treating it as the end of the run was a bug
                //worth naming: on a dense truss every sampled candidate can come back with both its rings
                //full, and the run then stopped with nine hundred balls still hanging and scored the level as
                //survived. It is the same shape of silent false pass the NaN aim direction gave - a probe
                //that stops playing reports no sag, so anything that can stop it early has to be deliberate.
                if (shot == Shot.Nothing) { outcome = Outcome.OutOfShots; break; }

                shotsFired++;

                if (shot == Shot.Bounced)
                {
                    //The world still runs: the cluster is swinging from the last landing and the glass may be
                    //mid-step, and a bounce does not pause either.
                    outcome = Advance(world, ceiling, balls, falling, ref watch, ref worstClearance,
                        ref ceilingY, ceilingTargetY, SHOT_SECONDS);
                    continue;
                }

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
            //Asked of ALL the balls here and not of the removable ones: this is a question about what is
            //still hanging in the simulation, and stone left over a cleared field is hanging.
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
        /// How far above the cluster's own underside a shot aims, in lattice levels — the working band, and
        /// the game's own rule rather than a guess: the gun stands under the hanging cluster and shoots up
        /// into it, so what a shot meets is the underside, which is why
        /// <c>GameplayScreen.TALL_AIM_HEADROOM_LEVELS</c> holds a tall level's aim to a band just above that
        /// underside and calls the reason out — <i>"the column has to be eaten from the bottom"</i>. The
        /// figure is that constant's.
        /// <para>
        /// It is where the player <b>points</b>. Where the ball ends up is <see cref="ShotPlacement"/>'s, off
        /// the gun's own line, exactly as in the game.
        /// </para>
        /// </summary>
        private const int AIM_BAND_LEVELS = 5;

        /// <summary>
        /// The grown sphere a shot sweeps: its own radius plus a structure ball's, which is what
        /// <see cref="ShotPlacement.TryFindFirstHit"/> measures the first touch against.
        /// </summary>
        private static readonly float SHOT_RADIUS_SUM = 2f * BallsConstraintsBuilder.BALL_RADIUS;

        /// <summary>
        /// <b>Which ball the player points the gun at</b> — a ball of the loaded colour that already has a
        /// neighbour of its own colour, so landing beside it completes the three, and low enough to shoot at
        /// (<see cref="AIM_BAND_LEVELS"/>). When the loaded colour has no such ball anywhere reachable, any
        /// ball in the band will do: the shot then simply sticks, which is a real shot and not a wasted turn.
        /// <para>
        /// <b>⚠ This used to pick a GROUP and release it outright, and the owner's playtest is what refuted
        /// that.</b> Asked over every standing group, the probe lost 38 of the 90 shipped levels — Amphora,
        /// Giza and Saturn among them, all three then confirmed finishable from play. The traces say what the
        /// old pick was doing: on Amphora it took a 57-ball group out of the vase's <i>waist</i> on the first
        /// shot, with all twenty ceiling anchors left intact and nothing orphaned, and left the foot hanging
        /// on two neighbours five levels below. No player can make that cut. So the probe stopped choosing
        /// what falls and started choosing where to aim, and lets <see cref="ShotPlacement"/> decide the rest.
        /// </para>
        /// </summary>
        private static List<XZLevel> PickAimTargets(BallsMap map, BallType loaded, Random random)
        {
            StaticBall[,,] array = map.GetStaticBallsArray();
            XZLevel size = map.GetStaticBallsArraySize();
            List<XZLevel> matching = new();
            List<XZLevel> anything = new();

            int reach = map.GetLowestOccupiedLevel() + AIM_BAND_LEVELS;

            //⚠ A PLAYER AIMS AT THE GAP, NOT AT A BALL, and this is the third and last thing the model had to
            //be taught. Aiming at a ball's centre lets ShotPlacement pick whichever cell of the ring the
            //contact happens to fall nearest, which is very nearly arbitrary: the probe matched 29 % of its
            //shots on Pylon, stacked the other 71 % onto the underside, and walked a column of its own misses
            //down into the death line at shot 41 - then reported the LEVEL as sagging. So the candidates are
            //EMPTY cells now, and the ones that would complete a group come first. The shot is still resolved
            //by ShotPlacement against the live cluster; what changed is only where the barrel is pointed.
            for (byte level = 0; level <= reach && level < map.Levels; level++)
                for (byte x = 0; x < map.StageSizeX; x++)
                    for (byte z = 0; z < map.StageSizeZ; z++)
                    {
                        if (array[x, z, level] != null) continue;

                        XZLevel cell = new(x, z, level);

                        //Only cells against the cluster: a shot cannot stick to empty space, and aiming into
                        //it is how the first cut of this wasted most of its candidates.
                        bool touching = false;
                        foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(cell, size))
                            if (array[neighbour.X, neighbour.Z, neighbour.Level] != null) { touching = true; break; }

                        if (!touching) continue;

                        if (WouldMatch(map, cell, loaded)) matching.Add(cell); else anything.Add(cell);
                    }

            //The colour's own balls first and everything else behind them, each half shuffled: the caller
            //walks this in order and takes the first shot that is both VISIBLE and lands a match, so the
            //order is the player's preference and the shuffle is what makes five runs five different games.
            Shuffle(matching, random);
            Shuffle(anything, random);
            matching.AddRange(anything);

            //How hard this player looks for a match before settling for a ball that merely sticks. Every
            //candidate costs a sweep of the structure (ShotPlacement.TryFindFirstHit is O(cells)), which is
            //nothing beside the 192 physics steps each shot is followed by - so the cap is about diminishing
            //returns rather than time.
            //
            //⚠ It was FORTY and that was too few, on the levels where it matters most. Most candidates fail
            //the visibility test - a ball of the loaded colour is usually somewhere in the body rather than
            //on the face the gun sees - so a shallow search falls back to "any ball that sticks" far more
            //often than a player would, and every such shot ADDS to the underside. On Pylon that flooded the
            //field: 41 % of shots matched, the misses built six levels below the layout's own floor, and the
            //level was scored as sagging when what had actually happened is that the probe played badly.
            if (matching.Count > AIM_CANDIDATES) matching.RemoveRange(AIM_CANDIDATES, matching.Count - AIM_CANDIDATES);

            return matching;
        }

        /// <inheritdoc cref="PickAimTargets"/>
        private const int AIM_CANDIDATES = 200;

        /// <summary>
        /// Where the gun points when it is not aiming anywhere else, and the length its aim target is thrown
        /// out to - see the note where the gun is built. The very vector both executables construct their
        /// cannon with.
        /// </summary>
        private static readonly Vector3 CANNON_ORBIT_CENTRE = new(0f, 5f, 0f);

        private static void Shuffle(List<XZLevel> cells, Random random)
        {
            for (int i = cells.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }
        }

        /// <summary>
        /// Whether a ball of <paramref name="colour"/> landing in <paramref name="cell"/> would complete a
        /// group — asked by putting it in the lattice, measuring, and taking it out again. The map is the
        /// cheap copy of the truth here; doing it any other way means re-deriving
        /// <see cref="BallsMap.GetConnectedSameTypeCells"/>'s own neighbour walk, which is the rule this has
        /// to agree with exactly.
        /// </summary>
        /// <remarks>
        /// <b>The glass is coloured before the group is measured and put back afterwards (#325)</b>, because
        /// that is what a landing does and therefore what a player is aiming at. Without it the probe cannot
        /// see the shot the transparent ball exists to make worth taking — a shot that completes a group
        /// THROUGH the glass — so on a Mirage glass level it would rank every such cell as "sticks but does
        /// not match" and fire somewhere else. The undo puts the KIND back and does not bother with the
        /// colour, which is exact rather than approximate: a transparent ball's stored type is read by
        /// nothing at all — the flood fill skips it, the magazine census skips it, and the colouring
        /// overwrites it — so the cell it comes back as is the cell it was in every respect anything asks
        /// about. The scratch lists are static and reused for the reason the contact handler's is: this runs
        /// a few hundred times per shot and a few million times per pack.
        /// </remarks>
        private static bool WouldMatch(BallsMap map, XZLevel cell, BallType colour)
        {
            //A cell that arms a bomb is a shot worth taking whatever colour is loaded (#326) — the blast is
            //not a colour question at all, so it is answered before the lattice is touched. Without this the
            //probe would rank every bomb-adjacent gap as "sticks but does not match" and only ever land there
            //by fallback, which underrates the one shot a bomb level is played on.
            if (ArmedBombs(map, cell, _candidateBombs).Count > 0) return true;

            map.PutBallAt((byte)cell.X, (byte)cell.Z, (byte)cell.Level, colour);
            map.ColourTransparentNeighbours(cell, colour, _wouldMatchColoured);

            int group = map.GetConnectedSameTypeCells(cell).Count;

            foreach (XZLevel at in _wouldMatchColoured)
                map.PutBallAt((byte)at.X, (byte)at.Z, (byte)at.Level, colour, BallKind.Transparent);

            map.RemoveBallAt((byte)cell.X, (byte)cell.Z, (byte)cell.Level);

            return group >= BallsConstraintsBuilder.MINIMUM_CLUSTER_SIZE;
        }

        /// <inheritdoc cref="WouldMatch"/>
        private static readonly List<XZLevel> _wouldMatchColoured = new();

        /// <summary>
        /// The landing's own colouring step, run over both sides at once — the map, which is the truth about
        /// what a ball IS, and the physics array, which is its mirror (#323). Both have to move together or
        /// the flood fill and the constraint walk disagree about the same cell for the rest of the level.
        /// <para>
        /// It is <see cref="BallContactEventHandler"/>'s loop with the two things the probe has no use for
        /// left out: there is no renderer here, so nothing starts the colour fade, and there is no contact
        /// stream, so nothing is unregistered.
        /// </para>
        /// </summary>
        private static void ColourTransparentNeighbours(BallsMap map, PhysicsBall[,,] balls, XZLevel cell, BallType colour)
        {
            map.ColourTransparentNeighbours(cell, colour, _landingColoured);

            foreach (XZLevel at in _landingColoured)
            {
                PhysicsBall glass = balls[at.X, at.Z, at.Level];
                if (glass == null) continue;

                glass.Type = colour;
                glass.Kind = BallKind.Normal;
            }
        }

        /// <inheritdoc cref="ColourTransparentNeighbours(BallsMap, PhysicsBall[,,], XZLevel, BallType)"/>
        private static readonly List<XZLevel> _landingColoured = new();

        /// <summary>
        /// The bombs standing beside a cell (#326) — <c>BallContactEventHandler.CollectArmedBombs</c>, repeated
        /// here for the reason the colouring above is: the probe lands a ball straight into the lattice and
        /// runs no contact handler, so every step of a landing has to be repeated or it is not the same game.
        /// </summary>
        /// <param name="into">Where to put them. Passed in rather than answered from a shared scratch list,
        /// because the two callers overlap: the aim search asks this of every candidate cell and the landing
        /// asks it of the one that was chosen, so a single buffer would have the search's last answer standing
        /// where the landing's belongs.</param>
        private static List<XZLevel> ArmedBombs(BallsMap map, XZLevel cell, List<XZLevel> into)
        {
            into.Clear();

            StaticBall[,,] cells = map.GetStaticBallsArray();
            XZLevel size = map.GetStaticBallsArraySize();

            foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(cell, size))
            {
                StaticBall ball = cells[neighbour.X, neighbour.Z, neighbour.Level];
                if (ball != null && ball.Kind == BallKind.Bomb) into.Add(neighbour);
            }

            return into;
        }

        /// <inheritdoc cref="ArmedBombs"/>
        private static readonly List<XZLevel> _landingBombs = new();

        /// <inheritdoc cref="ArmedBombs"/>
        private static readonly List<XZLevel> _candidateBombs = new();

        /// <summary>
        /// What the barrel is loaded with — <b>uniform over the colours still standing</b>, which is
        /// <c>GameplayScreen.RandomBallType</c>'s rule exactly, count-blind and all. The magazine's queue of
        /// five only delays that draw, so a probe that draws per shot loads the same distribution.
        /// <para>
        /// <b>Over the MATCHABLE balls only (#323/#325)</b>, which is <c>RecountBallTypes</c>'s own rule and
        /// not a nicety. A rock carries a colour in the file that nothing reads — the granite shading
        /// ignores it outright — so counting it would have loaded the barrel with slate on the Mirage's five
        /// stone levels, a colour no matchable ball in any of them wears; roughly one shot in five would
        /// have been unmatchable by construction and every one of them would have added a ball to the
        /// underside. Glass is skipped for the plainer reason that it has no colour to contribute.
        /// </para>
        /// </summary>
        private static BallType? LoadedColour(BallsMap map, Random random)
        {
            StaticBall[,,] array = map.GetStaticBallsArray();
            List<BallType> live = new();

            for (byte level = 0; level < map.Levels; level++)
                for (byte x = 0; x < map.StageSizeX; x++)
                    for (byte z = 0; z < map.StageSizeZ; z++)
                    {
                        StaticBall ball = array[x, z, level];

                        if (ball != null && BallKinds.Matchable(ball.Kind) && !live.Contains(ball.Type))
                            live.Add(ball.Type);
                    }

            return live.Count == 0 ? null : live[random.Next(live.Count)];
        }

        /// <summary>
        /// <b>One shot, through the game's own path</b>: the gun stands to face the ball being aimed at, the
        /// barrel is laid on it under its own elevation and traverse clamps, the shot's line is swept against
        /// the live structure by <see cref="ShotPlacement.TryFindFirstHit"/>, and the cell it lands in is
        /// <see cref="ShotPlacement"/>'s answer rather than this file's guess.
        /// <para>
        /// The landing then mirrors <c>BallContactEventHandler</c> step for step, and the order is load-bearing
        /// in one place that handler had to learn the hard way (#265): <b>the body is PUT in the cell before
        /// the constraints are made</b>. Letting the new sockets drag it in looks equivalent and is not — the
        /// ceiling socket ties the ball's own north pole to a point under the plate, a pair with two solutions,
        /// and a ball dragged in from one side settles into the inverted one and sleeps there.
        /// </para>
        /// </summary>
        private static Shot FireOneShot(PhysicsWorld world, Cannon cannon, BodyReference ceiling, float ceilingY,
            BallsMap map, PhysicsBall[,,] balls, List<PhysicsBall> falling, Vector3 worldOffset, Random random,
            out BallsReleased released)
        {
            released = default;

            BallType? loaded = LoadedColour(map, random);
            if (loaded == null) return Shot.Nothing;

            List<XZLevel> candidates = PickAimTargets(map, loaded.Value, random);
            if (candidates.Count == 0) return Shot.Nothing;

            bool found = false;
            XZLevel cell = default;
            Vector3 drift = default;

            //⚠ A PLAYER AIMS AT WHAT THEY CAN SEE, and the first cut of this did not: it pointed the gun at a
            //ball of the loaded colour wherever it was, and ShotPlacement then dutifully landed the shot beside
            //whatever ball the LINE met first - an outer one, usually of another colour entirely. The result
            //was a probe that matched almost nothing and grew the cluster it was supposed to be clearing (502
            //balls to 513 over eighteen shots on Amphora). So a candidate is only taken if the sweep comes
            //back holding THAT ball, which is the honest reading of "visible from the gun", and preferred if
            //the cell it lands in completes a group. The first workable shot is kept as the fallback: a player
            //with nothing to match still fires, and the ball that sticks is the point of firing at all.
            foreach (XZLevel candidate in candidates)
            {
                //The gap itself, in the frame the cluster hangs in. Taken off the lattice rather than off a
                //live body because an empty cell has no body to read - the drift the cluster has picked up is
                //well under a cell, and the shot is resolved against the live structure regardless.
                Vector3 target = map.GetRealCenteredPosition(candidate) + worldOffset;

                //Stand facing the cell first, so the shot is the clean one over open ground rather than one
                //fired across the whole cluster - the Testbed's aimshoot sweep makes the same move for the
                //same reason.
                cannon.OrbitToFace(target);
                cannon.AimAt(target);

                if (!ShotPlacement.TryFindFirstHit(balls, cannon.Position, cannon.AimDirection, SHOT_RADIUS_SUM,
                        out PhysicsBall hit, out Vector3 contact)) continue;

                if (!ShotPlacement.TrySolveAgainstBall(map, hit, contact, worldOffset, out XZLevel solved,
                        out Vector3 solvedDrift)) continue;                    //both rings full: it would bounce

                //The first shot that would stick anywhere, kept in case nothing matches: a player with no
                //match available still fires, and the ball that sticks is the point of firing at all.
                if (!found) { cell = solved; drift = solvedDrift; found = true; }

                //What the gun ACTUALLY put there, which need not be the gap aimed at - the line may have met
                //a ball short of it. Asked of the solved cell rather than of the intent, so a shot only counts
                //as a match when it really is one.
                if (!WouldMatch(map, solved, loaded.Value)) continue;

                cell = solved;
                drift = solvedDrift;
                break;
            }

            if (!found) return Shot.Bounced;

            Vector3 rest = ShotPlacement.CellWorldPosition(map, cell, worldOffset, drift);

            BodyHandle handle = world.Simulation.Bodies.Add(BodyDescription.CreateDynamic(
                rest.ToNumerics(),
                new Sphere(BallsConstraintsBuilder.BALL_RADIUS).ComputeInertia(BallsConstraintsBuilder.BALL_MASS),
                new CollidableDescription(BallsConstraintsBuilder.GetSphereShapeIndex(world.Simulation),
                    BallsConstraintsBuilder.SPECULATIVE_MARGIN),
                new BodyActivityDescription(PhysicsWorld.SLEEP_THRESHOLD)));

            PhysicsBall landed = new()
            {
                BallReference = new BodyReference(handle, world.Simulation.Bodies),
                Type = loaded.Value,
                ArrayPosition = cell,
            };

            map.PutBallAt((byte)cell.X, (byte)cell.Z, (byte)cell.Level, loaded.Value);
            balls[cell.X, cell.Z, cell.Level] = landed;

            BallsConstraintsBuilder.AttachBallToStructure(landed, balls, map, world.Simulation, ceiling);

            //The glass takes the colour that just arrived (#325) - after the attach and BEFORE the group is
            //counted, which is BallContactEventHandler's own order and the whole of where this may go. It is
            //here because the probe does not run the contact handler: it lands a ball straight into the
            //lattice, so every step of a landing that lives in the handler has to be repeated here or the
            //probe plays a different game from the one it is measuring. It played exactly that different
            //game once, and the record is worth keeping: the Mirage's five glass levels were probed with no
            //colouring at all, so their transparent balls could only ever leave the cluster by being
            //ORPHANED - which made two of the five look like layout faults and the opener look like a level
            //that clears in ten shots.
            ColourTransparentNeighbours(map, balls, cell, loaded.Value);

            //Which bombs this landing armed (#326), read BEFORE the release for the handler's own reason: a
            //bomb the release orphans has already fallen and must not go off in mid-air. Same shape as the
            //colouring above and the same lesson - a step of a landing that lives in the contact handler has
            //to be repeated here, or the probe measures a game the player is not playing.
            List<XZLevel> armed = ArmedBombs(map, cell, _landingBombs);

            //And the game rule: three or more of a colour touching each other let go, and so does anything
            //that was only held up by them. A shot that completes nothing simply stays, which is the whole
            //point of firing rather than picking - two throws in three add a ball to the underside.
            released = BallsConstraintsBuilder.ReleaseSameTypeCluster(landed, balls, map, world.Simulation, falling);

            //Then the blast, over what the match left standing - and it runs its own disconnection pass, which
            //is the half of it a sag probe actually cares about: a hole opened under a third of the cluster is
            //the largest single thing that can happen to a level's remaining load path.
            if (armed.Count > 0)
                released = released.Plus(BallsConstraintsBuilder.DetonateBombs(
                    armed, balls, map, world.Simulation, falling));

            return Shot.Landed;
        }

        /// <summary>
        /// The pack's worst run of a level, which is what a gate has to be decided on: a level with a losing
        /// order in it is a level a player can lose, however well the other orders went.
        /// </summary>
        internal static Run Worst(Run[] runs) =>
            runs.OrderBy(r => r.Outcome == Outcome.Sagged ? 0 : 1).ThenBy(r => r.WorstClearance).First();
    }
}
