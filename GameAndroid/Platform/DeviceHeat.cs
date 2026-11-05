using System;
using Android.OS;
using Android.Runtime;

namespace BS3D.Platform
{
    /// <summary>
    /// The Android build's namesake of <c>Game/Platform/DeviceHeat.cs</c> (#1060): the PowerManager's thermal status
    /// (Android 10 and up; 0 before it), asked at most once a second, because Auto quality reads it every frame and a
    /// status changes over minutes (#1061: Low at 1080 rows reached moderate in minute 7).
    /// </summary>
    internal static class DeviceHeat
    {
        internal const int MODERATE = 2;
        internal const int SEVERE = 3;

        private const long ASK_EVERY_MS = 1000;

        private static PowerManager _power;
        //Never long.MinValue: "now - _askedAt" overflows to a negative and the status was never asked at all (#1060)
        private static long _askedAt = -ASK_EVERY_MS;
        private static int _status;
        private static bool _reported;

        internal static int Status
        {
            get
            {
                long now = System.Environment.TickCount64;
                if (now - _askedAt < ASK_EVERY_MS) return _status;
                _askedAt = now;

                if (!OperatingSystem.IsAndroidVersionAtLeast(29)) return _status = 0;

                try
                {
                    _power ??= Android.App.Application.Context.GetSystemService(Android.Content.Context.PowerService).JavaCast<PowerManager>();
                    _status = _power == null ? 0 : (int)_power.CurrentThermalStatus;
                    if (!_reported) Console.WriteLine($"[heat] thermal status {_status}, from the PowerManager once a second");
                }
                catch (Exception ex)
                {
                    _status = 0;
                    if (!_reported) Console.WriteLine($"[heat] no thermal status ({ex.GetType().Name}: {ex.Message}): Auto quality reads the frame alone");
                }
                _reported = true;

                return _status;
            }
        }
    }
}
