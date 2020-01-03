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
		private readonly byte _stageSizeX, _stageSizeZ, _levels;
		private static readonly float SQRTTWO = (float)Math.Sqrt(2);

		private Model _ballModel;

		public BallsMap(byte stageSizeX, byte stageSizeZ, byte levels, Model ballModel)
		{
			if (stageSizeX < 2 || stageSizeZ < 2 || levels < 2) throw new ArgumentException($"Minimum BallsMap size is 2×2×2, given arguments: {stageSizeX}, {stageSizeZ}, {levels}");

			_stageSizeX = stageSizeX;
			_stageSizeZ = stageSizeZ;
			_levels = levels;
			_ballModel = ballModel;

			_balls = new StaticBall[stageSizeX, stageSizeZ, levels];
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
			if (stageX > _stageSizeX || stageZ > _stageSizeZ || level > _levels) throw new ArgumentOutOfRangeException($"Invalid requested ball position, array size is: {_stageSizeX} × {_stageSizeZ} × {_levels}");

			bool isShifted = (level % 2) > 0;

			float realPosX = stageX;
			if (isShifted) realPosX += 0.5f;

			float realPosZ = stageZ;
			if (isShifted) realPosZ += 0.5f;

			float realPosY = (level - 1) / SQRTTWO;

			_balls[stageX, stageZ, level] = new StaticBall(new Vector3(realPosX, realPosY, realPosZ), type);

			_balls[stageX, stageZ, level].RecomputeTransformations(_ballModel);
			_balls[stageX, stageZ, level].RecomputeWorldMatrix();
		}

		public StaticBall[,,] GetStaticBallsArray()
		{
			return _balls;
		}

		public int GetBallsCount()
		{
			int count = 0;

			for (int level = 0; level < _levels; level++)
				for (int x = 0; x < _stageSizeX; x++)
					for (int z = 0; z < _stageSizeZ; z++)
						if (_balls[x, z, level] != null)
							count++;
			return count;
		}

		public void Draw(ICamera camera)
		{
			for (int level = 0; level < _levels; level++)
				for (int x = 0; x < _stageSizeX; x++)
					for (int z = 0; z < _stageSizeZ; z++)
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
		}

		private BallPositionTypes BuildBallPositionTypes()
		{
			BallPositionTypes ballPositionTypes = new BallPositionTypes();

			ballPositionTypes.Balls = new BallPositionType[_stageSizeX, _stageSizeZ, _levels];

			for (int level = 0; level < _levels; level++)
				for (int x = 0; x < _stageSizeX; x++)
					for (int z = 0; z < _stageSizeZ; z++)
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
	}
}