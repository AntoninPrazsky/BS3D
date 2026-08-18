using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D.GameStructure;
using Prazsky.Core;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;

namespace MapEditor.GUI
{
    class Selector : Object3D
    {
        private byte _stageX = 0, _stageZ = 0, _level = 0;
        private BallsMap _ballsMap;
        private BallType _ActiveBallType = BallType.Type1;
        private readonly ICamera _camera;

        /// <summary>The type the next placed ball gets — read by the host's cycle keys (#152).</summary>
        public BallType ActiveBallType => _ActiveBallType;

        public Selector(ContentManager contentManager, BallsMap ballsMap, ICamera camera)
        {
            _camera = camera;

            Model = contentManager.Load<Model>("GUI/Selector");
            Transformations = new Matrix[Model.Bones.Count];
            Model.CopyAbsoluteBoneTransformsTo(Transformations);

            _ballsMap = ballsMap;

            BasicEffectParams = BasicEffectParamsProvider.ColorWhite; //a shared static instance, allocated once by the provider
            World = Matrix.CreateTranslation(Position);
        }

        public void PutBall(BallType ballType)
        {
            _ballsMap.PutBallAt(_stageX, _stageZ, _level, ballType);
        }

        public void PutBall()
        {
            _ballsMap.PutBallAt(_stageX, _stageZ, _level, _ActiveBallType);
        }

        public void ChangeBallType(BallType ballType)
        {
            _ActiveBallType = ballType;
        }

        public void RemoveBall()
        {
            _ballsMap.RemoveBallAt(_stageX, _stageZ, _level);
        }

        /// <summary>
        /// Snaps a horizontal vector to the nearest of the four axis-aligned grid directions.
        /// </summary>
        private static Vector3 SnapToGridAxis(float x, float z)
        {
            if (Math.Abs(x) >= Math.Abs(z)) return x >= 0f ? Vector3.Right : Vector3.Left;
            return z >= 0f ? Vector3.Backward : Vector3.Forward;
        }

        /// <summary>
        /// Turns a direction expressed from the camera's point of view into a direction in the grid frame.
        /// </summary>
        private Vector3 ToGridDirection(Vector3 viewDirection)
        {
            if (_camera == null || viewDirection == Vector3.Up || viewDirection == Vector3.Down) return viewDirection;

            //Camera axes as stored in the view matrix (which holds the inverse of the camera orientation)
            Matrix view = _camera.View;
            Vector3 right = new(view.M11, view.M21, view.M31);
            Vector3 up = new(view.M12, view.M22, view.M32);
            Vector3 forward = -new Vector3(view.M13, view.M23, view.M33);

            //Sideways movement follows the camera right vector, which is always horizontal
            if (viewDirection == Vector3.Right || viewDirection == Vector3.Left)
            {
                Vector3 rightAxis = SnapToGridAxis(right.X, right.Z);
                return viewDirection == Vector3.Right ? rightAxis : -rightAxis;
            }

            //Movement up and down the screen normally means movement into and out of the depth of the scene,
            //but the steeper the camera looks, the less depth there is on screen and the more the horizontal
            //part of its up vector takes over. Beyond a quarter turn (typically a view from below the map, where
            //the far end of the structure projects downwards) the two disagree and the up vector wins, keeping
            //the movement aligned with what the screen shows
            Vector2 depth = new(forward.X, forward.Z);
            Vector2 screenUp = new(up.X, up.Z);
            Vector2 heading = screenUp.LengthSquared() > depth.LengthSquared() ? screenUp : depth;

            if (heading.LengthSquared() < Constants.THOUSANDTH) return viewDirection;

            Vector3 headingAxis = SnapToGridAxis(heading.X, heading.Y);
            return viewDirection == Vector3.Forward ? headingAxis : -headingAxis;
        }

        public void Move(Vector3 viewDirection)
        {
            Vector3 direction = ToGridDirection(viewDirection);

            if (direction == Vector3.Forward)
            {
                if (_stageZ - 1 < 0)
                {
                    Console.WriteLine($"Selector is at the beggining of the array (stageZ = {_stageZ})");
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
                    Console.WriteLine($"Selector is at the end of the array (stageZ = {_stageZ})");
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
                    Console.WriteLine($"Selector is at the beggining of the array (stageX = {_stageX})");
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
                    Console.WriteLine($"Selector is at the end of the array (stageX = {_stageX})");
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
                    Console.WriteLine($"Selector is at the end of the array (level = {_level})");
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
                    Console.WriteLine($"Selector is at the beggining of the array (level = {_level})");
                    BasicEffectParams = BasicEffectParamsProvider.ColorRed;
                    return;
                }
                else
                {
                    _level--;
                    BasicEffectParams = BasicEffectParamsProvider.ColorWhite;
                }
            }
            else throw new ArgumentException("Unknown value for selector movement: " + direction);

            Position = BallsMap.GetRealPosition(_stageX, _stageZ, _level);
            Console.WriteLine($"Selector map position: stageX: {_stageX}; stageZ: {_stageZ}; level: {_level}; Selector real position: {Position}");
            World = Matrix.CreateTranslation(Position);
        }

        public void UpdateBallsBap(BallsMap ballsMap)
        {
            _ballsMap = ballsMap;
            _stageX = _stageZ = _level = 0;

            Position = BallsMap.GetRealPosition(_stageX, _stageZ, _level);
            World = Matrix.CreateTranslation(Position);
            BasicEffectParams = BasicEffectParamsProvider.ColorWhite;
        }
    }
}
