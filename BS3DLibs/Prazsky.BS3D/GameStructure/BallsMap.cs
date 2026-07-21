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

        /// <summary>
        /// Empty levels added below the layout of legacy map files, which carried no play field size.
        /// Gives the structure room to grow downwards when shot balls attach.
        /// Must be an even number so the layout keeps its level parity (odd levels are shifted by +0.5 in X and Z);
        /// an odd offset would flip the parity of every layer and change how shaped layouts nest into each other.
        /// </summary>
        private static readonly byte DEFAULT_EXTRA_LEVELS = 6;

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
            byte level = (byte)(Levels - 1); //Balls hitting the ceiling always land on the top level
            bool isShifted = (level % 2) > 0;

            arrayPosition = new XZLevel(-1, -1, -1);

            Vector3 uncentered = ComputeUncentered(position);

            if (isShifted) uncentered = new Vector3(uncentered.X - Constants.HALF, uncentered.Y, uncentered.Z - Constants.HALF);

            //Convert.ToByte rounds to nearest, so [-0.5, byte.MaxValue + 0.5) is exactly the range that
            //maps to a valid byte without overflowing
            if (uncentered.X < -0.5f || uncentered.X >= 255.5f || uncentered.Z < -0.5f || uncentered.Z >= 255.5f) return new Vector3(float.MinValue);

            byte x = Convert.ToByte(uncentered.X);
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
        /// on each adjacent level. Odd levels are shifted by +0.5 in X and Z, so their neighbors on adjacent levels sit
        /// towards +X/+Z indices, while even levels neighbor towards -X/-Z.
        /// </summary>
        public static IEnumerable<XZLevel> GetNeighboringCells(XZLevel cell, XZLevel size)
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
        /// Counts occupied cells among the up-to-12 cells touching the given one (the same parity rules as
        /// <see cref="GetNeighboringCells"/>, but without allocating an enumerator, since this runs for every
        /// ball every frame). Used for ambient occlusion, over either the logical or the physics ball array.
        /// </summary>
        /// <param name="occlusionDirection">Sum of the unit vectors pointing at the occupied neighbors
        /// (touching neighbors are always exactly one ball diameter away, so every contribution has length 1).
        /// The part of the ball surface facing this direction is the occluded one.</param>
        public static int CountOccupiedNeighbors<T>(T[,,] balls, XZLevel cell, XZLevel size, out Vector3 occlusionDirection) where T : class
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
                            //Independently of level parity the horizontal offset to a touching cross-level neighbor is ±0.5
                            occlusionDirection += new Vector3(dX - Constants.HALF, offsetY, dZ - Constants.HALF);
                        }
                    }
            }

            return occupied;
        }

        /// <summary>
        /// Puts a ball into the empty cell neighboring the <paramref name="nextTo"/> cell that is closest to the given position.
        /// Candidate cells come from <see cref="GetNeighboringCells"/>.
        /// </summary>
        /// <param name="position">Centered (world) position the new ball should be placed closest to, typically the contact point.</param>
        /// <param name="nextTo">Cell of the existing ball that was hit.</param>
        /// <param name="arrayPosition">Cell the ball was placed into.</param>
        /// <param name="type">Type of the placed ball.</param>
        /// <returns>Centered position of the placed ball, or a <see cref="float.MinValue"/> vector when no neighboring cell is free.</returns>
        public Vector3 PutBallAtClosestEmptyPositionNextTo(Vector3 position, XZLevel nextTo, out XZLevel arrayPosition, BallType type = BallType.Type4)
        {
            arrayPosition = new XZLevel(-1, -1, -1);

            float closestDistanceSquared = float.MaxValue;

            foreach (XZLevel candidate in GetNeighboringCells(nextTo, new XZLevel(StageSizeX, StageSizeZ, Levels)))
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
        /// walking over touching cells (see <see cref="GetNeighboringCells"/>). The start cell itself is included.
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

                foreach (XZLevel neighbor in GetNeighboringCells(cell, size))
                {
                    if (visited[neighbor.X, neighbor.Z, neighbor.Level]) continue;
                    visited[neighbor.X, neighbor.Z, neighbor.Level] = true;

                    StaticBall neighborBall = _balls[neighbor.X, neighbor.Z, neighbor.Level];
                    if (neighborBall == null || neighborBall.Type != startBall.Type) continue;

                    toVisit.Enqueue(neighbor);
                }
            }

            return cluster;
        }

        /// <summary>
        /// Returns cells of balls that are no longer connected to the ceiling: walks the touching-neighbor graph
        /// (see <see cref="GetNeighboringCells"/>) from all balls on the top level (those hang from the ceiling)
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

                foreach (XZLevel neighbor in GetNeighboringCells(cell, size))
                {
                    if (visited[neighbor.X, neighbor.Z, neighbor.Level]) continue;
                    if (_balls[neighbor.X, neighbor.Z, neighbor.Level] == null) continue;

                    visited[neighbor.X, neighbor.Z, neighbor.Level] = true;
                    toVisit.Enqueue(neighbor);
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

            if (ballPositionTypes.Levels == 0)
            {
                //Legacy file without play field size: the field is the layout itself,
                //plus a few empty levels below so the structure has room to grow
                StageSizeX = (byte)size.X;
                StageSizeZ = (byte)size.Z;
                Levels = (byte)Math.Min(byte.MaxValue, size.Level + DEFAULT_EXTRA_LEVELS);
            }
            else
            {
                //The play field can never be smaller than the stored layout
                StageSizeX = Math.Max(ballPositionTypes.StageSizeX, (byte)size.X);
                StageSizeZ = Math.Max(ballPositionTypes.StageSizeZ, (byte)size.Z);
                Levels = Math.Max(ballPositionTypes.Levels, (byte)size.Level);
            }

            //The layout is placed at the top of the field. An odd offset would flip the level parity of every layer
            //(odd levels are shifted by +0.5 in X and Z) and change how shaped layouts nest into each other,
            //so the field is extended by one level to keep the offset even.
            if (((Levels - size.Level) % 2) != 0) Levels = (byte)Math.Min(byte.MaxValue, Levels + 1);

            BuildMapFromBallPositionTypes(ballPositionTypes);
        }

        private BallPositionTypes BuildBallPositionTypes()
        {
            BallPositionTypes ballPositionTypes = new();

            ballPositionTypes.StageSizeX = StageSizeX;
            ballPositionTypes.StageSizeZ = StageSizeZ;
            ballPositionTypes.Levels = Levels;

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

            XZLevel layoutSize = XZLevel.FromArray(ballPositionTypes.Balls);

            //The layout hangs from the ceiling: its top level goes to the field's top level,
            //extra field levels stay empty at the bottom as room to grow
            byte levelOffset = (byte)(Levels - layoutSize.Level);

            for (byte level = 0; level < layoutSize.Level; level++)
                for (byte x = 0; x < layoutSize.X; x++)
                    for (byte z = 0; z < layoutSize.Z; z++)
                        if (ballPositionTypes.Balls[x, z, level] != null)
                            PutBallAt(x, z, (byte)(level + levelOffset), ballPositionTypes.Balls[x, z, level].Type);
        }

        private Vector3 ComputeUncentered(Vector3 position)
        {
            if (!Centered) return position;

            return new(
                position.X + BoundingBoxCenter.X + BALL_RADIUS,
                position.Y,
                position.Z + BoundingBoxCenter.Y + BALL_RADIUS);
        }

        private Vector3 ComputeCentered(Vector3 position) => Centered ? ApplyCenterOffset(position) : position;

        /// <summary>Translates a raw grid-frame position into the centered world frame.</summary>
        private Vector3 ApplyCenterOffset(Vector3 position) => new(
            position.X - BoundingBoxCenter.X - BALL_RADIUS,
            position.Y,
            position.Z - BoundingBoxCenter.Y - BALL_RADIUS);

        public void Center()
        {
            float minPosX = float.MaxValue, minPosZ = float.MaxValue;
            float maxPosX = float.MinValue, maxPosZ = float.MinValue;

            byte topLevel = (byte)(Levels - 1);

            for (byte x = 0; x < StageSizeX; x++)
                for (byte z = 0; z < StageSizeZ; z++)
                    if (_balls[x, z, topLevel] != null)
                    {
                        StaticBall currentBall = _balls[x, z, topLevel];

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
                            _balls[x, z, level].Position = ApplyCenterOffset(_balls[x, z, level].Position);

            Centered = true;
        }
    }
}