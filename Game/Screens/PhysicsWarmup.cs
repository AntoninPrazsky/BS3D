using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Physics;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>The physics solver compiled before the first level needs it</b> (#1086): a small cluster hung in a throwaway
    /// <see cref="PhysicsBench"/> and stepped on a background thread for <see cref="SECONDS"/> once the game has loaded,
    /// while the menus are up, so the first level of a launch does not pay for it at its release.
    /// <para>
    /// Bepu's solver, as the game instantiates it (generic over the session's own callback structs, in another
    /// assembly), is compiled the first time a world is stepped, and ReadyToRun cannot precompile it. That first step is
    /// <c>ReleaseCluster</c>'s, at the first level of a launch: 120 ms on the desktop, 57-80 ms on the vivo X300 Ultra and
    /// about 0.3 s on the Pi, whose quick JIT is off (#1085). Where the quick JIT is on (Windows, Android) the steps then
    /// ran unoptimised until the runtime tiered them up: about 1.5 s on the desktop at 4.5 ms a step against 1.2. Compiled
    /// code belongs to the callback types, not to a world, so a world that is thrown away compiles the one the level
    /// builds, and stepped for a few seconds it is tiered up as well.
    /// </para>
    /// <para>
    /// <b>It also puts its cluster to sleep and wakes it</b> (#1241), every <see cref="SLEEP_EVERY_SLICES"/>th slice: a
    /// level's cluster falls asleep a few seconds after it settles and a shot wakes it, and those passes are compiled code of
    /// their own that stepping never reaches.
    /// </para>
    /// <para>
    /// It steps in slices of <see cref="SLICE_MILLISECONDS"/> with <see cref="REST_MILLISECONDS"/> between them, on one
    /// worker fewer than the machine has, so the menu's frames keep a core. <see cref="Stop"/> ends it early: a level's
    /// release and a benchmark must not share the cores with it. What it compiled by then stays compiled. The front end's
    /// quality probe measures no window while it runs (<see cref="Running"/>).
    /// </para>
    /// </summary>
    internal static class PhysicsWarmup
    {
        /// <summary>How long it steps, wall time: the desktop's quick-JIT code was tiered up 2.2-3.1 s in.</summary>
        internal const float SECONDS = 4f;

        /// <summary>The steps of one slice, by wall time.</summary>
        internal const double SLICE_MILLISECONDS = 2.0;

        /// <summary>The rest after each slice.</summary>
        internal const int REST_MILLISECONDS = 6;

        /// <summary>
        /// Every this many slices the cluster is put to sleep and woken again (<see cref="PhysicsBench.SleepAndWake"/>, #1241),
        /// from the first slice on, which made its contacts. Bepu's sleep and wake run only when a cluster settles and when a
        /// shot wakes it, and this world's cluster is kept awake, so without them a launch compiled both in its first level.
        /// In the Game on a Ryzen 9 5900X, Eleven's first sleep made a step of 18.9-19.2 ms; after the warm-up's 57-64 cycles,
        /// 3.3-3.5 ms, beside the 2.0-2.3 of the steps around it. One cycle compiles them and the rest run them until the
        /// runtime optimises them, at a quarter of the slices so that the steps' own tier-up is not pushed back.
        /// </summary>
        internal const int SLEEP_EVERY_SLICES = 4;

        /// <summary>Enough steps for <see cref="SECONDS"/> at any machine's pace: the bench's figures are not read.</summary>
        private const int STEPS = 4000;

        /// <summary>The field: 7 x 7, its top <see cref="FULL_LEVELS"/> levels full.</summary>
        private const byte FIELD = 7, LEVELS = 8, FULL_LEVELS = 4;

        /// <summary>
        /// A breeze, only so that the cluster never sleeps (<see cref="PhysicsBench"/> keeps it awake under any wind): a
        /// sleeping cluster is skipped by the solver, which would then compile nothing.
        /// </summary>
        private static readonly WindField BREEZE = new(new System.Numerics.Vector2(0.87f, 0.5f), 0.05f);

        private static Thread _thread;
        private static volatile bool _stop, _running;

        /// <summary>
        /// Whether it is still stepping. The front end's quality probe waits for it: its threads are no part of what a frame
        /// of the game costs, and on a machine whose processor and graphics share one power budget they could cost the menu
        /// a tier for the whole session.
        /// </summary>
        internal static bool Running => _running;

        /// <summary>Starts it, once a run; a second call does nothing.</summary>
        internal static void Start()
        {
            if (_thread != null) return;
            _running = true;
            _thread = new Thread(Run) { Name = "physics warm-up", IsBackground = true, Priority = ThreadPriority.BelowNormal };
            _thread.Start();
        }

        /// <summary>Ends it after the slice it is in, if it is still stepping.</summary>
        internal static void Stop() => _stop = true;

        /// <summary>Waits up to <paramref name="milliseconds"/> for it to end; true when it has (or never started).</summary>
        internal static bool Join(int milliseconds) => _thread == null || _thread.Join(milliseconds);

        /// <summary>
        /// The throwaway world: the glass, the funnel's floor and a 196-ball cluster with every kind of socket the game
        /// builds (to the glass, within a level, between levels), swinging from its release under <see cref="BREEZE"/>.
        /// </summary>
        internal static PhysicsBench Build() =>
            new(Cluster(), BREEZE, STEPS, workerThreads: Math.Max(2, Environment.ProcessorCount - 1));

        private static BallsMap Cluster()
        {
            BallsMap map = new(FIELD, FIELD, LEVELS);
            for (int level = LEVELS - FULL_LEVELS; level < LEVELS; level++)
                for (byte x = 0; x < FIELD; x++)
                    for (byte z = 0; z < FIELD; z++)
                        map.PutBallAt(x, z, (byte)level, (BallType)(1 + (x + z + level) % 4));
            return map;
        }

        private static void Run()
        {
            long start = Stopwatch.GetTimestamp();
            try
            {
                using PhysicsBench bench = Build();

                double firstSlice = 0;
                int slices = 0;
                while (!_stop && !bench.Done && Stopwatch.GetElapsedTime(start).TotalSeconds < SECONDS)
                {
                    long slice = Stopwatch.GetTimestamp();
                    bench.Run(SLICE_MILLISECONDS);
                    if (firstSlice == 0) firstSlice = Stopwatch.GetElapsedTime(slice).TotalMilliseconds;
                    if (slices++ % SLEEP_EVERY_SLICES == 0 && !_stop) bench.SleepAndWake();
                    Thread.Sleep(REST_MILLISECONDS);
                }

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "[warmup] physics solver compiled on a {0}-ball world: {1} steps and {2} sleeps in {3:F1} s, the first slice {4:F0} ms, the last slice's steps {5:F2} ms{6}",
                    bench.Balls, bench.Taken, bench.SleepCycles, Stopwatch.GetElapsedTime(start).TotalSeconds, firstSlice,
                    bench.SliceMean, _stop ? ", stopped early" : ""));
            }
            catch (Exception e)
            {
                //Nothing waits for it: the first level compiles whatever is left at its release, as it did before
                Console.WriteLine($"[warmup] failed, the first level compiles the physics solver at its release: {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                _running = false;
            }
        }
    }
}
