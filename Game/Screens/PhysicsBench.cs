using BepuPhysics;
using BepuPhysics.Collidables;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Diagnostics;

namespace BS3D.Screens
{
    /// <summary>
    /// The benchmark's physics segment (#988): the CPU's cost of the game's own simulation, measured apart from the GPU. The
    /// tours' only cluster is the front end's preview, drawn without the game's simulation, so this hangs a shipped level's
    /// cluster in a <see cref="PhysicsWorld"/> the way <c>GameplayScreen</c> builds one - the kinematic glass sized off
    /// <see cref="CeilingPlate"/>, the island's funnel
    /// floor, <see cref="BallsConstraintsBuilder.BuildBallsStructure"/> at <see cref="ClusterHang.FitWorldOffset"/>, the
    /// spring from the glass (<see cref="PhysicsWorld.BeginStartSwing"/>), the scene's air keeping it awake - and times every
    /// <see cref="PhysicsWorld.FIXED_TIMESTEP"/> step on the stopwatch. Nothing is drawn: the world exists only here.
    /// <para>
    /// It is the game's steady load, not its worst: no shot is fired, nothing attaches or falls. A level's cost is mostly
    /// its sockets' solve, which this is, and a shot adds one body and a handful of sockets to it.
    /// </para>
    /// <para>
    /// The first <see cref="WARMUP_STEPS"/> are the code's first use (the JIT, the pool's first buffers) and are reported
    /// apart, as a tour's first seconds are.
    /// </para>
    /// </summary>
    internal sealed class PhysicsBench : IDisposable
    {
        /// <summary>The level hung: the largest cluster that ships (1204 balls, 32 levels deep), so a machine that holds it holds every level.</summary>
        internal const string LEVEL = "Girandole";

        /// <summary>A second of the game's steps, left out of the figures.</summary>
        internal const int WARMUP_STEPS = 120;

        /// <summary>Ten seconds of the game's steps.</summary>
        internal const int STEPS = 1200;

        private static readonly Action NO_CONTACT_WORK = () => { };

        private readonly PhysicsWorld _world;
        private readonly WindField _wind;
        private readonly int _steps;
        private readonly FrameStats _measured, _warmup = new(WARMUP_STEPS);
        private float _clock;
        private int _taken;

        /// <summary>How many balls the cluster holds.</summary>
        internal int Balls { get; }

        /// <param name="map">The cluster's map; centred here, as the game centres it.</param>
        /// <param name="wind">The air over it, which also keeps it awake (an empty one lets it settle and sleep, as a tier without wind does).</param>
        /// <param name="steps">How many steps are measured after the warm-up; <see cref="STEPS"/> but for a test.</param>
        internal PhysicsBench(BallsMap map, WindField wind, int steps = STEPS)
        {
            _wind = wind;
            _steps = steps;
            _measured = new FrameStats(steps);

            map.Center();
            Microsoft.Xna.Framework.Vector3 offset = ClusterHang.FitWorldOffset(map, out float fieldTopY);

            _world = new PhysicsWorld();
            try
            {
                Balls = Hang(map, offset, fieldTopY);
            }
            catch
            {
                //A world half built is still a buffer pool and a dispatcher's threads
                _world.Dispose();
                throw;
            }
        }

        //The glass, the floor and the cluster, as GameplayScreen builds them; how many balls it hung
        private int Hang(BallsMap map, Microsoft.Xna.Framework.Vector3 offset, float fieldTopY)
        {
            float ceilingY = CeilingPlate.CentreYAbove(fieldTopY);
            Box glass = new(CeilingPlate.FootprintFor(map.StageSizeX), CeilingPlate.THICKNESS, CeilingPlate.FootprintFor(map.StageSizeZ));
            BodyHandle ceilingHandle = _world.Simulation.Bodies.Add(BodyDescription.CreateKinematic(
                new System.Numerics.Vector3(0f, ceilingY, 0f),
                new CollidableDescription(_world.Simulation.Shapes.Add(glass), 0.1f),
                new BodyActivityDescription(PhysicsWorld.SLEEP_THRESHOLD)));
            BodyReference ceiling = new(ceilingHandle, _world.Simulation.Bodies);

            FunnelPhysics.Build(_world.Simulation, _world.BufferPool, ArenaIsland.TOP_Y, ArenaIsland.FUNNEL_BOTTOM_Y,
                ArenaIsland.FUNNEL_TOP_RADIUS, ArenaIsland.FUNNEL_HOLE_RADIUS, ArenaIsland.FLOOR_RADIUS,
                ArenaIsland.DISH_DEPTH, ArenaIsland.FUNNEL_SEGMENTS);

            PhysicsBall[,,] balls = BallsConstraintsBuilder.BuildBallsStructure(map.GetStaticBallsArray(), _world.Simulation, ceiling,
                offset.ToNumerics());
            int count = 0;
            foreach (PhysicsBall ball in balls)
                if (ball != null) count++;

            _world.BeginStartSwing(balls);
            if (_wind.Strength > 0f) _world.KeepClusterAwake(balls);
            return count;
        }

        /// <summary>Whether every step has been taken.</summary>
        internal bool Done => _taken >= WARMUP_STEPS + _steps;

        /// <summary>How far through the steps it is, 0 to 1.</summary>
        internal float Progress => (float)_taken / (WARMUP_STEPS + _steps);

        /// <summary>
        /// Takes steps until <paramref name="milliseconds"/> of wall time have gone or every step is taken, each timed on its
        /// own: a frame's share, so the window keeps answering while a slow machine steps.
        /// </summary>
        internal void Run(double milliseconds)
        {
            long start = Stopwatch.GetTimestamp();
            double tick = 1000.0 / Stopwatch.Frequency;
            while (!Done && (Stopwatch.GetTimestamp() - start) * tick < milliseconds)
            {
                long before = Stopwatch.GetTimestamp();
                _world.Wind = _wind.Acceleration(_clock);
                _clock += PhysicsWorld.FIXED_TIMESTEP;
                _world.Step(PhysicsWorld.FIXED_TIMESTEP, NO_CONTACT_WORK);
                float took = (float)((Stopwatch.GetTimestamp() - before) * tick);
                (_taken++ < WARMUP_STEPS ? _warmup : _measured).Add(took);
            }
        }

        /// <summary>
        /// A step's budget: the time it simulates. A step that takes longer than that cannot keep up with the clock, whatever
        /// the display: the game takes 120 steps a second at any refresh, so the physics' share of the frame is the share of
        /// every second it takes, a step's time over this.
        /// </summary>
        internal const float STEP_BUDGET_MILLISECONDS = 1000f * PhysicsWorld.FIXED_TIMESTEP;

        /// <summary>The measured steps' figures, over <see cref="STEP_BUDGET_MILLISECONDS"/>.</summary>
        internal FrameStats.Summary Summarize() => _measured.Summarize(STEP_BUDGET_MILLISECONDS);

        /// <summary>The warm-up steps' figures.</summary>
        internal FrameStats.Summary SummarizeWarmup() => _warmup.Summarize(STEP_BUDGET_MILLISECONDS);

        /// <summary>The share of every second, and so of every frame, that steps of <paramref name="stepMilliseconds"/> take.</summary>
        internal static float TimeShare(float stepMilliseconds) => stepMilliseconds / STEP_BUDGET_MILLISECONDS;

        public void Dispose() => _world.Dispose();
    }
}
