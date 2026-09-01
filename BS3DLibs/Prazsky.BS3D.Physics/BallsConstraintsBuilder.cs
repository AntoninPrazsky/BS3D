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

            BodyInertia bodyInertia = new Sphere(BALL_RADIUS).ComputeInertia(BALL_MASS);

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
                            BodyDescription bodyDescription = BodyDescription.CreateDynamic(
                                staticBalls[x, z, level].GetPosition() + worldOffset,
                                bodyInertia,
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
        /// <returns>
        /// The two kinds of released ball, kept apart rather than summed (see <see cref="BallsReleased"/>): a
        /// scorer has to be able to tell the group the player aimed at from everything that fell because they
        /// cut its support. Zero of both when the cluster is below the minimum size.
        /// </returns>
        public static BallsReleased ReleaseSameTypeCluster(PhysicsBall attachedBall, PhysicsBall[,,] physicsBalls, BallsMap map, Simulation simulation, List<PhysicsBall> releasedInto)
        {
            List<XZLevel> cluster = map.GetConnectedSameTypeCells(attachedBall.ArrayPosition);
            if (cluster.Count < MINIMUM_CLUSTER_SIZE) return default;

            XZLevel size = map.GetStaticBallsArraySize();
            List<ConstraintHandle> handleBuffer = new();

            foreach (XZLevel cell in cluster)
                ReleaseBall(cell, physicsBalls, map, simulation, size, handleBuffer, releasedInto);

            //Balls no longer connected to the ceiling would fall as chains still constrained to each other;
            //releasing them explicitly cuts those constraints so they fall as individual balls.
            List<XZLevel> disconnected = map.GetCellsDisconnectedFromCeiling();
            foreach (XZLevel cell in disconnected)
                ReleaseBall(cell, physicsBalls, map, simulation, size, handleBuffer, releasedInto);

            return new BallsReleased(cluster.Count, disconnected.Count);
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
