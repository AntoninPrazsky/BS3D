using System;

namespace Testbed
{
    public static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string startupMapPath = args.Length > 0 ? args[0] : null;
            using (var game = new Testbed(startupMapPath: startupMapPath)) game.Run();
        }
    }
}
