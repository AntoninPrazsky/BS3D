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

        private static readonly float SPECULATIVE_MARGIN = 0.1f; //TODO: Study what this value does exactly and how can it be optimized

        /// <summary>
        /// Threshold of squared velocity under which the body is allowed to go to sleep.
        /// </summary>
        private static readonly float SLEEP_TRESHOLD = Constants.HUNDREDTH;

        public static readonly SpringSettings SPRING_SETTINGS = new(frequency: 15f, dampingRatio: 1f);

        public static PhysicsBall[,,] BuildBallsStructure(StaticBall[,,] staticBalls, ref Simulation simulation, BodyReference ceilingReference)
        {
            if (staticBalls == null) throw new NullReferenceException(nameof(staticBalls));
            if (simulation == null) throw new NullReferenceException(nameof(simulation));
            if (staticBalls.Rank != 3) throw new ArgumentOutOfRangeException(nameof(staticBalls.Rank));

            XZLevel size = XZLevel.FromArray(staticBalls);

            //Same [x, z, level] dimensions as the static balls array
            PhysicsBall[,,] physicsBalls = new PhysicsBall[size.X, size.Z, size.Level];

            #region Create physical representation for each ball (without connecting them)

            Sphere sphere = new(BALL_RADIUS);
            BodyInertia bodyInertia = sphere.ComputeInertia(BALL_MASS);

            TypedIndex speheShapeIndex = simulation.Shapes.Add(sphere);

            CollidableDescription collidableDescription = new(speheShapeIndex, SPECULATIVE_MARGIN);
            BodyActivityDescription bodyActivityDescription = new(SLEEP_TRESHOLD);

            for (byte level = 0; level < size.Level; level++)
            {
                for (byte x = 0; x < size.X; x++)
                {
                    for (int z = 0; z < size.Z; z++)
                    {
                        if (staticBalls[x, z, level] != null) //Is there even a ball here?
                        {
                            BodyDescription bodyDescription = BodyDescription.CreateDynamic(
                                staticBalls[x, z, level].GetPosition(),
                                bodyInertia,
                                collidableDescription,
                                bodyActivityDescription);

                            BodyHandle bodyHandle = simulation.Bodies.Add(in bodyDescription);

                            BodyReference bodyReference = new(bodyHandle, simulation.Bodies);

                            PhysicsBall ball = new()
                            {
                                BallReference = bodyReference,
                                Type = staticBalls[x, z, level].Type,
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
                            ConnectToNeighboursOnOtherLevels(currentPhysicsBall, physicsBalls, simulation, size);

                        //Highest level - also attach to ceiling
                        if (level == size.Level - 1)
                            currentPhysicsBall.HandlesTop.TryStore(ConnectBallToCeiling(currentPhysicsBall, ceilingReference, simulation));
                    }
                }
            }

            return physicsBalls;
        }

        /// <summary>
        /// Counts occupied cells among the up-to-12 neighbouring cells of the given cell
        /// (4 on the same level, up to 4 on each adjacent level — the same parity rules as
        /// <see cref="BallsMap.GetNeighbouringCells"/>, but without allocating an enumerator,
        /// since this runs for every ball every frame). Used for ambient occlusion.
        /// </summary>
        /// <param name="occlusionDirection">Sum of the unit vectors pointing at the occupied neighbours
        /// (touching neighbours are always exactly one ball diameter away, so every contribution has length 1).
        /// The part of the ball surface facing this direction is the occluded one.</param>
        public static int CountOccupiedNeighbours(PhysicsBall[,,] balls, XZLevel cell, XZLevel size, out Vector3 occlusionDirection)
        {
            int occupied = 0;
            occlusionDirection = Vector3.Zero;

            if (cell.X - 1 >= 0 && balls[cell.X - 1, cell.Z, cell.Level] != null) { occupied++; occlusionDirection.X -= 1f; }
            if (cell.X + 1 < size.X && balls[cell.X + 1, cell.Z, cell.Level] != null) { occupied++; occlusionDirection.X += 1f; }
            if (cell.Z - 1 >= 0 && balls[cell.X, cell.Z - 1, cell.Level] != null) { occupied++; occlusionDirection.Z -= 1f; }
            if (cell.Z + 1 < size.Z && balls[cell.X, cell.Z + 1, cell.Level] != null) { occupied++; occlusionDirection.Z += 1f; }

            int diagonalShift = (cell.Level % 2) > 0 ? 0 : -1;

            for (int levelOffset = -1; levelOffset <= 1; levelOffset += 2)
            {
                int level = cell.Level + levelOffset;
                if (level < 0 || level >= size.Level) continue;

                float offsetY = levelOffset * Constants.SQRT_TWO * Constants.HALF;

                for (int dX = 0; dX <= 1; dX++)
                    for (int dZ = 0; dZ <= 1; dZ++)
                    {
                        int x = cell.X + dX + diagonalShift;
                        int z = cell.Z + dZ + diagonalShift;

                        if (x >= 0 && z >= 0 && x < size.X && z < size.Z && balls[x, z, level] != null)
                        {
                            occupied++;
                            //Independently of level parity the horizontal offset to a touching cross-level neighbour is ±0.5
                            occlusionDirection += new Vector3(dX - 0.5f, offsetY, dZ - 0.5f);
                        }
                    }
            }

            return occupied;
        }

        /// <summary>
        /// Minimum number of touching same-type balls required for the cluster to be released.
        /// </summary>
        public static readonly int MINIMUM_CLUSTER_SIZE = 3;

        /// <summary>
        /// Checks whether the freshly attached ball completed a cluster of at least <see cref="MINIMUM_CLUSTER_SIZE"/>
        /// touching balls of the same <see cref="BallType"/> and if so, releases the whole cluster: removes all constraints
        /// of its balls (so they fall freely) and removes the balls from both the logical map and <paramref name="physicsBalls"/>.
        /// Balls that lose their connection to the ceiling by that are then released the same way, so everything that
        /// falls, falls as individual unconstrained balls.
        /// </summary>
        /// <param name="releasedInto">Released balls are added here so the caller can keep drawing (and later dispose of) them.</param>
        /// <returns>Total number of released balls, or 0 when the cluster is below the minimum size.</returns>
        public static int ReleaseSameTypeCluster(PhysicsBall attachedBall, PhysicsBall[,,] physicsBalls, BallsMap map, Simulation simulation, List<PhysicsBall> releasedInto)
        {
            List<XZLevel> cluster = map.GetConnectedSameTypeCells(attachedBall.ArrayPosition);
            if (cluster.Count < MINIMUM_CLUSTER_SIZE) return 0;

            XZLevel size = map.GetStaticBallsArraySize();
            List<ConstraintHandle> handleBuffer = new();

            foreach (XZLevel cell in cluster)
                ReleaseBall(cell, physicsBalls, map, simulation, size, handleBuffer, releasedInto);

            //Balls no longer connected to the ceiling would fall as chains still constrained to each other;
            //releasing them explicitly cuts those constraints so they fall as individual balls.
            List<XZLevel> disconnected = map.GetCellsDisconnectedFromCeiling();
            foreach (XZLevel cell in disconnected)
                ReleaseBall(cell, physicsBalls, map, simulation, size, handleBuffer, releasedInto);

            return cluster.Count + disconnected.Count;
        }

        /// <summary>
        /// Releases a single ball from the structure: removes all its constraints, clears their handles from the
        /// neighbouring balls' slots (a stale value could alias a different constraint once the solver reuses the index),
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

            foreach (XZLevel neighbourCell in BallsMap.GetNeighbouringCells(cell, size))
            {
                PhysicsBall neighbour = physicsBalls[neighbourCell.X, neighbourCell.Z, neighbourCell.Level];
                if (neighbour == null) continue;

                foreach (ConstraintHandle handle in handleBuffer) neighbour.ClearStoredHandle(handle);
            }

            simulation.Awakener.AwakenBody(ball.BallReference.Handle); //Make sure the released ball starts falling even if it was asleep

            physicsBalls[cell.X, cell.Z, cell.Level] = null;
            map.RemoveBallAt((byte)cell.X, (byte)cell.Z, (byte)cell.Level);

            releasedInto?.Add(ball);
        }

        /// <summary>
        /// Attaches a freshly placed ball to everything it should be connected to: the ceiling (when on the top level),
        /// neighbours on the same level and neighbours on the levels directly above and below.
        /// The ball must already have its <see cref="PhysicsBall.ArrayPosition"/> set, be present in the static map and in <paramref name="physicsBalls"/>.
        /// </summary>
        public static void AttachBallToStructure(PhysicsBall physicsBall, PhysicsBall[,,] physicsBalls, BallsMap map, Simulation simulation, BodyReference ceilingReference)
        {
            XZLevel size = map.GetStaticBallsArraySize();

            if (physicsBall.ArrayPosition.Level == size.Level - 1)
                physicsBall.HandlesTop.TryStore(ConnectBallToCeiling(physicsBall, ceilingReference, simulation, map.GetRealCenteredPosition(physicsBall.ArrayPosition).ToNumerics()));

            ConnectToNeighboursOnSameLevel(physicsBall, physicsBalls, simulation, size, map);
            ConnectToNeighboursOnOtherLevels(physicsBall, physicsBalls, simulation, size, map);
        }

        /// <summary>
        /// Connects a ball to the occupied neighbouring cells on its own level in all four directions.
        /// Meant for a freshly attached ball, which has no same-level constraints yet, so every neighbour needs a new constraint.
        /// (The build-time pass instead connects only towards +X/+Z from each ball so pairs are not visited twice.)
        /// </summary>
        public static void ConnectToNeighboursOnSameLevel(PhysicsBall physicsBall, PhysicsBall[,,] physicsBalls, Simulation simulation, XZLevel size, BallsMap map = null)
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
        /// Connects a ball to the occupied neighbouring cells on the levels directly above and below.
        /// Takes level parity into account: odd levels are shifted by +0.5 in X and Z, so their neighbours on adjacent
        /// levels sit towards +X/+Z indices, while even levels neighbour towards -X/-Z.
        /// Used both by the build-time pass (from even levels only, so every cross-level pair is visited exactly once)
        /// and when attaching a freshly shot ball (which has no constraints yet).
        /// </summary>
        public static void ConnectToNeighboursOnOtherLevels(PhysicsBall physicsBall, PhysicsBall[,,] physicsBalls, Simulation simulation, XZLevel size, BallsMap map = null)
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

                        PhysicsBall neighbour = physicsBalls[x, z, level];
                        if (neighbour == null) continue;

                        ConstraintHandle handle = ConnectBalls(physicsBall, neighbour, simulation, map);

                        //Constraints to balls below are stored in HandlesBottom, to balls above in HandlesTop, on both sides
                        if (levelOffset < 0)
                        {
                            physicsBall.HandlesBottom.TryStore(handle);
                            neighbour.HandlesTop.TryStore(handle);
                        }
                        else
                        {
                            physicsBall.HandlesTop.TryStore(handle);
                            neighbour.HandlesBottom.TryStore(handle);
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
