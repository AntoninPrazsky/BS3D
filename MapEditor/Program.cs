using System;

namespace MapEditor
{
    public static class Program
    {
        [STAThread]
        private static void Main()
        {
            using (var game = new MapEditor()) game.Run();
        }
    }
}
