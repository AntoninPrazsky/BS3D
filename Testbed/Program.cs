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
            bool uncappedFps = false;
            byte skyNumber = 0;
            int supersampleFactor = 2;
            float exposure = 0f;

            foreach (string arg in args)
            {
                if (string.Equals(arg, "autoshoot", StringComparison.OrdinalIgnoreCase)) autoShoot = true;
                else if (string.Equals(arg, "nocap", StringComparison.OrdinalIgnoreCase)) uncappedFps = true;
                else if (arg.StartsWith("switchmap=", StringComparison.OrdinalIgnoreCase)) switchMapPath = arg.Substring("switchmap=".Length);
                else if (arg.StartsWith("sky=", StringComparison.OrdinalIgnoreCase) && byte.TryParse(arg.Substring("sky=".Length), out byte parsedSky)) skyNumber = parsedSky;
                else if (arg.StartsWith("ssaa=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring("ssaa=".Length), out int parsedSsaa)) supersampleFactor = parsedSsaa;
                else if (arg.StartsWith("exposure=", StringComparison.OrdinalIgnoreCase) && float.TryParse(arg.Substring("exposure=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedExposure)) exposure = parsedExposure;
                else startupMapPath = arg;
            }

            using (var game = new Testbed(startupMapPath: startupMapPath, autoShoot: autoShoot, switchMapPath: switchMapPath, skyNumber: skyNumber, uncappedFps: uncappedFps, supersampleFactor: supersampleFactor, exposure: exposure)) game.Run();
        }
    }
}
