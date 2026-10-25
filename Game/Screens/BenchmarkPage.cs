using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using Prazsky.Core.Render;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// Extras > Benchmark (#988, first stage): the owner wants a benchmark that finds the surprises on hardware we do not own
    /// (Intel, Qualcomm, other drivers, the Pi and whatever follows it) and is worth watching. It flies the chapter tour of
    /// each of <see cref="SEGMENTS"/>, the dearest scenes, at the player's own tier with the frame left free of the display's
    /// refresh (<see cref="BS3DGame.BeginBenchmark"/>), each tour on a fixed seed so two runs of one machine fly one path, and
    /// measures every frame. A segment's first <see cref="SETTLE_SECONDS"/> are the scene's first-use cost (shaders, a hall
    /// built off the frame) and are reported apart. The result is shown here, written to the log as <c>[bench]</c> lines and
    /// to <c>Benchmark.json</c> in the player's profile with the machine it ran on (<see cref="BS3DGame.BenchmarkSystem"/>);
    /// nothing is changed and nothing is sent. The skip key stops it. After the tours the CPU's physics is timed on its own
    /// (<see cref="PhysicsBench"/>), the largest shipped cluster stepped off screen.
    /// </summary>
    internal sealed class BenchmarkPage : MenuPage
    {
        //The dearest scenes on the desktop's High (docs/game-shell/quality-measurements.md): the cavern's chamber, the neon
        //city's towers, the forest's wood and the meadow's trees under the volume clouds
        private static readonly SceneKind[] SEGMENTS = { SceneKind.Cavern, SceneKind.NeonCity, SceneKind.Forest, SceneKind.Meadow };
        private const float SETTLE_SECONDS = 1.5f;
        private const int SEED = 988;

        private enum Phase { Idle, Settling, Touring, Physics, Done }

        private Phase _phase = Phase.Idle;
        private int _segment;
        private float _phaseSeconds;
        private long _lastTick;
        private readonly FrameStats _frames = new(), _settle = new(512);
        private readonly FrameStats.Summary[] _results = new FrameStats.Summary[SEGMENTS.Length];
        private readonly FrameStats.Summary[] _settles = new FrameStats.Summary[SEGMENTS.Length];
        private int _measured;

        //The probe's verdict on each segment's frame (FrameProbe), asked PROBE_AT seconds into its tour; and whether the frame
        //after a probe is still to be left out of the figures (the read back stalled it)
        private const float PROBE_AT = 3f;
        private readonly FrameProbe.Verdict[] _probes = new FrameProbe.Verdict[SEGMENTS.Length];
        private readonly float[] _probeMean = new float[SEGMENTS.Length], _probeSpread = new float[SEGMENTS.Length];
        //The HDR scene's patches: how many texels were read (0: none, the Potato path), how many were NaN or infinite, the brightest
        private readonly int[] _hdrRead = new int[SEGMENTS.Length], _hdrNonFinite = new int[SEGMENTS.Length];
        private readonly float[] _hdrBrightest = new float[SEGMENTS.Length];
        private bool _probed, _skipNext;

        //The physics segment (PhysicsBench): stepped a frame's slice at a time so the window keeps answering on a slow machine;
        //measured is false until every step is taken
        private const double PHYSICS_SLICE_MILLISECONDS = 25.0;
        private PhysicsBench _physics;
        private FrameStats.Summary _physicsResult, _physicsWarmup;
        private int _physicsBalls;
        private bool _physicsMeasured;

        //What the result was measured at, kept with it: the page is kept between visits and the player may change the tier
        //in Settings before coming back, and a result judged against a tier it never ran at would be a verdict on nothing.
        //And whether the run went to its end: a stopped one is shown, never saved and never acted on
        private QualityLevel _measuredTier;
        private JsonObject _measuredSystem;
        private bool _complete;
        private readonly PlayHud _skipHint;
        private Button _firstEntry;

        public BenchmarkPage(BS3DGame game) : base(game) => _skipHint = new PlayHud(game);

        private bool Running => _phase is Phase.Settling or Phase.Touring or Phase.Physics;

        internal override bool StandsAside => Running;

        internal override Button NavArrival => _firstEntry;

        internal override bool CanGoBack => !Running;

        //The front end's camera flies the tour behind nothing: the page is aside while it runs
        internal override bool DimsFrame => false;

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();
            column.Widgets.Add(ScreenHeading("BENCHMARK"));

            if (_phase == Phase.Done && _measured > 0) AddResults(column);
            else
            {
                column.Widgets.Add(Paragraph(string.Create(CultureInfo.InvariantCulture,
                    $"Flies through {SEGMENTS.Length} of the most demanding scenes at your quality ({Game.QualityLabel}), with the frame rate unlocked, and measures every frame, then times the physics of the biggest level. About a minute. The skip key stops it."),
                    ProseMeasure));
                if (!Game.SceneryShown && !BS3DGame.PotatoPath)
                    column.Widgets.Add(Paragraph("The scenery is off in your settings: the benchmark shows it, so that it has something to measure.", ProseMeasure));
            }

            _firstEntry = MenuButton(_phase == Phase.Done ? "Run again" : "Start", Start);
            column.Widgets.Add(_firstEntry);
            column.Widgets.Add(BackButton());
            return ScreenRoot(Plate(column));
        }

        private void AddResults(VerticalStackPanel column)
        {
            float budget = Budget;
            for (int i = 0; i < _measured; i++)
            {
                FrameStats.Summary s = _results[i];
                string line = string.Create(CultureInfo.InvariantCulture,
                    $"{SceneRenderer.SceneName(SEGMENTS[i])}: {s.MedianFps:0} FPS · 1 % low {s.OnePercentLowFps:0} · {s.OverBudgetShare * 100f:0} % over {budget:0.0} ms");
                column.Widgets.Add(new Label
                {
                    Text = line,
                    Font = FontBody,
                    TextColor = BS3DGame.MENU_TEXT,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = ScaledThickness(0, 6, 0, 6),
                });
            }

            //The physics: a step's time and the share of every frame it takes, which no quality tier changes
            if (_physicsMeasured)
            {
                float share = PhysicsBench.TimeShare(_physicsResult.Median);
                column.Widgets.Add(new Label
                {
                    Text = string.Create(CultureInfo.InvariantCulture,
                        $"Physics ({_physicsBalls} balls): {_physicsResult.Median:0.00} ms a step · p99 {_physicsResult.P99:0.00} ms · {share * 100f:0} % of every frame"),
                    Font = FontBody,
                    TextColor = BS3DGame.MENU_TEXT,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = ScaledThickness(0, 6, 0, 6),
                });
                if (share > PHYSICS_HEAVY_SHARE)
                    column.Widgets.Add(Paragraph("The physics alone takes most of each frame on this processor: the biggest levels may run slow at any quality.", ProseMeasure));
            }

            //A frame the probe found wrong is the surprise this page exists to find: said plainly, with where to send it
            for (int i = 0; i < _measured; i++)
                if (_probes[i] is FrameProbe.Verdict.Black or FrameProbe.Verdict.Flat or FrameProbe.Verdict.NonFinite)
                    column.Widgets.Add(Paragraph($"{SceneRenderer.SceneName(SEGMENTS[i])} drew {ProbeFault(_probes[i])}: your graphics driver may not draw this game right. Please tell the author: Extras > {BS3DGame.NOTE_ENTRY}.", ProseMeasure));

            //What the numbers recommend (BenchmarkVerdict) for the tier they were measured at, offered as an entry and never
            //applied unasked; a stopped run says how far it got and offers nothing
            QualityLevel tier = _measuredTier;
            QualityLevel recommended = BenchmarkVerdict.Recommend(tier, _results, _measured, out int decidedBy);
            int hz = Game.DisplayRefreshHz > 0 ? Game.DisplayRefreshHz : 60;
            string verdict = recommended < tier
                ? string.Create(CultureInfo.InvariantCulture,
                    $"{tier} drops frames in {SceneRenderer.SceneName(SEGMENTS[decidedBy])} ({_results[decidedBy].OverBudgetShare * 100f:0} % over {hz} Hz). {recommended} should hold it.")
                : recommended > tier
                    ? $"{tier} holds {hz} Hz in every scene with room to spare. {recommended} may hold it too."
                    : $"{tier} holds your display's {hz} Hz.";
            if (!_complete)
                verdict = string.Create(CultureInfo.InvariantCulture, $"Stopped after {_measured} of {SEGMENTS.Length} scenes. ") + verdict;
            if (Game.Quality != tier) verdict += $" Measured at {tier}; your quality is {Game.Quality} now.";
            column.Widgets.Add(Paragraph(verdict, ProseMeasure));
            if (_complete && recommended != tier && recommended != Game.Quality && Game.CanOfferQuality)
                column.Widgets.Add(MenuButton(recommended > tier ? $"Try {recommended}" : $"Use {recommended}", () => TakeOffer(recommended)));

            JsonObject system = _measuredSystem ?? Game.BenchmarkSystem();
            column.Widgets.Add(new Label
            {
                Text = $"{system["gpu"]} · {system["backBuffer"]} · {system["quality"]} · {(_complete ? "saved as Benchmark.json" : "not saved")}",
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 20, 0, 20),
            });
        }

        //A failed probe in the page's words
        private static string ProbeFault(FrameProbe.Verdict verdict) => verdict switch
        {
            FrameProbe.Verdict.Black => "a black frame",
            FrameProbe.Verdict.Flat => "a one-colour frame",
            _ => "broken values in its picture",
        };

        /// <summary>Whether the game should read the next finished frame back for the probe: once a segment, a few seconds into its tour.</summary>
        internal bool WantsProbe => _phase == Phase.Touring && !_probed && _phaseSeconds >= PROBE_AT;

        /// <summary>
        /// The frame read back (null when it could not be) and <paramref name="hdrCount"/> texels of the HDR scene behind it:
        /// the probe's verdict on this segment, and the next frame left out.
        /// </summary>
        internal void TakeProbe(Color[] frame, int width, int height, Microsoft.Xna.Framework.Graphics.PackedVector.HalfVector4[] hdr, int hdrCount)
        {
            _probed = true;
            _skipNext = true;
            int i = _segment;
            _probes[i] = FrameProbe.Check(frame, width, height, out _probeMean[i], out _probeSpread[i]);
            _hdrRead[i] = hdr == null ? 0 : hdrCount;
            _hdrNonFinite[i] = _hdrRead[i] > 0 ? FrameProbe.CountNonFinite(hdr, hdrCount, out _hdrBrightest[i]) : 0;
            if (_probes[i] == FrameProbe.Verdict.Ok && _hdrNonFinite[i] > 0) _probes[i] = FrameProbe.Verdict.NonFinite;
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"[bench] {SceneRenderer.SceneName(SEGMENTS[i])} frame {_probes[i]} (mean {_probeMean[i]:0.000}, spread {_probeSpread[i]:0.000}; HDR {_hdrRead[i]} texels, {_hdrNonFinite[i]} not finite, brightest {_hdrBrightest[i]:0.00})"));
        }

        //The offer taken: the tier on, and the page ready to run again on it
        private void TakeOffer(QualityLevel tier)
        {
            Game.ChooseQuality(tier);
            _phase = Phase.Idle;
            Rebuild();
        }

        //The share of every frame past which the physics is said to be the machine's limit: no tier lowers it
        private const float PHYSICS_HEAVY_SHARE = 0.5f;

        //A frame's budget: one frame of the display's refresh, or 60 Hz's where the display does not say
        private float Budget => 1000f / (Game.DisplayRefreshHz > 0 ? Game.DisplayRefreshHz : 60);

        /// <summary>Starts at once, without the press (the <c>benchmark</c> argument's, #988).</summary>
        internal void StartNow() => Start();

        private void Start()
        {
            if (Running) return;
            _segment = 0;
            _measured = 0;
            _physicsMeasured = false;
            _complete = false;
            Game.BeginBenchmark();
            BeginSegment();
        }

        private void BeginSegment()
        {
            Game.SetScene(SEGMENTS[_segment]);
            _phase = Phase.Settling;
            _phaseSeconds = 0f;
            _frames.Clear();
            _settle.Clear();
            _probed = false;
            _probes[_segment] = FrameProbe.Verdict.Ok;
            _hdrRead[_segment] = _hdrNonFinite[_segment] = 0;
            _hdrBrightest[_segment] = 0f;
            _lastTick = Stopwatch.GetTimestamp();
            Console.WriteLine($"[bench] segment {_segment + 1}/{SEGMENTS.Length} {SceneRenderer.SceneName(SEGMENTS[_segment])}");
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            BackdropScreen backdrop = Manager?.Find<BackdropScreen>();
            _skipHint.UpdateSkipHint((float)gameTime.ElapsedGameTime.TotalSeconds, Running, chapterIntro: true,
                Running ? _phaseSeconds : 0f, 0f);
            if (_phase == Phase.Physics)
            {
                StepPhysics();
                return;
            }
            if (!Running || backdrop == null) return;

            //The wall time since the last frame: what the frame cost, the limiter let go
            long now = Stopwatch.GetTimestamp();
            float milliseconds = (float)((now - _lastTick) * 1000.0 / Stopwatch.Frequency);
            _lastTick = now;
            _phaseSeconds += milliseconds / 1000f;

            if (_phase == Phase.Settling)
            {
                _settle.Add(milliseconds);
                if (_phaseSeconds < SETTLE_SECONDS) return;
                _phase = Phase.Touring;
                _phaseSeconds = 0f;
                backdrop.PlayTour(new Random(SEED + _segment));
                return;
            }

            if (_skipNext) _skipNext = false;
            else _frames.Add(milliseconds);
            if (backdrop.TourRunning || _phaseSeconds < 0.5f) return;

            //The tour is home: this segment's figures
            float budget = Budget;
            _results[_segment] = _frames.Summarize(budget);
            _settles[_segment] = _settle.Summarize(budget);
            _measured = _segment + 1;
            Log(_segment);

            if (++_segment < SEGMENTS.Length) BeginSegment();
            else BeginPhysics();
        }

        //The physics segment: the largest shipped cluster hung off screen; a level that cannot be read is said and skipped
        private void BeginPhysics()
        {
            string path = Path.Combine(AppContext.BaseDirectory, BS3DGame.LEVELS_DIRECTORY, PhysicsBench.LEVEL + ".json");
            try
            {
                Level level = Level.Load(path);
                _physics = new PhysicsBench(new BallsMap(level.Map), SceneWind.For(level.Scene ?? SceneKind.City, null));
                _physicsBalls = _physics.Balls;
                _phase = Phase.Physics;
                Console.WriteLine($"[bench] physics {PhysicsBench.LEVEL}: {_physicsBalls} balls, {Environment.ProcessorCount} threads");
            }
            catch (Exception e)
            {
                //Whatever the reason (a missing file, a map the builder refuses, a machine out of memory), the tours stand
                //measured: said in the log and finished without the physics, never the game taken down mid-benchmark
                Console.WriteLine($"[bench] physics skipped: {e.GetType().Name}: {e.Message}");
                DisposePhysics();
                Finish(complete: true);
            }
        }

        private void StepPhysics()
        {
            _physics.Run(PHYSICS_SLICE_MILLISECONDS);
            if (!_physics.Done) return;

            _physicsResult = _physics.Summarize();
            _physicsWarmup = _physics.SummarizeWarmup();
            _physicsMeasured = true;
            DisposePhysics();
            FrameStats.Summary s = _physicsResult, first = _physicsWarmup;
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"[bench] physics: {s.Frames} steps, mean {s.Mean:0.000} ms, median {s.Median:0.000}, p95 {s.P95:0.000}, p99 {s.P99:0.000}, worst {s.Worst:0.00} ms, {s.OverBudgetShare * 100f:0.0} % over {PhysicsBench.STEP_BUDGET_MILLISECONDS:0.00} ms, {PhysicsBench.TimeShare(s.Median) * 100f:0.0} % of every frame; first {PhysicsBench.WARMUP_STEPS} steps: worst {first.Worst:0.0} ms, median {first.Median:0.000}"));
            Finish(complete: true);
        }

        private void DisposePhysics()
        {
            _physics?.Dispose();
            _physics = null;
        }

        private void Log(int i)
        {
            FrameStats.Summary s = _results[i], first = _settles[i];
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"[bench] {SceneRenderer.SceneName(SEGMENTS[i])}: {s.Frames} frames, mean {s.Mean:0.00} ms, median {s.Median:0.00}, p95 {s.P95:0.00}, p99 {s.P99:0.00}, 1% low {s.OnePercentLowFps:0.0} FPS, worst {s.Worst:0.0} ms, {s.OverBudgetShare * 100f:0.0} % over {Budget:0.00} ms; first {SETTLE_SECONDS:0.0} s: worst {first.Worst:0.0} ms, median {first.Median:0.00}"));
        }

        //The run's end: what it measured kept with the tier and the machine it measured them on, and saved only when it went
        //to its end, so a run stopped after one scene never overwrites a whole one
        private void Finish(bool complete)
        {
            _phase = Phase.Done;
            _complete = complete;
            _measuredTier = Game.Quality;
            JsonObject system = _measuredSystem = Game.BenchmarkSystem();
            Console.WriteLine($"[bench] system {system.ToJsonString()}");

            var segments = new JsonArray();
            for (int i = 0; i < _measured; i++)
            {
                FrameStats.Summary s = _results[i], first = _settles[i];
                segments.Add(new JsonObject
                {
                    ["scene"] = SceneRenderer.SceneName(SEGMENTS[i]),
                    ["frames"] = s.Frames,
                    ["meanMs"] = Math.Round(s.Mean, 3),
                    ["medianMs"] = Math.Round(s.Median, 3),
                    ["p95Ms"] = Math.Round(s.P95, 3),
                    ["p99Ms"] = Math.Round(s.P99, 3),
                    ["onePercentLowFps"] = Math.Round(s.OnePercentLowFps, 1),
                    ["worstMs"] = Math.Round(s.Worst, 2),
                    ["overBudgetShare"] = Math.Round(s.OverBudgetShare, 4),
                    ["firstSecondsWorstMs"] = Math.Round(first.Worst, 2),
                    ["firstSecondsMedianMs"] = Math.Round(first.Median, 3),
                    ["frame"] = _probes[i].ToString(),
                    ["hdrTexels"] = _hdrRead[i],
                    ["hdrNonFinite"] = _hdrNonFinite[i],
                    ["hdrBrightest"] = Math.Round(_hdrBrightest[i], 3),
                });
            }
            JsonObject physics = null;
            if (_physicsMeasured)
                physics = new JsonObject
                {
                    ["level"] = PhysicsBench.LEVEL,
                    ["balls"] = _physicsBalls,
                    ["threads"] = Environment.ProcessorCount,
                    ["steps"] = _physicsResult.Frames,
                    ["stepBudgetMs"] = Math.Round(PhysicsBench.STEP_BUDGET_MILLISECONDS, 3),
                    ["meanMs"] = Math.Round(_physicsResult.Mean, 4),
                    ["medianMs"] = Math.Round(_physicsResult.Median, 4),
                    ["p95Ms"] = Math.Round(_physicsResult.P95, 4),
                    ["p99Ms"] = Math.Round(_physicsResult.P99, 4),
                    ["worstMs"] = Math.Round(_physicsResult.Worst, 3),
                    ["overBudgetShare"] = Math.Round(_physicsResult.OverBudgetShare, 4),
                    ["frameShare"] = Math.Round(PhysicsBench.TimeShare(_physicsResult.Median), 4),
                    ["warmupWorstMs"] = Math.Round(_physicsWarmup.Worst, 3),
                    ["warmupMedianMs"] = Math.Round(_physicsWarmup.Median, 4),
                };
            QualityLevel recommended = BenchmarkVerdict.Recommend(Game.Quality, _results, _measured, out _);
            Console.WriteLine($"[bench] recommends {recommended} (ran at {Game.Quality}){(complete ? "" : $", stopped after {_measured} of {SEGMENTS.Length} scenes: not saved")}");
            if (complete) Game.SaveBenchmark(new JsonObject
            {
                ["when"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                ["budgetMs"] = Math.Round(Budget, 3),
                ["recommended"] = recommended.ToString(),
                ["system"] = system,
                ["segments"] = segments,
                ["physics"] = physics,
            });
            Game.EndBenchmark();
            Rebuild();
        }

        //The page's content changed while it stands: built again and put up, the focus on its first entry (Run again)
        private void Rebuild()
        {
            InvalidateTree();
            Game.RebuildPage(this, () => _firstEntry);
        }

        /// <summary>The skip key stops a running benchmark: what was measured so far is shown, not saved.</summary>
        internal override void SkipPressed()
        {
            if (!Running) return;
            //A press the tour refuses (its opening moments' lockout) is no press at all: stopping the run under a tour that
            //flies on would put the player's scene back behind the old scene's shots, and the next run would find it engaged
            BackdropScreen backdrop = Manager?.Find<BackdropScreen>();
            if (backdrop != null && backdrop.TourRunning && !backdrop.TrySkipTour()) return;
            DisposePhysics();
            Console.WriteLine("[bench] stopped");
            if (_measured > 0) Finish(complete: false);
            else
            {
                _phase = Phase.Idle;
                Game.EndBenchmark();
                Rebuild();
            }
        }

        internal override void DrawAside() => _skipHint.DrawSkipHintAlone(Game.PromptsForPad);
    }
}
