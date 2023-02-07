using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;
using System.IO;

namespace Prazsky.BS3D.GameStructure
{
    public class BallsMap
    {
        private StaticBall[,,] _balls;
        private static readonly float SQRTTWO = (float)Math.Sqrt(2);
        private Matrix[] _transformations;
        private Model _ballModel;

        private static readonly float BALL_RADIUS = Constants.HALF;

        public byte StageSizeX { get; internal set; }
        public byte StageSizeZ { get; internal set; }
        public byte Levels { get; internal set; }

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
        /// <param name="type">Type.</param>
        public void PutBallAt(byte stageX, byte stageZ, byte level, BallType type)
        {
            if (stageX > StageSizeX || stageZ > StageSizeZ || level > Levels) throw new ArgumentOutOfRangeException($"Invalid requested ball position, array size is: {StageSizeX} × {StageSizeZ} × {Levels}");

            Vector3 realPos = GetRealPosition(stageX, stageZ, level);

#if DEBUG
            Console.WriteLine($"Putting ball at stageX: {stageX}; stageZ: {stageZ}; level: {level}; Real position: {realPos}");
#endif

            _balls[stageX, stageZ, level] = new StaticBall(realPos, type, _transformations);
        }

        public void RemoveBallAt(byte stageX, byte stageZ, byte level)
        {
            if (stageX > StageSizeX || stageZ > StageSizeZ || level > Levels) throw new ArgumentOutOfRangeException($"Invalid requested ball position, array size is: {StageSizeX} × {StageSizeZ} × {Levels}");

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

            float realPosY = level / SQRTTWO;

            return new Vector3(realPosX, realPosY, realPosZ);
        }

        public StaticBall[,,] GetStaticBallsArray()
        {
            return _balls;
        }

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

			int length1 = ballPositionTypes.Balls.GetLength(0);
			int length2 = ballPositionTypes.Balls.GetLength(1);
			int length3 = ballPositionTypes.Balls.GetLength(2);

			if (length1 > byte.MaxValue || length2 > byte.MaxValue || length3 > byte.MaxValue)
                throw new InvalidDataException("Deserialized data invalid");

            #endregion

			StageSizeX = (byte)length1;
			StageSizeZ = (byte)length2;
			Levels = (byte)length3;

			BuildMapFromBallPositionTypes(ballPositionTypes);
		}

        private BallPositionTypes BuildBallPositionTypes()
        {
            BallPositionTypes ballPositionTypes = new BallPositionTypes();

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

        public Vector3 GetStaticBallsMapCenter()
        {
            byte minX = StageSizeX;
            byte maxX = 0;

            byte minZ = StageSizeZ;
            byte maxZ = 0;

            byte minLevel = Levels;
            byte maxLevel = 0;

            for (byte level = 0; level < Levels; level++)
                for (byte x = 0; x < StageSizeX; x++)
                    for (byte z = 0; z < StageSizeZ; z++)
                        if (_balls[x, z, level] != null)
                        {
                            if (x < minX) minX = x;
                            if (z < minZ) minZ = z;
                            if (level < minLevel) minLevel = level;

                            if (x > maxX) maxX = x;
                            if (z > maxZ) maxZ = z;
                            if (level > maxLevel) maxLevel = level;
                        }

            Vector3 minPos = GetRealPosition(minX, minZ, minLevel);
            Vector3 maxPos = GetRealPosition(maxX, maxZ, maxLevel);

#if DEBUG
            Console.WriteLine($"Min ball: {minPos}");
            Console.WriteLine($"Max ball: {maxPos}");
#endif

            Vector3 result = (maxPos + minPos) / 2f;

#if DEBUG
            Console.WriteLine($"Balls map center: {result}");
#endif

            return result;
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

            Vector2 minPos = new Vector2(minPosX, minPosZ);
            Vector2 maxPos = new Vector2(maxPosX, maxPosZ);

#if DEBUG
            Console.WriteLine("Map minPos: " + minPos);
            Console.WriteLine("Map maxPos: " + maxPos);
#endif

            Vector2 boundingBoxCenter = (maxPos - minPos) / 2f;

#if DEBUG
            Console.WriteLine("Map AABB center: " + boundingBoxCenter);
#endif

            for (byte level = 0; level < Levels; level++)
                for (byte x = 0; x < StageSizeX; x++)
                    for (byte z = 0; z < StageSizeZ; z++)
                        if (_balls[x, z, level] != null)
                        {
                            _balls[x, z, level].Position = new Vector3(
                                _balls[x, z, level].Position.X - boundingBoxCenter.X - BALL_RADIUS,
                                _balls[x, z, level].Position.Y,
                                _balls[x, z, level].Position.Z - boundingBoxCenter.Y - BALL_RADIUS
                                );
                        }
        }
    }
}