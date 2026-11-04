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
    internal static class DisplayRefresh
    {
        internal static bool TryGetForWindow(IntPtr windowHandle, out int refreshHz)
        {
            refreshHz = 0;
            Display display = CurrentDisplay();
            if (display == null || display.RefreshRate < 10f) return false;
            refreshHz = (int)MathF.Round(display.RefreshRate);
            return true;
        }

        internal static bool TryGetScale(IntPtr windowHandle, out float scale, out bool perMonitorAware)
        {
            scale = 1f;
            perMonitorAware = false;
            return false;
        }

        internal static void WatchScale(IntPtr windowHandle, Action changed)
        {
        }

        internal static bool TryGetBitsPerColor(IntPtr windowHandle, out int bitsPerColor)
        {
            bitsPerColor = 0;
            return false;
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
}
