using System;

namespace Testbed
{
    public static class Program
    {
        [STAThread]
        private static void Main()
        {
            using (var game = new Testbed())
                game.Run();
        }
    }
}
