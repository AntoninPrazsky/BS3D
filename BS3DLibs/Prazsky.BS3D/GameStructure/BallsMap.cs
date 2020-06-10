using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.Core.Camera;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Prazsky.BS3D.GameStructure
{
	public class BallsMap
	{
		private StaticBall[,,] _balls;
		private static readonly float SQRTTWO = (float)Math.Sqrt(2);
		private Matrix[] _transformations;
		private Model _ballModel;

		private static readonly float BALL_RADIUS = 0.5f;

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
			DeserializeBinary(fileNameForDeserialization);

			InitializeModel(ballModel);
		}

		private void InitializeModel(Model ballModel)
		{
			_ballModel = ballModel;
			_transformations = new Matrix[ballModel.Bones.Count];
			ballModel.CopyAbsoluteBoneTransformsTo(_transformations);
		}

		/// <summary>
		/// Vytvoří nový <see cref="StaticBall"/> daného typu <see cref="eBallType"/>, vypočte mu reálné pozice a umístí ho do interního trojrozměrného pole.
		/// Pokud už se na dané pozici <see cref="StaticBall"/> nachází, je tento bez upozornění nahrazen.
		/// </summary>
		/// <param name="stageX">Souřadnice X v dané úrovni.</param>
		/// <param name="stageZ">Souřadnice Z v dané úrovni.</param>
		/// <param name="level">Úroveň.</param>
		/// <param name="type">Typ.</param>
		public void PutBallAt(byte stageX, byte stageZ, byte level, eBallType type)
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
			if (isShifted) realPosX += 0.5f;

			float realPosZ = stageZ;
			if (isShifted) realPosZ += 0.5f;

			float realPosY = (level) / SQRTTWO;

			return new Vector3(realPosX, realPosY, realPosZ);
		}

		public StaticBall[,,] GetStaticBallsArray()
		{
			return _balls;
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

		public void SerializeAsBinary(string fileName)
		{
			//Proč ano jako Binary:
			// 1) Výsledek je výrazně menší než XML.
			// 2) Serializace i deserializace je výrazně rychlejší než serializace do XML (cca 6×).

			//Proč ne jako Binary:
			// 1) Výsledek nemůže číst ani editovat člověk.

			BallPositionTypes ballPositionTypes = BuildBallPositionTypes();

			BinaryFormatter binaryFormatter = new BinaryFormatter();

			using (Stream stream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.Write))
			{
				binaryFormatter.Serialize(stream, ballPositionTypes);
			}
		}

		public void DeserializeBinary(string fileName)
		{
			if (string.IsNullOrEmpty(fileName)) return;

			BallPositionTypes ballPositionTypes;

			BinaryFormatter binaryFormatter = new BinaryFormatter();

			using (Stream stream = new FileStream(fileName, FileMode.Open))
			{
				ballPositionTypes = (BallPositionTypes)binaryFormatter.Deserialize(stream);
			}

			//Je otázka, jestli vůbec dělat validaci - ale vypadá to, že nebere moc času, tak je to asi jedno

			if (ballPositionTypes.Balls.Rank != 3) throw new InvalidDataException("Deserialized data invalid");

			int length1 = ballPositionTypes.Balls.GetLength(0);
			int length2 = ballPositionTypes.Balls.GetLength(1);
			int length3 = ballPositionTypes.Balls.GetLength(2);

			if (length1 > byte.MaxValue || length2 > byte.MaxValue || length3 > byte.MaxValue) throw new InvalidDataException("Deserialized data invalid");

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