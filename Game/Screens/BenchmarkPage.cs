using BS3D.Online;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    /// each of the dearest scenes (<see cref="SEGMENTS"/>), at the player's own tier with the frame left free of the display's
    /// refresh (<see cref="BS3DGame.BeginBenchmark"/>), each tour on a fixed seed so two runs of one machine fly one path, and
    /// measures every frame. A segment's first <see cref="SETTLE_SECONDS"/> are the scene's first-use cost (shaders, a hall
    /// built off the frame) and are reported apart. The result is shown here, written to the log as <c>[bench]</c> lines and
    /// to <c>Benchmark.json</c> in the player's profile with the machine it ran on (<see cref="BS3DGame.BenchmarkSystem"/>), and
    /// sent to the author (<see cref="OnlineBenchmarks"/>, the owner's ask of 2026-10-25), which the page says before it starts:
    /// the Start press is the consent, as a note's Send is. Nothing is changed unasked. The skip key stops it, and a stopped run
    /// is neither saved nor sent. After the tours it plays the city's biggest level (#1018): the chapter's opening, which ends
    /// at the gun, and then the gun firing at the cluster by itself under a lens of its own (<c>GameplayScreen.Benchmark</c>),
    /// measured as the tours are. Then the CPU's physics is timed on its own
    /// (<see cref="PhysicsBench"/>), the largest shipped cluster stepped off screen. While it runs, a panel in the corner shows
    /// the part being measured, the frame's time and the last frames against the display's budget (<see cref="FrameTrace"/>).
    /// </summary>
    internal sealed class BenchmarkPage : MenuPage
    {
        //What a segment measures: a chapter's tour over the front end, or the benchmark's play (#1018): the city's opening
        //flown into the gun, and the gun firing at the cluster by itself
        private enum Part { Tour, Intro, Action }

        /// <summary>One segment: its name on the page, its title on the panel, its scene and what it measures.</summary>
        private readonly record struct Segment(string Name, string Title, SceneKind Scene, Part Part);

        //The dearest scenes on the desktop's High (docs/game-shell/quality-measurements.md): the cavern's chamber, the neon
        //city's towers, the forest's wood and the meadow's trees under the volume clouds; then the city's biggest level played
        //(#1018), the gun, the shots and the falls the tours never draw
        private static readonly Segment[] SEGMENTS =
        {
            new("Cavern", "CAVERN", SceneKind.Cavern, Part.Tour),
            new("Neon City", "NEON CITY", SceneKind.NeonCity, Part.Tour),
            new("Forest", "FOREST", SceneKind.Forest, Part.Tour),
            new("Meadow", "MEADOW", SceneKind.Meadow, Part.Tour),
            new("City intro", "CITY INTRO", SceneKind.City, Part.Intro),
            new("City in action", "IN ACTION", SceneKind.City, Part.Action),
        };

        //How many of them are tours, which come first
        private static readonly int TOURS = Array.FindAll(SEGMENTS, s => s.Part == Part.Tour).Length;
        private const float SETTLE_SECONDS = 1.5f;
        private const int SEED = 988;

        private enum Phase { Idle, Settling, Touring, Play, Swarm, Physics, Done }

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

        //The finale (BenchmarkSwarm): a storm of the game's balls grown until the frame drops under 60 FPS, in a scene whose own
        //cost is small and fixed, so what grows is the balls'. Its figures are kept once it ends, and the storm let go
        private static readonly SceneKind SWARM_SCENE = SceneKind.Space;
        private BenchmarkSwarm _swarm;
        private bool _swarmMeasured, _swarmCapped;
        private int _swarmHeld, _swarmBrokeAt, _swarmSteps;
        private float _swarmHeldMedian, _swarmBrokenMedian;

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

        //The finished result's way to the author: its id once handed to the sender, and how it fared once the sender knows
        //(null while it is on its way); the label that says so is changed in place, so the arrival moves no focus
        private Guid _resultId;
        private BenchmarkDelivery? _delivery;
        private Label _deliveryLabel;

        //The live panel beside the flight: the part being measured, a readout of its time (the mean of the last
        //READOUT_SECONDS) and the last frames against the budget, so a run is worth watching and a hitch is seen where it
        //happens. Its strings are made once a part and the readout's in reused builders, so a frame allocates nothing
        private const float READOUT_SECONDS = 0.25f;
        private const int PANEL_MARGIN = 64, PANEL_WIDTH = 760, PANEL_PAD = 28, PANEL_GAP = 10, GRAPH_HEIGHT = 150, PROGRESS_HEIGHT = 8;
        private const int TITLE_FONT = 60, READOUT_FONT = 92, CAPTION_FONT = 32, BUDGET_LINE = 3;
        private readonly FrameTrace _trace = new();
        private readonly StringBuilder _readout = new(16), _readoutUnit = new(32);
        private float _readoutSum, _readoutSeconds;
        private int _readoutFrames;
        private string _partTitle = "", _partStep = "", _partBudgetCaption = "";
        private float _partBudget;
        private SpriteFontBase _titleFont, _readoutFont, _captionFont;
        private int _laidOutWidth, _laidOutHeight;

        private readonly PlayHud _skipHint;
        private Button _firstEntry;

        public BenchmarkPage(BS3DGame game) : base(game) => _skipHint = new PlayHud(game);

        private bool Running => _phase is Phase.Settling or Phase.Touring or Phase.Play or Phase.Swarm or Phase.Physics;

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
                    $"Flies through {TOURS} of the most demanding scenes at your quality ({Game.QualityLabel}), with the frame rate unlocked, then plays the biggest level: the city's opening flight to the cannon, and the cannon firing by itself. Then a storm of balls grows until your computer can no longer draw it at 60 FPS. It measures every frame, then times the physics. About two and a half minutes. The skip key stops it."),
                    ProseMeasure));
                if (!Game.SceneryShown && !BS3DGame.PotatoPath)
                    column.Widgets.Add(Paragraph("The scenery is off in your settings: the benchmark shows it, so that it has something to measure.", ProseMeasure));
                column.Widgets.Add(Paragraph(SENT_NOTE, ProseMeasure));
            }

            //Read as the tree is built, and the tree built again whenever it changes (Refresh): a level kept for Continue can be
            //left after the page was first seen, and a run started over it closes it
            _builtWithKeptSession = Game.HasKeptSession;
            if (_builtWithKeptSession)
                column.Widgets.Add(Paragraph("The benchmark plays a level of its own, so the level you left unfinished will be closed.", ProseMeasure));

            _firstEntry = MenuButton(_phase == Phase.Done ? "Run again" : "Start", () => StartNow());
            column.Widgets.Add(_firstEntry);
            column.Widgets.Add(BackButton());
            return ScreenRoot(Plate(column));
        }

        //Whether the tree was built with a level kept for Continue, which it warns of
        private bool _builtWithKeptSession;

        /// <summary>The page is built again when a level has been kept or let go since its tree was built: the warning follows it.</summary>
        internal override void Refresh()
        {
            if (!Running && _builtWithKeptSession != Game.HasKeptSession) InvalidateTree();
        }

        /// <summary>What the page says before a run about what leaves the machine: the Start press is the consent to it.</summary>
        internal const string SENT_NOTE = "When it finishes, the result and your computer's specification (graphics card, processor, memory, "
            + "system and screen) go to the author, to see how the game runs on computers like yours. Nothing that names you goes with them.";

        //How the result fared, in the page's words
        private string DeliveryText() => _delivery switch
        {
            null => "Sending the result to the author…",
            BenchmarkDelivery.Sent => "Sent to the author with your computer's specification. Thank you!",
            BenchmarkDelivery.Kept => "Not sent yet: the author's server did not answer. It goes the next time the game starts.",
            BenchmarkDelivery.Refused => "The author's server did not take the result.",
            _ => "Not sent: this build of the game sends results nowhere.",
        };

        private void AddResults(VerticalStackPanel column)
        {
            float budget = Budget;
            for (int i = 0; i < _measured; i++)
            {
                FrameStats.Summary s = _results[i];
                if (s.Frames == 0) continue;
                string line = string.Create(CultureInfo.InvariantCulture,
                    $"{SEGMENTS[i].Name}: {s.MedianFps:0} FPS · 1 % low {s.OnePercentLowFps:0} · {s.OverBudgetShare * 100f:0} % over {budget:0.0} ms");
                column.Widgets.Add(new Label
                {
                    Text = line,
                    Font = FontBody,
                    TextColor = BS3DGame.MENU_TEXT,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = ScaledThickness(0, 6, 0, 6),
                });
            }

            //The swarm: how many of the game's balls this machine draws at 60 FPS, a figure two machines compare by
            if (_swarmMeasured)
                column.Widgets.Add(new Label
                {
                    Text = _swarmCapped
                        ? string.Create(CultureInfo.InvariantCulture, $"Swarm: more than {_swarmHeld:N0} balls at 60 FPS (the most it asks)")
                        : _swarmHeld > 0
                            ? string.Create(CultureInfo.InvariantCulture, $"Swarm: {_swarmHeld:N0} balls at 60 FPS")
                            : string.Create(CultureInfo.InvariantCulture, $"Swarm: fewer than {BenchmarkSwarm.START_BALLS:N0} balls at 60 FPS"),
                    Font = FontBody,
                    TextColor = BS3DGame.MENU_TEXT,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = ScaledThickness(0, 6, 0, 6),
                });

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
                    column.Widgets.Add(Paragraph($"{SEGMENTS[i].Name} drew {ProbeFault(_probes[i])}: your graphics driver may not draw this game right. Please tell the author: Extras > {BS3DGame.NOTE_ENTRY}.", ProseMeasure));

            //What the numbers recommend (BenchmarkVerdict) for the tier they were measured at, offered as an entry and never
            //applied unasked; a stopped run says how far it got and offers nothing
            QualityLevel tier = _measuredTier;
            //Over the tours alone: the play's frames carry the physics, the shots and the falls, which no tier changes, and a
            //machine short of processor would be told to lower a graphics tier that would not help it (the physics' own line
            //says when the processor is the limit)
            QualityLevel recommended = BenchmarkVerdict.Recommend(tier, _results, Math.Min(_measured, TOURS), out int decidedBy);
            int hz = Game.DisplayRefreshHz > 0 ? Game.DisplayRefreshHz : 60;
            string verdict = recommended < tier
                ? string.Create(CultureInfo.InvariantCulture,
                    $"{tier} drops frames in {SEGMENTS[decidedBy].Name} ({_results[decidedBy].OverBudgetShare * 100f:0} % over {hz} Hz). {recommended} should hold it.")
                : recommended > tier
                    ? $"{tier} holds {hz} Hz in every scene with room to spare. {recommended} may hold it too."
                    : $"{tier} holds your display's {hz} Hz.";
            if (!_complete)
                verdict = string.Create(CultureInfo.InvariantCulture, $"Stopped after {_measured} of {SEGMENTS.Length} parts. ") + verdict;
            if (Game.Quality != tier) verdict += $" Measured at {tier}; your quality is {Game.Quality} now.";
            column.Widgets.Add(Paragraph(verdict, ProseMeasure));
            if (_complete && recommended != tier && recommended != Game.Quality && Game.CanOfferQuality)
                column.Widgets.Add(MenuButton(recommended > tier ? $"Try {recommended}" : $"Use {recommended}", () => TakeOffer(recommended)));

            _deliveryLabel = null;
            if (_complete && _resultId != Guid.Empty)
            {
                _deliveryLabel = Paragraph(DeliveryText(), ProseMeasure);
                column.Widgets.Add(_deliveryLabel);
            }

            JsonObject system = _measuredSystem ?? Game.BenchmarkSystem();
            column.Widgets.Add(new Label
            {
                Text = $"{system["gpu"]} · {system["backBuffer"]} · {system["quality"]} · {(_complete ? "saved as Benchmark.json" : "not saved or sent")}",
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
        internal bool WantsProbe => _phase is Phase.Touring or Phase.Play && !_probed && _phaseSeconds >= PROBE_AT;

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
                $"[bench] {SEGMENTS[i].Name} frame {_probes[i]} (mean {_probeMean[i]:0.000}, spread {_probeSpread[i]:0.000}; HDR {_hdrRead[i]} texels, {_hdrNonFinite[i]} not finite, brightest {_hdrBrightest[i]:0.00})"));
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
        private int RefreshHz => Game.DisplayRefreshHz > 0 ? Game.DisplayRefreshHz : 60;

        private float Budget => 1000f / RefreshHz;

        /// <summary>
        /// Starts at once, without the press (the <c>benchmark</c> argument's, #988); with <paramref name="playOnly"/> at the
        /// play, the tours left out (<c>benchmarkplay</c>, #1018, testing only: the run is shown, never saved or sent).
        /// </summary>
        internal void StartNow(bool playOnly = false)
        {
            _playOnly = playOnly;
            Start();
        }

        //A run begun at the play by the benchmarkplay argument: complete or not, it is never saved or sent, since its tours are missing
        private bool _playOnly;

        private void Start()
        {
            if (Running) return;
            _segment = 0;
            _measured = 0;
            _swarmMeasured = false;
            _physicsMeasured = false;
            _complete = false;
            _resultId = Guid.Empty;
            _delivery = null;
            Array.Clear(_results);
            Game.BeginBenchmark();
            if (_playOnly)
            {
                _segment = TOURS;
                BeginPlay();
            }
            else BeginSegment();
        }

        private void BeginSegment()
        {
            Game.SetScene(SEGMENTS[_segment].Scene);
            BeginPart(SEGMENTS[_segment].Title, _segment + 1,
                string.Create(CultureInfo.InvariantCulture, $"{RefreshHz} Hz"), Budget);
            _phase = Phase.Settling;
            _phaseSeconds = 0f;
            _frames.Clear();
            _settle.Clear();
            _probed = false;
            _probes[_segment] = FrameProbe.Verdict.Ok;
            _hdrRead[_segment] = _hdrNonFinite[_segment] = 0;
            _hdrBrightest[_segment] = 0f;
            _lastTick = Stopwatch.GetTimestamp();
            Console.WriteLine($"[bench] segment {_segment + 1}/{SEGMENTS.Length} {SEGMENTS[_segment].Name}");
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            TakeDelivery();
            BackdropScreen backdrop = Manager?.Find<BackdropScreen>();
            _skipHint.UpdateSkipHint((float)gameTime.ElapsedGameTime.TotalSeconds, Running, chapterIntro: true,
                Running ? _phaseSeconds : 0f, 0f);
            if (_phase == Phase.Physics)
            {
                StepPhysics((float)gameTime.ElapsedGameTime.TotalSeconds);
                return;
            }
            if (!Running || backdrop == null) return;

            //The wall time since the last frame: what the frame cost, the limiter let go
            long now = Stopwatch.GetTimestamp();
            float milliseconds = (float)((now - _lastTick) * 1000.0 / Stopwatch.Frequency);
            _lastTick = now;
            _phaseSeconds += milliseconds / 1000f;

            if (_phase == Phase.Swarm)
            {
                StepSwarm(milliseconds);
                return;
            }

            if (_phase == Phase.Settling)
            {
                _settle.Add(milliseconds);
                Tally(milliseconds, milliseconds / 1000f, apart: true);
                if (_phaseSeconds < SETTLE_SECONDS) return;
                _phase = Phase.Touring;
                _phaseSeconds = 0f;
                backdrop.PlayTour(new Random(SEED + _segment));
                return;
            }

            if (_skipNext) _skipNext = false;
            else
            {
                _frames.Add(milliseconds);
                Tally(milliseconds, milliseconds / 1000f, apart: false);
            }
            if (backdrop.TourRunning || _phaseSeconds < 0.5f) return;

            //The tour is home: this segment's figures
            float budget = Budget;
            _results[_segment] = _frames.Summarize(budget);
            _settles[_segment] = _settle.Summarize(budget);
            _measured = _segment + 1;
            Log(_segment);

            if (++_segment < TOURS) BeginSegment();
            else BeginPlay();
        }

        //The benchmark's play (#1018): the city's biggest level built as a benchmark run and put on the stack; this page leaves
        //it, and the host ticks TickPlay above the stack until the play is over
        private void BeginPlay()
        {
            _phase = Phase.Play;
            BeginPlaySegment();
            Game.StartBenchmarkPlay();

            //The clock starts after the level's build, which blocks: its stall is no frame, and the first-use frames after it
            //are what the settle window is for
            _lastTick = Stopwatch.GetTimestamp();
            _phaseSeconds = 0f;
        }

        private void BeginPlaySegment()
        {
            BeginPart(SEGMENTS[_segment].Title, _segment + 1,
                string.Create(CultureInfo.InvariantCulture, $"{RefreshHz} Hz"), Budget);
            _phaseSeconds = 0f;
            _frames.Clear();
            _settle.Clear();
            _probed = false;
            _probes[_segment] = FrameProbe.Verdict.Ok;
            _hdrRead[_segment] = _hdrNonFinite[_segment] = 0;
            _hdrBrightest[_segment] = 0f;
            _lastTick = Stopwatch.GetTimestamp();
            Console.WriteLine($"[bench] segment {_segment + 1}/{SEGMENTS.Length} {SEGMENTS[_segment].Name}");
        }

        /// <summary>
        /// The benchmark's play, measured from the host's frame while the level is on the stack (#1018): the intro's frames (its
        /// first <see cref="SETTLE_SECONDS"/> apart, the level's build and first draws), then the action's from the moment the
        /// gun is handed over, each summed as a tour's when it ends; when the play is over the session goes and this page comes
        /// back for the physics.
        /// </summary>
        internal void TickPlay()
        {
            if (_phase != Phase.Play) return;

            long now = Stopwatch.GetTimestamp();
            float milliseconds = (float)((now - _lastTick) * 1000.0 / Stopwatch.Frequency);
            _lastTick = now;
            _phaseSeconds += milliseconds / 1000f;
            _skipHint.UpdateSkipHint(milliseconds / 1000f, true, chapterIntro: true, _phaseSeconds, 0f);

            GameplayScreen.BenchmarkStage stage = Game.BenchmarkStage;
            Part part = SEGMENTS[_segment].Part;

            //The intro handed the gun over: its figures, and the action's begin
            if (part == Part.Intro && stage != GameplayScreen.BenchmarkStage.Intro)
            {
                EndPlaySegment();
                if (++_segment < SEGMENTS.Length && stage == GameplayScreen.BenchmarkStage.Action) BeginPlaySegment();
                else
                {
                    LeavePlay();
                    return;
                }
                part = SEGMENTS[_segment].Part;
            }

            if (stage == GameplayScreen.BenchmarkStage.Done || stage == GameplayScreen.BenchmarkStage.None)
            {
                if (part == Part.Action && _frames.Count > 0) EndPlaySegment();
                LeavePlay();
                return;
            }

            if (part == Part.Intro && _phaseSeconds < SETTLE_SECONDS)
            {
                _settle.Add(milliseconds);
                Tally(milliseconds, milliseconds / 1000f, apart: true);
            }
            else if (_skipNext) _skipNext = false;
            else
            {
                _frames.Add(milliseconds);
                Tally(milliseconds, milliseconds / 1000f, apart: false);
            }
        }

        //A play segment's figures, as a tour's
        private void EndPlaySegment()
        {
            float budget = Budget;
            _results[_segment] = _frames.Summarize(budget);
            _settles[_segment] = _settle.Summarize(budget);
            _measured = _segment + 1;
            Log(_segment);
        }

        //The play is over: the session goes, this page comes back over the main menu, and the swarm runs
        private void LeavePlay()
        {
            Game.EndBenchmarkPlay();
            BeginSwarm();
        }

        //The finale: the storm's places worked out, its scene put up and the front end's camera held on its wide turn, so the
        //run's every frame looks at the storm from outside it; the ramp starts once the scene's first frames have gone
        private void BeginSwarm()
        {
            _phase = Phase.Swarm;
            Game.SetScene(SWARM_SCENE);
            _swarm = new BenchmarkSwarm();
            _swarm.Prepare();
            Game.BenchmarkSwarm = _swarm;
            Game.Backdrop?.HoldOnWideTurn(true);
            BeginPart("SWARM", SEGMENTS.Length + 1, "60 FPS", BenchmarkSwarm.BUDGET_MILLISECONDS);
            _phaseSeconds = 0f;
            _lastTick = Stopwatch.GetTimestamp();
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"[bench] swarm in {SceneRenderer.SceneName(SWARM_SCENE)}: from {BenchmarkSwarm.START_BALLS} balls, x{BenchmarkSwarm.GROWTH} every {BenchmarkSwarm.STEP_SECONDS} s while the median frame holds {BenchmarkSwarm.BUDGET_MILLISECONDS:0.00} ms"));
        }

        //One frame of the finale: the scene's first frames apart, then the ramp; when it is over its figures are kept and
        //the physics runs
        private void StepSwarm(float milliseconds)
        {
            if (_phaseSeconds < SETTLE_SECONDS)
            {
                Tally(milliseconds, milliseconds / 1000f, apart: true);
                return;
            }

            Tally(milliseconds, milliseconds / 1000f, apart: false);
            if (!_swarm.Tick(milliseconds) || !_swarm.Done) return;

            _swarmMeasured = true;
            _swarmHeld = _swarm.Held;
            _swarmHeldMedian = _swarm.HeldMedian;
            _swarmBrokeAt = _swarm.Capped ? 0 : _swarm.Balls;
            _swarmBrokenMedian = _swarm.BrokenMedian;
            _swarmSteps = _swarm.Steps;
            _swarmCapped = _swarm.Capped;
            ReleaseSwarm();
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"[bench] swarm: held {_swarmHeld} balls (median {_swarmHeldMedian:0.00} ms)")
                + (_swarmCapped ? ", the most it asks"
                    : string.Create(CultureInfo.InvariantCulture, $", broke at {_swarmBrokeAt} ({_swarmBrokenMedian:0.00} ms)"))
                + string.Create(CultureInfo.InvariantCulture, $" after {_swarmSteps} steps"));
            BeginPhysics();
        }

        //The storm let go: out of the frame, its places dropped, and the front end's camera free to fly in again
        private void ReleaseSwarm()
        {
            if (_swarm == null) return;

            _swarm = null;
            Game.BenchmarkSwarm = null;
            Game.Backdrop?.HoldOnWideTurn(false);
        }

        /// <summary>
        /// A key or a button pressed while the benchmark's level plays: the benchmark stops, as the skip key stops a tour,
        /// keeping what was measured (the part in progress is left out).
        /// </summary>
        internal void StopFromPlay()
        {
            if (_phase != Phase.Play) return;

            Console.WriteLine("[bench] stopped");
            Game.EndBenchmarkPlay();
            if (_measured > 0) Finish(complete: false);
            else
            {
                _phase = Phase.Idle;
                Game.EndBenchmark();
                Rebuild();
            }
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
                BeginPart("PHYSICS", SEGMENTS.Length + 2, "real time", PhysicsBench.STEP_BUDGET_MILLISECONDS);
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

        private void StepPhysics(float seconds)
        {
            _physics.Run(PHYSICS_SLICE_MILLISECONDS);
            Tally(_physics.SliceMean, seconds, apart: false);
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
                $"[bench] {SEGMENTS[i].Name}: {s.Frames} frames, mean {s.Mean:0.00} ms, median {s.Median:0.00}, p95 {s.P95:0.00}, p99 {s.P99:0.00}, 1% low {s.OnePercentLowFps:0.0} FPS, worst {s.Worst:0.0} ms, {s.OverBudgetShare * 100f:0.0} % over {Budget:0.00} ms; first {SETTLE_SECONDS:0.0} s: worst {first.Worst:0.0} ms, median {first.Median:0.00}"));
        }

        //The run's end: what it measured kept with the tier and the machine it measured them on, and saved only when it went
        //to its end, so a run stopped after one scene never overwrites a whole one
        private void Finish(bool complete)
        {
            ReleaseSwarm();
            _phase = Phase.Done;
            //A run begun at the play (benchmarkplay) lacks its tours: shown, never saved or sent
            complete &= !_playOnly;
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
                    ["scene"] = SceneRenderer.SceneName(SEGMENTS[i].Scene),
                    ["part"] = SEGMENTS[i].Part switch { Part.Intro => "intro", Part.Action => "action", _ => "tour" },
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
            JsonObject swarm = null;
            if (_swarmMeasured)
                swarm = new JsonObject
                {
                    ["scene"] = SceneRenderer.SceneName(SWARM_SCENE),
                    ["budgetMs"] = Math.Round(BenchmarkSwarm.BUDGET_MILLISECONDS, 3),
                    ["startBalls"] = BenchmarkSwarm.START_BALLS,
                    ["growth"] = BenchmarkSwarm.GROWTH,
                    ["stepSeconds"] = BenchmarkSwarm.STEP_SECONDS,
                    ["held"] = _swarmHeld,
                    ["heldMedianMs"] = Math.Round(_swarmHeldMedian, 3),
                    ["brokeAt"] = _swarmBrokeAt,
                    ["brokenMedianMs"] = Math.Round(_swarmBrokenMedian, 3),
                    ["steps"] = _swarmSteps,
                    ["capped"] = _swarmCapped,
                };
            QualityLevel recommended = BenchmarkVerdict.Recommend(Game.Quality, _results, Math.Min(_measured, TOURS), out _);
            Console.WriteLine($"[bench] recommends {recommended} (ran at {Game.Quality}){(complete ? "" : $", stopped after {_measured} of {SEGMENTS.Length} parts: not saved or sent")}");
            if (complete)
            {
                JsonObject result = new()
                {
                    ["when"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    ["budgetMs"] = Math.Round(Budget, 3),
                    ["recommended"] = recommended.ToString(),
                    ["system"] = system,
                    ["segments"] = segments,
                    ["swarm"] = swarm,
                    ["physics"] = physics,
                };
                Game.SaveBenchmark(result);
                //To the author, as the page said before the run (the consent): written to the sender's outbox first, so a
                //result the server does not answer for goes at the next start
                _resultId = Game.BenchmarkResults?.Send(result) ?? Guid.Empty;
                _delivery = null;
            }
            Game.EndBenchmark();
            Rebuild();
        }

        //The sender's word on the finished result, said in place on the page whenever it arrives (also on a later visit)
        private void TakeDelivery()
        {
            if (_resultId == Guid.Empty || _delivery != null || Game.BenchmarkResults is not { } results) return;
            if (!results.TryTakeOutcome(out BenchmarkOutcome outcome) || outcome.BenchmarkId != _resultId) return;
            _delivery = outcome.Delivery;
            if (_deliveryLabel != null) _deliveryLabel.Text = DeliveryText();
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
            ReleaseSwarm();
            Console.WriteLine("[bench] stopped");
            if (_measured > 0) Finish(complete: false);
            else
            {
                _phase = Phase.Idle;
                Game.EndBenchmark();
                Rebuild();
            }
        }

        //A part of the run begins: the panel's title, its place in the run (the physics is the last part), what its budget's
        //line stands for, and an empty graph
        private void BeginPart(string title, int part, string budgetCaption, float budget)
        {
            _partTitle = title;
            _partStep = string.Create(CultureInfo.InvariantCulture, $"{part} / {SEGMENTS.Length + 2}");
            _partBudgetCaption = budgetCaption;
            _partBudget = budget;
            _trace.Clear();
            _readout.Clear();
            _readoutUnit.Clear();
            _readoutSum = _readoutSeconds = 0f;
            _readoutFrames = 0;
        }

        //One frame (or one physics slice's mean step) into the graph, and the readout renewed every READOUT_SECONDS
        private void Tally(float milliseconds, float seconds, bool apart)
        {
            _trace.Add(milliseconds, apart);
            _readoutSum += milliseconds;
            _readoutFrames++;
            _readoutSeconds += seconds;
            if (_readoutSeconds < READOUT_SECONDS && _readout.Length > 0) return;

            float mean = _readoutSum / _readoutFrames;
            bool physics = _phase == Phase.Physics;
            _readout.Clear();
            if (physics) _readout.Append(CultureInfo.InvariantCulture, $"{mean:0.00} ms");
            else _readout.Append(CultureInfo.InvariantCulture, $"{mean:0.0} ms");
            _readoutUnit.Clear();
            if (physics) _readoutUnit.Append("a step, ").Append(_physicsBalls).Append(" balls");
            else if (apart) _readoutUnit.Append("warming up");
            else if (_phase == Phase.Swarm && _swarm != null && mean > 0f)
                _readoutUnit.Append(_swarm.Balls).Append(CultureInfo.InvariantCulture, $" balls, {1000f / mean:0} FPS");
            else if (mean > 0f) _readoutUnit.Append(CultureInfo.InvariantCulture, $"{1000f / mean:0} FPS");
            _readoutSum = _readoutSeconds = 0f;
            _readoutFrames = 0;
        }

        internal override void DrawAside()
        {
            if (Running && _partTitle.Length > 0) DrawPanel();
            _skipHint.DrawSkipHintAlone(Game.PromptsForPad);
        }

        //The panel in the top right corner (the FPS line owns the top left, and is on by default): the part and its place in
        //the run, the readout, then the graph, a bar a frame with the newest on the right and the budget's line across its
        //middle; first-use frames dim, frames over the budget in the alert colour, the rest in the note colour; under the
        //physics, how far through its steps it is
        private void DrawPanel()
        {
            Viewport viewport = Game.GraphicsDevice.Viewport;
            if (viewport.Width != _laidOutWidth || viewport.Height != _laidOutHeight)
            {
                _laidOutWidth = viewport.Width;
                _laidOutHeight = viewport.Height;
                _titleFont = Game.DisplayFontAt(Scaled(TITLE_FONT));
                _readoutFont = Game.DisplayFontAt(Scaled(READOUT_FONT));
                _captionFont = Game.ReadingFontAt(Scaled(CAPTION_FONT));
            }

            int margin = Scaled(PANEL_MARGIN), pad = Scaled(PANEL_PAD), gap = Scaled(PANEL_GAP);
            int width = Math.Min(Scaled(PANEL_WIDTH), viewport.Width - 2 * margin);
            int inner = width - 2 * pad;
            int panelLeft = viewport.Width - margin - width;
            int left = panelLeft + pad;
            int titleTop = margin + pad;
            int readoutTop = titleTop + _titleFont.LineHeight + gap;
            int graphTop = readoutTop + _readoutFont.LineHeight + gap;
            int graphHeight = Scaled(GRAPH_HEIGHT);
            bool physics = _phase == Phase.Physics, swarm = _phase == Phase.Swarm && _swarm != null;
            int bottom = graphTop + graphHeight + (physics || swarm ? gap + Scaled(PROGRESS_HEIGHT) : 0) + pad;

            SpriteBatch batch = Game.OverlayBatch;
            Texture2D texel = Game.Texel;
            batch.Begin();
            batch.Draw(texel, new Rectangle(panelLeft, margin, width, bottom - margin), BS3DGame.MENU_PLATE);

            batch.DrawString(_titleFont, _partTitle, new Vector2(left, titleTop), BS3DGame.MENU_TEXT);
            Vector2 stepSize = _captionFont.MeasureString(_partStep);
            batch.DrawString(_captionFont, _partStep,
                new Vector2(left + inner - MathF.Round(stepSize.X), titleTop + MathF.Round((_titleFont.LineHeight - stepSize.Y) * 0.5f)),
                BS3DGame.MENU_TEXT_DIM);

            if (_readout.Length > 0)
            {
                batch.DrawString(_readoutFont, _readout, new Vector2(left, readoutTop), BS3DGame.MENU_TEXT);
                float readoutWidth = _readoutFont.MeasureString(_readout).X;
                batch.DrawString(_captionFont, _readoutUnit,
                    new Vector2(left + MathF.Round(readoutWidth) + gap * 2, readoutTop + _readoutFont.LineHeight - _captionFont.LineHeight - gap),
                    BS3DGame.MENU_TEXT_BODY);
            }

            //The graph's ground, faint, so its extent reads before it fills; then the bars; then the budget's line over them
            batch.Draw(texel, new Rectangle(left, graphTop, inner, graphHeight), BS3DGame.MENU_TEXT * 0.05f);
            int count = _trace.Count;
            for (int i = 0; i < count; i++)
            {
                int slot = FrameTrace.CAPACITY - count + i;
                int x0 = left + (int)((long)inner * slot / FrameTrace.CAPACITY);
                int x1 = left + (int)((long)inner * (slot + 1) / FrameTrace.CAPACITY);
                int barWidth = Math.Max(1, x1 - x0 - (x1 - x0 >= 3 ? 1 : 0));
                float ms = _trace[i];
                int height = Math.Max(1, (int)MathF.Round(FrameTrace.Height(ms, _partBudget) * graphHeight));
                Color colour = _trace.IsApart(i) ? BS3DGame.MENU_TEXT_DIM
                    : FrameTrace.Over(ms, _partBudget) ? BS3DGame.MENU_TEXT_ALERT : BS3DGame.MENU_TEXT_NOTE;
                batch.Draw(texel, new Rectangle(x0, graphTop + graphHeight - height, barWidth, height), colour);
            }
            int line = Math.Max(1, Scaled(BUDGET_LINE));
            int lineY = graphTop + (int)MathF.Round(graphHeight * (1f - 1f / FrameTrace.HEADROOM)) - line / 2;
            batch.Draw(texel, new Rectangle(left, lineY, inner, line), PlayHud.HUD_ACCENT);
            Vector2 captionSize = _captionFont.MeasureString(_partBudgetCaption);
            batch.DrawString(_captionFont, _partBudgetCaption,
                new Vector2(left + inner - MathF.Round(captionSize.X), lineY - MathF.Round(captionSize.Y) - line), PlayHud.HUD_ACCENT);

            //How far through its steps the physics is; how far the storm has grown towards the most it asks, on the ramp's own
            //(logarithmic) scale, so each step moves the bar alike
            float progress = physics && _physics != null ? _physics.Progress
                : swarm ? MathF.Log((float)_swarm.Balls / BenchmarkSwarm.START_BALLS) / MathF.Log((float)BenchmarkSwarm.MAX_BALLS / BenchmarkSwarm.START_BALLS)
                : -1f;
            if (progress >= 0f)
            {
                int progressTop = graphTop + graphHeight + gap, progressHeight = Scaled(PROGRESS_HEIGHT);
                batch.Draw(texel, new Rectangle(left, progressTop, inner, progressHeight), BS3DGame.MENU_TEXT * 0.12f);
                batch.Draw(texel, new Rectangle(left, progressTop, (int)MathF.Round(inner * Math.Clamp(progress, 0f, 1f)), progressHeight),
                    BS3DGame.MENU_TEXT_NOTE);
            }
            batch.End();
        }
    }
}
