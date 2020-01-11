using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D.GameStructure;
using Prazsky.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace MapEditor.GUI
{
	class Selector : Object3D
	{
		private byte _stageX = 0, _stageZ = 0, _level = 0;
		private BallsMap _ballsMap;

		public Selector(ContentManager contentManager, BallsMap ballsMap)
		{
			Model = contentManager.Load<Model>("GUI/Selector");
			Transformations = new Matrix[Model.Bones.Count];
			Model.CopyAbsoluteBoneTransformsTo(Transformations);

			_ballsMap = ballsMap;

			BasicEffectParams = BasicEffectParamsProvider.ColorWhite; //BasicEffectParamsProvider udělá vždycky novou instanci, dávalo by spíš smysl si je pamatovat
			World = Matrix.CreateTranslation(Position);
		}

		public void PutBall(eBallType ballType)
		{
			_ballsMap.PutBallAt(_stageX, _stageZ, _level, ballType);
		}

		public void RemoveBall()
		{
			_ballsMap.RemoveBallAt(_stageX, _stageZ, _level);
		}

		public void Move(Vector3 direction)
		{
			if (direction == Vector3.Forward)
			{
				if (_stageZ - 1 < 0)
				{
					Console.WriteLine("Selector is at the beggining of the array (stageZ)");
					BasicEffectParams = BasicEffectParamsProvider.ColorRed;
					return;
				}
				else
				{
					BasicEffectParams = BasicEffectParamsProvider.ColorWhite;
					_stageZ--;
				}
			}
			else if (direction == Vector3.Backward)
			{
				if (_stageZ + 1 >= _ballsMap.StageSizeZ)
				{
					Console.WriteLine("Selector is at the end of the array (Z)");
					BasicEffectParams = BasicEffectParamsProvider.ColorRed;
					return;
				}
				else
				{
					BasicEffectParams = BasicEffectParamsProvider.ColorWhite;
					_stageZ++;
				}
			}
			else if (direction == Vector3.Left)
			{
				if (_stageX - 1 < 0)
				{
					Console.WriteLine("Selector is at the beggining of the array (stageX)");
					BasicEffectParams = BasicEffectParamsProvider.ColorRed;
					return;
				}
				else
				{
					BasicEffectParams = BasicEffectParamsProvider.ColorWhite;
					_stageX--;
				}
			}
			else if (direction == Vector3.Right)
			{
				if (_stageX + 1 >= _ballsMap.StageSizeX)
				{
					Console.WriteLine("Selector is at the end of the array (stageX)");
					BasicEffectParams = BasicEffectParamsProvider.ColorRed;
					return;
				}
				else
				{
					BasicEffectParams = BasicEffectParamsProvider.ColorWhite;
					_stageX++;
				}
			}
			else if (direction == Vector3.Up)
			{
				if (_level + 1 >= _ballsMap.Levels)
				{
					Console.WriteLine("Selector is at the end of the array (level)");
					BasicEffectParams = BasicEffectParamsProvider.ColorRed;
					return;
				}
				else
				{
					BasicEffectParams = BasicEffectParamsProvider.ColorWhite;
					_level++;
				}
			}
			else if (direction == Vector3.Down)
			{
				if (_level - 1 < 0)
				{
					Console.WriteLine("Selector is at the beggining of the array (level)");
					BasicEffectParams = BasicEffectParamsProvider.ColorRed;
					return;
				}
				else
				{
					_level--;
					BasicEffectParams = BasicEffectParamsProvider.ColorWhite;
				}
			}
			else throw new ArgumentException("Neznámá hodnota pro pohyb selectoru: " + direction);

			Position = BallsMap.GetRealPosition(_stageX, _stageZ, _level);
			Console.WriteLine($"Selector map position: stageX: {_stageX}; stageZ: {_stageZ}; level: {_level}; Selector real position: {Position}");
			World = Matrix.CreateTranslation(Position);
		}

		public void UpdateBallsBap(BallsMap ballsMap)
		{
			_ballsMap = ballsMap;
			_stageX = _stageZ = _level = 0;
		}
	}
}
