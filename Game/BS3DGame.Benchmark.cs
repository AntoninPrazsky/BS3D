using BS3D.Platform;
using BS3D.Screens;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BS3D
{
    /// <summary>
    /// The benchmark's hold on the game (#988, Extras > Benchmark, <see cref="BenchmarkPage"/>): while it runs the frame is not
    /// held to the display's refresh, so what is measured is what a frame costs and not the limiter, and the adaptive quality
    /// probe does not step the tier under it; the scene it tours is put back when it ends. And the machine it ran on, in the
    /// words a result is read by.
    /// </summary>
    public partial class BS3DGame
    {
        private BenchmarkPage _benchmarkPage;
        private bool _benchmarking;
        private SceneKind _sceneBeforeBenchmark;
        private byte _domeBeforeBenchmark;

        /// <summary>Whether a benchmark is running: the limiter lets go and the quality probe waits.</summary>
        internal bool Benchmarking => _benchmarking;

        internal void OpenBenchmark() => OpenBenchmark(start: false);

        /// <summary>The page, and with <paramref name="start"/> the run at once (the <c>benchmark</c> argument's).</summary>
        internal void OpenBenchmark(bool start)
        {
            OpenPage(_benchmarkPage ??= new BenchmarkPage(this));
            if (start) _benchmarkPage.StartNow();
        }

        /// <summary>
        /// Lets go of the frame limit and the quality probe, shows the scenery whatever the Scenery row says
        /// (<see cref="SceneryShown"/>: with it off the tours draw no scene and the run would measure nothing), hangs the
        /// one preview cluster every run tours past (<see cref="PinnedPreviewLevel"/>), and remembers the scene and its dome to
        /// put them back in <see cref="EndBenchmark"/>.
        /// </summary>
        internal void BeginBenchmark()
        {
            if (_benchmarking) return;
            _benchmarking = true;
            _sceneBeforeBenchmark = _scene;
            _domeBeforeBenchmark = _skyDome;
            _screens?.Find<BackdropScreen>()?.RollPreviewMap();
            Console.WriteLine("[bench] begin");
        }

        /// <summary>
        /// The frame limit, the probe and the Scenery row back as they were, the scene the player had under the dome it had
        /// (a toured scene that brings a dome of its own, the forest's, would otherwise stay over it), and a preview of the
        /// player's own roll again.
        /// </summary>
        internal void EndBenchmark()
        {
            if (!_benchmarking) return;
            _benchmarking = false;
            SetScene(_sceneBeforeBenchmark);
            SetSkyDome(_domeBeforeBenchmark);
            _screens?.Find<BackdropScreen>()?.RollPreviewMap();
            Console.WriteLine("[bench] end");
        }

        /// <summary>Whether the player may be offered another tier: not on a locked or a Potato run, whose renderer cannot change while it runs.</summary>
        internal bool CanOfferQuality => !IsQualityLocked && !PotatoPath;

        /// <summary>
        /// Puts <paramref name="tier"/> on, as the Quality row's press does (<see cref="CycleQuality"/>): the tier's own look,
        /// pinned by the player, stored, and the Auto quality row off. The benchmark's offer (#988), taken by a press.
        /// </summary>
        internal void ChooseQuality(QualityLevel tier)
        {
            if (!CanOfferQuality || tier == QualityLevel.Potato) return;
            _settings.Scenery = null;
            ApplyQuality(tier);
            _qualityPinnedByPlayer = true;
            _qualitySettled = true;
            _settings.Quality = _quality;
            _settings.AdaptiveQuality = false;
            SaveSettings();
            _mainMenuPage.ClearQualityNotice();
            _settingsPage.Refresh();
            Console.WriteLine($"[bench] quality {tier} chosen");
        }

        //The frame the probe reads back into, kept for the back buffer's size: four reads a run, never a frame's allocation
        private Microsoft.Xna.Framework.Color[] _benchmarkFrame;

        //And the HDR scene target's patches (FrameProbe.CountNonFinite): HDR_PATCHES squares of HDR_PATCH texels, the middle
        //and the four quarters' middles of the target at whatever size the supersample made it, copied side by side into
        //_benchmarkHdrTarget before the resolve (CopyBenchmarkHdr) and read back with the frame
        private const int HDR_PATCH = 64;
        private static readonly Microsoft.Xna.Framework.Vector2[] HDR_PATCHES =
            { new(0.5f, 0.5f), new(0.25f, 0.25f), new(0.75f, 0.25f), new(0.25f, 0.75f), new(0.75f, 0.75f) };
        private readonly Microsoft.Xna.Framework.Graphics.PackedVector.HalfVector4[] _benchmarkHdr =
            new Microsoft.Xna.Framework.Graphics.PackedVector.HalfVector4[HDR_PATCH * HDR_PATCH * 5];
        private RenderTarget2D _benchmarkHdrTarget;
        private SpriteBatch _benchmarkHdrBatch;
        private bool _benchmarkHdrCopied;

        /// <summary>
        /// The benchmark's correctness probe (#988, <see cref="FrameProbe"/>): when the page asks, the finished frame is read
        /// back at the end of Draw, as a note's picture is, and handed to it with the HDR patches this frame copied. The read
        /// stalls the GPU, which is why the page asks once a segment and leaves the frame after it out of its figures.
        /// </summary>
        private void ServiceBenchmarkProbe()
        {
            if (!_benchmarking || _benchmarkPage == null || !_benchmarkPage.WantsProbe) return;
            int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int height = GraphicsDevice.PresentationParameters.BackBufferHeight;
            int hdr = ReadBenchmarkHdr();
            try
            {
                if (_benchmarkFrame == null || _benchmarkFrame.Length != width * height)
                    _benchmarkFrame = new Microsoft.Xna.Framework.Color[width * height];
                BackBufferPixels.Read(GraphicsDevice, _benchmarkFrame);
                _benchmarkPage.TakeProbe(_benchmarkFrame, width, height, _benchmarkHdr, hdr);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[bench] the frame could not be read back: {exception.GetType().Name}: {exception.Message}");
                _benchmarkPage.TakeProbe(null, width, height, _benchmarkHdr, hdr);
            }
        }

        /// <summary>
        /// Before the resolve, on a frame the probe will read: the HDR scene's patches drawn texel for texel (point sampled,
        /// opaque, so a NaN or an infinity arrives as it is) into a small target MonoGame can read back. The scene target itself
        /// cannot be: its <see cref="SurfaceFormat.HdrBlendable"/> is a format <c>Texture2D.GetData</c>'s size check refuses
        /// ("Value does not fall within the expected range", <c>GraphicsExtensions.GetSize</c>), and its
        /// <see cref="SurfaceFormat.HalfVector4"/> twin is the same texels. Here and not at the end of Draw because binding a
        /// target and then the back buffer clears the back buffer (its usage is the default discard), and the resolve after
        /// this draws all of it anyway. No-op on the Potato path (no HDR target) and on any frame the probe does not want.
        /// </summary>
        private void CopyBenchmarkHdr()
        {
            _benchmarkHdrCopied = false;
            if (!_benchmarking || _benchmarkPage == null || !_benchmarkPage.WantsProbe || PotatoPath) return;
            if (_pipeline?.SceneTarget is not RenderTarget2D scene || Math.Min(scene.Width, scene.Height) < HDR_PATCH * 4) return;

            _benchmarkHdrTarget ??= new RenderTarget2D(GraphicsDevice, HDR_PATCH * HDR_PATCHES.Length, HDR_PATCH, false,
                SurfaceFormat.HalfVector4, DepthFormat.None);
            _benchmarkHdrBatch ??= new SpriteBatch(GraphicsDevice);

            BlendState blend = GraphicsDevice.BlendState;
            DepthStencilState depth = GraphicsDevice.DepthStencilState;
            RasterizerState rasterizer = GraphicsDevice.RasterizerState;
            SamplerState sampler = GraphicsDevice.SamplerStates[0];
            GraphicsDevice.SetRenderTarget(_benchmarkHdrTarget);
            _benchmarkHdrBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            for (int i = 0; i < HDR_PATCHES.Length; i++)
            {
                var source = new Microsoft.Xna.Framework.Rectangle((int)(HDR_PATCHES[i].X * scene.Width) - HDR_PATCH / 2,
                    (int)(HDR_PATCHES[i].Y * scene.Height) - HDR_PATCH / 2, HDR_PATCH, HDR_PATCH);
                _benchmarkHdrBatch.Draw(scene, new Microsoft.Xna.Framework.Rectangle(i * HDR_PATCH, 0, HDR_PATCH, HDR_PATCH), source,
                    Microsoft.Xna.Framework.Color.White);
            }
            _benchmarkHdrBatch.End();
            GraphicsDevice.BlendState = blend;
            GraphicsDevice.DepthStencilState = depth;
            GraphicsDevice.RasterizerState = rasterizer;
            GraphicsDevice.SamplerStates[0] = sampler;
            _benchmarkHdrCopied = true;
        }

        /// <summary>The patches <see cref="CopyBenchmarkHdr"/> drew this frame into <see cref="_benchmarkHdr"/>, and how many texels: 0 when none were.</summary>
        private int ReadBenchmarkHdr()
        {
            if (!_benchmarkHdrCopied) return 0;
            _benchmarkHdrCopied = false;
            try
            {
                _benchmarkHdrTarget.GetData(_benchmarkHdr);
                return _benchmarkHdr.Length;
            }
            catch (Exception exception)
            {
                //Its own failure, never the frame's: the back buffer is read and judged on its own
                Console.WriteLine($"[bench] the HDR scene could not be read back: {exception.GetType().Name}: {exception.Message}");
                return 0;
            }
        }

        /// <summary>The display's refresh, or 0 when the adapter reports nothing sensible: a frame's budget is a thousandth of it.</summary>
        internal int DisplayRefreshHz => _displayRefreshHz;

        /// <summary>
        /// The machine, as a result is read by (#988's system report): the build, the platform and the OS, the CPU and its
        /// threads, the graphics adapter with its vendor and device ids, the back buffer and the display's scale and refresh,
        /// windowed or full screen, the quality tier, its supersample and whether the Potato path draws. Nothing that names
        /// the player or the machine: no paths, no user name, no address, as a note's context.
        /// </summary>
        internal JsonObject BenchmarkSystem()
        {
            GraphicsAdapter adapter = GraphicsDevice.Adapter;
            JsonObject system = new()
            {
                ["build"] = BuildVersion.DisplayName,
                ["platform"] = RuntimeInformation.RuntimeIdentifier,
                ["os"] = RuntimeInformation.OSDescription,
                ["osArchitecture"] = RuntimeInformation.OSArchitecture.ToString(),
                ["processArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
                ["cpu"] = CpuName(),
                ["cpuThreads"] = Environment.ProcessorCount,
                ["gpu"] = adapter.Description,
                ["backBuffer"] = string.Create(CultureInfo.InvariantCulture,
                    $"{GraphicsDevice.PresentationParameters.BackBufferWidth}x{GraphicsDevice.PresentationParameters.BackBufferHeight}"),
                ["refreshHz"] = _displayRefreshHz,
                ["fullscreen"] = _graphics.IsFullScreen,
                ["quality"] = _quality.ToString(),
                ["supersample"] = _supersampleFactor,
                ["potato"] = PotatoPath,
            };
            //The adapter's vendor and device ids, where the platform's adapter has them: DirectX's does, DesktopGL's (the Pi's)
            //has neither property, so they are read by name rather than compiled against
            foreach (string id in new[] { "VendorId", "DeviceId" })
                if (adapter.GetType().GetProperty(id)?.GetValue(adapter) is int value)
                    system["gpu" + id] = value.ToString("X4", CultureInfo.InvariantCulture);

            //The display's scale (#825): a benchmark from a scaled laptop says whether the back buffer is the panel's own
            if (DisplayRefresh.TryGetScale(Window.Handle, out float scale, out bool aware))
            {
                system["scale"] = MathF.Round(scale, 2);
                system["dpiAware"] = aware;
            }
            return system;
        }

        /// <summary>The CPU's own name where the platform says it (the registry on Windows, /proc/cpuinfo on Linux), else empty.</summary>
        private static string CpuName()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                    return (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? string.Empty;
                }
                if (File.Exists("/proc/cpuinfo"))
                {
                    foreach (string line in File.ReadLines("/proc/cpuinfo"))
                        if (line.StartsWith("model name", StringComparison.OrdinalIgnoreCase) || line.StartsWith("Model", StringComparison.Ordinal))
                            return line[(line.IndexOf(':') + 1)..].Trim();
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
            }
            return string.Empty;
        }

        /// <summary>
        /// Writes a finished benchmark to <c>Benchmark.json</c> in the player's profile (through <see cref="AtomicFile"/>, as every
        /// player file): the machine and every segment's figures. Never <c>Progress.json</c>, never the settings, never online.
        /// </summary>
        internal void SaveBenchmark(JsonObject result)
        {
            try
            {
                string path = Path.Combine(UserData.Directory, "Benchmark.json");
                //No backup: a benchmark is retaken, not restored
                AtomicFile.WriteText(path, result.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), null);
                Console.WriteLine($"[bench] saved {Path.GetFileName(path)}");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"[bench] not saved: {exception.Message}");
            }
        }
    }
}
