using System;

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
            byte skyNumber = 0;

            foreach (string arg in args)
            {
                if (string.Equals(arg, "autoshoot", StringComparison.OrdinalIgnoreCase)) autoShoot = true;
                else if (arg.StartsWith("switchmap=", StringComparison.OrdinalIgnoreCase)) switchMapPath = arg.Substring("switchmap=".Length);
                else if (arg.StartsWith("sky=", StringComparison.OrdinalIgnoreCase) && byte.TryParse(arg.Substring("sky=".Length), out byte parsedSky)) skyNumber = parsedSky;
                else startupMapPath = arg;
            }

            using (var game = new Testbed(startupMapPath: startupMapPath, autoShoot: autoShoot, switchMapPath: switchMapPath, skyNumber: skyNumber)) game.Run();
        }
    }
}
