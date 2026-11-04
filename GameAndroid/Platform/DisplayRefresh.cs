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
    /// <b>The rate moves under a running game.</b> vivo's game mode takes the display from 120 Hz to 60 a few seconds after
    /// start, so a refresh read once at start is wrong for the rest of the run: the resolution probe's floor stayed at
    /// 120's, called the 60 the display presented too slow and lowered the 3D to 720 rows for nothing. So the refresh is
    /// read again once a second (<see cref="DisplayWatch"/>, on the game's own thread), and a change raises the callback
    /// the Game hands <see cref="WatchScale"/>, its hook for "the display under the window changed", which reads it again.
    /// A display listener was tried first and missed a switch made before it was registered.
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

            if (_changed != null && TryRead(out int hz) && hz != _lastHz) _changed();
        }

        private static bool TryRead(out int hz)
        {
            hz = 0;
            Display display = CurrentDisplay();
            if (display == null || display.RefreshRate < 10f) return false;
            hz = (int)MathF.Round(display.RefreshRate);
            return true;
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
