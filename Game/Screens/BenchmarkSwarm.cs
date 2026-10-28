using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>The benchmark's finale, the swarm</b> (#988): a storm of the game's own balls winding up round the island, ramped
    /// until the frame no longer holds <see cref="BUDGET_MILLISECONDS"/>, and how many it held. The tours say whether a
    /// machine holds the player's tier; this says how far past it the machine reaches, in one figure that grows with the
    /// graphics card, so a machine nobody here owns (a stronger Pi, a new integrated GPU) is told apart without its name.
    /// <para>
    /// <b>The balls are the game's</b>: the shared <see cref="BallRenderSet"/>, its LODs, its instancing and its shadow pass,
    /// fed through the frame's <see cref="BallDrawFrame"/> as the front end's preview is. So the figure is the cost the
    /// game's levels are made of, CPU side and GPU side, on the player's own tier and resolution.
    /// </para>
    /// <para>
    /// <b>The ramp</b> (<see cref="Tick"/>, pure): <see cref="START_BALLS"/>, then <see cref="GROWTH"/> times more every
    /// <see cref="STEP_SECONDS"/> while the median frame of the step (its first <see cref="STEP_SETTLE_SECONDS"/> left out:
    /// the buffers grow and the new balls' first draws) stays within the budget. A step past it is measured once more, so a
    /// hitch of the machine's own (a background task, a clock step) does not end the run; past it twice, the run ends, and
    /// <see cref="Held"/> is the last count that held. At most <see cref="MAX_BALLS"/>. <b>The budget is 60 FPS on every
    /// machine</b>, not the display's refresh the tours are judged by: a figure two machines can be compared by.
    /// </para>
    /// <para>
    /// <b>The shape is a function of the ball's index alone</b> (<see cref="Place"/>), so a bigger count is the smaller one
    /// filled in: the storm thickens as it grows, and it is the same storm on every machine and every run. It is turned
    /// about the island's axis as a whole, so the camera's bearing changes nothing the frame pays for.
    /// </para>
    /// </summary>
    internal sealed class BenchmarkSwarm
    {
        /// <summary>The first count, low enough that a Pi's Potato path can hold it, so the ramp has a step that held.</summary>
        public const int START_BALLS = 1024;

        /// <summary>Each step's count over the last one's: about 21 steps from the start to the cap, under half a minute.</summary>
        public const float GROWTH = 1.3f;

        /// <summary>The most the storm grows to: past a quarter of a million balls the figure says nothing a player needs.</summary>
        public const int MAX_BALLS = 1 << 18;

        /// <summary>How long each count is held, and the first part of it left out of its median.</summary>
        public const float STEP_SECONDS = 1.2f, STEP_SETTLE_SECONDS = 0.3f;

        /// <summary>The frame the swarm is held to: 60 FPS, on every machine (see the class).</summary>
        public const float BUDGET_MILLISECONDS = 1000f / 60f;

        //THE STORM: a funnel of balls round the island, its inner wall clear of the biggest cluster's reach and widening as it
        //rises, from just over the island's stone to well over the glass, in ARMS broad spiral arms that wind TURNS of the way
        //round as they rise: steep enough to read as a vortex and not as stacked rings, and outside in clear of the front
        //end's wide turn (33 units out on the widest window)
        private const float INNER_FOOT = 13f, INNER_TOP = 19f, THICKNESS = 8f;
        private const float FLOOR_OVER_ISLAND = 1f, HEIGHT = 22f;
        private const int ARMS = 6;
        private const float TURNS = 0.6f, ARM_SPREAD = 0.8f;

        //A colour an arm: six of the game's thirteen, never a new one
        private static readonly BallType[] ARM_COLOURS =
            { BallType.Type1, BallType.Type2, BallType.Type3, BallType.Type4, BallType.Type5, BallType.Type6 };

        //How fast the storm turns about the island's axis, radians a second: slow, a weather and not a carousel
        private const float TURN_RATE = 0.12f;

        /// <summary>How many balls the storm has this step.</summary>
        public int Balls { get; private set; } = START_BALLS;

        /// <summary>The largest count whose step held the budget, 0 when the first did not.</summary>
        public int Held { get; private set; }

        /// <summary>The median frame of the step that held <see cref="Held"/>, and of the step that ended the run.</summary>
        public float HeldMedian { get; private set; }

        /// <inheritdoc cref="HeldMedian"/>
        public float BrokenMedian { get; private set; }

        /// <summary>How many steps were measured, a count measured again included.</summary>
        public int Steps { get; private set; }

        /// <summary>The ramp is over: a step broke the budget, or the storm reached <see cref="MAX_BALLS"/> holding it.</summary>
        public bool Done { get; private set; }

        /// <summary>It ended at <see cref="MAX_BALLS"/> still holding: the machine's reach is past what the run asks.</summary>
        public bool Capped { get; private set; }

        private readonly FrameStats _step = new(4096);
        private float _stepSeconds;

        //Whether this count has broken the budget once already: the second time ends the run
        private bool _brokeOnce;

        //Every ball's place and colour, worked out once for the whole ramp; and the storm's turn
        private Vector3[] _places;
        private BallType[] _colours;
        private Vector4 _occlusion;
        private float _turn;

        /// <summary>
        /// One frame of <paramref name="milliseconds"/> into the ramp. Returns true when the step ended on it (the count
        /// grew, or the run is <see cref="Done"/>), so the caller can say so.
        /// </summary>
        public bool Tick(float milliseconds)
        {
            if (Done) return false;

            _stepSeconds += milliseconds / 1000f;
            if (_stepSeconds > STEP_SETTLE_SECONDS) _step.Add(milliseconds);
            if (_stepSeconds < STEP_SECONDS) return false;

            //Once a step: the sort FrameStats makes is the step's one allocation
            float median = _step.Summarize(BUDGET_MILLISECONDS).Median;
            _step.Clear();
            _stepSeconds = 0f;
            Steps++;

            if (median > BUDGET_MILLISECONDS)
            {
                BrokenMedian = median;
                if (!_brokeOnce)
                {
                    _brokeOnce = true;
                    return true;
                }

                Done = true;
                return true;
            }

            _brokeOnce = false;
            Held = Balls;
            HeldMedian = median;

            if (Balls >= MAX_BALLS)
            {
                Capped = Done = true;
                return true;
            }

            Balls = Math.Min(MAX_BALLS, (int)MathF.Ceiling(Balls * GROWTH));
            return true;
        }

        /// <summary>Works out every ball's place: once, before the ramp's first frame is measured.</summary>
        public void Prepare()
        {
            if (_places != null) return;

            _places = new Vector3[MAX_BALLS];
            _colours = new BallType[MAX_BALLS];
            for (int i = 0; i < MAX_BALLS; i++)
            {
                _places[i] = Place(i);
                _colours[i] = ARM_COLOURS[i % ARMS];
            }

            //A ball alone in the air: nothing round it shades it
            _occlusion = BallRenderSet.OcclusionTarget(0, Vector3.Zero, 0);
        }

        /// <summary>
        /// Where ball <paramref name="index"/> stands in the storm, before its turn: a funnel round the island's axis, on the
        /// arm its index names, at a height, radius and spread its index's hash picks, uniform over the funnel's cross-section
        /// at that height so the storm fills evenly as it thickens.
        /// </summary>
        internal static Vector3 Place(int index)
        {
            uint h = Hash((uint)index), g = Hash((uint)index ^ 0x9E3779B9u);
            float radiusRoll = (h & 0xFFFF) / 65535f, rise = (h >> 16) / 65535f, spreadRoll = (g & 0xFFFF) / 65535f;

            float inner = INNER_FOOT + (INNER_TOP - INNER_FOOT) * rise, outer = inner + THICKNESS;
            float radius = MathF.Sqrt(inner * inner + radiusRoll * (outer * outer - inner * inner));
            float height = rise * HEIGHT;
            float angle = index % ARMS * (MathHelper.TwoPi / ARMS) + rise * TURNS * MathHelper.TwoPi + (spreadRoll - 0.5f) * ARM_SPREAD;

            return new Vector3(MathF.Cos(angle) * radius, ArenaIsland.TOP_Y + FLOOR_OVER_ISLAND + height, MathF.Sin(angle) * radius);
        }

        //A 32-bit integer hash (lowbias32): the storm's one source of variety, the same on every machine
        private static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }

        /// <summary>
        /// The storm's balls into this frame, turned by <paramref name="elapsedSeconds"/> more. The turn is one rotation for
        /// all of them, applied as each ball's translation is written.
        /// </summary>
        public void Collect(BallDrawFrame frame, float elapsedSeconds)
        {
            if (_places == null) return;

            _turn = (_turn + TURN_RATE * elapsedSeconds) % MathHelper.TwoPi;
            float cos = MathF.Cos(_turn), sin = MathF.Sin(_turn);

            Matrix world = Matrix.Identity;
            for (int i = 0; i < Balls; i++)
            {
                Vector3 p = _places[i];
                Vector3 position = new(p.X * cos - p.Z * sin, p.Y, p.X * sin + p.Z * cos);
                world.M41 = position.X;
                world.M42 = position.Y;
                world.M43 = position.Z;
                frame.Add(_colours[i], position, world, _occlusion);
            }
        }
    }
}
