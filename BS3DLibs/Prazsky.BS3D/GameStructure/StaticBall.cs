using Microsoft.Xna.Framework;
using Prazsky.Core;

namespace Prazsky.BS3D.GameStructure
{
    public class StaticBall : Object3D
    {
        public eBallType Type { get; }

        public StaticBall(Vector3 position, eBallType type, Matrix[] transformations)
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