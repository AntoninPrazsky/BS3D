using Microsoft.Xna.Framework;
using System;
using System.Globalization;

namespace Testbed
{
    public static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string startupMapPath = null;
            string switchMapPath = null;
            bool autoShoot = false;
            bool aimCheck = false;
            bool aimShoot = false;
            bool uncappedFps = false;
            byte skyNumber = 0;
            int supersampleFactor = 2;
            float exposure = 0f;
            string scene = null;
            Vector3? camPos = null;
            Vector3? camTarget = null;

            //Testing: "width=<n>"/"height=<n>" set the windowed back buffer directly, so a screenshot can be
            //captured at an arbitrary resolution regardless of the physical monitor — the desktop's 4K play
            //experience from any machine (#141). Absent, the 1600×900 windowed default stands.
            int windowWidth = 1600;
            int windowHeight = 900;

            foreach (string arg in args)
            {
                if (string.Equals(arg, "autoshoot", StringComparison.OrdinalIgnoreCase)) autoShoot = true;
                //Testing: "aimcheck" logs per map whether the cannon can aim at every cell; "aimshoot" auto-enters
                //game mode and fires up the field's centre column so the aim + shoot path is exercised end to end
                else if (string.Equals(arg, "aimcheck", StringComparison.OrdinalIgnoreCase)) aimCheck = true;
                else if (string.Equals(arg, "aimshoot", StringComparison.OrdinalIgnoreCase)) { aimShoot = true; aimCheck = true; }
                else if (string.Equals(arg, "nocap", StringComparison.OrdinalIgnoreCase)) uncappedFps = true;
                else if (arg.StartsWith("switchmap=", StringComparison.OrdinalIgnoreCase)) switchMapPath = arg.Substring("switchmap=".Length);
                else if (arg.StartsWith("sky=", StringComparison.OrdinalIgnoreCase) && byte.TryParse(arg.Substring("sky=".Length), out byte parsedSky)) skyNumber = parsedSky;
                else if (arg.StartsWith("ssaa=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring("ssaa=".Length), out int parsedSsaa)) supersampleFactor = parsedSsaa;
                else if (arg.StartsWith("exposure=", StringComparison.OrdinalIgnoreCase) && float.TryParse(arg.Substring("exposure=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedExposure)) exposure = parsedExposure;
                else if (arg.StartsWith("scene=", StringComparison.OrdinalIgnoreCase)) scene = arg.Substring("scene=".Length);
                else if (arg.StartsWith("width=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring("width=".Length), out int parsedWidth) && parsedWidth > 0) windowWidth = parsedWidth;
                else if (arg.StartsWith("height=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring("height=".Length), out int parsedHeight) && parsedHeight > 0) windowHeight = parsedHeight;
                //Testing: "campos=x,y,z" / "camtarget=x,y,z" place and aim the free camera at startup, so a
                //screenshot can be taken from any vantage (e.g. under the sea, or close in on the drain).
                else if (arg.StartsWith("campos=", StringComparison.OrdinalIgnoreCase) && TryParseVec3(arg.Substring("campos=".Length), out Vector3 parsedPos)) camPos = parsedPos;
                else if (arg.StartsWith("camtarget=", StringComparison.OrdinalIgnoreCase) && TryParseVec3(arg.Substring("camtarget=".Length), out Vector3 parsedTarget)) camTarget = parsedTarget;
                else startupMapPath = arg;
            }

            using (var game = new Testbed(startupMapPath: startupMapPath, autoShoot: autoShoot, aimCheck: aimCheck, aimShoot: aimShoot, switchMapPath: switchMapPath, skyNumber: skyNumber, uncappedFps: uncappedFps, supersampleFactor: supersampleFactor, exposure: exposure, scene: scene, camPos: camPos, camTarget: camTarget, windowWidth: windowWidth, windowHeight: windowHeight)) game.Run();
        }

        //Parses "x,y,z" (invariant, so a decimal point) into a Vector3
        private static bool TryParseVec3(string s, out Vector3 result)
        {
            result = Vector3.Zero;
            string[] parts = s.Split(',');
            if (parts.Length != 3) return false;
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) return false;
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) return false;
            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) return false;
            result = new Vector3(x, y, z);
            return true;
        }
    }
}
