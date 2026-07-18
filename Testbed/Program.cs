using System;

namespace Testbed
{
    public static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string startupMapPath = null;
            bool autoShoot = false;

            foreach (string arg in args)
            {
                if (string.Equals(arg, "autoshoot", StringComparison.OrdinalIgnoreCase)) autoShoot = true;
                else startupMapPath = arg;
            }

            using (var game = new Testbed(startupMapPath: startupMapPath, autoShoot: autoShoot)) game.Run();
        }
    }
}
