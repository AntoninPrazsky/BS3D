using System;
using System.Globalization;

namespace BS3D
{
    public static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool fullscreen = false;
            bool uncappedFps = false;
            int supersampleFactor = 2;
            float exposure = 0f;

            foreach (string arg in args)
            {
                if (string.Equals(arg, "fullscreen", StringComparison.OrdinalIgnoreCase)) fullscreen = true;
                //"nocap" disables vsync so real rendering headroom can be measured
                else if (string.Equals(arg, "nocap", StringComparison.OrdinalIgnoreCase)) uncappedFps = true;
                //"ssaa=<n>" trades sharpness against fill rate; "exposure=<f>" is the renderer's shutter speed
                else if (arg.StartsWith("ssaa=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring("ssaa=".Length), out int parsedSsaa)) supersampleFactor = parsedSsaa;
                else if (arg.StartsWith("exposure=", StringComparison.OrdinalIgnoreCase) && float.TryParse(arg.Substring("exposure=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedExposure)) exposure = parsedExposure;
            }

            using var game = new BS3DGame(fullscreen: fullscreen, supersampleFactor: supersampleFactor, exposure: exposure, uncappedFps: uncappedFps);
            game.Run();
        }
    }
}
