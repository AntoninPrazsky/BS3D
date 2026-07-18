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

            PhysicsBall[,,] physicsBalls = new PhysicsBall[size.Level, size.X, size.Z]; //Same three-dimensional array for physical balls

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

                        #region level

                        EnsureConnectedOnSameLevel(staticBalls, new(x, z, level), currentPhysicsBall, physicsBalls, simulation, size);

                        #endregion

                        //Cross-level connections are created only from even (unshifted) levels: adjacent levels always differ in parity,
                        //and the (x - 1..x, z - 1..z) index offsets are geometrically correct only when looking from an even level.
                        //Looking from an odd (shifted by +0.5 in X/Z) level, the same offsets would enumerate distant non-touching cells;
                        //the true neighbours of odd-level balls are all covered by the even side of each pair.
                        if ((level % 2) == 0)
                        {
                            #region level - 1

                            //1
                            //x - 1, y - 1, z - 1
                            if (x - 1 >= 0 && level - 1 >= 0 && z - 1 >= 0)
                                if (staticBalls[x - 1, z - 1, level - 1] != null)
                                    EnsureConnected(ref currentPhysicsBall, ref physicsBalls[x - 1, z - 1, level - 1], ConstraintType.Type1, simulation);

                            //2
                            //x - 1, y - 1, z
                            if (x - 1 >= 0 && level - 1 >= 0)
                                if (staticBalls[x - 1, z, level - 1] != null)
                                    EnsureConnected(ref currentPhysicsBall, ref physicsBalls[x - 1, z, level - 1], ConstraintType.Type2, simulation);

                            //3
                            //x,     y - 1, z - 1
                            if (level - 1 >= 0 && z - 1 >= 0)
                                if (staticBalls[x, z - 1, level - 1] != null)
                                    EnsureConnected(ref currentPhysicsBall, ref physicsBalls[x, z - 1, level - 1], ConstraintType.Type3, simulation);

                            //4
                            //x,     y - 1, z
                            if (level - 1 >= 0)
                                if (staticBalls[x, z, level - 1] != null)
                                    EnsureConnected(ref currentPhysicsBall, ref physicsBalls[x, z, level - 1], ConstraintType.Type4, simulation);

                            #endregion

                            #region level + 1

                            //9
                            //x - 1, y + 1, z - 1
                            if (x - 1 >= 0 && level + 1 < size.Level && z - 1 >= 0)
                                if (staticBalls[x - 1, z - 1, level + 1] != null)
                                    EnsureConnected(ref currentPhysicsBall, ref physicsBalls[x - 1, z - 1, level + 1], ConstraintType.Type9, simulation);

                            //10
                            //x - 1, y + 1, z
                            if (x - 1 >= 0 && level + 1 < size.Level)
                                if (staticBalls[x - 1, z, level + 1] != null)
                                    EnsureConnected(ref currentPhysicsBall, ref physicsBalls[x - 1, z, level + 1], ConstraintType.Type10, simulation);

                            //11
                            //x,     y + 1, z - 1
                            if (level + 1 < size.Level && z - 1 >= 0)
                                if (staticBalls[x, z - 1, level + 1] != null)
                                    EnsureConnected(ref currentPhysicsBall, ref physicsBalls[x, z - 1, level + 1], ConstraintType.Type11, simulation);

                            //12
                            //x,     y + 1, z
                            if (level + 1 < size.Level)
                                if (staticBalls[x, z, level + 1] != null)
                                    EnsureConnected(ref currentPhysicsBall, ref physicsBalls[x, z, level + 1], ConstraintType.Type12, simulation);

                            #endregion
                        }

                        //Highest level - only attach to ceiling
                        if (level == size.Level - 1)
                            currentPhysicsBall.HandlesTop.Handle1 = ConnectBallToCeiling(currentPhysicsBall, ceilingReference, simulation);
                    }
                }
            }

            return physicsBalls;
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

            EnsureConnectedOnSameLevel(map.GetStaticBallsArray(), physicsBall.ArrayPosition, physicsBall, physicsBalls, simulation, size, map);
            ConnectToNeighboursOnOtherLevels(physicsBall, physicsBalls, simulation, size, map);
        }

        /// <summary>
        /// Connects a freshly attached ball to the occupied neighbouring cells on the levels directly above and below.
        /// Unlike the build-time pass this takes level parity into account: odd levels are shifted by +0.5 in X and Z,
        /// so their neighbours on adjacent levels sit towards +X/+Z indices, while even levels neighbour towards -X/-Z.
        /// Constraints are created directly instead of via EnsureConnected: the new ball has no constraints yet and every
        /// neighbour is visited exactly once, so the build-time deduplication (which reads slot state of already connected balls) must not run here.
        /// </summary>
        public static void ConnectToNeighboursOnOtherLevels(PhysicsBall physicsBall, PhysicsBall[,,] physicsBalls, Simulation simulation, XZLevel size, BallsMap map)
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

                        //The neighbour's slots may all be taken (the build-time pass can consume them); the handle then stays
                        //tracked only on the new ball, which is enough for removal thanks to the ConstraintExists guards.
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

        public static void EnsureConnectedOnSameLevel(
            StaticBall[,,] staticBalls,
            XZLevel arrayPosition,
            PhysicsBall currentPhysicsBall,
            PhysicsBall[,,] physicsBalls,
            Simulation simulation,
            XZLevel size,
            BallsMap map = null)
        {
            //5
            //x - 1, y, z
            if (arrayPosition.X - 1 >= 0)
                if (staticBalls[arrayPosition.X - 1, arrayPosition.Z, arrayPosition.Level] != null)
                    EnsureConnected(ref currentPhysicsBall, ref physicsBalls[arrayPosition.X - 1, arrayPosition.Z, arrayPosition.Level], ConstraintType.Type5, simulation, map);

            //6
            //x,     y, z - 1
            if (arrayPosition.Z - 1 >= 0)
                if (staticBalls[arrayPosition.X, arrayPosition.Z - 1, arrayPosition.Level] != null)
                    EnsureConnected(ref currentPhysicsBall, ref physicsBalls[arrayPosition.X, arrayPosition.Z - 1, arrayPosition.Level], ConstraintType.Type6, simulation, map);

            //7
            //x,     y, z + 1
            if (arrayPosition.Z + 1 < size.Z)
                if (staticBalls[arrayPosition.X, arrayPosition.Z + 1, arrayPosition.Level] != null)
                    EnsureConnected(ref currentPhysicsBall, ref physicsBalls[arrayPosition.X, arrayPosition.Z + 1, arrayPosition.Level], ConstraintType.Type7, simulation, map);

            //8
            //x + 1, y, z
            if (arrayPosition.X + 1 < size.X)
                if (staticBalls[arrayPosition.X + 1, arrayPosition.Z, arrayPosition.Level] != null)
                    EnsureConnected(ref currentPhysicsBall, ref physicsBalls[arrayPosition.X + 1, arrayPosition.Z, arrayPosition.Level], ConstraintType.Type8, simulation, map);
        }

        private static void EnsureConnected(
            ref PhysicsBall ballA,
            ref PhysicsBall ballB,
            ConstraintType constraintType,
            Simulation simulation,
            BallsMap map = null)
        {
            if (constraintType == ConstraintType.None) return;

            //1 4 under → 1
            //4 1 under → 2

            //2 3 under → 3
            //3 2 under → 4

            //1 4 same → 5
            //4 1 same → 6

            //2 3 same → 7
            //3 2 same → 8

            //1 4 over → 9
            //4 1 over → 10

            //2 3 over → 11
            //3 2 over → 12

            switch (constraintType)
            {
                #region HandlesBottom

                case ConstraintType.Type1:
                    //1
                    if (ballA.HandlesBottom.Handle1.Value >= 0) ballA.HandlesBottom.Handle4 = ballB.HandlesBottom.Handle1;
                    else
                    {
                        ConstraintHandle constraintHandle = ConnectBalls(ballA, ballB, simulation, map);
                        ballA.HandlesBottom.Handle4 = constraintHandle;
                        ballB.HandlesBottom.Handle1 = constraintHandle;
                    }
                    break;

                case ConstraintType.Type2:
                    //2
                    if (ballA.HandlesBottom.Handle4.Value >= 0) ballA.HandlesBottom.Handle1 = ballB.HandlesBottom.Handle4;
                    else
                    {
                        ConstraintHandle constraintHandle = ConnectBalls(ballA, ballB, simulation, map);
                        ballA.HandlesBottom.Handle1 = constraintHandle;
                        ballB.HandlesBottom.Handle4 = constraintHandle;
                    }
                    break;

                case ConstraintType.Type3:
                    //3
                    if (ballA.HandlesBottom.Handle2.Value >= 0) ballA.HandlesBottom.Handle3 = ballB.HandlesBottom.Handle2;
                    else
                    {
                        ConstraintHandle constraintHandle = ConnectBalls(ballA, ballB, simulation, map);
                        ballA.HandlesBottom.Handle3 = constraintHandle;
                        ballB.HandlesBottom.Handle2 = constraintHandle;
                    }
                    break;

                case ConstraintType.Type4:
                    //4
                    if (ballA.HandlesBottom.Handle3.Value >= 0) ballA.HandlesBottom.Handle2 = ballB.HandlesBottom.Handle3;
                    else
                    {
                        ConstraintHandle constraintHandle = ConnectBalls(ballA, ballB, simulation, map);
                        ballA.HandlesBottom.Handle2 = constraintHandle;
                        ballB.HandlesBottom.Handle3 = constraintHandle;
                    }
                    break;

                #endregion

                #region HandlesMiddle

                case ConstraintType.Type5:
                    //5
                    if (ballA.HandlesMiddle.Handle1.Value >= 0) ballA.HandlesMiddle.Handle4 = ballB.HandlesMiddle.Handle1;
                    else
                    {
                        ConstraintHandle constraintHandle = ConnectBalls(ballA, ballB, simulation, map);
                        ballA.HandlesMiddle.Handle1 = constraintHandle;
                        ballB.HandlesMiddle.Handle4 = constraintHandle;
                    }
                    break;

                case ConstraintType.Type6:
                    //6
                    if (ballA.HandlesMiddle.Handle4.Value >= 0) ballA.HandlesMiddle.Handle1 = ballB.HandlesMiddle.Handle4;
                    else
                    {
                        ConstraintHandle constraintHandle = ConnectBalls(ballA, ballB, simulation, map);
                        ballA.HandlesMiddle.Handle4 = constraintHandle;
                        ballB.HandlesMiddle.Handle1 = constraintHandle;
                    }
                    break;

                case ConstraintType.Type7:
                    //7
                    if (ballA.HandlesMiddle.Handle2.Value >= 0) ballA.HandlesMiddle.Handle3 = ballB.HandlesMiddle.Handle2;
                    else
                    {
                        ConstraintHandle constraintHandle = ConnectBalls(ballA, ballB, simulation, map);
                        ballA.HandlesMiddle.Handle3 = constraintHandle;
                        ballB.HandlesMiddle.Handle2 = constraintHandle;
                    }
                    break;

                case ConstraintType.Type8:
                    //8
                    if (ballA.HandlesMiddle.Handle3.Value >= 0) ballA.HandlesMiddle.Handle2 = ballB.HandlesMiddle.Handle3;
                    else
                    {
                        ConstraintHandle constraintHandle = ConnectBalls(ballA, ballB, simulation, map);
                        ballA.HandlesMiddle.Handle2 = constraintHandle;
                        ballB.HandlesMiddle.Handle3 = constraintHandle;
                    }
                    break;

                #endregion

                #region HandlesTop

                case ConstraintType.Type9:
                    //9
                    if (ballA.HandlesTop.Handle1.Value >= 0) ballA.HandlesTop.Handle4 = ballB.HandlesTop.Handle1;
                    else
                    {
                        ConstraintHandle constraintHandle = ConnectBalls(ballA, ballB, simulation, map);
                        ballA.HandlesTop.Handle1 = constraintHandle;
                        ballB.HandlesTop.Handle4 = constraintHandle;
                    }
                    break;

                case ConstraintType.Type10:
                    //10
                    if (ballA.HandlesTop.Handle4.Value >= 0) ballA.HandlesTop.Handle1 = ballB.HandlesTop.Handle4;
                    else
                    {
                        ConstraintHandle constraintHandle = ConnectBalls(ballA, ballB, simulation, map);
                        ballA.HandlesTop.Handle4 = constraintHandle;
                        ballB.HandlesTop.Handle1 = constraintHandle;
                    }

                    break;

                case ConstraintType.Type11:
                    //11
                    if (ballA.HandlesTop.Handle2.Value >= 0) ballA.HandlesTop.Handle3 = ballB.HandlesTop.Handle2;
                    else
                    {
                        ConstraintHandle constraintHandle = ConnectBalls(ballA, ballB, simulation, map);
                        ballA.HandlesTop.Handle3 = constraintHandle;
                        ballB.HandlesTop.Handle2 = constraintHandle;
                    }

                    break;

                case ConstraintType.Type12:
                    //12
                    if (ballA.HandlesTop.Handle3.Value >= 0) ballA.HandlesTop.Handle2 = ballB.HandlesTop.Handle3;
                    else
                    {
                        ConstraintHandle constraintHandle = ConnectBalls(ballA, ballB, simulation, map);
                        ballA.HandlesTop.Handle2 = constraintHandle;
                        ballB.HandlesTop.Handle3 = constraintHandle;
                    }
                    break;

                #endregion
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