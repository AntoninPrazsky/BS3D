using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;

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
		/// <returns></returns>
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

		public void Draw(ICamera camera)
		{
			for (int level = 0; level < _levels; level++)
				for (int x = 0; x < _stageSizeX; x++)
					for (int z = 0; z < _stageSizeZ; z++)
						if (_balls[x, z, level] != null)
							_balls[x, z, level].Draw(camera, _ballModel);
		}
	}
}