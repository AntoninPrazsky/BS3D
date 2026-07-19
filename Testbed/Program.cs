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

            foreach (string arg in args)
            {
                if (string.Equals(arg, "autoshoot", StringComparison.OrdinalIgnoreCase)) autoShoot = true;
                else if (arg.StartsWith("switchmap=", StringComparison.OrdinalIgnoreCase)) switchMapPath = arg.Substring("switchmap=".Length);
                else startupMapPath = arg;
            }

            using (var game = new Testbed(startupMapPath: startupMapPath, autoShoot: autoShoot, switchMapPath: switchMapPath)) game.Run();
        }
    }
}
