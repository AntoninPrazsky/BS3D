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

            //The map or level to open is the first argument that is not one of the three options (#1209 added them, so
            //the editor's own screen can be photographed at a stated size the way the Testbed's is): width= and height=
            //size the window's client area, shot= saves frames on a schedule in seconds. An argument is an option only by
            //its exact prefix, so a file path stays a file path.
            string filePath = null;
            int width = DEFAULT_WINDOW_WIDTH, height = DEFAULT_WINDOW_HEIGHT;
            float[] shotSeconds = null;

            foreach (string arg in args ?? Array.Empty<string>())
            {
                if (TryOption(arg, "width=", out string value)) width = PositiveOr(value, width);
                else if (TryOption(arg, "height=", out value)) height = PositiveOr(value, height);
                else if (TryOption(arg, "shot=", out value)) shotSeconds = ScreenshotWriter.ParseSeconds(value);
                else filePath ??= arg;
            }

            using var game = new MapEditor(windowed: true, width, height)
            {
                StartupFilePath = filePath,
                ShotSeconds = shotSeconds,
            };
            game.Run();
        }

        private static bool TryOption(string arg, string prefix, out string value)
        {
            bool match = arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            value = match ? arg.Substring(prefix.Length) : null;
            return match;
        }

        //A value that will not parse, or is not above zero, keeps the default rather than failing the launch
        private static int PositiveOr(string value, int fallback) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0 ? parsed : fallback;
    }
}
