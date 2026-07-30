using Prazsky.Core.Render;
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
            float exposure = 0f;

            //Left null when absent, so the game keeps doing what it normally does: a random one of the seven
            //scenes, and whatever dome that scene wants. Pinning both is what makes a frame-cost measurement
            //repeatable (see BS3DGame.LogFrameRate) â€” without it every run measures a different backdrop.
            SceneKind? scene = null;
            byte? skyDome = null;
            bool logFrameRate = false;

            //Null means "nobody said", so the adaptive path is free to measure this machine and step the tier
            //down. Naming one settles it, exactly as naming ssaa= does.
            QualityLevel? quality = null;

            //Left null when "ssaa=" is absent, which is how the game tells "the player wants two" from "nobody
            //said" — only the latter may be lowered for a machine that cannot afford the default
            int? supersampleFactor = null;

            //Testing only: start the victory display on the front end, which is otherwise reachable only by
            //clearing a level.
            bool celebrate = false;

            //Testing only: pin the floor alarm's laser net on. Reaching it honestly means playing a level to
            //within two ceiling steps of losing it, which can no more be scripted than clearing one can.
            bool lasers = false;

            //Testing only: start with the master volume at zero. A scripted screenshot or benchmark run has
            //no business making noise, and nothing is persisted, so there is no settings file to pre-set.
            bool mute = false;

            foreach (string arg in args)
            {
                if (string.Equals(arg, "fullscreen", StringComparison.OrdinalIgnoreCase)) fullscreen = true;
                //"nocap" disables vsync so real rendering headroom can be measured
                else if (string.Equals(arg, "nocap", StringComparison.OrdinalIgnoreCase)) uncappedFps = true;
                //"ssaa=<n>" trades sharpness against fill rate; "exposure=<f>" is the renderer's shutter speed
                else if (arg.StartsWith("ssaa=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring("ssaa=".Length), out int parsedSsaa)) supersampleFactor = parsedSsaa;
                else if (arg.StartsWith("exposure=", StringComparison.OrdinalIgnoreCase) && float.TryParse(arg.Substring("exposure=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedExposure)) exposure = parsedExposure;
                //"logfps" writes one frame-rate line a second to stdout; "scene="/"sky=" pin what is being
                //measured. The scene names are the Testbed's, so one benchmark script drives either executable.
                else if (string.Equals(arg, "logfps", StringComparison.OrdinalIgnoreCase)) logFrameRate = true;
                else if (arg.StartsWith("scene=", StringComparison.OrdinalIgnoreCase) && TryParseScene(arg.Substring("scene=".Length), out SceneKind parsedScene)) scene = parsedScene;
                else if (arg.StartsWith("sky=", StringComparison.OrdinalIgnoreCase) && byte.TryParse(arg.Substring("sky=".Length), out byte parsedSky) && parsedSky >= 1 && parsedSky <= BS3DGame.SKY_DOME_COUNT) skyDome = parsedSky;
                //"quality=" pins the whole detail tier; "ssaa=" then overrides just its supersample entry.
                else if (arg.StartsWith("quality=", StringComparison.OrdinalIgnoreCase) && Enum.TryParse(arg.Substring("quality=".Length), ignoreCase: true, out QualityLevel parsedQuality)) quality = parsedQuality;
                //"celebrate" fires the victory display at startup. Clearing a level is the only thing that
                //normally starts it, and clearing one cannot be scripted, so without this the fireworks can be
                //neither screenshotted nor measured — the same reason autoshoot and aimshoot exist.
                else if (string.Equals(arg, "celebrate", StringComparison.OrdinalIgnoreCase)) celebrate = true;
                //"lasers" pins the floor alarm's laser net on while a level is played, for the same reason.
                else if (string.Equals(arg, "lasers", StringComparison.OrdinalIgnoreCase)) lasers = true;
                //"mute" starts silent, for the harnesses; the settings rows can still raise it.
                else if (string.Equals(arg, "mute", StringComparison.OrdinalIgnoreCase)) mute = true;
            }

            using var game = new BS3DGame(fullscreen: fullscreen, supersampleFactor: supersampleFactor, exposure: exposure,
                uncappedFps: uncappedFps, scene: scene, skyDome: skyDome, logFrameRate: logFrameRate, quality: quality,
                celebrate: celebrate, lasers: lasers, mute: mute);
            game.Run();
        }

        /// <summary>
        /// The scene names the Testbed's own <c>scene=</c> takes, so a benchmark or a screenshot script written
        /// against one executable drives the other unchanged. <c>neon</c> rather than <c>neoncity</c> for the
        /// same reason: it is the name that already exists.
        /// </summary>
        private static bool TryParseScene(string name, out SceneKind scene)
        {
            switch (name.ToLowerInvariant())
            {
                case "city": scene = SceneKind.City; return true;
                case "sea": scene = SceneKind.Sea; return true;
                case "savanna": scene = SceneKind.Savanna; return true;
                case "desert": scene = SceneKind.Desert; return true;
                case "mountain": scene = SceneKind.Mountain; return true;
                case "meadow": scene = SceneKind.Meadow; return true;
                case "neon": scene = SceneKind.NeonCity; return true;
                case "forest": scene = SceneKind.Forest; return true;
                case "space": scene = SceneKind.Space; return true;
                case "dream": scene = SceneKind.Dream; return true;
                default: scene = default; return false;
            }
        }
    }
}
