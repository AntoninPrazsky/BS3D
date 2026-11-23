using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Globalization;

namespace MapEditor
{
    public static class Program
    {
        //The window the editor opens in when the command line names none
        private const int DEFAULT_WINDOW_WIDTH = 1280;
        private const int DEFAULT_WINDOW_HEIGHT = 800;

        [STAThread]
        private static void Main(string[] args)
        {
            //What this run IS, before anything else (#372). The editor gets its compiled shaders from
            //Prazsky.Shaders like the other two (#618), so it is exposed to the same trap they are: MGCB skips
            //an .fx whose .xnb is newer and then copies nothing, and a look judged here would be a look judged
            //through the previous shader.
            BuildStamp.Report();

            //The map or level to open is the first argument that is not an option. The options are there so the editor's
            //own screen can be photographed the way the Testbed's is (#1209, #1210): width= and height= size the window's
            //client area, shot= saves frames on a schedule in seconds, campos= and camtarget= place the camera (in the
            //editor's grid frame, the one the selector and the six views work in), and notext starts with the text hidden,
            //as F12 hides it. gamesky=, island= and glass= start a view switch off (G, I, C), and save= writes the file
            //opened here back out as F4 would, once it is applied: the same level saved with the switches on and off is how a
            //run shows they are a view and not data. An argument is an option only by its exact prefix, so a file path stays
            //a file path.
            string filePath = null, savePath = null;
            int width = DEFAULT_WINDOW_WIDTH, height = DEFAULT_WINDOW_HEIGHT;
            float[] shotSeconds = null;
            Vector3? cameraPosition = null, cameraTarget = null;
            bool hideText = false, gameSky = true, island = true, glass = true;

            foreach (string arg in args ?? Array.Empty<string>())
            {
                if (TryOption(arg, "width=", out string value)) width = PositiveOr(value, width);
                else if (TryOption(arg, "height=", out value)) height = PositiveOr(value, height);
                else if (TryOption(arg, "shot=", out value)) shotSeconds = ScreenshotWriter.ParseSeconds(value);
                else if (TryOption(arg, "campos=", out value)) cameraPosition = VectorOr(value, cameraPosition);
                else if (TryOption(arg, "camtarget=", out value)) cameraTarget = VectorOr(value, cameraTarget);
                else if (arg.Equals("notext", StringComparison.OrdinalIgnoreCase)) hideText = true;
                else if (TryOption(arg, "gamesky=", out value)) gameSky = OnOr(value, gameSky);
                else if (TryOption(arg, "island=", out value)) island = OnOr(value, island);
                else if (TryOption(arg, "glass=", out value)) glass = OnOr(value, glass);
                else if (TryOption(arg, "save=", out value)) savePath = value.Length > 0 ? value : savePath;
                else filePath ??= arg;
            }

            using var game = new MapEditor(windowed: true, width, height)
            {
                StartupFilePath = filePath,
                ShotSeconds = shotSeconds,
                StartCameraPosition = cameraPosition,
                StartCameraTarget = cameraTarget,
                StartWithTextHidden = hideText,
                ShowGameSky = gameSky,
                ShowIsland = island,
                ShowCeilingGlass = glass,
                SaveLevelOnStart = savePath,
            };
            game.Run();
        }

        //on/off, 1/0 or true/false; anything else keeps the default rather than failing the launch
        private static bool OnOr(string value, bool fallback) => value.ToLowerInvariant() switch
        {
            "on" or "1" or "true" => true,
            "off" or "0" or "false" => false,
            _ => fallback,
        };

        private static bool TryOption(string arg, string prefix, out string value)
        {
            bool match = arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            value = match ? arg.Substring(prefix.Length) : null;
            return match;
        }

        //"x,y,z" in invariant numbers; anything else keeps what was there rather than failing the launch
        private static Vector3? VectorOr(string value, Vector3? fallback)
        {
            string[] parts = value.Split(',');
            if (parts.Length != 3) return fallback;

            float[] xyz = new float[3];
            for (int i = 0; i < 3; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out xyz[i])) return fallback;

            return new Vector3(xyz[0], xyz[1], xyz[2]);
        }

        //A value that will not parse, or is not above zero, keeps the default rather than failing the launch
        private static int PositiveOr(string value, int fallback) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0 ? parsed : fallback;
    }
}
