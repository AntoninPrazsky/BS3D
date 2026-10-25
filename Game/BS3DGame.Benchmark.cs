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

        /// <summary>Whether a benchmark is running: the limiter lets go and the quality probe waits.</summary>
        internal bool Benchmarking => _benchmarking;

        internal void OpenBenchmark() => OpenBenchmark(start: false);

        /// <summary>The page, and with <paramref name="start"/> the run at once (the <c>benchmark</c> argument's).</summary>
        internal void OpenBenchmark(bool start)
        {
            OpenPage(_benchmarkPage ??= new BenchmarkPage(this));
            if (start) _benchmarkPage.StartNow();
        }

        /// <summary>Lets go of the frame limit and the quality probe and remembers the scene, to put it back in <see cref="EndBenchmark"/>.</summary>
        internal void BeginBenchmark()
        {
            if (_benchmarking) return;
            _benchmarking = true;
            _sceneBeforeBenchmark = _scene;
            Console.WriteLine("[bench] begin");
        }

        /// <summary>The frame limit and the probe back as they were, and the scene the player had.</summary>
        internal void EndBenchmark()
        {
            if (!_benchmarking) return;
            _benchmarking = false;
            SetScene(_sceneBeforeBenchmark);
            Console.WriteLine("[bench] end");
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
