using Microsoft.Xna.Framework;
using Prazsky.Core;

namespace Prazsky.BS3D.GameStructure
{
    public class StaticBall : Object3D
    {
        public BallType Type { get; }

        /// <summary>
        /// What this ball IS, beside what colour it is (#323) — <see cref="BallKind.Normal"/> for everything
        /// authored before kinds existed, which is what an absent <c>"k"</c> in a map file means.
        /// </summary>
        public BallKind Kind { get; }

        public StaticBall(Vector3 position, BallType type, BallKind kind = BallKind.Normal)
        {
            Type = type;
            Kind = kind;
            Position = position;
            BasicEffectParams = BasicEffectParamsProvider.GetEffectByType(type);

            World = Matrix.CreateTranslation(Position);
        }

        public System.Numerics.Vector3 GetPosition()
        {
            return new System.Numerics.Vector3(Position.X, Position.Y, Position.Z);
        }
    }
}