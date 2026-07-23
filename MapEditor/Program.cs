using System;

namespace MapEditor
{
    public static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            using var game = new MapEditor();
            if (args != null && args.Length > 0) game.StartupFilePath = args[0];
            game.Run();
        }
    }
}
