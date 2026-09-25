using BepuPhysics;
using BepuPhysics.Collidables;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;

namespace BS3D.Tests
{
    /// <summary>
    /// A level hung in the real simulation the way the Game hangs it, copied from Tools/LevelGen/SagProbe's
    /// setup: <see cref="ClusterHang.FitWorldOffset"/>, the kinematic glass at
    /// <see cref="CeilingPlate.CentreYAbove"/> sized off <see cref="CeilingPlate.FootprintFor"/>, and
    /// <see cref="BallsConstraintsBuilder.BuildBallsStructure"/> with the world offset. No island floor — none of
    /// the tests lets anything fall far enough to reach it.
    /// </summary>
    internal sealed class HungLevel : IDisposable
    {
        /// <summary>The Game's step, <c>GameplayScreen.PHYSICS_TIMESTEP</c> (private there, restated as SagProbe does).</summary>
        public const float TIMESTEP = 1f / 120f;

        private static readonly Action NO_CONTACT_WORK = () => { };

        public PhysicsWorld World { get; }
        public BallsMap Map { get; }
        public BodyReference Ceiling { get; }
        public PhysicsBall[,,] Balls { get; }
        public Microsoft.Xna.Framework.Vector3 WorldOffset { get; }

        public HungLevel(BallsMap map)
        {
            Map = map;
            Map.Center();

            WorldOffset = ClusterHang.FitWorldOffset(Map, out float fieldTopY);

            World = new PhysicsWorld();

            float ceilingY = CeilingPlate.CentreYAbove(fieldTopY);
            Box box = new(CeilingPlate.FootprintFor(Map.StageSizeX), CeilingPlate.THICKNESS,
                CeilingPlate.FootprintFor(Map.StageSizeZ));
            BodyHandle ceilingHandle = World.Simulation.Bodies.Add(BodyDescription.CreateKinematic(
                new System.Numerics.Vector3(0f, ceilingY, 0f),
                new CollidableDescription(World.Simulation.Shapes.Add(box), 0.1f),
                new BodyActivityDescription(PhysicsWorld.SLEEP_THRESHOLD)));
            Ceiling = new BodyReference(ceilingHandle, World.Simulation.Bodies);

            Balls = BallsConstraintsBuilder.BuildBallsStructure(Map.GetStaticBallsArray(), World.Simulation, Ceiling,
                WorldOffset.ToNumerics());
        }

        public static HungLevel FromLevelFile(string path) => new(new BallsMap(Level.Load(path).Map));

        public void Run(float seconds)
        {
            int steps = (int)MathF.Round(seconds / TIMESTEP);
            for (int i = 0; i < steps; i++) World.Step(TIMESTEP, NO_CONTACT_WORK);
        }

        public void Dispose() => World.Dispose();
    }
}
