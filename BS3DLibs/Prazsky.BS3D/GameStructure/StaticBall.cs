using Microsoft.Xna.Framework;
using Prazsky.Core;

namespace Prazsky.BS3D.GameStructure
{
    public class StaticBall : Object3D
    {
        public BallType Type { get; }

        public StaticBall(Vector3 position, BallType type, Matrix[] transformations)
        {
            Type = type;
            Position = position;
            BasicEffectParams = BasicEffectParamsProvider.GetEffectByType(type);

            Transformations = transformations;
            World = Matrix.CreateTranslation(Position);
        }

        public System.Numerics.Vector3 GetPosition()
        {
            return new System.Numerics.Vector3(Position.X, Position.Y, Position.Z);
        }
    }
}