using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using Prazsky.Core.Render;
using System;
using System.Diagnostics;
using System.Globalization;
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
    /// nothing is changed and nothing is sent. The skip key stops it.
    /// </summary>
    internal sealed class BenchmarkPage : MenuPage
    {
        //The dearest scenes on the desktop's High (docs/game-shell/quality-measurements.md): the cavern's chamber, the neon
        //city's towers, the forest's wood and the meadow's trees under the volume clouds
        private static readonly SceneKind[] SEGMENTS = { SceneKind.Cavern, SceneKind.NeonCity, SceneKind.Forest, SceneKind.Meadow };
        private const float SETTLE_SECONDS = 1.5f;
        private const int SEED = 988;

        private enum Phase { Idle, Settling, Touring, Done }

        private Phase _phase = Phase.Idle;
        private int _segment;
        private float _phaseSeconds;
        private long _lastTick;
        private readonly FrameStats _frames = new(), _settle = new(512);
        private readonly FrameStats.Summary[] _results = new FrameStats.Summary[SEGMENTS.Length];
        private readonly FrameStats.Summary[] _settles = new FrameStats.Summary[SEGMENTS.Length];
        private int _measured;
        private readonly PlayHud _skipHint;
        private Button _firstEntry;

        public BenchmarkPage(BS3DGame game) : base(game) => _skipHint = new PlayHud(game);

        private bool Running => _phase is Phase.Settling or Phase.Touring;

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
                    $"Flies through {SEGMENTS.Length} of the most demanding scenes at your quality ({Game.QualityLabel}), with the frame rate unlocked, and measures every frame. About a minute. The skip key stops it."),
                    ProseMeasure));
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

            //What the numbers recommend (BenchmarkVerdict), offered as an entry and never applied unasked
            QualityLevel tier = Game.Quality;
            QualityLevel recommended = BenchmarkVerdict.Recommend(tier, _results, _measured, out int decidedBy);
            int hz = Game.DisplayRefreshHz > 0 ? Game.DisplayRefreshHz : 60;
            string verdict = recommended < tier
                ? string.Create(CultureInfo.InvariantCulture,
                    $"{tier} drops frames in {SceneRenderer.SceneName(SEGMENTS[decidedBy])} ({_results[decidedBy].OverBudgetShare * 100f:0} % over {hz} Hz). {recommended} should hold it.")
                : recommended > tier
                    ? $"{tier} holds {hz} Hz in every scene with room to spare. {recommended} may hold it too."
                    : $"{tier} holds your display's {hz} Hz.";
            column.Widgets.Add(Paragraph(verdict, ProseMeasure));
            if (recommended != tier && Game.CanOfferQuality)
                column.Widgets.Add(MenuButton(recommended > tier ? $"Try {recommended}" : $"Use {recommended}", () => TakeOffer(recommended)));

            JsonObject system = Game.BenchmarkSystem();
            column.Widgets.Add(new Label
            {
                Text = $"{system["gpu"]} · {system["backBuffer"]} · {system["quality"]} · saved as Benchmark.json",
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 20, 0, 20),
            });
        }

        //The offer taken: the tier on, and the page ready to run again on it
        private void TakeOffer(QualityLevel tier)
        {
            Game.ChooseQuality(tier);
            _phase = Phase.Idle;
            Rebuild();
        }

        //A frame's budget: one frame of the display's refresh, or 60 Hz's where the display does not say
        private float Budget => 1000f / (Game.DisplayRefreshHz > 0 ? Game.DisplayRefreshHz : 60);

        /// <summary>Starts at once, without the press (the <c>benchmark</c> argument's, #988).</summary>
        internal void StartNow() => Start();

        private void Start()
        {
            if (Running) return;
            _segment = 0;
            _measured = 0;
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
            _lastTick = Stopwatch.GetTimestamp();
            Console.WriteLine($"[bench] segment {_segment + 1}/{SEGMENTS.Length} {SceneRenderer.SceneName(SEGMENTS[_segment])}");
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            BackdropScreen backdrop = Manager?.Find<BackdropScreen>();
            _skipHint.UpdateSkipHint((float)gameTime.ElapsedGameTime.TotalSeconds, Running, chapterIntro: true,
                Running ? _phaseSeconds : 0f, 0f);
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

            _frames.Add(milliseconds);
            if (backdrop.TourRunning || _phaseSeconds < 0.5f) return;

            //The tour is home: this segment's figures
            float budget = Budget;
            _results[_segment] = _frames.Summarize(budget);
            _settles[_segment] = _settle.Summarize(budget);
            _measured = _segment + 1;
            Log(_segment);

            if (++_segment < SEGMENTS.Length) BeginSegment();
            else Finish();
        }

        private void Log(int i)
        {
            FrameStats.Summary s = _results[i], first = _settles[i];
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"[bench] {SceneRenderer.SceneName(SEGMENTS[i])}: {s.Frames} frames, mean {s.Mean:0.00} ms, median {s.Median:0.00}, p95 {s.P95:0.00}, p99 {s.P99:0.00}, 1% low {s.OnePercentLowFps:0.0} FPS, worst {s.Worst:0.0} ms, {s.OverBudgetShare * 100f:0.0} % over {Budget:0.00} ms; first {SETTLE_SECONDS:0.0} s: worst {first.Worst:0.0} ms, median {first.Median:0.00}"));
        }

        private void Finish()
        {
            _phase = Phase.Done;
            JsonObject system = Game.BenchmarkSystem();
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
                });
            }
            QualityLevel recommended = BenchmarkVerdict.Recommend(Game.Quality, _results, _measured, out _);
            Console.WriteLine($"[bench] recommends {recommended} (ran at {Game.Quality})");
            Game.SaveBenchmark(new JsonObject
            {
                ["when"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                ["budgetMs"] = Math.Round(Budget, 3),
                ["recommended"] = recommended.ToString(),
                ["system"] = system,
                ["segments"] = segments,
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

        /// <summary>The skip key stops a running benchmark: what was measured so far is kept and shown.</summary>
        internal override void SkipPressed()
        {
            if (!Running) return;
            Manager?.Find<BackdropScreen>()?.TrySkipTour();
            Console.WriteLine("[bench] stopped");
            if (_measured > 0) Finish();
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
