using Microsoft.Xna.Framework;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The Grid's light cycles as the runs they ride, for a host that films one (the chapter intro's ride, #1116): the very
    /// riders <see cref="LightCycles"/> draws, made by the same code (<see cref="LightCycles.Rider"/>) from the same seeds
    /// (<see cref="LightCycles.RiderSeed"/>), so where this says a cycle stands at a wall-clock second is where the frame
    /// drawn at that second puts it. Riders of its own, not the drawn ones: a host asking ahead of the frame would have the
    /// drawn ones make their runs again. Nothing here allocates once it is built while a host reads no point of a cycle's run more
    /// than <see cref="KEPT_SECONDS"/> before the furthest it has asked that cycle at: further back, a rider makes its run again
    /// from its start (a <see cref="System.Random"/>, and every node since, more the longer the game has run).
    /// </summary>
    public sealed class LightCycleRuns
    {
        /// <summary>How many cycles ride.</summary>
        public const int COUNT = LightCycles.RIDERS;

        /// <summary>How fast they ride, units a second.</summary>
        public const float SPEED = LightCycles.SPEED;

        /// <summary>The lattice they ride: every run is straight between nodes this far apart, and turns only at one.</summary>
        public const float LATTICE = LightCycles.LATTICE;

        /// <summary>How tall a cycle's wall of light stands over the floor.</summary>
        public const float WALL_HEIGHT = LightCycles.TRAIL_HEIGHT;

        /// <summary>How far a cycle's body reaches from its middle along its heading.</summary>
        public const float BODY_REACH = LightCycles.BODY_REACH;

        /// <summary>How tall a cycle's body stands over the floor.</summary>
        public const float BODY_HEIGHT = LightCycles.BODY_HEIGHT;

        /// <summary>
        /// How many seconds of a cycle's run a host may read back across without the run being made again: from the furthest
        /// second it has asked that cycle at to the earliest point of the run it then reads, a wall's whole length behind the
        /// second it is asked at included (<see cref="WallDistance"/>, 4.7 seconds of run). A host that chooses over a stretch
        /// ahead and then films from its start reads back across all of it: the chapter intro's ride, under nine seconds.
        /// </summary>
        public const float KEPT_SECONDS = 12f;

        //The nodes each rider keeps: KEPT_SECONDS of its run, and the segment at either end
        private const int KEPT = (int)(KEPT_SECONDS * SPEED / LATTICE) + 3;

        //One rider a cycle for where it is and one for its wall: the wall reaches seven nodes back and the heading a host smooths
        //reads ahead, and one ring of kept nodes asked both ways makes its run again from the start
        private readonly LightCycles.Rider[] _riders = new LightCycles.Rider[COUNT];
        private readonly LightCycles.Rider[] _walls = new LightCycles.Rider[COUNT];

        /// <summary>The runs on <paramref name="config"/>, the Grid the renderer draws.</summary>
        public LightCycleRuns(GridSceneConfig config)
        {
            for (int i = 0; i < COUNT; i++)
            {
                _riders[i] = new LightCycles.Rider(LightCycles.RiderSeed(config, i), KEPT);
                _walls[i] = new LightCycles.Rider(LightCycles.RiderSeed(config, i), KEPT);
            }
        }

        /// <summary>Where cycle <paramref name="index"/>'s middle stands on the floor (world X, Z) at <paramref name="time"/> on the
        /// wall clock, and the unit way it is heading: along one axis, snapping a right angle at a node. A time before the clock's
        /// start is its start, where the run begins.</summary>
        public void At(int index, float time, out Vector2 position, out Vector2 heading) =>
            _riders[index].At(MathHelper.Max(time, 0f) * SPEED, out position, out heading);

        /// <summary>How far <paramref name="point"/> (world X, Z) stands from cycle <paramref name="index"/>'s wall at
        /// <paramref name="time"/> on the wall clock, seen from above; infinity before it has a wall.</summary>
        public float WallDistance(int index, float time, Vector2 point) => _walls[index].WallDistance(time * SPEED, point);
    }
}
