using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.IO;

namespace Prazsky.BS3D.GameStructure
{
    public class BallsMap
    {
        private StaticBall[,,] _balls;
        private Matrix[] _transformations;
        private Model _ballModel;

        private static readonly float BALL_RADIUS = Constants.HALF;

        public byte StageSizeX { get; internal set; }
        public byte StageSizeZ { get; internal set; }
        public byte Levels { get; internal set; }
        public bool Centered { get; internal set; } = false;
        public Vector2 BoundingBoxCenter { get; internal set; }


        public BallsMap(byte stageSizeX, byte stageSizeZ, byte levels, Model ballModel)
        {
            if (stageSizeX < 2 || stageSizeZ < 2 || levels < 2) throw new ArgumentException($"Minimum BallsMap size is 2×2×2, given arguments: {stageSizeX}, {stageSizeZ}, {levels}");

            StageSizeX = stageSizeX;
            StageSizeZ = stageSizeZ;
            Levels = levels;

            InitializeModel(ballModel);

            _balls = new StaticBall[StageSizeX, StageSizeZ, Levels];
        }

        public BallsMap(string fileNameForDeserialization, Model ballModel)
        {
            DeserializeJson(fileNameForDeserialization);
            InitializeModel(ballModel);
        }

        private void InitializeModel(Model ballModel)
        {
            _ballModel = ballModel;
            _transformations = new Matrix[ballModel.Bones.Count];
            ballModel.CopyAbsoluteBoneTransformsTo(_transformations);
        }

        /// <summary>
        /// Creates new <see cref="StaticBall"/> of given type <see cref="BallType"/>, computes its real position and places it into internal three-dimensional array.
        /// If there is already a <see cref="StaticBall"/> at that position, it is replaced without warning.
        /// </summary>
        /// <param name="stageX">The X coordinate in the given level.</param>
        /// <param name="stageZ">The Z coordinate in the given level.</param>
        /// <param name="level">Level.</param>
        /// <param name="type">Ball type.</param>
        /// <returns>Created static ball.</returns>
        public StaticBall PutBallAt(byte stageX, byte stageZ, byte level, BallType type = BallType.Type4)
        {
            if (stageX >= StageSizeX || stageZ >= StageSizeZ || level >= Levels) throw new ArgumentOutOfRangeException($"Invalid requested ball position, array size is: {StageSizeX} × {StageSizeZ} × {Levels}");

            Vector3 realPosition = GetRealPosition(stageX, stageZ, level);
            if (Centered) realPosition = ComputeCentered(realPosition);
#if DEBUG
            Console.WriteLine($"Putting ball at stageX: {stageX}; stageZ: {stageZ}; level: {level}; Real position: {realPosition}");
#endif
            var ball = new StaticBall(realPosition, type, _transformations);
			_balls[stageX, stageZ, level] = ball;

            return ball;
        }

        public void RemoveBallAt(byte stageX, byte stageZ, byte level)
        {
            if (stageX >= StageSizeX || stageZ >= StageSizeZ || level >= Levels) throw new ArgumentOutOfRangeException($"Invalid requested ball position, array size is: {StageSizeX} × {StageSizeZ} × {Levels}");

#if DEBUG
            Console.WriteLine($"Removing ball at stageX: {stageX}; stageZ: {stageZ}; level: {level}");
#endif

            _balls[stageX, stageZ, level] = null;
        }

        public static Vector3 GetRealPosition(byte stageX, byte stageZ, byte level)
        {
            bool isShifted = (level % 2) > 0;

            float realPosX = stageX;
            if (isShifted) realPosX += Constants.HALF;

            float realPosZ = stageZ;
            if (isShifted) realPosZ += Constants.HALF;

            float realPosY = level / Constants.SQRT_TWO;

            return new Vector3(realPosX, realPosY, realPosZ);
        }

        public Vector3 GetRealCenteredPosition(XZLevel arrayPosition)
        {
            var realPos = GetRealPosition((byte)arrayPosition.X, (byte)arrayPosition.Z, (byte)arrayPosition.Level);
            return ComputeCentered(realPos);
        }

        //WIP
        public Vector3 PutBallAtClosestEmptyCeilingPosition(Vector3 position, out XZLevel arrayPosition, BallType type = BallType.Type4)
        {
            bool isShifted = true; //Currently computes only for top level (below ceiling)
            byte level = 9; //Currently only level 9 (top level)

            arrayPosition = new XZLevel(-1, -1, -1);

            Vector3 uncentered = ComputeUncentered(position);

            if (isShifted) uncentered = new Vector3(uncentered.X - Constants.HALF, uncentered.Y, uncentered.Z - Constants.HALF);

            if (uncentered.X < -0.5f || uncentered.X >= 255.5f || uncentered.Z < -0.5f || uncentered.Z >= 255.5f) return new Vector3(float.MinValue);

            byte x = Convert.ToByte(uncentered.X);
            //byte y = Convert.ToByte(uncentered.Y);
            byte z = Convert.ToByte(uncentered.Z);

            arrayPosition.X = x;
            arrayPosition.Z = z;
            arrayPosition.Level = level;

            if (x >= StageSizeX || z >= StageSizeZ //Outside of map
                || _balls[x, z, level] != null) //There is already a ball there
                return new Vector3(float.MinValue);

            return PutBallAt(x, z, level, type).Position;
        }

        /// <summary>
        /// Enumerates all in-bounds cells that geometrically touch the given cell: four on the same level and up to four
        /// on each adjacent level. Odd levels are shifted by +0.5 in X and Z, so their neighbours on adjacent levels sit
        /// towards +X/+Z indices, while even levels neighbour towards -X/-Z.
        /// </summary>
        public static IEnumerable<XZLevel> GetNeighbouringCells(XZLevel cell, XZLevel size)
        {
            //Same level
            if (cell.X - 1 >= 0) yield return new XZLevel(cell.X - 1, cell.Z, cell.Level);
            if (cell.X + 1 < size.X) yield return new XZLevel(cell.X + 1, cell.Z, cell.Level);
            if (cell.Z - 1 >= 0) yield return new XZLevel(cell.X, cell.Z - 1, cell.Level);
            if (cell.Z + 1 < size.Z) yield return new XZLevel(cell.X, cell.Z + 1, cell.Level);

            //Levels above and below
            int diagonalShift = (cell.Level % 2) > 0 ? 0 : -1;

            for (int levelOffset = -1; levelOffset <= 1; levelOffset += 2)
            {
                int level = cell.Level + levelOffset;
                if (level < 0 || level >= size.Level) continue;

                for (int dX = 0; dX <= 1; dX++)
                    for (int dZ = 0; dZ <= 1; dZ++)
                    {
                        int x = cell.X + dX + diagonalShift;
                        int z = cell.Z + dZ + diagonalShift;

                        if (x >= 0 && z >= 0 && x < size.X && z < size.Z) yield return new XZLevel(x, z, level);
                    }
            }
        }

        /// <summary>
        /// Puts a ball into the empty cell neighbouring the <paramref name="nextTo"/> cell that is closest to the given position.
        /// Candidate cells come from <see cref="GetNeighbouringCells"/>.
        /// </summary>
        /// <param name="position">Centered (world) position the new ball should be placed closest to, typically the contact point.</param>
        /// <param name="nextTo">Cell of the existing ball that was hit.</param>
        /// <param name="arrayPosition">Cell the ball was placed into.</param>
        /// <param name="type">Type of the placed ball.</param>
        /// <returns>Centered position of the placed ball, or a <see cref="float.MinValue"/> vector when no neighbouring cell is free.</returns>
        public Vector3 PutBallAtClosestEmptyPositionNextTo(Vector3 position, XZLevel nextTo, out XZLevel arrayPosition, BallType type = BallType.Type4)
        {
            arrayPosition = new XZLevel(-1, -1, -1);

            float closestDistanceSquared = float.MaxValue;

            foreach (XZLevel candidate in GetNeighbouringCells(nextTo, new XZLevel(StageSizeX, StageSizeZ, Levels)))
            {
                if (_balls[candidate.X, candidate.Z, candidate.Level] != null) continue;

                float distanceSquared = Vector3.DistanceSquared(GetRealCenteredPosition(candidate), position);

                if (distanceSquared < closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    arrayPosition = candidate;
                }
            }

            if (arrayPosition.X < 0) return new Vector3(float.MinValue);

            return PutBallAt((byte)arrayPosition.X, (byte)arrayPosition.Z, (byte)arrayPosition.Level, type).Position;
        }

        /// <summary>
        /// Finds the connected cluster of balls of the same <see cref="BallType"/> as the ball at <paramref name="start"/>,
        /// walking over touching cells (see <see cref="GetNeighbouringCells"/>). The start cell itself is included.
        /// Returns an empty list when the start cell is empty.
        /// </summary>
        public List<XZLevel> GetConnectedSameTypeCells(XZLevel start)
        {
            List<XZLevel> cluster = new();

            StaticBall startBall = _balls[start.X, start.Z, start.Level];
            if (startBall == null) return cluster;

            XZLevel size = new(StageSizeX, StageSizeZ, Levels);

            var visited = new bool[StageSizeX, StageSizeZ, Levels];
            var toVisit = new Queue<XZLevel>();

            visited[start.X, start.Z, start.Level] = true;
            toVisit.Enqueue(start);

            while (toVisit.Count > 0)
            {
                XZLevel cell = toVisit.Dequeue();
                cluster.Add(cell);

                foreach (XZLevel neighbour in GetNeighbouringCells(cell, size))
                {
                    if (visited[neighbour.X, neighbour.Z, neighbour.Level]) continue;
                    visited[neighbour.X, neighbour.Z, neighbour.Level] = true;

                    StaticBall neighbourBall = _balls[neighbour.X, neighbour.Z, neighbour.Level];
                    if (neighbourBall == null || neighbourBall.Type != startBall.Type) continue;

                    toVisit.Enqueue(neighbour);
                }
            }

            return cluster;
        }

        /// <summary>
        /// Returns cells of balls that are no longer connected to the ceiling: walks the touching-neighbour graph
        /// (see <see cref="GetNeighbouringCells"/>) from all balls on the top level (those hang from the ceiling)
        /// and collects every ball the walk did not reach.
        /// </summary>
        public List<XZLevel> GetCellsDisconnectedFromCeiling()
        {
            XZLevel size = new(StageSizeX, StageSizeZ, Levels);

            var visited = new bool[StageSizeX, StageSizeZ, Levels];
            var toVisit = new Queue<XZLevel>();

            byte topLevel = (byte)(Levels - 1);
            for (byte x = 0; x < StageSizeX; x++)
                for (byte z = 0; z < StageSizeZ; z++)
                    if (_balls[x, z, topLevel] != null)
                    {
                        visited[x, z, topLevel] = true;
                        toVisit.Enqueue(new XZLevel(x, z, topLevel));
                    }

            while (toVisit.Count > 0)
            {
                XZLevel cell = toVisit.Dequeue();

                foreach (XZLevel neighbour in GetNeighbouringCells(cell, size))
                {
                    if (visited[neighbour.X, neighbour.Z, neighbour.Level]) continue;
                    if (_balls[neighbour.X, neighbour.Z, neighbour.Level] == null) continue;

                    visited[neighbour.X, neighbour.Z, neighbour.Level] = true;
                    toVisit.Enqueue(neighbour);
                }
            }

            List<XZLevel> disconnected = new();

            for (byte level = 0; level < Levels; level++)
                for (byte x = 0; x < StageSizeX; x++)
                    for (byte z = 0; z < StageSizeZ; z++)
                        if (_balls[x, z, level] != null && !visited[x, z, level])
                            disconnected.Add(new XZLevel(x, z, level));

            return disconnected;
        }

        public StaticBall[,,] GetStaticBallsArray() => _balls;

        public XZLevel GetStaticBallsArraySize() => XZLevel.FromArray(_balls);

        public void Clear()
        {
            if (_balls == null) return;

            for (byte level = 0; level < Levels; level++)
                for (byte x = 0; x < StageSizeX; x++)
                    for (byte z = 0; z < StageSizeZ; z++)
                        _balls[x, z, level] = null;
        }

        public int GetBallsCount()
        {
            int count = 0;

            for (byte level = 0; level < Levels; level++)
                for (byte x = 0; x < StageSizeX; x++)
                    for (byte z = 0; z < StageSizeZ; z++)
                        if (_balls[x, z, level] != null)
                            count++;
            return count;
        }

        public void Draw(ICamera camera)
        {
            for (byte level = 0; level < Levels; level++)
                for (byte x = 0; x < StageSizeX; x++)
                    for (byte z = 0; z < StageSizeZ; z++)
                        if (_balls[x, z, level] != null)
                            _balls[x, z, level].Draw(camera, _ballModel);
        }

        public void SerializeAsJson(string fileName)
        {
            var ballPositionTypes = BuildBallPositionTypes();
            var json = JsonConvert.SerializeObject(ballPositionTypes);
            File.WriteAllText(fileName, json);
            Console.WriteLine(json);
        }

        public void DeserializeJson(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) throw new ArgumentNullException(nameof(fileName));

            BallPositionTypes ballPositionTypes;

            using StreamReader reader = File.OpenText(fileName);
            var serializer = new JsonSerializer();
            ballPositionTypes = (BallPositionTypes)serializer.Deserialize(reader, typeof(BallPositionTypes));

            #region Basic validation

            if (ballPositionTypes == null || ballPositionTypes.Balls == null) return;

            if (ballPositionTypes.Balls.Rank != 3)
                throw new InvalidDataException("Deserialized data invalid");

            XZLevel size = XZLevel.FromArray(ballPositionTypes.Balls);

            if (size.Level > byte.MaxValue || size.X > byte.MaxValue || size.Z > byte.MaxValue)
                throw new InvalidDataException("Deserialized data invalid");

            #endregion

            StageSizeX = (byte)size.Level;
            StageSizeZ = (byte)size.X;
            Levels = (byte)size.Z;

            BuildMapFromBallPositionTypes(ballPositionTypes);
        }

        private BallPositionTypes BuildBallPositionTypes()
        {
            BallPositionTypes ballPositionTypes = new();

            ballPositionTypes.Balls = new BallPositionType[StageSizeX, StageSizeZ, Levels];

            for (byte level = 0; level < Levels; level++)
                for (byte x = 0; x < StageSizeX; x++)
                    for (byte z = 0; z < StageSizeZ; z++)
                        if (_balls[x, z, level] != null)
                            ballPositionTypes.Balls[x, z, level] = new BallPositionType()
                            {
                                PositionX = _balls[x, z, level].Position.X,
                                PositionY = _balls[x, z, level].Position.Y,
                                PositionZ = _balls[x, z, level].Position.Z,
                                Type = _balls[x, z, level].Type
                            };

            return ballPositionTypes;
        }

        private void BuildMapFromBallPositionTypes(BallPositionTypes ballPositionTypes)
        {
            _balls = new StaticBall[StageSizeX, StageSizeZ, Levels];

            for (byte level = 0; level < Levels; level++)
                for (byte x = 0; x < StageSizeX; x++)
                    for (byte z = 0; z < StageSizeZ; z++)
                        if (ballPositionTypes.Balls[x, z, level] != null)
                            PutBallAt(x, z, level, ballPositionTypes.Balls[x, z, level].Type);
        }

        private Vector3 ComputeUncentered(Vector3 position)
        {
            if (!Centered) return position;

            return new(
                position.X + BoundingBoxCenter.X + BALL_RADIUS,
                position.Y,
                position.Z + BoundingBoxCenter.Y + BALL_RADIUS);
        }

        private Vector3 ComputeCentered(Vector3 position)
        {
            if (!Centered) return position;

            return new(
                position.X - BoundingBoxCenter.X - BALL_RADIUS,
                position.Y,
                position.Z - BoundingBoxCenter.Y - BALL_RADIUS);
        }

        public void Center()
        {
            float minPosX = float.MaxValue, minPosZ = float.MaxValue;
            float maxPosX = float.MinValue, maxPosZ = float.MinValue;

            for (byte x = 0; x < StageSizeX; x++)
                for (byte z = 0; z < StageSizeZ; z++)
                    if (_balls[x, z, 9] != null)
                    {
                        StaticBall currentBall = _balls[x, z, 9];

                        if (currentBall.Position.X < minPosX) minPosX = currentBall.Position.X;
                        if (currentBall.Position.Z < minPosZ) minPosZ = currentBall.Position.Z;

                        if (currentBall.Position.X > maxPosX) maxPosX = currentBall.Position.X;
                        if (currentBall.Position.Z > maxPosZ) maxPosZ = currentBall.Position.Z;
                    }

            Vector2 minPos = new(minPosX, minPosZ);
            Vector2 maxPos = new(maxPosX, maxPosZ);

            BoundingBoxCenter = (maxPos - minPos) / 2f;

#if DEBUG
            Console.WriteLine("Map minPos: " + minPos);
            Console.WriteLine("Map maxPos: " + maxPos);
            Console.WriteLine("Map AABB center: " + BoundingBoxCenter);
#endif

            for (byte level = 0; level < Levels; level++)
                for (byte x = 0; x < StageSizeX; x++)
                    for (byte z = 0; z < StageSizeZ; z++)
                        if (_balls[x, z, level] != null)
                        {
                            _balls[x, z, level].Position = new(
                                _balls[x, z, level].Position.X - BoundingBoxCenter.X - BALL_RADIUS,
                                _balls[x, z, level].Position.Y,
                                _balls[x, z, level].Position.Z - BoundingBoxCenter.Y - BALL_RADIUS
                                );
                        }

            Centered = true;
        }
    }
}