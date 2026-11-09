using System;
using Android.Views;
using Microsoft.Xna.Framework;

namespace BS3D.Platform
{
    /// <summary>
    /// The Android build's namesake of <c>Game/Platform/DisplayRefresh.cs</c> (#1048): the display's refresh from the
    /// activity's display, which <c>MainActivity</c> has asked to run at its fastest mode. A phone has no DPI awareness
    /// to report and no bit depth the Game asks about.
    /// </summary>
    /// <remarks>
    /// <b>The rate moves under a running game.</b> A vivo holds an app at 60 Hz until its player allows it a high refresh
    /// rate in the phone's settings, and switches the display from 120 Hz about a second after the app starts (#1070;
    /// nothing the app declares or asks for lifts it, "GameAndroid" in <c>docs/build-and-release.md</c>). So a refresh
    /// read once at start is wrong for the rest of the run: the limiter aimed at 124 FPS on a display presenting 60, and
    /// the resolution probe's floor, then at 120's, called that 60 too slow and lowered the 3D to 720 rows for nothing.
    /// So the refresh is read again once a second (<see cref="DisplayWatch"/>, on the game's own thread), and a change
    /// raises the callback the Game hands <see cref="WatchScale"/>, its hook for "the display under the window changed",
    /// which reads it again. A display listener was tried first and missed a switch made before it was registered.
    /// </remarks>
    internal static class DisplayRefresh
    {
        //How often the refresh is read again, in seconds of game time: a switch is noticed within one of these, well
        //inside the resolution probe's ten seconds under its floor
        private const float WATCH_SECONDS = 1f;

        private static Action _changed;
        private static int _lastHz;
        private static float _sinceWatch;

        internal static bool TryGetForWindow(IntPtr windowHandle, out int refreshHz)
        {
            refreshHz = 0;
            if (!TryRead(out int hz)) return false;
            refreshHz = _lastHz = hz;
            return true;
        }

        internal static bool TryGetScale(IntPtr windowHandle, out float scale, out bool perMonitorAware)
        {
            scale = 1f;
            perMonitorAware = false;
            return false;
        }

        internal static void WatchScale(IntPtr windowHandle, Action changed) => _changed = changed;

        internal static bool TryGetBitsPerColor(IntPtr windowHandle, out int bitsPerColor)
        {
            bitsPerColor = 0;
            return false;
        }

        /// <summary>Reads the refresh again once every <see cref="WATCH_SECONDS"/> and raises the Game's callback on a change.</summary>
        internal static void Watch(float elapsed)
        {
            _sinceWatch += elapsed;
            if (_sinceWatch < WATCH_SECONDS) return;
            _sinceWatch = 0f;

            ReadCutout();

            if (_changed != null && TryRead(out int hz) && hz != _lastHz) _changed();
        }

        /// <summary>
        /// How far the camera's cutout reaches into the left and the right edge, in pixels (#1055): the window's own safe
        /// insets, read with the refresh once a second (<see cref="Watch"/>), since the phone held the other way round
        /// moves the hole to the other edge. The vivo X300 Ultra's punch-hole is 121 px deep at the middle of the edge.
        /// </summary>
        internal static int CutoutLeft { get; private set; }

        /// <inheritdoc cref="CutoutLeft"/>
        internal static int CutoutRight { get; private set; }

        private static void ReadCutout()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(28)) return;

            DisplayCutout cutout = Game.Activity?.Window?.DecorView?.RootWindowInsets?.DisplayCutout;
            int left = cutout?.SafeInsetLeft ?? 0, right = cutout?.SafeInsetRight ?? 0;
            if (left == CutoutLeft && right == CutoutRight) return;

            CutoutLeft = left;
            CutoutRight = right;
            Console.WriteLine($"[display] the camera's cutout reaches {left} px into the left edge and {right} px into the right");
        }

        /// <summary>
        /// The display's physical height in inches (#1052): the short side's pixels over its dots per inch, the phone being
        /// held landscape. What sizes the interface for a screen held close (<c>BS3DGame.UiScale</c>).
        /// </summary>
        internal static bool TryGetPhysicalHeightInches(out float inches)
        {
            inches = 0f;
            Android.Util.DisplayMetrics metrics = Game.Activity?.Resources?.DisplayMetrics;
            if (metrics == null || metrics.Ydpi < 50f || metrics.Xdpi < 50f) return false;

            //The short side, whichever way the metrics are read, at its own axis's density
            float shortPixels = Math.Min(metrics.WidthPixels, metrics.HeightPixels);
            float dpi = metrics.HeightPixels <= metrics.WidthPixels ? metrics.Ydpi : metrics.Xdpi;
            inches = shortPixels / dpi;
            return inches > 0.5f;
        }

        /// <summary>
        /// The fastest refresh among the display's modes at its current size, 0 until a display answers (#1070). What the
        /// phone would run the game at if its player allowed the app a high refresh rate: a refresh of 60 under a
        /// <c>FastestHz</c> of 120 is the phone's own hold, and the FPS limit row says so (<c>PhoneTiers.FpsLimitLabel</c>).
        /// </summary>
        internal static int FastestHz { get; private set; }

        private static bool TryRead(out int hz)
        {
            hz = 0;
            Display display = CurrentDisplay();
            if (display == null || display.RefreshRate < 10f) return false;
            hz = (int)MathF.Round(display.RefreshRate);

            //Once: a display's modes do not change under a run, and the list is an allocation
            if (FastestHz == 0) FastestHz = ReadFastest(display);
            return true;
        }

        private static int ReadFastest(Display display)
        {
            Display.Mode current = display.GetMode();
            Display.Mode[] modes = display.GetSupportedModes();
            if (current == null || modes == null) return 0;

            float fastest = 0f;
            foreach (Display.Mode mode in modes)
                if (mode.PhysicalWidth == current.PhysicalWidth && mode.PhysicalHeight == current.PhysicalHeight && mode.RefreshRate > fastest)
                    fastest = mode.RefreshRate;
            return (int)MathF.Round(fastest);
        }

        private static Display CurrentDisplay()
        {
            Android.App.Activity activity = Game.Activity;
            if (activity == null) return null;
            if (OperatingSystem.IsAndroidVersionAtLeast(30)) return activity.Display;
#pragma warning disable CA1422, CS0618 //The window manager's display is how an activity asked before API 30
            return activity.WindowManager?.DefaultDisplay;
#pragma warning restore CA1422, CS0618
        }
    }

    /// <summary>Gives <see cref="DisplayRefresh.Watch"/> the game's own thread and clock, once an update.</summary>
    internal sealed class DisplayWatch : GameComponent
    {
        internal DisplayWatch(Game game) : base(game) { }

        public override void Update(GameTime gameTime) => DisplayRefresh.Watch((float)gameTime.ElapsedGameTime.TotalSeconds);
    }
}
