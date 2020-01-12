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

		public byte StageSizeX { get; internal set; }
		public byte StageSizeZ { get; internal set; }
		public byte Levels { get; internal set; }

		public BallsMap(byte stageSizeX, byte stageSizeZ, byte levels, Model ballModel)
		{
			if (stageSizeX < 2 || stageSizeZ < 2 || levels < 2) throw new ArgumentException($"Minimum BallsMap size is 2×2×2, given arguments: {stageSizeX}, {stageSizeZ}, {levels}");

			StageSizeX = stageSizeX;
			StageSizeZ = stageSizeZ;
			Levels = levels;
			_ballModel = ballModel;

			_transformations = new Matrix[ballModel.Bones.Count];
			ballModel.CopyAbsoluteBoneTransformsTo(_transformations);

			_balls = new StaticBall[StageSizeX, StageSizeZ, Levels];
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

			Console.WriteLine($"Putting ball at stageX: {stageX}; stageZ: {stageZ}; level: {level}; Real position: {realPos}");

			_balls[stageX, stageZ, level] = new StaticBall(realPos, type, _transformations);
		}

		public void RemoveBallAt(byte stageX, byte stageZ, byte level)
		{
			if (stageX > StageSizeX || stageZ > StageSizeZ || level > Levels) throw new ArgumentOutOfRangeException($"Invalid requested ball position, array size is: {StageSizeX} × {StageSizeZ} × {Levels}");

			Console.WriteLine($"Removing ball at stageX: {stageX}; stageZ: {stageZ}; level: {level}");

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

		public void SerializeAsXML()
		{
			//Proč ne jako XML:
			// 1) XmlSerializer neumí serializovat multidimenziální pole ([,,]), musel jsem ho ručně předělat na pole polí polí ([][][]).
			// 2) Výsledné XML je zbytečně velké, protože místa, kde nejsou v mapě kuličky, jsou null - do XML se renderovaly prázdné elementy s atributem xsi:nil="true".
			// 3) Trvá to dlouho (cca 6× déle než binární serializace a deserializace).

			//Proč ano jako XML:
			// 1) Výsledek může číst a editovat člověk.

			throw new NotImplementedException();
		}

		public void DeserializeXML()
		{
			throw new NotImplementedException();
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

			Console.WriteLine($"Min ball: {minPos}");
			Console.WriteLine($"Max ball: {maxPos}");

			Vector3 result = (maxPos + minPos) / 2f;

			Console.WriteLine($"Balls map center: {result}");

			return result;
		}
	}
}