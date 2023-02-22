using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.Core.Tools;
using System;
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

                        #region level

                        EnsureConnectedOnSameLevel(staticBalls, new(x, z, level), currentPhysicsBall, physicsBalls, simulation, size);

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

                        //Highest level - only attach to ceiling
                        if (level == size.Level - 1)
                            currentPhysicsBall.HandlesTop.Handle1 = ConnectBallToCeiling(currentPhysicsBall, ceilingReference, simulation);
                    }
                }
            }

            return physicsBalls;
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

            Vector3 offsetAB = GetLocalOffset(ballAPosition, ballBPosition);
            Vector3 offsetBA = Vector3.Negate(offsetAB); //I could use GetLocalOffset again with inverted parameters, but changing polarity of the first vector is enough

            BallSocket ballSocket = new()
            { 
                LocalOffsetA = offsetAB,
                LocalOffsetB = offsetBA,
                SpringSettings = SPRING_SETTINGS
            };

            return simulation.Solver.Add(physicsBallA.BallReference.Handle, physicsBallB.BallReference.Handle, ballSocket);
        }

        private static ConstraintHandle ConnectBallToCeiling(PhysicsBall physicsBall, BodyReference ceilingReference, Simulation simulation)
        {
            Vector3 offsetBall = new(0f, BALL_RADIUS, 0f);
            Vector3 offsetCeiling = new(physicsBall.BallReference.Pose.Position.X, -BALL_RADIUS, physicsBall.BallReference.Pose.Position.Z);

            BallSocket ballSocket = new()
            {
                LocalOffsetA = offsetBall,
                LocalOffsetB = offsetCeiling,
                SpringSettings = SPRING_SETTINGS
            };

            return simulation.Solver.Add(physicsBall.BallReference.Handle, ceilingReference.Handle, ballSocket);
        }

        public static ConstraintHandle ConnectBallToCeiling(PhysicsBall physicsBall, BodyReference ceilingReference, Simulation simulation, Vector3 ceilingPosition)
        {
            Vector3 offsetBall = new(0f, BALL_RADIUS, 0f);
            Vector3 offsetCeiling = new(ceilingPosition.X, -BALL_RADIUS, ceilingPosition.Z);

            BallSocket ballSocket = new()
            {
                LocalOffsetA = offsetBall,
                LocalOffsetB = offsetCeiling,
                SpringSettings = SPRING_SETTINGS
            };

            return simulation.Solver.Add(physicsBall.BallReference.Handle, ceilingReference.Handle, ballSocket);
        }

        private static Vector3 GetLocalOffset(Vector3 ballAPosition, Vector3 ballBPosition)
        {
            return Vector3.Subtract(ballBPosition, ballAPosition) / 2;
        }
    }
}

// Level is built from left back to right front
// 1 2 3
// 4 5 6
// 7 8 9