using BS3D.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Tools;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// The benchmark's play (#1018): a level played by nobody, for Extras &gt; Benchmark to measure what play draws. The
    /// owner: "in the benchmark I do not see the cannon... the shot from the city where the camera flies all the way to the
    /// cannon should be part of the benchmark", and "scenes where the cannon fires at the map and balls fall, with a camera
    /// other than the game's".
    /// <para>
    /// <b>A benchmark run is marked</b> (<see cref="LevelRun.Benchmark"/>) and writes nothing a player's run writes: no play
    /// count (started, ended, abandoned), no record in the save, nothing to the online boards, no lesson taught and no
    /// chapter's opening marked seen. It is dealt off <see cref="BENCHMARK_SEED"/>, plays its chapter's whole intro (the
    /// city's ends at the gun), hides the HUD, and then the gun plays by itself under <see cref="BenchmarkDemo"/>'s lens, as a
    /// player's hand would (the owner: "it should stay there and aim smoothly, as if a player controlled it"): the barrel
    /// glides to its mark, holds still on it, fires along it and stays a moment before it moves on. The mark is where the aim's
    /// own solver says a shot would hit a ball of the loaded colour with neighbours of that colour, so the shot completes a
    /// group and it falls; two cuts on the way. Keys and the pad's buttons end the benchmark rather than pause or skip; a lost
    /// focus pauses nothing.
    /// </para>
    /// </summary>
    internal sealed partial class GameplayScreen
    {
        /// <summary>The deal of every benchmark run, so two runs of one machine play the same level the same way.</summary>
        internal const int BENCHMARK_SEED = 1018;

        //The hand on the gun, as a player's is: the barrel glides to its mark on a critically damped spring (SPRING, in radians
        //a second, so it eases out of its start and into the mark) no faster than MAX_RATE, fires once it has held still on the
        //mark for DWELL, and stays on the shot's line for HOLD before it moves on (AFTER_CUT for a cutter, so its strike and its
        //slow fall are seen). Settled is within SETTLE_ANGLE of the mark and turning slower than SETTLE_RATE. A frame longer than
        //MAX_STEP is stepped as that, so a hitch cannot fling the spring
        private const float DEMO_SPRING = 7f, DEMO_MAX_RATE = 1.4f, DEMO_DWELL = 0.18f, DEMO_HOLD = 0.3f, DEMO_AFTER_CUT = 2.6f;
        private const float DEMO_SETTLE_ANGLE = 0.004f, DEMO_SETTLE_RATE = 0.06f, DEMO_MAX_STEP = 0.05f;

        //A cutter's aim a little under the ball it is aimed at, and a plain round's a little over: a shot falls on its way
        private const float DEMO_AIM_LIFT = 0.25f;

        //The seconds into the action a cut is given and loaded
        private static readonly float[] DEMO_CUTS = { 7f, 15.5f };

        //How a mark is picked: aims at this many random balls the gun can reach (out of at most ATTEMPTS cells looked at), each
        //solved for the ball it would hit first; the most neighbours of its own colour counted on a hit of the loaded colour
        private const int DEMO_CANDIDATES = 28, DEMO_ATTEMPTS = 400, DEMO_NEIGHBOURS_COUNTED = 3;

        private readonly BenchmarkDemo _demo = new();
        private bool _benchmarkNext;
        private Random _demoRandom;
        private float _demoNextFire, _demoHoldUntil, _demoSettledFor;
        private float _demoElevationRate, _demoTraverseRate;
        private int _demoCutsUsed;
        private bool _demoHasMark, _demoCutterLoaded, _demoReported;
        private PhysicsBall _demoMarkBall;
        private XZLevel _demoMarkCell;
        private float _demoMarkLift;
        private int _demoShots, _demoMatched, _demoOrphaned, _demoDestroyed;

        /// <summary>Where a benchmark run stands, for the page that measures it.</summary>
        internal enum BenchmarkStage
        {
            /// <summary>No benchmark run is standing.</summary>
            None,

            /// <summary>The chapter's intro is flying, or the cluster has not been handed the gun yet.</summary>
            Intro,

            /// <summary>The gun is firing by itself.</summary>
            Action,

            /// <summary>The action has had its time, or the level is decided.</summary>
            Done,
        }

        internal BenchmarkStage BenchmarkPlay =>
            !IsBuilt || _run == null || !_run.Benchmark ? BenchmarkStage.None
            : _demo.Done || LevelDecided ? BenchmarkStage.Done
            : _demo.Running ? BenchmarkStage.Action
            : BenchmarkStage.Intro;

        /// <summary>Builds <paramref name="index"/> as a benchmark run (see the class's remarks).</summary>
        internal void BuildBenchmarkLevel(int index)
        {
            _benchmarkNext = true;
            try
            {
                BuildLevel(index);
            }
            finally
            {
                _benchmarkNext = false;
            }
        }

        /// <summary>
        /// A benchmark run's input: the keys and buttons that would skip or pause end the benchmark instead, once the window
        /// has the focus. Not the mouse: a click is how a window is brought forward, and one that lands on the benchmark's
        /// window while someone works beside it must not end two minutes of measuring. True when it ended (the frame stops,
        /// as a pause's does).
        /// </summary>
        private bool StepBenchmarkInput()
        {
            Game.IsMouseVisible = true;
            if (!Game.IsActive || !Game.EdgeInputAllowed) return false;

            KeyboardState keyboard = Keyboard.GetState();
            GamePadState pad = GamePad.GetState(PlayerIndex.One);
            bool pressed = Game.IsKeyEdge(keyboard, Keys.Escape) || Game.IsKeyEdge(keyboard, Keys.Space) || Game.IsKeyEdge(keyboard, Keys.Enter)
                || Edge(pad, Buttons.A) || Edge(pad, Buttons.B) || Edge(pad, Buttons.Back) || Edge(pad, Buttons.Start);
            //The window's own keys, as in play
            if (Game.IsKeyEdge(keyboard, Keys.F12)) Game.RequestScreenshot();
            if (Game.IsKeyEdge(keyboard, Keys.F11)) Game.ToggleFullscreen();
            if (Game.IsKeyEdge(keyboard, Keys.F10)) Game.ToggleFpsOverlay();

            Game.PreviousKeyboard = keyboard;
            Game.PreviousPad = pad;
            if (!pressed) return false;

            Console.WriteLine("[skip] benchmark play");
            Game.StopBenchmarkPlay();
            return true;
        }

        private bool Edge(GamePadState pad, Buttons button) => pad.IsButtonDown(button) && !Game.PreviousPad.IsButtonDown(button);

        /// <summary>
        /// The action, once the intro has handed the gun back: the lens taken, and the gun aimed and fired as a player's hand
        /// would (the class's constants), a cut loaded at each of <see cref="DEMO_CUTS"/>. Nothing while a takeover has the
        /// camera (a drop's cinematic films what fell), nor once the level is decided.
        /// </summary>
        private void StepBenchmarkDemo(float elapsed)
        {
            if (!_demo.Running)
            {
                if (_chapterIntro.Running || LevelDecided) return;

                _demo.Begin(new Vector3(_cannon.OrbitCenter.X, _clusterCentreY, _cannon.OrbitCenter.Z), _cannon.Position,
                    _gameCameraDistance, GAME_FOV);
                _demoRandom = new Random(BENCHMARK_SEED);
                _demoNextFire = BenchmarkDemo.BLEND_IN * 0.5f;
                _demoHoldUntil = _demoSettledFor = _demoElevationRate = _demoTraverseRate = 0f;
                _demoCutsUsed = 0;
                _demoHasMark = _demoCutterLoaded = _demoReported = false;
                _demoShots = _run.Score.ShotsFired;
                _demoMatched = _run.Score.MatchedBalls;
                _demoOrphaned = _run.Score.OrphanedBalls;
                _demoDestroyed = _run.Score.DestroyedBalls;
                Console.WriteLine("[bench] action: the gun fires by itself");
                return;
            }

            _demo.Step(elapsed);
            if ((_demo.Done || LevelDecided) && !_demoReported)
            {
                //What the action did, once, so a run says whether groups fell
                _demoReported = true;
                Console.WriteLine($"[bench] action: {_run.Score.ShotsFired - _demoShots} shots ({_demoCutsUsed} cuts), "
                    + $"{_run.Score.MatchedBalls - _demoMatched} balls matched, {_run.Score.OrphanedBalls - _demoOrphaned} fell loose, "
                    + $"{_run.Score.DestroyedBalls - _demoDestroyed} cut away{(LevelDecided ? ", the level decided" : "")}");
            }
            if (_demo.Done || LevelDecided || CameraTakeoverEngaged) return;

            //On the shot's line for a moment after it leaves, as a hand that has just fired is
            if (_demo.Age < _demoHoldUntil) return;

            //A cut at its second: a charge given and the round in the bore turned into a cutter, as R does
            if (_demoCutsUsed < DEMO_CUTS.Length && _demo.Age >= DEMO_CUTS[_demoCutsUsed] && !_demoCutterLoaded)
            {
                _demoCutsUsed++;
                _run.PowerupCharges[(int)PowerupKind.Cut]++;
                _run.CutOffered = true;
                if (CanActivate(PowerupKind.Cut))
                {
                    Activate(PowerupKind.Cut);
                    _demoCutterLoaded = true;
                    _demoHasMark = false;
                }
            }

            if (!_demoHasMark)
            {
                _demoHasMark = ChooseDemoMark();
                _demoSettledFor = 0f;
                if (!_demoHasMark) return;
            }

            //The mark is a ball, followed as the cluster sways; one that has fallen or been popped is let go
            if (_physicsBalls[_demoMarkCell.X, _demoMarkCell.Z, _demoMarkCell.Level] != _demoMarkBall
                || !_cannon.CanAimAt(_demoMarkBall.BallReference.Pose.Position.ToXna() + Vector3.Up * _demoMarkLift,
                    out float elevation, out float traverse))
            {
                _demoHasMark = false;
                return;
            }

            float dt = MathF.Min(elapsed, DEMO_MAX_STEP);
            float e = _cannon.Elevation, t = _cannon.Traverse;
            Glide(ref e, ref _demoElevationRate, elevation, dt);
            Glide(ref t, ref _demoTraverseRate, traverse, dt);
            _cannon.AimTo(e, t);

            bool settled = MathF.Abs(elevation - e) < DEMO_SETTLE_ANGLE && MathF.Abs(traverse - t) < DEMO_SETTLE_ANGLE
                && MathF.Abs(_demoElevationRate) < DEMO_SETTLE_RATE && MathF.Abs(_demoTraverseRate) < DEMO_SETTLE_RATE;
            _demoSettledFor = settled ? _demoSettledFor + elapsed : 0f;
            if (_demoSettledFor < DEMO_DWELL || _demo.Age < _demoNextFire) return;

            Shoot();
            _demoHoldUntil = _demoNextFire = _demo.Age + (_demoCutterLoaded ? DEMO_AFTER_CUT : DEMO_HOLD);
            _demoCutterLoaded = false;
            _demoHasMark = false;
        }

        //One axis of the hand: a critically damped spring towards the mark, its rate capped
        private static void Glide(ref float angle, ref float rate, float mark, float dt)
        {
            rate += (DEMO_SPRING * DEMO_SPRING * (mark - angle) - 2f * DEMO_SPRING * rate) * dt;
            rate = Math.Clamp(rate, -DEMO_MAX_RATE, DEMO_MAX_RATE);
            angle += rate * dt;
        }

        /// <summary>
        /// The next ball to aim at: of <see cref="DEMO_CANDIDATES"/> random balls the gun can reach, the one whose aim, solved
        /// as the aim's preview solves it (<see cref="ShotPlacement.TryFindFirstHitCurved"/>), first hits a ball of the loaded
        /// round's colour with the most neighbours of that colour: the shot then completes a group and it falls. A cutter takes
        /// any hit. A roll of the run's own generator breaks the ties, so the shots spread over the cluster.
        /// </summary>
        private bool ChooseDemoMark()
        {
            if (_physicsBalls == null) return false;

            BallType loaded = LoadedColour(0);
            XZLevel size = new(_physicsBalls.GetLength(0), _physicsBalls.GetLength(1), _physicsBalls.GetLength(2));
            float lift = _demoCutterLoaded ? -DEMO_AIM_LIFT : DEMO_AIM_LIFT;
            float best = float.MinValue;
            int candidates = 0;

            for (int attempt = 0; attempt < DEMO_ATTEMPTS && candidates < DEMO_CANDIDATES; attempt++)
            {
                XZLevel cell = new(_demoRandom.Next(size.X), _demoRandom.Next(size.Z), _demoRandom.Next(size.Level));
                PhysicsBall ball = _physicsBalls[cell.X, cell.Z, cell.Level];
                if (ball == null) continue;

                Vector3 at = ball.BallReference.Pose.Position.ToXna() + Vector3.Up * lift;
                if (!_cannon.CanAimAt(at, out _, out _)) continue;
                candidates++;

                Vector3 direction = Vector3.Normalize(at - _cannon.Position);
                Vector3 muzzle = _cannon.Position + direction * Game.CannonRig.PivotToFrontBall;
                if (!ShotPlacement.TryFindFirstHitCurved(_physicsBalls, muzzle, direction * SHOOT_SPEED,
                        2f * BallsConstraintsBuilder.BALL_RADIUS, _gravityWells, out PhysicsBall hit, out _, null, _crates,
                        worldGravity: true))
                    continue;

                float score = (float)_demoRandom.NextDouble();
                if (!_demoCutterLoaded && hit.Type == loaded) score += 2f + SameColourNeighbours(hit, size);
                if (score <= best) continue;

                best = score;
                _demoMarkBall = ball;
                _demoMarkCell = cell;
                _demoMarkLift = lift;
            }

            return best > float.MinValue;
        }

        //How many of a ball's neighbours share its colour, up to DEMO_NEIGHBOURS_COUNTED
        private int SameColourNeighbours(PhysicsBall ball, XZLevel size)
        {
            int same = 0;
            foreach (XZLevel cell in BallsMap.GetNeighboringCells(ball.ArrayPosition, size))
                if (_physicsBalls[cell.X, cell.Z, cell.Level] is PhysicsBall neighbour && neighbour.Type == ball.Type
                    && ++same >= DEMO_NEIGHBOURS_COUNTED) break;
            return same;
        }
    }
}
